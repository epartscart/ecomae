using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-hull CP social login. PHP identifiers kept for the inventory:
/// <c>epc_auth_social_providers</c>, <c>epc_auth_oauth_central_callback_url</c>,
/// <c>epc_auth_oauth_state_pack</c>, <c>epc_auth_oauth_state_unpack</c>,
/// <c>epc_auth_google_start_url</c>, <c>epc_auth_google_exchange_code</c>,
/// <c>epc_auth_google_verify_id_token</c>, <c>epc_auth_google_complete_login</c>,
/// <c>epc_cp_login_modern_auth_html</c>, <c>showOtpMsg</c>.
/// Leftover auth-common / portal / OTP-modal parents stay injected.
/// </summary>
public static class PhpPlanQ1Hull
{
    public const string AuthSocialPath = "content/general_pages/epc_auth_social.php";

    public static Dictionary<string, object?> OAuthConfig { get; set; } = DefaultOauth();
    public static string SigningSecret { get; set; } = "secret";
    public static Dictionary<string, object?> Policy { get; set; } = new(StringComparer.Ordinal)
    {
        ["password"] = true,
        ["email_otp"] = true,
        ["google_oauth"] = true
    };
    public static Func<long> Clock { get; set; } = () => 1760000000;
    public static Func<string> Nonce { get; set; } = () => "aa".PadRight(32, 'a');
    public static Func<string, string, (bool Ok, string Raw, string Error)>? HttpPost { get; set; }
    public static Func<string, Dictionary<string, object?>, Dictionary<string, object?>>? ResolveForMode { get; set; }
    public static Func<string, string, Dictionary<string, object?>>? ContextFromRegistry { get; set; }
    public static Func<string, string, Dictionary<string, object?>>? StorefrontFromRegistry { get; set; }
    public static Func<Dictionary<string, object?>, string, string, int>? ProvisionStorefront { get; set; }
    public static Func<Dictionary<string, object?>, string, string, int>? ProvisionCp { get; set; }
    public static Func<Dictionary<string, object?>, int, Dictionary<string, object?>>? FinishLogin { get; set; }
    public static Func<Dictionary<string, object?>, string>? PostLoginRedirect { get; set; }
    public static Func<string>? OauthButtonsHtml { get; set; }
    public static Func<Dictionary<string, object?>, string>? OtpModalHtml { get; set; }
    public static int HtmlCalls { get; set; }

    public static void Reset()
    {
        OAuthConfig = DefaultOauth();
        SigningSecret = "secret";
        Policy = new(StringComparer.Ordinal) { ["password"] = true, ["email_otp"] = true, ["google_oauth"] = true };
        Clock = () => 1760000000;
        Nonce = () => "aa".PadRight(32, 'a');
        HttpPost = null;
        ResolveForMode = null;
        ContextFromRegistry = null;
        StorefrontFromRegistry = null;
        ProvisionStorefront = null;
        ProvisionCp = null;
        FinishLogin = null;
        PostLoginRedirect = null;
        OauthButtonsHtml = null;
        OtpModalHtml = null;
        HtmlCalls = 0;
    }

    private static Dictionary<string, object?> DefaultOauth()
        => new(StringComparer.Ordinal)
        {
            ["google"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["client_id"] = "",
                ["client_secret"] = "",
                ["redirect_uri"] = "https://www.ecomae.com/epc-auth-google-callback.php"
            }
        };

    private static bool PhpEmpty(object? value)
        => value switch
        {
            null => true,
            false => true,
            0 => true,
            0L => true,
            "" => true,
            "0" => true,
            System.Collections.ICollection c => c.Count == 0,
            _ => false
        };

    private static string PhpString(object? value)
        => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

