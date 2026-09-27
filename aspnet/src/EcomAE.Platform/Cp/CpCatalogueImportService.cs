using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Clear mode of the PHP import page: 0 keep, 1 clear storage data, 2 clear the whole catalogue.</summary>
public enum CpCatalogueClearMode
{
    None = 0,
    StorageData = 1,
    Catalogue = 2
}

/// <summary>One offer read from an uploaded XML/JSON catalogue dump.</summary>
public sealed record CpCatalogueImportOffer(
    long CategoryId,
    string Caption,
    string Alias,
    decimal Price,
    decimal Exist);

public sealed record CpCatalogueImportResult(
    bool Succeeded,
    string Message,
    int Created,
    int Updated,
    int Skipped,
    IReadOnlyList<string> Warnings)
{
    public static CpCatalogueImportResult Failed(string message) => new(false, message, 0, 0, 0, []);
}

/// <summary>
/// Native twin of the PHP catalogue XML/JSON import (<c>pages/catalogue_xml_json_import.php</c> +
/// <c>ajax_xml_reader.php</c>): an uploaded dump is written into one own warehouse
/// (<c>shop_storages.interface_type = 1</c>) the signed-in admin is listed on, with the PHP clear options.
/// </summary>
public interface ICpCatalogueImportService
{
    Task<CpCatalogueImportResult> ImportAsync(
        long adminUserId,
        long storageId,
        CpCatalogueClearMode clearMode,
        string payload,
        CancellationToken cancellationToken = default);
}

public sealed class CpCatalogueImportService : ICpCatalogueImportService
{
    private const int MaxOffers = 20000;

    private readonly IErpWriteConnectionFactory _connections;

    public CpCatalogueImportService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static CpCatalogueClearMode ParseClearMode(string? value)
        => value switch
        {
            "1" => CpCatalogueClearMode.StorageData,
            "2" => CpCatalogueClearMode.Catalogue,
            _ => CpCatalogueClearMode.None
        };

    /// <summary>Reads the dump produced by the export page; XML and JSON carry the same offer fields.</summary>
    public static IReadOnlyList<CpCatalogueImportOffer> ParsePayload(string payload)
    {
        var text = (payload ?? string.Empty).TrimStart('\uFEFF', ' ', '\r', '\n', '\t');
        if (text.Length == 0)
        {
            return [];
        }

        return text[0] == '<' ? ParseXml(text) : ParseJson(text);
    }

    private static IReadOnlyList<CpCatalogueImportOffer> ParseXml(string text)
    {
        var offers = new List<CpCatalogueImportOffer>();
        XDocument doc;
        try
        {
            doc = XDocument.Parse(text, LoadOptions.None);
        }
        catch (System.Xml.XmlException)
        {
            return [];
        }

        foreach (var offer in doc.Descendants("offer"))
        {
            offers.Add(new CpCatalogueImportOffer(
                ParseLong(offer.Element("categoryId")?.Value),
                (offer.Element("name")?.Value ?? string.Empty).Trim(),
                (offer.Element("url")?.Value ?? string.Empty).Trim(),
                ParseDecimal(offer.Element("price")?.Value),
                ParseDecimal(offer.Element("count")?.Value)));
        }

        return offers;
    }

