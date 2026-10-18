using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpIndustryDashboardTests
{
    [Theory]
    [InlineData(true, false, false, "core", ErpIndustryDashboardCatalog.Jewellery, "Jewellery dashboard")]
    [InlineData(false, true, false, "core", ErpIndustryDashboardCatalog.RealEstate, "Real estate dashboard")]
    [InlineData(false, false, true, "core", ErpIndustryDashboardCatalog.FitOut, "Fit-out dashboard")]
    [InlineData(false, false, false, "auto_parts", ErpIndustryDashboardCatalog.AutoParts, "Parts trading dashboard")]
    [InlineData(false, false, false, "trading", ErpIndustryDashboardCatalog.Trading, "Trading dashboard")]
    public void SummaryLeadsWithTheCompanyIndustry(bool jewellery, bool realEstate, bool fitOut, string pack, string kind, string title)
    {
        var resolved = ErpIndustryDashboardCatalog.Kind(jewellery, realEstate, fitOut, pack);
        Assert.Equal(kind, resolved);
        Assert.Equal(title, ErpIndustryDashboardCatalog.Title(resolved));
        Assert.False(string.IsNullOrWhiteSpace(ErpIndustryDashboardCatalog.Lead(resolved)));
    }

    [Fact]
    public void DashboardPageRendersAnIndustryBoardAndKeepsTheSharedBooks()
    {
        var page = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpDashboardSummaryApp.razor"));
        var pulse = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpIndustryPulseReadService.cs"));

        Assert.Contains("data-industry=\"@_kind\"", page, StringComparison.Ordinal);
        Assert.Contains("epc-exec-kpis", page, StringComparison.Ordinal);
        Assert.Contains("Executive dashboard", page, StringComparison.Ordinal);
        Assert.Contains("ErpIndustryDashboardCatalog.Kind", page, StringComparison.Ordinal);
        Assert.Contains("24K sell", page, StringComparison.Ordinal);
        Assert.Contains("profit-and-loss", page, StringComparison.Ordinal);
        Assert.Contains("Site records", page, StringComparison.Ordinal);
        Assert.Contains("epc_gold_rates", pulse, StringComparison.Ordinal);
        Assert.Contains("epc_jewel_metal_stock", pulse, StringComparison.Ordinal);
        Assert.Contains("ecomae_fitout_delivery_records", pulse, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO `epc_gold_rates`", pulse, StringComparison.Ordinal);
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

        throw new InvalidOperationException("Missing " + relative);
    }
}
