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
/// Lịch sử giá theo quy cách (port từ Mst_SpecPriceHist nguồn 2019.4.ProductCenter).
/// Bảng audit: mỗi lần Create/Update/Delete một dòng SpecPrice, hệ thống ghi lại một
/// bản chụp (snapshot) toàn bộ trường giá kèm FunctionName (hàm gọi),
/// FunctionActionType (ADD/UPDATE/DELETE), HistRefType ('MST_SPECPRICE') và RefCode00..03.
/// Dùng để truy vết biến động giá bán/giá mua/chiết khấu theo thời gian.
/// </summary>
public sealed class SpecPriceHist
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string SpecCode { get; set; } = "";        // mã quy cách
    public string UnitCode { get; set; } = "";        // đơn vị tính
    public string NetworkID { get; set; } = "";       // kênh/vùng áp giá
    public decimal BuyPrice { get; set; }              // giá mua (snapshot)
    public decimal SellPrice { get; set; }             // giá bán (snapshot)
    public string CurrencyCode { get; set; } = "VND";
    public string VATRateCode { get; set; } = "VAT10";
    public decimal DiscountVND { get; set; }           // chiết khấu (VND)
    public DateTime EffectDTimeStart { get; set; }     // hiệu lực từ
    public DateTime? EffectDTimeEnd { get; set; }      // hiệu lực đến
    public string? Remark { get; set; }
    public bool FlagActive { get; set; } = true;
    public DateTime LogLUDTimeUTC { get; set; } = DateTime.UtcNow;
    public string? LogLUBy { get; set; }
    public string FunctionName { get; set; } = "";     // hàm gọi (vd WAS_Mst_SpecPrice_Create)
    public string FunctionActionType { get; set; } = ""; // ADD / UPDATE / DELETE
    public string? FunctionRemark { get; set; }
    public string HistRefType { get; set; } = "MST_SPECPRICE"; // loại tham chiếu lịch sử
    public string? RefCode00 { get; set; }
    public string? RefCode01 { get; set; }
    public string? RefCode02 { get; set; }
    public string? RefCode03 { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now; // thời điểm ghi lịch sử
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

/// <summary>
/// Quy cách × đơn vị tính (port từ Mst_SpecUnit nguồn 2019.4.ProductCenter).
/// Khoá nghiệp vụ: SpecCode + UnitCode (+ NetworkID). Mỗi dòng khai báo đơn vị tính
/// của một quy cách: hệ số quy đổi (Qty) về đơn vị chuẩn (StandardUnitCode) và
/// kích thước/khối lượng (Length/Width/Height/Volume/Weight) dùng để tính giá theo đơn vị.
/// SpecPrice tham chiếu tới đây qua cặp SpecCode + UnitCode.
/// </summary>
public sealed class SpecUnit
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string SpecCode { get; set; } = "";            // mã quy cách
    public string UnitCode { get; set; } = "";            // mã đơn vị tính
    public string NetworkID { get; set; } = "";           // kênh/vùng áp dụng
    public string StandardUnitCode { get; set; } = "";    // đơn vị chuẩn quy đổi về
    public string SpecUnitDesc { get; set; } = "";        // mô tả đơn vị tính của quy cách
    public decimal Qty { get; set; } = 1;                  // hệ số quy đổi về đơn vị chuẩn
    public decimal? Length { get; set; }                   // dài
    public decimal? Width { get; set; }                    // rộng
    public decimal? Height { get; set; }                   // cao
    public decimal? Volume { get; set; }                   // thể tích
    public decimal? Weight { get; set; }                   // khối lượng
    public string? Remark { get; set; }
    public bool FlagActive { get; set; } = true;
    public DateTime LogLUDTimeUTC { get; set; } = DateTime.UtcNow;
    public string? LogLUBy { get; set; }
}

/// <summary>
/// Tỷ giá ngoại tệ (port từ Mst_CurrencyEx nguồn 2019.4.ProductCenter).
/// Khoá nghiệp vụ: CurrencyCode (+ NetworkID). Mỗi dòng có tên tiền tệ, tiền tệ gốc,
/// tỷ giá mua (BuyRate) / tỷ giá bán (SellRate), thời điểm cập nhật và ghi chú.
/// Dùng để quy đổi giá bán/giá mua giữa các loại tiền tệ khi bảng giá ghi bằng ngoại tệ.
/// </summary>
public sealed class CurrencyEx
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string CurrencyCode { get; set; } = "";       // mã tiền tệ (vd USD)
    public string NetworkID { get; set; } = "";          // kênh/vùng áp dụng
    public string CurrencyName { get; set; } = "";       // tên tiền tệ
    public string BaseCurrencyCode { get; set; } = "";   // tiền tệ gốc quy đổi (vd VND)
    public decimal BuyRate { get; set; }                   // tỷ giá mua
    public decimal SellRate { get; set; }                  // tỷ giá bán
    public DateTime UpdatedTime { get; set; } = DateTime.Now; // thời điểm cập nhật tỷ giá
    public string InterEx { get; set; } = "";            // cờ/nguồn tỷ giá liên ngân hàng
    public string? Remark { get; set; }
    public bool FlagActive { get; set; } = true;
    public DateTime LogLUDTimeUTC { get; set; } = DateTime.UtcNow;
    public string? LogLUBy { get; set; }
}

