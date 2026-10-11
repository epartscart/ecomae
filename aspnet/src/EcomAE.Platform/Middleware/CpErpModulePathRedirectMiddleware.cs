namespace EcomAE.Platform.Middleware;

/// <summary>
/// ERP modules live only under /erp. Legacy /cp/{erp-module}-app bookmarks are
/// redirected permanently so CP never renders ERP finance/operations content.
/// </summary>
public sealed class CpErpModulePathRedirectMiddleware
{
    public static readonly IReadOnlySet<string> ErpOnlySlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "aml-compliance-app", "audit-trail-app", "blockchain-proofs-app", "budgets-app", "consolidations-app",
        "cost-models-app", "crm-opportunities-app", "crm-tickets-app", "data-migrations-app", "doc-expiry-app",
        "einvoice-documents-app", "electronic-reporting-app", "fin-advanced-app", "finance-close-app",
        "hr-overview-app", "insurance-compliance-app", "jewellery-fixing-app", "jewellery-masters-app",
        "jewellery-repairs-app", "jewellery-retail-app", "jewellery-stock-verification-app", "landed-cost-app",
        "pos-overview-app", "production-overview-app", "projects-overview-app", "purchase-requests-app",
        "tax-external-reporting-app", "tenant-config-app", "uae-tax-compliance-app", "warehouse-wms-app",
        "workflows-app",
    };

    private readonly RequestDelegate _next;

    public CpErpModulePathRedirectMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        if (path.StartsWith("/cp/", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsGet(context.Request.Method))
        {
            var slug = path["/cp/".Length..].TrimEnd('/');
            if (ErpOnlySlugs.Contains(slug))
            {
                context.Response.Headers["x-ecomae-surface-ownership"] = "erp-only";
                context.Response.Redirect("/erp/" + slug + context.Request.QueryString.Value, permanent: true);
                return Task.CompletedTask;
            }
        }

        return _next(context);
    }
}
