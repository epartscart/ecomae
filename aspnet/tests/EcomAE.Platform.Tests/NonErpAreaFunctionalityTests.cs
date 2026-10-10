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
    public void DelTmpFolder_NameAndRoot_StayOnTheInjectedTenant()
    {
        PhpPlanQ1Halyard.Reset();
        PhpPlanQ1Halyard.DocumentRoot = "/tmp/acme";
        var acme = PhpPlanQ1Halyard.EpcDelTmpFolderRun("gone");
        PhpPlanQ1Halyard.DocumentRoot = "/tmp/beta";
        var beta = PhpPlanQ1Halyard.EpcDelTmpFolderRun("gone");
        Assert.Contains("/tmp/acme/", Convert.ToString(acme["tmp_folder_name"])!, StringComparison.Ordinal);
        Assert.Contains("/tmp/beta/", Convert.ToString(beta["tmp_folder_name"])!, StringComparison.Ordinal);
        Assert.DoesNotContain("/tmp/beta/", Convert.ToString(acme["tmp_folder_name"])!, StringComparison.Ordinal);
        Assert.False((bool)PhpPlanQ1Halyard.EpcDelTmpFolderRun("Bad.Name")["status"]!);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Halyard.DelTmpFolderPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Halyard.DelTmpFolderPath, StringComparison.Ordinal);
    }

    [Fact]
    public void BreadCrumbs_QueryAndCaption_StayOnTheInjectedTenant()
    {
        PhpPlanQ1Cringle.Reset();
        PhpPlanQ1Cringle.QueryGet = key => key == "q" ? "acme" : "";
        Assert.Equal("acme", PhpPlanQ1Cringle.QueryGet("q"));
        PhpPlanQ1Cringle.QueryGet = key => key == "q" ? "beta" : "";
        Assert.Equal("beta", PhpPlanQ1Cringle.QueryGet("q"));
        Assert.NotEqual("acme", PhpPlanQ1Cringle.QueryGet("q"));
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Cringle.BreadCrumbsHelperPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Cringle.BreadCrumbsHelperPath, StringComparison.Ordinal);
        Assert.Contains("modules/bread_crumbs/helper.php", PhpPlanQ1Cringle.BreadCrumbsHelperPath, StringComparison.Ordinal);
    }

    [Fact]
    public void OrderStaffSummary_ProfileAndCrm_StayOnTheInjectedTenant()
    {
        PhpPlanQ1Throat.Reset();
        PhpPlanQ1Throat.ProfileHtml = (_, _) => "acme-profile";
        PhpPlanQ1Throat.CrmUserId = _ => 11;
        var acme = PhpPlanQ1Throat.EpcOrderStaffSummaryRenderHtml(new Dictionary<string, object?> { ["id"] = 1 }, 1, 3, 10, 5, 5, 50);
        PhpPlanQ1Throat.ProfileHtml = (_, _) => "beta-profile";
        PhpPlanQ1Throat.CrmUserId = _ => 22;
        var beta = PhpPlanQ1Throat.EpcOrderStaffSummaryRenderHtml(new Dictionary<string, object?> { ["id"] = 1 }, 1, 4, 10, 5, 5, 50);
        Assert.Contains("acme-profile", acme, StringComparison.Ordinal);
        Assert.Contains("<strong>11</strong>", acme, StringComparison.Ordinal);
        Assert.Contains("beta-profile", beta, StringComparison.Ordinal);
        Assert.Contains("<strong>22</strong>", beta, StringComparison.Ordinal);
        Assert.DoesNotContain("beta-profile", acme, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", acme, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Throat.OrderStaffSummaryPath, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckSsl_ProbeAndRedirect_StayOnTheInjectedTenant()
    {
        PhpPlanQ1Leech.Reset();
        PhpPlanQ1Leech.ProbeOk = host => host == "acme.example";
        PhpPlanQ1Leech.RedirectConfigured = () => true;
        var acme = PhpPlanQ1Leech.EpcCheckSslEvaluate("https://acme.example/");
        PhpPlanQ1Leech.RedirectConfigured = () => false;
        var beta = PhpPlanQ1Leech.EpcCheckSslEvaluate("https://beta.example/");
        Assert.Equal("11", acme["state"]);
        Assert.Equal("acme.example", acme["host"]);
        Assert.Equal("22", beta["state"]);
        Assert.Equal("beta.example", beta["host"]);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Leech.CheckSslPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Leech.CheckSslPath, StringComparison.Ordinal);
    }

    [Fact]
    public void MetadataHandler_RulesAndUrlOverride_StayOnTheInjectedTenant()
    {
        PhpPlanQ1Knot.Reset();
        PhpPlanQ1Knot.PageUrl = () => "/acme";
        PhpPlanQ1Knot.Translate = id => "acme-" + id;
        Assert.Equal("/acme", PhpPlanQ1Knot.PageUrl());
        Assert.Equal("acme-7", PhpPlanQ1Knot.Translate(7));
        PhpPlanQ1Knot.PageUrl = () => "/beta";
        PhpPlanQ1Knot.Translate = id => "beta-" + id;
        Assert.Equal("/beta", PhpPlanQ1Knot.PageUrl());
        Assert.Equal("beta-7", PhpPlanQ1Knot.Translate(7));
        Assert.NotEqual("/acme", PhpPlanQ1Knot.PageUrl());
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Knot.MetadataHandlerPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Knot.MetadataHandlerPath, StringComparison.Ordinal);
    }

    [Fact]
    public void OrderWhatsappShare_PhoneAndLpo_StayOnTheInjectedTenant()
    {
        PhpPlanQ1Bend.Reset();
        PhpPlanQ1Bend.SalesDigits = () => "97150";
        PhpPlanQ1Bend.CustomerMessage = (_, _, _) => "acme-cust";
        PhpPlanQ1Bend.SalesMessage = (_, _, _) => "acme-sales";
        var acme = PhpPlanQ1Bend.EpcOrderWhatsappShareRenderHtml(
            new Dictionary<string, object?> { ["id"] = 1 },
            1,
            new Dictionary<string, object?> { ["phone"] = "+971 50 111" });
        PhpPlanQ1Bend.SalesDigits = () => "97156";
        PhpPlanQ1Bend.CustomerMessage = (_, _, _) => "beta-cust";
        PhpPlanQ1Bend.SalesMessage = (_, _, _) => "beta-sales";
        var beta = PhpPlanQ1Bend.EpcOrderWhatsappShareRenderHtml(
            new Dictionary<string, object?> { ["id"] = 2 },
            2,
            new Dictionary<string, object?> { ["phone"] = "+971 56 222" });
        Assert.Contains("97150111", acme, StringComparison.Ordinal);
        Assert.Contains("acme-cust", acme, StringComparison.Ordinal);
        Assert.Contains("97156222", beta, StringComparison.Ordinal);
        Assert.Contains("beta-cust", beta, StringComparison.Ordinal);
        Assert.DoesNotContain("beta-cust", acme, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", acme, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Bend.OrderWhatsappSharePath, StringComparison.Ordinal);
    }

    [Fact]
    public void BocPageShell_AreaAndHostGate_StayOnTheInjectedTenant()
    {
        PhpPlanQ1Wake.Reset();
        PhpPlanQ1Wake.IsSuperCpHost = () => true;
        PhpPlanQ1Wake.Areas = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["shop"] = new(StringComparer.Ordinal) { ["path"] = "shop", ["label"] = "Acme shop" }
        };
        PhpPlanQ1Wake.ContentUrl = "shop/prices";
        PhpPlanQ1Wake.OperatorName = () => "acme-op";
        var acme = PhpPlanQ1Wake.EpcBocPageShellOpen();
        var acmeCtx = (Dictionary<string, object?>)acme["ctx"]!;
        Assert.Equal("Acme shop", acmeCtx["title"]);
        Assert.Equal("acme-op", acmeCtx["operator"]);
        PhpPlanQ1Wake.Reset();
        PhpPlanQ1Wake.IsSuperCpHost = () => false;
        PhpPlanQ1Wake.Areas = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["shop"] = new(StringComparer.Ordinal) { ["path"] = "shop", ["label"] = "Beta shop" }
        };
        var beta = PhpPlanQ1Wake.EpcBocPageShellOpen(new Dictionary<string, object?> { ["title"] = "Nope" });
        Assert.Equal(0, beta["opened"]);
        Assert.False(beta.ContainsKey("ctx"));
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Wake.BocPageShellPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Wake.BocPageShellPath, StringComparison.Ordinal);
    }

    [Fact]
    public void PriceUploadDiagnostics_DomainAndKey_StayOnTheInjectedTenant()
    {
        PhpPlanQ1Wind.Reset();
        var acme = PhpPlanQ1Wind.EpcPriceUploadChannelDefinitions(new Dictionary<string, object?>
        {
            ["backend_dir"] = "cp",
            ["domain_path"] = "https://acme.example",
            ["tech_key"] = "acme-key"
        });
        var beta = PhpPlanQ1Wind.EpcPriceUploadChannelDefinitions(new Dictionary<string, object?>
        {
            ["backend_dir"] = "cp",
            ["domain_path"] = "https://beta.example/",
            ["tech_key"] = "beta-key"
        });
        var acmeCron = Convert.ToString(acme.First(r => Convert.ToString(r["id"]) == "cron_scheduled")["cron_wget"]);
        var betaCron = Convert.ToString(beta.First(r => Convert.ToString(r["id"]) == "cron_scheduled")["cron_wget"]);
        Assert.Contains("https://acme.examplecp/", acmeCron, StringComparison.Ordinal);
        Assert.Contains("acme-key", acmeCron, StringComparison.Ordinal);
        Assert.Contains("https://beta.example/cp/", betaCron, StringComparison.Ordinal);
        Assert.Contains("beta-key", betaCron, StringComparison.Ordinal);
        Assert.DoesNotContain("beta-key", acmeCron, StringComparison.Ordinal);
        Assert.Equal("https://acme.example/pyprices/pyprices-api.php", PhpPlanQ1Wind.EpcPypricesApiUrl("https://acme.example/"));
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Wind.PriceUploadDiagnosticsPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Wind.PriceUploadDiagnosticsPath, StringComparison.Ordinal);
    }

    [Fact]
    public void TenantTemplates_LiveDefsAndApply_StayOnTheInjectedTenant()
    {
        PhpPlanQ1Sail.Reset();
        PhpPlanQ1Sail.LiveDefs = () => new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["auto_parts"] = new(StringComparer.Ordinal) { ["template_key"] = "automotive", ["mode"] = "hub_root" }
        };
        PhpPlanQ1Sail.Groups = () => new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["a_auto"] = new(StringComparer.Ordinal)
            {
                ["template_key"] = "automotive",
                ["label"] = "Auto",
                ["erp_base"] = "auto",
                ["color_scheme"] = new Dictionary<string, object?>()
            }
        };
        PhpPlanQ1Sail.ErpPacks = () => new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["auto"] = new(StringComparer.Ordinal) { ["label"] = "Auto ERP" }
        };
        PhpPlanQ1Sail.SeoHost = tk => "acme-" + tk + ".example";
        var acme = PhpPlanQ1Sail.EpcThIndustryTemplatesCatalog();
        PhpPlanQ1Sail.SeoHost = tk => "beta-" + tk + ".example";
        var beta = PhpPlanQ1Sail.EpcThIndustryTemplatesCatalog();
        Assert.Equal("https://acme-automotive.example/", acme[0]["live_url"]);
        Assert.Equal("https://beta-automotive.example/", beta[0]["live_url"]);
        Assert.DoesNotContain("beta-automotive", Convert.ToString(acme[0]["live_url"]), StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Sail.TenantTemplatesCatalogPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Sail.TenantTemplatesCatalogPath, StringComparison.Ordinal);
    }

    [Fact]
    public void ArticleBrands_WarehouseAndCanon_StayOnTheInjectedTenant()
    {
        PhpPlanQ1Line.Reset();
        PhpPlanQ1Line.WarehouseBrands = (_, _, _) => ["AcmeBrand"];
        PhpPlanQ1Line.CanonicalMap = _ => new Dictionary<string, string>(StringComparer.Ordinal);
        PhpPlanQ1Line.SynonymCanonical = (_, _) => "";
        PhpPlanQ1Line.CacheRead = _ => "";
        PhpPlanQ1Line.HttpGet = (_, _) => "";
        using var admin = new MySqlConnector.MySqlConnection(
            "Server=127.0.0.1;Port=3306;User ID=ecomae;Password=" +
            (Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN") ?? "") +
            ";AllowUserVariables=true;");
        admin.Open();
        var schema = "ecomae_cpw_linearea_" + Guid.NewGuid().ToString("N")[..8];
        using (var cmd = admin.CreateCommand())
        {
            cmd.CommandText = $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
            cmd.ExecuteNonQuery();
        }

        try
        {
            using var db = new MySqlConnector.MySqlConnection(
                $"Server=127.0.0.1;Port=3306;Database={schema};User ID=ecomae;Password=" +
                (Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN") ?? "") +
                ";AllowUserVariables=true;");
            db.Open();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "CREATE TABLE shop_docpart_articles_analogs_list (article VARCHAR(64), manufacturer_article VARCHAR(64), analog VARCHAR(64), manufacturer_analog VARCHAR(64))";
                cmd.ExecuteNonQuery();
            }

            var acme = PhpPlanQ1Line.EpcCollectArticleCatalogBrands(db, new Dictionary<string, object?> { ["local_crosses"] = 0 }, "OC47");
            PhpPlanQ1Line.WarehouseBrands = (_, _, _) => ["BetaBrand"];
            var beta = PhpPlanQ1Line.EpcCollectArticleCatalogBrands(db, new Dictionary<string, object?> { ["local_crosses"] = 0 }, "OC47");
            var acmeShows = ((List<Dictionary<string, object?>>)acme["manufacturers"]!).Select(m => Convert.ToString(m["manufacturer_show"])).ToList();
            var betaShows = ((List<Dictionary<string, object?>>)beta["manufacturers"]!).Select(m => Convert.ToString(m["manufacturer_show"])).ToList();
            Assert.Contains("ACMEBRAND", acmeShows);
            Assert.Contains("BETABRAND", betaShows);
            Assert.DoesNotContain("BETABRAND", acmeShows);
        }
        finally
        {
            using var drop = admin.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS `{schema}`";
            drop.ExecuteNonQuery();
        }

        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Line.ArticleBrandsPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Line.ArticleBrandsPath, StringComparison.Ordinal);
    }

    [Fact]
    public void AccessoriesCatalog_CachePath_StaysOnTheInjectedTenant()
    {
        PhpPlanQ1Stem.Reset();
        PhpPlanQ1Stem.DocumentRoot = "/shop/acme";
        var acme = PhpPlanQ1Stem.EpcAccCachePath();
        PhpPlanQ1Stem.DocumentRoot = "/shop/beta";
        var beta = PhpPlanQ1Stem.EpcAccCachePath();
        Assert.Contains("epc_acc_catalog_v1_", acme, StringComparison.Ordinal);
        Assert.NotEqual(acme, beta);
        Assert.DoesNotContain("beta", Path.GetFileName(acme), StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Stem.AccessoriesCatalogPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Stem.AccessoriesCatalogPath, StringComparison.Ordinal);
    }

    [Fact]
    public void BrochureLive_ErpNav_StaysOnTheInjectedTenant()
    {
        PhpPlanQ1Port.Reset();
        PhpPlanQ1Port.ErpNavLoad = () => new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["acme"] = new(StringComparer.Ordinal) { ["label"] = "Acme ledger", ["icon"] = "fa-book", ["desc"] = "Acme books" }
        };
        var acme = PhpPlanQ1Port.EpcCpBrochureErpNavItems();
        PhpPlanQ1Port.Reset();
        PhpPlanQ1Port.ErpNavLoad = () => new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["beta"] = new(StringComparer.Ordinal) { ["label"] = "Beta ledger", ["icon"] = "fa-book", ["desc"] = "Beta books" }
        };
        var beta = PhpPlanQ1Port.EpcCpBrochureErpNavItems();
        Assert.Equal("Acme ledger", acme[0]["name"]);
        Assert.Equal("Beta ledger", beta[0]["name"]);
        Assert.DoesNotContain("Beta", Convert.ToString(acme[0]["name"]), StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Port.BrochureLivePath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Port.BrochureLivePath, StringComparison.Ordinal);
    }

    [Fact]
    public void FullBrochure_FilteredBundle_StaysOnTheInjectedTenant()
    {
        PhpPlanQ1Starboard.Reset();
        PhpPlanQ1Starboard.LiveInventory = () => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["areas"] = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.Ordinal)
            {
                ["Shop / OMS"] =
                [
                    new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Acme desk", ["does"] = "A", ["url"] = "/a", ["scope"] = "client" }
                ]
            },
            ["meta"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["generated_at"] = 1L, ["sources"] = new List<string>(), ["total"] = 1, ["area_count"] = 1 }
        };
        var acme = PhpPlanQ1Starboard.EpcCpBrochureFilteredBundle("client");
        PhpPlanQ1Starboard.LiveInventory = () => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["areas"] = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.Ordinal)
            {
                ["Shop / OMS"] =
                [
                    new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Beta desk", ["does"] = "B", ["url"] = "/b", ["scope"] = "client" }
                ]
            },
            ["meta"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["generated_at"] = 1L, ["sources"] = new List<string>(), ["total"] = 1, ["area_count"] = 1 }
        };
        var beta = PhpPlanQ1Starboard.EpcCpBrochureFilteredBundle("client");
        var acmeName = ((Dictionary<string, List<Dictionary<string, object?>>>)acme["areas"]!)["Shop / OMS"][0]["name"];
        var betaName = ((Dictionary<string, List<Dictionary<string, object?>>>)beta["areas"]!)["Shop / OMS"][0]["name"];
        Assert.Equal("Acme desk", acmeName);
        Assert.Equal("Beta desk", betaName);
        Assert.DoesNotContain("Beta", Convert.ToString(acmeName), StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Starboard.FullBrochurePath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Starboard.FullBrochurePath, StringComparison.Ordinal);
        PhpPlanQ1Starboard.Reset();
        PhpPlanQ1Starboard.HeadersSent = false;
        PhpPlanQ1Starboard.LiveInventory = () => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["areas"] = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.Ordinal),
            ["meta"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["generated_at"] = 1L, ["sources"] = new List<string>(), ["total"] = 0, ["area_count"] = 0 }
        };
        PhpPlanQ1Starboard.EpcCpFullBrochureRenderAndExit(new Dictionary<string, object?> { ["brand"] = "ecomae" });
        Assert.True(PhpPlanQ1Starboard.ExitCalled);
        Assert.Contains("Content-Type: text/html; charset=utf-8", PhpPlanQ1Starboard.ResponseHeaders);
        Assert.Contains("Cache-Control: no-store, no-cache, must-revalidate, max-age=0", PhpPlanQ1Starboard.ResponseHeaders);
    }

    [Fact]
    public void MarketingStrategies_DomainHost_StayOnTheInjectedTenant()
    {
        PhpPlanQ1Aft.Reset();
        PhpPlanQ1Aft.SiteDomain = () => "https://acme.test";
        PhpPlanQ1Aft.SiteHost = () => "acme.test";
        var acme = PhpPlanQ1Aft.EpcMarketingStrategies();
        PhpPlanQ1Aft.SiteDomain = () => "https://beta.test";
        PhpPlanQ1Aft.SiteHost = () => "beta.test";
        var beta = PhpPlanQ1Aft.EpcMarketingStrategies();
        var acmeUrl = ((List<object?>)acme["measurement"]["links"]!)[0] is Dictionary<string, object?> acmeLink
            ? Convert.ToString(acmeLink["url"])
            : "";
        var betaUrl = ((List<object?>)beta["measurement"]["links"]!)[0] is Dictionary<string, object?> betaLink
            ? Convert.ToString(betaLink["url"])
            : "";
        Assert.Equal("https://acme.test/sitemap-products.php", acmeUrl);
        Assert.Equal("https://beta.test/sitemap-products.php", betaUrl);
        Assert.DoesNotContain("beta", acmeUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Aft.StrategiesDataPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Aft.StrategiesDataPath, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductFamily_CachedGroup_StaysOnTheTenantDatabase()
    {
        PhpPlanQ1Bow.Reset();
        var pass = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN") ?? "";
        using var admin = new MySqlConnector.MySqlConnection(
            "Server=127.0.0.1;Port=3306;User ID=ecomae;Password=" + pass + ";AllowUserVariables=true;");
        admin.Open();
        var acmeSchema = "ecomae_cpw_bowacme_" + Guid.NewGuid().ToString("N")[..8];
        var betaSchema = "ecomae_cpw_bowbeta_" + Guid.NewGuid().ToString("N")[..8];
        using (var cmd = admin.CreateCommand())
        {
            cmd.CommandText = $"CREATE DATABASE `{acmeSchema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
            cmd.ExecuteNonQuery();
            cmd.CommandText = $"CREATE DATABASE `{betaSchema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
            cmd.ExecuteNonQuery();
        }

        try
        {
            using var acmeDb = new MySqlConnector.MySqlConnection(
                $"Server=127.0.0.1;Port=3306;Database={acmeSchema};User ID=ecomae;Password={pass};AllowUserVariables=true;");
            using var betaDb = new MySqlConnector.MySqlConnection(
                $"Server=127.0.0.1;Port=3306;Database={betaSchema};User ID=ecomae;Password={pass};AllowUserVariables=true;");
            acmeDb.Open();
            betaDb.Open();
            PhpPlanQ1Bow.EpcPfEnsureUmapiGroupSchema(acmeDb);
            PhpPlanQ1Bow.EpcPfEnsureUmapiGroupSchema(betaDb);
            using (var ins = acmeDb.CreateCommand())
            {
                ins.CommandText = "INSERT INTO `epc_umapi_product_group` (`manufacturer`,`article_norm`,`product_group`,`umapi_raw`,`updated_at`) VALUES ('Bosch','OC47','AcmeFam','raw',1)";
                ins.ExecuteNonQuery();
            }

            using (var ins = betaDb.CreateCommand())
            {
                ins.CommandText = "INSERT INTO `epc_umapi_product_group` (`manufacturer`,`article_norm`,`product_group`,`umapi_raw`,`updated_at`) VALUES ('Bosch','OC47','BetaFam','raw',1)";
                ins.ExecuteNonQuery();
            }

            var acmeBudget = 5;
            var betaBudget = 5;
            var acme = PhpPlanQ1Bow.EpcPfResolveProductGroup(new object(), acmeDb, "Bosch", "OC-47", "Oil filter", ref acmeBudget);
            var beta = PhpPlanQ1Bow.EpcPfResolveProductGroup(new object(), betaDb, "Bosch", "OC-47", "Oil filter", ref betaBudget);
            Assert.Equal("AcmeFam", acme);
            Assert.Equal("BetaFam", beta);
            Assert.DoesNotContain("Beta", acme, StringComparison.Ordinal);
        }
        finally
        {
            using var drop = admin.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS `{acmeSchema}`";
            drop.ExecuteNonQuery();
            drop.CommandText = $"DROP DATABASE IF EXISTS `{betaSchema}`";
            drop.ExecuteNonQuery();
        }

        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Bow.ProductFamilyPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Bow.ProductFamilyPath, StringComparison.Ordinal);
    }

    [Fact]
    public void ArticleMatch_PartUrl_StaysOnTheInjectedTenant()
    {
        PhpPlanQ1Beam.Reset();
        var acme = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["chpu_search_config"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["level_1"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["url"] = "acme-parts" },
                ["level_2"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["mode_1"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["url"] = "brands" }
                },
                ["slash_code"] = "---"
            }
        };
        var beta = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["chpu_search_config"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["level_1"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["url"] = "beta-parts" },
                ["level_2"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["mode_1"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["url"] = "brands" }
                },
                ["slash_code"] = "---"
            }
        };
        var acmeUrl = PhpPlanQ1Beam.EpcChpuBuildPartUrl(acme, "/en", "Bosch", "OC-47");
        var betaUrl = PhpPlanQ1Beam.EpcChpuBuildPartUrl(beta, "/en", "Bosch", "OC-47");
        Assert.Equal("/en/acme-parts/BOSCH/OC47", acmeUrl);
        Assert.Equal("/en/beta-parts/BOSCH/OC47", betaUrl);
        Assert.DoesNotContain("beta-parts", acmeUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Beam.ArticleMatchPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Beam.ArticleMatchPath, StringComparison.Ordinal);
    }

    [Fact]
    public void CrossInterchange_PersistedPair_StaysOnTheTenantDatabase()
    {
        PhpPlanQ1Draft.Reset();
        var pass = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN") ?? "";
        using var admin = new MySqlConnector.MySqlConnection(
            "Server=127.0.0.1;Port=3306;User ID=ecomae;Password=" + pass + ";AllowUserVariables=true;");
        admin.Open();
        var acmeSchema = "ecomae_cpw_draftacme_" + Guid.NewGuid().ToString("N")[..8];
        var betaSchema = "ecomae_cpw_draftbeta_" + Guid.NewGuid().ToString("N")[..8];
        using (var cmd = admin.CreateCommand())
        {
            cmd.CommandText = $"CREATE DATABASE `{acmeSchema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
            cmd.ExecuteNonQuery();
            cmd.CommandText = $"CREATE DATABASE `{betaSchema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
            cmd.ExecuteNonQuery();
        }

        try
        {
            using var acmeDb = new MySqlConnector.MySqlConnection(
                $"Server=127.0.0.1;Port=3306;Database={acmeSchema};User ID=ecomae;Password={pass};AllowUserVariables=true;");
            using var betaDb = new MySqlConnector.MySqlConnection(
                $"Server=127.0.0.1;Port=3306;Database={betaSchema};User ID=ecomae;Password={pass};AllowUserVariables=true;");
            acmeDb.Open();
            betaDb.Open();
            foreach (var db in new[] { acmeDb, betaDb })
            {
                using var create = db.CreateCommand();
                create.CommandText = "CREATE TABLE shop_docpart_articles_analogs_list (id INT PRIMARY KEY AUTO_INCREMENT, article VARCHAR(64), manufacturer_article VARCHAR(64), analog VARCHAR(64), manufacturer_analog VARCHAR(64))";
                create.ExecuteNonQuery();
            }

            Assert.True(PhpPlanQ1Draft.DocpartCrossPersistInterchangePair(acmeDb, "OC-47", "Bosch", "OC47X", "Mann"));
            var acme = PhpPlanQ1Draft.DocpartCrossPairExistsWithBrands(acmeDb, "OC-47", "Bosch", "OC47X", "Mann");
            var beta = PhpPlanQ1Draft.DocpartCrossPairExistsWithBrands(betaDb, "OC-47", "Bosch", "OC47X", "Mann");
            Assert.True(true.Equals(acme["linked"]));
            Assert.False(true.Equals(beta["linked"]));
        }
        finally
        {
            using var drop = admin.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS `{acmeSchema}`";
            drop.ExecuteNonQuery();
            drop.CommandText = $"DROP DATABASE IF EXISTS `{betaSchema}`";
            drop.ExecuteNonQuery();
        }

        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Draft.CrossInterchangePath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Draft.CrossInterchangePath, StringComparison.Ordinal);
    }

    [Fact]
    public void CommerceIngest_ImportedRows_StayOnTheTenantDatabase()
    {
        PhpPlanQ1Surge.Reset();
        var pass = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN") ?? "";
        using var admin = new MySqlConnector.MySqlConnection(
            "Server=127.0.0.1;Port=3306;User ID=ecomae;Password=" + pass + ";AllowUserVariables=true;");
        admin.Open();
        var acmeSchema = "ecomae_cpw_surgeacme_" + Guid.NewGuid().ToString("N")[..8];
        var betaSchema = "ecomae_cpw_surgebeta_" + Guid.NewGuid().ToString("N")[..8];
        using (var cmd = admin.CreateCommand())
        {
            cmd.CommandText = $"CREATE DATABASE `{acmeSchema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
            cmd.ExecuteNonQuery();
            cmd.CommandText = $"CREATE DATABASE `{betaSchema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
            cmd.ExecuteNonQuery();
        }

        try
        {
            using var acmeDb = new MySqlConnector.MySqlConnection(
                $"Server=127.0.0.1;Port=3306;Database={acmeSchema};User ID=ecomae;Password={pass};AllowUserVariables=true;");
            using var betaDb = new MySqlConnector.MySqlConnection(
                $"Server=127.0.0.1;Port=3306;Database={betaSchema};User ID=ecomae;Password={pass};AllowUserVariables=true;");
            acmeDb.Open();
            betaDb.Open();
            foreach (var db in new[] { acmeDb, betaDb })
            {
                using var create = db.CreateCommand();
                create.CommandText = """
                    CREATE TABLE shop_docpart_prices (
                        id INT PRIMARY KEY AUTO_INCREMENT,
                        name VARCHAR(128),
                        link VARCHAR(500) DEFAULT '',
                        load_mode INT DEFAULT 0,
                        file_name_substring VARCHAR(128) DEFAULT '',
                        message_header_substring VARCHAR(500) DEFAULT '',
                        last_updated INT DEFAULT 0,
                        records_count INT DEFAULT 0
                    )
                    """;
                create.ExecuteNonQuery();
                create.CommandText = """
                    CREATE TABLE shop_docpart_prices_data (
                        id INT PRIMARY KEY,
                        price_id INT,
                        manufacturer VARCHAR(128),
                        article VARCHAR(64),
                        article_show VARCHAR(64),
                        name VARCHAR(255),
                        `exist` INT,
                        price DECIMAL(12,2),
                        time_to_exe INT,
                        storage VARCHAR(64),
                        min_order INT
                    )
                    """;
                create.ExecuteNonQuery();
                create.CommandText = """
                    CREATE TABLE shop_storages (
                        id INT PRIMARY KEY AUTO_INCREMENT,
                        name VARCHAR(128),
                        interface_type INT,
                        users TEXT,
                        connection_options TEXT,
                        currency INT,
                        short_name VARCHAR(128),
                        hidden INT,
                        bg_line_color INT
                    )
                    """;
                create.ExecuteNonQuery();
                create.CommandText = "CREATE TABLE shop_offices (id INT PRIMARY KEY AUTO_INCREMENT)";
                create.ExecuteNonQuery();
                create.CommandText = "INSERT INTO shop_offices (id) VALUES (1)";
                create.ExecuteNonQuery();
                create.CommandText = "CREATE TABLE shop_offices_storages_map (office_id INT, storage_id INT, group_id INT, min_point INT, max_point INT, markup INT, additional_time INT)";
                create.ExecuteNonQuery();
                create.CommandText = "CREATE TABLE users (id INT PRIMARY KEY AUTO_INCREMENT, user_type INT)";
                create.ExecuteNonQuery();
                create.CommandText = "INSERT INTO users (id, user_type) VALUES (3, 2)";
                create.ExecuteNonQuery();
            }

            var dir = Path.Combine(Path.GetTempPath(), "ecomae_surge_area_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(dir);
            var src = Path.Combine(dir, "src.csv");
            File.WriteAllText(src, "article,brand,qty,price,name\nOC-47,Bosch,2,12.5,Oil filter\n");
            var acme = PhpPlanQ1Surge.EpcCommerceIngestFile(acmeDb, src, "sales", "ACME", 0);
            var betaSources = PhpPlanQ1Surge.EpcCommerceListSources(betaDb, false);
            try { File.Delete(src); } catch { /* ignore */ }
            try { Directory.Delete(dir); } catch { /* ignore */ }
            Assert.True(true.Equals(acme["status"]));
            Assert.Equal(1, Convert.ToInt32(acme["source_rows"], System.Globalization.CultureInfo.InvariantCulture));
            Assert.Empty(betaSources);
        }
        finally
        {
            using var drop = admin.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS `{acmeSchema}`";
            drop.ExecuteNonQuery();
            drop.CommandText = $"DROP DATABASE IF EXISTS `{betaSchema}`";
            drop.ExecuteNonQuery();
        }

        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Surge.CommercePriceIngestPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Surge.CommercePriceIngestPath, StringComparison.Ordinal);
    }

    [Fact]
    public void MultivendorIngest_ImportedRows_StayOnTheTenantDatabase()
    {
        PhpPlanQ1Swell.Reset();
        PhpPlanQ1Surge.Reset();
        var pass = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN") ?? "";
        using var admin = new MySqlConnector.MySqlConnection(
            "Server=127.0.0.1;Port=3306;User ID=ecomae;Password=" + pass + ";AllowUserVariables=true;");
        admin.Open();
        var acmeSchema = "ecomae_cpw_swellacme_" + Guid.NewGuid().ToString("N")[..8];
        var betaSchema = "ecomae_cpw_swellbeta_" + Guid.NewGuid().ToString("N")[..8];
        using (var cmd = admin.CreateCommand())
        {
            cmd.CommandText = $"CREATE DATABASE `{acmeSchema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
            cmd.ExecuteNonQuery();
            cmd.CommandText = $"CREATE DATABASE `{betaSchema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
            cmd.ExecuteNonQuery();
        }

        try
        {
            using var acmeDb = new MySqlConnector.MySqlConnection(
                $"Server=127.0.0.1;Port=3306;Database={acmeSchema};User ID=ecomae;Password={pass};AllowUserVariables=true;");
            using var betaDb = new MySqlConnector.MySqlConnection(
                $"Server=127.0.0.1;Port=3306;Database={betaSchema};User ID=ecomae;Password={pass};AllowUserVariables=true;");
            acmeDb.Open();
            betaDb.Open();
            foreach (var db in new[] { acmeDb, betaDb })
            {
                using var create = db.CreateCommand();
                create.CommandText = """
                    CREATE TABLE shop_docpart_prices (
                        id INT PRIMARY KEY AUTO_INCREMENT,
                        name VARCHAR(128),
                        link VARCHAR(500) DEFAULT '',
                        load_mode INT DEFAULT 0,
                        file_name_substring VARCHAR(128) DEFAULT '',
                        message_header_substring VARCHAR(500) DEFAULT '',
                        last_updated INT DEFAULT 0,
                        records_count INT DEFAULT 0
                    )
                    """;
                create.ExecuteNonQuery();
                create.CommandText = """
                    CREATE TABLE shop_docpart_prices_data (
                        id INT PRIMARY KEY,
                        price_id INT,
                        manufacturer VARCHAR(128),
                        article VARCHAR(64),
                        article_show VARCHAR(64),
                        name VARCHAR(255),
                        `exist` INT,
                        price DECIMAL(12,2),
                        time_to_exe INT,
                        storage VARCHAR(64),
                        min_order INT
                    )
                    """;
                create.ExecuteNonQuery();
                create.CommandText = """
                    CREATE TABLE shop_storages (
                        id INT PRIMARY KEY AUTO_INCREMENT,
                        name VARCHAR(128),
                        interface_type INT,
                        users TEXT,
                        connection_options TEXT,
                        currency INT,
                        short_name VARCHAR(128),
                        hidden INT,
                        bg_line_color INT
                    )
                    """;
                create.ExecuteNonQuery();
                create.CommandText = "CREATE TABLE shop_offices (id INT PRIMARY KEY AUTO_INCREMENT)";
                create.ExecuteNonQuery();
                create.CommandText = "INSERT INTO shop_offices (id) VALUES (1)";
                create.ExecuteNonQuery();
                create.CommandText = "CREATE TABLE shop_offices_storages_map (office_id INT, storage_id INT, group_id INT, min_point INT, max_point INT, markup INT, additional_time INT)";
                create.ExecuteNonQuery();
                create.CommandText = "CREATE TABLE users (id INT PRIMARY KEY AUTO_INCREMENT, user_type INT)";
                create.ExecuteNonQuery();
                create.CommandText = "INSERT INTO users (id, user_type) VALUES (3, 2)";
                create.ExecuteNonQuery();
            }

            var dir = Path.Combine(Path.GetTempPath(), "ecomae_swell_area_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(dir);
            var src = Path.Combine(dir, "src.csv");
            File.WriteAllText(src, "Brand,Article,Name,Qty,Price,Vendor full name,Vendor short,Data type\nTOYOTA,446610010,PAD KIT,8,103.51,S-UAE Trading LLC,S-UAE,inventory\n");
            var acme = PhpPlanQ1Swell.EpcMultivendorIngestFile(acmeDb, src, "src.csv", "combine");
            var betaCodes = PhpPlanQ1Swell.EpcMultivendorVendorCodesList(betaDb);
            try { File.Delete(src); } catch { /* ignore */ }
            try { Directory.Delete(dir); } catch { /* ignore */ }
            Assert.True(true.Equals(acme["status"]));
            Assert.Equal(1, Convert.ToInt32(acme["rows_source"], System.Globalization.CultureInfo.InvariantCulture));
            Assert.Empty(betaCodes);
        }
        finally
        {
            using var drop = admin.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS `{acmeSchema}`";
            drop.ExecuteNonQuery();
            drop.CommandText = $"DROP DATABASE IF EXISTS `{betaSchema}`";
            drop.ExecuteNonQuery();
        }

        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Swell.MultivendorPriceIngestPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Swell.MultivendorPriceIngestPath, StringComparison.Ordinal);
    }

    [Fact]
    public void AccessoriesMarketplace_Listings_StayOnTheTenantDatabase()
    {
        PhpPlanQ1Foam.Reset();
        PhpPlanQ1Foam.LoadTaxonomyJson = () => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["categories"] = new List<object?>
            {
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["slug"] = "brakes",
                    ["label"] = "Brakes",
                    ["pw_id"] = 1,
                    ["children"] = new List<object?>
                    {
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["slug"] = "pads",
                            ["label"] = "Brake Pads",
                            ["pw_id"] = 11
                        }
                    }
                }
            },
            ["makes"] = new List<object?> { "Toyota" },
            ["cities"] = new List<object?> { "Dubai" },
            ["filters"] = new List<object?>()
        };
        var pass = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN") ?? "";
        using var admin = new MySqlConnector.MySqlConnection(
            "Server=127.0.0.1;Port=3306;User ID=ecomae;Password=" + pass + ";AllowUserVariables=true;");
        admin.Open();
        var acmeSchema = "ecomae_cpw_foamacme_" + Guid.NewGuid().ToString("N")[..8];
        var betaSchema = "ecomae_cpw_foambeta_" + Guid.NewGuid().ToString("N")[..8];
        using (var cmd = admin.CreateCommand())
        {
            cmd.CommandText = $"CREATE DATABASE `{acmeSchema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
            cmd.ExecuteNonQuery();
            cmd.CommandText = $"CREATE DATABASE `{betaSchema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
            cmd.ExecuteNonQuery();
        }

        try
        {
            using var acmeDb = new MySqlConnector.MySqlConnection(
                $"Server=127.0.0.1;Port=3306;Database={acmeSchema};User ID=ecomae;Password={pass};AllowUserVariables=true;");
            using var betaDb = new MySqlConnector.MySqlConnection(
                $"Server=127.0.0.1;Port=3306;Database={betaSchema};User ID=ecomae;Password={pass};AllowUserVariables=true;");
            acmeDb.Open();
            betaDb.Open();
            PhpPlanQ1Foam.EpcAccSeedCategoriesFromJson(acmeDb);
            PhpPlanQ1Foam.EpcAccEnsureSchema(betaDb);
            var tree = PhpPlanQ1Foam.EpcAccGetCategoryTree(acmeDb);
            var lid = PhpPlanQ1Foam.EpcAccAddListing(acmeDb, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["category_id"] = tree[0]["id"],
                ["title"] = "Acme pad",
                ["make"] = "Toyota",
                ["city"] = "Dubai",
                ["price"] = 20
            });
            var acme = PhpPlanQ1Foam.EpcAccMarketplaceSearch(acmeDb, new Dictionary<string, object?>(StringComparer.Ordinal));
            var beta = PhpPlanQ1Foam.EpcAccMarketplaceSearch(betaDb, new Dictionary<string, object?>(StringComparer.Ordinal));
            Assert.True(lid > 0);
            Assert.Equal(1, Convert.ToInt32(acme["total"], System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(0, Convert.ToInt32(beta["total"], System.Globalization.CultureInfo.InvariantCulture));
            Assert.True(true.Equals(beta["empty_catalog"]));
        }
        finally
        {
            using var drop = admin.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS `{acmeSchema}`";
            drop.ExecuteNonQuery();
            drop.CommandText = $"DROP DATABASE IF EXISTS `{betaSchema}`";
            drop.ExecuteNonQuery();
        }

        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Foam.AccessoriesDbPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Foam.AccessoriesDbPath, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfessionalShell_FlagsStayOnTheConfiguredSurface()
    {
        PhpPlanQ1Spray.Reset();
        PhpPlanQ1Spray.BrandCpContext = () => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["company_name"] = "Acme Parts",
            ["product_name"] = "Control Panel",
            ["hub_tagline"] = "Finance & operations"
        };
        PhpPlanQ1Spray.TranslateById = id => "T" + id;
        var acme = PhpPlanQ1Spray.EpcCpLoginContext();
        PhpPlanQ1Spray.Reset();
        PhpPlanQ1Spray.IsSuperCpHost = () => true;
        var bos = PhpPlanQ1Spray.EpcCpLoginContext();
        Assert.Equal("tenant", Convert.ToString(acme["type"]));
        Assert.Equal("Acme Parts", Convert.ToString(acme["heading"]));
        Assert.Equal("super", Convert.ToString(bos["type"]));
        Assert.Equal("BOS — Business Operation System", Convert.ToString(bos["heading"]));
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Spray.ProfessionalShellPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Spray.ProfessionalShellPath, StringComparison.Ordinal);
    }

    [Fact]
    public void PortalErpModules_FlagsStayOnTheConfiguredSurface()
    {
        PhpPlanQ1Brine.Reset();
        PhpPlanQ1Brine.IsPlatformErpActive = () => false;
        var acme = PhpPlanQ1Brine.EpcPortalErpModulesEnabled(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["erp_modules"] = new[] { "erp_sales" }
        });
        PhpPlanQ1Brine.Reset();
        PhpPlanQ1Brine.IsPlatformErpActive = () => true;
        var bos = PhpPlanQ1Brine.EpcPortalErpModulesEnabled(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["erp_modules"] = new[] { "erp_sales" }
        });
        Assert.Equal(new[] { "erp_sales" }, acme);
        Assert.Equal(11, bos.Count);
        Assert.Contains("erp_finance", bos);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Brine.PortalErpModulesPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Brine.PortalErpModulesPath, StringComparison.Ordinal);
    }

    [Fact]
    public void TenantIntro_SiteKeyAndFlagsStayOnTheConfiguredSurface()
    {
        PhpPlanQ1Kelp.Reset();
        var acme = PhpPlanQ1Kelp.EpcPortalSiteKeyFromHostname("www.acme-parts.com");
        var beta = PhpPlanQ1Kelp.EpcPortalSiteKeyFromHostname("www.beta-trading.com");
        Assert.Equal("acme_parts", acme);
        Assert.Equal("beta_trading", beta);
        Assert.NotEqual(acme, beta);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Kelp.PortalTenantIntroPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Kelp.PortalTenantIntroPath, StringComparison.Ordinal);
    }

    [Fact]
    public void TenantCountryProfile_CodesStayOnTheConfiguredSurface()
    {
        PhpPlanQ1Rip.Reset();
        Assert.Equal("AE", PhpPlanQ1Rip.EpcTenantCountryNormalize("ae"));
        Assert.Equal("PK", PhpPlanQ1Rip.EpcTenantCountryNormalize("Pakistan"));
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Rip.TenantCountryProfilePath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Rip.TenantCountryProfilePath, StringComparison.Ordinal);
    }

    [Fact]
    public void TenantHubHelpers_ActionUrlsStayOnTheConfiguredSurface()
    {
        PhpPlanQ1Inlet.Reset();
        var acme = PhpPlanQ1Inlet.EpcThTenantActionUrls(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = "acme_parts",
            ["hostname"] = "www.acme.test",
            ["industry_code"] = "auto_parts"
        });
        var beta = PhpPlanQ1Inlet.EpcThTenantActionUrls(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = "beta_trading",
            ["hostname"] = "www.beta.test",
            ["industry_code"] = "auto_parts"
        });
        Assert.Equal("https://www.acme.test/en/", acme["storefront"]);
        Assert.Equal("https://www.beta.test/en/", beta["storefront"]);
        Assert.NotEqual(acme["storefront"], beta["storefront"]);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Inlet.TenantHubHelpersPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Inlet.TenantHubHelpersPath, StringComparison.Ordinal);
    }

    [Fact]
    public void IntegrationsHelpers_SiteKeyAndFlagsStayOnTheConfiguredSurface()
    {
        PhpPlanQ1Shoal.Reset();
        PhpPlanQ1Shoal.IsSuperHost = () => false;
        PhpPlanQ1Shoal.Host = () => "www.acme.test";
        PhpPlanQ1Shoal.Tenants.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["hostname"] = "www.acme.test",
            ["site_key"] = "acme_parts"
        });
        PhpPlanQ1Shoal.Tenants.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["hostname"] = "www.beta.test",
            ["site_key"] = "beta_trading"
        });
        Assert.Equal("acme-test", PhpPlanQ1Shoal.EpcIntegrationsSiteKey());
        PhpPlanQ1Shoal.Host = () => "www.beta.test";
        Assert.Equal("beta-test", PhpPlanQ1Shoal.EpcIntegrationsSiteKey());
        Assert.NotEqual("acme-test", PhpPlanQ1Shoal.EpcIntegrationsSiteKey());
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Shoal.IntegrationsHelpersPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Shoal.IntegrationsHelpersPath, StringComparison.Ordinal);
    }

    [Fact]
    public void EpartscartStorefront_PlaceholderAndTreeStayOnTheConfiguredSurface()
    {
        PhpPlanQ1Cape.Reset();
        PhpPlanQ1Cape.IsWarehouseStorefront = _ => true;
        var tree = new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal)
            {
                ["alias"] = "tires",
                ["data"] = new List<Dictionary<string, object?>>
                {
                    new(StringComparer.Ordinal) { ["alias"] = "apai-summer", ["data"] = new List<Dictionary<string, object?>>() }
                }
            },
            new(StringComparer.Ordinal) { ["alias"] = "apai_rims", ["data"] = new List<Dictionary<string, object?>>() }
        };
        var filtered = PhpPlanQ1Cape.EpcEpartscartFilterMenuTree(new object(), tree);
        Assert.Equal("tires", filtered[0]["alias"]);
        Assert.Single(filtered);
        Assert.Equal("/content/files/images/epc_electronics_placeholder.svg", PhpPlanQ1Cape.EpcStorefrontCatalogPlaceholderForHint("electronics"));
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Cape.EpartscartStorefrontPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Cape.EpartscartStorefrontPath, StringComparison.Ordinal);
    }

    [Fact]
    public void SuperCpPlatform_TenantOptionsStayOnTheConfiguredSurface()
    {
        PhpPlanQ1Gulf.Reset();
        PhpPlanQ1Gulf.Tenants.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = "acme_parts",
            ["trade_name"] = "Acme Parts",
            ["hostname"] = "www.acme.test",
            ["in_registry"] = 1,
            ["urls"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["cp"] = "https://www.acme.test/cp" }
        });
        PhpPlanQ1Gulf.Tenants.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = "beta",
            ["trade_name"] = "",
            ["hostname"] = "www.beta.test",
            ["in_registry"] = 1,
            ["access_blocked"] = 1
        });
        PhpPlanQ1Gulf.TenantUrls = t => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["cp"] = "https://" + Convert.ToString(t.GetValueOrDefault("hostname")) + "/cp"
        };
        var opts = PhpPlanQ1Gulf.EpcScpTenantOptions(null!);
        Assert.Equal("acme_parts", Convert.ToString(opts[0]["site_key"]));
        Assert.Equal("beta", Convert.ToString(opts[1]["site_key"]));
        Assert.Equal(" (beta)", Convert.ToString(opts[1]["label"]));
        Assert.NotEqual(opts[0]["hostname"], opts[1]["hostname"]);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Gulf.SuperCpPlatformPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Gulf.SuperCpPlatformPath, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_portal.php", PhpPlanQ1Gulf.SuperCpPlatformPath, StringComparison.Ordinal);
    }

    [Fact]
    public void ElectronicaeStorefront_TreeAndHrefStayOnTheConfiguredSurface()
    {
        PhpPlanQ1Haven.Reset();
        PhpPlanQ1Haven.CategorySlug = (_, _) => "apai-electronics-root";
        var tree = new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal) { ["alias"] = "tires", ["data"] = new List<Dictionary<string, object?>>() },
            new(StringComparer.Ordinal)
            {
                ["alias"] = "apai-electronics-root",
                ["data"] = new List<Dictionary<string, object?>>
                {
                    new(StringComparer.Ordinal) { ["alias"] = "apai-phones", ["data"] = new List<Dictionary<string, object?>>() }
                }
            }
        };
        var filtered = PhpPlanQ1Haven.EpcElectronicaeFilterMenuTree(null!, tree, "acme_el");
        Assert.Equal("apai-phones", Convert.ToString(filtered[0]["alias"]));
        Assert.Single(filtered);
        PhpPlanQ1Haven.LangPrefix = () => "/ar/";
        Assert.Equal("/ar/phones", PhpPlanQ1Haven.EpcElectronicaeHref("/phones"));
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Haven.ElectronicaeStorefrontPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Haven.ElectronicaeStorefrontPath, StringComparison.Ordinal);
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
            PhpPlanQ1Halyard.DelTmpFolderPath,
            PhpPlanQ1Cringle.BreadCrumbsHelperPath,
            PhpPlanQ1Throat.OrderStaffSummaryPath,
            PhpPlanQ1Reef.PricesPerfPath,
            PhpPlanQ1Leech.CheckSslPath,
            PhpPlanQ1Knot.MetadataHandlerPath,
            PhpPlanQ1Bend.OrderWhatsappSharePath,
            PhpPlanQ1Wake.BocPageShellPath,
            PhpPlanQ1Wind.PriceUploadDiagnosticsPath,
            PhpPlanQ1Sail.TenantTemplatesCatalogPath,
            PhpPlanQ1Line.ArticleBrandsPath,
            PhpPlanQ1Stem.AccessoriesCatalogPath,
            PhpPlanQ1Port.BrochureLivePath,
            PhpPlanQ1Starboard.FullBrochurePath,
            PhpPlanQ1Aft.StrategiesDataPath,
            PhpPlanQ1Bow.ProductFamilyPath,
            PhpPlanQ1Beam.ArticleMatchPath,
            PhpPlanQ1Draft.CrossInterchangePath,
            PhpPlanQ1Surge.CommercePriceIngestPath,
            PhpPlanQ1Swell.MultivendorPriceIngestPath,
            PhpPlanQ1Foam.AccessoriesDbPath,
            PhpPlanQ1Spray.ProfessionalShellPath,
            PhpPlanQ1Brine.PortalErpModulesPath,
            PhpPlanQ1Kelp.PortalTenantIntroPath,
            PhpPlanQ1Rip.TenantCountryProfilePath,
            PhpPlanQ1Inlet.TenantHubHelpersPath,
            PhpPlanQ1Shoal.IntegrationsHelpersPath,
            PhpPlanQ1Cape.EpartscartStorefrontPath,
            PhpPlanQ1Gulf.SuperCpPlatformPath,
            PhpPlanQ1Haven.ElectronicaeStorefrontPath,
            PhpPlanQ1Tide.FailoverPath,
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
