using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live twins of the PHP custom-shipping declaration ajax actions
/// (epc_custom_shipping.php): <c>cs_save_declaration</c> (epc_cs_save_declaration +
/// epc_cs_attach_pdf_to_declaration + epc_cs_redirect_after_save),
/// <c>cs_submit_declaration</c> (epc_cs_submit_declaration) and
/// <c>cs_delete_declaration</c> (epc_cs_delete_declaration) — same schemas, same
/// validations, same messages. PHP's unconditional call to the never-defined
/// <c>epc_cs_sync_company_box06</c> is dead code; its only implied behavior
/// (box_06 → company fill) is already performed explicitly by
/// <c>epc_cs_sync_core_from_boxes</c> and is ported there.
/// </summary>
public interface IErpCsDeclarationWriteService
{
    Task<ErpCsSaveResult> SaveDeclarationAsync(IReadOnlyDictionary<string, string> fields, int adminId, CancellationToken cancellationToken = default);
    Task SubmitDeclarationAsync(long id, CancellationToken cancellationToken = default);
    Task DeleteDeclarationAsync(long id, CancellationToken cancellationToken = default);
}

public sealed record ErpCsSaveResult(long Id, string Redirect, string Message);

public sealed class ErpCsDeclarationWriteService : IErpCsDeclarationWriteService
{
    private static readonly string[] CoreColumns =
        ["company", "customs_emirate", "declaration_type", "entry_date", "declaration_date",
         "declaration_number", "bl_number", "bl_date", "srv_number", "lc_dc_number", "ld_po_number",
         "supplier_detail", "currency", "invoice_amount_aed", "total_cost_aed", "remarks"];

    /// <summary>PHP epc_cs_core_required_field_keys (non-LGP categories).</summary>
    private static readonly string[] CoreRequired = ["company", "customs_emirate", "declaration_type", "entry_date", "declaration_date"];

    private static readonly string[] LgpRequired =
        ["customer_ref_no", "cargo_source", "goods_coming_from", "warehouse_name", "local_company",
         "purpose_of_entry", "warehouse_number", "packing_list", "commercial_invoice"];

    /// <summary>PHP epc_cs_core_fields_mapped_to_boxes + box_03→declaration_type + box_06→company.</summary>
    private static readonly Dictionary<string, string> CoreToBox = new(StringComparer.Ordinal)
    {
        ["declaration_number"] = "box_01",
        ["declaration_date"] = "box_02",
        ["bl_number"] = "box_17",
        ["gross_weight"] = "box_10",
        ["net_weight"] = "box_07",
        ["package_type"] = "box_33",
        ["package_detail"] = "box_16",
        ["port_of_entry"] = "box_20",
        ["port_of_exit"] = "box_46",
        ["description_items"] = "box_23",
        ["currency"] = "box_26",
        ["invoice_amount_aed"] = "box_48",
    };

    private static readonly string[] UnitOptions = ["PCS", "KG", "SET", "PAIR", "M", "L", "BOX", "CTN"];
    private static readonly string[] VolumeUnitOptions = ["CBM", "CFT", "L"];

    /// <summary>PHP epc_cs_declaration_box_definitions — keys + number-typed boxes.</summary>
    private static readonly string[] BoxKeys =
        ["box_01", "box_02", "box_03", "box_04", "box_05", "box_06", "box_07", "box_08", "box_09",
         "box_10", "box_11", "box_12", "box_12a", "box_13", "box_14", "box_15", "box_16", "box_17",
         "box_18", "box_19", "box_20", "box_21", "box_22", "box_23", "box_24", "box_25", "box_26",
         "box_27", "box_28", "box_29", "box_30", "box_31", "box_32", "box_33", "box_34", "box_35",
         "box_36", "box_37", "box_37a", "box_37b", "box_38", "box_39", "box_40", "box_41", "box_42",
         "box_43", "box_44", "box_45", "box_46", "box_47", "box_48", "box_48a", "box_48b", "box_48c",
         "box_49", "box_50", "box_51", "box_52", "box_53", "box_54", "box_55", "box_56", "box_57",
         "box_58", "box_59"];

    private static readonly HashSet<string> NumberBoxes = new(StringComparer.Ordinal)
    {
        "box_25", "box_27", "box_28", "box_29", "box_31", "box_32", "box_34",
        "box_48", "box_48a", "box_48b", "box_48c", "box_49", "box_50", "box_51", "box_52",
    };

