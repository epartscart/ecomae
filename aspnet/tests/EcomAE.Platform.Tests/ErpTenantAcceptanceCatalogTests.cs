using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpTenantAcceptanceCatalogTests
{
    [Fact]
    public void Catalog_covers_the_ERP_business_processes_before_tenant_promotion()
    {
        var ids = ErpTenantAcceptanceCatalog.Scenarios.Select(s => s.Id).ToArray();

        Assert.Equal(["B1", "B2", "B3", "B4", "B5", "B6", "B7", "B8", "BJ", "BF"], ids);
        Assert.Contains(
            ErpTenantAcceptanceCatalog.Scenarios,
            scenario => scenario.Id == "B3" && scenario.Description.Contains("settlement", StringComparison.Ordinal));
        Assert.Contains(
            ErpTenantAcceptanceCatalog.Scenarios,
            scenario => scenario.Id == "BF" && scenario.Description.Contains("project P&L", StringComparison.Ordinal));
    }

    [Fact]
    public void Tenant_promotion_requires_every_acceptance_gate()
    {
        var passed = ErpTenantAcceptanceCatalog.PromotionGates.ToHashSet(StringComparer.Ordinal);

        Assert.True(ErpTenantAcceptanceCatalog.CanPromoteTenant(passed));
        passed.Remove("tenant-isolation");
        Assert.False(ErpTenantAcceptanceCatalog.CanPromoteTenant(passed));
    }
}
