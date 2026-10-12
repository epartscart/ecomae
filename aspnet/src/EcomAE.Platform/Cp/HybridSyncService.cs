using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Cp;

public sealed record HybridSyncInstallationRow(
    string SiteKey,
    string InstallationId,
    string EnrollmentRequestId,
    string DeploymentKind,
    string Status,
    string CloudBaseUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSeenAt);

public sealed record HybridSyncCursorRow(
    string SiteKey,
    string InstallationId,
    string Direction,
    long LastVersion,
    string LastEnvelopeId,
    DateTimeOffset UpdatedAt);

public sealed record HybridSyncConflictRow(
    long Id,
    string SiteKey,
    string EnvelopeId,
    string EntityType,
    string EntityId,
    string Reason,
    string Detail,
    DateTimeOffset QueuedAt,
    string Resolution);

public sealed record HybridSyncEnvelopeRow(
    string EnvelopeId,
    string TenantKey,
    string InstallationId,
    string EntityType,
    string EntityId,
    long Version,
    string Direction,
    string PayloadHash,
    string PayloadJson,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record HybridSyncWriteResult(
    bool Ok,
    string Code,
    string Message,
    int Writes,
    bool DryRun,
    IReadOnlyList<HybridSyncEnvelopeRow> Envelopes,
    HybridSyncInstallationRow? Installation = null);

public sealed record HybridSyncStatusView(
    IReadOnlyList<HybridSyncInstallationRow> Installations,
    IReadOnlyList<HybridSyncCursorRow> Cursors,
    IReadOnlyList<HybridSyncConflictRow> Conflicts,
    IReadOnlyDictionary<string, string> KindBySiteKey,
    string Error);

public interface IHybridSyncService
{
    Task<HybridSyncWriteResult> RegisterAsync(
        string siteKey,
        string? installationId,
        string? enrollmentRequestId,
        string? cloudBaseUrl,
        string? deploymentKind,
        bool confirmWrites,
        CancellationToken cancellationToken = default);

    Task<HybridSyncWriteResult> IngestAsync(
        TenantSyncEnvelope envelope,
        string? payloadJson,
        bool confirmWrites,
        CancellationToken cancellationToken = default);

    Task<HybridSyncWriteResult> PullAsync(
        string siteKey,
        string installationId,
        bool confirmWrites,
        CancellationToken cancellationToken = default);

    Task<HybridSyncWriteResult> AckAsync(
        string siteKey,
        string installationId,
        IReadOnlyList<string> envelopeIds,
        bool confirmWrites,
        CancellationToken cancellationToken = default);

    Task<HybridSyncStatusView> StatusAsync(string? siteKey, CancellationToken cancellationToken = default);

    Task<bool> EnrollmentMatchesAsync(
        string siteKey,
        string installationId,
        string enrollmentRequestId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Platform-DB hybrid change-log (cursor, envelopes, conflicts). Does not copy shop tables
/// and does not invent a MySQL user or a send.
/// </summary>
public sealed class HybridSyncService : IHybridSyncService
{
    private readonly IErpWriteConnectionFactory _connections;

    public HybridSyncService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<HybridSyncWriteResult> RegisterAsync(
        string siteKey,
        string? installationId,
        string? enrollmentRequestId,
        string? cloudBaseUrl,
        string? deploymentKind,
        bool confirmWrites,
        CancellationToken cancellationToken = default)
    {
        var tenantKey = TenantInstallationControlPlane.NormalizeTenantKey(siteKey);
        var inst = string.IsNullOrWhiteSpace(installationId)
            ? HybridSyncControlPlane.NewInstallationId()
            : installationId.Trim();
        var enrollment = string.IsNullOrWhiteSpace(enrollmentRequestId)
            ? HybridSyncControlPlane.NewEnrollmentRequestId()
            : enrollmentRequestId.Trim();
        if (!HybridSyncControlPlane.IsEnrollmentRequestId(enrollment))
        {
            return Fail("invalid-enrollment", "Enrollment request id must be an enr_ token.");
        }

        var kind = CpDeploymentModelsCatalog.NormalizeKind(deploymentKind);
        if (kind.Length == 0)
        {
            kind = "hybrid";
        }

        var url = (cloudBaseUrl ?? string.Empty).Trim();
        if (url.Length > 0)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return Fail("invalid-cloud-url", "Cloud base URL must be HTTPS.");
            }

            url = uri.ToString().TrimEnd('/');
        }

        if (!confirmWrites)
        {
            return new(
                true,
                "dry_run",
                "Set confirmWrites=true to register the " + kind + " installation for " + tenantKey + ".",
                0,
                true,
                [],
                new HybridSyncInstallationRow(tenantKey, inst, enrollment, kind, "preview", url, DateTimeOffset.UtcNow, null));
        }

        if (!_connections.IsConfigured)
        {
            return Fail("db-unconfigured", "Platform database is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            """
            INSERT INTO epc_hybrid_sync_installations
                (site_key, installation_id, enrollment_request_id, deployment_kind, cloud_base_url, status, created_at)
            VALUES (@p0, @p1, @p2, @p3, @p4, 'enrolled', @p5)
            ON DUPLICATE KEY UPDATE
                enrollment_request_id = VALUES(enrollment_request_id),
                deployment_kind = VALUES(deployment_kind),
                cloud_base_url = VALUES(cloud_base_url),
                status = 'enrolled',
                last_seen_at = VALUES(created_at)
            """,
            cancellationToken,
            tenantKey, inst, enrollment, kind, url, now).ConfigureAwait(false);

        return new(
            true,
            "registered",
            "Installation enrolled. Store the enrollment token on the site agent — this service does not invent a send.",
            1,
            false,
            [],
            new HybridSyncInstallationRow(tenantKey, inst, enrollment, kind, "enrolled", url, DateTimeOffset.UtcNow, null));
    }

    public async Task<HybridSyncWriteResult> IngestAsync(
        TenantSyncEnvelope envelope,
        string? payloadJson,
        bool confirmWrites,
        CancellationToken cancellationToken = default)
    {
        string sanitized;
        try
        {
            sanitized = HybridSyncControlPlane.SanitizePayloadJson(payloadJson);
        }
        catch (ArgumentException ex)
        {
            return Fail("invalid-payload", ex.Message);
        }

        var expectedHash = TenantInstallationControlPlane.Sha256Hex(sanitized);
        if (!string.Equals(expectedHash, envelope.PayloadHash, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(payloadJson))
        {
            envelope = envelope with { PayloadHash = expectedHash };
        }

        if (!_connections.IsConfigured)
        {
            var preview = HybridSyncControlPlane.DecideIngest(envelope, null, 0, null);
            return FromDecision(preview, dryRun: !confirmWrites, []);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var existing = await LoadExistingAsync(connection, envelope.TenantKey, envelope.InstallationId, envelope.EntityType, envelope.EntityId, cancellationToken).ConfigureAwait(false);
        var decision = HybridSyncControlPlane.DecideIngest(envelope, existing?.EnvelopeId, existing?.Version ?? 0, existing?.Status);
        if (decision.Kind is HybridSyncDecisionKind.Reject)
        {
            return FromDecision(decision, confirmWrites, []);
        }

        if (!confirmWrites)
        {
            return FromDecision(decision, true, decision.Kind == HybridSyncDecisionKind.Accept
                ? [ToRow(envelope, sanitized, decision.Kind == HybridSyncDecisionKind.Conflict ? "conflict" : "queued")]
                : []);
        }

        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        if (decision.Kind == HybridSyncDecisionKind.Replay)
        {
            return new(true, decision.Code, decision.Detail, 0, false, existing is null ? [] : [existing]);
        }

        if (decision.Kind == HybridSyncDecisionKind.Conflict)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                """
                INSERT INTO epc_hybrid_sync_conflicts
                    (site_key, envelope_id, entity_type, entity_id, reason, detail, queued_at, resolution)
                VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, 'queued')
                """,
                cancellationToken,
                envelope.TenantKey, envelope.EnvelopeId, envelope.EntityType, envelope.EntityId, decision.Code, decision.Detail, now).ConfigureAwait(false);
            await UpsertEnvelopeAsync(connection, envelope, sanitized, "conflict", now, cancellationToken).ConfigureAwait(false);
            return new(true, decision.Code, decision.Detail, 1, false, [ToRow(envelope, sanitized, "conflict")]);
        }

        await UpsertEnvelopeAsync(connection, envelope, sanitized, "queued", now, cancellationToken).ConfigureAwait(false);
        await UpsertCursorAsync(connection, envelope.TenantKey, envelope.InstallationId, envelope.Direction, envelope.Version, envelope.EnvelopeId, now, cancellationToken).ConfigureAwait(false);
        await TouchInstallationAsync(connection, envelope.TenantKey, envelope.InstallationId, now, cancellationToken).ConfigureAwait(false);
        return new(true, decision.Code, decision.Detail, 1, false, [ToRow(envelope, sanitized, "queued")]);
    }

    public async Task<HybridSyncWriteResult> PullAsync(
        string siteKey,
        string installationId,
        bool confirmWrites,
        CancellationToken cancellationToken = default)
    {
        var tenantKey = TenantInstallationControlPlane.NormalizeTenantKey(siteKey);
        if (string.IsNullOrWhiteSpace(installationId))
        {
            return Fail("invalid-installation", "Installation id is required.");
        }

        if (!_connections.IsConfigured)
        {
            var empty = HybridSyncControlPlane.DecidePull(0);
            return new(true, empty.Code, empty.Detail, 0, !confirmWrites, []);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var pending = await LoadPendingMastersAsync(connection, tenantKey, installationId.Trim(), cancellationToken).ConfigureAwait(false);
        var decision = HybridSyncControlPlane.DecidePull(pending.Count);
        if (!confirmWrites)
        {
            return new(true, decision.Code, decision.Detail + " Set confirmWrites=true only after the agent persists the envelopes, then call ack.", 0, true, pending);
        }

        return new(true, decision.Code, decision.Detail, 0, false, pending);
    }

    public async Task<HybridSyncWriteResult> AckAsync(
        string siteKey,
        string installationId,
        IReadOnlyList<string> envelopeIds,
        bool confirmWrites,
        CancellationToken cancellationToken = default)
    {
        var tenantKey = TenantInstallationControlPlane.NormalizeTenantKey(siteKey);
        var ids = (envelopeIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0)
        {
            return Fail("invalid-envelope", "At least one envelope id is required to acknowledge a pull.");
        }

        if (!confirmWrites)
        {
            return new(true, "dry_run", "Set confirmWrites=true to mark " + ids.Length + " pulled master envelope(s) applied.", 0, true, []);
        }

        if (!_connections.IsConfigured)
        {
            return Fail("db-unconfigured", "Platform database is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var writes = 0;
        string lastId = "";
        long lastVersion = 0;
        foreach (var id in ids)
        {
            writes += await ErpDb.ExecuteAsync(
                connection,
                null,
                """
                UPDATE epc_hybrid_sync_envelopes
                   SET status = 'applied', applied_at = @p0
                 WHERE site_key = @p1 AND installation_id = @p2 AND envelope_id = @p3
                   AND direction IN ('cloud-to-hybrid', 'cloud-to-onpremises')
                   AND status IN ('queued', 'applied')
                """,
                cancellationToken,
                now, tenantKey, installationId.Trim(), id).ConfigureAwait(false);
            lastId = id;
        }

        if (writes > 0 && lastId.Length > 0)
        {
            lastVersion = await ErpDb.LongAsync(
                connection,
                null,
                "SELECT COALESCE(MAX(version),0) FROM epc_hybrid_sync_envelopes WHERE site_key = @p0 AND installation_id = @p1 AND envelope_id = @p2",
                cancellationToken,
                tenantKey, installationId.Trim(), lastId).ConfigureAwait(false);
            await UpsertCursorAsync(connection, tenantKey, installationId.Trim(), "cloud-to-hybrid", lastVersion, lastId, now, cancellationToken).ConfigureAwait(false);
            await TouchInstallationAsync(connection, tenantKey, installationId.Trim(), now, cancellationToken).ConfigureAwait(false);
        }

        return new(true, "acked", "Marked pulled master envelopes applied.", writes, false, []);
    }

    public async Task<HybridSyncStatusView> StatusAsync(string? siteKey, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new([], [], [], new Dictionary<string, string>(), "Platform database is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            var key = string.IsNullOrWhiteSpace(siteKey) ? null : TenantInstallationControlPlane.NormalizeTenantKey(siteKey);
            var installations = await LoadInstallationsAsync(connection, key, cancellationToken).ConfigureAwait(false);
            var cursors = await LoadCursorsAsync(connection, key, cancellationToken).ConfigureAwait(false);
            var conflicts = await LoadConflictsAsync(connection, key, cancellationToken).ConfigureAwait(false);
            var kinds = installations
                .GroupBy(i => i.SiteKey, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Last().DeploymentKind, StringComparer.Ordinal);
            return new(installations, cursors, conflicts, kinds, string.Empty);
        }
        catch (Exception ex)
        {
            return new([], [], [], new Dictionary<string, string>(), ex.Message);
        }
    }

    public async Task<bool> EnrollmentMatchesAsync(
        string siteKey,
        string installationId,
        string enrollmentRequestId,
        CancellationToken cancellationToken = default)
    {
        if (!HybridSyncControlPlane.IsEnrollmentRequestId(enrollmentRequestId)
            || string.IsNullOrWhiteSpace(installationId)
            || !_connections.IsConfigured)
        {
            return false;
        }

        try
        {
            var tenantKey = TenantInstallationControlPlane.NormalizeTenantKey(siteKey);
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            var found = await ErpDb.StringAsync(
                connection,
                null,
                """
                SELECT enrollment_request_id
                  FROM epc_hybrid_sync_installations
                 WHERE site_key = @p0 AND installation_id = @p1
                 LIMIT 1
                """,
                cancellationToken,
                tenantKey, installationId.Trim()).ConfigureAwait(false);
            return string.Equals(found, enrollmentRequestId.Trim(), StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static HybridSyncWriteResult Fail(string code, string message)
        => new(false, code, message, 0, false, []);

    private static HybridSyncWriteResult FromDecision(HybridSyncDecision decision, bool dryRun, IReadOnlyList<HybridSyncEnvelopeRow> envelopes)
        => new(
            decision.Kind is not HybridSyncDecisionKind.Reject,
            decision.Code,
            dryRun && decision.Kind == HybridSyncDecisionKind.Accept
                ? decision.Detail + " Set confirmWrites=true to persist the change-log row."
                : decision.Detail,
            0,
            dryRun,
            envelopes);

    private static HybridSyncEnvelopeRow ToRow(TenantSyncEnvelope envelope, string payloadJson, string status)
        => new(
            envelope.EnvelopeId,
            envelope.TenantKey,
            envelope.InstallationId,
            envelope.EntityType,
            envelope.EntityId,
            envelope.Version,
            envelope.Direction,
            envelope.PayloadHash,
            payloadJson,
            status,
            envelope.CreatedAt);

    private static async Task UpsertEnvelopeAsync(
        DbConnection connection,
        TenantSyncEnvelope envelope,
        string payloadJson,
        string status,
        string now,
        CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            null,
            """
            INSERT INTO epc_hybrid_sync_envelopes
                (envelope_id, site_key, installation_id, entity_type, entity_id, version, direction, payload_hash, payload_json, status, created_at)
            VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10)
            ON DUPLICATE KEY UPDATE
                payload_hash = VALUES(payload_hash),
                payload_json = VALUES(payload_json),
                status = VALUES(status),
                version = VALUES(version)
            """,
            cancellationToken,
            envelope.EnvelopeId,
            envelope.TenantKey,
            envelope.InstallationId,
            envelope.EntityType,
            envelope.EntityId,
            envelope.Version,
            envelope.Direction,
            envelope.PayloadHash,
            payloadJson,
            status,
            now).ConfigureAwait(false);
    }

    private static Task UpsertCursorAsync(
        DbConnection connection,
        string siteKey,
        string installationId,
        string direction,
        long version,
        string envelopeId,
        string now,
        CancellationToken cancellationToken)
        => ErpDb.ExecuteAsync(
            connection,
            null,
            """
            INSERT INTO epc_hybrid_sync_cursor
                (site_key, installation_id, direction, last_version, last_envelope_id, updated_at)
            VALUES (@p0, @p1, @p2, @p3, @p4, @p5)
            ON DUPLICATE KEY UPDATE
                last_version = VALUES(last_version),
                last_envelope_id = VALUES(last_envelope_id),
                updated_at = VALUES(updated_at)
            """,
            cancellationToken,
            siteKey, installationId, direction, version, envelopeId, now);

    private static Task TouchInstallationAsync(
        DbConnection connection,
        string siteKey,
        string installationId,
        string now,
        CancellationToken cancellationToken)
        => ErpDb.ExecuteAsync(
            connection,
            null,
            "UPDATE epc_hybrid_sync_installations SET last_seen_at = @p0 WHERE site_key = @p1 AND installation_id = @p2",
            cancellationToken,
            now, siteKey, installationId);

    private static async Task<HybridSyncEnvelopeRow?> LoadExistingAsync(
        DbConnection connection,
        string siteKey,
        string installationId,
        string entityType,
        string entityId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT envelope_id, site_key, installation_id, entity_type, entity_id, version, direction, payload_hash, payload_json, status, created_at
              FROM epc_hybrid_sync_envelopes
             WHERE site_key = @p0 AND installation_id = @p1 AND entity_type = @p2 AND entity_id = @p3
             ORDER BY version DESC, id DESC
             LIMIT 1
            """;
        ErpDb.AddParameters(command, siteKey, installationId, entityType, entityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadEnvelope(reader) : null;
    }

    private static async Task<IReadOnlyList<HybridSyncEnvelopeRow>> LoadPendingMastersAsync(
        DbConnection connection,
        string siteKey,
        string installationId,
        CancellationToken cancellationToken)
    {
        var rows = new List<HybridSyncEnvelopeRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT envelope_id, site_key, installation_id, entity_type, entity_id, version, direction, payload_hash, payload_json, status, created_at
              FROM epc_hybrid_sync_envelopes
             WHERE site_key = @p0 AND installation_id = @p1
               AND direction IN ('cloud-to-hybrid', 'cloud-to-onpremises')
               AND status = 'queued'
             ORDER BY version ASC, id ASC
             LIMIT 200
            """;
        ErpDb.AddParameters(command, siteKey, installationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(ReadEnvelope(reader));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<HybridSyncInstallationRow>> LoadInstallationsAsync(
        DbConnection connection,
        string? siteKey,
        CancellationToken cancellationToken)
    {
        var rows = new List<HybridSyncInstallationRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = siteKey is null
            ? """
              SELECT site_key, installation_id, enrollment_request_id, deployment_kind, status, cloud_base_url, created_at, last_seen_at
                FROM epc_hybrid_sync_installations
               ORDER BY id DESC
               LIMIT 200
              """
            : """
              SELECT site_key, installation_id, enrollment_request_id, deployment_kind, status, cloud_base_url, created_at, last_seen_at
                FROM epc_hybrid_sync_installations
               WHERE site_key = @p0
               ORDER BY id DESC
               LIMIT 200
              """;
        if (siteKey is not null)
        {
            ErpDb.AddParameters(command, siteKey);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                ReadTime(reader, 6),
                reader.IsDBNull(7) ? null : ReadTime(reader, 7)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<HybridSyncCursorRow>> LoadCursorsAsync(
        DbConnection connection,
        string? siteKey,
        CancellationToken cancellationToken)
    {
        var rows = new List<HybridSyncCursorRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = siteKey is null
            ? "SELECT site_key, installation_id, direction, last_version, last_envelope_id, updated_at FROM epc_hybrid_sync_cursor ORDER BY id DESC LIMIT 200"
            : "SELECT site_key, installation_id, direction, last_version, last_envelope_id, updated_at FROM epc_hybrid_sync_cursor WHERE site_key = @p0 ORDER BY id DESC LIMIT 200";
        if (siteKey is not null)
        {
            ErpDb.AddParameters(command, siteKey);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture),
                reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                ReadTime(reader, 5)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<HybridSyncConflictRow>> LoadConflictsAsync(
        DbConnection connection,
        string? siteKey,
        CancellationToken cancellationToken)
    {
        var rows = new List<HybridSyncConflictRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = siteKey is null
            ? "SELECT id, site_key, envelope_id, entity_type, entity_id, reason, detail, queued_at, resolution FROM epc_hybrid_sync_conflicts WHERE resolution = 'queued' ORDER BY id DESC LIMIT 200"
            : "SELECT id, site_key, envelope_id, entity_type, entity_id, reason, detail, queued_at, resolution FROM epc_hybrid_sync_conflicts WHERE resolution = 'queued' AND site_key = @p0 ORDER BY id DESC LIMIT 200";
        if (siteKey is not null)
        {
            ErpDb.AddParameters(command, siteKey);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                ReadTime(reader, 7),
                reader.IsDBNull(8) ? "queued" : reader.GetString(8)));
        }

        return rows;
    }

    private static HybridSyncEnvelopeRow ReadEnvelope(DbDataReader reader)
        => new(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            Convert.ToInt64(reader.GetValue(5), CultureInfo.InvariantCulture),
            reader.GetString(6),
            reader.GetString(7),
            reader.IsDBNull(8) ? "{}" : reader.GetString(8),
            reader.GetString(9),
            ReadTime(reader, 10));

    private static DateTimeOffset ReadTime(DbDataReader reader, int ordinal)
    {
        var value = reader.GetValue(ordinal);
        return value is DateTime dt
            ? new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc))
            : DateTimeOffset.UtcNow;
    }

    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS epc_hybrid_sync_installations (
              id BIGINT NOT NULL AUTO_INCREMENT,
              site_key VARCHAR(80) NOT NULL,
              installation_id VARCHAR(80) NOT NULL,
              enrollment_request_id VARCHAR(120) NOT NULL,
              deployment_kind VARCHAR(20) NOT NULL,
              cloud_base_url VARCHAR(255) NOT NULL DEFAULT '',
              status VARCHAR(40) NOT NULL DEFAULT 'enrolled',
              created_at DATETIME NOT NULL,
              last_seen_at DATETIME NULL,
              PRIMARY KEY (id),
              UNIQUE KEY uq_hybrid_inst (site_key, installation_id),
              UNIQUE KEY uq_hybrid_enroll (enrollment_request_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """,
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS epc_hybrid_sync_cursor (
              id BIGINT NOT NULL AUTO_INCREMENT,
              site_key VARCHAR(80) NOT NULL,
              installation_id VARCHAR(80) NOT NULL,
              direction VARCHAR(40) NOT NULL,
              last_version BIGINT NOT NULL DEFAULT 0,
              last_envelope_id VARCHAR(80) NOT NULL DEFAULT '',
              updated_at DATETIME NOT NULL,
              PRIMARY KEY (id),
              UNIQUE KEY uq_hybrid_cursor (site_key, installation_id, direction)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """,
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS epc_hybrid_sync_envelopes (
              id BIGINT NOT NULL AUTO_INCREMENT,
              envelope_id VARCHAR(80) NOT NULL,
              site_key VARCHAR(80) NOT NULL,
              installation_id VARCHAR(80) NOT NULL,
              entity_type VARCHAR(80) NOT NULL,
              entity_id VARCHAR(80) NOT NULL,
              version BIGINT NOT NULL,
              direction VARCHAR(40) NOT NULL,
              payload_hash CHAR(64) NOT NULL,
              payload_json MEDIUMTEXT NULL,
              status VARCHAR(20) NOT NULL DEFAULT 'queued',
              created_at DATETIME NOT NULL,
              applied_at DATETIME NULL,
              PRIMARY KEY (id),
              UNIQUE KEY uq_hybrid_envelope (envelope_id),
              KEY idx_hybrid_entity (site_key, installation_id, entity_type, entity_id, version),
              KEY idx_hybrid_pending (site_key, installation_id, direction, status)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """,
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS epc_hybrid_sync_conflicts (
              id BIGINT NOT NULL AUTO_INCREMENT,
              site_key VARCHAR(80) NOT NULL,
              envelope_id VARCHAR(80) NOT NULL,
              entity_type VARCHAR(80) NOT NULL,
              entity_id VARCHAR(80) NOT NULL,
              reason VARCHAR(80) NOT NULL,
              detail TEXT NULL,
              queued_at DATETIME NOT NULL,
              resolution VARCHAR(20) NOT NULL DEFAULT 'queued',
              PRIMARY KEY (id),
              KEY idx_hybrid_conflict (site_key, resolution)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """,
            cancellationToken).ConfigureAwait(false);
    }
}
