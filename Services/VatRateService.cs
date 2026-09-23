using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;

namespace MiniPricing.Services;

// ===== Danh mục thuế suất VAT (port từ Mst_VATRate — 2019.4.ProductCenter) =====
// Nghiệp vụ nguồn: mỗi mã thuế suất (VATRateCode) × kênh (NetworkID) có % thuế suất,
// mô tả và cờ hiệu lực. SpecPrice tham chiếu tới đây qua VATRateCode để tính giá gồm VAT.
// Quy tắc nguồn (Mst_VATRate_CheckDB): Create → mã chưa tồn tại; Update/Delete → mã phải tồn tại.

public record UpsertVatRateDto(
    string VATRateCode, string? NetworkID,
    decimal VATRate, string? VATDesc, bool? FlagActive);

public interface IVatRateService
{
    Task<object> UpsertAsync(UpsertVatRateDto dto);
    Task<object> ListAsync(string? vatRateCode);
    Task<object?> DeleteAsync(string vatRateCode, string networkId);
    Task<object> ResolveAsync(string vatRateCode, string? networkId);
}

public sealed class VatRateService(AppDbContext db, ITenantContext tenant) : IVatRateService
{
    private Guid Org => tenant.OrgId;

    public async Task<object> UpsertAsync(UpsertVatRateDto dto)
    {
        var code = dto.VATRateCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(dto.NetworkID) ? "ALL" : dto.NetworkID!.Trim().ToUpperInvariant();
        if (code.Length < 1) throw new InvalidOperationException("Cần VATRateCode.");   // Mst_VATRate_Create_InvalidVATRateCode
        if (dto.VATRate < 0 || dto.VATRate > 100) throw new InvalidOperationException("Thuế suất phải trong khoảng 0–100%.");

        var row = await db.VatRates.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.VATRateCode == code && x.NetworkID == net);
        if (row is null)
        {
            row = new VatRate { OrgId = Org, VATRateCode = code, NetworkID = net };
            db.VatRates.Add(row);
        }
        row.VATRate = dto.VATRate;
        row.VATDesc = dto.VATDesc?.Trim() ?? "";
        row.FlagActive = dto.FlagActive ?? true;
        row.LogLUDTimeUTC = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Project(row);
    }

    public async Task<object> ListAsync(string? vatRateCode)
    {
        var q = db.VatRates.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(vatRateCode))
        {
            var c = vatRateCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.VATRateCode == c);
        }
        var rows = await q.OrderBy(x => x.VATRateCode).ThenBy(x => x.NetworkID).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    public async Task<object?> DeleteAsync(string vatRateCode, string networkId)
    {
        var code = vatRateCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId.Trim().ToUpperInvariant();
        var row = await db.VatRates.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.VATRateCode == code && x.NetworkID == net);
        if (row is null) return null;   // Mst_VATRate_CheckDB_VATRateCodeNotFound
        db.VatRates.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, vatRateCode = code, network = net };
    }

    // Tra thuế suất hiệu lực: ưu tiên NetworkID khớp rồi tới "ALL"; chỉ lấy dòng FlagActive.
    public async Task<object> ResolveAsync(string vatRateCode, string? networkId)
    {
        var code = vatRateCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();

        var candidates = await db.VatRates.Where(x => x.OrgId == Org && x.VATRateCode == code && x.FlagActive)
            .ToListAsync();
        var row = candidates.FirstOrDefault(x => x.NetworkID == net)
               ?? candidates.FirstOrDefault(x => x.NetworkID == "ALL");
        if (row is null) return new { found = false, vatRateCode = code, network = net };

        return new
        {
            found = true, vatRateCode = row.VATRateCode, network = row.NetworkID,
            vatRate = row.VATRate, vatDesc = row.VATDesc, flagActive = row.FlagActive
        };
    }

    private static object Project(VatRate x) => new
    {
        x.VATRateCode, x.NetworkID, x.VATRate, x.VATDesc, x.FlagActive
    };
}
