using System.Net.Http.Headers;
using System.Text.Json;

namespace EcomAE.Platform.Auth;

/// <summary>
/// PHP twin: <c>epc_oauth_exchange_code</c>. Turns an authorization code into a verified email profile.
/// </summary>
public static class OAuthCodeExchange
{
    public static async Task<OAuthExchangeResult> ExchangeAsync(
        HttpClient http,
        string provider,
        string code,
        OAuthProviderCredentials credentials,
        long unixNow,
        CancellationToken cancellationToken)
    {
        if (!OAuthStart.IsConfigured(provider, credentials))
        {
            return OAuthExchangeResult.Failed(OAuthCallback.UcFirst(provider) + " sign-in is not configured");
        }

        if (string.IsNullOrEmpty(code))
        {
            return OAuthExchangeResult.Failed("Missing authorization code");
        }

        var clientSecret = credentials.ClientSecret;
        var tokenUrl = TokenUrl(provider, credentials);
        if (provider == "apple" && string.IsNullOrWhiteSpace(clientSecret))
        {
            return OAuthExchangeResult.Failed("Apple sign-in key is incomplete (need Services ID, Team ID, Key ID and .p8 key)");
        }

        var form = new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = credentials.ClientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = OAuthStart.CallbackUrl,
            ["grant_type"] = "authorization_code",
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = new FormUrlEncodedContent(form),
        };
        if (provider == "github")
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        string body;
        int status;
        try
        {
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            status = (int)response.StatusCode;
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return OAuthExchangeResult.Failed("Token exchange failed with " + OAuthCallback.UcFirst(provider));
        }
        catch (TaskCanceledException)
        {
            return OAuthExchangeResult.Failed("Token exchange failed with " + OAuthCallback.UcFirst(provider));
        }

        if (status < 200 || status >= 300 || string.IsNullOrEmpty(body))
        {
            return OAuthExchangeResult.Failed("Token exchange failed with " + OAuthCallback.UcFirst(provider));
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return OAuthExchangeResult.Failed("Invalid token response from " + OAuthCallback.UcFirst(provider));
        }

