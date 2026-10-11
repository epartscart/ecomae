using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-data helpers. PHP identifiers kept for the inventory:
/// <c>epc_jewellery_retail_kiyasha_image_catalog</c>, <c>epc_jewellery_retail_kiyasha_img</c>,
/// <c>epc_jewellery_retail_kiyasha_img_alt</c>, <c>epc_jewellery_retail_kiyasha_promo_strip</c>,
/// <c>epc_jewellery_retail_kiyasha_trust_badges</c>, <c>epc_jewellery_retail_kiyasha_departments</c>,
/// <c>epc_jewellery_retail_kiyasha_collection_tabs</c>, <c>epc_jewellery_retail_kiyasha_category_chips</c>,
/// <c>epc_jewellery_retail_kiyasha_brand_filters</c>, <c>epc_jewellery_retail_kiyasha_hero_slides</c>,
/// <c>epc_jewellery_retail_kiyasha_category_tiles</c>, <c>epc_jewellery_retail_kiyasha_product_sections</c>,
/// <c>epc_jewellery_retail_kiyasha_resolve_product_images</c>, <c>epc_jewellery_retail_kiyasha_format_aed</c>,
/// <c>epc_jewellery_retail_kiyasha_product_badge</c>, <c>epc_jewellery_retail_kiyasha_utility_links</c>,
/// <c>epc_jewellery_retail_kiyasha_mega_nav</c>, <c>epc_jewellery_retail_kiyasha_footer_columns</c>,
/// <c>epc_jewellery_retail_kiyasha_social_links</c>, <c>epc_jewellery_retail_kiyasha_payment_methods</c>,
/// <c>epc_jrk_pro_hero_eyebrow</c>, <c>epc_jrk_pro_hero_title</c>, <c>epc_jrk_pro_hero_copy</c>,
/// <c>epc_jrk_pro_hero_actions</c>, <c>epc_jrk_pro_hero_stats</c>,
/// <c>epc_fashion_retail_namshi_image_catalog</c>, <c>epc_fashion_retail_namshi_img</c>,
/// <c>epc_fashion_retail_namshi_img_alt</c>, <c>epc_fashion_retail_namshi_promo_strip</c>,
/// <c>epc_fashion_retail_namshi_trust_badges</c>, <c>epc_fashion_retail_namshi_departments</c>,
/// <c>epc_fashion_retail_namshi_beauty_tabs</c>, <c>epc_fashion_retail_namshi_category_chips</c>,
/// <c>epc_fashion_retail_namshi_brand_filters</c>, <c>epc_fashion_retail_namshi_hero_slides</c>,
/// <c>epc_fashion_retail_namshi_category_tiles</c>, <c>epc_fashion_retail_namshi_product_sections</c>,
/// <c>epc_fashion_retail_namshi_resolve_product_images</c>, <c>epc_fashion_retail_namshi_format_aed</c>,
/// <c>epc_fashion_retail_namshi_product_badge</c>, <c>epc_fashion_retail_namshi_utility_links</c>,
/// <c>epc_fashion_retail_namshi_mega_nav</c>, <c>epc_fashion_retail_namshi_footer_columns</c>,
/// <c>epc_fashion_retail_namshi_social_links</c>, <c>epc_fashion_retail_namshi_payment_methods</c>,
/// <c>epc_frn_pro_hero_eyebrow</c>, <c>epc_frn_pro_hero_title</c>, <c>epc_frn_pro_hero_copy</c>,
/// <c>epc_frn_pro_hero_actions</c>, <c>epc_frn_pro_hero_stats</c>,
/// <c>epc_electronics_retail_image_catalog</c>, <c>epc_electronics_retail_img</c>,
/// <c>epc_electronics_retail_img_alt</c>, <c>epc_electronics_retail_promo_strip</c>,
/// <c>epc_electronics_retail_trust_badges</c>, <c>epc_electronics_retail_hero_slides</c>,
/// <c>epc_electronics_retail_category_tiles</c>, <c>epc_electronics_retail_deal_sections</c>,
/// <c>epc_electronics_retail_resolve_product_images</c>, <c>epc_electronics_retail_brands</c>,
/// <c>epc_electronics_retail_featured_categories</c>, <c>epc_electronics_retail_format_aed</c>,
/// <c>epc_electronics_retail_utility_links</c>, <c>epc_electronics_retail_mega_nav</c>,
/// <c>epc_electronics_retail_footer_columns</c>, <c>epc_electronics_retail_social_links</c>,
/// <c>epc_electronics_retail_payment_methods</c>, <c>epc_electronics_retail_product_badge</c>,
/// <c>epc_er_pro_hero_eyebrow</c>, <c>epc_er_pro_hero_title</c>, <c>epc_er_pro_hero_copy</c>,
/// <c>epc_er_pro_hero_actions</c>, <c>epc_er_pro_hero_stats</c>.
/// </summary>
public static class PhpPlanQ1Data
{
    public const string JewelleryDataPath = "content/general_pages/epc_jewellery_retail_kiyasha_data.php";
    public const string FashionDataPath = "content/general_pages/epc_fashion_retail_namshi_data.php";
    public const string ElectronicsDataPath = "content/general_pages/epc_electronics_retail_data.php";

