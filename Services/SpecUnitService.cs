using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;

namespace MiniPricing.Services;

// ===== Quy cách × đơn vị tính (port từ Mst_SpecUnit — 2019.4.ProductCenter) =====
// Nghiệp vụ nguồn: mỗi quy cách (SpecCode) × đơn vị tính (UnitCode) × kênh (NetworkID)
// khai báo đơn vị chuẩn quy đổi (StandardUnitCode), hệ số quy đổi (Qty) và kích thước/
// khối lượng (Length/Width/Height/Volume/Weight). Dùng để quy đổi số lượng và tính giá
// theo đơn vị tính khi lập bảng giá (SpecPrice tham chiếu SpecCode + UnitCode).
// Quy tắc nguồn (Mst_SpecUnit_CheckDB + Create/Update/Delete):
//   - Create: SpecUnit chưa tồn tại (nếu có → SpecUnitExist).
//   - Update/Delete: SpecUnit phải tồn tại (nếu không → SpecUnitNotFound).
//   - FlagActive phải khớp giá trị mong đợi (nếu không → FlagActiveNotMatched).
//   - Quy cách (SpecCode) và đơn vị tính (UnitCode/StandardUnitCode) phải tồn tại & đang hoạt động.

public record UpsertSpecUnitDto(
    string SpecCode, string UnitCode, string? NetworkID,
    string? StandardUnitCode, string? SpecUnitDesc,
    decimal? Qty, decimal? Length, decimal? Width, decimal? Height,
    decimal? Volume, decimal? Weight, string? Remark, bool? FlagActive);

public interface ISpecUnitService
{
    Task<object> UpsertAsync(UpsertSpecUnitDto dto);
    Task<object> ListAsync(string? specCode, string? unitCode);
    Task<object?> DeleteAsync(string specCode, string unitCode, string networkId);
    Task<object> ResolveAsync(string specCode, string unitCode, string? networkId);
    Task<object> ConvertQtyAsync(string specCode, string unitCode, decimal qty, string? networkId);
}

public sealed class SpecUnitService(AppDbContext db, ITenantContext tenant) : ISpecUnitService
{
    private Guid Org => tenant.OrgId;

    public async Task<object> UpsertAsync(UpsertSpecUnitDto dto)
    {
        var spec = dto.SpecCode.Trim().ToUpperInvariant();
        var unit = dto.UnitCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(dto.NetworkID) ? "ALL" : dto.NetworkID!.Trim().ToUpperInvariant();
        if (spec.Length < 1) throw new InvalidOperationException("Cần SpecCode.");   // Mst_SpecUnit_CheckDB_SpecUnitNotFound
        if (unit.Length < 1) throw new InvalidOperationException("Cần UnitCode.");   // Mst_SpecUnit_CheckDB_SpecUnitNotFound

        var stdUnit = dto.StandardUnitCode?.Trim().ToUpperInvariant() ?? "";
        var qty = dto.Qty ?? 1;
        if (qty <= 0) throw new InvalidOperationException("Qty (hệ số quy đổi) phải lớn hơn 0.");
        if (dto.Length is < 0 || dto.Width is < 0 || dto.Height is < 0 || dto.Volume is < 0 || dto.Weight is < 0)
            throw new InvalidOperationException("Kích thước/khối lượng không được âm.");

        var row = await db.SpecUnits.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.SpecCode == spec && x.UnitCode == unit && x.NetworkID == net);
        if (row is null)
        {
            row = new SpecUnit { OrgId = Org, SpecCode = spec, UnitCode = unit, NetworkID = net };
            db.SpecUnits.Add(row);
        }
        row.StandardUnitCode = stdUnit;
        row.SpecUnitDesc = dto.SpecUnitDesc?.Trim() ?? "";
        row.Qty = qty;
        row.Length = dto.Length;
        row.Width = dto.Width;
        row.Height = dto.Height;
        row.Volume = dto.Volume;
        row.Weight = dto.Weight;
        row.Remark = dto.Remark?.Trim();
        row.FlagActive = dto.FlagActive ?? true;
        row.LogLUDTimeUTC = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Project(row);
    }

    public async Task<object> ListAsync(string? specCode, string? unitCode)
    {
        var q = db.SpecUnits.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(specCode))
        {
            var s = specCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.SpecCode == s);
        }
        if (!string.IsNullOrWhiteSpace(unitCode))
        {
            var u = unitCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.UnitCode == u);
        }
        var rows = await q.OrderBy(x => x.SpecCode).ThenBy(x => x.UnitCode).ThenBy(x => x.NetworkID).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    public async Task<object?> DeleteAsync(string specCode, string unitCode, string networkId)
    {
        var spec = specCode.Trim().ToUpperInvariant();
        var unit = unitCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId.Trim().ToUpperInvariant();
        var row = await db.SpecUnits.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.SpecCode == spec && x.UnitCode == unit && x.NetworkID == net);
        if (row is null) return null;   // Mst_SpecUnit_CheckDB_SpecUnitNotFound
        db.SpecUnits.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, specCode = spec, unitCode = unit, network = net };
    }

    // Tra đơn vị tính hiệu lực: ưu tiên NetworkID khớp rồi tới "ALL"; chỉ lấy dòng FlagActive.
    public async Task<object> ResolveAsync(string specCode, string unitCode, string? networkId)
    {
        var spec = specCode.Trim().ToUpperInvariant();
        var unit = unitCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();

        var candidates = await db.SpecUnits.Where(x => x.OrgId == Org
            && x.SpecCode == spec && x.UnitCode == unit && x.FlagActive).ToListAsync();
        var row = candidates.FirstOrDefault(x => x.NetworkID == net)
               ?? candidates.FirstOrDefault(x => x.NetworkID == "ALL");
        if (row is null) return new { found = false, specCode = spec, unitCode = unit, network = net };

        return new
        {
            found = true, specCode = row.SpecCode, unitCode = row.UnitCode, network = row.NetworkID,
            standardUnitCode = row.StandardUnitCode, specUnitDesc = row.SpecUnitDesc, qty = row.Qty,
            length = row.Length, width = row.Width, height = row.Height, volume = row.Volume,
            weight = row.Weight, flagActive = row.FlagActive
        };
    }

    // Quy đổi số lượng theo đơn vị tính về số lượng theo đơn vị chuẩn (Qty × hệ số quy đổi).
    public async Task<object> ConvertQtyAsync(string specCode, string unitCode, decimal qty, string? networkId)
    {
        var spec = specCode.Trim().ToUpperInvariant();
        var unit = unitCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();

        var candidates = await db.SpecUnits.Where(x => x.OrgId == Org
            && x.SpecCode == spec && x.UnitCode == unit && x.FlagActive).ToListAsync();
        var row = candidates.FirstOrDefault(x => x.NetworkID == net)
               ?? candidates.FirstOrDefault(x => x.NetworkID == "ALL");
        if (row is null) return new { found = false, specCode = spec, unitCode = unit, network = net };

        return new
        {
            found = true, specCode = row.SpecCode, unitCode = row.UnitCode, network = row.NetworkID,
            standardUnitCode = row.StandardUnitCode, qty, factor = row.Qty,
            standardQty = qty * row.Qty
        };
    }

    private static object Project(SpecUnit x) => new
    {
        x.SpecCode, x.UnitCode, x.NetworkID, x.StandardUnitCode, x.SpecUnitDesc, x.Qty,
        x.Length, x.Width, x.Height, x.Volume, x.Weight, x.Remark, x.FlagActive
    };
}
