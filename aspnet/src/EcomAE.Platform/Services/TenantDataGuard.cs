using System.Text.RegularExpressions;

namespace EcomAE.Platform.Services;

/// <summary>
/// PHP <c>content/general_pages/epc_tenant_data_guard.php</c>: containment for a client tenant that is
/// still mapped to the shared spare-parts database (<c>docpart</c>). While contained, tenant surfaces
/// must return zero-safe data instead of leaking another tenant's rows.
/// </summary>
public static partial class TenantDataGuard
{
    private const string SharedDatabase = "docpart";

    /// <summary>True for a client tenant hostname — PHP <c>epc_portal_is_client_hostname()</c>.</summary>
    public static bool IsClientHost(string? host)
    {
        var h = PlatformHostPolicy.NormalizeHost(host);
        if (h.Length == 0 || PlatformHostPolicy.IsSuperCpHost(h))
        {
            return false;
        }

        return !IndustrySubdomain().IsMatch(h);
    }

    /// <summary>
    /// True when the current tenant must be contained: a client host, not ePartsCart itself,
    /// resolved onto the shared <c>docpart</c> database.
    /// </summary>
    public static bool IsContained(TenantContext? tenant)
    {
        if (tenant is null)
        {
            return false;
        }

        if (!IsClientHost(tenant.Host))
        {
            return false;
        }

        if (RouteTenantResolver.IsEpartsCartHost(tenant.Host, tenant.SiteKey))
        {
            return false;
        }

        return string.Equals(tenant.DatabaseName?.Trim(), SharedDatabase, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]*\\.ecomae\\.com$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IndustrySubdomain();
}
