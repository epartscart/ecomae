using EcomAE.Platform.Presentation;
using EcomAE.Platform.Surfaces;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacyHtmlShellRendererTests
{
    [Fact]
    public void RenderReusesPhpCpStylesheetsAndBrandMark()
    {
        var shell = new MigrationSurfaceShellCatalog().Build("cp", null);
        var html = new LegacyHtmlShellRenderer().Render(
            "cp",
            shell,
            new { kind = "Admin", user_id = 1 },
            "test note");

        Assert.Contains("ECOM AE", html, StringComparison.Ordinal);
        Assert.Contains("/content/general_pages/epc_ecomae_logo_svg.php", html, StringComparison.Ordinal);
        Assert.Contains("/content/general_pages/epc_cp_professional_css.php", html, StringComparison.Ordinal);
        Assert.Contains("/epc-static.php?f=cp/templates/bootstrap_admin/styles/style.css", html, StringComparison.Ordinal);
        Assert.Contains("presentation-shell-scaffolded", html, StringComparison.Ordinal);
        Assert.Contains("data-epc-surface=\"cp\"", html, StringComparison.Ordinal);
        Assert.Contains("?format=json", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>alert", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderBosLinksBosShellCss()
    {
        var shell = new MigrationSurfaceShellCatalog().Build("bos", null);
        var html = new LegacyHtmlShellRenderer().Render("bos", shell, null);

        Assert.Contains("/epc-static.php?f=bos/epc_bos_shell.css", html, StringComparison.Ordinal);
        Assert.Contains("data-epc-surface=\"bos\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderStorefrontLinksModexAssets()
    {
        var shell = new MigrationSurfaceShellCatalog().Build("storefront", null);
        var html = new LegacyHtmlShellRenderer().Render("storefront", shell, null);

        Assert.Contains("/templates/modex/assets/css/style_color.css", html, StringComparison.Ordinal);
        Assert.Contains("/templates/modex/css/catalogue/catalogue.css", html, StringComparison.Ordinal);
    }
}
