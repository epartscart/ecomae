using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using EcomAE.Platform.Surfaces;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Security;

namespace EcomAE.Platform.Modules;

public sealed class ControlPanelModule : ISurfaceModule
{
    public SurfaceModuleDescriptor Descriptor { get; } = new(
        "cp",
        "Control Panel / Super CP",
        EcomAeRoutes.ControlPanel,
        "cp/",
        "shell-started",
        [EcomAePermissions.SuperCpAccess, EcomAePermissions.TenantCpAccess]);

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(EcomAeRoutes.ControlPanel, (HttpContext context, ISurfaceShellCatalog shells) =>
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            return Results.Ok(shells.Build("cp", tenant));
        });
    }
}
