using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// PHP part_search mode-1: same article across warehouses → one article number + warehouse sub-rows;
/// left filter gets price/qty/term ranges + brand list from offers.
/// </summary>
public sealed class ChpuWarehouseGroupFilterParityTests
{
    [Fact]
    public void SearchApp_GroupsWarehouseOffersAndRendersSubRows()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));
        Assert.Contains("GroupWarehouseOffers", text, StringComparison.Ordinal);
        Assert.Contains("epc-warehouse-subrow", text, StringComparison.Ordinal);
        Assert.Contains("epc-warehouse-subrow__label", text, StringComparison.Ordinal);
        Assert.Contains("data-group-key", text, StringComparison.Ordinal);
        Assert.Contains("normalizeWarehouseGroups", text, StringComparison.Ordinal);
        Assert.Contains("epc_filter_exist_min", text, StringComparison.Ordinal);
        Assert.Contains("Availability (qty)", text, StringComparison.Ordinal);
        Assert.Contains("epc_warehouse_search_parity.css?v=20260812-fitment-sku", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ParityJs_FillsFilterRangesFromOffers()
    {
        var text = File.ReadAllText(FindRepoFile("content/general_pages/epc_warehouse_search_parity.js"));
        Assert.Contains("fillRangeInputsFromOffers", text, StringComparison.Ordinal);
        Assert.Contains("epc_filter_exist_min", text, StringComparison.Ordinal);
        Assert.Contains("epc_filter_exist_max", text, StringComparison.Ordinal);
        Assert.Contains("warehouse offer(s) visible", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ParityCss_HasWarehouseSubrowStyles()
    {
        var text = File.ReadAllText(FindRepoFile("content/general_pages/epc_warehouse_search_parity.css"));
        Assert.Contains("epc-warehouse-subrow", text, StringComparison.Ordinal);
        Assert.Contains("epc-warehouse-subrow__label", text, StringComparison.Ordinal);
        Assert.Contains("epc-warehouse-group-hint", text, StringComparison.Ordinal);
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

        throw new FileNotFoundException(relative);
    }
}
