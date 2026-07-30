using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Services;
using EcomAE.Platform.Surfaces;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Security;

namespace EcomAE.Platform.Modules;

public sealed class ErpModule : ISurfaceModule
{
    public SurfaceModuleDescriptor Descriptor { get; } = new(
        "erp",
        "ERP",
        EcomAeRoutes.Erp,
        "content/shop/finance/ and cp/content/shop/finance/erp/",
        "shell-started",
        [EcomAePermissions.SuperErpAccess, EcomAePermissions.TenantErpAccess]);

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(EcomAeRoutes.ErpParity, (IErpParityReporter reporter) => Results.Ok(reporter.BuildReport()));

        foreach (var route in EcomAeRoutes.ErpAliases)
        {
            endpoints.MapGet(route, (HttpContext context, ISurfaceShellCatalog shells) =>
            {
                var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
                return Results.Ok(shells.Build("erp", tenant));
            });
        }
    }
}
