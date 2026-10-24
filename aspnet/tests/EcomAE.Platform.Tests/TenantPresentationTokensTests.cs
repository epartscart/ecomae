using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class TenantPresentationTokensTests
{
    [Fact]
    public void BrandColorsBecomeScopedCssVariables()
    {
        var tokens = new TenantPresentation("sham", new Dictionary<string, string>
        {
            ["brand_primary"] = "#1e40af",
            ["brand_sidebar_bg"] = "#0f172a",
            ["brand_radius"] = "6px",
        });

        var css = tokens.BuildCss();

        Assert.Contains("--epc-erp-primary:#1e40af", css, StringComparison.Ordinal);
        Assert.Contains("--epc-sidebar-from:#0f172a", css, StringComparison.Ordinal);
        Assert.Contains("--epc-radius:6px", css, StringComparison.Ordinal);
        Assert.Contains(".epc-tenant-tokens", css, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsafeValuesAreDropped()
    {
        var tokens = new TenantPresentation("sham", new Dictionary<string, string>
        {
            ["brand_primary"] = "url(javascript:alert(1))",
            ["brand_radius"] = "6px;position:fixed",
            ["brand_logo_url"] = "javascript:evil",
            ["brand_font"] = "x'\";body{display:none}",
        });

        var css = tokens.BuildCss();

        Assert.DoesNotContain("javascript", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("position:fixed", css, StringComparison.Ordinal);
        Assert.DoesNotContain("display:none", css, StringComparison.Ordinal);
    }

    [Fact]
    public void DensityAndFlagsEmitRulesAndAttributes()
    {
        var tokens = new TenantPresentation("sham", new Dictionary<string, string>
        {
            ["ui_density"] = "compact",
            ["ui_nav_label_mode"] = "short",
            ["ui_form_layout"] = "split",
            ["ui_show_help"] = "0",
            ["ui_dashboard_style"] = "executive",
        });

        var css = tokens.BuildCss();
        var attrs = tokens.Attributes();

        Assert.Contains("data-epc-density='compact'", css, StringComparison.Ordinal);
        Assert.Contains("epc-help", css, StringComparison.Ordinal);
        Assert.Equal("compact", attrs["data-epc-density"]);
        Assert.Equal("split", attrs["data-epc-form-layout"]);
        Assert.Equal("short", attrs["data-epc-nav-labels"]);
        Assert.Equal("executive", attrs["data-epc-dash-style"]);
    }

    [Fact]
    public void InvalidEnumValuesEmitNothing()
    {
        var tokens = new TenantPresentation("sham", new Dictionary<string, string>
        {
            ["ui_density"] = "huge",
            ["ui_form_layout"] = "weird",
            ["ui_nav_label_mode"] = "tiny",
        });

        Assert.Empty(tokens.Attributes());
        Assert.DoesNotContain("data-epc-density", tokens.BuildCss(), StringComparison.Ordinal);
    }
}
