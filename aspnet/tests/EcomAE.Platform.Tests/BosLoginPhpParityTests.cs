using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards /bos/login ASP.NET shell against light-theme / stub-login regressions vs PHP bos/index.php.
/// </summary>
public sealed class BosLoginPhpParityTests
{
    [Fact]
    public void LoginBodyClass_IsDarkBosLoginTheme()
    {
        var cls = LegacyPresentationAssets.LoginBodyClassFor("bos");
        Assert.Contains("bos-body", cls, StringComparison.Ordinal);
        Assert.Contains("bos-body--login", cls, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-bos-shell", cls, StringComparison.Ordinal);
    }

    [Fact]
    public void BosLoginApp_MatchesPhpDualCredentialLogin()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/BosLoginApp.razor"));

        Assert.Contains("bos-body--login", text, StringComparison.Ordinal);
        Assert.Contains("epc-login-bos bos-login", text, StringComparison.Ordinal);
        Assert.Contains("PhpBosLoginAtmosphere", text, StringComparison.Ordinal);
        Assert.Contains("IncludeLoginCss=\"true\"", text, StringComparison.Ordinal);
        Assert.Contains("bosParticles", text, StringComparison.Ordinal);
        Assert.Contains("Platform Operator", text, StringComparison.Ordinal);
        Assert.Contains("ERP Customer", text, StringComparison.Ordinal);
        Assert.Contains("Sign In to BOS", text, StringComparison.Ordinal);
        Assert.Contains("Access ERP System", text, StringComparison.Ordinal);
        Assert.Contains("Business Email", text, StringComparison.Ordinal);
        Assert.Contains("bos-login__erp-features", text, StringComparison.Ordinal);
        Assert.Contains("Business Operating System v1.5.0", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.BosLogin", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpLogin", text, StringComparison.Ordinal);
        Assert.Contains("name=\"contact\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"password\"", text, StringComparison.Ordinal);
        Assert.Contains("id=\"bosLoginForm\"", text, StringComparison.Ordinal);
        Assert.Contains("id=\"bosLoginFormErp\"", text, StringComparison.Ordinal);
        Assert.Contains("bos-login__form-wrap--active", text, StringComparison.Ordinal);

        // Must not regress to hybrid stub / light-theme clutter
        Assert.DoesNotContain("LegacyAdminLoginForm", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Sign-in is temporarily unavailable", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bridge is not configured", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bos-login__industries", text, StringComparison.Ordinal);
        Assert.DoesNotContain("bos-login__platform-deck", text, StringComparison.Ordinal);
        // PHP login JS hijacks these forms to /bos/?action=login — keep it off this page
        Assert.DoesNotContain("IncludeBosLoginScripts=\"true\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BosStylesheet_IncludesShellCss()
    {
        var sheets = LegacyPresentationAssets.StylesheetsFor("bos");
        Assert.Contains(sheets, s => s.Contains("bos/epc_bos_shell.css", StringComparison.Ordinal));
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

            var alt = Path.Combine(dir.FullName, "..", "..", "..", "..", "..", relative);
            alt = Path.GetFullPath(alt);
            if (File.Exists(alt))
            {
                return alt;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate {relative}");
    }
}
