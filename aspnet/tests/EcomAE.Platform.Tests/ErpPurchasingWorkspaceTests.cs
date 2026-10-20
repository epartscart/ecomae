using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public class ErpPurchasingWorkspaceTests
{
    [Fact]
    public void Specs_AreKnownPurchasingTabs()
    {
        var generic = ErpD365WorkspaceCatalog.ForArea("purchasing")
            .Where(p => p.ExistingHref is null)
            .Select(p => p.Tab)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tab in ErpPurchasingWorkspaceSpecs.Tabs.Keys)
        {
            Assert.True(generic.Contains(tab), $"{tab} is not a generic purchasing tab in the catalog");
        }
    }

    [Fact]
    public void Specs_SqlTargetsRealTables()
    {
        foreach (var (tab, spec) in ErpPurchasingWorkspaceSpecs.Tabs)
        {
            Assert.StartsWith("SELECT", spec.Sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("epc_erp_", spec.Sql, StringComparison.Ordinal);
            Assert.DoesNotContain("null", spec.Sql, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(spec.EmptyHint), tab);
            Assert.True(spec.Headers.Count > 0, tab);
            Assert.NotNull(spec.Ensure);
        }
    }

    [Fact]
    public void Coverage_RemainderIsTracked()
    {
        // Purchasing tabs still on the generic grid — shrink this as areas get specs.
        var uncovered = ErpD365WorkspaceCatalog.ForArea("purchasing")
            .Where(p => p.ExistingHref is null)
            .Select(p => p.Tab)
            .Where(t => !ErpWorkspaceReadService.Specs.ContainsKey(t))
            .ToList();

        Assert.True(uncovered.Count == 0,
            $"Purchasing generic backlog grew to {uncovered.Count}: " + string.Join(", ", uncovered.Take(10)));
    }

    [Fact]
    public void ReadService_ServesAllSpecAreas()
    {
        // Merged spec dictionary: PIM + purchasing tabs resolve through the shared engine.
        Assert.True(ErpWorkspaceReadService.Specs.ContainsKey("pim_readiness_discrete"));
        Assert.True(ErpWorkspaceReadService.Specs.ContainsKey("ps_top100"));
        Assert.True(ErpWorkspaceReadService.Specs.ContainsKey("pim_product_upload") || !ErpD365WorkspaceCatalog.ForArea("pim").Any(p2 => p2.Tab == "pim_product_upload" && p2.ExistingHref is null));
    }
}

public class ErpWarehouseWorkspaceTests
{
    [Fact]
    public void Specs_AreKnownWarehouseTabs()
    {
        var generic = ErpD365WorkspaceCatalog.ForArea("warehouse")
            .Where(p => p.ExistingHref is null)
            .Select(p => p.Tab)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tab in ErpWarehouseWorkspaceSpecs.Tabs.Keys)
        {
            Assert.True(generic.Contains(tab), $"{tab} is not a generic warehouse tab in the catalog");
        }
    }

    [Fact]
    public void Specs_SqlTargetsRealTables()
    {
        foreach (var (tab, spec) in ErpWarehouseWorkspaceSpecs.Tabs)
        {
            Assert.StartsWith("SELECT", spec.Sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("epc_erp_", spec.Sql, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(spec.EmptyHint), tab);
            Assert.True(spec.Headers.Count > 0, tab);
        }
    }
}

public class ErpProductionWorkspaceTests
{
    [Fact]
    public void Specs_AreKnownProductionTabs()
    {
        var generic = ErpD365WorkspaceCatalog.ForArea("production")
            .Where(p => p.ExistingHref is null)
            .Select(p => p.Tab)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tab in ErpProductionWorkspaceSpecs.Tabs.Keys)
        {
            Assert.True(generic.Contains(tab), $"{tab} is not a generic production tab in the catalog");
        }
    }

    [Fact]
    public void Specs_SqlTargetsRealTables()
    {
        foreach (var (tab, spec) in ErpProductionWorkspaceSpecs.Tabs)
        {
            Assert.StartsWith("SELECT", spec.Sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("epc_", spec.Sql, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(spec.EmptyHint), tab);
            Assert.True(spec.Headers.Count > 0, tab);
        }
    }
}

