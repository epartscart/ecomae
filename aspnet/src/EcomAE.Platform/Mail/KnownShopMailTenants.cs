using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Mail;

/// <summary>
/// Named live shop domains that get the same business mailbox set as ecomae.com.
/// Registry rows (when present) are merged on top. Shared ERP-only brands stay on ecomae.com.
/// </summary>
public static class KnownShopMailTenants
{
    public static readonly IReadOnlyList<(string Hostname, string SiteKey)> Rows =
        LiveTenantPresentationLock.Tenants
            .Select(t => (PlatformMailNormalize.Domain(t.Hosts[0]), t.Id))
            .Where(t => t.Item1.Length > 0)
            .ToArray();

    public static IReadOnlyList<(string Hostname, string SiteKey)> Merge(IReadOnlyList<(string Hostname, string SiteKey)>? registry)
    {
        var rows = new List<(string Hostname, string SiteKey)>(Rows);
        foreach (var row in registry ?? [])
        {
            rows.Add(row);
        }

        return rows;
    }
}
