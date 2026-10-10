using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-lock helpers. PHP identifiers kept for the inventory:
/// <c>epc_industry_groups</c>, <c>epc_industry_resolve_group</c>,
/// <c>epc_industry_get_group</c>, <c>epc_industry_template_key</c>,
/// <c>epc_industry_tenant_sub_areas</c>, <c>epc_industry_save_tenant_sub_areas</c>,
/// <c>epc_industry_ensure_config_schema</c>, <c>epc_industry_consolidation_stats</c>,
/// <c>epc_industry_route_template</c>, <c>epc_industry_hero_config</c>,
/// <c>epc_industry_filter_categories_by_sub_areas</c>,
/// <c>epc_industry_erp_modules_for_sub_areas</c>,
/// <c>epc_industry_cp_sections_for_sub_areas</c>,
/// <c>epc_industry_consolidation_savings</c>.
/// </summary>
public static class PhpPlanQ1Lock
{
    public const string ConsolidationPath = "content/general_pages/epc_industry_consolidation.php";
    public const string TemplateRouterPath = "content/general_pages/epc_industry_template_router.php";

    private static readonly Regex CodeKeep = new("[^a-z0-9_]", RegexOptions.CultureInvariant);
    private static readonly (string Group, string[] Keywords)[] ResolveRules =
    [
        ("automotive", ["auto_", "auto ", "car ", "vehicle", "motor", "tyre", "tire ", "driving school", "garage", "automotive", "fleet", "tarpaulin", "spare part", "motorcycl"]),
        ("healthcare_medical", ["supplement", "nutrition", "vitamin", "nutraceut", "protein powder", "herbal product", "pharmacy", "drugstore", "medical", "health", "hospital", "clinic", "pharma", "dental", "doctor", "nurse", "veterinar", "optical", "ambulance", "physiotherapy", "mental health", "fertility", "addiction", "urology", "oncology", "cardio", "ortho", "derma", "pediatr", "chiropr", "acupuncture", "ayurveda", "homeopath", "rehabilitation", "diagnostic", "patholog", "radiology", "social work"]),
        ("food_beverage", ["restaurant", "food", "bakery", "cafe", "coffee", "catering", "kitchen", "butcher", "confection", "ice cream", "juice", "beverage", "brewery", "winery", "bar &", "pub", "bbq", "smokehouse", "pizza", "sushi", "cheese", "yogurt", "cream of milk", "meat process", "fish canning", "grain", "rice ", "bread", "foodstuff", "packaging food", "halal"]),
        ("fashion_apparel", ["fashion", "apparel", "clothing", "garment", "textile", "footwear", "shoe", "tailoring", "bridal", "swimwear", "sportswear", "uniform", "wig", "gown", "handbag", "leather"]),
        ("jewellery_luxury", ["jeweller", "jewelry", "diamond", "gold ", "watch ", "watches", "gemstone", "luxury goods", "precious", "hallmark", "karat", "carat"]),
        ("electronics_technology", ["electronic", "computer", "laptop", "mobile phone", "gadget", "gaming", "audio", "tv ", "television", "appliance", "typewriter", "semiconductor", "circuit"]),
        ("construction_realestate", ["construction", "real estate", "property", "architect", "interior design", "plumb", "electrical contract", "painting", "landscap", "demolition", "survey", "steel fabricat", "concrete", "waterproof", "insulation", "flooring", "roofing", "tunnel", "pipeline", "road ", "bridge", "contracting", "building", "civil engineer", "hvac", "fit-out", "marine construct"]),
        ("manufacturing_industrial", ["manufactur", "factory", "production", "assembly", "chemical", "plastic", "rubber", "metal", "steel ", "glass ", "ceramic", "pottery", "paper", "pulp", "3d print", "woodwork", "carpentry", "foundry", "extrusion", "welding", "fabricat", "adhesive", "varnish", "aluminum", "wire ", "valve", "casting", "machinery", "equipment mfg", "aircraft", "handicraft", "mill"]),
        ("professional_services", ["consulting", "consultancy", "law firm", "legal", "accounting firm", "audit firm", "tax ", "bookkeep", "hr ", "recruitment", "staffing", "marketing agency", "advertising agency", "pr agency", "translation", "research", "advisor", "notary", "trademark", "patent", "classification", "certification", "inspection", "testing lab", "quality"]),
        ("education_training", ["school", "university", "college", "education", "training", "tutor", "coaching", "learning", "academy", "certification", "vocational", "e-learning", "language institute"]),
        ("hospitality_travel", ["hotel", "resort", "hostel", "travel", "tourism", "tour operator", "airline", "cruise", "camping", "glamping", "bed & breakfast", "vacation", "airport", "accommodation"]),
        ("beauty_wellness", ["beauty", "salon", "spa", "barber", "nail ", "skincare", "cosmetic", "perfume", "tattoo", "grooming", "wellness clinic", "wellness retreat"]),
        ("home_living", ["furniture", "home & living", "home living", "interiors", "mattress", "bedding", "curtain", "lighting", "kitchen &", "bath", "garden", "storage", "art supply", "sofa", "furnishing"]),
        ("pet_animal", ["pet shop", "pet ", "animal", "vet ", "dog ", "cat ", "aquarium", "equestrian", "shelter"]),
        ("agriculture_farming", ["agricultur", "farm", "crop", "livestock", "poultry", "aquaculture", "fishery", "dairy", "organic farm", "vertical farm", "seeds", "fertilizer", "beehive", "honey", "wheat", "vegetable farm", "growing of", "fishing", "palm", "fruit tree", "citrus", "cereal"]),
        ("logistics_transport", ["logistic", "freight", "shipping", "courier", "warehouse", "3pl", "cargo", "trucking", "transport", "moving", "relocation", "delivery", "customs broker", "cold chain", "postal", "storage"]),
        ("energy_utilities", ["energy", "solar", "wind ", "oil &", "gas ", "power ", "utility", "nuclear", "biomass", "mining", "uranium", "battery ", "quarry", "extraction", "crude oil", "natural gas", "electricity", "water supply", "renewable"]),
        ("financial_services", ["bank", "insurance", "fintech", "wealth", "investment", "venture capital", "lending", "forex", "payment", "crypto", "asset management", "collection agency", "financial intermediat", "exchange", "money transfer", "islamic finance", "reinsurance"]),
        ("it_software", ["software", "saas", "web design", "web develop", "app develop", "cybersecurity", "data analy", "ai &", "machine learn", "iot", "blockchain", "devops", "cloud ", "voip", "it ", "telecom"]),
        ("media_entertainment", ["film", "video production", "music", "publishing", "photography", "animation", "streaming", "broadcast", "radio", "podcast", "youtube", "theater", "event", "amusement", "theme park", "entertainment", "cinema"]),
        ("sports_fitness", ["gym", "fitness", "sport", "yoga", "martial", "swimming", "golf", "tennis", "cycling", "bicycle", "trampoline", "esport", "recreation"]),
        ("wholesale_trading", ["wholesale", "trading", "import", "export", "distribution", "commodity", "fmcg", "b2b", "liquidat", "general trading"]),
        ("rental_leasing", ["rental", "leasing", "hire", "charter", "co-working", "self-storage", "renting"]),
        ("nonprofit_government", ["non-profit", "nonprofit", "ngo", "charity", "government", "public sector", "social enterprise", "religious", "church", "mosque", "temple", "community center", "library", "museum", "volunteer", "advocacy", "voter", "extra territorial", "international org"]),
        ("cleaning_maintenance", ["cleaning", "pest control", "waste", "recycling", "janitorial", "laundry", "dry clean", "carpet clean", "window clean", "pool maintenance"]),
        ("printing_signage", ["printing", "signage", "sign ", "banner", "label", "embroidery", "engraving"]),
        ("security_safety", ["security", "cctv", "surveillance", "fire safety", "alarm", "guard", "access control", "investigation"]),
        ("retail_ecommerce", ["retail", "supermarket", "grocery", "convenience store", "department store", "marketplace", "gift shop", "toy store", "bookstore", "stationery", "home decor", "antique", "thrift", "vending", "e-commerce", "ecommerce"])
    ];

