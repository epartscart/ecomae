namespace EcomAE.Platform.Security;

/// <summary>
/// UX-neutral security headers for all responses. No CSP — avoids breaking Blazor/SignalR.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    public const string CommitShaHeader = "X-EcomAE-Commit-Sha";

    private readonly RequestDelegate _next;
    private readonly string _commitSha;

    public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
    {
        _next = next;
        var releaseFile = Path.Combine(environment.ContentRootPath, "RELEASE_SHA");
        _commitSha = File.Exists(releaseFile) ? File.ReadAllText(releaseFile).Trim() : "unknown";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "SAMEORIGIN";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";
        headers["X-XSS-Protection"] = "0";
        headers[CommitShaHeader] = _commitSha;

        if (context.Request.IsHttps)
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }

        await _next(context);
    }
}
