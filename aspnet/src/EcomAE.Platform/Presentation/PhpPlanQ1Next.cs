using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Storefront;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-next named helpers. PHP identifiers kept for the inventory:
/// <c>epc_portal_storefront_hub_logo_setting</c>, <c>epc_portal_storefront_hub_enabled</c>,
/// <c>epc_portal_storefront_hub_logo_enqueue</c>, <c>epc_portal_storefront_logo_show_trade_label</c>,
/// <c>epc_portal_storefront_logo_markup</c>, <c>epc_portal_storefront_epartscart_svg_markup</c>,
/// <c>epc_industry_ensure_schema</c>, <c>epc_industry_builtin_packs</c>,
/// <c>epc_industry_seed_packs</c>, <c>epc_industry_assign_pack</c>,
/// <c>epc_industry_tenant_packs</c>, <c>epc_industry_fleet_stats</c>,
/// <c>epc_promo_ensure_schema</c>, <c>epc_promo_create</c>, <c>epc_promo_list</c>,
/// <c>epc_promo_apply</c>, <c>epc_promo_record_usage</c>, <c>epc_promo_fleet_stats</c>.
/// </summary>
public static class PhpPlanQ1Next
{
    public const string StorefrontLogoPath = "content/general_pages/epc_portal_storefront_logo.php";
    public const string IndustryPacksPath = "content/general_pages/epc_industry_packs.php";
    public const string PromotionsEnginePath = "content/general_pages/epc_promotions_engine.php";
    public const string IndustryPacksVersion = "1.0.0";
    public const string PromotionsVersion = "1.0.0";

