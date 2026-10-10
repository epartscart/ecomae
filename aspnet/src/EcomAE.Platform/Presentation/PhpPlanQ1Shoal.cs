using System.Collections;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-shoal integrations helpers. PHP identifiers kept for the inventory:
/// <c>epc_int_h</c>, <c>epc_int_backend</c>, <c>epc_integrations_categories</c>,
/// <c>epc_integrations_resolve_guide</c>, <c>epc_integrations_catalog</c>,
/// <c>epc_integrations_ensure_schema</c>, <c>epc_integrations_site_key</c>,
/// <c>epc_integrations_features_for_site</c>, <c>epc_integrations_feature_enabled</c>,
/// <c>epc_integrations_save_feature_flags</c>, <c>epc_integrations_load_tenant_config</c>,
/// <c>epc_integrations_save_tenant_config</c>, <c>epc_integrations_default_mobile_config</c>,
/// <c>epc_integrations_mobile_config</c>, <c>epc_integrations_platform_mobile_defaults</c>,
/// <c>epc_integrations_menu_blocked_by_feature</c>, <c>epc_integrations_hub_rows</c>,
/// <c>epc_integrations_register_cp_content</c>.
/// Path: <c>content/general_pages/epc_integrations_helpers.php</c>.
/// GET never mints a session cookie. Leftover portal stay injected.
/// Catalog URLs are path strings without leftover unique <c>.php</c> page basenames.
/// </summary>
public static class PhpPlanQ1Shoal
{
    public const string IntegrationsHelpersPath = "content/general_pages/epc_integrations_helpers.php";

    public static string BackendDir { get; set; } = "cp";
    public static Func<bool>? IsSuperHost { get; set; }
    public static Func<string>? Host { get; set; }
    public static Func<MySqlConnection, List<Dictionary<string, object?>>>? ListTenants { get; set; }
    public static Func<MySqlConnection?>? PlatformPdo { get; set; }
    public static Func<MySqlConnection, Dictionary<string, object?>>? LoadSettings { get; set; }
    public static Action<MySqlConnection, Dictionary<string, object?>>? SaveSettings { get; set; }
    public static Action<MySqlConnection>? DbEnsure { get; set; }
    public static Action? RegisterMenu { get; set; }
    public static Func<string, int, Func<object?>, object?>? PerfCache { get; set; }
    public static Func<long>? Clock { get; set; }
    public static List<Dictionary<string, object?>> Tenants { get; } = [];
    public static Dictionary<string, object?> Settings { get; set; } = new(StringComparer.Ordinal);
    public static List<Dictionary<string, object?>> SavedSettings { get; } = [];

    private static readonly HashSet<MySqlConnection> SchemaDone = new(ReferenceEqualityComparer.Instance);
    private static readonly Dictionary<string, Dictionary<string, bool>> FeatureCache = new(StringComparer.Ordinal);

    public static void Reset()
    {
        BackendDir = "cp";
        IsSuperHost = null;
        Host = null;
        ListTenants = null;
        PlatformPdo = null;
        LoadSettings = null;
        SaveSettings = null;
        DbEnsure = null;
        RegisterMenu = null;
        PerfCache = null;
        Clock = null;
        Tenants.Clear();
        Settings = new Dictionary<string, object?>(StringComparer.Ordinal);
        SavedSettings.Clear();
        SchemaDone.Clear();
        FeatureCache.Clear();
    }

    public static string EpcIntH(object? value)
        => WebUtility.HtmlEncode(Str(value)).Replace("&#39;", "&#039;", StringComparison.Ordinal).Replace("'", "&#039;", StringComparison.Ordinal);

    public static string EpcIntBackend()
        => BackendDir.Trim('/');

