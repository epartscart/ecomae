using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacyDesktopChromeCatalogTests
{
    [Fact]
    public void ControlPanelTopnavCoversPhpNavLabels()
    {
        var groups = LegacyDesktopChromeCatalog.ControlPanelTopnav();
        Assert.Equal(LegacyChromeNavCatalog.ControlPanel.Count, groups.Count);
        Assert.All(groups, g => Assert.NotEmpty(g.Links));
        // Catalog may still store PHP source hrefs; product chrome rewrites via AspNetPrimaryHref.
        Assert.Contains(groups, g => g.Links.Any(l =>
            PhpSurfaceLinkMap.AspNetPrimaryHref(l.Href).StartsWith("/cp", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void ControlPanelTopnav_CommerceUsesShopOmsCategory()
    {
        var commerce = Assert.Single(
            LegacyDesktopChromeCatalog.ControlPanelTopnav(includeSuperOnly: false, industryCode: "auto_parts"),
            g => g.Id == "commerce");
        Assert.Contains(commerce.Links, l => (l.Group ?? "").Equals("Shop / OMS", StringComparison.OrdinalIgnoreCase)
            || l.Id.Equals("oms-orders", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(commerce.Links, l =>
            l.Label.Contains("Retail and commerce", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ErpTopnavUsesCategoryAreaColumnsNotFlatCap()
    {
        var groups = LegacyDesktopChromeCatalog.ErpTopnav();
        Assert.Equal(PhpModuleCatalog.ErpCategoryCount, groups.Count);

        var r2r = Assert.Single(groups, g => g.Id == "record_to_report");
        Assert.NotNull(r2r.Columns);
        Assert.Contains(r2r.Columns!, c => string.Equals(c.Id, "finance", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(r2r.Links, l => string.Equals(l.Group, "finance", StringComparison.OrdinalIgnoreCase));
        Assert.False(string.IsNullOrWhiteSpace(r2r.HubHref));
        Assert.Equal("fa-university", r2r.Icon);
        Assert.Equal("R2R", r2r.ShortLabel);

        var o2c = Assert.Single(groups, g => g.Id == "order_to_cash");
        Assert.Contains(o2c.Columns!, c => string.Equals(c.Id, "sales", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(o2c.Links, l => string.Equals(l.Group, "sales", StringComparison.OrdinalIgnoreCase));

        // No artificial Take(36) — every catalogued tab under mapped areas is listed.
        var allTabIds = groups.SelectMany(g => g.Links).Select(l => l.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.True(allTabIds.Count >= PhpModuleCatalog.ErpTabCount);
        Assert.Contains(groups, g => (g.Columns?.Count ?? 0) > 1);
    }

    [Fact]
    public void BosTopnavUsesExplicitPhpSectionModuleMaps()
    {
        var groups = LegacyDesktopChromeCatalog.BosTopnav();
        Assert.Equal(PhpModuleCatalog.BosSectionCount, groups.Count);
        Assert.All(groups, g => Assert.NotEmpty(g.Links));
        Assert.All(groups, g => Assert.False(string.IsNullOrWhiteSpace(g.HubHref)));
        Assert.All(groups, g => Assert.False(string.IsNullOrWhiteSpace(g.Icon)));

        var fleet = Assert.Single(groups, g => g.Id == "fleet");
        Assert.Contains(fleet.Links, l => l.Id == "command_center");
        Assert.Contains(fleet.Links, l => l.Id == "platform_health");
        Assert.True(fleet.Links.Count >= 40, $"Fleet section should list PHP fleet items, got {fleet.Links.Count}");

        var commerce = Assert.Single(groups, g => g.Id == "commerce");
        Assert.Contains(commerce.Links, l => l.Id == "orders");
        Assert.DoesNotContain(commerce.Links, l => l.Id == "command_center");

        Assert.Contains(groups.SelectMany(g => g.Links), l =>
        {
            var asp = PhpSurfaceLinkMap.AspNetPrimaryHref(l.Href);
            return asp.StartsWith("/bos", StringComparison.OrdinalIgnoreCase)
                || asp.StartsWith("/cp", StringComparison.OrdinalIgnoreCase)
                || asp.StartsWith("/erp", StringComparison.OrdinalIgnoreCase);
        });
    }

    [Theory]
    [InlineData("cp", "#header")]
    [InlineData("erp", ".epc-erp-topnav")]
    [InlineData("erp", ".epc-erp-topnav-panel-hub")]
    [InlineData("bos", ".bos-topnav")]
    [InlineData("bos", ".bos-topnav__panel-hub")]
    [InlineData("storefront", ".top-menu-line")]
    [InlineData("storefront", ".schearch-line")]
    [InlineData("storefront", ".header_search_form_attr")]
    [InlineData("storefront", "#footer-widgets")]
    public void RequiredStructuralSelectorsDocumentProbeTargets(string surface, string expected)
    {
        var selectors = LegacyDesktopChromeCatalog.RequiredStructuralSelectors(surface);
        Assert.Contains(expected, selectors);
    }
}
