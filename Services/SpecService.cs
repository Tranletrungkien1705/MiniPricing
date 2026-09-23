using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;

namespace MiniPricing.Services;

// ===== Quy cách / sản phẩm (port từ Mst_Spec — 2019.4.ProductCenter) =====
// Nghiệp vụ nguồn: đây là danh mục gốc của bảng giá. Mỗi quy cách có SpecCode + OrgID (+ NetworkID),
// SpecName, ModelCode (Mst_Model), SpecType1/SpecType2 (Mst_SpecType1/2), Color, đơn vị mặc định/chuẩn,
// cờ quản lý serial/LOT và các trường mở rộng CustomField1..10.
// SpecPrice/SpecUnit tham chiếu tới đây qua SpecCode.
// Quy tắc nguồn (WAS_Mst_Spec_Add/Upd/Del + Mst_Spec_CheckDB):
//   - Add: SpecCode bắt buộc (nếu rỗng → Mst_Spec_Add_InvalidSpecCode);
//          SpecCode chưa tồn tại (nếu đã có → Mst_Spec_CheckDB_SpecNotExist);
//          SpecName bắt buộc (nếu rỗng → Mst_Spec_Add_InvalidModelName);
//          NetworkSpecCode (nếu khác rỗng) phải tồn tại ở kênh cha
//          (nếu không → Mst_Spec_CheckDB_NotExistNetworkSpecCode).
//   - Upd: SpecCode phải tồn tại (nếu không → Mst_Spec_CheckDB_SpecNotFound);
//          SpecName không được rỗng (→ Mst_Spec_Upd_InvalidSpecName).
//   - Del: SpecCode phải tồn tại (nếu không → Mst_Spec_CheckDB_SpecNotFound).

public record UpsertSpecDto(
    string SpecCode, string? NetworkID,
    string? SpecName, string? SpecDesc, string? ModelCode,
    string? SpecType1, string? SpecType2, string? Color,
    bool? FlagHasSerial, bool? FlagHasLOT,
    string? DefaultUnitCode, string? StandardUnitCode, string? NetworkSpecCode,
    string? Remark, bool? FlagActive);

public interface ISpecService
{
    Task<object> UpsertAsync(UpsertSpecDto dto);
    Task<object> ListAsync(string? specCode, string? specName, string? modelCode);
    Task<object?> DeleteAsync(string specCode, string networkId);
    Task<object> ResolveAsync(string specCode, string? networkId);
}

public sealed class SpecService(AppDbContext db, ITenantContext tenant) : ISpecService
{
    private Guid Org => tenant.OrgId;

    public async Task<object> UpsertAsync(UpsertSpecDto dto)
    {
        var code = dto.SpecCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(dto.NetworkID) ? "ALL" : dto.NetworkID!.Trim().ToUpperInvariant();
        var name = dto.SpecName?.Trim() ?? "";
        var netSpecCode = dto.NetworkSpecCode?.Trim().ToUpperInvariant() ?? "";

        // SpecCode bắt buộc (Mst_Spec_Add_InvalidSpecCode).
        if (code.Length < 1) throw new InvalidOperationException("Mã quy cách không hợp lệ.");

        var row = await db.Specs.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.SpecCode == code && x.NetworkID == net);

        if (row is null)
        {
            // Add: SpecCode chưa tồn tại (Mst_Spec_CheckDB_SpecNotExist).
            if (await db.Specs.AnyAsync(x => x.OrgId == Org && x.SpecCode == code))
                throw new InvalidOperationException($"Mã quy cách '{code}' đã tồn tại.");
            // SpecName bắt buộc (Mst_Spec_Add_InvalidModelName).
            if (name.Length < 1) throw new InvalidOperationException("Tên quy cách không hợp lệ.");
            // NetworkSpecCode phải tồn tại ở kênh cha (Mst_Spec_CheckDB_NotExistNetworkSpecCode).
            if (netSpecCode.Length > 0 && !await db.Specs.AnyAsync(x => x.OrgId == Org && x.SpecCode == netSpecCode))
                throw new InvalidOperationException($"Mã quy cách kênh cha '{netSpecCode}' không tồn tại.");

            row = new Spec { OrgId = Org, SpecCode = code, NetworkID = net };
            db.Specs.Add(row);
        }
        else
        {
            // Upd: SpecName không được rỗng (Mst_Spec_Upd_InvalidSpecName).
            if (name.Length < 1) throw new InvalidOperationException("Tên quy cách không hợp lệ.");
        }

