using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-rest helpers. PHP identifiers kept for the inventory:
/// <c>epc_demand_iso2_to_iso3_map</c>, <c>epc_demand_country_registry</c>,
/// <c>epc_demand_is_stock_pool_country_code</c>, <c>epc_demand_normalize_country_code</c>,
/// <c>epc_demand_parse_country_codes_string</c>, <c>epc_demand_format_countries_display</c>,
/// <c>epc_demand_migrate_country_codes_to_iso3</c>, <c>epc_demand_csv_parse_row</c>,
/// <c>epc_demand_csv_preview_file</c>, <c>epc_demand_csv_import_file</c>,
/// <c>epc_industry_theme</c>, <c>epc_industry_theme_default</c>,
/// <c>epc_industry_theme_registry</c>, <c>epc_erp_industry_kit</c>,
/// <c>epc_cp_industry_alignment</c>,
/// <c>epc_cp_brochure_topic_catalog</c>, <c>epc_cp_brochure_topic_svg_url</c>,
/// <c>epc_cp_brochure_resolve_topic</c>, <c>epc_cp_brochure_assign_unique_photos</c>,
/// <c>epc_cp_brochure_item_topic_photo</c>,
/// <c>epc_portal_theme_template_slot_ids</c>, <c>epc_portal_default_theme_template_by_industry</c>,
/// <c>epc_portal_default_theme_template</c>, <c>epc_portal_theme_palette</c>,
/// <c>epc_portal_theme_style</c>, <c>epc_portal_theme_palette_definitions</c>,
/// <c>epc_portal_industry_style_templates</c>, <c>epc_portal_quartet_from_base_theme</c>,
/// <c>epc_portal_triplet_from_base_theme</c>, <c>epc_portal_style_templates_for_industry</c>,
/// <c>epc_portal_style_template_ids</c>, <c>epc_portal_theme_template_aliases</c>,
/// <c>epc_portal_normalize_theme_template</c>, <c>epc_portal_style_template_theme</c>,
/// <c>epc_portal_style_template_meta</c>, <c>epc_portal_style_templates_for_js</c>,
/// <c>epc_portal_resolve_site_theme</c>,
/// <c>epc_portal_storefront_package_registry</c>, <c>epc_portal_storefront_package_meta</c>,
/// <c>epc_portal_storefront_package_for_industry</c>, <c>epc_portal_resolve_storefront_package</c>,
/// <c>epc_portal_storefront_package_implemented_ids</c>, <c>epc_portal_storefront_package_preset</c>,
/// <c>epc_portal_storefront_packages_for_js</c>, <c>epc_portal_apply_industry_theme_profile</c>.
/// </summary>
public static class PhpPlanQ1Rest
{
    public const string DemandCountryIsoPath = "content/shop/docpart/epc_demand_country_iso.php";
    public const string StorefrontIndustryThemesPath = "content/general_pages/epc_storefront_industry_themes.php";
    public const string CpBrochureTopicPhotosPath = "content/general_pages/epc_cp_brochure_topic_photos.php";
    public const string PortalThemeTemplatesPath = "content/general_pages/epc_portal_theme_templates.php";
    public const string PortalStorefrontPackagesPath = "content/general_pages/epc_portal_storefront_packages.php";

    private static readonly Lazy<JsonElement> Themes = new(() => Parse(PhpPlanQ1RestJson.ThemesJson));
    private static readonly Lazy<JsonElement> Kits = new(() => Parse(PhpPlanQ1RestJson.KitsJson));
    private static readonly Lazy<JsonElement> Align = new(() => Parse(PhpPlanQ1RestJson.AlignJson));
    private static readonly Lazy<JsonElement> Brochure = new(() => Parse(PhpPlanQ1RestJson.BrochureJson));
    private static readonly Lazy<JsonElement> Palettes = new(() => Parse(PhpPlanQ1RestJson.PalettesJson));
    private static readonly Lazy<JsonElement> Defaults = new(() => Parse(PhpPlanQ1RestJson.DefaultsJson));
    private static readonly Lazy<JsonElement> Packages = new(() => Parse(PhpPlanQ1RestJson.PackagesJson));

    private static bool _demandMigrateRan;

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    public static JsonElement EpcDemandIso2ToIso3Map()
    {
        using var doc = JsonDocument.Parse("""{"SD":"SDN","DZ":"DZA","KE":"KEN","AE":"ARE","EG":"EGY","NG":"NGA","SA":"SAU"}""");
        return doc.RootElement.Clone();
    }

    public static JsonElement EpcDemandCountryRegistry()
    {
        using var doc = JsonDocument.Parse("""{"SDN":{"code":"SDN","name":"Sudan","iso2":"SD"},"DZA":{"code":"DZA","name":"Algeria","iso2":"DZ"},"KEN":{"code":"KEN","name":"Kenya","iso2":"KE"},"ARE":{"code":"ARE","name":"United Arab Emirates","iso2":"AE"},"EGY":{"code":"EGY","name":"Egypt","iso2":"EG"},"NGA":{"code":"NGA","name":"Nigeria","iso2":"NG"},"SAU":{"code":"SAU","name":"Saudi Arabia","iso2":"SA"}}""");
        return doc.RootElement.Clone();
    }

    public static bool EpcDemandIsStockPoolCountryCode(string code)
        => code is "ARE" or "AE";

    public static string EpcDemandNormalizeCountryCode(string code)
    {
        code = Regex.Replace(code.Trim(), "[^A-Z]", "");
        code = code.ToUpperInvariant();
        if (code.Length == 0)
        {
            return "";
        }

        var registry = EpcDemandCountryRegistry();
        if (registry.TryGetProperty(code, out _))
        {
            return code;
        }

        if (code.Length == 2)
        {
            var map = EpcDemandIso2ToIso3Map();
            if (map.TryGetProperty(code, out var iso3))
            {
                return iso3.GetString() ?? "";
            }
        }

        return "";
    }

    public static List<string> EpcDemandParseCountryCodesString(string raw)
    {
        raw = raw.Trim();
        if (raw.Length == 0)
        {
            return new List<string>();
        }

        var codes = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var part in Regex.Split(raw, "[,;|/]+"))
        {
            var piece = part.Trim();
            if (piece.Length == 0)
            {
                continue;
            }

            var norm = EpcDemandNormalizeCountryCode(piece);
            if (norm.Length != 0)
            {
                codes[norm] = true;
            }
        }

