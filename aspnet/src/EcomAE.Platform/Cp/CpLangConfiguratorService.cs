using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Erp;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Cp;

/// <summary>
/// PHP <c>cp/content/lang/page_lang_configurator.php</c> twin (multilang on/off, active languages, default language)
/// plus the <c>ajax_get_text_strings.php</c> search grid used by <c>page_lang_editor.php</c>.
/// </summary>
public interface ICpLangConfiguratorService
{
    Task<CpLangConfiguratorReadResult> ReadAsync(CancellationToken cancellationToken = default);

    Task<CpLangStringSearchResult> SearchStringsAsync(CpLangStringSearch search, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveConfigurationAsync(bool multilangOn, IReadOnlyList<string> activeLangs, string? defaultLang, CancellationToken cancellationToken = default);
}

public sealed record CpLanguageRow(long Id, string LangCode, string Caption, int Active, int IsDefault);

public sealed record CpLangConfiguratorReadResult(IReadOnlyList<CpLanguageRow> Languages, bool MultilangOn, bool ConfigFileWritable, string Source, string Message);

/// <param name="Scope">PHP <c>filter_translated</c>: all | full | partial | none | missing.</param>
public sealed record CpLangStringSearch(string? Query, string Scope, string? OnlyCustom, int Page, int PageSize)
{
    public static CpLangStringSearch Default => new(null, "all", null, 1, 50);
}

public sealed record CpLangStringRow(string StrKey, string Description, int IsCustom, int IsError, string Same, int UsedFound, IReadOnlyDictionary<string, string> Translations);

public sealed record CpLangStringSearchResult(IReadOnlyList<CpLangStringRow> Rows, IReadOnlyList<string> Languages, long Total, int Page, int PageSize, string Source, string Message);

public sealed class CpLangConfiguratorService : ICpLangConfiguratorService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly PhpReferenceOptions _reference;

    public CpLangConfiguratorService(IErpWriteConnectionFactory connections, IOptions<PhpReferenceOptions> reference)
    {
        _connections = connections;
        _reference = reference.Value;
    }

    private string ConfigPath
    {
        get
        {
            var root = (_reference.PhpDocRoot ?? Environment.GetEnvironmentVariable("ECOMAE_PHP_DOCROOT") ?? string.Empty).Trim();
            return root.Length == 0 ? string.Empty : Path.Combine(root, "config.php");
        }
    }