    internal static readonly Dictionary<string, string[]> DeclarationTypes = new(StringComparer.Ordinal)
    {
        ["import"] =
        [
            "Import to Local from ROW", "Import to local from FZ", "Import to Local from CW",
            "Import Statistical Declaration", "Import for Re Export to Local from ROW",
            "Import for Re Export to Local from FZ", "Import for Re Export to Local from CW",
            "Import for CW from ROW", "Import to CW from FZ",
            "Import to CW from Local (after temporary admission)", "Courier Import",
            "Import to Local After Temporary Admission",
        ],
        ["export"] =
        [
            "Export from Local to ROW", "Export from Local to FZ", "Export statisitical Declaration",
            "Temporary Export from local to ROW", "Temporay Export from local to FZ",
            "Export from CW to ROW", "Export from CW to FZ",
            "Re Export to ROW (after import for re export)", "Re Export to FZ (after import for Re Export)",
            "Return to FZ after temporary Admission", "Return to ROW after Temporary Admission",
            "Courier Export", "Goods Consumption within FZ",
        ],
        ["transit"] =
        [
            "Transit (ROW to ROW)", "FZ transit in", "FZ transit Out",
            "FZ transit in from GCC and other Emirates FZ and GCC local Market",
            "FZ Transit Between Dubai based FZ", "Courier Transit",
        ],
        ["temp_admission"] =
        [
            "Temporary Admission from ROW to Local", "Temporary Admission from FZ to Local",
            "Temporary Admission from CW to Local",
        ],
        ["transfer"] = ["Transfer of Cargo by Dubai Based CW", "Transfer within a FZ"],
    };

    /// <summary>PHP epc_cs_field_definitions — non-core keys per category become field_data.</summary>
    private static readonly string[] CommonExtraFieldKeys =
        ["package_type", "package_detail", "gross_weight", "net_weight", "description_items",
         "port_of_entry", "port_of_exit", "shipping_terms_inco"];

    private static readonly string[] ImportExtraFieldKeys =
        ["supplier_code_customs", "custom_inspection", "d365_po_reference"];

    private static readonly string[] OutboundExtraFieldKeys =
        ["import_reexport_declaration_ref", "document_expiry_date", "d365_so_reference",
         "customer_ref", "customer_country"];

    private static readonly string[] LgpFieldKeys =
        ["customer_ref_no", "cargo_source", "goods_coming_from", "warehouse_name", "local_company",
         "purpose_of_entry", "warehouse_number", "documents_ref_no", "packing_list",
         "commercial_invoice", "hs_code", "goods_description", "package_type", "quantity",
         "weight_kgs", "volume_cbm", "value_aed", "remarks"];

    private static readonly string[] LineItemKeys =
        ["hs_code", "country_of_origin", "description", "quantity", "unit", "volume", "volume_unit",
         "amount", "weight", "foreign_value", "currency", "currency_rate", "cif_local_value",
         "duty_rate", "income_type", "total_duty_aed", "packages_qty", "packages_type",
         "weight_net", "weight_gross", "aip_no", "aip_duty"];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;
    private readonly TimeProvider _clock;

    public ErpCsDeclarationWriteService(
        IErpWriteConnectionFactory connections,
        Microsoft.AspNetCore.Hosting.IWebHostEnvironment env,
        TimeProvider clock)
    {
        _connections = connections;
        _env = env;
        _clock = clock;
    }

    private string FilesRoot => Path.Combine(Presentation.PhpLegacyAssetBridge.FindRepoRoot(_env), "content", "files");
    private string PdfRoot => Path.Combine(FilesRoot, "epc_custom_shipping_pdfs");

    public async Task<ErpCsSaveResult> SaveDeclarationAsync(IReadOnlyDictionary<string, string> fields, int adminId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("TenantRegistry DB is not configured.");
        }

        var data = new Dictionary<string, string>(fields, StringComparer.Ordinal);
        var boxData = MergeBoxData(data);
        SyncCoreFromBoxes(data, boxData);

        var declNo = DeclarationNumberFromData(data, boxData);
        if (declNo.Length > 0)
        {
            data["declaration_number"] = declNo;
        }

        var lineItems = ParseLineItems(data);

