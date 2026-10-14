using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPageSpeedTests
{
    [Fact]
    public void HotReadCache_RoundTripsUntilTheTtlPasses()
    {
        var key = "speed-" + Guid.NewGuid().ToString("N");
        Assert.False(ErpHotReadCache.TryGet(key, out int missing));
        Assert.Equal(0, missing);

        ErpHotReadCache.Set(key, 7, TimeSpan.FromMilliseconds(-1));
        Assert.False(ErpHotReadCache.TryGet(key, out int expired));
        Assert.Equal(0, expired);

        ErpHotReadCache.Set(key, 7, TimeSpan.FromMinutes(1));
        Assert.True(ErpHotReadCache.TryGet(key, out int value));
        Assert.Equal(7, value);
        Assert.False(ErpHotReadCache.TryGet(string.Empty, out int blank));
        Assert.Equal(0, blank);
    }

    [Fact]
    public void PaintBatch_OmitsTablesTheTenantDoesNotHave()
    {
        var lean = ErpDashboardPaintSql.Batch(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "epc_erp_sales_orders", "epc_erp_gl_journals" },
            includeShop: true,
            includePeriod: true);
        Assert.Contains("epc_erp_sales_orders", lean, StringComparison.Ordinal);
        Assert.Contains("epc_erp_gl_journals", lean, StringComparison.Ordinal);
        Assert.DoesNotContain("shop_orders", lean, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_invoices", lean, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_bills", lean, StringComparison.Ordinal);
        Assert.Contains("AS revenue_ex_vat", lean, StringComparison.Ordinal);
        Assert.Contains("AS cash_position", lean, StringComparison.Ordinal);
        Assert.Contains("'open' AS period_status", lean, StringComparison.Ordinal);

        var shop = ErpDashboardPaintSql.Batch(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "shop_orders", "epc_erp_periods" },
            includeShop: true,
            includePeriod: true);
        Assert.Contains("price_total_wt", shop, StringComparison.Ordinal);
        Assert.Contains("epc_erp_periods", shop, StringComparison.Ordinal);

        var retry = ErpDashboardPaintSql.Batch(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "shop_orders", "epc_erp_periods", "epc_erp_purchase_orders" },
            includeShop: false,
            includePeriod: false);
        Assert.DoesNotContain("price_total_wt", retry, StringComparison.Ordinal);
        Assert.DoesNotContain("FROM `epc_erp_periods`", retry, StringComparison.Ordinal);
        Assert.Contains("epc_erp_purchase_orders", retry, StringComparison.Ordinal);
    }

    [Fact]
    public void ColumnEnsure_SkipsInformationSchemaAfterTheFirstSuccess()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpLazySchema.cs"));
        var start = text.IndexOf("public static async Task AddColumnIfMissingAsync", StringComparison.Ordinal);
        Assert.True(start > 0);
        var body = text[start..(start + 700)];
        Assert.Contains("ErpSchemaMemo.IsReady", body, StringComparison.Ordinal);
        Assert.Contains("ErpSchemaMemo.Remember", body, StringComparison.Ordinal);
        Assert.Contains("ColumnKnownAsync", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Favourites_DoNotScanInformationSchemaOnEveryPage()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpShortcutReadService.cs"));
        Assert.Contains("table:epc_user_shortcuts", text, StringComparison.Ordinal);
        Assert.Contains("ErpSchemaMemo.IsReady", text, StringComparison.Ordinal);
        Assert.Contains("ErpTableCatalog.LoadAsync", text, StringComparison.Ordinal);
        Assert.DoesNotContain("information_schema.TABLES", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Home_ReusesTheBatchWhenFirstPaintIsActive()
    {
        var reporter = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs"));
        var homeStart = reporter.IndexOf(
            "public async Task<ErpWorkspaceHomeDigest> BuildErpWorkspaceHomeAsync",
            StringComparison.Ordinal);
        var homeEnd = reporter.IndexOf(
            "private async Task<ErpDashboardSummary> ReadErpDashboardForPeriodAsync",
            homeStart,
            StringComparison.Ordinal);
        Assert.True(homeEnd > homeStart);
        var home = reporter[homeStart..homeEnd];
        Assert.Contains("ExtrasFromSummary", home, StringComparison.Ordinal);
        Assert.Contains("paintFast", home, StringComparison.Ordinal);
        Assert.Contains("ErpHotReadCache", home, StringComparison.Ordinal);
        Assert.Contains("ReadWorkspaceTopSuppliersAsync", home, StringComparison.Ordinal);
        Assert.Contains("skip 12 extra month scans", home, StringComparison.Ordinal);
        Assert.Contains("ErpDashboardPaintSql.Batch", reporter, StringComparison.Ordinal);
        Assert.Contains("ErpHotReadCache", reporter[reporter.IndexOf("BuildErpCompaniesDigestAsync", StringComparison.Ordinal)..], StringComparison.Ordinal);
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
