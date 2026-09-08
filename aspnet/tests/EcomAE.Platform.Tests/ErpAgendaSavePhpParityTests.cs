using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpAgendaSavePhpParityTests
{
    [Fact]
    public void AgendaApp_PostsNativeEventSaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpAgendaApp.razor"));
        Assert.Contains("action=\"/erp/agenda/events/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"title\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"start_at\"", text, StringComparison.Ordinal);
        Assert.Contains("Add event", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersAgendaSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpAgendaSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksAgendaSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/agenda/events/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_erp_agenda_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/agenda-save").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpAgendaSaveDryRun().Evaluate(new ErpAgendaSaveRequest(Title: "Standup"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal("Event title required", new ErpAgendaSaveDryRun().Evaluate(new ErpAgendaSaveRequest()).Detail);
        Assert.Equal("confirm_writes_refused", new ErpAgendaSaveDryRun().Evaluate(new ErpAgendaSaveRequest(ConfirmWrites: true, Title: "Standup")).ValidationCode);
    }

    [Fact]
    public void Validate_AndTimeResolve_MatchPhp()
    {
        Assert.Equal("Event title required", ErpAgendaSaveWriteService.Validate(""));
        Assert.Null(ErpAgendaSaveWriteService.Validate("Standup"));
        Assert.Equal(1_700_000_000, ErpAgendaSaveWriteService.ResolveStart("", 1_700_000_000));
        Assert.Equal(1_700_000_000, ErpAgendaSaveWriteService.ResolveStart("0", 1_700_000_000));
        Assert.Equal(1_700_003_600, ErpAgendaSaveWriteService.ResolveEnd("", 1_700_000_000));
        Assert.Equal(1_710_000_000, ErpAgendaSaveWriteService.ParseWhen("1710000000"));
        Assert.True(ErpAgendaSaveWriteService.ParseWhen("2026-09-07 10:00:00") > 0);
        Assert.True(ErpAgendaSaveWriteService.ParseWhen("2026-09-07T10:00") > 0);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpAgendaEventsSave", text, StringComparison.Ordinal);
        Assert.Contains("HandleAgendaSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpAgendaSaveWriteService.cs"));
        Assert.Contains("Agenda event added", service, StringComparison.Ordinal);
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
