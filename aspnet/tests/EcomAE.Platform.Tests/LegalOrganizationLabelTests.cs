using System.IO;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegalOrganizationLabelTests
{
    [Fact]
    public void Resolve_ReplacesMainCompanyWithLegalName()
    {
        Assert.Equal("Jewellery Trend LLC", LegalOrganizationLabel.Resolve("Main Company", "Jewellery Trend LLC", "ECOM AE"));
        Assert.Equal("ECOM AE", LegalOrganizationLabel.Resolve("Main Company", null, "ECOM AE"));
        Assert.Equal("INDJ Jewellery", LegalOrganizationLabel.Resolve("Main Company", "INDJ Jewellery"));
        Assert.Equal("Jewellery Trend LLC", LegalOrganizationLabel.Display("MAIN", "Main Company", "Jewellery Trend LLC", "ECOM AE"));
        Assert.True(LegalOrganizationLabel.IsGeneric("Main Company"));
        Assert.False(LegalOrganizationLabel.IsGeneric("Jewellery Trend LLC"));
    }

    [Fact]
    public void Resolve_KeepsRealEntityName()
    {
        Assert.Equal("Jewellery Division", LegalOrganizationLabel.Resolve("Jewellery Division", "Jewellery Trend LLC"));
    }

    [Fact]
    public void CompanyDigestReadsPortalTradeNameWhenShopProfileIsEmpty()
    {
        var reporter = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs"));
        Assert.Contains("FROM `epc_co_profile`", reporter, StringComparison.Ordinal);
        Assert.Contains("FROM `epc_portal_tenants` WHERE `site_key` = @siteKey", reporter, StringComparison.Ordinal);
        Assert.Contains("LegalOrganizationLabel.Resolve", reporter, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
