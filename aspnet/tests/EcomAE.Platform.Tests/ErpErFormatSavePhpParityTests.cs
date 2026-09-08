using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards the live PHP <c>epc_er_format_save</c> twin: SSR form, DI, catalog.
/// Field add, run generation, and schema ensure stay PHP.
/// </summary>
public sealed class ErpErFormatSavePhpParityTests
{
    [Fact]
    public void ElectronicReportingApp_PostsNativeFormatSaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpElectronicReportingApp.razor"));
        Assert.Contains("action=\"/erp/electronic-reporting/formats/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"code\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"name\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"output_type\"", text, StringComparison.Ordinal);
        Assert.Contains("Save format", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersFormatSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpErFormatSaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpErFormatSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksFormatSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/electronic-reporting/formats/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_er_format_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/er-format-save");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpErFormatSaveDryRun().Evaluate(new ErpErFormatSaveRequest(
            Code: "VENDLIST",
            Name: "Vendor list",
            OutputType: "csv"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.False(ok.CutoverAllowed);
        Assert.True(ok.WouldWrite);

        var missing = new ErpErFormatSaveDryRun().Evaluate(new ErpErFormatSaveRequest());
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("Code and name are required", missing.Detail);

        var type = new ErpErFormatSaveDryRun().Evaluate(new ErpErFormatSaveRequest(
            Code: "X",
            Name: "X",
            OutputType: "pdf"));
        Assert.Equal("invalid_request", type.ValidationCode);
        Assert.Equal("Output type must be csv, xml or json", type.Detail);

        var confirm = new ErpErFormatSaveDryRun().Evaluate(new ErpErFormatSaveRequest(
            ConfirmWrites: true,
            Code: "VENDLIST",
            Name: "Vendor list",
            OutputType: "csv"));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Validate_MatchesPhpBounds()
    {
        Assert.Equal("Code and name are required", ErpErFormatSaveWriteService.Validate("", "", "csv"));
        Assert.Equal("Code and name are required", ErpErFormatSaveWriteService.Validate("VENDLIST", "", "csv"));
        Assert.Equal("Output type must be csv, xml or json", ErpErFormatSaveWriteService.Validate("X", "X", "pdf"));
        Assert.Null(ErpErFormatSaveWriteService.Validate("VENDLIST", "Vendor list", "csv"));
        Assert.Null(ErpErFormatSaveWriteService.Validate("VJSON", "Vendor json", "json"));
        Assert.Null(ErpErFormatSaveWriteService.Validate("VXML", "Vendor xml", "xml"));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpElectronicReportingFormatsSave", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxErFormatSave", text, StringComparison.Ordinal);
        Assert.Contains("IErpErFormatSaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleErFormatSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpErFormatSaveWriteService.cs"));
        Assert.Contains("Format saved", service, StringComparison.Ordinal);
        Assert.Contains("Code and name are required", service, StringComparison.Ordinal);
        Assert.Contains("Electronic reporting format table is not provisioned", service, StringComparison.Ordinal);
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
