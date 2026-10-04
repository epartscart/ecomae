using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Data;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Auth;

/// <summary>
/// GET and POST <c>/api/epc_oauth_callback.php</c> plus <c>/epc-auth-handoff.php</c>.
/// A provider return with credentials finishes login (session row + cookie or cross-host handoff).
/// Without credentials the route still exists and rejects a bad or missing state the way PHP does.
/// </summary>
public static class OAuthCallbackEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods("/api/epc_oauth_callback.php", ["GET", "POST"], HandleAsync);
        endpoints.MapGet("/epc-auth-handoff.php", HandoffAsync);
    }

    internal static async Task<IResult> HandleAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        IOptions<EcomAeOptions> options,
        IOptions<PhpReferenceOptions> php,
        IWebHostEnvironment env,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var bag = await ReadBagAsync(context, cancellationToken).ConfigureAwait(false);
            var secret = options.Value.SecretSuccession ?? string.Empty;
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var unpacked = OAuthStart.UnpackState(bag("state"), secret, now);
            var configured = false;
            if (unpacked is not null)
            {
                var credentialsForState = await OAuthStartEndpoint.LoadCredentialsAsync(
                    unpacked.Provider, connections, php.Value, env, cancellationToken).ConfigureAwait(false);
                configured = OAuthStart.IsConfigured(unpacked.Provider, credentialsForState);
            }

            var decision = OAuthCallback.Decide(
                bag("error"), bag("error_description"), bag("code"), bag("state"), secret, now, configured);
            if (!decision.Continue || decision.State is null)
            {
                return Html(decision);
            }

            var credentials = await OAuthStartEndpoint.LoadCredentialsAsync(
                decision.State.Provider, connections, php.Value, env, cancellationToken).ConfigureAwait(false);
            unpacked = decision.State;
            var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(20);
            var exchange = await OAuthCodeExchange.ExchangeAsync(http, unpacked.Provider, bag("code") ?? string.Empty, credentials, now, cancellationToken)
                .ConfigureAwait(false);
            if (!exchange.Ok)
            {
                return Html(OAuthCallback.Fail(StatusCodes.Status400BadRequest, string.IsNullOrWhiteSpace(exchange.Message) ? "Token exchange failed." : exchange.Message));
            }

            if (string.IsNullOrWhiteSpace(exchange.Email) || !exchange.Email.Contains('@', StringComparison.Ordinal))
            {
                return Html(OAuthCallback.Fail(StatusCodes.Status403Forbidden, "No usable email returned by " + OAuthCallback.UcFirst(unpacked.Provider)));
            }

            if (!exchange.EmailVerified)
            {
                return Html(OAuthCallback.Fail(StatusCodes.Status403Forbidden, "Your " + OAuthCallback.UcFirst(unpacked.Provider) + " email is not verified"));
            }

            var finished = await FinishLoginAsync(context, connections, unpacked, exchange, secret, now, cancellationToken).ConfigureAwait(false);
            return finished;
        }
        catch (Exception)
        {
            return Html(OAuthCallback.Fail(StatusCodes.Status400BadRequest, "Token exchange failed."));
        }
    }

    internal static async Task<IResult> HandoffAsync(
        HttpContext context,
        IOptions<EcomAeOptions> options,
        CancellationToken cancellationToken)
    {
        var payload = OAuthCallback.UnpackHandoff(
            context.Request.Query["p"],
            context.Request.Query["s"],
            options.Value.SecretSuccession,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        if (payload is null)
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Text("Invalid or expired sign-in link. Please try again.", "text/plain", statusCode: StatusCodes.Status403Forbidden);
        }

        LegacyLoginCookieWriter.Apply(
            context.Response,
            new LegacyLoginSuccess(payload.UserId, "", payload.SessionToken, "", payload.Mode != "storefront", payload.Path),
            rememberMe: false);
        var location = payload.Path;
        if (!string.IsNullOrWhiteSpace(payload.Host))
        {
            location = "https://" + OAuthCallback.NormalizeHost(payload.Host) + payload.Path;
        }

        context.Response.Headers.CacheControl = "no-store";
        return Results.Redirect(location);
    }

    private static async Task<IResult> FinishLoginAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        OAuthUnpackedState state,
        OAuthExchangeResult profile,
        string secret,
        long now,
        CancellationToken cancellationToken)
    {
        if (!connections.IsConfigured)
        {
            return Html(OAuthCallback.Fail(StatusCodes.Status403Forbidden, "Could not create session"));
        }

        TenantDbTarget? target;
        try
        {
            target = await ResolveShopAsync(connections, state, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return Html(OAuthCallback.Fail(StatusCodes.Status403Forbidden, "Tenant context lost"));
        }

        if (target is null)
        {
            return Html(OAuthCallback.Fail(StatusCodes.Status403Forbidden, "Tenant context lost"));
        }

        int userId;
        try
        {
            await using var connection = await connections.OpenAsync(target.Database, target.User, target.Password, cancellationToken).ConfigureAwait(false);
            userId = await FindUserIdAsync(connection, profile.Email, cancellationToken).ConfigureAwait(false);
            if (userId <= 0)
            {
                var message = state.AuthMode == "storefront"
                    ? "Could not sign in with this " + OAuthCallback.UcFirst(state.Provider) + " account"
                    : "No CP access for this " + OAuthCallback.UcFirst(state.Provider) + " account on this workspace";
                return Html(OAuthCallback.Fail(StatusCodes.Status403Forbidden, message));
            }

            var remoteIp = LegacySessionTokenFactory.ResolveClientIp(context.Request);
            var userAgent = context.Request.Headers.UserAgent.ToString();
            var session = state.AuthMode == "storefront"
                ? LegacySessionTokenFactory.CustomerSessionToken(profile.Email, userId, now, secret)
                : LegacySessionTokenFactory.AdminSessionToken(profile.Email, now, secret);
            var csrf = LegacySessionTokenFactory.CsrfGuardKey(secret, session, remoteIp, userAgent);
            await InsertSessionAsync(connection, state.AuthMode, session, userId, now, csrf, cancellationToken).ConfigureAwait(false);
            if (OAuthCallback.NeedsHandoff(context.Request.Host.Host, state.ReturnHost))
            {
                var query = OAuthCallback.PackHandoff(userId, session, state, secret, now + 120);
                var location = "https://" + OAuthCallback.NormalizeHost(state.ReturnHost) + "/epc-auth-handoff.php?" + query;
                if (state.Provider == "google")
                {
                    RememberGoogle(context, profile.Email);
                }

                return Redirect(location);
            }

            LegacyLoginCookieWriter.Apply(
                context.Response,
                new LegacyLoginSuccess(userId, profile.Email, session, csrf, state.AuthMode != "storefront", OAuthCallback.SafeReturnPath(state)),
                rememberMe: false);
            if (state.Provider == "google")
            {
                RememberGoogle(context, profile.Email);
            }

            return Redirect(OAuthCallback.SafeReturnPath(state));
        }
        catch (DbException)
        {
            return Html(OAuthCallback.Fail(StatusCodes.Status403Forbidden, "Could not create session"));
        }
    }

    private static async Task<TenantDbTarget?> ResolveShopAsync(
        ITenantDbConnectionFactory connections,
        OAuthUnpackedState state,
        CancellationToken cancellationToken)
    {
        var host = OAuthCallback.NormalizeHost(state.ReturnHost);
        if (host.Length == 0)
        {
            return null;
        }

        var alias = host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : "www." + host;
        await using var registry = await connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
        await using var command = registry.CreateCommand();
        command.CommandText = PortalTenantSql.SelectActiveTenantByHosts;
        Add(command, "@h0", host);
        Add(command, "@h1", alias);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var database = reader.IsDBNull(2) ? "" : reader.GetString(2).Trim();
            if (database.Length == 0)
            {
                return null;
            }

            return new TenantDbTarget(
                database,
                reader.IsDBNull(3) ? "" : reader.GetString(3),
                reader.IsDBNull(4) ? "" : reader.GetString(4));
        }
        catch (DbException)
        {
            command.Parameters.Clear();
            command.CommandText = PortalTenantSql.SelectActiveTenantByHostsMinimal;
            Add(command, "@h0", host);
            Add(command, "@h1", alias);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var database = reader.IsDBNull(2) ? "" : reader.GetString(2).Trim();
            if (database.Length == 0)
            {
                return null;
            }

            return new TenantDbTarget(
                database,
                reader.IsDBNull(3) ? "" : reader.GetString(3),
                reader.IsDBNull(4) ? "" : reader.GetString(4));
        }
    }

    private static async Task<int> FindUserIdAsync(DbConnection connection, string email, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = LegacyAdminLoginSql.SelectUserByEmail;
        Add(command, "@contact", email);
        var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (scalar is null || scalar is DBNull)
        {
            return 0;
        }

        return Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
    }

    private static async Task InsertSessionAsync(
        DbConnection connection,
        string mode,
        string session,
        int userId,
        long now,
        string csrf,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        var admin = mode != "storefront";
        command.CommandText = admin ? LegacyAdminLoginSql.InsertAdminSession : LegacyAdminLoginSql.InsertCustomerSession;
        Add(command, "@session", session);
        Add(command, "@userId", userId);
        Add(command, "@time", now);
        Add(command, "@csrf", csrf);
        if (admin)
        {
            Add(command, "@contactType", "email");
        }

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void RememberGoogle(HttpContext context, string email)
    {
        context.Response.Cookies.Append("epc_oauth_last_google_email", email, new CookieOptions
        {
            Path = "/",
            HttpOnly = false,
            Secure = false,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(60),
        });
    }

    private static async Task<Func<string, string?>> ReadBagAsync(HttpContext context, CancellationToken cancellationToken)
    {
        if (HttpMethods.IsPost(context.Request.Method) && context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            return key => form[key].ToString();
        }

        return key => context.Request.Query[key].ToString();
    }

    private static void Add(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static IResult Html(OAuthCallbackDecision decision)
        => new OAuthCallbackResult(decision.StatusCode, decision.Html, null);

    private static IResult Redirect(string location)
        => new OAuthCallbackResult(StatusCodes.Status302Found, string.Empty, location);

    private sealed record TenantDbTarget(string Database, string User, string Password);

    private sealed class OAuthCallbackResult : IResult
    {
        private readonly int _status;
        private readonly string _html;
        private readonly string? _location;

        public OAuthCallbackResult(int status, string html, string? location)
        {
            _status = status;
            _html = html;
            _location = location;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.CacheControl = "no-store";
            if (!string.IsNullOrEmpty(_location))
            {
                httpContext.Response.StatusCode = StatusCodes.Status302Found;
                httpContext.Response.Headers.Location = _location;
                return;
            }

            httpContext.Response.StatusCode = _status;
            httpContext.Response.ContentType = "text/html; charset=utf-8";
            await httpContext.Response.WriteAsync(_html).ConfigureAwait(false);
        }
    }
}
