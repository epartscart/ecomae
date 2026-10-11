using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-hint EParts CATA config library.
/// PHP identifiers kept for the inventory: <c>epc_cata_providers</c>,
/// <c>epc_cata_provider_ids</c>, <c>epc_cata_normalize_provider</c>,
/// <c>epc_cata_file_config</c>, <c>epc_cata_enabled_for_request</c>,
/// <c>epc_cata_db</c>, <c>epc_cata_tables</c>, <c>epc_cata_ensure_tables</c>,
/// <c>epc_cata_default_categories</c>, <c>epc_cata_default_categories_json_path</c>,
/// <c>epc_cata_default_categories_cached</c>, <c>epc_cata_sanitize_category_name</c>,
/// <c>epc_cata_normalize_category_rows</c>, <c>epc_cata_payload_data</c>,
/// <c>epc_cata_tecdoc_config</c>, <c>epc_cata_provider_enabled</c>,
/// <c>epc_cata_providers_enabled_map</c>, <c>epc_cata_local_catalog_config</c>,
/// <c>epc_cata_local_catalog_pdo</c>, <c>epc_cata_cp_status_shell</c>,
/// <c>epc_cata_cp_status_cached</c>, <c>epc_cata_cp_import_monitor</c>,
/// <c>epc_cata_should_use_default_category_tree</c>, <c>epc_cata_route_url</c>,
/// <c>epc_cata_normalize_vin</c>, <c>epc_cata_modification_from_row</c>,
/// <c>epc_cata_provider_origin</c>, <c>epc_cata_legacy_browse_counts</c>,
/// <c>epc_cata_legacy_vin_counts</c>, <c>epc_cata_settings_table</c>,
/// <c>epc_cata_settings_ensure</c>, <c>epc_cata_settings_cache_ref</c>,
/// <c>epc_cata_setting_forget</c>, <c>epc_cata_setting_get</c>,
/// <c>epc_cata_setting_set</c>, <c>epc_cata_storefront_url</c>,
/// <c>epc_cata_presentation_defaults</c>, <c>epc_cata_presentation_config</c>,
/// <c>epc_cata_default_subcategories_json_path</c>,
/// <c>epc_cata_default_subcategories_cached</c>, <c>epc_cata_attach_subcategories</c>,
/// <c>epc_cata_category_config_setting_key</c>, <c>epc_cata_category_icon_dir</c>,
/// <c>epc_cata_category_icons_available</c>, <c>epc_cata_category_config_raw</c>,
/// <c>epc_cata_category_config_normalize_subcategories</c>,
/// <c>epc_cata_category_config_normalize_entry</c>,
/// <c>epc_cata_category_config_seed</c>, <c>epc_cata_category_config_rows_for_cp</c>,
/// <c>epc_cata_storefront_subcategories_map</c>,
/// <c>epc_cata_category_config_save_bulk</c>, <c>epc_cata_category_config_save_one</c>,
/// <c>epc_cata_category_config_reset</c>,
/// <c>epc_cata_category_config_sync_presentation_disabled</c>,
/// <c>epc_cata_category_config_sync_legacy_mappings</c>,
/// <c>epc_cata_category_config_for_ajax</c>, <c>epc_cata_catalog_targets</c>,
/// <c>epc_cata_normalize_catalog_target</c>, <c>epc_cata_category_mappings_raw</c>,
/// <c>epc_cata_category_mapping_for</c>, <c>epc_cata_apply_category_mappings</c>,
/// <c>epc_cata_storefront_slug</c>, <c>epc_cata_storefront_resolve_category</c>,
/// <c>epc_cata_storefront_category_href</c>, <c>epc_cata_storefront_categories</c>,
/// <c>epc_cata_presentation_js_config_light</c>,
/// <c>epc_cata_presentation_js_config</c>, <c>EPC_CATA_VERSION</c>,
/// <c>EPC_CATA_CP_CACHE_TTL</c>.
/// Path: <c>content/general_pages/epc_cata_config.php</c>.
/// GET never mints a session cookie. Leftover unique portal / user helper
/// parents stay unnamed. Auto-parts site is an injected flag. ePartsCart
/// host uses the already-closed Dock hostname helper. DB helpers return
/// empty/false without a PDO.
/// </summary>
public static class PhpPlanQ1Hint
{
    public const string CataConfigPath = "content/general_pages/epc_cata_config.php";
    public const string EpcCataVersion = "0.5.26-home-critical-bundle";
    public const int EpcCataCpCacheTtl = 300;

    public static bool AutoPartsSite { get; set; }
    public static string DocumentRoot { get; set; } = "";
    public static string LangHref { get; set; } = "";
    public static Dictionary<string, object?> FileConfigValues { get; set; } = new(StringComparer.Ordinal);
    public static Dictionary<string, object?> SettingsCache { get; set; } = new(StringComparer.Ordinal);