    public async Task<CpLangConfiguratorReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new([], false, false, "migration", "TenantRegistry DB is not configured.");
        }

        var path = ConfigPath;
        var multilang = false;
        var writable = false;
        var message = string.Empty;
        if (path.Length > 0 && File.Exists(path))
        {
            try
            {
                var values = PhpConfigFile.Values(PhpConfigFile.Parse(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)));
                multilang = values.TryGetValue("multilang", out var m) && PhpConfigFile.IsTruthy(m);
                writable = true;
            }
            catch (IOException ex) { message = "config.php could not be read: " + ex.Message; }
            catch (UnauthorizedAccessException ex) { message = "config.php could not be read: " + ex.Message; }
        }
        else
        {
            message = "config.php is not reachable (EcomAE:PhpReference:PhpDocRoot); the multilang switch cannot be saved from ASP.NET.";
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var rows = await ReadLanguagesAsync(connection, cancellationToken).ConfigureAwait(false);
            return new(rows, multilang, writable, "lang_languages", message);
        }
        catch (DbException ex)
        {
            return new([], multilang, writable, "migration", ex.Message);
        }
    }

    public async Task<CpLangStringSearchResult> SearchStringsAsync(CpLangStringSearch search, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new([], [], 0, 1, search.PageSize, "migration", "TenantRegistry DB is not configured.");
        }

        var page = Math.Max(1, search.Page);
        var pageSize = Math.Clamp(search.PageSize, 10, 200);
        var scope = (search.Scope ?? "all").Trim().ToLowerInvariant();
        var q = (search.Query ?? string.Empty).Trim();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var languages = (await ReadLanguagesAsync(connection, cancellationToken).ConfigureAwait(false))
                .Where(l => l.Active == 1 || l.IsDefault == 1)
                .Select(l => l.LangCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (languages.Count == 0) { languages = ["en"]; }

            var where = new List<string>();
            var args = new List<object?>();
            if (q.Length > 0)
            {
                where.Add("(s.`str_key` = ? OR s.`description` LIKE ? OR (SELECT COUNT(*) FROM `lang_text_strings_translation` x WHERE x.`str_key` = s.`str_key` AND x.`value` LIKE ?) > 0)");
                args.Add(q);
                args.Add("%" + q + "%");
                args.Add("%" + q + "%");
            }

            const string translatedCount = "(SELECT COUNT(*) FROM `lang_text_strings_translation` x WHERE x.`str_key` = s.`str_key`)";
            const string langCount = "(SELECT COUNT(*) FROM `lang_languages`)";
            switch (scope)
            {
                case "full": where.Add(translatedCount + " = " + langCount); break;
                case "partial": where.Add(translatedCount + " < " + langCount + " AND " + translatedCount + " != 0"); break;
                case "none": where.Add(translatedCount + " = 0"); break;
                case "missing": where.Add(translatedCount + " < " + langCount); break;
            }

            if (search.OnlyCustom is "1") { where.Add("s.`is_custom` = 1"); }
            else if (search.OnlyCustom is "0") { where.Add("s.`is_custom` = 0"); }

            var whereSql = where.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", where);
            var total = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings` s" + whereSql), cancellationToken, args.ToArray()).ConfigureAwait(false);

            var rows = new List<CpLangStringRow>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(
                    "SELECT s.`str_key`, IFNULL(s.`description`,''), IFNULL(s.`is_custom`,0), IFNULL(s.`is_error`,0), IFNULL(s.`same`,'no'), IFNULL(s.`used_found`,0) FROM `lang_text_strings` s"
                    + whereSql + " ORDER BY CAST(s.`str_key` AS UNSIGNED) DESC, s.`str_key` DESC LIMIT " + pageSize.ToString(CultureInfo.InvariantCulture) + " OFFSET " + ((page - 1) * pageSize).ToString(CultureInfo.InvariantCulture));
                ErpDb.AddParameters(cmd, args.ToArray());
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpLangStringRow(
                        reader.GetValue(0).ToString() ?? string.Empty,
                        reader.GetString(1),
                        Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
                        reader.GetString(4),
                        Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture),
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));
                }
            }

            if (rows.Count > 0)
            {
                var keys = rows.Select(r => r.StrKey).ToList();
                var map = rows.ToDictionary(r => r.StrKey, r => (Dictionary<string, string>)r.Translations, StringComparer.Ordinal);
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = ErpDb.Positional("SELECT `str_key`, `lang_code`, IFNULL(`value`,'') FROM `lang_text_strings_translation` WHERE `str_key` IN (" + string.Join(",", keys.Select(_ => "?")) + ")");
                ErpDb.AddParameters(cmd, keys.Cast<object?>().ToArray());
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var key = reader.GetValue(0).ToString() ?? string.Empty;
                    if (map.TryGetValue(key, out var t))
                    {
                        t[reader.GetString(1)] = reader.GetString(2);
                    }
                }
            }

            return new(rows, languages, total, page, pageSize, "lang_text_strings", string.Empty);
        }
        catch (DbException ex)
        {
            return new([], [], 0, page, pageSize, "migration", ex.Message);
        }
    }

    public async Task<ErpSimpleWriteResult> SaveConfigurationAsync(bool multilangOn, IReadOnlyList<string> activeLangs, string? defaultLang, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var def = (defaultLang ?? string.Empty).Trim();
        if (def.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose the default language.");
        }

        var active = activeLangs.Select(a => (a ?? string.Empty).Trim()).Where(a => a.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (multilangOn && !active.Contains(def, StringComparer.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("invalid", "The default language must be one of the active languages.");
        }

        if (!multilangOn)
        {
            active = [def];
        }

        var path = ConfigPath;
        if (path.Length == 0 || !File.Exists(path))
        {
            return ErpSimpleWriteResult.Fail("invalid", "config.php is not reachable (EcomAE:PhpReference:PhpDocRoot), so the multilang switch cannot be saved.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var code in active.Append(def).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var known = await ErpDb.LongAsync(connection, transaction, ErpDb.Positional("SELECT COUNT(*) FROM `lang_languages` WHERE `lang_code` = ?"), cancellationToken, code).ConfigureAwait(false);
                if (known != 1)
                {
                    throw new ErpWriteException("Unknown language code: " + code);
                }
            }

            var writes = 0;
            writes += await ErpDb.ExecuteAsync(connection, transaction, "UPDATE `lang_languages` SET `active` = 0", cancellationToken).ConfigureAwait(false);
            writes += await ErpDb.ExecuteAsync(connection, transaction, ErpDb.Positional("UPDATE `lang_languages` SET `active` = 1 WHERE `lang_code` IN (" + string.Join(",", active.Select(_ => "?")) + ")"), cancellationToken, active.Cast<object?>().ToArray()).ConfigureAwait(false);
            writes += await ErpDb.ExecuteAsync(connection, transaction, "UPDATE `lang_languages` SET `is_default` = 0", cancellationToken).ConfigureAwait(false);
            writes += await ErpDb.ExecuteAsync(connection, transaction, ErpDb.Positional("UPDATE `lang_languages` SET `is_default` = 1 WHERE `lang_code` = ?"), cancellationToken, def).ConfigureAwait(false);

            var lines = PhpConfigFile.Parse(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false));
            var rendered = PhpConfigFile.Render(PhpConfigFile.Set(lines, "multilang", multilangOn ? "1" : string.Empty));
            var tmp = path + ".aspnet.tmp";
            await File.WriteAllTextAsync(tmp, rendered, new System.Text.UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            File.Move(tmp, path, overwrite: true);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ErpSimpleWriteResult(true, "ok", "Language configuration saved (multilang " + (multilangOn ? "on" : "off") + ", default " + def + ", " + active.Count.ToString(CultureInfo.InvariantCulture) + " active).", 0, writes + 1);
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

    private static async Task<List<CpLanguageRow>> ReadLanguagesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<CpLanguageRow>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT l.`id`, l.`lang_code`, IFNULL(t.`value`, l.`lang_code`) AS caption, IFNULL(l.`active`,0), IFNULL(l.`is_default`,0)
            FROM `lang_languages` l
            LEFT JOIN `lang_text_strings_translation` t ON t.`str_key` = CAST(l.`caption_str_key` AS CHAR) AND t.`lang_code` = 'en'
            ORDER BY l.`id` ASC
            """;
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CpLanguageRow(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                reader.GetString(1),
                reader.GetString(2),
                Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
                Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture)));
        }

        return rows;
    }
}
