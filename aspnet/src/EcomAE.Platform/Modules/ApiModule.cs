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
        endpoints.MapGet("/api/migration/status", () => Results.Ok(new
        {
            surface = "API",
            migration = "placeholder",
            next = "Port catalog, price lookup, tenant, ERP, BOS, mobile, and webhook APIs"
        }));
    }
}
