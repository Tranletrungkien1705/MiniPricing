using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;

namespace MiniPricing.Services;

// ===== Danh mục hãng / thương hiệu (port từ Mst_Brand — 2019.4.ProductCenter) =====
// Nghiệp vụ nguồn: đây là danh mục gốc của bảng giá (Model tham chiếu qua BrandCode).
// Mỗi hãng có BrandCode + OrgID (+ NetworkID), BrandName, NetworkBrandCode (mã hãng ở kênh cha),
// Remark và cờ hiệu lực.
// Quy tắc nguồn (WAS_Mst_Brand_Create/Update/Delete + Mst_Brand_CheckDB + Mst_Brand_CheckBrandName):
//   - Create: BrandCode bắt buộc (nếu rỗng → Mst_Brand_Create_InvalidBrandCode);
//             BrandCode chưa tồn tại (nếu đã có → Mst_Brand_CheckDB_BrandCodeExist);
//             BrandName bắt buộc (nếu rỗng → Mst_Brand_Create_InvalidBrandName);
//             BrandName chưa tồn tại trong org (nếu đã có → Mst_Brand_CheckDB_BrandCodeExist);
//             NetworkBrandCode (nếu khác rỗng) phải tồn tại ở kênh cha
//             (nếu không → Mst_Brand_CheckDB_NotExistNetworkBrandCode); nếu NetworkID == OrgID
//             thì NetworkBrandCode phải bằng BrandCode
//             (nếu không → Mst_Brand_CheckDB_NetworkBrandCodeNoEqualBrandCode).
//   - Update: BrandCode phải tồn tại (nếu không → Mst_Brand_CheckDB_BrandCodeNotFound);
//             BrandName không được rỗng (→ Mst_Brand_Update_InvalidBrandName).
//   - Delete: BrandCode phải tồn tại (nếu không → Mst_Brand_CheckDB_BrandCodeNotFound).

public record UpsertBrandDto(
    string BrandCode, string? NetworkID,
    string? BrandName, string? NetworkBrandCode,
    string? Remark, bool? FlagActive);

public interface IBrandService
{
    Task<object> UpsertAsync(UpsertBrandDto dto);
    Task<object> ListAsync(string? brandCode, string? brandName);
    Task<object?> DeleteAsync(string brandCode, string networkId);
    Task<object> ResolveAsync(string brandCode, string? networkId);
}

public sealed class BrandService(AppDbContext db, ITenantContext tenant) : IBrandService
{
    private Guid Org => tenant.OrgId;

    public async Task<object> UpsertAsync(UpsertBrandDto dto)
    {
        var code = dto.BrandCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(dto.NetworkID) ? "ALL" : dto.NetworkID!.Trim().ToUpperInvariant();
        var name = dto.BrandName?.Trim() ?? "";
        var netBrandCode = dto.NetworkBrandCode?.Trim().ToUpperInvariant() ?? "";

        // BrandCode bắt buộc (Mst_Brand_Create_InvalidBrandCode).
        if (code.Length < 1) throw new InvalidOperationException("Mã hãng không hợp lệ.");

        var row = await db.Brands.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.BrandCode == code && x.NetworkID == net);

        if (row is null)
        {
            // Create: BrandCode chưa tồn tại (Mst_Brand_CheckDB_BrandCodeExist).
            if (await db.Brands.AnyAsync(x => x.OrgId == Org && x.BrandCode == code))
                throw new InvalidOperationException($"Mã hãng '{code}' đã tồn tại.");
            // BrandName bắt buộc (Mst_Brand_Create_InvalidBrandName).
            if (name.Length < 1) throw new InvalidOperationException("Tên hãng không hợp lệ.");
            // BrandName chưa tồn tại trong org (Mst_Brand_CheckBrandName → Mst_Brand_CheckDB_BrandCodeExist).
            if (await db.Brands.AnyAsync(x => x.OrgId == Org && x.BrandName == name))
                throw new InvalidOperationException($"Tên hãng '{name}' đã tồn tại.");
            // NetworkBrandCode phải tồn tại ở kênh cha (Mst_Brand_CheckDB_NetworkBrandCodeOfOrgParent).
            if (netBrandCode.Length > 0)
            {
                if (net == Org.ToString().ToUpperInvariant() && netBrandCode != code)
                    throw new InvalidOperationException("Mã hãng kênh cha phải bằng mã hãng.");
                if (!await db.Brands.AnyAsync(x => x.OrgId == Org && x.BrandCode == netBrandCode))
                    throw new InvalidOperationException($"Mã hãng kênh cha '{netBrandCode}' không tồn tại.");
            }

            row = new Brand { OrgId = Org, BrandCode = code, NetworkID = net };
            db.Brands.Add(row);
        }
        else
        {
            // Update: BrandName không được rỗng (Mst_Brand_Update_InvalidBrandName).
            if (name.Length < 1) throw new InvalidOperationException("Tên hãng không hợp lệ.");
        }

        var entity = row!;
        entity.BrandName = name;
        entity.NetworkBrandCode = netBrandCode.Length > 0 ? netBrandCode : null;
        entity.Remark = dto.Remark?.Trim();
        entity.FlagActive = dto.FlagActive ?? true;
        entity.LogLUDTimeUTC = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Project(entity);
    }

    public async Task<object> ListAsync(string? brandCode, string? brandName)
    {
        var q = db.Brands.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(brandCode))
        {
            var c = brandCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.BrandCode == c);
        }
        if (!string.IsNullOrWhiteSpace(brandName))
        {
            var n = brandName!.Trim();
            q = q.Where(x => x.BrandName.Contains(n));
        }
        var rows = await q.OrderBy(x => x.BrandCode).ThenBy(x => x.NetworkID).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    public async Task<object?> DeleteAsync(string brandCode, string networkId)
    {
        var code = brandCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId.Trim().ToUpperInvariant();
        var row = await db.Brands.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.BrandCode == code && x.NetworkID == net);
        if (row is null) return null;   // Mst_Brand_CheckDB_BrandCodeNotFound
        db.Brands.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, brandCode = code, network = net };
    }

    // Tra hãng hiệu lực: ưu tiên NetworkID khớp rồi tới "ALL"; chỉ lấy dòng FlagActive.
    public async Task<object> ResolveAsync(string brandCode, string? networkId)
    {
        var code = brandCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();

        var candidates = await db.Brands.Where(x => x.OrgId == Org && x.BrandCode == code && x.FlagActive)
            .ToListAsync();
        var row = candidates.FirstOrDefault(x => x.NetworkID == net)
               ?? candidates.FirstOrDefault(x => x.NetworkID == "ALL");
        if (row is null) return new { found = false, brandCode = code, network = net };

        return new
        {
            found = true, brandCode = row.BrandCode, network = row.NetworkID,
            brandName = row.BrandName, networkBrandCode = row.NetworkBrandCode,
            remark = row.Remark, flagActive = row.FlagActive
        };
    }

    private static object Project(Brand x) => new
    {
        x.BrandCode, x.NetworkID, x.BrandName, x.NetworkBrandCode, x.Remark, x.FlagActive
    };
}
