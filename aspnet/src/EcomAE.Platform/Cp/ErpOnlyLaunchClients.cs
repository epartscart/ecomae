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
        string LoginPath);

    public static readonly IReadOnlyList<Client> All =
    [
        new("asap", "ASAP Autoparts", "auto_parts", "asap@ecomae.com", "/cp/client-erp/asap/"),
        new("indj", "INDJ Jewellery", "jewellery", "indj@ecomae.com", "/cp/client-erp/indj/"),
        new("fito", "FITO Fit-out", "fit_out", "fito@ecomae.com", "/cp/client-erp/fito/"),
        new("real", "REAL Real Estate", "real_estate", "real@ecomae.com", "/cp/client-erp/real/"),
    ];

    public static bool IsLaunchKey(string? siteKey)
        => All.Any(c => string.Equals(c.SiteKey, siteKey, StringComparison.OrdinalIgnoreCase));
}
