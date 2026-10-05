using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_rbac_role_attach_duty</c> / ajax <c>rbac_role_duty</c> twin.
/// INSERT IGNORE or DELETE <c>epc_rbac_role_duty</c>.
/// Privilege/duty/role save and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpRbacRoleDutyWriteService
{
    Task<ErpSimpleWriteResult> AttachAsync(
        ErpRbacRoleDutyWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpRbacRoleDutyWriteRequest(
    long RoleId = 0,
    long DutyId = 0,
    int? Attach = null,
    int ActorUserId = 0);

public sealed class ErpRbacRoleDutyWriteService : IErpRbacRoleDutyWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpAuditLogWriter _audit;

    public ErpRbacRoleDutyWriteService(IErpWriteConnectionFactory connections, IErpAuditLogWriter audit)
    {
        _connections = connections;
        _audit = audit;
    }

    public async Task<ErpSimpleWriteResult> AttachAsync(
        ErpRbacRoleDutyWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var attach = request.Attach is null || request.Attach != 0;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        await ErpLazySchema.EnsureRbacAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_rbac_role_duty", "role_id", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_rbac_role_duty", "duty_id", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Role duty table is not provisioned");
        }

        if (attach)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT IGNORE INTO `epc_rbac_role_duty` (`role_id`,`duty_id`) VALUES (?,?)"),
                cancellationToken,
                request.RoleId,
                request.DutyId).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("DELETE FROM `epc_rbac_role_duty` WHERE `role_id`=? AND `duty_id`=?"),
                cancellationToken,
                request.RoleId,
                request.DutyId).ConfigureAwait(false);
        }

        await _audit.LogAsync(
            connection,
            null,
            request.ActorUserId,
            attach ? "rbac_role_duty_attach" : "rbac_role_duty_detach",
            "rbac_role_duty",
            request.RoleId,
            attach ? "ERP duty attached to role" : "ERP duty detached from role",
            new Dictionary<string, string?>
            {
                ["role_id"] = request.RoleId.ToString(),
                ["duty_id"] = request.DutyId.ToString(),
            },
            cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Role duties updated", request.RoleId);
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
