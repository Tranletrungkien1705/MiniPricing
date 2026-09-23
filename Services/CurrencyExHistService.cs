using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;

namespace MiniPricing.Services;

// ===== Lịch sử tỷ giá ngoại tệ (port từ Mst_CurrencyExHist — 2019.4.ProductCenter) =====
// Nghiệp vụ nguồn: mỗi lần Create/Update/Delete một dòng Mst_CurrencyEx, business layer
// (WAS_Mst_CurrencyEx_*) gọi Mst_CurrencyExHist_Perform để ghi một bản chụp (snapshot) toàn bộ
// trường tỷ giá vào bảng Mst_CurrencyExHist, kèm:
//   - FunctionName       : hàm gọi (vd WAS_Mst_CurrencyEx_Create/Update/Delete)
//   - FunctionActionType : ADD / UPDATE / DELETE (TConst.FunctionActionType)
//   - HistRefType        : 'MST_CURRENTCYEX' (giữ nguyên như nguồn)
//   - RefCode00..03      : mã tham chiếu phụ (nguồn để null)
// Bảng này là audit trail để truy vết biến động tỷ giá mua/bán theo thời gian.

public record RecordCurrencyExHistDto(
    string CurrencyCode, string? NetworkID,
    string? CurrencyName, decimal BuyRate, decimal SellRate,
    string? InterEx, string? Remark,
    string FunctionName, string FunctionActionType,
    string? FunctionRemark, string? LogLUBy,
    string? RefCode00, string? RefCode01, string? RefCode02, string? RefCode03);

public interface ICurrencyExHistService
{
    Task<object> RecordAsync(RecordCurrencyExHistDto dto);
    Task<object> ListAsync(string? currencyCode, string? actionType);
    Task<object> TimelineAsync(string currencyCode, string? networkId);
}

public sealed class CurrencyExHistService(AppDbContext db, ITenantContext tenant) : ICurrencyExHistService
{
    private Guid Org => tenant.OrgId;

    // Ghi một bản chụp lịch sử tỷ giá. ActionType chuẩn hoá về ADD/UPDATE/DELETE.
    public async Task<object> RecordAsync(RecordCurrencyExHistDto dto)
    {
        var code = dto.CurrencyCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(dto.NetworkID) ? "ALL" : dto.NetworkID!.Trim().ToUpperInvariant();
        if (code.Length < 1) throw new InvalidOperationException("Cần CurrencyCode.");

        var action = NormalizeAction(dto.FunctionActionType);
        var row = new CurrencyExHist
        {
            OrgId = Org,
            CurrencyCode = code,
            NetworkID = net,
            CurrencyName = dto.CurrencyName?.Trim() ?? "",
            BuyRate = dto.BuyRate,
            SellRate = dto.SellRate,
            UpdatedTime = DateTime.Now,
            InterEx = dto.InterEx?.Trim() ?? "",
            Remark = dto.Remark,
            LogLUDTimeUTC = DateTime.UtcNow,
            LogLUBy = dto.LogLUBy,
            FunctionName = dto.FunctionName?.Trim() ?? "",
            FunctionActionType = action,
            FunctionRemark = dto.FunctionRemark,
            HistRefType = "MST_CURRENTCYEX",
            RefCode00 = dto.RefCode00,
            RefCode01 = dto.RefCode01,
            RefCode02 = dto.RefCode02,
            RefCode03 = dto.RefCode03,
            CreatedAt = DateTime.Now
        };
        db.CurrencyExHists.Add(row);
        await db.SaveChangesAsync();
        return Project(row);
    }

    public async Task<object> ListAsync(string? currencyCode, string? actionType)
    {
        var q = db.CurrencyExHists.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(currencyCode))
        {
            var c = currencyCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.CurrencyCode == c);
        }
        if (!string.IsNullOrWhiteSpace(actionType))
        {
            var a = NormalizeAction(actionType);
            q = q.Where(x => x.FunctionActionType == a);
        }
        var rows = await q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    // Dòng thời gian biến động tỷ giá của một mã tiền tệ × kênh (mới nhất trước).
    public async Task<object> TimelineAsync(string currencyCode, string? networkId)
    {
        var code = currencyCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? null : networkId!.Trim().ToUpperInvariant();

        var q = db.CurrencyExHists.Where(x => x.OrgId == Org && x.CurrencyCode == code);
        if (net is not null) q = q.Where(x => x.NetworkID == net);
        var rows = await q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToListAsync();

        return new
        {
            currencyCode = code, network = net, count = rows.Count,
            items = rows.Select(x => new
            {
                x.NetworkID, x.CurrencyName, x.BuyRate, x.SellRate, x.InterEx,
                x.FunctionName, x.FunctionActionType, x.FunctionRemark, x.HistRefType,
                updatedTime = x.UpdatedTime.ToString("yyyy-MM-dd HH:mm:ss"),
                x.LogLUBy,
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

    private static object Project(CurrencyExHist x) => new
    {
        x.CurrencyCode, x.NetworkID, x.CurrencyName, x.BuyRate, x.SellRate,
        updatedTime = x.UpdatedTime.ToString("yyyy-MM-dd HH:mm:ss"),
        x.InterEx, x.Remark, x.FunctionName, x.FunctionActionType, x.FunctionRemark,
        x.HistRefType, x.RefCode00, x.RefCode01, x.RefCode02, x.RefCode03,
        x.LogLUBy, createdAt = x.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
    };
}
