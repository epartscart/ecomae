using EcomAE.Platform.Auth;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
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
        "presentation-shell-scaffolded",
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

        endpoints.MapGet(EcomAeRoutes.ControlPanelOrdersDigest, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for orders digest.");
            }

            var result = await dashboards.ListCpOrdersAsync(limit ?? 50, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                orders = result.Orders,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only shop_orders digest. PHP OMS (/CP/shop/orders/orders) remains authoritative for writes and full console. Office ACL not applied."
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

        endpoints.MapGet(EcomAeRoutes.ControlPanelModules, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for modules digest.");
            }

            var result = await dashboards.ListCpModulesAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                modules = result.Modules,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only modules digest. PHP modules_manager remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelConfigItems, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for config-items digest.");
            }

            var result = await dashboards.ListCpConfigItemsMetaAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                items = result.Items,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only config_items metadata only (no secret values). PHP config_edit remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelMenus, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for menus digest.");
            }

            var result = await dashboards.ListCpMenusAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                menus = result.Menus,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only menu metadata (structure JSON omitted). PHP menu_manager remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelPages, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for pages digest.");
            }

            var result = await dashboards.ListCpPagesAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                pages = result.Pages,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only content pages metadata (body omitted). PHP content_manager remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelAdminSessions, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for admin-sessions digest.");
            }

            var result = await dashboards.ListCpAdminSessionsAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                sessions = result.Sessions,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only admin session counts by user (raw session tokens never returned)."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelStorages, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for storages digest.");
            }

            var result = await dashboards.ListCpStoragesAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                storages = result.Storages,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only shop_storages digest. PHP shop storages UI remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelCurrencies, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for currencies digest.");
            }

            var result = await dashboards.ListCpCurrenciesAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                currencies = result.Currencies,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only shop_currencies digest. PHP currency manager remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelApiClients, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for api-clients digest.");
            }

            var result = await dashboards.ListCpApiClientsMetaAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                clients = result.Clients,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_api_clients metadata only (client_key_hash never returned)."
            });
        });

        foreach (var route in EcomAeRoutes.ControlPanelAliases)
        {
            endpoints.MapGet(route, async (
                HttpContext context,
                ISurfaceShellCatalog shells,
                ILegacyHtmlShellRenderer html,
                ILegacySessionValidator validator) =>
            {
                var session = await validator.ValidateAsync(context);
                if (session.Kind != LegacySessionKind.Admin)
                {
                    return Unauthorized("Admin session required for CP shell.");
                }

                var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
                return SurfaceShellResponder.Respond(
                    context,
                    "cp",
                    shells,
                    html,
                    tenant,
                    SessionPayload(session),
                    "Presentation-preserving CP shell. PHP Super CP remains authoritative until cutover approval.");
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
