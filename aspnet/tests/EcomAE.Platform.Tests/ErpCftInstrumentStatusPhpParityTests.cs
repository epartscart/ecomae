using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCftInstrumentStatusPhpParityTests
{
    [Fact]
    public void BankReconApp_PostsNativeStatusForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpBankReconciliationApp.razor"));
        Assert.Contains("/erp/bank-instruments/status", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"status\"", text, StringComparison.Ordinal);
        Assert.Contains("Advance status", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersCftInstrumentStatusWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpCftInstrumentStatusWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksCftInstrumentStatusLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/bank-instruments/status");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_cft_instrument_set_status", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/cft-instrument-status").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpCftInstrumentStatusDryRun().Evaluate(new ErpCftInstrumentStatusRequest(Id: 1, TargetStatus: "issued"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpCftInstrumentStatusDryRun().Evaluate(new ErpCftInstrumentStatusRequest(ConfirmWrites: true, Id: 1)).ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpBankInstrumentStatus", text, StringComparison.Ordinal);
        Assert.Contains("HandleCftInstrumentStatusAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpCftInstrumentStatusWriteService.cs"));
        Assert.Contains("Instrument moved to ", service, StringComparison.Ordinal);
        Assert.Contains("Instrument not found", service, StringComparison.Ordinal);
        Assert.Contains("Cannot move instrument from ", service, StringComparison.Ordinal);
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
