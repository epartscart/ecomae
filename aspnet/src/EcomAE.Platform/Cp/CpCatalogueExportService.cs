using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml.Linq;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Options posted by the PHP catalogue export page (<c>catalogue_xml_json_export.php</c>).</summary>
public sealed record CpCatalogueExportOptions(
    string OutputFormat,
    bool OutputProductsText,
    bool OutputProductsImages,
    bool OutputProductsSuggestions,
    IReadOnlyList<long> Offices,
    long GroupId)
{
    public bool IsJson => string.Equals(OutputFormat, "json", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Result of a catalogue export: the file written under the CP export directory.</summary>
public sealed record CpCatalogueExportResult(
    bool Succeeded,
    string Message,
    string FileName,
    int Categories,
    int Offers)
{
    public static CpCatalogueExportResult Failed(string message) => new(false, message, "", 0, 0);
}

/// <summary>
/// Native catalogue export used by the data-transfer page. Mirrors the PHP offer model of
/// <c>ajax/ajax_export_to_yml.php</c>: offers are the priced <c>shop_storages_data</c> rows of the own warehouses
/// (<c>interface_type = 1</c>) mapped to the selected offices for the selected customer group.
/// </summary>
public interface ICpCatalogueExportService
{
    Task<CpCatalogueExportResult> ExportAsync(CpCatalogueExportOptions options, CancellationToken cancellationToken = default);

    /// <summary>Absolute path of a previously exported file, or null when the name is not a known export.</summary>
    string? ResolveExportPath(string fileName);
}

public sealed class CpCatalogueExportService : ICpCatalogueExportService
{
    private const int MaxOffers = 20000;

    private readonly IErpWriteConnectionFactory _connections;
    private readonly string _exportRoot;

    public CpCatalogueExportService(IErpWriteConnectionFactory connections, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        : this(connections, Path.Combine(env.ContentRootPath, "App_Data", "data-transfer"))
    {
    }

    public CpCatalogueExportService(IErpWriteConnectionFactory connections, string exportRoot)
    {
        _connections = connections;
        _exportRoot = Path.GetFullPath(exportRoot);
    }

    public string ExportRoot => _exportRoot;

    /// <summary>PHP names the dump by format; the timestamp keeps concurrent tenants' downloads apart.</summary>
    public static string BuildFileName(CpCatalogueExportOptions options, DateTimeOffset now)
        => "catalogue_dump_" + now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + (options.IsJson ? ".json" : ".xml");

    public static CpCatalogueExportOptions ParseOptions(IReadOnlyDictionary<string, string> form)
    {
        var offices = new List<long>();
        if (form.TryGetValue("offices", out var raw))
        {
            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0 && !offices.Contains(id))
                {
                    offices.Add(id);
                }
            }
        }

        long groupId = 0;
        if (form.TryGetValue("group_id", out var group))
        {
            long.TryParse(group, NumberStyles.Integer, CultureInfo.InvariantCulture, out groupId);
        }

        return new CpCatalogueExportOptions(
            form.TryGetValue("output_format", out var format) && string.Equals(format, "json", StringComparison.OrdinalIgnoreCase) ? "json" : "xml",
            Truthy(form, "output_products_text"),
            Truthy(form, "output_products_images"),
            Truthy(form, "output_products_suggestions"),
            offices,
            groupId);
    }

    private static bool Truthy(IReadOnlyDictionary<string, string> form, string key)
        => form.TryGetValue(key, out var value)
           && (value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase));

    public string? ResolveExportPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(['/', '\\', '\0']) >= 0 || fileName.Contains(".."))
        {
            return null;
        }

        var path = Path.GetFullPath(Path.Combine(_exportRoot, fileName));
        if (!path.StartsWith(_exportRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(path))
        {
            return null;
        }

        return path;
    }

    public async Task<CpCatalogueExportResult> ExportAsync(CpCatalogueExportOptions options, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpCatalogueExportResult.Failed("No database configured — catalogue export is unavailable.");
        }

        if (options.Offices.Count == 0)
        {
            return CpCatalogueExportResult.Failed("Select at least one pickup point to export offers for.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

            var categories = await LoadCategoriesAsync(connection, cancellationToken).ConfigureAwait(false);
            var offers = new List<CatalogueOffer>();
            foreach (var officeId in options.Offices)
            {
                offers.AddRange(await LoadOffersAsync(connection, officeId, options, cancellationToken).ConfigureAwait(false));
                if (offers.Count >= MaxOffers)
                {
                    break;
                }
            }

            if (offers.Count > MaxOffers)
            {
                offers = offers.Take(MaxOffers).ToList();
            }

            Directory.CreateDirectory(_exportRoot);
            var fileName = BuildFileName(options, DateTimeOffset.UtcNow);
            var payload = options.IsJson
                ? RenderJson(categories, offers, options)
                : RenderXml(categories, offers, options);
            await File.WriteAllTextAsync(Path.Combine(_exportRoot, fileName), payload, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);

            return new CpCatalogueExportResult(
                true,
                "Catalogue exported: " + categories.Count.ToString(CultureInfo.InvariantCulture) + " categories, "
                    + offers.Count.ToString(CultureInfo.InvariantCulture) + " offers.",
                fileName,
                categories.Count,
                offers.Count);
        }
        catch (DbException ex)
        {
            return CpCatalogueExportResult.Failed("Catalogue export failed: " + ex.Message);
        }
        catch (IOException ex)
        {
            return CpCatalogueExportResult.Failed("Catalogue export file could not be written: " + ex.Message);
        }
    }

    public sealed record CatalogueCategory(long Id, long ParentId, string Caption);

    public sealed record CatalogueOffer(
        long ProductId,
        long CategoryId,
        long OfficeId,
        long StorageId,
        string Caption,
        string Alias,
        decimal Price,
        decimal Exist,
        string Text,
        string Image,
        IReadOnlyList<long> Suggestions);

    private static async Task<List<CatalogueCategory>> LoadCategoriesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var categories = new List<CatalogueCategory>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT `id`, IFNULL(`parent_id`,0), IFNULL(`caption`,'') FROM `shop_catalogue_categories` ORDER BY `level`, `order`, `id`";
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            categories.Add(new CatalogueCategory(
                Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                Convert.ToInt64(r.GetValue(1), CultureInfo.InvariantCulture),
                r.GetString(2)));
        }

        return categories;
    }

    private static async Task<List<CatalogueOffer>> LoadOffersAsync(
        DbConnection connection,
        long officeId,
        CpCatalogueExportOptions options,
        CancellationToken cancellationToken)
    {
        var offers = new List<CatalogueOffer>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = ErpDb.Positional(
                """
                SELECT `shop_catalogue_products`.`id`,
                       `shop_catalogue_products`.`category_id`,
                       IFNULL(`shop_catalogue_products`.`caption`,''),
                       IFNULL(`shop_catalogue_products`.`alias`,''),
                       `shop_storages_data`.`storage_id`,
                       IFNULL(`shop_storages_data`.`price`,0),
                       IFNULL(`shop_storages_data`.`exist`,0)
                FROM `shop_catalogue_products`
                INNER JOIN `shop_storages_data`
                    ON `shop_storages_data`.`product_id` = `shop_catalogue_products`.`id`
                   AND `shop_storages_data`.`exist` > 0
                   AND `shop_storages_data`.`price` > 0
                WHERE `shop_storages_data`.`storage_id` IN (
                    SELECT `storage_id` FROM `shop_offices_storages_map`
                    WHERE `office_id` = ? AND `group_id` = ?
                      AND `storage_id` IN (SELECT `id` FROM `shop_storages` WHERE `interface_type` = 1)
                )
                ORDER BY `shop_catalogue_products`.`id`
                LIMIT ?
                """);
            ErpDb.AddParameters(cmd, officeId, options.GroupId, MaxOffers);
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                offers.Add(new CatalogueOffer(
                    Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToInt64(r.GetValue(1), CultureInfo.InvariantCulture),
                    officeId,
                    Convert.ToInt64(r.GetValue(4), CultureInfo.InvariantCulture),
                    r.GetString(2),
                    r.GetString(3),
                    Convert.ToDecimal(r.GetValue(5), CultureInfo.InvariantCulture),
                    Convert.ToDecimal(r.GetValue(6), CultureInfo.InvariantCulture),
                    "",
                    "",
                    []));
            }
        }

        if (offers.Count == 0)
        {
            return offers;
        }

        var ids = offers.Select(o => o.ProductId).Distinct().ToList();
        var texts = options.OutputProductsText
            ? await LoadStringMapAsync(connection, "SELECT `product_id`, IFNULL(`content`,'') FROM `shop_products_text` WHERE `product_id` IN (", ids, cancellationToken).ConfigureAwait(false)
            : [];
        var images = options.OutputProductsImages
            ? await LoadStringMapAsync(connection, "SELECT `product_id`, IFNULL(`file_name`,'') FROM `shop_products_images` WHERE `product_id` IN (", ids, cancellationToken).ConfigureAwait(false)
            : [];
        var suggestions = options.OutputProductsSuggestions
            ? await LoadSuggestionsAsync(connection, ids, cancellationToken).ConfigureAwait(false)
            : [];

        for (var i = 0; i < offers.Count; i++)
        {
            var offer = offers[i];
            offers[i] = offer with
            {
                Text = texts.TryGetValue(offer.ProductId, out var text) ? text : "",
                Image = images.TryGetValue(offer.ProductId, out var image) ? image : "",
                Suggestions = suggestions.TryGetValue(offer.ProductId, out var related) ? related : []
            };
        }

        return offers;
    }

    private static async Task<Dictionary<long, string>> LoadStringMapAsync(
        DbConnection connection,
        string sqlPrefix,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<long, string>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional(sqlPrefix + string.Join(",", ids.Select(_ => "?")) + ")");
        ErpDb.AddParameters(cmd, ids.Cast<object?>().ToArray());
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture);
            if (!map.ContainsKey(id))
            {
                map[id] = r.GetString(1);
            }
        }

        return map;
    }

    private static async Task<Dictionary<long, IReadOnlyList<long>>> LoadSuggestionsAsync(
        DbConnection connection,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<long, IReadOnlyList<long>>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional(
            "SELECT `product_id`, `product_id_related` FROM `shop_related_products` WHERE `product_id` IN ("
            + string.Join(",", ids.Select(_ => "?")) + ") ORDER BY `product_id`, `order`");
        ErpDb.AddParameters(cmd, ids.Cast<object?>().ToArray());
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture);
            var related = Convert.ToInt64(r.GetValue(1), CultureInfo.InvariantCulture);
            if (!map.TryGetValue(id, out var list))
            {
                list = new List<long>();
                map[id] = list;
            }

            ((List<long>)list).Add(related);
        }

        return map;
    }

    public static string RenderXml(
        IReadOnlyList<CatalogueCategory> categories,
        IReadOnlyList<CatalogueOffer> offers,
        CpCatalogueExportOptions options)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("catalogue",
                new XAttribute("generated", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)),
                new XAttribute("group_id", options.GroupId),
                new XElement("categories", categories.Select(category =>
                    new XElement("category",
                        new XAttribute("id", category.Id),
                        new XAttribute("parentId", category.ParentId),
                        category.Caption))),
                new XElement("offers", offers.Select(offer =>
                {
                    var element = new XElement("offer",
                        new XAttribute("id", offer.ProductId),
                        new XAttribute("officeId", offer.OfficeId),
                        new XAttribute("storageId", offer.StorageId),
                        new XElement("categoryId", offer.CategoryId),
                        new XElement("name", offer.Caption),
                        new XElement("url", offer.Alias),
                        new XElement("price", offer.Price.ToString(CultureInfo.InvariantCulture)),
                        new XElement("count", offer.Exist.ToString(CultureInfo.InvariantCulture)));

                    if (options.OutputProductsText && offer.Text.Length > 0)
                    {
                        element.Add(new XElement("description", offer.Text));
                    }

                    if (options.OutputProductsImages && offer.Image.Length > 0)
                    {
                        element.Add(new XElement("picture", offer.Image));
                    }

                    if (options.OutputProductsSuggestions && offer.Suggestions.Count > 0)
                    {
                        element.Add(new XElement("suggestions", offer.Suggestions.Select(id => new XElement("suggestion", id))));
                    }

                    return element;
                }))));

        return doc.Declaration + Environment.NewLine + doc;
    }

    public static string RenderJson(
        IReadOnlyList<CatalogueCategory> categories,
        IReadOnlyList<CatalogueOffer> offers,
        CpCatalogueExportOptions options)
    {
        var payload = new
        {
            generated = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            group_id = options.GroupId,
            categories = categories.Select(category => new { id = category.Id, parent_id = category.ParentId, caption = category.Caption }),
            offers = offers.Select(offer => new
            {
                id = offer.ProductId,
                office_id = offer.OfficeId,
                storage_id = offer.StorageId,
                category_id = offer.CategoryId,
                name = offer.Caption,
                url = offer.Alias,
                price = offer.Price,
                count = offer.Exist,
                description = options.OutputProductsText ? offer.Text : null,
                picture = options.OutputProductsImages && offer.Image.Length > 0 ? offer.Image : null,
                suggestions = options.OutputProductsSuggestions ? offer.Suggestions : null
            })
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }
}
