using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpDocumentLineSqlTests
{
    [Theory]
    [InlineData(nameof(LegacySurfaceDashboardSql.SelectErpSalesOrderLines))]
    [InlineData(nameof(LegacySurfaceDashboardSql.SelectErpPurchaseOrderLines))]
    [InlineData(nameof(LegacySurfaceDashboardSql.SelectErpPurchaseInvoiceLines))]
    public void LineQueriesExposeTheAliasesTheLoaderBinds(string constant)
    {
        var sql = constant switch
        {
            nameof(LegacySurfaceDashboardSql.SelectErpSalesOrderLines) => LegacySurfaceDashboardSql.SelectErpSalesOrderLines,
            nameof(LegacySurfaceDashboardSql.SelectErpPurchaseOrderLines) => LegacySurfaceDashboardSql.SelectErpPurchaseOrderLines,
            _ => LegacySurfaceDashboardSql.SelectErpPurchaseInvoiceLines,
        };

        foreach (var alias in new[] { "line_id", "document_id", "item_code", "description", "qty", "unit_price_ex_vat", "line_ex_vat" })
        {
            Assert.Contains("AS " + alias, sql, StringComparison.Ordinal);
        }

        Assert.Contains("@limit", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void PurchaseOrderLinesCarryReceivedQty()
        => Assert.Contains("AS qty_received", LegacySurfaceDashboardSql.SelectErpPurchaseOrderLines, StringComparison.Ordinal);

    [Fact]
    public void InventoryPickerMatchesPhpActiveItemQuery()
    {
        var sql = LegacySurfaceDashboardSql.SelectErpInventoryItemsForPicker;
        Assert.Contains("`epc_erp_inv_items`", sql, StringComparison.Ordinal);
        Assert.Contains("`active` = 1", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY `sku`", sql, StringComparison.Ordinal);
        Assert.Contains("sales_price", sql, StringComparison.Ordinal);
        Assert.Contains("purchase_price", sql, StringComparison.Ordinal);
    }
}
