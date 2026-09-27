using System;
using System.IO;
using System.Linq;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// The Super CP operator guide must stay a twin of
/// <c>cp/content/control/portal/epc_super_cp_operator_guide.php</c>, not a digest chapter.
/// </summary>
public sealed class SuperCpOperatorGuideParityTests
{
    private static string RepoPath(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "aspnet")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, relative);
    }

    [Fact]
    public void Catalog_MatchesThePhpModuleTable()
    {
        var modules = SuperCpOperatorGuideCatalog.Modules;

        Assert.Equal(
            [
                "Super CP Fleet Dashboard",
                "Super ERP Fleet Dashboard",
                "Customer board",
                "Price configs",
                "Info blocks",
                "Communication",
            ],
            modules.Select(m => m.Title));

        Assert.All(modules, m =>
        {
            Assert.StartsWith("fa-", m.Icon, StringComparison.Ordinal);
            Assert.StartsWith("/cp/", m.Url, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(m.Summary));
            Assert.False(string.IsNullOrWhiteSpace(m.Who));
            Assert.InRange(m.Workflow.Count, 3, 4);
        });

        Assert.Equal(
            "Cross-tenant customer search across the platform registry and every live tenant MySQL database.",
            modules.Single(m => m.Title == "Customer board").Summary);

        Assert.Equal(5, SuperCpOperatorGuideCatalog.OperatorDay.Count);
        Assert.StartsWith("Tenant hub", SuperCpOperatorGuideCatalog.OperatorDay[0], StringComparison.Ordinal);
    }

    [Fact]
    public void DedicatedPage_OwnsThePhpRoute_AndGuardsSuperCp()
    {
        var page = File.ReadAllText(
            RepoPath("aspnet/src/EcomAE.Platform/Components/Pages/CpSuperOperatorGuideApp.razor"));

        Assert.Contains("@page \"/cp/super-cp-operator-guide-app\"", page, StringComparison.Ordinal);
        Assert.Contains("@page \"/cp/control/portal/epc_super_cp_operator_guide\"", page, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed(ctx)", page, StringComparison.Ordinal);
        Assert.Contains("available on ECOM AE Super CP only", page, StringComparison.Ordinal);
        Assert.Contains("log in to Super CP", page, StringComparison.Ordinal);
        Assert.Contains("Typical operator day", page, StringComparison.Ordinal);
        Assert.Contains("epc-scp-guide-module", page, StringComparison.Ordinal);

        var hub = File.ReadAllText(
            RepoPath("aspnet/src/EcomAE.Platform/Components/Pages/CpGuidesHubApp.razor"));
        Assert.DoesNotContain("epc_super_cp_operator_guide", hub, StringComparison.Ordinal);

        Assert.Equal(
            "/cp/super-cp-operator-guide-app",
            OperatorGuidesCatalog.Get("super-cp-operator")!.Href);
    }
}