    public static readonly string EpartsCartSvgMarkup = "<span class=\"epc-animated-logo\" aria-label=\"EpartsCart\">\n\t<span class=\"epc-animated-logo__text\">eparts</span>\n\t<span class=\"epc-animated-logo__mark\" aria-hidden=\"true\">\n\t\t<svg viewBox=\"0 0 220 100\" xmlns=\"http://www.w3.org/2000/svg\" focusable=\"false\">\n\t\t\t<path class=\"epc-logo-speed epc-logo-speed--one\" d=\"M10 28 H72\" />\n\t\t\t<path class=\"epc-logo-speed epc-logo-speed--two\" d=\"M0 48 H68\" />\n\t\t\t<path class=\"epc-logo-speed epc-logo-speed--three\" d=\"M20 68 H76\" />\n\t\t\t<path class=\"epc-logo-road\" d=\"M70 96 H190\" />\n\t\t\t<g class=\"epc-logo-cart-motion\">\n\t\t\t\t<path class=\"epc-logo-cart\" d=\"M66 18 H178 C186 18 192 25 190 33 L177 70 H83 L66 18 Z\" />\n\t\t\t\t<path class=\"epc-logo-handle\" d=\"M64 18 L52 18 L43 10\" />\n\t\t\t\t<path class=\"epc-logo-basket\" d=\"M82 32 H172 L163 58 H92 Z\" />\n\t\t\t\t<g class=\"epc-logo-parts\">\n\t\t\t\t\t<g class=\"epc-logo-gear\" transform=\"translate(126 48)\">\n\t\t\t\t\t\t<path d=\"M0 -18 L4 -13 L10 -15 L12 -9 L18 -7 L15 -1 L18 5 L12 8 L10 15 L3 13 L-2 18 L-7 13 L-14 15 L-15 8 L-20 5 L-17 -1 L-20 -7 L-15 -9 L-14 -15 L-7 -13 Z\" />\n\t\t\t\t\t\t<circle r=\"12\" />\n\t\t\t\t\t\t<circle r=\"5\" class=\"epc-logo-gear-hole\" />\n\t\t\t\t\t</g>\n\t\t\t\t\t<g class=\"epc-logo-piston\" transform=\"translate(98 39)\">\n\t\t\t\t\t\t<rect x=\"0\" y=\"0\" width=\"24\" height=\"18\" rx=\"4\" />\n\t\t\t\t\t\t<path d=\"M3 5 H21 M3 10 H21\" />\n\t\t\t\t\t\t<path d=\"M12 18 V31\" />\n\t\t\t\t\t</g>\n\t\t\t\t\t<g class=\"epc-logo-ring\" transform=\"translate(152 48)\">\n\t\t\t\t\t\t<circle r=\"12\" />\n\t\t\t\t\t\t<path d=\"M8 -8 L16 -15\" />\n\t\t\t\t\t</g>\n\t\t\t\t\t<rect class=\"epc-logo-box\" x=\"137\" y=\"31\" width=\"24\" height=\"17\" rx=\"4\" />\n\t\t\t\t</g>\n\t\t\t\t<g class=\"epc-logo-wheel epc-logo-wheel--left\" transform=\"translate(86 88)\">\n\t\t\t\t\t<g class=\"epc-logo-wheel-spin\">\n\t\t\t\t\t\t<circle class=\"epc-logo-tyre\" r=\"16\" />\n\t\t\t\t\t\t<circle class=\"epc-logo-wheel-rim\" r=\"10\" />\n\t\t\t\t\t\t<circle class=\"epc-logo-wheel-hole\" r=\"4\" />\n\t\t\t\t\t\t<path class=\"epc-logo-wheel-spokes\" d=\"M0 -10 V10 M-10 0 H10 M-7 -7 L7 7 M7 -7 L-7 7\" />\n\t\t\t\t\t\t<path class=\"epc-logo-wheel-tread\" d=\"M-5 -15 L-2 -11 M5 -15 L2 -11 M15 -5 L11 -2 M15 5 L11 2 M5 15 L2 11 M-5 15 L-2 11 M-15 5 L-11 2 M-15 -5 L-11 -2\" />\n\t\t\t\t\t</g>\n\t\t\t\t</g>\n\t\t\t\t<g class=\"epc-logo-wheel epc-logo-wheel--right\" transform=\"translate(166 88)\">\n\t\t\t\t\t<g class=\"epc-logo-wheel-spin\">\n\t\t\t\t\t\t<circle class=\"epc-logo-tyre\" r=\"16\" />\n\t\t\t\t\t\t<circle class=\"epc-logo-wheel-rim\" r=\"10\" />\n\t\t\t\t\t\t<circle class=\"epc-logo-wheel-hole\" r=\"4\" />\n\t\t\t\t\t\t<path class=\"epc-logo-wheel-spokes\" d=\"M0 -10 V10 M-10 0 H10 M-7 -7 L7 7 M7 -7 L-7 7\" />\n\t\t\t\t\t\t<path class=\"epc-logo-wheel-tread\" d=\"M-5 -15 L-2 -11 M5 -15 L2 -11 M15 -5 L11 -2 M15 5 L11 2 M5 15 L2 11 M-5 15 L-2 11 M-15 5 L-11 2 M-15 -5 L-11 -2\" />\n\t\t\t\t\t</g>\n\t\t\t\t</g>\n\t\t\t</g>\n\t\t</svg>\n\t</span>\n</span>\n\t";

    public sealed class StorefrontLogoCtx
    {
        public Dictionary<string, object?> Settings { get; init; } = new(StringComparer.Ordinal);
        public bool DemoStorefront { get; init; }
        public bool OperatorHost { get; init; }
        public bool CommerceEnabled { get; init; }
        public bool MandatoryLine { get; init; }
        public bool TenantBrandEnabled { get; init; }
        public string Package { get; init; } = "";
        public string TradeName { get; init; } = "";
        public string? Industry { get; init; }
        public string HubHtml { get; init; } = "HUB";
        public string AnimatedHtml { get; init; } = "";
        public string TenantBrandHtml { get; init; } = "TENANT";
        public string TenantBrandEnqueue { get; init; } = "TENANT_ENQ";
        public string HubEnqueue { get; init; } = "HUB_ENQ";
    }

    public sealed class IndustryAssignment
    {
        public int Id { get; set; }
        public string SiteKey { get; set; } = "";
        public string PackKey { get; set; } = "";
        public int AppliedBy { get; set; }
    }