        var entity = row!;
        entity.SpecName = name;
        entity.SpecDesc = dto.SpecDesc?.Trim();
        entity.ModelCode = dto.ModelCode?.Trim();
        entity.SpecType1 = dto.SpecType1?.Trim();
        entity.SpecType2 = dto.SpecType2?.Trim();
        entity.Color = dto.Color?.Trim();
        entity.FlagHasSerial = dto.FlagHasSerial ?? false;
        entity.FlagHasLOT = dto.FlagHasLOT ?? false;
        entity.DefaultUnitCode = dto.DefaultUnitCode?.Trim().ToUpperInvariant() ?? "";
        entity.StandardUnitCode = dto.StandardUnitCode?.Trim().ToUpperInvariant() ?? "";
        entity.NetworkSpecCode = netSpecCode.Length > 0 ? netSpecCode : null;
        entity.Remark = dto.Remark?.Trim();
        entity.FlagActive = dto.FlagActive ?? true;
        entity.LogLUDTimeUTC = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Project(entity);
    }

    public async Task<object> ListAsync(string? specCode, string? specName, string? modelCode)
    {
        var q = db.Specs.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(specCode))
        {
            var c = specCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.SpecCode == c);
        }
        if (!string.IsNullOrWhiteSpace(specName))
        {
            var n = specName!.Trim();
            q = q.Where(x => x.SpecName.Contains(n));
        }
        if (!string.IsNullOrWhiteSpace(modelCode))
        {
            var m = modelCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.ModelCode == m);
        }
        var rows = await q.OrderBy(x => x.SpecCode).ThenBy(x => x.NetworkID).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    public async Task<object?> DeleteAsync(string specCode, string networkId)
    {
        var code = specCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId.Trim().ToUpperInvariant();
        var row = await db.Specs.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.SpecCode == code && x.NetworkID == net);
        if (row is null) return null;   // Mst_Spec_CheckDB_SpecNotFound
        db.Specs.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, specCode = code, network = net };
    }

    // Tra quy cách hiệu lực: ưu tiên NetworkID khớp rồi tới "ALL"; chỉ lấy dòng FlagActive.
    public async Task<object> ResolveAsync(string specCode, string? networkId)
    {
        var code = specCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();

        var candidates = await db.Specs.Where(x => x.OrgId == Org && x.SpecCode == code && x.FlagActive)
            .ToListAsync();
        var row = candidates.FirstOrDefault(x => x.NetworkID == net)
               ?? candidates.FirstOrDefault(x => x.NetworkID == "ALL");
        if (row is null) return new { found = false, specCode = code, network = net };

        return new
        {
            found = true, specCode = row.SpecCode, network = row.NetworkID,
            specName = row.SpecName, specDesc = row.SpecDesc, modelCode = row.ModelCode,
            specType1 = row.SpecType1, specType2 = row.SpecType2, color = row.Color,
            flagHasSerial = row.FlagHasSerial, flagHasLOT = row.FlagHasLOT,
            defaultUnitCode = row.DefaultUnitCode, standardUnitCode = row.StandardUnitCode,
            networkSpecCode = row.NetworkSpecCode, remark = row.Remark, flagActive = row.FlagActive
        };
    }

    private static object Project(Spec x) => new
    {
        x.SpecCode, x.NetworkID, x.SpecName, x.SpecDesc, x.ModelCode,
        x.SpecType1, x.SpecType2, x.Color, x.FlagHasSerial, x.FlagHasLOT,
        x.DefaultUnitCode, x.StandardUnitCode, x.NetworkSpecCode, x.Remark, x.FlagActive
    };
}
