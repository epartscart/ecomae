namespace EcomAE.Platform.Presentation;

/// <summary>
/// Rewrites PHP product hrefs to ASP.NET browse routes.
/// PHP stays available only under /php-reference/* (never as a primary click target).
/// </summary>
public static class PhpSurfaceLinkMap
{
    public static string AspNetPrimaryHref(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return "/";
        }

        var value = href.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
        {
            if (absolute.Host.Equals("epartscart.com", StringComparison.OrdinalIgnoreCase)
                || absolute.Host.EndsWith(".epartscart.com", StringComparison.OrdinalIgnoreCase))
            {
                value = string.IsNullOrEmpty(absolute.AbsolutePath) ? "/" : absolute.AbsolutePath;
                if (!string.IsNullOrEmpty(absolute.Query))
                {
                    value += absolute.Query;
                }
            }
            else if (absolute.Host.Equals("www.ecomae.com", StringComparison.OrdinalIgnoreCase)
                || absolute.Host.Equals("ecomae.com", StringComparison.OrdinalIgnoreCase)
                || absolute.Host.EndsWith(".ecomae.com", StringComparison.OrdinalIgnoreCase))
            {
                return MapMarketingPath(absolute.AbsolutePath, absolute.Fragment);
            }
        }

        // Uppercase PHP shells / deep modules → concrete ASP.NET apps when known (never leave /CP/ /ERP/ /BOS/).
        if (IsUpperPhpShell(value, "CP"))
        {
            return MapCpPhpPath(value);
        }

        if (IsUpperPhpShell(value, "ERP")
            || value.Contains("epc_erp_shell=", StringComparison.OrdinalIgnoreCase))
        {
            return MapErpPhpPath(value);
        }

        if (IsUpperPhpShell(value, "BOS"))
        {
            return MapBosPhpPath(value);
        }

        if (value.StartsWith("/php-reference/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/storefront/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/marketing/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/cp/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/erp/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/bos/", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/cp", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/erp", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/bos", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/", StringComparison.Ordinal))
        {
            return value;
        }

        if (value.StartsWith("/shop/cart", StringComparison.OrdinalIgnoreCase))
        {
            return "/storefront/cart-app";
        }

        if (value.StartsWith("/shop/checkout", StringComparison.OrdinalIgnoreCase))
        {
            return "/storefront/checkout-app";
        }

        if (value.StartsWith("/shop/orders", StringComparison.OrdinalIgnoreCase))
        {
            return "/storefront/orders-app";
        }

