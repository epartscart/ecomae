using System.Net;
using System.Text;
using EcomAE.Platform.Auth;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// PHP <c>api/epc_oauth_callback.php</c>: missing or bad state is 400 HTML, a valid
/// state for an unconfigured provider is 503 HTML, and a provider code can be exchanged.
/// </summary>
public sealed class OAuthCallbackParityTests
{
    [Fact]
    public void MissingParameters_Is400Html()
    {
        var decision = OAuthCallback.Decide(null, null, "", "", "test-secret", 1_700_000_000, providerConfigured: false);

        Assert.Equal(400, decision.StatusCode);
        Assert.Contains("Missing sign-in parameters.", decision.Html, StringComparison.Ordinal);
        Assert.Contains("We couldn&rsquo;t sign you in", decision.Html, StringComparison.Ordinal);
        Assert.False(decision.Continue);
    }

    [Fact]
    public void ProviderError_Is400Html()
    {
        var decision = OAuthCallback.Decide("access_denied", "user cancelled", "code", "state", "test-secret", 1_700_000_000, providerConfigured: false);

        Assert.Equal(400, decision.StatusCode);
        Assert.Contains("The sign-in was cancelled or failed: user cancelled", decision.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void BadState_Is400Expired()
    {
        var decision = OAuthCallback.Decide(null, null, "auth-code", "not-a-state", "test-secret", 1_700_000_000, providerConfigured: true);

        Assert.Equal(400, decision.StatusCode);
        Assert.Contains("This sign-in link has expired. Please try again.", decision.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void PackedState_RoundTrips_AndExpiresAfter900Seconds()
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
        var state = OAuthStart.PackState("google", context, "storefront", "/en/", true, "test-secret", "0123456789abcdef0123456789abcdef", 1_700_000_000);
        var fresh = OAuthStart.UnpackState(state, "test-secret", 1_700_000_000);
        Assert.NotNull(fresh);
        Assert.Equal("google", fresh!.Provider);
        Assert.Equal("epartscart", fresh.TenantKey);
        Assert.Equal("storefront", fresh.AuthMode);
        Assert.Equal("/en/", fresh.ReturnPath);
        Assert.True(fresh.TermsAccepted);
        Assert.Null(OAuthStart.UnpackState(state, "test-secret", 1_700_000_000 + 901));
        Assert.Null(OAuthStart.UnpackState(state, "other-secret", 1_700_000_000));
    }

    [Fact]
    public void ValidState_UnconfiguredProvider_Is503_Not404()
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
        var state = OAuthStart.PackState("google", context, "storefront", "", false, "test-secret", "0123456789abcdef0123456789abcdef", 1_700_000_000);
        var decision = OAuthCallback.Decide(null, null, "auth-code", state, "test-secret", 1_700_000_000, providerConfigured: false);

        Assert.Equal(503, decision.StatusCode);
        Assert.Contains("Google sign-in is not configured.", decision.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("not configured yet", decision.Html, StringComparison.Ordinal);
        Assert.False(decision.Continue);
    }

    [Fact]
    public void ValidState_ConfiguredProvider_Continues()
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
        var state = OAuthStart.PackState("google", context, "storefront", "/en/shop/cart", false, "test-secret", "n", 1_700_000_000);
        var decision = OAuthCallback.Decide(null, null, "auth-code", state, "test-secret", 1_700_000_000, providerConfigured: true);

        Assert.True(decision.Continue);
        Assert.NotNull(decision.State);
        Assert.Equal("/en/shop/cart", OAuthCallback.SafeReturnPath(decision.State!));
        Assert.True(OAuthCallback.NeedsHandoff("www.ecomae.com", decision.State.ReturnHost));
    }

    [Fact]
    public void Handoff_RoundTrip()
    {
        var state = new OAuthUnpackedState
        {
            Provider = "google",
            ReturnHost = "www.epartscart.com",
            ReturnPath = "/en/",
            AuthMode = "storefront",
            TenantKey = "epartscart",
        };
        var query = OAuthCallback.PackHandoff(7, "sess-token", state, "test-secret", 1_700_000_120);
        var parts = query.Split('&');
        var p = Uri.UnescapeDataString(parts[0]["p=".Length..]);
        var s = Uri.UnescapeDataString(parts[1]["s=".Length..]);
        var payload = OAuthCallback.UnpackHandoff(p, s, "test-secret", 1_700_000_000);
        Assert.NotNull(payload);
        Assert.Equal(7, payload!.UserId);
        Assert.Equal("sess-token", payload.SessionToken);
        Assert.Equal("storefront", payload.Mode);
        Assert.Equal("/en/", payload.Path);
        Assert.Null(OAuthCallback.UnpackHandoff(p, s, "test-secret", 1_700_000_121));
    }

    [Fact]
    public async Task Exchange_HttpFailure_MatchesPhpMessage()
    {
        var handler = new StubHandler(HttpStatusCode.BadGateway, "");
        var http = new HttpClient(handler);
        var result = await OAuthCodeExchange.ExchangeAsync(
            http,
            "google",
            "code",
            new OAuthProviderCredentials { ClientId = "id", ClientSecret = "secret", Enabled = true },
            1_700_000_000,
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal("Token exchange failed with Google", result.Message);
    }

    [Fact]
    public async Task Exchange_GoogleIdToken_ReturnsVerifiedEmail()
    {
        var header = OAuthStart.Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"none"}"""));
        var body = OAuthStart.Base64UrlEncode(Encoding.UTF8.GetBytes("""{"email":"buyer@example.com","email_verified":true,"name":"Buyer","sub":"sub-1","aud":"id","exp":1800000000}"""));
        var token = header + "." + body + ".sig";
        var json = "{\"access_token\":\"at\",\"id_token\":\"" + token + "\"}";
        var handler = new StubHandler(HttpStatusCode.OK, json);
        var http = new HttpClient(handler);
        var result = await OAuthCodeExchange.ExchangeAsync(
            http,
            "google",
            "code",
            new OAuthProviderCredentials { ClientId = "id", ClientSecret = "secret", Enabled = true },
            1_700_000_000,
            CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal("buyer@example.com", result.Email);
        Assert.Equal("Buyer", result.Name);
        Assert.True(result.EmailVerified);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
    }
}
