using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using EcomAE.Platform.Configuration;
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

public sealed class StorefrontReturnsLoadTests
{
    [Fact]
    public async Task ReturnRequest_SplitsPhotosNotifiesAndMarksLinesLikePhp_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        await using (var adminConnection = new MySqlConnection(admin))
        {
            await adminConnection.OpenAsync();
            await using var create = adminConnection.CreateCommand();
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";";
        Assert.DoesNotContain("Database=docpart", cs, StringComparison.OrdinalIgnoreCase);
        var docRoot = Path.Combine(Path.GetTempPath(), "ecomae-returns-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(docRoot);
        await File.WriteAllTextAsync(Path.Combine(docRoot, "config.php"), """
            <?php
            class DP_Config {
            public $tech_key = 'local-tech';
            }
            """);
        try
        {
            foreach (var sql in Setup)
            {
                await ExecuteAsync(cs, sql);
            }

            var notify = new RecordingDispatcher();
            await using var host = await StartAsync(cs, docRoot, notify);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };

            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 };
            var created = await PostAsync(
                client,
                [("items[0][item_id]", "90"), ("items[0][reason_id]", "2"), ("items[0][comment]", "Wrong <size>"), ("items[0][count]", "2"),
                 ("items[1][item_id]", "91"), ("items[1][reason_id]", "2"), ("items[1][comment]", "Broken"), ("items[1][count]", "1")],
                [("images[90][0]", "image/png", png), ("images[90][1]", "image/jpeg", png), ("images[91][0]", "image/png", png)]);
            Assert.Equal("{\"status\":true,\"data\":\"success\"}", created);

            Assert.Equal("1|7|12.50", await ScalarAsync(cs, "SELECT CONCAT_WS('|', status_id, user_id, `sum`) FROM shop_orders_returns"));
            Assert.Equal(
                "100|2|Wrong &lt;size&gt;|2,91|1|Broken|2",
                await ScalarAsync(cs, "SELECT GROUP_CONCAT(CONCAT_WS('|', item_id, count_need, comment, reason_id) ORDER BY id) FROM shop_orders_returns_items"));
            Assert.Equal(
                "90|3|10,91|1|20,99|1|10,100|2|20",
                await ScalarAsync(cs, "SELECT GROUP_CONCAT(CONCAT_WS('|', id, count_need, status) ORDER BY id) FROM shop_orders_items"));
            Assert.Equal("Oil filter|15.00|40", await ScalarAsync(cs, "SELECT CONCAT_WS('|', t2_name, price, order_id) FROM shop_orders_items WHERE id = 100"));
            Assert.Equal(
                "90|3,100|2",
                await ScalarAsync(cs, "SELECT GROUP_CONCAT(CONCAT_WS('|', order_item_id, count_reserved) ORDER BY order_item_id) FROM shop_orders_items_details"));

            Assert.Equal("1,1,2", await ScalarAsync(cs, "SELECT GROUP_CONCAT(return_item_id ORDER BY id) FROM shop_orders_returns_items_images"));
            var stored = await ScalarAsync(cs, "SELECT image FROM shop_orders_returns_items_images ORDER BY id LIMIT 1");
            Assert.StartsWith(Path.Combine(docRoot, "content", "files", "returns_images"), stored, StringComparison.Ordinal);
            Assert.Equal(png, await File.ReadAllBytesAsync(stored));

            Assert.Equal(["return_new_manager", "return_new_customer"], notify.Sent.Select(s => s.Name));
            Assert.Equal([3, 4], notify.Sent[0].Persons.Select(p => p.UserId));
            Assert.Equal(7, Assert.Single(notify.Sent[1].Persons).UserId);
            Assert.Equal("1", notify.Sent[1].Vars["return_id"]);

            Assert.Equal(
                [
                    "Lines [100, 91] sent to return|1",
                    "Cut ID 100 from ID 90 count 2|1",
                    "Split ID 100 from ID 90. Was 5 now 3|1",
                ],
                (await ScalarAsync(cs, "SELECT GROUP_CONCAT(CONCAT_WS('|', text, is_robot) ORDER BY id SEPARATOR '~') FROM shop_orders_logs WHERE order_id = 40")).Split('~'));

            var duplicate = await PostAsync(client, [("items[0][item_id]", "91"), ("items[0][reason_id]", "2"), ("items[0][comment]", "x"), ("items[0][count]", "1")], []);
            Assert.Equal("{\"status\":false,\"error_message\":\"Already in a return\"}", duplicate);

            var gif = await PostAsync(client, [("items[0][item_id]", "99"), ("items[0][reason_id]", "2"), ("items[0][comment]", "x"), ("items[0][count]", "1")], [("images[99][0]", "image/gif", png)]);
            Assert.Equal("{\"status\":false,\"error_message\":\"Wrong image type\"}", gif);
            var big = await PostAsync(client, [("items[0][item_id]", "99"), ("items[0][reason_id]", "2"), ("items[0][comment]", "x"), ("items[0][count]", "1")], [("images[99][0]", "image/png", new byte[5242881])]);
            Assert.Equal("{\"status\":false,\"error_message\":\"Image too big\"}", big);
            Assert.Equal("1", await ScalarAsync(cs, "SELECT COUNT(*) FROM shop_orders_returns"));
            Assert.Equal("3", await ScalarAsync(cs, "SELECT COUNT(*) FROM shop_orders_returns_items_images"));
            Assert.Equal(3, Directory.GetFiles(Path.Combine(docRoot, "content", "files", "returns_images")).Length);
            Assert.Equal("10", await ScalarAsync(cs, "SELECT status FROM shop_orders_items WHERE id = 99"));

            var forbidden = await PostAsync(client, [("items[0][item_id]", "99"), ("items[0][count]", "1")], [], "wrong");
            Assert.Equal("{\"status\":false,\"error_message\":\"Forbidden\"}", forbidden);
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            Directory.Delete(docRoot, true);
            await using var adminConnection = new MySqlConnection(admin);
            await adminConnection.OpenAsync();
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static readonly string[] Setup =
    [
        "CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL DEFAULT 0, csrf_guard_key VARCHAR(64) NOT NULL DEFAULT '', data TEXT NULL)",
        "INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES (12, 'user-token', 7, 0, 'csrf-1')",
        "CREATE TABLE shop_orders_items (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, order_id INT NOT NULL, product_type INT NOT NULL DEFAULT 1, status INT NOT NULL DEFAULT 0, price DECIMAL(10,2) NOT NULL DEFAULT 0, count_need INT NOT NULL DEFAULT 0, t2_name VARCHAR(255) NOT NULL DEFAULT '')",
        "INSERT INTO shop_orders_items (id, order_id, product_type, status, price, count_need, t2_name) VALUES (90, 40, 1, 10, 15, 5, 'Oil filter'), (91, 40, 2, 10, 2.5, 1, 'Plug'), (99, 40, 2, 10, 1, 1, 'Cap')",
        "CREATE TABLE shop_orders_items_details (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, order_id INT NOT NULL, order_item_id INT NOT NULL, office_id INT NOT NULL DEFAULT 0, storage_id INT NOT NULL DEFAULT 0, storage_record_id INT NOT NULL DEFAULT 0, count_reserved INT NOT NULL DEFAULT 0, count_issued INT NOT NULL DEFAULT 0, count_canceled INT NOT NULL DEFAULT 0, price_purchase DECIMAL(10,2) NOT NULL DEFAULT 0)",
        "INSERT INTO shop_orders_items_details (order_id, order_item_id, office_id, storage_id, storage_record_id, count_reserved) VALUES (40, 90, 1, 1, 50, 5)",
        "CREATE TABLE shop_orders_items_statuses_ref (id INT NOT NULL PRIMARY KEY, for_return TINYINT NOT NULL DEFAULT 0)",
        "INSERT INTO shop_orders_items_statuses_ref (id, for_return) VALUES (10, 0), (20, 1)",
        "CREATE TABLE shop_orders_returns_statuses (id INT NOT NULL PRIMARY KEY, caption VARCHAR(64) NOT NULL)",
        "INSERT INTO shop_orders_returns_statuses (id, caption) VALUES (1, '3806'), (2, 'done')",
        "CREATE TABLE shop_orders_returns (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, status_id INT NULL, user_id INT NULL, `sum` DECIMAL(12,2) NULL)",
        "CREATE TABLE shop_orders_returns_items (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, comment VARCHAR(255) NULL, reason_id INT NULL, return_id INT NULL, item_id INT NULL, count_need VARCHAR(32) NULL)",
        "CREATE TABLE shop_orders_returns_items_images (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, return_item_id INT NOT NULL, image VARCHAR(512) NOT NULL)",
        "CREATE TABLE shop_orders_logs (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, order_id INT NOT NULL, time INT NOT NULL, user_id INT NOT NULL DEFAULT 0, is_manager TINYINT NOT NULL DEFAULT 0, is_robot TINYINT NOT NULL DEFAULT 0, text TEXT NOT NULL)",
        "CREATE TABLE shop_offices (id INT NOT NULL PRIMARY KEY, users TEXT NULL)",
        "INSERT INTO shop_offices (id, users) VALUES (1, '[\"3\",4]')",
        "CREATE TABLE lang_text_strings_translation (str_key VARCHAR(64) NOT NULL, lang_code VARCHAR(8) NOT NULL, value TEXT NOT NULL)",
        """
        INSERT INTO lang_text_strings_translation (str_key, lang_code, value) VALUES
        ('4571','en','Already in a return'), ('4575','en','Wrong image type'), ('4576','en','Image too big'),
        ('5689','en','Lines'), ('5690','en','sent to return'), ('5686','en','Cut'), ('5687','en','from'), ('5688','en','count'),
        ('5636','en','Split'), ('5637','en','from'), ('5638','en','Was'), ('5639','en','now')
        """,
    ];

    private static async Task<string> PostAsync(
        HttpClient client,
        IReadOnlyList<(string Name, string Value)> fields,
        IReadOnlyList<(string Name, string Type, byte[] Bytes)> files,
        string techKey = "local-tech")
    {
        using var content = new MultipartFormDataContent();
        foreach (var (name, value) in new (string, string)[] { ("csrf_guard_key", "csrf-1"), ("tech_key", techKey), ("user_id", "7"), ("office_id", "1"), ("total_sum", "12.50") }.Concat(fields))
        {
            content.Add(new StringContent(value), name);
        }

        foreach (var (name, type, bytes) in files)
        {
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue(type);
            content.Add(file, name, "photo");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, StorefrontPhpAjax.ReturnsLoadPath) { Content = content };
        request.Headers.TryAddWithoutValidation("Cookie", "session=user-token; u_id=7");
        using var response = await client.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task ExecuteAsync(string cs, string sql)
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
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static async Task<ProbeHost> StartAsync(string cs, string docRoot, RecordingDispatcher notify)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture));
        builder.Services.AddSingleton<ITenantDbConnectionFactory>(new FixedConnections(cs));
        builder.Services.AddSingleton<IStorefrontPriceAccess>(new GuestPrices());
        builder.Services.AddSingleton(ReporterStub.Create());
        builder.Services.AddSingleton<IStorefrontNotifyDispatcher>(notify);
        builder.Services.Configure<PhpReferenceOptions>(options => options.PhpDocRoot = docRoot);
        var app = builder.Build();
        StorefrontPhpAjaxEndpoints.Map(app);
        await app.StartAsync();
        return new ProbeHost(app, new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/"));
    }

