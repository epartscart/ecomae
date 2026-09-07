using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards the live PHP <c>epc_wms_receive</c> twin: SSR form, DI, catalog, auto LP format.
/// Wave create, pick add, work complete, and schema ensure stay PHP.
/// </summary>
public sealed class ErpWmsReceivePhpParityTests
{
    [Fact]
    public void WarehouseApp_PostsNativeReceiveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpWarehouseWmsApp.razor"));
        Assert.Contains("action=\"/erp/wms/receive\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"item\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"qty\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"receive_location_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"putaway_location_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"lp_code\"", text, StringComparison.Ordinal);
        Assert.Contains("Receive &amp; raise put-away", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersReceiveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpWmsReceiveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpWmsReceiveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksReceiveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/wms/receive");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_wms_receive", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void AutoLpCode_MatchesPhpPad6()
    {
        Assert.Equal("LP000001", ErpWmsReceiveWriteService.FormatAutoLpCode(1));
        Assert.Equal("LP000042", ErpWmsReceiveWriteService.FormatAutoLpCode(42));
        Assert.Equal("LP100000", ErpWmsReceiveWriteService.FormatAutoLpCode(100000));
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpWmsReceiveDryRun().Evaluate(new ErpWmsReceiveRequest("WIDGET", 10, 1, 2));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.False(ok.CutoverAllowed);
        Assert.True(ok.WouldWrite);
        Assert.Equal("ok", ok.ValidationCode);
        Assert.Contains("epc_erp_wms_lp", ok.SimulatedSql[0], StringComparison.Ordinal);
        Assert.Contains("putaway", ok.SimulatedSql[0], StringComparison.Ordinal);

        var qty = new ErpWmsReceiveDryRun().Evaluate(new ErpWmsReceiveRequest("WIDGET", 0));
        Assert.Equal("qty_required", qty.ValidationCode);
        Assert.False(qty.WouldWrite);

        var confirm = new ErpWmsReceiveDryRun().Evaluate(new ErpWmsReceiveRequest("WIDGET", 1, ConfirmWrites: true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpWmsReceive", text, StringComparison.Ordinal);
        Assert.Contains("IErpWmsReceiveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("receive_location_id", text, StringComparison.Ordinal);
        Assert.Contains("putaway_location_id", text, StringComparison.Ordinal);
        Assert.Contains("lp_code", text, StringComparison.Ordinal);
        Assert.Contains("Received — put-away work raised", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpWmsReceiveWriteService.cs")), StringComparison.Ordinal);
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
