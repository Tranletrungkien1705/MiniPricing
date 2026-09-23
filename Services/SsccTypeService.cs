using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;
namespace MiniPricing.Services;
// ===== Danh mục loại SSCC (port từ Mst_SSCCType — 2019.4.ProductCenter) =====
// Nghiệp vụ nguồn: đây là danh mục gốc của bảng giá — Product tham chiếu tới qua SSCCType
// (mã loại SSCC dùng để đóng gói/định danh lô hàng khi ghi giá và xuất kho).
// Quy tắc nguồn (Mst_SSCCType_CheckDB + error codes):
//   - Create: SSCCType bắt buộc & chưa tồn tại (Mst_SSCCType_CheckDB_SSCCTypeExist);
//             SSCCTypeName bắt buộc.
//   - Update: SSCCType phải tồn tại (Mst_SSCCType_CheckDB_SSCCTypeNotFound);
//             SSCCTypeName không rỗng.
//   - Resolve: trạng thái hiệu lực không khớp (Mst_SSCCType_CheckDB_FlagActiveNotMatched).
public record UpsertSsccTypeDto(string SSCCType, string? NetworkID, string? SSCCTypeName, bool? FlagActive);
public interface ISsccTypeService
{
    Task<object> UpsertAsync(UpsertSsccTypeDto dto);
    Task<object> ListAsync(string? code, string? name);
    Task<object?> DeleteAsync(string code, string networkId);
    Task<object> ResolveAsync(string code, string? networkId);
}
public sealed class SsccTypeService(AppDbContext db, ITenantContext tenant) : ISsccTypeService
{
    private Guid Org => tenant.OrgId;
    private static string Net(string? networkId) =>
        string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();

    public async Task<object> UpsertAsync(UpsertSsccTypeDto dto)
    {
        var code = dto.SSCCType.Trim().ToUpperInvariant();
        var net = Net(dto.NetworkID);
        var name = dto.SSCCTypeName?.Trim() ?? "";
        // SSCCType bắt buộc.
        if (code.Length < 1) throw new InvalidOperationException("Mã loại SSCC không hợp lệ.");
        var row = await db.SsccTypes.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.SSCCType == code && x.NetworkID == net);
        if (row is null)
        {
            // Create: SSCCType chưa tồn tại (Mst_SSCCType_CheckDB_SSCCTypeExist).
            if (await db.SsccTypes.AnyAsync(x => x.OrgId == Org && x.SSCCType == code))
                throw new InvalidOperationException($"Loại SSCC '{code}' đã tồn tại.");
            // SSCCTypeName bắt buộc.
            if (name.Length < 1) throw new InvalidOperationException("Tên loại SSCC không hợp lệ.");
            row = new SsccType { OrgId = Org, SSCCType = code, NetworkID = net };
            db.SsccTypes.Add(row);
        }
        else
        {
            // Update: SSCCTypeName không rỗng.
            if (name.Length < 1) throw new InvalidOperationException("Tên loại SSCC không hợp lệ.");
        }
        var entity = row!;
        entity.SSCCTypeName = name;
        entity.FlagActive = dto.FlagActive ?? true;
        entity.LogLUDTimeUTC = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Project(entity);
    }

    public async Task<object> ListAsync(string? code, string? name)
    {
        var q = db.SsccTypes.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(code))
        {
            var c = code!.Trim().ToUpperInvariant();
            q = q.Where(x => x.SSCCType == c);
        }
        if (!string.IsNullOrWhiteSpace(name))
        {
            var n = name!.Trim();
            q = q.Where(x => x.SSCCTypeName.Contains(n));
        }
        var rows = await q.OrderBy(x => x.SSCCType).ThenBy(x => x.NetworkID).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    public async Task<object?> DeleteAsync(string code, string networkId)
    {
        var c = code.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var row = await db.SsccTypes.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.SSCCType == c && x.NetworkID == net);
        if (row is null) return null;   // Mst_SSCCType_CheckDB_SSCCTypeNotFound
        // Không cho xoá nếu còn hàng hóa tham chiếu loại SSCC này (bảo toàn tham chiếu).
        if (await db.Products.AnyAsync(x => x.OrgId == Org && x.SSCCType == c))
            throw new InvalidOperationException($"Loại SSCC '{c}' còn hàng hóa tham chiếu, không thể xoá.");
        db.SsccTypes.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, ssccType = c, network = net };
    }

    // Tra loại SSCC hiệu lực: ưu tiên NetworkID khớp rồi tới "ALL"; chỉ lấy dòng FlagActive.
    public async Task<object> ResolveAsync(string code, string? networkId)
    {
        var c = code.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var candidates = await db.SsccTypes.Where(x => x.OrgId == Org && x.SSCCType == c && x.FlagActive)
            .ToListAsync();
        var row = candidates.FirstOrDefault(x => x.NetworkID == net)
               ?? candidates.FirstOrDefault(x => x.NetworkID == "ALL");
        if (row is null) return new { found = false, ssccType = c, network = net };
        return new
        {
            found = true, ssccType = row.SSCCType, network = row.NetworkID,
            ssccTypeName = row.SSCCTypeName, flagActive = row.FlagActive
        };
    }

    private static object Project(SsccType x) => new
    {
        ssccType = x.SSCCType, x.NetworkID, ssccTypeName = x.SSCCTypeName, x.FlagActive
    };
}
