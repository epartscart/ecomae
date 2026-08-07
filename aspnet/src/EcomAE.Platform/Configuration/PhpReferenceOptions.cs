namespace EcomAE.Platform.Configuration;

/// <summary>
/// Confirmed operating model: ASP.NET Core becomes the live primary runtime while the
/// PHP project remains available as a <b>reference</b> to compare previous results and find gaps.
/// This is not PHP decommission / source deletion and does not flip cutover gates.
/// </summary>
public sealed class PhpReferenceOptions
{
    public const string SectionName = "EcomAE:PhpReference";

    /// <summary>
    /// When true, reporters and the compare board expose PHP reference URLs for gap-finding.
    /// Does not move live traffic by itself.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Stable mode label for digests/boards.</summary>
    public string Mode { get; set; } = "aspnet-primary-php-reference";

    /// <summary>Human-confirmed architecture intent (config declaration only).</summary>
    public bool ArchitectureConfirmed { get; set; } = true;

    /// <summary>Keep the PHP project/docroot available for side-by-side compares.</summary>
    public bool KeepPhpProjectAvailable { get; set; } = true;

    /// <summary>
    /// Product chrome and storefront links use ASP.NET <c>/storefront/*-app</c> paths
    /// (not interim PHP <c>/en/*</c>). Default <c>true</c>: product is ASP.NET-based;
    /// PHP remains only under <c>/php-reference/*</c> for gap compares.
    /// Does <b>not</b> flip cutoverAllowed / readyForPhpRemoval / delete PHP source.
    /// </summary>
    public bool PreferAspNetStorefrontApps { get; set; } = true;

    /// <summary>
    /// Temporary deep-test switch: stop serving PHP reference + stop stub→PHP redirects so
    /// product traffic stays on ASP.NET apps. Does <b>not</b> delete PHP source, does <b>not</b>
    /// set cutoverAllowed/readyForPhpRemoval, and KeepPhpProjectAvailable must stay true.
    /// Operator: <c>cloudpanel_temporarily_deactivate_php_serving.sh</c>.
    /// </summary>
    public bool TemporarilyDeactivatePhpServing { get; set; }

    /// <summary>Reference PHP base for www / Super CP (no trailing slash required).</summary>
    public string WwwPhpBaseUrl { get; set; } = "https://www.ecomae.com";

    /// <summary>Reference PHP base for a named live tenant (gap-finding only).</summary>
    public string TenantPhpBaseUrl { get; set; } = "https://www.epartscart.com";

    /// <summary>Optional dedicated Super CP PHP host.</summary>
    public string DedicatedCpPhpBaseUrl { get; set; } = "https://cp.ecomae.com";

    /// <summary>Optional ASP.NET primary base used on the compare board.</summary>
    public string AspNetPrimaryBaseUrl { get; set; } = "https://www.ecomae.com";

    /// <summary>
    /// Optional PHP docroot path on the server (operator/env; never delete until separate approval).
    /// </summary>
    public string? PhpDocRoot { get; set; }

    /// <summary>Operator note shown on boards.</summary>
    public string Note { get; set; } =
        "ASP.NET Core is product-primary (PreferAspNetStorefrontApps=true). PHP stays installed only as /php-reference/* for gap-finding until dual-sample + separate ReadyToRemovePhp / readyForPhpRemoval gate. cutoverAllowed=false. PreferAspNetStorefrontApps ≠ PHP source deletion. TemporarilyDeactivatePhpServing only pauses PHP HTTP for deep tests.";
}
