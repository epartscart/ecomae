using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpAutomationDeactivatePhpParityTests
{
    [Fact]
    public void WorkflowsApp_PostsNativeDeactivateForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpWorkflowsApp.razor"));
        Assert.Contains("/erp/automation/deactivate", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"id\"", text, StringComparison.Ordinal);
        Assert.Contains("value=\"order_to_erp\"", text, StringComparison.Ordinal);
        Assert.Contains("value=\"goods_receipt_notify\"", text, StringComparison.Ordinal);
        Assert.Contains("Disable automation", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersAutomationDeactivateWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpAutomationDeactivateWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksAutomationDeactivateLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/automation/deactivate");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_erp_automation_set_enabled", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/automation-deactivate").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpAutomationDeactivateDryRun().Evaluate(new ErpAutomationDeactivateRequest());
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpAutomationDeactivateDryRun().Evaluate(new ErpAutomationDeactivateRequest(true)).ValidationCode);
    }

    [Fact]
    public void Catalogue_MapsPhpIdsToErpAutoSettingKeys()
    {
        Assert.Equal(22, ErpAutomationDeactivateWriteService.CatalogueSettingKeys.Count);
        Assert.True(ErpAutomationDeactivateWriteService.TryResolveSettingKey("order_to_erp", out var orderKey));
        Assert.Equal("erp_auto_auto_order_to_erp", orderKey);
        Assert.True(ErpAutomationDeactivateWriteService.TryResolveSettingKey(" year_end_close ", out var fyKey));
        Assert.Equal("erp_auto_auto_year_end", fyKey);
        Assert.True(ErpAutomationDeactivateWriteService.TryResolveSettingKey("payment_reminder", out var apKey));
        Assert.Equal("erp_auto_auto_ap_payment_reminder", apKey);
        Assert.True(ErpAutomationDeactivateWriteService.TryResolveSettingKey("goods_receipt_notify", out var grnKey));
        Assert.Equal("erp_auto_auto_grn_notify", grnKey);
        Assert.False(ErpAutomationDeactivateWriteService.TryResolveSettingKey("", out _));
        Assert.False(ErpAutomationDeactivateWriteService.TryResolveSettingKey("Order_To_Erp", out _));
        Assert.False(ErpAutomationDeactivateWriteService.TryResolveSettingKey("vat_filing_reminder", out _));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpAutomationDeactivate", text, StringComparison.Ordinal);
        Assert.Contains("HandleAutomationDeactivateAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpAutomationDeactivateWriteService.cs"));
        Assert.Contains("Automation disabled", service, StringComparison.Ordinal);
        Assert.Contains("Unknown automation", service, StringComparison.Ordinal);
        Assert.Contains("erp_auto_", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_adv_settings_ensure", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_automation_activate", service, StringComparison.Ordinal);
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
