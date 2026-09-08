using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCtrSignPhpParityTests
{
    [Fact]
    public void ContractsApp_PostsNativeSignForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpContractsApp.razor"));
        Assert.Contains("/erp/contracts/sign", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"contract_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"signer_name\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"signer_email\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("writes=0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersCtrSignWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpCtrSignWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksCtrSignLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/contracts/sign");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_ctr_sign", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/ctr-sign").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpCtrSignDryRun().Evaluate(new ErpCtrSignRequest());
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpCtrSignDryRun().Evaluate(new ErpCtrSignRequest(ConfirmWrites: true)).ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpCtrSign", text, StringComparison.Ordinal);
        Assert.Contains("HandleCtrSignAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpCtrSignWriteService.cs"));
        Assert.Contains("Signer name is required", service, StringComparison.Ordinal);
        Assert.Contains("Contract not found", service, StringComparison.Ordinal);
        Assert.Contains("Signed — ", service, StringComparison.Ordinal);
        Assert.Contains("SHA256", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_ctr_ensure_schema", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Hash_MatchesPhpSha256Hex()
    {
        Assert.Equal(
            "e3b0c44298fc1c149afbf4c8996fb924",
            ErpCtrSignWriteService.Sha256Hex("").Substring(0, 32));
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
