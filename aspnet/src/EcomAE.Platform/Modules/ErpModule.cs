using EcomAE.Platform.Auth;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
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
        "presentation-shell-scaffolded",
        [EcomAePermissions.SuperErpAccess, EcomAePermissions.TenantErpAccess]);

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(EcomAeRoutes.ErpParity, (IErpParityReporter reporter) => Results.Ok(reporter.BuildReport()));

        endpoints.MapPost(EcomAeRoutes.ErpOnPremisesHealthDryRun, (
            OnPremisesHealthBody? body,
            IOnPremisesHealthDryRun dryRun) =>
        {
            body ??= new OnPremisesHealthBody(null, null, null, null, null, null, null, null, false);
            var result = dryRun.Evaluate(new OnPremisesHealthRequest(
                body.LicenseKey,
                body.Status,
                body.Uptime,
                body.DiskFreeGb,
                body.MemoryUsageMb,
                body.PhpVersion,
                body.DbSizeMb,
                body.LastBackup,
                body.ConfirmWrites));
            return Results.Ok(result.ToPayload());
        });

        endpoints.MapPost(EcomAeRoutes.ErpOnPremisesLicenseActivateDryRun, (
            OnPremisesLicenseActivateBody? body,
            IOnPremisesLicenseActivateDryRun dryRun) =>
        {
            body ??= new OnPremisesLicenseActivateBody(null, null, null, null, null, null, false);
            var result = dryRun.Evaluate(new OnPremisesLicenseActivateRequest(
                body.LicenseKey,
                body.Fingerprint,
                body.Hostname,
                body.Ip,
                body.PhpVersion,
                body.Os,
                body.ConfirmWrites));
            return Results.Ok(result.ToPayload());
        });

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

        endpoints.MapPost(EcomAeRoutes.ErpCashEntriesAmend, async (
            HttpContext context,
            ErpCashVoucherAmendBody? body,
            ILegacySessionValidator validator,
            IErpCashVoucherAmendDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for cash voucher amend dry-run.");
            }

            body ??= new ErpCashVoucherAmendBody(0, null, null, false);
            var result = await dryRun.EvaluateAsync(
                new ErpCashVoucherAmendRequest(body.EntryId, body.Reference, body.Note, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpCashEntriesVoid, async (
            HttpContext context,
            ErpCashVoucherVoidBody? body,
            ILegacySessionValidator validator,
            IErpCashVoucherVoidDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for cash voucher void dry-run.");
            }

            body ??= new ErpCashVoucherVoidBody(0, null, false);
            var result = await dryRun.EvaluateAsync(
                new ErpCashVoucherVoidRequest(body.EntryId, body.Reason, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpGlJournalsManual, async (
            HttpContext context,
            ErpGlManualEntryBody? body,
            ILegacySessionValidator validator,
            IErpGlManualEntryDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for GL manual entry dry-run.");
            }

            body ??= new ErpGlManualEntryBody([], null, null, false);
            var lines = (body.Lines ?? [])
                .Select(l => new ErpGlManualLine(l.CoaId, l.Debit, l.Credit, l.LineNote))
                .ToList();
            var result = await dryRun.EvaluateAsync(
                new ErpGlManualEntryRequest(lines, body.Reference, body.Description, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpGlJournalsReverse, async (
            HttpContext context,
            ErpGlReverseJournalBody? body,
            ILegacySessionValidator validator,
            IErpGlReverseJournalDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for GL reverse journal dry-run.");
            }

            body ??= new ErpGlReverseJournalBody(0, null, false);
            var result = await dryRun.EvaluateAsync(
                new ErpGlReverseJournalRequest(body.JournalId, body.Note, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpPurchasesVoid, async (
            HttpContext context,
            ErpPurchaseVoidBody? body,
            ILegacySessionValidator validator,
            IErpPurchaseVoidDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for purchase void dry-run.");
            }

            body ??= new ErpPurchaseVoidBody(0, null, false);
            var result = await dryRun.EvaluateAsync(
                new ErpPurchaseVoidRequest(body.PurchaseId, body.Reason, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpInvoicesCancel, async (
            HttpContext context,
            ErpInvoiceCancelBody? body,
            ILegacySessionValidator validator,
            IErpInvoiceCancelDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for invoice cancel dry-run.");
            }

            body ??= new ErpInvoiceCancelBody(0, null, false);
            var result = await dryRun.EvaluateAsync(
                new ErpInvoiceCancelRequest(body.InvoiceId, body.Reason, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpSalesOrdersCancel, async (
            HttpContext context,
            ErpSalesOrderCancelBody? body,
            ILegacySessionValidator validator,
            IErpSalesOrderCancelDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for sales-order cancel dry-run.");
            }

            body ??= new ErpSalesOrderCancelBody(0, null, false);
            var result = await dryRun.EvaluateAsync(
                new ErpSalesOrderCancelRequest(body.SalesOrderId, body.Reason, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpPurchaseOrdersDelete, async (
            HttpContext context,
            ErpPoDeleteBody? body,
            ILegacySessionValidator validator,
            IErpPoDeleteDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for PO delete dry-run.");
            }

            body ??= new ErpPoDeleteBody(0, false);
            var result = await dryRun.EvaluateAsync(
                new ErpPoDeleteRequest(body.PurchaseOrderId, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
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

        endpoints.MapGet(EcomAeRoutes.ErpPurchaseOrders, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for purchase-orders digest.");
            }

            var result = await dashboards.ListErpPurchaseOrdersAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                orders = result.Orders,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only ERP purchase-orders digest. PHP epc_erp_purchase_orders remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpInventoryStock, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for inventory-stock digest.");
            }

            var result = await dashboards.BuildErpInventoryStockSummaryAsync(cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary = result,
                session = SessionPayload(session),
                note = "Read-only ERP inventory stock KPI digest. PHP epc_erp_inv_stock remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpBankReconciliation, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for bank-reconciliation digest.");
            }

            var result = await dashboards.BuildErpBankReconciliationDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary = result.Summary,
                lines = result.Lines,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_erp_bank_statement_lines KPIs + lines. PHP bank_recon tab remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpStockTransfers, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for stock-transfers digest.");
            }

            var result = await dashboards.BuildErpStockTransfersDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary = result.Summary,
                transfers = result.Transfers,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_warehouse_transfers KPIs + transfers (notes omitted). PHP inventory/warehouse transfer UX remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpSalesQuotations, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for sales-quotations digest.");
            }

            var result = await dashboards.BuildErpSalesQuotationsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary = result.Summary,
                quotations = result.Quotations,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_crm_quotes KPIs + quotations (notes omitted). PHP sales proposals/quotations shell remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpWorkspaceFavorites, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for workspace-favorites digest.");
            }

            var result = await dashboards.BuildErpWorkspaceFavoritesDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary = result.Summary,
                favorites = result.Favorites,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_user_shortcuts KPIs + favorites. PHP ERP/CP dashboard shortcuts remain authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpFixedAssets, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for fixed-assets digest.");
            }

            var result = await dashboards.BuildErpFixedAssetsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary = result.Summary,
                assets = result.Assets,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_erp_fa_assets KPIs + assets (note omitted). PHP fixed_assets tab remains authoritative."
            });
        });

        foreach (var route in EcomAeRoutes.ErpAliases)
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
                    return Unauthorized("Admin session required for ERP shell.");
                }

                var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
                return SurfaceShellResponder.Respond(
                    context,
                    "erp",
                    shells,
                    html,
                    tenant,
                    SessionPayload(session),
                    "Presentation-preserving ERP shell. PHP Platform ERP remains authoritative until cutover approval.");
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

    private sealed record OnPremisesHealthBody(
        string? LicenseKey,
        string? Status,
        string? Uptime,
        decimal? DiskFreeGb,
        decimal? MemoryUsageMb,
        string? PhpVersion,
        decimal? DbSizeMb,
        string? LastBackup,
        bool ConfirmWrites = false);
    private sealed record OnPremisesLicenseActivateBody(
        string? LicenseKey,
        string? Fingerprint,
        string? Hostname = null,
        string? Ip = null,
        string? PhpVersion = null,
        string? Os = null,
        bool ConfirmWrites = false);
    private sealed record ErpCashVoucherAmendBody(long EntryId, string? Reference, string? Note, bool ConfirmWrites = false);
    private sealed record ErpCashVoucherVoidBody(long EntryId, string? Reason, bool ConfirmWrites = false);
    private sealed record ErpGlManualLineBody(long CoaId, decimal Debit, decimal Credit, string? LineNote = null);
    private sealed record ErpGlManualEntryBody(IReadOnlyList<ErpGlManualLineBody>? Lines, string? Reference, string? Description, bool ConfirmWrites = false);
    private sealed record ErpGlReverseJournalBody(long JournalId, string? Note, bool ConfirmWrites = false);
    private sealed record ErpPurchaseVoidBody(long PurchaseId, string? Reason, bool ConfirmWrites = false);
    private sealed record ErpInvoiceCancelBody(long InvoiceId, string? Reason, bool ConfirmWrites = false);
    private sealed record ErpSalesOrderCancelBody(long SalesOrderId, string? Reason, bool ConfirmWrites = false);
    private sealed record ErpPoDeleteBody(long PurchaseOrderId, bool ConfirmWrites = false);
}
