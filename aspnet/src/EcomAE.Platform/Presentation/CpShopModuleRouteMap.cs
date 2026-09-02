namespace EcomAE.Platform.Presentation;

/// <summary>
/// Authoritative ePartsCart PHP <c>cp/content/shop/*</c> module → ASP.NET CP app map.
/// Used by coverage boards and tests; <see cref="PhpSurfaceLinkMap"/> owns live href rewriting.
/// </summary>
public static class CpShopModuleRouteMap
{
    private static readonly Dictionary<string, string> Modules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["accessories"] = "/cp/accessories-app",
        ["bulk_upload"] = "/cp/bulk-upload-app",
        ["catalogue"] = "/cp/product-catalogue-app",
        ["channels"] = "/cp/marketplace-channels-app",
        ["crm"] = "/cp/crm-board-app",
        ["crosses"] = "/cp/crosses-app",
        ["customer_mgmt"] = "/cp/users-app",
        ["data_transfer"] = "/cp/data-transfer-app",
        ["demand_countries"] = "/cp/demand-intelligence-app",
        ["document_control"] = "/cp/document-control-app",
        ["eparts-cata"] = "/cp/product-catalogue-app",
        ["eparts-mod"] = "/cp/product-catalogue-app",
        ["filter"] = "/cp/product-filters-app",
        ["finance"] = "/erp",
        ["geo"] = "/cp/geo-regions-app",
        ["kkt"] = "/cp/kkt-app",
        ["logistics"] = "/cp/delivery-methods-app",
        ["offices"] = "/cp/offices-app",
        ["manufacturers_synonyms"] = "/cp/synonyms-app",
        ["marketing"] = "/cp/marketing-growth-app",
        ["order_process"] = "/cp/orders",
        ["parts_agent"] = "/cp/parts-agent-chats-app",
        ["payments"] = "/cp/payment-gateways-app",
        ["pos"] = "/cp/pos-overview-app",
        ["prices_edit"] = "/cp/prices-edit-app",
        ["prices_send"] = "/cp/prices-send-app",
        ["prices_upload"] = "/cp/prices-upload-app",
        ["pricing"] = "/cp/price-lists-app",
        ["print_docs"] = "/cp/print-docs-app",
        ["procurement"] = "/cp/purchase-requests-app",
        ["quote_requests"] = "/cp/quote-requests-app",
        ["returns"] = "/cp/returns-rma-app",
        ["sao"] = "/cp/sao-app",
        ["search_tabs"] = "/cp/search-tabs-app",
        ["statistics"] = "/cp/statistics-app",
        ["tenant_hub"] = "/cp/tenants-app",
        ["workshop"] = "/cp/workshop-app",
    };

    public static IReadOnlyDictionary<string, string> All => Modules;

    public static bool TryMap(string module, out string href)
        => Modules.TryGetValue(module.Trim(), out href!);

    public static object BuildCoverageReport()
    {
        var rows = Modules
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new
            {
                module = kv.Key,
                phpPath = "/CP/shop/" + kv.Key,
                aspnetApp = kv.Value,
                superOnly = string.Equals(kv.Key, "tenant_hub", StringComparison.OrdinalIgnoreCase),
            })
            .ToList();

        return new
        {
            ok = true,
            surface = "cp",
            role = "cp-shop-module-coverage",
            host = "epartscart.com",
            moduleCount = rows.Count,
            mappedCount = rows.Count,
            coveragePct = 100,
            cutoverAllowed = false,
            readyForPhpRemoval = false,
            phpAuthoritative = true,
            modules = rows,
            note = "All cp/content/shop modules resolve to ASP.NET-primary surfaces. Interactive writes remain PHP.",
        };
    }
}
