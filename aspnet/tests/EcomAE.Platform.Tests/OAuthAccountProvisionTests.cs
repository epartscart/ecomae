using System.Globalization;
using EcomAE.Platform.Auth;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// PHP <c>epc_auth_find_or_provision_storefront_customer</c> and
/// <c>epc_auth_find_or_provision_cp_user</c>. The database is created for the test and dropped.
/// Does not write <c>docpart</c>.
/// </summary>
public sealed class OAuthAccountProvisionTests
{
    [Theory]
    [InlineData("storefront", "www.ecomae.com", false, true)]
    [InlineData("storefront", "www.electronicae.com", false, true)]
    [InlineData("cp", "www.ecomae.com", false, false)]
    [InlineData("cp", "cp.ecomae.com", true, false)]
    [InlineData("cp", "industries.ecomae.com", true, false)]
    [InlineData("cp", "agriculture.ecomae.com", true, false)]
    [InlineData("cp", "www.electronicae.com", false, false)]
    [InlineData("cp", "www.epartscart.com", false, false)]
    [InlineData("cp", "demo-acme.example", true, true)]
    public void AllowNewAccount_creates_cp_accounts_only_on_demo_sandboxes(string mode, string host, bool demo, bool expected)
    {
        Assert.Equal(expected, OAuthAccountProvision.AllowNewAccount(mode, host, demo));
    }

