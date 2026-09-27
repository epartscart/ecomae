using System.Data.Common;
using System.Globalization;
using System.Net;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>PHP <c>prices_edit/prices.php</c> filter state (fuzzy search + exact match + site preview).</summary>
public sealed record CpPricesEditFilter(
    string SearchText = "",
    long PriceId = 0,
    string Article = "",
    string Manufacturer = "",
    bool NoArticle = false,
    bool NoManufacturer = false,
    int PreviewGroupId = 0,
    decimal MinMargin = 0,
    bool HideHidden = false,
    int Page = 1)
{
    /// <summary>PHP <c>$kol = 20</c>.</summary>
    public const int PageSize = 20;

    public int SafePage => Page < 1 ? 1 : Page;
}

public sealed record CpPricesEditPriceList(long Id, string Name);

public sealed record CpPricesEditProfile(int GroupId, string Value, string Code);

public sealed record CpPricesEditRow(
    long Id,
    long PriceId,
    string PriceListName,
    string WarehouseLabel,
    string Article,
    string ArticleShow,
    string Manufacturer,
    string Name,
    int Exist,
    decimal Price,
    decimal? SitePrice,
    decimal? MarginPct,
    bool Hidden,
    int TimeToExe,
    int MinOrder,
    string Storage,
    string SiteUrl);

public sealed record CpPricesEditDesk(
    bool Available,
    string Message,
    IReadOnlyList<CpPricesEditPriceList> PriceLists,
    IReadOnlyList<CpPricesEditProfile> Profiles,
    IReadOnlyList<CpPricesEditRow> Rows,
    int Total,
    int Page,
    string ProfileTitle)
{
    public static CpPricesEditDesk Unavailable(string message)
        => new(false, message, [], [], [], 0, 1, "Site price");

    public int PageCount => Total <= 0 ? 1 : (int)Math.Ceiling(Total / (double)CpPricesEditFilter.PageSize);
}

/// <summary>
/// Typed twin of PHP <c>prices_edit/ajax_operations.php</c> <c>get_table</c> plus
/// <c>epc_prices_edit_helpers.php</c> (price-list names, profiles, warehouse map, site-price preview).
/// </summary>
public interface ICpPricesEditDeskService
{
    Task<CpPricesEditDesk> LoadAsync(CpPricesEditFilter filter, CancellationToken cancellationToken = default);
}

