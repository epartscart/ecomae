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
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/CpLoginApp.razor", "epcCpParticles", "EcomAeRoutes.ControlPanelLogin")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpLoginApp.razor", "erpPortalParticles", "EcomAeRoutes.ErpLogin")]
    public void LoginApp_UsesBosShellStructure(string path, string particlesId, string routeSymbol)
    {
        var text = Read(path);
        Assert.Contains("epc-login-bos bos-login", text, StringComparison.Ordinal);
        Assert.Contains("RootModifierClass", text, StringComparison.Ordinal);
        Assert.Contains("bos-login__hero", text, StringComparison.Ordinal);
        Assert.Contains("bos-login__panel", text, StringComparison.Ordinal);
        Assert.Contains("bos-login__form", text, StringComparison.Ordinal);
        Assert.Contains("PhpBosLoginAtmosphere", text, StringComparison.Ordinal);
        Assert.Contains("PhpLoginHostBrand", text, StringComparison.Ordinal);
        Assert.Contains("LoginHostBrand.Resolve", text, StringComparison.Ordinal);
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
    public void AtmosphereComponent_UsesSkyGradientAndFallingBodies()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Components/Shared/PhpBosLoginAtmosphere.razor");
        Assert.Contains("bos-login__bg", text, StringComparison.Ordinal);
        Assert.Contains("bos-login__bg--sky", text, StringComparison.Ordinal);
        Assert.Contains("bos-login__sky-veil", text, StringComparison.Ordinal);
        Assert.Contains("bos-login__particles", text, StringComparison.Ordinal);
        Assert.Contains("bos-login__body--moon", text, StringComparison.Ordinal);
        Assert.Contains("bos-login__body--planet", text, StringComparison.Ordinal);
        Assert.Contains("createFallingBodies", text, StringComparison.Ordinal);
        Assert.Contains("pickDuration", text, StringComparison.Ordinal);
        Assert.Contains("bosFloat", text, StringComparison.Ordinal);
        Assert.Contains("var count = 480", text, StringComparison.Ordinal);
        Assert.DoesNotContain("bos-login__universe-photo", text, StringComparison.Ordinal);
        Assert.DoesNotContain("images-assets.nasa.gov", text, StringComparison.Ordinal);
        Assert.DoesNotContain("cdn.esawebb.org", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/platform-assets/universe/", text, StringComparison.Ordinal);
        // No glyph icons (half moons / star characters) in the foreground
        Assert.DoesNotContain("bos-login__celestial", text, StringComparison.Ordinal);
        Assert.DoesNotContain("☾", text, StringComparison.Ordinal);
        Assert.DoesNotContain("★", text, StringComparison.Ordinal);
        Assert.DoesNotContain("✦", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AccentCss_IncludesSkyBodiesAndTenantLooks()
    {
        var css = Read("content/general_pages/epc_bos_login_surface_accents.css");
        Assert.Contains("bos-login__bg--sky", css, StringComparison.Ordinal);
        Assert.Contains("bos-login__sky-veil", css, StringComparison.Ordinal);
        Assert.Contains("bos-login__body--moon", css, StringComparison.Ordinal);
        Assert.Contains("border-radius: 50%", css, StringComparison.Ordinal);
        Assert.Contains("linear-gradient", css, StringComparison.Ordinal);
        Assert.Contains("bos-login__cap-icon--tone-1", css, StringComparison.Ordinal);
        Assert.Contains("bos-login--tenant-epartscart", css, StringComparison.Ordinal);
        Assert.Contains("bos-login--tenant-jewellery .bos-login__cap-icon--tone-1", css, StringComparison.Ordinal);
        Assert.Contains("bos-login__tenant-logo--eparts", css, StringComparison.Ordinal);
        Assert.DoesNotContain("bos-login__universe-photo", css, StringComparison.Ordinal);
        Assert.DoesNotContain("bos-login__celestial", css, StringComparison.Ordinal);
        Assert.DoesNotContain("var(--bos-fall-x", css, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyAssetBridge_ServesUniverseStills()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Presentation/PhpLegacyAssetBridge.cs");
        Assert.Contains("ServeUniverseStill", text, StringComparison.Ordinal);
        Assert.Contains("/platform-assets/universe/{fileName}", text, StringComparison.Ordinal);
        Assert.Contains("/content/general_pages/universe/{fileName}", text, StringComparison.Ordinal);
        Assert.Contains("cassiopeia-a.jpg", text, StringComparison.Ordinal);
        Assert.Contains("andromeda.jpg", text, StringComparison.Ordinal);
        Assert.Contains("pillars.jpg", text, StringComparison.Ordinal);
        Assert.Contains("tarantula.jpg", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LoginAccentCss_CacheBust_IsCurrent()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Presentation/LegacyPresentationAssets.cs");
        Assert.Contains("epc_bos_login_surface_accents.css?v=20260806f", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LoginApps_CycleCapabilityIconTones()
    {
        var erp = Read("aspnet/src/EcomAE.Platform/Components/Pages/ErpLoginApp.razor");
        var cp = Read("aspnet/src/EcomAE.Platform/Components/Pages/CpLoginApp.razor");
        Assert.Contains("CapIconTone", erp, StringComparison.Ordinal);
        Assert.Contains("bos-login__cap-icon--tone-", erp, StringComparison.Ordinal);
        Assert.Contains("CapIconTone", cp, StringComparison.Ordinal);
        Assert.DoesNotContain("bos-login__cap-icon--erp\"><i class=\"fa @cap.Icon\"", erp, StringComparison.Ordinal);
    }
}
