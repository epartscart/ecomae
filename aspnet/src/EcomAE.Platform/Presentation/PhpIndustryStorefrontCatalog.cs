namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP <c>epc_storefront_seed_data.php</c> category trees + demo products for the
/// four custom-package live tenants. Host-gated — jewellery slugs never resolve
/// on epartscart / electronics / fashion / tax.
/// </summary>
public static class PhpIndustryStorefrontCatalog
{
    public sealed record Category(string Alias, string Name, string Url, int Level, string? ParentAlias);

    public sealed record Product(string Name, string Alias, decimal Price, string CategoryAlias, string Image);

    public static IReadOnlyList<Category> CategoriesFor(string industryCode) =>
        NormalizeIndustry(industryCode) switch
        {
            "electronics" => ElectronicsCategories,
            "fashion" => FashionCategories,
            "jewellery" => JewelleryCategories,
            "tax_advisory" => ConsultingCategories,
            _ => [],
        };

    public static IReadOnlyList<Product> ProductsFor(string industryCode) =>
        NormalizeIndustry(industryCode) switch
        {
            "electronics" => ElectronicsProducts,
            "fashion" => FashionProducts,
            "jewellery" => JewelleryProducts,
            "tax_advisory" => ConsultingProducts,
            _ => [],
        };

    public static bool TryResolve(string industryCode, string? url, out Category category)
    {
        category = null!;
        var key = NormalizeUrl(url);
        if (key.Length == 0)
        {
            return false;
        }

        var match = CategoriesFor(industryCode)
            .FirstOrDefault(c => c.Url.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return false;
        }

        category = match;
        return true;
    }

    public static bool OwnsUrl(string industryCode, string? url) => TryResolve(industryCode, url, out _);

    public static IReadOnlyList<Category> ChildrenOf(string industryCode, string alias)
        => CategoriesFor(industryCode)
            .Where(c => string.Equals(c.ParentAlias, alias, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Level)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static IReadOnlyList<Category> Roots(string industryCode)
        => CategoriesFor(industryCode).Where(c => c.Level == 1).ToArray();

    public static IReadOnlyList<Product> ProductsIn(string industryCode, Category category)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { category.Alias };
        CollectDescendantAliases(CategoriesFor(industryCode), category.Alias, aliases);
        return ProductsFor(industryCode).Where(p => aliases.Contains(p.CategoryAlias)).ToArray();
    }

