using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// The captcha image and check against goldens recorded from the PHP files by <c>Fixtures/Captcha/harness.py</c>.
/// </summary>
public sealed class StorefrontCaptchaTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Captcha");

    private static readonly Lazy<JsonElement> Goldens = new(() => JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "goldens.json"))).RootElement);

    public static TheoryData<string> CheckCases()
    {
        var data = new TheoryData<string>();
        foreach (var testCase in Cases())
        {
            data.Add(testCase.GetProperty("name").GetString()!);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CheckCases))]
    public async Task Check_matches_php(string name)
    {
        var testCase = Cases().Single(c => c.GetProperty("name").GetString() == name);
        var golden = Goldens.Value.GetProperty("check").GetProperty(name);
        await using var host = await Host.StartAsync();

        var path = StorefrontCaptcha.CheckPath + Query(testCase);
        using var request = new HttpRequestMessage(new HttpMethod(testCase.GetProperty("method").GetString()!), path);
        if (testCase.TryGetProperty("post", out var post))
        {
            request.Content = new FormUrlEncodedContent(post.EnumerateObject().Select(p => KeyValuePair.Create(p.Name, p.Value.GetString()!)));
        }

        if (testCase.TryGetProperty("cookie", out var cookie))
        {
            request.Headers.TryAddWithoutValidation("Cookie", StorefrontCaptcha.CookieName + "=" + cookie.GetString());
        }

        using var response = await host.Client.SendAsync(request);
        Assert.Equal(golden.GetProperty("status").GetInt32(), (int)response.StatusCode);
        Assert.Equal(golden.GetProperty("body").GetString(), await response.Content.ReadAsStringAsync());
        Assert.Equal(golden.GetProperty("type").GetString(), response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task Image_sends_php_headers_cookie_and_png()
    {
        var image = Goldens.Value.GetProperty("image");
        var png = image.GetProperty("shapes").EnumerateArray().Single(s => s[0].GetString() == "png");
        var expectedHeaders = image.GetProperty("headers").EnumerateArray().Select(h => (h[0].GetString()!, h[1].GetString()!)).ToList();
        await using var host = await Host.StartAsync();

        var (status, headers, body) = await RawGetAsync(host.Port, StorefrontCaptcha.ImagePath);
        Assert.Equal(png[1].GetInt32(), status);
        foreach (var group in expectedHeaders.GroupBy(h => h.Item1))
        {
            Assert.Equal(group.Select(h => h.Item2), headers.Where(h => h.Name == group.Key).Select(h => h.Value));
        }

        Assert.Equal(png[2].GetString(), headers.Single(h => h.Name == "Content-Type").Value);

        var setCookie = headers.Single(h => h.Name == "Set-Cookie").Value.Split("; ");
        Assert.Matches("^captcha=[0-9a-f]{32}$", setCookie[0]);
        Assert.Contains("max-age=" + png[6].GetString(), setCookie);
        Assert.Contains("path=" + png[7].GetString(), setCookie);
        Assert.Contains(setCookie, p => p.StartsWith("expires=", StringComparison.Ordinal));

        using var bitmap = SKBitmap.Decode(body);
        Assert.NotNull(bitmap);
        Assert.Equal(png[3].GetInt32(), bitmap.Width);
        Assert.Equal(png[4].GetInt32(), bitmap.Height);
    }

    [Fact]
    public async Task Refresh_button_is_served()
    {
        await using var host = await Host.StartAsync();
        using var response = await host.Client.GetAsync(StorefrontCaptcha.RefreshPath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(RepoRoot(), "lib", "captcha", "refresh.png")), await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public void Codes_use_php_charset_and_length_and_pass_their_own_check()
    {
        var random = new Random(20261008);
        for (var i = 0; i < 500; i++)
        {
            var code = StorefrontCaptcha.GenerateCode(random);
            Assert.InRange(code.Length, 4, 7);
            Assert.All(code, c => Assert.Contains(c, StorefrontCaptcha.Chars));
            Assert.True(StorefrontCaptcha.Check(code, StorefrontCaptcha.CookieValue(code)));
            Assert.False(StorefrontCaptcha.Check(code + "x", StorefrontCaptcha.CookieValue(code)));
        }

        using var bitmap = SKBitmap.Decode(StorefrontCaptcha.RenderPng("k7n4", new Random(1)));
        Assert.Equal((150, 70), (bitmap.Width, bitmap.Height));
    }

    private static IEnumerable<JsonElement> Cases()
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json"))).RootElement.EnumerateArray().ToList();

    private static string Query(JsonElement testCase)
        => testCase.TryGetProperty("query", out var query)
            ? "?" + string.Join("&", query.EnumerateObject().Select(p => Uri.EscapeDataString(p.Name) + "=" + Uri.EscapeDataString(p.Value.GetString()!)))
            : string.Empty;

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "lib", "captcha")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("lib/captcha");
    }

    private static async Task<(int Status, List<(string Name, string Value)> Headers, byte[] Body)> RawGetAsync(int port, string path)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, port);
        await using var stream = tcp.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes("GET " + path + " HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n"));
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        var raw = buffer.ToArray();
        var split = raw.AsSpan().IndexOf("\r\n\r\n"u8);
        var lines = Encoding.ASCII.GetString(raw, 0, split).Split("\r\n");
        var headers = lines.Skip(1).Select(l => l.Split(": ", 2)).Select(p => (p[0], p[1])).ToList();
        var body = raw[(split + 4)..];
        if (headers.Any(h => h.Item1 == "Transfer-Encoding" && h.Item2 == "chunked"))
        {
            body = Dechunk(body);
        }

        return (int.Parse(lines[0].Split(' ')[1], CultureInfo.InvariantCulture), headers, body);
    }

    private static byte[] Dechunk(byte[] body)
    {
        using var output = new MemoryStream();
        var offset = 0;
        while (true)
        {
            var end = body.AsSpan(offset).IndexOf("\r\n"u8) + offset;
            var size = int.Parse(Encoding.ASCII.GetString(body, offset, end - offset), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (size == 0)
            {
                return output.ToArray();
            }

            output.Write(body, end + 2, size);
            offset = end + 2 + size + 2;
        }
    }

    private sealed class Host : IAsyncDisposable
    {
        private readonly WebApplication app;

        private Host(WebApplication app, int port)
        {
            this.app = app;
            Port = port;
            Client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/") };
        }

        public int Port { get; }

        public HttpClient Client { get; }

        public static async Task<Host> StartAsync()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture));
            builder.Services.AddSingleton<EcomAE.Platform.Data.ITenantDbConnectionFactory>(new NoConnections());
            var app = builder.Build();
            app.UseRouting();
            StorefrontUsersRegisterEndpoints.Map(app);
            await app.StartAsync();
            return new Host(app, port);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class NoConnections : EcomAE.Platform.Data.ITenantDbConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<DbConnection> OpenAsync(string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<DbConnection> OpenForTenantAsync(EcomAE.Platform.Services.TenantContext? tenant, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }
}
