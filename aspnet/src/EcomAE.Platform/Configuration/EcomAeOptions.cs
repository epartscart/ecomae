namespace EcomAE.Platform.Configuration;

public sealed class EcomAeOptions
{
    public const string SectionName = "EcomAE";

    public string PlatformHost { get; set; } = "www.ecomae.com";

    public string DefaultBackendPath { get; set; } = "/CP";

    public string DefaultErpPath { get; set; } = "/ERP";

    public string DefaultBosPath { get; set; } = "/BOS";

    public string TenantRegistryConnectionStringName { get; set; } = "TenantRegistry";

    public List<TenantSeedOptions> SeedTenants { get; set; } = [];
}
