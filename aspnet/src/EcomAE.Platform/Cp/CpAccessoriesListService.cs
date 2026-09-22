using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Cp;

public sealed record CpAccCategoryNode(long Id, long ParentId, string Slug, string Label, int SortOrder, bool Active, IReadOnlyList<CpAccCategoryNode> Children);

public sealed record CpAccTerm(long Id, string TermType, long ParentId, string Value, string Label, int SortOrder, bool Active);

public sealed record CpAccListingRow(
    long Id,
    long CategoryId,
    long SubcategoryId,
    string CategorySlug,
    string CategoryLabel,
    string SubcategorySlug,
    string SubcategoryLabel,
    string Title,
    string Description,
    string Make,
    string Model,
    string Year,
    string City,
    string ConditionType,
    decimal Price,
    decimal ComparePrice,
    string Currency,
    string ImageUrl,
    string ExternalUrl,
    int PhotoCount,
    bool Featured,
    int StockQty,
    string Status,
    long CreatedAt,
    long UpdatedAt);

public sealed record CpAccPhoto(long Id, long ListingId, string FileName, string Url, int SortOrder, bool IsPrimary);

public sealed record CpAccListFilter(string Q, string Category, string Subcategory, string Status, string Make, int Page, int PerPage);

public sealed record CpAccListResult(
    IReadOnlyList<CpAccListingRow> Items,
    int Total,
    int Page,
    int Pages,
    IReadOnlyDictionary<string, int> StatusCounts);

public interface ICpAccessoriesListService
{
    Task<IReadOnlyList<CpAccCategoryNode>> CategoryTreeAsync(bool includeInactive, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CpAccTerm>> TermsAsync(string type, bool includeInactive, CancellationToken cancellationToken = default);

    Task<CpAccListResult> SearchAsync(CpAccListFilter filter, CancellationToken cancellationToken = default);

    Task<CpAccListingRow?> GetListingAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CpAccPhoto>> PhotosAsync(long listingId, CancellationToken cancellationToken = default);
}

public sealed class CpAccessoriesListService : ICpAccessoriesListService
{
    public const string PhotoPublicDir = "/content/files/images/accessories/";
    public static readonly string[] Statuses = ["published", "draft", "unpublished"];
    public static readonly (string Key, string Label)[] TermTypes =
    [
        ("make", "Make"),
        ("model", "Model"),
        ("year", "Year"),
        ("city", "City"),
        ("condition", "Condition"),
    ];

    private readonly IErpWriteConnectionFactory _connections;

    public CpAccessoriesListService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static CpAccListFilter ReadFilter(HttpRequest request)
    {
        var q = request.Query;
        var page = int.TryParse(q["page"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) && p > 0 ? p : 1;
        return new CpAccListFilter(
            q["q"].ToString().Trim(),
            q["category"].ToString().Trim(),
            q["subcategory"].ToString().Trim(),
            q["status"].ToString().Trim(),
            q["make"].ToString().Trim(),
            page,
            50);
    }

    public static string StorefrontUrl(long id, string categorySlug, string subcategorySlug)
    {
        var parts = new List<string>();
        if (id > 0)
        {
            parts.Add("id=" + id.ToString(CultureInfo.InvariantCulture));
        }

        if (categorySlug.Length > 0)
        {
            parts.Add("category=" + Uri.EscapeDataString(categorySlug));
        }

        if (subcategorySlug.Length > 0)
        {
            parts.Add("subcategory=" + Uri.EscapeDataString(subcategorySlug));
        }

        return "/en/accessories-spare-parts" + (parts.Count == 0 ? "" : "?" + string.Join("&", parts));
    }

    public static string PhotoUrl(string fileName)
    {
        var name = Path.GetFileName(fileName.Trim());
        return name.Length == 0 ? "" : PhotoPublicDir + Uri.EscapeDataString(name);
    }

    public static string NormalizeTermType(string? type)
    {
        var t = (type ?? "").Trim().ToLowerInvariant();
        return TermTypes.Any(x => x.Key == t) ? t : "make";
    }

    public async Task<IReadOnlyList<CpAccCategoryNode>> CategoryTreeAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            return await ReadTreeAsync(connection, includeInactive, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return [];
        }
    }

