using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Storefront;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-more named helpers. PHP identifiers kept for the inventory:
/// <c>epc_portal_industry_catalog_profile</c>, <c>epc_portal_industry_catalog_section_title</c>,
/// <c>epc_portal_industry_catalog_unsplash</c>, <c>epc_portal_industry_catalog_categories</c>,
/// <c>epc_portal_industry_catalog_render</c>,
/// <c>epc_storefront_layout_registry</c>, <c>epc_storefront_layouts_for_industry</c>,
/// <c>epc_storefront_layout_default</c>, <c>epc_storefront_layout_meta</c>,
/// <c>epc_storefront_layout_sections</c>, <c>epc_storefront_active_layout</c>,
/// <c>epc_storefront_layouts_for_js</c>,
/// <c>epc_industry_subdomain_detect</c>, <c>epc_is_industry_subdomain</c>,
/// <c>epc_industry_subdomain_resolve_group</c>, <c>epc_industry_subdomain_bootstrap</c>,
/// <c>epc_dealer_ensure_schema</c>, <c>epc_dealer_tier_discounts</c>, <c>epc_dealer_register</c>,
/// <c>epc_dealer_list</c>, <c>epc_dealer_place_order</c>, <c>epc_dealer_auto_tier</c>,
/// <c>epc_dealer_fleet_stats</c>, <c>epc_dealer_get</c>, <c>epc_dealer_update</c>,
/// <c>epc_dealer_orders</c>, <c>epc_dealer_suspend</c>, <c>epc_dealer_activate</c>,
/// <c>epc_dealer_performance_report</c>.
/// </summary>
public static class PhpPlanQ1More
{
    public const string IndustryCatalogPath = "content/general_pages/epc_portal_industry_catalog.php";
    public const string StorefrontLayoutsPath = "content/general_pages/epc_storefront_layouts.php";
    public const string IndustrySubdomainRouterPath = "content/general_pages/epc_industry_subdomain_router.php";
    public const string DealerPortalPath = "content/general_pages/epc_dealer_portal.php";
    public const string SocialHubCssPath = "content/general_pages/epc_social_media_hub_css.php";
    public const string DealerPortalVersion = "1.0.0";

    private static readonly string[] DirectGroups =
    {
        "construction", "healthcare", "automotive", "food", "hospitality",
        "beauty", "education", "energy", "manufacturing", "agriculture",
        "technology", "finance", "logistics", "realestate", "media",
        "legal", "sports", "environmental", "aerospace", "mining",
        "telecom", "retail", "fashion", "jewellery", "electronics",
        "consulting", "government", "nonprofit",
        "wholesale", "rental", "cleaning", "pet", "printing", "security",
        "homeliving", "professional"
    };

    private static readonly HashSet<string> ReservedHosts = new(StringComparer.Ordinal)
    {
        "www", "cp", "api", "mail", "smtp", "ftp", "ns1", "ns2", "cdn", "admin", "www1", "asap"
    };

