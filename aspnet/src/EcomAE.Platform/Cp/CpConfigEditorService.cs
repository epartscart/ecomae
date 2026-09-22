using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Erp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Cp;

/// <summary>
/// PHP <c>cp/content/control/config_edit.php</c> + <c>DP_ConfigEditor</c> twin.
/// Metadata (groups, items, widget types) comes from <c>config_groups</c> / <c>config_items</c>;
/// values live in the PHP <c>config.php</c> under <see cref="PhpReferenceOptions.PhpDocRoot"/> so both runtimes read the same settings.
/// </summary>
public interface ICpConfigEditorService
{
    Task<CpConfigEditorReadResult> ReadAsync(int needConfigGroup, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveAsync(IFormCollection form, int needConfigGroup, string? langCode, string? domainPath, CancellationToken cancellationToken = default);
}

public sealed record CpConfigOption(string Value, string Caption);

public sealed record CpConfigItem(
    long Id,
    int GroupId,
    string Name,
    string Caption,
    string Type,
    IReadOnlyList<CpConfigOption> Options,
    string Hint,
    int HtmlEntities,
    string DefaultValue,
    string CurrentValue,
    string LangStrId,
    bool IsTranslated);

public sealed record CpConfigGroup(int Id, string Caption, IReadOnlyList<CpConfigItem> Items);

public sealed record CpConfigEditorReadResult(
    IReadOnlyList<CpConfigGroup> Groups,
    string Source,
    string Message,
    string ConfigPath,
    bool ConfigFileWritable);

/// <summary>Line model of PHP <c>config.php</c> (<c>public $name = 'value';comment</c>) as parsed by <c>DP_ConfigEditor::setParameter</c>.</summary>
public static class PhpConfigFile
{
    private static readonly Regex Parameter = new(
        @"^\s*public\s+\$([a-zA-Z0-9_]+)\s*=\s*'(.*)';(.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public sealed record Line(string Raw, string? Name, string? Value, string? Comment)
    {
        public bool IsParameter => Name is not null;
    }

    public static IReadOnlyList<Line> Parse(string text)
    {
        var lines = new List<Line>();
        foreach (var raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var m = Parameter.Match(raw);
            lines.Add(m.Success
                ? new Line(raw, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value)
                : new Line(raw, null, null, null));
        }

        return lines;
    }

    public static IReadOnlyDictionary<string, string> Values(IReadOnlyList<Line> lines)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            if (line.IsParameter && !map.ContainsKey(line.Name!))
            {
                map[line.Name!] = line.Value ?? string.Empty;
            }
        }

        return map;
    }

    /// <summary>Replaces the value of an existing <c>public $name</c> line; unknown names are appended before the closing brace.</summary>
    public static List<Line> Set(IReadOnlyList<Line> lines, string name, string value)
    {
        var result = new List<Line>(lines.Count + 1);
        var replaced = false;
        foreach (var line in lines)
        {
            if (line.IsParameter && string.Equals(line.Name, name, StringComparison.Ordinal))
            {
                result.Add(new Line(Render(name, value, line.Comment ?? string.Empty), name, value, line.Comment));
                replaced = true;
            }
            else
            {
                result.Add(line);
            }
        }

        if (!replaced)
        {
            var close = result.FindLastIndex(l => !l.IsParameter && l.Raw.TrimStart().StartsWith('}'));
            var inserted = new Line(Render(name, value, string.Empty), name, value, string.Empty);
            if (close >= 0) { result.Insert(close, inserted); } else { result.Add(inserted); }
        }

        return result;
    }

    public static string Render(string name, string value, string comment)
        => "\tpublic $" + name + " = '" + value + "';" + comment;

    public static string Render(IReadOnlyList<Line> lines)
    {
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            sb.Append(line.Raw).Append('\n');
        }

        return sb.ToString().TrimEnd('\n');
    }

    /// <summary>PHP <c>config_edit.php</c> value sanitisation before <c>setParameter</c>.</summary>
    public static string Sanitize(string? posted, string name, int htmlEntities)
    {
        var value = (posted ?? string.Empty).Trim();
        if (htmlEntities == 1)
        {
            value = System.Net.WebUtility.HtmlEncode(value);
        }

        value = value.Replace("'", "&#039;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal);
        if (name is "epc_head_office_address" or "epc_global_locations_countries")
        {
            value = value.Replace("\r\n", "\\n", StringComparison.Ordinal).Replace("\r", "\\n", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal).Replace("\t", " ", StringComparison.Ordinal);
        }
        else
        {
            value = value.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal).Replace("\t", string.Empty, StringComparison.Ordinal);
        }

        while (value.Contains("<?", StringComparison.Ordinal)) { value = value.Replace("<?", "[CODE]", StringComparison.Ordinal); }
        while (value.Contains("?>", StringComparison.Ordinal)) { value = value.Replace("?>", "[/CODE]", StringComparison.Ordinal); }
        return value;
    }

    /// <summary>PHP <c>filter_var($value, FILTER_VALIDATE_BOOLEAN)</c> rendered back as <c>1</c>/<c></c> like a PHP bool cast to string.</summary>
    public static string CheckboxValue(string? posted)
    {
        var v = (posted ?? string.Empty).Trim().ToLowerInvariant();
        return v is "1" or "true" or "on" or "yes" ? "1" : string.Empty;
    }

    public static bool IsTruthy(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        return v is "1" or "true" or "on" or "yes";
    }
}

