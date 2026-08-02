using EcomAE.Platform.Auth;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
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
        endpoints.MapGet(EcomAeRoutes.BosParity, (IBosParityReporter reporter) => Results.Ok(reporter.BuildReport()));

        endpoints.MapGet(EcomAeRoutes.BosFleetSummary, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin)
            {
                return Unauthorized("Admin session required for BOS fleet summary.");
            }

            var summary = await dashboards.BuildBosAsync(cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "bos",
                summary,
                session = SessionPayload(session),
                note = "Read-only migration summary. PHP BOS command center remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.BosTenants, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return Unauthorized("Admin BOS capability required for tenant digest.");
            }

            var result = await dashboards.ListPortalTenantsAsync(limit ?? 100, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "bos",
                tenants = result.Tenants,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only portal tenant digest. PHP BOS tenant switcher remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.BosFleetHealth, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return Unauthorized("Admin BOS capability required for fleet health.");
            }

            var result = await dashboards.BuildBosFleetHealthAsync(limit ?? 25, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "bos",
                summary = result.Summary,
                sample_tenants = result.SampleTenants,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only fleet health digest. PHP epc_bos_health_check remains authoritative."
            });
        });

        foreach (var route in EcomAeRoutes.BosAliases)
        {
            endpoints.MapGet(route, async (HttpContext context, ISurfaceShellCatalog shells, ILegacySessionValidator validator) =>
            {
                var session = await validator.ValidateAsync(context);
                if (session.Kind != LegacySessionKind.Admin)
                {
                    return Unauthorized("Admin session required for BOS shell.");
                }

                var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
                return Results.Ok(new
                {
                    shell = shells.Build("bos", tenant),
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
