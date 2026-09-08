using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPrjaTxnAddPhpParityTests
{
    [Fact]
    public void ProjectAccountingApp_PostsNativeTxnForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpProjectAccountingApp.razor"));
        Assert.Contains("action=\"/erp/project-accounting/txns/add\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"project_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"txn_type\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"amount\"", text, StringComparison.Ordinal);
        Assert.Contains("Post transaction", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersPrjaTxnAddWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPrjaTxnAddWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksPrjaTxnAddLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/project-accounting/txns/add");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_prja_txn_add", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/prja-txn-add").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpPrjaTxnAddDryRun().Evaluate(new ErpPrjaTxnAddRequest(ProjectId: 1, TxnType: "cost", Amount: 10));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal("cost", new ErpPrjaTxnAddDryRun().Evaluate(new ErpPrjaTxnAddRequest(ProjectId: 1)).TxnType);
        Assert.Equal("Invalid project transaction type", new ErpPrjaTxnAddDryRun().Evaluate(new ErpPrjaTxnAddRequest(TxnType: "bogus")).Detail);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpPrjaTxnAddDryRun().Evaluate(new ErpPrjaTxnAddRequest(ConfirmWrites: true, TxnType: "cost")).ValidationCode);
    }

    [Fact]
    public void Validate_MatchesPhp()
    {
        Assert.Null(ErpPrjaTxnAddWriteService.Validate("cost"));
        Assert.Null(ErpPrjaTxnAddWriteService.Validate("revenue"));
        Assert.Null(ErpPrjaTxnAddWriteService.Validate("billing"));
        Assert.Equal("Invalid project transaction type", ErpPrjaTxnAddWriteService.Validate("bogus"));
        Assert.Equal("Invalid project transaction type", ErpPrjaTxnAddWriteService.Validate(""));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpProjectAccountingTxnsAdd", text, StringComparison.Ordinal);
        Assert.Contains("HandlePrjaTxnAddAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPrjaTxnAddWriteService.cs"));
        Assert.Contains("Transaction posted", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
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
