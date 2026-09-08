using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_pf_process_save</c> / ajax <c>pf_process_save</c> twin.
/// INSERT/UPDATE <c>epc_pf_processes</c>. Step save, case start/act, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpPfProcessSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpPfProcessSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPfProcessSaveWriteRequest(
    long Id = 0,
    string? Name = null,
    string? Description = null,
    string? Category = null,
    int? Active = null);

public sealed class ErpPfProcessSaveWriteService : IErpPfProcessSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpPfProcessSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpPfProcessSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = Clip((request.Name ?? string.Empty).Trim(), 160);
        var invalid = Validate(name);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var description = request.Description ?? string.Empty;
        var category = Clip(request.Category ?? "general", 64);
        var active = ResolveActive(request.Active);
        var id = request.Id;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_pf_processes", "name", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_pf_processes", "category", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Process template table is not provisioned");
        }

        if (id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_pf_processes` SET `name`=?, `description`=?, `category`=?, `active`=? WHERE `id`=?"),
                cancellationToken,
                name,
                description,
                category,
                active,
                id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Process saved", id);
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_pf_processes` (`name`,`description`,`category`,`active`,`time_created`) VALUES (?,?,?,?,?)"),
            cancellationToken,
            name,
            description,
            category,
            1,
            now).ConfigureAwait(false);
        id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Process saved", id);
    }

    public static string? Validate(string name)
        => name.Length == 0 ? "Process name is required" : null;

    /// <summary>PHP <c>!empty($active) ? 1 : (isset($active) ? 0 : 1)</c>.</summary>
    public static int ResolveActive(int? active)
        => active is null ? 1 : (active.Value != 0 ? 1 : 0);

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];

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
