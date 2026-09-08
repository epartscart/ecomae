using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_proc_category_save</c> / ajax <c>proc_category_save</c> twin.
/// UPDATE <c>epc_proc_category</c> when <c>id</c> &gt; 0, else INSERT.
/// Policy save and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpProcCategorySaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpProcCategorySaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpProcCategorySaveWriteRequest(
    long Id = 0,
    long CompanyId = 0,
    string? Code = null,
    string? Name = null,
    long ParentId = 0,
    string? DefaultAccount = null,
    int? Active = null);

public sealed class ErpProcCategorySaveWriteService : IErpProcCategorySaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpProcCategorySaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpProcCategorySaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = (request.Code ?? string.Empty).Trim();
        var name = (request.Name ?? string.Empty).Trim();
        if (code.Length == 0 || name.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Category code and name are required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var parentId = request.ParentId < 0 ? 0 : request.ParentId;
        var account = request.DefaultAccount ?? string.Empty;
        var active = request.Id > 0
            ? (request.Active is null || request.Active == 0 ? 0 : 1)
            : (request.Active is null ? 1 : (request.Active == 0 ? 0 : 1));

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_proc_category", "code", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_proc_category", "name", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Category table is not provisioned");
        }

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_proc_category` SET `code`=?, `name`=?, `parent_id`=?, `default_account`=?, `active`=? WHERE `id`=?"),
                cancellationToken,
                code,
                name,
                parentId,
                account,
                active,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Category saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_proc_category` (`company_id`,`code`,`name`,`parent_id`,`default_account`,`active`,`time_created`) VALUES (?,?,?,?,?,?,?)"),
            cancellationToken,
            companyId,
            code,
            name,
            parentId,
            account,
            active,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Category saved", id);
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
