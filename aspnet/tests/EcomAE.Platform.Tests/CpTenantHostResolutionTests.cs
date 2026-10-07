using System.Data.Common;
using System.Globalization;
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
/// PHP binds electronicae, stylenlook, thejewellerytrend, and taxofinca to shared
/// <c>docpart</c> when <c>epc_portal_tenants</c> has no dedicated row. industries.ecomae.com
/// <c>/cp</c> is platform <c>ecomae</c>. This test does not insert portal rows or accounts
/// into those databases. Login proof opens a throwaway database instead of <c>docpart</c>.
/// </summary>
public sealed class CpTenantHostResolutionTests
{
    [Theory]
    [InlineData("www.electronicae.com", "electronicae")]
    [InlineData("electronicae.com", "electronicae")]
    [InlineData("www.stylenlook.com", "stylenlook")]
    [InlineData("stylenlook.com", "stylenlook")]
    [InlineData("www.thejewellerytrend.com", "thejewellerytrend")]
    [InlineData("thejewellerytrend.com", "thejewellerytrend")]
    [InlineData("www.taxofinca.com", "taxofinca")]
    [InlineData("taxofinca.com", "taxofinca")]
    public async Task EmptyRegistry_BindsNamedShopsToSharedDocpart(string host, string siteKey)
    {
        var tenant = await ResolveAsync(host, "/cp/login", EmptyOptions());

        Assert.True(tenant.HasTenantDatabase);
        Assert.Equal("docpart", tenant.DatabaseName);
        Assert.Equal(siteKey, tenant.SiteKey);
        Assert.False(tenant.DedicatedDb);
        Assert.Equal(TenantMode.LiveTenant, tenant.Mode);
        Assert.Equal(TenantSurface.ControlPanel, tenant.Surface);
        Assert.Null(tenant.DbUser);
        Assert.Null(tenant.DbPassword);
    }

    [Fact]
    public async Task DedicatedRegistryDatabase_WinsOverSharedDocpart()
    {
        var options = EmptyOptions();
        var registry = new StubRegistry(new TenantRegistryRecord(
            "www.electronicae.com",
            TenantMode.LiveTenant,
            "electronicae",
            "electronicae_shop",
            StorefrontEnabled: true,
            ErpEnabled: true,
            ControlPanelEnabled: true,
            BosEnabled: false,
            DedicatedDb: true));
        var resolver = new RouteTenantResolver(options, registry);
        var http = new DefaultHttpContext();
        http.Request.Host = new HostString("www.electronicae.com");
        http.Request.Path = "/cp/login";

        var tenant = await resolver.ResolveAsync(http);

        Assert.Equal("electronicae_shop", tenant.DatabaseName);
        Assert.True(tenant.DedicatedDb);
        Assert.NotEqual("docpart", tenant.DatabaseName);
    }

    [Fact]
    public async Task IndustriesControlPanel_UsesPlatformDatabase()
    {
        Assert.False(PlatformHostPolicy.IsSuperCpHost("industries.ecomae.com"));

        var cp = await ResolveAsync("industries.ecomae.com", "/cp/login", EmptyOptions());
        Assert.Equal(TenantMode.Platform, cp.Mode);
        Assert.Equal("platform", cp.SiteKey);
        Assert.Equal("ecomae", cp.DatabaseName);
        Assert.NotEqual("docpart", cp.DatabaseName);
        Assert.False(cp.DedicatedDb);

        var erp = await ResolveAsync("industries.ecomae.com", "/erp", EmptyOptions());
        Assert.Equal(TenantMode.Platform, erp.Mode);
        Assert.Equal("ecomae", erp.DatabaseName);

        var storefront = await ResolveAsync("industries.ecomae.com", "/", EmptyOptions());
        Assert.False(storefront.HasTenantDatabase);
        Assert.NotEqual("docpart", storefront.DatabaseName);
    }

