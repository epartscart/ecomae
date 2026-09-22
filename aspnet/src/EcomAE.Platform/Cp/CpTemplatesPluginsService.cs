using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Cp;

public sealed record CpWidgetOption(string Value, string Caption);

/// <summary>One <c>data_structure</c> entry rendered through PHP <c>get_widget()</c>.</summary>
public sealed record CpWidgetField(
    string Name,
    string Caption,
    string Type,
    string Value,
    string LangStrId,
    bool Multilang,
    IReadOnlyList<CpWidgetOption> Options);

public sealed record CpTemplateRow(long Id, string Name, string Caption, bool PhoneSupport, bool TabletSupport, bool Current);

public sealed record CpTemplateDetail(
    long Id,
    string Name,
    string Caption,
    bool Current,
    int IsFrontend,
    IReadOnlyList<CpWidgetField> Fields);

public sealed record CpPluginRow(long Id, string Caption, int Order, bool Activated, bool ControlLock);

public sealed record CpPluginDetail(
    long Id,
    string Caption,
    string CaptionLangStrId,
    string Description,
    string DescriptionLangStrId,
    int Order,
    bool Activated,
    bool ControlLock,
    int IsFrontend,
    IReadOnlyList<CpWidgetField> Fields);

public sealed record CpPagedList<T>(IReadOnlyList<T> Items, int Total, int Page, int Pages);

public sealed record CpModuleRow(long Id, string Caption, string PrototypeName, string ContentType, string Position, bool Activated);

public sealed record CpModulePrototype(long Id, string Name);

public sealed record CpModuleGroup(long Id, string Caption, int Level);

public sealed record CpTemplatePositions(long TemplateId, string Caption, IReadOnlyList<CpWidgetOption> Positions);

/// <summary>PHP <c>edit_module.php</c> page state: <c>Mode</c> = create (from prototype) | edit.</summary>
public sealed record CpModuleEditor(
    string Mode,
    long Id,
    long PrototypeId,
    string PrototypeName,
    string PrototypeNameLangStrId,
    string Caption,
    string CaptionLangStrId,
    string ContentType,
    string Content,
    string ContentLangStrId,
    string Position,
    bool Activated,
    bool ShowCaption,
    int Order,
    bool ForAll,
    int IsFrontend,
    IReadOnlyList<CpWidgetField> Fields,
    IReadOnlyList<long> ContentIds,
    IReadOnlyList<long> GroupsAllowed,
    string ContentTreeJson,
    IReadOnlyList<CpModuleGroup> Groups,
    IReadOnlyList<CpTemplatePositions> Templates);

