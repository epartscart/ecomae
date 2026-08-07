using EcomAE.Platform.Auth;
using EcomAE.Platform.Routing;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// Hard login wall for confidential admin surfaces (/cp /erp /bos /ip).
/// Guest-browse of Control chrome on live tenants is a data-leak risk — PHP CP
/// redirects bare /cp → /cp/control and requires credentials; ASP.NET must match.
/// LifeOS customer marketing (<c>/lifeos</c>) stays public — only IP is gated here.
/// </summary>
public sealed class AdminSurfaceAuthGateMiddleware
{
    public const string ChallengeHeader = "X-EcomAE-Admin-Auth";
    public const string ChallengeValue = "required";

    private readonly RequestDelegate _next;

    public AdminSurfaceAuthGateMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILegacySessionValidator sessions)
    {
        var path = context.Request.Path.Value ?? "/";
        if (!RequiresAdmin(path))
        {
            await _next(context);
            return;
        }

        var session = await sessions.ValidateAsync(context, context.RequestAborted).ConfigureAwait(false);
        if (session.Kind == LegacySessionKind.Admin && session.HasBackendAccess)
        {
            context.Response.Headers[ChallengeHeader] = "ok";
            await _next(context);
            return;
        }

        // Validated admin with CP/ERP/BOS/IP capabilities (some fixtures omit HasBackendAccess).
        if (session.Kind == LegacySessionKind.Admin
            && (session.Capabilities.Contains("cp", StringComparer.OrdinalIgnoreCase)
                || session.Capabilities.Contains("erp", StringComparer.OrdinalIgnoreCase)
                || session.Capabilities.Contains("bos", StringComparer.OrdinalIgnoreCase)
                || session.Capabilities.Contains("ip", StringComparer.OrdinalIgnoreCase)))
        {
            context.Response.Headers[ChallengeHeader] = "ok";
            await _next(context);
            return;
        }

        // Admin cookie present but not validated → still challenge (do not trust cookie-only).
        context.Response.Headers[ChallengeHeader] = ChallengeValue;
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";

        var login = LoginPathFor(path);
        var returnUrl = path + (context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty);
        var target = login + "?returnUrl=" + Uri.EscapeDataString(returnUrl);

        if (WantsJson(context))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(
                "{\"ok\":false,\"code\":\"admin_auth_required\",\"message\":\"Admin login required for Control / ERP / BOS / IP.\",\"login\":\""
                + login + "\"}");
            return;
        }

        context.Response.StatusCode = StatusCodes.Status302Found;
        context.Response.Headers.Location = target;
    }

    public static bool RequiresAdmin(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var value = path.Trim();
        if (value.Length == 0)
        {
            return false;
        }

        // Never gate PHP reference or public auth bridges.
        if (value.StartsWith("/php-reference", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/auth/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/migration/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/storefront/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/marketing/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IsAllowlistedLoginOrLogout(value))
        {
            return false;
        }

        // Temporary: LifeOS joined-clients console is public until client IAM lands.
        // Prefer /lifeos/clients-board; /cp/lifeos-clients-app stays allowlisted for the same UI.
        if (IsLifeOsClientsBoardAllowlisted(value))
        {
            return false;
        }

        return IsAdminSurface(value);
    }

    internal static bool IsLifeOsClientsBoardAllowlisted(string path)
    {
        var bare = path.TrimEnd('/');
        return bare.Equals(EcomAeRoutes.ControlPanelLifeOsClientsApp, StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LifeOsClientsBoard, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsAdminSurface(string path)
    {
        if (path.Equals("/cp", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/erp", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/bos", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/ip", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/CP", StringComparison.Ordinal)
            || path.Equals("/ERP", StringComparison.Ordinal)
            || path.Equals("/BOS", StringComparison.Ordinal)
            || path.Equals("/IP", StringComparison.Ordinal))
        {
            return true;
        }

        return path.StartsWith("/cp/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/erp/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/bos/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/ip/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/CP/", StringComparison.Ordinal)
            || path.StartsWith("/ERP/", StringComparison.Ordinal)
            || path.StartsWith("/BOS/", StringComparison.Ordinal)
            || path.StartsWith("/IP/", StringComparison.Ordinal);
    }

    internal static bool IsAllowlistedLoginOrLogout(string path)
    {
        var bare = path.TrimEnd('/');
        return bare.Equals("/cp/login", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/cp/logout", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/erp/login", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/erp/logout", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/bos/login", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/bos/logout", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/ip/login", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/ip/logout", StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LegacyAdminLogin, StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/auth/logout", StringComparison.OrdinalIgnoreCase);
    }

    private static string LoginPathFor(string path)
    {
        if (path.StartsWith("/erp", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/ERP", StringComparison.Ordinal))
        {
            return EcomAeRoutes.ErpLogin;
        }

        if (path.StartsWith("/bos", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/BOS", StringComparison.Ordinal))
        {
            return EcomAeRoutes.BosLogin;
        }

        if (path.StartsWith("/ip", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/IP", StringComparison.Ordinal))
        {
            return EcomAeRoutes.IpLogin;
        }

        return EcomAeRoutes.ControlPanelLogin;
    }

    private static bool WantsJson(HttpContext context)
    {
        var accept = context.Request.Headers.Accept.ToString();
        if (accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)
            && !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Digests / API-ish GETs under /cp|/erp|/bos|/ip without Blazor navigation.
        var path = context.Request.Path.Value ?? "";
        return path.Contains("dashboard-summary", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/parity", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/ajax", StringComparison.OrdinalIgnoreCase)
            || path.Contains("dry-run", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/writes/", StringComparison.OrdinalIgnoreCase);
    }
}
