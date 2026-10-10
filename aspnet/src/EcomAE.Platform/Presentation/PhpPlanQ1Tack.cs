using MySqlConnector;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-tack product exist-limit cron. Path kept for the inventory:
/// <c>content/cron/product_exist_limit.php</c>.
/// GET never mints a session cookie. Empty <c>IN ()</c> is skipped (PHP would
/// emit invalid SQL and fail the update).
/// </summary>
public static class PhpPlanQ1Tack
{
    public const string ProductExistLimitPath = "content/cron/product_exist_limit.php";

    public static Func<long>? UnixNow { get; set; }
    public static Action<string>? WriteLog { get; set; }

    public static void Reset()
    {
        UnixNow = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        WriteLog = _ => { };
    }

    public static Dictionary<string, object?> EpcProductExistLimitRun(MySqlConnection db)
    {
        using (var reset = db.CreateCommand())
        {
            reset.CommandText = "UPDATE `shop_catalogue_products` SET `min_limit_status` = 0 WHERE `min_limit_status` != '0'";
            reset.ExecuteNonQuery();
        }

        var products = new Dictionary<int, ProductAgg>();
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                SELECT
                `shop_catalogue_products`.`id` AS `product_id`,
                `shop_catalogue_products`.`category_id` AS `product_category_id`,
                `shop_catalogue_products`.`min_limit`,
                `shop_storages_data`.`storage_id`,
                `shop_storages_data`.`category_id`,
                `shop_storages_data`.`exist`
                FROM `shop_catalogue_products` LEFT JOIN `shop_storages_data`
                ON `shop_catalogue_products`.`id` = `shop_storages_data`.`product_id`
                WHERE `shop_catalogue_products`.`min_limit_enable` = '1'
                """;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var productId = ToInt(reader, "product_id");
                var productCategoryId = Convert.ToString(reader["product_category_id"]) ?? "";
                var categoryId = reader["category_id"] is DBNull ? "" : Convert.ToString(reader["category_id"]) ?? "";
                if (productCategoryId != categoryId)
                {
                    continue;
                }

                var storageId = ToInt(reader, "storage_id");
                if (!products.TryGetValue(productId, out var agg))
                {
                    agg = new ProductAgg { MinLimit = ToInt(reader, "min_limit") };
                    products[productId] = agg;
                }

                if (agg.Storages.ContainsKey(storageId))
                {
                    agg.Storages[storageId] += ToInt(reader, "exist");
                }
                else
                {
                    agg.Storages[storageId] = ToInt(reader, "exist");
                }

                agg.MinLimit = ToInt(reader, "min_limit");
            }
        }

        var limited = new List<int>();
        foreach (var (productId, product) in products)
        {
            var total = product.Storages.Values.Sum();
            if (total < product.MinLimit)
            {
                limited.Add(productId);
            }
        }

        var wroteLog = false;
        if (limited.Count > 0)
        {
            using var update = db.CreateCommand();
            var names = new List<string>();
            for (var i = 0; i < limited.Count; i++)
            {
                var name = "@id" + i;
                names.Add(name);
                update.Parameters.AddWithValue(name, limited[i]);
            }

            update.CommandText = "UPDATE `shop_catalogue_products` SET `min_limit_status` = 1 WHERE `id` IN (" + string.Join(",", names) + ")";
            update.ExecuteNonQuery();
            WriteLog?.Invoke((UnixNow != null ? UnixNow() : DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ToString() + "\n\n");
            wroteLog = true;
        }

        var rows = new List<Dictionary<string, object?>>();
        using (var list = db.CreateCommand())
        {
            list.CommandText = "SELECT id, min_limit_status FROM shop_catalogue_products ORDER BY id";
            using var reader = list.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = Convert.ToInt32(reader["id"]),
                    ["min_limit_status"] = Convert.ToString(reader["min_limit_status"]) ?? ""
                });
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["rows"] = rows,
            ["log"] = wroteLog ? 1 : 0
        };
    }

    private static int ToInt(MySqlDataReader reader, string name)
    {
        var raw = reader[name];
        if (raw is DBNull)
        {
            return 0;
        }

        return Convert.ToInt32(raw);
    }

    private sealed class ProductAgg
    {
        public int MinLimit { get; set; }
        public Dictionary<int, int> Storages { get; } = [];
    }
}
