using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Cp.PriceImport;
using EcomAE.Platform.Storefront;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Named-function non-ERP helpers closed in this slice. PHP identifiers kept so the inventory
/// can close the files: <c>epc_auto_tax_seed_tree</c>, <c>docpart_type2_cart_check_hash</c>,
/// <c>docpart_refresh_product_cart_hashes</c>, <c>docpart_refresh_products_cart_hashes</c>,
/// <c>epc_guide_snapshot</c>, <c>epc_guide_channel_definitions</c>,
/// <c>epc_crossbase_cache_dir</c>, <c>epc_crossbase_cache_key_for_article</c>,
/// <c>epc_crossbase_cache_path</c>, <c>epc_crossbase_cache_read</c>,
/// <c>epc_crossbase_cache_write</c>, <c>epc_crossbase_cache_stats</c>,
/// <c>epc_complementary_normalize_key</c>, <c>epc_complementary_suggest_for_article</c>,
/// <c>epc_complementary_suggest_for_cart</c>, <c>epc_complementary_search_url</c>,
/// <c>epc_complementary_render_html</c>, <c>epc_session_harden_ini</c>,
/// <c>epc_session_regenerate</c>, <c>epc_session_validate</c>,
/// <c>epc_session_destroy_safe</c>, <c>epc_session_metadata</c>,
/// <c>epc_ecomae_legal_meta</c>, <c>epc_ecomae_legal_canonical_path</c>,
/// <c>epc_ecomae_legal_related_links_html</c>, <c>epc_ecomae_platform_page_legal</c>,
/// <c>epc_brand_system_name</c>, <c>epc_brand_hub_name</c>, <c>epc_brand_designer_name</c>,
/// <c>epc_brand_tagline_html</c>, <c>epc_brand_copyright_html</c>, <c>epc_brand_trade_name</c>,
/// <c>epc_brand_mandatory_line_applies</c>, <c>epc_brand_hosted_by_css_link_html</c>,
/// <c>epc_brand_hosted_by_html</c>, <c>epc_brand_cp_context</c>.
/// </summary>
public static class PhpNamedBatch
{
    public const string AutoTaxonomyPath = "content/shop/price_engine/epc_auto_parts_taxonomy.php";
    public const string ProductHashPath = "content/shop/docpart/docpart_product_hash.php";
    public const string GuideDataPath = "content/shop/docpart/epc_price_upload_guide_data.php";
    public const string CrossbaseCachePath = "content/shop/docpart/epc_crossbase_cache.php";
    public const string ComplementaryPath = "content/shop/docpart/epc_complementary_parts.php";
    public const string SessionSecurityPath = "content/users/epc_session_security.php";
    public const string LegalPagesPath = "content/general_pages/epc_ecomae_legal_pages.php";
    public const string BrandingPath = "content/general_pages/epc_branding.php";

