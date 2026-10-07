using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Data;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Services;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// The legacy SMS operators in <c>content/sms/handlers</c>. <c>Fixtures/SmsHandlers/goldens.json</c> is what the real PHP
/// handlers send to the provider and print for each case of <c>cases.json</c> (<c>harness.sh</c>: curl, PDO and the
/// translator stubbed, <c>translate_str_by_id(n)</c> = <c>Tn</c>).
/// </summary>
public sealed class CpSmsLegacyOperatorsTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SmsHandlers");

    private static readonly CpSmsHandlerContext TestContext = new(
        (id, _) => Task.FromResult("T" + id.ToString(CultureInfo.InvariantCulture)),
        "https://shop.example.com/");

    public static IEnumerable<object[]> Cases()
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "SmsHandlers", "cases.json")))
            .RootElement.EnumerateArray()
            .Where(c => c.GetProperty("name").GetString() != "sms_ru_forbidden")
            .Select(c => new object[] { c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Handler_SendsAndAnswers_LikePhp(string name)
    {
        var testCase = Case(name);
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "goldens.json"))).RootElement.GetProperty(name);
        var http = new FakeHttp(_ => Reply(testCase));
        var post = testCase.GetProperty("post");

        var answer = await CpSmsLegacyOperators.SendAsync(
            http,
            testCase.GetProperty("handler").GetString()!,
            Parameters(testCase),
            post.GetProperty("main_field").GetString()!,
            post.GetProperty("body").GetString()!,
            TestContext,
            CancellationToken.None);

        Assert.Equal(golden.GetProperty("output").GetString(), CpSmsLegacyOperators.AnswerJson(answer));
        var expected = golden.GetProperty("requests").EnumerateArray().ToList();
        Assert.Equal(expected.Count, http.Requests.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            var sent = http.Requests[i];
            Assert.Equal(expected[i].GetProperty("method").GetString(), sent.Method);
            Assert.Equal(expected[i].GetProperty("url").GetString(), sent.Url);
            Assert.Equal(expected[i].GetProperty("body").ValueKind == JsonValueKind.Null ? null : expected[i].GetProperty("body").GetString(), sent.Body);
            foreach (var header in expected[i].GetProperty("headers").EnumerateArray().Select(h => h.GetString()!))
            {
                var (key, value) = (header[..header.IndexOf(':')], header[(header.IndexOf(':') + 2)..]);
                Assert.True(sent.Headers.TryGetValue(key.ToLowerInvariant(), out var actual), name + " misses " + key);
                Assert.Equal(value, actual);
            }
        }
    }

    [Fact]
    public async Task Smsimple_AuthenticatesThenSends_OverXmlRpc()
    {
        var calls = 0;
        var http = new FakeHttp(_ => ++calls == 1
            ? Xml("<methodResponse><params><param><value><struct><member><name>result</name><value><struct><member><name>session_id</name><value><string>s-1</string></value></member></struct></value></member></struct></value></param></params></methodResponse>")
            : Xml("<methodResponse><params><param><value><struct><member><name>result</name><value><int>55</int></value></member></struct></value></param></params></methodResponse>"));
        var parameters = new Dictionary<string, string> { ["login"] = "me", ["password"] = "p<w", ["signature_id"] = "12" };

        var answer = await CpSmsLegacyOperators.SendAsync(http, "smsimple", parameters, "9161234567", "Hi & bye", TestContext, CancellationToken.None);

        Assert.Equal("{\"status\":true,\"message\":\"\"}", CpSmsLegacyOperators.AnswerJson(answer));
        Assert.Equal(2, http.Requests.Count);
        Assert.All(http.Requests, r => Assert.Equal("http://api.smsimple.ru/", r.Url));
        Assert.Contains("<methodName>pajm.user.auth</methodName>", http.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("<name>password</name><value><string>p&lt;w</string></value>", http.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("<name>session_id</name><value><string>s-1</string></value>", http.Requests[1].Body, StringComparison.Ordinal);
        Assert.Contains("<name>origin_id</name><value><string>12</string></value>", http.Requests[1].Body, StringComparison.Ordinal);
        Assert.Contains("<name>phone</name><value><string>79161234567</string></value>", http.Requests[1].Body, StringComparison.Ordinal);
        Assert.Contains("<name>message</name><value><string>Hi &amp; bye</string></value>", http.Requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Smsimple_ReportsFaultsAndInfo_AndAFailedLogin()
    {
        var fault = new FakeHttp(_ => Xml("<methodResponse><fault><value><struct><member><name>faultCode</name><value><int>4</int></value></member><member><name>faultString</name><value><string>Too many</string></value></member></struct></value></fault></methodResponse>"));
        Assert.Equal(
            "{\"status\":false,\"message\":\"Too many\"}",
            CpSmsLegacyOperators.AnswerJson(await CpSmsLegacyOperators.SendAsync(fault, "smsimple", new Dictionary<string, string>(), "1", "x", TestContext, CancellationToken.None)));

        var info = new FakeHttp(_ => Xml("<methodResponse><params><param><value><struct><member><name>info</name><value><string>Bad origin</string></value></member></struct></value></param></params></methodResponse>"));
        Assert.Equal(
            "{\"status\":false,\"message\":\"Bad origin\"}",
            CpSmsLegacyOperators.AnswerJson(await CpSmsLegacyOperators.SendAsync(info, "smsimple", new Dictionary<string, string>(), "1", "x", TestContext, CancellationToken.None)));

        var empty = new FakeHttp(_ => Xml("<methodResponse><params><param><value><struct><member><name>result</name><value><boolean>0</boolean></value></member></struct></value></param></params></methodResponse>"));
        Assert.Equal(
            "{\"status\":false,\"message\":\"Invalid API username or password\"}",
            CpSmsLegacyOperators.AnswerJson(await CpSmsLegacyOperators.SendAsync(empty, "smsimple", new Dictionary<string, string>(), "1", "x", TestContext, CancellationToken.None)));
        Assert.Single(empty.Requests);
    }

    [Theory]
    [InlineData("sec-key", "sec-key", true)]
    [InlineData("sec-key", "other", false)]
    [InlineData(null, "", true)]
    [InlineData(null, "sec", false)]
    [InlineData("", "", true)]
    [InlineData("1e3", "1000", true)]
    [InlineData(" 10", "10", true)]
    [InlineData("abc", "ABC", false)]
    public void LegacyCheck_IsPhpLooseNotEquals(string? posted, string secret, bool allowed)
        => Assert.Equal(allowed, StorefrontSmsHandlers.LooseEquals(posted, secret));

    [Theory]
    [InlineData("null", true, true)]
    [InlineData("false", true, true)]
    [InlineData("0", true, true)]
    [InlineData("\"0\"", true, true)]
    [InlineData("\"0.0\"", true, true)]
    [InlineData("\"\"", false, false)]
    [InlineData("\"abc\"", false, true)]
    [InlineData("-1", false, false)]
    [InlineData("\"-2\"", false, false)]
    [InlineData("true", false, true)]
    [InlineData("3", false, true)]
    public void PhpComparisons_MatchPhp8(string json, bool equalsZero, bool atLeastZero)
    {
        var value = JsonDocument.Parse(json).RootElement.Clone();
        Assert.Equal(equalsZero, CpSmsLegacyOperators.LooseEqualsZero(value));
        Assert.Equal(atLeastZero, CpSmsLegacyOperators.LooseAtLeastZero(value));
    }

    [Fact]
    public async Task HandlerUrls_AnswerLikePhp_OnThrowawayDatabase_ThenDropped()
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
        var docRoot = Path.Combine(Path.GetTempPath(), "ecomae-sms-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(docRoot);
        await File.WriteAllTextAsync(Path.Combine(docRoot, "config.php"), "<?php\nclass DP_Config {\npublic $secret_succession = 'sec-key';\npublic $domain_path = 'https://shop.example.com/';\n}\n");
        try
        {
            await ExecAsync(cs, "CREATE TABLE sms_api (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, handler VARCHAR(64) NOT NULL, active TINYINT NOT NULL DEFAULT 0, parameters_values TEXT)");
            await ExecAsync(cs, "INSERT INTO sms_api (handler, active, parameters_values) VALUES ('sms_ru', 0, '{\"api_id\":\"key-9\",\"translit\":\"0\"}'), ('epc_unifonic', 1, '{\"appsid\":\"\"}')");

            var http = new FakeHttp(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"OK\",\"sms\":{\"79161234567\":{\"status\":\"OK\"}}}"),
            });
            await using var host = await StartAsync(cs, docRoot, http);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };

            Assert.Equal(
                ("text/html; charset=utf-8", "{\"status\":true,\"message\":\"\"}"),
                await PostAsync(client, "sms_ru", "check=sec-key&body=Hello+there&main_field=%2B79161234567"));
            Assert.Equal("https://sms.ru/sms/send?api_id=key-9&to=79161234567&msg=Hello+there&json=1&translit=0", Assert.Single(http.Requests).Url);

            Assert.Equal(("text/html; charset=utf-8", "{\"status\":false,\"message\":\"Forbidden\"}"), await PostAsync(client, "sms_ru", "check=nope&body=x&main_field=1"));
            Assert.Equal(("text/html; charset=utf-8", "{\"status\":false,\"message\":\"Forbidden\"}"), await PostAsync(client, "terasms_ru", "body=x&main_field=1"));
            Assert.Equal(("application/json; charset=utf-8", "{\"status\":false,\"message\":\"Forbidden\"}"), await PostAsync(client, "epc_du", "body=x&main_field=1"));
            Assert.Equal(("application/json; charset=utf-8", "{\"status\":false,\"message\":\"du operator not configured\"}"), await PostAsync(client, "epc_du", "check=sec-key&body=x&main_field=1"));
            Assert.Equal(
                ("application/json; charset=utf-8", "{\"status\":false,\"message\":\"Unifonic AppSid / API key is required\"}"),
                await PostAsync(client, "epc_unifonic", "check=sec-key&body=x&main_field=0501234567"));
            Assert.Single(http.Requests);

            using var unknown = await client.PostAsync("content/sms/handlers/nosuch/send_sms.php", new StringContent("check=sec-key", Encoding.UTF8, "application/x-www-form-urlencoded"));
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            Directory.Delete(docRoot, true);
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + database + "`");
        }
    }

    private static JsonElement Case(string name)
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json")))
            .RootElement.EnumerateArray().First(c => c.GetProperty("name").GetString() == name).Clone();

    private static IReadOnlyDictionary<string, string> Parameters(JsonElement testCase)
        => testCase.GetProperty("params").ValueKind == JsonValueKind.Null
            ? new Dictionary<string, string>()
            : CpCommunicationsTestService.ParseParameters(testCase.GetProperty("params").GetRawText());

    private static HttpResponseMessage Reply(JsonElement testCase)
    {
        var response = testCase.GetProperty("response");
        if (response.ValueKind == JsonValueKind.Null)
        {
            throw new HttpRequestException("Connection refused");
        }

        var message = new HttpResponseMessage((HttpStatusCode)response.GetProperty("status").GetInt32())
        {
            Content = new StringContent(response.GetProperty("body").GetString()!),
        };
        if (response.TryGetProperty("reason", out var reason))
        {
            message.ReasonPhrase = reason.GetString();
        }

        return message;
    }

    private static HttpResponseMessage Xml(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/xml") };

    private static async Task<(string ContentType, string Body)> PostAsync(HttpClient client, string handler, string form)
    {
        using var response = await client.PostAsync(
            "content/sms/handlers/" + handler + "/send_sms.php",
            new StringContent(form, Encoding.UTF8, "application/x-www-form-urlencoded"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (response.Content.Headers.ContentType?.ToString() ?? string.Empty, await response.Content.ReadAsStringAsync());
    }

    private static async Task ExecAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<StorefrontOrderPrintTests.ProbeHost> StartAsync(string cs, string docRoot, FakeHttp http)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture));
        builder.Services.AddSingleton<ITenantDbConnectionFactory>(new FixedConnections(cs));
        builder.Services.AddSingleton<IErpWriteConnectionFactory>(new WriteConnections(cs));
        builder.Services.AddSingleton<IHttpClientFactory>(http);
        builder.Services.AddSingleton<ICpSmsGateway>(new CpSmsGateway(http));
        builder.Services.AddSingleton<IStorefrontPriceAccess>(StorefrontOrderPrintTests.Stub<IStorefrontPriceAccess>.Create());
        builder.Services.AddSingleton(StorefrontOrderPrintTests.Stub<ISurfaceDashboardSummaryReporter>.Create());
        builder.Services.AddSingleton(StorefrontOrderPrintTests.Stub<ICpTenantEmailWriteService>.Create());
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.Configure<PhpReferenceOptions>(options => options.PhpDocRoot = docRoot);
        var app = builder.Build();
        StorefrontPhpAjaxEndpoints.Map(app);
        await app.StartAsync();
        return new StorefrontOrderPrintTests.ProbeHost(app, new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/"));
    }

    private sealed record Sent(string Method, string Url, string? Body, Dictionary<string, string> Headers);

    private sealed class FakeHttp(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler, IHttpClientFactory
    {
        public List<Sent> Requests { get; } = [];

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var header in request.Headers.NonValidated)
            {
                headers[header.Key.ToLowerInvariant()] = header.Value.ToString();
            }

            string? body = null;
            if (request.Content is not null)
            {
                body = await request.Content.ReadAsStringAsync(cancellationToken);
                foreach (var header in request.Content.Headers)
                {
                    headers[header.Key.ToLowerInvariant()] = string.Join(", ", header.Value);
                }
            }

            Requests.Add(new Sent(request.Method.Method, request.RequestUri!.AbsoluteUri, body, headers));
            return respond(request);
        }
    }

    private sealed class FixedConnections(string cs) : ITenantDbConnectionFactory
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

    private sealed class WriteConnections(string cs) : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public async Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(cs);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }
}
