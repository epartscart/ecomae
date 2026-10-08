using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Product information management custom attributes (owner-accepted enhancement from PR #8).
/// Operators define unlimited typed attributes with option lists; each attribute can show on the
/// inventory, sales and/or purchase forms. Values are stored per inventory item.
/// </summary>
public static partial class ErpPimCustomFields
{
    public static readonly IReadOnlyList<(string Type, string Label)> TypeLabels =
    [
        ("text", "Text"),
        ("number", "Number"),
        ("date", "Date"),
        ("boolean", "Yes/No"),
        ("single_option", "Dropdown (single)"),
        ("multi_option", "Checkboxes (multi)"),
    ];

    public static readonly IReadOnlyList<string> Modules = ["inventory", "sales", "purchase"];

    public const string InputPrefix = "pim_field_";

    public sealed record Field(
        long Id,
        string Name,
        string Code,
        string FieldType,
        string Description,
        bool Required,
        bool ShowInventory,
        bool ShowSales,
        bool ShowPurchase,
        int Position = 0,
        int OptionCount = 0)
    {
        public bool IsOptionType => FieldType is "single_option" or "multi_option";

        public bool ShowsOn(string module) => module switch
        {
            "inventory" => ShowInventory,
            "sales" => ShowSales,
            "purchase" => ShowPurchase,
            _ => true,
        };
    }

    public sealed record Option(long Id, long FieldId, string Label, string Value, int Position = 0);

    public sealed record ItemValue(
        long FieldId,
        string? ValueText,
        decimal? ValueNumber,
        string? ValueDate,
        bool? ValueBool,
        string ValueOptionIds);

    public sealed record DisplayRow(Field Field, ItemValue Value, string DisplayValue);

    public sealed record FieldInput(
        string Name,
        string FieldType,
        string Description,
        bool Required,
        bool ShowInventory,
        bool ShowSales,
        bool ShowPurchase,
        string OptionsRaw = "");

    public sealed record SaveOutcome(int Written, IReadOnlyList<string> Errors);

    public static string TypeLabel(string type)
    {
        foreach (var (t, label) in TypeLabels)
        {
            if (t == type)
            {
                return label;
            }
        }

        return type;
    }

    public static string NormalizeType(string? type)
    {
        var t = (type ?? "").Trim();
        foreach (var (known, _) in TypeLabels)
        {
            if (known == t)
            {
                return t;
            }
        }

        return "text";
    }

    public static string NormalizeModule(string? module)
    {
        var m = (module ?? "").Trim().ToLowerInvariant();
        return Modules.Contains(m) ? m : "";
    }

    /// <summary>Lower-case code built from the name: runs of non-alphanumerics become <c>_</c>, trailing <c>_</c> trimmed.</summary>
    public static string CodeFromName(string? name)
    {
        var code = NonAlnumRuns().Replace((name ?? "").Trim(), "_").ToLowerInvariant();
        return code.TrimEnd('_');
    }

    public static IReadOnlyList<string> ParseOptionList(string? raw)
    {
        var list = new List<string>();
        foreach (var part in OptionSeparators().Split(raw ?? ""))
        {
            var label = part.Trim();
            if (label.Length > 0)
            {
                list.Add(label);
            }
        }

        return list;
    }

    public static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_pim_fields` (
              `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
              `name` VARCHAR(200) NOT NULL DEFAULT '',
              `code` VARCHAR(60) NOT NULL DEFAULT '',
              `field_type` ENUM('text','number','date','boolean','single_option','multi_option') NOT NULL DEFAULT 'text',
              `description` VARCHAR(500) NOT NULL DEFAULT '',
              `default_value` VARCHAR(500) NOT NULL DEFAULT '',
              `required` TINYINT(1) NOT NULL DEFAULT 0,
              `show_inventory` TINYINT(1) NOT NULL DEFAULT 1,
              `show_sales` TINYINT(1) NOT NULL DEFAULT 1,
              `show_purchase` TINYINT(1) NOT NULL DEFAULT 1,
              `position` SMALLINT UNSIGNED NOT NULL DEFAULT 0,
              `active` TINYINT(1) NOT NULL DEFAULT 1,
              `created_at` INT UNSIGNED NOT NULL DEFAULT 0,
              `updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
              UNIQUE KEY `uk_code` (`code`),
              INDEX `idx_type` (`field_type`),
              INDEX `idx_active` (`active`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_pim_field_options` (
              `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
              `field_id` INT UNSIGNED NOT NULL DEFAULT 0,
              `label` VARCHAR(200) NOT NULL DEFAULT '',
              `value` VARCHAR(200) NOT NULL DEFAULT '',
              `position` SMALLINT UNSIGNED NOT NULL DEFAULT 0,
              `active` TINYINT(1) NOT NULL DEFAULT 1,
              INDEX `idx_field` (`field_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_pim_item_values` (
              `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
              `item_id` INT UNSIGNED NOT NULL DEFAULT 0,
              `field_id` INT UNSIGNED NOT NULL DEFAULT 0,
              `value_text` TEXT,
              `value_number` DECIMAL(16,4) DEFAULT NULL,
              `value_date` DATE DEFAULT NULL,
              `value_bool` TINYINT(1) DEFAULT NULL,
              `value_option_ids` VARCHAR(500) NOT NULL DEFAULT '',
              `updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
              UNIQUE KEY `uk_item_field` (`item_id`, `field_id`),
              INDEX `idx_item` (`item_id`),
              INDEX `idx_field` (`field_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<Field>> ListFieldsAsync(DbConnection connection, string module, CancellationToken cancellationToken)
    {
        var sql = "SELECT f.`id`, f.`name`, f.`code`, f.`field_type`, f.`description`, f.`required`, f.`show_inventory`, f.`show_sales`, f.`show_purchase`, f.`position`,"
                  + " (SELECT COUNT(*) FROM `epc_pim_field_options` o WHERE o.`field_id` = f.`id` AND o.`active` = 1) AS option_count"
                  + " FROM `epc_pim_fields` f WHERE f.`active` = 1";
        sql += NormalizeModule(module) switch
        {
            "inventory" => " AND f.`show_inventory` = 1",
            "sales" => " AND f.`show_sales` = 1",
            "purchase" => " AND f.`show_purchase` = 1",
            _ => "",
        };
        sql += " ORDER BY f.`position` ASC, f.`name` ASC";
        var list = new List<Field>();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new Field(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                Str(reader, 1),
                Str(reader, 2),
                Str(reader, 3),
                Str(reader, 4),
                Flag(reader, 5),
                Flag(reader, 6),
                Flag(reader, 7),
                Flag(reader, 8),
                Convert.ToInt32(reader.GetValue(9), CultureInfo.InvariantCulture),
                Convert.ToInt32(reader.GetValue(10), CultureInfo.InvariantCulture)));
        }

        return list;
    }

    public static async Task<Field?> GetFieldAsync(DbConnection connection, long fieldId, CancellationToken cancellationToken)
    {
        foreach (var field in await ListFieldsAsync(connection, "", cancellationToken).ConfigureAwait(false))
        {
            if (field.Id == fieldId)
            {
                return field;
            }
        }

        return null;
    }

    /// <summary>Active options per field, ordered by position then label.</summary>
    public static async Task<IReadOnlyDictionary<long, IReadOnlyList<Option>>> OptionsByFieldAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<long, List<Option>>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `id`, `field_id`, `label`, `value`, `position` FROM `epc_pim_field_options` WHERE `active` = 1 ORDER BY `field_id` ASC, `position` ASC, `label` ASC";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var option = new Option(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                Str(reader, 2),
                Str(reader, 3),
                Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture));
            if (!map.TryGetValue(option.FieldId, out var list))
            {
                map[option.FieldId] = list = [];
            }

            list.Add(option);
        }

        return map.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<Option>)kv.Value);
    }

    public static async Task<IReadOnlyDictionary<long, ItemValue>> ValuesForItemAsync(
        DbConnection connection,
        long itemId,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<long, ItemValue>();
        if (itemId <= 0)
        {
            return map;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `field_id`, `value_text`, `value_number`, DATE_FORMAT(`value_date`, '%Y-%m-%d'), `value_bool`, `value_option_ids` FROM `epc_pim_item_values` WHERE `item_id` = @p0";
        ErpDb.AddParameters(command, itemId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var fieldId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
            map[fieldId] = new ItemValue(
                fieldId,
                reader.IsDBNull(1) ? null : reader.GetValue(1).ToString(),
                reader.IsDBNull(2) ? null : Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture),
                reader.IsDBNull(3) ? null : reader.GetValue(3).ToString(),
                reader.IsDBNull(4) ? null : Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture) != 0,
                Str(reader, 5));
        }

        return map;
    }

    public static async Task<string> RenderFormFieldsAsync(DbConnection connection, string module, long itemId, CancellationToken cancellationToken)
    {
        var fields = await ListFieldsAsync(connection, module, cancellationToken).ConfigureAwait(false);
        if (fields.Count == 0)
        {
            return "";
        }

        var options = await OptionsByFieldAsync(connection, cancellationToken).ConfigureAwait(false);
        var values = await ValuesForItemAsync(connection, itemId, cancellationToken).ConfigureAwait(false);
        return RenderFormFields(fields, options, values);
    }

    public static async Task<IReadOnlyList<DisplayRow>> DisplayRowsAsync(DbConnection connection, long itemId, string module, CancellationToken cancellationToken)
    {
        var fields = await ListFieldsAsync(connection, "", cancellationToken).ConfigureAwait(false);
        var options = await OptionsByFieldAsync(connection, cancellationToken).ConfigureAwait(false);
        var values = await ValuesForItemAsync(connection, itemId, cancellationToken).ConfigureAwait(false);
        return DisplayRows(fields, options, values, module);
    }

    /// <summary>Creates a field (and its comma/newline separated options for option types). Returns the new id.</summary>
    public static async Task<long> CreateFieldAsync(DbConnection connection, FieldInput input, CancellationToken cancellationToken)
    {
        var name = (input.Name ?? "").Trim();
        if (name.Length == 0)
        {
            throw new ErpWriteException("Field name is required.");
        }

        if (name.Length > 200)
        {
            name = name[..200];
        }

        var type = NormalizeType(input.FieldType);
        var code = await UniqueCodeAsync(connection, CodeFromName(name), cancellationToken).ConfigureAwait(false);
        var description = (input.Description ?? "").Trim();
        if (description.Length > 500)
        {
            description = description[..500];
        }

        var now = Now();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_pim_fields` (`name`,`code`,`field_type`,`description`,`default_value`,`required`,`show_inventory`,`show_sales`,`show_purchase`,`position`,`active`,`created_at`,`updated_at`) VALUES (?,?,?,?,'',?,?,?,?,0,1,?,?)"),
            cancellationToken,
            name, code, type, description, input.Required ? 1 : 0, input.ShowInventory ? 1 : 0, input.ShowSales ? 1 : 0, input.ShowPurchase ? 1 : 0, now, now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id > 0 && type is "single_option" or "multi_option")
        {
            var position = 0;
            foreach (var label in ParseOptionList(input.OptionsRaw))
            {
                await InsertOptionAsync(connection, id, label, position++, cancellationToken).ConfigureAwait(false);
            }
        }

        return id;
    }

    public static async Task<bool> DeactivateFieldAsync(DbConnection connection, long fieldId, CancellationToken cancellationToken)
        => await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_pim_fields` SET `active` = 0, `updated_at` = ? WHERE `id` = ? AND `active` = 1"),
            cancellationToken,
            Now(), fieldId).ConfigureAwait(false) > 0;

    public static async Task<long> AddOptionAsync(DbConnection connection, long fieldId, string label, CancellationToken cancellationToken)
    {
        label = (label ?? "").Trim();
        if (label.Length == 0)
        {
            throw new ErpWriteException("Option label is required.");
        }

        var field = await GetFieldAsync(connection, fieldId, cancellationToken).ConfigureAwait(false);
        if (field is null)
        {
            throw new ErpWriteException("Attribute not found.");
        }

        if (!field.IsOptionType)
        {
            throw new ErpWriteException("Only dropdown and checkbox attributes have options.");
        }

        var position = (int)await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COALESCE(MAX(`position`) + 1, 0) FROM `epc_pim_field_options` WHERE `field_id` = ? AND `active` = 1"),
            cancellationToken,
            fieldId).ConfigureAwait(false);
        return await InsertOptionAsync(connection, fieldId, label, position, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<bool> DeactivateOptionAsync(DbConnection connection, long optionId, CancellationToken cancellationToken)
        => await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_pim_field_options` SET `active` = 0 WHERE `id` = ? AND `active` = 1"),
            cancellationToken,
            optionId).ConfigureAwait(false) > 0;

    /// <summary>Required-field check for a create/edit form post, run before the item itself is written.</summary>
    public static IReadOnlyList<string> ValidatePost(IReadOnlyList<Field> fields, IReadOnlyDictionary<string, IReadOnlyList<string>> post)
    {
        var errors = new List<string>();
        foreach (var field in fields)
        {
            post.TryGetValue(InputPrefix + field.Id.ToString(CultureInfo.InvariantCulture), out var raw);
            var values = (raw ?? [])
                .Select(v => (v ?? "").Trim())
                .Where(v => v.Length > 0 && !(field.IsOptionType && v == "0"))
                .ToList();
            if (field.Required && field.FieldType != "boolean" && values.Count == 0)
            {
                errors.Add(field.Name + " is required.");
                continue;
            }

            if (values.Count == 0)
            {
                continue;
            }

            if (field.FieldType == "number" && ParseNumber(values[0]) is null)
            {
                errors.Add(field.Name + " must be a number.");
            }
            else if (field.FieldType == "date" && ParseDate(values[0]) is null)
            {
                errors.Add(field.Name + " must be a date (YYYY-MM-DD).");
            }
        }

        return errors;
    }

    /// <summary>
    /// Saves the posted <c>pim_field_{id}</c> values for every field of the module. A missing checkbox stores "No";
    /// option ids that are not active options of that field are dropped; invalid numbers and dates are skipped.
    /// </summary>
    public static async Task<SaveOutcome> SaveFromPostAsync(
        DbConnection connection,
        long itemId,
        IReadOnlyDictionary<string, IReadOnlyList<string>> post,
        string module,
        CancellationToken cancellationToken)
    {
        if (itemId <= 0)
        {
            return new SaveOutcome(0, []);
        }

        var fields = await ListFieldsAsync(connection, module, cancellationToken).ConfigureAwait(false);
        if (fields.Count == 0)
        {
            return new SaveOutcome(0, []);
        }

        var options = await OptionsByFieldAsync(connection, cancellationToken).ConfigureAwait(false);
        var errors = new List<string>();
        var written = 0;
        foreach (var field in fields)
        {
            var key = InputPrefix + field.Id.ToString(CultureInfo.InvariantCulture);
            if (!post.TryGetValue(key, out var raw))
            {
                if (field.FieldType == "boolean")
                {
                    await UpsertValueAsync(connection, itemId, field.Id, null, null, null, false, "", cancellationToken).ConfigureAwait(false);
                    written++;
                }

                continue;
            }

            var first = raw.Count > 0 ? (raw[0] ?? "").Trim() : "";
            switch (field.FieldType)
            {
                case "number":
                {
                    decimal? number = null;
                    if (first.Length > 0)
                    {
                        number = ParseNumber(first);
                        if (number is null)
                        {
                            errors.Add(field.Name + " must be a number.");
                            continue;
                        }
                    }

                    await UpsertValueAsync(connection, itemId, field.Id, null, number, null, null, "", cancellationToken).ConfigureAwait(false);
                    break;
                }

                case "date":
                {
                    string? date = null;
                    if (first.Length > 0)
                    {
                        date = ParseDate(first);
                        if (date is null)
                        {
                            errors.Add(field.Name + " must be a date (YYYY-MM-DD).");
                            continue;
                        }
                    }

                    await UpsertValueAsync(connection, itemId, field.Id, null, null, date, null, "", cancellationToken).ConfigureAwait(false);
                    break;
                }

                case "boolean":
                    await UpsertValueAsync(connection, itemId, field.Id, null, null, null, first.Length > 0 && first != "0", "", cancellationToken).ConfigureAwait(false);
                    break;
                case "single_option":
                case "multi_option":
                {
                    var allowed = options.TryGetValue(field.Id, out var opts) ? opts.Select(o => o.Id).ToHashSet() : [];
                    var ids = new List<long>();
                    foreach (var item in field.FieldType == "single_option" ? raw.Take(1) : raw)
                    {
                        if (long.TryParse((item ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && allowed.Contains(id) && !ids.Contains(id))
                        {
                            ids.Add(id);
                        }
                    }

                    await UpsertValueAsync(connection, itemId, field.Id, null, null, null, null, string.Join(",", ids), cancellationToken).ConfigureAwait(false);
                    break;
                }

                default:
                    await UpsertValueAsync(connection, itemId, field.Id, first, null, null, null, "", cancellationToken).ConfigureAwait(false);
                    break;
            }

            written++;
        }

        return new SaveOutcome(written, errors);
    }

    public static string RenderFormFields(
        IReadOnlyList<Field> fields,
        IReadOnlyDictionary<long, IReadOnlyList<Option>> options,
        IReadOnlyDictionary<long, ItemValue> existing)
    {
        const string InputStyle = "width:100%; padding:6px 10px; border:1px solid #ccc; border-radius:4px;";
        var html = new StringBuilder();
        foreach (var f in fields)
        {
            var inputName = InputPrefix + f.Id.ToString(CultureInfo.InvariantCulture);
            var req = f.Required ? " <span style=\"color:red\">*</span>" : "";
            existing.TryGetValue(f.Id, out var val);
            html.Append("<div class=\"form-group\" style=\"margin-bottom:12px;\">");
            html.Append("<label style=\"font-weight:600; display:block; margin-bottom:4px;\">").Append(H(f.Name)).Append(req).Append("</label>");
            switch (f.FieldType)
            {
                case "text":
                    html.Append("<input type=\"text\" name=\"").Append(inputName).Append("\" value=\"").Append(val is null ? "" : H(val.ValueText ?? ""))
                        .Append("\" class=\"form-control\" style=\"").Append(InputStyle).Append("\" />");
                    break;
                case "number":
                    html.Append("<input type=\"number\" step=\"any\" name=\"").Append(inputName).Append("\" value=\"")
                        .Append(val?.ValueNumber is { } n ? PhpFloat(n) : "")
                        .Append("\" class=\"form-control\" style=\"").Append(InputStyle).Append("\" />");
                    break;
                case "date":
                    html.Append("<input type=\"date\" name=\"").Append(inputName).Append("\" value=\"").Append(val is null ? "" : H(val.ValueDate ?? ""))
                        .Append("\" class=\"form-control\" style=\"").Append(InputStyle).Append("\" />");
                    break;
                case "boolean":
                    html.Append("<label style=\"display:inline-flex; align-items:center; gap:6px; cursor:pointer;\"><input type=\"checkbox\" name=\"").Append(inputName)
                        .Append("\" value=\"1\"").Append(val?.ValueBool == true ? " checked" : "").Append(" /> Yes</label>");
                    break;
                case "single_option":
                {
                    var selected = SelectedIds(val);
                    html.Append("<select name=\"").Append(inputName).Append("\" class=\"form-control\" style=\"").Append(InputStyle).Append("\">");
                    html.Append("<option value=\"\">-- Select --</option>");
                    foreach (var opt in OptionsOf(options, f.Id))
                    {
                        var id = opt.Id.ToString(CultureInfo.InvariantCulture);
                        html.Append("<option value=\"").Append(id).Append('"').Append(selected.Contains(id) ? " selected" : "").Append('>').Append(H(opt.Label)).Append("</option>");
                    }

                    html.Append("</select>");
                    break;
                }

                case "multi_option":
                {
                    var selected = SelectedIds(val);
                    html.Append("<div style=\"border:1px solid #ccc; border-radius:4px; padding:8px; max-height:160px; overflow-y:auto;\">");
                    foreach (var opt in OptionsOf(options, f.Id))
                    {
                        var id = opt.Id.ToString(CultureInfo.InvariantCulture);
                        html.Append("<label style=\"display:block; cursor:pointer; padding:2px 0;\"><input type=\"checkbox\" name=\"").Append(inputName).Append("[]\" value=\"").Append(id).Append('"')
                            .Append(selected.Contains(id) ? " checked" : "").Append(" /> ").Append(H(opt.Label)).Append("</label>");
                    }

                    html.Append("</div>");
                    break;
                }
            }

            if (f.Description != "")
            {
                html.Append("<small style=\"color:#888; display:block; margin-top:2px;\">").Append(H(f.Description)).Append("</small>");
            }

            html.Append("</div>");
        }

        return html.ToString();
    }

    public static IReadOnlyList<DisplayRow> DisplayRows(
        IReadOnlyList<Field> fields,
        IReadOnlyDictionary<long, IReadOnlyList<Option>> options,
        IReadOnlyDictionary<long, ItemValue> values,
        string module)
    {
        var rows = new List<DisplayRow>();
        var m = NormalizeModule(module);
        foreach (var field in fields)
        {
            if (!values.TryGetValue(field.Id, out var value) || (m != "" && !field.ShowsOn(m)))
            {
                continue;
            }

            rows.Add(new DisplayRow(field, value, DisplayValue(field, value, options)));
        }

        return rows;
    }

    public static string DisplayValue(Field field, ItemValue value, IReadOnlyDictionary<long, IReadOnlyList<Option>> options)
    {
        switch (field.FieldType)
        {
            case "number":
                return value.ValueNumber is { } n ? PhpFloat(n) : "";
            case "date":
                return value.ValueDate ?? "";
            case "boolean":
                return value.ValueBool is { } b ? (b ? "Yes" : "No") : "";
            case "single_option":
            case "multi_option":
            {
                var ids = value.ValueOptionIds.Split(',').Where(s => s.Length > 0 && s != "0").ToHashSet();
                if (ids.Count == 0)
                {
                    return value.ValueText ?? "";
                }

                return string.Join(", ", OptionsOf(options, field.Id)
                    .Where(o => ids.Contains(o.Id.ToString(CultureInfo.InvariantCulture)))
                    .OrderBy(o => o.Position)
                    .Select(o => o.Label));
            }

            default:
                return value.ValueText ?? "";
        }
    }

    public static string RenderDisplayTable(IReadOnlyList<DisplayRow> rows)
    {
        if (rows.Count == 0)
        {
            return "";
        }

        var html = new StringBuilder("<table style=\"width:100%; border-collapse:collapse; margin:10px 0;\">");
        html.Append("<thead><tr style=\"background:#f5f5f5;\"><th style=\"text-align:left; padding:6px 10px; border:1px solid #ddd;\">Field</th><th style=\"text-align:left; padding:6px 10px; border:1px solid #ddd;\">Value</th></tr></thead><tbody>");
        foreach (var row in rows)
        {
            html.Append("<tr><td style=\"padding:6px 10px; border:1px solid #ddd;\">").Append(H(row.Field.Name)).Append("</td>");
            html.Append("<td style=\"padding:6px 10px; border:1px solid #ddd;\">").Append(H(row.DisplayValue)).Append("</td></tr>");
        }

        html.Append("</tbody></table>");
        return html.ToString();
    }

    /// <summary>PHP float-to-string for DECIMAL(16,4) values: no trailing zeros, no trailing point.</summary>
    public static string PhpFloat(decimal value)
    {
        var text = value.ToString("0.############", CultureInfo.InvariantCulture);
        return text == "-0" ? "0" : text;
    }

    public static decimal? ParseNumber(string raw)
        => decimal.TryParse(raw.Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var n)
           && Math.Abs(n) < 1_000_000_000_000m
            ? decimal.Round(n, 4, MidpointRounding.AwayFromZero)
            : null;

    public static string? ParseDate(string raw)
        => DateOnly.TryParseExact(raw.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;

    private static async Task<string> UniqueCodeAsync(DbConnection connection, string baseCode, CancellationToken cancellationToken)
    {
        var stem = baseCode.Length == 0 ? "field" : baseCode;
        if (stem.Length > 54)
        {
            stem = stem[..54];
        }

        var code = stem;
        for (var n = 2; await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `epc_pim_fields` WHERE `code` = ?"), cancellationToken, code).ConfigureAwait(false) > 0; n++)
        {
            code = stem + "_" + n.ToString(CultureInfo.InvariantCulture);
        }

        return code;
    }

    private static async Task<long> InsertOptionAsync(DbConnection connection, long fieldId, string label, int position, CancellationToken cancellationToken)
    {
        if (label.Length > 200)
        {
            label = label[..200];
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_pim_field_options` (`field_id`,`label`,`value`,`position`) VALUES (?,?,?,?)"),
            cancellationToken,
            fieldId, label, label, position).ConfigureAwait(false);
        return await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    private static Task<int> UpsertValueAsync(
        DbConnection connection,
        long itemId,
        long fieldId,
        string? text,
        decimal? number,
        string? date,
        bool? flag,
        string optionIds,
        CancellationToken cancellationToken)
        => ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_pim_item_values` (`item_id`,`field_id`,`value_text`,`value_number`,`value_date`,`value_bool`,`value_option_ids`,`updated_at`) VALUES (?,?,?,?,?,?,?,?)"
                             + " ON DUPLICATE KEY UPDATE `value_text`=VALUES(`value_text`), `value_number`=VALUES(`value_number`), `value_date`=VALUES(`value_date`), `value_bool`=VALUES(`value_bool`), `value_option_ids`=VALUES(`value_option_ids`), `updated_at`=VALUES(`updated_at`)"),
            cancellationToken,
            itemId, fieldId, text, number, date, flag is null ? null : (flag.Value ? 1 : 0), optionIds, Now());

    private static IReadOnlyList<Option> OptionsOf(IReadOnlyDictionary<long, IReadOnlyList<Option>> options, long fieldId)
        => options.TryGetValue(fieldId, out var list) ? list : [];

    private static HashSet<string> SelectedIds(ItemValue? value)
        => value is null ? [] : value.ValueOptionIds.Split(',').ToHashSet();

    private static string H(string value) => EcomAE.Platform.Storefront.StorefrontSupplierLpoNotifier.H(value);

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static string Str(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? "" : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? "";

    private static bool Flag(DbDataReader reader, int ordinal)
        => !reader.IsDBNull(ordinal) && Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;

    [GeneratedRegex("[^a-zA-Z0-9]+")]
    private static partial Regex NonAlnumRuns();

    [GeneratedRegex("[\\r\\n,]+")]
    private static partial Regex OptionSeparators();
}
