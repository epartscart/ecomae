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
        Assert.Equal(20, book.Count);
        var tag = book.Single(sheet => sheet.Name == "Items_Tags").Records[0];
        Assert.Equal("12.5", tag["Gross_Gram"]);
        Assert.Equal("10.3508", tag["Pure_Gram"]);
        Assert.Equal("46310", book.Single(sheet => sheet.Name == "Melting_Refining").Records[0]["Date"]);
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
}
