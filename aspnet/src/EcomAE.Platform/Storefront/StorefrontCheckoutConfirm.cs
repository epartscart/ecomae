using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/shop/order_process/checkout_confirm.php</c>. This renderer is read-only: order creation remains
/// owned by the already ported legacy <c>ajax_checkout_create</c> endpoint.
/// </summary>
/// <remarks>Intentional security deviation: values read from the database and inserted into HTML or JavaScript are escaped.</remarks>
public static class StorefrontCheckoutConfirm
{
    public sealed record Input(
        int UserId,
        long SessionId,
        string CsrfKey,
        string LangHref,
        string ShopCurrency,
        string CurrencyShowMode,
        string HowGetJson,
        bool TradeCheckoutBlocked,
        string TradeCheckoutMessage);

    public sealed record RenderResult(string Html, string? RedirectLocation = null, bool IsJson = false);

    private sealed record CartLine(
        string Name,
        decimal Price,
        string CountNeed,
        int TimeToExe,
        int TimeToExeGuaranteed);

    public static async Task<RenderResult> RenderAsync(
        DbConnection connection,
        Input input,
        Func<string, Task<string>> translate,
        Func<string, bool> obtainHandlerExists,
        Func<string, string, string, Task<string>> renderObtainDetails,
        Func<Task<string>> renderComplementaryParts,
        CancellationToken cancellationToken)
    {
        var currency = await RowAsync(
            connection,
            "SELECT `sign`,`caption_short` FROM `shop_currencies` WHERE `iso_code` = ? LIMIT 1",
            [input.ShopCurrency],
            cancellationToken).ConfigureAwait(false);
        var currencyIndicator = input.CurrencyShowMode switch
        {
            "no" => string.Empty,
            "sign_before" or "sign_after" => currency?[0] ?? string.Empty,
            _ => currency?[1] ?? string.Empty,
        };

        if (input.UserId <= 0 && input.SessionId <= 0)
        {
            var message = await translate("4460").ConfigureAwait(false);
            return new RenderResult(
                JsonSerializer.Serialize(
                    new Dictionary<string, object?>
                    {
                        ["status"] = false,
                        ["code"] = "incorrect_session",
                        ["message"] = message,
                    },
                    new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
                IsJson: true);
        }

        if (!TryPrepareHowGet(input.HowGetJson, out var mode, out var preparedHowGet))
        {
            return new RenderResult(string.Empty, input.LangHref + "/shop/checkout/how_get");
        }

        var obtain = await RowAsync(
            connection,
            "SELECT `caption`,`handler` FROM `shop_obtaining_modes` WHERE `id` = ? AND `available` = 1 LIMIT 1",
            [mode],
            cancellationToken).ConfigureAwait(false);
        var handler = SanitizeHandler(obtain?[1]);
        if (handler.Length == 0 || !obtainHandlerExists(handler))
        {
            return new RenderResult(string.Empty, input.LangHref + "/shop/checkout/how_get");
        }

        var sessionId = input.UserId > 0 ? 0 : input.SessionId;
        var lines = await CartLinesAsync(connection, input.UserId, sessionId, cancellationToken).ConfigureAwait(false);
        var customerType = input.UserId > 0
            ? await ProfileValueAsync(connection, input.UserId, "epc_customer_type", cancellationToken).ConfigureAwait(false)
            : string.Empty;
        var details = await renderObtainDetails(
            handler,
            obtain?[0] ?? string.Empty,
            JsonSerializer.Serialize(preparedHowGet, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            .ConfigureAwait(false);
        var complementary = await renderComplementaryParts().ConfigureAwait(false);

        var html = new StringBuilder();
        html.Append("\n<p class=\"lead\">{4506}</p>\n<p>{4507}</p>\n");
        if (input.TradeCheckoutMessage.Length > 0)
        {
            html.Append("\t<div class=\"alert alert-warning\" style=\"margin:15px 0;\">")
                .Append(H(input.TradeCheckoutMessage)).Append("</div>\n\t");
        }

        html.Append("\n<div style=\"overflow: hidden; overflow-x: auto;\">\n    <table class=\"table\">\n\t\t<tr>\n")
            .Append("\t\t\t<th style=\"vertical-align: middle; white-space: nowrap;\">{4508}</th>\n")
            .Append("\t\t\t<th style=\"vertical-align: middle; white-space: nowrap; text-align: right;\">{3550}</th>\n")
            .Append("\t\t\t<th style=\"vertical-align: middle; white-space: nowrap; text-align: right;\">{2751}</th>\n")
            .Append("\t\t\t<th style=\"vertical-align: middle; white-space: nowrap; text-align: center;\">{2752}</th>\n")
            .Append("\t\t\t<th style=\"vertical-align: middle; white-space: nowrap; text-align: right;\">{3251}</th>\n")
            .Append("\t\t</tr>\n");

        decimal total = 0;
        foreach (var line in lines)
        {
            var sum = line.Price * PhpDecimal(line.CountNeed);
            total += sum;
            var term = line.TimeToExe < line.TimeToExeGuaranteed
                ? line.TimeToExe.ToString(CultureInfo.InvariantCulture) + " - " + line.TimeToExeGuaranteed.ToString(CultureInfo.InvariantCulture)
                : line.TimeToExe.ToString(CultureInfo.InvariantCulture);
            term = term == "0" ? "{4197}" : term + " {4097}.";
            html.Append("    \n    \n    \n    <tr>\n")
                .Append("        <td style=\"vertical-align: middle; width: 100%; min-width: 200px; max-width: 800px; word-wrap: break-word;\">\n")
                .Append("            ").Append(H(line.Name)).Append("\n        </td>\n        \n\t\t")
                .Append("\n\t\t\n\t\t\n        <td style=\"vertical-align: middle; white-space: nowrap; text-align: right;\">\n")
                .Append("            ").Append(term).Append("\n        </td>\n")
                .Append("        <td style=\"vertical-align: middle; white-space: nowrap; text-align: right;\">\n")
                .Append("            ").Append(Price(line.Price, input.CurrencyShowMode, currencyIndicator, false)).Append("\n        </td>\n")
                .Append("        \n        <td style=\"vertical-align: middle; white-space: nowrap; text-align: center;\">\n")
                .Append("            ").Append(H(line.CountNeed)).Append("\n        </td>\n        \n")
                .Append("        <td style=\"vertical-align: middle; white-space: nowrap; text-align: right;\">\n")
                .Append("            ").Append(Price(sum, input.CurrencyShowMode, currencyIndicator, true)).Append("\n        </td>\n")
                .Append("    </tr>\n    ");
        }

        html.Append("\n</table>\n</div>\n\n\n<div style=\"margin-bottom: 0px; text-align: right; font-size: 18px; font-weight: bold;\"><span style=\"font-size: 14px; font-weight: normal;\">{3503}:</span> ")
            .Append(Price(total, input.CurrencyShowMode, currencyIndicator, false)).Append("</div>\n\n")
            .Append("<div class=\"hidden-sm hidden-md hidden-lg\" style=\"margin-bottom:40px;\"></div>\n\n")
            .Append("<div style=\"overflow-x: auto;\">").Append(details).Append("</div>\n")
            .Append("\n\n\n\n\n\n<div class=\"row\">\n\t<div class=\"col-lg-12\">\n\t\t<p class=\"lead\">{4509}:</p>\n")
            .Append("\t\t<textarea style=\"height: 36px;\" class=\"form-control\" id=\"message_textarea\" rows=\"1\" placeholder=\"{4510}...\"></textarea>\n")
            .Append("\t</div>\n</div>\n\n");

        if (input.UserId > 0)
        {
            html.Append("<div class=\"row\" style=\"margin-top:12px;\">\n\t<div class=\"col-lg-6\">\n")
                .Append("\t\t<p class=\"lead\">Purchase order (PO) number")
                .Append(customerType == "wholesale" ? string.Empty : " <small class=\"text-muted\">(optional)</small>")
                .Append(":</p>\n")
                .Append("\t\t<input style=\"height: 36px;\" class=\"form-control\" type=\"text\" id=\"buyer_po_number\" maxlength=\"64\" placeholder=\"Your internal PO reference\" />\n")
                .Append("\t</div>\n</div>\n");
        }

        html.Append('\n').Append(complementary).Append("\n\n\n\n\n\n\n");
        if (input.UserId <= 0)
        {
            html.Append("\t<div class=\"row\" style=\"margin-top:20px;\">\n\t\t<div class=\"col-lg-6\">\n")
                .Append("\t\t\t<p class=\"lead\">{4511}*:</p>\n")
                .Append("\t\t\t<input style=\"height: 36px;\" class=\"form-control\" type=\"text\" id=\"phone_not_auth\" value=\"\" placeholder=\"{4512}\" />\n")
                .Append("\t\t</div>\n\n\t\n\t\t<div class=\"col-lg-6\">\n")
                .Append("\t\t\t<p class=\"lead\">E-mail:</p>\n")
                .Append("\t\t\t<input style=\"height: 36px;\" class=\"form-control\" type=\"text\" id=\"email_not_auth\" value=\"\" placeholder=\"{4513}\" />\n")
                .Append("\t\t</div>\n\n\t\t<div class=\"col-xs-12 hidden-lg\" style=\"margin-top: 20px;\"></div>\n\t</div>\n\t");
        }

        var phoneRegexp = input.UserId <= 0
            ? await RegexpAsync(connection, "phone", cancellationToken).ConfigureAwait(false)
            : string.Empty;
        var emailRegexp = input.UserId <= 0
            ? await RegexpAsync(connection, "email", cancellationToken).ConfigureAwait(false)
            : string.Empty;
        AppendScript(html, input, phoneRegexp, emailRegexp);
        html.Append('\n').Append(StorefrontAuthPartials.UsersAgreementModule(input.LangHref, id => "{" + id.ToString(CultureInfo.InvariantCulture) + "}"))
            .Append("\n\n\n<div class=\"order_confirm_button_div text-center\">\n\t");
        if (input.TradeCheckoutBlocked)
        {
            html.Append("<p class=\"text-muted\">Place order is disabled until your trade account is approved.</p>\n\t");
        }
        else
        {
            html.Append("<button id=\"confirm_btn\" class=\"btn btn-ar btn-primary\" onclick=\"confirm();\">{4521}</button>\n\t\n")
                .Append("\t<div id=\"confirm_loader\" style=\"display:none;\">\n\t\t<p>{4293}</p>\n")
                .Append("\t\t<img src=\"/content/files/images/ajax-loader-transparent.gif\" />\n\t</div>\n\t");
        }
        html.Append("\n</div>\n");
        return new RenderResult(await StorefrontCart.TranslateAsync(html.ToString(), translate).ConfigureAwait(false));
    }

    public static async Task<string> RenderComplementaryPartsAsync(
        DbConnection connection,
        int userId,
        long sessionId,
        string langHref,
        CancellationToken cancellationToken)
    {
        var cart = await RowsAsync(
            connection,
            "SELECT `t2_manufacturer`,`t2_article` FROM `shop_carts` WHERE `user_id` = ? AND `session_id` = ? AND `checked_for_order` = 1 ORDER BY `id` ASC LIMIT 6",
            [userId, sessionId],
            cancellationToken).ConfigureAwait(false);
        var suggestions = new List<(string Brand, string Article)>();
        var excluded = new HashSet<string>(cart.Select(r => PartKey(r[0], r[1])), StringComparer.Ordinal);
        foreach (var row in cart)
        {
            try
            {
                var norm = NormalizeArticle(row[1]);
                var refs = await RowsAsync(
                    connection,
                    "SELECT `article`,`manufacturer_article`,`analog`,`manufacturer_analog` FROM `shop_docpart_articles_analogs_list` "
                    + "WHERE `article_search` = ? OR `analog_search` = ? LIMIT 40",
                    [norm, norm],
                    cancellationToken).ConfigureAwait(false);
                foreach (var cross in refs)
                {
                    var candidate = NormalizeArticle(cross[0]) == norm ? (cross[3], cross[2]) : (cross[1], cross[0]);
                    var key = PartKey(candidate.Item1, candidate.Item2);
                    if (key != "|" && excluded.Add(key))
                    {
                        suggestions.Add((candidate.Item1 ?? string.Empty, candidate.Item2 ?? string.Empty));
                    }
                    if (suggestions.Count == 8)
                    {
                        break;
                    }
                }
            }
            catch (DbException)
            {
                break;
            }
        }
        if (suggestions.Count == 0)
        {
            return string.Empty;
        }

        var html = new StringBuilder("<div class=\"epc-complementary-panel alert alert-info\" style=\"margin:16px 0;\">")
            .Append("<h4 style=\"margin:0 0 10px;font-size:16px;\"><i class=\"fa fa-puzzle-piece\"></i> Customers also order these related parts</h4>")
            .Append("<p class=\"text-muted\" style=\"margin:0 0 10px;font-size:13px;\">Based on cross references and catalogue clusters — verify fitment before ordering.</p>")
            .Append("<div class=\"epc-complementary-list\" style=\"display:flex;flex-wrap:wrap;gap:8px;\">");
        foreach (var suggestion in suggestions)
        {
            var href = langHref + "/shop/part_search?article=" + Uri.EscapeDataString(suggestion.Article);
            if (suggestion.Brand.Length > 0)
            {
                href += "&brend=" + Uri.EscapeDataString(suggestion.Brand);
            }
            html.Append("<a class=\"btn btn-sm btn-default\" href=\"").Append(H(href)).Append("\" style=\"margin:0;\">")
                .Append(H((suggestion.Brand + " " + suggestion.Article).Trim())).Append("</a>");
        }
        return html.Append("</div></div>").ToString();
    }

    private static void AppendScript(StringBuilder html, Input input, string phoneRegexp, string emailRegexp)
    {
        html.Append("\n<script>\n//ОБРАБОТКА КНОПКИ ПОДТВЕРЖДЕНИЯ\nfunction confirm()\n{\n")
            .Append("\tdocument.getElementById(\"confirm_btn\").style.display = 'none';\n")
            .Append("\tdocument.getElementById(\"confirm_loader\").style.display = 'block';\n\t\n")
            .Append("\tvar result = confirm_order();\n\t\n\tif(result == false){\n")
            .Append("\t\tdocument.getElementById(\"confirm_loader\").style.display = 'none';\n")
            .Append("\t\tdocument.getElementById(\"confirm_btn\").style.display = 'inline';\n\t}\n}\n\n\n")
            .Append("//ПОДТВЕРЖДЕНИЕ ЗАКАЗА\nfunction confirm_order()\n{\n")
            .Append("\t//Проверка согласия с обработкой персональных данных\n\tif( !check_user_agreement() )\n\t{\n\t\treturn false;\n\t}\n\t\n\t\n\t\n")
            .Append("\tvar phone_not_auth = '';\n\tvar email_not_auth = '';\n\t");
        if (input.UserId <= 0)
        {
            html.Append("\t\t//Телефон - обязателен\n\t\tphone_not_auth = document.getElementById(\"phone_not_auth\").value;\n")
                .Append("\t\tif( String(phone_not_auth) == '' )\n\t\t{\n\t\t\talert(\"{4514}\");\n\t\t\treturn false;\n\t\t}\n\t\t\n")
                .Append("\t\t//E-mail - не обязателен\n\t\temail_not_auth = document.getElementById(\"email_not_auth\").value;\n\t\t\n")
                .Append("\t\t//var date = new Date(new Date().getTime() + 15552000 * 1000);\n")
                .Append("\t\t//document.cookie = \"phone_not_auth=\"+encodeURIComponent(phone_not_auth)+\"; path=/; expires=\" + date.toUTCString();\n\t\t");
            if (phoneRegexp.Length > 0)
            {
                html.Append("\t\t\tvar current_value = String(phone_not_auth);//Заполненное значение\n")
                    .Append("\t\t\tvar regex = new RegExp('").Append(Js(phoneRegexp)).Append("');//Регулярное выражение для поля\n")
                    .Append("\t\t\t//Далее ищем подстроку по регулярному выражению\n\t\t\tvar match = regex.exec(String(current_value));\n")
                    .Append("\t\t\tif(match == null)\n\t\t\t{\n\t\t\t\talert(\"{4515}\");\n\t\t\t\treturn false;\n\t\t\t}\n\t\t\telse\n\t\t\t{\n")
                    .Append("\t\t\t\tvar match_value = String(match[0]);//Подходящая подстрока\n\t\t\t\tif(match_value != current_value)\n\t\t\t\t{\n")
                    .Append("\t\t\t\t\talert(\"{4516}\");\n\t\t\t\t\treturn false;\n\t\t\t\t}\n\t\t\t}\n\t\t\t");
            }
            if (emailRegexp.Length > 0)
            {
                html.Append("\t\t\tif( String( email_not_auth ) != \"\" )\n\t\t\t{\n")
                    .Append("\t\t\t\tvar current_value = String(email_not_auth);//Заполненное значение\n")
                    .Append("\t\t\t\tvar regex = new RegExp('").Append(Js(emailRegexp)).Append("');//Регулярное выражение для поля\n")
                    .Append("\t\t\t\t//Далее ищем подстроку по регулярному выражению\n\t\t\t\tvar match = regex.exec(String(current_value));\n")
                    .Append("\t\t\t\tif(match == null)\n\t\t\t\t{\n\t\t\t\t\talert(\"{4517}\");\n\t\t\t\t\treturn false;\n\t\t\t\t}\n")
                    .Append("\t\t\t\telse\n\t\t\t\t{\n\t\t\t\t\tvar match_value = String(match[0]);//Подходящая подстрока\n")
                    .Append("\t\t\t\t\tif(match_value != current_value)\n\t\t\t\t\t{\n\t\t\t\t\t\talert(\"{4518}\");\n\t\t\t\t\t\treturn false;\n\t\t\t\t\t}\n\t\t\t\t}\n")
                    .Append("\t\t\t\t//Заполнено правильно, если: есть подстрока по регулярному выражению и она полностью равна самой строке\n\t\t\t}\n\t\t\t");
            }
        }
        html.Append("\n\t\n\t// Комментарий заказа\n\tvar message = document.getElementById(\"message_textarea\").value;\n")
            .Append("\tvar buyer_po = '';\n\tvar poEl = document.getElementById(\"buyer_po_number\");\n\tif (poEl) {\n\t\tbuyer_po = poEl.value || '';\n\t}\n\t\n\t\n")
            .Append("    jQuery.ajax({\n        type: \"POST\",\n        async: true, //Запрос синхронный\n")
            .Append("        url: \"/content/shop/order_process/ajax_checkout_create.php\",\n")
            .Append("        dataType: \"text\",//Тип возвращаемого значения\n")
            .Append("\t\tdata: \"order_message=\"+encodeURIComponent(message)+\"&buyer_po_number=\"+encodeURIComponent(buyer_po)+\"&phone_not_auth=\"+encodeURIComponent(phone_not_auth)+\"&email_not_auth=\"+encodeURIComponent(email_not_auth)+\"&csrf_guard_key=")
            .Append(Js(input.CsrfKey)).Append("\",\n")
            .Append("        success: function(answer)\n        {\n\t\t\tconsole.log(answer);\n\t\t\t\t\n\t\t\tvar answer_ob = JSON.parse(answer);\n\t\t\t\n")
            .Append("\t\t\t//Если некорректный парсинг ответа\n\t\t\tif( typeof answer_ob.status === \"undefined\" )\n\t\t\t{\n\t\t\t\talert(\"{4519}\");\n\t\t\t}\n")
            .Append("\t\t\telse\n\t\t\t{\n\t\t\t\t//Корректный парсинг ответа\n\t\t\t\tif(answer_ob.status == true)\n\t\t\t\t{\n\t\t\t\t\t");
        var destination = input.UserId > 0 ? "/shop/orders/order" : "/shop/orders/zakaz-bez-registracii";
        html.Append("\t\t\t\t\tlocation = \"").Append(Js(input.LangHref + destination))
            .Append("?order_id=\"+answer_ob.order_id+\"&success_message=\"+encodeURI(\"{4520}.\");\n\t\t\t\t\t\t")
            .Append("\t\t\t\t}\n\t\t\t\telse\n\t\t\t\t{\n\t\t\t\t\talert(answer_ob.message);\n\t\t\t\t\n")
            .Append("\t\t\t\t\tdocument.getElementById(\"confirm_loader\").style.display = 'none';\n")
            .Append("\t\t\t\t\tdocument.getElementById(\"confirm_btn\").style.display = 'inline';\n\t\t\t\t\t\n\t\t\t\t\treturn false;\n")
            .Append("\t\t\t\t}\n\t\t\t}\n        }\n    });\n}\n</script>\n");
    }

    private static async Task<List<CartLine>> CartLinesAsync(DbConnection connection, int userId, long sessionId, CancellationToken cancellationToken)
    {
        var ids = await RowsAsync(connection, "SELECT `id` FROM `shop_carts` WHERE `user_id` = ? AND `session_id` = ?", [userId, sessionId], cancellationToken).ConfigureAwait(false);
        var lines = new List<CartLine>();
        foreach (var id in ids)
        {
            var row = await RowAsync(
                connection,
                "SELECT `t2_manufacturer`,`t2_article`,`t2_name`,`price`,`count_need`,`t2_time_to_exe`,`t2_time_to_exe_guaranteed` "
                + "FROM `shop_carts` WHERE `id` = ? AND `user_id` = ? AND `checked_for_order` = 1",
                [id[0], userId],
                cancellationToken).ConfigureAwait(false);
            if (row is null)
            {
                continue;
            }
            lines.Add(new CartLine(
                string.Join(' ', new[] { row[0], row[1], row[2] }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim(),
                PhpDecimal(row[3]),
                row[4] ?? "0",
                Int(row[5]),
                Int(row[6])));
        }
        return lines;
    }

    private static bool TryPrepareHowGet(string raw, out int mode, out Dictionary<string, object?> prepared)
    {
        mode = 0;
        prepared = new(StringComparer.Ordinal);
        try
        {
            using var json = JsonDocument.Parse(raw);
            if (json.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }
            prepared = PrepareObject(json.RootElement);
            mode = prepared.TryGetValue("mode", out var value) ? Int(Convert.ToString(value, CultureInfo.InvariantCulture)) : 0;
            return mode != 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static Dictionary<string, object?> PrepareObject(JsonElement element)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.Object => PrepareObject(property.Value),
                JsonValueKind.Array => property.Value.EnumerateArray().Select(PrepareValue).ToArray(),
                _ => PrepareValue(property.Value),
            };
        }
        return result;
    }

    private static object? PrepareValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => PrepareObject(value),
        JsonValueKind.Array => value.EnumerateArray().Select(PrepareValue).ToArray(),
        JsonValueKind.True => "1",
        JsonValueKind.False or JsonValueKind.Null => string.Empty,
        JsonValueKind.String => WebUtility.HtmlEncode(value.GetString() ?? string.Empty),
        _ => WebUtility.HtmlEncode(value.GetRawText()),
    };

    private static string Price(decimal value, string mode, string indicator, bool bold)
    {
        var amount = value.ToString("#,0.00", CultureInfo.InvariantCulture).Replace(",", " ", StringComparison.Ordinal);
        var rendered = mode switch
        {
            "sign_before" => "<font class=\"currency\">" + H(indicator) + "</font> <font class=\"price\">" + amount + "</font>",
            "sign_after" or "short_name_after" => "<font class=\"price\">" + amount + "</font> <font class=\"currency\">" + H(indicator) + "</font>",
            _ => amount,
        };
        return bold && mode is "sign_before" or "sign_after" or "short_name_after" ? "<b>" + rendered + "</b>" : rendered;
    }

    private static async Task<string> ProfileValueAsync(DbConnection connection, int userId, string key, CancellationToken cancellationToken)
        => (await ScalarAsync(connection, "SELECT `data_value` FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = ? LIMIT 1", [userId, key], cancellationToken).ConfigureAwait(false) ?? string.Empty).Trim();

    private static async Task<string> RegexpAsync(DbConnection connection, string name, CancellationToken cancellationToken)
        => await ScalarAsync(connection, "SELECT `regexp` FROM `reg_fields` WHERE `name` = ? LIMIT 1", [name], cancellationToken).ConfigureAwait(false) ?? string.Empty;

    private static string SanitizeHandler(string? value)
        => new((value ?? string.Empty).Where(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-').ToArray());

    private static string PartKey(string? brand, string? article) => (brand ?? string.Empty).Trim().ToUpperInvariant() + "|" + NormalizeArticle(article);
    private static string NormalizeArticle(string? value)
        => new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    private static decimal PhpDecimal(string? value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : 0m;
    private static int Int(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0;
    private static string H(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
    private static string Js(string? value) => (value ?? string.Empty)
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("'", "\\'", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("<", "\\u003C", StringComparison.Ordinal)
        .Replace(">", "\\u003E", StringComparison.Ordinal)
        .Replace("&", "\\u0026", StringComparison.Ordinal);

    private static async Task<string?> ScalarAsync(DbConnection connection, string sql, object?[] args, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args);
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    private static async Task<string?[]?> RowAsync(DbConnection connection, string sql, object?[] args, CancellationToken cancellationToken)
        => (await RowsAsync(connection, sql, args, cancellationToken).ConfigureAwait(false)).FirstOrDefault();

    private static async Task<List<string?[]>> RowsAsync(DbConnection connection, string sql, object?[] args, CancellationToken cancellationToken)
    {
        var rows = new List<string?[]>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new string?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++)
            {
                row[i] = reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);
            }
            rows.Add(row);
        }
        return rows;
    }
}
