using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;
namespace MiniPricing.Services;
// ===== Định mức nguyên vật liệu / cấu thành sản phẩm (port từ Prd_BOM — 2019.4.ProductCenter) =====
// Nghiệp vụ nguồn: mỗi dòng BOM khai báo một thành phần (ProductCode) cấu thành nên hàng hóa cha
// (ProductCodeParent) với số lượng Qty. Dùng để **cộng dồn giá** (roll-up): giá mua/bán đề xuất
// của hàng hóa cha = Σ (UPBuy/UPSell của thành phần × Qty) — theo Prd_BOMUI.BuyAmount/SellAmount
// trong nguồn (BuyAmount = mp_UPBuy * Qty, SellAmount = mp_UPSell * Qty).
// Quy tắc nguồn (Mst_ProductController.GetBOM + WA_Prd_BOM_Get + Mst_Product_Create/Update):
//   - BOM chỉ áp cho hàng hóa loại COMBO (ProductType = "COMBO"); ngoài COMBO thì cha là
//     ProductCodeRoot (Mst_Product_Create_Input_Prd_BOMTblNotFound khi thiếu bảng BOM).
//   - Hàng hóa không được đồng thời quản lý LOT và Serial
//     (Mst_Product_Create_Invalid_Prd_BOM_FlagSerialOrFlagLot).
public record UpsertProductBomDto(
    string ProductCodeParent, string ProductCode, string? NetworkID,
    decimal? Qty, string? Remark, bool? FlagActive);
public interface IProductBomService
{
    Task<object> UpsertAsync(UpsertProductBomDto dto);
    Task<object> ListAsync(string? productCodeParent, string? productCode);
    Task<object?> DeleteAsync(string productCodeParent, string productCode, string networkId);
    // Tra định mức hiệu lực của một hàng hóa cha → danh sách thành phần + cộng dồn giá mua/bán.
    Task<object> ResolveAsync(string productCodeParent, string? networkId);
}
public sealed class ProductBomService(AppDbContext db, ITenantContext tenant) : IProductBomService
{
    private Guid Org => tenant.OrgId;
    private static string Net(string? networkId) =>
        string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();

    public async Task<object> UpsertAsync(UpsertProductBomDto dto)
    {
        var parent = dto.ProductCodeParent.Trim().ToUpperInvariant();
        var child = dto.ProductCode.Trim().ToUpperInvariant();
        var net = Net(dto.NetworkID);
        // ProductCodeParent + ProductCode bắt buộc (Mst_Product_Create_Input_Prd_BOMTblNotFound).
        if (parent.Length < 1) throw new InvalidOperationException("Mã hàng hóa cha không hợp lệ.");
        if (child.Length < 1) throw new InvalidOperationException("Mã hàng hóa con không hợp lệ.");
        // Không cho một hàng hóa là thành phần của chính nó (bảo toàn cấu trúc BOM).
        if (string.Equals(parent, child, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Hàng hóa cha và hàng hóa con không được trùng nhau.");
        // Hàng hóa cha phải tồn tại & active (Mst_Product_CheckDB_ProductNotFound).
        if (!await db.Products.AnyAsync(x => x.OrgId == Org && x.ProductCode == parent && x.FlagActive))
            throw new InvalidOperationException($"Hàng hóa cha '{parent}' không tồn tại hoặc đã ngừng dùng.");
        // Hàng hóa con (thành phần) phải tồn tại & active (Mst_Product_CheckDB_ProductNotFound).
        if (!await db.Products.AnyAsync(x => x.OrgId == Org && x.ProductCode == child && x.FlagActive))
            throw new InvalidOperationException($"Hàng hóa con '{child}' không tồn tại hoặc đã ngừng dùng.");
        // Hàng hóa không được đồng thời quản lý LOT và Serial
        // (Mst_Product_Create_Invalid_Prd_BOM_FlagSerialOrFlagLot).
        var childProduct = await db.Products.FirstAsync(x => x.OrgId == Org && x.ProductCode == child);
        if (childProduct.FlagSerial && childProduct.FlagLot)
            throw new InvalidOperationException($"Hàng hóa '{child}' không được đồng thời Quản lý LOT và Serial.");
        var row = await db.ProductBoms.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.ProductCodeParent == parent && x.ProductCode == child && x.NetworkID == net);
        if (row is null)
        {
            row = new ProductBom { OrgId = Org, ProductCodeParent = parent, ProductCode = child, NetworkID = net };
            db.ProductBoms.Add(row);
        }
        row.Qty = dto.Qty ?? row.Qty;
        row.Remark = dto.Remark?.Trim();
        row.FlagActive = dto.FlagActive ?? true;
        row.LogLUDTimeUTC = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Project(row);
    }

    public async Task<object> ListAsync(string? productCodeParent, string? productCode)
    {
        var q = db.ProductBoms.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(productCodeParent))
        {
            var p = productCodeParent!.Trim().ToUpperInvariant();
            q = q.Where(x => x.ProductCodeParent == p);
        }
        if (!string.IsNullOrWhiteSpace(productCode))
        {
            var c = productCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.ProductCode == c);
        }
        var rows = await q.OrderBy(x => x.ProductCodeParent).ThenBy(x => x.ProductCode).ThenBy(x => x.NetworkID).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    public async Task<object?> DeleteAsync(string productCodeParent, string productCode, string networkId)
    {
        var parent = productCodeParent.Trim().ToUpperInvariant();
        var child = productCode.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var row = await db.ProductBoms.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.ProductCodeParent == parent && x.ProductCode == child && x.NetworkID == net);
        if (row is null) return null;
        db.ProductBoms.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, productCodeParent = parent, productCode = child, network = net };
    }

    // Tra định mức hiệu lực của một hàng hóa cha: ưu tiên NetworkID khớp rồi tới "ALL";
    // cộng dồn giá mua/bán đề xuất của các thành phần (BuyAmount/SellAmount như nguồn).
    public async Task<object> ResolveAsync(string productCodeParent, string? networkId)
    {
        var parent = productCodeParent.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var all = await db.ProductBoms.Where(x => x.OrgId == Org
            && x.ProductCodeParent == parent && x.FlagActive).ToListAsync();
        // Ưu tiên dòng theo kênh khớp; nếu không có thì lấy kênh "ALL".
        var rows = all.Where(x => x.NetworkID == net).ToList();
        if (rows.Count == 0) rows = all.Where(x => x.NetworkID == "ALL").ToList();
        var items = new List<object>();
        decimal buyAmount = 0, sellAmount = 0;
        foreach (var r in rows)
        {
            var comp = await db.Products.FirstOrDefaultAsync(x => x.OrgId == Org && x.ProductCode == r.ProductCode);
            var upBuy = comp?.UPBuy ?? 0;
            var upSell = comp?.UPSell ?? 0;
            var lineBuy = upBuy * r.Qty;
            var lineSell = upSell * r.Qty;
            buyAmount += lineBuy;
            sellAmount += lineSell;
            items.Add(new
            {
                productCode = r.ProductCode, productName = comp?.ProductName,
                qty = r.Qty, upBuy, upSell, buyAmount = lineBuy, sellAmount = lineSell
            });
        }
        return new
        {
            found = rows.Count > 0, productCodeParent = parent, network = net,
            componentCount = rows.Count, buyAmount, sellAmount, items
        };
    }

    private static object Project(ProductBom x) => new
    {
        x.ProductCodeParent, x.ProductCode, x.NetworkID, x.Qty, x.Remark, x.FlagActive
    };
}
