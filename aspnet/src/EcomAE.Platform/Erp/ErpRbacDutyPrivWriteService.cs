using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_rbac_duty_attach_priv</c> / ajax <c>rbac_duty_priv</c> twin.
/// INSERT IGNORE or DELETE <c>epc_rbac_duty_priv</c>.
/// Privilege/duty/role save and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpRbacDutyPrivWriteService
{
    Task<ErpSimpleWriteResult> AttachAsync(
        ErpRbacDutyPrivWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpRbacDutyPrivWriteRequest(
    long DutyId = 0,
    long PrivilegeId = 0,
    int? Attach = null,
    int ActorUserId = 0);

public sealed class ErpRbacDutyPrivWriteService : IErpRbacDutyPrivWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpAuditLogWriter _audit;

    public ErpRbacDutyPrivWriteService(IErpWriteConnectionFactory connections, IErpAuditLogWriter audit)
    {
        _connections = connections;
        _audit = audit;
    }

    public async Task<ErpSimpleWriteResult> AttachAsync(
        ErpRbacDutyPrivWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var attach = request.Attach is null || request.Attach != 0;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_rbac_duty_priv", "duty_id", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_rbac_duty_priv", "privilege_id", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Duty privilege table is not provisioned");
        }

        if (attach)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT IGNORE INTO `epc_rbac_duty_priv` (`duty_id`,`privilege_id`) VALUES (?,?)"),
                cancellationToken,
                request.DutyId,
                request.PrivilegeId).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("DELETE FROM `epc_rbac_duty_priv` WHERE `duty_id`=? AND `privilege_id`=?"),
                cancellationToken,
                request.DutyId,
                request.PrivilegeId).ConfigureAwait(false);
        }

        await _audit.LogAsync(
            connection,
            null,
            request.ActorUserId,
            attach ? "rbac_duty_priv_attach" : "rbac_duty_priv_detach",
            "rbac_duty_privilege",
            request.DutyId,
            attach ? "ERP privilege attached to duty" : "ERP privilege detached from duty",
            new Dictionary<string, string?>
            {
                ["duty_id"] = request.DutyId.ToString(),
                ["privilege_id"] = request.PrivilegeId.ToString(),
            },
            cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Duty privileges updated", request.DutyId);
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
