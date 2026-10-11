using System.Globalization;
using EcomAE.Platform.Routing;

namespace EcomAE.Platform.Cp;

/// <summary>
/// One customer workspace assembled from CRM and ERP reads. Financial header
/// fields stay unread when the source table is missing. This is not a second
/// customer book and does not change <see cref="CpCrmCustomer360"/>.
/// </summary>
public sealed record CpCrmCustomer360Field(
    string Key,
    string Label,
    string? ValueText,
    bool Sourced,
    string SourceNote,
    string DrillHref);

public sealed record CpCrmCustomer360Line(
    string Caption,
    string Status,
    string? AmountText,
    string DrillHref);

public sealed record CpCrmCustomer360Tab(
    string Key,
    string Label,
    string SourceKind,
    string? SourceNote,
    string ListHref,
    IReadOnlyList<CpCrmCustomer360Line> Lines);

public sealed record CpCrmCustomer360Credit(
    bool Available,
    decimal? CreditLimit,
    decimal? BalanceUsed,
    decimal? AvailableCredit,
    string Currency,
    string Status,
    int? RiskScore);

public sealed record CpCrmCustomer360Workspace(
    long UserId,
    bool Found,
    bool DatabaseConfigured,
    string CustomerName,
    string Email,
    string Phone,
    string AccountNumber,
    string GroupName,
    CpCrmCustomer360 Totals,
    CpCrmCustomer360Credit Credit,
    IReadOnlyList<CpCrmCustomer360Field> Header,
    IReadOnlyList<CpCrmCustomer360Tab> Tabs,
    string? LoadError)
{
    public static CpCrmCustomer360Workspace Empty(long userId, string? error = null)
        => new(
            userId,
            false,
            false,
            "",
            "",
            "",
            "",
            "",
            new CpCrmCustomer360(userId, 0, 0m, 0m, 0, 0, 0m, 0, 0, 0, 0m),
            new CpCrmCustomer360Credit(false, null, null, null, "", "", null),
            [],
            [],
            error);
}

public static class CpCrmCustomer360Composer
{
    public static readonly (string Key, string Label)[] TabOrder =
    [
        ("overview", "Overview"),
        ("contacts", "Contacts"),
        ("leads", "Leads"),
        ("opportunities", "Opportunities"),
        ("activities", "Activities"),
        ("quotations", "Quotations"),
        ("orders", "Orders"),
        ("deliveries", "Deliveries"),
        ("invoices", "Invoices"),
        ("receipts", "Receipts"),
        ("outstanding", "Outstanding"),
        ("credit", "Credit"),
        ("returns", "Returns"),
        ("service", "Service"),
        ("communications", "Communications"),
        ("documents", "Documents"),
        ("profitability", "Profitability"),
    ];

    public static bool IsTab(string? tab)
        => TabOrder.Any(item => string.Equals(item.Key, tab, StringComparison.Ordinal));

