using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live twin of the PHP Industry-settings loader (<c>epc_portal_load_site_settings</c> in
/// content/general_pages/epc_portal_db.php) plus <c>epc_portal_cp_menu_groups_for_settings</c>:
/// the current tenant row behind <c>/cp/control/portal/industry_settings</c>.
/// </summary>
public interface ICpIndustrySettingsService
{
    Task<CpIndustrySettingsView> LoadAsync(string? host, CancellationToken cancellationToken = default);
}

public sealed record CpIndustrySettingsContact(
    string TradeName,
    string FromEmail,
    string AdminEmail,
    string Phone,
    string HeadOfficeAddress,
    string City,
    string Country,
    string CountryCode);

public sealed record CpIndustrySettingsMenuGroup(int Id, string Caption, string Label, int ItemCount);

public sealed record CpIndustrySettingsMenuItem(int Id, int GroupId, string Caption, string Url);

public sealed record CpIndustrySettingsView(
    string Host,
    string IndustryCode,
    string ThemeTemplate,
    string StorefrontLayout,
    string AccessMode,
    string CpDefaultLang,
    string SystemName,
    string HubName,
    string Tagline,
    string DomainPath,
    CpIndustrySettingsContact Contact,
    IReadOnlyList<string> EnabledPacks,
    IReadOnlyList<string> ErpModules,
    IReadOnlyList<int> HiddenGroups,
    IReadOnlyList<int> HiddenItems,
    IReadOnlyList<CpIndustrySettingsMenuGroup> MenuGroups,
    IReadOnlyList<CpIndustrySettingsMenuItem> MenuItems,
    string Source)
{
    public static CpIndustrySettingsView Empty(string host, string source) =>
        new(
            host,
            "auto_parts",
            "classic",
            string.Empty,
            "full",
            "en",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            new CpIndustrySettingsContact(
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, "AE"),
            ["core"],
            PortalSettingsCatalog.ErpModuleDefaults("full"),
            [],
            [],
            [],
            [],
            source);

    /// <summary>PHP counts the visible sidebar groups for the settings summary strip.</summary>
    public int VisibleGroupCount =>
        MenuGroups.Count(g => !HiddenGroups.Contains(g.Id));
}

public sealed class CpIndustrySettingsService : ICpIndustrySettingsService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpIndustrySettingsService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CpIndustrySettingsView> LoadAsync(string? host, CancellationToken cancellationToken = default)
    {
        var hostName = (host ?? string.Empty).Trim().ToLowerInvariant();
        if (!_connections.IsConfigured)
        {
            return CpIndustrySettingsView.Empty(hostName, "unconfigured");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var row = await LoadRowAsync(connection, hostName, cancellationToken).ConfigureAwait(false);
            var groups = await LoadMenuGroupsAsync(connection, cancellationToken).ConfigureAwait(false);
            var items = await LoadMenuItemsAsync(connection, cancellationToken).ConfigureAwait(false);
            var baseline = row ?? CpIndustrySettingsView.Empty(hostName, "defaults");
            return baseline with { MenuGroups = groups, MenuItems = items, Source = row is null ? "defaults" : "db" };
        }
        catch (DbException)
        {
            return CpIndustrySettingsView.Empty(hostName, "db-error");
        }
    }

    private static async Task<CpIndustrySettingsView?> LoadRowAsync(
        DbConnection connection,
        string host,
        CancellationToken cancellationToken)
    {
        var aliases = PlatformHostPolicy.NormalizeHostAliases(host);
        var sql =
            "SELECT `host`, IFNULL(`industry_code`,''), IFNULL(`theme_template`,''), IFNULL(`access_mode`,''), "
            + "IFNULL(`cp_default_lang`,''), IFNULL(`system_name`,''), IFNULL(`hub_name`,''), IFNULL(`tagline`,''), "
            + "IFNULL(`domain_path`,''), IFNULL(`contact_json`,''), IFNULL(`enabled_packs_json`,''), "
            + "IFNULL(`erp_modules_json`,''), IFNULL(`cp_menu_json`,''), IFNULL(`country_code`,'') "
            + "FROM `epc_portal_site_settings` ";
        object?[] parameters;
        if (aliases.Count > 0)
        {
            var placeholders = string.Join(", ", aliases.Select((_, i) => "@p" + i.ToString(CultureInfo.InvariantCulture)));
            sql += "WHERE `host` IN (" + placeholders + ") ORDER BY `id` ASC LIMIT 1";
            parameters = aliases.Cast<object?>().ToArray();
        }
        else
        {
            sql += "ORDER BY `id` ASC LIMIT 1";
            parameters = [];
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        ErpDb.AddParameters(cmd, parameters);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var industry = Slug(reader.GetString(1), "auto_parts");
        var contactJson = reader.GetString(9);
        var contact = ParseContact(contactJson, reader.GetString(13));
        var menu = ParseMenu(reader.GetString(12));
        return new CpIndustrySettingsView(
            reader.GetString(0),
            industry,
            PortalSettingsCatalog.NormalizeThemeTemplate(industry, reader.GetString(2)),
            StorefrontLayout(contactJson),
            AccessMode(reader.GetString(3)),
            Lang(reader.GetString(4)),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            contact,
            ParsePacks(reader.GetString(10)),
            NormalizeErpModules(reader.GetString(11), AccessMode(reader.GetString(3))),
            menu.Groups,
            menu.Items,
            [],
            [],
            "db");
    }

    private static async Task<IReadOnlyList<CpIndustrySettingsMenuGroup>> LoadMenuGroupsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var groups = new List<CpIndustrySettingsMenuGroup>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT g.`id`, IFNULL(g.`caption`,''), (SELECT COUNT(*) FROM `control_items` i WHERE i.`items_group` = g.`id`) "
            + "FROM `control_groups` g ORDER BY g.`order` ASC, g.`id` ASC";
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var caption = reader.GetString(1).Trim();
            groups.Add(new CpIndustrySettingsMenuGroup(
                Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                caption,
                CpNavTree.ResolveLabel(caption, string.Empty, static _ => null),
                Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture)));
        }

        return groups;
    }

    private static async Task<IReadOnlyList<CpIndustrySettingsMenuItem>> LoadMenuItemsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var items = new List<CpIndustrySettingsMenuItem>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT `id`, IFNULL(`items_group`,0), IFNULL(`caption`,''), IFNULL(`url`,'') FROM `control_items` "
            + "ORDER BY `items_group` ASC, `order` ASC, `id` ASC";
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var caption = reader.GetString(2).Trim();
            items.Add(new CpIndustrySettingsMenuItem(
                Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                CpNavTree.ResolveLabel(caption, reader.GetString(3), static _ => null),
                reader.GetString(3).Trim()));
        }

        return items;
    }

    /// <summary>PHP <c>preg_replace('/[^a-z0-9_]/', '', strtolower($v))</c>.</summary>
    public static string Slug(string? raw, string fallback)
    {
        var value = new string((raw ?? string.Empty)
            .ToLowerInvariant()
            .Where(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')
            .ToArray());
        return value.Length == 0 ? fallback : value;
    }

    /// <summary>PHP allows only these four access modes and maps the legacy <c>full_commerce</c>.</summary>
    public static string AccessMode(string? raw)
    {
        var mode = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (mode == "full_commerce")
        {
            mode = "full";
        }

        return mode is "full" or "erp_only" or "consultancy" or "mixed" ? mode : "full";
    }

    /// <summary>PHP <c>preg_replace('/[^a-z\-]/', '', strtolower($lang))</c> with the <c>en</c> fallback.</summary>
    public static string Lang(string? raw)
    {
        var value = new string((raw ?? string.Empty)
            .ToLowerInvariant()
            .Where(c => (c >= 'a' && c <= 'z') || c == '-')
            .ToArray());
        return value.Length == 0 ? "en" : value;
    }

    public static IReadOnlyList<string> ParsePacks(string? json)
    {
        var packs = ParseStringArray(json);
        if (packs.Count == 0)
        {
            return ["core"];
        }

        return packs.Contains("core", StringComparer.Ordinal) ? packs : ["core", .. packs];
    }

    /// <summary>PHP <c>epc_portal_erp_modules_normalize_list()</c>: only registry ids survive.</summary>
    public static IReadOnlyList<string> NormalizeErpModules(string? json, string accessMode)
    {
        var known = PortalSettingsCatalog.ErpModules.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
        var ids = ParseStringArray(json).Where(known.Contains).Distinct(StringComparer.Ordinal).ToList();
        return ids.Count > 0 ? ids : PortalSettingsCatalog.ErpModuleDefaults(accessMode);
    }

    /// <summary>PHP keeps the chosen storefront layout inside <c>contact_json</c>.</summary>
    public static string StorefrontLayout(string? contactJson)
    {
        if (string.IsNullOrWhiteSpace(contactJson))
        {
            return string.Empty;
        }

        try
        {
            if (JsonNode.Parse(contactJson) is JsonObject obj && obj["storefront_layout"] is JsonValue value)
            {
                return Slug(value.ToString(), string.Empty);
            }
        }
        catch (JsonException)
        {
            return string.Empty;
        }

        return string.Empty;
    }

    public static CpIndustrySettingsContact ParseContact(string? json, string? countryCodeColumn)
    {
        var country = (countryCodeColumn ?? string.Empty).Trim().ToUpperInvariant();
        JsonObject? obj = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                obj = JsonNode.Parse(json) as JsonObject;
            }
            catch (JsonException)
            {
                obj = null;
            }
        }

        string Get(string key) => obj?[key] is JsonValue value ? value.ToString() : string.Empty;

        if (country.Length == 0)
        {
            country = Get("country_code").Trim().ToUpperInvariant();
        }

        return new CpIndustrySettingsContact(
            Get("trade_name"),
            Get("from_email"),
            Get("admin_email"),
            Get("contact_phone"),
            Get("head_office_address"),
            Get("city"),
            Get("country"),
            country.Length >= 2 ? country[..2] : "AE");
    }

    private static (IReadOnlyList<int> Groups, IReadOnlyList<int> Items) ParseMenu(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ([], []);
        }

        try
        {
            if (JsonNode.Parse(json) is not JsonObject obj)
            {
                return ([], []);
            }

            return (Ints(obj["hidden_groups"]), Ints(obj["hidden_items"]));
        }
        catch (JsonException)
        {
            return ([], []);
        }
    }

    private static IReadOnlyList<int> Ints(JsonNode? node)
    {
        if (node is not JsonArray array)
        {
            return [];
        }

        var list = new List<int>();
        foreach (var entry in array)
        {
            if (entry is null)
            {
                continue;
            }

            if (int.TryParse(entry.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                list.Add(value);
            }
        }

        return list.Distinct().ToList();
    }

    private static IReadOnlyList<string> ParseStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            if (JsonNode.Parse(json) is not JsonArray array)
            {
                return [];
            }

            return array
                .Where(entry => entry is not null)
                .Select(entry => entry!.ToString().Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
