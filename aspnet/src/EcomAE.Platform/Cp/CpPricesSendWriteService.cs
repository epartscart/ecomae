using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>prices_send/ajax_operations.php</c> + <c>prices_send_helper.php</c> writes: storage links, CSV generation, mailing.</summary>
public interface ICpPricesSendWriteService
{
    Task<CpPricesSendAnswer> CheckOfficeStoragesMapAsync(CpPricesSendRequest request, CancellationToken cancellationToken = default);

    Task<CpPricesSendAnswer> EnsureOfficeStorageLinksAsync(CpPricesSendRequest request, CancellationToken cancellationToken = default);

    Task<CpPricesSendAnswer> CreatePricesAsync(CpPricesSendRequest request, CancellationToken cancellationToken = default);

    Task<CpPricesSendAnswer> SendPricesAsync(CpPricesSendRequest request, CancellationToken cancellationToken = default);
}

public sealed class CpPricesSendWriteService : ICpPricesSendWriteService
{
    /// <summary>PHP: common customer markup profiles + guests.</summary>
    public static readonly int[] DefaultLinkGroups = [2, 4, 5, 6, 7];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly ICpTenantEmailWriteService _email;
    private readonly string _filesRoot;

    public CpPricesSendWriteService(IErpWriteConnectionFactory connections, ICpTenantEmailWriteService email, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        : this(connections, email, Path.Combine(Presentation.PhpLegacyAssetBridge.FindRepoRoot(env), "content", "files"))
    {
    }

    public CpPricesSendWriteService(IErpWriteConnectionFactory connections, ICpTenantEmailWriteService email, string filesRoot)
    {
        _connections = connections;
        _email = email;
        _filesRoot = Path.GetFullPath(filesRoot);
    }

    public string FilesRoot => _filesRoot;

    /// <summary>Absolute path of the prices_tmp directory under the authorised content/files root.</summary>
    public string PricesTmpDir => Path.Combine(_filesRoot, "Documents", "prices_tmp");

    /// <summary>Resolves a generated filename inside prices_tmp; null when it would escape the directory.</summary>
    public string? SafeFilePath(string fileName)
    {
        if (!CpPricesSendCsv.IsSafeStem(Path.GetFileNameWithoutExtension(fileName)) || !fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var dir = Path.GetFullPath(PricesTmpDir);
        var full = Path.GetFullPath(Path.Combine(dir, fileName));
        return full.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? full : null;
    }

    public async Task<CpPricesSendAnswer> CheckOfficeStoragesMapAsync(CpPricesSendRequest request, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpPricesSendAnswer.Fail("db", "No database");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var notLinked = new List<string>();
            foreach (var storageId in request.StorageIds)
            {
                var count = await ErpDb.LongAsync(
                    connection, null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ?"),
                    cancellationToken, request.OfficeId, storageId).ConfigureAwait(false);
                if (count == 0)
                {
                    var name = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `name` FROM `shop_storages` WHERE `id` = ?"), cancellationToken, storageId).ConfigureAwait(false);
                    notLinked.Add(string.IsNullOrEmpty(name) ? "ID " + storageId.ToString(CultureInfo.InvariantCulture) : name);
                }
            }

            return notLinked.Count == 0
                ? CpPricesSendAnswer.Ok()
                : new CpPricesSendAnswer(false, string.Join(", ", notLinked), "not_linked", CanLink: true);
        }
        catch (DbException ex)
        {
            return CpPricesSendAnswer.Fail("db", ex.Message);
        }
    }

    public async Task<CpPricesSendAnswer> EnsureOfficeStorageLinksAsync(CpPricesSendRequest request, CancellationToken cancellationToken = default)
    {
        if (request.OfficeId < 1 || request.StorageIds.Count == 0)
        {
            return CpPricesSendAnswer.Fail("invalid", "Select shop and storages");
        }

        if (!_connections.IsConfigured)
        {
            return CpPricesSendAnswer.Fail("db", "No database");
        }

        var groups = request.GroupIds.Where(g => g > 0).Distinct().ToArray();
        if (groups.Length == 0)
        {
            groups = DefaultLinkGroups;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var linked = 0;
            foreach (var sid in request.StorageIds.Where(s => s > 0).Distinct())
            {
                foreach (var gid in groups)
                {
                    linked += await ErpDb.ExecuteAsync(
                        connection, tx,
                        ErpDb.Positional(
                            "INSERT INTO `shop_offices_storages_map` (`office_id`, `storage_id`, `group_id`, `min_point`, `max_point`, `markup`, `additional_time`) "
                            + "SELECT ?, ?, ?, 0, 999999999, 0, 0 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM `shop_offices_storages_map` "
                            + "WHERE `office_id` = ? AND `storage_id` = ? AND `group_id` = ? AND `min_point` = 0 AND `max_point` = 999999999)"),
                        cancellationToken, request.OfficeId, sid, gid, request.OfficeId, sid, gid).ConfigureAwait(false);
                }
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new CpPricesSendAnswer(true, "Linked " + linked.ToString(CultureInfo.InvariantCulture) + " markup map row(s)", "ok", Linked: linked);
        }
        catch (DbException ex)
        {
            return CpPricesSendAnswer.Fail("db", ex.Message);
        }
    }

    /// <summary>PHP group derivation: users' groups ∪ email-list group ∪ profile groups (deduplicated, in that order).</summary>
    public static IReadOnlyList<int> MergeGroups(IEnumerable<int> userGroups, int emailListGroup, IEnumerable<int> profileGroups)
    {
        var groups = new List<int>();
        foreach (var g in userGroups)
        {
            if (!groups.Contains(g)) groups.Add(g);
        }

        if (emailListGroup != 0 && !groups.Contains(emailListGroup))
        {
            groups.Add(emailListGroup);
        }

        foreach (var g in profileGroups.Where(g => g > 0))
        {
            if (!groups.Contains(g)) groups.Add(g);
        }

        return groups;
    }

    private sealed record ShopConfig(string ShopCurrency, int PriceRounding, string ProductUrlMode, string DomainPath);

    private static async Task<ShopConfig> LoadConfigAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        async Task<string> Item(string name)
            => await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `value` FROM `config_items` WHERE `name` = ? LIMIT 1"), cancellationToken, name).ConfigureAwait(false) ?? string.Empty;

        var rounding = int.TryParse(await Item("price_rounding").ConfigureAwait(false), NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) ? r : 0;
        var domain = (await Item("domain_path").ConfigureAwait(false)).Trim();
        if (domain.Length > 0 && !domain.EndsWith('/'))
        {
            domain += "/";
        }

        return new ShopConfig(
            (await Item("shop_currency").ConfigureAwait(false)).Trim(),
            rounding,
            (await Item("product_url").ConfigureAwait(false)).Trim(),
            domain);
    }

    private sealed record StorageRecord(int Id, int InterfaceType, string Currency, string ConnectionOptions);

    private static async Task<StorageRecord?> LoadStorageAsync(DbConnection connection, int storageId, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional("SELECT `id`, IFNULL(`interface_type`,0), IFNULL(`currency`,''), IFNULL(`connection_options`,'') FROM `shop_storages` WHERE `id` = ?");
        ErpDb.AddParameters(cmd, storageId);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new StorageRecord(
            Convert.ToInt32(r.GetValue(0), CultureInfo.InvariantCulture),
            Convert.ToInt32(r.GetValue(1), CultureInfo.InvariantCulture),
            r.GetString(2),
            r.GetString(3));
    }

    private static async Task<decimal> CurrencyRateAsync(DbConnection connection, string storageCurrency, string shopCurrency, bool byId, CancellationToken cancellationToken)
    {
        if (storageCurrency.Length == 0 || string.Equals(storageCurrency, shopCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        var sql = byId
            ? "SELECT IFNULL(`rate`,0) FROM `shop_currencies` WHERE `id` = ? LIMIT 1"
            : "SELECT IFNULL(`rate`,0) FROM `shop_currencies` WHERE `iso_code` = ? LIMIT 1";
        var rate = await ErpDb.DecimalAsync(connection, null, ErpDb.Positional(sql), cancellationToken, storageCurrency).ConfigureAwait(false);
        return rate == 0m ? 1m : rate;
    }

    private static async Task<int> AdditionalDaysAsync(DbConnection connection, int officeId, int storageId, CancellationToken cancellationToken)
    {
        var hours = await ErpDb.LongAsync(
            connection, null,
            ErpDb.Positional("SELECT IFNULL((SELECT `additional_time` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? LIMIT 1), 0)"),
            cancellationToken, officeId, storageId).ConfigureAwait(false);
        return CpPricesSendCsv.AdditionalDays(hours);
    }

    private static string? PriceIdFromOptions(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("price_id", out var el))
            {
                return el.ValueKind == JsonValueKind.Number ? el.GetRawText() : el.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    public async Task<CpPricesSendAnswer> CreatePricesAsync(CpPricesSendRequest request, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpPricesSendAnswer.Fail("db", "No database");
        }

        if (request.StorageIds.Count == 0)
        {
            return CpPricesSendAnswer.Fail("invalid", "Select at least one storage");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var config = await LoadConfigAsync(connection, cancellationToken).ConfigureAwait(false);
            var translate = CpOfficeEditorService.Translator(connection, cancellationToken);

            var userGroups = new List<int>();
            var userIds = request.UserIds.Where(u => u > 0).Distinct().ToArray();
            if (userIds.Length > 0)
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT DISTINCT `group_id` FROM `users_groups_bind` WHERE `user_id` IN ("
                                  + string.Join(",", userIds.Select(u => u.ToString(CultureInfo.InvariantCulture))) + ")";
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    userGroups.Add(Convert.ToInt32(r.GetValue(0), CultureInfo.InvariantCulture));
                }
            }

            var groups = MergeGroups(userGroups, request.GroupIdMyListEmails, request.ProfileGroupIds);
            if (groups.Count == 0)
            {
                return CpPricesSendAnswer.Fail("no_groups", "No markup profile selected (choose customers, emails+group, or a profile group).");
            }

            Directory.CreateDirectory(PricesTmpDir);
            var columns = CpPricesSendCsv.DefaultColumns;
            var filterBrand = request.FilterBrand.Trim();
            var filterArticleNorm = CpPricesSendCsv.NormalizeArticle(request.FilterArticle);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var files = new List<CpPricesSendGeneratedFile>();

            foreach (var group in groups)
            {
                var fileName = CpPricesSendCsv.FileName(group, request.PatternName);
                var path = SafeFilePath(fileName);
                if (path is null)
                {
                    return CpPricesSendAnswer.Fail("invalid", "Unsafe file name");
                }

                var rows = 0;
                await using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
                await writer.WriteAsync(CpPricesSendCsv.HeaderLine(columns) + "\r\n").ConfigureAwait(false);

                foreach (var storageId in request.StorageIds.Distinct())
                {
                    var storage = await LoadStorageAsync(connection, storageId, cancellationToken).ConfigureAwait(false);
                    if (storage is null || storage.InterfaceType != 2)
                    {
                        continue;
                    }

                    var priceId = PriceIdFromOptions(storage.ConnectionOptions);
                    if (string.IsNullOrEmpty(priceId))
                    {
                        continue;
                    }

                    var rate = await CurrencyRateAsync(connection, storage.Currency, config.ShopCurrency, false, cancellationToken).ConfigureAwait(false);
                    var additional = await AdditionalDaysAsync(connection, request.OfficeId, storageId, cancellationToken).ConfigureAwait(false);

                    var sql = "SELECT IFNULL(`manufacturer`,''), IFNULL(`article`,''), IFNULL(`name`,''), IFNULL(`exist`,''), IFNULL(`time_to_exe`,0), IFNULL(`price`,0), IFNULL(`min_order`,0), "
                              + "IFNULL((SELECT `markup`/100 FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? AND `group_id` = ? "
                              + "AND `min_point` <= `shop_docpart_prices_data`.`price` AND `max_point` > `shop_docpart_prices_data`.`price` LIMIT 1), 0) AS `markup` "
                              + "FROM `shop_docpart_prices_data` WHERE `price_id` = ?";
                    var args = new List<object?> { request.OfficeId, storageId, group, priceId };
                    if (filterBrand.Length > 0)
                    {
                        sql += " AND UPPER(`manufacturer`) LIKE ?";
                        args.Add("%" + filterBrand.ToUpperInvariant() + "%");
                    }

                    if (filterArticleNorm.Length > 0)
                    {
                        sql += " AND REPLACE(REPLACE(REPLACE(UPPER(`article`),'-',''),' ',''),'_','') LIKE ?";
                        args.Add("%" + filterArticleNorm + "%");
                    }

                    await using var cmd = connection.CreateCommand();
                    cmd.CommandText = ErpDb.Positional(sql);
                    ErpDb.AddParameters(cmd, args.ToArray());
                    await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        var price = Convert.ToDecimal(r.GetValue(5), CultureInfo.InvariantCulture);
                        if (price == 0m)
                        {
                            continue;
                        }

                        var markup = Convert.ToDecimal(r.GetValue(7), CultureInfo.InvariantCulture);
                        var row = new CpPricesSendCsv.Row(
                            CpPricesSendCsv.StripEntities(r.GetString(0)),
                            CpPricesSendCsv.StripEntities(r.GetString(1)),
                            CpPricesSendCsv.StripEntities(r.GetString(2)),
                            Convert.ToString(r.GetValue(3), CultureInfo.InvariantCulture) ?? string.Empty,
                            CpPricesSendCsv.DocpartDays(Convert.ToInt32(r.GetValue(4), CultureInfo.InvariantCulture), additional),
                            CpPricesSendCsv.FinalPrice(price, rate, markup, config.PriceRounding),
                            Convert.ToString(r.GetValue(6), CultureInfo.InvariantCulture) ?? string.Empty);
                        await writer.WriteAsync(CpPricesSendCsv.RowLine(columns, row, true) + "\r\n").ConfigureAwait(false);
                        rows++;
                    }
                }

                var categories = request.CategoryIds.Where(c => c > 0).Distinct().ToArray();
                if (categories.Length > 0)
                {
                    var categorySql = string.Join(",", categories.Select(c => c.ToString(CultureInfo.InvariantCulture)));
                    foreach (var storageId in request.StorageIds.Distinct())
                    {
                        var storage = await LoadStorageAsync(connection, storageId, cancellationToken).ConfigureAwait(false);
                        if (storage is null || storage.InterfaceType != 1)
                        {
                            continue;
                        }

                        var rate = await CurrencyRateAsync(connection, storage.Currency, config.ShopCurrency, true, cancellationToken).ConfigureAwait(false);
                        var additional = await AdditionalDaysAsync(connection, request.OfficeId, storageId, cancellationToken).ConfigureAwait(false);

                        await using var cmd = connection.CreateCommand();
                        cmd.CommandText = ErpDb.Positional(
                            "SELECT d.`product_id`, IFNULL(d.`price`,0), IFNULL(d.`exist`,''), IFNULL(d.`time_to_exe`,0), IFNULL(d.`arrival_time`,0), IFNULL(d.`min_order`,0), "
                            + "IFNULL((SELECT `url` FROM `shop_catalogue_categories` WHERE `id` = d.`category_id`),'') AS `category_url`, "
                            + "IFNULL((SELECT `alias` FROM `shop_catalogue_products` WHERE `id` = d.`product_id`),'') AS `product_alias`, "
                            + "IFNULL((SELECT `caption` FROM `shop_catalogue_products` WHERE `id` = d.`product_id`),'') AS `name`, "
                            + "IFNULL((SELECT `content` FROM `shop_products_text` WHERE `product_id` = d.`product_id` LIMIT 1),'') AS `content`, "
                            + "IFNULL((SELECT `file_name` FROM `shop_products_images` WHERE `product_id` = d.`product_id` LIMIT 1),'') AS `img`, "
                            + "IFNULL((SELECT `value` FROM `shop_properties_values_text` WHERE `product_id` = d.`product_id` AND `property_id` = "
                            + "(SELECT `id` FROM `shop_categories_properties_map` WHERE `value` LIKE 'Артикул' AND `property_type_id` = 3 AND `category_id` = d.`category_id` LIMIT 1) LIMIT 1),'') AS `article`, "
                            + "IFNULL((SELECT `value` FROM `shop_line_lists_items` WHERE `id` = (SELECT `value` FROM `shop_properties_values_list` WHERE `product_id` = d.`product_id` AND `property_id` = "
                            + "(SELECT `id` FROM `shop_categories_properties_map` WHERE `category_id` = d.`category_id` AND `value` LIKE 'Производитель' AND `property_type_id` = 5 LIMIT 1) LIMIT 1) LIMIT 1),'') AS `manufacturer`, "
                            + "IFNULL((SELECT `markup`/100 FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? AND `group_id` = ? "
                            + "AND `min_point` <= d.`price` AND `max_point` > d.`price` LIMIT 1), 0) AS `markup` "
                            + "FROM `shop_storages_data` d WHERE d.`storage_id` = ? AND d.`category_id` IN (" + categorySql + ")");
                        ErpDb.AddParameters(cmd, request.OfficeId, storageId, group, storageId);
                        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            var name = CpPricesSendCsv.StripEntities(await translate(r.GetString(8)).ConfigureAwait(false));
                            var article = CpPricesSendCsv.StripEntities(await translate(r.GetString(11)).ConfigureAwait(false)).Trim();
                            var manufacturer = CpPricesSendCsv.StripEntities(await translate(r.GetString(12)).ConfigureAwait(false)).Trim();
                            if (!CpPricesSendCsv.BrandMatches(manufacturer, filterBrand) || !CpPricesSendCsv.ArticleMatches(article, filterArticleNorm))
                            {
                                continue;
                            }

                            var price = Convert.ToDecimal(r.GetValue(1), CultureInfo.InvariantCulture);
                            if (price == 0m || (article.Length == 0 && name.Length == 0))
                            {
                                continue;
                            }

                            var productId = Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture);
                            var markup = Convert.ToDecimal(r.GetValue(13), CultureInfo.InvariantCulture);
                            var row = new CpPricesSendCsv.Row(
                                manufacturer,
                                article,
                                name,
                                Convert.ToString(r.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                                CpPricesSendCsv.CatalogueDays(Convert.ToInt32(r.GetValue(3), CultureInfo.InvariantCulture), Convert.ToInt64(r.GetValue(4), CultureInfo.InvariantCulture), now, additional),
                                CpPricesSendCsv.FinalPrice(price, rate, markup, config.PriceRounding),
                                Convert.ToString(r.GetValue(5), CultureInfo.InvariantCulture) ?? string.Empty,
                                CpPricesSendCsv.ProductUrl(config.DomainPath, config.ProductUrlMode, r.GetString(6), productId, r.GetString(7)),
                                CpPricesSendCsv.ImageUrl(config.DomainPath, r.GetString(10)),
                                CpPricesSendCsv.ContentCell(await translate(r.GetString(9)).ConfigureAwait(false)));
                            await writer.WriteAsync(CpPricesSendCsv.RowLine(columns, row, false) + "\r\n").ConfigureAwait(false);
                            rows++;
                        }
                    }
                }

                files.Add(new CpPricesSendGeneratedFile(group, fileName, rows, CpPricesSendCsv.PublicUrl(fileName)));
            }

            var total = files.Sum(f => f.Rows);
            return new CpPricesSendAnswer(
                true,
                "Generated " + files.Count.ToString(CultureInfo.InvariantCulture) + " file(s), " + total.ToString("N0", CultureInfo.InvariantCulture) + " row(s).",
                "ok",
                Files: files,
                RowsTotal: total);
        }
        catch (DbException ex)
        {
            return CpPricesSendAnswer.Fail("db", ex.Message);
        }
        catch (IOException ex)
        {
            return CpPricesSendAnswer.Fail("io", "Cannot write price file: " + ex.Message);
        }
    }