        if (value.StartsWith("/shop/part_search", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/shop/search", StringComparison.OrdinalIgnoreCase))
        {
            return "/storefront/search-app";
        }

        if (value.Contains("garage", StringComparison.OrdinalIgnoreCase))
        {
            return "/storefront/garage-app";
        }

        if (value.StartsWith("/users", StringComparison.OrdinalIgnoreCase))
        {
            return "/storefront/login";
        }

        if (value.StartsWith("/shop/", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/index.php", StringComparison.OrdinalIgnoreCase))
        {
            return "/";
        }

        return "/";
    }

    private static string MapMarketingPath(string path, string fragment)
    {
        var value = string.IsNullOrEmpty(path) ? "/" : path;
        var frag = string.IsNullOrEmpty(fragment) ? "" : fragment;
        if (value.Equals("/", StringComparison.Ordinal) || value.Equals("/index.php", StringComparison.OrdinalIgnoreCase))
        {
            return EcomaeMarketingPages.AspNetHome + frag;
        }

        if (value.StartsWith("/cp", StringComparison.OrdinalIgnoreCase))
        {
            return "/cp";
        }

        if (value.StartsWith("/erp", StringComparison.OrdinalIgnoreCase))
        {
            return "/erp";
        }

        if (value.StartsWith("/bos", StringComparison.OrdinalIgnoreCase) && !value.StartsWith("/BOS", StringComparison.Ordinal))
        {
            return "/marketing/bos" + frag;
        }

        // /platform/demo → /marketing/demo ; /platform → /marketing/platform ; /legal → /marketing/legal
        if (value.StartsWith("/platform/", StringComparison.OrdinalIgnoreCase))
        {
            var rest = value["/platform/".Length..];
            return "/marketing/" + rest.TrimEnd('/') + frag;
        }

        if (value.Equals("/platform", StringComparison.OrdinalIgnoreCase))
        {
            return "/marketing/platform" + frag;
        }

        if (value.StartsWith("/brochure/cp", StringComparison.OrdinalIgnoreCase))
        {
            return "/marketing/brochure-cp" + frag;
        }

        var slug = value.Trim('/');
        return "/marketing/" + slug + frag;
    }

    public static string PhpReferenceOnlyHref(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return "/php-reference/home";
        }

        var value = href.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
        {
            value = absolute.AbsolutePath;
        }

        if (IsUpperPhpShell(value, "CP")
            || value.StartsWith("/cp/", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/cp", StringComparison.OrdinalIgnoreCase))
        {
            return "/php-reference/cp";
        }

        if (IsUpperPhpShell(value, "ERP")
            || value.StartsWith("/erp/", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/erp", StringComparison.OrdinalIgnoreCase))
        {
            return "/php-reference/erp";
        }

        if (IsUpperPhpShell(value, "BOS")
            || value.StartsWith("/bos/", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/bos", StringComparison.OrdinalIgnoreCase))
        {
            return "/php-reference/bos";
        }

        if (value.StartsWith("/shop/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/users", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/storefront", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/", StringComparison.Ordinal)
            || value.Equals("/index.php", StringComparison.OrdinalIgnoreCase))
        {
            return "/php-reference/home";
        }

        return "/php-reference/storefront";
    }

    public static bool IsPhpProductHref(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        var value = href.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute)
            && (absolute.Host.Equals("epartscart.com", StringComparison.OrdinalIgnoreCase)
                || absolute.Host.EndsWith(".epartscart.com", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return IsUpperPhpShell(value, "CP")
            || IsUpperPhpShell(value, "ERP")
            || IsUpperPhpShell(value, "BOS")
            || value.StartsWith("/shop/", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".php", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUpperPhpShell(string value, string shell)
    {
        // Product PHP chrome uses uppercase /CP /ERP /BOS (catalog + legacy nav).
        var prefix = "/" + shell;
        return value.StartsWith(prefix, StringComparison.Ordinal)
            || value.StartsWith(prefix + "/", StringComparison.Ordinal)
            || value.StartsWith(prefix + "?", StringComparison.Ordinal);
    }

    private static string MapCpPhpPath(string value)
    {
        // CP embeds ERP tabs under /CP/shop/finance/erp — route those via ERP map.
        if (value.Contains("epc_erp_shell=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("/finance/erp", StringComparison.OrdinalIgnoreCase))
        {
            return MapErpPhpPath(value);
        }

        var path = value.Split('?', 2)[0];
        // Common CP brochure / logistics / payments deep links → *-app routes.
        if (ContainsAny(path, "payments", "payment"))
        {
            return "/cp/payment-gateways-app";
        }

        if (ContainsAny(path, "channels_main", "channels"))
        {
            return "/cp/marketplace-channels-app";
        }

        if (ContainsAny(path, "carriers"))
        {
            return "/cp/carriers-app";
        }

        if (ContainsAny(path, "sposoby-polucheniya", "delivery"))
        {
            return "/cp/delivery-methods-app";
        }

        if (ContainsAny(path, "epc_integrations", "integrations"))
        {
            return "/cp/integrations-app";
        }

        if (ContainsAny(path, "epc_api_clients", "api_clients"))
        {
            return "/cp/api-clients-app";
        }

        if (ContainsAny(path, "epc_demo_tenants", "demo_tenants"))
        {
            return "/cp/demo-tenants-app";
        }

        if (ContainsAny(path, "epc_platform_governance", "governance"))
        {
            return "/cp/platform-governance-app";
        }

        if (ContainsAny(path, "epc_free_tools", "free_tools"))
        {
            return "/cp/free-tools-app";
        }

        if (ContainsAny(path, "epc_pos_tenant", "pos"))
        {
            return "/cp/pos-overview-app";
        }

        if (ContainsAny(path, "tenant_control", "tenants"))
        {
            return "/cp/tenants-app";
        }

        if (ContainsAny(path, "failover"))
        {
            return "/cp/failover-status-app";
        }

        if (ContainsAny(path, "industry_settings", "industry"))
        {
            return "/cp/industry-packs-app";
        }

        if (ContainsAny(path, "ops", "guide"))
        {
            return "/cp/ops-guides-app";
        }

        return "/cp";
    }

    private static string MapErpPhpPath(string value)
    {
        var tab = ExtractQuery(value, "tab");
        var area = ExtractQuery(value, "area");
        var key = (tab ?? area ?? string.Empty).Trim().ToLowerInvariant();

        return key switch
        {
            "dashboard" or "overview" => "/erp",
            "processflow" or "process_flow" or "workflow" => "/erp/process-flow-tasks-app",
            "aging" or "ar_aging" or "ap_aging" => "/erp/aging-app",
            "report_center" or "reports" or "reportcenter" => "/erp/report-center-app",
            "sales_orders" or "salesorders" => "/erp/sales-orders-app",
            "sales_quotations" or "quotations" => "/erp/sales-quotations-app",
            "purchase_orders" or "purchaseorders" => "/erp/purchase-orders-app",
            "purchases" or "payables" => "/erp/purchases-app",
            "invoices" or "receivables" => "/erp/invoices-app",
            "cash_bank" or "cash" or "banking" => "/erp/cash-accounts-app",
            "cash_entries" or "bank_entries" => "/erp/cash-entries-app",
            "coa" or "chart_of_accounts" => "/erp/coa-accounts-app",
            "gl" or "journals" or "general_journal" => "/erp/gl-journals-app",
            "inventory" or "stock" or "inventory_stock" => "/erp/inventory-stock-app",
            "stock_movements" or "movements" or "ledger" => "/erp/stock-movements-app",
            "stock_transfers" or "transfers" => "/erp/stock-transfers-app",
            "warehouses" or "warehouse" or "wms" => "/erp/warehouses-app",
            "suppliers" or "vendors" => "/erp/suppliers-app",
            "fixed_assets" or "assets" => "/erp/fixed-assets-app",
            "bank_reconciliation" or "reconciliation" => "/erp/bank-reconciliation-app",
            "on_premises" or "onpremises" => "/erp/on-premises-app",
            "favorites" or "workspace" => "/erp/workspace-favorites-app",
            "accounts" => "/erp/accounts-summary-app",
            _ => "/erp",
        };
    }

    private static string MapBosPhpPath(string value)
    {
        var path = value.Split('?', 2)[0].ToLowerInvariant();
        if (path.Contains("tenant", StringComparison.Ordinal))
        {
            return "/bos/tenants-app";
        }

        if (path.Contains("health", StringComparison.Ordinal))
        {
            return "/bos/fleet-health-app";
        }

        if (path.Contains("ready", StringComparison.Ordinal) || path.Contains("readiness", StringComparison.Ordinal))
        {
            return "/bos/fleet-readiness-app";
        }

        if (path.Contains("audit", StringComparison.Ordinal))
        {
            return "/bos/audit-log-app";
        }

        if (path.Contains("summary", StringComparison.Ordinal))
        {
            return "/bos/fleet-summary-app";
        }

        return "/bos";
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (haystack.Contains(n, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ExtractQuery(string href, string key)
    {
        var qIndex = href.IndexOf('?', StringComparison.Ordinal);
        if (qIndex < 0 || qIndex >= href.Length - 1)
        {
            return null;
        }

        var query = href[(qIndex + 1)..];
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(kv[1]);
            }
        }

        return null;
    }
}
