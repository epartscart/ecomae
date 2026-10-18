using EcomAE.Platform.Components.Shared;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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
        Assert.Contains("ErpProcessFlowCatalog", map, StringComparison.Ordinal);
        Assert.Contains("data-erp-flow", map, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion", map, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_shell", map, StringComparison.Ordinal);
    }

    [Fact]
    public void ProcessFlowCoversEveryModuleArea()
    {
        var areas = ErpProcessFlowCatalog.Areas;
        Assert.Equal(PhpModuleCatalog.ErpAreaCount, areas.Count);
        Assert.Equal(areas.Count, areas.Select(a => a.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(ErpProcessFlowCatalog.DefaultAreaId, areas.Select(a => a.Id), StringComparer.OrdinalIgnoreCase);
        Assert.All(areas, area =>
        {
            Assert.True(area.Steps.Count >= 3, area.Id);
            Assert.All(area.Steps, step =>
            {
                Assert.StartsWith("/erp", step.Href, StringComparison.OrdinalIgnoreCase);
                Assert.StartsWith("fa-", step.Icon, StringComparison.Ordinal);
                Assert.DoesNotContain("epc_erp_shell", step.Href, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("php-reference", step.Href, StringComparison.OrdinalIgnoreCase);
            });
        });

        Assert.Contains(areas, a => a.Steps.Any(s => s.Label == "Purchase orders" && s.Href == "/erp/purchase-orders-app"));
        Assert.Contains(areas, a => a.Steps.Any(s => s.Href.Contains("/erp/invoices-app", StringComparison.Ordinal)));
        Assert.Contains(areas, a => a.Steps.Any(s => s.Href.Contains("/erp/product-info-app", StringComparison.Ordinal)));
        Assert.Contains(areas, a => a.Steps.Any(s => s.Href.Contains("/erp/bank-reconciliation-app", StringComparison.Ordinal)));
        Assert.Contains(areas, a => a.Steps.Any(s => s.Href.Contains("/erp/payroll-app", StringComparison.Ordinal)));
        Assert.Contains(areas, a => a.Id == "purchasing" && a.JewellerySteps.Count > 0);
    }

    [Fact]
    public async Task ProcessFlowRendersOneAreaAndKeepsTheRestReady()
    {
        var html = await RenderMapAsync(companyId: 7, jewellery: false);
        Assert.Contains("data-erp-flow", html, StringComparison.Ordinal);
        Assert.Contains("Process area", html, StringComparison.Ordinal);
        Assert.Contains(">Purchase orders<", html, StringComparison.Ordinal);
        Assert.Contains("/erp/purchase-orders-app?company=7", html, StringComparison.Ordinal);
        Assert.Contains("/erp/invoices-app", html, StringComparison.Ordinal);
        Assert.Contains("/erp/product-info-app", html, StringComparison.Ordinal);
        Assert.Contains("/erp/bank-reconciliation-app", html, StringComparison.Ordinal);
        Assert.Contains("/erp/payroll-app", html, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_shell", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("php-reference", html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, Count(html, "data-flow-panel="));
        Assert.Equal(PhpModuleCatalog.ErpAreaCount, Count(html, "<option "));
        Assert.Contains("\"id\":\"sales\"", html, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"finance\"", html, StringComparison.Ordinal);
        Assert.True(html.Length < 80_000, "process flow markup is " + html.Length);
        Assert.DoesNotContain("jw_metal_purchase", html, StringComparison.Ordinal);

        var jewellery = await RenderMapAsync(companyId: 0, jewellery: true);
        Assert.Contains("jw_metal_purchase", jewellery, StringComparison.Ordinal);
        Assert.Contains("/erp/invoices-app", jewellery, StringComparison.Ordinal);
    }

    private static async Task<string> RenderMapAsync(long companyId, bool jewellery)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var renderer = new HtmlRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = new Dictionary<string, object?>
            {
                [nameof(ErpHomeProcessMap.CompanyId)] = companyId,
                [nameof(ErpHomeProcessMap.ShowJewellery)] = jewellery,
            };
            var view = await renderer.RenderComponentAsync<ErpHomeProcessMap>(ParameterView.FromDictionary(parameters));
            return view.ToHtmlString();
        });
    }

    private static int Count(string html, string token)
    {
        var count = 0;
        var from = 0;
        while (from < html.Length)
        {
            var at = html.IndexOf(token, from, StringComparison.Ordinal);
            if (at < 0)
            {
                break;
            }

            count++;
            from = at + token.Length;
        }

        return count;
    }

    [Fact]
    public async Task HomeInsightsSwitchBetweenCustomerSupplierAndOtherDashboards()
    {
        var html = await RenderInsightsAsync(companyId: 7);
        Assert.Contains("data-board=\"overview\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"erp-home-board\"", html, StringComparison.Ordinal);
        Assert.Contains("erp-home-board", html, StringComparison.Ordinal);
        Assert.Contains(">Customers<", html, StringComparison.Ordinal);
        Assert.Contains(">Overall customer sales<", html, StringComparison.Ordinal);
        Assert.Contains(">Suppliers<", html, StringComparison.Ordinal);
        Assert.Contains(">Cash and bank<", html, StringComparison.Ordinal);
        Assert.Contains(">Profit<", html, StringComparison.Ordinal);
        Assert.Contains(">Inventory<", html, StringComparison.Ordinal);
        Assert.Contains("data-boards=\"overview customers\"", html, StringComparison.Ordinal);
        Assert.Contains("Who owes money", html, StringComparison.Ordinal);
        Assert.Contains("Who we owe", html, StringComparison.Ordinal);
        Assert.Contains("data-boards=\"suppliers\"", html, StringComparison.Ordinal);
        Assert.Contains("/erp/payables-app?company=7", html, StringComparison.Ordinal);
        Assert.Contains("/erp/receivables-app?company=7", html, StringComparison.Ordinal);
        Assert.Contains("/erp/report-center-app?tab=pl&amp;company=7", html, StringComparison.Ordinal);
        Assert.Contains("sessionStorage", html, StringComparison.Ordinal);
        Assert.DoesNotContain("php-reference", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("epc_erp_shell", html, StringComparison.OrdinalIgnoreCase);
        Assert.True(html.Length < 40_000, "insights markup is " + html.Length);
    }

    private static async Task<string> RenderInsightsAsync(long companyId)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var renderer = new HtmlRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = new Dictionary<string, object?>
            {
                [nameof(ErpHomeInsights.CompanyId)] = companyId,
                [nameof(ErpHomeInsights.Revenue)] = 1200m,
                [nameof(ErpHomeInsights.PreviousRevenue)] = 900m,
                [nameof(ErpHomeInsights.Expense)] = 400m,
                [nameof(ErpHomeInsights.PreviousExpense)] = 350m,
                [nameof(ErpHomeInsights.Profit)] = 800m,
                [nameof(ErpHomeInsights.Receivables)] = 250m,
                [nameof(ErpHomeInsights.Payables)] = 180m,
                [nameof(ErpHomeInsights.Cash)] = 60m,
                [nameof(ErpHomeInsights.Stock)] = 40m,
                [nameof(ErpHomeInsights.Vat)] = 15m,
                [nameof(ErpHomeInsights.InvoicesDue)] = 3,
                [nameof(ErpHomeInsights.OverdueInvoices)] = 1,
                [nameof(ErpHomeInsights.TrendLabels)] = new[] { "May", "Jun" },
                [nameof(ErpHomeInsights.TrendRevenue)] = new[] { 100m, 120m },
                [nameof(ErpHomeInsights.TrendProfit)] = new[] { 40m, 50m },
            };
            var view = await renderer.RenderComponentAsync<ErpHomeInsights>(ParameterView.FromDictionary(parameters));
            return view.ToHtmlString();
        });
    }

    [Fact]
    public void ProductInformationHasInventoryPartAndAssembly()
    {
        var page = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpProductInfoApp.razor"));
        Assert.Contains("inventory_part", page, StringComparison.Ordinal);
        Assert.Contains("inventory_assembly", page, StringComparison.Ordinal);
        Assert.Contains("FieldName=\"bom_sku\"", page, StringComparison.Ordinal);
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
