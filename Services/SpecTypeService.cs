using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;
namespace MiniPricing.Services;
// ===== Danh má»¥c phÃ¢n loáº¡i quy cÃ¡ch (port tá»« Mst_SpecType1 / Mst_SpecType2 â€” 2019.4.ProductCenter) =====
// Nghiá»‡p vá»¥ nguá»“n: Ä‘Ã¢y lÃ  danh má»¥c gá»‘c cá»§a báº£ng giÃ¡ (Spec tham chiáº¿u qua SpecType1/SpecType2).
// Má»—i dÃ²ng cÃ³ mÃ£ phÃ¢n loáº¡i + OrgID (+ NetworkID), tÃªn phÃ¢n loáº¡i, ghi chÃº vÃ  cá» hiá»‡u lá»±c.
// Quy táº¯c nguá»“n (WAS_Mst_SpecType1_Create/Update/Delete + Mst_SpecType1_CheckDB):
//   - Create: SpecType1 báº¯t buá»™c (náº¿u rá»—ng â†’ Mst_SpecType1_Create_InvalidSpecType1);
//             SpecType1 chÆ°a tá»“n táº¡i (náº¿u Ä‘Ã£ cÃ³ â†’ Mst_SpecType1_CheckDB_SpecType1Exist);
//             SpecType1Name báº¯t buá»™c (náº¿u rá»—ng â†’ Mst_SpecType1_Create_InvalidSpecType1Name).
//   - Update: SpecType1 pháº£i tá»“n táº¡i (náº¿u khÃ´ng â†’ Mst_SpecType1_CheckDB_SpecType1NotFound);
//             SpecType1Name khÃ´ng Ä‘Æ°á»£c rá»—ng (â†’ Mst_SpecType1_Update_InvalidSpecType1Name).
//   - Delete: SpecType1 pháº£i tá»“n táº¡i (náº¿u khÃ´ng â†’ Mst_SpecType1_CheckDB_SpecType1NotFound).
// SpecType2 cÃ³ cÃ¹ng quy táº¯c vá»›i tiá»n tá»‘ Mst_SpecType2_*.
public record UpsertSpecTypeDto(
    string Code, string? NetworkID,
    string? Name, string? Remark, bool? FlagActive);
public interface ISpecTypeService
{
    Task<object> UpsertType1Async(UpsertSpecTypeDto dto);
    Task<object> ListType1Async(string? code, string? name);
    Task<object?> DeleteType1Async(string code, string networkId);
    Task<object> ResolveType1Async(string code, string? networkId);
    Task<object> UpsertType2Async(UpsertSpecTypeDto dto);
    Task<object> ListType2Async(string? code, string? name);
    Task<object?> DeleteType2Async(string code, string networkId);
    Task<object> ResolveType2Async(string code, string? networkId);
}
public sealed class SpecTypeService(AppDbContext db, ITenantContext tenant) : ISpecTypeService
{
    private Guid Org => tenant.OrgId;
    private static string Net(string? networkId) =>
        string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();