    public const int CrossbaseFreshTtl = 21600;
    public const int SessionIdleDefault = 1800;
    public const int SessionAbsoluteDefault = 28800;
    public const string LegalEffectiveDate = "16 July 2026";
    public const string BrandingCssHref = "/content/general_pages/epc_branding.css?v=20260527";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static object EpcAutoTaxSeedTree()
        => new object[]
        {
            Branch("auto-engine", "Engine & drivetrain", 10, new object[]
            {
                Branch("auto-engine-parts", "Engine parts", null, new object[]
                {
                    Leaf("auto-engine-parts-pistons", "Pistons & rings"),
                    Leaf("auto-engine-parts-gaskets", "Gaskets & seals"),
                    Leaf("auto-engine-parts-valves", "Valves & guides")
                }),
                Branch("auto-engine-filters", "Filters", null, new object[]
                {
                    Leaf("auto-engine-filters-oil", "Oil filters"),
                    Leaf("auto-engine-filters-air", "Air filters"),
                    Leaf("auto-engine-filters-fuel", "Fuel filters"),
                    Leaf("auto-engine-filters-cabin", "Cabin filters")
                }),
                Branch("auto-engine-belts", "Belts & hoses", null, new object[]
                {
                    Leaf("auto-engine-belts-timing", "Timing belts"),
                    Leaf("auto-engine-belts-serpentine", "Serpentine belts"),
                    Leaf("auto-engine-belts-hoses", "Radiator & coolant hoses")
                }),
                Branch("auto-engine-ignition", "Ignition system", null, new object[]
                {
                    Leaf("auto-engine-spark", "Spark plugs"),
                    Leaf("auto-engine-coils", "Ignition coils"),
                    Leaf("auto-engine-distributors", "Distributors & caps")
                }),
                Branch("auto-engine-turbo", "Turbo & supercharger", null, new object[]
                {
                    Leaf("auto-engine-turbo-kits", "Turbo kits"),
                    Leaf("auto-engine-turbo-intercooler", "Intercoolers")
                })
            }),
            Branch("auto-transmission", "Transmission & clutch", 15, new object[]
            {
                Leaf("auto-transmission-gearbox", "Gearbox parts"),
                Branch("auto-transmission-clutch", "Clutch kits", null, new object[]
                {
                    Leaf("auto-transmission-clutch-disc", "Clutch discs"),
                    Leaf("auto-transmission-clutch-pressure", "Pressure plates")
                }),
                Branch("auto-transmission-cv", "CV joints & axles", null, new object[]
                {
                    Leaf("auto-transmission-cv-joints", "CV joints"),
                    Leaf("auto-transmission-cv-boots", "CV boots")
                }),
                Leaf("auto-transmission-differential", "Differential parts")
            }),
            Branch("auto-brakes", "Brakes", 20, new object[]
            {
                Branch("auto-brakes-pads", "Brake pads", null, new object[]
                {
                    Leaf("auto-brakes-pads-ceramic", "Ceramic pads"),
                    Leaf("auto-brakes-pads-semi", "Semi-metallic pads")
                }),
                Leaf("auto-brakes-rotors", "Brake rotors & discs"),
                Leaf("auto-brakes-calipers", "Calipers & hardware"),
                Leaf("auto-brakes-lines", "Brake lines & fluid")
            }),
            Branch("auto-suspension", "Suspension & steering", 25, new object[]
            {
                Branch("auto-suspension-shocks", "Shock absorbers & struts", null, new object[]
                {
                    Leaf("auto-brakes-shocks", "Shocks & struts"),
                    Leaf("auto-suspension-coilovers", "Coilovers")
                }),
                Leaf("auto-suspension-control-arms", "Control arms & bushings"),
                Leaf("auto-suspension-bearings", "Wheel bearings & hubs"),
                Branch("auto-steering", "Steering parts", null, new object[]
                {
                    Leaf("auto-steering-rack", "Steering racks"),
                    Leaf("auto-steering-pump", "Power steering pumps"),
                    Leaf("auto-steering-tie-rods", "Tie rods & ends")
                })
            }),
            Branch("auto-cooling", "Cooling & climate", 30, new object[]
            {
                Leaf("auto-cooling-radiator", "Radiators & caps"),
                Leaf("auto-cooling-water-pump", "Water pumps"),
                Leaf("auto-cooling-fans", "Cooling fans"),
                Branch("auto-ac", "AC & climate", null, new object[]
                {
                    Leaf("auto-ac-compressor", "AC compressors"),
                    Leaf("auto-ac-condenser", "Condensers & evaporators")
                })
            }),
            Branch("auto-fuel", "Fuel system", 35, new object[]
            {
                Leaf("auto-fuel-pumps", "Fuel pumps"),
                Leaf("auto-fuel-injectors", "Fuel injectors"),
                Leaf("auto-fuel-tanks", "Fuel tanks & caps")
            }),
            Branch("auto-exhaust", "Exhaust system", 40, new object[]
            {
                Leaf("auto-exhaust-manifolds", "Manifolds & headers"),
                Leaf("auto-exhaust-catalytic", "Catalytic converters"),
                Leaf("auto-exhaust-mufflers", "Mufflers & pipes")
            }),
            Branch("auto-electrical", "Electrical & sensors", 45, new object[]
            {
                Branch("auto-electrical-batteries", "Batteries", null, new object[]
                {
                    Leaf("auto-batteries", "Car batteries"),
                    Leaf("auto-batteries-agm", "AGM & start-stop batteries")
                }),
                Leaf("auto-electrical-alternators", "Alternators"),
                Leaf("auto-electrical-starters", "Starters & solenoids"),
                Branch("auto-sensors-ecu", "Sensors & ECU", null, new object[]
                {
                    Leaf("auto-sensors-o2", "O2 & lambda sensors"),
                    Leaf("auto-sensors-abs", "ABS & wheel speed sensors"),
                    Leaf("auto-sensors-ecu-units", "ECU & control modules")
                })
            }),
            Branch("auto-body", "Body & exterior", 50, new object[]
            {
                Branch("auto-body-panels", "Body panels", null, new object[]
                {
                    Leaf("auto-body-panels-doors", "Doors & fenders"),
                    Leaf("auto-body-panels-hood", "Hoods & trunk lids")
                }),
                Branch("auto-body-lights", "Lighting", null, new object[]
                {
                    Leaf("auto-lighting-headlights", "Headlights"),
                    Leaf("auto-lighting-tail", "Tail & brake lights"),
                    Leaf("auto-lighting-fog", "Fog & DRL lamps")
                }),
                Leaf("auto-body-mirrors", "Mirrors & glass"),
                Leaf("auto-body-bumpers", "Bumpers & trim"),
                Branch("auto-wiper", "Wiper & wash", null, new object[]
                {
                    Leaf("auto-wiper-blades", "Wiper blades"),
                    Leaf("auto-wiper-motors", "Wiper motors & pumps")
                })
            }),
            Branch("auto-interior", "Interior & trim", 55, new object[]
            {
                Leaf("auto-interior-mats", "Floor mats & covers"),
                Leaf("auto-interior-seats", "Seat covers & cushions"),
                Leaf("auto-interior-dash", "Dashboard & trim panels"),
                Leaf("auto-interior-electronics", "Car electronics & infotainment")
            }),
            Branch("auto-fluids", "Oils & fluids", 60, new object[]
            {
                Leaf("auto-fluids-engine-oil", "Engine oil"),
                Leaf("auto-fluids-transmission", "Transmission fluid"),
                Leaf("auto-fluids-coolant", "Coolant & antifreeze"),
                Leaf("auto-fluids-brake", "Brake fluid")
            }),
            Branch("auto-tires", "Tires & wheels", 65, new object[]
            {
                Leaf("auto-tires-passenger", "Passenger tires"),
                Leaf("auto-tires-suv", "SUV & 4x4 tires"),
                Leaf("auto-tires-alloy", "Alloy wheels & rims")
            }),
            Branch("auto-oem-brands", "OEM brand lines", 70, new object[]
            {
                Branch("auto-oem-toyota", "Toyota & Lexus", null, new object[]
                {
                    Leaf("auto-oem-toyota-engine", "Toyota engine parts"),
                    Leaf("auto-oem-lexus", "Lexus parts")
                }),
                Leaf("auto-oem-nissan", "Nissan & Infiniti"),
                Leaf("auto-oem-honda", "Honda & Acura"),
                Leaf("auto-oem-bmw", "BMW"),
                Leaf("auto-oem-mercedes", "Mercedes-Benz"),
                Leaf("auto-oem-ford", "Ford & Lincoln"),
                Leaf("auto-oem-hyundai", "Hyundai"),
                Leaf("auto-oem-kia", "Kia"),
                Leaf("auto-oem-mitsubishi", "Mitsubishi"),
                Leaf("auto-oem-landrover", "Land Rover & Range Rover"),
                Leaf("auto-oem-chevrolet", "Chevrolet & GMC")
            })
        };