    public static string Href(long userId, string? tab = null)
    {
        var path = EcomAeRoutes.ControlPanelCustomer360App;
        var parts = new List<string>();
        if (userId > 0)
        {
            parts.Add("user_id=" + userId.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(tab) && IsTab(tab))
        {
            parts.Add("tab=" + Uri.EscapeDataString(tab.Trim()));
        }

        return parts.Count == 0 ? path : path + "?" + string.Join('&', parts);
    }

    public static CpCrmCustomer360Workspace Compose(
        long userId,
        bool found,
        bool databaseConfigured,
        string customerName,
        string email,
        string phone,
        string accountNumber,
        string groupName,
        CpCrmCustomer360 totals,
        CpCrmCustomer360Credit credit,
        IReadOnlyList<CpCrmLeadRow> leads,
        IReadOnlyList<CpCrmOpportunityRow> opportunities,
        IReadOnlyList<CpCrmActivityRow> activities,
        IReadOnlyList<CpCrmQuoteRow> quotes,
        IReadOnlyList<CpCrmTicketRow> tickets,
        IReadOnlyList<CpCrmOrderRow> orders,
        bool ordersAvailable,
        IReadOnlyList<CpCrmCustomer360Line> invoices,
        bool invoicesAvailable,
        string? loadError)
    {
        var header = new List<CpCrmCustomer360Field>
        {
            Field("customer", "Customer", found ? Display(customerName, email, userId) : null, found, "users", Href(userId)),
            Field("account", "Account number", found ? accountNumber : null, found && accountNumber.Length > 0, "users.user_id", Href(userId)),
            Field("group", "Group", found ? groupName : null, found && groupName.Length > 0, "users group bind", Href(userId, "overview")),
            MoneyField("ytd_sales", "Year-to-date sales", ordersAvailable ? totals.SalesRevenue : null, ordersAvailable, "shop_orders.price_total_wt_vat", "/cp/orders-app"),
            MoneyField("open_pipeline", "Open pipeline", totals.OppOpenValue, true, "epc_crm_opportunities", Href(userId, "opportunities")),
            MoneyField("won", "Won opportunities", totals.OppWonValue, true, "epc_crm_opportunities", Href(userId, "opportunities")),
            MoneyField("quotes", "Quote value", totals.QuoteValue, true, "epc_crm_quotes", Href(userId, "quotations")),
            CountField("orders", "Sales orders", ordersAvailable ? totals.SalesOrders : null, ordersAvailable, "shop_orders", Href(userId, "orders")),
            CountField("tickets", "Open tickets", totals.TicketsOpen, true, "epc_crm_tickets", Href(userId, "service")),
            CreditField("credit_limit", "Credit limit", credit.Available ? credit.CreditLimit : null, credit, "/cp/credit-limits-app"),
            CreditField("available_credit", "Available credit", credit.Available ? credit.AvailableCredit : null, credit, "/cp/credit-limits-app"),
            Field("outstanding", "Outstanding", null, false, "ERP AR is not sourced on this workspace.", "/erp/finance-app"),
            Field("overdue", "Overdue", null, false, "ERP AR ageing is not sourced on this workspace.", "/erp/finance-app"),
            Field("margin", "Margin", null, false, "ERP margin is not sourced on this workspace.", "/erp/finance-app"),
            Field("salesperson", "Salesperson", null, false, "Salesperson is not sourced on this workspace.", Href(userId, "overview")),
            Field("risk", "Risk", credit.Available && credit.RiskScore is int risk ? risk.ToString(CultureInfo.InvariantCulture) : null, credit.Available && credit.RiskScore is not null, "epc_credit_limits.risk_score", "/cp/credit-limits-app"),
            Field("last_interaction", "Last interaction", LastTouch(leads, opportunities, activities, tickets), true, "CRM timestamps", Href(userId, "activities")),
        };

        var tabs = new List<CpCrmCustomer360Tab>
        {
            Tab("overview", "Overview", "crm", "CRM totals plus shop orders when the commerce tables exist.", Href(userId, "overview"), OverviewLines(totals, ordersAvailable, credit)),
            Tab("contacts", "Contacts", "crm", "Shop user contact on this record.", "/erp/contacts-app", ContactLines(found, customerName, email, phone)),
            Tab("leads", "Leads", "crm", "epc_crm_leads for this e-mail.", "/cp/crm-board-app?tab=leads", LeadLines(leads)),
            Tab("opportunities", "Opportunities", "crm", "epc_crm_opportunities.linked_user_id.", "/cp/crm-board-app?tab=pipeline", OppLines(opportunities)),
            Tab("activities", "Activities", "crm", "epc_crm_activities on this customer's CRM records.", "/cp/crm-board-app?tab=activities", ActivityLines(activities)),
            Tab("quotations", "Quotations", "crm", "epc_crm_quotes.customer_user_id.", "/cp/crm-board-app?tab=quotes", QuoteLines(quotes)),
            Tab("orders", "Orders", ordersAvailable ? "erp" : "not-sourced", ordersAvailable ? "shop_orders for this user." : "shop_orders is not in this database.", "/cp/orders-app", OrderLines(orders)),
            Unsourced("deliveries", "Deliveries", "ERP deliveries are not sourced on this workspace.", "/erp/sales-orders-app"),
            Tab("invoices", "Invoices", invoicesAvailable ? "erp" : "not-sourced", invoicesAvailable ? "epc_erp_sales_invoices for this user." : "ERP sales invoices are not sourced.", "/erp/sales-invoices-app", invoices),
            Unsourced("receipts", "Receipts", "ERP receipts are not sourced on this workspace.", "/erp/finance-app"),
            Unsourced("outstanding", "Outstanding", "ERP AR outstanding is not sourced on this workspace.", "/erp/finance-app"),
            Tab("credit", "Credit", credit.Available ? "erp" : "not-sourced", credit.Available ? "epc_credit_limits for this customer." : "epc_credit_limits is unread.", "/cp/credit-limits-app", CreditLines(credit)),
            Unsourced("returns", "Returns", "ERP returns are not sourced on this workspace.", "/cp/returns-rma-app"),
            Tab("service", "Service", "crm", "epc_crm_tickets.customer_user_id.", "/cp/crm-board-app?tab=tickets", TicketLines(tickets)),
            Tab("communications", "Communications", "crm", "CRM activities that are calls, e-mail or notes.", "/cp/crm-board-app?tab=activities", ActivityLines(activities.Where(row => IsCommunication(row.ActivityType)).ToList())),
            Unsourced("documents", "Documents", "Document lineage stays on the ERP document.", "/erp"),
            Unsourced("profitability", "Profitability", "ERP margin is not sourced on this workspace.", "/erp/finance-app"),
        };

        return new CpCrmCustomer360Workspace(
            userId,
            found,
            databaseConfigured,
            customerName,
            email,
            phone,
            accountNumber,
            groupName,
            totals,
            credit,
            header,
            tabs,
            loadError);
    }

    private static CpCrmCustomer360Field Field(string key, string label, string? value, bool sourced, string note, string href)
        => new(key, label, sourced ? value : null, sourced, note, href);

    private static CpCrmCustomer360Field MoneyField(string key, string label, decimal? amount, bool sourced, string note, string href)
        => Field(key, label, amount is null ? null : CpCrmDeskService.Money(amount.Value), sourced, note, href);

    private static CpCrmCustomer360Field CountField(string key, string label, int? count, bool sourced, string note, string href)
        => Field(key, label, count is null ? null : count.Value.ToString(CultureInfo.InvariantCulture), sourced, note, href);

    private static CpCrmCustomer360Field CreditField(string key, string label, decimal? amount, CpCrmCustomer360Credit credit, string href)
        => MoneyField(key, label, amount, credit.Available, credit.Available ? "epc_credit_limits" : "epc_credit_limits is unread.", href);

    private static CpCrmCustomer360Tab Tab(string key, string label, string kind, string note, string href, IReadOnlyList<CpCrmCustomer360Line> lines)
        => new(key, label, kind, note, href, lines);

    private static CpCrmCustomer360Tab Unsourced(string key, string label, string note, string href)
        => new(key, label, "not-sourced", note, href, []);

    private static string Display(string name, string email, long userId)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            return email.Trim();
        }

