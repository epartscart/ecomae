using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPltFeatureSavePhpParityTests
{
    [Fact]
    public void TenantConfigApp_PostsNativeFeatureSaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpTenantConfigApp.razor"));
        Assert.Contains("action=\"/erp/platform/features/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"code\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"enabled\"", text, StringComparison.Ordinal);
        Assert.Contains("Save feature", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersFeatureSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPltFeatureSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksFeatureSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/platform/features/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_plt_feature_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/plt-feature-save").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpPltFeatureSaveDryRun().Evaluate(new ErpPltFeatureSaveRequest(Code: "NEW_GRID"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal("Feature code is required", new ErpPltFeatureSaveDryRun().Evaluate(new ErpPltFeatureSaveRequest()).Detail);
        Assert.Equal("confirm_writes_refused", new ErpPltFeatureSaveDryRun().Evaluate(new ErpPltFeatureSaveRequest(ConfirmWrites: true, Code: "NEW_GRID")).ValidationCode);
    }

    [Fact]
    public void Validate_MatchesPhp()
    {
        Assert.Equal("Feature code is required", ErpPltFeatureSaveWriteService.Validate(""));
        Assert.Null(ErpPltFeatureSaveWriteService.Validate("NEW_GRID"));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpPlatformFeaturesSave", text, StringComparison.Ordinal);
        Assert.Contains("HandlePltFeatureSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPltFeatureSaveWriteService.cs"));
        Assert.Contains("Feature saved", service, StringComparison.Ordinal);
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
