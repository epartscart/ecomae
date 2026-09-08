using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpUaeTaxLegislationChecklistSetPhpParityTests
{
    [Fact]
    public void UaeTaxApp_PostsNativeChecklistForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpUaeTaxComplianceApp.razor"));
        Assert.Contains("/erp/uae-tax/legislation/checklist/set", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"item_key\"", text, StringComparison.Ordinal);
        Assert.Contains("Checklist step", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersChecklistSetWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpUaeTaxLegislationChecklistSetWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksChecklistSetLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/uae-tax/legislation/checklist/set");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_uae_tax_legislation_checklist_set_status", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/uae-tax-legislation-checklist-set").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpUaeTaxLegislationChecklistSetDryRun().Evaluate(new ErpUaeTaxLegislationChecklistSetRequest());
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpUaeTaxLegislationChecklistSetDryRun().Evaluate(new ErpUaeTaxLegislationChecklistSetRequest(ConfirmWrites: true)).ValidationCode);
    }

    [Fact]
    public void ActionKey_MatchesPhpSha256Prefix()
    {
        Assert.Equal(
            "2cf24dba5fb0a30e26e83b2ac5b9e29e",
            ErpUaeTaxLegislationChecklistSetWriteService.ActionKey("hello"));
        var parsed = ErpUaeTaxLegislationChecklistSetWriteService.ParseAllActionsJson("[\"A\",\"\",\"B\"]");
        Assert.Equal(new[] { "A", "B" }, parsed);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpUaeTaxLegislationChecklistSet", text, StringComparison.Ordinal);
        Assert.Contains("HandleUaeTaxLegislationChecklistSetAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpUaeTaxLegislationChecklistSetWriteService.cs"));
        Assert.Contains("Missing item or action key.", service, StringComparison.Ordinal);
        Assert.Contains("Checklist step marked implemented.", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_uae_tax_legislation_build_summary(", service, StringComparison.Ordinal);
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
