namespace EcomAE.Platform.Configuration;

public sealed class PriceLookupOptions
{
    public const string SectionName = "PriceLookup";

    /// <summary>
    /// Optional CSV fixture path for captured PHP/staging parity exports.
    /// When set, DI prefers <c>CsvPriceOfferRepository</c> over the database provider.
    /// </summary>
    public string FixtureCsvPath { get; set; } = string.Empty;

    /// <summary>
    /// Connection string name used by <c>DbPriceOfferRepository</c>.
    /// Defaults to the tenant registry / platform MySQL connection.
    /// </summary>
    public string ConnectionStringName { get; set; } = "TenantRegistry";

    /// <summary>
    /// Optional database override when no request tenant context is available.
    /// Request-scoped <c>TenantContext.DatabaseName</c> always wins when present.
    /// </summary>
    public string? DatabaseName { get; set; }
}
