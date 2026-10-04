using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpFixedAssetWriteServicePhpParityTests
{
    [Fact]
    public void PeriodNormalizationMatchesPhp()
    {
        Assert.Equal("2026-03", ErpFixedAssetWriteService.NormalizePeriod(" 2026-03x"));
        var ex = Assert.Throws<ErpWriteException>(() => ErpFixedAssetWriteService.NormalizePeriod("2026-3"));
        Assert.Equal("Period must be YYYY-MM", ex.Message);
    }

    [Fact]
    public void MethodDefaultsToStraightLine()
    {
        Assert.Equal("straight_line", ErpFixedAssetWriteService.NormalizeMethod("bogus"));
        Assert.Equal("double_declining", ErpFixedAssetWriteService.NormalizeMethod("double_declining"));
        Assert.Equal(4, ErpFixedAssetWriteService.DepreciationMethods.Count);
    }

    [Fact]
    public void DepreciationAmountsMatchPhpFormulas()
    {
        Assert.Equal(100m, ErpFixedAssetWriteService.PeriodDepreciation(6600m, 600m, 0m, 60, "straight_line"));
        Assert.Equal(220m, ErpFixedAssetWriteService.PeriodDepreciation(6600m, 600m, 0m, 60, "declining_balance"));
        Assert.Equal(440m, ErpFixedAssetWriteService.PeriodDepreciation(6600m, 600m, 0m, 60, "double_declining"));
        Assert.Equal(50m, ErpFixedAssetWriteService.PeriodDepreciation(6600m, 600m, 5950m, 60, "straight_line"));
        Assert.Equal(0m, ErpFixedAssetWriteService.PeriodDepreciation(6600m, 600m, 6000m, 60, "straight_line"));
        Assert.Equal(1000m, ErpFixedAssetWriteService.PeriodDepreciation(1000m, 0m, 0m, 0, "straight_line"));
    }

    [Fact]
    public void MessagesAndAccountsMatchPhp()
    {
        Assert.Equal("Depreciation posted — 1,234.50 AED", ErpFixedAssetWriteService.PostedMessage(1234.5m));
        Assert.Equal("5100", ErpFixedAssetWriteService.ExpenseCode);
        Assert.Equal("1550", ErpFixedAssetWriteService.AccumCode);
    }

    [Fact]
    public void ServiceMirrorsPhpSchemaAndGlOrdering()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Erp/ErpFixedAssetWriteService.cs"));
        Assert.Contains("Depreciation already posted for ", src, StringComparison.Ordinal);
        Assert.Contains("Asset code, name and cost required", src, StringComparison.Ordinal);
        Assert.Contains("`status` = 'fully_depreciated'", src, StringComparison.Ordinal);
        Assert.Contains("SourceType = \"adjustment\"", src, StringComparison.Ordinal);
        Assert.True(src.IndexOf("tx.CommitAsync", StringComparison.Ordinal) < src.IndexOf("_gl.PostJournalAsync", StringComparison.Ordinal));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