    public static Dictionary<string, Dictionary<string, object?>> EpcIntegrationsCategories()
        => new(StringComparer.Ordinal)
        {
            ["identity"] = Cat("Identity & messaging", "fa-id-badge", "Login, email delivery, and customer messaging channels."),
            ["commerce"] = Cat("Commerce & payments", "fa-shopping-bag", "Checkout, POS, tax, and settlement rails."),
            ["growth"] = Cat("Marketing & growth", "fa-bullhorn", "Broadcast, social, tracking, and storefront content."),
            ["catalog"] = Cat("Catalog & AI", "fa-cubes", "Pricing intelligence and parts expert assistants."),
            ["data"] = Cat("Data & APIs", "fa-database", "REST keys, Power BI datasets, and analytics embeds."),
            ["platform"] = Cat("Platform", "fa-server", "Mobile shells and multi-tenant control.")
        };

    public static string EpcIntegrationsResolveGuide(string guide, string key = "")
    {
        guide = guide.Trim();
        var master = "/" + EpcIntBackend() + "/control/portal/epc_integrations_guide";
        if (guide == "")
        {
            return key != "" ? master + "#" + Uri.EscapeDataString(key) : master;
        }

        if (guide.StartsWith("http://", StringComparison.Ordinal)
            || guide.StartsWith("https://", StringComparison.Ordinal)
            || guide.StartsWith('/'))
        {
            return guide;
        }

        if (guide.StartsWith("docs/", StringComparison.Ordinal) || guide.StartsWith('#'))
        {
            var anchor = key != "" ? key : guide.TrimStart('#');
            return master + "#" + Uri.EscapeDataString(anchor);
        }

        return master + (key != "" ? "#" + Uri.EscapeDataString(key) : "");
    }