        using (doc)
        {
            var root = doc.RootElement;
            var access = JsonString(root, "access_token");
            var idToken = JsonString(root, "id_token");
            if (access.Length == 0 && idToken.Length == 0)
            {
                return OAuthExchangeResult.Failed("Invalid token response from " + OAuthCallback.UcFirst(provider));
            }

            return provider switch
            {
                "google" => ProfileFromIdToken(idToken, credentials.ClientId, "Google", requireIssuer: null, unixNow),
                "microsoft" => await ProfileFromMicrosoftAsync(http, root, credentials, unixNow, cancellationToken).ConfigureAwait(false),
                "apple" => ProfileFromIdToken(idToken, credentials.ClientId, "Apple", "https://appleid.apple.com", unixNow),
                "facebook" => await ProfileFromFacebookAsync(http, access, cancellationToken).ConfigureAwait(false),
                "github" => await ProfileFromGithubAsync(http, access, cancellationToken).ConfigureAwait(false),
                _ => OAuthExchangeResult.Failed("Unsupported provider"),
            };
        }
    }

    private static string TokenUrl(string provider, OAuthProviderCredentials credentials)
    {
        var url = provider switch
        {
            "microsoft" => "https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token",
            "facebook" => "https://graph.facebook.com/v19.0/oauth/access_token",
            "github" => "https://github.com/login/oauth/access_token",
            "apple" => "https://appleid.apple.com/auth/token",
            _ => "https://oauth2.googleapis.com/token",
        };
        if (provider != "microsoft")
        {
            return url;
        }

        var tenant = credentials.Extra.TryGetValue("tenant", out var value) ? value.Trim() : "";
        if (tenant.Length == 0)
        {
            tenant = "common";
        }

        return url.Replace("{tenant}", Uri.EscapeDataString(tenant), StringComparison.Ordinal);
    }

    private static OAuthExchangeResult ProfileFromIdToken(string idToken, string clientId, string label, string? requireIssuer, long unixNow)
    {
        if (idToken.Length == 0)
        {
            return OAuthExchangeResult.Failed(label + " did not return an id_token");
        }

        var claims = JwtPayload(idToken);
        if (claims is null)
        {
            return OAuthExchangeResult.Failed("Invalid " + label + " id_token");
        }

        using (claims)
        {
            var root = claims.RootElement;
            var aud = JsonString(root, "aud");
            if (aud.Length > 0 && !string.Equals(aud, clientId, StringComparison.Ordinal))
            {
                return OAuthExchangeResult.Failed(label + " id_token audience mismatch");
            }

            if (root.TryGetProperty("exp", out var expEl) && expEl.TryGetInt64(out var exp) && exp < unixNow)
            {
                return OAuthExchangeResult.Failed(label + " id_token expired");
            }

            if (requireIssuer is not null && !string.Equals(JsonString(root, "iss"), requireIssuer, StringComparison.Ordinal))
            {
                return OAuthExchangeResult.Failed(label + " id_token issuer invalid");
            }

            var email = JsonString(root, "email").Trim().ToLowerInvariant();
            if (email.Length == 0 || !email.Contains('@', StringComparison.Ordinal))
            {
                return label == "Apple"
                    ? OAuthExchangeResult.Failed("Apple did not share an email (enable email scope / private relay)")
                    : OAuthExchangeResult.Failed(label + " did not return an id_token");
            }

            if (label == "Google")
            {
                var verified = root.TryGetProperty("email_verified", out var flag) && IsTruthy(flag);
                if (!verified)
                {
                    return OAuthExchangeResult.Failed("Your Google email is not verified");
                }
            }

            var name = JsonString(root, "name");
            return OAuthExchangeResult.Verified(email, name, JsonString(root, "sub"));
        }
    }

    private static async Task<OAuthExchangeResult> ProfileFromMicrosoftAsync(
        HttpClient http,
        JsonElement token,
        OAuthProviderCredentials credentials,
        long unixNow,
        CancellationToken cancellationToken)
    {
        var idToken = JsonString(token, "id_token");
        var email = "";
        var name = "";
        var sub = "";
        if (idToken.Length > 0)
        {
            var claims = JwtPayload(idToken);
            if (claims is not null)
            {
                using (claims)
                {
                    var root = claims.RootElement;
                    var aud = JsonString(root, "aud");
                    if (aud.Length > 0 && !string.Equals(aud, credentials.ClientId, StringComparison.Ordinal))
                    {
                        return OAuthExchangeResult.Failed("Microsoft id_token audience mismatch");
                    }

                    if (root.TryGetProperty("exp", out var expEl) && expEl.TryGetInt64(out var exp) && exp < unixNow)
                    {
                        return OAuthExchangeResult.Failed("Microsoft id_token expired");
                    }

                    email = FirstEmail(JsonString(root, "email"), JsonString(root, "preferred_username"));
                    name = JsonString(root, "name").Trim();
                    sub = FirstNonEmpty(JsonString(root, "sub"), JsonString(root, "oid"));
                }
            }
        }

        var access = JsonString(token, "access_token");
        if (email.Length == 0 && access.Length > 0)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            try
            {
                using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using var me = JsonDocument.Parse(body);
                email = FirstEmail(JsonString(me.RootElement, "mail"), JsonString(me.RootElement, "userPrincipalName"));
                if (name.Length == 0)
                {
                    name = JsonString(me.RootElement, "displayName").Trim();
                }

                if (sub.Length == 0)
                {
                    sub = JsonString(me.RootElement, "id");
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                email = "";
            }
        }

        if (email.Length == 0 || !email.Contains('@', StringComparison.Ordinal))
        {
            return OAuthExchangeResult.Failed("Microsoft account has no usable email address");
        }

        return OAuthExchangeResult.Verified(email, name, sub);
    }

    private static async Task<OAuthExchangeResult> ProfileFromFacebookAsync(HttpClient http, string accessToken, CancellationToken cancellationToken)
    {
        if (accessToken.Length == 0)
        {
            return OAuthExchangeResult.Failed("Facebook did not return an access token");
        }

        var url = "https://graph.facebook.com/v19.0/me?fields=id,name,email&access_token=" + Uri.EscapeDataString(accessToken);
        try
        {
            using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            var email = JsonString(doc.RootElement, "email").Trim().ToLowerInvariant();
            if (email.Length == 0 || !email.Contains('@', StringComparison.Ordinal))
            {
                return OAuthExchangeResult.Failed("Facebook account has no shared email — use another method");
            }

            return OAuthExchangeResult.Verified(email, JsonString(doc.RootElement, "name"), JsonString(doc.RootElement, "id"));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return OAuthExchangeResult.Failed("Could not read Facebook profile");
        }
    }

    private static async Task<OAuthExchangeResult> ProfileFromGithubAsync(HttpClient http, string accessToken, CancellationToken cancellationToken)
    {
        if (accessToken.Length == 0)
        {
            return OAuthExchangeResult.Failed("GitHub did not return an access token");
        }

        try
        {
            using var userRequest = Authorized(HttpMethod.Get, "https://api.github.com/user", accessToken);
            using var userResponse = await http.SendAsync(userRequest, cancellationToken).ConfigureAwait(false);
            using var userDoc = JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            var sub = JsonString(userDoc.RootElement, "id");
            var name = FirstNonEmpty(JsonString(userDoc.RootElement, "name"), JsonString(userDoc.RootElement, "login"));
            var email = JsonString(userDoc.RootElement, "email").Trim().ToLowerInvariant();

            using var mailRequest = Authorized(HttpMethod.Get, "https://api.github.com/user/emails", accessToken);
            using var mailResponse = await http.SendAsync(mailRequest, cancellationToken).ConfigureAwait(false);
            using var mailDoc = JsonDocument.Parse(await mailResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            if (mailDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var primary = "";
                var any = "";
                foreach (var row in mailDoc.RootElement.EnumerateArray())
                {
                    var addr = JsonString(row, "email").Trim().ToLowerInvariant();
                    var verified = row.TryGetProperty("verified", out var flag) && IsTruthy(flag);
                    if (addr.Length == 0 || !verified)
                    {
                        continue;
                    }

                    if (any.Length == 0)
                    {
                        any = addr;
                    }

                    if (row.TryGetProperty("primary", out var primaryFlag) && IsTruthy(primaryFlag))
                    {
                        primary = addr;
                    }
                }

                if (primary.Length == 0 && any.Length == 0)
                {
                    return OAuthExchangeResult.Failed("No verified email on your GitHub account");
                }

                email = primary.Length > 0 ? primary : any;
            }

            if (email.Length == 0 || !email.Contains('@', StringComparison.Ordinal))
            {
                return OAuthExchangeResult.Failed("GitHub account has no verified email");
            }

            return OAuthExchangeResult.Verified(email, name, sub);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return OAuthExchangeResult.Failed("Could not read GitHub profile");
        }
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd("epartscart-oauth");
        return request;
    }

    private static JsonDocument? JwtPayload(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(OAuthStart.Base64UrlDecode(parts[1]));
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }

    private static string JsonString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            _ => string.Empty,
        };
    }

    private static bool IsTruthy(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => value.TryGetInt32(out var n) && n != 0,
            JsonValueKind.String => value.GetString() is "1" or "true",
            _ => false,
        };

    private static string FirstEmail(string preferred, string fallback)
    {
        var email = preferred.Trim().ToLowerInvariant();
        if (email.Contains('@', StringComparison.Ordinal))
        {
            return email;
        }

        email = fallback.Trim().ToLowerInvariant();
        return email.Contains('@', StringComparison.Ordinal) ? email : string.Empty;
    }

    private static string FirstNonEmpty(string preferred, string fallback)
        => string.IsNullOrWhiteSpace(preferred) ? fallback.Trim() : preferred.Trim();
}
