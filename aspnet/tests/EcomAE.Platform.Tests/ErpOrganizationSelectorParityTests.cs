using System.Reflection;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpOrganizationSelectorParityTests
{
    [Fact]
    public void PurchaseRequests_UsesPhpBackedBusinessUnitSelectorWithNumericFallback()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/CpPurchaseRequestsApp.razor"));

        Assert.Contains("IErpOrganizationMasterReadService", text, StringComparison.Ordinal);
        Assert.Contains("OrganizationMasters.LoadAsync", text, StringComparison.Ordinal);
        Assert.Contains("name=\"business_unit_id\"", text, StringComparison.Ordinal);
        Assert.Contains("unit.Active", text, StringComparison.Ordinal);
        Assert.Contains("Business unit <input", text, StringComparison.Ordinal);
        Assert.Contains("<select name=\"business_unit_id\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Budgets_UsesOrganizationSelectorsForBudgetAndMasterScopes()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/CpBudgetsApp.razor"));

        Assert.Contains("OrganizationMasters.LoadAsync", text, StringComparison.Ordinal);
        Assert.Contains("<select class=\"form-control input-sm\" name=\"business_unit_id\"", text, StringComparison.Ordinal);
        Assert.Contains("<select class=\"form-control input-sm\" name=\"legal_entity_id\"", text, StringComparison.Ordinal);
        Assert.Contains("<select class=\"form-control input-sm\" name=\"parent_id\"", text, StringComparison.Ordinal);
        Assert.Contains("<select class=\"form-control input-sm\" name=\"dimension_id\"", text, StringComparison.Ordinal);
        Assert.Contains("Legal entity id <input", text, StringComparison.Ordinal);
        Assert.Contains("Parent BU id <input", text, StringComparison.Ordinal);
        Assert.Contains("Dimension id <input", text, StringComparison.Ordinal);
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

            dir = dir.Parent;
        }

        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var rooted = Path.GetFullPath(Path.Combine(asmDir, "..", "..", "..", "..", "..", relative));
        Assert.True(File.Exists(rooted), $"Missing repo file: {relative}");
        return rooted;
    }
}
