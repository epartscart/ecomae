using System.Text;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Cp.PriceImport;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Storefront;

/// <summary>The PHP URLs of the UAE gateway stubs under <c>content/shop/finance/payment_systems/</c>.</summary>
public static class StorefrontPhpPaymentGatewayEndpoints
{
    private const string HtmlType = "text/html; charset=UTF-8";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var root = StorefrontPhpAjax.PaymentSystemsPath;
        foreach (var handler in StorefrontPhpAjax.DemoGatewayHandlers)
        {
            var name = handler;
            Route(endpoints, root + "/" + name + "/go_to_pay.php", (context, connections, ct) => GoToPayAsync(context, connections, name, ct));
            if (name != "nowpayments")
            {
                Route(endpoints, root + "/" + name + "/notification.php", (context, connections, ct) => NotificationAsync(context, connections, name, ct));
            }
        }

        foreach (var path in new[] { "/go_to_pay.php", "/epc_demo/go_to_pay.php", "/notification.php", "/epc_demo/notification.php" })
        {
            Route(endpoints, root + path, (_, _, _) => Task.FromResult(Results.Text(StorefrontPhpAjax.NoHandler, HtmlType)));
        }

        Route(endpoints, root + "/pay_page_entry.php", (context, _, ct) => PayPageAsync(context, true, ct));
        Route(endpoints, root + "/epc_demo/pay_page.php", (context, _, ct) => PayPageAsync(context, true, ct));
        Route(endpoints, root + "/pay_page.php", (context, _, ct) => PayPageAsync(context, false, ct));
        Route(endpoints, root + "/crypto_pay_page.php", CryptoPayPageAsync);
        Route(endpoints, root + "/nowpayments/notification.php", NowPaymentsNotificationAsync);
    }

    private static void Route(IEndpointRouteBuilder endpoints, string path, Func<HttpContext, ITenantDbConnectionFactory, CancellationToken, Task<IResult>> handler)
        => endpoints.MapMethods(
                path,
                ["GET", "POST"],
                (HttpContext context, ITenantDbConnectionFactory connections, CancellationToken cancellationToken) => handler(context, connections, cancellationToken))
            .DisableAntiforgery()
            .AllowAnonymous();

    private static async Task<IResult> GoToPayAsync(HttpContext context, ITenantDbConnectionFactory connections, string handler, CancellationToken cancellationToken)
    {
        var request = await RequestAsync(context, cancellationToken).ConfigureAwait(false);
        return await WithDbAsync(
            context,
            connections,
            (connection, ct) => StorefrontPhpAjax.GoToPayAsync(connection, handler, request, ct),
            Results.Text("{\"result\":false}", HtmlType),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IResult> PayPageAsync(HttpContext context, bool entry, CancellationToken cancellationToken)
    {
        var request = await RequestAsync(context, cancellationToken).ConfigureAwait(false);
        return Emit(StorefrontPhpAjax.PayPage(request, entry));
    }

    private static async Task<IResult> CryptoPayPageAsync(HttpContext context, ITenantDbConnectionFactory connections, CancellationToken cancellationToken)
    {
        var request = await RequestAsync(context, cancellationToken).ConfigureAwait(false);
        var http = context.RequestServices.GetService<IHttpClientFactory>();
        StorefrontPhpAjax.NowPaymentsPost post = async (url, apiKey, json, ct) =>
        {
            using var client = http?.CreateClient() ?? new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            using var message = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            message.Headers.TryAddWithoutValidation("x-api-key", apiKey);
            using var response = await client.SendAsync(message, ct).ConfigureAwait(false);
            return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        };
        return await WithDbAsync(
            context,
            connections,
            (connection, ct) => StorefrontPhpAjax.CryptoPayPageAsync(connection, request, post, ct),
            Results.Text("Database connection error", HtmlType, statusCode: StatusCodes.Status500InternalServerError),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IResult> NotificationAsync(HttpContext context, ITenantDbConnectionFactory connections, string handler, CancellationToken cancellationToken)
    {
        var request = await RequestAsync(context, cancellationToken).ConfigureAwait(false);
        var payments = Payments(context);
        return await WithDbAsync(
            context,
            connections,
            (connection, ct) => StorefrontPhpAjax.GatewayNotificationAsync(connection, payments, handler, request, ct),
            Results.Text("{\"result\":false}", HtmlType),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IResult> NowPaymentsNotificationAsync(HttpContext context, ITenantDbConnectionFactory connections, CancellationToken cancellationToken)
    {
        context.Request.EnableBuffering();
        string raw;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            raw = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        context.Request.Body.Position = 0;
        var request = await RequestAsync(context, cancellationToken).ConfigureAwait(false);
        var payments = Payments(context);
        var signature = context.Request.Headers["x-nowpayments-sig"].ToString();
        return await WithDbAsync(
            context,
            connections,
            (connection, ct) => StorefrontPhpAjax.NowPaymentsNotificationAsync(connection, payments, request, raw, signature, ct),
            Results.Text("{\"result\":false}", HtmlType, statusCode: StatusCodes.Status500InternalServerError),
            cancellationToken).ConfigureAwait(false);
    }

    private static IStorefrontPaymentWriteService Payments(HttpContext context)
        => context.RequestServices.GetService<IStorefrontPaymentWriteService>()
           ?? ActivatorUtilities.CreateInstance<StorefrontPaymentWriteService>(context.RequestServices);

    private static async Task<StorefrontPhpAjax.GatewayPageRequest> RequestAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var query = context.Request.Query.ToDictionary(p => p.Key, p => p.Value.ToString(), StringComparer.Ordinal);
        var form = new Dictionary<string, string>(StringComparer.Ordinal);
        if (HttpMethods.IsPost(context.Request.Method) && context.Request.HasFormContentType)
        {
            foreach (var pair in await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false))
            {
                form[pair.Key] = pair.Value.ToString();
            }
        }

        var options = context.RequestServices.GetService<IOptions<PhpReferenceOptions>>();
        var config = options is null ? new Dictionary<string, string>(StringComparer.Ordinal) : CpPhpConfig.Read(options.Value);
        return new StorefrontPhpAjax.GatewayPageRequest(
            query,
            form,
            context.Request.Cookies["session"],
            context.Request.Cookies["u_id"],
            context.Request.Cookies["admin_session"],
            context.Request.Cookies["admin_u_id"],
            context.Request.Headers.Referer.ToString(),
            StorefrontPhpHomeLinks.LangHref(context),
            config);
    }

    private static async Task<IResult> WithDbAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        Func<System.Data.Common.DbConnection, CancellationToken, Task<object>> body,
        IResult fallback,
        CancellationToken cancellationToken)
    {
        if (!connections.IsConfigured)
        {
            return fallback;
        }

        try
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
            return Emit(await body(connection, cancellationToken).ConfigureAwait(false));
        }
        catch (System.Data.Common.DbException)
        {
            return fallback;
        }
    }

    private static IResult Emit(object payload)
        => payload switch
        {
            StorefrontPhpAjax.GatewayRedirect redirect => Results.Redirect(redirect.Location),
            StorefrontPhpAjax.RawHttp raw => Results.Text(raw.Body, raw.ContentType, statusCode: raw.StatusCode),
            _ => Results.Text(payload.ToString() ?? string.Empty, HtmlType),
        };
}
