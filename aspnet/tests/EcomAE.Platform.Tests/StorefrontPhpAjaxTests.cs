using System.Globalization;
using System.Net;
using System.Text.Json;
using EcomAE.Platform.Data;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Services;
using EcomAE.Platform.Storefront;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontPhpAjaxTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void WarehouseOffers_EmptyArticle_MatchesPhp()
    {
        var body = (StorefrontPhpAjax.WarehouseBody)StorefrontPhpAjax.WarehouseOffers(
            new StorefrontPartSearchResult(string.Empty, [], 0, "empty", "Enter a part number or OE code."),
            string.Empty,
            pricesVisible: true);

        Assert.False(body.Status);
        Assert.Equal("Empty article", body.Message);
        Assert.Empty(body.Rows);
    }

    [Fact]
    public void WarehouseOffers_GuestMask_HidesPriceQtyAndWarehouse()
    {
        var offer = new StorefrontPartOfferDigest(4, "Main", "BOSCH", "0986", "0 986", "Pad", 12.5m, 6, "Sharjah", "2");
        var body = (StorefrontPhpAjax.WarehouseBody)StorefrontPhpAjax.WarehouseOffers(
            new StorefrontPartSearchResult("0986", [offer], 1, "database", string.Empty),
            "BOSCH",
            pricesVisible: false);
        var row = Assert.Single(body.Rows);
        Assert.Null(row.Price);
        Assert.Null(row.Exist);
        Assert.Equal(string.Empty, row.Storage);
        Assert.Null(row.TimeToExe);
        Assert.Equal("BOSCH", row.Manufacturer);
        Assert.DoesNotContain("doesn't exist", body.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WarehouseOffers_MissingTableMessage_IsClear()
    {
        var body = (StorefrontPhpAjax.WarehouseBody)StorefrontPhpAjax.WarehouseOffers(
            new StorefrontPartSearchResult("0986", [], 0, "database-error", "Table 'docpart.shop_docpart_prices_data' doesn't exist"),
            "BOSCH",
            pricesVisible: true);
        Assert.Equal("Warehouse offers are not in this database.", body.Message);
        Assert.DoesNotContain("doesn't exist", body.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CrossSearch_GuestMask_UsesSensitiveMask()
    {
        var stock = new StorefrontCrossStockDigest("BOSCH", "0986", "0986", "Pad", 9m, 4m, "2 days", "Sharjah", 8, 3);
        var body = (StorefrontPhpAjax.CrossBody)StorefrontPhpAjax.CrossSearch(
            new StorefrontCrossSearchResult("0986", "BOSCH", [], [stock], 0, 1, 1, "aspnet-cross-local+crossbase", string.Empty),
            pricesVisible: false);
        var row = Assert.Single(body.Stock);
        Assert.Equal(0m, row.Price);
        Assert.Null(row.Qty);
        Assert.Equal(StorefrontPhpAjax.SensitiveMask, row.Warehouse);
        Assert.Equal(0, row.PriceId);
        Assert.False(row.PricesVisible);
        Assert.False(body.PricesVisible);
    }

    [Fact]
    public void ArticleBrands_EmptyArticle_MatchesPhp()
    {
        var body = (StorefrontPhpAjax.BrandsBody)StorefrontPhpAjax.ArticleBrands(
            new StorefrontArticleBrandsResult(string.Empty, [], 0, "empty", "Enter a part number or OE code."));
        Assert.False(body.Status);
        Assert.Equal("Empty article", body.Message);
        Assert.Empty(body.Manufacturers);
    }

    [Fact]
    public void Csrf_MissingKey_MatchesPhp()
    {
        var body = (StorefrontPhpAjax.CsrfBody)StorefrontPhpAjax.CsrfFailure("Error! CSRF 1");
        Assert.False(body.Status);
        Assert.Equal("Error! CSRF 1", body.Message);
        Assert.Equal("Error! CSRF 1", body.Error);
    }

    [Fact]
    public async Task HeaderCounts_OnThrowawayDatabase_ThenDropped()
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
                CREATE TABLE sessions (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  session VARCHAR(255) NOT NULL,
                  user_id INT NOT NULL,
                  csrf_guard_key VARCHAR(255) NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_carts (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  user_id INT NOT NULL,
                  session_id INT NOT NULL,
                  price DECIMAL(12,2) NOT NULL,
                  count_need INT NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, "CREATE TABLE shop_orders (id INT NOT NULL PRIMARY KEY, user_id INT NOT NULL)");
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_orders_messages (
                  id INT NOT NULL PRIMARY KEY,
                  `read` TINYINT NOT NULL,
                  is_customer TINYINT NOT NULL,
                  order_id INT NOT NULL,
                  return_id INT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_orders_returns (id INT NOT NULL PRIMARY KEY, user_id INT NOT NULL)
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE users_vin (
                  id INT NOT NULL PRIMARY KEY,
                  user_id INT NOT NULL,
                  viewed_customer TINYINT NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO sessions (session, user_id, csrf_guard_key) VALUES ('sess-probe', 7, 'csrf-probe')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_carts (user_id, session_id, price, count_need) VALUES (7, 0, 10.00, 2)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders (id, user_id) VALUES (3, 7)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_messages (id, `read`, is_customer, order_id) VALUES (1, 0, 0, 3)");
            await ExecuteAsync(connectionString, "INSERT INTO users_vin (id, user_id, viewed_customer) VALUES (1, 7, 0)");

            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            var missing = await StorefrontPhpAjax.ReadCsrfAsync(connection, "sess-probe", null, CancellationToken.None);
            Assert.Equal("Error! CSRF 1", missing.Message);
            var wrong = await StorefrontPhpAjax.ReadCsrfAsync(connection, "sess-probe", "nope", CancellationToken.None);
            Assert.Equal("Error! CSRF 4", wrong.Message);
            var csrf = await StorefrontPhpAjax.ReadCsrfAsync(connection, "sess-probe", "csrf-probe", CancellationToken.None);
            Assert.True(csrf.Ok);
            Assert.Equal(7, csrf.UserId);

            var cart = (StorefrontPhpAjax.CartBody)await StorefrontPhpAjax.CartInfoAsync(connection, csrf.UserId, csrf.SessionRecordId, pricesVisible: true, CancellationToken.None);
            Assert.Equal(1, cart.CartItemsCount);
            Assert.Equal("20.00", cart.CartItemsSum);

            var unread = (StorefrontPhpAjax.CountBody)await StorefrontPhpAjax.UnreadMessagesAsync(connection, 7, returns: false, CancellationToken.None);
            Assert.True(unread.Status);
            Assert.Equal(1, unread.Count);

            var vin = (StorefrontPhpAjax.VinBody)await StorefrontPhpAjax.VinUnreadAsync(connection, 7, CancellationToken.None);
            Assert.True(vin.Status);
            Assert.Equal(1, vin.Count);

            var guest = (StorefrontPhpAjax.VinBody)await StorefrontPhpAjax.VinUnreadAsync(connection, 0, CancellationToken.None);
            Assert.False(guest.Status);
            Assert.Equal("Forbidden", guest.Message);
            Assert.Equal(501, guest.Code);

            await ExecuteAsync(connectionString, "DROP TABLE shop_carts");
            var missingCart = (StorefrontPhpAjax.StatusBody)await StorefrontPhpAjax.CartInfoAsync(connection, 7, 1, true, CancellationToken.None);
            Assert.False(missingCart.Status);
            Assert.Equal("Cart is not in this database.", missingCart.Message);
            Assert.DoesNotContain("doesn't exist", missingCart.Message, StringComparison.OrdinalIgnoreCase);

            await ExecuteAsync(connectionString, "DROP TABLE users_vin");
            var missingVin = (StorefrontPhpAjax.VinBody)await StorefrontPhpAjax.VinUnreadAsync(connection, 7, CancellationToken.None);
            Assert.Equal("VIN requests are not in this database.", missingVin.Message);

            await using var host = await StartAsync(connectionString);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var denied = await client.GetAsync(StorefrontPhpAjax.CartInfoPath);
            Assert.Equal(HttpStatusCode.OK, denied.StatusCode);
            var deniedJson = await JsonDocument.ParseAsync(await denied.Content.ReadAsStreamAsync());
            Assert.Equal("Error! CSRF 1", deniedJson.RootElement.GetProperty("message").GetString());
            var offers = await client.GetAsync(StorefrontPhpAjax.WarehouseOffersPath);
            Assert.Equal(HttpStatusCode.OK, offers.StatusCode);
            var offersJson = await JsonDocument.ParseAsync(await offers.Content.ReadAsStreamAsync());
            Assert.False(offersJson.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("Empty article", offersJson.RootElement.GetProperty("message").GetString());
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task<ProbeHost> StartAsync(string connectionString)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture));
        builder.Services.AddSingleton<ITenantDbConnectionFactory>(new FixedConnections(connectionString));
        builder.Services.AddSingleton<IStorefrontPriceAccess>(new GuestPrices());
        builder.Services.AddSingleton(ReporterStub.Create());
        var app = builder.Build();
        StorefrontPhpAjaxEndpoints.Map(app);
        await app.StartAsync();
        return new ProbeHost(app, new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture)));
    }

    private sealed class ProbeHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        public ProbeHost(WebApplication app, Uri baseAddress)
        {
            _app = app;
            BaseAddress = baseAddress;
        }

        public Uri BaseAddress { get; }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
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

    private sealed class GuestPrices : IStorefrontPriceAccess
    {
        public ValueTask<StorefrontPriceAccessResult> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new StorefrontPriceAccessResult(StorefrontPriceAccessState.Guest, false, "**", string.Empty, string.Empty));

        public IReadOnlyList<StorefrontPartOfferDigest> RedactOffers(IReadOnlyList<StorefrontPartOfferDigest> offers) => offers;
    }

    private sealed class FixedConnections : ITenantDbConnectionFactory
    {
        private readonly string _connectionString;

        public FixedConnections(string connectionString) => _connectionString = connectionString;

        public bool IsConfigured => true;

        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
            => OpenAsync();

        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default)
            => OpenAsync();

        public Task<System.Data.Common.DbConnection> OpenForTenantAsync(TenantContext? tenant, CancellationToken cancellationToken = default)
            => OpenAsync();

        public Task<System.Data.Common.DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default)
            => OpenAsync();

        private async Task<System.Data.Common.DbConnection> OpenAsync()
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }
    }

    private class ReporterStub : System.Reflection.DispatchProxy
    {
        public ReporterStub()
        {
        }

        public static ISurfaceDashboardSummaryReporter Create()
            => Create<ISurfaceDashboardSummaryReporter, ReporterStub>();

        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.ReturnType == typeof(Task<StorefrontPartSearchResult>))
            {
                return Task.FromResult(new StorefrontPartSearchResult(string.Empty, [], 0, "empty", "Enter a part number or OE code."));
            }

            throw new InvalidOperationException(targetMethod?.Name ?? "dispatch");
        }
    }
}
