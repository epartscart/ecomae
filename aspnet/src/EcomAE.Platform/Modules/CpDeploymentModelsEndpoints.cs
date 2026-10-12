using System.Text.Json;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Modules;

/// <summary>
/// Super CP setup-pack downloads and hybrid change-log sync. Super host only for
/// downloads; ingest/pull accept Super staff or a stored enrollment token.
/// Writes stay dry-run unless <c>confirmWrites=true</c>.
/// </summary>
internal static class CpDeploymentModelsEndpoints
{
    private const string ReturnUrl = "/cp/deployment-models-app";

    public static void Map(IEndpointRouteBuilder endpoints, Func<LegacySessionContext, object> sessionPayload)
    {
        endpoints.MapGet(EcomAeRoutes.ControlPanelDeploymentModelsSetupFiles, async (
            HttpContext context,
            ILegacySessionValidator validator,
            CancellationToken cancellationToken) =>
        {
            var gate = await RequireSuperStaffAsync(context, validator, cancellationToken).ConfigureAwait(false);
            if (gate.Error is not null)
            {
                return gate.Error;
            }

            return Results.Json(new
            {
                ok = true,
                packs = CpDeploymentSetupPackCatalog.CatalogPayload(),
                note = "Allowlisted setup files only. This path does not invent a send.",
                session = sessionPayload(gate.Session!),
            });
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelDeploymentModelsSetupFile, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IWebHostEnvironment environment,
            CancellationToken cancellationToken) =>
        {
            var gate = await RequireSuperStaffAsync(context, validator, cancellationToken).ConfigureAwait(false);
            if (gate.Error is not null)
            {
                return gate.Error;
            }

            var kind = context.Request.Query["kind"].ToString();
            var file = context.Request.Query["file"].ToString();
            var entry = CpDeploymentSetupPackCatalog.Find(kind, file);
            if (entry is null)
            {
                return Results.NotFound(new { ok = false, validation_code = "unknown-file", message = "That setup file is not on the allowlist." });
            }

            var physical = CpDeploymentSetupPackCatalog.ResolveFile(kind, file, environment.ContentRootPath, AppContext.BaseDirectory);
            if (physical is null)
            {
                return Results.NotFound(new { ok = false, validation_code = "missing-file", message = "The setup file is allowlisted but not on this host." });
            }

            return Results.File(physical, entry.ContentType, Path.GetFileName(entry.RelativePath));
        });

        endpoints.MapGet(EcomAeRoutes.ControlPanelDeploymentModelsSetupPack, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IWebHostEnvironment environment,
            CancellationToken cancellationToken) =>
        {
            var gate = await RequireSuperStaffAsync(context, validator, cancellationToken).ConfigureAwait(false);
            if (gate.Error is not null)
            {
                return gate.Error;
            }

            var kind = CpDeploymentSetupPackCatalog.NormalizeKind(context.Request.Query["kind"].ToString());
            if (!CpDeploymentSetupPackCatalog.IsKind(kind))
            {
                return Results.BadRequest(new { ok = false, validation_code = "unknown-kind", message = "kind must be on-premises or hybrid." });
            }

            try
            {
                var bytes = CpDeploymentSetupPackCatalog.ZipKind(kind, environment.ContentRootPath, AppContext.BaseDirectory);
                return Results.File(bytes, "application/zip", CpDeploymentSetupPackCatalog.ZipFileName(kind));
            }
            catch (FileNotFoundException ex)
            {
                return Results.NotFound(new { ok = false, validation_code = "missing-pack", message = ex.Message });
            }
        });

