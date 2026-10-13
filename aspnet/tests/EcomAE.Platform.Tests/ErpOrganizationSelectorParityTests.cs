using System.Reflection;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpOrganizationSelectorParityTests
{
    [Fact]
    public void PurchaseRequests_UsesSharedHeaderBusinessUnitPicker()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/CpPurchaseRequestsApp.razor"));

        Assert.Contains("<ErpVoucherHeaderFields", text, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"business_unit_id\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<label>Business unit <input", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<select name=\"business_unit_id\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Budgets_UsesBusinessUnitPickerForBudgetAndParent()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/CpBudgetsApp.razor"));

        Assert.Contains("OrganizationMasters.LoadAsync", text, StringComparison.Ordinal);
        Assert.Contains("Kind=\"business_unit\"", text, StringComparison.Ordinal);
        Assert.Contains("FieldName=\"business_unit_id\"", text, StringComparison.Ordinal);
        Assert.Contains("FieldName=\"parent_id\"", text, StringComparison.Ordinal);
        Assert.Contains("<select class=\"form-control input-sm\" name=\"legal_entity_id\"", text, StringComparison.Ordinal);
        Assert.Contains("<select class=\"form-control input-sm\" name=\"dimension_id\"", text, StringComparison.Ordinal);
        Assert.Contains("Legal entity id <input", text, StringComparison.Ordinal);
        Assert.Contains("Dimension id <input", text, StringComparison.Ordinal);
        Assert.DoesNotContain("BU id <input", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Parent BU id <input", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BranchMeansBusinessUnitMaster()
    {
        Assert.Equal("business-unit", ErpMasterLookupReadService.ResolveKind("branch"));
        Assert.Equal("Business unit", ErpMasterLookupReadService.KindLabel("business-unit"));
        Assert.Equal("/erp/organization-app?tab=business_units", ErpMasterLookupReadService.MasterHref("business-unit"));

        var surfaces = new[]
        {
            "aspnet/src/EcomAE.Platform/Components/Pages/CpJewelleryFixingApp.razor",
            "aspnet/src/EcomAE.Platform/Components/Pages/CpJewelleryRetailApp.razor",
            "aspnet/src/EcomAE.Platform/Components/Pages/CpJewelleryStockVerificationApp.razor",
            "aspnet/src/EcomAE.Platform/Components/Pages/CpJewelleryRepairsApp.razor",
            "aspnet/src/EcomAE.Platform/Components/Pages/ErpOrganizationApp.razor",
            "aspnet/src/EcomAE.Platform/Components/Pages/CpFinAdvancedApp.razor",
        };
        foreach (var relative in surfaces)
        {
            var text = File.ReadAllText(FindRepoFile(relative));
            Assert.DoesNotContain("value=\"HO\"", text, StringComparison.Ordinal);
            Assert.DoesNotContain("<label>Branch ", text, StringComparison.Ordinal);
            Assert.DoesNotContain("<label>Branch id", text, StringComparison.Ordinal);
            Assert.DoesNotContain("<label>From branch", text, StringComparison.Ordinal);
            Assert.DoesNotContain("<label>To branch", text, StringComparison.Ordinal);
        }

        Assert.Contains("Kind=\"business_unit\"", File.ReadAllText(FindRepoFile(surfaces[0])), StringComparison.Ordinal);
        Assert.Contains("<ErpVoucherHeaderFields", File.ReadAllText(FindRepoFile(surfaces[1])), StringComparison.Ordinal);
        Assert.Contains("Kind=\"business_unit\"", File.ReadAllText(FindRepoFile(surfaces[2])), StringComparison.Ordinal);
        Assert.Contains("Kind=\"business_unit\"", File.ReadAllText(FindRepoFile(surfaces[3])), StringComparison.Ordinal);
        Assert.Contains("Kind=\"business_unit\"", File.ReadAllText(FindRepoFile(surfaces[4])), StringComparison.Ordinal);
        Assert.Contains("Kind=\"business_unit\"", File.ReadAllText(FindRepoFile(surfaces[5])), StringComparison.Ordinal);
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
