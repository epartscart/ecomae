using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-note helpers. PHP identifiers kept for the inventory:
/// <c>epc_storefront_seed_resolve_tenant_locale</c>, <c>epc_storefront_seed_fx_rate</c>,
/// <c>epc_storefront_seed_convert_price</c>, <c>epc_storefront_seed_all</c>,
/// <c>epc_storefront_seed_upsert_category</c>, <c>epc_storefront_seed_upsert_product</c>,
/// <c>epc_storefront_seed_generic_catalog</c>, <c>epc_storefront_seed_category_tree</c>,
/// <c>epc_storefront_seed_categories_electronics</c>, <c>epc_storefront_seed_categories_fashion</c>,
/// <c>epc_storefront_seed_categories_consulting</c>, <c>epc_storefront_seed_categories_jewellery</c>,
/// <c>epc_storefront_seed_product_catalog</c>, <c>epc_storefront_seed_products_electronics</c>,
/// <c>epc_storefront_seed_products_fashion</c>, <c>epc_storefront_seed_products_consulting</c>,
/// <c>epc_storefront_seed_products_jewellery</c>.
/// </summary>
public static class PhpPlanQ1Note
{
    public const string SeedDataPath = "content/general_pages/epc_storefront_seed_data.php";

    public static Func<long> Clock { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static readonly Lazy<JsonElement> Seed = new(() => JsonDocument.Parse(PhpPlanQ1NoteJson.SeedJson).RootElement.Clone());

    private static readonly Dictionary<string, double> Fx = new(StringComparer.Ordinal)
    {
        ["USD"] = 1.0, ["AED"] = 3.67, ["SAR"] = 3.75, ["QAR"] = 3.64, ["OMR"] = 0.385,
        ["BHD"] = 0.376, ["KWD"] = 0.308, ["GBP"] = 0.79, ["EUR"] = 0.92, ["INR"] = 83.5,
        ["PKR"] = 278.0, ["BDT"] = 110.0, ["LKR"] = 320.0, ["JPY"] = 155.0, ["KRW"] = 1350.0,
        ["CNY"] = 7.25, ["AUD"] = 1.55, ["NZD"] = 1.68, ["SGD"] = 1.35, ["CAD"] = 1.37,
        ["MXN"] = 17.5, ["BRL"] = 5.0, ["ZAR"] = 18.5, ["NGN"] = 1550.0, ["KES"] = 155.0,
        ["EGP"] = 48.0, ["SEK"] = 10.8, ["NOK"] = 10.7, ["DKK"] = 6.9, ["PLN"] = 4.0,
        ["CZK"] = 23.0, ["HUF"] = 365.0, ["RON"] = 4.6, ["CHF"] = 0.88, ["IQD"] = 1310.0,
        ["JOD"] = 0.709, ["LBP"] = 89500.0, ["MAD"] = 10.0, ["GHS"] = 15.0, ["TZS"] = 2700.0,
        ["NPR"] = 133.5, ["COP"] = 3950.0, ["ARS"] = 870.0, ["CLP"] = 940.0
    };

    public static void Reset() => Clock = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public sealed class SeedStore
    {
        public int NextCategoryId { get; set; } = 1;
        public int NextProductId { get; set; } = 1;
        public List<CatRow> Categories { get; } = new();
        public List<ProdRow> Products { get; } = new();
        public List<(int ProductId, int CategoryId)> ProductCategories { get; } = new();
        public List<(int ProductId, string ImageUrl)> Images { get; } = new();
    }

    public sealed class CatRow
    {
        public int Id { get; set; }
        public string Alias { get; set; } = "";
        public string Url { get; set; } = "";
        public string Value { get; set; } = "";
        public int Level { get; set; }
        public int Order { get; set; }
        public int Parent { get; set; }
        public int PublishedFlag { get; set; } = 1;
    }

    public sealed class ProdRow
    {
        public int Id { get; set; }
        public string Caption { get; set; } = "";
        public string Alias { get; set; } = "";
        public double Price { get; set; }
        public int TimeCreated { get; set; }
        public int PublishedFlag { get; set; } = 1;
    }

    public static Dictionary<string, object?> EpcStorefrontSeedResolveTenantLocale(SeedStore? db = null)
        => new(StringComparer.Ordinal) { ["country"] = "AE", ["currency"] = "AED", ["tax_label"] = "VAT", ["region"] = "gcc" };

    public static double EpcStorefrontSeedFxRate(string currency)
        => Fx.TryGetValue(currency, out var rate) ? rate : 1.0;

    public static double EpcStorefrontSeedConvertPrice(double usdPrice, string currency)
        => Math.Round(usdPrice * EpcStorefrontSeedFxRate(currency), 0, MidpointRounding.AwayFromZero);

    public static Dictionary<string, object?> EpcStorefrontSeedAll(SeedStore db, string industry, string siteKey)
    {
        var locale = EpcStorefrontSeedResolveTenantLocale(db);
        var report = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["industry"] = industry,
            ["site_key"] = siteKey,
            ["currency"] = locale["currency"],
            ["region"] = locale["region"],
            ["categories"] = 0,
            ["products"] = 0,
            ["errors"] = new List<string>()
        };
        var catTree = EpcStorefrontSeedCategoryTree(industry, locale);
        var products = EpcStorefrontSeedProductCatalog(industry, locale);
        var catIdMap = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var cat in catTree)
        {
            try
            {
                var parentId = 0;
                var parentAlias = Str(cat, "parent_alias");
                if (!PhpEmpty(parentAlias) && catIdMap.TryGetValue(parentAlias, out var pid))
                {
                    parentId = pid;
                }

                var id = EpcStorefrontSeedUpsertCategory(db, cat, parentId);
                catIdMap[Str(cat, "alias")] = id;
                report["categories"] = Convert.ToInt32(report["categories"], CultureInfo.InvariantCulture) + 1;
            }
            catch (Exception ex)
            {
                ((List<string>)report["errors"]!).Add("cat:" + Str(cat, "alias") + ":" + ex.Message);
            }
        }

