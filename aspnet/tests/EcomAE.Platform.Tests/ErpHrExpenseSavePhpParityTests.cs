using System.Reflection;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards /erp/ajax/hr-expense-save live writes without inventing schema ensure.</summary>
public sealed class ErpHrExpenseSavePhpParityTests
{
    [Fact]
    public void HrOverviewApp_EmitsExpenseSaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpHrOverviewApp.razor"));
        Assert.Contains("/erp/ajax/hr-expense-save", text, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/hr-expense-status", text, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
        Assert.Contains("Submit claim", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Open PHP reference", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgramAndRoutes_RegisterExpenseSaveWrite()
    {
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("ErpAjaxHrExpenseSave", routes, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/hr-expense-save", routes, StringComparison.Ordinal);
        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpHrExpenseSaveWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("IErpHrExpenseSaveWriteService", module, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_KeepsWriteLiveGatedStatus()
    {
        var catalog = SurfacePayloadContractCatalog.Functions;
        var write = catalog.First(item => item.AspNetRouteOrCapability.Contains("/erp/ajax/hr-expense-save", StringComparison.Ordinal));
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_hr_expense_save", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Defaults_MatchPhp()
    {
        Assert.Equal("Expense claim saved — 42.00 AED", ErpHrExpenseSaveWriteService.FormatSavedMessage(42));
        Assert.Equal("Expense claim saved — 1,250.50 AED", ErpHrExpenseSaveWriteService.FormatSavedMessage(1250.5m));
        var skipped = ErpHrExpenseSaveWriteService.NormalizeLines(
        [
            new ErpHrExpenseLine("zero", 0),
            new ErpHrExpenseLine("taxi", 12.5m)
        ]);
        Assert.Single(skipped);
        Assert.Equal("taxi", skipped[0].Label);
        Assert.Equal(12.5m, skipped[0].Amount);
        var fromJson = ErpHrExpenseSaveWriteService.ParseLines(
            null,
            """[{"label":"hotel","amount":80}]""",
            null);
        Assert.Equal("hotel", Assert.Single(fromJson).Label);
    }

    [Fact]
    public void DryRun_RequiresEmployeeLineAndRefusesConfirm()
    {
        var ok = new ErpHrExpenseSaveDryRun().Evaluate(new ErpHrExpenseSaveRequest(9, "Taxi", 1));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(0, ok.Writes);
        var missingEmp = new ErpHrExpenseSaveDryRun().Evaluate(new ErpHrExpenseSaveRequest(0, "Taxi", 1));
        Assert.Equal("invalid_request", missingEmp.ValidationCode);
        var missingLine = new ErpHrExpenseSaveDryRun().Evaluate(new ErpHrExpenseSaveRequest(9, "Taxi", 0));
        Assert.Equal("invalid_request", missingLine.ValidationCode);
        var refused = new ErpHrExpenseSaveDryRun().Evaluate(new ErpHrExpenseSaveRequest(9, "Taxi", 1, true));
        Assert.Equal("confirm_writes_refused", refused.ValidationCode);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var alt = Path.GetFullPath(Path.Combine(dir.FullName, "..", "..", "..", "..", "..", relative));
            if (File.Exists(alt))
            {
                return alt;
            }

            dir = dir.Parent;
        }

        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var rooted = Path.GetFullPath(Path.Combine(asmDir, "..", "..", "..", "..", "..", relative));
        Assert.True(File.Exists(rooted), $"Missing repo file: {relative}");
        return rooted;
    }
}
