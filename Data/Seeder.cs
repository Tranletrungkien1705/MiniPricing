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
    }
}