        var category = Field(data, "category", "import");
        var fieldDataKeys = FieldDataKeys(category);
        var extra = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in fieldDataKeys)
        {
            if (CoreColumns.Contains(key, StringComparer.Ordinal))
            {
                continue;
            }
            if (data.TryGetValue(key, out var v))
            {
                extra[key] = v;
            }
        }

        var pdfAutofill = ParseJsonStringArray(Field(data, "pdf_autofill_keys", ""));

        var status = Field(data, "status", "draft");
        if (status is not ("draft" or "submitted" or "cleared"))
        {
            status = "draft";
        }

        var id = ParseLong(Field(data, "id", ""));

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        await AssertUniqueDeclarationNumberAsync(connection, declNo, id, cancellationToken).ConfigureAwait(false);
        ValidateDeclaration(data, category);
        ValidateLineItems(lineItems);

        var now = _clock.GetUtcNow().ToUnixTimeSeconds();
        var fieldJson = JsonSerializer.Serialize(extra);
        var boxJson = JsonSerializer.Serialize(boxData);
        var autofillJson = JsonSerializer.Serialize(pdfAutofill);

        long declarationId;
        if (id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_custom_shipping_declarations` SET `category`=?, `declaration_type`=?, `status`=?, `company`=?, `customs_emirate`=?, `entry_date`=?, `declaration_date`=?, `declaration_number`=?, `bl_number`=?, `bl_date`=?, `srv_number`=?, `lc_dc_number`=?, `ld_po_number`=?, `supplier_detail`=?, `currency`=?, `invoice_amount_aed`=?, `total_cost_aed`=?, `remarks`=?, `field_data`=?, `box_data`=?, `pdf_autofill_keys`=?, `updated_at`=? WHERE `id`=?"),
                cancellationToken,
                category,
                Field(data, "declaration_type", category == "lgp" ? "LGP entry" : "").Trim(),
                status,
                Field(data, "company", Field(data, "local_company", "")).Trim(),
                Field(data, "customs_emirate", "DUBAI").Trim(),
                NullableDate(Field(data, "entry_date", "")),
                NullableDate(Field(data, "declaration_date", "")),
                declNo.Length > 0 ? declNo : null,
                Field(data, "bl_number", "").Trim(),
                NullableDate(Field(data, "bl_date", "")),
                Field(data, "srv_number", "").Trim(),
                Field(data, "lc_dc_number", "").Trim(),
                Field(data, "ld_po_number", "").Trim(),
                Field(data, "supplier_detail", "").Trim(),
                Field(data, "currency", "AED").Trim(),
                ParseDecimal(Field(data, "invoice_amount_aed", Field(data, "value_aed", "0"))),
                ParseDecimal(Field(data, "total_cost_aed", Field(data, "value_aed", "0"))),
                Field(data, "remarks", "").Trim(),
                fieldJson,
                boxJson,
                autofillJson,
                now,
                id).ConfigureAwait(false);
            await SaveItemsAsync(connection, id, lineItems, cancellationToken).ConfigureAwait(false);
            await AttachPdfAsync(connection, id, data, cancellationToken).ConfigureAwait(false);
            declarationId = id;
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_custom_shipping_declarations` (`category`, `declaration_type`, `status`, `company`, `customs_emirate`, `entry_date`, `declaration_date`, `declaration_number`, `bl_number`, `bl_date`, `srv_number`, `lc_dc_number`, `ld_po_number`, `supplier_detail`, `currency`, `invoice_amount_aed`, `total_cost_aed`, `remarks`, `field_data`, `box_data`, `pdf_autofill_keys`, `created_at`, `updated_at`, `created_by`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
                cancellationToken,
                category,
                Field(data, "declaration_type", category == "lgp" ? "LGP entry" : "").Trim(),
                status,
                Field(data, "company", Field(data, "local_company", "")).Trim(),
                Field(data, "customs_emirate", "DUBAI").Trim(),
                NullableDate(Field(data, "entry_date", "")),
                NullableDate(Field(data, "declaration_date", "")),
                declNo.Length > 0 ? declNo : null,
                Field(data, "bl_number", "").Trim(),
                NullableDate(Field(data, "bl_date", "")),
                Field(data, "srv_number", "").Trim(),
                Field(data, "lc_dc_number", "").Trim(),
                Field(data, "ld_po_number", "").Trim(),
                Field(data, "supplier_detail", "").Trim(),
                Field(data, "currency", "AED").Trim(),
                ParseDecimal(Field(data, "invoice_amount_aed", Field(data, "value_aed", "0"))),
                ParseDecimal(Field(data, "total_cost_aed", Field(data, "value_aed", "0"))),
                Field(data, "remarks", "").Trim(),
                fieldJson,
                boxJson,
                autofillJson,
                now,
                now,
                adminId).ConfigureAwait(false);
            declarationId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            await SaveItemsAsync(connection, declarationId, lineItems, cancellationToken).ConfigureAwait(false);
            await AttachPdfAsync(connection, declarationId, data, cancellationToken).ConfigureAwait(false);
        }

        var from = Field(data, "from", "");
        var to = Field(data, "to", "");
        if (from.Length == 0)
        {
            from = _clock.GetUtcNow().ToString("yyyy-MM-01", CultureInfo.InvariantCulture);
        }
        if (to.Length == 0)
        {
            to = _clock.GetUtcNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        var redirect = "/erp?area=custom_shipping&tab=custom_shipping&from=" + Uri.EscapeDataString(from)
            + "&to=" + Uri.EscapeDataString(to) + "&cs_view=reports&cs_report=search_results";
        return new ErpCsSaveResult(declarationId, redirect, "Declaration saved");
    }

    public async Task SubmitDeclarationAsync(long id, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("TenantRegistry DB is not configured.");
        }
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_custom_shipping_declarations` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            id).ConfigureAwait(false);
        if (exists <= 0)
        {
            throw new ErpWriteException("Declaration not found");
        }
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_custom_shipping_declarations` SET `status` = ?, `updated_at` = ? WHERE `id` = ?"),
            cancellationToken,
            "submitted",
            _clock.GetUtcNow().ToUnixTimeSeconds(),
            id).ConfigureAwait(false);
    }

    public async Task DeleteDeclarationAsync(long id, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("TenantRegistry DB is not configured.");
        }
        if (id <= 0)
        {
            throw new ErpWriteException("Invalid declaration id");
        }
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var pdfPath = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `pdf_file_path` FROM `epc_custom_shipping_declarations` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            id).ConfigureAwait(false);
        if (pdfPath is null)
        {
            throw new ErpWriteException("Declaration not found");
        }
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_custom_shipping_declaration_items` WHERE `declaration_id` = ?"),
            cancellationToken,
            id).ConfigureAwait(false);
        if (pdfPath.Length > 0)
        {
            var rel = pdfPath.Replace('\\', '/').TrimStart('/');
            if (rel.Contains("content/files/epc_custom_shipping_pdfs/", StringComparison.Ordinal))
            {
                var full = Path.Combine(Presentation.PhpLegacyAssetBridge.FindRepoRoot(_env), rel.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(full))
                {
                    File.Delete(full);
                }
            }
        }
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_custom_shipping_declarations` WHERE `id` = ?"),
            cancellationToken,
            id).ConfigureAwait(false);
    }

    /// <summary>PHP epc_cs_ensure_schema + box/line-item columns.</summary>
    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            null,
            "CREATE TABLE IF NOT EXISTS `epc_custom_shipping_declarations` ("
            + " `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,"
            + " `category` VARCHAR(32) NOT NULL DEFAULT 'import',"
            + " `declaration_type` VARCHAR(191) NOT NULL DEFAULT '',"
            + " `status` VARCHAR(32) NOT NULL DEFAULT 'draft',"
            + " `company` VARCHAR(255) NOT NULL DEFAULT '',"
            + " `customs_emirate` VARCHAR(64) NOT NULL DEFAULT '',"
            + " `entry_date` DATE NULL,"
            + " `declaration_date` DATE NULL,"
            + " `declaration_number` VARCHAR(64) NOT NULL DEFAULT '',"
            + " `bl_number` VARCHAR(64) NOT NULL DEFAULT '',"
            + " `bl_date` DATE NULL,"
            + " `srv_number` VARCHAR(64) NOT NULL DEFAULT '',"
            + " `lc_dc_number` VARCHAR(128) NOT NULL DEFAULT '',"
            + " `ld_po_number` VARCHAR(128) NOT NULL DEFAULT '',"
            + " `supplier_detail` VARCHAR(255) NOT NULL DEFAULT '',"
            + " `currency` VARCHAR(16) NOT NULL DEFAULT 'AED',"
            + " `invoice_amount_aed` DECIMAL(18,2) NOT NULL DEFAULT 0,"
            + " `total_cost_aed` DECIMAL(18,2) NOT NULL DEFAULT 0,"
            + " `remarks` TEXT,"
            + " `field_data` LONGTEXT,"
            + " `created_at` INT UNSIGNED NOT NULL DEFAULT 0,"
            + " `updated_at` INT UNSIGNED NOT NULL DEFAULT 0,"
            + " `created_by` INT UNSIGNED NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`),"
            + " KEY `idx_cs_category` (`category`),"
            + " KEY `idx_cs_status` (`status`),"
            + " KEY `idx_cs_declaration_date` (`declaration_date`),"
            + " KEY `idx_cs_declaration_type` (`declaration_type`(64))"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            "CREATE TABLE IF NOT EXISTS `epc_custom_shipping_declaration_items` ("
            + " `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,"
            + " `declaration_id` INT UNSIGNED NOT NULL,"
            + " `line_number` INT UNSIGNED NOT NULL DEFAULT 1,"
            + " `hs_code` VARCHAR(32) NOT NULL DEFAULT '',"
            + " `country_of_origin` VARCHAR(64) NOT NULL DEFAULT '',"
            + " `description` VARCHAR(512) NOT NULL DEFAULT '',"
            + " `quantity` DECIMAL(18,4) NOT NULL DEFAULT 0,"
            + " `unit` VARCHAR(16) NOT NULL DEFAULT 'PCS',"
            + " `volume` DECIMAL(18,4) NOT NULL DEFAULT 0,"
            + " `volume_unit` VARCHAR(16) NOT NULL DEFAULT 'CBM',"
            + " `amount` DECIMAL(18,2) NOT NULL DEFAULT 0,"
            + " `weight` DECIMAL(18,4) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`),"
            + " KEY `idx_cs_item_declaration` (`declaration_id`),"
            + " KEY `idx_cs_item_line` (`declaration_id`, `line_number`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8",
            cancellationToken).ConfigureAwait(false);
        await EnsureColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>PHP epc_cs_ensure_box_schema + epc_cs_ensure_line_item_box_columns.</summary>
    private static async Task EnsureColumnsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var declCols = await ColumnSetAsync(connection, "epc_custom_shipping_declarations", cancellationToken).ConfigureAwait(false);
        var add = new (string Name, string Def)[]
        {
            ("box_data", "LONGTEXT NULL"),
            ("pdf_autofill_keys", "TEXT NULL"),
            ("pdf_file_path", "VARCHAR(512) NULL"),
            ("pdf_file_name", "VARCHAR(255) NULL"),
            ("box_45_invoice_term", "VARCHAR(16) NULL DEFAULT NULL"),
            ("box_45_invoice_value", "DECIMAL(18,2) NULL DEFAULT NULL"),
            ("box_45_customs_inspection", "VARCHAR(8) NULL DEFAULT NULL"),
            ("invoice_term", "VARCHAR(16) NOT NULL DEFAULT ''"),
            ("customs_inspection_required", "VARCHAR(8) NOT NULL DEFAULT ''"),
        };
        foreach (var (name, def) in add)
        {
            if (!declCols.Contains(name))
            {
                await TryAlterAsync(connection, "ALTER TABLE `epc_custom_shipping_declarations` ADD COLUMN `" + name + "` " + def, cancellationToken).ConfigureAwait(false);
            }
        }
        if (declCols.Contains("declaration_number"))
        {
            await TryAlterAsync(connection, "ALTER TABLE `epc_custom_shipping_declarations` MODIFY `declaration_number` VARCHAR(64) NULL DEFAULT NULL", cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(connection, null, "UPDATE `epc_custom_shipping_declarations` SET `declaration_number` = NULL WHERE TRIM(COALESCE(`declaration_number`, '')) = ''", cancellationToken).ConfigureAwait(false);
            var hasIdx = await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = 'epc_custom_shipping_declarations' AND index_name = 'uq_cs_declaration_number'", cancellationToken).ConfigureAwait(false);
            if (hasIdx == 0)
            {
                await TryAlterAsync(connection, "ALTER TABLE `epc_custom_shipping_declarations` ADD UNIQUE KEY `uq_cs_declaration_number` (`declaration_number`)", cancellationToken).ConfigureAwait(false);
            }
        }

        var itemCols = await ColumnSetAsync(connection, "epc_custom_shipping_declaration_items", cancellationToken).ConfigureAwait(false);
        var itemAdds = new (string Name, string Def)[]
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
        foreach (var (name, def) in itemAdds)
        {
            if (!itemCols.Contains(name))
            {
                await TryAlterAsync(connection, "ALTER TABLE `epc_custom_shipping_declaration_items` ADD COLUMN `" + name + "` " + def, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<HashSet<string>> ColumnSetAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText = "SHOW COLUMNS FROM `" + table + "`";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            set.Add(reader.GetString(0));
        }
        return set;
    }

    private static async Task TryAlterAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        try
        {
            await ErpDb.ExecuteAsync(connection, null, sql, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    /// <summary>PHP epc_cs_merge_box_data_from_post.</summary>
    private static Dictionary<string, JsonElement> MergeBoxData(Dictionary<string, string> data)
    {
        if (data.TryGetValue("box_data", out var boxDataJson) && boxDataJson.Length > 0 && boxDataJson.StartsWith('{'))
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(boxDataJson) ?? [];
            }
            catch (JsonException)
            {
            }
        }

        var boxes = new Dictionary<string, string>(StringComparer.Ordinal);
        if (data.TryGetValue("boxes", out var boxesJson) && boxesJson.StartsWith('{'))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(boxesJson);
                if (parsed is not null)
                {
                    foreach (var (k, v) in parsed)
                    {
                        boxes[k] = JsonScalar(v);
                    }
                }
            }
            catch (JsonException)
            {
            }
        }
        foreach (var key in BoxKeys)
        {
            if (data.TryGetValue("boxes[" + key + "]", out var raw))
            {
                boxes[key] = NumberBoxes.Contains(key) ? DecimalString(raw) : raw.Trim();
            }
        }

        var box45 = ReadStringList(data, "box_45_lines");
        var box54 = ReadStringList(data, "box_54_lines");
        if (data.TryGetValue("company", out var company) && company.Trim().Length > 0)
        {
            boxes["box_06"] = company.Trim();
        }

        var box45Fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var fk in new[] { "invoice_term", "invoice_value", "customs_inspection_required" })
        {
            if (data.TryGetValue(fk, out var raw) && raw.Trim().Length > 0)
            {
                box45Fields[fk] = fk == "invoice_value" ? DecimalString(raw) : raw.Trim();
            }
        }

        var merged = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["boxes"] = JsonSerializer.SerializeToElement(boxes),
            ["box_45_lines"] = JsonSerializer.SerializeToElement(box45),
            ["box_54_lines"] = JsonSerializer.SerializeToElement(box54),
            ["box_45_fields"] = JsonSerializer.SerializeToElement(box45Fields),
        };
        return merged;
    }

    /// <summary>PHP epc_cs_sync_core_from_boxes (incl. the box_06→company fill of the dead epc_cs_sync_company_box06 call).</summary>
    private static void SyncCoreFromBoxes(Dictionary<string, string> data, Dictionary<string, JsonElement> boxData)
    {
        var boxes = BoxMap(boxData);
        foreach (var (coreKey, boxKey) in CoreToBox)
        {
            if ((!data.TryGetValue(coreKey, out var v) || v.Trim().Length == 0) && boxes.TryGetValue(boxKey, out var bv) && bv.Length > 0)
            {
                data[coreKey] = bv;
            }
        }
        if ((!data.TryGetValue("declaration_type", out var dt) || dt.Trim().Length == 0) && boxes.TryGetValue("box_03", out var b3) && b3.Length > 0)
        {
            data["declaration_type"] = b3;
        }
        if ((!data.TryGetValue("company", out var c) || c.Trim().Length == 0) && boxes.TryGetValue("box_06", out var b6) && b6.Length > 0)
        {
            data["company"] = b6;
        }
    }

    /// <summary>PHP epc_cs_declaration_number_from_data.</summary>
    private static string DeclarationNumberFromData(Dictionary<string, string> data, Dictionary<string, JsonElement> boxData)
    {
        var num = Field(data, "declaration_number", "").Trim();
        if (num.Length > 0)
        {
            return num;
        }
        if (BoxMap(boxData).TryGetValue("box_01", out var box01) && box01.Trim().Length > 0)
        {
            return box01.Trim();
        }
        return "";
    }

    /// <summary>PHP epc_cs_assert_unique_declaration_number.</summary>
    internal static async Task AssertUniqueDeclarationNumberAsync(DbConnection connection, string declNo, long excludeId, CancellationToken cancellationToken)
    {
        if (declNo.Length == 0)
        {
            return;
        }
        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_custom_shipping_declarations` WHERE `declaration_number` = ? ORDER BY `id` DESC LIMIT 1"),
            cancellationToken,
            declNo).ConfigureAwait(false);
        if (existing > 0 && existing != excludeId)
        {
            throw new ErpWriteException(excludeId <= 0
                ? "Declaration already saved — open from Reports to edit"
                : "Declaration number " + declNo + " already exists (record #" + existing + "). Each customs declaration copy must be unique.");
        }
    }

    /// <summary>PHP epc_cs_validate_declaration.</summary>
    private static void ValidateDeclaration(Dictionary<string, string> data, string category)
    {
        var required = category == "lgp" ? LgpRequired : CoreRequired;
        var missing = required.Where(k => !data.TryGetValue(k, out var v) || v.Trim().Length == 0).ToList();
        if (missing.Count > 0)
        {
            throw new ErpWriteException("Required fields missing: " + string.Join(", ", missing));
        }
        if (category != "lgp")
        {
            var declType = Field(data, "declaration_type", "").Trim();
            if (declType.Length == 0 || !DeclarationTypes.TryGetValue(category, out var types) || !types.Contains(declType))
            {
                throw new ErpWriteException("Invalid declaration type for category");
            }
        }
    }

    /// <summary>PHP epc_cs_validate_line_items.</summary>
    private static void ValidateLineItems(List<Dictionary<string, JsonElement>> items)
    {
        if (items.Count == 0)
        {
            throw new ErpWriteException("Add at least one declaration line item (HS code, country of origin, quantity).");
        }
        var errors = new List<string>();
        foreach (var item in items)
        {
            var n = (int)JsonNum(item, "line_number");
            if (JsonScalar(item, "hs_code").Length == 0)
            {
                errors.Add("Line " + n + ": HS code is required");
            }
            if (JsonScalar(item, "country_of_origin").Length == 0)
            {
                errors.Add("Line " + n + ": country of origin is required");
            }
            if (JsonNum(item, "quantity") <= 0)
            {
                errors.Add("Line " + n + ": quantity must be greater than zero");
            }
        }
        if (errors.Count > 0)
        {
            throw new ErpWriteException(string.Join("; ", errors));
        }
    }

    /// <summary>PHP epc_cs_parse_line_items_input + epc_cs_normalize_line_item.</summary>
    private static List<Dictionary<string, JsonElement>> ParseLineItems(Dictionary<string, string> data)
    {
        List<JsonElement>? raw = null;
        foreach (var key in new[] { "line_items_json", "line_items" })
        {
            if (data.TryGetValue(key, out var json) && json.StartsWith('['))
            {
                try
                {
                    raw = JsonSerializer.Deserialize<List<JsonElement>>(json);
                }
                catch (JsonException)
                {
                }
                if (raw is not null)
                {
                    break;
                }
            }
        }

        var items = new List<Dictionary<string, JsonElement>>();
        if (raw is null)
        {
            return items;
        }
        var lineNo = 0;
        foreach (var row in raw)
        {
            if (row.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            lineNo++;
            var norm = NormalizeLineItem(row, lineNo);
            if (JsonScalar(norm, "hs_code").Length == 0 && JsonScalar(norm, "country_of_origin").Length == 0
                && JsonNum(norm, "quantity") <= 0 && JsonNum(norm, "volume") <= 0
                && JsonNum(norm, "amount") <= 0 && JsonScalar(norm, "description").Length == 0)
            {
                continue;
            }
            items.Add(norm);
        }
        return items;
    }

    private static Dictionary<string, JsonElement> NormalizeLineItem(JsonElement row, int lineNumber)
    {
        var get = (string k) => row.TryGetProperty(k, out var v) ? v : default;
        var unit = JsonScalar(get("unit"), "PCS").Trim().ToUpperInvariant();
        if (!UnitOptions.Contains(unit))
        {
            unit = "PCS";
        }
        var volUnit = JsonScalar(get("volume_unit"), "CBM").Trim().ToUpperInvariant();
        if (!VolumeUnitOptions.Contains(volUnit))
        {
            volUnit = "CBM";
        }
        var ln = (int)Math.Max(1, JsonNum(get("line_number"), JsonNum(get("line_no"), lineNumber)));
        var map = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["line_number"] = JsonSerializer.SerializeToElement(ln),
            ["hs_code"] = JsonSerializer.SerializeToElement(JsonScalar(get("hs_code")).Trim()),
            ["country_of_origin"] = JsonSerializer.SerializeToElement(JsonScalar(get("country_of_origin")).Trim()),
            ["description"] = JsonSerializer.SerializeToElement(JsonScalar(get("description")).Trim()),
            ["quantity"] = JsonSerializer.SerializeToElement(JsonNum(get("quantity"))),
            ["unit"] = JsonSerializer.SerializeToElement(unit),
            ["volume"] = JsonSerializer.SerializeToElement(JsonNum(get("volume"))),
            ["volume_unit"] = JsonSerializer.SerializeToElement(volUnit),
            ["amount"] = JsonSerializer.SerializeToElement(JsonNum(get("amount"))),
            ["weight"] = JsonSerializer.SerializeToElement(JsonNum(get("weight"))),
            ["foreign_value"] = JsonSerializer.SerializeToElement(JsonNum(get("foreign_value"))),
            ["currency"] = JsonSerializer.SerializeToElement(JsonScalar(get("currency"), "AED").Trim()),
            ["currency_rate"] = JsonSerializer.SerializeToElement(JsonNum(get("currency_rate"))),
            ["cif_local_value"] = JsonSerializer.SerializeToElement(JsonNum(get("cif_local_value"))),
            ["duty_rate"] = JsonSerializer.SerializeToElement(JsonNum(get("duty_rate"))),
            ["income_type"] = JsonSerializer.SerializeToElement(JsonScalar(get("income_type")).Trim()),
            ["total_duty_aed"] = JsonSerializer.SerializeToElement(JsonNum(get("total_duty_aed"))),
            ["packages_qty"] = JsonSerializer.SerializeToElement(JsonNum(get("packages_qty"))),
            ["packages_type"] = JsonSerializer.SerializeToElement(JsonScalar(get("packages_type")).Trim()),
            ["weight_net"] = JsonSerializer.SerializeToElement(JsonNum(get("weight_net"))),
            ["weight_gross"] = JsonSerializer.SerializeToElement(JsonNum(get("weight_gross"))),
            ["aip_no"] = JsonSerializer.SerializeToElement(JsonScalar(get("aip_no")).Trim()),
            ["aip_duty"] = JsonSerializer.SerializeToElement(JsonScalar(get("aip_duty")).Trim()),
        };
        return map;
    }

    /// <summary>PHP epc_cs_save_declaration_items: delete-then-insert per declaration.</summary>
    private static async Task SaveItemsAsync(DbConnection connection, long declarationId, List<Dictionary<string, JsonElement>> items, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_custom_shipping_declaration_items` WHERE `declaration_id` = ?"),
            cancellationToken,
            declarationId).ConfigureAwait(false);
        var lineNo = 0;
        foreach (var item in items)
        {
            lineNo++;
            var norm = NormalizeLineItem(JsonSerializer.SerializeToElement(item), lineNo);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_custom_shipping_declaration_items` (`declaration_id`, `line_number`, `hs_code`, `country_of_origin`, `description`, `quantity`, `unit`, `volume`, `volume_unit`, `amount`, `weight`, `foreign_value`, `currency`, `currency_rate`, `cif_local_value`, `duty_rate`, `income_type`, `total_duty_aed`, `packages_qty`, `packages_type`, `weight_net`, `weight_gross`, `aip_no`, `aip_duty`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
                cancellationToken,
                declarationId,
                lineNo,
                JsonScalar(norm, "hs_code"),
                JsonScalar(norm, "country_of_origin"),
                JsonScalar(norm, "description"),
                JsonNum(norm, "quantity"),
                JsonScalar(norm, "unit"),
                JsonNum(norm, "volume"),
                JsonScalar(norm, "volume_unit"),
                JsonNum(norm, "amount"),
                JsonNum(norm, "weight"),
                JsonNum(norm, "foreign_value"),
                JsonScalar(norm, "currency"),
                JsonNum(norm, "currency_rate"),
                JsonNum(norm, "cif_local_value"),
                JsonNum(norm, "duty_rate"),
                JsonScalar(norm, "income_type"),
                JsonNum(norm, "total_duty_aed"),
                JsonNum(norm, "packages_qty"),
                JsonScalar(norm, "packages_type"),
                JsonNum(norm, "weight_net"),
                JsonNum(norm, "weight_gross"),
                JsonScalar(norm, "aip_no"),
                JsonScalar(norm, "aip_duty")).ConfigureAwait(false);
        }
    }

    /// <summary>PHP epc_cs_attach_pdf_to_declaration → epc_cs_commit_staged_pdf.</summary>
    private async Task AttachPdfAsync(DbConnection connection, long declarationId, Dictionary<string, string> data, CancellationToken cancellationToken)
    {
        if (declarationId <= 0)
        {
            return;
        }
        var token = new string(Field(data, "pdf_token", "").ToLowerInvariant().Where(Uri.IsHexDigit).ToArray());
        if (token.Length == 0)
        {
            return;
        }
        Directory.CreateDirectory(PdfRoot);
        var stagingDir = Path.Combine(PdfRoot, "staging");
        var staging = Path.Combine(stagingDir, token + ".pdf");
        if (!File.Exists(staging))
        {
            return;
        }
        var safeName = System.Text.RegularExpressions.Regex.Replace(Field(data, "pdf_file_name", ""), "[^a-zA-Z0-9._-]+", "_");
        if (safeName.Length == 0 || !safeName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            safeName = "declaration_" + declarationId + ".pdf";
        }
        var destName = "decl_" + declarationId + "_" + safeName;
        if (!destName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            destName += ".pdf";
        }
        var dest = Path.Combine(PdfRoot, destName);
        try
        {
            File.Move(staging, dest);
        }
        catch (IOException)
        {
            try
            {
                File.Copy(staging, dest);
                File.Delete(staging);
            }
            catch (IOException)
            {
                throw new ErpWriteException("Could not attach PDF to declaration");
            }
        }
        var rel = "/content/files/epc_custom_shipping_pdfs/" + destName;
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_custom_shipping_declarations` SET `pdf_file_path` = ?, `pdf_file_name` = ?, `updated_at` = ? WHERE `id` = ?"),
            cancellationToken,
            rel,
            Path.GetFileName(destName),
            _clock.GetUtcNow().ToUnixTimeSeconds(),
            declarationId).ConfigureAwait(false);
    }

    private static string[] FieldDataKeys(string category)
    {
        if (category == "lgp")
        {
            return LgpFieldKeys;
        }
        var keys = new List<string>(CoreColumns);
        keys.AddRange(CommonExtraFieldKeys);
        if (category == "import")
        {
            keys.AddRange(ImportExtraFieldKeys);
        }
        else if (category is "export" or "transit" or "temp_admission" or "transfer")
        {
            keys.AddRange(OutboundExtraFieldKeys);
        }
        return keys.ToArray();
    }

    private static Dictionary<string, string> BoxMap(Dictionary<string, JsonElement> boxData)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (boxData.TryGetValue("boxes", out var boxes) && boxes.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in boxes.EnumerateObject())
            {
                map[prop.Name] = JsonScalar(prop.Value);
            }
        }
        return map;
    }

    private static List<string> ReadStringList(Dictionary<string, string> data, string key)
    {
        var list = new List<string>();
        if (data.TryGetValue(key, out var json) && json.StartsWith('['))
        {
            try
            {
                foreach (var el in JsonSerializer.Deserialize<List<JsonElement>>(json) ?? [])
                {
                    var s = JsonScalar(el).Trim();
                    if (s.Length > 0)
                    {
                        list.Add(s);
                    }
                }
            }
            catch (JsonException)
            {
            }
        }
        return list;
    }

    private static List<string> ParseJsonStringArray(string json)
    {
        if (json.StartsWith('['))
        {
            try
            {
                var list = new List<string>();
                foreach (var el in JsonSerializer.Deserialize<List<JsonElement>>(json) ?? [])
                {
                    list.Add(JsonScalar(el));
                }
                return list;
            }
            catch (JsonException)
            {
            }
        }
        return [];
    }

    private static string JsonScalar(JsonElement el, string fallback = "")
        => el.ValueKind switch
        {
            JsonValueKind.String => el.GetString() ?? fallback,
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.True => "1",
            JsonValueKind.False => "0",
            _ => fallback,
        };

    private static string JsonScalar(Dictionary<string, JsonElement> map, string key, string fallback = "")
        => map.TryGetValue(key, out var v) ? JsonScalar(v, fallback) : fallback;

    private static double JsonNum(JsonElement el, double fallback = 0)
        => el.ValueKind switch
        {
            JsonValueKind.Number => el.GetDouble(),
            JsonValueKind.String => double.TryParse(el.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : fallback,
            _ => fallback,
        };

    private static double JsonNum(Dictionary<string, JsonElement> map, string key, double fallback = 0)
        => map.TryGetValue(key, out var v) ? JsonNum(v, fallback) : fallback;

    private static string Field(Dictionary<string, string> data, string key, string fallback)
        => data.TryGetValue(key, out var v) ? v : fallback;

    private static string? NullableDate(string value)
        => value.Trim().Length > 0 ? value.Trim() : null;

    private static long ParseLong(string value)
        => long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static decimal ParseDecimal(string value)
        => decimal.TryParse(value.Replace(",", "").Replace(" ", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    private static string DecimalString(string value)
    {
        var s = value.Trim().Replace(",", "").Replace(" ", "");
        return decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d.ToString(CultureInfo.InvariantCulture)
            : "";
    }
}