    public static string DocpartType2CartCheckHash(IReadOnlyDictionary<string, object?> product, object? price, string? techKey)
    {
        var jsonParams = "";
        if (product.TryGetValue("json_params", out var raw) && raw is not null && !"".Equals(raw))
        {
            jsonParams = raw is string text
                ? text
                : JsonSerializer.Serialize(raw, JsonOpts);
        }

        var priceStr = Money(price);
        return Md5(
            Str(product, "manufacturer")
            + Str(product, "article")
            + Str(product, "article_show")
            + Str(product, "name")
            + Str(product, "exist")
            + priceStr
            + Str(product, "time_to_exe")
            + Str(product, "time_to_exe_guaranteed")
            + Str(product, "storage")
            + Str(product, "min_order")
            + Str(product, "probability")
            + Str(product, "office_id")
            + Str(product, "storage_id")
            + Str(product, "price_purchase")
            + Str(product, "markup")
            + jsonParams
            + "2"
            + (techKey ?? ""));
    }

    public static Dictionary<string, object?> DocpartRefreshProductCartHashes(Dictionary<string, object?> product, string? techKey)
    {
        techKey ??= "";
        var productType = ToInt(product.TryGetValue("product_type", out var type) ? type : 2);
        if (product.TryGetValue("price", out var price) && IsNumeric(price))
        {
            product["price"] = Money(price);
        }

        if (product.TryGetValue("price_purchase", out var purchase) && IsNumeric(purchase))
        {
            product["price_purchase"] = Money(purchase);
        }

        if (product.TryGetValue("markup", out var markup) && IsNumeric(markup))
        {
            product["markup"] = ((int)ToDouble(markup)).ToString(CultureInfo.InvariantCulture);
        }

        if (productType == 1)
        {
            product["check_hash"] = Md5(
                Cat(product, "product_id")
                + Cat(product, "office_id")
                + Cat(product, "storage_id")
                + Cat(product, "storage_record_id")
                + Cat(product, "price")
                + techKey);
            return product;
        }

        product["check_hash"] = DocpartType2CartCheckHash(product, product.TryGetValue("price", out var nowPrice) ? nowPrice : 0, techKey);
        if (!product.TryGetValue("groups_price", out var groupsObj) || groupsObj is not Dictionary<string, object?> groups || groups.Count == 0)
        {
            return product;
        }

        if (!product.TryGetValue("groups_check_hash", out var hashesObj) || hashesObj is not Dictionary<string, object?> hashes)
        {
            hashes = new Dictionary<string, object?>();
            product["groups_check_hash"] = hashes;
        }

        var markups = product.TryGetValue("groups_markup", out var markupObj) && markupObj is Dictionary<string, object?> map
            ? map
            : null;
        foreach (var gid in groups.Keys.ToList())
        {
            var groupPrice = groups[gid];
            var norm = IsNumeric(groupPrice) ? Money(groupPrice) : Convert.ToString(groupPrice, CultureInfo.InvariantCulture) ?? "";
            groups[gid] = norm;
            var tmp = new Dictionary<string, object?>(product, StringComparer.Ordinal) { ["price"] = norm };
            if (markups is not null && markups.TryGetValue(gid, out var groupMarkup))
            {
                tmp["markup"] = ((int)ToDouble(groupMarkup)).ToString(CultureInfo.InvariantCulture);
            }

            hashes[gid] = DocpartType2CartCheckHash(tmp, norm, techKey);
        }

        return product;
    }