public sealed class CpConfigEditorService : ICpConfigEditorService
{
    public static readonly string[] TranslatedItems = ["site_name", "description_tag", "keywords_tag", "retention_percentage_text"];
    private readonly IErpWriteConnectionFactory _connections;
    private readonly PhpReferenceOptions _reference;
    private readonly CpCustomTranslationWriter _translations = new("CONFIG EDITING");

    public CpConfigEditorService(IErpWriteConnectionFactory connections, IOptions<PhpReferenceOptions> reference)
    {
        _connections = connections;
        _reference = reference.Value;
    }

    public string ConfigPath
    {
        get
        {
            var root = (_reference.PhpDocRoot ?? Environment.GetEnvironmentVariable("ECOMAE_PHP_DOCROOT") ?? string.Empty).Trim();
            return root.Length == 0 ? string.Empty : Path.Combine(root, "config.php");
        }
    }

    public async Task<CpConfigEditorReadResult> ReadAsync(int needConfigGroup, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new([], "migration", "TenantRegistry DB is not configured.", ConfigPath, false);
        }

        var path = ConfigPath;
        IReadOnlyDictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
        var fileOk = false;
        var message = string.Empty;
        if (path.Length == 0)
        {
            message = "EcomAE:PhpReference:PhpDocRoot is not set; showing config_items defaults (values cannot be saved).";
        }
        else if (!File.Exists(path))
        {
            message = "config.php was not found at " + path + "; showing config_items defaults (values cannot be saved).";
        }
        else
        {
            try
            {
                values = PhpConfigFile.Values(PhpConfigFile.Parse(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)));
                fileOk = true;
            }
            catch (IOException ex)
            {
                message = "config.php could not be read: " + ex.Message;
            }
            catch (UnauthorizedAccessException ex)
            {
                message = "config.php could not be read: " + ex.Message;
            }
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var groups = new List<(int Id, string Caption)>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT g.`id`, IFNULL(t.`value`, CAST(g.`caption` AS CHAR)) AS caption
                    FROM `config_groups` g
                    LEFT JOIN `lang_text_strings_translation` t ON t.`str_key` = CAST(g.`caption` AS CHAR) AND t.`lang_code` = 'en'
                    WHERE g.`visible` = 1
                    ORDER BY g.`order` ASC, g.`id` ASC
                    """;
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var id = Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);
                    if (needConfigGroup > 0 && id != needConfigGroup) { continue; }
                    groups.Add((id, reader.IsDBNull(1) ? string.Empty : reader.GetString(1)));
                }
            }

            var items = new List<CpConfigItem>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT i.`id`, IFNULL(i.`config_group`,0), i.`name`, IFNULL(t.`value`, CAST(i.`caption` AS CHAR)) AS caption,
                           IFNULL(i.`type`,'text'), IFNULL(i.`options`,''), IFNULL(h.`value`, '') AS hint,
                           IFNULL(i.`htmlentities`,0), IFNULL(i.`default_value`,'')
                    FROM `config_items` i
                    LEFT JOIN `lang_text_strings_translation` t ON t.`str_key` = CAST(i.`caption` AS CHAR) AND t.`lang_code` = 'en'
                    LEFT JOIN `lang_text_strings_translation` h ON h.`str_key` = CAST(i.`hint` AS CHAR) AND i.`hint` <> '0' AND h.`lang_code` = 'en'
                    WHERE i.`visible` = 1
                    ORDER BY i.`order` ASC, i.`id` ASC
                    """;
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var groupId = Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
                    if (needConfigGroup > 0 && groupId != needConfigGroup) { continue; }
                    var name = reader.GetString(2);
                    var defaultValue = reader.GetString(8);
                    var current = values.TryGetValue(name, out var v) ? v : defaultValue;
                    var translated = Array.IndexOf(TranslatedItems, name) >= 0;
                    items.Add(new CpConfigItem(
                        Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                        groupId,
                        name,
                        reader.IsDBNull(3) ? name : reader.GetString(3),
                        reader.GetString(4).Trim().ToLowerInvariant(),
                        ParseOptions(reader.GetString(5)),
                        reader.GetString(6),
                        Convert.ToInt32(reader.GetValue(7), CultureInfo.InvariantCulture),
                        defaultValue,
                        current,
                        translated ? current : string.Empty,
                        translated));
                }
            }

            items = await ResolveTranslatedValuesAsync(connection, items, cancellationToken).ConfigureAwait(false);

            var byGroup = items.GroupBy(i => i.GroupId).ToDictionary(g => g.Key, g => (IReadOnlyList<CpConfigItem>)g.ToList());
            var result = groups.Select(g => new CpConfigGroup(g.Id, g.Caption, byGroup.TryGetValue(g.Id, out var list) ? list : [])).ToList();
            foreach (var orphan in byGroup.Keys.Where(k => groups.All(g => g.Id != k)).OrderBy(k => k))
            {
                result.Add(new CpConfigGroup(orphan, "Group #" + orphan.ToString(CultureInfo.InvariantCulture), byGroup[orphan]));
            }

            return new(result, fileOk ? "config.php" : "database-defaults", message, path, fileOk);
        }
        catch (DbException ex)
        {
            return new([], "migration", ex.Message, path, fileOk);
        }
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(IFormCollection form, int needConfigGroup, string? langCode, string? domainPath, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var path = ConfigPath;
        if (path.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "EcomAE:PhpReference:PhpDocRoot is not set, so config.php cannot be written from ASP.NET.");
        }

        if (!File.Exists(path))
        {
            return ErpSimpleWriteResult.Fail("invalid", "config.php was not found at " + path + ".");
        }

        var lang = CpCustomTranslationWriter.NormalizeLang(langCode);
        var lines = PhpConfigFile.Parse(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false));
        var writes = 0;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var meta = new List<(string Name, string Type, int HtmlEntities, int GroupId)>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "SELECT `name`, IFNULL(`type`,'text'), IFNULL(`htmlentities`,0), IFNULL(`config_group`,0) FROM `config_items`";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    meta.Add((reader.GetString(0), reader.GetString(1).Trim().ToLowerInvariant(),
                        Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture), Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture)));
                }
            }

            var current = lines;
            foreach (var (name, type, htmlEntities, groupId) in meta)
            {
                if (needConfigGroup > 0 && groupId != needConfigGroup) { continue; }
                if (type == "password")
                {
                    var pw = PhpConfigFile.Sanitize(form[name].ToString(), name, htmlEntities);
                    if (pw.Length == 0) { continue; }
                    current = PhpConfigFile.Set(current, name, pw);
                    writes++;
                    continue;
                }

                if (type == "checkbox")
                {
                    current = PhpConfigFile.Set(current, name, PhpConfigFile.CheckboxValue(form[name].ToString()));
                    writes++;
                    continue;
                }

                if (!form.ContainsKey(name) && type is "image" or "image_file" or "hidden")
                {
                    continue;
                }

                var value = PhpConfigFile.Sanitize(form[name].ToString(), name, htmlEntities);
                if (Array.IndexOf(TranslatedItems, name) >= 0)
                {
                    value = await _translations.SaveAsync(connection, transaction, form[name + "_lang_str_id"].ToString(), value, lang, domainPath, cancellationToken).ConfigureAwait(false);
                }

                current = PhpConfigFile.Set(current, name, value);
                writes++;
            }

            var rendered = PhpConfigFile.Render(current);
            if (!rendered.TrimStart().StartsWith("<?php", StringComparison.Ordinal))
            {
                rendered = "<?php\n" + rendered;
            }

            var tmp = path + ".aspnet.tmp";
            await File.WriteAllTextAsync(tmp, rendered, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            File.Move(tmp, path, overwrite: true);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ErpSimpleWriteResult(true, "ok", "Settings saved to config.php (" + writes.ToString(CultureInfo.InvariantCulture) + " parameters).", 0, writes);
        }
        catch (ErpWriteException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
        catch (DbException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("db", ex.Message);
        }
        catch (IOException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("io", "config.php could not be written: " + ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("io", "config.php is not writable by the ASP.NET process: " + ex.Message);
        }
    }

    private static async Task<List<CpConfigItem>> ResolveTranslatedValuesAsync(DbConnection connection, List<CpConfigItem> items, CancellationToken cancellationToken)
    {
        var cache = new Dictionary<string, string?>(StringComparer.Ordinal);
        async Task<string?> TranslateAsync(string key)
        {
            if (key.Length == 0 || !key.All(char.IsDigit)) { return null; }
            if (!cache.TryGetValue(key, out var text))
            {
                text = await ErpDb.StringAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = 'en' LIMIT 1"),
                    cancellationToken,
                    key).ConfigureAwait(false);
                cache[key] = text;
            }

            return text;
        }

        var resolved = new List<CpConfigItem>(items.Count);
        foreach (var item in items)
        {
            var next = item;
            if (item.IsTranslated && item.LangStrId.Length > 0)
            {
                next = next with { CurrentValue = await TranslateAsync(item.LangStrId).ConfigureAwait(false) ?? item.CurrentValue };
            }

            if (item.Options.Count > 0)
            {
                var options = new List<CpConfigOption>(item.Options.Count);
                foreach (var o in item.Options)
                {
                    options.Add(o with { Caption = await TranslateAsync(o.Caption).ConfigureAwait(false) ?? o.Caption });
                }

                next = next with { Options = options };
            }

            resolved.Add(next);
        }

        return resolved;
    }

    private static IReadOnlyList<CpConfigOption> ParseOptions(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) { return []; }
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) { return []; }
            var list = new List<CpConfigOption>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) { continue; }
                var value = el.TryGetProperty("value", out var v) ? v.ToString() : string.Empty;
                var caption = el.TryGetProperty("caption", out var c) ? c.ToString() : value;
                list.Add(new CpConfigOption(value, caption));
            }

            return list;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
