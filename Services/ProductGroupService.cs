using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;
namespace MiniPricing.Services;
// ===== Danh má»¥c nhÃ³m hÃ ng (port tá»« Mst_ProductGroup â€” 2019.4.ProductCenter) =====
// Nghiá»‡p vá»¥ nguá»“n: Ä‘Ã¢y lÃ  danh má»¥c gá»‘c cá»§a báº£ng giÃ¡, dÃ¹ng Ä‘á»ƒ Ã¡p giÃ¡ theo nhÃ³m hÃ ng.
// Má»—i dÃ²ng cÃ³ mÃ£ nhÃ³m + OrgID (+ NetworkID), tÃªn nhÃ³m, mÃ´ táº£, nhÃ³m cha (dá»±ng cÃ¢y phÃ¢n cáº¥p),
// mÃ£ BU (ProductGrpBUCode/Pattern), cáº¥p (ProductGrpLevel), hÃ£ng (BrandCode) vÃ  cá» thÃ nh pháº©m (FlagFG).
// Quy táº¯c nguá»“n (WAS_Mst_ProductGroup_Create/Update/Delete + Mst_ProductGroup_CheckDB):
//   - Create: ProductGrpCode báº¯t buá»™c (náº¿u rá»—ng â†’ Mst_ProductGroup_Create_InvalidProductGrpCode);
//             ProductGrpCode chÆ°a tá»“n táº¡i (náº¿u Ä‘Ã£ cÃ³ â†’ Mst_ProductGroup_CheckDB_ProductGroupExist);
//             ProductGrpName báº¯t buá»™c (náº¿u rá»—ng â†’ Mst_ProductGroup_Create_InvalidProductGrpName);
//             ProductGrpName chÆ°a tá»“n táº¡i trong org (Mst_ProductGroup_CheckProductGrpName);
//             BrandCode (náº¿u khÃ¡c rá»—ng) pháº£i tá»“n táº¡i & active (Mst_Brand_CheckDB).
//   - Update: ProductGrpCode pháº£i tá»“n táº¡i (náº¿u khÃ´ng â†’ Mst_ProductGroup_CheckDB_ProductGroupNotFound);
//             ProductGrpName khÃ´ng Ä‘Æ°á»£c rá»—ng (â†’ Mst_ProductGroup_UpdateX_InvalidProductGrpName);
//             náº¿u Ä‘á»•i tÃªn thÃ¬ tÃªn má»›i chÆ°a tá»“n táº¡i (Mst_ProductGroup_CheckProductGrpName).
//   - Delete: ProductGrpCode pháº£i tá»“n táº¡i; khÃ´ng cho xoÃ¡ náº¿u cÃ²n hÃ ng hoÃ¡ thuá»™c nhÃ³m
//             (Mst_ProductGroup_Delete_Invalid_ProductBelongProductGroup).
public record UpsertProductGroupDto(
    string ProductGrpCode, string? NetworkID,
    string? ProductGrpCodeParent, string? ProductGrpName, string? ProductGrpDesc,
    string? BrandCode, bool? FlagFG, bool? FlagActive);
public interface IProductGroupService
{
    Task<object> UpsertAsync(UpsertProductGroupDto dto);
    Task<object> ListAsync(string? productGrpCode, string? productGrpName, string? brandCode);
    Task<object?> DeleteAsync(string productGrpCode, string networkId);
    Task<object> ResolveAsync(string productGrpCode, string? networkId);
    Task<object> TreeAsync(string? networkId);
}
public sealed class ProductGroupService(AppDbContext db, ITenantContext tenant) : IProductGroupService
{
    private Guid Org => tenant.OrgId;
    private static string Net(string? networkId) =>
        string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();

