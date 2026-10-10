using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-gaff WhatsApp share helpers. PHP identifiers kept for the inventory:
/// <c>epc_wa_h</c>, <c>epc_wa_digits</c>, <c>epc_wa_sales_digits</c>,
/// <c>epc_wa_sales_display</c>, <c>epc_wa_share_url</c>, <c>epc_wa_bilingual</c>,
/// <c>epc_wa_site_name</c>, <c>epc_wa_order_items</c>, <c>epc_wa_order_lines_text</c>,
/// <c>epc_wa_order_status_message</c>, <c>epc_wa_notify_order_status_change</c>,
/// <c>epc_wa_order_customer_message</c>, <c>epc_wa_order_sales_message</c>,
/// <c>epc_wa_supplier_lpo_message</c>, <c>epc_wa_product_message</c>,
/// <c>epc_wa_cart_message</c>, <c>epc_wa_supplier_phone_for_storage</c>,
/// <c>epc_wa_storage_name</c>, <c>epc_wa_order_lpo_groups</c>,
/// <c>epc_wa_button</c>, <c>epc_wa_styles</c>, <c>epc_wa_frontend_script</c>.
/// GET never mints a session cookie. Leftover agent / branding / supplier-notify
/// parents stay injected.
/// </summary>
public static class PhpPlanQ1Gaff
{
    public const string WhatsappSharePath = "content/general_pages/epc_whatsapp_share.php";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static Func<string>? AgentHref { get; set; }
    public static Func<string>? BrandName { get; set; }
    public static Func<int, List<Dictionary<string, object?>>>? OrderItems { get; set; }
    public static Func<int, string>? UserPhone { get; set; }
    public static Action<int, long, string>? InsertLog { get; set; }
    public static Func<int, string>? SupplierPhone { get; set; }
    public static Func<int, string>? StorageName { get; set; }
    public static Func<Dictionary<string, object?>, int>? ItemStorageId { get; set; }
    public static Func<long>? Clock { get; set; }

