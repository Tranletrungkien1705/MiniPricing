using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;

namespace MiniPricing.Services;

// ===== GiÃ¡ xe theo CarSubSpec (port tá»« Mst_CarSubSpecPrice â€” 2019.4.ProductCenter) =====
// Nghiá»‡p vá»¥ nguá»“n: má»—i xe (CarCode) Ã— quy cÃ¡ch con (SubSpecCode) Ã— kÃªnh (NetworkID)
// cÃ³ GTÄG (giÃ¡ thá»‹ trÆ°á»ng Ä‘á» xuáº¥t) vÃ  GTBÄTD (giÃ¡ bÃ¡n Ä‘á» xuáº¥t tá»‘i Ä‘a) lÃ m giÃ¡ tham chiáº¿u,
// kÃ¨m giÃ¡ bÃ¡n thá»±c táº¿ vÃ  khoáº£ng hiá»‡u lá»±c riÃªng.
// Nguá»“n yÃªu cáº§u GTÄG/GTBÄTD báº¯t buá»™c (ErrProductCenter.Mst_CarSubSpecPrice_Save_InputTblNotFound).
// Tra giÃ¡ hiá»‡u lá»±c â†’ tráº£ giÃ¡ bÃ¡n + chÃªnh lá»‡ch so vá»›i GTÄG/GTBÄTD.

public record UpsertCarSubSpecPriceDto(
    string CarCode, string SubSpecCode, string? NetworkID,
    decimal GTDG, decimal GTBDTD, decimal SellPrice,
    string? CurrencyCode, DateTime EffectDTimeStart, DateTime? EffectDTimeEnd, string? Remark);

public interface ICarSubSpecPriceService
{
    Task<object> UpsertAsync(UpsertCarSubSpecPriceDto dto);
    Task<object> ListAsync(string? carCode);
    Task<object?> DeleteAsync(string carCode, string subSpecCode, string networkId, DateTime effectStart);
    Task<object> ResolveAsync(string carCode, string? subSpecCode, string? networkId, string? date);
}

public sealed class CarSubSpecPriceService(AppDbContext db, ITenantContext tenant) : ICarSubSpecPriceService
{
    private Guid Org => tenant.OrgId;