    private static readonly Dictionary<string, string> SubAliases = new(StringComparer.Ordinal)
    {
        ["medical"] = "healthcare",
        ["medicalequipment"] = "healthcare",
        ["health"] = "healthcare",
        ["pharma"] = "healthcare",
        ["pharmacy"] = "healthcare",
        ["dental"] = "healthcare",
        ["hospital"] = "healthcare",
        ["auto"] = "automotive",
        ["autoparts"] = "automotive",
        ["cars"] = "automotive",
        ["vehicles"] = "automotive",
        ["spare"] = "automotive",
        ["spareparts"] = "automotive",
        ["foodbeverage"] = "food",
        ["restaurant"] = "food",
        ["catering"] = "food",
        ["bakery"] = "food",
        ["hotel"] = "hospitality",
        ["hotels"] = "hospitality",
        ["tourism"] = "hospitality",
        ["travel"] = "hospitality",
        ["salon"] = "beauty",
        ["cosmetics"] = "beauty",
        ["skincare"] = "beauty",
        ["school"] = "education",
        ["university"] = "education",
        ["training"] = "education",
        ["solar"] = "energy",
        ["oil"] = "energy",
        ["gas"] = "energy",
        ["petroleum"] = "energy",
        ["power"] = "energy",
        ["utility"] = "energy",
        ["utilities"] = "energy",
        ["renewable"] = "energy",
        ["electric"] = "energy",
        ["biomass"] = "energy",
        ["bioenergy"] = "energy",
        ["biogas"] = "energy",
        ["hydrogen"] = "energy",
        ["geothermal"] = "energy",
        ["nuclear"] = "energy",
        ["wind"] = "energy",
        ["ev"] = "energy",
        ["evcharging"] = "energy",
        ["battery"] = "energy",
        ["grid"] = "energy",
        ["energy_utilities"] = "energy",
        ["factory"] = "manufacturing",
        ["industrial"] = "manufacturing",
        ["production"] = "manufacturing",
        ["farming"] = "agriculture",
        ["tech"] = "technology",
        ["software"] = "technology",
        ["it"] = "technology",
        ["fintech"] = "finance",
        ["banking"] = "finance",
        ["insurance"] = "finance",
        ["accounting"] = "finance",
        ["tax"] = "consulting",
        ["shipping"] = "logistics",
        ["transport"] = "logistics",
        ["freight"] = "logistics",
        ["warehouse"] = "logistics",
        ["property"] = "realestate",
        ["properties"] = "realestate",
        ["clothing"] = "fashion",
        ["apparel"] = "fashion",
        ["gold"] = "jewellery",
        ["diamonds"] = "jewellery",
        ["gadgets"] = "electronics",
        ["computers"] = "electronics",
        ["phones"] = "electronics",
        ["advisory"] = "consulting",
        ["consultancy"] = "consulting",
        ["law"] = "legal",
        ["fitness"] = "sports",
        ["gym"] = "sports",
        ["green"] = "environmental",
        ["recycling"] = "environmental",
        ["aviation"] = "aerospace",
        ["defense"] = "aerospace",
        ["defence"] = "aerospace",
        ["telco"] = "telecom",
        ["mobile"] = "telecom",
        ["shopping"] = "retail",
        ["ecommerce"] = "retail",
        ["supermarket"] = "retail",
        ["ngo"] = "nonprofit",
        ["charity"] = "nonprofit",
        ["furniture"] = "homeliving",
        ["interiors"] = "homeliving",
        ["home"] = "homeliving",
        ["distribution"] = "wholesale",
        ["trading"] = "wholesale",
        ["leasing"] = "rental",
        ["hire"] = "rental",
        ["facilities"] = "cleaning",
        ["maintenance"] = "cleaning",
        ["veterinary"] = "pet",
        ["petcare"] = "pet",
        ["signage"] = "printing",
        ["print"] = "printing",
        ["surveillance"] = "security",
        ["guard"] = "security",
        ["cctv"] = "security"
    };

    private static readonly string[] UpdateFields =
    {
        "company_name", "contact_email", "phone", "tier", "discount_pct", "status", "credit_limit", "payment_terms_days"
    };

    public sealed class DealerRow
    {
        public int Id { get; set; }
        public string SiteKey { get; set; } = "";
        public string DealerCode { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string ContactName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Tier { get; set; } = "bronze";
        public double DiscountPct { get; set; }
        public double CreditLimit { get; set; }
        public string Territory { get; set; } = "";
        public string Status { get; set; } = "pending";
        public double YtdRevenue { get; set; }
        public int OrderCount { get; set; }
        public Dictionary<string, object?> Extra { get; } = new(StringComparer.Ordinal);
    }

    public sealed class DealerOrderRow
    {
        public int Id { get; set; }
        public string SiteKey { get; set; } = "";
        public int DealerId { get; set; }
        public string OrderRef { get; set; } = "";
        public double OrderTotal { get; set; }
        public double DiscountApplied { get; set; }
        public double NetTotal { get; set; }
        public string Status { get; set; } = "submitted";
        public string CreatedAt { get; set; } = "";
    }

    public sealed class DealerStore
    {
        public List<DealerRow> Dealers { get; } = new();
        public List<DealerOrderRow> Orders { get; } = new();
        public int NextDealerId { get; set; } = 1;
        public int NextOrderId { get; set; } = 1;
    }