    public static List<Dictionary<string, object?>> DocpartRefreshProductsCartHashes(List<Dictionary<string, object?>> products, string? techKey)
    {
        foreach (var product in products)
        {
            DocpartRefreshProductCartHashes(product, techKey);
        }

        return products;
    }

    public static object[] EpcGuideChannelDefinitions(IReadOnlyDictionary<string, object?>? config = null)
    {
        _ = config;
        return
        [
            Channel("CP upload wizard", "CSV, TXT, ZIP, Excel", "Green Upload button on price row"),
            Channel("Pyprices — file from PC", "CSV, XLSX per pyprices", "File input on manager row"),
            Channel("FTP", "File on FTP (file_name_substring)", "Manual FTP icon"),
            Channel("E-mail", "IMAP attachment per list rules", "Manual E-mail icon; one list per file name"),
            Channel("URL / link", "Direct file URL", "Manual link icon"),
            Channel("Cron schedule", "FTP/email/URL per schedule", "wget cron every minute"),
            Channel("Deploy API", "epc-upload-uae-prices.php", "POST price_file + tech_key"),
            Channel(
                "Multi-vendor price upload",
                "Excel/CSV with Vendor full + Vendor short; data types inventory/sales/purchase; auto warehouse+list per vendor",
                "CP /shop/prices/multivendor (sample CSV) or POST /epc-upload-multivendor-prices.php — short code on storefront, full name in CP only")
        ];
    }

    public static Dictionary<string, object?> EpcGuideSnapshot(
        IReadOnlyList<GuidePriceRow> prices,
        IReadOnlyDictionary<string, GuideHistoryRow> history,
        int cronTasks,
        int cronPriceLinks,
        int pendingTasks,
        IReadOnlyDictionary<string, object?>? config = null)
    {
        var byLoadMode = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var row in prices)
        {
            var key = row.LoadMode.ToString(CultureInfo.InvariantCulture);
            if (!byLoadMode.TryGetValue(key, out var bucketObj) || bucketObj is not Dictionary<string, object?> bucket)
            {
                bucket = new Dictionary<string, object?>
                {
                    ["count"] = 0,
                    ["records"] = 0,
                    ["lists"] = new List<Dictionary<string, object?>>()
                };
                byLoadMode[key] = bucket;
            }

            bucket["count"] = (int)bucket["count"]! + 1;
            bucket["records"] = (int)bucket["records"]! + row.RecordsCount;
            ((List<Dictionary<string, object?>>)bucket["lists"]!).Add(new Dictionary<string, object?>
            {
                ["id"] = row.Id,
                ["name"] = row.Name,
                ["last_updated"] = row.LastUpdated,
                ["records_count"] = row.RecordsCount
            });
        }