public sealed class CpPricesEditDeskService : ICpPricesEditDeskService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpPricesEditDeskService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>get_where()</c>: exact price/article/manufacturer, empty-value flags, 3+ char fuzzy terms.</summary>
    public static (string Where, object?[] Args) BuildWhere(CpPricesEditFilter filter)
    {
        var parts = new List<string>();
        var args = new List<object?>();

        if (filter.PriceId > 0)
        {
            parts.Add("(`price_id` IN(?))");
            args.Add(filter.PriceId);
        }

        var article = EpcPricing.NormalizeArticle(filter.Article);
        if (article.Length > 0)
        {
            parts.Add("(`article` LIKE ?)");
            args.Add(article);
        }

        var manufacturer = WebUtility.HtmlEncode(filter.Manufacturer.Trim().ToUpperInvariant());
        if (manufacturer.Length > 0)
        {
            parts.Add("(`manufacturer` LIKE ?)");
            args.Add(manufacturer);
        }

        if (filter.NoArticle)
        {
            parts.Add("(`article` LIKE ?)");
            args.Add("");
        }

        if (filter.NoManufacturer)
        {
            parts.Add("(`manufacturer` LIKE ?)");
            args.Add("");
        }

        var terms = filter.SearchText
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 3)
            .ToArray();
        if (terms.Length > 0)
        {
            var columns = new[] { "article", "manufacturer", "name" };
            var groups = new List<string>();
            foreach (var column in columns)
            {
                groups.Add("(" + string.Join(" AND ", terms.Select(_ => "(`" + column + "` LIKE ?)")) + ")");
                args.AddRange(terms.Select(object? (t) => "%" + t + "%"));
            }

            parts.Add("(" + string.Join(" OR ", groups) + ")");
        }

        return (parts.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", parts), args.ToArray());
    }

    /// <summary>PHP <c>epc_prices_edit_site_url()</c>.</summary>
    public static string SiteUrl(string article, string manufacturer)
    {
        var url = "/shop/part_search?article=" + Uri.EscapeDataString(article);
        return manufacturer.Trim().Length == 0 ? url : url + "&brend=" + Uri.EscapeDataString(manufacturer);
    }

    public async Task<CpPricesEditDesk> LoadAsync(CpPricesEditFilter filter, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpPricesEditDesk.Unavailable("No database configured — the price row editor is read-only.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

            var priceLists = await LoadPriceListsAsync(connection, cancellationToken).ConfigureAwait(false);
            var profiles = await LoadProfilesAsync(connection, cancellationToken).ConfigureAwait(false);
            var warehouses = await LoadWarehouseMapAsync(connection, cancellationToken).ConfigureAwait(false);

            var (where, args) = BuildWhere(filter);
            var total = 0;
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional("SELECT COUNT(*) FROM `shop_docpart_prices_data` " + where);
                ErpDb.AddParameters(cmd, args);
                var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                total = scalar is null or DBNull ? 0 : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
            }

            var offset = (filter.SafePage - 1) * CpPricesEditFilter.PageSize;
            var raw = new List<(long Id, long PriceId, string Article, string ArticleShow, string Manufacturer, string Name, int Exist, decimal Price, int TimeToExe, int MinOrder, string Storage)>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(
                    "SELECT `id`, `price_id`, IFNULL(`article`,''), IFNULL(`article_show`,''), IFNULL(`manufacturer`,''), IFNULL(`name`,''), "
                    + "IFNULL(`exist`,0), IFNULL(`price`,0), IFNULL(`time_to_exe`,0), IFNULL(`min_order`,0), IFNULL(`storage`,'') "
                    + "FROM `shop_docpart_prices_data` " + where
                    + " ORDER BY `article` LIMIT " + offset.ToString(CultureInfo.InvariantCulture)
                    + ", " + CpPricesEditFilter.PageSize.ToString(CultureInfo.InvariantCulture));
                ErpDb.AddParameters(cmd, args);
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    raw.Add((
                        Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                        Convert.ToInt64(r.GetValue(1), CultureInfo.InvariantCulture),
                        r.GetString(2),
                        r.GetString(3),
                        r.GetString(4),
                        r.GetString(5),
                        Convert.ToInt32(r.GetValue(6), CultureInfo.InvariantCulture),
                        Convert.ToDecimal(r.GetValue(7), CultureInfo.InvariantCulture),
                        Convert.ToInt32(r.GetValue(8), CultureInfo.InvariantCulture),
                        Convert.ToInt32(r.GetValue(9), CultureInfo.InvariantCulture),
                        r.GetString(10)));
                }
            }

            var rows = new List<CpPricesEditRow>(raw.Count);
            foreach (var row in raw)
            {
                var manufacturer = WebUtility.HtmlDecode(row.Manufacturer);
                var articleLink = row.ArticleShow.Trim().Length > 0 ? row.ArticleShow : row.Article;

                var warehouseLabel = "—";
                var storageId = 0;
                if (warehouses.TryGetValue(row.PriceId, out var wh))
                {
                    warehouseLabel = wh.Label;
                    storageId = wh.StorageId;
                }

                var rowStorage = WebUtility.HtmlDecode(row.Storage).Trim();
                if (rowStorage.Length > 0)
                {
                    warehouseLabel += (warehouseLabel == "—" ? "" : "; ") + "row: " + rowStorage;
                }

                decimal? sitePrice = null;
                decimal? marginPct = null;
                var hidden = false;
                if (filter.PreviewGroupId > 0 && row.Price > 0)
                {
                    var rule = await EpcPricing.ApplyPriceRulesAsync(
                        connection,
                        filter.PreviewGroupId,
                        manufacturer,
                        row.Price,
                        0m,
                        articleLink,
                        storageId,
                        cancellationToken).ConfigureAwait(false);
                    if (!rule.Visible)
                    {
                        if (filter.HideHidden)
                        {
                            continue;
                        }

                        hidden = true;
                    }
                    else
                    {
                        sitePrice = Math.Round(rule.Price, 2, MidpointRounding.AwayFromZero);
                        marginPct = Math.Round(((sitePrice.Value - row.Price) / row.Price) * 100m, 1, MidpointRounding.AwayFromZero);
                        if (filter.MinMargin > 0 && marginPct < filter.MinMargin)
                        {
                            continue;
                        }
                    }
                }
                else if (filter.MinMargin > 0 || filter.HideHidden)
                {
                    continue;
                }

                rows.Add(new CpPricesEditRow(
                    row.Id,
                    row.PriceId,
                    priceLists.FirstOrDefault(p => p.Id == row.PriceId)?.Name ?? "#" + row.PriceId.ToString(CultureInfo.InvariantCulture),
                    warehouseLabel,
                    row.Article,
                    row.ArticleShow,
                    row.Manufacturer,
                    row.Name,
                    row.Exist,
                    row.Price,
                    sitePrice,
                    marginPct,
                    hidden,
                    row.TimeToExe,
                    row.MinOrder,
                    row.Storage,
                    SiteUrl(articleLink, manufacturer)));
            }

            var profileTitle = "Site price";
            var active = profiles.FirstOrDefault(p => p.GroupId == filter.PreviewGroupId);
            if (active is not null)
            {
                profileTitle = "Site (" + active.Value + ")";
            }

            return new CpPricesEditDesk(true, "", priceLists, profiles, rows, total, filter.SafePage, profileTitle);
        }
        catch (DbException ex)
        {
            return CpPricesEditDesk.Unavailable("Price rows could not be loaded: " + ex.Message);
        }
    }

    /// <summary>PHP <c>epc_prices_edit_load_price_names()</c>.</summary>
    private static async Task<IReadOnlyList<CpPricesEditPriceList>> LoadPriceListsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var list = new List<CpPricesEditPriceList>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT `id`, IFNULL(`name`,'') FROM `shop_docpart_prices` ORDER BY `name`";
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new CpPricesEditPriceList(Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture), r.GetString(1)));
        }

        return list;
    }

    /// <summary>PHP <c>epc_prices_edit_load_profiles()</c>; missing table degrades to no preview.</summary>
    private static async Task<IReadOnlyList<CpPricesEditProfile>> LoadProfilesAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var list = new List<CpPricesEditProfile>();
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT `groups`.`id`, IFNULL(`groups`.`value`,''), IFNULL(`epc_price_profiles`.`code`,'') "
                              + "FROM `epc_price_profiles` INNER JOIN `groups` ON `groups`.`id` = `epc_price_profiles`.`group_id` "
                              + "ORDER BY `epc_price_profiles`.`id` ASC";
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                list.Add(new CpPricesEditProfile(
                    Convert.ToInt32(r.GetValue(0), CultureInfo.InvariantCulture),
                    r.GetString(1),
                    r.GetString(2)));
            }
        }
        catch (DbException)
        {
            return [];
        }

        return list;
    }

    /// <summary>PHP <c>epc_prices_edit_load_warehouse_map()</c>: interface_type 2 storages keyed by price_id.</summary>
    private static async Task<Dictionary<long, (string Label, int StorageId)>> LoadWarehouseMapAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<long, (string Label, int StorageId)>();
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT `id`, IFNULL(`name`,''), IFNULL(`connection_options`,'') FROM `shop_storages` WHERE `interface_type` = 2";
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var storageId = Convert.ToInt32(r.GetValue(0), CultureInfo.InvariantCulture);
                var name = r.GetString(1);
                var priceId = PriceIdFromOptions(r.GetString(2));
                if (priceId <= 0)
                {
                    continue;
                }

                map[priceId] = map.TryGetValue(priceId, out var existing)
                    ? (existing.Label + ", " + name, existing.StorageId)
                    : (name, storageId);
            }
        }
        catch (DbException)
        {
            return map;
        }

        return map;
    }

    private static long PriceIdFromOptions(string json)
    {
        if (json.Length == 0)
        {
            return 0;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("price_id", out var value))
            {
                return 0;
            }

            return value.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Number => value.TryGetInt64(out var n) ? n : 0,
                System.Text.Json.JsonValueKind.String => long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? s : 0,
                _ => 0,
            };
        }
        catch (System.Text.Json.JsonException)
        {
            return 0;
        }
    }
}
