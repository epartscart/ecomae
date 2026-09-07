using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_rbac_privilege_save</c> / ajax <c>rbac_priv_save</c> twin.
/// UPSERT <c>epc_rbac_privilege</c> on <c>company_id</c>+<c>code</c>.
/// Duty save, role save, attach helpers, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpRbacPrivSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpRbacPrivSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpRbacPrivSaveWriteRequest(
    long CompanyId = 0,
    string? Code = null,
    string? Name = null,
    string? AccessLevel = null);

public sealed class ErpRbacPrivSaveWriteService : IErpRbacPrivSaveWriteService
{
    public static readonly HashSet<string> AccessLevels = new(StringComparer.Ordinal)
    {
        "read", "update", "create", "delete", "full",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpRbacPrivSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpRbacPrivSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = (request.Code ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Privilege code is required");
        }

        var level = request.AccessLevel ?? "read";
        if (!AccessLevels.Contains(level))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid access level");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var name = request.Name ?? string.Empty;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_rbac_privilege", "code", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_rbac_privilege", "access_level", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Privilege table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_rbac_privilege` (`company_id`,`code`,`name`,`access_level`) VALUES (?,?,?,?) "
                + "ON DUPLICATE KEY UPDATE `name`=VALUES(`name`), `access_level`=VALUES(`access_level`)"),
            cancellationToken,
            companyId,
            code,
            name,
            level).ConfigureAwait(false);
        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_rbac_privilege` WHERE `company_id`=? AND `code`=? LIMIT 1"),
            cancellationToken,
            companyId,
            code).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Privilege saved", id);
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
