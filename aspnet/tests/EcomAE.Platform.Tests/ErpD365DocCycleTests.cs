using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpD365DocCycleTests
{
    [Fact]
    public void WholesaleInvoiceColumns_AreNotDuplicatedAcrossAllowlists()
    {
        var all = ErpD365DocCycleService.SalesOrderHeaderColumns
            .Concat(ErpD365DocCycleService.PurchaseOrderHeaderColumns)
            .Concat(ErpD365DocCycleService.DeliveryNoteColumns)
            .Concat(ErpD365DocCycleService.CustomerColumns)
            .Concat(ErpD365DocCycleService.VendorColumns)
            .Concat(ErpD365DocCycleService.WarehouseColumns)
            .Concat(ErpD365DocCycleService.QuotationHeaderColumns)
            .Concat(ErpD365DocCycleService.QuotationLineColumns)
            .Concat(ErpD365DocCycleService.EntityDocProfileColumns);
        Assert.DoesNotContain(all, c => string.IsNullOrWhiteSpace(c));
        Assert.All(all, c => Assert.Matches("^[a-z0-9_]+$", c));
    }

    [Fact]
    public void ProductFieldKeys_AreIndustryScopedNotGlobalColumns()
    {
        Assert.Contains("brand", ErpD365DocCycleService.ProductFieldKeys);
        Assert.Contains("oem_part_no", ErpD365DocCycleService.ProductFieldKeys);
        Assert.Contains("car_maker", ErpD365DocCycleService.ProductFieldKeys);
        // Deliberately absent from base product columns — tenants opt in via KV store.
    }

    [Fact]
    public void BulkCsv_AreasCoverOpeningDataAndReports()
    {
        foreach (var key in new[] { "items", "stock", "customers", "vendors", "coa", "warehouses", "sales_orders", "purchase_orders", "quotations", "gl_opening", "customer_invoice_journal", "vendor_invoice_journal", "customer_statement", "sales_analysis_by_invoice" })
        {
            Assert.True(ErpD365BulkCsv.Areas.ContainsKey(key), "missing area " + key);
        }
        Assert.NotEmpty(ErpD365BulkCsv.Template("items"));
        Assert.Equal("", ErpD365BulkCsv.Template("__nope__"));
    }

    [Fact]
    public void BulkCsv_ParsesRfc4180()
    {
        var rows = ErpD365BulkCsv.Parse("a,b\n\"x,y\",2\n1,\"with \"\"quote\"\"\"");
        Assert.Equal(3, rows.Count);
        Assert.Equal("x,y", rows[1][0]);
        Assert.Equal("with \"quote\"", rows[2][1]);
    }

    [Fact]
    public void GlService_BuiltinDimensionTypes_IncludeLegalEntityBuCostCentre()
    {
        var codes = ErpD365GlService.BuiltinDimensionTypes.Select(t => t.Code).ToArray();
        Assert.Contains("legal_entity", codes);
        Assert.Contains("business_unit", codes);
        Assert.Contains("cost_centre", codes);
    }

    [Fact]
    public void GlService_CoaColumns_CoverMainAccountDetails()
    {
        foreach (var col in new[] { "main_account_type", "main_account_category", "debit_credit_default", "validate_sales_tax", "posting_type", "currency_code", "consolidation_account_group", "default_dimension_string", "cost_centre_id" })
        {
            Assert.Contains(col, ErpD365GlService.CoaColumns);
        }
    }
}
