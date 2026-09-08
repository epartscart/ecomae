using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpIntgSubSavePhpParityTests
{
    [Fact]
    public void IntegrationsApp_PostsNativeSubscriptionForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpIntegrationsApp.razor"));
        Assert.Contains("action=\"/erp/integrations/subscriptions/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"event\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"target_type\"", text, StringComparison.Ordinal);
        Assert.Contains("Subscribe", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersIntgSubSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpIntgSubSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksIntgSubSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/integrations/subscriptions/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_intg_sub_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/intg-sub-save").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpIntgSubSaveDryRun().Evaluate(new ErpIntgSubSaveRequest(Event: "PaymentPosted", TargetType: "webhook"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal("Invalid subscription target type", new ErpIntgSubSaveDryRun().Evaluate(new ErpIntgSubSaveRequest(TargetType: "ftp")).Detail);
        Assert.Equal("confirm_writes_refused", new ErpIntgSubSaveDryRun().Evaluate(new ErpIntgSubSaveRequest(ConfirmWrites: true, TargetType: "webhook")).ValidationCode);
    }

    [Fact]
    public void Validate_MatchesPhp()
    {
        Assert.Null(ErpIntgSubSaveWriteService.Validate("webhook"));
        Assert.Null(ErpIntgSubSaveWriteService.Validate("internal"));
        Assert.Null(ErpIntgSubSaveWriteService.Validate("email"));
        Assert.Equal("Invalid subscription target type", ErpIntgSubSaveWriteService.Validate("ftp"));
        Assert.Equal("Invalid subscription target type", ErpIntgSubSaveWriteService.Validate(""));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpIntegrationsSubsSave", text, StringComparison.Ordinal);
        Assert.Contains("HandleIntgSubSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpIntgSubSaveWriteService.cs"));
        Assert.Contains("Subscription saved", service, StringComparison.Ordinal);
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
