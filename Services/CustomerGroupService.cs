using Microsoft.EntityFrameworkCore;
using MiniPricing.Data;
using MiniPricing.Models;
namespace MiniPricing.Services;
// ===== Danh mục nhóm khách hàng (port từ Mst_CustomerGroup — 2019.4.ProductCenter) =====
// Nghiệp vụ nguồn: đây là danh mục gốc của bảng giá, dùng để áp giá theo nhóm khách hàng
// (khách hàng thuộc nhóm nào thì nhận bảng giá/chiết khấu của nhóm đó). Mỗi dòng có
// mã nhóm + OrgID (+ NetworkID), tên nhóm, mô tả, nhóm cha (dựng cây phân cấp),
// mã BU (CustomerGrpBUCode/Pattern), cấp (CustomerGrpLevel) và cờ hiệu lực.
// Quy tắc nguồn (WAS_Mst_CustomerGroup_Create/Update/Delete + Mst_CustomerGroup_CheckDB):
//   - Create: OrgID bắt buộc (nếu rỗng → Mst_CustomerGroup_Create_InvalidOrgID);
//             CustomerGrpCode chưa tồn tại (nếu đã có → Mst_CustomerGroup_CheckDB_OrganExist);
//             CustomerGrpName bắt buộc (Mst_CustomerGroup_Create_InvalidOrganName);
//             CustomerGrpName chưa tồn tại trong org (Mst_CustomerGroup_CheckCustomerGrpName).
//   - Update: CustomerGrpCode phải tồn tại (nếu không → Mst_CustomerGroup_CheckDB_OrganNotFound);
//             CustomerGrpName không được rỗng (Mst_CustomerGroup_UpdateX_InvalidOrganName);
//             nếu đổi tên thì tên mới chưa tồn tại (Mst_CustomerGroup_CheckCustomerGrpName).
//   - Delete: CustomerGrpCode phải tồn tại; không cho xoá nếu còn khách hàng thuộc nhóm
//             (Mst_CustomerGroup_Delete_Invalid_CustomerBelongCustomerGroup).
public record UpsertCustomerGroupDto(
    string CustomerGrpCode, string? NetworkID,
    string? CustomerGrpCodeParent, string? CustomerGrpName, string? CustomerGrpDesc,
    string? SolutionCode, bool? FlagActive);
public interface ICustomerGroupService
{
    Task<object> UpsertAsync(UpsertCustomerGroupDto dto);
    Task<object> ListAsync(string? customerGrpCode, string? customerGrpName);
    Task<object?> DeleteAsync(string customerGrpCode, string networkId);
    Task<object> ResolveAsync(string customerGrpCode, string? networkId);
    Task<object> TreeAsync(string? networkId);
}
public sealed class CustomerGroupService(AppDbContext db, ITenantContext tenant) : ICustomerGroupService
{
    private Guid Org => tenant.OrgId;
    private static string Net(string? networkId) =>
        string.IsNullOrWhiteSpace(networkId) ? "ALL" : networkId!.Trim().ToUpperInvariant();

