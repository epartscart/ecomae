using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-mere storefront cart module.
/// Path: <c>modules/shop/cart/cart.php</c> (basename is not unique).
/// GET never mints a session cookie. Leftover unique user helper stays injected.
/// </summary>
public static class PhpPlanQ1Mere
{
    public const string CartModulePath = "modules/shop/cart/cart.php";

    public static Func<int>? GetUserId { get; set; }
    public static Func<Dictionary<string, object?>>? GetUserSession { get; set; }
    public static Func<Dictionary<string, object?>>? QueryCurrency { get; set; }
    public static Func<int, Dictionary<string, object?>?>? QuerySignedCart { get; set; }
    public static Func<List<int>, Dictionary<string, object?>?>? QueryGuestCart { get; set; }
    public static Func<string>? CurrencyShowMode { get; set; }
    public static Func<string>? LangHref { get; set; }
    public static Func<string, string>? TranslateById { get; set; }
    public static string? ProductsInCartCookie { get; set; }
    public static string LastOutput { get; private set; } = "";

    public static void Reset()
    {
        GetUserId = null;
        GetUserSession = null;
        QueryCurrency = null;
        QuerySignedCart = null;
        QueryGuestCart = null;
        CurrencyShowMode = null;
        LangHref = null;
        TranslateById = null;
        ProductsInCartCookie = null;
        LastOutput = "";
    }

    public static string EpcCartModule()
    {
        _ = GetUserSession?.Invoke();
        var currency = QueryCurrency?.Invoke() ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var mode = CurrencyShowMode?.Invoke() ?? "sign_after";
        var sign = Str(currency.GetValueOrDefault("sign"));
        var indicator = mode == "no"
            ? ""
            : mode is "sign_before" or "sign_after"
                ? sign
                : Str(currency.GetValueOrDefault("caption_short"));
        var userId = GetUserId?.Invoke() ?? 0;
        object? count = 0;
        object? sum = 0;
        if (userId > 0)
        {
            var rec = QuerySignedCart?.Invoke(userId);
            if (rec is not null)
            {
                count = rec.GetValueOrDefault("count") ?? 0;
                sum = rec.GetValueOrDefault("sum") ?? 0;
            }
        }
        else if (ProductsInCartCookie is not null)
        {
            var ids = ParseCookieIds(ProductsInCartCookie);
            var rec = QueryGuestCart?.Invoke(ids);
            if (rec is not null)
            {
                count = rec.GetValueOrDefault("count") ?? 0;
                sum = rec.GetValueOrDefault("sum") ?? 0;
            }
        }

        var formatted = NumberFormat2(sum);
        formatted = mode == "sign_before" ? indicator + " " + formatted : formatted + " " + indicator;
        var lang = LangHref?.Invoke() ?? "/en";
        LastOutput =
            "\n\n<div class=\"cart_module\" onclick=\"location = '" + lang + "/shop/cart';\">\n" +
            "\t<div class=\"cart_module_positions\" id=\"cart_module_positions\"><b>" + Tr("4495") + "</b> " + Str(count) + "</div>\n" +
            "\t<div class=\"cart_module_sum\" id=\"cart_module_sum\"><b>На сумму</b> " + formatted + "</div>\n" +
            "</div>";
        return LastOutput;
    }

    private static List<int> ParseCookieIds(string raw)
    {
        var ids = new List<int>();
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ids;
            }

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.TryGetInt32(out var n))
                {
                    ids.Add(n);
                }
            }
        }
        catch (JsonException)
        {
            // Match json_decode failure by leaving the list empty.
        }

        return ids;
    }

    private static string NumberFormat2(object? value)
    {
        try
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("0.00", CultureInfo.InvariantCulture);
        }
        catch
        {
            return "0.00";
        }
    }

    private static string Tr(string id)
        => TranslateById?.Invoke(id) ?? id;

    private static string Str(object? value)
        => value is null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
}
