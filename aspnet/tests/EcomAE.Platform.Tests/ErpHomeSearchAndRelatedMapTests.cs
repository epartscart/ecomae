using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpHomeSearchAndRelatedMapTests
{
    [Fact]
    public void GlobalSearchPutsIndustryModulesFirstAndFiltersPages()
    {
        var catalog = ErpGlobalSearchCatalog.Build(jewellery: true, fitOut: false);
        Assert.Equal("Jewellery retail", catalog[0].Label);
        Assert.Contains(catalog, e => e.Href.Contains("/erp/purchase-orders-app", StringComparison.Ordinal));
        var hits = ErpGlobalSearchCatalog.Filter(catalog, "gold");
        Assert.Contains(hits, e => e.Label.Contains("Gold", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GlobalSearchFindsTaxExemptPages()
    {
        var hits = ErpGlobalSearchCatalog.Filter(ErpGlobalSearchCatalog.Build(false, false), "tax exempt");
        Assert.Contains(hits, e => e.Label.Contains("Sales tax exempt codes", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(hits, e => e.Label.Contains("Tax exempt numbers", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(hits, e => e.Href.Contains("tax_exempt_codes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ManualProcessesLinkExistingForms()
    {
        var close = ErpManualProcessCatalog.For(null, "monthly_close,year_end");
        Assert.Equal(2, close.Count);
        Assert.Contains(close[0].Steps, step => step.Href == "/erp/vat-app");
        Assert.Contains(close[1].Steps, step => step.Href == "/erp/period-close-app");
        Assert.Contains(ErpManualProcessCatalog.For("time_att", null)[0].Steps, step => step.Href == "/erp/time-attendance-app");
        Assert.Contains(ErpManualProcessCatalog.For("payroll_area", null)[0].Steps, step => step.Href == "/erp/payroll-app");
        Assert.Contains(ErpManualProcessCatalog.For("people", null)[0].Steps, step => step.Href == "/erp/staff-app");
        var sales = ErpManualProcessCatalog.For("sales", null);
        Assert.Contains(sales[0].Steps, step => step.Href == "/erp/sales-orders-app");
        var syncron = ErpManualProcessCatalog.For(null, "syncron");
        Assert.Contains(syncron[0].Steps, step => step.Detail.Contains("purchase order", StringComparison.OrdinalIgnoreCase));
        var profit = ErpManualProcessCatalog.For("cost_mgmt", null);
        Assert.Contains(profit[0].Steps, step => step.Label.Contains("Power BI", StringComparison.OrdinalIgnoreCase));
        var ar = ErpManualProcessCatalog.For("ar", null);
        Assert.Contains(ar[0].Steps, step => step.Href.Contains("ar=open", StringComparison.Ordinal));
        Assert.Contains(ar[0].Steps, step => step.Href.Contains("ar=statement", StringComparison.Ordinal));
        Assert.Contains(ErpManualProcessCatalog.For("purchasing", null)[0].Steps, step => step.Href == "/erp/rfq-app");
        Assert.Contains(ErpManualProcessCatalog.For("inventory_mgmt", null)[0].Steps, step => step.Href.Contains("/erp/syncron-app", StringComparison.Ordinal));
        Assert.Contains(ErpManualProcessCatalog.For(null, "jewellery")[0].Steps, step => step.Href == "/erp/jewellery-retail-app?tab=jw_retail_sales");
    }

    [Fact]
    public void RelatedMapBuildsPurchaseOrderToApInvoice()
    {
        var nodes = ErpRelatedDocumentMapFactory.ForPurchase(
            "88",
            "27/09/11",
            "AED 19,903.13",
            "/erp/purchase-orders-app?po_id=88",
            [
                ErpRelatedDocumentMapFactory.Node("goods_receipt", "Goods Receipt PO", "92", "27/09/11", "AED 16,190.63", "/erp/purchase-orders-app?po_id=88", false, true),
                ErpRelatedDocumentMapFactory.Node("goods_receipt", "Goods Receipt PO", "91", "27/09/11", "AED 3,712.50", "/erp/purchase-orders-app?po_id=88", false, true),
            ],
            "89",
            "27/09/11",
            "AED 16,190.63",
            "/erp/invoices-app?inv_id=89");

        Assert.Equal("Purchase Order", nodes[0].Title);
        Assert.Contains(nodes, n => n.Title == "Goods Receipt PO" && n.Number == "92");
        Assert.Contains(nodes, n => n.Title == "A/P Invoice");
        Assert.True(nodes[0].IsCurrent);
    }
}
