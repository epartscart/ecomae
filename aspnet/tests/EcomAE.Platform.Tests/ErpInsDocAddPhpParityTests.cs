using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_ins_doc_add</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpInsDocAddPhpParityTests
{
    [Fact]
    public void InsuranceApp_PostsNativeDocAddForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpInsuranceComplianceApp.razor"));
        Assert.Contains("action=\"/erp/insurance/docs/add\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"policy_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"file_path\"", text, StringComparison.Ordinal);
        Assert.Contains("Add document", text, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"file\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersInsDocAddWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpInsDocAddWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpInsDocAddWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksInsDocAddLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/insurance/docs/add");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_ins_doc_add", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/ins-doc-add");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_RequiresPolicyAndRefusesConfirm()
    {
        var ok = new ErpInsDocAddDryRun().Evaluate(new ErpInsDocAddRequest(3, "Schedule"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var missing = new ErpInsDocAddDryRun().Evaluate(new ErpInsDocAddRequest());
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("Select a policy", missing.Detail);

        var confirm = new ErpInsDocAddDryRun().Evaluate(new ErpInsDocAddRequest(3, "Schedule", true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpInsuranceDocsAdd", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxInsDocAdd", text, StringComparison.Ordinal);
        Assert.Contains("IErpInsDocAddWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleInsDocAddAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpInsDocAddWriteService.cs"));
        Assert.Contains("Document added", service, StringComparison.Ordinal);
        Assert.Contains("Insurance document table is not provisioned", service, StringComparison.Ordinal);
        Assert.Contains("Select a policy", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("IFormFile", service, StringComparison.Ordinal);
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
