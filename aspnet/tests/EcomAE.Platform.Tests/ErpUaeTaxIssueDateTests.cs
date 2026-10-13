using System.Linq;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpUaeTaxIssueDateTests
{
    [Theory]
    [InlineData("01 Jan 2018", 2018, 1, 1)]
    [InlineData("23 Nov 2017", 2017, 11, 23)]
    [InlineData("01 January 2026", 2026, 1, 1)]
    [InlineData("2025-12-31", 2025, 12, 31)]
    [InlineData("15/03/2024", 2024, 3, 15)]
    public void TryParse_ReadsFtaDisplayDates(string raw, int year, int month, int day)
    {
        Assert.True(ErpUaeTaxIssueDate.TryParse(raw, out var value));
        Assert.Equal(new DateTime(year, month, day), value.Date);
    }

    [Fact]
    public void NewestFirst_PutsLatestIssueDateFirst()
    {
        var rows = new[]
        {
            new CpUaeTaxItemDigest(30, "old-id", "Older insert, older date", "01 Jan 2018", "Laws", "vat", false, false, 1),
            new CpUaeTaxItemDigest(10, "new-date", "Newer date, older id", "01 Jan 2026", "Decisions", "einvoicing", false, false, 1),
            new CpUaeTaxItemDigest(20, "mid", "Mid date", "01 Dec 2022", "Laws", "corporate_tax", false, false, 1),
            new CpUaeTaxItemDigest(40, "blank", "No date", "", "Laws", "general", false, false, 1),
        };

        var ordered = ErpUaeTaxIssueDate.NewestFirst(rows, item => item.IssueDate, item => item.Id);
        Assert.Equal(new[] { 10L, 20L, 30L, 40L }, ordered.Select(item => item.Id).ToArray());
        Assert.Equal("01 Jan 2026", ordered[0].IssueDate);
    }

    [Fact]
    public void ListSql_OrdersByParsedIssueDateNotInsertId()
    {
        Assert.Contains(ErpUaeTaxIssueDate.NewestFirstSql, LegacySurfaceDashboardSql.SelectCpUaeTaxItems, StringComparison.Ordinal);
        Assert.Contains(ErpUaeTaxIssueDate.NewestFirstSql, LegacySurfaceDashboardSql.SelectCpUaeTaxItemsCategorySiblings, StringComparison.Ordinal);
        Assert.DoesNotContain("ORDER BY `id` DESC\n        LIMIT @limit", LegacySurfaceDashboardSql.SelectCpUaeTaxItems, StringComparison.Ordinal);
    }
}
