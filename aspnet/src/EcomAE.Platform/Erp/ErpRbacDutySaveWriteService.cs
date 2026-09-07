using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_rbac_duty_save</c> / ajax <c>rbac_duty_save</c> twin.
/// UPSERT <c>epc_rbac_duty</c> on <c>company_id</c>+<c>code</c>.
/// Privilege save, role save, attach helpers, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpRbacDutySaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpRbacDutySaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpRbacDutySaveWriteRequest(
    long CompanyId = 0,
    string? Code = null,
    string? Name = null);

public sealed class ErpRbacDutySaveWriteService : IErpRbacDutySaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpRbacDutySaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpRbacDutySaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = (request.Code ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Duty code is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var name = request.Name ?? string.Empty;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_rbac_duty", "code", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_rbac_duty", "name", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Duty table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_rbac_duty` (`company_id`,`code`,`name`) VALUES (?,?,?) "
                + "ON DUPLICATE KEY UPDATE `name`=VALUES(`name`)"),
            cancellationToken,
            companyId,
            code,
            name).ConfigureAwait(false);
        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_rbac_duty` WHERE `company_id`=? AND `code`=? LIMIT 1"),
            cancellationToken,
            companyId,
            code).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Duty saved", id);
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