    public sealed class PromoRow
    {
        public int Id { get; set; }
        public string SiteKey { get; set; } = "";
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
        public string Type { get; set; } = "percentage";
        public double Value { get; set; }
        public double MinOrder { get; set; }
        public double MaxDiscount { get; set; }
        public string StartDate { get; set; } = "";
        public string EndDate { get; set; } = "";
        public int UsageLimit { get; set; }
        public int PerCustomer { get; set; }
        public int UsedCount { get; set; }
        public int Stackable { get; set; }
        public int Priority { get; set; }
        public object? Conditions { get; set; }
        public object? AppliesTo { get; set; }
        public object? CustomerSegments { get; set; }
        public int Active { get; set; } = 1;
        public int CreatedBy { get; set; }
        public List<int> UsageCustomerIds { get; } = new();
    }

    public static bool? EpcPortalStorefrontHubLogoSetting(StorefrontLogoCtx ctx)
    {
        if (!ctx.Settings.TryGetValue("contact", out var contactObj) || contactObj is not IDictionary<string, object?> contact)
        {
            return null;
        }

        if (!contact.ContainsKey("use_animated_hub_logo"))
        {
            return null;
        }

        return !PhpEmpty(contact["use_animated_hub_logo"]);
    }

    public static bool EpcPortalStorefrontHubEnabled(StorefrontLogoCtx ctx)
    {
        if (ctx.DemoStorefront)
        {
            var demo = EpcPortalStorefrontHubLogoSetting(ctx);
            return demo ?? false;
        }

        if (ctx.OperatorHost)
        {
            return true;
        }

        var explicitFlag = EpcPortalStorefrontHubLogoSetting(ctx);
        if (explicitFlag is not null)
        {
            return explicitFlag.Value;
        }

        if (!ctx.CommerceEnabled)
        {
            return true;
        }

        return ctx.MandatoryLine;
    }

    public static string EpcPortalStorefrontHubLogoEnqueue(StorefrontLogoCtx ctx)
    {
        if (ctx.TenantBrandEnabled)
        {
            return ctx.TenantBrandEnqueue;
        }

        return EpcPortalStorefrontHubEnabled(ctx) ? ctx.HubEnqueue : "";
    }

    public static bool EpcPortalStorefrontLogoShowTradeLabel(StorefrontLogoCtx ctx)
        => EpcPortalStorefrontHubEnabled(ctx) && !ctx.OperatorHost;

    public static string EpcPortalStorefrontLogoMarkup(StorefrontLogoCtx ctx)
    {
        var pkg = ctx.Package ?? "";
        if (pkg.Length > 0 && pkg != "automotive_spareparts_pro")
        {
            var animated = ctx.AnimatedHtml ?? "";
            if (animated.Length > 0)
            {
                return animated;
            }
        }

        if (EpcPortalStorefrontHubEnabled(ctx))
        {
            var label = ctx.TradeName ?? "";
            var hub = ctx.HubHtml ?? "";
            if (!EpcPortalStorefrontLogoShowTradeLabel(ctx))
            {
                return "<span class=\"epc-storefront-logo epc-storefront-logo--hub-only\">" + hub + "</span>";
            }

            return "<span class=\"epc-storefront-logo epc-storefront-logo--hub\">"
                + "<span class=\"epc-storefront-logo__hub\">" + hub + "</span>"
                + "<span class=\"epc-storefront-logo__label\">" + H(label) + "</span>"
                + "</span>";
        }

        if (ctx.TenantBrandEnabled)
        {
            return ctx.TenantBrandHtml ?? "";
        }

        var isParts = ctx.Industry == "auto_parts" && ctx.CommerceEnabled;
        if (isParts)
        {
            return EpcPortalStorefrontEpartscartSvgMarkup();
        }

        if (ctx.Industry == "electronics" && pkg == "electronics_retail_virgin")
        {
            var elabel = ctx.TradeName ?? "";
            if (elabel.Length == 0 || elabel.Contains("epart", StringComparison.OrdinalIgnoreCase))
            {
                elabel = "Electronicae";
            }

            return "<span class=\"epc-text-logo epc-text-logo--electronics\" aria-label=\"" + H(elabel) + "\">"
                + H(elabel) + "</span>";
        }

        var text = ctx.TradeName ?? "";
        return "<span class=\"epc-text-logo\" aria-label=\"" + H(text) + "\">" + H(text) + "</span>";
    }

    public static string EpcPortalStorefrontEpartscartSvgMarkup() => EpartsCartSvgMarkup;

    public static string EpcIndustryPacksVersion() => IndustryPacksVersion;

