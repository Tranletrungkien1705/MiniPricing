using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;

namespace MiniPricing.Services;

// ===== Danh mục đơn vị tính (port từ Mst_Unit — 2019.4.ProductCenter) =====
// Nghiệp vụ nguồn: mỗi đơn vị tính có UnitCode (mã hệ thống ngầm) + OrgID (+ NetworkID),
// UnitCodeUser (mã người dùng nhập), UnitName (tên đơn vị tính), ghi chú và cờ hiệu lực.
// SpecUnit/SpecPrice tham chiếu tới đây qua UnitCode để quy đổi và tính giá theo đơn vị.
// Quy tắc nguồn (WAS_Mst_Unit_Create/Update + Mst_Unit_CheckDB/CheckUnitCodeUser/CheckUnitName):
//   - Create: UnitCode bắt buộc (nếu rỗng → Mst_Unit_Create_InvalidUnitCode);
//             UnitCode chưa tồn tại (nếu đã có → Mst_Unit_CheckDB_UnitCodeExist);
//             UnitCodeUser chưa tồn tại (nếu đã có → Mst_Unit_CheckDB_UnitCodeUserExist);
//             UnitName bắt buộc (nếu rỗng → Mst_Unit_Create_InvalidUnitName);
//             UnitName chưa tồn tại (nếu đã có → Mst_Unit_CheckDB_UnitNameExist).
//   - Update: UnitCode phải tồn tại (nếu không → Mst_Unit_CheckDB_UnitCodeNotFound);
//             UnitName mới không được trùng đơn vị khác (→ Mst_Unit_CheckDB_UnitNameExist);
//             UnitName không được rỗng (→ Mst_Unit_Update_InvalidUnitName).
//   - Delete: UnitCode phải tồn tại (nếu không → Mst_Unit_CheckDB_UnitCodeNotFound).

public record UpsertUnitDto(
    string UnitCode, string? NetworkID,
    string? UnitCodeUser, string? UnitName, string? Remark, bool? FlagActive);

public interface IUnitService
{
    Task<object> UpsertAsync(UpsertUnitDto dto);
    Task<object> ListAsync(string? unitCode, string? unitName);
    Task<object?> DeleteAsync(string unitCode, string networkId);
    Task<object> ResolveAsync(string unitCode, string? networkId);
}

public sealed class UnitService(AppDbContext db, ITenantContext tenant) : IUnitService
{
    private Guid Org => tenant.OrgId;

    public async Task<object> UpsertAsync(UpsertUnitDto dto)
    {
        var code = dto.UnitCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(dto.NetworkID) ? "ALL" : dto.NetworkID!.Trim().ToUpperInvariant();
        var codeUser = dto.UnitCodeUser?.Trim() ?? "";
        var name = dto.UnitName?.Trim() ?? "";

        // UnitCode bắt buộc (Mst_Unit_Create_InvalidUnitCode).
        if (code.Length < 1) throw new InvalidOperationException("Mã đơn vị tính không hợp lệ.");

        var row = await db.Units.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.UnitCode == code && x.NetworkID == net);

        if (row is null)
        {
            // Create: UnitCode chưa tồn tại (Mst_Unit_CheckDB_UnitCodeExist).
            if (await db.Units.AnyAsync(x => x.OrgId == Org && x.UnitCode == code))
                throw new InvalidOperationException($"Mã đơn vị tính '{code}' đã tồn tại.");
            // UnitCodeUser chưa tồn tại (Mst_Unit_CheckDB_UnitCodeUserExist).
            if (codeUser.Length > 0 && await db.Units.AnyAsync(x => x.OrgId == Org && x.UnitCodeUser == codeUser))
                throw new InvalidOperationException($"Mã đơn vị tính người dùng '{codeUser}' đã tồn tại.");
            // UnitName bắt buộc (Mst_Unit_Create_InvalidUnitName).
            if (name.Length < 1) throw new InvalidOperationException("Tên đơn vị tính không hợp lệ.");
            // UnitName chưa tồn tại (Mst_Unit_CheckDB_UnitNameExist).
            if (await db.Units.AnyAsync(x => x.OrgId == Org && x.UnitName == name))
                throw new InvalidOperationException($"Tên đơn vị tính '{name}' đã tồn tại.");

            row = new Unit { OrgId = Org, UnitCode = code, NetworkID = net, CodeGuid = Guid.NewGuid().ToString() };
            db.Units.Add(row);
        }
        else
        {
            // Update: UnitName mới không được trùng đơn vị khác (Mst_Unit_CheckDB_UnitNameExist).
            if (name.Length > 0 && !string.Equals(row.UnitName, name, StringComparison.OrdinalIgnoreCase)
                && await db.Units.AnyAsync(x => x.OrgId == Org && x.UnitName == name && x.Id != row.Id))
                throw new InvalidOperationException($"Tên đơn vị tính '{name}' đã tồn tại.");
            // UnitName không được rỗng (Mst_Unit_Update_InvalidUnitName).
            if (name.Length < 1) throw new InvalidOperationException("Tên đơn vị tính không hợp lệ.");
        }

        var entity = row!;
        entity.UnitCodeUser = codeUser;
        entity.UnitName = name;
        entity.Remark = dto.Remark?.Trim();
        entity.FlagActive = dto.FlagActive ?? true;
        entity.LogLUDTimeUTC = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Project(entity);
    }

    public async Task<object> ListAsync(string? unitCode, string? unitName)
    {
        var q = db.Units.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(unitCode))
        {
            var c = unitCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.UnitCode == c);
        }
        if (!string.IsNullOrWhiteSpace(unitName))
        {
            var n = unitName!.Trim();
            q = q.Where(x => x.UnitName.Contains(n));
        }
        var rows = await q.OrderBy(x => x.UnitCode).ThenBy(x => x.NetworkID).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    public async Task<object?> DeleteAsync(string unitCode, string networkId)
    {
        var code = unitCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId.Trim().ToUpperInvariant();
        var row = await db.Units.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.UnitCode == code && x.NetworkID == net);
        if (row is null) return null;   // Mst_Unit_CheckDB_UnitCodeNotFound
        db.Units.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, unitCode = code, network = net };
    }

    // Tra đơn vị tính hiệu lực: ưu tiên NetworkID khớp rồi tới "ALL"; chỉ lấy dòng FlagActive.
    public async Task<object> ResolveAsync(string unitCode, string? networkId)
    {
        var code = unitCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();

        var candidates = await db.Units.Where(x => x.OrgId == Org && x.UnitCode == code && x.FlagActive)
            .ToListAsync();
        var row = candidates.FirstOrDefault(x => x.NetworkID == net)
               ?? candidates.FirstOrDefault(x => x.NetworkID == "ALL");
        if (row is null) return new { found = false, unitCode = code, network = net };

        return new
        {
            found = true, unitCode = row.UnitCode, network = row.NetworkID,
            unitCodeUser = row.UnitCodeUser, unitName = row.UnitName,
            remark = row.Remark, flagActive = row.FlagActive
        };
    }

    private static object Project(Unit x) => new
    {
        x.UnitCode, x.NetworkID, x.UnitCodeUser, x.UnitName, x.Remark, x.FlagActive
    };
}