    public static void Reset()
    {
        AgentHref = () => "";
        BrandName = () => "";
        OrderItems = _ => [];
        UserPhone = _ => "";
        InsertLog = (_, _, _) => { };
        SupplierPhone = _ => "";
        StorageName = id => id <= 0 ? "Warehouse" : "Warehouse #" + id;
        ItemStorageId = item => ToInt(item, "t2_storage_id");
        Clock = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public static string EpcWaH(object? value)
        => H(Convert.ToString(value) ?? "");

    public static string EpcWaDigits(string? phone)
        => Regex.Replace(phone ?? "", "[^0-9]", "");

    public static string EpcWaSalesDigits(Dictionary<string, object?> config)
    {
        var href = AgentHref != null ? AgentHref() : "";
        if (href != "")
        {
            var m = Regex.Match(href, "/([0-9]+)$");
            if (m.Success)
            {
                return m.Groups[1].Value;
            }
        }

        return "971567607011";
    }

    public static string EpcWaSalesDisplay(Dictionary<string, object?> config)
    {
        var raw = Str(config, "epc_whatsapp_number");
        return !Empty(raw) ? raw.Trim() : "+971 56 760 7011";
    }

    public static string EpcWaShareUrl(string digits, string text)
    {
        digits = EpcWaDigits(digits);
        return digits == "" ? "" : "https://wa.me/" + digits + "?text=" + Uri.EscapeDataString(text);
    }

    public static string EpcWaBilingual(string en, string ar)
    {
        en = en.Trim();
        ar = ar.Trim();
        if (en == "")
        {
            return ar;
        }

        return ar == "" ? en : en + "\n" + ar;
    }

    public static string EpcWaSiteName(Dictionary<string, object?> config)
    {
        var name = BrandName != null ? BrandName() : "";
        if (name != "")
        {
            return name;
        }

        var from = Str(config, "from_name");
        return !Empty(from) ? from : "Store";
    }

    public static List<Dictionary<string, object?>> EpcWaOrderItems(int orderId)
        => OrderItems != null ? OrderItems(orderId) : [];

    public static string EpcWaOrderLinesText(IEnumerable<Dictionary<string, object?>> items, int max = 12)
    {
        var lines = new List<string>();
        foreach (var item in items)
        {
            if (lines.Count >= max)
            {
                lines.Add("…");
                break;
            }

            var brand = (Str(item, "t2_manufacturer") ?? "").Trim();
            var article = (item.TryGetValue("t2_article_show", out var show) && show != null
                ? Convert.ToString(show) : Str(item, "t2_article")) ?? "";
            article = article.Trim();
            var name = (Str(item, "t2_name") ?? "").Trim();
            var qty = ToInt(item, "count_need");
            var line = brand + " " + article;
            if (name != "")
            {
                line += " — " + name;
            }

            if (qty > 0)
            {
                line += " ×" + qty;
            }

            lines.Add(line.Trim());
        }

        return string.Join("\n", lines);
    }

    public static string EpcWaOrderStatusMessage(
        Dictionary<string, object?> config, int orderId, string statusName,
        Dictionary<string, object?>? order = null, IList<Dictionary<string, object?>>? items = null)
    {
        order ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        items ??= [];
        var site = EpcWaSiteName(config);
        var domain = Str(config, "domain_path").TrimEnd('/');
        var lines = EpcWaOrderLinesText(items, 8);
        var en = $"Hello from {site}.\n\nOrder #{orderId} update: {statusName}.";
        if (lines != "")
        {
            en += "\n\nItems:\n" + lines;
        }

        en += "\n\nTrack your order: " + domain + "/shop/orders";
        var ar = $"تحديث طلب #{orderId} من {site}: {statusName}.";
        if (lines != "")
        {
            ar += "\n\n" + lines;
        }

        ar += "\n\n" + domain + "/shop/orders";
        return EpcWaBilingual(en, ar);
    }

    public static void EpcWaNotifyOrderStatusChange(
        Dictionary<string, object?> config, int orderId, string statusName,
        Dictionary<string, object?>? order = null)
    {
        if (orderId <= 0 || statusName == "")
        {
            return;
        }

        order ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        var items = EpcWaOrderItems(orderId);
        var msg = EpcWaOrderStatusMessage(config, orderId, statusName, order, items);
        var phone = "";
        if (!Empty(Str(order, "phone_not_auth")))
        {
            phone = EpcWaDigits(Str(order, "phone_not_auth"));
        }
        else if (!Empty(Str(order, "user_id")) && ToInt(order, "user_id") > 0)
        {
            try
            {
                phone = EpcWaDigits(UserPhone != null ? UserPhone(ToInt(order, "user_id")) : "");
            }
            catch
            {
                phone = "";
            }
        }

        var target = phone != "" ? phone : EpcWaSalesDigits(config);
        var href = EpcWaShareUrl(target, msg);
        var log = "WhatsApp tracking template ready: " + statusName;
        if (href != "")
        {
            log += " — " + href;
        }

        try
        {
            InsertLog?.Invoke(orderId, Clock != null ? Clock() : 0, log);
        }
        catch
        {
            // PHP swallows log insert failures.
        }
    }

    public static string EpcWaOrderCustomerMessage(
        Dictionary<string, object?> config, int orderId,
        Dictionary<string, object?> order, IList<Dictionary<string, object?>> items)
    {
        var site = EpcWaSiteName(config);
        var sum = order.ContainsKey("price_sum")
            ? NumberFormat(Convert.ToDouble(order["price_sum"], CultureInfo.InvariantCulture))
            : "";

        var lines = EpcWaOrderLinesText(items);
        var en = $"Hello from {site}.\n\nYour order #{orderId}";
        if (sum != "")
        {
            en += " — total " + sum + " AED";
        }

        en += ".\n\nItems:\n" + lines + "\n\nReply here if you have questions.";
        var ar = $"مرحباً من {site}.\n\nطلبكم رقم #{orderId}";
        if (sum != "")
        {
            ar += " — الإجمالي " + sum + " درهم";
        }

        ar += ".\n\nالأصناف:\n" + lines + "\n\nردّوا على هذه الرسالة لأي استفسار.";
        return EpcWaBilingual(en, ar);
    }

    public static string EpcWaOrderSalesMessage(
        Dictionary<string, object?> config, int orderId,
        Dictionary<string, object?> order, IList<Dictionary<string, object?>> items, string context = "staff")
    {
        var site = EpcWaSiteName(config);
        var sum = order.ContainsKey("price_sum")
            ? NumberFormat(Convert.ToDouble(order["price_sum"], CultureInfo.InvariantCulture))
            : "";
        var lines = EpcWaOrderLinesText(items);
        var domain = Str(config, "domain_path").TrimEnd('/');
        var link = domain + "/" + Str(config, "backend_dir") + "/shop/orders/order?order_id=" + orderId;
        var en = $"[{site}] Order #{orderId} ({context})";
        if (sum != "")
        {
            en += " — " + sum + " AED";
        }

        en += "\n\n" + lines + "\n\nCP: " + link;
        var ar = $"[{site}] طلب #{orderId}\n\n{lines}";
        return EpcWaBilingual(en, ar);
    }

    public static string EpcWaSupplierLpoMessage(
        Dictionary<string, object?> config, int orderId, string storageName,
        IEnumerable<Dictionary<string, object?>> items)
    {
        var site = EpcWaSiteName(config);
        var lines = new List<string>();
        foreach (var item in items)
        {
            var qty = ToInt(item, "count_need");
            if (qty <= 0)
            {
                continue;
            }

            var article = item.TryGetValue("t2_article_show", out var show) && show != null
                ? Convert.ToString(show) ?? ""
                : Str(item, "t2_article");
            lines.Add((Str(item, "t2_manufacturer") + " " + article + " ×" + qty).Trim());
        }

        var body = string.Join("\n", lines);
        var en = $"LPO #{orderId} — {storageName}\n\nPlease supply:\n{body}\n\nReference LPO {orderId} on invoice.\n— {site}";
        var ar = $"أمر شراء #{orderId} — {storageName}\n\n{body}\n\nرقم LPO: {orderId}";
        return EpcWaBilingual(en, ar);
    }

    public static string EpcWaProductMessage(
        Dictionary<string, object?> config, string brand, string article, string name, object? price = null)
    {
        var site = EpcWaSiteName(config);
        var domain = Str(config, "domain_path").TrimEnd('/');
        var en = $"Hello {site}, please quote:\n\n{brand} {article}";
        if (name != "")
        {
            en += "\n" + name;
        }

        if (price != null && Convert.ToString(price) != "")
        {
            en += "\nListed: " + price + " AED";
        }

        en += "\n\n" + domain;
        var ar = $"السلام عليكم {site}، أرجو تسعير:\n\n{brand} {article}";
        if (name != "")
        {
            ar += "\n" + name;
        }

        return EpcWaBilingual(en, ar);
    }

    public static string EpcWaCartMessage(Dictionary<string, object?> config, IList<string> lines, object? total = null)
    {
        var site = EpcWaSiteName(config);
        var domain = Str(config, "domain_path").TrimEnd('/');
        var body = string.Join("\n", lines.Take(15));
        var en = $"Hello {site}, please assist with my cart:\n\n{body}";
        if (total != null && Convert.ToString(total) != "")
        {
            en += "\n\nEstimated total: " + total + " AED";
        }

        en += "\n\n" + domain + "/shop/cart";
        var ar = $"مرحباً {site}، أرجو المساعدة في سلة التسوق:\n\n{body}";
        return EpcWaBilingual(en, ar);
    }

    public static string EpcWaSupplierPhoneForStorage(int storageId)
    {
        if (storageId <= 0)
        {
            return "";
        }

        try
        {
            return EpcWaDigits(SupplierPhone != null ? SupplierPhone(storageId) : "");
        }
        catch
        {
            return "";
        }
    }

    public static string EpcWaStorageName(int storageId)
    {
        if (storageId <= 0)
        {
            return "Warehouse";
        }

        var name = (StorageName != null ? StorageName(storageId) : "").Trim();
        return name != "" ? name : "Warehouse #" + storageId;
    }

    public static List<Dictionary<string, object?>> EpcWaOrderLpoGroups(
        Dictionary<string, object?> config, int orderId, IEnumerable<Dictionary<string, object?>> items)
    {
        var groups = new Dictionary<int, Dictionary<string, object?>>();
        foreach (var item in items)
        {
            var sid = ItemStorageId != null ? ItemStorageId(item) : ToInt(item, "t2_storage_id");
            if (!groups.TryGetValue(sid, out var group))
            {
                group = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["storage_id"] = sid,
                    ["storage_name"] = EpcWaStorageName(sid),
                    ["items"] = new List<Dictionary<string, object?>>(),
                    ["supplier_phone"] = EpcWaSupplierPhoneForStorage(sid)
                };
                groups[sid] = group;
            }

            ((List<Dictionary<string, object?>>)group["items"]!).Add(item);
        }

        var sales = EpcWaSalesDigits(config);
        var output = new List<Dictionary<string, object?>>();
        foreach (var g in groups.Values)
        {
            var msg = EpcWaSupplierLpoMessage(config, orderId, Str(g, "storage_name"), (List<Dictionary<string, object?>>)g["items"]!);
            var phone = Str(g, "supplier_phone");
            var target = phone != "" ? phone : sales;
            g["lpo_message"] = msg;
            g["wa_href"] = EpcWaShareUrl(target, msg);
            g["target_label"] = phone != "" ? "supplier" : "sales (forward LPO)";
            output.Add(g);
        }

        return output;
    }

