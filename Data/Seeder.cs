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
    }
}
