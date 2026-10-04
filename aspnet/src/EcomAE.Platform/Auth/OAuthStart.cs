using System.Security.Cryptography;
using System.Text;

namespace EcomAE.Platform.Auth;

/// <summary>
/// PHP twin: <c>api/epc_oauth_start.php</c> + <c>epc_oauth_build_auth_url</c>.
/// Unknown provider is 400, unconfigured provider is 422 (never the platform splash / 503),
/// a failed sign-in context is 400, and a configured provider is a 302 to the authorize URL.
/// State is HMAC-SHA256 over PHP <c>json_encode</c> bytes so the PHP callback can unpack it.
/// </summary>
public static class OAuthStart
{
    public const string CallbackUrl = "https://www.ecomae.com/api/epc_oauth_callback.php";

    public const string NotConfiguredSuffix =
        " sign-in is not configured yet. Super CP → Auth settings / epc_oauth_config, or config.epc-oauth.php. Callback URI must be "
        + CallbackUrl;

    private static readonly string[] KnownProviders = ["google", "microsoft", "facebook", "github", "apple"];

    public static bool IsKnownProvider(string? provider)
        => provider is not null && KnownProviders.Contains(provider, StringComparer.Ordinal);

    public static string NormalizeProvider(string? provider)
        => (provider ?? string.Empty).Trim().ToLowerInvariant();

    public static string NormalizeMode(string? mode)
        => string.Equals((mode ?? string.Empty).Trim(), "storefront", StringComparison.OrdinalIgnoreCase)
            ? "storefront"
            : "cp";

    /// <summary>PHP <c>empty()</c> on the terms query value: only "" and "0" are empty.</summary>
    public static bool TermsAccepted(string? terms)
        => terms is not null && terms.Length > 0 && !string.Equals(terms, "0", StringComparison.Ordinal);

    public static bool IsConfigured(string provider, OAuthProviderCredentials credentials)
    {
        if (!IsKnownProvider(provider) || !credentials.Enabled || string.IsNullOrEmpty(credentials.ClientId))
        {
            return false;
        }

        if (provider == "apple")
        {
            var haveSecret = !string.IsNullOrWhiteSpace(credentials.ClientSecret);
            var extra = credentials.Extra;
            var haveKey = !string.IsNullOrWhiteSpace(Extra(extra, "private_key"))
                && !string.IsNullOrWhiteSpace(Extra(extra, "team_id"))
                && !string.IsNullOrWhiteSpace(Extra(extra, "key_id"));
            return haveSecret || haveKey;
        }

        return !string.IsNullOrWhiteSpace(credentials.ClientSecret);
    }

    public static OAuthStartResponse Decide(
        string? providerRaw,
        string? contextMode,
        string? returnUrl,
        bool termsAccepted,
        OAuthProviderCredentials credentials,
        OAuthSignInContext context,
        string signingSecret,
        string nonce,
        long unixTime)
    {
        var provider = NormalizeProvider(providerRaw);
        if (!IsKnownProvider(provider))
        {
            return OAuthStartResponse.Plain(StatusCodes.Status400BadRequest, "Unknown sign-in provider.");
        }

        if (!IsConfigured(provider, credentials))
        {
            return OAuthStartResponse.Plain(
                StatusCodes.Status422UnprocessableEntity,
                char.ToUpperInvariant(provider[0]) + provider[1..] + NotConfiguredSuffix);
        }

        if (!context.Ok)
        {
            var message = string.IsNullOrWhiteSpace(context.Error) ? "unknown" : context.Error.Trim();
            return OAuthStartResponse.Plain(
                StatusCodes.Status400BadRequest,
                "Sign-in context error: " + HtmlEncode(message));
        }

        var url = BuildAuthorizeUrl(provider, credentials, context, NormalizeMode(contextMode), returnUrl, termsAccepted, signingSecret, nonce, unixTime);
        if (string.IsNullOrEmpty(url))
        {
            return OAuthStartResponse.Plain(
                StatusCodes.Status422UnprocessableEntity,
                char.ToUpperInvariant(provider[0]) + provider[1..] + NotConfiguredSuffix);
        }

        return OAuthStartResponse.Redirect(url);
    }

