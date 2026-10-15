using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpFitOutCostingTests
{
    [Fact]
    public void MarginSellingIsNotTheSameAsMarkup()
    {
        var direct = ErpFitOutCalc.TotalDirectCost(10m, ErpFitOutCalc.DirectUnitCost(100, 40, 10, 50), 0);
        var estimated = ErpFitOutCalc.EstimatedTotalCost(direct, 200, 0);
        Assert.Equal(2200m, estimated);
        Assert.Equal(2750m, ErpFitOutCalc.SellingFromMargin(estimated, 20));
        Assert.Equal(2640m, ErpFitOutCalc.SellingFromMarkup(estimated, 20));
        Assert.Equal(0m, ErpFitOutCalc.SellingFromMargin(estimated, 100));
    }

    [Fact]
    public void ProjectBooksKeepCommitmentVariationAndChequesApart()
    {
        var revenue = ErpFitOutCalc.ContractRevenue(1_000_000, 50_000, 80_000);
        Assert.Equal(1_050_000m, revenue);
        var eac = ErpFitOutCalc.EstimateAtCompletion(400_000, 500_000);
        Assert.Equal(900_000m, eac);
        Assert.Equal(150_000m, ErpFitOutCalc.ForecastProfit(revenue, eac));
        Assert.Equal(14.29m, ErpFitOutCalc.ForecastMarginPercent(150_000, revenue));
        Assert.Equal(40_000m, ErpFitOutCalc.OpenCommitment(100_000, 60_000));
        Assert.Equal(0m, ErpFitOutCalc.OpenCommitment(60_000, 60_000));
        Assert.Equal(89_750m, ErpFitOutCalc.ProgressNet(100_000, 10_000, 5_000, 4_750));
        Assert.False(ErpFitOutCalc.CountsAsBankCash("held"));
        Assert.True(ErpFitOutCalc.CountsAsBankCash("cleared"));
        Assert.Equal(0m, ErpFitOutCalc.ApprovedVariationValue("pending", 80_000));
        Assert.Equal(50_000m, ErpFitOutCalc.ApprovedVariationValue("approved", 50_000));
        Assert.Equal(450m, ErpFitOutCalc.LabourCost(8, 2, 45));
        Assert.Equal(2_000m, ErpFitOutCalc.RetentionOutstanding(10_000, 8_000));
    }

    [Fact]
    public void WorkbookMatchesReferenceSheetsAndRejectsABadBoq()
    {
        var book = ErpFitOutWorkbook.Read(ErpFitOutWorkbook.Template());
        Assert.Equal(31, book.Count);
        Assert.Contains(book, sheet => sheet.Name == "BOQ" && sheet.Headers.Contains("Target_Margin_Pct"));
        Assert.Contains(book, sheet => sheet.Name == "PDC_Cheques" && sheet.Headers.Contains("Cleared_Date"));
        Assert.Contains(book, sheet => sheet.Name == "START_HERE" && sheet.Records.Any(row => row.Values.Contains("1.0 | 15 Oct 2026")));
        Assert.Contains(book, sheet => sheet.Name == "Suppliers" && sheet.Headers[0] == "Supplier_ID");
        Assert.Contains(book, sheet => sheet.Name == "Rate_Analysis" && sheet.Headers.Contains("Quantity_Per_Unit"));
        Assert.Contains(book, sheet => sheet.Name == "Variations" && sheet.Headers.Contains("Selling_Value_AED"));
        Assert.Contains(book, sheet => sheet.Name == "Timesheets" && sheet.Headers.Contains("Overtime_Hours"));
        Assert.Contains(book, sheet => sheet.Name == "Retention" && sheet.Headers.Contains("Released_AED"));
        Assert.Contains(book, sheet => sheet.Name == "Supplier_Invoices" && sheet.Headers[0] == "AP_Invoice_ID");
        Assert.Empty(ErpFitOutWorkbook.Validate(book));

        var bad = book.Select(sheet => sheet.Name == "BOQ"
            ? sheet with
            {
                Records =
                [
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["BOQ_ID"] = "BOQ-9",
                        ["Quantity"] = "10",
                        ["Material_Unit_Cost_AED"] = "100",
                        ["Labour_Unit_Cost_AED"] = "0",
                        ["Equipment_Unit_Cost_AED"] = "0",
                        ["Subcontract_Unit_Cost_AED"] = "0",
                        ["Overhead_AED"] = "0",
                        ["Target_Margin_Pct"] = "20",
                        ["Direct_Cost_AED"] = "1000",
                        ["Selling_Unit_Rate_AED"] = "120",
                        ["Line_Total_AED"] = "1200",
                    },
                ],
            }
            : sheet).ToArray();
        var errors = ErpFitOutWorkbook.Validate(bad);
        Assert.Contains(errors, error => error.Contains("selling rate", StringComparison.Ordinal));

        var package = ErpFitOutWorkbook.ExportPackage();
        Assert.True(package.Length > 100);
    }

    [Fact]
    public void NewSheetsRejectBrokenRatesRetentionAndMissingPurchaseOrders()
    {
        var book = ErpFitOutWorkbook.Read(ErpFitOutWorkbook.Template()).Select(sheet => sheet.Name switch
        {
            "Rate_Analysis" => sheet with
            {
                Records =
                [
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Rate_ID"] = "RATE-1",
                        ["Quantity_Per_Unit"] = "2",
                        ["Unit_Cost_AED"] = "50",
                        ["Total_Cost_AED"] = "80",
                    },
                ],
            },
            "Variations" => sheet with
            {
                Records =
                [
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Variation_ID"] = "VAR-1",
                        ["Project_ID"] = "PRJ-1",
                        ["Status"] = "approved",
                        ["Approved_Date"] = "",
                        ["Selling_Value_AED"] = "80000",
                    },
                ],
            },
            "Projects" => sheet with
            {
                Records =
                [
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Project_ID"] = "PRJ-1",
                    },
                ],
            },
            "Retention" => sheet with
            {
                Records =
                [
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Retention_ID"] = "RET-1",
                        ["Retention_Amount_AED"] = "1000",
                        ["Released_AED"] = "1500",
                    },
                ],
            },
            "Supplier_Invoices" => sheet with
            {
                Records =
                [
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["AP_Invoice_ID"] = "AP-1",
                        ["Supplier_ID"] = "SUP-1",
                        ["PO_ID"] = "PO-MISSING",
                    },
                ],
            },
            _ => sheet,
        }).ToArray();

        var errors = ErpFitOutWorkbook.Validate(book);
        Assert.Contains(errors, error => error.Contains("quantity per unit", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("approved without an approved date", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("released amount", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("PO-MISSING", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, error => error.Contains("VAR-1 references missing Project_ID", StringComparison.Ordinal));
    }
}
