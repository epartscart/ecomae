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
    public void BlockchainProofs_StayOnTheTenantAndSkipWhenOff()
    {
        PhpPlanQ1Boom.Reset();
        PhpPlanQ1Boom.EpcBcBosRecordProof("acme", "invoice", "A1", new Dictionary<string, object?>(StringComparer.Ordinal) { ["n"] = 1 },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["ts"] = "2026-10-10T08:00:00+00:00" });
        PhpPlanQ1Boom.EpcBcBosRecordProof("beta", "invoice", "B1", new Dictionary<string, object?>(StringComparer.Ordinal) { ["n"] = 2 },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["ts"] = "2026-10-10T08:00:00+00:00" });
        var acme = PhpPlanQ1Boom.EpcBcBosListProofs("acme");
        var beta = PhpPlanQ1Boom.EpcBcBosListProofs("beta");
        Assert.Single(acme);
        Assert.Single(beta);
        Assert.Equal("acme", Convert.ToString(acme[0]["tenant_key"]));
        Assert.Equal("beta", Convert.ToString(beta[0]["tenant_key"]));
        Assert.NotEqual(Convert.ToString(acme[0]["proof_uid"]), Convert.ToString(beta[0]["proof_uid"]));
        PhpPlanQ1Boom.ClientErpKey = () => "beta";
        PhpPlanQ1Boom.TenantRows["beta"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["blockchain_mode"] = "off" };
        var skipped = PhpPlanQ1Boom.EpcBcBosMaybeRecordDocument("invoice", "B2", new Dictionary<string, object?>(StringComparer.Ordinal) { ["n"] = 3 });
        Assert.True((bool)skipped["ok"]!);
        Assert.Equal("mode_off", Convert.ToString(skipped["reason"]));
        Assert.Single(PhpPlanQ1Boom.EpcBcBosListProofs("beta"));
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Boom.BlockchainBosPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Boom.BlockchainBosPath, StringComparison.Ordinal);
    }

    [Fact]
    public void CpPageAssets_SiteKeyAndEmptyBackend_StayIsolated()
    {
        PhpPlanQ1Stay.Reset();
        PhpPlanQ1Stay.Get["site_key"] = "Acme-1!";
        PhpPlanQ1Stay.Get["tab"] = "discover";
        var acme = PhpPlanQ1Stay.EpcCpApaiShellConfigScript();
        PhpPlanQ1Stay.Get["site_key"] = "Beta-2!";
        var beta = PhpPlanQ1Stay.EpcCpApaiShellConfigScript();
        Assert.Contains("\"siteKey\":\"acme1\"", acme, StringComparison.Ordinal);
        Assert.Contains("\"siteKey\":\"beta2\"", beta, StringComparison.Ordinal);
        Assert.DoesNotContain("acme1", beta, StringComparison.Ordinal);
        Assert.DoesNotContain("beta2", acme, StringComparison.Ordinal);
        PhpPlanQ1Stay.BackendDir = "";
        var emptyBackend = PhpPlanQ1Stay.EpcCpApaiShellConfigScript();
        Assert.Contains("\"backend\":\"cp\"", emptyBackend, StringComparison.Ordinal);
        var inline = PhpPlanQ1Stay.EpcCpApaiInlineDiscoverConfigScript();
        Assert.Contains("\"backend\":\"\"", inline, StringComparison.Ordinal);
        Assert.Contains("\"ajaxUrl\":\"//control/portal/ajax_auto_price\"", inline, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", acme, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Stay.PageAssetsPath, StringComparison.Ordinal);
    }

    [Fact]
    public void TenantShowcase_IndustryAndScreenshot_StayOnTheInjectedTenant()
    {
        PhpPlanQ1Jib.Reset();
        PhpPlanQ1Jib.CustomerResults = () =>
        [
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["key"] = "acme",
                ["name"] = "Acme",
                ["industry"] = "fashion"
            }
        ];
        var acme = PhpPlanQ1Jib.EpcEcomaePlatformTenantShowcaseRows();
        PhpPlanQ1Jib.CustomerResults = () =>
        [
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["key"] = "beta",
                ["name"] = "Beta",
                ["industry"] = "jewellery"
            }
        ];
        var beta = PhpPlanQ1Jib.EpcEcomaePlatformTenantShowcaseRows();
        Assert.Equal("fashion", Convert.ToString(acme[0]["industry"]));
        Assert.Equal("jewellery", Convert.ToString(beta[0]["industry"]));
        Assert.NotEqual(Convert.ToString(((Dictionary<string, string>)acme[0]["theme_meta"]!)["label"]),
            Convert.ToString(((Dictionary<string, string>)beta[0]["theme_meta"]!)["label"]));
        var shots = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tenant-acme1-storefront"] = "/content/files/images/tenant-acme1-storefront.webp",
            ["tenant-beta2-storefront"] = "/content/files/images/tenant-beta2-storefront.png"
        };
        PhpPlanQ1Jib.Screenshot = slug => shots.TryGetValue(slug, out var path) ? path : "";
        var acmeShot = PhpPlanQ1Jib.EpcEcomaePlatformTenantStorefrontScreenshot("Acme-1!");
        var betaShot = PhpPlanQ1Jib.EpcEcomaePlatformTenantStorefrontScreenshot("Beta_2!");
        Assert.Contains("tenant-acme1-storefront.webp", acmeShot, StringComparison.Ordinal);
        Assert.Contains("tenant-beta2-storefront.png", betaShot, StringComparison.Ordinal);
        Assert.DoesNotContain("acme1", betaShot, StringComparison.Ordinal);
        Assert.DoesNotContain("beta2", acmeShot, StringComparison.Ordinal);
        Assert.DoesNotContain("_", acmeShot, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", acmeShot, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Jib.TenantShowcasePath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Jib.TenantShowcasePath, StringComparison.Ordinal);
    }

    [Fact]
    public void WhatsappShare_SalesDigitsAndLogs_StayOnTheInjectedTenant()
    {
        PhpPlanQ1Gaff.Reset();
        PhpPlanQ1Gaff.AgentHref = () => "https://wa.me/971500000001";
        var acme = PhpPlanQ1Gaff.EpcWaSalesDigits(new Dictionary<string, object?>(StringComparer.Ordinal));
        PhpPlanQ1Gaff.AgentHref = () => "https://wa.me/971500000002";
        var beta = PhpPlanQ1Gaff.EpcWaSalesDigits(new Dictionary<string, object?>(StringComparer.Ordinal));
        Assert.Equal("971500000001", acme);
        Assert.Equal("971500000002", beta);
        var logs = new List<string>();
        PhpPlanQ1Gaff.InsertLog = (_, _, text) => logs.Add(text);
        PhpPlanQ1Gaff.OrderItems = _ => [];
        PhpPlanQ1Gaff.EpcWaNotifyOrderStatusChange(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["from_name"] = "Acme",
                ["domain_path"] = "https://acme.example/"
            },
            9,
            "Packed",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["phone_not_auth"] = "+971-55-1" });
        Assert.Single(logs);
        Assert.Contains("Packed", logs[0], StringComparison.Ordinal);
        Assert.DoesNotContain("971500000002", logs[0], StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Gaff.WhatsappSharePath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Gaff.WhatsappSharePath, StringComparison.Ordinal);
    }

    [Fact]
    public void MarketingBrochure_BrandAndLiveDeck_StayOnTheInjectedBrand()
    {
        PhpPlanQ1Sprit.Reset();
        var parts = PhpPlanQ1Sprit.EpcBrochureProfile("AUTO_PARTS");
        var platform = PhpPlanQ1Sprit.EpcBrochureProfile("Fashion!");
        Assert.Equal("epartscart", Convert.ToString(parts["id"]));
        Assert.Equal("ecomae", Convert.ToString(platform["id"]));
        Assert.NotEqual(Convert.ToString(parts["accent"]), Convert.ToString(platform["accent"]));
        PhpPlanQ1Sprit.ItemImage = (_, group) => group == "How work flows" ? "/live/acme.jpg" : "/live/acme-sec.jpg";
        var acme = PhpPlanQ1Sprit.EpcBrochureRenderHtml("ecomae");
        PhpPlanQ1Sprit.ItemImage = (_, group) => group == "How work flows" ? "/live/beta.jpg" : "/live/beta-sec.jpg";
        var beta = PhpPlanQ1Sprit.EpcBrochureRenderHtml("ecomae");
        Assert.Contains("/live/acme.jpg", acme, StringComparison.Ordinal);
        Assert.Contains("/live/beta.jpg", beta, StringComparison.Ordinal);
        Assert.DoesNotContain("/live/acme", beta, StringComparison.Ordinal);
        Assert.DoesNotContain("/live/beta", acme, StringComparison.Ordinal);
        Assert.Contains("&#039;", PhpPlanQ1Sprit.EpcBrochureH("O'area"), StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", acme, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Sprit.MarketingBrochurePath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Sprit.MarketingBrochurePath, StringComparison.Ordinal);
    }

    [Fact]
    public void DemoAutopartsBootstrap_SourceAndPreset_StayOnTheInjectedTenant()
    {
        PhpPlanQ1Luff.Reset();
        var acmePreset = Path.Combine(Path.GetTempPath(), "ecomae_luff_acme_" + Guid.NewGuid().ToString("N")[..8] + ".json");
        var betaPreset = Path.Combine(Path.GetTempPath(), "ecomae_luff_beta_" + Guid.NewGuid().ToString("N")[..8] + ".json");
        File.WriteAllText(acmePreset, "{\"id\":\"acme\",\"docpart_clone_tables\":[\"shop_geo\"]}");
        File.WriteAllText(betaPreset, "{\"id\":\"beta\",\"docpart_clone_tables\":[\"shop_offices\"]}");
        PhpPlanQ1Luff.PresetPath = () => acmePreset;
        var acme = PhpPlanQ1Luff.EpcDemoAutopartsBootstrapCloneTables();
        PhpPlanQ1Luff.PresetPath = () => betaPreset;
        var beta = PhpPlanQ1Luff.EpcDemoAutopartsBootstrapCloneTables();
        Assert.Equal(new[] { "shop_geo" }, acme);
        Assert.Equal(new[] { "shop_offices" }, beta);
        Assert.DoesNotContain("shop_offices", acme);
        Assert.DoesNotContain("shop_geo", beta);
        PhpPlanQ1Luff.SourcePdo = () => null;
        Assert.Null(PhpPlanQ1Luff.EpcDemoAutopartsBootstrapDocpartPdo());
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Luff.DemoAutopartsBootstrapPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Luff.DemoAutopartsBootstrapPath, StringComparison.Ordinal);
        File.Delete(acmePreset);
        File.Delete(betaPreset);
    }

    [Fact]
    public void ShippingExport_CopyAndCountry_StayOnTheInjectedTenant()
    {
        PhpPlanQ1Clew.Reset();
        PhpPlanQ1Clew.CurrentLang = () => "en";
        PhpPlanQ1Clew.TenantCountry = () => "AE";
        PhpPlanQ1Clew.ShippingPhrase = _ => "acme-ship";
        var acme = PhpPlanQ1Clew.EpcSeoShippingExportRenderHtml();
        PhpPlanQ1Clew.TenantCountry = () => "OM";
        PhpPlanQ1Clew.ShippingPhrase = _ => "beta-ship";
        var beta = PhpPlanQ1Clew.EpcSeoShippingExportRenderHtml();
        Assert.Contains("acme-ship", acme, StringComparison.Ordinal);
        Assert.Contains("Tenant country profile: AE", acme, StringComparison.Ordinal);
        Assert.Contains("beta-ship", beta, StringComparison.Ordinal);
        Assert.Contains("Tenant country profile: OM", beta, StringComparison.Ordinal);
        Assert.DoesNotContain("beta-ship", acme, StringComparison.Ordinal);
        Assert.DoesNotContain("acme-ship", beta, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", acme, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Clew.SeoShippingExportPath, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductExistLimit_Statuses_StayOnTheInjectedCatalogue()
    {
        PhpPlanQ1Tack.Reset();
        Assert.Contains("product_exist_limit.php", PhpPlanQ1Tack.ProductExistLimitPath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Tack.ProductExistLimitPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Tack.ProductExistLimitPath, StringComparison.Ordinal);
    }

    [Fact]
    public void PosCpInstall_WalkinAndMenu_StayOnTheInjectedTenant()
    {
        PhpPlanQ1Vang.Reset();
        PhpPlanQ1Vang.EnsureWalkin = _ => 11;
        PhpPlanQ1Vang.PortalMenu = _ => new Dictionary<string, object?>(StringComparer.Ordinal) { ["acme"] = 1 };
        PhpPlanQ1Vang.PosMenu = _ => new Dictionary<string, object?>(StringComparer.Ordinal) { ["pos"] = "acme" };
        Assert.Equal(11, PhpPlanQ1Vang.EnsureWalkin(null!));
        PhpPlanQ1Vang.EnsureWalkin = _ => 22;
        PhpPlanQ1Vang.PortalMenu = _ => new Dictionary<string, object?>(StringComparer.Ordinal) { ["beta"] = 1 };
        Assert.Equal(22, PhpPlanQ1Vang.EnsureWalkin(null!));
        Assert.Contains("beta", PhpPlanQ1Vang.PortalMenu(null!).Keys);
        Assert.DoesNotContain("acme", PhpPlanQ1Vang.PortalMenu(null!).Keys);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Vang.PosCpInstallPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Vang.PosCpInstallPath, StringComparison.Ordinal);
    }

    [Fact]
    public void CpCrossHelpers_PairLookup_StaysOnTheInjectedCatalogue()
    {
        PhpPlanQ1Sheet.Reset();
        PhpPlanQ1Sheet.PairExists = (a, ab, r, rb) =>
        {
            var acme = a == "AB12" && r == "XY9" && ab == "ACME";
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["linked"] = acme, ["id"] = acme ? 1 : 0 };
        };
        var acme = PhpPlanQ1Sheet.EpcCpCrossPairStatus(null, "ab-12", "acme", "xy-9", "valeo");
        PhpPlanQ1Sheet.PairExists = (a, ab, r, rb) =>
        {
            var beta = a == "AB12" && r == "XY9" && ab == "BETA";
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["linked"] = beta, ["id"] = beta ? 2 : 0 };
        };
        var beta = PhpPlanQ1Sheet.EpcCpCrossPairStatus(null, "ab-12", "beta", "xy-9", "valeo");
        Assert.True((bool)acme["linked"]!);
        Assert.Equal(1, Convert.ToInt32(acme["id"]));
        Assert.True((bool)beta["linked"]!);
        Assert.Equal(2, Convert.ToInt32(beta["id"]));
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Sheet.CpCrossHelpersPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Sheet.CpCrossHelpersPath, StringComparison.Ordinal);
    }

    [Fact]
    public void PartsApi_KeysAndHostGate_StayOnTheConfiguredSurface()
    {
        PhpPlanQ1Spar.Reset();
        PhpPlanQ1Spar.File = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["api_key"] = "platformkey99",
            ["method_keys"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["getMakes"] = "makeskey1"
            },
            ["allow_auto_parts_tenants"] = "0"
        };
        PhpPlanQ1Spar.IsEpartsHost = true;
        Assert.True(PhpPlanQ1Spar.EpcPartsapiEnabledForRequest());
        Assert.Equal("makeskey1", PhpPlanQ1Spar.EpcPartsapiResolveKeyForMethod("getMakes"));
        Assert.Equal("platformkey99", PhpPlanQ1Spar.EpcPartsapiResolveKeyForMethod("getModels"));
        PhpPlanQ1Spar.IsEpartsHost = false;
        PhpPlanQ1Spar.IsAutoPartsSite = true;
        Assert.False(PhpPlanQ1Spar.EpcPartsapiEnabledForRequest());
        PhpPlanQ1Spar.File["allow_auto_parts_tenants"] = "1";
        Assert.True(PhpPlanQ1Spar.EpcPartsapiEnabledForRequest());
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Spar.PartsApiPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Spar.PartsApiPath, StringComparison.Ordinal);
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
            PhpPlanQ1Spar.PartsApiPath,
            PhpPlanQ1Boom.BlockchainBosPath,
            PhpPlanQ1Stay.PageAssetsPath,
            PhpPlanQ1Jib.TenantShowcasePath,
            PhpPlanQ1Gaff.WhatsappSharePath,
            PhpPlanQ1Sprit.MarketingBrochurePath,
            PhpPlanQ1Luff.DemoAutopartsBootstrapPath,
            PhpPlanQ1Clew.SeoShippingExportPath,
            PhpPlanQ1Tack.ProductExistLimitPath,
            PhpPlanQ1Vang.PosCpInstallPath,
            PhpPlanQ1Sheet.CpCrossHelpersPath,
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
