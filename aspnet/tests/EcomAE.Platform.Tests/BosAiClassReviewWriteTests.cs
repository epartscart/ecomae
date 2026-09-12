using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosAiClassReviewWriteTests
{
    [Fact]
    public void Route_exposes_review()
    {
        Assert.Equal("/bos/ai-class/review", EcomAeRoutes.BosAiClassReview);
    }

    [Fact]
    public void Php_intval_matches_php()
    {
        Assert.Equal(12, BosAiClassWriteService.PhpIntval("12abc"));
        Assert.Equal(0, BosAiClassWriteService.PhpIntval("abc"));
        Assert.Equal(0, BosAiClassWriteService.PhpIntval(null));
        Assert.Equal(-3, BosAiClassWriteService.PhpIntval("-3x"));
    }

    [Fact]
    public async Task Unconfigured_factory_fails_db_even_when_id_is_zero()
    {
        var written = await new BosAiClassWriteService(new UnconfiguredConnections())
            .ReviewAsync(0, "Auto Parts", "Brakes", "8708", 1);
        Assert.False(written.Succeeded);
        Assert.Equal("db", written.Code);
        Assert.Equal("Database unavailable", written.Message);
    }

    [Fact]
    public void Page_posts_native_review()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/ai-class/review\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"classification_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"category\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"subcategory\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"hs_code\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"reviewer_id\"", razor, StringComparison.Ordinal);
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
            item.AspNetRouteOrCapability == "/bos/ai-class/review");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("review", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_ai_review", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_ai_classifications", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_review_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosAiClassReview", module, StringComparison.Ordinal);
        Assert.Contains("IBosAiClassWriteService", module, StringComparison.Ordinal);
        Assert.Contains("ReviewAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosAiClassWriteService.cs"));
        Assert.Contains("epc_ai_review", service, StringComparison.Ordinal);
        Assert.Contains("UPDATE `epc_ai_classifications`", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IBosAiClassWriteService", program, StringComparison.Ordinal);
        var policy = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Services/PlatformHostPolicy.cs"));
        Assert.Contains("\"ai-class\"", policy, StringComparison.Ordinal);
    }

    private sealed class UnconfiguredConnections : EcomAE.Platform.Erp.IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unconfigured factory must not open.");
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
