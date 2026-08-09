using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Google storefront OAuth must not return the warmup splash when unconfigured.
/// </summary>
public sealed class OAuthGoogleSplashParityTests
{
    [Fact]
    public void OauthStart_Uses422_Not503_WhenUnconfigured()
    {
        var start = File.ReadAllText(Find("api/epc_oauth_start.php"));
        Assert.Contains("http_response_code(422)", start, StringComparison.Ordinal);
        Assert.DoesNotContain("http_response_code(503)", start, StringComparison.Ordinal);
        Assert.Contains("epc-platform-splash", start, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/epc_oauth_callback.php", start, StringComparison.Ordinal);
    }

    [Fact]
    public void CloudPanelHelpers_OauthLocations_DisableFastcgiIntercept()
    {
        var helpers = File.ReadAllText(Find("content/general_pages/epc_cloudpanel_helpers.php"));
        Assert.Contains("location = /api/epc_oauth_start.php", helpers, StringComparison.Ordinal);
        Assert.Contains("location = /api/epc_oauth_callback.php", helpers, StringComparison.Ordinal);
        Assert.Contains("fastcgi_intercept_errors off", helpers, StringComparison.Ordinal);
        Assert.Contains("ecomae-oauth-no-splash", File.ReadAllText(
            Find("scripts/cloudpanel_FIX_OAUTH_GOOGLE_SPLASH_NOW.sh")), StringComparison.Ordinal);
    }

    [Fact]
    public void LoginButtonsStillPointAtOauthStart()
    {
        var razor = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Shared/PhpOAuthLoginButtons.razor"));
        Assert.Contains("/api/epc_oauth_start.php?", razor, StringComparison.Ordinal);
        Assert.Contains("StartHref(\"google\")", razor, StringComparison.Ordinal);
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
