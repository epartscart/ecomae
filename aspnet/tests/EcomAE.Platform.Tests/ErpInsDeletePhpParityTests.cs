using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_ins_delete</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpInsDeletePhpParityTests
{
    [Fact]
    public void InsuranceApp_PostsNativePolicyDeleteForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpInsuranceComplianceApp.razor"));
        Assert.Contains("action=\"/erp/insurance/delete\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("Delete policy", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersInsDeleteWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpInsDeleteWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpInsDeleteWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksInsDeleteLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/insurance/delete");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_ins_delete", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/ins-delete");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_RequiresIdAndRefusesConfirm()
    {
        var ok = new ErpInsDeleteDryRun().Evaluate(new ErpInsDeleteRequest(3));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var missing = new ErpInsDeleteDryRun().Evaluate(new ErpInsDeleteRequest());
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("A policy id is required.", missing.Detail);

        var confirm = new ErpInsDeleteDryRun().Evaluate(new ErpInsDeleteRequest(3, true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpInsuranceDelete", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxInsDelete", text, StringComparison.Ordinal);
        Assert.Contains("IErpInsDeleteWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleInsDeleteAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpInsDeleteWriteService.cs"));
        Assert.Contains("Policy deleted", service, StringComparison.Ordinal);
        Assert.Contains("Insurance policy table is not provisioned", service, StringComparison.Ordinal);
        Assert.Contains("A policy id is required.", service, StringComparison.Ordinal);
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
