using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using EcomAE.Platform.Configuration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class IndustryShowcaseSnapshotTests
{
    [Theory]
    [InlineData("automotive.ecomae.com", "automotive")]
    [InlineData("food.ecomae.com", "food")]
    [InlineData("technology.ecomae.com", "technology")]
    [InlineData("www.ecomae.com", null)]
    [InlineData("www.epartscart.com", null)]
    public void TryResolveHostSlug_MatchesCatalog(string host, string? expected)
    {
        var ok = EcomaeIndustryShowcaseSnapshots.TryResolveHostSlug(host, out var slug);
        if (expected is null)
        {
            Assert.False(ok);
        }
        else
        {
            Assert.True(ok);
            Assert.Equal(expected, slug);
        }
    }

    [Theory]
    [InlineData("automotive", "/", "automotive")]
    [InlineData("automotive", "/storefront/app", "automotive")]
    [InlineData("automotive", "/vehicle-dealership-sales", "automotive__vehicle-dealership-sales")]
    [InlineData("automotive", "/cp/app", null)]
    [InlineData("energy", "/solar-energy", "energy__solar-energy")]
    public void FileSlugFor_HubAndSub(string hostSlug, string path, string? expected)
    {
        Assert.Equal(expected, EcomaeIndustryShowcaseSnapshots.FileSlugFor(hostSlug, path));
    }

    [Fact]
    public void HtmlFor_AutomotiveHub_ContainsIndustryMarkers()
    {
        var html = EcomaeIndustryShowcaseSnapshots.HtmlFor("automotive.ecomae.com", "/");
        Assert.False(string.IsNullOrWhiteSpace(html));
        Assert.Contains("Automotive", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("industry", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ecomae-chrome-surface", html, StringComparison.Ordinal);
        Assert.DoesNotContain("_framework/blazor", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("epc-static.php", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HtmlFor_BeautySkincareFacials_MatchesLivePattern()
    {
        var html = EcomaeIndustryShowcaseSnapshots.HtmlFor("beauty.ecomae.com", "/skincare-facials");
        Assert.False(string.IsNullOrWhiteSpace(html));
        Assert.Contains("Skincare", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ecomae-chrome-surface", html, StringComparison.Ordinal);
        Assert.Contains("skincare-facials", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EveryRenderedIndustrySnapshot_IsRoutableLikeBeautySkincare()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? repo = null;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "content", "general_pages", "epc_rendered_industry");
            if (Directory.Exists(candidate))
            {
                repo = candidate;
                break;
            }

            dir = dir.Parent;
        }

        Assert.False(string.IsNullOrWhiteSpace(repo));
        var files = Directory.GetFiles(repo!, "*.html");
        Assert.True(files.Length >= 700, "expected the full 700+ industrial page set, got " + files.Length);

        var missing = new List<string>();
        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            string host;
            string path;
            var split = name.IndexOf("__", StringComparison.Ordinal);
            if (split < 0)
            {
                host = name + ".ecomae.com";
                path = "/";
            }
            else
            {
                host = name[..split] + ".ecomae.com";
                path = "/" + name[(split + 2)..];
            }

            var html = EcomaeIndustryShowcaseSnapshots.HtmlFor(host, path);
            if (string.IsNullOrWhiteSpace(html))
            {
                missing.Add(name);
            }
        }

        Assert.True(missing.Count == 0, "unroutable industry snapshots: " + string.Join(", ", missing.Take(20)));
    }

    [Fact]
    public void HtmlFor_AutomotiveSub_ContainsDealership()
    {
        var html = EcomaeIndustryShowcaseSnapshots.HtmlFor(
            "automotive.ecomae.com",
            "/vehicle-dealership-sales");
        Assert.False(string.IsNullOrWhiteSpace(html));
        Assert.Contains("dealership", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Resolver_SetsIndustrySubdomainMode()
    {
        var http = new DefaultHttpContext();
        http.Request.Host = new HostString("automotive.ecomae.com");
        http.Request.Path = "/";
        var resolver = new RouteTenantResolver(
            Options.Create(new EcomAeOptions()),
            new EmptyTenantRegistry());

        var tenant = await resolver.ResolveAsync(http);

        Assert.Equal(TenantMode.IndustrySubdomain, tenant.Mode);
        Assert.Equal("industry-automotive", tenant.SiteKey);
    }


    [Fact]
    public void PhpHomeWidgetHtml_RecognizesPackedIndustrySnapshots()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        // Walk up to monorepo from test output.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? repo = null;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "content", "general_pages", "epc_rendered_industry", "automotive.html")))
            {
                repo = dir.FullName;
                break;
            }
            dir = dir.Parent;
        }
        Assert.False(string.IsNullOrWhiteSpace(repo));
        Assert.True(PhpHomeWidgetHtml.IsPhpSourceRoot(repo!));
        Assert.False(PhpHomeWidgetHtml.IsPhpSourceRoot("/tmp/no-such-ecomae-root"));
    }
    private sealed class EmptyTenantRegistry : ITenantRegistry
    {
        public ValueTask<TenantRegistryRecord?> FindByHostAsync(string host, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<TenantRegistryRecord?>(null);
    }
}
