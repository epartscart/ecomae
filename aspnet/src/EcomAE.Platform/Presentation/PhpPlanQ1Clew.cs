namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-clew shipping-export page. Path kept for the inventory:
/// <c>content/general_pages/epc_seo_shipping_export.php</c>.
/// GET never mints a session cookie. Leftover SEO parents stay injected.
/// </summary>
public static class PhpPlanQ1Clew
{
    public const string SeoShippingExportPath = "content/general_pages/epc_seo_shipping_export.php";

    public static Func<string>? CurrentLang { get; set; }
    public static Func<string>? LangHref { get; set; }
    public static Func<string>? TenantCountry { get; set; }
    public static Func<string, string>? ShippingPhrase { get; set; }
    public static Func<string, string>? CourierPhrase { get; set; }
    public static bool HasPdo { get; set; } = true;
    public static string DomainPath { get; set; } = "https://www.epartscart.com/";
    public static string PartsSlug { get; set; } = "parts";

    private static readonly Dictionary<string, Dictionary<string, string>> Copy = new(StringComparer.Ordinal)
    {
        ["en"] = new(StringComparer.Ordinal)
        {
            ["title"] = "Shipping & export",
            ["lead"] = "eParts Cart ships from UAE, Oman and KSA warehouses — fast delivery across the GCC and worldwide export.",
            ["gcc"] = "GCC delivery: UAE, Saudi Arabia, Oman, Qatar, Bahrain, and Kuwait.",
            ["couriers"] = "Courier options: DHL, FedEx, Aramex, UPS and other carriers depending on destination.",
            ["world"] = "Worldwide export: contact us for customs documentation and freight quotes.",
            ["cta_parts"] = "Search parts",
            ["cta_brands"] = "Browse brands"
        },
        ["ar"] = new(StringComparer.Ordinal)
        {
            ["title"] = "الشحن والتصدير",
            ["lead"] = "يشحن eParts Cart من مستودعات الإمارات وعُمان والسعودية — توصيل سريع إلى دول الخليج وتصدير عالمي.",
            ["gcc"] = "توصيل الخليج: الإمارات، السعودية، عُمان، قطر، البحرين، والكويت.",
            ["couriers"] = "خيارات الشحن: DHL، FedEx، Aramex، UPS وغيرها حسب الوجهة.",
            ["world"] = "تصدير عالمي: تواصل معنا لوثائق الجمارك وعروض الشحن.",
            ["cta_parts"] = "بحث القطع",
            ["cta_brands"] = "الماركات"
        },
        ["ru"] = new(StringComparer.Ordinal)
        {
            ["title"] = "Доставка и экспорт",
            ["lead"] = "eParts Cart отправляет со складов ОАЭ, Омана и КСА — быстрая доставка в GCC и экспорт по всему миру.",
            ["gcc"] = "GCC: ОАЭ, Саудовская Аравия, Оман, Катар, Бахрейн, Кувейт.",
            ["couriers"] = "Курьеры: DHL, FedEx, Aramex, UPS и другие — в зависимости от направления.",
            ["world"] = "Мировой экспорт: таможенные документы и расчёт фрахта по запросу.",
            ["cta_parts"] = "Поиск запчастей",
            ["cta_brands"] = "Бренды"
        }
    };

    public static void Reset()
    {
        CurrentLang = () => "en";
        LangHref = () => "/en";
        TenantCountry = () => "AE";
        ShippingPhrase = _ => "Ships from UAE";
        CourierPhrase = _ => "DHL / FedEx";
        HasPdo = true;
        DomainPath = "https://www.epartscart.com/";
        PartsSlug = "parts";
    }

    public static string EpcSeoShippingExportRenderHtml()
    {
        var lang = CurrentLang != null ? CurrentLang() : "en";
        var langHref = LangHref != null ? LangHref() : "/en";
        var tenantCountry = HasPdo && TenantCountry != null ? TenantCountry() : "AE";
        var shipPhrase = ShippingPhrase != null ? ShippingPhrase(lang) : "";
        var courierPhrase = CourierPhrase != null ? CourierPhrase(lang) : "";
        var domain = DomainPath.TrimEnd('/');
        var partsUrl = domain + langHref + "/" + (PartsSlug == "" ? "parts" : PartsSlug);
        var brandsUrl = domain + langHref + "/available-brands";
        var t = Copy.TryGetValue(lang, out var row) ? row : Copy["en"];
        return "<div class=\"epc-shipping-export\" style=\"max-width:860px;margin:0 auto;padding:8px 0 24px;\">\n"
            + "\t<h1 style=\"margin:0 0 12px;font-size:28px;\">" + H(t["title"]) + "</h1>\n"
            + "\t<p style=\"font-size:16px;line-height:1.6;color:#333;\">" + H(t["lead"]) + "</p>\n"
            + "\t<p style=\"font-size:15px;line-height:1.6;color:#555;margin-top:12px;\"><strong>" + H(shipPhrase) + "</strong></p>\n"
            + "\t<p style=\"font-size:14px;line-height:1.6;color:#555;margin-top:8px;\">" + H(courierPhrase) + "</p>\n"
            + "\t<ul style=\"margin:16px 0 0 18px;line-height:1.7;font-size:15px;\">\n"
            + "\t\t<li>" + H(t["gcc"]) + "</li>\n"
            + "\t\t<li>" + H(t["couriers"]) + "</li>\n"
            + "\t\t<li>" + H(t["world"]) + "</li>\n"
            + "\t</ul>\n"
            + "\t<p style=\"margin-top:18px;font-size:14px;color:#666;\">Tenant country profile: " + H(tenantCountry) + " (UAE–Oman–KSA warehouse network).</p>\n"
            + "\t<p style=\"margin-top:16px;\">\n"
            + "\t\t<a class=\"btn btn-ar btn-primary\" href=\"" + H(partsUrl) + "\">" + H(t["cta_parts"]) + "</a>\n"
            + "\t\t&nbsp;\n"
            + "\t\t<a class=\"btn btn-default\" href=\"" + H(brandsUrl) + "\">" + H(t["cta_brands"]) + "</a>\n"
            + "\t</p>\n"
            + "</div>\n";
    }

    private static string H(string value)
        => value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal);
}
