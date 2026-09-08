using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards the live PHP <c>epc_oa_address_save</c> twin: SSR form, DI, catalog.
/// Party, contact, calendar, holiday, and schema ensure stay PHP.
/// </summary>
public sealed class ErpOaAddressSavePhpParityTests
{
    [Fact]
    public void ContactsApp_PostsNativeAddressSaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpContactsApp.razor"));
        Assert.Contains("action=\"/erp/contacts/addresses/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"party_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"purpose\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"line1\"", text, StringComparison.Ordinal);
        Assert.Contains("Save address", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersAddressSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpOaAddressSaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpOaAddressSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksAddressSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/contacts/addresses/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_oa_address_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/oa-address-save");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpOaAddressSaveDryRun().Evaluate(new ErpOaAddressSaveRequest(
            PartyId: 1,
            Purpose: "business",
            Line1: "1 Sheikh Zayed Rd"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var bad = new ErpOaAddressSaveDryRun().Evaluate(new ErpOaAddressSaveRequest(
            PartyId: 1,
            Purpose: "alien"));
        Assert.Equal("invalid_request", bad.ValidationCode);
        Assert.Equal("Invalid address purpose", bad.Detail);

        var confirm = new ErpOaAddressSaveDryRun().Evaluate(new ErpOaAddressSaveRequest(
            ConfirmWrites: true,
            PartyId: 1,
            Purpose: "business"));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Validate_MatchesPhpBounds()
    {
        Assert.Equal("Invalid address purpose", ErpOaAddressSaveWriteService.Validate("alien"));
        Assert.Null(ErpOaAddressSaveWriteService.Validate("business"));
        Assert.Null(ErpOaAddressSaveWriteService.Validate("invoice"));
        Assert.Null(ErpOaAddressSaveWriteService.Validate("delivery"));
        Assert.Null(ErpOaAddressSaveWriteService.Validate("home"));
        Assert.Null(ErpOaAddressSaveWriteService.Validate("other"));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpContactsAddressesSave", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxOaAddressSave", text, StringComparison.Ordinal);
        Assert.Contains("IErpOaAddressSaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleOaAddressSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpOaAddressSaveWriteService.cs"));
        Assert.Contains("Address saved", service, StringComparison.Ordinal);
        Assert.Contains("Invalid address purpose", service, StringComparison.Ordinal);
        Assert.Contains("Address-book address table is not provisioned", service, StringComparison.Ordinal);
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
