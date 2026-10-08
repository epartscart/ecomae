using System.Globalization;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Data;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>The admin login rate limit and the md5-to-bcrypt upgrade, on a throwaway schema.</summary>
public sealed class LegacyLoginSecurityTests
{
    private const string Secret = "succession";

    [Fact]
    public void ClientIp_PrefersCloudflareThenFirstForwardedHop()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.9");
        Assert.Equal("10.0.0.9", LegacyLoginSecurity.ClientIp(context));
        context.Request.Headers["X-Forwarded-For"] = " 203.0.113.4 , 10.0.0.1";
        Assert.Equal("203.0.113.4", LegacyLoginSecurity.ClientIp(context));
        context.Request.Headers["CF-Connecting-IP"] = "198.51.100.7";
        Assert.Equal("198.51.100.7", LegacyLoginSecurity.ClientIp(context));
        Assert.Equal("0.0.0.0", LegacyLoginSecurity.ClientIp(null));
    }

    [Fact]
    public void BlockedMessage_RoundsUpToWholeMinutes_AndTheLoginPageShowsIt()
    {
        Assert.Equal("Too many failed attempts. Please wait 1 minutes before trying again.", new LegacyLoginSecurity.RateLimitResult(true, 0, 0).Message);
        Assert.Equal("Too many failed attempts. Please wait 15 minutes before trying again.", new LegacyLoginSecurity.RateLimitResult(true, 0, 841).Message);
        var failure = new LegacyLoginFailure("x", "rate_limited", 15);
        Assert.Equal("error=rate_limited&wait=15", failure.Query);
        Assert.Equal("error=invalid_credentials", new LegacyLoginFailure("x", "invalid_credentials").Query);
        Assert.Equal("Too many failed attempts. Please wait 15 minutes before trying again.", LoginErrorHelper.FromUri("https://shop.test/cp/login?" + failure.Query));
    }

    [Fact]
    public void HashPassword_IsPhpBcryptCost12()
    {
        var hash = LegacyLoginSecurity.HashPassword("pw");
        Assert.StartsWith("$2y$12$", hash, StringComparison.Ordinal);
        Assert.True(LegacyPasswordVerifier.Verify("pw", hash, Secret));
    }

    [Fact]
    public async Task RateLimit_AndPasswordUpgrade_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        await ExecAsync(admin, "CREATE DATABASE `" + database + "`");
        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";";
        Assert.DoesNotContain("Database=docpart", cs, StringComparison.OrdinalIgnoreCase);
        try
        {
            var md5 = LegacyPasswordVerifier.Md5Hex("right" + Secret);
            await ExecAsync(cs, $"""
                CREATE TABLE users (
                  user_id INT NOT NULL PRIMARY KEY,
                  password VARCHAR(255) NOT NULL DEFAULT '',
                  email VARCHAR(190) NULL,
                  email_confirmed INT NOT NULL DEFAULT 0,
                  phone VARCHAR(64) NOT NULL DEFAULT '',
                  phone_confirmed INT NOT NULL DEFAULT 0,
                  unlocked INT NOT NULL DEFAULT 1
                );
                INSERT INTO users (user_id, password, email, email_confirmed) VALUES
                (7, '{md5}', 'ops@x.test', 1),
                (8, '{md5}', 'shopper@x.test', 1),
                (9, '{md5}', 'nobackend@x.test', 1);
                CREATE TABLE sessions (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  session VARCHAR(64) NOT NULL,
                  user_id INT NOT NULL,
                  time INT NOT NULL,
                  data TEXT NULL,
                  type INT NOT NULL DEFAULT 0,
                  contact_type VARCHAR(16) NULL,
                  csrf_guard_key VARCHAR(64) NULL,
                  last_activiti_time INT NULL
                );
                """);

            var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            http.HttpContext.Request.Host = new HostString("www.ecomae.com");
            http.HttpContext.Request.Headers["CF-Connecting-IP"] = "198.51.100.7";
            var service = new DbLegacyAdminLoginService(
                new ThrowawayConnections(cs),
                new BackendUsers(7, 8),
                Options.Create(new EcomAeOptions { SecretSuccession = Secret }),
                http,
                NullLogger<DbLegacyAdminLoginService>.Instance);

            Task<LegacyLoginOutcome> Login(string contact, string pw, LegacyLoginSurface surface = LegacyLoginSurface.ControlPanel)
                => service.LoginAsync(new LegacyLoginRequest(contact, pw, "email", false, surface), "198.51.100.7", "test-agent");

            var wrong = await Login("Ops@x.test ", "nope");
            Assert.Equal("invalid_credentials", wrong.Failure?.Code);
            Assert.Equal("1", await ScalarAsync(cs, "SELECT COUNT(*) FROM epc_login_attempts WHERE success = 0 AND email = 'ops@x.test' AND ip_address = '198.51.100.7'"));
            Assert.Equal(md5, await ScalarAsync(cs, "SELECT password FROM users WHERE user_id = 7"));

            var noBackend = await Login("nobackend@x.test", "right");
            Assert.Equal("no_backend_access", noBackend.Failure?.Code);
            Assert.StartsWith("$2y$12$", await ScalarAsync(cs, "SELECT password FROM users WHERE user_id = 9"), StringComparison.Ordinal);
            Assert.Equal("2", await ScalarAsync(cs, "SELECT COUNT(*) FROM epc_login_attempts WHERE success = 0"));

            var ok = await Login("ops@x.test", "right");
            Assert.True(ok.Ok);
            var upgraded = await ScalarAsync(cs, "SELECT password FROM users WHERE user_id = 7");
            Assert.StartsWith("$2y$12$", upgraded, StringComparison.Ordinal);
            Assert.True(BCrypt.Net.BCrypt.Verify("right", upgraded));
            Assert.Equal("0", await ScalarAsync(cs, "SELECT COUNT(*) FROM epc_login_attempts WHERE email = 'ops@x.test' AND success = 0"));
            Assert.Equal("1", await ScalarAsync(cs, "SELECT COUNT(*) FROM epc_login_attempts WHERE email = 'ops@x.test' AND success = 1"));
            Assert.True((await Login("ops@x.test", "right")).Ok);
            Assert.Equal(upgraded, await ScalarAsync(cs, "SELECT password FROM users WHERE user_id = 7"));

            var shopper = await Login("shopper@x.test", "nope", LegacyLoginSurface.Storefront);
            Assert.Equal("invalid_credentials", shopper.Failure?.Code);
            Assert.Equal("0", await ScalarAsync(cs, "SELECT COUNT(*) FROM epc_login_attempts WHERE email = 'shopper@x.test'"));
            Assert.True((await Login("shopper@x.test", "right", LegacyLoginSurface.Storefront)).Ok);
            Assert.StartsWith("$2y$12$", await ScalarAsync(cs, "SELECT password FROM users WHERE user_id = 8"), StringComparison.Ordinal);
            Assert.Equal("0", await ScalarAsync(cs, "SELECT COUNT(*) FROM epc_login_attempts WHERE email = 'shopper@x.test'"));

            await ExecAsync(cs, "DELETE FROM epc_login_attempts");
            await ExecAsync(cs, "INSERT INTO epc_login_attempts (ip_address, email, attempted_at, success) SELECT '203.0.113.1', 'ops@x.test', NOW() - INTERVAL 61 SECOND, 0 FROM information_schema.tables LIMIT 10");
            await ExecAsync(cs, "INSERT INTO epc_login_attempts (ip_address, email, attempted_at, success) VALUES ('203.0.113.1', 'ops@x.test', NOW() - INTERVAL 25 HOUR, 0)");
            var sessionsBefore = await ScalarAsync(cs, "SELECT COUNT(*) FROM sessions");
            var blocked = await Login("OPS@x.test", "right");
            Assert.Equal("rate_limited", blocked.Failure?.Code);
            Assert.Equal(14, blocked.Failure?.WaitMinutes);
            Assert.Equal("Too many failed attempts. Please wait 14 minutes before trying again.", blocked.Failure?.Message);
            Assert.Equal("1", await ScalarAsync(cs, "SELECT COUNT(*) FROM epc_login_attempts WHERE ip_address = '198.51.100.7' AND success = 0"));
            Assert.Equal(sessionsBefore, await ScalarAsync(cs, "SELECT COUNT(*) FROM sessions"));

            await ExecAsync(cs, "UPDATE epc_login_attempts SET attempted_at = NOW() - INTERVAL 901 SECOND WHERE ip_address = '203.0.113.1' AND attempted_at > NOW() - INTERVAL 1 HOUR");
            Assert.True((await Login("ops@x.test", "right")).Ok);

            await using (var connection = new MySqlConnection(cs))
            {
                await connection.OpenAsync();
                var check = await LegacyLoginSecurity.CheckAsync(connection, "203.0.113.1", "x@x.test", CancellationToken.None);
                Assert.Equal(new LegacyLoginSecurity.RateLimitResult(false, 10, 0), check);
                await LegacyLoginSecurity.CleanupAsync(connection, CancellationToken.None);
            }

            Assert.Equal("0", await ScalarAsync(cs, "SELECT COUNT(*) FROM epc_login_attempts WHERE attempted_at < NOW() - INTERVAL 24 HOUR"));
            Assert.Equal("10", await ScalarAsync(cs, "SELECT COUNT(*) FROM epc_login_attempts WHERE ip_address = '203.0.113.1'"));
            Assert.Equal("11", await ScalarAsync(cs, "SELECT COUNT(*) FROM epc_login_attempts WHERE ip_address = '203.0.113.1' OR success = 1"));
            Assert.Equal("2", await ScalarAsync(admin, "SELECT COUNT(*) FROM `docpart`.`users`"));
            Assert.Equal("2", await ScalarAsync(admin, "SELECT COUNT(*) FROM `ecomae`.`users`"));
        }
        finally
        {
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + database + "`");
        }

        Assert.Equal("0", await ScalarAsync(admin, "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = '" + database + "'"));
    }

    private static async Task ExecAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ScalarAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private sealed class ThrowawayConnections(string cs) : ITenantDbConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default) => OpenAsync();

        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default) => OpenAsync();

        public Task<System.Data.Common.DbConnection> OpenForTenantAsync(TenantContext? tenant, CancellationToken cancellationToken = default) => OpenAsync();

        public Task<System.Data.Common.DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default) => OpenAsync();

        private async Task<System.Data.Common.DbConnection> OpenAsync()
        {
            var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            return connection;
        }
    }

    private sealed class BackendUsers(params int[] userIds) : ILegacySessionStore
    {
        public bool IsConfigured => true;

        public Task<bool> AdminSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<bool> CustomerSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<LegacyAdminIdentity?> GetAdminIdentityAsync(int userId, CancellationToken cancellationToken = default)
            => Task.FromResult<LegacyAdminIdentity?>(new LegacyAdminIdentity(string.Empty, [], userIds.Contains(userId)));
    }
}