    public static string? EpcPortalIndustryCatalogProfile(Dictionary<string, object?>? site, string? package, bool profileExists = true)
    {
        if (!profileExists)
        {
            return null;
        }

        var industry = site is not null && site.TryGetValue("industry", out var raw) && raw is not null
            ? Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "auto_parts"
            : "auto_parts";
        var pkg = package ?? "";
        if (pkg == "automotive_spareparts_pro")
        {
            return null;
        }

        if (industry == "auto_parts" && pkg.Length == 0)
        {
            return null;
        }

        if (pkg == "electronics_retail_virgin" || industry == "electronics")
        {
            return "electronics";
        }

        if (pkg == "consulting_primeinvest" || industry is "tax_advisory" or "consultancy")
        {
            return "consulting";
        }

        if (industry == "fashion")
        {
            return "fashion";
        }

        return industry == "jewellery" ? "jewellery" : null;
    }

    public static string EpcPortalIndustryCatalogSectionTitle(string? profile, Func<int, string>? translate = null)
        => profile switch
        {
            "electronics" => "Shop by category",
            "consulting" => "Our services",
            "fashion" => "Shop by collection",
            "jewellery" => "Our collections",
            _ => translate is not null ? translate(3994) : "Catalog of goods"
        };

    public static string EpcPortalIndustryCatalogUnsplash(string photoId, object? w = null, object? h = null, Func<string, bool>? localExists = null)
    {
        var rel = "/content/files/images/storefronts/catalog/" + photoId + ".jpg";
        if (localExists is not null && localExists(rel))
        {
            return rel;
        }

        var width = w is null ? 400 : StorefrontPhpInt.Cast(Convert.ToString(w, CultureInfo.InvariantCulture));
        var height = h is null ? 400 : StorefrontPhpInt.Cast(Convert.ToString(h, CultureInfo.InvariantCulture));
        return "https://images.unsplash.com/" + photoId + "?auto=format&fit=crop&w=" + width + "&h=" + height + "&q=80";
    }

    public static List<Dictionary<string, string>> EpcPortalIndustryCatalogCategories(
        string? profile,
        string langHref = "",
        Func<string, int, int, string>? electronicsImg = null,
        Func<string, string>? electronicsImgAlt = null,
        Func<string, bool>? localExists = null)
    {
        switch (profile)
        {
            case "electronics":
                var items = new (string Label, string Href, string Key)[]
                {
                    ("Phones", "/shop/search?q=phones", "smartphone"),
                    ("Laptops", "/shop/search?q=laptops", "laptop"),
                    ("Gaming", "/shop/search?q=gaming", "gaming_setup"),
                    ("Audio", "/shop/search?q=audio", "headphones"),
                    ("TVs", "/shop/search?q=tv", "tv_living"),
                    ("Accessories", "/shop/search?q=accessories", "mouse"),
                    ("Smart Home", "/shop/search?q=smart+home", "smart_home"),
                    ("Tablets", "/shop/search?q=tablet", "tablet_lifestyle")
                };
                return items.Select(item =>
                {
                    var image = electronicsImg is not null
                        ? electronicsImg(item.Key, 400, 400)
                        : EpcPortalIndustryCatalogUnsplash("photo-1592899677977-9c10ca588bbd", 400, 400, localExists);
                    var alt = electronicsImgAlt is not null ? electronicsImgAlt(item.Key) : item.Label;
                    return new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["label"] = item.Label,
                        ["href"] = langHref + item.Href,
                        ["image"] = image,
                        ["alt"] = alt
                    };
                }).ToList();
            case "consulting":
                return new List<Dictionary<string, string>>
                {
                    Cat("Corporate Tax", langHref + "/#epc-cpi-services", "photo-1450101499163-c8848c66ca85", "Corporate tax advisory", localExists),
                    Cat("VAT", langHref + "/#epc-cpi-services", "photo-1554224155-6726b3ff858f", "VAT compliance and filing", localExists),
                    Cat("Accounting", langHref + "/#epc-cpi-services", "photo-1551836022-d5d88e9c9639", "Accounting and bookkeeping", localExists),
                    Cat("Audit", langHref + "/#epc-cpi-services", "photo-1507679799987-c73779587ccf", "Audit-ready reporting", localExists),
                    Cat("ERP", langHref + "/erp", "photo-1460925895917-afdab827c52f", "Client ERP portal", localExists),
                    Cat("Advisory", langHref + "/kontakty", "photo-1454165804606-220107c9a589", "Business advisory", localExists)
                };
            case "fashion":
                return new List<Dictionary<string, string>>
                {
                    Cat("Women", langHref + "/shop/search?q=women", "photo-1490481651871-ab68de25d43d", "Women's fashion", localExists),
                    Cat("Men", langHref + "/shop/search?q=men", "photo-1617137968427-85924c800a22", "Men's fashion", localExists),
                    Cat("Shoes", langHref + "/shop/search?q=shoes", "photo-1543163521-1bf539c55dd1", "Footwear", localExists),
                    Cat("Bags", langHref + "/shop/search?q=bags", "photo-1584917865442-de89a762c4a6", "Bags and leather goods", localExists),
                    Cat("Accessories", langHref + "/shop/search?q=accessories", "photo-1523381210434-271fa8a0a992", "Fashion accessories", localExists),
                    Cat("New arrivals", langHref + "/shop/search?q=new", "photo-1483985988357-763728e1935b", "New arrivals", localExists)
                };
            case "jewellery":
                return new List<Dictionary<string, string>>
                {
                    Cat("Rings", langHref + "/shop/search?q=rings", "photo-1605100804763-247f67b3557e", "Rings", localExists),
                    Cat("Necklaces", langHref + "/shop/search?q=necklaces", "photo-1599643478518-a784e69ba83f", "Necklaces", localExists),
                    Cat("Earrings", langHref + "/shop/search?q=earrings", "photo-1535632066927-ab7c9a509e3f", "Earrings", localExists),
                    Cat("Bracelets", langHref + "/shop/search?q=bracelets", "photo-1611591437281-460bfac7a2c3", "Bracelets", localExists),
                    Cat("Wedding", langHref + "/shop/search?q=wedding", "photo-1515562141207-7a88fb7ce338", "Wedding jewellery", localExists),
                    Cat("Gifts", langHref + "/shop/search?q=gifts", "photo-1602173574767-37ac01994b2a", "Gift jewellery", localExists)
                };
            default:
                return new List<Dictionary<string, string>>();
        }
    }

