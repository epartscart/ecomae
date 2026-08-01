namespace EcomAE.Platform.Api.Catalog;

public sealed class CatalogParityReporter : ICatalogParityReporter
{
    public CatalogParityReport BuildReport()
    {
        return new CatalogParityReport(
            "PHP api/v1/catalog.php and Laximo/UMAPI integrations",
            "ASP.NET Core catalog status and planned endpoint contracts",
            "contract-ready-with-gaps",
            ReadyForShadowTraffic: true,
            [
                "Wire manufacturer, model, vehicle, catalog group, and part endpoints to production providers.",
                "Replay captured PHP catalog fixtures against ASP.NET Core responses before public cutover.",
                "Enforce legacy API-key product/action policy and tenant scoping on every catalog route."
            ]);
    }
}
