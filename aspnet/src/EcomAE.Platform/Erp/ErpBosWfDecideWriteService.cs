using System.Data.Common;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_bos_wf_decide</c> / ajax <c>bos_wf_decide</c> twin. UPDATE
/// <c>epc_bos_approval_requests</c> and INSERT <c>epc_bos_approval_log</c>.
/// Does not CREATE tables. Disable is already ASP.NET-live. Save, raise,
/// seed, and schema ensure stay PHP.
/// </summary>
public interface IErpBosWfDecideWriteService
{
    Task<ErpSimpleWriteResult> DecideAsync(
        ErpBosWfDecideWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpBosWfDecideWriteRequest(
    long RequestId = 0,
    string? Decision = null,
    string? Comment = null,
    long AdminId = 0,
    string? ActorName = null);

public sealed class ErpBosWfDecideWriteService : IErpBosWfDecideWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpBosWfDecideWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> DecideAsync(
        ErpBosWfDecideWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Request not pending");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var decision = (request.Decision ?? "approve").Trim().ToLowerInvariant();
        var comment = request.Comment ?? string.Empty;
        var adminId = request.AdminId < 0 ? 0 : request.AdminId;
        var actor = Clip((request.ActorName ?? string.Empty).Trim(), 120);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_bos_approval_requests", "status", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_bos_approval_requests", "current_step", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_bos_approval_log", "request_id", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Approval request table is not provisioned");
        }

        var status = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `status` FROM `epc_bos_approval_requests` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            request.RequestId).ConfigureAwait(false);
        if (!string.Equals(status, "pending", StringComparison.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Request not pending");
        }

        var current = (int)await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `current_step` FROM `epc_bos_approval_requests` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            request.RequestId).ConfigureAwait(false);
        var stepsJson = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `steps_json` FROM `epc_bos_approval_requests` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            request.RequestId).ConfigureAwait(false);
        var stepCount = DecodeStepCount(stepsJson);

        if (decision == "reject")
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_bos_approval_requests` SET `status` = 'rejected', `decided_at` = ? WHERE `id` = ?"),
                cancellationToken,
                now, request.RequestId).ConfigureAwait(false);
            await InsertLogAsync(connection, request.RequestId, current, "rejected", adminId, actor, comment, now, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Rejected", request.RequestId);
        }

        await InsertLogAsync(connection, request.RequestId, current, "approved", adminId, actor, comment, now, cancellationToken).ConfigureAwait(false);
        if (current + 1 >= stepCount)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_bos_approval_requests` SET `status` = 'approved', `current_step` = ?, `decided_at` = ? WHERE `id` = ?"),
                cancellationToken,
                current, now, request.RequestId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Approved (final)", request.RequestId);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_bos_approval_requests` SET `current_step` = ? WHERE `id` = ?"),
            cancellationToken,
            current + 1, request.RequestId).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Approved — advanced to next step", request.RequestId);
    }

    public static int DecodeStepCount(string? json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                return doc.RootElement.GetArrayLength();
            }
        }
        catch (JsonException)
        {
            // PHP falls back to a single default step.
        }

        return 1;
    }

    private static async Task InsertLogAsync(
        DbConnection connection,
        long requestId,
        int stepIndex,
        string action,
        long adminId,
        string actor,
        string comment,
        long now,
        CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_bos_approval_log` (`request_id`,`step_index`,`action`,`actor_id`,`actor_name`,`comment`,`time`) VALUES (?,?,?,?,?,?,?)"),
            cancellationToken,
            requestId, stepIndex, action, adminId, actor, comment, now).ConfigureAwait(false);
    }

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];

    private static async Task<bool> ColumnExistsAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
