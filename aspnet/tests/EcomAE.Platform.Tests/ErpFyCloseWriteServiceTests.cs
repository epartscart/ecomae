using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpFyCloseWriteServiceTests
{
    [Fact]
    public void ProfitClosingEntryDebitsClearingCreditsRetained()
    {
        var lines = ErpFyCloseWriteService.ClosingEntry(1234.56m);
        Assert.Equal(2, lines.Count);
        Assert.Equal(("3900", 1234.56m, 0m), (lines[0].Account, lines[0].Debit, lines[0].Credit));
        Assert.Equal(("3200", 0m, 1234.56m), (lines[1].Account, lines[1].Debit, lines[1].Credit));
        Assert.Equal("profit", ErpFyCloseWriteService.Outcome(0m));
    }

    [Fact]
    public void LossClosingEntryIsReversedWithAbsoluteAmount()
    {
        var lines = ErpFyCloseWriteService.ClosingEntry(-50.25m);
        Assert.Equal(("3200", 50.25m, 0m), (lines[0].Account, lines[0].Debit, lines[0].Credit));
        Assert.Equal(("3900", 0m, 50.25m), (lines[1].Account, lines[1].Debit, lines[1].Credit));
        Assert.Equal("loss", ErpFyCloseWriteService.Outcome(-50.25m));
    }

    [Fact]
    public void MessageMatchesPhpNumberFormat()
    {
        Assert.Equal("Year closed · profit net P&L 12,345.50", ErpFyCloseWriteService.Message(12345.5m));
        Assert.Equal("Year closed · loss net P&L -1,000.00", ErpFyCloseWriteService.Message(-1000m));
        Assert.Equal("Year-end close FY2026", ErpFyCloseWriteService.Memo("FY2026"));
    }

    [Fact]
    public void ServiceDoesNotCreateSchemaOrPostGl()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Erp/ErpFyCloseWriteService.cs"));
        Assert.DoesNotContain("CREATE TABLE", src, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_gl_journals`", src, StringComparison.Ordinal);
        Assert.Contains("SET `status`='closed' WHERE `year_id`=? AND `status`='open'", src, StringComparison.Ordinal);
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
