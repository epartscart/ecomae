using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-after industry live-bridge. PHP identifiers kept for the inventory:
/// <c>epc_portal_industry_live_defs</c>, <c>epc_portal_industry_live_storefront_url</c>,
/// <c>epc_portal_merge_live_subs_into_industry</c>, <c>epc_portal_industry_live_audit</c>.
/// </summary>
public static class PhpPlanQ1After
{
    public const string IndustryLiveBridgePath = "content/general_pages/epc_portal_industry_live_bridge.php";

    private static readonly Lazy<JsonElement> Defs = new(() => JsonDocument.Parse(DefsJson).RootElement.Clone());

    private const string DefsJson = """
{"auto_parts":{"template_key":"automotive","mode":"hub_root"},"electronics":{"template_key":"electronics","mode":"hub_root"},"fashion":{"template_key":"fashion","mode":"hub_root"},"jewellery":{"template_key":"jewellery","mode":"hub_root"},"food_beverage":{"template_key":"food_beverage","mode":"hub_root"},"hospitality_travel":{"template_key":"hospitality","mode":"hub_root"},"education_training":{"template_key":"education","mode":"hub_root"},"logistics_freight":{"template_key":"logistics","mode":"hub_root"},"financial_services":{"template_key":"finance","mode":"hub_root"},"energy_utilities":{"template_key":"energy","mode":"hub_root"},"manufacturing_industrial":{"template_key":"manufacturing","mode":"hub_root"},"media_entertainment":{"template_key":"media","mode":"hub_root"},"printing_signage":{"template_key":"printing","mode":"hub_root"},"nonprofit_government":{"template_key":"nonprofit","mode":"hub_root"},"rental":{"template_key":"rental","mode":"hub_root"},"construction_contracting":{"template_key":"construction","mode":"hub_root"},"agricultural_products":{"template_key":"agriculture","mode":"hub_root"},"automotive_full_vehicles":{"template_key":"automotive","mode":"sub","sub_label":"Vehicle dealership & sales"},"it_hardware_accessories":{"template_key":"it_software","mode":"sub","sub_label":"IT hardware distribution"},"medical":{"template_key":"healthcare","mode":"sub","sub_label":"Medical equipment supply"},"pharmacy_retail":{"template_key":"healthcare","mode":"sub","sub_label":"Pharmacy & drug dispensing"},"grocery_retail":{"template_key":"retail","mode":"sub","sub_label":"Supermarket / grocery"},"furniture_interiors":{"template_key":"home_living","mode":"sub","sub_label":"Furniture retail"},"building_materials":{"template_key":"construction","mode":"sub","sub_label":"Building materials supply"},"fmcg_wholesale":{"template_key":"wholesale","mode":"sub","sub_label":"FMCG distribution"},"industrial_equipment":{"template_key":"manufacturing","mode":"sub","sub_label":"Machinery & equipment"},"perfume_cosmetics":{"template_key":"beauty","mode":"sub","sub_label":"Perfume & fragrances"},"pet_services":{"template_key":"pet","mode":"sub","sub_label":"Pet supply store"},"health":{"template_key":"healthcare","mode":"sub","sub_label":"Wellness & holistic health"},"beauty_skincare":{"template_key":"beauty","mode":"sub","sub_label":"Skincare & facials"},"fitness_training":{"template_key":"sports","mode":"sub","sub_label":"Gym & fitness center"},"clinics_telemedicine":{"template_key":"healthcare","mode":"sub","sub_label":"Telemedicine & telehealth"},"tax_advisory":{"template_key":"professional","mode":"sub","sub_label":"Tax advisory & compliance"},"consultancy":{"template_key":"professional","mode":"sub","sub_label":"Management consulting"},"legal_services":{"template_key":"professional","mode":"sub","sub_label":"Law firm & legal services"},"accounting_auditing":{"template_key":"professional","mode":"sub","sub_label":"Accounting & bookkeeping"},"hr_recruitment":{"template_key":"professional","mode":"sub","sub_label":"HR & recruitment"},"marketing_digital":{"template_key":"it_software","mode":"sub","sub_label":"Digital marketing agency"},"cleaning_facilities":{"template_key":"cleaning","mode":"sub","sub_label":"Commercial cleaning"},"security_services":{"template_key":"security","mode":"sub","sub_label":"Security guarding"},"vehicle_leasing":{"template_key":"rental","mode":"sub","sub_label":"Car & vehicle rental"},"machinery_rental":{"template_key":"rental","mode":"sub","sub_label":"Equipment & tool rental"},"it_services_saas_support":{"template_key":"it_software","mode":"sub","sub_label":"SaaS / cloud platform"},"nutrition_supplements":{"template_key":"healthcare","mode":"inject","sub_label":"Food supplements & nutrition","photo":"https://images.unsplash.com/photo-1550572017-edd951aa8f72?w=1600&q=80","desc":"UAE/GCC food supplement and nutrition shops: vitamins, protein, nutraceuticals, herbal products, batch/expiry tracking, retail POS and e-commerce — DET/DED health-trading aligned.","categories":["Vitamins","Protein & sports nutrition","Herbal & Ayurveda","Weight management","Immunity & wellness","Kids nutrition"],"products":[{"name":"Vitamin D3 Softgels 5000 IU","price":"AED 85","category":"Vitamins","image":"https://images.unsplash.com/photo-1550572017-edd951aa8f72?w=400&q=75"},{"name":"Whey Protein Isolate 2kg","price":"AED 220","category":"Protein & sports nutrition","image":"https://images.unsplash.com/photo-1593095948071-474c5cc2989d?w=400&q=75"},{"name":"Omega-3 Fish Oil Complex","price":"AED 95","category":"Vitamins","image":"https://images.unsplash.com/photo-1584308666744-24d5c474f2ae?w=400&q=75"},{"name":"Immunity Multivitamin Pack","price":"AED 120","category":"Immunity & wellness","image":"https://images.unsplash.com/photo-1471867481825-e8f285119f6d?w=400&q=75"},{"name":"Ashwagandha Herbal Capsules","price":"AED 75","category":"Herbal & Ayurveda","image":"https://images.unsplash.com/photo-1505751172876-fa1923c5c528?w=400&q=75"},{"name":"Kids Gummy Multivitamins","price":"AED 65","category":"Kids nutrition","image":"https://images.unsplash.com/photo-1587854692152-cbe660dbde88?w=400&q=75"}]}}
""";

