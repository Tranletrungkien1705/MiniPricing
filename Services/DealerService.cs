using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;
namespace MiniPricing.Services;
// ===== Danh mục đại lý (port từ Mst_Dealer — 2019.4.ProductCenter) =====
// Nghiệp vụ nguồn: đây là danh mục gốc của bảng giá — đại lý là kênh nhận giá riêng
// (NetworkID = "DEALER"), dùng để áp giá theo kênh đại lý (SpecPrice/PriceItem theo
// NetworkID/tier Dealer). Mỗi dòng có mã đại lý + OrgID (+ NetworkID), tên đại lý,
// ghi chú và cờ hiệu lực.
// Quy tắc nguồn (Mst_Dealer_CheckDB + error codes ErrProductCenter.Mst_Dealer_*):
//   - Create: DLCode bắt buộc (nếu rỗng → Mst_Dealer_Create_InvalidDLCode);
//             DLCode chưa tồn tại (nếu đã có → Mst_Dealer_CheckDB_DLCodeExist);
//             DLName bắt buộc (nếu rỗng → Mst_Dealer_Create_InvalidDLName).
//   - Update: DLCode phải tồn tại (nếu không → Mst_Dealer_CheckDB_DLCodeNotFound);
//             DLName không rỗng (→ Mst_Dealer_Update_InvalidDLName).
//   - Delete: DLCode phải tồn tại (Mst_Dealer_CheckDB_DLCodeNotFound).
//   - Resolve: trạng thái hiệu lực không khớp (Mst_Dealer_CheckDB_FlagActiveNotMatched).
public record UpsertDealerDto(
    string DLCode, string? NetworkID,
    string? DLName, string? Remark, bool? FlagActive);
public interface IDealerService
{
    Task<object> UpsertAsync(UpsertDealerDto dto);
    Task<object> ListAsync(string? dlCode, string? dlName);
    Task<object?> DeleteAsync(string dlCode, string networkId);
    Task<object> ResolveAsync(string dlCode, string? networkId);
}
public sealed class DealerService(AppDbContext db, ITenantContext tenant) : IDealerService
{
    private Guid Org => tenant.OrgId;
    private static string Net(string? networkId) =>
        string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();

    public async Task<object> UpsertAsync(UpsertDealerDto dto)
    {
        var code = dto.DLCode.Trim().ToUpperInvariant();
        var net = Net(dto.NetworkID);
        var name = dto.DLName?.Trim() ?? "";
        // DLCode bắt buộc (Mst_Dealer_Create_InvalidDLCode).
        if (code.Length < 1) throw new InvalidOperationException("Mã đại lý không hợp lệ.");
        var row = await db.Dealers.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.DLCode == code && x.NetworkID == net);
        if (row is null)
        {
            // Create: DLCode chưa tồn tại (Mst_Dealer_CheckDB_DLCodeExist).
            if (await db.Dealers.AnyAsync(x => x.OrgId == Org && x.DLCode == code))
                throw new InvalidOperationException($"Mã đại lý '{code}' đã tồn tại trong hệ thống.");
            // DLName bắt buộc (Mst_Dealer_Create_InvalidDLName).
            if (name.Length < 1) throw new InvalidOperationException("Tên đại lý không hợp lệ.");
            row = new Dealer { OrgId = Org, DLCode = code, NetworkID = net };
            db.Dealers.Add(row);
        }
        else
        {
            // Update: DLName không rỗng (Mst_Dealer_Update_InvalidDLName).
            if (name.Length < 1) throw new InvalidOperationException("Tên đại lý không hợp lệ.");
        }
        var entity = row!;
        entity.DLName = name;
        entity.Remark = string.IsNullOrWhiteSpace(dto.Remark) ? null : dto.Remark!.Trim();
        entity.FlagActive = dto.FlagActive ?? true;
        entity.LogLUDTimeUTC = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Project(entity);
    }

    public async Task<object> ListAsync(string? dlCode, string? dlName)
    {
        var q = db.Dealers.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(dlCode))
        {
            var c = dlCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.DLCode == c);
        }
        if (!string.IsNullOrWhiteSpace(dlName))
        {
            var n = dlName!.Trim();
            q = q.Where(x => x.DLName.Contains(n));
        }
        var rows = await q.OrderBy(x => x.DLCode).ThenBy(x => x.NetworkID).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    public async Task<object?> DeleteAsync(string dlCode, string networkId)
    {
        var c = dlCode.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var row = await db.Dealers.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.DLCode == c && x.NetworkID == net);
        if (row is null) return null;   // Mst_Dealer_CheckDB_DLCodeNotFound
        db.Dealers.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, dlCode = c, network = net };
    }

    // Tra đại lý hiệu lực: ưu tiên NetworkID khớp rồi tới "ALL"; chỉ lấy dòng FlagActive.
    public async Task<object> ResolveAsync(string dlCode, string? networkId)
    {
        var c = dlCode.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var candidates = await db.Dealers.Where(x => x.OrgId == Org && x.DLCode == c && x.FlagActive)
            .ToListAsync();
        var row = candidates.FirstOrDefault(x => x.NetworkID == net)
               ?? candidates.FirstOrDefault(x => x.NetworkID == "ALL");
        if (row is null) return new { found = false, dlCode = c, network = net };
        return new
        {
            found = true, dlCode = row.DLCode, network = row.NetworkID,
            dlName = row.DLName, remark = row.Remark, flagActive = row.FlagActive
        };
    }

    private static object Project(Dealer x) => new
    {
        dlCode = x.DLCode, x.NetworkID, dlName = x.DLName, x.Remark, x.FlagActive
    };
}
