using System.Text.Json;
using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <c>Fixtures/ProductOffers/harness.php</c> ran the offers block of PHP <c>product_page_for_customer.php</c> (real
/// <c>get_customer_offices.php</c> and <c>epc_pricing.php</c>) per case on throwaway MariaDB databases, and echoed
/// <c>common_add_to_basket.php</c>; ASP.NET must render the same bytes.
/// </summary>
public sealed class StorefrontProductOffersTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ProductOffers");

    private static JsonElement Golden() => JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;

    [Fact]
    public void Script_MatchesPhpGolden()
    {
        var golden = Golden();
        var actual = StorefrontCommonAddToBasket.Script(golden.GetProperty("csrf").GetString()!, id => "{" + id + "}");
        Assert.Equal(golden.GetProperty("script").GetString(), actual);
    }

    [Fact]
    public async Task Offers_MatchPhpGolden_OnThrowawayDatabases_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var spec = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "cases.json"))).RootElement;
        var golden = Golden();
        var results = golden.GetProperty("results").EnumerateArray().ToList();
        var now = golden.GetProperty("now").GetInt64();
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        var cases = spec.GetProperty("cases").EnumerateArray().ToList();
        Assert.Equal(cases.Count, results.Count);
        for (var i = 0; i < cases.Count; i++)
        {
            var c = cases[i];
            Assert.Equal(c.GetProperty("name").GetString(), results[i].GetProperty("name").GetString());
            var name = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
            await ExecAsync(admin, "CREATE DATABASE `" + name + "`");
            try
            {
                await using var connection = new MySqlConnection("Server=127.0.0.1;Port=3306;Database=" + name + ";User ID=ecomae;Password=" + password + ";");
                await connection.OpenAsync();
                foreach (var sql in spec.GetProperty("schema").EnumerateArray()
                             .Concat(spec.GetProperty("base").EnumerateArray())
                             .Concat(c.GetProperty("setup").EnumerateArray()))
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = sql.GetString();
                    await command.ExecuteNonQueryAsync();
                }

                var config = c.GetProperty("config");
                var request = new StorefrontProductOffers.Request(
                    c.TryGetProperty("product_id", out var pid) ? pid.GetInt64() : 7,
                    c.GetProperty("user_id").GetInt64(),
                    c.GetProperty("cookie").ValueKind == JsonValueKind.Null ? null : c.GetProperty("cookie").GetString(),
                    c.TryGetProperty("min_order", out var mo) ? mo.GetString()! : "1",
                    c.TryGetProperty("lang_href", out var lh) ? lh.GetString()! : "/en",
                    now,
                    config.GetProperty("price_rounding").GetString()!,
                    config.GetProperty("tech_key").GetString()!);
                var html = await StorefrontProductOffers.RenderAsync(connection, request, (key, _) => Task.FromResult("{" + key + "}"), CancellationToken.None);
                Assert.True(
                    results[i].GetProperty("html").GetString() == html,
                    c.GetProperty("name").GetString() + ":\n--- PHP ---\n" + results[i].GetProperty("html").GetString() + "\n--- ASP.NET ---\n" + html);
            }
            finally
            {
                await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + name + "`");
            }
        }
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
