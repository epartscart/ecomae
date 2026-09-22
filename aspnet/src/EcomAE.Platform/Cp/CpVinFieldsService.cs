using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>PHP <c>cp/content/requests/vin_fields_edit.php</c> read model: one <c>vin_fields</c> row with resolved translations.</summary>
public sealed record CpVinField(
    long Id,
    string Name,
    string Caption,
    string CaptionLangStrId,
    string Regexp,
    string RegexpLangStrId,
    string Example,
    string ExampleLangStrId,
    int MaxLen,
    bool Show,
    bool Required);

/// <summary>One serialized tree item posted back from the editor (PHP <c>tree_json</c>).</summary>
public sealed record CpVinFieldSaveItem(
    long Id,
    bool IsNew,
    string Name,
    string Value,
    string ValueLangStrId,
    string Regexp,
    string RegexpLangStrId,
    string Example,
    string ExampleLangStrId,
    int MaxLen,
    bool Show,
    bool Required);

public interface ICpVinFieldsService
{
    Task<IReadOnlyList<CpVinField>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>PHP save_action: order reset, per-item translation upsert + INSERT/UPDATE, then DELETE of rows left at order 0.</summary>
    Task<ErpSimpleWriteResult> SaveAsync(string? treeJson, CancellationToken cancellationToken = default);
}

public sealed partial class CpVinFieldsService : ICpVinFieldsService
{
    public const string ProtectedName = "client_vin";
    public const int MaxFields = 200;

    /// <summary>PHP forbids keys equal to a <c>users</c> table column (INFORMATION_SCHEMA lookup); mirrored as the known column set.</summary>
    public static readonly IReadOnlyList<string> ReservedUserColumns =
    [
        "user_id", "email", "phone", "password", "unlocked", "reg_variant", "time_registered", "time_last_visit",
        "admin_created", "comment", "email_confirmed", "phone_confirmed", "session", "balance",
    ];
    private static readonly string[] ReservedNames = ReservedUserColumns.ToArray();

    private readonly IErpWriteConnectionFactory _connections;

    public CpVinFieldsService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    [GeneratedRegex("^[a-z_]+$")]
    private static partial Regex NamePattern();

    public static bool IsValidName(string name) => name.Length > 0 && NamePattern().IsMatch(name) && !ReservedNames.Contains(name, StringComparer.Ordinal);

    /// <summary>Parses the editor tree JSON; rejects unknown shapes instead of guessing.</summary>
    public static (IReadOnlyList<CpVinFieldSaveItem> Items, string? Error) ParseTree(string? treeJson)
    {
        if (string.IsNullOrWhiteSpace(treeJson))
        {
            return ([], "tree_json is required.");
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(treeJson);
        }
        catch (JsonException)
        {
            return ([], "tree_json is not valid JSON.");
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ([], "tree_json must be an array.");
            }

            var items = new List<CpVinFieldSaveItem>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = Str(e, "name").Trim();
                var value = Str(e, "value").Trim();
                if (!IsValidName(name))
                {
                    return ([], "Field key '" + name + "' (" + value + ") must contain only lowercase latin letters and underscores and must not clash with a users column.");
                }

                if (!names.Add(name))
                {
                    return ([], "Field key '" + name + "' is used more than once.");
                }

                items.Add(new CpVinFieldSaveItem(
                    Long(e, "id"),
                    Bool(e, "is_new"),
                    name,
                    Encode(value),
                    Str(e, "value_lang_str_id"),
                    Encode(Str(e, "regexp")),
                    Str(e, "regexp_lang_str_id"),
                    Encode(Str(e, "example")),
                    Str(e, "example_lang_str_id"),
                    (int)Math.Clamp(Long(e, "maxlen"), 0, 65535),
                    Bool(e, "show"),
                    Bool(e, "required")));
                if (items.Count > MaxFields)
                {
                    return ([], "Too many fields.");
                }
            }