    private sealed class RecordingDispatcher : IStorefrontNotifyDispatcher
    {
        public List<(string Name, IReadOnlyDictionary<string, string> Vars, IReadOnlyList<StorefrontNotifyPerson> Persons)> Sent { get; } = [];

        public Task<StorefrontNotifyAnswer> SendAsync(
            DbConnection connection,
            string name,
            IReadOnlyDictionary<string, string> vars,
            IReadOnlyList<StorefrontNotifyPerson> persons,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, string>? statusRef = null)
        {
            Sent.Add((name, vars, persons));
            return Task.FromResult(new StorefrontNotifyAnswer(true, string.Empty, []));
        }

        public Task<StorefrontNotifyOutcome> SendDirectEmailAsync(DbConnection connection, string name, IReadOnlyDictionary<string, string> vars, string email, CancellationToken cancellationToken = default)
            => Task.FromResult(new StorefrontNotifyOutcome(true, true, string.Empty));
    }

    private sealed class ProbeHost(WebApplication app, Uri baseAddress) : IAsyncDisposable
    {
        public Uri BaseAddress { get; } = baseAddress;

        public async ValueTask DisposeAsync()
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private sealed class GuestPrices : IStorefrontPriceAccess
    {
        public ValueTask<StorefrontPriceAccessResult> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new StorefrontPriceAccessResult(StorefrontPriceAccessState.Guest, false, "**", string.Empty, string.Empty));

        public IReadOnlyList<StorefrontPartOfferDigest> RedactOffers(IReadOnlyList<StorefrontPartOfferDigest> offers) => offers;
    }

    private sealed class FixedConnections(string cs) : ITenantDbConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default) => OpenAsync();

        public Task<DbConnection> OpenAsync(string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default) => OpenAsync();

        public Task<DbConnection> OpenForTenantAsync(TenantContext? tenant, CancellationToken cancellationToken = default) => OpenAsync();

        public Task<DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default) => OpenAsync();

        private async Task<DbConnection> OpenAsync()
        {
            var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            return connection;
        }
    }

    private class ReporterStub : System.Reflection.DispatchProxy
    {
        public static ISurfaceDashboardSummaryReporter Create()
            => Create<ISurfaceDashboardSummaryReporter, ReporterStub>();

        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException(targetMethod?.Name ?? "dispatch");
    }
}