    [Fact]
    public async Task ExistingAccount_CanSignInWhenHostResolves_WithoutWritingDocpart()
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
        var docpartCount = await ScalarAsync(admin, "SELECT COUNT(*) FROM `docpart`.`users`");
        var ecomaeCount = await ScalarAsync(admin, "SELECT COUNT(*) FROM `ecomae`.`users`");
        var portalCount = await ScalarAsync(admin, """
            SELECT COUNT(*) FROM `ecomae`.`epc_portal_tenants`
            WHERE `hostname` IN (
              'www.electronicae.com','electronicae.com',
              'www.stylenlook.com','stylenlook.com',
              'www.thejewellerytrend.com','thejewellerytrend.com',
              'www.taxofinca.com','taxofinca.com',
              'industries.ecomae.com')
            """);
        Assert.Equal("0", portalCount);
        try
        {
            await ExecuteAsync(connectionString, """
                CREATE TABLE users (
                  user_id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  email VARCHAR(190) NULL,
                  password VARCHAR(255) NULL,
                  email_confirmed TINYINT NULL,
                  unlocked TINYINT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE sessions (
                  session VARCHAR(255) NOT NULL,
                  user_id INT NOT NULL,
                  time BIGINT NOT NULL,
                  data TEXT NULL,
                  type INT NULL,
                  contact_type VARCHAR(32) NULL,
                  csrf_guard_key VARCHAR(255) NULL,
                  last_activiti_time BIGINT NULL
                )
                """);
            const string secret = "host-resolution-secret";
            var plain = "ProbeHostLogin1!";
            var hash = LegacyPasswordVerifier.Md5Hex(plain + secret);
            await ExecuteAsync(
                connectionString,
                "INSERT INTO users (email, password, email_confirmed, unlocked) VALUES ('hostprobe@local.test', '" + hash + "', 1, 1)");

            var shop = await ResolveAsync("www.electronicae.com", "/cp/login", EmptyOptions());
            Assert.Equal("docpart", shop.DatabaseName);
            var shopLogin = await LoginAsync(shop, "www.electronicae.com", plain, secret, connectionString);
            Assert.True(shopLogin.Outcome.Ok, shopLogin.Outcome.Failure?.Code);
            Assert.NotEqual("tenant_db_unbound", shopLogin.Outcome.Failure?.Code);
            Assert.Equal("docpart", shopLogin.RequestedDatabase);
            Assert.Equal("hostprobe@local.test", shopLogin.Outcome.Success?.Email);

            var industries = await ResolveAsync("industries.ecomae.com", "/cp/login", EmptyOptions());
            Assert.Equal("ecomae", industries.DatabaseName);
            var industriesLogin = await LoginAsync(industries, "industries.ecomae.com", plain, secret, connectionString);
            Assert.True(industriesLogin.Outcome.Ok, industriesLogin.Outcome.Failure?.Code);
            Assert.NotEqual("tenant_db_unbound", industriesLogin.Outcome.Failure?.Code);
            Assert.Equal("ecomae", industriesLogin.RequestedDatabase);

            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM sessions"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }

        Assert.Equal(docpartCount, await ScalarAsync(admin, "SELECT COUNT(*) FROM `docpart`.`users`"));
        Assert.Equal(ecomaeCount, await ScalarAsync(admin, "SELECT COUNT(*) FROM `ecomae`.`users`"));
        Assert.Equal("0", await ScalarAsync(admin, """
            SELECT COUNT(*) FROM `ecomae`.`epc_portal_tenants`
            WHERE `hostname` IN (
              'www.electronicae.com','electronicae.com',
              'www.stylenlook.com','stylenlook.com',
              'www.thejewellerytrend.com','thejewellerytrend.com',
              'www.taxofinca.com','taxofinca.com',
              'industries.ecomae.com')
            """));
    }

    private static async Task<(LegacyLoginOutcome Outcome, string? RequestedDatabase)> LoginAsync(
        TenantContext tenant,
        string host,
        string plain,
        string secret,
        string throwawayConnectionString)
    {
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        http.HttpContext.Request.Host = new HostString(host);
        http.HttpContext.Request.Path = "/cp/login";
        http.HttpContext.Items[TenantResolutionMiddleware.HttpContextItemKey] = tenant;
        var connections = new ThrowawayInsteadOfNamedDb(throwawayConnectionString);
        var service = new DbLegacyAdminLoginService(
            connections,
            new BackendSessionStore(),
            Options.Create(new EcomAeOptions { SecretSuccession = secret }),
            http,
            NullLogger<DbLegacyAdminLoginService>.Instance);
        var outcome = await service.LoginAsync(
            new LegacyLoginRequest("hostprobe@local.test", plain, "email", false, LegacyLoginSurface.ControlPanel),
            "127.0.0.1",
            "test-agent");
        return (outcome, connections.RequestedDatabase);
    }

    private static IOptions<EcomAeOptions> EmptyOptions()
        => Options.Create(new EcomAeOptions
        {
            PlatformHost = "www.ecomae.com",
            SeedTenants =
            [
                new TenantSeedOptions
                {
                    Host = "www.ecomae.com",
                    SiteKey = "platform",
                    DatabaseName = "ecomae",
                    Mode = TenantMode.Platform,
                },
            ],
        });

    private static async Task<TenantContext> ResolveAsync(string host, string path, IOptions<EcomAeOptions> options)
    {
        var resolver = new RouteTenantResolver(options, new ConfigurationTenantRegistry(options));
        var http = new DefaultHttpContext();
        http.Request.Host = new HostString(host);
        http.Request.Path = path;
        return await resolver.ResolveAsync(http);
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

    private sealed class StubRegistry : ITenantRegistry
    {
        private readonly TenantRegistryRecord _record;

        public StubRegistry(TenantRegistryRecord record) => _record = record;

        public ValueTask<TenantRegistryRecord?> FindByHostAsync(string host, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<TenantRegistryRecord?>(_record);
    }

    private sealed class BackendSessionStore : ILegacySessionStore
    {
        public bool IsConfigured => true;

        public Task<bool> AdminSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> CustomerSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<LegacyAdminIdentity?> GetAdminIdentityAsync(int userId, CancellationToken cancellationToken = default)
            => Task.FromResult<LegacyAdminIdentity?>(new LegacyAdminIdentity("hostprobe@local.test", [3], true));
    }

    /// <summary>
    /// Records the database the resolver asked to open, then opens only the throwaway schema.
    /// </summary>
    private sealed class ThrowawayInsteadOfNamedDb : ITenantDbConnectionFactory
    {
        private readonly string _connectionString;

        public ThrowawayInsteadOfNamedDb(string connectionString) => _connectionString = connectionString;

        public string? RequestedDatabase { get; private set; }

        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
        {
            RequestedDatabase = databaseName;
            return OpenThrowawayAsync();
        }

        public Task<DbConnection> OpenAsync(string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default)
        {
            RequestedDatabase = databaseName;
            return OpenThrowawayAsync();
        }

        public Task<DbConnection> OpenForTenantAsync(TenantContext? tenant, CancellationToken cancellationToken = default)
        {
            RequestedDatabase = tenant?.DatabaseName;
            return OpenThrowawayAsync();
        }

        public Task<DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("registry open is not part of this login proof");

        private async Task<DbConnection> OpenThrowawayAsync()
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }
    }
}
