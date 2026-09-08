using EcomAE.Platform.Components.Shared;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Leftover ERP/CP boards must use Classic page-hd / hpanel chrome, not fake zero KPIs or invent gradient heroes.
/// </summary>
public sealed class ErpCpClassicChromeParityTests
{
    [Fact]
    public void GhostScaffold_DefaultsOff()
    {
        var body = new PhpParityModuleBody();
        Assert.False(body.ShowGhostScaffold);
    }

    [Fact]
    public void WorkflowBoard_UsesClassicPageHeaderAndHpanel()
    {
        var text = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Components/Pages/ErpWorkflowApp.razor"));
        Assert.Contains("PhpErpModulePageHeader", text, StringComparison.Ordinal);
        Assert.Contains("hpanel", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.DoesNotContain("linear-gradient(135deg", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("classic twin", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProcessFlowAndDashboard_UseClassicPageHeader()
    {
        foreach (var name in new[] { "ErpProcessFlowTasksApp.razor", "ErpDashboardSummaryApp.razor" })
        {
            var text = File.ReadAllText(Path.Combine(FindRepoRoot(),
                "aspnet/src/EcomAE.Platform/Components/Pages", name));
            Assert.Contains("PhpErpModulePageHeader", text, StringComparison.Ordinal);
            Assert.DoesNotContain("classic twin", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ModuleApp_DropsFakeZeroKpis()
    {
        var text = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Components/Pages/ErpModuleApp.razor"));
        Assert.DoesNotContain("<div class=\"val\">0</div>", text, StringComparison.Ordinal);
        Assert.Contains("hpanel", text, StringComparison.Ordinal);
        Assert.Contains("Dedicated board", text, StringComparison.Ordinal);
    }

    [Fact]
    public void StaffApp_WiresOpenWorkflowCounts()
    {
        var text = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Components/Pages/ErpStaffApp.razor"));
        Assert.Contains("BuildErpWorkflowTasksDigestAsync", text, StringComparison.Ordinal);
        Assert.Contains("OpenTasksFor", text, StringComparison.Ordinal);
        Assert.Contains("hpanel", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<div class=\"val\">0</div>", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ErpAndCpChrome_UseHostAwareLogoNotHardcodedEparts()
    {
        var erp = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor"));
        var cp = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor"));
        var bos = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpBosDesktopChrome.razor"));
        Assert.Contains("PhpSurfaceHostLogo", erp, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpEpartsCartAnimatedLogo", erp, StringComparison.Ordinal);
        Assert.Contains("PhpSurfaceHostLogo", cp, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpEpartsCartAnimatedLogo", cp, StringComparison.Ordinal);
        Assert.DoesNotContain("eParts Cart - Control Panel", cp, StringComparison.Ordinal);
        Assert.Contains("PhpEcomaeAnimatedLogo", bos, StringComparison.Ordinal);
    }

    [Fact]
    public void CommandCentreAndStorefront_DoNotHardcodeEpartsMark()
    {
        var command = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Components/Pages/CpCommandCentreApp.razor"));
        var storefront = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor"));
        var super = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpSuperCpCommandCentre.razor"));
        Assert.Contains("PhpSurfaceHostLogo", command, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpEpartsCartAnimatedLogo", command, StringComparison.Ordinal);
        Assert.DoesNotContain("private string _brandLabel = \"eParts Cart\"", command, StringComparison.Ordinal);
        Assert.Contains("PhpSurfaceHostLogo", storefront, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpEpartsCartAnimatedLogo", storefront, StringComparison.Ordinal);
        Assert.Contains("PhpEcomaeAnimatedLogo", super, StringComparison.Ordinal);
    }

    [Fact]
    public void OrderStatuses_UsesHpanelNotInventHero()
    {
        var text = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Components/Pages/CpOrderStatusesApp.razor"));
        Assert.Contains("hpanel", text, StringComparison.Ordinal);
        Assert.Contains("panel-heading hbuilt", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-statuses-page__hero", text, StringComparison.Ordinal);
        Assert.DoesNotContain("linear-gradient(135deg", text, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cp", "content", "shop", "finance", "erp", "ajax_erp.php")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cp", "content", "shop", "finance", "erp", "ajax_erp.php")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root.");
    }
}