    public static string EpcPortalIndustryCatalogRender(string? profile, string langHref = "", Func<string, bool>? localExists = null)
    {
        var categories = EpcPortalIndustryCatalogCategories(profile, langHref, localExists: localExists);
        if (categories.Count == 0)
        {
            return "";
        }

        var sb = new StringBuilder();
        sb.Append("\t<div class=\"row\" style=\"padding: 0px 4px; margin-top:-9px;\">\n");
        var first = true;
        foreach (var cat in categories)
        {
            var href = H(cat["href"]);
            var img = H(cat["image"]);
            var alt = H(cat["alt"]);
            var label = H(cat["label"]);
            sb.Append(first ? "\t\t\t" : "\t\t\t\t");
            first = false;
            sb.Append("<div class=\"col-xs-6 col-sm-4 col-md-4 col-lg-3 new-cat-block\">\n");
            sb.Append("\t\t\t<a href=\"").Append(href).Append("\" class=\"ucats-h-1 new-cat-block-catalog\">\n");
            sb.Append("\t\t\t\t<div class=\"new-cat-block-catalog-img\" style=\"background:url('").Append(img).Append("') no-repeat; background-position: center; background-size: contain;\" role=\"img\" aria-label=\"").Append(alt).Append("\"></div>\n");
            sb.Append("\t\t\t\t<div class=\"new-cat-block-text navbar-inverse\">").Append(label).Append("</div>\n");
            sb.Append("\t\t\t</a>\n");
            sb.Append("\t\t</div>\n");
        }

        sb.Append("\t\t\t</div>\n\t");
        return sb.ToString();
    }

    public static JsonElement EpcStorefrontLayoutRegistry()
        => JsonDocument.Parse(PhpPlanQ1MoreData.LayoutsJson).RootElement.Clone();

    public static JsonElement EpcStorefrontLayoutsForIndustry(string industry)
    {
        using var doc = JsonDocument.Parse(PhpPlanQ1MoreData.LayoutsJson);
        return doc.RootElement.TryGetProperty(industry, out var layouts)
            ? layouts.Clone()
            : JsonDocument.Parse("[]").RootElement.Clone();
    }

