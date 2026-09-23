using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MiniPricing.Data;
using MiniPricing.Models;
using MiniPricing.Services;
using Serilog;

JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
FleetObs.ConfigureLogger("minipricing");

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();
builder.WebHost.UseUrls($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");

var conn = Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=minipricing.db";
builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (DbUtil.IsPostgres(conn)) o.UseNpgsql(DbUtil.ToNpgsql(conn));
    else o.UseSqlite(conn);
});
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<ISpecPriceService, SpecPriceService>();
builder.Services.AddScoped<ICarSubSpecPriceService, CarSubSpecPriceService>();
builder.Services.AddScoped<IVatRateService, VatRateService>();
builder.Services.AddScoped<ICurrencyExService, CurrencyExService>();
builder.Services.AddScoped<ISpecUnitService, SpecUnitService>();

var ssoAuthority = Environment.GetEnvironmentVariable("SSO_AUTHORITY") ?? "https://minisso.onrender.com";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.Authority = ssoAuthority;
    o.RequireHttpsMetadata = ssoAuthority.StartsWith("https");
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = ssoAuthority,
        ValidateAudience = false, ValidateLifetime = true, NameClaimType = "name", RoleClaimType = "role"
    };
});
builder.Services.AddAuthorization();
builder.Services.AddFleetObs();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
    await Seeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

app.UseFleetObs();
FleetObs.ReportLicense(ssoAuthority, "minipricing");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/whoami", (ClaimsPrincipal u) => Results.Ok(new
{
    app = "minipricing",
    sub = u.FindFirst("sub")?.Value, name = u.Identity?.Name ?? u.FindFirst("name")?.Value,
    email = u.FindFirst("email")?.Value, tenant = u.FindFirst("tenant")?.Value,
    roles = u.FindAll("role").Select(c => c.Value)
})).RequireAuthorization();

