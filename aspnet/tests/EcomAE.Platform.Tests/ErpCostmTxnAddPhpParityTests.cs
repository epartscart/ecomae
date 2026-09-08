using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCostmTxnAddPhpParityTests
{
    [Fact]
    public void CostModelsApp_PostsNativeTxnForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpCostModelsApp.razor"));
        Assert.Contains("action=\"/erp/cost-models/txns/add\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"item_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"txn_type\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"qty\"", text, StringComparison.Ordinal);
        Assert.Contains("Add transaction", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersCostmTxnAddWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpCostmTxnAddWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksCostmTxnAddLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/cost-models/txns/add");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_costm_txn_add", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/costm-txn-add").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpCostmTxnAddDryRun().Evaluate(new ErpCostmTxnAddRequest(ItemId: 9001, TxnType: "receipt", Qty: 10));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal("receipt", new ErpCostmTxnAddDryRun().Evaluate(new ErpCostmTxnAddRequest(ItemId: 1)).TxnType);
        Assert.Equal("Invalid transaction type", new ErpCostmTxnAddDryRun().Evaluate(new ErpCostmTxnAddRequest(TxnType: "bogus")).Detail);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpCostmTxnAddDryRun().Evaluate(new ErpCostmTxnAddRequest(ConfirmWrites: true, TxnType: "receipt")).ValidationCode);
    }

    [Fact]
    public void Validate_MatchesPhp()
    {
        Assert.Null(ErpCostmTxnAddWriteService.Validate("receipt"));
        Assert.Null(ErpCostmTxnAddWriteService.Validate("issue"));
        Assert.Equal("Invalid transaction type", ErpCostmTxnAddWriteService.Validate("bogus"));
        Assert.Equal("Invalid transaction type", ErpCostmTxnAddWriteService.Validate(""));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpCostModelsTxnsAdd", text, StringComparison.Ordinal);
        Assert.Contains("HandleCostmTxnAddAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpCostmTxnAddWriteService.cs"));
        Assert.Contains("Transaction added", service, StringComparison.Ordinal);
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
