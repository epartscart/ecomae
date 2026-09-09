using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// PHP <c>epc_cp_build_nav_tabs</c> leftover groups 740–745 must appear on tenant CP topnav.
/// </summary>
public sealed class CpTopMenuPhpParityTests
{
    [Fact]
    public void TenantTopnav_IncludesPhpLegacyGroupLabels()
    {
        var groups = LegacyDesktopChromeCatalog.ControlPanelTopnav(
            includeSuperOnly: false,
            industryCode: "auto_parts");
        var labels = groups.Select(g => g.Label).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var label in new[]
                 {
                     "Commerce", "Customers", "Documents", "ERP", "Purchase", "Channels", "Logistics",
                     "Catalogue", "Users", "Content", "System", "Modules",
                     "AI", "Marketing", "Payments", "Integrations", "Portal"
                 })
        {
            Assert.Contains(label, labels);
        }

        Assert.DoesNotContain(labels, l => l.Equals("Platform", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(labels, l => l.Equals("Operator", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TenantTopnav_SystemAndModulesHavePhpDbItems()
    {
        var groups = LegacyDesktopChromeCatalog.ControlPanelTopnav(
            includeSuperOnly: false,
            industryCode: "auto_parts");
        var system = Assert.Single(groups, g => g.Label.Equals("System", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Config, SMS & languages", system.Subtitle);
        Assert.False(system.IsAdvanced);
        AssertContainsAspNet(system, "/cp/config-items-app");
        AssertContainsAspNet(system, "/cp/sms-whatsapp-app");
        AssertContainsAspNet(system, "/cp/communications-test-app");
        AssertContainsAspNet(system, "/cp/notifications-app");
        AssertContainsAspNet(system, "/cp/server-ip-app");
        AssertContainsAspNet(system, "/cp/languages-app");
        AssertContainsAspNet(system, "/cp/guides-app");
        AssertContainsAspNet(system, "/cp/tenant-email-app");

        var modules = Assert.Single(groups, g => g.Label.Equals("Modules", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Modules, plugins & templates", modules.Subtitle);
        AssertContainsAspNet(modules, "/cp/modules-app");
        AssertContainsAspNet(modules, "/cp/plugins-manager-app");
        AssertContainsAspNet(modules, "/cp/templates-manager-app");
        AssertContainsAspNet(modules, "/cp/debug-console-app");
        AssertContainsAspNet(modules, "/cp/industry-packs-app");
    }

    [Fact]
    public void TenantTopnav_UsersContentCatalogueDocumentsSplitLikePhp()
    {
        var groups = LegacyDesktopChromeCatalog.ControlPanelTopnav(
            includeSuperOnly: false,
            industryCode: "auto_parts");
        var users = Assert.Single(groups, g => g.Label.Equals("Users", StringComparison.OrdinalIgnoreCase));
        AssertContainsAspNet(users, "/cp/users-app");
        AssertContainsAspNet(users, "/cp/groups-app");

        var content = Assert.Single(groups, g => g.Label.Equals("Content", StringComparison.OrdinalIgnoreCase));
        AssertContainsAspNet(content, "/cp/pages-app");
        AssertContainsAspNet(content, "/cp/menus-app");
        AssertContainsAspNet(content, "/cp/file-manager-app");
        AssertContainsAspNet(content, "/cp/sitemap-app");

        var catalogue = Assert.Single(groups, g => g.Label.Equals("Catalogue", StringComparison.OrdinalIgnoreCase));
        AssertContainsAspNet(catalogue, "/cp/product-catalogue-app");
        AssertContainsAspNet(catalogue, "/erp/inventory-stock-app");
        AssertContainsAspNet(catalogue, "/cp/data-transfer-app");

        var documents = Assert.Single(groups, g => g.Label.Equals("Documents", StringComparison.OrdinalIgnoreCase));
        AssertContainsAspNet(documents, "/cp/document-control-app");
        Assert.DoesNotContain(documents.Links, l =>
            PhpSurfaceLinkMap.AspNetPrimaryHref(l.Href)
                .Equals("/cp/pages-app", StringComparison.OrdinalIgnoreCase)
            && !l.Href.Contains("document", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TenantTopnav_CommerceKeepsPhpLeftoverDeskItems()
    {
        var groups = LegacyDesktopChromeCatalog.ControlPanelTopnav(
            includeSuperOnly: false,
            industryCode: "auto_parts");
        var commerce = Assert.Single(groups, g => g.Label.Equals("Commerce", StringComparison.OrdinalIgnoreCase));
        AssertContainsAspNet(commerce, "/cp/offices-app");
        AssertContainsAspNet(commerce, "/cp/storages-app");
        AssertContainsAspNet(commerce, "/cp/geo-regions-app");
        AssertContainsAspNet(commerce, "/cp/price-lists-app");
        AssertContainsAspNet(commerce, "/cp/crosses-app");
        AssertContainsAspNet(commerce, "/cp/returns-rma-app");
        AssertContainsAspNet(commerce, "/cp/quote-requests-app");

        var logistics = Assert.Single(groups, g => g.Label.Equals("Logistics", StringComparison.OrdinalIgnoreCase));
        AssertContainsAspNet(logistics, "/cp/carriers-app");
        AssertContainsAspNet(logistics, "/cp/delivery-methods-app");
        Assert.DoesNotContain(logistics.Links, l =>
            PhpSurfaceLinkMap.AspNetPrimaryHref(l.Href)
                .Equals("/cp/offices-app", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logistics.Links, l =>
            PhpSurfaceLinkMap.AspNetPrimaryHref(l.Href)
                .Equals("/erp/inventory-stock-app", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TenantTopnav_EveryPhpLeftoverItemLandsInItsGroup()
    {
        var groups = LegacyDesktopChromeCatalog.ControlPanelTopnav(
            includeSuperOnly: false,
            industryCode: "auto_parts");
        foreach (var label in LegacyDesktopChromeCatalog.PhpLegacyParityGroupLabels)
        {
            var group = Assert.Single(groups, g => g.Label.Equals(label, StringComparison.OrdinalIgnoreCase));
            foreach (var extra in LegacyDesktopChromeCatalog.PhpLegacyGroupParityLinks(label))
            {
                if (LegacyDesktopChromeCatalog.IsSuperOnlyCpLink(extra.Href, extra.Group))
                {
                    continue;
                }

                var asp = PhpSurfaceLinkMap.AspNetPrimaryHref(extra.Href);
                Assert.Contains(
                    group.Links,
                    l => string.Equals(l.Href, extra.Href, StringComparison.OrdinalIgnoreCase)
                         || PhpSurfaceLinkMap.AspNetPrimaryHref(l.Href)
                             .Equals(asp, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public void PhpLegacyParityHrefs_RewriteToAspNetApps()
    {
        foreach (var label in LegacyDesktopChromeCatalog.PhpLegacyParityGroupLabels)
        {
            foreach (var link in LegacyDesktopChromeCatalog.PhpLegacyGroupParityLinks(label))
            {
                var asp = PhpSurfaceLinkMap.AspNetPrimaryHref(link.Href);
                Assert.False(string.IsNullOrWhiteSpace(asp), link.Href);
                Assert.False(asp.StartsWith("/CP", StringComparison.Ordinal), $"{link.Label} → {asp}");
                Assert.False(asp.EndsWith(".php", StringComparison.OrdinalIgnoreCase), $"{link.Label} → {asp}");
                Assert.True(
                    asp.StartsWith("/cp", StringComparison.OrdinalIgnoreCase)
                    || asp.StartsWith("/erp", StringComparison.OrdinalIgnoreCase),
                    $"{link.Label} → {asp}");
            }
        }
    }

    private static void AssertContainsAspNet(LegacyDesktopChromeCatalog.MegaGroup group, string aspNetHref)
    {
        Assert.Contains(
            group.Links,
            l => PhpSurfaceLinkMap.AspNetPrimaryHref(l.Href)
                .StartsWith(aspNetHref, StringComparison.OrdinalIgnoreCase));
    }
}