    private static async Task<IReadOnlyList<CpAccCategoryNode>> ReadTreeAsync(DbConnection connection, bool includeInactive, CancellationToken cancellationToken)
    {
        var flat = new List<(long Id, long ParentId, string Slug, string Label, int Sort, bool Active)>();
        await using (var c = connection.CreateCommand())
        {
            c.CommandText =
                "SELECT `id`, `parent_id`, `slug`, `label`, `sort_order`, `active` FROM `epc_acc_categories` " +
                (includeInactive ? "" : "WHERE `active` = 1 ") +
                "ORDER BY `parent_id` ASC, `sort_order` ASC, `label` ASC";
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                flat.Add((L(r, 0), L(r, 1), S(r, 2), S(r, 3), I(r, 4), I(r, 5) == 1));
            }
        }

        var parents = new List<CpAccCategoryNode>();
        var childBuckets = new Dictionary<long, List<CpAccCategoryNode>>();
        foreach (var row in flat.Where(x => x.ParentId == 0))
        {
            var children = new List<CpAccCategoryNode>();
            childBuckets[row.Id] = children;
            parents.Add(new CpAccCategoryNode(row.Id, 0, row.Slug, row.Label, row.Sort, row.Active, children));
        }

        foreach (var row in flat.Where(x => x.ParentId != 0))
        {
            if (childBuckets.TryGetValue(row.ParentId, out var bucket))
            {
                bucket.Add(new CpAccCategoryNode(row.Id, row.ParentId, row.Slug, row.Label, row.Sort, row.Active, []));
            }
        }

        return parents;
    }

