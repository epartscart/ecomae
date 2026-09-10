using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosMarketplaceReviewWriteTests
{
    [Fact]
    public void Route_exposes_review()
    {
        Assert.Equal("/bos/marketplace/review", EcomAeRoutes.BosMarketplaceReview);
    }

    [Fact]
    public void Php_review_data_matches_php_defaults_and_clamp()
    {
        Assert.Equal(5, BosMarketplaceWriteService.ParseReviewData(null).Rating);
        Assert.Equal(5, BosMarketplaceWriteService.ParseReviewData("").Rating);
        Assert.Equal(5, BosMarketplaceWriteService.ParseReviewData("{}").Rating);
        Assert.Equal(5, BosMarketplaceWriteService.ParseReviewData("[]").Rating);
        Assert.Equal(5, BosMarketplaceWriteService.ParseReviewData("not-json").Rating);
        Assert.Equal(1, BosMarketplaceWriteService.ParseReviewData("{\"rating\":0}").Rating);
        Assert.Equal(5, BosMarketplaceWriteService.ParseReviewData("{\"rating\":9}").Rating);
        Assert.Equal(3, BosMarketplaceWriteService.ParseReviewData("{\"rating\":3.9}").Rating);
        Assert.Equal(1, BosMarketplaceWriteService.ParseReviewData("{\"rating\":true}").Rating);
        Assert.Equal(4, BosMarketplaceWriteService.ParseReviewData("{\"rating\":\"4stars\"}").Rating);
        var parsed = BosMarketplaceWriteService.ParseReviewData(
            "{\"rating\":4,\"title\":\"Solid\",\"review_text\":\"Works\",\"reviewer_name\":\"Ada\"}");
        Assert.Equal(4, parsed.Rating);
        Assert.Equal("Solid", parsed.Title);
        Assert.Equal("Works", parsed.ReviewText);
        Assert.Equal("Ada", parsed.ReviewerName);
    }

    [Fact]
    public void Page_posts_native_review()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/marketplace/review\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"app_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"site_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"rating\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"review_text\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_review_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/marketplace/review");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_marketplace_add_review", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_marketplace_reviews", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_review_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosMarketplaceReview", module, StringComparison.Ordinal);
        Assert.Contains("AddReviewAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosMarketplaceWriteService.cs"));
        Assert.Contains("epc_marketplace_uninstall", service, StringComparison.Ordinal);
        Assert.Contains("epc_marketplace_add_review", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_marketplace_reviews`", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
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
