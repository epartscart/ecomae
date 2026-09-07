using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards the live PHP <c>epc_proc_req_add_line</c> twin: SSR form, DI, catalog.
/// Schema ensure stays PHP.
/// </summary>
public sealed class ErpProcurementReqAddLinePhpParityTests
{
    [Fact]
    public void PurchaseRequestsApp_PostsNativeAddLineForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpPurchaseRequestsApp.razor"));
        Assert.Contains("action=\"/erp/procurement/requisitions/add-line\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"item_code\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"unit_price\"", text, StringComparison.Ordinal);
        Assert.Contains("Add line", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Add line stays on the Classic twin", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersAddLineWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpProcurementReqAddLineWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpProcurementReqAddLineWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksAddLineLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/procurement/requisitions/add-line");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_proc_req_add_line", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/proc-req-add-line");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpProcReqAddLineDryRun().Evaluate(new ErpProcReqAddLineRequest(3));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.False(ok.CutoverAllowed);
        Assert.True(ok.WouldWrite);

        var missing = new ErpProcReqAddLineDryRun().Evaluate(new ErpProcReqAddLineRequest(0));
        Assert.Equal("invalid_request", missing.ValidationCode);

        var confirm = new ErpProcReqAddLineDryRun().Evaluate(new ErpProcReqAddLineRequest(3, ConfirmWrites: true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpProcurementReqAddLine", text, StringComparison.Ordinal);
        Assert.Contains("IErpProcurementReqAddLineWriteService", text, StringComparison.Ordinal);
        Assert.Contains("item_code", text, StringComparison.Ordinal);
        Assert.Contains("Line added", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpProcurementReqAddLineWriteService.cs")), StringComparison.Ordinal);
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
