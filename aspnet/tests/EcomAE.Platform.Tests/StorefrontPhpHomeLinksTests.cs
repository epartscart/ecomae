using EcomAE.Platform.Middleware;
using EcomAE.Platform.Presentation;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontPhpHomeLinksTests
{
    [Fact]
    public void EnglishRequestUsesPhpAutomotiveHrefs()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/en/";
        ctx.Items[LangHomeFallbackMiddleware.LangItem] = "en";
        var lang = StorefrontPhpHomeLinks.LangHref(ctx);
        Assert.Equal("/en", lang);
        Assert.Equal("/en/parts", StorefrontPhpHomeLinks.Parts(lang));
        Assert.Equal("/en/umapi_catalog", StorefrontPhpHomeLinks.UmapiCatalog(lang));
        Assert.Equal("/en/available-brands", StorefrontPhpHomeLinks.AvailableBrands(lang));
        Assert.Equal("/en/product-family", StorefrontPhpHomeLinks.ProductFamily(lang));
        Assert.Equal("/en/vehicle-catalog", StorefrontPhpHomeLinks.VehicleCatalog(lang));
        Assert.Equal("/en/zapros-prodavczu", StorefrontPhpHomeLinks.SellerRequest(lang));
    }

    [Fact]
    public void ArabicLangHomePrefixesHrefs()
    {
        var ctx = new DefaultHttpContext();
        ctx.Items[LangHomeFallbackMiddleware.LangItem] = "ar";
        var lang = StorefrontPhpHomeLinks.LangHref(ctx);
        Assert.Equal("/ar", lang);
        Assert.Equal("/ar/parts", StorefrontPhpHomeLinks.Parts(lang));
        Assert.Equal("/ar/zapros-prodavczu", StorefrontPhpHomeLinks.SellerRequest(lang));
    }

    [Theory]
    [InlineData("/ar/users/editform", "/ar")]
    [InlineData("/ru/users/profile", "/ru")]
    [InlineData("/me/users/login", "/me")]
    [InlineData("/en/users/editform", "/en")]
    [InlineData("/users/profile", "/en")]
    public async Task Users_pages_keep_the_visitor_prefix_before_and_after_the_lang_alias_rewrite(string path, string expected)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        Assert.Equal(expected, StorefrontPhpHomeLinks.LangHref(ctx));

        string? routed = null;
        string? pageLang = null;
        await new StorefrontLangAliasMiddleware(next =>
        {
            routed = next.Request.Path.Value;
            pageLang = StorefrontPhpHomeLinks.LangHref(next);
            return Task.CompletedTask;
        }).InvokeAsync(ctx);
        Assert.StartsWith(path.StartsWith("/users/", StringComparison.Ordinal) ? "/users/" : "/en/users/", routed, StringComparison.Ordinal);
        Assert.Equal(expected, pageLang);
    }

    [Fact]
    public void HomeAppWiresPhpHrefHelper()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontPreviewApp.razor");
            if (File.Exists(candidate))
            {
                var text = File.ReadAllText(candidate);
                Assert.Contains("StorefrontPhpHomeLinks.Parts", text, StringComparison.Ordinal);
                Assert.Contains("StorefrontPhpHomeLinks.UmapiCatalog", text, StringComparison.Ordinal);
                Assert.Contains("StorefrontPhpHomeLinks.SellerRequest", text, StringComparison.Ordinal);
                Assert.Contains("html lang=", File.ReadAllText(Path.Combine(dir.FullName, "aspnet/src/EcomAE.Platform/Components/App.razor")), StringComparison.Ordinal);
                return;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("StorefrontPreviewApp.razor");
    }
}
