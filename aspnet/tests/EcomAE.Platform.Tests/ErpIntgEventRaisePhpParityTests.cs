using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpIntgEventRaisePhpParityTests
{
    [Fact]
    public void IntegrationsApp_PostsNativeRaiseForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpIntegrationsApp.razor"));
        Assert.Contains("action=\"/erp/integrations/events/raise\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"event\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"payload\"", text, StringComparison.Ordinal);
        Assert.Contains("Raise test event", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersIntgEventRaiseWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpIntgEventRaiseWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksIntgEventRaiseLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/integrations/events/raise");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_intg_event_raise", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/intg-event-raise").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpIntgEventRaiseDryRun().Evaluate(new ErpIntgEventRaiseRequest(Event: "PaymentPosted"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpIntgEventRaiseDryRun().Evaluate(new ErpIntgEventRaiseRequest(ConfirmWrites: true, Event: "PaymentPosted")).ValidationCode);
    }

    [Fact]
    public void EncodePayload_MatchesPhpAjaxDecode()
    {
        Assert.Equal("{\"raw\":\"\"}", ErpIntgEventRaiseWriteService.EncodePayload(null));
        Assert.Equal("{\"raw\":\"\"}", ErpIntgEventRaiseWriteService.EncodePayload(""));
        Assert.Equal("{\"raw\":\"not-json\"}", ErpIntgEventRaiseWriteService.EncodePayload("not-json"));
        Assert.Equal("{\"raw\":\"42\"}", ErpIntgEventRaiseWriteService.EncodePayload("42"));
        Assert.Equal("{\"order_id\":42,\"total\":999}", ErpIntgEventRaiseWriteService.EncodePayload("{\"order_id\":42,\"total\":999}"));
        Assert.Equal("[]", ErpIntgEventRaiseWriteService.EncodePayload("[]"));
    }

    [Fact]
    public void ActiveMatch_MatchesPhpEmpty()
    {
        Assert.False(ErpIntgEventRaiseWriteService.IsPhpNonemptyActive(null));
        Assert.False(ErpIntgEventRaiseWriteService.IsPhpNonemptyActive(0));
        Assert.False(ErpIntgEventRaiseWriteService.IsPhpNonemptyActive("0"));
        Assert.False(ErpIntgEventRaiseWriteService.IsPhpNonemptyActive(""));
        Assert.True(ErpIntgEventRaiseWriteService.IsPhpNonemptyActive(1));
        Assert.True(ErpIntgEventRaiseWriteService.IsPhpNonemptyActive("1"));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpIntegrationsEventsRaise", text, StringComparison.Ordinal);
        Assert.Contains("HandleIntgEventRaiseAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpIntgEventRaiseWriteService.cs"));
        Assert.Contains("Event raised · ", service, StringComparison.Ordinal);
        Assert.Contains("no_subscriber", service, StringComparison.Ordinal);
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
