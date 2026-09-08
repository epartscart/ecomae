using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
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
            // Fleet portal counts are Super-CP registry metadata — never on tenant hosts.
            if (!SuperCpHostGate.IsAllowed(context)
                && (summary.PortalTenants != 0 || summary.ActivePortalTenants != 0))
            {
                summary = summary with
                {
                    PortalTenants = 0,
                    ActivePortalTenants = 0,
                    Message = string.IsNullOrWhiteSpace(summary.Message)
                        ? "fleet_counts_redacted_tenant_cp"
                        : summary.Message
                };
            }

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
            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound(new
                {
                    ok = false,
                    surface = "cp",
                    message = "Portal tenant fleet digest is Super CP only. Tenant CPs are independent."
                });
            }

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

        endpoints.MapGet(EcomAeRoutes.ControlPanelOrdersDetailDigest, async (
            HttpContext context,
            long orderId,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for order detail digest.");
            }

            var detail = await dashboards.GetCpOrderDetailAsync(orderId, cancellationToken);
            if (detail is null)
            {
                return Results.NotFound(new { ok = false, message = "Order not found." });
            }

            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                order = detail.Order,
                priceSum = detail.PriceSum,
                purchaseSum = detail.PurchaseSum,
                paidSum = detail.PaidSum,
                paidLeft = detail.PaidLeft,
                margin = detail.Margin,
                customerName = detail.CustomerName,
                customerEmail = detail.CustomerEmail,
                customerPhone = detail.CustomerPhone,
                items = detail.Items,
                logs = detail.Logs,
                messages = detail.Messages,
                source = detail.Source,
                message = detail.Message,
                session = SessionPayload(session),
                note = "Read-only OMS detail digest (PHP epc_orders_detail_pane markers). Writes remain PHP-authoritative."
            });
        });

        endpoints.MapPost(EcomAeRoutes.ControlPanelOmsSetItemStatus, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpOmsSetItemStatusDryRun dryRun,
            ICpOmsWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/orders", "Admin CP capability required for OMS set-item-status.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpOmsSetItemStatusBody>(context, cancellationToken) ?? new();
            var orderId = body.OrderId;
            var itemId = body.ItemId;
            var status = body.Status;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                orderId = LiveWriteFormBinder.Long(form, "orderId", "order_id");
                itemId = LiveWriteFormBinder.Long(form, "itemId", "item_id");
                status = LiveWriteFormBinder.Int(form, "status");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SetItemStatusAsync(orderId, itemId, status, session.UserId, cancellationToken);
                var dest = "/cp/orders?order_id=" + orderId.ToString(CultureInfo.InvariantCulture) + "&od=items";
                return LiveWriteFormBinder.Complete(
                    context,
                    dest,
                    written.Succeeded,
                    written.Message,
                    new
                    {
                        ok = written.Succeeded,
                        status = written.Succeeded,
                        surface = "cp",
                        writes = written.Writes,
                        writesBlocked = false,
                        phpAuthoritative = false,
                        validation_code = written.Code,
                        message = written.Message,
                        result = new { id = written.Id, order_id = orderId, item_id = itemId },
                        session = SessionPayload(session),
                    });
            }

            var result = await dryRun.EvaluateAsync(
                new CpOmsSetItemStatusRequest(orderId, itemId, status, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ControlPanelCreditLimitsSet, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpCreditLimitWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/credit-limits-app", "Admin CP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpCreditLimitSetBody>(context, cancellationToken) ?? new();
            var siteKey = body.SiteKey ?? string.Empty;
            var customerId = body.CustomerId;
            var limit = body.Limit;
            var currency = body.Currency ?? "AED";
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                siteKey = LiveWriteFormBinder.Text(form, "siteKey", "site_key");
                customerId = LiveWriteFormBinder.Int(form, "customerId", "customer_id");
                limit = LiveWriteFormBinder.Dec(form, "limit", "creditLimit", "credit_limit");
                currency = LiveWriteFormBinder.Text(form, "currency");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    ok = false,
                    status = false,
                    surface = "cp",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = false,
                    message = "Set confirmWrites=true to save the credit limit on ASP.NET.",
                    session = SessionPayload(session),
                });
            }

            var written = await writes.SetLimitAsync(siteKey, customerId, limit, currency, session.UserId, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/credit-limits-app",
                written.Succeeded,
                written.Message,
                new
                {
                    ok = written.Succeeded,
                    status = written.Succeeded,
                    surface = "cp",
                    writes = written.Writes,
                    writesBlocked = false,
                    phpAuthoritative = false,
                    validation_code = written.Code,
                    message = written.Message,
                    result = new { id = written.Id },
                    session = SessionPayload(session),
                });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ControlPanelPoApprovalsApprove, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpPoApprovalWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/po-approvals-app", "Admin CP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpPoApprovalBody>(context, cancellationToken) ?? new();
            var (poId, tier, comment, confirm) = await BindPoApprovalAsync(context, body, cancellationToken);
            if (!confirm)
            {
                return Results.Ok(new
                {
                    ok = false,
                    status = false,
                    surface = "cp",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = false,
                    message = "Set confirmWrites=true to approve the PO on ASP.NET.",
                    session = SessionPayload(session),
                });
            }

            var written = await writes.ApproveAsync(poId, tier, session.UserId, comment, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/po-approvals-app",
                written.Succeeded,
                written.Message,
                new
                {
                    ok = written.Succeeded,
                    status = written.Succeeded,
                    surface = "cp",
                    writes = written.Writes,
                    writesBlocked = false,
                    phpAuthoritative = false,
                    validation_code = written.Code,
                    message = written.Message,
                    result = new { id = written.Id },
                    session = SessionPayload(session),
                });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ControlPanelPoApprovalsReject, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpPoApprovalWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/po-approvals-app", "Admin CP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpPoApprovalBody>(context, cancellationToken) ?? new();
            var (poId, tier, reason, confirm) = await BindPoApprovalAsync(context, body, cancellationToken);
            if (!confirm)
            {
                return Results.Ok(new
                {
                    ok = false,
                    status = false,
                    surface = "cp",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = false,
                    message = "Set confirmWrites=true to reject the PO on ASP.NET.",
                    session = SessionPayload(session),
                });
            }

            var written = await writes.RejectAsync(poId, tier, session.UserId, reason, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/po-approvals-app",
                written.Succeeded,
                written.Message,
                new
                {
                    ok = written.Succeeded,
                    status = written.Succeeded,
                    surface = "cp",
                    writes = written.Writes,
                    writesBlocked = false,
                    phpAuthoritative = false,
                    validation_code = written.Code,
                    message = written.Message,
                    result = new { id = written.Id },
                    session = SessionPayload(session),
                });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ControlPanelOmsSetItemsStatus, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpOmsSetItemsStatusDryRun dryRun,
            ICpOmsWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/orders", "Admin CP capability required for OMS set-items-status.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpOmsSetItemsStatusBody>(context, cancellationToken)
                       ?? new(0, 0, [], false);
            var orderId = body.OrderId;
            var status = body.Status;
            var itemIds = body.ItemIds ?? [];
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                orderId = LiveWriteFormBinder.Long(form, "orderId", "order_id");
                status = LiveWriteFormBinder.Int(form, "status");
                itemIds = LiveWriteFormBinder.Longs(form, "itemIds", "item_ids", "itemId", "item_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SetItemsStatusAsync(orderId, status, itemIds, session.UserId, cancellationToken);
                var dest = "/cp/orders?order_id=" + orderId.ToString(CultureInfo.InvariantCulture) + "&od=items";
                return LiveWriteFormBinder.Complete(
                    context,
                    dest,
                    written.Succeeded,
                    written.Message,
                    new
                    {
                        ok = written.Succeeded,
                        status = written.Succeeded,
                        surface = "cp",
                        writes = written.Writes,
                        writesBlocked = false,
                        phpAuthoritative = false,
                        validation_code = written.Code,
                        message = written.Message,
                        result = new { id = written.Id, order_id = orderId, item_ids = itemIds },
                        session = SessionPayload(session),
                    });
            }

            var result = await dryRun.EvaluateAsync(
                new CpOmsSetItemsStatusRequest(orderId, status, itemIds, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ControlPanelOmsSendMessage, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpOmsSendMessageDryRun dryRun,
            ICpOmsWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/orders", "Admin CP capability required for OMS send-message.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpOmsSendMessageBody>(context, cancellationToken) ?? new(0, null);
            var orderId = body.OrderId;
            var text = body.Text;
            var itemId = body.ItemId ?? 0;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                orderId = LiveWriteFormBinder.Long(form, "orderId", "order_id");
                text = LiveWriteFormBinder.Text(form, "text", "message");
                itemId = LiveWriteFormBinder.Long(form, "itemId", "item_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SendMessageAsync(orderId, text ?? "", itemId, session.UserId, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/orders?order_id=" + orderId.ToString(CultureInfo.InvariantCulture) + "&od=messages",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            var result = await dryRun.EvaluateAsync(
                new CpOmsSendMessageRequest(orderId, text, itemId, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ControlPanelOmsSetCourier, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpOmsSetCourierDryRun dryRun,
            ICpOmsWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/orders", "Admin CP capability required for OMS set-courier.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpOmsSetCourierBody>(context, cancellationToken) ?? new(0, 0);
            var orderId = body.OrderId;
            var fee = body.DeliveryPrice;
            var country = body.Country;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                orderId = LiveWriteFormBinder.Long(form, "orderId", "order_id");
                fee = LiveWriteFormBinder.Dec(form, "deliveryPrice", "delivery_price");
                country = LiveWriteFormBinder.Text(form, "country");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SetCourierAsync(orderId, fee, country, session.UserId, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/orders?order_id=" + orderId.ToString(CultureInfo.InvariantCulture) + "&od=manage",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            var result = await dryRun.EvaluateAsync(
                new CpOmsSetCourierRequest(orderId, fee, country, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ControlPanelOmsDeleteOrders, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpOmsDeleteOrdersDryRun dryRun,
            ICpOmsWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/orders", "Admin CP capability required for OMS delete-orders.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpOmsDeleteOrdersBody>(context, cancellationToken) ?? new([]);
            var ids = body.OrderIds ?? [];
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
                var one = LiveWriteFormBinder.Long(form, "orderId", "order_id");
                ids = one > 0 ? [one] : ids;
            }

            if (confirm)
            {
                var written = await writes.DeleteUnpaidOrdersAsync(ids, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/orders",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            var result = await dryRun.EvaluateAsync(
                new CpOmsDeleteOrdersRequest(ids, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ControlPanelOmsAddComment, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpOmsAddCommentDryRun dryRun,
            ICpOmsWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/orders", "Admin CP capability required for OMS add-comment.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpOmsAddCommentBody>(context, cancellationToken)
                       ?? new(0, null, false);
            var orderId = body.OrderId;
            var text = body.Text;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                orderId = LiveWriteFormBinder.Long(form, "orderId", "order_id");
                text = LiveWriteFormBinder.Text(form, "text", "comment");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.AddCommentAsync(orderId, text, session.UserId, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/orders?order_id=" + orderId.ToString(CultureInfo.InvariantCulture) + "&od=timeline",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            var result = await dryRun.EvaluateAsync(
                new CpOmsAddCommentRequest(orderId, text, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ControlPanelOmsSetViewed, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpOmsSetViewedDryRun dryRun,
            ICpOmsWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/orders", "Admin CP capability required for OMS set-viewed.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpOmsSetViewedBody>(context, cancellationToken)
                       ?? new([], 1, false);
            var ids = body.OrderIds ?? [];
            var flag = body.ViewedFlag;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                ids = LiveWriteFormBinder.Longs(form, "orderIds", "order_ids", "orderId", "order_id");
                flag = LiveWriteFormBinder.Int(form, "viewedFlag", "viewed_flag");
                if (flag is not (0 or 1))
                {
                    flag = 1;
                }

                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SetViewedAsync(ids, flag, cancellationToken);
                var dest = ids.Count > 0
                    ? "/cp/orders?order_id=" + ids[0].ToString(CultureInfo.InvariantCulture)
                    : "/cp/orders";
                return LiveWriteFormBinder.Complete(
                    context,
                    dest,
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            var result = await dryRun.EvaluateAsync(
                new CpOmsSetViewedRequest(ids, flag, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ControlPanelOmsUpdateItem, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpOmsUpdateItemDryRun dryRun,
            ICpOmsWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/orders", "Admin CP capability required for OMS update-item.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpOmsUpdateItemBody>(context, cancellationToken)
                       ?? new(0, 0, null, null, null, null, null, false);
            var orderId = body.OrderId;
            var patch = new CpOmsItemWritePatch(
                body.ItemId, body.Price, body.CountNeed, body.Purchase, body.StorageId,
                body.Name, body.Manufacturer, body.Article, body.ArticleShow, body.RepriceFromWarehouse);
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                orderId = LiveWriteFormBinder.Long(form, "orderId", "order_id");
                patch = ReadOmsItemPatch(form, LiveWriteFormBinder.Long(form, "itemId", "item_id"));
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.UpdateItemAsync(orderId, patch, session.UserId, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/orders?order_id=" + orderId.ToString(CultureInfo.InvariantCulture) + "&od=items",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            var result = await dryRun.EvaluateAsync(
                new CpOmsUpdateItemRequest(
                    orderId, patch.ItemId, patch.Price, patch.CountNeed,
                    patch.Manufacturer, patch.Article, patch.StorageId, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ControlPanelOmsPayRefund, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpOmsPayRefundDryRun dryRun,
            ICpOmsWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/orders", "Admin CP capability required for OMS pay-refund.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpOmsPayRefundBody>(context, cancellationToken)
                       ?? new(0, false, null, false);
            var orderId = body.OrderId;
            var direct = body.DirectRefund;
            var paidSum = body.PaidSum;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                orderId = LiveWriteFormBinder.Long(form, "orderId", "order_id");
                direct = LiveWriteFormBinder.Flag(form, "directRefund", "direct_refund");
                var rawPaid = LiveWriteFormBinder.Dec(form, "paidSum", "paid_sum");
                paidSum = rawPaid > 0 ? rawPaid : null;
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.PayRefundAsync(orderId, direct, paidSum, session.UserId, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/orders?order_id=" + orderId.ToString(CultureInfo.InvariantCulture) + "&od=payment",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            var result = await dryRun.EvaluateAsync(
                new CpOmsPayRefundRequest(orderId, direct, paidSum, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ControlPanelOmsUpdateItems, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpOmsUpdateItemsDryRun dryRun,
            ICpOmsWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/orders", "Admin CP capability required for OMS update-items.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpOmsUpdateItemsBody>(context, cancellationToken)
                       ?? new(0, null, false);
            var orderId = body.OrderId;
            var patches = (body.Items ?? [])
                .Select(i => new CpOmsItemWritePatch(i.ItemId, i.Price, i.CountNeed, i.Purchase, i.StorageId, i.Name, i.Manufacturer, i.Article, i.ArticleShow, i.RepriceFromWarehouse))
                .ToList();
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                orderId = LiveWriteFormBinder.Long(form, "orderId", "order_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
                patches = ReadOmsItemPatches(form);
            }

            if (confirm)
            {
                var written = await writes.UpdateItemsAsync(orderId, patches, session.UserId, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/orders?order_id=" + orderId.ToString(CultureInfo.InvariantCulture) + "&od=items",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            var items = patches.Select(i => new CpOmsUpdateItemsItem(i.ItemId, i.Price, i.CountNeed)).ToList();
            var result = await dryRun.EvaluateAsync(new CpOmsUpdateItemsRequest(orderId, items, false), cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ControlPanelOmsFulfillmentSetStage, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpOmsFulfillmentSetStageDryRun dryRun,
            ICpOmsWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/orders", "Admin CP capability required for OMS fulfillment-set-stage.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpOmsFulfillmentSetStageBody>(context, cancellationToken)
                       ?? new(0, null, null, false);
            var orderId = body.OrderId;
            var key = body.SupplierKey;
            var stage = body.Stage;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                orderId = LiveWriteFormBinder.Long(form, "orderId", "order_id");
                key = LiveWriteFormBinder.Text(form, "supplierKey", "supplier_key");
                stage = LiveWriteFormBinder.Text(form, "stage");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SetFulfillmentStageAsync(orderId, key, stage, null, session.UserId, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/orders?order_id=" + orderId.ToString(CultureInfo.InvariantCulture) + "&od=fulfillment",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            var result = await dryRun.EvaluateAsync(
                new CpOmsFulfillmentSetStageRequest(orderId, key, stage, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ControlPanelOmsFulfillmentAdvance, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpOmsFulfillmentAdvanceDryRun dryRun,
            ICpOmsWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/orders", "Admin CP capability required for OMS fulfillment-advance.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpOmsFulfillmentAdvanceBody>(context, cancellationToken)
                       ?? new(0, null, false);
            var orderId = body.OrderId;
            var key = body.SupplierKey;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                orderId = LiveWriteFormBinder.Long(form, "orderId", "order_id");
                key = LiveWriteFormBinder.Text(form, "supplierKey", "supplier_key");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.AdvanceFulfillmentAsync(orderId, key, session.UserId, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/orders?order_id=" + orderId.ToString(CultureInfo.InvariantCulture) + "&od=fulfillment",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            var result = await dryRun.EvaluateAsync(
                new CpOmsFulfillmentAdvanceRequest(orderId, key, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ControlPanelOmsRefreshItemCost, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpOmsRefreshItemCostDryRun dryRun,
            ICpOmsWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/orders", "Admin CP capability required for OMS refresh-item-cost.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpOmsRefreshItemCostBody>(context, cancellationToken)
                       ?? new(0, 0, false);
            var orderId = body.OrderId;
            var itemId = body.ItemId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                orderId = LiveWriteFormBinder.Long(form, "orderId", "order_id");
                itemId = LiveWriteFormBinder.Long(form, "itemId", "item_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.RefreshItemCostAsync(orderId, itemId, session.UserId, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/orders?order_id=" + orderId.ToString(CultureInfo.InvariantCulture) + "&od=items",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            var result = await dryRun.EvaluateAsync(
                new CpOmsRefreshItemCostRequest(orderId, itemId, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.CpReturnAction, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpReturnActionDryRun dryRun,
            ICpReturnWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/returns-rma-app", "Admin CP capability required for return action.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpReturnActionBody>(context, cancellationToken)
                       ?? new(0, null, false);
            var returnId = body.ReturnId;
            var action = body.Action;
            var statusId = body.StatusId;
            var lineId = body.LineId;
            var decide = body.Decide;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                returnId = LiveWriteFormBinder.Long(form, "returnId", "return_id");
                action = LiveWriteFormBinder.Text(form, "action");
                statusId = LiveWriteFormBinder.Int(form, "statusId", "status_id");
                lineId = LiveWriteFormBinder.Long(form, "lineId", "line_id");
                decide = LiveWriteFormBinder.Int(form, "decide");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var key = (action ?? string.Empty).Trim();
                ErpSimpleWriteResult written = key switch
                {
                    "set_return_status" or "set-status" or "status" =>
                        await writes.SetStatusAsync(returnId, statusId, cancellationToken),
                    "decide_line" or "decide-line" or "decide" =>
                        await writes.DecideLineAsync(returnId, lineId, decide, session.UserId, cancellationToken),
                    "finalize_return" or "finalize" =>
                        await writes.FinalizeAsync(returnId, session.UserId, cancellationToken),
                    _ => ErpSimpleWriteResult.Fail("invalid", "Unknown action."),
                };
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/returns-rma-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new CpReturnActionRequest(returnId, action, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpSetUsersVinViewed, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpSetUsersVinViewedDryRun dryRun,
            ICpUserWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/system-requests-app", "Admin CP capability required for VIN viewed.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpSetUsersVinViewedBody>(context, cancellationToken)
                       ?? new(0, false);
            var ids = body.RequestId > 0 ? new List<long> { body.RequestId } : [];
            var flag = body.ViewedFlag;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                ids = LiveWriteFormBinder.Longs(form, "requestIds", "request_ids", "requestId", "request_id", "vins").ToList();
                flag = LiveWriteFormBinder.Int(form, "viewedFlag", "viewed_flag", "viewed");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SetVinViewedAsync(ids, flag, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/system-requests-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new CpSetUsersVinViewedRequest(ids.FirstOrDefault(), false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpSetUserComment, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpSetUserCommentDryRun dryRun,
            ICpUserWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/users-app", "Admin CP capability required for set-user-comment.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpSetUserCommentBody>(context, cancellationToken)
                       ?? new(0, null, false);
            var userId = body.UserId;
            var comment = body.Comment;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                userId = LiveWriteFormBinder.Long(form, "userId", "user_id");
                comment = LiveWriteFormBinder.Text(form, "comment");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SetCommentAsync(userId, comment, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/users-app?user_id=" + userId.ToString(CultureInfo.InvariantCulture) + "&tab=profile",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new CpSetUserCommentRequest(userId, comment, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpSetUserUnlocked, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpUserWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/users-app", "Admin CP capability required for set-user-unlocked.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpSetUserUnlockedBody>(context, cancellationToken)
                       ?? new(0, -1, false);
            var userId = body.UserId;
            var unlocked = body.Unlocked;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                userId = LiveWriteFormBinder.Long(form, "userId", "user_id");
                unlocked = LiveWriteFormBinder.Int(form, "unlocked", "unlockedFlag", "unlocked_flag", "unlock_user");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SetUnlockedAsync(userId, unlocked, session.UserId, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/users-app?user_id=" + userId.ToString(CultureInfo.InvariantCulture) + "&tab=profile",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            return Results.Ok(new
            {
                status = "dry-run",
                writes = 0,
                writesBlocked = true,
                phpAuthoritative = true,
                validation_code = "dry_run",
                message = "Set confirmWrites=true to lock or unlock the user on ASP.NET.",
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpUsersCreate, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpUserWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/users-app", "Admin CP capability required for user create.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpUsersCreateBody>(context, cancellationToken) ?? new();
            var email = body.Email;
            var emailConfirmed = body.EmailConfirmed;
            var phone = body.Phone;
            var phoneConfirmed = body.PhoneConfirmed;
            var password = body.Password;
            var unlocked = body.Unlocked;
            var regVariant = body.RegVariant;
            var fieldsJson = body.FieldsJson ?? body.Fields;
            var groupsJson = body.GroupsJson ?? body.Groups;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                email = LiveWriteFormBinder.Text(form, "email");
                emailConfirmed = LiveWriteFormBinder.Int(form, "emailConfirmed", "email_confirmed");
                phone = LiveWriteFormBinder.Text(form, "phone");
                phoneConfirmed = LiveWriteFormBinder.Int(form, "phoneConfirmed", "phone_confirmed");
                password = LiveWriteFormBinder.Text(form, "password");
                unlocked = LiveWriteFormBinder.Int(form, "unlocked");
                if (!form.ContainsKey("unlocked") && !form.ContainsKey("Unlocked"))
                {
                    unlocked = 1;
                }

                regVariant = LiveWriteFormBinder.Int(form, "regVariant", "reg_variant");
                fieldsJson = LiveWriteFormBinder.Text(form, "fieldsJson", "fields_json", "fields");
                groupsJson = LiveWriteFormBinder.Text(form, "groupsJson", "groups_json", "groups");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to create the user on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.CreateAsync(
                email,
                emailConfirmed,
                phone,
                phoneConfirmed,
                password,
                unlocked,
                regVariant,
                fieldsJson,
                groupsJson,
                cancellationToken);
            var returnUrl = written.Succeeded && written.Id > 0
                ? "/cp/users-app?user_id=" + written.Id.ToString(CultureInfo.InvariantCulture) + "&tab=profile"
                : "/cp/users-app";
            return LiveWriteFormBinder.Complete(
                context,
                returnUrl,
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpUsersSetPassword, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpUserWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/users-app", "Admin CP capability required for set-password.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpUsersSetPasswordBody>(context, cancellationToken) ?? new();
            var userId = body.UserId;
            var password = body.Password;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                userId = LiveWriteFormBinder.Long(form, "userId", "user_id");
                password = LiveWriteFormBinder.Text(form, "password");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to update the password on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.SetPasswordAsync(userId, password, session.SessionId, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/users-app?user_id=" + userId.ToString(CultureInfo.InvariantCulture) + "&tab=profile",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpPricesImportCsv, async (
            HttpContext context,
            CpPricesImportCsvBody? body,
            ILegacySessionValidator validator,
            ICpPricesImportCsvDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin)
                return Unauthorized("Admin session required.");
            body ??= new CpPricesImportCsvBody(0,false);
            return Results.Ok(dryRun.Evaluate(new CpPricesImportCsvRequest(body.SessionId, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });
        endpoints.MapPost(EcomAeRoutes.CpPricesCompleteSession, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpPricesCompleteSessionDryRun dryRun,
            ICpPricesUploadWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/prices-upload-app", "Admin CP capability required for price session complete.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpPricesCompleteSessionBody>(context, cancellationToken) ?? new();
            var priceId = body.PriceId > 0 ? body.PriceId : body.SessionId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                priceId = LiveWriteFormBinder.Long(form, "priceId", "price_id", "sessionId", "session_id", "id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new CpPricesCompleteSessionRequest(priceId, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.CompleteSessionAsync(priceId, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/prices-upload-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.CpCreateSitemap, async (HttpContext context, CpCreateSitemapBody? body, ILegacySessionValidator validator, ICpCreateSitemapDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required.");
            body ??= new CpCreateSitemapBody(null, false);
            return Results.Ok(dryRun.Evaluate(new CpCreateSitemapRequest(body.Action, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.CpLangSetIsCustom, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpLangSetIsCustomDryRun dryRun,
            ICpLangWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/languages-app", "Admin CP capability required for lang is_custom.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpLangSetIsCustomBody>(context, cancellationToken)
                       ?? new();
            var strKey = body.StrKey;
            var flag = body.IsCustom;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                strKey = LiveWriteFormBinder.Text(form, "strKey", "str_key");
                flag = LiveWriteFormBinder.Int(form, "isCustom", "is_custom");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SetIsCustomAsync(strKey, flag, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/languages-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new CpLangSetIsCustomRequest(body.Action, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.CpPosOpenSession, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpPosOpenSessionDryRun dryRun,
            ICpPosWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/pos-overview-app", "Admin CP capability required for POS open-session.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpPosOpenSessionBody>(context, cancellationToken)
                       ?? new();
            var openingFloat = body.OpeningFloat;
            var registerName = body.RegisterName;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                openingFloat = LiveWriteFormBinder.Dec(form, "openingFloat", "opening_float");
                registerName = LiveWriteFormBinder.Text(form, "registerName", "register_name");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.OpenSessionAsync(openingFloat, session.UserId, registerName, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/pos-overview-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new CpPosOpenSessionRequest(body.Action, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpPosCloseSession, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpPosCloseSessionDryRun dryRun,
            ICpPosWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/pos-overview-app", "Admin CP capability required for POS close-session.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpPosCloseSessionBody>(context, cancellationToken)
                       ?? new();
            var sessionId = body.SessionId;
            var closingCash = body.ClosingCash;
            var notes = body.Notes;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                sessionId = LiveWriteFormBinder.Long(form, "sessionId", "session_id");
                closingCash = LiveWriteFormBinder.Dec(form, "closingCash", "closing_cash");
                notes = LiveWriteFormBinder.Text(form, "notes");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.CloseSessionAsync(sessionId, closingCash, notes, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/pos-overview-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new CpPosCloseSessionRequest(body.Action, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpPosCompleteSale, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpPosCompleteSaleDryRun dryRun,
            ICpPosWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/pos-overview-app", "Admin CP capability required for POS complete-sale.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpPosCompleteSaleBody>(context, cancellationToken)
                       ?? new();
            var sessionId = body.SessionId;
            var linesJson = body.Lines.ValueKind == JsonValueKind.Array
                ? body.Lines.GetRawText()
                : body.LinesJson;
            var paymentMethod = body.PaymentMethod;
            var cashAmount = body.CashAmount;
            var cardAmount = body.CardAmount;
            var taxRate = body.TaxRate;
            var taxKitCode = body.TaxKitCode;
            var customerUserId = body.CustomerUserId;
            var contactId = body.ContactId;
            var customerLabel = body.CustomerLabel;
            var saleNotes = body.SaleNotes;
            var lineName = body.Name;
            var lineQty = body.Qty;
            var linePrice = body.UnitPriceEx;
            var lineSku = body.Sku;
            var warehouseId = body.WarehouseId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                sessionId = LiveWriteFormBinder.Long(form, "sessionId", "session_id");
                linesJson = LiveWriteFormBinder.Text(form, "lines", "linesJson", "lines_json");
                paymentMethod = LiveWriteFormBinder.Text(form, "paymentMethod", "payment_method");
                cashAmount = LiveWriteFormBinder.Dec(form, "cashAmount", "cash_amount");
                cardAmount = LiveWriteFormBinder.Dec(form, "cardAmount", "card_amount");
                taxRate = LiveWriteFormBinder.Dec(form, "taxRate", "tax_rate");
                taxKitCode = LiveWriteFormBinder.Text(form, "taxKitCode", "tax_kit_code");
                customerUserId = LiveWriteFormBinder.Long(form, "customerUserId", "customer_user_id");
                contactId = LiveWriteFormBinder.Long(form, "contactId", "contact_id");
                customerLabel = LiveWriteFormBinder.Text(form, "customerLabel", "customer_label");
                saleNotes = LiveWriteFormBinder.Text(form, "saleNotes", "sale_notes", "notes");
                lineName = LiveWriteFormBinder.Text(form, "name", "lineName", "line_name");
                lineQty = LiveWriteFormBinder.Dec(form, "qty", "lineQty", "line_qty");
                linePrice = LiveWriteFormBinder.Dec(form, "unitPriceEx", "unit_price_ex", "price");
                lineSku = LiveWriteFormBinder.Text(form, "sku");
                warehouseId = LiveWriteFormBinder.Long(form, "warehouseId", "warehouse_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            var parsedLines = CpPosWriteService.ParseLinesJson(linesJson);
            if (parsedLines.Count == 0 && !string.IsNullOrWhiteSpace(lineName))
            {
                parsedLines = [new CpPosSaleLineInput(lineName, lineQty <= 0 ? 1 : lineQty, linePrice, Sku: lineSku, Price: linePrice)];
            }

            if (confirm)
            {
                var written = await writes.CompleteSaleAsync(
                    new CpPosCompleteSaleWriteRequest(
                        sessionId,
                        parsedLines,
                        paymentMethod,
                        cashAmount,
                        cardAmount,
                        taxRate,
                        taxKitCode,
                        customerUserId,
                        contactId,
                        customerLabel,
                        saleNotes,
                        warehouseId),
                    session.UserId,
                    cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/pos-overview-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new CpPosCompleteSaleRequest(body.Action, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpPosSaveSettings, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpPosSaveSettingsDryRun dryRun,
            ICpPosWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/pos-overview-app", "Admin CP capability required for POS save-settings.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpPosSaveSettingsBody>(context, cancellationToken)
                       ?? new();
            var posEnabled = body.PosEnabled;
            var registerName = body.RegisterName;
            var defaultWarehouseId = body.DefaultWarehouseId;
            var defaultCashAccountId = body.DefaultCashAccountId;
            var defaultCardAccountId = body.DefaultCardAccountId;
            var receiptHeader = body.ReceiptHeader;
            var receiptFooter = body.ReceiptFooter;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                posEnabled = LiveWriteFormBinder.Flag(form, "posEnabled", "pos_enabled");
                registerName = LiveWriteFormBinder.Text(form, "registerName", "register_name");
                defaultWarehouseId = LiveWriteFormBinder.Int(form, "defaultWarehouseId", "default_warehouse_id");
                defaultCashAccountId = LiveWriteFormBinder.Int(form, "defaultCashAccountId", "default_cash_account_id");
                defaultCardAccountId = LiveWriteFormBinder.Int(form, "defaultCardAccountId", "default_card_account_id");
                receiptHeader = LiveWriteFormBinder.Text(form, "receiptHeader", "receipt_header");
                receiptFooter = LiveWriteFormBinder.Text(form, "receiptFooter", "receipt_footer");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SaveSettingsAsync(
                    posEnabled,
                    registerName,
                    defaultWarehouseId,
                    defaultCashAccountId,
                    defaultCardAccountId,
                    receiptHeader,
                    receiptFooter,
                    cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/pos-overview-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new CpPosSaveSettingsRequest(body.Action, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapMethods(EcomAeRoutes.CpPosSearchProducts, ["GET", "POST"], async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpPosWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Results.Json(new { status = false, message = "Access denied" }, statusCode: 401);
            }

            var q = context.Request.Query["q"].ToString();
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                q = LiveWriteFormBinder.Text(form, "q", "query");
            }
            else if (string.IsNullOrWhiteSpace(q) && context.Request.HasJsonContentType())
            {
                var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpPosSearchBody>(context, cancellationToken);
                q = body?.Q;
            }

            var products = await writes.SearchProductsAsync(q, 30, cancellationToken);
            return Results.Ok(new { status = true, products });
        }).DisableAntiforgery();
        endpoints.MapMethods(EcomAeRoutes.CpPosSearchCustomers, ["GET", "POST"], async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpPosWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Results.Json(new { status = false, message = "Access denied" }, statusCode: 401);
            }

            var q = context.Request.Query["q"].ToString();
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                q = LiveWriteFormBinder.Text(form, "q", "query");
            }
            else if (string.IsNullOrWhiteSpace(q) && context.Request.HasJsonContentType())
            {
                var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpPosSearchBody>(context, cancellationToken);
                q = body?.Q;
            }

            var customers = await writes.SearchCustomersAsync(q, 15, cancellationToken);
            return Results.Ok(new { status = true, customers });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpPosCalcCart, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpPosWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Results.Json(new { status = false, message = "Access denied" }, statusCode: 401);
            }

            var linesJson = "";
            long customerUserId = 0;
            long contactId = 0;
            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpPosCalcCartBody>(context, cancellationToken)
                       ?? new();
            linesJson = body.Lines.ValueKind == JsonValueKind.Array ? body.Lines.GetRawText() : body.LinesJson;
            customerUserId = body.CustomerUserId;
            contactId = body.ContactId;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                linesJson = LiveWriteFormBinder.Text(form, "lines", "linesJson", "lines_json");
                customerUserId = LiveWriteFormBinder.Long(form, "customerUserId", "customer_user_id");
                contactId = LiveWriteFormBinder.Long(form, "contactId", "contact_id");
            }

            var totals = await writes.CalcCartAsync(
                CpPosWriteService.ParseLinesJson(linesJson),
                customerUserId,
                contactId,
                cancellationToken);
            return Results.Ok(new
            {
                status = totals.Ok,
                message = totals.Message,
                totals = new
                {
                    lines = totals.Lines,
                    subtotal_ex = totals.SubtotalEx,
                    discount_total = totals.DiscountTotal,
                    amount_ex_vat = totals.AmountExVat,
                    vat_amount = totals.VatAmount,
                    total_amount = totals.TotalAmount,
                    tax_rate = totals.TaxRate,
                    tax_label = totals.TaxLabel,
                    kit_code = totals.KitCode,
                },
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpPortalSaveSettings, async (HttpContext context, CpPortalSaveSettingsBody? body, ILegacySessionValidator validator, ICpPortalSaveSettingsDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new CpPortalSaveSettingsBody(null,false); return Results.Ok(dryRun.Evaluate(new CpPortalSaveSettingsRequest(body.Action, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.CpPortalDeploySite, async (HttpContext context, CpPortalDeploySiteBody? body, ILegacySessionValidator validator, ICpPortalDeploySiteDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new CpPortalDeploySiteBody(null,false); return Results.Ok(dryRun.Evaluate(new CpPortalDeploySiteRequest(body.Action, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.CpCrmAction, async (HttpContext context, CpCrmActionBody? body, ILegacySessionValidator validator, ICpCrmActionDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new CpCrmActionBody(null,false); return Results.Ok(dryRun.Evaluate(new CpCrmActionRequest(body.Action, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });

        endpoints.MapGet(EcomAeRoutes.CpModuleAjaxWriteCatalog, (ICpModuleAjaxWriteCatalog catalog) => Results.Ok(catalog.BuildReport()));
        endpoints.MapPost(EcomAeRoutes.CpModuleAjaxWriteRegistryDryRun, async (
            string module,
            string action,
            CpModuleAjaxWriteRegistryBody? body,
            HttpContext context,
            ILegacySessionValidator validator,
            ICpModuleAjaxWriteRegistryDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin)
                return Unauthorized("Admin session required for CP module ajax registry dry-run.");
            body ??= new CpModuleAjaxWriteRegistryBody(false);
            return Results.Ok(dryRun.Evaluate(new CpModuleAjaxWriteRegistryRequest(module, action, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });
        endpoints.MapPost(EcomAeRoutes.CpModuleAjaxWriteDedicatedDryRun, async (
            string module,
            string action,
            CpModuleAjaxWriteDedicatedBody? body,
            HttpContext context,
            ILegacySessionValidator validator,
            ICpModuleAjaxWriteDedicatedDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin)
                return Unauthorized("Admin session required for CP module ajax dedicated dry-run.");
            body ??= new CpModuleAjaxWriteDedicatedBody(false);
            return Results.Ok(dryRun.Evaluate(new CpModuleAjaxWriteDedicatedRequest(module, action, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.CpLangSetIsError, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpLangSetIsErrorDryRun dryRun,
            ICpLangWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/languages-app", "Admin CP capability required for lang is_error.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpLangSetIsErrorBody>(context, cancellationToken)
                       ?? new();
            var strKey = body.StrKey;
            var flag = body.IsError;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                strKey = LiveWriteFormBinder.Text(form, "strKey", "str_key");
                flag = LiveWriteFormBinder.Int(form, "isError", "is_error");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SetIsErrorAsync(strKey, flag, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/languages-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new CpLangSetIsErrorRequest(body.Action, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpLangSetSame, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpLangSetSameDryRun dryRun,
            ICpLangWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/languages-app", "Admin CP capability required for lang same.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpLangSetSameBody>(context, cancellationToken)
                       ?? new();
            var strKey = body.StrKey;
            var same = body.Same;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                strKey = LiveWriteFormBinder.Text(form, "strKey", "str_key");
                same = LiveWriteFormBinder.Text(form, "same");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SetSameAsync(strKey, same, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/languages-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new CpLangSetSameRequest(body.Action, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpLangSetUsedFound, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpLangSetUsedFoundDryRun dryRun,
            ICpLangWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/languages-app", "Admin CP capability required for lang used_found.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpLangSetUsedFoundBody>(context, cancellationToken)
                       ?? new();
            var strKey = body.StrKey;
            var flag = body.UsedFound;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                strKey = LiveWriteFormBinder.Text(form, "strKey", "str_key");
                flag = LiveWriteFormBinder.Int(form, "usedFound", "used_found");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SetUsedFoundAsync(strKey, flag, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/languages-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new CpLangSetUsedFoundRequest(body.Action, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpLangSearchUsedFound, async (HttpContext context, CpLangSearchUsedFoundBody? body, ILegacySessionValidator validator, ICpLangSearchUsedFoundDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new CpLangSearchUsedFoundBody(null,false); return Results.Ok(dryRun.Evaluate(new CpLangSearchUsedFoundRequest(body.Action, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.CpVersionGetUpdatePack, async (HttpContext context, CpVersionGetUpdatePackBody? body, ILegacySessionValidator validator, ICpVersionGetUpdatePackDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new CpVersionGetUpdatePackBody(null,false); return Results.Ok(dryRun.Evaluate(new CpVersionGetUpdatePackRequest(body.Action, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.CpLangSaveTranslation, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpLangSaveTranslationDryRun dryRun,
            ICpLangWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/languages-app", "Admin CP capability required for lang save-translation.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpLangSaveTranslationBody>(context, cancellationToken)
                       ?? new();
            var strKey = body.StrKey;
            var langCode = body.LangCode;
            var value = body.Value;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                strKey = LiveWriteFormBinder.Text(form, "strKey", "str_key");
                langCode = LiveWriteFormBinder.Text(form, "langCode", "lang_code");
                value = LiveWriteFormBinder.Text(form, "value");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SaveTranslationAsync(strKey, langCode, value, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/languages-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new CpLangSaveTranslationRequest(body.Action, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpLangSaveDescription, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpLangSaveDescriptionDryRun dryRun,
            ICpLangWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/languages-app", "Admin CP capability required for lang save-description.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpLangSaveDescriptionBody>(context, cancellationToken)
                       ?? new();
            var strKey = body.StrKey;
            var value = body.Value;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                strKey = LiveWriteFormBinder.Text(form, "strKey", "str_key");
                value = LiveWriteFormBinder.Text(form, "value");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SaveDescriptionAsync(strKey, value, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/languages-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new CpLangSaveDescriptionRequest(body.Action, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpLangCreateString, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpLangCreateStringDryRun dryRun,
            ICpLangWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/languages-app", "Admin CP capability required for lang create-string.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpLangCreateStringBody>(context, cancellationToken)
                       ?? new();
            var description = body.Description;
            var same = body.Same;
            var isError = body.IsError;
            var isCustom = body.IsCustom;
            var usedFound = body.UsedFound;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                description = LiveWriteFormBinder.Text(form, "description");
                same = LiveWriteFormBinder.Text(form, "same");
                isError = LiveWriteFormBinder.Int(form, "isError", "is_error");
                isCustom = LiveWriteFormBinder.Int(form, "isCustom", "is_custom");
                usedFound = LiveWriteFormBinder.Int(form, "usedFound", "used_found");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var host = context.Request.Host.Host;
                var domainPath = string.IsNullOrWhiteSpace(host) ? "http://localhost/" : "http://" + host + "/";
                var written = await writes.CreateStringAsync(
                    new CpLangCreateStringWriteRequest(description, same, isError, isCustom, usedFound, domainPath),
                    cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/languages-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new CpLangCreateStringRequest(body.Action, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpLangDeleteNotUsed, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpLangDeleteNotUsedDryRun dryRun,
            ICpLangWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/languages-app", "Admin CP capability required for lang delete-not-used.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpLangDeleteNotUsedBody>(context, cancellationToken)
                       ?? new();
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.DeleteUnusedCustomAsync(cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/languages-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new CpLangDeleteNotUsedRequest(body.Action, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpPacksDelete, async (HttpContext context, CpPacksDeleteBody? body, ILegacySessionValidator validator, ICpPacksDeleteDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required.");
            body ??= new CpPacksDeleteBody(null, false);
            return Results.Ok(dryRun.Evaluate(new CpPacksDeleteRequest(body.Action, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });
        endpoints.MapPost(EcomAeRoutes.CpChannelsWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpChannelsWriteDryRun dryRun,
            ICpChannelWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/marketplace-channels-app", "Admin CP capability required for channel write.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpChannelsWriteBody>(context, cancellationToken)
                       ?? new();
            var action = body.Action;
            var code = body.Code;
            var enabled = body.Enabled;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                code = LiveWriteFormBinder.Text(form, "code", "channel_code", "channelCode");
                enabled = LiveWriteFormBinder.IntOrNull(form, "enabled", "active");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            var key = (action ?? string.Empty).Trim();
            if (confirm && key is "toggle_channel" or "toggle" or "toggle-channel")
            {
                var written = await writes.ToggleAsync(code, enabled, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/marketplace-channels-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new CpChannelsWriteRequest(action, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpLogisticsWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpLogisticsWriteDryRun dryRun,
            ICpLogisticsWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/carriers-app", "Admin CP capability required for logistics write.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpLogisticsWriteBody>(context, cancellationToken)
                       ?? new();
            var action = body.Action;
            var code = body.Code;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                code = LiveWriteFormBinder.Text(form, "code", "carrier_code", "carrierCode");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            var key = (action ?? string.Empty).Trim();
            if (confirm && key is "toggle_carrier" or "toggle" or "toggle-carrier")
            {
                var written = await writes.ToggleCarrierAsync(code, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/carriers-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new CpLogisticsWriteRequest(action, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpPaymentsWrite, async (HttpContext context, CpPaymentsWriteBody? body, ILegacySessionValidator validator, ICpPaymentsWriteDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required.");
            body ??= new CpPaymentsWriteBody(null, false);
            return Results.Ok(dryRun.Evaluate(new CpPaymentsWriteRequest(body.Action, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });
        endpoints.MapPost(EcomAeRoutes.CpWorkshopWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpWorkshopWriteDryRun dryRun,
            ICpWorkshopWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/workshop-app", "Admin CP capability required for workshop write.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpWorkshopWriteBody>(context, cancellationToken)
                       ?? new();
            var action = body.Action;
            var jobId = body.JobId;
            var bayId = body.BayId;
            var techId = body.TechId;
            var id = body.Id;
            var code = body.Code;
            var name = body.Name;
            var phone = body.Phone;
            var skill = body.Skill;
            var status = body.Status;
            var active = body.Active;
            var sortOrder = body.SortOrder;
            var jobNo = body.JobNo;
            var customerName = body.CustomerName;
            var customerPhone = body.CustomerPhone;
            var customerEmail = body.CustomerEmail;
            var customerId = body.CustomerId;
            var plate = body.Plate;
            var vin = body.Vin;
            var make = body.Make;
            var model = body.Model;
            var year = body.Year;
            var odometer = body.Odometer;
            var complaint = body.Complaint;
            var estimateApproved = body.EstimateApproved;
            var underWarranty = body.UnderWarranty;
            var notes = body.Notes;
            var timePromised = body.TimePromised;
            var labourDesc = body.LabourDesc;
            var labourHours = body.LabourHours;
            var labourRate = body.LabourRate;
            var partDesc = body.PartDesc;
            var partQty = body.PartQty;
            var partPrice = body.PartPrice;
            var lineType = body.LineType;
            var description = body.Description;
            var itemId = body.ItemId;
            var qty = body.Qty;
            var unitPrice = body.UnitPrice;
            var taxPercent = body.TaxPercent;
            var chargeable = body.Chargeable;
            var refNo = body.RefNo;
            var garageId = body.GarageId;
            var serviceType = body.ServiceType;
            var timeSlot = body.TimeSlot;
            var appointmentId = body.AppointmentId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                jobId = LiveWriteFormBinder.Long(form, "jobId", "job_id");
                bayId = LiveWriteFormBinder.Long(form, "bayId", "bay_id");
                techId = LiveWriteFormBinder.Long(form, "techId", "tech_id");
                id = LiveWriteFormBinder.Long(form, "id");
                code = LiveWriteFormBinder.Text(form, "code");
                name = LiveWriteFormBinder.Text(form, "name");
                phone = LiveWriteFormBinder.Text(form, "phone");
                skill = LiveWriteFormBinder.Text(form, "skill");
                status = LiveWriteFormBinder.Text(form, "status");
                active = LiveWriteFormBinder.Int(form, "active");
                sortOrder = LiveWriteFormBinder.Int(form, "sortOrder", "sort_order");
                jobNo = LiveWriteFormBinder.Text(form, "jobNo", "job_no");
                customerName = LiveWriteFormBinder.Text(form, "customerName", "customer_name");
                customerPhone = LiveWriteFormBinder.Text(form, "customerPhone", "customer_phone");
                customerEmail = LiveWriteFormBinder.Text(form, "customerEmail", "customer_email");
                customerId = LiveWriteFormBinder.Long(form, "customerId", "customer_id");
                plate = LiveWriteFormBinder.Text(form, "plate");
                vin = LiveWriteFormBinder.Text(form, "vin");
                make = LiveWriteFormBinder.Text(form, "make");
                model = LiveWriteFormBinder.Text(form, "model");
                year = LiveWriteFormBinder.Text(form, "year");
                odometer = LiveWriteFormBinder.Int(form, "odometer");
                complaint = LiveWriteFormBinder.Text(form, "complaint");
                estimateApproved = LiveWriteFormBinder.Flag(form, "estimateApproved", "estimate_approved");
                underWarranty = LiveWriteFormBinder.Flag(form, "underWarranty", "under_warranty");
                notes = LiveWriteFormBinder.Text(form, "notes");
                timePromised = LiveWriteFormBinder.Long(form, "timePromised", "time_promised");
                labourDesc = LiveWriteFormBinder.Text(form, "labourDesc", "labour_desc");
                labourHours = LiveWriteFormBinder.Dec(form, "labourHours", "labour_hours");
                labourRate = LiveWriteFormBinder.Dec(form, "labourRate", "labour_rate");
                partDesc = LiveWriteFormBinder.Text(form, "partDesc", "part_desc");
                partQty = LiveWriteFormBinder.Dec(form, "partQty", "part_qty");
                partPrice = LiveWriteFormBinder.Dec(form, "partPrice", "part_price");
                lineType = LiveWriteFormBinder.Text(form, "lineType", "line_type");
                description = LiveWriteFormBinder.Text(form, "description");
                itemId = LiveWriteFormBinder.Long(form, "itemId", "item_id");
                qty = LiveWriteFormBinder.Dec(form, "qty");
                unitPrice = LiveWriteFormBinder.Dec(form, "unitPrice", "unit_price");
                taxPercent = LiveWriteFormBinder.Dec(form, "taxPercent", "tax_percent");
                chargeable = LiveWriteFormBinder.IntOrNull(form, "chargeable") ?? 1;
                refNo = LiveWriteFormBinder.Text(form, "refNo", "ref_no");
                garageId = LiveWriteFormBinder.Long(form, "garageId", "garage_id");
                serviceType = LiveWriteFormBinder.Text(form, "serviceType", "service_type");
                timeSlot = LiveWriteFormBinder.Long(form, "timeSlot", "time_slot");
                appointmentId = LiveWriteFormBinder.Long(form, "appointmentId", "appointment_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var key = (action ?? string.Empty).Trim();
                ErpSimpleWriteResult written = key switch
                {
                    "assign" => await writes.AssignAsync(jobId, bayId, techId, cancellationToken),
                    "save_bay" or "save-bay" => await writes.SaveBayAsync(id, code, name, active, sortOrder, cancellationToken),
                    "save_tech" or "save-tech" => await writes.SaveTechAsync(id, name, phone, skill, active, cancellationToken),
                    "set_status" or "set-status" => await writes.SetStatusAsync(jobId, status, cancellationToken),
                    "create_job" or "create-job" => await writes.CreateJobAsync(
                        new CpWorkshopCreateJobRequest(
                            jobNo, status, customerName, customerPhone, customerEmail, customerId,
                            plate, vin, make, model, year, odometer, complaint, bayId, techId,
                            estimateApproved, underWarranty, notes, timePromised,
                            labourDesc, labourHours <= 0 ? 1 : labourHours, labourRate <= 0 ? 150 : labourRate,
                            partDesc, partQty <= 0 ? 1 : partQty, partPrice),
                        cancellationToken),
                    "add_line" or "add-line" => await writes.AddLineAsync(
                        new CpWorkshopAddLineRequest(
                            jobId, lineType, description, itemId,
                            qty <= 0 ? 1 : qty, unitPrice, taxPercent <= 0 ? 5 : taxPercent,
                            chargeable == 0 ? 0 : 1),
                        cancellationToken),
                    "create_appointment" or "create-appointment" => await writes.CreateAppointmentAsync(
                        new CpWorkshopCreateAppointmentRequest(
                            refNo, status, customerName, customerPhone, customerEmail, customerId,
                            garageId, plate, make, model, year, serviceType, notes, timeSlot),
                        cancellationToken),
                    "convert_appointment" or "convert-appointment" => await writes.ConvertAppointmentAsync(appointmentId, cancellationToken),
                    _ => ErpSimpleWriteResult.Fail("invalid", "Unknown workshop action. assign / save_bay / save_tech / set_status / create_job / add_line / create_appointment / convert_appointment are live; seed stays PHP."),
                };
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/workshop-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new CpWorkshopWriteRequest(action, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpCollectionsDunningWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpCollectionsDunningWriteDryRun dryRun,
            ICpCollectionsDunningWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/collections-dunning-app", "Admin CP capability required for dunning queue write.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpCollectionsDunningWriteBody>(context, cancellationToken)
                       ?? new();
            var action = body.Action;
            var queueId = body.QueueId;
            var status = body.Status;
            var notes = body.Notes;
            var amount = body.Amount;
            var siteKey = body.SiteKey;
            var name = body.Name;
            var stepsJson = body.StepsJson;
            var customerId = body.CustomerId;
            var customerName = body.CustomerName;
            var invoiceRef = body.InvoiceRef;
            var invoiceAmount = body.InvoiceAmount;
            var amountDue = body.AmountDue;
            var dueDate = body.DueDate;
            var profileId = body.ProfileId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                queueId = LiveWriteFormBinder.Long(form, "queueId", "queue_id", "id");
                status = LiveWriteFormBinder.Text(form, "status");
                notes = LiveWriteFormBinder.Text(form, "notes");
                amount = LiveWriteFormBinder.Dec(form, "amount");
                siteKey = LiveWriteFormBinder.Text(form, "siteKey", "site_key");
                name = LiveWriteFormBinder.Text(form, "name");
                stepsJson = LiveWriteFormBinder.Text(form, "stepsJson", "steps_json", "steps");
                customerId = LiveWriteFormBinder.Long(form, "customerId", "customer_id");
                customerName = LiveWriteFormBinder.Text(form, "customerName", "customer_name");
                invoiceRef = LiveWriteFormBinder.Text(form, "invoiceRef", "invoice_ref");
                invoiceAmount = LiveWriteFormBinder.Dec(form, "invoiceAmount", "invoice_amount");
                amountDue = LiveWriteFormBinder.DecOrNull(form, "amountDue", "amount_due");
                dueDate = LiveWriteFormBinder.Text(form, "dueDate", "due_date");
                profileId = LiveWriteFormBinder.Long(form, "profileId", "profile_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (string.IsNullOrWhiteSpace(siteKey)
                && context.Items[TenantResolutionMiddleware.HttpContextItemKey] is TenantContext dunningTenant
                && !string.IsNullOrWhiteSpace(dunningTenant.SiteKey))
            {
                siteKey = dunningTenant.SiteKey;
            }

            if (confirm)
            {
                var key = (action ?? string.Empty).Trim();
                ErpSimpleWriteResult written = key switch
                {
                    "update_status" or "update-status" or "set_status" or "set-status" =>
                        await writes.UpdateStatusAsync(queueId, status, notes, session.UserId, cancellationToken),
                    "record_payment" or "record-payment" =>
                        await writes.RecordPaymentAsync(queueId, amount, session.UserId, cancellationToken),
                    "create_profile" or "create-profile" or "profile_create" or "profile-create" =>
                        await writes.CreateProfileAsync(siteKey, name, stepsJson, cancellationToken),
                    "add_invoice" or "add-invoice" =>
                        await writes.AddInvoiceAsync(
                            siteKey, customerId, customerName, invoiceRef, invoiceAmount,
                            amountDue, dueDate, profileId, cancellationToken),
                    "process" or "process_steps" or "process-steps" =>
                        await writes.ProcessAsync(siteKey, cancellationToken),
                    _ => ErpSimpleWriteResult.Fail("invalid", "Unknown dunning action. update_status / record_payment / create_profile / add_invoice / process are live."),
                };
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/collections-dunning-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new CpCollectionsDunningWriteRequest(action, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpCustomShippingWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpCustomShippingWriteDryRun dryRun,
            ICpCustomShippingWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/carriers-app", "Admin CP capability required for custom-shipping write.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpCustomShippingWriteBody>(context, cancellationToken)
                       ?? new();
            var action = body.Action;
            var id = body.Id;
            var category = body.Category;
            var declarationType = body.DeclarationType;
            var status = body.Status;
            var company = body.Company;
            var customsEmirate = body.CustomsEmirate;
            var entryDate = body.EntryDate;
            var declarationDate = body.DeclarationDate;
            var declarationNumber = body.DeclarationNumber;
            var blNumber = body.BlNumber;
            var blDate = body.BlDate;
            var srvNumber = body.SrvNumber;
            var lcDcNumber = body.LcDcNumber;
            var ldPoNumber = body.LdPoNumber;
            var supplierDetail = body.SupplierDetail;
            var currency = body.Currency;
            var invoiceAmountAed = body.InvoiceAmountAed;
            var totalCostAed = body.TotalCostAed;
            var remarks = body.Remarks;
            var itemsJson = body.ItemsJson;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                id = LiveWriteFormBinder.Long(form, "id", "declarationId", "declaration_id");
                category = LiveWriteFormBinder.Text(form, "category");
                declarationType = LiveWriteFormBinder.Text(form, "declarationType", "declaration_type");
                status = LiveWriteFormBinder.Text(form, "status");
                company = LiveWriteFormBinder.Text(form, "company");
                customsEmirate = LiveWriteFormBinder.Text(form, "customsEmirate", "customs_emirate");
                entryDate = LiveWriteFormBinder.Text(form, "entryDate", "entry_date");
                declarationDate = LiveWriteFormBinder.Text(form, "declarationDate", "declaration_date");
                declarationNumber = LiveWriteFormBinder.Text(form, "declarationNumber", "declaration_number");
                blNumber = LiveWriteFormBinder.Text(form, "blNumber", "bl_number");
                blDate = LiveWriteFormBinder.Text(form, "blDate", "bl_date");
                srvNumber = LiveWriteFormBinder.Text(form, "srvNumber", "srv_number");
                lcDcNumber = LiveWriteFormBinder.Text(form, "lcDcNumber", "lc_dc_number");
                ldPoNumber = LiveWriteFormBinder.Text(form, "ldPoNumber", "ld_po_number");
                supplierDetail = LiveWriteFormBinder.Text(form, "supplierDetail", "supplier_detail");
                currency = LiveWriteFormBinder.Text(form, "currency");
                invoiceAmountAed = LiveWriteFormBinder.Dec(form, "invoiceAmountAed", "invoice_amount_aed");
                totalCostAed = LiveWriteFormBinder.Dec(form, "totalCostAed", "total_cost_aed");
                remarks = LiveWriteFormBinder.Text(form, "remarks");
                itemsJson = LiveWriteFormBinder.Text(form, "itemsJson", "items_json", "line_items_json", "items");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var key = (action ?? string.Empty).Trim();
                ErpSimpleWriteResult written = key switch
                {
                    "save" or "save_declaration" or "save-declaration" =>
                        await writes.SaveAsync(
                            new CpCustomShippingSaveRequest(
                                id,
                                category,
                                declarationType,
                                status,
                                company,
                                customsEmirate,
                                entryDate,
                                declarationDate,
                                declarationNumber,
                                blNumber,
                                blDate,
                                srvNumber,
                                lcDcNumber,
                                ldPoNumber,
                                supplierDetail,
                                currency,
                                invoiceAmountAed,
                                totalCostAed,
                                remarks,
                                itemsJson),
                            session.UserId,
                            cancellationToken),
                    "submit" or "submit_declaration" or "submit-declaration" =>
                        await writes.SubmitAsync(id, cancellationToken),
                    _ => ErpSimpleWriteResult.Fail("invalid", "Unknown custom-shipping action. save / submit are live."),
                };
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/carriers-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new CpCustomShippingWriteRequest(action, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpFulfillmentQueueWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpFulfillmentQueueWriteDryRun dryRun,
            ICpFulfillmentQueueWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/fulfillment-queue-app", "Admin CP capability required for fulfillment-queue write.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpFulfillmentQueueWriteBody>(context, cancellationToken)
                       ?? new();
            var action = body.Action;
            var fulfillmentId = body.FulfillmentId;
            var itemId = body.ItemId;
            var assignedTo = body.AssignedTo;
            var assignedName = body.AssignedName;
            var status = body.Status;
            var pickStatus = body.PickStatus;
            var qtyPicked = body.QtyPicked;
            var qtyPacked = body.QtyPacked;
            var carrier = body.Carrier;
            var trackingNumber = body.TrackingNumber;
            var siteKey = body.SiteKey;
            var fulfillmentIds = body.FulfillmentIds ?? [];
            var orderId = body.OrderId;
            var orderNumber = body.OrderNumber;
            var customerName = body.CustomerName;
            var priority = body.Priority;
            var warehouse = body.Warehouse;
            var totalItems = body.TotalItems;
            var totalWeight = body.TotalWeight;
            var shipAddressJson = body.ShipAddressJson;
            var notes = body.Notes;
            var shippingMethod = body.ShippingMethod;
            var items = body.Items;
            var itemsJson = body.ItemsJson;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                fulfillmentId = LiveWriteFormBinder.Long(form, "fulfillmentId", "fulfillment_id");
                itemId = LiveWriteFormBinder.Long(form, "itemId", "item_id");
                assignedTo = LiveWriteFormBinder.Long(form, "assignedTo", "assigned_to");
                assignedName = LiveWriteFormBinder.Text(form, "assignedName", "assigned_name");
                status = LiveWriteFormBinder.Text(form, "status", "newStatus", "new_status");
                pickStatus = LiveWriteFormBinder.Text(form, "pickStatus", "pick_status");
                qtyPicked = LiveWriteFormBinder.Int(form, "qtyPicked", "qty_picked");
                qtyPacked = LiveWriteFormBinder.Int(form, "qtyPacked", "qty_packed");
                carrier = LiveWriteFormBinder.Text(form, "carrier");
                trackingNumber = LiveWriteFormBinder.Text(form, "trackingNumber", "tracking_number");
                siteKey = LiveWriteFormBinder.Text(form, "siteKey", "site_key");
                fulfillmentIds = LiveWriteFormBinder.Longs(form, "fulfillmentIds", "fulfillment_ids", "fulfillmentId", "fulfillment_id");
                orderId = LiveWriteFormBinder.Long(form, "orderId", "order_id");
                orderNumber = LiveWriteFormBinder.Text(form, "orderNumber", "order_number");
                customerName = LiveWriteFormBinder.Text(form, "customerName", "customer_name");
                priority = LiveWriteFormBinder.Text(form, "priority");
                warehouse = LiveWriteFormBinder.Text(form, "warehouse");
                totalItems = LiveWriteFormBinder.Int(form, "totalItems", "total_items");
                totalWeight = LiveWriteFormBinder.Dec(form, "totalWeight", "total_weight");
                shipAddressJson = LiveWriteFormBinder.Text(form, "shipAddressJson", "ship_address_json", "ship_address");
                notes = LiveWriteFormBinder.Text(form, "notes");
                shippingMethod = LiveWriteFormBinder.Text(form, "shippingMethod", "shipping_method");
                itemsJson = LiveWriteFormBinder.Text(form, "itemsJson", "items_json", "items");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (string.IsNullOrWhiteSpace(siteKey)
                && context.Items[TenantResolutionMiddleware.HttpContextItemKey] is TenantContext tenant
                && !string.IsNullOrWhiteSpace(tenant.SiteKey))
            {
                siteKey = tenant.SiteKey;
            }

            if (confirm)
            {
                var key = (action ?? string.Empty).Trim();
                var waveIds = fulfillmentIds.Count > 0 ? fulfillmentIds : (fulfillmentId > 0 ? new[] { fulfillmentId } : Array.Empty<long>());
                var queueItems = (items is { Count: > 0 } ? items : null)
                    ?? CpFulfillmentQueueWriteService.ParseItemsJson(itemsJson);
                ErpSimpleWriteResult written = key switch
                {
                    "transition" or "set_status" or "set-status" => await writes.TransitionAsync(
                        fulfillmentId, status, assignedTo, assignedName, carrier, trackingNumber, cancellationToken),
                    "assign" => await writes.AssignAsync(fulfillmentId, assignedTo, assignedName, cancellationToken),
                    "pick_item" or "pick-item" => await writes.PickItemAsync(itemId, qtyPicked, pickStatus, cancellationToken),
                    "pack_item" or "pack-item" => await writes.PackItemAsync(itemId, qtyPacked, cancellationToken),
                    "create_wave" or "create-wave" => await writes.CreateWaveAsync(siteKey, waveIds, cancellationToken),
                    "queue" or "queue_from_order" or "queue-from-order" or "create" => await writes.QueueFromOrderAsync(
                        siteKey, orderId, orderNumber, customerName, priority, warehouse, totalItems, totalWeight,
                        shipAddressJson, notes, shippingMethod, queueItems, cancellationToken),
                    _ => ErpSimpleWriteResult.Fail("invalid", "Unknown fulfillment action. transition / assign / pick_item / pack_item / create_wave / queue are live."),
                };
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/fulfillment-queue-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new CpFulfillmentQueueWriteRequest(action, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpCatalogueSetMinLimit, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpCatalogueWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/product-catalogue-app", "Admin CP capability required for catalogue min-limit.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpCatalogueSetMinLimitBody>(context, cancellationToken)
                       ?? new();
            var action = body.Action;
            var productId = body.ProductId;
            var enabled = body.Enabled;
            var value = body.Value;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                productId = LiveWriteFormBinder.Long(form, "productId", "product_id");
                enabled = LiveWriteFormBinder.Int(form, "enabled", "status");
                value = LiveWriteFormBinder.Dec(form, "value", "minLimit", "min_limit");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save the catalogue min-limit on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var key = (action ?? string.Empty).Trim();
            var written = key is "value" or "save_product_value_limit" or "min_limit"
                ? await writes.SetMinLimitValueAsync(productId, value, cancellationToken)
                : await writes.SetMinLimitEnableAsync(productId, enabled, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/product-catalogue-app" + (productId > 0 ? "?product_id=" + productId.ToString(CultureInfo.InvariantCulture) : ""),
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpPricesEditWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpPricesEditWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/prices-edit-app", "Admin CP capability required for prices-edit write.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpPricesEditWriteBody>(context, cancellationToken)
                       ?? new();
            var action = body.Action;
            var id = body.Id;
            var priceId = body.PriceId;
            var article = body.Article;
            var manufacturer = body.Manufacturer;
            var name = body.Name;
            var exist = body.Exist;
            var price = body.Price;
            var timeToExe = body.TimeToExe;
            var storage = body.Storage;
            var minOrder = body.MinOrder;
            var noArticle = body.NoArticle;
            var noManufacturer = body.NoManufacturer;
            var searchText = body.SearchText;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                id = LiveWriteFormBinder.Long(form, "id");
                priceId = LiveWriteFormBinder.Long(form, "priceId", "price_id");
                article = LiveWriteFormBinder.Text(form, "article");
                manufacturer = LiveWriteFormBinder.Text(form, "manufacturer");
                name = LiveWriteFormBinder.Text(form, "name");
                exist = LiveWriteFormBinder.Int(form, "exist");
                price = LiveWriteFormBinder.Dec(form, "price");
                timeToExe = LiveWriteFormBinder.Int(form, "timeToExe", "time_to_exe");
                storage = LiveWriteFormBinder.Text(form, "storage");
                minOrder = LiveWriteFormBinder.Int(form, "minOrder", "min_order");
                noArticle = LiveWriteFormBinder.Flag(form, "noArticle", "no_article");
                noManufacturer = LiveWriteFormBinder.Flag(form, "noManufacturer", "no_manufacturer");
                searchText = LiveWriteFormBinder.Text(form, "searchText", "search_text");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to write price rows on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var key = (action ?? string.Empty).Trim();
            ErpSimpleWriteResult written = key switch
            {
                "add" => await writes.AddAsync(priceId, article, manufacturer, name, exist, price, timeToExe, storage, minOrder, cancellationToken),
                "save" => await writes.SaveAsync(id, priceId, article, manufacturer, name, exist, price, timeToExe, storage, minOrder, cancellationToken),
                "del" or "delete" => await writes.DeleteAsync(id, cancellationToken),
                "del_search" or "del-search" or "search-delete" =>
                    await writes.DeleteSearchAsync(priceId, article, manufacturer, noArticle, noManufacturer, searchText, cancellationToken),
                _ => ErpSimpleWriteResult.Fail("invalid", "Unknown prices-edit action."),
            };
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/prices-edit-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpTemplatesActions, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpTemplatesActionsDryRun dryRun,
            ICpCatalogueWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/product-catalogue-app", "Admin CP capability required for category templates.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpTemplatesActionsBody>(context, cancellationToken) ?? new();
            var action = body.Action;
            var templateId = body.TemplateId;
            var caption = body.Caption;
            var categoryObject = body.CategoryObject;
            var imageBase64 = body.ImageBase64;
            var imageName = body.ImageName;
            var imageType = body.ImageType;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                templateId = LiveWriteFormBinder.Long(form, "templateId", "template_id", "id");
                caption = LiveWriteFormBinder.Text(form, "caption");
                categoryObject = LiveWriteFormBinder.Text(form, "categoryObject", "category_object");
                imageBase64 = LiveWriteFormBinder.Text(form, "imageBase64", "image_base64", "image");
                imageName = LiveWriteFormBinder.Text(form, "imageName", "image_name");
                imageType = LiveWriteFormBinder.Text(form, "imageType", "image_type");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new CpTemplatesActionsRequest(action, false)).ToPayload(SessionPayload(session)));
            }

            var key = CpCatalogueWriteService.NormalizeAction(action);
            ErpSimpleWriteResult written;
            if (key == "delete")
            {
                written = await writes.DeleteCategoryTemplateAsync(templateId, cancellationToken);
            }
            else if (key == "create")
            {
                written = await writes.CreateCategoryTemplateAsync(
                    new CpCategoryTemplateCreateRequest(caption, categoryObject, imageBase64, imageName, imageType),
                    cancellationToken);
            }
            else
            {
                written = ErpSimpleWriteResult.Fail("invalid", "Action must be create or delete.");
            }
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/product-catalogue-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpLineListsWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpLineListWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/product-catalogue-app", "Admin CP capability required for line-list writes.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpLineListsWriteBody>(context, cancellationToken) ?? new();
            var action = body.Action;
            var listId = body.ListId;
            var caption = body.Caption;
            var captionLangStrId = body.CaptionLangStrId;
            var type = body.Type;
            var dataType = body.DataType;
            var autoSort = body.AutoSort;
            var itemsJson = body.ItemsJson ?? body.TreeJson;
            var ids = body.Ids ?? body.LineLists;
            var langCode = body.LangCode;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action", "save_action");
                listId = LiveWriteFormBinder.Long(form, "listId", "list_id", "id");
                caption = LiveWriteFormBinder.Text(form, "caption");
                captionLangStrId = LiveWriteFormBinder.Text(form, "captionLangStrId", "caption_lang_str_id");
                type = LiveWriteFormBinder.IntOrNull(form, "type") ?? 1;
                dataType = LiveWriteFormBinder.Text(form, "dataType", "data_type");
                autoSort = LiveWriteFormBinder.Text(form, "autoSort", "auto_sort");
                itemsJson = LiveWriteFormBinder.Text(form, "itemsJson", "tree_json", "treeJson");
                ids = LiveWriteFormBinder.Text(form, "ids", "line_lists", "lineLists");
                langCode = LiveWriteFormBinder.Text(form, "langCode", "lang_code");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to create, save, or delete a line list on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var host = context.Request.Host.Host;
            var domainPath = string.IsNullOrWhiteSpace(host) ? "http://localhost/" : "http://" + host + "/";
            var normalized = CpLineListWriteService.NormalizeAction(action);
            ErpSimpleWriteResult written;
            if (normalized == "delete")
            {
                written = await writes.DeleteAsync(ids, cancellationToken);
            }
            else
            {
                written = await writes.SaveAsync(
                    new CpLineListSaveRequest(
                        string.IsNullOrWhiteSpace(normalized) ? "create" : normalized,
                        listId,
                        caption,
                        captionLangStrId,
                        type,
                        dataType,
                        autoSort,
                        itemsJson,
                        langCode,
                        domainPath),
                    cancellationToken);
            }

            return LiveWriteFormBinder.Complete(
                context,
                "/cp/product-catalogue-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpTreeListsWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpTreeListWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/product-catalogue-app", "Admin CP capability required for tree-list writes.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpTreeListsWriteBody>(context, cancellationToken) ?? new();
            var action = body.Action;
            var listId = body.ListId;
            var caption = body.Caption;
            var captionLangStrId = body.CaptionLangStrId;
            var dataType = body.DataType;
            var treeJson = body.TreeJson ?? body.ItemsJson;
            var ids = body.Ids ?? body.TreeLists;
            var parentId = body.ParentId;
            var langCode = body.LangCode;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action", "save_action");
                listId = LiveWriteFormBinder.Long(form, "listId", "list_id", "id", "tree_list_id");
                parentId = LiveWriteFormBinder.Long(form, "parentId", "parent_id");
                caption = LiveWriteFormBinder.Text(form, "caption");
                captionLangStrId = LiveWriteFormBinder.Text(form, "captionLangStrId", "caption_lang_str_id");
                dataType = LiveWriteFormBinder.Text(form, "dataType", "data_type");
                treeJson = LiveWriteFormBinder.Text(form, "treeJson", "tree_json", "itemsJson");
                ids = LiveWriteFormBinder.Text(form, "ids", "tree_lists", "treeLists");
                langCode = LiveWriteFormBinder.Text(form, "langCode", "lang_code");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to create, save, or delete a tree list on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var host = context.Request.Host.Host;
            var domainPath = string.IsNullOrWhiteSpace(host) ? "http://localhost/" : "http://" + host + "/";
            var normalized = CpTreeListWriteService.NormalizeAction(action);
            ErpSimpleWriteResult written;
            if (normalized == "delete")
            {
                written = await writes.DeleteAsync(ids, cancellationToken);
            }
            else if (normalized is "branch_create" or "branch_edit")
            {
                written = await writes.SaveBranchAsync(
                    new CpTreeListBranchSaveRequest(
                        normalized,
                        listId,
                        parentId,
                        caption,
                        captionLangStrId,
                        dataType,
                        treeJson,
                        langCode,
                        domainPath),
                    cancellationToken);
            }
            else
            {
                written = await writes.SaveAsync(
                    new CpTreeListSaveRequest(
                        string.IsNullOrWhiteSpace(normalized) ? "create" : normalized,
                        listId,
                        caption,
                        captionLangStrId,
                        dataType,
                        treeJson,
                        langCode,
                        domainPath),
                    cancellationToken);
            }

            return LiveWriteFormBinder.Complete(
                context,
                "/cp/product-catalogue-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpSkuMediaWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpSkuMediaWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/product-catalogue-app", "Admin CP capability required for SKU media writes.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpSkuMediaWriteBody>(context, cancellationToken) ?? new();
            var action = body.Action;
            var profileId = body.ProfileId;
            var productId = body.ProductId;
            var brand = body.Brand;
            var article = body.Article;
            var title = body.Title;
            var subtitle = body.Subtitle;
            var status = body.Status;
            var groupId = body.GroupId;
            var rowId = body.RowId;
            var photoId = body.PhotoId;
            var name = body.Name;
            var code = body.Code;
            var icon = body.Icon;
            var label = body.Label;
            var value = body.Value;
            var valueType = body.ValueType;
            var unit = body.Unit;
            var alt = body.Alt;
            var caption = body.Caption;
            var photoType = body.PhotoType;
            var sortOrder = body.SortOrder;
            var isPrimary = body.IsPrimary;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                profileId = LiveWriteFormBinder.Long(form, "profileId", "profile_id");
                productId = LiveWriteFormBinder.Long(form, "productId", "product_id");
                brand = LiveWriteFormBinder.Text(form, "brand");
                article = LiveWriteFormBinder.Text(form, "article");
                title = LiveWriteFormBinder.Text(form, "title");
                subtitle = LiveWriteFormBinder.Text(form, "subtitle");
                status = LiveWriteFormBinder.Text(form, "status");
                groupId = LiveWriteFormBinder.Long(form, "groupId", "group_id");
                rowId = LiveWriteFormBinder.Long(form, "rowId", "row_id");
                photoId = LiveWriteFormBinder.Long(form, "photoId", "photo_id");
                name = LiveWriteFormBinder.Text(form, "name");
                code = LiveWriteFormBinder.Text(form, "code");
                icon = LiveWriteFormBinder.Text(form, "icon");
                label = LiveWriteFormBinder.Text(form, "label");
                value = LiveWriteFormBinder.Text(form, "value");
                valueType = LiveWriteFormBinder.Text(form, "valueType", "value_type");
                unit = LiveWriteFormBinder.Text(form, "unit");
                alt = LiveWriteFormBinder.Text(form, "alt");
                caption = LiveWriteFormBinder.Text(form, "caption");
                photoType = LiveWriteFormBinder.Text(form, "photoType", "photo_type");
                sortOrder = LiveWriteFormBinder.IntOrNull(form, "sortOrder", "sort_order");
                isPrimary = LiveWriteFormBinder.IntOrNull(form, "isPrimary", "is_primary");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save, ensure, or delete a SKU profile, spec, or photo row on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var request = new CpSkuMediaProfileRequest(profileId, productId, brand, article, title, subtitle, status);
            var group = new CpSkuMediaSpecGroupRequest(profileId, name, code, icon, sortOrder);
            var row = new CpSkuMediaSpecRowRequest(groupId, rowId, label, value, valueType, unit, sortOrder);
            var photo = new CpSkuMediaPhotoMetaRequest(photoId, alt, caption, photoType, sortOrder, isPrimary);
            var key = CpSkuMediaWriteService.NormalizeAction(action);
            ErpSimpleWriteResult written = key switch
            {
                "delete_profile" => await writes.DeleteProfileAsync(profileId, cancellationToken),
                "ensure" => await writes.EnsureAsync(request, cancellationToken),
                "save_profile" => await writes.SaveProfileAsync(request, cancellationToken),
                "add_spec_group" => await writes.AddSpecGroupAsync(group, cancellationToken),
                "delete_spec_group" => await writes.DeleteSpecGroupAsync(groupId, cancellationToken),
                "add_spec_row" => await writes.AddSpecRowAsync(row, cancellationToken),
                "update_spec_row" => await writes.UpdateSpecRowAsync(row, cancellationToken),
                "delete_spec_row" => await writes.DeleteSpecRowAsync(rowId, cancellationToken),
                "update_photo" => await writes.UpdatePhotoAsync(photo, cancellationToken),
                "delete_photo" => await writes.DeletePhotoAsync(photoId, cancellationToken),
                _ => ErpSimpleWriteResult.Fail("invalid", "Unknown SKU media action."),
            };

            return LiveWriteFormBinder.Complete(
                context,
                "/cp/product-catalogue-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpMainPageProductsWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpMainPageProductsWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/product-catalogue-app", "Admin CP capability required for homepage-slot writes.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpMainPageProductsWriteBody>(context, cancellationToken) ?? new();
            var treeJson = body.TreeJson;
            var langCode = body.LangCode;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                treeJson = LiveWriteFormBinder.Text(form, "treeJson", "tree_json");
                langCode = LiveWriteFormBinder.Text(form, "langCode", "lang_code");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save homepage slots on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var host = context.Request.Host.Host;
            var domainPath = string.IsNullOrWhiteSpace(host) ? "http://localhost/" : "http://" + host + "/";
            var written = await writes.SaveAsync(
                new CpMainPageProductsSaveRequest(treeJson, langCode, domainPath),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/product-catalogue-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpSpecialSearchesWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpSpecialSearchWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/product-catalogue-app", "Admin CP capability required for special-search writes.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpSpecialSearchesWriteBody>(context, cancellationToken) ?? new();
            var action = body.Action;
            var searchId = body.SearchId;
            var caption = body.Caption ?? body.SearchCaption;
            var captionLangStrId = body.CaptionLangStrId ?? body.SearchCaptionLangStrId;
            var title = body.Title ?? body.SearchTitle;
            var titleLangStrId = body.TitleLangStrId ?? body.SearchTitleLangStrId;
            var description = body.Description ?? body.SearchDescription;
            var descriptionLangStrId = body.DescriptionLangStrId ?? body.SearchDescriptionLangStrId;
            var keywords = body.Keywords ?? body.SearchKeywords;
            var keywordsLangStrId = body.KeywordsLangStrId ?? body.SearchKeywordsLangStrId;
            var robots = body.Robots ?? body.SearchRobots;
            var alias = body.Alias ?? body.SearchAlias;
            var order = body.Order != 0 ? body.Order : body.SearchOrder;
            var active = body.Active != 0 ? body.Active : body.SearchActive;
            var treeJson = body.TreeJson;
            var deletedSteps = body.DeletedSteps ?? body.DeletedStepsJson;
            var searchesIds = body.SearchesIds ?? body.SearchesIdsJson;
            var imageName = body.ImageName ?? body.Img ?? body.FileLocal;
            var langCode = body.LangCode;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                searchId = LiveWriteFormBinder.Long(form, "searchId", "search_id");
                caption = LiveWriteFormBinder.Text(form, "caption", "search_caption", "searchCaption");
                captionLangStrId = LiveWriteFormBinder.Text(form, "captionLangStrId", "search_caption_lang_str_id");
                title = LiveWriteFormBinder.Text(form, "title", "search_title", "searchTitle");
                titleLangStrId = LiveWriteFormBinder.Text(form, "titleLangStrId", "search_title_lang_str_id");
                description = LiveWriteFormBinder.Text(form, "description", "search_description", "searchDescription");
                descriptionLangStrId = LiveWriteFormBinder.Text(form, "descriptionLangStrId", "search_description_lang_str_id");
                keywords = LiveWriteFormBinder.Text(form, "keywords", "search_keywords", "searchKeywords");
                keywordsLangStrId = LiveWriteFormBinder.Text(form, "keywordsLangStrId", "search_keywords_lang_str_id");
                robots = LiveWriteFormBinder.Text(form, "robots", "search_robots", "searchRobots");
                alias = LiveWriteFormBinder.Text(form, "alias", "search_alias", "searchAlias");
                order = LiveWriteFormBinder.Int(form, "order", "search_order", "searchOrder");
                active = LiveWriteFormBinder.Int(form, "active", "search_active", "searchActive");
                treeJson = LiveWriteFormBinder.Text(form, "treeJson", "tree_json");
                deletedSteps = LiveWriteFormBinder.Text(form, "deletedSteps", "deleted_steps", "deletedStepsJson");
                searchesIds = LiveWriteFormBinder.Text(form, "searchesIds", "searches_ids", "searchesIdsJson");
                imageName = LiveWriteFormBinder.Text(form, "imageName", "img", "file_local", "fileLocal");
                langCode = LiveWriteFormBinder.Text(form, "langCode", "lang_code");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save or delete special searches on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var key = CpSpecialSearchWriteService.NormalizeAction(action);
            if (key is not ("save" or "delete"))
            {
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/product-catalogue-app",
                    false,
                    "Action must be save or delete.",
                    new { ok = false, writes = 0, phpAuthoritative = false, validation_code = "invalid", message = "Action must be save or delete.", session = SessionPayload(session) });
            }

            var host = context.Request.Host.Host;
            var domainPath = string.IsNullOrWhiteSpace(host) ? "http://localhost/" : "http://" + host + "/";
            var written = key == "delete"
                ? await writes.DeleteAsync(searchesIds, cancellationToken)
                : await writes.SaveAsync(
                    new CpSpecialSearchSaveRequest(
                        searchId,
                        caption,
                        captionLangStrId,
                        title,
                        titleLangStrId,
                        description,
                        descriptionLangStrId,
                        keywords,
                        keywordsLangStrId,
                        robots,
                        alias,
                        order,
                        active,
                        treeJson,
                        deletedSteps,
                        imageName,
                        langCode,
                        domainPath),
                    cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/product-catalogue-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpCatalogueEditorWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpCatalogueEditorWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/product-catalogue-app", "Admin CP capability required for catalogue-tree writes.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpCatalogueEditorWriteBody>(context, cancellationToken) ?? new();
            var treeJson = body.TreeJson;
            var langCode = body.LangCode;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                treeJson = LiveWriteFormBinder.Text(form, "treeJson", "tree_json");
                langCode = LiveWriteFormBinder.Text(form, "langCode", "lang_code");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save the catalogue tree on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var host = context.Request.Host.Host;
            var domainPath = string.IsNullOrWhiteSpace(host) ? "http://localhost/" : "http://" + host + "/";
            var written = await writes.SaveTreeAsync(
                new CpCatalogueEditorSaveRequest(treeJson, langCode, domainPath),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/product-catalogue-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpCatalogueProductWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpCatalogueProductWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/product-catalogue-app", "Admin CP capability required for product writes.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpCatalogueProductWriteBody>(context, cancellationToken) ?? new();
            var action = body.Action;
            var productId = body.ProductId;
            var categoryId = body.CategoryId;
            var caption = body.Caption;
            var captionLangStrId = body.CaptionLangStrId;
            var alias = body.Alias;
            var titleTag = body.TitleTag;
            var titleTagLangStrId = body.TitleTagLangStrId;
            var descriptionTag = body.DescriptionTag;
            var descriptionTagLangStrId = body.DescriptionTagLangStrId;
            var keywordsTag = body.KeywordsTag;
            var keywordsTagLangStrId = body.KeywordsTagLangStrId;
            var robotsTag = body.RobotsTag;
            var publishedFlag = body.PublishedFlag;
            var productText = body.ProductText;
            var productTextLangStrId = body.ProductTextLangStrId;
            var propertiesJson = body.PropertiesJson ?? body.PropertiesObjects;
            var stickersJson = body.StickersJson ?? body.ProductStickers;
            var relatedJson = body.RelatedJson ?? body.ProductRelated;
            var imagesJson = body.ImagesJson ?? body.ImagesList;
            var langCode = body.LangCode;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action", "save_action");
                productId = LiveWriteFormBinder.Long(form, "productId", "product_id");
                categoryId = LiveWriteFormBinder.Long(form, "categoryId", "category_id");
                caption = LiveWriteFormBinder.Text(form, "caption");
                captionLangStrId = LiveWriteFormBinder.Text(form, "captionLangStrId", "caption_lang_str_id");
                alias = LiveWriteFormBinder.Text(form, "alias");
                titleTag = LiveWriteFormBinder.Text(form, "titleTag", "title_tag");
                titleTagLangStrId = LiveWriteFormBinder.Text(form, "titleTagLangStrId", "title_tag_lang_str_id");
                descriptionTag = LiveWriteFormBinder.Text(form, "descriptionTag", "description_tag");
                descriptionTagLangStrId = LiveWriteFormBinder.Text(form, "descriptionTagLangStrId", "description_tag_lang_str_id");
                keywordsTag = LiveWriteFormBinder.Text(form, "keywordsTag", "keywords_tag");
                keywordsTagLangStrId = LiveWriteFormBinder.Text(form, "keywordsTagLangStrId", "keywords_tag_lang_str_id");
                robotsTag = LiveWriteFormBinder.Text(form, "robotsTag", "robots_tag");
                publishedFlag = LiveWriteFormBinder.Int(form, "publishedFlag", "published_flag");
                productText = LiveWriteFormBinder.Text(form, "productText", "product_text");
                productTextLangStrId = LiveWriteFormBinder.Text(form, "productTextLangStrId", "product_text_lang_str_id");
                propertiesJson = LiveWriteFormBinder.Text(form, "propertiesJson", "properties_objects");
                stickersJson = LiveWriteFormBinder.Text(form, "stickersJson", "product_stickers");
                relatedJson = LiveWriteFormBinder.Text(form, "relatedJson", "product_related");
                imagesJson = LiveWriteFormBinder.Text(form, "imagesJson", "images_list", "imagesList");
                langCode = LiveWriteFormBinder.Text(form, "langCode", "lang_code");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to create or edit a catalogue product on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var host = context.Request.Host.Host;
            var domainPath = string.IsNullOrWhiteSpace(host) ? "http://localhost/" : "http://" + host + "/";
            var written = await writes.SaveAsync(
                new CpCatalogueProductSaveRequest(
                    action,
                    productId,
                    categoryId,
                    caption,
                    captionLangStrId,
                    alias,
                    titleTag,
                    titleTagLangStrId,
                    descriptionTag,
                    descriptionTagLangStrId,
                    keywordsTag,
                    keywordsTagLangStrId,
                    robotsTag,
                    publishedFlag,
                    productText,
                    productTextLangStrId,
                    propertiesJson,
                    stickersJson,
                    relatedJson,
                    imagesJson,
                    langCode,
                    domainPath),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/product-catalogue-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpCatalogueReviewsWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpCatalogueReviewWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/product-catalogue-app", "Admin CP capability required for review writes.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpCatalogueReviewsWriteBody>(context, cancellationToken) ?? new();
            var action = body.Action;
            var reviewId = body.ReviewId != 0 ? body.ReviewId : body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                reviewId = LiveWriteFormBinder.Long(form, "reviewId", "review_id", "id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to delete a product review on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var key = CpCatalogueReviewWriteService.NormalizeAction(action);
            if (key != "delete")
            {
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/product-catalogue-app",
                    false,
                    "Action must be delete.",
                    new { ok = false, writes = 0, phpAuthoritative = false, validation_code = "invalid", message = "Action must be delete.", session = SessionPayload(session) });
            }

            var written = await writes.DeleteAsync(reviewId, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/product-catalogue-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpCatalogueProductsDelete, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpCatalogueProductsDeleteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/product-catalogue-app", "Admin CP capability required for product-delete writes.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpCatalogueProductsDeleteBody>(context, cancellationToken) ?? new();
            var action = body.Action;
            var categoryId = body.CategoryId;
            var productsJson = body.ProductsJson ?? body.ProductsList;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                categoryId = LiveWriteFormBinder.Long(form, "categoryId", "category_id");
                productsJson = LiveWriteFormBinder.Text(form, "productsJson", "products_list", "productsList");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to delete catalogue products on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var key = CpCatalogueProductsDeleteService.NormalizeAction(action);
            if (key != "delete")
            {
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/product-catalogue-app",
                    false,
                    "Action must be delete.",
                    new { ok = false, writes = 0, phpAuthoritative = false, validation_code = "invalid", message = "Action must be delete.", session = SessionPayload(session) });
            }

            var written = await writes.DeleteAsync(
                new CpCatalogueProductsDeleteRequest(categoryId, productsJson),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/product-catalogue-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpPriceReviewWrite, async (HttpContext context, CpPriceReviewWriteBody? body, ILegacySessionValidator validator, ICpPriceReviewWriteDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required.");
            body ??= new CpPriceReviewWriteBody(null, false);
            return Results.Ok(dryRun.Evaluate(new CpPriceReviewWriteRequest(body.Action, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });
        endpoints.MapPost(EcomAeRoutes.CpPriceReviewCreateCsv, async (HttpContext context, CpPriceReviewCreateCsvBody? body, ILegacySessionValidator validator, ICpPriceReviewCreateCsvDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required.");
            body ??= new CpPriceReviewCreateCsvBody(null, false);
            return Results.Ok(dryRun.Evaluate(new CpPriceReviewCreateCsvRequest(body.Action, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });
        endpoints.MapPost(EcomAeRoutes.CpAccessoriesPhotos, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpAccessoriesPhotoWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/accessories-app", "Admin CP capability required for accessories photo writes.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpAccessoriesPhotosBody>(context, cancellationToken) ?? new();
            var action = body.Action;
            var listingId = body.ListingId;
            var photoId = body.PhotoId;
            var fileName = body.FileName ?? body.ImageName ?? body.Photo;
            var asPrimary = body.AsPrimary;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                listingId = LiveWriteFormBinder.Long(form, "listingId", "listing_id");
                photoId = LiveWriteFormBinder.Long(form, "photoId", "photo_id");
                fileName = LiveWriteFormBinder.Text(form, "fileName", "file_name", "imageName", "photo");
                asPrimary = LiveWriteFormBinder.Flag(form, "asPrimary", "as_primary");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save or delete accessory photos on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.WriteAsync(
                new CpAccessoriesPhotoWriteRequest(action, listingId, photoId, fileName, asPrimary),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/accessories-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpAccessoriesListingsWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpAccessoriesListingWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/accessories-app", "Admin CP capability required for accessories listing writes.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpAccessoriesListingsWriteBody>(context, cancellationToken) ?? new();
            var action = body.Action;
            var listingId = body.ListingId != 0 ? body.ListingId : body.Id;
            var categoryId = body.CategoryId;
            var subcategoryId = body.SubcategoryId;
            var title = body.Title;
            var description = body.Description;
            var make = body.Make;
            var model = body.Model;
            var year = body.Year;
            var city = body.City;
            var conditionType = body.ConditionType;
            var price = body.Price;
            var comparePrice = body.ComparePrice;
            var currency = body.Currency;
            var imageUrl = body.ImageUrl;
            var externalUrl = body.ExternalUrl;
            var photoCount = body.PhotoCount;
            var featured = body.Featured;
            var stockQty = body.StockQty;
            var status = body.Status;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                listingId = LiveWriteFormBinder.Long(form, "listingId", "listing_id", "id");
                categoryId = LiveWriteFormBinder.Long(form, "categoryId", "category_id");
                subcategoryId = LiveWriteFormBinder.Long(form, "subcategoryId", "subcategory_id");
                title = LiveWriteFormBinder.Text(form, "title");
                description = LiveWriteFormBinder.Text(form, "description");
                make = LiveWriteFormBinder.Text(form, "make");
                model = LiveWriteFormBinder.Text(form, "model");
                year = LiveWriteFormBinder.Text(form, "year");
                city = LiveWriteFormBinder.Text(form, "city");
                conditionType = LiveWriteFormBinder.Text(form, "conditionType", "condition_type");
                price = LiveWriteFormBinder.Dec(form, "price");
                comparePrice = LiveWriteFormBinder.Dec(form, "comparePrice", "compare_price");
                currency = LiveWriteFormBinder.Text(form, "currency");
                imageUrl = LiveWriteFormBinder.Text(form, "imageUrl", "image_url");
                externalUrl = LiveWriteFormBinder.Text(form, "externalUrl", "external_url");
                photoCount = LiveWriteFormBinder.Int(form, "photoCount", "photo_count");
                featured = LiveWriteFormBinder.Flag(form, "featured");
                stockQty = LiveWriteFormBinder.Int(form, "stockQty", "stock_qty");
                status = LiveWriteFormBinder.Text(form, "status");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save or delete accessory listings on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.WriteAsync(
                new CpAccessoriesListingWriteRequest(
                    action,
                    listingId,
                    categoryId,
                    subcategoryId,
                    title,
                    description,
                    make,
                    model,
                    year,
                    city,
                    conditionType,
                    price,
                    comparePrice,
                    currency,
                    imageUrl,
                    externalUrl,
                    photoCount == 0 ? 1 : photoCount,
                    featured,
                    stockQty,
                    status),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/accessories-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpAccessoriesTaxonomyWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpAccessoriesTaxonomyWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/accessories-app", "Admin CP capability required for accessories taxonomy writes.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpAccessoriesTaxonomyWriteBody>(context, cancellationToken) ?? new();
            var action = body.Action;
            var id = body.Id != 0 ? body.Id : body.CategoryId != 0 ? body.CategoryId : body.TermId;
            var parentId = body.ParentId;
            var label = body.Label;
            var termType = body.TermType;
            var sortOrder = body.SortOrder;
            var active = body.Active;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                id = LiveWriteFormBinder.Long(form, "id", "categoryId", "category_id", "termId", "term_id");
                parentId = LiveWriteFormBinder.Long(form, "parentId", "parent_id");
                label = LiveWriteFormBinder.Text(form, "label");
                termType = LiveWriteFormBinder.Text(form, "termType", "term_type");
                sortOrder = LiveWriteFormBinder.Int(form, "sortOrder", "sort_order");
                active = LiveWriteFormBinder.Flag(form, "active");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save accessory categories or terms on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.WriteAsync(
                new CpAccessoriesTaxonomyWriteRequest(action, id, parentId, label, termType, sortOrder, active),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/accessories-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpVersionClearUpdates, async (HttpContext context, CpVersionClearUpdatesBody? body, ILegacySessionValidator validator, ICpVersionClearUpdatesDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required.");
            body ??= new CpVersionClearUpdatesBody(null, false);
            return Results.Ok(dryRun.Evaluate(new CpVersionClearUpdatesRequest(body.Action, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
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

        endpoints.MapGet(EcomAeRoutes.ControlPanelUsersDetailDigest, async (
            HttpContext context,
            int userId,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for user detail digest.");
            }

            var detail = await dashboards.GetCpUserDetailAsync(userId, cancellationToken);
            if (detail is null)
            {
                return Results.NotFound(new { ok = false, message = "User not found." });
            }

            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                user = detail,
                source = detail.Source,
                message = detail.Message,
                session = SessionPayload(session),
                note = "Read-only user detail digest (PHP users/usermanager/user). Writes remain PHP-authoritative."
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
                note = "Modules digest. Create/edit/delete/activate POST /cp/modules/write when confirmWrites=true."
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
                note = "Menu metadata + structure summary (raw structure JSON omitted). Create/update/delete POST /cp/menus/write when confirmWrites=true. Drag-tree UX stays PHP."
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
                note = "Content pages metadata (body omitted). Publish, main, body, create/edit, and tree POST /cp/content/* when confirmWrites=true. TinyMCE upload stays PHP."
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
        endpoints.MapPost(EcomAeRoutes.CpStoragesGroups, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpStorageGroupWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/storages-app", "Admin CP capability required for storage groups.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpStoragesGroupsBody>(context, cancellationToken) ?? new();
            var action = body.Action;
            var id = body.Id;
            var name = body.Name;
            var storages = body.Storages;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                id = LiveWriteFormBinder.Long(form, "id", "groupId", "group_id");
                name = LiveWriteFormBinder.Text(form, "name");
                storages = LiveWriteFormBinder.Text(form, "storages");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to add or delete a storage group on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var key = (action ?? string.Empty).Trim();
            var written = key is "del" or "delete"
                ? await writes.DeleteAsync(id, cancellationToken)
                : await writes.AddAsync(name, storages, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/storages-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpStoragesWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpStorageWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/storages-app", "Admin CP capability required for warehouse save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpStoragesWriteBody>(context, cancellationToken) ?? new();
            var action = body.Action ?? body.SaveAction;
            var storageId = body.StorageId;
            var name = body.Name;
            var shortName = body.ShortName;
            var currency = body.Currency;
            var interfaceType = body.InterfaceType;
            var usersJson = body.UsersJson ?? body.Users;
            var optionsJson = body.ConnectionOptionsJson ?? body.ConnectionOptions;
            var handlerFolder = body.HandlerFolder;
            var hidden = body.Hidden;
            var bgLineColor = body.BgLineColor;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action", "saveAction", "save_action");
                storageId = LiveWriteFormBinder.Long(form, "storageId", "storage_id", "id");
                name = LiveWriteFormBinder.Text(form, "name");
                shortName = LiveWriteFormBinder.Text(form, "shortName", "short_name");
                currency = LiveWriteFormBinder.Int(form, "currency");
                interfaceType = LiveWriteFormBinder.Int(form, "interfaceType", "interface_type");
                usersJson = LiveWriteFormBinder.Text(form, "usersJson", "users_json", "users");
                optionsJson = LiveWriteFormBinder.Text(form, "connectionOptionsJson", "connection_options_json", "connection_options", "connectionOptions");
                handlerFolder = LiveWriteFormBinder.Text(form, "handlerFolder", "handler_folder");
                hidden = LiveWriteFormBinder.Int(form, "hidden");
                bgLineColor = LiveWriteFormBinder.Int(form, "bgLineColor", "bg_line_color");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save the warehouse on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var key = (action ?? string.Empty).Trim().ToLowerInvariant();
            var written = key is "edit" or "update"
                ? await writes.UpdateAsync(
                    storageId, name, shortName, currency, interfaceType, usersJson, optionsJson, handlerFolder, hidden, bgLineColor, cancellationToken)
                : await writes.CreateAsync(
                    name, shortName, currency, interfaceType, usersJson, optionsJson, handlerFolder, hidden, bgLineColor, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/storages-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpStoragesMembership, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpStorageWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/storages-app", "Admin CP capability required for warehouse membership.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpStoragesMembershipBody>(context, cancellationToken) ?? new();
            var officeId = body.OfficeId;
            var storagesList = body.StoragesList;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                officeId = LiveWriteFormBinder.Long(form, "officeId", "office_id");
                storagesList = LiveWriteFormBinder.Text(form, "storagesList", "storages_list");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save office warehouse membership on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.SaveMembershipAsync(officeId, storagesList, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/storages-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpOfficesWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpOfficeWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/offices-app", "Admin CP capability required for office save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpOfficesWriteBody>(context, cancellationToken) ?? new();
            var action = body.Action ?? body.SaveAction;
            var officeId = body.OfficeId;
            var caption = body.Caption;
            var country = body.Country;
            var region = body.Region;
            var city = body.City;
            var address = body.Address;
            var phone = body.Phone;
            var email = body.Email;
            var coordinates = body.Coordinates;
            var description = body.Description;
            var timetable = body.Timetable;
            var usersJson = body.UsersJson ?? body.Users;
            var captionLangStrId = body.CaptionLangStrId;
            var countryLangStrId = body.CountryLangStrId;
            var regionLangStrId = body.RegionLangStrId;
            var cityLangStrId = body.CityLangStrId;
            var addressLangStrId = body.AddressLangStrId;
            var descriptionLangStrId = body.DescriptionLangStrId;
            var timetableLangStrId = body.TimetableLangStrId;
            var langCode = body.LangCode;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action", "saveAction", "save_action");
                officeId = LiveWriteFormBinder.Long(form, "officeId", "office_id", "id");
                caption = LiveWriteFormBinder.Text(form, "caption", "name");
                country = LiveWriteFormBinder.Text(form, "country");
                region = LiveWriteFormBinder.Text(form, "region");
                city = LiveWriteFormBinder.Text(form, "city");
                address = LiveWriteFormBinder.Text(form, "address");
                phone = LiveWriteFormBinder.Text(form, "phone");
                email = LiveWriteFormBinder.Text(form, "email");
                coordinates = LiveWriteFormBinder.Text(form, "coordinates");
                description = LiveWriteFormBinder.Text(form, "description");
                timetable = LiveWriteFormBinder.Text(form, "timetable");
                usersJson = LiveWriteFormBinder.Text(form, "usersJson", "users_json", "users");
                captionLangStrId = LiveWriteFormBinder.Text(form, "captionLangStrId", "caption_lang_str_id");
                countryLangStrId = LiveWriteFormBinder.Text(form, "countryLangStrId", "country_lang_str_id");
                regionLangStrId = LiveWriteFormBinder.Text(form, "regionLangStrId", "region_lang_str_id");
                cityLangStrId = LiveWriteFormBinder.Text(form, "cityLangStrId", "city_lang_str_id");
                addressLangStrId = LiveWriteFormBinder.Text(form, "addressLangStrId", "address_lang_str_id");
                descriptionLangStrId = LiveWriteFormBinder.Text(form, "descriptionLangStrId", "description_lang_str_id");
                timetableLangStrId = LiveWriteFormBinder.Text(form, "timetableLangStrId", "timetable_lang_str_id");
                langCode = LiveWriteFormBinder.Text(form, "langCode", "lang_code");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save the office on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var host = context.Request.Host.Host;
            var domainPath = string.IsNullOrWhiteSpace(host) ? "http://localhost/" : "http://" + host + "/";
            var request = new CpOfficeSaveRequest(
                officeId,
                caption,
                country,
                region,
                city,
                address,
                phone,
                email,
                coordinates,
                description,
                usersJson,
                timetable,
                captionLangStrId,
                countryLangStrId,
                regionLangStrId,
                cityLangStrId,
                addressLangStrId,
                descriptionLangStrId,
                timetableLangStrId,
                langCode,
                domainPath);
            var key = (action ?? string.Empty).Trim().ToLowerInvariant();
            var written = key is "edit" or "update"
                ? await writes.UpdateAsync(request, cancellationToken)
                : await writes.CreateAsync(request, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/offices-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpOfficesDelete, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpOfficeWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/offices-app", "Admin CP capability required for office delete.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpOfficesDeleteBody>(context, cancellationToken) ?? new();
            var officeIds = body.OfficeIds ?? body.Offices;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                officeIds = LiveWriteFormBinder.Text(form, "officeIds", "office_ids", "offices");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to delete offices on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.DeleteAsync(officeIds, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/offices-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpOfficesGeo, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpOfficeWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/offices-app", "Admin CP capability required for office geo.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpOfficesGeoBody>(context, cancellationToken) ?? new();
            var officeId = body.OfficeId;
            var geoList = body.GeoList;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                officeId = LiveWriteFormBinder.Long(form, "officeId", "office_id");
                geoList = LiveWriteFormBinder.Text(form, "geoList", "geo_list");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save office geo membership on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.SaveGeoAsync(officeId, geoList, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/offices-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpDeliveryMethodsWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpObtainingModeWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/delivery-methods-app", "Admin CP capability required for delivery methods.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpDeliveryMethodsWriteBody>(context, cancellationToken) ?? new();
            var action = body.Action;
            var modeId = body.ModeId;
            var available = body.Available;
            var caption = body.Caption;
            var captionLangStrId = body.CaptionLangStrId;
            var sortOrder = body.SortOrder;
            var parametersValues = body.ParametersValues;
            var langCode = body.LangCode;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                modeId = LiveWriteFormBinder.Long(form, "modeId", "mode_id", "id", "obtain_mode_id", "obtainModeId");
                available = LiveWriteFormBinder.Int(form, "available", "activate_obtain_mode", "activateObtainMode");
                caption = LiveWriteFormBinder.Text(form, "caption");
                captionLangStrId = LiveWriteFormBinder.Text(form, "captionLangStrId", "caption_lang_str_id");
                sortOrder = LiveWriteFormBinder.Int(form, "sortOrder", "sort_order", "order");
                parametersValues = LiveWriteFormBinder.Text(form, "parametersValues", "parameters_values");
                langCode = LiveWriteFormBinder.Text(form, "langCode", "lang_code");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save the delivery method on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var key = (action ?? string.Empty).Trim().ToLowerInvariant();
            ErpSimpleWriteResult written;
            if (key is "save" or "save_action" or "edit" or "update")
            {
                var host = context.Request.Host.Host;
                var domainPath = string.IsNullOrWhiteSpace(host) ? "http://localhost/" : "http://" + host + "/";
                written = await writes.SaveAsync(
                    new CpObtainingModeSaveRequest(
                        modeId,
                        caption,
                        captionLangStrId,
                        sortOrder,
                        available,
                        parametersValues,
                        langCode,
                        domainPath),
                    cancellationToken);
            }
            else
            {
                written = await writes.SetAvailableAsync(modeId, available, cancellationToken);
            }

            return LiveWriteFormBinder.Complete(
                context,
                "/cp/delivery-methods-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpGeoRegionsWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpGeoTreeWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/geo-regions-app", "Admin CP capability required for geo tree save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpGeoRegionsWriteBody>(context, cancellationToken) ?? new();
            var treeJson = body.TreeJson ?? body.TreeJsonText;
            var langCode = body.LangCode;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                treeJson = LiveWriteFormBinder.Text(form, "treeJson", "tree_json");
                langCode = LiveWriteFormBinder.Text(form, "langCode", "lang_code");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save the geo tree on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var host = context.Request.Host.Host;
            var domainPath = string.IsNullOrWhiteSpace(host) ? "http://localhost/" : "http://" + host + "/";
            var written = await writes.SaveTreeAsync(treeJson, langCode, domainPath, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/geo-regions-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpSearchTabsWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpSearchTabWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/search-tabs-app", "Admin CP capability required for search tabs.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpSearchTabsWriteBody>(context, cancellationToken) ?? new();
            var action = body.Action;
            var tabId = body.TabId;
            var enabled = body.Enabled;
            var tabEnabled = body.TabEnabled;
            var caption = body.Caption ?? body.TabCaption;
            var captionLangStrId = body.CaptionLangStrId ?? body.TabCaptionLangStrId;
            var sortOrder = body.SortOrder;
            var parametersValues = body.ParametersValues;
            var langCode = body.LangCode;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                tabId = LiveWriteFormBinder.Long(form, "tabId", "tab_id", "id");
                enabled = LiveWriteFormBinder.Int(form, "enabled", "activate_tab", "activateTab");
                tabEnabled = LiveWriteFormBinder.Text(form, "tabEnabled", "tab_enabled", "enabled");
                caption = LiveWriteFormBinder.Text(form, "caption", "tabCaption", "tab_caption");
                captionLangStrId = LiveWriteFormBinder.Text(form, "captionLangStrId", "caption_lang_str_id", "tab_caption_lang_str_id");
                sortOrder = LiveWriteFormBinder.Int(form, "sortOrder", "sort_order", "tab_order", "order");
                parametersValues = LiveWriteFormBinder.Text(form, "parametersValues", "parameters_values");
                langCode = LiveWriteFormBinder.Text(form, "langCode", "lang_code");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save the search tab on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var key = (action ?? string.Empty).Trim().ToLowerInvariant();
            var flag = CpSearchTabWriteService.ParseEnabled(tabEnabled, enabled);
            ErpSimpleWriteResult written;
            if (key is "save" or "save_action" or "edit" or "update")
            {
                var host = context.Request.Host.Host;
                var domainPath = string.IsNullOrWhiteSpace(host) ? "http://localhost/" : "http://" + host + "/";
                written = await writes.SaveAsync(
                    new CpSearchTabSaveRequest(
                        tabId,
                        caption,
                        captionLangStrId,
                        sortOrder,
                        flag,
                        parametersValues,
                        langCode,
                        domainPath),
                    cancellationToken);
            }
            else
            {
                written = await writes.SetEnabledAsync(tabId, flag, cancellationToken);
            }

            return LiveWriteFormBinder.Complete(
                context,
                "/cp/search-tabs-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpAdditionalTextsWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpAdditionalTextWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/additional-texts-app", "Admin CP capability required for additional texts.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpAdditionalTextsWriteBody>(context, cancellationToken) ?? new();
            var url = body.Url;
            var content = body.Content ?? body.Text;
            var beforeMain = body.BeforeMain;
            var titleTag = body.TitleTag;
            var descriptionTag = body.DescriptionTag;
            var keywordsTag = body.KeywordsTag;
            var contentLangStrId = body.ContentLangStrId ?? body.TextLangStrId;
            var titleLangStrId = body.TitleLangStrId;
            var descriptionLangStrId = body.DescriptionLangStrId;
            var keywordsLangStrId = body.KeywordsLangStrId;
            var langCode = body.LangCode;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                url = LiveWriteFormBinder.Text(form, "url");
                content = LiveWriteFormBinder.Text(form, "content", "text");
                beforeMain = LiveWriteFormBinder.Flag(form, "beforeMain", "before_main") ? 1 : LiveWriteFormBinder.Int(form, "beforeMain", "before_main");
                titleTag = LiveWriteFormBinder.Text(form, "titleTag", "title_tag");
                descriptionTag = LiveWriteFormBinder.Text(form, "descriptionTag", "description_tag");
                keywordsTag = LiveWriteFormBinder.Text(form, "keywordsTag", "keywords_tag");
                contentLangStrId = LiveWriteFormBinder.Text(form, "contentLangStrId", "text_lang_str_id", "content_lang_str_id");
                titleLangStrId = LiveWriteFormBinder.Text(form, "titleLangStrId", "title_tag_lang_str_id");
                descriptionLangStrId = LiveWriteFormBinder.Text(form, "descriptionLangStrId", "description_tag_lang_str_id");
                keywordsLangStrId = LiveWriteFormBinder.Text(form, "keywordsLangStrId", "keywords_tag_lang_str_id");
                langCode = LiveWriteFormBinder.Text(form, "langCode", "lang_code");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save additional text on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var host = context.Request.Host.Host;
            var domainPath = string.IsNullOrWhiteSpace(host) ? "http://localhost/" : "http://" + host + "/";
            var written = await writes.SaveAsync(
                new CpAdditionalTextSaveRequest(
                    url,
                    content,
                    beforeMain,
                    titleTag,
                    descriptionTag,
                    keywordsTag,
                    contentLangStrId,
                    titleLangStrId,
                    descriptionLangStrId,
                    keywordsLangStrId,
                    langCode,
                    domainPath),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/additional-texts-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpAdditionalTextsDelete, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpAdditionalTextWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/additional-texts-app", "Admin CP capability required for additional-text delete.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpAdditionalTextsDeleteBody>(context, cancellationToken) ?? new();
            var ids = body.Ids ?? body.UrlsToDel;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                ids = LiveWriteFormBinder.Text(form, "ids", "urls_to_del", "urlsToDel");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to delete additional texts on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.DeleteAsync(ids, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/additional-texts-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpSliderBannersWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpSliderWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/slider-banners-app", "Admin CP capability required for slider banners.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpSliderBannersWriteBody>(context, cancellationToken) ?? new();
            var action = body.Action;
            var imageId = body.ImageId;
            var href = body.Href;
            var link = body.Link;
            var connected = body.Connected;
            var connectedFlag = body.ConnectedFlag;
            var cntImg = body.CntImg;
            var cntImgNext = body.CntImgNext;
            var timeNext = body.TimeNext;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                imageId = LiveWriteFormBinder.Long(form, "imageId", "image_id", "id");
                href = LiveWriteFormBinder.Text(form, "href");
                link = LiveWriteFormBinder.Text(form, "link");
                connected = LiveWriteFormBinder.Int(form, "connected");
                connectedFlag = LiveWriteFormBinder.Text(form, "connected");
                cntImg = LiveWriteFormBinder.Int(form, "cntImg", "cnt_img");
                cntImgNext = LiveWriteFormBinder.Int(form, "cntImgNext", "cnt_img_next");
                timeNext = LiveWriteFormBinder.Int(form, "timeNext", "time_next");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save slider banners on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var key = (action ?? string.Empty).Trim().ToLowerInvariant();
            var connectedOn = connected == 1 || connectedFlag is "on" or "1" or "true" or "yes";
            ErpSimpleWriteResult written = key switch
            {
                "up" => await writes.MoveAsync(imageId, true, cancellationToken),
                "do" or "down" => await writes.MoveAsync(imageId, false, cancellationToken),
                "del" or "delete" => await writes.DeleteAsync(imageId, cancellationToken),
                "add" => await writes.AddAsync(href, link, cancellationToken),
                _ => await writes.SaveSettingsAsync(connectedOn ? 1 : 0, cntImg, cntImgNext, timeNext, cancellationToken)
            };
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/slider-banners-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpProductFiltersWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpProductFilterWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/product-filters-app", "Admin CP capability required for product filters.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpProductFiltersWriteBody>(context, cancellationToken) ?? new();
            var action = body.Action;
            var filterId = body.FilterId;
            var manufacturer = body.Manufacturer;
            var article = body.Article;
            var name = body.Name;
            var flag = body.Flag;
            var flagText = body.FlagText;
            var storagesJson = body.StoragesJson ?? body.ListStorages;
            var minPrice = body.MinPrice;
            var maxPrice = body.MaxPrice;
            var minTime = body.MinTime;
            var maxTime = body.MaxTime;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                filterId = LiveWriteFormBinder.Long(form, "filterId", "filter_id", "id");
                manufacturer = LiveWriteFormBinder.Text(form, "manufacturer");
                article = LiveWriteFormBinder.Text(form, "article");
                name = LiveWriteFormBinder.Text(form, "name");
                flag = LiveWriteFormBinder.Int(form, "flag", "active", "enabled");
                flagText = LiveWriteFormBinder.Text(form, "flag", "active", "enabled");
                storagesJson = LiveWriteFormBinder.Text(form, "storagesJson", "storages_list_json", "list_storages", "listStorages");
                minPrice = LiveWriteFormBinder.Text(form, "minPrice", "min_price");
                maxPrice = LiveWriteFormBinder.Text(form, "maxPrice", "max_price");
                minTime = LiveWriteFormBinder.Text(form, "minTime", "min_time");
                maxTime = LiveWriteFormBinder.Text(form, "maxTime", "max_time");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save product filters on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var key = (action ?? string.Empty).Trim().ToLowerInvariant();
            var on = CpProductFilterWriteService.ParseFlag(flagText, flag);
            ErpSimpleWriteResult written = key switch
            {
                "add" => await writes.AddAsync(manufacturer, article, name, cancellationToken),
                "save" or "edit" or "update" => await writes.SaveAsync(filterId, manufacturer, article, name, cancellationToken),
                "del" or "delete" => await writes.DeleteAsync(filterId, cancellationToken),
                "active" or "activation" => await writes.SetActiveAsync(filterId, on, cancellationToken),
                "active_all" or "activate_all" or "deactivate_all" => await writes.SetActiveAllAsync(
                    key == "deactivate_all" ? 0 : on,
                    cancellationToken),
                "save_storages" or "scope" or "setting" => await writes.SaveStoragesAsync(
                    filterId, storagesJson, minPrice, maxPrice, minTime, maxTime, cancellationToken),
                _ => ErpSimpleWriteResult.Fail("invalid", "Unknown product-filter action.")
            };
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/product-filters-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpOrderStatusesWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpOrderStatusWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/order-statuses-app", "Admin CP capability required for order statuses.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpOrderStatusesWriteBody>(context, cancellationToken) ?? new();
            var ordersJson = body.OrdersJson ?? body.OrdersStatuses;
            var itemsJson = body.ItemsJson ?? body.OrdersItemsStatuses;
            var langCode = body.LangCode;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                ordersJson = LiveWriteFormBinder.Text(form, "ordersJson", "orders_statuses", "orders");
                itemsJson = LiveWriteFormBinder.Text(form, "itemsJson", "orders_items_statuses", "items");
                langCode = LiveWriteFormBinder.Text(form, "langCode", "lang_code");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save order statuses on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var host = context.Request.Host.Host;
            var domainPath = string.IsNullOrWhiteSpace(host) ? "http://localhost/" : "http://" + host + "/";
            var written = await writes.SaveAsync(ordersJson, itemsJson, langCode, domainPath, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/order-statuses-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpQuoteSaveNote, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpQuoteWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/quote-requests-app", "Admin CP capability required for quote notes.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpQuoteSaveNoteBody>(context, cancellationToken) ?? new();
            var quoteId = body.QuoteId;
            var note = body.AdminNote;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                quoteId = LiveWriteFormBinder.Long(form, "quoteId", "quote_id", "id");
                note = LiveWriteFormBinder.Text(form, "adminNote", "admin_note", "note");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save a quote admin note on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.SaveAdminNoteAsync(quoteId, note, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/quote-requests-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpQuoteSaveLines, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpQuoteWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/quote-requests-app", "Admin CP capability required for quote lines.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpQuoteSaveLinesBody>(context, cancellationToken) ?? new();
            var quoteId = body.QuoteId;
            var note = body.AdminNote;
            var linesJson = body.LinesJson ?? body.Lines;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                quoteId = LiveWriteFormBinder.Long(form, "quoteId", "quote_id", "id");
                note = LiveWriteFormBinder.Text(form, "adminNote", "admin_note", "note");
                linesJson = LiveWriteFormBinder.Text(form, "linesJson", "lines_json", "lines");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
                if (string.IsNullOrWhiteSpace(linesJson))
                {
                    var lineId = LiveWriteFormBinder.Long(form, "lineId", "line_id");
                    if (lineId > 0)
                    {
                        var one = new
                        {
                            id = lineId,
                            quotedPrice = LiveWriteFormBinder.DecOrNull(form, "quotedPrice", "quoted_price"),
                            quotedTimeToExe = LiveWriteFormBinder.IntOrNull(form, "quotedTimeToExe", "quoted_time_to_exe"),
                            lineAdminNote = LiveWriteFormBinder.Text(form, "lineAdminNote", "line_admin_note"),
                            offerAlternative = LiveWriteFormBinder.Flag(form, "offerAlternative", "offer_alternative"),
                            altManufacturer = LiveWriteFormBinder.Text(form, "altManufacturer", "alt_manufacturer"),
                            altArticle = LiveWriteFormBinder.Text(form, "altArticle", "alt_article"),
                            altName = LiveWriteFormBinder.Text(form, "altName", "alt_name"),
                            altCountNeed = LiveWriteFormBinder.IntOrNull(form, "altCountNeed", "alt_count_need"),
                            altQuotedPrice = LiveWriteFormBinder.DecOrNull(form, "altQuotedPrice", "alt_quoted_price"),
                            altStorageId = LiveWriteFormBinder.Long(form, "altStorageId", "alt_storage_id")
                        };
                        linesJson = JsonSerializer.Serialize(one) is { } oneJson
                            ? "[" + oneJson + "]"
                            : "[]";
                    }
                }
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save quote lines on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.SaveLinesAsync(quoteId, note, linesJson, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/quote-requests-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpQuoteSend, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpQuoteWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/quote-requests-app", "Admin CP capability required to send a quote.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpQuoteSendBody>(context, cancellationToken) ?? new();
            var quoteId = body.QuoteId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                quoteId = LiveWriteFormBinder.Long(form, "quoteId", "quote_id", "id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to send a quote on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.SendQuoteAsync(quoteId, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/quote-requests-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpVendorApprovals, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpVendorApprovalWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/users-app", "Admin CP capability required for vendor approvals.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpVendorApprovalsBody>(context, cancellationToken) ?? new();
            var id = body.Id;
            var action = body.Action;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "accountId", "account_id");
                action = LiveWriteFormBinder.Text(form, "action");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to approve, suspend, or reject a vendor on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.SetStatusAsync(id, action, session.UserId, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/users-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpPriceStorageRules, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpPriceStorageRuleWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/price-lists-app", "Admin CP capability required for price storage rules.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpPriceStorageRulesBody>(context, cancellationToken) ?? new();
            var kind = body.Action ?? body.Kind;
            var ruleId = body.RuleId;
            var storageId = body.StorageId;
            var manufacturer = body.Manufacturer;
            var article = body.Article;
            var margin = body.MarginPercent;
            var visible = body.Visible;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                kind = LiveWriteFormBinder.Text(form, "action", "kind");
                ruleId = LiveWriteFormBinder.Long(form, "ruleId", "rule_id", "id");
                storageId = LiveWriteFormBinder.Long(form, "storageId", "storage_id");
                manufacturer = LiveWriteFormBinder.Text(form, "manufacturer", "brand");
                article = LiveWriteFormBinder.Text(form, "article");
                margin = LiveWriteFormBinder.Text(form, "marginPercent", "margin_percent");
                visible = LiveWriteFormBinder.Flag(form, "visible") ? 1 : 0;
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save or delete a price storage rule on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.ApplyAsync(kind, ruleId, storageId, manufacturer, article, margin, visible, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/price-lists-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpContentPublished, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpContentManagerWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/pages-app", "Admin CP capability required for content publish.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpContentPublishedBody>(context, cancellationToken) ?? new();
            var contentId = body.ContentId;
            var published = body.PublishedFlag;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                contentId = LiveWriteFormBinder.Long(form, "contentId", "content_id", "id");
                published = LiveWriteFormBinder.Int(form, "publishedFlag", "published_flag", "published");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to change a content publish flag on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.SetPublishedAsync(contentId, published, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/pages-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpContentMain, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpContentManagerWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/pages-app", "Admin CP capability required for content main flag.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpContentMainBody>(context, cancellationToken) ?? new();
            var contentId = body.ContentId;
            var isFrontend = body.IsFrontend;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                contentId = LiveWriteFormBinder.Long(form, "contentId", "content_id", "id");
                isFrontend = LiveWriteFormBinder.IntOrNull(form, "isFrontend", "is_frontend") ?? 1;
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to set the main content page on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.SetMainAsync(contentId, isFrontend, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/pages-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpContentBody, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpContentManagerWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/pages-app", "Admin CP capability required for content body save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpContentBodyWriteBody>(context, cancellationToken) ?? new();
            var contentId = body.ContentId;
            var contentType = body.ContentType;
            var content = body.Content;
            var contentLangStrId = body.ContentLangStrId;
            var langCode = body.LangCode;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                contentId = LiveWriteFormBinder.Long(form, "contentId", "content_id", "id");
                contentType = LiveWriteFormBinder.Text(form, "contentType", "content_type");
                content = LiveWriteFormBinder.Text(form, "content");
                contentLangStrId = LiveWriteFormBinder.Text(form, "contentLangStrId", "content_lang_str_id");
                langCode = LiveWriteFormBinder.Text(form, "langCode", "lang_code");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save a content page body on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var host = context.Request.Host.Host;
            var domainPath = string.IsNullOrWhiteSpace(host) ? "http://localhost/" : "http://" + host + "/";
            var written = await writes.SaveBodyAsync(
                new CpContentBodySaveRequest(contentId, contentType, content, contentLangStrId, langCode, domainPath),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/pages-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpContentSave, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpContentManagerWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/pages-app", "Admin CP capability required for content metadata save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpContentMetaWriteBody>(context, cancellationToken) ?? new();
            var contentId = body.ContentId;
            var alias = body.Alias;
            var value = body.Value;
            var parent = body.Parent;
            var description = body.Description;
            var isFrontend = body.IsFrontend;
            var contentType = body.ContentType;
            var content = body.Content;
            var titleTag = body.TitleTag;
            var descriptionTag = body.DescriptionTag;
            var keywordsTag = body.KeywordsTag;
            var authorTag = body.AuthorTag;
            var mainFlag = body.MainFlag;
            var cssJs = body.CssJs;
            var robotsTag = body.RobotsTag;
            var publishedFlag = body.PublishedFlag;
            var groupsAccess = body.GroupsAccess;
            var valueLangStrId = body.ValueLangStrId;
            var descriptionLangStrId = body.DescriptionLangStrId;
            var contentLangStrId = body.ContentLangStrId;
            var titleLangStrId = body.TitleLangStrId;
            var descriptionTagLangStrId = body.DescriptionTagLangStrId;
            var keywordsLangStrId = body.KeywordsLangStrId;
            var authorLangStrId = body.AuthorLangStrId;
            var langCode = body.LangCode;
            var checkHash = body.CheckHash;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                contentId = LiveWriteFormBinder.Long(form, "contentId", "content_id", "id");
                alias = LiveWriteFormBinder.Text(form, "alias");
                value = LiveWriteFormBinder.Text(form, "value", "caption");
                parent = LiveWriteFormBinder.Long(form, "parent", "parentId", "parent_id");
                description = LiveWriteFormBinder.Text(form, "description");
                isFrontend = LiveWriteFormBinder.IntOrNull(form, "isFrontend", "is_frontend") ?? 1;
                contentType = LiveWriteFormBinder.Text(form, "contentType", "content_type");
                content = LiveWriteFormBinder.Text(form, "content");
                titleTag = LiveWriteFormBinder.Text(form, "titleTag", "title_tag");
                descriptionTag = LiveWriteFormBinder.Text(form, "descriptionTag", "description_tag");
                keywordsTag = LiveWriteFormBinder.Text(form, "keywordsTag", "keywords_tag");
                authorTag = LiveWriteFormBinder.Text(form, "authorTag", "author_tag");
                mainFlag = LiveWriteFormBinder.Flag(form, "mainFlag", "main_flag") ? 1 : LiveWriteFormBinder.Int(form, "mainFlag", "main_flag");
                cssJs = LiveWriteFormBinder.Text(form, "cssJs", "css_js");
                robotsTag = LiveWriteFormBinder.Text(form, "robotsTag", "robots_tag");
                publishedFlag = LiveWriteFormBinder.IntOrNull(form, "publishedFlag", "published_flag") ?? 1;
                groupsAccess = LiveWriteFormBinder.Text(form, "groupsAccess", "groups_access");
                valueLangStrId = LiveWriteFormBinder.Text(form, "valueLangStrId", "value_lang_str_id");
                descriptionLangStrId = LiveWriteFormBinder.Text(form, "descriptionLangStrId", "description_lang_str_id");
                contentLangStrId = LiveWriteFormBinder.Text(form, "contentLangStrId", "content_lang_str_id");
                titleLangStrId = LiveWriteFormBinder.Text(form, "titleLangStrId", "title_tag_lang_str_id");
                descriptionTagLangStrId = LiveWriteFormBinder.Text(form, "descriptionTagLangStrId", "description_tag_lang_str_id");
                keywordsLangStrId = LiveWriteFormBinder.Text(form, "keywordsLangStrId", "keywords_tag_lang_str_id");
                authorLangStrId = LiveWriteFormBinder.Text(form, "authorLangStrId", "author_tag_lang_str_id");
                langCode = LiveWriteFormBinder.Text(form, "langCode", "lang_code");
                checkHash = LiveWriteFormBinder.Text(form, "checkHash", "check_hash");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to create or save a content page on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var host = context.Request.Host.Host;
            var domainPath = string.IsNullOrWhiteSpace(host) ? "http://localhost/" : "http://" + host + "/";
            var written = await writes.SaveMetaAsync(
                new CpContentMetaSaveRequest(
                    contentId,
                    alias,
                    value,
                    parent,
                    description,
                    isFrontend,
                    contentType,
                    content,
                    titleTag,
                    descriptionTag,
                    keywordsTag,
                    authorTag,
                    mainFlag,
                    cssJs,
                    robotsTag,
                    publishedFlag,
                    groupsAccess,
                    valueLangStrId,
                    descriptionLangStrId,
                    contentLangStrId,
                    titleLangStrId,
                    descriptionTagLangStrId,
                    keywordsLangStrId,
                    authorLangStrId,
                    langCode,
                    domainPath,
                    checkHash),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/pages-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpContentTree, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpContentManagerWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/pages-app", "Admin CP capability required for content tree save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpContentTreeWriteBody>(context, cancellationToken) ?? new();
            var treeJson = body.TreeJson;
            var isFrontend = body.IsFrontend;
            var langCode = body.LangCode;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                treeJson = LiveWriteFormBinder.Text(form, "treeJson", "tree_json");
                isFrontend = LiveWriteFormBinder.IntOrNull(form, "isFrontend", "is_frontend") ?? 1;
                langCode = LiveWriteFormBinder.Text(form, "langCode", "lang_code");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to save the content tree on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var host = context.Request.Host.Host;
            var domainPath = string.IsNullOrWhiteSpace(host) ? "http://localhost/" : "http://" + host + "/";
            var written = await writes.SaveTreeAsync(
                new CpContentTreeSaveRequest(treeJson, isFrontend, langCode, domainPath),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/pages-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpMenusWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpMenuWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/menus-app", "Admin CP capability required for menu writes.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpMenusWriteBody>(context, cancellationToken) ?? new();
            var action = body.Action;
            var menuId = body.MenuId;
            var caption = body.Caption;
            var captionLangStrId = body.CaptionLangStrId;
            var menuUlClass = body.MenuUlClass;
            var menuUlId = body.MenuUlId;
            var isFrontend = body.IsFrontend;
            var treeJson = body.TreeJson ?? body.MenuTree;
            var ids = body.Ids ?? body.MenuList;
            var langCode = body.LangCode;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action", "save_action", "menu_action");
                menuId = LiveWriteFormBinder.Long(form, "menuId", "menu_id", "id");
                caption = LiveWriteFormBinder.Text(form, "caption", "menu_caption");
                captionLangStrId = LiveWriteFormBinder.Text(form, "captionLangStrId", "menu_caption_lang_str_id");
                menuUlClass = LiveWriteFormBinder.Text(form, "menuUlClass", "menu_ul_class");
                menuUlId = LiveWriteFormBinder.Text(form, "menuUlId", "menu_ul_id");
                isFrontend = LiveWriteFormBinder.IntOrNull(form, "isFrontend", "is_frontend") ?? 1;
                treeJson = LiveWriteFormBinder.Text(form, "treeJson", "menu_tree", "structure");
                ids = LiveWriteFormBinder.Text(form, "ids", "menu_list", "menuList");
                langCode = LiveWriteFormBinder.Text(form, "langCode", "lang_code");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to create, save, or delete a menu on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var host = context.Request.Host.Host;
            var domainPath = string.IsNullOrWhiteSpace(host) ? "http://localhost/" : "http://" + host + "/";
            var normalized = CpMenuWriteService.NormalizeAction(action);
            ErpSimpleWriteResult written;
            if (normalized == "delete")
            {
                written = await writes.DeleteAsync(ids, cancellationToken);
            }
            else
            {
                written = await writes.SaveAsync(
                    new CpMenuSaveRequest(
                        string.IsNullOrWhiteSpace(normalized) ? "create" : normalized,
                        menuId,
                        caption,
                        captionLangStrId,
                        menuUlClass,
                        menuUlId,
                        isFrontend,
                        treeJson,
                        langCode,
                        domainPath),
                    cancellationToken);
            }

            return LiveWriteFormBinder.Complete(
                context,
                "/cp/menus-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpModulesWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpModuleWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/modules-app", "Admin CP capability required for module writes.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpModulesWriteBody>(context, cancellationToken) ?? new();
            var action = body.Action;
            var moduleId = body.ModuleId;
            var prototypeId = body.PrototypeId;
            var prototypeNameLangStrId = body.PrototypeNameLangStrId;
            var caption = body.Caption;
            var captionLangStrId = body.CaptionLangStrId;
            var contentType = body.ContentType;
            var content = body.Content;
            var contentLangStrId = body.ContentLangStrId;
            var position = body.Position;
            var activated = body.Activated;
            var dataJson = body.DataJson ?? body.DataValue;
            var showCaption = body.ShowCaption;
            var sortOrder = body.SortOrder;
            var forAll = body.ForAll;
            var isFrontend = body.IsFrontend;
            var contentIds = body.ContentIds ?? body.ContentArray;
            var groupsAllowed = body.GroupsAllowed;
            var ids = body.Ids ?? body.ModulesList;
            var langCode = body.LangCode;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action", "module_save_action", "modules_action_type");
                moduleId = LiveWriteFormBinder.Long(form, "moduleId", "module_id", "id");
                prototypeId = LiveWriteFormBinder.Long(form, "prototypeId", "prototype_id");
                prototypeNameLangStrId = LiveWriteFormBinder.Text(form, "prototypeNameLangStrId", "prototype_name_lang_str_id");
                caption = LiveWriteFormBinder.Text(form, "caption");
                captionLangStrId = LiveWriteFormBinder.Text(form, "captionLangStrId", "caption_lang_str_id");
                contentType = LiveWriteFormBinder.Text(form, "contentType", "content_type");
                content = LiveWriteFormBinder.Text(form, "content");
                contentLangStrId = LiveWriteFormBinder.Text(form, "contentLangStrId", "content_lang_str_id");
                position = LiveWriteFormBinder.Text(form, "position");
                activated = LiveWriteFormBinder.IntOrNull(form, "activated", "flag_value") ?? 0;
                dataJson = LiveWriteFormBinder.Text(form, "dataJson", "data_value", "data");
                showCaption = LiveWriteFormBinder.Flag(form, "showCaption", "show_caption") ? 1 : LiveWriteFormBinder.Int(form, "showCaption", "show_caption");
                sortOrder = LiveWriteFormBinder.Int(form, "sortOrder", "order");
                forAll = LiveWriteFormBinder.Flag(form, "forAll", "for_all") ? 1 : LiveWriteFormBinder.Int(form, "forAll", "for_all");
                isFrontend = LiveWriteFormBinder.IntOrNull(form, "isFrontend", "is_frontend") ?? 1;
                contentIds = LiveWriteFormBinder.Text(form, "contentIds", "content_array");
                groupsAllowed = LiveWriteFormBinder.Text(form, "groupsAllowed", "groups_allowed");
                ids = LiveWriteFormBinder.Text(form, "ids", "modules_list", "modulesList");
                langCode = LiveWriteFormBinder.Text(form, "langCode", "lang_code");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to create, save, activate, or delete a module on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var host = context.Request.Host.Host;
            var domainPath = string.IsNullOrWhiteSpace(host) ? "http://localhost/" : "http://" + host + "/";
            var normalized = CpModuleWriteService.NormalizeAction(action);
            ErpSimpleWriteResult written;
            if (normalized == "delete")
            {
                written = await writes.DeleteAsync(ids, isFrontend, cancellationToken);
            }
            else if (normalized == "activate")
            {
                written = await writes.SetActivatedAsync(ids, activated, cancellationToken);
            }
            else
            {
                written = await writes.SaveAsync(
                    new CpModuleSaveRequest(
                        string.IsNullOrWhiteSpace(normalized) ? "create" : normalized,
                        moduleId,
                        prototypeId,
                        prototypeNameLangStrId,
                        caption,
                        captionLangStrId,
                        contentType,
                        content,
                        contentLangStrId,
                        position,
                        activated,
                        dataJson,
                        showCaption,
                        sortOrder,
                        forAll,
                        isFrontend,
                        contentIds,
                        groupsAllowed,
                        langCode,
                        domainPath),
                    cancellationToken);
            }

            return LiveWriteFormBinder.Complete(
                context,
                "/cp/modules-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

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
                note = "shop_currencies digest. Rate POST /cp/currencies/set-rate and available POST /cp/currencies/set-available when confirmWrites=true. Live FX stays PHP."
            });
        });
        endpoints.MapPost(EcomAeRoutes.CpCurrenciesSetRate, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpCurrencyWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/currencies-app", "Admin CP capability required for currency rate.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpCurrenciesSetRateBody>(context, cancellationToken)
                       ?? new();
            var iso = body.IsoCode;
            var rate = body.Rate;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                iso = LiveWriteFormBinder.Text(form, "isoCode", "iso_code");
                rate = LiveWriteFormBinder.Dec(form, "rate");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to write a currency rate on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.SetRateAsync(iso, rate, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/currencies-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.CpCurrenciesSetAvailable, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpCurrencyWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/currencies-app", "Admin CP capability required for currency available flags.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpCurrenciesSetAvailableBody>(context, cancellationToken)
                       ?? new();
            var isoCodes = body.IsoCodes ?? body.CurrenciesList;
            var available = body.Available;
            var shopCurrency = body.ShopCurrency;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                isoCodes = LiveWriteFormBinder.Text(form, "isoCodes", "iso_codes", "currencies_list", "currenciesList");
                available = LiveWriteFormBinder.Int(form, "available");
                shopCurrency = LiveWriteFormBinder.Text(form, "shopCurrency", "shop_currency");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to write currency available flags on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.SetAvailableAsync(isoCodes, available, shopCurrency, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/currencies-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

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
        endpoints.MapPost(EcomAeRoutes.CpApiClientsToggle, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpApiClientWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/api-clients-app", "Admin CP capability required for API client toggle.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpApiClientsToggleBody>(context, cancellationToken) ?? new();
            var id = body.ClientId;
            var action = body.Action;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "clientId", "client_id", "id");
                action = LiveWriteFormBinder.Text(form, "action");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to revoke or activate an API client on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var key = (action ?? string.Empty).Trim().ToLowerInvariant();
            var active = key is "activate" or "1" or "on" ? 1 : 0;
            if (key is not ("revoke" or "activate" or "0" or "1" or "on" or "off"))
            {
                active = -1;
            }

            var written = await writes.SetActiveAsync(id, active, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/api-clients-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

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
                note = "Read-only epc_power_bi_config + epc_power_bi_reports metadata. Open ?pbi_id= loads a 280-char notes excerpt plus category siblings. Configure/embed writes remain PHP epc_power_bi."
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
                note = "Read-only epc_metabase_config + epc_metabase_dashboards. Open ?mb_id= loads site URL plus category siblings. secret_key never returned. Writes remain PHP epc_metabase_embed."
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
                note = "Read-only epc_report_definitions metadata. Open ?report_id= loads a 280-char query excerpt plus runs. recipients/parameters omitted. Generate/schedule writes stay PHP."
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
            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound(new
                {
                    ok = false,
                    surface = "cp",
                    message = "Demo tenant fleet digest is Super CP only. Tenant CPs are independent."
                });
            }

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
                note = "epc_pos_settings + epc_pos_sales digest. open/close session, save settings, and sale/line INSERT write on POST /cp/pos/* when confirmWrites=true. Printable receipt at /cp/pos/receipt/{id}. Walk-in user create, tax-toolkit totals, ERP SO/invoice/voucher, inventory sale_out, product/customer search, and calc_cart are ASP.NET-live."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelTaxToolkits, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound(new
                {
                    ok = false,
                    surface = "cp",
                    message = "Tax toolkits digest is Super CP only. Tenant CPs are independent."
                });
            }

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
                note = "epc_tax_toolkits metadata (rules_json/reg_number omitted). Install and assign write on POST /cp/tax-toolkits/* when confirmWrites=true. Refresh, seed, and migrate-all stay PHP."
            });
        });

        endpoints.MapPost(EcomAeRoutes.ControlPanelTaxToolkitInstall, HandleTaxToolkitInstallAsync).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ControlPanelTaxToolkitAssign, HandleTaxToolkitAssignAsync).DisableAntiforgery();

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
                note = "shop_docpart_articles_analogs_list digest. Save/delete/add/search-delete POST /cp/crosses/write when confirmWrites=true. File import and crossbase stay PHP."
            });
        });
        endpoints.MapPost(EcomAeRoutes.CpCrossesWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpCrossWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/crosses-app", "Admin CP capability required for crosses write.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpCrossesWriteBody>(context, cancellationToken)
                       ?? new();
            var action = body.Action;
            var id = body.Id;
            var article = body.Article;
            var manufacturerArticle = body.ManufacturerArticle;
            var analog = body.Analog;
            var manufacturerAnalog = body.ManufacturerAnalog;
            var manufacturer = body.Manufacturer;
            var emptyOnly = body.EmptyOnly || body.Null == 1;
            var idFrom = body.IdFrom;
            var idBefore = body.IdBefore;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                id = LiveWriteFormBinder.Long(form, "id");
                article = LiveWriteFormBinder.Text(form, "article");
                manufacturerArticle = LiveWriteFormBinder.Text(form, "manufacturer_article", "manufacturerArticle");
                analog = LiveWriteFormBinder.Text(form, "analog");
                manufacturerAnalog = LiveWriteFormBinder.Text(form, "manufacturer_analog", "manufacturerAnalog");
                manufacturer = LiveWriteFormBinder.Text(form, "manufacturer");
                emptyOnly = LiveWriteFormBinder.Flag(form, "emptyOnly", "empty_only", "null");
                idFrom = LiveWriteFormBinder.Long(form, "idFrom", "id_from");
                idBefore = LiveWriteFormBinder.Long(form, "idBefore", "id_before");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to write crosses on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var key = (action ?? string.Empty).Trim();
            ErpSimpleWriteResult written = key switch
            {
                "save_crosses" or "save-crosses" or "save" =>
                    await writes.SaveAsync(id, article, manufacturerArticle, analog, manufacturerAnalog, cancellationToken),
                "del_crosses" or "delete_crosses" or "del-crosses" or "delete" =>
                    await writes.DeleteAsync(id, cancellationToken),
                "add_crosses" or "add-crosses" or "add" =>
                    await writes.AddAsync(article, manufacturerArticle, analog, manufacturerAnalog, cancellationToken),
                "del_search_crosses" or "delete_search_crosses" or "del-search-crosses" or "search-delete" =>
                    await writes.DeleteSearchAsync(article, manufacturer, emptyOnly, idFrom, idBefore, cancellationToken),
                _ => ErpSimpleWriteResult.Fail("invalid", "Unknown crosses action."),
            };
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/crosses-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

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
                note = "Read-only epc_industry_packs metadata. Open ?pack_id= loads a 280-char modules excerpt plus tenant assignments. GL/tax/theme JSON omitted. PHP industry_settings remains authoritative."
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
                note = "Read-only epc_pl_lists KPIs + lists (stats_json/error_text/stored_relpath omitted). PHP commerce profiles remain authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelDocpartPriceLists, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for Docpart price-lists digest.");
            }

            var result = await dashboards.BuildCpDocpartPriceListsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                lists = result.Lists,
                ways = CpPricesUploadWaysCatalog.All,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only shop_docpart_prices + linked warehouses. PC/FTP/e-mail/URL writes stay PHP."
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
                note = "Read-only epc_erp_pm_budgets KPIs + budgets. Open ?budget_id= loads 280-char note excerpt plus monthly lines. note omitted from the list. Save/add-line/advance write here."
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
                note = "Read-only epc_carrier_accounts + epc_carrier_shipments KPIs + carriers (config_json omitted; catalog region/blurb). Custom shipping save / submit write on POST /cp/custom-shipping/write when confirmWrites=true. PDF attach, box autofill, LGP, and schema-ensure stay PHP."
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
                note = "Read-only shop_payment_systems KPIs + gateways (anable=Enabled; active=Default; parameters/credentials omitted). PHP /CP/shop/payments/payments remains authoritative."
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
                note = "Read-only Integrations Hub catalog (key/label/blurb/category/configure_url; feature flags overlay). Not epc_webhooks. PHP epc_integrations_hub remains authoritative."
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
            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound(new
                {
                    ok = false,
                    surface = "cp",
                    message = "Platform governance digest is Super CP only. Tenant CPs are independent."
                });
            }

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
                note = "Read-only epc_platform_governance_rules. Open ?rule_id= loads a 280-char description excerpt plus category siblings. config_json omitted. PHP epc_platform_governance remains authoritative."
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
                note = "E-invoice documents digest. Seller/buyer/ASP profile POST /erp/ajax/einvoice-save-* when confirmWrites=true. Create/submit stay PHP."
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
                note = "Read-only epc_crm_tickets KPIs + tickets. Open ?ticket_id= loads 280-char message excerpts. Full bodies omitted. PHP CRM shell remains authoritative."
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
                note = "Read-only epc_marketing_* KPIs + reviews. Open ?review_id= loads a 280-char notes excerpt plus strategy siblings. PHP marketing growth / campaigns hub remains authoritative."
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
                note = "Read-only epc_fin_* KPIs + periods. Open ?period_id= loads company alloc/accrual/FX rows (basis/schedule/lines JSON omitted). Alloc run, accrual save, and FX revalue stay PHP."
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
                note = "Read-only epc_bc_* KPIs + proofs. Open ?proof_id= loads a 280-char payload excerpt and batch siblings. merkle_proof_json and batch meta_json omitted. Verify/anchor writes stay PHP."
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
                note = "epc_erp_wms_* KPIs + work pool. Open ?work_id= loads from/to location and LP. Location save/delete, receive, wave create/release, and work complete write when confirmWrites=true. Schema ensure stays PHP."
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
                note = "Read-only epc_ai_* KPIs + queries. Open ?query_id= loads a 280-char input excerpt plus service siblings. Output omitted. PHP epc_ai_service remains authoritative."
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
                note = "Read-only epc_ci_* KPIs + audit runs. Open ?run_id= loads a 280-char report excerpt plus same-day violation excerpts. IP omitted. PHP commerce isolation audit remains authoritative."
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
                note = "Read-only epc_cons_* KPIs + group entities. Open ?cons_id= loads figures and IC rows. Entity/figures/IC writes stay here. Consolidation run stays PHP."
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
                note = "Read-only epc_crm_activities KPIs + rows. Open ?activity_id= loads a 280-char notes excerpt plus related siblings. Notes omitted from the list. PHP CRM activities remain authoritative."
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
                note = "epc_dunning_* KPIs + queue (notes omitted). Status / payment / profile / add-invoice / process write on POST /cp/collections-dunning/write when confirmWrites=true. Schema-ensure stays PHP."
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
                note = "Read-only epc_marketplace_* KPIs + channels (config_json omitted; catalog family/region/api/blurb). PHP marketplace/channels remain authoritative."
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
                note = "Read-only epc_po_requests + approval_steps KPIs + requests. Open ?po_req_id= loads 280-char description/notes excerpts plus steps. items/attachments JSON omitted. Approve/reject write here."
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
                note = "Read-only epc_web_tracker_sessions/pageviews/events KPIs + sessions (ip/ua/meta_json omitted). Full dashboard at /cp/web-tracker/dashboard."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelWebTrackerDashboard, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for web-tracker dashboard.");
            }

            var host = context.Request.Host.Host;
            // Super tracker = platform host only — never derive from bos capability (tenant admins must not fleet-read).
            var isSuper = PlatformHostPolicy.IsSuperCpHost(host);
            var own = CpWebTrackerDashboardBuilder.ResolveOwnSiteKey(host);
            var q = context.Request.Query;
            var filters = CpWebTrackerDashboardBuilder.NormalizeFilters(
                q["site_key"], q["from"], q["to"], q["device"], q["country"], q["ip"],
                q["user_id"], q["user_type"], q["browser"], q["path"], isSuper, own);
            var result = await dashboards.BuildCpWebTrackerDashboardAsync(filters, cancellationToken);
            return Results.Ok(new
            {
                ok = result.Ok,
                site_key = result.SiteKey,
                from = result.FromUnix,
                to = result.ToUnix,
                filters = new
                {
                    device = result.Filters.Device,
                    country = result.Filters.Country,
                    ip = result.Filters.Ip,
                    user_id = result.Filters.UserId,
                    user_type = result.Filters.UserType,
                    path = result.Filters.Path,
                    browser = result.Filters.Browser,
                },
                is_super = result.IsSuper,
                db = result.Db,
                site_options = result.SiteOptions,
                data = new
                {
                    summary = new
                    {
                        sessions = result.Summary.Sessions,
                        visitors = result.Summary.Visitors,
                        pageviews = result.Summary.Pageviews,
                        events = result.Summary.Events,
                        clicks = result.Summary.Clicks,
                        searches = result.Summary.Searches,
                        guest_sessions = result.Summary.GuestSessions,
                        registered_sessions = result.Summary.RegisteredSessions,
                        avg_duration_ms = result.Summary.AvgDurationMs,
                        avg_pages = result.Summary.AvgPages,
                        bounce_rate = result.Summary.BounceRate,
                    },
                    daily = result.Daily.Select(x => new { date = x.Date, sessions = x.Sessions, pageviews = x.Pageviews }),
                    top_pages = result.TopPages.Select(x => new { path = x.Path, views = x.Views, sessions = x.Sessions, avg_time_ms = x.AvgTimeMs, avg_scroll = x.AvgScroll }),
                    geo = result.Geo.Select(x => new { country_code = x.CountryCode, country_name = x.CountryName, city = x.City, sessions = x.Sessions }),
                    devices = result.Devices.Select(x => new { device_type = x.DeviceType, browser = x.Browser, os = x.Os, sessions = x.Sessions }),
                    searches = result.Searches.Select(x => new { search_query = x.SearchQuery, search_context = x.SearchContext, hits = x.Hits, sessions = x.Sessions }),
                    top_clicks = result.TopClicks.Select(x => new { path = x.Path, element_tag = x.ElementTag, element_id = x.ElementId, element_text = x.ElementText, element_href = x.ElementHref, hits = x.Hits }),
                    referrers = result.Referrers.Select(x => new { host = x.Host, utm_source = x.UtmSource, utm_medium = x.UtmMedium, utm_campaign = x.UtmCampaign, sessions = x.Sessions }),
                    recent_sessions = result.RecentSessions.Select(x => new
                    {
                        id = x.Id,
                        session_uid = x.SessionUid,
                        site_key = x.SiteKey,
                        hostname = x.Hostname,
                        user_id = x.UserId,
                        is_registered = x.IsRegistered ? 1 : 0,
                        first_seen_at = x.FirstSeenAt,
                        last_seen_at = x.LastSeenAt,
                        pageview_count = x.PageviewCount,
                        event_count = x.EventCount,
                        duration_ms = x.DurationMs,
                        landing_path = x.LandingPath,
                        exit_path = x.ExitPath,
                        country_code = x.CountryCode,
                        country_name = x.CountryName,
                        city = x.City,
                        region = x.Region,
                        device_type = x.DeviceType,
                        browser = x.Browser,
                        os = x.Os,
                        ip = x.Ip,
                        referrer_host = x.ReferrerHost,
                        utm_source = x.UtmSource,
                    }),
                    by_tenant = result.ByTenant.Select(x => new { site_key = x.SiteKey, hostname = x.Hostname, sessions = x.Sessions, pageviews = x.Pageviews, visitors = x.Visitors }),
                    facets = new
                    {
                        countries = result.Facets.Countries.Select(x => new { country_code = x.Value, country_name = x.Label, sessions = x.Sessions }),
                        devices = result.Facets.Devices.Select(x => new { device_type = x.Value, sessions = x.Sessions }),
                        browsers = result.Facets.Browsers.Select(x => new { browser = x.Value, sessions = x.Sessions }),
                    },
                },
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "PHP-parity web tracker dashboard over epc_web_tracker_* (filters + facets + charts)."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelWebTrackerSession, async (
            HttpContext context,
            long? id,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for web-tracker session detail.");
            }

            var host = context.Request.Host.Host;
            var isSuper = PlatformHostPolicy.IsSuperCpHost(host);
            var own = CpWebTrackerDashboardBuilder.ResolveOwnSiteKey(host);
            var siteKey = (string?)context.Request.Query["site_key"] ?? string.Empty;
            if (!isSuper)
            {
                siteKey = own;
            }

            var detail = await dashboards.BuildCpWebTrackerSessionDetailAsync(id ?? 0, siteKey, isSuper, cancellationToken);
            if (!detail.Ok || detail.Session is null)
            {
                return Results.Json(new { ok = false, error = "not_found", message = detail.Message }, statusCode: 404);
            }

            var s = detail.Session;
            return Results.Ok(new
            {
                ok = true,
                detail = new
                {
                    session = new
                    {
                        id = s.Id,
                        session_uid = s.SessionUid,
                        site_key = s.SiteKey,
                        hostname = s.Hostname,
                        user_id = s.UserId,
                        is_registered = s.IsRegistered ? 1 : 0,
                        first_seen_at = s.FirstSeenAt,
                        last_seen_at = s.LastSeenAt,
                        pageview_count = s.PageviewCount,
                        event_count = s.EventCount,
                        duration_ms = s.DurationMs,
                        landing_path = s.LandingPath,
                        exit_path = s.ExitPath,
                        country_code = s.CountryCode,
                        country_name = s.CountryName,
                        city = s.City,
                        region = s.Region,
                        device_type = s.DeviceType,
                        browser = s.Browser,
                        os = s.Os,
                        ip = s.Ip,
                        referrer_host = s.ReferrerHost,
                        utm_source = s.UtmSource,
                    },
                    pageviews = detail.Pageviews.Select(p => new
                    {
                        id = p.Id,
                        ts = p.Ts,
                        path = p.Path,
                        query = p.Query,
                        title = p.Title,
                        time_on_page_ms = p.TimeOnPageMs,
                        scroll_max_pct = p.ScrollMaxPct,
                        load_time_ms = p.LoadTimeMs,
                    }),
                    events = detail.Events.Select(e => new
                    {
                        id = e.Id,
                        ts = e.Ts,
                        event_type = e.EventType,
                        path = e.Path,
                        search_query = e.SearchQuery,
                        search_context = e.SearchContext,
                        element_tag = e.ElementTag,
                        element_id = e.ElementId,
                        element_text = e.ElementText,
                        element_href = e.ElementHref,
                        x = e.X,
                        y = e.Y,
                    }),
                },
                session = SessionPayload(session),
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelWebTrackerCsv, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for web-tracker CSV.");
            }

            var host = context.Request.Host.Host;
            var isSuper = PlatformHostPolicy.IsSuperCpHost(host);
            var own = CpWebTrackerDashboardBuilder.ResolveOwnSiteKey(host);
            var q = context.Request.Query;
            var filters = CpWebTrackerDashboardBuilder.NormalizeFilters(
                q["site_key"], q["from"], q["to"], q["device"], q["country"], q["ip"],
                q["user_id"], q["user_type"], q["browser"], q["path"], isSuper, own);
            var result = await dashboards.BuildCpWebTrackerDashboardAsync(filters, cancellationToken);
            var csv = CpWebTrackerDashboardBuilder.BuildCsv(result);
            var label = result.SiteKey is "_all" or "" ? "all" : result.SiteKey;
            var fname = $"web-tracker-{label}-{DateTimeOffset.FromUnixTimeSeconds(result.FromUnix):yyyyMMdd}-{DateTimeOffset.FromUnixTimeSeconds(result.ToUnix):yyyyMMdd}.csv";
            return Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fname);
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
                note = "Read-only epc_platform_comm_settings + epc_platform_internal_tasks. Open ?task_id= loads a 280-char description excerpt plus category siblings. PHP super CP communication remains authoritative."
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
                note = "Read-only epc_platform_info_blocks KPIs + blocks. Open ?block_id= loads a 280-char content excerpt plus placement siblings. PHP info blocks CMS remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelFreeTools, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound(new
                {
                    ok = false,
                    surface = "cp",
                    message = "Free tools digest is Super CP only. Tenant CPs are independent."
                });
            }

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
                note = "Read-only epc_free_tool_accounts/saves/settings KPIs + accounts. Open ?account_id= loads last-login plus saved-tool titles. token/pass_hash/del_code_hash/payload omitted. PHP free tools admin remains authoritative."
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
                note = "Read-only epc_config_snapshots + epc_sandbox_changes. Open ?snapshot_id= loads a 280-char config excerpt plus change keys. old_value/new_value omitted. PHP config sandbox remains authoritative."
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
                note = "Read-only epc_marketplace_apps/installs/reviews KPIs + apps. Open ?app_id= loads a 280-char description excerpt plus installs/reviews. features/config/review_text omitted. Install/review writes stay PHP."
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
                note = "Read-only epc_notifications + epc_notification_prefs KPIs + notifications. Open ?notif_id= loads a 280-char body excerpt plus category siblings. metadata omitted. PHP notification settings remain authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelPortalSettings, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            // Multi-site fleet + deploy targets are Super CP only — never on tenant hosts.
            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound(new
                {
                    ok = false,
                    surface = "cp",
                    message = "Portal settings fleet digest is Super CP only. Tenant CPs are independent."
                });
            }

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
                note = "Super-CP-only read-only epc_portal_site_settings + epc_portal_deploy_targets KPIs + sites (contact_json/enabled_packs_json/theme_json/cp_menu_json/erp_modules_json omitted). Tenant hosts receive 404. PHP portal settings remain authoritative."
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
                note = "shop_geo + shop_offices_geo_map KPIs + nodes (raw lang string bodies; value stored as lang id). Tree save is POST /cp/geo-regions/write."
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
                note = "shop_docpart_filter KPIs + filters (list_storages JSON). Add/save/delete/activate/scope is POST /cp/product-filters/write."
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
                note = "shop_docpart_search_tabs KPIs + tabs. Open ?tab_id= loads 280-char parameters_values excerpt. Full JSON omitted. Activate/save is POST /cp/search-tabs/write."
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
                note = "Read-only users_vin KPIs + requests. Open ?vin_id= loads 280-char request and message excerpts. Full HTML omitted. Mark viewed writes here."
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
                note = "text_for_url KPIs + texts. Open ?text_id= loads 280-char content and description excerpts. Full HTML omitted from the list. Save is POST /cp/additional-texts/write; delete is POST /cp/additional-texts/delete."
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
                note = "slider_images + slider_setings KPIs + images. Settings/move/delete/path-add is POST /cp/slider-banners/write. File upload stays Classic."
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
            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound(new
                {
                    ok = false,
                    surface = "cp",
                    message = "Failover status digest is Super CP only. Tenant CPs are independent."
                });
            }

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
                note = "Read-only control_groups + control_items KPIs + items. Open ?item_id= loads group siblings. Guide HTML omitted. PHP Ops guides remains authoritative."
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

        endpoints.MapGet(EcomAeRoutes.ControlPanelDebugConsole, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for debug-console digest.");
            }

            var result = await dashboards.BuildCpDebugConsoleDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                files = result.Files,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only metadata for allowlisted debug tmp basenames (dmY_Hi.php). No file contents; no LFI. PHP Debug console remains authoritative."
            });
        });


        endpoints.MapGet(EcomAeRoutes.ControlPanelStatistics, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for statistics digest.");
            }

            var result = await dashboards.BuildCpStatisticsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                rows = result.Rows,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only shop_orders + shop_stat_article_queries KPIs (ip omitted). PHP shop/statistics remains authoritative for writes."
            });
        });


        endpoints.MapGet(EcomAeRoutes.ControlPanelAccessories, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for accessories digest.");
            }

            var result = await dashboards.BuildCpAccessoriesDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                rows = result.Rows,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Listing save/status/delete POST /cp/accessories/listings/write, photo filename attach POST /cp/accessories/photos, and taxonomy POST /cp/accessories/taxonomy/write when confirmWrites=true. Multipart photo bytes stay PHP."
            });
        });


        endpoints.MapGet(EcomAeRoutes.ControlPanelSynonyms, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for synonyms digest.");
            }

            var result = await dashboards.BuildCpSynonymsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                rows = result.Rows,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Manufacturer synonyms digest. Add/save/del POST /cp/synonyms/write when confirmWrites=true. PHP manufacturers_synonyms remains the compare twin."
            });
        });
        endpoints.MapPost(EcomAeRoutes.CpSynonymsWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpManufacturerSynonymWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/synonyms-app", "Admin CP capability required for synonyms write.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<CpSynonymsWriteBody>(context, cancellationToken)
                       ?? new();
            var action = body.Action;
            var id = body.Id;
            var name = body.Name;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                id = LiveWriteFormBinder.Long(form, "id", "manufacturerId", "manufacturer_id");
                name = LiveWriteFormBinder.Text(form, "name", "synonym");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    status = "dry-run",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = true,
                    validation_code = "dry_run",
                    message = "Set confirmWrites=true to write manufacturer synonyms on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var key = (action ?? string.Empty).Trim();
            ErpSimpleWriteResult written = key switch
            {
                "add_manufacturer" or "add-manufacturer" =>
                    await writes.AddManufacturerAsync(name, cancellationToken),
                "save_manufacturer" or "save-manufacturer" =>
                    await writes.SaveManufacturerAsync(id, name, cancellationToken),
                "del_manufacturer" or "delete_manufacturer" or "del-manufacturer" =>
                    await writes.DeleteManufacturerAsync(id, cancellationToken),
                "add_synonym" or "add-synonym" =>
                    await writes.AddSynonymAsync(id, name, cancellationToken),
                "save_synonym" or "save-synonym" =>
                    await writes.SaveSynonymAsync(id, name, cancellationToken),
                "del_synonym" or "delete_synonym" or "del-synonym" =>
                    await writes.DeleteSynonymAsync(id, cancellationToken),
                _ => ErpSimpleWriteResult.Fail("invalid", "Unknown synonyms action."),
            };
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/synonyms-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();


        endpoints.MapGet(EcomAeRoutes.ControlPanelSeo, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for seo digest.");
            }

            var result = await dashboards.BuildCpSeoDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                rows = result.Rows,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only content frontend SEO KPIs. Sitemap detail at /cp/sitemap-app; ping/warm remain PHP shop/marketing/seo."
            });
        });


        endpoints.MapGet(EcomAeRoutes.ControlPanelSocialHub, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for social-hub digest.");
            }

            var result = await dashboards.BuildCpSocialHubDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                rows = result.Rows,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_social_accounts/drafts. Open ?social_id= loads last-test plus draft caption excerpts. encrypted_credentials omitted. Publish/save remain portal_social dry-run."
            });
        });


        endpoints.MapGet(EcomAeRoutes.ControlPanelTenantFeatures, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound(new
                {
                    ok = false,
                    surface = "cp",
                    message = "Tenant features digest is Super CP only. Tenant CPs are independent."
                });
            }

            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for tenant-features digest.");
            }

            var result = await dashboards.BuildCpTenantFeaturesDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                rows = result.Rows,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_tenant_feature_flags matrix. save_feature_flags remains portal_integrations dry-run. Super-only Blazor app."
            });
        });


        endpoints.MapGet(EcomAeRoutes.ControlPanelCustomerBoard, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound(new
                {
                    ok = false,
                    surface = "cp",
                    message = "Customer board digest is Super CP only. Tenant CPs are independent."
                });
            }

            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for customer-board digest.");
            }

            var result = await dashboards.BuildCpCustomerBoardDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                rows = result.Rows,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only users peek for Super customer board. Open ?user_id= loads last visit, confirm flags, and group binds. Password omitted. Super-only Blazor app."
            });
        });


        endpoints.MapGet(EcomAeRoutes.ControlPanelFulfillmentQueue, async (
            HttpContext context,
            int? limit,
            string? status,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for fulfillment-queue digest.");
            }

            var result = await dashboards.BuildCpFulfillmentQueueDigestAsync(limit ?? 200, cancellationToken, status);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                rows = result.Rows,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "epc_fulfillment_orders digest. transition / assign / pick / pack / wave / queue-from-order write on POST /cp/fulfillment-queue/write when confirmWrites=true. Printable packing slip at /cp/fulfillment-queue/packing-slip/{id}. Document-control branded PDF templates stay PHP."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelFulfillmentQueueDetailDigest, async (
            HttpContext context,
            long fulfillmentId,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for fulfillment-queue detail digest.");
            }

            var detail = await dashboards.GetCpFulfillmentDetailAsync(fulfillmentId, cancellationToken);
            if (detail is null)
            {
                return Results.NotFound(new { ok = false, message = "Fulfillment order not found." });
            }

            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                fulfillment = detail,
                items = detail.Items,
                source = detail.Source,
                message = detail.Message,
                session = SessionPayload(session),
                note = "PHP epc_fulfillment_get digest. transition / assign / pick / pack / wave / queue-from-order write on POST /cp/fulfillment-queue/write when confirmWrites=true. Printable packing slip at /cp/fulfillment-queue/packing-slip/{id}."
            });
        });


        endpoints.MapGet(EcomAeRoutes.ControlPanelSsoSaml, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound(new
                {
                    ok = false,
                    surface = "cp",
                    message = "SSO/SAML digest is Super CP only. Tenant CPs are independent."
                });
            }

            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for sso-saml digest.");
            }

            var result = await dashboards.BuildCpSsoSamlDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                rows = result.Rows,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_sso_providers/sessions (certs/metadata_xml omitted). Writes remain PHP. Super-only Blazor app."
            });
        });


        endpoints.MapGet(EcomAeRoutes.ControlPanelEventBus, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound(new
                {
                    ok = false,
                    surface = "cp",
                    message = "Event bus digest is Super CP only. Tenant CPs are independent."
                });
            }

            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for event-bus digest.");
            }

            var result = await dashboards.BuildCpEventBusDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "cp",
                summary = result.Summary,
                rows = result.Rows,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only MySQL epc_events peek (no Kafka/Rabbit). Open ?event_id= loads a 280-char payload excerpt. Webhook dispatch stays PHP. Super-only Blazor app."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelShopModuleCoverage, () =>
            Results.Ok(CpShopModuleRouteMap.BuildCoverageReport()));

        endpoints.MapGet(EcomAeRoutes.ControlPanelTopLevelAreaCoverage, () =>
            Results.Ok(CpTopLevelAreaRouteMap.BuildCoverageReport()));

        // /cp (+ /cp/control /CP) owned by Blazor CpCommandCentreApp (AdminSurfaceAuthGateMiddleware).
        // Do not MapGet those aliases here — they AmbiguousMatch with @page routes.
    }

    private static async Task<(long PoId, int Tier, string Comment, bool Confirm)> BindPoApprovalAsync(
        HttpContext context,
        CpPoApprovalBody? body,
        CancellationToken cancellationToken)
    {
        body ??= new();
        var poId = body.PoId;
        var tier = body.Tier;
        var comment = body.Comment ?? body.Reason ?? string.Empty;
        var confirm = body.ConfirmWrites;
        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken);
            poId = LiveWriteFormBinder.Long(form, "poId", "po_id", "id");
            tier = LiveWriteFormBinder.Int(form, "tier", "currentTier", "current_tier");
            comment = LiveWriteFormBinder.Text(form, "comment", "reason");
            confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
        }

        return (poId, tier, comment, confirm);
    }

    private static CpOmsItemWritePatch ReadOmsItemPatch(IFormCollection form, long itemId)
        => new(
            itemId,
            LiveWriteFormBinder.DecOrNull(form, "price"),
            LiveWriteFormBinder.IntOrNull(form, "countNeed", "count_need"),
            LiveWriteFormBinder.DecOrNull(form, "purchase", "t2_price_purchase"),
            LiveWriteFormBinder.IntOrNull(form, "storageId", "t2_storage_id", "storage_id"),
            NullIfEmpty(LiveWriteFormBinder.Text(form, "name", "t2_name")),
            NullIfEmpty(LiveWriteFormBinder.Text(form, "manufacturer", "t2_manufacturer", "brand")),
            NullIfEmpty(LiveWriteFormBinder.Text(form, "article", "t2_article")),
            NullIfEmpty(LiveWriteFormBinder.Text(form, "articleShow", "t2_article_show")),
            LiveWriteFormBinder.Flag(form, "repriceFromWarehouse", "reprice_from_warehouse", "apply_warehouse_price"));

    private static List<CpOmsItemWritePatch> ReadOmsItemPatches(IFormCollection form)
    {
        var raw = LiveWriteFormBinder.Text(form, "items", "itemsJson", "items_json");
        if (raw.Length > 0)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var fromJson = new List<CpOmsItemWritePatch>();
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (el.ValueKind != System.Text.Json.JsonValueKind.Object)
                        {
                            continue;
                        }

                        var itemId = JsonLong(el, "item_id", "itemId");
                        if (itemId <= 0)
                        {
                            continue;
                        }

                        fromJson.Add(new CpOmsItemWritePatch(
                            itemId,
                            JsonDec(el, "price"),
                            JsonInt(el, "count_need", "countNeed"),
                            JsonDec(el, "t2_price_purchase", "purchase"),
                            JsonInt(el, "t2_storage_id", "storageId", "storage_id"),
                            JsonText(el, "t2_name", "name"),
                            JsonText(el, "t2_manufacturer", "manufacturer"),
                            JsonText(el, "t2_article", "article"),
                            JsonText(el, "t2_article_show", "articleShow"),
                            JsonFlag(el, "reprice_from_warehouse", "repriceFromWarehouse")));
                    }

                    if (fromJson.Count > 0)
                    {
                        return fromJson;
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Fall through to single-item fields.
            }
        }

        var one = ReadOmsItemPatch(form, LiveWriteFormBinder.Long(form, "itemId", "item_id"));
        return one.ItemId > 0 ? [one] : [];
    }

    private static string? NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static long JsonLong(System.Text.Json.JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out var prop) && prop.TryGetInt64(out var value))
            {
                return value;
            }
        }

        return 0;
    }

    private static int? JsonInt(System.Text.Json.JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out var prop) && prop.TryGetInt32(out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static decimal? JsonDec(System.Text.Json.JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out var prop) && prop.TryGetDecimal(out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? JsonText(System.Text.Json.JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out var prop) && prop.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var text = prop.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }
        }

        return null;
    }

    private static bool JsonFlag(System.Text.Json.JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == System.Text.Json.JsonValueKind.True)
            {
                return true;
            }

            if (prop.ValueKind == System.Text.Json.JsonValueKind.Number && prop.TryGetInt32(out var n) && n != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<IResult> HandleTaxToolkitInstallAsync(
        HttpContext context,
        ILegacySessionValidator validator,
        ICpTaxToolkitInstallDryRun dryRun,
        ICpTaxToolkitWriteService writes,
        CancellationToken cancellationToken)
    {
        if (!SuperCpHostGate.IsAllowed(context))
        {
            return Results.NotFound(new { ok = false, surface = "cp", message = "Tax toolkit install is Super CP only." });
        }

        var session = await validator.ValidateAsync(context, cancellationToken);
        if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
        {
            return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/tax-toolkits-app", "Admin CP capability required for tax toolkit install.");
        }

        var kitCode = "";
        var setDefault = false;
        var confirm = false;
        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken);
            kitCode = LiveWriteFormBinder.Text(form, "kit_code", "kitCode");
            setDefault = LiveWriteFormBinder.Flag(form, "set_default", "setDefault");
            confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
        }
        else
        {
            var root = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<System.Text.Json.JsonElement>(context, cancellationToken);
            if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                kitCode = JsonText(root, "kit_code", "kitCode") ?? "";
                setDefault = JsonFlag(root, "set_default", "setDefault");
                confirm = JsonFlag(root, "confirmWrites", "confirm_writes");
            }
        }

        if (!confirm)
        {
            return Results.Ok(dryRun.Evaluate(new CpTaxToolkitInstallRequest(kitCode, setDefault, false)).ToPayload(SessionPayload(session)));
        }

        var written = await writes.InstallAsync(
            new CpTaxToolkitInstallWriteRequest(kitCode, setDefault, session.UserId),
            cancellationToken);
        return LiveWriteFormBinder.Complete(
            context,
            "/cp/tax-toolkits-app",
            written.Succeeded,
            written.Message,
            new
            {
                ok = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                kit_code = written.KitCode,
                id = written.Id,
                session = SessionPayload(session),
            });
    }

    private static async Task<IResult> HandleTaxToolkitAssignAsync(
        HttpContext context,
        ILegacySessionValidator validator,
        ICpTaxToolkitAssignDryRun dryRun,
        ICpTaxToolkitWriteService writes,
        CancellationToken cancellationToken)
    {
        if (!SuperCpHostGate.IsAllowed(context))
        {
            return Results.NotFound(new { ok = false, surface = "cp", message = "Tax toolkit assign is Super CP only." });
        }

        var session = await validator.ValidateAsync(context, cancellationToken);
        if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
        {
            return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=/cp/tax-toolkits-app", "Admin CP capability required for tax toolkit assign.");
        }

        var kitCode = "";
        var country = "";
        var siteKey = "platform";
        var confirm = false;
        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken);
            kitCode = LiveWriteFormBinder.Text(form, "kit_code", "kitCode");
            country = LiveWriteFormBinder.Text(form, "country_code", "countryCode");
            siteKey = LiveWriteFormBinder.Text(form, "site_key", "siteKey");
            confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
        }
        else
        {
            var root = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<System.Text.Json.JsonElement>(context, cancellationToken);
            if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                kitCode = JsonText(root, "kit_code", "kitCode") ?? "";
                country = JsonText(root, "country_code", "countryCode") ?? "";
                siteKey = JsonText(root, "site_key", "siteKey") ?? "platform";
                confirm = JsonFlag(root, "confirmWrites", "confirm_writes");
            }
        }

        if (!confirm)
        {
            return Results.Ok(dryRun.Evaluate(new CpTaxToolkitAssignRequest(country, kitCode, siteKey, false)).ToPayload(SessionPayload(session)));
        }

        var written = await writes.AssignTenantAsync(
            new CpTaxToolkitAssignWriteRequest(country, kitCode, siteKey, "", session.UserId),
            cancellationToken);
        return LiveWriteFormBinder.Complete(
            context,
            "/cp/tax-toolkits-app",
            written.Succeeded,
            written.Message,
            new
            {
                ok = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                kit_code = written.KitCode,
                id = written.Id,
                session = SessionPayload(session),
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
        has_backend_access = session.HasBackendAccess,
        capabilities = session.Capabilities,
        module_acl = session.Modules,
        permissions = session.Permissions
    };

    private sealed record CpCreditLimitSetBody(
        string? SiteKey = null,
        int CustomerId = 0,
        decimal Limit = 0,
        string? Currency = null,
        bool ConfirmWrites = false);
    private sealed record CpPoApprovalBody(
        long PoId = 0,
        int Tier = 1,
        string? Comment = null,
        string? Reason = null,
        bool ConfirmWrites = false);
    private sealed record CpOmsSetItemStatusBody(long OrderId = 0, long ItemId = 0, int Status = 0, bool ConfirmWrites = false);
    private sealed record CpOmsSetItemsStatusBody(long OrderId, int Status, IReadOnlyList<long>? ItemIds, bool ConfirmWrites = false);
    private sealed record CpOmsSendMessageBody(long OrderId, string? Text, long? ItemId = null, bool ConfirmWrites = false);
    private sealed record CpOmsSetCourierBody(long OrderId, decimal DeliveryPrice, string? Country = null, bool ConfirmWrites = false);
    private sealed record CpOmsDeleteOrdersBody(IReadOnlyList<long>? OrderIds, bool ConfirmWrites = false);
    private sealed record CpOmsAddCommentBody(long OrderId, string? Text, bool ConfirmWrites = false);
    private sealed record CpOmsSetViewedBody(IReadOnlyList<long>? OrderIds, int ViewedFlag = 1, bool ConfirmWrites = false);
    private sealed record CpOmsUpdateItemBody(
        long OrderId,
        long ItemId,
        decimal? Price = null,
        int? CountNeed = null,
        string? Manufacturer = null,
        string? Article = null,
        int? StorageId = null,
        bool ConfirmWrites = false,
        decimal? Purchase = null,
        string? Name = null,
        string? ArticleShow = null,
        bool RepriceFromWarehouse = false);
    private sealed record CpOmsPayRefundBody(long OrderId, bool DirectRefund, decimal? PaidSum = null, bool ConfirmWrites = false);
    private sealed record CpOmsUpdateItemsItemBody(
        long ItemId,
        decimal? Price = null,
        int? CountNeed = null,
        decimal? Purchase = null,
        int? StorageId = null,
        string? Name = null,
        string? Manufacturer = null,
        string? Article = null,
        string? ArticleShow = null,
        bool RepriceFromWarehouse = false);
    private sealed record CpOmsUpdateItemsBody(long OrderId, IReadOnlyList<CpOmsUpdateItemsItemBody>? Items, bool ConfirmWrites = false);
    private sealed record CpOmsFulfillmentSetStageBody(long OrderId, string? SupplierKey, string? Stage, bool ConfirmWrites = false);
    private sealed record CpOmsFulfillmentAdvanceBody(long OrderId, string? SupplierKey, bool ConfirmWrites = false);
    private sealed record CpOmsRefreshItemCostBody(long OrderId, long ItemId, bool ConfirmWrites = false);
    private sealed record CpFulfillmentQueueWriteBody(
        string? Action = null,
        bool ConfirmWrites = false,
        long FulfillmentId = 0,
        long ItemId = 0,
        long AssignedTo = 0,
        string? AssignedName = null,
        string? Status = null,
        string? PickStatus = null,
        int QtyPicked = 0,
        int QtyPacked = 0,
        string? Carrier = null,
        string? TrackingNumber = null,
        string? SiteKey = null,
        IReadOnlyList<long>? FulfillmentIds = null,
        long OrderId = 0,
        string? OrderNumber = null,
        string? CustomerName = null,
        string? Priority = null,
        string? Warehouse = null,
        int TotalItems = 0,
        decimal TotalWeight = 0,
        string? ShipAddressJson = null,
        string? Notes = null,
        string? ShippingMethod = null,
        IReadOnlyList<CpFulfillmentQueueLineInput>? Items = null,
        string? ItemsJson = null);
    private sealed record CpCustomShippingWriteBody(
        string? Action = null,
        bool ConfirmWrites = false,
        long Id = 0,
        string? Category = null,
        string? DeclarationType = null,
        string? Status = null,
        string? Company = null,
        string? CustomsEmirate = null,
        string? EntryDate = null,
        string? DeclarationDate = null,
        string? DeclarationNumber = null,
        string? BlNumber = null,
        string? BlDate = null,
        string? SrvNumber = null,
        string? LcDcNumber = null,
        string? LdPoNumber = null,
        string? SupplierDetail = null,
        string? Currency = null,
        decimal InvoiceAmountAed = 0,
        decimal TotalCostAed = 0,
        string? Remarks = null,
        string? ItemsJson = null);
    private sealed record CpCollectionsDunningWriteBody(
        string? Action = null,
        bool ConfirmWrites = false,
        long QueueId = 0,
        string? Status = null,
        string? Notes = null,
        decimal Amount = 0,
        string? SiteKey = null,
        string? Name = null,
        string? StepsJson = null,
        long CustomerId = 0,
        string? CustomerName = null,
        string? InvoiceRef = null,
        decimal InvoiceAmount = 0,
        decimal? AmountDue = null,
        string? DueDate = null,
        long ProfileId = 0);
    private sealed record CpPosOpenSessionBody(
        string? Action = null,
        bool ConfirmWrites = false,
        decimal OpeningFloat = 0,
        string? RegisterName = null);
    private sealed record CpPosCloseSessionBody(
        string? Action = null,
        bool ConfirmWrites = false,
        long SessionId = 0,
        decimal ClosingCash = 0,
        string? Notes = null);
    private sealed record CpPosCompleteSaleBody(
        string? Action = null,
        bool ConfirmWrites = false,
        long SessionId = 0,
        JsonElement Lines = default,
        string? LinesJson = null,
        string? PaymentMethod = null,
        decimal CashAmount = 0,
        decimal CardAmount = 0,
        decimal TaxRate = 0,
        string? TaxKitCode = null,
        long CustomerUserId = 0,
        long ContactId = 0,
        string? CustomerLabel = null,
        string? SaleNotes = null,
        string? Name = null,
        decimal Qty = 0,
        decimal UnitPriceEx = 0,
        string? Sku = null,
        long WarehouseId = 0);
    private sealed record CpPosSearchBody(string? Q = null, string? Query = null);
    private sealed record CpPosCalcCartBody(
        JsonElement Lines = default,
        string? LinesJson = null,
        long CustomerUserId = 0,
        long ContactId = 0);
    private sealed record CpPosSaveSettingsBody(
        string? Action = null,
        bool ConfirmWrites = false,
        bool PosEnabled = false,
        string? RegisterName = null,
        int DefaultWarehouseId = 0,
        int DefaultCashAccountId = 0,
        int DefaultCardAccountId = 0,
        string? ReceiptHeader = null,
        string? ReceiptFooter = null);
    private sealed record CpPortalSaveSettingsBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record CpPortalDeploySiteBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record CpCrmActionBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record CpModuleAjaxWriteRegistryBody(bool ConfirmWrites = false);
    private sealed record CpModuleAjaxWriteDedicatedBody(bool ConfirmWrites = false);
    private sealed record CpLangSetIsCustomBody(string? Action = null, bool ConfirmWrites = false, string? StrKey = null, int IsCustom = -1);
    private sealed record CpLangSetIsErrorBody(string? Action = null, bool ConfirmWrites = false, string? StrKey = null, int IsError = -1);
    private sealed record CpLangSetSameBody(string? Action = null, bool ConfirmWrites = false, string? StrKey = null, string? Same = null);
    private sealed record CpLangSetUsedFoundBody(string? Action = null, bool ConfirmWrites = false, string? StrKey = null, int UsedFound = -1);
    private sealed record CpLangSearchUsedFoundBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record CpVersionGetUpdatePackBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record CpCreateSitemapBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record CpLangSaveTranslationBody(string? Action = null, bool ConfirmWrites = false, string? StrKey = null, string? LangCode = null, string? Value = null);
    private sealed record CpLangSaveDescriptionBody(string? Action = null, bool ConfirmWrites = false, string? StrKey = null, string? Value = null);
    private sealed record CpLangCreateStringBody(
        string? Action = null,
        bool ConfirmWrites = false,
        string? Description = null,
        string? Same = null,
        int IsError = 0,
        int IsCustom = 0,
        int UsedFound = 0);
    private sealed record CpLangDeleteNotUsedBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record CpPacksDeleteBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record CpChannelsWriteBody(string? Action = null, bool ConfirmWrites = false, string? Code = null, int? Enabled = null);
    private sealed record CpLogisticsWriteBody(string? Action = null, bool ConfirmWrites = false, string? Code = null);
    private sealed record CpPaymentsWriteBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record CpWorkshopWriteBody(
        string? Action = null,
        bool ConfirmWrites = false,
        long JobId = 0,
        long BayId = 0,
        long TechId = 0,
        long Id = 0,
        string? Code = null,
        string? Name = null,
        string? Phone = null,
        string? Skill = null,
        string? Status = null,
        int Active = 1,
        int SortOrder = 0,
        string? JobNo = null,
        string? CustomerName = null,
        string? CustomerPhone = null,
        string? CustomerEmail = null,
        long CustomerId = 0,
        string? Plate = null,
        string? Vin = null,
        string? Make = null,
        string? Model = null,
        string? Year = null,
        int Odometer = 0,
        string? Complaint = null,
        bool EstimateApproved = false,
        bool UnderWarranty = false,
        string? Notes = null,
        long TimePromised = 0,
        string? LabourDesc = null,
        decimal LabourHours = 1,
        decimal LabourRate = 150,
        string? PartDesc = null,
        decimal PartQty = 1,
        decimal PartPrice = 0,
        string? LineType = null,
        string? Description = null,
        long ItemId = 0,
        decimal Qty = 1,
        decimal UnitPrice = 0,
        decimal TaxPercent = 5,
        int Chargeable = 1,
        string? RefNo = null,
        long GarageId = 0,
        string? ServiceType = null,
        long TimeSlot = 0,
        long AppointmentId = 0);
    private sealed record CpCurrenciesSetRateBody(string? IsoCode = null, decimal Rate = 0, bool ConfirmWrites = false);
    private sealed record CpCurrenciesSetAvailableBody(
        string? IsoCodes = null,
        string? CurrenciesList = null,
        int Available = -1,
        string? ShopCurrency = null,
        bool ConfirmWrites = false);
    private sealed record CpPricesEditWriteBody(
        string? Action = null,
        bool ConfirmWrites = false,
        long Id = 0,
        long PriceId = 0,
        string? Article = null,
        string? Manufacturer = null,
        string? Name = null,
        int Exist = 0,
        decimal Price = 0,
        int TimeToExe = 0,
        string? Storage = null,
        int MinOrder = 0,
        bool NoArticle = false,
        bool NoManufacturer = false,
        string? SearchText = null);
    private sealed record CpCatalogueSetMinLimitBody(
        string? Action = null,
        bool ConfirmWrites = false,
        long ProductId = 0,
        int Enabled = -1,
        decimal Value = -1);
    private sealed record CpSynonymsWriteBody(string? Action = null, bool ConfirmWrites = false, long Id = 0, string? Name = null);
    private sealed record CpCrossesWriteBody(
        string? Action = null,
        bool ConfirmWrites = false,
        long Id = 0,
        string? Article = null,
        string? ManufacturerArticle = null,
        string? Analog = null,
        string? ManufacturerAnalog = null,
        string? Manufacturer = null,
        bool EmptyOnly = false,
        int Null = 0,
        long IdFrom = 0,
        long IdBefore = 0);
    private sealed record CpTemplatesActionsBody(
        string? Action = null,
        bool ConfirmWrites = false,
        long TemplateId = 0,
        string? Caption = null,
        string? CategoryObject = null,
        string? ImageBase64 = null,
        string? ImageName = null,
        string? ImageType = null);
    private sealed record CpLineListsWriteBody(
        string? Action = null,
        long ListId = 0,
        string? Caption = null,
        string? CaptionLangStrId = null,
        int Type = 1,
        string? DataType = null,
        string? AutoSort = null,
        string? ItemsJson = null,
        string? TreeJson = null,
        string? Ids = null,
        string? LineLists = null,
        string? LangCode = null,
        bool ConfirmWrites = false);
    private sealed record CpTreeListsWriteBody(
        string? Action = null,
        long ListId = 0,
        long ParentId = 0,
        string? Caption = null,
        string? CaptionLangStrId = null,
        string? DataType = null,
        string? TreeJson = null,
        string? ItemsJson = null,
        string? Ids = null,
        string? TreeLists = null,
        string? LangCode = null,
        bool ConfirmWrites = false);
    private sealed record CpSkuMediaWriteBody(
        string? Action = null,
        long ProfileId = 0,
        long ProductId = 0,
        string? Brand = null,
        string? Article = null,
        string? Title = null,
        string? Subtitle = null,
        string? Status = null,
        long GroupId = 0,
        long RowId = 0,
        long PhotoId = 0,
        string? Name = null,
        string? Code = null,
        string? Icon = null,
        string? Label = null,
        string? Value = null,
        string? ValueType = null,
        string? Unit = null,
        string? Alt = null,
        string? Caption = null,
        string? PhotoType = null,
        int? SortOrder = null,
        int? IsPrimary = null,
        bool ConfirmWrites = false);
    private sealed record CpMainPageProductsWriteBody(
        string? TreeJson = null,
        string? LangCode = null,
        bool ConfirmWrites = false);
    private sealed record CpSpecialSearchesWriteBody(
        string? Action = null,
        long SearchId = 0,
        string? Caption = null,
        string? SearchCaption = null,
        string? CaptionLangStrId = null,
        string? SearchCaptionLangStrId = null,
        string? Title = null,
        string? SearchTitle = null,
        string? TitleLangStrId = null,
        string? SearchTitleLangStrId = null,
        string? Description = null,
        string? SearchDescription = null,
        string? DescriptionLangStrId = null,
        string? SearchDescriptionLangStrId = null,
        string? Keywords = null,
        string? SearchKeywords = null,
        string? KeywordsLangStrId = null,
        string? SearchKeywordsLangStrId = null,
        string? Robots = null,
        string? SearchRobots = null,
        string? Alias = null,
        string? SearchAlias = null,
        int Order = 0,
        int SearchOrder = 0,
        int Active = 0,
        int SearchActive = 0,
        string? TreeJson = null,
        string? DeletedSteps = null,
        string? DeletedStepsJson = null,
        string? SearchesIds = null,
        string? SearchesIdsJson = null,
        string? ImageName = null,
        string? Img = null,
        string? FileLocal = null,
        string? LangCode = null,
        bool ConfirmWrites = false);
    private sealed record CpCatalogueEditorWriteBody(
        string? TreeJson = null,
        string? LangCode = null,
        bool ConfirmWrites = false);
    private sealed record CpCatalogueProductsDeleteBody(
        string? Action = null,
        long CategoryId = 0,
        string? ProductsJson = null,
        string? ProductsList = null,
        bool ConfirmWrites = false);
    private sealed record CpCatalogueReviewsWriteBody(
        string? Action = null,
        long ReviewId = 0,
        long Id = 0,
        bool ConfirmWrites = false);
    private sealed record CpCatalogueProductWriteBody(
        string? Action = null,
        long ProductId = 0,
        long CategoryId = 0,
        string? Caption = null,
        string? CaptionLangStrId = null,
        string? Alias = null,
        string? TitleTag = null,
        string? TitleTagLangStrId = null,
        string? DescriptionTag = null,
        string? DescriptionTagLangStrId = null,
        string? KeywordsTag = null,
        string? KeywordsTagLangStrId = null,
        string? RobotsTag = null,
        int PublishedFlag = 1,
        string? ProductText = null,
        string? ProductTextLangStrId = null,
        string? PropertiesJson = null,
        string? PropertiesObjects = null,
        string? StickersJson = null,
        string? ProductStickers = null,
        string? RelatedJson = null,
        string? ProductRelated = null,
        string? ImagesJson = null,
        string? ImagesList = null,
        string? LangCode = null,
        bool ConfirmWrites = false);
    private sealed record CpPriceReviewWriteBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record CpPriceReviewCreateCsvBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record CpAccessoriesPhotosBody(
        string? Action = null,
        long ListingId = 0,
        long PhotoId = 0,
        string? FileName = null,
        string? ImageName = null,
        string? Photo = null,
        bool AsPrimary = false,
        bool ConfirmWrites = false);
    private sealed record CpAccessoriesTaxonomyWriteBody(
        string? Action = null,
        long Id = 0,
        long CategoryId = 0,
        long TermId = 0,
        long ParentId = 0,
        string? Label = null,
        string? TermType = null,
        int SortOrder = 0,
        bool Active = false,
        bool ConfirmWrites = false);
    private sealed record CpAccessoriesListingsWriteBody(
        string? Action = null,
        long ListingId = 0,
        long Id = 0,
        long CategoryId = 0,
        long SubcategoryId = 0,
        string? Title = null,
        string? Description = null,
        string? Make = null,
        string? Model = null,
        string? Year = null,
        string? City = null,
        string? ConditionType = null,
        decimal Price = 0,
        decimal ComparePrice = 0,
        string? Currency = null,
        string? ImageUrl = null,
        string? ExternalUrl = null,
        int PhotoCount = 1,
        bool Featured = false,
        int StockQty = 0,
        string? Status = null,
        bool ConfirmWrites = false);
    private sealed record CpVersionClearUpdatesBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record CpReturnActionBody(
        long ReturnId,
        string? Action,
        bool ConfirmWrites = false,
        int StatusId = 0,
        long LineId = 0,
        int Decide = -1);
    private sealed record CpSetUsersVinViewedBody(long RequestId, bool ConfirmWrites = false, int ViewedFlag = 1);
    private sealed record CpSetUserCommentBody(long UserId, string? Comment, bool ConfirmWrites = false);
    private sealed record CpSetUserUnlockedBody(long UserId, int Unlocked, bool ConfirmWrites = false);
    private sealed record CpUsersCreateBody(
        string? Email = null,
        int EmailConfirmed = 0,
        string? Phone = null,
        int PhoneConfirmed = 0,
        string? Password = null,
        int Unlocked = 1,
        int RegVariant = 1,
        string? FieldsJson = null,
        string? Fields = null,
        string? GroupsJson = null,
        string? Groups = null,
        bool ConfirmWrites = false);
    private sealed record CpUsersSetPasswordBody(long UserId = 0, string? Password = null, bool ConfirmWrites = false);
    private sealed record CpPricesImportCsvBody(long SessionId, bool ConfirmWrites = false);
    private sealed record CpPricesCompleteSessionBody(long SessionId = 0, long PriceId = 0, bool ConfirmWrites = false);
    private sealed record CpStoragesGroupsBody(string? Action = null, bool ConfirmWrites = false, long Id = 0, string? Name = null, string? Storages = null);
    private sealed record CpStoragesWriteBody(
        string? Action = null,
        string? SaveAction = null,
        long StorageId = 0,
        string? Name = null,
        string? ShortName = null,
        int Currency = 1,
        int InterfaceType = 1,
        string? UsersJson = null,
        string? Users = null,
        string? ConnectionOptionsJson = null,
        string? ConnectionOptions = null,
        string? HandlerFolder = null,
        int Hidden = 0,
        int BgLineColor = 0,
        bool ConfirmWrites = false);
    private sealed record CpStoragesMembershipBody(long OfficeId = 0, string? StoragesList = null, bool ConfirmWrites = false);
    private sealed record CpOfficesWriteBody(
        string? Action = null,
        string? SaveAction = null,
        long OfficeId = 0,
        string? Caption = null,
        string? Country = null,
        string? Region = null,
        string? City = null,
        string? Address = null,
        string? Phone = null,
        string? Email = null,
        string? Coordinates = null,
        string? Description = null,
        string? Timetable = null,
        string? UsersJson = null,
        string? Users = null,
        string? CaptionLangStrId = null,
        string? CountryLangStrId = null,
        string? RegionLangStrId = null,
        string? CityLangStrId = null,
        string? AddressLangStrId = null,
        string? DescriptionLangStrId = null,
        string? TimetableLangStrId = null,
        string? LangCode = null,
        bool ConfirmWrites = false);
    private sealed record CpOfficesDeleteBody(string? OfficeIds = null, string? Offices = null, bool ConfirmWrites = false);
    private sealed record CpOfficesGeoBody(long OfficeId = 0, string? GeoList = null, bool ConfirmWrites = false);
    private sealed record CpDeliveryMethodsWriteBody(
        string? Action = null,
        long ModeId = 0,
        int Available = 0,
        string? Caption = null,
        string? CaptionLangStrId = null,
        int SortOrder = 0,
        string? ParametersValues = null,
        string? LangCode = null,
        bool ConfirmWrites = false);
    private sealed record CpGeoRegionsWriteBody(
        string? TreeJson = null,
        string? TreeJsonText = null,
        string? LangCode = null,
        bool ConfirmWrites = false);
    private sealed record CpSearchTabsWriteBody(
        string? Action = null,
        long TabId = 0,
        int Enabled = 0,
        string? TabEnabled = null,
        string? Caption = null,
        string? TabCaption = null,
        string? CaptionLangStrId = null,
        string? TabCaptionLangStrId = null,
        int SortOrder = 0,
        string? ParametersValues = null,
        string? LangCode = null,
        bool ConfirmWrites = false);
    private sealed record CpAdditionalTextsWriteBody(
        string? Url = null,
        string? Content = null,
        string? Text = null,
        int BeforeMain = 0,
        string? TitleTag = null,
        string? DescriptionTag = null,
        string? KeywordsTag = null,
        string? ContentLangStrId = null,
        string? TextLangStrId = null,
        string? TitleLangStrId = null,
        string? DescriptionLangStrId = null,
        string? KeywordsLangStrId = null,
        string? LangCode = null,
        bool ConfirmWrites = false);
    private sealed record CpAdditionalTextsDeleteBody(string? Ids = null, string? UrlsToDel = null, bool ConfirmWrites = false);
    private sealed record CpOrderStatusesWriteBody(
        string? OrdersJson = null,
        string? OrdersStatuses = null,
        string? ItemsJson = null,
        string? OrdersItemsStatuses = null,
        string? LangCode = null,
        bool ConfirmWrites = false);
    private sealed record CpProductFiltersWriteBody(
        string? Action = null,
        long FilterId = 0,
        string? Manufacturer = null,
        string? Article = null,
        string? Name = null,
        int Flag = 0,
        string? FlagText = null,
        string? StoragesJson = null,
        string? ListStorages = null,
        string? MinPrice = null,
        string? MaxPrice = null,
        string? MinTime = null,
        string? MaxTime = null,
        bool ConfirmWrites = false);
    private sealed record CpSliderBannersWriteBody(
        string? Action = null,
        long ImageId = 0,
        string? Href = null,
        string? Link = null,
        int Connected = 0,
        string? ConnectedFlag = null,
        int CntImg = 0,
        int CntImgNext = 0,
        int TimeNext = 0,
        bool ConfirmWrites = false);
    private sealed record CpQuoteSaveNoteBody(long QuoteId = 0, string? AdminNote = null, bool ConfirmWrites = false);
    private sealed record CpQuoteSaveLinesBody(
        long QuoteId = 0,
        string? AdminNote = null,
        string? LinesJson = null,
        string? Lines = null,
        bool ConfirmWrites = false);
    private sealed record CpQuoteSendBody(long QuoteId = 0, bool ConfirmWrites = false);
    private sealed record CpVendorApprovalsBody(long Id = 0, string? Action = null, bool ConfirmWrites = false);
    private sealed record CpApiClientsToggleBody(long ClientId = 0, string? Action = null, bool ConfirmWrites = false);
    private sealed record CpPriceStorageRulesBody(
        string? Action = null,
        string? Kind = null,
        long RuleId = 0,
        long StorageId = 0,
        string? Manufacturer = null,
        string? Article = null,
        string? MarginPercent = null,
        int Visible = 0,
        bool ConfirmWrites = false);
    private sealed record CpContentPublishedBody(long ContentId = 0, int PublishedFlag = 0, bool ConfirmWrites = false);
    private sealed record CpContentMainBody(long ContentId = 0, int IsFrontend = 1, bool ConfirmWrites = false);
    private sealed record CpContentBodyWriteBody(
        long ContentId = 0,
        string? ContentType = null,
        string? Content = null,
        string? ContentLangStrId = null,
        string? LangCode = null,
        bool ConfirmWrites = false);
    private sealed record CpContentMetaWriteBody(
        long ContentId = 0,
        string? Alias = null,
        string? Value = null,
        long Parent = 0,
        string? Description = null,
        int IsFrontend = 1,
        string? ContentType = null,
        string? Content = null,
        string? TitleTag = null,
        string? DescriptionTag = null,
        string? KeywordsTag = null,
        string? AuthorTag = null,
        int MainFlag = 0,
        string? CssJs = null,
        string? RobotsTag = null,
        int PublishedFlag = 1,
        string? GroupsAccess = null,
        string? ValueLangStrId = null,
        string? DescriptionLangStrId = null,
        string? ContentLangStrId = null,
        string? TitleLangStrId = null,
        string? DescriptionTagLangStrId = null,
        string? KeywordsLangStrId = null,
        string? AuthorLangStrId = null,
        string? LangCode = null,
        string? CheckHash = null,
        bool ConfirmWrites = false);
    private sealed record CpContentTreeWriteBody(
        string? TreeJson = null,
        int IsFrontend = 1,
        string? LangCode = null,
        bool ConfirmWrites = false);
    private sealed record CpMenusWriteBody(
        string? Action = null,
        long MenuId = 0,
        string? Caption = null,
        string? CaptionLangStrId = null,
        string? MenuUlClass = null,
        string? MenuUlId = null,
        int IsFrontend = 1,
        string? TreeJson = null,
        string? MenuTree = null,
        string? Ids = null,
        string? MenuList = null,
        string? LangCode = null,
        bool ConfirmWrites = false);
    private sealed record CpModulesWriteBody(
        string? Action = null,
        long ModuleId = 0,
        long PrototypeId = 0,
        string? PrototypeNameLangStrId = null,
        string? Caption = null,
        string? CaptionLangStrId = null,
        string? ContentType = null,
        string? Content = null,
        string? ContentLangStrId = null,
        string? Position = null,
        int Activated = 1,
        string? DataJson = null,
        string? DataValue = null,
        int ShowCaption = 0,
        int SortOrder = 0,
        int ForAll = 0,
        int IsFrontend = 1,
        string? ContentIds = null,
        string? ContentArray = null,
        string? GroupsAllowed = null,
        string? Ids = null,
        string? ModulesList = null,
        string? LangCode = null,
        bool ConfirmWrites = false);
}
