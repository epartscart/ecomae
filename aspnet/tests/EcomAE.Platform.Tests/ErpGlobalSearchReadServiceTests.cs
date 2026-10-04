using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpGlobalSearchReadServiceTests
{
    [Fact]
    public void MatchModulesIsCaseInsensitiveOnTabOrAreaLabelAndSkipsExternalLinks()
    {
        var nav = LegacyDesktopChromeCatalog.ErpTopnav();
        var rows = ErpGlobalSearchReadService.MatchModules("JOURNAL", nav);
        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Equal("module", r["type"]));
        Assert.All(rows, r => Assert.True(
            ((string)r["label"]!).Contains("journal", StringComparison.OrdinalIgnoreCase)
            || ((string)r["sub"]!).Contains("journal", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(ErpGlobalSearchReadService.MatchModules("Procurement CP", nav), r => (string)r["tab"]! == "procurement_link");
    }

    [Fact]
    public void TabUrlAndTabToAreaMatchPhpHelpers()
    {
        var nav = LegacyDesktopChromeCatalog.ErpTopnav();
        Assert.Equal("/cp/content/shop/finance/erp/erp_main_page.php?area=ar&tab=receivables&from=&to=", ErpGlobalSearchReadService.TabUrl("receivables", "ar"));
        Assert.Equal("banking", ErpGlobalSearchReadService.TabToArea("bank_recon", nav));
        Assert.Equal("people", ErpGlobalSearchReadService.TabToArea("hr_ops", nav));
        Assert.Equal("overview", ErpGlobalSearchReadService.TabToArea("no_such_tab", nav));
        Assert.Equal("A & B", ErpGlobalSearchReadService.Plain("<b>A</b> &amp; B"));
    }
}
