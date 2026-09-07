using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards the live PHP <c>epc_wht_code_save</c> twin: SSR form, DI, catalog.
/// Record, certificate, and schema ensure stay PHP.
/// </summary>
public sealed class ErpWhtCodeSavePhpParityTests
{
    [Fact]
    public void WithholdingApp_PostsNativeCodeSaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpWithholdingApp.razor"));
        Assert.Contains("action=\"/erp/withholding/codes/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"code\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"name\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"rate\"", text, StringComparison.Ordinal);
        Assert.Contains("Save code", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Code save, record, and certificate minting stay on the classic twin", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersCodeSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpWhtCodeSaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpWhtCodeSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksCodeSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/withholding/codes/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_wht_code_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/wht-code-save");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpWhtCodeSaveDryRun().Evaluate(new ErpWhtCodeSaveRequest(
            Code: "WHT5",
            Name: "Services 5%",
            Rate: 5));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.False(ok.CutoverAllowed);
        Assert.True(ok.WouldWrite);

        var missing = new ErpWhtCodeSaveDryRun().Evaluate(new ErpWhtCodeSaveRequest());
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("Code and name are required", missing.Detail);

        var rate = new ErpWhtCodeSaveDryRun().Evaluate(new ErpWhtCodeSaveRequest(
            Code: "X",
            Name: "X",
            Rate: 250));
        Assert.Equal("invalid_request", rate.ValidationCode);
        Assert.Equal("Rate must be a percentage between 0 and 100", rate.Detail);

        var confirm = new ErpWhtCodeSaveDryRun().Evaluate(new ErpWhtCodeSaveRequest(
            ConfirmWrites: true,
            Code: "WHT5",
            Name: "Services 5%",
            Rate: 5));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Validate_MatchesPhpBounds()
    {
        Assert.Equal("Code and name are required", ErpWhtCodeSaveWriteService.Validate("", "", 5));
        Assert.Equal("Code and name are required", ErpWhtCodeSaveWriteService.Validate("WHT5", "", 5));
        Assert.Equal("Rate must be a percentage between 0 and 100", ErpWhtCodeSaveWriteService.Validate("X", "X", 250));
        Assert.Equal("Rate must be a percentage between 0 and 100", ErpWhtCodeSaveWriteService.Validate("X", "X", -1));
        Assert.Null(ErpWhtCodeSaveWriteService.Validate("WHT5", "Services 5%", 0));
        Assert.Null(ErpWhtCodeSaveWriteService.Validate("WHT5", "Services 5%", 100));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpWithholdingCodesSave", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxWhtCodeSave", text, StringComparison.Ordinal);
        Assert.Contains("IErpWhtCodeSaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleWhtCodeSaveAsync", text, StringComparison.Ordinal);
        Assert.Contains(
            "Withholding code saved",
            File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpWhtCodeSaveWriteService.cs")),
            StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpWhtCodeSaveWriteService.cs")), StringComparison.Ordinal);
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
