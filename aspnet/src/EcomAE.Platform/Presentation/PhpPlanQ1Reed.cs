using System.Globalization;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-reed storefront customer-balance module.
/// Path: <c>modules/shop/balance/module.php</c>.
/// GET never mints a session cookie. Leftover unique user helper stays injected.
/// </summary>
public static class PhpPlanQ1Reed
{
    public const string BalanceModulePath = "modules/shop/balance/module.php";

    public static Func<int>? GetUserId { get; set; }
    public static Func<Dictionary<string, object?>>? QueryCurrency { get; set; }
    public static Func<int, Dictionary<string, object?>?>? QueryBalance { get; set; }
    public static Func<string>? CurrencyShowMode { get; set; }
    public static Func<string, string>? TranslateById { get; set; }
    public static string LastOutput { get; private set; } = "";

    public static void Reset()
    {
        GetUserId = null;
        QueryCurrency = null;
        QueryBalance = null;
        CurrencyShowMode = null;
        TranslateById = null;
        LastOutput = "";
    }

    public static string EpcBalanceModule()
    {
        var currency = QueryCurrency?.Invoke() ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var mode = CurrencyShowMode?.Invoke() ?? "sign_after";
        var sign = Str(currency.GetValueOrDefault("sign"));
        var indicator = mode == "no"
            ? ""
            : mode is "sign_before" or "sign_after"
                ? sign
                : Str(currency.GetValueOrDefault("caption_short"));
        var userId = GetUserId?.Invoke() ?? 0;
        if (userId > 0)
        {
            var rec = QueryBalance?.Invoke(userId);
            object? balance = rec is null ? 0 : rec.GetValueOrDefault("balance");
            if (PhpEqEmpty(balance))
            {
                balance = 0;
            }

            var formatted = NumberFormat2(balance);
            if (mode == "sign_before")
            {
                formatted = indicator + " " + formatted;
            }
            else if (mode is "sign_after" or "short_name_after")
            {
                formatted = formatted + " " + indicator;
            }

            LastOutput =
                "    <div class=\"epc-balance-panel epc-balance-panel--active\">\n" +
                "        <div class=\"epc-balance-panel__icon\"><i class=\"fa fa-credit-card\" aria-hidden=\"true\"></i></div>\n" +
                "        <div class=\"epc-balance-panel__content\">\n" +
                "            <span class=\"epc-balance-panel__eyebrow\">Customer account</span>\n" +
                "            <h2>" + Tr("4655") + "</h2>\n" +
                "                    <div class=\"epc-balance-panel__amount\">" + formatted + "</div>\n" +
                "            <p>Your available account balance is shown here after login.</p>\n" +
                "        </div>\n" +
                "    </div>\n    ";
            return LastOutput;
        }

        LastOutput =
            "\t<div class=\"epc-balance-panel epc-balance-panel--login\">\n" +
            "\t\t<div class=\"epc-balance-panel__icon\"><i class=\"fa fa-lock\" aria-hidden=\"true\"></i></div>\n" +
            "\t\t<div class=\"epc-balance-panel__content\">\n" +
            "\t\t\t<span class=\"epc-balance-panel__eyebrow\">Secure customer area</span>\n" +
            "\t\t\t<h2>Login required to view balance</h2>\n" +
            "\t\t\t<p>Your balance is private account information. Please log in or register below to see your available credit, payments and account status.</p>\n" +
            "\t\t\t<div class=\"epc-balance-panel__chips\">\n" +
            "\t\t\t\t<span><i class=\"fa fa-shield\" aria-hidden=\"true\"></i> Protected account data</span>\n" +
            "\t\t\t\t<span><i class=\"fa fa-user\" aria-hidden=\"true\"></i> Customer only</span>\n" +
            "\t\t\t</div>\n" +
            "\t\t</div>\n" +
            "\t</div>\n\t";
        return LastOutput;
    }

    private static bool PhpEqEmpty(object? value)
    {
        if (value is null or false or "")
        {
            return true;
        }

        try
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture) == 0;
        }
        catch
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) == "";
        }
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
