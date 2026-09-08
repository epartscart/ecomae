using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPmListingAttachPhpParityTests
{
    [Fact]
    public void BudgetsApp_PostsNativeListingAttachForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpBudgetsApp.razor"));
        Assert.Contains("/erp/pm/listings/attach", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"voucher_ref\"", text, StringComparison.Ordinal);
        Assert.Contains("Attach listing to voucher", text, StringComparison.Ordinal);
        Assert.DoesNotContain("writes=0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersPmListingAttachWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPmListingAttachWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksPmListingAttachLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/pm/listings/attach");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_erp_pm_listing_attach", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/pm-listing-attach").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpPmListingAttachDryRun().Evaluate(new ErpPmListingAttachRequest());
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpPmListingAttachDryRun().Evaluate(new ErpPmListingAttachRequest(ConfirmWrites: true)).ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpPmListingAttach", text, StringComparison.Ordinal);
        Assert.Contains("HandlePmListingAttachAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPmListingAttachWriteService.cs"));
        Assert.Contains("Listing attached to voucher", service, StringComparison.Ordinal);
        Assert.Contains("attached", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_pm_listing_save", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_pm_next_listing_seq", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_dim_save", service, StringComparison.Ordinal);
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