/// <summary>
/// Danh mục đơn vị tính (port từ Mst_Unit nguồn 2019.4.ProductCenter).
/// Khoá nghiệp vụ: UnitCode (mã hệ thống ngầm) + OrgID (+ NetworkID). Mỗi dòng có
/// UnitCodeUser (mã người dùng nhập), UnitName (tên đơn vị tính), ghi chú và cờ hiệu lực.
/// SpecUnit/SpecPrice tham chiếu tới đây qua UnitCode để quy đổi và tính giá theo đơn vị.
/// </summary>
public sealed class Unit
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string UnitCode { get; set; } = "";       // mã đơn vị tính (UnitCodeSys ngầm)
    public string NetworkID { get; set; } = "";      // kênh/vùng áp dụng
    public string UnitCodeUser { get; set; } = "";   // mã đơn vị tính người dùng nhập
    public string UnitName { get; set; } = "";       // tên đơn vị tính
    public string? Remark { get; set; }               // ghi chú, mô tả
    public string CodeGuid { get; set; } = "";       // định danh guid
    public bool FlagActive { get; set; } = true;
    public bool DTimeUsed { get; set; }                // cờ đã tham gia nghiệp vụ
    public DateTime LogLUDTimeUTC { get; set; } = DateTime.UtcNow;
    public string? LogLUBy { get; set; }
}

/// <summary>
/// Quy cách / sản phẩm (port từ Mst_Spec nguồn 2019.4.ProductCenter).
/// Khoá nghiệp vụ: SpecCode + OrgID (+ NetworkID). Đây là danh mục gốc của bảng giá:
/// SpecPrice/SpecUnit tham chiếu tới đây qua SpecCode. Mỗi dòng khai báo tên quy cách,
/// model (ModelCode), phân loại (SpecType1/SpecType2), màu, đơn vị mặc định/chuẩn,
/// cờ quản lý serial/LOT và các trường mở rộng (CustomField1..10).
/// </summary>
public sealed class Spec
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string SpecCode { get; set; } = "";            // mã quy cách
    public string NetworkID { get; set; } = "";           // kênh/vùng áp dụng
    public string SpecName { get; set; } = "";            // tên quy cách
    public string? SpecDesc { get; set; }                  // mô tả
    public string? ModelCode { get; set; }                 // mã model (Mst_Model)
    public string? SpecType1 { get; set; }                 // phân loại 1 (Mst_SpecType1)
    public string? SpecType2 { get; set; }                 // phân loại 2 (Mst_SpecType2)
    public string? Color { get; set; }                     // màu
    public bool FlagHasSerial { get; set; }                // có quản lý serial
    public bool FlagHasLOT { get; set; }                   // có quản lý LOT
    public string DefaultUnitCode { get; set; } = "";     // đơn vị tính mặc định
    public string StandardUnitCode { get; set; } = "";    // đơn vị tính chuẩn
    public string? NetworkSpecCode { get; set; }           // mã quy cách ở kênh cha
    public string? Remark { get; set; }
    public bool FlagActive { get; set; } = true;
    public bool FlagExist { get; set; }                    // cờ đã phát sinh nghiệp vụ
    public string? CustomField1 { get; set; }
    public string? CustomField2 { get; set; }
    public string? CustomField3 { get; set; }
    public string? CustomField4 { get; set; }
    public string? CustomField5 { get; set; }
    public string? CustomField6 { get; set; }
    public string? CustomField7 { get; set; }
    public string? CustomField8 { get; set; }
    public string? CustomField9 { get; set; }
    public string? CustomField10 { get; set; }
    public DateTime LogLUDTimeUTC { get; set; } = DateTime.UtcNow;
    public string? LogLUBy { get; set; }
}

