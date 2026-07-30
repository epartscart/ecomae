using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using EcomAE.Platform.Surfaces;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Security;

namespace EcomAE.Platform.Modules;

public sealed class BosModule : ISurfaceModule
{
    public SurfaceModuleDescriptor Descriptor { get; } = new(
        "bos",
        "BOS / BOC",
        EcomAeRoutes.Bos,
        "bos/ and cp/content/control/portal/epc_boc_*",
        "shell-started",
        [EcomAePermissions.SuperBosAccess]);

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(EcomAeRoutes.Bos, (HttpContext context, ISurfaceShellCatalog shells) =>
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            return Results.Ok(shells.Build("bos", tenant));
        });
    }
}