    public static string FormatAed(decimal amount)
    {
        if (amount <= 0)
        {
            return string.Empty;
        }

        return "AED " + amount.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string NormalizeIndustry(string? industryCode)
    {
        var code = (industryCode ?? string.Empty).Trim().ToLowerInvariant();
        return code is "tax_advisory" or "consultancy" ? "tax_advisory" : code;
    }

    public static string NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var value = url.Trim().Trim('/');
        var q = value.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
        {
            value = value[..q];
        }

        if (value.StartsWith("en/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("ar/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("me/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("ru/", StringComparison.OrdinalIgnoreCase))
        {
            value = value[3..];
        }

        return value.ToLowerInvariant();
    }

    private static void CollectDescendantAliases(IReadOnlyList<Category> all, string parentAlias, HashSet<string> into)
    {
        foreach (var child in all.Where(c => string.Equals(c.ParentAlias, parentAlias, StringComparison.OrdinalIgnoreCase)))
        {
            if (into.Add(child.Alias))
            {
                CollectDescendantAliases(all, child.Alias, into);
            }
        }
    }

    private static readonly Category[] ElectronicsCategories =
    [
        new("elc-smartphones", "Smartphones & Mobiles", "smartphones", 1, null),
        new("elc-iphones", "iPhones", "smartphones/iphones", 2, "elc-smartphones"),
        new("elc-samsung-phone", "Samsung Galaxy", "smartphones/samsung", 2, "elc-smartphones"),
        new("elc-android-other", "Android Phones", "smartphones/android", 2, "elc-smartphones"),
        new("elc-laptops", "Laptops & Computers", "laptops", 1, null),
        new("elc-macbooks", "MacBooks", "laptops/macbooks", 2, "elc-laptops"),
        new("elc-win-laptops", "Windows Laptops", "laptops/windows", 2, "elc-laptops"),
        new("elc-gaming-laptop", "Gaming Laptops", "laptops/gaming", 2, "elc-laptops"),
        new("elc-tablets", "Tablets & E-Readers", "tablets", 1, null),
        new("elc-ipads", "iPads", "tablets/ipads", 2, "elc-tablets"),
        new("elc-android-tab", "Android Tablets", "tablets/android", 2, "elc-tablets"),
        new("elc-gaming", "Gaming", "gaming", 1, null),
        new("elc-consoles", "Consoles", "gaming/consoles", 2, "elc-gaming"),
        new("elc-accessories-g", "Gaming Accessories", "gaming/accessories", 2, "elc-gaming"),
        new("elc-audio", "Audio & Headphones", "audio", 1, null),
        new("elc-headphones", "Headphones", "audio/headphones", 2, "elc-audio"),
        new("elc-speakers", "Speakers", "audio/speakers", 2, "elc-audio"),
        new("elc-wearables", "Wearables", "wearables", 1, null),
        new("elc-smart-home", "Smart Home", "smart-home", 1, null),
        new("elc-tv-cinema", "TV & Home Cinema", "tv-cinema", 1, null),
        new("elc-cameras", "Cameras & Drones", "cameras", 1, null),
    ];

    private static readonly Category[] FashionCategories =
    [
        new("fsn-women", "Women's Fashion", "women", 1, null),
        new("fsn-women-dresses", "Dresses", "women/dresses", 2, "fsn-women"),
        new("fsn-women-tops", "Tops & Blouses", "women/tops", 2, "fsn-women"),
        new("fsn-women-abayas", "Abayas & Modest", "women/abayas", 2, "fsn-women"),
        new("fsn-women-shoes", "Shoes & Sandals", "women/shoes", 2, "fsn-women"),
        new("fsn-men", "Men's Fashion", "men", 1, null),
        new("fsn-men-shirts", "Shirts & Polos", "men/shirts", 2, "fsn-men"),
        new("fsn-men-pants", "Trousers & Chinos", "men/trousers", 2, "fsn-men"),
        new("fsn-men-thobes", "Thobes & Kandoras", "men/thobes", 2, "fsn-men"),
        new("fsn-men-shoes", "Sneakers & Shoes", "men/shoes", 2, "fsn-men"),
        new("fsn-beauty", "Beauty & Fragrance", "beauty", 1, null),
        new("fsn-perfume", "Perfumes", "beauty/perfumes", 2, "fsn-beauty"),
        new("fsn-skincare", "Skincare", "beauty/skincare", 2, "fsn-beauty"),
        new("fsn-makeup", "Makeup", "beauty/makeup", 2, "fsn-beauty"),
        new("fsn-kids", "Kids", "kids", 1, null),
        new("fsn-accessories", "Accessories", "accessories", 1, null),
        new("fsn-bags", "Bags & Wallets", "accessories/bags", 2, "fsn-accessories"),
        new("fsn-jewellery", "Fashion Jewellery", "accessories/jewellery", 2, "fsn-accessories"),
        new("fsn-sunglasses", "Sunglasses", "accessories/sunglasses", 2, "fsn-accessories"),
        new("fsn-sports", "Sports & Activewear", "sports", 1, null),
        new("fsn-home", "Home & Lifestyle", "home-lifestyle", 1, null),
    ];

    private static readonly Category[] JewelleryCategories =
    [
        new("jwl-gold", "Gold Jewellery", "gold", 1, null),
        new("jwl-gold-necklace", "Gold Necklaces", "gold/necklaces", 2, "jwl-gold"),
        new("jwl-gold-bangles", "Gold Bangles", "gold/bangles", 2, "jwl-gold"),
        new("jwl-gold-rings", "Gold Rings", "gold/rings", 2, "jwl-gold"),
        new("jwl-gold-earrings", "Gold Earrings", "gold/earrings", 2, "jwl-gold"),
        new("jwl-diamond", "Diamond Jewellery", "diamonds", 1, null),
        new("jwl-dia-rings", "Diamond Rings", "diamonds/rings", 2, "jwl-diamond"),
        new("jwl-dia-necklace", "Diamond Necklaces", "diamonds/necklaces", 2, "jwl-diamond"),
        new("jwl-dia-earrings", "Diamond Earrings", "diamonds/earrings", 2, "jwl-diamond"),
        new("jwl-bridal", "Bridal Collection", "bridal", 1, null),
        new("jwl-bridal-sets", "Bridal Sets", "bridal/sets", 2, "jwl-bridal"),
        new("jwl-bridal-rings", "Engagement Rings", "bridal/engagement", 2, "jwl-bridal"),
        new("jwl-everyday", "Everyday Jewellery", "everyday", 1, null),
        new("jwl-chains", "Chains", "everyday/chains", 2, "jwl-everyday"),
        new("jwl-pendants", "Pendants", "everyday/pendants", 2, "jwl-everyday"),
        new("jwl-watches", "Watches", "watches", 1, null),
        new("jwl-silver", "Silver Jewellery", "silver", 1, null),
        new("jwl-pearls", "Pearls", "pearls", 1, null),
    ];

    private static readonly Category[] ConsultingCategories =
    [
        new("cns-vat", "VAT Services", "services/tax", 1, null),
        new("cns-vat-reg", "VAT Registration", "services/tax/registration", 2, "cns-vat"),
        new("cns-vat-filing", "VAT Return Filing", "services/tax/filing", 2, "cns-vat"),
        new("cns-vat-audit", "VAT Health Check", "services/tax/health-check", 2, "cns-vat"),
        new("cns-ct", "Corporate Tax", "services/corporate-tax", 1, null),
        new("cns-ct-reg", "CT Registration", "services/corporate-tax/registration", 2, "cns-ct"),
        new("cns-ct-filing", "CT Return Filing", "services/corporate-tax/filing", 2, "cns-ct"),
        new("cns-audit", "Audit & Assurance", "services/audit", 1, null),
        new("cns-audit-ext", "External Audit", "services/audit/external", 2, "cns-audit"),
        new("cns-audit-int", "Internal Audit", "services/audit/internal", 2, "cns-audit"),
        new("cns-bookkeeping", "Bookkeeping", "services/bookkeeping", 1, null),
        new("cns-compliance", "Compliance & AML", "services/compliance", 1, null),
        new("cns-aml", "AML Compliance", "services/compliance/aml", 2, "cns-compliance"),
        new("cns-esrub", "ESR & UBO Filing", "services/compliance/esr", 2, "cns-compliance"),
        new("cns-advisory", "Business Advisory", "services/advisory", 1, null),
        new("cns-setup", "Company Formation", "services/company-setup", 1, null),
    ];

    private static readonly Product[] ElectronicsProducts =
    [
        new("iPhone 16 Pro Max 256GB — Natural Titanium", "ELC-IP16PM-256", 5299, "elc-iphones", "https://images.unsplash.com/photo-1695048133142-1a20484d2569?auto=format&fit=crop&w=480"),
        new("iPhone 16 128GB — Ultramarine", "ELC-IP16-128", 3499, "elc-iphones", "https://images.unsplash.com/photo-1695048133142-1a20484d2569?auto=format&fit=crop&w=480"),
        new("Samsung Galaxy S25 Ultra 512GB — Titanium Grey", "ELC-GS25U-512", 4699, "elc-samsung-phone", "https://images.unsplash.com/photo-1610945265064-0e34e5519bbf?auto=format&fit=crop&w=480"),
        new("Samsung Galaxy A55 128GB — Ice Blue", "ELC-GA55-128", 1299, "elc-samsung-phone", "https://images.unsplash.com/photo-1610945265064-0e34e5519bbf?auto=format&fit=crop&w=480"),
        new("Google Pixel 9 Pro 256GB — Porcelain", "ELC-PX9P-256", 3899, "elc-android-other", "https://images.unsplash.com/photo-1598327105666-5b89351aff97?auto=format&fit=crop&w=480"),
        new("MacBook Air M3 15\" 16GB/512GB — Midnight", "ELC-MBA-M3-15", 5999, "elc-macbooks", "https://images.unsplash.com/photo-1517336714731-489689fd1ca8?auto=format&fit=crop&w=480"),
        new("MacBook Pro M4 Pro 14\" 24GB/1TB", "ELC-MBP-M4P-14", 9499, "elc-macbooks", "https://images.unsplash.com/photo-1517336714731-489689fd1ca8?auto=format&fit=crop&w=480"),
        new("Dell XPS 15 i7 32GB/1TB — Platinum", "ELC-DXPS15-32", 6499, "elc-win-laptops", "https://images.unsplash.com/photo-1496181133206-80ce9b88a853?auto=format&fit=crop&w=480"),
        new("ASUS ROG Strix G16 RTX 4070 i9 32GB", "ELC-ROG-G16", 7999, "elc-gaming-laptop", "https://images.unsplash.com/photo-1593642632559-0c6d3fc62b89?auto=format&fit=crop&w=480"),
        new("iPad Pro M4 13\" 256GB Wi-Fi — Space Black", "ELC-IPADP-M4", 5499, "elc-ipads", "https://images.unsplash.com/photo-1544244015-0df4b3ffc6b0?auto=format&fit=crop&w=480"),
        new("Samsung Galaxy Tab S10 FE 5G 12GB/256GB", "ELC-TABS10FE", 2004, "elc-android-tab", "https://images.unsplash.com/photo-1585790050230-5dd28404ccb9?auto=format&fit=crop&w=480"),
        new("PlayStation 5 Pro Digital Edition", "ELC-PS5PRO-D", 3499, "elc-consoles", "https://images.unsplash.com/photo-1606144042614-b2417e99c4e3?auto=format&fit=crop&w=480"),
        new("Nintendo Switch 2 Joy-Con Bundle", "ELC-NSW2-JC", 2099, "elc-consoles", "https://images.unsplash.com/photo-1578303512597-81e6cc155b3e?auto=format&fit=crop&w=480"),
        new("Razer Viper V4 Pro Wireless Esports Mouse", "ELC-RZ-VPR4", 669, "elc-accessories-g", "https://images.unsplash.com/photo-1527814050087-3793815479db?auto=format&fit=crop&w=480"),
        new("Apple AirPods Pro 2nd Gen USB-C", "ELC-APP2-USC", 899, "elc-headphones", "https://images.unsplash.com/photo-1606220588913-b3aacb4d2f46?auto=format&fit=crop&w=480"),
        new("Bose QuietComfort Ultra Headphones — Black", "ELC-BOSE-QCU", 1499, "elc-headphones", "https://images.unsplash.com/photo-1546435770-a3e426bf472b?auto=format&fit=crop&w=480"),
        new("JBL Charge 5 Bluetooth Speaker — Squad", "ELC-JBL-CH5", 599, "elc-speakers", "https://images.unsplash.com/photo-1608043152269-423dbba4e7e1?auto=format&fit=crop&w=480"),
        new("Sony WH-1000XM5 Wireless — Silver", "ELC-SONY-XM5", 1299, "elc-headphones", "https://images.unsplash.com/photo-1618366712010-f4ae9c647dcb?auto=format&fit=crop&w=480"),
        new("Apple Watch Ultra 2 49mm Titanium", "ELC-AWU2-49", 3699, "elc-wearables", "https://images.unsplash.com/photo-1434493789847-2f02dc6ca35d?auto=format&fit=crop&w=480"),
        new("Samsung Galaxy Watch 7 44mm — Green", "ELC-GW7-44", 1199, "elc-wearables", "https://images.unsplash.com/photo-1434493789847-2f02dc6ca35d?auto=format&fit=crop&w=480"),
        new("Amazon Echo Show 10 3rd Gen", "ELC-ECHO10-3", 999, "elc-smart-home", "https://images.unsplash.com/photo-1558618666-fcd25c85f82e?auto=format&fit=crop&w=480"),
        new("Google Nest Hub Max 10\" Smart Display", "ELC-NEST-HUB", 899, "elc-smart-home", "https://images.unsplash.com/photo-1558618666-fcd25c85f82e?auto=format&fit=crop&w=480"),
        new("Samsung 65\" Neo QLED 4K Smart TV", "ELC-SAM-65QLED", 5999, "elc-tv-cinema", "https://images.unsplash.com/photo-1593359677879-a4bb92f829d1?auto=format&fit=crop&w=480"),
        new("LG OLED C4 55\" 4K Dolby Vision", "ELC-LG-OLED55", 4799, "elc-tv-cinema", "https://images.unsplash.com/photo-1593359677879-a4bb92f829d1?auto=format&fit=crop&w=480"),
        new("DJI Mini 4 Pro Fly More Combo", "ELC-DJI-M4P", 3999, "elc-cameras", "https://images.unsplash.com/photo-1473968512647-3e447244af8f?auto=format&fit=crop&w=480"),
        new("Sony Alpha A7 IV Full Frame Mirrorless", "ELC-SONY-A7IV", 8999, "elc-cameras", "https://images.unsplash.com/photo-1516035069371-29a1b244cc32?auto=format&fit=crop&w=480"),
    ];

    private static readonly Product[] FashionProducts =
    [
        new("Silk Midi Dress — Emerald Green", "FSN-WD-SILK-EM", 449, "fsn-women-dresses", "https://images.unsplash.com/photo-1595777457583-95e059d581b8?auto=format&fit=crop&w=480"),
        new("Floral Maxi Dress — Blush Pink", "FSN-WD-FLR-BP", 359, "fsn-women-dresses", "https://images.unsplash.com/photo-1572804013309-59a88b7e92f1?auto=format&fit=crop&w=480"),
        new("Linen Wrap Top — Ivory", "FSN-WT-LN-IV", 189, "fsn-women-tops", "https://images.unsplash.com/photo-1564257631407-4deb1f99d992?auto=format&fit=crop&w=480"),
        new("Premium Open Abaya — Black with Gold Trim", "FSN-WA-BLK-GLD", 599, "fsn-women-abayas", "https://images.unsplash.com/photo-1590735213920-68192a487bc2?auto=format&fit=crop&w=480"),
        new("Embroidered Abaya — Navy", "FSN-WA-EMB-NVY", 699, "fsn-women-abayas", "https://images.unsplash.com/photo-1590735213920-68192a487bc2?auto=format&fit=crop&w=480"),
        new("Block Heel Sandals — Nude", "FSN-WS-HEEL-ND", 329, "fsn-women-shoes", "https://images.unsplash.com/photo-1543163521-1bf539c55dd2?auto=format&fit=crop&w=480"),
        new("Oxford Button-Down Shirt — White", "FSN-MS-OXF-WHT", 199, "fsn-men-shirts", "https://images.unsplash.com/photo-1596755094514-f87e34085b2c?auto=format&fit=crop&w=480"),
        new("Slim Fit Polo — Navy", "FSN-MS-PLO-NVY", 149, "fsn-men-shirts", "https://images.unsplash.com/photo-1625910513413-5fc421e0b6b4?auto=format&fit=crop&w=480"),
        new("Tailored Chinos — Olive", "FSN-MP-CHN-OLV", 229, "fsn-men-pants", "https://images.unsplash.com/photo-1473966968600-fa801b869a1a?auto=format&fit=crop&w=480"),
        new("Premium White Thobe — Emirati Style", "FSN-MT-WHT-EM", 399, "fsn-men-thobes", "https://images.unsplash.com/photo-1590735213920-68192a487bc2?auto=format&fit=crop&w=480"),
        new("Retro Running Sneakers — White/Grey", "FSN-MSH-RUN-WG", 499, "fsn-men-shoes", "https://images.unsplash.com/photo-1542291026-7eec264c27ff?auto=format&fit=crop&w=480"),
        new("Classic Leather Loafers — Brown", "FSN-MSH-LOF-BR", 449, "fsn-men-shoes", "https://images.unsplash.com/photo-1614252369475-531eba835eb1?auto=format&fit=crop&w=480"),
        new("Oud Rose EDP 100ml — Unisex", "FSN-BF-OUD-100", 599, "fsn-perfume", "https://images.unsplash.com/photo-1541643600914-78b084683601?auto=format&fit=crop&w=480"),
        new("French Vanilla EDP 50ml — Women", "FSN-BF-VAN-50", 349, "fsn-perfume", "https://images.unsplash.com/photo-1541643600914-78b084683601?auto=format&fit=crop&w=480"),
        new("Vitamin C Serum 30ml — Radiance Boost", "FSN-BS-VITC-30", 189, "fsn-skincare", "https://images.unsplash.com/photo-1556228578-0d85b1a4d571?auto=format&fit=crop&w=480"),
        new("Hydrating Face Cream 50ml — SPF 30", "FSN-BS-HYD-50", 149, "fsn-skincare", "https://images.unsplash.com/photo-1556228578-0d85b1a4d571?auto=format&fit=crop&w=480"),
        new("Matte Lipstick Set — 6 Shades", "FSN-BM-LIP-SET", 129, "fsn-makeup", "https://images.unsplash.com/photo-1586495777744-4413f21062fa?auto=format&fit=crop&w=480"),
        new("Leather Tote Bag — Camel", "FSN-AB-TOTE-CM", 549, "fsn-bags", "https://images.unsplash.com/photo-1548036328-c9fa89d128fa?auto=format&fit=crop&w=480"),
        new("Mini Crossbody — Black", "FSN-AB-CROSS-BK", 299, "fsn-bags", "https://images.unsplash.com/photo-1548036328-c9fa89d128fa?auto=format&fit=crop&w=480"),
        new("Gold-Plated Statement Necklace", "FSN-AJ-NECK-GLD", 179, "fsn-jewellery", "https://images.unsplash.com/photo-1515562141589-67f0d727b750?auto=format&fit=crop&w=480"),
        new("Oversized Square Sunglasses — Tortoise", "FSN-ASG-SQ-TRT", 249, "fsn-sunglasses", "https://images.unsplash.com/photo-1511499767150-a48a237f0083?auto=format&fit=crop&w=480"),
        new("Aviator Polarized Sunglasses — Gold Frame", "FSN-ASG-AV-GLD", 299, "fsn-sunglasses", "https://images.unsplash.com/photo-1511499767150-a48a237f0083?auto=format&fit=crop&w=480"),
        new("Performance Running Tights — Black", "FSN-SP-RUN-BLK", 199, "fsn-sports", "https://images.unsplash.com/photo-1571902943202-507ec2618e8f?auto=format&fit=crop&w=480"),
        new("Yoga Mat Premium 6mm — Sage Green", "FSN-SP-YGA-SGE", 149, "fsn-sports", "https://images.unsplash.com/photo-1571902943202-507ec2618e8f?auto=format&fit=crop&w=480"),
        new("Boys Cotton T-Shirt Pack (3) — Multi", "FSN-KD-TSHRT-3", 99, "fsn-kids", "https://images.unsplash.com/photo-1519238263530-99bdd11df2ea?auto=format&fit=crop&w=480"),
    ];

    private static readonly Product[] JewelleryProducts =
    [
        new("22K Gold Chain Necklace 20\" — 15g", "JWL-GN-22K-15G", 5250, "jwl-gold-necklace", "https://images.unsplash.com/photo-1599643478518-a784e5dc4c8f?auto=format&fit=crop&w=480"),
        new("21K Gold Necklace with Pendant — 12g", "JWL-GN-21K-12G", 3960, "jwl-gold-necklace", "https://images.unsplash.com/photo-1599643478518-a784e5dc4c8f?auto=format&fit=crop&w=480"),
        new("18K Italian Gold Choker — Rose Gold 8g", "JWL-GN-18K-CHK", 2640, "jwl-gold-necklace", "https://images.unsplash.com/photo-1515562141589-67f0d727b750?auto=format&fit=crop&w=480"),
        new("22K Gold Bangle Set (4 pcs) — 40g", "JWL-GB-22K-40G", 14000, "jwl-gold-bangles", "https://images.unsplash.com/photo-1535632066927-ab7c9ab60908?auto=format&fit=crop&w=480"),
        new("21K Gold Bangle — Twisted Design 12g", "JWL-GB-21K-12G", 3960, "jwl-gold-bangles", "https://images.unsplash.com/photo-1535632066927-ab7c9ab60908?auto=format&fit=crop&w=480"),
        new("22K Gold Wedding Band — 6g", "JWL-GR-22K-6G", 2100, "jwl-gold-rings", "https://images.unsplash.com/photo-1605100804763-247f67b3557e?auto=format&fit=crop&w=480"),
        new("21K Gold Statement Ring — Filigree 8g", "JWL-GR-21K-8G", 2640, "jwl-gold-rings", "https://images.unsplash.com/photo-1605100804763-247f67b3557e?auto=format&fit=crop&w=480"),
        new("22K Gold Jhumka Earrings — 10g", "JWL-GE-22K-JHM", 3500, "jwl-gold-earrings", "https://images.unsplash.com/photo-1535632787350-4e68ef0ac584?auto=format&fit=crop&w=480"),
        new("Solitaire Diamond Ring 1.0ct — Platinum", "JWL-DR-SOL-100", 22000, "jwl-dia-rings", "https://images.unsplash.com/photo-1605100804763-247f67b3557e?auto=format&fit=crop&w=480"),
        new("Halo Diamond Ring 0.75ct — 18K White Gold", "JWL-DR-HAL-075", 15000, "jwl-dia-rings", "https://images.unsplash.com/photo-1605100804763-247f67b3557e?auto=format&fit=crop&w=480"),
        new("Three-Stone Diamond Ring 1.5ct Total", "JWL-DR-3ST-150", 28000, "jwl-dia-rings", "https://images.unsplash.com/photo-1605100804763-247f67b3557e?auto=format&fit=crop&w=480"),
        new("Diamond Tennis Necklace 5.0ct — 18K Gold", "JWL-DN-TEN-500", 45000, "jwl-dia-necklace", "https://images.unsplash.com/photo-1515562141589-67f0d727b750?auto=format&fit=crop&w=480"),
        new("Diamond Pendant 0.50ct — 18K White Gold", "JWL-DN-PND-050", 7500, "jwl-dia-necklace", "https://images.unsplash.com/photo-1515562141589-67f0d727b750?auto=format&fit=crop&w=480"),
        new("Diamond Stud Earrings 1.0ct Total — 18K", "JWL-DE-STD-100", 12000, "jwl-dia-earrings", "https://images.unsplash.com/photo-1535632787350-4e68ef0ac584?auto=format&fit=crop&w=480"),
        new("Bridal Set — 22K Necklace + Earrings + Ring", "JWL-BR-SET-22K", 35000, "jwl-bridal-sets", "https://images.unsplash.com/photo-1599643478518-a784e5dc4c8f?auto=format&fit=crop&w=480"),
        new("Diamond Engagement Ring Solitaire 0.80ct", "JWL-BR-ENG-080", 18000, "jwl-bridal-rings", "https://images.unsplash.com/photo-1605100804763-247f67b3557e?auto=format&fit=crop&w=480"),
        new("18K Gold Rope Chain 18\" — 5g", "JWL-EV-ROPE-5G", 1650, "jwl-chains", "https://images.unsplash.com/photo-1599643478518-a784e5dc4c8f?auto=format&fit=crop&w=480"),
        new("18K Gold Heart Pendant — 3g", "JWL-EV-HEART-3G", 990, "jwl-pendants", "https://images.unsplash.com/photo-1515562141589-67f0d727b750?auto=format&fit=crop&w=480"),
        new("Swiss Automatic Watch — Rose Gold Dial", "JWL-WTC-SWISS-RG", 8500, "jwl-watches", "https://images.unsplash.com/photo-1524592094714-0f0654e20314?auto=format&fit=crop&w=480"),
        new("Diamond-Set Ladies Watch — 18K Gold", "JWL-WTC-DIA-18K", 15000, "jwl-watches", "https://images.unsplash.com/photo-1524592094714-0f0654e20314?auto=format&fit=crop&w=480"),
        new("Sterling Silver Cuff Bracelet — Hammered", "JWL-SLV-CUFF-HM", 350, "jwl-silver", "https://images.unsplash.com/photo-1535632066927-ab7c9ab60908?auto=format&fit=crop&w=480"),
        new("Sterling Silver Pendant — Evil Eye", "JWL-SLV-EYE-PND", 180, "jwl-silver", "https://images.unsplash.com/photo-1515562141589-67f0d727b750?auto=format&fit=crop&w=480"),
        new("South Sea Pearl Necklace 18\" — White", "JWL-PRL-SS-18", 12000, "jwl-pearls", "https://images.unsplash.com/photo-1515562141589-67f0d727b750?auto=format&fit=crop&w=480"),
        new("Freshwater Pearl Stud Earrings — 8mm", "JWL-PRL-STD-8", 450, "jwl-pearls", "https://images.unsplash.com/photo-1535632787350-4e68ef0ac584?auto=format&fit=crop&w=480"),
    ];

    private static readonly Product[] ConsultingProducts =
    [
        new("VAT Registration — New Business", "CNS-VAT-REG-NEW", 1500, "cns-vat-reg", "https://images.unsplash.com/photo-1554224155-6726b3ff858f?auto=format&fit=crop&w=480"),
        new("VAT Registration — Group Registration", "CNS-VAT-REG-GRP", 3500, "cns-vat-reg", "https://images.unsplash.com/photo-1554224155-6726b3ff858f?auto=format&fit=crop&w=480"),
        new("VAT Return Filing — Quarterly", "CNS-VAT-FIL-Q", 1000, "cns-vat-filing", "https://images.unsplash.com/photo-1460925895917-afdab827c52f?auto=format&fit=crop&w=480"),
        new("VAT Return Filing — Monthly", "CNS-VAT-FIL-M", 800, "cns-vat-filing", "https://images.unsplash.com/photo-1460925895917-afdab827c52f?auto=format&fit=crop&w=480"),
        new("VAT Health Check & Compliance Review", "CNS-VAT-HLTH", 5000, "cns-vat-audit", "https://images.unsplash.com/photo-1450101499163-c8848c66ca85?auto=format&fit=crop&w=480"),
        new("Corporate Tax Registration", "CNS-CT-REG", 2000, "cns-ct-reg", "https://images.unsplash.com/photo-1554224155-6726b3ff858f?auto=format&fit=crop&w=480"),
        new("Corporate Tax Return Filing — Annual", "CNS-CT-FIL-A", 5000, "cns-ct-filing", "https://images.unsplash.com/photo-1460925895917-afdab827c52f?auto=format&fit=crop&w=480"),
        new("Transfer Pricing Documentation", "CNS-CT-TP-DOC", 8000, "cns-ct-filing", "https://images.unsplash.com/photo-1450101499163-c8848c66ca85?auto=format&fit=crop&w=480"),
        new("External Audit — SME", "CNS-AUD-EXT-SM", 8000, "cns-audit-ext", "https://images.unsplash.com/photo-1454165804606-c3d57bc86b40?auto=format&fit=crop&w=480"),
        new("External Audit — Enterprise", "CNS-AUD-EXT-EN", 25000, "cns-audit-ext", "https://images.unsplash.com/photo-1454165804606-c3d57bc86b40?auto=format&fit=crop&w=480"),
        new("Internal Audit — Quarterly Review", "CNS-AUD-INT-Q", 6000, "cns-audit-int", "https://images.unsplash.com/photo-1454165804606-c3d57bc86b40?auto=format&fit=crop&w=480"),
        new("Monthly Bookkeeping — Starter (< 100 txn)", "CNS-BK-STR-M", 1500, "cns-bookkeeping", "https://images.unsplash.com/photo-1554224155-6726b3ff858f?auto=format&fit=crop&w=480"),
        new("Monthly Bookkeeping — Growth (100-500 txn)", "CNS-BK-GRW-M", 3000, "cns-bookkeeping", "https://images.unsplash.com/photo-1554224155-6726b3ff858f?auto=format&fit=crop&w=480"),
        new("Monthly Bookkeeping — Enterprise (500+ txn)", "CNS-BK-ENT-M", 5000, "cns-bookkeeping", "https://images.unsplash.com/photo-1554224155-6726b3ff858f?auto=format&fit=crop&w=480"),
        new("AML/CFT Compliance Setup & Training", "CNS-AML-SETUP", 7500, "cns-aml", "https://images.unsplash.com/photo-1450101499163-c8848c66ca85?auto=format&fit=crop&w=480"),
        new("AML Ongoing Monitoring — Annual", "CNS-AML-MON-A", 3000, "cns-aml", "https://images.unsplash.com/photo-1450101499163-c8848c66ca85?auto=format&fit=crop&w=480"),
        new("Regulatory Compliance Filing", "CNS-ESR-FILE", 2500, "cns-esrub", "https://images.unsplash.com/photo-1460925895917-afdab827c52f?auto=format&fit=crop&w=480"),
        new("Beneficial Ownership Declaration", "CNS-UBO-FILE", 1500, "cns-esrub", "https://images.unsplash.com/photo-1460925895917-afdab827c52f?auto=format&fit=crop&w=480"),
        new("Business Plan & Financial Projections", "CNS-ADV-BPLAN", 10000, "cns-advisory", "https://images.unsplash.com/photo-1454165804606-c3d57bc86b40?auto=format&fit=crop&w=480"),
        new("CFO-as-a-Service — Monthly Retainer", "CNS-ADV-CFO-M", 8000, "cns-advisory", "https://images.unsplash.com/photo-1454165804606-c3d57bc86b40?auto=format&fit=crop&w=480"),
        new("Company Formation — Standard", "CNS-CO-MAIN-DXB", 15000, "cns-setup", "https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?auto=format&fit=crop&w=480"),
        new("Company Formation — Premium", "CNS-CO-FZ-DMCC", 12000, "cns-setup", "https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?auto=format&fit=crop&w=480"),
        new("Company Formation — Express", "CNS-CO-FZ-IFZA", 8500, "cns-setup", "https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?auto=format&fit=crop&w=480"),
    ];
}
