using Microsoft.EntityFrameworkCore;
using MiniPricing.Models;
namespace MiniPricing.Data;
public sealed class AppDbContext(DbContextOptions<AppDbContext> opt) : DbContext(opt)
{
    public DbSet<Org> Orgs => Set<Org>();
    public DbSet<PriceList> PriceLists => Set<PriceList>();
    public DbSet<PriceItem> PriceItems => Set<PriceItem>();
    public DbSet<SpecPrice> SpecPrices => Set<SpecPrice>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Org>().HasIndex(x => x.ApiKey).IsUnique();
        b.Entity<PriceList>().HasIndex(x => new { x.OrgId, x.Code }).IsUnique();
        b.Entity<PriceItem>().HasIndex(x => new { x.OrgId, x.PriceListId, x.ItemCode, x.Tier }).IsUnique();
        b.Entity<SpecPrice>().HasIndex(x => new { x.OrgId, x.SpecCode, x.UnitCode, x.NetworkID, x.EffectDTimeStart }).IsUnique();
    }
}