    public static string EpcStorefrontLayoutDefault(string industry)
    {
        foreach (var layout in EpcStorefrontLayoutsForIndustry(industry).EnumerateArray())
        {
            if (layout.TryGetProperty("default", out var def) && def.ValueKind is JsonValueKind.True)
            {
                return layout.GetProperty("id").GetString() ?? "hero_carousel";
            }
        }

        var all = EpcStorefrontLayoutsForIndustry(industry);
        return all.GetArrayLength() > 0 ? all[0].GetProperty("id").GetString() ?? "hero_carousel" : "hero_carousel";
    }

    public static Dictionary<string, object?>? EpcStorefrontLayoutMeta(string industry, string layoutId)
    {
        foreach (var layout in EpcStorefrontLayoutsForIndustry(industry).EnumerateArray())
        {
            if (layout.GetProperty("id").GetString() == layoutId)
            {
                return JsonSerializer.Deserialize<Dictionary<string, object?>>(layout.GetRawText());
            }
        }

        return null;
    }

    public static string[] EpcStorefrontLayoutSections(string industry, string layoutId)
    {
        var meta = EpcStorefrontLayoutMeta(industry, layoutId);
        if (meta is null || !meta.TryGetValue("sections", out var secs) || secs is not JsonElement el || el.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return el.EnumerateArray().Select(x => x.GetString() ?? "").ToArray();
    }

    public static string EpcStorefrontActiveLayout(Dictionary<string, object?>? settings, Func<Dictionary<string, object?>>? loadSettings = null)
    {
        settings ??= loadSettings?.Invoke();
        settings ??= new Dictionary<string, object?>();
        var layout = settings.TryGetValue("storefront_layout", out var raw) && raw is not null
            ? Convert.ToString(raw, CultureInfo.InvariantCulture)?.Trim() ?? ""
            : "";
        if (layout.Length > 0)
        {
            return layout;
        }

        var industry = settings.TryGetValue("industry_code", out var ind) && ind is not null
            ? Convert.ToString(ind, CultureInfo.InvariantCulture) ?? ""
            : "";
        return EpcStorefrontLayoutDefault(industry);
    }

    public static Dictionary<string, List<Dictionary<string, object?>>> EpcStorefrontLayoutsForJs()
    {
        var outDict = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.Ordinal);
        foreach (var industry in EpcStorefrontLayoutRegistry().EnumerateObject())
        {
            var rows = new List<Dictionary<string, object?>>();
            foreach (var layout in industry.Value.EnumerateArray())
            {
                rows.Add(new Dictionary<string, object?>
                {
                    ["id"] = layout.GetProperty("id").GetString(),
                    ["label"] = layout.GetProperty("label").GetString(),
                    ["desc"] = layout.GetProperty("desc").GetString(),
                    ["default"] = layout.TryGetProperty("default", out var def) && def.ValueKind == JsonValueKind.True,
                    ["sections"] = layout.GetProperty("sections").EnumerateArray().Select(s => s.GetString()).ToArray()
                });
            }

            outDict[industry.Name] = rows;
        }

        return outDict;
    }

    public static string? EpcIndustrySubdomainDetect(string? httpHost, string? serverName = null)
    {
        var host = (httpHost ?? "").Trim().ToLowerInvariant();
        if (host.Length > 0 && host.Contains(':', StringComparison.Ordinal))
        {
            host = host.Split(':', 2)[0];
        }

        if (host.Length == 0)
        {
            host = (serverName ?? "").Trim().ToLowerInvariant();
        }

        var m = Regex.Match(host, @"^([a-z0-9][a-z0-9_-]*)\.ecomae\.com$");
        if (!m.Success)
        {
            return null;
        }

        var slug = m.Groups[1].Value;
        return ReservedHosts.Contains(slug) ? null : slug;
    }

    public static bool EpcIsIndustrySubdomain(string? httpHost, string? serverName = null)
        => EpcIndustrySubdomainDetect(httpHost, serverName) is not null;

    public static string EpcIndustrySubdomainResolveGroup(string slug)
    {
        if (DirectGroups.Contains(slug))
        {
            return slug;
        }

        if (SubAliases.TryGetValue(slug, out var aliased))
        {
            return aliased;
        }

        foreach (var group in DirectGroups)
        {
            if (slug.Contains(group, StringComparison.Ordinal))
            {
                return group;
            }
        }

        return "technology";
    }

