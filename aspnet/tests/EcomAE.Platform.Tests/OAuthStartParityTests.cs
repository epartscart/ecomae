using EcomAE.Platform.Auth;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Customer/CP login buttons call GET /api/epc_oauth_start.php. That route must answer
/// like PHP: 400 unknown, 422 unconfigured (not 404 or 503), 302 when credentials exist.
/// </summary>
public sealed class OAuthStartParityTests
{
    [Fact]
    public void UnknownProvider_Is400_Not404()
    {
        var response = OAuthStart.Decide(
            "nope",
            "storefront",
            "/en/",
            termsAccepted: false,
            OAuthProviderCredentials.Empty,
            OAuthSignInContext.Failed("unused"),
            "test-secret",
            "00",
            0);

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("Unknown sign-in provider.", response.Text);
        Assert.Null(response.Location);
    }

    [Fact]
    public void UnconfiguredGoogle_Is422_WithPhpMessage()
    {
        var response = OAuthStart.Decide(
            "google",
            "storefront",
            "/en/",
            termsAccepted: false,
            OAuthProviderCredentials.Empty,
            OAuthSignInContext.Failed("Database unavailable"),
            "test-secret",
            "00",
            0);

        Assert.Equal(422, response.StatusCode);
        Assert.StartsWith("Google sign-in is not configured yet.", response.Text, StringComparison.Ordinal);
        Assert.Contains("https://www.ecomae.com/api/epc_oauth_callback.php", response.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("503", response.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ContextFailure_Is400_AfterConfigured()
    {
        var response = OAuthStart.Decide(
            "google",
            "cp",
            "/cp",
            termsAccepted: false,
            ConfiguredGoogle(),
            OAuthSignInContext.Failed("Database unavailable"),
            "test-secret",
            "00",
            0);

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("Sign-in context error: Database unavailable", response.Text);
    }

    [Fact]
    public void ConfiguredGoogle_RedirectMatchesPhpStateAndQuery()
    {
        var context = new OAuthSignInContext
        {
            Ok = true,
            TenantKey = "epartscart",
            Kind = "storefront_local",
            ReturnHost = "www.epartscart.com",
            ReturnPath = "/en/",
            LangPrefix = "/en",
        };
        var response = OAuthStart.Decide(
            "Google",
            "storefront",
            "/en/",
            termsAccepted: true,
            ConfiguredGoogle(),
            context,
            "test-secret",
            "0123456789abcdef0123456789abcdef",
            1700000000);

        Assert.Equal(302, response.StatusCode);
        Assert.Equal(
            "https://accounts.google.com/o/oauth2/v2/auth?client_id=id.apps.googleusercontent.com&redirect_uri=https%3A%2F%2Fwww.ecomae.com%2Fapi%2Fepc_oauth_callback.php&response_type=code&scope=openid+email+profile&state=eyJwdiI6Imdvb2dsZSIsIm4iOiIwMTIzNDU2Nzg5YWJjZGVmMDEyMzQ1Njc4OWFiY2RlZiIsInRrIjoiZXBhcnRzY2FydCIsImsiOiJzdG9yZWZyb250X2xvY2FsIiwicmgiOiJ3d3cuZXBhcnRzY2FydC5jb20iLCJycCI6IlwvZW5cLyIsImFtIjoic3RvcmVmcm9udCIsImxwIjoiXC9lbiIsInJ1IjoiXC9lblwvIiwidG0iOjEsInQiOjE3MDAwMDAwMDB9.2d4c153d2a2db16249ed8817ade18873aa41742496f7ce9e2cd042a012b82cb2&nonce=0123456789abcdef0123456789abcdef&prompt=select_account&access_type=online",
            response.Location);
    }

    [Fact]
    public void PhpFileCredentials_ParseClientId()
    {
        var php = """
            <?php
            return array(
            	'google' => array(
            		'client_id' => 'file-client.apps.googleusercontent.com',
            		'client_secret' => 'file-secret',
            		'redirect_uri' => 'https://www.ecomae.com/api/epc_oauth_callback.php',
            	),
            );
            """;
        var parsed = OAuthStartEndpoint.ParsePhpOauthFile(php, "google");
        Assert.NotNull(parsed);
        Assert.Equal("file-client.apps.googleusercontent.com", parsed!.ClientId);
        Assert.Equal("file-secret", parsed.ClientSecret);
        Assert.True(OAuthStart.IsConfigured("google", parsed));
        Assert.False(OAuthStart.IsConfigured("google", OAuthStartEndpoint.ParsePhpOauthFile(php, "microsoft")!));
    }

    [Fact]
    public void EndpointIsMappedOnThePhpUrl()
    {
        var endpoint = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Auth/OAuthStartEndpoint.cs"));
        var program = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Program.cs"));
        var razor = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/PhpOAuthLoginButtons.razor"));
        Assert.Contains("MapGet(\"/api/epc_oauth_start.php\"", endpoint, StringComparison.Ordinal);
        Assert.Contains("OAuthStartEndpoint.Map(app)", program, StringComparison.Ordinal);
        Assert.Contains("/api/epc_oauth_start.php?", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void StorefrontShellAssetsAreMapped()
    {
        var bridge = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Presentation/PhpLegacyAssetBridge.cs"));
        Assert.Contains("\"/modules/slider/css/style.css\"", bridge, StringComparison.Ordinal);
        Assert.Contains("\"/assets/media/logos/ecomae_mark.svg\"", bridge, StringComparison.Ordinal);
        Assert.True(File.Exists(Find("modules/slider/css/style.css")));
    }

    private static OAuthProviderCredentials ConfiguredGoogle() => new()
    {
        ClientId = "id.apps.googleusercontent.com",
        ClientSecret = "secret",
        Enabled = true,
    };

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
