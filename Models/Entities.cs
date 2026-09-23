namespace MiniPricing.Models;

public sealed class Org
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>Bảng giá theo hiệu lực (MasterData pricing). Draft → Active → Expired.</summary>
public sealed class PriceList
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string Status { get; set; } = "Draft";   // Draft → Active → Expired
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>Dòng giá: 1 mã hàng + 1 hạng (tier) → đơn giá.</summary>
public sealed class PriceItem
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public long PriceListId { get; set; }
    public string ItemCode { get; set; } = "";       // SKU/model
    public string Tier { get; set; } = "Default";     // Default/Dealer/VIP
    public decimal Price { get; set; }
}

/// <summary>
/// Giá theo quy cách (port từ Mst_SpecPrice nguồn 2019.4.ProductCenter).
/// Khoá nghiệp vụ: SpecCode + UnitCode + NetworkID. Mỗi dòng có giá mua/giá bán,
/// chiết khấu (VND), thuế suất VAT và khoảng hiệu lực riêng.
/// </summary>
public sealed class SpecPrice
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string SpecCode { get; set; } = "";        // mã quy cách
    public string UnitCode { get; set; } = "";        // đơn vị tính
    public string NetworkID { get; set; } = "";       // kênh/vùng áp giá
    public decimal BuyPrice { get; set; }              // giá mua
    public decimal SellPrice { get; set; }             // giá bán
    public decimal DiscountVND { get; set; }           // chiết khấu (VND)
    public string CurrencyCode { get; set; } = "VND";
    public string VATRateCode { get; set; } = "VAT10"; // mã thuế suất
    public DateTime EffectDTimeStart { get; set; }     // hiệu lực từ
    public DateTime? EffectDTimeEnd { get; set; }      // hiệu lực đến
    public string? Remark { get; set; }
    public bool FlagActive { get; set; } = true;
    public DateTime LogLUDTimeUTC { get; set; } = DateTime.UtcNow;
    public string? LogLUBy { get; set; }
}