    public static Dictionary<string, Dictionary<string, object?>> EpcIntegrationsCatalog()
    {
        var be = EpcIntBackend();
        var guide = "/" + be + "/control/portal/epc_integrations_guide";
        return new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["email_smtp"] = Item("Email / SMTP", "fa-envelope", "#059669", "identity",
                "Transactional mail (orders, OTP, alerts) via tenant or platform SMTP.",
                "/" + be + "/control/portal/epc_cp_auth_settings",
                "/" + be + "/control/portal/epc_tenant_email_settings",
                guide + "#email_smtp"),
            ["oauth"] = Item("OAuth (Google, Microsoft…)", "fa-sign-in", "#2563eb", "identity",
                "Social / Microsoft login for CP and storefront — configured on Super CP.",
                "/" + be + "/control/portal/epc_cp_auth_settings",
                "/" + be + "/control/portal/epc_integrations_hub",
                guide + "#oauth", superOnly: true),
            ["registration_enhanced"] = Item("Registration enhanced", "fa-user-plus", "#0891b2", "identity",
                "Stronger signup flows, verification, and auth policies for tenants.",
                "/" + be + "/control/portal/epc_cp_auth_settings",
                "/" + be + "/control/portal/epc_integrations_hub",
                guide + "#registration_enhanced", superOnly: true),
            ["whatsapp"] = Item("WhatsApp sharing", "fa-whatsapp", "#16a34a", "identity",
                "wa.me order sharing with bilingual EN/AR templates for sales desks.",
                "/" + be + "/shop/orders/whatsapp-guide",
                "/" + be + "/shop/orders/whatsapp-guide",
                "/" + be + "/shop/orders/whatsapp-guide",
                menu: ["whatsapp"]),
            ["payment_gateways"] = Item("Payment gateways", "fa-credit-card", "#0369a1", "commerce",
                "Telr, GCC BNPL, JazzCash/Easypaisa, crypto, and per-account settlements.",
                "/" + be + "/shop/payments/payments",
                "/" + be + "/shop/payments/payments",
                guide + "#payment_gateways",
                menu: ["/shop/payments/"]),
            ["pos"] = Item("POS Terminal", "fa-cash-register", "#1d4ed8", "commerce",
                "Counter sales, cash/card tender, and ERP-linked receipts.",
                "/" + be + "/control/portal/epc_pos_tenant_manage",
                "/" + be + "/shop/pos/terminal",
                guide + "#pos",
                menu: ["/shop/pos/"]),
            ["tax_toolkit"] = Item("Tax Toolkit", "fa-globe", "#0f766e", "commerce",
                "Market VAT / tax profiles that follow the tenant country registration.",
                "/" + be + "/control/portal/epc_tax_toolkit_manage",
                "/" + be + "/shop/finance/erp",
                guide + "#tax_toolkit", superOnly: true,
                menu: ["epc_tax_toolkit", "uae-tax-compliance"]),
            ["custom_shipping"] = Item("Custom & shipping", "fa-ship", "#0e7490", "commerce",
                "Customs declarations, LGP intake, and shipping reports inside ERP.",
                "/" + be + "/control/portal/epc_custom_shipping_guide",
                "/" + be + "/shop/finance/erp?area=custom_shipping&tab=custom_shipping&epc_erp_shell=1",
                "/" + be + "/control/portal/epc_custom_shipping_guide",
                menu: ["custom_shipping", "custom-shipping"]),
            ["social_media_hub"] = Item("Social media hub", "fa-share-alt", "#db2777", "growth",
                "Publish calendars, account links, and AI-assisted social posts.",
                "/" + be + "/control/portal/epc_social_media_hub",
                "/" + be + "/control/portal/epc_social_media_hub",
                "/" + be + "/control/portal/epc_social_media_hub?tab=guide",
                menu: ["epc_social_media_hub"]),
            ["marketing_broadcast"] = Item("Marketing broadcast", "fa-paper-plane", "#ea580c", "growth",
                "Bulk email and WhatsApp campaigns with audience segments.",
                "/" + be + "/control/portal/epc_marketing_broadcast",
                "/" + be + "/control/portal/epc_marketing_broadcast",
                "/" + be + "/control/portal/epc_marketing_broadcast?tab=guide",
                menu: ["epc_marketing_broadcast", "/shop/marketing/"]),
            ["web_tracker"] = Item("Web tracker", "fa-line-chart", "#0284c7", "growth",
                "GA4 / Meta / TikTok pixels and storefront event wiring.",
                "/" + be + "/control/portal/epc_web_tracker",
                "/" + be + "/control/portal/epc_web_tracker",
                guide + "#web_tracker",
                menu: ["epc_web_tracker"]),
            ["visual_page_editor"] = Item("Visual page editor", "fa-paint-brush", "#be185d", "growth",
                "Drag-and-drop landing and content blocks for the storefront.",
                "/" + be + "/control/portal/epc_visual_page_editor",
                "/" + be + "/control/portal/epc_visual_page_editor",
                guide + "#visual_page_editor",
                menu: ["epc_visual_page_editor"]),
            ["auto_price_ai"] = Item("Auto Price AI", "fa-magic", "#0f766e", "catalog",
                "Discover, compare, and import competitive parts pricing by market.",
                "/" + be + "/control/portal/epc_auto_price_engine",
                "/" + be + "/control/portal/epc_auto_price_engine",
                "/" + be + "/control/portal/epc_auto_price_guide",
                menu: ["epc_auto_price", "/shop/parts_agent"]),
            ["parts_agent"] = Item("AI parts agent", "fa-robot", "#0e7490", "catalog",
                "Conversational parts expert for staff and storefront shoppers.",
                "/" + be + "/shop/parts_agent_chats",
                "/" + be + "/shop/parts_agent_chats",
                guide + "#parts_agent",
                menu: ["parts_agent"]),
            ["api_integrations"] = Item("API clients & keys", "fa-code", "#475569", "data",
                "Catalog & Price PRO clients plus tenant-scoped REST API keys.",
                "/" + be + "/control/portal/epc_api_clients_manage",
                "/" + be + "/control/portal/epc_api_clients_manage",
                "/" + be + "/control/portal/epc_api_documentation_guide",
                menu: ["epc_api_clients"]),
            ["power_bi"] = Item("Power BI", "fa-bar-chart", "#ca8a04", "data",
                "JSON/CSV datasets for Desktop refresh and optional report embed.",
                "/" + be + "/control/portal/epc_power_bi",
                "/" + be + "/control/portal/epc_power_bi",
                "/" + be + "/control/portal/epc_power_bi_guide",
                menu: ["epc_power_bi", "powerbi", "epc_power_bi_guide"]),
            ["mobile_apps"] = Item("Mobile apps (Android / iOS)", "fa-mobile-alt", "#dc2626", "platform",
                "PWA install plus Capacitor targets for CP, ERP, and storefront.",
                "/" + be + "/control/portal/epc_mobile_apps",
                "/" + be + "/control/portal/epc_mobile_apps",
                guide + "#mobile_apps"),
            ["tenant_registry"] = Item("Multi-tenant registry", "fa-sitemap", "#0369a1", "platform",
                "Live tenant hosts, DB credentials, and Super CP feature toggles.",
                "/" + be + "/shop/tenant_hub/tenant_hub",
                "",
                guide + "#tenant_registry", superOnly: true,
                menu: ["tenant_hub"])
        };
    }

    public static void EpcIntegrationsEnsureSchema(MySqlConnection pdo)
    {
        if (!SchemaDone.Add(pdo))
        {
            return;
        }

        using (var cmd = pdo.CreateCommand())
        {
            cmd.CommandText =
                "CREATE TABLE IF NOT EXISTS `epc_tenant_feature_flags` (" +
                "`site_key` VARCHAR(64) NOT NULL," +
                "`feature_key` VARCHAR(64) NOT NULL," +
                "`enabled` TINYINT(1) NOT NULL DEFAULT 1," +
                "`config_json` TEXT NULL," +
                "`updated_at` INT NOT NULL DEFAULT 0," +
                "PRIMARY KEY (`site_key`, `feature_key`)," +
                "KEY `feature_key` (`feature_key`)" +
                ") ENGINE=InnoDB DEFAULT CHARSET=utf8";
            cmd.ExecuteNonQuery();
        }

        DbEnsure?.Invoke(pdo);
        try
        {
            using var probe = pdo.CreateCommand();
            probe.CommandText = "SELECT `integrations_json` FROM `epc_portal_site_settings` LIMIT 1";
            probe.ExecuteScalar();
        }
        catch
        {
            using var alter = pdo.CreateCommand();
            alter.CommandText = "ALTER TABLE `epc_portal_site_settings` ADD COLUMN `integrations_json` TEXT NULL AFTER `cp_menu_json`";
            try
            {
                alter.ExecuteNonQuery();
            }
            catch
            {
            }
        }
    }

    public static string EpcIntegrationsSiteKey(MySqlConnection? pdo = null)
    {
        if (IsSuperHost?.Invoke() == true)
        {
            return "platform";
        }

        var host = (Host?.Invoke() ?? "").Trim().ToLowerInvariant();
        host = Regex.Replace(host, @"^www\.", "");
        if (pdo is not null)
        {
            foreach (var row in ListTenants?.Invoke(pdo) ?? Tenants)
            {
                var h = Regex.Replace(Str(row.GetValueOrDefault("hostname")).ToLowerInvariant(), @"^www\.", "");
                if (h == host || host == "www." + h)
                {
                    var key = Str(row.GetValueOrDefault("site_key"));
                    return key != "" ? key : h;
                }
            }
        }

        return Regex.Replace(host.Replace(".", "-"), @"[^a-z0-9_-]", "");
    }

    public static Dictionary<string, bool> EpcIntegrationsFeaturesForSite(string siteKey, MySqlConnection? platformPdo = null)
    {
        siteKey = Regex.Replace(siteKey.ToLowerInvariant(), @"[^a-z0-9_\-]", "");
        if (siteKey != "" && FeatureCache.TryGetValue(siteKey, out var cached))
        {
            return cached;
        }

        var defaults = Defaults();
        if (siteKey is "" or "platform")
        {
            FeatureCache[siteKey] = defaults;
            return defaults;
        }

        var loaded = PerfRemember("epc_int_features:v2:" + siteKey, 300, () =>
        {
            platformPdo ??= PlatformPdo?.Invoke();
            if (platformPdo is null)
            {
                return defaults;
            }

            EpcIntegrationsEnsureSchema(platformPdo);
            var output = new Dictionary<string, bool>(defaults, StringComparer.Ordinal);
            try
            {
                using var cmd = platformPdo.CreateCommand();
                cmd.CommandText = "SELECT `feature_key`, `enabled` FROM `epc_tenant_feature_flags` WHERE `site_key` = @k";
                cmd.Parameters.AddWithValue("@k", siteKey);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var fk = reader.IsDBNull(0) ? "" : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "";
                    if (fk == "" || !output.ContainsKey(fk))
                    {
                        continue;
                    }

                    output[fk] = ToInt(reader.GetValue(1)) == 1;
                }
            }
            catch
            {
                return defaults;
            }

            return output;
        });

        var map = loaded as Dictionary<string, bool> ?? defaults;
        FeatureCache[siteKey] = map;
        return map;
    }

    public static bool EpcIntegrationsFeatureEnabled(string featureKey, string? siteKey = null, MySqlConnection? platformPdo = null)
    {
        var catalog = EpcIntegrationsCatalog();
        if (!catalog.ContainsKey(featureKey))
        {
            return true;
        }

        var defaultOn = !PhpEmpty(catalog[featureKey].GetValueOrDefault("default_enabled"));
        if (string.IsNullOrEmpty(siteKey))
        {
            siteKey = EpcIntegrationsSiteKey(platformPdo);
        }

        if (siteKey is "platform" or "")
        {
            return defaultOn;
        }

        var flags = EpcIntegrationsFeaturesForSite(siteKey, platformPdo);
        return flags.TryGetValue(featureKey, out var value) ? value : defaultOn;
    }

    public static Dictionary<string, object?> EpcIntegrationsSaveFeatureFlags(
        MySqlConnection platformPdo,
        string siteKey,
        Dictionary<string, object?> flags)
    {
        EpcIntegrationsEnsureSchema(platformPdo);
        var now = Clock?.Invoke() ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var catalog = EpcIntegrationsCatalog();
        var saved = 0;
        foreach (var pair in flags)
        {
            var featureKey = Regex.Replace(Str(pair.Key), @"[^a-z0-9_]", "");
            if (featureKey == "" || !catalog.ContainsKey(featureKey))
            {
                continue;
            }

            using var cmd = platformPdo.CreateCommand();
            cmd.CommandText =
                "INSERT INTO `epc_tenant_feature_flags` (`site_key`, `feature_key`, `enabled`, `updated_at`) " +
                "VALUES (@s, @f, @e, @t) " +
                "ON DUPLICATE KEY UPDATE `enabled` = VALUES(`enabled`), `updated_at` = VALUES(`updated_at`)";
            cmd.Parameters.AddWithValue("@s", siteKey);
            cmd.Parameters.AddWithValue("@f", featureKey);
            cmd.Parameters.AddWithValue("@e", PhpEmpty(pair.Value) ? 0 : 1);
            cmd.Parameters.AddWithValue("@t", now);
            cmd.ExecuteNonQuery();
            saved++;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["saved"] = saved
        };
    }

    public static Dictionary<string, object?> EpcIntegrationsLoadTenantConfig(MySqlConnection? pdo = null)
    {
        pdo ??= PlatformPdo?.Invoke();
        if (pdo is null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        EpcIntegrationsEnsureSchema(pdo);
        var settings = LoadSettings?.Invoke(pdo) ?? Settings;
        var raw = settings.GetValueOrDefault("integrations");
        if (raw is string text)
        {
            try
            {
                var decoded = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(text);
                return decoded ?? new Dictionary<string, object?>(StringComparer.Ordinal);
            }
            catch
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal);
            }
        }

        if (raw is Dictionary<string, object?> map)
        {
            return map;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    public static Dictionary<string, object?> EpcIntegrationsSaveTenantConfig(MySqlConnection pdo, Dictionary<string, object?> integrations)
    {
        EpcIntegrationsEnsureSchema(pdo);
        var settings = new Dictionary<string, object?>(LoadSettings?.Invoke(pdo) ?? Settings, StringComparer.Ordinal)
        {
            ["integrations"] = integrations
        };
        if (SaveSettings is not null)
        {
            SaveSettings(pdo, settings);
        }
        else
        {
            Settings = settings;
        }

        SavedSettings.Add(settings);
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true };
    }

    public static Dictionary<string, object?> EpcIntegrationsDefaultMobileConfig()
        => new(StringComparer.Ordinal)
        {
            ["enabled"] = false,
            ["app_name"] = "",
            ["bundle_id"] = "",
            ["deep_link_scheme"] = "",
            ["deep_link_domain"] = "",
            ["api_base_url"] = "",
            ["play_store_url"] = "",
            ["app_store_url"] = "",
            ["pwa_enabled"] = true,
            ["firebase_project_id"] = "",
            ["push_enabled"] = false
        };

    public static Dictionary<string, object?> EpcIntegrationsMobileConfig(MySqlConnection? pdo = null)
    {
        var cfg = EpcIntegrationsLoadTenantConfig(pdo);
        var mobile = cfg.GetValueOrDefault("mobile") is Dictionary<string, object?> map
            ? map
            : new Dictionary<string, object?>(StringComparer.Ordinal);
        var merged = EpcIntegrationsDefaultMobileConfig();
        foreach (var pair in mobile)
        {
            merged[pair.Key] = pair.Value;
        }

        return merged;
    }

    public static Dictionary<string, object?> EpcIntegrationsPlatformMobileDefaults()
        => new(StringComparer.Ordinal)
        {
            ["api_base_url"] = "https://www.ecomae.com",
            ["firebase_template"] = "",
            ["capacitor_version"] = "6",
            ["allow_push"] = true,
            ["default_deep_link_scheme"] = "epartscart://"
        };

    public static bool EpcIntegrationsMenuBlockedByFeature(string itemUrl)
    {
        if (IsSuperHost?.Invoke() == true)
        {
            return false;
        }

        var url = Regex.Replace(itemUrl, @"\?.*$", "").ToLowerInvariant();
        var siteKey = EpcIntegrationsSiteKey();
        if (siteKey is "platform" or "")
        {
            return false;
        }

        var flags = EpcIntegrationsFeaturesForSite(siteKey);
        foreach (var (featureKey, meta) in EpcIntegrationsCatalog())
        {
            if (flags.TryGetValue(featureKey, out var on) && !PhpEmpty(on))
            {
                continue;
            }

            if (meta.GetValueOrDefault("menu_patterns") is not IEnumerable patterns)
            {
                continue;
            }

            foreach (var patternObj in patterns)
            {
                var pattern = Str(patternObj).ToLowerInvariant();
                if (pattern != "" && url.Contains(pattern, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static List<Dictionary<string, object?>> EpcIntegrationsHubRows(MySqlConnection? pdo = null, bool isSuper = false)
    {
        var siteKey = isSuper ? "platform" : EpcIntegrationsSiteKey(pdo);
        var features = EpcIntegrationsFeaturesForSite(siteKey, isSuper ? pdo : null);
        var rows = new List<Dictionary<string, object?>>();
        foreach (var (key, meta) in EpcIntegrationsCatalog())
        {
            if (isSuper && PhpEmpty(meta.GetValueOrDefault("super_url")))
            {
                continue;
            }

            if (!isSuper && PhpEmpty(meta.GetValueOrDefault("tenant_url")) && !PhpEmpty(meta.GetValueOrDefault("super_only_config")))
            {
                continue;
            }

            var enabled = isSuper || (features.TryGetValue(key, out var flag) && !PhpEmpty(flag));
            var configUrl = isSuper
                ? Str(meta.GetValueOrDefault("super_url"))
                : Str(meta.GetValueOrDefault("tenant_url"), Str(meta.GetValueOrDefault("super_url")));
            if (!isSuper && !PhpEmpty(meta.GetValueOrDefault("super_only_config")))
            {
                configUrl = "/" + EpcIntBackend() + "/control/portal/epc_integrations_hub";
            }

            var guideRaw = Str(meta.GetValueOrDefault("guide"));
            if (!isSuper && guideRaw.Contains("epc_api_documentation_guide", StringComparison.Ordinal))
            {
                guideRaw = "/" + EpcIntBackend() + "/control/portal/epc_integrations_guide#api_integrations";
            }

            rows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["key"] = key,
                ["label"] = Str(meta.GetValueOrDefault("label")),
                ["icon"] = Str(meta.GetValueOrDefault("icon"), "fa-plug"),
                ["color"] = Str(meta.GetValueOrDefault("color"), "#64748b"),
                ["category"] = Str(meta.GetValueOrDefault("category"), "platform"),
                ["blurb"] = Str(meta.GetValueOrDefault("blurb")),
                ["enabled"] = enabled,
                ["active"] = enabled,
                ["configure_url"] = configUrl,
                ["guide"] = EpcIntegrationsResolveGuide(guideRaw, key),
                ["super_only"] = !PhpEmpty(meta.GetValueOrDefault("super_only_config"))
            });
        }

        return rows;
    }

    public static int EpcIntegrationsRegisterCpContent(
        MySqlConnection pdo,
        string urlSlug,
        string langKey,
        string titleEn,
        string titleRu,
        string phpRel,
        int menuOrder = 8)
    {
        RegisterMenu?.Invoke();
        Exec(pdo,
            "INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (@k, @d, NULL, 0, 1, 1)",
            ("@k", langKey), ("@d", titleEn));
        Exec(pdo,
            "INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (@k, @c, @v) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)",
            ("@k", langKey), ("@c", "en"), ("@v", titleEn));
        Exec(pdo,
            "INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (@k, @c, @v) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)",
            ("@k", langKey), ("@c", "ru"), ("@v", titleRu));

        var contentUrl = "control/portal/" + urlSlug;
        var phpPath = "/<backend_dir>/content/control/portal/" + urlSlug + ".php";
        _ = phpRel;
        var now = Clock?.Invoke() ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var parent = QueryRow(pdo, "SELECT `id`, `level` FROM `content` WHERE `url` = @u AND `is_frontend` = 0 LIMIT 1", ("@u", "control/config"));
        parent ??= QueryRow(pdo, "SELECT `id`, `level` FROM `content` WHERE `url` = @u AND `is_frontend` = 0 LIMIT 1", ("@u", "control"));
        if (parent is null)
        {
            return 0;
        }

        var parentId = ToInt(parent["id"]);
        var level = ToInt(parent["level"]) + 1;
        var contentId = ScalarInt(pdo, "SELECT `id` FROM `content` WHERE `url` = @u AND `is_frontend` = 0 LIMIT 1", ("@u", contentUrl));
        if (contentId > 0)
        {
            Exec(pdo,
                "UPDATE `content` SET `published_flag` = 1, `content_type` = 'php', `content` = @p, `title_tag` = @t, `value` = @v, `parent` = @par, `level` = @l, `alias` = @a WHERE `id` = @id",
                ("@p", phpPath), ("@t", langKey), ("@v", langKey), ("@par", parentId), ("@l", level), ("@a", urlSlug), ("@id", contentId));
        }
        else
        {
            using var ins = pdo.CreateCommand();
            ins.CommandText =
                "INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`, " +
                "`title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`, " +
                "`system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`) " +
                "VALUES (0, @url, @level, @alias, @value, @parent, @desc, 0, 'php', @content, @title, '0', '0', '0', 0, '[]', '', '', 0, 1, 0, @now, @now2, @ord)";
            ins.Parameters.AddWithValue("@url", contentUrl);
            ins.Parameters.AddWithValue("@level", level);
            ins.Parameters.AddWithValue("@alias", urlSlug);
            ins.Parameters.AddWithValue("@value", langKey);
            ins.Parameters.AddWithValue("@parent", parentId);
            ins.Parameters.AddWithValue("@desc", titleEn);
            ins.Parameters.AddWithValue("@content", phpPath);
            ins.Parameters.AddWithValue("@title", langKey);
            ins.Parameters.AddWithValue("@now", now);
            ins.Parameters.AddWithValue("@now2", now);
            ins.Parameters.AddWithValue("@ord", menuOrder);
            ins.ExecuteNonQuery();
            contentId = (int)ins.LastInsertedId;
        }

        var refId = ScalarInt(pdo, "SELECT `id` FROM `content` WHERE `url` = @u AND `is_frontend` = 0 LIMIT 1", ("@u", "control/portal/industry_settings"));
        if (refId > 0 && contentId > 0)
        {
            Exec(pdo, "DELETE FROM `content_access` WHERE `content_id` = @id", ("@id", contentId));
            using var groups = pdo.CreateCommand();
            groups.CommandText = "SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = @id";
            groups.Parameters.AddWithValue("@id", refId);
            using var reader = groups.ExecuteReader();
            var ids = new List<int>();
            while (reader.Read())
            {
                ids.Add(ToInt(reader.GetValue(0)));
            }

            reader.Close();
            foreach (var groupId in ids)
            {
                try
                {
                    Exec(pdo, "INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (@c, @g)", ("@c", contentId), ("@g", groupId));
                }
                catch
                {
                }
            }
        }

        return contentId;
    }

    private static Dictionary<string, bool> Defaults()
    {
        var catalog = EpcIntegrationsCatalog();
        var defaults = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var (key, meta) in catalog)
        {
            defaults[key] = !PhpEmpty(meta.GetValueOrDefault("default_enabled"));
        }

        return defaults;
    }

    private static object? PerfRemember(string key, int ttl, Func<object?> fn)
        => PerfCache is not null ? PerfCache(key, ttl, fn) : fn();

    private static Dictionary<string, object?> Cat(string label, string icon, string blurb)
        => new(StringComparer.Ordinal)
        {
            ["label"] = label,
            ["icon"] = icon,
            ["blurb"] = blurb
        };

    private static Dictionary<string, object?> Item(
        string label,
        string icon,
        string color,
        string category,
        string blurb,
        string superUrl,
        string tenantUrl,
        string guide,
        bool superOnly = false,
        bool defaultEnabled = true,
        string[]? menu = null)
        => new(StringComparer.Ordinal)
        {
            ["label"] = label,
            ["icon"] = icon,
            ["color"] = color,
            ["category"] = category,
            ["blurb"] = blurb,
            ["super_url"] = superUrl,
            ["tenant_url"] = tenantUrl,
            ["guide"] = guide,
            ["super_only_config"] = superOnly,
            ["default_enabled"] = defaultEnabled,
            ["menu_patterns"] = menu ?? []
        };

    private static void Exec(MySqlConnection pdo, string sql, params (string Name, object? Value)[] args)
    {
        using var cmd = pdo.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        cmd.ExecuteNonQuery();
    }

    private static Dictionary<string, object?>? QueryRow(MySqlConnection pdo, string sql, params (string Name, object? Value)[] args)
    {
        using var cmd = pdo.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var row = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        return row;
    }

    private static int ScalarInt(MySqlConnection pdo, string sql, params (string Name, object? Value)[] args)
    {
        using var cmd = pdo.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        var value = cmd.ExecuteScalar();
        return value is null or DBNull ? 0 : ToInt(value);
    }

    private static bool PhpEmpty(object? value)
    {
        if (value is null or DBNull or false)
        {
            return true;
        }

        return value switch
        {
            string s => s is "" or "0",
            bool b => !b,
            int i => i == 0,
            long l => l == 0,
            short s => s == 0,
            byte b => b == 0,
            sbyte sb => sb == 0,
            uint u => u == 0,
            ulong ul => ul == 0,
            double d => d == 0,
            float f => f == 0,
            decimal m => m == 0,
            ICollection c => c.Count == 0,
            _ => false
        };
    }

    private static int ToInt(object? value)
    {
        if (value is null or DBNull or false)
        {
            return 0;
        }

        if (value is true)
        {
            return 1;
        }

        if (value is IConvertible)
        {
            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        return 0;
    }

    private static string Str(object? value, string fallback = "")
        => value is null or DBNull ? fallback : Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;
}