            return (items, null);
        }
    }

    public async Task<IReadOnlyList<CpVinField>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var raw = new List<(long Id, string Name, string Caption, string Regexp, string Example, int MaxLen, bool Show, bool Required)>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT `record_id`, IFNULL(`name`,''), IFNULL(`caption`,''), IFNULL(`regexp`,''), IFNULL(`example`,''), " +
                    "IFNULL(`maxlen`,0), IFNULL(`show`,0), IFNULL(`required`,0) FROM `vin_fields` ORDER BY `order` ASC";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    raw.Add((
                        Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetString(4),
                        Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt32(reader.GetValue(7), CultureInfo.InvariantCulture) != 0));
                }
            }

            var list = new List<CpVinField>(raw.Count);
            foreach (var r in raw)
            {
                list.Add(new CpVinField(
                    r.Id,
                    r.Name,
                    await TranslateAsync(connection, r.Caption, cancellationToken).ConfigureAwait(false),
                    r.Caption,
                    await TranslateAsync(connection, r.Regexp, cancellationToken).ConfigureAwait(false),
                    r.Regexp,
                    await TranslateAsync(connection, r.Example, cancellationToken).ConfigureAwait(false),
                    r.Example,
                    r.MaxLen,
                    r.Show,
                    r.Required));
            }

            return list;
        }
        catch (DbException)
        {
            return [];
        }
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(string? treeJson, CancellationToken cancellationToken = default)
    {
        var (items, error) = ParseTree(treeJson);
        if (error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", error);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var protectedCount = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `vin_fields` WHERE `name` = ?"),
            cancellationToken,
            ProtectedName).ConfigureAwait(false);
        if (protectedCount > 0 && !items.Any(i => i.Name == ProtectedName))
        {
            return ErpSimpleWriteResult.Fail("invalid", "The " + ProtectedName + " field cannot be deleted.");
        }

        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var writes = await ErpDb.ExecuteAsync(connection, tx, "UPDATE `vin_fields` SET `order` = 0", cancellationToken).ConfigureAwait(false);
            var translations = new CpCustomTranslationWriter("VIN FIELDS EDITING");
            var order = 0;
            foreach (var item in items)
            {
                order++;
                var captionKey = await translations.SaveAsync(connection, tx, item.ValueLangStrId, item.Value, "en", null, cancellationToken).ConfigureAwait(false);
                var regexpKey = await translations.SaveAsync(connection, tx, item.RegexpLangStrId, item.Regexp, "en", null, cancellationToken).ConfigureAwait(false);
                var exampleKey = await translations.SaveAsync(connection, tx, item.ExampleLangStrId, item.Example, "en", null, cancellationToken).ConfigureAwait(false);
                var show = item.Show ? 1 : 0;
                var required = item.Required ? 1 : 0;
                if (item.IsNew || item.Id <= 0)
                {
                    writes += await ErpDb.ExecuteAsync(
                        connection,
                        tx,
                        ErpDb.Positional(
                            "INSERT INTO `vin_fields` (`main_flag`, `name`, `caption`, `show`, `required`, `maxlen`, `regexp`, `widget_type`, `widget_options`, `example`, `order`) " +
                            "VALUES (?,?,?,?,?,?,?,?,?,?,?)"),
                        cancellationToken,
                        0, item.Name, captionKey, show, required, item.MaxLen, regexpKey, "text", "[]", exampleKey, order).ConfigureAwait(false);
                }
                else
                {
                    writes += await ErpDb.ExecuteAsync(
                        connection,
                        tx,
                        ErpDb.Positional(
                            "UPDATE `vin_fields` SET `main_flag` = ?, `name` = ?, `caption` = ?, `show` = ?, `required` = ?, `maxlen` = ?, `regexp` = ?, " +
                            "`widget_type` = ?, `widget_options` = ?, `example` = ?, `order` = ? WHERE `record_id` = ?"),
                        cancellationToken,
                        0, item.Name, captionKey, show, required, item.MaxLen, regexpKey, "text", "[]", exampleKey, order, item.Id).ConfigureAwait(false);
                }
            }

            writes += await ErpDb.ExecuteAsync(connection, tx, "DELETE FROM `vin_fields` WHERE `order` = 0", cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ErpSimpleWriteResult(true, "ok", "Request fields saved.", items.Count, Math.Max(1, writes));
        }
        catch (Exception ex) when (ex is DbException or ErpWriteException)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("db", "Could not save request fields: " + ex.Message);
        }
    }

    private static async Task<string> TranslateAsync(DbConnection connection, string key, CancellationToken cancellationToken)
    {
        key = key.Trim();
        if (key.Length == 0)
        {
            return key;
        }

        var text = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? ORDER BY `lang_code` = 'en' DESC LIMIT 1"),
            cancellationToken,
            key).ConfigureAwait(false);
        return text ?? key;
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(WebUtility.HtmlDecode(value));

    private static string Str(JsonElement e, string name)
        => e.TryGetProperty(name, out var v)
            ? (v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? string.Empty : v.ToString())
            : string.Empty;

    private static long Long(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v))
        {
            return 0;
        }

        return v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n)
            ? n
            : long.TryParse(Str(e, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? p : 0;
    }

    private static bool Bool(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v))
        {
            return false;
        }

        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => v.TryGetInt64(out var n) && n != 0,
            JsonValueKind.String => v.GetString() is "1" or "true",
            _ => false,
        };
    }
}
