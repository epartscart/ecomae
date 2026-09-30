using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Cp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpNavMatrixTests
{
    [Fact]
    public void DefaultTenantGetsPhpTreeWithoutDuplicatePlacements()
    {
        var all = LegacyDesktopChromeCatalog.ErpTopnav();
        var filtered = ErpIndustryNav.FilterTopnav(
            all,
            new ErpIndustryNav.ErpNavAudience("current", "auto_parts", "AE", false));

        Assert.NotEmpty(filtered);
        Assert.Empty(ErpIndustryNav.DuplicateTabIds(filtered));
        Assert.DoesNotContain(
            filtered.SelectMany(g => g.Links),
            ErpIndustryNav.IsJewelleryTab);
    }

    [Fact]
    public void SuperErpCanRetainFullPhpTreeAndApplyVersionedDenyList()
    {
        var all = LegacyDesktopChromeCatalog.ErpTopnav();
        var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "workflow_automation",
        };

        var filtered = ErpIndustryNav.FilterTopnav(
            all,
            new ErpIndustryNav.ErpNavAudience(
                "v2",
                "jewellery",
                "AE",
                IsSuperErp: true,
                DisabledTabIds: disabled));

        Assert.Contains(filtered.SelectMany(g => g.Links), ErpIndustryNav.IsJewelleryTab);
        Assert.DoesNotContain(
            filtered.SelectMany(g => g.Links),
            tab => tab.Id.EndsWith("/workflow_automation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GeneratedMatrixRetainsPhpDescriptionsGroupsAndVisibilityFlags()
    {
        var finance = PhpModuleCatalog.ErpAreas.Single(a => a.Id == "finance");
        var trialBalance = PhpModuleCatalog.ErpTabs.Single(t => t.Id == "finance/jw_trial_balance");
        var labourLaw = PhpModuleCatalog.ErpTabs.Single(t => t.Id == "people/hr_law");

        Assert.Equal("Chart of accounts, journals, period close and financial statements", finance.Description);
        Assert.True(trialBalance.IsJewellery);
        Assert.Equal("Inquiries and reports", trialBalance.PhpGroup);
        Assert.True(labourLaw.IsRaw);
        Assert.All(
            PhpModuleCatalog.ErpTabs,
            tab => Assert.False(string.IsNullOrWhiteSpace(tab.Href)));
    }

    [Fact]
    public void TenantModulePackCanRestrictTabsWithoutChangingPhpCatalog()
    {
        var filtered = ErpIndustryNav.FilterTopnav(
            LegacyDesktopChromeCatalog.ErpTopnav(),
            new ErpIndustryNav.ErpNavAudience(
                "v3",
                "auto_parts",
                "AE",
                IsSuperErp: false,
                EnabledModuleIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "finance",
                }));

        Assert.NotEmpty(filtered.SelectMany(g => g.Links));
        Assert.All(
            filtered.SelectMany(g => g.Links),
            tab => Assert.Equal("finance", tab.Group));
    }

    [Fact]
    public void VisibilityExplanationIdentifiesTenantRestrictions()
    {
        var tab = PhpModuleCatalog.ErpTabs.Single(t => t.Id == "finance/coa");
        var decision = ErpIndustryNav.ExplainTab(
            tab,
            new ErpIndustryNav.ErpNavAudience(
                "v4",
                "auto_parts",
                "AE",
                IsSuperErp: false,
                DisabledTabIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "coa",
                }));

        Assert.False(decision.Visible);
        Assert.Equal("disabled", decision.Code);
        Assert.Contains("deny", decision.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TenantRuleFeatureKeysUseAnExplicitNamespace()
    {
        Assert.Equal(
            "finance/coa",
            ErpNavTenantRules.DisabledTabId("erp.nav.disabled.finance/coa"));
        Assert.Equal(
            string.Empty,
            ErpNavTenantRules.DisabledTabId("feature.finance"));
        Assert.Equal(
            "tenant.example",
            ErpNavTenantRules.NormalizeSiteKey(" Tenant.Example "));
        Assert.Contains("finance", ErpNavTenantRules.ExplicitModuleIds("""["finance","inventory","catalog"]"""));
        Assert.Contains("inventory_mgmt/inventory", ErpNavTenantRules.ExplicitModuleIds("""["inventory_mgmt/inventory","catalog"]"""));
        Assert.DoesNotContain("catalog", ErpNavTenantRules.ExplicitModuleIds("""["catalog"]"""));
    }

    [Fact]
    public void InspectionIncludesVisibleAndHiddenPhpPlacements()
    {
        var rows = ErpIndustryNav.Inspect(new ErpIndustryNav.ErpNavAudience(
            "v1",
            "core",
            "AE",
            false,
            DisabledTabIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "finance/gl"
            }));

        Assert.Equal(PhpModuleCatalog.ErpTabs.Count, rows.Count);
        Assert.Contains(rows, row => row.PlacementId == "finance/gl"
            && !row.Visible
            && row.Code == "disabled");
        Assert.Contains(rows, row => row.PlacementId == "overview/dashboard"
            && row.Visible
            && row.Detail.Contains("AE", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/erp/jewellery-retail-app", "jewellery")]
    [InlineData("/erp/jewellery/stock-availability", "jewellery")]
    [InlineData("/cp/jewellery-masters-app", "jewellery")]
    [InlineData("/erp/project-accounting-app", "fitout")]
    [InlineData("/erp/fitout/project-pnl", "fitout")]
    public void IndustrySpecificRoutesDeclareTheirRequiredPack(string path, string industry)
    {
        Assert.Equal(industry, IndustrySpecificRoutePolicy.RequiredIndustry(path));
    }

    [Fact]
    public void Tenant_design_token_writes_use_resolved_tenant_scope()
    {
        Assert.Equal("tenant-a", CpDesignTokensWriteService.ResolveSiteKey(
            "tenant-b", superCpHost: false, tenantSiteKey: "Tenant-A"));
        Assert.Equal("tenant-b", CpDesignTokensWriteService.ResolveSiteKey(
            "Tenant-B", superCpHost: true, tenantSiteKey: "tenant-a"));
        Assert.Equal(string.Empty, CpDesignTokensWriteService.ResolveSiteKey(
            "tenant-b", superCpHost: false, tenantSiteKey: "!!!"));
    }

    [Fact]
    public void SharedErpRoutesRemainUnrestricted()
    {
        Assert.Null(IndustrySpecificRoutePolicy.RequiredIndustry("/erp/finance-vouchers-app"));
        Assert.Null(IndustrySpecificRoutePolicy.RequiredIndustry("/erp/inventory-app"));
    }

    [Theory]
    [InlineData("jewellery", true)]
    [InlineData("jewelry", true)]
    [InlineData("construction", true)]
    [InlineData("fitout", true)]
    [InlineData("auto_parts", false)]
    public void IndustrySpecificRoutesRequireMatchingHostPack(
        string hostIndustry,
        bool expected)
    {
        var allowed = IndustrySpecificRoutePolicy.Allows("jewellery", hostIndustry)
            || IndustrySpecificRoutePolicy.Allows("fitout", hostIndustry);
        Assert.Equal(expected, allowed);
    }

    [Fact]
    public void SharedHostAllowsTrustedJewelleryCompanyPack()
    {
        var company = new ErpCompanyDigest(2, "JW", "Jewellery Division", "AED", "AE", "jewellery_diamond", true);

        Assert.True(IndustrySpecificRoutePolicy.Allows("jewellery", "auto_parts", company));
        Assert.False(IndustrySpecificRoutePolicy.Allows("jewellery", "auto_parts", null));
    }

    [Fact]
    public void SharedHostAllowsTrustedFitOutCompanyPack()
    {
        var company = new ErpCompanyDigest(3, "FITOUT", "Fit-out Company", "AED", "AE", "fitout_contracting", true);

        Assert.True(IndustrySpecificRoutePolicy.Allows("fitout", "auto_parts", company));
        Assert.False(IndustrySpecificRoutePolicy.Allows("fitout", "auto_parts", null));
    }

    [Fact]
    public void NonMatchingCompanyCannotGrantIndustryRoute()
    {
        var company = new ErpCompanyDigest(1, "MAIN", "Main Company", "AED", "AE", "auto_parts", true);

        Assert.False(IndustrySpecificRoutePolicy.Allows("jewellery", "auto_parts", company));
        Assert.False(IndustrySpecificRoutePolicy.Allows("fitout", "auto_parts", company));
    }
}