    public static Dictionary<string, Dictionary<string, object?>> EpcIndustryBuiltinPacks()
        => new(StringComparer.Ordinal)
        {
            ["auto_parts"] = Pack(
                "Auto Parts & Accessories",
                "fa-car",
                "Full auto-parts vertical: OEM/aftermarket, VIN lookup, fitment data, core deposit tracking.",
                new[] { "catalog", "inventory", "orders", "fulfillment", "warranty_rma", "dealer_portal", "credit_limit", "po_approval", "inventory_forecast" },
                new[]
                {
                    Gl("1000", "Cash & Bank", "asset"),
                    Gl("1200", "Accounts Receivable", "asset"),
                    Gl("1300", "Inventory - Parts", "asset"),
                    Gl("1310", "Inventory - Core Deposits", "asset"),
                    Gl("2000", "Accounts Payable", "liability"),
                    Gl("2100", "Core Deposit Liability", "liability"),
                    Gl("4000", "Parts Revenue", "revenue"),
                    Gl("5000", "Cost of Goods - Parts", "expense"),
                    Gl("5100", "Freight & Shipping", "expense"),
                    Gl("6000", "Warranty Claims Expense", "expense")
                },
                new[] { "oem_number", "brand", "fitment", "vehicle_make", "vehicle_model", "year_range", "core_charge", "weight_kg", "condition" },
                Tax(5, Array.Empty<string>()),
                Theme("#1565C0", "#FF6F00", "Auto Parts")),
            ["fashion"] = Pack(
                "Fashion & Apparel",
                "fa-shopping-bag",
                "Fashion vertical: size/color matrix, seasonal collections, lookbooks, returns management.",
                new[] { "catalog", "inventory", "orders", "fulfillment", "warranty_rma", "promotions", "collections_dunning" },
                new[]
                {
                    Gl("1000", "Cash & Bank", "asset"),
                    Gl("1200", "Accounts Receivable", "asset"),
                    Gl("1300", "Inventory - Apparel", "asset"),
                    Gl("2000", "Accounts Payable", "liability"),
                    Gl("4000", "Apparel Revenue", "revenue"),
                    Gl("4100", "Returns & Allowances", "revenue"),
                    Gl("5000", "Cost of Goods - Apparel", "expense")
                },
                new[] { "size", "color", "material", "collection", "season", "gender", "style_code", "care_instructions" },
                Tax(5, new[] { "children_clothing" }),
                Theme("#AD1457", "#F06292", "Fashion")),
            ["electronics"] = Pack(
                "Electronics & Technology",
                "fa-microchip",
                "Electronics vertical: serial tracking, warranty registration, RMA, compatibility matrix.",
                new[] { "catalog", "inventory", "orders", "fulfillment", "warranty_rma", "ai_classification", "credit_limit", "dealer_portal" },
                new[]
                {
                    Gl("1000", "Cash & Bank", "asset"),
                    Gl("1200", "Accounts Receivable", "asset"),
                    Gl("1300", "Inventory - Electronics", "asset"),
                    Gl("2000", "Accounts Payable", "liability"),
                    Gl("4000", "Electronics Revenue", "revenue"),
                    Gl("5000", "Cost of Goods - Electronics", "expense"),
                    Gl("5200", "Extended Warranty Revenue", "revenue")
                },
                new[] { "model_number", "brand", "serial_number", "ean", "warranty_months", "voltage", "wattage", "connectivity", "compatibility" },
                Tax(5, Array.Empty<string>()),
                Theme("#0D47A1", "#00BCD4", "Electronics")),
            ["jewellery"] = Pack(
                "Jewellery & Precious Metals",
                "fa-diamond",
                "Jewellery vertical: karat/purity tracking, hallmark, stone certification, gold rate integration.",
                new[] { "catalog", "inventory", "orders", "warranty_rma", "multi_currency_gl", "collections_dunning" },
                new[]
                {
                    Gl("1000", "Cash & Bank", "asset"),
                    Gl("1200", "Accounts Receivable", "asset"),
                    Gl("1300", "Inventory - Gold", "asset"),
                    Gl("1310", "Inventory - Diamonds", "asset"),
                    Gl("1320", "Inventory - Silver", "asset"),
                    Gl("2000", "Accounts Payable", "liability"),
                    Gl("4000", "Jewellery Sales", "revenue"),
                    Gl("5000", "Cost of Gold", "expense"),
                    Gl("5100", "Cost of Stones", "expense"),
                    Gl("5200", "Making Charges", "expense")
                },
                new[] { "karat", "purity", "weight_gm", "stone_type", "stone_carat", "hallmark_number", "certificate_number", "metal_type", "making_charge" },
                Tax(5, new[] { "investment_gold" }),
                Theme("#BF360C", "#FFD600", "Jewellery"))
        };