    private static readonly Lazy<JsonElement> Jrk = new(() => JsonDocument.Parse(PhpPlanQ1DataJson.JewelleryJson).RootElement.Clone());
    private static readonly Lazy<JsonElement> Frn = new(() => JsonDocument.Parse(PhpPlanQ1DataJson.FashionJson).RootElement.Clone());
    private static readonly Lazy<JsonElement> Er = new(() => JsonDocument.Parse(PhpPlanQ1DataJson.ElectronicsJson).RootElement.Clone());

    public static JsonElement EpcJewelleryRetailKiyashaImageCatalog() => Jrk.Value.GetProperty("images").Clone();
    public static JsonElement EpcJewelleryRetailKiyashaPromoStrip() => Jrk.Value.GetProperty("promo").Clone();
    public static JsonElement EpcJewelleryRetailKiyashaTrustBadges() => Jrk.Value.GetProperty("trust").Clone();
    public static JsonElement EpcJewelleryRetailKiyashaDepartments() => Jrk.Value.GetProperty("departments").Clone();
    public static JsonElement EpcJewelleryRetailKiyashaCollectionTabs() => Jrk.Value.GetProperty("tabs").Clone();
    public static JsonElement EpcJewelleryRetailKiyashaCategoryChips() => Jrk.Value.GetProperty("chips").Clone();
    public static JsonElement EpcJewelleryRetailKiyashaBrandFilters() => Jrk.Value.GetProperty("brands").Clone();
    public static JsonElement EpcJewelleryRetailKiyashaHeroSlides() => Jrk.Value.GetProperty("hero").Clone();
    public static JsonElement EpcJewelleryRetailKiyashaCategoryTiles() => Jrk.Value.GetProperty("tiles").Clone();
    public static JsonElement EpcJewelleryRetailKiyashaProductSections() => Jrk.Value.GetProperty("sections").Clone();
    public static JsonElement EpcJewelleryRetailKiyashaUtilityLinks() => Jrk.Value.GetProperty("utility").Clone();
    public static JsonElement EpcJewelleryRetailKiyashaMegaNav() => Jrk.Value.GetProperty("mega").Clone();
    public static JsonElement EpcJewelleryRetailKiyashaSocialLinks() => Jrk.Value.GetProperty("social").Clone();
    public static JsonElement EpcJewelleryRetailKiyashaPaymentMethods() => Jrk.Value.GetProperty("payments").Clone();
    public static string EpcJrkProHeroEyebrow() => Jrk.Value.GetProperty("eyebrow").GetString() ?? "";
    public static string EpcJrkProHeroTitle() => Jrk.Value.GetProperty("title").GetString() ?? "";
    public static string EpcJrkProHeroCopy() => Jrk.Value.GetProperty("copy").GetString() ?? "";
    public static JsonElement EpcJrkProHeroActions(string lang) => Jrk.Value.GetProperty("actions").Clone();
    public static JsonElement EpcJrkProHeroStats() => Jrk.Value.GetProperty("stats").Clone();

    public static string EpcJewelleryRetailKiyashaImg(string keyOrPath, int w = 400, int h = 0)
        => StorefrontImg(EpcJewelleryRetailKiyashaImageCatalog(), "jewellery", keyOrPath, w, h);

    public static string EpcJewelleryRetailKiyashaImgAlt(string key)
        => ImgAlt(EpcJewelleryRetailKiyashaImageCatalog(), key);

    public static Dictionary<string, object?> EpcJewelleryRetailKiyashaResolveProductImages(Dictionary<string, object?> product)
        => ResolveProductImages(product, EpcJewelleryRetailKiyashaImg, EpcJewelleryRetailKiyashaImgAlt);

    public static string EpcJewelleryRetailKiyashaFormatAed(object? amount) => FormatAed(amount);

