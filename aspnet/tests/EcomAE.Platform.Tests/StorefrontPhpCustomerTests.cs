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

public sealed class StorefrontPhpCustomerTests
{
    [Fact]
    public async Task CustomerPhpRoutes_OnThrowawayDatabase_ThenDropped()
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
            var user = "session=user-token";
            var guest = "session=guest-token";
            var other = "session=other-token";

            var retired = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.GuestOrderLookupPath, null, string.Empty);
            Assert.Equal(string.Empty, retired.Body);
            Assert.Contains("text/html", retired.ContentType, StringComparison.OrdinalIgnoreCase);

            var cityBlocked = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.SetMyCityPath, userForm("user-key", ("geo_id", "12")), user);
            Assert.Equal("Sessions are not in this database.", cityBlocked.Json.RootElement.GetProperty("message").GetString());
            Assert.DoesNotContain("my_city=", cityBlocked.SetCookie, StringComparison.Ordinal);

            await ExecuteAsync(connectionString, """
                CREATE TABLE sessions (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  session VARCHAR(64) NOT NULL,
                  user_id INT NOT NULL,
                  csrf_guard_key VARCHAR(64) NOT NULL,
                  type INT NOT NULL DEFAULT 0
                )
                """);
            await ExecuteAsync(connectionString, """
                INSERT INTO sessions (id, session, user_id, csrf_guard_key, type) VALUES
                (12, 'user-token', 7, 'user-key', 0),
                (13, 'other-token', 8, 'other-key', 0),
                (14, 'guest-token', 0, 'guest-key', 0),
                (15, 'admin-token', 9, 'admin-key', 1)
                """);

            var city = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.SetMyCityPath, userForm("user-key", ("geo_id", "12")), user);
            Assert.Equal("1", city.Body);
            Assert.Contains("my_city=12", city.SetCookie, StringComparison.Ordinal);
            Assert.Contains("path=/", city.SetCookie, StringComparison.OrdinalIgnoreCase);

            var missingOption = await PostJsonAsync(client, StorefrontPhpAjax.SetUserOptionPath, userForm("user-key", ("key", "selected_manufacturer"), ("value", "Bosch")), user);
            Assert.False(missingOption.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(StorefrontPhpAjax.UserOptionsMissing, missingOption.RootElement.GetProperty("message").GetString());
            Assert.DoesNotContain("doesn't exist", missingOption.RootElement.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);

            await ExecuteAsync(connectionString, """
                CREATE TABLE users_options (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  user_id INT NOT NULL,
                  session_id INT NOT NULL,
                  data_key VARCHAR(64) NOT NULL,
                  data_value TEXT NOT NULL
                )
                """);
            var missingCategory = await PostJsonAsync(client, StorefrontPhpAjax.SetUserOptionPath, userForm("user-key", ("key", "propucts_request_3"), ("value", "pads")), user);
            Assert.Equal(StorefrontPhpAjax.CatalogueCategoriesMissing, missingCategory.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM users_options"));

            var saved = await PostJsonAsync(client, StorefrontPhpAjax.SetUserOptionPath, userForm("user-key", ("key", "selected_manufacturer"), ("value", "Bosch & Co")), user);
            Assert.True(saved.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("Ok", saved.RootElement.GetProperty("message").GetString());
            Assert.Equal("Bosch & Co", await ScalarAsync(connectionString, "SELECT data_value FROM users_options WHERE data_key='selected_manufacturer'"));
            Assert.Equal("7", await ScalarAsync(connectionString, "SELECT user_id FROM users_options WHERE data_key='selected_manufacturer'"));
            Assert.Equal("12", await ScalarAsync(connectionString, "SELECT session_id FROM users_options WHERE data_key='selected_manufacturer'"));

            var forbidden = await PostJsonAsync(client, StorefrontPhpAjax.SetUserOptionPath, userForm("user-key", ("key", "nope"), ("value", "x")), user);
            Assert.Equal("Forbidden", forbidden.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM users_options"));

            await ExecuteAsync(connectionString, "CREATE TABLE shop_catalogue_categories (id INT NOT NULL PRIMARY KEY)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_catalogue_categories (id) VALUES (3)");
            var category = await PostJsonAsync(client, StorefrontPhpAjax.SetUserOptionPath, userForm("guest-key", ("key", "propucts_request_3"), ("value", "pads")), guest);
            Assert.True(category.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT user_id FROM users_options WHERE data_key='propucts_request_3'"));
            Assert.Equal("14", await ScalarAsync(connectionString, "SELECT session_id FROM users_options WHERE data_key='propucts_request_3'"));
            var unknownCategory = await PostJsonAsync(client, StorefrontPhpAjax.SetUserOptionPath, userForm("user-key", ("key", "propucts_request_9"), ("value", "x")), user);
            Assert.Equal("Forbidden", unknownCategory.RootElement.GetProperty("message").GetString());
            var updated = await PostJsonAsync(client, StorefrontPhpAjax.SetUserOptionPath, userForm("user-key", ("key", "selected_manufacturer"), ("value", "MANN")), user);
            Assert.True(updated.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("MANN", await ScalarAsync(connectionString, "SELECT data_value FROM users_options WHERE data_key='selected_manufacturer'"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM users_options WHERE data_key='selected_manufacturer'"));

            var missingEval = await PostJsonAsync(
                client,
                StorefrontPhpAjax.AddEvaluationPath,
                userForm("user-key", ("evaluation_object", "{\"product_id\":9,\"mark\":5,\"text_plus\":\"good\",\"text_minus\":\"\",\"text\":\"a <b> & \\\"\",\"hide_user_data\":0}")),
                user);
            Assert.False(missingEval.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(StorefrontPhpAjax.ProductEvaluationsMissing, missingEval.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_products_evaluations'"));

            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_products_evaluations (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  product_id INT NOT NULL,
                  mark INT NOT NULL,
                  text_plus TEXT NOT NULL,
                  text_minus TEXT NOT NULL,
                  text TEXT NOT NULL,
                  user_id INT NOT NULL,
                  time INT NOT NULL,
                  hide_user_data INT NOT NULL
                )
                """);
            var guestEval = await PostJsonAsync(
                client,
                StorefrontPhpAjax.AddEvaluationPath,
                userForm("guest-key", ("evaluation_object", "{\"product_id\":9,\"mark\":5,\"text\":\"no\"}")),
                guest);
            Assert.Equal("4088", guestEval.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_products_evaluations"));

            var added = await PostJsonAsync(
                client,
                StorefrontPhpAjax.AddEvaluationPath,
                userForm("user-key", ("evaluation_object", "{\"product_id\":9,\"mark\":5,\"text_plus\":\"good\",\"text_minus\":\"slow\",\"text\":\"a <b> & \\\"\",\"hide_user_data\":0}")),
                user);
            Assert.True(added.RootElement.GetProperty("status").GetBoolean());
            Assert.False(added.RootElement.TryGetProperty("message", out _));
            Assert.Equal("a &lt;b&gt; &amp; &quot;", await ScalarAsync(connectionString, "SELECT text FROM shop_products_evaluations WHERE user_id=7"));
            Assert.Equal("good", await ScalarAsync(connectionString, "SELECT text_plus FROM shop_products_evaluations WHERE user_id=7"));
            var again = await PostJsonAsync(
                client,
                StorefrontPhpAjax.AddEvaluationPath,
                userForm("user-key", ("evaluation_object", "{\"product_id\":9,\"mark\":1,\"text\":\"again\"}")),
                user);
            Assert.Equal("4089", again.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_products_evaluations"));

            var missingProfiles = await PostJsonAsync(
                client,
                StorefrontPhpAjax.ProductEvaluationsPath,
                userForm("user-key", ("evaluation_query", "{\"product_id\":9,\"mark\":0,\"asc_desc\":\"desc\",\"page\":0}")),
                user);
            Assert.Equal("User profiles are not in this database.", missingProfiles.RootElement.GetProperty("message").GetString());

            await ExecuteAsync(connectionString, """
                CREATE TABLE users_profiles (
                  user_id INT NOT NULL,
                  data_key VARCHAR(64) NOT NULL,
                  data_value VARCHAR(255) NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO users_profiles (user_id, data_key, data_value) VALUES (7, 'name', 'Nora'), (8, 'name', 'Omar')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_products_evaluations (product_id, mark, text_plus, text_minus, text, user_id, time, hide_user_data) VALUES (9, 4, '', '', 'hidden', 8, 1700000000, 1)");
            var listed = await PostJsonAsync(
                client,
                StorefrontPhpAjax.ProductEvaluationsPath,
                userForm("user-key", ("evaluation_query", "{\"product_id\":9,\"mark\":0,\"asc_desc\":\"asc\",\"page\":0}")),
                user);
            Assert.True(listed.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(1, listed.RootElement.GetProperty("pages_total").GetInt32());
            Assert.Equal("4091", listed.RootElement.GetProperty("evaluations")[1].GetProperty("user_name").GetString());
            Assert.Equal("4", listed.RootElement.GetProperty("evaluations")[1].GetProperty("mark").GetString());
            var shown = listed.RootElement.GetProperty("evaluations")[0];
            Assert.Equal("Nora", shown.GetProperty("user_name").GetString());
            Assert.Equal("5", shown.GetProperty("mark").GetString());
            Assert.Equal("a &lt;b&gt; &amp; &quot;", shown.GetProperty("text").GetString());
            var storedTime = long.Parse(await ScalarAsync(connectionString, "SELECT time FROM shop_products_evaluations WHERE user_id=7"), CultureInfo.InvariantCulture);
            var local = DateTimeOffset.FromUnixTimeSeconds(storedTime).ToLocalTime();
            var expectedTime = local.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) + " " + local.Hour.ToString(CultureInfo.InvariantCulture) + ":" + local.ToString("mm", CultureInfo.InvariantCulture);
            Assert.Equal(expectedTime, shown.GetProperty("time").GetString());

            var badSort = await SendAsync(
                client,
                HttpMethod.Post,
                StorefrontPhpAjax.ProductEvaluationsPath,
                userForm("user-key", ("evaluation_query", "{\"product_id\":9,\"mark\":0,\"asc_desc\":\"sideways\",\"page\":0}")),
                user);
            Assert.Equal(string.Empty, badSort.Body);

            var mark = await PostJsonAsync(client, StorefrontPhpAjax.ProductMarkPath, userForm("user-key", ("product_id", "9")), user);
            Assert.Equal("2", mark.RootElement.GetProperty("marks_count").GetString());
            Assert.Equal("5", mark.RootElement.GetProperty("general_mark").GetString());
            Assert.Equal("0", mark.RootElement.GetProperty("mark_1_count").GetString());
            Assert.Equal("1", mark.RootElement.GetProperty("mark_4_count").GetString());
            Assert.Equal("1", mark.RootElement.GetProperty("mark_5_count").GetString());
            var emptyMark = await PostJsonAsync(client, StorefrontPhpAjax.ProductMarkPath, userForm("user-key", ("product_id", "99")), user);
            Assert.Equal("0", emptyMark.RootElement.GetProperty("marks_count").GetString());
            Assert.Equal(JsonValueKind.Null, emptyMark.RootElement.GetProperty("general_mark").ValueKind);

            var missingVin = await GetJsonAsync(client, StorefrontPhpAjax.VinMessagesPath + "?vin_id=4&csrf_guard_key=user-key", user);
            Assert.Equal(StorefrontPhpAjax.VinRequestsMissing, missingVin.RootElement.GetProperty("message").GetString());

            await ExecuteAsync(connectionString, """
                CREATE TABLE users_vin (
                  id INT NOT NULL PRIMARY KEY,
                  user_id INT NOT NULL,
                  viewed INT NOT NULL,
                  viewed_customer INT NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO users_vin (id, user_id, viewed, viewed_customer) VALUES (4, 7, 1, 1)");
            var missingMessages = await GetJsonAsync(client, StorefrontPhpAjax.VinMessagesPath + "?vin_id=4&csrf_guard_key=user-key", user);
            Assert.Equal(StorefrontPhpAjax.VinMessagesMissing, missingMessages.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'users_vin_messages'"));

            await ExecuteAsync(connectionString, """
                CREATE TABLE users_vin_messages (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  vin_id INT NOT NULL,
                  is_customer INT NOT NULL,
                  text TEXT NOT NULL,
                  time INT NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO users_vin_messages (vin_id, is_customer, text, time) VALUES (4, 1, 'hello', 1700000000)");
            var alien = await GetJsonAsync(client, StorefrontPhpAjax.VinMessagesPath + "?vin_id=4&csrf_guard_key=other-key", other);
            Assert.Equal("Forbidden", alien.RootElement.GetProperty("message").GetString());
            Assert.Equal(501, alien.RootElement.GetProperty("code").GetInt32());
            var guestVin = await GetJsonAsync(client, StorefrontPhpAjax.VinMessagesPath + "?vin_id=4&csrf_guard_key=guest-key", guest);
            Assert.Equal(501, guestVin.RootElement.GetProperty("code").GetInt32());
            var managerBlocked = await GetJsonAsync(client, StorefrontPhpAjax.VinMessagesPath + "?vin_id=4&manager=1&csrf_guard_key=user-key", user);
            Assert.Equal(501, managerBlocked.RootElement.GetProperty("code").GetInt32());

            var messages = await GetJsonAsync(client, StorefrontPhpAjax.VinMessagesPath + "?vin_id=4&csrf_guard_key=user-key", user);
            Assert.Equal(JsonValueKind.Array, messages.RootElement.ValueKind);
            Assert.Equal("hello", messages.RootElement[0].GetProperty("text").GetString());
            Assert.True(messages.RootElement[0].GetProperty("is_customer").GetBoolean());
            var stamp = DateTimeOffset.FromUnixTimeSeconds(1700000000).ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);
            Assert.Equal(stamp, messages.RootElement[0].GetProperty("time").GetString());

            var sent = await GetJsonAsync(client, StorefrontPhpAjax.VinSendMessagePath + "?vin_id=4&text=" + Uri.EscapeDataString("a <b> & \"") + "&csrf_guard_key=user-key", user);
            Assert.True(sent.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("a &lt;b&gt; &amp; &quot;", await ScalarAsync(connectionString, "SELECT text FROM users_vin_messages WHERE vin_id=4 ORDER BY id DESC LIMIT 1"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT is_customer FROM users_vin_messages WHERE vin_id=4 ORDER BY id DESC LIMIT 1"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT viewed FROM users_vin WHERE id=4"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT viewed_customer FROM users_vin WHERE id=4"));

            var staff = await GetJsonAsync(
                client,
                StorefrontPhpAjax.VinSendMessagePath + "?vin_id=4&manager=1&text=" + Uri.EscapeDataString("seen") + "&csrf_guard_key=user-key",
                user + "; admin_session=admin-token; admin_u_id=9");
            Assert.True(staff.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT is_customer FROM users_vin_messages WHERE text='seen'"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT viewed_customer FROM users_vin WHERE id=4"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static Dictionary<string, string> userForm(string csrf, params (string Key, string Value)[] fields)
    {
        var form = new Dictionary<string, string> { ["csrf_guard_key"] = csrf };
        foreach (var field in fields)
        {
            form[field.Key] = field.Value;
        }

        return form;
    }

    private static async Task<JsonDocument> PostJsonAsync(HttpClient client, string path, Dictionary<string, string> form, string cookie)
    {
        var sent = await SendAsync(client, HttpMethod.Post, path, form, cookie);
        return sent.Json;
    }

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string path, string cookie)
    {
        var sent = await SendAsync(client, HttpMethod.Get, path, null, cookie);
        return sent.Json;
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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookieHeader = response.Headers.TryGetValues("Set-Cookie", out var values) ? string.Join(";", values) : string.Empty;
        JsonDocument json = null!;
        if (body.Length > 0 && body[0] is '{' or '[')
        {
            json = JsonDocument.Parse(body);
        }

        return new Sent(body, response.Content.Headers.ContentType?.ToString() ?? string.Empty, cookieHeader, json);
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

    private sealed record Sent(string Body, string ContentType, string SetCookie, JsonDocument Json);

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
