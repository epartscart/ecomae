using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpDashboardFigureLinksTests
{
    [Fact]
    public void YearBookReplacesEmptyShopFiguresAndKeepsARealLedger()
    {
        var book = new ErpLandlordMeasure(2025, 8076457m, 2802027m, 8076457m, 2802027m, 16);
        var empty = Period(0, 0, 0);
        Assert.True(ErpDashboardFigureLinks.UseYearBook(true, book, empty));
        Assert.False(ErpDashboardFigureLinks.UseYearBook(false, book, empty));
        Assert.False(ErpDashboardFigureLinks.UseYearBook(true, ErpLandlordMeasure.Empty, empty));

        var posted = Period(100m, 40m, 60m);
        Assert.False(ErpDashboardFigureLinks.UseYearBook(true, book, posted));

        var applied = ErpDashboardFigureLinks.ApplyYearBook(empty, book);
        Assert.Equal(8076457m, applied.RevenueExVat);
        Assert.Equal(2802027m, applied.PurchaseExVat);
        Assert.Equal(8076457m - 2802027m, applied.ProfitExVat);
        Assert.Equal(applied.ProfitExVat, applied.GlNetProfit);
        Assert.Equal(16, book.Properties);
    }

    [Fact]
    public void EveryFigureOpensThePageThatHoldsIt()
    {
        Assert.Equal("2025 income", ErpDashboardFigureLinks.DisplayLabel("Sales (ex VAT)", 2025));
        Assert.Equal("2025 expenses", ErpDashboardFigureLinks.DisplayLabel("Expenses (purchases)", 2025));
        Assert.Equal("2025 profit & loss", ErpDashboardFigureLinks.DisplayLabel("Gross profit", 2025));
        Assert.Equal("2025 return %", ErpDashboardFigureLinks.DisplayLabel("Gross margin %", 2025));
        Assert.Equal("Cash & bank", ErpDashboardFigureLinks.DisplayLabel("Cash & bank", 2025));
        Assert.Equal("Sales (ex VAT)", ErpDashboardFigureLinks.DisplayLabel("Sales (ex VAT)", 0));

        Assert.Equal("/erp/landlord-app?year=2025#income-lines", ErpDashboardFigureLinks.Href("Sales (ex VAT)", 2025));
        Assert.Equal("/erp/landlord-app?year=2025#income-lines", ErpDashboardFigureLinks.Href("revenue", 2025));
        Assert.Equal("/erp/landlord-app?year=2025#expense-lines", ErpDashboardFigureLinks.Href("Expenses (purchases)", 2025));
        Assert.Equal("/erp/landlord-app?year=2025#profit-and-loss", ErpDashboardFigureLinks.Href("Gross profit", 2025));
        Assert.Equal("/erp/landlord-app?year=2025#profit-and-loss", ErpDashboardFigureLinks.Href("pl", 2025));
        Assert.Equal("/erp/cash-accounts-app", ErpDashboardFigureLinks.Href("Cash & bank", 2025));
        Assert.Equal("/erp/aging-app?tab=ar", ErpDashboardFigureLinks.Href("Receivables", 2025));
        Assert.Equal("/erp/payables-app", ErpDashboardFigureLinks.Href("Payables", 0));
        Assert.Equal("/erp/report-center-app?tab=pl", ErpDashboardFigureLinks.Href("Gross profit", 0));
        Assert.Equal("/erp/sales-orders-app", ErpDashboardFigureLinks.Href("Completed orders", 2025));

        var home = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpBosDashboardApp.razor"));
        Assert.Contains("profit-and-loss", home, StringComparison.Ordinal);
        Assert.Contains("Profit &amp; loss", home, StringComparison.Ordinal);
        var insights = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/ErpHomeInsights.razor"));
        Assert.Contains("Profit &amp; loss", insights, StringComparison.Ordinal);
        Assert.Contains("data-boards=\"overview profit\"", insights, StringComparison.Ordinal);
    }

    private static ErpWorkspacePeriodKpis Period(decimal revenue, decimal purchase, decimal gl)
        => new(0, revenue, purchase, revenue - purchase, 0, 0, 0, 0, revenue, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "open", gl);

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

        throw new FileNotFoundException(relative);
    }
}
