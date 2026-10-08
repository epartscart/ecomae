using EcomAE.Platform.Middleware;
using EcomAE.Platform.Presentation;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpAssetWrappersTests
{
    [Fact]
    public void ETag_is_phps_md5_of_mtime_size_and_version()
    {
        Assert.Equal("\"097430a0ae14a29bbbc1858665b43f98\"", PhpAssetWrappers.ETagFor(1700000000, 1234, "20260722pacFix3"));
        Assert.Equal("\"20c0197902df66f2e0ea73f5008dde5b\"", PhpAssetWrappers.ETagFor(1700000000, 1234, null));
    }

    [Fact]
    public void Every_wrapper_has_its_php_file_and_a_source_in_the_repository()
    {
        var root = FindRepoRoot();
        Assert.Equal(40, PhpAssetWrappers.All.Count);
        foreach (var wrapper in PhpAssetWrappers.All)
        {
            Assert.True(File.Exists(Path.Combine(root, wrapper.Url.TrimStart('/'))), wrapper.Url);
            Assert.Contains(wrapper.Sources, source => File.Exists(Path.Combine(root, source)));
        }
    }

    [Fact]
    public void Wrappers_stay_public_like_php()
    {
        foreach (var wrapper in PhpAssetWrappers.All)
        {
            Assert.True(PhpAssetWrappers.IsWrapperPath(wrapper.Url));
            Assert.False(AdminSurfaceAuthGateMiddleware.RequiresAdmin(wrapper.Url), wrapper.Url);
        }

        Assert.True(AdminSurfaceAuthGateMiddleware.RequiresAdmin("/cp/content/control/portal/epc_mobile_apps.php"));
    }

    [Fact]
    public async Task Serves_the_file_with_etag_and_answers_304_on_a_match()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "a.css"), "body{}");
        var wrapper = new PhpAssetWrappers.Wrapper("/x.php", ["a.css"], "text/css; charset=utf-8", "public, max-age=86400", true, "v1", "a.css missing");

        var first = await RunAsync(wrapper, dir.Path, "GET", null);
        Assert.Equal(200, first.StatusCode);
        Assert.Equal("text/css; charset=utf-8", first.ContentType);
        Assert.Equal("public, max-age=86400", first.Headers.CacheControl.ToString());
        Assert.Equal("body{}", Body(first));
        var etag = first.Headers.ETag.ToString();
        var info = new FileInfo(Path.Combine(dir.Path, "a.css"));
        Assert.Equal(PhpAssetWrappers.ETagFor(new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds(), info.Length, "v1"), etag);

        var cached = await RunAsync(wrapper, dir.Path, "GET", " " + etag + " ");
        Assert.Equal(304, cached.StatusCode);
        Assert.Equal(string.Empty, Body(cached));

        var head = await RunAsync(wrapper, dir.Path, "HEAD", null);
        Assert.Equal(200, head.StatusCode);
        Assert.Equal(string.Empty, Body(head));
    }

    [Fact]
    public async Task Missing_source_answers_phps_404_text_or_an_empty_loader()
    {
        using var dir = new TempDir();
        var missing = await RunAsync(new PhpAssetWrappers.Wrapper("/x.php", ["a.css"], "text/css; charset=utf-8", "public, max-age=86400", true, "v1", "a.css missing"), dir.Path, "GET", null);
        Assert.Equal(404, missing.StatusCode);
        Assert.Equal("text/plain; charset=utf-8", missing.ContentType);
        Assert.Equal("a.css missing", Body(missing));

        var loader = await RunAsync(new PhpAssetWrappers.Wrapper("/y.php", ["a.js"], "application/javascript; charset=utf-8", null, false, null, null), dir.Path, "GET", null);
        Assert.Equal(200, loader.StatusCode);
        Assert.Equal("application/javascript; charset=utf-8", loader.ContentType);
        Assert.Equal(string.Empty, Body(loader));
    }

    [Fact]
    public async Task Candidates_are_tried_in_order()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "second.css"), "second");
        File.WriteAllText(Path.Combine(dir.Path, "third.css"), "third");
        var response = await RunAsync(new PhpAssetWrappers.Wrapper("/x.php", ["first.css", "second.css", "third.css"], "text/css; charset=utf-8", null, false, null, "missing"), dir.Path, "GET", null);
        Assert.Equal("second", Body(response));
        Assert.Equal(string.Empty, response.Headers.ETag.ToString());
    }

    private static async Task<HttpResponse> RunAsync(PhpAssetWrappers.Wrapper wrapper, string root, string method, string? ifNoneMatch)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        if (ifNoneMatch is not null)
        {
            context.Request.Headers.IfNoneMatch = ifNoneMatch;
        }

        context.Response.Body = new MemoryStream();
        context.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>(
            new StreamResponseBodyFeature(context.Response.Body));
        await PhpAssetWrappers.WriteAsync(context, wrapper, root);
        return context.Response;
    }

    private static string Body(HttpResponse response)
    {
        response.Body.Position = 0;
        return new StreamReader(response.Body).ReadToEnd();
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("repository root not found");
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("php-asset-wrappers-").FullName + System.IO.Path.DirectorySeparatorChar;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
