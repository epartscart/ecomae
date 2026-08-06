using EcomAE.Platform.Migration;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Host-aware login branding (PHP epc_cp_login_hero_markup / epc_portal_tenant_brand parity).
/// Tenant hosts show animated or catalog logos; Super-CP shows platform mark.
/// </summary>
public static class LoginHostBrand
{
    public enum Kind
    {
        Platform,
        AnimatedEparts,
        TenantImage,
    }

    public sealed record Brand(
        Kind LogoKind,
        string SiteKey,
        string Label,
        string Tagline,
        string? LogoUrl,
        string AccentHex,
        string GlowRgba,
        string AtmosphereTheme,
        string[] ParticleColors,
        string RootModifierClass);

    private static readonly IReadOnlyDictionary<string, Brand> TenantCatalog =
        new Dictionary<string, Brand>(StringComparer.OrdinalIgnoreCase)
        {
            ["epartscart"] = new(
                Kind.AnimatedEparts,
                "epartscart",
                "eParts Cart",
                "Automotive spare parts",
                null,
                "#dc2626",
                "rgba(220,38,38,.28)",
                "autoparts",
                [
                    "rgba(220,38,38,.9)", "rgba(248,113,113,.7)", "rgba(254,202,202,.5)",
                    "rgba(255,255,255,.55)", "rgba(251,146,60,.55)", "rgba(185,28,28,.65)"
                ],
                "bos-login--tenant-epartscart"),
            ["electronicae"] = new(
                Kind.TenantImage,
                "electronicae",
                "Electronicae",
                "TECH • GAMING • UAE",
                "/content/files/images/ecomae-platform/assets/electronicae.png",
                "#e10a0a",
                "rgba(225,10,10,.24)",
                "circuit",
                [
                    "rgba(225,10,10,.85)", "rgba(56,189,248,.55)", "rgba(255,255,255,.45)",
                    "rgba(248,113,113,.5)", "rgba(14,165,233,.4)"
                ],
                "bos-login--tenant-electronicae"),
            ["stylenlook"] = new(
                Kind.TenantImage,
                "stylenlook",
                "StyleNLook",
                "FASHION & BEAUTY",
                "/content/files/images/ecomae-platform/assets/stylenlook.png",
                "#ec4899",
                "rgba(236,72,153,.26)",
                "fashion",
                [
                    "rgba(236,72,153,.85)", "rgba(244,114,182,.65)", "rgba(251,207,232,.5)",
                    "rgba(255,255,255,.5)", "rgba(192,38,211,.45)"
                ],
                "bos-login--tenant-stylenlook"),
            ["thejewellerytrend"] = new(
                Kind.TenantImage,
                "thejewellerytrend",
                "The Jewellery Trend",
                "STYLE • SPARKLE • SHINE",
                "/content/files/images/ecomae-platform/assets/thejewellerytrend.png",
                "#d97706",
                "rgba(217,119,6,.32)",
                "sparkle",
                [
                    "rgba(251,191,36,.9)", "rgba(217,119,6,.7)", "rgba(253,224,71,.55)",
                    "rgba(255,255,255,.65)", "rgba(245,158,11,.5)"
                ],
                "bos-login--tenant-jewellery"),
            ["taxofinca"] = new(
                Kind.TenantImage,
                "taxofinca",
                "TaxoFinca",
                "TAX & ACCOUNTING SOLUTIONS",
                "/content/files/images/ecomae-platform/assets/taxofinca.png",
                "#227a40",
                "rgba(34,122,64,.24)",
                "advisory",
                [
                    "rgba(34,197,94,.85)", "rgba(34,122,64,.65)", "rgba(134,239,172,.5)",
                    "rgba(255,255,255,.45)", "rgba(16,185,129,.5)"
                ],
                "bos-login--tenant-taxofinca"),
        };

    public static Brand Resolve(string? host, string surface = "cp")
    {
        var normalized = StripWww(PlatformHostPolicy.NormalizeHost(host));
        var surfaceKey = (surface ?? "cp").Trim().ToLowerInvariant();

        if (PlatformHostPolicy.IsSuperCpHost(host))
        {
            return PlatformBrand(surfaceKey);
        }

        var siteKey = SiteKeyFor(normalized);
        if (siteKey is not null && TenantCatalog.TryGetValue(siteKey, out var tenant))
        {
            return tenant;
        }

        var locked = LiveTenantPresentationLock.Tenants.FirstOrDefault(t =>
            t.Hosts.Any(h => StripWww(PlatformHostPolicy.NormalizeHost(h)) == normalized));
        if (locked is not null && TenantCatalog.TryGetValue(locked.Id, out var byId))
        {
            return byId;
        }

        return PlatformBrand(surfaceKey);
    }

    private static string StripWww(string host)
        => host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;

    private static Brand PlatformBrand(string surface) => surface switch
    {
        "erp" => new(
            Kind.Platform,
            "platform",
            "ERP",
            "Enterprise Resource Planning",
            null,
            "#0d9488",
            "rgba(13,148,136,.28)",
            "teal-moon",
            [
                "rgba(45,212,191,.8)", "rgba(13,148,136,.55)", "rgba(56,189,248,.45)",
                "rgba(255,255,255,.35)", "rgba(94,234,212,.4)"
            ],
            "bos-login--erp"),
        "bos" => new(
            Kind.Platform,
            "platform",
            "BOS",
            "Business Operating System",
            null,
            "#0ea5e9",
            "rgba(14,165,233,.28)",
            "cyan-stars",
            [
                "rgba(14,165,233,.8)", "rgba(56,189,248,.7)", "rgba(99,102,241,.5)",
                "rgba(255,255,255,.35)", "rgba(168,85,247,.4)"
            ],
            "bos-login--bos"),
        _ => new(
            Kind.Platform,
            "platform",
            "CP",
            "Control Panel · Operator Console",
            null,
            "#e11d48",
            "rgba(225,29,72,.28)",
            "crimson-stars",
            [
                "rgba(251,113,133,.8)", "rgba(225,29,72,.55)", "rgba(248,113,113,.45)",
                "rgba(255,255,255,.35)", "rgba(254,205,211,.4)"
            ],
            "bos-login--cp"),
    };

    private static string? SiteKeyFor(string normalized)
    {
        foreach (var key in TenantCatalog.Keys)
        {
            if (normalized == key + ".com" || normalized == key)
            {
                return key;
            }
        }

        return null;
    }
}
