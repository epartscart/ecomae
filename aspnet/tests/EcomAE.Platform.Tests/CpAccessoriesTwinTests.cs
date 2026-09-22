using EcomAE.Platform.Cp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpAccessoriesTwinTests
{
    [Fact]
    public void Storefront_and_photo_urls_match_php_helpers()
    {
        Assert.Equal("/en/accessories-spare-parts", CpAccessoriesListService.StorefrontUrl(0, "", ""));
        Assert.Equal("/en/accessories-spare-parts?id=7&category=brakes&subcategory=pads",
            CpAccessoriesListService.StorefrontUrl(7, "brakes", "pads"));
        Assert.Equal("/content/files/images/accessories/acc_1_2_ab.jpg", CpAccessoriesListService.PhotoUrl("../acc_1_2_ab.jpg"));
        Assert.Equal("", CpAccessoriesListService.PhotoUrl("  "));
        Assert.Equal("make", CpAccessoriesListService.NormalizeTermType("bogus"));
        Assert.Equal("condition", CpAccessoriesListService.NormalizeTermType(" Condition "));
        Assert.Equal(["published", "draft", "unpublished"], CpAccessoriesListService.Statuses);
        Assert.Equal(5, CpAccessoriesListService.TermTypes.Length);
    }

    [Fact]
    public void Razor_is_php_shaped_twin_not_digest_shell()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpAccessoriesApp.razor"));
        Assert.DoesNotContain("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("CpPhpModuleCopy.PurposeFor", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildCpAccessoriesDigestAsync", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);

        Assert.Contains("/platform-assets/epc_accessories_cp.css", razor, StringComparison.Ordinal);
        Assert.Contains("class=\"col-lg-12 epc-acc-cp\"", razor, StringComparison.Ordinal);
        Assert.Contains("Accessories Marketplace", razor, StringComparison.Ordinal);
        Assert.Contains("+ Add listing", razor, StringComparison.Ordinal);
        Assert.Contains("Manage categories &amp; filters", razor, StringComparison.Ordinal);
        Assert.Contains("Categories ready to fill", razor, StringComparison.Ordinal);
        Assert.Contains("Filter lists — Make · Model · Year · City · Condition", razor, StringComparison.Ordinal);

        foreach (var field in new[] { "category", "subcategory", "title", "description", "make", "model", "year", "city", "condition_type", "status", "price", "compare_price", "currency", "stock_qty", "photo_count", "featured", "image_url", "external_url" })
        {
            Assert.Contains("name=\"" + field + "\"", razor, StringComparison.Ordinal);
        }

        foreach (var action in new[] { "save", "set_status", "delete", "save_category", "set_category_active", "delete_category", "save_term", "set_term_active", "delete_term", "upload", "set_primary" })
        {
            Assert.Contains("name=\"action\" value=\"" + action + "\"", razor, StringComparison.Ordinal);
        }

        Assert.Contains("name=\"photos[]\"", razor, StringComparison.Ordinal);
        Assert.Contains("enctype=\"multipart/form-data\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\" value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("class=\"pagination\"", razor, StringComparison.Ordinal);
        Assert.Contains("Acc.SearchAsync(_filter", razor, StringComparison.Ordinal);
        Assert.Contains("CpAccessoriesListService.ReadFilter(ctx.Request)", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Photos_endpoint_stores_multipart_bytes_and_unlinks_on_delete()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("writes.StoreUploadAsync(listingId, file", module, StringComparison.Ordinal);
        Assert.Contains("CpAccessoriesPhotoWriteService.TryUnlink(photoRoot, unlink)", module, StringComparison.Ordinal);
        Assert.Contains("\"content\", \"files\", \"images\", \"accessories\"", module, StringComparison.Ordinal);

        var svc = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpAccessoriesPhotoWriteService.cs"));
        Assert.Contains("8 * 1024 * 1024", svc, StringComparison.Ordinal);
        Assert.Contains("\"webp\"", svc, StringComparison.Ordinal);
        Assert.Contains("\"acc_\"", svc, StringComparison.Ordinal);

        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpAccessoriesListService", program, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Repository root with aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj was not found.");
    }
}