    private static readonly Dictionary<string, string> HostMap = new(StringComparer.Ordinal)
    {
        ["automotive"] = "automotive",
        ["healthcare"] = "healthcare",
        ["food_beverage"] = "food",
        ["fashion"] = "fashion",
        ["jewellery"] = "jewellery",
        ["electronics"] = "electronics",
        ["construction"] = "construction",
        ["manufacturing"] = "manufacturing",
        ["professional"] = "professional",
        ["education"] = "education",
        ["hospitality"] = "hospitality",
        ["beauty"] = "beauty",
        ["retail"] = "retail",
        ["agriculture"] = "agriculture",
        ["logistics"] = "logistics",
        ["energy"] = "energy",
        ["finance"] = "finance",
        ["it_software"] = "technology",
        ["media"] = "media",
        ["sports"] = "sports",
        ["home_living"] = "homeliving",
        ["wholesale"] = "wholesale",
        ["rental"] = "rental",
        ["nonprofit"] = "nonprofit",
        ["cleaning"] = "cleaning",
        ["pet"] = "pet",
        ["printing"] = "printing",
        ["security"] = "security"
    };

    private static readonly Dictionary<string, string> HostAlias = new(StringComparer.Ordinal)
    {
        ["realestate"] = "construction",
        ["consulting"] = "professional",
        ["legal"] = "professional",
        ["environmental"] = "energy",
        ["telecom"] = "electronics",
        ["government"] = "nonprofit",
        ["aerospace"] = "manufacturing",
        ["mining"] = "manufacturing",
        ["food"] = "food",
        ["technology"] = "technology",
        ["homeliving"] = "homeliving"
    };