    public static string BuildAuthorizeUrl(
        string provider,
        OAuthProviderCredentials credentials,
        OAuthSignInContext context,
        string authMode,
        string? returnUrl,
        bool termsAccepted,
        string signingSecret,
        string nonce,
        long unixTime)
    {
        if (!IsConfigured(provider, credentials))
        {
            return string.Empty;
        }

        var authorize = AuthorizeUrl(provider, credentials);
        var state = PackState(provider, context, authMode, returnUrl, termsAccepted, signingSecret, nonce, unixTime);
        var scope = provider switch
        {
            "facebook" => "email public_profile",
            "github" => "read:user user:email",
            "apple" => "name email",
            _ => "openid email profile",
        };

        var pairs = new List<(string Key, string Value)>
        {
            ("client_id", credentials.ClientId),
            ("redirect_uri", CallbackUrl),
            ("response_type", "code"),
            ("scope", scope),
            ("state", state),
        };

        switch (provider)
        {
            case "google":
                pairs.Add(("nonce", nonce));
                pairs.Add(("prompt", "select_account"));
                pairs.Add(("access_type", "online"));
                break;
            case "microsoft":
                pairs.Add(("nonce", nonce));
                pairs.Add(("response_mode", "query"));
                pairs.Add(("prompt", "select_account"));
                break;
            case "apple":
                pairs.Add(("response_mode", "form_post"));
                break;
            case "github":
                pairs.Add(("allow_signup", "true"));
                break;
        }

        return authorize + "?" + string.Join('&', pairs.Select(p => PhpUrlEncode(p.Key) + "=" + PhpUrlEncode(p.Value)));
    }

    public static string PackState(
        string provider,
        OAuthSignInContext context,
        string authMode,
        string? returnUrl,
        bool termsAccepted,
        string signingSecret,
        string nonce,
        long unixTime)
    {
        var json = PhpJsonObject(
            ("pv", PhpJsonString(provider)),
            ("n", PhpJsonString(nonce)),
            ("tk", PhpJsonString(context.TenantKey)),
            ("k", PhpJsonString(context.Kind)),
            ("rh", PhpJsonString(context.ReturnHost)),
            ("rp", PhpJsonString(context.ReturnPath)),
            ("am", PhpJsonString(NormalizeMode(authMode))),
            ("lp", PhpJsonString(context.LangPrefix)),
            ("ru", PhpJsonString(returnUrl ?? string.Empty)),
            ("tm", termsAccepted ? "1" : "0"),
            ("t", unixTime.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var payload = Base64Url(Encoding.UTF8.GetBytes(json));
        var sig = HmacHex(payload, signingSecret ?? string.Empty);
        return payload + "." + sig;
    }

    public static string PhpUrlEncode(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        var sb = new StringBuilder(bytes.Length * 3);
        foreach (var b in bytes)
        {
            var c = (char)b;
            if (c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-' or '_' or '.')
            {
                sb.Append(c);
            }
            else if (c == ' ')
            {
                sb.Append('+');
            }
            else
            {
                sb.Append('%');
                sb.Append(b.ToString("X2"));
            }
        }

        return sb.ToString();
    }

    public static string HtmlEncode(string value)
    {
        return (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static string AuthorizeUrl(string provider, OAuthProviderCredentials credentials)
    {
        var url = provider switch
        {
            "microsoft" => "https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize",
            "facebook" => "https://www.facebook.com/v19.0/dialog/oauth",
            "github" => "https://github.com/login/oauth/authorize",
            "apple" => "https://appleid.apple.com/auth/authorize",
            _ => "https://accounts.google.com/o/oauth2/v2/auth",
        };

        if (provider == "microsoft")
        {
            var tenant = Extra(credentials.Extra, "tenant");
            if (string.IsNullOrWhiteSpace(tenant))
            {
                tenant = "common";
            }

            url = url.Replace("{tenant}", Uri.EscapeDataString(tenant), StringComparison.Ordinal);
        }

        return url;
    }

    private static string Extra(IReadOnlyDictionary<string, string> extra, string key)
        => extra.TryGetValue(key, out var value) ? value : string.Empty;

    private static string PhpJsonObject(params (string Key, string RawValue)[] fields)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        for (var i = 0; i < fields.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append(PhpJsonString(fields[i].Key));
            sb.Append(':');
            sb.Append(fields[i].RawValue);
        }

        sb.Append('}');
        return sb.ToString();
    }

    public static string PhpJsonString(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '/':
                    sb.Append("\\/");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (c < 0x20 || c > 0x7F)
                    {
                        sb.Append("\\u");
                        sb.Append(((int)c).ToString("x4"));
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

    private static string Base64Url(byte[] bytes)
    {
        var encoded = Convert.ToBase64String(bytes);
        return encoded.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string HmacHex(string payload, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(key, data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public sealed class OAuthProviderCredentials
{
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public IReadOnlyDictionary<string, string> Extra { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public static OAuthProviderCredentials Empty { get; } = new();
}

public sealed class OAuthSignInContext
{
    public bool Ok { get; init; }
    public string Error { get; init; } = "";
    public string TenantKey { get; init; } = "";
    public string Kind { get; init; } = "";
    public string ReturnHost { get; init; } = "";
    public string ReturnPath { get; init; } = "/";
    public string LangPrefix { get; init; } = "";

    public static OAuthSignInContext Failed(string message) => new() { Ok = false, Error = message };
}

public readonly record struct OAuthStartResponse(int StatusCode, string? Text, string? Location)
{
    public static OAuthStartResponse Plain(int statusCode, string text) => new(statusCode, text, null);

    public static OAuthStartResponse Redirect(string location) => new(StatusCodes.Status302Found, null, location);
}
