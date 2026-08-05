using System.Text.Json;
using EcomAE.Platform.Routing;
using Microsoft.Extensions.Logging;

namespace EcomAE.Platform.Auth;

/// <summary>
/// Handles credential POSTs for ASP.NET login pages and /auth/login/admin
/// before Blazor antiforgery can return HTTP 400 / before uncaught DB errors
/// become empty HTTP 500s through nginx.
/// </summary>
public sealed class LegacyLoginBridgeMiddleware
{
    private static readonly PathString[] LoginPaths =
    [
        new(EcomAeRoutes.LegacyAdminLogin),
        new(EcomAeRoutes.ControlPanelLogin),
        new(EcomAeRoutes.ErpLogin),
        new(EcomAeRoutes.BosLogin),
        new(EcomAeRoutes.StorefrontLogin),
    ];

    private readonly RequestDelegate _next;
    private readonly ILogger<LegacyLoginBridgeMiddleware> _log;

    public LegacyLoginBridgeMiddleware(RequestDelegate next, ILogger<LegacyLoginBridgeMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext context, ILegacyAdminLoginService login)
    {
        if (!HttpMethods.IsPost(context.Request.Method) || !IsLoginPath(context.Request.Path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var wantsHtml = context.Request.HasFormContentType
            || (context.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase)
                && !context.Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase));

        string contact = "", password = "", contactType = "email", surface = SurfaceFromPath(context.Request.Path), redirect = "";
        var remember = false;

        try
        {
            if (context.Request.HasFormContentType)
            {
                context.Request.EnableBuffering();
                var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
                // Leave Blazor/enhanced-nav POSTs alone (no credential fields).
                if (!form.ContainsKey("contact") || !form.ContainsKey("password"))
                {
                    context.Request.Body.Position = 0;
                    await _next(context).ConfigureAwait(false);
                    return;
                }

                contact = form["contact"].ToString();
                password = form["password"].ToString();
                contactType = string.IsNullOrWhiteSpace(form["contact_type"]) ? "email" : form["contact_type"].ToString();
                if (!string.IsNullOrWhiteSpace(form["surface"]))
                {
                    surface = form["surface"].ToString();
                }

                redirect = form["redirect"].ToString();
                remember = form["remember_me"].Count > 0;
                wantsHtml = true;
            }
            else if ((context.Request.ContentType ?? "").Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                context.Request.EnableBuffering();
                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                var body = await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false);
                context.Request.Body.Position = 0;
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
                var root = doc.RootElement;
                if (!root.TryGetProperty("contact", out _) || !root.TryGetProperty("password", out _))
                {
                    await _next(context).ConfigureAwait(false);
                    return;
                }

                contact = root.TryGetProperty("contact", out var c) ? c.GetString() ?? "" : "";
                password = root.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "";
                contactType = root.TryGetProperty("contact_type", out var t) ? t.GetString() ?? "email" : "email";
                if (root.TryGetProperty("surface", out var s) && s.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(s.GetString()))
                {
                    surface = s.GetString() ?? surface;
                }

                redirect = root.TryGetProperty("redirect", out var rd) ? rd.GetString() ?? "" : "";
                remember = root.TryGetProperty("remember_me", out var r) && r.ValueKind == JsonValueKind.True;
            }
            else
            {
                await _next(context).ConfigureAwait(false);
                return;
            }

            if (!login.IsConfigured)
            {
                await WriteFailureAsync(context, wantsHtml, surface, "bridge_not_configured",
                    "ASP.NET login bridge is not configured. Sync PHP secret_succession into platform.env.", 503).ConfigureAwait(false);
                return;
            }

            var loginSurface = LegacyLoginSurfaceParser.Parse(surface);
            var outcome = await login.LoginAsync(
                new LegacyLoginRequest(contact, password, contactType, remember, loginSurface),
                LegacySessionTokenFactory.ResolveClientIp(context.Request),
                context.Request.Headers.UserAgent.ToString(),
                context.RequestAborted).ConfigureAwait(false);

            if (!outcome.Ok || outcome.Success is null)
            {
                await WriteFailureAsync(
                    context,
                    wantsHtml,
                    surface,
                    outcome.Failure?.Code ?? "invalid_credentials",
                    outcome.Failure?.Message ?? "Incorrect login or password.",
                    401).ConfigureAwait(false);
                return;
            }

            LegacyLoginCookieWriter.Apply(context.Response, outcome.Success, remember);
            var dest = string.IsNullOrWhiteSpace(redirect) ? outcome.Success.RedirectPath : redirect;
            if (!dest.StartsWith('/') || dest.StartsWith("//", StringComparison.Ordinal))
            {
                dest = outcome.Success.RedirectPath;
            }

            if (wantsHtml)
            {
                context.Response.StatusCode = StatusCodes.Status302Found;
                context.Response.Headers.Location = dest;
                return;
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(new
            {
                ok = true,
                user_id = outcome.Success.UserId,
                email = outcome.Success.Email,
                admin_session = outcome.Success.AdminSession,
                redirect = dest
            }, context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Login bridge middleware failed for {Path}", context.Request.Path);
            await WriteFailureAsync(context, wantsHtml, surface, "login_backend_error",
                "Login backend error. Check TenantRegistry DB + EcomAE__SecretSuccession (journalctl -u ecomae-platform).",
                500).ConfigureAwait(false);
        }
    }

    private static bool IsLoginPath(PathString path)
    {
        foreach (var candidate in LoginPaths)
        {
            if (path.Equals(candidate, StringComparison.OrdinalIgnoreCase)
                || path.Equals(candidate.Add("/"), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string SurfaceFromPath(PathString path)
    {
        var p = path.Value ?? "";
        if (p.StartsWith("/erp", StringComparison.OrdinalIgnoreCase))
        {
            return "erp";
        }

        if (p.StartsWith("/bos", StringComparison.OrdinalIgnoreCase))
        {
            return "bos";
        }

        if (p.StartsWith("/storefront", StringComparison.OrdinalIgnoreCase))
        {
            return "storefront";
        }

        return "cp";
    }

    private static async Task WriteFailureAsync(
        HttpContext context,
        bool wantsHtml,
        string surface,
        string code,
        string message,
        int jsonStatus)
    {
        if (wantsHtml)
        {
            context.Response.StatusCode = StatusCodes.Status302Found;
            context.Response.Headers.Location =
                $"/{LegacyLoginSurfaceParser.Key(surface)}/login?error={Uri.EscapeDataString(code)}";
            return;
        }

        context.Response.StatusCode = jsonStatus;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(new { ok = false, code, message }, context.RequestAborted)
            .ConfigureAwait(false);
    }
}
