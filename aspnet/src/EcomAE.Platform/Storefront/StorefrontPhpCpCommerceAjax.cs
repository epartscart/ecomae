using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string CommerceChooseFile = "Choose an Excel/CSV file, or paste a file URL";
    public const string CommerceFileStaysClassic = "File ingest stays on the classic importer.";
    public const string CommerceUrlStaysClassic = "URL refresh stays on the classic importer.";
    public const string CommercePricesMissing = "Price lists are not in this database.";

    private static readonly Regex CommercePurchaseName = new(@"\.P$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex CommerceInventoryName = new(@"-L$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex CommerceSalesName = new(@"-S$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex CommerceHttpLink = new(@"^https?://", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static Task<object> CommerceIngestAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken)
        => WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            () => new FlagBody(false, "Forbidden"),
            (_, token) => CommerceBodyAsync(connection, fields, token),
            cancellationToken);

    private static async Task<object> CommerceBodyAsync(
        DbConnection connection,
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken)
    {
        var action = OmsField(fields, "action").Trim().ToLowerInvariant();
        if (action.Length == 0)
        {
            action = "upload";
        }

        if (action == "list_sources")
        {
            var sources = await CommerceSourcesAsync(connection, OmsField(fields, "url_only") == "1", cancellationToken).ConfigureAwait(false);
            return new JsonObject
            {
                ["status"] = true,
                ["count"] = sources.Count,
                ["sources"] = sources,
            };
        }

        if (action == "refresh_all")
        {
            return await CommerceRefreshAllAsync(connection, cancellationToken).ConfigureAwait(false);
        }

        if (action == "refresh_url")
        {
            return await CommerceRefreshOneAsync(connection, ParseId(OmsField(fields, "price_id")), cancellationToken).ConfigureAwait(false);
        }

        var sourceUrl = OmsField(fields, "source_url").Trim();
        if (!fields.ContainsKey("price_file") && sourceUrl.Length == 0)
        {
            return new FlagBody(false, CommerceChooseFile);
        }

        return new FlagBody(false, CommerceFileStaysClassic);
    }

    private static async Task<JsonObject> CommerceRefreshAllAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var sources = await CommerceSourcesAsync(connection, true, cancellationToken).ConfigureAwait(false);
        if (sources.Count == 0)
        {
            return new JsonObject
            {
                ["status"] = true,
                ["message"] = "No commerce URL-linked lists found",
                ["ok"] = 0,
                ["failed"] = 0,
                ["total"] = 0,
                ["results"] = new JsonArray(),
                ["action"] = "refresh_all",
            };
        }

        var results = new JsonArray();
        foreach (var source in sources)
        {
            var row = source as JsonObject;
            results.Add(new JsonObject
            {
                ["price_id"] = row?["price_id"]?.GetValue<int>() ?? 0,
                ["price_name"] = JsonText(row?["price_name"]),
                ["status"] = false,
                ["message"] = CommerceUrlStaysClassic,
                ["source_rows"] = 0,
            });
        }

        return new JsonObject
        {
            ["status"] = false,
            ["message"] = CommerceUrlStaysClassic,
            ["ok"] = 0,
            ["failed"] = sources.Count,
            ["total"] = sources.Count,
            ["results"] = results,
            ["action"] = "refresh_all",
        };
    }

    private static async Task<object> CommerceRefreshOneAsync(DbConnection connection, int priceId, CancellationToken cancellationToken)
    {
        if (priceId <= 0)
        {
            return new FlagBody(false, "price_id required");
        }

        string name;
        string link;
        string header;
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT `name`, IFNULL(`link`, ''), IFNULL(`message_header_substring`, '') FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1");
            ErpDb.AddParameters(command, priceId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return new FlagBody(false, "Price list not found");
            }

            name = reader.IsDBNull(0) ? string.Empty : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty;
            link = reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty;
            header = reader.IsDBNull(2) ? string.Empty : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty;
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, CommercePricesMissing);
        }

        if (!CommerceHttpLink.IsMatch(link.Trim()))
        {
            return new JsonObject
            {
                ["status"] = false,
                ["message"] = "No http(s) link on this price list",
                ["price_id"] = priceId,
            };
        }

        var role = CommerceRole(name, header);
        if (role.Length == 0)
        {
            return new JsonObject
            {
                ["status"] = false,
                ["message"] = "Cannot detect commerce role from list name " + name,
                ["price_id"] = priceId,
            };
        }

        return new JsonObject
        {
            ["status"] = false,
            ["message"] = CommerceUrlStaysClassic,
            ["price_id"] = priceId,
            ["price_name"] = name,
        };
    }

    private static async Task<JsonArray> CommerceSourcesAsync(DbConnection connection, bool urlOnly, CancellationToken cancellationToken)
    {
        const string filter = """
            WHERE (`name` LIKE '%-S' OR `name` LIKE '%.P' OR `name` LIKE '%-L'
                OR `message_header_substring` LIKE 'EPC_COMMERCE:%')
            """;
        var url = urlOnly ? " AND `load_mode` = 4 AND `link` LIKE 'http%'" : string.Empty;
        var withCount = "SELECT `id`, `name`, `link`, `load_mode`, `message_header_substring`, `last_updated`, `records_count` FROM `shop_docpart_prices` " + filter + url + " ORDER BY `name` ASC";
        var withoutCount = "SELECT `id`, `name`, `link`, `load_mode`, `message_header_substring`, `last_updated` FROM `shop_docpart_prices` " + filter + url + " ORDER BY `name` ASC";
        try
        {
            return await ReadCommerceSourcesAsync(connection, withCount, true, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            try
            {
                return await ReadCommerceSourcesAsync(connection, withoutCount, false, cancellationToken).ConfigureAwait(false);
            }
            catch (DbException retry) when (CpMissingSchema.IsMissing(retry))
            {
                return new JsonArray();
            }
        }
    }

    private static async Task<JsonArray> ReadCommerceSourcesAsync(
        DbConnection connection,
        string sql,
        bool hasCount,
        CancellationToken cancellationToken)
    {
        var sources = new JsonArray();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var name = reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty;
            var link = reader.IsDBNull(2) ? string.Empty : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty;
            var header = reader.IsDBNull(4) ? string.Empty : Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? string.Empty;
            var records = 0;
            if (hasCount && !reader.IsDBNull(6))
            {
                records = Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture);
            }

            sources.Add(new JsonObject
            {
                ["price_id"] = Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                ["price_name"] = name,
                ["role"] = CommerceRole(name, header),
                ["base_name"] = CommerceBase(name, header),
                ["margin_percent"] = CommerceMargin(header),
                ["link"] = link,
                ["load_mode"] = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
                ["last_updated"] = reader.IsDBNull(5) ? 0 : Convert.ToInt64(reader.GetValue(5), CultureInfo.InvariantCulture),
                ["records_count"] = records,
                ["has_url"] = CommerceHttpLink.IsMatch(link.Trim()),
            });
        }

        return sources;
    }

    private static string CommerceRole(string name, string header)
    {
        if (TryCommerceMeta(header, out var role, out _, out _) && role.Length > 0)
        {
            return role;
        }

        if (CommercePurchaseName.IsMatch(name))
        {
            return "purchase";
        }

        if (CommerceInventoryName.IsMatch(name))
        {
            return "inventory";
        }

        if (CommerceSalesName.IsMatch(name))
        {
            return "sales";
        }

        return string.Empty;
    }

    private static string CommerceBase(string name, string header)
    {
        if (TryCommerceMeta(header, out var role, out var baseName, out _) && baseName.Length > 0)
        {
            return baseName;
        }

        var detected = role.Length > 0 ? role : CommerceRole(name, string.Empty);
        if (detected == "purchase")
        {
            return CommercePurchaseName.Replace(name, string.Empty);
        }

        if (detected == "inventory")
        {
            return CommerceInventoryName.Replace(name, string.Empty);
        }

        if (detected == "sales")
        {
            return CommerceSalesName.Replace(name, string.Empty);
        }

        return name;
    }

    private static double CommerceMargin(string header)
        => TryCommerceMeta(header, out _, out _, out var margin) ? margin : 0;

    private static bool TryCommerceMeta(string raw, out string role, out string baseName, out double margin)
    {
        role = string.Empty;
        baseName = string.Empty;
        margin = 0;
        raw = raw.Trim();
        const string prefix = "EPC_COMMERCE:";
        if (!raw.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            if (JsonNode.Parse(raw[prefix.Length..]) is not JsonObject node)
            {
                return false;
            }

            role = JsonText(node["role"]).ToLowerInvariant();
            baseName = JsonText(node["base"]);
            margin = JsonDouble(node["margin"]);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string JsonText(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return text ?? string.Empty;
        }

        return string.Empty;
    }

    private static double JsonDouble(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<double>(out var number))
            {
                return number;
            }

            if (value.TryGetValue<int>(out var whole))
            {
                return whole;
            }
        }

        return 0;
    }
}
