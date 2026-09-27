using System.Data.Common;
using System.Globalization;
using System.Text;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>One CSV column mapped onto a category property (PHP input id <c>col_&lt;property_id&gt;</c>).</summary>
public sealed record CpCatalogueCsvPropertyMap(long PropertyId, int Column, bool UrlCheck);

/// <summary>
/// The <c>import_options</c> object the PHP CSV page posts to <c>ajax_handle_file.php</c>.
/// Column numbers are 1-based; <c>0</c> means "column not used".
/// </summary>
public sealed record CpCatalogueCsvImportOptions(
    long CategoryId,
    long StorageId,
    bool DeleteStorageData,
    bool DeleteProductsData,
    int StringsToSkip,
    int ColName,
    bool ColNameUrlCheck,
    int ColText,
    int ColImage,
    int ColPrice,
    int ColExist,
    string Encoding,
    IReadOnlyList<CpCatalogueCsvPropertyMap> Properties);

public sealed record CpCatalogueCsvImportResult(
    bool Succeeded,
    string Message,
    int Created,
    int Updated,
    int Skipped,
    IReadOnlyList<string> Warnings)
{
    public static CpCatalogueCsvImportResult Failed(string message) => new(false, message, 0, 0, 0, []);
}

/// <summary>
/// Native twin of the PHP CSV catalogue import
/// (<c>data_transfer/catalogue_csv_import/catalogue_csv_import.php</c> plus its
/// <c>ajax_upload_file_to_tmp.php</c> → <c>ajax_handle_file.php</c> pair, neither of which exists in this repo):
/// rows of an uploaded CSV are mapped onto one leaf category and written into one own warehouse
/// (<c>shop_storages.interface_type = 1</c>) the signed-in admin is listed on.
/// </summary>
public interface ICpCatalogueCsvImportService
{
    Task<CpCatalogueCsvImportResult> ImportAsync(
        long adminUserId,
        CpCatalogueCsvImportOptions options,
        byte[] content,
        CancellationToken cancellationToken = default);
}

public sealed class CpCatalogueCsvImportService : ICpCatalogueCsvImportService
{
    private const int MaxRows = 20000;

    /// <summary>Windows-1251 high range (0x80-0xFF); the PHP page offers ANSI or UTF-8 uploads.</summary>
    private const string Windows1251High =
        "\u0402\u0403\u201A\u0453\u201E\u2026\u2020\u2021\u20AC\u2030\u0409\u2039\u040A\u040C\u040B\u040F"
        + "\u0452\u2018\u2019\u201C\u201D\u2022\u2013\u2014\u0098\u2122\u0459\u203A\u045A\u045C\u045B\u045F"
        + "\u00A0\u040E\u045E\u0408\u00A4\u0490\u00A6\u00A7\u0401\u00A9\u0404\u00AB\u00AC\u00AD\u00AE\u0407"
        + "\u00B0\u00B1\u0406\u0456\u0491\u00B5\u00B6\u00B7\u0451\u2116\u0454\u00BB\u0458\u0405\u0455\u0457"
        + "\u0410\u0411\u0412\u0413\u0414\u0415\u0416\u0417\u0418\u0419\u041A\u041B\u041C\u041D\u041E\u041F"
        + "\u0420\u0421\u0422\u0423\u0424\u0425\u0426\u0427\u0428\u0429\u042A\u042B\u042C\u042D\u042E\u042F"
        + "\u0430\u0431\u0432\u0433\u0434\u0435\u0436\u0437\u0438\u0439\u043A\u043B\u043C\u043D\u043E\u043F"
        + "\u0440\u0441\u0442\u0443\u0444\u0445\u0446\u0447\u0448\u0449\u044A\u044B\u044C\u044D\u044E\u044F";

    private readonly IErpWriteConnectionFactory _connections;

    public CpCatalogueCsvImportService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>Reads the PHP <c>import_options</c> field names off the posted form.</summary>
    public static CpCatalogueCsvImportOptions ParseOptions(IReadOnlyDictionary<string, string> form)
    {
        var properties = new List<CpCatalogueCsvPropertyMap>();
        foreach (var pair in form)
        {
            if (!pair.Key.StartsWith("col_", StringComparison.Ordinal) || pair.Key.EndsWith("_url_check", StringComparison.Ordinal))
            {
                continue;
            }

            var suffix = pair.Key["col_".Length..];
            if (!long.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var propertyId) || propertyId <= 0)
            {
                continue;
            }

            properties.Add(new CpCatalogueCsvPropertyMap(
                propertyId,
                Int(form, pair.Key),
                Flag(form, "col_" + suffix + "_url_check")));
        }

