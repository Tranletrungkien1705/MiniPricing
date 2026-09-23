using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;

namespace MiniPricing.Services;

// ===== Tỷ giá ngoại tệ (port từ Mst_CurrencyEx — 2019.4.ProductCenter) =====
// Nghiệp vụ nguồn: mỗi mã tiền tệ (CurrencyCode) × kênh (NetworkID) có tên tiền tệ,
// tiền tệ gốc (BaseCurrencyCode), tỷ giá mua (BuyRate) / tỷ giá bán (SellRate),
// thời điểm cập nhật và ghi chú. Dùng để quy đổi giá giữa các loại tiền tệ.
// Quy tắc nguồn (Mst_CurrencyEx_CheckDB + Create):
//   - Create: CurrencyCode chưa tồn tại (nếu có → CurrencyCodeExist).
//   - Update/Delete: CurrencyCode phải tồn tại (nếu không → CurrencyCodeNotFound).
//   - CurrencyName bắt buộc (nếu rỗng → InvalidCurrencyName).
//   - Nếu có BaseCurrencyCode thì tiền tệ gốc phải tồn tại.

public record UpsertCurrencyExDto(
    string CurrencyCode, string? NetworkID,
    string CurrencyName, string? BaseCurrencyCode,
    decimal BuyRate, decimal SellRate,
    string? InterEx, string? Remark, bool? FlagActive);

public interface ICurrencyExService
{
    Task<object> UpsertAsync(UpsertCurrencyExDto dto);
    Task<object> ListAsync(string? currencyCode);
    Task<object?> DeleteAsync(string currencyCode, string networkId);
    Task<object> ResolveAsync(string currencyCode, string? networkId);
    Task<object> ConvertAsync(string currencyCode, decimal amount, string? networkId, string? side);
}

public sealed class CurrencyExService(AppDbContext db, ITenantContext tenant) : ICurrencyExService
{
    private Guid Org => tenant.OrgId;

    public async Task<object> UpsertAsync(UpsertCurrencyExDto dto)
    {
        var code = dto.CurrencyCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(dto.NetworkID) ? "ALL" : dto.NetworkID!.Trim().ToUpperInvariant();
        if (code.Length < 1) throw new InvalidOperationException("Cần CurrencyCode.");   // Mst_CurrencyEx_CheckDB_CurrencyCodeNotFound
        if (string.IsNullOrWhiteSpace(dto.CurrencyName)) throw new InvalidOperationException("Cần CurrencyName."); // Mst_CurrencyEx_Create_InvalidCurrencyName
        if (dto.BuyRate < 0 || dto.SellRate < 0) throw new InvalidOperationException("Tỷ giá không được âm.");

        var baseCode = dto.BaseCurrencyCode?.Trim().ToUpperInvariant() ?? "";
        if (baseCode.Length > 0 && baseCode != code)
        {
            // Tiền tệ gốc phải tồn tại (Mst_CurrencyEx_CheckDB_CurrencyCodeNotFound).
            var baseExists = await db.CurrencyExes.AnyAsync(x => x.OrgId == Org && x.CurrencyCode == baseCode);
            if (!baseExists) throw new InvalidOperationException($"Không tìm thấy tiền tệ gốc '{baseCode}'.");
        }

        var row = await db.CurrencyExes.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.CurrencyCode == code && x.NetworkID == net);
        if (row is null)
        {
            row = new CurrencyEx { OrgId = Org, CurrencyCode = code, NetworkID = net };
            db.CurrencyExes.Add(row);
        }
        row.CurrencyName = dto.CurrencyName.Trim();
        row.BaseCurrencyCode = baseCode;
        row.BuyRate = dto.BuyRate;
        row.SellRate = dto.SellRate;
        row.InterEx = dto.InterEx?.Trim() ?? "";
        row.Remark = dto.Remark?.Trim();
        row.FlagActive = dto.FlagActive ?? true;
        row.UpdatedTime = DateTime.Now;
        row.LogLUDTimeUTC = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Project(row);
    }

    public async Task<object> ListAsync(string? currencyCode)
    {
        var q = db.CurrencyExes.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(currencyCode))
        {
            var c = currencyCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.CurrencyCode == c);
        }
        var rows = await q.OrderBy(x => x.CurrencyCode).ThenBy(x => x.NetworkID).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    public async Task<object?> DeleteAsync(string currencyCode, string networkId)
    {
        var code = currencyCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId.Trim().ToUpperInvariant();
        var row = await db.CurrencyExes.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.CurrencyCode == code && x.NetworkID == net);
        if (row is null) return null;   // Mst_CurrencyEx_CheckDB_CurrencyCodeNotFound
        db.CurrencyExes.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, currencyCode = code, network = net };
    }

    // Tra tỷ giá hiệu lực: ưu tiên NetworkID khớp rồi tới "ALL"; chỉ lấy dòng FlagActive.
    public async Task<object> ResolveAsync(string currencyCode, string? networkId)
    {
        var code = currencyCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();

        var candidates = await db.CurrencyExes.Where(x => x.OrgId == Org && x.CurrencyCode == code && x.FlagActive)
            .ToListAsync();
        var row = candidates.FirstOrDefault(x => x.NetworkID == net)
               ?? candidates.FirstOrDefault(x => x.NetworkID == "ALL");
        if (row is null) return new { found = false, currencyCode = code, network = net };

        return new
        {
            found = true, currencyCode = row.CurrencyCode, network = row.NetworkID,
            currencyName = row.CurrencyName, baseCurrencyCode = row.BaseCurrencyCode,
            buyRate = row.BuyRate, sellRate = row.SellRate, interEx = row.InterEx,
            updatedTime = row.UpdatedTime, flagActive = row.FlagActive
        };
    }

    // Quy đổi số tiền sang tiền tệ gốc theo tỷ giá hiệu lực.
    // side = "buy" (mua ngoại tệ) hoặc "sell" (bán ngoại tệ); mặc định "sell".
    public async Task<object> ConvertAsync(string currencyCode, decimal amount, string? networkId, string? side)
    {
        var code = currencyCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();
        var useBuy = string.Equals(side?.Trim(), "buy", StringComparison.OrdinalIgnoreCase);

        var candidates = await db.CurrencyExes.Where(x => x.OrgId == Org && x.CurrencyCode == code && x.FlagActive)
            .ToListAsync();
        var row = candidates.FirstOrDefault(x => x.NetworkID == net)
               ?? candidates.FirstOrDefault(x => x.NetworkID == "ALL");
        if (row is null) return new { found = false, currencyCode = code, network = net };

        var rate = useBuy ? row.BuyRate : row.SellRate;
        var converted = amount * rate;
        return new
        {
            found = true, currencyCode = row.CurrencyCode, network = row.NetworkID,
            baseCurrencyCode = row.BaseCurrencyCode, side = useBuy ? "buy" : "sell",
            rate, amount, convertedAmount = converted
        };
    }

    private static object Project(CurrencyEx x) => new
    {
        x.CurrencyCode, x.NetworkID, x.CurrencyName, x.BaseCurrencyCode,
        x.BuyRate, x.SellRate, x.UpdatedTime, x.InterEx, x.Remark, x.FlagActive
    };
}
