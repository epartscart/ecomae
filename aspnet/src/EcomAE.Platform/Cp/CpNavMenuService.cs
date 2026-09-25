using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;
using Microsoft.Extensions.Caching.Memory;

namespace EcomAE.Platform.Cp;

/// <summary>Raw PHP menu rows (<c>control_groups</c> + <c>control_items</c>) from the DB or the repo snapshot.</summary>
public sealed record CpNavRows(
    IReadOnlyList<CpNavRawGroup> Groups,
    IReadOnlyList<CpNavRawItem> Items,
    string Source);

/// <summary>Builds the PHP-equivalent CP top menu for the current host + admin session.</summary>
public interface ICpNavMenuService
{
    Task<IReadOnlyList<CpNavGroup>> BuildAsync(string? host, LegacySessionContext session, CancellationToken cancellationToken = default);
}

public sealed class CpNavMenuService : ICpNavMenuService
{
    private const string RowsCacheKey = "epc_cp_menu_rows:v1";
    private static readonly TimeSpan RowsTtl = TimeSpan.FromMinutes(5);
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_-]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IMemoryCache _cache;
    private readonly IWebHostEnvironment _env;

    public CpNavMenuService(IErpWriteConnectionFactory connections, IMemoryCache cache, IWebHostEnvironment env)
    {
        _connections = connections;
        _cache = cache;
        _env = env;
    }

    public async Task<IReadOnlyList<CpNavGroup>> BuildAsync(string? host, LegacySessionContext session, CancellationToken cancellationToken = default)
    {
        var isSuperHost = PlatformHostPolicy.IsSuperCpHost(host);
        var isAdmin = session.Kind == LegacySessionKind.Admin;

        if (!_connections.IsConfigured)
        {
            var snapshot = LoadSnapshotRows(host);
            if (snapshot is null)
            {
                return Array.Empty<CpNavGroup>();
            }

            var offlinePolicy = isSuperHost && isAdmin ? CpNavPolicy.SuperOperator() : CpNavPolicy.Tenant();
            return CpNavTree.Build(snapshot.Groups, snapshot.Items, offlinePolicy, static _ => null);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var rows = await LoadRowsAsync(connection, cancellationToken).ConfigureAwait(false)
                       ?? LoadSnapshotRows(host);
            if (rows is null)
            {
                return Array.Empty<CpNavGroup>();
            }

            var settings = await LoadSiteMenuSettingsAsync(connection, host, cancellationToken).ConfigureAwait(false);
            var disabled = isSuperHost
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : await LoadDisabledFeaturesAsync(connection, SiteKey(host), cancellationToken).ConfigureAwait(false);

            // PHP has no guest CP (it redirects to login); guest browse keeps the navigation aid and lets
            // each page enforce auth, so only signed-in admins go through content_access.
            Func<string, bool> acl = (isSuperHost && isAdmin) || !isAdmin
                ? static _ => true
                : await BuildAclAsync(connection, rows.Items, session, cancellationToken).ConfigureAwait(false);

            var policy = new CpNavPolicy(
                IsSuperHost: isSuperHost,
                IsSuperAdmin: isSuperHost && isAdmin,
                EnabledPacks: isSuperHost ? CpNavTree.AllPackKeys : settings.EnabledPacks,
                HiddenGroups: settings.HiddenGroups,
                HiddenItems: settings.HiddenItems,
                DisabledFeatures: disabled,
                AclAllows: acl);

            var translate = await BuildTranslatorAsync(connection, rows, cancellationToken).ConfigureAwait(false);
            return CpNavTree.Build(rows.Groups, rows.Items, policy, translate);
        }
        catch (DbException)
        {
            var snapshot = LoadSnapshotRows(host);
            if (snapshot is null)
            {
                return Array.Empty<CpNavGroup>();
            }

            var policy = isSuperHost && isAdmin ? CpNavPolicy.SuperOperator() : CpNavPolicy.Tenant();
            return CpNavTree.Build(snapshot.Groups, snapshot.Items, policy, static _ => null);
        }
    }

    /// <summary>PHP <c>epc_integrations_site_key()</c> without the registry pass: host → <c>www.</c>-less slug.</summary>
    public static string SiteKey(string? host)
    {
        var h = (host ?? string.Empty).Trim().ToLowerInvariant();
        if (h.StartsWith("www.", StringComparison.Ordinal))
        {
            h = h[4..];
        }

        return SiteKeySafe.Replace(h.Replace('.', '-'), string.Empty);
    }

    private async Task<CpNavRows?> LoadRowsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(RowsCacheKey, out CpNavRows? cached) && cached is not null)
        {
            return cached;
        }

        var groups = new List<CpNavRawGroup>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT `id`, IFNULL(`caption`,''), IFNULL(`order`,0) FROM `control_groups` ORDER BY `order` ASC, `id` ASC";
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                groups.Add(new CpNavRawGroup(
                    Convert.ToInt32(r.GetValue(0), CultureInfo.InvariantCulture),
                    r.GetString(1).Trim(),
                    Convert.ToInt32(r.GetValue(2), CultureInfo.InvariantCulture)));
            }
        }

        var items = new List<CpNavRawItem>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT `id`, IFNULL(`items_group`,0), IFNULL(`caption`,''), IFNULL(`url`,''), IFNULL(`order`,0), IFNULL(`fontawesome_class`,''), IFNULL(`show_anyway`,0) FROM `control_items` ORDER BY `order` ASC, `id` ASC";
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                items.Add(new CpNavRawItem(
                    Convert.ToInt32(r.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToInt32(r.GetValue(1), CultureInfo.InvariantCulture),
                    r.GetString(2).Trim(),
                    r.GetString(3).Trim(),
                    Convert.ToInt32(r.GetValue(4), CultureInfo.InvariantCulture),
                    r.GetString(5).Trim(),
                    Convert.ToInt32(r.GetValue(6), CultureInfo.InvariantCulture) == 1));
            }
        }

        if (groups.Count == 0 || items.Count == 0)
        {
            return null;
        }

        var rows = new CpNavRows(groups, items, "db");
        _cache.Set(RowsCacheKey, rows, RowsTtl);
        return rows;
    }

    /// <summary>
    /// PHP perf-cache snapshot (<c>content/files/epc_cache/epc_cp_menu_rows_v1_&lt;site&gt;.json</c>) — used when the
    /// tenant DB is unreachable so the chrome still renders the PHP menu rather than an invented one.
    /// </summary>
    public CpNavRows? LoadSnapshotRows(string? host)
    {
        var root = PhpLegacyAssetBridge.FindRepoRoot(_env);
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        var dir = Path.Combine(root, "content", "files", "epc_cache");
        if (!Directory.Exists(dir))
        {
            return null;
        }

        var preferred = new List<string>();
        var bare = (host ?? string.Empty).Trim().ToLowerInvariant();
        if (bare.StartsWith("www.", StringComparison.Ordinal))
        {
            bare = bare[4..];
        }

        var siteName = bare.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        if (siteName.Length > 0)
        {
            preferred.Add(Path.Combine(dir, "epc_cp_menu_rows_v1_" + siteName + ".json"));
        }

        preferred.Add(Path.Combine(dir, "epc_cp_menu_rows_v1_ecomae.json"));
        preferred.AddRange(Directory.EnumerateFiles(dir, "epc_cp_menu_rows_v1_*.json").OrderBy(p => p, StringComparer.Ordinal));

        foreach (var path in preferred.Distinct(StringComparer.Ordinal))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var parsed = ParseSnapshot(File.ReadAllText(path), Path.GetFileName(path));
            if (parsed is not null)
            {
                return parsed;
            }
        }

        return null;
    }

    public static CpNavRows? ParseSnapshot(string json, string source)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        var v = root?["v"];
        if (v?["groups"] is not JsonArray groupsArr || v["items"] is not JsonArray itemsArr)
        {
            return null;
        }

        var groups = new List<CpNavRawGroup>();
        foreach (var g in groupsArr)
        {
            if (g is null)
            {
                continue;
            }

            groups.Add(new CpNavRawGroup(
                IntOf(g["id"]),
                (g["caption"]?.GetValue<string>() ?? string.Empty).Trim(),
                IntOf(g["order"])));
        }

        var items = new List<CpNavRawItem>();
        foreach (var i in itemsArr)
        {
            if (i is null)
            {
                continue;
            }

            items.Add(new CpNavRawItem(
                IntOf(i["id"]),
                IntOf(i["items_group"]),
                (i["caption"]?.GetValue<string>() ?? string.Empty).Trim(),
                (i["url"]?.GetValue<string>() ?? string.Empty).Trim(),
                IntOf(i["order"]),
                (i["fontawesome_class"]?.GetValue<string>() ?? string.Empty).Trim(),
                IntOf(i["show_anyway"]) == 1));
        }

        return groups.Count == 0 || items.Count == 0 ? null : new CpNavRows(groups, items, source);
    }

    private static int IntOf(JsonNode? node)
    {
        if (node is null)
        {
            return 0;
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var i))
            {
                return i;
            }

            if (value.TryGetValue<long>(out var l))
            {
                return (int)l;
            }

            if (value.TryGetValue<string>(out var s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p))
            {
                return p;
            }
        }

        return 0;
    }

    private sealed record SiteMenuSettings(
        IReadOnlyCollection<string> EnabledPacks,
        IReadOnlySet<int> HiddenGroups,
        IReadOnlySet<int> HiddenItems);

    /// <summary>PHP <c>epc_portal_load_site_settings()</c> → <c>enabled_packs</c> + <c>cp_menu.hidden_groups/hidden_items</c>.</summary>
    private static async Task<SiteMenuSettings> LoadSiteMenuSettingsAsync(DbConnection connection, string? host, CancellationToken cancellationToken)
    {
        var packs = new List<string>(CpNavPolicy.TenantDefaultPacks);
        var hiddenGroups = new HashSet<int>();
        var hiddenItems = new HashSet<int>();

        var h = (host ?? string.Empty).Trim().ToLowerInvariant();
        var bare = h.StartsWith("www.", StringComparison.Ordinal) ? h[4..] : h;
        var www = bare.Length > 0 ? "www." + bare : string.Empty;

        try
        {
            string? packsJson = null;
            string? menuJson = null;
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(
                    "SELECT IFNULL(`enabled_packs_json`,''), IFNULL(`cp_menu_json`,'') FROM `epc_portal_site_settings` WHERE `host` = ? OR `host` = ? ORDER BY `host` = ? DESC LIMIT 1");
                ErpDb.AddParameters(cmd, www, bare, www);
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    packsJson = r.GetString(0);
                    menuJson = r.GetString(1);
                }
            }

            if (!string.IsNullOrWhiteSpace(packsJson))
            {
                try
                {
                    if (JsonNode.Parse(packsJson) is JsonArray arr)
                    {
                        var parsed = arr.Select(n => (n?.GetValue<string>() ?? string.Empty).Trim().ToLowerInvariant())
                            .Where(s => s.Length > 0)
                            .Distinct(StringComparer.Ordinal)
                            .ToList();
                        if (parsed.Count > 0)
                        {
                            packs = parsed;
                        }
                    }
                }
                catch (JsonException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }

            if (!string.IsNullOrWhiteSpace(menuJson))
            {
                try
                {
                    var menu = JsonNode.Parse(menuJson);
                    if (menu?["hidden_groups"] is JsonArray hg)
                    {
                        foreach (var n in hg)
                        {
                            var id = IntOf(n);
                            if (id > 0)
                            {
                                hiddenGroups.Add(id);
                            }
                        }
                    }

                    if (menu?["hidden_items"] is JsonArray hi)
                    {
                        foreach (var n in hi)
                        {
                            var id = IntOf(n);
                            if (id > 0)
                            {
                                hiddenItems.Add(id);
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                }
            }
        }
        catch (DbException)
        {
            // Site-settings table missing: PHP falls back to defaults (core pack, no hides).
        }

        return new SiteMenuSettings(packs, hiddenGroups, hiddenItems);
    }

    /// <summary>PHP <c>epc_integrations_features_for_site()</c>: every catalog feature defaults on; DB rows with enabled=0 disable.</summary>
    private static async Task<HashSet<string>> LoadDisabledFeaturesAsync(DbConnection connection, string siteKey, CancellationToken cancellationToken)
    {
        var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (siteKey.Length == 0)
        {
            return disabled;
        }

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ErpDb.Positional("SELECT `feature_key`, IFNULL(`enabled`,1) FROM `epc_tenant_feature_flags` WHERE `site_key` = ?");
            ErpDb.AddParameters(cmd, siteKey);
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (Convert.ToInt32(r.GetValue(1), CultureInfo.InvariantCulture) == 0)
                {
                    disabled.Add(r.GetString(0).Trim());
                }
            }
        }
        catch (DbException)
        {
        }

        return disabled;
    }

    /// <summary>
    /// PHP <c>is_anable()</c> for every menu URL in one pass: <c>content.url</c> → <c>content_access.group_id</c>
    /// (expanded down the <c>groups.parent</c> tree) ∩ the admin's groups. Content rows with no explicit
    /// groups are open to any signed-in backend user; URLs with no content row are denied.
    /// </summary>
    private static async Task<Func<string, bool>> BuildAclAsync(
        DbConnection connection,
        IReadOnlyList<CpNavRawItem> items,
        LegacySessionContext session,
        CancellationToken cancellationToken)
    {
        var userGroups = session.Groups.Where(g => g > 0).ToHashSet();
        if (session.Kind != LegacySessionKind.Admin || userGroups.Count == 0)
        {
            return static _ => false;
        }

        var urls = items
            .Select(i => CpNavTree.ContentUrl(CpNavTree.ResolveUrl(i.Url)))
            .Where(u => u.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (urls.Count == 0)
        {
            return static _ => false;
        }

        var contentIdByUrl = new Dictionary<string, int>(StringComparer.Ordinal);
        var groupsByContent = new Dictionary<int, List<int>>();
        var parentOf = new Dictionary<int, int>();

        try
        {
            await using (var cmd = connection.CreateCommand())
            {
                var marks = string.Join(",", Enumerable.Repeat("?", urls.Count));
                cmd.CommandText = ErpDb.Positional("SELECT `id`, `url` FROM `content` WHERE `url` IN (" + marks + ")");
                ErpDb.AddParameters(cmd, urls.Cast<object?>().ToArray());
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var id = Convert.ToInt32(r.GetValue(0), CultureInfo.InvariantCulture);
                    var url = r.GetString(1);
                    contentIdByUrl.TryAdd(url, id);
                }
            }

            if (contentIdByUrl.Count > 0)
            {
                await using var cmd = connection.CreateCommand();
                var ids = string.Join(",", contentIdByUrl.Values.Distinct().Select(i => i.ToString(CultureInfo.InvariantCulture)));
                cmd.CommandText = "SELECT `content_id`, `group_id` FROM `content_access` WHERE `content_id` IN (" + ids + ")";
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var cid = Convert.ToInt32(r.GetValue(0), CultureInfo.InvariantCulture);
                    var gid = Convert.ToInt32(r.GetValue(1), CultureInfo.InvariantCulture);
                    if (!groupsByContent.TryGetValue(cid, out var list))
                    {
                        list = new List<int>();
                        groupsByContent[cid] = list;
                    }

                    list.Add(gid);
                }
            }

            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`parent`,0) FROM `groups`";
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    parentOf[Convert.ToInt32(r.GetValue(0), CultureInfo.InvariantCulture)] =
                        Convert.ToInt32(r.GetValue(1), CultureInfo.InvariantCulture);
                }
            }
        }
        catch (DbException)
        {
            // ACL tables unavailable → PHP denies every item that isn't show_anyway.
            return static _ => false;
        }

        // Ancestry of the user's groups: a grant on a parent group covers its children (PHP getAllowedGroups expands downward).
        var userAncestry = new HashSet<int>();
        foreach (var g in userGroups)
        {
            var current = g;
            var guard = 0;
            while (current > 0 && userAncestry.Add(current) && guard++ < 64)
            {
                current = parentOf.TryGetValue(current, out var parent) ? parent : 0;
            }
        }

        var results = new Dictionary<string, bool>(StringComparer.Ordinal);
        return url =>
        {
            if (results.TryGetValue(url, out var cached))
            {
                return cached;
            }

            var allowed = false;
            if (contentIdByUrl.TryGetValue(url, out var cid) && cid > 0)
            {
                if (!groupsByContent.TryGetValue(cid, out var explicitGroups) || explicitGroups.Count == 0)
                {
                    allowed = true;
                }
                else
                {
                    allowed = explicitGroups.Any(userAncestry.Contains);
                }
            }

            results[url] = allowed;
            return allowed;
        };
    }

    /// <summary>
    /// PHP <c>epc_portal_cp_menu_resolve_label</c> lookups in two batched queries:
    /// named keys via <c>lang_text_strings_translation.str_key</c>, numeric captions via <c>lang_text_strings.id</c>.
    /// </summary>
    private static async Task<Func<string, string?>> BuildTranslatorAsync(DbConnection connection, CpNavRows rows, CancellationToken cancellationToken)
    {
        var named = new HashSet<string>(StringComparer.Ordinal);
        var numeric = new HashSet<int>();

        void Collect(string caption)
        {
            caption = (caption ?? string.Empty).Trim();
            if (caption.Length == 0)
            {
                return;
            }

            if (caption.All(char.IsDigit))
            {
                if (int.TryParse(caption, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                {
                    numeric.Add(id);
                }
            }
            else if (char.IsLetter(caption[0]) && caption.All(c => char.IsLetterOrDigit(c) || c == '_'))
            {
                named.Add(caption);
            }
        }

        foreach (var g in rows.Groups)
        {
            Collect(g.Caption);
        }

        foreach (var i in rows.Items)
        {
            Collect(i.Caption);
        }

        foreach (var sub in CpNavTree.GroupSubtitles.Values)
        {
            Collect(sub);
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            if (named.Count > 0)
            {
                var list = named.ToList();
                await using var cmd = connection.CreateCommand();
                var marks = string.Join(",", Enumerable.Repeat("?", list.Count));
                cmd.CommandText = ErpDb.Positional(
                    "SELECT `str_key`, IFNULL(`value`,'') FROM `lang_text_strings_translation` WHERE `lang_code` = 'en' AND `str_key` IN (" + marks + ")");
                ErpDb.AddParameters(cmd, list.Cast<object?>().ToArray());
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var value = r.GetString(1).Trim();
                    if (value.Length > 0)
                    {
                        map.TryAdd(r.GetString(0), value);
                    }
                }
            }

            if (numeric.Count > 0)
            {
                await using var cmd = connection.CreateCommand();
                var ids = string.Join(",", numeric.Select(i => i.ToString(CultureInfo.InvariantCulture)));
                cmd.CommandText =
                    "SELECT s.`id`, IFNULL(t.`value`,'') FROM `lang_text_strings` s INNER JOIN `lang_text_strings_translation` t ON t.`str_key` = s.`str_key` WHERE t.`lang_code` = 'en' AND s.`id` IN (" + ids + ")";
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var value = r.GetString(1).Trim();
                    if (value.Length > 0)
                    {
                        map.TryAdd(Convert.ToInt32(r.GetValue(0), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture), value);
                    }
                }
            }
        }
        catch (DbException)
        {
            // Missing lang tables → PHP falls back to humanised URL/key labels.
        }

        return key => map.TryGetValue((key ?? string.Empty).Trim(), out var v) ? v : null;
    }
}