public interface ICpTemplatesPluginsService
{
    Task<CpPagedList<CpTemplateRow>> TemplatesAsync(int isFrontend, int page, CancellationToken cancellationToken = default);
    Task<CpTemplateDetail?> TemplateAsync(long id, int isFrontend, CancellationToken cancellationToken = default);
    Task<CpPagedList<CpPluginRow>> PluginsAsync(int isFrontend, int page, CancellationToken cancellationToken = default);
    Task<CpPluginDetail?> PluginAsync(long id, int isFrontend, CancellationToken cancellationToken = default);
    Task<CpPagedList<CpModuleRow>> ModulesAsync(int isFrontend, int page, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CpModulePrototype>> ModulePrototypesAsync(int isFrontend, CancellationToken cancellationToken = default);
    Task<CpModuleEditor?> ModuleEditorAsync(long moduleId, long prototypeId, int isFrontend, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read twin of PHP <c>templates_control/templates_manager.php</c>, <c>template_edit.php</c>,
/// <c>plugins_control/plugins_manager.php</c> and <c>plugin_edit.php</c> (tables <c>templates</c>, <c>plugins</c>).
/// </summary>
public sealed class CpTemplatesPluginsService : ICpTemplatesPluginsService
{
    /// <summary>PHP <c>DP_Config->list_page_limit</c> default.</summary>
    public const int PageLimit = 20;
    public const string EditModeCookie = "edit_mode";

    private readonly IErpWriteConnectionFactory _connections;

    public CpTemplatesPluginsService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP: cookie <c>edit_mode</c> = frontend|backend, default frontend (1).</summary>
    public static int ReadIsFrontend(HttpRequest request)
        => string.Equals(request.Cookies[EditModeCookie], "backend", StringComparison.OrdinalIgnoreCase) ? 0 : 1;

    public static int ReadPage(HttpRequest request)
    {
        var raw = request.Query["s_page"].ToString();
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) && p > 0 ? p : 0;
    }

    public async Task<CpPagedList<CpTemplateRow>> TemplatesAsync(int isFrontend, int page, CancellationToken cancellationToken = default)
    {
        var rows = new List<CpTemplateRow>();
        if (!_connections.IsConfigured)
        {
            return new(rows, 0, 0, 0);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var total = (int)await ErpDb.LongAsync(connection, null,
                ErpDb.Positional("SELECT COUNT(*) FROM `templates` WHERE `is_frontend` = ?"), cancellationToken, isFrontend).ConfigureAwait(false);
            var pages = Pages(total);
            page = Math.Clamp(page, 0, Math.Max(0, pages - 1));

            await using var c = connection.CreateCommand();
            c.CommandText = ErpDb.Positional(
                "SELECT `id`, IFNULL(`name`,''), IFNULL(`caption`,''), IFNULL(`phone_support`,0), IFNULL(`tablet_support`,0), IFNULL(`current`,0) " +
                "FROM `templates` WHERE `is_frontend` = ? ORDER BY `id` LIMIT ? OFFSET ?");
            ErpDb.AddParameters(c, isFrontend, PageLimit, page * PageLimit);
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpTemplateRow(
                    Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                    r.GetString(1),
                    r.GetString(2),
                    Convert.ToInt32(r.GetValue(3), CultureInfo.InvariantCulture) == 1,
                    Convert.ToInt32(r.GetValue(4), CultureInfo.InvariantCulture) == 1,
                    Convert.ToInt32(r.GetValue(5), CultureInfo.InvariantCulture) == 1));
            }

            return new(rows, total, page, pages);
        }
        catch (DbException)
        {
            return new(rows, 0, 0, 0);
        }
    }