    public static string EpcIndustryEnsureSchema()
        => "epc_industry_packs+epc_tenant_pack_assignments";

    public static int EpcIndustrySeedPacks(ISet<string> existingKeys)
    {
        var inserted = 0;
        foreach (var key in EpcIndustryBuiltinPacks().Keys)
        {
            if (!existingKeys.Contains(key))
            {
                existingKeys.Add(key);
                inserted++;
            }
        }

        return inserted;
    }

    public static Dictionary<string, object?> EpcIndustryAssignPack(IList<IndustryAssignment> rows, string siteKey, string packKey, int userId, ref int nextId)
    {
        if (nextId <= 0)
        {
            nextId = 1;
        }

        var hit = rows.FirstOrDefault(r => r.SiteKey == siteKey && r.PackKey == packKey);
        if (hit is null)
        {
            rows.Add(new IndustryAssignment
            {
                Id = nextId,
                SiteKey = siteKey,
                PackKey = packKey,
                AppliedBy = userId
            });
        }

        nextId++;
        return new Dictionary<string, object?>
        {
            ["ok"] = true,
            ["site_key"] = siteKey,
            ["pack_key"] = packKey
        };
    }

    public static List<Dictionary<string, object?>> EpcIndustryTenantPacks(IEnumerable<IndustryAssignment> rows, string siteKey)
    {
        var packs = EpcIndustryBuiltinPacks();
        return rows.Where(r => r.SiteKey == siteKey)
            .OrderBy(r => r.PackKey, StringComparer.Ordinal)
            .Select(row =>
            {
                packs.TryGetValue(row.PackKey, out var pack);
                return new Dictionary<string, object?>
                {
                    ["id"] = row.Id,
                    ["site_key"] = row.SiteKey,
                    ["pack_key"] = row.PackKey,
                    ["applied_by"] = row.AppliedBy,
                    ["name"] = pack is null ? "" : pack["name"],
                    ["icon"] = pack is null ? "" : pack["icon"],
                    ["description"] = pack is null ? "" : pack["description"]
                };
            })
            .ToList();
    }

    public static List<Dictionary<string, object?>> EpcIndustryFleetStats(IEnumerable<IndustryAssignment> rows)
        => rows.GroupBy(r => r.PackKey, StringComparer.Ordinal)
            .Select(g => new Dictionary<string, object?> { ["pack_key"] = g.Key, ["tenants"] = g.Count() })
            .OrderByDescending(r => Convert.ToInt32(r["tenants"], CultureInfo.InvariantCulture))
            .ToList();

    public static string EpcPromoEnsureSchema() => "epc_promotions+epc_promotion_usage";

    public static string EpcPromoVersion() => PromotionsVersion;

