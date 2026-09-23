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

/// <summary>
/// Giá xe theo CarSubSpec (port từ Mst_CarSubSpecPrice nguồn 2019.4.ProductCenter).
/// Khoá nghiệp vụ: CarCode (mã xe) + SubSpecCode (quy cách con) + NetworkID.
/// Mỗi dòng có GTĐG (giá thị trường đề xuất) và GTBĐTD (giá bán đề xuất tối đa)
/// làm giá tham chiếu, kèm giá bán thực tế và khoảng hiệu lực riêng.
/// </summary>
public sealed class CarSubSpecPrice
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string CarCode { get; set; } = "";        // mã xe
    public string SubSpecCode { get; set; } = "";    // mã quy cách con (CarSubSpec)
    public string NetworkID { get; set; } = "";      // kênh/vùng áp giá
    public decimal GTDG { get; set; }                  // giá thị trường đề xuất
    public decimal GTBDTD { get; set; }                // giá bán đề xuất tối đa
    public decimal SellPrice { get; set; }             // giá bán thực tế
    public string CurrencyCode { get; set; } = "VND";
    public DateTime EffectDTimeStart { get; set; }     // hiệu lực từ
    public DateTime? EffectDTimeEnd { get; set; }      // hiệu lực đến
    public string? Remark { get; set; }
    public bool FlagActive { get; set; } = true;
    public DateTime LogLUDTimeUTC { get; set; } = DateTime.UtcNow;
    public string? LogLUBy { get; set; }
}

/// <summary>
/// Danh mục thuế suất VAT (port từ Mst_VATRate nguồn 2019.4.ProductCenter).
/// Khoá nghiệp vụ: VATRateCode (+ NetworkID). Mỗi dòng có % thuế suất, mô tả và cờ hiệu lực.
/// SpecPrice tham chiếu tới đây qua VATRateCode để tính giá đã gồm VAT.
/// </summary>
public sealed class VatRate
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string VATRateCode { get; set; } = "";   // mã thuế suất (vd VAT10)
    public string NetworkID { get; set; } = "";     // kênh/vùng áp dụng
    public decimal VATRate { get; set; }             // % thuế suất
    public string VATDesc { get; set; } = "";       // mô tả
    public bool FlagActive { get; set; } = true;
    public DateTime LogLUDTimeUTC { get; set; } = DateTime.UtcNow;
    public string? LogLUBy { get; set; }
}
