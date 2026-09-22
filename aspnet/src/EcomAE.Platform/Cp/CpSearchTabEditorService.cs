using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Read twin of PHP <c>search_tabs.php</c> (list, translated captions, on/off) and <c>search_tab.php</c>
/// (single tab: general settings + <c>parameters</c> schema rendered against <c>parameters_values</c>).
/// </summary>
public interface ICpSearchTabEditorService
{
    Task<CpSearchTabList> ListAsync(string langCode, CancellationToken cancellationToken = default);

    Task<CpSearchTabEditor?> OpenAsync(long tabId, string langCode, CancellationToken cancellationToken = default);
}

public sealed record CpSearchTabListRow(long Id, string Name, string CaptionKey, string Caption, int Order, int Enabled);

public sealed record CpSearchTabList(IReadOnlyList<CpSearchTabListRow> Rows, string Source, string Message);

public sealed record CpSearchTabOption(string Value, string Caption);

/// <summary>One element inside a PHP <c>groupbox</c>: text | number | checkbox | multiselect | completed_html.</summary>
public sealed record CpSearchTabElement(
    string Name,
    string Caption,
    string Type,
    string Value,
    IReadOnlyList<string> Values,
    IReadOnlyList<CpSearchTabOption> Options,
    string Html,
    IReadOnlyDictionary<string, string> HtmlFieldValues);

public sealed record CpSearchTabGroup(string Caption, IReadOnlyList<CpSearchTabElement> Elements);

public sealed record CpSearchTabEditor(
    long Id,
    string Name,
    string CaptionKey,
    string Caption,
    int Order,
    int Enabled,
    IReadOnlyList<CpSearchTabGroup> Groups,
    string ParametersValuesJson);

