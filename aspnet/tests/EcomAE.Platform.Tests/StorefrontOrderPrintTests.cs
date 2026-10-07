using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Data;
using EcomAE.Platform.Erp;
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

/// <summary>
/// PHP <c>content/shop/print_docs/service/print.php</c>. The goldens under <c>Fixtures/OrderPrint</c> are the output of the real
/// PHP generators (<c>harness.php</c>) on <c>fixture.sql</c>, in the same print order this test uses.
/// </summary>
public sealed class StorefrontOrderPrintTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "OrderPrint");

    [Fact]
    public async Task OrderPrint_MatchesPhpGoldens_OnThrowawayDatabase_ThenDropped()
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
        var docRoot = Path.Combine(Path.GetTempPath(), "ecomae-print-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(docRoot);
        await File.WriteAllTextAsync(Path.Combine(docRoot, "config.php"), """
            <?php
            class DP_Config {
            public $shop_currency = 'AED';
            }
            """);
        try
        {
            var fixture = await File.ReadAllTextAsync(Path.Combine(FixtureDir, "fixture.sql"));
            foreach (var sql in fixture.Split(";\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                await ExecuteAsync(cs, sql);
            }

            await using var host = await StartAsync(cs, docRoot);
            using var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = host.BaseAddress };
            const string adminCookies = "admin_session=adm-sess; admin_u_id=1";

            foreach (var (golden, doc, order) in new[]
            {
                ("receipt_40", "sales_receipt", 40),
                ("tax_40", "fta_tax_invoice", 40),
                ("tax_40_saved", "uae_tax_invoice", 40),
                ("tax_41", "invoice_for_payment", 41),
                ("receipt_41", "torg_12", 41),
            })
            {
                var (status, body) = await GetAsync(client, "doc_name=" + doc + "&order_id=" + order.ToString(CultureInfo.InvariantCulture) + "&csrf_guard_key=adm-key", adminCookies);
                Assert.True(status == HttpStatusCode.OK, golden + ": " + status + " " + body);
                Assert.Equal(Normalize(await File.ReadAllTextAsync(Path.Combine(FixtureDir, golden + ".html"))), Normalize(body));
            }

            Assert.Equal(
                (await File.ReadAllTextAsync(Path.Combine(FixtureDir, "einvoice_documents.txt"))).Trim(),
                await ScalarAsync(cs, "SELECT GROUP_CONCAT(CONCAT_WS('|', order_id, invoice_number) ORDER BY id SEPARATOR '\n') FROM epc_einvoice_documents"));

            var filled = await GetAsync(client, "doc_name=sales_receipt&order_id=41&csrf_guard_key=", adminCookies);
            Assert.Equal(HttpStatusCode.OK, filled.Status);
            Assert.Contains("<strong>Receipt / Order No:</strong> 41<br />", filled.Body, StringComparison.Ordinal);

            const string customerCookies = "session=cust-sess; u_id=7";
            var mine = await GetAsync(client, "doc_name=sales_receipt&order_id=40&csrf_guard_key=cust-key", customerCookies);
            Assert.Equal(HttpStatusCode.OK, mine.Status);
            Assert.Contains("<strong>Customer:</strong> buyer@example.test / +971500000007</p>", mine.Body, StringComparison.Ordinal);
            Assert.Equal((HttpStatusCode.NotFound, "No such order"), await GetAsync(client, "doc_name=sales_receipt&order_id=42&csrf_guard_key=cust-key", customerCookies));
            Assert.Equal((HttpStatusCode.BadRequest, "doc_name and order_id are required"), await GetAsync(client, "doc_name=sales_receipt&csrf_guard_key=cust-key", customerCookies));

            Assert.Equal((HttpStatusCode.OK, Csrf("Error! CSRF 1")), await GetAsync(client, "doc_name=sales_receipt&order_id=40", customerCookies));
            Assert.Equal((HttpStatusCode.OK, Csrf("Error! CSRF 3")), await GetAsync(client, "doc_name=sales_receipt&order_id=40&csrf_guard_key=", customerCookies));
            Assert.Equal((HttpStatusCode.OK, Csrf("Error! CSRF 3.1")), await GetAsync(client, "doc_name=sales_receipt&order_id=40&csrf_guard_key=x", "session=nope; u_id=7"));
            Assert.Equal((HttpStatusCode.OK, Csrf("Error! CSRF 4")), await GetAsync(client, "doc_name=sales_receipt&order_id=40&csrf_guard_key=adm-key", customerCookies));
            Assert.Equal((HttpStatusCode.OK, Csrf("Error! CSRF 4")), await GetAsync(client, "doc_name=sales_receipt&order_id=40&csrf_guard_key=cust-key&csrf_admin=1", customerCookies + "; " + adminCookies));
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

    private static string Csrf(string message) => "{\"error\":\"" + message + "\",\"message\":\"" + message + "\",\"status\":false}";

    private static string Normalize(string html)
        => Regex.Replace(html, "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", "UUID");

    private static async Task<(HttpStatusCode Status, string Body)> GetAsync(HttpClient client, string query, string cookies)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, StorefrontPhpAjax.OrderPrintPath.TrimStart('/') + "?" + query);
        request.Headers.Add("Cookie", cookies);
        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
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
        return Convert.ToString(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static async Task<ProbeHost> StartAsync(string cs, string docRoot)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture));
        builder.Services.AddSingleton<ITenantDbConnectionFactory>(new FixedConnections(cs));
        builder.Services.AddSingleton<IErpWriteConnectionFactory>(new WriteConnections(cs));
        builder.Services.AddSingleton<IStorefrontPriceAccess>(Stub<IStorefrontPriceAccess>.Create());
        builder.Services.AddSingleton(Stub<ISurfaceDashboardSummaryReporter>.Create());
        builder.Services.AddSingleton(Stub<ICpTenantEmailWriteService>.Create());
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<IErpVoucherNumberService, ErpVoucherNumberService>();
        builder.Services.AddScoped<IErpTaxAmountCalculator, ErpTaxAmountCalculator>();
        builder.Services.AddScoped<IErpAuditLogWriter, ErpAuditLogWriter>();
        builder.Services.AddScoped<IErpGlPostingService, ErpGlPostingService>();
        builder.Services.AddScoped<IErpSettlementAllocationService, ErpSettlementAllocationService>();
        builder.Services.AddScoped<IErpAdvanceVatService, ErpAdvanceVatService>();
        builder.Services.AddScoped<IErpCashWriteService, ErpCashWriteService>();
        builder.Services.AddScoped<IErpInvoiceFromOrderWriteService, ErpInvoiceFromOrderWriteService>();
        builder.Services.AddScoped<IErpDocControlWriteService, ErpDocControlWriteService>();
        builder.Services.Configure<PhpReferenceOptions>(options => options.PhpDocRoot = docRoot);
        var app = builder.Build();
        StorefrontPhpAjaxEndpoints.Map(app);
        await app.StartAsync();
        return new ProbeHost(app, new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/"));
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

    public class Stub<T> : System.Reflection.DispatchProxy
        where T : class
    {
        public static T Create() => Create<T, Stub<T>>();

        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException(targetMethod?.Name ?? "stub");
    }
}
