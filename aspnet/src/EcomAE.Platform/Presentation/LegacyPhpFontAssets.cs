namespace EcomAE.Platform.Presentation;

/// <summary>
/// Single PHP-parity product typography for CP, ERP, BOS, and storefront.
/// Canonical source: cp/templates/bootstrap_admin/styles/style.css
/// (<c>"Open Sans", "Helvetica Neue", Helvetica, Arial, sans-serif</c> at 14px).
/// Marketing keeps its own display stack; monospace is JetBrains for code only.
/// </summary>
public static class LegacyPhpFontAssets
{
    // Use %20 (not +) so Blazor attribute encoding cannot turn markers into &#x2B;
    // and presentation probes still see open%20sans / pt%20sans / jetbrains%20mono.
    public const string OpenSans =
        "https://fonts.googleapis.com/css2?family=Open%20Sans:wght@300;400;600;700&display=swap";

    public const string PtSans =
        "https://fonts.googleapis.com/css2?family=PT%20Sans:wght@400;700&display=swap";

    public const string FrauncesSora =
        "https://fonts.googleapis.com/css2?family=Fraunces:wght@600;700&family=Sora:wght@400;600;700&display=swap";

    /// <summary>Monospace only (tables/code). Body UI uses <see cref="ProductStack"/>.</summary>
    public const string JetBrainsMono =
        "https://fonts.googleapis.com/css2?family=JetBrains%20Mono:wght@400;600&display=swap";

    /// <summary>Obsolete name kept for callers; resolves to JetBrains Mono only (no Inter body).</summary>
    public const string InterJetBrains = JetBrainsMono;

    /// <summary>Matches PHP epc_ecomae_home_sections_enqueue / marketing headline fonts.</summary>
    public const string SyneDmSans =
        "https://fonts.googleapis.com/css2?family=Syne:wght@400;600;700;800&family=DM%20Sans:opsz,wght@9..40,300;9..40,400;9..40,500;9..40,600;9..40,700&display=swap";

    public const string FontAwesomeCdn =
        "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css";

    /// <summary>One body stack for CP / ERP / BOS / storefront (PHP CP + limo body).</summary>
    public const string ProductStack =
        "\"Open Sans\", \"Helvetica Neue\", Helvetica, Arial, sans-serif";

    /// <summary>One base size for CP / ERP / BOS / storefront (PHP CP style.css / nero body / BOS html).</summary>
    public const string BaseFontSize = "14px";

    public const string MarketingStack =
        "\"DM Sans\", system-ui, -apple-system, \"Segoe UI\", Roboto, sans-serif";

    public const string MonoStack =
        "\"JetBrains Mono\", ui-monospace, SFMono-Regular, Menlo, Consolas, monospace";

    public static string StackFor(string surfaceKey) => surfaceKey.Trim().ToLowerInvariant() switch
    {
        "marketing" => MarketingStack,
        // cp | erp | bos | storefront | default → same PHP product UI font
        _ => ProductStack
    };

    public static string FontSizeFor(string surfaceKey) => surfaceKey.Trim().ToLowerInvariant() switch
    {
        "marketing" => "16px",
        _ => BaseFontSize
    };

    public static IReadOnlyList<string> FontHrefsFor(string surfaceKey) => surfaceKey.Trim().ToLowerInvariant() switch
    {
        "marketing" => new[] { SyneDmSans },
        // Product surfaces share Open Sans; ERP keeps Fraunces/Sora for premium headings;
        // BOS keeps JetBrains Mono for code; storefront keeps PT Sans for legacy glyph pages.
        "erp" => new[] { OpenSans, FrauncesSora },
        "bos" => new[] { OpenSans, JetBrainsMono },
        "storefront" => new[] { OpenSans, PtSans },
        _ => new[] { OpenSans }
    };
}
