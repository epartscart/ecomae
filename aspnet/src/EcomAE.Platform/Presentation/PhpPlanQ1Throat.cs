using System.Globalization;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-throat CP order staff summary. Path kept for the inventory:
/// <c>cp/content/shop/order_process/epc_order_staff_summary.php</c>.
/// GET never mints a session cookie. Leftover notify/currency parents stay injected.
/// </summary>
public static class PhpPlanQ1Throat
{
    public const string OrderStaffSummaryPath = "cp/content/shop/order_process/epc_order_staff_summary.php";

    public static Func<int, int>? CrmUserId { get; set; }
    public static Func<int, object?, string>? ProfileHtml { get; set; }

    public static void Reset()
    {
        CrmUserId = _ => 0;
        ProfileHtml = (_, _) => "";
    }

    public static string EpcOrderStaffSummaryRenderHtml(
        object? order,
        object? orderId,
        object? customerId,
        object? sale,
        object? purchase,
        object? profit,
        object? margin)
    {
        if (PhpEmpty(order) || PhpEmpty(orderId))
        {
            return "";
        }

        var customer = ToInt(customerId);
        var crmId = CrmUserId != null ? CrmUserId(customer) : 0;
        var profile = ProfileHtml != null ? ProfileHtml(customer, order) : "";
        var saleN = ToDouble(sale);
        var html = "<style>\n"
            + ".epc-order-staff { margin-bottom: 18px; padding: 14px 16px; background: #f8fafc; border: 1px solid #dce4ef; border-radius: 8px; }\n"
            + ".epc-order-staff h4 { margin: 0 0 10px; font-size: 15px; font-weight: 700; color: #172536; }\n"
            + ".epc-order-staff table.epc-kv td { padding: 4px 10px 4px 0; font-size: 13px; vertical-align: top; }\n"
            + ".epc-order-staff table.epc-kv td:first-child { font-weight: 700; width: 160px; color: #475569; }\n"
            + ".epc-order-staff .epc-totals { margin-top: 12px; text-align: right; font-size: 13px; }\n"
            + ".epc-order-staff .epc-totals div { margin: 4px 0; }\n"
            + "</style>\n"
            + "<div class=\"epc-order-staff\">\n"
            + "\t<h4>Order intelligence (staff)</h4>\n"
            + "\t" + profile;
        if (crmId > 0)
        {
            html += "<p style=\"font-size:12px;color:#64748b;margin:8px 0 0;\">Relationship manager user ID: <strong>" + crmId + "</strong></p>";
        }

        html += "\t<div class=\"epc-totals\">\n"
            + "\t\t<div>Sale (ex VAT): <strong>" + NumberFormat(saleN, 2, ".", ",") + " AED</strong></div>\n"
            + "\t\t<div>Purchase: <strong>" + NumberFormat(ToDouble(purchase), 2, ".", ",") + " AED</strong></div>\n"
            + "\t\t<div>Margin: <strong style=\"color:#166534;\">" + NumberFormat(ToDouble(profit), 2, ".", ",") + " AED</strong> (" + NumberFormat(ToDouble(margin), 2) + "%)</div>\n"
            + "\t\t<div>VAT 5% on sale: <strong>" + NumberFormat(saleN * 0.05, 2, ".", ",") + " AED</strong></div>\n"
            + "\t</div>\n"
            + "</div>\n";
        return html;
    }

    private static bool PhpEmpty(object? value)
        => value is null or false or "" or 0 or 0L or 0d
            || (value is string s && (s == "" || s == "0"))
            || (value is System.Collections.IEnumerable e && value is not string && !e.Cast<object?>().Any());

    private static int ToInt(object? value)
        => int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static double ToDouble(object? value)
        => double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static string NumberFormat(double value, int decimals, string decPoint = ".", string thousands = ",")
    {
        var rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        var raw = rounded.ToString("F" + decimals, CultureInfo.InvariantCulture);
        var parts = raw.Split('.');
        var whole = parts[0];
        var sign = "";
        if (whole.StartsWith('-'))
        {
            sign = "-";
            whole = whole[1..];
        }

        var grouped = "";
        for (var i = 0; i < whole.Length; i++)
        {
            if (i > 0 && (whole.Length - i) % 3 == 0)
            {
                grouped += thousands;
            }

            grouped += whole[i];
        }

        return sign + grouped + (parts.Length > 1 ? decPoint + parts[1] : "");
    }
}
