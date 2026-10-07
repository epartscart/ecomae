using System.Text;
using System.Text.Json;

namespace EcomAE.Platform.Auth;

/// <summary>
/// PHP twin: <c>api/epc_oauth_callback.php</c>.
/// Errors are a small HTML card (never a 500). Missing or bad state is 400.
/// A valid state for an unconfigured provider is 503 — start uses 422, the callback does not.
/// </summary>
public static class OAuthCallback
{
    public static OAuthCallbackDecision Decide(
        string? error,
        string? errorDescription,
        string? code,
        string? state,
        string? signingSecret,
        long unixNow,
        bool providerConfigured)
    {
        if (!OAuthStart.PhpEmpty(error))
        {
            var desc = OAuthStart.PhpEmpty(errorDescription) ? error! : errorDescription!;
            return Fail(StatusCodes.Status400BadRequest, "The sign-in was cancelled or failed: " + desc);
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            return Fail(StatusCodes.Status400BadRequest, "Missing sign-in parameters.");
        }

        var unpacked = OAuthStart.UnpackState(state, signingSecret, unixNow);
        if (unpacked is null)
        {
            return Fail(StatusCodes.Status400BadRequest, "This sign-in link has expired. Please try again.");
        }

        if (!providerConfigured || !OAuthStart.IsKnownProvider(unpacked.Provider))
        {
            return Fail(
                StatusCodes.Status503ServiceUnavailable,
                UcFirst(unpacked.Provider) + " sign-in is not configured.");
        }

        return new OAuthCallbackDecision(0, string.Empty, null, true, unpacked);
    }

    public static OAuthCallbackDecision Fail(int statusCode, string message)
        => new(statusCode, FailHtml(message), null, false, null);

    public static string FailHtml(string message)
    {
        var safe = OAuthStart.HtmlEncode(message);
        return "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">"
            + "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">"
            + "<title>Sign-in</title>"
            + "<style>body{font-family:-apple-system,Segoe UI,Roboto,Arial,sans-serif;background:#f5f6f8;"
            + "display:flex;align-items:center;justify-content:center;min-height:100vh;margin:0;color:#222}"
            + ".card{background:#fff;border-radius:14px;box-shadow:0 18px 50px rgba(0,0,0,.12);"
            + "padding:32px 28px;max-width:380px;width:calc(100% - 32px);text-align:center}"
            + "h1{font-size:18px;margin:0 0 10px}p{font-size:14px;color:#555;line-height:1.5;margin:0 0 18px}"
            + "a{display:inline-block;padding:10px 18px;background:#2563eb;color:#fff;text-decoration:none;"
            + "border-radius:8px;font-size:14px;font-weight:600}</style></head><body>"
            + "<div class=\"card\"><h1>We couldn&rsquo;t sign you in</h1>"
            + "<p>" + safe + "</p>"
            + "<a href=\"javascript:history.length>1?history.back():(location.href='/')\">Go back</a></div></body></html>";
    }

    public static string UcFirst(string provider)
    {
        if (string.IsNullOrEmpty(provider))
        {
            return string.Empty;
        }

        return char.ToUpperInvariant(provider[0]) + provider[1..];
    }

    /// <summary>Relative post-login path. Rejects off-site return URLs.</summary>
    public static string SafeReturnPath(OAuthUnpackedState state)
    {
        var candidate = FirstPath(state.ReturnUrl) ?? FirstPath(state.ReturnPath);
        if (candidate is not null)
        {
            return candidate;
        }

        return state.AuthMode == "storefront" ? "/en/" : "/cp/control";
    }

    public static bool NeedsHandoff(string? currentHost, string? returnHost)
    {
        var here = NormalizeHost(currentHost);
        var target = NormalizeHost(returnHost);
        return target.Length > 0 && !string.Equals(here, target, StringComparison.Ordinal);
    }

