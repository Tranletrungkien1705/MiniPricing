using Microsoft.EntityFrameworkCore;
using MiniPricing.Models;
namespace MiniPricing.Data;
public sealed class AppDbContext(DbContextOptions<AppDbContext> opt) : DbContext(opt)
{
    public DbSet<Org> Orgs => Set<Org>();
    public DbSet<PriceList> PriceLists => Set<PriceList>();
    public DbSet<PriceItem> PriceItems => Set<PriceItem>();
    public DbSet<SpecPrice> SpecPrices => Set<SpecPrice>();
    public DbSet<SpecPriceHist> SpecPriceHists => Set<SpecPriceHist>();
    public DbSet<CarSubSpecPrice> CarSubSpecPrices => Set<CarSubSpecPrice>();
    public DbSet<VatRate> VatRates => Set<VatRate>();
    public DbSet<CurrencyEx> CurrencyExes => Set<CurrencyEx>();
    public DbSet<SpecUnit> SpecUnits => Set<SpecUnit>();
    public DbSet<CurrencyConvert> CurrencyConverts => Set<CurrencyConvert>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Spec> Specs => Set<Spec>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Org>().HasIndex(x => x.ApiKey).IsUnique();
        b.Entity<PriceList>().HasIndex(x => new { x.OrgId, x.Code }).IsUnique();
        b.Entity<PriceItem>().HasIndex(x => new { x.OrgId, x.PriceListId, x.ItemCode, x.Tier }).IsUnique();
        b.Entity<SpecPrice>().HasIndex(x => new { x.OrgId, x.SpecCode, x.UnitCode, x.NetworkID, x.EffectDTimeStart }).IsUnique();
        b.Entity<SpecPriceHist>().HasIndex(x => new { x.OrgId, x.SpecCode, x.UnitCode, x.NetworkID, x.CreatedAt });
        b.Entity<CarSubSpecPrice>().HasIndex(x => new { x.OrgId, x.CarCode, x.SubSpecCode, x.NetworkID, x.EffectDTimeStart }).IsUnique();
        b.Entity<VatRate>().HasIndex(x => new { x.OrgId, x.VATRateCode, x.NetworkID }).IsUnique();
        b.Entity<CurrencyEx>().HasIndex(x => new { x.OrgId, x.CurrencyCode, x.NetworkID }).IsUnique();
        b.Entity<SpecUnit>().HasIndex(x => new { x.OrgId, x.SpecCode, x.UnitCode, x.NetworkID }).IsUnique();
        b.Entity<CurrencyConvert>().HasIndex(x => new { x.OrgId, x.CurrencyCode, x.CurrencyCodeV, x.NetworkID, x.EffectDTimeStartV }).IsUnique();
        b.Entity<Unit>().HasIndex(x => new { x.OrgId, x.UnitCode, x.NetworkID }).IsUnique();
        b.Entity<Spec>().HasIndex(x => new { x.OrgId, x.SpecCode, x.NetworkID }).IsUnique();
    }
}
