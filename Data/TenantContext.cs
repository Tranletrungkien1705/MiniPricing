namespace MiniPricing.Data;
public interface ITenantContext { Guid OrgId { get; set; } }
public sealed class TenantContext : ITenantContext
{
    public static readonly Guid DefaultOrgId = new("88888888-8888-8888-8888-888888888888");
    public const string CookieName = "org_key";
    public Guid OrgId { get; set; } = DefaultOrgId;
}
