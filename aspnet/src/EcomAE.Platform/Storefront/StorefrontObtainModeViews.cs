using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// The obtaining-mode includes PHP renders on the checkout confirm page (<c>show_details.php</c>) and the CP order card
/// (<c>manager_interface.php</c>) for <c>content/shop/obtaining_modes/get_in_office</c> and <c>epc_carriers</c>, with
/// <c>get_in_office/show_office_info.php</c> (office table and Yandex map). <c>howGetJson</c> is the order's or the
/// cookie's <c>how_get_json</c>; <c>caption</c> is the <c>shop_obtaining_modes.caption</c> string key.
/// </summary>
public static class StorefrontObtainModeViews
{
    private const string YandexStart = "\n\n\n\n<!-- START БЛОК ДЛЯ ВЫВОДА СХЕМ РАСПОЛОЖЕНИЯ ОФИСОВ -->\n<script src=\"https://api-maps.yandex.ru/2.0-stable/?load=package.standard&lang=";

    private const string YandexMapHead =
        "\" type=\"text/javascript\"></script>\n<script type=\"text/javascript\">\n\tvar myMap, myPlacemark;\n\tvar ymaps_init_flag = 0;\n\t// --------------------------------------------------------------------------------------\n\tfunction map_init()\n\t{\n\t\tmyMap = new ymaps.Map (\"map\", {\n\t\t\tcenter: [";

    private const string YandexPlacemark = "],\n\t\t\tzoom: 16\n\t\t}); \n\t\t\n\t\t\n\t\tmyPlacemark = new ymaps.Placemark([";

    private const string YandexBalloon = "], {balloonContent: \"";

    private const string YandexTail =
        "\"}, {\n        \t\t\ticonImageHref: \"/content/files/images/maps-marker.png\",\n        \t\t\ticonImageSize: [27, 44],\n        \t\t\ticonImageOffset: [-13, -44]});\n\t\t\n\t\tmyMap.geoObjects.add(myPlacemark);\n\t\t\n\t\t\n\t\tmyMap.controls.add(new ymaps.control.MapTools());\n\t\tmyMap.controls.add('typeSelector');\n\t\tmyMap.controls.add('zoomControl');\n\t}\n\t// --------------------------------------------------------------------------------------\n\tfunction ymaps_init(){\n\t\tif(ymaps_init_flag == 0){\n\t\t\tymaps.ready(map_init);\n\t\t\tymaps_init_flag = 1;\n\t\t}\n\t}\n</script>\n<!-- END БЛОК ДЛЯ ВЫВОДА СХЕМ РАСПОЛОЖЕНИЯ ОФИСОВ -->";

