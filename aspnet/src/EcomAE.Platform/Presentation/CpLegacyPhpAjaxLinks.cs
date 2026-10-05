namespace EcomAE.Platform.Presentation;

using EcomAE.Platform.Routing;

/// <summary>
/// Control Panel pages still post to the PHP ajax URLs. Procurement posts use the existing
/// procurement dispatcher, which calls the ERP write services. ERP posts use the existing
/// ERP ajax endpoint. The include scripts stay “No access”, matching PHP’s _ASTEXE_ guard.
/// </summary>
public static class CpLegacyPhpAjaxLinks
{
    public const string ProcurementScript = "/cp/content/shop/procurement/ajax_procurement.php";
    public const string ProcurementEndpoint = "/cp/content/shop/procurement/ajax_procurement_endpoint.php";
    public const string ErpScript = "/cp/content/shop/finance/erp/ajax_erp.php";
    public const string ErpEndpoint = "/cp/content/shop/finance/erp/ajax_erp_endpoint.php";

    public static bool IsGuardedScript(PathString path)
        => Equals(path, ProcurementScript) || Equals(path, ErpScript);

    public static bool IsLinkedEndpoint(PathString path)
        => Equals(path, ProcurementEndpoint) || Equals(path, ErpEndpoint);

    public static bool StaysOnAjax(string? pathAndQuery)
    {
        var path = PathOnly(pathAndQuery);
        return path.Equals(ProcurementScript, StringComparison.OrdinalIgnoreCase)
            || path.Equals(ProcurementEndpoint, StringComparison.OrdinalIgnoreCase)
            || path.Equals(ErpScript, StringComparison.OrdinalIgnoreCase)
            || path.Equals(ErpEndpoint, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryRewritePost(string? method, PathString path, out PathString target)
    {
        target = default;
        if (!HttpMethods.IsPost(method ?? string.Empty))
        {
            return false;
        }

        if (Equals(path, ProcurementEndpoint))
        {
            target = EcomAeRoutes.CpProcurementAjax;
            return true;
        }

        if (Equals(path, ErpEndpoint))
        {
            target = EcomAeRoutes.ErpAjaxPhpEndpoint;
            return true;
        }

        return false;
    }

    private static bool Equals(PathString path, string expected)
        => string.Equals(path.Value, expected, StringComparison.OrdinalIgnoreCase);

    private static string PathOnly(string? pathAndQuery)
    {
        var value = (pathAndQuery ?? string.Empty).Trim();
        var query = value.IndexOf('?', StringComparison.Ordinal);
        return query < 0 ? value : value[..query];
    }
}
