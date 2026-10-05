using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
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

public sealed class StorefrontPhpCpOrderAjaxTests
{
    [Fact]
    public async Task CpOrderAjax_OnThrowawayDatabase_ThenDropped()
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
            await using var host = await StartAsync(connectionString);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var staff = "admin_session=admin-token; admin_u_id=9";
            var customer = "session=user-token; u_id=7";

            var license = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.LicenseApiPath, null, string.Empty);
            Assert.Equal(HttpStatusCode.Forbidden, license.Status);
            Assert.Contains("License API disabled", license.Body, StringComparison.Ordinal);
            Assert.False(license.Json.RootElement.GetProperty("status").GetBoolean());

            var noSessions = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.CpAddOrderLogPath + "?order_id=41&text=hi&csrf_guard_key=admin-csrf", null, staff);
            Assert.Equal(HttpStatusCode.OK, noSessions.Status);
            Assert.Equal(StorefrontPhpAjax.AdminSessionsMissing, noSessions.Json.RootElement.GetProperty("message").GetString());
            Assert.DoesNotContain("doesn't exist", noSessions.Body, StringComparison.OrdinalIgnoreCase);

            await ExecuteAsync(connectionString, """
                CREATE TABLE sessions (
                  id INT NOT NULL PRIMARY KEY,
                  session VARCHAR(64) NOT NULL,
                  user_id INT NOT NULL,
                  type INT NOT NULL DEFAULT 0,
                  csrf_guard_key VARCHAR(64) NOT NULL DEFAULT ''
                )
                """);
            await ExecuteAsync(connectionString, """
                INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES
                (12, 'user-token', 7, 0, 'csrf-1'),
                (15, 'admin-token', 9, 1, 'admin-csrf')
                """);

            var missingKey = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpAddOrderLogPath, Form(("order_id", "41"), ("text", "hi")), staff);
            Assert.Equal("Error! CSRF 1", missingKey.Json.RootElement.GetProperty("message").GetString());
            var customerKey = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpAddOrderLogPath, Form(("order_id", "41"), ("text", "hi"), ("csrf_guard_key", "csrf-1")), customer);
            Assert.Equal("Error! CSRF 3.1", customerKey.Json.RootElement.GetProperty("message").GetString());
            var wrongKey = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpAddOrderLogPath, Form(("order_id", "41"), ("text", "hi"), ("csrf_guard_key", "csrf-1")), staff);
            Assert.Equal("Error! CSRF 4", wrongKey.Json.RootElement.GetProperty("message").GetString());

            var noLogs = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.CpAddOrderLogPath + "?order_id=41&text=" + Uri.EscapeDataString("a <b> & \"") + "&csrf_guard_key=admin-csrf", null, staff);
            Assert.Equal(StorefrontPhpAjax.OrderLogsMissing, noLogs.Json.RootElement.GetProperty("message").GetString());

            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_orders_logs (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  order_id INT NOT NULL,
                  time INT NOT NULL,
                  user_id INT NOT NULL,
                  is_manager INT NOT NULL,
                  text TEXT NOT NULL
                )
                """);
            var logged = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.CpAddOrderLogPath + "?order_id=41&text=" + Uri.EscapeDataString("a <b> & \"") + "&csrf_guard_key=admin-csrf", null, staff);
            Assert.True(logged.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("Ok", logged.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal(0, logged.Json.RootElement.GetProperty("code").GetInt32());
            Assert.Equal("a &lt;b&gt; &amp; &quot;", await ScalarAsync(connectionString, "SELECT text FROM shop_orders_logs WHERE order_id = 41"));
            Assert.Equal("9", await ScalarAsync(connectionString, "SELECT user_id FROM shop_orders_logs WHERE order_id = 41"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT is_manager FROM shop_orders_logs WHERE order_id = 41"));

            var noViewed = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpSetOrdersViewedPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"orders\":[4],\"viewed_flag\":1}")), staff);
            Assert.Equal(StorefrontPhpAjax.OrderViewedMissing, noViewed.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_orders_viewed (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  order_id INT NOT NULL,
                  user_id INT NOT NULL,
                  viewed_flag INT NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_viewed (order_id, user_id, viewed_flag) VALUES (4, 7, 0), (5, 7, 0)");
            var viewed = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpSetOrdersViewedPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"orders\":[4],\"viewed_flag\":1}")), staff);
            Assert.True(viewed.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("UPDATE `shop_orders_viewed` SET `viewed_flag` = ? WHERE `order_id` IN (?);", viewed.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT viewed_flag FROM shop_orders_viewed WHERE order_id = 4"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT viewed_flag FROM shop_orders_viewed WHERE order_id = 5"));

            var info = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpOrdersInfoPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"user_id\":7}")), staff);
            Assert.True(info.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("1", info.Json.RootElement.GetProperty("message").GetString());

            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_orders (
                  id INT NOT NULL PRIMARY KEY,
                  paid INT NOT NULL DEFAULT 0,
                  status INT NOT NULL DEFAULT 0
                )
                """);
            var noStatuses = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpPaidOrdersCountPath, Form(("csrf_guard_key", "admin-csrf")), staff);
            Assert.Equal(StorefrontPhpAjax.PaidStatusesMissing, noStatuses.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_orders_statuses_ref (
                  id INT NOT NULL PRIMARY KEY,
                  for_paid INT NOT NULL DEFAULT 0
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_statuses_ref (id, for_paid) VALUES (1, 1), (2, 0)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders (id, paid, status) VALUES (4, 0, 1), (5, 1, 1), (8, 0, 2)");
            var paidCount = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpPaidOrdersCountPath, Form(("csrf_guard_key", "admin-csrf")), staff);
            Assert.True(paidCount.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("2", paidCount.Json.RootElement.GetProperty("count").GetString());

            await ExecuteAsync(connectionString, "CREATE TABLE shop_orders_items (id INT NOT NULL PRIMARY KEY, order_id INT NOT NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_orders_items_details (id INT NOT NULL PRIMARY KEY, order_id INT NOT NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_orders_messages (id INT NOT NULL PRIMARY KEY, order_id INT NOT NULL, text VARCHAR(255) NOT NULL DEFAULT '')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_items (id, order_id) VALUES (1, 4), (2, 5)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_items_details (id, order_id) VALUES (1, 4)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_messages (id, order_id, text) VALUES (1, 4, 'note')");
            var blocked = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpDeleteOrdersPath, Form(("csrf_guard_key", "admin-csrf"), ("orders_list", "[4,5]")), staff);
            Assert.False(blocked.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("3481", blocked.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders WHERE id IN (4, 5)"));

            var deleted = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpDeleteOrdersPath, Form(("csrf_guard_key", "admin-csrf"), ("orders_list", "[4]")), staff);
            Assert.True(deleted.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.False(deleted.Json.RootElement.TryGetProperty("message", out _));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders WHERE id = 4"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders WHERE id = 5"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders_items WHERE order_id = 4"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders_items WHERE order_id = 5"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders_logs WHERE order_id = 4"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders_logs WHERE order_id = 41"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders_viewed WHERE order_id = 4"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders_viewed WHERE order_id = 5"));

            await ExecuteAsync(connectionString, "DROP TABLE shop_orders_items");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders (id, paid, status) VALUES (7, 0, 1)");
            var missingItems = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpDeleteOrdersPath, Form(("csrf_guard_key", "admin-csrf"), ("orders_list", "[7]")), staff);
            Assert.Equal(StorefrontPhpAjax.OrderItemsMissing, missingItems.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders WHERE id = 7"));

            var noUsers = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpSetUserCommentPath, Form(("csrf_guard_key", "admin-csrf"), ("user_id", "7"), ("comment", "desk")), staff);
            Assert.Equal(StorefrontPhpAjax.UserAccountsMissing, noUsers.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, """
                CREATE TABLE users (
                  user_id INT NOT NULL PRIMARY KEY,
                  email VARCHAR(190) NOT NULL DEFAULT '',
                  phone VARCHAR(64) NOT NULL DEFAULT ''
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO users (user_id, email, phone) VALUES (7, 'nora@local.test', '050'), (8, 'other@local.test', '')");
            var noComment = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpSetUserCommentPath, Form(("csrf_guard_key", "admin-csrf"), ("user_id", "7"), ("comment", "desk")), staff);
            Assert.Equal(StorefrontPhpAjax.UserCommentMissing, noComment.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "ALTER TABLE users ADD COLUMN comment VARCHAR(255) NOT NULL DEFAULT ''");
            var comment = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpSetUserCommentPath, Form(("csrf_guard_key", "admin-csrf"), ("user_id", "7"), ("comment", "desk & co")), staff);
            Assert.True(comment.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("Ok", comment.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("desk & co", await ScalarAsync(connectionString, "SELECT comment FROM users WHERE user_id = 7"));

            var noFields = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpUsersAutocompletePath, Form(("csrf_guard_key", "admin-csrf"), ("input_str", "nora")), staff);
            Assert.Equal(StorefrontPhpAjax.RegistrationFieldsMissing, noFields.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE reg_fields (id INT NOT NULL PRIMARY KEY, name VARCHAR(64) NOT NULL, to_users_table INT NOT NULL DEFAULT 0)");
            await ExecuteAsync(connectionString, "INSERT INTO reg_fields (id, name, to_users_table) VALUES (1, 'surname', 1)");
            var noProfiles = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpUsersAutocompletePath, Form(("csrf_guard_key", "admin-csrf"), ("input_str", "nora")), staff);
            Assert.Equal(StorefrontPhpAjax.UserProfilesMissing, noProfiles.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE users_profiles (id INT NOT NULL PRIMARY KEY, user_id INT NOT NULL, data_key VARCHAR(64) NOT NULL, data_value VARCHAR(255) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO users_profiles (id, user_id, data_key, data_value) VALUES (1, 7, 'surname', 'Nora')");
            var found = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpUsersAutocompletePath, Form(("csrf_guard_key", "admin-csrf"), ("input_str", "nora")), staff);
            Assert.True(found.Json.RootElement.GetProperty("status").GetBoolean());
            var vars = found.Json.RootElement.GetProperty("vars");
            Assert.Equal(2, vars.GetArrayLength());
            Assert.Equal(0, vars[0].GetProperty("user_id").GetInt32());
            Assert.Equal("ID 0, 3233", vars[0].GetProperty("user_info").GetString());
            Assert.Equal(7, vars[1].GetProperty("user_id").GetInt32());
            Assert.Equal("ID 7, E-mail: nora@local.test, Телефон: 050, Nora", vars[1].GetProperty("user_info").GetString());

            var guestReturns = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpReturnsInfoPath, null, customer);
            Assert.Equal(0, guestReturns.Json.RootElement.GetProperty("status").GetInt32());
            Assert.Equal(0, guestReturns.Json.RootElement.GetProperty("message").GetInt32());
            var noReturns = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpReturnsInfoPath, null, staff);
            Assert.Equal(StorefrontPhpAjax.ReturnsHeaderMissing, noReturns.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_orders_returns (
                  id INT NOT NULL PRIMARY KEY,
                  status_id INT NOT NULL,
                  return_complete INT NULL
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_returns (id, status_id, return_complete) VALUES (1, 1, 0), (2, 3, 0), (3, 1, 1)");
            var openReturns = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpReturnsInfoPath, null, staff);
            Assert.Equal(1, openReturns.Json.RootElement.GetProperty("status").GetInt32());
            Assert.Equal(2, openReturns.Json.RootElement.GetProperty("message").GetInt32());
            await ExecuteAsync(connectionString, "CREATE TABLE shop_orders_returns_statuses (id INT NOT NULL PRIMARY KEY, caption VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_returns_statuses (id, caption) VALUES (3, '3798')");
            var filtered = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpReturnsInfoPath, null, staff);
            Assert.Equal(1, filtered.Json.RootElement.GetProperty("message").GetInt32());

            var noAlias = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpProductAliasPath, Form(("csrf_guard_key", "admin-csrf"), ("product_id", "1"), ("category_id", "3"), ("alias", "pads")), staff);
            Assert.Equal(StorefrontPhpAjax.CatalogueProductsMissing, noAlias.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE shop_catalogue_products (id INT NOT NULL PRIMARY KEY, category_id INT NOT NULL, alias VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_catalogue_products (id, category_id, alias) VALUES (1, 3, 'pads')");
            var taken = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpProductAliasPath, Form(("csrf_guard_key", "admin-csrf"), ("product_id", "2"), ("category_id", "3"), ("alias", "pads")), staff);
            Assert.Equal("false", taken.Body);
            var free = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpProductAliasPath, Form(("csrf_guard_key", "admin-csrf"), ("product_id", "1"), ("category_id", "3"), ("alias", "pads")), staff);
            Assert.Equal("true", free.Body);

            var noVin = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpVinViewedPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"vins\":\"[3]\",\"viewed_flag\":1}")), staff);
            Assert.Equal(StorefrontPhpAjax.VinRequestsMissing, noVin.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE users_vin (id INT NOT NULL PRIMARY KEY, viewed INT NOT NULL DEFAULT 0)");
            await ExecuteAsync(connectionString, "INSERT INTO users_vin (id, viewed) VALUES (3, 0), (4, 0)");
            var badVin = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpVinViewedPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"vins\":\"[3]; DROP TABLE users_vin\",\"viewed_flag\":1}")), staff);
            Assert.Equal(StorefrontPhpAjax.SqlErrorLower, badVin.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'users_vin'"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT viewed FROM users_vin WHERE id = 3"));
            var vin = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpVinViewedPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"vins\":\"[3]\",\"viewed_flag\":1}")), staff);
            Assert.True(vin.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(string.Empty, vin.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT viewed FROM users_vin WHERE id = 3"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT viewed FROM users_vin WHERE id = 4"));

            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));
            Assert.Equal("2", await ScalarAsync(admin, "SELECT COUNT(*) FROM `docpart`.`users`"));
            Assert.Equal("2", await ScalarAsync(admin, "SELECT COUNT(*) FROM `ecomae`.`users`"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }

        Assert.Equal("0", await ScalarAsync(admin, "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = '" + database + "'"));
    }

    private static Dictionary<string, string> Form(params (string Key, string Value)[] fields)
    {
        var form = new Dictionary<string, string>();
        foreach (var field in fields)
        {
            form[field.Key] = field.Value;
        }

        return form;
    }

    private static async Task<Sent> SendAsync(HttpClient client, HttpMethod method, string path, Dictionary<string, string>? form, string cookie)
    {
        using var request = new HttpRequestMessage(method, path);
        if (cookie.Length > 0)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        if (form is not null)
        {
            request.Content = new FormUrlEncodedContent(form);
        }

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        JsonDocument json = null!;
        if (body.Length > 0 && body[0] is '{' or '[')
        {
            json = JsonDocument.Parse(body);
        }

        return new Sent(response.StatusCode, body, json);
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

    private sealed record Sent(HttpStatusCode Status, string Body, JsonDocument Json);

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
