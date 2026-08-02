using EcomAE.Platform.Auth;
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

        endpoints.MapGet(EcomAeRoutes.ErpDashboardSummary, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin)
            {
                return Unauthorized("Admin session required for ERP dashboard summary.");
            }

            var summary = await dashboards.BuildErpAsync(cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary,
                session = SessionPayload(session),
                note = "Read-only migration summary. PHP ERP dashboard remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpAccountsSummary, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for accounts summary.");
            }

            var result = await dashboards.BuildErpAccountsAsync(cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary = result.Summary,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only ERP cash/supplier KPI digest using epc_erp_* tables. PHP remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpSuppliers, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for suppliers digest.");
            }

            var result = await dashboards.ListErpSuppliersAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                suppliers = result.Suppliers,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only ERP suppliers digest. PHP epc_erp_list_suppliers remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpPurchases, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for purchases digest.");
            }

            var result = await dashboards.ListErpPurchasesAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                purchases = result.Purchases,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only ERP purchases digest. PHP epc_erp_list_purchases remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpCashAccounts, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for cash accounts digest.");
            }

            var result = await dashboards.ListErpCashAccountsAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                accounts = result.Accounts,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only ERP cash/bank accounts digest. PHP epc_erp_list_cash_accounts remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpCashEntries, async (
            HttpContext context,
            int? limit,
            int? account_id,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for cash entries digest.");
            }

            var result = await dashboards.ListErpCashEntriesAsync(account_id, limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                entries = result.Entries,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only ERP cash/bank entries digest. PHP epc_erp_list_cash_entries remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpInvoices, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for invoices digest.");
            }

            var result = await dashboards.ListErpInvoicesAsync(limit ?? 150, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                invoices = result.Invoices,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only e-invoice documents digest. PHP epc_erp_invoice_list remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpGlJournals, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for GL journals digest.");
            }

            var result = await dashboards.ListErpGlJournalsAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                journals = result.Journals,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only GL journals digest. PHP epc_erp_gl_list_journals remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpCoaAccounts, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for COA accounts digest.");
            }

            var result = await dashboards.ListErpCoaAccountsAsync(limit ?? 300, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                accounts = result.Accounts,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only chart-of-accounts digest. PHP epc_erp_coa remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpWarehouses, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for warehouses digest.");
            }

            var result = await dashboards.ListErpWarehousesAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                warehouses = result.Warehouses,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only ERP warehouses digest. PHP epc_erp_inv_warehouses remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpSalesOrders, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for sales-orders digest.");
            }

            var result = await dashboards.ListErpSalesOrdersAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                orders = result.Orders,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only ERP sales-orders digest. PHP epc_erp_sales_orders remains authoritative."
            });
        });

        foreach (var route in EcomAeRoutes.ErpAliases)
        {
            endpoints.MapGet(route, async (HttpContext context, ISurfaceShellCatalog shells, ILegacySessionValidator validator) =>
            {
                var session = await validator.ValidateAsync(context);
                if (session.Kind != LegacySessionKind.Admin)
                {
                    return Unauthorized("Admin session required for ERP shell.");
                }

                var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
                return Results.Ok(new
                {
                    shell = shells.Build("erp", tenant),
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