    public async Task<CpPricesSendAnswer> SendPricesAsync(CpPricesSendRequest request, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpPricesSendAnswer.Fail("db", "No database");
        }

        var emails = request.Emails;
        if (request.UserIds.Count == 0 && emails.Count == 0)
        {
            return CpPricesSendAnswer.Fail("invalid", "Select customers or enter emails");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var siteName = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `value` FROM `config_items` WHERE `name` = ? LIMIT 1"), cancellationToken, "site_name").ConfigureAwait(false) ?? string.Empty;
            var translate = CpOfficeEditorService.Translator(connection, cancellationToken);
            siteName = await translate(siteName).ConfigureAwait(false);

            var recipients = new List<(string Email, int GroupId)>();
            foreach (var userId in request.UserIds.Where(u => u > 0).Distinct())
            {
                var groupId = (int)await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT IFNULL((SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ? LIMIT 1), 0)"), cancellationToken, userId).ConfigureAwait(false);
                var email = (await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `email` FROM `users` WHERE `user_id` = ?"), cancellationToken, userId).ConfigureAwait(false) ?? string.Empty).Trim();
                if (groupId != 0 && email.Length > 0)
                {
                    recipients.Add((email, groupId));
                }
            }

            foreach (var email in emails)
            {
                if (request.GroupIdMyListEmails != 0)
                {
                    recipients.Add((email, request.GroupIdMyListEmails));
                }
            }

            if (recipients.Count == 0)
            {
                return CpPricesSendAnswer.Fail("no_recipients", "No recipients with a group and email");
            }

            var subject = (siteName + " Price list").Trim();
            var body = "<p>Price list " + System.Net.WebUtility.HtmlEncode(siteName) + " as of " + DateTime.UtcNow.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) + "</p>";
            var attachmentName = "prices_" + DateTime.UtcNow.ToString("dd_MM_yyyy", CultureInfo.InvariantCulture) + ".csv";

            var sent = 0;
            var allOk = true;
            string? failure = null;
            var anyFile = false;
            foreach (var (email, groupId) in recipients)
            {
                var path = SafeFilePath(CpPricesSendCsv.FileName(groupId, null));
                if (path is null || !File.Exists(path))
                {
                    continue;
                }

                anyFile = true;
                var result = await _email.SendAsync(new CpTenantEmailMessage(email, subject, body, path, attachmentName), cancellationToken).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    sent++;
                }
                else
                {
                    allOk = false;
                    failure ??= result.Code + ": " + result.Message;
                    if (result.Code is "disabled" or "missing" or "invalid" or "db")
                    {
                        return new CpPricesSendAnswer(false, "Tenant SMTP unavailable — " + result.Message, "smtp_" + result.Code, Sent: sent);
                    }
                }
            }

            if (!anyFile)
            {
                return CpPricesSendAnswer.Fail("not_generated", "Generate price lists first — no prices_<group>.csv found for the selected recipients.");
            }

            return new CpPricesSendAnswer(allOk, allOk ? string.Empty : "Some emails failed to send" + (failure is null ? string.Empty : " (" + failure + ")"), allOk ? "ok" : "partial", Sent: sent);
        }
        catch (DbException ex)
        {
            return CpPricesSendAnswer.Fail("db", ex.Message);
        }
    }
}
