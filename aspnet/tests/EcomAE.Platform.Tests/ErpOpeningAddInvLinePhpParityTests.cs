using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpOpeningAddInvLinePhpParityTests
{
    [Fact]
    public void OpeningApp_PostsNativeAddInvLineForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpOpeningApp.razor"));
        Assert.Contains("/erp/opening/add-inv-line", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"batch_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"item_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"qty\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"unit_cost\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"warehouse_id\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersOpeningAddInvLineWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpOpeningAddInvLineWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksOpeningAddInvLineLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/opening/add-inv-line");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_erp_opening_add_line", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/opening-add-inv-line").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpOpeningAddInvLineDryRun().Evaluate(new ErpOpeningAddInvLineRequest());
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpOpeningAddInvLineDryRun().Evaluate(new ErpOpeningAddInvLineRequest(ConfirmWrites: true)).ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpOpeningAddInvLine", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxOpeningAddInvLine", text, StringComparison.Ordinal);
        Assert.Contains("HandleOpeningAddInvLineAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpOpeningAddInvLineWriteService.cs"));
        Assert.Contains("Inventory opening line added", service, StringComparison.Ordinal);
        Assert.Contains("Opening lines table is not provisioned", service, StringComparison.Ordinal);
        Assert.Contains("\"inventory\"", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_opening_ensure_schema", service, StringComparison.Ordinal);
    }

    [Fact]
    public void EncodeMeta_MatchesPhpJsonKeys()
    {
        Assert.Equal(
            "{\"warehouse_id\":3,\"batch_no\":\"B1\",\"expiry_date\":\"2026-09-08\"}",
            ErpOpeningAddInvLineWriteService.EncodeMeta(3, "B1", "2026-09-08"));
        Assert.Equal(
            "{\"warehouse_id\":0,\"batch_no\":\"\",\"expiry_date\":\"\"}",
            ErpOpeningAddInvLineWriteService.EncodeMeta(0, null, null));
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
