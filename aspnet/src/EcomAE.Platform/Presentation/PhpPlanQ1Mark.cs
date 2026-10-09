using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-mark helper. PHP identifiers kept for the inventory:
/// <c>printProductBlock</c> from <c>content/shop/catalogue/helper.php</c>.
/// </summary>
public static class PhpPlanQ1Mark
{
    public const string CatalogueHelperPath = "content/shop/catalogue/helper.php";

    private const string Nl = "\r\n";
    private const string ImageFolder = "/content/files/images/products_images/";

    public static string PrintProductBlock(
        JsonElement product,
        string langHref,
        bool mainFlag,
        bool admin,
        IReadOnlyDictionary<string, string>? cookies = null,
        string domain = "https://shop.example/",
        string backend = "cp")
    {
        cookies ??= new Dictionary<string, string>(StringComparer.Ordinal);
        var cls = S(Get(product, "main_class_of_block"));
        var ptype = ToInt(Get(product, "product_block_type"));
        if (ptype is 2 or 3)
        {
            cls += " backend_product_box";
        }

        var caption = Tr(Get(product, "caption"));
        var pid = Get(product, "id");
        var url = S(Get(product, "product_url"));
        var target = mainFlag ? "" : "target=\"_blank\"";
        var href = langHref + url;
        var o = new StringBuilder();

        o.Append("\t<div class=\"" + cls + "\">" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t");
        var showImg = cls.Contains("product_div_tile", StringComparison.Ordinal)
            || cls.Contains("product_div_list_photo", StringComparison.Ordinal);
        if (showImg)
        {
            o.Append("\t\t\t<div class=\"product_div_image_wrap\">" + Nl);
            o.Append("\t\t\t\t<a title=\"" + caption + "\" href=\"" + href + "\" " + target + ">" + Nl);
            o.Append("\t\t\t\t\t");
            var hasImageKey = Has(product, "image");
            var image = hasImageKey ? Get(product, "image") : default;
            var hasImg = hasImageKey && !PhpEmpty(image) && S(image) != ImageFolder;
            if (hasImg)
            {
                o.Append("\t\t\t\t\t\t<img src=\"" + S(image) + "\" alt=\"" + caption + "\" onerror=\"this.src='/content/files/images/no_image.png'\" border=\"0\"  />" + Nl);
                o.Append("\t\t\t\t\t");
            }
            else
            {
                o.Append("\t\t\t\t\t\t<img src=\"/content/files/images/no_image.png\" alt=\"" + caption + "\" border=\"0\" />" + Nl);
                o.Append("\t\t\t\t\t");
            }

            o.Append("\t\t\t\t</a>" + Nl);
            o.Append("\t\t\t</div>" + Nl);
            o.Append("\t\t\t");
        }

        o.Append("\t\t" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t<div class=\"product_div_name\">" + Nl);
        o.Append("\t\t\t<a title=\"" + caption + "\" href=\"" + href + "\" " + target + ">" + Nl);
        o.Append("\t\t\t\t<div class=\"product_div_manufacturer\">" + Nl);
        o.Append("\t\t\t\t\t<span>" + Tr(Get(product, "manufacturer")) + "</span> <span>" + Tr(Get(product, "article")) + "</span>" + Nl);
        o.Append("\t\t\t\t</div>" + Nl);
        o.Append("\t\t\t\t<div class=\"product_div_caption\">" + Nl);
        o.Append("\t\t\t\t\t" + caption + "\t\t\t\t</div>" + Nl);
        o.Append("\t\t\t</a>" + Nl);
        o.Append("\t\t</div>" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t");
        o.Append("\t\t<div class=\"stickers\">" + Nl);
        o.Append("\t\t");
        if (Has(product, "stickers"))
        {
            var stickers = Get(product, "stickers");
            if (stickers.ValueKind == JsonValueKind.Object)
            {
                foreach (var stickerProp in stickers.EnumerateObject())
                {
                    AppendSticker(o, stickerProp.Value, langHref);
                }
            }
            else if (stickers.ValueKind == JsonValueKind.Array)
            {
                foreach (var sticker in stickers.EnumerateArray())
                {
                    AppendSticker(o, sticker, langHref);
                }
            }
        }

        o.Append("\t\t</div>" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t");
        o.Append("\t\t<div title=\"" + Tr(4101) + ": " + S(Get(product, "marks_count")) + "\" class=\"product_div_marks\" rel=\"popover\" data-html=\"true\" data-toggle=\"popover\" data-placement=\"bottom\" data-content=\"<div><i class='fa fa-star em-primary'></i> <i class='fa fa-star-o em-primary'></i> <i class='fa fa-star-o em-primary'></i> <i class='fa fa-star-o em-primary'></i> <i class='fa fa-star-o em-primary'></i> " + S(Get(product, "mark_1")) + "</div> <div><i class='fa fa-star em-primary'></i> <i class='fa fa-star em-primary'></i> <i class='fa fa-star-o em-primary'></i> <i class='fa fa-star-o em-primary'></i> <i class='fa fa-star-o em-primary'></i>  " + S(Get(product, "mark_2")) + "</div> <div><i class='fa fa-star em-primary'></i> <i class='fa fa-star em-primary'></i> <i class='fa fa-star em-primary'></i> <i class='fa fa-star-o em-primary'></i> <i class='fa fa-star-o em-primary'></i>  " + S(Get(product, "mark_3")) + "</div> <div><i class='fa fa-star em-primary'></i> <i class='fa fa-star em-primary'></i> <i class='fa fa-star em-primary'></i> <i class='fa fa-star em-primary'></i> <i class='fa fa-star-o em-primary'></i>  " + S(Get(product, "mark_4")) + "</div> <div><i class='fa fa-star em-primary'></i> <i class='fa fa-star em-primary'></i> <i class='fa fa-star em-primary'></i> <i class='fa fa-star em-primary'></i> <i class='fa fa-star em-primary'></i>  " + S(Get(product, "mark_5")) + "</div>\">" + Nl);
        o.Append("\t\t\t");
        var mark = ToInt(Get(product, "mark"));
        for (var i = 0; i < 5; i++)
        {
            if (i + 1 <= mark)
            {
                o.Append("\t\t\t\t\t<i class=\"fa fa-star em-primary\"></i>" + Nl);
                o.Append("\t\t\t\t\t");
            }
            else
            {
                o.Append("\t\t\t\t\t<i class=\"fa fa-star-o em-primary\"></i>" + Nl);
                o.Append("\t\t\t\t\t");
            }
        }

        o.Append("\t\t\t<span class=\"product_div_marks_count hidden\">" + S(Get(product, "marks_count")) + "</span>" + Nl);
        o.Append("\t\t</div>" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t\t" + Nl);
        o.Append("\t\t\t" + Nl);
        o.Append("            " + Nl);
        o.Append("\t\t\t" + Nl);
        o.Append("\t\t\t" + Nl);
        o.Append("\t\t\t" + Nl);
        o.Append("\t\t\t");
        var inBm = InList(Cookie(cookies, "bookmarks"), pid);
        if (inBm)
        {
            if (ptype == 6)
            {
                o.Append("\t\t\t\t\t<div class=\"product_div_bookmark\">" + Nl);
                o.Append("\t\t\t\t\t\t<a href=\"javascript:void(0);\" onclick=\"removeBookmark(" + S(pid) + ", this);\" title=\"" + Tr(4102) + "\">" + Nl);
                o.Append("\t\t\t\t\t\t\t<i class=\"fa fa-remove\"></i>" + Nl);
                o.Append("\t\t\t\t\t\t\t<span>" + Nl);
                o.Append("\t\t\t\t\t\t\t\t" + Tr(2224) + "\t\t\t\t\t\t\t</span>" + Nl);
                o.Append("\t\t\t\t\t\t</a>" + Nl);
                o.Append("\t\t\t\t\t</div>" + Nl);
                o.Append("\t\t\t\t\t");
            }
            else
            {
                o.Append("\t\t\t\t\t<div class=\"product_div_bookmark\">" + Nl);
                o.Append("\t\t\t\t\t\t<a href=\"javascript:void(0);\" onclick=\"location = '/shop/zakladki';\" title=\"" + Tr(4103) + "\">" + Nl);
                o.Append("\t\t\t\t\t\t\t<i class=\"fa fa-bookmark\"></i>" + Nl);
                o.Append("\t\t\t\t\t\t\t<span>" + Nl);
                o.Append("\t\t\t\t\t\t\t\t" + Tr(4104) + "\t\t\t\t\t\t\t</span>" + Nl);
                o.Append("\t\t\t\t\t\t</a>" + Nl);
                o.Append("\t\t\t\t\t</div>" + Nl);
                o.Append("\t\t\t\t\t");
            }
        }
        else
        {
            o.Append("\t\t\t\t<div class=\"product_div_bookmark\">" + Nl);
            o.Append("\t\t\t\t\t<a href=\"javascript:void(0);\" onclick=\"addToBookmarks(" + S(pid) + ", this);\" title=\"" + Tr(4105) + "\">" + Nl);
            o.Append("\t\t\t\t\t\t<i class=\"fa fa-bookmark-o\"></i>" + Nl);
            o.Append("\t\t\t\t\t\t<span>" + Nl);
            o.Append("\t\t\t\t\t\t\t" + Tr(4106) + "\t\t\t\t\t\t</span>" + Nl);
            o.Append("\t\t\t\t\t</a>" + Nl);
            o.Append("\t\t\t\t</div>" + Nl);
            o.Append("\t\t\t\t");
        }

        o.Append("\t\t\t" + Nl);
        o.Append("\t\t\t" + Nl);
        o.Append("\t\t\t" + Nl);
        o.Append("\t\t\t" + Nl);
        o.Append("\t\t\t" + Nl);
        o.Append("\t\t\t");
        var inCmp = InList(Cookie(cookies, "compare"), pid);
        if (inCmp)
        {
            if (ptype == 7)
            {
                o.Append("\t\t\t\t\t<div class=\"product_div_compare\">" + Nl);
                o.Append("\t\t\t\t\t\t<a href=\"javascript:void(0);\" onclick=\"removeCompare(" + S(pid) + ", this);\" title=\"" + Tr(4107) + "\">" + Nl);
                o.Append("\t\t\t\t\t\t\t<i class=\"fa fa-remove\"></i>" + Nl);
                o.Append("\t\t\t\t\t\t\t<span>" + Nl);
                o.Append("\t\t\t\t\t\t\t\t" + Tr(2224) + "\t\t\t\t\t\t\t</span>" + Nl);
                o.Append("\t\t\t\t\t\t</a>" + Nl);
                o.Append("\t\t\t\t\t</div>" + Nl);
                o.Append("\t\t\t\t\t");
            }
            else
            {
                o.Append("\t\t\t\t\t<div class=\"product_div_compare\">" + Nl);
                o.Append("\t\t\t\t\t\t<a href=\"javascript:void(0);\" onclick=\"location = '/shop/sravneniya'; \" title=\"" + Tr(4108) + "\">" + Nl);
                o.Append("\t\t\t\t\t\t\t<i class=\"glyphicon glyphicon-duplicate\"></i>" + Nl);
                o.Append("\t\t\t\t\t\t\t<span>" + Nl);
                o.Append("\t\t\t\t\t\t\t\t" + Tr(4109) + "\t\t\t\t\t\t\t</span>" + Nl);
                o.Append("\t\t\t\t\t\t</a>" + Nl);
                o.Append("\t\t\t\t\t</div>" + Nl);
                o.Append("\t\t\t\t\t");
            }
        }
        else
        {
            o.Append("\t\t\t\t<div class=\"product_div_compare\">" + Nl);
            o.Append("\t\t\t\t\t<a href=\"javascript:void(0);\" onclick=\"addToCompare(" + S(pid) + ", this);\" title=\"" + Tr(4110) + "\">" + Nl);
            o.Append("\t\t\t\t\t\t<i class=\"fa fa-copy fa-flip-horizontal\"></i>" + Nl);
            o.Append("\t\t\t\t\t\t<span>" + Nl);
            o.Append("\t\t\t\t\t\t\t" + Tr(4111) + "\t\t\t\t\t\t</span>" + Nl);
            o.Append("\t\t\t\t\t</a>" + Nl);
            o.Append("\t\t\t\t</div>" + Nl);
            o.Append("\t\t\t\t");
        }

        o.Append("\t\t\t" + Nl);
        o.Append("\t\t\t" + Nl);
        o.Append("\t\t\t" + Nl);
        o.Append("\t\t\t");
        if (admin)
        {
            o.Append("\t\t\t\t<div class=\"product_div_admin\">" + Nl);
            o.Append("\t\t\t\t\t<a href=\"" + domain + backend + "/shop/catalogue/products/product?category_id=" + S(Get(product, "category_id")) + "&product_id=" + S(pid) + "\" target=\"_blank\"><i class=\"fa fa-external-link\"></i></a>" + Nl);
            o.Append("\t\t\t\t</div>" + Nl);
            o.Append("\t\t\t\t");
        }

        o.Append("\t\t\t" + Nl);
        o.Append("\t\t\t" + Nl);
        o.Append("\t\t\t" + Nl);
        o.Append("\t\t\t" + Nl);
        o.Append("\t\t\t");
        if (ptype is 1 or 4 or 5 or 6 or 7)
        {
            if (Has(product, "exist_info_variant"))
            {
                o.Append("\t\t\t\t<div class=\"product_div_exist_info\">" + S(Get(product, "exist_info_variant")) + "</div>" + Nl);
                o.Append("\t\t\t\t");
            }

            if (Has(product, "price") && S(Get(product, "price")) != "")
            {
                o.Append("\t\t\t\t\t<div class=\"product_div_price\">" + S(Get(product, "price")) + "</div>" + Nl);
                o.Append("\t\t\t\t\t");
            }

            if (Has(product, "price_crossed_out") && S(Get(product, "price_crossed_out")) != "")
            {
                o.Append("\t\t\t\t\t<div class=\"product_div_price_crossed_out\">" + S(Get(product, "price_crossed_out")) + "</div>" + Nl);
                o.Append("\t\t\t\t\t");
            }

            if (Has(product, "price_from_to") && S(Get(product, "price_from_to")) != "")
            {
                o.Append("\t\t\t\t\t<div class=\"product_div_price_from_to\">" + S(Get(product, "price_from_to")) + "</div>" + Nl);
                o.Append("\t\t\t\t\t");
            }

            o.Append("\t\t\t" + Nl);
            o.Append("\t\t\t" + Nl);
            o.Append("\t\t\t" + Nl);
            o.Append("\t\t\t" + Nl);
            o.Append("\t\t\t");
            if (Has(product, "cart_suggestion"))
            {
                var sugKey = S(Get(product, "cart_suggestion"));
                if (Has(product, "storage_data"))
                {
                    var storage = Get(product, "storage_data");
                    if (storage.ValueKind == JsonValueKind.Object && storage.TryGetProperty(sugKey, out var sug))
                    {
                        o.Append("\t\t\t\t\t<div id=\"product_object_" + S(pid) + "\" style=\"display:none\">" + Nl);
                        o.Append("\t\t\t\t\t\t<div" + Nl);
                        o.Append("\t\t\t\t\t\t\tid = \"" + sugKey + "\"" + Nl);
                        o.Append("\t\t\t\t\t\t\tproduct_id = \"" + S(pid) + "\"" + Nl);
                        o.Append("\t\t\t\t\t\t\toffice_id = \"" + S(Get(sug, "office_id")) + "\"" + Nl);
                        o.Append("\t\t\t\t\t\t\tstorage_id = \"" + S(Get(sug, "storage_id")) + "\"" + Nl);
                        o.Append("\t\t\t\t\t\t\tstorage_record_id = \"" + S(Get(sug, "record_id")) + "\"" + Nl);
                        o.Append("\t\t\t\t\t\t\tprice = \"" + S(Get(sug, "customer_price")) + "\"" + Nl);
                        o.Append("\t\t\t\t\t\t\ttime_to_exe = \"" + S(Get(sug, "time_to_exe")) + "\"" + Nl);
                        o.Append("\t\t\t\t\t\t\texist = \"" + S(Get(sug, "exist")) + "\"" + Nl);
                        o.Append("\t\t\t\t\t\t\tcheck_hash = \"" + S(Get(sug, "check_hash")) + "\"" + Nl);
                        o.Append("\t\t\t\t\t\t></div>" + Nl);
                        o.Append("\t\t\t\t\t</div>" + Nl);
                        o.Append("\t\t\t\t\t");
                    }
                }
            }
        }

        o.Append("\t\t" + Nl);
        o.Append(Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t");
        if (ptype == 2)
        {
            var capChk = caption.Replace("\"", "", StringComparison.Ordinal).Replace("'", "", StringComparison.Ordinal);
            o.Append("\t\t\t<div class=\"product_checkbox_div\">" + Nl);
            o.Append("\t\t\t\t<input class=\"product_checkbox\" product_id=\"" + S(pid) + "\" product_caption=\"" + capChk + "\" type=\"checkbox\" id=\"product_checkbox_" + S(pid) + "\" />" + Nl);
            o.Append("\t\t\t</div>" + Nl);
            o.Append("\t\t\t");
        }

        o.Append("\t\t" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t<div class=\"article_button\">" + S(Get(product, "article_button")) + "</div>" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t\t<div class=\"main_action_div\">" + S(Get(product, "button")) + "</div>" + Nl);
        o.Append("\t\t" + Nl);
        o.Append("\t</div>" + Nl);
        o.Append("\t");
        if (ptype == 3 && !cls.Contains("product_div_tile", StringComparison.Ordinal))
        {
            o.Append("\t\t<div class=\"product_price_quick_edit work_quick_edit\" product_id=\"" + S(pid) + "\" id=\"work_quick_edit_" + S(pid) + "\"></div>" + Nl);
            o.Append("\t\t");
        }

        return o.ToString();
    }

    private static void AppendSticker(StringBuilder o, JsonElement sticker, string langHref)
    {
        var desc = "";
        if (S(Get(sticker, "description")) != "")
        {
            desc = " title = '" + S(Get(sticker, "description")) + "'";
        }

        o.Append("\t\t\t<div " + desc + " class=\"sticker " + S(Get(sticker, "class_css")) + "\" style=\"background-color:" + S(Get(sticker, "color_background")) + "; color: " + S(Get(sticker, "color_text")) + ";\">" + Nl);
        o.Append("\t\t\t" + Nl);
        o.Append("\t\t\t\t");
        var hrefS = S(Get(sticker, "href"));
        if (hrefS != "")
        {
            o.Append("\t\t\t\t\t<a target=\"_blank\" style=\"color:" + S(Get(sticker, "color_text")) + "; border-bottom:1px dotted " + S(Get(sticker, "color_text")) + ";\" href=\"" + langHref + hrefS + "\">" + Nl);
            o.Append("\t\t\t\t\t\t\t\t\t\t" + Tr(Get(sticker, "value")) + "\t\t\t\t\t\t\t\t\t</a>" + Nl);
            o.Append("\t\t\t\t\t\t\t\t</div>" + Nl);
        }
        else
        {
            o.Append("\t\t\t\t\t" + Tr(Get(sticker, "value")) + "\t\t\t\t\t\t\t</div>" + Nl);
        }

        o.Append("\t\t\t");
    }

    private static string Cookie(IReadOnlyDictionary<string, string> cookies, string name)
        => cookies.TryGetValue(name, out var value) ? value : "";

    private static bool Has(JsonElement obj, string name)
        => obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out _);

    private static JsonElement Get(JsonElement obj, string name)
        => obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var value) ? value : default;

    private static bool PhpEmpty(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null or JsonValueKind.False => true,
            JsonValueKind.True => false,
            JsonValueKind.Number => value.GetDouble() == 0,
            JsonValueKind.String => value.GetString() is "" or "0",
            JsonValueKind.Array => value.GetArrayLength() == 0,
            JsonValueKind.Object => !value.EnumerateObject().Any(),
            _ => false
        };

    private static string S(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => "",
            JsonValueKind.True => "1",
            JsonValueKind.False => "",
            JsonValueKind.Number => NumberText(value),
            JsonValueKind.String => value.GetString() ?? "",
            _ => value.GetRawText()
        };

    private static string NumberText(JsonElement value)
    {
        if (value.TryGetInt64(out var n) && value.GetRawText().IndexOf('.') < 0 && value.GetRawText().IndexOf('e', StringComparison.OrdinalIgnoreCase) < 0)
        {
            return n.ToString(CultureInfo.InvariantCulture);
        }

        return value.GetRawText();
    }

    private static string Tr(int id) => "T" + id.ToString(CultureInfo.InvariantCulture);

    private static string Tr(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var n)
            && value.GetRawText().IndexOf('.') < 0 && value.GetRawText().IndexOf('e', StringComparison.OrdinalIgnoreCase) < 0)
        {
            return "T" + n.ToString(CultureInfo.InvariantCulture);
        }

        var text = S(value);
        return Regex.IsMatch(text, @"^-?\d+$") ? "T" + text : text;
    }

    private static int ToInt(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n))
        {
            return n;
        }

        var text = S(value);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static bool InList(string raw, JsonElement pid)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (S(item) == S(pid))
                {
                    return true;
                }

                if (double.TryParse(S(item), NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
                    && double.TryParse(S(pid), NumberStyles.Float, CultureInfo.InvariantCulture, out var b)
                    && a == b)
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
        }

        return false;
    }
}
