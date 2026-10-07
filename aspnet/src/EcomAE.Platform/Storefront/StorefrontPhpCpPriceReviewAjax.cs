using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Price review repricing and its review CSV export run dry: the PHP gates and argument checks hold,
/// the batch is evaluated, and no <c>shop_docpart_prices_data</c> row or tmp file is written.
/// </summary>
public static partial class StorefrontPhpAjax
{
    public const string PriceReviewPath = "/cp/content/shop/prices_upload/price_review/ajax_price_review.php";
    public const string PriceReviewCsvPath = "/cp/content/shop/prices_upload/price_review/ajax_create_csv.php";
    public const string PriceReviewDryRun = "Price review runs dry in ASP.NET: no prices were changed.";
    public const string PriceReviewCsvDryRun = "Review CSV export runs dry in ASP.NET: no file was written.";

    public sealed record PriceReviewBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("dry_run")] bool DryRun,
        [property: JsonPropertyName("items")] int Items,
        [property: JsonPropertyName("would_review")] int WouldReview);

    public sealed record PriceReviewCsvBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("dry_run")] bool DryRun,
        [property: JsonPropertyName("type")] int Type,
        [property: JsonPropertyName("rows")] long Rows);

    private static readonly string[] PriceReviewFields = ["price_id", "start", "base_mark", "plus_minus", "percent", "prices", "from", "items_per_time", "end"];

    public static async Task<object> PriceReviewAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        IReadOnlyDictionary<string, string> posted,
        CancellationToken cancellationToken)
    {
        var denied = await PriceReviewGateAsync(connection, adminSession, adminUser, posted, cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        if (PriceReviewFields.Any(field => !posted.ContainsKey(field)))
        {
            return string.Empty;
        }

        var function = posted["base_mark"] switch
        {
            "min" => "MIN",
            "max" => "MAX",
            "middle" => "AVG",
            _ => null,
        };
        if (function is null)
        {
            return string.Empty;
        }

        var prices = PriceReviewIds(posted["prices"]);
        var percent = PriceReviewNumber(posted["percent"]);
        var plusMinus = posted["plus_minus"];
        var items = 0;
        var wouldReview = 0;
        try
        {
            var batch = new List<(string Article, string Manufacturer)>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = ErpDb.Positional("SELECT `article`, `manufacturer` FROM `shop_docpart_prices_data` WHERE `price_id` = ? ORDER BY `id` LIMIT ? OFFSET ?");
                ErpDb.AddParameters(command, PhpInt(posted["price_id"]), Math.Max(0, PhpInt(posted["items_per_time"])), Math.Max(0, PhpInt(posted["from"])));
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    batch.Add((reader.IsDBNull(0) ? string.Empty : reader.GetValue(0).ToString() ?? string.Empty, reader.IsDBNull(1) ? string.Empty : reader.GetValue(1).ToString() ?? string.Empty));
                }
            }

            foreach (var (article, manufacturer) in batch)
            {
                items++;
                if (prices.Count == 0)
                {
                    continue;
                }

                var manufacturers = await PriceReviewManufacturersAsync(connection, manufacturer, cancellationToken).ConfigureAwait(false);
                var sql = "SELECT " + function + "(`price`) FROM `shop_docpart_prices_data` WHERE `price_id` IN ("
                    + string.Join(',', prices.Select(_ => "?")) + ") AND `article` = ? AND `manufacturer` IN ("
                    + string.Join(',', manufacturers.Select(_ => "?")) + ") AND `price` > ?";
                var parameters = prices.Cast<object?>().Append(article).Concat(manufacturers).Append(0).ToArray();
                var raw = await ErpDb.ScalarAsync(connection, null, ErpDb.Positional(sql), cancellationToken, parameters).ConfigureAwait(false);
                if (raw is null or DBNull)
                {
                    continue;
                }

                var price = Convert.ToDecimal(raw, CultureInfo.InvariantCulture);
                if (percent > 0)
                {
                    var delta = price * percent / 100m;
                    price = plusMinus switch
                    {
                        "plus" => price + delta,
                        "minus" => price - delta,
                        _ => 0m,
                    };
                }

                if (price > 0)
                {
                    wouldReview++;
                }
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, "Price rows are not in this database.");
        }

        return new PriceReviewBody(false, PriceReviewDryRun, true, items, wouldReview);
    }

    public static async Task<object> PriceReviewCsvAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        IReadOnlyDictionary<string, string> posted,
        CancellationToken cancellationToken)
    {
        var denied = await PriceReviewGateAsync(connection, adminSession, adminUser, posted, cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        if (!posted.TryGetValue("price_id", out var priceId) || !posted.TryGetValue("type", out var rawType))
        {
            return string.Empty;
        }

        var reviewed = rawType switch
        {
            "1" => string.Empty,
            "2" => " AND `reviewed` = 1",
            "3" => " AND `reviewed` = 0",
            _ => null,
        };
        if (reviewed is null)
        {
            return string.Empty;
        }

        try
        {
            var rows = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` = ?" + reviewed),
                cancellationToken,
                PhpInt(priceId)).ConfigureAwait(false);
            return new PriceReviewCsvBody(false, PriceReviewCsvDryRun, true, PhpInt(rawType), rows);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, "Price rows are not in this database.");
        }
    }

    private static async Task<object?> PriceReviewGateAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        IReadOnlyDictionary<string, string> posted,
        CancellationToken cancellationToken)
    {
        var csrf = await ReadCsrfAsync(
            connection,
            adminSession,
            posted.TryGetValue("csrf_guard_key", out var key) ? key : null,
            cancellationToken).ConfigureAwait(false);
        if (!csrf.Ok)
        {
            return CsrfFailure(csrf.Message);
        }

        return await StaffAsync(connection, adminSession, adminUser, new FlagBody(false, "Forbidden"), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The item's manufacturer plus every synonym of its canonical manufacturer, as PHP collects them.</summary>
    private static async Task<List<string>> PriceReviewManufacturersAsync(DbConnection connection, string manufacturer, CancellationToken cancellationToken)
    {
        var names = new List<string> { manufacturer };
        var id = await ErpDb.ScalarAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_docpart_manufacturers` WHERE `name` = ? LIMIT 1"),
            cancellationToken,
            manufacturer).ConfigureAwait(false);
        if (id is null or DBNull)
        {
            id = await ErpDb.ScalarAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `manufacturer_id` FROM `shop_docpart_manufacturers_synonyms` WHERE `synonym` = ? LIMIT 1"),
                cancellationToken,
                manufacturer).ConfigureAwait(false);
        }

        if (id is null or DBNull)
        {
            return names;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT `synonym` FROM `shop_docpart_manufacturers_synonyms` WHERE `manufacturer_id` = ?");
        ErpDb.AddParameters(command, id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            names.Add(reader.IsDBNull(0) ? string.Empty : reader.GetValue(0).ToString() ?? string.Empty);
        }

        return names;
    }

    private static List<int> PriceReviewIds(string raw)
    {
        try
        {
            using var document = JsonDocument.Parse(raw);
            var ids = new List<int>();
            var values = document.RootElement.ValueKind switch
            {
                JsonValueKind.Array => document.RootElement.EnumerateArray().ToList(),
                JsonValueKind.Object => document.RootElement.EnumerateObject().Select(pair => pair.Value).ToList(),
                _ => [],
            };
            foreach (var value in values)
            {
                ids.Add(PhpInt(value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText()));
            }

            return ids;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static decimal PriceReviewNumber(string raw)
        => decimal.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0m;
}
