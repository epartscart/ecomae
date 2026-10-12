using EcomAE.Platform.Auth;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Services;

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
        if (StorefrontLangPrefix.TryStripAdminShell(path, out var strippedAdmin))
        {
            context.Request.Path = strippedAdmin;
            path = strippedAdmin;
        }

        if (!RequiresAdmin(path))
        {
            await _next(context);
            return;
        }

        if (IsControlPanelPath(path) && !await TenantHasControlPanelAsync(context).ConfigureAwait(false))
        {
            // ERP-only tenants have no Control Panel surface: /cp never renders there.
            context.Response.Headers["x-ecomae-surface-ownership"] = "erp-only-tenant";
            context.Response.Headers["Cache-Control"] = "no-store";
            context.Response.Redirect(EcomAeRoutes.Erp, permanent: false);
            return;
        }

        var session = await sessions.ValidateAsync(context, context.RequestAborted).ConfigureAwait(false);
        if (session.Kind == LegacySessionKind.Admin && !HasSurfaceCapability(path, session))
        {
            // A signed-in admin without the surface's capability (CP-only user on /erp, ERP-only user on /cp)
            // is refused here, before any module renders.
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.Headers["x-ecomae-surface-ownership"] = IsControlPanelPath(path) ? "cp-capability-required" : "erp-capability-required";
            context.Response.Headers["Cache-Control"] = "no-store";
            await context.Response.WriteAsync("This account has no access to this surface.").ConfigureAwait(false);
            return;
        }

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

        var login = LoginPathFor(path, context);
        var returnUrl = OriginalReturnUrl(context, path);
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

    public static bool IsErpPath(string path)
        => path.Equals("/erp", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/erp/", StringComparison.OrdinalIgnoreCase);

    /// <summary>CP paths need the <c>cp</c> capability, ERP paths the <c>erp</c> capability; other admin surfaces keep the any-capability rule.</summary>
    public static bool HasSurfaceCapability(string path, LegacySessionContext session)
    {
        if (IsControlPanelPath(path))
        {
            return session.Capabilities.Contains("cp", StringComparer.OrdinalIgnoreCase);
        }

        if (IsErpPath(path))
        {
            return session.Capabilities.Contains("erp", StringComparer.OrdinalIgnoreCase);
        }

        return true;
    }

    public static bool IsControlPanelPath(string path)
        => path.Equals("/cp", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/cp/", StringComparison.OrdinalIgnoreCase);

    internal static async Task<bool> TenantHasControlPanelAsync(HttpContext context)
    {
        if (context.Items[TenantResolutionMiddleware.HttpContextItemKey] is not TenantContext tenant)
        {
            return true;
        }

        if (tenant.Mode is TenantMode.ErpOnlyTenant)
        {
            return false;
        }

        if (tenant.Mode is TenantMode.DemoTenant
            && context.RequestServices.GetService(typeof(ITenantRegistry)) is ITenantRegistry registry)
        {
            var record = await registry.FindByHostAsync(tenant.Host, context.RequestAborted).ConfigureAwait(false);
            return record?.ControlPanelEnabled ?? true;
        }

        return true;
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

        if (StorefrontLangPrefix.TryStripAdminShell(value, out var stripped))
        {
            value = stripped;
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

        // PHP cp/index.php serves these before the CP login wall (epc_cp_pwa_maybe_serve_asset).
        if (IsPublicCpPwaAsset(value))
        {
            return false;
        }

        // The portal *_config.php JS loaders run outside cp/index.php, so PHP never puts them behind the CP login.
        if (Presentation.PhpAssetWrappers.IsWrapperPath(value))
        {
            return false;
        }

        return IsAdminSurface(value);
    }

    internal static bool IsAdminSurface(string path)
    {
        if (path.Equals("/cp", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/erp", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/bos", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/ip", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/attention", StringComparison.OrdinalIgnoreCase)
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
            || path.StartsWith("/attention/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/CP/", StringComparison.Ordinal)
            || path.StartsWith("/ERP/", StringComparison.Ordinal)
            || path.StartsWith("/BOS/", StringComparison.Ordinal)
            || path.StartsWith("/IP/", StringComparison.Ordinal);
    }

    /// <summary>PHP <c>epc_cp_pwa_maybe_serve_asset</c> map. Anonymous.</summary>
    internal static bool IsPublicCpPwaAsset(string path)
    {
        var bare = path.TrimEnd('/');
        return bare.Equals("/cp/manifest.webmanifest", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/cp/sw.js", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/cp/offline.html", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/cp/assets/app/icon-192.svg", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/cp/assets/app/icon-512.svg", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsAllowlistedLoginOrLogout(string path)
    {
        var bare = path.TrimEnd('/');
        return bare.Equals("/cp/login", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/cp/logout", StringComparison.OrdinalIgnoreCase)
            // PHP cron_crutch.php is a tech_key URL, not an admin session.
            || bare.Equals(EcomAeRoutes.CpPricesCronTick, StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/erp/login", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/erp/logout", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/bos/login", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/bos/logout", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/ip/login", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/ip/logout", StringComparison.OrdinalIgnoreCase)
            || bare.Equals(EcomAeRoutes.LegacyAdminLogin, StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/auth/logout", StringComparison.OrdinalIgnoreCase)
            || IsClientErpLoginPath(bare);
    }

    /// <summary>PHP <c>/cp/client-erp</c> picker and <c>/cp/client-erp/{site_key}/</c> company login are public.</summary>
    public static bool IsClientErpLoginPath(string path)
    {
        if (!ClientErpPath.TryParse(path, out var parsed))
        {
            return false;
        }

        return parsed.IsBare || parsed.IsLoginRoot;
    }

    internal static string LoginPathFor(string path, HttpContext? context = null)
    {
        if (context?.Items[ClientErpPath.HttpContextItemKey] is string bound
            && !string.IsNullOrWhiteSpace(bound))
        {
            return ClientErpPath.LoginUrl(bound);
        }

        if (ClientErpPath.TryParseSiteKey(path, out var fromPath))
        {
            return ClientErpPath.LoginUrl(fromPath);
        }

        if (path.StartsWith("/erp", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/ERP", StringComparison.Ordinal)
            || path.Equals("/attention", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/attention/", StringComparison.OrdinalIgnoreCase))
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

    private static string OriginalReturnUrl(HttpContext context, string path)
    {
        var original = context.Items[ClientErpPathRewriteMiddleware.OriginalPathItem] as string
            ?? context.Request.Path.Value
            ?? path;
        return original + (context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty);
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
        // Use path-segment suffix match so Blazor shells like /cp/dashboard-summary-app
        // still get the HTML login redirect (not a raw 401 JSON body).
        return IsJsonChallengePath(context.Request.Path.Value);
    }

    /// <summary>
    /// True for JSON digest/API paths. False for Blazor <c>*-app</c> presentation shells
    /// even when the route name contains a digest stem (e.g. dashboard-summary-app).
    /// </summary>
    public static bool IsJsonChallengePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var bare = path.TrimEnd('/');
        return bare.EndsWith("/dashboard-summary", StringComparison.OrdinalIgnoreCase)
            || bare.EndsWith("/parity", StringComparison.OrdinalIgnoreCase)
            || bare.Contains("/ajax", StringComparison.OrdinalIgnoreCase)
            || bare.Contains("dry-run", StringComparison.OrdinalIgnoreCase)
            || bare.Contains("/writes/", StringComparison.OrdinalIgnoreCase);
    }
}