    public static string EpcJewelleryRetailKiyashaProductBadge(Dictionary<string, object?> product) => ProductBadge(product);

    public static object[] EpcJewelleryRetailKiyashaFooterColumns()
        => FooterColumns("The Jewellery Trend", JewelleryFooterLinks());

    public static JsonElement EpcFashionRetailNamshiImageCatalog() => Frn.Value.GetProperty("images").Clone();
    public static JsonElement EpcFashionRetailNamshiPromoStrip() => Frn.Value.GetProperty("promo").Clone();
    public static JsonElement EpcFashionRetailNamshiTrustBadges() => Frn.Value.GetProperty("trust").Clone();
    public static JsonElement EpcFashionRetailNamshiDepartments() => Frn.Value.GetProperty("departments").Clone();
    public static JsonElement EpcFashionRetailNamshiBeautyTabs() => Frn.Value.GetProperty("tabs").Clone();
    public static JsonElement EpcFashionRetailNamshiCategoryChips() => Frn.Value.GetProperty("chips").Clone();
    public static JsonElement EpcFashionRetailNamshiBrandFilters() => Frn.Value.GetProperty("brands").Clone();
    public static JsonElement EpcFashionRetailNamshiHeroSlides() => Frn.Value.GetProperty("hero").Clone();
    public static JsonElement EpcFashionRetailNamshiCategoryTiles() => Frn.Value.GetProperty("tiles").Clone();
    public static JsonElement EpcFashionRetailNamshiProductSections() => Frn.Value.GetProperty("sections").Clone();
    public static JsonElement EpcFashionRetailNamshiUtilityLinks() => Frn.Value.GetProperty("utility").Clone();
    public static JsonElement EpcFashionRetailNamshiMegaNav() => Frn.Value.GetProperty("mega").Clone();
    public static JsonElement EpcFashionRetailNamshiSocialLinks() => Frn.Value.GetProperty("social").Clone();
    public static JsonElement EpcFashionRetailNamshiPaymentMethods() => Frn.Value.GetProperty("payments").Clone();
    public static string EpcFrnProHeroEyebrow() => Frn.Value.GetProperty("eyebrow").GetString() ?? "";
    public static string EpcFrnProHeroTitle() => Frn.Value.GetProperty("title").GetString() ?? "";
    public static string EpcFrnProHeroCopy() => Frn.Value.GetProperty("copy").GetString() ?? "";
    public static JsonElement EpcFrnProHeroActions(string lang) => Frn.Value.GetProperty("actions").Clone();
    public static JsonElement EpcFrnProHeroStats() => Frn.Value.GetProperty("stats").Clone();

    public static string EpcFashionRetailNamshiImg(string keyOrPath, int w = 400, int h = 0)
        => StorefrontImg(EpcFashionRetailNamshiImageCatalog(), "fashion", keyOrPath, w, h);

    public static string EpcFashionRetailNamshiImgAlt(string key)
        => ImgAlt(EpcFashionRetailNamshiImageCatalog(), key);

    public static Dictionary<string, object?> EpcFashionRetailNamshiResolveProductImages(Dictionary<string, object?> product)
        => ResolveProductImages(product, EpcFashionRetailNamshiImg, EpcFashionRetailNamshiImgAlt);

    public static string EpcFashionRetailNamshiFormatAed(object? amount) => FormatAed(amount);

    public static string EpcFashionRetailNamshiProductBadge(Dictionary<string, object?> product) => ProductBadge(product);

    public static object[] EpcFashionRetailNamshiFooterColumns()
        => FooterColumns("Stylenlook", FashionFooterLinks());

