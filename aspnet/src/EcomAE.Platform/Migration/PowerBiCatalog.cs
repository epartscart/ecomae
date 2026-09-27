using System.Text.RegularExpressions;

namespace EcomAE.Platform.Migration;

public sealed record PowerBiDataset(
    string Id,
    string Name,
    string Description,
    string Path,
    IReadOnlyList<string> Formats,
    string Scope,
    string Refresh,
    string Parameters);

public sealed record PowerBiEmbedResolution(
    bool Ok,
    string Phase,
    string Url,
    string Error);

/// <summary>
/// Typed twin of PHP <c>content/general_pages/epc_power_bi.php</c>: capability matrix,
/// Web-connector dataset catalogue, connect guide and embed resolution.
/// </summary>
public static class PowerBiCatalog
{
    public const string Version = "1.0.0";
    public const string ApiRoot = "https://www.ecomae.com/epc-api/v1/powerbi";

    private static readonly Regex EmbedHost = new(
        "^https://([a-z0-9-]+\\.)*powerbi\\.(com|us)(/|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyList<string> AvailableNow { get; } =
    [
        "web_connector_json",
        "web_connector_csv",
        "api_key_auth",
        "tenant_isolation",
        "workspace_config_storage",
        "url_embed_iframe",
        "native_erp_dashboard",
        "metabase_embed_parallel",
    ];

    public static IReadOnlyList<(string Key, string Why)> NeedsCustomerCredentials { get; } =
    [
        ("azure_ad_app", "Azure AD application (client id/secret) in their Microsoft tenant"),
        ("power_bi_pro_or_embedded", "Power BI Pro / Premium / Embedded capacity for secure embed tokens"),
        ("workspace_access", "Workspace + report IDs from their Power BI service"),
    ];

    public static IReadOnlyList<string> NotInScopePhaseA { get; } =
    [
        "azure_embed_token_generation",
        "power_bi_rest_admin_apis",
        "row_level_security_via_azure",
    ];

    public static IReadOnlyList<string> ConnectGuide { get; } =
    [
        "1. Issue a tenant API key with scope read:bi (or read:erp / read:*).",
        "2. In Power BI Desktop → Get data → Web → Advanced.",
        "3. URL example: https://www.ecomae.com/epc-api/v1/powerbi/kpis?format=csv",
        "4. HTTP header: X-API-Key = <tenant key>.",
        "5. Schedule refresh in Power BI Service with the same header.",
    ];

    public const string CurlExample = """
        # Example — KPI CSV for Power BI Desktop
        curl -s -H "X-API-Key: epc_YOUR_TENANT_read_XXXXXXXX" \
          "https://www.ecomae.com/epc-api/v1/powerbi/kpis?format=csv"
        """;

    public static IReadOnlyList<PowerBiDataset> Datasets { get; } =
    [
        new("catalog", "Dataset catalog", "Lists all Power BI–ready endpoints for this tenant.",
            ApiRoot + "/catalog", ["json"], "read:bi", "manual", ""),
        new("kpis", "ERP KPI snapshot", "Flat KPI rows (revenue, AR, AP, cash, VAT) for cards and scorecards.",
            ApiRoot + "/kpis", ["json", "csv"], "read:bi", "scheduled", ""),
        new("orders", "Recent orders", "Successfully created shop orders (tabular).",
            ApiRoot + "/orders", ["json", "csv"], "read:bi", "scheduled", "limit = 1–200 (default 100)"),
        new("sales", "Sales register", "Completed order sales ex-VAT with paid/due amounts.",
            ApiRoot + "/sales", ["json", "csv"], "read:bi", "scheduled", "from = YYYY-MM-DD, to = YYYY-MM-DD"),
        new("stock", "Inventory stock", "On-hand qty, average cost, and stock value by SKU/warehouse.",
            ApiRoot + "/stock", ["json", "csv"], "read:bi", "scheduled", ""),
        new("gl", "GL trial balance", "Chart-of-accounts trial balance lines.",
            ApiRoot + "/gl", ["json", "csv"], "read:bi", "scheduled", "to = YYYY-MM-DD optional as-of date"),
        new("metrics", "BI metric snapshots", "Latest materialized KPI snapshots from the BI metrics engine (when computed).",
            ApiRoot + "/metrics", ["json", "csv"], "read:bi", "scheduled", ""),
    ];

    public static bool EmbedUrlAllowed(string? url)
        => EmbedHost.IsMatch((url ?? string.Empty).Trim());

    /// <summary>PHP <c>epc_power_bi_embed_resolve</c> decision table over an already-loaded config row.</summary>
    public static PowerBiEmbedResolution ResolveEmbed(
        bool configActive,
        string? embedMode,
        string? configEmbedUrl,
        string? reportEmbedUrl = null)
    {
        if (!configActive)
        {
            return new(false, "config_missing",
                string.Empty,
                "Power BI not configured. Save workspace/report IDs or an embed URL in CP → Power BI.");
        }

        var mode = (embedMode ?? string.Empty).Trim().ToLowerInvariant();
        var url = (reportEmbedUrl ?? string.Empty).Trim();
        if (url.Length > 0)
        {
            mode = "url";
        }
        else
        {
            url = (configEmbedUrl ?? string.Empty).Trim();
        }

        if (mode == "azure")
        {
            return new(false, "needs_azure",
                string.Empty,
                "Azure embed mode needs customer Azure AD app + Power BI capacity. Store IDs now; token minting ships when credentials are provided.");
        }

        if (url.Length == 0 || mode is "" or "none")
        {
            return new(false, "url_missing",
                string.Empty,
                "No embed URL saved. Use Power BI publish-to-web / secure share link, or connect Desktop to /epc-api/v1/powerbi/* datasets.");
        }

        return EmbedUrlAllowed(url)
            ? new(true, "ready", url, string.Empty)
            : new(false, "url_invalid", string.Empty, "Embed URL must be an https://*.powerbi.com link.");
    }
}
