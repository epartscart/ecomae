using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpJwIndusImportTests
{
    [Fact]
    public void EverySampleFileValidatesCleanAgainstItsTemplate()
    {
        var samples = ErpJwIndusImportWriteService.SampleFiles();
        Assert.Equal(ErpJwIndusImportSpec.Templates.Count, samples.Count);
        foreach (var (key, csv) in samples)
        {
            var template = ErpJwIndusImportSpec.Find(key)!;
            var issues = new List<ErpJwIndusImportIssue>();
            var rows = ErpJwIndusCsv.Validate(template, csv, issues);
            Assert.True(issues.Count == 0, key + ": " + string.Join(" | ", issues.Select(i => i.Line + " " + i.Message)));
            Assert.NotEmpty(rows);
        }
    }

    [Fact]
    public void SampleChildRowsReferenceSampleParents()
    {
        var samples = ErpJwIndusImportWriteService.SampleFiles().ToDictionary(s => s.Key, s => s.Csv);
        foreach (var template in ErpJwIndusImportSpec.Templates.Where(t => t.ParentKey is not null))
        {
            var parents = ErpJwIndusCsv.Validate(ErpJwIndusImportSpec.Find(template.ParentKey)!, samples[template.ParentKey!], []).Select(r => r.SourceRef).ToHashSet();
            foreach (var row in ErpJwIndusCsv.Validate(template, samples[template.Key], []))
            {
                Assert.Contains(row.ParentRef, parents);
            }
        }
    }

    [Fact]
    public void SampleOpeningTrialBalanceBalancesAndVouchersFootToInduSPrints()
    {
        var samples = ErpJwIndusImportWriteService.SampleFiles().ToDictionary(s => s.Key, s => s.Csv);
        var coa = ErpJwIndusCsv.Validate(ErpJwIndusImportSpec.Find("coa")!, samples["coa"], []);
        Assert.Equal(coa.Sum(r => r.Dec("opening_debit")), coa.Sum(r => r.Dec("opening_credit")));

        var vouchers = ErpJwIndusCsv.Validate(ErpJwIndusImportSpec.Find("vouchers")!, samples["vouchers"], []).ToDictionary(r => r.SourceRef);
        var lines = ErpJwIndusCsv.Validate(ErpJwIndusImportSpec.Find("voucher_lines")!, samples["voucher_lines"], []);
        foreach (var group in lines.GroupBy(l => l.ParentRef))
        {
            var v = vouchers[group.Key];
            Assert.Equal(v.Dec("net_amount"), group.Sum(l => l.Dec("total_amount")));
            Assert.Equal(v.Dec("total_with_vat"), group.Sum(l => l.Dec("total_with_vat")));
        }

        // INDUS GH-5120: 1,980.00 incl VAT − 1,780.00 scrap gold = 200.00 net received.
        var gh5120 = vouchers["V-GH-POS-5120"];
        Assert.Equal(200.00m, gh5120.Dec("total_with_vat") - gh5120.Dec("old_gold_exchange"));
    }

    [Fact]
    public void ValidationRejectsBadRowsWithoutThrowing()
    {
        var template = ErpJwIndusImportSpec.Find("vouchers")!;
        var csv = "source_ref,voc_type,branch,voc_no,voc_date,net_amount,total_with_vat,bogus\n"
                  + "A,POS,HO,1,31/02/2026,10,10.5,x\n"
                  + "A,XXX,HO,abc,01/01/2026,1,1,\n"
                  + ",POS,HO,1,01/01/2026,1,1,\n";
        var issues = new List<ErpJwIndusImportIssue>();
        ErpJwIndusCsv.Validate(template, csv, issues);
        Assert.Contains(issues, i => i.Message.Contains("Unknown column 'bogus'", StringComparison.Ordinal));
        Assert.Contains(issues, i => i.Message.Contains("is not a date", StringComparison.Ordinal));
        Assert.Contains(issues, i => i.Message.Contains("Duplicate source_ref", StringComparison.Ordinal));
        Assert.Contains(issues, i => i.Message.Contains("source_ref is empty", StringComparison.Ordinal));
    }

    [Fact]
    public void CsvParserHandlesQuotesCommasAndBom()
    {
        var rows = ErpJwIndusCsv.Parse("\uFEFFa,b\r\n\"x, y\",\"say \"\"hi\"\"\"\r\n");
        Assert.Equal(2, rows.Count);
        Assert.Equal("x, y", rows[1][0]);
        Assert.Equal("say \"hi\"", rows[1][1]);
    }

    [Theory]
    [InlineData("indj_sandbox", true)]
    [InlineData("ecomae_cpw_onb1", true)]
    [InlineData("indj", false)]
    [InlineData("ecomae", false)]
    [InlineData("", false)]
    public void ImportsAreSandboxOnly(string database, bool allowed)
        => Assert.Equal(allowed, ErpJwIndusImportWriteService.IsSandboxDatabase(database));

    [Fact]
    public void AmountInWordsMatchesInduSWording()
    {
        Assert.Equal("One Thousand One Hundred U.A.E. Dirhams Only", ErpJwVoucherPrint.AmountInWords(1100m));
        Assert.Equal("Two Hundred U.A.E. Dirhams Only", ErpJwVoucherPrint.AmountInWords(200m));
        Assert.Equal("Five Hundred Fifty Seven Thousand Four Hundred Thirty Four U.A.E. Dirhams and Thirty Eight Fils Only", ErpJwVoucherPrint.AmountInWords(557434.38m));
    }

    [Fact]
    public void JewelleryGlRecipesBalance()
    {
        foreach (var recipe in new[]
                 {
                     ErpJewelleryGl.OpeningStock(18350m),
                     ErpJewelleryGl.CostOfSale(1650m),
                     ErpJewelleryGl.CostReturned(700m),
                     ErpJewelleryGl.OldGoldExchange(1780m),
                     ErpJewelleryGl.SalesReturn(1047.62m, 52.38m),
                     ErpJewelleryGl.Advance([("1000", 1000m)], 1000m, 47.62m),
                     ErpJewelleryGl.Tenders([("1000", 200m), ("1030", 50m)], "1100", tenderIsDebit: true),
                 })
        {
            Assert.Equal(recipe.Sum(l => l.Debit), recipe.Sum(l => l.Credit));
        }

        Assert.Equal("1030", ErpJewelleryGl.TenderAccount("VISA"));
        Assert.Equal("1000", ErpJewelleryGl.TenderAccount("NET"));
        Assert.Equal("1010", ErpJewelleryGl.TenderAccount("BANK TRANSFER"));
        Assert.Equal(1650m, ErpJewelleryGl.AverageCost(1, 9.78m, 1650m, 0m, 1, 9.78m));
        Assert.Equal(700m, ErpJewelleryGl.AverageCost(0, 0m, 0m, 700m, 1, 1m));
    }

    [Fact]
    public void RepoTemplatesAndFieldMapMatchTheSpec()
    {
        var dir = Path.Combine(RepoRoot(), "docs", "migration", "evidence", "jewellery", "indus_csv");
        foreach (var t in ErpJwIndusImportSpec.Templates)
        {
            var file = Path.Combine(dir, "templates", t.Step.ToString("00", System.Globalization.CultureInfo.InvariantCulture) + "_" + t.Key + ".csv");
            Assert.True(File.Exists(file), file);
            Assert.Equal(ErpJwIndusImportSpec.HeaderLine(t), File.ReadAllText(file).Trim());
        }

        var map = Path.Combine(dir, "INDUS_FIELD_MAP.csv");
        if (Environment.GetEnvironmentVariable("INDUS_FIELD_MAP_WRITE") == "1")
        {
            File.WriteAllText(map, ErpJwIndusFieldMap.Csv());
        }

        Assert.Equal(ErpJwIndusFieldMap.Csv().Replace("\r\n", "\n", StringComparison.Ordinal), File.ReadAllText(map).Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "docs", "migration", "evidence", "jewellery")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root");
    }
}
