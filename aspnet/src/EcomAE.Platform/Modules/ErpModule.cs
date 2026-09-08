using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;
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

        endpoints.MapGet(EcomAeRoutes.ErpAjaxWriteCatalog, (IErpAjaxWriteCatalog catalog) => Results.Ok(catalog.BuildReport()));

        endpoints.MapPost(EcomAeRoutes.ErpAjaxWriteRegistryDryRun, async (
            string action,
            ErpAjaxWriteRegistryBody? body,
            HttpContext context,
            ILegacySessionValidator validator,
            IErpAjaxWriteRegistryDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for ajax-write registry dry-run.");
            body ??= new ErpAjaxWriteRegistryBody(false);
            return Results.Ok(dryRun.Evaluate(new ErpAjaxWriteRegistryRequest(action, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.ErpAjaxConcurrencyStatus, async (HttpContext context, ErpConcurrencyStatusBody? body, ILegacySessionValidator validator, IErpConcurrencyStatusDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpConcurrencyStatusRequest(body.Id, body.TargetStatus, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxSettlementOpenDocs, async (HttpContext context, ErpSettlementOpenDocsBody? body, ILegacySessionValidator validator, IErpSettlementOpenDocsDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpSettlementOpenDocsRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxDashboard, async (HttpContext context, ErpDashboardBody? body, ILegacySessionValidator validator, IErpDashboardDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpDashboardRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCommandCenter, async (HttpContext context, ErpCommandCenterBody? body, ILegacySessionValidator validator, IErpCommandCenterDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpCommandCenterRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCcKpiTiles, async (HttpContext context, ErpCcKpiTilesBody? body, ILegacySessionValidator validator, IErpCcKpiTilesDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpCcKpiTilesRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCcApprovalQueue, async (HttpContext context, ErpCcApprovalQueueBody? body, ILegacySessionValidator validator, IErpCcApprovalQueueDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpCcApprovalQueueRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPeriodList, async (HttpContext context, ErpPeriodListBody? body, ILegacySessionValidator validator, IErpPeriodListDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpPeriodListRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPeriodChecklist, async (HttpContext context, ErpPeriodChecklistBody? body, ILegacySessionValidator validator, IErpPeriodChecklistDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpPeriodChecklistRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPeriodSummary, async (HttpContext context, ErpPeriodSummaryBody? body, ILegacySessionValidator validator, IErpPeriodSummaryDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpPeriodSummaryRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxFxRevaluationPreview, async (HttpContext context, ErpFxRevaluationPreviewBody? body, ILegacySessionValidator validator, IErpFxRevaluationPreviewDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpFxRevaluationPreviewRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBosComplianceFetch, async (HttpContext context, ErpBosComplianceFetchBody? body, ILegacySessionValidator validator, IErpBosComplianceFetchDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpBosComplianceFetchRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxRtlAssortmentSet, async (HttpContext context, ErpRtlAssortmentSetBody? body, ILegacySessionValidator validator, IErpRtlAssortmentSetDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpRtlAssortmentSetRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxRtlDiscountSave, async (HttpContext context, ErpRtlDiscountSaveBody? body, ILegacySessionValidator validator, IErpRtlDiscountSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpRtlDiscountSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxRtlPosSale, async (HttpContext context, ErpRtlPosSaleBody? body, ILegacySessionValidator validator, IErpRtlPosSaleDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpRtlPosSaleRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInsClaimStatus, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpInsClaimStatusDryRun dryRun,
            IErpInsClaimStatusWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/insurance-compliance-app", "Admin ERP capability required for insurance claim status.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpInsClaimStatusBody>(context, cancellationToken) ?? new(0, null, false);
            var id = body.Id;
            var status = body.TargetStatus;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "claimId", "claim_id");
                status = LiveWriteFormBinder.Text(form, "status", "targetStatus", "target_status");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpInsClaimStatusRequest(id, status, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.SetStatusAsync(id, status, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/insurance-compliance-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPrjSave, async (HttpContext context, ErpPrjSaveBody? body, ILegacySessionValidator validator, IErpPrjSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPrjSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPrjTaskSave, async (HttpContext context, ErpPrjTaskSaveBody? body, ILegacySessionValidator validator, IErpPrjTaskSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPrjTaskSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPrjLogTime, async (HttpContext context, ErpPrjLogTimeBody? body, ILegacySessionValidator validator, IErpPrjLogTimeDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPrjLogTimeRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxConsEntitySave, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpConsEntitySaveDryRun dryRun,
            EcomAE.Platform.Erp.IErpConsEntitySaveWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/consolidations-app", "Admin ERP capability required for consolidation entity save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpConsEntitySaveBody>(context, cancellationToken)
                       ?? new();
            var id = body.Id;
            var code = body.Code;
            var name = body.Name;
            var currencyCode = body.CurrencyCode;
            var ownershipPct = body.OwnershipPct;
            var isHome = body.IsHome;
            var parentCode = body.ParentCode;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id");
                code = LiveWriteFormBinder.Text(form, "code");
                name = LiveWriteFormBinder.Text(form, "name");
                currencyCode = LiveWriteFormBinder.Text(form, "currencyCode", "currency_code");
                var ownershipRaw = LiveWriteFormBinder.DecOrNull(form, "ownershipPct", "ownership_pct");
                ownershipPct = ownershipRaw ?? 100;
                isHome = LiveWriteFormBinder.Flag(form, "isHome", "is_home");
                parentCode = LiveWriteFormBinder.Text(form, "parentCode", "parent_code");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SaveAsync(id, code, name, currencyCode, ownershipPct, isHome, parentCode, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/consolidations-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new ErpConsEntitySaveRequest(id, code, name, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxConsEntityDelete, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpConsEntityDeleteDryRun dryRun,
            IErpConsDeleteWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/consolidations-app", "Admin ERP capability required for consolidation entity delete.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpConsEntityDeleteBody>(context, cancellationToken) ?? new(0, false);
            var id = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "entityId", "entity_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpConsEntityDeleteRequest(id, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.DeleteEntityAsync(id, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/consolidations-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxConsFiguresSave, async (HttpContext context, ErpConsFiguresSaveBody? body, ILegacySessionValidator validator, IErpConsFiguresSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpConsFiguresSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxConsIcSave, async (HttpContext context, ErpConsIcSaveBody? body, ILegacySessionValidator validator, IErpConsIcSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpConsIcSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxConsIcDelete, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpConsIcDeleteDryRun dryRun,
            IErpConsDeleteWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/consolidations-app", "Admin ERP capability required for consolidation IC delete.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpConsIcDeleteBody>(context, cancellationToken) ?? new(0, false);
            var id = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "icId", "ic_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpConsIcDeleteRequest(id, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.DeleteIcAsync(id, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/consolidations-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxMfgBomSave, async (HttpContext context, ErpMfgBomSaveBody? body, ILegacySessionValidator validator, IErpMfgBomSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpMfgBomSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxMfgWoCreate, async (HttpContext context, ErpMfgWoCreateBody? body, ILegacySessionValidator validator, IErpMfgWoCreateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpMfgWoCreateRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxMfgWoIssue, async (HttpContext context, ErpMfgWoIssueBody? body, ILegacySessionValidator validator, IErpMfgWoIssueDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpMfgWoIssueRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxMfgWoComplete, async (HttpContext context, ErpMfgWoCompleteBody? body, ILegacySessionValidator validator, IErpMfgWoCompleteDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpMfgWoCompleteRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPayrollGenerate, async (HttpContext context, ErpPayrollGenerateBody? body, ILegacySessionValidator validator, IErpPayrollGenerateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpPayrollGenerateRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPayrollApprove, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpPayrollApproveDryRun dryRun,
            IErpPayrollWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/payroll-app", "Admin ERP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpPayrollApproveBody>(context, cancellationToken) ?? new();
            var id = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "runId", "run_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpPayrollApproveRequest(id, body.Code, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.ApproveRunAsync(id, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/payroll-app",
                written.Succeeded,
                written.Message,
                new
                {
                    ok = written.Succeeded,
                    status = written.Succeeded,
                    surface = "erp",
                    writes = written.Writes,
                    writesBlocked = false,
                    phpAuthoritative = false,
                    validation_code = written.Code,
                    message = written.Message,
                    result = new { id = written.Id },
                    session = SessionPayload(session),
                });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPayrollPay, async (HttpContext context, ErpPayrollPayBody? body, ILegacySessionValidator validator, IErpPayrollPayDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPayrollPayRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPayrollUpdateDays, async (HttpContext context, ErpPayrollUpdateDaysBody? body, ILegacySessionValidator validator, IErpPayrollUpdateDaysDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPayrollUpdateDaysRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxUaeTaxFtaFetch, async (HttpContext context, ErpUaeTaxFtaFetchBody? body, ILegacySessionValidator validator, IErpUaeTaxFtaFetchDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpUaeTaxFtaFetchRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxAmlCheck, async (HttpContext context, ErpAmlCheckBody? body, ILegacySessionValidator validator, IErpAmlCheckDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpAmlCheckRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxAmlReportGenerate, async (HttpContext context, ErpAmlReportGenerateBody? body, ILegacySessionValidator validator, IErpAmlReportGenerateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpAmlReportGenerateRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxAmlSeedRules, async (HttpContext context, ErpAmlSeedRulesBody? body, ILegacySessionValidator validator, IErpAmlSeedRulesDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpAmlSeedRulesRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxUaeTaxLegislationRegenSummaries, async (HttpContext context, ErpUaeTaxLegislationRegenSummariesBody? body, ILegacySessionValidator validator, IErpUaeTaxLegislationRegenSummariesDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpUaeTaxLegislationRegenSummariesRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxUaeTaxLegislationAsk, async (HttpContext context, ErpUaeTaxLegislationAskBody? body, ILegacySessionValidator validator, IErpUaeTaxLegislationAskDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpUaeTaxLegislationAskRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxUaeTaxSaveCtAdjustments, async (HttpContext context, ErpUaeTaxSaveCtAdjustmentsBody? body, ILegacySessionValidator validator, IErpUaeTaxSaveCtAdjustmentsDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpUaeTaxSaveCtAdjustmentsRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxUaeTaxLegislationChecklistSet, async (HttpContext context, ErpUaeTaxLegislationChecklistSetBody? body, ILegacySessionValidator validator, IErpUaeTaxLegislationChecklistSetDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpUaeTaxLegislationChecklistSetRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInvoiceSave, async (HttpContext context, ErpInvoiceSaveBody? body, ILegacySessionValidator validator, IErpInvoiceSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpInvoiceSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInvoiceList, async (HttpContext context, ErpInvoiceListBody? body, ILegacySessionValidator validator, IErpInvoiceListDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpInvoiceListRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInvoiceFromOrder, async (HttpContext context, ErpInvoiceFromOrderBody? body, ILegacySessionValidator validator, IErpInvoiceFromOrderDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpInvoiceFromOrderRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxAiQuery, async (HttpContext context, ErpAiQueryBody? body, ILegacySessionValidator validator, IErpAiQueryDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpAiQueryRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxIntegrityScan, async (HttpContext context, ErpIntegrityScanBody? body, ILegacySessionValidator validator, IErpIntegrityScanDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpIntegrityScanRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxIntegrityApplyFks, async (HttpContext context, ErpIntegrityApplyFksBody? body, ILegacySessionValidator validator, IErpIntegrityApplyFksDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpIntegrityApplyFksRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxFaCreateAsset, async (HttpContext context, ErpFaCreateAssetBody? body, ILegacySessionValidator validator, IErpFaCreateAssetDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpFaCreateAssetRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxFaRunDepreciation, async (HttpContext context, ErpFaRunDepreciationBody? body, ILegacySessionValidator validator, IErpFaRunDepreciationDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpFaRunDepreciationRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOpeningCreateBatch, async (HttpContext context, ErpOpeningCreateBatchBody? body, ILegacySessionValidator validator, IErpOpeningCreateBatchDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpOpeningCreateBatchRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOpeningAddCoaLine, async (HttpContext context, ErpOpeningAddCoaLineBody? body, ILegacySessionValidator validator, IErpOpeningAddCoaLineDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpOpeningAddCoaLineRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOpeningAddInvLine, async (HttpContext context, ErpOpeningAddInvLineBody? body, ILegacySessionValidator validator, IErpOpeningAddInvLineDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpOpeningAddInvLineRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOpeningPostBatch, async (HttpContext context, ErpOpeningPostBatchBody? body, ILegacySessionValidator validator, IErpOpeningPostBatchDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpOpeningPostBatchRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxSaveRfq, async (HttpContext context, ErpSaveRfqBody? body, ILegacySessionValidator validator, IErpSaveRfqDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpSaveRfqRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxDeliveryNoteCreate, async (HttpContext context, ErpDeliveryNoteCreateBody? body, ILegacySessionValidator validator, IErpDeliveryNoteCreateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpDeliveryNoteCreateRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxSaveContact, async (HttpContext context, ErpSaveContactBody? body, ILegacySessionValidator validator, IErpSaveContactDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpSaveContactRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxSyncContacts, async (HttpContext context, ErpSyncContactsBody? body, ILegacySessionValidator validator, IErpSyncContactsDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpSyncContactsRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxDocumentUpload, async (HttpContext context, ErpDocumentUploadBody? body, ILegacySessionValidator validator, IErpDocumentUploadDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpDocumentUploadRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxDocumentDelete, async (HttpContext context, ErpDocumentDeleteBody? body, ILegacySessionValidator validator, IErpDocumentDeleteDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpDocumentDeleteRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxSaveCompany, async (HttpContext context, ErpSaveCompanyBody? body, ILegacySessionValidator validator, IErpSaveCompanyDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpSaveCompanyRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxSaveTemplate, async (HttpContext context, ErpSaveTemplateBody? body, ILegacySessionValidator validator, IErpSaveTemplateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpSaveTemplateRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxUploadLogo, async (HttpContext context, ErpUploadLogoBody? body, ILegacySessionValidator validator, IErpUploadLogoDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpUploadLogoRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxUploadAttachment, async (HttpContext context, ErpUploadAttachmentBody? body, ILegacySessionValidator validator, IErpUploadAttachmentDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpUploadAttachmentRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxDeleteAttachment, async (HttpContext context, ErpDeleteAttachmentBody? body, ILegacySessionValidator validator, IErpDeleteAttachmentDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpDeleteAttachmentRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxSyncEinvoiceSeller, async (HttpContext context, ErpSyncEinvoiceSellerBody? body, ILegacySessionValidator validator, IErpSyncEinvoiceSellerDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpSyncEinvoiceSellerRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxExpenseReportSave, async (HttpContext context, ErpExpenseReportSaveBody? body, ILegacySessionValidator validator, IErpExpenseReportSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpExpenseReportSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        // Live write (PHP po_save parity) when confirmWrites=true; otherwise the Wave B dry-run gate.
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPoSave, async (HttpContext context, ErpPoSaveBody? body, ILegacySessionValidator validator, IErpPoSaveDryRun dryRun, IErpPurchaseOrderWriteService writes, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required.");
            body ??= new();
            if (!body.ConfirmWrites)
                return Results.Ok(dryRun.Evaluate(new ErpPoSaveRequest(body.Id, body.Code, false)).ToPayload(SessionPayload(session)));

            return await ExecuteErpWriteAsync(session, async () =>
            {
                var saved = await writes.SaveAsync(
                    new ErpPurchaseOrderInput
                    {
                        Id = body.Id,
                        SupplierId = body.SupplierId,
                        Title = body.Title ?? string.Empty,
                        AmountExVat = body.AmountExVat,
                        Status = body.Status ?? string.Empty,
                        Notes = body.Notes ?? string.Empty,
                        ExpectedVersion = body.ExpectedVersion,
                        LinesJson = body.LinesJson,
                    },
                    session.UserId,
                    cancellationToken);
                return (saved.Created ? "Purchase order created" : "Purchase order updated", new
                {
                    id = saved.Id,
                    po_no = saved.PoNo,
                    amount_ex_vat = saved.AmountExVat,
                    vat_amount = saved.VatAmount,
                    total_amount = saved.TotalAmount,
                    status = saved.Status,
                    lines = saved.LinesAdded,
                });
            });
        });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPoStatus, async (HttpContext context, ErpPoStatusBody? body, ILegacySessionValidator validator, IErpPoStatusDryRun dryRun, IErpPurchaseOrderWriteService writes, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required.");
            body ??= new(0,null,false);
            if (!body.ConfirmWrites)
                return Results.Ok(dryRun.Evaluate(new ErpPoStatusRequest(body.Id, body.TargetStatus, false)).ToPayload(SessionPayload(session)));

            return await ExecuteErpWriteAsync(session, async () =>
            {
                await writes.SetStatusAsync(body.Id, body.TargetStatus ?? string.Empty, session.UserId, cancellationToken);
                return ("PO status updated", new { id = body.Id, status = body.TargetStatus ?? string.Empty });
            });
        });
        // Live writes (PHP po_receive_lines / po_to_invoice parity) when confirmWrites=true.
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPoReceiveLines, async (HttpContext context, ErpPoReceiveLinesBody? body, ILegacySessionValidator validator, IErpPoReceiveLinesDryRun dryRun, IErpPurchaseOrderWriteService writes, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required.");
            body ??= new();
            if (!body.ConfirmWrites)
                return Results.Ok(dryRun.Evaluate(new ErpPoReceiveLinesRequest(body.Id, body.Code, false)).ToPayload(SessionPayload(session)));

            return await ExecuteErpWriteAsync(session, async () =>
            {
                var received = await writes.ReceiveLinesAsync(
                    body.Id,
                    ErpPurchaseOrderWriteService.ParseReceivedJson(body.ReceivedJson),
                    session.UserId,
                    cancellationToken);
                return ("Purchase order lines received", new
                {
                    po_id = received.PurchaseOrderId,
                    status = received.Status,
                    qty_received = received.QtyReceived,
                    qty_open = received.QtyOpen,
                });
            });
        });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPoToInvoice, async (HttpContext context, ErpPoToInvoiceBody? body, ILegacySessionValidator validator, IErpPoToInvoiceDryRun dryRun, IErpPurchaseInvoiceWriteService writes, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required.");
            body ??= new(0,null,false);
            if (!body.ConfirmWrites)
                return Results.Ok(dryRun.Evaluate(new ErpPoToInvoiceRequest(body.Id, body.Code, false)).ToPayload(SessionPayload(session)));

            return await ExecuteErpWriteAsync(session, async () =>
            {
                var converted = await writes.ConvertPurchaseOrderAsync(body.Id, session.UserId, cancellationToken);
                return ("Purchase invoice " + converted.VoucherNo + " created", new
                {
                    po_id = converted.PurchaseOrderId,
                    purchase_id = converted.PurchaseId,
                    voucher_no = converted.VoucherNo,
                    amount_ex_vat = converted.AmountExVat,
                    vat_amount = converted.VatAmount,
                    total_amount = converted.TotalAmount,
                });
            });
        });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCustomerCreate, async (HttpContext context, ErpCustomerCreateBody? body, ILegacySessionValidator validator, IErpCustomerCreateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpCustomerCreateRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        // Live write (PHP so_save parity) when confirmWrites=true; otherwise the Wave B dry-run gate.
        endpoints.MapPost(EcomAeRoutes.ErpAjaxSoSave, async (HttpContext context, ErpSoSaveBody? body, ILegacySessionValidator validator, IErpSoSaveDryRun dryRun, IErpSalesOrderWriteService writes, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required.");
            body ??= new();
            if (!body.ConfirmWrites)
                return Results.Ok(dryRun.Evaluate(new ErpSoSaveRequest(body.Id, body.Code, false)).ToPayload(SessionPayload(session)));

            return await ExecuteErpWriteAsync(session, async () =>
            {
                var saved = await writes.SaveAsync(
                    new ErpSalesOrderInput
                    {
                        Id = body.Id,
                        CustomerUserId = body.CustomerUserId,
                        ContactId = body.ContactId,
                        Title = body.Title ?? string.Empty,
                        AmountExVat = body.AmountExVat,
                        Status = body.Status ?? string.Empty,
                        Notes = body.Notes ?? string.Empty,
                        Export = body.Export,
                        LinesJson = body.LinesJson,
                    },
                    session.UserId,
                    cancellationToken);
                return ("Sales order saved", new
                {
                    id = saved.Id,
                    so_no = saved.SoNo,
                    amount_ex_vat = saved.AmountExVat,
                    vat_amount = saved.VatAmount,
                    total_amount = saved.TotalAmount,
                    status = saved.Status,
                });
            });
        });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxSoStatus, async (HttpContext context, ErpSoStatusBody? body, ILegacySessionValidator validator, IErpSoStatusDryRun dryRun, IErpSalesOrderWriteService writes, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required.");
            body ??= new(0,null,false);
            if (!body.ConfirmWrites)
                return Results.Ok(dryRun.Evaluate(new ErpSoStatusRequest(body.Id, body.TargetStatus, false)).ToPayload(SessionPayload(session)));

            return await ExecuteErpWriteAsync(session, async () =>
            {
                await writes.SetStatusAsync(body.Id, body.TargetStatus ?? string.Empty, session.UserId, cancellationToken);
                return ("Sales order status updated", new { id = body.Id, status = body.TargetStatus ?? string.Empty });
            });
        });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxSoToInvoice, async (HttpContext context, ErpSoToInvoiceBody? body, ILegacySessionValidator validator, IErpSoToInvoiceDryRun dryRun, IErpSalesInvoiceWriteService writes, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required.");
            body ??= new(0,null,false);
            if (!body.ConfirmWrites)
                return Results.Ok(dryRun.Evaluate(new ErpSoToInvoiceRequest(body.Id, body.Code, false)).ToPayload(SessionPayload(session)));

            return await ExecuteErpWriteAsync(session, async () =>
            {
                var invoice = await writes.ConvertSalesOrderAsync(body.Id, session.UserId, cancellationToken);
                return ("Sales order converted to tax invoice", new
                {
                    sales_order_id = invoice.SalesOrderId,
                    sales_invoice_id = invoice.SalesInvoiceId,
                    invoice_number = invoice.InvoiceNumber,
                    subtotal_ex_vat = invoice.SubtotalExVat,
                    total_vat = invoice.TotalVat,
                    total_incl_vat = invoice.TotalInclVat,
                    ledger_id = invoice.LedgerId,
                });
            });
        });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxTransferVoucher, async (
            HttpContext context,
            ErpTransferVoucherBody? body,
            ILegacySessionValidator validator,
            IErpTransferVoucherDryRun dryRun,
            IErpCashWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required.");
            }

            body ??= new ErpTransferVoucherBody();
            if (!body.ConfirmWrites)
            {
                return Results.Ok(dryRun
                    .Evaluate(new ErpTransferVoucherRequest(body.Id, body.Code, false))
                    .ToPayload(SessionPayload(session)));
            }

            return await ExecuteErpWriteAsync(session, async () =>
            {
                var saved = await writes.TransferVoucherAsync(
                    new ErpTransferVoucherInput
                    {
                        FromAccountId = (int)body.FromAccountId,
                        ToAccountId = (int)body.ToAccountId,
                        Amount = body.Amount,
                        Note = body.Note ?? string.Empty,
                    },
                    session.UserId,
                    cancellationToken);
                return ("Transfer voucher posted", (object)new
                {
                    voucher_no = saved.VoucherNo,
                    out_id = saved.OutEntryId,
                    in_id = saved.InEntryId,
                });
            });
        });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPaymentBatchSave, async (HttpContext context, ErpPaymentBatchSaveBody? body, ILegacySessionValidator validator, IErpPaymentBatchSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPaymentBatchSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPettyCashSave, async (HttpContext context, ErpPettyCashSaveBody? body, ILegacySessionValidator validator, IErpPettyCashSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPettyCashSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxAgendaSave, async (HttpContext context, ErpAgendaSaveBody? body, ILegacySessionValidator validator, IErpAgendaSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpAgendaSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxKbSave, async (HttpContext context, ErpKbSaveBody? body, ILegacySessionValidator validator, IErpKbSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpKbSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxMultiEntitySave, async (HttpContext context, ErpMultiEntitySaveBody? body, ILegacySessionValidator validator, IErpMultiEntitySaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpMultiEntitySaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCsSaveDeclaration, async (HttpContext context, ErpCsSaveDeclarationBody? body, ILegacySessionValidator validator, IErpCsSaveDeclarationDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpCsSaveDeclarationRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCsSubmitDeclaration, async (HttpContext context, ErpCsSubmitDeclarationBody? body, ILegacySessionValidator validator, IErpCsSubmitDeclarationDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpCsSubmitDeclarationRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCsDeleteDeclaration, async (HttpContext context, ErpCsDeleteDeclarationBody? body, ILegacySessionValidator validator, IErpCsDeleteDeclarationDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpCsDeleteDeclarationRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCsListDeclarations, async (HttpContext context, ErpCsListDeclarationsBody? body, ILegacySessionValidator validator, IErpCsListDeclarationsDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpCsListDeclarationsRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCsImportDeclarationPdf, async (HttpContext context, ErpCsImportDeclarationPdfBody? body, ILegacySessionValidator validator, IErpCsImportDeclarationPdfDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpCsImportDeclarationPdfRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxShortcutList, async (HttpContext context, ErpShortcutListBody? body, ILegacySessionValidator validator, IErpShortcutListDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpShortcutListRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxShortcutAdd, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpShortcutAddDryRun dryRun,
            IErpWorkspaceFavoritesWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/workspace-favorites-app", "Admin ERP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpShortcutAddBody>(context, cancellationToken) ?? new();
            var label = body.Label;
            var targetUrl = body.TargetUrl ?? body.Code;
            var shortcutKey = body.ShortcutKey;
            var surface = body.Surface;
            var iconClass = body.IconClass;
            var iconColor = body.IconColor;
            var targetTab = body.TargetTab;
            var companyId = body.CompanyId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                label = LiveWriteFormBinder.Text(form, "label");
                targetUrl = LiveWriteFormBinder.Text(form, "targetUrl", "target_url", "url");
                shortcutKey = LiveWriteFormBinder.Text(form, "shortcutKey", "shortcut_key", "key");
                surface = LiveWriteFormBinder.Text(form, "surface");
                iconClass = LiveWriteFormBinder.Text(form, "iconClass", "icon_class");
                iconColor = LiveWriteFormBinder.Text(form, "iconColor", "icon_color");
                targetTab = LiveWriteFormBinder.Text(form, "targetTab", "target_tab");
                companyId = LiveWriteFormBinder.Long(form, "companyId", "company_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpShortcutAddRequest(body.Id, targetUrl, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.AddShortcutAsync(
                session.UserId,
                label,
                targetUrl,
                shortcutKey,
                surface,
                iconClass,
                iconColor,
                targetTab,
                companyId,
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/workspace-favorites-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxShortcutDelete, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpShortcutDeleteDryRun dryRun,
            IErpWorkspaceFavoritesWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/workspace-favorites-app", "Admin ERP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpShortcutDeleteBody>(context, cancellationToken) ?? new();
            var id = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "shortcutId", "shortcut_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpShortcutDeleteRequest(id, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.DeleteShortcutAsync(session.UserId, id, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/workspace-favorites-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxShortcutDeleteKey, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpShortcutDeleteKeyDryRun dryRun,
            IErpWorkspaceFavoritesWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/workspace-favorites-app", "Admin ERP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpShortcutDeleteKeyBody>(context, cancellationToken) ?? new();
            var key = body.ShortcutKey ?? body.Code;
            var surface = body.Surface;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                key = LiveWriteFormBinder.Text(form, "shortcutKey", "shortcut_key", "key");
                surface = LiveWriteFormBinder.Text(form, "surface");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpShortcutDeleteKeyRequest(body.Id, key, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.DeleteShortcutByKeyAsync(session.UserId, key, surface, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/workspace-favorites-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxShortcutReset, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpShortcutResetDryRun dryRun,
            IErpWorkspaceFavoritesWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/workspace-favorites-app", "Admin ERP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpShortcutResetBody>(context, cancellationToken) ?? new();
            var surface = body.Surface ?? body.Code;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                surface = LiveWriteFormBinder.Text(form, "surface");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpShortcutResetRequest(body.Id, surface, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.ResetShortcutsAsync(session.UserId, surface, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/workspace-favorites-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxShortcutReorder, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpShortcutReorderDryRun dryRun,
            IErpWorkspaceFavoritesWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/workspace-favorites-app", "Admin ERP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpShortcutReorderBody>(context, cancellationToken) ?? new();
            var ids = ErpWorkspaceFavoritesWriteService.ParseShortcutIds(body.Ids ?? body.Code);
            if (body.Id > 0 && ids.Count == 0)
            {
                ids = [body.Id];
            }

            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                ids = LiveWriteFormBinder.Longs(form, "ids", "id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpShortcutReorderRequest(body.Id, body.Ids ?? body.Code, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.ReorderShortcutsAsync(session.UserId, ids, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/workspace-favorites-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxErpFavAdd, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpErpFavAddDryRun dryRun,
            IErpWorkspaceFavoritesWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/workspace-favorites-app", "Admin ERP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpErpFavAddBody>(context, cancellationToken) ?? new();
            var tabKey = body.TabKey ?? body.Code;
            var areaKey = body.AreaKey;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                tabKey = LiveWriteFormBinder.Text(form, "tabKey", "tab_key");
                areaKey = LiveWriteFormBinder.Text(form, "areaKey", "area_key");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpErpFavAddRequest(body.Id, tabKey, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.AddAsync(session.UserId, areaKey, tabKey, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/workspace-favorites-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxErpFavRemove, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpErpFavRemoveDryRun dryRun,
            IErpWorkspaceFavoritesWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/workspace-favorites-app", "Admin ERP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpErpFavRemoveBody>(context, cancellationToken) ?? new();
            var tabKey = body.TabKey ?? body.Code;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                tabKey = LiveWriteFormBinder.Text(form, "tabKey", "tab_key");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpErpFavRemoveRequest(body.Id, tabKey, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.RemoveAsync(session.UserId, tabKey, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/workspace-favorites-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxErpGlobalSearch, async (HttpContext context, ErpErpGlobalSearchBody? body, ILegacySessionValidator validator, IErpErpGlobalSearchDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpErpGlobalSearchRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxJwRepairCreate, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwRepairCreateDryRun dryRun,
            IErpJwRepairWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session)
                && (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-repairs-app",
                    "Admin ERP capability required for jewellery repair create.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwRepairCreateBody>(context, cancellationToken)
                       ?? new();
            var repairNo = body.RepairNo;
            var customerId = body.CustomerId;
            var customerName = body.CustomerName;
            if (string.IsNullOrWhiteSpace(customerName)) customerName = body.Code;
            var customerPhone = body.CustomerPhone;
            var itemDescription = body.ItemDescription;
            var metalType = body.MetalType;
            var karat = body.Karat;
            var grossWtIn = body.GrossWtIn;
            var netWtIn = body.NetWtIn;
            var stoneDetails = body.StoneDetails;
            var repairType = body.RepairType;
            var estimatedCost = body.EstimatedCost;
            var receivedDate = body.ReceivedDate;
            var promisedDate = body.PromisedDate;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                repairNo = LiveWriteFormBinder.Text(form, "repairNo", "repair_no");
                customerId = LiveWriteFormBinder.Long(form, "customerId", "customer_id");
                customerName = LiveWriteFormBinder.Text(form, "customerName", "customer_name", "code");
                customerPhone = LiveWriteFormBinder.Text(form, "customerPhone", "customer_phone");
                itemDescription = LiveWriteFormBinder.Text(form, "itemDescription", "item_description");
                metalType = LiveWriteFormBinder.Text(form, "metalType", "metal_type");
                karat = LiveWriteFormBinder.Text(form, "karat");
                grossWtIn = LiveWriteFormBinder.Dec(form, "grossWtIn", "gross_wt_in");
                netWtIn = LiveWriteFormBinder.Dec(form, "netWtIn", "net_wt_in");
                stoneDetails = LiveWriteFormBinder.Text(form, "stoneDetails", "stone_details");
                repairType = LiveWriteFormBinder.Text(form, "repairType", "repair_type");
                estimatedCost = LiveWriteFormBinder.Dec(form, "estimatedCost", "estimated_cost");
                receivedDate = LiveWriteFormBinder.Long(form, "receivedDate", "received_date");
                promisedDate = LiveWriteFormBinder.Long(form, "promisedDate", "promised_date");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwRepairCreateRequest(body.Id, customerName, false));
                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.CreateAsync(
                new ErpJwRepairSaveRequest(
                    repairNo,
                    customerId,
                    customerName,
                    customerPhone,
                    itemDescription,
                    metalType,
                    karat,
                    grossWtIn,
                    netWtIn,
                    stoneDetails,
                    repairType,
                    estimatedCost,
                    receivedDate,
                    promisedDate,
                    session.UserId),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/jewellery-repairs-app?tab=jw_repairs",
                written.Succeeded,
                written.Message,
                new
                {
                    ok = written.Succeeded,
                    status = written.Succeeded,
                    writes = written.Writes,
                    phpAuthoritative = false,
                    validation_code = written.Code,
                    message = written.Message,
                    id = written.Id,
                    repair_id = written.Id,
                    session = SessionPayload(session)
                });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxJwRepairUpdateStatus, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwRepairUpdateStatusDryRun dryRun,
            IErpJwRepairWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/jewellery-repairs-app", "Admin ERP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwRepairUpdateStatusBody>(context, cancellationToken) ?? new();
            var repairId = body.RepairId > 0 ? body.RepairId : body.Id;
            var status = body.NewStatus ?? body.TargetStatus;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                repairId = LiveWriteFormBinder.Long(form, "repairId", "repair_id", "id");
                status = LiveWriteFormBinder.Text(form, "newStatus", "new_status", "targetStatus", "status");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpJwRepairUpdateStatusRequest(repairId, status, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.SetStatusAsync(repairId, status, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/jewellery-repairs-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxJwSeedSampleData, async (HttpContext context, ErpJwSeedSampleDataBody? body, ILegacySessionValidator validator, IErpJwSeedSampleDataDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpJwSeedSampleDataRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxAiAssistantQuery, async (HttpContext context, ErpAiAssistantQueryBody? body, ILegacySessionValidator validator, IErpAiAssistantQueryDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpAiAssistantQueryRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPrintDesignerSave, async (HttpContext context, ErpPrintDesignerSaveBody? body, ILegacySessionValidator validator, IErpPrintDesignerSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPrintDesignerSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxWorkflowSave, async (HttpContext context, ErpWorkflowSaveBody? body, ILegacySessionValidator validator, IErpWorkflowSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpWorkflowSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxWorkflowRun, async (HttpContext context, ErpWorkflowRunBody? body, ILegacySessionValidator validator, IErpWorkflowRunDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpWorkflowRunRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxAutomationActivate, async (HttpContext context, ErpAutomationActivateBody? body, ILegacySessionValidator validator, IErpAutomationActivateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpAutomationActivateRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxAutomationDeactivate, async (HttpContext context, ErpAutomationDeactivateBody? body, ILegacySessionValidator validator, IErpAutomationDeactivateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpAutomationDeactivateRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxAutomationInstallTemplate, async (HttpContext context, ErpAutomationInstallTemplateBody? body, ILegacySessionValidator validator, IErpAutomationInstallTemplateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpAutomationInstallTemplateRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxAutomationEnableCategory, async (HttpContext context, ErpAutomationEnableCategoryBody? body, ILegacySessionValidator validator, IErpAutomationEnableCategoryDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpAutomationEnableCategoryRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxAutomationTick, async (HttpContext context, ErpAutomationTickBody? body, ILegacySessionValidator validator, IErpAutomationTickDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpAutomationTickRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxTenantConfigSave, async (HttpContext context, ErpTenantConfigSaveBody? body, ILegacySessionValidator validator, IErpTenantConfigSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpTenantConfigSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });

        endpoints.MapPost(EcomAeRoutes.ErpOnPremisesSetupWizardDryRun, (
            OnPremisesSetupWizardBody? body,
            IOnPremisesSetupWizardDryRun dryRun) =>
        {
            body ??= new OnPremisesSetupWizardBody(null, false);
            return Results.Ok(dryRun.Evaluate(new OnPremisesSetupWizardRequest(body.TenantCode, body.ConfirmWrites)).ToPayload());
        });

        endpoints.MapPost(EcomAeRoutes.ErpOnPremisesBackupDryRun, (
            OnPremisesBackupBody? body,
            IOnPremisesBackupDryRun dryRun) =>
        {
            body ??= new OnPremisesBackupBody(null, false);
            return Results.Ok(dryRun.Evaluate(new OnPremisesBackupRequest(body.Label, body.ConfirmWrites)).ToPayload());
        });

        endpoints.MapPost(EcomAeRoutes.OnPremisesActivateLicenseCli, (OnPremisesActivateLicenseCliBody? body, IOnPremisesActivateLicenseCliDryRun dryRun) =>
        {
            body ??= new OnPremisesActivateLicenseCliBody(null, false);
            return Results.Ok(dryRun.Evaluate(new OnPremisesActivateLicenseCliRequest(body.Action, body.ConfirmWrites)).ToPayload());
        });
        endpoints.MapPost(EcomAeRoutes.OnPremisesHealthCheckPack, (OnPremisesHealthCheckPackBody? body, IOnPremisesHealthCheckPackDryRun dryRun) =>
        {
            body ??= new OnPremisesHealthCheckPackBody(null, false);
            return Results.Ok(dryRun.Evaluate(new OnPremisesHealthCheckPackRequest(body.Action, body.ConfirmWrites)).ToPayload());
        });


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

            var result = await dashboards.BuildErpAsync(cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary = result.Summary,
                approvalQueue = result.ApprovalQueue,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only migration summary + approval queue. PHP ERP dashboard / command center remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpCompanies, async (
            int? limit,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var result = await dashboards.BuildErpCompaniesDigestAsync(limit ?? 50, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                companies = result.Companies,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                note = "Read-only legal entities for company picker (PHP epc_erp_companies_list). Industry pack apply + session company remain PHP authoritative."
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
            IErpDocLifecycleWriteService lifecycle,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for cash voucher amend dry-run.");
            }

            body ??= new ErpCashVoucherAmendBody(0, null, null, false);
            if (!body.ConfirmWrites)
            {
                var result = await dryRun.EvaluateAsync(
                    new ErpCashVoucherAmendRequest(body.EntryId, body.Reference, body.Note, false),
                    cancellationToken);
                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            return await ExecuteErpWriteAsync(session, async () =>
            {
                await lifecycle.CashVoucherAmendAsync(
                    body.EntryId,
                    body.Reference,
                    body.Note,
                    session.UserId,
                    cancellationToken);
                return ("Voucher narrative updated", (object)new { entry_id = body.EntryId });
            });
        });

        endpoints.MapPost(EcomAeRoutes.ErpCashEntriesVoid, async (
            HttpContext context,
            ErpCashVoucherVoidBody? body,
            ILegacySessionValidator validator,
            IErpCashVoucherVoidDryRun dryRun,
            IErpDocLifecycleWriteService lifecycle,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for cash voucher void dry-run.");
            }

            body ??= new ErpCashVoucherVoidBody(0, null, false);
            if (!body.ConfirmWrites)
            {
                var result = await dryRun.EvaluateAsync(
                    new ErpCashVoucherVoidRequest(body.EntryId, body.Reason, false),
                    cancellationToken);
                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            return await ExecuteErpWriteAsync(session, async () =>
            {
                var voided = await lifecycle.CashVoucherVoidAsync(
                    body.EntryId,
                    body.Reason ?? string.Empty,
                    session.UserId,
                    cancellationToken);
                return (
                    "Voucher voided — reversing journal posted",
                    (object)new
                    {
                        reversal_journal_ids = voided.ReversalJournalIds,
                        voided_ids = voided.VoidedIds,
                    });
            });
        });

        endpoints.MapPost(EcomAeRoutes.ErpCashEntriesCreate, async (
            HttpContext context,
            ErpCashEntryCreateBody? body,
            ILegacySessionValidator validator,
            IErpCashEntryCreateDryRun dryRun,
            IErpCashWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for cash entry create dry-run.");
            }

            body ??= new ErpCashEntryCreateBody(0, 0, false, null, null, null, false);
            if (!body.ConfirmWrites)
            {
                var result = await dryRun.EvaluateAsync(
                    new ErpCashEntryCreateRequest(
                        body.AccountId, body.Amount, body.Direction, body.EntryType,
                        body.Reference, body.Note, false),
                    cancellationToken);
                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            return await ExecuteErpWriteAsync(session, async () =>
            {
                var saved = await writes.CashEntryAsync(
                    new ErpCashEntryInput
                    {
                        AccountId = (int)body.AccountId,
                        Amount = body.Amount,
                        Direction = body.Direction,
                        EntryType = body.EntryType ?? string.Empty,
                        CounterpartyType = body.CounterpartyType ?? "none",
                        CounterpartyId = (int)body.CounterpartyId,
                        Reference = body.Reference ?? string.Empty,
                        VoucherNo = body.VoucherNo ?? string.Empty,
                        Note = body.Note ?? string.Empty,
                    },
                    session.UserId,
                    cancellationToken);
                return ("Cash entry saved", CashPayload(saved));
            });
        });

        endpoints.MapPost(EcomAeRoutes.ErpCashEntriesReceiptVoucher, async (
            HttpContext context,
            ErpReceiptVoucherBody? body,
            ILegacySessionValidator validator,
            IErpReceiptVoucherDryRun dryRun,
            IErpCashWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for receipt voucher dry-run.");
            }

            body ??= new ErpReceiptVoucherBody(0, 0, 0);
            if (!body.ConfirmWrites)
            {
                var result = dryRun.Evaluate(
                    new ErpReceiptVoucherRequest(body.UserId, body.AccountId, body.Amount, body.SalesOrderId, false));
                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            return await ExecuteErpWriteAsync(session, async () =>
            {
                var saved = await writes.ReceiptVoucherAsync(
                    new ErpReceiptVoucherInput
                    {
                        UserId = (int)body.UserId,
                        AccountId = (int)body.AccountId,
                        Amount = body.Amount,
                        SalesOrderId = body.SalesOrderId ?? 0,
                        SalesInvoiceId = body.SalesInvoiceId ?? 0,
                        IsAdvance = body.IsAdvance,
                        PostGl = body.PostGl,
                        OrderId = body.OrderId ?? 0,
                        AutoAllocate = body.AutoAllocate,
                        AllocInvoiceIds = body.AllocInvoiceId,
                        AllocAmounts = body.AllocAmount,
                        Note = body.Note ?? string.Empty,
                    },
                    session.UserId,
                    cancellationToken);
                return ("Receipt voucher posted", CashPayload(saved));
            });
        });

        endpoints.MapPost(EcomAeRoutes.ErpCashEntriesPaymentVoucher, async (
            HttpContext context,
            ErpPaymentVoucherBody? body,
            ILegacySessionValidator validator,
            IErpPaymentVoucherDryRun dryRun,
            IErpCashWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for payment voucher dry-run.");
            }

            body ??= new ErpPaymentVoucherBody(0, 0, 0);
            if (!body.ConfirmWrites)
            {
                var result = dryRun.Evaluate(
                    new ErpPaymentVoucherRequest(body.SupplierId, body.AccountId, body.Amount, false));
                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            return await ExecuteErpWriteAsync(session, async () =>
            {
                var saved = await writes.PaymentVoucherAsync(
                    new ErpPaymentVoucherInput
                    {
                        SupplierId = (int)body.SupplierId,
                        AccountId = (int)body.AccountId,
                        Amount = body.Amount,
                        PurchaseId = body.PurchaseId ?? 0,
                        PurchaseOrderId = body.PurchaseOrderId ?? 0,
                        IsAdvance = body.IsAdvance,
                        AutoAllocate = body.AutoAllocate,
                        AllocInvoiceIds = body.AllocInvoiceId,
                        AllocAmounts = body.AllocAmount,
                        Reference = body.Reference ?? string.Empty,
                        Note = body.Note ?? string.Empty,
                    },
                    session.UserId,
                    cancellationToken);
                return ("Payment voucher posted", CashPayload(saved));
            });
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
            IErpDocLifecycleWriteService lifecycle,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for purchase delete dry-run.");
            }
            body ??= new ErpPurchaseDeleteBody(0, false);
            if (!body.ConfirmWrites)
            {
                var result = await dryRun.EvaluateAsync(
                    new ErpPurchaseDeleteRequest(body.PurchaseId, false),
                    cancellationToken);
                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            return await ExecuteErpWriteAsync(session, async () =>
            {
                await lifecycle.PurchaseDeleteAsync(body.PurchaseId, session.UserId, cancellationToken);
                return ("Draft purchase deleted", (object)new { purchase_id = body.PurchaseId });
            });
        });

        endpoints.MapPost(EcomAeRoutes.ErpPurchasesAmend, async (
            HttpContext context,
            ErpPurchaseAmendBody? body,
            ILegacySessionValidator validator,
            IErpPurchaseAmendDryRun dryRun,
            IErpDocLifecycleWriteService lifecycle,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for purchase amend dry-run.");
            }
            body ??= new ErpPurchaseAmendBody(0, null, null, null, false);
            if (!body.ConfirmWrites)
            {
                var result = await dryRun.EvaluateAsync(
                    new ErpPurchaseAmendRequest(
                        body.PurchaseId, body.InvoiceNumber, body.Note, body.AmountExVat, false),
                    cancellationToken);
                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            return await ExecuteErpWriteAsync(session, async () =>
            {
                await lifecycle.PurchaseAmendAsync(
                    new ErpPurchaseAmendInput
                    {
                        PurchaseId = body.PurchaseId,
                        InvoiceNumber = body.InvoiceNumber,
                        Note = body.Note,
                        AmountExVat = body.AmountExVat,
                    },
                    session.UserId,
                    cancellationToken);
                return ("Purchase updated", (object)new { purchase_id = body.PurchaseId });
            });
        });

        endpoints.MapPost(EcomAeRoutes.ErpSalesOrdersDelete, async (
            HttpContext context,
            ErpSalesOrderDeleteBody? body,
            ILegacySessionValidator validator,
            IErpSalesOrderDeleteDryRun dryRun,
            IErpSalesOrderWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for sales-order delete.");
            }
            body ??= new ErpSalesOrderDeleteBody(0, false);
            if (!body.ConfirmWrites)
            {
                var result = await dryRun.EvaluateAsync(
                    new ErpSalesOrderDeleteRequest(body.SalesOrderId, false),
                    cancellationToken);
                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            return await ExecuteErpWriteAsync(session, async () =>
            {
                await writes.DeleteAsync(body.SalesOrderId, session.UserId, cancellationToken);
                return ("Sales order deleted", new { id = body.SalesOrderId });
            });
        });

        endpoints.MapPost(EcomAeRoutes.ErpCustomersMasterSave, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpCustomerMasterSaveDryRun dryRun,
            IErpCustomerMasterWriteService writes,
            IErpDimensionWriteService dimensions,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/erp/contacts-app?tab=ar_setup",
                    "Admin ERP capability required for customer master-save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpCustomerMasterSaveBody>(context, cancellationToken)
                       ?? new();
            var customerId = body.CustomerId;
            var customerAccount = body.CustomerAccount;
            var customerName = body.CustomerName;
            var customerGroup = body.CustomerGroup;
            long? legalEntityId = body.LegalEntityId;
            long? businessUnitId = body.BusinessUnitId;
            var currencyCode = body.CurrencyCode;
            var paymentMethod = body.PaymentMethod;
            var deliveryTerms = body.DeliveryTerms;
            var deliveryMode = body.DeliveryMode;
            var trn = body.Trn;
            var taxExempt = body.TaxExempt;
            var salesTaxGroup = body.SalesTaxGroup;
            var contactPerson = body.ContactPerson;
            var contactEmail = body.ContactEmail;
            var contactPhone = body.ContactPhone;
            var website = body.Website;
            var address = body.Address;
            var city = body.City;
            var stateRegion = body.StateRegion;
            var postalCode = body.PostalCode;
            var countryCode = body.CountryCode;
            var creditLimit = body.CreditLimit;
            var termsDays = body.TermsDays;
            var onHold = body.OnHold;
            var riskBand = body.RiskBand;
            var notes = body.Notes;
            IReadOnlyDictionary<string, long>? dim = body.Dim;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                customerId = LiveWriteFormBinder.Long(form, "customerId", "customer_id");
                customerAccount = LiveWriteFormBinder.Text(form, "customerAccount", "customer_account");
                customerName = LiveWriteFormBinder.Text(form, "customerName", "customer_name");
                customerGroup = LiveWriteFormBinder.Text(form, "customerGroup", "customer_group");
                if (form.ContainsKey("legalEntityId") || form.ContainsKey("legal_entity_id"))
                {
                    legalEntityId = LiveWriteFormBinder.Long(form, "legalEntityId", "legal_entity_id");
                }

                if (form.ContainsKey("businessUnitId") || form.ContainsKey("business_unit_id"))
                {
                    businessUnitId = LiveWriteFormBinder.Long(form, "businessUnitId", "business_unit_id");
                }

                currencyCode = LiveWriteFormBinder.Text(form, "currencyCode", "currency_code");
                paymentMethod = LiveWriteFormBinder.Text(form, "paymentMethod", "payment_method");
                deliveryTerms = LiveWriteFormBinder.Text(form, "deliveryTerms", "delivery_terms");
                deliveryMode = LiveWriteFormBinder.Text(form, "deliveryMode", "delivery_mode");
                trn = LiveWriteFormBinder.Text(form, "trn");
                taxExempt = LiveWriteFormBinder.Flag(form, "taxExempt", "tax_exempt");
                salesTaxGroup = LiveWriteFormBinder.Text(form, "salesTaxGroup", "sales_tax_group");
                contactPerson = LiveWriteFormBinder.Text(form, "contactPerson", "contact_person");
                contactEmail = LiveWriteFormBinder.Text(form, "contactEmail", "contact_email");
                contactPhone = LiveWriteFormBinder.Text(form, "contactPhone", "contact_phone");
                website = LiveWriteFormBinder.Text(form, "website");
                address = LiveWriteFormBinder.Text(form, "address");
                city = LiveWriteFormBinder.Text(form, "city");
                stateRegion = LiveWriteFormBinder.Text(form, "stateRegion", "state_region");
                postalCode = LiveWriteFormBinder.Text(form, "postalCode", "postal_code");
                countryCode = LiveWriteFormBinder.Text(form, "countryCode", "country_code");
                creditLimit = LiveWriteFormBinder.DecOrNull(form, "creditLimit", "credit_limit");
                termsDays = LiveWriteFormBinder.IntOrNull(form, "termsDays", "terms_days");
                onHold = LiveWriteFormBinder.Flag(form, "onHold", "on_hold");
                riskBand = LiveWriteFormBinder.Text(form, "riskBand", "risk_band");
                notes = LiveWriteFormBinder.Text(form, "notes");
                dim = ErpDimensionWriteService.ParseDimMap(form);
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpCustomerMasterSaveRequest(
                    customerId, customerName, creditLimit, termsDays, onHold, false));
                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveAsync(
                new ErpCustomerMasterWriteRequest(
                    customerId,
                    customerAccount,
                    customerName,
                    customerGroup,
                    legalEntityId,
                    businessUnitId,
                    currencyCode,
                    paymentMethod,
                    deliveryTerms,
                    deliveryMode,
                    trn,
                    taxExempt,
                    salesTaxGroup,
                    contactPerson,
                    contactEmail,
                    contactPhone,
                    website,
                    address,
                    city,
                    stateRegion,
                    postalCode,
                    countryCode,
                    creditLimit,
                    termsDays,
                    onHold,
                    riskBand,
                    notes),
                cancellationToken);
            if (written.Succeeded && dim is { Count: > 0 })
            {
                await dimensions.SaveAsync("customer", customerId, dim, cancellationToken);
            }

            return LiveWriteFormBinder.Complete(
                context,
                "/erp/contacts-app?tab=ar_setup",
                written.Succeeded,
                written.Message,
                new
                {
                    ok = written.Succeeded,
                    status = written.Succeeded,
                    writes = written.Writes,
                    phpAuthoritative = false,
                    validation_code = written.Code,
                    message = written.Message,
                    customer_id = customerId,
                    id = written.Id,
                    session = SessionPayload(session)
                });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ErpAftersalesRmaCreate, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpAsRmaCreateDryRun dryRun,
            IErpAftersalesRmaWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/returns-rma-app",
                    "Admin ERP capability required for aftersales RMA create.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpAsRmaCreateBody>(context, cancellationToken)
                       ?? new();
            var customerId = body.CustomerId;
            var sourceId = body.SourceId;
            var rmaNo = body.RmaNo;
            var reason = body.Reason;
            var restock = body.Restock;
            var confirm = body.ConfirmWrites;
            var writeLines = (body.Lines ?? [])
                .Select(l => new ErpAftersalesRmaLine(l.ItemId, l.Qty, l.UnitPrice, l.ConditionNote))
                .ToList();
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                customerId = LiveWriteFormBinder.Long(form, "customerId", "customer_id");
                sourceId = LiveWriteFormBinder.Long(form, "sourceId", "source_id");
                rmaNo = LiveWriteFormBinder.Text(form, "rmaNo", "rma_no");
                reason = LiveWriteFormBinder.Text(form, "reason");
                restock = LiveWriteFormBinder.Flag(form, "restock");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
                writeLines = ErpAftersalesRmaWriteService.ParseLinesCsv(
                        LiveWriteFormBinder.Text(form, "linesCsv", "lines_csv"))
                    .ToList();
                var itemId = LiveWriteFormBinder.Long(form, "itemId", "item_id");
                var qty = LiveWriteFormBinder.Dec(form, "qty");
                if (itemId > 0 || qty > 0)
                {
                    writeLines.Add(new ErpAftersalesRmaLine(
                        itemId,
                        qty,
                        LiveWriteFormBinder.Dec(form, "unitPrice", "unit_price"),
                        LiveWriteFormBinder.Text(form, "conditionNote", "condition_note")));
                }
            }

            var dryLines = writeLines
                .Select(l => new ErpAsRmaCreateLine(l.ItemId, l.Qty, l.UnitPrice, l.ConditionNote))
                .ToList();
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpAsRmaCreateRequest(
                    customerId, sourceId, rmaNo, reason, restock, dryLines, false));
                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.CreateAsync(
                new ErpAftersalesRmaCreateRequest(customerId, sourceId, rmaNo, reason, restock, writeLines),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/returns-rma-app",
                written.Succeeded,
                written.Message,
                new
                {
                    ok = written.Succeeded,
                    status = written.Succeeded,
                    writes = written.Writes,
                    phpAuthoritative = false,
                    validation_code = written.Code,
                    message = written.Message,
                    id = written.Id,
                    rma_id = written.Id,
                    session = SessionPayload(session)
                });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAftersalesRmaResolve, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpAftersalesRmaWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/returns-rma-app",
                    "Admin ERP capability required for aftersales RMA resolve.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpAsRmaResolveBody>(context, cancellationToken)
                       ?? new();
            var rmaId = body.RmaId;
            var disposition = body.Disposition;
            var refundAmount = body.RefundAmount;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                rmaId = LiveWriteFormBinder.Long(form, "rmaId", "rma_id", "id");
                disposition = LiveWriteFormBinder.Text(form, "disposition");
                refundAmount = form["refund_amount"].Count > 0 || form["refundAmount"].Count > 0
                    ? LiveWriteFormBinder.Dec(form, "refund_amount", "refundAmount")
                    : -1;
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/cp/returns-rma-app";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("as_rma_resolve", rmaId.ToString(CultureInfo.InvariantCulture), false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.ResolveAsync(new ErpAftersalesRmaResolveRequest(rmaId, disposition, refundAmount), cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                rma_id = written.Id,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAftersalesWarrantyRegister, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpAftersalesWarrantyWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/returns-rma-app",
                    "Admin ERP capability required for aftersales warranty register.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpAsWarrantyRegisterBody>(context, cancellationToken)
                       ?? new();
            var itemId = body.ItemId;
            var serialNo = body.SerialNo;
            var customerId = body.CustomerId;
            var sourceType = body.SourceType;
            var sourceId = body.SourceId;
            var startDate = body.StartDate;
            var months = body.Months;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                itemId = LiveWriteFormBinder.Long(form, "itemId", "item_id");
                serialNo = LiveWriteFormBinder.Text(form, "serialNo", "serial_no");
                customerId = LiveWriteFormBinder.Long(form, "customerId", "customer_id");
                sourceType = LiveWriteFormBinder.Text(form, "sourceType", "source_type");
                sourceId = LiveWriteFormBinder.Long(form, "sourceId", "source_id");
                startDate = LiveWriteFormBinder.Long(form, "startDate", "start_date");
                months = LiveWriteFormBinder.Int(form, "months");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/cp/returns-rma-app";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("as_warranty_register", serialNo, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.RegisterAsync(
                new ErpAftersalesWarrantyRegisterRequest(itemId, serialNo, customerId, sourceType, sourceId, startDate, months),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAftersalesJobCreate, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpAftersalesJobWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/returns-rma-app",
                    "Admin ERP capability required for aftersales job create.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpAsJobCreateBody>(context, cancellationToken)
                       ?? new();
            var jobNo = body.JobNo;
            var customerId = body.CustomerId;
            var assetRef = body.AssetRef;
            var complaint = body.Complaint;
            var underWarranty = body.UnderWarranty;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                jobNo = LiveWriteFormBinder.Text(form, "jobNo", "job_no");
                customerId = LiveWriteFormBinder.Long(form, "customerId", "customer_id");
                assetRef = LiveWriteFormBinder.Text(form, "assetRef", "asset_ref");
                complaint = LiveWriteFormBinder.Text(form, "complaint");
                underWarranty = LiveWriteFormBinder.Flag(form, "underWarranty", "under_warranty");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/cp/returns-rma-app";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("as_job_create", complaint, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.CreateAsync(
                new ErpAftersalesJobCreateRequest(jobNo, customerId, assetRef, complaint, underWarranty),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAftersalesJobAddLine, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpAftersalesJobWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/returns-rma-app",
                    "Admin ERP capability required for aftersales job line add.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpAsJobAddLineBody>(context, cancellationToken)
                       ?? new();
            var jobId = body.JobId;
            var lineType = body.LineType;
            var description = body.Description;
            var itemId = body.ItemId;
            var qty = body.Qty;
            var unitPrice = body.UnitPrice;
            var taxPercent = body.TaxPercent;
            var chargeable = body.Chargeable;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                jobId = LiveWriteFormBinder.Long(form, "jobId", "job_id");
                lineType = LiveWriteFormBinder.Text(form, "lineType", "line_type");
                description = LiveWriteFormBinder.Text(form, "description");
                itemId = LiveWriteFormBinder.Long(form, "itemId", "item_id");
                qty = LiveWriteFormBinder.Dec(form, "qty");
                unitPrice = LiveWriteFormBinder.Dec(form, "unitPrice", "unit_price");
                taxPercent = LiveWriteFormBinder.Dec(form, "taxPercent", "tax_percent");
                chargeable = LiveWriteFormBinder.Flag(form, "chargeable");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/cp/returns-rma-app";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("as_job_add_line", jobId.ToString(CultureInfo.InvariantCulture), false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.AddLineAsync(
                new ErpAftersalesJobAddLineRequest(jobId, lineType, description, itemId, qty, unitPrice, taxPercent, chargeable),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                job_id = jobId,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAftersalesJobClose, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpAftersalesJobWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/returns-rma-app",
                    "Admin ERP capability required for aftersales job close.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpAsJobCloseBody>(context, cancellationToken)
                       ?? new();
            var jobId = body.JobId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                jobId = LiveWriteFormBinder.Long(form, "jobId", "job_id", "id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/cp/returns-rma-app";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("as_job_close", jobId.ToString(CultureInfo.InvariantCulture), false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.CloseAsync(new ErpAftersalesJobCloseRequest(jobId), cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                job_id = written.Id,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();

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
            ILegacySessionValidator validator,
            IErpCustomerSettlementDryRun dryRun,
            IErpCashWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/erp/receivables-app",
                    "Admin ERP capability required for customer settlement.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpCustomerSettlementBody>(context, cancellationToken)
                       ?? new();
            var userId = body.UserId;
            var amount = body.Amount;
            var direction = body.Direction;
            var entryKind = body.EntryKind;
            var orderId = body.OrderId;
            var reference = body.Reference;
            var note = body.Note;
            var postGl = body.PostGl;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                userId = LiveWriteFormBinder.Long(form, "userId", "user_id", "customerId", "customer_id");
                amount = LiveWriteFormBinder.Dec(form, "amount");
                direction = LiveWriteFormBinder.Text(form, "direction");
                entryKind = LiveWriteFormBinder.Text(form, "entryKind", "entry_kind");
                orderId = LiveWriteFormBinder.Long(form, "orderId", "order_id");
                reference = LiveWriteFormBinder.Text(form, "reference");
                note = LiveWriteFormBinder.Text(form, "note");
                postGl = LiveWriteFormBinder.Flag(form, "postGl", "post_gl");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpCustomerSettlementRequest(
                    userId, amount, direction, entryKind, orderId, false));
                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var dir = (direction ?? "credit").Trim().ToLowerInvariant();
            if (dir.Length == 0)
            {
                dir = "credit";
            }

            if (dir is not ("credit" or "debit"))
            {
                return LiveWriteFormBinder.Complete(
                    context,
                    "/erp/receivables-app",
                    false,
                    "Direction must be credit or debit",
                    new
                    {
                        ok = false,
                        status = false,
                        writes = 0,
                        phpAuthoritative = false,
                        validation_code = "invalid",
                        message = "Direction must be credit or debit",
                        session = SessionPayload(session)
                    });
            }

            try
            {
                var ledgerId = await writes.CustomerSettlementAsync(
                    new ErpCustomerSettlementInput
                    {
                        UserId = (int)userId,
                        Amount = amount,
                        Income = dir == "credit",
                        EntryKind = string.IsNullOrWhiteSpace(entryKind) ? "adjustment" : entryKind,
                        OrderId = orderId,
                        Reference = reference ?? string.Empty,
                        Note = note ?? string.Empty,
                        PostGl = postGl,
                    },
                    session.UserId,
                    cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/erp/receivables-app",
                    true,
                    "Customer adjustment/settlement posted",
                    new
                    {
                        ok = true,
                        status = true,
                        writes = 1,
                        phpAuthoritative = false,
                        validation_code = "ok",
                        message = "Customer adjustment/settlement posted",
                        ledger_id = ledgerId,
                        session = SessionPayload(session)
                    });
            }
            catch (ErpWriteException ex)
            {
                return LiveWriteFormBinder.Complete(
                    context,
                    "/erp/receivables-app",
                    false,
                    ex.Message,
                    new
                    {
                        ok = false,
                        status = false,
                        writes = 0,
                        phpAuthoritative = false,
                        validation_code = "invalid",
                        message = ex.Message,
                        session = SessionPayload(session)
                    });
            }
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ErpSupplierSettlement, async (
            HttpContext context,
            ErpSupplierSettlementBody? body,
            ILegacySessionValidator validator,
            IErpSupplierSettlementDryRun dryRun,
            IErpCashWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for supplier settlement dry-run.");
            }
            body ??= new ErpSupplierSettlementBody(0, 0, "decrease", false);
            if (!body.ConfirmWrites)
            {
                var result = dryRun.Evaluate(new ErpSupplierSettlementRequest(
                    body.SupplierId, body.Amount, body.Direction, false));
                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            return await ExecuteErpWriteAsync(session, async () =>
            {
                var settled = await writes.SupplierSettlementAsync(
                    new ErpSupplierSettlementInput
                    {
                        SupplierId = (int)body.SupplierId,
                        Amount = body.Amount,
                        Direction = body.Direction ?? "decrease",
                        EntryKind = body.EntryKind ?? "adjustment",
                        PurchaseId = body.PurchaseId,
                        OrderId = body.OrderId,
                        Reference = body.Reference ?? string.Empty,
                        Note = body.Note ?? string.Empty,
                        Time = body.Time,
                        PostGl = body.PostGl,
                    },
                    session.UserId,
                    cancellationToken);
                return ("Supplier ledger updated", new
                {
                    ledger_id = settled.LedgerId,
                    gl_journal_id = settled.GlJournalId,
                });
            });
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
            ILegacySessionValidator validator,
            IErpOrderSettlementDryRun dryRun,
            IErpCashWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/erp/sales-orders-app",
                    "Admin ERP capability required for order settlement.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpOrderSettlementBody>(context, cancellationToken)
                       ?? new();
            var orderId = body.OrderId;
            var amount = body.Amount;
            var direction = body.Direction;
            var entryKind = body.EntryKind;
            var reference = body.Reference;
            var note = body.Note;
            var postGl = body.PostGl;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                orderId = LiveWriteFormBinder.Long(form, "orderId", "order_id");
                amount = LiveWriteFormBinder.Dec(form, "amount");
                direction = LiveWriteFormBinder.Text(form, "direction");
                entryKind = LiveWriteFormBinder.Text(form, "entryKind", "entry_kind");
                reference = LiveWriteFormBinder.Text(form, "reference");
                note = LiveWriteFormBinder.Text(form, "note");
                postGl = LiveWriteFormBinder.Flag(form, "postGl", "post_gl");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                var result = await dryRun.EvaluateAsync(
                    new ErpOrderSettlementRequest(orderId, amount, direction, false),
                    cancellationToken);
                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var dir = (direction ?? "credit").Trim().ToLowerInvariant();
            if (dir.Length == 0)
            {
                dir = "credit";
            }

            if (dir is not ("credit" or "debit"))
            {
                return LiveWriteFormBinder.Complete(
                    context,
                    "/erp/sales-orders-app",
                    false,
                    "Direction must be credit or debit",
                    new
                    {
                        ok = false,
                        status = false,
                        writes = 0,
                        phpAuthoritative = false,
                        validation_code = "invalid",
                        message = "Direction must be credit or debit",
                        session = SessionPayload(session)
                    });
            }

            try
            {
                var ledgerId = await writes.OrderSettlementAsync(
                    new ErpCustomerSettlementInput
                    {
                        Amount = amount,
                        Income = dir == "credit",
                        EntryKind = string.IsNullOrWhiteSpace(entryKind) ? "adjustment" : entryKind,
                        OrderId = orderId,
                        Reference = reference ?? string.Empty,
                        Note = note ?? string.Empty,
                        PostGl = postGl,
                    },
                    session.UserId,
                    cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/erp/sales-orders-app",
                    true,
                    "Order revenue settlement posted",
                    new
                    {
                        ok = true,
                        status = true,
                        writes = 1,
                        phpAuthoritative = false,
                        validation_code = "ok",
                        message = "Order revenue settlement posted",
                        ledger_id = ledgerId,
                        order_id = orderId,
                        session = SessionPayload(session)
                    });
            }
            catch (ErpWriteException ex)
            {
                return LiveWriteFormBinder.Complete(
                    context,
                    "/erp/sales-orders-app",
                    false,
                    ex.Message,
                    new
                    {
                        ok = false,
                        status = false,
                        writes = 0,
                        phpAuthoritative = false,
                        validation_code = "invalid",
                        message = ex.Message,
                        session = SessionPayload(session)
                    });
            }
        }).DisableAntiforgery();

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
            ILegacySessionValidator validator,
            IErpWorkflowStatusDryRun dryRun,
            IErpWorkflowStatusWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/workflow-app", "Admin ERP capability required for workflow status.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpWorkflowStatusBody>(context, cancellationToken)
                       ?? new ErpWorkflowStatusBody(0, "done", false);
            var taskId = body.TaskId;
            var status = body.Status;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                taskId = LiveWriteFormBinder.Long(form, "taskId", "task_id", "id");
                status = LiveWriteFormBinder.Text(form, "status");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpWorkflowStatusRequest(taskId, status, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.SetStatusAsync(taskId, status, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/workflow-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ErpWorkflowCreate, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpWorkflowCreateDryRun dryRun,
            EcomAE.Platform.Erp.IErpWorkflowCreateWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/workflow-app", "Admin ERP capability required for workflow create.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpWorkflowCreateBody>(context, cancellationToken)
                       ?? new();
            var title = body.Title;
            var departmentCode = body.DepartmentCode;
            var priority = body.Priority;
            var orderId = body.OrderId;
            var description = body.Description;
            var workflowStep = body.WorkflowStep;
            var assignedUserId = body.AssignedUserId;
            var dueAt = body.DueAt;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                title = LiveWriteFormBinder.Text(form, "title");
                departmentCode = LiveWriteFormBinder.Text(form, "departmentCode", "department_code", "department");
                priority = LiveWriteFormBinder.Text(form, "priority");
                orderId = LiveWriteFormBinder.Long(form, "orderId", "order_id");
                description = LiveWriteFormBinder.Text(form, "description");
                workflowStep = LiveWriteFormBinder.Text(form, "workflowStep", "workflow_step", "step");
                assignedUserId = LiveWriteFormBinder.Int(form, "assignedUserId", "assigned_user_id");
                dueAt = LiveWriteFormBinder.Text(form, "dueAt", "due_at", "due");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.CreateAsync(
                    title, departmentCode, priority, orderId, description, workflowStep, assignedUserId, dueAt, session.UserId, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/erp/workflow-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new ErpWorkflowCreateRequest(title, departmentCode, priority, orderId, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ErpMarketingCreate, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpMarketingCreateDryRun dryRun,
            EcomAE.Platform.Erp.IErpMarketingWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/marketing-app", "Admin ERP capability required for marketing create.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpMarketingCreateBody>(context, cancellationToken)
                       ?? new();
            var name = body.Name;
            var channel = body.Channel;
            var budget = body.Budget;
            var status = body.Status;
            var timeStart = body.TimeStart;
            var timeEnd = body.TimeEnd;
            var notes = body.Notes;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                name = LiveWriteFormBinder.Text(form, "name");
                channel = LiveWriteFormBinder.Text(form, "channel");
                budget = LiveWriteFormBinder.Dec(form, "budget");
                status = LiveWriteFormBinder.Text(form, "status");
                timeStart = LiveWriteFormBinder.Text(form, "time_start", "timeStart");
                timeEnd = LiveWriteFormBinder.Text(form, "time_end", "timeEnd");
                notes = LiveWriteFormBinder.Text(form, "notes");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.CreateAsync(name, channel, budget, status, timeStart, timeEnd, notes, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/erp/marketing-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new ErpMarketingCreateRequest(name, false, channel, budget, status, timeStart, timeEnd, notes)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ErpSubscriptionsSave, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpSubscriptionSaveDryRun dryRun,
            EcomAE.Platform.Erp.IErpSubscriptionSaveWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/sales-orders-app?tab=subscriptions", "Admin ERP capability required for subscription save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpSubscriptionSaveBody>(context, cancellationToken)
                       ?? new();
            var code = body.Code;
            var customer = body.Customer;
            var planName = body.PlanName;
            var amount = body.Amount;
            var currency = body.Currency;
            var cycle = body.Cycle;
            var termMonths = body.TermMonths;
            var startDate = body.StartDate;
            var id = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                code = LiveWriteFormBinder.Text(form, "code");
                customer = LiveWriteFormBinder.Text(form, "customer");
                planName = LiveWriteFormBinder.Text(form, "planName", "plan_name", "plan");
                amount = LiveWriteFormBinder.Dec(form, "amount");
                currency = LiveWriteFormBinder.Text(form, "currency");
                cycle = LiveWriteFormBinder.Text(form, "cycle");
                termMonths = LiveWriteFormBinder.Int(form, "termMonths", "term_months");
                startDate = LiveWriteFormBinder.Text(form, "startDate", "start_date", "start_date_str", "start");
                id = LiveWriteFormBinder.Long(form, "id", "subscriptionId", "subscription_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SaveAsync(
                    code, customer, planName, amount, currency, cycle, termMonths, startDate, id, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/erp/sales-orders-app?tab=subscriptions",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new ErpSubscriptionSaveRequest(code, customer, id, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ErpContractsSave, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpContractSaveDryRun dryRun,
            EcomAE.Platform.Erp.IErpContractSaveWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/contracts-app", "Admin ERP capability required for contract save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpContractSaveBody>(context, cancellationToken)
                       ?? new();
            var code = body.Code;
            var title = body.Title;
            var counterparty = body.Counterparty;
            var contractValue = body.ContractValue;
            var currency = body.Currency;
            var startDate = body.StartDate;
            var endDate = body.EndDate;
            var bodyText = body.BodyText;
            var id = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                code = LiveWriteFormBinder.Text(form, "code");
                title = LiveWriteFormBinder.Text(form, "title");
                counterparty = LiveWriteFormBinder.Text(form, "counterparty");
                contractValue = LiveWriteFormBinder.Dec(form, "contractValue", "contract_value", "value");
                currency = LiveWriteFormBinder.Text(form, "currency");
                startDate = LiveWriteFormBinder.Text(form, "startDate", "start_date", "start_date_str", "start");
                endDate = LiveWriteFormBinder.Text(form, "endDate", "end_date", "end_date_str", "end");
                bodyText = LiveWriteFormBinder.Text(form, "bodyText", "body_text", "body");
                id = LiveWriteFormBinder.Long(form, "id", "contractId", "contract_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SaveAsync(
                    code, title, counterparty, contractValue, currency, startDate, endDate, bodyText, id, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/erp/contracts-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new ErpContractSaveRequest(code, title, id, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ErpWmsReceive, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpWmsReceiveDryRun dryRun,
            IErpWmsReceiveWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/warehouse-wms-app", "Admin ERP capability required for WMS receive.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpWmsReceiveBody>(context, cancellationToken) ?? new(null, 0, 0, 0, false);
            var item = body.Item;
            var qty = body.Qty;
            var receiveLocationId = body.ReceiveLocationId;
            var putawayLocationId = body.PutawayLocationId;
            var reference = body.Reference;
            var lpCode = body.LpCode;
            var companyId = body.CompanyId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                item = LiveWriteFormBinder.Text(form, "item");
                qty = LiveWriteFormBinder.Dec(form, "qty");
                receiveLocationId = LiveWriteFormBinder.Long(form, "receive_location_id", "receiveLocationId");
                putawayLocationId = LiveWriteFormBinder.Long(form, "putaway_location_id", "putawayLocationId");
                reference = LiveWriteFormBinder.Text(form, "reference");
                lpCode = LiveWriteFormBinder.Text(form, "lp_code", "lpCode");
                companyId = LiveWriteFormBinder.Long(form, "company_id", "companyId", "company");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpWmsReceiveRequest(item, qty, receiveLocationId, putawayLocationId, false, reference, lpCode, companyId)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.ReceiveAsync(item, qty, receiveLocationId, putawayLocationId, reference, lpCode, companyId, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/warehouse-wms-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ErpWmsLocationSave, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpWmsLocationSaveDryRun dryRun,
            EcomAE.Platform.Erp.IErpWmsLocationWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/warehouse-wms-app", "Admin ERP capability required for WMS location save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpWmsLocationSaveBody>(context, cancellationToken)
                       ?? new();
            var code = body.Code;
            var warehouse = body.Warehouse;
            var zone = body.Zone;
            var type = body.Type;
            var capacity = body.Capacity;
            var active = body.Active;
            var companyId = body.CompanyId;
            var id = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                code = LiveWriteFormBinder.Text(form, "code");
                warehouse = LiveWriteFormBinder.Text(form, "warehouse");
                zone = LiveWriteFormBinder.Text(form, "zone");
                type = LiveWriteFormBinder.Text(form, "type");
                capacity = LiveWriteFormBinder.Int(form, "capacity");
                active = form.ContainsKey("active") || form.ContainsKey("is_active")
                    ? LiveWriteFormBinder.Int(form, "active", "is_active")
                    : 1;
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id");
                id = LiveWriteFormBinder.Long(form, "id", "locationId", "location_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SaveAsync(
                    code, warehouse, zone, type, capacity, active, companyId, id, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/warehouse-wms-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new ErpWmsLocationSaveRequest(code, id, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ErpCollectionsCaseSave, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpCollectionsCaseSaveDryRun dryRun,
            EcomAE.Platform.Erp.IErpCollectionsCaseSaveWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/collections-dunning-app", "Admin ERP capability required for collections case save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpCollectionsCaseSaveBody>(context, cancellationToken)
                       ?? new();
            var customerId = body.CustomerId;
            var status = body.Status;
            var balance = body.Balance;
            var promiseAmount = body.PromiseAmount;
            var promiseDate = body.PromiseDate;
            var assignedTo = body.AssignedTo;
            var notes = body.Notes;
            var companyId = body.CompanyId;
            var id = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                customerId = LiveWriteFormBinder.Long(form, "customerId", "customer_id");
                status = LiveWriteFormBinder.Text(form, "status");
                balance = LiveWriteFormBinder.Dec(form, "balance");
                promiseAmount = LiveWriteFormBinder.Dec(form, "promiseAmount", "promise_amount");
                promiseDate = LiveWriteFormBinder.Text(form, "promiseDate", "promise_date");
                assignedTo = LiveWriteFormBinder.Text(form, "assignedTo", "assigned_to");
                notes = LiveWriteFormBinder.Text(form, "notes");
                companyId = LiveWriteFormBinder.Long(form, "companyId", "company_id");
                id = LiveWriteFormBinder.Long(form, "id", "caseId", "case_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SaveAsync(
                    customerId, status, balance, promiseAmount, promiseDate, assignedTo, notes, companyId, id, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/collections-dunning-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new ErpCollectionsCaseSaveRequest(customerId, id, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ErpCollectionsCasePromise, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpCollCasePromiseDryRun dryRun,
            IErpCollectionsCasePromiseWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/collections-dunning-app", "Admin ERP capability required for collections case promise.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpCollCasePromiseBody>(context, cancellationToken) ?? new();
            var id = body.Id;
            var amount = body.Amount;
            var promiseDate = body.PromiseDate;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "caseId", "case_id");
                amount = LiveWriteFormBinder.Dec(form, "amount", "promiseAmount", "promise_amount");
                promiseDate = LiveWriteFormBinder.Text(form, "promise_date", "promiseDate");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpCollCasePromiseRequest(id, false, amount, promiseDate)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.PromiseAsync(
                new ErpCollectionsCasePromiseWriteRequest(id, amount, promiseDate),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/collections-dunning-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ErpCollectionsActivityLog, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpCollActivityLogDryRun dryRun,
            IErpCollectionsActivityLogWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/collections-dunning-app", "Admin ERP capability required for collections activity log.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpCollActivityLogBody>(context, cancellationToken) ?? new();
            var id = body.Id;
            var type = body.Type;
            var outcome = body.Outcome;
            var amount = body.Amount;
            var followUpDate = body.FollowUpDate;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "caseId", "case_id");
                type = LiveWriteFormBinder.Text(form, "type");
                outcome = LiveWriteFormBinder.Text(form, "outcome");
                amount = LiveWriteFormBinder.Dec(form, "amount");
                followUpDate = LiveWriteFormBinder.Text(form, "follow_up_date", "followUpDate", "follow_up");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpCollActivityLogRequest(id, false, type, outcome, amount, followUpDate)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.LogAsync(
                new ErpCollectionsActivityLogWriteRequest(id, type, outcome, amount, followUpDate),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/collections-dunning-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ErpCollectionsHoldSet, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpCollHoldSetDryRun dryRun,
            IErpCollectionsHoldSetWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/collections-dunning-app", "Admin ERP capability required for collections hold.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpCollHoldSetBody>(context, cancellationToken) ?? new();
            var customerId = body.CustomerId;
            var place = body.Place;
            var reason = body.Reason;
            var actor = body.Actor;
            var companyId = body.CompanyId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                customerId = LiveWriteFormBinder.Long(form, "customerId", "customer_id");
                place = form.ContainsKey("place") || form.ContainsKey("on_hold")
                    ? LiveWriteFormBinder.Flag(form, "place", "on_hold")
                    : true;
                reason = LiveWriteFormBinder.Text(form, "reason");
                actor = LiveWriteFormBinder.Text(form, "actor", "by");
                companyId = LiveWriteFormBinder.Long(form, "companyId", "company_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (string.IsNullOrWhiteSpace(actor))
            {
                actor = session.Email;
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpCollHoldSetRequest(customerId, false, place, reason, actor, companyId)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.SetHoldAsync(
                new ErpCollectionsHoldSetWriteRequest(customerId, place, reason, actor, companyId),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/collections-dunning-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ErpCollectionsDunningRun, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpCollDunningRunDryRun dryRun,
            IErpCollectionsDunningRunWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/collections-dunning-app", "Admin ERP capability required for collections dunning run.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpCollDunningRunBody>(context, cancellationToken) ?? new();
            var customers = body.Customers;
            var companyId = body.CompanyId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                customers = LiveWriteFormBinder.Text(form, "customers");
                companyId = LiveWriteFormBinder.Long(form, "companyId", "company_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpCollDunningRunRequest(false, customers, companyId)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.RunAsync(
                new ErpCollectionsDunningRunWriteRequest(customers, companyId),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/collections-dunning-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ErpProcurementReqSave, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpProcReqSaveDryRun dryRun,
            EcomAE.Platform.Erp.IErpProcurementReqSaveWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/purchase-requests-app", "Admin ERP capability required for procurement req save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpProcReqSaveBody>(context, cancellationToken)
                       ?? new();
            var requester = body.Requester;
            var businessUnitId = body.BusinessUnitId;
            var justification = body.Justification;
            var reqNumber = body.ReqNumber;
            var companyId = body.CompanyId;
            var id = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                requester = LiveWriteFormBinder.Text(form, "requester");
                businessUnitId = LiveWriteFormBinder.Long(form, "businessUnitId", "business_unit_id");
                justification = LiveWriteFormBinder.Text(form, "justification");
                reqNumber = LiveWriteFormBinder.Text(form, "reqNumber", "req_number");
                companyId = LiveWriteFormBinder.Long(form, "companyId", "company_id");
                id = LiveWriteFormBinder.Long(form, "id", "reqId", "req_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SaveAsync(
                    requester, businessUnitId, justification, reqNumber, companyId, id, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/purchase-requests-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new ErpProcReqSaveRequest(requester, id, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ErpProcurementReqAddLine, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpProcReqAddLineDryRun dryRun,
            IErpProcurementReqAddLineWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/purchase-requests-app", "Admin ERP capability required for procurement req add-line.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpProcReqAddLineBody>(context, cancellationToken) ?? new();
            var id = body.Id;
            var categoryId = body.CategoryId;
            var itemCode = body.ItemCode;
            var description = body.Description;
            var qty = body.Qty;
            var unitPrice = body.UnitPrice;
            var preferredVendor = body.PreferredVendor;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "reqId", "req_id");
                categoryId = LiveWriteFormBinder.Long(form, "category_id", "categoryId");
                itemCode = LiveWriteFormBinder.Text(form, "item_code", "itemCode");
                description = LiveWriteFormBinder.Text(form, "description");
                qty = LiveWriteFormBinder.Dec(form, "qty");
                unitPrice = LiveWriteFormBinder.Dec(form, "unit_price", "unitPrice");
                preferredVendor = LiveWriteFormBinder.Text(form, "preferred_vendor", "preferredVendor");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpProcReqAddLineRequest(id, false, categoryId, itemCode, description, qty, unitPrice, preferredVendor)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.AddLineAsync(
                new ErpProcurementReqAddLineWriteRequest(id, categoryId, itemCode, description, qty, unitPrice, preferredVendor),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/purchase-requests-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ErpFinPeriodStatus, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpFinPeriodStatusDryRun dryRun,
            IErpFinPeriodStatusWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/fin-advanced-app", "Admin ERP capability required for fin period status.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpFinPeriodStatusBody>(context, cancellationToken) ?? new(0, 0, "open", false);
            var fy = body.Fy;
            var periodNo = body.PeriodNo;
            var status = body.Status;
            var companyId = body.CompanyId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                fy = LiveWriteFormBinder.Int(form, "fy");
                periodNo = LiveWriteFormBinder.Int(form, "period_no", "periodNo");
                status = LiveWriteFormBinder.Text(form, "status");
                companyId = LiveWriteFormBinder.Long(form, "company_id", "companyId", "company");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpFinPeriodStatusRequest(fy, periodNo, status, false, companyId)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.SetStatusAsync(companyId, fy, periodNo, status, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/fin-advanced-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ErpWmsWaveCreate, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpWmsWaveCreateDryRun dryRun,
            EcomAE.Platform.Erp.IErpWmsWaveCreateWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/warehouse-wms-app", "Admin ERP capability required for WMS wave create.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpWmsWaveCreateBody>(context, cancellationToken)
                       ?? new();
            var item = body.Item;
            var qty = body.Qty;
            var reference = body.Reference;
            var companyId = body.CompanyId;
            var fromLocationId = body.FromLocationId;
            var toLocationId = body.ToLocationId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                item = LiveWriteFormBinder.Text(form, "item");
                qty = LiveWriteFormBinder.Dec(form, "qty");
                reference = LiveWriteFormBinder.Text(form, "reference");
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                fromLocationId = LiveWriteFormBinder.Long(form, "fromLocationId", "from_location_id");
                toLocationId = LiveWriteFormBinder.Long(form, "toLocationId", "to_location_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.CreateWithPickAsync(item, qty, reference, companyId, fromLocationId, toLocationId, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/warehouse-wms-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new ErpWmsWaveCreateRequest(item, qty, reference, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpWmsWaveRelease, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpWmsWaveReleaseDryRun dryRun,
            IErpWmsWaveReleaseWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/warehouse-wms-app", "Admin ERP capability required for WMS wave release.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpWmsWaveReleaseBody>(context, cancellationToken) ?? new(0, false);
            var id = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "waveId", "wave_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpWmsWaveReleaseRequest(id, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.ReleaseAsync(id, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/warehouse-wms-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpWmsWorkComplete, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpWmsWorkCompleteDryRun dryRun,
            IErpWmsWorkCompleteWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/warehouse-wms-app", "Admin ERP capability required for WMS work complete.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpWmsWorkCompleteBody>(context, cancellationToken) ?? new(0, false);
            var id = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "workId", "work_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpWmsWorkCompleteRequest(id, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.CompleteAsync(id, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/warehouse-wms-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpSubscriptionsStatus, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpSubscriptionStatusDryRun dryRun,
            IErpSubscriptionStatusWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/sales-orders-app?tab=subscriptions", "Admin ERP capability required for subscription status.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpSubscriptionStatusBody>(context, cancellationToken)
                       ?? new(0, "active", false);
            var id = body.Id;
            var status = body.Status;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "subscriptionId", "subscription_id");
                status = LiveWriteFormBinder.Text(form, "status");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpSubscriptionStatusRequest(id, status, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.SetStatusAsync(id, status, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/sales-orders-app?tab=subscriptions",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpCollectionsCaseStatus, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpCollectionsCaseStatusDryRun dryRun,
            IErpCollectionsCaseStatusWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/collections-dunning-app", "Admin ERP capability required for collections case status.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpCollectionsCaseStatusBody>(context, cancellationToken)
                       ?? new(0, "new", false);
            var id = body.Id;
            var status = body.Status;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "caseId", "case_id");
                status = LiveWriteFormBinder.Text(form, "status");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpCollectionsCaseStatusRequest(id, status, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.SetStatusAsync(id, status, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/collections-dunning-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpProcurementReqSubmit, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpProcReqSubmitDryRun dryRun,
            IErpProcurementReqWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/purchase-requests-app", "Admin ERP capability required for procurement submit.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpProcReqSubmitBody>(context, cancellationToken) ?? new(0, false);
            var id = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "reqId", "req_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpProcReqSubmitRequest(id, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.SubmitAsync(id, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/purchase-requests-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpProcurementReqDecision, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpProcReqDecisionDryRun dryRun,
            IErpProcurementReqWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/purchase-requests-app", "Admin ERP capability required for procurement decision.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpProcReqDecisionBody>(context, cancellationToken)
                       ?? new(0, true, null, false);
            var id = body.Id;
            var approve = body.Approve;
            var note = body.Note;
            var by = session.Email ?? string.Empty;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "reqId", "req_id");
                approve = LiveWriteFormBinder.Flag(form, "approve");
                note = LiveWriteFormBinder.Text(form, "note", "decision_note", "decisionNote");
                var formBy = LiveWriteFormBinder.Text(form, "by", "decidedBy", "decided_by");
                if (!string.IsNullOrWhiteSpace(formBy))
                {
                    by = formBy;
                }

                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpProcReqDecisionRequest(id, approve, note, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.DecideAsync(id, approve, by, note, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/purchase-requests-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpWmsLocationDelete, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpWmsLocationDeleteDryRun dryRun,
            IErpWmsLocationWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/warehouse-wms-app", "Admin ERP capability required for WMS location delete.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpWmsLocationDeleteBody>(context, cancellationToken) ?? new(0, false);
            var id = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "locationId", "location_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpWmsLocationDeleteRequest(id, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.DeleteAsync(id, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/warehouse-wms-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpOfficesCashAdd, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpOfficesCashWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/cash-accounts-app", "Admin ERP capability required for office cash.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpOfficesCashAddBody>(context, cancellationToken) ?? new();
            var officeId = body.OfficeId;
            var income = body.Income;
            var amount = body.Amount;
            var codeId = body.OperationCodeId;
            var comment = body.Comment;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                officeId = LiveWriteFormBinder.Long(form, "officeId", "office_id");
                income = LiveWriteFormBinder.Int(form, "income");
                amount = LiveWriteFormBinder.Dec(form, "amount");
                codeId = LiveWriteFormBinder.Long(form, "name", "operationCodeId", "operation_code", "codeId", "code_id");
                comment = LiveWriteFormBinder.Text(form, "comment");
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
                    message = "Set confirmWrites=true to add an office cash entry on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.AddEntryAsync(session.UserId, officeId, income, amount, codeId, comment, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/cash-accounts-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpOfficesCashCodeAdd, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpOfficesCashWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/cash-accounts-app", "Admin ERP capability required for office cash codes.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpOfficesCashCodeAddBody>(context, cancellationToken) ?? new();
            var officeId = body.OfficeId;
            var income = body.Income;
            var name = body.Name;
            var langStrId = body.NameLangStrId ?? body.LangStrId;
            var langCode = body.LangCode;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                officeId = LiveWriteFormBinder.Long(form, "officeId", "office_id");
                income = LiveWriteFormBinder.Int(form, "income");
                name = LiveWriteFormBinder.Text(form, "name", "caption");
                langStrId = LiveWriteFormBinder.Text(form, "nameLangStrId", "name_lang_str_id", "langStrId", "lang_str_id");
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
                    message = "Set confirmWrites=true to add an office cash code on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var host = context.Request.Host.Host;
            var domainPath = string.IsNullOrWhiteSpace(host) ? "http://localhost/" : "http://" + host + "/";
            var written = await writes.AddCodeAsync(session.UserId, officeId, income, name, langStrId, langCode, domainPath, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/cash-accounts-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpOfficesCashCodeDelete, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpOfficesCashWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/cash-accounts-app", "Admin ERP capability required for office cash codes.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpOfficesCashCodeDeleteBody>(context, cancellationToken) ?? new();
            var officeId = body.OfficeId;
            var codeId = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                officeId = LiveWriteFormBinder.Long(form, "officeId", "office_id");
                codeId = LiveWriteFormBinder.Long(form, "id", "codeId", "code_id");
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
                    message = "Set confirmWrites=true to delete an office cash code on ASP.NET.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.DeleteCodeAsync(session.UserId, officeId, codeId, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/cash-accounts-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.ErpAjaxEditLockAcquire, async (HttpContext context, ErpEditLockAcquireBody? body, ILegacySessionValidator validator, IErpEditLockAcquireDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(null,false); return Results.Ok(dryRun.Evaluate(new ErpEditLockAcquireRequest(body.ResourceKey, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxEditLockHeartbeat, async (HttpContext context, ErpEditLockHeartbeatBody? body, ILegacySessionValidator validator, IErpEditLockHeartbeatDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(null,false); return Results.Ok(dryRun.Evaluate(new ErpEditLockHeartbeatRequest(body.ResourceKey, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxEditLockRelease, async (HttpContext context, ErpEditLockReleaseBody? body, ILegacySessionValidator validator, IErpEditLockReleaseDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(null,false); return Results.Ok(dryRun.Evaluate(new ErpEditLockReleaseRequest(body.ResourceKey, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPresenceHeartbeat, async (HttpContext context, ErpPresenceHeartbeatBody? body, ILegacySessionValidator validator, IErpPresenceHeartbeatDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(null,false); return Results.Ok(dryRun.Evaluate(new ErpPresenceHeartbeatRequest(body.ResourceKey, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBosComplianceAddObligation, async (HttpContext context, ErpBosComplianceAddObligationBody? body, ILegacySessionValidator validator, IErpBosComplianceAddObligationDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpBosComplianceAddObligationRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBosComplianceDisableObligation, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpBosComplianceDisableObligationDryRun dryRun,
            IErpBosComplianceDisableObligationWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/soc2-compliance-app", "Admin ERP capability required for compliance disable obligation.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpBosComplianceDisableObligationBody>(context, cancellationToken) ?? new(0, null, false);
            var id = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "obligationId", "obligation_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpBosComplianceDisableObligationRequest(id, body.Code, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.DisableAsync(id, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/soc2-compliance-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBosComplianceFile, async (HttpContext context, ErpBosComplianceFileBody? body, ILegacySessionValidator validator, IErpBosComplianceFileDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpBosComplianceFileRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBosComplianceSaveRetention, async (HttpContext context, ErpBosComplianceSaveRetentionBody? body, ILegacySessionValidator validator, IErpBosComplianceSaveRetentionDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpBosComplianceSaveRetentionRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBosWfSaveRule, async (HttpContext context, ErpBosWfSaveRuleBody? body, ILegacySessionValidator validator, IErpBosWfSaveRuleDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpBosWfSaveRuleRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBosWfDisableRule, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpBosWfDisableRuleDryRun dryRun,
            IErpBosWfDisableRuleWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/approvals-app", "Admin ERP capability required for workflow disable rule.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpBosWfDisableRuleBody>(context, cancellationToken) ?? new(0, null, false);
            var id = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "ruleId", "rule_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpBosWfDisableRuleRequest(id, body.Code, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.DisableAsync(id, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/approvals-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBosWfDecide, async (HttpContext context, ErpBosWfDecideBody? body, ILegacySessionValidator validator, IErpBosWfDecideDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,true,null,false); return Results.Ok(dryRun.Evaluate(new ErpBosWfDecideRequest(body.Id, body.Approve, body.Note, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBosWfRaiseTest, async (HttpContext context, ErpBosWfRaiseTestBody? body, ILegacySessionValidator validator, IErpBosWfRaiseTestDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpBosWfRaiseTestRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBosIntelToggleControl, async (HttpContext context, ErpBosIntelToggleControlBody? body, ILegacySessionValidator validator, IErpBosIntelToggleControlDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(null,true,false); return Results.Ok(dryRun.Evaluate(new ErpBosIntelToggleControlRequest(body.ControlKey, body.Enabled, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBosVatRefundSave, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpBosVatRefundSaveDryRun dryRun,
            IErpBosVatRefundSaveWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/vat-app?tab=vat_refund", "Admin ERP capability required for VAT refund save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpBosVatRefundSaveBody>(context, cancellationToken) ?? new();
            var id = body.Id;
            var tagRef = body.TagRef;
            var invoiceRef = body.InvoiceRef;
            var customerName = body.CustomerName;
            var passportNo = body.PassportNo;
            var nationality = body.Nationality;
            var saleAmount = body.SaleAmount;
            var vatAmount = body.VatAmount;
            var saleDate = body.SaleDate;
            var status = body.Status;
            var notes = body.Notes;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id");
                tagRef = LiveWriteFormBinder.Text(form, "tag_ref", "tagRef", "code");
                invoiceRef = LiveWriteFormBinder.Text(form, "invoice_ref", "invoiceRef");
                customerName = LiveWriteFormBinder.Text(form, "customer_name", "customerName");
                passportNo = LiveWriteFormBinder.Text(form, "passport_no", "passportNo");
                nationality = LiveWriteFormBinder.Text(form, "nationality");
                saleAmount = LiveWriteFormBinder.Dec(form, "sale_amount", "saleAmount");
                vatAmount = LiveWriteFormBinder.DecOrNull(form, "vat_amount", "vatAmount");
                saleDate = LiveWriteFormBinder.Text(form, "sale_date", "saleDate");
                status = LiveWriteFormBinder.Text(form, "status");
                notes = LiveWriteFormBinder.Text(form, "notes");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpBosVatRefundSaveRequest(id, invoiceRef, saleAmount, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveAsync(
                id,
                tagRef,
                invoiceRef,
                customerName,
                passportNo,
                nationality,
                saleAmount,
                vatAmount,
                saleDate,
                status,
                notes,
                session.UserId,
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/vat-app?tab=vat_refund",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBosVatRefundStatus, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpBosVatRefundStatusDryRun dryRun,
            IErpBosVatRefundStatusWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/vat-app?tab=vat_refund", "Admin ERP capability required for VAT refund status.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpBosVatRefundStatusBody>(context, cancellationToken) ?? new(0, null, false);
            var id = body.Id;
            var status = body.TargetStatus;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "refundId", "refund_id");
                status = LiveWriteFormBinder.Text(form, "status", "targetStatus", "target_status");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpBosVatRefundStatusRequest(id, status, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.SetStatusAsync(id, status, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/vat-app?tab=vat_refund",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOplParamsSave, async (HttpContext context, ErpOplParamsSaveBody? body, ILegacySessionValidator validator, IErpOplParamsSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpOplParamsSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOplSetStatus, async (HttpContext context, ErpOplSetStatusBody? body, ILegacySessionValidator validator, IErpOplSetStatusDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpOplSetStatusRequest(body.Id, body.TargetStatus, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOplConfirmAll, async (HttpContext context, ErpOplConfirmAllBody? body, ILegacySessionValidator validator, IErpOplConfirmAllDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpOplConfirmAllRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOplCreatePos, async (HttpContext context, ErpOplCreatePosBody? body, ILegacySessionValidator validator, IErpOplCreatePosDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpOplCreatePosRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPfProcessSave, async (HttpContext context, ErpPfProcessSaveBody? body, ILegacySessionValidator validator, IErpPfProcessSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPfProcessSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPfStepSave, async (HttpContext context, ErpPfStepSaveBody? body, ILegacySessionValidator validator, IErpPfStepSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPfStepSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPfStepDelete, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpPfStepDeleteDryRun dryRun,
            IErpPfStepDeleteWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/process-flow-tasks-app", "Admin ERP capability required for process-flow step delete.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpPfStepDeleteBody>(context, cancellationToken) ?? new(0, false);
            var id = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "step_id", "stepId", "id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpPfStepDeleteRequest(id, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.DeleteAsync(id, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/process-flow-tasks-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPfCaseStart, async (HttpContext context, ErpPfCaseStartBody? body, ILegacySessionValidator validator, IErpPfCaseStartDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpPfCaseStartRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPfCaseAct, async (HttpContext context, ErpPfCaseActBody? body, ILegacySessionValidator validator, IErpPfCaseActDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpPfCaseActRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxSubGenerate, async (HttpContext context, ErpSubGenerateBody? body, ILegacySessionValidator validator, IErpSubGenerateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpSubGenerateRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxSubInvoicePaid, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpSubInvoicePaidDryRun dryRun,
            IErpSubInvoicePaidWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/sales-orders-app?tab=subscriptions", "Admin ERP capability required for subscription invoice paid.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpSubInvoicePaidBody>(context, cancellationToken) ?? new(0, false);
            var id = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "invoiceId", "invoice_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpSubInvoicePaidRequest(id, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.MarkPaidAsync(id, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/sales-orders-app?tab=subscriptions",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCtrStatus, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpCtrStatusDryRun dryRun,
            IErpContractStatusWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/contracts-app", "Admin ERP capability required for contract status.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpCtrStatusBody>(context, cancellationToken)
                       ?? new(0, null, false);
            var id = body.Id;
            var status = body.TargetStatus;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "contractId", "contract_id");
                status = LiveWriteFormBinder.Text(form, "status", "targetStatus", "target_status");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpCtrStatusRequest(id, status, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.SetStatusAsync(id, status, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/contracts-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCtrSign, async (HttpContext context, ErpCtrSignBody? body, ILegacySessionValidator validator, IErpCtrSignDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpCtrSignRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCollCasePromise, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpCollCasePromiseDryRun dryRun,
            IErpCollectionsCasePromiseWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/collections-dunning-app", "Admin ERP capability required for collections case promise.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpCollCasePromiseBody>(context, cancellationToken) ?? new();
            var id = body.Id;
            var amount = body.Amount;
            var promiseDate = body.PromiseDate;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "caseId", "case_id");
                amount = LiveWriteFormBinder.Dec(form, "amount", "promiseAmount", "promise_amount");
                promiseDate = LiveWriteFormBinder.Text(form, "promise_date", "promiseDate");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpCollCasePromiseRequest(id, false, amount, promiseDate)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.PromiseAsync(
                new ErpCollectionsCasePromiseWriteRequest(id, amount, promiseDate),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/collections-dunning-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCollActivityLog, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpCollActivityLogDryRun dryRun,
            IErpCollectionsActivityLogWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/collections-dunning-app", "Admin ERP capability required for collections activity log.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpCollActivityLogBody>(context, cancellationToken) ?? new();
            var id = body.Id;
            var type = body.Type;
            var outcome = body.Outcome;
            var amount = body.Amount;
            var followUpDate = body.FollowUpDate;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "caseId", "case_id");
                type = LiveWriteFormBinder.Text(form, "type");
                outcome = LiveWriteFormBinder.Text(form, "outcome");
                amount = LiveWriteFormBinder.Dec(form, "amount");
                followUpDate = LiveWriteFormBinder.Text(form, "follow_up_date", "followUpDate", "follow_up");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpCollActivityLogRequest(id, false, type, outcome, amount, followUpDate)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.LogAsync(
                new ErpCollectionsActivityLogWriteRequest(id, type, outcome, amount, followUpDate),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/collections-dunning-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCollDunningRun, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpCollDunningRunDryRun dryRun,
            IErpCollectionsDunningRunWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/collections-dunning-app", "Admin ERP capability required for collections dunning run.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpCollDunningRunBody>(context, cancellationToken) ?? new();
            var customers = body.Customers;
            var companyId = body.CompanyId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                customers = LiveWriteFormBinder.Text(form, "customers");
                companyId = LiveWriteFormBinder.Long(form, "companyId", "company_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpCollDunningRunRequest(false, customers, companyId)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.RunAsync(
                new ErpCollectionsDunningRunWriteRequest(customers, companyId),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/collections-dunning-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxProcCategorySave, async (HttpContext context, ErpProcCategorySaveBody? body, ILegacySessionValidator validator, IErpProcCategorySaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpProcCategorySaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxProcPolicySave, async (HttpContext context, ErpProcPolicySaveBody? body, ILegacySessionValidator validator, IErpProcPolicySaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpProcPolicySaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxProcReqAddLine, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpProcReqAddLineDryRun dryRun,
            IErpProcurementReqAddLineWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/purchase-requests-app", "Admin ERP capability required for procurement req add-line.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpProcReqAddLineBody>(context, cancellationToken) ?? new();
            var id = body.Id;
            var categoryId = body.CategoryId;
            var itemCode = body.ItemCode;
            var description = body.Description;
            var qty = body.Qty;
            var unitPrice = body.UnitPrice;
            var preferredVendor = body.PreferredVendor;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "reqId", "req_id");
                categoryId = LiveWriteFormBinder.Long(form, "category_id", "categoryId");
                itemCode = LiveWriteFormBinder.Text(form, "item_code", "itemCode");
                description = LiveWriteFormBinder.Text(form, "description");
                qty = LiveWriteFormBinder.Dec(form, "qty");
                unitPrice = LiveWriteFormBinder.Dec(form, "unit_price", "unitPrice");
                preferredVendor = LiveWriteFormBinder.Text(form, "preferred_vendor", "preferredVendor");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpProcReqAddLineRequest(id, false, categoryId, itemCode, description, qty, unitPrice, preferredVendor)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.AddLineAsync(
                new ErpProcurementReqAddLineWriteRequest(id, categoryId, itemCode, description, qty, unitPrice, preferredVendor),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/purchase-requests-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxProcReqConvert, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpProcReqConvertDryRun dryRun,
            IErpProcurementReqWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/purchase-requests-app", "Admin ERP capability required for procurement convert.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpProcReqConvertBody>(context, cancellationToken) ?? new(0, false);
            var id = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "reqId", "req_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpProcReqConvertRequest(id, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.ConvertAsync(id, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/purchase-requests-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBplanSave, async (HttpContext context, ErpBplanSaveBody? body, ILegacySessionValidator validator, IErpBplanSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpBplanSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBplanAdvance, async (HttpContext context, ErpBplanAdvanceBody? body, ILegacySessionValidator validator, IErpBplanAdvanceDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpBplanAdvanceRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxAmlKycSave, async (HttpContext context, ErpAmlKycSaveBody? body, ILegacySessionValidator validator, IErpAmlKycSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpAmlKycSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxAmlAlertStatus, async (HttpContext context, ErpAmlAlertStatusBody? body, ILegacySessionValidator validator, IErpAmlAlertStatusDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpAmlAlertStatusRequest(body.Id, body.TargetStatus, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxAmlSettingsSave, async (HttpContext context, ErpAmlSettingsSaveBody? body, ILegacySessionValidator validator, IErpAmlSettingsSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpAmlSettingsSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBankImport, async (HttpContext context, ErpBankImportBody? body, ILegacySessionValidator validator, IErpBankImportDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpBankImportRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBankReconcile, async (HttpContext context, ErpBankReconcileBody? body, ILegacySessionValidator validator, IErpBankReconcileDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpBankReconcileRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxFxPostRevaluation, async (HttpContext context, ErpFxPostRevaluationBody? body, ILegacySessionValidator validator, IErpFxPostRevaluationDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpFxPostRevaluationRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        // Live write (PHP supplier_payment parity) when confirmWrites=true; otherwise the Wave B dry-run gate.
        endpoints.MapPost(EcomAeRoutes.ErpAjaxSupplierPayment, async (HttpContext context, ErpSupplierPaymentBody? body, ILegacySessionValidator validator, IErpSupplierPaymentDryRun dryRun, IErpCashWriteService writes, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required.");
            body ??= new(0, false);
            if (!body.ConfirmWrites)
                return Results.Ok(dryRun.Evaluate(new ErpSupplierPaymentRequest(body.Id, false)).ToPayload(SessionPayload(session)));

            return await ExecuteErpWriteAsync(session, async () =>
            {
                var paid = await writes.PaymentVoucherAsync(
                    new ErpPaymentVoucherInput
                    {
                        SupplierId = body.SupplierId > 0 ? body.SupplierId : (int)body.Id,
                        AccountId = body.AccountId,
                        Amount = body.Amount,
                        PurchaseId = body.PurchaseId,
                        Reference = body.Reference ?? string.Empty,
                        Note = body.Note ?? string.Empty,
                        Time = body.Time,
                    },
                    session.UserId,
                    cancellationToken);
                return ("Supplier payment " + paid.VoucherNo + " posted", new
                {
                    cash_entry_id = paid.CashEntryId,
                    voucher_no = paid.VoucherNo,
                    gl_journal_id = paid.GlJournalId,
                });
            });
        });

        endpoints.MapPost(EcomAeRoutes.ErpAjaxInvSyncWarehouses, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpInvSyncWarehousesDryRun dryRun,
            IErpInventoryMovementWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/inventory-stock-app", "Admin ERP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpInvSyncWarehousesBody>(context, cancellationToken) ?? new();
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpInvSyncWarehousesRequest(false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.SyncWarehousesAsync(cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/inventory-stock-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, created = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInvCreateWarehouse, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpInvCreateWarehouseDryRun dryRun,
            IErpInventoryMovementWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/inventory-stock-app", "Admin ERP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpInvCreateWarehouseBody>(context, cancellationToken) ?? new();
            var code = body.Code;
            var name = body.Name;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                code = LiveWriteFormBinder.Text(form, "code");
                name = LiveWriteFormBinder.Text(form, "name");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpInvCreateWarehouseRequest(body.Id, code, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.CreateWarehouseAsync(code, name, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/inventory-stock-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInvCreateItem, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpInvCreateItemDryRun dryRun,
            IErpInventoryMovementWriteService writes,
            IErpDimensionWriteService dimensions,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/inventory-stock-app", "Admin ERP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpInvCreateItemBody>(context, cancellationToken) ?? new();
            var sku = body.Sku ?? body.Code;
            var name = body.Name;
            var itemType = body.ItemType;
            var unit = body.Unit;
            var barcode = body.Barcode;
            var productId = body.ProductId;
            var trackExpiry = body.TrackExpiry;
            var reorderLevel = body.ReorderLevel;
            IReadOnlyDictionary<string, string>? customFields = body.CustomFields;
            IReadOnlyDictionary<string, long>? dim = body.Dim;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                sku = LiveWriteFormBinder.Text(form, "sku", "code");
                name = LiveWriteFormBinder.Text(form, "name");
                itemType = LiveWriteFormBinder.Text(form, "itemType", "item_type");
                unit = LiveWriteFormBinder.Text(form, "unit");
                barcode = LiveWriteFormBinder.Text(form, "barcode");
                productId = LiveWriteFormBinder.Long(form, "productId", "product_id");
                trackExpiry = LiveWriteFormBinder.Flag(form, "trackExpiry", "track_expiry");
                var rawReorder = LiveWriteFormBinder.Text(form, "reorderLevel", "reorder_level");
                reorderLevel = decimal.TryParse(rawReorder, NumberStyles.Any, CultureInfo.InvariantCulture, out var lvl) ? lvl : null;
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
                var fromForm = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var key in form.Keys)
                {
                    if (!key.StartsWith("custom_", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var fieldKey = key[7..];
                    if (fieldKey.Length == 0)
                    {
                        continue;
                    }

                    fromForm[fieldKey] = form[key].ToString();
                }

                if (fromForm.Count > 0)
                {
                    customFields = fromForm;
                }

                dim = ErpDimensionWriteService.ParseDimMap(form);
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpInvCreateItemRequest(body.Id, sku, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.CreateItemAsync(
                new ErpInventoryItemWriteRequest(sku, name, itemType, unit, barcode, productId, trackExpiry, reorderLevel, customFields),
                cancellationToken);
            if (written.Succeeded && written.Id > 0 && dim is { Count: > 0 })
            {
                await dimensions.SaveAsync("inventory_item", written.Id, dim, cancellationToken);
            }

            return LiveWriteFormBinder.Complete(
                context,
                "/erp/inventory-stock-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxDimSave, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpDimensionWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/inventory-stock-app", "Admin ERP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpDimSaveBody>(context, cancellationToken) ?? new();
            var entityType = body.EntityType;
            var entityId = body.EntityId;
            IReadOnlyDictionary<string, long>? dim = body.Dim;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                entityType = LiveWriteFormBinder.Text(form, "entityType", "entity_type");
                entityId = LiveWriteFormBinder.Long(form, "entityId", "entity_id");
                dim = ErpDimensionWriteService.ParseDimMap(form);
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    ok = true,
                    surface = "erp",
                    status = "dry-run-validated",
                    writes = 0,
                    writesBlocked = true,
                    cutoverAllowed = false,
                    phpAuthoritative = true,
                    validation_code = "ok",
                    would_write = true,
                    intended = new { action = "dim_save", entityType, entityId },
                    simulated = new[] { "epc_erp_dim_save (NOT executed)" },
                    php_ajax = "/CP/content/shop/finance/erp/ajax_erp.php",
                    session = SessionPayload(session),
                    note = "ERP dim_save payload validated; UPDATE blocked."
                });
            }

            var written = await writes.SaveAsync(entityType, entityId, dim, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/inventory-stock-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, links = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInvSetReorderLevel, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpInvSetReorderLevelDryRun dryRun,
            IErpInventoryReorderWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/inventory-stock-app", "Admin ERP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpInvSetReorderLevelBody>(context, cancellationToken) ?? new();
            var itemId = body.ItemId > 0 ? body.ItemId : body.Id;
            var level = body.ReorderLevel;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                itemId = LiveWriteFormBinder.Long(form, "itemId", "item_id", "id");
                level = LiveWriteFormBinder.Dec(form, "reorderLevel", "reorder_level", "level");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpInvSetReorderLevelRequest(itemId, body.Code, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.SetReorderLevelAsync(itemId, level, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/inventory-stock-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInvRecordMovement, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpInvRecordMovementDryRun dryRun,
            IErpInventoryMovementWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/inventory-stock-app", "Admin ERP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpInvRecordMovementBody>(context, cancellationToken) ?? new();
            var warehouseId = body.WarehouseId;
            var itemId = body.ItemId > 0 ? body.ItemId : body.Id;
            var qty = body.Qty;
            var unitCost = body.UnitCost;
            var type = body.MovementType ?? body.Code;
            var batchNo = body.BatchNo;
            var variant = body.VariantLabel;
            var expiry = body.ExpiryDate;
            var serial = body.SerialNo;
            var reference = body.Reference;
            var note = body.Note;
            var movementDate = body.MovementDate;
            var transferWh = body.TransferWarehouseId;
            var purchaseId = body.PurchaseId;
            var orderId = body.OrderId;
            var openingBatchId = body.OpeningBatchId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                warehouseId = LiveWriteFormBinder.Long(form, "warehouseId", "warehouse_id");
                itemId = LiveWriteFormBinder.Long(form, "itemId", "item_id", "id");
                qty = LiveWriteFormBinder.Dec(form, "qty");
                unitCost = LiveWriteFormBinder.Dec(form, "unitCost", "unit_cost");
                type = LiveWriteFormBinder.Text(form, "movementType", "movement_type", "type");
                batchNo = LiveWriteFormBinder.Text(form, "batchNo", "batch_no");
                variant = LiveWriteFormBinder.Text(form, "variantLabel", "variant_label");
                expiry = LiveWriteFormBinder.Text(form, "expiryDate", "expiry_date");
                serial = LiveWriteFormBinder.Text(form, "serialNo", "serial_no");
                reference = LiveWriteFormBinder.Text(form, "reference");
                note = LiveWriteFormBinder.Text(form, "note");
                movementDate = LiveWriteFormBinder.Text(form, "movementDate", "movement_date");
                transferWh = LiveWriteFormBinder.Long(form, "transferWarehouseId", "transfer_warehouse_id");
                purchaseId = LiveWriteFormBinder.Long(form, "purchaseId", "purchase_id");
                orderId = LiveWriteFormBinder.Long(form, "orderId", "order_id");
                openingBatchId = LiveWriteFormBinder.Long(form, "openingBatchId", "opening_batch_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpInvRecordMovementRequest(itemId, type, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.RecordMovementAsync(
                new ErpInventoryMovementWriteRequest(
                    session.UserId,
                    type,
                    warehouseId,
                    itemId,
                    qty,
                    unitCost,
                    batchNo,
                    variant,
                    expiry,
                    serial,
                    reference,
                    note,
                    movementDate,
                    transferWh,
                    purchaseId,
                    orderId,
                    openingBatchId),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/inventory-stock-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, movement_id = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInvScanLookup, async (HttpContext context, ErpInvScanLookupBody? body, ILegacySessionValidator validator, IErpInvScanLookupDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpInvScanLookupRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInvTransfer, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpInvTransferDryRun dryRun,
            IErpInventoryMovementWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/inventory-stock-app", "Admin ERP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpInvTransferBody>(context, cancellationToken) ?? new();
            var fromWh = body.FromWarehouseId;
            var toWh = body.ToWarehouseId;
            var itemId = body.ItemId > 0 ? body.ItemId : body.Id;
            var qty = body.Qty;
            var batchNo = body.BatchNo;
            var variant = body.VariantLabel;
            var reference = body.Reference ?? body.Code;
            var note = body.Note;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                fromWh = LiveWriteFormBinder.Long(form, "fromWarehouseId", "from_warehouse_id");
                toWh = LiveWriteFormBinder.Long(form, "toWarehouseId", "to_warehouse_id");
                itemId = LiveWriteFormBinder.Long(form, "itemId", "item_id", "id");
                qty = LiveWriteFormBinder.Dec(form, "qty");
                batchNo = LiveWriteFormBinder.Text(form, "batchNo", "batch_no");
                variant = LiveWriteFormBinder.Text(form, "variantLabel", "variant_label");
                reference = LiveWriteFormBinder.Text(form, "reference");
                note = LiveWriteFormBinder.Text(form, "note");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpInvTransferRequest(itemId, reference, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.TransferAsync(
                new ErpInventoryTransferWriteRequest(
                    session.UserId,
                    fromWh,
                    toWh,
                    itemId,
                    qty,
                    batchNo,
                    variant,
                    reference,
                    note),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/inventory-stock-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, transfer_out_id = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInvImportCsv, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpInvImportCsvDryRun dryRun,
            IErpInventoryMovementWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/inventory-stock-app", "Admin ERP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpInvImportCsvBody>(context, cancellationToken) ?? new();
            var csvText = body.CsvText;
            var warehouseId = body.WarehouseId;
            var movementType = body.DefaultMovementType;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                csvText = LiveWriteFormBinder.Text(form, "csvText", "csv_text");
                warehouseId = LiveWriteFormBinder.Long(form, "warehouseId", "warehouse_id");
                movementType = LiveWriteFormBinder.Text(form, "defaultMovementType", "default_movement_type");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpInvImportCsvRequest(false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.ImportCsvAsync(
                new ErpInventoryCsvImportRequest(session.UserId, csvText, warehouseId, movementType),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/inventory-stock-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, posted = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInvRunClosing, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpInvRunClosingDryRun dryRun,
            IErpInventoryMovementWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/inventory-stock-app", "Admin ERP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpInvRunClosingBody>(context, cancellationToken) ?? new();
            var periodEnd = body.PeriodEnd;
            var warehouseId = body.WarehouseId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                periodEnd = LiveWriteFormBinder.Text(form, "periodEnd", "period_end");
                warehouseId = LiveWriteFormBinder.Long(form, "warehouseId", "warehouse_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpInvRunClosingRequest(false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.RunClosingAsync(periodEnd, warehouseId, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/inventory-stock-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, lines = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpHrEmployeesSave, HandleHrEmpSaveAsync).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxHrEmpSave, HandleHrEmpSaveAsync).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpHrAttendanceLog, HandleHrAttendanceAsync).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxHrAttendance, HandleHrAttendanceAsync).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxHrLeaveRequest, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpHrLeaveRequestDryRun dryRun,
            EcomAE.Platform.Erp.IErpHrLeaveRequestWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/hr-overview-app", "Admin ERP capability required for leave request.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpHrLeaveRequestBody>(context, cancellationToken)
                       ?? new();
            var employeeId = body.EmployeeId;
            var type = body.Type;
            var days = body.Days;
            var dateFrom = body.DateFrom;
            var dateTo = body.DateTo;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                employeeId = LiveWriteFormBinder.Long(form, "employeeId", "employee_id");
                type = LiveWriteFormBinder.Text(form, "type");
                days = LiveWriteFormBinder.Dec(form, "days");
                dateFrom = LiveWriteFormBinder.Text(form, "dateFrom", "date_from", "date_from_str");
                dateTo = LiveWriteFormBinder.Text(form, "dateTo", "date_to", "date_to_str");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.RequestAsync(employeeId, type, days, dateFrom, dateTo, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/hr-overview-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new ErpHrLeaveRequestRequest(employeeId, type, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxHrLeaveStatus, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpHrLeaveStatusDryRun dryRun,
            IErpHrStatusWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/hr-overview-app", "Admin ERP capability required for leave status.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpHrLeaveStatusBody>(context, cancellationToken) ?? new(0, null, false);
            var id = body.Id;
            var status = body.TargetStatus;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "leaveId", "leave_id");
                status = LiveWriteFormBinder.Text(form, "status", "targetStatus", "target_status");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpHrLeaveStatusRequest(id, status, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.SetLeaveStatusAsync(id, status, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/hr-overview-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxHrExpenseSave, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpHrExpenseSaveDryRun dryRun,
            EcomAE.Platform.Erp.IErpHrExpenseSaveWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/hr-overview-app", "Admin ERP capability required for expense save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpHrExpenseSaveBody>(context, cancellationToken)
                       ?? new();
            var employeeId = body.EmployeeId;
            var title = body.Title;
            var confirm = body.ConfirmWrites;
            IFormCollection? form = null;
            if (context.Request.HasFormContentType)
            {
                form = await context.Request.ReadFormAsync(cancellationToken);
                employeeId = LiveWriteFormBinder.Long(form, "employeeId", "employee_id");
                title = LiveWriteFormBinder.Text(form, "title");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            var lines = EcomAE.Platform.Erp.ErpHrExpenseSaveWriteService.ParseLines(body.Lines, body.LinesJson, form);
            if (confirm)
            {
                var written = await writes.SaveAsync(employeeId, title, lines, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/hr-overview-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new ErpHrExpenseSaveRequest(employeeId, title, lines.Count, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxHrExpenseStatus, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpHrExpenseStatusDryRun dryRun,
            IErpHrStatusWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/hr-overview-app", "Admin ERP capability required for expense status.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpHrExpenseStatusBody>(context, cancellationToken) ?? new(0, null, false);
            var id = body.Id;
            var status = body.TargetStatus;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "expenseId", "expense_id");
                status = LiveWriteFormBinder.Text(form, "status", "targetStatus", "target_status");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpHrExpenseStatusRequest(id, status, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.SetExpenseStatusAsync(id, status, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/hr-overview-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxHrUpdateDays, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpHrUpdateDaysDryRun dryRun,
            IErpHrDaysWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/payroll-app", "Admin ERP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpHrUpdateDaysBody>(context, cancellationToken) ?? new();
            var profileId = body.StaffProfileId > 0 ? body.StaffProfileId : body.Id;
            var days = body.DaysWorked;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                profileId = LiveWriteFormBinder.Long(form, "staffProfileId", "staff_profile_id", "id");
                days = LiveWriteFormBinder.Dec(form, "daysWorked", "days_worked", "days");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpHrUpdateDaysRequest(profileId, body.Code, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.SetDaysWorkedAsync(profileId, days, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/payroll-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxEinvoiceCreate, async (HttpContext context, ErpEinvoiceCreateBody? body, ILegacySessionValidator validator, IErpEinvoiceCreateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpEinvoiceCreateRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxEinvoiceSaveSeller, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpEinvoiceSaveSellerDryRun dryRun,
            IErpEinvoiceProfileWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/einvoice-documents-app", "Admin ERP capability required for e-invoice seller save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpEinvoiceSaveSellerBody>(context, cancellationToken) ?? new();
            var sellerName = body.SellerName;
            var sellerTrn = body.SellerTrn;
            var sellerTin = body.SellerTin;
            var sellerLegalRegNo = body.SellerLegalRegNo;
            var sellerLegalRegType = body.SellerLegalRegType;
            var sellerAuthorityName = body.SellerAuthorityName;
            var sellerAddressLine1 = body.SellerAddressLine1;
            var sellerCity = body.SellerCity;
            var sellerEmirate = body.SellerEmirate;
            var sellerCountryCode = body.SellerCountryCode;
            var sellerPhone = body.SellerPhone;
            var sellerEmail = body.SellerEmail;
            var sellerBankAccount = body.SellerBankAccount;
            var paymentMeansCode = body.PaymentMeansCode;
            var paymentTerms = body.PaymentTerms;
            var vatRegistered = body.CompanyVatRegistered;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                sellerName = LiveWriteFormBinder.Text(form, "sellerName", "seller_name");
                sellerTrn = LiveWriteFormBinder.Text(form, "sellerTrn", "seller_trn");
                sellerTin = LiveWriteFormBinder.Text(form, "sellerTin", "seller_tin");
                sellerLegalRegNo = LiveWriteFormBinder.Text(form, "sellerLegalRegNo", "seller_legal_reg_no");
                sellerLegalRegType = LiveWriteFormBinder.Text(form, "sellerLegalRegType", "seller_legal_reg_type");
                sellerAuthorityName = LiveWriteFormBinder.Text(form, "sellerAuthorityName", "seller_authority_name");
                sellerAddressLine1 = LiveWriteFormBinder.Text(form, "sellerAddressLine1", "seller_address_line1");
                sellerCity = LiveWriteFormBinder.Text(form, "sellerCity", "seller_city");
                sellerEmirate = LiveWriteFormBinder.Text(form, "sellerEmirate", "seller_emirate");
                sellerCountryCode = LiveWriteFormBinder.Text(form, "sellerCountryCode", "seller_country_code");
                sellerPhone = LiveWriteFormBinder.Text(form, "sellerPhone", "seller_phone");
                sellerEmail = LiveWriteFormBinder.Text(form, "sellerEmail", "seller_email");
                sellerBankAccount = LiveWriteFormBinder.Text(form, "sellerBankAccount", "seller_bank_account");
                paymentMeansCode = LiveWriteFormBinder.Text(form, "paymentMeansCode", "payment_means_code");
                paymentTerms = LiveWriteFormBinder.Text(form, "paymentTerms", "payment_terms");
                vatRegistered = LiveWriteFormBinder.Flag(form, "companyVatRegistered", "company_vat_registered");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpEinvoiceSaveSellerRequest(0, sellerTrn, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveSellerAsync(
                new ErpEinvoiceSellerWriteRequest(
                    sellerName, sellerTrn, sellerTin, sellerLegalRegNo, sellerLegalRegType, sellerAuthorityName,
                    sellerAddressLine1, sellerCity, sellerEmirate, sellerCountryCode, sellerPhone, sellerEmail,
                    sellerBankAccount, paymentMeansCode, paymentTerms, vatRegistered),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/einvoice-documents-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxEinvoiceSaveBuyer, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpEinvoiceSaveBuyerDryRun dryRun,
            IErpEinvoiceProfileWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/einvoice-documents-app", "Admin ERP capability required for e-invoice buyer save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpEinvoiceSaveBuyerBody>(context, cancellationToken) ?? new();
            var userId = body.UserId != 0 ? body.UserId : body.Id;
            var buyerName = body.BuyerName;
            var trn = body.Trn;
            var tin = body.Tin;
            var legalRegNo = body.LegalRegNo;
            var legalRegType = body.LegalRegType;
            var authorityName = body.AuthorityName;
            var addressLine1 = body.AddressLine1;
            var city = body.City;
            var emirate = body.Emirate;
            var countryCode = body.CountryCode;
            var phone = body.Phone;
            var email = body.Email;
            var peppolEndpoint = body.PeppolEndpoint;
            var onboarded = body.BuyerOnboarded;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                userId = LiveWriteFormBinder.Long(form, "userId", "user_id", "id");
                buyerName = LiveWriteFormBinder.Text(form, "buyerName", "buyer_name");
                trn = LiveWriteFormBinder.Text(form, "trn");
                tin = LiveWriteFormBinder.Text(form, "tin");
                legalRegNo = LiveWriteFormBinder.Text(form, "legalRegNo", "legal_reg_no");
                legalRegType = LiveWriteFormBinder.Text(form, "legalRegType", "legal_reg_type");
                authorityName = LiveWriteFormBinder.Text(form, "authorityName", "authority_name");
                addressLine1 = LiveWriteFormBinder.Text(form, "addressLine1", "address_line1");
                city = LiveWriteFormBinder.Text(form, "city");
                emirate = LiveWriteFormBinder.Text(form, "emirate");
                countryCode = LiveWriteFormBinder.Text(form, "countryCode", "country_code");
                phone = LiveWriteFormBinder.Text(form, "phone");
                email = LiveWriteFormBinder.Text(form, "email");
                peppolEndpoint = LiveWriteFormBinder.Text(form, "peppolEndpoint", "peppol_endpoint");
                onboarded = LiveWriteFormBinder.Flag(form, "buyerOnboarded", "buyer_onboarded");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpEinvoiceSaveBuyerRequest(userId, null, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveBuyerAsync(
                new ErpEinvoiceBuyerWriteRequest(
                    userId, buyerName, trn, tin, legalRegNo, legalRegType, authorityName,
                    addressLine1, city, emirate, countryCode, phone, email, peppolEndpoint, onboarded),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/einvoice-documents-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, id = written.Id, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxEinvoiceSaveAsp, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpEinvoiceSaveAspDryRun dryRun,
            IErpEinvoiceProfileWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/einvoice-documents-app", "Admin ERP capability required for e-invoice ASP save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpEinvoiceSaveAspBody>(context, cancellationToken) ?? new();
            var aspName = body.AspName;
            var aspApiMode = body.AspApiMode;
            var aspApiUrl = body.AspApiUrl;
            var aspApiKey = body.AspApiKey;
            var einvoiceEnabled = body.EinvoiceEnabled;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                aspName = LiveWriteFormBinder.Text(form, "aspName", "asp_name");
                aspApiMode = LiveWriteFormBinder.Text(form, "aspApiMode", "asp_api_mode");
                aspApiUrl = LiveWriteFormBinder.Text(form, "aspApiUrl", "asp_api_url");
                aspApiKey = LiveWriteFormBinder.Text(form, "aspApiKey", "asp_api_key");
                einvoiceEnabled = LiveWriteFormBinder.Text(form, "einvoiceEnabled", "einvoice_enabled");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpEinvoiceSaveAspRequest(0, aspName, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveAspAsync(
                new ErpEinvoiceAspWriteRequest(aspName, aspApiMode, aspApiUrl, aspApiKey, einvoiceEnabled),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/einvoice-documents-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxEinvoiceSubmit, async (HttpContext context, ErpEinvoiceSubmitBody? body, ILegacySessionValidator validator, IErpEinvoiceSubmitDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpEinvoiceSubmitRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxEinvoiceCreditNote, async (HttpContext context, ErpEinvoiceCreditNoteBody? body, ILegacySessionValidator validator, IErpEinvoiceCreditNoteDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpEinvoiceCreditNoteRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxEinvoicePollAsp, async (HttpContext context, ErpEinvoicePollAspBody? body, ILegacySessionValidator validator, IErpEinvoicePollAspDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpEinvoicePollAspRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapGet(EcomAeRoutes.ErpTaxExternalReporting, async (
            HttpContext context,
            string? country,
            ILegacySessionValidator validator,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for external-reporting digest.");
            }

            var cc = ErpExternalReportingCatalog.NormalizeCountry(country);
            var cats = ErpExternalReportingCatalog.Categories.Select(c =>
            {
                var stats = ErpExternalReportingCatalog.CategoryStats(c.Key);
                return new { key = c.Key, label = c.Label, reports = stats.Count, live = stats.HasLive };
            }).ToList();
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary = new
                {
                    categories = ErpExternalReportingCatalog.CategoryCount,
                    reports = ErpExternalReportingCatalog.ReportCount,
                    country = cc,
                    country_name = ErpExternalReportingCatalog.CountryName(cc),
                    ifrs18 = ErpExternalReportingCatalog.Ifrs18Applies(DateTime.UtcNow.Year),
                },
                categories = cats,
                source = "catalog",
                message = string.Empty,
                session = SessionPayload(session),
                note = "Read-only PHP epc_ext_reports_* catalogue. Fetch / import / intake generate stay PHP (writes=0 dry-run)."
            });
        });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxExternalReportingFetch, async (HttpContext context, ErpExternalReportingFetchBody? body, ILegacySessionValidator validator, IErpExternalReportingFetchDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new("fetch", null, false); return Results.Ok(dryRun.Evaluate(new ErpExternalReportingFetchRequest(body.Action, body.ReportKey, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOrderFulfillmentBootstrap, async (HttpContext context, ErpOrderFulfillmentBootstrapBody? body, ILegacySessionValidator validator, IErpOrderFulfillmentBootstrapDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpOrderFulfillmentBootstrapRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOrderFulfillmentStatus, async (HttpContext context, ErpOrderFulfillmentStatusBody? body, ILegacySessionValidator validator, IErpOrderFulfillmentStatusDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpOrderFulfillmentStatusRequest(body.Id, body.TargetStatus, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOrderFulfillmentSync, async (HttpContext context, ErpOrderFulfillmentSyncBody? body, ILegacySessionValidator validator, IErpOrderFulfillmentSyncDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpOrderFulfillmentSyncRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOrderFulfillmentPostPo, async (HttpContext context, ErpOrderFulfillmentPostPoBody? body, ILegacySessionValidator validator, IErpOrderFulfillmentPostPoDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpOrderFulfillmentPostPoRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOrderFulfillmentPostSales, async (HttpContext context, ErpOrderFulfillmentPostSalesBody? body, ILegacySessionValidator validator, IErpOrderFulfillmentPostSalesDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpOrderFulfillmentPostSalesRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOrderFulfillmentAutoPost, async (HttpContext context, ErpOrderFulfillmentAutoPostBody? body, ILegacySessionValidator validator, IErpOrderFulfillmentAutoPostDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpOrderFulfillmentAutoPostRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOrderFulfillmentSwapSupplier, async (HttpContext context, ErpOrderFulfillmentSwapSupplierBody? body, ILegacySessionValidator validator, IErpOrderFulfillmentSwapSupplierDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpOrderFulfillmentSwapSupplierRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPmSave, async (HttpContext context, ErpPmSaveBody? body, ILegacySessionValidator validator, IErpPmSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPmSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPmToggle, async (HttpContext context, ErpPmToggleBody? body, ILegacySessionValidator validator, IErpPmToggleDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpPmToggleRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPmBudgetSave, async (HttpContext context, ErpPmBudgetSaveBody? body, ILegacySessionValidator validator, IErpPmBudgetSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPmBudgetSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPmBudgetLineSave, async (HttpContext context, ErpPmBudgetLineSaveBody? body, ILegacySessionValidator validator, IErpPmBudgetLineSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPmBudgetLineSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPmListingSave, async (HttpContext context, ErpPmListingSaveBody? body, ILegacySessionValidator validator, IErpPmListingSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPmListingSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPmListingAttach, async (HttpContext context, ErpPmListingAttachBody? body, ILegacySessionValidator validator, IErpPmListingAttachDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPmListingAttachRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPmChequeSave, async (HttpContext context, ErpPmChequeSaveBody? body, ILegacySessionValidator validator, IErpPmChequeSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPmChequeSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxMfgrWcSave, async (HttpContext context, ErpMfgrWcSaveBody? body, ILegacySessionValidator validator, IErpMfgrWcSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpMfgrWcSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxMfgrRouteSave, async (HttpContext context, ErpMfgrRouteSaveBody? body, ILegacySessionValidator validator, IErpMfgrRouteSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpMfgrRouteSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxMfgrMrpRun, async (HttpContext context, ErpMfgrMrpRunBody? body, ILegacySessionValidator validator, IErpMfgrMrpRunDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpMfgrMrpRunRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxMfgrPlannedFirm, async (HttpContext context, ErpMfgrPlannedFirmBody? body, ILegacySessionValidator validator, IErpMfgrPlannedFirmDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpMfgrPlannedFirmRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxQmPlanSave, async (HttpContext context, ErpQmPlanSaveBody? body, ILegacySessionValidator validator, IErpQmPlanSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpQmPlanSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxQmTestAdd, async (HttpContext context, ErpQmTestAddBody? body, ILegacySessionValidator validator, IErpQmTestAddDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpQmTestAddRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxQmOrderCreate, async (HttpContext context, ErpQmOrderCreateBody? body, ILegacySessionValidator validator, IErpQmOrderCreateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpQmOrderCreateRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxQmOrderRecord, async (HttpContext context, ErpQmOrderRecordBody? body, ILegacySessionValidator validator, IErpQmOrderRecordDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpQmOrderRecordRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxQmNcrCreate, async (HttpContext context, ErpQmNcrCreateBody? body, ILegacySessionValidator validator, IErpQmNcrCreateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpQmNcrCreateRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxQmNcrUpdate, async (HttpContext context, ErpQmNcrUpdateBody? body, ILegacySessionValidator validator, IErpQmNcrUpdateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpQmNcrUpdateRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });

        endpoints.MapPost(EcomAeRoutes.ErpQualityPlanSaveForm, async (HttpContext context, ILegacySessionValidator validator, IErpQmPlanSaveDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            var ret = DryRunHtmlForm.SafeReturnUrl(context.Request, "/erp/quality-app?qv=plans");
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Results.Redirect("/erp/login");
            var code = DryRunHtmlForm.Read(context.Request, "code");
            var result = dryRun.Evaluate(new ErpQmPlanSaveRequest(0, code, false));
            return DryRunHtmlForm.Redirect(ret, result.ValidationCode == "ok", result.Detail);
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpQualityOrderCreateForm, async (HttpContext context, ILegacySessionValidator validator, IErpQmOrderCreateDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            var ret = DryRunHtmlForm.SafeReturnUrl(context.Request, "/erp/quality-app?qv=orders");
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Results.Redirect("/erp/login");
            var code = DryRunHtmlForm.Read(context.Request, "ref_id");
            if (string.IsNullOrWhiteSpace(code)) code = DryRunHtmlForm.Read(context.Request, "code");
            var result = dryRun.Evaluate(new ErpQmOrderCreateRequest(0, code, false));
            return DryRunHtmlForm.Redirect(ret, result.ValidationCode == "ok", result.Detail);
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpQualityNcrCreateForm, async (HttpContext context, ILegacySessionValidator validator, IErpQmNcrCreateDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            var ret = DryRunHtmlForm.SafeReturnUrl(context.Request, "/erp/quality-app?qv=ncr");
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Results.Redirect("/erp/login");
            var code = DryRunHtmlForm.Read(context.Request, "title");
            var result = dryRun.Evaluate(new ErpQmNcrCreateRequest(0, code, false));
            return DryRunHtmlForm.Redirect(ret, result.ValidationCode == "ok", result.Detail);
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpProductInfoCreateItemForm, async (HttpContext context, ILegacySessionValidator validator, IErpInvCreateItemDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            var ret = DryRunHtmlForm.SafeReturnUrl(context.Request, "/erp/product-info-app");
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Results.Redirect("/erp/login");
            var code = DryRunHtmlForm.Read(context.Request, "code");
            if (string.IsNullOrWhiteSpace(code)) code = DryRunHtmlForm.Read(context.Request, "sku");
            var result = dryRun.Evaluate(new ErpInvCreateItemRequest(0, code, false));
            return DryRunHtmlForm.Redirect(ret, result.ValidationCode == "ok", result.Detail);
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryRepairCreateForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwRepairCreateDryRun dryRun,
            IErpJwRepairWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-repairs-app",
                    "Admin ERP capability required for jewellery repair create.");
            }

            var form = context.Request.HasFormContentType
                ? await context.Request.ReadFormAsync(cancellationToken)
                : null;
            var confirm = form is not null && LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            var customerName = form is not null
                ? LiveWriteFormBinder.Text(form, "customerName", "customer_name", "code")
                : DryRunHtmlForm.Read(context.Request, "customer_name");
            var itemDescription = form is not null
                ? LiveWriteFormBinder.Text(form, "itemDescription", "item_description")
                : DryRunHtmlForm.Read(context.Request, "item_description");
            if (!confirm)
            {
                var code = string.IsNullOrWhiteSpace(customerName) ? itemDescription : customerName;
                var result = dryRun.Evaluate(new ErpJwRepairCreateRequest(0, code, false));
                return DryRunHtmlForm.Redirect(
                    DryRunHtmlForm.SafeReturnUrl(context.Request, "/cp/jewellery-repairs-app?tab=jw_repairs"),
                    result.ValidationCode == "ok",
                    result.Detail);
            }

            var written = await writes.CreateAsync(
                new ErpJwRepairSaveRequest(
                    form is not null ? LiveWriteFormBinder.Text(form, "repairNo", "repair_no") : null,
                    form is not null ? LiveWriteFormBinder.Long(form, "customerId", "customer_id") : 0,
                    customerName,
                    form is not null ? LiveWriteFormBinder.Text(form, "customerPhone", "customer_phone") : null,
                    itemDescription,
                    form is not null ? LiveWriteFormBinder.Text(form, "metalType", "metal_type") : null,
                    form is not null ? LiveWriteFormBinder.Text(form, "karat") : null,
                    form is not null ? LiveWriteFormBinder.Dec(form, "grossWtIn", "gross_wt_in") : 0,
                    form is not null ? LiveWriteFormBinder.Dec(form, "netWtIn", "net_wt_in") : 0,
                    form is not null ? LiveWriteFormBinder.Text(form, "stoneDetails", "stone_details") : null,
                    form is not null ? LiveWriteFormBinder.Text(form, "repairType", "repair_type") : null,
                    form is not null ? LiveWriteFormBinder.Dec(form, "estimatedCost", "estimated_cost") : 0,
                    form is not null ? LiveWriteFormBinder.Long(form, "receivedDate", "received_date") : 0,
                    form is not null ? LiveWriteFormBinder.Long(form, "promisedDate", "promised_date") : 0,
                    session.UserId),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/jewellery-repairs-app?tab=jw_repairs",
                written.Succeeded,
                written.Message,
                new
                {
                    ok = written.Succeeded,
                    status = written.Succeeded,
                    writes = written.Writes,
                    phpAuthoritative = false,
                    validation_code = written.Code,
                    message = written.Message,
                    id = written.Id,
                    repair_id = written.Id,
                    session = SessionPayload(session)
                });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryRepairStatusForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwRepairUpdateStatusDryRun dryRun,
            IErpJwRepairWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-repairs-app",
                    "Admin ERP capability required for jewellery repair status.");
            }

            var form = context.Request.HasFormContentType
                ? await context.Request.ReadFormAsync(cancellationToken)
                : null;
            var repairId = form is not null
                ? LiveWriteFormBinder.Long(form, "repairId", "repair_id", "id")
                : 0;
            if (repairId <= 0)
            {
                var idRaw = DryRunHtmlForm.Read(context.Request, "repair_id");
                _ = long.TryParse(idRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out repairId);
            }

            var status = form is not null
                ? LiveWriteFormBinder.Text(form, "newStatus", "new_status", "targetStatus", "status")
                : DryRunHtmlForm.Read(context.Request, "new_status");
            var confirm = form is not null && LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwRepairUpdateStatusRequest(repairId, status, false));
                return DryRunHtmlForm.Redirect(
                    DryRunHtmlForm.SafeReturnUrl(context.Request, "/cp/jewellery-repairs-app?tab=jw_repairs"),
                    result.ValidationCode == "ok",
                    result.Detail);
            }

            var written = await writes.SetStatusAsync(repairId, status, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/jewellery-repairs-app?tab=jw_repairs",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryKaratSaveForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwKaratWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-masters-app?tab=jw_karat",
                    "Admin ERP capability required for jewellery karat save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwKaratSaveBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var karatCode = body.KaratCode;
            var description = body.Description;
            var stdPurity = body.StdPurity;
            var rangeFrom = body.RangeFrom;
            var rangeTo = body.RangeTo;
            var spGravity = body.SpGravity;
            var posRate = body.PosRateMinMax;
            var division = body.Division;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                karatCode = LiveWriteFormBinder.Text(form, "karatCode", "karat_code", "code");
                description = LiveWriteFormBinder.Text(form, "description");
                stdPurity = LiveWriteFormBinder.Dec(form, "stdPurity", "std_purity");
                rangeFrom = LiveWriteFormBinder.Dec(form, "rangeFrom", "range_from");
                rangeTo = LiveWriteFormBinder.Dec(form, "rangeTo", "range_to");
                spGravity = LiveWriteFormBinder.Dec(form, "spGravity", "sp_gravity");
                posRate = LiveWriteFormBinder.Dec(form, "posRateMinMax", "pos_rate_min_max");
                division = LiveWriteFormBinder.Text(form, "division");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_karat_save", karatCode, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(
                        DryRunHtmlForm.SafeReturnUrl(context.Request, "/cp/jewellery-masters-app?tab=jw_karat"),
                        result.ValidationCode == "ok",
                        result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveAsync(
                new ErpJwKaratSaveRequest(
                    companyId,
                    karatCode,
                    description,
                    stdPurity,
                    rangeFrom,
                    rangeTo,
                    spGravity,
                    posRate,
                    division),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/jewellery-masters-app?tab=jw_karat",
                written.Succeeded,
                written.Message,
                new
                {
                    ok = written.Succeeded,
                    status = written.Succeeded,
                    writes = written.Writes,
                    phpAuthoritative = false,
                    validation_code = written.Code,
                    message = written.Message,
                    id = written.Id,
                    karat_code = karatCode,
                    session = SessionPayload(session)
                });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryRateTypeSaveForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwRateTypeWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-masters-app?tab=jw_rate_type",
                    "Admin ERP capability required for jewellery rate type save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwRateTypeSaveBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var metal = body.Metal;
            var rateType = body.RateType;
            var convFactor = body.ConvFactor;
            var convFactorOz = body.ConvFactorOz;
            var currency = body.Currency;
            var currRate = body.CurrRate;
            var rateVariancePct = body.RateVariancePct;
            var posMarginMin = body.PosMarginMin;
            var posMarginMax = body.PosMarginMax;
            var status = body.Status;
            var isDefault = body.IsDefault;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                metal = LiveWriteFormBinder.Text(form, "metal");
                rateType = LiveWriteFormBinder.Text(form, "rateType", "rate_type", "code");
                convFactor = LiveWriteFormBinder.Dec(form, "convFactor", "conv_factor");
                if (convFactor == 0 && string.IsNullOrWhiteSpace(LiveWriteFormBinder.Text(form, "convFactor", "conv_factor")))
                {
                    convFactor = 1;
                }

                convFactorOz = LiveWriteFormBinder.Dec(form, "convFactorOz", "conv_factor_oz");
                if (convFactorOz == 0 && string.IsNullOrWhiteSpace(LiveWriteFormBinder.Text(form, "convFactorOz", "conv_factor_oz")))
                {
                    convFactorOz = 31.1035m;
                }

                currency = LiveWriteFormBinder.Text(form, "currency");
                currRate = LiveWriteFormBinder.Dec(form, "currRate", "curr_rate");
                if (currRate == 0 && string.IsNullOrWhiteSpace(LiveWriteFormBinder.Text(form, "currRate", "curr_rate")))
                {
                    currRate = 1;
                }

                rateVariancePct = LiveWriteFormBinder.Dec(form, "rateVariancePct", "rate_variance_pct");
                if (rateVariancePct == 0 && string.IsNullOrWhiteSpace(LiveWriteFormBinder.Text(form, "rateVariancePct", "rate_variance_pct")))
                {
                    rateVariancePct = 50;
                }

                posMarginMin = LiveWriteFormBinder.Dec(form, "posMarginMin", "pos_margin_min");
                if (posMarginMin == 0 && string.IsNullOrWhiteSpace(LiveWriteFormBinder.Text(form, "posMarginMin", "pos_margin_min")))
                {
                    posMarginMin = 1;
                }

                posMarginMax = LiveWriteFormBinder.Dec(form, "posMarginMax", "pos_margin_max");
                if (posMarginMax == 0 && string.IsNullOrWhiteSpace(LiveWriteFormBinder.Text(form, "posMarginMax", "pos_margin_max")))
                {
                    posMarginMax = 50;
                }

                status = LiveWriteFormBinder.Text(form, "status");
                isDefault = LiveWriteFormBinder.Flag(form, "isDefault", "is_default");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_rate_type_save", rateType, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(
                        DryRunHtmlForm.SafeReturnUrl(context.Request, "/cp/jewellery-masters-app?tab=jw_rate_type"),
                        result.ValidationCode == "ok",
                        result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveAsync(
                new ErpJwRateTypeSaveRequest(
                    companyId,
                    metal,
                    rateType,
                    convFactor,
                    convFactorOz,
                    currency,
                    currRate,
                    rateVariancePct,
                    posMarginMin,
                    posMarginMax,
                    status,
                    isDefault),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/jewellery-masters-app?tab=jw_rate_type",
                written.Succeeded,
                written.Message,
                new
                {
                    ok = written.Succeeded,
                    status = written.Succeeded,
                    writes = written.Writes,
                    phpAuthoritative = false,
                    validation_code = written.Code,
                    message = written.Message,
                    id = written.Id,
                    rate_type = rateType,
                    session = SessionPayload(session)
                });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryCurrencySaveForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwCurrencyWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-masters-app?tab=jw_currency",
                    "Admin ERP capability required for jewellery currency save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwCurrencySaveBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var currCode = body.CurrCode;
            var description = body.Description;
            var fraction = body.Fraction;
            var symbol = body.Symbol;
            var convRate = body.ConvRate;
            var minConvRate = body.MinConvRate;
            var maxConvRate = body.MaxConvRate;
            var status = body.Status;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                currCode = LiveWriteFormBinder.Text(form, "currCode", "curr_code", "code");
                description = LiveWriteFormBinder.Text(form, "description", "name");
                fraction = LiveWriteFormBinder.Text(form, "fraction");
                symbol = LiveWriteFormBinder.Text(form, "symbol");
                convRate = LiveWriteFormBinder.Dec(form, "convRate", "conv_rate");
                if (convRate == 0 && string.IsNullOrWhiteSpace(LiveWriteFormBinder.Text(form, "convRate", "conv_rate")))
                {
                    convRate = 1;
                }

                minConvRate = LiveWriteFormBinder.Dec(form, "minConvRate", "min_conv_rate");
                if (minConvRate == 0 && string.IsNullOrWhiteSpace(LiveWriteFormBinder.Text(form, "minConvRate", "min_conv_rate")))
                {
                    minConvRate = 1;
                }

                maxConvRate = LiveWriteFormBinder.Dec(form, "maxConvRate", "max_conv_rate");
                if (maxConvRate == 0 && string.IsNullOrWhiteSpace(LiveWriteFormBinder.Text(form, "maxConvRate", "max_conv_rate")))
                {
                    maxConvRate = 1;
                }

                status = LiveWriteFormBinder.Text(form, "status");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_currency_save", currCode, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(
                        DryRunHtmlForm.SafeReturnUrl(context.Request, "/cp/jewellery-masters-app?tab=jw_currency"),
                        result.ValidationCode == "ok",
                        result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveAsync(
                new ErpJwCurrencySaveRequest(
                    companyId,
                    currCode,
                    description,
                    fraction,
                    symbol,
                    convRate,
                    minConvRate,
                    maxConvRate,
                    status),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/jewellery-masters-app?tab=jw_currency",
                written.Succeeded,
                written.Message,
                new
                {
                    ok = written.Succeeded,
                    status = written.Succeeded,
                    writes = written.Writes,
                    phpAuthoritative = false,
                    validation_code = written.Code,
                    message = written.Message,
                    id = written.Id,
                    curr_code = currCode,
                    session = SessionPayload(session)
                });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryDiamondSaveForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwDiamondWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-masters-app?tab=jw_diamond",
                    "Admin ERP capability required for jewellery diamond save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwDiamondSaveBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var itemCode = body.ItemCode;
            var description = body.Description;
            var design = body.Design;
            var rfid = body.Rfid;
            var category = body.Category;
            var subCategory = body.SubCategory;
            var type = body.Type;
            var brand = body.Brand;
            var color = body.Color;
            var clarity = body.Clarity;
            var fluorescence = body.Fluorescence;
            var style = body.Style;
            var setRef = body.SetRef;
            var country = body.Country;
            var vendor = body.Vendor;
            var vendorRef = body.VendorRef;
            var currency = body.Currency;
            var currencyRate = body.CurrencyRate;
            var costCentre = body.CostCentre;
            var costAmount = body.CostAmount;
            var itemGrWt = body.ItemGrWt;
            var price1Code = body.Price1Code;
            var price1Pct = body.Price1Pct;
            var price1Fc = body.Price1Fc;
            var price1Lc = body.Price1Lc;
            var promotional = body.Promotional;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                itemCode = LiveWriteFormBinder.Text(form, "itemCode", "item_code", "code");
                description = LiveWriteFormBinder.Text(form, "description", "name");
                design = LiveWriteFormBinder.Text(form, "design");
                rfid = LiveWriteFormBinder.Text(form, "rfid");
                category = LiveWriteFormBinder.Text(form, "category");
                subCategory = LiveWriteFormBinder.Text(form, "subCategory", "sub_category");
                type = LiveWriteFormBinder.Text(form, "type");
                brand = LiveWriteFormBinder.Text(form, "brand");
                color = LiveWriteFormBinder.Text(form, "color");
                clarity = LiveWriteFormBinder.Text(form, "clarity");
                fluorescence = LiveWriteFormBinder.Text(form, "fluorescence");
                style = LiveWriteFormBinder.Text(form, "style");
                setRef = LiveWriteFormBinder.Text(form, "setRef", "set_ref");
                country = LiveWriteFormBinder.Text(form, "country");
                vendor = LiveWriteFormBinder.Text(form, "vendor");
                vendorRef = LiveWriteFormBinder.Text(form, "vendorRef", "vendor_ref");
                currency = LiveWriteFormBinder.Text(form, "currency");
                currencyRate = LiveWriteFormBinder.Dec(form, "currencyRate", "currency_rate");
                if (currencyRate == 0 && string.IsNullOrWhiteSpace(LiveWriteFormBinder.Text(form, "currencyRate", "currency_rate")))
                {
                    currencyRate = 1;
                }

                costCentre = LiveWriteFormBinder.Text(form, "costCentre", "cost_centre");
                costAmount = LiveWriteFormBinder.Dec(form, "costAmount", "cost_amount");
                itemGrWt = LiveWriteFormBinder.Dec(form, "itemGrWt", "item_gr_wt");
                price1Code = LiveWriteFormBinder.Text(form, "price1Code", "price1_code");
                price1Pct = LiveWriteFormBinder.Dec(form, "price1Pct", "price1_pct");
                price1Fc = LiveWriteFormBinder.Dec(form, "price1Fc", "price1_fc");
                price1Lc = LiveWriteFormBinder.Dec(form, "price1Lc", "price1_lc");
                promotional = LiveWriteFormBinder.Flag(form, "promotional");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_diamond_save", itemCode, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(
                        DryRunHtmlForm.SafeReturnUrl(context.Request, "/cp/jewellery-masters-app?tab=jw_diamond"),
                        result.ValidationCode == "ok",
                        result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveAsync(
                new ErpJwDiamondSaveRequest(
                    companyId,
                    itemCode,
                    description,
                    design,
                    rfid,
                    category,
                    subCategory,
                    type,
                    brand,
                    color,
                    clarity,
                    fluorescence,
                    style,
                    setRef,
                    country,
                    vendor,
                    vendorRef,
                    currency,
                    currencyRate,
                    costCentre,
                    costAmount,
                    itemGrWt,
                    price1Code,
                    price1Pct,
                    price1Fc,
                    price1Lc,
                    promotional),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/jewellery-masters-app?tab=jw_diamond",
                written.Succeeded,
                written.Message,
                new
                {
                    ok = written.Succeeded,
                    status = written.Succeeded,
                    writes = written.Writes,
                    phpAuthoritative = false,
                    validation_code = written.Code,
                    message = written.Message,
                    id = written.Id,
                    item_code = itemCode,
                    session = SessionPayload(session)
                });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryDesignSaveForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwDesignWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-masters-app?tab=jw_design",
                    "Admin ERP capability required for jewellery design save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwDesignSaveBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var designCode = body.DesignCode;
            var description = body.Description;
            var currency = body.Currency;
            var currencyRate = body.CurrencyRate;
            var costCentre = body.CostCentre;
            var category = body.Category;
            var subCategory = body.SubCategory;
            var type = body.Type;
            var brand = body.Brand;
            var color = body.Color;
            var country = body.Country;
            var vendor = body.Vendor;
            var vendorRef = body.VendorRef;
            var costAmount = body.CostAmount;
            var price1Code = body.Price1Code;
            var price1Pct = body.Price1Pct;
            var price1Fc = body.Price1Fc;
            var price1Lc = body.Price1Lc;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                designCode = LiveWriteFormBinder.Text(form, "designCode", "design_code", "code");
                description = LiveWriteFormBinder.Text(form, "description", "name");
                currency = LiveWriteFormBinder.Text(form, "currency");
                currencyRate = LiveWriteFormBinder.Dec(form, "currencyRate", "currency_rate");
                if (currencyRate == 0 && string.IsNullOrWhiteSpace(LiveWriteFormBinder.Text(form, "currencyRate", "currency_rate")))
                {
                    currencyRate = 1;
                }

                costCentre = LiveWriteFormBinder.Text(form, "costCentre", "cost_centre");
                category = LiveWriteFormBinder.Text(form, "category");
                subCategory = LiveWriteFormBinder.Text(form, "subCategory", "sub_category");
                type = LiveWriteFormBinder.Text(form, "type");
                brand = LiveWriteFormBinder.Text(form, "brand");
                color = LiveWriteFormBinder.Text(form, "color");
                country = LiveWriteFormBinder.Text(form, "country");
                vendor = LiveWriteFormBinder.Text(form, "vendor");
                vendorRef = LiveWriteFormBinder.Text(form, "vendorRef", "vendor_ref");
                costAmount = LiveWriteFormBinder.Dec(form, "costAmount", "cost_amount");
                price1Code = LiveWriteFormBinder.Text(form, "price1Code", "price1_code");
                price1Pct = LiveWriteFormBinder.Dec(form, "price1Pct", "price1_pct");
                price1Fc = LiveWriteFormBinder.Dec(form, "price1Fc", "price1_fc");
                price1Lc = LiveWriteFormBinder.Dec(form, "price1Lc", "price1_lc");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_design_save", designCode, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(
                        DryRunHtmlForm.SafeReturnUrl(context.Request, "/cp/jewellery-masters-app?tab=jw_design"),
                        result.ValidationCode == "ok",
                        result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveAsync(
                new ErpJwDesignSaveRequest(
                    companyId,
                    designCode,
                    description,
                    currency,
                    currencyRate,
                    costCentre,
                    category,
                    subCategory,
                    type,
                    brand,
                    color,
                    country,
                    vendor,
                    vendorRef,
                    costAmount,
                    price1Code,
                    price1Pct,
                    price1Fc,
                    price1Lc),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/jewellery-masters-app?tab=jw_design",
                written.Succeeded,
                written.Message,
                new
                {
                    ok = written.Succeeded,
                    status = written.Succeeded,
                    writes = written.Writes,
                    phpAuthoritative = false,
                    validation_code = written.Code,
                    message = written.Message,
                    id = written.Id,
                    design_code = designCode,
                    session = SessionPayload(session)
                });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryPearlSaveForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwPearlWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-masters-app?tab=jw_pearl",
                    "Admin ERP capability required for jewellery pearl save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwPearlSaveBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var code = body.Code;
            var nature = body.Nature;
            var description = body.Description;
            var design = body.Design;
            var type = body.Type;
            var costCentre = body.CostCentre;
            var category = body.Category;
            var color = body.Color;
            var vendor = body.Vendor;
            var vendorRef = body.VendorRef;
            var luster = body.Luster;
            var shape = body.Shape;
            var size = body.Size;
            var brand = body.Brand;
            var country = body.Country;
            var grade = body.Grade;
            var subCategory = body.SubCategory;
            var currency = body.Currency;
            var currencyRate = body.CurrencyRate;
            var costAmount = body.CostAmount;
            var price1Code = body.Price1Code;
            var price1Pct = body.Price1Pct;
            var price1Fc = body.Price1Fc;
            var price1Lc = body.Price1Lc;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                code = LiveWriteFormBinder.Text(form, "code", "pearl_code", "item_code");
                nature = LiveWriteFormBinder.Text(form, "nature");
                description = LiveWriteFormBinder.Text(form, "description", "name");
                design = LiveWriteFormBinder.Text(form, "design");
                type = LiveWriteFormBinder.Text(form, "type");
                costCentre = LiveWriteFormBinder.Text(form, "costCentre", "cost_centre");
                category = LiveWriteFormBinder.Text(form, "category");
                color = LiveWriteFormBinder.Text(form, "color");
                vendor = LiveWriteFormBinder.Text(form, "vendor");
                vendorRef = LiveWriteFormBinder.Text(form, "vendorRef", "vendor_ref");
                luster = LiveWriteFormBinder.Text(form, "luster");
                shape = LiveWriteFormBinder.Text(form, "shape");
                size = LiveWriteFormBinder.Text(form, "size");
                brand = LiveWriteFormBinder.Text(form, "brand");
                country = LiveWriteFormBinder.Text(form, "country");
                grade = LiveWriteFormBinder.Text(form, "grade");
                subCategory = LiveWriteFormBinder.Text(form, "subCategory", "sub_category");
                currency = LiveWriteFormBinder.Text(form, "currency");
                currencyRate = LiveWriteFormBinder.Dec(form, "currencyRate", "currency_rate");
                if (currencyRate == 0 && string.IsNullOrWhiteSpace(LiveWriteFormBinder.Text(form, "currencyRate", "currency_rate")))
                {
                    currencyRate = 1;
                }

                costAmount = LiveWriteFormBinder.Dec(form, "costAmount", "cost_amount");
                price1Code = LiveWriteFormBinder.Text(form, "price1Code", "price1_code");
                price1Pct = LiveWriteFormBinder.Dec(form, "price1Pct", "price1_pct");
                price1Fc = LiveWriteFormBinder.Dec(form, "price1Fc", "price1_fc");
                price1Lc = LiveWriteFormBinder.Dec(form, "price1Lc", "price1_lc");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_pearl_save", code, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(
                        DryRunHtmlForm.SafeReturnUrl(context.Request, "/cp/jewellery-masters-app?tab=jw_pearl"),
                        result.ValidationCode == "ok",
                        result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveAsync(
                new ErpJwPearlSaveRequest(
                    companyId,
                    code,
                    nature,
                    description,
                    design,
                    type,
                    costCentre,
                    category,
                    color,
                    vendor,
                    vendorRef,
                    luster,
                    shape,
                    size,
                    brand,
                    country,
                    grade,
                    subCategory,
                    currency,
                    currencyRate,
                    costAmount,
                    price1Code,
                    price1Pct,
                    price1Fc,
                    price1Lc),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/jewellery-masters-app?tab=jw_pearl",
                written.Succeeded,
                written.Message,
                new
                {
                    ok = written.Succeeded,
                    status = written.Succeeded,
                    writes = written.Writes,
                    phpAuthoritative = false,
                    validation_code = written.Code,
                    message = written.Message,
                    id = written.Id,
                    code,
                    session = SessionPayload(session)
                });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryColorStoneSaveForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwColorStoneWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-masters-app?tab=jw_color_stone",
                    "Admin ERP capability required for jewellery color stone save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwColorStoneSaveBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var code = body.Code;
            var description = body.Description;
            var category = body.Category;
            var shape = body.Shape;
            var clarity = body.Clarity;
            var size = body.Size;
            var color = body.Color;
            var finish = body.Finish;
            var country = body.Country;
            var certificateNo = body.CertificateNo;
            var vendor = body.Vendor;
            var costCentre = body.CostCentre;
            var grade = body.Grade;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                code = LiveWriteFormBinder.Text(form, "item_code", "itemCode", "code");
                description = LiveWriteFormBinder.Text(form, "description", "name");
                category = LiveWriteFormBinder.Text(form, "stone_type", "stoneType", "category");
                shape = LiveWriteFormBinder.Text(form, "shape");
                clarity = LiveWriteFormBinder.Text(form, "clarity");
                size = LiveWriteFormBinder.Text(form, "size_mm", "sizeMm", "size");
                color = LiveWriteFormBinder.Text(form, "color_grade", "colorGrade", "color");
                finish = LiveWriteFormBinder.Text(form, "treatment", "finish");
                country = LiveWriteFormBinder.Text(form, "origin", "country");
                certificateNo = LiveWriteFormBinder.Text(form, "certificate_no", "certificateNo");
                vendor = LiveWriteFormBinder.Text(form, "vendor");
                costCentre = LiveWriteFormBinder.Text(form, "cost_centre", "costCentre");
                grade = LiveWriteFormBinder.Text(form, "grade");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_color_stone_save", code, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(
                        DryRunHtmlForm.SafeReturnUrl(context.Request, "/cp/jewellery-masters-app?tab=jw_color_stone"),
                        result.ValidationCode == "ok",
                        result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveAsync(
                new ErpJwColorStoneSaveRequest(
                    companyId,
                    code,
                    description,
                    category,
                    shape,
                    clarity,
                    size,
                    color,
                    finish,
                    country,
                    certificateNo,
                    vendor,
                    costCentre,
                    grade),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/jewellery-masters-app?tab=jw_color_stone",
                written.Succeeded,
                written.Message,
                new
                {
                    ok = written.Succeeded,
                    status = written.Succeeded,
                    writes = written.Writes,
                    phpAuthoritative = false,
                    validation_code = written.Code,
                    message = written.Message,
                    id = written.Id,
                    code,
                    session = SessionPayload(session)
                });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryBarcodeGenerateForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwBarcodeWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-masters-app?tab=jw_barcode",
                    "Admin ERP capability required for jewellery barcode generate.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwBarcodeGenerateBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var stockCode = body.StockCode;
            var division = body.Division;
            var karat = body.Karat;
            var grossWt = body.GrossWt;
            var purity = body.Purity;
            var tagPrice = body.TagPrice;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                stockCode = LiveWriteFormBinder.Text(form, "stockCode", "stock_code", "item_code", "code");
                division = LiveWriteFormBinder.Text(form, "division");
                karat = LiveWriteFormBinder.Text(form, "karat");
                grossWt = LiveWriteFormBinder.Dec(form, "grossWt", "gross_wt");
                purity = LiveWriteFormBinder.Dec(form, "purity");
                tagPrice = LiveWriteFormBinder.Dec(form, "tagPrice", "tag_price");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/cp/jewellery-masters-app?tab=jw_barcode";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_barcode_generate", stockCode, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.GenerateAsync(
                new ErpJwBarcodeGenerateRequest(companyId, stockCode, division, karat, grossWt, purity, tagPrice),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                stock_code = stockCode,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryTagCreateForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwTagWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-masters-app?tab=jewellery_tag",
                    "Admin ERP capability required for jewellery tag create.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwTagCreateBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var tagNo = body.TagNo;
            var barcode = body.Barcode;
            var itemType = body.ItemType;
            var karat = body.Karat;
            var grossWeight = body.GrossWeight;
            var netWeight = body.NetWeight;
            var stoneWeight = body.StoneWeight;
            var stoneCount = body.StoneCount;
            var makingCharges = body.MakingCharges;
            var makingType = body.MakingType;
            var costPrice = body.CostPrice;
            var sellPrice = body.SellPrice;
            var marginPct = body.MarginPct;
            var designNo = body.DesignNo;
            var category = body.Category;
            var subcategory = body.Subcategory;
            var supplierId = body.SupplierId;
            var purchaseId = body.PurchaseId;
            var purchaseDate = body.PurchaseDate;
            var location = body.Location;
            var description = body.Description;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                tagNo = LiveWriteFormBinder.Text(form, "tagNo", "tag_no");
                barcode = LiveWriteFormBinder.Text(form, "barcode");
                itemType = LiveWriteFormBinder.Text(form, "itemType", "item_type");
                karat = LiveWriteFormBinder.Text(form, "karat");
                grossWeight = LiveWriteFormBinder.Dec(form, "grossWeight", "gross_weight", "gross_wt");
                netWeight = LiveWriteFormBinder.Dec(form, "netWeight", "net_weight", "net_wt");
                stoneWeight = LiveWriteFormBinder.Dec(form, "stoneWeight", "stone_weight", "stone_wt");
                stoneCount = LiveWriteFormBinder.Int(form, "stoneCount", "stone_count");
                makingCharges = LiveWriteFormBinder.Dec(form, "makingCharges", "making_charges");
                makingType = LiveWriteFormBinder.Text(form, "makingType", "making_type");
                costPrice = LiveWriteFormBinder.Dec(form, "costPrice", "cost_price", "cost");
                sellPrice = LiveWriteFormBinder.Dec(form, "sellPrice", "sell_price");
                marginPct = LiveWriteFormBinder.Dec(form, "marginPct", "margin_pct");
                designNo = LiveWriteFormBinder.Text(form, "designNo", "design_no");
                category = LiveWriteFormBinder.Text(form, "category");
                subcategory = LiveWriteFormBinder.Text(form, "subcategory");
                supplierId = LiveWriteFormBinder.Int(form, "supplierId", "supplier_id");
                purchaseId = LiveWriteFormBinder.Int(form, "purchaseId", "purchase_id");
                purchaseDate = LiveWriteFormBinder.Text(form, "purchaseDate", "purchase_date");
                location = LiveWriteFormBinder.Text(form, "location");
                description = LiveWriteFormBinder.Text(form, "description");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/cp/jewellery-masters-app?tab=jewellery_tag";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_tag_create", tagNo ?? description, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.CreateAsync(
                new ErpJwTagCreateRequest(
                    companyId,
                    tagNo,
                    barcode,
                    itemType,
                    karat,
                    grossWeight,
                    netWeight,
                    stoneWeight,
                    stoneCount,
                    makingCharges,
                    makingType,
                    costPrice,
                    sellPrice,
                    marginPct,
                    designNo,
                    category,
                    subcategory,
                    supplierId,
                    purchaseId,
                    purchaseDate,
                    location,
                    description),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                tag_no = tagNo,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryTagSellForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwTagWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-masters-app?tab=jewellery_tag",
                    "Admin ERP capability required for jewellery tag sell.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwTagSellBody>(context, cancellationToken)
                       ?? new();
            var tagId = body.TagId;
            var invoiceId = body.InvoiceId;
            var salesmanId = body.SalesmanId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                tagId = LiveWriteFormBinder.Long(form, "tagId", "tag_id", "id");
                invoiceId = LiveWriteFormBinder.Int(form, "invoiceId", "invoice_id", "sold_invoice_id");
                salesmanId = LiveWriteFormBinder.Int(form, "salesmanId", "salesman_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/cp/jewellery-masters-app?tab=jewellery_tag";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_tag_sell", tagId.ToString(CultureInfo.InvariantCulture), false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SellAsync(
                new ErpJwTagSellRequest(tagId, invoiceId, salesmanId),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                tag_id = tagId,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryGoldSchemeCreateForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwGoldSchemeWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-masters-app?tab=gold_scheme",
                    "Admin ERP capability required for gold scheme create.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwGoldSchemeCreateBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var schemeCode = body.SchemeCode;
            var schemeName = body.SchemeName;
            var schemeType = body.SchemeType;
            var maturityMonths = body.MaturityMonths;
            var bonusType = body.BonusType;
            var bonusValue = body.BonusValue;
            var minInstallment = body.MinInstallment;
            var maxInstallment = body.MaxInstallment;
            var termsText = body.TermsText;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                schemeCode = LiveWriteFormBinder.Text(form, "schemeCode", "scheme_code", "code");
                schemeName = LiveWriteFormBinder.Text(form, "schemeName", "scheme_name", "name");
                schemeType = LiveWriteFormBinder.Text(form, "schemeType", "scheme_type");
                maturityMonths = LiveWriteFormBinder.Int(form, "maturityMonths", "maturity_months");
                bonusType = LiveWriteFormBinder.Text(form, "bonusType", "bonus_type");
                bonusValue = LiveWriteFormBinder.Dec(form, "bonusValue", "bonus_value");
                minInstallment = LiveWriteFormBinder.Dec(form, "minInstallment", "min_installment");
                maxInstallment = LiveWriteFormBinder.Dec(form, "maxInstallment", "max_installment");
                termsText = LiveWriteFormBinder.Text(form, "termsText", "terms_text", "terms");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/cp/jewellery-masters-app?tab=gold_scheme";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_gold_scheme_create", schemeCode ?? schemeName, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.CreateAsync(
                new ErpJwGoldSchemeCreateRequest(
                    companyId,
                    schemeCode,
                    schemeName,
                    schemeType,
                    maturityMonths,
                    bonusType,
                    bonusValue,
                    minInstallment,
                    maxInstallment,
                    termsText),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                scheme_code = schemeCode,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryGoldSchemeEnrollForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwGoldSchemeWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-masters-app?tab=gold_scheme",
                    "Admin ERP capability required for gold scheme enroll.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwGoldSchemeEnrollBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var schemeId = body.SchemeId;
            var customerId = body.CustomerId;
            var customerName = body.CustomerName;
            var installmentAmount = body.InstallmentAmount;
            var startDate = body.StartDate;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                schemeId = LiveWriteFormBinder.Long(form, "schemeId", "scheme_id");
                customerId = LiveWriteFormBinder.Int(form, "customerId", "customer_id");
                customerName = LiveWriteFormBinder.Text(form, "customerName", "customer_name");
                installmentAmount = LiveWriteFormBinder.Dec(form, "installmentAmount", "installment_amount");
                startDate = LiveWriteFormBinder.Text(form, "startDate", "start_date");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/cp/jewellery-masters-app?tab=gold_scheme";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_gold_scheme_enroll", schemeId.ToString(CultureInfo.InvariantCulture), false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.EnrollAsync(
                new ErpJwGoldSchemeEnrollRequest(companyId, schemeId, customerId, customerName, installmentAmount, startDate),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                scheme_id = schemeId,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryGoldSchemePayForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwGoldSchemeWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-masters-app?tab=gold_scheme",
                    "Admin ERP capability required for gold scheme pay.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwGoldSchemePayBody>(context, cancellationToken)
                       ?? new();
            var enrollmentId = body.EnrollmentId;
            var amount = body.Amount;
            var paymentMode = body.PaymentMode;
            var goldRate = body.GoldRate;
            var receiptNo = body.ReceiptNo;
            var paymentDate = body.PaymentDate;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                enrollmentId = LiveWriteFormBinder.Long(form, "enrollmentId", "enrollment_id");
                amount = LiveWriteFormBinder.Dec(form, "amount");
                paymentMode = LiveWriteFormBinder.Text(form, "paymentMode", "payment_mode");
                goldRate = LiveWriteFormBinder.Dec(form, "goldRate", "gold_rate");
                receiptNo = LiveWriteFormBinder.Text(form, "receiptNo", "receipt_no");
                paymentDate = LiveWriteFormBinder.Text(form, "paymentDate", "payment_date");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/cp/jewellery-masters-app?tab=gold_scheme";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_gold_scheme_pay", enrollmentId.ToString(CultureInfo.InvariantCulture), false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.PayAsync(
                new ErpJwGoldSchemePayRequest(enrollmentId, amount, paymentMode, goldRate, receiptNo, paymentDate),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                enrollment_id = enrollmentId,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryFixUnfixCreateForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwFixUnfixWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-fixing-app?tab=fix_unfix",
                    "Admin ERP capability required for fix/unfix create.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwFixUnfixCreateBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var purchaseId = body.PurchaseId;
            var supplierId = body.SupplierId;
            var supplierName = body.SupplierName;
            var purchaseDate = body.PurchaseDate;
            var structureType = body.StructureType;
            var metalType = body.MetalType;
            var karat = body.Karat;
            var weightGrams = body.WeightGrams;
            var fixRate = body.FixRate;
            var fixDate = body.FixDate;
            var fixReference = body.FixReference;
            var unfixEstimatedRate = body.UnfixEstimatedRate;
            var marginOnFix = body.MarginOnFix;
            var marginOnUnfix = body.MarginOnUnfix;
            var makingCharges = body.MakingCharges;
            var notes = body.Notes;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                purchaseId = LiveWriteFormBinder.Int(form, "purchaseId", "purchase_id");
                supplierId = LiveWriteFormBinder.Int(form, "supplierId", "supplier_id");
                supplierName = LiveWriteFormBinder.Text(form, "supplierName", "supplier_name");
                purchaseDate = LiveWriteFormBinder.Text(form, "purchaseDate", "purchase_date");
                structureType = LiveWriteFormBinder.Text(form, "structureType", "structure_type");
                metalType = LiveWriteFormBinder.Text(form, "metalType", "metal_type");
                karat = LiveWriteFormBinder.Text(form, "karat");
                weightGrams = LiveWriteFormBinder.Dec(form, "weightGrams", "weight_grams", "weight");
                fixRate = LiveWriteFormBinder.Dec(form, "fixRate", "fix_rate");
                fixDate = LiveWriteFormBinder.Text(form, "fixDate", "fix_date");
                fixReference = LiveWriteFormBinder.Text(form, "fixReference", "fix_reference");
                unfixEstimatedRate = LiveWriteFormBinder.Dec(form, "unfixEstimatedRate", "unfix_estimated_rate");
                marginOnFix = LiveWriteFormBinder.Dec(form, "marginOnFix", "margin_on_fix");
                marginOnUnfix = LiveWriteFormBinder.Dec(form, "marginOnUnfix", "margin_on_unfix");
                makingCharges = LiveWriteFormBinder.Dec(form, "makingCharges", "making_charges");
                notes = LiveWriteFormBinder.Text(form, "notes");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/cp/jewellery-fixing-app?tab=fix_unfix";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_fix_unfix_create", supplierName, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.CreateAsync(
                new ErpJwFixUnfixCreateRequest(
                    companyId,
                    purchaseId,
                    supplierId,
                    supplierName,
                    purchaseDate,
                    structureType,
                    metalType,
                    karat,
                    weightGrams,
                    fixRate,
                    fixDate,
                    fixReference,
                    unfixEstimatedRate,
                    marginOnFix,
                    marginOnUnfix,
                    makingCharges,
                    notes),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                supplier_name = supplierName,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryFixUnfixSettleForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwFixUnfixWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-fixing-app?tab=fix_unfix",
                    "Admin ERP capability required for fix/unfix settle.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwFixUnfixSettleBody>(context, cancellationToken)
                       ?? new();
            var id = body.Id;
            var settleRate = body.SettleRate;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "purchaseId", "purchase_id");
                settleRate = LiveWriteFormBinder.Dec(form, "settleRate", "settle_rate");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/cp/jewellery-fixing-app?tab=fix_unfix";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_fix_unfix_settle", id.ToString(CultureInfo.InvariantCulture), false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SettleAsync(new ErpJwFixUnfixSettleRequest(id, settleRate), cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryBarcodePurchaseCreateForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwBarcodePurchaseWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/erp/purchase-orders-app?tab=barcode_purchase",
                    "Admin ERP capability required for barcode purchase create.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwBarcodePurchaseCreateBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var barcode = body.Barcode;
            var itemDescription = body.ItemDescription;
            var supplierId = body.SupplierId;
            var supplierName = body.SupplierName;
            var purchaseDate = body.PurchaseDate;
            var purchaseInvoiceNo = body.PurchaseInvoiceNo;
            var metalType = body.MetalType;
            var karat = body.Karat;
            var grossWeight = body.GrossWeight;
            var netWeight = body.NetWeight;
            var stoneWeight = body.StoneWeight;
            var goldRateAtPurchase = body.GoldRateAtPurchase;
            var makingCharges = body.MakingCharges;
            var stoneValue = body.StoneValue;
            var otherCharges = body.OtherCharges;
            var marginPct = body.MarginPct;
            var salesmanId = body.SalesmanId;
            var salesmanName = body.SalesmanName;
            var salesmanCommissionPct = body.SalesmanCommissionPct;
            var category = body.Category;
            var designNo = body.DesignNo;
            var hallmarkNo = body.HallmarkNo;
            var certificateNo = body.CertificateNo;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                barcode = LiveWriteFormBinder.Text(form, "barcode");
                itemDescription = LiveWriteFormBinder.Text(form, "itemDescription", "item_description", "description");
                supplierId = LiveWriteFormBinder.Int(form, "supplierId", "supplier_id");
                supplierName = LiveWriteFormBinder.Text(form, "supplierName", "supplier_name");
                purchaseDate = LiveWriteFormBinder.Text(form, "purchaseDate", "purchase_date");
                purchaseInvoiceNo = LiveWriteFormBinder.Text(form, "purchaseInvoiceNo", "purchase_invoice_no");
                metalType = LiveWriteFormBinder.Text(form, "metalType", "metal_type");
                karat = LiveWriteFormBinder.Text(form, "karat");
                grossWeight = LiveWriteFormBinder.Dec(form, "grossWeight", "gross_weight");
                netWeight = LiveWriteFormBinder.Dec(form, "netWeight", "net_weight");
                stoneWeight = LiveWriteFormBinder.Dec(form, "stoneWeight", "stone_weight");
                goldRateAtPurchase = LiveWriteFormBinder.Dec(form, "goldRateAtPurchase", "gold_rate_at_purchase", "gold_rate");
                makingCharges = LiveWriteFormBinder.Dec(form, "makingCharges", "making_charges");
                stoneValue = LiveWriteFormBinder.Dec(form, "stoneValue", "stone_value");
                otherCharges = LiveWriteFormBinder.Dec(form, "otherCharges", "other_charges");
                marginPct = LiveWriteFormBinder.Dec(form, "marginPct", "margin_pct");
                salesmanId = LiveWriteFormBinder.Int(form, "salesmanId", "salesman_id");
                salesmanName = LiveWriteFormBinder.Text(form, "salesmanName", "salesman_name");
                salesmanCommissionPct = LiveWriteFormBinder.Dec(form, "salesmanCommissionPct", "salesman_commission_pct");
                category = LiveWriteFormBinder.Text(form, "category");
                designNo = LiveWriteFormBinder.Text(form, "designNo", "design_no");
                hallmarkNo = LiveWriteFormBinder.Text(form, "hallmarkNo", "hallmark_no");
                certificateNo = LiveWriteFormBinder.Text(form, "certificateNo", "certificate_no");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/erp/purchase-orders-app?tab=barcode_purchase";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_barcode_purchase_create", barcode ?? itemDescription, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.CreateAsync(
                new ErpJwBarcodePurchaseCreateRequest(
                    companyId,
                    barcode,
                    itemDescription,
                    supplierId,
                    supplierName,
                    purchaseDate,
                    purchaseInvoiceNo,
                    metalType,
                    karat,
                    grossWeight,
                    netWeight,
                    stoneWeight,
                    goldRateAtPurchase,
                    makingCharges,
                    stoneValue,
                    otherCharges,
                    marginPct,
                    salesmanId,
                    salesmanName,
                    salesmanCommissionPct,
                    category,
                    designNo,
                    hallmarkNo,
                    certificateNo),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                barcode,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryBarcodePurchaseSellForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwBarcodePurchaseWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/erp/purchase-orders-app?tab=barcode_purchase",
                    "Admin ERP capability required for barcode purchase sell.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwBarcodePurchaseSellBody>(context, cancellationToken)
                       ?? new();
            var id = body.Id;
            var customerId = body.CustomerId;
            var invoiceId = body.InvoiceId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "purchaseId", "purchase_id");
                customerId = LiveWriteFormBinder.Int(form, "customerId", "customer_id");
                invoiceId = LiveWriteFormBinder.Int(form, "invoiceId", "invoice_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/erp/purchase-orders-app?tab=barcode_purchase";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_barcode_purchase_sell", id.ToString(CultureInfo.InvariantCulture), false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SellAsync(new ErpJwBarcodePurchaseSellRequest(id, customerId, invoiceId), cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpSlaCreateForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpSlaWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!PhpParityDumpCatalog.HasStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/crm-tickets-app?tab=sla",
                    "Admin ERP capability required for SLA create.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpSlaCreateBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var slaCode = body.SlaCode;
            var clientName = body.ClientName;
            var clientId = body.ClientId;
            var serviceType = body.ServiceType;
            var responseHours = body.ResponseHours;
            var resolutionHours = body.ResolutionHours;
            var uptimePct = body.UptimePct;
            var penaltyType = body.PenaltyType;
            var penaltyAmount = body.PenaltyAmount;
            var startDate = body.StartDate;
            var endDate = body.EndDate;
            var status = body.Status;
            var notes = body.Notes;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                slaCode = LiveWriteFormBinder.Text(form, "slaCode", "sla_code", "code");
                clientName = LiveWriteFormBinder.Text(form, "clientName", "client_name");
                clientId = LiveWriteFormBinder.Int(form, "clientId", "client_id");
                serviceType = LiveWriteFormBinder.Text(form, "serviceType", "service_type");
                responseHours = LiveWriteFormBinder.Dec(form, "responseHours", "response_hours");
                resolutionHours = LiveWriteFormBinder.Dec(form, "resolutionHours", "resolution_hours");
                uptimePct = LiveWriteFormBinder.Dec(form, "uptimePct", "uptime_pct");
                penaltyType = LiveWriteFormBinder.Text(form, "penaltyType", "penalty_type");
                penaltyAmount = LiveWriteFormBinder.Dec(form, "penaltyAmount", "penalty_amount");
                startDate = LiveWriteFormBinder.Text(form, "startDate", "start_date");
                endDate = LiveWriteFormBinder.Text(form, "endDate", "end_date");
                status = LiveWriteFormBinder.Text(form, "status");
                notes = LiveWriteFormBinder.Text(form, "notes");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/cp/crm-tickets-app?tab=sla";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("sla_create", slaCode ?? clientName, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.CreateAsync(
                new ErpSlaCreateRequest(
                    companyId,
                    slaCode,
                    clientName,
                    clientId,
                    serviceType,
                    responseHours,
                    resolutionHours,
                    uptimePct,
                    penaltyType,
                    penaltyAmount,
                    startDate,
                    endDate,
                    status,
                    notes),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                sla_code = slaCode,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpTouristRefundCreateForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpTouristRefundWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!PhpParityDumpCatalog.HasStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/erp/vat-app?tab=tourist_refund",
                    "Admin ERP capability required for tourist refund create.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpTouristRefundCreateBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var invoiceId = body.InvoiceId;
            var invoiceNo = body.InvoiceNo;
            var touristName = body.TouristName;
            var passportNo = body.PassportNo;
            var nationality = body.Nationality;
            var departureDate = body.DepartureDate;
            var totalAmount = body.TotalAmount;
            var vatAmount = body.VatAmount;
            var refundPct = body.RefundPct;
            var refundProvider = body.RefundProvider;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                invoiceId = LiveWriteFormBinder.Long(form, "invoiceId", "invoice_id");
                invoiceNo = LiveWriteFormBinder.Text(form, "invoiceNo", "invoice_no");
                touristName = LiveWriteFormBinder.Text(form, "touristName", "tourist_name");
                passportNo = LiveWriteFormBinder.Text(form, "passportNo", "passport_no");
                nationality = LiveWriteFormBinder.Text(form, "nationality");
                departureDate = LiveWriteFormBinder.Text(form, "departureDate", "departure_date");
                totalAmount = LiveWriteFormBinder.Dec(form, "totalAmount", "total_amount");
                vatAmount = LiveWriteFormBinder.Dec(form, "vatAmount", "vat_amount");
                refundPct = LiveWriteFormBinder.Dec(form, "refundPct", "refund_pct");
                refundProvider = LiveWriteFormBinder.Text(form, "refundProvider", "refund_provider");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/erp/vat-app?tab=tourist_refund";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("tourist_refund_create", touristName, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.CreateAsync(
                new ErpTouristRefundCreateRequest(companyId, invoiceId, invoiceNo, touristName, passportNo, nationality, departureDate, totalAmount, vatAmount, refundPct, refundProvider),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpTouristRefundValidateForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpTouristRefundWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!PhpParityDumpCatalog.HasStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/erp/vat-app?tab=tourist_refund",
                    "Admin ERP capability required for tourist refund validate.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpTouristRefundValidateBody>(context, cancellationToken)
                       ?? new();
            var barcode = body.Barcode;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                barcode = LiveWriteFormBinder.Text(form, "barcode");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/erp/vat-app?tab=tourist_refund";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("tourist_refund_validate", barcode, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.ValidateAsync(new ErpTouristRefundValidateRequest(barcode), cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpRfidRegisterForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpRfidRegisterWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!PhpParityDumpCatalog.HasStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/erp/rfid-app",
                    "Admin ERP capability required for RFID register.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpRfidRegisterBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var rfidEpc = body.RfidEpc;
            var rfidTid = body.RfidTid;
            var productId = body.ProductId;
            var barcode = body.Barcode;
            var sku = body.Sku;
            var itemDescription = body.ItemDescription;
            var warehouseId = body.WarehouseId;
            var locationZone = body.LocationZone;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                rfidEpc = LiveWriteFormBinder.Text(form, "rfidEpc", "rfid_epc", "epc");
                rfidTid = LiveWriteFormBinder.Text(form, "rfidTid", "rfid_tid", "tid");
                productId = LiveWriteFormBinder.Long(form, "productId", "product_id");
                barcode = LiveWriteFormBinder.Text(form, "barcode");
                sku = LiveWriteFormBinder.Text(form, "sku");
                itemDescription = LiveWriteFormBinder.Text(form, "itemDescription", "item_description");
                warehouseId = LiveWriteFormBinder.Long(form, "warehouseId", "warehouse_id");
                locationZone = LiveWriteFormBinder.Text(form, "locationZone", "location_zone", "zone");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/erp/rfid-app";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("rfid_register", rfidEpc, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.RegisterAsync(
                new ErpRfidRegisterRequest(companyId, rfidEpc, rfidTid, productId, barcode, sku, itemDescription, warehouseId, locationZone, session.UserId),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpRfidStartSessionForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpRfidScanWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!PhpParityDumpCatalog.HasStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/erp/rfid-app",
                    "Admin ERP capability required for RFID scan session.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpRfidStartSessionBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var sessionType = body.SessionType;
            var warehouseId = body.WarehouseId;
            var zone = body.Zone;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                sessionType = LiveWriteFormBinder.Text(form, "sessionType", "session_type");
                warehouseId = LiveWriteFormBinder.Long(form, "warehouseId", "warehouse_id");
                zone = LiveWriteFormBinder.Text(form, "zone", "location_zone");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/erp/rfid-app";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("rfid_start_session", sessionType ?? zone, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.StartSessionAsync(
                new ErpRfidStartSessionRequest(companyId, sessionType, warehouseId, zone, session.UserId, session.Email),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpRfidProcessScanForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpRfidScanWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!PhpParityDumpCatalog.HasStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/erp/rfid-app",
                    "Admin ERP capability required for RFID scan.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpRfidProcessScanBody>(context, cancellationToken)
                       ?? new();
            var sessionId = body.SessionId;
            var companyId = body.CompanyId;
            var rfidEpc = body.RfidEpc;
            var rssi = body.Rssi;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                sessionId = LiveWriteFormBinder.Long(form, "sessionId", "session_id");
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                rfidEpc = LiveWriteFormBinder.Text(form, "rfidEpc", "rfid_epc", "epc");
                rssi = LiveWriteFormBinder.Int(form, "rssi", "signal_strength");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/erp/rfid-app";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("rfid_scan", rfidEpc, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.ProcessScanAsync(
                new ErpRfidProcessScanRequest(sessionId, companyId, rfidEpc, rssi),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpGoldRateSetForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpGoldRateSetWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!PhpParityDumpCatalog.HasStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/erp/jewellery-masters-app?tab=gold_rate",
                    "Admin ERP capability required for gold rate set.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpGoldRateSetBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var rateDate = body.RateDate;
            var karat = body.Karat;
            var currency = body.Currency;
            var buyRate = body.BuyRate;
            var sellRate = body.SellRate;
            var unit = body.Unit;
            var source = body.Source;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                rateDate = LiveWriteFormBinder.Text(form, "rateDate", "rate_date");
                karat = LiveWriteFormBinder.Text(form, "karat");
                currency = LiveWriteFormBinder.Text(form, "currency");
                buyRate = LiveWriteFormBinder.Dec(form, "buyRate", "buy_rate");
                sellRate = LiveWriteFormBinder.Dec(form, "sellRate", "sell_rate");
                unit = LiveWriteFormBinder.Text(form, "unit");
                source = LiveWriteFormBinder.Text(form, "source");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/erp/jewellery-masters-app?tab=gold_rate";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("gold_rate_set", karat ?? currency, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SetAsync(
                new ErpGoldRateSetRequest(companyId, rateDate, karat, currency, buyRate, sellRate, unit, source),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAmlKycSaveForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpAmlKycSaveDryRun dryRun,
            IErpAmlKycSaveWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!PhpParityDumpCatalog.HasStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/erp/aml-compliance-app",
                    "Admin ERP capability required for AML KYC save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpAmlKycLiveSaveBody>(context, cancellationToken)
                       ?? new();
            var id = body.Id;
            var companyId = body.CompanyId;
            var customerId = body.CustomerId;
            var customerName = body.CustomerName;
            var idType = body.IdType;
            var idNumber = body.IdNumber;
            var idExpiry = body.IdExpiry;
            var nationality = body.Nationality;
            var riskLevel = body.RiskLevel;
            var pepStatus = body.PepStatus;
            var sanctionsChecked = body.SanctionsChecked;
            var sanctionsMatch = body.SanctionsMatch;
            var verificationStatus = body.VerificationStatus;
            var nextReview = body.NextReview;
            var notes = body.Notes;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id");
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                customerId = LiveWriteFormBinder.Long(form, "customerId", "customer_id");
                customerName = LiveWriteFormBinder.Text(form, "customerName", "customer_name");
                idType = LiveWriteFormBinder.Text(form, "idType", "id_type");
                idNumber = LiveWriteFormBinder.Text(form, "idNumber", "id_number");
                idExpiry = LiveWriteFormBinder.Text(form, "idExpiry", "id_expiry");
                nationality = LiveWriteFormBinder.Text(form, "nationality");
                riskLevel = LiveWriteFormBinder.Text(form, "riskLevel", "risk_level");
                pepStatus = LiveWriteFormBinder.Flag(form, "pepStatus", "pep_status");
                sanctionsChecked = LiveWriteFormBinder.Flag(form, "sanctionsChecked", "sanctions_checked");
                sanctionsMatch = LiveWriteFormBinder.Flag(form, "sanctionsMatch", "sanctions_match");
                verificationStatus = LiveWriteFormBinder.Text(form, "verificationStatus", "verification_status", "status");
                nextReview = LiveWriteFormBinder.Text(form, "nextReview", "next_review");
                notes = LiveWriteFormBinder.Text(form, "notes");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/erp/aml-compliance-app";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpAmlKycSaveRequest(id, customerName, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveAsync(
                new ErpAmlKycSaveWriteRequest(id, companyId, customerId, customerName, idType, idNumber, idExpiry, nationality, riskLevel, pepStatus, sanctionsChecked, sanctionsMatch, verificationStatus, nextReview, notes),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAmlAlertStatusForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpAmlAlertStatusDryRun dryRun,
            IErpAmlAlertStatusWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!PhpParityDumpCatalog.HasStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/erp/aml-compliance-app",
                    "Admin ERP capability required for AML alert status.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpAmlAlertStatusLiveBody>(context, cancellationToken)
                       ?? new();
            var id = body.Id;
            var targetStatus = body.TargetStatus;
            var fileSar = body.FileSar;
            var sarReference = body.SarReference;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "txId", "tx_id", "transaction_id");
                targetStatus = LiveWriteFormBinder.Text(form, "targetStatus", "target_status", "status", "review_status");
                fileSar = LiveWriteFormBinder.Flag(form, "fileSar", "file_sar", "sar_filed");
                sarReference = LiveWriteFormBinder.Text(form, "sarReference", "sar_reference", "sarRef");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/erp/aml-compliance-app";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpAmlAlertStatusRequest(id, targetStatus, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SetStatusAsync(
                new ErpAmlAlertStatusWriteRequest(id, targetStatus, session.UserId, fileSar, sarReference),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpTicketsCreateForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpTicketsWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!PhpParityDumpCatalog.HasStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/crm-tickets-app?tab=tickets",
                    "Admin ERP capability required for ticket create.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpTicketsCreateBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var subject = body.Subject;
            var description = body.Description;
            var category = body.Category;
            var priority = body.Priority;
            var clientId = body.ClientId;
            var clientName = body.ClientName;
            var assignedTo = body.AssignedTo;
            var assignedName = body.AssignedName;
            var slaId = body.SlaId;
            var responseDeadline = body.ResponseDeadline;
            var resolutionDeadline = body.ResolutionDeadline;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                subject = LiveWriteFormBinder.Text(form, "subject");
                description = LiveWriteFormBinder.Text(form, "description");
                category = LiveWriteFormBinder.Text(form, "category");
                priority = LiveWriteFormBinder.Text(form, "priority");
                clientId = LiveWriteFormBinder.Int(form, "clientId", "client_id");
                clientName = LiveWriteFormBinder.Text(form, "clientName", "client_name");
                assignedTo = LiveWriteFormBinder.Int(form, "assignedTo", "assigned_to");
                assignedName = LiveWriteFormBinder.Text(form, "assignedName", "assigned_name");
                slaId = LiveWriteFormBinder.Int(form, "slaId", "sla_id");
                responseDeadline = LiveWriteFormBinder.Text(form, "responseDeadline", "response_deadline");
                resolutionDeadline = LiveWriteFormBinder.Text(form, "resolutionDeadline", "resolution_deadline");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/cp/crm-tickets-app?tab=tickets";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("tickets_create", subject, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.CreateAsync(
                new ErpTicketsCreateRequest(
                    companyId,
                    subject,
                    description,
                    category,
                    priority,
                    clientId,
                    clientName,
                    assignedTo,
                    assignedName,
                    slaId,
                    responseDeadline,
                    resolutionDeadline),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                subject,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpTicketsReplyForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpTicketsWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!PhpParityDumpCatalog.HasStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/crm-tickets-app?tab=tickets",
                    "Admin ERP capability required for ticket reply.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpTicketsReplyBody>(context, cancellationToken)
                       ?? new();
            var ticketId = body.TicketId;
            var authorName = body.AuthorName;
            var authorType = body.AuthorType;
            var message = body.Message;
            var isInternal = body.IsInternal;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                ticketId = LiveWriteFormBinder.Long(form, "ticketId", "ticket_id");
                authorName = LiveWriteFormBinder.Text(form, "authorName", "author_name");
                authorType = LiveWriteFormBinder.Text(form, "authorType", "author_type");
                message = LiveWriteFormBinder.Text(form, "message");
                isInternal = LiveWriteFormBinder.Flag(form, "isInternal", "is_internal");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/cp/crm-tickets-app?tab=tickets";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("tickets_reply", message, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.ReplyAsync(
                new ErpTicketsReplyRequest(ticketId, session.UserId, authorName, authorType, message, isInternal),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpCustomerGroupsCreateForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpCustomerGroupsWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/erp/customer-groups-app",
                    "Admin ERP capability required for customer group create.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpCustomerGroupCreateBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var groupCode = body.GroupCode;
            var groupName = body.GroupName;
            var groupType = body.GroupType;
            var discountPct = body.DiscountPct;
            var creditLimit = body.CreditLimit;
            var paymentTermsDays = body.PaymentTermsDays;
            var priceListId = body.PriceListId;
            var description = body.Description;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                groupCode = LiveWriteFormBinder.Text(form, "groupCode", "group_code", "code");
                groupName = LiveWriteFormBinder.Text(form, "groupName", "group_name", "name");
                groupType = LiveWriteFormBinder.Text(form, "groupType", "group_type");
                discountPct = LiveWriteFormBinder.Dec(form, "discountPct", "discount_pct");
                creditLimit = LiveWriteFormBinder.Dec(form, "creditLimit", "credit_limit");
                paymentTermsDays = LiveWriteFormBinder.Int(form, "paymentTermsDays", "payment_terms_days");
                priceListId = LiveWriteFormBinder.Int(form, "priceListId", "price_list_id");
                description = LiveWriteFormBinder.Text(form, "description");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/erp/customer-groups-app";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("customer_groups_create", groupCode ?? groupName, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.CreateAsync(
                new ErpCustomerGroupCreateRequest(
                    companyId,
                    groupCode,
                    groupName,
                    groupType,
                    discountPct,
                    creditLimit,
                    paymentTermsDays,
                    priceListId,
                    description),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                group_code = groupCode,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpCustomerGroupsAssignForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpCustomerGroupsWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/erp/customer-groups-app",
                    "Admin ERP capability required for customer group assign.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpCustomerGroupAssignBody>(context, cancellationToken)
                       ?? new();
            var groupId = body.GroupId;
            var customerId = body.CustomerId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                groupId = LiveWriteFormBinder.Long(form, "groupId", "group_id");
                customerId = LiveWriteFormBinder.Int(form, "customerId", "customer_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/erp/customer-groups-app";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("customer_groups_assign", groupId.ToString(CultureInfo.InvariantCulture), false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.AssignAsync(new ErpCustomerGroupAssignRequest(groupId, customerId), cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                group_id = groupId,
                customer_id = customerId,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpReportSchedulerCreateForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpReportSchedulerWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/erp/report-scheduler-app",
                    "Admin ERP capability required for report schedule create.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpReportScheduleCreateBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var reportName = body.ReportName;
            var reportType = body.ReportType;
            var frequency = body.Frequency;
            var dayOfWeek = body.DayOfWeek;
            var dayOfMonth = body.DayOfMonth;
            var timeOfDay = body.TimeOfDay;
            var format = body.Format;
            var recipients = body.Recipients;
            var ccRecipients = body.CcRecipients;
            var subjectTemplate = body.SubjectTemplate;
            var bodyTemplate = body.BodyTemplate;
            var filters = body.Filters;
            var createdBy = body.CreatedBy;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                reportName = LiveWriteFormBinder.Text(form, "reportName", "report_name", "name");
                reportType = LiveWriteFormBinder.Text(form, "reportType", "report_type");
                frequency = LiveWriteFormBinder.Text(form, "frequency");
                dayOfWeek = LiveWriteFormBinder.Int(form, "dayOfWeek", "day_of_week");
                dayOfMonth = LiveWriteFormBinder.Int(form, "dayOfMonth", "day_of_month");
                timeOfDay = LiveWriteFormBinder.Text(form, "timeOfDay", "time_of_day");
                format = LiveWriteFormBinder.Text(form, "format");
                recipients = LiveWriteFormBinder.Text(form, "recipients");
                ccRecipients = LiveWriteFormBinder.Text(form, "ccRecipients", "cc_recipients");
                subjectTemplate = LiveWriteFormBinder.Text(form, "subjectTemplate", "subject_template");
                bodyTemplate = LiveWriteFormBinder.Text(form, "bodyTemplate", "body_template");
                filters = LiveWriteFormBinder.Text(form, "filters");
                createdBy = LiveWriteFormBinder.Int(form, "createdBy", "created_by");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/erp/report-scheduler-app";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("report_scheduler_create", reportName, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.CreateAsync(
                new ErpReportScheduleCreateRequest(
                    companyId,
                    reportName,
                    reportType,
                    frequency,
                    dayOfWeek,
                    dayOfMonth,
                    timeOfDay,
                    format,
                    recipients,
                    ccRecipients,
                    subjectTemplate,
                    bodyTemplate,
                    filters,
                    createdBy > 0 ? createdBy : session.UserId),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                report_name = reportName,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpVirtualWarehouseCreateForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpVirtualWarehouseWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!PhpParityDumpCatalog.HasStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/erp/warehouses-app?tab=virtual_warehouse",
                    "Admin ERP capability required for virtual warehouse create.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpVirtualWarehouseCreateBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var code = body.Code;
            var name = body.Name;
            var type = body.Type;
            var address = body.Address;
            var managerId = body.ManagerId;
            var managerName = body.ManagerName;
            var isSellable = body.IsSellable;
            var eventName = body.EventName;
            var eventStart = body.EventStart;
            var eventEnd = body.EventEnd;
            var returnWarehouseId = body.ReturnWarehouseId;
            var notes = body.Notes;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                code = LiveWriteFormBinder.Text(form, "code");
                name = LiveWriteFormBinder.Text(form, "name");
                type = LiveWriteFormBinder.Text(form, "type");
                address = LiveWriteFormBinder.Text(form, "address");
                managerId = LiveWriteFormBinder.Int(form, "managerId", "manager_id");
                managerName = LiveWriteFormBinder.Text(form, "managerName", "manager_name");
                isSellable = LiveWriteFormBinder.Flag(form, "isSellable", "is_sellable") ? 1 : LiveWriteFormBinder.Int(form, "isSellable", "is_sellable");
                if (isSellable == 0 && LiveWriteFormBinder.Text(form, "isSellable", "is_sellable").Length == 0)
                {
                    isSellable = 1;
                }

                eventName = LiveWriteFormBinder.Text(form, "eventName", "event_name");
                eventStart = LiveWriteFormBinder.Text(form, "eventStart", "event_start");
                eventEnd = LiveWriteFormBinder.Text(form, "eventEnd", "event_end");
                returnWarehouseId = LiveWriteFormBinder.Int(form, "returnWarehouseId", "return_warehouse_id");
                notes = LiveWriteFormBinder.Text(form, "notes");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/erp/warehouses-app?tab=virtual_warehouse";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("virtual_warehouse_create", code ?? name, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.CreateAsync(
                new ErpVirtualWarehouseCreateRequest(
                    companyId,
                    code,
                    name,
                    type,
                    address,
                    managerId,
                    managerName,
                    isSellable,
                    eventName,
                    eventStart,
                    eventEnd,
                    returnWarehouseId,
                    notes),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                code,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpVirtualWarehouseTransferForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpVirtualWarehouseWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!PhpParityDumpCatalog.HasStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/erp/warehouses-app?tab=virtual_warehouse",
                    "Admin ERP capability required for virtual warehouse transfer.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpVirtualWarehouseTransferBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var fromWarehouseId = body.FromWarehouseId;
            var toWarehouseId = body.ToWarehouseId;
            var reason = body.Reason;
            var notes = body.Notes;
            var linesJson = body.LinesJson;
            var productId = body.ProductId;
            var sku = body.Sku;
            var barcode = body.Barcode;
            var qty = body.Qty;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                fromWarehouseId = LiveWriteFormBinder.Long(form, "fromWarehouseId", "from_warehouse_id");
                toWarehouseId = LiveWriteFormBinder.Long(form, "toWarehouseId", "to_warehouse_id");
                reason = LiveWriteFormBinder.Text(form, "reason");
                notes = LiveWriteFormBinder.Text(form, "notes");
                linesJson = LiveWriteFormBinder.Text(form, "linesJson", "lines_json", "lines");
                productId = LiveWriteFormBinder.Int(form, "productId", "product_id");
                sku = LiveWriteFormBinder.Text(form, "sku");
                barcode = LiveWriteFormBinder.Text(form, "barcode");
                qty = LiveWriteFormBinder.Dec(form, "qty");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/erp/warehouses-app?tab=virtual_warehouse";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("virtual_warehouse_transfer", fromWarehouseId.ToString(CultureInfo.InvariantCulture), false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.TransferAsync(
                new ErpVirtualWarehouseTransferRequest(
                    companyId,
                    fromWarehouseId,
                    toWarehouseId,
                    reason,
                    notes,
                    session.UserId,
                    linesJson,
                    body.Lines,
                    productId,
                    sku,
                    barcode,
                    qty),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                from_warehouse_id = fromWarehouseId,
                to_warehouse_id = toWarehouseId,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryMetalStockSaveForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwMetalStockWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-stock-verification-app?tab=jw_metal_stock",
                    "Admin ERP capability required for jewellery metal stock save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwMetalStockSaveBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var metal = body.Metal;
            var itemCode = body.ItemCode;
            var description = body.Description;
            var karat = body.Karat;
            var purity = body.Purity;
            var type = body.Type;
            var category = body.Category;
            var mcUnit = body.McUnit;
            var stdCost = body.StdCost;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                metal = LiveWriteFormBinder.Text(form, "metal");
                itemCode = LiveWriteFormBinder.Text(form, "item_code", "itemCode", "code");
                description = LiveWriteFormBinder.Text(form, "description", "name");
                karat = LiveWriteFormBinder.Text(form, "karat");
                purity = LiveWriteFormBinder.Dec(form, "purity");
                type = LiveWriteFormBinder.Text(form, "type");
                category = LiveWriteFormBinder.Text(form, "category");
                mcUnit = LiveWriteFormBinder.Text(form, "mc_unit", "mcUnit");
                stdCost = LiveWriteFormBinder.Dec(form, "std_cost", "stdCost");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_metal_stock_save", itemCode, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(
                        DryRunHtmlForm.SafeReturnUrl(context.Request, "/cp/jewellery-stock-verification-app?tab=jw_metal_stock"),
                        result.ValidationCode == "ok",
                        result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveAsync(
                new ErpJwMetalStockSaveRequest(
                    CompanyId: companyId,
                    Metal: metal,
                    ItemCode: itemCode,
                    Description: description,
                    Karat: karat,
                    Purity: purity,
                    Type: type,
                    Category: category,
                    McUnit: mcUnit,
                    StdCost: stdCost,
                    IncludeStoneWeight: body.IncludeStoneWeight,
                    InPieces: body.InPieces,
                    GstTrnOnMakingStone: body.GstTrnOnMakingStone,
                    ConvFactorOz: body.ConvFactorOz,
                    Price1Code: body.Price1Code,
                    Price1Label: body.Price1Label),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/jewellery-stock-verification-app?tab=jw_metal_stock",
                written.Succeeded,
                written.Message,
                new
                {
                    ok = written.Succeeded,
                    status = written.Succeeded,
                    writes = written.Writes,
                    phpAuthoritative = false,
                    validation_code = written.Code,
                    message = written.Message,
                    id = written.Id,
                    item_code = itemCode,
                    session = SessionPayload(session)
                });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryFixingSaveForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwFixingWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-fixing-app?tab=jw_purchase_fixing",
                    "Admin ERP capability required for jewellery fixing save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwFixingSaveBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var partyCode = body.PartyCode;
            var partyName = body.PartyName;
            var metal = body.Metal;
            var karat = body.Karat;
            var rateType = body.RateType;
            var fixingWt = body.FixingWt != 0 ? body.FixingWt : (body.NetWt != 0 ? body.NetWt : body.FixQtyGms);
            var fixingRate = body.FixingRate != 0 ? body.FixingRate : (body.FixedRate != 0 ? body.FixedRate : body.FixRate);
            var fixingAmount = body.FixingAmount != 0 ? body.FixingAmount : body.FixAmount;
            var refVoucher = !string.IsNullOrWhiteSpace(body.RefVoucher)
                ? body.RefVoucher
                : !string.IsNullOrWhiteSpace(body.Code) ? body.Code : body.ReferenceVoc;
            var narration = !string.IsNullOrWhiteSpace(body.Narration) ? body.Narration : body.Remarks;
            var fixDirection = !string.IsNullOrWhiteSpace(body.FixDirection) ? body.FixDirection : body.FixType;
            var branch = body.Branch;
            var fixDate = body.FixDate;
            var fixNo = body.FixNo;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                partyCode = LiveWriteFormBinder.Text(form, "party_code", "partyCode");
                partyName = LiveWriteFormBinder.Text(form, "party_name", "partyName");
                metal = LiveWriteFormBinder.Text(form, "metal");
                karat = LiveWriteFormBinder.Text(form, "karat");
                rateType = LiveWriteFormBinder.Text(form, "rate_type", "rateType");
                fixingWt = LiveWriteFormBinder.Dec(form, "fixing_wt", "net_wt", "fix_qty_gms", "fixingWt", "netWt", "fixQtyGms");
                fixingRate = LiveWriteFormBinder.Dec(form, "fixing_rate", "fixed_rate", "fix_rate", "fixingRate", "fixedRate", "fixRate");
                fixingAmount = LiveWriteFormBinder.Dec(form, "fixing_amount", "fix_amount", "fixingAmount", "fixAmount");
                refVoucher = LiveWriteFormBinder.Text(form, "ref_voucher", "code", "reference_voc", "refVoucher", "referenceVoc");
                narration = LiveWriteFormBinder.Text(form, "narration", "remarks");
                fixDirection = LiveWriteFormBinder.Text(form, "fix_direction", "fix_type", "fixDirection", "fixType");
                branch = LiveWriteFormBinder.Text(form, "branch");
                fixDate = LiveWriteFormBinder.Text(form, "fix_date", "fixDate");
                fixNo = LiveWriteFormBinder.Int(form, "fix_no", "fixNo");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            var sales = (fixDirection ?? string.Empty).Contains("sale", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fixDirection, "SF", StringComparison.OrdinalIgnoreCase);
            var dryAction = sales ? "jw_sales_fixing_save" : "jw_purchase_fixing_save";
            var returnTab = sales ? "jw_sales_fixing" : "jw_purchase_fixing";
            var returnApp = "/cp/jewellery-fixing-app?tab=" + returnTab;
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest(
                    dryAction,
                    !string.IsNullOrWhiteSpace(partyCode) ? partyCode : refVoucher,
                    false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(
                        DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp),
                        result.ValidationCode == "ok",
                        result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveAsync(
                new ErpJwFixingSaveRequest(
                    CompanyId: companyId,
                    FixType: fixDirection,
                    FixDirection: fixDirection,
                    Branch: branch,
                    FixDate: fixDate,
                    FixNo: fixNo,
                    PartyCode: partyCode,
                    PartyName: partyName,
                    Metal: metal,
                    Karat: karat,
                    RateType: rateType,
                    FixingWt: fixingWt,
                    FixingRate: fixingRate,
                    FixingAmount: fixingAmount,
                    RefVoucher: refVoucher,
                    Narration: narration),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                returnApp,
                written.Succeeded,
                written.Message,
                new
                {
                    ok = written.Succeeded,
                    status = written.Succeeded,
                    writes = written.Writes,
                    phpAuthoritative = false,
                    validation_code = written.Code,
                    message = written.Message,
                    id = written.Id,
                    party_code = partyCode,
                    session = SessionPayload(session)
                });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryVoucherSaveForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwVoucherWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-retail-app?tab=jw_retail_sales",
                    "Admin ERP capability required for jewellery voucher save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwVoucherSaveBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var action = body.Action;
            var vocType = body.VocType;
            var branch = body.Branch;
            var vocDate = body.VocDate;
            var vocNo = body.VocNo;
            var partyCode = body.PartyCode;
            var partyName = body.PartyName;
            var customerName = body.CustomerName;
            var currency = body.Currency;
            var currencyRate = body.CurrencyRate;
            var salesman = body.Salesman;
            var refInvoiceNo = body.RefInvoiceNo;
            var creditDays = body.CreditDays;
            var narration = body.Narration;
            var netAmount = body.NetAmount;
            var vatAmount = body.VatAmount;
            var roundOff = body.RoundOff;
            var grossTotal = body.GrossTotal;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                action = LiveWriteFormBinder.Text(form, "action");
                vocType = LiveWriteFormBinder.Text(form, "voc_type", "vocType");
                branch = LiveWriteFormBinder.Text(form, "branch");
                vocDate = LiveWriteFormBinder.Text(form, "voc_date", "vocDate");
                vocNo = LiveWriteFormBinder.Int(form, "voc_no", "vocNo");
                partyCode = LiveWriteFormBinder.Text(form, "party_code", "partyCode");
                partyName = LiveWriteFormBinder.Text(form, "party_name", "partyName");
                customerName = LiveWriteFormBinder.Text(form, "customer_name", "customerName");
                currency = LiveWriteFormBinder.Text(form, "currency", "party_curr", "partyCurr");
                currencyRate = LiveWriteFormBinder.Dec(form, "currency_rate", "party_curr_rate", "currencyRate");
                salesman = LiveWriteFormBinder.Text(form, "salesman", "code");
                refInvoiceNo = LiveWriteFormBinder.Text(form, "ref_invoice_no", "supp_inv_no", "refInvoiceNo", "repair_ref", "repair_no");
                creditDays = LiveWriteFormBinder.Int(form, "credit_days", "cr_days", "creditDays");
                narration = LiveWriteFormBinder.Text(form, "narration", "remarks");
                netAmount = LiveWriteFormBinder.Dec(form, "net_amount", "netAmount");
                vatAmount = LiveWriteFormBinder.Dec(form, "vat_amount", "vatAmount");
                roundOff = LiveWriteFormBinder.Dec(form, "round_off", "rnd_off_amount", "roundOff");
                grossTotal = LiveWriteFormBinder.Dec(form, "gross_total", "grossTotal");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            var resolvedType = ErpJwVoucherWriteService.NormalizeVocType(vocType, action);
            var dryAction = !string.IsNullOrWhiteSpace(action) ? action : "jw_voucher_save";
            var returnApp = JewelleryVoucherReturnApp(resolvedType);
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest(
                    dryAction,
                    !string.IsNullOrWhiteSpace(resolvedType) ? resolvedType : dryAction,
                    false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(
                        DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp),
                        result.ValidationCode == "ok",
                        result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveAsync(
                new ErpJwVoucherSaveRequest(
                    CompanyId: companyId,
                    Action: action,
                    VocType: vocType,
                    Branch: branch,
                    VocDate: vocDate,
                    VocNo: vocNo,
                    PartyCode: partyCode,
                    PartyName: partyName,
                    CustomerName: customerName,
                    Currency: currency,
                    CurrencyRate: currencyRate,
                    Salesman: salesman,
                    RefInvoiceNo: refInvoiceNo,
                    CreditDays: creditDays,
                    Narration: narration,
                    NetAmount: netAmount,
                    VatAmount: vatAmount,
                    RoundOff: roundOff,
                    GrossTotal: grossTotal),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                returnApp,
                written.Succeeded,
                written.Message,
                new
                {
                    ok = written.Succeeded,
                    status = written.Succeeded,
                    writes = written.Writes,
                    phpAuthoritative = false,
                    validation_code = written.Code,
                    message = written.Message,
                    id = written.Id,
                    voc_type = resolvedType,
                    session = SessionPayload(session)
                });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryPettyCashSaveForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwPettyCashWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/erp/cash-accounts-app?tab=jw_petty_cash",
                    "Admin ERP capability required for jewellery petty cash save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwPettyCashSaveBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var branch = body.Branch;
            var vocDate = body.VocDate;
            var vocNo = body.VocNo;
            var payTo = body.PayTo;
            var paidTo = body.PaidTo;
            var cashAccount = body.CashAccount;
            var accountCode = body.AccountCode;
            var narration = body.Narration;
            var total = body.Total;
            var grandTotal = body.GrandTotal;
            var totalAmount = body.TotalAmount;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                branch = LiveWriteFormBinder.Text(form, "branch");
                vocDate = LiveWriteFormBinder.Text(form, "voc_date", "vocDate");
                vocNo = LiveWriteFormBinder.Int(form, "voc_no", "vocNo");
                payTo = LiveWriteFormBinder.Text(form, "pay_to", "payTo");
                paidTo = LiveWriteFormBinder.Text(form, "paid_to", "paidTo");
                cashAccount = LiveWriteFormBinder.Text(form, "cash_account", "cashAccount");
                accountCode = LiveWriteFormBinder.Text(form, "account_code", "accountCode");
                narration = LiveWriteFormBinder.Text(form, "narration", "remarks");
                total = LiveWriteFormBinder.Dec(form, "total");
                grandTotal = LiveWriteFormBinder.Dec(form, "grand_total", "grandTotal");
                totalAmount = LiveWriteFormBinder.Dec(form, "total_amount", "li_total", "totalAmount");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/erp/cash-accounts-app?tab=jw_petty_cash";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest(
                    "jw_petty_cash_save",
                    !string.IsNullOrWhiteSpace(payTo) ? payTo : paidTo,
                    false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(
                        DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp),
                        result.ValidationCode == "ok",
                        result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveAsync(
                new ErpJwPettyCashSaveRequest(
                    CompanyId: companyId,
                    Branch: branch,
                    VocDate: vocDate,
                    VocNo: vocNo,
                    PayTo: payTo,
                    PaidTo: paidTo,
                    CashAccount: cashAccount,
                    AccountCode: accountCode,
                    Narration: narration,
                    Total: total,
                    GrandTotal: grandTotal,
                    TotalAmount: totalAmount),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                returnApp,
                written.Succeeded,
                written.Message,
                new
                {
                    ok = written.Succeeded,
                    status = written.Succeeded,
                    writes = written.Writes,
                    phpAuthoritative = false,
                    validation_code = written.Code,
                    message = written.Message,
                    id = written.Id,
                    session = SessionPayload(session)
                });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryTouristVatSaveForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwTouristVatWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/uae-tax-compliance-app?tab=jw_tourist_vat",
                    "Admin ERP capability required for jewellery tourist VAT save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwTouristVatSaveBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var branch = body.Branch;
            var refundDate = body.RefundDate;
            var vocDate = body.VocDate;
            var touristName = body.TouristName;
            var passportNo = body.PassportNo;
            var nationality = body.Nationality;
            var mobile = body.Mobile;
            var email = body.Email;
            var flightNo = body.FlightNo;
            var departureDate = body.DepartureDate;
            var invoiceNo = body.InvoiceNo;
            var invoiceDate = body.InvoiceDate;
            var salesman = body.Salesman;
            var narration = body.Narration;
            var totalVat = body.TotalVat;
            var vatAmount = body.VatAmount;
            var totalRefund = body.TotalRefund;
            var refundAmount = body.RefundAmount;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                branch = LiveWriteFormBinder.Text(form, "branch");
                refundDate = LiveWriteFormBinder.Text(form, "refund_date", "refundDate");
                vocDate = LiveWriteFormBinder.Text(form, "voc_date", "vocDate");
                touristName = LiveWriteFormBinder.Text(form, "tourist_name", "touristName");
                passportNo = LiveWriteFormBinder.Text(form, "passport_no", "passportNo");
                nationality = LiveWriteFormBinder.Text(form, "nationality");
                mobile = LiveWriteFormBinder.Text(form, "mobile");
                email = LiveWriteFormBinder.Text(form, "email");
                flightNo = LiveWriteFormBinder.Text(form, "flight_no", "flightNo");
                departureDate = LiveWriteFormBinder.Text(form, "departure_date", "departureDate");
                invoiceNo = LiveWriteFormBinder.Text(form, "invoice_no", "invoiceNo");
                invoiceDate = LiveWriteFormBinder.Text(form, "invoice_date", "invoiceDate");
                salesman = LiveWriteFormBinder.Text(form, "salesman");
                narration = LiveWriteFormBinder.Text(form, "narration", "remarks");
                totalVat = LiveWriteFormBinder.Dec(form, "total_vat", "totalVat");
                vatAmount = LiveWriteFormBinder.Dec(form, "vat_amount", "vatAmount");
                totalRefund = LiveWriteFormBinder.Dec(form, "total_refund", "totalRefund");
                refundAmount = LiveWriteFormBinder.Dec(form, "refund_amount", "refundAmount");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/cp/uae-tax-compliance-app?tab=jw_tourist_vat";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_tourist_vat_save", touristName, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(
                        DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp),
                        result.ValidationCode == "ok",
                        result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveAsync(
                new ErpJwTouristVatSaveRequest(
                    CompanyId: companyId,
                    Branch: branch,
                    RefundDate: refundDate,
                    VocDate: vocDate,
                    TouristName: touristName,
                    PassportNo: passportNo,
                    Nationality: nationality,
                    Mobile: mobile,
                    Email: email,
                    FlightNo: flightNo,
                    DepartureDate: departureDate,
                    InvoiceNo: invoiceNo,
                    InvoiceDate: invoiceDate,
                    Salesman: salesman,
                    Narration: narration,
                    TotalVat: totalVat,
                    VatAmount: vatAmount,
                    TotalRefund: totalRefund,
                    RefundAmount: refundAmount),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                returnApp,
                written.Succeeded,
                written.Message,
                new
                {
                    ok = written.Succeeded,
                    status = written.Succeeded,
                    writes = written.Writes,
                    phpAuthoritative = false,
                    validation_code = written.Code,
                    message = written.Message,
                    id = written.Id,
                    tourist_name = touristName,
                    session = SessionPayload(session)
                });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryRepairReceiptSaveForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwRepairReceiptWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-repairs-app?tab=jw_repair_receipt",
                    "Admin ERP capability required for jewellery repair receipt save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwRepairReceiptSaveBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var branch = body.Branch;
            var receiptDate = body.ReceiptDate;
            var vocDate = body.VocDate;
            var vocNo = body.VocNo;
            var customerCode = body.CustomerCode;
            var customerName = body.CustomerName;
            var mobile = body.Mobile;
            var salesman = body.Salesman;
            var promiseDate = body.PromiseDate;
            var priority = body.Priority;
            var totalEstCost = body.TotalEstCost;
            var advanceAmt = body.AdvanceAmt;
            var narration = body.Narration;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                branch = LiveWriteFormBinder.Text(form, "branch");
                receiptDate = LiveWriteFormBinder.Text(form, "receipt_date", "receiptDate");
                vocDate = LiveWriteFormBinder.Text(form, "voc_date", "vocDate");
                vocNo = LiveWriteFormBinder.Int(form, "voc_no", "vocNo");
                customerCode = LiveWriteFormBinder.Text(form, "customer_code", "customerCode");
                customerName = LiveWriteFormBinder.Text(form, "customer_name", "customerName");
                mobile = LiveWriteFormBinder.Text(form, "mobile", "customer_phone", "customerPhone");
                salesman = LiveWriteFormBinder.Text(form, "salesman");
                promiseDate = LiveWriteFormBinder.Text(form, "promise_date", "promiseDate");
                priority = LiveWriteFormBinder.Text(form, "priority");
                totalEstCost = LiveWriteFormBinder.Dec(form, "total_est_cost", "estimated_cost", "totalEstCost");
                advanceAmt = LiveWriteFormBinder.Dec(form, "advance_amt", "advanceAmt");
                narration = LiveWriteFormBinder.Text(form, "narration", "remarks");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/cp/jewellery-repairs-app?tab=jw_repair_receipt";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_repair_save", customerName, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveAsync(
                new ErpJwRepairReceiptSaveRequest(companyId, branch, receiptDate, vocDate, vocNo, customerCode, customerName, mobile, salesman, promiseDate, priority, totalEstCost, advanceAmt, narration),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryRepairTransferSaveForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwRepairTransferWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-repairs-app?tab=jw_repair_transfer",
                    "Admin ERP capability required for jewellery repair transfer save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwRepairTransferSaveBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var fromBranch = body.FromBranch;
            var branch = body.Branch;
            var toBranch = body.ToBranch;
            var transferDate = body.TransferDate;
            var vocDate = body.VocDate;
            var vocNo = body.VocNo;
            var repairNo = body.RepairNo;
            var code = body.Code;
            var workshopContact = body.WorkshopContact;
            var expectedReturn = body.ExpectedReturn;
            var narration = body.Narration;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                fromBranch = LiveWriteFormBinder.Text(form, "from_branch", "fromBranch");
                branch = LiveWriteFormBinder.Text(form, "branch");
                toBranch = LiveWriteFormBinder.Text(form, "to_branch", "toBranch");
                transferDate = LiveWriteFormBinder.Text(form, "transfer_date", "transferDate");
                vocDate = LiveWriteFormBinder.Text(form, "voc_date", "vocDate");
                vocNo = LiveWriteFormBinder.Int(form, "voc_no", "vocNo");
                repairNo = LiveWriteFormBinder.Text(form, "repair_no", "repairNo");
                code = LiveWriteFormBinder.Text(form, "code");
                workshopContact = LiveWriteFormBinder.Text(form, "workshop_contact", "workshopContact");
                expectedReturn = LiveWriteFormBinder.Text(form, "expected_return", "expectedReturn");
                narration = LiveWriteFormBinder.Text(form, "narration", "remarks");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/cp/jewellery-repairs-app?tab=jw_repair_transfer";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_repair_transfer_save", !string.IsNullOrWhiteSpace(repairNo) ? repairNo : code, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveAsync(
                new ErpJwRepairTransferSaveRequest(companyId, fromBranch, branch, toBranch, transferDate, vocDate, vocNo, repairNo, code, workshopContact, expectedReturn, narration),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryWorkshopReceiveSaveForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwWorkshopReceiveWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-repairs-app?tab=jw_workshop_receive",
                    "Admin ERP capability required for jewellery workshop receive save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwWorkshopReceiveSaveBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var branch = body.Branch;
            var receiveDate = body.ReceiveDate;
            var vocDate = body.VocDate;
            var vocNo = body.VocNo;
            var transferRef = body.TransferRef;
            var code = body.Code;
            var fromWorkshop = body.FromWorkshop;
            var narration = body.Narration;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                branch = LiveWriteFormBinder.Text(form, "branch");
                receiveDate = LiveWriteFormBinder.Text(form, "receive_date", "receiveDate");
                vocDate = LiveWriteFormBinder.Text(form, "voc_date", "vocDate");
                vocNo = LiveWriteFormBinder.Int(form, "voc_no", "vocNo");
                transferRef = LiveWriteFormBinder.Text(form, "transfer_ref", "transferRef");
                code = LiveWriteFormBinder.Text(form, "code");
                fromWorkshop = LiveWriteFormBinder.Text(form, "from_workshop", "fromWorkshop");
                narration = LiveWriteFormBinder.Text(form, "narration", "remarks");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/cp/jewellery-repairs-app?tab=jw_workshop_receive";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_workshop_receive_save", !string.IsNullOrWhiteSpace(transferRef) ? transferRef : code, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveAsync(
                new ErpJwWorkshopReceiveSaveRequest(companyId, branch, receiveDate, vocDate, vocNo, transferRef, code, fromWorkshop, narration),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryRepairDeliverySaveForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwRepairDeliveryWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-repairs-app?tab=jw_repair_delivery",
                    "Admin ERP capability required for jewellery repair delivery save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwRepairDeliverySaveBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var branch = body.Branch;
            var deliveryDate = body.DeliveryDate;
            var vocDate = body.VocDate;
            var vocNo = body.VocNo;
            var repairNo = body.RepairNo;
            var code = body.Code;
            var customerCode = body.CustomerCode;
            var customerName = body.CustomerName;
            var mobile = body.Mobile;
            var totalCharge = body.TotalCharge;
            var advancePaid = body.AdvancePaid;
            var balanceDue = body.BalanceDue;
            var payMode = body.PayMode;
            var amountPaid = body.AmountPaid;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                branch = LiveWriteFormBinder.Text(form, "branch");
                deliveryDate = LiveWriteFormBinder.Text(form, "delivery_date", "deliveryDate");
                vocDate = LiveWriteFormBinder.Text(form, "voc_date", "vocDate");
                vocNo = LiveWriteFormBinder.Int(form, "voc_no", "vocNo");
                repairNo = LiveWriteFormBinder.Text(form, "repair_no", "repairNo");
                code = LiveWriteFormBinder.Text(form, "code");
                customerCode = LiveWriteFormBinder.Text(form, "customer_code", "customerCode");
                customerName = LiveWriteFormBinder.Text(form, "customer_name", "customerName");
                mobile = LiveWriteFormBinder.Text(form, "mobile");
                totalCharge = LiveWriteFormBinder.Dec(form, "total_charge", "totalCharge");
                advancePaid = LiveWriteFormBinder.Dec(form, "advance_paid", "advancePaid");
                balanceDue = LiveWriteFormBinder.Dec(form, "balance_due", "balanceDue");
                payMode = LiveWriteFormBinder.Text(form, "pay_mode", "payMode");
                amountPaid = LiveWriteFormBinder.Dec(form, "amount_paid", "amountPaid");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/cp/jewellery-repairs-app?tab=jw_repair_delivery";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_repair_delivery_save", !string.IsNullOrWhiteSpace(repairNo) ? repairNo : code, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveAsync(
                new ErpJwRepairDeliverySaveRequest(companyId, branch, deliveryDate, vocDate, vocNo, repairNo, code, customerCode, customerName, mobile, totalCharge, advancePaid, balanceDue, payMode, amountPaid),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryStockVerifySaveForm, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpJwModuleSaveDryRun dryRun,
            IErpJwStockVerifyWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return LiveWriteFormBinder.LoginRedirect(
                    context,
                    "/erp/login?returnUrl=/cp/jewellery-stock-verification-app?tab=jw_stock_verification",
                    "Admin ERP capability required for jewellery stock verification save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpJwStockVerifySaveBody>(context, cancellationToken)
                       ?? new();
            var companyId = body.CompanyId;
            var branch = body.Branch;
            var countDate = body.CountDate;
            var vocDate = body.VocDate;
            var vocNo = body.VocNo;
            var division = body.Division;
            var counter = body.Counter;
            var location = body.Location;
            var code = body.Code;
            var supervisor = body.Supervisor;
            var verifiedBy = body.VerifiedBy;
            var narration = body.Narration;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                companyId = LiveWriteFormBinder.Int(form, "companyId", "company_id", "company");
                branch = LiveWriteFormBinder.Text(form, "branch");
                countDate = LiveWriteFormBinder.Text(form, "count_date", "countDate");
                vocDate = LiveWriteFormBinder.Text(form, "voc_date", "vocDate");
                vocNo = LiveWriteFormBinder.Int(form, "voc_no", "vocNo");
                division = LiveWriteFormBinder.Text(form, "division");
                counter = LiveWriteFormBinder.Text(form, "counter");
                location = LiveWriteFormBinder.Text(form, "location");
                code = LiveWriteFormBinder.Text(form, "code");
                supervisor = LiveWriteFormBinder.Text(form, "supervisor");
                verifiedBy = LiveWriteFormBinder.Text(form, "verified_by", "verifiedBy");
                narration = LiveWriteFormBinder.Text(form, "narration", "remarks");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            const string returnApp = "/cp/jewellery-stock-verification-app?tab=jw_stock_verification";
            if (!confirm)
            {
                var result = dryRun.Evaluate(new ErpJwModuleSaveRequest("jw_stock_verification_save", !string.IsNullOrWhiteSpace(location) ? location : code, false));
                if (LiveWriteFormBinder.WantsHtml(context))
                {
                    return DryRunHtmlForm.Redirect(DryRunHtmlForm.SafeReturnUrl(context.Request, returnApp), result.ValidationCode == "ok", result.Detail);
                }

                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            var written = await writes.SaveAsync(
                new ErpJwStockVerifySaveRequest(companyId, branch, countDate, vocDate, vocNo, division, counter, location, code, supervisor, verifiedBy, narration),
                cancellationToken);
            return LiveWriteFormBinder.Complete(context, returnApp, written.Succeeded, written.Message, new
            {
                ok = written.Succeeded,
                status = written.Succeeded,
                writes = written.Writes,
                phpAuthoritative = false,
                validation_code = written.Code,
                message = written.Message,
                id = written.Id,
                session = SessionPayload(session)
            });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryKaratSeedForm, async (HttpContext context, ILegacySessionValidator validator, IErpJwSeedSampleDataDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            var ret = DryRunHtmlForm.SafeReturnUrl(context.Request, "/cp/jewellery-masters-app?tab=jw_karat");
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
                return Results.Redirect("/erp/login");
            var result = dryRun.Evaluate(new ErpJwSeedSampleDataRequest(0, "jw_karat_seed", false));
            return DryRunHtmlForm.Redirect(ret, result.ValidationCode == "ok", result.Detail);
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpJewelleryModuleSaveForm, async (HttpContext context, ILegacySessionValidator validator, IErpJwModuleSaveDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            var ret = DryRunHtmlForm.SafeReturnUrl(context.Request, "/cp/jewellery-masters-app");
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
                return Results.Redirect("/erp/login");
            var action = DryRunHtmlForm.Read(context.Request, "action");
            if (string.IsNullOrWhiteSpace(action)) action = "jw_module_save";
            var code = DryRunHtmlForm.Read(context.Request, "code");
            if (string.IsNullOrWhiteSpace(code)) code = DryRunHtmlForm.Read(context.Request, "karat_code");
            if (string.IsNullOrWhiteSpace(code)) code = DryRunHtmlForm.Read(context.Request, "fix_no");
            if (string.IsNullOrWhiteSpace(code)) code = DryRunHtmlForm.Read(context.Request, "party_code");
            if (string.IsNullOrWhiteSpace(code)) code = DryRunHtmlForm.Read(context.Request, "customer_name");
            var result = dryRun.Evaluate(new ErpJwModuleSaveRequest(action, code, false));
            return DryRunHtmlForm.Redirect(ret, result.ValidationCode == "ok", result.Detail);
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpParityModuleSaveForm, async (HttpContext context, ILegacySessionValidator validator, IErpJwModuleSaveDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            var ret = DryRunHtmlForm.SafeReturnUrl(context.Request, "/erp/module-app");
            if (!PhpParityDumpCatalog.HasStaffAccess(session))
                return Results.Redirect("/erp/login");
            var action = DryRunHtmlForm.Read(context.Request, "action");
            if (string.IsNullOrWhiteSpace(action)) action = "php_parity_save";
            var code = DryRunHtmlForm.Read(context.Request, "code");
            if (string.IsNullOrWhiteSpace(code)) code = DryRunHtmlForm.Read(context.Request, "name");
            var result = dryRun.Evaluate(new ErpJwModuleSaveRequest(action, code, false));
            return DryRunHtmlForm.Redirect(ret, result.ValidationCode == "ok", result.Detail);
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxRbacPrivSave, async (HttpContext context, ErpRbacPrivSaveBody? body, ILegacySessionValidator validator, IErpRbacPrivSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpRbacPrivSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxRbacDutySave, async (HttpContext context, ErpRbacDutySaveBody? body, ILegacySessionValidator validator, IErpRbacDutySaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpRbacDutySaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxRbacDutyPriv, async (HttpContext context, ErpRbacDutyPrivBody? body, ILegacySessionValidator validator, IErpRbacDutyPrivDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpRbacDutyPrivRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });

        endpoints.MapPost(EcomAeRoutes.ErpAjaxPeriodLog, async (HttpContext context, ErpPeriodLogBody? body, ILegacySessionValidator validator, IErpPeriodLogDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPeriodLogRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOplAutoplan, async (HttpContext context, ErpOplAutoplanBody? body, ILegacySessionValidator validator, IErpOplAutoplanDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpOplAutoplanRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOplSeedDemo, async (HttpContext context, ErpOplSeedDemoBody? body, ILegacySessionValidator validator, IErpOplSeedDemoDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpOplSeedDemoRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOplClearDemo, async (HttpContext context, ErpOplClearDemoBody? body, ILegacySessionValidator validator, IErpOplClearDemoDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpOplClearDemoRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPfSetDeptHead, async (HttpContext context, ErpPfSetDeptHeadBody? body, ILegacySessionValidator validator, IErpPfSetDeptHeadDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPfSetDeptHeadRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPfCaseReassign, async (HttpContext context, ErpPfCaseReassignBody? body, ILegacySessionValidator validator, IErpPfCaseReassignDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpPfCaseReassignRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPfCaseCancel, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpPfCaseCancelDryRun dryRun,
            IErpPfCaseCancelWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/process-flow-tasks-app", "Admin ERP capability required for process-flow cancel.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpPfCaseCancelBody>(context, cancellationToken) ?? new(0, false);
            var id = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "caseId", "case_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpPfCaseCancelRequest(id, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.CancelAsync(id, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/process-flow-tasks-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPfSeedDemo, async (HttpContext context, ErpPfSeedDemoBody? body, ILegacySessionValidator validator, IErpPfSeedDemoDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpPfSeedDemoRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPfClearDemo, async (HttpContext context, ErpPfClearDemoBody? body, ILegacySessionValidator validator, IErpPfClearDemoDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpPfClearDemoRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPfSyncOrders, async (HttpContext context, ErpPfSyncOrdersBody? body, ILegacySessionValidator validator, IErpPfSyncOrdersDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpPfSyncOrdersRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxDemoSeedSales, async (HttpContext context, ErpDemoSeedSalesBody? body, ILegacySessionValidator validator, IErpDemoSeedSalesDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpDemoSeedSalesRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxDemoClearSales, async (HttpContext context, ErpDemoClearSalesBody? body, ILegacySessionValidator validator, IErpDemoClearSalesDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpDemoClearSalesRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCtrOcr, async (HttpContext context, ErpCtrOcrBody? body, ILegacySessionValidator validator, IErpCtrOcrDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpCtrOcrRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxDocxSave, async (HttpContext context, ErpDocxSaveBody? body, ILegacySessionValidator validator, IErpDocxSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpDocxSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxDocxDelete, async (HttpContext context, ErpDocxDeleteBody? body, ILegacySessionValidator validator, IErpDocxDeleteDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpDocxDeleteRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxDocxRunReminders, async (HttpContext context, ErpDocxRunRemindersBody? body, ILegacySessionValidator validator, IErpDocxRunRemindersDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpDocxRunRemindersRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInsSave, async (HttpContext context, ErpInsSaveBody? body, ILegacySessionValidator validator, IErpInsSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpInsSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInsDelete, async (HttpContext context, ErpInsDeleteBody? body, ILegacySessionValidator validator, IErpInsDeleteDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpInsDeleteRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInsDocAdd, async (HttpContext context, ErpInsDocAddBody? body, ILegacySessionValidator validator, IErpInsDocAddDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpInsDocAddRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInsDocDelete, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpInsDocDeleteDryRun dryRun,
            IErpInsDocDeleteWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/insurance-compliance-app", "Admin ERP capability required for insurance document delete.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpInsDocDeleteBody>(context, cancellationToken) ?? new(0, false);
            var id = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "docId", "doc_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpInsDocDeleteRequest(id, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.DeleteAsync(id, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/insurance-compliance-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInsClaimAdd, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpInsClaimAddDryRun dryRun,
            EcomAE.Platform.Erp.IErpInsClaimAddWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/insurance-compliance-app", "Admin ERP capability required for insurance claim add.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpInsClaimAddBody>(context, cancellationToken)
                       ?? new();
            var id = body.Id;
            var policyId = body.PolicyId;
            var claimNo = body.ClaimNo;
            var lossDate = body.LossDate;
            var notifiedDate = body.NotifiedDate;
            var deadlineDate = body.DeadlineDate;
            var description = body.Description;
            var claimAmount = body.ClaimAmount;
            var settledAmount = body.SettledAmount;
            var surveyor = body.Surveyor;
            var status = body.Status;
            var note = body.Note;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id");
                policyId = LiveWriteFormBinder.Long(form, "policyId", "policy_id");
                claimNo = LiveWriteFormBinder.Text(form, "claimNo", "claim_no");
                lossDate = LiveWriteFormBinder.Text(form, "lossDate", "loss_date", "loss_date_str");
                notifiedDate = LiveWriteFormBinder.Text(form, "notifiedDate", "notified_date", "notified_date_str");
                deadlineDate = LiveWriteFormBinder.Text(form, "deadlineDate", "deadline_date", "deadline_date_str");
                description = LiveWriteFormBinder.Text(form, "description");
                claimAmount = LiveWriteFormBinder.Dec(form, "claimAmount", "claim_amount");
                settledAmount = LiveWriteFormBinder.Dec(form, "settledAmount", "settled_amount");
                surveyor = LiveWriteFormBinder.Text(form, "surveyor");
                status = LiveWriteFormBinder.Text(form, "status");
                note = LiveWriteFormBinder.Text(form, "note");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SaveAsync(
                    id, policyId, claimNo, lossDate, notifiedDate, deadlineDate, description,
                    claimAmount, settledAmount, surveyor, status, note, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/cp/insurance-compliance-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new ErpInsClaimAddRequest(id, policyId, claimNo, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxFinPeriodsGenerate, async (HttpContext context, ErpFinPeriodsGenerateBody? body, ILegacySessionValidator validator, IErpFinPeriodsGenerateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpFinPeriodsGenerateRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxFinFxRevalue, async (HttpContext context, ErpFinFxRevalueBody? body, ILegacySessionValidator validator, IErpFinFxRevalueDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpFinFxRevalueRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxFinAllocSave, async (HttpContext context, ErpFinAllocSaveBody? body, ILegacySessionValidator validator, IErpFinAllocSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpFinAllocSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxFinAllocRun, async (HttpContext context, ErpFinAllocRunBody? body, ILegacySessionValidator validator, IErpFinAllocRunDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpFinAllocRunRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxFinAccrualSave, async (HttpContext context, ErpFinAccrualSaveBody? body, ILegacySessionValidator validator, IErpFinAccrualSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpFinAccrualSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCollHoldSet, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpCollHoldSetDryRun dryRun,
            IErpCollectionsHoldSetWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/collections-dunning-app", "Admin ERP capability required for collections hold.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpCollHoldSetBody>(context, cancellationToken) ?? new();
            var customerId = body.CustomerId;
            var place = body.Place;
            var reason = body.Reason;
            var actor = body.Actor;
            var companyId = body.CompanyId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                customerId = LiveWriteFormBinder.Long(form, "customerId", "customer_id");
                place = form.ContainsKey("place") || form.ContainsKey("on_hold")
                    ? LiveWriteFormBinder.Flag(form, "place", "on_hold")
                    : true;
                reason = LiveWriteFormBinder.Text(form, "reason");
                actor = LiveWriteFormBinder.Text(form, "actor", "by");
                companyId = LiveWriteFormBinder.Long(form, "companyId", "company_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (string.IsNullOrWhiteSpace(actor))
            {
                actor = session.Email;
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpCollHoldSetRequest(customerId, false, place, reason, actor, companyId)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.SetHoldAsync(
                new ErpCollectionsHoldSetWriteRequest(customerId, place, reason, actor, companyId),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/cp/collections-dunning-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBplanLineAdd, async (HttpContext context, ErpBplanLineAddBody? body, ILegacySessionValidator validator, IErpBplanLineAddDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpBplanLineAddRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBplanPositionAdd, async (HttpContext context, ErpBplanPositionAddBody? body, ILegacySessionValidator validator, IErpBplanPositionAddDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpBplanPositionAddRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxHrtJobSave, async (HttpContext context, ErpHrtJobSaveBody? body, ILegacySessionValidator validator, IErpHrtJobSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpHrtJobSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxHrtApplicantAdd, async (HttpContext context, ErpHrtApplicantAddBody? body, ILegacySessionValidator validator, IErpHrtApplicantAddDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpHrtApplicantAddRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxHrtApplicantStage, async (HttpContext context, ErpHrtApplicantStageBody? body, ILegacySessionValidator validator, IErpHrtApplicantStageDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpHrtApplicantStageRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxHrtReviewSave, async (HttpContext context, ErpHrtReviewSaveBody? body, ILegacySessionValidator validator, IErpHrtReviewSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpHrtReviewSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxHrtGoalAdd, async (HttpContext context, ErpHrtGoalAddBody? body, ILegacySessionValidator validator, IErpHrtGoalAddDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpHrtGoalAddRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxHrtReviewFinalize, async (HttpContext context, ErpHrtReviewFinalizeBody? body, ILegacySessionValidator validator, IErpHrtReviewFinalizeDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpHrtReviewFinalizeRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCftForecastSave, async (HttpContext context, ErpCftForecastSaveBody? body, ILegacySessionValidator validator, IErpCftForecastSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpCftForecastSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCftLineAdd, async (HttpContext context, ErpCftLineAddBody? body, ILegacySessionValidator validator, IErpCftLineAddDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpCftLineAddRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCftInstrumentSave, async (HttpContext context, ErpCftInstrumentSaveBody? body, ILegacySessionValidator validator, IErpCftInstrumentSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpCftInstrumentSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCftInstrumentStatus, HandleCftInstrumentStatusAsync).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpBankInstrumentStatus, HandleCftInstrumentStatusAsync).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpWithholdingCodesSave, HandleWhtCodeSaveAsync).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxWhtCodeSave, HandleWhtCodeSaveAsync).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpWithholdingTxnsRecord, HandleWhtRecordAsync).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxWhtRecord, HandleWhtRecordAsync).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpWithholdingTxnsCertificate, HandleWhtCertificateAsync).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxWhtCertificate, HandleWhtCertificateAsync).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxWhtSettle, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpWhtSettleDryRun dryRun,
            IErpWhtSettleWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/withholding-app", "Admin ERP capability required for withholding settle.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpWhtSettleBody>(context, cancellationToken) ?? new(0, null, false);
            var id = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id", "txnId", "txn_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpWhtSettleRequest(id, body.Code, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.SettleAsync(id, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/withholding-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxErFormatSave, async (HttpContext context, ErpErFormatSaveBody? body, ILegacySessionValidator validator, IErpErFormatSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpErFormatSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxErFieldAdd, async (HttpContext context, ErpErFieldAddBody? body, ILegacySessionValidator validator, IErpErFieldAddDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpErFieldAddRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPrjaBudgetSave, async (HttpContext context, ErpPrjaBudgetSaveBody? body, ILegacySessionValidator validator, IErpPrjaBudgetSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPrjaBudgetSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPrjaTxnAdd, async (HttpContext context, ErpPrjaTxnAddBody? body, ILegacySessionValidator validator, IErpPrjaTxnAddDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPrjaTxnAddRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPrjaRecognize, async (HttpContext context, ErpPrjaRecognizeBody? body, ILegacySessionValidator validator, IErpPrjaRecognizeDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPrjaRecognizeRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCostmItemSet, async (HttpContext context, ErpCostmItemSetBody? body, ILegacySessionValidator validator, IErpCostmItemSetDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpCostmItemSetRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCostmTxnAdd, async (HttpContext context, ErpCostmTxnAddBody? body, ILegacySessionValidator validator, IErpCostmTxnAddDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpCostmTxnAddRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCostmCloseRun, async (HttpContext context, ErpCostmCloseRunBody? body, ILegacySessionValidator validator, IErpCostmCloseRunDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpCostmCloseRunRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxIntgEntitySave, async (HttpContext context, ErpIntgEntitySaveBody? body, ILegacySessionValidator validator, IErpIntgEntitySaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpIntgEntitySaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxIntgSubSave, async (HttpContext context, ErpIntgSubSaveBody? body, ILegacySessionValidator validator, IErpIntgSubSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpIntgSubSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxIntgEventRaise, async (HttpContext context, ErpIntgEventRaiseBody? body, ILegacySessionValidator validator, IErpIntgEventRaiseDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpIntgEventRaiseRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxFyCreate, async (HttpContext context, ErpFyCreateBody? body, ILegacySessionValidator validator, IErpFyCreateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpFyCreateRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxFyClose, async (HttpContext context, ErpFyCloseBody? body, ILegacySessionValidator validator, IErpFyCloseDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpFyCloseRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxFyReopen, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpFyReopenDryRun dryRun,
            IErpFyWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/period-close-app", "Admin ERP capability required for fiscal-year reopen.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpFyReopenBody>(context, cancellationToken) ?? new(0, null, false);
            var id = body.Id;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "year_id", "yearId", "id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpFyReopenRequest(id, body.Code, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.ReopenYearAsync(id, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/period-close-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxFyPeriodStatus, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpFyPeriodStatusDryRun dryRun,
            IErpFyWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/period-close-app", "Admin ERP capability required for period status.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpFyPeriodStatusBody>(context, cancellationToken) ?? new(0, null, false);
            var yearId = body.Id;
            var periodNo = body.PeriodNo;
            var status = body.TargetStatus;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                yearId = LiveWriteFormBinder.Long(form, "year_id", "yearId", "id");
                periodNo = LiveWriteFormBinder.Int(form, "period_no", "periodNo");
                status = LiveWriteFormBinder.Text(form, "status", "targetStatus", "target_status");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(dryRun.Evaluate(new ErpFyPeriodStatusRequest(yearId, status, false)).ToPayload(SessionPayload(session)));
            }

            var written = await writes.SetPeriodStatusAsync(yearId, periodNo, status, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/period-close-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPltJobSave, async (HttpContext context, ErpPltJobSaveBody? body, ILegacySessionValidator validator, IErpPltJobSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPltJobSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPltJobRun, async (HttpContext context, ErpPltJobRunBody? body, ILegacySessionValidator validator, IErpPltJobRunDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpPltJobRunRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPltFeatureSave, async (HttpContext context, ErpPltFeatureSaveBody? body, ILegacySessionValidator validator, IErpPltFeatureSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPltFeatureSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOaPartySave, async (HttpContext context, ErpOaPartySaveBody? body, ILegacySessionValidator validator, IErpOaPartySaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpOaPartySaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOaAddressSave, async (HttpContext context, ErpOaAddressSaveBody? body, ILegacySessionValidator validator, IErpOaAddressSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpOaAddressSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOaContactSave, async (HttpContext context, ErpOaContactSaveBody? body, ILegacySessionValidator validator, IErpOaContactSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpOaContactSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOaCalendarSave, async (HttpContext context, ErpOaCalendarSaveBody? body, ILegacySessionValidator validator, IErpOaCalendarSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpOaCalendarSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxOaHolidayAdd, async (HttpContext context, ErpOaHolidayAddBody? body, ILegacySessionValidator validator, IErpOaHolidayAddDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpOaHolidayAddRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxRbacRoleSave, async (HttpContext context, ErpRbacRoleSaveBody? body, ILegacySessionValidator validator, IErpRbacRoleSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpRbacRoleSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxRbacRoleDuty, async (HttpContext context, ErpRbacRoleDutyBody? body, ILegacySessionValidator validator, IErpRbacRoleDutyDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpRbacRoleDutyRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxRbacUserRole, async (HttpContext context, ErpRbacUserRoleBody? body, ILegacySessionValidator validator, IErpRbacUserRoleDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpRbacUserRoleRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxRtlChannelSave, async (HttpContext context, ErpRtlChannelSaveBody? body, ILegacySessionValidator validator, IErpRtlChannelSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpRtlChannelSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });

        endpoints.MapPost(EcomAeRoutes.ErpInvoicesDelete, async (
            HttpContext context,
            ErpInvoiceDeleteBody? body,
            ILegacySessionValidator validator,
            IErpInvoiceDeleteDryRun dryRun,
            IErpDocLifecycleWriteService lifecycle,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for invoice delete dry-run.");
            }
            body ??= new ErpInvoiceDeleteBody(0, false);
            if (!body.ConfirmWrites)
            {
                var result = await dryRun.EvaluateAsync(
                    new ErpInvoiceDeleteRequest(body.InvoiceId, false),
                    cancellationToken);
                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            return await ExecuteErpWriteAsync(session, async () =>
            {
                await lifecycle.InvoiceDeleteAsync(body.InvoiceId, session.UserId, cancellationToken);
                return ("Draft invoice deleted", (object)new { invoice_id = body.InvoiceId });
            });
        });

        endpoints.MapPost(EcomAeRoutes.ErpCashAccountsCreate, async (
            HttpContext context,
            ErpCashAccountCreateBody? body,
            ILegacySessionValidator validator,
            IErpCashAccountCreateDryRun dryRun,
            IErpGlLedgerWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for cash account create dry-run.");
            }
            body ??= new ErpCashAccountCreateBody(null, "cash", false);
            if (!body.ConfirmWrites)
            {
                return Results.Ok(dryRun
                    .Evaluate(new ErpCashAccountCreateRequest(body.Name, body.AccountType, false))
                    .ToPayload(SessionPayload(session)));
            }

            return await ExecuteErpWriteAsync(session, async () =>
            {
                var accountId = await writes.CreateCashAccountAsync(
                    new ErpCashAccountInput
                    {
                        Name = body.Name ?? string.Empty,
                        AccountType = body.AccountType ?? "cash",
                        BankName = body.BankName ?? string.Empty,
                        AccountNumber = body.AccountNumber ?? string.Empty,
                        CurrencyCode = body.CurrencyCode ?? "AED",
                        OpeningBalance = body.OpeningBalance,
                        OfficeId = body.OfficeId,
                        LegalEntityId = body.LegalEntityId,
                        BusinessUnitId = body.BusinessUnitId,
                        GlAccountId = body.GlAccountId,
                        Iban = body.Iban ?? string.Empty,
                        SwiftBic = body.SwiftBic ?? string.Empty,
                        BankBranch = body.BankBranch ?? string.Empty,
                        RoutingCode = body.RoutingCode ?? string.Empty,
                        Address = body.Address ?? string.Empty,
                        ContactName = body.ContactName ?? string.Empty,
                        ContactPhone = body.ContactPhone ?? string.Empty,
                        ContactEmail = body.ContactEmail ?? string.Empty,
                        Status = body.Status ?? "active",
                        Notes = body.Notes ?? string.Empty,
                    },
                    session.UserId,
                    cancellationToken);
                return ("Account created", (object)new { id = accountId });
            });
        });

        endpoints.MapPost(EcomAeRoutes.ErpCoaAccountsCreate, async (
            HttpContext context,
            ErpCoaCreateBody? body,
            ILegacySessionValidator validator,
            IErpCoaCreateDryRun dryRun,
            IErpGlLedgerWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for COA create dry-run.");
            }
            body ??= new ErpCoaCreateBody(null, null, "expense", false);
            if (!body.ConfirmWrites)
            {
                return Results.Ok(dryRun
                    .Evaluate(new ErpCoaCreateRequest(body.Code, body.Name, body.AccountType, false))
                    .ToPayload(SessionPayload(session)));
            }

            return await ExecuteErpWriteAsync(session, async () =>
            {
                var accountId = await writes.CreateCoaAccountAsync(
                    new ErpCoaAccountInput
                    {
                        Code = body.Code ?? string.Empty,
                        Name = body.Name ?? string.Empty,
                        AccountType = body.AccountType ?? "expense",
                        NormalSide = body.NormalSide ?? string.Empty,
                        ParentId = body.ParentId,
                        OpeningBalance = body.OpeningBalance,
                        Description = body.Description ?? string.Empty,
                    },
                    session.UserId,
                    cancellationToken);
                return ("COA account created", (object)new { id = accountId });
            });
        });

        endpoints.MapPost(EcomAeRoutes.ErpGlJournalsManual, async (
            HttpContext context,
            ErpGlManualEntryBody? body,
            ILegacySessionValidator validator,
            IErpGlManualEntryDryRun dryRun,
            IErpGlLedgerWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for GL manual entry dry-run.");
            }

            body ??= new ErpGlManualEntryBody([], null, null, false);
            if (!body.ConfirmWrites)
            {
                var lines = (body.Lines ?? [])
                    .Select(l => new ErpGlManualLine(l.CoaId, l.Debit, l.Credit, l.LineNote))
                    .ToList();
                var result = await dryRun.EvaluateAsync(
                    new ErpGlManualEntryRequest(lines, body.Reference, body.Description, false),
                    cancellationToken);
                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            return await ExecuteErpWriteAsync(session, async () =>
            {
                var posted = await writes.ManualJournalAsync(
                    new ErpManualJournalInput
                    {
                        Lines = (body.Lines ?? [])
                            .Select(l => new ErpGlLine(l.CoaId, l.Debit, l.Credit, l.LineNote ?? string.Empty))
                            .ToList(),
                        Reference = body.Reference ?? string.Empty,
                        Description = body.Description ?? string.Empty,
                        JournalDate = body.JournalDate,
                    },
                    session.UserId,
                    cancellationToken);
                return (
                    "GL journal posted",
                    (object)new { journal_id = posted.JournalId, journal_no = posted.JournalNo });
            });
        });

        endpoints.MapPost(EcomAeRoutes.ErpGlJournalsReverse, async (
            HttpContext context,
            ErpGlReverseJournalBody? body,
            ILegacySessionValidator validator,
            IErpGlReverseJournalDryRun dryRun,
            IErpGlLedgerWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for GL reverse journal dry-run.");
            }

            body ??= new ErpGlReverseJournalBody(0, null, false);
            if (!body.ConfirmWrites)
            {
                var result = await dryRun.EvaluateAsync(
                    new ErpGlReverseJournalRequest(body.JournalId, body.Note, false),
                    cancellationToken);
                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            return await ExecuteErpWriteAsync(session, async () =>
            {
                var reversal = await writes.ReverseJournalAsync(
                    body.JournalId,
                    body.ReverseDate,
                    body.Note ?? string.Empty,
                    session.UserId,
                    cancellationToken);
                return (
                    "Journal reversed (new journal #" + reversal.JournalId.ToString(CultureInfo.InvariantCulture) + ")",
                    (object)new
                    {
                        journal_id = reversal.JournalId,
                        journal_no = reversal.JournalNo,
                        source_journal_id = reversal.SourceJournalId,
                        source_journal_no = reversal.SourceJournalNo,
                    });
            });
        });

        endpoints.MapPost(EcomAeRoutes.ErpPurchasesVoid, async (
            HttpContext context,
            ErpPurchaseVoidBody? body,
            ILegacySessionValidator validator,
            IErpPurchaseVoidDryRun dryRun,
            IErpDocLifecycleWriteService lifecycle,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for purchase void dry-run.");
            }

            body ??= new ErpPurchaseVoidBody(0, null, false);
            if (!body.ConfirmWrites)
            {
                var result = await dryRun.EvaluateAsync(
                    new ErpPurchaseVoidRequest(body.PurchaseId, body.Reason, false),
                    cancellationToken);
                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            return await ExecuteErpWriteAsync(session, async () =>
            {
                var voided = await lifecycle.PurchaseVoidAsync(
                    body.PurchaseId,
                    body.Reason ?? string.Empty,
                    session.UserId,
                    cancellationToken);
                return (
                    "Purchase invoice voided — reversing journal posted",
                    (object)new { reversal_journal_ids = voided.ReversalJournalIds });
            });
        });

        endpoints.MapPost(EcomAeRoutes.ErpInvoicesCancel, async (
            HttpContext context,
            ErpInvoiceCancelBody? body,
            ILegacySessionValidator validator,
            IErpInvoiceCancelDryRun dryRun,
            IErpDocLifecycleWriteService lifecycle,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for invoice cancel dry-run.");
            }

            body ??= new ErpInvoiceCancelBody(0, null, false);
            if (!body.ConfirmWrites)
            {
                var result = await dryRun.EvaluateAsync(
                    new ErpInvoiceCancelRequest(body.InvoiceId, body.Reason, false),
                    cancellationToken);
                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            return await ExecuteErpWriteAsync(session, async () =>
            {
                await lifecycle.InvoiceCancelAsync(
                    body.InvoiceId,
                    body.Reason ?? string.Empty,
                    session.UserId,
                    cancellationToken);
                return ("Invoice cancelled", (object)new { invoice_id = body.InvoiceId });
            });
        });

        endpoints.MapPost(EcomAeRoutes.ErpSalesOrdersCancel, async (
            HttpContext context,
            ErpSalesOrderCancelBody? body,
            ILegacySessionValidator validator,
            IErpSalesOrderCancelDryRun dryRun,
            IErpDocLifecycleWriteService lifecycle,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for sales-order cancel dry-run.");
            }

            body ??= new ErpSalesOrderCancelBody(0, null, false);
            if (!body.ConfirmWrites)
            {
                var result = await dryRun.EvaluateAsync(
                    new ErpSalesOrderCancelRequest(body.SalesOrderId, body.Reason, false),
                    cancellationToken);
                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            return await ExecuteErpWriteAsync(session, async () =>
            {
                await lifecycle.SalesOrderCancelAsync(
                    body.SalesOrderId,
                    body.Reason ?? string.Empty,
                    session.UserId,
                    cancellationToken);
                return ("Sales order cancelled", (object)new { sales_order_id = body.SalesOrderId });
            });
        });

        endpoints.MapPost(EcomAeRoutes.ErpPurchaseOrdersDelete, async (
            HttpContext context,
            ErpPoDeleteBody? body,
            ILegacySessionValidator validator,
            IErpPoDeleteDryRun dryRun,
            IErpPurchaseOrderWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for PO delete.");
            }

            body ??= new ErpPoDeleteBody(0, false);
            if (!body.ConfirmWrites)
            {
                var result = await dryRun.EvaluateAsync(
                    new ErpPoDeleteRequest(body.PurchaseOrderId, false),
                    cancellationToken);
                return Results.Ok(result.ToPayload(SessionPayload(session)));
            }

            return await ExecuteErpWriteAsync(session, async () =>
            {
                await writes.DeleteAsync(body.PurchaseOrderId, session.UserId, cancellationToken);
                return ("Draft purchase order deleted", new { id = body.PurchaseOrderId });
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
            int? limit,
            int? warehouseId,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for inventory-stock digest.");
            }

            var result = await dashboards.BuildErpInventoryStockDigestAsync(limit ?? 200, warehouseId, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary = result.Summary,
                stock = result.Stock,
                lowStock = result.LowStock,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only ERP inventory stock KPIs + on-hand/low-stock rows (epc_erp_inventory_stock_report / low_stock_lines). PHP epc_erp_inv_stock remains authoritative."
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

        endpoints.MapGet(EcomAeRoutes.ErpProcessFlowTasks, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for process-flow-tasks digest.");
            }

            var result = await dashboards.BuildErpProcessFlowTasksDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary = result.Summary,
                tasks = result.Tasks,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_pf_cases KPIs + tasks (comments/step detail omitted). PHP epc_erp_processflow.php remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpWorkflowTasks, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for workflow-tasks digest.");
            }

            var result = await dashboards.BuildErpWorkflowTasksDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary = result.Summary,
                tasks = result.Tasks,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "epc_erp_workflow_tasks. Create on POST /erp/workflow/create when confirmWrites=true. Staff schema seed stays PHP."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpVatReturn, async (
            HttpContext context,
            long? from,
            long? to,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for vat-return digest.");
            }

            var result = await dashboards.BuildErpVatReturnDigestAsync(from, to, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary = result.Summary,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only operational VAT 201 boxes from shop_orders + epc_erp_purchases. FTA filing stays PHP."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpWithholding, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for withholding digest.");
            }

            var result = await dashboards.BuildErpWithholdingDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary = result.Summary,
                codes = result.Codes,
                txns = result.Txns,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_wht_code + epc_wht_txn digest. Settle, code save, record, and certificate are ASP.NET-live; schema ensure stays PHP."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpPettyCash, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for petty-cash digest.");
            }

            var result = await dashboards.ListErpPettyCashAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                floats = result.Floats,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_erp_petty_cash floats. PHP petty_cash tab remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpCashForecast, async (
            HttpContext context,
            int? limit,
            long? forecastId,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for cash-forecast digest.");
            }

            var result = await dashboards.BuildErpCashForecastDigestAsync(limit ?? 200, forecastId, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary = result.Summary,
                forecasts = result.Forecasts,
                lines = result.Lines,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_cft_forecast + lines. PHP cash_forecast tab remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpBankInstruments, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for bank-instruments digest.");
            }

            var result = await dashboards.BuildErpBankInstrumentsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary = result.Summary,
                instruments = result.Instruments,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_cft_instrument. PHP bank_instruments tab remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpSubscriptions, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for subscriptions digest.");
            }

            var result = await dashboards.BuildErpSubscriptionsDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary = result.Summary,
                subscriptions = result.Subscriptions,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "epc_erp_subscriptions + MRR/ARR. Save on POST /erp/subscriptions/save when confirmWrites=true. Cycle generate stays PHP."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpSupplierPortal, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for supplier-portal digest.");
            }

            var result = await dashboards.BuildErpSupplierPortalDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary = result.Summary,
                cards = result.Cards,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only supplier scorecards (PO/RFQ/payables). PHP supplier_portal tab remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpVirtualWarehouses, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for virtual-warehouses digest.");
            }

            var result = await dashboards.BuildErpVirtualWarehouseDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                locationCount = result.LocationCount,
                locations = result.Locations,
                transfers = result.Transfers,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read digest over warehouse locations + transfer history. Virtual-warehouse create/transfer are live when confirmed."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpStaff, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for staff digest.");
            }

            var result = await dashboards.ListErpStaffAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                staff = result.Staff,
                count = result.Count,
                activeCount = result.ActiveCount,
                departmentCount = result.DepartmentCount,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_erp_staff_profiles. PHP staff tab / ajax_erp.php remain authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpContracts, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for contracts digest.");
            }

            var result = await dashboards.ListErpContractsAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                contracts = result.Contracts,
                count = result.Count,
                activeCount = result.ActiveCount,
                valueTotal = result.ValueTotal,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "epc_erp_contracts (body/OCR omitted). Save on POST /erp/contracts/save when confirmWrites=true. Sign and OCR stay PHP."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpOpening, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for opening digest.");
            }

            var result = await dashboards.ListErpOpeningBatchesAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                batches = result.Batches,
                count = result.Count,
                postedCount = result.PostedCount,
                debitTotal = result.DebitTotal,
                creditTotal = result.CreditTotal,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_erp_opening_batches. PHP opening tab remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpMarketing, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for marketing digest.");
            }

            var result = await dashboards.ListErpMarketingCampaignsAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                campaigns = result.Campaigns,
                count = result.Count,
                activeCount = result.ActiveCount,
                budgetTotal = result.BudgetTotal,
                leadTotal = result.LeadTotal,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "epc_erp_marketing_campaigns. Create on POST /erp/marketing/create when confirmWrites=true. Staff schema seed stays PHP."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpPayroll, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for payroll digest.");
            }

            var result = await dashboards.ListErpPayrollRunsAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                runs = result.Runs,
                count = result.Count,
                paidCount = result.PaidCount,
                grossTotal = result.GrossTotal,
                netTotal = result.NetTotal,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_erp_payroll_runs. PHP payroll tab remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpPrintTemplates, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for print-templates digest.");
            }

            var result = await dashboards.ListErpPrintTemplatesAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                templates = result.Templates,
                count = result.Count,
                defaultCount = result.DefaultCount,
                docTypeCount = result.DocTypeCount,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_erp_print_templates (HTML/CSS omitted). PHP print_designer tab remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpOrderPlanning, async (HttpContext context, int? limit, ILegacySessionValidator validator, ISurfaceDashboardSummaryReporter dashboards, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for order-planning digest.");
            var result = await dashboards.BuildErpOrderPlanningDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new { ok = true, surface = "erp", recommendations = result.Recommendations, @params = result.Params, count = result.Count, pendingCount = result.PendingCount, pendingValue = result.PendingValue, source = result.Source, message = result.Message, session = SessionPayload(session), note = "Read-only epc_erp_order_recommendations + planning params. PHP order_planning remains authoritative." });
        });

        endpoints.MapGet(EcomAeRoutes.ErpProcurementCategories, async (HttpContext context, int? limit, ILegacySessionValidator validator, ISurfaceDashboardSummaryReporter dashboards, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for procurement-categories digest.");
            var result = await dashboards.BuildErpProcurementCategoriesDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new { ok = true, surface = "erp", categories = result.Categories, policies = result.Policies, count = result.Count, activeCount = result.ActiveCount, policyCount = result.PolicyCount, source = result.Source, message = result.Message, session = SessionPayload(session), note = "Read-only epc_proc_category + epc_proc_policy. PHP procurement_categories remains authoritative." });
        });

        endpoints.MapGet(EcomAeRoutes.ErpQuality, async (HttpContext context, int? limit, ILegacySessionValidator validator, ISurfaceDashboardSummaryReporter dashboards, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for quality digest.");
            var result = await dashboards.BuildErpQualityDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new { ok = true, surface = "erp", plans = result.Plans, orders = result.Orders, ncrs = result.Ncrs, count = result.Count, openOrderCount = result.OpenOrderCount, openNcrCount = result.OpenNcrCount, source = result.Source, message = result.Message, session = SessionPayload(session), note = "Read-only epc_qm_plan/order/ncr. PHP quality tab remains authoritative." });
        });

        endpoints.MapGet(EcomAeRoutes.ErpRfid, async (HttpContext context, int? limit, ILegacySessionValidator validator, ISurfaceDashboardSummaryReporter dashboards, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for rfid digest.");
            var result = await dashboards.BuildErpRfidDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new { ok = true, surface = "erp", tags = result.Tags, sessions = result.Sessions, count = result.Count, activeTagCount = result.ActiveTagCount, sessionCount = result.SessionCount, source = result.Source, message = result.Message, session = SessionPayload(session), note = "Read-only epc_rfid_tags + scan sessions. PHP rfid tab remains authoritative." });
        });

        endpoints.MapGet(EcomAeRoutes.ErpRecruitment, async (HttpContext context, int? limit, ILegacySessionValidator validator, ISurfaceDashboardSummaryReporter dashboards, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for recruitment digest.");
            var result = await dashboards.BuildErpRecruitmentDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new { ok = true, surface = "erp", jobs = result.Jobs, applicants = result.Applicants, count = result.Count, openJobCount = result.OpenJobCount, applicantCount = result.ApplicantCount, source = result.Source, message = result.Message, session = SessionPayload(session), note = "Read-only epc_hrt_job + applicants. PHP recruitment tab remains authoritative." });
        });

        endpoints.MapGet(EcomAeRoutes.ErpCustomerGroups, async (HttpContext context, int? limit, ILegacySessionValidator validator, ISurfaceDashboardSummaryReporter dashboards, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for customer-groups digest.");
            var result = await dashboards.ListErpCustomerGroupsAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new { ok = true, surface = "erp", groups = result.Groups, count = result.Count, activeCount = result.ActiveCount, memberTotal = result.MemberTotal, source = result.Source, message = result.Message, session = SessionPayload(session), note = "Read digest over epc_customer_groups. Create/assign are live when confirmed." });
        });

        endpoints.MapGet(EcomAeRoutes.ErpPerformance, async (HttpContext context, int? limit, ILegacySessionValidator validator, ISurfaceDashboardSummaryReporter dashboards, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for performance digest.");
            var result = await dashboards.BuildErpPerformanceDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new { ok = true, surface = "erp", reviews = result.Reviews, goals = result.Goals, count = result.Count, openCount = result.OpenCount, doneCount = result.DoneCount, source = result.Source, message = result.Message, session = SessionPayload(session), note = "Read-only epc_hrt_review + goals. PHP performance tab remains authoritative." });
        });

        endpoints.MapGet(EcomAeRoutes.ErpProductInfo, async (HttpContext context, int? limit, ILegacySessionValidator validator, ISurfaceDashboardSummaryReporter dashboards, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for product-info digest.");
            var result = await dashboards.BuildErpProductInfoDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new { ok = true, surface = "erp", items = result.Items, fieldDefs = result.FieldDefs, variants = result.Variants, count = result.Count, activeCount = result.ActiveCount, fieldCount = result.FieldCount, source = result.Source, message = result.Message, session = SessionPayload(session), note = "Read-only epc_erp_inv_items + field defs + variants. PHP product_info remains authoritative." });
        });

        endpoints.MapGet(EcomAeRoutes.ErpReportScheduler, async (HttpContext context, int? limit, ILegacySessionValidator validator, ISurfaceDashboardSummaryReporter dashboards, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for report-scheduler digest.");
            var result = await dashboards.BuildErpReportSchedulerDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new { ok = true, surface = "erp", schedules = result.Schedules, count = result.Count, activeCount = result.ActiveCount, source = result.Source, message = result.Message, session = SessionPayload(session), note = "Read digest over epc_report_schedules (recipients/body omitted). Create is live when confirmed; send/email stay Classic." });
        });

        endpoints.MapGet(EcomAeRoutes.ErpProjectAccounting, async (HttpContext context, int? limit, ILegacySessionValidator validator, ISurfaceDashboardSummaryReporter dashboards, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for project-accounting digest.");
            var result = await dashboards.BuildErpProjectAccountingDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new { ok = true, surface = "erp", budgets = result.Budgets, txns = result.Txns, recognitions = result.Recognitions, count = result.Count, txnCount = result.TxnCount, recognitionCount = result.RecognitionCount, source = result.Source, message = result.Message, session = SessionPayload(session), note = "Read-only epc_prja_budget/txn/recognition. PHP project_accounting remains authoritative." });
        });

        endpoints.MapGet(EcomAeRoutes.ErpDocAttachments, async (HttpContext context, int? limit, ILegacySessionValidator validator, ISurfaceDashboardSummaryReporter dashboards, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for doc-attachments digest.");
            var result = await dashboards.ListErpDocAttachmentsAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new { ok = true, surface = "erp", attachments = result.Attachments, count = result.Count, entityTypeCount = result.EntityTypeCount, source = result.Source, message = result.Message, session = SessionPayload(session), note = "Read-only epc_doc_attachments (file_path omitted). PHP doc_attachment remains authoritative." });
        });

        endpoints.MapGet(EcomAeRoutes.ErpInventoryReport, async (HttpContext context, int? limit, ILegacySessionValidator validator, ISurfaceDashboardSummaryReporter dashboards, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for inventory-report digest.");
            var result = await dashboards.BuildErpInventoryReportDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new { ok = true, surface = "erp", categories = result.Categories, snapshots = result.Snapshots, count = result.Count, snapshotCount = result.SnapshotCount, totalValue = result.TotalValue, source = result.Source, message = result.Message, session = SessionPayload(session), note = "Read-only epc_inventory_categories + snapshots. PHP inventory_report remains authoritative." });
        });

        endpoints.MapGet(EcomAeRoutes.ErpOrderPipeline, async (HttpContext context, int? limit, ILegacySessionValidator validator, ISurfaceDashboardSummaryReporter dashboards, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for order-pipeline digest.");
            var result = await dashboards.ListErpOrderPipelineLogAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new { ok = true, surface = "erp", logs = result.Logs, count = result.Count, successCount = result.SuccessCount, failedCount = result.FailedCount, pendingCount = result.PendingCount, avgDurationMs = result.AvgDurationMs, source = result.Source, message = result.Message, session = SessionPayload(session), note = "Read-only epc_order_erp_log. PHP order→ERP pipeline remains authoritative." });
        });

        endpoints.MapGet(EcomAeRoutes.ErpInventoryForecast, async (HttpContext context, int? limit, ILegacySessionValidator validator, ISurfaceDashboardSummaryReporter dashboards, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for inventory-forecast digest.");
            var result = await dashboards.ListErpInventoryForecastAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new { ok = true, surface = "erp", forecasts = result.Forecasts, count = result.Count, healthyCount = result.HealthyCount, lowCount = result.LowCount, criticalCount = result.CriticalCount, stockoutCount = result.StockoutCount, source = result.Source, message = result.Message, session = SessionPayload(session), note = "Read-only epc_inventory_forecast. POST /erp/inventory-forecast/recompute is the ASP.NET live twin of epc_forecast_compute." });
        });

        endpoints.MapPost(EcomAeRoutes.ErpInventoryForecastRecompute, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpInventoryForecastWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/inventory-forecast-app", "Admin ERP capability required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpInventoryForecastRecomputeBody>(context, cancellationToken) ?? new();
            var siteKey = body.SiteKey ?? string.Empty;
            var sku = body.Sku ?? string.Empty;
            var stock = body.CurrentStock;
            var name = body.ProductName ?? string.Empty;
            var lead = body.LeadTimeDays;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                siteKey = LiveWriteFormBinder.Text(form, "siteKey", "site_key");
                sku = LiveWriteFormBinder.Text(form, "sku");
                stock = LiveWriteFormBinder.Int(form, "currentStock", "current_stock");
                name = LiveWriteFormBinder.Text(form, "productName", "product_name", "name");
                lead = LiveWriteFormBinder.Int(form, "leadTimeDays", "lead_time_days");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    ok = false,
                    status = false,
                    surface = "erp",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = false,
                    message = "Set confirmWrites=true to recompute the forecast on ASP.NET.",
                    session = SessionPayload(session),
                });
            }

            var written = await writes.RecomputeSkuAsync(siteKey, sku, stock, name, lead, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/erp/inventory-forecast-app",
                written.Succeeded,
                written.Message,
                new
                {
                    ok = written.Succeeded,
                    status = written.Succeeded,
                    surface = "erp",
                    writes = written.Writes,
                    writesBlocked = false,
                    phpAuthoritative = false,
                    validation_code = written.Code,
                    message = written.Message,
                    session = SessionPayload(session),
                });
        }).DisableAntiforgery();

        endpoints.MapGet(EcomAeRoutes.ErpMultiEntity, async (HttpContext context, int? limit, ILegacySessionValidator validator, ISurfaceDashboardSummaryReporter dashboards, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for multi-entity digest.");
            var result = await dashboards.ListErpMultiEntityAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new { ok = true, surface = "erp", groups = result.Groups, intercompany = result.Intercompany, count = result.Count, memberTotal = result.MemberTotal, icTxnCount = result.IcTxnCount, pendingIcCount = result.PendingIcCount, source = result.Source, message = result.Message, session = SessionPayload(session), note = "epc_entity_groups + epc_intercompany_txns. Group/member/IC/eliminate on POST /erp/multi-entity/write when confirmWrites=true. ajax_erp multi_entity_save stays dry-run." });
        });
        endpoints.MapPost(EcomAeRoutes.ErpMultiEntityWrite, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpMultiEntityWriteDryRun dryRun,
            IErpMultiEntityWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/multi-entity-app", "Admin ERP capability required for multi-entity write.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpMultiEntityWriteBody>(context, cancellationToken)
                       ?? new();
            var action = body.Action;
            var groupId = body.GroupId;
            var groupCode = body.GroupCode;
            var groupName = body.GroupName;
            var parentEntity = body.ParentEntity;
            var baseCurrency = body.BaseCurrency;
            var fiscalYearEnd = body.FiscalYearEnd;
            var siteKey = body.SiteKey;
            var entityName = body.EntityName;
            var ownershipPct = body.OwnershipPct;
            var localCurrency = body.LocalCurrency;
            var consolidation = body.Consolidation;
            var fromSiteKey = body.FromSiteKey;
            var toSiteKey = body.ToSiteKey;
            var amount = body.Amount;
            var description = body.Description;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                groupId = LiveWriteFormBinder.Long(form, "groupId", "group_id", "id");
                groupCode = LiveWriteFormBinder.Text(form, "groupCode", "group_code");
                groupName = LiveWriteFormBinder.Text(form, "groupName", "group_name", "name");
                parentEntity = LiveWriteFormBinder.Text(form, "parentEntity", "parent_entity");
                baseCurrency = LiveWriteFormBinder.Text(form, "baseCurrency", "base_currency");
                fiscalYearEnd = LiveWriteFormBinder.Text(form, "fiscalYearEnd", "fiscal_year_end");
                siteKey = LiveWriteFormBinder.Text(form, "siteKey", "site_key");
                entityName = LiveWriteFormBinder.Text(form, "entityName", "entity_name");
                ownershipPct = LiveWriteFormBinder.Dec(form, "ownershipPct", "ownership_pct");
                localCurrency = LiveWriteFormBinder.Text(form, "localCurrency", "local_currency");
                consolidation = LiveWriteFormBinder.Text(form, "consolidation");
                fromSiteKey = LiveWriteFormBinder.Text(form, "fromSiteKey", "from_site_key", "from");
                toSiteKey = LiveWriteFormBinder.Text(form, "toSiteKey", "to_site_key", "to");
                amount = LiveWriteFormBinder.Dec(form, "amount");
                description = LiveWriteFormBinder.Text(form, "description", "desc");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var key = (action ?? string.Empty).Trim();
                ErpSimpleWriteResult written = key switch
                {
                    "create_group" or "create-group" =>
                        await writes.CreateGroupAsync(groupCode, groupName, parentEntity, baseCurrency, fiscalYearEnd, cancellationToken),
                    "add_member" or "add-member" =>
                        await writes.AddMemberAsync(groupId, siteKey, entityName, ownershipPct, localCurrency, consolidation, cancellationToken),
                    "record_intercompany" or "record-intercompany" or "record_ic" =>
                        await writes.RecordIntercompanyAsync(groupId, fromSiteKey, toSiteKey, amount, description, cancellationToken),
                    "eliminate" =>
                        await writes.EliminateAsync(groupId, cancellationToken),
                    _ => ErpSimpleWriteResult.Fail("invalid", "Unknown multi-entity action. create_group / add_member / record_intercompany / eliminate are live; ajax_erp multi_entity_save stays PHP."),
                };
                return LiveWriteFormBinder.Complete(
                    context,
                    "/erp/multi-entity-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new ErpMultiEntityWriteRequest(action, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapGet(EcomAeRoutes.ErpMultiCurrencyGl, async (HttpContext context, int? limit, ILegacySessionValidator validator, ISurfaceDashboardSummaryReporter dashboards, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for multi-currency-gl digest.");
            var result = await dashboards.ListErpMultiCurrencyGlAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new { ok = true, surface = "erp", rates = result.Rates, entries = result.Entries, count = result.Count, entryCount = result.EntryCount, unrevaluedCount = result.UnrevaluedCount, revalGainLossTotal = result.RevalGainLossTotal, source = result.Source, message = result.Message, session = SessionPayload(session), note = "epc_fx_rates + epc_gl_currency_entries. Rate UPSERT on POST /erp/multi-currency-gl/set-rate when confirmWrites=true. Revaluation, journal, and seed stay PHP." });
        });
        endpoints.MapPost(EcomAeRoutes.ErpMultiCurrencyGlSetRate, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IErpMultiCurrencyGlWriteDryRun dryRun,
            IErpMultiCurrencyGlWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/multi-currency-gl-app", "Admin ERP capability required for FX rate write.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpMultiCurrencyGlSetRateBody>(context, cancellationToken)
                       ?? new();
            var action = body.Action;
            var baseCurrency = body.BaseCurrency;
            var targetCurrency = body.TargetCurrency;
            var rate = body.Rate;
            var effectiveDate = body.EffectiveDate;
            var source = body.Source;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                action = LiveWriteFormBinder.Text(form, "action");
                baseCurrency = LiveWriteFormBinder.Text(form, "baseCurrency", "base_currency", "base");
                targetCurrency = LiveWriteFormBinder.Text(form, "targetCurrency", "target_currency", "target");
                rate = LiveWriteFormBinder.Dec(form, "rate");
                effectiveDate = LiveWriteFormBinder.Text(form, "effectiveDate", "effective_date", "date");
                source = LiveWriteFormBinder.Text(form, "source");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var key = (action ?? string.Empty).Trim();
                if (key.Length == 0 || key is "set_rate" or "set-rate")
                {
                    var written = await writes.SetRateAsync(baseCurrency, targetCurrency, rate, effectiveDate, source, cancellationToken);
                    return LiveWriteFormBinder.Complete(
                        context,
                        "/erp/multi-currency-gl-app",
                        written.Succeeded,
                        written.Message,
                        new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
                }

                return LiveWriteFormBinder.Complete(
                    context,
                    "/erp/multi-currency-gl-app",
                    false,
                    "Unknown multi-currency GL action. set_rate is live; revaluation, journal, and seed stay PHP.",
                    new { ok = false, writes = 0, phpAuthoritative = false, validation_code = "invalid", message = "Unknown multi-currency GL action. set_rate is live; revaluation, journal, and seed stay PHP.", session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new ErpMultiCurrencyGlWriteRequest(action ?? "set_rate", false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapGet(EcomAeRoutes.ErpReportCenter, async (
            HttpContext context,
            string? key,
            int? limit,
            int? companyId,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for report-center digest.");
            }

            var result = await dashboards.BuildErpReportCenterDigestAsync(key, limit ?? 100, cancellationToken, companyId);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary = result.Summary,
                reports = result.Reports,
                columns = result.Columns,
                rows = result.Rows,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_rc_registry mirror (+ optional table/computed peek). PHP epc_erp_report_center.php CSV/export remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpAging, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for aging digest.");
            }

            var result = await dashboards.BuildErpAgingDigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary = result.Summary,
                arLabels = result.ArLabels,
                apLabels = result.ApLabels,
                inventoryLabels = result.InventoryLabels,
                arRows = result.ArRows,
                apRows = result.ApRows,
                inventoryRows = result.InventoryRows,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only AR/AP/inventory aging (epc_erp_aging.php). Interactive aging UX remains PHP."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpReceivables, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for receivables digest.");
            }

            var result = await dashboards.BuildErpReceivablesDigestAsync(limit ?? 300, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary = result.Summary,
                customers = result.Customers,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_erp_receivables customer balances. Writes remain PHP."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpStockMovements, async (
            HttpContext context,
            int? limit,
            int? itemId,
            int? warehouseId,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for stock-movements digest.");
            }

            var result = await dashboards.BuildErpInventoryMovementsDigestAsync(limit ?? 200, itemId, warehouseId, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                summary = result.Summary,
                movements = result.Movements,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only inventory movement ledger (epc_erp_inventory_ledger). Writes remain PHP."
            });
        });


        endpoints.MapGet(EcomAeRoutes.ErpDeliveryNotes, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for delivery-notes digest.");
            }

            var result = await dashboards.ListErpDeliveryNotesAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                notes = result.Notes,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only ERP delivery notes digest. PHP epc_erp_delivery_notes remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpRfqs, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for RFQ digest.");
            }

            var result = await dashboards.ListErpRfqsAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                rfqs = result.Rfqs,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only ERP RFQ digest. PHP epc_erp_rfq remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpThreeWayMatch, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for three-way-match digest.");
            }

            var result = await dashboards.ListErpThreeWayMatchAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                rows = result.Rows,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only ERP three-way match digest. PHP epc_erp_three_way_match_rows remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpContacts, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for contacts digest.");
            }

            var result = await dashboards.ListErpContactsAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                contacts = result.Contacts,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only ERP contacts digest. PHP epc_erp_contacts remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpPaymentBatches, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for payment-batches digest.");
            }

            var result = await dashboards.ListErpPaymentBatchesAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                batches = result.Batches,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only ERP payment batches digest. PHP epc_erp_payment_batches remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpFiscalPeriods, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for fiscal-periods digest.");
            }

            var result = await dashboards.ListErpFiscalPeriodsAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                periods = result.Periods,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only ERP fiscal periods digest. PHP period_list / year_end remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpAgendaEvents, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for agenda digest.");
            }

            var result = await dashboards.ListErpAgendaEventsAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                events = result.Events,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only ERP agenda digest. PHP epc_erp_agenda_events remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpDocuments, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for documents digest.");
            }

            var result = await dashboards.ListErpDocumentsAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                documents = result.Documents,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only ERP documents digest. PHP epc_erp_documents remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpExpenseReports, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
            {
                return Unauthorized("Admin ERP capability required for expense-reports digest.");
            }

            var result = await dashboards.ListErpExpenseReportsAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                reports = result.Reports,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only ERP expense reports digest. PHP epc_erp_expense_reports remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.ErpTabCoverage, (IErpAjaxWriteCatalog catalog) =>
        {
            var tabs = ErpPhpTabRouteMap.All
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => new { tab = kv.Key, aspnetApp = kv.Value })
                .ToList();
            var ajax = catalog.BuildReport();
            return Results.Ok(new
            {
                ok = true,
                surface = "erp",
                role = "erp-tab-coverage",
                tabCount = tabs.Count,
                tabs,
                ajaxActions = ajax.TotalActions,
                ajaxCoveragePct = ajax.CoveragePct,
                cutoverAllowed = false,
                readyForPhpRemoval = false,
                phpAuthoritative = true,
                note = "Full PHP erp_tabs_* → ASP.NET app map. Interactive writes remain PHP."
            });
        });

        // /erp (+ aliases) owned by Blazor ErpBosDashboardApp — do not MapGet shell aliases
        // (AmbiguousMatch + admin login wall vs ASP.NET-primary guest browse).
    }

    private static IResult Unauthorized(string message) => Results.Json(
        new { ok = false, error = new { code = "unauthorized", message } },
        statusCode: StatusCodes.Status401Unauthorized);

    private static string JewelleryVoucherReturnApp(string vocType) => vocType switch
    {
        "MMP" or "MLP" => "/cp/jewellery-fixing-app?tab=jw_metal_purchase",
        "DMP" or "DLP" => "/cp/jewellery-fixing-app?tab=jw_diamond_purchase",
        "MSI" or "MSC" => "/cp/jewellery-retail-app?tab=jw_metal_sales",
        "SRN" or "SRC" => "/cp/jewellery-retail-app?tab=jw_sales_return",
        "PCV" => "/erp/cash-accounts-app?tab=jw_petty_cash",
        "RSL" => "/cp/jewellery-repairs-app?tab=jw_repair_sale",
        "PAD" => "/cp/pos-overview-app",
        "JVG" => "/erp/gl-journals-app?tab=jw_journal_voucher",
        _ => "/cp/jewellery-retail-app?tab=jw_retail_sales"
    };

    /// <summary>
    /// Executes a live ERP write and shapes the PHP <c>epc_erp_json</c> response
    /// (<c>status</c>/<c>message</c> + action payload). Validation failures answer HTTP 200
    /// with <c>status=false</c>, exactly like ajax_erp.php.
    /// </summary>
    private static object CashPayload(ErpCashEntryResult result) => new
    {
        cash_entry_id = result.CashEntryId,
        voucher_no = result.VoucherNo,
        gl_journal_id = result.GlJournalId,
        ledger_id = result.LedgerId,
        is_advance = result.IsAdvance,
        allocated = result.Allocated,
        unallocated = result.Unallocated,
    };

    private static async Task<IResult> ExecuteErpWriteAsync(
        LegacySessionContext session,
        Func<Task<(string Message, object Payload)>> write)
    {
        try
        {
            var (message, payload) = await write();
            return Results.Ok(new
            {
                ok = true,
                status = true,
                surface = "erp",
                writes = 1,
                writesBlocked = false,
                phpAuthoritative = false,
                message,
                result = payload,
                session = SessionPayload(session),
            });
        }
        catch (ErpWriteException ex)
        {
            return Results.Ok(new
            {
                ok = false,
                status = false,
                surface = "erp",
                writes = 0,
                writesBlocked = false,
                phpAuthoritative = false,
                message = ex.Message,
                session = SessionPayload(session),
            });
        }
    }

    private static async Task<IResult> HandleCftInstrumentStatusAsync(
        HttpContext context,
        ILegacySessionValidator validator,
        IErpCftInstrumentStatusDryRun dryRun,
        IErpCftInstrumentStatusWriteService writes,
        CancellationToken cancellationToken)
    {
        var session = await validator.ValidateAsync(context, cancellationToken);
        if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
        {
            return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/bank-reconciliation-app", "Admin ERP capability required for bank instrument status.");
        }

        var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpCftInstrumentStatusBody>(context, cancellationToken) ?? new();
        var id = body.Id;
        var targetStatus = body.TargetStatus;
        var detail = body.Detail;
        var amount = body.Amount;
        var confirm = body.ConfirmWrites;
        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken);
            id = LiveWriteFormBinder.Long(form, "id");
            if (form.ContainsKey("status") || form.ContainsKey("targetStatus") || form.ContainsKey("TargetStatus"))
            {
                targetStatus = LiveWriteFormBinder.Text(form, "status", "targetStatus", "TargetStatus");
            }
            detail = LiveWriteFormBinder.Text(form, "detail");
            amount = LiveWriteFormBinder.Dec(form, "amount");
            confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
        }

        if (!confirm)
        {
            return Results.Ok(dryRun.Evaluate(new ErpCftInstrumentStatusRequest(id, targetStatus, detail, amount, false)).ToPayload(SessionPayload(session)));
        }

        var written = await writes.SetStatusAsync(
            new ErpCftInstrumentStatusWriteRequest(id, targetStatus, detail, amount),
            cancellationToken);
        return LiveWriteFormBinder.Complete(
            context,
            "/erp/bank-reconciliation-app?tab=bank_instruments",
            written.Succeeded,
            written.Message,
            new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
    }

    private static async Task<IResult> HandleWhtCodeSaveAsync(
        HttpContext context,
        ILegacySessionValidator validator,
        IErpWhtCodeSaveDryRun dryRun,
        IErpWhtCodeSaveWriteService writes,
        CancellationToken cancellationToken)
    {
        var session = await validator.ValidateAsync(context, cancellationToken);
        if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
        {
            return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/withholding-app", "Admin ERP capability required for withholding code save.");
        }

        var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpWhtCodeSaveBody>(context, cancellationToken) ?? new();
        var id = body.Id;
        var code = body.Code;
        var name = body.Name;
        var rate = body.Rate;
        var account = body.Account;
        var active = body.Active;
        var companyId = body.CompanyId;
        var confirm = body.ConfirmWrites;
        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken);
            id = LiveWriteFormBinder.Long(form, "id");
            code = LiveWriteFormBinder.Text(form, "code");
            name = LiveWriteFormBinder.Text(form, "name");
            rate = LiveWriteFormBinder.Dec(form, "rate");
            account = LiveWriteFormBinder.Text(form, "account");
            companyId = LiveWriteFormBinder.Long(form, "companyId", "company_id");
            confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            if (form.ContainsKey("active") || form.ContainsKey("Active"))
            {
                active = LiveWriteFormBinder.Flag(form, "active", "Active") ? 1 : 0;
            }
        }

        if (!confirm)
        {
            return Results.Ok(dryRun.Evaluate(new ErpWhtCodeSaveRequest(id, code, false, name, rate, account, active, companyId)).ToPayload(SessionPayload(session)));
        }

        var written = await writes.SaveAsync(
            new ErpWhtCodeSaveWriteRequest(id, companyId, code, name, rate, account, active),
            cancellationToken);
        return LiveWriteFormBinder.Complete(
            context,
            "/erp/withholding-app",
            written.Succeeded,
            written.Message,
            new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
    }

    private static async Task<IResult> HandleWhtRecordAsync(
        HttpContext context,
        ILegacySessionValidator validator,
        IErpWhtRecordDryRun dryRun,
        IErpWhtRecordWriteService writes,
        CancellationToken cancellationToken)
    {
        var session = await validator.ValidateAsync(context, cancellationToken);
        if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
        {
            return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/withholding-app", "Admin ERP capability required for withholding record.");
        }

        var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpWhtRecordBody>(context, cancellationToken) ?? new();
        var codeId = body.CodeId > 0 ? body.CodeId : body.Id;
        var vendor = body.Vendor;
        var docRef = body.DocRef;
        var txnDate = body.TxnDate;
        var baseAmount = body.BaseAmount;
        var companyId = body.CompanyId;
        var confirm = body.ConfirmWrites;
        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken);
            codeId = LiveWriteFormBinder.Long(form, "codeId", "code_id", "id");
            vendor = LiveWriteFormBinder.Text(form, "vendor");
            docRef = LiveWriteFormBinder.Text(form, "docRef", "doc_ref");
            txnDate = LiveWriteFormBinder.Text(form, "txnDate", "txn_date");
            baseAmount = LiveWriteFormBinder.Dec(form, "baseAmount", "base_amount");
            companyId = LiveWriteFormBinder.Long(form, "companyId", "company_id");
            confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
        }

        if (!confirm)
        {
            return Results.Ok(dryRun.Evaluate(new ErpWhtRecordRequest(codeId, body.Code, false, codeId, baseAmount, vendor, docRef, txnDate, companyId)).ToPayload(SessionPayload(session)));
        }

        var written = await writes.RecordAsync(
            new ErpWhtRecordWriteRequest(codeId, companyId, vendor, docRef, txnDate, baseAmount),
            cancellationToken);
        return LiveWriteFormBinder.Complete(
            context,
            "/erp/withholding-app",
            written.Succeeded,
            written.Message,
            new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
    }

    private static async Task<IResult> HandleHrAttendanceAsync(
        HttpContext context,
        ILegacySessionValidator validator,
        IErpHrAttendanceDryRun dryRun,
        IErpHrAttendanceWriteService writes,
        CancellationToken cancellationToken)
    {
        var session = await validator.ValidateAsync(context, cancellationToken);
        if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
        {
            return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/hr-overview-app", "Admin ERP capability required for attendance.");
        }

        var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpHrAttendanceBody>(context, cancellationToken) ?? new();
        var employeeId = body.EmployeeId > 0 ? body.EmployeeId : body.Id;
        var workDate = body.WorkDate;
        var workDateStr = body.WorkDateStr;
        var hours = body.Hours;
        var status = body.Status;
        var confirm = body.ConfirmWrites;
        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken);
            employeeId = LiveWriteFormBinder.Long(form, "employeeId", "employee_id", "id");
            workDate = LiveWriteFormBinder.Text(form, "workDate", "work_date");
            workDateStr = LiveWriteFormBinder.Text(form, "workDateStr", "work_date_str");
            hours = LiveWriteFormBinder.Dec(form, "hours");
            status = LiveWriteFormBinder.Text(form, "status");
            confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
        }

        if (!confirm)
        {
            return Results.Ok(dryRun.Evaluate(new ErpHrAttendanceRequest(employeeId, status, false)).ToPayload(SessionPayload(session)));
        }

        var written = await writes.LogAsync(
            new ErpHrAttendanceWriteRequest(employeeId, workDate, workDateStr, hours, status),
            cancellationToken);
        return LiveWriteFormBinder.Complete(
            context,
            "/cp/hr-overview-app",
            written.Succeeded,
            written.Message,
            new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
    }

    private static async Task<IResult> HandleHrEmpSaveAsync(
        HttpContext context,
        ILegacySessionValidator validator,
        IErpHrEmpSaveDryRun dryRun,
        IErpHrEmpSaveWriteService writes,
        CancellationToken cancellationToken)
    {
        var session = await validator.ValidateAsync(context, cancellationToken);
        if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
        {
            return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/hr-overview-app", "Admin ERP capability required for employee save.");
        }

        var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpHrEmpSaveBody>(context, cancellationToken) ?? new();
        var id = body.Id;
        var code = body.Code;
        var name = body.Name;
        var department = body.Department;
        var branchId = body.BranchId;
        var joinDate = body.JoinDate;
        var joinDateStr = body.JoinDateStr;
        var basicSalary = body.BasicSalary;
        var allowances = body.Allowances;
        var currency = body.Currency;
        var annualLeaveDays = body.AnnualLeaveDays;
        var status = body.Status;
        var confirm = body.ConfirmWrites;
        IFormCollection? form = null;
        if (context.Request.HasFormContentType)
        {
            form = await context.Request.ReadFormAsync(cancellationToken);
            id = LiveWriteFormBinder.Long(form, "id");
            code = LiveWriteFormBinder.Text(form, "code");
            name = LiveWriteFormBinder.Text(form, "name");
            department = LiveWriteFormBinder.Text(form, "department");
            branchId = LiveWriteFormBinder.Long(form, "branchId", "branch_id");
            joinDate = LiveWriteFormBinder.Text(form, "joinDate", "join_date");
            joinDateStr = LiveWriteFormBinder.Text(form, "joinDateStr", "join_date_str");
            basicSalary = LiveWriteFormBinder.Dec(form, "basicSalary", "basic_salary");
            allowances = LiveWriteFormBinder.Dec(form, "allowances");
            currency = LiveWriteFormBinder.Text(form, "currency");
            if (form.ContainsKey("annual_leave_days") || form.ContainsKey("annualLeaveDays"))
            {
                annualLeaveDays = LiveWriteFormBinder.Dec(form, "annualLeaveDays", "annual_leave_days");
            }

            status = LiveWriteFormBinder.Text(form, "status");
            confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
        }

        if (!confirm)
        {
            return Results.Ok(dryRun.Evaluate(new ErpHrEmpSaveRequest(id, code, false, name)).ToPayload(SessionPayload(session)));
        }

        var extras = ErpHrEmpSaveWriteService.CollectProvidedExtras(form, body.Extra);
        var written = await writes.SaveAsync(
            new ErpHrEmpSaveWriteRequest(
                id,
                code,
                name,
                department,
                branchId,
                joinDate,
                joinDateStr,
                basicSalary,
                allowances,
                currency,
                annualLeaveDays,
                status,
                extras),
            cancellationToken);
        return LiveWriteFormBinder.Complete(
            context,
            "/cp/hr-overview-app",
            written.Succeeded,
            written.Message,
            new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
    }

    private static async Task<IResult> HandleWhtCertificateAsync(
        HttpContext context,
        ILegacySessionValidator validator,
        IErpWhtCertificateDryRun dryRun,
        IErpWhtCertificateWriteService writes,
        CancellationToken cancellationToken)
    {
        var session = await validator.ValidateAsync(context, cancellationToken);
        if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
        {
            return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/erp/withholding-app", "Admin ERP capability required for withholding certificate.");
        }

        var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<ErpWhtCertificateBody>(context, cancellationToken) ?? new();
        var id = body.Id;
        var certificateNo = body.CertificateNo;
        var confirm = body.ConfirmWrites;
        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken);
            id = LiveWriteFormBinder.Long(form, "id", "txnId", "txn_id");
            certificateNo = LiveWriteFormBinder.Text(form, "certificateNo", "certificate_no");
            confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
        }

        if (!confirm)
        {
            return Results.Ok(dryRun.Evaluate(new ErpWhtCertificateRequest(id, body.Code, false, certificateNo)).ToPayload(SessionPayload(session)));
        }

        var written = await writes.IssueAsync(
            new ErpWhtCertificateWriteRequest(id, certificateNo),
            cancellationToken);
        return LiveWriteFormBinder.Complete(
            context,
            "/erp/withholding-app",
            written.Succeeded,
            written.Message,
            new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
    }

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
        bool ConfirmWrites = false,
        string? CounterpartyType = null,
        long CounterpartyId = 0,
        string? VoucherNo = null);
    private sealed record ErpReceiptVoucherBody(
        long UserId,
        long AccountId,
        decimal Amount,
        long? SalesOrderId = null,
        bool ConfirmWrites = false,
        long? SalesInvoiceId = null,
        bool? IsAdvance = null,
        bool PostGl = false,
        string? Note = null,
        long? OrderId = null,
        bool AutoAllocate = false,
        IReadOnlyList<long>? AllocInvoiceId = null,
        IReadOnlyList<decimal>? AllocAmount = null);
    private sealed record ErpPaymentVoucherBody(
        long SupplierId,
        long AccountId,
        decimal Amount,
        bool ConfirmWrites = false,
        long? PurchaseId = null,
        string? Reference = null,
        string? Note = null,
        long? PurchaseOrderId = null,
        bool IsAdvance = false,
        bool AutoAllocate = false,
        IReadOnlyList<long>? AllocInvoiceId = null,
        IReadOnlyList<decimal>? AllocAmount = null);
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
    private sealed record ErpCashAccountCreateBody(
        string? Name,
        string? AccountType = "cash",
        bool ConfirmWrites = false,
        string? BankName = null,
        string? AccountNumber = null,
        string? CurrencyCode = "AED",
        decimal OpeningBalance = 0m,
        long OfficeId = 0,
        long LegalEntityId = 0,
        long BusinessUnitId = 0,
        long GlAccountId = 0,
        string? Iban = null,
        string? SwiftBic = null,
        string? BankBranch = null,
        string? RoutingCode = null,
        string? Address = null,
        string? ContactName = null,
        string? ContactPhone = null,
        string? ContactEmail = null,
        string? Status = "active",
        string? Notes = null);
    private sealed record ErpCoaCreateBody(
        string? Code,
        string? Name,
        string? AccountType = "expense",
        bool ConfirmWrites = false,
        string? NormalSide = null,
        long ParentId = 0,
        decimal OpeningBalance = 0m,
        string? Description = null);
    private sealed record ErpCustomerMasterSaveBody(
        long CustomerId = 0,
        string? CustomerAccount = null,
        string? CustomerName = null,
        string? CustomerGroup = null,
        long? LegalEntityId = null,
        long? BusinessUnitId = null,
        string? CurrencyCode = null,
        string? PaymentMethod = null,
        string? DeliveryTerms = null,
        string? DeliveryMode = null,
        string? Trn = null,
        bool TaxExempt = false,
        string? SalesTaxGroup = null,
        string? ContactPerson = null,
        string? ContactEmail = null,
        string? ContactPhone = null,
        string? Website = null,
        string? Address = null,
        string? City = null,
        string? StateRegion = null,
        string? PostalCode = null,
        string? CountryCode = null,
        decimal? CreditLimit = null,
        int? TermsDays = null,
        bool OnHold = false,
        string? RiskBand = null,
        string? Notes = null,
        Dictionary<string, long>? Dim = null,
        bool ConfirmWrites = false);
    private sealed record ErpAsRmaCreateLineBody(long ItemId, decimal Qty, decimal UnitPrice = 0, string? ConditionNote = null);
    private sealed record ErpAsRmaResolveBody(
        long RmaId = 0,
        string? Disposition = null,
        decimal RefundAmount = -1,
        bool ConfirmWrites = false);
    private sealed record ErpAsWarrantyRegisterBody(
        long ItemId = 0,
        string? SerialNo = null,
        long CustomerId = 0,
        string? SourceType = null,
        long SourceId = 0,
        long StartDate = 0,
        int Months = 0,
        bool ConfirmWrites = false);
    private sealed record ErpAsJobCreateBody(
        string? JobNo = null,
        long CustomerId = 0,
        string? AssetRef = null,
        string? Complaint = null,
        bool UnderWarranty = false,
        bool ConfirmWrites = false);
    private sealed record ErpAsJobAddLineBody(
        long JobId = 0,
        string? LineType = null,
        string? Description = null,
        long ItemId = 0,
        decimal Qty = 0,
        decimal UnitPrice = 0,
        decimal TaxPercent = 0,
        bool Chargeable = true,
        bool ConfirmWrites = false);
    private sealed record ErpAsJobCloseBody(long JobId = 0, bool ConfirmWrites = false);
    private sealed record ErpAsRmaCreateBody(
        long CustomerId = 0,
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
        long UserId = 0,
        decimal Amount = 0,
        string? Direction = "credit",
        string? EntryKind = "adjustment",
        long OrderId = 0,
        string? Reference = null,
        string? Note = null,
        bool PostGl = false,
        bool ConfirmWrites = false);
    private sealed record ErpSupplierSettlementBody(
        long SupplierId,
        decimal Amount,
        string? Direction = "decrease",
        bool ConfirmWrites = false,
        string? EntryKind = "adjustment",
        long PurchaseId = 0,
        long OrderId = 0,
        string? Reference = null,
        string? Note = null,
        long Time = 0,
        bool PostGl = false);
    private sealed record ErpFiscalSetLockBody(long LockDateUnix = 0, string? Note = null, bool ConfirmWrites = false);
    private sealed record ErpPeriodReopenBody(string? YearMonth, string? Note = null, bool ConfirmWrites = false);
    private sealed record ErpPurchaseAdjustmentBody(long PurchaseId, decimal DeltaExVat, string? Note = null, bool ConfirmWrites = false);
    private sealed record ErpOrderSettlementBody(
        long OrderId = 0,
        decimal Amount = 0,
        string? Direction = "credit",
        string? EntryKind = "adjustment",
        string? Reference = null,
        string? Note = null,
        bool PostGl = false,
        bool ConfirmWrites = false);
    private sealed record ErpSyncSuppliersBody(bool ConfirmWrites = false);
    private sealed record ErpGlPostSalesBody(long? DateFromUnix = null, long? DateToUnix = null, bool ConfirmWrites = false);
    private sealed record ErpGlSyncUnpostedBody(bool ConfirmWrites = false);
    private sealed record ErpWorkflowStatusBody(long TaskId, string? Status = "done", bool ConfirmWrites = false);
    private sealed record ErpWorkflowCreateBody(
        string? Title = null,
        string? DepartmentCode = "admin",
        string? Priority = "normal",
        long OrderId = 0,
        bool ConfirmWrites = false,
        string? Description = null,
        string? WorkflowStep = null,
        int AssignedUserId = 0,
        string? DueAt = null);
    private sealed record ErpMarketingCreateBody(
        string? Name = null,
        bool ConfirmWrites = false,
        string? Channel = null,
        decimal Budget = 0,
        string? Status = null,
        string? TimeStart = null,
        string? TimeEnd = null,
        string? Notes = null);
    private sealed record ErpSubscriptionSaveBody(
        string? Code = null,
        string? Customer = null,
        long Id = 0,
        bool ConfirmWrites = false,
        string? PlanName = null,
        decimal Amount = 0,
        string? Currency = null,
        string? Cycle = null,
        int TermMonths = 12,
        string? StartDate = null);
    private sealed record ErpContractSaveBody(
        string? Code = null,
        string? Title = null,
        long Id = 0,
        bool ConfirmWrites = false,
        string? Counterparty = null,
        decimal ContractValue = 0,
        string? Currency = null,
        string? StartDate = null,
        string? EndDate = null,
        string? BodyText = null);
    private sealed record ErpWmsReceiveBody(
        string? Item,
        decimal Qty,
        long ReceiveLocationId = 0,
        long PutawayLocationId = 0,
        bool ConfirmWrites = false,
        string? Reference = null,
        string? LpCode = null,
        long CompanyId = 0);
    private sealed record ErpWmsLocationSaveBody(
        string? Code = null,
        long Id = 0,
        bool ConfirmWrites = false,
        string? Warehouse = null,
        string? Zone = null,
        string? Type = null,
        int Capacity = 0,
        int Active = 1,
        int CompanyId = 0);
    private sealed record ErpCollectionsCaseSaveBody(
        long CustomerId = 0,
        string? Status = null,
        decimal Balance = 0,
        decimal PromiseAmount = 0,
        string? PromiseDate = null,
        string? AssignedTo = null,
        string? Notes = null,
        long CompanyId = 0,
        long Id = 0,
        bool ConfirmWrites = false);
    private sealed record ErpProcReqSaveBody(
        string? Requester = null,
        long BusinessUnitId = 0,
        string? Justification = null,
        string? ReqNumber = null,
        long CompanyId = 0,
        long Id = 0,
        bool ConfirmWrites = false);
    private sealed record ErpFinPeriodStatusBody(int Fy, int PeriodNo, string? Status = "open", bool ConfirmWrites = false, long CompanyId = 0);
    private sealed record ErpWmsWaveCreateBody(
        string? Item = null,
        decimal Qty = 0,
        string? Reference = null,
        bool ConfirmWrites = false,
        int CompanyId = 0,
        long FromLocationId = 0,
        long ToLocationId = 0);
    private sealed record ErpWmsWaveReleaseBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpWmsWorkCompleteBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpSubscriptionStatusBody(long Id, string? Status = "active", bool ConfirmWrites = false);
    private sealed record ErpCollectionsCaseStatusBody(long Id, string? Status = "new", bool ConfirmWrites = false);
    private sealed record ErpProcReqSubmitBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpProcReqDecisionBody(long Id, bool Approve = true, string? Note = null, bool ConfirmWrites = false);
    private sealed record ErpWmsLocationDeleteBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpOfficesCashAddBody(
        long OfficeId = 0,
        int Income = 0,
        decimal Amount = 0,
        long OperationCodeId = 0,
        string? Comment = null,
        bool ConfirmWrites = false);
    private sealed record ErpOfficesCashCodeAddBody(
        long OfficeId = 0,
        int Income = 1,
        string? Name = null,
        string? NameLangStrId = null,
        string? LangStrId = null,
        string? LangCode = null,
        bool ConfirmWrites = false);
    private sealed record ErpOfficesCashCodeDeleteBody(long OfficeId = 0, long Id = 0, bool ConfirmWrites = false);
    private sealed record ErpGlManualLineBody(long CoaId, decimal Debit, decimal Credit, string? LineNote = null);
    private sealed record ErpGlManualEntryBody(
        IReadOnlyList<ErpGlManualLineBody>? Lines,
        string? Reference,
        string? Description,
        bool ConfirmWrites = false,
        long JournalDate = 0);
    private sealed record ErpGlReverseJournalBody(
        long JournalId,
        string? Note,
        bool ConfirmWrites = false,
        long ReverseDate = 0);
    private sealed record ErpPurchaseVoidBody(long PurchaseId, string? Reason, bool ConfirmWrites = false);
    private sealed record ErpInvoiceCancelBody(long InvoiceId, string? Reason, bool ConfirmWrites = false);
    private sealed record ErpSalesOrderCancelBody(long SalesOrderId, string? Reason, bool ConfirmWrites = false);
    private sealed record ErpSalesOrderDeleteBody(long SalesOrderId, bool ConfirmWrites = false);
    private sealed record ErpPoDeleteBody(long PurchaseOrderId, bool ConfirmWrites = false);
    private sealed record ErpInvSyncWarehousesBody(bool ConfirmWrites = false);
    private sealed record ErpInvCreateWarehouseBody(long Id = 0, string? Code = null, string? Name = null, bool ConfirmWrites = false);
    private sealed record ErpInvCreateItemBody(
        long Id = 0,
        string? Code = null,
        string? Sku = null,
        string? Name = null,
        string? ItemType = null,
        string? Unit = null,
        string? Barcode = null,
        long ProductId = 0,
        bool TrackExpiry = false,
        decimal? ReorderLevel = null,
        Dictionary<string, string>? CustomFields = null,
        Dictionary<string, long>? Dim = null,
        bool ConfirmWrites = false);
    private sealed record ErpInvSetReorderLevelBody(long Id = 0, long ItemId = 0, decimal ReorderLevel = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpInvRecordMovementBody(
        long Id = 0,
        long WarehouseId = 0,
        long ItemId = 0,
        decimal Qty = 0,
        decimal UnitCost = 0,
        string? Code = null,
        string? MovementType = null,
        string? BatchNo = null,
        string? VariantLabel = null,
        string? ExpiryDate = null,
        string? SerialNo = null,
        string? Reference = null,
        string? Note = null,
        string? MovementDate = null,
        long TransferWarehouseId = 0,
        long PurchaseId = 0,
        long OrderId = 0,
        long OpeningBatchId = 0,
        bool ConfirmWrites = false);
    private sealed record ErpInvScanLookupBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpInvTransferBody(
        long Id = 0,
        long FromWarehouseId = 0,
        long ToWarehouseId = 0,
        long ItemId = 0,
        decimal Qty = 0,
        string? Code = null,
        string? BatchNo = null,
        string? VariantLabel = null,
        string? Reference = null,
        string? Note = null,
        bool ConfirmWrites = false);
    private sealed record ErpInvImportCsvBody(
        bool ConfirmWrites = false,
        string? CsvText = null,
        long WarehouseId = 0,
        string? DefaultMovementType = null);
    private sealed record ErpDimSaveBody(
        string? EntityType = null,
        long EntityId = 0,
        Dictionary<string, long>? Dim = null,
        bool ConfirmWrites = false);
    private sealed record ErpInvRunClosingBody(string? PeriodEnd = null, long WarehouseId = 0, bool ConfirmWrites = false);
    private sealed class ErpHrEmpSaveBody
    {
        public long Id { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Department { get; set; }
        public long BranchId { get; set; }
        public string? JoinDate { get; set; }
        public string? JoinDateStr { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal Allowances { get; set; }
        public string? Currency { get; set; }
        public decimal? AnnualLeaveDays { get; set; }
        public string? Status { get; set; }
        public bool ConfirmWrites { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }
    private sealed record ErpHrAttendanceBody(
        long Id = 0,
        long EmployeeId = 0,
        string? WorkDate = null,
        string? WorkDateStr = null,
        decimal Hours = 0,
        string? Status = null,
        bool ConfirmWrites = false);
    private sealed record ErpHrLeaveRequestBody(
        long EmployeeId = 0,
        string? Type = null,
        decimal Days = 0,
        string? DateFrom = null,
        string? DateTo = null,
        bool ConfirmWrites = false);
    private sealed record ErpHrLeaveStatusBody(long Id, string? TargetStatus = null, bool ConfirmWrites = false);
    private sealed record ErpHrExpenseSaveBody(
        long EmployeeId = 0,
        string? Title = null,
        IReadOnlyList<EcomAE.Platform.Erp.ErpHrExpenseLine>? Lines = null,
        string? LinesJson = null,
        bool ConfirmWrites = false);
    private sealed record ErpHrExpenseStatusBody(long Id, string? TargetStatus = null, bool ConfirmWrites = false);
    private sealed record ErpHrUpdateDaysBody(long Id = 0, long StaffProfileId = 0, decimal DaysWorked = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpEinvoiceCreateBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpEinvoiceSaveSellerBody(
        long Id = 0,
        string? Code = null,
        string? SellerName = null,
        string? SellerTrn = null,
        string? SellerTin = null,
        string? SellerLegalRegNo = null,
        string? SellerLegalRegType = null,
        string? SellerAuthorityName = null,
        string? SellerAddressLine1 = null,
        string? SellerCity = null,
        string? SellerEmirate = null,
        string? SellerCountryCode = null,
        string? SellerPhone = null,
        string? SellerEmail = null,
        string? SellerBankAccount = null,
        string? PaymentMeansCode = null,
        string? PaymentTerms = null,
        bool CompanyVatRegistered = false,
        bool ConfirmWrites = false);
    private sealed record ErpEinvoiceSaveBuyerBody(
        long Id = 0,
        long UserId = 0,
        string? Code = null,
        string? BuyerName = null,
        string? Trn = null,
        string? Tin = null,
        string? LegalRegNo = null,
        string? LegalRegType = null,
        string? AuthorityName = null,
        string? AddressLine1 = null,
        string? City = null,
        string? Emirate = null,
        string? CountryCode = null,
        string? Phone = null,
        string? Email = null,
        string? PeppolEndpoint = null,
        bool BuyerOnboarded = false,
        bool ConfirmWrites = false);
    private sealed record ErpEinvoiceSaveAspBody(
        long Id = 0,
        string? Code = null,
        string? AspName = null,
        string? AspApiMode = null,
        string? AspApiUrl = null,
        string? AspApiKey = null,
        string? EinvoiceEnabled = null,
        bool ConfirmWrites = false);
    private sealed record ErpEinvoiceSubmitBody(long Id = 0, bool ConfirmWrites = false);
    private sealed record ErpEinvoiceCreditNoteBody(bool ConfirmWrites = false);
    private sealed record ErpEinvoicePollAspBody(bool ConfirmWrites = false);
    private sealed record ErpExternalReportingFetchBody(string? Action = "fetch", string? ReportKey = null, bool ConfirmWrites = false);
    private sealed record ErpOrderFulfillmentBootstrapBody(bool ConfirmWrites = false);
    private sealed record ErpOrderFulfillmentStatusBody(long Id, string? TargetStatus = null, bool ConfirmWrites = false);
    private sealed record ErpOrderFulfillmentSyncBody(bool ConfirmWrites = false);
    private sealed record ErpOrderFulfillmentPostPoBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpOrderFulfillmentPostSalesBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpOrderFulfillmentAutoPostBody(bool ConfirmWrites = false);
    private sealed record ErpOrderFulfillmentSwapSupplierBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpPmSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpPmToggleBody(long Id = 0, bool ConfirmWrites = false);
    private sealed record ErpPmBudgetSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpPmBudgetLineSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpPmListingSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpPmListingAttachBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpPmChequeSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpMfgrWcSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpMfgrRouteSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpMfgrMrpRunBody(bool ConfirmWrites = false);
    private sealed record ErpMfgrPlannedFirmBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpQmPlanSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpQmTestAddBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpQmOrderCreateBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpQmOrderRecordBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpQmNcrCreateBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpQmNcrUpdateBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpRbacPrivSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpRbacDutySaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpRbacDutyPrivBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpPeriodLogBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpOplAutoplanBody(bool ConfirmWrites = false);
    private sealed record ErpOplSeedDemoBody(bool ConfirmWrites = false);
    private sealed record ErpOplClearDemoBody(bool ConfirmWrites = false);
    private sealed record ErpPfSetDeptHeadBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpPfCaseReassignBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpPfCaseCancelBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpPfSeedDemoBody(bool ConfirmWrites = false);
    private sealed record ErpPfClearDemoBody(bool ConfirmWrites = false);
    private sealed record ErpPfSyncOrdersBody(bool ConfirmWrites = false);
    private sealed record ErpDemoSeedSalesBody(bool ConfirmWrites = false);
    private sealed record ErpDemoClearSalesBody(bool ConfirmWrites = false);
    private sealed record ErpCtrOcrBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpDocxSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpDocxDeleteBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpDocxRunRemindersBody(bool ConfirmWrites = false);
    private sealed record ErpInsSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpInsDeleteBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpInsDocAddBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpInsDocDeleteBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpInsClaimAddBody(
        long Id = 0,
        long PolicyId = 0,
        string? ClaimNo = null,
        string? LossDate = null,
        string? NotifiedDate = null,
        string? DeadlineDate = null,
        string? Description = null,
        decimal ClaimAmount = 0,
        decimal SettledAmount = 0,
        string? Surveyor = null,
        string? Status = null,
        string? Note = null,
        bool ConfirmWrites = false);
    private sealed record ErpFinPeriodsGenerateBody(bool ConfirmWrites = false);
    private sealed record ErpFinFxRevalueBody(bool ConfirmWrites = false);
    private sealed record ErpFinAllocSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpFinAllocRunBody(bool ConfirmWrites = false);
    private sealed record ErpFinAccrualSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpCollHoldSetBody(
        long CustomerId = 0,
        bool ConfirmWrites = false,
        bool Place = true,
        string? Reason = null,
        string? Actor = null,
        long CompanyId = 0);
    private sealed record ErpBplanLineAddBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpBplanPositionAddBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpHrtJobSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpHrtApplicantAddBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpHrtApplicantStageBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpHrtReviewSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpHrtGoalAddBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpHrtReviewFinalizeBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpCftForecastSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpCftLineAddBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpCftInstrumentSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpCftInstrumentStatusBody(
        long Id = 0,
        string? TargetStatus = null,
        string? Detail = null,
        decimal Amount = 0,
        bool ConfirmWrites = false);
    private sealed record ErpWhtCodeSaveBody(
        long Id = 0,
        string? Code = null,
        bool ConfirmWrites = false,
        string? Name = null,
        decimal Rate = 0,
        string? Account = null,
        int? Active = null,
        long CompanyId = 0);
    private sealed record ErpWhtRecordBody(
        long Id = 0,
        string? Code = null,
        bool ConfirmWrites = false,
        long CodeId = 0,
        decimal BaseAmount = 0,
        string? Vendor = null,
        string? DocRef = null,
        string? TxnDate = null,
        long CompanyId = 0);
    private sealed record ErpWhtCertificateBody(
        long Id = 0,
        string? Code = null,
        bool ConfirmWrites = false,
        string? CertificateNo = null);
    private sealed record ErpWhtSettleBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpErFormatSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpErFieldAddBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpPrjaBudgetSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpPrjaTxnAddBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpPrjaRecognizeBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpCostmItemSetBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpCostmTxnAddBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpCostmCloseRunBody(bool ConfirmWrites = false);
    private sealed record ErpIntgEntitySaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpIntgSubSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpIntgEventRaiseBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpFyCreateBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpFyCloseBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpFyReopenBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpFyPeriodStatusBody(long Id, string? TargetStatus = null, bool ConfirmWrites = false, int PeriodNo = 0);
    private sealed record ErpPltJobSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpPltJobRunBody(bool ConfirmWrites = false);
    private sealed record ErpPltFeatureSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpOaPartySaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpOaAddressSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpOaContactSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpOaCalendarSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpOaHolidayAddBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpRbacRoleSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpRbacRoleDutyBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpRbacUserRoleBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpRtlChannelSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpConcurrencyStatusBody(long Id, string? TargetStatus = null, bool ConfirmWrites = false);
    private sealed record ErpSettlementOpenDocsBody(bool ConfirmWrites = false);
    private sealed record ErpDashboardBody(bool ConfirmWrites = false);
    private sealed record ErpCommandCenterBody(bool ConfirmWrites = false);
    private sealed record ErpCcKpiTilesBody(bool ConfirmWrites = false);
    private sealed record ErpCcApprovalQueueBody(bool ConfirmWrites = false);
    private sealed record ErpPeriodListBody(bool ConfirmWrites = false);
    private sealed record ErpPeriodChecklistBody(bool ConfirmWrites = false);
    private sealed record ErpPeriodSummaryBody(bool ConfirmWrites = false);
    private sealed record ErpFxRevaluationPreviewBody(bool ConfirmWrites = false);
    private sealed record ErpBosComplianceFetchBody(bool ConfirmWrites = false);
    private sealed record ErpRtlAssortmentSetBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpRtlDiscountSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpRtlPosSaleBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpInsClaimStatusBody(long Id, string? TargetStatus = null, bool ConfirmWrites = false);
    private sealed record ErpPrjSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpPrjTaskSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpPrjLogTimeBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpConsEntitySaveBody(
        long Id = 0,
        string? Code = null,
        string? Name = null,
        string? CurrencyCode = null,
        decimal OwnershipPct = 100,
        bool IsHome = false,
        string? ParentCode = null,
        bool ConfirmWrites = false);
    private sealed record ErpConsEntityDeleteBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpConsFiguresSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpConsIcSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpConsIcDeleteBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpMfgBomSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpMfgWoCreateBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpMfgWoIssueBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpMfgWoCompleteBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpPayrollGenerateBody(bool ConfirmWrites = false);
    private sealed record ErpPayrollApproveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpInventoryForecastRecomputeBody(
        string? SiteKey = null,
        string? Sku = null,
        int CurrentStock = 0,
        string? ProductName = null,
        int LeadTimeDays = 7,
        bool ConfirmWrites = false);
    private sealed record ErpMultiEntityWriteBody(
        string? Action = null,
        bool ConfirmWrites = false,
        long GroupId = 0,
        string? GroupCode = null,
        string? GroupName = null,
        string? ParentEntity = null,
        string? BaseCurrency = null,
        string? FiscalYearEnd = null,
        string? SiteKey = null,
        string? EntityName = null,
        decimal OwnershipPct = 100,
        string? LocalCurrency = null,
        string? Consolidation = null,
        string? FromSiteKey = null,
        string? ToSiteKey = null,
        decimal Amount = 0,
        string? Description = null);
    private sealed record ErpMultiCurrencyGlSetRateBody(
        string? Action = null,
        string? BaseCurrency = null,
        string? TargetCurrency = null,
        decimal Rate = 0,
        string? EffectiveDate = null,
        string? Source = null,
        bool ConfirmWrites = false);
    private sealed record ErpPayrollPayBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpPayrollUpdateDaysBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpUaeTaxFtaFetchBody(bool ConfirmWrites = false);
    private sealed record ErpAmlCheckBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpAmlReportGenerateBody(bool ConfirmWrites = false);
    private sealed record ErpAmlSeedRulesBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpUaeTaxLegislationRegenSummariesBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpUaeTaxLegislationAskBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpUaeTaxSaveCtAdjustmentsBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpUaeTaxLegislationChecklistSetBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpInvoiceSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpInvoiceListBody(bool ConfirmWrites = false);
    private sealed record ErpInvoiceFromOrderBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpAiQueryBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpIntegrityScanBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpIntegrityApplyFksBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpFaCreateAssetBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpFaRunDepreciationBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpOpeningCreateBatchBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpOpeningAddCoaLineBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpOpeningAddInvLineBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpOpeningPostBatchBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpSaveRfqBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpDeliveryNoteCreateBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpSaveContactBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpSyncContactsBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpDocumentUploadBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpDocumentDeleteBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpSaveCompanyBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpSaveTemplateBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpUploadLogoBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpUploadAttachmentBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpDeleteAttachmentBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpSyncEinvoiceSellerBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpExpenseReportSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpPoSaveBody(
        long Id = 0,
        string? Code = null,
        bool ConfirmWrites = false,
        int SupplierId = 0,
        string? Title = null,
        decimal AmountExVat = 0m,
        string? Status = null,
        string? Notes = null,
        int ExpectedVersion = 0,
        string? LinesJson = null);
    private sealed record ErpPoStatusBody(long Id, string? TargetStatus = null, bool ConfirmWrites = false);
    private sealed record ErpPoReceiveLinesBody(long Id = 0, string? Code = null, bool ConfirmWrites = false, string? ReceivedJson = null);
    private sealed record ErpPoToInvoiceBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpCustomerCreateBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpSoSaveBody(
        long Id = 0,
        string? Code = null,
        bool ConfirmWrites = false,
        int CustomerUserId = 0,
        int ContactId = 0,
        string? Title = null,
        decimal AmountExVat = 0m,
        string? Status = null,
        string? Notes = null,
        bool Export = false,
        string? LinesJson = null);
    private sealed record ErpSoStatusBody(long Id, string? TargetStatus = null, bool ConfirmWrites = false);
    private sealed record ErpSoToInvoiceBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpTransferVoucherBody(
        long Id = 0,
        string? Code = null,
        bool ConfirmWrites = false,
        long FromAccountId = 0,
        long ToAccountId = 0,
        decimal Amount = 0m,
        string? Note = null);
    private sealed record ErpPaymentBatchSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpPettyCashSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpAgendaSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpKbSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpMultiEntitySaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpCsSaveDeclarationBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpCsSubmitDeclarationBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpCsDeleteDeclarationBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpCsListDeclarationsBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpCsImportDeclarationPdfBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpShortcutListBody(bool ConfirmWrites = false);
    private sealed record ErpShortcutAddBody(
        long Id = 0,
        string? Code = null,
        string? Label = null,
        string? TargetUrl = null,
        string? ShortcutKey = null,
        string? Surface = null,
        string? IconClass = null,
        string? IconColor = null,
        string? TargetTab = null,
        long CompanyId = 0,
        bool ConfirmWrites = false);
    private sealed record ErpShortcutDeleteBody(long Id = 0, bool ConfirmWrites = false);
    private sealed record ErpShortcutDeleteKeyBody(long Id = 0, string? Code = null, string? ShortcutKey = null, string? Surface = null, bool ConfirmWrites = false);
    private sealed record ErpShortcutResetBody(long Id = 0, string? Code = null, string? Surface = null, bool ConfirmWrites = false);
    private sealed record ErpShortcutReorderBody(long Id = 0, string? Code = null, string? Ids = null, bool ConfirmWrites = false);
    private sealed record ErpErpFavAddBody(long Id = 0, string? Code = null, string? TabKey = null, string? AreaKey = null, bool ConfirmWrites = false);
    private sealed record ErpErpFavRemoveBody(long Id = 0, string? Code = null, string? TabKey = null, bool ConfirmWrites = false);
    private sealed record ErpErpGlobalSearchBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpJwRepairCreateBody(
        long Id = 0,
        string? Code = null,
        bool ConfirmWrites = false,
        string? RepairNo = null,
        long CustomerId = 0,
        string? CustomerName = null,
        string? CustomerPhone = null,
        string? ItemDescription = null,
        string? MetalType = null,
        string? Karat = null,
        decimal GrossWtIn = 0,
        decimal NetWtIn = 0,
        string? StoneDetails = null,
        string? RepairType = null,
        decimal EstimatedCost = 0,
        long ReceivedDate = 0,
        long PromisedDate = 0);
    private sealed record ErpJwPettyCashSaveBody(
        int CompanyId = 0,
        string? Branch = null,
        string? VocDate = null,
        int VocNo = 0,
        string? PayTo = null,
        string? PaidTo = null,
        string? CashAccount = null,
        string? AccountCode = null,
        string? Narration = null,
        decimal Total = 0,
        decimal GrandTotal = 0,
        decimal TotalAmount = 0,
        bool ConfirmWrites = false);
    private sealed record ErpJwTouristVatSaveBody(
        int CompanyId = 0,
        string? Branch = null,
        string? RefundDate = null,
        string? VocDate = null,
        string? TouristName = null,
        string? PassportNo = null,
        string? Nationality = null,
        string? Mobile = null,
        string? Email = null,
        string? FlightNo = null,
        string? DepartureDate = null,
        string? InvoiceNo = null,
        string? InvoiceDate = null,
        string? Salesman = null,
        string? Narration = null,
        decimal TotalVat = 0,
        decimal VatAmount = 0,
        decimal TotalRefund = 0,
        decimal RefundAmount = 0,
        bool ConfirmWrites = false);
    private sealed record ErpJwRepairReceiptSaveBody(
        int CompanyId = 0,
        string? Branch = null,
        string? ReceiptDate = null,
        string? VocDate = null,
        int VocNo = 0,
        string? CustomerCode = null,
        string? CustomerName = null,
        string? Mobile = null,
        string? Salesman = null,
        string? PromiseDate = null,
        string? Priority = null,
        decimal TotalEstCost = 0,
        decimal AdvanceAmt = 0,
        string? Narration = null,
        bool ConfirmWrites = false);
    private sealed record ErpJwRepairTransferSaveBody(
        int CompanyId = 0,
        string? FromBranch = null,
        string? Branch = null,
        string? ToBranch = null,
        string? TransferDate = null,
        string? VocDate = null,
        int VocNo = 0,
        string? RepairNo = null,
        string? Code = null,
        string? WorkshopContact = null,
        string? ExpectedReturn = null,
        string? Narration = null,
        bool ConfirmWrites = false);
    private sealed record ErpJwWorkshopReceiveSaveBody(
        int CompanyId = 0,
        string? Branch = null,
        string? ReceiveDate = null,
        string? VocDate = null,
        int VocNo = 0,
        string? TransferRef = null,
        string? Code = null,
        string? FromWorkshop = null,
        string? Narration = null,
        bool ConfirmWrites = false);
    private sealed record ErpJwRepairDeliverySaveBody(
        int CompanyId = 0,
        string? Branch = null,
        string? DeliveryDate = null,
        string? VocDate = null,
        int VocNo = 0,
        string? RepairNo = null,
        string? Code = null,
        string? CustomerCode = null,
        string? CustomerName = null,
        string? Mobile = null,
        decimal TotalCharge = 0,
        decimal AdvancePaid = 0,
        decimal BalanceDue = 0,
        string? PayMode = null,
        decimal AmountPaid = 0,
        bool ConfirmWrites = false);
    private sealed record ErpJwStockVerifySaveBody(
        int CompanyId = 0,
        string? Branch = null,
        string? CountDate = null,
        string? VocDate = null,
        int VocNo = 0,
        string? Division = null,
        string? Counter = null,
        string? Location = null,
        string? Code = null,
        string? Supervisor = null,
        string? VerifiedBy = null,
        string? Narration = null,
        bool ConfirmWrites = false);
    private sealed record ErpJwVoucherSaveBody(
        int CompanyId = 0,
        string? Action = null,
        string? VocType = null,
        string? Branch = null,
        string? VocDate = null,
        int VocNo = 0,
        string? PartyCode = null,
        string? PartyName = null,
        string? CustomerName = null,
        string? Currency = null,
        decimal CurrencyRate = 0,
        string? Salesman = null,
        string? RefInvoiceNo = null,
        int CreditDays = 0,
        string? Narration = null,
        decimal NetAmount = 0,
        decimal VatAmount = 0,
        decimal RoundOff = 0,
        decimal GrossTotal = 0,
        bool ConfirmWrites = false);
    private sealed record ErpJwFixingSaveBody(
        int CompanyId = 0,
        string? PartyCode = null,
        string? PartyName = null,
        string? Metal = null,
        string? Karat = null,
        string? RateType = null,
        decimal FixingWt = 0,
        decimal NetWt = 0,
        decimal FixQtyGms = 0,
        decimal FixingRate = 0,
        decimal FixedRate = 0,
        decimal FixRate = 0,
        decimal FixingAmount = 0,
        decimal FixAmount = 0,
        string? RefVoucher = null,
        string? Code = null,
        string? ReferenceVoc = null,
        string? Narration = null,
        string? Remarks = null,
        string? FixDirection = null,
        string? FixType = null,
        string? Branch = null,
        string? FixDate = null,
        int FixNo = 0,
        bool ConfirmWrites = false);
    private sealed record ErpJwMetalStockSaveBody(
        int CompanyId = 0,
        string? Metal = null,
        string? ItemCode = null,
        string? Description = null,
        string? Karat = null,
        decimal Purity = 0,
        string? Type = null,
        string? Category = null,
        string? McUnit = null,
        decimal StdCost = 0,
        bool IncludeStoneWeight = false,
        bool InPieces = true,
        bool GstTrnOnMakingStone = true,
        decimal ConvFactorOz = 31.10347m,
        string? Price1Code = null,
        string? Price1Label = null,
        bool ConfirmWrites = false);
    private sealed record ErpJwBarcodeGenerateBody(
        int CompanyId = 0,
        string? StockCode = null,
        string? Division = null,
        string? Karat = null,
        decimal GrossWt = 0,
        decimal Purity = 0,
        decimal TagPrice = 0,
        bool ConfirmWrites = false);
    private sealed record ErpJwTagCreateBody(
        int CompanyId = 0,
        string? TagNo = null,
        string? Barcode = null,
        string? ItemType = null,
        string? Karat = null,
        decimal GrossWeight = 0,
        decimal NetWeight = 0,
        decimal StoneWeight = 0,
        int StoneCount = 0,
        decimal MakingCharges = 0,
        string? MakingType = null,
        decimal CostPrice = 0,
        decimal SellPrice = 0,
        decimal MarginPct = 0,
        string? DesignNo = null,
        string? Category = null,
        string? Subcategory = null,
        int SupplierId = 0,
        int PurchaseId = 0,
        string? PurchaseDate = null,
        string? Location = null,
        string? Description = null,
        bool ConfirmWrites = false);
    private sealed record ErpJwTagSellBody(
        long TagId = 0,
        int InvoiceId = 0,
        int SalesmanId = 0,
        bool ConfirmWrites = false);
    private sealed record ErpJwGoldSchemeCreateBody(
        int CompanyId = 0,
        string? SchemeCode = null,
        string? SchemeName = null,
        string? SchemeType = null,
        int MaturityMonths = 11,
        string? BonusType = null,
        decimal BonusValue = 0,
        decimal MinInstallment = 500,
        decimal MaxInstallment = 50000,
        string? TermsText = null,
        bool ConfirmWrites = false);
    private sealed record ErpJwGoldSchemeEnrollBody(
        int CompanyId = 0,
        long SchemeId = 0,
        int CustomerId = 0,
        string? CustomerName = null,
        decimal InstallmentAmount = 0,
        string? StartDate = null,
        bool ConfirmWrites = false);
    private sealed record ErpJwGoldSchemePayBody(
        long EnrollmentId = 0,
        decimal Amount = 0,
        string? PaymentMode = null,
        decimal GoldRate = 0,
        string? ReceiptNo = null,
        string? PaymentDate = null,
        bool ConfirmWrites = false);
    private sealed record ErpJwFixUnfixCreateBody(
        int CompanyId = 0,
        int PurchaseId = 0,
        int SupplierId = 0,
        string? SupplierName = null,
        string? PurchaseDate = null,
        string? StructureType = null,
        string? MetalType = null,
        string? Karat = null,
        decimal WeightGrams = 0,
        decimal FixRate = 0,
        string? FixDate = null,
        string? FixReference = null,
        decimal UnfixEstimatedRate = 0,
        decimal MarginOnFix = 0,
        decimal MarginOnUnfix = 0,
        decimal MakingCharges = 0,
        string? Notes = null,
        bool ConfirmWrites = false);
    private sealed record ErpJwFixUnfixSettleBody(
        long Id = 0,
        decimal SettleRate = 0,
        bool ConfirmWrites = false);
    private sealed record ErpJwBarcodePurchaseCreateBody(
        int CompanyId = 0,
        string? Barcode = null,
        string? ItemDescription = null,
        int SupplierId = 0,
        string? SupplierName = null,
        string? PurchaseDate = null,
        string? PurchaseInvoiceNo = null,
        string? MetalType = null,
        string? Karat = null,
        decimal GrossWeight = 0,
        decimal NetWeight = 0,
        decimal StoneWeight = 0,
        decimal GoldRateAtPurchase = 0,
        decimal MakingCharges = 0,
        decimal StoneValue = 0,
        decimal OtherCharges = 0,
        decimal MarginPct = 15,
        int SalesmanId = 0,
        string? SalesmanName = null,
        decimal SalesmanCommissionPct = 2,
        string? Category = null,
        string? DesignNo = null,
        string? HallmarkNo = null,
        string? CertificateNo = null,
        bool ConfirmWrites = false);
    private sealed record ErpJwBarcodePurchaseSellBody(
        long Id = 0,
        int CustomerId = 0,
        int InvoiceId = 0,
        bool ConfirmWrites = false);
    private sealed record ErpSlaCreateBody(
        int CompanyId = 0,
        string? SlaCode = null,
        string? ClientName = null,
        int ClientId = 0,
        string? ServiceType = null,
        decimal ResponseHours = 4,
        decimal ResolutionHours = 24,
        decimal UptimePct = 99.5m,
        string? PenaltyType = null,
        decimal PenaltyAmount = 0,
        string? StartDate = null,
        string? EndDate = null,
        string? Status = null,
        string? Notes = null,
        bool ConfirmWrites = false);
    private sealed record ErpTouristRefundCreateBody(
        int CompanyId = 0,
        long InvoiceId = 0,
        string? InvoiceNo = null,
        string? TouristName = null,
        string? PassportNo = null,
        string? Nationality = null,
        string? DepartureDate = null,
        decimal TotalAmount = 0,
        decimal VatAmount = 0,
        decimal RefundPct = 85,
        string? RefundProvider = null,
        bool ConfirmWrites = false);
    private sealed record ErpTouristRefundValidateBody(string? Barcode = null, bool ConfirmWrites = false);
    private sealed record ErpRfidRegisterBody(
        int CompanyId = 0,
        string? RfidEpc = null,
        string? RfidTid = null,
        long ProductId = 0,
        string? Barcode = null,
        string? Sku = null,
        string? ItemDescription = null,
        long WarehouseId = 0,
        string? LocationZone = null,
        bool ConfirmWrites = false);
    private sealed record ErpRfidStartSessionBody(
        int CompanyId = 0,
        string? SessionType = null,
        long WarehouseId = 0,
        string? Zone = null,
        bool ConfirmWrites = false);
    private sealed record ErpRfidProcessScanBody(
        long SessionId = 0,
        int CompanyId = 0,
        string? RfidEpc = null,
        int Rssi = 0,
        bool ConfirmWrites = false);
    private sealed record ErpGoldRateSetBody(
        int CompanyId = 0,
        string? RateDate = null,
        string? Karat = null,
        string? Currency = null,
        decimal BuyRate = 0,
        decimal SellRate = 0,
        string? Unit = null,
        string? Source = null,
        bool ConfirmWrites = false);
    private sealed record ErpAmlKycLiveSaveBody(
        long Id = 0,
        int CompanyId = 0,
        long CustomerId = 0,
        string? CustomerName = null,
        string? IdType = null,
        string? IdNumber = null,
        string? IdExpiry = null,
        string? Nationality = null,
        string? RiskLevel = null,
        bool PepStatus = false,
        bool SanctionsChecked = false,
        bool SanctionsMatch = false,
        string? VerificationStatus = null,
        string? NextReview = null,
        string? Notes = null,
        bool ConfirmWrites = false);
    private sealed record ErpAmlAlertStatusLiveBody(
        long Id = 0,
        string? TargetStatus = null,
        bool FileSar = false,
        string? SarReference = null,
        bool ConfirmWrites = false);
    private sealed record ErpTicketsCreateBody(
        int CompanyId = 0,
        string? Subject = null,
        string? Description = null,
        string? Category = null,
        string? Priority = null,
        int ClientId = 0,
        string? ClientName = null,
        int AssignedTo = 0,
        string? AssignedName = null,
        int SlaId = 0,
        string? ResponseDeadline = null,
        string? ResolutionDeadline = null,
        bool ConfirmWrites = false);
    private sealed record ErpTicketsReplyBody(
        long TicketId = 0,
        string? AuthorName = null,
        string? AuthorType = null,
        string? Message = null,
        bool IsInternal = false,
        bool ConfirmWrites = false);
    private sealed record ErpCustomerGroupCreateBody(
        int CompanyId = 0,
        string? GroupCode = null,
        string? GroupName = null,
        string? GroupType = null,
        decimal DiscountPct = 0,
        decimal CreditLimit = 0,
        int PaymentTermsDays = 30,
        int PriceListId = 0,
        string? Description = null,
        bool ConfirmWrites = false);
    private sealed record ErpCustomerGroupAssignBody(
        long GroupId = 0,
        int CustomerId = 0,
        bool ConfirmWrites = false);
    private sealed record ErpReportScheduleCreateBody(
        int CompanyId = 0,
        string? ReportName = null,
        string? ReportType = null,
        string? Frequency = null,
        int DayOfWeek = 1,
        int DayOfMonth = 1,
        string? TimeOfDay = null,
        string? Format = null,
        string? Recipients = null,
        string? CcRecipients = null,
        string? SubjectTemplate = null,
        string? BodyTemplate = null,
        string? Filters = null,
        int CreatedBy = 0,
        bool ConfirmWrites = false);
    private sealed record ErpVirtualWarehouseCreateBody(
        int CompanyId = 0,
        string? Code = null,
        string? Name = null,
        string? Type = null,
        string? Address = null,
        int ManagerId = 0,
        string? ManagerName = null,
        int IsSellable = 1,
        string? EventName = null,
        string? EventStart = null,
        string? EventEnd = null,
        int ReturnWarehouseId = 0,
        string? Notes = null,
        bool ConfirmWrites = false);
    private sealed record ErpVirtualWarehouseTransferBody(
        int CompanyId = 0,
        long FromWarehouseId = 0,
        long ToWarehouseId = 0,
        string? Reason = null,
        string? Notes = null,
        string? LinesJson = null,
        IReadOnlyList<ErpVirtualWarehouseTransferLine>? Lines = null,
        int ProductId = 0,
        string? Sku = null,
        string? Barcode = null,
        decimal Qty = 0,
        bool ConfirmWrites = false);
    private sealed record ErpJwColorStoneSaveBody(
        int CompanyId = 0,
        string? Code = null,
        string? Description = null,
        string? Category = null,
        string? Shape = null,
        string? Clarity = null,
        string? Size = null,
        string? Color = null,
        string? Finish = null,
        string? Country = null,
        string? CertificateNo = null,
        string? Vendor = null,
        string? CostCentre = null,
        string? Grade = null,
        bool ConfirmWrites = false);
    private sealed record ErpJwPearlSaveBody(
        int CompanyId = 0,
        string? Code = null,
        string? Nature = null,
        string? Description = null,
        string? Design = null,
        string? Type = null,
        string? CostCentre = null,
        string? Category = null,
        string? Color = null,
        string? Vendor = null,
        string? VendorRef = null,
        string? Luster = null,
        string? Shape = null,
        string? Size = null,
        string? Brand = null,
        string? Country = null,
        string? Grade = null,
        string? SubCategory = null,
        string? Currency = null,
        decimal CurrencyRate = 1,
        decimal CostAmount = 0,
        string? Price1Code = null,
        decimal Price1Pct = 0,
        decimal Price1Fc = 0,
        decimal Price1Lc = 0,
        bool ConfirmWrites = false);
    private sealed record ErpJwDesignSaveBody(
        int CompanyId = 0,
        string? DesignCode = null,
        string? Description = null,
        string? Currency = null,
        decimal CurrencyRate = 1,
        string? CostCentre = null,
        string? Category = null,
        string? SubCategory = null,
        string? Type = null,
        string? Brand = null,
        string? Color = null,
        string? Country = null,
        string? Vendor = null,
        string? VendorRef = null,
        decimal CostAmount = 0,
        string? Price1Code = null,
        decimal Price1Pct = 0,
        decimal Price1Fc = 0,
        decimal Price1Lc = 0,
        bool ConfirmWrites = false);
    private sealed record ErpJwDiamondSaveBody(
        int CompanyId = 0,
        string? ItemCode = null,
        string? Description = null,
        string? Design = null,
        string? Rfid = null,
        string? Category = null,
        string? SubCategory = null,
        string? Type = null,
        string? Brand = null,
        string? Color = null,
        string? Clarity = null,
        string? Fluorescence = null,
        string? Style = null,
        string? SetRef = null,
        string? Country = null,
        string? Vendor = null,
        string? VendorRef = null,
        string? Currency = null,
        decimal CurrencyRate = 1,
        string? CostCentre = null,
        decimal CostAmount = 0,
        decimal ItemGrWt = 0,
        string? Price1Code = null,
        decimal Price1Pct = 0,
        decimal Price1Fc = 0,
        decimal Price1Lc = 0,
        bool Promotional = false,
        bool ConfirmWrites = false);
    private sealed record ErpJwCurrencySaveBody(
        int CompanyId = 0,
        string? CurrCode = null,
        string? Description = null,
        string? Fraction = null,
        string? Symbol = null,
        decimal ConvRate = 1,
        decimal MinConvRate = 1,
        decimal MaxConvRate = 1,
        string? Status = null,
        bool ConfirmWrites = false);
    private sealed record ErpJwRateTypeSaveBody(
        int CompanyId = 0,
        string? Metal = null,
        string? RateType = null,
        decimal ConvFactor = 1,
        decimal ConvFactorOz = 31.1035m,
        string? Currency = null,
        decimal CurrRate = 1,
        decimal RateVariancePct = 50,
        decimal PosMarginMin = 1,
        decimal PosMarginMax = 50,
        string? Status = null,
        bool IsDefault = false,
        bool ConfirmWrites = false);
    private sealed record ErpJwKaratSaveBody(
        int CompanyId = 0,
        string? KaratCode = null,
        string? Description = null,
        decimal StdPurity = 0,
        decimal RangeFrom = 0,
        decimal RangeTo = 0,
        decimal SpGravity = 0,
        decimal PosRateMinMax = 0,
        string? Division = null,
        bool ConfirmWrites = false);
    private sealed record ErpJwRepairUpdateStatusBody(long Id = 0, long RepairId = 0, string? TargetStatus = null, string? NewStatus = null, bool ConfirmWrites = false);
    private sealed record ErpJwSeedSampleDataBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpAiAssistantQueryBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpPrintDesignerSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpWorkflowSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpWorkflowRunBody(bool ConfirmWrites = false);
    private sealed record ErpAutomationActivateBody(bool ConfirmWrites = false);
    private sealed record ErpAutomationDeactivateBody(bool ConfirmWrites = false);
    private sealed record ErpAutomationInstallTemplateBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpAutomationEnableCategoryBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpAutomationTickBody(bool ConfirmWrites = false);
    private sealed record ErpTenantConfigSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpAjaxWriteRegistryBody(bool ConfirmWrites = false);
    private sealed record OnPremisesSetupWizardBody(string? TenantCode = null, bool ConfirmWrites = false);
    private sealed record OnPremisesBackupBody(string? Label = null, bool ConfirmWrites = false);
    private sealed record OnPremisesActivateLicenseCliBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record OnPremisesHealthCheckPackBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record ErpEditLockAcquireBody(string? ResourceKey = null, bool ConfirmWrites = false);
    private sealed record ErpEditLockHeartbeatBody(string? ResourceKey = null, bool ConfirmWrites = false);
    private sealed record ErpEditLockReleaseBody(string? ResourceKey = null, bool ConfirmWrites = false);
    private sealed record ErpPresenceHeartbeatBody(string? ResourceKey = null, bool ConfirmWrites = false);
    private sealed record ErpBosComplianceAddObligationBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpBosComplianceDisableObligationBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpBosComplianceFileBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpBosComplianceSaveRetentionBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpBosWfSaveRuleBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpBosWfDisableRuleBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpBosWfDecideBody(long Id, bool Approve = true, string? Note = null, bool ConfirmWrites = false);
    private sealed record ErpBosWfRaiseTestBody(bool ConfirmWrites = false);
    private sealed record ErpBosIntelToggleControlBody(string? ControlKey = null, bool Enabled = true, bool ConfirmWrites = false);
    private sealed record ErpBosVatRefundSaveBody(
        long Id = 0,
        string? TagRef = null,
        string? InvoiceRef = null,
        string? CustomerName = null,
        string? PassportNo = null,
        string? Nationality = null,
        decimal SaleAmount = 0,
        decimal? VatAmount = null,
        string? SaleDate = null,
        string? Status = null,
        string? Notes = null,
        bool ConfirmWrites = false);
    private sealed record ErpBosVatRefundStatusBody(long Id, string? TargetStatus = null, bool ConfirmWrites = false);
    private sealed record ErpOplParamsSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpOplSetStatusBody(long Id, string? TargetStatus = null, bool ConfirmWrites = false);
    private sealed record ErpOplConfirmAllBody(bool ConfirmWrites = false);
    private sealed record ErpOplCreatePosBody(bool ConfirmWrites = false);
    private sealed record ErpPfProcessSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpPfStepSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpPfStepDeleteBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpPfCaseStartBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpPfCaseActBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpSubGenerateBody(bool ConfirmWrites = false);
    private sealed record ErpSubInvoicePaidBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpCtrStatusBody(long Id, string? TargetStatus = null, bool ConfirmWrites = false);
    private sealed record ErpCtrSignBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpCollCasePromiseBody(
        long Id = 0,
        bool ConfirmWrites = false,
        decimal Amount = 0,
        string? PromiseDate = null);
    private sealed record ErpCollActivityLogBody(
        long Id = 0,
        bool ConfirmWrites = false,
        string? Type = null,
        string? Outcome = null,
        decimal Amount = 0,
        string? FollowUpDate = null);
    private sealed record ErpCollDunningRunBody(
        bool ConfirmWrites = false,
        string? Customers = null,
        long CompanyId = 0);
    private sealed record ErpProcCategorySaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpProcPolicySaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpProcReqAddLineBody(
        long Id = 0,
        bool ConfirmWrites = false,
        long CategoryId = 0,
        string? ItemCode = null,
        string? Description = null,
        decimal Qty = 0,
        decimal UnitPrice = 0,
        string? PreferredVendor = null);
    private sealed record ErpProcReqConvertBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpBplanSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpBplanAdvanceBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpAmlKycSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpAmlAlertStatusBody(long Id, string? TargetStatus = null, bool ConfirmWrites = false);
    private sealed record ErpAmlSettingsSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpBankImportBody(bool ConfirmWrites = false);
    private sealed record ErpBankReconcileBody(bool ConfirmWrites = false);
    private sealed record ErpFxPostRevaluationBody(bool ConfirmWrites = false);
    private sealed record ErpSupplierPaymentBody(
        long Id,
        bool ConfirmWrites = false,
        int SupplierId = 0,
        int AccountId = 0,
        decimal Amount = 0m,
        long PurchaseId = 0,
        string? Reference = null,
        string? Note = null,
        long Time = 0);
}
