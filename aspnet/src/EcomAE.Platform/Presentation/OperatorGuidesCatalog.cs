namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP-faithful operator guide catalog for tenant CP, Super CP, and ERP books.
/// Chapter text is taken from PHP headings / ol / li — not invented marketing copy.
/// Live KPI snapshots stay empty without a shop MySQL (same as other CP/ERP reads here).
/// </summary>
public static class OperatorGuidesCatalog
{
    public sealed record Chapter(
        string Title,
        string Lead,
        IReadOnlyList<string> Steps,
        string? Note = null);

    public sealed record Guide(
        string Key,
        string Surface,
        string Family,
        string Title,
        string Icon,
        string PhpPath,
        string Href,
        string Summary,
        IReadOnlyList<Chapter> Chapters,
        IReadOnlyList<string> RelatedHrefs);

    public sealed record PhpMap(string Marker, string Href, bool QueryContains = false);

    public static IReadOnlyList<Guide> All { get; } = BuildAll();

    public static IReadOnlyList<string> RequiredKeys { get; } =
    [
        "cp-guideline",
        "prices-upload",
        "oms-daily",
        "fulfilment",
        "whatsapp",
        "logistics",
        "payments",
        "channels",
        "procurement",
        "customer-mgmt",
        "document-control",
        "api-docs",
        "auto-price",
        "workshop",
        "custom-shipping",
        "erp-only-onboard",
        "integrations",
        "failover",
        "power-bi",
        "super-cp-operator",
    ];

    /// <summary>Longest / most specific PHP path fragments → hub or ERP book. Checked before module hubs.</summary>
    public static IReadOnlyList<PhpMap> PhpPathMaps { get; } =
    [
        new("shop/orders/oms-guide", "/cp/guides-app?g=oms-daily"),
        new("shop/orders/whatsapp-guide", "/cp/guides-app?g=whatsapp"),
        new("shop/logistics/whatsapp-guide", "/cp/guides-app?g=whatsapp"),
        new("shop/orders/guide", "/cp/guides-app?g=fulfilment"),
        new("shop/logistics/guide", "/cp/guides-app?g=logistics"),
        new("shop/payments/guide", "/cp/guides-app?g=payments"),
        new("shop/channels/guide", "/cp/guides-app?g=channels"),
        new("shop/procurement/procurement_guide", "/cp/guides-app?g=procurement"),
        new("shop/prices/guide", "/cp/guides-app?g=prices-upload"),
        new("view=guide", "/cp/guides-app?g=prices-upload", QueryContains: true),
        new("control/cp-guideline", "/cp/guides-app?g=cp-guideline"),
        new("control/cp_guideline", "/cp/guides-app?g=cp-guideline"),
        new("control/guideline", "/cp/guides-app?g=cp-guideline"),
        new("shop/customer_mgmt/customer_mgmt_guide", "/cp/guides-app?g=customer-mgmt"),
        new("users/customer_mgmt_guide", "/cp/guides-app?g=customer-mgmt"),
        new("shop/document_control/document_control_guide", "/cp/guides-app?g=document-control"),
        new("epc_api_documentation_guide", "/cp/guides-app?g=api-docs"),
        new("epc_auto_price_guide", "/cp/guides-app?g=auto-price"),
        new("epc_autoworkshop_guide", "/cp/guides-app?g=workshop"),
        new("epc_custom_shipping_guide", "/cp/guides-app?g=custom-shipping"),
        new("epc_erp_only_onboard_guide", "/cp/guides-app?g=erp-only-onboard"),
        new("epc_integrations_guide", "/cp/guides-app?g=integrations"),
        new("epc_platform_failover_guide", "/cp/guides-app?g=failover"),
        new("epc_power_bi_guide", "/cp/guides-app?g=power-bi"),
        new("epc_super_cp_operator_guide", "/cp/guides-app?g=super-cp-operator"),
        new("shop/finance/erp/erp_full_guide", "/erp/guide-app?book=full"),
        new("shop/finance/erp/erp_advanced_guide", "/erp/guide-app?book=advanced"),
        new("shop/finance/erp/erp_only_operator", "/erp/guide-app?book=erp-only"),
        new("custom_shipping/custom_shipping_guide", "/erp/guide-app?book=customs"),
        new("custom-shipping-guide", "/erp/guide-app?book=customs"),
        new("shop/finance/erp/guide", "/erp/guide-app?book=howto"),
    ];

