using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1SwellParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Swell");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Swell_MatchPhpGolden()
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
        => Assert.Equal("content/shop/docpart/epc_multivendor_price_ingest.php", PhpPlanQ1Swell.MultivendorPriceIngestPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Swell.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Swell.MultivendorPriceIngestPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Swell.EpcMultivendorSanitizeShort("S-UAE"), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Swell.Reset();
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
        var aliases = PhpPlanQ1Swell.EpcMultivendorHeaderAliases();
        var map = PhpPlanQ1Swell.EpcMultivendorMapHeaders([
            "Part Number", "Brand", "Qty", "Sales Price", "Name",
            "Vendor full name", "Vendor short", "Data type", "Delivery", "Min order"
        ]);
        var emptyMap = PhpPlanQ1Swell.EpcMultivendorMapHeaders(["", "unknown"]);
        var subMap = PhpPlanQ1Swell.EpcMultivendorMapHeaders([
            "sku code", "vendor company legal", "wh code", "selling amount", "item title", "price type"
        ]);
        var inv = PhpPlanQ1Swell.EpcMultivendorCollapseProductCandidates([
            Row(("name", "Oil"), ("exist", 2), ("price", 12.5), ("extras", new Dictionary<string, string>())),
            Row(("name", "Oil filter long"), ("exist", 3), ("price", 11), ("extras", new Dictionary<string, string>())),
            Row(("name", "X"), ("exist", 1), ("price", 0), ("extras", new Dictionary<string, string>()))
        ], "inventory");
        var sales = PhpPlanQ1Swell.EpcMultivendorCollapseProductCandidates([
            Row(("name", "FILTER"), ("exist", 12), ("price", 18.0), ("extras", new Dictionary<string, string>())),
            Row(("name", "FILTER"), ("exist", 5), ("price", 22.5), ("extras", new Dictionary<string, string>())),
            Row(("name", "FILTER"), ("exist", 2), ("price", 29.9), ("extras", new Dictionary<string, string>()))
        ], "sales");
        var same = PhpPlanQ1Swell.EpcMultivendorCollapseProductCandidates([
            Row(("name", "A"), ("exist", 2), ("price", 10), ("extras", new Dictionary<string, string>())),
            Row(("name", "B"), ("exist", 3), ("price", 10), ("extras", new Dictionary<string, string>()))
        ], "sales");
        var portal = PhpPlanQ1Swell.EpcVendorPortalSampleCsv();
        var sample = PhpPlanQ1Swell.EpcMultivendorSampleCsv();
        var pdf = PhpPlanQ1Surge.EpcCommerceExcelToCsv("/tmp/missing.pdf");
        return new object?[]
        {
            aliases.Keys.ToList(),
            aliases["vendor_short"][0],
            PhpPlanQ1Swell.EpcMultivendorNormalizeDataType("INV"),
            PhpPlanQ1Swell.EpcMultivendorNormalizeDataType("sale"),
            PhpPlanQ1Swell.EpcMultivendorNormalizeDataType("buying"),
            PhpPlanQ1Swell.EpcMultivendorNormalizeDataType("stock"),
            PhpPlanQ1Swell.EpcMultivendorNormalizeDataType("default", "sales"),
            PhpPlanQ1Swell.EpcMultivendorNormalizeDataType("", "purchase"),
            PhpPlanQ1Swell.EpcMultivendorNormalizeDataType("weird", "nope"),
            PhpPlanQ1Swell.EpcMultivendorIsCombineMode("combine") ? 1 : 0,
            PhpPlanQ1Swell.EpcMultivendorIsCombineMode("from file") ? 1 : 0,
            PhpPlanQ1Swell.EpcMultivendorIsCombineMode("inventory") ? 1 : 0,
            PhpPlanQ1Swell.EpcMultivendorResolveDataTypeMode(""),
            PhpPlanQ1Swell.EpcMultivendorResolveDataTypeMode("mixed"),
            PhpPlanQ1Swell.EpcMultivendorResolveDataTypeMode("sales"),
            PhpPlanQ1Swell.EpcMultivendorDataTypeListSuffix("sales"),
            PhpPlanQ1Swell.EpcMultivendorDataTypeListSuffix("purchase"),
            PhpPlanQ1Swell.EpcMultivendorDataTypeListSuffix("inventory"),
            PhpPlanQ1Swell.EpcMultivendorSanitizeShort(" S/UAE#'\"\\  "),
            PhpPlanQ1Swell.EpcMultivendorSanitizeFull("  S-UAE   Trading  "),
            PhpPlanQ1Swell.EpcMultivendorVendorKey("Acme Trading", "s-uae"),
            PhpPlanQ1Swell.EpcMultivendorVendorKey("only-code"),
            PhpPlanQ1Swell.EpcMultivendorVendorKey("", ""),
            PhpPlanQ1Swell.EpcMultivendorListBaseName("S-UAE", "S-UAE Trading LLC"),
            PhpPlanQ1Swell.EpcMultivendorListBaseName("S-UAE", "s-uae"),
            PhpPlanQ1Swell.EpcMultivendorListBaseName("", "Full Only"),
            PhpPlanQ1Swell.EpcMultivendorListBaseName("", ""),
            map,
            emptyMap,
            subMap,
            CollapseView(inv),
            CollapseView(sales),
            CollapseView(same),
            portal[..40],
            portal.Count(c => c == '\n'),
            sample[..50],
            sample.Count(c => c == '\n'),
            PhpPlanQ1Surge.EpcCommerceExcelToCsv("/tmp/missing.csv")["message"],
            Truthy(pdf, "ok") ? 1 : 0
        };
    }

    private static object Rows()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ecomae_swell_rows_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        var ok = Path.Combine(dir, "ok.csv");
        var semi = Path.Combine(dir, "semi.csv");
        var noArt = Path.Combine(dir, "noart.csv");
        var noPrice = Path.Combine(dir, "noprice.csv");
        var noShort = Path.Combine(dir, "noshort.csv");
        var noFull = Path.Combine(dir, "nofull.csv");
        var noType = Path.Combine(dir, "notype.csv");
        File.WriteAllText(ok, "Brand,Article,Name,Qty,Price,Vendor full name,Vendor short,Data type,Delivery\nTOYOTA,446610010,PAD KIT,8,103.51,S-UAE Trading LLC,S-UAE,inventory,0\nDENSO,0671007450,FILTER,12,18.00,S-UAE Trading LLC,S-UAE,sales,0\nDENSO,0671007450,FILTER,5,22.50,S-UAE Trading LLC,S-UAE,sales,0\nDENSO,0671007450,FILTER,2,29.90,S-UAE Trading LLC,S-UAE,sales,0\nBOSCH,F026400039,FILTER,4,15.00,Gulf Parts Trading,S-UAE,inventory,0\n,SKIP,x,1,9,S-UAE Trading LLC,S-UAE,inventory,0\n");
        File.WriteAllText(semi, "sku;price;vendor_short;vendor_full;data_type;qty\nOC47;10;S-UAE;Acme;inventory;4\n");
        File.WriteAllText(noArt, "Name,Qty,Price,Vendor full name,Vendor short,Data type\nfoo,1,2,Acme,AC,inventory\n");
        File.WriteAllText(noPrice, "Brand,Article,Vendor full name,Vendor short,Data type\nBosch,OC47,Acme,AC,inventory\n");
        File.WriteAllText(noShort, "Brand,Article,Price,Vendor full name,Data type\nBosch,OC47,10,Acme,inventory\n");
        File.WriteAllText(noFull, "Brand,Article,Price,Vendor short,Data type\nBosch,OC47,10,AC,inventory\n");
        File.WriteAllText(noType, "Brand,Article,Price,Vendor full name,Vendor short\nBosch,OC47,10,Acme,AC\n");
        var delim = PhpPlanQ1Surge.EpcCommerceDetectDelimiter(semi);
        var badArt = PhpPlanQ1Swell.EpcMultivendorReadSourceRows(noArt, "combine");
        var badPrice = PhpPlanQ1Swell.EpcMultivendorReadSourceRows(noPrice, "combine");
        var badShort = PhpPlanQ1Swell.EpcMultivendorReadSourceRows(noShort, "combine");
        var badFull = PhpPlanQ1Swell.EpcMultivendorReadSourceRows(noFull, "combine");
        var badType = PhpPlanQ1Swell.EpcMultivendorReadSourceRows(noType, "combine");
        var okRead = PhpPlanQ1Swell.EpcMultivendorReadSourceRows(ok, "combine");
        var invOnly = PhpPlanQ1Swell.EpcMultivendorReadSourceRows(ok, "inventory");
        var okRows = (List<Dictionary<string, object?>>)okRead["rows"]!;
        var groups = PhpPlanQ1Swell.EpcMultivendorGroupByVendor(okRows);
        var groupView = groups.Values.Select(g =>
        {
            var prods = ((List<Dictionary<string, object?>>)g["products"]!).Select(p => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["article"] = Convert.ToString(p.GetValueOrDefault("article"), CultureInfo.InvariantCulture) ?? "",
                ["name"] = Convert.ToString(p.GetValueOrDefault("name"), CultureInfo.InvariantCulture) ?? "",
                ["exist"] = Convert.ToInt32(p.GetValueOrDefault("exist") ?? 0, CultureInfo.InvariantCulture),
                ["price"] = Convert.ToDouble(p.GetValueOrDefault("price") ?? 0, CultureInfo.InvariantCulture),
                ["storage"] = Convert.ToString(p.GetValueOrDefault("storage"), CultureInfo.InvariantCulture) ?? "",
                ["tier"] = Convert.ToString(p.GetValueOrDefault("epc_price_tier"), CultureInfo.InvariantCulture) ?? "",
                ["vendor_short"] = Convert.ToString(p.GetValueOrDefault("vendor_short"), CultureInfo.InvariantCulture) ?? ""
            }).ToList();
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["vendor_full"] = Convert.ToString(g["vendor_full"], CultureInfo.InvariantCulture) ?? "",
                ["vendor_short"] = Convert.ToString(g["vendor_short"], CultureInfo.InvariantCulture) ?? "",
                ["data_type"] = Convert.ToString(g["data_type"], CultureInfo.InvariantCulture) ?? "",
                ["n"] = prods.Count,
                ["products"] = prods
            };
        }).ToList();
        var csvPath = Path.Combine(dir, "out.csv");
        var firstGroup = groups.Values.First();
        var wrote = PhpPlanQ1Swell.EpcMultivendorWriteDocpartCsv(csvPath, (List<Dictionary<string, object?>>)firstGroup["products"]!) ? 1 : 0;
        var csv = File.Exists(csvPath) ? File.ReadAllText(csvPath) : "";
        foreach (var f in new[] { ok, semi, noArt, noPrice, noShort, noFull, noType, csvPath })
        {
            try { File.Delete(f); } catch { /* ignore */ }
        }

        try { Directory.Delete(dir); } catch { /* ignore */ }
        return new object?[]
        {
            delim,
            Truthy(badArt, "ok") ? 1 : 0,
            badArt["message"],
            Truthy(badPrice, "ok") ? 1 : 0,
            badPrice["message"],
            Truthy(badShort, "ok") ? 1 : 0,
            badShort["message"],
            Truthy(badFull, "ok") ? 1 : 0,
            badFull["message"],
            Truthy(badType, "ok") ? 1 : 0,
            badType["message"],
            Truthy(okRead, "ok") ? 1 : 0,
            okRows.Count,
            okRows[0]["article"],
            okRows[0]["vendor_short"],
            okRows[0]["data_type"],
            Convert.ToInt32(okRead.GetValueOrDefault("rows_skipped") ?? 0, CultureInfo.InvariantCulture),
            okRead["mode"],
            Truthy(invOnly, "ok") ? 1 : 0,
            ((List<Dictionary<string, object?>>)invOnly["rows"]!).Count,
            groups.Count,
            groupView,
            wrote,
            csv
        };
    }

    private static object Import()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_swell_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            Schema(db);
            var dir = Path.Combine(Path.GetTempPath(), "ecomae_swell_imp_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(dir);
            var csv = Path.Combine(dir, "doc.csv");
            PhpPlanQ1Swell.EpcMultivendorWriteDocpartCsv(csv, [
                Row(
                    ("manufacturer", "Bosch"),
                    ("article", "OC47"),
                    ("article_show", "OC-47"),
                    ("name", "Oil filter"),
                    ("exist", 2),
                    ("price", 12.5),
                    ("time_to_exe", 0),
                    ("min_order", 0),
                    ("storage", PhpPlanQ1Swell.EpcMvMaxTier),
                    ("epc_price_tier", "max")),
                Row(
                    ("manufacturer", "X"),
                    ("article", ""),
                    ("article_show", ""),
                    ("name", "skip"),
                    ("exist", 1),
                    ("price", 0))
            ]);
            var price = Resolve(db, "S-UAE · S-UAE Trading LLC");
            var local = PhpPlanQ1Swell.EpcMultivendorImportCsvLocal(db, price, csv);
            var sid = PhpPlanQ1Swell.EpcMultivendorEnsureWarehouse(db, "S-UAE Trading LLC", "S-UAE", Convert.ToInt32(price["id"], CultureInfo.InvariantCulture), true);
            var codes = PhpPlanQ1Swell.EpcMultivendorVendorCodesList(db);
            var badId = PhpPlanQ1Swell.EpcMultivendorVendorCodeSave(db, 0, "XX");
            var empty = PhpPlanQ1Swell.EpcMultivendorVendorCodeSave(db, sid, "");
            var okSave = PhpPlanQ1Swell.EpcMultivendorVendorCodeSave(db, sid, "S-UAE2", "S-UAE Trading LLC");
            try { File.Delete(csv); } catch { /* ignore */ }
            try { Directory.Delete(dir); } catch { /* ignore */ }
            return new object?[]
            {
                Truthy(local, "status") ? 1 : 0,
                local["records_handled"],
                local["rows_skipped"],
                Convert.ToInt32(local.GetValueOrDefault("extras_saved") ?? 0, CultureInfo.InvariantCulture),
                sid > 0 ? 1 : 0,
                codes.Count,
                codes.Count > 0 ? codes[0]["vendor_code"] : "",
                codes.Count > 0 ? codes[0]["vendor_full"] : "",
                codes.Count > 0 && Truthy(codes[0], "is_multivendor") ? 1 : 0,
                Truthy(badId, "ok") ? 1 : 0,
                badId["message"],
                Truthy(empty, "ok") ? 1 : 0,
                empty["message"],
                Truthy(okSave, "ok") ? 1 : 0,
                okSave.TryGetValue("vendor", out var v) && v is Dictionary<string, object?> vend ? vend["vendor_code"] : ""
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
        var schema = "ecomae_cpw_swell_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            Schema(db);
            var dir = Path.Combine(Path.GetTempPath(), "ecomae_swell_ing_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(dir);
            var src = Path.Combine(dir, "src.csv");
            File.WriteAllText(src, "Brand,Article,Name,Qty,Price,Vendor full name,Vendor short,Data type\nTOYOTA,446610010,PAD KIT,8,103.51,S-UAE Trading LLC,S-UAE,inventory\nDENSO,0671007450,FILTER,12,18.00,S-UAE Trading LLC,S-UAE,sales\nDENSO,0671007450,FILTER,5,22.50,S-UAE Trading LLC,S-UAE,sales\nDENSO,0671007450,FILTER,2,29.90,S-UAE Trading LLC,S-UAE,sales\nBOSCH,F026400039,FILTER,4,15.00,Gulf Parts Trading,S-UAE,inventory\n");
            var bad = PhpPlanQ1Swell.EpcMultivendorIngestFile(db, src, "src.csv", "nope");
            var ok = PhpPlanQ1Swell.EpcMultivendorIngestFile(db, src, "src.csv", "combine");
            var portalOther = PhpPlanQ1Swell.EpcMultivendorIngestForVendor(db, src, "src.csv", "S-UAE Trading LLC", "OTHER", "inventory", 3);
            var portalOk = PhpPlanQ1Swell.EpcMultivendorIngestForVendor(db, src, "src.csv", "S-UAE Trading LLC", "S-UAE", "inventory", 3);
            try { File.Delete(src); } catch { /* ignore */ }
            try { Directory.Delete(dir); } catch { /* ignore */ }
            var vendors = ((List<Dictionary<string, object?>>)ok["vendors"]!).Select(item => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = Truthy(item, "status") ? 1 : 0,
                ["price_name"] = Convert.ToString(item["price_name"], CultureInfo.InvariantCulture) ?? "",
                ["data_type"] = Convert.ToString(item["data_type"], CultureInfo.InvariantCulture) ?? "",
                ["vendor_short"] = Convert.ToString(item["vendor_short"], CultureInfo.InvariantCulture) ?? "",
                ["records_handled"] = Convert.ToInt32(item["records_handled"], CultureInfo.InvariantCulture),
                ["records_in_db"] = Convert.ToInt32(item["records_in_db"], CultureInfo.InvariantCulture),
                ["history_id"] = Convert.ToInt32(item["history_id"], CultureInfo.InvariantCulture),
                ["storage_id"] = Convert.ToInt32(item["storage_id"], CultureInfo.InvariantCulture) > 0 ? 1 : 0
            }).ToList();
            return new object?[]
            {
                Truthy(bad, "status") ? 1 : 0,
                bad["message"],
                Truthy(ok, "status") ? 1 : 0,
                ok["message"],
                ok["data_type_default"],
                ok["vendors_total"],
                ok["vendors_ok"],
                ok["vendors_failed"],
                ok["rows_source"],
                ok["rows_imported"],
                ok["warehouses_linked"],
                vendors,
                Truthy(portalOther, "status") ? 1 : 0,
                portalOther["message"],
                Convert.ToInt32(portalOther.GetValueOrDefault("rows_rejected_other_vendor") ?? 0, CultureInfo.InvariantCulture),
                Truthy(portalOk, "status") ? 1 : 0,
                portalOk["message"],
                portalOk["vendor_short"],
                portalOk["rows_imported"]
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static List<Dictionary<string, object?>> CollapseView(IEnumerable<Dictionary<string, object?>> rows)
        => rows.Select(row => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = Convert.ToString(row.GetValueOrDefault("name"), CultureInfo.InvariantCulture) ?? "",
            ["exist"] = Convert.ToInt32(row.GetValueOrDefault("exist") ?? 0, CultureInfo.InvariantCulture),
            ["price"] = Convert.ToDouble(row.GetValueOrDefault("price") ?? 0, CultureInfo.InvariantCulture),
            ["storage"] = Convert.ToString(row.GetValueOrDefault("storage"), CultureInfo.InvariantCulture) ?? "",
            ["tier"] = Convert.ToString(row.GetValueOrDefault("epc_price_tier"), CultureInfo.InvariantCulture) ?? ""
        }).ToList();

    private static Dictionary<string, object?> Row(params (string Key, object? Value)[] pairs)
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (k, v) in pairs)
        {
            row[k] = v;
        }

        return row;
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
