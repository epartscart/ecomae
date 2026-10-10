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
    public void Cp_AuthGate_CountsExactlyOneSession_AndNeverMintsOnGet()
    {
        PhpPlanQ1Keel.Reset();
        var store = new PhpPlanQ1Keel.GateStore();
        store.Sessions.Add(new PhpPlanQ1Keel.SessionRow { Session = "acme-op", Type = 1, UserId = 7 });
        store.Sessions.Add(new PhpPlanQ1Keel.SessionRow { Session = "beta-op", Type = 1, UserId = 9 });
        store.Sessions.Add(new PhpPlanQ1Keel.SessionRow { Session = "dup", Type = 1, UserId = 7 });
        store.Sessions.Add(new PhpPlanQ1Keel.SessionRow { Session = "dup", Type = 1, UserId = 7 });
        PhpPlanQ1Keel.Store = store;
        PhpPlanQ1Keel.Cookies = new(StringComparer.Ordinal) { ["admin_session"] = "acme-op", ["admin_u_id"] = "7" };
        Assert.True(PhpPlanQ1Keel.EpcCpAuthGateIsAdmin());
        PhpPlanQ1Keel.Cookies["admin_session"] = "beta-op";
        Assert.False(PhpPlanQ1Keel.EpcCpAuthGateIsAdmin());
        PhpPlanQ1Keel.Cookies = new(StringComparer.Ordinal) { ["admin_session"] = "dup", ["admin_u_id"] = "7" };
        Assert.False(PhpPlanQ1Keel.EpcCpAuthGateIsAdmin());
        PhpPlanQ1Keel.Reset();
        PhpPlanQ1Keel.Server["REQUEST_URI"] = "/cp/";
        PhpPlanQ1Keel.Server["REQUEST_METHOD"] = "GET";
        var cookiesBefore = PhpPlanQ1Keel.Cookies.Count;
        var guest = PhpPlanQ1Keel.EpcCpAuthGateRun();
        Assert.Equal("redirect", Convert.ToString(guest["action"]));
        Assert.Equal("/cp/control", Convert.ToString(guest["location"]));
        Assert.Equal(cookiesBefore, PhpPlanQ1Keel.Cookies.Count);
        Assert.False(PhpPlanQ1Keel.EpcCpAuthGateIsAdmin());
        PhpPlanQ1Keel.IsErpOnlyTenant = true;
        PhpPlanQ1Keel.ErpShellUrlPresent = false;
        PhpPlanQ1Keel.IsPlatformHostname = false;
        Assert.Equal("/cp/shop/finance/erp?epc_erp_shell=1", PhpPlanQ1Keel.EpcCpAuthGateErpOnlyLanding());
        PhpPlanQ1Keel.IsPlatformHostname = true;
        PhpPlanQ1Keel.Store = store;
        PhpPlanQ1Keel.Cookies = new(StringComparer.Ordinal) { ["admin_session"] = "acme-op", ["admin_u_id"] = "7" };
        PhpPlanQ1Keel.Server["REQUEST_URI"] = "/cp/";
        PhpPlanQ1Keel.Server["REQUEST_METHOD"] = "GET";
        PhpPlanQ1Keel.Server["QUERY_STRING"] = "tab=1";
        var platform = PhpPlanQ1Keel.EpcCpAuthGateRun();
        Assert.Equal("/cp/control?tab=1", Convert.ToString(platform["location"]));
        PhpPlanQ1Keel.Reset();
        PhpPlanQ1Keel.Server["REQUEST_URI"] = "/cp/shop/tenant_hub/x";
        PhpPlanQ1Keel.Server["QUERY_STRING"] = "a=1";
        var hub = PhpPlanQ1Keel.EpcCpAuthGateRun();
        Assert.Equal("/cp/control?a=1", Convert.ToString(hub["location"]));
        Assert.Empty(PhpPlanQ1Keel.Cookies);
        var ajax = PhpPlanQ1Keel.EpcCpAuthGateMfaAjax();
        Assert.Equal("json", Convert.ToString(ajax["action"]));
        Assert.Contains("Not authenticated", Convert.ToString(ajax["body"]), StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", Convert.ToString(ajax["body"]), StringComparison.Ordinal);
    }

    [Fact]
    public void Api_KeysAndHandle_StayOnTheKeyTenant()
    {
        PhpPlanQ1Mast.Reset();
        var acme = PhpPlanQ1Mast.EpcApiKeyGenerate("acme", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["scopes"] = new[] { "read" }
        });
        var beta = PhpPlanQ1Mast.EpcApiKeyGenerate("beta", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["scopes"] = new[] { "read" }
        });
        Assert.Empty(PhpPlanQ1Mast.EpcApiKeysList("missing"));
        Assert.Single(PhpPlanQ1Mast.EpcApiKeysList("acme"));
        Assert.Single(PhpPlanQ1Mast.EpcApiKeysList("beta"));
        PhpPlanQ1Mast.Server["HTTP_AUTHORIZATION"] = "Bearer " + acme["api_key"];
        var hit = PhpPlanQ1Mast.EpcApiV2Handle("GET", "/api/v2/products", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = "beta"
        });
        Assert.True((bool)hit["ok"]!);
        Assert.Equal("acme", Convert.ToString(hit["site_key"]));
        PhpPlanQ1Mast.EpcApiKeyRevoke(Convert.ToInt32(acme["key_id"]));
        var after = PhpPlanQ1Mast.EpcApiV2Handle("GET", "/api/v2/products");
        Assert.False((bool)after["ok"]!);
        Assert.DoesNotContain("PHPSESSID", Convert.ToString(after["error"]), StringComparison.Ordinal);
        Assert.Equal("beta", Convert.ToString(((Dictionary<string, object?>)PhpPlanQ1Mast.EpcApiKeyValidate(Convert.ToString(beta["api_key"])!)["key"]!)["site_key"]));
    }

    [Fact]
    public void Auth_SmtpOverlayAndOtpLookup_StayOnTheTenant()
    {
        PhpPlanQ1Helm.Reset();
        PhpPlanQ1Helm.SmtpFileExists = true;
        PhpPlanQ1Helm.SmtpFile = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["smtp_mode"] = "1",
            ["smtp_host"] = "file.example",
            ["smtp_port"] = "587",
            ["smtp_encryption"] = "tls",
            ["smtp_username"] = "file@shop.example",
            ["smtp_password"] = "file-secret-1",
            ["from_email"] = "file@shop.example",
            ["from_name"] = "File"
        };
        PhpPlanQ1Helm.SiteSettings = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["integrations"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["smtp"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["use_tenant_smtp"] = "1",
                    ["smtp_host"] = "acme.example",
                    ["smtp_port"] = "465",
                    ["smtp_encryption"] = "ssl",
                    ["smtp_username"] = "acme@shop.example",
                    ["smtp_password"] = "acme-secret",
                    ["from_email"] = "acme@shop.example",
                    ["from_name"] = "Acme"
                }
            }
        };
        PhpPlanQ1Helm.IsSuperCp = false;
        var tenant = PhpPlanQ1Helm.EpcAuthSmtpEffectiveConfig();
        Assert.Equal("acme.example", Convert.ToString(tenant["smtp_host"]));
        Assert.Equal("tenant integrations (site_settings)", Convert.ToString(tenant["_source"]));
        PhpPlanQ1Helm.IsSuperCp = true;
        var platform = PhpPlanQ1Helm.EpcAuthSmtpEffectiveConfig();
        Assert.Equal("file.example", Convert.ToString(platform["smtp_host"]));
        Assert.Equal("config.epc-smtp.php", Convert.ToString(platform["_source"]));
        PhpPlanQ1Helm.Otps.Add(new PhpPlanQ1Helm.OtpRow
        {
            Id = 1,
            Email = "ops@acme.example",
            TenantKey = "acme",
            ContextJson = "{}",
            CreatedAt = 1760083200
        });
        PhpPlanQ1Helm.Otps.Add(new PhpPlanQ1Helm.OtpRow
        {
            Id = 2,
            Email = "ops@beta.example",
            TenantKey = "beta",
            ContextJson = "{}",
            CreatedAt = 1760083200
        });
        PhpPlanQ1Helm.EpcAuthOtpStoreOperatorCode(1, "111111");
        PhpPlanQ1Helm.EpcAuthOtpStoreOperatorCode(2, "222222");
        var acme = PhpPlanQ1Helm.EpcAuthOtpOperatorLookup("OPS@acme.example");
        var beta = PhpPlanQ1Helm.EpcAuthOtpOperatorLookup("ops@beta.example");
        Assert.Equal("111111", Convert.ToString(acme["code"]));
        Assert.Equal("acme", Convert.ToString(acme["tenant_key"]));
        Assert.Equal("222222", Convert.ToString(beta["code"]));
        Assert.Equal("beta", Convert.ToString(beta["tenant_key"]));
        Assert.False((bool)PhpPlanQ1Helm.EpcAuthOtpOperatorLookup("other@shop.example")["ok"]!);
        Assert.DoesNotContain("PHPSESSID", Convert.ToString(acme["code"]), StringComparison.Ordinal);
        Assert.False(PhpPlanQ1Helm.EpcAuthOtpDemoFallbackAllowed("acme"));
    }

    [Fact]
    public void Readiness_ScoreAndFleet_StayOnTheTenant()
    {
        PhpPlanQ1Yard.Reset();
        PhpPlanQ1Yard.AddSetting("acme", "isolation_audit_status", "pass");
        PhpPlanQ1Yard.AddSetting("acme", "mfa_enabled", "1");
        PhpPlanQ1Yard.AddSetting("beta", "isolation_audit_status", "fail");
        PhpPlanQ1Yard.Tenants.Add(new PhpPlanQ1Yard.TenantRow
        {
            SiteKey = "acme",
            TradeName = "Acme",
            Status = "live",
            Industry = "auto",
            ErpEnabled = 1
        });
        PhpPlanQ1Yard.Tenants.Add(new PhpPlanQ1Yard.TenantRow
        {
            SiteKey = "beta",
            TradeName = "Beta",
            Status = "live",
            Industry = "retail",
            ErpEnabled = 0
        });
        var acme = PhpPlanQ1Yard.EpcReadinessScore("acme");
        var beta = PhpPlanQ1Yard.EpcReadinessScore("beta");
        Assert.Equal("acme", Convert.ToString(acme["site_key"]));
        Assert.Equal("beta", Convert.ToString(beta["site_key"]));
        Assert.True(Convert.ToInt32(acme["score"]) > Convert.ToInt32(beta["score"]));
        var isoAcme = ((List<Dictionary<string, object?>>)acme["checks"]!).First(c => Convert.ToString(c["id"]) == "isolation");
        var isoBeta = ((List<Dictionary<string, object?>>)beta["checks"]!).First(c => Convert.ToString(c["id"]) == "isolation");
        Assert.Equal("pass", Convert.ToString(isoAcme["status"]));
        Assert.Equal("fail", Convert.ToString(isoBeta["status"]));
        var fleet = PhpPlanQ1Yard.EpcReadinessFleetSummary();
        Assert.Equal(2, Convert.ToInt32(fleet["tenant_count"]));
        Assert.DoesNotContain("PHPSESSID", Convert.ToString(acme["tier"]), StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Yard.ReadinessPath, StringComparison.Ordinal);
    }

    [Fact]
    public void CoveredAreas_AreCursorOwnedNotErp()
    {
        var paths = new[]
        {
            PhpPlanQ1Hull.AuthSocialPath,
            PhpPlanQ1Keel.AuthGatePath,
            PhpPlanQ1Mast.RestApiPath,
            PhpPlanQ1Helm.AuthSmtpPath,
            PhpPlanQ1Yard.ReadinessPath,
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
