namespace EcomAE.Platform.Api.Catalog;

public sealed class CatalogParityReporter : ICatalogParityReporter
{
    public CatalogParityReport BuildReport()
    {
        return new CatalogParityReport(
            "PHP api/v1/catalog.php and Laximo/UMAPI integrations",
            "ASP.NET Core catalog status/manufacturers/models/modifications/brands/vin/engines/analogs/article-brands/categories/products DB/cache readers + API-key auth",
            "catalog-cache-routes-wired-awaiting-staging",
            ReadyForShadowTraffic: true,
            [
                "Wire live UMAPI proxy fills still served by PHP when cache misses.",
                "Replay captured PHP catalog fixtures against ASP.NET Core responses before public cutover.",
                "Enforce staging smoke with epc_catalog_ API keys before enabling exact-route catalog shadows."
            ]);
    }
}