    [Fact]
    public async Task FindOrProvision_CoversPhpFailureAndCreatePaths()
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
        Assert.DoesNotContain("Database=docpart", connectionString, StringComparison.OrdinalIgnoreCase);
        try
        {
            await ExecuteAsync(connectionString, """
                CREATE TABLE users (
                  user_id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  email VARCHAR(190) NULL,
                  email_confirmed TINYINT NULL,
                  password VARCHAR(255) NULL,
                  unlocked TINYINT NULL,
                  reg_variant INT NULL,
                  time_registered VARCHAR(32) NULL,
                  admin_created TINYINT NULL,
                  ip_address VARCHAR(64) NULL,
                  phone VARCHAR(40) NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE users_profiles (
                  user_id INT NOT NULL,
                  data_key VARCHAR(64) NOT NULL,
                  data_value TEXT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE `groups` (
                  id INT NOT NULL PRIMARY KEY,
                  for_backend TINYINT NOT NULL DEFAULT 0,
                  for_registrated TINYINT NOT NULL DEFAULT 0,
                  for_guests TINYINT NOT NULL DEFAULT 0
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE users_groups_bind (
                  user_id INT NOT NULL,
                  group_id INT NOT NULL,
                  PRIMARY KEY (user_id, group_id)
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO `groups` (id, for_backend, for_registrated, for_guests) VALUES (2, 0, 1, 0), (3, 1, 0, 0)");

            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            await ExecuteAsync(connectionString, "INSERT INTO users (email, email_confirmed, password, unlocked) VALUES ('locked@local.test', 1, 'x', 0)");
            var locked = await OAuthAccountProvision.FindOrProvisionAsync(connection, "locked@local.test", "Locked", storefront: true, allowProvision: true, "secret");
            Assert.Equal(0, locked.UserId);
            Assert.False(locked.Created);
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM users"));

            await ExecuteAsync(connectionString, "UPDATE users SET unlocked = 1, email_confirmed = 0 WHERE email = 'locked@local.test'");
            var confirmed = await OAuthAccountProvision.FindOrProvisionAsync(connection, "Locked@Local.Test", "Locked", storefront: true, allowProvision: true, "secret");
            Assert.Equal(1, confirmed.UserId);
            Assert.False(confirmed.Created);
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT email_confirmed FROM users WHERE user_id = 1"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM users"));

            var noBackend = await OAuthAccountProvision.FindOrProvisionAsync(connection, "locked@local.test", "Locked", storefront: false, allowProvision: true, "secret");
            Assert.Equal(0, noBackend.UserId);
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM users"));

            await ExecuteAsync(connectionString, "INSERT INTO users_groups_bind (user_id, group_id) VALUES (1, 3)");
            var withBackend = await OAuthAccountProvision.FindOrProvisionAsync(connection, "locked@local.test", "Locked", storefront: false, allowProvision: true, "secret");
            Assert.Equal(1, withBackend.UserId);
            Assert.False(withBackend.Created);

            var shop = await OAuthAccountProvision.FindOrProvisionAsync(connection, "newshop@local.test", "Nora", storefront: true, allowProvision: true, "secret");
            Assert.True(shop.UserId > 1);
            Assert.True(shop.Created);
            Assert.Equal("Nora", await ScalarAsync(connectionString, "SELECT data_value FROM users_profiles WHERE user_id = " + shop.UserId.ToString(CultureInfo.InvariantCulture) + " AND data_key = 'name'"));
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT group_id FROM users_groups_bind WHERE user_id = " + shop.UserId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT email_confirmed FROM users WHERE user_id = " + shop.UserId.ToString(CultureInfo.InvariantCulture)));

            var cp = await OAuthAccountProvision.FindOrProvisionAsync(connection, "newcp@local.test", "", storefront: false, allowProvision: true, "secret");
            Assert.True(cp.UserId > shop.UserId);
            Assert.True(cp.Created);
            Assert.Equal("newcp", await ScalarAsync(connectionString, "SELECT data_value FROM users_profiles WHERE user_id = " + cp.UserId.ToString(CultureInfo.InvariantCulture) + " AND data_key = 'name'"));
            Assert.Equal("3", await ScalarAsync(connectionString, "SELECT group_id FROM users_groups_bind WHERE user_id = " + cp.UserId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT admin_created FROM users WHERE user_id = " + cp.UserId.ToString(CultureInfo.InvariantCulture)));
            var hash = await ScalarAsync(connectionString, "SELECT password FROM users WHERE user_id = " + cp.UserId.ToString(CultureInfo.InvariantCulture));
            Assert.Matches("^[0-9a-f]{32}$", hash);

            var beforeDisabled = await ScalarAsync(connectionString, "SELECT COUNT(*) FROM users");
            var disabled = await OAuthAccountProvision.FindOrProvisionAsync(connection, "nosignup@local.test", "No", storefront: false, allowProvision: false, "secret");
            Assert.Equal(0, disabled.UserId);
            Assert.False(disabled.Created);
            Assert.Equal(beforeDisabled, await ScalarAsync(connectionString, "SELECT COUNT(*) FROM users"));

            var missingEmail = await OAuthAccountProvision.FindOrProvisionAsync(connection, "  ", "No", storefront: true, allowProvision: true, "secret");
            Assert.Equal(0, missingEmail.UserId);
            var notAnEmail = await OAuthAccountProvision.FindOrProvisionAsync(connection, "not-an-email", "No", storefront: true, allowProvision: true, "secret");
            Assert.Equal(0, notAnEmail.UserId);
            Assert.Equal(beforeDisabled, await ScalarAsync(connectionString, "SELECT COUNT(*) FROM users"));

            await ExecuteAsync(connectionString, "DROP TABLE users");
            var missingTable = await OAuthAccountProvision.FindOrProvisionAsync(connection, "gone@local.test", "Gone", storefront: true, allowProvision: true, "secret");
            Assert.Equal(0, missingTable.UserId);
            Assert.Equal("Accounts are not in this database.", missingTable.Message);
            Assert.DoesNotContain("doesn't exist", missingTable.Message, StringComparison.OrdinalIgnoreCase);

            await ExecuteAsync(connectionString, """
                CREATE TABLE users (
                  user_id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  email VARCHAR(190) NULL,
                  email_confirmed TINYINT NULL,
                  password VARCHAR(255) NULL,
                  unlocked TINYINT NULL
                )
                """);
            var slim = await OAuthAccountProvision.FindOrProvisionAsync(connection, "slim@local.test", "Slim", storefront: true, allowProvision: true, "secret");
            Assert.True(slim.UserId > 0, slim.Message);
            Assert.True(slim.Created);
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT email_confirmed FROM users WHERE email = 'slim@local.test'"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT unlocked FROM users WHERE email = 'slim@local.test'"));
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
}
