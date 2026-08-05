namespace EcomAE.Platform.Services;

/// <summary>
/// Super-CP / platform operator hosts — mirrors PHP <c>epc_portal_platform_hostnames()</c>.
/// Product BOS is confidential and must never answer on named live tenants (e.g. epartscart.com).
/// </summary>
public static class PlatformHostPolicy
{
    /// <summary>Exact hosts allowed to serve product <c>/bos</c> (Super CP / platform only).</summary>
    public static readonly IReadOnlyList<string> SuperCpHosts =
    [
        "www.ecomae.com",
        "ecomae.com",
        "cp.ecomae.com",
    ];

    public static string NormalizeHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return string.Empty;
        }

        var h = host.Trim().TrimEnd('.').ToLowerInvariant();
        var colon = h.IndexOf(':');
        if (colon > 0)
        {
            h = h[..colon];
        }

        return h;
    }

    /// <summary>True when host may run Super BOS / Super CP platform ops.</summary>
    public static bool IsSuperCpHost(string? host)
    {
        var h = NormalizeHost(host);
        if (h.Length == 0)
        {
            return false;
        }

        return SuperCpHosts.Any(p => string.Equals(p, h, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>True when the request path is product BOS (not marketing /bos knowledge pages).</summary>
    public static bool IsProductBosPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var p = path.Replace('\\', '/');
        if (p.StartsWith("/marketing/", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("/php-reference/marketing", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Product BOS app + login + digests
        if (p.Equals("/bos", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("/bos/", StringComparison.OrdinalIgnoreCase)
            || p.Equals("/BOS", StringComparison.Ordinal)
            || p.StartsWith("/BOS/", StringComparison.Ordinal))
        {
            return true;
        }

        // Tenant php-reference must not expose Super BOS either
        if (p.Equals("/php-reference/bos", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("/php-reference/bos/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
