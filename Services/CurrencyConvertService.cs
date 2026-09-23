using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;

namespace MiniPricing.Services;

// ===== Quy đổi tiền tệ theo hiệu lực (port từ Mst_CurrencyConvert — 2019.4.ProductCenter) =====
// Nghiệp vụ nguồn: mỗi cặp tiền tệ nguồn (CurrencyCode) → tiền tệ đích (CurrencyCodeV) × kênh
// (NetworkID) khai báo tỷ giá mua/bán (BuyRate/SellRate) và các giá trị quy đổi
// (ValConvert, ValConvertP, ValConvertToVND) kèm khoảng hiệu lực riêng
// (EffectDTimeStartV/EffectDTimeEndV). Dùng để quy đổi giá bán/giá mua giữa hai loại tiền tệ
// theo thời gian — khác Mst_CurrencyEx (chỉ lưu tỷ giá theo một mã tiền tệ).
// Quy tắc nguồn (Mst_CurrencyConvert_Save + Mst_Currency_CheckDB):
//   - Save: dữ liệu phải hợp lệ (nếu không → Mst_CurrencyConvert_Save_InvalidValue).
//   - Tiền tệ nguồn/đích phải tồn tại trong hệ thống (nếu không → Mst_CurrencyConvert_Save_InputTblNotFound).
//   - Tỷ giá và giá trị quy đổi không được âm.

public record UpsertCurrencyConvertDto(
    string CurrencyCode, string CurrencyCodeV, string? NetworkID,
    string? CurrencyNameV, string? BaseCurrencyCode,
    decimal BuyRate, decimal SellRate,
    decimal? ValConvert, decimal? ValConvertP, decimal? ValConvertToVND,
    DateTime? EffectDTimeStartV, DateTime? EffectDTimeEndV,
    string? Remark, bool? FlagActive);

public interface ICurrencyConvertService
{
    Task<object> UpsertAsync(UpsertCurrencyConvertDto dto);
    Task<object> ListAsync(string? currencyCode, string? currencyCodeV);
    Task<object?> DeleteAsync(string currencyCode, string currencyCodeV, string networkId, DateTime effectStart);
    Task<object> ResolveAsync(string currencyCode, string currencyCodeV, string? networkId, string? date);
    Task<object> ConvertAsync(string currencyCode, string currencyCodeV, decimal amount, string? networkId, string? date, string? side);
}

public sealed class CurrencyConvertService(AppDbContext db, ITenantContext tenant) : ICurrencyConvertService
{
    private Guid Org => tenant.OrgId;

