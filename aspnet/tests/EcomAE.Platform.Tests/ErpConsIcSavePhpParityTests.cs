using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_cons_ic_save</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpConsIcSavePhpParityTests
{
    [Fact]
    public void ConsolidationsApp_PostsNativeIcSaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpConsolidationsApp.razor"));
        Assert.Contains("action=\"/erp/consolidations/ic/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"from_entity\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"to_entity\"", text, StringComparison.Ordinal);
        Assert.Contains("Record IC", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersConsIcSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpConsIcSaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpConsIcSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksConsIcSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/consolidations/ic/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_cons_ic_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/cons-ic-save");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_RequiresEntitiesAmountAndRefusesConfirm()
    {
        var ok = new ErpConsIcSaveDryRun().Evaluate(new ErpConsIcSaveRequest("HOME", "SUB1", 100));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var missing = new ErpConsIcSaveDryRun().Evaluate(new ErpConsIcSaveRequest());
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("From and to entities are required", missing.Detail);

        var same = new ErpConsIcSaveDryRun().Evaluate(new ErpConsIcSaveRequest("HOME", "home", 100));
        Assert.Equal("Intercompany needs two different entities", same.Detail);

        var zero = new ErpConsIcSaveDryRun().Evaluate(new ErpConsIcSaveRequest("HOME", "SUB1", 0));
        Assert.Equal("Amount must be positive", zero.Detail);

        var confirm = new ErpConsIcSaveDryRun().Evaluate(new ErpConsIcSaveRequest("HOME", "SUB1", 100, true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpConsolidationsIcSave", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxConsIcSave", text, StringComparison.Ordinal);
        Assert.Contains("IErpConsIcSaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleConsIcSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpConsIcSaveWriteService.cs"));
        Assert.Contains("Intercompany transaction recorded", service, StringComparison.Ordinal);
        Assert.Contains("Consolidation IC table is not provisioned", service, StringComparison.Ordinal);
        Assert.Contains("From and to entities are required", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
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

        throw new FileNotFoundException("Could not locate " + relative);
    }
}
