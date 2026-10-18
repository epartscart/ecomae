using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpActionPaneViewsTests
{
    [Fact]
    public void Suppliers_CollapsesAliasesAndKeepsRealTabs()
    {
        var views = ErpActionPaneViews.ForPath("/erp/suppliers-app");

        Assert.Equal(3, views.Count);
        Assert.Contains(views, view => view.IsDefault && view.Label == "Vendors" && view.Href == "/erp/suppliers-app");
        Assert.Contains(views, view => view.Key == "ap_setup" && view.Label == "AP setup" && view.Href.Contains("tab=ap_setup", StringComparison.Ordinal));
        Assert.Contains(views, view => view.Key == "supplier_portal" && view.Label == "Supplier portal");
        Assert.Equal("vendors", ErpActionPaneViews.ActiveKey(views, ""));
        Assert.Equal("ap_setup", ErpActionPaneViews.ActiveKey(views, "ap_setup"));
    }

    [Fact]
    public void ChartOfAccounts_IsOneView()
    {
        var views = ErpActionPaneViews.ForPath("/erp/coa-accounts-app");

        Assert.Single(views);
        Assert.True(views[0].IsDefault);
        Assert.Equal("Chart of accounts", views[0].Label);
        Assert.DoesNotContain(views, view => view.Key.Equals("coa", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CashAccounts_DefaultIsCashAndBankAndForecastIsItsOwnTab()
    {
        var views = ErpActionPaneViews.ForPath("/erp/cash-accounts-app");

        Assert.Contains(views, view => view.IsDefault && view.Label == "Cash & bank" && view.Href == "/erp/cash-accounts-app");
        Assert.Contains(views, view => view.Key == "cash_forecast");
        Assert.Contains(views, view => view.Key == "bank_setup");
        Assert.Contains(views, view => view.Key == "petty_cash");
        Assert.DoesNotContain(views, view => view.Key is "cash" or "banking");
    }
}
