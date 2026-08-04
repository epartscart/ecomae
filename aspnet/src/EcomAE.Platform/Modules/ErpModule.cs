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

        endpoints.MapGet(EcomAeRoutes.ErpOnPremisesLicenses, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for on-premises licenses digest.");
            }

            var result = await dashboards.ListOnPremisesLicensesAsync(limit ?? 100, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "on-premises",
                licenses = result.Licenses,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                cutoverAllowed = false,
                phpAuthoritative = true,
                session = SessionPayload(session),
                note = "Read-only epc_onprem_licenses digest. notes/fingerprint/ip omitted; license keys masked. PHP activate/health + registry remain authoritative. Not in surface-digest exact-route allowlist until dual-sample."
            });
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

        endpoints.MapPost(EcomAeRoutes.ErpCashEntriesCreate, async (
            HttpContext context,
            ErpCashEntryCreateBody? body,
            ILegacySessionValidator validator,
            IErpCashEntryCreateDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for cash entry create dry-run.");
            }

            body ??= new ErpCashEntryCreateBody(0, 0, false, null, null, null, false);
            var result = await dryRun.EvaluateAsync(
                new ErpCashEntryCreateRequest(
                    body.AccountId, body.Amount, body.Direction, body.EntryType,
                    body.Reference, body.Note, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpCashEntriesReceiptVoucher, async (
            HttpContext context,
            ErpReceiptVoucherBody? body,
            ILegacySessionValidator validator,
            IErpReceiptVoucherDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for receipt voucher dry-run.");
            }

            body ??= new ErpReceiptVoucherBody(0, 0, 0, null, false);
            var result = dryRun.Evaluate(
                new ErpReceiptVoucherRequest(body.UserId, body.AccountId, body.Amount, body.SalesOrderId, body.ConfirmWrites));
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpCashEntriesPaymentVoucher, async (
            HttpContext context,
            ErpPaymentVoucherBody? body,
            ILegacySessionValidator validator,
            IErpPaymentVoucherDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for payment voucher dry-run.");
            }

            body ??= new ErpPaymentVoucherBody(0, 0, 0, false);
            var result = dryRun.Evaluate(
                new ErpPaymentVoucherRequest(body.SupplierId, body.AccountId, body.Amount, body.ConfirmWrites));
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpSuppliersCreate, async (
            HttpContext context,
            ErpSupplierCreateBody? body,
            ILegacySessionValidator validator,
            IErpSupplierCreateDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for supplier create dry-run.");
            }
            body ??= new ErpSupplierCreateBody(null, null, false);
            var result = dryRun.Evaluate(new ErpSupplierCreateRequest(body.Name, body.ContactEmail, body.ConfirmWrites));
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpPurchasesCreate, async (
            HttpContext context,
            ErpPurchaseCreateBody? body,
            ILegacySessionValidator validator,
            IErpPurchaseCreateDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for purchase create dry-run.");
            }
            body ??= new ErpPurchaseCreateBody(0, 0, false);
            var result = dryRun.Evaluate(new ErpPurchaseCreateRequest(body.SupplierId, body.AmountExVat, body.ConfirmWrites));
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpPurchasesDelete, async (
            HttpContext context,
            ErpPurchaseDeleteBody? body,
            ILegacySessionValidator validator,
            IErpPurchaseDeleteDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for purchase delete dry-run.");
            }
            body ??= new ErpPurchaseDeleteBody(0, false);
            var result = await dryRun.EvaluateAsync(new ErpPurchaseDeleteRequest(body.PurchaseId, body.ConfirmWrites), cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpPurchasesAmend, async (
            HttpContext context,
            ErpPurchaseAmendBody? body,
            ILegacySessionValidator validator,
            IErpPurchaseAmendDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for purchase amend dry-run.");
            }
            body ??= new ErpPurchaseAmendBody(0, null, null, null, false);
            var result = await dryRun.EvaluateAsync(
                new ErpPurchaseAmendRequest(
                    body.PurchaseId, body.InvoiceNumber, body.Note, body.AmountExVat, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpSalesOrdersDelete, async (
            HttpContext context,
            ErpSalesOrderDeleteBody? body,
            ILegacySessionValidator validator,
            IErpSalesOrderDeleteDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for sales-order delete dry-run.");
            }
            body ??= new ErpSalesOrderDeleteBody(0, false);
            var result = await dryRun.EvaluateAsync(
                new ErpSalesOrderDeleteRequest(body.SalesOrderId, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpCustomersMasterSave, async (
            HttpContext context,
            ErpCustomerMasterSaveBody? body,
            ILegacySessionValidator validator,
            IErpCustomerMasterSaveDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for customer master-save dry-run.");
            }
            body ??= new ErpCustomerMasterSaveBody(0, null, null, null, false, false);
            var result = dryRun.Evaluate(new ErpCustomerMasterSaveRequest(
                body.CustomerId, body.CustomerName, body.CreditLimit, body.TermsDays, body.OnHold, body.ConfirmWrites));
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpAftersalesRmaCreate, async (
            HttpContext context,
            ErpAsRmaCreateBody? body,
            ILegacySessionValidator validator,
            IErpAsRmaCreateDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for aftersales RMA create dry-run.");
            }
            body ??= new ErpAsRmaCreateBody(0, 0, null, null, false, null, false);
            var lines = (body.Lines ?? [])
                .Select(l => new ErpAsRmaCreateLine(l.ItemId, l.Qty, l.UnitPrice, l.ConditionNote))
                .ToList();
            var result = dryRun.Evaluate(new ErpAsRmaCreateRequest(
                body.CustomerId, body.SourceId, body.RmaNo, body.Reason, body.Restock, lines, body.ConfirmWrites));
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpPurchasesFromOrder, async (
            HttpContext context,
            ErpPurchaseFromOrderBody? body,
            ILegacySessionValidator validator,
            IErpPurchaseFromOrderDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for purchase-from-order dry-run.");
            }
            body ??= new ErpPurchaseFromOrderBody(0, 0, false);
            var result = await dryRun.EvaluateAsync(
                new ErpPurchaseFromOrderRequest(body.OrderId, body.SupplierId, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpCcySetRate, async (
            HttpContext context,
            ErpCcySetRateBody? body,
            ILegacySessionValidator validator,
            IErpCcySetRateDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for currency set-rate dry-run.");
            }
            body ??= new ErpCcySetRateBody(null, null, 0, false);
            var result = dryRun.Evaluate(new ErpCcySetRateRequest(body.From, body.To, body.Rate, body.ConfirmWrites));
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpPeriodSoftClose, async (
            HttpContext context,
            ErpPeriodSoftCloseBody? body,
            ILegacySessionValidator validator,
            IErpPeriodSoftCloseDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for period soft-close dry-run.");
            }
            body ??= new ErpPeriodSoftCloseBody(null, null, false);
            var result = dryRun.Evaluate(new ErpPeriodSoftCloseRequest(body.YearMonth, body.Note, body.ConfirmWrites));
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpPeriodLock, async (
            HttpContext context,
            ErpPeriodLockBody? body,
            ILegacySessionValidator validator,
            IErpPeriodLockDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for period lock dry-run.");
            }
            body ??= new ErpPeriodLockBody(null, null, false);
            var result = dryRun.Evaluate(new ErpPeriodLockRequest(body.YearMonth, body.Note, body.ConfirmWrites));
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpCustomerSettlement, async (
            HttpContext context,
            ErpCustomerSettlementBody? body,
            ILegacySessionValidator validator,
            IErpCustomerSettlementDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for customer settlement dry-run.");
            }
            body ??= new ErpCustomerSettlementBody(0, 0, "credit", "adjustment", 0, false);
            var result = dryRun.Evaluate(new ErpCustomerSettlementRequest(
                body.UserId, body.Amount, body.Direction, body.EntryKind, body.OrderId, body.ConfirmWrites));
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpSupplierSettlement, async (
            HttpContext context,
            ErpSupplierSettlementBody? body,
            ILegacySessionValidator validator,
            IErpSupplierSettlementDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for supplier settlement dry-run.");
            }
            body ??= new ErpSupplierSettlementBody(0, 0, "decrease", false);
            var result = dryRun.Evaluate(new ErpSupplierSettlementRequest(
                body.SupplierId, body.Amount, body.Direction, body.ConfirmWrites));
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpFiscalSetLock, async (
            HttpContext context,
            ErpFiscalSetLockBody? body,
            ILegacySessionValidator validator,
            IErpFiscalSetLockDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for fiscal set-lock dry-run.");
            }
            body ??= new ErpFiscalSetLockBody(0, null, false);
            var result = dryRun.Evaluate(new ErpFiscalSetLockRequest(body.LockDateUnix, body.Note, body.ConfirmWrites));
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpPeriodReopen, async (
            HttpContext context,
            ErpPeriodReopenBody? body,
            ILegacySessionValidator validator,
            IErpPeriodReopenDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for period reopen dry-run.");
            }
            body ??= new ErpPeriodReopenBody(null, null, false);
            var result = dryRun.Evaluate(new ErpPeriodReopenRequest(body.YearMonth, body.Note, body.ConfirmWrites));
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpPurchasesAdjust, async (
            HttpContext context,
            ErpPurchaseAdjustmentBody? body,
            ILegacySessionValidator validator,
            IErpPurchaseAdjustmentDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for purchase adjust dry-run.");
            }
            body ??= new ErpPurchaseAdjustmentBody(0, 0, null, false);
            var result = await dryRun.EvaluateAsync(
                new ErpPurchaseAdjustmentRequest(body.PurchaseId, body.DeltaExVat, body.Note, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpOrderSettlement, async (
            HttpContext context,
            ErpOrderSettlementBody? body,
            ILegacySessionValidator validator,
            IErpOrderSettlementDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for order settlement dry-run.");
            }
            body ??= new ErpOrderSettlementBody(0, 0, "credit", false);
            var result = await dryRun.EvaluateAsync(
                new ErpOrderSettlementRequest(body.OrderId, body.Amount, body.Direction, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpSuppliersSync, async (
            HttpContext context,
            ErpSyncSuppliersBody? body,
            ILegacySessionValidator validator,
            IErpSyncSuppliersDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for suppliers sync dry-run.");
            }
            body ??= new ErpSyncSuppliersBody(false);
            var result = dryRun.Evaluate(new ErpSyncSuppliersRequest(body.ConfirmWrites));
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpGlPostSales, async (
            HttpContext context,
            ErpGlPostSalesBody? body,
            ILegacySessionValidator validator,
            IErpGlPostSalesDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for GL post-sales dry-run.");
            }
            body ??= new ErpGlPostSalesBody(null, null, false);
            var result = dryRun.Evaluate(new ErpGlPostSalesRequest(body.DateFromUnix, body.DateToUnix, body.ConfirmWrites));
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpGlSyncUnposted, async (
            HttpContext context,
            ErpGlSyncUnpostedBody? body,
            ILegacySessionValidator validator,
            IErpGlSyncUnpostedDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for GL sync-unposted dry-run.");
            }
            body ??= new ErpGlSyncUnpostedBody(false);
            var result = dryRun.Evaluate(new ErpGlSyncUnpostedRequest(body.ConfirmWrites));
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpWorkflowStatus, async (
            HttpContext context,
            ErpWorkflowStatusBody? body,
            ILegacySessionValidator validator,
            IErpWorkflowStatusDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for workflow status dry-run.");
            }
            body ??= new ErpWorkflowStatusBody(0, "done", false);
            var result = dryRun.Evaluate(new ErpWorkflowStatusRequest(body.TaskId, body.Status, body.ConfirmWrites));
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpWorkflowCreate, async (
            HttpContext context,
            ErpWorkflowCreateBody? body,
            ILegacySessionValidator validator,
            IErpWorkflowCreateDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for workflow create dry-run.");
            }
            body ??= new ErpWorkflowCreateBody(null, "admin", "normal", 0, false);
            var result = dryRun.Evaluate(new ErpWorkflowCreateRequest(
                body.Title, body.DepartmentCode, body.Priority, body.OrderId, body.ConfirmWrites));
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpMarketingCreate, async (HttpContext context, ErpMarketingCreateBody? body, ILegacySessionValidator validator, IErpMarketingCreateDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for marketing create dry-run.");
            body ??= new ErpMarketingCreateBody(null, false);
            return Results.Ok(dryRun.Evaluate(new ErpMarketingCreateRequest(body.Name, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpSubscriptionsSave, async (HttpContext context, ErpSubscriptionSaveBody? body, ILegacySessionValidator validator, IErpSubscriptionSaveDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for subscription save dry-run.");
            body ??= new ErpSubscriptionSaveBody(null, null, 0, false);
            return Results.Ok(dryRun.Evaluate(new ErpSubscriptionSaveRequest(body.Code, body.Customer, body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpContractsSave, async (HttpContext context, ErpContractSaveBody? body, ILegacySessionValidator validator, IErpContractSaveDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for contract save dry-run.");
            body ??= new ErpContractSaveBody(null, null, 0, false);
            return Results.Ok(dryRun.Evaluate(new ErpContractSaveRequest(body.Code, body.Title, body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpWmsReceive, async (HttpContext context, ErpWmsReceiveBody? body, ILegacySessionValidator validator, IErpWmsReceiveDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for WMS receive dry-run.");
            body ??= new ErpWmsReceiveBody(null, 0, 0, 0, false);
            return Results.Ok(dryRun.Evaluate(new ErpWmsReceiveRequest(body.Item, body.Qty, body.ReceiveLocationId, body.PutawayLocationId, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpWmsLocationSave, async (HttpContext context, ErpWmsLocationSaveBody? body, ILegacySessionValidator validator, IErpWmsLocationSaveDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for WMS location save dry-run.");
            body ??= new ErpWmsLocationSaveBody(null, 0, false);
            return Results.Ok(dryRun.Evaluate(new ErpWmsLocationSaveRequest(body.Code, body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpCollectionsCaseSave, async (HttpContext context, ErpCollectionsCaseSaveBody? body, ILegacySessionValidator validator, IErpCollectionsCaseSaveDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for collections case save dry-run.");
            body ??= new ErpCollectionsCaseSaveBody(0, 0, false);
            return Results.Ok(dryRun.Evaluate(new ErpCollectionsCaseSaveRequest(body.CustomerId, body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpProcurementReqSave, async (HttpContext context, ErpProcReqSaveBody? body, ILegacySessionValidator validator, IErpProcReqSaveDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for procurement req save dry-run.");
            body ??= new ErpProcReqSaveBody(null, 0, false);
            return Results.Ok(dryRun.Evaluate(new ErpProcReqSaveRequest(body.Requester, body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpFinPeriodStatus, async (HttpContext context, ErpFinPeriodStatusBody? body, ILegacySessionValidator validator, IErpFinPeriodStatusDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for fin period status dry-run.");
            body ??= new ErpFinPeriodStatusBody(0, 0, "open", false);
            return Results.Ok(dryRun.Evaluate(new ErpFinPeriodStatusRequest(body.Fy, body.PeriodNo, body.Status, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpWmsWaveCreate, async (HttpContext context, ErpWmsWaveCreateBody? body, ILegacySessionValidator validator, IErpWmsWaveCreateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(null,0,null,false); return Results.Ok(dryRun.Evaluate(new ErpWmsWaveCreateRequest(body.Item, body.Qty, body.Reference, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpWmsWaveRelease, async (HttpContext context, ErpWmsWaveReleaseBody? body, ILegacySessionValidator validator, IErpWmsWaveReleaseDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpWmsWaveReleaseRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpWmsWorkComplete, async (HttpContext context, ErpWmsWorkCompleteBody? body, ILegacySessionValidator validator, IErpWmsWorkCompleteDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpWmsWorkCompleteRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpSubscriptionsStatus, async (HttpContext context, ErpSubscriptionStatusBody? body, ILegacySessionValidator validator, IErpSubscriptionStatusDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,"active",false); return Results.Ok(dryRun.Evaluate(new ErpSubscriptionStatusRequest(body.Id, body.Status, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpCollectionsCaseStatus, async (HttpContext context, ErpCollectionsCaseStatusBody? body, ILegacySessionValidator validator, IErpCollectionsCaseStatusDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,"new",false); return Results.Ok(dryRun.Evaluate(new ErpCollectionsCaseStatusRequest(body.Id, body.Status, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpProcurementReqSubmit, async (HttpContext context, ErpProcReqSubmitBody? body, ILegacySessionValidator validator, IErpProcReqSubmitDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpProcReqSubmitRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpProcurementReqDecision, async (HttpContext context, ErpProcReqDecisionBody? body, ILegacySessionValidator validator, IErpProcReqDecisionDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,true,null,false); return Results.Ok(dryRun.Evaluate(new ErpProcReqDecisionRequest(body.Id, body.Approve, body.Note, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpWmsLocationDelete, async (HttpContext context, ErpWmsLocationDeleteBody? body, ILegacySessionValidator validator, IErpWmsLocationDeleteDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpWmsLocationDeleteRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });

        endpoints.MapPost(EcomAeRoutes.ErpInvoicesDelete, async (
            HttpContext context,
            ErpInvoiceDeleteBody? body,
            ILegacySessionValidator validator,
            IErpInvoiceDeleteDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for invoice delete dry-run.");
            }
            body ??= new ErpInvoiceDeleteBody(0, false);
            var result = await dryRun.EvaluateAsync(new ErpInvoiceDeleteRequest(body.InvoiceId, body.ConfirmWrites), cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpCashAccountsCreate, async (
            HttpContext context,
            ErpCashAccountCreateBody? body,
            ILegacySessionValidator validator,
            IErpCashAccountCreateDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for cash account create dry-run.");
            }
            body ??= new ErpCashAccountCreateBody(null, "cash", false);
            var result = dryRun.Evaluate(new ErpCashAccountCreateRequest(body.Name, body.AccountType, body.ConfirmWrites));
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpCoaAccountsCreate, async (
            HttpContext context,
            ErpCoaCreateBody? body,
            ILegacySessionValidator validator,
            IErpCoaCreateDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for COA create dry-run.");
            }
            body ??= new ErpCoaCreateBody(null, null, "expense", false);
            var result = dryRun.Evaluate(new ErpCoaCreateRequest(body.Code, body.Name, body.AccountType, body.ConfirmWrites));
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
    private sealed record ErpCashEntryCreateBody(
        long AccountId,
        decimal Amount,
        bool Direction = false,
        string? EntryType = null,
        string? Reference = null,
        string? Note = null,
        bool ConfirmWrites = false);
    private sealed record ErpReceiptVoucherBody(
        long UserId,
        long AccountId,
        decimal Amount,
        long? SalesOrderId = null,
        bool ConfirmWrites = false);
    private sealed record ErpPaymentVoucherBody(
        long SupplierId,
        long AccountId,
        decimal Amount,
        bool ConfirmWrites = false);
    private sealed record ErpSupplierCreateBody(string? Name, string? ContactEmail = null, bool ConfirmWrites = false);
    private sealed record ErpPurchaseCreateBody(long SupplierId, decimal AmountExVat, bool ConfirmWrites = false);
    private sealed record ErpPurchaseDeleteBody(long PurchaseId, bool ConfirmWrites = false);
    private sealed record ErpPurchaseAmendBody(
        long PurchaseId,
        string? InvoiceNumber = null,
        string? Note = null,
        decimal? AmountExVat = null,
        bool ConfirmWrites = false);
    private sealed record ErpInvoiceDeleteBody(long InvoiceId, bool ConfirmWrites = false);
    private sealed record ErpCashAccountCreateBody(string? Name, string? AccountType = "cash", bool ConfirmWrites = false);
    private sealed record ErpCoaCreateBody(string? Code, string? Name, string? AccountType = "expense", bool ConfirmWrites = false);
    private sealed record ErpCustomerMasterSaveBody(
        long CustomerId,
        string? CustomerName = null,
        decimal? CreditLimit = null,
        int? TermsDays = null,
        bool OnHold = false,
        bool ConfirmWrites = false);
    private sealed record ErpAsRmaCreateLineBody(long ItemId, decimal Qty, decimal UnitPrice = 0, string? ConditionNote = null);
    private sealed record ErpAsRmaCreateBody(
        long CustomerId,
        long SourceId = 0,
        string? RmaNo = null,
        string? Reason = null,
        bool Restock = false,
        IReadOnlyList<ErpAsRmaCreateLineBody>? Lines = null,
        bool ConfirmWrites = false);
    private sealed record ErpPurchaseFromOrderBody(long OrderId, long SupplierId, bool ConfirmWrites = false);
    private sealed record ErpCcySetRateBody(string? From, string? To, decimal Rate, bool ConfirmWrites = false);
    private sealed record ErpPeriodSoftCloseBody(string? YearMonth, string? Note = null, bool ConfirmWrites = false);
    private sealed record ErpPeriodLockBody(string? YearMonth, string? Note = null, bool ConfirmWrites = false);
    private sealed record ErpCustomerSettlementBody(
        long UserId,
        decimal Amount,
        string? Direction = "credit",
        string? EntryKind = "adjustment",
        long OrderId = 0,
        bool ConfirmWrites = false);
    private sealed record ErpSupplierSettlementBody(
        long SupplierId,
        decimal Amount,
        string? Direction = "decrease",
        bool ConfirmWrites = false);
    private sealed record ErpFiscalSetLockBody(long LockDateUnix = 0, string? Note = null, bool ConfirmWrites = false);
    private sealed record ErpPeriodReopenBody(string? YearMonth, string? Note = null, bool ConfirmWrites = false);
    private sealed record ErpPurchaseAdjustmentBody(long PurchaseId, decimal DeltaExVat, string? Note = null, bool ConfirmWrites = false);
    private sealed record ErpOrderSettlementBody(long OrderId, decimal Amount, string? Direction = "credit", bool ConfirmWrites = false);
    private sealed record ErpSyncSuppliersBody(bool ConfirmWrites = false);
    private sealed record ErpGlPostSalesBody(long? DateFromUnix = null, long? DateToUnix = null, bool ConfirmWrites = false);
    private sealed record ErpGlSyncUnpostedBody(bool ConfirmWrites = false);
    private sealed record ErpWorkflowStatusBody(long TaskId, string? Status = "done", bool ConfirmWrites = false);
    private sealed record ErpWorkflowCreateBody(
        string? Title,
        string? DepartmentCode = "admin",
        string? Priority = "normal",
        long OrderId = 0,
        bool ConfirmWrites = false);
    private sealed record ErpMarketingCreateBody(string? Name, bool ConfirmWrites = false);
    private sealed record ErpSubscriptionSaveBody(string? Code, string? Customer, long Id = 0, bool ConfirmWrites = false);
    private sealed record ErpContractSaveBody(string? Code, string? Title, long Id = 0, bool ConfirmWrites = false);
    private sealed record ErpWmsReceiveBody(string? Item, decimal Qty, long ReceiveLocationId = 0, long PutawayLocationId = 0, bool ConfirmWrites = false);
    private sealed record ErpWmsLocationSaveBody(string? Code, long Id = 0, bool ConfirmWrites = false);
    private sealed record ErpCollectionsCaseSaveBody(long CustomerId = 0, long Id = 0, bool ConfirmWrites = false);
    private sealed record ErpProcReqSaveBody(string? Requester, long Id = 0, bool ConfirmWrites = false);
    private sealed record ErpFinPeriodStatusBody(int Fy, int PeriodNo, string? Status = "open", bool ConfirmWrites = false);
    private sealed record ErpWmsWaveCreateBody(string? Item, decimal Qty, string? Reference = null, bool ConfirmWrites = false);
    private sealed record ErpWmsWaveReleaseBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpWmsWorkCompleteBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpSubscriptionStatusBody(long Id, string? Status = "active", bool ConfirmWrites = false);
    private sealed record ErpCollectionsCaseStatusBody(long Id, string? Status = "new", bool ConfirmWrites = false);
    private sealed record ErpProcReqSubmitBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpProcReqDecisionBody(long Id, bool Approve = true, string? Note = null, bool ConfirmWrites = false);
    private sealed record ErpWmsLocationDeleteBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpGlManualLineBody(long CoaId, decimal Debit, decimal Credit, string? LineNote = null);
    private sealed record ErpGlManualEntryBody(IReadOnlyList<ErpGlManualLineBody>? Lines, string? Reference, string? Description, bool ConfirmWrites = false);
    private sealed record ErpGlReverseJournalBody(long JournalId, string? Note, bool ConfirmWrites = false);
    private sealed record ErpPurchaseVoidBody(long PurchaseId, string? Reason, bool ConfirmWrites = false);
    private sealed record ErpInvoiceCancelBody(long InvoiceId, string? Reason, bool ConfirmWrites = false);
    private sealed record ErpSalesOrderCancelBody(long SalesOrderId, string? Reason, bool ConfirmWrites = false);
    private sealed record ErpSalesOrderDeleteBody(long SalesOrderId, bool ConfirmWrites = false);
    private sealed record ErpPoDeleteBody(long PurchaseOrderId, bool ConfirmWrites = false);
}
