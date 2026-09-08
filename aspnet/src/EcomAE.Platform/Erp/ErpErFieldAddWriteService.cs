using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_er_field_add</c> twin. INSERT <c>epc_er_field</c>.
/// Format save, run generation, and schema ensure stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpErFieldAddWriteService
{
    Task<ErpSimpleWriteResult> AddAsync(
        ErpErFieldAddWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpErFieldAddWriteRequest(
    long FormatId = 0,
    string? Label = null,
    string? SourceKey = null,
    int Ordinal = 0);

public sealed class ErpErFieldAddWriteService : IErpErFieldAddWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpErFieldAddWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddAsync(
        ErpErFieldAddWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var label = (request.Label ?? string.Empty).Trim();
        var key = (request.SourceKey ?? string.Empty).Trim();
        var invalid = Validate(label, key);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        label = Clip(label, 80);
        key = Clip(key, 80);
        var ordinal = request.Ordinal;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_er_format", "id", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Electronic reporting format table is not provisioned");
        }

        if (request.FormatId <= 0
            || await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_er_format` WHERE `id`=? LIMIT 1"),
                cancellationToken,
                request.FormatId).ConfigureAwait(false) <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Format not found");
        }

        if (!await ColumnExistsAsync(connection, "epc_er_field", "label", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_er_field", "source_key", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_er_field", "ordinal", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Electronic reporting field table is not provisioned");
        }

        if (ordinal <= 0)
        {
            ordinal = (int)await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COALESCE(MAX(`ordinal`),0)+1 FROM `epc_er_field` WHERE `format_id`=?"),
                cancellationToken,
                request.FormatId).ConfigureAwait(false);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_er_field` (`format_id`,`label`,`source_key`,`ordinal`) VALUES (?,?,?,?)"),
            cancellationToken,
            request.FormatId,
            label,
            key,
            ordinal).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Field added", id);
    }

    public static string? Validate(string label, string sourceKey)
    {
        if (label.Length == 0 || sourceKey.Length == 0)
        {
            return "Field label and source key are required";
        }

        return null;
    }

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