        endpoints.MapGet(EcomAeRoutes.CpHybridSyncStatus, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IHybridSyncService sync,
            CancellationToken cancellationToken) =>
        {
            var gate = await RequireSuperStaffAsync(context, validator, cancellationToken).ConfigureAwait(false);
            if (gate.Error is not null)
            {
                return gate.Error;
            }

            var status = await sync.StatusAsync(context.Request.Query["siteKey"].ToString(), cancellationToken).ConfigureAwait(false);
            return Results.Json(new
            {
                ok = true,
                installations = status.Installations,
                cursors = status.Cursors,
                conflicts = status.Conflicts,
                error = status.Error,
                note = "Control-plane change-log only. Shop tables are not copied.",
                session = sessionPayload(gate.Session!),
            });
        });

        endpoints.MapPost(EcomAeRoutes.CpHybridSyncRegister, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IHybridSyncService sync,
            CancellationToken cancellationToken) =>
        {
            var gate = await RequireSuperStaffAsync(context, validator, cancellationToken).ConfigureAwait(false);
            if (gate.Error is not null)
            {
                return gate.Error;
            }

            var body = await ReadBodyAsync(context, cancellationToken).ConfigureAwait(false);
            try
            {
                var result = await sync.RegisterAsync(
                    body.Text("siteKey", "site_key", "tenantKey"),
                    body.TextOrNull("installationId", "installation_id"),
                    body.TextOrNull("enrollmentRequestId", "enrollment_request_id"),
                    body.TextOrNull("cloudBaseUrl", "cloud_base_url"),
                    body.TextOrNull("deploymentKind", "deployment_kind") ?? "hybrid",
                    body.ConfirmWrites,
                    cancellationToken).ConfigureAwait(false);
                return Answer(context, result, sessionPayload(gate.Session!));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { ok = false, validation_code = "invalid", message = ex.Message });
            }
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.CpHybridSyncIngest, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IHybridSyncService sync,
            CancellationToken cancellationToken) =>
        {
            var body = await ReadBodyAsync(context, cancellationToken).ConfigureAwait(false);
            var envelope = body.ToEnvelope();
            var gate = await RequireSuperOrEnrollmentAsync(
                context, validator, sync, envelope.TenantKey, envelope.InstallationId, body, cancellationToken).ConfigureAwait(false);
            if (gate.Error is not null)
            {
                return gate.Error;
            }

            try
            {
                var result = await sync.IngestAsync(envelope, body.PayloadJson, body.ConfirmWrites, cancellationToken).ConfigureAwait(false);
                return Answer(context, result, gate.Session is null ? new { kind = "enrollment" } : sessionPayload(gate.Session));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { ok = false, validation_code = "invalid", message = ex.Message });
            }
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.CpHybridSyncPull, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IHybridSyncService sync,
            CancellationToken cancellationToken) =>
        {
            var body = await ReadBodyAsync(context, cancellationToken).ConfigureAwait(false);
            var siteKey = body.Text("siteKey", "site_key", "tenantKey");
            var installationId = body.Text("installationId", "installation_id");
            var gate = await RequireSuperOrEnrollmentAsync(
                context, validator, sync, siteKey, installationId, body, cancellationToken).ConfigureAwait(false);
            if (gate.Error is not null)
            {
                return gate.Error;
            }

            try
            {
                var result = await sync.PullAsync(siteKey, installationId, body.ConfirmWrites, cancellationToken).ConfigureAwait(false);
                return Answer(context, result, gate.Session is null ? new { kind = "enrollment" } : sessionPayload(gate.Session));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { ok = false, validation_code = "invalid", message = ex.Message });
            }
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.CpHybridSyncAck, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IHybridSyncService sync,
            CancellationToken cancellationToken) =>
        {
            var body = await ReadBodyAsync(context, cancellationToken).ConfigureAwait(false);
            var siteKey = body.Text("siteKey", "site_key", "tenantKey");
            var installationId = body.Text("installationId", "installation_id");
            var gate = await RequireSuperOrEnrollmentAsync(
                context, validator, sync, siteKey, installationId, body, cancellationToken).ConfigureAwait(false);
            if (gate.Error is not null)
            {
                return gate.Error;
            }

            try
            {
                var result = await sync.AckAsync(siteKey, installationId, body.EnvelopeIds, body.ConfirmWrites, cancellationToken).ConfigureAwait(false);
                return Answer(context, result, gate.Session is null ? new { kind = "enrollment" } : sessionPayload(gate.Session));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { ok = false, validation_code = "invalid", message = ex.Message });
            }
        }).DisableAntiforgery();
    }

    private static async Task<(LegacySessionContext? Session, IResult? Error)> RequireSuperStaffAsync(
        HttpContext context,
        ILegacySessionValidator validator,
        CancellationToken cancellationToken)
    {
        if (!SuperCpHostGate.IsAllowed(context))
        {
            return (null, Results.NotFound(new { ok = false, message = "Setup packs are available on the Super CP host only." }));
        }

        var session = await validator.ValidateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!PhpParityDumpCatalog.HasStaffAccess(session))
        {
            return (null, Results.Json(
                new { ok = false, error = new { code = "unauthorized", message = "Admin CP session required." } },
                statusCode: StatusCodes.Status401Unauthorized));
        }

        return (session, null);
    }

    private static async Task<(LegacySessionContext? Session, IResult? Error)> RequireSuperOrEnrollmentAsync(
        HttpContext context,
        ILegacySessionValidator validator,
        IHybridSyncService sync,
        string siteKey,
        string installationId,
        SyncBody body,
        CancellationToken cancellationToken)
    {
        if (!SuperCpHostGate.IsAllowed(context))
        {
            return (null, Results.NotFound(new { ok = false, message = "Hybrid sync is available on the Super CP host only." }));
        }

        var session = await validator.ValidateAsync(context, cancellationToken).ConfigureAwait(false);
        if (PhpParityDumpCatalog.HasStaffAccess(session))
        {
            return (session, null);
        }

        var enrollment = body.EnrollmentRequestId;
        if (enrollment.Length == 0)
        {
            enrollment = context.Request.Headers["X-EcomAE-Enrollment"].ToString().Trim();
        }

        if (await sync.EnrollmentMatchesAsync(siteKey, installationId, enrollment, cancellationToken).ConfigureAwait(false))
        {
            return (null, null);
        }

        return (null, Results.Json(
            new { ok = false, error = new { code = "unauthorized", message = "Super CP session or a stored enrollment token is required." } },
            statusCode: StatusCodes.Status401Unauthorized));
    }

    private static IResult Answer(HttpContext context, HybridSyncWriteResult result, object session)
    {
        var payload = new Dictionary<string, object?>
        {
            ["ok"] = result.Ok,
            ["status"] = result.Ok,
            ["validation_code"] = result.Code,
            ["message"] = result.Message,
            ["writes"] = result.Writes,
            ["writesBlocked"] = result.DryRun,
            ["dryRun"] = result.DryRun,
            ["phpAuthoritative"] = false,
            ["envelopes"] = result.Envelopes,
            ["installation"] = result.Installation,
            ["session"] = session,
        };
        if (LiveWriteFormBinder.WantsHtml(context))
        {
            return LiveWriteFormBinder.Complete(context, ReturnUrl, result.Ok, result.Message, payload);
        }

        return Results.Json(payload);
    }

    private static async Task<SyncBody> ReadBodyAsync(HttpContext context, CancellationToken cancellationToken)
    {
        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            return SyncBody.FromForm(form);
        }

        var json = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<SyncJson>(context, cancellationToken).ConfigureAwait(false);
        return SyncBody.FromJson(json);
    }

    private sealed record SyncJson(
        string? SiteKey = null,
        string? TenantKey = null,
        string? InstallationId = null,
        string? EnrollmentRequestId = null,
        string? CloudBaseUrl = null,
        string? DeploymentKind = null,
        string? EnvelopeId = null,
        string? EntityType = null,
        string? EntityId = null,
        long Version = 0,
        string? Direction = null,
        string? PayloadHash = null,
        string? PayloadJson = null,
        JsonElement? Payload = null,
        string[]? EnvelopeIds = null,
        DateTimeOffset? CreatedAt = null,
        bool ConfirmWrites = false);

    private sealed record SyncBody(
        IReadOnlyDictionary<string, string> Values,
        string? PayloadJson,
        IReadOnlyList<string> EnvelopeIds,
        bool ConfirmWrites,
        DateTimeOffset? CreatedAt)
    {
        public string EnrollmentRequestId => Text("enrollmentRequestId", "enrollment_request_id", "enrollment");

        public string Text(params string[] names)
        {
            foreach (var name in names)
            {
                if (Values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        public string? TextOrNull(params string[] names)
        {
            var value = Text(names);
            return value.Length == 0 ? null : value;
        }

        public TenantSyncEnvelope ToEnvelope()
        {
            var payload = PayloadJson ?? "{}";
            var hash = Text("payloadHash", "payload_hash");
            if (hash.Length == 0)
            {
                hash = TenantInstallationControlPlane.Sha256Hex(payload);
            }

            return new TenantSyncEnvelope(
                Text("envelopeId", "envelope_id"),
                Text("tenantKey", "tenant_key", "siteKey", "site_key"),
                Text("installationId", "installation_id"),
                Text("entityType", "entity_type"),
                Text("entityId", "entity_id"),
                long.TryParse(Text("version"), out var version) ? version : 0,
                Text("direction"),
                hash,
                CreatedAt ?? DateTimeOffset.UtcNow);
        }

        public static SyncBody FromForm(IFormCollection form)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in form)
            {
                values[pair.Key] = pair.Value.ToString();
            }

            var ids = (form["envelopeIds"].ToString() + "," + form["envelope_ids"].ToString())
                .Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return new(
                values,
                form["payloadJson"].ToString().Length > 0 ? form["payloadJson"].ToString() : form["payload_json"].ToString(),
                ids,
                LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes"),
                null);
        }

        public static SyncBody FromJson(SyncJson? json)
        {
            json ??= new SyncJson();
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["siteKey"] = json.SiteKey ?? string.Empty,
                ["tenantKey"] = json.TenantKey ?? json.SiteKey ?? string.Empty,
                ["installationId"] = json.InstallationId ?? string.Empty,
                ["enrollmentRequestId"] = json.EnrollmentRequestId ?? string.Empty,
                ["cloudBaseUrl"] = json.CloudBaseUrl ?? string.Empty,
                ["deploymentKind"] = json.DeploymentKind ?? string.Empty,
                ["envelopeId"] = json.EnvelopeId ?? string.Empty,
                ["entityType"] = json.EntityType ?? string.Empty,
                ["entityId"] = json.EntityId ?? string.Empty,
                ["version"] = json.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["direction"] = json.Direction ?? string.Empty,
                ["payloadHash"] = json.PayloadHash ?? string.Empty,
            };
            string? payload = json.PayloadJson;
            if (string.IsNullOrWhiteSpace(payload) && json.Payload is { } element && element.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                payload = element.GetRawText();
            }

            return new(values, payload, json.EnvelopeIds ?? [], json.ConfirmWrites, json.CreatedAt);
        }
    }
}