app.Use(async (ctx, next) =>
{
    var key = ctx.Request.Headers["X-Api-Key"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(key)) ctx.Request.Cookies.TryGetValue(TenantContext.CookieName, out key);
    if (!string.IsNullOrWhiteSpace(key))
    {
        using var lookup = app.Services.CreateScope();
        var ldb = lookup.ServiceProvider.GetRequiredService<AppDbContext>();
        var org = await ldb.Orgs.FirstOrDefaultAsync(o => o.ApiKey == key);
        if (org != null) ctx.RequestServices.GetRequiredService<ITenantContext>().OrgId = org.Id;
    }
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/healthz", () => "ok");

// ===== Bảng giá =====
app.MapPost("/api/pricelists", async (CreateListDto dto, IPricingService svc) =>
{
    if (string.IsNullOrWhiteSpace(dto.Code)) return Results.BadRequest(new { error = "Cần Code." });
    try { return Results.Ok(await svc.CreateListAsync(dto)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

app.MapGet("/api/pricelists", async (IPricingService svc) => Results.Ok(await svc.ListAsync())).RequireAuthorization();

app.MapPost("/api/pricelists/{code}/items", async (string code, SetPriceDto dto, IPricingService svc) =>
{
    if (string.IsNullOrWhiteSpace(dto.ItemCode)) return Results.BadRequest(new { error = "Cần ItemCode." });
    try
    {
        var r = await svc.SetPriceAsync(code, dto);
        return r is null ? Results.NotFound(new { code }) : Results.Ok(r);
    }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

app.MapGet("/api/pricelists/{code}/items", async (string code, IPricingService svc) =>
{
    var r = await svc.ItemsAsync(code);
    return r is null ? Results.NotFound(new { code }) : Results.Ok(r);
}).RequireAuthorization();

app.MapPost("/api/pricelists/{code}/activate", async (string code, IPricingService svc) =>
{
    var r = await svc.ActivateAsync(code);
    return r is null ? Results.NotFound(new { code }) : Results.Ok(r);
}).RequireAuthorization();

// Tra giá hiệu lực — app fleet gọi để lấy đơn giá.
app.MapGet("/api/price", async (string item, IPricingService svc, string? tier, string? date) =>
    Results.Ok(await svc.ResolvePriceAsync(item, tier, date))).RequireAuthorization();

// ===== Giá theo quy cách (Mst_SpecPrice) — giá mua/bán + chiết khấu + VAT theo hiệu lực =====
app.MapPost("/api/specprices", async (UpsertSpecPriceDto dto, ISpecPriceService svc) =>
{
    if (string.IsNullOrWhiteSpace(dto.SpecCode) || string.IsNullOrWhiteSpace(dto.UnitCode))
        return Results.BadRequest(new { error = "Cần SpecCode và UnitCode." });
    try { return Results.Ok(await svc.UpsertAsync(dto)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

app.MapGet("/api/specprices", async (ISpecPriceService svc, string? specCode) =>
    Results.Ok(await svc.ListAsync(specCode))).RequireAuthorization();

app.MapDelete("/api/specprices", async (string specCode, string unitCode, ISpecPriceService svc, string? networkId, DateTime effectStart) =>
{
    var r = await svc.DeleteAsync(specCode, unitCode, networkId ?? "ALL", effectStart);
    return r is null ? Results.NotFound(new { specCode, unitCode }) : Results.Ok(r);
}).RequireAuthorization();

// Tra giá quy cách hiệu lực → giá sau chiết khấu + giá đã gồm VAT.
app.MapGet("/api/specprice", async (string spec, ISpecPriceService svc, string? unit, string? network, string? date) =>
    Results.Ok(await svc.ResolveAsync(spec, unit, network, date))).RequireAuthorization();

// ===== Giá xe theo CarSubSpec (Mst_CarSubSpecPrice) — GTĐG/GTBĐTD + giá bán theo hiệu lực =====
app.MapPost("/api/carsubspeccprices", async (UpsertCarSubSpecPriceDto dto, ICarSubSpecPriceService svc) =>
{
    if (string.IsNullOrWhiteSpace(dto.CarCode) || string.IsNullOrWhiteSpace(dto.SubSpecCode))
        return Results.BadRequest(new { error = "Cần CarCode và SubSpecCode." });
    try { return Results.Ok(await svc.UpsertAsync(dto)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

app.MapGet("/api/carsubspeccprices", async (ICarSubSpecPriceService svc, string? carCode) =>
    Results.Ok(await svc.ListAsync(carCode))).RequireAuthorization();

app.MapDelete("/api/carsubspeccprices", async (string carCode, string subSpecCode, ICarSubSpecPriceService svc, string? networkId, DateTime effectStart) =>
{
    var r = await svc.DeleteAsync(carCode, subSpecCode, networkId ?? "ALL", effectStart);
    return r is null ? Results.NotFound(new { carCode, subSpecCode }) : Results.Ok(r);
}).RequireAuthorization();

// Tra giá xe theo CarSubSpec hiệu lực → giá bán + chênh lệch so với GTĐG/GTBĐTD.
app.MapGet("/api/carsubspeccprice", async (string car, ICarSubSpecPriceService svc, string? subSpec, string? network, string? date) =>
    Results.Ok(await svc.ResolveAsync(car, subSpec, network, date))).RequireAuthorization();

// ===== Danh mục thuế suất VAT (Mst_VATRate) — % thuế suất + mô tả theo kênh =====
app.MapPost("/api/vatrates", async (UpsertVatRateDto dto, IVatRateService svc) =>
{
    if (string.IsNullOrWhiteSpace(dto.VATRateCode)) return Results.BadRequest(new { error = "Cần VATRateCode." });
    try { return Results.Ok(await svc.UpsertAsync(dto)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

app.MapGet("/api/vatrates", async (IVatRateService svc, string? vatRateCode) =>
    Results.Ok(await svc.ListAsync(vatRateCode))).RequireAuthorization();

app.MapDelete("/api/vatrates", async (string vatRateCode, IVatRateService svc, string? networkId) =>
{
    var r = await svc.DeleteAsync(vatRateCode, networkId ?? "ALL");
    return r is null ? Results.NotFound(new { vatRateCode }) : Results.Ok(r);
}).RequireAuthorization();

// Tra thuế suất hiệu lực theo mã + kênh.
app.MapGet("/api/vatrate", async (string code, IVatRateService svc, string? network) =>
    Results.Ok(await svc.ResolveAsync(code, network))).RequireAuthorization();

// ===== Tỷ giá ngoại tệ (Mst_CurrencyEx) — tỷ giá mua/bán theo mã tiền tệ × kênh =====
app.MapPost("/api/currencyexes", async (UpsertCurrencyExDto dto, ICurrencyExService svc) =>
{
    if (string.IsNullOrWhiteSpace(dto.CurrencyCode)) return Results.BadRequest(new { error = "Cần CurrencyCode." });
    try { return Results.Ok(await svc.UpsertAsync(dto)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

app.MapGet("/api/currencyexes", async (ICurrencyExService svc, string? currencyCode) =>
    Results.Ok(await svc.ListAsync(currencyCode))).RequireAuthorization();

app.MapDelete("/api/currencyexes", async (string currencyCode, ICurrencyExService svc, string? networkId) =>
{
    var r = await svc.DeleteAsync(currencyCode, networkId ?? "ALL");
    return r is null ? Results.NotFound(new { currencyCode }) : Results.Ok(r);
}).RequireAuthorization();

// Tra tỷ giá hiệu lực theo mã + kênh.
app.MapGet("/api/currencyex", async (string code, ICurrencyExService svc, string? network) =>
    Results.Ok(await svc.ResolveAsync(code, network))).RequireAuthorization();

// Quy đổi số tiền sang tiền tệ gốc theo tỷ giá hiệu lực (side=buy|sell).
app.MapGet("/api/currencyex/convert", async (string code, decimal amount, ICurrencyExService svc, string? network, string? side) =>
    Results.Ok(await svc.ConvertAsync(code, amount, network, side))).RequireAuthorization();

// ===== Quy cách × đơn vị tính (Mst_SpecUnit) — hệ số quy đổi + kích thước/khối lượng theo quy cách =====
app.MapPost("/api/specunits", async (UpsertSpecUnitDto dto, ISpecUnitService svc) =>
{
    if (string.IsNullOrWhiteSpace(dto.SpecCode) || string.IsNullOrWhiteSpace(dto.UnitCode))
        return Results.BadRequest(new { error = "Cần SpecCode và UnitCode." });
    try { return Results.Ok(await svc.UpsertAsync(dto)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

app.MapGet("/api/specunits", async (ISpecUnitService svc, string? specCode, string? unitCode) =>
    Results.Ok(await svc.ListAsync(specCode, unitCode))).RequireAuthorization();

app.MapDelete("/api/specunits", async (string specCode, string unitCode, ISpecUnitService svc, string? networkId) =>
{
    var r = await svc.DeleteAsync(specCode, unitCode, networkId ?? "ALL");
    return r is null ? Results.NotFound(new { specCode, unitCode }) : Results.Ok(r);
}).RequireAuthorization();

// Tra đơn vị tính hiệu lực theo quy cách + đơn vị + kênh.
app.MapGet("/api/specunit", async (string spec, string unit, ISpecUnitService svc, string? network) =>
    Results.Ok(await svc.ResolveAsync(spec, unit, network))).RequireAuthorization();

// Quy đổi số lượng theo đơn vị tính về số lượng theo đơn vị chuẩn.
app.MapGet("/api/specunit/convert", async (string spec, string unit, decimal qty, ISpecUnitService svc, string? network) =>
    Results.Ok(await svc.ConvertQtyAsync(spec, unit, qty, network))).RequireAuthorization();

// Import bulk giá thật (Mst_CarPrice nguồn 2010.HTC) — upsert 1 PriceList theo Code + nhiều PriceItem theo ItemCode.
app.MapPost("/api/import/priceitems", async (ImportPriceDto dto, AppDbContext db, ITenantContext tenant) =>
{
    var rows = dto.Rows ?? new List<ImportPriceRowDto>();
    if (rows.Count == 0) return Results.BadRequest(new { error = "Rows rỗng." });
    var code = (dto.Code ?? "REAL-CARPRICE").Trim().ToUpperInvariant();
    var l = await db.PriceLists.FirstOrDefaultAsync(x => x.OrgId == tenant.OrgId && x.Code == code);
    if (l is null)
    {
        l = new PriceList { OrgId = tenant.OrgId, Code = code, Name = dto.Name ?? "Giá xe thật (Mst_CarPrice)", EffectiveFrom = dto.EffectiveFrom ?? DateTime.Now.AddYears(-1), Status = "Active" };
        db.PriceLists.Add(l);
        await db.SaveChangesAsync();
    }
    int added = 0, updated = 0;
    foreach (var r in rows)
    {
        if (string.IsNullOrWhiteSpace(r.ItemCode) || r.Price <= 0) continue;
        var item = r.ItemCode.Trim().ToUpperInvariant();
        var pi = await db.PriceItems.FirstOrDefaultAsync(x => x.OrgId == tenant.OrgId && x.PriceListId == l.Id && x.ItemCode == item && x.Tier == "Default");
        if (pi is null) { db.PriceItems.Add(new PriceItem { OrgId = tenant.OrgId, PriceListId = l.Id, ItemCode = item, Tier = "Default", Price = r.Price }); added++; }
        else { pi.Price = r.Price; updated++; }
    }
    await db.SaveChangesAsync();
    return Results.Ok(new { list = l.Code, added, updated });
}).RequireAuthorization();

app.MapPost("/api/orgs/register", async (RegisterOrgDto dto, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(dto.Name)) return Results.BadRequest(new { error = "Cần Name." });
    var org = new Org { Name = dto.Name.Trim(), ApiKey = "prc_" + Guid.NewGuid().ToString("N") };
    db.Orgs.Add(org); await db.SaveChangesAsync();
    return Results.Ok(new { orgId = org.Id, apiKey = org.ApiKey });
});

app.Run();

record RegisterOrgDto(string Name);
record ImportPriceRowDto(string? ItemCode, decimal Price);
record ImportPriceDto(string? Code, string? Name, DateTime? EffectiveFrom, List<ImportPriceRowDto>? Rows);
