using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;

namespace MiniPricing.Services;

// ===== Danh mục model / dòng sản phẩm (port từ Mst_Model — 2019.4.ProductCenter) =====
// Nghiệp vụ nguồn: đây là danh mục gốc của bảng giá (Spec tham chiếu qua ModelCode).
// Mỗi model có ModelCode + OrgID (+ NetworkID), ModelName, OrgModelCode, BrandCode (Mst_Brand),
// NetworkModelCode (mã model ở kênh cha), Remark và cờ hiệu lực.
// Quy tắc nguồn (WAS_Mst_Model_Create/Update/Delete + Mst_Model_CheckDB):
//   - Create: ModelCode bắt buộc (nếu rỗng → Mst_Model_Create_InvalidModelCode);
//             ModelCode chưa tồn tại (nếu đã có → Mst_Model_CheckDB_ModelCodeExist);
//             BrandCode bắt buộc (nếu rỗng → Mst_Model_Create_InvalidModelCode);
//             ModelName bắt buộc (nếu rỗng → Mst_Model_Create_InvalidModelName);
//             NetworkModelCode (nếu khác rỗng) phải tồn tại ở kênh cha
//             (nếu không → Mst_Model_CheckDB_NotExistNetworkModelCode).
//   - Update: ModelCode phải tồn tại (nếu không → Mst_Model_CheckDB_ModelCodeNotFound);
//             ModelName không được rỗng (→ Mst_Model_Update_InvalidModelName).
//   - Delete: ModelCode phải tồn tại (nếu không → Mst_Model_CheckDB_ModelCodeNotFound).
// Lưu ý: nguồn còn kiểm tra BrandCode tồn tại & active qua Mst_Brand_CheckDB, nhưng
// MiniPricing chưa có danh mục Mst_Brand nên chỉ kiểm tra BrandCode bắt buộc (không rỗng).

public record UpsertModelDto(
    string ModelCode, string? NetworkID,
    string? ModelName, string? OrgModelCode, string? BrandCode,
    string? NetworkModelCode, string? Remark, bool? FlagActive);

public interface IModelService
{
    Task<object> UpsertAsync(UpsertModelDto dto);
    Task<object> ListAsync(string? modelCode, string? modelName, string? brandCode);
    Task<object?> DeleteAsync(string modelCode, string networkId);
    Task<object> ResolveAsync(string modelCode, string? networkId);
}

public sealed class ModelService(AppDbContext db, ITenantContext tenant) : IModelService
{
    private Guid Org => tenant.OrgId;

    public async Task<object> UpsertAsync(UpsertModelDto dto)
    {
        var code = dto.ModelCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(dto.NetworkID) ? "ALL" : dto.NetworkID!.Trim().ToUpperInvariant();
        var name = dto.ModelName?.Trim() ?? "";
        var brand = dto.BrandCode?.Trim().ToUpperInvariant() ?? "";
        var netModelCode = dto.NetworkModelCode?.Trim().ToUpperInvariant() ?? "";

        // ModelCode bắt buộc (Mst_Model_Create_InvalidModelCode).
        if (code.Length < 1) throw new InvalidOperationException("Mã model không hợp lệ.");

        var row = await db.Models.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.ModelCode == code && x.NetworkID == net);

        if (row is null)
        {
            // Create: ModelCode chưa tồn tại (Mst_Model_CheckDB_ModelCodeExist).
            if (await db.Models.AnyAsync(x => x.OrgId == Org && x.ModelCode == code))
                throw new InvalidOperationException($"Mã model '{code}' đã tồn tại.");
            // BrandCode bắt buộc (Mst_Model_Create_InvalidModelCode).
            if (brand.Length < 1) throw new InvalidOperationException("Mã hãng không hợp lệ.");
            // ModelName bắt buộc (Mst_Model_Create_InvalidModelName).
            if (name.Length < 1) throw new InvalidOperationException("Tên model không hợp lệ.");
            // NetworkModelCode phải tồn tại ở kênh cha (Mst_Model_CheckDB_NotExistNetworkModelCode).
            if (netModelCode.Length > 0 && !await db.Models.AnyAsync(x => x.OrgId == Org && x.ModelCode == netModelCode))
                throw new InvalidOperationException($"Mã model kênh cha '{netModelCode}' không tồn tại.");

            row = new Model { OrgId = Org, ModelCode = code, NetworkID = net };
            db.Models.Add(row);
        }
        else
        {
            // Update: ModelName không được rỗng (Mst_Model_Update_InvalidModelName).
            if (name.Length < 1) throw new InvalidOperationException("Tên model không hợp lệ.");
        }

        var entity = row!;
        entity.ModelName = name;
        entity.OrgModelCode = dto.OrgModelCode?.Trim().ToUpperInvariant() ?? "";
        if (brand.Length > 0) entity.BrandCode = brand;
        entity.NetworkModelCode = netModelCode.Length > 0 ? netModelCode : null;
        entity.Remark = dto.Remark?.Trim();
        entity.FlagActive = dto.FlagActive ?? true;
        entity.LogLUDTimeUTC = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Project(entity);
    }

    public async Task<object> ListAsync(string? modelCode, string? modelName, string? brandCode)
    {
        var q = db.Models.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(modelCode))
        {
            var c = modelCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.ModelCode == c);
        }
        if (!string.IsNullOrWhiteSpace(modelName))
        {
            var n = modelName!.Trim();
            q = q.Where(x => x.ModelName.Contains(n));
        }
        if (!string.IsNullOrWhiteSpace(brandCode))
        {
            var b = brandCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.BrandCode == b);
        }
        var rows = await q.OrderBy(x => x.ModelCode).ThenBy(x => x.NetworkID).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    public async Task<object?> DeleteAsync(string modelCode, string networkId)
    {
        var code = modelCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId.Trim().ToUpperInvariant();
        var row = await db.Models.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.ModelCode == code && x.NetworkID == net);
        if (row is null) return null;   // Mst_Model_CheckDB_ModelCodeNotFound
        db.Models.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, modelCode = code, network = net };
    }

    // Tra model hiệu lực: ưu tiên NetworkID khớp rồi tới "ALL"; chỉ lấy dòng FlagActive.
    public async Task<object> ResolveAsync(string modelCode, string? networkId)
    {
        var code = modelCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();

        var candidates = await db.Models.Where(x => x.OrgId == Org && x.ModelCode == code && x.FlagActive)
            .ToListAsync();
        var row = candidates.FirstOrDefault(x => x.NetworkID == net)
               ?? candidates.FirstOrDefault(x => x.NetworkID == "ALL");
        if (row is null) return new { found = false, modelCode = code, network = net };

        return new
        {
            found = true, modelCode = row.ModelCode, network = row.NetworkID,
            modelName = row.ModelName, orgModelCode = row.OrgModelCode, brandCode = row.BrandCode,
            networkModelCode = row.NetworkModelCode, remark = row.Remark, flagActive = row.FlagActive
        };
    }

    private static object Project(Model x) => new
    {
        x.ModelCode, x.NetworkID, x.ModelName, x.OrgModelCode, x.BrandCode,
        x.NetworkModelCode, x.Remark, x.FlagActive
    };
}