    public async Task<IReadOnlyList<CpAccTerm>> TermsAsync(string type, bool includeInactive, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var c = connection.CreateCommand();
            c.CommandText = ErpDb.Positional(
                "SELECT `id`, `term_type`, `parent_id`, `value`, `label`, `sort_order`, `active` FROM `epc_acc_terms` WHERE `term_type` = ?" +
                (includeInactive ? "" : " AND `active` = 1") +
                " ORDER BY `sort_order` ASC, `label` ASC");
            ErpDb.AddParameters(c, NormalizeTermType(type));
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var rows = new List<CpAccTerm>();
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpAccTerm(L(r, 0), S(r, 1), L(r, 2), S(r, 3), S(r, 4), I(r, 5), I(r, 6) == 1));
            }

            return rows;
        }
        catch (DbException)
        {
            return [];
        }
    }

    public async Task<CpAccListResult> SearchAsync(CpAccListFilter filter, CancellationToken cancellationToken = default)
    {
        var empty = new CpAccListResult([], 0, 1, 1, new Dictionary<string, int>());
        if (!_connections.IsConfigured)
        {
            return empty;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var tree = await ReadTreeAsync(connection, false, cancellationToken).ConfigureAwait(false);

            long categoryId = 0, subcategoryId = 0;
            foreach (var parent in tree)
            {
                if (filter.Category.Length > 0 && (parent.Slug == filter.Category || parent.Id.ToString(CultureInfo.InvariantCulture) == filter.Category))
                {
                    categoryId = parent.Id;
                }

                foreach (var child in parent.Children)
                {
                    if (filter.Subcategory.Length > 0 && (child.Slug == filter.Subcategory || child.Id.ToString(CultureInfo.InvariantCulture) == filter.Subcategory))
                    {
                        subcategoryId = child.Id;
                        if (categoryId < 1)
                        {
                            categoryId = parent.Id;
                        }
                    }
                }
            }

            var where = new List<string> { "1=1" };
            var args = new List<object?>();
            if (categoryId > 0)
            {
                where.Add("l.`category_id` = ?");
                args.Add(categoryId);
            }

            if (subcategoryId > 0)
            {
                where.Add("l.`subcategory_id` = ?");
                args.Add(subcategoryId);
            }

            if (filter.Status.Length > 0)
            {
                where.Add("l.`status` = ?");
                args.Add(filter.Status);
            }

            if (filter.Make.Length > 0)
            {
                where.Add("l.`make` = ?");
                args.Add(filter.Make);
            }

            if (filter.Q.Length > 0)
            {
                where.Add("(l.`title` LIKE ? OR l.`make` LIKE ? OR l.`model` LIKE ? OR l.`city` LIKE ? OR l.`id` = ?)");
                var like = "%" + filter.Q + "%";
                args.AddRange([like, like, like, like, long.TryParse(filter.Q, NumberStyles.Integer, CultureInfo.InvariantCulture, out var qid) ? qid : 0L]);
            }

            var whereSql = string.Join(" AND ", where);
            var perPage = Math.Clamp(filter.PerPage, 10, 100);
            var total = (int)await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `epc_acc_listings` l WHERE " + whereSql), cancellationToken, args.ToArray()).ConfigureAwait(false);
            var pages = Math.Max(1, (int)Math.Ceiling(total / (double)perPage));
            var page = Math.Min(Math.Max(1, filter.Page), pages);
            var offset = (page - 1) * perPage;

            var items = new List<CpAccListingRow>();
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ErpDb.Positional(
                    ListingSelect + " WHERE " + whereSql +
                    " ORDER BY l.`updated_at` DESC, l.`id` DESC LIMIT " + perPage.ToString(CultureInfo.InvariantCulture) +
                    " OFFSET " + offset.ToString(CultureInfo.InvariantCulture));
                ErpDb.AddParameters(c, args.ToArray());
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    items.Add(ReadListing(r));
                }
            }

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = "SELECT `status`, COUNT(*) FROM `epc_acc_listings` GROUP BY `status`";
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    counts[S(r, 0)] = I(r, 1);
                }
            }

            return new CpAccListResult(items, total, page, pages, counts);
        }
        catch (DbException)
        {
            return empty;
        }
    }

    public async Task<CpAccListingRow?> GetListingAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var c = connection.CreateCommand();
            c.CommandText = ErpDb.Positional(ListingSelect + " WHERE l.`id` = ? LIMIT 1");
            ErpDb.AddParameters(c, id);
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await r.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadListing(r) : null;
        }
        catch (DbException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<CpAccPhoto>> PhotosAsync(long listingId, CancellationToken cancellationToken = default)
    {
        if (listingId <= 0 || !_connections.IsConfigured)
        {
            return [];
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var c = connection.CreateCommand();
            c.CommandText = ErpDb.Positional(
                "SELECT `id`, `listing_id`, `file_name`, `sort_order`, `is_primary` FROM `epc_acc_photos` WHERE `listing_id` = ? " +
                "ORDER BY `is_primary` DESC, `sort_order` ASC, `id` ASC");
            ErpDb.AddParameters(c, listingId);
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var rows = new List<CpAccPhoto>();
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var file = S(r, 2);
                rows.Add(new CpAccPhoto(L(r, 0), L(r, 1), file, PhotoUrl(file), I(r, 3), I(r, 4) == 1));
            }

            return rows;
        }
        catch (DbException)
        {
            return [];
        }
    }

    private const string ListingSelect =
        "SELECT l.`id`, l.`category_id`, l.`subcategory_id`, IFNULL(c.`slug`,''), IFNULL(c.`label`,''), IFNULL(s.`slug`,''), IFNULL(s.`label`,''), " +
        "l.`title`, IFNULL(l.`description`,''), l.`make`, l.`model`, l.`year`, l.`city`, l.`condition_type`, l.`price`, l.`compare_price`, l.`currency`, " +
        "l.`image_url`, l.`external_url`, l.`photo_count`, l.`featured`, l.`stock_qty`, l.`status`, l.`created_at`, l.`updated_at` " +
        "FROM `epc_acc_listings` l " +
        "LEFT JOIN `epc_acc_categories` c ON c.`id` = l.`category_id` " +
        "LEFT JOIN `epc_acc_categories` s ON s.`id` = l.`subcategory_id`";

    private static CpAccListingRow ReadListing(DbDataReader r) => new(
        L(r, 0), L(r, 1), L(r, 2), S(r, 3), S(r, 4), S(r, 5), S(r, 6),
        S(r, 7), S(r, 8), S(r, 9), S(r, 10), S(r, 11), S(r, 12), S(r, 13),
        D(r, 14), D(r, 15), S(r, 16), S(r, 17), S(r, 18), I(r, 19), I(r, 20) == 1, I(r, 21), S(r, 22), L(r, 23), L(r, 24));

    private static int I(DbDataReader r, int i) => r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i), CultureInfo.InvariantCulture);
    private static long L(DbDataReader r, int i) => r.IsDBNull(i) ? 0 : Convert.ToInt64(r.GetValue(i), CultureInfo.InvariantCulture);
    private static decimal D(DbDataReader r, int i) => r.IsDBNull(i) ? 0m : Convert.ToDecimal(r.GetValue(i), CultureInfo.InvariantCulture);
    private static string S(DbDataReader r, int i) => r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i), CultureInfo.InvariantCulture) ?? "";
}
