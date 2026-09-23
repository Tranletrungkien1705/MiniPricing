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
builder.Services.AddScoped<ISpecPriceHistService, SpecPriceHistService>();
builder.Services.AddScoped<ICarSubSpecPriceService, CarSubSpecPriceService>();
builder.Services.AddScoped<IVatRateService, VatRateService>();
builder.Services.AddScoped<ICurrencyExService, CurrencyExService>();
builder.Services.AddScoped<ICurrencyExHistService, CurrencyExHistService>();
builder.Services.AddScoped<ISpecUnitService, SpecUnitService>();
builder.Services.AddScoped<ICurrencyConvertService, CurrencyConvertService>();
builder.Services.AddScoped<IUnitService, UnitService>();
builder.Services.AddScoped<ISpecService, SpecService>();
builder.Services.AddScoped<IDiscountCodeService, DiscountCodeService>();
builder.Services.AddScoped<IModelService, ModelService>();
builder.Services.AddScoped<IBrandService, BrandService>();
builder.Services.AddScoped<ISpecTypeService, SpecTypeService>();
builder.Services.AddScoped<IProductGroupService, ProductGroupService>();
builder.Services.AddScoped<IProductTypeService, ProductTypeService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ISpecCustomFieldService, SpecCustomFieldService>();
builder.Services.AddScoped<ICurrencyService, CurrencyService>();

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

