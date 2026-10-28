using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpDashboardGlBookTests
{
    private static ErpWorkspacePeriodKpis Zero() => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "open");

    [Fact]
    public void Empty_operational_figures_take_the_posted_ledger()
    {
        var gl = new ErpDashboardGlBook(55100m, 13156.5m, 680974.38m, 706238.28m, -5319.88m, 35415.72m, 28240m);
        var k = ErpDashboardGlBook.Apply(Zero(), gl);
        Assert.Equal(55100m, k.CashPosition);
        Assert.Equal(13156.5m, k.Receivables);
        Assert.Equal(680974.38m, k.StockValue);
        Assert.Equal(706238.28m, k.Payables);
        Assert.Equal(35415.72m, k.RevenueExVat);
        Assert.Equal(7175.72m, k.ProfitExVat);
        Assert.Equal(7175.72m, k.GlNetProfit);
    }

    [Fact]
    public void Operational_figures_win_over_the_ledger()
    {
        var k = ErpDashboardGlBook.Apply(Zero() with { CashPosition = 10m, StockValue = 20m }, new(1m, 1m, 1m, 1m, 1m, 1m, 1m));
        Assert.Equal(10m, k.CashPosition);
        Assert.Equal(20m, k.StockValue);
        Assert.Equal(1m, k.Receivables);
    }

    [Fact]
    public void Ledger_sql_is_company_scoped_and_reads_coa_bands()
    {
        var sql = ErpDashboardGlBook.BuildSql(1, true);
        Assert.Contains("j.`company_id` = @companyId OR IFNULL(j.`company_id`, 0) = 0", sql, StringComparison.Ordinal);
        Assert.Contains("epc_erp_coa_accounts", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("@companyId", ErpDashboardGlBook.BuildSql(0, false), StringComparison.Ordinal);
    }

    [Fact]
    public void Jewellery_prints_follow_the_print_designer_template()
    {
        var html = "<html><head><!--jw-kind:invoice--><!--jw-tpl--></head><body><div class=\"sheet\"><h1>TAX INVOICE</h1><div class=\"sign\"><span>SIGNATURE</span></div></div></body></html>";
        var t = new Dictionary<string, object?>
        {
            ["primary_color"] = "#b8860b", ["font_size"] = 13, ["header_html"] = "<p>LOGO HERE</p>", ["footer_html"] = "<p>Thank you</p>",
            ["terms_html"] = "<p>No refund on gold</p>", ["show_terms"] = 1, ["signature_labels"] = "Cashier,Manager", ["show_signature_line"] = 1,
        };
        var outHtml = ErpJwVoucherPrint.ApplyTemplate(html, t);
        Assert.Contains("h1{color:#b8860b}", outHtml, StringComparison.Ordinal);
        Assert.Contains("LOGO HERE", outHtml, StringComparison.Ordinal);
        Assert.Contains("No refund on gold", outHtml, StringComparison.Ordinal);
        Assert.Contains("<span>CASHIER</span><span>MANAGER</span>", outHtml, StringComparison.Ordinal);
        Assert.Equal("jw_fixing", ErpJwVoucherPrint.DesignKindFor("METAL SALE FIXING"));
        Assert.Equal("jw_purchase", ErpJwVoucherPrint.DesignKindFor("METAL PURCHASE MP-1"));
        Assert.Equal("payment_voucher", ErpJwVoucherPrint.DesignKindFor("PETTY CASH VOUCHER"));
        Assert.Contains(ErpDocumentDesign.Kinds, k => k.Code == "jw_repair");
    }

    [Fact]
    public void Erp_counts_fill_empty_storefront_figures()
    {
        var ops = new ErpDashboardOps(5, 21, 2, 2, 3, 2, 1, 0, 1, 0, 8, 1, 1, 5);
        var k = ErpDashboardGlBook.ApplyOps(Zero(), ops);
        Assert.Equal(5, k.OrdersCount);
        Assert.Equal(21, k.InventoryItems);
        Assert.Equal(3, k.OpenPurchaseOrders);
        var c = ErpDashboardGlBook.ApplyCommerce(new ErpInsightsCommerceStats(0, 0, 0, 0, 0, 0, 0, 0, 0, 0), ops);
        Assert.Equal(8, c.Clients);
        Assert.Equal(4, c.OpenOrders);
        Assert.Equal(5, c.PriceLists);
    }

    [Fact]
    public void Report_pages_print_with_the_saved_template()
    {
        var block = ErpJwVoucherPrint.ReportPrintBlock(new Dictionary<string, object?>
        {
            ["header_html"] = "<p>INDJ LOGO</p>", ["footer_html"] = "<p>Prepared by finance</p>", ["signature_labels"] = "Accountant,Director", ["primary_color"] = "#123456",
        });
        Assert.Contains("@media print", block, StringComparison.Ordinal);
        Assert.Contains("INDJ LOGO", block, StringComparison.Ordinal);
        Assert.Contains("<span>DIRECTOR</span>", block, StringComparison.Ordinal);
        Assert.Equal(string.Empty, ErpJwVoucherPrint.ReportPrintBlock(null));
    }
}