    private static readonly Dictionary<string, string> BadgeMap = new(StringComparer.Ordinal)
    {
        ["commerce"] = "retail_ecommerce",
        ["services"] = "professional_services",
        ["industrial"] = "manufacturing_industrial",
        ["supply"] = "wholesale_trading",
        ["healthcare"] = "healthcare_medical",
        ["technology"] = "it_software",
        ["hospitality"] = "hospitality_travel",
        ["specialized"] = "professional_services"
    };

    public static string DocumentRoot { get; set; } = "";
    public static Func<Dictionary<string, string>>? PortalGroupMap { get; set; }
    private static bool _schemaDone;
    private static Dictionary<string, Dictionary<string, object?>>? _groups;

    public static void Reset()
    {
        DocumentRoot = "";
        PortalGroupMap = null;
        _schemaDone = false;
    }

    public sealed class ConfigStore
    {
        public bool TableReady { get; set; }
        public bool ThrowOnRead { get; set; }
        public Dictionary<string, ConfigRow> Rows { get; } = new(StringComparer.Ordinal);
    }

    public sealed class ConfigRow
    {
        public string SiteKey { get; set; } = "";
        public string IndustryCode { get; set; } = "";
        public string GroupKey { get; set; } = "";
        public string SubAreasJson { get; set; } = "{}";
    }

