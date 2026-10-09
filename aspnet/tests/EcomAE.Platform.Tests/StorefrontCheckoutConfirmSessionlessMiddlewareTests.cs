using System.Data.Common;
using System.Globalization;
using System.Text;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Data;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Services;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontCheckoutConfirmSessionlessMiddlewareTests
{
    [Theory]
    [InlineData("GET", "/shop/checkout/confirm", "en")]
    [InlineData("HEAD", "/shop/checkout/confirm/", "en")]
    [InlineData("GET", "/en/shop/checkout/confirm", "en")]
    [InlineData("GET", "/AR/shop/checkout/confirm/", "ar")]
    public void Matcher_AcceptsOnlyCanonicalGetAndHeadRoutes(string method, string path, string lang)
    {
        Assert.True(StorefrontCheckoutConfirmSessionlessMiddleware.TryMatch(method, path, out var actual));
        Assert.Equal(lang, actual);
    }

    [Theory]
    [InlineData("POST", "/shop/checkout/confirm")]
    [InlineData("GET", "/shop/checkout_confirm")]
    [InlineData("GET", "/en/shop/checkout_confirm")]
    [InlineData("GET", "/eng/shop/checkout/confirm")]
    [InlineData("GET", "/en/shop/checkout/how_get")]
    [InlineData("GET", "/shop/checkout/confirm/extra")]
    public void Matcher_LeavesMethodsAndCompatibilityAliasesAlone(string method, string path)
        => Assert.False(StorefrontCheckoutConfirmSessionlessMiddleware.TryMatch(method, path, out _));

    [Theory]
    [InlineData("GET", true)]
    [InlineData("HEAD", false)]
    public async Task SessionlessGuest_GetsRawPhpJson_WithoutMintingOrWritingSession(string method, bool expectBody)
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        await WithDatabaseAsync(password, async (connectionString, connection) =>
        {
            await ExecAsync(connection, """
                CREATE TABLE `sessions` (
                    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
                    `session` VARCHAR(128),
                    `user_id` INT,
                    `time` BIGINT,
                    `data` TEXT,
                    `last_activiti_time` BIGINT,
                    `csrf_guard_key` VARCHAR(128)
                );
                CREATE TABLE `lang_text_strings` (`str_key` VARCHAR(64) PRIMARY KEY, `is_error` TINYINT, `same` VARCHAR(8) NULL);
                CREATE TABLE `lang_text_strings_translation` (`str_key` VARCHAR(64), `lang_code` VARCHAR(8), `value` TEXT);
                INSERT INTO `lang_text_strings` VALUES ('4460',0,NULL);
                INSERT INTO `lang_text_strings_translation` VALUES ('4460','en','Incorrect session / retry');
                """);

            var tenantConnections = new TestTenantConnections(connectionString);
            var guests = new StorefrontGuestSessionService(
                new TestWriteConnections(connectionString),
                Options.Create(new EcomAeOptions { SecretSuccession = "must-not-be-used" }));
            var nextCalls = 0;
            var middleware = new StorefrontCheckoutConfirmSessionlessMiddleware(_ =>
            {
                nextCalls++;
                return Task.CompletedTask;
            });
            var context = new DefaultHttpContext();
            context.Request.Method = method;
            context.Request.Path = "/en/shop/checkout/confirm";
            context.Response.Body = new MemoryStream();

            await middleware.InvokeAsync(
                context,
                new TestSessions(new LegacySessionContext(LegacySessionKind.Anonymous, 0, null, [])),
                guests,
                tenantConnections);

            const string expected = "{\"status\":false,\"code\":\"incorrect_session\",\"message\":\"Incorrect session \\/ retry\"}";
            context.Response.Body.Position = 0;
            var body = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
            Assert.Equal(expectBody ? expected : string.Empty, body);
            Assert.Equal(Encoding.UTF8.GetByteCount(expected), context.Response.ContentLength);
            Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            Assert.Equal(0, nextCalls);
            Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
            Assert.Equal(0L, await ScalarAsync(connection, "SELECT COUNT(*) FROM `sessions`"));
        });
    }

    [Fact]
    public async Task ValidCustomer_PassesThroughWithoutGuestOrTranslatorLookup()
    {
        var nextCalls = 0;
        var middleware = new StorefrontCheckoutConfirmSessionlessMiddleware(_ =>
        {
            nextCalls++;
            return Task.CompletedTask;
        });
        var context = Context();
        var guests = new TestGuests { ThrowOnResolve = true };

        await middleware.InvokeAsync(
            context,
            new TestSessions(new LegacySessionContext(LegacySessionKind.Customer, 7, "customer", [])),
            guests,
            new ThrowingTenantConnections());

        Assert.Equal(1, nextCalls);
        Assert.Equal(0, guests.ResolveCalls);
    }

    [Fact]
    public async Task ExistingGuestSession_PassesThroughWithoutTranslatorLookupOrMinting()
    {
        var nextCalls = 0;
        var middleware = new StorefrontCheckoutConfirmSessionlessMiddleware(_ =>
        {
            nextCalls++;
            return Task.CompletedTask;
        });
        var context = Context();
        var guests = new TestGuests { Shopper = new StorefrontShopper(0, 91, "guest", false) };

        await middleware.InvokeAsync(
            context,
            new TestSessions(new LegacySessionContext(LegacySessionKind.Anonymous, 0, null, [])),
            guests,
            new ThrowingTenantConnections());

        Assert.Equal(1, nextCalls);
        Assert.Equal(1, guests.ResolveCalls);
        Assert.False(guests.LastCreateIfMissing);
    }

    private static DefaultHttpContext Context()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/shop/checkout/confirm";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task WithDatabaseAsync(string password, Func<string, MySqlConnection, Task> body)
    {
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];
        await ExecAsync(admin, "CREATE DATABASE `" + database + "` CHARACTER SET utf8mb4");
        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";";
        try
        {
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            await body(connectionString, connection);
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + database + "`");
        }
    }

    private static async Task<long> ScalarAsync(MySqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
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

    private sealed class TestSessions(LegacySessionContext result) : ILegacySessionValidator
    {
        public ValueTask<LegacySessionContext> ValidateAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(result);

        public ValueTask<LegacySessionContext> ValidateCustomerAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(result);
    }

    private sealed class TestGuests : IStorefrontGuestSessionService
    {
        public StorefrontShopper Shopper { get; init; } = new(0, 0, string.Empty, false);
        public bool ThrowOnResolve { get; init; }
        public int ResolveCalls { get; private set; }
        public bool LastCreateIfMissing { get; private set; }

        public Task<StorefrontShopper> ResolveAsync(HttpContext context, bool createIfMissing, CancellationToken cancellationToken = default)
        {
            ResolveCalls++;
            LastCreateIfMissing = createIfMissing;
            return ThrowOnResolve
                ? throw new InvalidOperationException("Guest lookup must not run.")
                : Task.FromResult(Shopper);
        }

        public void ApplyGuestCookies(HttpResponse response, StorefrontShopper shopper) => throw new NotSupportedException();
        public void ApplyCheckoutCookies(HttpResponse response, long orderId) => throw new NotSupportedException();
        public void AppendProductsInCartCookie(HttpRequest request, HttpResponse response, long cartId) => throw new NotSupportedException();
    }

    private sealed class TestWriteConnections(string connectionString) : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }

    private sealed class TestTenantConnections(string connectionString) : ITenantDbConnectionFactory
    {
        public bool IsConfigured => true;
        public Task<DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default) => Open(cancellationToken);
        public Task<DbConnection> OpenAsync(string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default) => Open(cancellationToken);
        public Task<DbConnection> OpenForTenantAsync(TenantContext? tenant, CancellationToken cancellationToken = default) => Open(cancellationToken);
        public Task<DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default) => Open(cancellationToken);

        private async Task<DbConnection> Open(CancellationToken cancellationToken)
        {
            var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }

    private sealed class ThrowingTenantConnections : ITenantDbConnectionFactory
    {
        public bool IsConfigured => true;
        public Task<DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<DbConnection> OpenAsync(string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<DbConnection> OpenForTenantAsync(TenantContext? tenant, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default) => throw Unexpected();
        private static InvalidOperationException Unexpected() => new("Translator lookup must not run.");
    }
}
