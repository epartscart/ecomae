using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpMenuListRow(long Id, string Caption);

public sealed record CpMenuList(IReadOnlyList<CpMenuListRow> Rows, int Total, int Page, int PageSize, bool IsFrontend)
{
    public int PageCount => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public sealed record CpMenuContentOption(long Id, string Caption, string Url);

/// <summary>
/// Editor state of <c>menu_edit.php</c>: caption (translated + lang key), UL attributes and the structure dump with
/// <c>value</c>/<c>a_innerhtml</c> translated and their <c>*_lang_str_id</c> set (PHP <c>tree_htmlentities($data, true)</c>).
/// </summary>
public sealed record CpMenuEditor(
    long Id,
    bool IsFrontend,
    string Caption,
    string CaptionLangStrId,
    string MenuUlClass,
    string MenuUlId,
    string TreeJson,
    IReadOnlyList<CpMenuContentOption> ContentOptions);

public interface ICpMenuEditorService
{
    Task<CpMenuList> ListAsync(bool isFrontend, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<CpMenuEditor?> OpenAsync(long menuId, bool isFrontend, CancellationToken cancellationToken = default);
}

/// <summary>Read side of the CP menus twin (<c>menu_manager.php</c> / <c>menu_edit.php</c>); writes stay in <see cref="ICpMenuWriteService"/>.</summary>
public sealed class CpMenuEditorService : ICpMenuEditorService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpMenuEditorService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CpMenuList> ListAsync(bool isFrontend, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var rows = new List<CpMenuListRow>();
        var total = 0;
        if (!_connections.IsConfigured)
        {
            return new(rows, total, page, pageSize, isFrontend);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var frontend = isFrontend ? 1 : 0;
            total = (int)await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `menu` WHERE `is_frontend` = ?"),
                cancellationToken,
                frontend).ConfigureAwait(false);

            var raw = new List<(long Id, string Caption)>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(
                    "SELECT `id`, IFNULL(`caption`,'') FROM `menu` WHERE `is_frontend` = ? ORDER BY `id` LIMIT ? OFFSET ?");
                ErpDb.AddParameters(cmd, frontend, pageSize, (page - 1) * pageSize);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    raw.Add((Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture), reader.GetString(1)));
                }
            }

            var translate = CpOfficeEditorService.Translator(connection, cancellationToken);
            foreach (var r in raw)
            {
                rows.Add(new CpMenuListRow(r.Id, await translate(r.Caption).ConfigureAwait(false)));
            }
        }
        catch (DbException)
        {
        }

        return new(rows, total, page, pageSize, isFrontend);
    }

    public async Task<CpMenuEditor?> OpenAsync(long menuId, bool isFrontend, CancellationToken cancellationToken = default)
    {
        var caption = string.Empty;
        var captionKey = "0";
        var ulClass = string.Empty;
        var ulId = string.Empty;
        var tree = "[]";
        var options = new List<CpMenuContentOption>();
        if (!_connections.IsConfigured)
        {
            return menuId > 0 ? null : new CpMenuEditor(0, isFrontend, caption, captionKey, ulClass, ulId, tree, options);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var translate = CpOfficeEditorService.Translator(connection, cancellationToken);
            if (menuId > 0)
            {
                string? rawCaption = null;
                string? rawStructure = null;
                await using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = ErpDb.Positional(
                        "SELECT IFNULL(`caption`,''), IFNULL(`structure`,'[]'), IFNULL(`menu_ul_class`,''), IFNULL(`menu_ul_id`,''), IFNULL(`is_frontend`,1) FROM `menu` WHERE `id` = ? LIMIT 1");
                    ErpDb.AddParameters(cmd, menuId);
                    await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        return null;
                    }

                    rawCaption = reader.GetString(0);
                    rawStructure = reader.GetString(1);
                    ulClass = reader.GetString(2);
                    ulId = reader.GetString(3);
                    isFrontend = Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture) > 0;
                }

                captionKey = rawCaption.All(char.IsDigit) && rawCaption.Length > 0 ? rawCaption : "0";
                caption = await translate(rawCaption).ConfigureAwait(false);
                tree = await TranslateTreeAsync(rawStructure, translate).ConfigureAwait(false);
            }

            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(
                    "SELECT `id`, IFNULL(`value`,''), IFNULL(`url`,'') FROM `content` WHERE `is_frontend` = ? ORDER BY `level`, `order`, `id` LIMIT 2000");
                ErpDb.AddParameters(cmd, isFrontend ? 1 : 0);
                var raw = new List<(long Id, string Value, string Url)>();
                await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        raw.Add((Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture), reader.GetString(1), reader.GetString(2)));
                    }
                }

                foreach (var r in raw)
                {
                    options.Add(new CpMenuContentOption(r.Id, await translate(r.Value).ConfigureAwait(false), r.Url));
                }
            }
        }
        catch (DbException)
        {
            if (menuId > 0)
            {
                return null;
            }
        }

        return new CpMenuEditor(menuId, isFrontend, caption, captionKey, ulClass, ulId, tree, options);
    }

    private static async Task<string> TranslateTreeAsync(string? rawStructure, Func<string, Task<string>> translate)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(string.IsNullOrWhiteSpace(rawStructure) ? "[]" : rawStructure);
        }
        catch (JsonException)
        {
            return "[]";
        }

        if (root is not JsonArray arr)
        {
            return "[]";
        }

        await WalkAsync(arr, translate).ConfigureAwait(false);
        return arr.ToJsonString(JsonSerializerOptions.Default);
    }

    private static async Task WalkAsync(JsonArray arr, Func<string, Task<string>> translate)
    {
        foreach (var node in arr)
        {
            if (node is not JsonObject obj)
            {
                continue;
            }

            foreach (var key in new[] { "value", "a_innerhtml" })
            {
                var text = ReadText(obj, key);
                var langKey = text.Length > 0 && text.All(char.IsDigit) ? text : "0";
                obj[key + "_lang_str_id"] = langKey;
                obj[key] = await translate(text).ConfigureAwait(false);
            }

            if (obj.TryGetPropertyValue("data", out var data) && data is JsonArray children)
            {
                await WalkAsync(children, translate).ConfigureAwait(false);
            }
        }
    }

    private static string ReadText(JsonObject obj, string key)
    {
        if (!obj.TryGetPropertyValue(key, out var node) || node is not JsonValue value)
        {
            return string.Empty;
        }

        return value.GetValueKind() switch
        {
            JsonValueKind.String => value.GetValue<string>() ?? string.Empty,
            JsonValueKind.Number => value.ToJsonString(),
            _ => string.Empty
        };
    }
}
