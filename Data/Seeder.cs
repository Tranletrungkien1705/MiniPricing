using Microsoft.EntityFrameworkCore;
using MiniPricing.Models;
namespace MiniPricing.Data;
public static class Seeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (!await db.Orgs.AnyAsync(o => o.Id == TenantContext.DefaultOrgId))
            db.Orgs.Add(new Org { Id = TenantContext.DefaultOrgId, Name = "Demo Pricing", ApiKey = "demo-pricing" });
        await db.SaveChangesAsync();

        // Danh mục thuế suất VAT mẫu (Mst_VATRate): mã × kênh → % thuế suất + mô tả.
        if (!await db.VatRates.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            db.VatRates.AddRange(
                new VatRate { OrgId = TenantContext.DefaultOrgId, VATRateCode = "VAT0", NetworkID = "ALL", VATRate = 0, VATDesc = "Không chịu thuế" },
                new VatRate { OrgId = TenantContext.DefaultOrgId, VATRateCode = "VAT5", NetworkID = "ALL", VATRate = 5, VATDesc = "Thuế suất 5%" },
                new VatRate { OrgId = TenantContext.DefaultOrgId, VATRateCode = "VAT8", NetworkID = "ALL", VATRate = 8, VATDesc = "Thuế suất 8%" },
                new VatRate { OrgId = TenantContext.DefaultOrgId, VATRateCode = "VAT10", NetworkID = "ALL", VATRate = 10, VATDesc = "Thuế suất 10%" }
            );
            await db.SaveChangesAsync();
        }

        // Giá theo quy cách mẫu (Mst_SpecPrice): 1 quy cách × đơn vị × kênh, có chiết khấu + VAT.
        if (!await db.SpecPrices.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            var from = new DateTime(2024, 1, 1);
            db.SpecPrices.AddRange(
                new SpecPrice { OrgId = TenantContext.DefaultOrgId, SpecCode = "SP-001", UnitCode = "CAI", NetworkID = "ALL", BuyPrice = 8_000_000, SellPrice = 10_000_000, DiscountVND = 500_000, VATRateCode = "VAT10", EffectDTimeStart = from, Remark = "Giá niêm yết" },
                new SpecPrice { OrgId = TenantContext.DefaultOrgId, SpecCode = "SP-001", UnitCode = "CAI", NetworkID = "DEALER", BuyPrice = 8_000_000, SellPrice = 10_000_000, DiscountVND = 1_500_000, VATRateCode = "VAT10", EffectDTimeStart = from, Remark = "Chiết khấu đại lý" },
                new SpecPrice { OrgId = TenantContext.DefaultOrgId, SpecCode = "SP-002", UnitCode = "BO", NetworkID = "ALL", BuyPrice = 1_200_000, SellPrice = 1_500_000, DiscountVND = 0, VATRateCode = "VAT8", EffectDTimeStart = from }
            );
            await db.SaveChangesAsync();
        }

        // Tỷ giá ngoại tệ mẫu (Mst_CurrencyEx): mã tiền tệ × kênh → tỷ giá mua/bán so với VND.
        if (!await db.CurrencyExes.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            db.CurrencyExes.AddRange(
                new CurrencyEx { OrgId = TenantContext.DefaultOrgId, CurrencyCode = "VND", NetworkID = "ALL", CurrencyName = "Việt Nam Đồng", BaseCurrencyCode = "VND", BuyRate = 1, SellRate = 1, InterEx = "N", Remark = "Tiền tệ gốc" },
                new CurrencyEx { OrgId = TenantContext.DefaultOrgId, CurrencyCode = "USD", NetworkID = "ALL", CurrencyName = "Đô la Mỹ", BaseCurrencyCode = "VND", BuyRate = 25_000, SellRate = 25_400, InterEx = "Y", Remark = "Tỷ giá liên ngân hàng" },
                new CurrencyEx { OrgId = TenantContext.DefaultOrgId, CurrencyCode = "EUR", NetworkID = "ALL", CurrencyName = "Euro", BaseCurrencyCode = "VND", BuyRate = 27_000, SellRate = 27_500, InterEx = "Y" }
            );
            await db.SaveChangesAsync();
        }

        // Lịch sử tỷ giá ngoại tệ mẫu (Mst_CurrencyExHist): audit trail ADD/UPDATE/DELETE.
        if (!await db.CurrencyExHists.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            var from = new DateTime(2024, 1, 1);
            db.CurrencyExHists.AddRange(
                new CurrencyExHist { OrgId = TenantContext.DefaultOrgId, CurrencyCode = "USD", NetworkID = "ALL", CurrencyName = "Đô la Mỹ", BuyRate = 24_800, SellRate = 25_200, InterEx = "Y", FunctionName = "WAS_Mst_CurrencyEx_Create", FunctionActionType = "ADD", HistRefType = "MST_CURRENTCYEX", CreatedAt = from, Remark = "Tỷ giá ban đầu" },
                new CurrencyExHist { OrgId = TenantContext.DefaultOrgId, CurrencyCode = "USD", NetworkID = "ALL", CurrencyName = "Đô la Mỹ", BuyRate = 25_000, SellRate = 25_400, InterEx = "Y", FunctionName = "WAS_Mst_CurrencyEx_Update", FunctionActionType = "UPDATE", HistRefType = "MST_CURRENTCYEX", CreatedAt = from.AddDays(30), Remark = "Điều chỉnh tỷ giá" }
            );
            await db.SaveChangesAsync();
        }

        // Giá xe theo CarSubSpec mẫu (Mst_CarSubSpecPrice): GTĐG/GTBĐTD + giá bán theo kênh.
        if (!await db.CarSubSpecPrices.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            var from = new DateTime(2024, 1, 1);
            db.CarSubSpecPrices.AddRange(
                new CarSubSpecPrice { OrgId = TenantContext.DefaultOrgId, CarCode = "VF8", SubSpecCode = "ECO", NetworkID = "ALL", GTDG = 1_100_000_000, GTBDTD = 1_150_000_000, SellPrice = 1_090_000_000, EffectDTimeStart = from, Remark = "Giá niêm yết" },
                new CarSubSpecPrice { OrgId = TenantContext.DefaultOrgId, CarCode = "VF8", SubSpecCode = "PLUS", NetworkID = "ALL", GTDG = 1_250_000_000, GTBDTD = 1_300_000_000, SellPrice = 1_240_000_000, EffectDTimeStart = from },
                new CarSubSpecPrice { OrgId = TenantContext.DefaultOrgId, CarCode = "VF8", SubSpecCode = "PLUS", NetworkID = "DEALER", GTDG = 1_250_000_000, GTBDTD = 1_300_000_000, SellPrice = 1_200_000_000, EffectDTimeStart = from, Remark = "Giá đại lý" }
            );
            await db.SaveChangesAsync();
        }

        // Quy cách × đơn vị tính mẫu (Mst_SpecUnit): hệ số quy đổi + kích thước/khối lượng.
        if (!await db.SpecUnits.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            db.SpecUnits.AddRange(
                new SpecUnit { OrgId = TenantContext.DefaultOrgId, SpecCode = "SP-001", UnitCode = "CAI", NetworkID = "ALL", StandardUnitCode = "CAI", SpecUnitDesc = "Cái (đơn vị chuẩn)", Qty = 1, Weight = 12.5m, Remark = "Đơn vị chuẩn" },
                new SpecUnit { OrgId = TenantContext.DefaultOrgId, SpecCode = "SP-001", UnitCode = "THUNG", NetworkID = "ALL", StandardUnitCode = "CAI", SpecUnitDesc = "Thùng 12 cái", Qty = 12, Length = 60, Width = 40, Height = 30, Volume = 0.072m, Weight = 150, Remark = "Quy đổi theo thùng" },
                new SpecUnit { OrgId = TenantContext.DefaultOrgId, SpecCode = "SP-002", UnitCode = "BO", NetworkID = "ALL", StandardUnitCode = "BO", SpecUnitDesc = "Bộ (đơn vị chuẩn)", Qty = 1 }
            );
            await db.SaveChangesAsync();
        }

        // Lịch sử giá theo quy cách mẫu (Mst_SpecPriceHist): audit trail ADD/UPDATE/DELETE.
        if (!await db.SpecPriceHists.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            var from = new DateTime(2024, 1, 1);
            db.SpecPriceHists.AddRange(
                new SpecPriceHist { OrgId = TenantContext.DefaultOrgId, SpecCode = "SP-001", UnitCode = "CAI", NetworkID = "ALL", BuyPrice = 8_000_000, SellPrice = 9_500_000, DiscountVND = 0, VATRateCode = "VAT10", EffectDTimeStart = from, FunctionName = "WAS_Mst_SpecPrice_Create", FunctionActionType = "ADD", HistRefType = "MST_SPECPRICE", CreatedAt = from, Remark = "Giá niêm yết ban đầu" },
                new SpecPriceHist { OrgId = TenantContext.DefaultOrgId, SpecCode = "SP-001", UnitCode = "CAI", NetworkID = "ALL", BuyPrice = 8_000_000, SellPrice = 10_000_000, DiscountVND = 500_000, VATRateCode = "VAT10", EffectDTimeStart = from, FunctionName = "WAS_Mst_SpecPrice_Update", FunctionActionType = "UPDATE", HistRefType = "MST_SPECPRICE", CreatedAt = from.AddDays(30), Remark = "Điều chỉnh giá bán + chiết khấu" }
            );
            await db.SaveChangesAsync();
        }

        // Quy đổi tiền tệ mẫu (Mst_CurrencyConvert): cặp tiền tệ nguồn→đích × kênh, tỷ giá + giá trị quy đổi theo hiệu lực.
        if (!await db.CurrencyConverts.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            var from = new DateTime(2024, 1, 1);
            db.CurrencyConverts.AddRange(
                new CurrencyConvert { OrgId = TenantContext.DefaultOrgId, CurrencyCode = "USD", CurrencyCodeV = "VND", NetworkID = "ALL", CurrencyNameV = "Việt Nam Đồng", BaseCurrencyCode = "VND", BuyRate = 25_000, SellRate = 25_400, ValConvert = 25_400, ValConvertP = 25_000, ValConvertToVND = 25_400, EffectDTimeStartV = from, Remark = "Quy đổi USD→VND" },
                new CurrencyConvert { OrgId = TenantContext.DefaultOrgId, CurrencyCode = "EUR", CurrencyCodeV = "VND", NetworkID = "ALL", CurrencyNameV = "Việt Nam Đồng", BaseCurrencyCode = "VND", BuyRate = 27_000, SellRate = 27_500, ValConvert = 27_500, ValConvertP = 27_000, ValConvertToVND = 27_500, EffectDTimeStartV = from, Remark = "Quy đổi EUR→VND" },
                new CurrencyConvert { OrgId = TenantContext.DefaultOrgId, CurrencyCode = "USD", CurrencyCodeV = "VND", NetworkID = "DEALER", CurrencyNameV = "Việt Nam Đồng", BaseCurrencyCode = "VND", BuyRate = 25_100, SellRate = 25_500, ValConvert = 25_500, ValConvertP = 25_100, ValConvertToVND = 25_500, EffectDTimeStartV = from, Remark = "Tỷ giá đại lý" }
            );
            await db.SaveChangesAsync();
        }

        // Danh mục đơn vị tính mẫu (Mst_Unit): mã hệ thống + mã người dùng + tên đơn vị tính.
        if (!await db.Units.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            db.Units.AddRange(
                new Unit { OrgId = TenantContext.DefaultOrgId, UnitCode = "CAI", NetworkID = "ALL", UnitCodeUser = "CAI", UnitName = "Cái", Remark = "Đơn vị chuẩn" },
                new Unit { OrgId = TenantContext.DefaultOrgId, UnitCode = "THUNG", NetworkID = "ALL", UnitCodeUser = "THUNG", UnitName = "Thùng", Remark = "Thùng đóng gói" },
                new Unit { OrgId = TenantContext.DefaultOrgId, UnitCode = "BO", NetworkID = "ALL", UnitCodeUser = "BO", UnitName = "Bộ" }
            );
            await db.SaveChangesAsync();
        }

        // Quy cách / sản phẩm mẫu (Mst_Spec): danh mục gốc của bảng giá, SpecPrice tham chiếu qua SpecCode.
        if (!await db.Specs.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            db.Specs.AddRange(
                new Spec { OrgId = TenantContext.DefaultOrgId, SpecCode = "SP-001", NetworkID = "ALL", SpecName = "Máy lọc nước RO", SpecDesc = "Máy lọc nước gia đình", ModelCode = "RO-100", SpecType1 = "GIA DUNG", Color = "Trắng", FlagHasSerial = true, FlagHasLOT = false, DefaultUnitCode = "CAI", StandardUnitCode = "CAI", Remark = "Quy cách chuẩn" },
                new Spec { OrgId = TenantContext.DefaultOrgId, SpecCode = "SP-002", NetworkID = "ALL", SpecName = "Bộ lõi lọc", SpecDesc = "Bộ lõi lọc thay thế", ModelCode = "RO-100", SpecType1 = "PHU KIEN", Color = "Xanh", FlagHasSerial = false, FlagHasLOT = true, DefaultUnitCode = "BO", StandardUnitCode = "BO" }
            );
            await db.SaveChangesAsync();
        }

        // Mã giảm giá / chiết khấu mẫu (Inos_DiscountCode): loại giảm giá + giá trị + hiệu lực.
        if (!await db.DiscountCodes.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            var from = new DateTime(2024, 1, 1);
            db.DiscountCodes.AddRange(
                new DiscountCode { OrgId = TenantContext.DefaultOrgId, Code = "SALE10", DiscountType = "Percent", DiscountAmount = 10, RemainQty = 100, Description = "Giảm 10%", Enabled = true, EffectDateFrom = from },
                new DiscountCode { OrgId = TenantContext.DefaultOrgId, Code = "GIAM500K", DiscountType = "Absolute", DiscountAmount = 500_000, RemainQty = 50, Description = "Giảm 500.000đ", Enabled = true, EffectDateFrom = from },
                new DiscountCode { OrgId = TenantContext.DefaultOrgId, Code = "TET2024", DiscountType = "Percent", DiscountAmount = 15, RemainQty = 0, Description = "Khuyến mãi Tết (hết lượt)", Enabled = false, EffectDateFrom = from, EffectDateTo = from.AddMonths(2) }
            );
            await db.SaveChangesAsync();
        }

        // Danh mục model / dòng sản phẩm mẫu (Mst_Model): danh mục gốc của bảng giá, Spec tham chiếu qua ModelCode.
        if (!await db.Models.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            db.Models.AddRange(
                new Model { OrgId = TenantContext.DefaultOrgId, ModelCode = "RO-100", NetworkID = "ALL", ModelName = "Máy lọc nước RO 100", OrgModelCode = "RO100", BrandCode = "KANGAROO", Remark = "Dòng máy lọc gia đình" },
                new Model { OrgId = TenantContext.DefaultOrgId, ModelCode = "RO-200", NetworkID = "ALL", ModelName = "Máy lọc nước RO 200", OrgModelCode = "RO200", BrandCode = "KANGAROO", Remark = "Dòng máy lọc công suất lớn" },
                new Model { OrgId = TenantContext.DefaultOrgId, ModelCode = "RO-100", NetworkID = "DEALER", ModelName = "Máy lọc nước RO 100 (đại lý)", OrgModelCode = "RO100", BrandCode = "KANGAROO", NetworkModelCode = "RO-100", Remark = "Model theo kênh đại lý" }
            );
            await db.SaveChangesAsync();
        }

        // Danh mục hãng / thương hiệu mẫu (Mst_Brand): danh mục gốc của bảng giá, Model tham chiếu qua BrandCode.
        if (!await db.Brands.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            db.Brands.AddRange(
                new Brand { OrgId = TenantContext.DefaultOrgId, BrandCode = "KANGAROO", NetworkID = "ALL", BrandName = "Kangaroo", Remark = "Thương hiệu máy lọc nước" },
                new Brand { OrgId = TenantContext.DefaultOrgId, BrandCode = "AQUA", NetworkID = "ALL", BrandName = "Aqua", Remark = "Thương hiệu gia dụng" },
                new Brand { OrgId = TenantContext.DefaultOrgId, BrandCode = "KANGAROO", NetworkID = "DEALER", BrandName = "Kangaroo (đại lý)", NetworkBrandCode = "KANGAROO", Remark = "Hãng theo kênh đại lý" }
            );
            await db.SaveChangesAsync();
        }

        // Danh mục phân loại quy cách cấp 1 mẫu (Mst_SpecType1): danh mục gốc của bảng giá, Spec tham chiếu qua SpecType1.
        if (!await db.SpecType1s.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            db.SpecType1s.AddRange(
                new SpecType1 { OrgId = TenantContext.DefaultOrgId, SpecType1Code = "GIA DUNG", NetworkID = "ALL", SpecType1Name = "Gia dụng", Remark = "Nhóm sản phẩm gia đình" },
                new SpecType1 { OrgId = TenantContext.DefaultOrgId, SpecType1Code = "PHU KIEN", NetworkID = "ALL", SpecType1Name = "Phụ kiện", Remark = "Nhóm phụ kiện thay thế" },
                new SpecType1 { OrgId = TenantContext.DefaultOrgId, SpecType1Code = "GIA DUNG", NetworkID = "DEALER", SpecType1Name = "Gia dụng (đại lý)", Remark = "Phân loại theo kênh đại lý" }
            );
            await db.SaveChangesAsync();
        }

        // Danh mục phân loại quy cách cấp 2 mẫu (Mst_SpecType2): danh mục gốc của bảng giá, Spec tham chiếu qua SpecType2.
        if (!await db.SpecType2s.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            db.SpecType2s.AddRange(
                new SpecType2 { OrgId = TenantContext.DefaultOrgId, SpecType2Code = "LOC NUOC", NetworkID = "ALL", SpecType2Name = "Lọc nước", Remark = "Phân loại con máy lọc" },
                new SpecType2 { OrgId = TenantContext.DefaultOrgId, SpecType2Code = "LINH KIEN", NetworkID = "ALL", SpecType2Name = "Linh kiện", Remark = "Phân loại con linh kiện" }
            );
            await db.SaveChangesAsync();
        }

        // Danh mục nhóm hàng mẫu (Mst_ProductGroup): danh mục gốc của bảng giá, dùng để áp giá theo nhóm.
        if (!await db.ProductGroups.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            db.ProductGroups.AddRange(
                new ProductGroup { OrgId = TenantContext.DefaultOrgId, ProductGrpCode = "GIA DUNG", NetworkID = "ALL", ProductGrpName = "Gia dụng", ProductGrpDesc = "Nhóm hàng gia đình", ProductGrpBUCode = "ALL", ProductGrpBUPattern = "ALL%", ProductGrpLevel = 0, FlagFG = true, CodeGuid = Guid.NewGuid().ToString() },
                new ProductGroup { OrgId = TenantContext.DefaultOrgId, ProductGrpCode = "LOC NUOC", NetworkID = "ALL", ProductGrpCodeParent = "GIA DUNG", ProductGrpName = "Máy lọc nước", ProductGrpDesc = "Nhóm máy lọc nước", BrandCode = "KANGAROO", ProductGrpBUCode = "GIA DUNG", ProductGrpBUPattern = "GIA DUNG%", ProductGrpLevel = 1, FlagFG = true, CodeGuid = Guid.NewGuid().ToString() },
                new ProductGroup { OrgId = TenantContext.DefaultOrgId, ProductGrpCode = "PHU KIEN", NetworkID = "ALL", ProductGrpCodeParent = "GIA DUNG", ProductGrpName = "Phụ kiện", ProductGrpDesc = "Nhóm phụ kiện thay thế", ProductGrpBUCode = "GIA DUNG", ProductGrpBUPattern = "GIA DUNG%", ProductGrpLevel = 1, FlagFG = false, CodeGuid = Guid.NewGuid().ToString() }
            );
            await db.SaveChangesAsync();
        }

        // Trường mở rộng của quy cách mẫu (Mst_SpecCustomField): khai báo CustomField1..10 gắn vào Spec.
        if (!await db.SpecCustomFields.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            db.SpecCustomFields.AddRange(
                new SpecCustomField { OrgId = TenantContext.DefaultOrgId, SpecCustomFieldCode = "CF1", NetworkID = "ALL", SpecCustomFieldName = "Xuất xứ", DBPhysicalType = "nvarchar(255)", Remark = "Trường mở rộng 1" },
                new SpecCustomField { OrgId = TenantContext.DefaultOrgId, SpecCustomFieldCode = "CF2", NetworkID = "ALL", SpecCustomFieldName = "Bảo hành (tháng)", DBPhysicalType = "int", Remark = "Trường mở rộng 2" },
                new SpecCustomField { OrgId = TenantContext.DefaultOrgId, SpecCustomFieldCode = "CF3", NetworkID = "ALL", SpecCustomFieldName = "Màu sắc", DBPhysicalType = "nvarchar(100)", Remark = "Trường mở rộng 3" }
            );
            await db.SaveChangesAsync();
        }

        // Danh mục loại tiền tệ mẫu (Mst_Currency): danh mục gốc của bảng giá, khai báo tiền tệ dùng để ghi giá.
        if (!await db.Currencies.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            db.Currencies.AddRange(
                new Currency { OrgId = TenantContext.DefaultOrgId, CurrencyCode = "VND", CurrencyName = "Việt Nam Đồng", FlagActive = true },
                new Currency { OrgId = TenantContext.DefaultOrgId, CurrencyCode = "USD", CurrencyName = "Đô la Mỹ", FlagActive = true },
                new Currency { OrgId = TenantContext.DefaultOrgId, CurrencyCode = "EUR", CurrencyName = "Euro", FlagActive = true }
            );
            await db.SaveChangesAsync();
        }

        // Danh mục loại SSCC mẫu (Mst_SSCCType): danh mục gốc của bảng giá, Product tham chiếu qua SSCCType.
        if (!await db.SsccTypes.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            db.SsccTypes.AddRange(
                new SsccType { OrgId = TenantContext.DefaultOrgId, SSCCType = "SSCC-PALLET", NetworkID = "ALL", SSCCTypeName = "Pallet" },
                new SsccType { OrgId = TenantContext.DefaultOrgId, SSCCType = "SSCC-CARTON", NetworkID = "ALL", SSCCTypeName = "Thùng carton" },
                new SsccType { OrgId = TenantContext.DefaultOrgId, SSCCType = "SSCC-BAG", NetworkID = "ALL", SSCCTypeName = "Bao/bì" }
            );
            await db.SaveChangesAsync();
        }

        // Danh mục đặc tính hàng hóa mẫu (Mst_Attribute): danh mục gốc của bảng giá, dùng để phân loại/áp giá.
        if (!await db.Attributes.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            db.Attributes.AddRange(
                new AttributeDef { OrgId = TenantContext.DefaultOrgId, AttributeCode = "CHATLIEU", NetworkID = "ALL", AttributeName = "Chất liệu" },
                new AttributeDef { OrgId = TenantContext.DefaultOrgId, AttributeCode = "MAUSAC", NetworkID = "ALL", AttributeName = "Màu sắc" },
                new AttributeDef { OrgId = TenantContext.DefaultOrgId, AttributeCode = "KICHTHUOC", NetworkID = "ALL", AttributeName = "Kích thước" }
            );
            await db.SaveChangesAsync();
        }

        // Danh mục đại lý mẫu (Mst_Dealer): danh mục gốc của bảng giá, đại lý là kênh nhận giá riêng (DEALER).
        if (!await db.Dealers.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            db.Dealers.AddRange(
                new Dealer { OrgId = TenantContext.DefaultOrgId, DLCode = "DL-HN", NetworkID = "DEALER", DLName = "Đại lý Hà Nội", Remark = "Đại lý khu vực miền Bắc" },
                new Dealer { OrgId = TenantContext.DefaultOrgId, DLCode = "DL-HCM", NetworkID = "DEALER", DLName = "Đại lý Hồ Chí Minh", Remark = "Đại lý khu vực miền Nam" },
                new Dealer { OrgId = TenantContext.DefaultOrgId, DLCode = "DL-DN", NetworkID = "DEALER", DLName = "Đại lý Đà Nẵng", FlagActive = false, Remark = "Tạm ngưng" }
            );
            await db.SaveChangesAsync();
        }

        // Danh mục nhóm khách hàng mẫu (Mst_CustomerGroup): danh mục gốc của bảng giá, dùng để áp giá theo nhóm khách hàng.
        if (!await db.CustomerGroups.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            db.CustomerGroups.AddRange(
                new CustomerGroup { OrgId = TenantContext.DefaultOrgId, CustomerGrpCode = "ALL", NetworkID = "ALL", CustomerGrpName = "Tất cả khách hàng", CustomerGrpDesc = "Nhóm gốc", CustomerGrpBUCode = "ALL", CustomerGrpBUPattern = "ALL%", CustomerGrpLevel = 1 },
                new CustomerGroup { OrgId = TenantContext.DefaultOrgId, CustomerGrpCode = "VIP", NetworkID = "ALL", CustomerGrpCodeParent = "ALL", CustomerGrpName = "Khách VIP", CustomerGrpDesc = "Khách hàng thân thiết", CustomerGrpBUCode = "ALL", CustomerGrpBUPattern = "ALL%", CustomerGrpLevel = 2 },
                new CustomerGroup { OrgId = TenantContext.DefaultOrgId, CustomerGrpCode = "DAILY", NetworkID = "ALL", CustomerGrpCodeParent = "ALL", CustomerGrpName = "Khách đại lý", CustomerGrpDesc = "Khách mua buôn", CustomerGrpBUCode = "ALL", CustomerGrpBUPattern = "ALL%", CustomerGrpLevel = 2 },
                new CustomerGroup { OrgId = TenantContext.DefaultOrgId, CustomerGrpCode = "VIP", NetworkID = "DEALER", CustomerGrpCodeParent = "ALL", CustomerGrpName = "Khách VIP (đại lý)", CustomerGrpDesc = "Nhóm VIP theo kênh đại lý", CustomerGrpBUCode = "ALL", CustomerGrpBUPattern = "ALL%", CustomerGrpLevel = 2 }
            );
            await db.SaveChangesAsync();
        }

        // Định mức nguyên vật liệu mẫu (Prd_BOM): hàng hóa cha COMBO = Σ thành phần × Qty.
        if (!await db.ProductBoms.AnyAsync(x => x.OrgId == TenantContext.DefaultOrgId))
        {
            db.ProductBoms.AddRange(
                new ProductBom { OrgId = TenantContext.DefaultOrgId, ProductCodeParent = "COMBO-RO", ProductCode = "SP-001", NetworkID = "ALL", Qty = 1, Remark = "Máy lọc RO" },
                new ProductBom { OrgId = TenantContext.DefaultOrgId, ProductCodeParent = "COMBO-RO", ProductCode = "SP-002", NetworkID = "ALL", Qty = 2, Remark = "2 bộ lõi lọc kèm theo" }
            );
            await db.SaveChangesAsync();
        }
    }
}
