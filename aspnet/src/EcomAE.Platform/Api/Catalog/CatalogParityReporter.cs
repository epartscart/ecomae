namespace EcomAE.Platform.Api.Catalog;

public sealed class CatalogParityReporter : ICatalogParityReporter
{
    public CatalogParityReport BuildReport()
    {
        return new CatalogParityReport(
            "PHP api/v1/catalog.php and Laximo/UMAPI integrations",
            "ASP.NET Core catalog status/manufacturers/models/modifications/brands/vin/engines/analogs/article-brands/categories/products/engine-search/article-links/article/articles/engine/suppliers/brand-parts DB/cache readers + API-key auth",
            "catalog-cache-routes-wired-awaiting-staging",
            ReadyForShadowTraffic: true,
            [
                "Wire live UMAPI proxy fills for articles/engine (not PHP-cacheable) still served by PHP on cache miss.",
                "Replay captured PHP catalog fixtures against ASP.NET Core responses before public cutover.",
                "Enforce staging smoke with epc_catalog_ API keys before enabling exact-route catalog shadows."
            ]);
    }
}