    /// <summary>The handler's <c>show_details.php</c>; empty for handlers without one.</summary>
    public static async Task<string> ShowDetailsAsync(
        DbConnection connection,
        StorefrontPhpTranslator translator,
        string? handler,
        string? caption,
        string? howGetJson,
        string lang,
        CancellationToken cancellationToken)
    {
        using var how = Parse(howGetJson);
        var root = how.RootElement;
        if (handler == EpcObtainModes.GetInOffice)
        {
            var html = new StringBuilder("<p class=\"lead\">")
                .Append(await translator.TextAsync(3507, cancellationToken).ConfigureAwait(false)).Append(" - ")
                .Append(await translator.TextAsync(caption, cancellationToken).ConfigureAwait(false)).Append("</p>\r\n");
            return html.Append(await OfficeInfoAsync(connection, translator, Text(root, "office_id"), lang, cancellationToken).ConfigureAwait(false)).ToString();
        }

        if (handler != EpcObtainModes.EpcCarriers)
        {
            return string.Empty;
        }

        var carrier = Has(root, "carrier") ? Text(root, "carrier") : "dhl";
        var service = Has(root, "service") ? Text(root, "service") : string.Empty;
        var known = Carrier(carrier);
        var serviceName = known is not null && known.Services.Any(s => s.Code == service)
            ? known.Services.First(s => s.Code == service).Label
            : service;
        double rate;
        if (Has(root, "delivery_price"))
        {
            rate = StorefrontPhpAjax.PhpFloatCast(Text(root, "delivery_price"));
        }
        else if (Has(root, "rate"))
        {
            rate = StorefrontPhpAjax.PhpFloatCast(Text(root, "rate"));
        }
        else
        {
            rate = DemoRate(carrier, Has(root, "weight_kg") ? StorefrontPhpAjax.PhpFloatCast(Text(root, "weight_kg")) : 1, Has(root, "country") ? Text(root, "country") : "AE");
        }

        var details = new StringBuilder("<p class=\"lead\">Delivery — ")
            .Append(H(await translator.TextAsync(caption, cancellationToken).ConfigureAwait(false))).Append("</p>\n<table class=\"table\">\n<tr><th>Carrier &amp; service</th></tr>\n<tr><td>")
            .Append(H(known?.Name ?? carrier.ToUpperInvariant())).Append(" — ").Append(H(serviceName))
            .Append(" (customer pays ").Append(ErpDocumentControlRender.PhpNumberFormat((decimal)rate)).Append(" AED + VAT if UAE)</td></tr>\n")
            .Append("<tr><td>").Append(H(Text(root, "city"))).Append(", ").Append(H(Text(root, "country"))).Append("</td></tr>\n")
            .Append("<tr><td>").Append(H(Text(root, "address"))).Append("</td></tr>\n")
            .Append("<tr><td>Phone: ").Append(H(Text(root, "phone"))).Append("</td></tr>\n")
            .Append("<tr><td>Weight: ").Append(H(Has(root, "weight_kg") ? Text(root, "weight_kg") : "1")).Append(" kg</td></tr>\n</table>\n");
        return details.ToString();
    }

