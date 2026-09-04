using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;

namespace MiniPricing.Services;

public record CreateListDto(string Code, string Name, DateTime EffectiveFrom, DateTime? EffectiveTo);
public record SetPriceDto(string ItemCode, string? Tier, decimal Price);

public interface IPricingService
{
    Task<object> CreateListAsync(CreateListDto dto);
    Task<object> ListAsync();
    Task<object?> SetPriceAsync(string code, SetPriceDto dto);
    Task<object?> ActivateAsync(string code);
    Task<object?> ItemsAsync(string code);
    Task<object> ResolvePriceAsync(string item, string? tier, string? date);
}

public sealed class PricingService(AppDbContext db, ITenantContext tenant) : IPricingService
{
    private Guid Org => tenant.OrgId;

    public async Task<object> CreateListAsync(CreateListDto dto)
    {
        var code = dto.Code.Trim().ToUpperInvariant();
        if (await db.PriceLists.AnyAsync(x => x.OrgId == Org && x.Code == code))
            throw new InvalidOperationException($"Bảng giá {code} đã tồn tại.");
        var l = new PriceList { OrgId = Org, Code = code, Name = dto.Name.Trim(), EffectiveFrom = dto.EffectiveFrom, EffectiveTo = dto.EffectiveTo, Status = "Draft" };
        db.PriceLists.Add(l);
        await db.SaveChangesAsync();
        return new { l.Code, l.Name, l.EffectiveFrom, l.EffectiveTo, l.Status };
    }

    public async Task<object> ListAsync()
    {
        var items = await db.PriceLists.Where(x => x.OrgId == Org).OrderByDescending(x => x.Id).Select(x => new
        {
            x.Code, x.Name, x.EffectiveFrom, x.EffectiveTo, x.Status,
            items = db.PriceItems.Count(p => p.OrgId == Org && p.PriceListId == x.Id)
        }).ToListAsync();
        return new { count = items.Count, items };
    }

    private async Task<PriceList?> Get(string code)
    {
        code = code.Trim().ToUpperInvariant();
        return await db.PriceLists.FirstOrDefaultAsync(x => x.OrgId == Org && x.Code == code);
    }

    public async Task<object?> SetPriceAsync(string code, SetPriceDto dto)
    {
        var l = await Get(code);
        if (l is null) return null;
        if (l.Status != "Draft")
            throw new InvalidOperationException($"Bảng giá {l.Code} đang ở trạng thái {l.Status} — không thể sửa giá trực tiếp. Chỉ sửa được khi ở Draft (tạo bảng giá mới rồi Activate để thay thế).");
        var item = dto.ItemCode.Trim().ToUpperInvariant();
        var tier = string.IsNullOrWhiteSpace(dto.Tier) ? "Default" : dto.Tier!.Trim();
        var pi = await db.PriceItems.FirstOrDefaultAsync(x => x.OrgId == Org && x.PriceListId == l.Id && x.ItemCode == item && x.Tier == tier);
        if (pi is null) { pi = new PriceItem { OrgId = Org, PriceListId = l.Id, ItemCode = item, Tier = tier, Price = dto.Price }; db.PriceItems.Add(pi); }
        else pi.Price = dto.Price;
        await db.SaveChangesAsync();
        return new { list = l.Code, pi.ItemCode, pi.Tier, pi.Price };
    }

    // Kích hoạt bảng giá + tự Expire các bảng Active bị chồng hiệu lực (cùng org).
    public async Task<object?> ActivateAsync(string code)
    {
        var l = await Get(code);
        if (l is null) return null;
        l.Status = "Active";
        var others = await db.PriceLists.Where(x => x.OrgId == Org && x.Status == "Active" && x.Id != l.Id).ToListAsync();
        foreach (var o in others)
            if (o.EffectiveFrom <= (l.EffectiveTo ?? DateTime.MaxValue) && (o.EffectiveTo ?? DateTime.MaxValue) >= l.EffectiveFrom)
                o.Status = "Expired";   // chồng hiệu lực → bản mới thắng
        await db.SaveChangesAsync();
        return new { l.Code, l.Status, expiredOverlaps = others.Count(o => o.Status == "Expired") };
    }

    public async Task<object?> ItemsAsync(string code)
    {
        var l = await Get(code);
        if (l is null) return null;
        var items = await db.PriceItems.Where(x => x.OrgId == Org && x.PriceListId == l.Id).OrderBy(x => x.ItemCode)
            .Select(x => new { x.ItemCode, x.Tier, x.Price }).ToListAsync();
        return new { list = l.Code, l.Status, count = items.Count, items };
    }

    // Tra giá: bảng Active phủ ngày → giá theo tier (fallback Default nếu tier không có).
    public async Task<object> ResolvePriceAsync(string item, string? tier, string? date)
    {
        item = item.Trim().ToUpperInvariant();
        tier = string.IsNullOrWhiteSpace(tier) ? "Default" : tier!.Trim();
        var at = DateTime.TryParse(date, out var d) ? d.Date : DateTime.Now.Date;
        var lists = await db.PriceLists.Where(x => x.OrgId == Org && x.Status == "Active"
            && x.EffectiveFrom.Date <= at && (x.EffectiveTo == null || x.EffectiveTo.Value.Date >= at))
            .OrderByDescending(x => x.EffectiveFrom).ToListAsync();
        foreach (var l in lists)
        {
            var pi = await db.PriceItems.FirstOrDefaultAsync(x => x.OrgId == Org && x.PriceListId == l.Id && x.ItemCode == item && x.Tier == tier)
                  ?? await db.PriceItems.FirstOrDefaultAsync(x => x.OrgId == Org && x.PriceListId == l.Id && x.ItemCode == item && x.Tier == "Default");
            if (pi != null) return new { found = true, item, tier = pi.Tier, price = pi.Price, priceList = l.Code, date = at.ToString("yyyy-MM-dd") };
        }
        return new { found = false, item, tier, date = at.ToString("yyyy-MM-dd") };
    }
}
