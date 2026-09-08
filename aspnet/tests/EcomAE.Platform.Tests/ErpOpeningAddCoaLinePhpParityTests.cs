using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpOpeningAddCoaLinePhpParityTests
{
    [Fact]
    public void OpeningApp_PostsNativeAddCoaLineForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpOpeningApp.razor"));
        Assert.Contains("/erp/opening/add-coa-line", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"batch_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"entity_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"debit\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"credit\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersOpeningAddCoaLineWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpOpeningAddCoaLineWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksOpeningAddCoaLineLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/opening/add-coa-line");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_erp_opening_add_line", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/opening-add-coa-line").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpOpeningAddCoaLineDryRun().Evaluate(new ErpOpeningAddCoaLineRequest());
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpOpeningAddCoaLineDryRun().Evaluate(new ErpOpeningAddCoaLineRequest(ConfirmWrites: true)).ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpOpeningAddCoaLine", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxOpeningAddCoaLine", text, StringComparison.Ordinal);
        Assert.Contains("HandleOpeningAddCoaLineAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpOpeningAddCoaLineWriteService.cs"));
        Assert.Contains("COA opening line added", service, StringComparison.Ordinal);
        Assert.Contains("Opening lines table is not provisioned", service, StringComparison.Ordinal);
        Assert.Contains("\"coa\"", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_opening_ensure_schema", service, StringComparison.Ordinal);
    }

    [Fact]
    public void RoundMoney_MatchesPhpRound2()
    {
        Assert.Equal(12.35m, ErpOpeningAddCoaLineWriteService.RoundMoney(12.346m));
        Assert.Equal(12.35m, ErpOpeningAddCoaLineWriteService.RoundMoney(12.345m));
        Assert.Equal(0m, ErpOpeningAddCoaLineWriteService.RoundMoney(0));
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