    public async Task<object> UpsertAsync(UpsertCurrencyConvertDto dto)
    {
        var code = dto.CurrencyCode.Trim().ToUpperInvariant();
        var codeV = dto.CurrencyCodeV.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(dto.NetworkID) ? "ALL" : dto.NetworkID!.Trim().ToUpperInvariant();
        if (code.Length < 1 || codeV.Length < 1)
            throw new InvalidOperationException("Cần CurrencyCode và CurrencyCodeV.");   // Mst_CurrencyConvert_Save_InvalidValue
        if (code == codeV)
            throw new InvalidOperationException("Tiền tệ nguồn và đích phải khác nhau."); // Mst_CurrencyConvert_Save_InvalidValue
        if (dto.BuyRate < 0 || dto.SellRate < 0)
            throw new InvalidOperationException("Tỷ giá không được âm.");                  // Mst_CurrencyConvert_Save_InvalidValue
        if (dto.ValConvert is < 0 || dto.ValConvertP is < 0 || dto.ValConvertToVND is < 0)
            throw new InvalidOperationException("Giá trị quy đổi không được âm.");        // Mst_CurrencyConvert_Save_InvalidValue

        // Tiền tệ nguồn/đích phải tồn tại (Mst_CurrencyConvert_Save_InputTblNotFound).
        var srcExists = await db.CurrencyExes.AnyAsync(x => x.OrgId == Org && x.CurrencyCode == code);
        if (!srcExists) throw new InvalidOperationException($"Không tìm thấy tiền tệ nguồn '{code}'.");
        var dstExists = await db.CurrencyExes.AnyAsync(x => x.OrgId == Org && x.CurrencyCode == codeV);
        if (!dstExists) throw new InvalidOperationException($"Không tìm thấy tiền tệ đích '{codeV}'.");

        var start = dto.EffectDTimeStartV ?? DateTime.Now;
        if (dto.EffectDTimeEndV is { } end && end < start)
            throw new InvalidOperationException("Hiệu lực đến phải sau hiệu lực từ.");    // Mst_CurrencyConvert_Save_InvalidValue

        var row = await db.CurrencyConverts.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.CurrencyCode == code && x.CurrencyCodeV == codeV && x.NetworkID == net
            && x.EffectDTimeStartV == start);
        if (row is null)
        {
            row = new CurrencyConvert { OrgId = Org, CurrencyCode = code, CurrencyCodeV = codeV, NetworkID = net, EffectDTimeStartV = start };
            db.CurrencyConverts.Add(row);
        }
        row.CurrencyNameV = dto.CurrencyNameV?.Trim() ?? "";
        row.BaseCurrencyCode = dto.BaseCurrencyCode?.Trim().ToUpperInvariant() ?? "";
        row.BuyRate = dto.BuyRate;
        row.SellRate = dto.SellRate;
        row.ValConvert = dto.ValConvert ?? 0;
        row.ValConvertP = dto.ValConvertP ?? 0;
        row.ValConvertToVND = dto.ValConvertToVND ?? 0;
        row.EffectDTimeEndV = dto.EffectDTimeEndV;
        row.Remark = dto.Remark?.Trim();
        row.FlagActive = dto.FlagActive ?? true;
        row.LogLUDTimeUTC = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Project(row);
    }

    public async Task<object> ListAsync(string? currencyCode, string? currencyCodeV)
    {
        var q = db.CurrencyConverts.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(currencyCode))
        {
            var c = currencyCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.CurrencyCode == c);
        }
        if (!string.IsNullOrWhiteSpace(currencyCodeV))
        {
            var cv = currencyCodeV!.Trim().ToUpperInvariant();
            q = q.Where(x => x.CurrencyCodeV == cv);
        }
        var rows = await q.OrderBy(x => x.CurrencyCode).ThenBy(x => x.CurrencyCodeV)
            .ThenBy(x => x.NetworkID).ThenBy(x => x.EffectDTimeStartV).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    public async Task<object?> DeleteAsync(string currencyCode, string currencyCodeV, string networkId, DateTime effectStart)
    {
        var code = currencyCode.Trim().ToUpperInvariant();
        var codeV = currencyCodeV.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId.Trim().ToUpperInvariant();
        var row = await db.CurrencyConverts.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.CurrencyCode == code && x.CurrencyCodeV == codeV && x.NetworkID == net
            && x.EffectDTimeStartV == effectStart);
        if (row is null) return null;   // Mst_CurrencyConvert_Save_InputTblNotFound
        db.CurrencyConverts.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, currencyCode = code, currencyCodeV = codeV, network = net, effectStart };
    }

    // Tra tỷ giá quy đổi hiệu lực: ưu tiên NetworkID khớp rồi tới "ALL"; chỉ lấy dòng FlagActive
    // và còn hiệu lực tại thời điểm date (mặc định hiện tại).
    public async Task<object> ResolveAsync(string currencyCode, string currencyCodeV, string? networkId, string? date)
    {
        var code = currencyCode.Trim().ToUpperInvariant();
        var codeV = currencyCodeV.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();
        var at = ParseDate(date);

        var candidates = await db.CurrencyConverts.Where(x => x.OrgId == Org
            && x.CurrencyCode == code && x.CurrencyCodeV == codeV && x.FlagActive).ToListAsync();
        var row = PickEffective(candidates, net, at);
        if (row is null) return new { found = false, currencyCode = code, currencyCodeV = codeV, network = net };

        return new
        {
            found = true, currencyCode = row.CurrencyCode, currencyCodeV = row.CurrencyCodeV,
            network = row.NetworkID, currencyNameV = row.CurrencyNameV, baseCurrencyCode = row.BaseCurrencyCode,
            buyRate = row.BuyRate, sellRate = row.SellRate, valConvert = row.ValConvert,
            valConvertP = row.ValConvertP, valConvertToVND = row.ValConvertToVND,
            effectDTimeStartV = row.EffectDTimeStartV, effectDTimeEndV = row.EffectDTimeEndV,
            flagActive = row.FlagActive
        };
    }

    // Quy đổi số tiền từ tiền tệ nguồn sang tiền tệ đích theo tỷ giá hiệu lực.
    // side = "buy" (mua) hoặc "sell" (bán); mặc định "sell".
    public async Task<object> ConvertAsync(string currencyCode, string currencyCodeV, decimal amount, string? networkId, string? date, string? side)
    {
        var code = currencyCode.Trim().ToUpperInvariant();
        var codeV = currencyCodeV.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();
        var at = ParseDate(date);
        var useBuy = string.Equals(side?.Trim(), "buy", StringComparison.OrdinalIgnoreCase);

        var candidates = await db.CurrencyConverts.Where(x => x.OrgId == Org
            && x.CurrencyCode == code && x.CurrencyCodeV == codeV && x.FlagActive).ToListAsync();
        var row = PickEffective(candidates, net, at);
        if (row is null) return new { found = false, currencyCode = code, currencyCodeV = codeV, network = net };

        var rate = useBuy ? row.BuyRate : row.SellRate;
        return new
        {
            found = true, currencyCode = row.CurrencyCode, currencyCodeV = row.CurrencyCodeV,
            network = row.NetworkID, side = useBuy ? "buy" : "sell", rate, amount,
            convertedAmount = amount * rate
        };
    }

    private static DateTime ParseDate(string? date) =>
        DateTime.TryParse(date, out var d) ? d : DateTime.Now;

    // Chọn dòng hiệu lực: khớp NetworkID trước, rồi "ALL"; trong cùng kênh lấy bản mới nhất
    // có EffectDTimeStartV <= at và (EffectDTimeEndV null hoặc >= at).
    private static CurrencyConvert? PickEffective(List<CurrencyConvert> rows, string net, DateTime at)
    {
        var effective = rows.Where(x => x.EffectDTimeStartV <= at
            && (x.EffectDTimeEndV == null || x.EffectDTimeEndV >= at)).ToList();
        return effective.Where(x => x.NetworkID == net).OrderByDescending(x => x.EffectDTimeStartV).FirstOrDefault()
            ?? effective.Where(x => x.NetworkID == "ALL").OrderByDescending(x => x.EffectDTimeStartV).FirstOrDefault();
    }

    private static object Project(CurrencyConvert x) => new
    {
        x.CurrencyCode, x.CurrencyCodeV, x.NetworkID, x.CurrencyNameV, x.BaseCurrencyCode,
        x.BuyRate, x.SellRate, x.ValConvert, x.ValConvertP, x.ValConvertToVND,
        x.EffectDTimeStartV, x.EffectDTimeEndV, x.Remark, x.FlagActive
    };
}
