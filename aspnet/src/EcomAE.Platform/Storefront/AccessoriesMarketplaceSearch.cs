using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Data;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Public accessories catalog. PHP <c>ajax_epc_accessories_search.php</c> plus
/// <c>epc_acc_marketplace_search</c>. Anonymous. No login.
/// </summary>
public static class AccessoriesMarketplaceSearch
{
    public const string Path = "/content/shop/docpart/ajax_epc_accessories_search.php";

    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods(Path, ["GET", "POST"], SearchAsync);
    }

    /// <summary>PHP <c>epc_acc_storefront_url</c>.</summary>
    public static string StorefrontDetailUrl(long id, string? category, string? subcategory, string langHref = "/en")
    {
        var lang = (string.IsNullOrWhiteSpace(langHref) ? "/en" : langHref).TrimEnd('/');
        var query = new List<string>();
        if (id > 0)
        {
            query.Add("id=" + id.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query.Add("category=" + PhpQuery(category.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(subcategory))
        {
            query.Add("subcategory=" + PhpQuery(subcategory.Trim()));
        }

        var path = lang + "/accessories-spare-parts";
        return query.Count == 0 ? path : path + "?" + string.Join('&', query);
    }

    /// <summary>PHP <c>epc_acc_is_outbound_external_url</c>.</summary>
    public static bool IsOutboundExternalUrl(string? url)
    {
        var value = (url ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return false;
        }

        if (System.Text.RegularExpressions.Regex.IsMatch(value, @"(^|/)accessories(-spare-parts)?([/?#]|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            return false;
        }

        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return value[0] == '/';
    }

    /// <summary>PHP <c>max(12, min(48, per_page))</c> with a missing value of 24.</summary>
    public static int NormalizePerPage(string? raw)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            parsed = 24;
        }

        return Math.Max(12, Math.Min(48, parsed));
    }

    private static async Task<IResult> SearchAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        IWebHostEnvironment env,
        CancellationToken cancellationToken)
    {
        if (!connections.IsConfigured)
        {
            return Write(Unavailable());
        }

        try
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
            var body = await SearchJsonAsync(connection, env, FiltersOf(context), cancellationToken).ConfigureAwait(false);
            return Write(body);
        }
        catch (DbException ex)
        {
            return Write(CatalogError(context, ex.Message));
        }
        catch (InvalidOperationException)
        {
            return Write(Unavailable());
        }
        catch (Exception ex)
        {
            return Write(CatalogError(context, ex.Message));
        }
    }

    internal static async Task<string> SearchJsonAsync(
        DbConnection connection,
        IWebHostEnvironment env,
        AccFilters filters,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var tree = await CategoryTreeAsync(connection, env, cancellationToken).ConfigureAwait(false);
        var (categoryId, subcategoryId) = ResolveCategory(tree, filters.Category, filters.Subcategory);
        var where = new List<string> { "l.`status` = 'published'" };
        var args = new List<object?>();
        if (filters.ListingId > 0)
        {
            where.Add("l.`id` = ?");
            args.Add(filters.ListingId);
        }
        else
        {
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

            if (filters.Make.Length > 0)
            {
                where.Add("l.`make` = ?");
                args.Add(filters.Make);
            }

            if (filters.Model.Length > 0)
            {
                where.Add("l.`model` LIKE ?");
                args.Add("%" + filters.Model + "%");
            }

            if (filters.City.Length > 0)
            {
                where.Add("l.`city` = ?");
                args.Add(filters.City);
            }

            if (filters.Condition.Length > 0)
            {
                where.Add("l.`condition_type` = ?");
                args.Add(filters.Condition.ToLowerInvariant());
            }

            if (filters.PriceMin > 0)
            {
                where.Add("l.`price` >= ?");
                args.Add(filters.PriceMin);
            }

            if (filters.PriceMax > 0)
            {
                where.Add("l.`price` <= ?");
                args.Add(filters.PriceMax);
            }

            if (filters.Q.Length > 0)
            {
                where.Add("(l.`title` LIKE ? OR l.`description` LIKE ? OR l.`make` LIKE ? OR l.`model` LIKE ?)");
                var like = "%" + filters.Q + "%";
                args.Add(like);
                args.Add(like);
                args.Add(like);
                args.Add(like);
            }
        }

        var whereSql = string.Join(" AND ", where);
        var total = (int)await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_acc_listings` l WHERE " + whereSql),
            cancellationToken,
            args.ToArray()).ConfigureAwait(false);
        var perPage = filters.PerPage;
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)perPage));
        var page = Math.Min(Math.Max(1, filters.Page), pages);
        var offset = (page - 1) * perPage;
        var orderSql = OrderSql(filters.Sort);
        var rows = await ReadListingsAsync(connection, whereSql, orderSql, perPage, offset, args, cancellationToken).ConfigureAwait(false);
        var items = new List<object>();
        foreach (var row in rows)
        {
            var photos = await PhotosAsync(connection, row.Id, cancellationToken).ConfigureAwait(false);
            var imageUrl = row.ImageUrl;
            if (imageUrl.Length == 0 && photos.Count > 0)
            {
                imageUrl = photos[0].Url;
                foreach (var photo in photos)
                {
                    if (photo.IsPrimary)
                    {
                        imageUrl = photo.Url;
                        break;
                    }
                }
            }

            var photoCount = Math.Max(photos.Count, Math.Max(row.PhotoCount, imageUrl.Length > 0 ? 1 : 0));
            var detail = StorefrontDetailUrl(row.Id, row.CategorySlug, row.SubcategorySlug);
            var outbound = IsOutboundExternalUrl(row.ExternalUrl) ? row.ExternalUrl.Trim() : string.Empty;
            items.Add(new
            {
                id = row.Id,
                title = row.Title,
                description = row.Description,
                make = row.Make,
                model = row.Model,
                year = row.Year,
                city = row.City,
                condition = row.Condition,
                price = row.Price,
                compare_price = row.ComparePrice,
                currency = row.Currency,
                image_url = imageUrl,
                photos,
                external_url = outbound,
                detail_url = detail,
                url = detail,
                photo_count = Math.Max(1, photoCount),
                featured = row.Featured,
                stock_qty = row.StockQty,
                category = row.CategorySlug,
                category_label = row.CategoryLabel,
                subcategory = row.SubcategorySlug,
                subcategory_label = row.SubcategoryLabel,
                updated_at = row.UpdatedAt,
            });
        }

        var facets = await FacetsAsync(connection, env, tree, cancellationToken).ConfigureAwait(false);
        var from = total == 0 ? 0 : offset + 1;
        var to = Math.Min(total, offset + items.Count);
        var payload = new
        {
            total,
            page,
            per_page = perPage,
            pages,
            from,
            to,
            items,
            facets = new
            {
                categories = facets.Categories,
                makes = facets.Makes,
                cities = facets.Cities,
                conditions = facets.Conditions,
            },
            taxonomy = facets.Categories,
            makes = facets.MakeLabels,
            cities = facets.CityLabels,
            sort = string.IsNullOrWhiteSpace(filters.Sort) ? "updated-desc" : filters.Sort,
            source = "epc_acc_listings",
            empty_catalog = total == 0,
            status = true,
            currency_default = "AED",
        };
        return JsonSerializer.Serialize(payload, Json);
    }

    private static string OrderSql(string sort) => sort switch
    {
        "price-asc" => "l.`featured` DESC, l.`price` ASC, l.`updated_at` DESC",
        "price-desc" => "l.`featured` DESC, l.`price` DESC, l.`updated_at` DESC",
        "updated-asc" => "l.`featured` DESC, l.`updated_at` ASC",
        "top-sales" => "l.`featured` DESC, l.`stock_qty` DESC, l.`updated_at` DESC",
        _ => "l.`featured` DESC, l.`updated_at` DESC, l.`id` DESC",
    };

    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_acc_categories` (
                `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
                `parent_id` INT UNSIGNED NOT NULL DEFAULT 0,
                `slug` VARCHAR(120) NOT NULL,
                `label` VARCHAR(190) NOT NULL,
                `pw_id` INT UNSIGNED NOT NULL DEFAULT 0,
                `sort_order` INT NOT NULL DEFAULT 0,
                `active` TINYINT(1) NOT NULL DEFAULT 1,
                PRIMARY KEY (`id`),
                UNIQUE KEY `slug_parent` (`slug`, `parent_id`),
                KEY `parent_id` (`parent_id`),
                KEY `active` (`active`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_acc_listings` (
                `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
                `category_id` INT UNSIGNED NOT NULL DEFAULT 0,
                `subcategory_id` INT UNSIGNED NOT NULL DEFAULT 0,
                `title` VARCHAR(255) NOT NULL,
                `description` TEXT NULL,
                `make` VARCHAR(120) NOT NULL DEFAULT '',
                `model` VARCHAR(120) NOT NULL DEFAULT '',
                `year` VARCHAR(16) NOT NULL DEFAULT '',
                `city` VARCHAR(120) NOT NULL DEFAULT '',
                `condition_type` VARCHAR(32) NOT NULL DEFAULT 'new',
                `price` DECIMAL(12,2) NOT NULL DEFAULT 0,
                `compare_price` DECIMAL(12,2) NOT NULL DEFAULT 0,
                `currency` VARCHAR(8) NOT NULL DEFAULT 'AED',
                `image_url` VARCHAR(500) NOT NULL DEFAULT '',
                `external_url` VARCHAR(500) NOT NULL DEFAULT '',
                `photo_count` INT NOT NULL DEFAULT 1,
                `featured` TINYINT(1) NOT NULL DEFAULT 0,
                `stock_qty` INT NOT NULL DEFAULT 0,
                `status` VARCHAR(32) NOT NULL DEFAULT 'published',
                `created_at` INT UNSIGNED NOT NULL DEFAULT 0,
                `updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                KEY `category_id` (`category_id`),
                KEY `subcategory_id` (`subcategory_id`),
                KEY `make` (`make`),
                KEY `city` (`city`),
                KEY `status_price` (`status`, `price`),
                KEY `featured` (`featured`),
                KEY `updated_at` (`updated_at`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_acc_terms` (
                `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
                `term_type` VARCHAR(32) NOT NULL,
                `parent_id` INT UNSIGNED NOT NULL DEFAULT 0,
                `value` VARCHAR(190) NOT NULL,
                `label` VARCHAR(190) NOT NULL,
                `sort_order` INT NOT NULL DEFAULT 0,
                `active` TINYINT(1) NOT NULL DEFAULT 1,
                PRIMARY KEY (`id`),
                UNIQUE KEY `type_value_parent` (`term_type`, `value`, `parent_id`),
                KEY `term_type_active` (`term_type`, `active`),
                KEY `parent_id` (`parent_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_acc_photos` (
                `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
                `listing_id` INT UNSIGNED NOT NULL,
                `file_name` VARCHAR(255) NOT NULL,
                `sort_order` INT NOT NULL DEFAULT 0,
                `is_primary` TINYINT(1) NOT NULL DEFAULT 0,
                `created_at` INT UNSIGNED NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                KEY `listing_id` (`listing_id`),
                KEY `listing_primary` (`listing_id`, `is_primary`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<List<AccCategory>> CategoryTreeAsync(DbConnection connection, IWebHostEnvironment env, CancellationToken cancellationToken)
    {
        var rows = await ReadCategoriesAsync(connection, cancellationToken).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            await SeedCategoriesAsync(connection, env, cancellationToken).ConfigureAwait(false);
            rows = await ReadCategoriesAsync(connection, cancellationToken).ConfigureAwait(false);
        }

        var parents = new Dictionary<long, AccCategory>();
        var children = new List<(long ParentId, AccCategory Node)>();
        foreach (var row in rows)
        {
            if (row.ParentId == 0)
            {
                parents[row.Id] = row with { Children = [] };
            }
            else
            {
                children.Add((row.ParentId, row));
            }
        }

        foreach (var (parentId, child) in children)
        {
            if (!parents.TryGetValue(parentId, out var parent))
            {
                continue;
            }

            parent.Children.Add(new AccCategory(child.Id, child.ParentId, child.Slug, child.Label, child.PwId, []));
        }

        return parents.Values.ToList();
    }

    private static async Task<List<AccCategory>> ReadCategoriesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<AccCategory>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT `id`, `parent_id`, `slug`, `label`, `pw_id`
            FROM `epc_acc_categories` WHERE `active` = 1
            ORDER BY `parent_id` ASC, `sort_order` ASC, `label` ASC
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new AccCategory(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
                []));
        }

        return rows;
    }

    private static async Task SeedCategoriesAsync(DbConnection connection, IWebHostEnvironment env, CancellationToken cancellationToken)
    {
        var tax = LoadTaxonomy(env);
        var order = 0;
        foreach (var parent in tax.Categories)
        {
            order++;
            if (parent.Slug.Length == 0)
            {
                continue;
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("""
                    INSERT INTO `epc_acc_categories` (`parent_id`, `slug`, `label`, `pw_id`, `sort_order`, `active`)
                    VALUES (?, ?, ?, ?, ?, 1)
                    ON DUPLICATE KEY UPDATE `label` = VALUES(`label`), `pw_id` = VALUES(`pw_id`), `sort_order` = VALUES(`sort_order`), `active` = 1
                    """),
                cancellationToken,
                0,
                parent.Slug,
                parent.Label.Length == 0 ? parent.Slug : parent.Label,
                parent.PwId,
                order).ConfigureAwait(false);
            var parentId = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_acc_categories` WHERE `parent_id` = 0 AND `slug` = ? LIMIT 1"),
                cancellationToken,
                parent.Slug).ConfigureAwait(false);
            var childOrder = 0;
            foreach (var child in parent.Children)
            {
                childOrder++;
                if (child.Slug.Length == 0 || parentId < 1)
                {
                    continue;
                }

                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("""
                        INSERT INTO `epc_acc_categories` (`parent_id`, `slug`, `label`, `pw_id`, `sort_order`, `active`)
                        VALUES (?, ?, ?, ?, ?, 1)
                        ON DUPLICATE KEY UPDATE `label` = VALUES(`label`), `pw_id` = VALUES(`pw_id`), `sort_order` = VALUES(`sort_order`), `active` = 1
                        """),
                    cancellationToken,
                    parentId,
                    child.Slug,
                    child.Label.Length == 0 ? child.Slug : child.Label,
                    child.PwId,
                    childOrder).ConfigureAwait(false);
            }
        }
    }

    private static (long CategoryId, long SubcategoryId) ResolveCategory(IReadOnlyList<AccCategory> tree, string category, string subcategory)
    {
        long categoryId = 0;
        long subcategoryId = 0;
        foreach (var parent in tree)
        {
            if (category.Length > 0 && (parent.Slug == category || parent.Id.ToString(CultureInfo.InvariantCulture) == category))
            {
                categoryId = parent.Id;
            }

            foreach (var child in parent.Children)
            {
                if (subcategory.Length > 0 && (child.Slug == subcategory || child.Id.ToString(CultureInfo.InvariantCulture) == subcategory))
                {
                    subcategoryId = child.Id;
                    if (categoryId < 1)
                    {
                        categoryId = parent.Id;
                    }
                }
            }
        }

        return (categoryId, subcategoryId);
    }

    private static async Task<List<AccListing>> ReadListingsAsync(
        DbConnection connection,
        string whereSql,
        string orderSql,
        int perPage,
        int offset,
        List<object?> args,
        CancellationToken cancellationToken)
    {
        var rows = new List<AccListing>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT l.`id`, l.`title`, l.`description`, l.`make`, l.`model`, l.`year`, l.`city`, l.`condition_type`, " +
            "l.`price`, l.`compare_price`, l.`currency`, l.`image_url`, l.`external_url`, l.`photo_count`, l.`featured`, " +
            "l.`stock_qty`, c.`slug`, c.`label`, s.`slug`, s.`label`, l.`updated_at` " +
            "FROM `epc_acc_listings` l " +
            "LEFT JOIN `epc_acc_categories` c ON c.id = l.category_id " +
            "LEFT JOIN `epc_acc_categories` s ON s.id = l.subcategory_id " +
            "WHERE " + whereSql + " ORDER BY " + orderSql +
            " LIMIT " + perPage.ToString(CultureInfo.InvariantCulture) +
            " OFFSET " + offset.ToString(CultureInfo.InvariantCulture));
        ErpDb.AddParameters(command, args.ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new AccListing(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                Text(reader, 1),
                Text(reader, 2),
                Text(reader, 3),
                Text(reader, 4),
                Text(reader, 5),
                Text(reader, 6),
                Text(reader, 7),
                reader.IsDBNull(8) ? 0 : Convert.ToDouble(reader.GetValue(8), CultureInfo.InvariantCulture),
                reader.IsDBNull(9) ? 0 : Convert.ToDouble(reader.GetValue(9), CultureInfo.InvariantCulture),
                Text(reader, 10),
                Text(reader, 11),
                Text(reader, 12),
                reader.IsDBNull(13) ? 0 : Convert.ToInt32(reader.GetValue(13), CultureInfo.InvariantCulture),
                !reader.IsDBNull(14) && Convert.ToInt32(reader.GetValue(14), CultureInfo.InvariantCulture) != 0,
                reader.IsDBNull(15) ? 0 : Convert.ToInt32(reader.GetValue(15), CultureInfo.InvariantCulture),
                Text(reader, 16),
                Text(reader, 17),
                Text(reader, 18),
                Text(reader, 19),
                reader.IsDBNull(20) ? 0 : Convert.ToInt64(reader.GetValue(20), CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static async Task<List<AccPhoto>> PhotosAsync(DbConnection connection, long listingId, CancellationToken cancellationToken)
    {
        var photos = new List<AccPhoto>();
        if (listingId <= 0)
        {
            return photos;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("""
            SELECT `id`, `file_name`, `is_primary`
            FROM `epc_acc_photos`
            WHERE `listing_id` = ?
            ORDER BY `is_primary` DESC, `sort_order` ASC, `id` ASC
            """);
        ErpDb.AddParameters(command, listingId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var file = Text(reader, 1);
            var url = PhotoUrl(file);
            if (url.Length == 0)
            {
                continue;
            }

            photos.Add(new AccPhoto(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                url,
                !reader.IsDBNull(2) && Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture) != 0));
        }

        return photos;
    }

    private static string PhotoUrl(string fileName)
    {
        var name = System.IO.Path.GetFileName(fileName.Trim());
        return name.Length == 0 ? string.Empty : "/content/files/images/accessories/" + Uri.EscapeDataString(name);
    }

    private static async Task<AccFacets> FacetsAsync(
        DbConnection connection,
        IWebHostEnvironment env,
        IReadOnlyList<AccCategory> tree,
        CancellationToken cancellationToken)
    {
        var catCounts = new Dictionary<long, int>();
        var subCounts = new Dictionary<long, int>();
        var makeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var cityCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT category_id, subcategory_id, make, city, COUNT(*) AS cnt
                FROM `epc_acc_listings` WHERE `status` = 'published'
                GROUP BY category_id, subcategory_id, make, city
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var cid = reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                var sid = reader.IsDBNull(1) ? 0 : Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
                var cnt = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture);
                if (cid > 0)
                {
                    catCounts[cid] = catCounts.GetValueOrDefault(cid) + cnt;
                }

                if (sid > 0)
                {
                    subCounts[sid] = subCounts.GetValueOrDefault(sid) + cnt;
                }

                var make = Text(reader, 2);
                if (make.Length > 0)
                {
                    makeCounts[make] = makeCounts.GetValueOrDefault(make) + cnt;
                }

                var city = Text(reader, 3);
                if (city.Length > 0)
                {
                    cityCounts[city] = cityCounts.GetValueOrDefault(city) + cnt;
                }
            }
        }

        var categories = tree.Select(parent => new
        {
            id = parent.Id,
            slug = parent.Slug,
            label = parent.Label,
            count = catCounts.GetValueOrDefault(parent.Id),
            subs = parent.Children.Select(child => new
            {
                id = child.Id,
                slug = child.Slug,
                label = child.Label,
                count = subCounts.GetValueOrDefault(child.Id),
            }).ToList(),
        }).ToList();

        var makeLabels = await TermLabelsAsync(connection, env, "make", cancellationToken).ConfigureAwait(false);
        var cityLabels = await TermLabelsAsync(connection, env, "city", cancellationToken).ConfigureAwait(false);
        var conditions = await ConditionFacetsAsync(connection, cancellationToken).ConfigureAwait(false);
        var facetMakes = new List<object>();
        foreach (var name in makeLabels)
        {
            facetMakes.Add(new { make = name, count = makeCounts.GetValueOrDefault(name) });
        }

        foreach (var pair in makeCounts)
        {
            if (!makeLabels.Contains(pair.Key, StringComparer.Ordinal))
            {
                facetMakes.Add(new { make = pair.Key, count = pair.Value });
            }
        }

        var facetCities = new List<object>();
        foreach (var name in cityLabels)
        {
            facetCities.Add(new { city = name, count = cityCounts.GetValueOrDefault(name) });
        }

        foreach (var pair in cityCounts)
        {
            if (!cityLabels.Contains(pair.Key, StringComparer.Ordinal))
            {
                facetCities.Add(new { city = pair.Key, count = pair.Value });
            }
        }

        return new AccFacets(categories, facetMakes, facetCities, conditions, makeLabels, cityLabels);
    }

    private static async Task<List<string>> TermLabelsAsync(DbConnection connection, IWebHostEnvironment env, string type, CancellationToken cancellationToken)
    {
        var labels = await ReadTermLabelsAsync(connection, type, cancellationToken).ConfigureAwait(false);
        if (labels.Count == 0 && type is "make" or "city")
        {
            await SeedTermsAsync(connection, env, cancellationToken).ConfigureAwait(false);
            labels = await ReadTermLabelsAsync(connection, type, cancellationToken).ConfigureAwait(false);
        }

        if (labels.Count == 0)
        {
            var tax = LoadTaxonomy(env);
            labels = type == "city" ? tax.Cities : tax.Makes;
        }

        return labels;
    }

    private static async Task<List<string>> ReadTermLabelsAsync(DbConnection connection, string type, CancellationToken cancellationToken)
    {
        var labels = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("""
            SELECT `label` FROM `epc_acc_terms`
            WHERE `term_type` = ? AND `active` = 1
            ORDER BY `sort_order` ASC, `label` ASC
            """);
        ErpDb.AddParameters(command, type);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var label = Text(reader, 0);
            if (label.Length > 0)
            {
                labels.Add(label);
            }
        }

        return labels;
    }

    private static async Task SeedTermsAsync(DbConnection connection, IWebHostEnvironment env, CancellationToken cancellationToken)
    {
        var tax = LoadTaxonomy(env);
        var i = 0;
        foreach (var make in tax.Makes)
        {
            i++;
            await UpsertTermAsync(connection, "make", make, make, i, cancellationToken).ConfigureAwait(false);
        }

        i = 0;
        foreach (var city in tax.Cities)
        {
            i++;
            await UpsertTermAsync(connection, "city", city, city, i, cancellationToken).ConfigureAwait(false);
        }

        await UpsertTermAsync(connection, "condition", "new", "New", 1, cancellationToken).ConfigureAwait(false);
        await UpsertTermAsync(connection, "condition", "used", "Used", 2, cancellationToken).ConfigureAwait(false);
        var year = DateTime.UtcNow.Year;
        i = 0;
        for (var y = year; y >= year - 30; y--)
        {
            i++;
            var text = y.ToString(CultureInfo.InvariantCulture);
            await UpsertTermAsync(connection, "year", text, text, i, cancellationToken).ConfigureAwait(false);
        }
    }

    private static Task UpsertTermAsync(DbConnection connection, string type, string value, string label, int sort, CancellationToken cancellationToken)
        => ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `epc_acc_terms` (`term_type`, `parent_id`, `value`, `label`, `sort_order`, `active`)
                VALUES (?, 0, ?, ?, ?, 1)
                ON DUPLICATE KEY UPDATE `label` = VALUES(`label`), `sort_order` = VALUES(`sort_order`)
                """),
            cancellationToken,
            type,
            value,
            label,
            sort);

    private static async Task<List<object>> ConditionFacetsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<object>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT `value`, `label` FROM `epc_acc_terms`
            WHERE `term_type` = 'condition' AND `active` = 1
            ORDER BY `sort_order` ASC, `label` ASC
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new { value = Text(reader, 0), label = Text(reader, 1) });
        }

        if (rows.Count == 0)
        {
            rows.Add(new { value = "new", label = "New" });
            rows.Add(new { value = "used", label = "Used" });
        }

        return rows;
    }

    private static AccTaxonomy LoadTaxonomy(IWebHostEnvironment env)
    {
        var path = System.IO.Path.Combine(PhpLegacyAssetBridge.FindRepoRoot(env), "content", "general_pages", "epc_pakwheels_accessories_taxonomy.json");
        if (!File.Exists(path))
        {
            return new AccTaxonomy([], [], []);
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        var categories = new List<AccTaxCategory>();
        if (root.TryGetProperty("categories", out var cats) && cats.ValueKind == JsonValueKind.Array)
        {
            foreach (var parent in cats.EnumerateArray())
            {
                var children = new List<AccTaxCategory>();
                if (parent.TryGetProperty("children", out var kids) && kids.ValueKind == JsonValueKind.Array)
                {
                    foreach (var child in kids.EnumerateArray())
                    {
                        children.Add(new AccTaxCategory(Str(child, "slug"), Str(child, "label"), Int(child, "pw_id"), []));
                    }
                }

                categories.Add(new AccTaxCategory(Str(parent, "slug"), Str(parent, "label"), Int(parent, "pw_id"), children));
            }
        }

        return new AccTaxonomy(categories, Strings(root, "makes"), Strings(root, "cities"));
    }

    private static List<string> Strings(JsonElement root, string name)
    {
        var list = new List<string>();
        if (!root.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var item in array.EnumerateArray())
        {
            var text = item.GetString()?.Trim() ?? string.Empty;
            if (text.Length > 0)
            {
                list.Add(text);
            }
        }

        return list;
    }

    private static string Str(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? (value.GetString() ?? string.Empty).Trim()
            : string.Empty;

    private static int Int(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;

    private static AccFilters FiltersOf(HttpContext context)
    {
        string Field(string name)
        {
            var query = context.Request.Query[name].ToString();
            if (query.Length > 0)
            {
                return query.Trim();
            }

            return context.Request.HasFormContentType ? context.Request.Form[name].ToString().Trim() : string.Empty;
        }

        var idRaw = Field("id");
        if (idRaw.Length == 0)
        {
            idRaw = Field("listing_id");
        }

        _ = long.TryParse(idRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var listingId);
        _ = int.TryParse(Field("page"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var page);
        _ = double.TryParse(Field("price_min"), NumberStyles.Float, CultureInfo.InvariantCulture, out var priceMin);
        _ = double.TryParse(Field("price_max"), NumberStyles.Float, CultureInfo.InvariantCulture, out var priceMax);
        var sort = Field("sort");
        return new AccFilters(
            Field("q"),
            Field("category"),
            Field("subcategory"),
            Field("make"),
            Field("model"),
            Field("city"),
            Field("condition"),
            priceMin,
            priceMax,
            listingId,
            sort.Length == 0 ? "updated-desc" : sort,
            Math.Max(1, page == 0 ? 1 : page),
            NormalizePerPage(Field("per_page").Length == 0 ? null : Field("per_page")));
    }

    private static string Unavailable()
        => """{"status":false,"message":"Database unavailable"}""";

    private static string CatalogError(HttpContext context, string message)
    {
        if (string.Equals(context.Request.Query["epc_debug"], "1", StringComparison.Ordinal))
        {
            return JsonSerializer.Serialize(new { status = false, message = "Catalog error", debug = message }, Json);
        }

        return """{"status":false,"message":"Catalog error"}""";
    }

    private static IResult Write(string body)
        => new CatalogJsonResult(body);

    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? string.Empty;

    private static string PhpQuery(string value)
        => Uri.EscapeDataString(value).Replace("%20", "+", StringComparison.Ordinal);

    private sealed class CatalogJsonResult(string body) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = StatusCodes.Status200OK;
            httpContext.Response.ContentType = "application/json; charset=utf-8";
            httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
            httpContext.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            await httpContext.Response.WriteAsync(body, Encoding.UTF8).ConfigureAwait(false);
        }
    }

    internal readonly record struct AccFilters(
        string Q,
        string Category,
        string Subcategory,
        string Make,
        string Model,
        string City,
        string Condition,
        double PriceMin,
        double PriceMax,
        long ListingId,
        string Sort,
        int Page,
        int PerPage);

    private sealed record AccCategory(long Id, long ParentId, string Slug, string Label, int PwId, List<AccCategory> Children);

    private sealed record AccListing(
        long Id,
        string Title,
        string Description,
        string Make,
        string Model,
        string Year,
        string City,
        string Condition,
        double Price,
        double ComparePrice,
        string Currency,
        string ImageUrl,
        string ExternalUrl,
        int PhotoCount,
        bool Featured,
        int StockQty,
        string CategorySlug,
        string CategoryLabel,
        string SubcategorySlug,
        string SubcategoryLabel,
        long UpdatedAt);

    private sealed record AccPhoto(long Id, string Url, bool IsPrimary);

    private sealed record AccFacets(
        object Categories,
        object Makes,
        object Cities,
        object Conditions,
        IReadOnlyList<string> MakeLabels,
        IReadOnlyList<string> CityLabels);

    private sealed record AccTaxonomy(IReadOnlyList<AccTaxCategory> Categories, List<string> Makes, List<string> Cities);

    private sealed record AccTaxCategory(string Slug, string Label, int PwId, IReadOnlyList<AccTaxCategory> Children);
}