    public static Dictionary<string, object?> EpcIndustrySubdomainBootstrap(string? httpHost, Dictionary<string, object?> config)
    {
        var slug = EpcIndustrySubdomainDetect(httpHost);
        if (slug is null)
        {
            return new Dictionary<string, object?>
            {
                ["ok"] = false,
                ["active"] = false
            };
        }

        var group = EpcIndustrySubdomainResolveGroup(slug);
        var host = (httpHost ?? "").Trim().ToLowerInvariant();
        if (host.Length > 0 && host.Contains(':', StringComparison.Ordinal))
        {
            host = host.Split(':', 2)[0];
        }

        config["domain_path"] = "https://" + host + "/";
        config["epc_portal_industry"] = group;
        return new Dictionary<string, object?>
        {
            ["ok"] = true,
            ["active"] = true,
            ["slug"] = slug,
            ["group"] = group,
            ["domain_path"] = config["domain_path"],
            ["epc_portal_industry"] = group
        };
    }

    public static string EpcDealerEnsureSchema() => "epc_dealers+epc_dealer_orders";

    public static Dictionary<string, Dictionary<string, object>> EpcDealerTierDiscounts()
        => new(StringComparer.Ordinal)
        {
            ["bronze"] = new Dictionary<string, object>(StringComparer.Ordinal) { ["discount"] = 5, ["min_revenue"] = 0, ["label"] = "Bronze" },
            ["silver"] = new Dictionary<string, object>(StringComparer.Ordinal) { ["discount"] = 10, ["min_revenue"] = 50000, ["label"] = "Silver" },
            ["gold"] = new Dictionary<string, object>(StringComparer.Ordinal) { ["discount"] = 15, ["min_revenue"] = 200000, ["label"] = "Gold" },
            ["platinum"] = new Dictionary<string, object>(StringComparer.Ordinal) { ["discount"] = 20, ["min_revenue"] = 500000, ["label"] = "Platinum" }
        };

    public static Dictionary<string, object?> EpcDealerRegister(DealerStore store, string siteKey, Dictionary<string, object?> data)
    {
        var code = data.TryGetValue("dealer_code", out var rawCode) && rawCode is not null
            ? Convert.ToString(rawCode, CultureInfo.InvariantCulture) ?? ""
            : "DLR-" + Random.Shared.Next(1, 10000).ToString("D4", CultureInfo.InvariantCulture);
        code = code.ToUpperInvariant();
        var tier = data.TryGetValue("tier", out var rawTier) && rawTier is not null
            ? Convert.ToString(rawTier, CultureInfo.InvariantCulture) ?? "bronze"
            : "bronze";
        var tiers = EpcDealerTierDiscounts();
        var discount = tiers.TryGetValue(tier, out var t) ? Convert.ToDouble(t["discount"], CultureInfo.InvariantCulture) : 5;
        var row = new DealerRow
        {
            Id = store.NextDealerId++,
            SiteKey = siteKey,
            DealerCode = code,
            CompanyName = Str(data, "company_name"),
            ContactName = Str(data, "contact_name"),
            Email = Str(data, "email"),
            Phone = Str(data, "phone"),
            Tier = tier,
            DiscountPct = discount,
            CreditLimit = data.TryGetValue("credit_limit", out var cl) && cl is not null ? Convert.ToDouble(cl, CultureInfo.InvariantCulture) : 0,
            Territory = Str(data, "territory")
        };
        store.Dealers.Add(row);
        return new Dictionary<string, object?> { ["ok"] = true, ["dealer_id"] = row.Id, ["dealer_code"] = code };
    }

    public static List<DealerRow> EpcDealerList(DealerStore store, string siteKey, Dictionary<string, object?>? filters = null)
    {
        IEnumerable<DealerRow> q = store.Dealers.Where(d => d.SiteKey == siteKey);
        if (filters is not null && filters.TryGetValue("tier", out var tier) && !IsEmpty(tier))
        {
            q = q.Where(d => d.Tier == Convert.ToString(tier, CultureInfo.InvariantCulture));
        }

        if (filters is not null && filters.TryGetValue("status", out var status) && !IsEmpty(status))
        {
            q = q.Where(d => d.Status == Convert.ToString(status, CultureInfo.InvariantCulture));
        }

        return q.OrderByDescending(d => d.YtdRevenue).ToList();
    }

