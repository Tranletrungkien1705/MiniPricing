using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;
namespace MiniPricing.Services;
// ===== Danh mục đặc tính hàng hóa (port từ Mst_Attribute — 2019.4.ProductCenter) =====
// Nghiệp vụ nguồn: đây là danh mục gốc của bảng giá, khai báo các đặc tính (Attribute)
// dùng để phân loại hàng hóa/quy cách phục vụ áp giá.
// Mỗi dòng có mã đặc tính + OrgID (+ NetworkID), tên đặc tính, mã giải pháp và cờ hiệu lực.
// Quy tắc nguồn (Mst_Attribute_CreateX/UpdateX/DeleteX + Mst_Attribute_CheckDB/CheckAttributeName):
//   - Create: AttributeCode bắt buộc (nếu rỗng → Mst_Attribute_Create_InvalidAttributeCode);
//             AttributeCode chưa tồn tại (nếu đã có → Mst_Attribute_CheckDB_AttributeExist);
//             AttributeName bắt buộc (nếu rỗng → Mst_Attribute_Create_InvalidAttributeName);
//             AttributeName chưa tồn tại trong cùng NetworkID (Mst_Attribute_CheckDB_AttributeExist).
//   - Update: AttributeCode phải tồn tại (nếu không → Mst_Attribute_CheckDB_AttributeNotFound);
//             AttributeName không rỗng (→ Mst_Attribute_UpdateX_InvalidAttributeName);
//             nếu đổi tên thì tên mới chưa tồn tại trong cùng NetworkID.
//   - Delete: AttributeCode phải tồn tại (Mst_Attribute_CheckDB_AttributeNotFound).
public record UpsertAttributeDto(
    string AttributeCode, string? NetworkID,
    string? AttributeName, string? SolutionCode, bool? FlagActive);
public interface IAttributeService
{
    Task<object> UpsertAsync(UpsertAttributeDto dto);
    Task<object> ListAsync(string? attributeCode, string? attributeName);
    Task<object?> DeleteAsync(string attributeCode, string networkId);
    Task<object> ResolveAsync(string attributeCode, string? networkId);
}
public sealed class AttributeService(AppDbContext db, ITenantContext tenant) : IAttributeService
{
    private Guid Org => tenant.OrgId;
    private static string Net(string? networkId) =>
        string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();

    public async Task<object> UpsertAsync(UpsertAttributeDto dto)
    {
        var code = dto.AttributeCode.Trim().ToUpperInvariant();
        var net = Net(dto.NetworkID);
        var name = dto.AttributeName?.Trim() ?? "";
        // AttributeCode bắt buộc (Mst_Attribute_Create_InvalidAttributeCode).
        if (code.Length < 1) throw new InvalidOperationException("Mã Đặc tính Attribute không hợp lệ.");
        var row = await db.Attributes.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.AttributeCode == code && x.NetworkID == net);
        if (row is null)
        {
            // Create: AttributeCode chưa tồn tại (Mst_Attribute_CheckDB_AttributeExist).
            if (await db.Attributes.AnyAsync(x => x.OrgId == Org && x.AttributeCode == code))
                throw new InvalidOperationException($"Đặc tính Attribute '{code}' đã tồn tại.");
            // AttributeName bắt buộc (Mst_Attribute_Create_InvalidAttributeName).
            if (name.Length < 1) throw new InvalidOperationException("Tên Đặc tính Attribute không hợp lệ.");
            // AttributeName chưa tồn tại trong cùng NetworkID (Mst_Attribute_CheckDB_AttributeExist).
            if (await db.Attributes.AnyAsync(x => x.OrgId == Org && x.NetworkID == net && x.AttributeName == name))
                throw new InvalidOperationException($"Tên Đặc tính Attribute '{name}' đã tồn tại.");
            row = new AttributeDef { OrgId = Org, AttributeCode = code, NetworkID = net };
            db.Attributes.Add(row);
        }
        else
        {
            // Update: AttributeName không được rỗng (Mst_Attribute_UpdateX_InvalidAttributeName).
            if (name.Length < 1) throw new InvalidOperationException("Tên Đặc tính Attribute không hợp lệ.");
            // Nếu đổi tên thì tên mới chưa tồn tại trong cùng NetworkID (Mst_Attribute_CheckAttributeName).
            if (!string.Equals(row.AttributeName, name, StringComparison.OrdinalIgnoreCase)
                && await db.Attributes.AnyAsync(x => x.OrgId == Org && x.NetworkID == net && x.AttributeName == name))
                throw new InvalidOperationException($"Tên Đặc tính Attribute '{name}' đã tồn tại.");
        }
        var entity = row!;
        entity.AttributeName = name;
        entity.SolutionCode = string.IsNullOrWhiteSpace(dto.SolutionCode) ? null : dto.SolutionCode!.Trim();
        entity.FlagActive = dto.FlagActive ?? true;
        entity.LogLUDTimeUTC = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Project(entity);
    }

    public async Task<object> ListAsync(string? attributeCode, string? attributeName)
    {
        var q = db.Attributes.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(attributeCode))
        {
            var c = attributeCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.AttributeCode == c);
        }
        if (!string.IsNullOrWhiteSpace(attributeName))
        {
            var n = attributeName!.Trim();
            q = q.Where(x => x.AttributeName.Contains(n));
        }
        var rows = await q.OrderBy(x => x.AttributeCode).ThenBy(x => x.NetworkID).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    public async Task<object?> DeleteAsync(string attributeCode, string networkId)
    {
        var c = attributeCode.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var row = await db.Attributes.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.AttributeCode == c && x.NetworkID == net);
        if (row is null) return null;   // Mst_Attribute_CheckDB_AttributeNotFound
        db.Attributes.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, attributeCode = c, network = net };
    }

    // Tra đặc tính hiệu lực: ưu tiên NetworkID khớp rồi tới "ALL"; chỉ lấy dòng FlagActive.
    public async Task<object> ResolveAsync(string attributeCode, string? networkId)
    {
        var c = attributeCode.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var candidates = await db.Attributes.Where(x => x.OrgId == Org && x.AttributeCode == c && x.FlagActive)
            .ToListAsync();
        var row = candidates.FirstOrDefault(x => x.NetworkID == net)
               ?? candidates.FirstOrDefault(x => x.NetworkID == "ALL");
        if (row is null) return new { found = false, attributeCode = c, network = net };
        return new
        {
            found = true, attributeCode = row.AttributeCode, network = row.NetworkID,
            attributeName = row.AttributeName, solutionCode = row.SolutionCode, flagActive = row.FlagActive
        };
    }

    private static object Project(AttributeDef x) => new
    {
        attributeCode = x.AttributeCode, x.NetworkID, x.AttributeName,
        x.SolutionCode, x.FlagActive
    };
}
