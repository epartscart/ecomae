using EcomAE.Platform.Cp;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Services;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpDeploymentModelsTests
{
    [Fact]
    public void Three_models_and_honest_hybrid_gap()
    {
        Assert.Equal(3, CpDeploymentModelsCatalog.Models.Count);
        Assert.Equal(TenantDeploymentKind.Hybrid, CpDeploymentModelsCatalog.ParseKind("hybrid"));
        Assert.Equal("cloud", CpDeploymentModelsCatalog.Classify("erp_only", "platform", null));
        Assert.Equal("on-premises", CpDeploymentModelsCatalog.Classify("commerce", "client", "on_premises"));
        Assert.Equal("hybrid", CpDeploymentModelsCatalog.Classify("erp_only", "platform", "hybrid"));
        Assert.Contains(CpDeploymentModelsCatalog.Models, m => m.Key == "hybrid" && m.Status.Contains("not live", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(CpDeploymentModelsCatalog.Matrix, r => r.Capability.Contains("Who hosts", StringComparison.Ordinal));
    }

    [Fact]
    public void Page_is_super_cp_and_on_the_tenants_menu()
    {
        Assert.Equal("/cp/deployment-models-app", EcomAeRoutes.ControlPanelDeploymentModelsApp);
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpDeploymentModelsApp.razor"));
        Assert.Contains("SuperCpHostGate", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("CpDeploymentModelsCatalog", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);

        Assert.Contains(PhpSuperCpBocNav.Areas, a => a.Key == "deployment_models" && a.Href == "/cp/deployment-models-app");
        Assert.Equal("/cp/deployment-models-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/control/portal/epc_deployment_models"));
    }

    [Fact]
    public void Hybrid_cannot_skip_the_on_premises_package()
    {
        var now = DateTimeOffset.UtcNow;
        var state = TenantInstallationControlPlane.Start("indj", TenantDeploymentKind.Hybrid, now);
        state = TenantInstallationControlPlane.Advance(state, TenantInstallationStage.ProvisioningCloudTenant, now.AddMinutes(1));
        Assert.Throws<InvalidOperationException>(() =>
            TenantInstallationControlPlane.Advance(state, TenantInstallationStage.Synchronizing, now.AddMinutes(2)));

        state = TenantInstallationControlPlane.Advance(state, TenantInstallationStage.PackageReady, now.AddMinutes(2));
        Assert.Equal(TenantDeploymentKind.Hybrid, state.DeploymentKind);
        Assert.Equal(40, state.Percent);
    }

    [Fact]
    public void Hybrid_sync_directions_are_accepted()
    {
        var envelope = new TenantSyncEnvelope(
            "env-h",
            "indj",
            "installation-1",
            "customer",
            "1",
            1,
            "hybrid-to-cloud",
            TenantInstallationControlPlane.Sha256Hex("{}"),
            DateTimeOffset.UtcNow);
        var result = TenantInstallationControlPlane.ValidateSyncEnvelope(envelope);
        Assert.True(result.Accepted);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