        properties.Sort((left, right) => left.PropertyId.CompareTo(right.PropertyId));

        return new CpCatalogueCsvImportOptions(
            Long(form, "category_id"),
            form.ContainsKey("storages") ? Long(form, "storages") : Long(form, "storage_id"),
            Flag(form, "delete_storage_data"),
            Flag(form, "delete_products_data"),
            Math.Max(0, Int(form, "strings_to_left")),
            Int(form, "col_name"),
            Flag(form, "col_name_url_check"),
            Int(form, "col_text"),
            Int(form, "col_img"),
            Int(form, "col_price"),
            Int(form, "col_exist"),
            Value(form, "encoding"),
            properties);
    }

    /// <summary>The PHP page's client-side gate (<c>start_import</c>), applied server-side; null when the options are usable.</summary>
    public static string? Validate(CpCatalogueCsvImportOptions options)
    {
        if (options.CategoryId <= 0)
        {
            return "Select the category the products belong to.";
        }

        if (options.StorageId <= 0)
        {
            return "Select the warehouse to import into.";
        }

        if (options.ColName <= 0)
        {
            return "Set the CSV column number of the product name.";
        }

        if (options.ColPrice <= 0)
        {
            return "Set the CSV column number of the price.";
        }

        if (options.ColExist <= 0)
        {
            return "Set the CSV column number of the quantity.";
        }

        if (options.ColText < 0 || options.ColImage < 0)
        {
            return "CSV column numbers cannot be negative.";
        }

        foreach (var property in options.Properties)
        {
            if (property.Column < 0)
            {
                return "CSV column numbers cannot be negative.";
            }
        }

        return null;
    }

    /// <summary>Decodes the upload as the encoding chosen on the page (Windows-1251 ANSI or UTF-8).</summary>
    public static string Decode(byte[] content, string encoding)
    {
        if (!string.Equals(encoding, "windows-1251", StringComparison.OrdinalIgnoreCase))
        {
            return new UTF8Encoding(false).GetString(content).TrimStart('\uFEFF');
        }

        var text = new StringBuilder(content.Length);
        foreach (var raw in content)
        {
            text.Append(raw < 0x80 ? (char)raw : Windows1251High[raw - 0x80]);
        }

        return text.ToString();
    }

    /// <summary>Splits a CSV the way <c>fgetcsv</c> does, with the delimiter detected from the first line (<c>;</c>, <c>,</c> or tab).</summary>
    public static IReadOnlyList<IReadOnlyList<string>> ParseCsv(string text)
    {
        var rows = new List<IReadOnlyList<string>>();
        if (text.Length == 0)
        {
            return rows;
        }

        var delimiter = DetectDelimiter(text);
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch != '"')
                {
                    field.Append(ch);
                }
                else if (i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    quoted = false;
                }

                continue;
            }

            if (ch == '"' && field.Length == 0)
            {
                quoted = true;
            }
            else if (ch == delimiter)
            {
                fields.Add(field.ToString().Trim());
                field.Clear();
            }
            else if (ch is '\n' or '\r')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                fields.Add(field.ToString().Trim());
                field.Clear();
                if (fields.Count > 1 || fields[0].Length > 0)
                {
                    rows.Add(fields.ToArray());
                }

                fields.Clear();
            }
            else
            {
                field.Append(ch);
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString().Trim());
            if (fields.Count > 1 || fields[0].Length > 0)
            {
                rows.Add(fields.ToArray());
            }
        }

        return rows;
    }

    private static char DetectDelimiter(string text)
    {
        var firstLine = text.Split('\n', 2)[0];
        var semicolons = firstLine.Count(c => c == ';');
        var commas = firstLine.Count(c => c == ',');
        var tabs = firstLine.Count(c => c == '\t');
        if (tabs > semicolons && tabs > commas)
        {
            return '\t';
        }

        return commas > semicolons ? ',' : ';';
    }

    public async Task<CpCatalogueCsvImportResult> ImportAsync(
        long adminUserId,
        CpCatalogueCsvImportOptions options,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpCatalogueCsvImportResult.Failed("No database configured — CSV import is unavailable.");
        }

        var invalid = Validate(options);
        if (invalid is not null)
        {
            return CpCatalogueCsvImportResult.Failed(invalid);
        }

        var rows = ParseCsv(Decode(content, options.Encoding));
        if (rows.Count <= options.StringsToSkip)
        {
            return CpCatalogueCsvImportResult.Failed("The uploaded CSV contains no data rows.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

            if (!await StorageIsAllowedAsync(connection, options.StorageId, adminUserId, cancellationToken).ConfigureAwait(false))
            {
                return CpCatalogueCsvImportResult.Failed("This warehouse is not assigned to your account for imports.");
            }

            if (!await CategoryIsLeafAsync(connection, options.CategoryId, cancellationToken).ConfigureAwait(false))
            {
                return CpCatalogueCsvImportResult.Failed("Pick an end category — categories with sub-categories cannot hold products.");
            }

            var propertyTypes = await LoadPropertyTypesAsync(connection, options.CategoryId, cancellationToken).ConfigureAwait(false);
            var warnings = new List<string>();

            if (options.DeleteProductsData)
            {
                await DeleteCategoryProductsAsync(connection, options.CategoryId, cancellationToken).ConfigureAwait(false);
            }
            else if (options.DeleteStorageData)
            {
                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("DELETE FROM `shop_storages_data` WHERE `storage_id` = ? AND `category_id` = ?"),
                    cancellationToken, options.StorageId, options.CategoryId).ConfigureAwait(false);
            }

            var created = 0;
            var updated = 0;
            var skipped = 0;
            var handled = 0;

            for (var index = options.StringsToSkip; index < rows.Count; index++)
            {
                if (handled >= MaxRows)
                {
                    warnings.Add("Only the first " + MaxRows.ToString(CultureInfo.InvariantCulture) + " rows were imported.");
                    break;
                }

                handled++;
                var row = rows[index];
                var caption = Cell(row, options.ColName);
                if (caption.Length == 0)
                {
                    skipped++;
                    AddWarning(warnings, "Row " + (index + 1).ToString(CultureInfo.InvariantCulture) + " has no product name.");
                    continue;
                }

                var price = ParseDecimal(Cell(row, options.ColPrice));
                var exist = ParseDecimal(Cell(row, options.ColExist));
                var alias = options.ColNameUrlCheck ? Alias(caption) : caption;

                var productId = await FindProductAsync(connection, options.CategoryId, alias, caption, cancellationToken).ConfigureAwait(false);
                if (productId <= 0)
                {
                    productId = await InsertProductAsync(connection, options.CategoryId, caption, alias, cancellationToken).ConfigureAwait(false);
                    created++;
                }

                if (options.ColText > 0)
                {
                    await SaveProductTextAsync(connection, productId, Cell(row, options.ColText), cancellationToken).ConfigureAwait(false);
                }

                if (options.ColImage > 0)
                {
                    await SaveProductImageAsync(connection, productId, Cell(row, options.ColImage), cancellationToken).ConfigureAwait(false);
                }

                foreach (var property in options.Properties)
                {
                    if (property.Column <= 0)
                    {
                        continue;
                    }

                    if (!propertyTypes.TryGetValue(property.PropertyId, out var typeId))
                    {
                        AddWarning(warnings, "Property " + property.PropertyId.ToString(CultureInfo.InvariantCulture) + " does not belong to this category.");
                        continue;
                    }

                    var written = await SavePropertyValueAsync(
                        connection,
                        productId,
                        options.CategoryId,
                        property.PropertyId,
                        typeId,
                        Cell(row, property.Column),
                        cancellationToken).ConfigureAwait(false);

                    if (!written)
                    {
                        AddWarning(warnings, "Property " + property.PropertyId.ToString(CultureInfo.InvariantCulture)
                            + " is a list property — list values must be imported from the catalogue editor.");
                    }
                }

                await UpsertStorageDataAsync(connection, options, productId, price, exist, cancellationToken).ConfigureAwait(false);
                updated++;
            }

            return new CpCatalogueCsvImportResult(
                true,
                "CSV import finished: " + created.ToString(CultureInfo.InvariantCulture) + " new products, "
                    + updated.ToString(CultureInfo.InvariantCulture) + " stock rows written, "
                    + skipped.ToString(CultureInfo.InvariantCulture) + " rows skipped.",
                created,
                updated,
                skipped,
                warnings);
        }
        catch (DbException ex)
        {
            return CpCatalogueCsvImportResult.Failed("CSV import failed: " + ex.Message);
        }
    }

    private static void AddWarning(List<string> warnings, string message)
    {
        if (warnings.Count < 50)
        {
            warnings.Add(message);
        }
    }

    /// <summary>Column numbers on the PHP page are 1-based; a missing column yields an empty value.</summary>
    private static string Cell(IReadOnlyList<string> row, int column)
        => column > 0 && column <= row.Count ? row[column - 1] : string.Empty;

    private static decimal ParseDecimal(string value)
    {
        var normalized = value.Replace(" ", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
    }

    /// <summary>Product URL built from the caption, as the page's URL checkbox asks for.</summary>
    public static string Alias(string caption)
    {
        var text = new StringBuilder(caption.Length);
        var pendingSeparator = false;
        foreach (var ch in caption.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                if (pendingSeparator && text.Length > 0)
                {
                    text.Append('-');
                }

                pendingSeparator = false;
                text.Append(ch);
            }
            else
            {
                pendingSeparator = true;
            }
        }

        return text.Length > 0 ? text.ToString() : caption.Trim();
    }

    private static async Task<bool> StorageIsAllowedAsync(DbConnection connection, long storageId, long adminUserId, CancellationToken cancellationToken)
    {
        var users = await ErpDb.StringAsync(
            connection, null,
            ErpDb.Positional("SELECT IFNULL(`users`,'') FROM `shop_storages` WHERE `id` = ? AND `interface_type` = 1"),
            cancellationToken, storageId).ConfigureAwait(false);
        return users is not null && CpDataTransferDeskService.StorageAllowsUser(users, adminUserId);
    }

    private static async Task<bool> CategoryIsLeafAsync(DbConnection connection, long categoryId, CancellationToken cancellationToken)
    {
        var exists = await ErpDb.LongAsync(
            connection, null,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_catalogue_categories` WHERE `id` = ?"),
            cancellationToken, categoryId).ConfigureAwait(false);
        if (exists <= 0)
        {
            return false;
        }

        var children = await ErpDb.LongAsync(
            connection, null,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_catalogue_categories` WHERE `parent_id` = ?"),
            cancellationToken, categoryId).ConfigureAwait(false);
        return children == 0;
    }

    private static async Task<Dictionary<long, long>> LoadPropertyTypesAsync(DbConnection connection, long categoryId, CancellationToken cancellationToken)
    {
        var types = new Dictionary<long, long>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional(
            "SELECT `id`, IFNULL(`property_type_id`,0) FROM `shop_categories_properties_map` WHERE `category_id` = ?");
        ErpDb.AddParameters(cmd, categoryId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            types[Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture)] =
                Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
        }

        return types;
    }

    private static async Task DeleteCategoryProductsAsync(DbConnection connection, long categoryId, CancellationToken cancellationToken)
    {
        const string productScope = "(SELECT `id` FROM `shop_catalogue_products` WHERE `category_id` = ?)";
        foreach (var table in new[] { "shop_properties_values_int", "shop_properties_values_float", "shop_properties_values_text", "shop_properties_values_bool", "shop_properties_values_list", "shop_properties_values_tree_list" })
        {
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("DELETE FROM `" + table + "` WHERE `product_id` IN " + productScope),
                cancellationToken, categoryId).ConfigureAwait(false);
        }

        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("DELETE FROM `shop_products_text` WHERE `product_id` IN " + productScope),
            cancellationToken, categoryId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("DELETE FROM `shop_products_images` WHERE `product_id` IN " + productScope),
            cancellationToken, categoryId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("DELETE FROM `shop_storages_data` WHERE `category_id` = ?"),
            cancellationToken, categoryId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("DELETE FROM `shop_catalogue_products` WHERE `category_id` = ?"),
            cancellationToken, categoryId).ConfigureAwait(false);
    }

    private static async Task<long> FindProductAsync(DbConnection connection, long categoryId, string alias, string caption, CancellationToken cancellationToken)
        => await ErpDb.LongAsync(
            connection, null,
            ErpDb.Positional("SELECT `id` FROM `shop_catalogue_products` WHERE `category_id` = ? AND (`alias` = ? OR `caption` = ?) LIMIT 1"),
            cancellationToken, categoryId, alias, caption).ConfigureAwait(false);

    private static async Task<long> InsertProductAsync(DbConnection connection, long categoryId, string caption, string alias, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("INSERT INTO `shop_catalogue_products` (`category_id`, `caption`, `alias`, `published_flag`) VALUES (?,?,?,1)"),
            cancellationToken, categoryId, caption, alias).ConfigureAwait(false);
        return await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    private static async Task SaveProductTextAsync(DbConnection connection, long productId, string text, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("DELETE FROM `shop_products_text` WHERE `product_id` = ?"),
            cancellationToken, productId).ConfigureAwait(false);
        if (text.Length == 0)
        {
            return;
        }

        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("INSERT INTO `shop_products_text` (`product_id`, `content`) VALUES (?,?)"),
            cancellationToken, productId, text).ConfigureAwait(false);
    }

    private static async Task SaveProductImageAsync(DbConnection connection, long productId, string image, CancellationToken cancellationToken)
    {
        if (image.Length == 0)
        {
            return;
        }

        var existing = await ErpDb.LongAsync(
            connection, null,
            ErpDb.Positional("SELECT `id` FROM `shop_products_images` WHERE `product_id` = ? AND `file_name` = ? LIMIT 1"),
            cancellationToken, productId, image).ConfigureAwait(false);
        if (existing > 0)
        {
            return;
        }

        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("INSERT INTO `shop_products_images` (`product_id`, `file_name`, `order`) VALUES (?,?,0)"),
            cancellationToken, productId, image).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes one property value into the typed table PHP <c>product.php</c> uses
    /// (1 int, 2 float, 3 text through a translation string, 4 bool); list types (5/6) are reported instead.
    /// </summary>
    private static async Task<bool> SavePropertyValueAsync(
        DbConnection connection,
        long productId,
        long categoryId,
        long propertyId,
        long propertyTypeId,
        string value,
        CancellationToken cancellationToken)
    {
        var table = propertyTypeId switch
        {
            1 => "shop_properties_values_int",
            2 => "shop_properties_values_float",
            3 => "shop_properties_values_text",
            4 => "shop_properties_values_bool",
            _ => ""
        };

        if (table.Length == 0)
        {
            return false;
        }

        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("DELETE FROM `" + table + "` WHERE `product_id` = ? AND `property_id` = ?"),
            cancellationToken, productId, propertyId).ConfigureAwait(false);

        if (value.Length == 0)
        {
            return true;
        }

        object stored = propertyTypeId switch
        {
            1 => (long)ParseDecimal(value),
            2 => ParseDecimal(value),
            4 => value is "1" or "true" or "yes" ? 1L : 0L,
            _ => await CpReturnWriteService.EnsureLangStringAsync(
                connection,
                "epc_csv_prop_" + propertyId.ToString(CultureInfo.InvariantCulture) + "_" + productId.ToString(CultureInfo.InvariantCulture),
                value,
                cancellationToken).ConfigureAwait(false)
        };

        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("INSERT INTO `" + table + "` (`product_id`, `property_id`, `category_id`, `value`) VALUES (?,?,?,?)"),
            cancellationToken, productId, propertyId, categoryId, stored).ConfigureAwait(false);
        return true;
    }

    private static async Task UpsertStorageDataAsync(
        DbConnection connection,
        CpCatalogueCsvImportOptions options,
        long productId,
        decimal price,
        decimal exist,
        CancellationToken cancellationToken)
    {
        var existing = await ErpDb.LongAsync(
            connection, null,
            ErpDb.Positional("SELECT `id` FROM `shop_storages_data` WHERE `storage_id` = ? AND `product_id` = ? LIMIT 1"),
            cancellationToken, options.StorageId, productId).ConfigureAwait(false);
        if (existing > 0)
        {
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `shop_storages_data` SET `price` = ?, `exist` = ? WHERE `id` = ?"),
                cancellationToken, price, exist, existing).ConfigureAwait(false);
            return;
        }

        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("INSERT INTO `shop_storages_data` (`storage_id`, `product_id`, `category_id`, `price`, `exist`) VALUES (?,?,?,?,?)"),
            cancellationToken, options.StorageId, productId, options.CategoryId, price, exist).ConfigureAwait(false);
    }

    private static string Value(IReadOnlyDictionary<string, string> form, string key)
        => form.TryGetValue(key, out var value) ? value.Trim() : string.Empty;

    private static bool Flag(IReadOnlyDictionary<string, string> form, string key)
        => Value(form, key) is "1" or "true" or "True" or "on" or "yes";

    private static int Int(IReadOnlyDictionary<string, string> form, string key)
        => int.TryParse(Value(form, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static long Long(IReadOnlyDictionary<string, string> form, string key)
        => long.TryParse(Value(form, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
