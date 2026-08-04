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

        endpoints.MapPost(EcomAeRoutes.ControlPanelOmsSetItemStatus, async (
            HttpContext context,
            CpOmsSetItemStatusBody? body,
            ILegacySessionValidator validator,
            ICpOmsSetItemStatusDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for OMS set-item-status dry-run.");
            }

            body ??= new CpOmsSetItemStatusBody(0, 0, 0, false);
            var result = await dryRun.EvaluateAsync(
                new CpOmsSetItemStatusRequest(body.OrderId, body.ItemId, body.Status, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ControlPanelOmsSetItemsStatus, async (
            HttpContext context,
            CpOmsSetItemsStatusBody? body,
            ILegacySessionValidator validator,
            ICpOmsSetItemsStatusDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for OMS set-items-status dry-run.");
            }

            body ??= new CpOmsSetItemsStatusBody(0, 0, [], false);
            var result = await dryRun.EvaluateAsync(
                new CpOmsSetItemsStatusRequest(body.OrderId, body.Status, body.ItemIds ?? [], body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
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
                note = "Read-only menu metadata + structure summary (raw structure JSON omitted). PHP menu_manager remains authoritative for create/edit."
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

        endpoints.MapGet(EcomAeRoutes.ControlPanelPowerBi, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for power-bi digest.");
            }

            var result = await dashboards.BuildCpPowerBiDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                reports = result.Reports,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_power_bi_config + epc_power_bi_reports metadata. Configure/embed writes remain PHP epc_power_bi."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelMobileApps, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for mobile-apps digest.");
            }

            var result = await dashboards.BuildCpMobileAppsDigestAsync(cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only integrations_json.mobile metadata. save_mobile writes remain PHP epc_mobile_apps."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelMetabase, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for metabase digest.");
            }

            var result = await dashboards.BuildCpMetabaseDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                dashboards = result.Dashboards,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_metabase_config + epc_metabase_dashboards (secret_key never returned). Writes remain PHP epc_metabase_embed."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelNlReporting, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for nl-reporting digest.");
            }

            var result = await dashboards.ListCpNlReportDefinitionsAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                definitions = result.Definitions,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_report_definitions metadata (query_template/recipients omitted). Writes remain PHP epc_nl_reporting."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelMarketingBroadcast, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for marketing-broadcast digest.");
            }

            var result = await dashboards.BuildCpMarketingBroadcastDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                campaigns = result.Campaigns,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_marketing_broadcast_campaigns metadata (body_html/text omitted). Send remains PHP epc_marketing_broadcast."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelDemoTenants, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for demo-tenants digest.");
            }

            var result = await dashboards.ListCpDemoTenantsAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                tenants = result.Tenants,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_portal_tenants WHERE is_demo=1 (passwords never returned). Provision remains PHP epc_demo_tenants_manage."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelPartsAgentChats, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for parts-agent-chats digest.");
            }

            var result = await dashboards.BuildCpPartsAgentDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                sessions = result.Sessions,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_parts_agent_* metadata (system_prompt/client_ip omitted). Chat UX remains PHP parts_agent."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelPosOverview, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for pos-overview digest.");
            }

            var result = await dashboards.BuildCpPosOverviewDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                sales = result.Sales,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_pos_settings + epc_pos_sales. Terminal sales writes remain PHP epc_pos_terminal."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelTaxToolkits, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for tax-toolkits digest.");
            }

            var result = await dashboards.BuildCpTaxToolkitsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                toolkits = result.Toolkits,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_tax_toolkits metadata (rules_json/reg_number omitted). Install/configure remains PHP epc_tax_toolkit_manage."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelSmsWhatsapp, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for sms-whatsapp digest.");
            }

            var result = await dashboards.BuildCpSmsWhatsappDigestAsync(limit ?? 50, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                operators = result.Operators,
                whatsappLog = result.WhatsappLog,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only sms_api + epc_whatsapp_notify_log (parameters_values/tokens/raw phone omitted). Configure/send remains PHP."
            });
        });


        endpoints.MapGet(EcomAeRoutes.ControlPanelCrmBoard, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for crm-board digest.");
            }

            var result = await dashboards.BuildCpCrmBoardDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                leads = result.Leads,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_crm_* KPIs + leads (email/phone/notes omitted). CRM UX remains PHP crm_main."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelDocumentControl, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for document-control digest.");
            }

            var result = await dashboards.BuildCpDocumentControlDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                templates = result.Templates,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_document_templates (HTML/bank secrets omitted). Print remains PHP document_control."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelDeliveryMethods, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for delivery-methods digest.");
            }

            var result = await dashboards.BuildCpDeliveryMethodsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                modes = result.Modes,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only shop_obtaining_modes (parameters_values omitted). Configure remains PHP sposoby-polucheniya."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelCrosses, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for crosses digest.");
            }

            var result = await dashboards.BuildCpCrossesDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                pairs = result.Pairs,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only shop_docpart_articles_analogs_list pairs. Import/edit remains PHP /CP/shop/crosses."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelHrOverview, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for hr-overview digest.");
            }

            var result = await dashboards.BuildCpHrOverviewDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                employees = result.Employees,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_hr_* KPIs + employees (salary/allowances/currency/payslip omitted). PHP people/HR shell remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelProductionOverview, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for production-overview digest.");
            }

            var result = await dashboards.BuildCpProductionOverviewDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                workOrders = result.WorkOrders,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_mfg_* KPIs + work orders (cost columns omitted). PHP production/manufacturing shell remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelProjectsOverview, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for projects-overview digest.");
            }

            var result = await dashboards.BuildCpProjectsOverviewDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                projects = result.Projects,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_prj_* KPIs + projects (timesheet rates omitted). PHP projects shell remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelIndustryPacks, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for industry-packs digest.");
            }

            var result = await dashboards.BuildCpIndustryPacksDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                packs = result.Packs,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_industry_packs metadata (modules/gl_template/tax_rules/theme/product_attrs JSON omitted). PHP industry_settings remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelJewelleryRetail, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for jewellery-retail digest.");
            }

            var result = await dashboards.BuildCpJewelleryRetailDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                vouchers = result.Vouchers,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_jewel_* KPIs + vouchers (mobile/email/tel/passport/remarks/narration/customer PII/cost omitted). PHP retail/jewellery shell remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelPriceLists, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for price-lists digest.");
            }

            var result = await dashboards.BuildCpPriceListsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                lists = result.Lists,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_pl_lists KPIs + lists (stats_json/error_text/stored_relpath omitted). PHP /CP/shop/prices remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelAutoPrice, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for auto-price digest.");
            }

            var result = await dashboards.BuildCpAutoPriceDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                rules = result.Rules,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_auto_price_rules KPIs + rules (config_json/notes/meta omitted). PHP epc_auto_price_engine remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelUaeTaxCompliance, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for uae-tax-compliance digest.");
            }

            var result = await dashboards.BuildCpUaeTaxComplianceDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                items = result.Items,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_uae_tax_legislation_items KPIs + items (erp_summary/compliance_actions_json/pdf_url/passport omitted). PHP uae-tax-compliance remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelBudgets, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for budgets digest.");
            }

            var result = await dashboards.BuildCpBudgetsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                budgets = result.Budgets,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_erp_pm_budgets KPIs + budgets (note omitted). PHP budgeting shell remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelCarriers, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for carriers digest.");
            }

            var result = await dashboards.BuildCpCarriersDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                carriers = result.Carriers,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_erp_carriers KPIs + carriers (contact_name/phone/email/tax_id omitted). PHP /CP/shop/logistics/carriers remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelPaymentGateways, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for payment-gateways digest.");
            }

            var result = await dashboards.BuildCpPaymentGatewaysDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                gateways = result.Gateways,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only shop_payment_systems KPIs + gateways (parameters/parameters_values/description omitted). PHP /CP/shop/payments/payments remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelWorkflows, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for workflows digest.");
            }

            var result = await dashboards.BuildCpWorkflowsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                workflows = result.Workflows,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_workflows KPIs + workflows (trigger_config/description omitted). PHP workflow automation shell remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelPurchaseRequests, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for purchase-requests digest.");
            }

            var result = await dashboards.BuildCpPurchaseRequestsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                requests = result.Requests,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_proc_req KPIs + requests (justification/decision_note omitted). PHP purchase requisitions shell remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelPromotions, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for promotions digest.");
            }

            var result = await dashboards.BuildCpPromotionsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                promotions = result.Promotions,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_promo_promotions KPIs + promotions. PHP epc_promotions_engine remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelCrmOpportunities, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for crm-opportunities digest.");
            }

            var result = await dashboards.BuildCpCrmOpportunitiesDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                opportunities = result.Opportunities,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_crm_opportunities KPIs + opportunities (notes omitted). PHP sales opportunities / CRM shell remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelIntegrations, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for integrations digest.");
            }

            var result = await dashboards.BuildCpIntegrationsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                integrations = result.Integrations,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_webhooks KPIs + integrations (secret_hash/secret_encrypted/events omitted). PHP epc_integrations_hub remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelPageBuilder, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for page-builder digest.");
            }

            var result = await dashboards.BuildCpPageBuilderDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                layouts = result.Layouts,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_page_builder_layouts KPIs + layouts (layout_json/brand_json omitted). PHP epc_visual_page_editor remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelProductCatalogue, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for product-catalogue digest.");
            }

            var result = await dashboards.BuildCpProductCatalogueDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                products = result.Products,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only shop_catalogue_products KPIs + products (safe columns). PHP catalogue editor remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelPlatformGovernance, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for platform-governance digest.");
            }

            var result = await dashboards.BuildCpPlatformGovernanceDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                rules = result.Rules,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_platform_governance_rules KPIs + rules (description/config_json omitted). PHP epc_platform_governance remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelEinvoiceDocuments, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for einvoice-documents digest.");
            }

            var result = await dashboards.BuildCpEinvoiceDocumentsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                documents = result.Documents,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_einvoice_documents KPIs + documents (seller_json/buyer_json/xml/validation/tax_breakdown omitted). PHP tax einvoice tab remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelJewelleryRepairs, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for jewellery-repairs digest.");
            }

            var result = await dashboards.BuildCpJewelleryRepairsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                repairs = result.Repairs,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_jewel_repair KPIs + repairs (mobile/email/tel/remarks/narration omitted). PHP service_mgmt jw_repairs remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelCrmTickets, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for crm-tickets digest.");
            }

            var result = await dashboards.BuildCpCrmTicketsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                tickets = result.Tickets,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_crm_tickets KPIs + tickets (message bodies omitted). PHP CRM shell remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelMarketingGrowth, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for marketing-growth digest.");
            }

            var result = await dashboards.BuildCpMarketingGrowthDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                reviews = result.Reviews,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_marketing_* KPIs + reviews (notes omitted). PHP marketing growth / campaigns hub remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelSoc2Compliance, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for soc2-compliance digest.");
            }

            var result = await dashboards.BuildCpSoc2ComplianceDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                controls = result.Controls,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_soc2_* KPIs + controls (description/implementation omitted). PHP epc_soc2_compliance remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelCostModels, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for cost-models digest.");
            }

            var result = await dashboards.BuildCpCostModelsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                items = result.Items,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_costm_* KPIs + items (detail_json omitted). PHP cost_mgmt cost_models remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelFinAdvanced, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for fin-advanced digest.");
            }

            var result = await dashboards.BuildCpFinAdvancedDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                periods = result.Periods,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_fin_* KPIs + periods (basis/schedule/lines JSON omitted). PHP cost_acct/finance fin_advanced remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelBlockchainProofs, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for blockchain-proofs digest.");
            }

            var result = await dashboards.BuildCpBlockchainProofsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                proofs = result.Proofs,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_bc_* KPIs + proofs (payload_json/merkle_proof_json omitted). PHP tax/audit_wb blockchain_proofs remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelLandedCost, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for landed-cost digest.");
            }

            var result = await dashboards.BuildCpLandedCostDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                sheets = result.Sheets,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_landed_cost_* KPIs + sheets (notes omitted). PHP landed_cost_area remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelWarehouseWms, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for warehouse-wms digest.");
            }

            var result = await dashboards.BuildCpWarehouseWmsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                work = result.Work,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_erp_wms_* KPIs + work pool. PHP warehouse/mhei WMS remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelAiService, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for ai-service digest.");
            }

            var result = await dashboards.BuildCpAiServiceDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                queries = result.Queries,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_ai_* KPIs + queries (input_text/output_text omitted). PHP epc_ai_service remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelReturnsRma, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for returns-rma digest.");
            }

            var result = await dashboards.BuildCpReturnsRmaDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                requests = result.Requests,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_rma_*/epc_warranties KPIs + requests (description/resolution_notes omitted). PHP returns-manager remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelIsolationAudit, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for isolation-audit digest.");
            }

            var result = await dashboards.BuildCpIsolationAuditDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                runs = result.Runs,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_ci_* KPIs + audit runs (report_json omitted). PHP commerce isolation audit remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelAmlCompliance, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for aml-compliance digest.");
            }

            var result = await dashboards.BuildCpAmlComplianceDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                kyc = result.Kyc,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_aml_* KPIs + KYC rows (notes/id_document_path omitted). PHP tax aml_compliance remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelJewelleryMasters, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for jewellery-masters digest.");
            }

            var result = await dashboards.BuildCpJewelleryMastersDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                karats = result.Karats,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_jewel_karat_master/rate_type/barcode KPIs + karat rows (description omitted). PHP jewellery masters remain authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelConsolidations, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for consolidations digest.");
            }

            var result = await dashboards.BuildCpConsolidationsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                entities = result.Entities,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_cons_* KPIs + group entities. PHP consolidations area remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelCrmActivities, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for crm-activities digest.");
            }

            var result = await dashboards.BuildCpCrmActivitiesDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                activities = result.Activities,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_crm_activities KPIs + rows (notes omitted). PHP CRM activities remain authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelAuthMfa, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for auth-mfa digest.");
            }

            var result = await dashboards.BuildCpAuthMfaDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                secrets = result.Secrets,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_mfa_* KPIs + enrollment rows (secret/webauthn material omitted). PHP auth settings remain authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelElectronicReporting, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for electronic-reporting digest.");
            }

            var result = await dashboards.BuildCpElectronicReportingDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                formats = result.Formats,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_er_* KPIs + formats (run preview omitted). PHP tax elec_reporting remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelCollectionsDunning, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for collections-dunning digest.");
            }

            var result = await dashboards.BuildCpCollectionsDunningDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                queue = result.Queue,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_dunning_* KPIs + queue (notes omitted). PHP collections/dunning remains authoritative."
            });
        });


        endpoints.MapGet(EcomAeRoutes.ControlPanelMarketplaceChannels, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for marketplace-channels digest.");
            }

            var result = await dashboards.BuildCpMarketplaceChannelsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                channels = result.Channels,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_marketplace_* KPIs + channels (config_json omitted). PHP marketplace/channels remain authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelDemandIntelligence, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for demand-intelligence digest.");
            }

            var result = await dashboards.BuildCpDemandIntelligenceDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                countries = result.Countries,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_demand_* KPIs + countries. PHP demand countries remain authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelCreditLimits, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for credit-limits digest.");
            }

            var result = await dashboards.BuildCpCreditLimitsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                limits = result.Limits,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_credit_* KPIs + limits (notes omitted). PHP credit limit engine remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelInsuranceCompliance, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for insurance-compliance digest.");
            }

            var result = await dashboards.BuildCpInsuranceComplianceDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                policies = result.Policies,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_erp_ins_* KPIs + policies (notes/emails omitted). PHP risk insurance remains authoritative."
            });
        });


        endpoints.MapGet(EcomAeRoutes.ControlPanelAuditTrail, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for audit-trail digest.");
            }

            var result = await dashboards.BuildCpAuditTrailDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                entries = result.Entries,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_erp_audit_log KPIs + entries (detail/old/new JSON omitted). PHP audit workbench remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelDocExpiry, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for doc-expiry digest.");
            }

            var result = await dashboards.BuildCpDocExpiryDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                documents = result.Documents,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_erp_doc_expiry* KPIs + documents (notes/emails/paths omitted). PHP doc expiry remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelTenantConfig, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for tenant-config digest.");
            }

            var result = await dashboards.BuildCpTenantConfigDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                entries = result.Entries,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_tenant_config* KPIs + keys (config_value omitted). PHP tenant configuration remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelJewelleryStockVerification, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for jewellery-stock-verification digest.");
            }

            var result = await dashboards.BuildCpJewelleryStockVerificationDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                verifications = result.Verifications,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_jewel_stock_verification* KPIs + vouchers (remarks omitted). PHP jewellery stock verification remains authoritative."
            });
        });

        
        endpoints.MapGet(EcomAeRoutes.ControlPanelTaxExternalReporting, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for tax-external-reporting digest.");
            }

            var result = await dashboards.BuildCpTaxExternalReportingDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                rules = result.Rules,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_cmp_rules + staging/audit KPIs + rules (value_json/notes omitted). PHP tax external reporting remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelPoApprovals, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for po-approvals digest.");
            }

            var result = await dashboards.BuildCpPoApprovalsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                requests = result.Requests,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_po_requests + approval_steps KPIs + requests (description/notes/attachments/items JSON omitted). PHP PO approval remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelFinanceClose, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for finance-close digest.");
            }

            var result = await dashboards.BuildCpFinanceCloseDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                batches = result.Batches,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_erp_opening_batches/lines + epc_erp_periods/close_log KPIs + batches (batch notes/meta_json/checklist omitted). PHP opening balances / year-end close remain authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelJewelleryFixing, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for jewellery-fixing digest.");
            }

            var result = await dashboards.BuildCpJewelleryFixingDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                fixings = result.Fixings,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_jewel_fixing + epc_fix_unfix_* + epc_jewel_petty_cash KPIs + fixings (remarks/notes omitted). PHP jewellery fixing / purchase window remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelWebTracker, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for web-tracker digest.");
            }

            var result = await dashboards.BuildCpWebTrackerDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                sessions = result.Sessions,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_web_tracker_sessions/pageviews/events KPIs + sessions (ip/ua/meta_json omitted). PHP web tracker remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelAbandonedCarts, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for abandoned-carts digest.");
            }

            var result = await dashboards.BuildCpAbandonedCartsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                carts = result.Carts,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only shop_carts abandoned-cart KPIs + lines (guest/session preferred). Deletes/filters remain PHP /CP/shop/orders/carts."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelQuoteRequests, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for quote-requests digest.");
            }

            var result = await dashboards.BuildCpQuoteRequestsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                quotes = result.Quotes,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only shop_quote_requests + shop_quote_items KPIs + quotes (admin_note/customer_note/product_object_json omitted). PHP quote requests remain authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelPlatformCommunication, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for platform-communication digest.");
            }

            var result = await dashboards.BuildCpPlatformCommunicationDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                tasks = result.Tasks,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_platform_comm_settings + epc_platform_internal_tasks KPIs + tasks (description omitted). PHP super CP communication remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelInfoBlocks, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for info-blocks digest.");
            }

            var result = await dashboards.BuildCpInfoBlocksDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                blocks = result.Blocks,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_platform_info_blocks KPIs + blocks (content_html omitted). PHP info blocks CMS remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelFreeTools, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for free-tools digest.");
            }

            var result = await dashboards.BuildCpFreeToolsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                accounts = result.Accounts,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_free_tool_accounts/saves/settings KPIs + accounts (token/pass_hash/del_code_hash/payload omitted). PHP free tools admin remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelConfigSandbox, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for config-sandbox digest.");
            }

            var result = await dashboards.BuildCpConfigSandboxDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                snapshots = result.Snapshots,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_config_snapshots + epc_sandbox_changes KPIs + snapshots (config_data/old_value/new_value omitted). PHP config sandbox remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelMarketplaceApps, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for marketplace-apps digest.");
            }

            var result = await dashboards.BuildCpMarketplaceAppsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                apps = result.Apps,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_marketplace_apps/installs/reviews KPIs + apps (description/features/config/review_text omitted). PHP marketplace portal remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelNotifications, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for notifications digest.");
            }

            var result = await dashboards.BuildCpNotificationsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                notifications = result.Notifications,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_notifications + epc_notification_prefs KPIs + notifications (body/metadata omitted). PHP notification settings remain authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelPortalSettings, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for portal-settings digest.");
            }

            var result = await dashboards.BuildCpPortalSettingsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                sites = result.Sites,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_portal_site_settings + epc_portal_deploy_targets KPIs + sites (contact_json/enabled_packs_json/theme_json/cp_menu_json/erp_modules_json omitted). PHP portal settings remain authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelDataMigrations, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for data-migrations digest.");
            }

            var result = await dashboards.BuildCpDataMigrationsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                migrations = result.Migrations,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_data_migrations + epc_data_migration_rows KPIs + migrations (file_path/column_mapping/validation_errors/options/raw_data/mapped_data omitted). PHP data migration remains authoritative."
            });
        });


        endpoints.MapGet(EcomAeRoutes.ControlPanelGeoRegions, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for geo-regions digest.");
            }

            var result = await dashboards.BuildCpGeoRegionsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                nodes = result.Nodes,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only shop_geo + shop_offices_geo_map KPIs + nodes (raw lang string bodies; value stored as lang id). PHP Geo / regions remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelProductFilters, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for product-filters digest.");
            }

            var result = await dashboards.BuildCpProductFiltersDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                filters = result.Filters,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only shop_docpart_filter KPIs + filters (list_storages JSON). PHP Product filters remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelSearchTabs, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for search-tabs digest.");
            }

            var result = await dashboards.BuildCpSearchTabsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                tabs = result.Tabs,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only shop_docpart_search_tabs KPIs + tabs (parameters_values JSON). PHP Search tabs remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelSystemRequests, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for system-requests digest.");
            }

            var result = await dashboards.BuildCpSystemRequestsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                requests = result.Requests,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only users_vin KPIs + requests (VIN request text body (injection-prone PHP cookie filters not ported)). PHP System requests remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelAdditionalTexts, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for additional-texts digest.");
            }

            var result = await dashboards.BuildCpAdditionalTextsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                texts = result.Texts,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only text_for_url KPIs + texts (content HTML + description_tag bodies in rows (title/keywords only)). PHP Additional texts remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelSliderBanners, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for slider-banners digest.");
            }

            var result = await dashboards.BuildCpSliderBannersDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                images = result.Images,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only slider_images + slider_setings KPIs + images (none critical (paths only)). PHP Slider / banners remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelStructureDumps, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for structure-dumps digest.");
            }

            var result = await dashboards.BuildCpStructureDumpsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                dumps = result.Dumps,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only content_structure_dumps KPIs + dumps (dump file bodies). PHP Structure dumps remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelCommunicationsTest, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for communications-test digest.");
            }

            var result = await dashboards.BuildCpCommunicationsTestDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                channels = result.Channels,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only debug_results + sms_api KPIs + channels (debug_result blobs + sms parameters_values secrets). PHP Communications test remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelLanguages, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for languages digest.");
            }

            var result = await dashboards.BuildCpLanguagesDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                languages = result.Languages,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only lang_languages KPIs + languages (translation string bodies). PHP Languages remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelPluginsManager, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for plugins-manager digest.");
            }

            var result = await dashboards.BuildCpPluginsManagerDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                plugins = result.Plugins,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only plugins KPIs + plugins (data_value JSON + filesystem delete side-effects). PHP Plugins manager remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelTemplatesManager, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for templates-manager digest.");
            }

            var result = await dashboards.BuildCpTemplatesManagerDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                templates = result.Templates,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only templates KPIs + templates (data_value JSON + FS delete). PHP Templates manager remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelDesignTokens, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for design-tokens digest.");
            }

            var result = await dashboards.BuildCpDesignTokensDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                tokens = result.Tokens,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_settings (brand_*) KPIs + tokens (setting_value (colors/URLs); ASP.NET also tolerates missing site_key via resilient KPIs). PHP Design tokens remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelSitemap, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for sitemap digest.");
            }

            var result = await dashboards.BuildCpSitemapDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                pages = result.Pages,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only content + shop_catalogue_categories + shop_catalogue_products KPIs + pages (sitemap.xml file artifact (generation remains PHP); content HTML omitted). PHP Sitemap remains authoritative."
            });
        });


        endpoints.MapGet(EcomAeRoutes.ControlPanelFailoverStatus, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for failover-status digest.");
            }

            var result = await dashboards.BuildCpFailoverStatusDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                signals = result.Signals,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only filesystem epc-platform-status.* KPIs + signals (secrets inside failover config). PHP Failover status remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelOpsGuides, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for ops-guides digest.");
            }

            var result = await dashboards.BuildCpOpsGuidesDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                items = result.Items,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only control_groups + control_items KPIs + items (guide HTML bodies). PHP Ops guides remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelFileManager, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for file-manager digest.");
            }

            var result = await dashboards.BuildCpFileManagerDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                entries = result.Entries,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only filesystem /content/files KPIs + entries (file contents). PHP File manager remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelServerIp, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for server-ip digest.");
            }

            var result = await dashboards.BuildCpServerIpDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                addresses = result.Addresses,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only runtime host KPIs + addresses (no outbound ipify). PHP Server IP remains authoritative."
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

    private sealed record CpOmsSetItemStatusBody(long OrderId, long ItemId, int Status, bool ConfirmWrites = false);
    private sealed record CpOmsSetItemsStatusBody(long OrderId, int Status, IReadOnlyList<long>? ItemIds, bool ConfirmWrites = false);
}
