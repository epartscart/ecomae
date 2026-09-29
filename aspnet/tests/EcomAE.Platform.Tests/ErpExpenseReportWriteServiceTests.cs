using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpExpenseReportWriteServiceTests
{
    [Theory]
    [InlineData(null, "EXP-00001")]
    [InlineData("", "EXP-00001")]
    [InlineData("EXP-00001", "EXP-00002")]
    [InlineData("EXP-00099", "EXP-00100")]
    public void ReportNumbersMatchPhpSequence(string? previous, string expected)
    {
        Assert.Equal(expected, ErpExpenseReportWriteService.NextReportNo(previous));
    }

    [Fact]
    public void SourceRegistersAndUsesLiveWriteService()
    {
        var root = FindRepoRoot();
        var program = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Program.cs"));
        var module = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        var service = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Erp/ErpExpenseReportWriteService.cs"));

        Assert.Contains("IErpExpenseReportWriteService", program, StringComparison.Ordinal);
        Assert.Contains("IErpExpenseReportWriteService", module, StringComparison.Ordinal);
        Assert.Contains("epc_erp_expense_reports", service, StringComparison.Ordinal);
        Assert.Contains("\"submitted\"", service, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "aspnet", "EcomAE.AspNetCore.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
