using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontOrdersBackgroundTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "OrdersBackground");

    [Fact]
    public async Task Loader_MatchesPhp83RuntimeGolden_AndDoesNotWrite()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var spec = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "cases.json"))).RootElement;
        var golden = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
        Assert.Equal("content/shop/order_process/orders_background.php", golden.GetProperty("source").GetString());

        var cases = spec.GetProperty("cases").EnumerateArray().ToList();
        var expected = golden.GetProperty("results").EnumerateArray().ToList();
        Assert.Equal(cases.Count, expected.Count);
        for (var i = 0; i < cases.Count; i++)
        {
            Assert.Equal(cases[i].GetProperty("name").GetString(), expected[i].GetProperty("name").GetString());
            await WithDatabaseAsync(password, spec, cases[i], async connection =>
            {
                var before = await CountsAsync(connection);
                var actual = await StorefrontOrdersBackground.LoadAsync(connection, CancellationToken.None);
                var after = await CountsAsync(connection);

                AssertRows(expected[i].GetProperty("orders_statuses"), actual.OrderStatuses);
                AssertRows(expected[i].GetProperty("orders_items_statuses"), actual.ItemStatuses);
                Assert.Equal(
                    expected[i].GetProperty("orders_items_statuses_not_count").EnumerateArray().Select(x => x.GetInt64()),
                    actual.ItemStatusesNotCount);
                AssertRows(expected[i].GetProperty("offices_list"), actual.Offices);
                AssertCounts(expected[i].GetProperty("counts_before"), before);
                AssertCounts(expected[i].GetProperty("counts_after"), after);
                Assert.Equal(before, after);
            });
        }
    }

    private static void AssertRows(
        JsonElement expected,
        IReadOnlyDictionary<long, StorefrontOrdersBackground.ReferenceRow> actual)
    {
        if (expected.ValueKind == JsonValueKind.Array)
        {
            Assert.Empty(expected.EnumerateArray());
            Assert.Empty(actual);
            return;
        }

        var expectedRows = expected.EnumerateObject().ToList();
        Assert.Equal(expectedRows.Select(x => x.Name), actual.Keys.Select(x => x.ToString(CultureInfo.InvariantCulture)));
        foreach (var expectedRow in expectedRows)
        {
            var row = actual[long.Parse(expectedRow.Name, CultureInfo.InvariantCulture)];
            var fields = expectedRow.Value.EnumerateObject().ToList();
            Assert.Equal(fields.Select(x => x.Name), row.Fields.Keys);
            foreach (var field in fields)
            {
                Assert.Equal(JsonString(field.Value), row.Fields[field.Name]);
            }
        }
    }

    private static string? JsonString(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            _ => value.GetRawText()
        };

    private static void AssertCounts(JsonElement expected, IReadOnlyDictionary<string, long> actual)
    {
        foreach (var row in expected.EnumerateObject())
        {
            Assert.Equal(row.Value.GetInt64(), actual[row.Name]);
        }
    }

    private static async Task<Dictionary<string, long>> CountsAsync(MySqlConnection connection)
    {
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var table in new[] { "shop_orders_statuses_ref", "shop_orders_items_statuses_ref", "shop_offices" })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM `" + table + "`";
            counts[table] = Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        }

        return counts;
    }

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
            await using var connection = new MySqlConnection(
                "Server=127.0.0.1;Port=3306;Database=" + name + ";User ID=ecomae;Password=" + password + ";");
            await connection.OpenAsync();
            foreach (var sql in spec.GetProperty("schema").EnumerateArray().Concat(testCase.GetProperty("setup").EnumerateArray()))
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
}
