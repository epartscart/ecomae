using EcomAE.Platform.Auth;
using EcomAE.Platform.Security;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Maps an ERP path to a capability area and decides whether a controlled user may open or post it.
/// Users with no posted grants stay on the existing module ACL. Super ERP is never limited.
/// </summary>
public static class ErpControlledAccess
{
    private static readonly string[] WriteActions =
    [
        "New", "Edit", "Delete", "Void", "Submit", "Approve", "Reject", "Post", "Reverse",
    ];

    private static readonly (string Prefix, string Group)[] Prefixes = BuildPrefixes();

    public static string Normalize(string? path)
    {
        var text = (path ?? string.Empty).Split('?', 2)[0].Trim();
        var marker = text.IndexOf("/erp", StringComparison.OrdinalIgnoreCase);
        if (marker > 0 && text.Contains("client-erp", StringComparison.OrdinalIgnoreCase))
        {
            text = text[marker..];
        }

        if (text.Length > 1 && text.EndsWith('/'))
        {
            text = text.TrimEnd('/');
        }

        return text.ToLowerInvariant();
    }

    public static string? AreaForPath(string? path)
    {
        var text = Normalize(path);
        if (text.Length == 0 || text is "/erp" or "/erp/app" or "/erp/login" or "/erp/logout" or "/erp/guide-app" or "/erp/lookups" or "/erp/landing" or "/erp/landing/save")
        {
            return null;
        }

        if (!text.StartsWith("/erp", StringComparison.Ordinal))
        {
            return null;
        }

        if (text.StartsWith("/erp/ajax/", StringComparison.Ordinal))
        {
            return AjaxArea(text);
        }

        foreach (var (prefix, group) in Prefixes)
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal))
            {
                return group;
            }
        }

        return null;
    }

    public static bool Allows(LegacySessionContext session, string? path, string? method)
    {
        ArgumentNullException.ThrowIfNull(session);
        var area = AreaForPath(path);
        if (area is null)
        {
            return true;
        }

        if (session.Permissions.Contains(EcomAePermissions.SuperErpAccess))
        {
            return true;
        }

        if (session.AreaGrants is not { Count: > 0 })
        {
            return true;
        }

        if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
        {
            return true;
        }

        var verb = (method ?? "GET").Trim().ToUpperInvariant();
        if (verb is "GET" or "HEAD")
        {
            return ErpCapabilityCatalog.CanAccess(session, area);
        }

        var action = ActionForWrite(Normalize(path));
        if (action is null)
        {
            return WriteActions.Any(candidate => ErpCapabilityCatalog.CanAction(session, area, candidate));
        }

        return ErpCapabilityCatalog.CanAction(session, area, action);
    }

    public static string? ActionForWrite(string? path)
    {
        var text = Normalize(path);
        if (text.Contains("delete", StringComparison.Ordinal))
        {
            return "Delete";
        }

        if (text.Contains("void", StringComparison.Ordinal))
        {
            return "Void";
        }

        if (text.Contains("approv", StringComparison.Ordinal))
        {
            return "Approve";
        }

        if (text.Contains("reject", StringComparison.Ordinal))
        {
            return "Reject";
        }

        if (text.Contains("revers", StringComparison.Ordinal))
        {
            return "Reverse";
        }

        if (text.Contains("export", StringComparison.Ordinal))
        {
            return "Export";
        }

        if (text.Contains("print", StringComparison.Ordinal))
        {
            return "Print";
        }

        if (text.Contains("/post", StringComparison.Ordinal) || text.Contains("confirm", StringComparison.Ordinal))
        {
            return "Post";
        }

        return null;
    }

    private static string? AjaxArea(string text)
    {
        if (ContainsAny(text, "jewel", "jw-"))
        {
            return "jewellery";
        }

        if (ContainsAny(text, "real-estate", "property", "landlord"))
        {
            return "real_estate";
        }

        if (ContainsAny(text, "payroll", "staff", "leave"))
        {
            return "people";
        }

        if (ContainsAny(text, "project", "fit-out", "fitout"))
        {
            return "projects";
        }

        if (ContainsAny(text, "po-", "purchase", "supplier", "rfq", "landed"))
        {
            return "purchasing";
        }

        if (ContainsAny(text, "so-", "invoice", "sales", "quotation"))
        {
            return "sales";
        }

        if (ContainsAny(text, "inv-", "stock", "warehouse"))
        {
            return "inventory";
        }

        if (ContainsAny(text, "gl-", "coa", "vat", "cash", "bank", "journal"))
        {
            return "finance";
        }

        if (ContainsAny(text, "user", "rbac", "grant", "audit"))
        {
            return "administration";
        }

        return null;
    }

    private static bool ContainsAny(string text, params string[] tokens)
        => tokens.Any(token => text.Contains(token, StringComparison.Ordinal));

    private static (string Prefix, string Group)[] BuildPrefixes()
    {
        (string Prefix, string Group)[] rows =
        [
            ("/erp/finance-app", "finance"),
            ("/erp/coa-accounts", "finance"),
            ("/erp/gl-journals", "finance"),
            ("/erp/trial-balance", "finance"),
            ("/erp/period-close", "finance"),
            ("/erp/vat-app", "finance"),
            ("/erp/vat", "finance"),
            ("/erp/withholding", "finance"),
            ("/erp/payables", "finance"),
            ("/erp/receivables", "finance"),
            ("/erp/payment-batches", "finance"),
            ("/erp/cash-accounts", "finance"),
            ("/erp/cash-entries", "finance"),
            ("/erp/bank-reconciliation", "finance"),
            ("/erp/aging-app", "finance"),
            ("/erp/budgets", "finance"),
            ("/erp/fixed-assets", "finance"),
            ("/erp/expense-reports", "finance"),
            ("/erp/corporate-tax", "finance"),
            ("/erp/ifrs", "finance"),
            ("/erp/collections-dunning", "finance"),
            ("/erp/purchases-app", "purchasing"),
            ("/erp/purchase-orders", "purchasing"),
            ("/erp/purchase-requests", "purchasing"),
            ("/erp/rfq", "purchasing"),
            ("/erp/suppliers", "purchasing"),
            ("/erp/three-way-match", "purchasing"),
            ("/erp/landed-cost", "purchasing"),
            ("/erp/sales-orders", "sales"),
            ("/erp/sales-quotations", "sales"),
            ("/erp/crm-opportunities", "sales"),
            ("/erp/delivery-notes", "sales"),
            ("/erp/invoices", "sales"),
            ("/erp/contacts", "sales"),
            ("/erp/inventory-stock", "inventory"),
            ("/erp/stock-transfers", "inventory"),
            ("/erp/stock-movements", "inventory"),
            ("/erp/warehouses", "inventory"),
            ("/erp/warehouse-wms", "inventory"),
            ("/erp/product-info", "inventory"),
            ("/erp/pim/", "inventory"),
            ("/erp/production-overview", "inventory"),
            ("/erp/quality-app", "inventory"),
            ("/erp/order-planning", "inventory"),
            ("/erp/projects-overview", "projects"),
            ("/erp/project-accounting", "projects"),
            ("/erp/fit-out", "projects"),
            ("/erp/contracts", "projects"),
            ("/erp/real-estate", "real_estate"),
            ("/erp/property", "real_estate"),
            ("/erp/landlord", "real_estate"),
            ("/erp/tenancy", "real_estate"),
            ("/erp/jewellery", "jewellery"),
            ("/erp/jw-", "jewellery"),
            ("/erp/payroll", "people"),
            ("/erp/staff", "people"),
            ("/erp/hr-", "people"),
            ("/erp/leave-absence", "people"),
            ("/erp/time-attendance", "people"),
            ("/erp/performance-app", "people"),
            ("/erp/recruitment", "people"),
            ("/erp/users", "administration"),
            ("/erp/user-control", "administration"),
            ("/erp/tenant-config", "administration"),
            ("/erp/workflows", "administration"),
            ("/erp/audit-trail", "administration"),
            ("/erp/navigation-coverage", "administration"),
        ];

        return rows
            .OrderByDescending(row => row.Prefix.Length)
            .ToArray();
    }
}