    public static string EpcWaButton(string href, string label, string extraClass = "", string title = "")
    {
        if (href == "")
        {
            return "";
        }

        var cls = "btn btn-sm epc-wa-share-btn " + extraClass.Trim();
        var t = title != "" ? " title=\"" + H(title) + "\"" : "";
        return "<a class=\"" + H(cls) + "\" href=\"" + H(href) + "\" target=\"_blank\" rel=\"noopener noreferrer\"" + t + ">"
            + "<i class=\"fa fa-whatsapp\"></i> " + H(label) + "</a>";
    }

    public static string EpcWaStyles()
        => "<style>"
            + ".epc-wa-share-btn{background:#25D366!important;border-color:#1da851!important;color:#fff!important;margin:2px 4px 2px 0;}"
            + ".epc-wa-share-btn:hover,.epc-wa-share-btn:focus{background:#1da851!important;color:#fff!important;}"
            + ".epc-wa-share-row{margin:8px 0 0;display:flex;flex-wrap:wrap;gap:6px;align-items:center;}"
            + ".epc-wa-share-panel{background:#f0fdf4;border:1px solid #bbf7d0;border-radius:8px;padding:12px 14px;margin:12px 0;}"
            + ".epc-wa-share-panel h5{margin:0 0 8px;font-size:14px;font-weight:700;color:#166534;}"
            + ".epc-wa-share-panel .text-muted{font-size:12px;margin-bottom:8px;}"
            + ".epc-product-actions .epc-wa-share-btn{padding:6px 10px;font-size:11px;text-decoration:none!important;display:inline-flex;align-items:center;gap:4px;white-space:nowrap;}"
            + ".epc-product-actions{display:inline-flex;flex-direction:row;flex-wrap:nowrap;gap:6px;align-items:center;justify-content:flex-start;}"
            + ".epc-product-actions__tools,.epc-product-actions__buy{display:inline-flex;flex-wrap:nowrap;gap:6px;align-items:center;justify-content:flex-start;width:auto;flex:0 0 auto;}"
            + ".epc-product-actions__tools .epc-wa-share-btn,.epc-product-actions__tools .epc-fitment-check-btn--row{flex:0 0 auto;}"
            + "#all_table_products .td_add_to_cart .epc-product-actions{flex-wrap:nowrap;justify-content:flex-start;overflow:visible;}"
            + "#all_table_products .td_price .epc-price-value{display:block;font-weight:700;white-space:nowrap;line-height:1.3;}"
            + "</style>";