    private static string HtmlEsc(string value)
        => value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal);

    private static Dictionary<string, object?> GoogleCfg()
    {
        if (OAuthConfig.TryGetValue("google", out var g) && g is Dictionary<string, object?> d)
        {
            return d;
        }

        return new(StringComparer.Ordinal);
    }

    private static string NormalizeMode(string mode)
        => mode.Trim().ToLowerInvariant() == "storefront" ? "storefront" : "cp";

    private static string Base64Url(string json)
    {
        var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return raw;
    }

    private static string HmacHex(string payload)
        => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(SigningSecret), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

    /// <summary>PHP <c>epc_auth_social_providers</c>.</summary>
    public static Dictionary<string, Dictionary<string, object?>> EpcAuthSocialProviders()
    {
        var google = GoogleCfg();
        var clientId = PhpString(google.TryGetValue("client_id", out var id) ? id : "").Trim();
        var secret = PhpString(google.TryGetValue("client_secret", out var sec) ? sec : "").Trim();
        var row = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = "google",
            ["label"] = "Continue with Google",
            ["icon"] = "fa-google",
            ["enabled"] = clientId != "" && secret != "",
            ["start_url"] = clientId != "" ? "/epc-auth-google-start.php" : ""
        };
        if (clientId != "" && secret == "")
        {
            row["enabled"] = false;
            row["start_url"] = "/epc-auth-google-start.php";
        }

        return new(StringComparer.Ordinal) { ["google"] = row };
    }

    /// <summary>PHP <c>epc_auth_oauth_central_callback_url</c>.</summary>
    public static string EpcAuthOauthCentralCallbackUrl()
    {
        var google = GoogleCfg();
        var uri = PhpString(google.TryGetValue("redirect_uri", out var r) ? r : "").Trim();
        return uri != "" ? uri : "https://www.ecomae.com/epc-auth-google-callback.php";
    }

    /// <summary>PHP <c>epc_auth_oauth_state_pack</c>.</summary>
    public static string EpcAuthOauthStatePack(Dictionary<string, object?> context, string nonce)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["n"] = nonce,
            ["tk"] = PhpString(context.TryGetValue("tenant_key", out var tk) ? tk : ""),
            ["k"] = PhpString(context.TryGetValue("kind", out var k) ? k : ""),
            ["rh"] = PhpString(context.TryGetValue("return_host", out var rh) ? rh : ""),
            ["rp"] = PhpString(context.TryGetValue("return_path", out var rp) ? rp : "/cp/"),
            ["am"] = NormalizeMode(PhpString(context.TryGetValue("auth_mode", out var am) ? am : "cp")),
            ["lp"] = PhpString(context.TryGetValue("lang_prefix", out var lp) ? lp : ""),
            ["t"] = Clock()
        };
        var json = PhpJsonEncode(payload);
        var p = Base64Url(json);
        return p + "." + HmacHex(p);
    }

    /// <summary>PHP <c>epc_auth_oauth_state_unpack</c>.</summary>
    public static Dictionary<string, object?>? EpcAuthOauthStateUnpack(string state)
    {
        var parts = state.Split('.', 2);
        if (parts.Length != 2)
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(HmacHex(parts[0])), Encoding.UTF8.GetBytes(parts[1])))
        {
            return null;
        }

        var pad = (4 - parts[0].Length % 4) % 4;
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0].Replace('-', '+').Replace('_', '/') + new string('=', pad)));
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var data = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            data[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.Number => prop.Value.TryGetInt64(out var n) ? n : prop.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => prop.Value.GetString()
            };
        }

        if (PhpEmpty(data.TryGetValue("n", out var nVal) ? nVal : null) || PhpEmpty(data.TryGetValue("t", out var tVal) ? tVal : null))
        {
            return null;
        }

        if (Clock() - Convert.ToInt64(data["t"], CultureInfo.InvariantCulture) > 900)
        {
            return null;
        }

        return data;
    }

    /// <summary>PHP <c>epc_auth_google_start_url</c>.</summary>
    public static string EpcAuthGoogleStartUrl(Dictionary<string, object?> context)
    {
        var clientId = PhpString(GoogleCfg().TryGetValue("client_id", out var id) ? id : "").Trim();
        if (clientId == "")
        {
            return "";
        }

        var nonce = Nonce();
        var state = EpcAuthOauthStatePack(context, nonce);
        var q = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = EpcAuthOauthCentralCallbackUrl(),
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["state"] = state,
            ["nonce"] = nonce,
            ["prompt"] = "select_account",
            ["access_type"] = "online"
        };
        var query = string.Join("&", q.Select(kv => kv.Key + "=" + Uri.EscapeDataString(kv.Value).Replace("%20", "+", StringComparison.Ordinal)));
        return "https://accounts.google.com/o/oauth2/v2/auth?" + query;
    }

    /// <summary>PHP <c>epc_auth_google_verify_id_token</c>.</summary>
    public static Dictionary<string, object?> EpcAuthGoogleVerifyIdToken(string idToken, string clientId)
    {
        var parts = idToken.Split('.');
        if (parts.Length < 2)
        {
            return Fail("Malformed id_token");
        }

        try
        {
            var pad = (4 - parts[1].Length % 4) % 4;
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1].Replace('-', '+').Replace('_', '/') + new string('=', pad)));
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Fail("Invalid id_token payload");
            }

            var payload = doc.RootElement;
            var aud = payload.TryGetProperty("aud", out var audEl) ? audEl.GetString() ?? "" : "";
            var email = (payload.TryGetProperty("email", out var em) ? em.GetString() ?? "" : "").Trim().ToLowerInvariant();
            object? verifiedRaw = null;
            if (payload.TryGetProperty("email_verified", out var verEl))
            {
                verifiedRaw = verEl.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number => verEl.TryGetInt64(out var vn) ? vn : verEl.GetDouble(),
                    JsonValueKind.Null => null,
                    _ => verEl.GetString()
                };
            }

            var verified = !PhpEmpty(verifiedRaw);

            var iss = payload.TryGetProperty("iss", out var issEl) ? issEl.GetString() ?? "" : "";
            if (aud != clientId)
            {
                return Fail("id_token audience mismatch");
            }

            if (email == "" || !verified)
            {
                return Fail("Google account email not verified");
            }

            if (iss != "https://accounts.google.com" && iss != "accounts.google.com")
            {
                return Fail("id_token issuer invalid");
            }

            if (payload.TryGetProperty("exp", out var expEl) && expEl.TryGetInt64(out var exp) && exp < Clock())
            {
                return Fail("id_token expired");
            }

            return new(StringComparer.Ordinal)
            {
                ["ok"] = true,
                ["email"] = email,
                ["name"] = (payload.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "").Trim(),
                ["sub"] = payload.TryGetProperty("sub", out var sub) ? sub.GetString() ?? "" : ""
            };
        }
        catch (Exception)
        {
            return Fail("Invalid id_token payload");
        }
    }

    /// <summary>PHP <c>epc_auth_google_exchange_code</c>.</summary>
    public static Dictionary<string, object?> EpcAuthGoogleExchangeCode(string code)
    {
        var google = GoogleCfg();
        var clientId = PhpString(google.TryGetValue("client_id", out var id) ? id : "").Trim();
        var clientSecret = PhpString(google.TryGetValue("client_secret", out var sec) ? sec : "").Trim();
        if (clientId == "" || clientSecret == "" || code == "")
        {
            return Fail("Google OAuth not configured");
        }

        var body = "code=" + Uri.EscapeDataString(code)
            + "&client_id=" + Uri.EscapeDataString(clientId)
            + "&client_secret=" + Uri.EscapeDataString(clientSecret)
            + "&redirect_uri=" + Uri.EscapeDataString(EpcAuthOauthCentralCallbackUrl())
            + "&grant_type=authorization_code";
        var hit = HttpPost?.Invoke("https://oauth2.googleapis.com/token", body) ?? (false, "", "no-http");
        if (!hit.Ok)
        {
            return Fail("Token exchange failed: " + hit.Error);
        }

        try
        {
            using var doc = JsonDocument.Parse(hit.Raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("id_token", out var tok) || PhpEmpty(tok.GetString()))
            {
                return Fail("Invalid token response from Google");
            }

            var profile = EpcAuthGoogleVerifyIdToken(tok.GetString() ?? "", clientId);
            if (PhpEmpty(profile.TryGetValue("ok", out var ok) ? ok : null))
            {
                return profile;
            }

            return new(StringComparer.Ordinal) { ["ok"] = true, ["profile"] = profile };
        }
        catch (JsonException)
        {
            return Fail("Invalid token response from Google");
        }
    }

    /// <summary>PHP <c>epc_auth_google_complete_login</c>.</summary>
    public static Dictionary<string, object?> EpcAuthGoogleCompleteLogin(Dictionary<string, object?> stateData, Dictionary<string, object?> profile)
    {
        var authMode = NormalizeMode(PhpString(stateData.TryGetValue("am", out var am) ? am : "cp"));
        var hints = new Dictionary<string, object?>(StringComparer.Ordinal) { ["tenant_key"] = PhpString(stateData.TryGetValue("tk", out var tk) ? tk : "") };
        var ctx = ResolveForMode?.Invoke(authMode, hints) ?? new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false };
        if (PhpEmpty(ctx.TryGetValue("ok", out var ok1) ? ok1 : null))
        {
            var key = PhpString(stateData.TryGetValue("tk", out var tk2) ? tk2 : "");
            if (authMode == "cp" && key != "")
            {
                ctx = ContextFromRegistry?.Invoke(key, PhpString(stateData.TryGetValue("k", out var k) ? k : ""))
                    ?? new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false };
            }

            if (PhpEmpty(ctx.TryGetValue("ok", out var ok2) ? ok2 : null) && key != "")
            {
                ctx = StorefrontFromRegistry?.Invoke(key, PhpString(stateData.TryGetValue("k", out var k2) ? k2 : ""))
                    ?? new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false };
            }
        }

        if (PhpEmpty(ctx.TryGetValue("ok", out var ok3) ? ok3 : null))
        {
            return Fail(PhpString(ctx.TryGetValue("message", out var msg) ? msg : "Tenant context lost"));
        }

        ctx["auth_mode"] = authMode;
        if (!PhpEmpty(stateData.TryGetValue("rh", out var rh) ? rh : null))
        {
            ctx["return_host"] = PhpString(rh);
        }

        if (!PhpEmpty(stateData.TryGetValue("rp", out var rp) ? rp : null))
        {
            ctx["return_path"] = PhpString(rp);
        }

        if (!PhpEmpty(stateData.TryGetValue("lp", out var lp) ? lp : null))
        {
            ctx["lang_prefix"] = PhpString(lp);
        }

        var email = PhpString(profile.TryGetValue("email", out var em) ? em : "");
        var name = PhpString(profile.TryGetValue("name", out var nm) ? nm : "");
        int userId;
        if (authMode == "storefront")
        {
            userId = ProvisionStorefront?.Invoke(ctx, email, name) ?? 0;
            if (userId <= 0)
            {
                return Fail("Could not sign in with this Google account");
            }
        }
        else
        {
            userId = ProvisionCp?.Invoke(ctx, email, name) ?? 0;
            if (userId <= 0)
            {
                return Fail("No CP access for this Google account on this workspace");
            }
        }

        var finish = FinishLogin?.Invoke(ctx, userId) ?? new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false };
        if (PhpEmpty(finish.TryGetValue("ok", out var okf) ? okf : null))
        {
            return Fail(PhpString(finish.TryGetValue("message", out var fm) ? fm : "Could not create session"));
        }

        return new(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["redirect"] = PhpString(finish.TryGetValue("redirect", out var redir) ? redir : PostLoginRedirect?.Invoke(ctx) ?? "")
        };
    }

    /// <summary>PHP <c>epc_cp_login_modern_auth_html</c> (JS helper <c>showOtpMsg</c>).</summary>
    public static string EpcCpLoginModernAuthHtml(Dictionary<string, object?> ui)
    {
        HtmlCalls++;
        if (HtmlCalls > 1)
        {
            return "";
        }

        var tenantKey = PhpString(ui.TryGetValue("tenant_key", out var tk) ? tk : "");
        var providers = EpcAuthSocialProviders();
        var google = providers["google"];
        var googleConfigured = !PhpEmpty(google["enabled"]);
        var passwordOn = !PhpEmpty(Policy.TryGetValue("password", out var pw) ? pw : null);
        var emailOtpOn = !PhpEmpty(Policy.TryGetValue("email_otp", out var otp) ? otp : null);
        var googleOn = !PhpEmpty(Policy.TryGetValue("google_oauth", out var go) ? go : null) && googleConfigured;
        var authContext = NormalizeMode(PhpString(ui.TryGetValue("context", out var ctx) ? ctx : "cp"));
        var label = PhpString(ui.TryGetValue("login_label", out var lb) ? lb : "Control Panel");
        var socialButtons = OauthButtonsHtml?.Invoke() ?? "";
        var defaultTab = passwordOn ? "password" : emailOtpOn ? "email_code" : "password";
        var tkEsc = HtmlEsc(tenantKey);
        var ctxEsc = HtmlEsc(authContext);
        var sb = new StringBuilder();
        sb.Append("<div class=\"epc-cp-auth-modern\" id=\"epc_cp_auth_modern\" data-tenant-key=\"").Append(tkEsc).Append("\" data-auth-context=\"").Append(ctxEsc).Append("\">\n");
        sb.Append("<p class=\"epc-cp-auth-title\">Sign in to ").Append(HtmlEsc(label)).Append("</p>\n");
        if (socialButtons != "")
        {
            sb.Append(socialButtons).Append("<div class=\"epc-social-divider\"><span>Or</span></div>\n");
        }

        if (passwordOn || emailOtpOn)
        {
            sb.Append("<div class=\"epc-cp-auth-tabs\" role=\"tablist\">\n");
            if (passwordOn)
            {
                sb.Append("  <button type=\"button\" class=\"epc-cp-auth-tab is-active\" data-tab=\"password\" role=\"tab\">Password</button>\n");
            }

            if (emailOtpOn)
            {
                sb.Append("  <button type=\"button\" class=\"epc-cp-auth-tab").Append(passwordOn ? "" : " is-active").Append("\" data-tab=\"email_code\" role=\"tab\">Email code</button>\n");
            }

            sb.Append("</div>\n");
        }

        sb.Append('\n');
        if (passwordOn)
        {
            sb.Append("<div class=\"epc-cp-auth-pane is-active\" data-pane=\"password\">\n  <p class=\"epc-cp-auth-hint\">Sign in with your CP password below.</p>\n</div>\n");
        }

        sb.Append('\n');
        if (emailOtpOn)
        {
            sb.Append("<div class=\"epc-cp-auth-pane").Append(passwordOn ? "" : " is-active").Append("\" data-pane=\"email_code\">\n");
            sb.Append("  <div class=\"form-group\">\n    <label class=\"control-label\" for=\"epc_auth_email\">Email</label>\n");
            sb.Append("    <input type=\"email\" class=\"form-control\" id=\"epc_auth_email\" autocomplete=\"email\" placeholder=\"you@company.com\" />\n  </div>\n");
            sb.Append("  <button type=\"button\" class=\"btn btn-block epc-cp-auth-continue-email\" id=\"epc_auth_send_code\">Continue with Email</button>\n");
            sb.Append("  <p class=\"epc-cp-auth-msg\" id=\"epc_auth_otp_msg\" aria-live=\"polite\"></p>\n</div>\n");
        }

        sb.Append("</div>\n\n");
        if (emailOtpOn && OtpModalHtml != null)
        {
            sb.Append(OtpModalHtml(ui));
        }

        sb.Append("<script>\n(function(){\nvar root=document.getElementById('epc_cp_auth_modern');\nif(!root)return;\nvar authContext=root.getAttribute('data-auth-context')||'cp';\nvar defaultTab=");
        sb.Append(JsonSerializer.Serialize(defaultTab));
        sb.Append(";\n\n// Tab switching\nroot.querySelectorAll('.epc-cp-auth-tab').forEach(function(btn){\n  btn.addEventListener('click',function(){\n    var t=btn.getAttribute('data-tab');\n    root.querySelectorAll('.epc-cp-auth-tab').forEach(function(b){b.classList.toggle('is-active',b===btn);});\n    root.querySelectorAll('.epc-cp-auth-pane').forEach(function(p){p.classList.toggle('is-active',p.getAttribute('data-pane')===t);});\n    var lf=document.getElementById('login_form');\n    if(lf)lf.style.display=(t==='password')?'':'none';\n  });\n});\nvar lf=document.getElementById('login_form');\nif(lf&&defaultTab!=='password')lf.style.display='none';\n");
        if (emailOtpOn)
        {
            sb.Append("\n// Send code → open modal\nvar sendBtn=document.getElementById('epc_auth_send_code');\nvar emailIn=document.getElementById('epc_auth_email');\nvar msgEl=document.getElementById('epc_auth_otp_msg');\nfunction showOtpMsg(t,ok){if(msgEl){msgEl.textContent=t;msgEl.className='epc-cp-auth-msg'+(ok?' is-ok':' is-err');}}\nif(sendBtn){\n  sendBtn.addEventListener('click',function(){\n    var em=(emailIn||{}).value||'';\n    em=em.trim();\n    if(!em||!/^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$/.test(em)){\n      showOtpMsg('Please enter a valid email address.',false);return;\n    }\n    showOtpMsg('',true);\n    if(window.EpcOtpModal&&window.EpcOtpModal['epc_cp_otp_modal']){\n      window.EpcOtpModal['epc_cp_otp_modal'].open(em);\n    }\n  });\n}\nif(emailIn){\n  emailIn.addEventListener('keydown',function(e){if(e.key==='Enter'){e.preventDefault();if(sendBtn)sendBtn.click();}});\n}\n");
        }

        sb.Append("})();\n</script>\n");
        _ = googleOn;
        return sb.ToString();
    }

    private static Dictionary<string, object?> Fail(string message)
        => new(StringComparer.Ordinal) { ["ok"] = false, ["message"] = message };

    /// <summary>PHP <c>json_encode</c> default (slash-escaped, no unicode flags).</summary>
    private static string PhpJsonEncode(Dictionary<string, object?> payload)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        var first = true;
        foreach (var kv in payload)
        {
            if (!first)
            {
                sb.Append(',');
            }

            first = false;
            sb.Append(PhpJsonString(kv.Key)).Append(':');
            if (kv.Value is long or int or short or byte)
            {
                sb.Append(Convert.ToString(kv.Value, CultureInfo.InvariantCulture));
            }
            else
            {
                sb.Append(PhpJsonString(PhpString(kv.Value)));
            }
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static string PhpJsonString(string value)
    {
        var sb = new StringBuilder("\"");
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '/': sb.Append("\\/"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ')
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        sb.Append('"');
        return sb.ToString();
    }
}