    public async Task<object> UpsertAsync(UpsertCustomerGroupDto dto)
    {
        var code = dto.CustomerGrpCode.Trim().ToUpperInvariant();
        var net = Net(dto.NetworkID);
        var name = dto.CustomerGrpName?.Trim() ?? "";
        // CustomerGrpCode bắt buộc (Mst_CustomerGroup_Create_InvalidOrgID / InvalidOrganName).
        if (code.Length < 1) throw new InvalidOperationException("Mã nhóm khách hàng không hợp lệ.");
        var row = await db.CustomerGroups.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.CustomerGrpCode == code && x.NetworkID == net);
        if (row is null)
        {
            // Create: CustomerGrpCode chưa tồn tại (Mst_CustomerGroup_CheckDB_OrganExist).
            if (await db.CustomerGroups.AnyAsync(x => x.OrgId == Org && x.CustomerGrpCode == code))
                throw new InvalidOperationException($"Nhóm khách hàng '{code}' đã tồn tại.");
            // CustomerGrpName bắt buộc (Mst_CustomerGroup_Create_InvalidOrganName).
            if (name.Length < 1) throw new InvalidOperationException("Tên nhóm khách hàng không hợp lệ.");
            // CustomerGrpName chưa tồn tại trong org (Mst_CustomerGroup_CheckCustomerGrpName).
            if (await db.CustomerGroups.AnyAsync(x => x.OrgId == Org && x.CustomerGrpName == name))
                throw new InvalidOperationException($"Tên nhóm khách hàng '{name}' đã tồn tại.");
            row = new CustomerGroup { OrgId = Org, CustomerGrpCode = code, NetworkID = net };
            db.CustomerGroups.Add(row);
        }
        else
        {
            // Update: CustomerGrpName không được rỗng (Mst_CustomerGroup_UpdateX_InvalidOrganName).
            if (name.Length < 1) throw new InvalidOperationException("Tên nhóm khách hàng không hợp lệ.");
            // Nếu đổi tên thì tên mới chưa tồn tại (Mst_CustomerGroup_CheckCustomerGrpName).
            if (!string.Equals(row.CustomerGrpName, name, StringComparison.OrdinalIgnoreCase)
                && await db.CustomerGroups.AnyAsync(x => x.OrgId == Org && x.CustomerGrpName == name))
                throw new InvalidOperationException($"Tên nhóm khách hàng '{name}' đã tồn tại.");
        }
        var entity = row!;
        entity.CustomerGrpCodeParent = string.IsNullOrWhiteSpace(dto.CustomerGrpCodeParent)
            ? null : dto.CustomerGrpCodeParent!.Trim().ToUpperInvariant();
        entity.CustomerGrpName = name;
        entity.CustomerGrpDesc = dto.CustomerGrpDesc?.Trim();
        entity.SolutionCode = string.IsNullOrWhiteSpace(dto.SolutionCode) ? null : dto.SolutionCode!.Trim();
        entity.FlagActive = dto.FlagActive ?? true;
        entity.LogLUDTimeUTC = DateTime.UtcNow;
        // Dựng BU code/pattern + cấp từ nhóm cha (Mst_CustomerGroup_UpdBU).
        await RebuildBuAsync(entity);
        await db.SaveChangesAsync();
        return Project(entity);
    }

    // Dựng CustomerGrpBUCode/Pattern/Level theo chuỗi nhóm cha (gốc = "ALL", cấp 1).
    private async Task RebuildBuAsync(CustomerGroup entity)
    {
        var chain = new List<string>();
        var cur = entity.CustomerGrpCodeParent;
        var guard = 0;
        while (!string.IsNullOrWhiteSpace(cur) && guard++ < 32)
        {
            chain.Insert(0, cur!);
            var parent = await db.CustomerGroups.FirstOrDefaultAsync(x => x.OrgId == Org
                && x.CustomerGrpCode == cur && x.NetworkID == entity.NetworkID);
            cur = parent?.CustomerGrpCodeParent;
        }
        entity.CustomerGrpLevel = chain.Count + 1;
        entity.CustomerGrpBUCode = chain.Count == 0 ? "ALL" : string.Join(".", chain);
        entity.CustomerGrpBUPattern = entity.CustomerGrpBUCode + "%";
    }

    public async Task<object> ListAsync(string? customerGrpCode, string? customerGrpName)
    {
        var q = db.CustomerGroups.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(customerGrpCode))
        {
            var c = customerGrpCode!.Trim().ToUpperInvariant();
            q = q.Where(x => x.CustomerGrpCode == c);
        }
        if (!string.IsNullOrWhiteSpace(customerGrpName))
        {
            var n = customerGrpName!.Trim();
            q = q.Where(x => x.CustomerGrpName.Contains(n));
        }
        var rows = await q.OrderBy(x => x.CustomerGrpCode).ThenBy(x => x.NetworkID).ToListAsync();
        return new { count = rows.Count, items = rows.Select(Project) };
    }

    public async Task<object?> DeleteAsync(string customerGrpCode, string networkId)
    {
        var c = customerGrpCode.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var row = await db.CustomerGroups.FirstOrDefaultAsync(x => x.OrgId == Org
            && x.CustomerGrpCode == c && x.NetworkID == net);
        if (row is null) return null;   // Mst_CustomerGroup_CheckDB_OrganNotFound
        // Không cho xoá nếu còn nhóm con tham chiếu (bảo toàn cây phân cấp).
        if (await db.CustomerGroups.AnyAsync(x => x.OrgId == Org && x.CustomerGrpCodeParent == c))
            throw new InvalidOperationException($"Nhóm khách hàng '{c}' còn nhóm con, không thể xoá.");
        db.CustomerGroups.Remove(row);
        await db.SaveChangesAsync();
        return new { deleted = true, customerGrpCode = c, network = net };
    }

    // Tra nhóm khách hàng hiệu lực: ưu tiên NetworkID khớp rồi tới "ALL"; chỉ lấy dòng FlagActive.
    public async Task<object> ResolveAsync(string customerGrpCode, string? networkId)
    {
        var c = customerGrpCode.Trim().ToUpperInvariant();
        var net = Net(networkId);
        var candidates = await db.CustomerGroups.Where(x => x.OrgId == Org && x.CustomerGrpCode == c && x.FlagActive)
            .ToListAsync();
        var row = candidates.FirstOrDefault(x => x.NetworkID == net)
               ?? candidates.FirstOrDefault(x => x.NetworkID == "ALL");
        if (row is null) return new { found = false, customerGrpCode = c, network = net };
        return new
        {
            found = true, customerGrpCode = row.CustomerGrpCode, network = row.NetworkID,
            customerGrpName = row.CustomerGrpName, customerGrpCodeParent = row.CustomerGrpCodeParent,
            customerGrpBUCode = row.CustomerGrpBUCode, customerGrpLevel = row.CustomerGrpLevel,
            solutionCode = row.SolutionCode, flagActive = row.FlagActive
        };
    }

    // Dựng cây nhóm khách hàng theo CustomerGrpCodeParent (chỉ gốc khi không có cha).
    public async Task<object> TreeAsync(string? networkId)
    {
        var net = Net(networkId);
        var rows = await db.CustomerGroups.Where(x => x.OrgId == Org && x.NetworkID == net)
            .OrderBy(x => x.CustomerGrpCode).ToListAsync();
        var byParent = rows.GroupBy(x => x.CustomerGrpCodeParent ?? "").ToDictionary(g => g.Key, g => g.ToList());
        object Build(CustomerGroup node)
        {
            var children = byParent.TryGetValue(node.CustomerGrpCode, out var ch)
                ? ch.Select(Build).ToList() : new List<object>();
            return new
            {
                customerGrpCode = node.CustomerGrpCode, customerGrpName = node.CustomerGrpName,
                customerGrpLevel = node.CustomerGrpLevel, solutionCode = node.SolutionCode,
                flagActive = node.FlagActive, children
            };
        }
        var roots = byParent.TryGetValue("", out var r) ? r.Select(Build).ToList() : new List<object>();
        return new { network = net, count = rows.Count, roots };
    }

    private static object Project(CustomerGroup x) => new
    {
        customerGrpCode = x.CustomerGrpCode, x.NetworkID, x.CustomerGrpCodeParent,
        x.CustomerGrpBUCode, x.CustomerGrpBUPattern, x.CustomerGrpLevel,
        x.CustomerGrpName, x.CustomerGrpDesc, x.SolutionCode, x.FlagActive
    };
}
