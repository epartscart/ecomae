using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// Rewrites tenant Control Panel ajax posts onto the existing procurement and ERP engines
/// before the product-path redirect can turn them into a browse page.
/// </summary>
public sealed class CpLegacyPhpAjaxLinkMiddleware
{
    private readonly RequestDelegate _next;

    public CpLegacyPhpAjaxLinkMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var action = context.Request.Query["action"].ToString();
        if (action.Length == 0
            && HttpMethods.IsPost(context.Request.Method)
            && context.Request.HasFormContentType
            && string.Equals(context.Request.Path.Value, CpLegacyPhpAjaxLinks.CurrencyRates, StringComparison.OrdinalIgnoreCase))
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
            action = form["action"].ToString();
        }

        if (CpLegacyPhpAjaxLinks.TryRewrite(context.Request.Method, context.Request.Path, action, out var target, out var operatorPost))
        {
            if (operatorPost)
            {
                context.Items[CpLegacyPhpAjaxLinks.OperatorPostItem] = true;
            }

            context.Request.Path = target;
        }

        await _next(context).ConfigureAwait(false);
    }
}
