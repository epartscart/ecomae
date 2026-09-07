using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_dim_save</c> / <c>epc_erp_dim_save_from_post</c> twin.
/// Schema-ensure stays PHP — missing tables refuse instead of CREATE.
/// </summary>
public interface IErpDimensionWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        string? entityType,
        long entityId,
        IReadOnlyDictionary<string, long>? dim,
        CancellationToken cancellationToken = default);
}

public sealed class ErpDimensionWriteService : IErpDimensionWriteService
{
    private static readonly Regex EntityTypeOk = new("^[a-z0-9_]{1,40}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly (string Key, string Table)[] FixedDims =
    [
        ("business_unit", "epc_erp_pm_business_units"),
        ("legal_entity", "epc_erp_pm_legal_entities"),
        ("class_unit", "epc_erp_pm_class_units")
    ];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpDimensionWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        string? entityType,
        long entityId,
        IReadOnlyDictionary<string, long>? dim,
        CancellationToken cancellationToken = default)
    {
        var type = (entityType ?? string.Empty).Trim().ToLowerInvariant();
        if (!EntityTypeOk.IsMatch(type) || entityId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Entity type and id required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_erp_dim_links", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Dimension tables are not provisioned");
        }

        var specs = await LoadSpecsAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_erp_dim_links` WHERE `entity_type` = ? AND `entity_id` = ?"),
            cancellationToken,
            type,
            entityId);

        var saved = 0;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (dim is { Count: > 0 })
        {
            foreach (var pair in dim)
            {
                var key = (pair.Key ?? string.Empty).Trim();
                if (pair.Value <= 0 || !specs.TryGetValue(key, out var spec))
                {
                    continue;
                }

                var option = spec.FirstOrDefault(o => o.Id == pair.Value);
                if (option is null || (option.Code.Length == 0 && option.Label.Length == 0))
                {
                    continue;
                }

                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("""
                        INSERT INTO `epc_erp_dim_links`
                        (`entity_type`,`entity_id`,`dim_key`,`ref_id`,`value_code`,`value_label`,`time_created`)
                        VALUES (?,?,?,?,?,?,?)
                        """),
                    cancellationToken,
                    type,
                    entityId,
                    key,
                    pair.Value,
                    option.Code,
                    option.Label,
                    now);
                saved++;
            }
        }

        return ErpSimpleWriteResult.Ok("Dimensions saved (" + saved.ToString(CultureInfo.InvariantCulture) + ")", saved);
    }

    public static IReadOnlyDictionary<string, long> ParseDimMap(IFormCollection form)
    {
        var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in form.Keys)
        {
            var dimKey = ExtractDimKey(key);
            if (dimKey.Length == 0)
            {
                continue;
            }

            if (long.TryParse(form[key].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0)
            {
                map[dimKey] = id;
            }
        }

        return map;
    }

    public static IReadOnlyDictionary<string, long> ParseDimMap(IReadOnlyDictionary<string, long>? dim)
        => dim ?? new Dictionary<string, long>();

    private static string ExtractDimKey(string raw)
    {
        if (raw.StartsWith("dim[", StringComparison.OrdinalIgnoreCase) && raw.EndsWith(']'))
        {
            return raw[4..^1].Trim();
        }

        if (raw.StartsWith("dim.", StringComparison.OrdinalIgnoreCase))
        {
            return raw[4..].Trim();
        }

        return string.Empty;
    }

    private static async Task<Dictionary<string, List<DimOption>>> LoadSpecsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var specs = new Dictionary<string, List<DimOption>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, table) in FixedDims)
        {
            if (!await TableExistsAsync(connection, table, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            var options = await LoadOptionsAsync(connection, table, cancellationToken).ConfigureAwait(false);
            if (options.Count > 0)
            {
                specs[key] = options;
            }
        }

        if (await TableExistsAsync(connection, "epc_erp_pm_dimensions", cancellationToken).ConfigureAwait(false)
            && await TableExistsAsync(connection, "epc_erp_pm_dimension_values", cancellationToken).ConfigureAwait(false))
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT `id` FROM `epc_erp_pm_dimensions` WHERE `active` = 1 ORDER BY `code`,`name`";
            var ids = new List<long>();
            await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    ids.Add(reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
                }
            }

            foreach (var dimId in ids.Where(id => id > 0))
            {
                var options = new List<DimOption>();
                await using var val = connection.CreateCommand();
                val.CommandText = ErpDb.Positional(
                    "SELECT `id`,`code`,`name` FROM `epc_erp_pm_dimension_values` WHERE `dimension_id` = ? AND `active` = 1 ORDER BY `code`,`name`");
                ErpDb.AddParameters(val, dimId);
                await using var reader = await val.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    options.Add(new DimOption(
                        reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                        reader.IsDBNull(1) ? "" : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? "",
                        reader.IsDBNull(2) ? "" : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? ""));
                }

                if (options.Count > 0)
                {
                    specs["dim" + dimId.ToString(CultureInfo.InvariantCulture)] = options;
                }
            }
        }

        return specs;
    }

    private static async Task<List<DimOption>> LoadOptionsAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        var options = new List<DimOption>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT `id`,`code`,`name` FROM `" + table + "` WHERE `active` = 1 ORDER BY `code`,`name`";
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            options.Add(new DimOption(
                reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                reader.IsDBNull(1) ? "" : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? "",
                reader.IsDBNull(2) ? "" : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? ""));
        }

        return options;
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }

    private sealed record DimOption(long Id, string Code, string Label);
}
