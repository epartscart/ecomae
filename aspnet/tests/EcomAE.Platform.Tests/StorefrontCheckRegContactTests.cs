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
/// <c>content/users/check_reg_contact.php</c>. <c>Fixtures/CheckRegContact/goldens.json</c> is what the real PHP script
/// (with the real <c>stop_csrf.php</c> and <c>dp_user.php</c>) answered for each case of <c>cases.json</c>, run by
/// <c>harness.py</c> on a throwaway schema seeded with <c>seed.sql</c>.
/// </summary>
public sealed class StorefrontCheckRegContactTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CheckRegContact");

    public static IEnumerable<object[]> Cases()
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json")))
            .RootElement.EnumerateArray()
            .Select(c => new object[] { c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Answer_MatchesPhp_OnThrowawayDatabase_ThenDropped(string name)
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

            await using var host = await StartAsync(cs);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var path = StorefrontPhpAjax.CheckRegContactPath.TrimStart('/');
            if (testCase.TryGetProperty("query", out var query))
            {
                path += "?" + string.Join("&", query.EnumerateObject().Select(p => Uri.EscapeDataString(p.Name) + "=" + Uri.EscapeDataString(p.Value.GetString()!)));
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = new FormUrlEncodedContent(testCase.GetProperty("post").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!)),
            };
            if (testCase.TryGetProperty("cookies", out var cookies))
            {
                request.Headers.Add("Cookie", string.Join("; ", cookies.EnumerateObject().Select(p => p.Name + "=" + p.Value.GetString())));
            }

            if (testCase.TryGetProperty("referer", out var referer))
            {
                request.Headers.Referrer = new Uri(referer.GetString()!);
            }

            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(golden.GetProperty("fatal").GetBoolean() ? HttpStatusCode.InternalServerError : HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(golden.GetProperty("body").GetString(), body);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + database + "`");
        }
    }

    private static async Task ExecAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<StorefrontOrderPrintTests.ProbeHost> StartAsync(string cs)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture));
        builder.Services.AddSingleton<ITenantDbConnectionFactory>(new StorefrontOrderPrintTests.FixedConnections(cs));
        builder.Services.AddSingleton(StorefrontOrderPrintTests.Stub<ICpPlatformMailer>.Create());
        builder.Services.AddSingleton(StorefrontOrderPrintTests.Stub<IStorefrontNotifyDispatcher>.Create());
        builder.Services.AddSingleton(StorefrontOrderPrintTests.Stub<IStorefrontPriceAccess>.Create());
        builder.Services.AddSingleton(StorefrontOrderPrintTests.Stub<ISurfaceDashboardSummaryReporter>.Create());
        var app = builder.Build();
        StorefrontPhpAjaxEndpoints.Map(app);
        await app.StartAsync();
        return new StorefrontOrderPrintTests.ProbeHost(app, new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/"));
    }
}