    public static void Reset()
    {
        AutoPartsSite = false;
        DocumentRoot = "";
        LangHref = "";
        FileConfigValues = new Dictionary<string, object?>(StringComparer.Ordinal);
        SettingsCache = new Dictionary<string, object?>(StringComparer.Ordinal);
        PhpPlanQ1Dock.Server = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public static Dictionary<string, Dictionary<string, object?>> EpcCataProviders()
        => PhpPlanQ1HintData.Providers();

    public static List<string> EpcCataProviderIds()
        => EpcCataProviders().Keys.ToList();

    public static string EpcCataNormalizeProvider(string provider)
    {
        provider = Regex.Replace(provider, "[^a-z0-9_]", "").ToLowerInvariant();
        provider = provider switch
        {
            "local" or "tecdoc" => "local_tecdoc",
            "eparts" or "partsapi" => "eparts_api",
            _ => provider
        };
        return EpcCataProviderIds().Contains(provider, StringComparer.Ordinal) ? provider : "";
    }

    public static Dictionary<string, object?> EpcCataFileConfig()
        => new(FileConfigValues, StringComparer.Ordinal);

    public static bool EpcCataEnabledForRequest()
    {
        if (PhpPlanQ1Dock.EpcPortalIsEpartscartHostname())
        {
            return true;
        }

        if (AutoPartsSite)
        {
            var file = EpcCataFileConfig();
            return !file.ContainsKey("enabled") || !PhpEmpty(file["enabled"]);
        }

        return false;
    }

    public static object? EpcCataDb() => null;

    public static Dictionary<string, string> EpcCataTables()
        => PhpPlanQ1HintData.Tables();

    public static bool EpcCataEnsureTables()
        => EpcCataDb() is not null;

    public static List<Dictionary<string, object?>> EpcCataDefaultCategories()
        => PhpPlanQ1HintData.DefaultCategories();

    public static string EpcCataDefaultCategoriesJsonPath()
        => DocumentRoot + "/content/general_pages/epc_cata_default_categories.json";

    public static List<Dictionary<string, object?>> EpcCataDefaultCategoriesCached()
        => EpcCataDefaultCategories();

    public static string EpcCataSanitizeCategoryName(string name, int strId = 0)
    {
        name = name.Trim();
        if (name == "" || Regex.IsMatch(name, @"^[\?\.\s\uFFFD]+$", RegexOptions.CultureInvariant))
        {
            return strId > 0 ? "Category " + strId : "";
        }

        if (Regex.IsMatch(name, @"^\?{2,}$", RegexOptions.CultureInvariant))
        {
            return strId > 0 ? "Category " + strId : "";
        }

        return name;
    }

    public static List<Dictionary<string, object?>> EpcCataNormalizeCategoryRows(IEnumerable<object?> rows)
    {
        var outRows = new List<Dictionary<string, object?>>();
        foreach (var raw in rows)
        {
            if (raw is not Dictionary<string, object?> row)
            {
                continue;
            }

            var strId = PhpInt(Get(row, "STR_ID") ?? Get(row, "CATEGORY_ID") ?? Get(row, "ext_id") ?? Get(row, "id"));
            var name = EpcCataSanitizeCategoryName(PhpString(Get(row, "CATEGORY_NAME") ?? Get(row, "name")), strId);
            if (name == "")
            {
                continue;
            }

            var iconId = PhpInt(Get(row, "ICON_ID") ?? Get(row, "icon_id") ?? strId);
            var order = PhpInt(Get(row, "ORDER") ?? Get(row, "order"));
            var fallback = outRows.Count + 1;
            var entry = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["STR_ID"] = strId > 0 ? strId : fallback,
                ["CATEGORY_ID"] = strId > 0 ? strId : fallback,
                ["ICON_ID"] = iconId > 0 ? iconId : (strId > 0 ? strId : fallback),
                ["CATEGORY_NAME"] = name,
                ["source"] = PhpString(Get(row, "source") ?? "default_tree"),
            };
            if (order > 0)
            {
                entry["ORDER"] = order;
            }

            outRows.Add(entry);
        }

        outRows.Sort(CompareOrderThenId);
        return outRows;
    }

    public static List<object?> EpcCataPayloadData(Dictionary<string, object?> payload)
    {
        if (payload.TryGetValue("data", out var data) && !PhpEmpty(data) && data is IEnumerable<object?> list && data is not string)
        {
            return list.ToList();
        }

        if (payload.ContainsKey("0") || payload.Keys.Any(k => k == "0"))
        {
            return payload.Values.ToList();
        }

        return [];
    }

    public static List<object?> EpcCataPayloadData(IEnumerable<object?> payload)
    {
        var list = payload.ToList();
        return list.Count > 0 && list[0] is Dictionary<string, object?> ? list : [];
    }

