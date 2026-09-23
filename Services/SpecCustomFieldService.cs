using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;
namespace MiniPricing.Services;
// ===== Trường mở rộng của quy cách (port từ Mst_SpecCustomField — 2019.4.ProductCenter) =====
// Nghiệp vụ nguồn: đây là danh mục gốc của bảng giá — khai báo các trường mở rộng
// (CustomField1..10) gắn vào quy cách (Mst_Spec) để lưu thêm thuộc tính phục vụ phân loại/áp giá.
// Quy tắc nguồn (Mst_SpecCustomField_CheckDB_New20190629 + error codes):
//   - Create: SpecCustomFieldCode bắt buộc & chưa tồn tại trong org
//             (Mst_SpecCustomField_CheckDB_CustomFieldExist);
//             SpecCustomFieldName bắt buộc.
//   - Update: SpecCustomFieldCode phải tồn tại
//             (Mst_SpecCustomField_CheckDB_CustomFieldNotFound);
//             nếu đổi tên thì tên không rỗng
//             (Mst_SpecCustomField_Update_InvalidSpecCustomFieldName).
//   - Delete: SpecCustomFieldCode phải tồn tại.
public record UpsertSpecCustomFieldDto(
    string SpecCustomFieldCode, string? NetworkID,
    string? SpecCustomFieldName, string? DBPhysicalType, string? Remark, bool? FlagActive);
public interface ISpecCustomFieldService
{
    Task<object> UpsertAsync(UpsertSpecCustomFieldDto dto);
    Task<object> ListAsync(string? code, string? name);
    Task<object?> DeleteAsync(string code, string networkId);
    Task<object> ResolveAsync(string code, string? networkId);
}
public sealed class SpecCustomFieldService(AppDbContext db, ITenantContext tenant) : ISpecCustomFieldService
{
    private Guid Org => tenant.OrgId;
    private static string Net(string? networkId) =>
        string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();

    public async Task<object> UpsertAsync(UpsertSpecCustomFieldDto dto)
    {
        var code = dto.SpecCustomFieldCode.Trim().ToUpperInvariant();
        var net = Net(dto.NetworkID);
        var name = dto.SpecCustomFieldName?.Trim() ?? "";
        // SpecCustomFieldCode bắt buộc.
        if (code.Length < 1) throw new InvalidOperationException("Mã trường mở rộng không hợp lệ.");
        var row = await db.SpecCustomFields.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.SpecCustomFieldCode == code && x.NetworkID == net);
        if (row is null)
        {
            // Create: SpecCustomFieldCode chưa tồn tại trong org
            // (Mst_SpecCustomField_CheckDB_CustomFieldExist).
            if (await db.SpecCustomFields.AnyAsync(x => x.OrgId == Org && x.SpecCustomFieldCode == code))
                throw new InvalidOperationException($"Trường mở rộng '{code}' đã tồn tại.");
            // SpecCustomFieldName bắt buộc.
            if (name.Length < 1) throw new InvalidOperationException("Tên trường mở rộng không hợp lệ.");
            row = new SpecCustomField { OrgId = Org, SpecCustomFieldCode = code, NetworkID = net };
            db.SpecCustomFields.Add(row);
        }
        else
        {
            // Update: nếu đổi tên thì tên không rỗng
            // (Mst_SpecCustomField_Update_InvalidSpecCustomFieldName).
            if (name.Length < 1) throw new InvalidOperationException("Tên trường mở rộng không hợp lệ.");
        }
        var entity = row!;
        entity.SpecCustomFieldName = name;
        entity.DBPhysicalType = dto.DBPhysicalType?.Trim() ?? "";
        entity.Remark = dto.Remark?.Trim();
        entity.FlagActive = dto.FlagActive ?? true;
        entity.LogLUDTimeUTC = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Project(entity);
    }

    public async Task<object> ListAsync(string? code, string? name)
    {
        var q = db.SpecCustomFields.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(code))
        {
            var c = code!.Trim().ToUpperInvariant();
            q = q.Where(x => x.SpecCustomFieldCode == c);
        }
        if (!string.IsNullOrWhiteSpace(name))
        {
            var n = name!.Trim();
            q = q.Where(x => x.SpecCustomFieldName.Contains(n));
        }
        var rows = await q.OrderBy(x => x.SpecCustomFieldCode).ThenBy(x => x.NetworkID).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    public async Task<object?> DeleteAsync(string code, string networkId)
    {
        var c = code.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var row = await db.SpecCustomFields.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.SpecCustomFieldCode == c && x.NetworkID == net);
        if (row is null) return null;   // Mst_SpecCustomField_CheckDB_CustomFieldNotFound
        db.SpecCustomFields.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, specCustomFieldCode = c, network = net };
    }

    // Tra trường mở rộng hiệu lực: ưu tiên NetworkID khớp rồi tới "ALL"; chỉ lấy dòng FlagActive.
    public async Task<object> ResolveAsync(string code, string? networkId)
    {
        var c = code.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var candidates = await db.SpecCustomFields.Where(x => x.OrgId == Org
            && x.SpecCustomFieldCode == c && x.FlagActive).ToListAsync();
        var row = candidates.FirstOrDefault(x => x.NetworkID == net)
               ?? candidates.FirstOrDefault(x => x.NetworkID == "ALL");
        if (row is null) return new { found = false, specCustomFieldCode = c, network = net };
        return new
        {
            found = true, specCustomFieldCode = row.SpecCustomFieldCode, network = row.NetworkID,
            specCustomFieldName = row.SpecCustomFieldName, dbPhysicalType = row.DBPhysicalType,
            remark = row.Remark, flagActive = row.FlagActive
        };
    }

    private static object Project(SpecCustomField x) => new
    {
        specCustomFieldCode = x.SpecCustomFieldCode, x.NetworkID, x.SpecCustomFieldName,
        x.DBPhysicalType, x.Remark, x.FlagActive
    };
}
