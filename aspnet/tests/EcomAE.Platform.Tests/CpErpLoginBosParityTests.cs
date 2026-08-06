using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// /cp/login and /erp/login must match /bos/login shell; only atmosphere accent differs.
/// Applies to every tenant host that serves these product login routes.
/// </summary>
public sealed class CpErpLoginBosParityTests
{
    private static string Read(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }

    [Theory]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/CpLoginApp.razor", "bos-login--cp", "epcCpParticles", "EcomAeRoutes.ControlPanelLogin")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpLoginApp.razor", "bos-login--erp", "erpPortalParticles", "EcomAeRoutes.ErpLogin")]
    public void LoginApp_UsesBosShellStructure(string path, string accentClass, string particlesId, string routeSymbol)
    {
        var text = Read(path);
        Assert.Contains("epc-login-bos bos-login", text, StringComparison.Ordinal);
        Assert.Contains(accentClass, text, StringComparison.Ordinal);
        Assert.Contains("bos-login__hero", text, StringComparison.Ordinal);
        Assert.Contains("bos-login__panel", text, StringComparison.Ordinal);
        Assert.Contains("bos-login__form", text, StringComparison.Ordinal);
        Assert.Contains("PhpBosLoginAtmosphere", text, StringComparison.Ordinal);
        Assert.Contains(particlesId, text, StringComparison.Ordinal);
        Assert.Contains(routeSymbol, text, StringComparison.Ordinal);
        Assert.Contains("name=\"contact\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"password\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("LegacyAdminLoginForm", text, StringComparison.Ordinal);
        Assert.DoesNotContain("eel-login", text, StringComparison.Ordinal);
        Assert.DoesNotContain("EnterpriseLogin3dScene", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_enterprise_login_3d.css", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AccentCss_ExistsForCpAndErp()
    {
        var css = Read("content/general_pages/epc_bos_login_surface_accents.css");
        Assert.Contains(".bos-login--cp", css, StringComparison.Ordinal);
        Assert.Contains(".bos-login--erp", css, StringComparison.Ordinal);
        Assert.Contains("bos-login__glow--1", css, StringComparison.Ordinal);
    }

    [Fact]
    public void AtmosphereComponent_EmitsBosBackgroundMarkup()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Components/Shared/PhpBosLoginAtmosphere.razor");
        Assert.Contains("bos-login__bg", text, StringComparison.Ordinal);
        Assert.Contains("bos-login__particles", text, StringComparison.Ordinal);
        Assert.Contains("bos-login__glow", text, StringComparison.Ordinal);
        Assert.Contains("bos-login__particle", text, StringComparison.Ordinal);
    }
}