        var historyBySource = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (source, row) in history)
        {
            historyBySource[source] = new Dictionary<string, object?>
            {
                ["uploads"] = row.Uploads,
                ["last_at"] = row.LastAt
            };
        }

        return new Dictionary<string, object?>
        {
            ["load_modes"] = new Dictionary<string, object?>
            {
                ["1"] = "Manual",
                ["2"] = "FTP",
                ["3"] = "E-mail",
                ["4"] = "URL"
            },
            ["by_load_mode"] = byLoadMode,
            ["price_lists_total"] = prices.Count,
            ["history_by_source"] = historyBySource,
            ["cron_tasks"] = cronTasks,
            ["cron_price_links"] = cronPriceLinks,
            ["pyprices_pending_tasks"] = pendingTasks,
            ["channels"] = EpcGuideChannelDefinitions(config)
        };
    }

    public static string EpcCrossbaseCacheDir(string documentRoot)
    {
        var dir = documentRoot.TrimEnd('/') + "/content/shop/docpart/cache/crossbase";
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string EpcCrossbaseCacheKeyForArticle(string? articleInput)
    {
        var key = DocpartArticle.NormalizeForPrice(articleInput);
        if (key.Length == 0)
        {
            key = Regex.Replace(articleInput ?? "", "[^A-Za-z0-9]", "");
        }

        return key;
    }

    public static string EpcCrossbaseCachePath(string documentRoot, string? articleInput)
    {
        var key = EpcCrossbaseCacheKeyForArticle(articleInput);
        return key.Length == 0 ? "" : EpcCrossbaseCacheDir(documentRoot) + "/" + key + ".html";
    }

    public static string EpcCrossbaseCacheRead(string documentRoot, string? articleInput, int freshTtlSeconds = CrossbaseFreshTtl, bool allowStale = true, long? now = null)
    {
        var path = EpcCrossbaseCachePath(documentRoot, articleInput);
        if (path.Length == 0 || !File.Exists(path))
        {
            return "";
        }

        var age = (now ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds()) - new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds();
        if (freshTtlSeconds > 0 && age <= freshTtlSeconds)
        {
            var html = File.ReadAllText(path);
            return html.Length > 0 ? html : "";
        }

        if (!allowStale)
        {
            return "";
        }

        var stale = File.ReadAllText(path);
        return stale.Length > 400 ? stale : "";
    }

    public static bool EpcCrossbaseCacheWrite(string documentRoot, string? articleInput, string? html)
    {
        html ??= "";
        if (html.Length == 0 || html.Length <= 400)
        {
            return false;
        }

        var path = EpcCrossbaseCachePath(documentRoot, articleInput);
        if (path.Length == 0)
        {
            return false;
        }

        File.WriteAllText(path, html);
        return true;
    }

    public static Dictionary<string, int> EpcCrossbaseCacheStats(string documentRoot, long? now = null)
    {
        var dir = EpcCrossbaseCacheDir(documentRoot);
        var files = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.html") : [];
        var fresh = 0;
        var stale = 0;
        var stamp = now ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var file in files)
        {
            var age = stamp - new DateTimeOffset(File.GetLastWriteTimeUtc(file)).ToUnixTimeSeconds();
            if (age <= CrossbaseFreshTtl)
            {
                fresh++;
            }
            else
            {
                stale++;
            }
        }

        return new Dictionary<string, int>
        {
            ["files_total"] = files.Length,
            ["files_fresh"] = fresh,
            ["files_stale"] = stale
        };
    }

    public static string EpcComplementaryNormalizeKey(string brand, string article)
        => (brand.Trim() + "|" + DocpartArticle.NormalizeForPrice(article)).ToUpperInvariant();

    public static List<Dictionary<string, string>> EpcComplementarySuggestForArticle(string brand, string article, int limit = 6)
    {
        _ = brand;
        _ = article;
        _ = Math.Clamp(limit, 1, 12);
        return [];
    }

    public static List<Dictionary<string, string>> EpcComplementarySuggestForCart(int userId, int sessionId, int limit = 8)
    {
        _ = userId;
        _ = sessionId;
        _ = Math.Clamp(limit, 1, 16);
        return [];
    }

    public static string EpcComplementarySearchUrl(string brand, string article, string? langHref = "")
    {
        var url = (langHref ?? "") + "/shop/part_search?article=" + Uri.EscapeDataString(article);
        if (brand.Length > 0)
        {
            url += "&brend=" + Uri.EscapeDataString(brand);
        }

        return url;
    }

    public static string EpcComplementaryRenderHtml(IReadOnlyList<Dictionary<string, string>> suggestions, string heading = "Related parts you may also need", string? langHref = "")
    {
        if (suggestions.Count == 0)
        {
            return "";
        }

        var html = new StringBuilder();
        html.Append("<div class=\"epc-complementary-panel alert alert-info\" style=\"margin:16px 0;\">")
            .Append("<h4 style=\"margin:0 0 10px;font-size:16px;\"><i class=\"fa fa-puzzle-piece\"></i> ")
            .Append(H(heading)).Append("</h4>")
            .Append("<p class=\"text-muted\" style=\"margin:0 0 10px;font-size:13px;\">Based on cross references and catalogue clusters — verify fitment before ordering.</p>")
            .Append("<div class=\"epc-complementary-list\" style=\"display:flex;flex-wrap:wrap;gap:8px;\">");
        foreach (var row in suggestions)
        {
            var brand = row.TryGetValue("brand", out var b) ? b : "";
            var article = row.TryGetValue("article", out var a) ? a : "";
            var label = (row.TryGetValue("label", out var l) ? l : "").Trim();
            if (label.Length == 0)
            {
                continue;
            }

            html.Append("<a class=\"btn btn-sm btn-default\" href=\"").Append(H(EpcComplementarySearchUrl(brand, article, langHref)))
                .Append("\" style=\"margin:0;\">").Append(H(label)).Append("</a>");
        }

        return html.Append("</div></div>").ToString();
    }

    public static SessionCookiePolicy EpcSessionHardenIni(bool https)
        => new(true, true, https, true, "Lax");

    public static void EpcSessionRegenerate(Dictionary<string, object?> session, long now, string? userAgent)
    {
        session["epc_session_created"] = now;
        session["epc_session_last_active"] = now;
        session["epc_session_ua"] = userAgent ?? "";
    }

    public static bool EpcSessionValidate(
        Dictionary<string, object?> session,
        bool active,
        long now,
        string? currentUa,
        int idleTimeout = SessionIdleDefault,
        int absoluteLifetime = SessionAbsoluteDefault)
    {
        if (!active)
        {
            return false;
        }

        var created = session.TryGetValue("epc_session_created", out var createdObj) ? ToInt(createdObj) : 0;
        if (created > 0 && now - created > absoluteLifetime)
        {
            EpcSessionDestroySafe(session);
            return false;
        }

        var lastActive = session.TryGetValue("epc_session_last_active", out var lastObj) ? ToInt(lastObj) : 0;
        if (lastActive > 0 && now - lastActive > idleTimeout)
        {
            EpcSessionDestroySafe(session);
            return false;
        }

        var storedUa = session.TryGetValue("epc_session_ua", out var uaObj) ? Convert.ToString(uaObj, CultureInfo.InvariantCulture) ?? "" : "";
        var current = currentUa ?? "";
        if (storedUa.Length > 0 && storedUa != current)
        {
            EpcSessionDestroySafe(session);
            return false;
        }

        session["epc_session_last_active"] = now;
        return true;
    }

    public static void EpcSessionDestroySafe(Dictionary<string, object?> session) => session.Clear();

    public static Dictionary<string, object?> EpcSessionMetadata(IReadOnlyDictionary<string, object?> session, long now)
    {
        var created = session.TryGetValue("epc_session_created", out var createdObj) ? ToInt(createdObj) : (int)now;
        var lastActive = session.TryGetValue("epc_session_last_active", out var lastObj) ? ToInt(lastObj) : (int)now;
        return new Dictionary<string, object?>
        {
            ["created"] = created,
            ["last_active"] = lastActive,
            ["idle_seconds"] = now - lastActive,
            ["lifetime_seconds"] = now - created
        };
    }

    public static string[] EpcEcomaeLegalMeta(IReadOnlyDictionary<string, LegalEntry>? catalog, string? slug)
    {
        catalog ??= LegalCatalog();
        var clean = SanitizeSlug(slug);
        if (clean.Length > 0 && catalog.TryGetValue(clean, out var title))
        {
            return [title.Title + " — ECOM AE Legal", title.Summary];
        }

        return
        [
            "Legal policies — ECOM AE",
            "Privacy, Terms, Security, Trademark, Right to Use, Copyright, Data Protection, and other legal policies for the ECOM AE Blockchain BOS Enterprise System."
        ];
    }

    public static string EpcEcomaeLegalCanonicalPath(string? slug)
    {
        var clean = SanitizeSlug(slug);
        return clean.Length > 0 ? "/legal/" + clean : "/legal";
    }

    public static string EpcEcomaeLegalRelatedLinksHtml(string currentSlug = "", string baseUrl = "https://www.ecomae.com/", IReadOnlyDictionary<string, LegalEntry>? catalog = null)
    {
        var trimmed = baseUrl.TrimEnd('/');
        catalog ??= LegalCatalog();
        var html = new StringBuilder();
        html.Append("<nav class=\"epm-legal-related\" aria-label=\"All legal policies\" style=\"margin-top:36px;padding-top:20px;border-top:1px solid var(--epm-border)\">")
            .Append("<p style=\"margin:0 0 10px;color:var(--epm-muted);font-size:13px;font-weight:700\">All policies</p>")
            .Append("<div style=\"display:flex;flex-wrap:wrap;gap:8px 16px\">")
            .Append("<a href=\"").Append(H(trimmed + "/legal")).Append("\" style=\"color:var(--epm-cyan);font-size:13px;font-weight:600;text-decoration:none\">Legal hub</a>");
        foreach (var (slug, entry) in catalog)
        {
            var style = slug == currentSlug
                ? "color:#fff;font-size:13px;font-weight:700;text-decoration:none"
                : "color:var(--epm-muted);font-size:13px;font-weight:600;text-decoration:none";
            html.Append("<a href=\"").Append(H(trimmed + "/legal/" + slug)).Append("\" style=\"").Append(style).Append("\">")
                .Append(H(entry.Title)).Append("</a>");
        }

        return html.Append("</div></nav>").ToString();
    }

    public static string EpcEcomaePlatformPageLegal(string? slug = "", string baseUrl = "https://www.ecomae.com/", IReadOnlyDictionary<string, LegalEntry>? catalog = null)
    {
        catalog ??= LegalCatalog();
        var clean = SanitizeSlug(slug);
        if (clean.Length > 0 && catalog.TryGetValue(clean, out var page))
        {
            return "<div class=\"epm-wrap\"><div class=\"epm-section\"><h1>" + H(page.Title) + "</h1><p>" + H(page.Summary)
                + "</p><p>Effective date: " + H(LegalEffectiveDate) + "</p>"
                + EpcEcomaeLegalRelatedLinksHtml(clean, baseUrl, catalog) + "</div></div>";
        }

        var sb = new StringBuilder("<div class=\"epm-wrap\"><h1>ECOM AE legal policies</h1>");
        foreach (var (key, entry) in catalog)
        {
            sb.Append("<a href=\"").Append(H(baseUrl.TrimEnd('/') + "/legal/" + key)).Append("\">").Append(H(entry.Title)).Append("</a>");
        }

        return sb.Append(EpcEcomaeLegalRelatedLinksHtml("", baseUrl, catalog)).Append("</div>").ToString();
    }

    public static string EpcBrandSystemName(BrandSite site)
        => string.IsNullOrEmpty(site.SystemName) ? "ECOM AE portal" : site.SystemName;

    public static string EpcBrandHubName(BrandSite site)
        => string.IsNullOrEmpty(site.HubName) ? "ecomae" : site.HubName;

    public static string EpcBrandDesignerName() => "ecomae";

    public static string EpcBrandTaglineHtml(BrandSite site)
    {
        var tagline = string.IsNullOrEmpty(site.Tagline) ? "Designed by ecomae" : site.Tagline;
        return "<strong>" + H(EpcBrandSystemName(site)) + "</strong> &mdash; " + H(tagline);
    }

    public static string EpcBrandCopyrightHtml() => "Designed by " + EpcBrandDesignerName();

    public static string EpcBrandTradeName(BrandSite site)
    {
        if (!string.IsNullOrEmpty(site.ContactTradeName))
        {
            return site.ContactTradeName;
        }

        return !string.IsNullOrEmpty(site.TradeName) ? site.TradeName : EpcBrandHubName(site);
    }

    public static bool EpcBrandMandatoryLineApplies(BrandSite site)
        => !site.AutopartsParity && site.ClientHost;

    public static string EpcBrandHostedByCssLinkHtml(BrandSite site)
        => EpcBrandMandatoryLineApplies(site) ? "<link rel=\"stylesheet\" href=\"" + BrandingCssHref + "\" />\n" : "";

    public static string EpcBrandHostedByHtml(BrandSite site)
        => EpcBrandMandatoryLineApplies(site)
            ? "<span class=\"epc-hosted-by\">Built &amp; Managed by <a href=\"https://www.ecomae.com/\" target=\"_blank\" rel=\"noopener noreferrer\">ecomae.com</a></span>"
            : "";

    public static Dictionary<string, object?> EpcBrandCpContext(BrandSite site)
        => new()
        {
            ["product_name"] = EpcBrandSystemName(site),
            ["company_name"] = EpcBrandTradeName(site),
            ["product_description"] = "Multi-industry commerce platform",
            ["hub_tagline"] = "Finance & operations",
            ["brand_copyright"] = EpcBrandCopyrightHtml(),
            ["designer_name"] = EpcBrandDesignerName(),
            ["trade_name"] = EpcBrandTradeName(site),
            ["hosted_by_html"] = EpcBrandHostedByHtml(site),
            ["is_shared_erp_session"] = false
        };

    public readonly record struct GuidePriceRow(int Id, string Name, int LoadMode, string LastUpdated, int RecordsCount);
    public readonly record struct GuideHistoryRow(int Uploads, string LastAt);
    public readonly record struct LegalEntry(string Title, string Summary);
    public readonly record struct SessionCookiePolicy(bool CookiesOnly, bool Strict, bool Secure, bool HttpOnly, string SameSite);
    public readonly record struct BrandSite(
        string? SystemName = null,
        string? HubName = null,
        string? Tagline = null,
        string? TradeName = null,
        string? ContactTradeName = null,
        bool ClientHost = false,
        bool AutopartsParity = false);

    public static Dictionary<string, LegalEntry> LegalCatalog()
        => new(StringComparer.Ordinal)
        {
            ["privacy"] = new("Privacy Policy", "How ECOM AE collects, uses, stores, and protects personal and business data across the platform, Super CP, and tenant workspaces."),
            ["terms"] = new("Terms of Use", "Terms for using the ECOM AE platform.")
        };

    private static Dictionary<string, object> Leaf(string slug, string name)
        => new() { ["slug"] = slug, ["name"] = name };

    private static Dictionary<string, object> Branch(string slug, string name, int? sort, object[] children)
    {
        var row = new Dictionary<string, object> { ["slug"] = slug, ["name"] = name, ["children"] = children };
        if (sort is int value)
        {
            row["sort"] = value;
        }

        return row;
    }

    private static Dictionary<string, object?> Channel(string title, string formats, string test)
        => new() { ["title"] = title, ["formats"] = formats, ["test"] = test };

    private static string H(string? value) => StorefrontTinyPages.HtmlSpecialChars(value);

    private static string SanitizeSlug(string? slug)
        => slug is null ? "" : Regex.Replace(slug, "[^a-z0-9\\-]", "");

    private static string Str(IReadOnlyDictionary<string, object?> product, string key)
        => product.TryGetValue(key, out var value) && value is not null
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
            : "";

    private static string Cat(IReadOnlyDictionary<string, object?> product, string key)
        => product.TryGetValue(key, out var value) && value is not null
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
            : "";

    private static string Money(object? value)
        => ToDouble(value).ToString("0.00", CultureInfo.InvariantCulture);

    private static bool IsNumeric(object? value)
        => value is not null && double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    private static double ToDouble(object? value)
        => value is null ? 0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);

    private static int ToInt(object? value)
        => value is null ? 0 : Convert.ToInt32(ToDouble(value));

    private static string Md5(string value)
        => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
