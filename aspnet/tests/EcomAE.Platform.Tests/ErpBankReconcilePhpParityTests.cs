using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpBankReconcilePhpParityTests
{
    [Fact]
    public void BankReconApp_PostsNativeMatchForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpBankReconciliationApp.razor"));
        Assert.Contains("/erp/bank-reconciliation/match", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"line_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"entry_id\"", text, StringComparison.Ordinal);
        Assert.Contains("Match statement line", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersBankReconcileWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpBankReconcileWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksBankReconcileLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/bank-reconciliation/match");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_erp_bank_reconcile_match", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/bank-reconcile").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpBankReconcileDryRun().Evaluate(new ErpBankReconcileRequest());
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpBankReconcileDryRun().Evaluate(new ErpBankReconcileRequest(true)).ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpBankReconcile", text, StringComparison.Ordinal);
        Assert.Contains("HandleBankReconcileAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpBankReconcileWriteService.cs"));
        Assert.Contains("Bank line matched to cash entry", service, StringComparison.Ordinal);
        Assert.Contains("Invalid match", service, StringComparison.Ordinal);
        Assert.Contains("Marked reconciled", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_phase8_ensure_schema", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_bank_statement_import", service, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
            var alt = Path.GetFullPath(Path.Combine(dir.FullName, "..", "..", "..", "..", "..", relative));
            if (File.Exists(alt)) return alt;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Could not locate " + relative);
    }
}
