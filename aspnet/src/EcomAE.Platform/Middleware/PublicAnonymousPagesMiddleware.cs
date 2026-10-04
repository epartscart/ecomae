using System.Text;
using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// Anonymous pages that must run before <see cref="PhpProductPathRedirectMiddleware"/>
/// so <c>/shop/erp-demo</c> and marketing <c>/shop</c> are not folded into product redirects.
/// </summary>
public sealed class PublicAnonymousPagesMiddleware
{
    private readonly RequestDelegate _next;

    public PublicAnonymousPagesMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value;
        var host = context.Request.Host.Host;

        if (PublicAnonymousPages.IsErpDemoPath(path))
        {
            await WriteHtml(context, PublicErpDemoPage.Html(context.Request.Query["industry"].ToString()));
            return;
        }

        if (PublicAnonymousPages.IsTenantCpBrochurePath(host, path))
        {
            var print = context.Request.Query["print"].ToString() == "1";
            var html = TenantCpBrochure.HtmlFor(
                context.Request.Query["scope"].ToString(),
                context.Request.Query["view"].ToString(),
                print);
            if (html.Length == 0)
            {
                await _next(context);
                return;
            }

            await WriteHtml(context, html);
            return;
        }

        if (PublicAnonymousPages.IsMarketingStorefrontDemo(host, path))
        {
            context.Request.Path = "/en";
        }

        await _next(context);
    }

    private static async Task WriteHtml(HttpContext context, string html)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        var bytes = Encoding.UTF8.GetBytes(html);
        context.Response.ContentLength = bytes.Length;
        if (HttpMethods.IsHead(context.Request.Method))
        {
            return;
        }

        await context.Response.Body.WriteAsync(bytes);
    }
}
