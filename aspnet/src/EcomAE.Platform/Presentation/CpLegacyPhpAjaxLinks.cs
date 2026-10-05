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
    public const string CurrencyRates = "/cp/content/shop/finance/ajax_currency_live_rates.php";
    public const string QuoteAltOptions = "/cp/content/shop/quote_requests/ajax_epc_quote_alt_options.php";
    public const string CrmScript = "/cp/content/shop/crm/ajax_crm.php";
    public const string CustomerScript = "/cp/content/shop/customer_mgmt/ajax_customer_mgmt.php";
    public const string UsersCustomerScript = "/cp/content/users/ajax_customer_mgmt.php";
    public const string DocumentScript = "/cp/content/shop/document_control/ajax_document_control.php";
    public const string OperatorPostItem = "EcomAE.CpPhpOperatorPost";

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
            || path.Equals(ErpEndpoint, StringComparison.OrdinalIgnoreCase)
            || path.Equals(CurrencyRates, StringComparison.OrdinalIgnoreCase)
            || path.Equals(QuoteAltOptions, StringComparison.OrdinalIgnoreCase)
            || path.Equals(CrmScript, StringComparison.OrdinalIgnoreCase)
            || path.Equals(CustomerScript, StringComparison.OrdinalIgnoreCase)
            || path.Equals(UsersCustomerScript, StringComparison.OrdinalIgnoreCase)
            || path.Equals(DocumentScript, StringComparison.OrdinalIgnoreCase)
            || path.Equals(PricePackSetup, StringComparison.OrdinalIgnoreCase)
            || path.Equals(PriceEnableKeys, StringComparison.OrdinalIgnoreCase);
    }

    public const string PricePackSetup = "/cp/content/shop/prices_upload/ajax_1_prepare_tmp_dir.php";
    public const string PriceEnableKeys = "/cp/content/shop/prices_upload/ajax_7_enable_keys.php";

    public static bool TryRewrite(string? method, PathString path, string? action, out PathString target, out bool operatorPost)
    {
        target = default;
        operatorPost = false;
        if (TryRewritePost(method, path, out target))
        {
            return true;
        }

        if (Equals(path, QuoteAltOptions) && (HttpMethods.IsGet(method ?? string.Empty) || HttpMethods.IsPost(method ?? string.Empty)))
        {
            target = EcomAeRoutes.CpQuoteAltOptions;
            return true;
        }

        if (!Equals(path, CurrencyRates))
        {
            return false;
        }

        var name = string.IsNullOrWhiteSpace(action) ? "preview" : action.Trim();
        if (HttpMethods.IsGet(method ?? string.Empty) && name == "preview")
        {
            target = EcomAeRoutes.CpCurrenciesLivePreview;
            return true;
        }

        if (HttpMethods.IsGet(method ?? string.Empty) && name == "schedule_get")
        {
            target = EcomAeRoutes.CpCurrenciesScheduleGet;
            return true;
        }

        if (!HttpMethods.IsPost(method ?? string.Empty))
        {
            return false;
        }

        if (name == "apply")
        {
            target = EcomAeRoutes.CpCurrenciesLiveApply;
            operatorPost = true;
            return true;
        }

        if (name == "schedule_save")
        {
            target = EcomAeRoutes.CpCurrenciesScheduleSave;
            operatorPost = true;
            return true;
        }

        if (name == "schedule_run_now")
        {
            target = EcomAeRoutes.CpCurrenciesScheduleRunNow;
            operatorPost = true;
            return true;
        }

        return false;
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
