using System.Data.Common;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP parts-catalogues search tab: AutoXP, Ilcats, and Catalogs-Parts links
/// from <c>shop_docpart_cars_catalogue_links</c>, plus the AutoXP monthly click gate.
/// </summary>
public static class StorefrontPartsCatalogues
{
    public const string Path = "/storefront/parts-catalogues.json";
    public const string AutoxpClicksPath = "/autoxp_clicks_control.php";
    public const string NotInDatabase = "Parts catalogues are not in this database.";
    public const string NotConfigured = "Parts catalogues are not configured in this database.";
    public const string NoneEnabled = "No parts catalogues are enabled.";
    public const string BadSettings = "Parts catalogue settings are not valid JSON.";
    public const string AutoxpMissing = "AutoXP click counter is not in this database.";
    public const int AutoxpMonthlyLimit = 2000;

    public sealed record CatalogueLink(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("caption")] string Caption,
        [property: JsonPropertyName("order")] int Order,
        [property: JsonPropertyName("href")] string Href);

    public sealed record Brand(
        [property: JsonPropertyName("car_id")] int CarId,
        [property: JsonPropertyName("caption")] string Caption,
        [property: JsonPropertyName("catalogues")] IReadOnlyList<CatalogueLink> Catalogues);

    public sealed record ListBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("brands")] IReadOnlyList<Brand> Brands);

    public readonly record struct ClickGate(bool Allowed, string? Message);

    public static async Task<ListBody> LoadAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            var raw = await ErpDb.StringAsync(
                connection,
                null,
                "SELECT `parameters_values` FROM `shop_docpart_search_tabs` WHERE `name` = 'parts_catalogues' LIMIT 1",
                cancellationToken).ConfigureAwait(false);
            if (raw is null)
            {
                return new ListBody(false, NotConfigured, []);
            }

            JsonNode? parameters;
            try
            {
                parameters = string.IsNullOrWhiteSpace(raw) ? new JsonObject() : JsonNode.Parse(raw);
            }
            catch (System.Text.Json.JsonException)
            {
                return new ListBody(false, BadSettings, []);
            }

            var enabled = new List<(int Id, string Name)>();
            await using (var catalogues = connection.CreateCommand())
            {
                catalogues.CommandText = "SELECT `id`, IFNULL(`assoc_name`, '') FROM `shop_docpart_cars_catalogues`";
                await using var reader = await catalogues.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var name = reader.GetString(1).Trim();
                    if (name.Length == 0 || !string.Equals(Text(parameters, name + "_show"), "on", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    enabled.Add((Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture), name));
                }
            }

            if (enabled.Count == 0)
            {
                return new ListBody(true, NoneEnabled, []);
            }

            var ids = string.Join(", ", enabled.Select(item => item.Id.ToString(CultureInfo.InvariantCulture)));
            var rows = new List<(int CarId, string Caption, string Name, string Href)>();
            await using (var links = connection.CreateCommand())
            {
                links.CommandText =
                    "SELECT l.`car_id`, IFNULL(c.`caption`, ''), IFNULL(cat.`assoc_name`, ''), IFNULL(l.`href`, '') "
                    + "FROM `shop_docpart_cars_catalogue_links` l "
                    + "INNER JOIN `shop_docpart_cars` c ON c.`id` = l.`car_id` "
                    + "INNER JOIN `shop_docpart_cars_catalogues` cat ON cat.`id` = l.`catalogue_id` "
                    + "WHERE l.`catalogue_id` IN (" + ids + ")";
                await using var reader = await links.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add((
                        Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                        reader.GetString(1),
                        reader.GetString(2).Trim(),
                        reader.GetString(3)));
                }
            }

            return new ListBody(true, string.Empty, Build(parameters, rows));
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new ListBody(false, NotInDatabase, []);
        }
    }

    public static IReadOnlyList<Brand> Build(
        JsonNode? parameters,
        IReadOnlyList<(int CarId, string Caption, string Name, string Href)> rows)
    {
        var byCar = new Dictionary<int, List<CatalogueLink>>();
        var captions = new Dictionary<int, string>();
        foreach (var row in rows)
        {
            if (!TryLink(parameters, row.CarId, row.Name, row.Href, out var link))
            {
                continue;
            }

            if (!byCar.TryGetValue(row.CarId, out var list))
            {
                list = [];
                byCar[row.CarId] = list;
                captions[row.CarId] = row.Caption.Trim().ToUpperInvariant();
            }

            list.Add(link);
        }

        return byCar
            .Select(pair => new Brand(
                pair.Key,
                captions[pair.Key],
                pair.Value.OrderBy(item => item.Order).ThenBy(item => item.Name, StringComparer.Ordinal).ToArray()))
            .Where(brand => brand.Catalogues.Count > 0)
            .OrderBy(brand => brand.Caption, StringComparer.Ordinal)
            .ToArray();
    }

    public static async Task<ClickGate> RecordAutoxpClickAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var month = now.Month;
        var year = now.Year;
        try
        {
            var count = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `clicks_count` FROM `shop_docpart_autoxp_clicks` WHERE `month` = ? AND `year` = ? LIMIT 1"),
                cancellationToken,
                month,
                year).ConfigureAwait(false);
            var exists = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `shop_docpart_autoxp_clicks` WHERE `month` = ? AND `year` = ?"),
                cancellationToken,
                month,
                year).ConfigureAwait(false);
            if (exists == 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("INSERT INTO `shop_docpart_autoxp_clicks` (`month`, `year`, `clicks_count`) VALUES (?, ?, ?)"),
                    cancellationToken,
                    month,
                    year,
                    1).ConfigureAwait(false);
                return new ClickGate(true, null);
            }

            if (count >= AutoxpMonthlyLimit)
            {
                return new ClickGate(false, null);
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_docpart_autoxp_clicks` SET `clicks_count` = `clicks_count` + 1 WHERE `month` = ? AND `year` = ?"),
                cancellationToken,
                month,
                year).ConfigureAwait(false);
            return new ClickGate(true, null);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new ClickGate(false, AutoxpMissing);
        }
    }

    public static bool IsHttpTarget(string? next)
    {
        if (!Uri.TryCreate(next, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme is "http" or "https";
    }

    private static bool TryLink(JsonNode? parameters, int carId, string name, string href, out CatalogueLink link)
    {
        link = new CatalogueLink(name, Text(parameters, name + "_caption"), Order(parameters, name + "_order"), href);
        if (link.Caption.Length == 0)
        {
            link = link with { Caption = name };
        }

        if (name == "autoxp")
        {
            if (!Listed(parameters, "autoxp_show_cars", carId))
            {
                return false;
            }

            link = link with { Href = href + Text(parameters, "autoxp_id") };
            return true;
        }

        if (name == "ilcats")
        {
            var pid = Text(parameters, "ilcats_car_" + carId.ToString(CultureInfo.InvariantCulture));
            if (pid.Length == 0)
            {
                return false;
            }

            link = link with
            {
                Href = href.Replace("<pid>", pid, StringComparison.Ordinal)
                    .Replace("<clid>", Text(parameters, "ilcats_clid"), StringComparison.Ordinal),
            };
            return true;
        }

        if (name == "catalogs_parts_com")
        {
            if (!Listed(parameters, "catalogs_parts_com_show_cars", carId))
            {
                return false;
            }

            link = link with
            {
                Href = href.Replace("client:;", "client:" + Text(parameters, "catalogs_parts_com_id") + ";", StringComparison.Ordinal),
            };
            return true;
        }

        return href.Length > 0;
    }

    private static bool Listed(JsonNode? parameters, string key, int carId)
    {
        if (parameters?[key] is not JsonArray array)
        {
            return false;
        }

        var id = carId.ToString(CultureInfo.InvariantCulture);
        foreach (var item in array)
        {
            if (string.Equals(NodeText(item), id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int Order(JsonNode? parameters, string key)
        => int.TryParse(Text(parameters, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var order) ? order : 0;

    private static string Text(JsonNode? parameters, string key)
        => NodeText(parameters?[key]);

    private static string NodeText(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return string.Empty;
        }

        if (value.TryGetValue<string>(out var text))
        {
            return text ?? string.Empty;
        }

        if (value.TryGetValue<int>(out var number))
        {
            return number.ToString(CultureInfo.InvariantCulture);
        }

        if (value.TryGetValue<long>(out var wide))
        {
            return wide.ToString(CultureInfo.InvariantCulture);
        }

        return value.ToString();
    }
}
