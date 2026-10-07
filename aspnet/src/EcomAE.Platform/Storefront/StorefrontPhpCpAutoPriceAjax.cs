using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string CpAutoPricePath = "/cp/content/control/portal/ajax_auto_price.php";
    public const string AutoPriceAdminRequired = "Admin login required";
    public const string AutoPriceTenantStaysClassic = "Auto price writes for another tenant database stay Classic.";
    public const string AutoPriceLoginStaysClassic = "Discovery sources with a login or a product-line scope stay Classic.";
    public const string AutoPriceActionStaysClassic = "Auto price action stays Classic: ";

    private static readonly Regex AutoPriceHostSuffix = new(@"\.[a-z0-9.-]+$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex AutoPriceSiteKeyUnsafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly HashSet<string> AutoPriceClassicActions = new(StringComparer.Ordinal)
    {
        "discover_search",
        "list_discovery_sources",
        "test_source_login",
        "fetch_prices",
        "start_job",
        "job_status",
        "crawl_sources",
        "crawl_status",
        "discover_counts",
        "list_discover_queue",
        "warehouse_market_match",
        "match_catalogue_market",
        "bulk_approve",
        "advise_category",
        "list_categories",
        "list_my_imports",
        "dismiss_duplicate",
        "shell_kpi",
        "load_tab_html",
        "product_line_prices",
        "product_lines_tax_tree",
        "warehouse_list",
        "warehouse_compare_selected",
        "country_info",
    };

    public static async Task<object> AutoPriceAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        IReadOnlyDictionary<string, string> posted,
        IReadOnlyDictionary<string, string> query,
        string? requestHost,
        ICpAutoPriceWriteService writes,
        IErpWriteConnectionFactory sources,
        CancellationToken cancellationToken)
    {
        var denied = await StaffAsync(
            connection,
            adminSession,
            adminUser,
            new CodedJson(403, new OkMessage(false, AutoPriceAdminRequired)),
            cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        var host = (requestHost ?? string.Empty).Trim().ToLowerInvariant();
        var siteKey = AutoPriceSiteKey(AutoPriceValue(posted, query, "site_key"), host);
        var action = AutoPriceValue(posted, query, "action") ?? string.Empty;
        var crossTenant = PlatformHostPolicy.IsSuperCpHost(requestHost) && siteKey != "platform";

        switch (action)
        {
            case "add_discovery_source":
            {
                var domain = AutoPricePosted(posted, "domain").Trim();
                if (domain.Length == 0)
                {
                    return new OkMessage(false, "Domain or URL is required");
                }

                if (crossTenant)
                {
                    return new OkMessage(false, AutoPriceTenantStaysClassic);
                }

                if (AutoPriceAddNeedsClassic(posted))
                {
                    return new OkMessage(false, AutoPriceLoginStaysClassic);
                }

                var id = Math.Max(0, PhpInt(AutoPricePosted(posted, "id")));
                if (id > 0)
                {
                    var existing = await AutoPriceSourceAsync(sources, id, siteKey, cancellationToken).ConfigureAwait(false);
                    if (existing is not null && AutoPriceRowNeedsClassic(existing))
                    {
                        return new OkMessage(false, AutoPriceLoginStaysClassic);
                    }
                }

                var written = await writes.AddSourceAsync(
                    new CpAutoPriceSourceAddRequest(
                        id,
                        domain,
                        AutoPricePosted(posted, "label"),
                        siteKey,
                        posted.TryGetValue("enabled", out var enabled) ? !PhpEmpty(enabled) : null,
                        posted.TryGetValue("priority", out var priority) ? PhpInt(priority) : null,
                        requestHost),
                    cancellationToken).ConfigureAwait(false);
                if (!written.Succeeded)
                {
                    return new OkMessage(false, written.Message);
                }

                var row = await AutoPriceSourceAsync(sources, written.Id, siteKey, cancellationToken).ConfigureAwait(false);
                return new JsonObject
                {
                    ["ok"] = true,
                    ["id"] = written.Id,
                    ["message"] = written.Message,
                    ["source"] = row is null ? null : AutoPriceSourceRow(row),
                };
            }

            case "delete_discovery_source":
            {
                var id = Math.Max(0, PhpInt(AutoPriceValue(posted, query, "id")));
                if (id <= 0)
                {
                    return new OkMessage(false, "Source id required");
                }

                if (crossTenant)
                {
                    return new OkMessage(false, AutoPriceTenantStaysClassic);
                }

                var written = await writes.DeleteSourceAsync(id, siteKey, cancellationToken).ConfigureAwait(false);
                if (!written.Succeeded)
                {
                    return new OkMessage(false, written.Message);
                }

                return new JsonObject
                {
                    ["ok"] = true,
                    ["message"] = written.Message,
                    ["id"] = id,
                };
            }

            case "toggle_discovery_source":
            {
                var id = Math.Max(0, PhpInt(AutoPriceValue(posted, query, "id")));
                if (id <= 0)
                {
                    return new OkMessage(false, "Source id required");
                }

                if (crossTenant)
                {
                    return new OkMessage(false, AutoPriceTenantStaysClassic);
                }

                bool? enabled = null;
                var postedEnabled = posted.TryGetValue("enabled", out var postValue);
                var queryEnabled = query.TryGetValue("enabled", out var queryValue);
                if (postedEnabled || queryEnabled)
                {
                    enabled = (postedEnabled && !PhpEmpty(postValue)) || (queryEnabled && !PhpEmpty(queryValue));
                }

                var written = await writes.ToggleSourceAsync(
                    new CpAutoPriceSourceToggleRequest(id, siteKey, enabled),
                    cancellationToken).ConfigureAwait(false);
                if (!written.Succeeded)
                {
                    return new OkMessage(false, written.Message);
                }

                var row = await AutoPriceSourceAsync(sources, id, siteKey, cancellationToken).ConfigureAwait(false);
                return new JsonObject
                {
                    ["ok"] = true,
                    ["message"] = written.Message,
                    ["source"] = row is null ? null : AutoPriceSourceRow(row),
                };
            }

            case "skip_source":
            {
                var id = Math.Max(0, PhpInt(AutoPriceValue(posted, query, "source_id")));
                if (id <= 0)
                {
                    return new OkMessage(false, "source_id required");
                }

                if (crossTenant)
                {
                    return new OkMessage(false, AutoPriceTenantStaysClassic);
                }

                var written = await writes.SkipSourceAsync(
                    new CpAutoPriceSourceSkipRequest(
                        id,
                        siteKey,
                        posted.TryGetValue("hours", out var hours) ? PhpInt(hours) : null),
                    cancellationToken).ConfigureAwait(false);
                return new JsonObject
                {
                    ["ok"] = written.Succeeded,
                    ["message"] = written.Message,
                    ["source_id"] = id,
                };
            }
        }

        if (AutoPriceClassicActions.Contains(action))
        {
            return new OkMessage(false, AutoPriceActionStaysClassic + action);
        }

        return new CodedJson(400, new OkMessage(false, "Unknown action: " + action));
    }

    /// <summary>PHP <c>ajax_auto_price.php</c> site_key resolution (posted key, known hosts, hostname, then platform).</summary>
    public static string AutoPriceSiteKey(string? postedSiteKey, string host)
    {
        var siteKey = AutoPriceSiteKeyUnsafe.Replace((postedSiteKey ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);
        if (siteKey.Length > 0)
        {
            return siteKey;
        }

        foreach (var known in new[] { "electronicae", "epartscart", "stylenlook", "thejewellerytrend", "taxofinca" })
        {
            if (host.Contains(known, StringComparison.Ordinal))
            {
                return known;
            }
        }

        if (host.Length > 0)
        {
            var bare = host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
            bare = AutoPriceHostSuffix.Replace(bare, string.Empty);
            siteKey = AutoPriceSiteKeyUnsafe.Replace(bare.Replace('-', '_'), string.Empty);
        }

        return siteKey.Length > 0 ? siteKey : "platform";
    }

    /// <summary>PHP <c>epc_disc_source_format_row</c>.</summary>
    public static JsonObject AutoPriceSourceRow(IReadOnlyDictionary<string, object?> row)
    {
        var taxonomyId = PhpInt(AutoPriceText(row, "taxonomy_node_id"));
        var slug = AutoPriceText(row, "product_line_slug");
        var authType = AutoPriceText(row, "auth_type", "none").Trim().ToLowerInvariant();
        if (authType is not ("none" or "basic" or "form_login"))
        {
            authType = "none";
        }

        var config = AutoPriceConfig(row);
        var custom = !PhpEmpty(AutoPriceText(row, "created_by_tenant"));
        var skipUntil = PhpInt(AutoPriceConfigText(config, "crawl_skip_until"));
        return new JsonObject
        {
            ["id"] = PhpInt(AutoPriceText(row, "id")),
            ["site_key"] = AutoPriceText(row, "site_key"),
            ["source_type"] = AutoPriceText(row, "source_type", "custom_website"),
            ["domain"] = AutoPriceText(row, "domain"),
            ["label"] = AutoPriceText(row, "label"),
            ["enabled"] = !PhpEmpty(AutoPriceText(row, "enabled")),
            ["priority"] = PhpInt(AutoPriceText(row, "priority", "100")),
            ["created_by_tenant"] = custom,
            ["taxonomy_node_id"] = taxonomyId,
            ["product_line_slug"] = slug,
            ["origin"] = custom ? "custom" : "country_pack",
            ["editable"] = custom,
            ["scoped"] = taxonomyId > 0 || slug.Length > 0,
            ["last_crawl"] = PhpInt(AutoPriceText(row, "last_crawl")),
            ["auth_type"] = authType,
            ["auth_username"] = AutoPriceText(row, "auth_username").Trim(),
            ["login_configured"] = AutoPriceHasLogin(row),
            ["login_url"] = AutoPriceConfigText(config, "login_url").Trim(),
            ["login_form_selector"] = AutoPriceConfigText(config, "login_form_selector").Trim(),
            ["last_test_at"] = PhpInt(AutoPriceConfigText(config, "last_test_at")),
            ["last_test_ok"] = !PhpEmpty(AutoPriceConfigText(config, "last_test_ok")),
            ["last_test_message"] = AutoPriceConfigText(config, "last_test_message").Trim(),
            ["crawl_skip_until"] = skipUntil,
            ["crawl_skipped"] = skipUntil > DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["last_crawl_error"] = AutoPriceConfigText(config, "last_crawl_error").Trim(),
        };
    }

    private static async Task<Dictionary<string, object?>?> AutoPriceSourceAsync(
        IErpWriteConnectionFactory sources,
        long id,
        string siteKey,
        CancellationToken cancellationToken)
    {
        if (id <= 0 || !sources.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await sources.OpenAsync(cancellationToken).ConfigureAwait(false);
            var row = await OneRowAsync(
                connection,
                "SELECT * FROM `epc_discovery_sources` WHERE `id` = ? LIMIT 1",
                cancellationToken,
                id).ConfigureAwait(false);
            if (row is null || (siteKey.Length > 0 && AutoPriceText(row, "site_key") != siteKey))
            {
                return null;
            }

            return row;
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return null;
        }
    }

    private static bool AutoPriceAddNeedsClassic(IReadOnlyDictionary<string, string> posted)
    {
        var authType = AutoPricePosted(posted, "auth_type").Trim().ToLowerInvariant();
        return !PhpEmpty(AutoPricePosted(posted, "requires_login"))
            || (authType.Length > 0 && authType != "none")
            || AutoPricePosted(posted, "auth_username").Trim().Length > 0
            || AutoPricePosted(posted, "auth_password").Length > 0
            || AutoPricePosted(posted, "login_url").Trim().Length > 0
            || AutoPricePosted(posted, "login_form_selector").Trim().Length > 0
            || PhpInt(AutoPricePosted(posted, "taxonomy_node_id")) > 0
            || AutoPricePosted(posted, "product_line_slug").Length > 0;
    }

    private static bool AutoPriceRowNeedsClassic(IReadOnlyDictionary<string, object?> row)
    {
        var config = AutoPriceConfig(row);
        return AutoPriceText(row, "auth_type", "none").Trim().ToLowerInvariant() != "none"
            || AutoPriceText(row, "auth_username").Trim().Length > 0
            || AutoPriceText(row, "auth_password").Length > 0
            || PhpInt(AutoPriceText(row, "taxonomy_node_id")) > 0
            || AutoPriceText(row, "product_line_slug").Length > 0
            || config.ContainsKey("login_url")
            || config.ContainsKey("login_form_selector")
            || config.ContainsKey("auth_storage");
    }

    private static bool AutoPriceHasLogin(IReadOnlyDictionary<string, object?> row)
    {
        var type = AutoPriceText(row, "auth_type", "none").Trim().ToLowerInvariant();
        var user = AutoPriceText(row, "auth_username").Trim();
        var stored = AutoPriceText(row, "auth_password");
        return type switch
        {
            "basic" => user.Length > 0 && stored.Trim().Length > 0,
            "form_login" => user.Length > 0 && AutoPricePasswordDecode(stored).Trim().Length > 0,
            _ => false,
        };
    }

    private static string AutoPricePasswordDecode(string stored)
    {
        if (!stored.StartsWith("b64:", StringComparison.Ordinal))
        {
            return stored;
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(stored[4..]));
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    private static JsonObject AutoPriceConfig(IReadOnlyDictionary<string, object?> row)
    {
        try
        {
            return JsonNode.Parse(AutoPriceText(row, "config_json")) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private static string AutoPriceConfigText(JsonObject config, string name)
    {
        if (!config.TryGetPropertyValue(name, out var node) || node is null)
        {
            return string.Empty;
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var flag))
            {
                return flag ? "1" : string.Empty;
            }

            if (value.TryGetValue<string>(out var text))
            {
                return text;
            }
        }

        return node.ToJsonString();
    }

    private static string AutoPriceText(IReadOnlyDictionary<string, object?> row, string name, string fallback = "")
        => row.TryGetValue(name, out var value) && value is not null
            ? value is bool flag ? (flag ? "1" : "0") : Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback
            : fallback;

    private static string? AutoPriceValue(IReadOnlyDictionary<string, string> posted, IReadOnlyDictionary<string, string> query, string name)
        => posted.TryGetValue(name, out var value) ? value : query.TryGetValue(name, out var fromQuery) ? fromQuery : null;

    private static string AutoPricePosted(IReadOnlyDictionary<string, string> posted, string name)
        => posted.TryGetValue(name, out var value) ? value : string.Empty;

    private static bool PhpEmpty(string? value) => string.IsNullOrEmpty(value) || value == "0";
}
