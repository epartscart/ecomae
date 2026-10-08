using System.Text.Json;
using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <c>Fixtures/CustomerOffices/harness.php</c> ran PHP <c>get_customer_offices.php</c> for every scenario and
/// <c>my_city</c> cookie on throwaway MariaDB databases; ASP.NET must pick the same offices in the same order.
/// </summary>
public sealed class StorefrontCustomerOfficesTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CustomerOffices");

    [Fact]
    public async Task Offices_MatchPhpGolden_OnThrowawayDatabases_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var spec = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "scenarios.json"))).RootElement;
        var golden = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "golden.json"))).RootElement
            .GetProperty("results").EnumerateArray().ToList();
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        var checkedCount = 0;
        foreach (var scenario in spec.GetProperty("scenarios").EnumerateArray())
        {
            var name = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
            await ExecAsync(admin, "CREATE DATABASE `" + name + "`");
            try
            {
                await using var connection = new MySqlConnection("Server=127.0.0.1;Port=3306;Database=" + name + ";User ID=ecomae;Password=" + password + ";");
                await connection.OpenAsync();
                foreach (var sql in spec.GetProperty("schema").EnumerateArray().Concat(scenario.GetProperty("setup").EnumerateArray()))
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = sql.GetString();
                    await command.ExecuteNonQueryAsync();
                }

                foreach (var cookie in scenario.GetProperty("cookies").EnumerateArray())
                {
                    var want = golden[checkedCount++];
                    Assert.Equal(scenario.GetProperty("name").GetString(), want.GetProperty("scenario").GetString());
                    var value = cookie.ValueKind == JsonValueKind.Null ? null : cookie.GetString();
                    var expected = want.GetProperty("offices").EnumerateArray().Select(e => e.GetInt32()).ToList();
                    var actual = await StorefrontCustomerOffices.LoadAsync(connection, value, CancellationToken.None);
                    Assert.True(expected.SequenceEqual(actual), $"{want.GetProperty("scenario")} cookie={value ?? "(unset)"}: PHP [{string.Join(",", expected)}] ASP.NET [{string.Join(",", actual)}]");
                }
            }
            finally
            {
                await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + name + "`");
            }
        }

        Assert.Equal(golden.Count, checkedCount);
    }

    [Fact]
    public async Task MissingGeoTables_FallBackToFirstOffice_OnlyWhenTolerated()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        var name = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        await ExecAsync(admin, "CREATE DATABASE `" + name + "`");
        try
        {
            var cs = "Server=127.0.0.1;Port=3306;Database=" + name + ";User ID=ecomae;Password=" + password + ";";
            await ExecAsync(cs, "CREATE TABLE `shop_offices` (`id` int(11) NOT NULL, PRIMARY KEY (`id`))");
            await ExecAsync(cs, "INSERT INTO `shop_offices` VALUES (12), (3)");
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            Assert.Equal([3], await StorefrontCustomerOffices.LoadAsync(connection, "5", CancellationToken.None, missingGeoTablesAsEmpty: true));
            await Assert.ThrowsAsync<MySqlException>(() => StorefrontCustomerOffices.LoadAsync(connection, null, CancellationToken.None));
        }
        finally
        {
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + name + "`");
        }
    }

    [Fact]
    public void CallSites_UseTheSharedPort()
    {
        var root = FindRepoDir();
        foreach (var file in new[] { "StorefrontPhpCart.cs", "StorefrontPhpShop.cs", "StorefrontPhpLocalBatch.cs", "StorefrontPhpCatalogueDemand.cs" })
        {
            var text = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Storefront", file));
            Assert.Contains("StorefrontCustomerOffices.LoadAsync(", text, StringComparison.Ordinal);
            Assert.DoesNotContain("FROM `shop_offices_geo_map`", text, StringComparison.Ordinal);
        }

        var endpoints = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Storefront/StorefrontPhpAjaxEndpoints.cs"));
        Assert.True(endpoints.Split("context.Request.Cookies[StorefrontCustomerOffices.CityCookie]").Length - 1 >= 4);
    }

    private static async Task ExecAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static string FindRepoDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "content/shop/order_process/get_customer_offices.php")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root");
    }
}
