using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpTenantConfigSavePhpParityTests
{
    [Fact]
    public void TenantConfigApp_PostsNativeSaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpTenantConfigApp.razor"));
        Assert.Contains("/erp/tenant-config/save", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"company_name\"", text, StringComparison.Ordinal);
        Assert.Contains("ERP settings", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersTenantConfigSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpTenantConfigSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksTenantConfigSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/tenant-config/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_erp_adv_set_setting", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/tenant-config-save").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpTenantConfigSaveDryRun().Evaluate(new ErpTenantConfigSaveRequest());
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpTenantConfigSaveDryRun().Evaluate(new ErpTenantConfigSaveRequest(ConfirmWrites: true)).ValidationCode);
    }

    [Fact]
    public void AllowedKeys_MatchPhpAjaxWhitelist()
    {
        Assert.Contains("company_name", ErpTenantConfigSaveWriteService.AllowedKeys);
        Assert.Contains("vat_rate", ErpTenantConfigSaveWriteService.AllowedKeys);
        Assert.Contains("ui_grid_rows", ErpTenantConfigSaveWriteService.AllowedKeys);
        Assert.Equal(34, ErpTenantConfigSaveWriteService.AllowedKeys.Length);
        var collected = ErpTenantConfigSaveWriteService.CollectAllowed(new Dictionary<string, string>
        {
            ["company_name"] = " Acme ",
            ["nope"] = "x",
        });
        Assert.Equal("Acme", collected["company_name"]);
        Assert.False(collected.ContainsKey("nope"));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpTenantConfigSave", text, StringComparison.Ordinal);
        Assert.Contains("HandleTenantConfigSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpTenantConfigSaveWriteService.cs"));
        Assert.Contains("settings saved", service, StringComparison.Ordinal);
        Assert.Contains("erp_", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_tenant_config_set(", service, StringComparison.Ordinal);
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
