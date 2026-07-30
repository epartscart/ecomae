using EcomAE.Platform.Api.Catalog;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Security;

namespace EcomAE.Platform.Modules;

public sealed class ApiModule : ISurfaceModule
{
    public SurfaceModuleDescriptor Descriptor { get; } = new(
        "api",
        "Public and tenant APIs",
        EcomAeRoutes.ApiPrefix,
        "api/, api/v1/, epc-api-v1.php, pyapi/",
        "placeholder",
        [EcomAePermissions.ApiAccess]);

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(EcomAeRoutes.ApiMigrationStatus, () => Results.Ok(new
        {
            surface = "API",
            migration = "placeholder",
            next = "Port catalog, price lookup, tenant, ERP, BOS, mobile, and webhook APIs"
        }));

        endpoints.MapGet(EcomAeRoutes.CatalogStatus, () => Results.Ok(new CatalogStatusResult(
            "Catalog",
            "api/v1/catalog.php",
            EcomAeRoutes.CatalogStatus,
            "placeholder",
            [
                "Connect catalog status to UMAPI/Laximo replacement services",
                "Add manufacturer/model/catalog endpoints",
                "Retire PHP api/v1/catalog.php after parity"
            ])));

        endpoints.MapGet(EcomAeRoutes.PriceLookup, async (string brand, string article, IPriceLookupService service, CancellationToken cancellationToken) =>
        {
            var result = await service.LookupAsync(new PriceLookupRequest(brand, article), cancellationToken);
            return result.Status ? Results.Ok(result) : Results.BadRequest(result);
        });
    }
}
