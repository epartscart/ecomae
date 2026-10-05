using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Proves a Control Panel save against a database created for the test and dropped afterward.
/// Does not write docpart or ecomae shop rows.
/// </summary>
public sealed class CpWriteThrowawayDbTests
{
    [Fact]
    public async Task PresentTablesSaveAndMissingTablesReturnAClearMessage()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        try
        {
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_docpart_search_tabs (
                  id INT NOT NULL PRIMARY KEY,
                  enabled INT NOT NULL DEFAULT 1
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO shop_docpart_search_tabs (id, enabled) VALUES (7, 1)");
            var tabs = new CpSearchTabWriteService(new FixedConnections(connectionString));
            var deactivated = await tabs.SetEnabledAsync(7, 0);
            Assert.True(deactivated.Succeeded, deactivated.Message);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT enabled FROM shop_docpart_search_tabs WHERE id = 7"));
            await ExecuteAsync(connectionString, "DROP TABLE shop_docpart_search_tabs");
            var missingTab = await tabs.SetEnabledAsync(7, 1);
            Assert.False(missingTab.Succeeded);
            Assert.Equal("Search tabs are not in this database.", missingTab.Message);
            Assert.DoesNotContain("doesn't exist", missingTab.Message, StringComparison.OrdinalIgnoreCase);

            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_offices (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  caption VARCHAR(255) NOT NULL,
                  country VARCHAR(255) NOT NULL,
                  city VARCHAR(255) NOT NULL,
                  address VARCHAR(255) NOT NULL,
                  phone VARCHAR(64) NOT NULL,
                  email VARCHAR(255) NOT NULL,
                  users TEXT NULL
                )
                """);
            var offices = new CpOfficeWriteService(new FixedConnections(connectionString));
            var created = await offices.CreateAsync(new CpOfficeSaveRequest(
                Caption: "Probe HQ",
                Country: "UAE",
                Region: "skipped",
                City: "Dubai",
                Address: "Al Quoz"));
            Assert.True(created.Succeeded, created.Message);
            Assert.Equal("Probe HQ", await ScalarAsync(connectionString, "SELECT caption FROM shop_offices WHERE id = " + created.Id.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("Dubai", await ScalarAsync(connectionString, "SELECT city FROM shop_offices WHERE id = " + created.Id.ToString(CultureInfo.InvariantCulture)));

            await ExecuteAsync(connectionString, """
                CREATE TABLE users (
                  user_id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  email VARCHAR(190) NULL,
                  phone VARCHAR(40) NULL,
                  password VARCHAR(255) NULL,
                  email_confirmed TINYINT NULL,
                  phone_confirmed TINYINT NULL,
                  unlocked TINYINT NULL,
                  name VARCHAR(120) NULL
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO users (email, phone, email_confirmed, phone_confirmed, unlocked) VALUES ('a@b.c', '', 1, 0, 1)");
            var users = new CpUserWriteService(new FixedConnections(connectionString));
            var saved = await users.UpdateAsync(1, "saved@local.test", 1, "050", 0, "", 1, 3, "[{\"name\":\"surname\",\"value\":\"Ali\"}]", "[3]", 0, null);
            Assert.True(saved.Succeeded, saved.Message);
            Assert.Contains("Profile fields are not in this database.", saved.Message, StringComparison.Ordinal);
            Assert.Contains("Group bindings are not in this database.", saved.Message, StringComparison.Ordinal);
            Assert.Equal("saved@local.test", await ScalarAsync(connectionString, "SELECT email FROM users WHERE user_id = 1"));
            Assert.Equal("050", await ScalarAsync(connectionString, "SELECT phone FROM users WHERE user_id = 1"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private sealed class FixedConnections : IErpWriteConnectionFactory
    {
        private readonly string _connectionString;

        public FixedConnections(string connectionString) => _connectionString = connectionString;

        public bool IsConfigured => true;

        public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
    }
}
