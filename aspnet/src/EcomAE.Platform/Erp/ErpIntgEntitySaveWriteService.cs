using System.Data.Common;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_intg_entity_save</c> / ajax <c>intg_entity_save</c> twin.
/// INSERT/UPDATE <c>epc_intg_entity</c>. Event raise, subscription save, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpIntgEntitySaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpIntgEntitySaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpIntgEntitySaveWriteRequest(
    long CompanyId = 0,
    string? Name = null,
    string? SourceTable = null,
    string? KeyField = null,
    string? Fields = null,
    int? Enabled = null);

public sealed class ErpIntgEntitySaveWriteService : IErpIntgEntitySaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpIntgEntitySaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpIntgEntitySaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = (request.Name ?? string.Empty).Trim();
        var invalid = Validate(name);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        name = Clip(name, 80);
        var sourceTable = Clip(request.SourceTable ?? string.Empty, 120);
        var keyField = Clip(string.IsNullOrWhiteSpace(request.KeyField) ? "id" : request.KeyField.Trim(), 80);
        var fieldsJson = SerializeFields(request.Fields);
        var enabled = request.Enabled is > 0 ? 1 : 0;
        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_intg_entity", "name", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_intg_entity", "source_table", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Integration entity table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_intg_entity` (`company_id`,`name`,`source_table`,`key_field`,`fields_json`,`enabled`,`time_updated`) VALUES (?,?,?,?,?,?,?) ON DUPLICATE KEY UPDATE `source_table`=VALUES(`source_table`), `key_field`=VALUES(`key_field`), `fields_json`=VALUES(`fields_json`), `enabled`=VALUES(`enabled`), `time_updated`=VALUES(`time_updated`)"),
            cancellationToken,
            companyId,
            name,
            sourceTable,
            keyField,
            fieldsJson,
            enabled,
            now).ConfigureAwait(false);
        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT id FROM `epc_intg_entity` WHERE company_id=? AND name=? LIMIT 1"),
            cancellationToken,
            companyId,
            name).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Data entity saved", id);
    }

    public static string? Validate(string name)
        => name.Length == 0 ? "Entity name is required" : null;

    public static string SerializeFields(string? fields)
    {
        var values = ParseFields(fields);
        return JsonSerializer.Serialize(values);
    }

    public static IReadOnlyList<string> ParseFields(string? fields)
    {
        if (string.IsNullOrWhiteSpace(fields))
        {
            return [];
        }

        var raw = fields.Trim();
        if (raw.StartsWith('[') && raw.EndsWith(']'))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<string[]>(raw);
                if (parsed is not null)
                {
                    return parsed
                        .Select(item => (item ?? string.Empty).Trim())
                        .Where(item => item.Length > 0)
                        .ToArray();
                }
            }
            catch (JsonException)
            {
                // fall through to comma split
            }
        }

        return raw
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToArray();
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
