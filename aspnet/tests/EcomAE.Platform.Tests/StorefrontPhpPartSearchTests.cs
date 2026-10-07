using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using EcomAE.Platform.Data;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Services;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontPhpPartSearchTests
{
    [Fact]
    public void Analogs_EmptyArticle_MatchesPhp()
    {
        var body = (StorefrontPhpAjax.AnalogsBody)StorefrontPhpAjax.AnalogsEmptyArticle();
        Assert.Equal(0, body.Result);
        Assert.Equal(1, body.Check);
        Assert.Empty(body.Analogs);
        Assert.Null(body.Time);
        Assert.Null(body.Message);
    }

    [Fact]
    public void PartInfo_RefererAndUnconfiguredUcats_MatchPhp()
    {
        Assert.False(StorefrontPhpAjax.RefererAllowed(null, "www.epartscart.com"));
        Assert.False(StorefrontPhpAjax.RefererAllowed("https://evil.test/www.epartscart.com", "www.epartscart.com"));
        Assert.True(StorefrontPhpAjax.RefererAllowed("https://www.epartscart.com/en/shop/part_search", "www.epartscart.com"));
        var body = (StorefrontPhpAjax.PartInfoBody)StorefrontPhpAjax.PartInfoUnconfigured();
        Assert.Equal(0, body.Result);
    }

    [Fact]
    public void CrossArticle_KeepsCyrillicAndStripsSeparators()
    {
        Assert.Equal("АБВ0986", StorefrontPhpAjax.NormalizeCrossArticle("абв-0986"));
        Assert.Equal("0986AB", StorefrontPhpAjax.NormalizePriceArticle("0986 ab"));
    }

    [Fact]
    public async Task PartSearch_OnThrowawayDatabase_ThenDropped()
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
                CREATE TABLE shop_storages (
                  id INT NOT NULL PRIMARY KEY,
                  interface_type INT NOT NULL,
                  connection_options TEXT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_storages_interfaces_types (
                  id INT NOT NULL PRIMARY KEY,
                  handler_folder VARCHAR(64) NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_docpart_prices_data (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  price_id INT NOT NULL,
                  manufacturer VARCHAR(255) NOT NULL,
                  article VARCHAR(255) NOT NULL,
                  name VARCHAR(255) NOT NULL,
                  exist INT NOT NULL,
                  price DECIMAL(15,2) NOT NULL,
                  storage VARCHAR(255) NOT NULL,
                  time_to_exe INT NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_docpart_articles_analogs_list (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  article VARCHAR(64) NOT NULL,
                  analog VARCHAR(64) NOT NULL,
                  manufacturer_article VARCHAR(64) NOT NULL,
                  manufacturer_analog VARCHAR(64) NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_offices_storages_map (
                  office_id INT NOT NULL,
                  storage_id INT NOT NULL,
                  group_id INT NOT NULL,
                  min_point DECIMAL(12,2) NOT NULL DEFAULT 0
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
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_carts_details (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  cart_record_id INT NOT NULL,
                  comment VARCHAR(32) NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO shop_storages_interfaces_types (id, handler_folder) VALUES (1, 'prices'), (2, 'supplier_api')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_storages (id, interface_type, connection_options) VALUES (4, 1, '{\"price_id\":9}'), (8, 2, '{}')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_docpart_prices_data (price_id, manufacturer, article, name, exist, price, storage, time_to_exe) VALUES (9, 'BOSCH', '0986', 'Pad', 4, 12.50, 'Sharjah', 2)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_docpart_articles_analogs_list (article, analog, manufacturer_article, manufacturer_analog) VALUES ('0986', '0987', 'BOSCH', 'MANN')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_carts (id, user_id, session_id, price, count_need) VALUES (1, 0, 11, 9.00, 1), (2, 0, 12, 3.00, 1), (3, 5, 0, 8.00, 1)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_carts_details (cart_record_id, comment) VALUES (1, 'guest'), (2, 'other')");

            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var supplier = (StorefrontPhpAjax.ManufacturersBody)await StorefrontPhpAjax.ManufacturersFromStorageAsync(
                connection, "{\"article\":\"0986\"}", 8, CancellationToken.None);
            Assert.False(supplier.Status);
            Assert.Equal(StorefrontPhpAjax.StorageHandlerManufacturersError, supplier.Message);

            var prices = (StorefrontPhpAjax.ManufacturersBody)await StorefrontPhpAjax.ManufacturersFromPricesAsync(
                connection,
                "{\"article\":\"0986\"}",
                "[{\"office_id\":1,\"storage_id\":4}]",
                0,
                CancellationToken.None);
            Assert.True(prices.Status);
            Assert.Equal(StorefrontPhpAjax.PricesStorageLabel, prices.Storage);
            var brand = Assert.Single(prices.ProductsManufacturers);
            Assert.Equal("BOSCH", brand.Manufacturer);
            Assert.Equal("BOSCH", brand.ManufacturerShow);
            Assert.Equal("Pad", brand.Name);
            Assert.Equal(4, brand.StorageId);
            Assert.Equal(1, brand.OfficeId);
            Assert.True(brand.Valid);
            Assert.Equal("prices", brand.Params["type"]);

            var crosses = (StorefrontPhpAjax.ManufacturersBody)await StorefrontPhpAjax.ManufacturersFromCrossServerAsync(
                connection, "{\"article\":\"0986\"}", CancellationToken.None);
            Assert.True(crosses.Status);
            Assert.Equal(StorefrontPhpAjax.CrossStorageLabel, crosses.Storage);
            Assert.Equal("BOSCH", Assert.Single(crosses.ProductsManufacturers).Manufacturer);

            var emptyCross = (StorefrontPhpAjax.ManufacturersBody)await StorefrontPhpAjax.ManufacturersFromCrossServerAsync(
                connection, "{\"article\":\"\"}", CancellationToken.None);
            Assert.True(emptyCross.Status);
            Assert.Empty(emptyCross.ProductsManufacturers);

            var analogs = (StorefrontPhpAjax.AnalogsBody)await StorefrontPhpAjax.AnalogsListAsync(
                connection,
                "{\"article\":\"0986\",\"manufacturers\":[{\"manufacturer\":\"BOSCH\"}]}",
                CancellationToken.None);
            Assert.Equal(1, analogs.Result);
            Assert.Equal(1, analogs.Check);
            var analog = Assert.Single(analogs.Analogs, row => row.Article == "0987");
            Assert.Equal("MANN", analog.Manufacturer);
            Assert.Equal("table", analog.Type);

            var unknown = await StorefrontPhpAjax.AsynchronAsync(connection, "{\"action\":\"nope\",\"article\":\"0986\",\"storages\":[]}", true, CancellationToken.None);
            var unknownBody = (StorefrontPhpAjax.AsynchronBody)unknown.Payload;
            Assert.Equal(0, unknownBody.Result);
            Assert.Equal(StorefrontPhpAjax.UnknownActionStringKey, unknownBody.Msg);

            var emptyStorages = await StorefrontPhpAjax.AsynchronAsync(
                connection,
                "{\"action\":\"get_manufacturers\",\"article\":\"0986\",\"storages\":[]}",
                true,
                CancellationToken.None);
            Assert.Equal(StorefrontPhpAjax.EmptyStoragesStringKey, ((StorefrontPhpAjax.AsynchronBody)emptyStorages.Payload).Msg);

            var asyncPrices = await StorefrontPhpAjax.AsynchronAsync(
                connection,
                "{\"action\":\"get_manufacturers\",\"article\":\"0986\",\"storages\":[{\"protocol_version\":3,\"office_storage_bunches\":[{\"office_id\":1,\"storage_id\":4}]}]}",
                false,
                CancellationToken.None);
            var asyncBody = (StorefrontPhpAjax.AsynchronBody)asyncPrices.Payload;
            Assert.Equal(1, asyncBody.Result);
            var asyncList = Assert.IsAssignableFrom<IReadOnlyList<object>>(asyncBody.Data);
            var asyncManufacturer = Assert.IsType<StorefrontPhpAjax.ManufacturersBody>(Assert.Single(asyncList));
            Assert.Equal("BOSCH", Assert.Single(asyncManufacturer.ProductsManufacturers).Manufacturer);

            var bunch = (StorefrontPhpAjax.Bunch2Body)await StorefrontPhpAjax.ProductsOfBunch2Async(
                connection,
                "0986",
                0,
                0,
                "{\"office_storage_bunches\":[{\"office_id\":1,\"storage_id\":4}]}",
                0,
                0,
                pricesVisible: false,
                CancellationToken.None);
            Assert.Equal(1, bunch.Result);
            Assert.False(bunch.PricesVisible);
            var product = Assert.Single(bunch.Products);
            Assert.Equal(0m, product.Price);
            Assert.Equal(1, product.Exist);
            Assert.Equal(string.Empty, product.Storage);
            Assert.Equal(0, product.StorageId);
            Assert.Equal("BOSCH", product.Manufacturer);

            var supplierBunch = (StorefrontPhpAjax.Bunch2Body)await StorefrontPhpAjax.ProductsOfBunch2Async(
                connection, "0986", 1, 8, null, 0, 0, true, CancellationToken.None);
            Assert.Equal(0, supplierBunch.Result);
            Assert.Equal(StorefrontPhpAjax.StorageHandlerError, supplierBunch.Message);
            Assert.Empty(supplierBunch.Products);

            var guest = (StorefrontPhpAjax.CartBody)await StorefrontPhpAjax.CartInfoAsync(connection, 0, 11, pricesVisible: false, CancellationToken.None);
            Assert.Equal(0, guest.CartItemsCount);
            Assert.Equal(string.Empty, guest.CartItemsSum);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts WHERE id = 1"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts_details WHERE cart_record_id = 1"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts WHERE id = 2"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts WHERE id = 3"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts_details WHERE cart_record_id = 2"));

            var kept = (StorefrontPhpAjax.CartBody)await StorefrontPhpAjax.CartInfoAsync(connection, 5, 0, pricesVisible: true, CancellationToken.None);
            Assert.Equal(1, kept.CartItemsCount);
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts WHERE user_id = 5"));

            await ExecuteAsync(connectionString, "DROP TABLE shop_docpart_prices_data");
            var missingPrices = (StorefrontPhpAjax.ManufacturersBody)await StorefrontPhpAjax.ManufacturersFromPricesAsync(
                connection, "{\"article\":\"0986\"}", "[{\"office_id\":1,\"storage_id\":4}]", 0, CancellationToken.None);
            Assert.Equal("Price lists are not in this database.", missingPrices.Message);
            Assert.DoesNotContain("doesn't exist", missingPrices.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(missingPrices.ProductsManufacturers);

            await ExecuteAsync(connectionString, "DROP TABLE shop_docpart_articles_analogs_list");
            var missingCross = (StorefrontPhpAjax.ManufacturersBody)await StorefrontPhpAjax.ManufacturersFromCrossServerAsync(
                connection, "{\"article\":\"0986\"}", CancellationToken.None);
            Assert.Equal("Cross references are not in this database.", missingCross.Message);
            Assert.DoesNotContain("doesn't exist", missingCross.Message, StringComparison.OrdinalIgnoreCase);
            var missingAnalogs = (StorefrontPhpAjax.AnalogsBody)await StorefrontPhpAjax.AnalogsListAsync(
                connection, "{\"article\":\"0986\"}", CancellationToken.None);
            Assert.Equal(0, missingAnalogs.Result);
            Assert.Equal("Cross references are not in this database.", missingAnalogs.Message);

            await ExecuteAsync(connectionString, "DROP TABLE shop_offices_storages_map");
            var missingMarkup = (StorefrontPhpAjax.Bunch2Body)await StorefrontPhpAjax.ProductsOfBunch2Async(
                connection, "0986", 2, 8, null, 0, 0, true, CancellationToken.None);
            Assert.Equal("Office storage markups are not in this database.", missingMarkup.Message);

            await using var host = await StartAsync(connectionString);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var emptyAnalogs = await client.PostAsync(
                StorefrontPhpAjax.AnalogsListPath,
                new FormUrlEncodedContent(new Dictionary<string, string> { ["search_object"] = "{\"article\":\"\"}" }));
            Assert.Equal(HttpStatusCode.OK, emptyAnalogs.StatusCode);
            var emptyJson = await JsonDocument.ParseAsync(await emptyAnalogs.Content.ReadAsStreamAsync());
            Assert.Equal(0, emptyJson.RootElement.GetProperty("result").GetInt32());
            Assert.Equal(1, emptyJson.RootElement.GetProperty("check").GetInt32());
            Assert.Equal(0, emptyJson.RootElement.GetProperty("analogs").GetArrayLength());
            Assert.False(emptyJson.RootElement.TryGetProperty("time", out _));

            var forbidden = await client.GetAsync(StorefrontPhpAjax.PartInfoPath);
            Assert.Equal("Forbidden 403", await forbidden.Content.ReadAsStringAsync());
            using var allowed = new HttpRequestMessage(HttpMethod.Get, StorefrontPhpAjax.PartInfoPath + "?image_path=missing.jpg");
            allowed.Headers.TryAddWithoutValidation("Referer", host.BaseAddress + "en/shop/part_search");
            var info = await client.SendAsync(allowed);
            var infoJson = await JsonDocument.ParseAsync(await info.Content.ReadAsStreamAsync());
            Assert.Equal(0, infoJson.RootElement.GetProperty("result").GetInt32());
            Assert.False(infoJson.RootElement.TryGetProperty("json", out _));
            Assert.False(infoJson.RootElement.TryGetProperty("message", out _));

            var handler = await client.PostAsync(
                StorefrontPhpAjax.ManufacturersListPath,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["storage_id"] = "8",
                    ["office_id"] = "1",
                    ["query"] = "{\"article\":\"0986\"}"
                }));
            var handlerJson = await JsonDocument.ParseAsync(await handler.Content.ReadAsStreamAsync());
            Assert.False(handlerJson.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(StorefrontPhpAjax.StorageHandlerManufacturersError, handlerJson.RootElement.GetProperty("message").GetString());
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task<string> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
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
        return new ProbeHost(app, new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/"));
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
            => throw new InvalidOperationException(targetMethod?.Name ?? "dispatch");
    }
}
