using System.Text.Json;
using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <c>Fixtures/CatalogueCount/harness.php</c> ran the real PHP 8.3 <c>content/shop/catalogue/ajax_get_products_count.php</c>
/// (with the real <c>query_products_all.php</c>, <c>get_customer_offices.php</c> and <c>text_search_algorithm.php</c>) once per case
/// on a throwaway MariaDB database; <see cref="StorefrontPhpAjax.CatalogueCountAsync"/> must echo the same integer for the same
/// request JSON, <c>my_city</c> cookie, user and data. Cases where PHP itself throws are recorded as <c>php_error</c> in the
/// golden and pinned here as documented ASP.NET deviations.
/// </summary>
public sealed class StorefrontCatalogueCountParityTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CatalogueCount");

    /// <summary>
    /// PHP aborts with an uncaught exception for these requests (500, no count). ASP.NET answers gracefully instead:
    /// the expected body is pinned so the deviation is intentional and visible.
    /// </summary>
    private static readonly Dictionary<string, string> PhpErrorDeviations = new(StringComparer.Ordinal)
    {
        // query_products_all.php builds an empty UNION when the shop has no office at all and runs `FROM ()`.
        ["offices_none_at_all_php_pdo_error"] = "5",
        // Two checked options with no previous list_type joiner produce "(a   b)" in PHP; ASP.NET ignores that property filter.
        ["list_unknown_list_type_two_options_php_pdo_error"] = "5",
        // count(null) TypeError in PHP; ASP.NET ignores the property filter.
        ["list_missing_list_options_php_type_error"] = "5",
        // Only one-letter tokens make text_search_algorithm.php emit "WHERE ()"; ASP.NET searches without a token filter
        // (every translated product, all categories) and still applies the published-only block type.
        ["search_only_short_tokens_php_pdo_error"] = "8",
        ["search_cyrillic_single_char_token_php_pdo_error"] = "8",
        // category_id is interpolated raw by PHP ("abc" is an unknown column); ASP.NET rejects non-numeric category ids.
        ["category_non_numeric_string_php_pdo_error"] = "Product request is empty.",
        // count("abc") TypeError in PHP; ASP.NET rejects a properties_list that is not a JSON array.
        ["properties_list_string_php_type_error"] = "Product request is empty."
    };

    private static JsonElement Golden() => JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;

    [Fact]
    public void Golden_IsPhp83RuntimeOutputOfTheCountScript()
    {
        var golden = Golden();
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
        Assert.Equal("content/shop/catalogue/ajax_get_products_count.php", golden.GetProperty("source").GetString());
        var spec = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement;
        var cases = spec.GetProperty("cases").EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToList();
        var results = golden.GetProperty("results").EnumerateArray().ToList();
        Assert.Equal(cases, results.Select(r => r.GetProperty("name").GetString()).ToList());
        Assert.Equal(cases.Count, cases.Distinct(StringComparer.Ordinal).Count());
        foreach (var result in results)
        {
            if (result.TryGetProperty("php_error", out _))
            {
                Assert.Contains(result.GetProperty("name").GetString()!, PhpErrorDeviations.Keys);
            }
            else
            {
                Assert.Matches("^[0-9]+$", result.GetProperty("output").GetString()!);
            }
        }

        Assert.Equal(
            PhpErrorDeviations.Keys.OrderBy(k => k, StringComparer.Ordinal),
            results.Where(r => r.TryGetProperty("php_error", out _)).Select(r => r.GetProperty("name").GetString()!).OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public async Task CatalogueCount_MatchesPhpGolden_OnThrowawayDatabases_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var spec = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "cases.json"))).RootElement;
        var results = Golden().GetProperty("results").EnumerateArray().ToList();
        var cases = spec.GetProperty("cases").EnumerateArray().ToList();
        Assert.Equal(cases.Count, results.Count);
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        var failures = new List<string>();
        for (var i = 0; i < cases.Count; i++)
        {
            var c = cases[i];
            var name = c.GetProperty("name").GetString()!;
            Assert.Equal(name, results[i].GetProperty("name").GetString());
            var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
            await ExecAsync(admin, "CREATE DATABASE `" + database + "`");
            try
            {
                await using var connection = new MySqlConnection("Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";Pooling=false;");
                await connection.OpenAsync();
                foreach (var sql in spec.GetProperty("schema").EnumerateArray()
                             .Concat(spec.GetProperty("base").EnumerateArray())
                             .Concat(c.GetProperty("setup").EnumerateArray()))
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = sql.GetString();
                    await command.ExecuteNonQueryAsync();
                }

                var requestJson = c.TryGetProperty("request_raw", out var raw) ? raw.GetString() : c.GetProperty("request").GetRawText();
                var cookie = c.GetProperty("cookie").ValueKind == JsonValueKind.Null ? null : c.GetProperty("cookie").GetString();
                var lang = c.TryGetProperty("lang", out var langElement) ? langElement.GetString() : "en";
                var body = await CountAsync(connection, requestJson, cookie, c.GetProperty("user_id").GetInt64(), lang);
                var expected = results[i].TryGetProperty("php_error", out _)
                    ? PhpErrorDeviations[name]
                    : results[i].GetProperty("output").GetString();
                if (!string.Equals(expected, body, StringComparison.Ordinal))
                {
                    failures.Add(name + ": PHP=" + expected + " ASP.NET=" + body);
                }
            }
            finally
            {
                await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + database + "`");
            }
        }

        Assert.True(failures.Count == 0, "Count differs from the PHP golden for " + failures.Count + " of " + cases.Count + " cases:\n" + string.Join("\n", failures));
    }

    /// <summary>
    /// Intentional ASP.NET hardening on top of PHP parity: PHP interpolates category_id and products_ids_str raw into SQL.
    /// ASP.NET must keep them parameterized or integer-only, so injection strings never widen the result.
    /// </summary>
    [Fact]
    public async Task CatalogueCount_RawSqlInterpolationOfPhp_IsHardened_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var spec = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "cases.json"))).RootElement;
        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        await ExecAsync(admin, "CREATE DATABASE `" + database + "`");
        try
        {
            await using var connection = new MySqlConnection("Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";Pooling=false;");
            await connection.OpenAsync();
            foreach (var sql in spec.GetProperty("schema").EnumerateArray().Concat(spec.GetProperty("base").EnumerateArray()))
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql.GetString();
                await command.ExecuteNonQueryAsync();
            }

            Assert.Equal("5", await CountAsync(connection, """{"category_id":10,"product_block_type":1}""", "1"));
            Assert.Equal("Product request is empty.", await CountAsync(connection, """{"category_id":"10 OR 1=1","product_block_type":1}""", "1"));
            Assert.Equal("Product request is empty.", await CountAsync(connection, """{"category_id":"10) OR (1=1","product_block_type":1}""", "1"));
            Assert.Equal("Product request is empty.", await CountAsync(connection, """{"category_id":[10],"product_block_type":1}""", "1"));

            Assert.Equal("0", await CountAsync(connection, """{"category_id":0,"product_block_type":3,"products_ids_str":"1) OR (1=1"}""", "1"));
            Assert.Equal("0", await CountAsync(connection, """{"category_id":0,"product_block_type":3,"products_ids_str":"1; DROP TABLE shop_catalogue_products"}""", "1"));
            Assert.Equal("0", await CountAsync(connection, """{"category_id":0,"product_block_type":3,"products_ids_str":"1,2,"}""", "1"));
            Assert.Equal("0", await CountAsync(connection, """{"category_id":0,"product_block_type":3,"products_ids_str":[1,2]}""", "1"));
            Assert.Equal("2", await CountAsync(connection, """{"category_id":0,"product_block_type":3,"products_ids_str":"1,2"}""", "1"));

            Assert.Equal("0", await CountAsync(connection, """{"category_id":0,"product_block_type":1,"search_string":"brake' OR '1'='1 pad"}""", "1"));
            Assert.Equal("0", await CountAsync(connection, """{"category_id":0,"product_block_type":1,"search_string":"zz') OR 1=1 -- "}""", "1"));
            await using var check = connection.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM `shop_catalogue_products`";
            Assert.Equal(10L, Convert.ToInt64(await check.ExecuteScalarAsync()));
        }
        finally
        {
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + database + "`");
        }
    }

    private static async Task<string> CountAsync(MySqlConnection connection, string? requestJson, string? cityCookie, long userId = 0, string? lang = "en")
    {
        var payload = await StorefrontPhpAjax.CatalogueCountAsync(connection, requestJson, CancellationToken.None, cityCookie, userId, lang);
        var http = Assert.IsType<StorefrontPhpAjax.RawHttp>(payload);
        return http.Body;
    }

    private static async Task ExecAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