    public static Dictionary<string, Dictionary<string, object?>> EpcIndustryGroups()
    {
        if (_groups is not null)
        {
            return CloneGroups(_groups);
        }

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EcomAE.Platform.Presentation.PlanQ1LockGroups.json")
            ?? throw new InvalidOperationException("missing groups resource");
        using var doc = JsonDocument.Parse(stream);
        var parsed = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            parsed[prop.Name] = (Dictionary<string, object?>)FromJson(prop.Value)!;
        }

        _groups = parsed;
        return CloneGroups(parsed);
    }

    public static string EpcIndustryResolveGroup(string industryName, string badgeCategory = "")
    {
        var name = industryName.Trim().ToLowerInvariant();
        var groups = EpcIndustryGroups();
        var code = CodeKeep.Replace(name, "");
        var map = PortalGroupMap?.Invoke();
        if (code != "" && map is not null && map.TryGetValue(code, out var mapped) && groups.ContainsKey(mapped))
        {
            return mapped;
        }

        foreach (var (group, keywords) in ResolveRules)
        {
            foreach (var keyword in keywords)
            {
                if (name.Contains(keyword, StringComparison.Ordinal))
                {
                    return group;
                }
            }
        }

        var cat = badgeCategory.Trim().ToLowerInvariant();
        return BadgeMap.TryGetValue(cat, out var badge) ? badge : "retail_ecommerce";
    }

    public static Dictionary<string, object?> EpcIndustryGetGroup(string industryIdentifier, string badgeCategory = "")
    {
        var groupKey = EpcIndustryResolveGroup(industryIdentifier, badgeCategory);
        var groups = EpcIndustryGroups();
        return groups.TryGetValue(groupKey, out var group) ? group : groups["retail_ecommerce"];
    }

    public static string EpcIndustryTemplateKey(string industryIdentifier, string badgeCategory = "")
    {
        var group = EpcIndustryGetGroup(industryIdentifier, badgeCategory);
        return Str(group, "template_key", "retail");
    }

    public static Dictionary<string, object?> EpcIndustryTenantSubAreas(ConfigStore db, string siteKey, string industryCode)
    {
        var group = EpcIndustryGetGroup(industryCode);
        var allAreas = group.TryGetValue("available_sub_areas", out var raw) && raw is Dictionary<string, object?> areas
            ? areas
            : new Dictionary<string, object?>(StringComparer.Ordinal);
        var defaults = group.TryGetValue("default_sub_areas", out var defRaw) && defRaw is List<object?> defList
            ? defList.Select(v => Convert.ToString(v) ?? "").ToList()
            : [];

        Dictionary<string, object?>? saved = null;
        try
        {
            if (!db.ThrowOnRead && db.Rows.TryGetValue(siteKey, out var row) && !PhpEmpty(row.SubAreasJson))
            {
                using var doc = JsonDocument.Parse(row.SubAreasJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    saved = (Dictionary<string, object?>)FromJson(doc.RootElement)!;
                }
            }
        }
        catch
        {
            saved = null;
        }

        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var key in allAreas.Keys)
        {
            result[key] = saved is not null
                ? !PhpEmpty(saved.TryGetValue(key, out var hit) ? hit : null)
                : defaults.Contains(key, StringComparer.Ordinal);
        }

        return result;
    }

    public static bool EpcIndustrySaveTenantSubAreas(ConfigStore db, string siteKey, string industryCode, string groupKey, Dictionary<string, object?> subAreas)
    {
        EpcIndustryEnsureConfigSchema(db);
        var json = JsonSerializer.Serialize(subAreas);
        db.Rows[siteKey] = new ConfigRow
        {
            SiteKey = siteKey,
            IndustryCode = industryCode,
            GroupKey = groupKey,
            SubAreasJson = json
        };
        return true;
    }

    public static void EpcIndustryEnsureConfigSchema(ConfigStore db)
    {
        if (_schemaDone)
        {
            return;
        }

        db.TableReady = true;
        _schemaDone = true;
    }

    public static Dictionary<string, object?> EpcIndustryConsolidationStats(IEnumerable<object?> allIndustries)
    {
        var groups = EpcIndustryGroups();
        var stats = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, group) in groups)
        {
            stats[key] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["label"] = Str(group, "label"),
                ["count"] = 0,
                ["industries"] = new List<object?>()
            };
        }

        foreach (var ind in allIndustries)
        {
            string name;
            var badge = "";
            if (ind is Dictionary<string, object?> row)
            {
                name = Str(row, "name");
                badge = Str(row, "badge");
            }
            else
            {
                name = Convert.ToString(ind) ?? "";
            }

            var groupKey = EpcIndustryResolveGroup(name, badge);
            if (stats.TryGetValue(groupKey, out var raw) && raw is Dictionary<string, object?> bucket)
            {
                bucket["count"] = Convert.ToInt32(bucket["count"]) + 1;
                ((List<object?>)bucket["industries"]!).Add(name);
            }
        }

        return stats
            .OrderByDescending(kv => Convert.ToInt32(((Dictionary<string, object?>)kv.Value!)["count"]))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
    }

    public static string EpcIndustryRouteTemplate(string industryCode, string layoutVariant = "")
    {
        var group = EpcIndustryGetGroup(industryCode);
        var templateKey = Str(group, "template_key", "retail");
        var root = DocumentRoot.TrimEnd('/', '\\');
        var templateDir = root + "/content/general_pages/industry_templates";
        if (layoutVariant != "")
        {
            var variantFile = templateDir + "/" + templateKey + "_" + layoutVariant + ".php";
            if (File.Exists(variantFile))
            {
                return variantFile;
            }
        }

        var mainFile = templateDir + "/" + templateKey + ".php";
        if (File.Exists(mainFile))
        {
            return mainFile;
        }

        var fallback = root + "/content/general_pages/" + "epc_generic_commerce_package" + ".php";
        return File.Exists(fallback) ? fallback : "/" + "retail" + ".php";
    }

    public static Dictionary<string, object?> EpcIndustryHeroConfig(string industryCode, Dictionary<string, object?>? tenantProfile = null)
    {
        var group = EpcIndustryGetGroup(industryCode);
        var colors = group.TryGetValue("color_scheme", out var raw) && raw is Dictionary<string, object?> scheme
            ? new Dictionary<string, object?>(scheme, StringComparer.Ordinal)
            : new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["primary"] = "#3b82f6",
                ["accent"] = "#60a5fa",
                ["bg_from"] = "#1e3a5f",
                ["bg_to"] = "#1e40af"
            };
        if (tenantProfile is not null
            && tenantProfile.TryGetValue("theme", out var themeRaw)
            && themeRaw is Dictionary<string, object?> theme)
        {
            if (!PhpEmpty(theme.TryGetValue("primary", out var primary) ? primary : null))
            {
                colors["primary"] = primary;
            }

            if (!PhpEmpty(theme.TryGetValue("accent", out var accent) ? accent : null))
            {
                colors["accent"] = accent;
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["style"] = Str(group, "hero_style", "parallax_dark"),
            ["colors"] = colors,
            ["icon"] = Str(group, "icon", "fa-shopping-cart"),
            ["group_label"] = Str(group, "label", "Business"),
            ["template_key"] = Str(group, "template_key", "retail")
        };
    }

    public static List<Dictionary<string, object?>> EpcIndustryFilterCategoriesBySubAreas(
        ConfigStore db, string siteKey, string industryCode, IEnumerable<Dictionary<string, object?>> allCategories)
    {
        var activeAreas = EpcIndustryTenantSubAreas(db, siteKey, industryCode);
        var activeKeys = activeAreas.Where(kv => !PhpEmpty(kv.Value)).Select(kv => kv.Key).ToList();
        var source = allCategories.ToList();
        if (activeKeys.Count == 0)
        {
            return source;
        }

        var filtered = new List<Dictionary<string, object?>>();
        foreach (var cat in source)
        {
            var catArea = Str(cat, "sub_area");
            if (catArea == "" || activeKeys.Contains(catArea, StringComparer.Ordinal))
            {
                filtered.Add(cat);
            }
        }

        return filtered;
    }

    public static List<string> EpcIndustryErpModulesForSubAreas(Dictionary<string, object?> activeAreas, string groupKey)
    {
        var modules = new List<string> { "dashboard", "gl", "ar", "ap", "inventory", "reports", "setup" };
        var groupModules = GroupModules();
        if (!groupModules.TryGetValue(groupKey, out var mapping))
        {
            mapping = new Dictionary<string, string[]>(StringComparer.Ordinal);
        }

        foreach (var (areaKey, isActive) in activeAreas)
        {
            if (!PhpEmpty(isActive) && mapping.TryGetValue(areaKey, out var extra))
            {
                modules.AddRange(extra);
            }
        }

        return UniqueFirst(modules);
    }

    public static List<string> EpcIndustryCpSectionsForSubAreas(Dictionary<string, object?> activeAreas, string groupKey)
    {
        _ = groupKey;
        var sections = new List<string> { "dashboard", "settings", "users", "content", "analytics" };
        foreach (var (areaKey, isActive) in activeAreas)
        {
            if (!PhpEmpty(isActive))
            {
                sections.Add("section_" + areaKey);
            }
        }

        return UniqueFirst(sections);
    }

    public static Dictionary<string, object?> EpcIndustryConsolidationSavings()
    {
        var groups = EpcIndustryGroups();
        const int totalIndustries = 1154;
        var templatesAfter = groups.Count;
        var reductionPct = Math.Round((1 - templatesAfter / (double)totalIndustries) * 100, 1, MidpointRounding.AwayFromZero);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["total_industries"] = totalIndustries,
            ["groups_count"] = templatesAfter,
            ["templates_before"] = totalIndustries,
            ["templates_after"] = templatesAfter,
            ["reduction_pct"] = reductionPct,
            ["php_files_saved"] = totalIndustries * 3 - templatesAfter * 3,
            ["css_files_saved"] = totalIndustries - templatesAfter,
            ["db_queries_saved_per_load"] = (totalIndustries - templatesAfter) * 2,
            ["memory_reduction_est"] = "~" + Math.Round((totalIndustries - templatesAfter) * 0.5, 0, MidpointRounding.AwayFromZero) + " MB less opcache"
        };
    }

    private static Dictionary<string, Dictionary<string, string[]>> GroupModules()
        => new(StringComparer.Ordinal)
        {
            ["automotive"] = new(StringComparer.Ordinal)
            {
                ["parts_catalog"] = ["catalog", "cross_reference", "vin_lookup"],
                ["workshop"] = ["job_card", "labour", "vehicle_history"],
                ["dealership"] = ["vehicle_sales", "test_drive", "trade_in"],
                ["rental"] = ["fleet_booking", "availability", "contracts"]
            },
            ["healthcare_medical"] = new(StringComparer.Ordinal)
            {
                ["clinic"] = ["patients", "appointments", "prescriptions"],
                ["pharmacy"] = ["drug_catalog", "batch_tracking", "dispensing"],
                ["equipment"] = ["asset_register", "maintenance", "calibration"],
                ["laboratory"] = ["samples", "tests", "results"]
            },
            ["food_beverage"] = new(StringComparer.Ordinal)
            {
                ["restaurant"] = ["menu", "table_service", "kitchen_display"],
                ["pos"] = ["pos_terminal", "shift_close", "tender"],
                ["kitchen"] = ["recipes", "ingredients", "wastage"],
                ["delivery"] = ["delivery_zones", "riders", "orders"]
            },
            ["jewellery_luxury"] = new(StringComparer.Ordinal)
            {
                ["retail"] = ["tag_system", "showcase", "certification"],
                ["gold"] = ["gold_rate", "weight_tracking", "purity"],
                ["custom_design"] = ["making_charges", "job_work", "design_catalog"],
                ["wholesale"] = ["consignment", "memo", "bulk_pricing"]
            },
            ["construction_realestate"] = new(StringComparer.Ordinal)
            {
                ["contracting"] = ["boq", "progress_billing", "subcontractors"],
                ["materials"] = ["procurement", "stock_yard", "delivery_challan"],
                ["real_estate"] = ["units", "payment_plans", "handover"]
            },
            ["manufacturing_industrial"] = new(StringComparer.Ordinal)
            {
                ["production"] = ["work_orders", "bom", "routing"],
                ["quality"] = ["qc_checks", "non_conformance", "capa"],
                ["inventory"] = ["raw_materials", "wip", "finished_goods"]
            }
        };

    private static Dictionary<string, Dictionary<string, object?>> CloneGroups(Dictionary<string, Dictionary<string, object?>> src)
    {
        var copy = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var (k, v) in src)
        {
            copy[k] = (Dictionary<string, object?>)Clone(v)!;
        }

        return copy;
    }

    private static object? Clone(object? value)
        => value switch
        {
            Dictionary<string, object?> dict => dict.ToDictionary(kv => kv.Key, kv => Clone(kv.Value), StringComparer.Ordinal),
            List<object?> list => list.Select(Clone).ToList(),
            _ => value
        };

    private static object? FromJson(JsonElement el)
        => el.ValueKind switch
        {
            JsonValueKind.Object => el.EnumerateObject().ToDictionary(p => p.Name, p => FromJson(p.Value), StringComparer.Ordinal),
            JsonValueKind.Array => el.EnumerateArray().Select(FromJson).ToList()!,
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var l) && l is >= int.MinValue and <= int.MaxValue && el.GetRawText().IndexOf('.') < 0
                ? (object)(int)l
                : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };

    private static List<string> UniqueFirst(List<string> items)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var outList = new List<string>();
        foreach (var item in items)
        {
            if (seen.Add(item))
            {
                outList.Add(item);
            }
        }

        return outList;
    }

    private static string Str(Dictionary<string, object?> row, string key, string fallback = "")
        => row.TryGetValue(key, out var value) && value is not null ? Convert.ToString(value) ?? fallback : fallback;

    private static bool PhpEmpty(object? value)
        => value switch
        {
            null => true,
            bool b => !b,
            string s => s == "" || s == "0",
            int i => i == 0,
            long l => l == 0,
            double d => d == 0,
            System.Collections.ICollection c => c.Count == 0,
            _ => false
        };
}
