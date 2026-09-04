using System.Globalization;
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
            CpOmsPayRefundBody? body,
            ILegacySessionValidator validator,
            ICpOmsPayRefundDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for OMS pay-refund dry-run.");
            }

            body ??= new CpOmsPayRefundBody(0, false, null, false);
            var result = await dryRun.EvaluateAsync(
                new CpOmsPayRefundRequest(body.OrderId, body.DirectRefund, body.PaidSum, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

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
            CpOmsRefreshItemCostBody? body,
            ILegacySessionValidator validator,
            ICpOmsRefreshItemCostDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {
                return Unauthorized("Admin CP capability required for OMS refresh-item-cost dry-run.");
            }

            body ??= new CpOmsRefreshItemCostBody(0, 0, false);
            var result = await dryRun.EvaluateAsync(
                new CpOmsRefreshItemCostRequest(body.OrderId, body.ItemId, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

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

        endpoints.MapPost(EcomAeRoutes.CpPosOpenSession, async (HttpContext context, CpPosOpenSessionBody? body, ILegacySessionValidator validator, ICpPosOpenSessionDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new CpPosOpenSessionBody(null,false); return Results.Ok(dryRun.Evaluate(new CpPosOpenSessionRequest(body.Action, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.CpPosCloseSession, async (HttpContext context, CpPosCloseSessionBody? body, ILegacySessionValidator validator, ICpPosCloseSessionDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new CpPosCloseSessionBody(null,false); return Results.Ok(dryRun.Evaluate(new CpPosCloseSessionRequest(body.Action, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.CpPosCompleteSale, async (HttpContext context, CpPosCompleteSaleBody? body, ILegacySessionValidator validator, ICpPosCompleteSaleDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new CpPosCompleteSaleBody(null,false); return Results.Ok(dryRun.Evaluate(new CpPosCompleteSaleRequest(body.Action, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.CpPosSaveSettings, async (HttpContext context, CpPosSaveSettingsBody? body, ILegacySessionValidator validator, ICpPosSaveSettingsDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new CpPosSaveSettingsBody(null,false); return Results.Ok(dryRun.Evaluate(new CpPosSaveSettingsRequest(body.Action, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
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
        endpoints.MapPost(EcomAeRoutes.CpLangCreateString, async (HttpContext context, CpLangCreateStringBody? body, ILegacySessionValidator validator, ICpLangCreateStringDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required.");
            body ??= new CpLangCreateStringBody(null, false);
            return Results.Ok(dryRun.Evaluate(new CpLangCreateStringRequest(body.Action, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });
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
                    _ => ErpSimpleWriteResult.Fail("invalid", "Unknown workshop action. assign / save_bay / save_tech / set_status are live; others stay PHP."),
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
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                templateId = LiveWriteFormBinder.Long(form, "templateId", "template_id", "id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new CpTemplatesActionsRequest(action, false)).ToPayload(SessionPayload(session)));
            }

            var key = (action ?? string.Empty).Trim();
            var written = key is "delete" or "del"
                ? await writes.DeleteCategoryTemplateAsync(templateId, cancellationToken)
                : ErpSimpleWriteResult.Fail("php", "Category template create stays PHP.");
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/product-catalogue-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = !written.Succeeded && written.Code == "php", validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
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
        endpoints.MapPost(EcomAeRoutes.CpAccessoriesPhotos, async (HttpContext context, CpAccessoriesPhotosBody? body, ILegacySessionValidator validator, ICpAccessoriesPhotosDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required.");
            body ??= new CpAccessoriesPhotosBody(null, false);
            return Results.Ok(dryRun.Evaluate(new CpAccessoriesPhotosRequest(body.Action, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });
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
                    message = "Set confirmWrites=true to suspend or reject a vendor on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.SetStatusAsync(id, action, cancellationToken);
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
                note = "shop_currencies digest. Single-rate POST /cp/currencies/set-rate when confirmWrites=true. Bulk available and live FX stay PHP."
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
                note = "shop_docpart_articles_analogs_list digest. Save/delete POST /cp/crosses/write when confirmWrites=true. Add, brand resolve, and search-delete stay PHP."
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
                note = "Read-only epc_carrier_accounts + epc_carrier_shipments KPIs + carriers (config_json omitted; catalog region/blurb). PHP /CP/shop/logistics/carriers remains authoritative."
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
                note = "Read-only epc_acc_* listings. Photo upload/delete remain /cp/accessories/photos + module-ajax dry-run; PHP accessories authoritative."
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
                note = "Read-only epc_social_accounts/drafts (encrypted_credentials omitted). Publish/save remain portal_social dry-run."
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
                note = "Read-only users peek for Super customer board (writes remain PHP). Super-only Blazor app."
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
                note = "Read-only epc_fulfillment_orders queue. Stage advance remains OMS fulfillment dry-run + PHP."
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
                note = "Read-only PHP epc_fulfillment_get digest. Stage set/advance remain OMS dry-run; writes remain PHP."
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
                note = "Read-only MySQL epc_events peek (no Kafka/Rabbit payloads). Super-only Blazor app."
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
    private sealed record CpPosOpenSessionBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record CpPosCloseSessionBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record CpPosCompleteSaleBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record CpPosSaveSettingsBody(string? Action = null, bool ConfirmWrites = false);
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
    private sealed record CpLangCreateStringBody(string? Action = null, bool ConfirmWrites = false);
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
        int SortOrder = 0);
    private sealed record CpCurrenciesSetRateBody(string? IsoCode = null, decimal Rate = 0, bool ConfirmWrites = false);
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
        int MinOrder = 0);
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
        string? ManufacturerAnalog = null);
    private sealed record CpTemplatesActionsBody(string? Action = null, bool ConfirmWrites = false, long TemplateId = 0);
    private sealed record CpPriceReviewWriteBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record CpPriceReviewCreateCsvBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record CpAccessoriesPhotosBody(string? Action = null, bool ConfirmWrites = false);
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
    private sealed record CpPricesImportCsvBody(long SessionId, bool ConfirmWrites = false);
    private sealed record CpPricesCompleteSessionBody(long SessionId = 0, long PriceId = 0, bool ConfirmWrites = false);
    private sealed record CpStoragesGroupsBody(string? Action = null, bool ConfirmWrites = false, long Id = 0, string? Name = null, string? Storages = null);
    private sealed record CpQuoteSaveNoteBody(long QuoteId = 0, string? AdminNote = null, bool ConfirmWrites = false);
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
}
