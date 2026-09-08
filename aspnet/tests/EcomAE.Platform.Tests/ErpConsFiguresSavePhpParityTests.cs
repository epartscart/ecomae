using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_cons_figures_save</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpConsFiguresSavePhpParityTests
{
    [Fact]
    public void ConsolidationsApp_PostsNativeFiguresForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpConsolidationsApp.razor"));
        Assert.Contains("action=\"/erp/consolidations/figures/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"entity_code\"", text, StringComparison.Ordinal);
        Assert.Contains("Save financials", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersConsFiguresSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpConsFiguresSaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpConsFiguresSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksConsFiguresSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/consolidations/figures/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_cons_figures_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/cons-figures-save");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_RequiresEntityAndRefusesConfirm()
    {
        var ok = new ErpConsFiguresSaveDryRun().Evaluate(new ErpConsFiguresSaveRequest("SUB1"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var missing = new ErpConsFiguresSaveDryRun().Evaluate(new ErpConsFiguresSaveRequest());
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("Entity is required", missing.Detail);

        var confirm = new ErpConsFiguresSaveDryRun().Evaluate(new ErpConsFiguresSaveRequest("SUB1", true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpConsolidationsFiguresSave", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxConsFiguresSave", text, StringComparison.Ordinal);
        Assert.Contains("IErpConsFiguresSaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleConsFiguresSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpConsFiguresSaveWriteService.cs"));
        Assert.Contains("Financials saved for", service, StringComparison.Ordinal);
        Assert.Contains("Consolidation figures table is not provisioned", service, StringComparison.Ordinal);
        Assert.Contains("Entity is required", service, StringComparison.Ordinal);
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
