using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;
namespace MiniPricing.Services;
// ===== Danh mục loại hàng hóa (port từ Mst_ProductType — 2019.4.ProductCenter) =====
// Nghiệp vụ nguồn: đây là danh mục gốc của bảng giá — Product tham chiếu tới qua ProductType.
// Quy tắc nguồn (Mst_ProductType_CheckDB + error codes):
//   - Create: ProductType bắt buộc & chưa tồn tại (Mst_ProductType_Create_InvalidProductType /
//             Mst_ProductType_CheckDB_ProductTypeExist);
//             ProductTypeName bắt buộc (Mst_ProductType_Create_InvalidProductTypeName).
//   - Update: ProductType phải tồn tại (Mst_ProductType_CheckDB_ProductTypeNotFound);
//             ProductTypeName không rỗng (Mst_ProductType_UpdateX_InvalidProductTypeName).
public record UpsertProductTypeDto(string ProductTypeCode, string? NetworkID, string? ProductTypeName, bool? FlagActive);
public interface IProductTypeService
{
    Task<object> UpsertAsync(UpsertProductTypeDto dto);
    Task<object> ListAsync(string? code, string? name);
    Task<object?> DeleteAsync(string code, string networkId);
    Task<object> ResolveAsync(string code, string? networkId);
}
public sealed class ProductTypeService(AppDbContext db, ITenantContext tenant) : IProductTypeService
{
    private Guid Org => tenant.OrgId;
    private static string Net(string? networkId) =>
        string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();

    public async Task<object> UpsertAsync(UpsertProductTypeDto dto)
    {
        var code = dto.ProductTypeCode.Trim().ToUpperInvariant();
        var net = Net(dto.NetworkID);
        var name = dto.ProductTypeName?.Trim() ?? "";
        // ProductType bắt buộc (Mst_ProductType_Create_InvalidProductType).
        if (code.Length < 1) throw new InvalidOperationException("Mã loại hàng hóa không hợp lệ.");
        var row = await db.ProductTypes.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.ProductTypeCode == code && x.NetworkID == net);
        if (row is null)
        {
            // Create: ProductType chưa tồn tại (Mst_ProductType_CheckDB_ProductTypeExist).
            if (await db.ProductTypes.AnyAsync(x => x.OrgId == Org && x.ProductTypeCode == code))
                throw new InvalidOperationException($"Loại hàng hóa '{code}' đã tồn tại.");
            // ProductTypeName bắt buộc (Mst_ProductType_Create_InvalidProductTypeName).
            if (name.Length < 1) throw new InvalidOperationException("Tên loại hàng hóa không hợp lệ.");
            row = new ProductType { OrgId = Org, ProductTypeCode = code, NetworkID = net };
            db.ProductTypes.Add(row);
        }
        else
        {
            // Update: ProductTypeName không rỗng (Mst_ProductType_UpdateX_InvalidProductTypeName).
            if (name.Length < 1) throw new InvalidOperationException("Tên loại hàng hóa không hợp lệ.");
        }
        var entity = row!;
        entity.ProductTypeName = name;
        entity.FlagActive = dto.FlagActive ?? true;
        entity.LogLUDTimeUTC = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Project(entity);
    }

    public async Task<object> ListAsync(string? code, string? name)
    {
        var q = db.ProductTypes.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(code))
        {
            var c = code!.Trim().ToUpperInvariant();
            q = q.Where(x => x.ProductTypeCode == c);
        }
        if (!string.IsNullOrWhiteSpace(name))
        {
            var n = name!.Trim();
            q = q.Where(x => x.ProductTypeName.Contains(n));
        }
        var rows = await q.OrderBy(x => x.ProductTypeCode).ThenBy(x => x.NetworkID).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    public async Task<object?> DeleteAsync(string code, string networkId)
    {
        var c = code.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var row = await db.ProductTypes.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.ProductTypeCode == c && x.NetworkID == net);
        if (row is null) return null;   // Mst_ProductType_CheckDB_ProductTypeNotFound
        // Không cho xoá nếu còn hàng hóa thuộc loại này (bảo toàn tham chiếu).
        if (await db.Products.AnyAsync(x => x.OrgId == Org && x.ProductType == c))
            throw new InvalidOperationException($"Loại hàng hóa '{c}' còn hàng hóa tham chiếu, không thể xoá.");
        db.ProductTypes.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, productTypeCode = c, network = net };
    }

    // Tra loại hàng hóa hiệu lực: ưu tiên NetworkID khớp rồi tới "ALL"; chỉ lấy dòng FlagActive.
    public async Task<object> ResolveAsync(string code, string? networkId)
    {
        var c = code.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var candidates = await db.ProductTypes.Where(x => x.OrgId == Org && x.ProductTypeCode == c && x.FlagActive)
            .ToListAsync();
        var row = candidates.FirstOrDefault(x => x.NetworkID == net)
               ?? candidates.FirstOrDefault(x => x.NetworkID == "ALL");
        if (row is null) return new { found = false, productTypeCode = c, network = net };
        return new
        {
            found = true, productTypeCode = row.ProductTypeCode, network = row.NetworkID,
            productTypeName = row.ProductTypeName, flagActive = row.FlagActive
        };
    }

    private static object Project(ProductType x) => new
    {
        productTypeCode = x.ProductTypeCode, x.NetworkID, x.ProductTypeName, x.FlagActive
    };
}