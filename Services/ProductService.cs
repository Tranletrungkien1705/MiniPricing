using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;
namespace MiniPricing.Services;
// ===== Hàng hóa / sản phẩm (port từ Mst_Product — 2019.4.ProductCenter) =====
// Nghiệp vụ nguồn: đây là danh mục gốc mang thông tin giá của bảng giá — giá mua đề xuất
// (UPBuy), giá bán đề xuất (UPSell), mã thuế suất (VATRateCode), đơn vị tính (UnitCode),
// hệ số quy đổi (ValConvert). Liên kết tới Brand/ProductType/ProductGroup để áp giá theo nhóm.
// Quy tắc nguồn (WAS_Mst_Product_Create/Update/Delete + Mst_Product_CheckDB):
//   - Create: ProductCode chưa tồn tại (Mst_Product_CheckDB_ProductExist);
//             ProductCodeUser chưa tồn tại (Mst_Product_CheckDB_ProductCodeUserExist);
//             ProductType phải tồn tại & active (Mst_ProductType_CheckDB);
//             VATRateCode (nếu khác rỗng) phải tồn tại & active (Mst_VATRate_CheckDB);
//             UnitCode (nếu khác rỗng) phải tồn tại & active (Mst_Unit_CheckDB_New20240108);
//             GTIN (nếu khác rỗng) phải là số (Mst_Product_Create_IsNotNumberGTIN).
//   - Update: ProductCode phải tồn tại (Mst_Product_CheckDB_ProductNotFound);
//             nếu hàng hóa đã phát sinh nghiệp vụ (DTimeUsed) thì không cho đổi FlagSerial/FlagLot
//             (Mst_Product_Update_Input_FlagSerialInvalid / _FlagLotInvalid).
//   - Delete: ProductCode phải tồn tại; không cho xoá nếu đã phát sinh nghiệp vụ
//             (Mst_Product_Delete_InvalidDTimeUsed).
public record UpsertProductDto(
    string ProductCode, string? NetworkID, string? ProductLevelSys, string? ProductCodeUser,
    string? BrandCode, string? ProductType, string? ProductGrpCode,
    string? ProductName, string? ProductNameEN, string? ProductBarCode,
    string? ProductCodeNetwork, string? ProductCodeBase, string? ProductCodeRoot,
    bool? FlagSerial, bool? FlagLot, decimal? ValConvert,
    string? VATRateCode, string? UnitCode, bool? FlagSell, bool? FlagBuy,
    decimal? UPBuy, decimal? UPSell, decimal? QtyMaxSt, decimal? QtyMinSt, decimal? QtyEffSt,
    string? ProductStd, string? ProductExpiry, string? ProductQuyCach, string? ProductOrigin,
    bool? FlagFG, string? GTIN, string? SSCCType,
    string? CustomField1, string? CustomField2, string? CustomField3, string? CustomField4, string? CustomField5,
    string? Remark, bool? FlagActive);
public interface IProductService
{
    Task<object> UpsertAsync(UpsertProductDto dto);
    Task<object> ListAsync(string? productCode, string? productName, string? productGrpCode, string? brandCode);
    Task<object?> DeleteAsync(string productCode, string networkId);
    Task<object> ResolveAsync(string productCode, string? networkId);
    // Tra giá hàng hóa hiệu lực: giá bán đề xuất + giá đã gồm VAT (theo VATRateCode).
    Task<object> ResolvePriceAsync(string productCode, string? networkId);
}
public sealed class ProductService(AppDbContext db, ITenantContext tenant) : IProductService
{
    private Guid Org => tenant.OrgId;
    private static string Net(string? networkId) =>
        string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();
    private static bool IsInteger(string s) => long.TryParse(s, out _);

