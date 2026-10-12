using EcomAE.Platform.Cp;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpFulfillmentSchemaEnsureTests
{
    [Fact]
    public async Task EnsureSchemaCreatesQueueTablesOnAnEmptyThrowawayDatabase()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await ExecAsync(admin, "CREATE DATABASE `" + database + "` DEFAULT CHARACTER SET utf8mb4");
        try
        {
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            await CpFulfillmentQueueWriteService.EnsureSchemaAsync(connection, CancellationToken.None);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM `epc_fulfillment_orders`";
            Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
            command.CommandText = "SELECT COUNT(*) FROM `epc_fulfillment_items`";
            Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
        }
        finally
        {
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + database + "`");
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
