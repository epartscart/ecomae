using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// cp/content/shop/returns (router.php, returns_list.php, return_detail.php, reasons_statuses.php,
/// ajax/ajax_return_action.php) twin: /cp/returns-app must keep the PHP router pages, action names,
/// DOM ids, filters and automation semantics.
/// </summary>
public sealed class CpReturnsDeskPhpParityTests
{
    private static readonly string[] AjaxActions = ["set_return_status", "decide_line", "finalize_return"];
    private static readonly string[] SetupActions = ["add_reason", "add_status"];

    [Fact]
    public void Endpoint_CoversEveryPhpAction_AndKeepsCpAdminGate()
    {
        var ajax = File.ReadAllText(FindRepoFile("cp/content/shop/returns/ajax/ajax_return_action.php"));
        var setup = File.ReadAllText(FindRepoFile("cp/content/shop/returns/reasons_statuses.php"));
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        var start = module.IndexOf("MapPost(EcomAeRoutes.CpReturnAction", StringComparison.Ordinal);
        Assert.True(start > 0);
        var endpoint = module[start..module.IndexOf("MapPost(EcomAeRoutes.CpSetUsersVinViewed", start, StringComparison.Ordinal)];

        foreach (var action in AjaxActions)
        {
            Assert.Contains("$action === '" + action + "'", ajax, StringComparison.Ordinal);
            Assert.Contains("\"" + action + "\"", endpoint, StringComparison.Ordinal);
        }

        foreach (var action in SetupActions)
        {
            Assert.Contains("$action === '" + action + "'", setup, StringComparison.Ordinal);
            Assert.Contains("\"" + action + "\"", endpoint, StringComparison.Ordinal);
        }

        Assert.Equal("/cp/returns/action", EcomAeRoutes.CpReturnAction);
        Assert.Equal("/cp/returns-app", EcomAeRoutes.ControlPanelReturnsApp);
        Assert.Contains("session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains(\"cp\")", endpoint, StringComparison.Ordinal);
        Assert.Contains("status = written.Succeeded", endpoint, StringComparison.Ordinal);
        Assert.Contains("?page=detail&return_id=", endpoint, StringComparison.Ordinal);
        Assert.Contains("?page=reasons_statuses&action=select", endpoint, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_KeepsPhpRouterPages_DomIds_FiltersAndCopy()
    {
        var razor = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpReturnsApp.razor"));
        Assert.Contains("@page \"/cp/returns-app\"", razor, StringComparison.Ordinal);
        Assert.Contains("@layout Layout.PhpChromeLayout", razor, StringComparison.Ordinal);
        Assert.Contains("<PhpChromeStyles Surface=\"cp\" />", razor, StringComparison.Ordinal);

        // router.php pages and return_id => detail shortcut
        Assert.Contains("_page == \"reasons_statuses\"", razor, StringComparison.Ordinal);
        Assert.Contains("_page == \"detail\"", razor, StringComparison.Ordinal);
        Assert.Contains("if (_returnId > 0 && _page == \"list\")", razor, StringComparison.Ordinal);
        Assert.Contains("?page=reasons_statuses&action=select", razor, StringComparison.Ordinal);

        // returns.php panel_a strip
        foreach (var caption in new[] { "Returns manager", "Reasons &amp; statuses", "Exit" })
        {
            Assert.Contains("<div class=\"panel_a_caption\">" + caption + "</div>", razor, StringComparison.Ordinal);
        }

        // returns_list.php filters, columns, copy
        Assert.Contains("Returns / refund requests", razor, StringComparison.Ordinal);
        Assert.Contains("Return process ready.", razor, StringComparison.Ordinal);
        foreach (var filter in new[] { "status_id", "order_id", "user_id" })
        {
            Assert.Contains("name=\"" + filter + "\"", razor, StringComparison.Ordinal);
        }

        Assert.Contains("<th>Return</th><th>Against order</th><th>Customer</th><th>Status</th><th>Lines</th><th>Sum</th>", razor, StringComparison.Ordinal);
        Assert.Contains("style=\"background:@r.StatusColor;\"", razor, StringComparison.Ordinal);
        Assert.Contains(">Reset</a>", razor, StringComparison.Ordinal);

        // return_detail.php DOM contract
        Assert.Contains("id=\"epc-return-status-form\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-ret-decide", razor, StringComparison.Ordinal);
        Assert.Contains("id=\"epc-ret-finalize\"", razor, StringComparison.Ordinal);
        Assert.Contains("data-line=\"@ln.Id\" data-decide=\"1\"", razor, StringComparison.Ordinal);
        Assert.Contains("data-line=\"@ln.Id\" data-decide=\"0\"", razor, StringComparison.Ordinal);
        Assert.Contains("Close this return? Approved lines move to Return approved; denied lines to Return rejected.", razor, StringComparison.Ordinal);
        Assert.Contains("Declared sum", razor, StringComparison.Ordinal);

        // reasons_statuses.php
        Assert.Contains("Return process &amp; automation", razor, StringComparison.Ordinal);
        Assert.Contains("<th>Item status</th><th>Can request return</th><th>In return</th><th>Approved</th><th>Rejected</th>", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"add_reason\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"add_status\"", razor, StringComparison.Ordinal);
        Assert.Contains("type=\"color\" name=\"color\"", razor, StringComparison.Ordinal);

        Assert.DoesNotContain("PhpParityModuleBody", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadService_UsesParameterizedSql_AndPhpLimit()
    {
        var svc = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Cp/CpReturnsDeskService.cs"));
        Assert.Equal(300, EcomAE.Platform.Cp.CpReturnsDeskService.ListLimit);
        Assert.Contains("`shop_orders_returns`", svc, StringComparison.Ordinal);
        Assert.Contains("`shop_orders_returns_items`", svc, StringComparison.Ordinal);
        Assert.Contains("`shop_orders_returns_statuses`", svc, StringComparison.Ordinal);
        Assert.Contains("`shop_orders_returns_reasons`", svc, StringComparison.Ordinal);
        Assert.Contains("`shop_orders_messages`", svc, StringComparison.Ordinal);
        Assert.Contains("EnsureAutomationAsync", svc, StringComparison.Ordinal);
        Assert.DoesNotContain("$\"SELECT", svc, StringComparison.Ordinal);
        Assert.DoesNotContain("$\"\"\"", svc, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteService_KeepsPhpFinalizeGuard_ApprovedSum_AndOrderLogs()
    {
        var php = File.ReadAllText(FindRepoFile("cp/content/shop/returns/ajax/ajax_return_action.php"));
        var svc = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Cp/CpReturnWriteService.cs"));

        Assert.Contains("return_success", php, StringComparison.Ordinal);
        Assert.Contains("`return_success` IS NULL", svc, StringComparison.Ordinal);
        Assert.Contains("SUM(oi.`price` * oi.`count_need`)", svc, StringComparison.Ordinal);
        Assert.Contains("`shop_orders_logs`", svc, StringComparison.Ordinal);
        Assert.Contains("return_complete", svc, StringComparison.Ordinal);

        Assert.Contains("(\"Created\", \"#dae1dd\")", svc, StringComparison.Ordinal);
        Assert.Contains("(\"Under consideration\", \"#f5f3cc\")", svc, StringComparison.Ordinal);
        Assert.Contains("(\"Closed\", \"#26ad5f\")", svc, StringComparison.Ordinal);
        Assert.Contains("Return approved", svc, StringComparison.Ordinal);
        Assert.Contains("Return rejected", svc, StringComparison.Ordinal);
        Assert.Contains("AddReasonAsync", svc, StringComparison.Ordinal);
        Assert.Contains("AddStatusAsync", svc, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/CP/shop/returns-manager", "/cp/returns-app")]
    [InlineData("/CP/shop/returns", "/cp/returns-app")]
    [InlineData("/CP/shop/returns-manager?page=detail&return_id=8", "/cp/returns-app?page=detail&return_id=8")]
    [InlineData("/CP/shop/returns-manager?return_id=12", "/cp/returns-app?page=detail&return_id=12")]
    [InlineData("/CP/shop/returns-manager?page=reasons_statuses&action=select", "/cp/returns-app?page=reasons_statuses&action=select")]
    [InlineData("/CP/shop/finance/epc_warranty_rma?rma_id=6", "/cp/returns-rma-app?rma_id=6")]
    public void PhpReturnsLinks_MapToTwin_AndKeepAftersalesAlias(string php, string expected)
    {
        Assert.Equal(expected, PhpSurfaceLinkMap.AspNetPrimaryHref(php));
    }

    [Fact]
    public void ShopModuleRoute_AndNav_PointReturnsAtTwin()
    {
        Assert.True(CpShopModuleRouteMap.TryMap("returns", out var href));
        Assert.Equal("/cp/returns-app", href);
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, l => l.Href == "/cp/returns-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, l => l.Href == "/cp/returns-rma-app");
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
