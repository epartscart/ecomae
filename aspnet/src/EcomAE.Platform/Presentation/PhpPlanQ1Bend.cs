namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-bend CP order WhatsApp share panel. Path kept for the inventory:
/// <c>cp/content/shop/order_process/epc_order_whatsapp_share.php</c>.
/// GET never mints a session cookie. Leftover WhatsApp helper parents stay injected.
/// </summary>
public static class PhpPlanQ1Bend
{
    public const string OrderWhatsappSharePath = "cp/content/shop/order_process/epc_order_whatsapp_share.php";

    public static Func<int, List<Dictionary<string, object?>>>? OrderItems { get; set; }
    public static Func<string>? SalesDigits { get; set; }
    public static Func<string, string>? Digits { get; set; }
    public static Func<int, object?, List<Dictionary<string, object?>>, string>? CustomerMessage { get; set; }
    public static Func<int, object?, List<Dictionary<string, object?>>, string>? SalesMessage { get; set; }
    public static Func<string, string, string>? ShareUrl { get; set; }
    public static Func<int, List<Dictionary<string, object?>>, List<Dictionary<string, object?>>>? LpoGroups { get; set; }
    public static Func<string>? Styles { get; set; }
    public static Func<object?, string>? Escape { get; set; }
    public static Func<string>? SalesDisplay { get; set; }
    public static Func<string, string, string, string, string>? Button { get; set; }

    public static void Reset()
    {
        OrderItems = _ => [];
        SalesDigits = () => "";
        Digits = phone => new string((phone ?? "").Where(char.IsDigit).ToArray());
        CustomerMessage = (_, _, _) => "";
        SalesMessage = (_, _, _) => "";
        ShareUrl = (digits, text) => digits == "" ? "" : "https://wa.me/" + digits + "?t=" + text;
        LpoGroups = (_, _) => [];
        Styles = () => "<style>wa</style>";
        Escape = value => Convert.ToString(value) ?? "";
        SalesDisplay = () => "Sales line";
        Button = (href, label, extraClass, _) => href == "" ? "" : "<a class=\"" + extraClass + "\" href=\"" + href + "\">" + label + "</a>";
    }

    public static string EpcOrderWhatsappShareRenderHtml(
        object? order,
        object? orderId,
        IReadOnlyDictionary<string, object?>? customerProfile)
    {
        if (PhpEmpty(order) || PhpEmpty(orderId))
        {
            return "";
        }

        var id = ToInt(orderId);
        var items = OrderItems != null ? OrderItems(id) : [];
        var salesDigits = SalesDigits != null ? SalesDigits() : "";
        var customerPhone = "";
        if (customerProfile != null && customerProfile.TryGetValue("phone", out var profilePhone) && !PhpEmpty(profilePhone))
        {
            customerPhone = Digits != null ? Digits(Convert.ToString(profilePhone) ?? "") : "";
        }
        else if (order is IReadOnlyDictionary<string, object?> row
                 && row.TryGetValue("phone_not_auth", out var orderPhone)
                 && !PhpEmpty(orderPhone))
        {
            customerPhone = Digits != null ? Digits(Convert.ToString(orderPhone) ?? "") : "";
        }

        var customerMsg = CustomerMessage != null ? CustomerMessage(id, order, items) : "";
        var salesMsg = SalesMessage != null ? SalesMessage(id, order, items) : "";
        var customerHref = customerPhone != "" && ShareUrl != null ? ShareUrl(customerPhone, customerMsg) : "";
        var salesHref = ShareUrl != null ? ShareUrl(salesDigits, salesMsg) : "";
        var lpoGroups = LpoGroups != null ? LpoGroups(id, items) : [];

        // PHP `?>` eats the following newline; a tab before `<?php if` is always emitted.
        var html = (Styles != null ? Styles() : "")
            + "<div class=\"epc-wa-share-panel\">\n"
            + "\t<h5><i class=\"fa fa-whatsapp\"></i> WhatsApp share</h5>\n"
            + "\t<p class=\"text-muted\">Sales line: <strong>" + (Escape != null ? Escape(SalesDisplay != null ? SalesDisplay() : "") : "") + "</strong> — bilingual EN/AR messages.</p>\n"
            + "\t<div class=\"epc-wa-share-row\">\n"
            + "\t\t";
        if (customerHref != "")
        {
            html += Button != null ? Button(customerHref, "Message customer", "btn-success", "Send order summary to customer on WhatsApp") : "";
        }
        else
        {
            html += "<span class=\"text-muted\" style=\"font-size:12px;\">No customer phone — add phone on order or user profile.</span>";
        }

        html += Button != null ? Button(salesHref, "Share with sales", "btn-success", "Order summary to sales WhatsApp") : "";
        html += "\t</div>\n\t";
        if (lpoGroups.Count > 0)
        {
            html += "\t<div style=\"margin-top:10px;\">\n"
                + "\t\t<strong style=\"font-size:12px;color:#475569;\">Supplier LPO (share text):</strong>\n"
                + "\t\t<div class=\"epc-wa-share-row\">\n"
                + "\t\t\t";
            foreach (var g in lpoGroups)
            {
                var label = "LPO: " + Str(g, "storage_name");
                if (Str(g, "target_label") == "supplier")
                {
                    label += " → supplier";
                }

                html += Button != null ? Button(Str(g, "wa_href"), label, "btn-default", Str(g, "lpo_message")) : "";
            }

            html += "\t\t</div>\n"
                + "\t</div>\n"
                + "\t";
        }

        html += "</div>\n";
        return html;
    }

    private static bool PhpEmpty(object? value)
        => value is null or false or "" or 0 or 0L or 0d
            || (value is string s && (s == "" || s == "0"))
            || (value is System.Collections.IEnumerable e && value is not string && !e.Cast<object?>().Any());

    private static int ToInt(object? value)
        => int.TryParse(Convert.ToString(value), out var n) ? n : 0;

    private static string Str(IReadOnlyDictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var v) ? Convert.ToString(v) ?? "" : "";
}