    public static string EpcWaFrontendScript(Dictionary<string, object?> config)
    {
        var sales = PhpJson(EpcWaSalesDigits(config));
        var display = PhpJson(EpcWaSalesDisplay(config));
        var site = PhpJson(EpcWaSiteName(config));
        var domain = PhpJson(Str(config, "domain_path").TrimEnd('/'));
        return "<script>\nwindow.epcWaShare = {\n"
            + "\tsales: " + sales + ",\n"
            + "\tsalesDisplay: " + display + ",\n"
            + "\tsite: " + site + ",\n"
            + "\tdomain: " + domain + "};\n"
            + "function epcWaOpen(digits, text) {\n"
            + "\tdigits = String(digits || '').replace(/\\D/g, '');\n"
            + "\tif (!digits) { return; }\n"
            + "\twindow.open('https://wa.me/' + digits + '?text=' + encodeURIComponent(text || ''), '_blank', 'noopener,noreferrer');\n"
            + "}\n"
            + "function epcWaBilingual(en, ar) {\n"
            + "\ten = (en || '').trim();\n"
            + "\tar = (ar || '').trim();\n"
            + "\tif (!en) { return ar; }\n"
            + "\tif (!ar) { return en; }\n"
            + "\treturn en + '\\n' + ar;\n"
            + "}\n"
            + "function epcWaShareProductHref(brand, article, name, price) {\n"
            + "\tvar s = window.epcWaShare || {};\n"
            + "\tvar digits = String(s.sales || '').replace(/\\D/g, '');\n"
            + "\tif (!digits) { return ''; }\n"
            + "\tvar en = 'Hello ' + (s.site || 'eParts Cart') + ', please quote:\\n\\n' + brand + ' ' + article;\n"
            + "\tif (name) { en += '\\n' + name; }\n"
            + "\tif (price !== undefined && price !== null && price !== '') { en += '\\nListed: ' + price + ' AED'; }\n"
            + "\ten += '\\n\\n' + (s.domain || '');\n"
            + "\tvar ar = 'السلام عليكم، أرجو تسعير:\\n\\n' + brand + ' ' + article;\n"
            + "\tif (name) { ar += '\\n' + name; }\n"
            + "\treturn 'https://wa.me/' + digits + '?text=' + encodeURIComponent(epcWaBilingual(en, ar));\n"
            + "}\n"
            + "function epcWaShareProduct(brand, article, name, price) {\n"
            + "\tvar href = epcWaShareProductHref(brand, article, name, price);\n"
            + "\tif (href) { window.open(href, '_blank', 'noopener,noreferrer'); }\n"
            + "}\n"
            + "function epcWaShareCart() {\n"
            + "\tvar s = window.epcWaShare || {};\n"
            + "\tif (typeof cart_records === 'undefined' || !cart_records || !cart_records.length) { return; }\n"
            + "\tvar lines = [];\n"
            + "\tvar total = 0;\n"
            + "\tfor (var i = 0; i < cart_records.length && lines.length < 15; i++) {\n"
            + "\t\tvar r = cart_records[i];\n"
            + "\t\tvar line = (r.manufacturer || '') + ' ' + (r.article || '');\n"
            + "\t\tif (r.name) { line += ' — ' + r.name; }\n"
            + "\t\tif (r.count_need) { line += ' ×' + r.count_need; }\n"
            + "\t\tlines.push(line.trim());\n"
            + "\t\tif (r.price && r.count_need) { total += parseFloat(r.price) * parseInt(r.count_need, 10); }\n"
            + "\t}\n"
            + "\tvar en = 'Hello ' + (s.site || 'eParts Cart') + ', please assist with my cart:\\n\\n' + lines.join('\\n');\n"
            + "\tif (total > 0) { en += '\\n\\nEstimated total: ' + total.toFixed(2) + ' AED'; }\n"
            + "\ten += '\\n\\n' + (s.domain || '') + '/shop/cart';\n"
            + "\tvar ar = 'مرحباً، أرجو المساعدة في سلة التسوق:\\n\\n' + lines.join('\\n');\n"
            + "\tepcWaOpen(s.sales, epcWaBilingual(en, ar));\n"
            + "}\n"
            + "function epcWaShareBtnHTML(brand, article, name, price) {\n"
            + "\tif (typeof epc_storefront_prices_visible !== 'undefined' && !epc_storefront_prices_visible) {\n"
            + "\t\treturn '';\n"
            + "\t}\n"
            + "\tvar href = epcWaShareProductHref(brand, article, name, price);\n"
            + "\tif (!href) { return ''; }\n"
            + "\tvar disp = (window.epcWaShare && window.epcWaShare.salesDisplay) ? window.epcWaShare.salesDisplay : '';\n"
            + "\tvar safeHref = href.replace(/&/g, '&amp;').replace(/\"/g, '&quot;');\n"
            + "\tvar safeDisp = String(disp).replace(/\"/g, '&quot;');\n"
            + "\treturn '<a class=\"btn btn-sm epc-wa-share-btn\" href=\"' + safeHref + '\" target=\"_blank\" rel=\"noopener noreferrer\" title=\"WhatsApp ' + safeDisp + '\">'\n"
            + "\t\t+ '<i class=\"fa fa-whatsapp\"></i> WhatsApp</a>';\n"
            + "}\n"
            + "</script>\n\t";
    }

    private static string NumberFormat(double value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("#,##0.00", CultureInfo.InvariantCulture);

    private static string PhpJson(string value)
        => JsonSerializer.Serialize(value, JsonOpts).Replace("/", "\\/", StringComparison.Ordinal);

    private static bool Empty(string? value)
        => string.IsNullOrEmpty(value) || value == "0";

    private static string Str(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var raw) && raw != null ? Convert.ToString(raw) ?? "" : "";

    private static int ToInt(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var raw) || raw == null)
        {
            return 0;
        }

        var text = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "";
        var i = 0;
        while (i < text.Length && (char.IsDigit(text[i]) || (i == 0 && text[i] == '-')))
        {
            i++;
        }

        return i == 0 ? 0 : int.TryParse(text[..i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    private static string H(string value)
        => value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
}
