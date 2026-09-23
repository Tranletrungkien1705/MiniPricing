using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;

namespace MiniPricing.Services;

// ===== Lịch sử giá theo quy cách (port từ Mst_SpecPriceHist — 2019.4.ProductCenter) =====
// Nghiệp vụ nguồn: mỗi lần Create/Update/Delete một dòng Mst_SpecPrice, business layer
// (WAS_Mst_SpecPrice_*) gọi Mst_SpecPriceHist_Perform để ghi một bản chụp (snapshot) toàn bộ
// trường giá vào bảng Mst_SpecPriceHist, kèm:
//   - FunctionName       : hàm gọi (vd WAS_Mst_SpecPrice_Create/Update/Delete)
//   - FunctionActionType : ADD / UPDATE / DELETE (TConst.FunctionActionType)
//   - HistRefType        : 'MST_SPECPRICE'
//   - RefCode00..03      : mã tham chiếu phụ (nguồn để null)
// Bảng này là audit trail để truy vết biến động giá bán/giá mua/chiết khấu theo thời gian.

public record RecordSpecPriceHistDto(
    string SpecCode, string UnitCode, string? NetworkID,
    decimal BuyPrice, decimal SellPrice, decimal DiscountVND,
    string? CurrencyCode, string? VATRateCode,
    DateTime EffectDTimeStart, DateTime? EffectDTimeEnd, string? Remark,
    bool? FlagActive, string FunctionName, string FunctionActionType,
    string? FunctionRemark, string? LogLUBy,
    string? RefCode00, string? RefCode01, string? RefCode02, string? RefCode03);

public interface ISpecPriceHistService
{
    Task<object> RecordAsync(RecordSpecPriceHistDto dto);
    Task<object> ListAsync(string? specCode, string? unitCode, string? actionType);
    Task<object> TimelineAsync(string specCode, string? unitCode, string? networkId);
}

public sealed class SpecPriceHistService(AppDbContext db, ITenantContext tenant) : ISpecPriceHistService
{
    private Guid Org => tenant.OrgId;

    // Ghi một bản chụp lịch sử giá. ActionType chuẩn hoá về ADD/UPDATE/DELETE.
    public async Task<object> RecordAsync(RecordSpecPriceHistDto dto)
    {
        var spec = dto.SpecCode.Trim().ToUpperInvariant();
        var unit = dto.UnitCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(dto.NetworkID) ? "ALL" : dto.NetworkID!.Trim().ToUpperInvariant();
        if (spec.Length < 1) throw new InvalidOperationException("Cần SpecCode.");
        if (unit.Length < 1) throw new InvalidOperationException("Cần UnitCode.");

        var action = NormalizeAction(dto.FunctionActionType);
        var row = new SpecPriceHist
        {
            OrgId = Org,
            SpecCode = spec,
            UnitCode = unit,
            NetworkID = net,
            BuyPrice = dto.BuyPrice,
            SellPrice = dto.SellPrice,
            DiscountVND = dto.DiscountVND,
            CurrencyCode = string.IsNullOrWhiteSpace(dto.CurrencyCode) ? "VND" : dto.CurrencyCode!.Trim().ToUpperInvariant(),
            VATRateCode = string.IsNullOrWhiteSpace(dto.VATRateCode) ? "VAT10" : dto.VATRateCode!.Trim().ToUpperInvariant(),
            EffectDTimeStart = dto.EffectDTimeStart,
            EffectDTimeEnd = dto.EffectDTimeEnd,
            Remark = dto.Remark,
            FlagActive = dto.FlagActive ?? true,
            LogLUDTimeUTC = DateTime.UtcNow,
            LogLUBy = dto.LogLUBy,
            FunctionName = dto.FunctionName?.Trim() ?? "",
            FunctionActionType = action,
            FunctionRemark = dto.FunctionRemark,
            HistRefType = "MST_SPECPRICE",
            RefCode00 = dto.RefCode00,
            RefCode01 = dto.RefCode01,
            RefCode02 = dto.RefCode02,
            RefCode03 = dto.RefCode03,
            CreatedAt = DateTime.Now
        };
        db.SpecPriceHists.Add(row);
        await db.SaveChangesAsync();
        return Project(row);
    }

    public async Task<object> ListAsync(string? specCode, string? unitCode, string? actionType)
    {
        var q = db.SpecPriceHists.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(specCode))
        {
            var s = specCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.SpecCode == s);
        }
        if (!string.IsNullOrWhiteSpace(unitCode))
        {
            var u = unitCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.UnitCode == u);
        }
        if (!string.IsNullOrWhiteSpace(actionType))
        {
            var a = NormalizeAction(actionType);
            q = q.Where(x => x.FunctionActionType == a);
        }
        var rows = await q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    // Dòng thời gian biến động giá của một quy cách × đơn vị × kênh (mới nhất trước).
    public async Task<object> TimelineAsync(string specCode, string? unitCode, string? networkId)
    {
        var spec = specCode.Trim().ToUpperInvariant();
        var unit = string.IsNullOrWhiteSpace(unitCode) ? null : unitCode!.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? null : networkId!.Trim().ToUpperInvariant();

        var q = db.SpecPriceHists.Where(x => x.OrgId == Org && x.SpecCode == spec);
        if (unit is not null) q = q.Where(x => x.UnitCode == unit);
        if (net is not null) q = q.Where(x => x.NetworkID == net);
        var rows = await q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToListAsync();

        return new
        {
            specCode = spec, unitCode = unit, network = net, count = rows.Count,
            items = rows.Select(x => new
            {
                x.UnitCode, x.NetworkID, x.BuyPrice, x.SellPrice, x.DiscountVND,
                x.CurrencyCode, x.VATRateCode, x.FunctionName, x.FunctionActionType,
                x.FunctionRemark, x.HistRefType,
                effectFrom = x.EffectDTimeStart.ToString("yyyy-MM-dd"),
                effectTo = x.EffectDTimeEnd?.ToString("yyyy-MM-dd"),
                x.FlagActive, x.LogLUBy,
                createdAt = x.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            })
        };
    }

    // Chuẩn hoá loại hành động về ADD/UPDATE/DELETE (TConst.FunctionActionType).
    private static string NormalizeAction(string? action)
    {
        var a = (action ?? "").Trim().ToUpperInvariant();
        return a switch
        {
            "ADD" or "CREATE" or "INSERT" => "ADD",
            "UPDATE" or "UPD" or "EDIT" => "UPDATE",
            "DELETE" or "DEL" or "REMOVE" => "DELETE",
            _ => a.Length == 0 ? "UPDATE" : a
        };
    }

    private static object Project(SpecPriceHist x) => new
    {
        x.SpecCode, x.UnitCode, x.NetworkID, x.BuyPrice, x.SellPrice, x.DiscountVND,
        x.CurrencyCode, x.VATRateCode,
        effectFrom = x.EffectDTimeStart.ToString("yyyy-MM-dd"),
        effectTo = x.EffectDTimeEnd?.ToString("yyyy-MM-dd"),
        x.Remark, x.FlagActive, x.FunctionName, x.FunctionActionType, x.FunctionRemark,
        x.HistRefType, x.RefCode00, x.RefCode01, x.RefCode02, x.RefCode03,
        x.LogLUBy, createdAt = x.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
    };
}