// ===== Lịch sử giá theo quy cách (Mst_SpecPriceHist) — audit trail biến động giá =====
app.MapPost("/api/specpricehists", async (RecordSpecPriceHistDto dto, ISpecPriceHistService svc) =>
{
    if (string.IsNullOrWhiteSpace(dto.SpecCode) || string.IsNullOrWhiteSpace(dto.UnitCode))
        return Results.BadRequest(new { error = "Cần SpecCode và UnitCode." });
    try { return Results.Ok(await svc.RecordAsync(dto)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

app.MapGet("/api/specpricehists", async (ISpecPriceHistService svc, string? specCode, string? unitCode, string? actionType) =>
    Results.Ok(await svc.ListAsync(specCode, unitCode, actionType))).RequireAuthorization();

// Dòng thời gian biến động giá của một quy cách × đơn vị × kênh.
app.MapGet("/api/specpricehist/timeline", async (string spec, ISpecPriceHistService svc, string? unit, string? network) =>
    Results.Ok(await svc.TimelineAsync(spec, unit, network))).RequireAuthorization();

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

// ===== Lịch sử tỷ giá ngoại tệ (Mst_CurrencyExHist) — audit trail biến động tỷ giá =====
app.MapPost("/api/currencyexhists", async (RecordCurrencyExHistDto dto, ICurrencyExHistService svc) =>
{
    if (string.IsNullOrWhiteSpace(dto.CurrencyCode))
        return Results.BadRequest(new { error = "Cần CurrencyCode." });
    try { return Results.Ok(await svc.RecordAsync(dto)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

app.MapGet("/api/currencyexhists", async (ICurrencyExHistService svc, string? currencyCode, string? actionType) =>
    Results.Ok(await svc.ListAsync(currencyCode, actionType))).RequireAuthorization();

// Dòng thời gian biến động tỷ giá của một mã tiền tệ × kênh.
app.MapGet("/api/currencyexhist/timeline", async (string code, ICurrencyExHistService svc, string? network) =>
    Results.Ok(await svc.TimelineAsync(code, network))).RequireAuthorization();

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

// ===== Quy đổi tiền tệ theo hiệu lực (Mst_CurrencyConvert) — tỷ giá mua/bán + giá trị quy đổi theo thời gian =====
app.MapPost("/api/currencyconverts", async (UpsertCurrencyConvertDto dto, ICurrencyConvertService svc) =>
{
    if (string.IsNullOrWhiteSpace(dto.CurrencyCode) || string.IsNullOrWhiteSpace(dto.CurrencyCodeV))
        return Results.BadRequest(new { error = "Cần CurrencyCode và CurrencyCodeV." });
    try { return Results.Ok(await svc.UpsertAsync(dto)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

app.MapGet("/api/currencyconverts", async (ICurrencyConvertService svc, string? currencyCode, string? currencyCodeV) =>
    Results.Ok(await svc.ListAsync(currencyCode, currencyCodeV))).RequireAuthorization();

app.MapDelete("/api/currencyconverts", async (string currencyCode, string currencyCodeV, ICurrencyConvertService svc, string? networkId, DateTime effectStart) =>
{
    var r = await svc.DeleteAsync(currencyCode, currencyCodeV, networkId ?? "ALL", effectStart);
    return r is null ? Results.NotFound(new { currencyCode, currencyCodeV }) : Results.Ok(r);
}).RequireAuthorization();

// Tra tỷ giá quy đổi hiệu lực theo cặp tiền tệ + kênh + thời điểm.
app.MapGet("/api/currencyconvert", async (string code, string codeV, ICurrencyConvertService svc, string? network, string? date) =>
    Results.Ok(await svc.ResolveAsync(code, codeV, network, date))).RequireAuthorization();

// Quy đổi số tiền từ tiền tệ nguồn sang tiền tệ đích theo tỷ giá hiệu lực (side=buy|sell).
app.MapGet("/api/currencyconvert/convert", async (string code, string codeV, decimal amount, ICurrencyConvertService svc, string? network, string? date, string? side) =>
    Results.Ok(await svc.ConvertAsync(code, codeV, amount, network, date, side))).RequireAuthorization();

// ===== Danh mục đơn vị tính (Mst_Unit) — mã/tên đơn vị tính + cờ hiệu lực theo kênh =====
app.MapPost("/api/units", async (UpsertUnitDto dto, IUnitService svc) =>
{
    if (string.IsNullOrWhiteSpace(dto.UnitCode)) return Results.BadRequest(new { error = "Cần UnitCode." });
    try { return Results.Ok(await svc.UpsertAsync(dto)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

app.MapGet("/api/units", async (IUnitService svc, string? unitCode, string? unitName) =>
    Results.Ok(await svc.ListAsync(unitCode, unitName))).RequireAuthorization();

app.MapDelete("/api/units", async (string unitCode, IUnitService svc, string? networkId) =>
{
    var r = await svc.DeleteAsync(unitCode, networkId ?? "ALL");
    return r is null ? Results.NotFound(new { unitCode }) : Results.Ok(r);
}).RequireAuthorization();

// Tra đơn vị tính hiệu lực theo mã + kênh.
app.MapGet("/api/unit", async (string code, IUnitService svc, string? network) =>
    Results.Ok(await svc.ResolveAsync(code, network))).RequireAuthorization();

// ===== Quy cách / sản phẩm (Mst_Spec) — danh mục gốc của bảng giá (SpecPrice tham chiếu qua SpecCode) =====
app.MapPost("/api/specs", async (UpsertSpecDto dto, ISpecService svc) =>
{
    if (string.IsNullOrWhiteSpace(dto.SpecCode)) return Results.BadRequest(new { error = "Cần SpecCode." });
    try { return Results.Ok(await svc.UpsertAsync(dto)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

app.MapGet("/api/specs", async (ISpecService svc, string? specCode, string? specName, string? modelCode) =>
    Results.Ok(await svc.ListAsync(specCode, specName, modelCode))).RequireAuthorization();

app.MapDelete("/api/specs", async (string specCode, ISpecService svc, string? networkId) =>
{
    var r = await svc.DeleteAsync(specCode, networkId ?? "ALL");
    return r is null ? Results.NotFound(new { specCode }) : Results.Ok(r);
}).RequireAuthorization();

// Tra quy cách hiệu lực theo mã + kênh.
app.MapGet("/api/spec", async (string code, ISpecService svc, string? network) =>
    Results.Ok(await svc.ResolveAsync(code, network))).RequireAuthorization();

// ===== Mã giảm giá / chiết khấu (Inos_DiscountCode) — loại giảm giá + giá trị + hiệu lực =====
app.MapPost("/api/discountcodes", async (UpsertDiscountCodeDto dto, IDiscountCodeService svc) =>
{
    if (string.IsNullOrWhiteSpace(dto.Code)) return Results.BadRequest(new { error = "Cần Code." });
    try { return Results.Ok(await svc.UpsertAsync(dto)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

app.MapGet("/api/discountcodes", async (IDiscountCodeService svc, string? code, bool? enabled) =>
    Results.Ok(await svc.ListAsync(code, enabled))).RequireAuthorization();

app.MapDelete("/api/discountcodes", async (string code, IDiscountCodeService svc) =>
{
    var r = await svc.DeleteAsync(code);
    return r is null ? Results.NotFound(new { code }) : Results.Ok(r);
}).RequireAuthorization();

// Tra mã giảm giá hiệu lực theo mã + thời điểm (kiểm tra tồn tại/Enabled/hiệu lực).
app.MapGet("/api/discountcode", async (string code, IDiscountCodeService svc, string? date) =>
    Results.Ok(await svc.ResolveAsync(code, DateTime.TryParse(date, out var d) ? d : null))).RequireAuthorization();

// Áp mã giảm giá lên một số tiền → số tiền giảm + số tiền sau giảm (trừ lượt còn lại).
app.MapGet("/api/discountcode/apply", async (string code, decimal amount, IDiscountCodeService svc, string? date) =>
{
    try { return Results.Ok(await svc.ApplyAsync(code, amount, DateTime.TryParse(date, out var d) ? d : null)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

// ===== Danh mục model / dòng sản phẩm (Mst_Model) — danh mục gốc của bảng giá (Spec tham chiếu qua ModelCode) =====
app.MapPost("/api/models", async (UpsertModelDto dto, IModelService svc) =>
{
    if (string.IsNullOrWhiteSpace(dto.ModelCode)) return Results.BadRequest(new { error = "Cần ModelCode." });
    try { return Results.Ok(await svc.UpsertAsync(dto)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

app.MapGet("/api/models", async (IModelService svc, string? modelCode, string? modelName, string? brandCode) =>
    Results.Ok(await svc.ListAsync(modelCode, modelName, brandCode))).RequireAuthorization();

app.MapDelete("/api/models", async (string modelCode, IModelService svc, string? networkId) =>
{
    var r = await svc.DeleteAsync(modelCode, networkId ?? "ALL");
    return r is null ? Results.NotFound(new { modelCode }) : Results.Ok(r);
}).RequireAuthorization();

// Tra model hiệu lực theo mã + kênh.
app.MapGet("/api/model", async (string code, IModelService svc, string? network) =>
    Results.Ok(await svc.ResolveAsync(code, network))).RequireAuthorization();

// ===== Danh mục hãng / thương hiệu (Mst_Brand) — danh mục gốc của bảng giá (Model tham chiếu qua BrandCode) =====
app.MapPost("/api/brands", async (UpsertBrandDto dto, IBrandService svc) =>
{
    if (string.IsNullOrWhiteSpace(dto.BrandCode)) return Results.BadRequest(new { error = "Cần BrandCode." });
    try { return Results.Ok(await svc.UpsertAsync(dto)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

app.MapGet("/api/brands", async (IBrandService svc, string? brandCode, string? brandName) =>
    Results.Ok(await svc.ListAsync(brandCode, brandName))).RequireAuthorization();

app.MapDelete("/api/brands", async (string brandCode, IBrandService svc, string? networkId) =>
{
    var r = await svc.DeleteAsync(brandCode, networkId ?? "ALL");
    return r is null ? Results.NotFound(new { brandCode }) : Results.Ok(r);
}).RequireAuthorization();

// Tra hãng hiệu lực theo mã + kênh.
app.MapGet("/api/brand", async (string code, IBrandService svc, string? network) =>
    Results.Ok(await svc.ResolveAsync(code, network))).RequireAuthorization();

// ===== Danh mục phân loại quy cách cấp 1 (Mst_SpecType1) — danh mục gốc của bảng giá (Spec tham chiếu qua SpecType1) =====
app.MapPost("/api/spectype1s", async (UpsertSpecTypeDto dto, ISpecTypeService svc) =>
{
    if (string.IsNullOrWhiteSpace(dto.Code)) return Results.BadRequest(new { error = "Cần Code." });
    try { return Results.Ok(await svc.UpsertType1Async(dto)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

app.MapGet("/api/spectype1s", async (ISpecTypeService svc, string? code, string? name) =>
    Results.Ok(await svc.ListType1Async(code, name))).RequireAuthorization();

app.MapDelete("/api/spectype1s", async (string code, ISpecTypeService svc, string? networkId) =>
{
    var r = await svc.DeleteType1Async(code, networkId ?? "ALL");
    return r is null ? Results.NotFound(new { code }) : Results.Ok(r);
}).RequireAuthorization();

// Tra phân loại 1 hiệu lực theo mã + kênh.
app.MapGet("/api/spectype1", async (string code, ISpecTypeService svc, string? network) =>
    Results.Ok(await svc.ResolveType1Async(code, network))).RequireAuthorization();

// ===== Danh mục phân loại quy cách cấp 2 (Mst_SpecType2) — danh mục gốc của bảng giá (Spec tham chiếu qua SpecType2) =====
app.MapPost("/api/spectype2s", async (UpsertSpecTypeDto dto, ISpecTypeService svc) =>
{
    if (string.IsNullOrWhiteSpace(dto.Code)) return Results.BadRequest(new { error = "Cần Code." });
    try { return Results.Ok(await svc.UpsertType2Async(dto)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

app.MapGet("/api/spectype2s", async (ISpecTypeService svc, string? code, string? name) =>
    Results.Ok(await svc.ListType2Async(code, name))).RequireAuthorization();

app.MapDelete("/api/spectype2s", async (string code, ISpecTypeService svc, string? networkId) =>
{
    var r = await svc.DeleteType2Async(code, networkId ?? "ALL");
    return r is null ? Results.NotFound(new { code }) : Results.Ok(r);
}).RequireAuthorization();

// Tra phân loại 2 hiệu lực theo mã + kênh.
app.MapGet("/api/spectype2", async (string code, ISpecTypeService svc, string? network) =>
    Results.Ok(await svc.ResolveType2Async(code, network))).RequireAuthorization();

// ===== Danh mục nhóm hàng (Mst_ProductGroup) — danh mục gốc của bảng giá, dùng để áp giá theo nhóm =====
app.MapPost("/api/productgroups", async (UpsertProductGroupDto dto, IProductGroupService svc) =>
{
    if (string.IsNullOrWhiteSpace(dto.ProductGrpCode)) return Results.BadRequest(new { error = "Cần ProductGrpCode." });
    try { return Results.Ok(await svc.UpsertAsync(dto)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

app.MapGet("/api/productgroups", async (IProductGroupService svc, string? productGrpCode, string? productGrpName, string? brandCode) =>
    Results.Ok(await svc.ListAsync(productGrpCode, productGrpName, brandCode))).RequireAuthorization();

app.MapDelete("/api/productgroups", async (string productGrpCode, IProductGroupService svc, string? networkId) =>
{
    try
    {
        var r = await svc.DeleteAsync(productGrpCode, networkId ?? "ALL");
        return r is null ? Results.NotFound(new { productGrpCode }) : Results.Ok(r);
    }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

// Tra nhóm hàng hiệu lực theo mã + kênh.
app.MapGet("/api/productgroup", async (string code, IProductGroupService svc, string? network) =>
    Results.Ok(await svc.ResolveAsync(code, network))).RequireAuthorization();

// Cây nhóm hàng theo kênh (dựng theo ProductGrpCodeParent).
app.MapGet("/api/productgroup/tree", async (IProductGroupService svc, string? network) =>
    Results.Ok(await svc.TreeAsync(network))).RequireAuthorization();

// ===== Danh mục loại hàng hóa (Mst_ProductType) — danh mục gốc của bảng giá (Product tham chiếu qua ProductType) =====
app.MapPost("/api/producttypes", async (UpsertProductTypeDto dto, IProductTypeService svc) =>
{
    if (string.IsNullOrWhiteSpace(dto.ProductTypeCode)) return Results.BadRequest(new { error = "Cần ProductTypeCode." });
    try { return Results.Ok(await svc.UpsertAsync(dto)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

app.MapGet("/api/producttypes", async (IProductTypeService svc, string? code, string? name) =>
    Results.Ok(await svc.ListAsync(code, name))).RequireAuthorization();

app.MapDelete("/api/producttypes", async (string code, IProductTypeService svc, string? networkId) =>
{
    try
    {
        var r = await svc.DeleteAsync(code, networkId ?? "ALL");
        return r is null ? Results.NotFound(new { code }) : Results.Ok(r);
    }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

// Tra loại hàng hóa hiệu lực theo mã + kênh.
app.MapGet("/api/producttype", async (string code, IProductTypeService svc, string? network) =>
    Results.Ok(await svc.ResolveAsync(code, network))).RequireAuthorization();

// ===== Trường mở rộng của quy cách (Mst_SpecCustomField) — danh mục gốc của bảng giá, khai báo CustomField1..10 gắn vào Spec =====
app.MapPost("/api/speccustomfields", async (UpsertSpecCustomFieldDto dto, ISpecCustomFieldService svc) =>
{
    if (string.IsNullOrWhiteSpace(dto.SpecCustomFieldCode)) return Results.BadRequest(new { error = "Cần SpecCustomFieldCode." });
    try { return Results.Ok(await svc.UpsertAsync(dto)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

app.MapGet("/api/speccustomfields", async (ISpecCustomFieldService svc, string? code, string? name) =>
    Results.Ok(await svc.ListAsync(code, name))).RequireAuthorization();

app.MapDelete("/api/speccustomfields", async (string code, ISpecCustomFieldService svc, string? networkId) =>
{
    var r = await svc.DeleteAsync(code, networkId ?? "ALL");
    return r is null ? Results.NotFound(new { code }) : Results.Ok(r);
}).RequireAuthorization();

// Tra trường mở rộng hiệu lực theo mã + kênh.
app.MapGet("/api/speccustomfield", async (string code, ISpecCustomFieldService svc, string? network) =>
    Results.Ok(await svc.ResolveAsync(code, network))).RequireAuthorization();

// ===== Danh mục loại tiền tệ (Mst_Currency) — danh mục gốc của bảng giá, khai báo tiền tệ dùng để ghi giá =====
app.MapPost("/api/currencies", async (UpsertCurrencyDto dto, ICurrencyService svc) =>
{
    if (string.IsNullOrWhiteSpace(dto.CurrencyCode)) return Results.BadRequest(new { error = "Cần CurrencyCode." });
    try { return Results.Ok(await svc.UpsertAsync(dto)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

app.MapGet("/api/currencies", async (ICurrencyService svc, string? code, string? name) =>
    Results.Ok(await svc.ListAsync(code, name))).RequireAuthorization();

app.MapDelete("/api/currencies", async (string code, ICurrencyService svc) =>
{
    var r = await svc.DeleteAsync(code);
    return r is null ? Results.NotFound(new { code }) : Results.Ok(r);
}).RequireAuthorization();

// Tra loại tiền tệ hiệu lực theo mã.
app.MapGet("/api/currency", async (string code, ICurrencyService svc) =>
    Results.Ok(await svc.ResolveAsync(code))).RequireAuthorization();

// ===== Hàng hóa / sản phẩm (Mst_Product) — danh mục gốc mang giá mua/bán đề xuất + VAT + đơn vị tính =====
app.MapPost("/api/products", async (UpsertProductDto dto, IProductService svc) =>
{
    if (string.IsNullOrWhiteSpace(dto.ProductCode)) return Results.BadRequest(new { error = "Cần ProductCode." });
    try { return Results.Ok(await svc.UpsertAsync(dto)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

app.MapGet("/api/products", async (IProductService svc, string? productCode, string? productName, string? productGrpCode, string? brandCode) =>
    Results.Ok(await svc.ListAsync(productCode, productName, productGrpCode, brandCode))).RequireAuthorization();

app.MapDelete("/api/products", async (string productCode, IProductService svc, string? networkId) =>
{
    try
    {
        var r = await svc.DeleteAsync(productCode, networkId ?? "ALL");
        return r is null ? Results.NotFound(new { productCode }) : Results.Ok(r);
    }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}).RequireAuthorization();

// Tra hàng hóa hiệu lực theo mã + kênh.
app.MapGet("/api/product", async (string code, IProductService svc, string? network) =>
    Results.Ok(await svc.ResolveAsync(code, network))).RequireAuthorization();

// Tra giá hàng hóa hiệu lực → giá bán đề xuất + giá đã gồm VAT (theo VATRateCode).
app.MapGet("/api/product/price", async (string code, IProductService svc, string? network) =>
    Results.Ok(await svc.ResolvePriceAsync(code, network))).RequireAuthorization();

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
