using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Bos;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
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
        "presentation-shell-scaffolded",
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

        endpoints.MapPost(EcomAeRoutes.BosNotificationsMarkRead, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosNotificationWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for notification mark_read.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosNotificationsMarkReadBody>(context, cancellationToken)
                       ?? new();
            var idsRaw = IdsRaw(body);
            var tenantKey = body.TenantKey;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                idsRaw = form["ids"].ToString();
                if (form.ContainsKey("tenant_key"))
                {
                    tenantKey = form["tenant_key"].ToString();
                }
                else if (form.ContainsKey("tenantKey"))
                {
                    tenantKey = form["tenantKey"].ToString();
                }
                else
                {
                    tenantKey = null;
                }

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
                    message = "Set confirmWrites=true to mark notifications read.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.MarkReadAsync(idsRaw, tenantKey, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, marked = written.Id, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosNotificationsDismiss, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosNotificationWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for notification dismiss.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosNotificationsDismissBody>(context, cancellationToken)
                       ?? new();
            var notificationId = body.NotificationId;
            var tenantKey = body.TenantKey;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                notificationId = LiveWriteFormBinder.Long(form, "notification_id", "notificationId", "id");
                if (form.ContainsKey("tenant_key"))
                {
                    tenantKey = form["tenant_key"].ToString();
                }
                else if (form.ContainsKey("tenantKey"))
                {
                    tenantKey = form["tenantKey"].ToString();
                }
                else
                {
                    tenantKey = null;
                }

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
                    message = "Set confirmWrites=true to dismiss a notification.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.DismissAsync(notificationId, tenantKey, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosNotificationsMarkAllRead, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosNotificationWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for notification mark_all_read.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosNotificationsMarkAllReadBody>(context, cancellationToken)
                       ?? new();
            var userId = body.UserId;
            var tenantKey = body.TenantKey;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                userId = LiveWriteFormBinder.Long(form, "user_id", "userId");
                if (form.ContainsKey("tenant_key"))
                {
                    tenantKey = form["tenant_key"].ToString();
                }
                else if (form.ContainsKey("tenantKey"))
                {
                    tenantKey = form["tenantKey"].ToString();
                }
                else
                {
                    tenantKey = null;
                }

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
                    message = "Set confirmWrites=true to mark all notifications read.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.MarkAllReadAsync(tenantKey, userId, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, marked = written.Id, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosNotificationsPrefsSave, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosNotificationWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for notification prefs_save.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosNotificationsPrefsSaveBody>(context, cancellationToken)
                       ?? new();
            var userId = body.UserId;
            var tenantKey = body.TenantKey;
            var category = body.Category;
            var channelInApp = body.ChannelInApp;
            var channelEmail = body.ChannelEmail;
            var channelWebhook = body.ChannelWebhook;
            var emailDigest = body.EmailDigest;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                userId = LiveWriteFormBinder.Long(form, "user_id", "userId");
                tenantKey = FormOrNull(form, "tenant_key", "tenantKey");
                category = FormOrNull(form, "category");
                channelInApp = FormOrNull(form, "channel_in_app", "channelInApp");
                channelEmail = FormOrNull(form, "channel_email", "channelEmail");
                channelWebhook = FormOrNull(form, "channel_webhook", "channelWebhook");
                emailDigest = FormOrNull(form, "email_digest", "emailDigest");
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
                    message = "Set confirmWrites=true to save notification preferences.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.SavePrefsAsync(
                tenantKey, userId, category, channelInApp, channelEmail, channelWebhook, emailDigest, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosNotificationsCleanup, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosNotificationWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for notification cleanup.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosNotificationsCleanupBody>(context, cancellationToken)
                       ?? new();
            var daysRaw = body.Days;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                daysRaw = FormOrNull(form, "days");
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
                    message = "Set confirmWrites=true to clean up old read notifications.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.CleanupAsync(daysRaw, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, deleted = written.Id, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosCreditHold, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosCreditWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for credit hold.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosCreditHoldBody>(context, cancellationToken)
                       ?? new();
            var siteKey = body.SiteKey;
            var customerId = body.CustomerId;
            var reason = body.Reason;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                siteKey = form.ContainsKey("site_key") ? form["site_key"].ToString() : form.ContainsKey("siteKey") ? form["siteKey"].ToString() : null;
                customerId = LiveWriteFormBinder.Long(form, "customer_id", "customerId");
                reason = form.ContainsKey("reason") ? form["reason"].ToString() : null;
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
                    message = "Set confirmWrites=true to hold a credit limit.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.HoldAsync(siteKey, customerId, reason, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosCreditRelease, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosCreditWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for credit release.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosCreditReleaseBody>(context, cancellationToken)
                       ?? new();
            var siteKey = body.SiteKey;
            var customerId = body.CustomerId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                siteKey = form.ContainsKey("site_key") ? form["site_key"].ToString() : form.ContainsKey("siteKey") ? form["siteKey"].ToString() : null;
                customerId = LiveWriteFormBinder.Long(form, "customer_id", "customerId");
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
                    message = "Set confirmWrites=true to release a credit hold.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.ReleaseAsync(siteKey, customerId, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosApiKeysRevoke, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosApiKeyWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for API key revoke.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosApiKeyRevokeBody>(context, cancellationToken)
                       ?? new();
            var keyId = body.KeyId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                keyId = LiveWriteFormBinder.Long(form, "key_id", "keyId");
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
                    message = "Set confirmWrites=true to revoke an API key.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.RevokeAsync(keyId, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosWebhooksDelete, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosWebhookWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for webhook delete.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosWebhookDeleteBody>(context, cancellationToken)
                       ?? new();
            var webhookId = body.WebhookId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                webhookId = LiveWriteFormBinder.Long(form, "webhook_id", "webhookId");
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
                    message = "Set confirmWrites=true to delete a webhook.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.DeleteAsync(webhookId, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosWebhooksDlqResolve, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosWebhookWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for webhook DLQ resolve.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosWebhookDlqResolveBody>(context, cancellationToken)
                       ?? new();
            var dlqId = body.DlqId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                dlqId = LiveWriteFormBinder.Long(form, "dlq_id", "dlqId");
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
                    message = "Set confirmWrites=true to resolve a webhook DLQ item.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.ResolveDlqAsync(dlqId, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosPoCancel, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosPoWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for PO cancel.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosPoCancelBody>(context, cancellationToken)
                       ?? new();
            var poId = body.PoId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                poId = LiveWriteFormBinder.Long(form, "po_id", "poId");
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
                    message = "Set confirmWrites=true to cancel a PO.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.CancelAsync(poId, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosPoReject, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosPoWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for PO reject.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosPoRejectBody>(context, cancellationToken)
                       ?? new();
            var poId = body.PoId;
            var tier = body.Tier;
            var approverId = body.ApproverId;
            var reason = body.Reason;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                poId = LiveWriteFormBinder.Long(form, "po_id", "poId");
                tier = LiveWriteFormBinder.Int(form, "tier");
                approverId = LiveWriteFormBinder.Long(form, "approver_id", "approverId");
                reason = form.ContainsKey("reason") ? form["reason"].ToString() : null;
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
                    message = "Set confirmWrites=true to reject a PO.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.RejectAsync(poId, tier, approverId, reason, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosPoApprove, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosPoWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for PO approve.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosPoApproveBody>(context, cancellationToken)
                       ?? new();
            var poId = body.PoId;
            var tier = body.Tier;
            var approverId = body.ApproverId;
            var comment = body.Comment;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                poId = LiveWriteFormBinder.Long(form, "po_id", "poId");
                tier = LiveWriteFormBinder.Int(form, "tier");
                approverId = LiveWriteFormBinder.Long(form, "approver_id", "approverId");
                comment = form.ContainsKey("comment") ? form["comment"].ToString() : null;
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
                    message = "Set confirmWrites=true to approve a PO.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.ApproveAsync(poId, tier, approverId, comment, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosFulfillmentPickItem, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosFulfillmentWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for fulfillment pick.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosFulfillmentPickBody>(context, cancellationToken)
                       ?? new();
            var itemId = body.ItemId;
            var qtyPicked = body.QtyPicked;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                itemId = LiveWriteFormBinder.Long(form, "item_id", "itemId");
                qtyPicked = LiveWriteFormBinder.Int(form, "qty_picked", "qtyPicked");
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
                    message = "Set confirmWrites=true to pick a fulfillment item.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.PickItemAsync(itemId, qtyPicked, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosFulfillmentTransition, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosFulfillmentWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for fulfillment transition.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosFulfillmentTransitionBody>(context, cancellationToken)
                       ?? new();
            var fulfillmentId = body.FulfillmentId;
            var newStatus = body.NewStatus;
            var assignedTo = body.AssignedTo;
            var assignedName = body.AssignedName;
            var carrier = body.Carrier;
            var trackingNumber = body.TrackingNumber;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                fulfillmentId = LiveWriteFormBinder.Long(form, "fulfillment_id", "fulfillmentId");
                newStatus = form.ContainsKey("new_status") ? form["new_status"].ToString() : (form.ContainsKey("newStatus") ? form["newStatus"].ToString() : null);
                assignedTo = LiveWriteFormBinder.Long(form, "assigned_to", "assignedTo");
                assignedName = form.ContainsKey("assigned_name") ? form["assigned_name"].ToString() : null;
                carrier = form.ContainsKey("carrier") ? form["carrier"].ToString() : null;
                trackingNumber = form.ContainsKey("tracking_number") ? form["tracking_number"].ToString() : null;
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
                if (form.ContainsKey("opts"))
                {
                    try
                    {
                        using var opts = JsonDocument.Parse(form["opts"].ToString());
                        if (opts.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            if (opts.RootElement.TryGetProperty("assigned_to", out var assignedEl)
                                && assignedEl.TryGetInt64(out var assignedFromOpts))
                            {
                                assignedTo = assignedFromOpts;
                            }

                            if (opts.RootElement.TryGetProperty("assigned_name", out var nameEl))
                            {
                                assignedName = nameEl.GetString();
                            }

                            if (opts.RootElement.TryGetProperty("carrier", out var carrierEl))
                            {
                                carrier = carrierEl.GetString();
                            }

                            if (opts.RootElement.TryGetProperty("tracking_number", out var trackingEl))
                            {
                                trackingNumber = trackingEl.GetString();
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // PHP json_decode failure becomes [] via `$opts ?: array()`.
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
                    message = "Set confirmWrites=true to transition a fulfillment order.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.TransitionAsync(fulfillmentId, newStatus, assignedTo, assignedName, carrier, trackingNumber, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosFulfillmentCreateWave, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosFulfillmentWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for fulfillment create-wave.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosFulfillmentCreateWaveBody>(context, cancellationToken)
                       ?? new();
            var siteKey = body.SiteKey;
            var fulfillmentIds = body.FulfillmentIds ?? [];
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                siteKey = form.ContainsKey("site_key") ? form["site_key"].ToString() : siteKey;
                fulfillmentIds = ParseFulfillmentIds(form.ContainsKey("fulfillment_ids") ? form["fulfillment_ids"].ToString() : null);
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
                    message = "Set confirmWrites=true to create a fulfillment wave.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.CreateWaveAsync(siteKey, fulfillmentIds, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosBillingCancel, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosBillingWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for billing cancel.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosBillingCancelBody>(context, cancellationToken)
                       ?? new();
            var subscriptionId = body.SubscriptionId;
            var reason = body.Reason;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                subscriptionId = LiveWriteFormBinder.Long(form, "subscription_id", "subscriptionId");
                reason = form.ContainsKey("reason") ? form["reason"].ToString() : null;
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
                    message = "Set confirmWrites=true to cancel a subscription.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.CancelAsync(subscriptionId, reason, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosBillingPay, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosBillingWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for billing pay.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosBillingPayBody>(context, cancellationToken)
                       ?? new();
            var invoiceId = body.InvoiceId;
            var method = body.Method;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                invoiceId = LiveWriteFormBinder.Long(form, "invoice_id", "invoiceId");
                method = form.ContainsKey("method") ? form["method"].ToString() : null;
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
                    message = "Set confirmWrites=true to record a billing payment.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.PayAsync(invoiceId, method, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosRmaTransition, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosRmaWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for RMA transition.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosRmaTransitionBody>(context, cancellationToken)
                       ?? new();
            var rmaId = body.RmaId;
            var status = body.Status;
            var notes = body.Notes;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                rmaId = LiveWriteFormBinder.Long(form, "rma_id", "rmaId");
                status = form.ContainsKey("status") ? form["status"].ToString() : null;
                notes = form.ContainsKey("notes") ? form["notes"].ToString() : null;
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
                    message = "Set confirmWrites=true to transition an RMA.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.TransitionAsync(rmaId, status, notes, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosDealersAutoTier, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosDealerWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for dealer auto-tier.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosDealersAutoTierBody>(context, cancellationToken)
                       ?? new();
            var dealerId = body.DealerId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                dealerId = LiveWriteFormBinder.Long(form, "dealer_id", "dealerId");
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
                    message = "Set confirmWrites=true to auto-tier a dealer.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.AutoTierAsync(dealerId, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosDunningUpdateStatus, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosDunningWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for dunning update-status.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosDunningUpdateStatusBody>(context, cancellationToken)
                       ?? new();
            var queueId = body.QueueId;
            var status = body.Status;
            var notes = body.Notes;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                queueId = LiveWriteFormBinder.Long(form, "queue_id", "queueId");
                status = form.ContainsKey("status") ? form["status"].ToString() : null;
                notes = form.ContainsKey("notes") ? form["notes"].ToString() : null;
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
                    message = "Set confirmWrites=true to update dunning status.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.UpdateStatusAsync(queueId, status, notes, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosDunningRecordPayment, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosDunningWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for dunning record-payment.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosDunningRecordPaymentBody>(context, cancellationToken)
                       ?? new();
            var queueId = body.QueueId;
            var amount = body.Amount;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                queueId = LiveWriteFormBinder.Long(form, "queue_id", "queueId");
                amount = LiveWriteFormBinder.Dec(form, "amount");
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
                    message = "Set confirmWrites=true to record a dunning payment.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.RecordPaymentAsync(queueId, amount, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosSsoProviderToggle, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosSsoWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for SSO provider-toggle.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosSsoProviderToggleBody>(context, cancellationToken)
                       ?? new();
            var providerId = body.ProviderId;
            var active = body.Active;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                providerId = LiveWriteFormBinder.Long(form, "provider_id", "providerId");
                active = form.ContainsKey("active")
                    ? BosSsoWriteService.PhpPostedBool(form["active"].ToString())
                    : false;
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
                    message = "Set confirmWrites=true to toggle an SSO provider.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.ToggleAsync(providerId, active, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosSsoProviderDelete, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosSsoWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for SSO provider-delete.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosSsoProviderDeleteBody>(context, cancellationToken)
                       ?? new();
            var providerId = body.ProviderId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                providerId = LiveWriteFormBinder.Long(form, "provider_id", "providerId");
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
                    message = "Set confirmWrites=true to delete an SSO provider.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.DeleteAsync(providerId, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosSandboxPromote, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosSandboxWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for sandbox promote.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosSandboxPromoteBody>(context, cancellationToken)
                       ?? new();
            var snapshotId = body.SnapshotId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                snapshotId = LiveWriteFormBinder.Long(form, "snapshot_id", "snapshotId");
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
                    message = "Set confirmWrites=true to promote a sandbox snapshot.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.PromoteAsync(snapshotId, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosSandboxDiscard, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosSandboxWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for sandbox discard.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosSandboxDiscardBody>(context, cancellationToken)
                       ?? new();
            var snapshotId = body.SnapshotId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                snapshotId = LiveWriteFormBinder.Long(form, "snapshot_id", "snapshotId");
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
                    message = "Set confirmWrites=true to discard a sandbox snapshot.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.DiscardAsync(snapshotId, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosSandboxApplyChange, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosSandboxWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for sandbox apply-change.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosSandboxApplyChangeBody>(context, cancellationToken)
                       ?? new();
            var snapshotId = body.SnapshotId;
            var key = body.Key;
            var oldValue = body.OldValue;
            var newValue = body.NewValue;
            var changeType = body.ChangeType;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                snapshotId = LiveWriteFormBinder.Long(form, "snapshot_id", "snapshotId");
                key = FormOrNull(form, "key");
                oldValue = FormOrNull(form, "old_value", "oldValue");
                newValue = FormOrNull(form, "new_value", "newValue");
                changeType = FormOrNull(form, "change_type", "changeType") ?? "modify";
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
                    message = "Set confirmWrites=true to apply a sandbox change.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.ApplyChangeAsync(snapshotId, key, oldValue, newValue, changeType, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosMarketplaceUninstall, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosMarketplaceWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for marketplace uninstall.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosMarketplaceUninstallBody>(context, cancellationToken)
                       ?? new();
            var appId = body.AppId;
            var siteKey = body.SiteKey;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                appId = LiveWriteFormBinder.Long(form, "app_id", "appId");
                siteKey = FormOrNull(form, "site_key", "siteKey");
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
                    message = "Set confirmWrites=true to uninstall a marketplace app.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.UninstallAsync(appId, siteKey, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosMarketplaceInstall, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosMarketplaceWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for marketplace install.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosMarketplaceInstallBody>(context, cancellationToken)
                       ?? new();
            var appId = body.AppId;
            var siteKey = body.SiteKey;
            var installedBy = body.InstalledBy;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                appId = LiveWriteFormBinder.Long(form, "app_id", "appId");
                siteKey = FormOrNull(form, "site_key", "siteKey");
                installedBy = LiveWriteFormBinder.Long(form, "installed_by", "installedBy");
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
                    message = "Set confirmWrites=true to install a marketplace app.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.InstallAsync(appId, siteKey, installedBy, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosMarketplaceReview, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosMarketplaceWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for marketplace review.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosMarketplaceReviewBody>(context, cancellationToken)
                       ?? new();
            var appId = body.AppId;
            var siteKey = body.SiteKey;
            var reviewData = body.ReviewData;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                appId = LiveWriteFormBinder.Long(form, "app_id", "appId");
                siteKey = FormOrNull(form, "site_key", "siteKey");
                reviewData = FormOrNull(form, "review_data", "reviewData");
                if (string.IsNullOrWhiteSpace(reviewData))
                {
                    var fields = new Dictionary<string, string?>
                    {
                        ["title"] = form["title"].ToString(),
                        ["review_text"] = form["review_text"].ToString(),
                        ["reviewer_name"] = form["reviewer_name"].ToString()
                    };
                    var rating = form["rating"].ToString();
                    if (!string.IsNullOrWhiteSpace(rating))
                    {
                        fields["rating"] = rating;
                    }

                    reviewData = JsonSerializer.Serialize(fields);
                }

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
                    message = "Set confirmWrites=true to add a marketplace review.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.AddReviewAsync(appId, siteKey, reviewData, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosPromosRecordUsage, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosPromoWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for promo record-usage.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosPromosRecordUsageBody>(context, cancellationToken)
                       ?? new();
            var promotionId = body.PromotionId;
            var siteKey = body.SiteKey;
            var customerId = body.CustomerId;
            var orderRef = body.OrderRef;
            var discount = body.Discount;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                promotionId = LiveWriteFormBinder.Long(form, "promotion_id", "promotionId");
                siteKey = FormOrNull(form, "site_key", "siteKey");
                customerId = LiveWriteFormBinder.Long(form, "customer_id", "customerId");
                orderRef = FormOrNull(form, "order_ref", "orderRef");
                discount = LiveWriteFormBinder.Dec(form, "discount");
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
                    message = "Set confirmWrites=true to record promotion usage.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.RecordUsageAsync(promotionId, siteKey, customerId, orderRef, discount, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosEntitiesEliminate, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosEntityWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for entity eliminate.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosEntitiesEliminateBody>(context, cancellationToken)
                       ?? new();
            var groupId = body.GroupId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                groupId = LiveWriteFormBinder.Long(form, "group_id", "groupId");
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
                    message = "Set confirmWrites=true to eliminate matched intercompany txns.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.EliminateAsync(groupId, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosPayrollApproveRun, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosPayrollWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for payroll approve-run.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosPayrollApproveRunBody>(context, cancellationToken)
                       ?? new();
            var runId = body.RunId;
            var approverId = body.ApproverId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                runId = LiveWriteFormBinder.Long(form, "run_id", "runId");
                approverId = LiveWriteFormBinder.Long(form, "approver_id", "approverId");
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
                    message = "Set confirmWrites=true to approve a payroll run.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.ApproveRunAsync(runId, approverId, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosSoc2AddEvidence, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosSoc2WriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for SOC2 add-evidence.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosSoc2AddEvidenceBody>(context, cancellationToken)
                       ?? new();
            var controlId = body.ControlId;
            var evidenceType = body.EvidenceType;
            var title = body.Title;
            var filePath = body.FilePath;
            var collectedBy = body.CollectedBy;
            var validFrom = body.ValidFrom;
            var validTo = body.ValidTo;
            var notes = body.Notes;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                controlId = FormOrNull(form, "control_id", "controlId");
                evidenceType = FormOrNull(form, "evidence_type", "evidenceType");
                title = FormOrNull(form, "title");
                filePath = FormOrNull(form, "file_path", "filePath");
                collectedBy = FormOrNull(form, "collected_by", "collectedBy");
                validFrom = FormOrNull(form, "valid_from", "validFrom");
                validTo = FormOrNull(form, "valid_to", "validTo");
                notes = FormOrNull(form, "notes");
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
                    message = "Set confirmWrites=true to add SOC 2 evidence.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.AddEvidenceAsync(
                controlId, evidenceType, title, filePath, collectedBy, validFrom, validTo, notes, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosSoc2UpdateControl, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosSoc2WriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for SOC2 update-control.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosSoc2UpdateControlBody>(context, cancellationToken)
                       ?? new();
            var controlId = body.ControlId;
            var controlData = body.ControlData;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                controlId = FormOrNull(form, "control_id", "controlId");
                controlData = FormOrNull(form, "control_data", "controlData");
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
                    message = "Set confirmWrites=true to update a SOC 2 control.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.UpdateControlAsync(
                controlId,
                BosSoc2WriteService.ParseControlData(controlData),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosSoc2CreatePolicy, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosSoc2WriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for SOC2 create-policy.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosSoc2CreatePolicyBody>(context, cancellationToken)
                       ?? new();
            var policyData = body.PolicyData;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                policyData = FormOrNull(form, "policy_data", "policyData");
                if (string.IsNullOrWhiteSpace(policyData))
                {
                    object relatedControls = Array.Empty<object>();
                    var relatedRaw = form["related_controls"].ToString();
                    if (!string.IsNullOrWhiteSpace(relatedRaw))
                    {
                        try
                        {
                            using var relatedDoc = JsonDocument.Parse(relatedRaw);
                            relatedControls = relatedDoc.RootElement.ValueKind == JsonValueKind.Array
                                ? JsonSerializer.Deserialize<object>(relatedRaw) ?? Array.Empty<object>()
                                : relatedRaw;
                        }
                        catch (JsonException)
                        {
                            relatedControls = relatedRaw;
                        }
                    }

                    policyData = JsonSerializer.Serialize(new Dictionary<string, object?>
                    {
                        ["policy_code"] = form["policy_code"].ToString(),
                        ["title"] = form["title"].ToString(),
                        ["content"] = form["content"].ToString(),
                        ["owner"] = form["owner"].ToString(),
                        ["related_controls"] = relatedControls
                    });
                }

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
                    message = "Set confirmWrites=true to create a SOC 2 policy.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.CreatePolicyAsync(policyData, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosWorkflowsToggle, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosWorkflowWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for workflow toggle.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosWorkflowsToggleBody>(context, cancellationToken)
                       ?? new();
            var workflowId = body.WorkflowId;
            var active = body.Active;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                workflowId = LiveWriteFormBinder.Long(form, "workflow_id", "workflowId");
                active = form.ContainsKey("active")
                    ? BosWorkflowWriteService.PhpPostedBool(form["active"].ToString())
                    : false;
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
                    message = "Set confirmWrites=true to toggle a workflow.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.ToggleAsync(workflowId, active, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosWorkflowsDelete, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosWorkflowWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for workflow delete.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosWorkflowsDeleteBody>(context, cancellationToken)
                       ?? new();
            var workflowId = body.WorkflowId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                workflowId = LiveWriteFormBinder.Long(form, "workflow_id", "workflowId");
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
                    message = "Set confirmWrites=true to delete a workflow.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.DeleteAsync(workflowId, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.BosVaultNewVersion, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IBosVaultWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/bos/login?returnUrl=/bos/fleet-summary-app", "Admin BOS capability required for vault new-version.");
            }

            if (!SuperCpHostGate.IsAllowed(context))
            {
                return Results.NotFound();
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<BosVaultNewVersionBody>(context, cancellationToken)
                       ?? new();
            var documentId = body.DocumentId;
            var versionData = body.VersionData;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                documentId = LiveWriteFormBinder.Long(form, "document_id", "documentId");
                versionData = FormOrNull(form, "version_data", "versionData");
                if (string.IsNullOrWhiteSpace(versionData))
                {
                    versionData = JsonSerializer.Serialize(new Dictionary<string, string?>
                    {
                        ["file_path"] = form["file_path"].ToString(),
                        ["file_size"] = form["file_size"].ToString(),
                        ["checksum"] = form["checksum"].ToString(),
                        ["change_note"] = form["change_note"].ToString(),
                        ["uploaded_by"] = form["uploaded_by"].ToString()
                    });
                }

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
                    message = "Set confirmWrites=true to add a vault document version.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.NewVersionAsync(documentId, versionData, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/bos/fleet-summary-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, cutoverAllowed = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapGet(EcomAeRoutes.BosAjaxWriteCatalog, (IBosAjaxWriteCatalog catalog) => Results.Ok(catalog.BuildReport()));

        endpoints.MapPost(EcomAeRoutes.BosAjaxWriteRegistryDryRun, async (
            string action,
            BosAjaxWriteRegistryBody? body,
            HttpContext context,
            ILegacySessionValidator validator,
            IBosAjaxWriteRegistryDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin)
                return Unauthorized("Admin session required for BOS ajax registry dry-run.");
            body ??= new BosAjaxWriteRegistryBody(false);
            return Results.Ok(dryRun.Evaluate(new BosAjaxWriteRegistryRequest(action, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });
        endpoints.MapPost(EcomAeRoutes.BosAjaxMfaPolicy, async (HttpContext context, BosAjaxMfaPolicyBody? body, ILegacySessionValidator validator, IBosAjaxMfaPolicyDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxMfaPolicyRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxDesignTokens, async (HttpContext context, BosAjaxDesignTokensBody? body, ILegacySessionValidator validator, IBosAjaxDesignTokensDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxDesignTokensRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxCreditLimit, async (HttpContext context, BosAjaxCreditLimitBody? body, ILegacySessionValidator validator, IBosAjaxCreditLimitDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxCreditLimitRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxRunAudit, async (HttpContext context, BosAjaxRunAuditBody? body, ILegacySessionValidator validator, IBosAjaxRunAuditDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new BosAjaxRunAuditRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxSave, async (HttpContext context, BosAjaxSaveBody? body, ILegacySessionValidator validator, IBosAjaxSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxUpdate, async (HttpContext context, BosAjaxUpdateBody? body, ILegacySessionValidator validator, IBosAjaxUpdateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxUpdateRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxDelete, async (HttpContext context, BosAjaxDeleteBody? body, ILegacySessionValidator validator, IBosAjaxDeleteDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new BosAjaxDeleteRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxGetTokens, async (HttpContext context, BosAjaxGetTokensBody? body, ILegacySessionValidator validator, IBosAjaxGetTokensDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxGetTokensRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxSaveToken, async (HttpContext context, BosAjaxSaveTokenBody? body, ILegacySessionValidator validator, IBosAjaxSaveTokenDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxSaveTokenRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxPrefsGet, async (HttpContext context, BosAjaxPrefsGetBody? body, ILegacySessionValidator validator, IBosAjaxPrefsGetDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxPrefsGetRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxPrefsSave, async (HttpContext context, BosAjaxPrefsSaveBody? body, ILegacySessionValidator validator, IBosAjaxPrefsSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxPrefsSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxStatus, async (HttpContext context, BosAjaxStatusBody? body, ILegacySessionValidator validator, IBosAjaxStatusDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxStatusRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxRunAll, async (HttpContext context, BosAjaxRunAllBody? body, ILegacySessionValidator validator, IBosAjaxRunAllDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new BosAjaxRunAllRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxSetLimit, async (HttpContext context, BosAjaxSetLimitBody? body, ILegacySessionValidator validator, IBosAjaxSetLimitDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxSetLimitRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxOrderStatus, async (HttpContext context, BosAjaxOrderStatusBody? body, ILegacySessionValidator validator, IBosAjaxOrderStatusDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxOrderStatusRequest(body.Id, body.TargetStatus, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxCreate, async (HttpContext context, BosAjaxCreateBody? body, ILegacySessionValidator validator, IBosAjaxCreateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxCreateRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxApprove, async (HttpContext context, BosAjaxApproveBody? body, ILegacySessionValidator validator, IBosAjaxApproveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new BosAjaxApproveRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxKeyGenerate, async (HttpContext context, BosAjaxKeyGenerateBody? body, ILegacySessionValidator validator, IBosAjaxKeyGenerateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxKeyGenerateRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxKeyRevoke, async (HttpContext context, BosAjaxKeyRevokeBody? body, ILegacySessionValidator validator, IBosAjaxKeyRevokeDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new BosAjaxKeyRevokeRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxCreateWave, async (HttpContext context, BosAjaxCreateWaveBody? body, ILegacySessionValidator validator, IBosAjaxCreateWaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxCreateWaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxSeedHs, async (HttpContext context, BosAjaxSeedHsBody? body, ILegacySessionValidator validator, IBosAjaxSeedHsDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxSeedHsRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxGroups, async (HttpContext context, BosAjaxGroupsBody? body, ILegacySessionValidator validator, IBosAjaxGroupsDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxGroupsRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxGroup, async (HttpContext context, BosAjaxGroupBody? body, ILegacySessionValidator validator, IBosAjaxGroupDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxGroupRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxSetRate, async (HttpContext context, BosAjaxSetRateBody? body, ILegacySessionValidator validator, IBosAjaxSetRateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxSetRateRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxSeedRates, async (HttpContext context, BosAjaxSeedRatesBody? body, ILegacySessionValidator validator, IBosAjaxSeedRatesDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxSeedRatesRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxProviderGet, async (HttpContext context, BosAjaxProviderGetBody? body, ILegacySessionValidator validator, IBosAjaxProviderGetDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxProviderGetRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxProviderCreate, async (HttpContext context, BosAjaxProviderCreateBody? body, ILegacySessionValidator validator, IBosAjaxProviderCreateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxProviderCreateRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxProviderToggle, async (HttpContext context, BosAjaxProviderToggleBody? body, ILegacySessionValidator validator, IBosAjaxProviderToggleDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxProviderToggleRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxProviderDelete, async (HttpContext context, BosAjaxProviderDeleteBody? body, ILegacySessionValidator validator, IBosAjaxProviderDeleteDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new BosAjaxProviderDeleteRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxCreateRun, async (HttpContext context, BosAjaxCreateRunBody? body, ILegacySessionValidator validator, IBosAjaxCreateRunDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxCreateRunRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxApproveRun, async (HttpContext context, BosAjaxApproveRunBody? body, ILegacySessionValidator validator, IBosAjaxApproveRunDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxApproveRunRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxRunDetails, async (HttpContext context, BosAjaxRunDetailsBody? body, ILegacySessionValidator validator, IBosAjaxRunDetailsDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxRunDetailsRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxProfileCreate, async (HttpContext context, BosAjaxProfileCreateBody? body, ILegacySessionValidator validator, IBosAjaxProfileCreateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxProfileCreateRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxAddInvoice, async (HttpContext context, BosAjaxAddInvoiceBody? body, ILegacySessionValidator validator, IBosAjaxAddInvoiceDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxAddInvoiceRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxUpdateStatus, async (HttpContext context, BosAjaxUpdateStatusBody? body, ILegacySessionValidator validator, IBosAjaxUpdateStatusDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxUpdateStatusRequest(body.Id, body.TargetStatus, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxRmaCreate, async (HttpContext context, BosAjaxRmaCreateBody? body, ILegacySessionValidator validator, IBosAjaxRmaCreateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxRmaCreateRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxRmaTransition, async (HttpContext context, BosAjaxRmaTransitionBody? body, ILegacySessionValidator validator, IBosAjaxRmaTransitionDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxRmaTransitionRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxRmaList, async (HttpContext context, BosAjaxRmaListBody? body, ILegacySessionValidator validator, IBosAjaxRmaListDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxRmaListRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxRmaDetail, async (HttpContext context, BosAjaxRmaDetailBody? body, ILegacySessionValidator validator, IBosAjaxRmaDetailDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxRmaDetailRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxSeed, async (HttpContext context, BosAjaxSeedBody? body, ILegacySessionValidator validator, IBosAjaxSeedDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new BosAjaxSeedRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxCreateGroup, async (HttpContext context, BosAjaxCreateGroupBody? body, ILegacySessionValidator validator, IBosAjaxCreateGroupDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxCreateGroupRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxMembers, async (HttpContext context, BosAjaxMembersBody? body, ILegacySessionValidator validator, IBosAjaxMembersDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxMembersRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxAddMember, async (HttpContext context, BosAjaxAddMemberBody? body, ILegacySessionValidator validator, IBosAjaxAddMemberDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxAddMemberRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxFolders, async (HttpContext context, BosAjaxFoldersBody? body, ILegacySessionValidator validator, IBosAjaxFoldersDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxFoldersRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxCreateFolder, async (HttpContext context, BosAjaxCreateFolderBody? body, ILegacySessionValidator validator, IBosAjaxCreateFolderDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxCreateFolderRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxPlans, async (HttpContext context, BosAjaxPlansBody? body, ILegacySessionValidator validator, IBosAjaxPlansDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxPlansRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxCreatePlan, async (HttpContext context, BosAjaxCreatePlanBody? body, ILegacySessionValidator validator, IBosAjaxCreatePlanDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxCreatePlanRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxInvoices, async (HttpContext context, BosAjaxInvoicesBody? body, ILegacySessionValidator validator, IBosAjaxInvoicesDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxInvoicesRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxControls, async (HttpContext context, BosAjaxControlsBody? body, ILegacySessionValidator validator, IBosAjaxControlsDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxControlsRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxUpdateControl, async (HttpContext context, BosAjaxUpdateControlBody? body, ILegacySessionValidator validator, IBosAjaxUpdateControlDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxUpdateControlRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxAddEvidence, async (HttpContext context, BosAjaxAddEvidenceBody? body, ILegacySessionValidator validator, IBosAjaxAddEvidenceDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxAddEvidenceRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxEvidence, async (HttpContext context, BosAjaxEvidenceBody? body, ILegacySessionValidator validator, IBosAjaxEvidenceDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxEvidenceRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.BosAjaxCreatePolicy, async (HttpContext context, BosAjaxCreatePolicyBody? body, ILegacySessionValidator validator, IBosAjaxCreatePolicyDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin) return Unauthorized("Admin session required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new BosAjaxCreatePolicyRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });

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

        endpoints.MapGet(EcomAeRoutes.BosFleetReadiness, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return Unauthorized("Admin BOS capability required for fleet readiness.");
            }

            var result = await dashboards.BuildBosFleetReadinessAsync(cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "bos",
                readiness = result,
                session = SessionPayload(session),
                note = "Platform-DB-only readiness scoring (no per-tenant connects). PHP epc_bos_health_check remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.BosAuditLog, async (
            HttpContext context,
            int? limit,
            string? area,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("bos"))
            {
                return Unauthorized("Admin BOS capability required for audit log digest.");
            }

            var result = await dashboards.ListBosAuditLogAsync(area, limit ?? 100, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "bos",
                entries = result.Entries,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_boc_audit digest (meta omitted). PHP epc_boc_audit_recent remains authoritative."
            });
        });

        // /bos (+ aliases) owned by Blazor BosFleetApp — do not MapGet shell aliases
        // (AmbiguousMatch + admin login wall vs ASP.NET-primary guest browse).
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
    private static string? IdsRaw(BosNotificationsMarkReadBody body)
    {
        if (body.Ids.ValueKind == JsonValueKind.Array || body.Ids.ValueKind == JsonValueKind.String)
        {
            return body.Ids.ValueKind == JsonValueKind.String
                ? body.Ids.GetString()
                : body.Ids.GetRawText();
        }

        return body.IdsText;
    }

    private sealed record BosNotificationsMarkReadBody(
        bool ConfirmWrites = false,
        JsonElement Ids = default,
        string? IdsText = null,
        string? TenantKey = null);

    private sealed record BosNotificationsDismissBody(
        bool ConfirmWrites = false,
        long NotificationId = 0,
        string? TenantKey = null);

    private sealed record BosNotificationsMarkAllReadBody(
        bool ConfirmWrites = false,
        long UserId = 0,
        string? TenantKey = null);

    private sealed record BosNotificationsCleanupBody(
        bool ConfirmWrites = false,
        string? Days = null);

    private sealed record BosCreditHoldBody(
        bool ConfirmWrites = false,
        string? SiteKey = null,
        long CustomerId = 0,
        string? Reason = null);

    private sealed record BosCreditReleaseBody(
        bool ConfirmWrites = false,
        string? SiteKey = null,
        long CustomerId = 0);

    private sealed record BosApiKeyRevokeBody(
        bool ConfirmWrites = false,
        long KeyId = 0);

    private sealed record BosWebhookDeleteBody(
        bool ConfirmWrites = false,
        long WebhookId = 0);

    private sealed record BosWebhookDlqResolveBody(
        bool ConfirmWrites = false,
        long DlqId = 0);

    private sealed record BosPoCancelBody(
        bool ConfirmWrites = false,
        long PoId = 0);

    private sealed record BosPoRejectBody(
        bool ConfirmWrites = false,
        long PoId = 0,
        int Tier = 0,
        long ApproverId = 0,
        string? Reason = null);

    private sealed record BosPoApproveBody(
        bool ConfirmWrites = false,
        long PoId = 0,
        int Tier = 0,
        long ApproverId = 0,
        string? Comment = null);

    private sealed record BosFulfillmentPickBody(
        bool ConfirmWrites = false,
        long ItemId = 0,
        int QtyPicked = 0);

    private sealed record BosFulfillmentTransitionBody(
        bool ConfirmWrites = false,
        long FulfillmentId = 0,
        string? NewStatus = null,
        long AssignedTo = 0,
        string? AssignedName = null,
        string? Carrier = null,
        string? TrackingNumber = null);

    private sealed record BosFulfillmentCreateWaveBody(
        bool ConfirmWrites = false,
        string? SiteKey = null,
        long[]? FulfillmentIds = null);

    private static long[] ParseFulfillmentIds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var text = raw.Trim();
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return doc.RootElement.EnumerateArray()
                    .Select(item => item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var id) ? id : 0)
                    .ToArray();
            }
        }
        catch (JsonException)
        {
            // PHP json_decode failure becomes [] via `$ids ?: array()`.
        }

        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0)
            .ToArray();
    }

    private sealed record BosBillingCancelBody(
        bool ConfirmWrites = false,
        long SubscriptionId = 0,
        string? Reason = null);

    private sealed record BosBillingPayBody(
        bool ConfirmWrites = false,
        long InvoiceId = 0,
        string? Method = null);

    private sealed record BosRmaTransitionBody(
        bool ConfirmWrites = false,
        long RmaId = 0,
        string? Status = null,
        string? Notes = null);

    private sealed record BosDealersAutoTierBody(
        bool ConfirmWrites = false,
        long DealerId = 0);

    private sealed record BosDunningUpdateStatusBody(
        bool ConfirmWrites = false,
        long QueueId = 0,
        string? Status = null,
        string? Notes = null);

    private sealed record BosDunningRecordPaymentBody(
        bool ConfirmWrites = false,
        long QueueId = 0,
        decimal Amount = 0);

    private sealed record BosSsoProviderToggleBody(
        bool ConfirmWrites = false,
        long ProviderId = 0,
        bool Active = false);

    private sealed record BosSsoProviderDeleteBody(
        bool ConfirmWrites = false,
        long ProviderId = 0);

    private sealed record BosSandboxPromoteBody(
        bool ConfirmWrites = false,
        long SnapshotId = 0);

    private sealed record BosSandboxDiscardBody(
        bool ConfirmWrites = false,
        long SnapshotId = 0);

    private sealed record BosSandboxApplyChangeBody(
        bool ConfirmWrites = false,
        long SnapshotId = 0,
        string? Key = null,
        string? OldValue = null,
        string? NewValue = null,
        string? ChangeType = "modify");

    private sealed record BosMarketplaceUninstallBody(
        bool ConfirmWrites = false,
        long AppId = 0,
        string? SiteKey = null);

    private sealed record BosMarketplaceInstallBody(
        bool ConfirmWrites = false,
        long AppId = 0,
        string? SiteKey = null,
        long InstalledBy = 0);

    private sealed record BosMarketplaceReviewBody(
        bool ConfirmWrites = false,
        long AppId = 0,
        string? SiteKey = null,
        string? ReviewData = null);

    private sealed record BosPromosRecordUsageBody(
        bool ConfirmWrites = false,
        long PromotionId = 0,
        string? SiteKey = null,
        long CustomerId = 0,
        string? OrderRef = null,
        decimal Discount = 0);

    private sealed record BosEntitiesEliminateBody(
        bool ConfirmWrites = false,
        long GroupId = 0);

    private sealed record BosPayrollApproveRunBody(
        bool ConfirmWrites = false,
        long RunId = 0,
        long ApproverId = 0);

    private sealed record BosSoc2AddEvidenceBody(
        bool ConfirmWrites = false,
        string? ControlId = null,
        string? EvidenceType = null,
        string? Title = null,
        string? FilePath = null,
        string? CollectedBy = null,
        string? ValidFrom = null,
        string? ValidTo = null,
        string? Notes = null);

    private sealed record BosSoc2UpdateControlBody(
        bool ConfirmWrites = false,
        string? ControlId = null,
        string? ControlData = null);

    private sealed record BosSoc2CreatePolicyBody(
        bool ConfirmWrites = false,
        string? PolicyData = null);

    private sealed record BosWorkflowsToggleBody(
        bool ConfirmWrites = false,
        long WorkflowId = 0,
        bool Active = false);

    private sealed record BosWorkflowsDeleteBody(
        bool ConfirmWrites = false,
        long WorkflowId = 0);

    private sealed record BosVaultNewVersionBody(
        bool ConfirmWrites = false,
        long DocumentId = 0,
        string? VersionData = null);

    private sealed record BosNotificationsPrefsSaveBody(
        bool ConfirmWrites = false,
        long UserId = 0,
        string? TenantKey = null,
        string? Category = null,
        string? ChannelInApp = null,
        string? ChannelEmail = null,
        string? ChannelWebhook = null,
        string? EmailDigest = null);

    private static string? FormOrNull(IFormCollection form, params string[] names)
    {
        foreach (var name in names)
        {
            if (form.ContainsKey(name))
            {
                return form[name].ToString();
            }
        }

        return null;
    }

    private sealed record BosAjaxWriteRegistryBody(bool ConfirmWrites = false);
    private sealed record BosAjaxMfaPolicyBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxDesignTokensBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxCreditLimitBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxRunAuditBody(long Id = 0, bool ConfirmWrites = false);
    private sealed record BosAjaxSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxUpdateBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxDeleteBody(long Id = 0, bool ConfirmWrites = false);
    private sealed record BosAjaxGetTokensBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxSaveTokenBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxPrefsGetBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxPrefsSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxStatusBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxRunAllBody(long Id = 0, bool ConfirmWrites = false);
    private sealed record BosAjaxSetLimitBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxOrderStatusBody(long Id, string? TargetStatus = null, bool ConfirmWrites = false);
    private sealed record BosAjaxCreateBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxApproveBody(long Id = 0, bool ConfirmWrites = false);
    private sealed record BosAjaxKeyGenerateBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxKeyRevokeBody(long Id = 0, bool ConfirmWrites = false);
    private sealed record BosAjaxCreateWaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxSeedHsBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxGroupsBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxGroupBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxSetRateBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxSeedRatesBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxProviderGetBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxProviderCreateBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxProviderToggleBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxProviderDeleteBody(long Id = 0, bool ConfirmWrites = false);
    private sealed record BosAjaxCreateRunBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxApproveRunBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxRunDetailsBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxProfileCreateBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxAddInvoiceBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxUpdateStatusBody(long Id, string? TargetStatus = null, bool ConfirmWrites = false);
    private sealed record BosAjaxRmaCreateBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxRmaTransitionBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxRmaListBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxRmaDetailBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxSeedBody(long Id = 0, bool ConfirmWrites = false);
    private sealed record BosAjaxCreateGroupBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxMembersBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxAddMemberBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxFoldersBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxCreateFolderBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxPlansBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxCreatePlanBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxInvoicesBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxControlsBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxUpdateControlBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxAddEvidenceBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxEvidenceBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record BosAjaxCreatePolicyBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
}
