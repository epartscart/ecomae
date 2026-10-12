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
