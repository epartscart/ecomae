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
    [InlineData("Jul 16, 2026", 2026, 7, 16)]
    [InlineData("Sep 15, 2026", 2026, 9, 15)]
    [InlineData("Jun 02, 2026", 2026, 6, 2)]
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
            new CpUaeTaxItemDigest(1987, "old-id", "Newer insert, older date", "Jul 16, 2026", "Laws", "vat", false, false, 1),
            new CpUaeTaxItemDigest(1980, "new-date", "Older insert, newest date", "Sep 15, 2026", "Decisions", "einvoicing", false, false, 1),
            new CpUaeTaxItemDigest(1982, "mid", "Mid date", "Sep 01, 2026", "Laws", "corporate_tax", false, false, 1),
            new CpUaeTaxItemDigest(3158, "blank", "No date", "NA", "Laws", "general", false, false, 1),
        };

        var ordered = ErpUaeTaxIssueDate.NewestFirst(rows, item => item.IssueDate, item => item.Id);
        Assert.Equal(new[] { 1980L, 1982L, 1987L, 3158L }, ordered.Select(item => item.Id).ToArray());
        Assert.Equal("Sep 15, 2026", ordered[0].IssueDate);
    }

    [Fact]
    public void ListSql_OrdersByParsedIssueDateNotInsertId()
    {
        Assert.Contains("STR_TO_DATE(`issue_date`, '%b %d, %Y')", LegacySurfaceDashboardSql.SelectCpUaeTaxItems, StringComparison.Ordinal);
        Assert.Contains("STR_TO_DATE(`issue_date`, '%b %d, %Y')", LegacySurfaceDashboardSql.SelectCpUaeTaxItemsCategorySiblings, StringComparison.Ordinal);
        Assert.Contains("ORDER BY COALESCE(", LegacySurfaceDashboardSql.SelectCpUaeTaxItems, StringComparison.Ordinal);
        Assert.DoesNotContain("ORDER BYCOALESCE", LegacySurfaceDashboardSql.SelectCpUaeTaxItems, StringComparison.Ordinal);
        Assert.DoesNotContain("ORDER BY `id` DESC\n        LIMIT @limit", LegacySurfaceDashboardSql.SelectCpUaeTaxItems, StringComparison.Ordinal);
    }
}