    public static JsonElement EpcPortalIndustryLiveDefs() => Defs.Value.Clone();

    public static string EpcPortalIndustryLiveStorefrontUrl(string industryCode, Func<string, string>? primaryHost = null, Func<string, string>? subSlug = null)
    {
        var code = Regex.Replace((industryCode ?? "").ToLowerInvariant(), "[^a-z0-9_]", "");
        if (code.Length == 0 || !Defs.Value.TryGetProperty(code, out var def))
        {
            return "";
        }

        var hostFn = primaryHost ?? IndustryLivePrimaryHost;
        var slugFn = subSlug ?? IndustryLiveSubSlug;
        var host = hostFn(def.GetProperty("template_key").GetString() ?? "");
        if (host.Length == 0)
        {
            return "";
        }

        var mode = def.TryGetProperty("mode", out var modeEl) ? modeEl.GetString() ?? "" : "";
        if (mode == "hub_root")
        {
            return "https://" + host + "/";
        }

        var label = def.TryGetProperty("sub_label", out var labelEl) ? labelEl.GetString() ?? "" : "";
        var slug = label.Length > 0 ? slugFn(label) : "";
        return slug.Length == 0 ? "https://" + host + "/" : "https://" + host + "/" + slug;
    }

    public static Dictionary<string, object?> EpcPortalMergeLiveSubsIntoIndustry(Dictionary<string, object?> industryData, string templateKey)
    {
        var tk = Regex.Replace((templateKey ?? "").ToLowerInvariant(), "[^a-z0-9_]", "");
        if (tk.Length == 0)
        {
            return industryData;
        }

        var subs = ListOfStrings(industryData, "sub_industries");
        var packs = DictOfDicts(industryData, "sub_industry_products");
        foreach (var defProp in Defs.Value.EnumerateObject())
        {
            var def = defProp.Value;
            var defTk = def.TryGetProperty("template_key", out var dtk) ? dtk.GetString() ?? "" : "";
            var mode = def.TryGetProperty("mode", out var dm) ? dm.GetString() ?? "" : "";
            if (defTk != tk || mode != "inject")
            {
                continue;
            }

            var label = def.TryGetProperty("sub_label", out var sl) ? sl.GetString() ?? "" : "";
            if (label.Length == 0)
            {
                continue;
            }

            if (!subs.Contains(label, StringComparer.Ordinal))
            {
                subs.Insert(0, label);
            }

            if (!packs.ContainsKey(label))
            {
                var hero = industryData.TryGetValue("hero_photo", out var hp) && hp is not null
                    ? Convert.ToString(hp, CultureInfo.InvariantCulture) ?? ""
                    : "";
                var photo = def.TryGetProperty("photo", out var ph) ? ph.GetString() ?? hero : hero;
                var desc = def.TryGetProperty("desc", out var ds) && !string.IsNullOrEmpty(ds.GetString())
                    ? ds.GetString()!
                    : label + " operations on ecomae.";
                var categories = def.TryGetProperty("categories", out var cats) && cats.ValueKind == JsonValueKind.Array
                    ? cats.EnumerateArray().Select(c => c.GetString() ?? "").Where(s => s.Length > 0).ToArray()
                    : new[] { "Products", "Services", "Packages", "Accessories", "Support", "Premium" };
                var products = def.TryGetProperty("products", out var prods) && prods.ValueKind == JsonValueKind.Array
                    ? JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(prods.GetRawText()) ?? new()
                    : new List<Dictionary<string, object?>>();
                packs[label] = new Dictionary<string, object?>
                {
                    ["photo"] = photo,
                    ["desc"] = desc,
                    ["categories"] = categories,
                    ["products"] = products
                };
            }
        }

        industryData["sub_industries"] = subs.Distinct(StringComparer.Ordinal).ToList();
        industryData["sub_industry_products"] = packs.Count == 0 ? Array.Empty<object>() : packs;
        return industryData;
    }

