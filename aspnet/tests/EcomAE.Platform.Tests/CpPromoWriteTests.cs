using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpPromoWriteTests
{
    [Fact]
    public void Routes_expose_save()
    {
        Assert.Equal("/cp/promotions-app", EcomAeRoutes.ControlPanelPromotionsApp);
        Assert.Equal("/cp/promotions/write", EcomAeRoutes.ControlPanelPromotionsWrite);
    }

    [Fact]
    public void Normalize_type_code_name_and_active()
    {
        Assert.Equal("percent", CpPromoWriteService.NormalizeType(""));
        Assert.Equal("percent", CpPromoWriteService.NormalizeType("   "));
        Assert.Equal("fixed", CpPromoWriteService.NormalizeType("fixed"));
        Assert.Equal("SAVE10", CpPromoWriteService.NormalizeCode(" SAVE10 "));
        Assert.Equal(40, CpPromoWriteService.NormalizeCode(new string('A', 80)).Length);
        Assert.Equal(160, CpPromoWriteService.NormalizeName(new string('N', 200)).Length);
        Assert.Equal(1, CpPromoWriteService.NormalizeActive(9));
        Assert.Equal(0, CpPromoWriteService.NormalizeActive(0));
    }

    [Fact]
    public void Page_posts_native_save_forms()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpPromotionsApp.razor"));
        Assert.Contains("method=\"post\"", razor);
        Assert.Contains("action=\"/cp/promotions/write\"", razor);
        Assert.Contains("name=\"confirmWrites\"", razor);
        Assert.Contains("value=\"true\"", razor);
        Assert.Contains("name=\"code\"", razor);
        Assert.Contains("name=\"min_spend\"", razor);
        Assert.Contains("does not invent a send", razor);
        Assert.Contains("stay Classic", razor);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor);
        Assert.DoesNotContain("ASP.NET", razor);
        Assert.DoesNotContain("/php-reference/", razor);
        Assert.DoesNotContain("epc_promotions`", razor);
    }

    [Fact]
    public void Catalog_marks_save_write_live_gated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/promotions/write");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_promo_save", row.Notes, StringComparison.Ordinal);
        Assert.Contains("does not change code", row.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_registers_write_service()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpPromoWriteService", src);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ControlPanelPromotionsWrite", module);
        Assert.Contains("ICpPromoWriteService", module);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj"))
                || File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.Platform.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root from " + AppContext.BaseDirectory);
    }
}
