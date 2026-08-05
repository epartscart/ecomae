using EcomAE.Platform.Auth;
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
