using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;
namespace MiniPricing.Services;
// ===== Danh mục loại tiền tệ (port từ Mst_Currency — 2019.4.ProductCenter) =====
// Nghiệp vụ nguồn: đây là danh mục gốc của bảng giá — khai báo các loại tiền tệ dùng để
// ghi giá bán/giá mua (SpecPrice.CurrencyCode, CurrencyEx.CurrencyCode, CurrencyConvert.CurrencyCode).
// Quy tắc nguồn (Mst_Currency_CheckDB + error codes):
//   - Create: CurrencyCode bắt buộc & chưa tồn tại trong org
//             (Mst_Currency_CheckDB_CurrencyExist);
//             CurrencyName bắt buộc.
//   - Update: CurrencyCode phải tồn tại
//             (Mst_Currency_CheckDB_CurrencyNotFound);
//             nếu đổi tên thì tên không rỗng.
//   - Delete: CurrencyCode phải tồn tại.
public record UpsertCurrencyDto(
    string CurrencyCode, string? CurrencyName, bool? FlagActive);
public interface ICurrencyService
{
    Task<object> UpsertAsync(UpsertCurrencyDto dto);
    Task<object> ListAsync(string? code, string? name);
    Task<object?> DeleteAsync(string code);
    Task<object> ResolveAsync(string code);
}
public sealed class CurrencyService(AppDbContext db, ITenantContext tenant) : ICurrencyService
{
    private Guid Org => tenant.OrgId;

    public async Task<object> UpsertAsync(UpsertCurrencyDto dto)
    {
        var code = dto.CurrencyCode.Trim().ToUpperInvariant();
        var name = dto.CurrencyName?.Trim() ?? "";
        // CurrencyCode bắt buộc.
        if (code.Length < 1) throw new InvalidOperationException("Mã loại tiền tệ không hợp lệ.");
        var row = await db.Currencies.FirstOrDefaultAsync(x => x.OrgId == Org && x.CurrencyCode == code);
        if (row is null)
        {
            // Create: CurrencyCode chưa tồn tại trong org (Mst_Currency_CheckDB_CurrencyExist).
            if (await db.Currencies.AnyAsync(x => x.OrgId == Org && x.CurrencyCode == code))
                throw new InvalidOperationException($"Loại tiền tệ '{code}' đã tồn tại trong hệ thống.");
            // CurrencyName bắt buộc.
            if (name.Length < 1) throw new InvalidOperationException("Tên loại tiền tệ không hợp lệ.");
            row = new Currency { OrgId = Org, CurrencyCode = code };
            db.Currencies.Add(row);
        }
        else
        {
            // Update: nếu đổi tên thì tên không rỗng.
            if (name.Length < 1) throw new InvalidOperationException("Tên loại tiền tệ không hợp lệ.");
        }
        var entity = row!;
        entity.CurrencyName = name;
        entity.FlagActive = dto.FlagActive ?? true;
        entity.LogLUDTimeUTC = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Project(entity);
    }

    public async Task<object> ListAsync(string? code, string? name)
    {
        var q = db.Currencies.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(code))
        {
            var c = code!.Trim().ToUpperInvariant();
            q = q.Where(x => x.CurrencyCode == c);
        }
        if (!string.IsNullOrWhiteSpace(name))
        {
            var n = name!.Trim();
            q = q.Where(x => x.CurrencyName.Contains(n));
        }
        var rows = await q.OrderBy(x => x.CurrencyCode).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    public async Task<object?> DeleteAsync(string code)
    {
        var c = code.Trim().ToUpperInvariant();
        var row = await db.Currencies.FirstOrDefaultAsync(x => x.OrgId == Org && x.CurrencyCode == c);
        if (row is null) return null;   // Mst_Currency_CheckDB_CurrencyNotFound
        db.Currencies.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, currencyCode = c };
    }

    // Tra loại tiền tệ hiệu lực theo mã; chỉ trả khi FlagActive.
    public async Task<object> ResolveAsync(string code)
    {
        var c = code.Trim().ToUpperInvariant();
        var row = await db.Currencies.FirstOrDefaultAsync(x => x.OrgId == Org && x.CurrencyCode == c);
        if (row is null) return new { found = false, currencyCode = c };
        return new
        {
            found = row.FlagActive, currencyCode = row.CurrencyCode,
            currencyName = row.CurrencyName, flagActive = row.FlagActive
        };
    }

    private static object Project(Currency x) => new
    {
        x.CurrencyCode, x.CurrencyName, x.FlagActive
    };
}