    public static readonly IReadOnlyDictionary<string, string> PathKeys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["/cp/guides-app"] = "",
            ["/cp/control/cp-guideline"] = "cp-guideline",
            ["/cp/control/guideline"] = "cp-guideline",
            ["/cp/shop/prices/guide"] = "prices-upload",
            ["/cp/shop/orders/oms-guide"] = "oms-daily",
            ["/cp/shop/orders/guide"] = "fulfilment",
            ["/cp/shop/orders/whatsapp-guide"] = "whatsapp",
            ["/cp/shop/logistics/guide"] = "logistics",
            ["/cp/shop/logistics/whatsapp-guide"] = "whatsapp",
            ["/cp/shop/payments/guide"] = "payments",
            ["/cp/shop/channels/guide"] = "channels",
            ["/cp/shop/procurement/procurement_guide"] = "procurement",
            ["/cp/shop/customer_mgmt/customer_mgmt_guide"] = "customer-mgmt",
            ["/cp/users/customer_mgmt_guide"] = "customer-mgmt",
            ["/cp/shop/document_control/document_control_guide"] = "document-control",
            ["/cp/control/portal/epc_api_documentation_guide"] = "api-docs",
            ["/cp/control/portal/epc_auto_price_guide"] = "auto-price",
            ["/cp/control/portal/epc_autoworkshop_guide"] = "workshop",
            ["/cp/control/portal/epc_custom_shipping_guide"] = "custom-shipping",
            ["/cp/control/portal/epc_erp_only_onboard_guide"] = "erp-only-onboard",
            ["/cp/control/portal/epc_integrations_guide"] = "integrations",
            ["/cp/control/portal/epc_platform_failover_guide"] = "failover",
            ["/cp/control/portal/epc_power_bi_guide"] = "power-bi",
            ["/cp/control/portal/epc_super_cp_operator_guide"] = "super-cp-operator",
        };

    public static Guide? Get(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var needle = key.Trim();
        foreach (var row in All)
        {
            if (row.Key.Equals(needle, StringComparison.OrdinalIgnoreCase))
            {
                return row;
            }
        }

        return null;
    }

    public static IReadOnlyList<Guide> ForFamily(string family)
    {
        var hits = new List<Guide>();
        foreach (var row in All)
        {
            if (row.Family.Equals(family, StringComparison.OrdinalIgnoreCase))
            {
                hits.Add(row);
            }
        }

        return hits;
    }

    public static bool TryMapPhpPath(string value, out string href)
    {
        href = "";
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var path = value.Split('?', 2)[0];
        foreach (var map in PhpPathMaps)
        {
            if (map.QueryContains)
            {
                if (value.Contains(map.Marker, StringComparison.OrdinalIgnoreCase)
                    && path.Contains("shop/prices", StringComparison.OrdinalIgnoreCase))
                {
                    href = map.Href;
                    return true;
                }

                continue;
            }

            if (path.Contains(map.Marker, StringComparison.OrdinalIgnoreCase)
                || value.Contains(map.Marker, StringComparison.OrdinalIgnoreCase))
            {
                href = map.Href;
                return true;
            }
        }

        return false;
    }

    public static string? KeyFromRequestPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var key = path.Trim();
        var q = key.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
        {
            key = key[..q];
        }

        key = key.TrimEnd('/');
        return PathKeys.TryGetValue(key, out var mapped) ? mapped : null;
    }

    internal static Chapter Ch(string title, string lead, IReadOnlyList<string> steps, string? note = null)
        => new(title, lead, steps, note);

    private static IReadOnlyList<Guide> BuildAll() => OperatorGuideChapters.AllGuides();
}
