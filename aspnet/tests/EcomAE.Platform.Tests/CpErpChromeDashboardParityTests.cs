using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guardrails so CP/ERP chrome stay aligned with PHP topnav (px fonts, click panels)
/// and dashboards keep NetSuite / Command Centre structural markers.
/// </summary>
public sealed class CpErpChromeDashboardParityTests
{
    [Fact]
    public void CpTopnavUsesPhpBrandAndClickToggle()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor"));
        Assert.Contains("<span>Control</span>", src);
        Assert.Contains("fa-th-large", src);
        Assert.Contains("data-topnav-toggle=", src);
        Assert.Contains("hidden data-topnav-panel=", src);
        Assert.Contains("epc-cp-topnav-caret", src);
        Assert.Contains("bindCpTopNav", src);
        Assert.DoesNotContain("epc-cp-topnav-item:hover .epc-cp-topnav-panel", src);
        Assert.DoesNotContain("font-size:.78rem", src);
        Assert.DoesNotContain(">CONTROL<", src);
    }

    [Fact]
    public void ErpTopnavUsesPhpBrandClickToggleAndPxShell()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor"));
        Assert.Contains("fa-cubes", src);
        Assert.Contains("BrandLabel", src);
        Assert.Contains("data-topnav-toggle=", src);
        Assert.Contains("hidden data-topnav-panel=", src);
        Assert.Contains("bindErpTopNav", src);
        Assert.Contains("GroupIsActive", src);
        Assert.Contains("is-active", src);
        Assert.DoesNotContain("epc-erp-topnav-item:hover .epc-erp-topnav-panel", src);
        Assert.DoesNotContain("font-size:.8rem", src);
        Assert.True(
            src.Contains("font-size:14px", StringComparison.Ordinal)
            || src.Contains("LegacyPhpFontAssets.BaseFontSize", StringComparison.Ordinal),
            "ERP chrome must use unified 14px base font size");
        Assert.Contains("LegacyPhpFontAssets.StackFor(\"erp\")", src);
        Assert.Contains("font-size:22px", src);
    }

    [Fact]
    public void CpCommandCentreUsesCommerceKpisNotAdminTenantPulse()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpCommandCentreApp.razor"));
        Assert.Contains("Orders today", src);
        Assert.Contains("Open orders", src);
        Assert.Contains("Warehouse qty", src);
        Assert.Contains("Vendors", src);
        Assert.Contains("Clients", src);
        Assert.DoesNotContain("Admin users", src);
        Assert.DoesNotContain("Portal tenants", src);
        Assert.Contains("PhpHybridModuleDirectory", src);
        Assert.Contains("epc-insights--cp", src);
        Assert.Contains("Unread VIN / parts requests", src);
        Assert.Contains("Open returns", src);
        Assert.Contains("Finance pulse (MTD)", src);
        Assert.Contains("id=\"cpDashOrdersChart\"", src);
        Assert.Contains("cp-dash-more", src);
        Assert.Contains("More tools", src);
        Assert.Contains("cp-dash-help", src);
        Assert.Contains("All warehouse SKUs (S-UAE, R-UAE", src);
        Assert.Contains("Supplier warehouse stock (all price lists)", src);
        Assert.Contains("fa-car", src);
        // Shortcuts are the PHP epc_dash_shortcuts_ui.php .eds-* grid + editor over persisted rows.
        Assert.Contains("eds-live-grid", src);
        Assert.Contains("data-key=\"@tile.Key\"", src);
        Assert.Contains("LoadShortcutsAsync", src);
        Assert.Contains("shortcut_delete_key", src);
        Assert.Contains("shortcut_reset", src);
        Assert.DoesNotContain("\"/cp/portal-settings-app\"", src);

        var catalog = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Cp/CpShortcutCatalog.cs"));
        Assert.Contains("\"crosses\"", catalog);
        Assert.Contains("\"multivendor\"", catalog);
    }

    [Fact]
    public void TenantCpChromeStampsIndustryAndStorefrontHost()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor"));
        Assert.Contains("epc-cp--blue-theme", src);
        Assert.Contains("ShellClass", src);
        Assert.Contains("epc-cp-topbar-cta__host", src);
        Assert.Contains("_storefrontHost", src);
        Assert.Contains("id=\"epc-cp-header-breadcrumb\"", src);
        Assert.Contains("id=\"mobile-collapse\"", src);
        Assert.Contains("class=\"splash\"", src);
        Assert.Contains("splash-title", src);
        Assert.Contains("epcHideCpSplash", src);
        Assert.Contains("epc-cp-header-icons", src);
        Assert.Contains("epc-cp-top-connectivity--ssl", src);
        Assert.Contains("epc-cp-industry-toggle", src);
        Assert.Contains("epc-cp-topnav-item--advanced", src);
        Assert.Contains("epc-cp-topnav-panel-sub", src);
        Assert.Contains("GroupIsActive", src);
    }

    [Fact]
    public void ErpHomeUsesNetSuiteDashChartsAndGauge()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpBosDashboardApp.razor"));
        Assert.Contains("class=\"ns-dash\"", src);
        Assert.Contains("ns-hero", src);
        Assert.Contains("ns-gauge", src);
        Assert.Contains("id=\"nsChartAr\"", src);
        Assert.Contains("id=\"nsChartTrend\"", src);
        Assert.Contains("chart.js@4.4.1", src);
        Assert.Contains("A/R aging", src);
        Assert.Contains("ns-qa-grid", src);
        Assert.Contains("Area workspaces", src);
        Assert.Contains("ErpCapabilityCatalog.Groups", src);
        Assert.Contains("WorkspaceVisible", src);
        Assert.Contains("Executive cockpit", src);
        Assert.Contains("Planning alerts", src);
        Assert.Contains("Top suppliers by spend", src);
        Assert.Contains("Task analytics", src);
        Assert.Contains("Industry intelligence", src);
        Assert.Contains("No supplier spend recorded yet.", src);
        Assert.Contains("No tasks tracked yet.", src);
        Assert.Contains("PhpHybridModuleDirectory", src);
        Assert.Contains("epc-dash-customize", src);
        Assert.Contains("dash_profile=", src);
        Assert.DoesNotContain("epc-erp-banner", src);
    }

    [Fact]
    public void ErpDashboardKeepsColorfulTilesAndD365WorkspaceModules()
    {
        var css = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/wwwroot/css/epc-enterprise-design-system.css"));
        var directory = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/PhpHybridModuleDirectory.razor"));
        var assets = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Presentation/LegacyPresentationAssets.cs"));

        Assert.Contains("a.ns-tile.gold { background: linear-gradient(145deg, #1d4f91, #163a6b) !important; }", css, StringComparison.Ordinal);
        Assert.Contains("a.ns-tile.green { background: linear-gradient(145deg, #0f766e, #0b5c56) !important; }", css, StringComparison.Ordinal);
        Assert.Contains("a.ns-tile.rust { background: linear-gradient(145deg, #334155, #1e293b) !important; }", css, StringComparison.Ordinal);
        Assert.Contains("a.ns-tile.slate { background: linear-gradient(145deg, #b45309, #92400e) !important; }", css, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: repeat(7, minmax(0, 1fr))", css, StringComparison.Ordinal);
        Assert.Contains("a.epc-d365-ws:nth-child(7n+1) { background: #eef4ff !important; border-left-color: #2563eb; }", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".ns-tile,\n.epc-erp-content-body .ns-port", css.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.DoesNotContain("a.epc-d365-ws { display:flex; align-items:center; gap:10px; min-height:52px; padding:8px 12px; background:#fff;", directory.Replace("\r\n", "\n"), StringComparison.Ordinal);

        Assert.Contains("epc-d365-ws", directory, StringComparison.Ordinal);
        Assert.Contains("data-epc-moddir-style=\"d365-fno\"", directory, StringComparison.Ordinal);
        Assert.Contains("epc-moddir-aspnet", directory, StringComparison.Ordinal);
        Assert.Contains("background:#eef4ff !important", directory, StringComparison.Ordinal);
        Assert.Contains("data-epc-dash=\"color-tiles\"", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpBosDashboardApp.razor")), StringComparison.Ordinal);
        Assert.Contains("ErpGlobalSearch", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor")), StringComparison.Ordinal);
        Assert.Contains("Search pages and workspaces", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/ErpGlobalSearch.razor")), StringComparison.Ordinal);
        Assert.Contains("v=20261012home", assets, StringComparison.Ordinal);
        Assert.DoesNotContain("v=20261012prja", assets, StringComparison.Ordinal);
        Assert.DoesNotContain("v=20261012wscolor", assets, StringComparison.Ordinal);

        var chrome = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor"));
        Assert.Contains("color:#0f172a !important", chrome, StringComparison.Ordinal);
        Assert.Contains(".epc-erp-global-search__input::placeholder", chrome, StringComparison.Ordinal);
        Assert.Contains(".ns-dash) .epc-module-visual-frame", chrome, StringComparison.Ordinal);

        var dash = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpBosDashboardApp.razor"));
        Assert.Contains("epc-dash-repeat", dash, StringComparison.Ordinal);
        Assert.Contains("Title=\"All modules\"", dash, StringComparison.Ordinal);
        Assert.DoesNotContain("Title=\"Workspaces\"", dash, StringComparison.Ordinal);
        Assert.DoesNotContain("Data scope:", dash, StringComparison.Ordinal);

        Assert.Contains("color: #0f172a !important", css, StringComparison.Ordinal);
        Assert.Contains(".ns-dash .epc-dash-repeat", css, StringComparison.Ordinal);
    }

    [Fact]
    public void FitOutDashboardPresentsCompanyScopedProjectModules()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpBosDashboardApp.razor"));
        var projectAccounting = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpProjectAccountingApp.razor"));
        Assert.Contains("data-epc-industry-pack=\"fitout_contracting\"", src);
        Assert.Contains("data-epc-industry-first=\"jewellery\"", src);
        Assert.Contains("data-epc-industry-first=\"fitout\"", src);
        Assert.True(
            src.IndexOf("data-epc-industry-first=\"jewellery\"", StringComparison.Ordinal)
            < src.IndexOf("<div class=\"ns-hero\">", StringComparison.Ordinal));
        Assert.Contains("Fit-out project modules", src);
        Assert.Contains("FitOutHref(\"/erp/fitout/delivery-dashboard\")", src);
        Assert.Contains("FitOutHref(\"/erp/fitout/finance-operations-report\")", src);
        Assert.Contains("FitOutHref(\"/erp/fitout/approval-queue\")", src);
        Assert.Contains("Every fit-out entry keeps the selected legal-entity scope", src);
        Assert.Contains("private string FitOutHref", src);
        Assert.Contains("FITOUT · Fit-out Company · Company 3", projectAccounting);
        Assert.Contains("action=\"@FitOutHref(\"/erp/fitout/project-pnl\")\"", projectAccounting);
        Assert.Contains("Report links retain the selected legal entity", projectAccounting);
        Assert.Contains("data-epc-prja=\"workspace\"", projectAccounting);
        Assert.Contains("WorkHref(\"commercial\")", projectAccounting);
        Assert.Contains("epc-prja-reports", projectAccounting);
        Assert.Contains("<optgroup label=\"Site / QA\">", projectAccounting);
    }

    [Fact]
    public void DocumentWorkspaceUsesSharedHeaderActionsAndSummaryRail()
    {
        var workspace = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/ErpDocumentWorkspace.razor"));
        var css = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/wwwroot/css/erp-document-workspace.css"));
        var sales = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpSalesOrdersApp.razor"));
        var purchase = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpPurchaseOrdersApp.razor"));
        var invoices = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpInvoicesApp.razor"));

        Assert.Contains("epc-document-workspace__summary", workspace);
        Assert.Contains("ErpRelatedDocumentMap", workspace);
        Assert.Contains("RelatedDocuments", workspace);
        Assert.Contains("epc-document-workspace__actions", workspace);
        Assert.Contains("HeaderActions", workspace);
        Assert.Contains("CompanyContext", workspace);
        Assert.Contains("epc-document-workspace__summary", css);
        Assert.Contains("epc-document-workspace__actions", css);
        Assert.Contains("SalesOrderHeaderActions", sales);
        Assert.Contains("PurchaseOrderHeaderActions", purchase);
        Assert.Contains("InvoiceHeaderActions", invoices);
        Assert.Contains("HeaderActions=\"@SalesOrderHeaderActions\"", sales);
        Assert.Contains("HeaderActions=\"@PurchaseOrderHeaderActions\"", purchase);
        Assert.Contains("HeaderActions=\"@InvoiceHeaderActions\"", invoices);
    }

    [Fact]
    public void FinanceVoucherWorkspaceKeepsPostingSummaryAndLifecycleActionsTogether()
    {
        var workspace = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/ErpFinanceVoucherWorkspace.razor"));
        var css = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/wwwroot/css/erp-finance-voucher-workspace.css"));
        var cashEntries = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpCashEntriesApp.razor"));
        var journals = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpGlJournalsApp.razor"));

        Assert.Contains("epc-finance-voucher__summary", workspace);
        Assert.Contains("epc-finance-voucher__actions", workspace);
        Assert.Contains("IsBalanced", workspace);
        Assert.Contains("HeaderActions", workspace);
        Assert.Contains("epc-finance-voucher__summary", css);
        Assert.Contains("epc-finance-voucher__actions", css);
        Assert.Contains("PaymentHeaderActions", cashEntries);
        Assert.Contains("HeaderActions=\"@PaymentHeaderActions\"", cashEntries);
        Assert.Contains("ErpCashEntriesVoid", cashEntries);
        Assert.Contains("Delete is unavailable for posted vouchers", cashEntries);
        Assert.Contains("GeneralJournalHeaderActions", journals);
        Assert.Contains("HeaderActions=\"@GeneralJournalHeaderActions\"", journals);
        Assert.Contains("ErpGlJournalsReverse", journals);
    }

    [Fact]
    public void DesktopChromeCatalogStillDocumentsTopnavSelectors()
    {
        Assert.Contains(".epc-cp-topnav", LegacyDesktopChromeCatalog.RequiredStructuralSelectors("cp"));
        Assert.Contains(".epc-erp-topnav-panel-hub", LegacyDesktopChromeCatalog.RequiredStructuralSelectors("erp"));
    }

    [Fact]
    public void CpTopnavInlineScriptUsesPointerdownToggle()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor"));
        Assert.Contains("pointerdown", src);
        Assert.Contains("aria-expanded", src);
    }

    [Fact]
    public void CpIndustryPacksApp_KeepsStylesInHeadContentNotInsideChrome()
    {
        // Live HTML pipelines have stripped body <style> tags and left CSS as visible text
        // (e.g. /cp/industry-packs-app showing .epc-pack-hero { ... }). Styles must use HeadOutlet.
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpIndustryPacksApp.razor"));
        Assert.Contains("<HeadContent>", src, StringComparison.Ordinal);
        Assert.Contains(".epc-pack-hero", src, StringComparison.Ordinal);
        var styleAt = src.IndexOf("<style>", StringComparison.Ordinal);
        var chromeAt = src.IndexOf("<PhpCpDesktopChrome", StringComparison.Ordinal);
        Assert.True(styleAt >= 0 && chromeAt > styleAt, "page styles must appear before PhpCpDesktopChrome");
        Assert.DoesNotContain("<style>", src[chromeAt..], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpBosDesktopChrome.razor")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Layout/PhpChromeLayout.razor")]
    public void DesktopChrome_EmitsInlineStylesInHeadAndBodyStream(string relative)
    {
        // Live SSR drops HeadContent on /cp|/erp|/bos login + shells; body <style> is the durable path.
        var src = File.ReadAllText(FindRepoFile(relative));
        Assert.Contains("<HeadContent>", src, StringComparison.Ordinal);
        Assert.Contains("<style>", src, StringComparison.Ordinal);
        var open = src.IndexOf("<HeadContent>", StringComparison.Ordinal);
        var close = src.IndexOf("</HeadContent>", StringComparison.Ordinal);
        Assert.True(open >= 0 && close > open);
        var head = src.Substring(open, close - open);
        Assert.Contains("<style>", head, StringComparison.Ordinal);
        var after = src[(close + "</HeadContent>".Length)..];
        Assert.Contains("<style>", after, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor", "ecomae-cp-chrome-css")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor", "ecomae-erp-chrome-css")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpBosDesktopChrome.razor", "ecomae-bos-chrome-css")]
    public void DesktopChrome_MarksBodyInlineCssFallback(string relative, string marker)
    {
        var src = File.ReadAllText(FindRepoFile(relative));
        Assert.Contains(marker, src, StringComparison.Ordinal);
        Assert.Contains("body-inline", src, StringComparison.Ordinal);
    }

    [Fact]
    public void StorefrontChrome_EmitsCriticalCssInBodyStreamForLiveProve()
    {
        // Live FORCE_LIVE prove saw HeadContent dropped for /storefront/app while
        // body <style> widgets survived — critical header CSS must live in body.
        var src = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor"));
        Assert.Contains("<HeadContent>", src, StringComparison.Ordinal);
        Assert.Contains("body-inline", src, StringComparison.Ordinal);
        var close = src.IndexOf("</HeadContent>", StringComparison.Ordinal);
        Assert.True(close > 0);
        var after = src[(close + "</HeadContent>".Length)..];
        Assert.Contains("<style>", after, StringComparison.Ordinal);
        Assert.Contains("header-call-box a { background:#ef4444", after, StringComparison.Ordinal);
        Assert.Contains("background:linear-gradient(135deg,#090f1d", after, StringComparison.Ordinal);
        Assert.Contains("color:rgba(255,255,255,.88) !important", after, StringComparison.Ordinal);
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

        throw new FileNotFoundException($"Could not locate {relative}");
    }
}