    public static Dictionary<string, object?> EpcCataTecdocConfig()
    {
        var file = EpcCataFileConfig();
        if (file.TryGetValue("tecdoc", out var td) && td is Dictionary<string, object?> tdMap && tdMap.Count > 0)
        {
            return tdMap;
        }

        var root = PhpString(Get(file, "tecdoc_data_root")).Trim();
        if (root == "")
        {
            root = "/home/ecomae/epc-tecdoc-data";
        }

        var archives = Get(file, "tecdoc_archive_paths") is IEnumerable<object?> arch ? arch.ToList() : [];
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["data_root"] = root,
            ["work_path"] = PhpString(Get(file, "tecdoc_work_path") ?? root + "/work"),
            ["dump_path"] = PhpString(Get(file, "tecdoc_dump_path") ?? root + "/work"),
            ["archive_path"] = PhpString(Get(file, "tecdoc_archive_path")),
            ["archive_paths"] = archives,
            ["archives_configured"] = false,
            ["export_inner_name"] = PhpString(Get(file, "tecdoc_export_inner_name") ?? "EN-EXPORT_UTF8.zip"),
            ["stream_mode"] = !PhpEmpty(Get(file, "tecdoc_stream_mode")),
            ["no_full_extract"] = !PhpEmpty(Get(file, "tecdoc_no_full_extract")),
            ["min_year"] = file.ContainsKey("tecdoc_min_year") ? PhpInt(file["tecdoc_min_year"]) : 2017,
            ["batch_limit"] = file.ContainsKey("tecdoc_batch_limit") ? PhpInt(file["tecdoc_batch_limit"]) : 5000,
            ["table_prefix"] = PhpString(Get(file, "tecdoc_table_prefix") ?? "td_"),
            ["sql_files"] = Get(file, "tecdoc_sql_files") is IEnumerable<object?> sql ? sql.ToList() : new List<object?>(),
        };
    }

    public static bool EpcCataProviderEnabled(string provider)
    {
        provider = EpcCataNormalizeProvider(provider);
        if (provider == "")
        {
            return false;
        }

        var providers = EpcCataProviders();
        if (!providers.TryGetValue(provider, out var meta) || PhpEmpty(Get(meta, "enabled")))
        {
            return false;
        }

        var file = EpcCataFileConfig();
        if (file.TryGetValue("providers", out var provs) &&
            provs is Dictionary<string, object?> pmap &&
            pmap.TryGetValue(provider, out var row) &&
            row is Dictionary<string, object?> prow &&
            prow.ContainsKey("enabled") &&
            PhpEmpty(prow["enabled"]))
        {
            return false;
        }

        var saved = EpcCataSettingGet("providers_enabled", new Dictionary<string, object?>(StringComparer.Ordinal));
        if (saved is Dictionary<string, object?> smap && smap.ContainsKey(provider))
        {
            return !PhpEmpty(smap[provider]);
        }

        return true;
    }

    public static Dictionary<string, bool> EpcCataProvidersEnabledMap()
    {
        var saved = EpcCataSettingGet("providers_enabled", new Dictionary<string, object?>(StringComparer.Ordinal)) as Dictionary<string, object?> ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var map = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var (id, meta) in EpcCataProviders())
        {
            if (saved.ContainsKey(id))
            {
                map[id] = !PhpEmpty(saved[id]);
                continue;
            }

            var file = EpcCataFileConfig();
            if (file.TryGetValue("providers", out var provs) &&
                provs is Dictionary<string, object?> pmap &&
                pmap.TryGetValue(id, out var row) &&
                row is Dictionary<string, object?> prow &&
                prow.ContainsKey("enabled"))
            {
                map[id] = !PhpEmpty(prow["enabled"]);
                continue;
            }

            map[id] = !PhpEmpty(Get(meta, "enabled"));
        }

        return map;
    }

    public static Dictionary<string, object?> EpcCataLocalCatalogConfig()
        => new(StringComparer.Ordinal)
        {
            ["host"] = "",
            ["db"] = "",
            ["user"] = "",
            ["password"] = "",
            ["table_prefix"] = "td_",
        };

    public static object? EpcCataLocalCatalogPdo() => null;

    public static Dictionary<string, object?> EpcCataCpStatusShell()
        => new(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["version"] = EpcCataVersion,
            ["providers"] = new List<object>(),
            ["totals"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["manufacturers"] = 0,
                ["models"] = 0,
                ["modifications"] = 0,
                ["categories"] = 0,
                ["articles"] = 0,
                ["crosses"] = 0,
                ["vins"] = 0,
                ["fitment"] = 0,
            },
            ["origins"] = new List<object>(),
            ["legacy_cache"] = new List<object>(),
            ["legacy_vins"] = new List<object>(),
            ["search_log"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["total"] = 0,
                ["by_provider"] = new List<object>(),
                ["by_type"] = new List<object>(),
            },
            ["save_coverage"] = new List<object>(),
            ["protocol"] = "",
        };

    public static Dictionary<string, object?> EpcCataCpStatusCached(int ttlSeconds = 0, bool forceRebuild = false)
        => new(StringComparer.Ordinal)
        {
            ["ok"] = false,
            ["error"] = "sync status unavailable",
            ["version"] = EpcCataVersion,
        };

    public static Dictionary<string, object?> EpcCataCpImportMonitor(bool forceRebuild = false)
    {
        const string stockNote = "No shop_docpart_prices_data rows with exist>0 yet — Emex import still partial or qty column empty";
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["import_phase"] = "unknown",
            ["rows"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["emex_articles"] = 0,
                ["emex_prices"] = 0,
                ["crosses"] = 0,
                ["price_stock_rows"] = 0,
            },
            ["stock_note"] = stockNote,
            ["datasets"] = new List<Dictionary<string, object?>>
            {
                Ds("tecdoc_transfernow", 0, "uploading"),
                Ds("wetransfer_outer", 0, "missing"),
                Ds("emex_articles", 0, "unknown"),
                Ds("emex_prices", 0, "waiting for xlsx"),
                Ds("crossbase", 0, "pending"),
            },
        };
    }

    public static bool EpcCataShouldUseDefaultCategoryTree(string source)
    {
        source = EpcCataNormalizeProvider(source);
        return source == "" || source == "local_tecdoc";
    }

    public static string EpcCataRouteUrl() => "/eparts-cata";

    public static string EpcCataNormalizeVin(string vin)
    {
        vin = Regex.Replace(vin, "[^A-Z0-9]", "").ToUpperInvariant();
        return vin.Length is >= 11 and <= 17 ? vin : "";
    }

    public static Dictionary<string, object?> EpcCataModificationFromRow(Dictionary<string, object?> row)
    {
        var raw = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (!PhpEmpty(Get(row, "raw_json")))
        {
            try
            {
                using var doc = JsonDocument.Parse(PhpString(Get(row, "raw_json")));
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in doc.RootElement.EnumerateObject())
                    {
                        raw[p.Name] = JsonBox(p.Value);
                    }
                }
            }
            catch (JsonException)
            {
            }
        }

        var baseRow = raw.Count > 0 ? raw : row;
        var id = PhpInt(Get(baseRow, "PC_ID") ?? Get(baseRow, "ID") ?? Get(baseRow, "carId") ?? Get(row, "ext_id"));
        if (id <= 0)
        {
            id = PhpInt(Get(row, "ext_id"));
        }

        var title = PhpString(
            Get(baseRow, "MODIFICATION") ?? Get(baseRow, "PASSENGER_CAR") ?? Get(baseRow, "COMMERCIAL_VEHICLE")
            ?? Get(baseRow, "MOTORBIKE") ?? Get(row, "title") ?? Get(baseRow, "title") ?? Get(baseRow, "name"));
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["source"] = PhpString(Get(row, "source") ?? Get(baseRow, "source")),
            ["section"] = PhpString(Get(row, "section") ?? Get(baseRow, "section") ?? "passenger"),
            ["ms_ext_id"] = PhpInt(Get(row, "ms_ext_id") ?? Get(baseRow, "MS_ID")),
            ["ext_id"] = id,
            ["ID"] = id,
            ["carId"] = id,
            ["PC_ID"] = PhpInt(Get(baseRow, "PC_ID") ?? id),
            ["title"] = title,
            ["MODIFICATION"] = title,
            ["PASSENGER_CAR"] = PhpString(Get(baseRow, "PASSENGER_CAR") ?? title),
            ["power_kw"] = PhpString(Get(baseRow, "POWER_KW") ?? Get(row, "power_kw")),
            ["power_ps"] = PhpString(Get(baseRow, "POWER_PS") ?? Get(row, "power_ps")),
            ["POWER_KW"] = PhpString(Get(baseRow, "POWER_KW") ?? Get(row, "power_kw")),
            ["POWER_PS"] = PhpString(Get(baseRow, "POWER_PS") ?? Get(row, "power_ps")),
            ["fuel_type"] = PhpString(Get(baseRow, "FUEL_TYPE") ?? Get(row, "fuel_type")),
            ["FUEL_TYPE"] = PhpString(Get(baseRow, "FUEL_TYPE") ?? Get(row, "fuel_type")),
            ["engine_type"] = PhpString(Get(baseRow, "ENGINE_TYPE") ?? Get(row, "engine_type")),
            ["ENGINE_TYPE"] = PhpString(Get(baseRow, "ENGINE_TYPE") ?? Get(row, "engine_type")),
            ["engine_code"] = PhpString(Get(baseRow, "ENGINE_CODE") ?? Get(baseRow, "ENG_CODE") ?? Get(row, "engine_code")),
            ["ENGINE_CODE"] = PhpString(Get(baseRow, "ENGINE_CODE") ?? Get(baseRow, "ENG_CODE") ?? Get(row, "engine_code")),
            ["drive_type"] = PhpString(Get(baseRow, "DRIVE_TYPE") ?? Get(row, "drive_type")),
            ["DRIVE_TYPE"] = PhpString(Get(baseRow, "DRIVE_TYPE") ?? Get(row, "drive_type")),
            ["capacity_lt"] = PhpString(Get(baseRow, "CAPACITY_LT") ?? Get(row, "capacity_lt")),
            ["CAPACITY_LT"] = PhpString(Get(baseRow, "CAPACITY_LT") ?? Get(row, "capacity_lt")),
            ["year_from"] = PhpString(Get(baseRow, "CI_FROM") ?? Get(row, "year_from")),
            ["year_to"] = PhpString(Get(baseRow, "CI_TO") ?? Get(row, "year_to")),
            ["CI_FROM"] = PhpString(Get(baseRow, "CI_FROM") ?? Get(row, "year_from")),
            ["CI_TO"] = PhpString(Get(baseRow, "CI_TO") ?? Get(row, "year_to")),
        };
    }

    public static string EpcCataProviderOrigin(string provider)
    {
        provider = EpcCataNormalizeProvider(provider);
        return provider is "local_tecdoc" or "eparts_api" or "umapi" ? "inside" : "outside";
    }

    public static List<object> EpcCataLegacyBrowseCounts() => [];

    public static List<object> EpcCataLegacyVinCounts() => [];

    public static string EpcCataSettingsTable() => "epc_cata_settings";

    public static bool EpcCataSettingsEnsure() => EpcCataDb() is not null;

    public static Dictionary<string, object?> EpcCataSettingsCacheRef() => SettingsCache;

    public static void EpcCataSettingForget(string key)
        => SettingsCache.Remove(key);

    public static object? EpcCataSettingGet(string key, object? defaultValue = null)
    {
        if (SettingsCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        return defaultValue;
    }

    public static bool EpcCataSettingSet(string key, object? value)
        => false;

    public static string EpcCataStorefrontUrl()
    {
        var lang = "";
        if (!PhpEmpty(LangHref))
        {
            lang = LangHref.TrimEnd('/');
        }

        return (lang != "" ? lang : "/en") + "/eparts-cata";
    }

    public static Dictionary<string, object?> EpcCataPresentationDefaults()
        => PhpPlanQ1HintData.PresentationDefaults();

    public static Dictionary<string, object?> EpcCataPresentationConfig()
    {
        var raw = EpcCataSettingGet("presentation", new Dictionary<string, object?>(StringComparer.Ordinal)) as Dictionary<string, object?> ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var cfg = new Dictionary<string, object?>(EpcCataPresentationDefaults(), StringComparer.Ordinal);
        foreach (var kv in raw)
        {
            cfg[kv.Key] = kv.Value;
        }

        var sections = Get(cfg, "sections_enabled") is Dictionary<string, object?> sec
            ? new Dictionary<string, object?>(sec, StringComparer.Ordinal)
            : new Dictionary<string, object?>(StringComparer.Ordinal);
        var defaults = (Dictionary<string, object?>)EpcCataPresentationDefaults()["sections_enabled"]!;
        foreach (var (key, on) in defaults)
        {
            sections[key] = sections.ContainsKey(key) ? !PhpEmpty(sections[key]) : on;
        }

        cfg["sections_enabled"] = sections;
        var disabled = Get(cfg, "disabled_category_ids") is IEnumerable<object?> dlist
            ? dlist.Select(PhpInt).Where(x => x != 0).Distinct().Select(x => (object)x).ToList()
            : new List<object>();
        cfg["disabled_category_ids"] = disabled;
        foreach (var boolKey in new[] { "show_category_icons", "warehouse_only_prices", "enable_vin_search", "enable_plate_search" })
        {
            cfg[boolKey] = !PhpEmpty(Get(cfg, boolKey));
        }

        var view = PhpString(Get(cfg, "default_article_view") ?? "list").ToLowerInvariant();
        if (view is not ("list" or "card" or "compact"))
        {
            view = "list";
        }

        cfg["default_article_view"] = view;
        var section = PhpString(Get(cfg, "default_section") ?? "passenger").ToLowerInvariant();
        if (section is not ("passenger" or "commercial" or "motorbike"))
        {
            section = "passenger";
        }

        if (PhpEmpty(Get(sections, section)))
        {
            foreach (var (secKey, secOn) in sections)
            {
                if (!PhpEmpty(secOn))
                {
                    section = secKey;
                    break;
                }
            }
        }

        cfg["default_section"] = section;
        return cfg;
    }

    public static string EpcCataDefaultSubcategoriesJsonPath()
        => DocumentRoot + "/content/general_pages/epc_cata_default_subcategories.json";

    public static Dictionary<string, List<Dictionary<string, object?>>> EpcCataDefaultSubcategoriesCached()
        => new(StringComparer.Ordinal);

    public static List<Dictionary<string, object?>> EpcCataAttachSubcategories(List<Dictionary<string, object?>> categories)
    {
        var subMap = EpcCataDefaultSubcategoriesCached();
        if (subMap.Count == 0)
        {
            return categories;
        }

        foreach (var row in categories)
        {
            var id = PhpInt(Get(row, "STR_ID") ?? Get(row, "CATEGORY_ID")).ToString();
            if (id != "0" && subMap.TryGetValue(id, out var kids) && kids.Count > 0)
            {
                row["children"] = kids;
            }
        }

        return categories;
    }

    public static string EpcCataCategoryConfigSettingKey() => "storefront_categories";

    public static string EpcCataCategoryIconDir()
        => DocumentRoot + "/content/files/epc-cata/category-icons/";

    public static List<int> EpcCataCategoryIconsAvailable() => [];

    public static Dictionary<string, object?>? EpcCataCategoryConfigRaw()
    {
        var raw = EpcCataSettingGet(EpcCataCategoryConfigSettingKey());
        if (raw is not Dictionary<string, object?> map ||
            !map.TryGetValue("categories", out var cats) ||
            PhpEmpty(cats) ||
            cats is not IEnumerable<object?>)
        {
            return null;
        }

        return map;
    }

    public static List<Dictionary<string, object?>> EpcCataCategoryConfigNormalizeSubcategories(object? subs)
    {
        if (subs is not IEnumerable<object?> list)
        {
            return [];
        }

        var outRows = new List<Dictionary<string, object?>>();
        foreach (var item in list)
        {
            if (item is not Dictionary<string, object?> sub)
            {
                continue;
            }

            var name = PhpString(Get(sub, "name") ?? Get(sub, "CATEGORY_NAME")).Trim();
            if (name == "")
            {
                continue;
            }

            var subId = PhpString(Get(sub, "STR_ID") ?? Get(sub, "id") ?? Get(sub, "CATEGORY_ID")).Trim();
            if (subId == "")
            {
                subId = (outRows.Count + 1).ToString();
            }

            var entry = new Dictionary<string, object?>(StringComparer.Ordinal) { ["STR_ID"] = subId, ["name"] = name };
            var filter = PhpString(Get(sub, "product_filter")).Trim();
            if (filter != "")
            {
                entry["product_filter"] = filter;
            }

            outRows.Add(entry);
        }

        return outRows;
    }

    public static Dictionary<string, object?>? EpcCataCategoryConfigNormalizeEntry(Dictionary<string, object?> row, int fallbackOrder = 0)
    {
        var strId = PhpInt(Get(row, "STR_ID") ?? Get(row, "str_id") ?? Get(row, "CATEGORY_ID") ?? Get(row, "id"));
        var defaultName = EpcCataSanitizeCategoryName(PhpString(Get(row, "default_name") ?? Get(row, "CATEGORY_NAME") ?? Get(row, "name")), strId);
        if (defaultName == "" && strId > 0)
        {
            foreach (var def in EpcCataDefaultCategoriesCached())
            {
                if (PhpInt(Get(def, "STR_ID")) == strId)
                {
                    defaultName = EpcCataSanitizeCategoryName(PhpString(Get(def, "CATEGORY_NAME")), strId);
                    break;
                }
            }
        }

        var displayName = PhpString(Get(row, "display_name")).Trim();
        var name = displayName != "" ? EpcCataSanitizeCategoryName(displayName, strId) : defaultName;
        if (strId <= 0 || name == "")
        {
            return null;
        }

        var iconId = PhpInt(Get(row, "ICON_ID") ?? Get(row, "icon_id") ?? strId);
        var order = PhpInt(Get(row, "ORDER") ?? Get(row, "order") ?? fallbackOrder);
        var enabled = !row.ContainsKey("enabled") || !PhpEmpty(row["enabled"]);
        var entry = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["STR_ID"] = strId,
            ["CATEGORY_ID"] = strId,
            ["ICON_ID"] = iconId > 0 ? iconId : strId,
            ["CATEGORY_NAME"] = name,
            ["default_name"] = defaultName != "" ? defaultName : name,
            ["ORDER"] = order > 0 ? order : (fallbackOrder > 0 ? fallbackOrder : 1),
            ["enabled"] = enabled,
            ["catalog_target"] = EpcCataNormalizeCatalogTarget(PhpString(Get(row, "catalog_target") ?? "eparts-mod")),
            ["subcategories"] = EpcCataCategoryConfigNormalizeSubcategories(Get(row, "subcategories")),
            ["source"] = PhpString(Get(row, "source") ?? "cp"),
        };
        if (displayName != "")
        {
            entry["display_name"] = displayName;
        }

        return entry;
    }

    public static List<Dictionary<string, object?>> EpcCataCategoryConfigSeed()
    {
        var defaults = EpcCataDefaultCategoriesCached();
        var disabled = EpcCataPresentationConfig()["disabled_category_ids"] is IEnumerable<object?> d
            ? d.Select(PhpInt).ToHashSet()
            : [];
        var rows = new List<Dictionary<string, object?>>();
        var order = 1;
        foreach (var row in defaults)
        {
            var strId = PhpInt(Get(row, "STR_ID"));
            if (strId <= 0)
            {
                continue;
            }

            var catName = PhpString(Get(row, "CATEGORY_NAME"));
            rows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["STR_ID"] = strId,
                ["CATEGORY_ID"] = strId,
                ["ICON_ID"] = PhpInt(Get(row, "ICON_ID") ?? strId),
                ["CATEGORY_NAME"] = catName,
                ["default_name"] = catName,
                ["display_name"] = "",
                ["ORDER"] = PhpInt(Get(row, "ORDER") ?? order),
                ["enabled"] = !disabled.Contains(strId),
                ["catalog_target"] = "eparts-mod",
                ["tecdoc_pt_id"] = 0,
                ["umapi_category_id"] = "",
                ["subcategories"] = new List<object>(),
                ["source"] = "default_tree",
            });
            order++;
        }

        return rows;
    }

    public static List<Dictionary<string, object?>> EpcCataCategoryConfigRowsForCp()
        => EpcCataCategoryConfigRaw() is null ? EpcCataCategoryConfigSeed() : EpcCataCategoryConfigSeed();

    public static Dictionary<string, List<Dictionary<string, object?>>> EpcCataStorefrontSubcategoriesMap()
        => EpcCataDefaultSubcategoriesCached();

    public static bool EpcCataCategoryConfigSaveBulk(IEnumerable<object?> categories)
        => false;

    public static bool EpcCataCategoryConfigSaveOne(Dictionary<string, object?> row)
        => EpcCataCategoryConfigNormalizeEntry(row) is not null && EpcCataSettingSet(EpcCataCategoryConfigSettingKey(), row);

    public static bool EpcCataCategoryConfigReset()
    {
        EpcCataSettingForget(EpcCataCategoryConfigSettingKey());
        return true;
    }

    public static void EpcCataCategoryConfigSyncPresentationDisabled(IEnumerable<Dictionary<string, object?>> rows)
        => EpcCataSettingSet("presentation", EpcCataPresentationConfig());

    public static void EpcCataCategoryConfigSyncLegacyMappings(IEnumerable<Dictionary<string, object?>> rows)
        => EpcCataSettingSet("category_mappings", new Dictionary<string, object?>(StringComparer.Ordinal));

    public static Dictionary<string, object?> EpcCataCategoryConfigForAjax()
        => new(StringComparer.Ordinal)
        {
            ["categories"] = EpcCataCategoryConfigRowsForCp(),
            ["icons"] = EpcCataCategoryIconsAvailable(),
            ["icon_base"] = "/content/files/epc-cata/category-icons/",
            ["catalog_targets"] = EpcCataCatalogTargets(),
            ["customized"] = EpcCataCategoryConfigRaw() is not null,
            ["storefront_count"] = EpcCataStorefrontCategories().Count,
        };

    public static Dictionary<string, string> EpcCataCatalogTargets()
        => PhpPlanQ1HintData.CatalogTargets();

    public static string EpcCataNormalizeCatalogTarget(string target)
    {
        target = Regex.Replace(target, "[^a-z0-9\\-]", "").ToLowerInvariant().Trim();
        return EpcCataCatalogTargets().ContainsKey(target) ? target : "eparts-mod";
    }

    public static Dictionary<string, Dictionary<string, object?>> EpcCataCategoryMappingsRaw()
        => new(StringComparer.Ordinal);

    public static Dictionary<string, object?> EpcCataCategoryMappingFor(int strId)
        => EpcCataCategoryMappingsRaw().TryGetValue(strId.ToString(), out var row) ? row : new Dictionary<string, object?>(StringComparer.Ordinal);

    public static List<Dictionary<string, object?>> EpcCataApplyCategoryMappings(List<Dictionary<string, object?>> categories)
    {
        var mappings = EpcCataCategoryMappingsRaw();
        if (mappings.Count == 0)
        {
            foreach (var row in categories)
            {
                if (PhpEmpty(Get(row, "catalog_target")))
                {
                    row["catalog_target"] = "eparts-mod";
                }
            }

            return categories;
        }

        return categories;
    }

    public static string EpcCataStorefrontSlug(string name)
    {
        var slug = name.Trim().ToLowerInvariant();
        slug = Regex.Replace(slug, "[^a-z0-9]+", "-");
        return slug.Trim('-');
    }

    public static Dictionary<string, object?>? EpcCataStorefrontResolveCategory(string key, string subKey = "")
    {
        key = key.Trim();
        if (key == "")
        {
            return null;
        }

        var categories = EpcCataStorefrontCategories();
        var keyLower = key.ToLowerInvariant();
        var keySlug = EpcCataStorefrontSlug(key);
        Dictionary<string, object?>? cat = null;
        foreach (var row in categories)
        {
            var strId = PhpInt(Get(row, "STR_ID") ?? Get(row, "CATEGORY_ID")).ToString();
            var name = PhpString(Get(row, "CATEGORY_NAME") ?? Get(row, "name")).Trim();
            var slug = name != "" ? EpcCataStorefrontSlug(name) : "";
            if (strId != "0" && (strId == key || strId == PhpInt(key).ToString()))
            {
                cat = row;
                break;
            }

            if (slug != "" && (slug == keySlug || slug == keyLower))
            {
                cat = row;
                break;
            }

            if (name != "" && name.ToLowerInvariant() == keyLower)
            {
                cat = row;
                break;
            }
        }

        if (cat is null)
        {
            return null;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["category"] = cat,
            ["subcategory"] = null,
        };
    }

    public static string EpcCataStorefrontCategoryHref(string langHref, Dictionary<string, object?> catRow, string subName = "")
    {
        langHref = (langHref != "" ? langHref : "/en").TrimEnd('/');
        var strId = PhpInt(Get(catRow, "STR_ID") ?? Get(catRow, "CATEGORY_ID"));
        var name = PhpString(Get(catRow, "CATEGORY_NAME") ?? Get(catRow, "name"));
        var target = EpcCataNormalizeCatalogTarget(PhpString(Get(catRow, "catalog_target") ?? "eparts-mod"));
        if (target == "parts-search")
        {
            var q = new List<string> { "group=" + Enc(name) };
            if (subName != "")
            {
                q.Add("term=" + Enc(subName));
            }

            var ptId = PhpInt(Get(catRow, "tecdoc_pt_id"));
            if (ptId > 0)
            {
                q.Add("pt_id=" + Enc(ptId.ToString()));
            }

            return langHref + "/shop/part_search?" + string.Join("&", q);
        }

        var baseUrl = target == "eparts-cata" ? langHref + "/eparts-cata" : langHref + "/eparts-mod";
        var href = baseUrl + "?category=" + Enc(strId.ToString()) + "&category_name=" + Enc(name);
        if (subName != "")
        {
            href += "&subcategory_name=" + Enc(subName);
        }

        return href;
    }

    public static List<Dictionary<string, object?>> EpcCataStorefrontCategories()
    {
        if (EpcCataCategoryConfigRaw() is not null)
        {
            return [];
        }

        var rows = EpcCataApplyCategoryMappings(EpcCataAttachSubcategories(CloneRows(EpcCataDefaultCategoriesCached())));
        rows.Sort(CompareOrderThenId);
        var disabled = EpcCataPresentationConfig()["disabled_category_ids"] is IEnumerable<object?> d
            ? d.Select(PhpInt).ToHashSet()
            : [];
        if (disabled.Count == 0)
        {
            return rows;
        }

        return rows.Where(row =>
        {
            var id = PhpInt(Get(row, "STR_ID") ?? Get(row, "CATEGORY_ID"));
            return id > 0 && !disabled.Contains(id);
        }).ToList();
    }

    public static Dictionary<string, object?> EpcCataPresentationJsConfigLight()
    {
        var p = EpcCataPresentationDefaults();
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["defaultSection"] = PhpString(Get(p, "default_section")),
            ["defaultArticleView"] = PhpString(Get(p, "default_article_view")),
            ["showCategoryIcons"] = !PhpEmpty(Get(p, "show_category_icons")),
            ["warehouseOnlyPrices"] = !PhpEmpty(Get(p, "warehouse_only_prices")),
            ["enableVinSearch"] = !PhpEmpty(Get(p, "enable_vin_search")),
            ["enablePlateSearch"] = !PhpEmpty(Get(p, "enable_plate_search")),
            ["sectionsEnabled"] = Get(p, "sections_enabled"),
            ["categories"] = new List<object>(),
            ["subcategoriesMap"] = new List<object>(),
            ["catalogTargets"] = EpcCataCatalogTargets(),
            ["categoryIconBase"] = "/content/files/epc-cata/category-icons/",
            ["version"] = EpcCataVersion,
            ["categoriesDeferred"] = true,
        };
    }

    public static Dictionary<string, object?> EpcCataPresentationJsConfig()
        => EpcCataPresentationJsConfigLight();

    public static Dictionary<string, object?> CatDigest(IEnumerable<Dictionary<string, object?>> rows)
    {
        var list = rows.ToList();
        var names = list.Select(r => PhpString(Get(r, "CATEGORY_NAME"))).ToList();
        var ids = list.Select(r => PhpInt(Get(r, "STR_ID"))).ToList();
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["count"] = names.Count,
            ["names"] = names,
            ["ids"] = ids,
            ["first"] = list.Count > 0 ? list[0] : null,
            ["last"] = list.Count > 0 ? list[^1] : null,
        };
    }

    public static Dictionary<string, object?> MonitorStable(Dictionary<string, object?> mon)
    {
        var keys = new List<string>();
        var pct = new List<object>();
        var phase = new List<string>();
        if (mon.TryGetValue("datasets", out var ds) && ds is IEnumerable<Dictionary<string, object?>> rows)
        {
            foreach (var row in rows)
            {
                keys.Add(PhpString(Get(row, "key")));
                pct.Add(Get(row, "pct") ?? 0);
                phase.Add(PhpString(Get(row, "phase")));
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = !PhpEmpty(Get(mon, "ok")) ? 1 : 0,
            ["rows"] = Get(mon, "rows"),
            ["stock_note"] = PhpString(Get(mon, "stock_note")),
            ["dataset_keys"] = keys,
            ["dataset_pct"] = pct,
            ["dataset_phase"] = phase,
        };
    }

    public static bool PathEndsWith(string path, string suffix)
        => path.EndsWith(suffix, StringComparison.Ordinal);

    private static Dictionary<string, object?> Ds(string key, object pct, string phase)
        => new(StringComparer.Ordinal) { ["key"] = key, ["pct"] = pct, ["phase"] = phase };

    private static string Enc(string value)
        => Uri.EscapeDataString(value);

    private static List<Dictionary<string, object?>> CloneRows(IEnumerable<Dictionary<string, object?>> rows)
        => rows.Select(r => new Dictionary<string, object?>(r, StringComparer.Ordinal)).ToList();

    private static int CompareOrderThenId(Dictionary<string, object?> a, Dictionary<string, object?> b)
    {
        var ao = PhpInt(Get(a, "ORDER"));
        var bo = PhpInt(Get(b, "ORDER"));
        if (ao != bo)
        {
            return ao.CompareTo(bo);
        }

        return PhpInt(Get(a, "STR_ID") ?? Get(a, "CATEGORY_ID")).CompareTo(PhpInt(Get(b, "STR_ID") ?? Get(b, "CATEGORY_ID")));
    }

    private static object? Get(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var v) ? v : null;

    private static object? JsonBox(JsonElement el)
        => el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number when el.TryGetInt32(out var i) => i,
            JsonValueKind.Number => el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => el.GetRawText()
        };

    private static int PhpInt(object? value)
    {
        if (value is null || value is bool)
        {
            return value is true ? 1 : 0;
        }

        if (value is int i)
        {
            return i;
        }

        if (value is long l)
        {
            return (int)l;
        }

        if (value is double d)
        {
            return (int)d;
        }

        return int.TryParse(Convert.ToString(value), out var parsed) ? parsed : 0;
    }

    private static string PhpString(object? value)
        => value is null ? "" : Convert.ToString(value) ?? "";

    public static bool PhpEmpty(object? value)
        => value switch
        {
            null => true,
            bool b => !b,
            string s => s is "" or "0",
            int i => i == 0,
            long l => l == 0,
            double d => d == 0,
            float f => f == 0,
            decimal m => m == 0,
            System.Collections.ICollection c => c.Count == 0,
            _ => false
        };
}
