using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpJwIfrsReconciliationTests
{
    private static decimal Dr(IEnumerable<ErpAutoPartsIfrs.JournalLine> l) => l.Sum(x => x.Debit);
    private static decimal Cr(IEnumerable<ErpAutoPartsIfrs.JournalLine> l) => l.Sum(x => x.Credit);

    [Fact]
    public void ReverseChargeSelfAccountsEqualInputAndOutputVat()
    {
        // Gold bar 100 g × 300 = 30,000 metal; 5% RCM = 1,500 (Dr 1150 / Cr 2100).
        var lines = ErpJewelleryGl.ReverseCharge(1500m);
        Assert.Equal(2, lines.Count);
        Assert.Equal(Dr(lines), Cr(lines));
        Assert.Contains(lines, l => l.Code == ErpAutoPartsIfrs.InputVatAccount && l.Debit == 1500m);
        Assert.Contains(lines, l => l.Code == ErpAutoPartsIfrs.VatAccount && l.Credit == 1500m);
        Assert.Empty(ErpJewelleryGl.ReverseCharge(0m));
    }

    [Fact]
    public void PurchaseIsCapitalisedAtTheStockCostNotTheVatBase()
    {
        // Metal 38,211.75 + making 5,000 entered stock; header net held only the 5,000 VAT base.
        var cost = ErpJewelleryGl.PurchaseInventoryCost(43211.75m, 5000m);
        Assert.Equal(43211.75m, cost);
        var journal = ErpAutoPartsIfrs.PurchaseReceived(cost, 250m);
        Assert.Equal(Dr(journal), Cr(journal));
        Assert.Contains(journal, l => l.Code == ErpAutoPartsIfrs.InventoryAccount && l.Debit == 43211.75m);
        Assert.Contains(journal, l => l.Code == ErpAutoPartsIfrs.ApAccount && l.Credit == 43461.75m);
        Assert.Equal(5000m, ErpJewelleryGl.PurchaseInventoryCost(0m, 5000m));
    }

    [Fact]
    public void JewelleryJournalRecipesAllBalance()
    {
        var recipes = new[]
        {
            ErpAutoPartsIfrs.SaleInvoiced(1885.71m, 94.29m),
            ErpJewelleryGl.CostOfSale(1650m),
            ErpJewelleryGl.CostReturned(700m),
            ErpJewelleryGl.OldGoldExchange(1780m),
            ErpJewelleryGl.SalesReturn(1047.62m, 52.38m),
            ErpJewelleryGl.Advance([(ErpJewelleryGl.CashAccount, 1000m)], 1000m, 47.62m),
            ErpJewelleryGl.OpeningStock(5000m),
        };
        foreach (var r in recipes)
        {
            Assert.Equal(Dr(r), Cr(r));
        }
    }

    [Fact]
    public void LedgerChecksPassForBalancedBooksAndFlagDifferences()
    {
        // Opening capital 10,000 cash; sale 1,050 incl. VAT 50; COGS 600.
        var accounts = new List<ErpJwIfrsAccount>
        {
            new("1000", "Cash", "asset", 10000m, 1050m, 0m),
            new("1300", "Inventory", "asset", 0m, 0m, 0m),
            new("2100", "VAT output", "liability", 0m, 0m, 50m),
            new("3000", "Capital", "equity", -10600m, 0m, 0m),
            new("4000", "Revenue", "revenue", 0m, 0m, 1000m),
            new("5000", "COGS", "expense", 0m, 600m, 0m),
            new("1310", "Stock", "asset", 600m, 0m, 600m),
        };
        Assert.All(ErpJwIfrsRules.LedgerChecks(accounts, 0), c => Assert.True(c.Passed, c.Check));
        var bad = ErpJwIfrsRules.LedgerChecks(accounts.Append(new("1100", "AR", "asset", 0m, 5m, 0m)).ToList(), 1);
        Assert.Contains(bad, c => !c.Passed && c.Check.Contains("debits ≠ credits", StringComparison.Ordinal));
        Assert.Contains(bad, c => !c.Passed && c.Area == "Trial balance");
    }

    [Fact]
    public void CashFlowSectionsFollowIas7()
    {
        Assert.Equal("Operating", ErpJwIfrsRules.CashFlowSection("4000", "revenue"));
        Assert.Equal("Operating", ErpJwIfrsRules.CashFlowSection("2000", "liability"));
        Assert.Equal("Investing", ErpJwIfrsRules.CashFlowSection("1600", "asset"));
        Assert.Equal("Financing", ErpJwIfrsRules.CashFlowSection("3000", "equity"));
        Assert.Equal("Financing", ErpJwIfrsRules.CashFlowSection("2600", "liability"));
        var cf = ErpJwIfrsRules.CashFlow([("Operating", "4000 Sales", 100m), ("Operating", "4000 Sales", 50m), ("Financing", "3000 Capital", 0m)]);
        Assert.Single(cf);
        Assert.Equal(150m, cf[0].Amount);
    }

    [Fact]
    public void EclMatrixAgesReceivables()
    {
        var b = ErpJwIfrsRules.Ecl([(10, 1000m), (45, 2000m), (400, 100m), (5, -50m)]);
        Assert.Equal(1000m, b[0].Exposure);
        Assert.Equal(10m, b[0].Allowance);
        Assert.Equal(60m, b[1].Allowance);
        Assert.Equal(50m, b[4].Allowance);
    }

    [Fact]
    public void NrvIndicatorUsesMetalContentLessCostsToSell()
    {
        // 10 g of 22K (0.916) at 24K rate 400, 2% cost to sell: NRV 3,590.72; carrying 4,000 → 409.28.
        var n = ErpJwIfrsRules.Nrv("22K-RING", "22K", 0.916m, 10m, 4000m, 400m, 0.02m);
        Assert.Equal(3590.72m, n.MetalNrv);
        Assert.Equal(409.28m, n.Shortfall);
        Assert.Equal(0m, ErpJwIfrsRules.Nrv("X", "24K", 1m, 10m, 100m, 400m, 0.02m).Shortfall);
    }
}
