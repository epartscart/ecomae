using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpJewelMetalTests
{
    [Fact]
    public void PureGoldUsesNetWeightAndPurityPerThousand()
    {
        var net = ErpJewelCalc.NetMetal(12.500m, 1.200m, 0m);
        Assert.Equal(11.3000m, net);
        Assert.Equal(10.3508m, ErpJewelCalc.PureMetal(net, 916));
        Assert.Equal(0.3000m, ErpJewelCalc.StoneGrams(1.5m));
        Assert.False(ErpJewelCalc.CanSell("Sold"));
        Assert.True(ErpJewelCalc.CanSell("In Stock"));
        Assert.False(ErpJewelCalc.CountsAsCompanyStock("On Approval"));
        Assert.True(ErpJewelCalc.CountsAsCompanyStock("In Stock"));
    }

    [Fact]
    public void ExchangeKeepsTheSaleAndTheBuybackApart()
    {
        var (sale, buyback, cash) = ErpJewelCalc.Exchange(8000, 3000);
        Assert.Equal(8000m, sale);
        Assert.Equal(3000m, buyback);
        Assert.Equal(5000m, cash);
    }

    [Fact]
    public void MeltVarianceDoesNotReplaceTheInputWeight()
    {
        var input = 91.6m;
        var variance = ErpJewelCalc.MeltVariance(91.54m, 0m, input);
        Assert.Equal(-0.0600m, variance);
        Assert.Equal(91.6m, input);
        Assert.True(ErpJewelCalc.MeltNeedsApproval(variance, ErpJewelCalc.DefaultToleranceGrams));
        Assert.False(ErpJewelCalc.MeltNeedsApproval(-0.005m, ErpJewelCalc.DefaultToleranceGrams));
    }

    [Fact]
    public void WorkbookMatchesTheReferenceTagAndRejectsABadMelt()
    {
        var book = ErpJewelWorkbook.Read(ErpJewelWorkbook.Template());
        Assert.Equal(33, book.Count);
        var tag = book.Single(sheet => sheet.Name == "Items_Tags").Records[0];
        Assert.Equal("12.5", tag["Gross_Gram"]);
        Assert.Equal("10.3508", tag["Pure_Gram"]);
        Assert.Equal("46310", book.Single(sheet => sheet.Name == "Melting_Refining").Records[0]["Date"]);
        var pos = book.Single(sheet => sheet.Name == "POS_Lines").Records[0];
        Assert.Equal("4882.5", pos["Line_Total_AED"]);
        Assert.Equal(4882.5m, ErpJewelCalc.PosLineTotal(4300, 350, 0, 0, 232.5m));
        Assert.Contains(book.Single(sheet => sheet.Name == "START_HERE").Records, row => row.Values.Any(value => value.Contains("Grams for metals", StringComparison.Ordinal)));
        Assert.Empty(ErpJewelWorkbook.Validate(book));

        var bad = book.Select(sheet => sheet.Name == "Melting_Refining"
            ? sheet with
            {
                Records =
                [
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Melt_ID"] = "MELT-9",
                        ["Input_Pure_Gram"] = "91.6",
                        ["Output_Gross_Gram"] = "92",
                        ["Output_Purity_PPT"] = "995",
                        ["Output_Pure_Gram"] = "91.54",
                        ["Pure_Variance_Gram"] = "-0.06",
                        ["Approved_By"] = "",
                    },
                ],
            }
            : sheet).ToArray();
        var errors = ErpJewelWorkbook.Validate(bad);
        Assert.Contains(errors, error => error.Contains("needs approval", StringComparison.Ordinal));
        Assert.True(ErpJewelWorkbook.ExportPackage().Length > 100);
    }

    [Fact]
    public void ATagCannotBeSoldTwiceAndAPaymentCannotExceedTheSale()
    {
        var book = ErpJewelWorkbook.Read(ErpJewelWorkbook.Template()).Select(sheet => sheet.Name switch
        {
            "POS_Lines" => sheet with
            {
                Records =
                [
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Sale_ID"] = "SALE-001",
                        ["Line_ID"] = "1",
                        ["Tag_ID"] = "TAG-0001",
                        ["Metal_Value_AED"] = "4300",
                        ["Making_AED"] = "350",
                        ["Stone_Value_AED"] = "0",
                        ["Discount_AED"] = "0",
                        ["VAT_AED"] = "232.5",
                        ["Line_Total_AED"] = "4882.5",
                    },
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Sale_ID"] = "SALE-002",
                        ["Line_ID"] = "1",
                        ["Tag_ID"] = "TAG-0001",
                        ["Metal_Value_AED"] = "100",
                        ["Making_AED"] = "0",
                        ["Stone_Value_AED"] = "0",
                        ["Discount_AED"] = "0",
                        ["VAT_AED"] = "0",
                        ["Line_Total_AED"] = "100",
                    },
                ],
            },
            "POS_Sales" => sheet with
            {
                Records =
                [
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Sale_ID"] = "SALE-001",
                        ["Branch_ID"] = "DXB01",
                        ["Customer_ID"] = "CUST-001",
                        ["Subtotal_AED"] = "4650",
                        ["VAT_AED"] = "232.5",
                        ["Total_AED"] = "4882.5",
                    },
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Sale_ID"] = "SALE-002",
                        ["Branch_ID"] = "DXB01",
                        ["Customer_ID"] = "CUST-001",
                        ["Subtotal_AED"] = "100",
                        ["VAT_AED"] = "0",
                        ["Total_AED"] = "100",
                    },
                ],
            },
            "POS_Payments" => sheet with
            {
                Records =
                [
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Payment_ID"] = "PAY-9",
                        ["Sale_ID"] = "SALE-002",
                        ["Amount_AED"] = "250",
                    },
                ],
            },
            "Returns" => sheet with { Records = [] },
            _ => sheet,
        }).ToArray();

        var errors = ErpJewelWorkbook.Validate(book);
        Assert.Contains(errors, error => error.Contains("sold on more than one sale", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("payments exceed", StringComparison.Ordinal));
    }
}
