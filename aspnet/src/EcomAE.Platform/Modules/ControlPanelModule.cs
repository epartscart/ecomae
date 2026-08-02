using EcomAE.Platform.Auth;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
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
        endpoints.MapGet(EcomAeRoutes.ControlPanelParity, (IControlPanelParityReporter reporter) => Results.Ok(reporter.BuildReport()));

        endpoints.MapGet(EcomAeRoutes.ControlPanelDashboardSummary, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin)
            {
                return Unauthorized("Admin session required for CP dashboard summary.");
            }

            var summary = await dashboards.BuildControlPanelAsync(cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary,
                session = SessionPayload(session),
                note = "Read-only migration summary. PHP CP dashboard remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelTenants, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for tenant digest.");
            }

            var result = await dashboards.ListPortalTenantsAsync(limit ?? 100, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                tenants = result.Tenants,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only portal tenant digest. PHP tenant control remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelUsers, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for users digest.");
            }

            var result = await dashboards.ListCpUsersAsync(limit ?? 100, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                users = result.Users,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only users digest. PHP user_manager remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelGroups, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for groups digest.");
            }

            var result = await dashboards.ListCpGroupsAsync(limit ?? 100, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                groups = result.Groups,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only groups digest. PHP user_groups remains authoritative."
            });
        });

        foreach (var route in EcomAeRoutes.ControlPanelAliases)
        {
            endpoints.MapGet(route, async (HttpContext context, ISurfaceShellCatalog shells, ILegacySessionValidator validator) =>
            {
                var session = await validator.ValidateAsync(context);
                if (session.Kind != LegacySessionKind.Admin)
                {
                    return Unauthorized("Admin session required for CP shell.");
                }

                var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
                return Results.Ok(new
                {
                    shell = shells.Build("cp", tenant),
                    session = SessionPayload(session)
                });
            });
        }
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
        has_backend_access = session.HasBackendAccess,
        capabilities = session.Capabilities,
        module_acl = session.Modules,
        permissions = session.Permissions
    };
}