    public async Task<object> UpsertAsync(UpsertProductGroupDto dto)
    {
        var code = dto.ProductGrpCode.Trim().ToUpperInvariant();
        var net = Net(dto.NetworkID);
        var name = dto.ProductGrpName?.Trim() ?? "";
        // ProductGrpCode báº¯t buá»™c (Mst_ProductGroup_Create_InvalidProductGrpCode).
        if (code.Length < 1) throw new InvalidOperationException("MÃ£ nhÃ³m hÃ ng khÃ´ng há»£p lá»‡.");
        var row = await db.ProductGroups.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.ProductGrpCode == code && x.NetworkID == net);
        if (row is null)
        {
            // Create: ProductGrpCode chÆ°a tá»“n táº¡i (Mst_ProductGroup_CheckDB_ProductGroupExist).
            if (await db.ProductGroups.AnyAsync(x => x.OrgId == Org && x.ProductGrpCode == code))
                throw new InvalidOperationException($"NhÃ³m hÃ ng '{code}' Ä‘Ã£ tá»“n táº¡i.");
            // ProductGrpName báº¯t buá»™c (Mst_ProductGroup_Create_InvalidProductGrpName).
            if (name.Length < 1) throw new InvalidOperationException("TÃªn nhÃ³m hÃ ng khÃ´ng há»£p lá»‡.");
            // ProductGrpName chÆ°a tá»“n táº¡i trong org (Mst_ProductGroup_CheckProductGrpName).
            if (await db.ProductGroups.AnyAsync(x => x.OrgId == Org && x.ProductGrpName == name))
                throw new InvalidOperationException($"TÃªn nhÃ³m hÃ ng '{name}' Ä‘Ã£ tá»“n táº¡i.");
            row = new ProductGroup { OrgId = Org, ProductGrpCode = code, NetworkID = net, CodeGuid = Guid.NewGuid().ToString() };
            db.ProductGroups.Add(row);
        }
        else
        {
            // Update: ProductGrpName khÃ´ng Ä‘Æ°á»£c rá»—ng (Mst_ProductGroup_UpdateX_InvalidProductGrpName).
            if (name.Length < 1) throw new InvalidOperationException("TÃªn nhÃ³m hÃ ng khÃ´ng há»£p lá»‡.");
            // Náº¿u Ä‘á»•i tÃªn thÃ¬ tÃªn má»›i chÆ°a tá»“n táº¡i (Mst_ProductGroup_CheckProductGrpName).
            if (!string.Equals(row.ProductGrpName, name, StringComparison.OrdinalIgnoreCase)
                && await db.ProductGroups.AnyAsync(x => x.OrgId == Org && x.ProductGrpName == name))
                throw new InvalidOperationException($"TÃªn nhÃ³m hÃ ng '{name}' Ä‘Ã£ tá»“n táº¡i.");
        }
        var entity = row!;
        var brandCode = dto.BrandCode?.Trim().ToUpperInvariant();
        // BrandCode (náº¿u khÃ¡c rá»—ng) pháº£i tá»“n táº¡i & active (Mst_Brand_CheckDB).
        if (!string.IsNullOrEmpty(brandCode)
            && !await db.Brands.AnyAsync(x => x.OrgId == Org && x.BrandCode == brandCode && x.FlagActive))
            throw new InvalidOperationException($"HÃ£ng '{brandCode}' khÃ´ng tá»“n táº¡i hoáº·c Ä‘Ã£ ngá»«ng dÃ¹ng.");
        entity.ProductGrpCodeParent = string.IsNullOrWhiteSpace(dto.ProductGrpCodeParent)
            ? null : dto.ProductGrpCodeParent!.Trim().ToUpperInvariant();
        entity.ProductGrpName = name;
        entity.ProductGrpDesc = dto.ProductGrpDesc?.Trim();
        entity.BrandCode = string.IsNullOrEmpty(brandCode) ? null : brandCode;
        entity.FlagFG = dto.FlagFG ?? false;
        entity.FlagActive = dto.FlagActive ?? true;
        entity.LogLUDTimeUTC = DateTime.UtcNow;
        // Dá»±ng BU code/pattern + cáº¥p tá»« nhÃ³m cha (Mst_ProductGroup_UpdBU).
        await RebuildBuAsync(entity);
        await db.SaveChangesAsync();
        return Project(entity);
    }

    // Dá»±ng ProductGrpBUCode/Pattern/Level theo chuá»—i nhÃ³m cha (gá»‘c = "ALL", cáº¥p 0).
    private async Task RebuildBuAsync(ProductGroup entity)
    {
        var chain = new List<string>();
        var cur = entity.ProductGrpCodeParent;
        var guard = 0;
        while (!string.IsNullOrWhiteSpace(cur) && guard++ < 32)
        {
            chain.Insert(0, cur!);
            var parent = await db.ProductGroups.FirstOrDefaultAsync(x => x.OrgId == Org
                && x.ProductGrpCode == cur && x.NetworkID == entity.NetworkID);
            cur = parent?.ProductGrpCodeParent;
        }
        entity.ProductGrpLevel = chain.Count;
        entity.ProductGrpBUCode = chain.Count == 0 ? "ALL" : string.Join("/", chain);
        entity.ProductGrpBUPattern = entity.ProductGrpBUCode + "%";
    }

    public async Task<object> ListAsync(string? productGrpCode, string? productGrpName, string? brandCode)
    {
        var q = db.ProductGroups.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(productGrpCode))
        {
            var c = productGrpCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.ProductGrpCode == c);
        }
        if (!string.IsNullOrWhiteSpace(productGrpName))
        {
            var n = productGrpName!.Trim();
            q = q.Where(x => x.ProductGrpName.Contains(n));
        }
        if (!string.IsNullOrWhiteSpace(brandCode))
        {
            var b = brandCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.BrandCode == b);
        }
        var rows = await q.OrderBy(x => x.ProductGrpCode).ThenBy(x => x.NetworkID).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    public async Task<object?> DeleteAsync(string productGrpCode, string networkId)
    {
        var c = productGrpCode.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var row = await db.ProductGroups.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.ProductGrpCode == c && x.NetworkID == net);
        if (row is null) return null;   // Mst_ProductGroup_CheckDB_ProductGroupNotFound
        // KhÃ´ng cho xoÃ¡ náº¿u cÃ²n nhÃ³m con tham chiáº¿u (báº£o toÃ n cÃ¢y phÃ¢n cáº¥p).
        if (await db.ProductGroups.AnyAsync(x => x.OrgId == Org && x.ProductGrpCodeParent == c))
            throw new InvalidOperationException($"NhÃ³m hÃ ng '{c}' cÃ²n nhÃ³m con, khÃ´ng thá»ƒ xoÃ¡.");
        db.ProductGroups.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, productGrpCode = c, network = net };
    }

    // Tra nhÃ³m hÃ ng hiá»‡u lá»±c: Æ°u tiÃªn NetworkID khá»›p rá»“i tá»›i "ALL"; chá»‰ láº¥y dÃ²ng FlagActive.
    public async Task<object> ResolveAsync(string productGrpCode, string? networkId)
    {
        var c = productGrpCode.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var candidates = await db.ProductGroups.Where(x => x.OrgId == Org && x.ProductGrpCode == c && x.FlagActive)
            .ToListAsync();
        var row = candidates.FirstOrDefault(x => x.NetworkID == net)
               ?? candidates.FirstOrDefault(x => x.NetworkID == "ALL");
        if (row is null) return new { found = false, productGrpCode = c, network = net };
        return new
        {
            found = true, productGrpCode = row.ProductGrpCode, network = row.NetworkID,
            productGrpName = row.ProductGrpName, productGrpCodeParent = row.ProductGrpCodeParent,
            productGrpBUCode = row.ProductGrpBUCode, productGrpLevel = row.ProductGrpLevel,
            brandCode = row.BrandCode, flagFG = row.FlagFG, flagActive = row.FlagActive
        };
    }

    // Dá»±ng cÃ¢y nhÃ³m hÃ ng theo ProductGrpCodeParent (chá»‰ gá»‘c cáº¥p 0 khi khÃ´ng cÃ³ cha).
    public async Task<object> TreeAsync(string? networkId)
    {
        var net = Net(networkId);
        var rows = await db.ProductGroups.Where(x => x.OrgId == Org && x.NetworkID == net)
            .OrderBy(x => x.ProductGrpCode).ToListAsync();
        var byParent = rows.GroupBy(x => x.ProductGrpCodeParent ?? "").ToDictionary(g => g.Key, g => g.ToList());
        object Build(ProductGroup node)
        {
            var children = byParent.TryGetValue(node.ProductGrpCode, out var ch)
                ? ch.Select(Build).ToList() : new List<object>();
            return new
            {
                productGrpCode = node.ProductGrpCode, productGrpName = node.ProductGrpName,
                productGrpLevel = node.ProductGrpLevel, brandCode = node.BrandCode,
                flagFG = node.FlagFG, flagActive = node.FlagActive, children
            };
        }
        var roots = byParent.TryGetValue("", out var r) ? r.Select(Build).ToList() : new List<object>();
        return new { network = net, count = rows.Count, roots };
    }

    private static object Project(ProductGroup x) => new
    {
        productGrpCode = x.ProductGrpCode, x.NetworkID, x.ProductGrpCodeParent,
        x.ProductGrpBUCode, x.ProductGrpBUPattern, x.ProductGrpLevel,
        x.ProductGrpName, x.ProductGrpDesc, x.BrandCode, x.FlagFG, x.FlagActive
    };
}