        return "Customer #" + userId.ToString(CultureInfo.InvariantCulture);
    }

    private static string? LastTouch(
        IReadOnlyList<CpCrmLeadRow> leads,
        IReadOnlyList<CpCrmOpportunityRow> opportunities,
        IReadOnlyList<CpCrmActivityRow> activities,
        IReadOnlyList<CpCrmTicketRow> tickets)
    {
        var unix = leads.Select(row => row.TimeUpdated)
            .Concat(opportunities.Select(row => row.TimeUpdated))
            .Concat(activities.Select(row => row.TimeCreated))
            .Concat(tickets.Select(row => row.TimeUpdated))
            .DefaultIfEmpty(0)
            .Max();
        var text = CpCrmDeskService.DateTime(unix);
        return text.Length == 0 ? null : text;
    }

    private static IReadOnlyList<CpCrmCustomer360Line> OverviewLines(CpCrmCustomer360 totals, bool ordersAvailable, CpCrmCustomer360Credit credit)
    {
        var lines = new List<CpCrmCustomer360Line>
        {
            new("Open opportunities", totals.OppCount.ToString(CultureInfo.InvariantCulture), CpCrmDeskService.Money(totals.OppOpenValue), Href(totals.UserId, "opportunities")),
            new("Quotes", totals.QuoteCount.ToString(CultureInfo.InvariantCulture), CpCrmDeskService.Money(totals.QuoteValue), Href(totals.UserId, "quotations")),
            new("Tickets", totals.TicketsOpen.ToString(CultureInfo.InvariantCulture) + " open / " + totals.TicketsTotal.ToString(CultureInfo.InvariantCulture), null, Href(totals.UserId, "service")),
        };
        if (ordersAvailable)
        {
            lines.Add(new("Shop orders", totals.SalesOrders.ToString(CultureInfo.InvariantCulture), CpCrmDeskService.Money(totals.SalesRevenue), Href(totals.UserId, "orders")));
        }

        if (credit.Available)
        {
            lines.Add(new("Credit", credit.Status, credit.CreditLimit is decimal limit ? CpCrmDeskService.Money(limit) : null, Href(totals.UserId, "credit")));
        }

        return lines;
    }

    private static IReadOnlyList<CpCrmCustomer360Line> ContactLines(bool found, string name, string email, string phone)
    {
        if (!found)
        {
            return [];
        }

        var caption = Display(name, email, 0);
        return [new(caption, phone.Length > 0 ? phone : "contact", null, "/erp/contacts-app")];
    }

    private static IReadOnlyList<CpCrmCustomer360Line> LeadLines(IReadOnlyList<CpCrmLeadRow> leads)
        => leads.Select(row => new CpCrmCustomer360Line(
            row.Company.Length > 0 ? row.Company : row.ContactName,
            row.Status,
            CpCrmDeskService.Money(row.ExpectedValue),
            "/cp/crm-board-app?tab=leads&id=" + row.Id.ToString(CultureInfo.InvariantCulture))).ToList();

    private static IReadOnlyList<CpCrmCustomer360Line> OppLines(IReadOnlyList<CpCrmOpportunityRow> opportunities)
        => opportunities.Select(row => new CpCrmCustomer360Line(
            row.Title,
            row.Stage,
            CpCrmDeskService.Money(row.Amount),
            "/cp/crm-board-app?tab=pipeline&id=" + row.Id.ToString(CultureInfo.InvariantCulture))).ToList();

    private static IReadOnlyList<CpCrmCustomer360Line> ActivityLines(IReadOnlyList<CpCrmActivityRow> activities)
        => activities.Select(row => new CpCrmCustomer360Line(
            row.Notes.Length > 0 ? row.Notes : row.ActivityType,
            row.Done ? "done" : "open",
            null,
            "/cp/crm-board-app?tab=activities")).ToList();

    private static IReadOnlyList<CpCrmCustomer360Line> QuoteLines(IReadOnlyList<CpCrmQuoteRow> quotes)
        => quotes.Select(row => new CpCrmCustomer360Line(
            row.QuoteNumber.Length > 0 ? row.QuoteNumber : "Quote #" + row.Id.ToString(CultureInfo.InvariantCulture),
            row.Status,
            CpCrmDeskService.Money(row.Subtotal),
            "/cp/crm-board-app?tab=quotes")).ToList();

    private static IReadOnlyList<CpCrmCustomer360Line> OrderLines(IReadOnlyList<CpCrmOrderRow> orders)
        => orders.Select(row => new CpCrmCustomer360Line(
            "Order #" + row.Id.ToString(CultureInfo.InvariantCulture),
            row.Paid ? "paid · " + row.StatusName : row.StatusName,
            CpCrmDeskService.Money(row.PriceTotalWtVat),
            "/cp/orders?order_id=" + row.Id.ToString(CultureInfo.InvariantCulture))).ToList();

    private static IReadOnlyList<CpCrmCustomer360Line> TicketLines(IReadOnlyList<CpCrmTicketRow> tickets)
        => tickets.Select(row => new CpCrmCustomer360Line(
            row.Subject.Length > 0 ? row.Subject : "Ticket #" + row.Id.ToString(CultureInfo.InvariantCulture),
            row.Status,
            row.Priority,
            "/cp/crm-board-app?tab=tickets&id=" + row.Id.ToString(CultureInfo.InvariantCulture))).ToList();

    private static IReadOnlyList<CpCrmCustomer360Line> CreditLines(CpCrmCustomer360Credit credit)
    {
        if (!credit.Available)
        {
            return [];
        }

        return
        [
            new("Credit limit", credit.Status, credit.CreditLimit is decimal limit ? CpCrmDeskService.Money(limit) : null, "/cp/credit-limits-app"),
            new("Balance used", credit.Currency, credit.BalanceUsed is decimal used ? CpCrmDeskService.Money(used) : null, "/cp/credit-limits-app"),
            new("Available", credit.Currency, credit.AvailableCredit is decimal open ? CpCrmDeskService.Money(open) : null, "/cp/credit-limits-app"),
        ];
    }

    private static bool IsCommunication(string activityType)
        => activityType is "call" or "email" or "note" or "meeting";
}
