using System.Data.Common;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Super admin login on www.ecomae.com accepts yawer@ecomae.com with the Gmail-operator
/// password while the registry row is still ecomaedxb@gmail.com. Uses a throwaway MariaDB.
/// </summary>
public sealed class SuperOperatorYawerLoginTests
{
    private const string Secret = "test-secret-succession";
    private const string PlainPassword = "same-operator-password";

    [Fact]
    public async Task SuperHost_YawerLogin_UsesGmailRow_ThenRemapsEmail()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_yawer_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        try
        {
            var hash = LegacyPasswordVerifier.Md5Hex(PlainPassword + Secret);
            await ExecuteAsync(cs, """
                CREATE TABLE users (
                  user_id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  email VARCHAR(190) NULL,
                  email_confirmed TINYINT NULL,
                  password VARCHAR(255) NULL,
                  unlocked TINYINT NULL
                )
                """);
            await ExecuteAsync(cs, """
                CREATE TABLE `groups` (
                  id INT NOT NULL PRIMARY KEY,
                  for_backend TINYINT NOT NULL DEFAULT 0
                )
                """);
            await ExecuteAsync(cs, """
                CREATE TABLE users_groups_bind (
                  user_id INT NOT NULL,
                  group_id INT NOT NULL,
                  PRIMARY KEY (user_id, group_id)
                )
                """);
            await ExecuteAsync(cs, """
                CREATE TABLE sessions (
                  session VARCHAR(64) NOT NULL PRIMARY KEY,
                  user_id INT NOT NULL,
                  time BIGINT NULL,
                  data TEXT NULL,
                  type TINYINT NULL,
                  contact_type VARCHAR(16) NULL,
                  csrf_guard_key VARCHAR(64) NULL,
                  last_activiti_time BIGINT NULL
                )
                """);
            await ExecuteAsync(cs, "INSERT INTO `groups` (id, for_backend) VALUES (3, 1)");
            await ExecuteAsync(cs, "INSERT INTO users (email, email_confirmed, password, unlocked) VALUES ('ecomaedxb@gmail.com', 1, '" + hash + "', 1)");
            await ExecuteAsync(cs, "INSERT INTO users_groups_bind (user_id, group_id) SELECT user_id, 3 FROM users WHERE email='ecomaedxb@gmail.com'");

            var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            http.HttpContext.Request.Host = new HostString("www.ecomae.com");
            http.HttpContext.Items[TenantResolutionMiddleware.HttpContextItemKey] = new TenantContext(
                Host: "www.ecomae.com",
                Path: "/erp/login",
                Surface: TenantSurface.Erp,
                Mode: TenantMode.Platform,
                SiteKey: "platform",
                DatabaseName: "should-not-open-shop");

            var service = new DbLegacyAdminLoginService(
                new RegistryOnlyConnections(cs),
                new BackendSessionStore(),
                Options.Create(new EcomAeOptions { SecretSuccession = Secret }),
                http,
                NullLogger<DbLegacyAdminLoginService>.Instance);

            var outcome = await service.LoginAsync(
                new LegacyLoginRequest("yawer@ecomae.com", PlainPassword, "email", false, LegacyLoginSurface.Erp),
                "127.0.0.1",
                "test-agent");

            Assert.True(outcome.Ok, outcome.Failure?.Message);
            Assert.Equal("yawer@ecomae.com", outcome.Success?.Email);

            await using var check = new MySqlConnection(cs);
            await check.OpenAsync();
            await using var cmd = check.CreateCommand();
            cmd.CommandText = "SELECT email, password FROM users WHERE user_id=1";
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("yawer@ecomae.com", Convert.ToString(reader["email"]));
            Assert.False(string.IsNullOrEmpty(Convert.ToString(reader["password"])));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task SuperHost_PrefersAliasRowWhosePasswordMatches()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_yawer_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        try
        {
            var good = LegacyPasswordVerifier.Md5Hex(PlainPassword + Secret);
            var other = LegacyPasswordVerifier.Md5Hex("other-password" + Secret);
            await ExecuteAsync(cs, """
                CREATE TABLE users (
                  user_id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  email VARCHAR(190) NULL,
                  email_confirmed TINYINT NULL,
                  password VARCHAR(255) NULL,
                  unlocked TINYINT NULL
                )
                """);
            await ExecuteAsync(cs, """
                CREATE TABLE `groups` (id INT NOT NULL PRIMARY KEY, for_backend TINYINT NOT NULL DEFAULT 0)
                """);
            await ExecuteAsync(cs, """
                CREATE TABLE users_groups_bind (user_id INT NOT NULL, group_id INT NOT NULL, PRIMARY KEY (user_id, group_id))
                """);
            await ExecuteAsync(cs, """
                CREATE TABLE sessions (
                  session VARCHAR(64) NOT NULL PRIMARY KEY,
                  user_id INT NOT NULL,
                  time BIGINT NULL,
                  data TEXT NULL,
                  type TINYINT NULL,
                  contact_type VARCHAR(16) NULL,
                  csrf_guard_key VARCHAR(64) NULL,
                  last_activiti_time BIGINT NULL
                )
                """);
            await ExecuteAsync(cs, "INSERT INTO `groups` (id, for_backend) VALUES (3, 1)");
            await ExecuteAsync(cs, "INSERT INTO users (email, email_confirmed, password, unlocked) VALUES ('yawer@ecomae.com', 1, '" + other + "', 1)");
            await ExecuteAsync(cs, "INSERT INTO users (email, email_confirmed, password, unlocked) VALUES ('ecomaedxb@gmail.com', 1, '" + good + "', 1)");
            await ExecuteAsync(cs, "INSERT INTO users_groups_bind (user_id, group_id) VALUES (2, 3)");

            var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            http.HttpContext.Request.Host = new HostString("www.ecomae.com");

            var service = new DbLegacyAdminLoginService(
                new RegistryOnlyConnections(cs),
                new BackendSessionStore(),
                Options.Create(new EcomAeOptions { SecretSuccession = Secret }),
                http,
                NullLogger<DbLegacyAdminLoginService>.Instance);

            var outcome = await service.LoginAsync(
                new LegacyLoginRequest("yawer@ecomae.com", PlainPassword, "email", false, LegacyLoginSurface.Erp),
                "127.0.0.1",
                "test-agent");

            Assert.True(outcome.Ok, outcome.Failure?.Message);
            Assert.Equal(2, outcome.Success?.UserId);
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

    private sealed class RegistryOnlyConnections : ITenantDbConnectionFactory
    {
        private readonly string _cs;
        public RegistryOnlyConnections(string cs) => _cs = cs;
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Super login must not OpenAsync");

        public Task<DbConnection> OpenAsync(
            string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Super login must not OpenAsync");

        public Task<DbConnection> OpenForTenantAsync(TenantContext? tenant, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Super login must not open a shop db_name");

        public async Task<DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(_cs);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }

    private sealed class BackendSessionStore : ILegacySessionStore
    {
        public bool IsConfigured => true;

        public Task<bool> AdminSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> CustomerSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<LegacyAdminIdentity?> GetAdminIdentityAsync(int userId, CancellationToken cancellationToken = default)
            => Task.FromResult<LegacyAdminIdentity?>(new LegacyAdminIdentity("yawer@ecomae.com", [3], true));
    }
}