    public static Dictionary<string, object?> EpcDealerPlaceOrder(DealerStore store, string siteKey, int dealerId, double orderTotal, string? orderRef = null, DateTime? now = null)
    {
        var dealer = store.Dealers.FirstOrDefault(d => d.Id == dealerId && d.SiteKey == siteKey);
        if (dealer is null)
        {
            return new Dictionary<string, object?> { ["ok"] = false, ["error"] = "Dealer not found" };
        }

        if (dealer.Status != "active")
        {
            return new Dictionary<string, object?> { ["ok"] = false, ["error"] = "Dealer not active" };
        }

        var discount = Math.Round(orderTotal * dealer.DiscountPct / 100.0, 2, MidpointRounding.AwayFromZero);
        var net = orderTotal - discount;
        var when = now ?? DateTime.UtcNow;
        var refCode = orderRef ?? ("DO-" + when.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-" + Random.Shared.Next(1, 10000).ToString("D4", CultureInfo.InvariantCulture));
        store.Orders.Add(new DealerOrderRow
        {
            Id = store.NextOrderId++,
            SiteKey = siteKey,
            DealerId = dealerId,
            OrderRef = refCode,
            OrderTotal = orderTotal,
            DiscountApplied = discount,
            NetTotal = net,
            CreatedAt = when.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        });
        dealer.YtdRevenue += net;
        dealer.OrderCount++;
        return new Dictionary<string, object?> { ["ok"] = true, ["order_ref"] = refCode, ["discount"] = discount, ["net_total"] = net };
    }

    public static Dictionary<string, object?> EpcDealerAutoTier(DealerStore store, int dealerId)
    {
        var d = store.Dealers.FirstOrDefault(x => x.Id == dealerId);
        if (d is null)
        {
            return new Dictionary<string, object?> { ["ok"] = false, ["error"] = "Dealer not found" };
        }

        var oldTier = d.Tier;
        var rev = d.YtdRevenue;
        var tiers = EpcDealerTierDiscounts();
        var newTier = "bronze";
        foreach (var t in tiers.Keys.Reverse())
        {
            if (rev >= Convert.ToDouble(tiers[t]["min_revenue"], CultureInfo.InvariantCulture))
            {
                newTier = t;
                break;
            }
        }

        if (newTier != oldTier)
        {
            d.Tier = newTier;
            d.DiscountPct = Convert.ToDouble(tiers[newTier]["discount"], CultureInfo.InvariantCulture);
        }

        return new Dictionary<string, object?> { ["ok"] = true, ["old_tier"] = oldTier, ["new_tier"] = newTier, ["ytd_revenue"] = rev };
    }

    public static List<Dictionary<string, object?>> EpcDealerFleetStats(DealerStore store)
        => store.Dealers
            .GroupBy(d => d.SiteKey, StringComparer.Ordinal)
            .Select(g => new Dictionary<string, object?>
            {
                ["site_key"] = g.Key,
                ["total_dealers"] = g.Count(),
                ["active"] = g.Count(d => d.Status == "active"),
                ["total_revenue"] = g.Sum(d => d.YtdRevenue),
                ["total_orders"] = g.Sum(d => d.OrderCount)
            })
            .OrderByDescending(r => Convert.ToDouble(r["total_revenue"], CultureInfo.InvariantCulture))
            .ToList();

    public static Dictionary<string, object?>? EpcDealerGet(DealerStore store, int dealerId)
    {
        var d = store.Dealers.FirstOrDefault(x => x.Id == dealerId);
        return d is null ? null : ToDict(d);
    }

    public static Dictionary<string, object?> EpcDealerUpdate(DealerStore store, int dealerId, Dictionary<string, object?> data)
    {
        var d = store.Dealers.FirstOrDefault(x => x.Id == dealerId);
        var fields = new List<string>();
        foreach (var f in UpdateFields)
        {
            if (!data.ContainsKey(f))
            {
                continue;
            }

            fields.Add(f);
            if (d is not null)
            {
                ApplyField(d, f, data[f]);
            }
        }

        return fields.Count == 0
            ? new Dictionary<string, object?> { ["ok"] = false, ["error"] = "No fields to update" }
            : new Dictionary<string, object?> { ["ok"] = true };
    }

    public static List<DealerOrderRow> EpcDealerOrders(DealerStore store, int dealerId, int limit = 50)
        => store.Orders.Where(o => o.DealerId == dealerId).OrderByDescending(o => o.CreatedAt).Take(limit).ToList();

    public static Dictionary<string, object?> EpcDealerSuspend(DealerStore store, int dealerId, string reason = "")
    {
        var d = store.Dealers.FirstOrDefault(x => x.Id == dealerId);
        if (d is not null)
        {
            d.Status = "suspended";
        }

        return new Dictionary<string, object?> { ["ok"] = true, ["message"] = "Dealer suspended" };
    }

    public static Dictionary<string, object?> EpcDealerActivate(DealerStore store, int dealerId)
    {
        var d = store.Dealers.FirstOrDefault(x => x.Id == dealerId);
        if (d is not null)
        {
            d.Status = "active";
        }

        return new Dictionary<string, object?> { ["ok"] = true };
    }

    public static List<Dictionary<string, object?>> EpcDealerPerformanceReport(DealerStore store, string siteKey)
        => store.Dealers
            .Where(d => d.SiteKey == siteKey && d.Status == "active")
            .OrderByDescending(d => d.YtdRevenue)
            .Select(d => new Dictionary<string, object?>
            {
                ["id"] = d.Id,
                ["company_name"] = d.CompanyName,
                ["tier"] = d.Tier,
                ["ytd_revenue"] = d.YtdRevenue,
                ["order_count"] = d.OrderCount,
                ["discount_pct"] = d.DiscountPct,
                ["last_order"] = store.Orders.Where(o => o.DealerId == d.Id).Select(o => o.CreatedAt).DefaultIfEmpty().Max()
            })
            .ToList();

    public static string EpcSocialMediaHubCss() => PhpPlanQ1MoreData.SocialHubCss;

    public static string[] EpcSocialMediaHubCssHeaders() => Array.Empty<string>();

    private static Dictionary<string, string> Cat(string label, string href, string photo, string alt, Func<string, bool>? localExists)
        => new(StringComparer.Ordinal)
        {
            ["label"] = label,
            ["href"] = href,
            ["image"] = EpcPortalIndustryCatalogUnsplash(photo, 400, 400, localExists),
            ["alt"] = alt
        };

    private static string H(string value)
        => value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private static string Str(Dictionary<string, object?> data, string key)
        => data.TryGetValue(key, out var raw) && raw is not null ? Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "" : "";

    private static bool IsEmpty(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is string s)
        {
            return s.Length == 0 || s == "0";
        }

        if (value is int i)
        {
            return i == 0;
        }

        return false;
    }

