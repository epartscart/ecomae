using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1HavenParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Haven");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Haven_MatchPhpGolden()
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
        => Assert.Equal("content/general_pages/epc_electronicae_storefront.php", PhpPlanQ1Haven.ElectronicaeStorefrontPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Haven.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Haven.ElectronicaeStorefrontPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Haven.EpcElectronicaeHref("/"), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Haven.Reset();
        return name switch
        {
            "names" => Names(),
            "tree" => Tree(),
            "catalog" => Catalog(),
            "tiles" => Tiles(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static void BindApe()
    {
        PhpPlanQ1Haven.ResolveIndustry = (_, _) => "electronics";
        PhpPlanQ1Haven.CategorySlug = (_, _) => "apai-electronics-root";
        PhpPlanQ1Haven.ProductImageUrl = path => "/img/" + path.TrimStart('/');
        PhpPlanQ1Haven.CacheRemember = (_, _, fn) => fn();
    }

    private static object Names()
    {
        using var admin = OpenAdmin();
        PhpPlanQ1Haven.IsElectronicsRetail = () => false;
        var off = PhpPlanQ1Haven.EpcElectronicaeStorefrontActive() ? 1 : 0;
        PhpPlanQ1Haven.IsElectronicsRetail = () => true;
        var on = PhpPlanQ1Haven.EpcElectronicaeStorefrontActive() ? 1 : 0;
        PhpPlanQ1Haven.ResolveStorefrontSiteKey = () => "";
        var fallback = PhpPlanQ1Haven.EpcElectronicaeSiteKey(admin);
        PhpPlanQ1Haven.ResolveStorefrontSiteKey = () => "acme_el";
        var site = PhpPlanQ1Haven.EpcElectronicaeSiteKey(admin);
        PhpPlanQ1Haven.LangPrefix = null;
        var langDef = PhpPlanQ1Haven.EpcElectronicaeLangPrefix();
        PhpPlanQ1Haven.LangPrefix = () => "/ar/";
        var langAr = PhpPlanQ1Haven.EpcElectronicaeLangPrefix();
        var hrefRoot = PhpPlanQ1Haven.EpcElectronicaeHref("/");
        var hrefPath = PhpPlanQ1Haven.EpcElectronicaeHref("/phones");
        var hrefRel = PhpPlanQ1Haven.EpcElectronicaeHref("phones");
        var hrefLang = PhpPlanQ1Haven.EpcElectronicaeHref("/x", "/en/");
        PhpPlanQ1Haven.LangPrefix = () => "/en";
        PhpPlanQ1Haven.ProductImageUrl = path => "/img/" + path.TrimStart('/');
        return new object?[]
        {
            off, on, fallback, site, langDef, langAr,
            hrefRoot, hrefPath, hrefRel, hrefLang,
            PhpPlanQ1Haven.EpcElectronicaePreferredLineSlugs(),
            PhpPlanQ1Haven.EpcElectronicaeLineVisual("cell-phones-5g"),
            PhpPlanQ1Haven.EpcElectronicaeLineVisual("obscure-gadget"),
            PhpPlanQ1Haven.EpcElectronicaeLineVisual("computers-laptops"),
            PhpPlanQ1Haven.EpcElectronicaeNormalizeImageUrl(""),
            PhpPlanQ1Haven.EpcElectronicaeNormalizeImageUrl("https://cdn.test/a.png"),
            PhpPlanQ1Haven.EpcElectronicaeNormalizeImageUrl("files/a.png"),
            PhpPlanQ1Haven.EpcElectronicaeRenderEmptyCategory()
        };
    }

    private static object Tree()
    {
        using var admin = OpenAdmin();
        BindApe();
        PhpPlanQ1Haven.ResolveStorefrontSiteKey = () => "acme_el";
        var tree = new List<Dictionary<string, object?>>
        {
            Node("tires"),
            Node("apai-electronics-root", [Node("apai-phones"), Node("apai-tv")]),
            Node("orphans")
        };
        var extracted = PhpPlanQ1Haven.EpcElectronicaeFilterMenuTree(admin, tree, "acme_el").Select(Alias).ToList();
        var emptyKids = PhpPlanQ1Haven.EpcElectronicaeFilterMenuTree(admin, [Node("apai-electronics-root"), Node("other")], "acme_el").Select(Alias).ToList();
        var apaiOnly = PhpPlanQ1Haven.EpcElectronicaeFilterMenuTree(admin, [Node("tires"), Node("apai-phones"), Node("apai-tv")], "acme_el").Select(Alias).ToList();
        var pass = PhpPlanQ1Haven.EpcElectronicaeFilterMenuTree(admin, [Node("tires"), Node("batteries")], "acme_el").Select(Alias).ToList();
        return new object[] { extracted, emptyKids, apaiOnly, pass };
    }

    private static object Catalog()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_haven_c_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            SeedCatalog(db);
            BindApe();
            PhpPlanQ1Haven.CategoryForTaxonomy = (_, _, node) => node == 7 ? 11 : 0;
            return new object?[]
            {
                PhpPlanQ1Haven.EpcElectronicaeRootCategoryId(db, "acme_el"),
                PhpPlanQ1Haven.EpcElectronicaeCategoryUrl(db, "acme_el", 7),
                PhpPlanQ1Haven.EpcElectronicaeCategoryUrl(db, "acme_el", 8),
                PhpPlanQ1Haven.EpcElectronicaeCategorySubtreeIds(db, 10),
                PhpPlanQ1Haven.EpcElectronicaeCategorySqlIn(db, 10),
                PhpPlanQ1Haven.EpcElectronicaeCategoryHasProducts(db, 11) ? 1 : 0,
                PhpPlanQ1Haven.EpcElectronicaeCategoryHasProducts(db, 13) ? 1 : 0,
                PhpPlanQ1Haven.EpcElectronicaeCategoryHasProducts(db, 0) ? 1 : 0,
                PhpPlanQ1Haven.EpcElectronicaeCategoryPrefersProducts(db, 11, 1) ? 1 : 0,
                PhpPlanQ1Haven.EpcElectronicaeCategoryPrefersProducts(db, 13, 0) ? 1 : 0,
                PhpPlanQ1Haven.EpcElectronicaeCategoryPrefersProducts(db, 13, 2) ? 1 : 0,
                PhpPlanQ1Haven.EpcElectronicaeAllLinesHref(db, "acme_el")
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object Tiles()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_haven_t_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            SeedCatalog(db);
            Exec(db, "INSERT INTO `shop_products_images` (`product_id`,`file_name`) VALUES (21,'pixel.png')");
            Exec(db, "INSERT INTO `shop_storages_data` (`product_id`,`price`) VALUES (21,1299.00),(21,0)");
            Exec(db, "INSERT INTO `epc_product_discovery_queue` (`site_key`,`status`,`product_id`,`taxonomy_node_id`,`title`,`updated_at`) VALUES ('acme_el','imported',21,7,'Pixel 8',50)");
            BindApe();
            PhpPlanQ1Haven.ResolveStorefrontSiteKey = () => "acme_el";
            PhpPlanQ1Haven.CategoryForTaxonomy = (_, _, node) => node is 7 or 9 ? 11 : 0;
            PhpPlanQ1Haven.TaxDescendantMap = _ => new Dictionary<int, List<int>> { [7] = [7], [9] = [9] };
            PhpPlanQ1Haven.ProductLineRankings = (_, _) =>
            [
                Line(7, "cell-phones-5g", "Phones", 1, 2, "up", 9, ""),
                Line(9, "gaming-laptops", "", 1, 0, "", 1, "https://cdn.test/g.png"),
                Line(8, "cell-phones-cases", "Cases", 2, 9, "", 3, ""),
                Line(12, "audio-kits", "Audio", 1, 1, "", 2, "")
            ];
            var tiles = PhpPlanQ1Haven.EpcElectronicaeProductLineTiles(db, "", 12);
            var nav = PhpPlanQ1Haven.EpcElectronicaeMegaNav(db, "acme_el");
            var hero = PhpPlanQ1Haven.EpcElectronicaeHeroSlides(db, "acme_el");
            var card = PhpPlanQ1Haven.EpcElectronicaeProductCard(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = 21,
                ["alias"] = "pixel-8",
                ["caption"] = "Pixel 8",
                ["category_url"] = "electronics/phones",
                ["file_name"] = "pixel.png",
                ["price"] = "1299.5",
                ["manufacturer"] = ""
            });
            PhpPlanQ1Haven.CatalogueProductPath = (_, _) => "/forced/path";
            var cardForced = PhpPlanQ1Haven.EpcElectronicaeProductCard(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = 21,
                ["caption"] = "Pixel 8",
                ["file_name"] = ""
            });
            PhpPlanQ1Haven.ProductUrlMode = "alias";
            var sections = PhpPlanQ1Haven.EpcElectronicaeHomeProductSections(db, "acme_el", 3, 6);
            return new object?[]
            {
                PhpPlanQ1Haven.LastCacheKey,
                PhpPlanQ1Haven.SyncCount,
                tiles, nav, hero, card, cardForced, sections
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static Dictionary<string, object?> Line(int id, string slug, string name, int level, int imported, string trend, int score, string preview)
        => new(StringComparer.Ordinal)
        {
            ["id"] = id,
            ["slug"] = slug,
            ["name_en"] = name,
            ["level"] = level,
            ["imported_count"] = imported,
            ["trend"] = trend,
            ["score"] = score,
            ["preview_image"] = preview
        };

    private static Dictionary<string, object?> Node(string alias, List<Dictionary<string, object?>>? data = null)
        => new(StringComparer.Ordinal) { ["alias"] = alias, ["data"] = data ?? [] };

    private static string Alias(Dictionary<string, object?> node)
        => Convert.ToString(node.GetValueOrDefault("alias")) ?? "";

    private static void SeedCatalog(MySqlConnection db)
    {
        Exec(db, """
            CREATE TABLE `shop_catalogue_categories` (
                `id` INT NOT NULL PRIMARY KEY,
                `parent` INT NOT NULL DEFAULT 0,
                `alias` VARCHAR(120) NOT NULL DEFAULT '',
                `url` VARCHAR(200) NOT NULL DEFAULT '',
                `published_flag` TINYINT NOT NULL DEFAULT 1
            )
            """);
        Exec(db, """
            CREATE TABLE `shop_catalogue_products` (
                `id` INT NOT NULL PRIMARY KEY,
                `category_id` INT NOT NULL DEFAULT 0,
                `alias` VARCHAR(120) NOT NULL DEFAULT '',
                `caption` VARCHAR(200) NOT NULL DEFAULT '',
                `published_flag` TINYINT NOT NULL DEFAULT 1
            )
            """);
        Exec(db, """
            CREATE TABLE `shop_products_images` (
                `id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                `product_id` INT NOT NULL,
                `file_name` VARCHAR(200) NOT NULL DEFAULT ''
            )
            """);
        Exec(db, """
            CREATE TABLE `shop_storages_data` (
                `id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                `product_id` INT NOT NULL,
                `price` DECIMAL(12,2) NOT NULL DEFAULT 0
            )
            """);
        Exec(db, """
            CREATE TABLE `epc_product_discovery_queue` (
                `id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                `site_key` VARCHAR(64) NOT NULL,
                `status` VARCHAR(24) NOT NULL,
                `product_id` INT NOT NULL DEFAULT 0,
                `taxonomy_node_id` INT NOT NULL DEFAULT 0,
                `title` VARCHAR(200) NOT NULL DEFAULT '',
                `updated_at` INT NOT NULL DEFAULT 0
            )
            """);
        Exec(db, """
            INSERT INTO `shop_catalogue_categories` (`id`,`parent`,`alias`,`url`,`published_flag`) VALUES
            (10,0,'apai-electronics-root','electronics',1),
            (11,10,'apai-phones','electronics/phones',1),
            (12,11,'apai-android','electronics/phones/android',1),
            (13,10,'apai-empty','electronics/empty',1),
            (14,13,'apai-empty-child','electronics/empty/child',0)
            """);
        Exec(db, """
            INSERT INTO `shop_catalogue_products` (`id`,`category_id`,`alias`,`caption`,`published_flag`) VALUES
            (21,11,'pixel-8','Pixel 8',1),
            (22,12,'pixel-8-pro','Pixel 8 Pro',0)
            """);
    }

    private static string Password()
        => Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN") ?? "local-throwaway-pw";

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