    /// <summary>
    /// The handler's <c>manager_interface.php</c> for the CP order card, or PHP <c>order_card.php</c>'s warning when the
    /// handler has none. <c>obtainCaption</c> is the order's <c>obtain_caption</c>.
    /// </summary>
    public static async Task<string> ManagerInterfaceAsync(
        DbConnection connection,
        StorefrontPhpTranslator translator,
        string? handler,
        string? obtainCaption,
        string? howGetJson,
        long orderId,
        string backendDir,
        string lang,
        CancellationToken cancellationToken)
    {
        using var how = Parse(howGetJson);
        var root = how.RootElement;
        if (handler == EpcObtainModes.GetInOffice)
        {
            var html = new StringBuilder("<p>")
                .Append(await translator.TextAsync(3507, cancellationToken).ConfigureAwait(false)).Append(" - <b>")
                .Append(await translator.TextAsync(obtainCaption, cancellationToken).ConfigureAwait(false)).Append("</b></p>\r\n");
            return html.Append(await OfficeInfoAsync(connection, translator, Text(root, "office_id"), lang, cancellationToken).ConfigureAwait(false)).ToString();
        }

        if (handler != EpcObtainModes.EpcCarriers)
        {
            return "<div class=\"alert alert-warning\">Delivery mode panel unavailable for handler " + H(handler) + ".</div>";
        }

        var carrier = Has(root, "carrier") ? Text(root, "carrier") : "dhl";
        var shipments = new List<string[]>();
        if (orderId > 0)
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = ErpDb.Positional(
                    "SELECT `carrier_code`, IFNULL(`label_url`, ''), IFNULL(`tracking_number`, ''), `status`, `cost`, `currency` FROM `epc_carrier_shipments` WHERE `order_id` = ? ORDER BY `id` DESC");
                ErpDb.AddParameters(command, orderId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var row = new string[6];
                    for (var i = 0; i < row.Length; i++)
                    {
                        row[i] = reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;
                    }

                    shipments.Add(row);
                }
            }
            catch (DbException)
            {
            }
        }

        var panel = new StringBuilder("<p><b>")
            .Append(H(await translator.TextAsync(obtainCaption, cancellationToken).ConfigureAwait(false))).Append("</b> — ")
            .Append(H(Carrier(carrier)?.Name ?? carrier.ToUpperInvariant())).Append("</p>\n<table class=\"table table-condensed\">\n")
            .Append("<tr><td>").Append(H(Text(root, "city"))).Append(", ").Append(H(Text(root, "country"))).Append("</td></tr>\n")
            .Append("<tr><td>").Append(H(Text(root, "address"))).Append("</td></tr>\n")
            .Append("<tr><td>").Append(H(Text(root, "phone"))).Append("</td></tr>\n</table>\n\n");
        if (shipments.Count > 0)
        {
            panel.Append("<table class=\"table table-bordered table-condensed\">\n<thead><tr><th>Carrier</th><th>Tracking</th><th>Status</th><th>Cost</th></tr></thead>\n<tbody>\n");
            foreach (var s in shipments)
            {
                panel.Append("<tr>\n\t<td>").Append(H(s[0].ToUpperInvariant())).Append("</td>\n\t<td>")
                    .Append(s[1].Length > 0 && s[1] != "0" ? "<a href=\"" + H(s[1]) + "\" target=\"_blank\">" + H(s[2]) + "</a>" : H(s[2]))
                    .Append("</td>\n\t<td>").Append(H(s[3])).Append("</td>\n\t<td>")
                    .Append(ErpDocumentControlRender.PhpNumberFormat(decimal.TryParse(s[4], NumberStyles.Number, CultureInfo.InvariantCulture, out var cost) ? cost : 0m))
                    .Append(' ').Append(H(s[5])).Append("</td>\n</tr>\n");
            }

            return panel.Append("</tbody>\n</table>\n").ToString();
        }

        panel.Append("<div class=\"well well-sm\">\n\t<p>Create a demo shipment label (worldwide carriers — DHL, FedEx, Aramex, UPS, and more):</p>\n")
            .Append("\t<form id=\"epc_cp_create_shipment\" class=\"form-inline\">\n\t\t<input type=\"hidden\" name=\"order_id\" value=\"")
            .Append(orderId.ToString(CultureInfo.InvariantCulture)).Append("\">\n\t\t<select name=\"carrier_code\" class=\"form-control input-sm\">\n\t\t\t");
        foreach (var option in EpcObtainModes.Carriers)
        {
            panel.Append("\t\t\t\t<option value=\"").Append(H(option.Code)).Append("\" ").Append(option.Code == carrier ? "selected" : string.Empty)
                .Append('>').Append(H(option.Name)).Append("</option>\n\t\t\t");
        }

        panel.Append("\t\t</select>\n\t\t<input type=\"number\" step=\"0.1\" name=\"weight_kg\" class=\"form-control input-sm\" value=\"")
            .Append(H(Has(root, "weight_kg") ? Text(root, "weight_kg") : "1.5"))
            .Append("\" placeholder=\"kg\">\n\t\t<button type=\"submit\" class=\"btn btn-sm btn-primary\">Create demo label</button>\n\t</form>\n\t<div id=\"epc_ship_msg\" class=\"text-muted\" style=\"margin-top:8px;\"></div>\n</div>\n")
            .Append("<script>\n(function(){\n\tvar f = document.getElementById('epc_cp_create_shipment');\n\tif (!f) return;\n\tf.addEventListener('submit', function(ev){\n\t\tev.preventDefault();\n\t\tvar fd = new FormData(f);\n\t\tfd.append('action', 'create_shipment');\n\t\tfetch('/")
            .Append(H(backendDir))
            .Append("/shop/logistics/carriers', { method: 'POST', body: fd, credentials: 'same-origin' })\n\t\t\t.then(function(r){ return r.json(); })\n\t\t\t.then(function(j){\n\t\t\t\tdocument.getElementById('epc_ship_msg').textContent = j.message || (j.status ? 'OK' : 'Error');\n\t\t\t\tif (j.status) setTimeout(function(){ location.reload(); }, 900);\n\t\t\t});\n\t});\n})();\n</script>\n");
        return panel.ToString();
    }

    /// <summary>PHP <c>get_in_office/show_office_info.php</c> for <c>$office_to_show</c>, with its Yandex map block.</summary>
    public static async Task<string> OfficeInfoAsync(
        DbConnection connection,
        StorefrontPhpTranslator translator,
        string officeId,
        string lang,
        CancellationToken cancellationToken)
    {
        var office = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT `city`, `address`, `timetable`, `phone`, `coordinates`, `caption` FROM `shop_offices` WHERE `id` = ?");
            ErpDb.AddParameters(command, officeId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                string[] keys = ["city", "address", "timetable", "phone", "coordinates", "caption"];
                for (var i = 0; i < keys.Length; i++)
                {
                    office[keys[i]] = reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;
                }
            }
        }
        catch (DbException)
        {
        }

        string O(string key) => office.TryGetValue(key, out var v) ? v : string.Empty;
        async Task<string> T(string key) => await translator.TextAsync(key, cancellationToken).ConfigureAwait(false);
        async Task<string> Tid(int id) => await translator.TextAsync(id, cancellationToken).ConfigureAwait(false);
        var html = new StringBuilder("\n<table class=\"table\">\n\t<tr>\n\t\t<th>").Append(await Tid(4418)).Append("</th>\n\t</tr>\n\t<tr>\n\t\t<td>\n\t\t\t<span>")
            .Append(await Tid(3376)).Append(": ").Append(await T(O("city"))).Append(", ").Append(await T(O("address")))
            .Append("</span> <a title=\"").Append(await Tid(4445))
            .Append("\" style=\"cursor:pointer;\" data-toggle=\"collapse\" onClick=\"ymaps_init();\" data-target=\"#collapse_office_map_container\" aria-expanded=\"false\" aria-controls=\"collapse_office_map_container\"><i class=\"fa fa-map-o\" aria-hidden=\"true\"></i></a>\n")
            .Append("\t\t\t<div class=\"collapse\" id=\"collapse_office_map_container\">\n\t\t\t\t<br/>\n\t\t\t\t<div style=\"width: 100%;\" id=\"map\" class=\"office_map_container\"></div>\n\t\t\t</div>\n\t\t</td>\n\t</tr>\n")
            .Append("\t<tr>\n\t\t<td>").Append(await Tid(4446)).Append(": ").Append((await T(O("timetable"))).Replace("\n", "<br>", StringComparison.Ordinal)).Append("</td>\n\t</tr>\n")
            .Append("\t<tr>\n\t\t<td>").Append(await Tid(1312)).Append(": ").Append(O("phone")).Append("</td>\n\t</tr>\n</table>")
            .Append(YandexStart).Append(lang != "ru" ? "en_US" : "ru-RU")
            .Append(YandexMapHead).Append(O("coordinates"))
            .Append(YandexPlacemark).Append(O("coordinates"))
            .Append(YandexBalloon).Append(await T(O("caption")))
            .Append(YandexTail);
        return html.ToString();
    }

    /// <summary>PHP <c>epc_channel_demo_rate</c>.</summary>
    public static double DemoRate(string carrier, double weightKg, string country)
    {
        var weight = (decimal)Math.Max(0.1, weightKg);
        var baseline = Carrier(carrier)?.DemoBase ?? 35m;
        var intl = country.Length > 0 && country.ToUpperInvariant() != "AE" ? 1.35m : 1.0m;
        return (double)Math.Round((baseline + (weight * 8.5m)) * intl, 2, MidpointRounding.AwayFromZero);
    }

    private static EpcCarrierOption? Carrier(string code)
        => EpcObtainModes.Carriers.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.Ordinal));

    private static JsonDocument Parse(string? json)
    {
        try
        {
            var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                return document;
            }

            document.Dispose();
        }
        catch (JsonException)
        {
        }

        return JsonDocument.Parse("{}");
    }

    private static bool Has(JsonElement root, string key)
        => root.TryGetProperty(key, out var value) && value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;

    private static string Text(JsonElement root, string key)
        => root.TryGetProperty(key, out var value) ? StorefrontPhpAjax.PhpJsonScalarString(value) : string.Empty;

    private static string H(string? value) => ErpDocumentControlRender.H(value);
}
