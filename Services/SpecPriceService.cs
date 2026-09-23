using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;

namespace MiniPricing.Services;

// ===== Giá theo quy cách (port từ Mst_SpecPrice — 2019.4.ProductCenter) =====
// Nghiệp vụ nguồn: mỗi quy cách (SpecCode) × đơn vị (UnitCode) × kênh (NetworkID)
// có giá mua/giá bán, chiết khấu VND, thuế suất VAT và khoảng hiệu lực riêng.
// Tra giá hiệu lực → tính giá sau chiết khấu và giá đã gồm VAT.

public record UpsertSpecPriceDto(
    string SpecCode, string UnitCode, string? NetworkID,
    decimal BuyPrice, decimal SellPrice, decimal DiscountVND,
    string? CurrencyCode, string? VATRateCode,
    DateTime EffectDTimeStart, DateTime? EffectDTimeEnd, string? Remark);

public interface ISpecPriceService
{
    Task<object> UpsertAsync(UpsertSpecPriceDto dto);
    Task<object> ListAsync(string? specCode);
    Task<object?> DeleteAsync(string specCode, string unitCode, string networkId, DateTime effectStart);
    Task<object> ResolveAsync(string specCode, string? unitCode, string? networkId, string? date);
}

public sealed class SpecPriceService(AppDbContext db, ITenantContext tenant) : ISpecPriceService
{
    private Guid Org => tenant.OrgId;

    // Thuế suất theo mã (nguồn dùng VATRateCode; ở đây map ra % để tính giá gồm VAT).
    private static decimal VatPercent(string? code) => (code ?? "").Trim().ToUpperInvariant() switch
    {
        "VAT0" => 0m,
        "VAT5" => 5m,
        "VAT8" => 8m,
        "VAT10" => 10m,
        _ => 10m
    };

    public async Task<object> UpsertAsync(UpsertSpecPriceDto dto)
    {
        var spec = dto.SpecCode.Trim().ToUpperInvariant();
        var unit = dto.UnitCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(dto.NetworkID) ? "ALL" : dto.NetworkID!.Trim().ToUpperInvariant();
        if (dto.SellPrice < 0 || dto.BuyPrice < 0) throw new InvalidOperationException("Giá không được âm.");
        if (dto.DiscountVND < 0) throw new InvalidOperationException("Chiết khấu không được âm.");
        if (dto.EffectDTimeEnd is { } end && end < dto.EffectDTimeStart)
            throw new InvalidOperationException("Ngày kết thúc hiệu lực phải sau ngày bắt đầu.");

        var row = await db.SpecPrices.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.SpecCode == spec && x.UnitCode == unit && x.NetworkID == net
            && x.EffectDTimeStart == dto.EffectDTimeStart);
        if (row is null)
        {
            row = new SpecPrice { OrgId = Org, SpecCode = spec, UnitCode = unit, NetworkID = net, EffectDTimeStart = dto.EffectDTimeStart };
            db.SpecPrices.Add(row);
        }
        row.BuyPrice = dto.BuyPrice;
        row.SellPrice = dto.SellPrice;
        row.DiscountVND = dto.DiscountVND;
        row.CurrencyCode = string.IsNullOrWhiteSpace(dto.CurrencyCode) ? "VND" : dto.CurrencyCode!.Trim().ToUpperInvariant();
        row.VATRateCode = string.IsNullOrWhiteSpace(dto.VATRateCode) ? "VAT10" : dto.VATRateCode!.Trim().ToUpperInvariant();
        row.EffectDTimeEnd = dto.EffectDTimeEnd;
        row.Remark = dto.Remark;
        row.FlagActive = true;
        row.LogLUDTimeUTC = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Project(row);
    }

    public async Task<object> ListAsync(string? specCode)
    {
        var q = db.SpecPrices.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(specCode))
        {
            var s = specCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.SpecCode == s);
        }
        var rows = await q.OrderBy(x => x.SpecCode).ThenBy(x => x.UnitCode).ThenBy(x => x.EffectDTimeStart)
            .ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    public async Task<object?> DeleteAsync(string specCode, string unitCode, string networkId, DateTime effectStart)
    {
        var spec = specCode.Trim().ToUpperInvariant();
        var unit = unitCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId.Trim().ToUpperInvariant();
        var row = await db.SpecPrices.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.SpecCode == spec && x.UnitCode == unit && x.NetworkID == net && x.EffectDTimeStart == effectStart);
        if (row is null) return null;
        db.SpecPrices.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, spec, unit, network = net, effectStart };
    }

    // Tra giá hiệu lực: chọn dòng Active phủ ngày, ưu tiên NetworkID khớp rồi tới "ALL".
    // Trả giá sau chiết khấu và giá đã gồm VAT.
    public async Task<object> ResolveAsync(string specCode, string? unitCode, string? networkId, string? date)
    {
        var spec = specCode.Trim().ToUpperInvariant();
        var unit = string.IsNullOrWhiteSpace(unitCode) ? null : unitCode!.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();
        var at = DateTime.TryParse(date, out var d) ? d.Date : DateTime.Now.Date;

        var candidates = await db.SpecPrices.Where(x => x.OrgId == Org && x.SpecCode == spec && x.FlagActive
            && x.EffectDTimeStart.Date <= at && (x.EffectDTimeEnd == null || x.EffectDTimeEnd.Value.Date >= at))
            .ToListAsync();
        if (unit is not null) candidates = candidates.Where(x => x.UnitCode == unit).ToList();

        var row = candidates.Where(x => x.NetworkID == net).OrderByDescending(x => x.EffectDTimeStart).FirstOrDefault()
               ?? candidates.Where(x => x.NetworkID == "ALL").OrderByDescending(x => x.EffectDTimeStart).FirstOrDefault();
        if (row is null) return new { found = false, spec, unit, network = net, date = at.ToString("yyyy-MM-dd") };

        var vat = VatPercent(row.VATRateCode);
        var afterDiscount = row.SellPrice - row.DiscountVND;
        var withVat = Math.Round(afterDiscount * (1 + vat / 100m), 2);
        return new
        {
            found = true, spec, unit = row.UnitCode, network = row.NetworkID,
            buyPrice = row.BuyPrice, sellPrice = row.SellPrice, discountVND = row.DiscountVND,
            priceAfterDiscount = afterDiscount, vatRateCode = row.VATRateCode, vatPercent = vat,
            priceWithVat = withVat, currency = row.CurrencyCode,
            effectFrom = row.EffectDTimeStart.ToString("yyyy-MM-dd"),
            effectTo = row.EffectDTimeEnd?.ToString("yyyy-MM-dd"),
            date = at.ToString("yyyy-MM-dd")
        };
    }

    private static object Project(SpecPrice x) => new
    {
        x.SpecCode, x.UnitCode, x.NetworkID, x.BuyPrice, x.SellPrice, x.DiscountVND,
        x.CurrencyCode, x.VATRateCode,
        effectFrom = x.EffectDTimeStart.ToString("yyyy-MM-dd"),
        effectTo = x.EffectDTimeEnd?.ToString("yyyy-MM-dd"),
        x.Remark, x.FlagActive
    };
}