    private static Dictionary<string, object?> ToDict(DealerRow d)
        => new(StringComparer.Ordinal)
        {
            ["id"] = d.Id,
            ["site_key"] = d.SiteKey,
            ["dealer_code"] = d.DealerCode,
            ["company_name"] = d.CompanyName,
            ["contact_name"] = d.ContactName,
            ["email"] = d.Email,
            ["phone"] = d.Phone,
            ["tier"] = d.Tier,
            ["discount_pct"] = d.DiscountPct,
            ["credit_limit"] = d.CreditLimit,
            ["territory"] = d.Territory,
            ["status"] = d.Status,
            ["ytd_revenue"] = d.YtdRevenue,
            ["order_count"] = d.OrderCount
        };

    private static void ApplyField(DealerRow d, string field, object? value)
    {
        switch (field)
        {
            case "company_name":
                d.CompanyName = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                break;
            case "phone":
                d.Phone = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                break;
            case "tier":
                d.Tier = Convert.ToString(value, CultureInfo.InvariantCulture) ?? d.Tier;
                break;
            case "status":
                d.Status = Convert.ToString(value, CultureInfo.InvariantCulture) ?? d.Status;
                break;
            case "discount_pct":
                d.DiscountPct = value is null ? d.DiscountPct : Convert.ToDouble(value, CultureInfo.InvariantCulture);
                break;
            case "credit_limit":
                d.CreditLimit = value is null ? d.CreditLimit : Convert.ToDouble(value, CultureInfo.InvariantCulture);
                break;
            default:
                d.Extra[field] = value;
                break;
        }
    }
}
