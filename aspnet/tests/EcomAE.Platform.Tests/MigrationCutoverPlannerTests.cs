using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class MigrationCutoverPlannerTests
{
    [Fact]
    public void BuildPlanKeepsCustomerSurfacesDisabledByDefault()
    {
        var plan = new MigrationCutoverPlanner().BuildPlan();

        Assert.Equal("feature-flagged-reverse-proxy-cutover-with-php-fallback", plan.Strategy);
        Assert.Contains(plan.Steps, step => step.RoutePattern == "ecomae.com/CP" && !step.EnabledByDefault);
        Assert.Contains(plan.Steps, step => step.RoutePattern == "ecomae.com/ERP" && !step.EnabledByDefault);
        Assert.Contains(plan.Steps, step => step.RoutePattern == "ecomae.com/BOS" && !step.EnabledByDefault);
        Assert.Contains(plan.Steps, step => step.RoutePattern == "tenant.com/ERP" && step.RequiredGate.Contains("ERP-only tenant", StringComparison.Ordinal));
        Assert.Contains(plan.RollbackActions, action => action.Contains("PHP upstream", StringComparison.Ordinal));
    }
}
