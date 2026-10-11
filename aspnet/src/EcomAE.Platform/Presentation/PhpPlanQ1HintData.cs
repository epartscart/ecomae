namespace EcomAE.Platform.Presentation;

/// <summary>
/// Static catalogs for <c>content/general_pages/epc_cata_config.php</c>.
/// PHP identifiers: <c>epc_cata_providers</c>, <c>epc_cata_default_categories</c>,
/// <c>epc_cata_tables</c>, <c>epc_cata_catalog_targets</c>,
/// <c>epc_cata_presentation_defaults</c>.
/// </summary>
public static class PhpPlanQ1HintData
{
    public static Dictionary<string, Dictionary<string, object?>> Providers()
        => new(StringComparer.Ordinal)
        {
            ["umapi"] = Prov("UMAPI", "/api/umapi_proxy.php", "epc_umapi", "epc_umapi_modifications", "merge_legacy"),
            ["local_tecdoc"] = Prov("TecDoc 2017 (local)", "", "td_", "", "tecdoc_import"),
            ["eparts_api"] = Prov("EParts API", "/api/partsapi_proxy.php", "epc_partsapi", "epc_partsapi_cars", "merge_legacy"),
            ["carcat"] = Prov("CarCat", "/api/carcat_proxy.php", "", "", "on_demand", legacyEmpty: true),
            ["laximo"] = Prov("Laximo", "", "", "", "on_demand", legacyEmpty: true),
            ["levam"] = Prov("Levam", "", "", "", "on_demand", legacyEmpty: true),
        };

    public static Dictionary<string, string> Tables()
        => new(StringComparer.Ordinal)
        {
            ["manufacturers"] = "epc_cata_manufacturers",
            ["models"] = "epc_cata_models",
            ["modifications"] = "epc_cata_modifications",
            ["categories"] = "epc_cata_categories",
            ["articles"] = "epc_cata_articles",
            ["crosses"] = "epc_cata_crosses",
            ["vin_cache"] = "epc_cata_vin_cache",
            ["fitment"] = "epc_cata_fitment",
            ["sync_status"] = "epc_cata_sync_status",
            ["sync_log"] = "epc_cata_sync_log",
        };

    public static Dictionary<string, string> CatalogTargets()
        => new(StringComparer.Ordinal)
        {
            ["eparts-mod"] = "EParts Mod (vehicle picker → category parts)",
            ["eparts-cata"] = "EParts CATA (unified catalog)",
            ["parts-search"] = "Parts search (keyword, no vehicle)",
        };

    public static Dictionary<string, object?> PresentationDefaults()
        => new(StringComparer.Ordinal)
        {
            ["default_article_view"] = "list",
            ["show_category_icons"] = 1,
            ["warehouse_only_prices"] = 1,
            ["default_section"] = "passenger",
            ["storefront_primary"] = "eparts-mod",
            ["enable_vin_search"] = 1,
            ["enable_plate_search"] = 1,
            ["sections_enabled"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["passenger"] = 1,
                ["commercial"] = 1,
                ["motorbike"] = 1,
            },
            ["disabled_category_ids"] = new List<object>(),
        };

