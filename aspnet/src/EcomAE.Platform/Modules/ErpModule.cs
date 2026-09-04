using System.Globalization;
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
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInsClaimStatus, async (HttpContext context, ErpInsClaimStatusBody? body, ILegacySessionValidator validator, IErpInsClaimStatusDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpInsClaimStatusRequest(body.Id, body.TargetStatus, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPrjSave, async (HttpContext context, ErpPrjSaveBody? body, ILegacySessionValidator validator, IErpPrjSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPrjSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPrjTaskSave, async (HttpContext context, ErpPrjTaskSaveBody? body, ILegacySessionValidator validator, IErpPrjTaskSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPrjTaskSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPrjLogTime, async (HttpContext context, ErpPrjLogTimeBody? body, ILegacySessionValidator validator, IErpPrjLogTimeDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpPrjLogTimeRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxConsEntitySave, async (HttpContext context, ErpConsEntitySaveBody? body, ILegacySessionValidator validator, IErpConsEntitySaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpConsEntitySaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxConsEntityDelete, async (HttpContext context, ErpConsEntityDeleteBody? body, ILegacySessionValidator validator, IErpConsEntityDeleteDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpConsEntityDeleteRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxConsFiguresSave, async (HttpContext context, ErpConsFiguresSaveBody? body, ILegacySessionValidator validator, IErpConsFiguresSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpConsFiguresSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxConsIcSave, async (HttpContext context, ErpConsIcSaveBody? body, ILegacySessionValidator validator, IErpConsIcSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpConsIcSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxConsIcDelete, async (HttpContext context, ErpConsIcDeleteBody? body, ILegacySessionValidator validator, IErpConsIcDeleteDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpConsIcDeleteRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
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
        endpoints.MapPost(EcomAeRoutes.ErpAjaxShortcutAdd, async (HttpContext context, ErpShortcutAddBody? body, ILegacySessionValidator validator, IErpShortcutAddDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpShortcutAddRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
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
        endpoints.MapPost(EcomAeRoutes.ErpAjaxShortcutReorder, async (HttpContext context, ErpShortcutReorderBody? body, ILegacySessionValidator validator, IErpShortcutReorderDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpShortcutReorderRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
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
        endpoints.MapPost(EcomAeRoutes.ErpAjaxJwRepairCreate, async (HttpContext context, ErpJwRepairCreateBody? body, ILegacySessionValidator validator, IErpJwRepairCreateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpJwRepairCreateRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
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
                return LiveWriteFormBinder.LoginRedirect(context, "/erp/login?returnUrl=/cp/warehouse-wms-app", "Admin ERP capability required for WMS location delete.");
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
                "/cp/warehouse-wms-app",
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
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBosComplianceDisableObligation, async (HttpContext context, ErpBosComplianceDisableObligationBody? body, ILegacySessionValidator validator, IErpBosComplianceDisableObligationDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpBosComplianceDisableObligationRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBosComplianceFile, async (HttpContext context, ErpBosComplianceFileBody? body, ILegacySessionValidator validator, IErpBosComplianceFileDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpBosComplianceFileRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBosComplianceSaveRetention, async (HttpContext context, ErpBosComplianceSaveRetentionBody? body, ILegacySessionValidator validator, IErpBosComplianceSaveRetentionDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpBosComplianceSaveRetentionRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBosWfSaveRule, async (HttpContext context, ErpBosWfSaveRuleBody? body, ILegacySessionValidator validator, IErpBosWfSaveRuleDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpBosWfSaveRuleRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBosWfDisableRule, async (HttpContext context, ErpBosWfDisableRuleBody? body, ILegacySessionValidator validator, IErpBosWfDisableRuleDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpBosWfDisableRuleRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBosWfDecide, async (HttpContext context, ErpBosWfDecideBody? body, ILegacySessionValidator validator, IErpBosWfDecideDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,true,null,false); return Results.Ok(dryRun.Evaluate(new ErpBosWfDecideRequest(body.Id, body.Approve, body.Note, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBosWfRaiseTest, async (HttpContext context, ErpBosWfRaiseTestBody? body, ILegacySessionValidator validator, IErpBosWfRaiseTestDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpBosWfRaiseTestRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBosIntelToggleControl, async (HttpContext context, ErpBosIntelToggleControlBody? body, ILegacySessionValidator validator, IErpBosIntelToggleControlDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(null,true,false); return Results.Ok(dryRun.Evaluate(new ErpBosIntelToggleControlRequest(body.ControlKey, body.Enabled, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBosVatRefundSave, async (HttpContext context, ErpBosVatRefundSaveBody? body, ILegacySessionValidator validator, IErpBosVatRefundSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpBosVatRefundSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxBosVatRefundStatus, async (HttpContext context, ErpBosVatRefundStatusBody? body, ILegacySessionValidator validator, IErpBosVatRefundStatusDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpBosVatRefundStatusRequest(body.Id, body.TargetStatus, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
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
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPfStepDelete, async (HttpContext context, ErpPfStepDeleteBody? body, ILegacySessionValidator validator, IErpPfStepDeleteDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpPfStepDeleteRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPfCaseStart, async (HttpContext context, ErpPfCaseStartBody? body, ILegacySessionValidator validator, IErpPfCaseStartDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpPfCaseStartRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPfCaseAct, async (HttpContext context, ErpPfCaseActBody? body, ILegacySessionValidator validator, IErpPfCaseActDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpPfCaseActRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxSubGenerate, async (HttpContext context, ErpSubGenerateBody? body, ILegacySessionValidator validator, IErpSubGenerateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpSubGenerateRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxSubInvoicePaid, async (HttpContext context, ErpSubInvoicePaidBody? body, ILegacySessionValidator validator, IErpSubInvoicePaidDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpSubInvoicePaidRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
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
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCollCasePromise, async (HttpContext context, ErpCollCasePromiseBody? body, ILegacySessionValidator validator, IErpCollCasePromiseDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpCollCasePromiseRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCollActivityLog, async (HttpContext context, ErpCollActivityLogBody? body, ILegacySessionValidator validator, IErpCollActivityLogDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpCollActivityLogRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCollDunningRun, async (HttpContext context, ErpCollDunningRunBody? body, ILegacySessionValidator validator, IErpCollDunningRunDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpCollDunningRunRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxProcCategorySave, async (HttpContext context, ErpProcCategorySaveBody? body, ILegacySessionValidator validator, IErpProcCategorySaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpProcCategorySaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxProcPolicySave, async (HttpContext context, ErpProcPolicySaveBody? body, ILegacySessionValidator validator, IErpProcPolicySaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpProcPolicySaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxProcReqAddLine, async (HttpContext context, ErpProcReqAddLineBody? body, ILegacySessionValidator validator, IErpProcReqAddLineDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpProcReqAddLineRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxProcReqConvert, async (HttpContext context, ErpProcReqConvertBody? body, ILegacySessionValidator validator, IErpProcReqConvertDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpProcReqConvertRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
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

        endpoints.MapPost(EcomAeRoutes.ErpAjaxInvSyncWarehouses, async (HttpContext context, ErpInvSyncWarehousesBody? body, ILegacySessionValidator validator, IErpInvSyncWarehousesDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpInvSyncWarehousesRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInvCreateWarehouse, async (HttpContext context, ErpInvCreateWarehouseBody? body, ILegacySessionValidator validator, IErpInvCreateWarehouseDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpInvCreateWarehouseRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInvCreateItem, async (HttpContext context, ErpInvCreateItemBody? body, ILegacySessionValidator validator, IErpInvCreateItemDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpInvCreateItemRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
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
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInvRecordMovement, async (HttpContext context, ErpInvRecordMovementBody? body, ILegacySessionValidator validator, IErpInvRecordMovementDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpInvRecordMovementRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInvScanLookup, async (HttpContext context, ErpInvScanLookupBody? body, ILegacySessionValidator validator, IErpInvScanLookupDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpInvScanLookupRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInvTransfer, async (HttpContext context, ErpInvTransferBody? body, ILegacySessionValidator validator, IErpInvTransferDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpInvTransferRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInvImportCsv, async (HttpContext context, ErpInvImportCsvBody? body, ILegacySessionValidator validator, IErpInvImportCsvDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpInvImportCsvRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInvRunClosing, async (HttpContext context, ErpInvRunClosingBody? body, ILegacySessionValidator validator, IErpInvRunClosingDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(false); return Results.Ok(dryRun.Evaluate(new ErpInvRunClosingRequest(body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxHrEmpSave, async (HttpContext context, ErpHrEmpSaveBody? body, ILegacySessionValidator validator, IErpHrEmpSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpHrEmpSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxHrAttendance, async (HttpContext context, ErpHrAttendanceBody? body, ILegacySessionValidator validator, IErpHrAttendanceDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpHrAttendanceRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxHrLeaveRequest, async (HttpContext context, ErpHrLeaveRequestBody? body, ILegacySessionValidator validator, IErpHrLeaveRequestDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpHrLeaveRequestRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxHrLeaveStatus, async (HttpContext context, ErpHrLeaveStatusBody? body, ILegacySessionValidator validator, IErpHrLeaveStatusDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpHrLeaveStatusRequest(body.Id, body.TargetStatus, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxHrExpenseSave, async (HttpContext context, ErpHrExpenseSaveBody? body, ILegacySessionValidator validator, IErpHrExpenseSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpHrExpenseSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxHrExpenseStatus, async (HttpContext context, ErpHrExpenseStatusBody? body, ILegacySessionValidator validator, IErpHrExpenseStatusDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpHrExpenseStatusRequest(body.Id, body.TargetStatus, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
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
        endpoints.MapPost(EcomAeRoutes.ErpAjaxEinvoiceSaveSeller, async (HttpContext context, ErpEinvoiceSaveSellerBody? body, ILegacySessionValidator validator, IErpEinvoiceSaveSellerDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpEinvoiceSaveSellerRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxEinvoiceSaveBuyer, async (HttpContext context, ErpEinvoiceSaveBuyerBody? body, ILegacySessionValidator validator, IErpEinvoiceSaveBuyerDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpEinvoiceSaveBuyerRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxEinvoiceSaveAsp, async (HttpContext context, ErpEinvoiceSaveAspBody? body, ILegacySessionValidator validator, IErpEinvoiceSaveAspDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpEinvoiceSaveAspRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
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
        endpoints.MapPost(EcomAeRoutes.ErpAjaxPfCaseCancel, async (HttpContext context, ErpPfCaseCancelBody? body, ILegacySessionValidator validator, IErpPfCaseCancelDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpPfCaseCancelRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
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
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInsDocDelete, async (HttpContext context, ErpInsDocDeleteBody? body, ILegacySessionValidator validator, IErpInsDocDeleteDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,false); return Results.Ok(dryRun.Evaluate(new ErpInsDocDeleteRequest(body.Id, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxInsClaimAdd, async (HttpContext context, ErpInsClaimAddBody? body, ILegacySessionValidator validator, IErpInsClaimAddDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpInsClaimAddRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
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
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCollHoldSet, async (HttpContext context, ErpCollHoldSetBody? body, ILegacySessionValidator validator, IErpCollHoldSetDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpCollHoldSetRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
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
        endpoints.MapPost(EcomAeRoutes.ErpAjaxCftInstrumentStatus, async (HttpContext context, ErpCftInstrumentStatusBody? body, ILegacySessionValidator validator, IErpCftInstrumentStatusDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpCftInstrumentStatusRequest(body.Id, body.TargetStatus, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxWhtCodeSave, async (HttpContext context, ErpWhtCodeSaveBody? body, ILegacySessionValidator validator, IErpWhtCodeSaveDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpWhtCodeSaveRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxWhtRecord, async (HttpContext context, ErpWhtRecordBody? body, ILegacySessionValidator validator, IErpWhtRecordDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpWhtRecordRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxWhtCertificate, async (HttpContext context, ErpWhtCertificateBody? body, ILegacySessionValidator validator, IErpWhtCertificateDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpWhtCertificateRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxWhtSettle, async (HttpContext context, ErpWhtSettleBody? body, ILegacySessionValidator validator, IErpWhtSettleDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpWhtSettleRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
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
        endpoints.MapPost(EcomAeRoutes.ErpAjaxFyReopen, async (HttpContext context, ErpFyReopenBody? body, ILegacySessionValidator validator, IErpFyReopenDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpFyReopenRequest(body.Id, body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.ErpAjaxFyPeriodStatus, async (HttpContext context, ErpFyPeriodStatusBody? body, ILegacySessionValidator validator, IErpFyPeriodStatusDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp")) return Unauthorized("Admin ERP capability required."); body ??= new(0,null,false); return Results.Ok(dryRun.Evaluate(new ErpFyPeriodStatusRequest(body.Id, body.TargetStatus, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
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
                note = "Read-only epc_erp_workflow_tasks KPIs + board. PHP epc_erp_staff.php / ajax_erp.php remain authoritative."
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
                note = "Read-only epc_wht_code + epc_wht_txn. PHP epc_erp_withholding.php writes remain authoritative."
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
                note = "Read-only epc_erp_subscriptions + MRR/ARR. PHP subscriptions tab remains authoritative."
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
                note = "Read-only virtual/exhibition warehouse locations + transfer history. PHP virtual_warehouse tab remains authoritative."
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
                note = "Read-only epc_erp_contracts (body/OCR omitted). PHP contracts tab remains authoritative."
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
                note = "Read-only epc_erp_marketing_campaigns. PHP marketing tab remains authoritative."
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
            return Results.Ok(new { ok = true, surface = "erp", groups = result.Groups, count = result.Count, activeCount = result.ActiveCount, memberTotal = result.MemberTotal, source = result.Source, message = result.Message, session = SessionPayload(session), note = "Read-only epc_customer_groups. PHP customer_groups tab remains authoritative." });
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
            return Results.Ok(new { ok = true, surface = "erp", schedules = result.Schedules, count = result.Count, activeCount = result.ActiveCount, source = result.Source, message = result.Message, session = SessionPayload(session), note = "Read-only epc_report_schedules (recipients/body omitted). PHP report_scheduler remains authoritative." });
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
            return Results.Ok(new { ok = true, surface = "erp", groups = result.Groups, intercompany = result.Intercompany, count = result.Count, memberTotal = result.MemberTotal, icTxnCount = result.IcTxnCount, pendingIcCount = result.PendingIcCount, source = result.Source, message = result.Message, session = SessionPayload(session), note = "Read-only epc_entity_groups + epc_intercompany_txns. PHP consolidation remains authoritative." });
        });

        endpoints.MapGet(EcomAeRoutes.ErpMultiCurrencyGl, async (HttpContext context, int? limit, ILegacySessionValidator validator, ISurfaceDashboardSummaryReporter dashboards, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
                return Unauthorized("Admin ERP capability required for multi-currency-gl digest.");
            var result = await dashboards.ListErpMultiCurrencyGlAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new { ok = true, surface = "erp", rates = result.Rates, entries = result.Entries, count = result.Count, entryCount = result.EntryCount, unrevaluedCount = result.UnrevaluedCount, revalGainLossTotal = result.RevalGainLossTotal, source = result.Source, message = result.Message, session = SessionPayload(session), note = "Read-only epc_fx_rates + epc_gl_currency_entries. PHP revaluation remains authoritative." });
        });

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
    private sealed record ErpOfficesCashAddBody(
        long OfficeId = 0,
        int Income = 0,
        decimal Amount = 0,
        long OperationCodeId = 0,
        string? Comment = null,
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
    private sealed record ErpInvCreateWarehouseBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpInvCreateItemBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpInvSetReorderLevelBody(long Id = 0, long ItemId = 0, decimal ReorderLevel = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpInvRecordMovementBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpInvScanLookupBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpInvTransferBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpInvImportCsvBody(bool ConfirmWrites = false);
    private sealed record ErpInvRunClosingBody(bool ConfirmWrites = false);
    private sealed record ErpHrEmpSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpHrAttendanceBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpHrLeaveRequestBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpHrLeaveStatusBody(long Id, string? TargetStatus = null, bool ConfirmWrites = false);
    private sealed record ErpHrExpenseSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpHrExpenseStatusBody(long Id, string? TargetStatus = null, bool ConfirmWrites = false);
    private sealed record ErpHrUpdateDaysBody(long Id = 0, long StaffProfileId = 0, decimal DaysWorked = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpEinvoiceCreateBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpEinvoiceSaveSellerBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpEinvoiceSaveBuyerBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpEinvoiceSaveAspBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
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
    private sealed record ErpInsClaimAddBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpFinPeriodsGenerateBody(bool ConfirmWrites = false);
    private sealed record ErpFinFxRevalueBody(bool ConfirmWrites = false);
    private sealed record ErpFinAllocSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpFinAllocRunBody(bool ConfirmWrites = false);
    private sealed record ErpFinAccrualSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpCollHoldSetBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
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
    private sealed record ErpCftInstrumentStatusBody(long Id, string? TargetStatus = null, bool ConfirmWrites = false);
    private sealed record ErpWhtCodeSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpWhtRecordBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpWhtCertificateBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
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
    private sealed record ErpFyPeriodStatusBody(long Id, string? TargetStatus = null, bool ConfirmWrites = false);
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
    private sealed record ErpConsEntitySaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
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
    private sealed record ErpShortcutAddBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpShortcutDeleteBody(long Id = 0, bool ConfirmWrites = false);
    private sealed record ErpShortcutDeleteKeyBody(long Id = 0, string? Code = null, string? ShortcutKey = null, string? Surface = null, bool ConfirmWrites = false);
    private sealed record ErpShortcutResetBody(long Id = 0, string? Code = null, string? Surface = null, bool ConfirmWrites = false);
    private sealed record ErpShortcutReorderBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpErpFavAddBody(long Id = 0, string? Code = null, string? TabKey = null, string? AreaKey = null, bool ConfirmWrites = false);
    private sealed record ErpErpFavRemoveBody(long Id = 0, string? Code = null, string? TabKey = null, bool ConfirmWrites = false);
    private sealed record ErpErpGlobalSearchBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpJwRepairCreateBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
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
    private sealed record ErpBosVatRefundSaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
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
    private sealed record ErpCollCasePromiseBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpCollActivityLogBody(long Id, bool ConfirmWrites = false);
    private sealed record ErpCollDunningRunBody(bool ConfirmWrites = false);
    private sealed record ErpProcCategorySaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpProcPolicySaveBody(long Id = 0, string? Code = null, bool ConfirmWrites = false);
    private sealed record ErpProcReqAddLineBody(long Id, bool ConfirmWrites = false);
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
