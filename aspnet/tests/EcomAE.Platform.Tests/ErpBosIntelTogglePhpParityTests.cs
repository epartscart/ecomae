using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpBosIntelTogglePhpParityTests
{
    [Fact]
    public void Dashboard_PostsNativeToggleForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpBosDashboardApp.razor"));
        Assert.Contains("/erp/bos-intel/toggle", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"code\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"checked\"", text, StringComparison.Ordinal);
        Assert.Contains("Toggle industry control", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Tick-to-save remains on the PHP workspace", text, StringComparison.Ordinal);
        Assert.DoesNotContain("writes=0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersBosIntelToggleWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpBosIntelToggleWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksBosIntelToggleLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/bos-intel/toggle");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_bos_intel_set_control", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/bos-intel-toggle-control").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting_EmptyCodeAllowed()
    {
        var ok = new ErpBosIntelToggleControlDryRun().Evaluate(new ErpBosIntelToggleControlRequest());
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpBosIntelToggleControlDryRun().Evaluate(new ErpBosIntelToggleControlRequest(ConfirmWrites: true)).ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpBosIntelToggle", text, StringComparison.Ordinal);
        Assert.Contains("HandleBosIntelToggleAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpBosIntelToggleWriteService.cs"));
        Assert.Contains("Control updated", service, StringComparison.Ordinal);
        Assert.Contains("bos_intel_controls", service, StringComparison.Ordinal);
        Assert.DoesNotContain("erp_bos_intel_controls", service, StringComparison.Ordinal);
        Assert.Contains("ON DUPLICATE KEY UPDATE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_adv_settings_ensure", service, StringComparison.Ordinal);
    }

    [Fact]
    public void StateCodec_MatchesPhpJsonEncode()
    {
        Assert.Equal("[]", ErpBosIntelToggleWriteService.EncodeState(new Dictionary<string, int>()));
        Assert.Equal(
            """{"monthly_close":1}""",
            ErpBosIntelToggleWriteService.EncodeState(new Dictionary<string, int> { ["monthly_close"] = 1 }));
        Assert.Empty(ErpBosIntelToggleWriteService.ParseState("[]"));
        Assert.Empty(ErpBosIntelToggleWriteService.ParseState("not-json"));
        Assert.True(ErpBosIntelToggleWriteService.ParseState("""{"monthly_close":1}""").ContainsKey("monthly_close"));
        Assert.False(ErpBosIntelToggleWriteService.PhpCheckedIsOne("true"));
        Assert.False(ErpBosIntelToggleWriteService.PhpCheckedIsOne(""));
        Assert.True(ErpBosIntelToggleWriteService.PhpCheckedIsOne("1"));
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