public class ErpMasterPlanningWorkspaceTests
{
    [Fact]
    public void Specs_AreKnownTabs()
    {
        var generic = ErpD365WorkspaceCatalog.ForArea("master_planning_area")
            .Where(p => p.ExistingHref is null)
            .Select(p => p.Tab)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tab in ErpMasterPlanningWorkspaceSpecs.Tabs.Keys)
        {
            Assert.True(generic.Contains(tab), $"{tab} is not a generic master-planning tab in the catalog");
        }
    }
}

public class ErpApWorkspaceTests
{
    [Fact]
    public void Specs_AreKnownTabs()
    {
        var generic = ErpD365WorkspaceCatalog.ForArea("ap")
            .Where(p => p.ExistingHref is null)
            .Select(p => p.Tab)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tab in ErpApWorkspaceSpecs.Tabs.Keys)
        {
            Assert.True(generic.Contains(tab), $"{tab} is not a generic ap tab in the catalog");
        }
    }
}

public class ErpArWorkspaceTests
{
    [Fact]
    public void Specs_AreKnownTabs()
    {
        var generic = ErpD365WorkspaceCatalog.ForArea("ar")
            .Where(p => p.ExistingHref is null)
            .Select(p => p.Tab)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tab in ErpArWorkspaceSpecs.Tabs.Keys)
        {
            Assert.True(generic.Contains(tab), $"{tab} is not a generic ar tab in the catalog");
        }
    }
}

public class ErpBankingWorkspaceTests
{
    [Fact]
    public void Specs_AreKnownTabs()
    {
        var generic = ErpD365WorkspaceCatalog.ForArea("banking")
            .Where(p => p.ExistingHref is null)
            .Select(p => p.Tab)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tab in ErpBankingWorkspaceSpecs.Tabs.Keys)
        {
            Assert.True(generic.Contains(tab), $"{tab} is not a generic banking tab in the catalog");
        }
    }
}

public class ErpTaxWorkspaceTests
{
    [Fact]
    public void Specs_AreKnownTabs()
    {
        var generic = ErpD365WorkspaceCatalog.ForArea("tax")
            .Where(p => p.ExistingHref is null)
            .Select(p => p.Tab)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tab in ErpTaxWorkspaceSpecs.Tabs.Keys)
        {
            Assert.True(generic.Contains(tab), $"{tab} is not a generic tax tab in the catalog");
        }
    }
}

public class ErpSalesWorkspaceTests
{
    [Fact]
    public void Specs_AreKnownTabs()
    {
        var generic = ErpD365WorkspaceCatalog.ForArea("sales")
            .Where(p => p.ExistingHref is null)
            .Select(p => p.Tab)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tab in ErpSalesWorkspaceSpecs.Tabs.Keys)
        {
            Assert.True(generic.Contains(tab), $"{tab} is not a generic sales tab in the catalog");
        }
    }
}

public class ErpCoverageSweepTests
{
    [Fact]
    public void EveryGenericTab_HasADataSpec()
    {
        var uncovered = new[]
            {
                "purchasing", "warehouse", "production", "master_planning_area", "enterprise",
                "payroll_area", "ap", "transportation", "projects", "tax", "time_att",
                "service_mgmt", "ar", "banking", "sales", "retail", "gl", "pim",
                "asset_mgmt", "budgeting", "setup", "questionnaire", "landed_cost_area",
                "common", "rebate_mgmt", "inventory_mgmt", "cost_acct", "credit_coll",
                "expense", "fixed_assets", "leave_abs", "cost_mgmt", "people", "mhei",
                "consolidations", "audit_wb",
            }.SelectMany(ErpD365WorkspaceCatalog.ForArea)
            .Where(p => p.ExistingHref is null)
            .Select(p => p.Tab)
            .Where(t => !ErpWorkspaceReadService.Specs.ContainsKey(t))
            .Distinct()
            .ToList();

        Assert.True(uncovered.Count == 0,
            "Generic tabs with no data spec: " + string.Join(", ", uncovered.Take(30)));
    }
}
