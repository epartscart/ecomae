namespace EcomAE.Platform.Configuration;

public sealed class EcomAeOptions
{
    public const string SectionName = "EcomAE";

    public string PlatformHost { get; set; } = "www.ecomae.com";

    public string DefaultBackendPath { get; set; } = "/CP";

    public string DefaultErpPath { get; set; } = "/ERP";

    public string DefaultBosPath { get; set; } = "/BOS";

    public string TenantRegistryConnectionStringName { get; set; } = "TenantRegistry";

    /// <summary>
    /// PHP <c>$DP_Config->secret_succession</c>. Required for ASP.NET login-bridge session minting.
    /// Prefer env <c>ECOMAE_SECRET_SUCCESSION</c> / <c>EcomAE__SecretSuccession</c> — never commit secrets.
    /// </summary>
    public string SecretSuccession { get; set; } = "";

    public List<TenantSeedOptions> SeedTenants { get; set; } = [];
}
