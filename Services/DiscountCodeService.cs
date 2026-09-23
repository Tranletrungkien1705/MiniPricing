using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;

namespace MiniPricing.Services;

// ===== Mã giảm giá / chiết khấu (port từ Inos_DiscountCode — 2019.4.ProductCenter) =====
// Nghiệp vụ nguồn: mỗi mã (Code) có loại giảm giá (Percent=1 / Absolute=2), giá trị giảm,
// số lượng còn lại, cờ Enabled và khoảng hiệu lực. Khi áp vào đơn, mã phải tồn tại
// (Mst_NNT_Calc_InvalidInosCreateOrder_DiscountCodeNotFound) và phải Enabled
// (Mst_NNT_Calc_InvalidInosCreateOrder_InvalidDiscountStatus).

public record UpsertDiscountCodeDto(
    string Code, string? DiscountType, decimal DiscountAmount,
    int? RemainQty, string? Description, bool? Enabled,
    DateTime? EffectDateFrom, DateTime? EffectDateTo);

public interface IDiscountCodeService
{
    Task<object> UpsertAsync(UpsertDiscountCodeDto dto);
    Task<object> ListAsync(string? code, bool? enabled);
    Task<object?> DeleteAsync(string code);
    Task<object> ResolveAsync(string code, DateTime? date);
    Task<object> ApplyAsync(string code, decimal amount, DateTime? date);
}

public sealed class DiscountCodeService(AppDbContext db, ITenantContext tenant) : IDiscountCodeService
{
    private Guid Org => tenant.OrgId;

    // Chuẩn hoá loại giảm giá về Percent/Absolute (nguồn: Inos_DiscountCodeTypes Percent=1, Absolute=2).
    private static string NormalizeType(string? type)
    {
        var t = (type ?? "Percent").Trim();
        if (t.Equals("1", StringComparison.OrdinalIgnoreCase) || t.Equals("Percent", StringComparison.OrdinalIgnoreCase))
            return "Percent";
        if (t.Equals("2", StringComparison.OrdinalIgnoreCase) || t.Equals("Absolute", StringComparison.OrdinalIgnoreCase))
            return "Absolute";
        throw new InvalidOperationException("DiscountType phải là Percent hoặc Absolute.");
    }

    public async Task<object> UpsertAsync(UpsertDiscountCodeDto dto)
    {
        var code = dto.Code.Trim().ToUpperInvariant();
        if (code.Length < 1) throw new InvalidOperationException("Cần Code.");
        var type = NormalizeType(dto.DiscountType);
        if (dto.DiscountAmount < 0) throw new InvalidOperationException("DiscountAmount không được âm.");
        if (type == "Percent" && dto.DiscountAmount > 100) throw new InvalidOperationException("Giảm giá theo % phải trong khoảng 0–100.");

        var row = await db.DiscountCodes.FirstOrDefaultAsync(x => x.OrgId == Org && x.Code == code);
        if (row is null)
        {
            row = new DiscountCode { OrgId = Org, Code = code };
            db.DiscountCodes.Add(row);
        }
        row.DiscountType = type;
        row.DiscountAmount = dto.DiscountAmount;
        row.RemainQty = dto.RemainQty ?? row.RemainQty;
        row.Description = dto.Description?.Trim() ?? "";
        row.Enabled = dto.Enabled ?? true;
        row.EffectDateFrom = dto.EffectDateFrom ?? row.EffectDateFrom;
        row.EffectDateTo = dto.EffectDateTo;
        row.LogLUDTimeUTC = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Project(row);
    }

    public async Task<object> ListAsync(string? code, bool? enabled)
    {
        var q = db.DiscountCodes.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(code))
        {
            var c = code!.Trim().ToUpperInvariant();
            q = q.Where(x => x.Code == c);
        }
        if (enabled.HasValue) q = q.Where(x => x.Enabled == enabled.Value);
        var rows = await q.OrderBy(x => x.Code).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    public async Task<object?> DeleteAsync(string code)
    {
        var c = code.Trim().ToUpperInvariant();
        var row = await db.DiscountCodes.FirstOrDefaultAsync(x => x.OrgId == Org && x.Code == c);
        if (row is null) return null;
        db.DiscountCodes.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, code = c };
    }

    // Tra mã giảm giá hiệu lực: phải tồn tại, Enabled và nằm trong khoảng hiệu lực.
    public async Task<object> ResolveAsync(string code, DateTime? date)
    {
        var c = code.Trim().ToUpperInvariant();
        var at = date ?? DateTime.Now;
        var row = await db.DiscountCodes.FirstOrDefaultAsync(x => x.OrgId == Org && x.Code == c);
        if (row is null) return new { found = false, code = c, reason = "DiscountCodeNotFound" };
        if (!row.Enabled) return new { found = false, code = c, reason = "InvalidDiscountStatus" };
        if (at < row.EffectDateFrom || (row.EffectDateTo.HasValue && at > row.EffectDateTo.Value))
            return new { found = false, code = c, reason = "OutOfEffectDate" };

        return new
        {
            found = true, code = row.Code, discountType = row.DiscountType,
            discountAmount = row.DiscountAmount, remainQty = row.RemainQty,
            description = row.Description, enabled = row.Enabled,
            effectDateFrom = row.EffectDateFrom, effectDateTo = row.EffectDateTo
        };
    }

    // Áp mã giảm giá lên một số tiền: trả số tiền giảm + số tiền sau giảm.
    public async Task<object> ApplyAsync(string code, decimal amount, DateTime? date)
    {
        var c = code.Trim().ToUpperInvariant();
        var at = date ?? DateTime.Now;
        var row = await db.DiscountCodes.FirstOrDefaultAsync(x => x.OrgId == Org && x.Code == c);
        if (row is null) throw new InvalidOperationException("DiscountCodeNotFound");
        if (!row.Enabled) throw new InvalidOperationException("InvalidDiscountStatus");
        if (at < row.EffectDateFrom || (row.EffectDateTo.HasValue && at > row.EffectDateTo.Value))
            throw new InvalidOperationException("OutOfEffectDate");
        if (row.RemainQty <= 0) throw new InvalidOperationException("DiscountCodeOutOfQty");

        var discount = row.DiscountType == "Percent"
            ? Math.Round(amount * row.DiscountAmount / 100m, 2)
            : row.DiscountAmount;
        if (discount > amount) discount = amount;
        var net = amount - discount;

        row.RemainQty -= 1;
        row.LogLUDTimeUTC = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return new
        {
            code = row.Code, discountType = row.DiscountType, discountAmount = row.DiscountAmount,
            amount, discount, netAmount = net, remainQty = row.RemainQty
        };
    }

    private static object Project(DiscountCode x) => new
    {
        x.Code, x.DiscountType, x.DiscountAmount, x.RemainQty, x.Description,
        x.Enabled, x.EffectDateFrom, x.EffectDateTo
    };
}
