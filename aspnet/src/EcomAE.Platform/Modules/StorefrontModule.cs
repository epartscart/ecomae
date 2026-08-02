using EcomAE.Platform.Auth;
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

        endpoints.MapGet(EcomAeRoutes.StorefrontAccount, async (
            HttpContext context,
            ISurfaceShellCatalog shells,
            ILegacySessionValidator validator,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer)
            {
                return Unauthorized("Customer session required for storefront account shell.");
            }

            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            return Results.Ok(new
            {
                shell = shells.Build("storefront", tenant),
                session = SessionPayload(session),
                note = "Customer-gated account shell only. PHP storefront remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.StorefrontAccountSummary, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer)
            {
                return Unauthorized("Customer session required for storefront account summary.");
            }

            var summary = await dashboards.BuildStorefrontAccountAsync(session.UserId, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "storefront",
                summary,
                session = SessionPayload(session),
                note = "Read-only migration summary. PHP customer account remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.StorefrontOrders, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer)
            {
                return Unauthorized("Customer session required for storefront orders digest.");
            }

            var result = await dashboards.ListStorefrontOrdersAsync(session.UserId, limit ?? 25, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "storefront",
                user_id = result.UserId,
                orders = result.Orders,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only recent shop_orders digest. PHP customer orders remain authoritative."
            });
        });
    }

    private static IResult Unauthorized(string message) => Results.Json(
        new { ok = false, error = new { code = "unauthorized", message } },
        statusCode: StatusCodes.Status401Unauthorized);

    private static object SessionPayload(LegacySessionContext session) => new
    {
        kind = session.Kind.ToString(),
        user_id = session.UserId,
        email = session.Email,
        group_ids = session.Groups,
        capabilities = session.Capabilities,
        permissions = session.Permissions
    };
}
