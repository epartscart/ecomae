namespace EcomAE.Platform.Presentation;

/// <summary>
/// Which company master a workspace field must use.
/// Party, item, and branch stay on the masters already in this ERP.
/// Matching-policy options are the ones named in the accounts payable reference.
/// </summary>
public static class ErpD365FieldBinding
{
    public const string Supplier = "supplier";
    public const string Customer = "customer";
    public const string Item = "item";
    public const string Account = "account";
    public const string Project = "project";
    public const string Branch = "business-unit";

    public static readonly string[] InvoiceMatchKinds =
    [
        "Two-way matching",
        "Three-way matching",
        "Invoice totals matching",
        "Charges matching",
    ];

    public sealed record Binding(string PartyKind, string NameKind, string BranchKind, IReadOnlyList<string> KindOptions)
    {
        public bool LinksParty => PartyKind.Length > 0;
        public bool LinksName => NameKind.Length > 0;
        public bool LinksBranch => BranchKind.Length > 0;
    }

    public static Binding For(ErpD365Part3Catalog.Page page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new(PartyKind(page), NameKind(page), BranchKind(page), KindOptions(page));
    }

    public static long CompanyOrDefault(long companyId) => companyId > 0 ? companyId : 1;

    private static string PartyKind(ErpD365Part3Catalog.Page page)
    {
        if (page.Kind == ErpD365Part3Catalog.FormKind.Queue)
        {
            return "";
        }

        var area = page.Area.Trim().ToLowerInvariant();
        var tab = page.Tab.Trim().ToLowerInvariant();
        if (tab.Contains("vendor", StringComparison.Ordinal) || tab.Contains("supplier", StringComparison.Ordinal))
        {
            return Supplier;
        }

        if (tab.Contains("customer", StringComparison.Ordinal))
        {
            return Customer;
        }

        if (area is "ap" or "purchasing" or "landed_cost_area" or "expense" or "transportation" or "logistics")
        {
            return Supplier;
        }

        if (area is "ar" or "sales" or "credit_coll" or "retail")
        {
            return Customer;
        }

        if (area == "rebate_mgmt")
        {
            return Customer;
        }

        if (area is "banking" or "gl" or "finance" or "general_ledger" or "tax" or "budgeting" or "consolidations")
        {
            return Account;
        }

        if (area is "projects" or "cost_acct" or "cost_mgmt")
        {
            return Project;
        }

        if (area is "warehouse" or "inventory_mgmt" or "inventory" or "mhei" or "master_planning_area" or "production" or "pim")
        {
            return page.Kind == ErpD365Part3Catalog.FormKind.Procurement ? Supplier : Branch;
        }

        return page.Kind is ErpD365Part3Catalog.FormKind.Procurement or ErpD365Part3Catalog.FormKind.Order
            ? Supplier
            : "";
    }

    private static string NameKind(ErpD365Part3Catalog.Page page)
    {
        var area = page.Area.Trim().ToLowerInvariant();
        if (page.Kind is ErpD365Part3Catalog.FormKind.Order or ErpD365Part3Catalog.FormKind.Forecast or ErpD365Part3Catalog.FormKind.Procurement)
        {
            return area is "banking" or "gl" or "finance" or "general_ledger" or "tax" or "budgeting" or "consolidations"
                ? Account
                : Item;
        }

        if (area == "projects" && page.Kind == ErpD365Part3Catalog.FormKind.Master)
        {
            return Project;
        }

        if (area == "pim" && page.Kind == ErpD365Part3Catalog.FormKind.Master)
        {
            return Item;
        }

        return "";
    }

    private static string BranchKind(ErpD365Part3Catalog.Page page)
    {
        if (page.Kind is not (ErpD365Part3Catalog.FormKind.Order or ErpD365Part3Catalog.FormKind.Forecast or ErpD365Part3Catalog.FormKind.Procurement))
        {
            return "";
        }

        var area = page.Area.Trim().ToLowerInvariant();
        return area is "warehouse" or "inventory_mgmt" or "inventory" or "purchasing" or "sales" or "landed_cost_area"
            or "transportation" or "logistics" or "master_planning_area" or "production" or "retail" or "pim" or "ap"
            ? Branch
            : "";
    }

    private static IReadOnlyList<string> KindOptions(ErpD365Part3Catalog.Page page)
    {
        var area = page.Area.Trim().ToLowerInvariant();
        var tab = page.Tab.Trim().ToLowerInvariant();
        return area == "ap" && tab.Contains("match", StringComparison.Ordinal)
            ? InvoiceMatchKinds
            : [];
    }
}
