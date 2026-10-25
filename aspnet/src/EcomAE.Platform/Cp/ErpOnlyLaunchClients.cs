namespace EcomAE.Platform.Cp;

/// <summary>
/// The four Super-host ERP-only companies plus the Super mailbox the operator
/// asked to see after deploy. Registry create is separate; this catalog is what
/// Super CP home / mail / tenants must display even before rows exist.
/// </summary>
public static class ErpOnlyLaunchClients
{
    public const string SuperMailbox = "yawer@ecomae.com";

    public sealed record Client(
        string SiteKey,
        string TradeName,
        string IndustryCode,
        string Email,
        string LoginPath,
        string Country3);

    // ASAP Autoparts offboarded Oct 2026 — tenant removed from launch catalog so Super CP,
    // mail, and repair-onboard no longer surface or recreate it. Its database is retained for
    // backup requests; drop it manually when the retention period ends.
    public static readonly IReadOnlyList<Client> All =
    [
        new("sham", "Al Shamali Auto Parts Group", "auto_parts", "sham@ecomae.com", "/cp/client-erp/sham/", "uae"),
        new("indj", "INDJ Jewellery", "jewellery", "indj@ecomae.com", "/cp/client-erp/indj/", "uae"),
        new("fito", "FITO Fit-out", "fit_out", "fito@ecomae.com", "/cp/client-erp/fito/", "uae"),
        new("real", "REAL Real Estate", "real_estate", "real@ecomae.com", "/cp/client-erp/real/", "uae"),
        new("fmcg", "Qatar FMCG Group", "fmcg_wholesale", "fmcg@ecomae.com", "/cp/client-erp/fmcg/", "qat"),
    ];

    public static bool IsLaunchKey(string? siteKey)
        => All.Any(c => string.Equals(c.SiteKey, siteKey, StringComparison.OrdinalIgnoreCase));
}
