using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Data;
using Microsoft.Extensions.Options;
using static EcomAE.Platform.Storefront.FreeToolsPhp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/general_pages/ajax_epc_free_tools.php</c>: the public free-tools API (register, login, password reset,
/// account deletion, whoami, compute, save, list). Accounts live in the platform database, as PHP's <c>config.php</c> PDO.
/// </summary>
public static class FreeToolsAjaxEndpoint
{
    public const string Path = "/content/general_pages/ajax_epc_free_tools.php";

    public static void Map(IEndpointRouteBuilder endpoints)
        => endpoints.MapMethods(Path, ["GET", "POST", "HEAD"], HandleAsync).DisableAntiforgery();

    private static async Task HandleAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        IOptions<PhpReferenceOptions> php,
        CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(context.Request, cancellationToken).ConfigureAwait(false);
        await using var accounts = new FreeToolsAccounts(
            ct => connections.IsConfigured
                ? connections.OpenRegistryAsync(ct)
                : throw new InvalidOperationException("Platform database is not configured."),
            (to, subject, html, ct) => AuthEmailOtpEndpoints.SendHtmlMailAsync(context, connections, php.Value, to, subject, html, ct));
        int status;
        PhpArray payload;
        try
        {
            (status, payload) = await DispatchAsync(body, accounts, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            (status, payload) = (500, new PhpArray { { "ok", false }, { "message", "Service unavailable, please try again." } });
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        if (!HttpMethods.IsHead(context.Request.Method))
        {
            await context.Response.WriteAsync(JsonEncode(payload), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>PHP <c>json_decode(php://input, true)</c> when it gives an array, else <c>$_POST</c>.</summary>
    public static async Task<PhpArray> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        request.EnableBuffering();
        string raw;
        using (var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
        {
            raw = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        request.Body.Position = 0;
        if (PhpArray.JsonDecode(raw) is PhpArray decoded)
        {
            return decoded;
        }

        if (!request.HasFormContentType)
        {
            return new PhpArray();
        }

        var form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        return PhpArray.FromForm(form.SelectMany(f => f.Value.Select(v => new KeyValuePair<string, string>(f.Key, v ?? string.Empty))));
    }

    private static string Field(PhpArray body, string key, string fallback = "") => Str(body[key] ?? fallback);

    private static string Clean(string value, string pattern) => Regex.Replace(value, pattern, string.Empty);

    public static async Task<(int Status, PhpArray Payload)> DispatchAsync(PhpArray body, FreeToolsAccounts accounts, long now, CancellationToken ct)
    {
        var action = body["action"] is { } a ? Clean(Str(a), "[^a-z_]") : string.Empty;
        switch (action)
        {
            case "register":
                return (200, await accounts.RegisterAsync(Field(body, "email"), Field(body, "company"), Field(body, "country"), Field(body, "password"), ct).ConfigureAwait(false));
            case "login":
                return (200, await accounts.LoginAsync(Field(body, "email"), Field(body, "password"), ct).ConfigureAwait(false));
            case "request_reset":
                return (200, await accounts.RequestResetAsync(Field(body, "email"), ct).ConfigureAwait(false));
            case "confirm_reset":
                return (200, await accounts.ConfirmResetAsync(Field(body, "email"), Field(body, "code"), Field(body, "password"), ct).ConfigureAwait(false));
            case "request_delete":
                return (200, await accounts.RequestDeleteAsync(Field(body, "token"), ct).ConfigureAwait(false));
            case "confirm_delete":
                return (200, await accounts.ConfirmDeleteAsync(Field(body, "token"), Field(body, "code"), ct).ConfigureAwait(false));
            case "whoami":
            {
                var acc = await accounts.AccountByTokenAsync(Field(body, "token"), ct).ConfigureAwait(false);
                return acc is null
                    ? (200, new PhpArray { { "ok", false } })
                    : (200, new PhpArray
                    {
                        { "ok", true },
                        {
                            "account", new PhpArray
                            {
                                { "email", acc.GetValueOrDefault("email") },
                                { "company", acc.GetValueOrDefault("company") },
                                { "country", acc.GetValueOrDefault("country") },
                            }
                        },
                    });
            }

            case "compute":
            {
                var acc = await accounts.AccountByTokenAsync(Field(body, "token"), ct).ConfigureAwait(false);
                if (acc is null)
                {
                    return (200, RegisterFirst());
                }

                var tool = Clean(Field(body, "tool"), "[^a-z]");
                if (!await accounts.IsActiveAsync(tool, ct).ConfigureAwait(false))
                {
                    return (200, new PhpArray { { "ok", false }, { "message", "This tool is temporarily unavailable. Please check back soon." } });
                }

                var country = Str(body["country"] ?? acc.GetValueOrDefault("country") ?? "XX");
                var inputs = body["inputs"] as PhpArray ?? new PhpArray();
                await accounts.TouchAccountAsync(Int(acc.GetValueOrDefault("id")), ct).ConfigureAwait(false);
                return (200, FreeToolsCompute.Compute(tool, country, inputs, now));
            }

            case "save":
            {
                var acc = await accounts.AccountByTokenAsync(Field(body, "token"), ct).ConfigureAwait(false);
                if (acc is null)
                {
                    return (200, RegisterFirst());
                }

                return (200, await accounts.SaveAsync(
                    Int(acc.GetValueOrDefault("id")),
                    Clean(Field(body, "tool"), "[^a-z]"),
                    Field(body, "country"),
                    SubstrBytes(Field(body, "title", "Saved result"), 180),
                    body["payload"] as PhpArray ?? new PhpArray(),
                    ct).ConfigureAwait(false));
            }

            case "list":
            {
                var acc = await accounts.AccountByTokenAsync(Field(body, "token"), ct).ConfigureAwait(false);
                if (acc is null)
                {
                    return (200, RegisterFirst());
                }

                return (200, new PhpArray { { "ok", true }, { "saves", await accounts.ListSavesAsync(Int(acc.GetValueOrDefault("id")), ct).ConfigureAwait(false) } });
            }

            default:
                return (400, new PhpArray { { "ok", false }, { "message", "Unknown action" } });
        }
    }

    private static PhpArray RegisterFirst() => new() { { "ok", false }, { "message", "Please register first." } };
}
