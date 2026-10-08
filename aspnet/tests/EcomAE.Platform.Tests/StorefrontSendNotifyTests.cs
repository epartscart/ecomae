using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Data;
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
/// <c>content/notifications/send_notify.php</c>. <c>Fixtures/SendNotify/goldens.json</c> is what the real PHP script
/// answered, wrote to <c>debug_results</c> and mailed for each case of <c>cases.json</c>, run by <c>harness.py</c> on a
/// throwaway schema seeded with <c>seed.sql</c> (mailer, translator, template and curl stubbed; an address containing
/// "fail" is refused, every SMS operator is unreachable).
/// </summary>
public sealed class StorefrontSendNotifyTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SendNotify");

    public static IEnumerable<object[]> Cases()
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json")))
            .RootElement.EnumerateArray()
            .Select(c => new object[] { c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Answer_DebugResults_AndMail_MatchPhp_OnThrowawayDatabase_ThenDropped(string name)
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var testCase = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json")))
            .RootElement.EnumerateArray().First(c => c.GetProperty("name").GetString() == name).Clone();
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "goldens.json"))).RootElement.GetProperty(name).Clone();

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        await ExecAsync(admin, "CREATE DATABASE `" + database + "`");
        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";";
        Assert.DoesNotContain("Database=docpart", cs, StringComparison.OrdinalIgnoreCase);
        try
        {
            await ExecAsync(cs, await File.ReadAllTextAsync(Path.Combine(FixtureDir, "seed.sql")));
            if (testCase.TryGetProperty("setup", out var setup))
            {
                foreach (var statement in setup.EnumerateArray())
                {
                    await ExecAsync(cs, statement.GetString()!);
                }
            }

            var mailer = new RecordingMailer(testCase.TryGetProperty("osns", out var osns) && osns.GetBoolean());
            await using var host = await StartAsync(cs, mailer);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var form = testCase.GetProperty("post").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
            using var response = await client.PostAsync(StorefrontNotifyDispatcher.SendNotifyPath.TrimStart('/'), new FormUrlEncodedContent(form));
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(golden.GetProperty("fatal").GetBoolean() ? HttpStatusCode.InternalServerError : HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(golden.GetProperty("body").GetString(), body);

            var debug = await RowsAsync(cs, "SELECT name, IFNULL(status,'NULL') FROM debug_results ORDER BY id");
            var expectedDebug = golden.GetProperty("debug").EnumerateArray()
                .Select(r => r[0].GetString() + "=" + Truthy(r[1].GetString()))
                .ToList();
            Assert.Equal(expectedDebug, debug.Select(r => r[0] + "=" + Truthy(r[1])).ToList());

            var mails = golden.GetProperty("mails").EnumerateArray().ToList();
            Assert.Equal(mails.Count, mailer.Sent.Count);
            for (var i = 0; i < mails.Count; i++)
            {
                Assert.Equal(Assert.Single(mails[i].GetProperty("to").EnumerateArray()).GetString(), mailer.Sent[i].To);
                Assert.Equal(mails[i].GetProperty("subject").GetString(), mailer.Sent[i].Subject);
                Assert.Contains(mails[i].GetProperty("body").GetString()!, mailer.Sent[i].Body, StringComparison.Ordinal);
            }
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + database + "`");
        }
    }

    private static bool Truthy(string? value) => value is { Length: > 0 } and not "0" and not "NULL";

    private static async Task ExecAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<List<string[]>> RowsAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string[]>();
        while (await reader.ReadAsync())
        {
            rows.Add([reader.GetString(0), reader.GetString(1)]);
        }

        return rows;
    }

    private static async Task<StorefrontOrderPrintTests.ProbeHost> StartAsync(string cs, RecordingMailer mailer)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture));
        builder.Services.AddSingleton<ITenantDbConnectionFactory>(new FixedConnections(cs));
        builder.Services.AddSingleton<ICpPlatformMailer>(mailer);
        builder.Services.AddSingleton<IStorefrontNotifyDispatcher>(new StorefrontNotifyDispatcher(mailer, null, new UnreachableSms()));
        builder.Services.AddSingleton<IStorefrontPriceAccess>(StorefrontOrderPrintTests.Stub<IStorefrontPriceAccess>.Create());
        builder.Services.AddSingleton(StorefrontOrderPrintTests.Stub<ISurfaceDashboardSummaryReporter>.Create());
        var app = builder.Build();
        StorefrontPhpAjaxEndpoints.Map(app);
        await app.StartAsync();
        return new StorefrontOrderPrintTests.ProbeHost(app, new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/"));
    }

    private sealed record Mail(string To, string Subject, string Body);

    private sealed class RecordingMailer(bool ordersStatusesNotifications) : ICpPlatformMailer
    {
        public List<Mail> Sent { get; } = [];

        public IReadOnlyDictionary<string, string> ReadConfig()
        {
            var config = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["secret_succession"] = "sek",
                ["domain_path"] = "http://127.0.0.1:9/",
            };
            if (ordersStatusesNotifications)
            {
                config["orders_statuses_notifications_settings"] = "1";
            }

            return config;
        }

        public Task<CpSmsSendOutcome> SendHtmlAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            Sent.Add(new Mail(to, subject, htmlBody));
            return Task.FromResult(to.Contains("fail", StringComparison.Ordinal) ? CpSmsSendOutcome.Fail("SMTP refused") : new CpSmsSendOutcome(true, "Sent"));
        }
    }

    private sealed class UnreachableSms : ICpSmsGateway
    {
        public Task<CpSmsSendOutcome> SendAsync(
            string handler,
            IReadOnlyDictionary<string, string> parameters,
            string phone,
            string body,
            CancellationToken cancellationToken = default,
            CpSmsHandlerContext? context = null)
            => Task.FromResult(CpSmsSendOutcome.Fail(string.Empty));
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
}
