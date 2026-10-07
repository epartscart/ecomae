using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
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

public sealed class ShopOrderProtocolEndpointTests
{
    [Theory]
    [InlineData("1", 1, true)]
    [InlineData(" 1.0 ", 1, true)]
    [InlineData("1abc", 1, false)]
    [InlineData("", 1, false)]
    [InlineData("4", 1, false)]
    public void PhpLooseEquals_ComparesNumericStringsLikePhp8(string raw, int value, bool expected)
        => Assert.Equal(expected, StorefrontPhpAjax.PhpLooseEquals(raw, value));

    [Fact]
    public void ProtocolIds_ReadsJsonArraysOnly()
    {
        Assert.Equal([5L, 6L, 7L], StorefrontPhpAjax.ProtocolIds("[5,\"6\",7.0]"));
        Assert.Empty(StorefrontPhpAjax.ProtocolIds("{\"a\":1}"));
        Assert.Empty(StorefrontPhpAjax.ProtocolIds("[5"));
        Assert.False(StorefrontPhpAjax.TechKeyMatches(null, string.Empty));
        Assert.False(StorefrontPhpAjax.TechKeyMatches(string.Empty, string.Empty));
        Assert.True(StorefrontPhpAjax.TechKeyMatches("local-tech", "local-tech"));
    }

    [Fact]
    public async Task ProtocolRoutes_AuthorizeLikePhp_OnThrowawayDatabase_ThenDropped()
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
        var configRoot = Path.Combine(Path.GetTempPath(), "ecomae-protocol-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(configRoot);
        await File.WriteAllTextAsync(Path.Combine(configRoot, "config.php"), """
            <?php
            class DP_Config {
            public $tech_key = 'local-tech';
            }
            """);
        try
        {
            await using (var connection = new MySqlConnection(cs))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL DEFAULT 0, csrf_guard_key VARCHAR(64) NOT NULL DEFAULT '');
                    INSERT INTO sessions VALUES (15, 'admin-token', 9, 1, 'admin-csrf');
                    """;
                await command.ExecuteNonQueryAsync();
            }

            var protocol = new RecordingProtocol();
            await using var host = await StartAsync(cs, configRoot, protocol);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            const string Staff = "admin_session=admin-token; admin_u_id=9";

            var unknown = await GetAsync(client, StorefrontPhpAjax.SetOrderStatusPath + "?initiator=3&orders=[5]&status=3", string.Empty);
            Assert.Equal((HttpStatusCode.OK, string.Empty), (unknown.Status, unknown.Body));
            Assert.StartsWith("application/json", unknown.ContentType, StringComparison.Ordinal);
            var unknownItem = await GetAsync(client, StorefrontPhpAjax.SetOrderItemStatusPath + "?initiator=4&orders_items=[5]&status=3", string.Empty);
            Assert.Equal(string.Empty, unknownItem.Body);

            Assert.Equal(
                "{\"status\":false,\"message\":\"Wrong key\",\"code\":503}",
                (await GetAsync(client, StorefrontPhpAjax.SetOrderStatusPath + "?initiator=4&orders=[5]&status=3&key=nope", string.Empty)).Body);
            Assert.Equal(
                "{\"status\":false,\"message\":\"Forbidden\",\"code\":501}",
                (await GetAsync(client, StorefrontPhpAjax.SetOrderItemStatusPath + "?initiator=2&orders_items=[5]&status=3", string.Empty)).Body);
            Assert.Contains("Error! CSRF 1", (await GetAsync(client, StorefrontPhpAjax.SetOrderStatusPath + "?initiator=1&orders=[5]&status=3", Staff)).Body, StringComparison.Ordinal);
            Assert.Contains("Error! CSRF 4", (await GetAsync(client, StorefrontPhpAjax.SetOrderStatusPath + "?initiator=1&orders=[5]&status=3&csrf_guard_key=bad", Staff)).Body, StringComparison.Ordinal);
            Assert.Empty(protocol.Calls);

            Assert.Equal(
                "{\"status\":true}",
                (await GetAsync(client, StorefrontPhpAjax.SetOrderStatusPath + "?initiator=4&orders=" + Uri.EscapeDataString("[5,\"6\"]") + "&status=2&key=local-tech", string.Empty)).Body);
            Assert.Equal(
                "{\"status\":true}",
                (await GetAsync(client, StorefrontPhpAjax.SetOrderStatusPath + "?initiator=1&orders=[7]&status=4&csrf_guard_key=admin-csrf", Staff)).Body);
            protocol.Next = new ShopProtocolResult(false, "Error. Bad count");
            Assert.Equal(
                "{\"status\":false,\"message\":\"Error. Bad count\"}",
                (await GetAsync(client, StorefrontPhpAjax.SetOrderItemStatusPath + "?initiator=1&orders_items=[31]&status=12&retun=1&count=2&csrf_guard_key=admin-csrf", Staff)).Body);
            protocol.Next = ShopProtocolResult.Ok;
            Assert.Equal(
                "{\"status\":true}",
                (await GetAsync(client, StorefrontPhpAjax.SetOrderItemStatusPath + "?initiator=2&orders_items=[31,32]&status=11&key=local-tech", string.Empty)).Body);

            Assert.Equal(
                [
                    "order|5,6|2|robot|0|",
                    "order|7|4|manager|9|",
                    "item|31|12|manager|9|2",
                    "item|31,32|11|robot|0|",
                ],
                protocol.Calls);
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            Directory.Delete(configRoot, true);
            await using var adminConnection = new MySqlConnection(admin);
            await adminConnection.OpenAsync();
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task<(HttpStatusCode Status, string Body, string ContentType)> GetAsync(HttpClient client, string path, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (cookie.Length > 0)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(), response.Content.Headers.ContentType?.ToString() ?? string.Empty);
    }

    private static async Task<ProbeHost> StartAsync(string cs, string configRoot, RecordingProtocol protocol)
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
        builder.Services.AddSingleton<IShopOrderProtocolService>(protocol);
        builder.Services.Configure<PhpReferenceOptions>(options => options.PhpDocRoot = configRoot);
        var app = builder.Build();
        StorefrontPhpAjaxEndpoints.Map(app);
        await app.StartAsync();
        return new ProbeHost(app, new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/"));
    }

    private sealed class RecordingProtocol : IShopOrderProtocolService
    {
        public List<string> Calls { get; } = [];

        public ShopProtocolResult Next { get; set; } = ShopProtocolResult.Ok;

        public Task<ShopProtocolResult> SetOrderStatusAsync(DbConnection connection, IReadOnlyList<long> orderIds, long status, ShopProtocolActor actor, CancellationToken cancellationToken = default)
        {
            Calls.Add(Line("order", orderIds, status, actor, null));
            return Task.FromResult(Next);
        }

        public Task<ShopProtocolResult> SetOrderItemStatusAsync(DbConnection connection, IReadOnlyList<long> itemIds, long status, ShopProtocolActor actor, int? returnSplitCount = null, CancellationToken cancellationToken = default)
        {
            Calls.Add(Line("item", itemIds, status, actor, returnSplitCount));
            return Task.FromResult(Next);
        }

        private static string Line(string kind, IReadOnlyList<long> ids, long status, ShopProtocolActor actor, int? split)
            => string.Join(
                "|",
                kind,
                string.Join(",", ids),
                status.ToString(CultureInfo.InvariantCulture),
                actor.IsManager ? "manager" : "robot",
                actor.AdminId.ToString(CultureInfo.InvariantCulture),
                split?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
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