    public static Dictionary<string, object?> EpcPortalIndustryLiveAudit(
        IReadOnlyDictionary<string, object?> industries,
        IReadOnlyDictionary<string, string> templateSources,
        Func<string, string>? primaryHost = null,
        Func<string, string>? subSlug = null)
    {
        var ok = new List<string>();
        var broken = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in industries)
        {
            var code = kv.Key;
            if (code is "platform_host" or "erp_standalone")
            {
                continue;
            }

            if (!Defs.Value.TryGetProperty(code, out var def))
            {
                broken[code] = "no live def";
                continue;
            }

            var url = EpcPortalIndustryLiveStorefrontUrl(code, primaryHost, subSlug);
            if (url.Length == 0)
            {
                broken[code] = "empty url";
                continue;
            }

            var mode = def.TryGetProperty("mode", out var dm) ? dm.GetString() ?? "" : "";
            if (mode == "sub")
            {
                var tk = def.GetProperty("template_key").GetString() ?? "";
                templateSources.TryGetValue(tk, out var src);
                src ??= "";
                var label = def.TryGetProperty("sub_label", out var sl) ? sl.GetString() ?? "" : "";
                if (label.Length == 0 || (!src.Contains("'" + label + "'", StringComparison.Ordinal) && !src.Contains("\"" + label + "\"", StringComparison.Ordinal)))
                {
                    broken[code] = "sub label missing in template: " + label;
                    continue;
                }
            }

            ok.Add(code);
        }

        return new Dictionary<string, object?> { ["ok"] = ok, ["broken"] = broken };
    }

    internal static string IndustryLiveSubSlug(string label)
    {
        var s = (label ?? "").Trim().ToLowerInvariant().Replace("&", " ", StringComparison.Ordinal).Replace("+", " ", StringComparison.Ordinal);
        s = Regex.Replace(s, "[^a-z0-9]+", "-");
        if (s.Length == 0)
        {
            s = (label ?? "").Trim().ToLowerInvariant();
        }

        return s.Trim('-');
    }

    internal static string IndustryLivePrimaryHost(string group)
    {
        var slug = HostAlias.TryGetValue(group, out var aliased) ? aliased : group;
        if (HostMap.TryGetValue(slug, out var mapped))
        {
            slug = mapped;
        }

        slug = Regex.Replace((slug ?? "").ToLowerInvariant(), "[^a-z0-9-]", "");
        if (slug.Length == 0)
        {
            slug = "retail";
        }

        return slug + ".ecomae.com";
    }

    private static List<string> ListOfStrings(Dictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var raw) || raw is null)
        {
            return new List<string>();
        }

        if (raw is IEnumerable<string> ss)
        {
            return ss.ToList();
        }

        if (raw is IEnumerable<object?> objs)
        {
            return objs.Select(o => Convert.ToString(o, CultureInfo.InvariantCulture) ?? "").Where(s => s.Length > 0).ToList();
        }

        return new List<string>();
    }

    private static Dictionary<string, Dictionary<string, object?>> DictOfDicts(Dictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var raw) || raw is null)
        {
            return new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        }

        if (raw is Dictionary<string, Dictionary<string, object?>> typed)
        {
            return typed;
        }

        if (raw is Dictionary<string, object?> loose)
        {
            var outDict = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
            foreach (var kv in loose)
            {
                if (kv.Value is Dictionary<string, object?> inner)
                {
                    outDict[kv.Key] = inner;
                }
            }

            return outDict;
        }

        return new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
    }
}
