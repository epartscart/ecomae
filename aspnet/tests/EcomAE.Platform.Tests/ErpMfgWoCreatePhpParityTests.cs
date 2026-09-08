using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_mfg_wo_create</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpMfgWoCreatePhpParityTests
{
    [Fact]
    public void ProductionApp_PostsNativeWoCreateForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpProductionOverviewApp.razor"));
        Assert.Contains("action=\"/erp/manufacturing/work-orders/create\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"bom_id\"", text, StringComparison.Ordinal);
        Assert.Contains("Create work order", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersMfgWoCreateWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpMfgWoCreateWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpMfgWoCreateWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksMfgWoCreateLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/manufacturing/work-orders/create");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_mfg_wo_create", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/mfg-wo-create");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void FormatCreatedMessage_MatchesPhp()
    {
        Assert.Equal("Work order WO-T1 created", ErpMfgWoCreateWriteService.FormatCreatedMessage("WO-T1", 9));
        Assert.Equal("Work order #9 created", ErpMfgWoCreateWriteService.FormatCreatedMessage("", 9));
    }

    [Fact]
    public void DryRun_RequiresBomAndRefusesConfirm()
    {
        var ok = new ErpMfgWoCreateDryRun().Evaluate(new ErpMfgWoCreateRequest(4, "WO-1"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var missing = new ErpMfgWoCreateDryRun().Evaluate(new ErpMfgWoCreateRequest());
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("BOM not found", missing.Detail);

        var confirm = new ErpMfgWoCreateDryRun().Evaluate(new ErpMfgWoCreateRequest(4, "WO-1", true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpManufacturingWoCreate", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxMfgWoCreate", text, StringComparison.Ordinal);
        Assert.Contains("IErpMfgWoCreateWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleMfgWoCreateAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpMfgWoCreateWriteService.cs"));
        Assert.Contains("Work order ", service, StringComparison.Ordinal);
        Assert.Contains("Manufacturing work-order tables are not provisioned", service, StringComparison.Ordinal);
        Assert.Contains("BOM not found", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_inventory", service, StringComparison.Ordinal);
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
