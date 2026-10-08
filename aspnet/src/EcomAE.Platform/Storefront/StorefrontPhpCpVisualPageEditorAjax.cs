using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string CpVisualPageEditorPath = "/cp/content/control/portal/ajax_visual_page_editor.php";
    public const string VisualEditorSaveStaysClassic = "Visual page editor save stays Classic (info blocks, brand settings and cache).";

    private static readonly Regex VisualPageKeyUnsafe = new("[^a-z0-9_-]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex VisualSiteKeyUnsafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public sealed record VisualLevel(string Id, string Label, string Icon, string Hint, string Placement, string Mode, string PreviewPath);

    /// <summary>PHP <c>epc_vpe_frontend_levels</c>, in PHP order.</summary>
    public static readonly IReadOnlyList<VisualLevel> VisualLevels =
    [
        new("homepage", "Homepage", "fa-home", "Main storefront landing", "homepage", "layout", "/en/"),
        new("product_list", "Category / list", "fa-th-list", "Product listing banner", "product_list", "layout", "/en/shop"),
        new("footer", "Footer", "fa-window-minimize", "Storefront footer strip", "footer", "layout", "/en/"),
        new("checkout", "Checkout", "fa-shopping-cart", "Cart / checkout sidebar", "checkout", "layout", "/en/shop/cart"),
        new("login", "Login", "fa-sign-in", "Login / register page", "login", "layout", "/en/users/login"),
        new("brand", "Brand global", "fa-paint-brush", "Colours, logo, tagline", string.Empty, "brand_only", "/en/"),
    ];

    private static readonly (string Key, string Value)[] VisualDefaultBrand =
    [
        ("primary", "#2563eb"),
        ("accent", "#0ea5e9"),
        ("background", "#f8fafc"),
        ("logo_url", ""),
        ("tagline", ""),
        ("footer_text", ""),
        ("hero_headline", ""),
        ("hero_subheadline", ""),
    ];

    public sealed record VisualTarget(string SiteKey, string PreviewUrl);

    public static async Task<object> VisualPageEditorAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        IReadOnlyDictionary<string, string> posted,
        IReadOnlyDictionary<string, string> query,
        string? requestHost,
        string? tenantSiteKey,
        CancellationToken cancellationToken)
    {
        var denied = await StaffAsync(
            connection,
            adminSession,
            adminUser,
            new CodedJson(403, new FlagBody(false, AutoPriceAdminRequired)),
            cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        var action = AutoPriceValue(posted, query, "action") ?? string.Empty;
        var siteKey = VisualSiteKey(AutoPriceValue(posted, query, "site_key"));
        var pageKey = VisualPageKey(AutoPriceValue(posted, query, "page_key") ?? "homepage");
        var super = PlatformHostPolicy.IsSuperCpHost(requestHost);
        var targets = super
            ? await VisualTargetsAsync(connection, cancellationToken).ConfigureAwait(false)
            : [];
        var allowed = super
            ? targets.Select(target => target.SiteKey).ToList()
            : [VisualStorefrontSiteKey(requestHost, tenantSiteKey) is { Length: > 0 } own ? own : "platform"];
        if (siteKey.Length == 0 || !allowed.Contains(siteKey, StringComparer.Ordinal))
        {
            return new FlagBody(false, "Invalid site");
        }

        if (action == "load_layout")
        {
            var layout = await VisualLayoutAsync(connection, siteKey, pageKey, cancellationToken).ConfigureAwait(false);
            return new JsonObject
            {
                ["status"] = true,
                ["layout"] = layout,
                ["preview_url"] = VisualPreviewUrl(targets, siteKey, pageKey),
                ["levels"] = VisualLevelsObject(),
            };
        }

        if (action == "save_layout")
        {
            var blocksRaw = posted.TryGetValue("blocks_json", out var raw) ? raw : "[]";
            JsonNode? blocks;
            try
            {
                blocks = JsonNode.Parse(blocksRaw);
            }
            catch (JsonException)
            {
                blocks = null;
            }

            if (blocks is not (JsonArray or JsonObject))
            {
                return new FlagBody(false, "Invalid blocks JSON");
            }

            return new FlagBody(false, VisualEditorSaveStaysClassic);
        }

        return new FlagBody(false, "Unknown action");
    }

    /// <summary>PHP <c>epc_vpe_normalize_site_key</c>.</summary>
    public static string VisualSiteKey(string? raw)
    {
        var key = VisualSiteKeyUnsafe.Replace((raw ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);
        return key == "ecomae" ? "platform" : key;
    }

    /// <summary>PHP <c>epc_vpe_normalize_page_key</c>.</summary>
    public static string VisualPageKey(string? raw)
    {
        var key = VisualPageKeyUnsafe.Replace((raw ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);
        return VisualLevels.Any(level => level.Id == key) ? key : "homepage";
    }

    /// <summary>PHP <c>epc_vpe_storefront_site_key</c> for a tenant Control Panel.</summary>
    public static string VisualStorefrontSiteKey(string? requestHost, string? tenantSiteKey)
    {
        var key = VisualSiteKeyUnsafe.Replace((tenantSiteKey ?? string.Empty).ToLowerInvariant(), string.Empty);
        if (key.Length > 0)
        {
            return key;
        }

        var host = (requestHost ?? string.Empty).Trim().ToLowerInvariant();
        host = host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
        return host switch
        {
            "ecomae.com" => "platform",
            "epartscart.com" => "epartscart",
            _ => string.Empty,
        };
    }

    /// <summary>PHP <c>epc_vpe_resolve_preview_url</c>.</summary>
    public static string VisualPreviewUrl(IReadOnlyList<VisualTarget> targets, string siteKey, string pageKey)
    {
        var path = VisualLevelFor(pageKey).PreviewPath;
        var preview = siteKey switch
        {
            "platform" => "https://www.ecomae.com/en/",
            "epartscart" => "https://www.epartscart.com/en/",
            _ => targets.FirstOrDefault(target => target.SiteKey == siteKey)?.PreviewUrl ?? "https://www.ecomae.com/en/",
        };
        return Uri.TryCreate(preview, UriKind.Absolute, out var uri) && uri.Host.Length > 0
            ? uri.Scheme + "://" + uri.Host + path
            : "https://www.ecomae.com" + path;
    }

    private static VisualLevel VisualLevelFor(string pageKey)
        => VisualLevels.FirstOrDefault(level => level.Id == pageKey) ?? VisualLevels[0];

    private static JsonObject VisualLevelsObject()
    {
        var levels = new JsonObject();
        foreach (var level in VisualLevels)
        {
            levels[level.Id] = new JsonObject
            {
                ["label"] = level.Label,
                ["icon"] = level.Icon,
                ["hint"] = level.Hint,
                ["page_key"] = level.Id,
                ["placement"] = level.Placement,
                ["mode"] = level.Mode,
                ["preview_path"] = level.PreviewPath,
            };
        }

        return levels;
    }

    /// <summary>PHP <c>epc_vpe_target_options</c>: platform, ePartsCart, then registry tenants.</summary>
    private static async Task<List<VisualTarget>> VisualTargetsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var targets = new List<VisualTarget>
        {
            new("platform", "https://www.ecomae.com/en/"),
            new("epartscart", "https://www.epartscart.com/en/"),
        };
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM `epc_portal_tenants` ORDER BY `site_key`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                var key = AutoPriceText(row, "site_key");
                if (key.Length == 0 || key == "epartscart")
                {
                    continue;
                }

                targets.Add(new VisualTarget(key, VisualTenantPreview(row)));
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
        }

        return targets;
    }

    /// <summary>Commerce tenants preview on their own host; demo and ERP-only tenants preview on ecomae.com, as in PHP.</summary>
    private static string VisualTenantPreview(IReadOnlyDictionary<string, object?> row)
    {
        var erpOnly = !PhpEmpty(AutoPriceText(row, "erp_only_shared"))
            || (AutoPriceText(row, "industry_code") == "erp_standalone" && AutoPriceText(row, "hosted_on") == "platform");
        if (!PhpEmpty(AutoPriceText(row, "is_demo")) || erpOnly)
        {
            return string.Empty;
        }

        var host = AutoPriceText(row, "hostname").Trim().ToLowerInvariant();
        host = Regex.Replace(host, "^https?://", string.Empty, RegexOptions.CultureInvariant);
        host = Regex.Replace(host, "/.*$", string.Empty, RegexOptions.CultureInvariant);
        host = host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
        return host.Contains('.', StringComparison.Ordinal) ? "https://www." + host + "/en/" : string.Empty;
    }

    /// <summary>PHP <c>epc_vpe_layout_load</c>, without the schema-ensure (a missing table reads as no saved layout).</summary>
    private static async Task<JsonObject> VisualLayoutAsync(DbConnection connection, string siteKey, string pageKey, CancellationToken cancellationToken)
    {
        Dictionary<string, object?>? row = null;
        string? homeBrandJson = null;
        try
        {
            row = await OneRowAsync(
                connection,
                "SELECT * FROM `epc_page_builder_layouts` WHERE `site_key` = ? AND `page_key` = ? LIMIT 1",
                cancellationToken,
                siteKey,
                pageKey).ConfigureAwait(false);
            if (pageKey != "homepage" || row is null)
            {
                homeBrandJson = await ErpDb.StringAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT `brand_json` FROM `epc_page_builder_layouts` WHERE `site_key` = ? AND `page_key` IN ('homepage', 'brand') ORDER BY FIELD(`page_key`, 'brand', 'homepage') LIMIT 1"),
                    cancellationToken,
                    siteKey).ConfigureAwait(false);
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            row = null;
        }

        var brand = new JsonObject();
        foreach (var (key, value) in VisualDefaultBrand)
        {
            brand[key] = value;
        }

        JsonNode blocks = new JsonArray();
        var published = false;
        if (row is not null)
        {
            if (VisualJson(AutoPriceText(row, "layout_json")) is { } decoded)
            {
                blocks = decoded;
            }

            if (VisualJson(AutoPriceText(row, "brand_json")) is JsonObject saved)
            {
                foreach (var pair in saved)
                {
                    brand[pair.Key] = pair.Value?.DeepClone();
                }
            }

            published = !PhpEmpty(AutoPriceText(row, "is_published"));
        }

        if (VisualJson(homeBrandJson) is JsonObject home)
        {
            foreach (var pair in home)
            {
                if (VisualBlank(brand.TryGetPropertyValue(pair.Key, out var current) ? current : null) && !VisualBlank(pair.Value))
                {
                    brand[pair.Key] = pair.Value!.DeepClone();
                }
            }
        }

        var level = VisualLevelFor(pageKey);
        return new JsonObject
        {
            ["site_key"] = siteKey,
            ["page_key"] = pageKey,
            ["level_id"] = level.Id,
            ["mode"] = level.Mode,
            ["blocks"] = blocks,
            ["brand"] = brand,
            ["is_published"] = published,
            ["updated_at"] = row is null ? 0 : PhpInt(AutoPriceText(row, "updated_at")),
        };
    }

    private static JsonNode? VisualJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            var node = JsonNode.Parse(raw);
            return node is JsonArray or JsonObject ? node : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool VisualBlank(JsonNode? node)
        => node is null || (node is JsonValue value && value.TryGetValue<string>(out var text) && text.Length == 0);
}
