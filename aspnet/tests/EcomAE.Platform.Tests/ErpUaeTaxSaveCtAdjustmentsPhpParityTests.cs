using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpUaeTaxSaveCtAdjustmentsPhpParityTests
{
    [Fact]
    public void UaeTaxApp_PostsNativeCtAdjustmentsForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpUaeTaxComplianceApp.razor"));
        Assert.Contains("/erp/uae-tax/ct-adjustments/save", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"date_from\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"date_to\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"ct_non_deductible_entertainment\"", text, StringComparison.Ordinal);
        Assert.Contains("Save CT adjustments", text, StringComparison.Ordinal);
        Assert.DoesNotContain("writes=0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersCtAdjustmentsWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpUaeTaxSaveCtAdjustmentsWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksCtAdjustmentsLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/uae-tax/ct-adjustments/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_uae_ct_save_adjustments", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/uae-tax-save-ct-adjustments").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpUaeTaxSaveCtAdjustmentsDryRun().Evaluate(new ErpUaeTaxSaveCtAdjustmentsRequest());
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpUaeTaxSaveCtAdjustmentsDryRun().Evaluate(new ErpUaeTaxSaveCtAdjustmentsRequest(ConfirmWrites: true)).ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpUaeTaxSaveCtAdjustments", text, StringComparison.Ordinal);
        Assert.Contains("HandleUaeTaxSaveCtAdjustmentsAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpUaeTaxSaveCtAdjustmentsWriteService.cs"));
        Assert.Contains("Corporate Tax adjustments saved for this period", service, StringComparison.Ordinal);
        Assert.Contains("Invalid period dates", service, StringComparison.Ordinal);
        Assert.Contains("ON DUPLICATE KEY UPDATE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_uae_tax_compliance_ensure_schema", service, StringComparison.Ordinal);
    }

    [Fact]
    public void PeriodKey_MatchesPhpYmdJoin()
    {
        Assert.True(ErpUaeTaxSaveCtAdjustmentsWriteService.TryPeriodKey("2026-09-01", "2026-09-30", out var key));
        Assert.Equal("20260901_20260930", key);
        Assert.False(ErpUaeTaxSaveCtAdjustmentsWriteService.TryPeriodKey("", "2026-09-30", out _));
        Assert.Equal(12.35m, ErpUaeTaxSaveCtAdjustmentsWriteService.RoundAmount(12.346m));
        Assert.Equal(0m, ErpUaeTaxSaveCtAdjustmentsWriteService.RoundAmount(-4));
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