    // ===== SpecType1 =====
    public async Task<object> UpsertType1Async(UpsertSpecTypeDto dto)
    {
        var code = dto.Code.Trim().ToUpperInvariant();
        var net = Net(dto.NetworkID);
        var name = dto.Name?.Trim() ?? "";
        // SpecType1 báº¯t buá»™c (Mst_SpecType1_Create_InvalidSpecType1).
        if (code.Length < 1) throw new InvalidOperationException("MÃ£ phÃ¢n loáº¡i 1 khÃ´ng há»£p lá»‡.");
        var row = await db.SpecType1s.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.SpecType1Code == code && x.NetworkID == net);
        if (row is null)
        {
            // Create: SpecType1 chÆ°a tá»“n táº¡i (Mst_SpecType1_CheckDB_SpecType1Exist).
            if (await db.SpecType1s.AnyAsync(x => x.OrgId == Org && x.SpecType1Code == code))
                throw new InvalidOperationException($"MÃ£ phÃ¢n loáº¡i 1 '{code}' Ä‘Ã£ tá»“n táº¡i.");
            // SpecType1Name báº¯t buá»™c (Mst_SpecType1_Create_InvalidSpecType1Name).
            if (name.Length < 1) throw new InvalidOperationException("TÃªn phÃ¢n loáº¡i 1 khÃ´ng há»£p lá»‡.");
            row = new SpecType1 { OrgId = Org, SpecType1Code = code, NetworkID = net };
            db.SpecType1s.Add(row);
        }
        else
        {
            // Update: SpecType1Name khÃ´ng Ä‘Æ°á»£c rá»—ng (Mst_SpecType1_Update_InvalidSpecType1Name).
            if (name.Length < 1) throw new InvalidOperationException("TÃªn phÃ¢n loáº¡i 1 khÃ´ng há»£p lá»‡.");
        }
        var entity = row!;
        entity.SpecType1Name = name;
        entity.Remark = dto.Remark?.Trim();
        entity.FlagActive = dto.FlagActive ?? true;
        entity.LogLUDTimeUTC = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Project1(entity);
    }
    public async Task<object> ListType1Async(string? code, string? name)
    {
        var q = db.SpecType1s.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(code))
        {
            var c = code!.Trim().ToUpperInvariant();
            q = q.Where(x => x.SpecType1Code == c);
        }
        if (!string.IsNullOrWhiteSpace(name))
        {
            var n = name!.Trim();
            q = q.Where(x => x.SpecType1Name.Contains(n));
        }
        var rows = await q.OrderBy(x => x.SpecType1Code).ThenBy(x => x.NetworkID).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project1) };
    }
    public async Task<object?> DeleteType1Async(string code, string networkId)
    {
        var c = code.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var row = await db.SpecType1s.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.SpecType1Code == c && x.NetworkID == net);
        if (row is null) return null;   // Mst_SpecType1_CheckDB_SpecType1NotFound
        db.SpecType1s.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, specType1 = c, network = net };
    }
    // Tra phÃ¢n loáº¡i 1 hiá»‡u lá»±c: Æ°u tiÃªn NetworkID khá»›p rá»“i tá»›i "ALL"; chá»‰ láº¥y dÃ²ng FlagActive.
    public async Task<object> ResolveType1Async(string code, string? networkId)
    {
        var c = code.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var candidates = await db.SpecType1s.Where(x => x.OrgId == Org && x.SpecType1Code == c && x.FlagActive)
            .ToListAsync();
        var row = candidates.FirstOrDefault(x => x.NetworkID == net)
               ?? candidates.FirstOrDefault(x => x.NetworkID == "ALL");
        if (row is null) return new { found = false, specType1 = c, network = net };
        return new
        {
            found = true, specType1 = row.SpecType1Code, network = row.NetworkID,
            specType1Name = row.SpecType1Name, remark = row.Remark, flagActive = row.FlagActive
        };
    }

    // ===== SpecType2 =====
    public async Task<object> UpsertType2Async(UpsertSpecTypeDto dto)
    {
        var code = dto.Code.Trim().ToUpperInvariant();
        var net = Net(dto.NetworkID);
        var name = dto.Name?.Trim() ?? "";
        // SpecType2 báº¯t buá»™c (Mst_SpecType2_Create_InvalidSpecType2).
        if (code.Length < 1) throw new InvalidOperationException("MÃ£ phÃ¢n loáº¡i 2 khÃ´ng há»£p lá»‡.");
        var row = await db.SpecType2s.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.SpecType2Code == code && x.NetworkID == net);
        if (row is null)
        {
            // Create: SpecType2 chÆ°a tá»“n táº¡i (Mst_SpecType2_CheckDB_SpecType2Exist).
            if (await db.SpecType2s.AnyAsync(x => x.OrgId == Org && x.SpecType2Code == code))
                throw new InvalidOperationException($"MÃ£ phÃ¢n loáº¡i 2 '{code}' Ä‘Ã£ tá»“n táº¡i.");
            // SpecType2Name báº¯t buá»™c (Mst_SpecType2_Create_InvalidSpecType2Name).
            if (name.Length < 1) throw new InvalidOperationException("TÃªn phÃ¢n loáº¡i 2 khÃ´ng há»£p lá»‡.");
            row = new SpecType2 { OrgId = Org, SpecType2Code = code, NetworkID = net };
            db.SpecType2s.Add(row);
        }
        else
        {
            // Update: SpecType2Name khÃ´ng Ä‘Æ°á»£c rá»—ng (Mst_SpecType2_Update_InvalidSpecType2Name).
            if (name.Length < 1) throw new InvalidOperationException("TÃªn phÃ¢n loáº¡i 2 khÃ´ng há»£p lá»‡.");
        }
        var entity = row!;
        entity.SpecType2Name = name;
        entity.Remark = dto.Remark?.Trim();
        entity.FlagActive = dto.FlagActive ?? true;
        entity.LogLUDTimeUTC = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Project2(entity);
    }
    public async Task<object> ListType2Async(string? code, string? name)
    {
        var q = db.SpecType2s.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(code))
        {
            var c = code!.Trim().ToUpperInvariant();
            q = q.Where(x => x.SpecType2Code == c);
        }
        if (!string.IsNullOrWhiteSpace(name))
        {
            var n = name!.Trim();
            q = q.Where(x => x.SpecType2Name.Contains(n));
        }
        var rows = await q.OrderBy(x => x.SpecType2Code).ThenBy(x => x.NetworkID).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project2) };
    }
    public async Task<object?> DeleteType2Async(string code, string networkId)
    {
        var c = code.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var row = await db.SpecType2s.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.SpecType2Code == c && x.NetworkID == net);
        if (row is null) return null;   // Mst_SpecType2_CheckDB_SpecType2NotFound
        db.SpecType2s.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, specType2 = c, network = net };
    }
    // Tra phÃ¢n loáº¡i 2 hiá»‡u lá»±c: Æ°u tiÃªn NetworkID khá»›p rá»“i tá»›i "ALL"; chá»‰ láº¥y dÃ²ng FlagActive.
    public async Task<object> ResolveType2Async(string code, string? networkId)
    {
        var c = code.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var candidates = await db.SpecType2s.Where(x => x.OrgId == Org && x.SpecType2Code == c && x.FlagActive)
            .ToListAsync();
        var row = candidates.FirstOrDefault(x => x.NetworkID == net)
               ?? candidates.FirstOrDefault(x => x.NetworkID == "ALL");
        if (row is null) return new { found = false, specType2 = c, network = net };
        return new
        {
            found = true, specType2 = row.SpecType2Code, network = row.NetworkID,
            specType2Name = row.SpecType2Name, remark = row.Remark, flagActive = row.FlagActive
        };
    }

    private static object Project1(SpecType1 x) => new
    {
        specType1 = x.SpecType1Code, x.NetworkID, x.SpecType1Name, x.Remark, x.FlagActive
    };
    private static object Project2(SpecType2 x) => new
    {
        specType2 = x.SpecType2Code, x.NetworkID, x.SpecType2Name, x.Remark, x.FlagActive
    };
}