        var outList = codes.Keys.ToList();
        outList.Sort(StringComparer.Ordinal);
        return outList;
    }

    public static string EpcDemandFormatCountriesDisplay(IEnumerable<object?> codes)
    {
        var clean = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var code in codes)
        {
            var norm = EpcDemandNormalizeCountryCode(Convert.ToString(code, CultureInfo.InvariantCulture) ?? "");
            if (norm.Length != 0)
            {
                clean[norm] = true;
            }
        }

        var list = clean.Keys.ToList();
        list.Sort(StringComparer.Ordinal);
        return string.Join(",", list);
    }

    public static void EpcDemandMigrateCountryCodesToIso3(DemandCountryStore db)
    {
        if (_demandMigrateRan)
        {
            return;
        }

        _demandMigrateRan = true;
        if (db.Countries.Count == 0)
        {
            return;
        }

        var sample = db.Countries[0].Code;
        var needs = sample.Length == 2 || string.Equals(db.CodeType, "char(2)", StringComparison.OrdinalIgnoreCase);
        if (!needs)
        {
            return;
        }

        var order = 0;
        foreach (var meta in EpcDemandCountryRegistry().EnumerateObject())
        {
            order += 10;
            var iso2 = meta.Value.GetProperty("iso2").GetString() ?? "";
            if (iso2.Length == 0)
            {
                continue;
            }

            var existing = db.Countries.Find(r => r.Code == meta.Name);
            if (existing is null)
            {
                db.Countries.Add(new DemandCountryRow { Code = meta.Name, Name = meta.Value.GetProperty("name").GetString() ?? "", SortOrder = order });
            }
            else
            {
                existing.Name = meta.Value.GetProperty("name").GetString() ?? "";
                existing.SortOrder = order;
            }
        }

        var map = EpcDemandIso2ToIso3Map();
        foreach (var pair in map.EnumerateObject())
        {
            foreach (var row in db.Articles)
            {
                if (row.CountryCode == pair.Name)
                {
                    row.CountryCode = pair.Value.GetString() ?? row.CountryCode;
                }
            }

            db.Countries.RemoveAll(r => r.Code == pair.Name);
        }

        db.CodeType = "char(3)";
    }

    public static Dictionary<string, object?> EpcDemandCsvParseRow(IReadOnlyList<string> headerRow, IReadOnlyList<string> dataRow)
    {
        var brand = "";
        var article = "";
        var countries = new List<string>();
        var abc = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var i = 0; i < headerRow.Count; i++)
        {
            var h = headerRow[i].Trim().ToLowerInvariant();
            var val = i < dataRow.Count ? dataRow[i].Trim() : "";
            if (h.Length == 0)
            {
                continue;
            }

            if (h is "brand" or "manufacturer" or "make" or "producer")
            {
                brand = val;
            }
            else if (h is "article" or "article_show" or "part_number" or "part" or "sku" or "number")
            {
                article = val;
            }
            else if (h is "countries" or "demand_countries" or "country_codes" or "demand" or "country")
            {
                countries.AddRange(EpcDemandParseCountryCodesString(val));
            }
            else
            {
                var m = Regex.Match(h, "^country[_\\s-]?([a-z])$");
                if (m.Success)
                {
                    abc[m.Groups[1].Value.ToUpperInvariant()] = val;
                }
                else if (h.Length == 1 && h[0] is >= 'a' and <= 'z')
                {
                    abc[h.ToUpperInvariant()] = val;
                }
            }
        }

        foreach (var val in abc.Values)
        {
            if (val.Length == 0)
            {
                continue;
            }

            var norm = EpcDemandNormalizeCountryCode(val);
            if (norm.Length != 0)
            {
                countries.Add(norm);
            }
        }

        countries = countries.Distinct(StringComparer.Ordinal).ToList();
        countries.Sort(StringComparer.Ordinal);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["brand"] = brand,
            ["article"] = article,
            ["countries"] = countries
        };
    }

    public static Dictionary<string, object?> EpcDemandCsvPreviewFile(string filePath, int maxRows = 25)
    {
        if (!File.Exists(filePath))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["status"] = false, ["message"] = "File not found" };
        }

        var text = File.ReadAllText(filePath);
        if (text.Length == 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["status"] = false, ["message"] = "Empty CSV" };
        }

        var lines = SplitCsvLines(text);
        if (lines.Count == 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["status"] = false, ["message"] = "Empty CSV" };
        }

        var headerNorm = lines[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        var rows = new List<Dictionary<string, object?>>();
        var line = 1;
        for (var i = 1; i < lines.Count; i++)
        {
            line++;
            var data = lines[i];
            if (data.All(c => c.Trim().Length == 0))
            {
                continue;
            }

            var parsed = EpcDemandCsvParseRow(headerNorm, data);
            var errors = new List<string>();
            if ((string)parsed["brand"]! == "")
            {
                errors.Add("missing brand");
            }

            if ((string)parsed["article"]! == "")
            {
                errors.Add("missing article");
            }

            var parsedCountries = (List<string>)parsed["countries"]!;
            if (parsedCountries.Count == 0)
            {
                errors.Add("no valid country codes (use ISO3: SDN,DZA,KEN)");
            }

            rows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["line"] = line,
                ["brand"] = parsed["brand"],
                ["article"] = parsed["article"],
                ["countries"] = EpcDemandFormatCountriesDisplay(parsedCountries),
                ["errors"] = errors
            });
            if (rows.Count >= maxRows)
            {
                break;
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["status"] = true, ["rows"] = rows };
    }

    public static Dictionary<string, object?> EpcDemandCsvImportFile(
        DemandCountryStore db,
        string filePath,
        string mode = "merge",
        Func<string, string>? normalizeArticle = null,
        Action<DemandCountryStore>? ensureSchema = null)
    {
        ensureSchema?.Invoke(db);
        if (!File.Exists(filePath))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["status"] = false, ["message"] = "Cannot open file" };
        }

        var text = File.ReadAllText(filePath);
        var lines = SplitCsvLines(text);
        if (lines.Count == 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["status"] = false, ["message"] = "Empty CSV" };
        }

        var headerNorm = lines[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        var replaceRow = mode == "replace";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var stats = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["rows_read"] = 0,
            ["rows_ok"] = 0,
            ["rows_skipped"] = 0,
            ["tags_inserted"] = 0,
            ["tags_removed"] = 0,
            ["errors"] = new List<string>()
        };
        var errors = (List<string>)stats["errors"]!;
        normalizeArticle ??= static a => Regex.Replace(a, "[^A-Z0-9]", "").ToUpperInvariant();

        for (var i = 1; i < lines.Count; i++)
        {
            var line = i + 1;
            var data = lines[i];
            if (data.All(c => c.Trim().Length == 0))
            {
                continue;
            }

            stats["rows_read"] = (int)stats["rows_read"]! + 1;
            var parsed = EpcDemandCsvParseRow(headerNorm, data);
            var brand = ((string)parsed["brand"]!).Trim();
            var article = ((string)parsed["article"]!).Trim();
            var countries = (List<string>)parsed["countries"]!;
            if (brand.Length == 0 || article.Length == 0 || countries.Count == 0)
            {
                stats["rows_skipped"] = (int)stats["rows_skipped"]! + 1;
                if (errors.Count < 30)
                {
                    errors.Add($"Line {line}: invalid row");
                }

                continue;
            }

            var articleNorm = normalizeArticle(article);
            if (articleNorm.Length == 0)
            {
                stats["rows_skipped"] = (int)stats["rows_skipped"]! + 1;
                continue;
            }

            if (replaceRow)
            {
                var removed = db.Articles.RemoveAll(r =>
                    string.Equals(r.Manufacturer, brand, StringComparison.OrdinalIgnoreCase) && r.ArticleNorm == articleNorm);
                stats["tags_removed"] = (int)stats["tags_removed"]! + removed;
            }

            foreach (var rawCode in countries)
            {
                var code = EpcDemandNormalizeCountryCode(rawCode);
                if (code.Length == 0 || EpcDemandIsStockPoolCountryCode(code))
                {
                    continue;
                }

                var existing = db.Articles.Find(r =>
                    r.Manufacturer == brand && r.ArticleNorm == articleNorm && r.CountryCode == code);
                if (existing is null)
                {
                    db.Articles.Add(new DemandArticleRow
                    {
                        Manufacturer = brand,
                        ArticleNorm = articleNorm,
                        CountryCode = code,
                        Source = "cp_csv_upload",
                        Notes = "CP demand CSV",
                        CreatedAt = now
                    });
                }
                else
                {
                    existing.Source = "cp_csv_upload";
                    existing.Notes = "CP demand CSV";
                }

                stats["tags_inserted"] = (int)stats["tags_inserted"]! + 1;
            }

            stats["rows_ok"] = (int)stats["rows_ok"]! + 1;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = true,
            ["stats"] = stats,
            ["message"] = "Import finished"
        };
    }

    public static JsonElement EpcIndustryThemeRegistry() => Themes.Value.Clone();

    public static JsonElement EpcIndustryTheme(string groupCode)
        => Themes.Value.TryGetProperty(groupCode, out var theme) ? theme.Clone() : EpcIndustryThemeDefault(groupCode);

    public static JsonElement EpcIndustryThemeDefault(string groupCode)
    {
        using var doc = JsonDocument.Parse("""{"hero":{"tagline":"Professional solutions for your business","accent":"#3b82f6","icon":"fa-briefcase","bg_image":"https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?w=1200"},"categories":[],"products":[],"erp_kit":["finance","inventory","procurement","sales"]}""");
        _ = groupCode;
        return doc.RootElement.Clone();
    }

    public static JsonElement EpcErpIndustryKit(string groupCode)
    {
        if (Kits.Value.TryGetProperty(groupCode, out var kit))
        {
            return kit.Clone();
        }

        var label = PhpUcwords(groupCode.Replace('_', ' ')) + " ERP Kit";
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["label"] = label,
            ["modules"] = new[] { "finance", "inventory", "procurement", "sales" },
            ["costing_method"] = "weighted_average",
            ["inventory_features"] = new[] { "multi_warehouse", "barcode_scanning" },
            ["compliance"] = Array.Empty<string>(),
            ["reports"] = new[] { "stock_report", "sales_analysis", "aged_payables" },
            ["integrations"] = Array.Empty<string>()
        }));
        return doc.RootElement.Clone();
    }

    public static JsonElement EpcCpIndustryAlignment(string groupCode)
    {
        if (Align.Value.TryGetProperty(groupCode, out var row))
        {
            return row.Clone();
        }

        using var doc = JsonDocument.Parse("""{"storefront_features":["product_specs","search","filters"],"product_fields":["specs","brand","sku"],"checkout_fields":[],"catalog_display":"standard_grid","price_display":"standard"}""");
        return doc.RootElement.Clone();
    }

    public static JsonElement EpcCpBrochureTopicCatalog() => Brochure.Value.Clone();

    public static string EpcCpBrochureTopicSvgUrl(string topic, string seed, string title = "", string area = "")
    {
        topic = Regex.Replace(topic.ToLowerInvariant(), "[^a-z]", "");
        if (topic.Length == 0)
        {
            topic = "platform";
        }

        seed = Regex.Replace(seed, "[^a-zA-Z0-9_\\-]", "");
        if (seed.Length == 0)
        {
            seed = "x";
        }

        var q = new List<string>
        {
            "topic=" + PhpQuery(topic),
            "seed=" + PhpQuery(seed)
        };
        if (title.Length != 0)
        {
            q.Add("t=" + PhpQuery(title));
        }

        if (area.Length != 0)
        {
            q.Add("a=" + PhpQuery(area));
        }

        return BrochureProcessPhoto.PhpPath + "?" + string.Join("&", q);
    }

    public static string EpcCpBrochureResolveTopic(string name, string area = "", string url = "", string does = "")
    {
        string Norm(string s)
        {
            s = s.Trim().ToLowerInvariant();
            s = Regex.Replace(s, "[^a-z0-9/\\s_\\-]+", " ");
            s = Regex.Replace(s, "\\s+", " ").Trim();
            return " " + s + " ";
        }

        var order = new[]
        {
            "autoparts", "inventory", "money", "orders", "logistics", "procurement",
            "ai", "marketing", "documents", "customers", "erp", "content", "platform", "settings"
        };
        var catalog = Brochure.Value;

        string MatchBest(string hay)
        {
            if (hay.Trim().Length == 0)
            {
                return "";
            }

            var best = "";
            var bestLen = 0;
            foreach (var topic in order)
            {
                if (!catalog.TryGetProperty(topic, out var row) || !row.TryGetProperty("queries", out var queries))
                {
                    continue;
                }

                foreach (var qEl in queries.EnumerateArray())
                {
                    var q = (qEl.GetString() ?? "").Trim().ToLowerInvariant();
                    if (q.Length < 3)
                    {
                        continue;
                    }

                    if (hay.Contains(q, StringComparison.Ordinal) && q.Length > bestLen)
                    {
                        best = topic;
                        bestLen = q.Length;
                    }
                }
            }

            return best;
        }

        var nameHay = Norm(name);
        if (Regex.IsMatch(nameHay, "\\bai\\b"))
        {
            return "ai";
        }

        var hit = MatchBest(nameHay);
        if (hit.Length != 0)
        {
            return hit;
        }

        hit = MatchBest(Norm(name + " " + url));
        if (hit.Length != 0)
        {
            return hit;
        }

        var hay = Norm(name + " " + url + " " + does);
        hit = MatchBest(hay);
        if (hit.Length != 0)
        {
            return hit;
        }

        if (Regex.IsMatch(hay, "\\bai\\b"))
        {
            return "ai";
        }

        var areaL = Norm(area);
        if (areaL.Contains("logistic", StringComparison.Ordinal))
        {
            return "logistics";
        }

        if (areaL.Contains("oms", StringComparison.Ordinal) || areaL.Contains("shop", StringComparison.Ordinal))
        {
            return "orders";
        }

        if (areaL.Contains("payment", StringComparison.Ordinal) || areaL.Contains("finance", StringComparison.Ordinal) || areaL.Contains("tax", StringComparison.Ordinal))
        {
            return "money";
        }

        if (areaL.Contains("price", StringComparison.Ordinal) || areaL.Contains("catalog", StringComparison.Ordinal))
        {
            return "inventory";
        }

        if (areaL.Contains("customer", StringComparison.Ordinal) || areaL.Contains("crm", StringComparison.Ordinal))
        {
            return "customers";
        }

        if (Regex.IsMatch(areaL, "\\bai\\b"))
        {
            return "ai";
        }

        if (areaL.Contains("market", StringComparison.Ordinal))
        {
            return "marketing";
        }

        if (areaL.Contains("document", StringComparison.Ordinal))
        {
            return "documents";
        }

        if (areaL.Contains("super", StringComparison.Ordinal) || areaL.Contains("portal", StringComparison.Ordinal) || areaL.Contains("integrat", StringComparison.Ordinal))
        {
            return "platform";
        }

        if (areaL.Contains("erp", StringComparison.Ordinal))
        {
            return "erp";
        }

        if (areaL.Contains("procure", StringComparison.Ordinal))
        {
            return "procurement";
        }

        if (areaL.Contains("content", StringComparison.Ordinal) || areaL.Contains("cms", StringComparison.Ordinal))
        {
            return "content";
        }

        if (areaL.Contains("system", StringComparison.Ordinal) || areaL.Contains("admin", StringComparison.Ordinal))
        {
            return "settings";
        }

        hit = MatchBest(areaL);
        return hit.Length != 0 ? hit : "default";
    }

    public static Dictionary<string, object?> EpcCpBrochureAssignUniquePhotos(Dictionary<string, List<Dictionary<string, object?>>> areas)
    {
        var catalog = Brochure.Value;
        var used = new HashSet<string>(StringComparer.Ordinal);
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        var seq = 0;
        foreach (var area in areas.Keys.ToList())
        {
            var items = areas[area];
            foreach (var item in items)
            {
                var id = (item.TryGetValue("id", out var idObj) ? Convert.ToString(idObj, CultureInfo.InvariantCulture) : "")?.Trim() ?? "";
                var name = (item.TryGetValue("name", out var nObj) ? Convert.ToString(nObj, CultureInfo.InvariantCulture) : "Process")?.Trim() ?? "Process";
                var does = (item.TryGetValue("does", out var dObj) ? Convert.ToString(dObj, CultureInfo.InvariantCulture) : "")?.Trim() ?? "";
                var urlPath = (item.TryGetValue("url", out var uObj) ? Convert.ToString(uObj, CultureInfo.InvariantCulture) : "")?.Trim() ?? "";
                if (id.Length == 0)
                {
                    id = "fn-" + Md5Hex(area + "|" + name + "|" + urlPath)[..12];
                    item["id"] = id;
                }

                seq++;
                var topic = EpcCpBrochureResolveTopic(name, area, urlPath, does);
                if (!catalog.TryGetProperty(topic, out _))
                {
                    topic = "default";
                }

                var label = catalog.GetProperty(topic).GetProperty("label").GetString() ?? "Operations";
                var custom = (item.TryGetValue("image", out var imgObj) ? Convert.ToString(imgObj, CultureInfo.InvariantCulture) : "")?.Trim() ?? "";
                string photo;
                if (custom.Length != 0
                    && !custom.Contains("epc_brochure_process_photo.php", StringComparison.Ordinal)
                    && (custom.StartsWith('/') || Regex.IsMatch(custom, "^https?://", RegexOptions.IgnoreCase))
                    && !used.Contains(custom))
                {
                    photo = custom;
                }
                else
                {
                    var seed = Regex.Replace(id, "[^a-zA-Z0-9_\\-]", "") + "-u" + seq.ToString(CultureInfo.InvariantCulture);
                    photo = EpcCpBrochureTopicSvgUrl(topic, seed, name, area);
                    var n = 0;
                    while (used.Contains(photo) && n < 8)
                    {
                        n++;
                        photo = EpcCpBrochureTopicSvgUrl(topic, seed + "x" + n.ToString(CultureInfo.InvariantCulture), name, area);
                    }
                }

                used.Add(photo);
                item["image"] = photo;
                item["photo_topic"] = topic;
                item["photo_label"] = label;
                map[id + "#u" + seq.ToString(CultureInfo.InvariantCulture)] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["topic"] = topic,
                    ["label"] = label,
                    ["photo"] = photo
                };
            }
        }

        return map;
    }

    public static Dictionary<string, object?> EpcCpBrochureItemTopicPhoto(
        Dictionary<string, object?> item,
        string area,
        IReadOnlyDictionary<string, Dictionary<string, object?>>? photoMap = null)
    {
        var id = (item.TryGetValue("id", out var idObj) ? Convert.ToString(idObj, CultureInfo.InvariantCulture) : "")?.Trim() ?? "";
        var name = (item.TryGetValue("name", out var nObj) ? Convert.ToString(nObj, CultureInfo.InvariantCulture) : "Process")?.Trim() ?? "Process";
        var does = (item.TryGetValue("does", out var dObj) ? Convert.ToString(dObj, CultureInfo.InvariantCulture) : "")?.Trim() ?? "";
        var url = (item.TryGetValue("url", out var uObj) ? Convert.ToString(uObj, CultureInfo.InvariantCulture) : "")?.Trim() ?? "";
        if (id.Length == 0)
        {
            id = "fn-" + Md5Hex(area + "|" + name + "|" + url)[..12];
        }

        var image = item.TryGetValue("image", out var imgObj) ? Convert.ToString(imgObj, CultureInfo.InvariantCulture) ?? "" : "";
        var photoLabel = item.TryGetValue("photo_label", out var plObj) ? Convert.ToString(plObj, CultureInfo.InvariantCulture) ?? "" : "";
        if (!PhpEmptyString(image) && !PhpEmptyString(photoLabel))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["topic"] = item.TryGetValue("photo_topic", out var pt) ? Convert.ToString(pt, CultureInfo.InvariantCulture) ?? "default" : "default",
                ["label"] = photoLabel,
                ["photo"] = image
            };
        }

        var na = "na:" + Md5Hex(name.ToLowerInvariant() + "|" + area.ToLowerInvariant() + "|" + url);
        if (photoMap is not null && photoMap.TryGetValue(na, out var mapped))
        {
            return mapped;
        }

        if (photoMap is not null && photoMap.TryGetValue(id, out var mappedId))
        {
            return mappedId;
        }

        var topic = EpcCpBrochureResolveTopic(name, area, url, does);
        var catalog = Brochure.Value;
        var meta = catalog.TryGetProperty(topic, out var row) ? row : catalog.GetProperty("default");
        var custom = image.Trim();
        if (custom.Length != 0 && (custom.StartsWith('/') || Regex.IsMatch(custom, "^https?://", RegexOptions.IgnoreCase)))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["topic"] = topic,
                ["label"] = meta.GetProperty("label").GetString() ?? "Operations",
                ["photo"] = custom
            };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["topic"] = topic,
            ["label"] = meta.GetProperty("label").GetString() ?? "Operations",
            ["photo"] = EpcCpBrochureTopicSvgUrl(topic, id + "-" + Md5Hex(area + "|" + url)[..6], name, area)
        };
    }

    public static string[] EpcPortalThemeTemplateSlotIds() => new[] { "classic", "modern", "midnight", "signature" };

    public static JsonElement EpcPortalDefaultThemeTemplateByIndustry() => Defaults.Value.Clone();

    public static Dictionary<string, string> EpcPortalThemePalette(string primary, string primaryDark, string accent, string sidebarFrom, string sidebarTo, string heroFrom, string heroTo)
        => new(StringComparer.Ordinal)
        {
            ["primary"] = primary,
            ["primary_dark"] = primaryDark,
            ["accent"] = accent,
            ["sidebar_from"] = sidebarFrom,
            ["sidebar_to"] = sidebarTo,
            ["hero_from"] = heroFrom,
            ["hero_to"] = heroTo
        };

    public static Dictionary<string, object?> EpcPortalThemeStyle(string id, string label, string desc, Dictionary<string, string> palette)
        => new(StringComparer.Ordinal)
        {
            ["id"] = id,
            ["label"] = label,
            ["desc"] = desc,
            ["theme"] = palette
        };

    public static JsonElement EpcPortalThemePaletteDefinitions() => Palettes.Value.Clone();

    public static List<Dictionary<string, object?>> EpcPortalQuartetFromBaseTheme(Dictionary<string, string> baseTheme, string industryName = "")
    {
        string Get(string key, string fallback) => baseTheme.TryGetValue(key, out var v) && v.Length != 0 ? v : fallback;
        var p = Get("primary", "#2563eb");
        var pd = Get("primary_dark", "#1d4ed8");
        var a = Get("accent", "#38bdf8");
        var sf = Get("sidebar_from", "#0f172a");
        var st = Get("sidebar_to", "#1e293b");
        var hf = Get("hero_from", "#0b1220");
        var ht = Get("hero_to", "#1e3a5f");
        var name = industryName.Trim();
        var prefix = name.Length != 0 ? name + " — " : "";
        return new List<Dictionary<string, object?>>
        {
            EpcPortalThemeStyle("classic", prefix + "Classic", "Default brand colours for this industry", new Dictionary<string, string>(baseTheme, StringComparer.Ordinal)),
            EpcPortalThemeStyle("modern", prefix + "Bright", "Lighter panels and stronger contrast", EpcPortalThemePalette(p, pd, a, "#334155", "#475569", hf, ht)),
            EpcPortalThemeStyle("midnight", prefix + "Midnight", "Dark navigation with vivid accents", EpcPortalThemePalette(a, p, p, "#020617", sf, "#020617", st)),
            EpcPortalThemeStyle("signature", prefix + "Accent", "Accent-led hero and sidebar highlights", EpcPortalThemePalette(a, pd, p, sf, st, hf, ht))
        };
    }

    public static Dictionary<string, Dictionary<string, string>> EpcPortalTripletFromBaseTheme(Dictionary<string, string> baseTheme)
    {
        var q = EpcPortalQuartetFromBaseTheme(baseTheme);
        return new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
        {
            ["classic"] = (Dictionary<string, string>)q[0]["theme"]!,
            ["modern"] = (Dictionary<string, string>)q[1]["theme"]!,
            ["midnight"] = (Dictionary<string, string>)q[2]["theme"]!
        };
    }

    public static Dictionary<string, string> EpcPortalThemeTemplateAliases()
        => new(StringComparer.Ordinal)
        {
            ["default"] = "classic",
            ["standard"] = "classic",
            ["light"] = "modern",
            ["dark"] = "midnight"
        };

    public static Dictionary<string, Dictionary<string, Dictionary<string, object?>>> EpcPortalIndustryStyleTemplates(
        IReadOnlyDictionary<string, IndustryInject>? industries = null)
    {
        industries ??= ThemeSlotIndustries();
        var definitions = Palettes.Value;
        var registry = new Dictionary<string, Dictionary<string, Dictionary<string, object?>>>(StringComparer.Ordinal);
        foreach (var (code, ind) in industries)
        {
            List<Dictionary<string, object?>> styles;
            if (definitions.TryGetProperty(code, out var def))
            {
                styles = new List<Dictionary<string, object?>>();
                foreach (var row in def.EnumerateArray())
                {
                    styles.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["id"] = row.GetProperty("id").GetString(),
                        ["label"] = row.GetProperty("label").GetString(),
                        ["desc"] = row.GetProperty("desc").GetString(),
                        ["theme"] = JsonElementToStringMap(row.GetProperty("theme"))
                    });
                }
            }
            else
            {
                styles = EpcPortalQuartetFromBaseTheme(ind.Theme, ind.Name);
            }

            var mapped = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
            foreach (var row in styles)
            {
                var id = (string)row["id"]!;
                mapped[id] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["label"] = row["label"],
                    ["desc"] = row["desc"],
                    ["theme"] = row["theme"]
                };
            }

            registry[code] = mapped;
        }

        return registry;
    }

    public static Dictionary<string, Dictionary<string, object?>> EpcPortalStyleTemplatesForIndustry(
        string industryCode,
        IReadOnlyDictionary<string, IndustryInject>? industries = null)
    {
        var code = Regex.Replace(industryCode, "[^a-z0-9_]", "");
        var all = EpcPortalIndustryStyleTemplates(industries);
        if (all.TryGetValue(code, out var found))
        {
            return found;
        }

        industries ??= ThemeSlotIndustries();
        var ind = industries.TryGetValue(code, out var row) ? row : new IndustryInject(code, new Dictionary<string, string>(StringComparer.Ordinal));
        var styles = EpcPortalQuartetFromBaseTheme(ind.Theme, ind.Name);
        var outMap = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var style in styles)
        {
            outMap[(string)style["id"]!] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["label"] = style["label"],
                ["desc"] = style["desc"],
                ["theme"] = style["theme"]
            };
        }

        return outMap;
    }

    public static string[] EpcPortalStyleTemplateIds(string industryCode, IReadOnlyDictionary<string, IndustryInject>? industries = null)
        => EpcPortalStyleTemplatesForIndustry(industryCode, industries).Keys.ToArray();

    public static string EpcPortalNormalizeThemeTemplate(string industryCode, string templateId, IReadOnlyDictionary<string, IndustryInject>? industries = null)
    {
        var tid = Regex.Replace(templateId.ToLowerInvariant(), "[^a-z0-9_]", "");
        var aliases = EpcPortalThemeTemplateAliases();
        if (aliases.TryGetValue(tid, out var aliased))
        {
            tid = aliased;
        }

        var allowed = EpcPortalStyleTemplateIds(industryCode, industries);
        if (tid.Length == 0 || Array.IndexOf(allowed, tid) < 0)
        {
            return "classic";
        }

        return tid;
    }

    public static string EpcPortalDefaultThemeTemplate(string industryCode, IReadOnlyDictionary<string, IndustryInject>? industries = null)
    {
        var code = Regex.Replace(industryCode, "[^a-z0-9_]", "");
        var candidate = Defaults.Value.TryGetProperty(code, out var mapped) ? mapped.GetString() ?? "classic" : "classic";
        return EpcPortalNormalizeThemeTemplate(code.Length != 0 ? code : "auto_parts", candidate, industries);
    }

    public static Dictionary<string, string> EpcPortalStyleTemplateTheme(string industryCode, string templateId, IReadOnlyDictionary<string, IndustryInject>? industries = null)
    {
        var tid = EpcPortalNormalizeThemeTemplate(industryCode, templateId, industries);
        var templates = EpcPortalStyleTemplatesForIndustry(industryCode, industries);
        if (templates.TryGetValue(tid, out var row) && row["theme"] is Dictionary<string, string> theme)
        {
            return theme;
        }

        return new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public static Dictionary<string, string> EpcPortalStyleTemplateMeta(string industryCode, string templateId, IReadOnlyDictionary<string, IndustryInject>? industries = null)
    {
        var tid = EpcPortalNormalizeThemeTemplate(industryCode, templateId, industries);
        var templates = EpcPortalStyleTemplatesForIndustry(industryCode, industries);
        if (!templates.TryGetValue(tid, out var row))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal) { ["label"] = "Classic", ["desc"] = "" };
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["label"] = Convert.ToString(row["label"], CultureInfo.InvariantCulture) ?? "",
            ["desc"] = Convert.ToString(row["desc"], CultureInfo.InvariantCulture) ?? ""
        };
    }

    public static Dictionary<string, Dictionary<string, Dictionary<string, object?>>> EpcPortalStyleTemplatesForJs(
        IReadOnlyDictionary<string, IndustryInject>? industries = null)
        => EpcPortalIndustryStyleTemplates(industries);

    public static Dictionary<string, string> EpcPortalResolveSiteTheme(Dictionary<string, object?> settings, IReadOnlyDictionary<string, IndustryInject>? industries = null)
    {
        var code = settings.TryGetValue("industry_code", out var c) ? Convert.ToString(c, CultureInfo.InvariantCulture) ?? "auto_parts" : "auto_parts";
        var tid = settings.TryGetValue("theme_template", out var t) ? Convert.ToString(t, CultureInfo.InvariantCulture) ?? "classic" : "classic";
        return EpcPortalStyleTemplateTheme(code, tid, industries);
    }

    public static JsonElement EpcPortalStorefrontPackageRegistry() => Packages.Value.Clone();

    public static JsonElement? EpcPortalStorefrontPackageMeta(string packageId)
    {
        var id = Regex.Replace(packageId, "[^a-z0-9_]", "");
        return Packages.Value.TryGetProperty(id, out var row) ? row.Clone() : null;
    }

    public static string EpcPortalStorefrontPackageForIndustry(string industryCode)
    {
        var code = Regex.Replace(industryCode, "[^a-z0-9_]", "");
        foreach (var pkg in Packages.Value.EnumerateObject())
        {
            if (!pkg.Value.TryGetProperty("industry_codes", out var codes))
            {
                continue;
            }

            foreach (var item in codes.EnumerateArray())
            {
                if (item.GetString() == code)
                {
                    return pkg.Value.GetProperty("id").GetString() ?? "";
                }
            }
        }

        return "";
    }

    public static string EpcPortalResolveStorefrontPackage(Dictionary<string, object?>? settings)
    {
        settings ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        var contact = settings.TryGetValue("contact", out var cObj) && cObj is Dictionary<string, object?> dict
            ? dict
            : new Dictionary<string, object?>(StringComparer.Ordinal);
        if (contact.TryGetValue("storefront_package", out var explicitObj))
        {
            var explicitId = Regex.Replace(Convert.ToString(explicitObj, CultureInfo.InvariantCulture) ?? "", "[^a-z0-9_]", "");
            var meta = EpcPortalStorefrontPackageMeta(explicitId);
            if (explicitId.Length != 0 && meta is not null)
            {
                if (meta.Value.TryGetProperty("implemented", out var impl) && impl.ValueKind == JsonValueKind.False)
                {
                    return "";
                }

                return explicitId;
            }
        }

        var industry = settings.TryGetValue("industry_code", out var ind) ? Convert.ToString(ind, CultureInfo.InvariantCulture) ?? "" : "";
        return industry switch
        {
            "auto_parts" => "automotive_spareparts_pro",
            "electronics" => "electronics_retail_virgin",
            "tax_advisory" or "consultancy" => "consulting_primeinvest",
            "fashion" => "fashion_retail_namshi",
            "jewellery" => "jewellery_retail_kiyasha",
            _ => ""
        };
    }

    public static List<string> EpcPortalStorefrontPackageImplementedIds()
    {
        var outList = new List<string>();
        foreach (var row in Packages.Value.EnumerateObject())
        {
            if (!row.Value.TryGetProperty("implemented", out var impl) || impl.ValueKind != JsonValueKind.False)
            {
                outList.Add(row.Name);
            }
        }

        return outList;
    }

    public static Dictionary<string, object?> EpcPortalStorefrontPackagePreset(string packageId, IReadOnlyDictionary<string, IndustryInject>? industries = null)
    {
        var meta = EpcPortalStorefrontPackageMeta(packageId);
        if (meta is null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        var industry = "electronics";
        if (meta.Value.TryGetProperty("industry_codes", out var codes) && codes.GetArrayLength() > 0)
        {
            industry = codes[0].GetString() ?? "electronics";
        }

        var template = meta.Value.TryGetProperty("theme_template", out var t) ? t.GetString() ?? "midnight" : "midnight";
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["storefront_package"] = meta.Value.GetProperty("id").GetString(),
            ["industry_code"] = industry,
            ["theme_template"] = template,
            ["access_mode"] = meta.Value.TryGetProperty("access_mode", out var am) ? am.GetString() : "full",
            ["enabled_packs"] = meta.Value.TryGetProperty("enabled_packs", out var packs) ? JsonSerializer.Deserialize<string[]>(packs.GetRawText()) : new[] { "core", "commerce", "catalogue" },
            ["tagline"] = meta.Value.TryGetProperty("tagline", out var tg) ? tg.GetString() : "",
            ["theme"] = EpcPortalStyleTemplateTheme(industry, template, industries),
            ["contact"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["storefront_package"] = meta.Value.GetProperty("id").GetString() }
        };
    }

    public static Dictionary<string, Dictionary<string, object?>> EpcPortalStorefrontPackagesForJs()
    {
        var outMap = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var row in Packages.Value.EnumerateObject())
        {
            outMap[row.Name] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["label"] = row.Value.GetProperty("label").GetString(),
                ["desc"] = row.Value.GetProperty("desc").GetString(),
                ["industry_codes"] = JsonSerializer.Deserialize<string[]>(row.Value.GetProperty("industry_codes").GetRawText()),
                ["theme_template"] = row.Value.GetProperty("theme_template").GetString()
            };
        }

        return outMap;
    }

    public static Dictionary<string, object?> EpcPortalApplyIndustryThemeProfile(
        Dictionary<string, object?> settings,
        Dictionary<string, object?> contact,
        string industryCode,
        Dictionary<string, object?>? opts = null,
        IReadOnlyDictionary<string, IndustryInject>? industries = null)
    {
        opts ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        var code = Regex.Replace(industryCode, "[^a-z0-9_]", "");
        if (code.Length == 0)
        {
            code = "auto_parts";
        }

        var erpOnly = PhpTruthy(opts, "erp_only") || code == "erp_standalone";
        var overrideTpl = opts.TryGetValue("theme_template", out var ot)
            ? Regex.Replace((Convert.ToString(ot, CultureInfo.InvariantCulture) ?? "").ToLowerInvariant(), "[^a-z0-9_]", "")
            : "";
        var overridePkg = opts.TryGetValue("storefront_package", out var op)
            ? Regex.Replace((Convert.ToString(op, CultureInfo.InvariantCulture) ?? "").ToLowerInvariant(), "[^a-z0-9_]", "")
            : "";

        var packageId = "";
        if (!erpOnly && !PhpTruthy(opts, "skip_package"))
        {
            if (overridePkg is "none" or "0")
            {
                packageId = "";
            }
            else if (overridePkg.Length != 0 && EpcPortalStorefrontPackageMeta(overridePkg) is not null)
            {
                packageId = overridePkg;
            }
            else
            {
                packageId = EpcPortalStorefrontPackageForIndustry(code);
            }
        }

        string themeTemplate;
        if (overrideTpl.Length != 0)
        {
            themeTemplate = EpcPortalNormalizeThemeTemplate(code, overrideTpl, industries);
        }
        else if (packageId.Length != 0)
        {
            var meta = EpcPortalStorefrontPackageMeta(packageId);
            var fromPkg = meta is not null && meta.Value.TryGetProperty("theme_template", out var tt)
                ? tt.GetString() ?? EpcPortalDefaultThemeTemplate(code, industries)
                : EpcPortalDefaultThemeTemplate(code, industries);
            themeTemplate = EpcPortalNormalizeThemeTemplate(code, fromPkg, industries);
        }
        else
        {
            themeTemplate = EpcPortalDefaultThemeTemplate(code, industries);
        }

        settings["industry_code"] = code;
        settings["theme_template"] = themeTemplate;
        settings["theme"] = EpcPortalStyleTemplateTheme(code, themeTemplate, industries);

        if (packageId.Length != 0)
        {
            var meta = EpcPortalStorefrontPackageMeta(packageId)!.Value;
            contact["storefront_package"] = packageId;
            if (meta.TryGetProperty("enabled_packs", out var packs) && packs.ValueKind == JsonValueKind.Array && !PhpTruthy(opts, "keep_packs"))
            {
                settings["enabled_packs"] = JsonSerializer.Deserialize<string[]>(packs.GetRawText());
            }

            if (meta.TryGetProperty("access_mode", out var am) && !string.IsNullOrEmpty(am.GetString()) && !PhpTruthy(opts, "keep_access_mode") && PhpEmpty(settings, "access_mode"))
            {
                settings["access_mode"] = am.GetString();
            }

            if (meta.TryGetProperty("tagline", out var tg) && !string.IsNullOrEmpty(tg.GetString()) && (PhpEmpty(settings, "tagline") || PhpTruthy(opts, "force_package_tagline")))
            {
                settings["tagline"] = tg.GetString();
            }

            if (packageId == "automotive_spareparts_pro")
            {
                contact["use_animated_hub_logo"] = false;
                contact["use_tenant_brand"] = false;
            }
            else
            {
                contact["use_animated_hub_logo"] = true;
                contact["use_tenant_brand"] = true;
            }
        }
        else
        {
            contact.Remove("storefront_package");
            if (erpOnly)
            {
                contact["use_animated_hub_logo"] = true;
                contact["use_tenant_brand"] = true;
            }
            else
            {
                contact["use_animated_hub_logo"] = true;
            }
        }

        var pkgLabel = packageId.Length != 0 ? packageId : "colour-only (no chrome package)";
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["theme_template"] = themeTemplate,
            ["storefront_package"] = packageId,
            ["message"] = "Applied " + code + " → style " + themeTemplate + " · package " + pkgLabel
        };
    }

    public static IReadOnlyDictionary<string, IndustryInject> ThemeSlotIndustries()
        => new Dictionary<string, IndustryInject>(StringComparer.Ordinal)
        {
            ["auto_parts"] = new("Auto parts", new Dictionary<string, string>(StringComparer.Ordinal) { ["primary"] = "#dc2626" }),
            ["electronics"] = new("Electronics", new Dictionary<string, string>(StringComparer.Ordinal)),
            ["unknown_vert"] = new("Unknown Vert", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["primary"] = "#111111",
                ["primary_dark"] = "#222222",
                ["accent"] = "#333333",
                ["sidebar_from"] = "#444444",
                ["sidebar_to"] = "#555555",
                ["hero_from"] = "#666666",
                ["hero_to"] = "#777777"
            })
        };

    public static IReadOnlyDictionary<string, IndustryInject> PackageIndustries()
        => new Dictionary<string, IndustryInject>(StringComparer.Ordinal)
        {
            ["auto_parts"] = new("Auto parts", new Dictionary<string, string>(StringComparer.Ordinal) { ["primary"] = "#dc2626" }),
            ["electronics"] = new("Electronics", new Dictionary<string, string>(StringComparer.Ordinal)),
            ["jewellery"] = new("Jewellery", new Dictionary<string, string>(StringComparer.Ordinal)),
            ["fashion"] = new("Fashion", new Dictionary<string, string>(StringComparer.Ordinal)),
            ["tax_advisory"] = new("Tax", new Dictionary<string, string>(StringComparer.Ordinal))
        };

    public sealed class IndustryInject
    {
        public IndustryInject(string name, Dictionary<string, string> theme)
        {
            Name = name;
            Theme = theme;
        }

        public string Name { get; }
        public Dictionary<string, string> Theme { get; }
    }

    public sealed class DemandCountryStore
    {
        public string CodeType { get; set; } = "char(3)";
        public List<DemandCountryRow> Countries { get; } = new();
        public List<DemandArticleRow> Articles { get; } = new();
    }

    public sealed class DemandCountryRow
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public int SortOrder { get; set; }
    }

    public sealed class DemandArticleRow
    {
        public string Manufacturer { get; set; } = "";
        public string ArticleNorm { get; set; } = "";
        public string CountryCode { get; set; } = "";
        public string Source { get; set; } = "";
        public string Notes { get; set; } = "";
        public long CreatedAt { get; set; }
    }

    private static Dictionary<string, string> JsonElementToStringMap(JsonElement el)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in el.EnumerateObject())
        {
            map[p.Name] = p.Value.GetString() ?? "";
        }

        return map;
    }

    private static List<List<string>> SplitCsvLines(string text)
    {
        var rows = new List<List<string>>();
        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            rows.Add(line.Split(',').ToList());
        }

        return rows;
    }

    private static string PhpQuery(string value)
        => Uri.EscapeDataString(value).Replace("%20", "+", StringComparison.Ordinal);

    private static string Md5Hex(string value)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(value));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    private static string PhpUcwords(string value)
    {
        var chars = value.ToCharArray();
        var cap = true;
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsWhiteSpace(chars[i]))
            {
                cap = true;
            }
            else if (cap)
            {
                chars[i] = char.ToUpperInvariant(chars[i]);
                cap = false;
            }
        }

        return new string(chars);
    }

    private static bool PhpEmptyString(string value)
        => value.Length == 0 || value == "0";

    private static bool PhpEmpty(Dictionary<string, object?> bag, string key)
    {
        if (!bag.TryGetValue(key, out var value) || value is null)
        {
            return true;
        }

        return value switch
        {
            string s => PhpEmptyString(s),
            bool b => !b,
            int i => i == 0,
            _ => false
        };
    }

    private static bool PhpTruthy(Dictionary<string, object?> bag, string key)
        => !PhpEmpty(bag, key);
}
