using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpTaxToolkitInstallPhpParityTests
{
    [Fact]
    public void KitCodeForCountry_MatchesPhpLegacyMap()
    {
        Assert.Equal("AE-UAE-VAT", CpTaxToolkitWriteService.KitCodeForCountry("AE"));
        Assert.Equal("AE-UAE-VAT", CpTaxToolkitWriteService.KitCodeForCountry("uae"));
        Assert.Equal("SA-KSA-VAT", CpTaxToolkitWriteService.KitCodeForCountry("SA"));
        Assert.Equal("GB-UK-VAT", CpTaxToolkitWriteService.KitCodeForCountry("UK"));
        Assert.Equal("platform", CpTaxToolkitWriteService.NormalizeSiteKey(""));
        Assert.Equal("platform", CpTaxToolkitWriteService.NormalizeSiteKey("Platform!"));
        Assert.Equal("epartscart", CpTaxToolkitWriteService.NormalizeSiteKey("ePartsCart"));
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var install = new CpTaxToolkitInstallDryRun().Evaluate(new CpTaxToolkitInstallRequest(KitCode: "AE-UAE-VAT"));
        Assert.Equal("dry-run-validated", install.Status);
        Assert.Equal(0, install.Writes);
        Assert.False(install.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new CpTaxToolkitInstallDryRun().Evaluate(new CpTaxToolkitInstallRequest(ConfirmWrites: true)).ValidationCode);

        var assign = new CpTaxToolkitAssignDryRun().Evaluate(new CpTaxToolkitAssignRequest(CountryCode: "AE"));
        Assert.Equal("dry-run-validated", assign.Status);
        Assert.Equal(0, assign.Writes);
        Assert.False(assign.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new CpTaxToolkitAssignDryRun().Evaluate(new CpTaxToolkitAssignRequest(ConfirmWrites: true)).ValidationCode);
    }

    [Fact]
    public void TaxToolkitsApp_PostsNativeInstallAndAssignForms()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpTaxToolkitsApp.razor"));
        Assert.Contains("/cp/tax-toolkits/install", text, StringComparison.Ordinal);
        Assert.Contains("/cp/tax-toolkits/assign", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"kit_code\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"set_default\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"country_code\"", text, StringComparison.Ordinal);
        Assert.Contains("Install kit", text, StringComparison.Ordinal);
        Assert.Contains("Save platform tenant kit", text, StringComparison.Ordinal);
        Assert.Contains("worldwide seed stay on the Classic twin", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Install stays on the Classic twin", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersToolkitWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpTaxToolkitWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ICpTaxToolkitInstallDryRun", text, StringComparison.Ordinal);
        Assert.Contains("ICpTaxToolkitAssignDryRun", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksInstallAndAssignLiveGated()
    {
        var install = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/tax-toolkits/install");
        Assert.Equal("write-live-gated", install.Status);
        Assert.Contains("epc_tax_toolkit_install", install.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", install.Notes, StringComparison.Ordinal);
        var assign = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/tax-toolkits/assign");
        Assert.Equal("write-live-gated", assign.Status);
        Assert.Contains("epc_tax_toolkit_assign_tenant", assign.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("EcomAeRoutes.ControlPanelTaxToolkitInstall", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ControlPanelTaxToolkitAssign", text, StringComparison.Ordinal);
        Assert.Contains("HandleTaxToolkitInstallAsync", text, StringComparison.Ordinal);
        Assert.Contains("HandleTaxToolkitAssignAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Cp/CpTaxToolkitWriteService.cs"));
        Assert.Contains("epc_tax_toolkit_installs", service, StringComparison.Ordinal);
        Assert.Contains("epc_tax_toolkit_tenant_profile", service, StringComparison.Ordinal);
        Assert.Contains("worldwide seed stays on the Classic twin", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_tax_toolkit_ensure_schema", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_tax_toolkit_seed_kits", service, StringComparison.Ordinal);
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
