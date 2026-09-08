using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpOaContactSavePhpParityTests
{
    [Fact]
    public void ContactsApp_PostsNativeContactSaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpContactsApp.razor"));
        Assert.Contains("action=\"/erp/contacts/party-contacts/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"party_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"contact_type\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"value\"", text, StringComparison.Ordinal);
        Assert.Contains("Save contact", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersContactSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpOaContactSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksContactSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/contacts/party-contacts/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_oa_contact_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/oa-contact-save").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpOaContactSaveDryRun().Evaluate(new ErpOaContactSaveRequest(1, "email", "ops@local.test"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal("invalid_request", new ErpOaContactSaveDryRun().Evaluate(new ErpOaContactSaveRequest(1, "alien")).ValidationCode);
        Assert.Equal("confirm_writes_refused", new ErpOaContactSaveDryRun().Evaluate(new ErpOaContactSaveRequest(ConfirmWrites: true, PartyId: 1, ContactType: "email")).ValidationCode);
    }

    [Fact]
    public void Validate_MatchesPhpBounds()
    {
        Assert.Equal("Invalid contact type", ErpOaContactSaveWriteService.Validate("alien"));
        Assert.Null(ErpOaContactSaveWriteService.Validate("email"));
        Assert.Null(ErpOaContactSaveWriteService.Validate("phone"));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpContactsPartyContactsSave", text, StringComparison.Ordinal);
        Assert.Contains("HandleOaContactSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpOaContactSaveWriteService.cs"));
        Assert.Contains("Contact saved", service, StringComparison.Ordinal);
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
