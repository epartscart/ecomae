using Xunit;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Tests;

public sealed class ErpPeriodReadServiceTests
{
    [Fact]
    public void RecentYearMonthsMatchPhpMinusMonths()
    {
        var now = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(new[] { "2026-01", "2025-12", "2025-11" }, ErpPeriodReadService.RecentYearMonths(now));
    }

    [Fact]
    public void SeverityCountsOnlyNonZeroItems()
    {
        var (b, w) = ErpPeriodReadService.CountSeverities(
        [
            new("a", "A", 0, "blocker", ""),
            new("b", "B", 2, "blocker", ""),
            new("c", "C", 1, "warning", ""),
            new("d", "D", 5, "info", ""),
        ]);
        Assert.Equal((1, 1), (b, w));
    }

    [Fact]
    public void DecodeChecklistFallsBackToEmptyArray()
    {
        Assert.Equal(System.Text.Json.JsonValueKind.Array, ErpPeriodReadService.DecodeChecklist(null).ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, ErpPeriodReadService.DecodeChecklist("not json").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, ErpPeriodReadService.DecodeChecklist("5").ValueKind);
        Assert.Equal(1, ErpPeriodReadService.DecodeChecklist("[{\"id\":\"x\"}]").GetArrayLength());
    }

    [Fact]
    public void ServiceDoesNotCreateSchema()
    {
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPeriodReadService.cs"));
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.Contains("ORDER BY `year_month` DESC LIMIT", service, StringComparison.Ordinal);
        Assert.Contains("MAX(`lock_date`)", service, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException(relative);
    }
}
