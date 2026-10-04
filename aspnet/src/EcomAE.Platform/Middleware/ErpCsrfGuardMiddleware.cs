using System.Text;
using System.Text.Json;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Routing;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// PHP <c>stop_csrf.php</c> twin for every ERP write request: an unsafe-method request under
/// <c>/erp/*</c> (or the ajax_epc_erp.php twin) carrying an admin session must present the
/// session's <c>csrf_guard_key</c> (form field, JSON property, query or <see cref="Header"/>)
/// and it must equal <c>sessions.csrf_guard_key</c>. Login/logout are excluded (no session yet).
/// Failure is a 403 with zero writes — never an <c>ok:true</c> stub.
/// </summary>
public sealed class ErpCsrfGuardMiddleware
{
    public const string Header = "X-Csrf-Guard-Key";
    public const string ResultHeader = "X-EcomAE-Erp-Csrf";

    private static readonly string[] Excluded = [EcomAeRoutes.ErpLogin, EcomAeRoutes.ErpLogout];

    private readonly RequestDelegate _next;

    public ErpCsrfGuardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public static bool AppliesTo(string method, string path)
    {
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
        {
            return false;
        }

        var bare = path.TrimEnd('/');
        if (bare.Length == 0)
        {
            bare = "/";
        }

        foreach (var excluded in Excluded)
        {
            if (bare.Equals(excluded, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return bare.Equals("/erp", StringComparison.OrdinalIgnoreCase)
               || bare.StartsWith("/erp/", StringComparison.OrdinalIgnoreCase)
               || bare.Equals(EcomAeRoutes.ErpAjaxPhpEndpoint, StringComparison.OrdinalIgnoreCase);
    }

    public async Task InvokeAsync(HttpContext context, ILegacySessionValidator sessions, ICpCsrfGuard csrf)
    {
        if (!AppliesTo(context.Request.Method, context.Request.Path.Value ?? "/"))
        {
            await _next(context);
            return;
        }

        var session = await sessions.ValidateAsync(context, context.RequestAborted).ConfigureAwait(false);
        if (session.Kind != LegacySessionKind.Admin)
        {
            // Anonymous requests are refused by the auth gate / handlers; API-key callers are not cookie-bound.
            await _next(context);
            return;
        }

        var submitted = await SubmittedKeyAsync(context).ConfigureAwait(false);
        var verdict = await csrf.VerifyAsync(context, session, submitted, context.RequestAborted).ConfigureAwait(false);
        if (verdict.Ok)
        {
            context.Response.Headers[ResultHeader] = "ok";
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.Headers[ResultHeader] = verdict.Code;
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            ok = false,
            status = false,
            writes = 0,
            code = verdict.Code,
            validation_code = verdict.Code,
            message = verdict.Message,
        })).ConfigureAwait(false);
    }

    public static async Task<string?> SubmittedKeyAsync(HttpContext context)
    {
        var request = context.Request;

        var header = request.Headers[Header].ToString();
        if (!string.IsNullOrWhiteSpace(header))
        {
            return header;
        }

        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
            var field = form[CpCsrfGuard.FieldName].ToString();
            if (!string.IsNullOrWhiteSpace(field))
            {
                return field;
            }
        }
        else if ((request.ContentType ?? string.Empty).Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            var fromJson = await JsonKeyAsync(context).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(fromJson))
            {
                return fromJson;
            }
        }

        var query = request.Query[CpCsrfGuard.FieldName].ToString();
        return string.IsNullOrWhiteSpace(query) ? null : query;
    }

    private static async Task<string?> JsonKeyAsync(HttpContext context)
    {
        var request = context.Request;
        request.EnableBuffering();
        string body;
        using (var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false);
        }

        request.Body.Position = 0;
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if ((property.Name.Equals(CpCsrfGuard.FieldName, StringComparison.OrdinalIgnoreCase)
                     || property.Name.Equals("csrfGuardKey", StringComparison.OrdinalIgnoreCase))
                    && property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString();
                }
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}