    public static Dictionary<string, object?> EpcPromoCreate(IList<PromoRow> rows, string siteKey, Dictionary<string, object?> data)
    {
        var row = new PromoRow
        {
            Id = rows.Count == 0 ? 1 : rows.Max(r => r.Id) + 1,
            SiteKey = siteKey,
            Name = Str(data, "name"),
            Code = Str(data, "code").ToUpperInvariant(),
            Type = Str(data, "type", "percentage"),
            Value = Flt(data, "value"),
            MinOrder = Flt(data, "min_order"),
            MaxDiscount = Flt(data, "max_discount"),
            StartDate = Str(data, "start_date", DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            EndDate = Str(data, "end_date", DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            UsageLimit = Int(data, "usage_limit"),
            PerCustomer = Int(data, "per_customer"),
            Stackable = Int(data, "stackable"),
            Priority = Int(data, "priority"),
            Conditions = data.TryGetValue("conditions", out var c) ? c : Array.Empty<object>(),
            AppliesTo = data.TryGetValue("applies_to", out var a) ? a : Array.Empty<object>(),
            CustomerSegments = data.TryGetValue("customer_segments", out var s) ? s : Array.Empty<object>(),
            CreatedBy = Int(data, "created_by"),
            Active = 1
        };
        rows.Add(row);
        return new Dictionary<string, object?> { ["ok"] = true, ["promotion_id"] = row.Id };
    }

    public static List<Dictionary<string, object?>> EpcPromoList(IEnumerable<PromoRow> rows, string siteKey, bool activeOnly, DateTime utcNow)
    {
        var now = utcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var list = rows.Where(r => r.SiteKey == siteKey).ToList();
        if (activeOnly)
        {
            list = list.Where(r => r.Active == 1 && string.CompareOrdinal(r.StartDate, now) <= 0 && string.CompareOrdinal(r.EndDate, now) >= 0).ToList();
        }

        return list
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.Id)
            .Select(PromoDict)
            .ToList();
    }

    public static Dictionary<string, object?> EpcPromoApply(IEnumerable<PromoRow> rows, string siteKey, string code, double orderTotal, int customerId = 0, DateTime? utcNow = null)
    {
        var now = (utcNow ?? DateTime.UtcNow).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var promo = rows.FirstOrDefault(r =>
            r.SiteKey == siteKey
            && r.Code == (code ?? "").ToUpperInvariant()
            && r.Active == 1
            && string.CompareOrdinal(r.StartDate, now) <= 0
            && string.CompareOrdinal(r.EndDate, now) >= 0);
        return EpcPromoApplyRow(promo, orderTotal, customerId);
    }

    public static Dictionary<string, object?> EpcPromoApplyRow(PromoRow? promo, double orderTotal, int customerId = 0)
    {
        if (promo is null)
        {
            return new Dictionary<string, object?> { ["ok"] = false, ["error"] = "Invalid or expired promotion code" };
        }

        if (promo.MinOrder > 0 && orderTotal < promo.MinOrder)
        {
            return new Dictionary<string, object?>
            {
                ["ok"] = false,
                ["error"] = "Minimum order " + promo.MinOrder.ToString("N2", CultureInfo.GetCultureInfo("en-US")) + " not met"
            };
        }

        if (promo.UsageLimit > 0 && promo.UsedCount >= promo.UsageLimit)
        {
            return new Dictionary<string, object?> { ["ok"] = false, ["error"] = "Promotion usage limit reached" };
        }

        if (promo.PerCustomer > 0 && customerId > 0)
        {
            var used = promo.UsageCustomerIds.Count(id => id == customerId);
            if (used >= promo.PerCustomer)
            {
                return new Dictionary<string, object?> { ["ok"] = false, ["error"] = "Per-customer usage limit reached" };
            }
        }

        double discount = promo.Type switch
        {
            "percentage" => Math.Round(orderTotal * promo.Value / 100.0, 2, MidpointRounding.AwayFromZero),
            "fixed" => promo.Value,
            "free_shipping" => 0,
            "bogo" => Math.Round(orderTotal * 0.5, 2, MidpointRounding.AwayFromZero),
            _ => Math.Round(orderTotal * promo.Value / 100.0, 2, MidpointRounding.AwayFromZero)
        };

        if (promo.MaxDiscount > 0)
        {
            discount = Math.Min(discount, promo.MaxDiscount);
        }

        discount = Math.Min(discount, orderTotal);
        return new Dictionary<string, object?>
        {
            ["ok"] = true,
            ["promotion_id"] = promo.Id,
            ["name"] = promo.Name,
            ["type"] = promo.Type,
            ["discount"] = discount,
            ["new_total"] = Math.Round(orderTotal - discount, 2, MidpointRounding.AwayFromZero),
            ["free_shipping"] = promo.Type == "free_shipping"
        };
    }

    public static Dictionary<string, object?> EpcPromoRecordUsage(PromoRow promo, string siteKey, int customerId, string orderRef, double discount)
    {
        promo.UsageCustomerIds.Add(customerId);
        promo.UsedCount++;
        _ = siteKey;
        _ = orderRef;
        _ = discount;
        return new Dictionary<string, object?> { ["ok"] = true };
    }

    public static List<Dictionary<string, object?>> EpcPromoFleetStats(IEnumerable<PromoRow> rows, DateTime utcNow)
    {
        var now = utcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        return rows.GroupBy(r => r.SiteKey, StringComparer.Ordinal)
            .Select(g => new Dictionary<string, object?>
            {
                ["site_key"] = g.Key,
                ["promotions"] = g.Count(),
                ["active"] = g.Count(r => r.Active == 1 && string.CompareOrdinal(r.StartDate, now) <= 0 && string.CompareOrdinal(r.EndDate, now) >= 0),
                ["total_usage"] = g.Sum(r => r.UsedCount)
            })
            .OrderBy(r => (string)r["site_key"]!, StringComparer.Ordinal)
            .ToList();
    }

    private static Dictionary<string, object?> PromoDict(PromoRow row)
        => new()
        {
            ["id"] = row.Id,
            ["site_key"] = row.SiteKey,
            ["name"] = row.Name,
            ["code"] = row.Code,
            ["type"] = row.Type,
            ["value"] = row.Value,
            ["min_order"] = row.MinOrder,
            ["max_discount"] = row.MaxDiscount,
            ["start_date"] = row.StartDate,
            ["end_date"] = row.EndDate,
            ["usage_limit"] = row.UsageLimit,
            ["per_customer"] = row.PerCustomer,
            ["used_count"] = row.UsedCount,
            ["stackable"] = row.Stackable,
            ["priority"] = row.Priority,
            ["conditions"] = row.Conditions ?? Array.Empty<object>(),
            ["applies_to"] = row.AppliesTo ?? Array.Empty<object>(),
            ["customer_segments"] = row.CustomerSegments ?? Array.Empty<object>(),
            ["active"] = row.Active,
            ["created_by"] = row.CreatedBy
        };

    private static Dictionary<string, object?> Pack(string name, string icon, string description, string[] modules, Dictionary<string, string>[] gl, string[] attrs, Dictionary<string, object?> tax, Dictionary<string, string> theme)
        => new()
        {
            ["name"] = name,
            ["icon"] = icon,
            ["description"] = description,
            ["modules"] = modules,
            ["gl_template"] = gl,
            ["product_attrs"] = attrs,
            ["tax_rules"] = tax,
            ["theme"] = theme
        };

    private static Dictionary<string, string> Gl(string code, string name, string type)
        => new() { ["code"] = code, ["name"] = name, ["type"] = type };

    private static Dictionary<string, object?> Tax(int vat, string[] exempt)
        => new() { ["vat_rate"] = vat, ["exempt_categories"] = exempt };

    private static Dictionary<string, string> Theme(string primary, string accent, string badge)
        => new() { ["primary"] = primary, ["accent"] = accent, ["industry_badge"] = badge };

    private static bool PhpEmpty(object? value)
    {
        if (value is null or false)
        {
            return true;
        }

        switch (value)
        {
            case string s:
                return s.Length == 0 || s == "0";
            case int i:
                return i == 0;
            case long l:
                return l == 0;
            case double d:
                return d == 0;
            case float f:
                return f == 0;
            case decimal m:
                return m == 0;
            case JsonElement el:
                return el.ValueKind is JsonValueKind.Null or JsonValueKind.False
                    || (el.ValueKind == JsonValueKind.String && (el.GetString() is "" or "0"))
                    || (el.ValueKind == JsonValueKind.Number && el.GetDouble() == 0)
                    || (el.ValueKind == JsonValueKind.Array && el.GetArrayLength() == 0);
            case System.Collections.ICollection col:
                return col.Count == 0;
            default:
                return false;
        }
    }

    private static string Str(Dictionary<string, object?> data, string key, string fallback = "")
        => data.TryGetValue(key, out var value) && value is not null ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback : fallback;

    private static int Int(Dictionary<string, object?> data, string key)
        => data.TryGetValue(key, out var value) && value is not null ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;

    private static double Flt(Dictionary<string, object?> data, string key)
        => data.TryGetValue(key, out var value) && value is not null ? Convert.ToDouble(value, CultureInfo.InvariantCulture) : 0;

    private static string H(string? value) => StorefrontTinyPages.HtmlSpecialChars(value);
}
