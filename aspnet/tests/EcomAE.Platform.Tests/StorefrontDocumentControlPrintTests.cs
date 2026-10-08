using System.Data.Common;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.WebUtilities;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// PHP <c>content/shop/document_control/service/print.php</c> against goldens recorded by
/// <c>Fixtures/DocumentControlPrint/harness.sh</c> (php -S serving the real file and <c>epc_erp_access.php</c> over the same
/// fixture), byte-for-byte with status and content type. PHP's “today” in the goldens is swapped for the run date.
/// </summary>
public sealed class StorefrontDocumentControlPrintTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DocumentControlPrint");

    private const string Admin = "admin_session=adm-sess; admin_u_id=1";

    [Theory]
    [InlineData("guest", "doc=fta_tax_invoice&order_id=40", "")]
    [InlineData("wrong_user", "preview=1", "session=tok-21; u_id=22")]
    [InlineData("customer_plain", "preview=1", "session=tok-28; u_id=28")]
    [InlineData("staff_inactive", "preview=1", "session=tok-29; u_id=29")]
    [InlineData("backend_child", "preview=1", "session=tok-21; u_id=21")]
    [InlineData("backend_tree", "preview=1", "session=tok-22; u_id=22")]
    [InlineData("administrator_group", "preview=1", "session=tok-23; u_id=23")]
    [InlineData("cp_erp_group", "preview=1", "session=tok-24; u_id=24")]
    [InlineData("staff_profile", "preview=1", "session=tok-25; u_id=25")]
    [InlineData("department_group", "preview=1", "session=tok-26; u_id=26")]
    [InlineData("erp_team", "preview=1", "session=tok-27; u_id=27")]
    [InlineData("admin_default", "", Admin)]
    [InlineData("admin_order_40", "doc=fta_tax_invoice&order_id=40", Admin)]
    [InlineData("admin_preview_invoice", "doc=fta_tax_invoice&preview=1&invoice_id=7&order_id=40", Admin)]
    [InlineData("invoice_7", "doc=fta_tax_invoice&invoice_id=7", Admin)]
    [InlineData("invoice_7_packing", "doc=packing_slip&invoice_id=7", Admin)]
    [InlineData("invoice_7_receipt", "doc=payment_receipt&invoice_id=7&order_id=40", Admin)]
    [InlineData("invoice_8_delivery", "doc=delivery_note&invoice_id=8", Admin)]
    [InlineData("invoice_10", "invoice_id=10", Admin)]
    [InlineData("invoice_inactive", "invoice_id=9", Admin)]
    [InlineData("order_missing", "order_id=999", Admin)]
    [InlineData("doc_unknown", "doc=nope%3Cx%3E&order_id=40", Admin)]
    public Task Print_MatchesPhpGolden_OnThrowawayDatabase_ThenDropped(string golden, string query, string cookies)
        => WithDatabaseAsync(async connection =>
        {
            var result = await StorefrontPhpAjax.PrintDocumentControlAsync(connection, Request(query, cookies), CancellationToken.None);
            var today = ErpDocumentControlRender.PhpDate("dd MMM yyyy", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var expected = File.ReadAllText(Path.Combine(FixtureDir, golden + ".html"))
                .Replace(File.ReadAllText(Path.Combine(FixtureDir, "today.txt")).Trim(), today, StringComparison.Ordinal);
            Assert.Equal(expected, result.Body);
            var meta = File.ReadAllText(Path.Combine(FixtureDir, golden + ".meta")).Split('|');
            Assert.Equal((meta[0], meta[1]), (result.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture), result.ContentType));
        });

    [Fact]
    public Task CanAccess_WithoutACpErpAccessList_LetsEverySignedInUserIn_LikePhp()
        => WithDatabaseAsync(async connection =>
        {
            var plain = new ErpUserAccess.Cookies("tok-28", "28", null, null);
            Assert.False(await ErpUserAccess.CanAccessAsync(connection, plain, CancellationToken.None));
            await ExecAsync(connection, "DELETE FROM content_access WHERE content_id = 5");
            Assert.True(await ErpUserAccess.CanAccessAsync(connection, plain, CancellationToken.None));
            Assert.False(await ErpUserAccess.CanAccessAsync(connection, new ErpUserAccess.Cookies(null, null, null, null), CancellationToken.None));
        });

    [Fact]
    public Task BackendGroupIds_WalkTheWholeTree_ElseFallBackToGroupsOneAndThree()
        => WithDatabaseAsync(async connection =>
        {
            Assert.Equal([2L, 3L, 4L], await ErpUserAccess.BackendGroupIdsAsync(connection, CancellationToken.None));
            await ExecAsync(connection, "UPDATE `groups` SET for_backend = 0");
            Assert.Equal([1L, 3L], await ErpUserAccess.BackendGroupIdsAsync(connection, CancellationToken.None));
        });

    [Fact]
    public Task DepartmentCodes_IgnoreTheStaffProfile_WhenNoDepartmentGroupExists_LikePhp()
        => WithDatabaseAsync(async connection =>
        {
            Assert.Equal(["finance"], await ErpUserAccess.DepartmentCodesAsync(connection, 25, CancellationToken.None));
            Assert.Equal(["sales"], await ErpUserAccess.DepartmentCodesAsync(connection, 26, CancellationToken.None));
            await ExecAsync(connection, "DELETE FROM `groups` WHERE id = 11");
            Assert.Empty(await ErpUserAccess.DepartmentCodesAsync(connection, 25, CancellationToken.None));
        });

    [Fact]
    public Task Print_DeniesWhenTheErpChainHitsADatabaseError_LikePhpsCatch()
        => WithDatabaseAsync(async connection =>
        {
            await ExecAsync(connection, "DROP TABLE content_access");
            var result = await StorefrontPhpAjax.PrintDocumentControlAsync(connection, Request("preview=1", "session=tok-27; u_id=27"), CancellationToken.None);
            Assert.Equal((403, "Access denied — sign in to ERP or the control panel."), (result.StatusCode, result.Body));
        });

    [Fact]
    public Task PrintRoute_ServesThePhpUrl_WithTheCookiesAndQueryPhpReads()
        => WithDatabaseAsync(async (_, cs) =>
        {
            var docRoot = Path.Combine(Path.GetTempPath(), "ecomae-dc-print-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(docRoot);
            try
            {
                await using var host = await StorefrontOrderPrintTests.StartAsync(cs, docRoot);
                using var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = host.BaseAddress };
                async Task<(int Status, string Type, string Body)> GetAsync(string query, string cookies)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, StorefrontPhpAjax.DocumentControlPrintPath.TrimStart('/') + "?" + query);
                    if (cookies.Length > 0)
                    {
                        request.Headers.Add("Cookie", cookies);
                    }

                    using var response = await client.SendAsync(request);
                    return ((int)response.StatusCode, response.Content.Headers.ContentType?.ToString() ?? string.Empty, await response.Content.ReadAsStringAsync());
                }

                var golden = File.ReadAllText(Path.Combine(FixtureDir, "invoice_7.html"));
                Assert.Equal((200, "text/html; charset=utf-8", golden), await GetAsync("doc=fta_tax_invoice&invoice_id=7", Admin));
                var team = await GetAsync("doc=packing_slip&invoice_id=7", "session=tok-27; u_id=27");
                Assert.Equal((200, File.ReadAllText(Path.Combine(FixtureDir, "invoice_7_packing.html"))), (team.Status, team.Body));
                Assert.Equal((403, "text/html; charset=utf-8", "Access denied — sign in to ERP or the control panel."), await GetAsync("invoice_id=7", "session=tok-28; u_id=28"));
                Assert.Equal((400, "text/html; charset=utf-8", "<p>Invoice not found</p>"), await GetAsync("invoice_id=9", Admin));
            }
            finally
            {
                Directory.Delete(docRoot, true);
            }
        });

    private static StorefrontPhpAjax.DocumentControlPrintRequest Request(string query, string cookies)
    {
        var q = QueryHelpers.ParseQuery(query).ToDictionary(p => p.Key, p => p.Value.ToString(), StringComparer.Ordinal);
        var c = cookies.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => p[1], StringComparer.Ordinal);
        string? Q(string k) => q.TryGetValue(k, out var v) ? v : null;
        string? C(string k) => c.TryGetValue(k, out var v) ? v : null;
        return new(Q("doc"), Q("order_id"), Q("invoice_id"), Q("preview"), new ErpUserAccess.Cookies(C("session"), C("u_id"), C("admin_session"), C("admin_u_id")));
    }

    private static async Task ExecAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static Task WithDatabaseAsync(Func<DbConnection, Task> run)
        => WithDatabaseAsync((connection, _) => run(connection));

    private static async Task WithDatabaseAsync(Func<DbConnection, string, Task> run)
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

        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        Assert.DoesNotContain("Database=docpart", cs, StringComparison.OrdinalIgnoreCase);
        try
        {
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            foreach (var statement in File.ReadAllText(Path.Combine(FixtureDir, "fixture.sql")).Split(";\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                await ExecAsync(connection, statement);
            }

            await run(connection, cs);
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
}
