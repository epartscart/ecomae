namespace EcomAE.Platform.Configuration;

public sealed class MigrationRouteCutoverOptions
{
    public const string SectionName = "MigrationRouteCutover";

    public bool ApiShadowTrafficEnabled { get; set; } = true;

    /// <summary>Product storefront is ASP.NET-primary for all tenants (PHP via /php-reference/* only).</summary>
    public bool StorefrontAspNetEnabled { get; set; } = true;

    /// <summary>Product CP/ERP/BOS are ASP.NET-primary for all tenants (PHP via /php-reference/* only).</summary>
    public bool AdminAspNetEnabled { get; set; } = true;

    /// <summary>Keep true until dual-sample-green per exact write route (reference keep ≠ removal).</summary>
    public bool RequirePhpFallback { get; set; } = true;
}
