using EcomAE.Platform.Cp;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// PHP <c>epc_pos_terminal.php</c> / <c>epc_pos_terminal_markup.php</c> / <c>ajax_pos_endpoint.php</c> twin.
/// </summary>
public sealed class CpPosTerminalTests
{
    private static string Repo(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EcomAE.AspNetCore.sln")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(Path.Combine(dir!.Parent!.FullName, relative));
    }

    [Theory]
    [InlineData("AE", "AED")]
    [InlineData("in", "INR")]
    [InlineData("GB", "GBP")]
    [InlineData("UK", "GBP")]
    [InlineData("DE", "EUR")]
    [InlineData("", "AED")]
    [InlineData("ZZ", "AED")]
    public void CurrencyFor_MatchesWorldRateOverrides(string country, string expected)
        => Assert.Equal(expected, CpPosTerminalService.CurrencyFor(country));

    [Fact]
    public void WarehouseLabel_PrefersNameThenCodeThenDefault()
    {
        Assert.Equal("Main", CpPosTerminalService.WarehouseLabel("Main", "WH1"));
        Assert.Equal("WH1", CpPosTerminalService.WarehouseLabel(" ", "WH1"));
        Assert.Equal("Warehouse", CpPosTerminalService.WarehouseLabel(null, ""));
    }

    [Fact]
    public void PickWarehouse_UsesSettingsThenFirstActive()
    {
        var list = new[] { new CpPosTerminalWarehouse(7, "A"), new CpPosTerminalWarehouse(9, "B") };
        Assert.Equal(3, CpPosTerminalService.PickWarehouse(3, list));
        Assert.Equal(7, CpPosTerminalService.PickWarehouse(0, list));
        Assert.Equal(0, CpPosTerminalService.PickWarehouse(0, []));
    }

    [Fact]
    public void Service_QueriesPhpTablesWithParameters()
    {
        var text = Repo("aspnet/src/EcomAE.Platform/Cp/CpPosTerminalService.cs");
        Assert.Contains("`epc_pos_settings`", text, StringComparison.Ordinal);
        Assert.Contains("`epc_pos_sessions` WHERE `status` = 'open'", text, StringComparison.Ordinal);
        Assert.Contains("FROM `epc_pos_sales` WHERE `time_created` >= ? AND `status` = 'completed'", text, StringComparison.Ordinal);
        Assert.Contains("`epc_erp_inv_warehouses` WHERE `active` = 1", text, StringComparison.Ordinal);
        Assert.Contains("ErpDb.AddParameters(c, since)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\" + since", text, StringComparison.Ordinal);
        Assert.Contains("ICpPosTerminalService, EcomAE.Platform.Cp.CpPosTerminalService", Repo("aspnet/src/EcomAE.Platform/Program.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void Routes_ExposeTerminalAjaxAndReceiptOpen()
    {
        Assert.Equal("/cp/pos/terminal-ajax", EcomAeRoutes.CpPosTerminalAjax);
        Assert.Equal("/cp/pos/receipt", EcomAeRoutes.CpPosTerminalReceiptOpen);
    }

    [Fact]
    public void Dispatcher_CoversEveryPhpAjaxAction()
    {
        var text = Repo("aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs");
        var start = text.IndexOf("EcomAeRoutes.CpPosTerminalAjax", StringComparison.Ordinal);
        Assert.True(start > 0);
        var body = text.Substring(start, 9000);
        foreach (var action in new[] { "search_products", "search_customers", "calc_cart", "open_session", "close_session", "complete_sale", "session_status", "save_settings" })
        {
            Assert.Contains("case \"" + action + "\":", body, StringComparison.Ordinal);
        }

        Assert.Contains("\"Unknown action\"", body, StringComparison.Ordinal);
        Assert.Contains("\"Access denied\"", body, StringComparison.Ordinal);
        Assert.Contains("sale_id = r.Id", body, StringComparison.Ordinal);
        Assert.Contains("ICpPosTerminalService terminal", body, StringComparison.Ordinal);
        Assert.Contains("Results.Redirect(\"/cp/pos/receipt/\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_RendersPhpTerminalMarkupAndBridgesPhpAssets()
    {
        var text = Repo("aspnet/src/EcomAE.Platform/Components/Pages/CpPosOverviewApp.razor");
        foreach (var id in new[]
                 {
                     "epc-pos-app", "epc-pos-q", "epc-pos-search-btn", "epc-pos-products", "epc-pos-customer-q", "epc-pos-customer-walkin",
                     "epc-pos-cart", "epc-pos-subtotal", "epc-pos-discount", "epc-pos-vat", "epc-pos-total", "epc-pos-pay-mode",
                     "epc-pos-tendered", "epc-pos-change", "epc-pos-cash-amt", "epc-pos-card-amt", "epc-pos-notes", "epc-pos-checkout",
                     "epc-pos-clear", "epc-pos-warehouse", "epc-pos-msg",
                 })
        {
            Assert.Contains("id=\"" + id + "\"", text, StringComparison.Ordinal);
        }

        Assert.Contains("id=\"epc-pos-open-session\"", text, StringComparison.Ordinal);
        Assert.Contains("id=\"epc-pos-close-session\"", text, StringComparison.Ordinal);
        foreach (var attr in new[] { "data-ajax-url", "data-pos-url", "data-session-id", "data-session-open", "data-currency", "data-tax-label", "data-tax-rate", "data-warehouse-id" })
        {
            Assert.Contains(attr + "=\"", text, StringComparison.Ordinal);
        }

        Assert.Contains("/platform-assets/epc_pos.css", text, StringComparison.Ordinal);
        Assert.Contains("/platform-assets/epc_pos_terminal.js", text, StringComparison.Ordinal);
        Assert.Contains("POS is disabled for this tenant", text, StringComparison.Ordinal);
        Assert.Contains("session.Kind == LegacySessionKind.Admin && session.Capabilities.Contains(\"cp\")", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic twin", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);

        var bridge = Repo("aspnet/src/EcomAE.Platform/Presentation/PhpLegacyAssetBridge.cs");
        Assert.Contains("(\"/platform-assets/epc_pos.css\",", bridge, StringComparison.Ordinal);
        Assert.Contains("\"content/shop/pos/epc_pos.css\"", bridge, StringComparison.Ordinal);
        Assert.Contains("\"content/shop/pos/epc_pos_terminal.js\"", bridge, StringComparison.Ordinal);
    }

    [Fact]
    public void PhpTerminalJs_UsesOnlyKeysTheDispatcherReturns()
    {
        var js = Repo("content/shop/pos/epc_pos_terminal.js");
        foreach (var key in new[] { "res.products", "res.customers", "res.totals", "res.sale_id", "res.sale_no", "res.status", "res.message" })
        {
            Assert.Contains(key, js, StringComparison.Ordinal);
        }

        Assert.Contains("'?action=receipt&sale_id='", js, StringComparison.Ordinal);
    }
}