/// <summary>
/// Danh mục model / dòng sản phẩm (port từ Mst_Model nguồn 2019.4.ProductCenter).
/// Khoá nghiệp vụ: ModelCode + OrgID (+ NetworkID). Đây là danh mục gốc của bảng giá:
/// Spec tham chiếu tới đây qua ModelCode. Mỗi dòng khai báo tên model (ModelName),
/// mã model nội bộ (OrgModelCode), hãng (BrandCode), mã model ở kênh cha
/// (NetworkModelCode) và cờ hiệu lực. Khi tạo: ModelCode + BrandCode bắt buộc,
/// BrandCode phải tồn tại & active (Mst_Brand_CheckDB), ModelName không rỗng.
/// </summary>
public sealed class Model
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string ModelCode { get; set; } = "";            // mã model
    public string NetworkID { get; set; } = "";            // kênh/vùng áp dụng
    public string ModelName { get; set; } = "";            // tên model
    public string OrgModelCode { get; set; } = "";         // mã model nội bộ của org
    public string BrandCode { get; set; } = "";            // mã hãng (Mst_Brand)
    public string? NetworkModelCode { get; set; }           // mã model ở kênh cha
    public string? Remark { get; set; }
    public bool FlagActive { get; set; } = true;
    public DateTime LogLUDTimeUTC { get; set; } = DateTime.UtcNow;
    public string? LogLUBy { get; set; }
}

/// <summary>
/// Danh mục hãng / thương hiệu (port từ Mst_Brand nguồn 2019.4.ProductCenter).
/// Khoá nghiệp vụ: BrandCode + OrgID (+ NetworkID). Đây là danh mục gốc của bảng giá:
/// Model tham chiếu tới đây qua BrandCode. Mỗi dòng khai báo tên hãng (BrandName),
/// mã hãng ở kênh cha (NetworkBrandCode) và cờ hiệu lực. Khi tạo: BrandCode bắt buộc,
/// BrandName bắt buộc & không trùng trong org (Mst_Brand_CheckBrandName),
/// NetworkBrandCode (nếu khác rỗng) phải tồn tại ở kênh cha
/// (Mst_Brand_CheckDB_NetworkBrandCodeOfOrgParent).
/// </summary>
public sealed class Brand
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string BrandCode { get; set; } = "";            // mã hãng
    public string NetworkID { get; set; } = "";            // kênh/vùng áp dụng
    public string BrandName { get; set; } = "";            // tên hãng
    public string? NetworkBrandCode { get; set; }           // mã hãng ở kênh cha
    public string? Remark { get; set; }
    public bool FlagActive { get; set; } = true;
    public DateTime LogLUDTimeUTC { get; set; } = DateTime.UtcNow;
    public string? LogLUBy { get; set; }
}

/// <summary>
/// Quy đổi tiền tệ theo hiệu lực (port từ Mst_CurrencyConvert nguồn 2019.4.ProductCenter).
/// Khoá nghiệp vụ: CurrencyCode (tiền tệ nguồn) + CurrencyCodeV (tiền tệ đích) + NetworkID.
/// Mỗi dòng khai báo tỷ giá mua/bán (BuyRate/SellRate) và các giá trị quy đổi
/// (ValConvert, ValConvertP, ValConvertToVND) kèm khoảng hiệu lực riêng
/// (EffectDTimeStartV/EffectDTimeEndV). Dùng để quy đổi giá bán/giá mua giữa hai loại
/// tiền tệ theo thời gian, khác với Mst_CurrencyEx (chỉ lưu tỷ giá theo mã tiền tệ).
/// </summary>
public sealed class CurrencyConvert
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string CurrencyCode { get; set; } = "";       // tiền tệ nguồn (vd USD)
    public string CurrencyCodeV { get; set; } = "";      // tiền tệ đích quy đổi (vd VND)
    public string CurrencyNameV { get; set; } = "";      // tên tiền tệ đích
    public string BaseCurrencyCode { get; set; } = "";   // tiền tệ gốc
    public string NetworkID { get; set; } = "";          // kênh/vùng áp dụng
    public decimal BuyRate { get; set; }                   // tỷ giá mua
    public decimal SellRate { get; set; }                  // tỷ giá bán
    public decimal ValConvert { get; set; }                // giá trị quy đổi
    public decimal ValConvertP { get; set; }               // giá trị quy đổi (P)
    public decimal ValConvertToVND { get; set; }           // giá trị quy đổi sang VND
    public DateTime EffectDTimeStartV { get; set; }        // hiệu lực từ
    public DateTime? EffectDTimeEndV { get; set; }         // hiệu lực đến
    public string? Remark { get; set; }
    public bool FlagActive { get; set; } = true;
    public DateTime LogLUDTimeUTC { get; set; } = DateTime.UtcNow;
    public string? LogLUBy { get; set; }
}

