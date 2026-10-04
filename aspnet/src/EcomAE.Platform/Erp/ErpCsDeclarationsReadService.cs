using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_cs_list_declarations</c> twin (epc_custom_shipping.php):
/// ensure schema, filtered declaration list ordered by id DESC (limit 200 as the
/// ajax caller), field_data / box_data / pdf_autofill_keys JSON-decoded, plus
/// epc_cs_attach_item_counts. Read-only apart from the PHP schema ensure.
/// </summary>
public interface IErpCsDeclarationsReadService
{
    Task<ErpCsDeclarationsResult> ListAsync(
        ErpCsDeclarationsFilters filters,
        CancellationToken cancellationToken = default);
}

public sealed record ErpCsDeclarationsFilters(
    string Category = "",
    string Status = "",
    string DeclarationType = "",
    string From = "",
    string To = "",
    string Company = "",
    string CustomsEmirate = "",
    string Q = "",
    int Limit = 200);

public sealed record ErpCsDeclarationsResult(
    bool Ok,
    string Message,
    IReadOnlyList<Dictionary<string, object?>> Declarations,
    int Writes);

public sealed class ErpCsDeclarationsReadService : IErpCsDeclarationsReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpCsDeclarationsReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpCsDeclarationsResult> ListAsync(
        ErpCsDeclarationsFilters filters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filters);
        if (!_connections.IsConfigured)
        {
            return new(false, "TenantRegistry DB is not configured.", [], 0);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var writes = await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var sql = "SELECT * FROM `epc_custom_shipping_declarations` WHERE 1=1";
        var args = new List<object?>();
        if (filters.Category.Length > 0) { sql += " AND `category` = ?"; args.Add(filters.Category); }
        if (filters.Status.Length > 0) { sql += " AND `status` = ?"; args.Add(filters.Status); }
        if (filters.DeclarationType.Length > 0) { sql += " AND `declaration_type` = ?"; args.Add(filters.DeclarationType); }
        if (filters.From.Length > 0) { sql += " AND (`entry_date` IS NULL OR `entry_date` >= ?)"; args.Add(filters.From); }
        if (filters.To.Length > 0) { sql += " AND (`entry_date` IS NULL OR `entry_date` <= ?)"; args.Add(filters.To); }
        if (filters.Company.Length > 0) { sql += " AND `company` LIKE ?"; args.Add("%" + filters.Company + "%"); }
        if (filters.CustomsEmirate.Length > 0) { sql += " AND `customs_emirate` = ?"; args.Add(filters.CustomsEmirate); }
        if (filters.Q.Length > 0)
        {
            var q = "%" + filters.Q + "%";
            sql += " AND (`declaration_number` LIKE ? OR `company` LIKE ? OR `supplier_detail` LIKE ? OR `srv_number` LIKE ? OR `bl_number` LIKE ? OR `ld_po_number` LIKE ?)";
            args.AddRange(new object?[] { q, q, q, q, q, q });
        }
        sql += " ORDER BY `id` DESC LIMIT " + filters.Limit;

        var rows = new List<Dictionary<string, object?>>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(sql);
            ErpDb.AddParameters(command, args.ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var row = new Dictionary<string, object?>(StringComparer.Ordinal);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var v = reader.GetValue(i);
                    row[reader.GetName(i)] = v is DBNull ? null : v;
                }
                rows.Add(row);
            }
        }

        foreach (var row in rows)
        {
            row["field_data"] = DecodeJson(row.TryGetValue("field_data", out var fd) ? fd : null);
            row["box_data"] = DecodeJson(row.TryGetValue("box_data", out var bd) ? bd : null);
            row["pdf_autofill_keys"] = DecodeJson(row.TryGetValue("pdf_autofill_keys", out var pk) ? pk : null);
        }

        await AttachItemCountsAsync(connection, rows, cancellationToken).ConfigureAwait(false);
        return new(true, "OK", rows, writes);
    }

    private static object? DecodeJson(object? raw)
    {
        var s = Convert.ToString(raw, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(s)) return new Dictionary<string, object?>();
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(s);
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?>();
        }
    }

    private static async Task AttachItemCountsAsync(DbConnection connection, List<Dictionary<string, object?>> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return;
        var ids = rows
            .Select(r => r.TryGetValue("id", out var v) ? Convert.ToInt64(v) : 0L)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        if (ids.Count == 0) return;

        var placeholders = string.Join(",", Enumerable.Range(0, ids.Count).Select(i => "@p" + i));
        var counts = new Dictionary<long, long>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"SELECT `declaration_id`, COUNT(*) AS cnt FROM `epc_custom_shipping_declaration_items` WHERE `declaration_id` IN ({placeholders}) GROUP BY `declaration_id`";
            ErpDb.AddParameters(command, ids.Cast<object?>().ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                counts[Convert.ToInt64(reader.GetValue(0))] = Convert.ToInt64(reader.GetValue(1));
            }
        }
        foreach (var row in rows)
        {
            var id = row.TryGetValue("id", out var v) ? Convert.ToInt64(v) : 0L;
            row["item_count"] = counts.TryGetValue(id, out var c) ? c : 0L;
        }
    }

    /// <summary>PHP epc_cs_ensure_schema + line-items + box-schema ALTERs.</summary>
    private static async Task<int> EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var writes = 0;
        writes += await ErpDb.ExecuteAsync(connection, null,
            "CREATE TABLE IF NOT EXISTS `epc_custom_shipping_declarations` (`id` INT UNSIGNED NOT NULL AUTO_INCREMENT, `category` VARCHAR(32) NOT NULL DEFAULT 'import', `declaration_type` VARCHAR(191) NOT NULL DEFAULT '', `status` VARCHAR(32) NOT NULL DEFAULT 'draft', `company` VARCHAR(255) NOT NULL DEFAULT '', `customs_emirate` VARCHAR(64) NOT NULL DEFAULT '', `entry_date` DATE NULL, `declaration_date` DATE NULL, `declaration_number` VARCHAR(64) NOT NULL DEFAULT '', `bl_number` VARCHAR(64) NOT NULL DEFAULT '', `bl_date` DATE NULL, `srv_number` VARCHAR(64) NOT NULL DEFAULT '', `lc_dc_number` VARCHAR(128) NOT NULL DEFAULT '', `ld_po_number` VARCHAR(128) NOT NULL DEFAULT '', `supplier_detail` VARCHAR(255) NOT NULL DEFAULT '', `currency` VARCHAR(16) NOT NULL DEFAULT 'AED', `invoice_amount_aed` DECIMAL(18,2) NOT NULL DEFAULT 0, `total_cost_aed` DECIMAL(18,2) NOT NULL DEFAULT 0, `remarks` TEXT, `field_data` LONGTEXT, `created_at` INT UNSIGNED NOT NULL DEFAULT 0, `updated_at` INT UNSIGNED NOT NULL DEFAULT 0, `created_by` INT UNSIGNED NOT NULL DEFAULT 0, PRIMARY KEY (`id`), KEY `idx_cs_category` (`category`), KEY `idx_cs_status` (`status`), KEY `idx_cs_declaration_date` (`declaration_date`), KEY `idx_cs_declaration_type` (`declaration_type`(64))) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            cancellationToken).ConfigureAwait(false);
        writes += await ErpDb.ExecuteAsync(connection, null,
            "CREATE TABLE IF NOT EXISTS `epc_custom_shipping_declaration_items` (`id` INT UNSIGNED NOT NULL AUTO_INCREMENT, `declaration_id` INT UNSIGNED NOT NULL, `line_number` INT UNSIGNED NOT NULL DEFAULT 1, `hs_code` VARCHAR(32) NOT NULL DEFAULT '', `country_of_origin` VARCHAR(64) NOT NULL DEFAULT '', `description` VARCHAR(512) NOT NULL DEFAULT '', `quantity` DECIMAL(18,4) NOT NULL DEFAULT 0, `unit` VARCHAR(16) NOT NULL DEFAULT 'PCS', `volume` DECIMAL(18,4) NOT NULL DEFAULT 0, `volume_unit` VARCHAR(16) NOT NULL DEFAULT 'CBM', `amount` DECIMAL(18,2) NOT NULL DEFAULT 0, `weight` DECIMAL(18,4) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), KEY `idx_cs_item_declaration` (`declaration_id`), KEY `idx_cs_item_line` (`declaration_id`, `line_number`)) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            cancellationToken).ConfigureAwait(false);
        writes += await EnsureBoxSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        return writes;
    }

    /// <summary>PHP epc_cs_ensure_box_schema + epc_cs_ensure_line_item_box_columns.</summary>
    private static async Task<int> EnsureBoxSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var writes = 0;
        var cols = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SHOW COLUMNS FROM `epc_custom_shipping_declarations`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cols.Add(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "");
            }
        }
        catch (Exception)
        {
            return writes;
        }

        async Task<int> AddCol(string name, string ddl)
        {
            if (cols.Contains(name)) return 0;
            try
            {
                return await ErpDb.ExecuteAsync(connection, null, $"ALTER TABLE `epc_custom_shipping_declarations` ADD COLUMN `{name}` {ddl}", cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        writes += await AddCol("box_data", "LONGTEXT NULL AFTER `field_data`");
        writes += await AddCol("pdf_autofill_keys", "TEXT NULL AFTER `box_data`");
        writes += await AddCol("pdf_file_path", "VARCHAR(512) NULL AFTER `pdf_autofill_keys`");
        writes += await AddCol("pdf_file_name", "VARCHAR(255) NULL AFTER `pdf_file_path`");
        writes += await AddCol("box_45_invoice_term", "VARCHAR(16) NULL DEFAULT NULL AFTER `pdf_file_name`");
        writes += await AddCol("box_45_invoice_value", "DECIMAL(18,2) NULL DEFAULT NULL AFTER `box_45_invoice_term`");
        writes += await AddCol("box_45_customs_inspection", "VARCHAR(8) NULL DEFAULT NULL AFTER `box_45_invoice_value`");
        writes += await AddCol("invoice_term", "VARCHAR(16) NOT NULL DEFAULT '' AFTER `invoice_amount_aed`");
        writes += await AddCol("customs_inspection_required", "VARCHAR(8) NOT NULL DEFAULT '' AFTER `invoice_term`");

        try
        {
            writes += await ErpDb.ExecuteAsync(connection, null,
                "ALTER TABLE `epc_custom_shipping_declarations` MODIFY `declaration_number` VARCHAR(64) NULL DEFAULT NULL",
                cancellationToken).ConfigureAwait(false);
            writes += await ErpDb.ExecuteAsync(connection, null,
                "UPDATE `epc_custom_shipping_declarations` SET `declaration_number` = NULL WHERE TRIM(COALESCE(`declaration_number`, '')) = ''",
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SHOW INDEX FROM `epc_custom_shipping_declarations` WHERE Key_name = 'uq_cs_declaration_number'";
            var idx = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (idx is null)
            {
                writes += await ErpDb.ExecuteAsync(connection, null,
                    "ALTER TABLE `epc_custom_shipping_declarations` ADD UNIQUE KEY `uq_cs_declaration_number` (`declaration_number`)",
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
        }

        // epc_cs_ensure_line_item_box_columns
        var itemCols = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SHOW COLUMNS FROM `epc_custom_shipping_declaration_items`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                itemCols.Add(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "");
            }
        }
        catch (Exception)
        {
            return writes;
        }
        var extra = new (string Name, string Ddl)[]
        {
            ("foreign_value", "DECIMAL(18,2) NOT NULL DEFAULT 0"),
            ("currency", "VARCHAR(16) NOT NULL DEFAULT ''"),
            ("currency_rate", "DECIMAL(18,6) NOT NULL DEFAULT 0"),
            ("cif_local_value", "DECIMAL(18,2) NOT NULL DEFAULT 0"),
            ("duty_rate", "DECIMAL(8,2) NOT NULL DEFAULT 0"),
            ("income_type", "VARCHAR(32) NOT NULL DEFAULT ''"),
            ("total_duty_aed", "DECIMAL(18,2) NOT NULL DEFAULT 0"),
            ("packages_qty", "DECIMAL(18,4) NOT NULL DEFAULT 0"),
            ("packages_type", "VARCHAR(64) NOT NULL DEFAULT ''"),
            ("weight_net", "DECIMAL(18,4) NOT NULL DEFAULT 0"),
            ("weight_gross", "DECIMAL(18,4) NOT NULL DEFAULT 0"),
            ("aip_no", "VARCHAR(64) NOT NULL DEFAULT ''"),
            ("aip_duty", "VARCHAR(64) NOT NULL DEFAULT ''"),
        };
        foreach (var (name, ddl) in extra)
        {
            if (itemCols.Contains(name)) continue;
            try
            {
                writes += await ErpDb.ExecuteAsync(connection, null,
                    $"ALTER TABLE `epc_custom_shipping_declaration_items` ADD COLUMN `{name}` {ddl}",
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }
        return writes;
    }
}
