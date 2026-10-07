using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Storefront;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontNotifySmsWhatsappTests
{
    private const string ArabicOrder77 = "مرحباً من eParts Cart — طلب #77. للاستفسار ردّوا على هذه الرسالة.";

    [Theory]
    [InlineData("+971 50-111 1111", "971501111111")]
    [InlineData("(050) 123_4567", "0501234567")]
    [InlineData("+7 916 000", "916000")]
    public void SmsPhone_StripsLikePhp(string raw, string expected)
        => Assert.Equal(expected, StorefrontNotifyDispatcher.SmsPhone(raw));

    [Fact]
    public void PlainFromHtml_StripsTagsDecodesAndCollapses()
        => Assert.Equal("Order &amp; 5\n\nDone", StorefrontWhatsappNotifier.PlainFromHtml("<p>Order \t <b>&amp;amp;</b> 5</p>\n\n\n\n<!-- x -->Done "));

    [Fact]
    public void BuildBody_FallsBackFromSmsToEmailToOrderTextToStub()
    {
        var vars = new Dictionary<string, string> { ["order_id"] = "12", ["order_text"] = "<b>Lines</b>" };
        Assert.Equal("SMS", StorefrontWhatsappNotifier.BuildBody(" SMS ", "<i>mail</i>", "new_order_to_user", vars, "Shop", false));
        Assert.Equal("mail", StorefrontWhatsappNotifier.BuildBody("", "<i>mail</i>", "new_order_to_user", vars, "Shop", false));
        Assert.Equal("Lines", StorefrontWhatsappNotifier.BuildBody("", "", "new_order_to_user", vars, "Shop", false));
        Assert.Equal("Shop — order #12", StorefrontWhatsappNotifier.BuildBody("", "", "new_order_to_user", new Dictionary<string, string> { ["order_id"] = "12" }, "Shop", false));
        Assert.Equal("", StorefrontWhatsappNotifier.BuildBody("", "", "new_order_to_user", new Dictionary<string, string>(), "Shop", true));
        Assert.Equal("Hi\nتسجيل دخول عميل — Shop", StorefrontWhatsappNotifier.BuildBody("Hi", "", "epc_customer_login", vars, "Shop", true));
        Assert.Equal("Hi\nرسالة من Shop.", StorefrontWhatsappNotifier.BuildBody("Hi", "", "news", vars, "Shop", true));
    }

    [Fact]
    public void BuildBody_CapsAt3500Bytes()
    {
        var body = StorefrontWhatsappNotifier.BuildBody(new string('é', 2000), "", "x", new Dictionary<string, string>(), "S", false);
        Assert.EndsWith("…", body, StringComparison.Ordinal);
        Assert.Equal(1749, body.Length - 1);
    }

    [Fact]
    public void NotifyNames_DefaultAndFallback()
    {
        Assert.Contains("new_order_to_manager", StorefrontWhatsappNotifier.NotifyNames(new Dictionary<string, string>()));
        Assert.Equal(["new_order_to_user", "order_status_to_customer"], StorefrontWhatsappNotifier.NotifyNames(new Dictionary<string, string> { ["epc_whatsapp_notify_names"] = " , 0 ," }));
        Assert.False(StorefrontWhatsappNotifier.ApiEnabled(new Dictionary<string, string> { ["epc_whatsapp_api_enabled"] = "1", ["epc_whatsapp_api_token"] = "t" }));
    }

    [Fact]
    public async Task Dispatch_SendsSmsAndWhatsappLikePhp_OnThrowawayDatabase_ThenDropped()
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
        try
        {
            foreach (var ddl in Schema)
            {
                await ExecuteAsync(cs, ddl);
            }

            await ExecuteAsync(cs, """
                INSERT INTO notifications_settings (name, email_on, sms_on, send_for_not_confirmed, email_subject, email_body, sms_body, vars) VALUES
                ('new_order_to_user', 1, 1, 0, 'Your order #%order_id%', '<p>%order_text%</p>', 'Order %order_id% received', '[{"name":"order_id","type":"text"},{"name":"order_text","type":"text"}]'),
                ('lpo_to_supplier', 1, 1, 0, 'LPO', 'Body', 'LPO sms', '[]')
                """);
            await ExecuteAsync(cs, """
                INSERT INTO users (user_id, email, email_confirmed, phone, phone_confirmed) VALUES
                (5, 'buyer@example.com', 1, '+971 50-111 1111', 1), (6, '', 0, '0502222222', 0)
                """);
            await ExecuteAsync(cs, "INSERT INTO sms_api (name, handler, parameters_values, active) VALUES ('Unifonic', 'epc_unifonic', '{\"appsid\":\"APP\",\"sender\":\"+971500000001\"}', 1)");
            await ExecuteAsync(cs, "INSERT INTO epc_portal_site_settings (host, hub_name, contact_json) VALUES ('www.epartscart.com', 'Hub', '{\"trade_name\":\"eParts Cart\"}')");

            var sms = new FakeSms();
            var graph = new FakeGraph();
            await using var provider = Services(cs, sms, graph);
            await using var scope = provider.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IStorefrontNotifyDispatcher>();
            var vars = new Dictionary<string, string> { ["order_id"] = "77", ["order_text"] = "Lines" };
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();

            var answer = await dispatcher.SendAsync(
                connection,
                "new_order_to_user",
                vars,
                [StorefrontNotifyPerson.User(5), StorefrontNotifyPerson.User(6), StorefrontNotifyPerson.Direct("guest@example.com", "0503333333")]);
            Assert.True(answer.Found);
            Assert.Equal(true, answer.EmailStatus("5"));
            Assert.Equal(new StorefrontNotifyChannel(true, true, ""), answer.Persons[0].Sms);
            Assert.Equal(new StorefrontNotifyChannel(true, true, ""), answer.Persons[0].WhatsApp);
            Assert.Equal(StorefrontNotifyChannel.None, answer.Persons[1].Sms);
            Assert.Equal(StorefrontNotifyChannel.None, answer.Persons[1].WhatsApp);
            Assert.False(answer.Persons[2].TriedToSend);
            Assert.Equal(StorefrontNotifyChannel.None, answer.Persons[2].Sms);
            Assert.Equal(StorefrontNotifyChannel.None, answer.Persons[2].WhatsApp);

            var sent = Assert.Single(sms.Sent);
            Assert.Equal(("epc_unifonic", "APP", "971501111111", "Order 77 received"), (sent.Handler, sent.Parameters["appsid"], sent.Phone, sent.Body));

            var request = Assert.Single(graph.Requests);
            Assert.Equal("https://graph.facebook.com/v21.0/PHONE-ID/messages", request.Url);
            Assert.Equal("Bearer TOKEN", request.Authorization);
            using (var payload = JsonDocument.Parse(request.Body))
            {
                Assert.Equal("whatsapp", payload.RootElement.GetProperty("messaging_product").GetString());
                Assert.Equal("971501111111", payload.RootElement.GetProperty("to").GetString());
                Assert.False(payload.RootElement.GetProperty("text").GetProperty("preview_url").GetBoolean());
                Assert.Equal("Order 77 received\n" + ArabicOrder77, payload.RootElement.GetProperty("text").GetProperty("body").GetString());
            }

            Assert.Equal(
                "new_order_to_user|971501111111|1|Order 77 received\n" + ArabicOrder77,
                await ScalarAsync(cs, "SELECT CONCAT_WS('|', notify_name, phone, status, message_preview) FROM epc_whatsapp_notify_log"));
            Assert.Contains("wamid.1", await ScalarAsync(cs, "SELECT response FROM epc_whatsapp_notify_log"), StringComparison.Ordinal);

            await ExecuteAsync(cs, "UPDATE notifications_settings SET sms_on = 0, sms_body = '' WHERE name = 'new_order_to_user'");
            graph.Fail = true;
            answer = await dispatcher.SendAsync(connection, "new_order_to_user", vars, [StorefrontNotifyPerson.User(5)]);
            Assert.Equal(StorefrontNotifyChannel.None, answer.Persons[0].Sms);
            Assert.Equal(new StorefrontNotifyChannel(true, false, "Invalid OAuth access token"), answer.Persons[0].WhatsApp);
            Assert.Single(sms.Sent);
            using (var payload = JsonDocument.Parse(graph.Requests[1].Body))
            {
                Assert.Equal("971501111111", payload.RootElement.GetProperty("to").GetString());
                Assert.Equal("Lines\n" + ArabicOrder77, payload.RootElement.GetProperty("text").GetProperty("body").GetString());
            }

            Assert.Equal("0", await ScalarAsync(cs, "SELECT status + 0 FROM epc_whatsapp_notify_log ORDER BY id DESC LIMIT 1"));

            answer = await dispatcher.SendAsync(connection, "lpo_to_supplier", vars, [StorefrontNotifyPerson.User(5)]);
            Assert.True(answer.Persons[0].Sms.TriedToSend);
            Assert.Equal(StorefrontNotifyChannel.None, answer.Persons[0].WhatsApp);
            Assert.Equal(2, graph.Requests.Count);

            await ExecuteAsync(cs, "UPDATE sms_api SET active = 0");
            answer = await dispatcher.SendAsync(connection, "lpo_to_supplier", vars, [StorefrontNotifyPerson.User(5)]);
            Assert.Equal(StorefrontNotifyChannel.None, answer.Persons[0].Sms);
            Assert.Equal(2, sms.Sent.Count);
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            await using var adminConnection = new MySqlConnection(admin);
            await adminConnection.OpenAsync();
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static readonly string[] Schema =
    [
        "CREATE TABLE notifications_settings (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, name VARCHAR(64) NOT NULL, email_on TINYINT NOT NULL DEFAULT 0, sms_on TINYINT NOT NULL DEFAULT 0, send_for_not_confirmed TINYINT NOT NULL DEFAULT 0, email_subject TEXT, email_body TEXT, sms_body TEXT, vars TEXT)",
        "CREATE TABLE users (user_id INT NOT NULL PRIMARY KEY, email VARCHAR(255) NOT NULL DEFAULT '', email_confirmed TINYINT NOT NULL DEFAULT 0, phone VARCHAR(64) NOT NULL DEFAULT '', phone_confirmed TINYINT NOT NULL DEFAULT 0)",
        "CREATE TABLE sms_api (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, name VARCHAR(64) NOT NULL, handler VARCHAR(64) NOT NULL, parameters_values TEXT, active TINYINT NOT NULL DEFAULT 0)",
        "CREATE TABLE epc_portal_site_settings (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, host VARCHAR(255) NOT NULL, hub_name VARCHAR(120) NOT NULL DEFAULT '', contact_json TEXT NULL)",
        "CREATE TABLE lang_text_strings_translation (str_key VARCHAR(64) NOT NULL, lang_code VARCHAR(8) NOT NULL, value TEXT NOT NULL)",
        "CREATE TABLE templates (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, is_frontend TINYINT NOT NULL, current TINYINT NOT NULL, data_value TEXT NULL)",
        "CREATE TABLE shop_obtaining_modes (id INT NOT NULL PRIMARY KEY, caption VARCHAR(255) NOT NULL DEFAULT '')",
    ];

    private static ServiceProvider Services(string cs, FakeSms sms, FakeGraph graph)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICpPlatformMailer>(new ConfigMailer());
        services.AddSingleton<ICpSmsGateway>(sms);
        services.AddSingleton<IHttpClientFactory>(new GraphClients(graph));
        services.AddScoped<IStorefrontWhatsappNotifier, StorefrontWhatsappNotifier>();
        services.AddScoped<IStorefrontNotifyDispatcher, StorefrontNotifyDispatcher>();
        return services.BuildServiceProvider();
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

    private sealed class ConfigMailer : ICpPlatformMailer
    {
        public IReadOnlyDictionary<string, string> ReadConfig()
            => new Dictionary<string, string>
            {
                ["domain_path"] = "https://www.epartscart.com/",
                ["epc_whatsapp_api_enabled"] = "1",
                ["epc_whatsapp_api_token"] = "TOKEN",
                ["epc_whatsapp_phone_number_id"] = "PHONE-ID",
            };

        public Task<CpSmsSendOutcome> SendHtmlAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
            => Task.FromResult(new CpSmsSendOutcome(true, string.Empty));
    }

    private sealed class FakeSms : ICpSmsGateway
    {
        public List<(string Handler, IReadOnlyDictionary<string, string> Parameters, string Phone, string Body)> Sent { get; } = [];

        public Task<CpSmsSendOutcome> SendAsync(string handler, IReadOnlyDictionary<string, string> parameters, string phone, string body, CancellationToken cancellationToken = default)
        {
            Sent.Add((handler, parameters, phone, body));
            return Task.FromResult(new CpSmsSendOutcome(true, string.Empty));
        }
    }

    private sealed class FakeGraph : HttpMessageHandler
    {
        public bool Fail { get; set; }

        public List<(string Url, string Authorization, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.RequestUri!.ToString(), request.Headers.Authorization?.ToString() ?? string.Empty, body));
            return Fail
                ? new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{\"error\":{\"message\":\"Invalid OAuth access token\"}}", Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"messages\":[{\"id\":\"wamid.1\"}]}", Encoding.UTF8, "application/json") };
        }
    }

    private sealed class GraphClients(FakeGraph handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
