using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Services;
using EcomAE.Platform.Surfaces;
using EcomAE.Platform.Routing;

namespace EcomAE.Platform.Modules;

public sealed class StorefrontModule : ISurfaceModule
{
    public SurfaceModuleDescriptor Descriptor { get; } = new(
        "storefront",
        "Storefront / Marketing",
        "/",
        "content/shop/, content/general_pages/, templates/",
        "shell-started",
        []);

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(EcomAeRoutes.StorefrontParity, (IStorefrontParityReporter reporter) => Results.Ok(reporter.BuildReport()));

        endpoints.MapGet("/storefront/migration-placeholder", (HttpContext context, ISurfaceShellCatalog shells) =>
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            return Results.Ok(shells.Build("storefront", tenant));
        });
    }
}
