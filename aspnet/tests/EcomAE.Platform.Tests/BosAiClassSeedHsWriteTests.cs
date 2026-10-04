using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosAiClassSeedHsWriteTests
{
    [Fact]
    public void Route_exposes_seed_hs()
    {
        Assert.Equal("/bos/ai-class/seed-hs", EcomAeRoutes.BosAiClassSeedHs);
        Assert.Equal("/bos/ajax/seed-hs", new BosAjaxWriteCatalog().All
            .First(item => item.Action == "seed_hs").AspNetRouteHint);
    }

    [Fact]
    public void Seed_codes_match_php()
    {
        Assert.Equal(10, BosAiClassWriteService.SeedCodes.Length);
        Assert.Equal("8409", BosAiClassWriteService.SeedCodes[0].Code);
        Assert.Equal("Parts for spark-ignition engines", BosAiClassWriteService.SeedCodes[0].Description);
        Assert.Equal("84", BosAiClassWriteService.SeedCodes[0].Chapter);
        Assert.Equal("Machinery", BosAiClassWriteService.SeedCodes[0].Section);
        Assert.Equal(5.00m, BosAiClassWriteService.SeedCodes[0].DutyRate);
        Assert.Equal("engine piston crankshaft camshaft valve cylinder", BosAiClassWriteService.SeedCodes[0].Keywords);
        Assert.Equal("8544", BosAiClassWriteService.SeedCodes[^1].Code);
        Assert.Equal("Insulated wire and cable", BosAiClassWriteService.SeedCodes[^1].Description);
        Assert.All(BosAiClassWriteService.SeedCodes, code => Assert.Equal(5.00m, code.DutyRate));
        Assert.Equal(10, BosAiClassWriteService.SeedCodes.Select(code => code.Code).Distinct().Count());
    }

    [Fact]
    public async Task Unconfigured_factory_fails_db()
    {
        var written = await new BosAiClassWriteService(new UnconfiguredConnections())
            .SeedHsAsync();
        Assert.False(written.Succeeded);
        Assert.Equal("db", written.Code);
        Assert.Equal("Database unavailable", written.Message);
        Assert.Equal(0, written.Writes);
    }

    [Fact]
    public void Page_posts_native_seed_hs()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/ai-class/seed-hs\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("Seed HS codes", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_seed_hs_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/ai-class/seed-hs");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("seed_hs", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_ai_seed_hs_codes", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_hs_codes", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
        Assert.Contains("/bos/ajax/seed-hs", write.Notes, StringComparison.Ordinal);
        Assert.Contains("refuse-confirm", write.Notes, StringComparison.Ordinal);

        var review = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/ai-class/review");
        Assert.DoesNotContain("seed_hs", review.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_seed_hs_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosAiClassSeedHs", module, StringComparison.Ordinal);
        Assert.Contains("IBosAiClassWriteService", module, StringComparison.Ordinal);
        Assert.Contains("SeedHsAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosAiClassWriteService.cs"));
        Assert.Contains("epc_ai_seed_hs_codes", service, StringComparison.Ordinal);
        Assert.Contains("INSERT IGNORE INTO `epc_hs_codes`", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("refuse-confirm", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IBosAiClassWriteService", program, StringComparison.Ordinal);
        var policy = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Services/PlatformHostPolicy.cs"));
        Assert.Contains("\"ai-class\"", policy, StringComparison.Ordinal);
        var reporter = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs"));
        Assert.Contains("AI classification review/seed-hs", reporter, StringComparison.Ordinal);
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
