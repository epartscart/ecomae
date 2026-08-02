namespace EcomAE.Platform.Configuration;

public sealed class MigrationRouteCutoverOptions
{
    public const string SectionName = "MigrationRouteCutover";

    public bool ApiShadowTrafficEnabled { get; set; } = true;

    public bool StorefrontAspNetEnabled { get; set; }

    public bool AdminAspNetEnabled { get; set; }

    public bool RequirePhpFallback { get; set; } = true;
}
