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
    var r = await svc.SetPriceAsync(code, dto);
    return r is null ? Results.NotFound(new { code }) : Results.Ok(r);
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

app.MapPost("/api/orgs/register", async (RegisterOrgDto dto, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(dto.Name)) return Results.BadRequest(new { error = "Cần Name." });
    var org = new Org { Name = dto.Name.Trim(), ApiKey = "prc_" + Guid.NewGuid().ToString("N") };
    db.Orgs.Add(org); await db.SaveChangesAsync();
    return Results.Ok(new { orgId = org.Id, apiKey = org.ApiKey });
});

app.Run();

record RegisterOrgDto(string Name);
