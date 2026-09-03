namespace EcomAE.Platform.Services;

/// <summary>
/// Super-CP / platform operator hosts — mirrors PHP <c>epc_portal_platform_hostnames()</c>.
/// Product BOS is confidential and must never answer on named live tenants (e.g. epartscart.com).
/// Marketing knowledge articles under <c>/bos/{slug}</c> are not product BOS (PHP router keeps them public).
/// </summary>
public static class PlatformHostPolicy
{
    /// <summary>Exact hosts allowed to serve product <c>/bos</c> and <c>/ip</c> (Super CP / platform only).</summary>
    public static readonly IReadOnlyList<string> SuperCpHosts =
    [
        "www.ecomae.com",
        "ecomae.com",
        "cp.ecomae.com",
    ];

    /// <summary>Customer LifeOS product hosts (public marketing + app shell).</summary>
    public static readonly IReadOnlyList<string> LifeOsHosts =
    [
        "lifeos.ecomae.com",
        "www.lifeos.ecomae.com",
    ];

    /// <summary>
    /// First path segment under <c>/bos/</c> that is product (apps, digests, ajax) — not marketing articles.
    /// Mirrors PHP: bare <c>/bos</c> → product app; <c>/bos/{article-slug}</c> → marketing content.
    /// </summary>
    private static readonly HashSet<string> ProductBosFirstSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "app",
        "login",
        "logout",
        "ajax-writes",
        "tenants",
        "tenants-app",
        "fleet-summary",
        "fleet-summary-app",
        "fleet-health",
        "fleet-health-app",
        "fleet-readiness",
        "fleet-readiness-app",
        "audit-log",
        "audit-log-app",
    };

    /// <summary>First path segment under <c>/ip/</c> that is product Intelligence Platform.</summary>
    private static readonly HashSet<string> ProductIpFirstSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "app",
        "login",
        "logout",
        "ecosystem",
        "ecosystem-app",
    };

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

    /// <summary>
    /// Hostname lookup candidates: exact normalized host first, then www-stripped or www-added alias.
    /// Lets <c>www.epartscart.com</c> resolve a registry row stored as <c>epartscart.com</c> (and vice versa).
    /// </summary>
    public static IReadOnlyList<string> NormalizeHostAliases(string? host)
    {
        var primary = NormalizeHost(host);
        if (primary.Length == 0)
        {
            return Array.Empty<string>();
        }

        string alias;
        if (primary.StartsWith("www.", StringComparison.Ordinal))
        {
            alias = primary[4..];
        }
        else
        {
            alias = "www." + primary;
        }

        if (alias.Length == 0 || string.Equals(alias, primary, StringComparison.Ordinal))
        {
            return [primary];
        }

        return [primary, alias];
    }

    /// <summary>True when host may run Super BOS / Super CP / Intelligence Platform ops.</summary>
    public static bool IsSuperCpHost(string? host)
    {
        var h = NormalizeHost(host);
        if (h.Length == 0)
        {
            return false;
        }

        return SuperCpHosts.Any(p => string.Equals(p, h, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>True when host is the LifeOS customer product domain.</summary>
    public static bool IsLifeOsHost(string? host)
    {
        var h = NormalizeHost(host);
        if (h.Length == 0)
        {
            return false;
        }

        return LifeOsHosts.Any(p => string.Equals(p, h, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Super-only CP apps (tenants, demo tenants, tax toolkits, free-tools hub,
    /// governance, failover, …) — same gate as <see cref="IsSuperCpHost"/>.
    /// </summary>
    public static bool AllowSuperOnlyApp(string? host) => IsSuperCpHost(host);

    /// <summary>
    /// True when the request path is product BOS (not marketing <c>/bos/{article}</c> knowledge pages).
    /// </summary>
    public static bool IsProductBosPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var p = path.Replace('\\', '/');
        var q = p.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
        {
            p = p[..q];
        }

        if (Presentation.StorefrontLangPrefix.TryStripAdminShell(p, out var strippedBos))
        {
            p = strippedBos;
        }

        if (p.StartsWith("/marketing/", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("/php-reference/marketing", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Tenant php-reference must not expose Super BOS either
        if (p.Equals("/php-reference/bos", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("/php-reference/bos/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Uppercase PHP shell is always product.
        if (p.Equals("/BOS", StringComparison.Ordinal)
            || p.StartsWith("/BOS/", StringComparison.Ordinal))
        {
            return true;
        }

        if (p.Equals("/bos", StringComparison.OrdinalIgnoreCase)
            || p.Equals("/bos/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!p.StartsWith("/bos/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = p["/bos/".Length..].Trim('/');
        if (rest.Length == 0)
        {
            return true;
        }

        var slash = rest.IndexOf('/');
        var first = slash < 0 ? rest : rest[..slash];

        // Explicit product apps/digests/ajax, or any *-app Blazor surface.
        if (ProductBosFirstSegments.Contains(first)
            || first.EndsWith("-app", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Remaining /bos/{slug} = public marketing knowledge (PHP).
        return false;
    }

    /// <summary>
    /// True when the request path is product Intelligence Platform (<c>/ip</c>, <c>/IP</c>).
    /// Super-CP only — never answer on named live tenants.
    /// </summary>
    public static bool IsProductIpPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var p = path.Replace('\\', '/');
        var q = p.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
        {
            p = p[..q];
        }

        if (p.Equals("/php-reference/ip", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("/php-reference/ip/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (p.Equals("/IP", StringComparison.Ordinal)
            || p.StartsWith("/IP/", StringComparison.Ordinal))
        {
            return true;
        }

        if (p.Equals("/ip", StringComparison.OrdinalIgnoreCase)
            || p.Equals("/ip/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!p.StartsWith("/ip/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = p["/ip/".Length..].Trim('/');
        if (rest.Length == 0)
        {
            return true;
        }

        var slash = rest.IndexOf('/');
        var first = slash < 0 ? rest : rest[..slash];

        return ProductIpFirstSegments.Contains(first)
            || first.EndsWith("-app", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True when path is LifeOS product (<c>/lifeos</c>…).</summary>
    public static bool IsLifeOsPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var p = path.Replace('\\', '/');
        var q = p.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
        {
            p = p[..q];
        }

        return p.Equals("/lifeos", StringComparison.OrdinalIgnoreCase)
            || p.Equals("/lifeos/", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("/lifeos/", StringComparison.OrdinalIgnoreCase);
    }
}
