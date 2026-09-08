using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_pf_case_reassign</c> / ajax <c>pf_case_reassign</c> twin.
/// UPDATE the open case assignee and the active step. Does not CREATE tables.
/// Case cancel, start, act, and step DELETE are already ASP.NET-live. Seed and
/// schema ensure stay PHP.
/// </summary>
public interface IErpPfCaseReassignWriteService
{
    Task<ErpSimpleWriteResult> ReassignAsync(
        long caseId,
        long userId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpPfCaseReassignWriteService : IErpPfCaseReassignWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpPfCaseReassignWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> ReassignAsync(
        long caseId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        if (caseId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Case is not open");
        }

        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select an assignee");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_pf_cases", "current_assignee_id", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_pf_cases", "status", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_pf_case_steps", "assignee_id", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Process-flow case tables are not provisioned");
        }

        var status = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `status` FROM `epc_pf_cases` WHERE `id`=?"),
            cancellationToken,
            caseId).ConfigureAwait(false);
        if (!string.Equals(status, "open", StringComparison.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Case is not open");
        }

        var stepNo = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `current_step_no` FROM `epc_pf_cases` WHERE `id`=?"),
            cancellationToken,
            caseId).ConfigureAwait(false);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_pf_case_steps` SET `assignee_id`=? WHERE `case_id`=? AND `step_no`=? AND `status`='active'"),
            cancellationToken,
            userId, caseId, stepNo).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_pf_cases` SET `current_assignee_id`=?, `time_updated`=? WHERE `id`=?"),
            cancellationToken,
            userId, now, caseId).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Case reassigned", caseId);
    }

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
