using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Portal settings multi-site fleet is Super CP only.
/// Tenant CPs (epartscart.com) must not see Super CP inventory / deploy secrets.
/// </summary>
public sealed class CpPortalSettingsSuperOnlyTests
{
    [Fact]
    public void PortalSettingsApp_GatesNonSuperHosts()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpPortalSettingsApp.razor"));
        Assert.Contains("SuperCpHostGate.IsAllowed", text, StringComparison.Ordinal);
        Assert.Contains("Super CP host only", text, StringComparison.Ordinal);
        Assert.Contains("independent", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/cp/tenant-email-app", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PortalSettingsDigest_GatesNonSuperHosts()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ControlPanelPortalSettings", text, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed(context)", text, StringComparison.Ordinal);
        Assert.Contains("Portal settings fleet digest is Super CP only", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TenantEmailApp_ExistsAndIsTenantScoped()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpTenantEmailApp.razor"));
        Assert.Contains("@page \"/cp/tenant-email-app\"", text, StringComparison.Ordinal);
        Assert.Contains("This tenant", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Deploy targets", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_portal_deploy_targets", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SuperCpHostGate", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TenantCommandCentre_DoesNotLinkPortalFleetSettings()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpCommandCentreApp.razor"));
        Assert.DoesNotContain("\"/cp/portal-settings-app\"", text, StringComparison.Ordinal);
        Assert.Contains("\"/cp/config-items-app\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ChromeFilter_HidesPortalSettingsOnTenant()
    {
        Assert.True(LegacyDesktopChromeCatalog.IsSuperOnlyCpLink("/cp/portal-settings-app"));
        var tenantGroups = LegacyDesktopChromeCatalog.ControlPanelTopnav(includeSuperOnly: false);
        var allHrefs = tenantGroups.SelectMany(g => g.Links).Select(l => l.Href).ToList();
        Assert.DoesNotContain(allHrefs, h => h.Contains("portal-settings-app", StringComparison.OrdinalIgnoreCase));
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

        throw new FileNotFoundException(relative);
    }
}
