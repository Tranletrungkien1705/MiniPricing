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
    }
}
