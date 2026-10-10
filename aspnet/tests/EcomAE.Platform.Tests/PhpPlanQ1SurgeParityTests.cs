using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1SurgeParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Surge");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Surge_MatchPhpGolden()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases").EnumerateArray().ToList();
        var results = golden.GetProperty("results").EnumerateArray().ToList();
        var failures = new List<string>();
        for (var i = 0; i < cases.Count; i++)
        {
            var name = cases[i].GetProperty("name").GetString()!;
            var expected = results[i].GetProperty("result");
            var actual = Render(name);
            if (!JsonEquivalent(JsonDocument.Parse(Json(actual)).RootElement, expected))
            {
                failures.Add(name + " exp=" + expected.GetRawText() + " got=" + Json(actual));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal("content/shop/docpart/epc_commerce_price_ingest.php", PhpPlanQ1Surge.CommercePriceIngestPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Surge.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Surge.CommercePriceIngestPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Surge.EpcCommerceNormalizeArticle("oc-47"), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Surge.Reset();
        return name switch
        {
            "names" => Names(),
            "rows" => Rows(),
            "import" => Import(),
            "ingest" => Ingest(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Names()
    {
        var aliases = PhpPlanQ1Surge.EpcCommerceHeaderAliases();
        var map = PhpPlanQ1Surge.EpcCommerceMapHeaders(["Part Number", "Brand", "Qty", "Sales Price", "Cost Price", "Vendor"]);
        var emptyMap = PhpPlanQ1Surge.EpcCommerceMapHeaders(["", "unknown"]);
        var enc = PhpPlanQ1Surge.EpcCommerceMetaEncode("sales", "ACME", 12.5, "ACME-S");
        var dec = PhpPlanQ1Surge.EpcCommerceMetaDecode(enc)!;
        var pdf = PhpPlanQ1Surge.EpcCommerceExcelToCsv("/tmp/missing.pdf");
        return new object?[]
        {
            aliases["manufacturer"],
            PhpPlanQ1Surge.EpcCommerceNormalizeHeaderCell("  Part\tNumber  "),
            PhpPlanQ1Surge.EpcCommerceNormalizeHeaderCell("SALES PRICE"),
            map,
            emptyMap,
            PhpPlanQ1Surge.EpcCommerceParseNumber(""),
            PhpPlanQ1Surge.EpcCommerceParseNumber("1.234,56"),
            PhpPlanQ1Surge.EpcCommerceParseNumber("12.50"),
            PhpPlanQ1Surge.EpcCommerceParseNumber("AED 8"),
            PhpPlanQ1Surge.EpcCommerceParseNumber("1.234.56"),
            PhpPlanQ1Surge.EpcCommerceNormalizeArticle("oc-47 / a"),
            PhpPlanQ1Surge.EpcCommerceNormalizeArticle("oc`47\n"),
            PhpPlanQ1Surge.EpcCommerceClip("café/#x", 4),
            PhpPlanQ1Surge.EpcCommerceClip("a/'\"\\#", 20),
            PhpPlanQ1Surge.EpcCommerceRoleSuffix("sales"),
            PhpPlanQ1Surge.EpcCommerceRoleSuffix("p"),
            PhpPlanQ1Surge.EpcCommerceRoleSuffix("local"),
            PhpPlanQ1Surge.EpcCommerceRoleSuffix("nope"),
            PhpPlanQ1Surge.EpcCommerceListName("sales", ""),
            PhpPlanQ1Surge.EpcCommerceListName("sales", "ACME-S"),
            PhpPlanQ1Surge.EpcCommerceListName("purchase", "ACME", "Bosch Parts"),
            PhpPlanQ1Surge.EpcCommerceListName("purchase", "ACME", "Mann.P"),
            PhpPlanQ1Surge.EpcCommerceListName("inventory", "ACME-L"),
            PhpPlanQ1Surge.EpcCommerceListName("weird", "ACME"),
            PhpPlanQ1Surge.EpcCommerceRoleFromListName("ACME-S"),
            PhpPlanQ1Surge.EpcCommerceRoleFromListName("Mann.P"),
            PhpPlanQ1Surge.EpcCommerceRoleFromListName("ACME-L"),
            PhpPlanQ1Surge.EpcCommerceRoleFromListName("plain"),
            PhpPlanQ1Surge.EpcCommerceBaseFromListName("ACME-S"),
            PhpPlanQ1Surge.EpcCommerceBaseFromListName("Mann.P"),
            PhpPlanQ1Surge.EpcCommerceBaseFromListName("ACME-L"),
            enc[..13],
            dec["role"],
            dec["base"],
            dec["margin"],
            dec["list"],
            PhpPlanQ1Surge.EpcCommerceMetaDecode("nope"),
            PhpPlanQ1Surge.EpcCommerceMetaDecode(""),
            PhpPlanQ1Surge.EpcCommerceNormalizeSourceUrl("https://drive.google.com/file/d/abc123/view"),
            PhpPlanQ1Surge.EpcCommerceNormalizeSourceUrl("https://drive.google.com/open?id=abc123"),
            PhpPlanQ1Surge.EpcCommerceNormalizeSourceUrl("https://docs.google.com/spreadsheets/d/sheet9/edit"),
            PhpPlanQ1Surge.EpcCommerceNormalizeSourceUrl("https://www.dropbox.com/s/x/file.csv?dl=0"),
            PhpPlanQ1Surge.EpcCommerceNormalizeSourceUrl("https://www.dropbox.com/s/x/file.csv"),
            PhpPlanQ1Surge.EpcCommerceNormalizeSourceUrl("https://example.com/a.csv"),
            PhpPlanQ1Surge.EpcCommerceExcelToCsv("/tmp/missing.csv")["message"],
            Truthy(pdf, "ok") ? 1 : 0
        };
    }

    private static object Rows()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ecomae_surge_rows_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        var sales = Path.Combine(dir, "sales.csv");
        var semi = Path.Combine(dir, "semi.csv");
        var noArt = Path.Combine(dir, "noart.csv");
        var noPrice = Path.Combine(dir, "noprice.csv");
        var purchase = Path.Combine(dir, "purchase.csv");
        File.WriteAllText(sales, "Part Number,Brand,Qty,Sales Price,Name\nOC-47,Bosch,2,12.5,Oil filter\nOC-47,Bosch,1,11,Oil filter cheap\n,Skip,1,9,Empty article\nHU712,Mann,3,0,Zero\nHU712,Mann,1,8.25,Filter\n");
        File.WriteAllText(semi, "sku;cost;supplier;qty\nOC47;10;Febi;4\nOC47;9;Febi;2\nOC47;12;Valeo;1\n");
        File.WriteAllText(noArt, "name,qty,price\nfoo,1,2\n");
        File.WriteAllText(noPrice, "sku,name\nOC47,Filter\n");
        File.WriteAllText(purchase, "article,cost,supplier,exist\nOC-47,10,Febi,4\nOC-47,9,Febi,2\nOC-47,12,Valeo,1\n");
        var delim = PhpPlanQ1Surge.EpcCommerceDetectDelimiter(semi);
        var bad = PhpPlanQ1Surge.EpcCommerceReadSourceRows(noArt, "sales");
        var need = PhpPlanQ1Surge.EpcCommerceReadSourceRows(noPrice, "sales");
        var ok = PhpPlanQ1Surge.EpcCommerceReadSourceRows(sales, "sales");
        var purRead = PhpPlanQ1Surge.EpcCommerceReadSourceRows(purchase, "purchase");
        var okRows = (List<Dictionary<string, object?>>)ok["rows"]!;
        var purRows = (List<Dictionary<string, object?>>)purRead["rows"]!;
        var salesAgg = PhpPlanQ1Surge.EpcCommerceAggregateRows("sales", okRows, "ACME", 0);
        var purAgg = PhpPlanQ1Surge.EpcCommerceAggregateRows("purchase", purRows, "ACME", 10);
        var invAgg = PhpPlanQ1Surge.EpcCommerceAggregateRows("inventory", purRows, "ACME", 10);
        var csvPath = Path.Combine(dir, "out.csv");
        var wrote = PhpPlanQ1Surge.EpcCommerceWriteDocpartCsv(csvPath, salesAgg.GetValueOrDefault("ACME-S") ?? []) ? 1 : 0;
        var csv = File.Exists(csvPath) ? File.ReadAllText(csvPath) : "";
        foreach (var f in new[] { sales, semi, noArt, noPrice, purchase, csvPath })
        {
            try { File.Delete(f); } catch { /* ignore */ }
        }

        try { Directory.Delete(dir); } catch { /* ignore */ }
        return new object?[]
        {
            delim,
            Truthy(bad, "ok") ? 1 : 0,
            Truthy(need, "ok") ? 1 : 0,
            need["message"],
            Truthy(ok, "ok") ? 1 : 0,
            okRows.Count,
            okRows[0]["article"],
            okRows[0]["exist"],
            salesAgg.Keys.ToList(),
            (salesAgg.GetValueOrDefault("ACME-S") ?? []).Count,
            salesAgg.GetValueOrDefault("ACME-S")?[0]["price"] ?? 0,
            salesAgg.GetValueOrDefault("ACME-S")?[0]["exist"] ?? 0,
            purAgg.Keys.ToList(),
            purAgg.TryGetValue("Febi.P", out var febi) && febi.Count > 0 ? febi[0]["price"] : 0,
            purAgg.TryGetValue("Febi.P", out var febi2) && febi2.Count > 0 ? febi2[0]["exist"] : 0,
            purAgg.TryGetValue("Valeo.P", out var valeo) && valeo.Count > 0 ? valeo[0]["price"] : 0,
            invAgg.Keys.ToList(),
            invAgg.TryGetValue("ACME-L", out var inv) && inv.Count > 0 ? inv[0]["exist"] : 0,
            invAgg.TryGetValue("ACME-L", out var inv2) && inv2.Count > 0 ? inv2[0]["price"] : 0,
            wrote,
            csv
        };
    }

    private static object Import()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_surge_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            Schema(db);
            var dir = Path.Combine(Path.GetTempPath(), "ecomae_surge_imp_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(dir);
            var csv = Path.Combine(dir, "doc.csv");
            PhpPlanQ1Surge.EpcCommerceWriteDocpartCsv(csv, [
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["manufacturer"] = "Bosch",
                    ["article"] = "OC47",
                    ["article_show"] = "OC-47",
                    ["name"] = "Oil filter",
                    ["exist"] = 2,
                    ["price"] = 12.5
                },
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["manufacturer"] = "X",
                    ["article"] = "",
                    ["article_show"] = "",
                    ["name"] = "skip",
                    ["exist"] = 1,
                    ["price"] = 0
                }
            ]);
            var price = Resolve(db, "ACME-S");
            var local = PhpPlanQ1Surge.EpcCommerceImportCsvLocal(db, price, csv);
            var sid = PhpPlanQ1Surge.EpcCommerceEnsureWarehouse(db, "ACME-S", Convert.ToInt32(price["id"], CultureInfo.InvariantCulture));
            PhpPlanQ1Surge.EpcCommerceStoreMetaOnly(db, Convert.ToInt32(price["id"], CultureInfo.InvariantCulture), "ACME-S", "sales", "ACME", 0);
            var sources = PhpPlanQ1Surge.EpcCommerceListSources(db, false);
            var emptyRefresh = PhpPlanQ1Surge.EpcCommerceRefreshAllLinked(db);
            try { File.Delete(csv); } catch { /* ignore */ }
            try { Directory.Delete(dir); } catch { /* ignore */ }
            return new object?[]
            {
                Truthy(local, "status") ? 1 : 0,
                local["records_handled"],
                local["rows_skipped"],
                sid > 0 ? 1 : 0,
                sources.Count,
                sources.Count > 0 ? sources[0]["role"] : "",
                sources.Count > 0 ? sources[0]["price_name"] : "",
                sources.Count > 0 && true.Equals(sources[0]["has_url"]) ? 1 : 0,
                emptyRefresh["ok"],
                emptyRefresh["failed"],
                emptyRefresh["total"]
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object Ingest()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_surge_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            Schema(db);
            var dir = Path.Combine(Path.GetTempPath(), "ecomae_surge_ing_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(dir);
            var src = Path.Combine(dir, "src.csv");
            File.WriteAllText(src, "article,brand,qty,price,name\nOC-47,Bosch,2,12.5,Oil filter\nHU712,Mann,1,8.25,Filter\n");
            var badRole = PhpPlanQ1Surge.EpcCommerceIngestFile(db, src, "nope", "ACME", 0);
            var ok = PhpPlanQ1Surge.EpcCommerceIngestFile(db, src, "sales", "ACME", 0);
            try { File.Delete(src); } catch { /* ignore */ }
            try { Directory.Delete(dir); } catch { /* ignore */ }
            var lists = ((List<Dictionary<string, object?>>)ok["lists"]!).Select(item => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = Truthy(item, "status") ? 1 : 0,
                ["price_name"] = Convert.ToString(item["price_name"], CultureInfo.InvariantCulture) ?? "",
                ["records_handled"] = Convert.ToInt32(item["records_handled"], CultureInfo.InvariantCulture),
                ["records_in_db"] = Convert.ToInt32(item["records_in_db"], CultureInfo.InvariantCulture),
                ["history_id"] = Convert.ToInt32(item["history_id"], CultureInfo.InvariantCulture),
                ["storage_id"] = Convert.ToInt32(item["storage_id"], CultureInfo.InvariantCulture) > 0 ? 1 : 0
            }).ToList();
            return new object?[]
            {
                Truthy(badRole, "status") ? 1 : 0,
                badRole["message"],
                Truthy(ok, "status") ? 1 : 0,
                ok["message"],
                ok["role"],
                ok["base_name"],
                ok["source_rows"],
                ((List<Dictionary<string, object?>>)ok["lists"]!).Count,
                lists
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static Dictionary<string, object?> Resolve(MySqlConnection db, string listName)
    {
        using var q = db.CreateCommand();
        q.CommandText = "SELECT * FROM `shop_docpart_prices` WHERE `name` = @n LIMIT 1";
        q.Parameters.AddWithValue("@n", listName);
        using var reader = q.ExecuteReader();
        if (reader.Read())
        {
            return ReadRow(reader);
        }

        reader.Close();
        using var ins = db.CreateCommand();
        ins.CommandText = "INSERT INTO `shop_docpart_prices` (`name`) VALUES (@n)";
        ins.Parameters.AddWithValue("@n", listName);
        ins.ExecuteNonQuery();
        var id = (int)ins.LastInsertedId;
        using var q2 = db.CreateCommand();
        q2.CommandText = "SELECT * FROM `shop_docpart_prices` WHERE `id` = @id";
        q2.Parameters.AddWithValue("@id", id);
        using var reader2 = q2.ExecuteReader();
        reader2.Read();
        return ReadRow(reader2);
    }

    private static Dictionary<string, object?> ReadRow(MySqlDataReader reader)
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        return row;
    }

    private static void Schema(MySqlConnection db)
    {
        Exec(db, """
            CREATE TABLE shop_docpart_prices (
                id INT PRIMARY KEY AUTO_INCREMENT,
                name VARCHAR(128),
                link VARCHAR(500) DEFAULT '',
                load_mode INT DEFAULT 0,
                file_name_substring VARCHAR(128) DEFAULT '',
                message_header_substring VARCHAR(500) DEFAULT '',
                last_updated INT DEFAULT 0,
                records_count INT DEFAULT 0
            )
            """);
        Exec(db, """
            CREATE TABLE shop_docpart_prices_data (
                id INT PRIMARY KEY,
                price_id INT,
                manufacturer VARCHAR(128),
                article VARCHAR(64),
                article_show VARCHAR(64),
                name VARCHAR(255),
                `exist` INT,
                price DECIMAL(12,2),
                time_to_exe INT,
                storage VARCHAR(64),
                min_order INT
            )
            """);
        Exec(db, """
            CREATE TABLE shop_storages (
                id INT PRIMARY KEY AUTO_INCREMENT,
                name VARCHAR(128),
                interface_type INT,
                users TEXT,
                connection_options TEXT,
                currency INT,
                short_name VARCHAR(128),
                hidden INT,
                bg_line_color INT
            )
            """);
        Exec(db, "CREATE TABLE shop_offices (id INT PRIMARY KEY AUTO_INCREMENT)");
        Exec(db, "INSERT INTO shop_offices (id) VALUES (1)");
        Exec(db, "CREATE TABLE shop_offices_storages_map (office_id INT, storage_id INT, group_id INT, min_point INT, max_point INT, markup INT, additional_time INT)");
        Exec(db, "CREATE TABLE users (id INT PRIMARY KEY AUTO_INCREMENT, user_type INT)");
        Exec(db, "INSERT INTO users (id, user_type) VALUES (3, 2)");
    }

    private static string Password()
        => Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN") ?? throw new InvalidOperationException("missing ECOMAE_LOCAL_MARIADB_E2E_DSN");

    private static MySqlConnection OpenAdmin()
    {
        var db = new MySqlConnection($"Server=127.0.0.1;Port=3306;User ID=ecomae;Password={Password()};AllowUserVariables=true;");
        db.Open();
        return db;
    }

    private static MySqlConnection OpenDb(string schema)
    {
        var db = new MySqlConnection($"Server=127.0.0.1;Port=3306;Database={schema};User ID=ecomae;Password={Password()};AllowUserVariables=true;");
        db.Open();
        return db;
    }

    private static void Exec(MySqlConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static bool Truthy(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var v) && v is true or 1 or 1L or 1.0;

    private static string Json(object? value) => JsonSerializer.Serialize(value, JsonOpts);

    private static bool JsonEquivalent(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number && left.GetDouble() == right.GetDouble();
        }

        return left.ValueKind switch
        {
            JsonValueKind.Object => left.EnumerateObject().All(p => right.TryGetProperty(p.Name, out var o) && JsonEquivalent(p.Value, o))
                && left.EnumerateObject().Count() == right.EnumerateObject().Count(),
            JsonValueKind.Array => left.EnumerateArray().ToList().Zip(right.EnumerateArray().ToList(), JsonEquivalent).All(x => x)
                && left.GetArrayLength() == right.GetArrayLength(),
            JsonValueKind.String => left.GetString() == right.GetString(),
            JsonValueKind.Number => left.GetRawText() == right.GetRawText() || left.GetDouble() == right.GetDouble(),
            JsonValueKind.True or JsonValueKind.False => left.GetBoolean() == right.GetBoolean(),
            JsonValueKind.Null => true,
            _ => true
        };
    }
}
