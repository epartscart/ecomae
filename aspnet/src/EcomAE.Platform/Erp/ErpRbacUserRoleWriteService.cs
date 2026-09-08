using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_rbac_user_assign_role</c> / ajax <c>rbac_user_role</c> twin.
/// INSERT IGNORE or DELETE <c>epc_rbac_user_role</c>.
/// Privilege/duty/role save and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpRbacUserRoleWriteService
{
    Task<ErpSimpleWriteResult> AssignAsync(
        ErpRbacUserRoleWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpRbacUserRoleWriteRequest(
    long CompanyId = 0,
    long UserId = 0,
    long RoleId = 0,
    int? Assign = null);

public sealed class ErpRbacUserRoleWriteService : IErpRbacUserRoleWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpRbacUserRoleWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AssignAsync(
        ErpRbacUserRoleWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var assign = request.Assign is null || request.Assign != 0;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_rbac_user_role", "user_id", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_rbac_user_role", "role_id", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "User role table is not provisioned");
        }

        if (assign)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT IGNORE INTO `epc_rbac_user_role` (`company_id`,`user_id`,`role_id`) VALUES (?,?,?)"),
                cancellationToken,
                companyId,
                request.UserId,
                request.RoleId).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("DELETE FROM `epc_rbac_user_role` WHERE `company_id`=? AND `user_id`=? AND `role_id`=?"),
                cancellationToken,
                companyId,
                request.UserId,
                request.RoleId).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok("User role updated", request.RoleId);
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
