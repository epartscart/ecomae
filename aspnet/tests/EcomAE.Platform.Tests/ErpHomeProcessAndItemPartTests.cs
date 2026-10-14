using EcomAE.Platform.Erp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpHomeProcessAndItemPartTests
{
    [Fact]
    public void DashboardHomeShowsProcessMapAndInsights()
    {
        var home = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpBosDashboardApp.razor"));
        Assert.Contains("ErpHomeProcessMap", home, StringComparison.Ordinal);
        Assert.Contains("ErpHomeInsights", home, StringComparison.Ordinal);
        var map = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/ErpHomeProcessMap.razor"));
        Assert.Contains("Purchase orders", map, StringComparison.Ordinal);
        Assert.Contains("/erp/invoices-app", map, StringComparison.Ordinal);
        Assert.Contains("/erp/product-info-app", map, StringComparison.Ordinal);
        Assert.Contains("/erp/bank-reconciliation-app", map, StringComparison.Ordinal);
        Assert.Contains("/erp/payroll-app", map, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductInformationHasInventoryPartAndAssembly()
    {
        var page = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpProductInfoApp.razor"));
        Assert.Contains("inventory_part", page, StringComparison.Ordinal);
        Assert.Contains("inventory_assembly", page, StringComparison.Ordinal);
        Assert.Contains("name=\"bom_sku\"", page, StringComparison.Ordinal);
        Assert.Contains("Bill of materials", page, StringComparison.Ordinal);
    }

    [Fact]
    public void PartFormReadsAssemblyBillOfMaterials()
    {
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["part_kind"] = "inventory_assembly",
            ["cost"] = "12.50",
            ["sales_price"] = "25",
            ["cogs_account"] = "Cost of goods sold",
            ["bom_sku"] = new StringValues(["COVER", "", "BOLT"]),
            ["bom_desc"] = new StringValues(["Manual cover", "", "Bolt"]),
            ["bom_qty"] = new StringValues(["2", "", "4"]),
            ["bom_cost"] = new StringValues(["3", "", "0.5"]),
            ["bom_unit"] = new StringValues(["pcs", "", "pcs"]),
        });

        var part = ErpItemPartProfile.FromForm(form);
        Assert.NotNull(part);
        Assert.True(part!.IsAssembly);
        Assert.Equal(2, part.Bom.Count);
        Assert.Equal(8m, part.BomCost);
        Assert.Equal(100m, part.MarkupPercent);
        Assert.Equal(50m, part.MarginPercent);
    }

    [Fact]
    public void UnknownPartKindIsIgnored()
    {
        var form = new FormCollection(new Dictionary<string, StringValues> { ["part_kind"] = "service" });
        Assert.Null(ErpItemPartProfile.FromForm(form));
    }

    private static string Find(string relative)
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

        throw new FileNotFoundException(relative);
    }
}