    public static JsonElement EpcElectronicsRetailImageCatalog() => Er.Value.GetProperty("images").Clone();
    public static JsonElement EpcElectronicsRetailPromoStrip() => Er.Value.GetProperty("promo").Clone();
    public static JsonElement EpcElectronicsRetailTrustBadges() => Er.Value.GetProperty("trust").Clone();
    public static JsonElement EpcElectronicsRetailHeroSlides() => Er.Value.GetProperty("hero").Clone();
    public static JsonElement EpcElectronicsRetailCategoryTiles() => Er.Value.GetProperty("tiles").Clone();
    public static JsonElement EpcElectronicsRetailDealSections() => Er.Value.GetProperty("deals").Clone();
    public static JsonElement EpcElectronicsRetailBrands() => Er.Value.GetProperty("brands").Clone();
    public static JsonElement EpcElectronicsRetailFeaturedCategories() => Er.Value.GetProperty("featured").Clone();
    public static JsonElement EpcElectronicsRetailUtilityLinks() => Er.Value.GetProperty("utility").Clone();
    public static JsonElement EpcElectronicsRetailMegaNav() => Er.Value.GetProperty("mega").Clone();
    public static JsonElement EpcElectronicsRetailSocialLinks() => Er.Value.GetProperty("social").Clone();
    public static JsonElement EpcElectronicsRetailPaymentMethods() => Er.Value.GetProperty("payments").Clone();
    public static string EpcErProHeroEyebrow() => Er.Value.GetProperty("eyebrow").GetString() ?? "";
    public static string EpcErProHeroTitle() => Er.Value.GetProperty("title").GetString() ?? "";
    public static string EpcErProHeroCopy() => Er.Value.GetProperty("copy").GetString() ?? "";
    public static JsonElement EpcErProHeroActions(string lang) => Er.Value.GetProperty("actions").Clone();
    public static JsonElement EpcErProHeroStats() => Er.Value.GetProperty("stats").Clone();

    public static string EpcElectronicsRetailImg(string keyOrPath, int w = 400, int h = 0)
        => StorefrontImg(EpcElectronicsRetailImageCatalog(), "electronics", keyOrPath, w, h);

    public static string EpcElectronicsRetailImgAlt(string key)
        => ImgAlt(EpcElectronicsRetailImageCatalog(), key);

    public static Dictionary<string, object?> EpcElectronicsRetailResolveProductImages(Dictionary<string, object?> product)
        => ResolveProductImages(product, EpcElectronicsRetailImg, EpcElectronicsRetailImgAlt);

    public static string EpcElectronicsRetailFormatAed(object? amount) => FormatAed(amount);

    public static string EpcElectronicsRetailProductBadge(Dictionary<string, object?> product) => ProductBadge(product);

    public static object[] EpcElectronicsRetailFooterColumns()
        => FooterColumns("Electronicae", ElectronicsFooterLinks());

    private static string StorefrontImg(JsonElement catalog, string folder, string keyOrPath, int w, int h)
    {
        if (catalog.TryGetProperty(keyOrPath, out _))
        {
            return "/content/files/images/storefronts/" + folder + "/" + keyOrPath + ".jpg";
        }

        var path = (keyOrPath ?? "").TrimStart('/');
        var url = "https://images.unsplash.com/" + path + "?auto=format&fit=crop&w=" + w.ToString(CultureInfo.InvariantCulture) + "&q=82";
        if (h > 0)
        {
            url += "&h=" + h.ToString(CultureInfo.InvariantCulture);
        }

        return url;
    }

    private static string ImgAlt(JsonElement catalog, string key)
        => catalog.TryGetProperty(key, out var row) && row.TryGetProperty("alt", out var alt) ? alt.GetString() ?? "" : "";

    private static Dictionary<string, object?> ResolveProductImages(
        Dictionary<string, object?> product,
        Func<string, int, int, string> img,
        Func<string, string> imgAlt)
    {
        var copy = new Dictionary<string, object?>(product, StringComparer.Ordinal);
        var key = copy.TryGetValue("key", out var rawKey) ? Convert.ToString(rawKey, CultureInfo.InvariantCulture) ?? "" : "";
        if (key != "" && !copy.ContainsKey("image"))
        {
            copy["image"] = img(key, 480, 480);
        }

        if (key != "" && IsPhpEmpty(copy.TryGetValue("alt", out var alt) ? alt : null))
        {
            var resolved = imgAlt(key);
            copy["alt"] = resolved == "" ? copy.TryGetValue("name", out var name) ? name : "" : resolved;
        }

        return copy;
    }

    public static string FormatAed(object? amount)
    {
        var value = amount is null ? 0d : Convert.ToDouble(amount, CultureInfo.InvariantCulture);
        if (value <= 0)
        {
            return "";
        }

        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        return "AED " + rounded.ToString("N0", CultureInfo.GetCultureInfo("en-US"));
    }

    public static string ProductBadge(Dictionary<string, object?> product)
    {
        if (!IsPhpEmpty(product.TryGetValue("badge", out var badge) ? badge : null))
        {
            return Convert.ToString(badge, CultureInfo.InvariantCulture) ?? "";
        }

        var was = ToFloat(product.TryGetValue("was", out var rawWas) ? rawWas : null);
        var price = ToFloat(product.TryGetValue("price", out var rawPrice) ? rawPrice : null);
        if (was > 0 && price > 0 && was > price)
        {
            return "sale";
        }

        if (!IsPhpEmpty(product.TryGetValue("is_new", out var isNew) ? isNew : null))
        {
            return "new";
        }

        return "";
    }

