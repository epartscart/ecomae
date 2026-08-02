namespace EcomAE.Platform.Api.Catalog;

public sealed class CatalogParityReporter : ICatalogParityReporter
{
    public CatalogParityReport BuildReport()
    {
        return new CatalogParityReport(
            "PHP api/v1/catalog.php and Laximo/UMAPI integrations",
            "ASP.NET Core /api/v1/catalog/status with DbCatalogStatusRepository + catalog API-key auth",
            "status-route-wired-awaiting-staging",
            ReadyForShadowTraffic: true,
            [
                "Wire manufacturer, model, vehicle, catalog group, and part endpoints to production providers.",
                "Replay captured PHP catalog status fixtures against ASP.NET Core responses before public cutover.",
                "Enforce staging smoke with epc_catalog_ API keys before enabling location = /api/v1/catalog/status shadow."
            ]);
    }
}