    public async Task<object> UpsertAsync(UpsertCarSubSpecPriceDto dto)
    {
        var car = dto.CarCode.Trim().ToUpperInvariant();
        var sub = dto.SubSpecCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(dto.NetworkID) ? "ALL" : dto.NetworkID!.Trim().ToUpperInvariant();
        // Nguá»“n: GTÄG/GTBÄTD báº¯t buá»™c â€” thiáº¿u thÃ¬ bÃ¡o "KhÃ´ng tÃ¬m tháº¥y GTÄG, GTBÄTÄ trong há»‡ thá»‘ng".
        if (dto.GTDG <= 0 || dto.GTBDTD <= 0)
            throw new InvalidOperationException("KhÃ´ng tÃ¬m tháº¥y GTÄG, GTBÄTÄ trong há»‡ thá»‘ng.");
        if (dto.SellPrice < 0) throw new InvalidOperationException("GiÃ¡ bÃ¡n khÃ´ng Ä‘Æ°á»£c Ã¢m.");
        if (dto.EffectDTimeEnd is { } end && end < dto.EffectDTimeStart)
            throw new InvalidOperationException("NgÃ y káº¿t thÃºc hiá»‡u lá»±c pháº£i sau ngÃ y báº¯t Ä‘áº§u.");

        var row = await db.CarSubSpecPrices.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.CarCode == car && x.SubSpecCode == sub && x.NetworkID == net
            && x.EffectDTimeStart == dto.EffectDTimeStart);
        if (row is null)
        {
            row = new CarSubSpecPrice { OrgId = Org, CarCode = car, SubSpecCode = sub, NetworkID = net, EffectDTimeStart = dto.EffectDTimeStart };
            db.CarSubSpecPrices.Add(row);
        }
        row.GTDG = dto.GTDG;
        row.GTBDTD = dto.GTBDTD;
        row.SellPrice = dto.SellPrice;
        row.CurrencyCode = string.IsNullOrWhiteSpace(dto.CurrencyCode) ? "VND" : dto.CurrencyCode!.Trim().ToUpperInvariant();
        row.EffectDTimeEnd = dto.EffectDTimeEnd;
        row.Remark = dto.Remark;
        row.FlagActive = true;
        row.LogLUDTimeUTC = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Project(row);
    }

    public async Task<object> ListAsync(string? carCode)
    {
        var q = db.CarSubSpecPrices.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(carCode))
        {
            var c = carCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.CarCode == c);
        }
        var rows = await q.OrderBy(x => x.CarCode).ThenBy(x => x.SubSpecCode).ThenBy(x => x.EffectDTimeStart)
            .ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    public async Task<object?> DeleteAsync(string carCode, string subSpecCode, string networkId, DateTime effectStart)
    {
        var car = carCode.Trim().ToUpperInvariant();
        var sub = subSpecCode.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId.Trim().ToUpperInvariant();
        var row = await db.CarSubSpecPrices.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.CarCode == car && x.SubSpecCode == sub && x.NetworkID == net && x.EffectDTimeStart == effectStart);
        if (row is null) return null;
        db.CarSubSpecPrices.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, car, subSpec = sub, network = net, effectStart };
    }

    // Tra giÃ¡ hiá»‡u lá»±c: chá»n dÃ²ng Active phá»§ ngÃ y, Æ°u tiÃªn NetworkID khá»›p rá»“i tá»›i "ALL".
    // Tráº£ giÃ¡ bÃ¡n + chÃªnh lá»‡ch so vá»›i GTÄG/GTBÄTD (giÃ¡ tham chiáº¿u).
    public async Task<object> ResolveAsync(string carCode, string? subSpecCode, string? networkId, string? date)
    {
        var car = carCode.Trim().ToUpperInvariant();
        var sub = string.IsNullOrWhiteSpace(subSpecCode) ? null : subSpecCode!.Trim().ToUpperInvariant();
        var net = string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();
        var at = DateTime.TryParse(date, out var d) ? d.Date : DateTime.Now.Date;

        var candidates = await db.CarSubSpecPrices.Where(x => x.OrgId == Org && x.CarCode == car && x.FlagActive
            && x.EffectDTimeStart.Date <= at && (x.EffectDTimeEnd == null || x.EffectDTimeEnd.Value.Date >= at))
            .ToListAsync();
        if (sub is not null) candidates = candidates.Where(x => x.SubSpecCode == sub).ToList();

        var row = candidates.Where(x => x.NetworkID == net).OrderByDescending(x => x.EffectDTimeStart).FirstOrDefault()
               ?? candidates.Where(x => x.NetworkID == "ALL").OrderByDescending(x => x.EffectDTimeStart).FirstOrDefault();
        if (row is null) return new { found = false, car, subSpec = sub, network = net, date = at.ToString("yyyy-MM-dd") };

        return new
        {
            found = true, car, subSpec = row.SubSpecCode, network = row.NetworkID,
            gtdg = row.GTDG, gtbdtd = row.GTBDTD, sellPrice = row.SellPrice,
            diffVsGTDG = row.SellPrice - row.GTDG, diffVsGTBDTD = row.SellPrice - row.GTBDTD,
            currency = row.CurrencyCode,
            effectFrom = row.EffectDTimeStart.ToString("yyyy-MM-dd"),
            effectTo = row.EffectDTimeEnd?.ToString("yyyy-MM-dd"),
            date = at.ToString("yyyy-MM-dd")
        };
    }

    private static object Project(CarSubSpecPrice x) => new
    {
        x.CarCode, x.SubSpecCode, x.NetworkID, x.GTDG, x.GTBDTD, x.SellPrice,
        x.CurrencyCode,
        effectFrom = x.EffectDTimeStart.ToString("yyyy-MM-dd"),
        effectTo = x.EffectDTimeEnd?.ToString("yyyy-MM-dd"),
        x.Remark, x.FlagActive
    };
}
