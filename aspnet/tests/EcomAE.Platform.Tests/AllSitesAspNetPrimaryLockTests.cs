using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class AllSitesAspNetPrimaryLockTests
{
    [Fact]
    public void CataloguesAllSiteClassesAspNetPrimary()
    {
        Assert.Equal(4, AllSitesAspNetPrimaryLock.Classes.Count);
        Assert.Contains(AllSitesAspNetPrimaryLock.Classes, c => c.Id == "super-cp");
        Assert.Contains(AllSitesAspNetPrimaryLock.Classes, c => c.Id == "product-tenants");
        Assert.Contains(AllSitesAspNetPrimaryLock.Classes, c => c.Id == "industry-showcase");
        Assert.Contains(AllSitesAspNetPrimaryLock.Classes, c => c.Id == "lifeos");
        Assert.Equal(5, AllSitesAspNetPrimaryLock.ProductTenantCount);
        Assert.Equal(28, AllSitesAspNetPrimaryLock.IndustryHostCount);
        Assert.Equal(EcomaeIndustryShowcaseHosts.Count, AllSitesAspNetPrimaryLock.IndustryHostCount);
    }

    [Fact]
    public void SummaryLocksAspNetPrimaryAndForbidsCutover()
    {
        var summary = AllSitesAspNetPrimaryLock.BuildSummary();
        Assert.Equal(false, summary["cutoverAllowed"]);
        Assert.Equal(false, summary["readyForPhpRemoval"]);
        Assert.Equal(false, summary["phpPrimaryUntilParity"]);
        Assert.Equal("aspnet", summary["stackToday"]);
        Assert.Equal("aspnet-primary-all-sites-php-reference-only", summary["policy"]);
        Assert.Equal("100%-aspnet-core-live-php-reference-kept", summary["targetEndState"]);
        Assert.Contains("ASP.NET Core", summary["mandate"]!.ToString()!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/php-reference", summary["mandate"]!.ToString()!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("scripts/cloudpanel_FORCE_LIVE_ALL_SITES.sh", summary["forceLiveScript"]);
        Assert.Contains("--all-hosts", summary["classicEntryScript"]!.ToString()!, StringComparison.Ordinal);
        Assert.Contains("industry-aspnet-primary", summary["industryNginxExample"]!.ToString()!, StringComparison.Ordinal);
        Assert.Contains("agriculture|automotive", summary["nginxIndustryHostRegex"]!.ToString()!, StringComparison.Ordinal);

        var classes = (Array)summary["classes"]!;
        Assert.Equal(4, classes.Length);
        foreach (var raw in classes)
        {
            var row = (IReadOnlyDictionary<string, object>)raw!;
            Assert.Equal("aspnet", row["stackToday"]);
            Assert.Equal("aspnet", row["targetStack"]);
            Assert.Contains("/php-reference", row["phpAccess"]!.ToString()!, StringComparison.Ordinal);
        }

        Assert.Equal(PlatformHostPolicy.SuperCpHosts.Count, ((IReadOnlyList<string>)summary["superCpHosts"]!).Count);
        Assert.Equal(PlatformHostPolicy.LifeOsHosts.Count, ((IReadOnlyList<string>)summary["lifeOsHosts"]!).Count);
        Assert.NotEmpty((IReadOnlyList<string>)summary["setLiveCriteria"]!);
    }

    [Fact]
    public void IndustryClassListsTwentyEightHosts()
    {
        var industry = AllSitesAspNetPrimaryLock.Classes.Single(c => c.Id == "industry-showcase");
        Assert.Equal(28, industry.Hosts.Count);
        Assert.Contains("healthcare.ecomae.com", industry.Hosts);
        Assert.Contains("agriculture.ecomae.com", industry.Hosts);
        Assert.Contains("jewellery.ecomae.com", industry.Hosts);
    }
}
