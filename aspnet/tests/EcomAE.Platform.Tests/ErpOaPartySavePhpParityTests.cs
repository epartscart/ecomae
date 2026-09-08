using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards the live PHP <c>epc_oa_party_save</c> twin: SSR form, DI, catalog.
/// Address, contact, calendar, holiday, and schema ensure stay PHP.
/// </summary>
public sealed class ErpOaPartySavePhpParityTests
{
    [Fact]
    public void ContactsApp_PostsNativePartySaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpContactsApp.razor"));
        Assert.Contains("action=\"/erp/contacts/parties/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"name\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"party_type\"", text, StringComparison.Ordinal);
        Assert.Contains("Save party", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersPartySaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpOaPartySaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpOaPartySaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksPartySaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/contacts/parties/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_oa_party_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/oa-party-save");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpOaPartySaveDryRun().Evaluate(new ErpOaPartySaveRequest(
            Name: "Acme Trading LLC",
            PartyType: "organization"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.False(ok.CutoverAllowed);
        Assert.True(ok.WouldWrite);

        var missing = new ErpOaPartySaveDryRun().Evaluate(new ErpOaPartySaveRequest());
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("Party name is required", missing.Detail);

        var type = new ErpOaPartySaveDryRun().Evaluate(new ErpOaPartySaveRequest(
            Name: "x",
            PartyType: "alien"));
        Assert.Equal("invalid_request", type.ValidationCode);
        Assert.Equal("Invalid party type", type.Detail);

        var confirm = new ErpOaPartySaveDryRun().Evaluate(new ErpOaPartySaveRequest(
            ConfirmWrites: true,
            Name: "Acme Trading LLC",
            PartyType: "organization"));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Validate_MatchesPhpBounds()
    {
        Assert.Equal("Party name is required", ErpOaPartySaveWriteService.Validate("", "organization"));
        Assert.Equal("Invalid party type", ErpOaPartySaveWriteService.Validate("x", "alien"));
        Assert.Null(ErpOaPartySaveWriteService.Validate("Acme Trading LLC", "organization"));
        Assert.Null(ErpOaPartySaveWriteService.Validate("John Smith", "person"));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpContactsPartiesSave", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxOaPartySave", text, StringComparison.Ordinal);
        Assert.Contains("IErpOaPartySaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleOaPartySaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpOaPartySaveWriteService.cs"));
        Assert.Contains("Party saved", service, StringComparison.Ordinal);
        Assert.Contains("Party name is required", service, StringComparison.Ordinal);
        Assert.Contains("Address-book party table is not provisioned", service, StringComparison.Ordinal);
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
