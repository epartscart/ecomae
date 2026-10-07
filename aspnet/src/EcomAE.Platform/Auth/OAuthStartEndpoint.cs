using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Data;
using EcomAE.Platform.Presentation;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Auth;

/// <summary>
/// GET <c>/api/epc_oauth_start.php</c> — the URL the login buttons already emit.
/// Session minting stays on the PHP callback; this only builds the provider redirect.
/// </summary>
public static class OAuthStartEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/epc_oauth_start.php", HandleAsync);
    }

    internal static async Task<IResult> HandleAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        IOptions<EcomAeOptions> options,
        IOptions<PhpReferenceOptions> php,
        IWebHostEnvironment env,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = context.Request.Query;
            var provider = OAuthStart.NormalizeProvider(query["provider"]);
            if (!OAuthStart.IsKnownProvider(provider))
            {
                return Write(OAuthStartResponse.Plain(StatusCodes.Status400BadRequest, "Unknown sign-in provider."));
            }

            var credentials = await LoadCredentialsAsync(
                provider,
                connections,
                php.Value,
                env,
                cancellationToken).ConfigureAwait(false);
            if (!OAuthStart.IsConfigured(provider, credentials))
            {
                return Write(OAuthStart.Decide(
                    provider,
                    query["context"],
                    query["return_url"],
                    OAuthStart.TermsAccepted(query["terms"]),
                    credentials,
                    OAuthSignInContext.Failed("unused"),
                    options.Value.SecretSuccession,
                    "00",
                    0));
            }

            var signIn = await ResolveContextAsync(
                context,
                connections,
                OAuthStart.NormalizeMode(query["context"]),
                query["tenant_key"].ToString(),
                cancellationToken).ConfigureAwait(false);
            var nonce = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            var decision = OAuthStart.Decide(
                provider,
                query["context"],
                query["return_url"],
                OAuthStart.TermsAccepted(query["terms"]),
                credentials,
                signIn,
                options.Value.SecretSuccession ?? string.Empty,
                nonce,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            return Write(decision);
        }
        catch (Exception)
        {
            return Write(OAuthStartResponse.Plain(
                StatusCodes.Status400BadRequest,
                "Sign-in context error: Database unavailable"));
        }
    }

    internal static async Task<OAuthProviderCredentials> LoadCredentialsAsync(
        string provider,
        ITenantDbConnectionFactory connections,
        PhpReferenceOptions php,
        IWebHostEnvironment env,
        CancellationToken cancellationToken)
    {
        var file = ReadFileCredentials(provider, php, env);
        OAuthProviderCredentials db;
        try
        {
            db = await ReadDbCredentialsAsync(provider, connections, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            db = OAuthProviderCredentials.Empty;
        }

        if (db == OAuthProviderCredentials.Empty && string.IsNullOrEmpty(file.ClientId) && string.IsNullOrEmpty(file.ClientSecret))
        {
            return file;
        }

        var extra = new Dictionary<string, string>(file.Extra, StringComparer.Ordinal);
        foreach (var pair in db.Extra)
        {
            extra[pair.Key] = pair.Value;
        }

        return new OAuthProviderCredentials
        {
            ClientId = FirstNonEmpty(db.ClientId, file.ClientId),
            ClientSecret = FirstNonEmpty(db.ClientSecret, file.ClientSecret),
            Enabled = db == OAuthProviderCredentials.Empty ? file.Enabled : db.Enabled,
            Extra = extra,
        };
    }

    internal static OAuthProviderCredentials ReadFileCredentials(string provider, PhpReferenceOptions php, IWebHostEnvironment env)
    {
        foreach (var root in CredentialRoots(php, env))
        {
            var path = Path.Combine(root, "config.epc-oauth.php");
            if (!File.Exists(path))
            {
                continue;
            }

            var parsed = ParsePhpOauthFile(File.ReadAllText(path), provider);
            if (parsed is not null)
            {
                return parsed;
            }
        }

        return OAuthProviderCredentials.Empty;
    }

    public static OAuthProviderCredentials? ParsePhpOauthFile(string php, string provider)
    {
        if (string.IsNullOrWhiteSpace(php) || string.IsNullOrWhiteSpace(provider))
        {
            return null;
        }

        var match = Regex.Match(
            php,
            "'" + Regex.Escape(provider) + @"'\s*=>\s*array\s*\((?<body>.*?)\)\s*,",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
        {
            match = Regex.Match(
                php,
                "'" + Regex.Escape(provider) + @"'\s*=>\s*array\s*\((?<body>.*)\)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        if (!match.Success)
        {
            return OAuthProviderCredentials.Empty;
        }

        var body = match.Groups["body"].Value;
        var extra = new Dictionary<string, string>(StringComparer.Ordinal);
        string clientId = "";
        string clientSecret = "";
        foreach (Match field in Regex.Matches(body, @"'(?<key>[A-Za-z0-9_]+)'\s*=>\s*'(?<value>(?:\\'|[^'])*)'"))
        {
            var key = field.Groups["key"].Value;
            var value = field.Groups["value"].Value.Replace("\\'", "'", StringComparison.Ordinal);
            if (key == "client_id")
            {
                clientId = value.Trim();
            }
            else if (key == "client_secret")
            {
                clientSecret = value.Trim();
            }
            else
            {
                extra[key] = value;
            }
        }

        return new OAuthProviderCredentials
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            Enabled = true,
            Extra = extra,
        };
    }

    internal static async Task<OAuthSignInContext> ResolveContextAsync(
        HttpContext http,
        ITenantDbConnectionFactory connections,
        string mode,
        string? tenantKeyRaw,
        CancellationToken cancellationToken)
    {
        var tenantKey = SanitizeKey(tenantKeyRaw);
        if (!connections.IsConfigured)
        {
            return OAuthSignInContext.Failed("Database unavailable");
        }

        try
        {
            await using var connection = await connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (DbException)
        {
            return OAuthSignInContext.Failed("Database unavailable");
        }
        catch (InvalidOperationException)
        {
            return OAuthSignInContext.Failed("Database unavailable");
        }

        var host = (http.Request.Host.Host ?? string.Empty).Trim().ToLowerInvariant();
        if (mode == "storefront")
        {
            return new OAuthSignInContext
            {
                Ok = true,
                TenantKey = tenantKey,
                Kind = "storefront_local",
                ReturnHost = host,
                ReturnPath = "/en/",
                LangPrefix = "/en",
            };
        }

        return new OAuthSignInContext
        {
            Ok = true,
            TenantKey = tenantKey,
            Kind = "tenant_local",
            ReturnHost = host,
            ReturnPath = "/cp/control",
            LangPrefix = "",
        };
    }

    private static async Task<OAuthProviderCredentials> ReadDbCredentialsAsync(
        string provider,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        if (!connections.IsConfigured)
        {
            return OAuthProviderCredentials.Empty;
        }

        await using var connection = await connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT client_id, client_secret, extra_json, enabled FROM epc_oauth_config WHERE provider = @provider LIMIT 1";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@provider";
        parameter.Value = provider;
        command.Parameters.Add(parameter);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return OAuthProviderCredentials.Empty;
        }

        var extra = ParseExtra(reader.IsDBNull(2) ? "" : reader.GetString(2));
        var enabled = !reader.IsDBNull(3) && Convert.ToInt32(reader.GetValue(3)) == 1;
        return new OAuthProviderCredentials
        {
            ClientId = reader.IsDBNull(0) ? "" : reader.GetString(0).Trim(),
            ClientSecret = reader.IsDBNull(1) ? "" : reader.GetString(1).Trim(),
            Enabled = enabled,
            Extra = extra,
        };
    }

    private static IReadOnlyDictionary<string, string> ParseExtra(string json)
    {
        var extra = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
        {
            return extra;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return extra;
            }

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                extra[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? ""
                    : property.Value.ToString();
            }
        }
        catch (JsonException)
        {
            return extra;
        }

        return extra;
    }

    private static IEnumerable<string> CredentialRoots(PhpReferenceOptions php, IWebHostEnvironment env)
    {
        if (!string.IsNullOrWhiteSpace(php.PhpDocRoot))
        {
            yield return php.PhpDocRoot.Trim();
        }

        var fromEnv = Environment.GetEnvironmentVariable("ECOMAE_PHP_DOCROOT");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            yield return fromEnv.Trim();
        }

        yield return PhpLegacyAssetBridge.FindRepoRoot(env);
    }

    private static string SanitizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value.Trim().ToLowerInvariant().Where(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_').ToArray();
        return new string(chars);
    }

    private static string FirstNonEmpty(string preferred, string fallback)
        => string.IsNullOrEmpty(preferred) ? fallback : preferred;

    private static IResult Write(OAuthStartResponse response) => new OAuthStartResult(response);

    private sealed class OAuthStartResult : IResult
    {
        private readonly OAuthStartResponse _response;

        public OAuthStartResult(OAuthStartResponse response) => _response = response;

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.CacheControl = "no-store";
            if (!string.IsNullOrEmpty(_response.Location))
            {
                httpContext.Response.StatusCode = StatusCodes.Status302Found;
                httpContext.Response.Headers.Location = _response.Location;
                return;
            }

            httpContext.Response.StatusCode = _response.StatusCode;
            httpContext.Response.ContentType = "text/plain; charset=utf-8";
            await httpContext.Response.WriteAsync(_response.Text ?? string.Empty).ConfigureAwait(false);
        }
    }
}
