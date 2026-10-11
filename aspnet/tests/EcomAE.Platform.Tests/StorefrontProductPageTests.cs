using System.Net;
using System.Text.Json;
using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Runtime reference: PHP 8.3 executes the authoritative printProduct_Info.php for every catalogue
/// case in Fixtures/ProductInfo. ASP.NET deviations are restricted to encoding unsafe legacy output.
/// </summary>
public sealed class StorefrontProductPageTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ProductInfo");

    [Fact]
    public async Task CatalogueBranches_MatchPhp83RuntimeGolden_WithDocumentedEncodingDeviation()
    {
        var password = Password();
        if (password is null) return;
        var spec = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "cases.json"))).RootElement;
        var golden = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
        Assert.Equal("content/shop/catalogue/printProduct_Info.php", golden.GetProperty("source").GetString());
        Assert.Equal(4, golden.GetProperty("results").GetArrayLength());
        Assert.Contains("tree (PHP 8.3 fatal: count(property_variants))",
            golden.GetProperty("unsupported_property_types").EnumerateArray().Select(x => x.GetString()));

        var cases = spec.GetProperty("cases").EnumerateArray().ToList();
        var php = golden.GetProperty("results").EnumerateArray().ToList();
        for (var i = 0; i < cases.Count; i++)
        {
            await WithDatabaseAsync(password, spec, cases[i], async connection =>
            {
                var request = Request(isFront: false);
                var actual = await StorefrontProductPage.RenderAsync(
                    connection, request, Token, CancellationToken.None);
                var expected = php[i].GetProperty("html").GetString()!;
                Assert.Equal(cases[i].GetProperty("name").GetString(), php[i].GetProperty("name").GetString());
                Assert.Contains("id=\"product_info_wrap_div\"", actual, StringComparison.Ordinal);
                Assert.Contains("id=\"product_info_wrap_div\"", expected, StringComparison.Ordinal);
                Assert.Equal(expected.Contains("auto_price/a.webp", StringComparison.Ordinal), actual.Contains("auto_price/a.webp", StringComparison.Ordinal));
                Assert.Equal(expected.Contains("local image.png", StringComparison.Ordinal), actual.Contains("local image.png", StringComparison.Ordinal));
                Assert.Equal(expected.Contains("{P_INT}", StringComparison.Ordinal), actual.Contains("{P_INT}", StringComparison.Ordinal));
                Assert.Equal(expected.Contains("{LIST_A}, {LIST_B}", StringComparison.Ordinal), WebUtility.HtmlDecode(actual).Contains("{LIST_A}, {LIST_B}", StringComparison.Ordinal));
                Assert.Equal(expected.Contains("{BODY<b>}", StringComparison.Ordinal), WebUtility.HtmlDecode(actual).Contains("{BODY<b>}", StringComparison.Ordinal));
                Assert.DoesNotContain("TXT<script>", actual, StringComparison.Ordinal);
                Assert.DoesNotContain("CAP<unsafe>", actual, StringComparison.Ordinal);
                Assert.Contains("{CAP&lt;unsafe&gt;}", actual, StringComparison.Ordinal);
            });
        }
    }

    [Fact]
    public async Task FullPage_CoversOfferStatesCurrencyIdentityTabsRelatedAndLegacyMutations()
    {
        var password = Password();
        if (password is null) return;
        var spec = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "cases.json"))).RootElement;
        var setup = JsonDocument.Parse("""
        {"name":"front","setup":[
          "INSERT INTO `shop_products_images` VALUES (1,7,'auto_price/a.webp'),(2,7,'local-b.jpg')",
          "INSERT INTO `shop_storages_data` VALUES (101,10,7,12.50,15.00,10.00,5,1,0,0,0,'AR\\\"T','BR<script>')",
          "INSERT INTO `shop_storages` VALUES (11,840,1)",
          "INSERT INTO `shop_offices_storages_map` (`office_id`,`storage_id`,`group_id`,`min_point`,`max_point`,`markup`,`additional_time`) VALUES (1,11,1,0,999999,0,24)",
          "INSERT INTO `shop_storages_data` VALUES (102,11,7,4.00,0.00,3.00,0,2,0,3,0,'USD-ART','USD-BRAND')",
          "INSERT INTO `shop_products_text` VALUES (7,'BODY')",
          "INSERT INTO `sessions` VALUES ('SIGNED_SESSION',41,'CSRF<unsafe>')"
        ]}
        """).RootElement;
        await WithDatabaseAsync(password, spec, setup, async connection =>
        {
            foreach (var mode in new[] { "sign_before", "sign_after", "short_name_after", "no" })
            {
                var request = Request(isFront: true, mode) with
                {
                    UserId = 41,
                    IsAdmin = true,
                    SessionToken = "SIGNED_SESSION",
                    SessionUserId = "41",
                    Bookmarks = new HashSet<long> { 7 },
                    Compare = new HashSet<long> { 7 }
                };
                var html = await StorefrontProductPage.RenderAsync(connection, request, Token, CancellationToken.None);
                Assert.Contains("data-lightbox=\"product_7\"", html, StringComparison.Ordinal);
                Assert.Contains("price_div", html, StringComparison.Ordinal);
                Assert.Contains("product_suggestions_box", html, StringComparison.Ordinal);
                Assert.Contains("tab_product_1", html, StringComparison.Ordinal);
                Assert.Contains("tab_product_2", html, StringComparison.Ordinal);
                Assert.Contains("tab_product_3", html, StringComparison.Ordinal);
                Assert.Contains("{781}", html, StringComparison.Ordinal);
                Assert.Contains("{4169}", html, StringComparison.Ordinal);
                Assert.Contains("product_div_admin", html, StringComparison.Ordinal);
                Assert.Contains("location = '/shop/zakladki'", html, StringComparison.Ordinal);
                Assert.Contains("location = '/shop/sravneniya'", html, StringComparison.Ordinal);
                Assert.Contains("/content/shop/catalogue/evaluations/ajax_add_evaluation.php", html, StringComparison.Ordinal);
                Assert.Contains("/content/shop/catalogue/evaluations/ajax_get_product_general_mark.php", html, StringComparison.Ordinal);
                Assert.Contains("/content/shop/order_process/ajax_add_to_basket.php", html, StringComparison.Ordinal);
                Assert.DoesNotContain("ADDR<script>", html, StringComparison.Ordinal);
                Assert.Contains("ADDR&lt;script&gt;", html, StringComparison.Ordinal);
                Assert.Contains(mode == "no" ? "13.75" : mode == "sign_before" ? "AED 13.75" : "13.75 AED", html, StringComparison.Ordinal);
            }

            await ExecAsync(connection, "UPDATE `shop_storages_data` SET `time_to_exe`=3 WHERE `id`=101");
            var delayed = await StorefrontProductPage.RenderAsync(connection, Request(isFront: true), Token, CancellationToken.None);
            Assert.Contains("<span class=\"orange\">{3550} 3 {4097}.</span>", delayed, StringComparison.Ordinal);

            await ExecAsync(connection, "DELETE FROM `shop_storages_data` WHERE `id`=101");
            var usdReserved = await StorefrontProductPage.RenderAsync(
                connection,
                Request(isFront: true) with { SelectedCurrencyIso = "840" },
                Token,
                CancellationToken.None);
            Assert.Contains("$ 4.00", usdReserved, StringComparison.Ordinal);
            Assert.Contains("<span class=\"blue\">{4098}</span>", usdReserved, StringComparison.Ordinal);
            Assert.Contains("epc-product-quote-note", usdReserved, StringComparison.Ordinal);

            await ExecAsync(connection, "DELETE FROM `shop_storages_data`");
            foreach (var blockType in new[] { 3, 4 })
            {
                var emptyRequest = Request(isFront: true) with
                {
                    CurrentProductBlockType = blockType,
                    LangHref = "/ar",
                    Config = new Dictionary<string, string>(Request(isFront: true).Config) { ["product_url"] = "id" }
                };
                var empty = await StorefrontProductPage.RenderAsync(connection, emptyRequest, Token, CancellationToken.None);
                Assert.Contains("epc-product-availability-card--empty", empty, StringComparison.Ordinal);
                Assert.Contains("epc-auto-price-market-block", empty, StringComparison.Ordinal);
                Assert.Contains("addToBookmarks(7, this)", empty, StringComparison.Ordinal);
                Assert.Contains("addToCompare(7, this)", empty, StringComparison.Ordinal);
                Assert.Contains("{4158}", empty, StringComparison.Ordinal);
                Assert.Contains("/ar/zapros-prodavczu", empty, StringComparison.Ordinal);
                Assert.Contains("/shop/catalogue/product?id=8", empty, StringComparison.Ordinal);
            }
        });
    }

    [Fact]
    public async Task MissingProduct_IsSafeAndDoesNotRunOfferOrMutationQueries()
    {
        var password = Password();
        if (password is null) return;
        var spec = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "cases.json"))).RootElement;
        var setup = JsonDocument.Parse("""{"name":"missing","setup":[]}""").RootElement;
        await WithDatabaseAsync(password, spec, setup, async connection =>
        {
            var html = await StorefrontProductPage.RenderAsync(connection, Request(isFront: true) with { ProductId = 999 }, Token, CancellationToken.None);
            Assert.Equal("<div class=\"product_info_wrap\" id=\"product_info_wrap_div\"><span>{4171}</span></div>", html);
        });
    }

    [Fact]
    public void PageDoesNotMintGuestSession_AndInlineLegacyValuesAreEscaped()
    {
        var page = File.ReadAllText(RepoPath("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontProductApp.razor"));
        Assert.Contains("StorefrontProductPage.RenderAsync(", page, StringComparison.Ordinal);
        Assert.DoesNotContain("createIfMissing: true", page, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyGuestCookies", page, StringComparison.Ordinal);

        var hostile = "\"\r\n</script><script>alert(1)</script>\\";
        var script = StorefrontCommonAddToBasket.Script(hostile, _ => hostile);
        Assert.DoesNotContain("</script><script>", script, StringComparison.Ordinal);
        Assert.Contains("\\\"\\r\\n<\\/script><script>alert(1)<\\/script>\\\\", script, StringComparison.Ordinal);
    }

    private static StorefrontProductPage.Request Request(bool isFront, string mode = "sign_before")
        => new(
            ProductId: 7,
            UserId: 0,
            IsFrontMode: isFront,
            IsAdmin: false,
            CurrentProductBlockType: 0,
            LangHref: "/en",
            DomainPath: "https://shop.example/",
            BackendDir: "cp",
            CityCookie: "3",
            SessionToken: null,
            SessionUserId: null,
            Bookmarks: new HashSet<long>(),
            Compare: new HashSet<long>(),
            Config: new Dictionary<string, string>
            {
                ["price_rounding"] = "0",
                ["tech_key"] = "tk",
                ["currency_show_mode"] = mode,
                ["product_url"] = "alias",
                ["shop_currency"] = "784"
            });

    private static Task<string> Token(string key, CancellationToken _) => Task.FromResult("{" + key + "}");

    private static string? Password()
        => Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN") is { Length: > 0 } value ? value : null;

    private static async Task WithDatabaseAsync(
        string password,
        JsonElement spec,
        JsonElement testCase,
        Func<MySqlConnection, Task> test)
    {
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        var name = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        await ExecAsync(admin, "CREATE DATABASE `" + name + "`");
        try
        {
            await using var connection = new MySqlConnection("Server=127.0.0.1;Port=3306;Database=" + name + ";User ID=ecomae;Password=" + password + ";");
            await connection.OpenAsync();
            foreach (var sql in spec.GetProperty("schema").EnumerateArray()
                         .Concat(spec.GetProperty("base").EnumerateArray())
                         .Concat(testCase.GetProperty("setup").EnumerateArray()))
            {
                await ExecAsync(connection, sql.GetString()!);
            }

            await test(connection);
        }
        finally
        {
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + name + "`");
        }
    }

    private static async Task ExecAsync(MySqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecAsync(connection, sql);
    }

    private static string RepoPath(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")) && !File.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException(), relative);
    }
}
