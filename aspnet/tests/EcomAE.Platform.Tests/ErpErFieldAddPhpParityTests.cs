using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards the live PHP <c>epc_er_field_add</c> twin: SSR form, DI, catalog.
/// Format save, run generation, and schema ensure stay PHP.
/// </summary>
public sealed class ErpErFieldAddPhpParityTests
{
    [Fact]
    public void ElectronicReportingApp_PostsNativeFieldAddForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpElectronicReportingApp.razor"));
        Assert.Contains("action=\"/erp/electronic-reporting/fields/add\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"format_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"label\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"source_key\"", text, StringComparison.Ordinal);
        Assert.Contains("Add field", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersFieldAddWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpErFieldAddWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpErFieldAddWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksFieldAddLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/electronic-reporting/fields/add");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_er_field_add", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/er-field-add");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpErFieldAddDryRun().Evaluate(new ErpErFieldAddRequest(
            FormatId: 1,
            Label: "Vendor",
            SourceKey: "name"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var missing = new ErpErFieldAddDryRun().Evaluate(new ErpErFieldAddRequest());
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("Field label and source key are required", missing.Detail);

        var confirm = new ErpErFieldAddDryRun().Evaluate(new ErpErFieldAddRequest(
            ConfirmWrites: true,
            FormatId: 1,
            Label: "Vendor",
            SourceKey: "name"));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Validate_MatchesPhpBounds()
    {
        Assert.Equal("Field label and source key are required", ErpErFieldAddWriteService.Validate("", "name"));
        Assert.Equal("Field label and source key are required", ErpErFieldAddWriteService.Validate("Vendor", ""));
        Assert.Null(ErpErFieldAddWriteService.Validate("Vendor", "name"));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpElectronicReportingFieldsAdd", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxErFieldAdd", text, StringComparison.Ordinal);
        Assert.Contains("IErpErFieldAddWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleErFieldAddAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpErFieldAddWriteService.cs"));
        Assert.Contains("Field added", service, StringComparison.Ordinal);
        Assert.Contains("Format not found", service, StringComparison.Ordinal);
        Assert.Contains("Electronic reporting field table is not provisioned", service, StringComparison.Ordinal);
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
