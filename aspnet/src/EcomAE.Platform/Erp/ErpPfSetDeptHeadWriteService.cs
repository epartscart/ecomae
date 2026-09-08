using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_pf_set_dept_head</c> / ajax <c>pf_set_dept_head</c> twin.
/// UPSERT <c>epc_pf_dept_heads</c> on <c>department_code</c>. Does not CREATE
/// tables. Case cancel, step DELETE stay ASP.NET-live. Start, act, reassign,
/// seed, and schema ensure stay PHP.
/// </summary>
public interface IErpPfSetDeptHeadWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        string? departmentCode,
        long headUserId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpPfSetDeptHeadWriteService : IErpPfSetDeptHeadWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpPfSetDeptHeadWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        string? departmentCode,
        long headUserId,
        CancellationToken cancellationToken = default)
    {
        var dept = Clip((departmentCode ?? string.Empty).Trim(), 32);
        if (dept.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Department code is required");
        }

        if (headUserId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select a department head");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_pf_dept_heads", "department_code", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_pf_dept_heads", "head_user_id", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Process-flow department-head table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_pf_dept_heads` (`department_code`,`head_user_id`,`time_updated`) VALUES (?,?,?) ON DUPLICATE KEY UPDATE `head_user_id` = VALUES(`head_user_id`), `time_updated` = VALUES(`time_updated`)"),
            cancellationToken,
            dept, headUserId, now).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Department head saved", headUserId);
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