    public async Task<CpTemplateDetail?> TemplateAsync(long id, int isFrontend, CancellationToken cancellationToken = default)
    {
        if (id <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            string name, caption, structure, values;
            bool current;
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ErpDb.Positional(
                    "SELECT IFNULL(`name`,''), IFNULL(`caption`,''), IFNULL(`current`,0), IFNULL(`data_structure`,''), IFNULL(`data_value`,'') FROM `templates` WHERE `id` = ? LIMIT 1");
                ErpDb.AddParameters(c, id);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                name = r.GetString(0);
                caption = r.GetString(1);
                current = Convert.ToInt32(r.GetValue(2), CultureInfo.InvariantCulture) == 1;
                structure = r.GetString(3);
                values = r.GetString(4);
            }

            var fields = await FieldsAsync(connection, structure, values, isFrontend, multilang: false, cancellationToken).ConfigureAwait(false);
            return new CpTemplateDetail(id, name, caption, current, isFrontend, fields);
        }
        catch (DbException)
        {
            return null;
        }
    }

    public async Task<CpPagedList<CpPluginRow>> PluginsAsync(int isFrontend, int page, CancellationToken cancellationToken = default)
    {
        var rows = new List<CpPluginRow>();
        if (!_connections.IsConfigured)
        {
            return new(rows, 0, 0, 0);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var total = (int)await ErpDb.LongAsync(connection, null,
                ErpDb.Positional("SELECT COUNT(*) FROM `plugins` WHERE `is_frontend` = ?"), cancellationToken, isFrontend).ConfigureAwait(false);
            var pages = Pages(total);
            page = Math.Clamp(page, 0, Math.Max(0, pages - 1));

            var raw = new List<(long Id, string Caption, int Order, bool Activated, bool Lock)>();
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ErpDb.Positional(
                    "SELECT `id`, IFNULL(`caption`,''), IFNULL(`order`,0), IFNULL(`activated`,0), IFNULL(`control_lock`,0) " +
                    "FROM `plugins` WHERE `is_frontend` = ? ORDER BY `id` LIMIT ? OFFSET ?");
                ErpDb.AddParameters(c, isFrontend, PageLimit, page * PageLimit);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    raw.Add((
                        Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                        r.GetString(1),
                        Convert.ToInt32(r.GetValue(2), CultureInfo.InvariantCulture),
                        Convert.ToInt32(r.GetValue(3), CultureInfo.InvariantCulture) == 1,
                        Convert.ToInt32(r.GetValue(4), CultureInfo.InvariantCulture) == 1));
                }
            }

            foreach (var p in raw)
            {
                rows.Add(new CpPluginRow(p.Id, await TranslateAsync(connection, p.Caption, cancellationToken).ConfigureAwait(false), p.Order, p.Activated, p.Lock));
            }

            return new(rows, total, page, pages);
        }
        catch (DbException)
        {
            return new(rows, 0, 0, 0);
        }
    }

    public async Task<CpPluginDetail?> PluginAsync(long id, int isFrontend, CancellationToken cancellationToken = default)
    {
        if (id <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            string captionKey, descriptionKey, structure, values;
            int order;
            bool activated, lockFlag;
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ErpDb.Positional(
                    "SELECT IFNULL(`caption`,''), IFNULL(`description`,''), IFNULL(`order`,0), IFNULL(`activated`,0), IFNULL(`control_lock`,0), IFNULL(`data_structure`,''), IFNULL(`data_value`,'') " +
                    "FROM `plugins` WHERE `id` = ? LIMIT 1");
                ErpDb.AddParameters(c, id);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                captionKey = r.GetString(0);
                descriptionKey = r.GetString(1);
                order = Convert.ToInt32(r.GetValue(2), CultureInfo.InvariantCulture);
                activated = Convert.ToInt32(r.GetValue(3), CultureInfo.InvariantCulture) == 1;
                lockFlag = Convert.ToInt32(r.GetValue(4), CultureInfo.InvariantCulture) == 1;
                structure = r.GetString(5);
                values = r.GetString(6);
            }

            var fields = await FieldsAsync(connection, structure, values, isFrontend, multilang: true, cancellationToken).ConfigureAwait(false);
            return new CpPluginDetail(
                id,
                await TranslateAsync(connection, captionKey, cancellationToken).ConfigureAwait(false),
                captionKey,
                await TranslateAsync(connection, descriptionKey, cancellationToken).ConfigureAwait(false),
                descriptionKey,
                order,
                activated,
                lockFlag,
                isFrontend,
                fields);
        }
        catch (DbException)
        {
            return null;
        }
    }

    public async Task<CpPagedList<CpModuleRow>> ModulesAsync(int isFrontend, int page, CancellationToken cancellationToken = default)
    {
        var rows = new List<CpModuleRow>();
        if (!_connections.IsConfigured)
        {
            return new(rows, 0, 0, 0);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var total = (int)await ErpDb.LongAsync(connection, null,
                ErpDb.Positional("SELECT COUNT(*) FROM `modules` WHERE `is_frontend` = ? AND `is_prototype` = 0 AND `control_available` = 1"), cancellationToken, isFrontend).ConfigureAwait(false);
            var pages = Pages(total);
            page = Math.Clamp(page, 0, Math.Max(0, pages - 1));

            var raw = new List<(long Id, string Caption, string Proto, string Type, string Position, bool Activated)>();
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ErpDb.Positional(
                    "SELECT `id`, IFNULL(`caption`,''), IFNULL(`prototype_name`,''), IFNULL(`content_type`,''), IFNULL(`position`,''), IFNULL(`activated`,0) " +
                    "FROM `modules` WHERE `is_frontend` = ? AND `is_prototype` = 0 AND `control_available` = 1 ORDER BY `id` LIMIT ? OFFSET ?");
                ErpDb.AddParameters(c, isFrontend, PageLimit, page * PageLimit);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    raw.Add((
                        Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                        r.GetString(1),
                        r.GetString(2),
                        r.GetString(3),
                        r.GetString(4),
                        Convert.ToInt32(r.GetValue(5), CultureInfo.InvariantCulture) == 1));
                }
            }

            foreach (var m in raw)
            {
                rows.Add(new CpModuleRow(
                    m.Id,
                    await TranslateAsync(connection, m.Caption, cancellationToken).ConfigureAwait(false),
                    await TranslateAsync(connection, m.Proto, cancellationToken).ConfigureAwait(false),
                    m.Type,
                    m.Position,
                    m.Activated));
            }

            return new(rows, total, page, pages);
        }
        catch (DbException)
        {
            return new(rows, 0, 0, 0);
        }
    }

    public async Task<IReadOnlyList<CpModulePrototype>> ModulePrototypesAsync(int isFrontend, CancellationToken cancellationToken = default)
    {
        var list = new List<CpModulePrototype>();
        if (!_connections.IsConfigured)
        {
            return list;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var raw = new List<(long Id, string Name)>();
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ErpDb.Positional(
                    "SELECT `id`, IFNULL(`prototype_name`,'') FROM `modules` WHERE `is_prototype` = 1 AND `is_frontend` = ? AND `control_available` = 1 ORDER BY `id`");
                ErpDb.AddParameters(c, isFrontend);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    raw.Add((Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture), r.GetString(1)));
                }
            }

            foreach (var p in raw)
            {
                list.Add(new CpModulePrototype(p.Id, await TranslateAsync(connection, p.Name, cancellationToken).ConfigureAwait(false)));
            }

            return list;
        }
        catch (DbException)
        {
            return list;
        }
    }

    public async Task<CpModuleEditor?> ModuleEditorAsync(long moduleId, long prototypeId, int isFrontend, CancellationToken cancellationToken = default)
    {
        if ((moduleId <= 0 && prototypeId <= 0) || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var mode = moduleId > 0 ? "edit" : "create";
            long id = 0;
            var protoKey = string.Empty;
            var captionKey = string.Empty;
            var contentType = string.Empty;
            var content = string.Empty;
            var position = string.Empty;
            var activated = true;
            var showCaption = false;
            var order = 0;
            var forAll = false;
            var values = "[]";

            if (mode == "edit")
            {
                await using var c = connection.CreateCommand();
                c.CommandText = ErpDb.Positional(
                    "SELECT `id`, IFNULL(`prototype_id`,0), IFNULL(`prototype_name`,''), IFNULL(`caption`,''), IFNULL(`content_type`,''), IFNULL(`content`,''), " +
                    "IFNULL(`position`,''), IFNULL(`activated`,0), IFNULL(`show_caption`,0), IFNULL(`order`,0), IFNULL(`for_all`,0), IFNULL(`data`,'[]') " +
                    "FROM `modules` WHERE `id` = ? LIMIT 1");
                ErpDb.AddParameters(c, moduleId);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                id = Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture);
                prototypeId = Convert.ToInt64(r.GetValue(1), CultureInfo.InvariantCulture);
                protoKey = r.GetString(2);
                captionKey = r.GetString(3);
                contentType = r.GetString(4);
                content = r.GetString(5);
                position = r.GetString(6);
                activated = Convert.ToInt32(r.GetValue(7), CultureInfo.InvariantCulture) == 1;
                showCaption = Convert.ToInt32(r.GetValue(8), CultureInfo.InvariantCulture) == 1;
                order = Convert.ToInt32(r.GetValue(9), CultureInfo.InvariantCulture);
                forAll = Convert.ToInt32(r.GetValue(10), CultureInfo.InvariantCulture) == 1;
                values = r.GetString(11);
            }

            var structure = "[]";
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ErpDb.Positional(
                    "SELECT IFNULL(`prototype_name`,''), IFNULL(`content_type`,''), IFNULL(`content`,''), IFNULL(`data`,'[]') FROM `modules` WHERE `id` = ? LIMIT 1");
                ErpDb.AddParameters(c, prototypeId);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (mode == "create")
                    {
                        protoKey = r.GetString(0);
                        contentType = r.GetString(1);
                        content = r.GetString(2);
                    }

                    structure = r.GetString(3);
                }
                else if (mode == "create")
                {
                    return null;
                }
            }

            var contentKey = string.Empty;
            if (contentType == "text")
            {
                contentKey = content;
                content = await TranslateAsync(connection, content, cancellationToken).ConfigureAwait(false);
            }

            var fields = await FieldsAsync(connection, structure, values, isFrontend, multilang: false, cancellationToken).ConfigureAwait(false);

            var groupsAllowed = new List<long>();
            if (id > 0)
            {
                await using var c = connection.CreateCommand();
                c.CommandText = ErpDb.Positional("SELECT `group_id` FROM `modules_access` WHERE `module_id` = ?");
                ErpDb.AddParameters(c, id);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    groupsAllowed.Add(Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture));
                }
            }

            var contentIds = new List<long>();
            var tree = new List<(long Id, long Parent, string Caption)>();
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ErpDb.Positional(
                    "SELECT `id`, IFNULL(`parent`,0), IFNULL(`value`,''), IFNULL(`modules_array`,'[]') FROM `content` WHERE `is_frontend` = ? ORDER BY `parent`, `order`, `id`");
                ErpDb.AddParameters(c, isFrontend);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var cid = Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture);
                    tree.Add((cid, Convert.ToInt64(r.GetValue(1), CultureInfo.InvariantCulture), r.GetString(2)));
                    if (id > 0 && ModuleIds(r.GetString(3)).Contains(id))
                    {
                        contentIds.Add(cid);
                    }
                }
            }

            var nodes = new List<object>();
            foreach (var n in tree)
            {
                nodes.Add(new { id = n.Id, parent = n.Parent, value = await TranslateAsync(connection, n.Caption, cancellationToken).ConfigureAwait(false) });
            }

            var groups = new List<CpModuleGroup>();
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = "SELECT `id`, IFNULL(`value`,''), IFNULL(`level`,1) FROM `groups` ORDER BY `level`, `id`";
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    groups.Add(new CpModuleGroup(
                        Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                        r.GetString(1),
                        Convert.ToInt32(r.GetValue(2), CultureInfo.InvariantCulture)));
                }
            }

            var templates = new List<CpTemplatePositions>();
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ErpDb.Positional("SELECT `id`, IFNULL(`caption`,''), IFNULL(`positions`,'[]') FROM `templates` WHERE `is_frontend` = ? ORDER BY `id`");
                ErpDb.AddParameters(c, isFrontend);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    templates.Add(new CpTemplatePositions(
                        Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                        r.GetString(1),
                        ModulePositions(r.GetString(2))));
                }
            }

            return new CpModuleEditor(
                mode,
                id,
                prototypeId,
                await TranslateAsync(connection, protoKey, cancellationToken).ConfigureAwait(false),
                protoKey,
                mode == "edit" ? await TranslateAsync(connection, captionKey, cancellationToken).ConfigureAwait(false) : string.Empty,
                captionKey,
                contentType,
                content,
                contentKey,
                position,
                activated,
                showCaption,
                order,
                forAll,
                isFrontend,
                fields,
                contentIds,
                groupsAllowed,
                JsonSerializer.Serialize(nodes),
                groups,
                templates);
        }
        catch (DbException)
        {
            return null;
        }
    }

    /// <summary>PHP <c>content.modules_array</c> JSON int list.</summary>
    public static HashSet<long> ModuleIds(string json)
    {
        var set = new HashSet<long>();
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return set;
            }

            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out var n))
                {
                    set.Add(n);
                }
                else if (e.ValueKind == JsonValueKind.String && long.TryParse(e.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s))
                {
                    set.Add(s);
                }
            }
        }
        catch (JsonException)
        {
        }

        return set;
    }

    /// <summary>PHP <c>templates.positions</c> entries with <c>type == "module"</c> → (name, caption).</summary>
    public static IReadOnlyList<CpWidgetOption> ModulePositions(string json)
    {
        var list = new List<CpWidgetOption>();
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return list;
            }

            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var type = e.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
                if (type is not null && type != "module")
                {
                    continue;
                }

                var name = e.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? string.Empty : string.Empty;
                if (name.Length == 0)
                {
                    continue;
                }

                var caption = e.TryGetProperty("caption", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? name : name;
                list.Add(new CpWidgetOption(name, caption));
            }
        }
        catch (JsonException)
        {
        }

        return list;
    }

    public static int Pages(int total)
        => total <= 0 ? 0 : (total + PageLimit - 1) / PageLimit;

    /// <summary>PHP <c>data_structure</c> loop: options_way direct|sql, <c>&lt;is_frontend&gt;</c> substitution, <c>version</c> +1, text/textarea multilang keys.</summary>
    private static async Task<IReadOnlyList<CpWidgetField>> FieldsAsync(
        DbConnection connection, string structureJson, string valuesJson, int isFrontend, bool multilang, CancellationToken cancellationToken)
    {
        var fields = new List<CpWidgetField>();
        if (string.IsNullOrWhiteSpace(structureJson) || structureJson.Trim() == "[]")
        {
            return fields;
        }

        Dictionary<string, string> values = new(StringComparer.Ordinal);
        try
        {
            using var vdoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(valuesJson) ? "{}" : valuesJson);
            if (vdoc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in vdoc.RootElement.EnumerateObject())
                {
                    values[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? string.Empty : p.Value.ToString();
                }
            }
        }
        catch (JsonException)
        {
        }

        try
        {
            using var doc = JsonDocument.Parse(structureJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return fields;
            }

            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = Str(e, "name");
                if (name.Length == 0)
                {
                    continue;
                }

                var type = Str(e, "type");
                if (type.Length == 0)
                {
                    type = "text";
                }

                var options = new List<CpWidgetOption>();
                var way = Str(e, "options_way");
                if (way.Length > 0 && e.TryGetProperty("options", out var opts))
                {
                    if (way == "sql" && opts.ValueKind == JsonValueKind.String)
                    {
                        var sql = (opts.GetString() ?? string.Empty).Replace("<is_frontend>", isFrontend.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
                        options = await OptionsFromSqlAsync(connection, sql, cancellationToken).ConfigureAwait(false);
                    }
                    else if (way == "direct")
                    {
                        JsonElement arr = opts;
                        JsonDocument? inner = null;
                        if (opts.ValueKind == JsonValueKind.String)
                        {
                            try
                            {
                                inner = JsonDocument.Parse(opts.GetString() ?? "[]");
                                arr = inner.RootElement;
                            }
                            catch (JsonException)
                            {
                                arr = default;
                            }
                        }

                        if (arr.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var o in arr.EnumerateArray())
                            {
                                options.Add(new CpWidgetOption(Str(o, "value"), await TranslateAsync(connection, Str(o, "caption"), cancellationToken).ConfigureAwait(false)));
                            }
                        }

                        inner?.Dispose();
                    }
                }

                values.TryGetValue(name, out var value);
                value ??= string.Empty;
                if (name == "version" && !multilang)
                {
                    value = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? (v + 1).ToString(CultureInfo.InvariantCulture) : "1";
                }

                var langStrId = string.Empty;
                var isMultilang = false;
                if (multilang && type is "text" or "textarea")
                {
                    isMultilang = true;
                    if (name == "403_content" && values.TryGetValue("403_content_type", out var t403) && t403 == "php")
                    {
                        isMultilang = false;
                    }

                    if (name == "404_content" && values.TryGetValue("404_content_type", out var t404) && t404 == "php")
                    {
                        isMultilang = false;
                    }

                    if (isMultilang)
                    {
                        langStrId = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var k) ? k.ToString(CultureInfo.InvariantCulture) : "0";
                        value = await TranslateAsync(connection, value, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        langStrId = "0";
                    }
                }

                fields.Add(new CpWidgetField(
                    name,
                    await TranslateAsync(connection, Str(e, "caption"), cancellationToken).ConfigureAwait(false),
                    type,
                    value,
                    langStrId,
                    isMultilang,
                    options));
            }
        }
        catch (JsonException)
        {
        }

        return fields;
    }

    /// <summary>Structure SQL is operator config the PHP page runs verbatim; only SELECTs are honoured.</summary>
    private static async Task<List<CpWidgetOption>> OptionsFromSqlAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        var list = new List<CpWidgetOption>();
        if (!sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return list;
        }

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql.TrimEnd().TrimEnd(';');
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var v = Ordinal(reader, "value");
            var c = Ordinal(reader, "caption");
            var raw = new List<(string Value, string Caption)>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var value = v >= 0 && !reader.IsDBNull(v) ? reader.GetValue(v).ToString() ?? string.Empty : string.Empty;
                var caption = c >= 0 && !reader.IsDBNull(c) ? reader.GetValue(c).ToString() ?? value : value;
                raw.Add((value, caption));
            }

            await reader.CloseAsync().ConfigureAwait(false);
            foreach (var o in raw)
            {
                list.Add(new CpWidgetOption(o.Value, await TranslateAsync(connection, o.Caption, cancellationToken).ConfigureAwait(false)));
            }
        }
        catch (DbException)
        {
        }

        return list;
    }

    /// <summary>PHP <c>translate_str_by_id</c>: numeric keys resolve through <c>lang_text_strings_translation</c> (en); other text is returned as-is.</summary>
    public static async Task<string> TranslateAsync(DbConnection connection, string key, CancellationToken cancellationToken)
    {
        key = (key ?? string.Empty).Trim();
        if (key.Length == 0 || !key.All(char.IsDigit))
        {
            return key;
        }

        try
        {
            var text = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = 'en' LIMIT 1"),
                cancellationToken,
                key).ConfigureAwait(false);
            return text ?? key;
        }
        catch (DbException)
        {
            return key;
        }
    }

    private static string Str(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v)
            ? (v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : v.ToString())
            : string.Empty;

    private static int Ordinal(DbDataReader reader, string name)
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
}
