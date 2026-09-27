using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>PHP <c>cp/content/control/portal/epc_integrations_guide.php</c> parity.</summary>
public sealed class CpIntegrationsGuideParityTests
{
    private static string PageSource()
        => File.ReadAllText(RepoPath("aspnet/src/EcomAE.Platform/Components/Pages/CpIntegrationsGuideApp.razor"));

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
    public void Sections_CoverEveryCatalogEntry_InCategoryOrder()
    {
        var sections = CpIntegrationsGuideCatalog.Build(isSuper: true);
        Assert.Equal(CpIntegrationsHubCatalog.All.Count, sections.Count);
        Assert.Equal(
            CpIntegrationsHubCatalog.Categories.Select(c => c.Key),
            sections.Select(s => s.Category).Distinct());
        Assert.All(sections, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Summary));
            Assert.NotEmpty(s.Steps);
            Assert.StartsWith("fa-", s.Icon, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void PhpSections_KeepExactCopy()
    {
        var smtp = CpIntegrationsGuideCatalog.Build(isSuper: true).Single(s => s.Key == "email_smtp");
        Assert.Equal(
            "Deliver order confirmations, OTP codes, and staff alerts through SMTP.",
            smtp.Summary);
        Assert.Equal(4, smtp.Steps.Count);
        Assert.Contains("Use Send test email — confirm delivery before go-live.", smtp.Steps);
        Assert.Contains(
            "Prefer a dedicated mailbox (orders@…) with SPF/DKIM aligned to your domain.",
            smtp.Tips);
    }

    [Fact]
    public void EverySuperSection_HasAuthoredStepsNotTheGenericFallback()
    {
        var sections = CpIntegrationsGuideCatalog.Build(isSuper: true);
        Assert.Equal(
            CpIntegrationsHubCatalog.All.Select(m => m.Key).Order(),
            sections.Select(s => s.Key).Order());
        Assert.All(sections, s =>
            Assert.DoesNotContain("Open Configure from the Integrations hub.", s.Steps));
    }

    [Fact]
    public void TenantView_HidesSuperOnlyEntriesWithoutTenantUrl_AndHidesConfigure()
    {
        var tenant = CpIntegrationsGuideCatalog.Build(isSuper: false);
        Assert.DoesNotContain(tenant, s => s.Key == "tenant_registry");
        Assert.All(
            tenant.Where(s => s.Key is "oauth" or "registration_enhanced" or "tax_toolkit"),
            s => Assert.Equal(string.Empty, s.ConfigureUrl));

        var super = CpIntegrationsGuideCatalog.Build(isSuper: true);
        Assert.Contains(super, s => s.Key == "tenant_registry");
        Assert.Equal(
            "/cp/control/portal/epc_cp_auth_settings",
            super.Single(s => s.Key == "oauth").ConfigureUrl);
    }

    [Fact]
    public void TenantView_DropsSuperOnlyApiDocumentationLink()
    {
        var tenant = CpIntegrationsGuideCatalog.Build(isSuper: false).Single(s => s.Key == "api_integrations");
        Assert.DoesNotContain(tenant.Links, l => l.Url.Contains("epc_api_documentation_guide", StringComparison.Ordinal));

        var super = CpIntegrationsGuideCatalog.Build(isSuper: true).Single(s => s.Key == "api_integrations");
        Assert.Contains(super.Links, l => l.Url.Contains("epc_api_documentation_guide", StringComparison.Ordinal));
    }

    [Fact]
    public void DedicatedGuideButton_OnlyForGuidesOutsideThisPage()
    {
        var sections = CpIntegrationsGuideCatalog.Build(isSuper: true);
        Assert.Equal(string.Empty, sections.Single(s => s.Key == "email_smtp").DedicatedGuide);
        Assert.Equal(
            "/cp/control/portal/epc_power_bi_guide",
            sections.Single(s => s.Key == "power_bi").DedicatedGuide);
        Assert.All(sections, s =>
            Assert.DoesNotContain("epc_integrations_guide", s.DedicatedGuide, StringComparison.Ordinal));
    }

    [Fact]
    public void Contents_ListEveryVisibleSection()
    {
        Assert.Equal(
            CpIntegrationsGuideCatalog.Build(isSuper: false).Select(s => s.Key).Order(),
            CpIntegrationsGuideCatalog.Contents(isSuper: false).Select(c => c.Key).Order());
        Assert.Contains(CpIntegrationsGuideCatalog.Contents(isSuper: true), c => c.Key == "tenant_registry");
    }

    [Fact]
    public void Page_OwnsThePhpRoute_AndRendersPhpStructure()
    {
        var source = PageSource();
        Assert.Contains("@page \"/cp/control/portal/epc_integrations_guide\"", source, StringComparison.Ordinal);
        Assert.Contains("Integrations Guide", source, StringComparison.Ordinal);
        Assert.Contains("Back to hub", source, StringComparison.Ordinal);
        Assert.Contains("Contents", source, StringComparison.Ordinal);
        Assert.Contains("Dedicated guide", source, StringComparison.Ordinal);
        Assert.Contains("CpIntegrationsGuideCatalog.Build", source, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", source, StringComparison.Ordinal);
        Assert.Contains("Admin login required.", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GuidesHub_NoLongerOwnsThePhpRoute_AndLinksToTheTwin()
    {
        var hub = File.ReadAllText(RepoPath("aspnet/src/EcomAE.Platform/Components/Pages/CpGuidesHubApp.razor"));
        Assert.DoesNotContain("epc_integrations_guide", hub, StringComparison.Ordinal);
        Assert.Equal(
            "/cp/integrations-guide-app",
            OperatorGuidesCatalog.Get("integrations")!.Href);
    }

    [Fact]
    public void Page_ExposesNoSecrets()
    {
        var source = PageSource();
        Assert.DoesNotContain("password", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api_key", source, StringComparison.OrdinalIgnoreCase);
    }
}
