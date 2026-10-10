using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// PHP 8.3 goldens in <c>Fixtures/StorefrontFragments</c> for the small storefront fragments. HTTP cases ran the
/// real <c>set_cookie_products_style.php</c> through PHP's built-in server; the other cases ran the real PHP files
/// through <c>harness.php</c>.
/// </summary>
public sealed class StorefrontFragmentsParityTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "StorefrontFragments");

    private static readonly HashSet<string> CoveredFiles = new(StringComparer.Ordinal)
    {
        "content/shop/catalogue/cat_lang_general.php",
        "content/shop/catalogue/product_description.php",
        "content/shop/catalogue/set_cookie_products_style.php",
        "content/shop/docpart/search_strs_for_inputs.php",
        "content/shop/general/get_currency_indicator.php",
        "content/shop/general/shop_button.php",
        "content/users/users_functions.php"
    };

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        var http = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden_http.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
        Assert.StartsWith("8.3.", http.GetProperty("php").GetString(), StringComparison.Ordinal);
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases");
        Assert.Equal(
            cases.EnumerateArray().Select(c => c.GetProperty("name").GetString()),
            golden.GetProperty("results").EnumerateArray().Select(r => r.GetProperty("name").GetString()));
    }

    [Fact]
    public void CatalogueLang_SearchInputs_ShopButton_UsersNode_MatchPhpGolden()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases").EnumerateArray().ToList();
        var results = golden.GetProperty("results").EnumerateArray().ToList();
        var failures = new List<string>();
        for (var i = 0; i < cases.Count; i++)
        {
            var file = cases[i].GetProperty("file").GetString()!;
            if (file is not (
                "content/shop/catalogue/cat_lang_general.php"
                or "content/shop/docpart/search_strs_for_inputs.php"
                or "content/shop/general/shop_button.php"
                or "content/users/users_functions.php"))
            {
                continue;
            }

            var name = cases[i].GetProperty("name").GetString()!;
            var expectedVars = results[i].GetProperty("vars");
            var expectedOutput = results[i].GetProperty("output").GetString() ?? string.Empty;
            switch (file)
            {
                case "content/shop/catalogue/cat_lang_general.php":
                    if (expectedVars.GetProperty("manufacturer_lang").GetString() != StorefrontCatalogueLang.Manufacturer
                        || expectedVars.GetProperty("article_lang").GetString() != StorefrontCatalogueLang.Article)
                    {
                        failures.Add(name);
                    }

                    break;
                case "content/shop/docpart/search_strs_for_inputs.php":
                    var get = ToMap(cases[i], "get");
                    var service = ToMap(cases[i], "content_service_data");
                    var values = StorefrontSearchInputs.From(get, service.Count == 0 ? null : service);
                    if (values.Article != expectedVars.GetProperty("value_for_input_search").GetString()
                        || values.SearchString != expectedVars.GetProperty("value_for_input_search_string").GetString())
                    {
                        failures.Add(name + " article=" + values.Article + " search=" + values.SearchString);
                    }

                    break;
                case "content/shop/general/shop_button.php":
                    var langHref = cases[i].TryGetProperty("lang_href", out var href) ? href.GetString() : string.Empty;
                    var html = StorefrontShopButton.Render(langHref);
                    if (html != expectedOutput)
                    {
                        failures.Add(name + " got " + html);
                    }

                    break;
                default:
                    var userId = cases[i].TryGetProperty("user_id", out var id) ? id.GetInt64() : 0;
                    var usersHref = cases[i].TryGetProperty("lang_href", out var usersLangHref) ? usersLangHref.GetString() : string.Empty;
                    var lang = cases[i].TryGetProperty("lang", out var langEl) ? langEl.GetString() : string.Empty;
                    var usersHtml = StorefrontUsersNode.Render(userId, usersHref, lang);
                    if (usersHtml != expectedOutput)
                    {
                        failures.Add(name + " got " + usersHtml);
                    }

                    break;
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public async Task CurrencyAndProductDescription_MatchPhpGolden_OnThrowawayDatabases_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var golden = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "golden.json"))).RootElement;
        var cases = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases").EnumerateArray().ToList();
        var results = golden.GetProperty("results").EnumerateArray().ToList();
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        var failures = new List<string>();
        for (var i = 0; i < cases.Count; i++)
        {
            var file = cases[i].GetProperty("file").GetString()!;
            if (file is not ("content/shop/general/get_currency_indicator.php" or "content/shop/catalogue/product_description.php"))
            {
                continue;
            }

            var name = cases[i].GetProperty("name").GetString()!;
            var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
            await ExecAsync(admin, "CREATE DATABASE `" + database + "`");
            try
            {
                await using var connection = new MySqlConnection("Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";Pooling=false;");
                await connection.OpenAsync();
                foreach (var sql in cases[i].GetProperty("schema").EnumerateArray().Concat(cases[i].GetProperty("setup").EnumerateArray()))
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = sql.GetString();
                    await command.ExecuteNonQueryAsync();
                }

                if (file == "content/shop/catalogue/product_description.php")
                {
                    var productId = cases[i].GetProperty("vars").GetProperty("product_id").GetInt32();
                    var body = await StorefrontProductDescription.RenderAsync(connection, productId, CancellationToken.None);
                    if (body != results[i].GetProperty("output").GetString())
                    {
                        failures.Add(name + " got " + body);
                    }
                }
                else
                {
                    var config = cases[i].GetProperty("config");
                    var loaded = await StorefrontCurrencyIndicator.LoadAsync(
                        connection,
                        config.GetProperty("shop_currency").GetString(),
                        config.GetProperty("currency_show_mode").GetString(),
                        CancellationToken.None);
                    var vars = results[i].GetProperty("vars");
                    var expectedIndicator = vars.TryGetProperty("currency_indicator", out var ind) && ind.ValueKind != JsonValueKind.Null ? ind.GetString() : null;
                    var expectedSign = vars.TryGetProperty("currency_sign", out var sign) && sign.ValueKind != JsonValueKind.Null ? sign.GetString() : null;
                    if (loaded.Indicator != expectedIndicator || (vars.TryGetProperty("currency_sign", out _) && loaded.Sign != expectedSign))
                    {
                        failures.Add(name + " indicator=" + loaded.Indicator + " sign=" + loaded.Sign);
                    }
                }
            }
            finally
            {
                await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + database + "`");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public async Task SetCookieProductsStyle_MatchesPhpGolden()
    {
        var golden = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "golden_http.json"))).RootElement;
        await using var host = await StartAsync();
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        var failures = new List<string>();
        foreach (var result in golden.GetProperty("results").EnumerateArray())
        {
            var name = result.GetProperty("name").GetString()!;
            var query = result.GetProperty("query").GetString() ?? string.Empty;
            using var response = await client.GetAsync(StorefrontProductsStyle.Path + (query.Length == 0 ? string.Empty : "?" + query));
            var body = await response.Content.ReadAsStringAsync();
            var expectedBody = result.GetProperty("body").GetString() ?? string.Empty;
            var expectedCookies = result.GetProperty("cookies").EnumerateArray().Select(c => c.GetString()!).ToList();
            var cookies = response.Headers.TryGetValues("Set-Cookie", out var set)
                ? set.Select(NormalizeCookie).ToList()
                : [];
            if ((int)response.StatusCode != result.GetProperty("status").GetInt32()
                || body != expectedBody
                || !cookies.SequenceEqual(expectedCookies, StringComparer.Ordinal))
            {
                failures.Add(name + " body=" + body + " cookies=" + string.Join("|", cookies));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedFragments()
        => Assert.Subset(
            JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement
                .GetProperty("cases").EnumerateArray().Select(c => c.GetProperty("file").GetString()!).Append(StorefrontProductsStyle.Path.TrimStart('/')).ToHashSet(StringComparer.Ordinal),
            CoveredFiles);

    private static Dictionary<string, string?> ToMap(JsonElement element, string name)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (!element.TryGetProperty(name, out var obj) || obj.ValueKind != JsonValueKind.Object)
        {
            return map;
        }

        foreach (var property in obj.EnumerateObject())
        {
            map[property.Name] = property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.GetString();
        }

        return map;
    }

    private static string NormalizeCookie(string header)
        => System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Replace(header, "Expires=[^;]+", "Expires=<date>", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            "Max-Age=\\d+",
            "Max-Age=<n>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static async Task ExecAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<ProbeHost> StartAsync()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var app = builder.Build();
        app.MapMethods(StorefrontProductsStyle.Path, ["GET", "POST"], (HttpContext context) => StorefrontProductsStyle.Apply(context.Request, context.Response));
        await app.StartAsync();
        return new ProbeHost(app, new Uri("http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/"));
    }

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
}