        var now = Clock();
        var i = 0;
        foreach (var prod in products)
        {
            try
            {
                var catId = 0;
                var alias = Str(prod, "category_alias");
                if (!PhpEmpty(alias) && catIdMap.TryGetValue(alias, out var cid))
                {
                    catId = cid;
                }

                EpcStorefrontSeedUpsertProduct(db, prod, now + i, catId);
                report["products"] = Convert.ToInt32(report["products"], CultureInfo.InvariantCulture) + 1;
            }
            catch (Exception ex)
            {
                ((List<string>)report["errors"]!).Add("prod:" + Str(prod, "alias") + ":" + ex.Message);
            }

            i++;
        }

        return report;
    }

    public static int EpcStorefrontSeedUpsertCategory(SeedStore db, IReadOnlyDictionary<string, object?> cat, int parentId)
    {
        var alias = Clip(Str(cat, "alias"), 255);
        var name = Str(cat, "name");
        if (name == "")
        {
            name = alias;
        }

        var url = !PhpEmpty(Str(cat, "url")) ? Clip(Str(cat, "url"), 255) : alias;
        var level = Int(cat, "level", parentId > 0 ? 2 : 1);
        var order = Int(cat, "order", 10);
        var existing = db.Categories.FirstOrDefault(c => c.Alias == alias);
        if (existing != null)
        {
            existing.Value = name;
            existing.Url = url;
            existing.Level = level;
            existing.Order = order;
            existing.PublishedFlag = 1;
            existing.Parent = parentId;
            return existing.Id;
        }

        var row = new CatRow
        {
            Id = db.NextCategoryId++,
            Alias = alias,
            Url = url,
            Value = name,
            Level = level,
            Order = order,
            Parent = parentId,
            PublishedFlag = 1
        };
        db.Categories.Add(row);
        return row.Id;
    }

    public static void EpcStorefrontSeedUpsertProduct(SeedStore db, IReadOnlyDictionary<string, object?> prod, long timeCreated, int catId)
    {
        var caption = Str(prod, "name");
        var alias = Str(prod, "alias");
        var price = ToDouble(prod, "price");
        var image = !PhpEmpty(Str(prod, "image")) ? Str(prod, "image") : "";
        var existing = db.Products.FirstOrDefault(p => p.Alias == alias);
        if (existing == null)
        {
            existing = new ProdRow
            {
                Id = db.NextProductId++,
                Caption = caption,
                Alias = alias,
                Price = price,
                TimeCreated = (int)timeCreated,
                PublishedFlag = 1
            };
            db.Products.Add(existing);
        }

        if (catId > 0 && !db.ProductCategories.Contains((existing.Id, catId)))
        {
            db.ProductCategories.Add((existing.Id, catId));
        }

        if (image != "" && !db.Images.Contains((existing.Id, image)))
        {
            db.Images.Add((existing.Id, image));
        }
    }

    public static Dictionary<string, object?> EpcStorefrontSeedGenericCatalog(string industry, IReadOnlyDictionary<string, object?>? locale = null)
    {
        var name = industry.Replace('_', ' ');
        var slug = Regex.Replace(industry.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        var tax = locale != null && locale.TryGetValue("tax_label", out var t) && t != null ? Convert.ToString(t, CultureInfo.InvariantCulture) ?? "VAT" : "VAT";
        var currency = locale != null && locale.TryGetValue("currency", out var c) && c != null ? Convert.ToString(c, CultureInfo.InvariantCulture) ?? "AED" : "AED";
        var price = EpcStorefrontSeedConvertPrice(25.0, currency);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["categories"] = new List<Dictionary<string, object?>>
            {
                Cat("demo-" + slug + "-featured", "DEMO | " + name + " featured", slug + "/featured", 1, 10),
                Cat("demo-" + slug + "-services", "DEMO | " + name + " services", slug + "/services", 1, 20),
                Cat("demo-" + slug + "-compliance", "DEMO | " + tax + " and compliance", slug + "/compliance", 1, 30)
            },
            ["products"] = new List<Dictionary<string, object?>>
            {
                Prod("DEMO | " + name + " starter offering", "DEMO-" + slug.ToUpperInvariant() + "-001", price, "demo-" + slug + "-featured"),
                Prod("DEMO | " + name + " professional service", "DEMO-" + slug.ToUpperInvariant() + "-002", price * 2, "demo-" + slug + "-services"),
                Prod("DEMO | " + name + " compliance package", "DEMO-" + slug.ToUpperInvariant() + "-003", price * 3, "demo-" + slug + "-compliance")
            }
        };
    }

    public static List<Dictionary<string, object?>> EpcStorefrontSeedCategoryTree(string industry, IReadOnlyDictionary<string, object?>? locale = null)
        => industry switch
        {
            "electronics" => EpcStorefrontSeedCategoriesElectronics(),
            "fashion" => EpcStorefrontSeedCategoriesFashion(locale),
            "tax_advisory" or "consultancy" => EpcStorefrontSeedCategoriesConsulting(locale),
            "jewellery" => EpcStorefrontSeedCategoriesJewellery(),
            _ => (List<Dictionary<string, object?>>)EpcStorefrontSeedGenericCatalog(industry, locale)["categories"]!
        };

    public static List<Dictionary<string, object?>> EpcStorefrontSeedCategoriesElectronics()
        => FromArray(Seed.Value.GetProperty("elc_cats"));

    public static List<Dictionary<string, object?>> EpcStorefrontSeedCategoriesFashion(IReadOnlyDictionary<string, object?>? locale = null)
    {
        var region = locale != null && locale.TryGetValue("region", out var r) && r != null ? Convert.ToString(r, CultureInfo.InvariantCulture) ?? "international" : "international";
        var gcc = region is "gcc" or "mena";
        return
        [
            Cat("fsn-women", "Women's Fashion", "women", 1, 10),
            Cat("fsn-women-dresses", "Dresses", "women/dresses", 2, 10, "fsn-women"),
            Cat("fsn-women-tops", "Tops & Blouses", "women/tops", 2, 20, "fsn-women"),
            Cat("fsn-women-abayas", gcc ? "Abayas & Modest" : "Modest & Traditional", gcc ? "women/abayas" : "women/modest", 2, 30, "fsn-women"),
            Cat("fsn-women-shoes", "Shoes & Sandals", "women/shoes", 2, 40, "fsn-women"),
            Cat("fsn-men", "Men's Fashion", "men", 1, 20),
            Cat("fsn-men-shirts", "Shirts & Polos", "men/shirts", 2, 10, "fsn-men"),
            Cat("fsn-men-pants", "Trousers & Chinos", "men/trousers", 2, 20, "fsn-men"),
            Cat("fsn-men-thobes", gcc ? "Thobes & Kandoras" : "Traditional Wear", gcc ? "men/thobes" : "men/traditional", 2, 30, "fsn-men"),
            Cat("fsn-men-shoes", "Sneakers & Shoes", "men/shoes", 2, 40, "fsn-men"),
            Cat("fsn-beauty", "Beauty & Fragrance", "beauty", 1, 30),
            Cat("fsn-perfume", "Perfumes", "beauty/perfumes", 2, 10, "fsn-beauty"),
            Cat("fsn-skincare", "Skincare", "beauty/skincare", 2, 20, "fsn-beauty"),
            Cat("fsn-makeup", "Makeup", "beauty/makeup", 2, 30, "fsn-beauty"),
            Cat("fsn-kids", "Kids", "kids", 1, 40),
            Cat("fsn-accessories", "Accessories", "accessories", 1, 50),
            Cat("fsn-bags", "Bags & Wallets", "accessories/bags", 2, 10, "fsn-accessories"),
            Cat("fsn-jewellery", "Fashion Jewellery", "accessories/jewellery", 2, 20, "fsn-accessories"),
            Cat("fsn-sunglasses", "Sunglasses", "accessories/sunglasses", 2, 30, "fsn-accessories"),
            Cat("fsn-sports", "Sports & Activewear", "sports", 1, 60),
            Cat("fsn-home", "Home & Lifestyle", "home-lifestyle", 1, 70)
        ];
    }

    public static List<Dictionary<string, object?>> EpcStorefrontSeedCategoriesConsulting(IReadOnlyDictionary<string, object?>? locale = null)
    {
        var tax = locale != null && locale.TryGetValue("tax_label", out var t) && t != null ? Convert.ToString(t, CultureInfo.InvariantCulture) ?? "VAT" : "VAT";
        return
        [
            Cat("cns-vat", tax + " Services", "services/tax", 1, 10),
            Cat("cns-vat-reg", tax + " Registration", "services/tax/registration", 2, 10, "cns-vat"),
            Cat("cns-vat-filing", tax + " Return Filing", "services/tax/filing", 2, 20, "cns-vat"),
            Cat("cns-vat-audit", tax + " Health Check", "services/tax/health-check", 2, 30, "cns-vat"),
            Cat("cns-ct", "Corporate Tax", "services/corporate-tax", 1, 20),
            Cat("cns-ct-reg", "CT Registration", "services/corporate-tax/registration", 2, 10, "cns-ct"),
            Cat("cns-ct-filing", "CT Return Filing", "services/corporate-tax/filing", 2, 20, "cns-ct"),
            Cat("cns-audit", "Audit & Assurance", "services/audit", 1, 30),
            Cat("cns-audit-ext", "External Audit", "services/audit/external", 2, 10, "cns-audit"),
            Cat("cns-audit-int", "Internal Audit", "services/audit/internal", 2, 20, "cns-audit"),
            Cat("cns-bookkeeping", "Bookkeeping", "services/bookkeeping", 1, 40),
            Cat("cns-compliance", "Compliance & AML", "services/compliance", 1, 50),
            Cat("cns-aml", "AML Compliance", "services/compliance/aml", 2, 10, "cns-compliance"),
            Cat("cns-esrub", "ESR & UBO Filing", "services/compliance/esr", 2, 20, "cns-compliance"),
            Cat("cns-advisory", "Business Advisory", "services/advisory", 1, 60),
            Cat("cns-setup", "Company Formation", "services/company-setup", 1, 70)
        ];
    }

    public static List<Dictionary<string, object?>> EpcStorefrontSeedCategoriesJewellery()
        => FromArray(Seed.Value.GetProperty("jwl_cats"));

    public static List<Dictionary<string, object?>> EpcStorefrontSeedProductCatalog(string industry, IReadOnlyDictionary<string, object?>? locale = null)
    {
        var currency = locale != null && locale.TryGetValue("currency", out var c) && c != null ? Convert.ToString(c, CultureInfo.InvariantCulture) ?? "AED" : "AED";
        var raw = industry switch
        {
            "electronics" => EpcStorefrontSeedProductsElectronics(),
            "fashion" => EpcStorefrontSeedProductsFashion(locale),
            "tax_advisory" or "consultancy" => EpcStorefrontSeedProductsConsulting(locale),
            "jewellery" => EpcStorefrontSeedProductsJewellery(),
            _ => (List<Dictionary<string, object?>>)EpcStorefrontSeedGenericCatalog(industry, locale)["products"]!
        };
        if (currency == "AED")
        {
            return raw;
        }

        foreach (var prod in raw)
        {
            var usdBase = ToDouble(prod, "price") / 3.67;
            prod["price"] = EpcStorefrontSeedConvertPrice(usdBase, currency);
        }

        return raw;
    }

    public static List<Dictionary<string, object?>> EpcStorefrontSeedProductsElectronics()
        => FromArray(Seed.Value.GetProperty("elc_prods"));

    public static List<Dictionary<string, object?>> EpcStorefrontSeedProductsFashion(IReadOnlyDictionary<string, object?>? locale = null)
    {
        var region = locale != null && locale.TryGetValue("region", out var r) && r != null ? Convert.ToString(r, CultureInfo.InvariantCulture) ?? "international" : "international";
        var gcc = region is "gcc" or "mena";
        var modest1 = gcc ? "Premium Open Abaya — Black with Gold Trim" : "Modest Long Dress — Black with Gold Trim";
        var modest2 = gcc ? "Embroidered Abaya — Navy" : "Embroidered Kaftan — Navy";
        var menTrad = gcc ? "Premium White Thobe — Emirati Style" : "Traditional Formal Tunic — White";
        return
        [
            Prod("Silk Midi Dress — Emerald Green", "FSN-WD-SILK-EM", 449, "fsn-women-dresses", "https://images.unsplash.com/photo-1595777457583-95e059d581b8?auto=format&fit=crop&w=480"),
            Prod("Floral Maxi Dress — Blush Pink", "FSN-WD-FLR-BP", 359, "fsn-women-dresses", "https://images.unsplash.com/photo-1572804013309-59a88b7e92f1?auto=format&fit=crop&w=480"),
            Prod("Linen Wrap Top — Ivory", "FSN-WT-LN-IV", 189, "fsn-women-tops", "https://images.unsplash.com/photo-1564257631407-4deb1f99d992?auto=format&fit=crop&w=480"),
            Prod(modest1, "FSN-WA-BLK-GLD", 599, "fsn-women-abayas", "https://images.unsplash.com/photo-1590735213920-68192a487bc2?auto=format&fit=crop&w=480"),
            Prod(modest2, "FSN-WA-EMB-NVY", 699, "fsn-women-abayas", "https://images.unsplash.com/photo-1590735213920-68192a487bc2?auto=format&fit=crop&w=480"),
            Prod("Block Heel Sandals — Nude", "FSN-WS-HEEL-ND", 329, "fsn-women-shoes", "https://images.unsplash.com/photo-1543163521-1bf539c55dd2?auto=format&fit=crop&w=480"),
            Prod("Oxford Button-Down Shirt — White", "FSN-MS-OXF-WHT", 199, "fsn-men-shirts", "https://images.unsplash.com/photo-1596755094514-f87e34085b2c?auto=format&fit=crop&w=480"),
            Prod("Slim Fit Polo — Navy", "FSN-MS-PLO-NVY", 149, "fsn-men-shirts", "https://images.unsplash.com/photo-1625910513413-5fc421e0b6b4?auto=format&fit=crop&w=480"),
            Prod("Tailored Chinos — Olive", "FSN-MP-CHN-OLV", 229, "fsn-men-pants", "https://images.unsplash.com/photo-1473966968600-fa801b869a1a?auto=format&fit=crop&w=480"),
            Prod(menTrad, "FSN-MT-WHT-EM", 399, "fsn-men-thobes", "https://images.unsplash.com/photo-1590735213920-68192a487bc2?auto=format&fit=crop&w=480"),
            Prod("Retro Running Sneakers — White/Grey", "FSN-MSH-RUN-WG", 499, "fsn-men-shoes", "https://images.unsplash.com/photo-1542291026-7eec264c27ff?auto=format&fit=crop&w=480"),
            Prod("Classic Leather Loafers — Brown", "FSN-MSH-LOF-BR", 449, "fsn-men-shoes", "https://images.unsplash.com/photo-1614252369475-531eba835eb1?auto=format&fit=crop&w=480"),
            Prod("Oud Rose EDP 100ml — Unisex", "FSN-BF-OUD-100", 599, "fsn-perfume", "https://images.unsplash.com/photo-1541643600914-78b084683601?auto=format&fit=crop&w=480"),
            Prod("French Vanilla EDP 50ml — Women", "FSN-BF-VAN-50", 349, "fsn-perfume", "https://images.unsplash.com/photo-1541643600914-78b084683601?auto=format&fit=crop&w=480"),
            Prod("Vitamin C Serum 30ml — Radiance Boost", "FSN-BS-VITC-30", 189, "fsn-skincare", "https://images.unsplash.com/photo-1556228578-0d85b1a4d571?auto=format&fit=crop&w=480"),
            Prod("Hydrating Face Cream 50ml — SPF 30", "FSN-BS-HYD-50", 149, "fsn-skincare", "https://images.unsplash.com/photo-1556228578-0d85b1a4d571?auto=format&fit=crop&w=480"),
            Prod("Matte Lipstick Set — 6 Shades", "FSN-BM-LIP-SET", 129, "fsn-makeup", "https://images.unsplash.com/photo-1586495777744-4413f21062fa?auto=format&fit=crop&w=480"),
            Prod("Leather Tote Bag — Camel", "FSN-AB-TOTE-CM", 549, "fsn-bags", "https://images.unsplash.com/photo-1548036328-c9fa89d128fa?auto=format&fit=crop&w=480"),
            Prod("Mini Crossbody — Black", "FSN-AB-CROSS-BK", 299, "fsn-bags", "https://images.unsplash.com/photo-1548036328-c9fa89d128fa?auto=format&fit=crop&w=480"),
            Prod("Gold-Plated Statement Necklace", "FSN-AJ-NECK-GLD", 179, "fsn-jewellery", "https://images.unsplash.com/photo-1515562141589-67f0d727b750?auto=format&fit=crop&w=480"),
            Prod("Oversized Square Sunglasses — Tortoise", "FSN-ASG-SQ-TRT", 249, "fsn-sunglasses", "https://images.unsplash.com/photo-1511499767150-a48a237f0083?auto=format&fit=crop&w=480"),
            Prod("Aviator Polarized Sunglasses — Gold Frame", "FSN-ASG-AV-GLD", 299, "fsn-sunglasses", "https://images.unsplash.com/photo-1511499767150-a48a237f0083?auto=format&fit=crop&w=480"),
            Prod("Performance Running Tights — Black", "FSN-SP-RUN-BLK", 199, "fsn-sports", "https://images.unsplash.com/photo-1571902943202-507ec2618e8f?auto=format&fit=crop&w=480"),
            Prod("Yoga Mat Premium 6mm — Sage Green", "FSN-SP-YGA-SGE", 149, "fsn-sports", "https://images.unsplash.com/photo-1571902943202-507ec2618e8f?auto=format&fit=crop&w=480"),
            Prod("Boys Cotton T-Shirt Pack (3) — Multi", "FSN-KD-TSHRT-3", 99, "fsn-kids", "https://images.unsplash.com/photo-1519238263530-99bdd11df2ea?auto=format&fit=crop&w=480")
        ];
    }

    public static List<Dictionary<string, object?>> EpcStorefrontSeedProductsConsulting(IReadOnlyDictionary<string, object?>? locale = null)
    {
        var tax = locale != null && locale.TryGetValue("tax_label", out var t) && t != null ? Convert.ToString(t, CultureInfo.InvariantCulture) ?? "VAT" : "VAT";
        return
        [
            Prod(tax + " Registration — New Business", "CNS-VAT-REG-NEW", 1500, "cns-vat-reg", "https://images.unsplash.com/photo-1554224155-6726b3ff858f?auto=format&fit=crop&w=480"),
            Prod(tax + " Registration — Group Registration", "CNS-VAT-REG-GRP", 3500, "cns-vat-reg", "https://images.unsplash.com/photo-1554224155-6726b3ff858f?auto=format&fit=crop&w=480"),
            Prod(tax + " Return Filing — Quarterly", "CNS-VAT-FIL-Q", 1000, "cns-vat-filing", "https://images.unsplash.com/photo-1460925895917-afdab827c52f?auto=format&fit=crop&w=480"),
            Prod(tax + " Return Filing — Monthly", "CNS-VAT-FIL-M", 800, "cns-vat-filing", "https://images.unsplash.com/photo-1460925895917-afdab827c52f?auto=format&fit=crop&w=480"),
            Prod(tax + " Health Check & Compliance Review", "CNS-VAT-HLTH", 5000, "cns-vat-audit", "https://images.unsplash.com/photo-1450101499163-c8848c66ca85?auto=format&fit=crop&w=480"),
            Prod("Corporate Tax Registration", "CNS-CT-REG", 2000, "cns-ct-reg", "https://images.unsplash.com/photo-1554224155-6726b3ff858f?auto=format&fit=crop&w=480"),
            Prod("Corporate Tax Return Filing — Annual", "CNS-CT-FIL-A", 5000, "cns-ct-filing", "https://images.unsplash.com/photo-1460925895917-afdab827c52f?auto=format&fit=crop&w=480"),
            Prod("Transfer Pricing Documentation", "CNS-CT-TP-DOC", 8000, "cns-ct-filing", "https://images.unsplash.com/photo-1450101499163-c8848c66ca85?auto=format&fit=crop&w=480"),
            Prod("External Audit — SME", "CNS-AUD-EXT-SM", 8000, "cns-audit-ext", "https://images.unsplash.com/photo-1454165804606-c3d57bc86b40?auto=format&fit=crop&w=480"),
            Prod("External Audit — Enterprise", "CNS-AUD-EXT-EN", 25000, "cns-audit-ext", "https://images.unsplash.com/photo-1454165804606-c3d57bc86b40?auto=format&fit=crop&w=480"),
            Prod("Internal Audit — Quarterly Review", "CNS-AUD-INT-Q", 6000, "cns-audit-int", "https://images.unsplash.com/photo-1454165804606-c3d57bc86b40?auto=format&fit=crop&w=480"),
            Prod("Monthly Bookkeeping — Starter (< 100 txn)", "CNS-BK-STR-M", 1500, "cns-bookkeeping", "https://images.unsplash.com/photo-1554224155-6726b3ff858f?auto=format&fit=crop&w=480"),
            Prod("Monthly Bookkeeping — Growth (100-500 txn)", "CNS-BK-GRW-M", 3000, "cns-bookkeeping", "https://images.unsplash.com/photo-1554224155-6726b3ff858f?auto=format&fit=crop&w=480"),
            Prod("Monthly Bookkeeping — Enterprise (500+ txn)", "CNS-BK-ENT-M", 5000, "cns-bookkeeping", "https://images.unsplash.com/photo-1554224155-6726b3ff858f?auto=format&fit=crop&w=480"),
            Prod("AML/CFT Compliance Setup & Training", "CNS-AML-SETUP", 7500, "cns-aml", "https://images.unsplash.com/photo-1450101499163-c8848c66ca85?auto=format&fit=crop&w=480"),
            Prod("AML Ongoing Monitoring — Annual", "CNS-AML-MON-A", 3000, "cns-aml", "https://images.unsplash.com/photo-1450101499163-c8848c66ca85?auto=format&fit=crop&w=480"),
            Prod("Regulatory Compliance Filing", "CNS-ESR-FILE", 2500, "cns-esrub", "https://images.unsplash.com/photo-1460925895917-afdab827c52f?auto=format&fit=crop&w=480"),
            Prod("Beneficial Ownership Declaration", "CNS-UBO-FILE", 1500, "cns-esrub", "https://images.unsplash.com/photo-1460925895917-afdab827c52f?auto=format&fit=crop&w=480"),
            Prod("Business Plan & Financial Projections", "CNS-ADV-BPLAN", 10000, "cns-advisory", "https://images.unsplash.com/photo-1454165804606-c3d57bc86b40?auto=format&fit=crop&w=480"),
            Prod("CFO-as-a-Service — Monthly Retainer", "CNS-ADV-CFO-M", 8000, "cns-advisory", "https://images.unsplash.com/photo-1454165804606-c3d57bc86b40?auto=format&fit=crop&w=480"),
            Prod("Company Formation — Standard", "CNS-CO-MAIN-DXB", 15000, "cns-setup", "https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?auto=format&fit=crop&w=480"),
            Prod("Company Formation — Premium", "CNS-CO-FZ-DMCC", 12000, "cns-setup", "https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?auto=format&fit=crop&w=480"),
            Prod("Company Formation — Express", "CNS-CO-FZ-IFZA", 8500, "cns-setup", "https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?auto=format&fit=crop&w=480")
        ];
    }

    public static List<Dictionary<string, object?>> EpcStorefrontSeedProductsJewellery()
        => FromArray(Seed.Value.GetProperty("jwl_prods"));

    private static List<Dictionary<string, object?>> FromArray(JsonElement arr)
    {
        var list = new List<Dictionary<string, object?>>();
        foreach (var item in arr.EnumerateArray())
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var p in item.EnumerateObject())
            {
                row[p.Name] = p.Value.ValueKind switch
                {
                    JsonValueKind.Number => p.Value.TryGetInt32(out var i) ? i : p.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => p.Value.GetString()
                };
            }

            list.Add(row);
        }

        return list;
    }

    private static Dictionary<string, object?> Cat(string alias, string name, string url, int level, int order, string? parent = null)
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["alias"] = alias,
            ["name"] = name,
            ["url"] = url,
            ["level"] = level,
            ["order"] = order
        };
        if (parent != null)
        {
            row["parent_alias"] = parent;
        }

        return row;
    }

    private static Dictionary<string, object?> Prod(string name, string alias, double price, string category, string? image = null)
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = name,
            ["alias"] = alias,
            ["price"] = price,
            ["category_alias"] = category
        };
        if (image != null)
        {
            row["image"] = image;
        }

        return row;
    }

    private static bool PhpEmpty(object? value)
        => value == null || value is false || value is 0 || (value is string s && (s.Length == 0 || s == "0"));

    private static string Str(IReadOnlyDictionary<string, object?> bag, string key)
        => bag.TryGetValue(key, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? "" : "";

    private static int Int(IReadOnlyDictionary<string, object?> bag, string key, int fallback)
        => bag.TryGetValue(key, out var v) && v != null ? Convert.ToInt32(v, CultureInfo.InvariantCulture) : fallback;

    private static double ToDouble(IReadOnlyDictionary<string, object?> bag, string key)
        => bag.TryGetValue(key, out var v) && v != null ? Convert.ToDouble(v, CultureInfo.InvariantCulture) : 0;

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];
}