    public async Task<object> UpsertAsync(UpsertProductDto dto)
    {
        var code = dto.ProductCode.Trim().ToUpperInvariant();
        var net = Net(dto.NetworkID);
        var name = dto.ProductName?.Trim() ?? "";
        // ProductCode bắt buộc.
        if (code.Length < 1) throw new InvalidOperationException("Mã hàng hóa không hợp lệ.");
        var row = await db.Products.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.ProductCode == code && x.NetworkID == net);
        var isCreate = row is null;
        if (isCreate)
        {
            // Create: ProductCode chưa tồn tại (Mst_Product_CheckDB_ProductExist).
            if (await db.Products.AnyAsync(x => x.OrgId == Org && x.ProductCode == code))
                throw new InvalidOperationException($"Hàng hóa '{code}' đã tồn tại.");
            // ProductCodeUser chưa tồn tại (Mst_Product_CheckDB_ProductCodeUserExist).
            var codeUser = dto.ProductCodeUser?.Trim().ToUpperInvariant() ?? "";
            if (codeUser.Length > 0 && await db.Products.AnyAsync(x => x.OrgId == Org && x.ProductCodeUser == codeUser))
                throw new InvalidOperationException($"Mã hàng hóa người dùng '{codeUser}' đã tồn tại.");
            row = new Product { OrgId = Org, ProductCode = code, NetworkID = net, CodeGuid = Guid.NewGuid().ToString() };
            db.Products.Add(row);
        }
        else
        {
            // Update: ProductCode phải tồn tại (Mst_Product_CheckDB_ProductNotFound) — đã có row.
            // Nếu đã phát sinh nghiệp vụ thì không cho đổi FlagSerial/FlagLot.
            if (row!.DTimeUsed)
            {
                if (dto.FlagSerial.HasValue && dto.FlagSerial.Value != row.FlagSerial)
                    throw new InvalidOperationException("Cờ Quản lý Serial cho Hàng hóa không hợp lệ.");
                if (dto.FlagLot.HasValue && dto.FlagLot.Value != row.FlagLot)
                    throw new InvalidOperationException("Cờ Quản lý LOT cho Hàng hóa không hợp lệ.");
            }
        }
        var entity = row!;
        // ProductType phải tồn tại & active (Mst_ProductType_CheckDB).
        var productType = dto.ProductType?.Trim().ToUpperInvariant() ?? "";
        if (productType.Length < 1) throw new InvalidOperationException("Loại hàng hóa không hợp lệ.");
        if (!await db.ProductTypes.AnyAsync(x => x.OrgId == Org && x.ProductTypeCode == productType && x.FlagActive))
            throw new InvalidOperationException($"Loại hàng hóa '{productType}' không tồn tại hoặc đã ngừng dùng.");
        // VATRateCode (nếu khác rỗng) phải tồn tại & active (Mst_VATRate_CheckDB).
        var vat = dto.VATRateCode?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(vat)
            && !await db.VatRates.AnyAsync(x => x.OrgId == Org && x.VATRateCode == vat && x.FlagActive))
            throw new InvalidOperationException($"Thuế suất '{vat}' không tồn tại hoặc đã ngừng dùng.");
        // UnitCode (nếu khác rỗng) phải tồn tại & active (Mst_Unit_CheckDB_New20240108).
        var unit = dto.UnitCode?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(unit)
            && !await db.Units.AnyAsync(x => x.OrgId == Org && x.UnitCode == unit && x.FlagActive))
            throw new InvalidOperationException($"Đơn vị tính '{unit}' không tồn tại hoặc đã ngừng dùng.");
        // GTIN (nếu khác rỗng) phải là số (Mst_Product_Create_IsNotNumberGTIN).
        var gtin = dto.GTIN?.Trim();
        if (!string.IsNullOrEmpty(gtin) && !IsInteger(gtin))
            throw new InvalidOperationException("Mã GTIN không hợp lệ (phải là số).");
        // BrandCode (nếu khác rỗng) phải tồn tại & active (Mst_Brand_CheckDB).
        var brand = dto.BrandCode?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(brand)
            && !await db.Brands.AnyAsync(x => x.OrgId == Org && x.BrandCode == brand && x.FlagActive))
            throw new InvalidOperationException($"Hãng '{brand}' không tồn tại hoặc đã ngừng dùng.");
        // ProductGrpCode (nếu khác rỗng) phải tồn tại & active (Mst_ProductGroup_CheckDB).
        var grp = dto.ProductGrpCode?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(grp)
            && !await db.ProductGroups.AnyAsync(x => x.OrgId == Org && x.ProductGrpCode == grp && x.FlagActive))
            throw new InvalidOperationException($"Nhóm hàng '{grp}' không tồn tại hoặc đã ngừng dùng.");

        entity.ProductLevelSys = dto.ProductLevelSys?.Trim().ToUpperInvariant() ?? entity.ProductLevelSys;
        entity.ProductCodeUser = dto.ProductCodeUser?.Trim().ToUpperInvariant() ?? entity.ProductCodeUser;
        entity.BrandCode = string.IsNullOrEmpty(brand) ? null : brand;
        entity.ProductType = productType;
        entity.ProductGrpCode = string.IsNullOrEmpty(grp) ? null : grp;
        entity.ProductName = name;
        entity.ProductNameEN = dto.ProductNameEN?.Trim();
        entity.ProductBarCode = dto.ProductBarCode?.Trim();
        entity.ProductCodeNetwork = dto.ProductCodeNetwork?.Trim().ToUpperInvariant();
        entity.ProductCodeBase = dto.ProductCodeBase?.Trim().ToUpperInvariant();
        entity.ProductCodeRoot = dto.ProductCodeRoot?.Trim().ToUpperInvariant();
        entity.FlagSerial = dto.FlagSerial ?? entity.FlagSerial;
        entity.FlagLot = dto.FlagLot ?? entity.FlagLot;
        entity.ValConvert = dto.ValConvert ?? entity.ValConvert;
        entity.VATRateCode = string.IsNullOrEmpty(vat) ? null : vat;
        entity.UnitCode = string.IsNullOrEmpty(unit) ? null : unit;
        entity.FlagSell = dto.FlagSell ?? true;
        entity.FlagBuy = dto.FlagBuy ?? true;
        entity.UPBuy = dto.UPBuy ?? entity.UPBuy;
        entity.UPSell = dto.UPSell ?? entity.UPSell;
        entity.QtyMaxSt = dto.QtyMaxSt ?? entity.QtyMaxSt;
        entity.QtyMinSt = dto.QtyMinSt ?? entity.QtyMinSt;
        entity.QtyEffSt = dto.QtyEffSt ?? entity.QtyEffSt;
        entity.ProductStd = dto.ProductStd?.Trim();
        entity.ProductExpiry = dto.ProductExpiry?.Trim();
        entity.ProductQuyCach = dto.ProductQuyCach?.Trim();
        entity.ProductOrigin = dto.ProductOrigin?.Trim();
        entity.FlagFG = dto.FlagFG ?? false;
        entity.GTIN = string.IsNullOrEmpty(gtin) ? null : gtin;
        entity.SSCCType = dto.SSCCType?.Trim().ToUpperInvariant();
        entity.CustomField1 = dto.CustomField1?.Trim();
        entity.CustomField2 = dto.CustomField2?.Trim();
        entity.CustomField3 = dto.CustomField3?.Trim();
        entity.CustomField4 = dto.CustomField4?.Trim();
        entity.CustomField5 = dto.CustomField5?.Trim();
        entity.Remark = dto.Remark?.Trim();
        entity.FlagActive = dto.FlagActive ?? true;
        entity.LogLUDTimeUTC = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Project(entity);
    }

    public async Task<object> ListAsync(string? productCode, string? productName, string? productGrpCode, string? brandCode)
    {
        var q = db.Products.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(productCode))
        {
            var c = productCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.ProductCode == c);
        }
        if (!string.IsNullOrWhiteSpace(productName))
        {
            var n = productName!.Trim();
            q = q.Where(x => x.ProductName.Contains(n));
        }
        if (!string.IsNullOrWhiteSpace(productGrpCode))
        {
            var g = productGrpCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.ProductGrpCode == g);
        }
        if (!string.IsNullOrWhiteSpace(brandCode))
        {
            var b = brandCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.BrandCode == b);
        }
        var rows = await q.OrderBy(x => x.ProductCode).ThenBy(x => x.NetworkID).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    public async Task<object?> DeleteAsync(string productCode, string networkId)
    {
        var c = productCode.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var row = await db.Products.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.ProductCode == c && x.NetworkID == net);
        if (row is null) return null;   // Mst_Product_CheckDB_ProductNotFound
        // Không cho xoá nếu đã phát sinh nghiệp vụ (Mst_Product_Delete_InvalidDTimeUsed).
        if (row.DTimeUsed)
            throw new InvalidOperationException($"Hàng hóa '{c}' đã được sử dụng trong nghiệp vụ, không thể xoá.");
        db.Products.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, productCode = c, network = net };
    }

    // Tra hàng hóa hiệu lực: ưu tiên NetworkID khớp rồi tới "ALL"; chỉ lấy dòng FlagActive.
    public async Task<object> ResolveAsync(string productCode, string? networkId)
    {
        var c = productCode.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var candidates = await db.Products.Where(x => x.OrgId == Org && x.ProductCode == c && x.FlagActive)
            .ToListAsync();
        var row = candidates.FirstOrDefault(x => x.NetworkID == net)
               ?? candidates.FirstOrDefault(x => x.NetworkID == "ALL");
        if (row is null) return new { found = false, productCode = c, network = net };
        return new
        {
            found = true, productCode = row.ProductCode, network = row.NetworkID,
            productCodeUser = row.ProductCodeUser, productName = row.ProductName,
            brandCode = row.BrandCode, productType = row.ProductType, productGrpCode = row.ProductGrpCode,
            unitCode = row.UnitCode, vatRateCode = row.VATRateCode,
            upBuy = row.UPBuy, upSell = row.UPSell, flagSell = row.FlagSell, flagBuy = row.FlagBuy,
            flagFG = row.FlagFG, flagActive = row.FlagActive
        };
    }

    // Tra giá hàng hóa hiệu lực: giá bán đề xuất + giá đã gồm VAT (theo VATRateCode hiệu lực).
    public async Task<object> ResolvePriceAsync(string productCode, string? networkId)
    {
        var c = productCode.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var candidates = await db.Products.Where(x => x.OrgId == Org && x.ProductCode == c && x.FlagActive)
            .ToListAsync();
        var row = candidates.FirstOrDefault(x => x.NetworkID == net)
               ?? candidates.FirstOrDefault(x => x.NetworkID == "ALL");
        if (row is null) return new { found = false, productCode = c, network = net };
        decimal vatRate = 0;
        if (!string.IsNullOrEmpty(row.VATRateCode))
        {
            var vat = await db.VatRates.FirstOrDefaultAsync(x => x.OrgId == Org
                && x.VATRateCode == row.VATRateCode && x.FlagActive);
            if (vat is not null) vatRate = vat.VATRate;
        }
        var sell = row.UPSell;
        var sellWithVat = sell + sell * vatRate / 100m;
        return new
        {
            found = true, productCode = row.ProductCode, network = row.NetworkID,
            unitCode = row.UnitCode, vatRateCode = row.VATRateCode, vatRate,
            upBuy = row.UPBuy, upSell = sell, sellWithVat
        };
    }

    private static object Project(Product x) => new
    {
        productCode = x.ProductCode, x.NetworkID, x.ProductLevelSys, x.ProductCodeUser,
        x.BrandCode, x.ProductType, x.ProductGrpCode, x.ProductName, x.ProductNameEN,
        x.ProductBarCode, x.ProductCodeNetwork, x.ProductCodeBase, x.ProductCodeRoot,
        x.FlagSerial, x.FlagLot, x.ValConvert, x.VATRateCode, x.UnitCode,
        x.FlagSell, x.FlagBuy, x.UPBuy, x.UPSell, x.QtyMaxSt, x.QtyMinSt, x.QtyEffSt,
        x.ProductStd, x.ProductExpiry, x.ProductQuyCach, x.ProductOrigin,
        x.FlagFG, x.GTIN, x.SSCCType, x.Remark, x.FlagActive, x.DTimeUsed
    };
}