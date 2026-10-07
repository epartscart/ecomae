using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Cp.PriceImport;
using EcomAE.Platform.Data;
using EcomAE.Platform.Services;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// UMAPI catalog sync, C110J interchange size, and supplier price upload speed
/// on a database this test creates and drops.
/// </summary>
public sealed class StorefrontCatalogSyncTests
{
    [Fact]
    public async Task UmapiSync_SavesLiveCatalog_AndReusesIt()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var (database, admin, connectionString) = await CreateDatabaseAsync(password);
        var handler = new CountingHandler();
        try
        {
            await using var host = await StartAsync(connectionString, handler);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var first = await client.GetAsync(HomeCatalogWidgets.UmapiProxyPath + "?action=manufacturers&section=passenger");
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            var firstBody = await first.Content.ReadAsStringAsync();
            Assert.Contains("Toyota", firstBody, StringComparison.Ordinal);
            Assert.Equal(1, handler.Calls);
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_umapi_manufacturers WHERE manufacturer = 'Toyota'"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT connected FROM epc_umapi_sync_status WHERE id = 1"));
            Assert.NotEqual("0", await ScalarAsync(connectionString, "SELECT last_sync FROM epc_umapi_cache WHERE action = 'manufacturers'"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_umapi_usage_log WHERE is_live = 1"));

            var second = await client.GetAsync(HomeCatalogWidgets.UmapiProxyPath + "?action=manufacturers&section=passenger");
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.Contains("Toyota", await second.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            Assert.Equal(1, handler.Calls);
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_umapi_usage_log WHERE from_cache = 1"));

            var status = await client.GetAsync(HomeCatalogWidgets.UmapiProxyPath + "?action=status");
            var statusJson = await JsonDocument.ParseAsync(await status.Content.ReadAsStreamAsync());
            Assert.True(statusJson.RootElement.GetProperty("connected").GetBoolean());
            Assert.Equal(1, statusJson.RootElement.GetProperty("counts").GetProperty("manufacturers").GetInt32());
            Assert.True(statusJson.RootElement.GetProperty("cache_rows").GetInt32() >= 1);
            Assert.True(statusJson.RootElement.GetProperty("last_success").GetInt64() > 0);

            handler.Status = HttpStatusCode.PaymentRequired;
            handler.Body = """{"message":"Payment Required"}""";
            var refreshed = await client.GetAsync(HomeCatalogWidgets.UmapiProxyPath + "?action=manufacturers&section=passenger&refresh=1");
            Assert.Equal("[]", await refreshed.Content.ReadAsStringAsync());
            Assert.Equal(2, handler.Calls);
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_umapi_manufacturers"));
            var afterReject = await client.GetAsync(HomeCatalogWidgets.UmapiProxyPath + "?action=status");
            var rejected = await JsonDocument.ParseAsync(await afterReject.Content.ReadAsStreamAsync());
            Assert.False(rejected.RootElement.GetProperty("connected").GetBoolean());
            Assert.Equal("Payment Required", rejected.RootElement.GetProperty("message").GetString());
            Assert.Equal(1, rejected.RootElement.GetProperty("counts").GetProperty("manufacturers").GetInt32());
            var reused = await client.GetAsync(HomeCatalogWidgets.UmapiProxyPath + "?action=manufacturers&section=passenger");
            Assert.Contains("Toyota", await reused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            Assert.Equal(2, handler.Calls);

            handler.Status = HttpStatusCode.OK;
            handler.Body = """{"data":{"matchingVehicles":[{"manuName":"Honda"}],"matchingManufacturers":[{"manuName":"Honda"}]}}""";
            var vin = "1HGBH41JXMN109186";
            var decoded = await client.GetAsync(HomeCatalogWidgets.UmapiProxyPath + "?action=vin&vin=" + vin);
            Assert.Equal(HttpStatusCode.OK, decoded.StatusCode);
            Assert.Contains("Honda", await decoded.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT vehicle_count FROM epc_umapi_vin_cache WHERE vin = '" + vin + "'"));
            var decodedAgain = await client.GetAsync(HomeCatalogWidgets.UmapiProxyPath + "?action=vin&vin=" + vin);
            Assert.Contains("Honda", await decodedAgain.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            Assert.Equal(3, handler.Calls);
        }
        finally
        {
            await DropAsync(admin, database);
        }
    }

    [Fact]
    public async Task C110J_SevenHundredCrosses_AreReturnedForThePage()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var (database, admin, connectionString) = await CreateDatabaseAsync(password);
        try
        {
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_docpart_articles_analogs_list (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  manufacturer_article VARCHAR(64) NOT NULL,
                  article VARCHAR(64) NOT NULL,
                  manufacturer_analog VARCHAR(64) NOT NULL,
                  analog VARCHAR(64) NOT NULL,
                  article_search VARCHAR(64) NOT NULL,
                  analog_search VARCHAR(64) NOT NULL,
                  KEY article_search (article_search),
                  KEY analog_search (analog_search)
                )
                """);
            var sql = new StringBuilder("INSERT INTO shop_docpart_articles_analogs_list (manufacturer_article, article, manufacturer_analog, analog, article_search, analog_search) VALUES ");
            for (var i = 0; i < 720; i++)
            {
                if (i > 0)
                {
                    sql.Append(',');
                }

                var article = "C110X" + i.ToString(CultureInfo.InvariantCulture);
                sql.Append("('JS ASAKASHI','C110J','BRAND").Append(i.ToString(CultureInfo.InvariantCulture)).Append("','")
                    .Append(article).Append("','C110J','").Append(article).Append("')");
            }

            await ExecuteAsync(connectionString, sql.ToString());
            var reporter = new SurfaceDashboardSummaryReporter(new ShopConnections(connectionString));
            var watch = Stopwatch.StartNew();
            var result = await reporter.BuildStorefrontCrossSearchAsync("C110J", "JS ASAKASHI", LegacySurfaceDashboardSql.StorefrontCrossSearchMax, CancellationToken.None, includeCrossbase: false);
            watch.Stop();
            Assert.True(result.References.Count >= 700, result.Message + " count=" + result.References.Count.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(720, result.References.Count);
            Assert.Equal(720, result.References.Select(row => row.Article).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            await File.WriteAllTextAsync("/opt/cursor/artifacts/c110j-cross.log", "references=" + result.References.Count.ToString(CultureInfo.InvariantCulture) + " ms=" + watch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture) + "\n");
        }
        finally
        {
            await DropAsync(admin, database);
        }
    }

    [Fact]
    public async Task SupplierPriceUpload_TimesImportAndStorefrontLookup()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var (database, admin, connectionString) = await CreateDatabaseAsync(password);
        var root = Path.Combine(Path.GetTempPath(), "ecomae-price-speed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_docpart_prices (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  name VARCHAR(255) NOT NULL DEFAULT '',
                  load_mode INT NOT NULL DEFAULT 1,
                  strings_to_left INT NOT NULL DEFAULT 1,
                  manufacturer_col INT NOT NULL DEFAULT 1,
                  article_col INT NOT NULL DEFAULT 2,
                  name_col INT NOT NULL DEFAULT 3,
                  exist_col INT NOT NULL DEFAULT 4,
                  price_col INT NOT NULL DEFAULT 5,
                  time_to_exe_col INT NOT NULL DEFAULT 0,
                  storage_col INT NOT NULL DEFAULT 0,
                  min_order_col INT NOT NULL DEFAULT 0,
                  clean_before VARCHAR(8) NOT NULL DEFAULT '1',
                  file_name_substring VARCHAR(255) NOT NULL DEFAULT '',
                  file_name_substring_arch VARCHAR(255) NOT NULL DEFAULT '',
                  encoding VARCHAR(16) NOT NULL DEFAULT '',
                  `separator` VARCHAR(8) NOT NULL DEFAULT '',
                  ftp_host VARCHAR(255) NOT NULL DEFAULT '',
                  ftp_user VARCHAR(255) NOT NULL DEFAULT '',
                  ftp_password VARCHAR(255) NOT NULL DEFAULT '',
                  ftp_folder VARCHAR(255) NOT NULL DEFAULT '',
                  sender_email VARCHAR(255) NOT NULL DEFAULT '',
                  not_mark_seen_email_messages INT NOT NULL DEFAULT 0,
                  message_header_substring VARCHAR(255) NOT NULL DEFAULT '',
                  link VARCHAR(512) NOT NULL DEFAULT '',
                  last_updated BIGINT NOT NULL DEFAULT 0,
                  records_count INT NOT NULL DEFAULT 0,
                  h_time VARCHAR(16) NOT NULL DEFAULT '0'
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_docpart_prices_data (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  price_id INT NOT NULL,
                  manufacturer VARCHAR(255) NOT NULL DEFAULT '',
                  article VARCHAR(255) NOT NULL DEFAULT '',
                  article_search VARCHAR(64) NOT NULL DEFAULT '',
                  article_show VARCHAR(255) NOT NULL DEFAULT '',
                  name VARCHAR(255) NOT NULL DEFAULT '',
                  exist INT NOT NULL DEFAULT 0,
                  price DECIMAL(15,2) NOT NULL DEFAULT 0,
                  time_to_exe INT NOT NULL DEFAULT 0,
                  storage VARCHAR(255) NOT NULL DEFAULT '',
                  min_order INT NOT NULL DEFAULT 0,
                  KEY article_search (article_search)
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO shop_docpart_prices (id, name, load_mode, strings_to_left, manufacturer_col, article_col, name_col, exist_col, price_col, clean_before) VALUES (9, 'Speed', 1, 1, 1, 2, 3, 4, 5, '1')");
            const int rows = 2000;
            var csv = new StringBuilder("brand,article,name,qty,price\n");
            for (var i = 1; i <= rows; i++)
            {
                csv.Append("BOSCH,SPEED").Append(i.ToString("0000", CultureInfo.InvariantCulture)).Append(",Pad,2,12.50\n");
            }

            var imports = new CpPriceImportService(new WriteConnections(connectionString), new IdleRemote(), () => new Dictionary<string, string>(), Path.Combine(root, "files"), Path.Combine(root, "work"));
            var upload = Stopwatch.StartNew();
            var imported = await imports.ImportUploadAsync(new CpPriceImportRequest(9, "pc", 1, new CpPriceUpload("speed.csv", new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString())))));
            upload.Stop();
            Assert.True(imported.Succeeded, imported.Message);
            Assert.Equal(rows, imported.RowsImported);
            Assert.Equal(rows.ToString(CultureInfo.InvariantCulture), await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_docpart_prices_data WHERE price_id = 9"));

            var reporter = new SurfaceDashboardSummaryReporter(new ShopConnections(connectionString));
            var lookup = Stopwatch.StartNew();
            var found = await reporter.SearchStorefrontPartsAsync("SPEED0001", "BOSCH", 20);
            lookup.Stop();
            Assert.Equal("database", found.Source);
            Assert.Contains(found.Rows, row => row.Article.Contains("SPEED0001", StringComparison.OrdinalIgnoreCase));
            var perSecond = upload.Elapsed.TotalSeconds <= 0 ? rows : rows / upload.Elapsed.TotalSeconds;
            await File.WriteAllTextAsync(
                "/opt/cursor/artifacts/price-upload-speed.log",
                "rows=" + rows.ToString(CultureInfo.InvariantCulture)
                + " upload_ms=" + upload.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)
                + " rows_per_sec=" + perSecond.ToString("0", CultureInfo.InvariantCulture)
                + " storefront_lookup_ms=" + lookup.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)
                + " offers=" + found.Rows.Count.ToString(CultureInfo.InvariantCulture)
                + "\n");
        }
        finally
        {
            await DropAsync(admin, database);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static async Task<(string Database, string Admin, string ConnectionString)> CreateDatabaseAsync(string password)
    {
        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var connection = new MySqlConnection(admin);
        await connection.OpenAsync();
        await using var create = connection.CreateCommand();
        create.CommandText = "CREATE DATABASE `" + database + "`";
        await create.ExecuteNonQueryAsync();
        return (database, admin, connectionString);
    }

    private static async Task DropAsync(string admin, string database)
    {
        await using var connection = new MySqlConnection(admin);
        await connection.OpenAsync();
        await using var drop = connection.CreateCommand();
        drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
        await drop.ExecuteNonQueryAsync();
    }

    private static async Task<ProbeHost> StartAsync(string connectionString, CountingHandler handler)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture));
        builder.Services.AddSingleton<ITenantDbConnectionFactory>(new ShopConnections(connectionString));
        builder.Services.AddSingleton<IHttpClientFactory>(new CountingFactory(handler));
        var app = builder.Build();
        HomeCatalogWidgets.Map(app);
        await app.StartAsync();
        return new ProbeHost(app, new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture)));
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

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls;
        public HttpStatusCode Status = HttpStatusCode.OK;
        public string Body = """[{"MFA_ID":11,"MANUFACTURER":"Toyota","TYPE":"PC","POPULAR_PC":1}]""";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(Status)
            {
                Content = new StringContent(Body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class CountingFactory : IHttpClientFactory
    {
        private readonly CountingHandler _handler;

        public CountingFactory(CountingHandler handler) => _handler = handler;

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed class ShopConnections : ITenantDbConnectionFactory
    {
        private readonly string _connectionString;

        public ShopConnections(string connectionString) => _connectionString = connectionString;

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

    private sealed class WriteConnections : IErpWriteConnectionFactory
    {
        private readonly string _connectionString;

        public WriteConnections(string connectionString) => _connectionString = connectionString;

        public bool IsConfigured => true;

        public async Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }

    private sealed class IdleRemote : ICpPriceRemoteSources
    {
        public Task DownloadUrlAsync(Uri url, string destinationPath, ICollection<string> messages, CancellationToken cancellationToken)
            => throw new InvalidOperationException("URL download was called.");

        public Task<IReadOnlyList<string>> FetchFtpAsync(CpPriceListConfig list, Func<string, bool> wanted, string targetDirectory, ICollection<string> messages, CancellationToken cancellationToken)
            => throw new InvalidOperationException("FTP download was called.");

        public Task<IReadOnlyList<CpPriceMailMessage>> FetchMailAsync(CpPriceMailSettings settings, string sender, bool markSeen, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Email download was called.");
    }
}