/// <summary>
/// Mã giảm giá / chiết khấu (port từ Inos_DiscountCode nguồn 2019.4.ProductCenter).
/// Khoá nghiệp vụ: Code (+ OrgID). Mỗi mã có loại giảm giá (DiscountType: Percent=1 /
/// Absolute=2), giá trị giảm (DiscountAmount), số lượng còn lại (RemainQty), mô tả,
/// cờ kích hoạt (Enabled) và khoảng hiệu lực (EffectDateFrom/EffectDateTo).
/// Khi áp vào đơn: mã phải tồn tại (DiscountCodeNotFound) và phải Enabled
/// (InvalidDiscountStatus) — theo Mst_NNT_Calc_InvalidInosCreateOrder_*.
/// </summary>
public sealed class DiscountCode
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";             // mã giảm giá
    public int RemainQty { get; set; }                  // số lượng còn lại
    public string DiscountType { get; set; } = "Percent"; // Percent (1) / Absolute (2)
    public decimal DiscountAmount { get; set; }         // giá trị giảm (% hoặc số tiền)
    public string Description { get; set; } = "";       // mô tả
    public bool Enabled { get; set; } = true;           // cờ kích hoạt
    public DateTime EffectDateFrom { get; set; }        // hiệu lực từ
    public DateTime? EffectDateTo { get; set; }         // hiệu lực đến
    public DateTime LogLUDTimeUTC { get; set; } = DateTime.UtcNow;
    public string? LogLUBy { get; set; }
}
/// <summary>
/// Danh má»¥c phÃ¢n loáº¡i quy cÃ¡ch cáº¥p 1 (port tá»« Mst_SpecType1 nguá»“n 2019.4.ProductCenter).
/// KhoÃ¡ nghiá»‡p vá»¥: SpecType1 + OrgID (+ NetworkID). ÄÃ¢y lÃ  danh má»¥c gá»‘c cá»§a báº£ng giÃ¡:
/// Spec tham chiáº¿u tá»›i Ä‘Ã¢y qua SpecType1. Má»—i dÃ²ng khai bÃ¡o tÃªn phÃ¢n loáº¡i (SpecType1Name),
/// ghi chÃº vÃ  cá» hiá»‡u lá»±c. Khi táº¡o: SpecType1 báº¯t buá»™c & chÆ°a tá»“n táº¡i
/// (Mst_SpecType1_Create_InvalidSpecType1 / Mst_SpecType1_CheckDB_SpecType1Exist),
/// SpecType1Name báº¯t buá»™c (Mst_SpecType1_Create_InvalidSpecType1Name).
/// </summary>
public sealed class SpecType1
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string SpecType1Code { get; set; } = "";   // mÃ£ phÃ¢n loáº¡i 1
    public string NetworkID { get; set; } = "";       // kÃªnh/vÃ¹ng Ã¡p dá»¥ng
    public string SpecType1Name { get; set; } = "";   // tÃªn phÃ¢n loáº¡i 1
    public string? Remark { get; set; }
    public bool FlagActive { get; set; } = true;
    public DateTime LogLUDTimeUTC { get; set; } = DateTime.UtcNow;
    public string? LogLUBy { get; set; }
}
/// <summary>
/// Danh má»¥c phÃ¢n loáº¡i quy cÃ¡ch cáº¥p 2 (port tá»« Mst_SpecType2 nguá»“n 2019.4.ProductCenter).
/// KhoÃ¡ nghiá»‡p vá»¥: SpecType2 + OrgID (+ NetworkID). ÄÃ¢y lÃ  danh má»¥c gá»‘c cá»§a báº£ng giÃ¡:
/// Spec tham chiáº¿u tá»›i Ä‘Ã¢y qua SpecType2. Má»—i dÃ²ng khai bÃ¡o tÃªn phÃ¢n loáº¡i (SpecType2Name),
/// ghi chÃº vÃ  cá» hiá»‡u lá»±c. Khi táº¡o: SpecType2 báº¯t buá»™c & chÆ°a tá»“n táº¡i
/// (Mst_SpecType2_Create_InvalidSpecType2 / Mst_SpecType2_CheckDB_SpecType2Exist),
/// SpecType2Name báº¯t buá»™c (Mst_SpecType2_Create_InvalidSpecType2Name).
/// </summary>
public sealed class SpecType2
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string SpecType2Code { get; set; } = "";   // mÃ£ phÃ¢n loáº¡i 2
    public string NetworkID { get; set; } = "";       // kÃªnh/vÃ¹ng Ã¡p dá»¥ng
    public string SpecType2Name { get; set; } = "";   // tÃªn phÃ¢n loáº¡i 2
    public string? Remark { get; set; }
    public bool FlagActive { get; set; } = true;
    public DateTime LogLUDTimeUTC { get; set; } = DateTime.UtcNow;
    public string? LogLUBy { get; set; }
}
