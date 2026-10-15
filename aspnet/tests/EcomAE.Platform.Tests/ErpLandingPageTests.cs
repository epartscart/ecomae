using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public class ErpLandingPageTests
{
    [Fact]
    public void CompanyHomeAndAModulePageAreAccepted()
    {
        Assert.Equal("/erp", ErpLandingPage.Normalize("/erp"));
        Assert.Equal("/erp/app", ErpLandingPage.Normalize("/erp/app/"));
        Assert.Equal("/erp?dash_profile=cfo", ErpLandingPage.Normalize("/erp?dash_profile=cfo"));
        Assert.Equal("/erp/landlord-app?year=2025", ErpLandingPage.Normalize("/erp/landlord-app?year=2025"));
        Assert.Equal("/erp/coa-accounts-app", ErpLandingPage.Normalize("/cp/client-erp/real/erp/coa-accounts-app?ok=saved"));
    }

    [Fact]
    public void OutsidePagesAreRefused()
    {
        Assert.Null(ErpLandingPage.Normalize("https://evil.example/erp"));
        Assert.Null(ErpLandingPage.Normalize("//evil.example/erp"));
        Assert.Null(ErpLandingPage.Normalize("/erp/../cp"));
        Assert.Null(ErpLandingPage.Normalize("/bos/app"));
        Assert.Null(ErpLandingPage.Normalize("/erp/landing/save"));
        Assert.Null(ErpLandingPage.Normalize("/erp?dash_profile=not-a-home"));
    }

    [Fact]
    public void DefaultHomeUsesTheSavedPageAndADeepLinkStays()
    {
        Assert.True(ErpLandingPage.IsDefaultHome("/erp"));
        Assert.True(ErpLandingPage.IsDefaultHome("/cp/client-erp/real/erp"));
        Assert.True(ErpLandingPage.IsDefaultHome("/erp/app"));
        Assert.False(ErpLandingPage.IsDefaultHome("/erp/landlord-app"));

        Assert.Equal(
            "/cp/client-erp/real/erp/landlord-app?year=2025",
            ErpLandingPage.Prefer("/cp/client-erp/real/erp", "/erp/landlord-app?year=2025"));
        Assert.Equal(
            "/erp/coa-accounts-app",
            ErpLandingPage.Prefer("/erp/coa-accounts-app", "/erp/landlord-app"));
        Assert.Equal("/erp", ErpLandingPage.Prefer("/erp", ""));
    }

    [Fact]
    public void EveryTenantCanPickAHome()
    {
        var core = ErpLandingPage.Choices(false, false, false);
        var real = ErpLandingPage.Choices(false, false, true);
        Assert.Contains(core, choice => choice.Path == "/erp");
        Assert.Contains(core, choice => choice.Path == "/erp?dash_profile=finance");
        Assert.Contains(core, choice => choice.Path == "/erp/coa-accounts-app" || choice.Label.Contains("Chart", StringComparison.OrdinalIgnoreCase) || choice.Path.StartsWith("/erp/", StringComparison.Ordinal));
        Assert.DoesNotContain(core, choice => choice.Path.Contains("landlord", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(real, choice => choice.Path == "/erp/landlord-app");
        Assert.Contains("Set this page as the home page", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor")), StringComparison.Ordinal);
        Assert.Contains("erp/landing/save", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpBosDashboardApp.razor")), StringComparison.Ordinal);
    }

    private static string Find(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(dir.FullName, relative);
            if (File.Exists(path))
            {
                return path;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root was not found.");
    }
}