    private static IReadOnlyList<CpCatalogueImportOffer> ParseJson(string text)
    {
        var offers = new List<CpCatalogueImportOffer>();
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("offers", out var array) || array.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            foreach (var element in array.EnumerateArray())
            {
                offers.Add(new CpCatalogueImportOffer(
                    ParseLong(Text(element, "category_id")),
                    Text(element, "name").Trim(),
                    Text(element, "url").Trim(),
                    ParseDecimal(Text(element, "price")),
                    ParseDecimal(Text(element, "count"))));
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return offers;
    }

    private static string Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            _ => string.Empty
        };
    }

    private static long ParseLong(string? value)
        => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static decimal ParseDecimal(string? value)
        => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;

    public async Task<CpCatalogueImportResult> ImportAsync(
        long adminUserId,
        long storageId,
        CpCatalogueClearMode clearMode,
        string payload,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpCatalogueImportResult.Failed("No database configured — catalogue import is unavailable.");
        }

        if (storageId <= 0)
        {
            return CpCatalogueImportResult.Failed("Select the warehouse to import into.");
        }

        var offers = ParsePayload(payload);
        if (offers.Count == 0)
        {
            return CpCatalogueImportResult.Failed("The uploaded file contains no catalogue offers.");
        }

        if (offers.Count > MaxOffers)
        {
            offers = offers.Take(MaxOffers).ToList();
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

            if (!await StorageIsAllowedAsync(connection, storageId, adminUserId, cancellationToken).ConfigureAwait(false))
            {
                return CpCatalogueImportResult.Failed("This warehouse is not assigned to your account for imports.");
            }

            var warnings = new List<string>();
            var created = 0;
            var updated = 0;
            var skipped = 0;

            if (clearMode == CpCatalogueClearMode.StorageData)
            {
                await ExecuteAsync(connection, "DELETE FROM `shop_storages_data` WHERE `storage_id` = ?", cancellationToken, storageId).ConfigureAwait(false);
            }
            else if (clearMode == CpCatalogueClearMode.Catalogue)
            {
                await ExecuteAsync(connection, "DELETE FROM `shop_storages_data` WHERE `storage_id` = ?", cancellationToken, storageId).ConfigureAwait(false);
                await ExecuteAsync(connection, "DELETE FROM `shop_catalogue_products` WHERE `id` > 0", cancellationToken).ConfigureAwait(false);
            }

            foreach (var offer in offers)
            {
                if (offer.CategoryId <= 0 || offer.Caption.Length == 0)
                {
                    skipped++;
                    continue;
                }

                var productId = await FindProductAsync(connection, offer, cancellationToken).ConfigureAwait(false);
                if (productId <= 0)
                {
                    if (!await CategoryExistsAsync(connection, offer.CategoryId, cancellationToken).ConfigureAwait(false))
                    {
                        skipped++;
                        if (warnings.Count < 20)
                        {
                            warnings.Add("Category " + offer.CategoryId.ToString(CultureInfo.InvariantCulture) + " is missing for “" + offer.Caption + "”.");
                        }

                        continue;
                    }

                    productId = await InsertProductAsync(connection, offer, cancellationToken).ConfigureAwait(false);
                    created++;
                }

                await UpsertStorageDataAsync(connection, storageId, productId, offer, cancellationToken).ConfigureAwait(false);
                updated++;
            }

            return new CpCatalogueImportResult(
                true,
                "Import finished: " + created.ToString(CultureInfo.InvariantCulture) + " new products, "
                    + updated.ToString(CultureInfo.InvariantCulture) + " stock rows written, "
                    + skipped.ToString(CultureInfo.InvariantCulture) + " skipped.",
                created,
                updated,
                skipped,
                warnings);
        }
        catch (DbException ex)
        {
            return CpCatalogueImportResult.Failed("Catalogue import failed: " + ex.Message);
        }
    }

    private static async Task<bool> StorageIsAllowedAsync(DbConnection connection, long storageId, long adminUserId, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional("SELECT IFNULL(`users`,'') FROM `shop_storages` WHERE `id` = ? AND `interface_type` = 1");
        ErpDb.AddParameters(cmd, storageId);
        var users = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return users is string json && CpDataTransferDeskService.StorageAllowsUser(json, adminUserId);
    }

    private static async Task<bool> CategoryExistsAsync(DbConnection connection, long categoryId, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional("SELECT `id` FROM `shop_catalogue_categories` WHERE `id` = ?");
        ErpDb.AddParameters(cmd, categoryId);
        return await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    private static async Task<long> FindProductAsync(DbConnection connection, CpCatalogueImportOffer offer, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional(
            "SELECT `id` FROM `shop_catalogue_products` WHERE `category_id` = ? AND (`alias` = ? OR `caption` = ?) LIMIT 1");
        ErpDb.AddParameters(cmd, offer.CategoryId, offer.Alias, offer.Caption);
        var found = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return found is null || found is DBNull ? 0 : Convert.ToInt64(found, CultureInfo.InvariantCulture);
    }

    private static async Task<long> InsertProductAsync(DbConnection connection, CpCatalogueImportOffer offer, CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            connection,
            "INSERT INTO `shop_catalogue_products` (`category_id`, `caption`, `alias`, `published_flag`) VALUES (?,?,?,1)",
            cancellationToken,
            offer.CategoryId,
            offer.Caption,
            offer.Alias.Length > 0 ? offer.Alias : offer.Caption).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT LAST_INSERT_ID()";
        var id = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return id is null || id is DBNull ? 0 : Convert.ToInt64(id, CultureInfo.InvariantCulture);
    }

    private static async Task UpsertStorageDataAsync(
        DbConnection connection,
        long storageId,
        long productId,
        CpCatalogueImportOffer offer,
        CancellationToken cancellationToken)
    {
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = ErpDb.Positional("SELECT `id` FROM `shop_storages_data` WHERE `storage_id` = ? AND `product_id` = ? LIMIT 1");
            ErpDb.AddParameters(cmd, storageId, productId);
            var existing = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (existing is not null && existing is not DBNull)
            {
                await ExecuteAsync(
                    connection,
                    "UPDATE `shop_storages_data` SET `price` = ?, `exist` = ? WHERE `id` = ?",
                    cancellationToken,
                    offer.Price,
                    offer.Exist,
                    Convert.ToInt64(existing, CultureInfo.InvariantCulture)).ConfigureAwait(false);
                return;
            }
        }

        await ExecuteAsync(
            connection,
            "INSERT INTO `shop_storages_data` (`storage_id`, `product_id`, `category_id`, `price`, `exist`) VALUES (?,?,?,?,?)",
            cancellationToken,
            storageId,
            productId,
            offer.CategoryId,
            offer.Price,
            offer.Exist).ConfigureAwait(false);
    }

    private static async Task ExecuteAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object?[] parameters)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(cmd, parameters);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