    private static object[] FooterColumns(string store, (string Title, (string Label, string Href)[] Links)[] columns)
    {
        return columns.Select(col => new Dictionary<string, object?>
        {
            ["title"] = col.Title.Replace("{store}", store, StringComparison.Ordinal),
            ["links"] = col.Links.Select(l => new Dictionary<string, object?> { ["label"] = l.Label, ["href"] = l.Href }).ToArray()
        }).ToArray();
    }

    private static (string Title, (string Label, string Href)[] Links)[] JewelleryFooterLinks()
        =>
        [
            ("Customer Care", [
                ("Contact Us", "/kontakty"),
                ("Ring Sizing Guide", "/kontakty"),
                ("Delivery & Insurance", "/o-dostavke"),
                ("Returns", "/ob-oplate"),
                ("Track Order", "/shop/orders")
            ]),
            ("Collections", [
                ("Rings", "/gold/rings"),
                ("Necklaces", "/gold/necklaces"),
                ("Earrings", "/gold/earrings"),
                ("Bridal", "/bridal"),
                ("Sale", "/gold")
            ]),
            ("About {store}", [
                ("Our Story", "/kontakty"),
                ("Gold Certification", "/kontakty"),
                ("Gift Cards", "/everyday"),
                ("Store Locator", "/kontakty")
            ]),
            ("My Account", [
                ("Sign In", "/users/register"),
                ("Orders", "/shop/orders"),
                ("Wishlist", "/shop/zakladki"),
                ("Addresses", "/shop/orders")
            ])
        ];

    private static (string Title, (string Label, string Href)[] Links)[] FashionFooterLinks()
        =>
        [
            ("Customer Care", [
                ("Contact Us", "/kontakty"),
                ("FAQs", "/kontakty"),
                ("Delivery", "/o-dostavke"),
                ("Returns & Refunds", "/ob-oplate"),
                ("Track Order", "/shop/orders")
            ]),
            ("Shop", [
                ("Women", "/women"),
                ("Men", "/men"),
                ("Beauty", "/beauty"),
                ("Shoes & Bags", "/women/shoes"),
                ("Sale", "/women")
            ]),
            ("About {store}", [
                ("About Us", "/kontakty"),
                ("Careers", "/kontakty"),
                ("Gift Cards", "/beauty"),
                ("Brand Directory", "/women")
            ]),
            ("My Account", [
                ("Sign In", "/users/register"),
                ("Orders", "/shop/orders"),
                ("Wishlist", "/shop/zakladki"),
                ("Addresses", "/shop/orders")
            ])
        ];

    private static (string Title, (string Label, string Href)[] Links)[] ElectronicsFooterLinks()
        =>
        [
            ("Customer Service", [
                ("Contact Us", "/kontakty"),
                ("FAQs", "/kontakty"),
                ("Delivery Information", "/o-dostavke"),
                ("Returns & Refunds", "/ob-oplate"),
                ("Track Your Order", "/shop/orders")
            ]),
            ("About {store}", [
                ("About Us", "/kontakty"),
                ("Careers", "/kontakty"),
                ("Store Locator", "/kontakty"),
                ("Gift Cards", "/smartphones")
            ]),
            ("Shop", [
                ("Gaming", "/gaming"),
                ("Tech & Gadgets", "/smartphones"),
                ("Laptops & Tablets", "/laptops"),
                ("Mobiles", "/smartphones"),
                ("Books & Music", "/tablets"),
                ("Online Exclusives", "/smartphones")
            ]),
            ("My Account", [
                ("Sign In", "/users/register"),
                ("My Orders", "/shop/orders"),
                ("Wishlist", "/shop/zakladki"),
                ("Loyalty Program", "/kontakty")
            ])
        ];

    private static bool IsPhpEmpty(object? value)
        => value is null
            || value is false
            || value is ""
            || value is 0
            || value is 0L
            || value is 0d
            || (value is string s && (s.Length == 0 || s == "0"))
            || (value is JsonElement el && (el.ValueKind is JsonValueKind.Null or JsonValueKind.False
                || (el.ValueKind == JsonValueKind.String && (el.GetString() is "" or "0"))
                || (el.ValueKind == JsonValueKind.Number && el.GetDouble() == 0)));

    private static double ToFloat(object? value)
        => value is null ? 0d : Convert.ToDouble(value, CultureInfo.InvariantCulture);
}
