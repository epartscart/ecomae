using EcomAE.Platform.Auth;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Cross-area functionality checks for Cursor-owned surfaces (storefront, CP, BOS, tenants).
/// ERP finance stays Devin and is not exercised here.
/// </summary>
[Collection("PlanQ1Statics")]
public sealed class NonErpAreaFunctionalityTests
{
    [Fact]
    public void Auth_SocialState_IsTenantScopedAndTamperProof()
    {
        PhpPlanQ1Hull.Reset();
        PhpPlanQ1Hull.Clock = () => 1_760_000_000;
        PhpPlanQ1Hull.SigningSecret = "secret";
        var acme = PhpPlanQ1Hull.EpcAuthOauthStatePack(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tenant_key"] = "acme",
            ["auth_mode"] = "cp",
            ["return_path"] = "/cp/"
        }, "nonce-acme");
        var beta = PhpPlanQ1Hull.EpcAuthOauthStatePack(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tenant_key"] = "beta",
            ["auth_mode"] = "storefront",
            ["return_path"] = "/en/profile"
        }, "nonce-beta");
        var unpackAcme = PhpPlanQ1Hull.EpcAuthOauthStateUnpack(acme)!;
        var unpackBeta = PhpPlanQ1Hull.EpcAuthOauthStateUnpack(beta)!;
        Assert.Equal("acme", Convert.ToString(unpackAcme["tk"]));
        Assert.Equal("cp", Convert.ToString(unpackAcme["am"]));
        Assert.Equal("beta", Convert.ToString(unpackBeta["tk"]));
        Assert.Equal("storefront", Convert.ToString(unpackBeta["am"]));
        Assert.NotEqual(acme, beta);
        Assert.Null(PhpPlanQ1Hull.EpcAuthOauthStateUnpack(acme + "z"));
        PhpPlanQ1Hull.Clock = () => 1_760_000_000 + 901;
        Assert.Null(PhpPlanQ1Hull.EpcAuthOauthStateUnpack(acme));
    }

    [Fact]
    public void Auth_GoogleComplete_DoesNotCrossTenantOrMintOnFailure()
    {
        PhpPlanQ1Hull.Reset();
        PhpPlanQ1Hull.ResolveForMode = (mode, hints) =>
        {
            var key = Convert.ToString(hints.TryGetValue("tenant_key", out var tk) ? tk : "") ?? "";
            if (key != "acme")
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["message"] = "Tenant context lost" };
            }

            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["tenant_key"] = key, ["auth_mode"] = mode };
        };
        PhpPlanQ1Hull.ProvisionCp = (_, _, _) => 4;
        PhpPlanQ1Hull.FinishLogin = (_, _) => new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["redirect"] = "/cp/" };
        var lost = PhpPlanQ1Hull.EpcAuthGoogleCompleteLogin(
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["am"] = "cp", ["tk"] = "other-tenant" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["email"] = "ops@acme.test" });
        Assert.False((bool)lost["ok"]!);
        Assert.Equal("Tenant context lost", Convert.ToString(lost["message"]));
        var ok = PhpPlanQ1Hull.EpcAuthGoogleCompleteLogin(
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["am"] = "cp", ["tk"] = "acme" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["email"] = "ops@acme.test" });
        Assert.True((bool)ok["ok"]!);
        Assert.Equal("/cp/", Convert.ToString(ok["redirect"]));
    }

    [Fact]
    public void Cp_ModernLoginHtml_TabsAndTenantKey_NoSessionCookie()
    {
        PhpPlanQ1Hull.Reset();
        PhpPlanQ1Hull.Policy = new(StringComparer.Ordinal) { ["password"] = true, ["email_otp"] = true, ["google_oauth"] = false };
        var html = PhpPlanQ1Hull.EpcCpLoginModernAuthHtml(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tenant_key"] = "acme",
            ["login_label"] = "Control Panel",
            ["context"] = "cp"
        });
        Assert.Contains("data-tenant-key=\"acme\"", html, StringComparison.Ordinal);
        Assert.Contains("data-tab=\"password\"", html, StringComparison.Ordinal);
        Assert.Contains("data-tab=\"email_code\"", html, StringComparison.Ordinal);
        Assert.Contains("Sign in to Control Panel", html, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", html, StringComparison.Ordinal);
        Assert.Equal("", PhpPlanQ1Hull.EpcCpLoginModernAuthHtml(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tenant_key"] = "acme"
        }));
    }

    [Fact]
    public void Bos_FailedLogin_DoesNotMintSession()
    {
        PhpPlanQ1Slip.Reset();
        PhpPlanQ1Slip.ConfigThrows = true;
        PhpPlanQ1Slip.PlatformPdo = () => null;
        PhpPlanQ1Slip.Post = new Dictionary<string, string>(StringComparer.Ordinal) { ["email"] = "a@b.c", ["password"] = "x" };
        var result = PhpPlanQ1Slip.EpcBosAjaxLoginSecure();
        Assert.False((bool)result["ok"]!);
        Assert.Equal(0, PhpPlanQ1Slip.RegenCount);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Slip.BosAjaxLoginPath, StringComparison.Ordinal);
    }

    [Fact]
    public void Tenants_HostAndTypeIsolation_SiteErpMixed()
    {
        PhpPlanQ1Dock.Reset();
        Assert.True(PhpPlanQ1Dock.EpcPortalIsPlatformHostname("www.ecomae.com"));
        Assert.False(PhpPlanQ1Dock.EpcPortalIsPlatformHostname("www.client.com"));
        Assert.True(PhpPlanQ1Dock.EpcPortalIsEpartscartHostname("www.epartscart.com:443"));
        Assert.False(PhpPlanQ1Dock.EpcPortalIsEpartscartHostname("shop.client.com"));
        Assert.True(PhpPlanQ1Dock.EpcPortalTenantIsSharedErpRow(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["erp_only_shared"] = 1
        }));
        Assert.False(PhpPlanQ1Dock.EpcPortalTenantIsSharedErpRow(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["erp_only_shared"] = "0",
            ["hosted_on"] = "client"
        }));
        Assert.True(PhpPlanQ1Dock.EpcPortalClientMayShareDocpart("www.epartscart.com"));
        Assert.False(PhpPlanQ1Dock.EpcPortalClientMayShareDocpart("shop.client.com"));
        var profile = PhpPlanQ1Dock.EpcPortalTenantRowToProfile(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = 7,
            ["status"] = "live",
            ["site_key"] = "alpha",
            ["hostname"] = "www.alpha.ae",
            ["industry_code"] = "food",
            ["db_name"] = "alpha",
            ["db_user"] = "alpha",
            ["db_password"] = "x",
            ["dedicated_db"] = 1,
            ["scale_policy"] = "",
            ["erp_only_shared"] = 0,
            ["trade_name"] = "Alpha",
            ["hub_name"] = "Hub",
            ["from_email"] = "a@x.com"
        });
        Assert.Equal("alpha", Convert.ToString(profile["site_key"]));
        Assert.Equal("https://www.alpha.ae/", Convert.ToString(profile["domain_path"]));
        Assert.Equal(1, Convert.ToInt32(profile["dedicated_db"]));
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Dock.PortalTenantPath, StringComparison.Ordinal);
    }

    [Fact]
    public void Tenants_PdoDedicatedFlag_EmptyZeroIsShared()
    {
        PhpPlanQ1Quay.Reset();
        Assert.True(PhpPlanQ1Quay.EpcTenantRowUsesDedicatedDb(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["dedicated_db"] = 1
        }));
        Assert.False(PhpPlanQ1Quay.EpcTenantRowUsesDedicatedDb(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["dedicated_db"] = "0"
        }));
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Quay.TenantPdoPath, StringComparison.Ordinal);
    }

    [Fact]
    public void Jobs_EnqueueIsTenantKeyed_EmptyPayloadIsArray()
    {
        PhpPlanQ1Pier.Reset();
        var store = new PhpPlanQ1Pier.PierStore();
        PhpPlanQ1Pier.UseStore(store);
        var opts = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dedupe"] = true };
        var first = PhpPlanQ1Pier.EpcPlatformJobsEnqueue("tenant_health_ping", "acme", new Dictionary<string, object?>(StringComparer.Ordinal), opts);
        var dup = PhpPlanQ1Pier.EpcPlatformJobsEnqueue("tenant_health_ping", "acme", new Dictionary<string, object?>(StringComparer.Ordinal), opts);
        var other = PhpPlanQ1Pier.EpcPlatformJobsEnqueue("tenant_health_ping", "beta", new Dictionary<string, object?>(StringComparer.Ordinal), opts);
        Assert.True(first > 0);
        Assert.Equal(first, dup);
        Assert.NotEqual(first, other);
        Assert.Equal("[]", store.Jobs[0].PayloadJson);
        Assert.Equal("acme", store.Jobs[0].TenantKey);
        Assert.Equal("beta", store.Jobs[1].TenantKey);
    }

    [Fact]
    public void Storefront_GuestCartDenied_AndLoginOfferPathsStaySessionless()
    {
        var denied = StorefrontPhpAjax.GuestCommerceDenied();
        Assert.NotNull(denied);
        Assert.True(StorefrontCheckoutConfirmSessionlessMiddleware.TryMatch("GET", "/en/shop/checkout/confirm", out var lang));
        Assert.Equal("en", lang);
        Assert.True(StorefrontLoginPostMiddleware.IsLoginPagePath("/en/users/login"));
        Assert.False(StorefrontLoginPostMiddleware.IsLoginPagePath("/erp/login"));
    }

    [Fact]
    public void Auth_EmailNormalize_AndCpVsStorefrontMode()
    {
        Assert.Equal("ops@acme.test", AuthEmailOtp.NormalizeEmail(" Ops@Acme.TEST "));
        Assert.Equal("storefront", AuthEmailOtp.NormalizeMode("STOREFRONT"));
        Assert.Equal("cp", AuthEmailOtp.NormalizeMode(""));
        Assert.True(AuthEmailOtp.IsValidEmail("ops@acme.test"));
        Assert.False(AuthEmailOtp.IsValidEmail("not-an-email"));
    }

    [Fact]
    public void CoveredAreas_AreCursorOwnedNotErp()
    {
        var paths = new[]
        {
            PhpPlanQ1Hull.AuthSocialPath,
            PhpPlanQ1Slip.BosAjaxLoginPath,
            PhpPlanQ1Dock.PortalTenantPath,
            PhpPlanQ1Quay.TenantPdoPath,
            PhpPlanQ1Pier.PlatformJobsPath,
            PhpPlanQ1Cove.SocialPublishPath
        };
        Assert.All(paths, path =>
        {
            Assert.DoesNotContain("/finance/", path, StringComparison.Ordinal);
            Assert.DoesNotContain("PHPSESSID", path, StringComparison.Ordinal);
        });
    }
}
