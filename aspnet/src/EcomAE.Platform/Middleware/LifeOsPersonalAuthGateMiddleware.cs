using EcomAE.Platform.Auth;
using EcomAE.Platform.Routing;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// Login wall for LifeOS operator / account-bound console surfaces.
/// Join is public for new users (creates a token-bound client).
/// Login is a separate path for existing ecomae accounts.
/// Companion/results stay token-gated in the app layer (clientId + joinToken).
/// </summary>
public sealed class LifeOsPersonalAuthGateMiddleware
{
    public const string ChallengeHeader = "X-EcomAE-LifeOs-Auth";
    public const string ChallengeValue = "required";

    private readonly RequestDelegate _next;

    public LifeOsPersonalAuthGateMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILegacySessionValidator sessions)
    {
        var path = context.Request.Path.Value ?? "/";
        if (!RequiresPersonalLogin(path))
        {
            await _next(context);
            return;
        }

        var session = await sessions.ValidateAsync(context, context.RequestAborted).ConfigureAwait(false);
        if (session.IsAuthenticated
            && (session.Kind == LegacySessionKind.Customer
                || session.Kind == LegacySessionKind.Admin))
        {
            context.Response.Headers[ChallengeHeader] = "ok";
            context.Items["LifeOsSession"] = session;
            await _next(context);
            return;
        }

        context.Response.Headers[ChallengeHeader] = ChallengeValue;
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";

        var returnUrl = path + (context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty);
        var login = EcomAeRoutes.LifeOsLogin + "?returnUrl=" + Uri.EscapeDataString(returnUrl);

        if (WantsJson(context))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(
                "{\"ok\":false,\"code\":\"lifeos_login_required\",\"message\":\"Sign in to open the LifeOS console or operator directory.\",\"login\":\""
                + EcomAeRoutes.LifeOsLogin + "\",\"join\":\"" + EcomAeRoutes.LifeOsJoin + "\"}");
            return;
        }

        context.Response.StatusCode = StatusCodes.Status302Found;
        context.Response.Headers.Location = login;
    }

    public static bool RequiresPersonalLogin(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var bare = path.TrimEnd('/');
        if (bare.Length == 0)
        {
            bare = "/";
        }

        if (IsPublicLifeOs(bare))
        {
            return false;
        }

        return IsPersonalLifeOs(bare);
    }

    internal static bool IsPublicLifeOs(string bare)
    {
        if (bare.Equals("/lifeos", StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsLogin, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsLogout, StringComparison.OrdinalIgnoreCase)
            // New users join without signing in first.
            || bare.Equals(EcomAeRoutes.LifeOsJoin, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsMobile, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsResults, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsResultsJson, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsCompanion, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsCompanionTrack, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsCompanionTalk, StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/lifeos/companion/digest", StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsSpec, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsSpecApp, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsSpecJson, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsCinematic, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsCinematicApp, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsRoutine, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsRoutineApp, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsRoutineJson, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsDemo, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsDemoApp, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsManifest, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(LifeOs.Clients.LifeOsPwaAssets.ManifestPath, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(LifeOs.Clients.LifeOsPwaAssets.ServiceWorkerPath, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(LifeOs.Clients.LifeOsPwaAssets.JoinScriptPath, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(LifeOs.Clients.LifeOsPwaAssets.CompanionScriptPath, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(LifeOs.Clients.LifeOsPwaAssets.ResultsScriptPath, StringComparison.OrdinalIgnoreCase)
            || bare.StartsWith("/lifeos/media/", StringComparison.OrdinalIgnoreCase)
            || bare.StartsWith("/lifeos/icons/", StringComparison.OrdinalIgnoreCase)
            || bare.StartsWith("/lifeos/cinematic/", StringComparison.OrdinalIgnoreCase)
            || bare.StartsWith("/lifeos/companion", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    internal static bool IsPersonalLifeOs(string bare)
    {
        // Account / operator surfaces — not new-user join.
        if (bare.Equals(EcomAeRoutes.LifeOsClientsBoard, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsClientsCp, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsDirectory, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsApp, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsBrain, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool WantsJson(HttpContext context)
    {
        var accept = context.Request.Headers.Accept.ToString();
        if (accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)
            && !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var path = context.Request.Path.Value ?? "";
        return path.EndsWith("/json", StringComparison.OrdinalIgnoreCase)
            || path.Equals(EcomAeRoutes.LifeOsDirectory, StringComparison.OrdinalIgnoreCase)
            || path.Equals(EcomAeRoutes.LifeOsClientsCp, StringComparison.OrdinalIgnoreCase);
    }
}