public sealed class CpSearchTabEditorService : ICpSearchTabEditorService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpSearchTabEditorService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CpSearchTabList> ListAsync(string langCode, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new([], "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var rows = new List<CpSearchTabListRow>();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ErpDb.Positional(
                """
                SELECT t.`id`, IFNULL(t.`name`,''), IFNULL(t.`caption`,''), IFNULL(t.`order`,0), IFNULL(t.`enabled`,0),
                       IFNULL((SELECT x.`translation` FROM `lang_text_strings_translation` x WHERE x.`str_key` = t.`caption` AND x.`lang_code` = ? LIMIT 1), '')
                FROM `shop_docpart_search_tabs` t
                ORDER BY t.`order` ASC, t.`id` ASC
                """);
            ErpDb.AddParameters(cmd, CpCustomTranslationWriter.NormalizeLang(langCode));
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var key = reader.GetString(2);
                var translated = reader.GetString(5);
                rows.Add(new CpSearchTabListRow(
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    reader.GetString(1),
                    key,
                    translated.Length > 0 ? translated : key,
                    Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture)));
            }

            return new(rows, "shop_docpart_search_tabs", string.Empty);
        }
        catch (DbException ex)
        {
            return new([], "migration", ex.Message);
        }
    }

    public async Task<CpSearchTabEditor?> OpenAsync(long tabId, string langCode, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured || tabId <= 0)
        {
            return null;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        string name, captionKey, caption, parametersJson, valuesJson;
        int order, enabled;
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = ErpDb.Positional(
                """
                SELECT IFNULL(t.`name`,''), IFNULL(t.`caption`,''), IFNULL(t.`order`,0), IFNULL(t.`enabled`,0),
                       IFNULL(t.`parameters`,''), IFNULL(t.`parameters_values`,''),
                       IFNULL((SELECT x.`translation` FROM `lang_text_strings_translation` x WHERE x.`str_key` = t.`caption` AND x.`lang_code` = ? LIMIT 1), '')
                FROM `shop_docpart_search_tabs` t WHERE t.`id` = ? LIMIT 1
                """);
            ErpDb.AddParameters(cmd, CpCustomTranslationWriter.NormalizeLang(langCode), tabId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            name = reader.GetString(0);
            captionKey = reader.GetString(1);
            order = Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture);
            enabled = Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture);
            parametersJson = reader.GetString(4);
            valuesJson = reader.GetString(5);
            var translated = reader.GetString(6);
            caption = translated.Length > 0 ? translated : captionKey;
        }

        var values = ParseValues(valuesJson);
        var groups = await BuildGroupsAsync(connection, parametersJson, values, cancellationToken).ConfigureAwait(false);
        return new CpSearchTabEditor(tabId, name, captionKey, caption, order, enabled, groups, valuesJson);
    }

    /// <summary>parameters_values is <c>json_encode($_POST)</c> in PHP: scalars or arrays (multiselect <c>name[]</c>).</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ParseValues(string json)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    result[prop.Name] = prop.Value.EnumerateArray().Select(Scalar).ToList();
                }
                else
                {
                    result[prop.Name] = [Scalar(prop.Value)];
                }
            }
        }
        catch (JsonException)
        {
        }

        return result;
    }

    private static string Scalar(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString() ?? string.Empty,
        JsonValueKind.Number => e.GetRawText(),
        JsonValueKind.True => "1",
        JsonValueKind.False => "0",
        JsonValueKind.Null => string.Empty,
        _ => e.GetRawText()
    };

    private static async Task<IReadOnlyList<CpSearchTabGroup>> BuildGroupsAsync(
        DbConnection connection,
        string parametersJson,
        IReadOnlyDictionary<string, IReadOnlyList<string>> values,
        CancellationToken cancellationToken)
    {
        var groups = new List<CpSearchTabGroup>();
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return groups;
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(parametersJson);
        }
        catch (JsonException)
        {
            return groups;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return groups;
            }

            foreach (var group in doc.RootElement.EnumerateArray())
            {
                if (Str(group, "type") != "groupbox" || !group.TryGetProperty("elements", out var elements) || elements.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var list = new List<CpSearchTabElement>();
                foreach (var el in elements.EnumerateArray())
                {
                    var elName = Str(el, "name");
                    var type = Str(el, "type");
                    var current = values.TryGetValue(elName, out var v) ? v : [];
                    var options = new List<CpSearchTabOption>();
                    var html = string.Empty;
                    var htmlFields = new Dictionary<string, string>(StringComparer.Ordinal);
                    if (type == "multiselect" && Str(el, "source") == "sql")
                    {
                        options = await OptionsFromSqlAsync(connection, Str(el, "sql"), cancellationToken).ConfigureAwait(false);
                    }
                    else if (type == "completed_html" && Str(el, "source") == "sql")
                    {
                        html = await HtmlFromSqlAsync(connection, Str(el, "sql"), cancellationToken).ConfigureAwait(false);
                        foreach (var field in await FieldNamesFromSqlAsync(connection, Str(el, "fields_names_sql"), cancellationToken).ConfigureAwait(false))
                        {
                            htmlFields[field] = values.TryGetValue(field, out var fv) && fv.Count > 0 ? fv[0] : string.Empty;
                        }
                    }

                    list.Add(new CpSearchTabElement(
                        elName,
                        Str(el, "caption"),
                        type,
                        current.Count > 0 ? current[0] : string.Empty,
                        current,
                        options,
                        html,
                        htmlFields));
                }

                groups.Add(new CpSearchTabGroup(Str(group, "caption"), list));
            }
        }

        return groups;
    }

    private static string Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p) ? Scalar(p) : string.Empty;

    /// <summary>Schema SQL is operator-authored config stored in <c>parameters</c>; PHP executes it verbatim. Only SELECTs are honoured here.</summary>
    private static bool IsSelect(string sql) => sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);

    private static async Task<List<CpSearchTabOption>> OptionsFromSqlAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        var list = new List<CpSearchTabOption>();
        if (!IsSelect(sql))
        {
            return list;
        }

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql.TrimEnd().TrimEnd(';');
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var valueOrd = SafeOrdinal(reader, "value");
            var captionOrd = SafeOrdinal(reader, "caption");
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var value = valueOrd >= 0 && !reader.IsDBNull(valueOrd) ? reader.GetValue(valueOrd).ToString() ?? "" : "";
                var caption = captionOrd >= 0 && !reader.IsDBNull(captionOrd) ? reader.GetValue(captionOrd).ToString() ?? "" : value;
                list.Add(new CpSearchTabOption(value, caption.ToUpperInvariant()));
            }
        }
        catch (DbException)
        {
        }

        return list;
    }

    private static async Task<string> HtmlFromSqlAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        var statements = sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var last = statements.LastOrDefault(s => s.Length > 0);
        if (last is null || !IsSelect(last))
        {
            return string.Empty;
        }

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = last;
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var ord = SafeOrdinal(reader, "html");
            if (ord >= 0 && await reader.ReadAsync(cancellationToken).ConfigureAwait(false) && !reader.IsDBNull(ord))
            {
                return reader.GetValue(ord).ToString() ?? string.Empty;
            }
        }
        catch (DbException)
        {
        }

        return string.Empty;
    }

    private static async Task<List<string>> FieldNamesFromSqlAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        var list = new List<string>();
        if (!IsSelect(sql))
        {
            return list;
        }

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql.TrimEnd().TrimEnd(';');
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var ord = SafeOrdinal(reader, "field_name");
            while (ord >= 0 && await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!reader.IsDBNull(ord))
                {
                    list.Add(reader.GetValue(ord).ToString() ?? string.Empty);
                }
            }
        }
        catch (DbException)
        {
        }

        return list;
    }

    private static int SafeOrdinal(DbDataReader reader, string name)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// PHP stores <c>json_encode($_POST)</c>. Build the same object from the posted form, dropping the
    /// general/control fields; repeated keys (multiselect <c>name[]</c>) become arrays.
    /// </summary>
    public static string ParametersFromForm(IFormCollection form)
    {
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "action", "tab_id", "tabId", "id", "csrf_guard_key", "confirmWrites", "confirm_writes", "returnUrl", "return_url",
            "tab_caption", "tab_caption_lang_str_id", "tab_order", "tab_enabled", "caption", "captionLangStrId", "sortOrder", "enabled",
            "tabEnabled", "parametersValues", "parameters_values", "langCode", "lang_code", "s_page", "activate_tab"
        };
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var kv in form)
            {
                if (reserved.Contains(kv.Key))
                {
                    continue;
                }

                var key = kv.Key.EndsWith("[]", StringComparison.Ordinal) ? kv.Key[..^2] : kv.Key;
                if (kv.Key.EndsWith("[]", StringComparison.Ordinal) || kv.Value.Count > 1)
                {
                    writer.WriteStartArray(key);
                    foreach (var v in kv.Value)
                    {
                        writer.WriteStringValue(v ?? string.Empty);
                    }

                    writer.WriteEndArray();
                }
                else
                {
                    writer.WriteString(key, kv.Value.ToString());
                }
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