    /// <summary>PHP <c>epc_auth_handoff_build</c> query, without the host prefix.</summary>
    public static string PackHandoff(int userId, string sessionToken, OAuthUnpackedState state, string signingSecret, long expiresUnix)
    {
        var json = "{"
            + "\"uid\":" + userId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ",\"sess\":" + OAuthStart.PhpJsonString(sessionToken)
            + ",\"host\":" + OAuthStart.PhpJsonString(state.ReturnHost)
            + ",\"path\":" + OAuthStart.PhpJsonString(SafeReturnPath(state))
            + ",\"exp\":" + expiresUnix.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ",\"tk\":" + OAuthStart.PhpJsonString(state.TenantKey)
            + ",\"mode\":" + OAuthStart.PhpJsonString(state.AuthMode)
            + "}";
        var payload = OAuthStart.Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        var sig = OAuthStart.HmacHex(payload, signingSecret ?? string.Empty);
        return "p=" + Uri.EscapeDataString(payload) + "&s=" + Uri.EscapeDataString(sig);
    }

    public static OAuthHandoffPayload? UnpackHandoff(string? payload, string? signature, string? signingSecret, long unixNow)
    {
        if (string.IsNullOrEmpty(payload) || string.IsNullOrEmpty(signature))
        {
            return null;
        }

        var expected = OAuthStart.HmacHex(payload, signingSecret ?? string.Empty);
        var sigBytes = Encoding.UTF8.GetBytes(signature);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        if (sigBytes.Length != expectedBytes.Length
            || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(sigBytes, expectedBytes))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(OAuthStart.Base64UrlDecode(payload));
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var root = doc.RootElement;
            if (!root.TryGetProperty("exp", out var expEl) || !expEl.TryGetInt64(out var exp) || exp < unixNow)
            {
                return null;
            }

            var userId = root.TryGetProperty("uid", out var uid) && uid.TryGetInt32(out var id) ? id : 0;
            var session = root.TryGetProperty("sess", out var sess) && sess.ValueKind == JsonValueKind.String
                ? sess.GetString() ?? string.Empty
                : string.Empty;
            if (userId <= 0 || session.Length == 0)
            {
                return null;
            }

            var mode = root.TryGetProperty("mode", out var modeEl) && modeEl.ValueKind == JsonValueKind.String
                ? OAuthStart.NormalizeMode(modeEl.GetString())
                : "cp";
            var path = root.TryGetProperty("path", out var pathEl) && pathEl.ValueKind == JsonValueKind.String
                ? pathEl.GetString() ?? "/"
                : "/";
            if (path.Length == 0 || path[0] != '/')
            {
                path = "/" + path.TrimStart('/');
            }

            var host = root.TryGetProperty("host", out var hostEl) && hostEl.ValueKind == JsonValueKind.String
                ? hostEl.GetString() ?? string.Empty
                : string.Empty;
            return new OAuthHandoffPayload(userId, session, mode, path, host);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }

    public static string NormalizeHost(string? host)
    {
        var value = (host ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();
        var colon = value.IndexOf(':');
        return colon >= 0 ? value[..colon] : value;
    }

    private static string? FirstPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var path = value.Trim();
        if (path.Contains("://", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out var absolute)
                && (absolute.Scheme == Uri.UriSchemeHttps || absolute.Scheme == Uri.UriSchemeHttp))
            {
                path = string.IsNullOrEmpty(absolute.PathAndQuery) ? "/" : absolute.PathAndQuery;
            }
            else
            {
                return null;
            }
        }

        if (path.Length == 0 || path[0] != '/' || path.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        return path;
    }
}

public readonly record struct OAuthCallbackDecision(
    int StatusCode,
    string Html,
    string? Location,
    bool Continue,
    OAuthUnpackedState? State);

public sealed record OAuthHandoffPayload(int UserId, string SessionToken, string Mode, string Path, string Host);

public sealed class OAuthExchangeResult
{
    public bool Ok { get; init; }
    public string Message { get; init; } = "";
    public string Email { get; init; } = "";
    public string Name { get; init; } = "";
    public string Subject { get; init; } = "";
    public bool EmailVerified { get; init; }

    public static OAuthExchangeResult Failed(string message) => new() { Ok = false, Message = message };

    public static OAuthExchangeResult Verified(string email, string name, string subject)
        => new()
        {
            Ok = true,
            Email = email.Trim().ToLowerInvariant(),
            Name = name.Trim(),
            Subject = subject,
            EmailVerified = true,
        };
}