    public static List<Dictionary<string, object?>> DefaultCategories()
        =>
        [
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 1,
                ["STR_ID"] = 2,
                ["CATEGORY_ID"] = 2,
                ["ICON_ID"] = 2,
                ["CATEGORY_NAME"] = "Filters",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 2,
                ["STR_ID"] = 1,
                ["CATEGORY_ID"] = 1,
                ["ICON_ID"] = 1,
                ["CATEGORY_NAME"] = "Service parts",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 3,
                ["STR_ID"] = 4,
                ["CATEGORY_ID"] = 4,
                ["ICON_ID"] = 4,
                ["CATEGORY_NAME"] = "Suspension",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 4,
                ["STR_ID"] = 5,
                ["CATEGORY_ID"] = 5,
                ["ICON_ID"] = 5,
                ["CATEGORY_NAME"] = "Brake System",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 5,
                ["STR_ID"] = 7,
                ["CATEGORY_ID"] = 7,
                ["ICON_ID"] = 7,
                ["CATEGORY_NAME"] = "Damping",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 6,
                ["STR_ID"] = 8,
                ["CATEGORY_ID"] = 8,
                ["ICON_ID"] = 8,
                ["CATEGORY_NAME"] = "Belt Drive",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 7,
                ["STR_ID"] = 15,
                ["CATEGORY_ID"] = 15,
                ["ICON_ID"] = 15,
                ["CATEGORY_NAME"] = "Windscreen Cleaning",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 8,
                ["STR_ID"] = 9,
                ["CATEGORY_ID"] = 9,
                ["ICON_ID"] = 9,
                ["CATEGORY_NAME"] = "Clutch",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 9,
                ["STR_ID"] = 10,
                ["CATEGORY_ID"] = 10,
                ["ICON_ID"] = 10,
                ["CATEGORY_NAME"] = "Ignition",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 10,
                ["STR_ID"] = 3,
                ["CATEGORY_ID"] = 3,
                ["ICON_ID"] = 3,
                ["CATEGORY_NAME"] = "Engine",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 11,
                ["STR_ID"] = 16,
                ["CATEGORY_ID"] = 16,
                ["ICON_ID"] = 16,
                ["CATEGORY_NAME"] = "Wheel Drive",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 12,
                ["STR_ID"] = 11,
                ["CATEGORY_ID"] = 11,
                ["ICON_ID"] = 11,
                ["CATEGORY_NAME"] = "Bodywork",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 13,
                ["STR_ID"] = 12,
                ["CATEGORY_ID"] = 12,
                ["ICON_ID"] = 12,
                ["CATEGORY_NAME"] = "Electrics",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 14,
                ["STR_ID"] = 6,
                ["CATEGORY_ID"] = 6,
                ["ICON_ID"] = 6,
                ["CATEGORY_NAME"] = "Wheels",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 15,
                ["STR_ID"] = 18,
                ["CATEGORY_ID"] = 18,
                ["ICON_ID"] = 18,
                ["CATEGORY_NAME"] = "Steering",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 16,
                ["STR_ID"] = 17,
                ["CATEGORY_ID"] = 17,
                ["ICON_ID"] = 17,
                ["CATEGORY_NAME"] = "Fuel Supply",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 17,
                ["STR_ID"] = 26,
                ["CATEGORY_ID"] = 26,
                ["ICON_ID"] = 26,
                ["CATEGORY_NAME"] = "Fuel Mixture Formation",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 18,
                ["STR_ID"] = 19,
                ["CATEGORY_ID"] = 19,
                ["ICON_ID"] = 19,
                ["CATEGORY_NAME"] = "Cooling",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 19,
                ["STR_ID"] = 20,
                ["CATEGORY_ID"] = 20,
                ["ICON_ID"] = 20,
                ["CATEGORY_NAME"] = "Exhaust",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 20,
                ["STR_ID"] = 24,
                ["CATEGORY_ID"] = 24,
                ["ICON_ID"] = 24,
                ["CATEGORY_NAME"] = "Axle Drive",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 21,
                ["STR_ID"] = 23,
                ["CATEGORY_ID"] = 23,
                ["ICON_ID"] = 23,
                ["CATEGORY_NAME"] = "Heating, Ventilation",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 22,
                ["STR_ID"] = 22,
                ["CATEGORY_ID"] = 22,
                ["ICON_ID"] = 22,
                ["CATEGORY_NAME"] = "Air Conditioning",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 23,
                ["STR_ID"] = 13,
                ["CATEGORY_ID"] = 13,
                ["ICON_ID"] = 13,
                ["CATEGORY_NAME"] = "Manual Transmission",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 24,
                ["STR_ID"] = 14,
                ["CATEGORY_ID"] = 14,
                ["ICON_ID"] = 14,
                ["CATEGORY_NAME"] = "Automatic Transmission",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 25,
                ["STR_ID"] = 872,
                ["CATEGORY_ID"] = 872,
                ["ICON_ID"] = 872,
                ["CATEGORY_NAME"] = "Auto Chemicals",
                ["source"] = "default_tree",
            },
            new(StringComparer.Ordinal)
            {
                ["ORDER"] = 26,
                ["STR_ID"] = 34,
                ["CATEGORY_ID"] = 34,
                ["ICON_ID"] = 34,
                ["CATEGORY_NAME"] = "Electric Drive",
                ["source"] = "default_tree",
            }
        ];

    private static Dictionary<string, object?> Prov(
        string label,
        string proxy,
        string prefix,
        string modTable,
        string sync,
        bool legacyEmpty = false)
        => new(StringComparer.Ordinal)
        {
            ["label"] = label,
            ["proxy"] = proxy,
            ["legacy_tables"] = legacyEmpty
                ? new Dictionary<string, object?>(StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["prefix"] = prefix,
                    ["mod_table"] = modTable,
                },
            ["enabled"] = true,
            ["sync_mode"] = sync,
        };
}
