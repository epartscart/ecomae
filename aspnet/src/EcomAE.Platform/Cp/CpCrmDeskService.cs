using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Read side of the PHP CRM (<c>epc_crm_helpers.php</c>, <c>epc_crm_modules.php</c>,
/// <c>epc_erp_crm_advanced.php</c>) that <c>crm_main.php</c>, <c>crm_enterprise_panels.php</c>
/// and <c>crm_tabs_extended.php</c> render. Schema-ensure stays Classic.
/// </summary>
public interface ICpCrmDeskService
{
    Task<CpCrmDesk> LoadAsync(string tab, string leadStatus, CancellationToken cancellationToken = default);

    Task<CpCrmLeadRow?> GetLeadAsync(long id, CancellationToken cancellationToken = default);

    Task<CpCrmOpportunityRow?> GetOpportunityAsync(long id, CancellationToken cancellationToken = default);

    Task<CpCrmQuoteDetail?> GetQuoteAsync(long id, CancellationToken cancellationToken = default);

    Task<CpCrmTicketDetail?> GetTicketAsync(long id, CancellationToken cancellationToken = default);

    Task<CpCrmProjectDetail?> GetProjectAsync(long id, CancellationToken cancellationToken = default);

    Task<CpCrmTimeline?> TimelineAsync(string entityType, long entityId, CancellationToken cancellationToken = default);

    Task<CpCrmCustomer360> Customer360Async(long userId, CancellationToken cancellationToken = default);

    Task<CpCrmWonHint> WonHintAsync(long opportunityId, CancellationToken cancellationToken = default);

    Task<CpCrmLeadScore> ScoreLeadAsync(long leadId, CancellationToken cancellationToken = default);

    Task<CpCrmDashboard> DashboardAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, IReadOnlyList<CpCrmOpportunityRow>>> PipelineAsync(CancellationToken cancellationToken = default);
}

public sealed record CpCrmLeadScore(int Score, string Band, IReadOnlyList<string> Reasons)
{
    public static readonly CpCrmLeadScore Empty = new(0, "cold", []);
}

public sealed record CpCrmLeadRow(
    long Id,
    string Company,
    string ContactName,
    string Email,
    string Phone,
    string Source,
    string Status,
    long OwnerUserId,
    decimal ExpectedValue,
    string Notes,
    long TimeCreated,
    long TimeUpdated)
{
    public CpCrmLeadScore Score { get; init; } = CpCrmLeadScore.Empty;
}

public sealed record CpCrmOpportunityRow(
    long Id,
    long LeadId,
    string Title,
    string Stage,
    decimal Amount,
    int Probability,
    long CloseDate,
    long OwnerUserId,
    long LinkedUserId,
    string Notes,
    long TimeCreated,
    long TimeUpdated,
    string LeadCompany);

public sealed record CpCrmActivityRow(
    long Id,
    string ActivityType,
    string RelatedType,
    long RelatedId,
    long DueDate,
    bool Done,
    long OwnerUserId,
    string Notes,
    long TimeCreated)
{
    public bool IsOverdue(long now) => !Done && DueDate > 0 && DueDate < now;
}

public sealed record CpCrmQuoteRow(
    long Id,
    long OpportunityId,
    long LeadId,
    long CustomerUserId,
    string QuoteNumber,
    string Status,
    string CurrencyCode,
    decimal Subtotal,
    long ShopOrderId,
    string Notes,
    long TimeCreated,
    long TimeUpdated,
    string OppTitle);

public sealed record CpCrmQuoteLine(long Id, string Description, decimal Qty, decimal UnitPrice, int SortOrder)
{
    public decimal LineTotal => Math.Round(Qty * UnitPrice, 2, MidpointRounding.AwayFromZero);
}

public sealed record CpCrmQuoteDetail(CpCrmQuoteRow Quote, IReadOnlyList<CpCrmQuoteLine> Lines);

public sealed record CpCrmTicketRow(
    long Id,
    long CustomerUserId,
    long OrderId,
    string Subject,
    string Status,
    string Priority,
    long AssignedUserId,
    long TimeCreated,
    long TimeUpdated,
    string CustomerEmail);

public sealed record CpCrmTicketMessage(long Id, long AuthorUserId, bool IsStaff, string Body, long TimeCreated);

public sealed record CpCrmTicketDetail(CpCrmTicketRow Ticket, IReadOnlyList<CpCrmTicketMessage> Messages);

public sealed record CpCrmProjectRow(
    long Id,
    string Name,
    long OpportunityId,
    long OrderId,
    string Status,
    int ProgressPct,
    long StartDate,
    long EndDate,
    long OwnerUserId,
    string Notes,
    long TimeUpdated,
    string OppTitle);

public sealed record CpCrmProjectTask(long Id, string Title, string Status, int ProgressPct, decimal HoursEst, decimal HoursLogged, long DueDate);

public sealed record CpCrmProjectDetail(CpCrmProjectRow Project, IReadOnlyList<CpCrmProjectTask> Tasks);

public sealed record CpCrmContractRow(
    long Id,
    long CustomerUserId,
    string Title,
    decimal Amount,
    string CurrencyCode,
    string BillingInterval,
    long NextBillingDate,
    string Status,
    string Notes,
    string CustomerEmail);

public sealed record CpCrmExpenseRow(
    long Id,
    long EmployeeUserId,
    decimal Amount,
    string CurrencyCode,
    string Category,
    string Status,
    string ReceiptNote,
    long CashEntryId,
    long TimeUpdated);

public sealed record CpCrmStageBucket(int Count, decimal Total, decimal Weighted);

/// <summary>PHP <c>epc_crm_dashboard_extended</c>.</summary>
public sealed record CpCrmDashboard(
    int LeadsTotal,
    int LeadsNew,
    int OpportunitiesOpen,
    decimal PipelineWeighted,
    decimal WonMtd,
    int ActivitiesDue,
    IReadOnlyDictionary<string, CpCrmStageBucket> ByStage,
    int QuotesOpen,
    int TicketsOpen,
    int ProjectsActive,
    int ContractsDue30d,
    int ExpensesPending)
{
    public static readonly CpCrmDashboard Empty = new(0, 0, 0, 0m, 0m, 0, new Dictionary<string, CpCrmStageBucket>(StringComparer.Ordinal), 0, 0, 0, 0, 0);
}

/// <summary>PHP <c>epc_crm_adv_pipeline_forecast</c>.</summary>
public sealed record CpCrmForecast(
    int OpenCount,
    decimal OpenValue,
    decimal WeightedValue,
    decimal WonValue,
    decimal LostValue,
    decimal WinRate,
    IReadOnlyDictionary<string, CpCrmStageBucket> ByStage)
{
    public static readonly CpCrmForecast Empty = new(0, 0m, 0m, 0m, 0m, 0m, new Dictionary<string, CpCrmStageBucket>(StringComparer.Ordinal));
}

/// <summary>PHP <c>epc_crm_adv_conversion_funnel</c>.</summary>
public sealed record CpCrmFunnel(int Leads, int Qualified, int Opportunities, int Proposals, int Won, decimal LeadToOppPct, decimal OppToWonPct)
{
    public static readonly CpCrmFunnel Empty = new(0, 0, 0, 0, 0, 0m, 0m);
}

public sealed record CpCrmLeadSource(string Source, int Count, decimal ExpectedValue, int QualifiedCount);

public sealed record CpCrmAccountRow(
    string Name,
    string Email,
    string Phone,
    int Leads,
    decimal ExpectedValue,
    int Opportunities,
    decimal OpenPipeline,
    decimal WonValue,
    long LinkedUserId,
    long LastTouch,
    IReadOnlyList<long> LeadIds);

public sealed record CpCrmLeadBands(int Hot, int Warm, int Cold, int Total);

/// <summary>PHP <c>epc_crm_adv_dashboard</c>.</summary>
public sealed record CpCrmIntelligence(
    CpCrmLeadBands LeadBands,
    IReadOnlyList<CpCrmLeadRow> TopLeads,
    CpCrmForecast Forecast,
    IReadOnlyList<CpCrmActivityRow> NextActions,
    CpCrmFunnel Funnel,
    IReadOnlyList<CpCrmLeadSource> Sources)
{
    public static readonly CpCrmIntelligence Empty = new(new CpCrmLeadBands(0, 0, 0, 0), [], CpCrmForecast.Empty, [], CpCrmFunnel.Empty, []);
}

public sealed record CpCrmOrderRow(long Id, long Time, bool Paid, decimal PriceTotalWtVat, string StatusName);

public sealed record CpCrmTimeline(
    string EntityType,
    long EntityId,
    string EntityCaption,
    IReadOnlyList<CpCrmActivityRow> Activities,
    IReadOnlyList<CpCrmQuoteRow> Quotes,
    long LinkedUserId,
    IReadOnlyList<CpCrmOrderRow> Orders,
    bool HasCommerce);

public sealed record CpCrmCustomer360(
    long UserId,
    int OppCount,
    decimal OppOpenValue,
    decimal OppWonValue,
    int QuoteCount,
    int QuoteAccepted,
    decimal QuoteValue,
    int TicketsOpen,
    int TicketsTotal,
    int SalesOrders,
    decimal SalesRevenue);

public sealed record CpCrmWonHint(string Hint, long LinkedUserId, decimal Amount, string Title);

public sealed record CpCrmDesk(
    bool DbAvailable,
    string LoadError,
    string CurrencyCode,
    CpCrmDashboard Dashboard,
    CpCrmIntelligence Intelligence,
    IReadOnlyDictionary<string, IReadOnlyList<CpCrmOpportunityRow>> Pipeline,
    IReadOnlyList<CpCrmLeadRow> Leads,
    IReadOnlyList<CpCrmOpportunityRow> Opportunities,
    IReadOnlyList<CpCrmAccountRow> Accounts,
    IReadOnlyList<CpCrmQuoteRow> Quotes,
    IReadOnlyList<CpCrmActivityRow> Activities,
    IReadOnlyList<CpCrmTicketRow> Tickets,
    IReadOnlyList<CpCrmProjectRow> Projects,
    IReadOnlyList<CpCrmContractRow> Contracts,
    IReadOnlyList<CpCrmContractRow> ContractsDue,
    IReadOnlyList<CpCrmExpenseRow> Expenses)
{
    public static CpCrmDesk Unavailable(string error, string currency) =>
        new(false, error, currency, CpCrmDashboard.Empty, CpCrmIntelligence.Empty,
            new Dictionary<string, IReadOnlyList<CpCrmOpportunityRow>>(StringComparer.Ordinal),
            [], [], [], [], [], [], [], [], [], []);
}

public sealed class CpCrmDeskService : ICpCrmDeskService
{
    /// <summary>PHP <c>$allTabs</c> in <c>crm_main.php</c>, in order.</summary>
    public static readonly IReadOnlyList<KeyValuePair<string, string>> Tabs =
    [
        new("dashboard", "Command Centre"),
        new("intelligence", "Intelligence"),
        new("pipeline", "Pipeline"),
        new("leads", "Leads"),
        new("opportunities", "Opps"),
        new("accounts", "Accounts"),
        new("quotes", "Quotes"),
        new("activities", "Activities"),
        new("tickets", "Support"),
        new("projects", "Projects"),
        new("contracts", "Contracts"),
        new("expenses", "Expenses"),
    ];

    public static readonly IReadOnlyList<KeyValuePair<string, string>> LeadStatuses =
    [
        new("new", "New"),
        new("contacted", "Contacted"),
        new("qualified", "Qualified"),
        new("unqualified", "Unqualified"),
        new("converted", "Converted"),
    ];

    public static readonly IReadOnlyList<KeyValuePair<string, string>> OpportunityStages =
    [
        new("prospect", "Prospect"),
        new("qualified", "Qualified"),
        new("proposal", "Proposal"),
        new("negotiation", "Negotiation"),
        new("won", "Won"),
        new("lost", "Lost"),
    ];

    public static readonly IReadOnlyList<KeyValuePair<string, string>> ActivityTypes =
    [
        new("call", "Call"),
        new("email", "Email"),
        new("meeting", "Meeting"),
        new("note", "Note"),
        new("task", "Task"),
    ];

    public static readonly IReadOnlyList<KeyValuePair<string, string>> QuoteStatuses =
    [
        new("draft", "Draft"), new("sent", "Sent"), new("accepted", "Accepted"), new("rejected", "Rejected"),
    ];

    public static readonly IReadOnlyList<KeyValuePair<string, string>> TicketStatuses =
    [
        new("open", "Open"), new("pending", "Pending"), new("resolved", "Resolved"), new("closed", "Closed"),
    ];

    public static readonly IReadOnlyList<KeyValuePair<string, string>> TicketPriorities =
    [
        new("low", "Low"), new("normal", "Normal"), new("high", "High"), new("urgent", "Urgent"),
    ];

    public static readonly IReadOnlyList<KeyValuePair<string, string>> ProjectStatuses =
    [
        new("planned", "Planned"), new("active", "Active"), new("on_hold", "On hold"), new("done", "Done"), new("cancelled", "Cancelled"),
    ];

    public static readonly IReadOnlyList<KeyValuePair<string, string>> ContractStatuses =
    [
        new("draft", "Draft"), new("active", "Active"), new("paused", "Paused"), new("ended", "Ended"),
    ];

    public static readonly IReadOnlyList<KeyValuePair<string, string>> ExpenseStatuses =
    [
        new("draft", "Draft"), new("submitted", "Submitted"), new("approved", "Approved"), new("rejected", "Rejected"), new("paid", "Paid"),
    ];

    public static readonly IReadOnlyList<string> BillingIntervals = ["monthly", "quarterly", "yearly", "once"];

    public static string TabLabel(string key) => Label(Tabs, key);

    public static string Label(IReadOnlyList<KeyValuePair<string, string>> map, string key)
    {
        foreach (var kv in map)
        {
            if (kv.Key == key)
            {
                return kv.Value;
            }
        }

        return key;
    }

    public static bool IsTab(string? tab)
    {
        if (string.IsNullOrEmpty(tab))
        {
            return false;
        }

        foreach (var kv in Tabs)
        {
            if (kv.Key == tab)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>PHP <c>epc_crm_money</c>: two decimals, thousands separator.</summary>
    public static string Money(decimal n) => n.ToString("#,##0.00", CultureInfo.InvariantCulture);

    public static string Date(long unix) => unix > 0
        ? DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        : "";

    public static string DateTime(long unix) => unix > 0
        ? DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
        : "";

    /// <summary>PHP <c>epc_crm_adv_score_weights</c> defaults.</summary>
    public sealed record ScoreWeights(int StatusNew = 5, int StatusContacted = 20, int StatusQualified = 45, int HasEmail = 10, int HasPhone = 10, int ValueBand = 20, int ActivityEach = 5, int ActivityCap = 15);

    /// <summary>Pure port of PHP <c>epc_crm_adv_score_lead</c>.</summary>
    public static CpCrmLeadScore ScoreLead(string status, string email, string phone, decimal expectedValue, int activityCount, ScoreWeights? weights = null)
    {
        var w = weights ?? new ScoreWeights();
        var score = 0;
        var reasons = new List<string>();
        if (status is "qualified" or "converted")
        {
            score += w.StatusQualified;
            reasons.Add("Qualified status (+" + w.StatusQualified.ToString(CultureInfo.InvariantCulture) + ")");
        }
        else if (status == "contacted")
        {
            score += w.StatusContacted;
            reasons.Add("Contacted (+" + w.StatusContacted.ToString(CultureInfo.InvariantCulture) + ")");
        }
        else
        {
            score += w.StatusNew;
            reasons.Add("New lead (+" + w.StatusNew.ToString(CultureInfo.InvariantCulture) + ")");
        }

        if (!string.IsNullOrEmpty(email))
        {
            score += w.HasEmail;
            reasons.Add("Has email (+" + w.HasEmail.ToString(CultureInfo.InvariantCulture) + ")");
        }

        if (!string.IsNullOrEmpty(phone))
        {
            score += w.HasPhone;
            reasons.Add("Has phone (+" + w.HasPhone.ToString(CultureInfo.InvariantCulture) + ")");
        }

        if (expectedValue > 0)
        {
            var band = expectedValue >= 50000m ? 1.0 : (expectedValue >= 10000m ? 0.66 : 0.33);
            var add = (int)Math.Round(w.ValueBand * band, MidpointRounding.AwayFromZero);
            score += add;
            reasons.Add("Expected value (+" + add.ToString(CultureInfo.InvariantCulture) + ")");
        }

        if (activityCount > 0)
        {
            var add = Math.Min(w.ActivityCap, activityCount * w.ActivityEach);
            score += add;
            reasons.Add(activityCount.ToString(CultureInfo.InvariantCulture) + " activities (+" + add.ToString(CultureInfo.InvariantCulture) + ")");
        }

        score = Math.Clamp(score, 0, 100);
        var bandName = score >= 70 ? "hot" : (score >= 40 ? "warm" : "cold");
        return new CpCrmLeadScore(score, bandName, reasons);
    }

    /// <summary>PHP <c>epc_crm_adv_pipeline_forecast</c> aggregation over stage rows.</summary>
    public static CpCrmForecast Forecast(IEnumerable<(string Stage, int Count, decimal Amount, decimal Weighted)> rows)
    {
        var byStage = new Dictionary<string, CpCrmStageBucket>(StringComparer.Ordinal);
        var openCount = 0;
        decimal openValue = 0, weighted = 0, won = 0, lost = 0;
        var wonCount = 0;
        var closedCount = 0;
        foreach (var (stage, count, amount, w) in rows)
        {
            byStage[stage] = new CpCrmStageBucket(count, amount, w);
            if (stage == "won")
            {
                won += amount;
                wonCount += count;
                closedCount += count;
            }
            else if (stage == "lost")
            {
                lost += amount;
                closedCount += count;
            }
            else
            {
                openCount += count;
                openValue += amount;
                weighted += w;
            }
        }

        var winRate = closedCount > 0 ? Math.Round((decimal)wonCount / closedCount * 100m, 1, MidpointRounding.AwayFromZero) : 0m;
        return new CpCrmForecast(openCount, openValue, weighted, won, lost, winRate, byStage);
    }

    /// <summary>PHP <c>epc_crm_adv_conversion_funnel</c> percentages.</summary>
    public static CpCrmFunnel Funnel(int leads, int qualified, int opportunities, int proposals, int won)
    {
        var l2o = leads > 0 ? Math.Round((decimal)opportunities / leads * 100m, 1, MidpointRounding.AwayFromZero) : 0m;
        var o2w = opportunities > 0 ? Math.Round((decimal)won / opportunities * 100m, 1, MidpointRounding.AwayFromZero) : 0m;
        return new CpCrmFunnel(leads, qualified, opportunities, proposals, won, l2o, o2w);
    }

    /// <summary>PHP <c>epc_crm_adv_accounts</c> rollup: leads keyed by company (else email, else lead-id), then opportunities.</summary>
    public static IReadOnlyList<CpCrmAccountRow> RollupAccounts(IEnumerable<CpCrmLeadRow> leads, IEnumerable<CpCrmOpportunityRow> opportunities, IReadOnlyDictionary<long, string> leadEmails, int limit)
    {
        var map = new Dictionary<string, AccountAcc>(StringComparer.Ordinal);
        foreach (var l in leads)
        {
            var key = (l.Company.Length > 0 ? l.Company : (l.Email.Length > 0 ? l.Email : "lead-" + l.Id.ToString(CultureInfo.InvariantCulture))).Trim().ToLowerInvariant();
            if (key.Length == 0)
            {
                continue;
            }

            if (!map.TryGetValue(key, out var acc))
            {
                acc = new AccountAcc
                {
                    Name = l.Company.Length > 0 ? l.Company : (l.ContactName.Length > 0 ? l.ContactName : l.Email),
                    Email = l.Email,
                    Phone = l.Phone,
                    LastTouch = l.TimeUpdated,
                };
                map[key] = acc;
            }

            acc.Leads++;
            acc.ExpectedValue += l.ExpectedValue;
            acc.LeadIds.Add(l.Id);
            acc.LastTouch = Math.Max(acc.LastTouch, l.TimeUpdated);
            if (acc.Email.Length == 0 && l.Email.Length > 0)
            {
                acc.Email = l.Email;
            }
        }

        foreach (var o in opportunities)
        {
            var company = o.LeadCompany.Trim();
            var key = (company.Length > 0 ? company : "opp-" + o.Id.ToString(CultureInfo.InvariantCulture)).ToLowerInvariant();
            if (!map.TryGetValue(key, out var acc))
            {
                acc = new AccountAcc
                {
                    Name = company.Length > 0 ? company : "Opportunity #" + o.Id.ToString(CultureInfo.InvariantCulture),
                    Email = leadEmails.TryGetValue(o.LeadId, out var em) ? em : "",
                    Phone = "",
                    LinkedUserId = o.LinkedUserId,
                    LastTouch = o.TimeUpdated,
                };
                map[key] = acc;
            }

            acc.Opportunities++;
            if (o.Stage == "won")
            {
                acc.WonValue += o.Amount;
            }
            else if (o.Stage != "lost")
            {
                acc.OpenPipeline += o.Amount;
            }

            if (o.LinkedUserId > 0)
            {
                acc.LinkedUserId = o.LinkedUserId;
            }

            acc.LastTouch = Math.Max(acc.LastTouch, o.TimeUpdated);
        }

        var rows = map.Values.ToList();
        rows.Sort((a, b) =>
        {
            var sa = a.OpenPipeline + a.WonValue + a.ExpectedValue;
            var sb = b.OpenPipeline + b.WonValue + b.ExpectedValue;
            return sa == sb ? b.LastTouch.CompareTo(a.LastTouch) : sb.CompareTo(sa);
        });
        return rows.Take(Math.Clamp(limit, 1, 300))
            .Select(a => new CpCrmAccountRow(a.Name, a.Email, a.Phone, a.Leads, a.ExpectedValue, a.Opportunities, a.OpenPipeline, a.WonValue, a.LinkedUserId, a.LastTouch, a.LeadIds))
            .ToList();
    }

    private sealed class AccountAcc
    {
        public string Name = "";
        public string Email = "";
        public string Phone = "";
        public int Leads;
        public decimal ExpectedValue;
        public int Opportunities;
        public decimal OpenPipeline;
        public decimal WonValue;
        public long LinkedUserId;
        public long LastTouch;
        public readonly List<long> LeadIds = [];
    }

    private static readonly string[] OpenStages = ["prospect", "qualified", "proposal", "negotiation"];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly ICpCurrencyLiveRatesService _currency;

    public CpCrmDeskService(IErpWriteConnectionFactory connections, ICpCurrencyLiveRatesService currency)
    {
        _connections = connections;
        _currency = currency;
    }

    private async Task<string> CurrencyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var (_, alpha) = await _currency.GetMainCurrencyAsync(cancellationToken).ConfigureAwait(false);
            return alpha.Length > 0 ? alpha : "AED";
        }
        catch (DbException)
        {
            return "AED";
        }
    }

    public async Task<CpCrmDesk> LoadAsync(string tab, string leadStatus, CancellationToken cancellationToken = default)
    {
        var currency = await CurrencyAsync(cancellationToken).ConfigureAwait(false);
        if (!_connections.IsConfigured)
        {
            return CpCrmDesk.Unavailable("No database", currency);
        }

        tab = IsTab(tab) ? tab : "dashboard";
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var dashboard = await DashboardCoreAsync(connection, cancellationToken).ConfigureAwait(false);
            var intelligence = tab is "dashboard" or "intelligence"
                ? await IntelligenceCoreAsync(connection, cancellationToken).ConfigureAwait(false)
                : CpCrmIntelligence.Empty;

            IReadOnlyDictionary<string, IReadOnlyList<CpCrmOpportunityRow>> pipeline = new Dictionary<string, IReadOnlyList<CpCrmOpportunityRow>>(StringComparer.Ordinal);
            IReadOnlyList<CpCrmOpportunityRow> opps = [];
            if (tab is "pipeline" or "dashboard")
            {
                pipeline = await PipelineCoreAsync(connection, cancellationToken).ConfigureAwait(false);
            }

            if (tab is "opportunities" or "accounts")
            {
                opps = await ListOpportunitiesCoreAsync(connection, null, tab == "accounts" ? 500 : 200, cancellationToken).ConfigureAwait(false);
            }

            IReadOnlyList<CpCrmLeadRow> leads = [];
            if (tab is "leads" or "accounts")
            {
                leads = await ListLeadsCoreAsync(connection, tab == "leads" ? leadStatus : "", tab == "accounts" ? 500 : 200, cancellationToken).ConfigureAwait(false);
                if (tab == "leads")
                {
                    leads = await ScoreAllAsync(connection, leads, cancellationToken).ConfigureAwait(false);
                }
            }

            IReadOnlyList<CpCrmAccountRow> accounts = [];
            if (tab == "accounts")
            {
                var emails = leads.ToDictionary(l => l.Id, l => l.Email);
                accounts = RollupAccounts(leads, opps, emails, 100);
            }

            return new CpCrmDesk(
                true,
                "",
                currency,
                dashboard,
                intelligence,
                pipeline,
                leads,
                opps,
                accounts,
                tab == "quotes" ? await ListQuotesCoreAsync(connection, 100, cancellationToken).ConfigureAwait(false) : [],
                tab == "activities" ? await ListActivitiesCoreAsync(connection, "", 0, 100, cancellationToken).ConfigureAwait(false) : [],
                tab == "tickets" ? await ListTicketsCoreAsync(connection, 100, cancellationToken).ConfigureAwait(false) : [],
                tab == "projects" ? await ListProjectsCoreAsync(connection, 100, cancellationToken).ConfigureAwait(false) : [],
                tab == "contracts" ? await ListContractsCoreAsync(connection, 100, false, cancellationToken).ConfigureAwait(false) : [],
                tab == "contracts" ? await ListContractsCoreAsync(connection, 500, true, cancellationToken).ConfigureAwait(false) : [],
                tab == "expenses" ? await ListExpensesCoreAsync(connection, 100, cancellationToken).ConfigureAwait(false) : []);
        }
        catch (DbException ex)
        {
            return CpCrmDesk.Unavailable(ex.Message, currency);
        }
    }

    public async Task<CpCrmLeadRow?> GetLeadAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            return await GetLeadCoreAsync(connection, id, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return null;
        }
    }

    public async Task<CpCrmOpportunityRow?> GetOpportunityAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            return await GetOpportunityCoreAsync(connection, id, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return null;
        }
    }

    public async Task<CpCrmQuoteDetail?> GetQuoteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            return await GetQuoteCoreAsync(connection, id, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return null;
        }
    }

    public async Task<CpCrmTicketDetail?> GetTicketAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            CpCrmTicketRow? ticket = null;
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ErpDb.Positional(TicketSelect + " WHERE t.`id` = ? AND t.`active` = 1 LIMIT 1");
                ErpDb.AddParameters(c, id);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    ticket = ReadTicket(r);
                }
            }

            if (ticket is null)
            {
                return null;
            }

            var messages = new List<CpCrmTicketMessage>();
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ErpDb.Positional("SELECT `id`, IFNULL(`author_user_id`,0), IFNULL(`is_staff`,0), IFNULL(`body`,''), IFNULL(`time_created`,0) FROM `epc_crm_ticket_messages` WHERE `ticket_id` = ? ORDER BY `time_created` ASC");
                ErpDb.AddParameters(c, id);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    messages.Add(new CpCrmTicketMessage(L(r, 0), L(r, 1), L(r, 2) != 0, S(r, 3), L(r, 4)));
                }
            }

            return new CpCrmTicketDetail(ticket, messages);
        }
        catch (DbException)
        {
            return null;
        }
    }

    public async Task<CpCrmProjectDetail?> GetProjectAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            CpCrmProjectRow? project = null;
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ErpDb.Positional(ProjectSelect + " WHERE p.`id` = ? AND p.`active` = 1 LIMIT 1");
                ErpDb.AddParameters(c, id);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    project = ReadProject(r);
                }
            }

            if (project is null)
            {
                return null;
            }

            var tasks = new List<CpCrmProjectTask>();
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ErpDb.Positional("SELECT `id`, IFNULL(`title`,''), IFNULL(`status`,''), IFNULL(`progress_pct`,0), IFNULL(`hours_est`,0), IFNULL(`hours_logged`,0), IFNULL(`due_date`,0) FROM `epc_crm_project_tasks` WHERE `project_id` = ? ORDER BY `sort_order`, `id`");
                ErpDb.AddParameters(c, id);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    tasks.Add(new CpCrmProjectTask(L(r, 0), S(r, 1), S(r, 2), (int)L(r, 3), D(r, 4), D(r, 5), L(r, 6)));
                }
            }

            return new CpCrmProjectDetail(project, tasks);
        }
        catch (DbException)
        {
            return null;
        }
    }

    public async Task<CpCrmTimeline?> TimelineAsync(string entityType, long entityId, CancellationToken cancellationToken = default)
    {
        if (entityId <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        entityType = entityType == "opportunity" ? "opportunity" : "lead";
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            string caption;
            long leadId = 0;
            long linkedUser = 0;
            var email = "";
            if (entityType == "opportunity")
            {
                var opp = await GetOpportunityCoreAsync(connection, entityId, cancellationToken).ConfigureAwait(false);
                if (opp is null)
                {
                    return null;
                }

                caption = opp.Title;
                leadId = opp.LeadId;
                linkedUser = opp.LinkedUserId;
                if (linkedUser <= 0 && leadId > 0)
                {
                    var lead = await GetLeadCoreAsync(connection, leadId, cancellationToken).ConfigureAwait(false);
                    email = lead?.Email ?? "";
                }
            }
            else
            {
                var lead = await GetLeadCoreAsync(connection, entityId, cancellationToken).ConfigureAwait(false);
                if (lead is null)
                {
                    return null;
                }

                caption = lead.Company;
                email = lead.Email;
            }

            var activities = new List<CpCrmActivityRow>(await ListActivitiesCoreAsync(connection, entityType, entityId, 100, cancellationToken).ConfigureAwait(false));
            if (entityType == "opportunity" && leadId > 0)
            {
                activities.AddRange(await ListActivitiesCoreAsync(connection, "lead", leadId, 100, cancellationToken).ConfigureAwait(false));
            }

            activities.Sort((a, b) => b.DueDate.CompareTo(a.DueDate));

            var quotes = await ListQuotesForEntityCoreAsync(connection, entityType, entityId, cancellationToken).ConfigureAwait(false);
            if (linkedUser <= 0 && email.Length > 0)
            {
                linkedUser = await UserIdByEmailAsync(connection, email, cancellationToken).ConfigureAwait(false);
            }

            var orders = await RecentOrdersCoreAsync(connection, linkedUser, 10, cancellationToken).ConfigureAwait(false);
            return new CpCrmTimeline(entityType, entityId, caption, activities, quotes, linkedUser, orders.Rows, orders.HasCommerce);
        }
        catch (DbException)
        {
            return null;
        }
    }

    public async Task<CpCrmCustomer360> Customer360Async(long userId, CancellationToken cancellationToken = default)
    {
        var empty = new CpCrmCustomer360(userId, 0, 0m, 0m, 0, 0, 0m, 0, 0, 0, 0m);
        if (userId <= 0 || !_connections.IsConfigured)
        {
            return empty;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var oppCount = 0;
            decimal oppOpen = 0, oppWon = 0;
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ErpDb.Positional("SELECT COUNT(*), IFNULL(SUM(CASE WHEN `stage` NOT IN ('won','lost') THEN `amount` ELSE 0 END),0), IFNULL(SUM(CASE WHEN `stage` = 'won' THEN `amount` ELSE 0 END),0) FROM `epc_crm_opportunities` WHERE `active` = 1 AND `linked_user_id` = ?");
                ErpDb.AddParameters(c, userId);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    oppCount = (int)L(r, 0);
                    oppOpen = D(r, 1);
                    oppWon = D(r, 2);
                }
            }

            var qCount = 0;
            var qAccepted = 0;
            decimal qValue = 0;
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ErpDb.Positional("SELECT COUNT(*), IFNULL(SUM(CASE WHEN `status` = 'accepted' THEN 1 ELSE 0 END),0), IFNULL(SUM(`subtotal`),0) FROM `epc_crm_quotes` WHERE `active` = 1 AND `customer_user_id` = ?");
                ErpDb.AddParameters(c, userId);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    qCount = (int)L(r, 0);
                    qAccepted = (int)L(r, 1);
                    qValue = D(r, 2);
                }
            }

            var tOpen = 0;
            var tTotal = 0;
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ErpDb.Positional("SELECT COUNT(*), IFNULL(SUM(CASE WHEN `status` NOT IN ('closed','resolved') THEN 1 ELSE 0 END),0) FROM `epc_crm_tickets` WHERE `customer_user_id` = ?");
                ErpDb.AddParameters(c, userId);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    tTotal = (int)L(r, 0);
                    tOpen = (int)L(r, 1);
                }
            }

            var sOrders = 0;
            decimal sRevenue = 0;
            try
            {
                await using var c = connection.CreateCommand();
                c.CommandText = ErpDb.Positional("SELECT COUNT(*), IFNULL(SUM(`price_total_wt_vat`),0) FROM `shop_orders` WHERE `user_id` = ? AND `successfully_created` = 1");
                ErpDb.AddParameters(c, userId);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    sOrders = (int)L(r, 0);
                    sRevenue = D(r, 1);
                }
            }
            catch (DbException)
            {
                // ERP-only tenants have no shop_orders.
            }

            return new CpCrmCustomer360(userId, oppCount, oppOpen, oppWon, qCount, qAccepted, qValue, tOpen, tTotal, sOrders, sRevenue);
        }
        catch (DbException)
        {
            return empty;
        }
    }

    public async Task<CpCrmWonHint> WonHintAsync(long opportunityId, CancellationToken cancellationToken = default)
    {
        var none = new CpCrmWonHint("", 0, 0m, "");
        if (opportunityId <= 0 || !_connections.IsConfigured)
        {
            return none;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var opp = await GetOpportunityCoreAsync(connection, opportunityId, cancellationToken).ConfigureAwait(false);
            if (opp is null || opp.Stage != "won")
            {
                return none;
            }

            var uid = opp.LinkedUserId;
            var hint = "Create a shop order for this customer";
            if (uid > 0)
            {
                hint += " (user #" + uid.ToString(CultureInfo.InvariantCulture) + ")";
            }
            else if (opp.LeadId > 0)
            {
                var lead = await GetLeadCoreAsync(connection, opp.LeadId, cancellationToken).ConfigureAwait(false);
                if (lead is not null && lead.Email.Length > 0)
                {
                    uid = await UserIdByEmailAsync(connection, lead.Email, cancellationToken).ConfigureAwait(false);
                    hint += uid > 0
                        ? " — match found: user #" + uid.ToString(CultureInfo.InvariantCulture)
                        : " — register customer with email " + lead.Email;
                }
            }

            var currency = await CurrencyAsync(cancellationToken).ConfigureAwait(false);
            hint += ". Amount: " + Money(opp.Amount) + " " + currency + ".";
            return new CpCrmWonHint(hint, uid, opp.Amount, opp.Title);
        }
        catch (DbException)
        {
            return none;
        }
    }

    public async Task<CpCrmLeadScore> ScoreLeadAsync(long leadId, CancellationToken cancellationToken = default)
    {
        var lead = await GetLeadAsync(leadId, cancellationToken).ConfigureAwait(false);
        return lead?.Score ?? CpCrmLeadScore.Empty;
    }

    public async Task<CpCrmDashboard> DashboardAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpCrmDashboard.Empty;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            return await DashboardCoreAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return CpCrmDashboard.Empty;
        }
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<CpCrmOpportunityRow>>> PipelineAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new Dictionary<string, IReadOnlyList<CpCrmOpportunityRow>>(StringComparer.Ordinal);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            return await PipelineCoreAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return new Dictionary<string, IReadOnlyList<CpCrmOpportunityRow>>(StringComparer.Ordinal);
        }
    }

    // ---- core queries -------------------------------------------------------------------------

    private const string LeadSelect = "SELECT `id`, IFNULL(`company`,''), IFNULL(`contact_name`,''), IFNULL(`email`,''), IFNULL(`phone`,''), IFNULL(`source`,''), IFNULL(`status`,'new'), IFNULL(`owner_user_id`,0), IFNULL(`expected_value`,0), IFNULL(`notes`,''), IFNULL(`time_created`,0), IFNULL(`time_updated`,0) FROM `epc_crm_leads`";

    private static CpCrmLeadRow ReadLead(DbDataReader r) =>
        new(L(r, 0), S(r, 1), S(r, 2), S(r, 3), S(r, 4), S(r, 5), S(r, 6), L(r, 7), D(r, 8), S(r, 9), L(r, 10), L(r, 11));

    private static async Task<IReadOnlyList<CpCrmLeadRow>> ListLeadsCoreAsync(DbConnection connection, string status, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 500);
        var list = new List<CpCrmLeadRow>();
        await using var c = connection.CreateCommand();
        var sql = LeadSelect + " WHERE `active` = 1";
        if (status.Length > 0)
        {
            sql += " AND `status` = ?";
        }

        sql += " ORDER BY `time_updated` DESC, `id` DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
        c.CommandText = ErpDb.Positional(sql);
        if (status.Length > 0)
        {
            ErpDb.AddParameters(c, status);
        }

        await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadLead(r));
        }

        return list;
    }

    private static async Task<CpCrmLeadRow?> GetLeadCoreAsync(DbConnection connection, long id, CancellationToken cancellationToken)
    {
        CpCrmLeadRow? lead = null;
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = ErpDb.Positional(LeadSelect + " WHERE `id` = ? AND `active` = 1 LIMIT 1");
            ErpDb.AddParameters(c, id);
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                lead = ReadLead(r);
            }
        }

        if (lead is null)
        {
            return null;
        }

        var acts = await LeadActivityCountAsync(connection, id, cancellationToken).ConfigureAwait(false);
        return lead with { Score = ScoreLead(lead.Status, lead.Email, lead.Phone, lead.ExpectedValue, acts) };
    }

    private static async Task<int> LeadActivityCountAsync(DbConnection connection, long leadId, CancellationToken cancellationToken)
    {
        try
        {
            return (int)await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `epc_crm_activities` WHERE `related_type` = 'lead' AND `related_id` = ? AND `active` = 1"), cancellationToken, leadId).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return 0;
        }
    }

    /// <summary>PHP <c>epc_crm_adv_scored_leads</c>: one activity-count query, then score, keep input order.</summary>
    private static async Task<IReadOnlyList<CpCrmLeadRow>> ScoreAllAsync(DbConnection connection, IReadOnlyList<CpCrmLeadRow> leads, CancellationToken cancellationToken)
    {
        var counts = new Dictionary<long, int>();
        try
        {
            await using var c = connection.CreateCommand();
            c.CommandText = "SELECT `related_id`, COUNT(*) FROM `epc_crm_activities` WHERE `related_type` = 'lead' AND `active` = 1 GROUP BY `related_id`";
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                counts[L(r, 0)] = (int)L(r, 1);
            }
        }
        catch (DbException)
        {
            counts.Clear();
        }

        var list = new List<CpCrmLeadRow>(leads.Count);
        foreach (var l in leads)
        {
            counts.TryGetValue(l.Id, out var n);
            list.Add(l with { Score = ScoreLead(l.Status, l.Email, l.Phone, l.ExpectedValue, n) });
        }

        return list;
    }

    private const string OppSelect = "SELECT o.`id`, IFNULL(o.`lead_id`,0), IFNULL(o.`title`,''), IFNULL(o.`stage`,'prospect'), IFNULL(o.`amount`,0), IFNULL(o.`probability`,0), IFNULL(o.`close_date`,0), IFNULL(o.`owner_user_id`,0), IFNULL(o.`linked_user_id`,0), IFNULL(o.`notes`,''), IFNULL(o.`time_created`,0), IFNULL(o.`time_updated`,0), IFNULL(l.`company`,'') FROM `epc_crm_opportunities` o LEFT JOIN `epc_crm_leads` l ON l.`id` = o.`lead_id`";

    private static CpCrmOpportunityRow ReadOpp(DbDataReader r) =>
        new(L(r, 0), L(r, 1), S(r, 2), S(r, 3), D(r, 4), (int)L(r, 5), L(r, 6), L(r, 7), L(r, 8), S(r, 9), L(r, 10), L(r, 11), S(r, 12));

    private static async Task<IReadOnlyList<CpCrmOpportunityRow>> ListOpportunitiesCoreAsync(DbConnection connection, string? stage, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 500);
        var list = new List<CpCrmOpportunityRow>();
        await using var c = connection.CreateCommand();
        var sql = OppSelect + " WHERE o.`active` = 1";
        var hasStage = !string.IsNullOrEmpty(stage);
        if (hasStage)
        {
            sql += " AND o.`stage` = ?";
        }

        sql += " ORDER BY FIELD(o.`stage`, 'negotiation', 'proposal', 'qualified', 'prospect', 'won', 'lost'), o.`close_date` ASC, o.`id` DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
        c.CommandText = ErpDb.Positional(sql);
        if (hasStage)
        {
            ErpDb.AddParameters(c, stage!);
        }

        await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadOpp(r));
        }

        return list;
    }

    private static async Task<CpCrmOpportunityRow?> GetOpportunityCoreAsync(DbConnection connection, long id, CancellationToken cancellationToken)
    {
        await using var c = connection.CreateCommand();
        c.CommandText = ErpDb.Positional(OppSelect + " WHERE o.`id` = ? AND o.`active` = 1 LIMIT 1");
        ErpDb.AddParameters(c, id);
        await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await r.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadOpp(r) : null;
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<CpCrmOpportunityRow>>> PipelineCoreAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var board = new Dictionary<string, List<CpCrmOpportunityRow>>(StringComparer.Ordinal);
        foreach (var s in OpportunityStages)
        {
            board[s.Key] = [];
        }

        foreach (var o in await ListOpportunitiesCoreAsync(connection, null, 500, cancellationToken).ConfigureAwait(false))
        {
            if (!board.TryGetValue(o.Stage, out var col))
            {
                col = [];
                board[o.Stage] = col;
            }

            col.Add(o);
        }

        return board.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<CpCrmOpportunityRow>)kv.Value, StringComparer.Ordinal);
    }

    private static async Task<IReadOnlyList<CpCrmActivityRow>> ListActivitiesCoreAsync(DbConnection connection, string relatedType, long relatedId, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 500);
        var list = new List<CpCrmActivityRow>();
        await using var c = connection.CreateCommand();
        var sql = "SELECT `id`, IFNULL(`activity_type`,'note'), IFNULL(`related_type`,''), IFNULL(`related_id`,0), IFNULL(`due_date`,0), IFNULL(`done`,0), IFNULL(`owner_user_id`,0), IFNULL(`notes`,''), IFNULL(`time_created`,0) FROM `epc_crm_activities` WHERE `active` = 1";
        var filtered = relatedType.Length > 0 && relatedId > 0;
        if (filtered)
        {
            sql += " AND `related_type` = ? AND `related_id` = ?";
        }

        sql += " ORDER BY `done` ASC, `due_date` ASC, `id` DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
        c.CommandText = ErpDb.Positional(sql);
        if (filtered)
        {
            ErpDb.AddParameters(c, relatedType, relatedId);
        }

        await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadActivity(r));
        }

        return list;
    }

    private static CpCrmActivityRow ReadActivity(DbDataReader r) =>
        new(L(r, 0), S(r, 1), S(r, 2), L(r, 3), L(r, 4), L(r, 5) != 0, L(r, 6), S(r, 7), L(r, 8));

    private const string QuoteSelect = "SELECT q.`id`, IFNULL(q.`opportunity_id`,0), IFNULL(q.`lead_id`,0), IFNULL(q.`customer_user_id`,0), IFNULL(q.`quote_number`,''), IFNULL(q.`status`,'draft'), IFNULL(q.`currency_code`,''), IFNULL(q.`subtotal`,0), IFNULL(q.`shop_order_id`,0), IFNULL(q.`notes`,''), IFNULL(q.`time_created`,0), IFNULL(q.`time_updated`,0), IFNULL(o.`title`,'') FROM `epc_crm_quotes` q LEFT JOIN `epc_crm_opportunities` o ON o.`id` = q.`opportunity_id`";

    private static CpCrmQuoteRow ReadQuote(DbDataReader r) =>
        new(L(r, 0), L(r, 1), L(r, 2), L(r, 3), S(r, 4), S(r, 5), S(r, 6), D(r, 7), L(r, 8), S(r, 9), L(r, 10), L(r, 11), S(r, 12));

    private static async Task<IReadOnlyList<CpCrmQuoteRow>> ListQuotesCoreAsync(DbConnection connection, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 500);
        var list = new List<CpCrmQuoteRow>();
        await using var c = connection.CreateCommand();
        c.CommandText = QuoteSelect + " WHERE q.`active` = 1 ORDER BY q.`time_updated` DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
        await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadQuote(r));
        }

        return list;
    }

    private static async Task<IReadOnlyList<CpCrmQuoteRow>> ListQuotesForEntityCoreAsync(DbConnection connection, string entityType, long entityId, CancellationToken cancellationToken)
    {
        var col = entityType == "opportunity" ? "opportunity_id" : "lead_id";
        var list = new List<CpCrmQuoteRow>();
        await using var c = connection.CreateCommand();
        c.CommandText = ErpDb.Positional(QuoteSelect + " WHERE q.`active` = 1 AND q.`" + col + "` = ? ORDER BY q.`time_updated` DESC LIMIT 50");
        ErpDb.AddParameters(c, entityId);
        await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadQuote(r));
        }

        return list;
    }

    private static async Task<CpCrmQuoteDetail?> GetQuoteCoreAsync(DbConnection connection, long id, CancellationToken cancellationToken)
    {
        CpCrmQuoteRow? quote = null;
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = ErpDb.Positional(QuoteSelect + " WHERE q.`id` = ? AND q.`active` = 1 LIMIT 1");
            ErpDb.AddParameters(c, id);
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                quote = ReadQuote(r);
            }
        }

        if (quote is null)
        {
            return null;
        }

        var lines = new List<CpCrmQuoteLine>();
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = ErpDb.Positional("SELECT `id`, IFNULL(`description`,''), IFNULL(`qty`,0), IFNULL(`unit_price`,0), IFNULL(`sort_order`,0) FROM `epc_crm_quote_lines` WHERE `quote_id` = ? ORDER BY `sort_order`, `id`");
            ErpDb.AddParameters(c, id);
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                lines.Add(new CpCrmQuoteLine(L(r, 0), S(r, 1), D(r, 2), D(r, 3), (int)L(r, 4)));
            }
        }

        return new CpCrmQuoteDetail(quote, lines);
    }

    private const string TicketSelect = "SELECT t.`id`, IFNULL(t.`customer_user_id`,0), IFNULL(t.`order_id`,0), IFNULL(t.`subject`,''), IFNULL(t.`status`,'open'), IFNULL(t.`priority`,'normal'), IFNULL(t.`assigned_user_id`,0), IFNULL(t.`time_created`,0), IFNULL(t.`time_updated`,0), IFNULL(u.`email`,'') FROM `epc_crm_tickets` t LEFT JOIN `users` u ON u.`user_id` = t.`customer_user_id`";

    private static CpCrmTicketRow ReadTicket(DbDataReader r) =>
        new(L(r, 0), L(r, 1), L(r, 2), S(r, 3), S(r, 4), S(r, 5), L(r, 6), L(r, 7), L(r, 8), S(r, 9));

    private static async Task<IReadOnlyList<CpCrmTicketRow>> ListTicketsCoreAsync(DbConnection connection, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 500);
        var list = new List<CpCrmTicketRow>();
        await using var c = connection.CreateCommand();
        c.CommandText = TicketSelect + " WHERE t.`active` = 1 ORDER BY FIELD(t.`status`, 'open', 'pending', 'resolved', 'closed'), t.`time_updated` DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
        await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadTicket(r));
        }

        return list;
    }

    private const string ProjectSelect = "SELECT p.`id`, IFNULL(p.`name`,''), IFNULL(p.`opportunity_id`,0), IFNULL(p.`order_id`,0), IFNULL(p.`status`,'planned'), IFNULL(p.`progress_pct`,0), IFNULL(p.`start_date`,0), IFNULL(p.`end_date`,0), IFNULL(p.`owner_user_id`,0), IFNULL(p.`notes`,''), IFNULL(p.`time_updated`,0), IFNULL(o.`title`,'') FROM `epc_crm_projects` p LEFT JOIN `epc_crm_opportunities` o ON o.`id` = p.`opportunity_id`";

    private static CpCrmProjectRow ReadProject(DbDataReader r) =>
        new(L(r, 0), S(r, 1), L(r, 2), L(r, 3), S(r, 4), (int)L(r, 5), L(r, 6), L(r, 7), L(r, 8), S(r, 9), L(r, 10), S(r, 11));

    private static async Task<IReadOnlyList<CpCrmProjectRow>> ListProjectsCoreAsync(DbConnection connection, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 500);
        var list = new List<CpCrmProjectRow>();
        await using var c = connection.CreateCommand();
        c.CommandText = ProjectSelect + " WHERE p.`active` = 1 ORDER BY p.`time_updated` DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
        await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadProject(r));
        }

        return list;
    }

    private static async Task<IReadOnlyList<CpCrmContractRow>> ListContractsCoreAsync(DbConnection connection, int limit, bool dueOnly, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 500);
        var list = new List<CpCrmContractRow>();
        await using var c = connection.CreateCommand();
        var sql = "SELECT c.`id`, IFNULL(c.`customer_user_id`,0), IFNULL(c.`title`,''), IFNULL(c.`amount`,0), IFNULL(c.`currency_code`,''), IFNULL(c.`billing_interval`,'monthly'), IFNULL(c.`next_billing_date`,0), IFNULL(c.`status`,'draft'), IFNULL(c.`notes`,''), IFNULL(u.`email`,'') FROM `epc_crm_contracts` c LEFT JOIN `users` u ON u.`user_id` = c.`customer_user_id` WHERE c.`active` = 1";
        if (dueOnly)
        {
            sql += " AND c.`status` = 'active' AND c.`next_billing_date` > 0 AND c.`next_billing_date` <= ?";
        }

        sql += " ORDER BY c.`next_billing_date` ASC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
        c.CommandText = ErpDb.Positional(sql);
        if (dueOnly)
        {
            ErpDb.AddParameters(c, DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 90L * 86400);
        }

        await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new CpCrmContractRow(L(r, 0), L(r, 1), S(r, 2), D(r, 3), S(r, 4), S(r, 5), L(r, 6), S(r, 7), S(r, 8), S(r, 9)));
        }

        return list;
    }

    private static async Task<IReadOnlyList<CpCrmExpenseRow>> ListExpensesCoreAsync(DbConnection connection, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 500);
        var list = new List<CpCrmExpenseRow>();
        await using var c = connection.CreateCommand();
        c.CommandText = "SELECT `id`, IFNULL(`employee_user_id`,0), IFNULL(`amount`,0), IFNULL(`currency_code`,''), IFNULL(`category`,''), IFNULL(`status`,'draft'), IFNULL(`receipt_note`,''), IFNULL(`cash_entry_id`,0), IFNULL(`time_updated`,0) FROM `epc_crm_expenses` WHERE `active` = 1 ORDER BY `time_updated` DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
        await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new CpCrmExpenseRow(L(r, 0), L(r, 1), D(r, 2), S(r, 3), S(r, 4), S(r, 5), S(r, 6), L(r, 7), L(r, 8)));
        }

        return list;
    }

    private static async Task<CpCrmDashboard> DashboardCoreAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        var weighted = await ErpDb.DecimalAsync(connection, null, "SELECT IFNULL(SUM(`amount` * `probability` / 100), 0) FROM `epc_crm_opportunities` WHERE `active` = 1 AND `stage` IN ('prospect','qualified','proposal','negotiation')", cancellationToken).ConfigureAwait(false);
        var wonMtd = await ErpDb.DecimalAsync(connection, null, ErpDb.Positional("SELECT IFNULL(SUM(`amount`), 0) FROM `epc_crm_opportunities` WHERE `active` = 1 AND `stage` = 'won' AND `time_updated` >= ?"), cancellationToken, monthStart).ConfigureAwait(false);
        var byStage = new Dictionary<string, CpCrmStageBucket>(StringComparer.Ordinal);
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = "SELECT `stage`, COUNT(*), IFNULL(SUM(`amount`),0), IFNULL(SUM(`amount` * `probability` / 100),0) FROM `epc_crm_opportunities` WHERE `active` = 1 GROUP BY `stage`";
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                byStage[S(r, 0)] = new CpCrmStageBucket((int)L(r, 1), D(r, 2), D(r, 3));
            }
        }

        var leadsTotal = (int)await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_crm_leads` WHERE `active` = 1", cancellationToken).ConfigureAwait(false);
        var leadsNew = (int)await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_crm_leads` WHERE `active` = 1 AND `status` = 'new'", cancellationToken).ConfigureAwait(false);
        var oppsOpen = (int)await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_crm_opportunities` WHERE `active` = 1 AND `stage` IN ('prospect','qualified','proposal','negotiation')", cancellationToken).ConfigureAwait(false);
        var actsDue = (int)await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `epc_crm_activities` WHERE `active` = 1 AND `done` = 0 AND `due_date` <= ?"), cancellationToken, now.ToUnixTimeSeconds() + 86400L * 7).ConfigureAwait(false);

        var quotesOpen = await CountOrZeroAsync(connection, "SELECT COUNT(*) FROM `epc_crm_quotes` WHERE `active` = 1 AND `status` IN ('draft','sent')", cancellationToken).ConfigureAwait(false);
        var ticketsOpen = await CountOrZeroAsync(connection, "SELECT COUNT(*) FROM `epc_crm_tickets` WHERE `active` = 1 AND `status` IN ('open','pending')", cancellationToken).ConfigureAwait(false);
        var projectsActive = await CountOrZeroAsync(connection, "SELECT COUNT(*) FROM `epc_crm_projects` WHERE `active` = 1 AND `status` = 'active'", cancellationToken).ConfigureAwait(false);
        var contractsDue = await CountOrZeroAsync(connection, ErpDb.Positional("SELECT COUNT(*) FROM `epc_crm_contracts` WHERE `active` = 1 AND `status` = 'active' AND `next_billing_date` <= ?"), cancellationToken, now.ToUnixTimeSeconds() + 86400L * 30).ConfigureAwait(false);
        var expensesPending = await CountOrZeroAsync(connection, "SELECT COUNT(*) FROM `epc_crm_expenses` WHERE `active` = 1 AND `status` IN ('submitted','approved')", cancellationToken).ConfigureAwait(false);

        return new CpCrmDashboard(leadsTotal, leadsNew, oppsOpen, weighted, wonMtd, actsDue, byStage, quotesOpen, ticketsOpen, projectsActive, contractsDue, expensesPending);
    }

    private static async Task<CpCrmIntelligence> IntelligenceCoreAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var raw = new List<CpCrmLeadRow>();
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = LeadSelect + " WHERE `active` = 1 ORDER BY `time_created` DESC LIMIT 100";
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                raw.Add(ReadLead(r));
            }
        }

        var scored = new List<CpCrmLeadRow>(await ScoreAllAsync(connection, raw, cancellationToken).ConfigureAwait(false));
        scored.Sort((a, b) => b.Score.Score.CompareTo(a.Score.Score));
        var hot = scored.Count(l => l.Score.Band == "hot");
        var warm = scored.Count(l => l.Score.Band == "warm");
        var cold = scored.Count - hot - warm;

        var stageRows = new List<(string, int, decimal, decimal)>();
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = "SELECT `stage`, COUNT(*), IFNULL(SUM(`amount`),0), IFNULL(SUM(`amount` * `probability` / 100),0) FROM `epc_crm_opportunities` WHERE `active` = 1 GROUP BY `stage`";
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                stageRows.Add((S(r, 0), (int)L(r, 1), D(r, 2), D(r, 3)));
            }
        }

        var next = new List<CpCrmActivityRow>();
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = "SELECT `id`, IFNULL(`activity_type`,'note'), IFNULL(`related_type`,''), IFNULL(`related_id`,0), IFNULL(`due_date`,0), IFNULL(`done`,0), IFNULL(`owner_user_id`,0), IFNULL(`notes`,''), IFNULL(`time_created`,0) FROM `epc_crm_activities` WHERE `active` = 1 AND `done` = 0 AND `due_date` > 0 ORDER BY `due_date` ASC LIMIT 10";
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                next.Add(ReadActivity(r));
            }
        }

        var leads = await CountOrZeroAsync(connection, "SELECT COUNT(*) FROM `epc_crm_leads` WHERE `active` = 1", cancellationToken).ConfigureAwait(false);
        var qualified = await CountOrZeroAsync(connection, "SELECT COUNT(*) FROM `epc_crm_leads` WHERE `active` = 1 AND `status` IN ('qualified','converted')", cancellationToken).ConfigureAwait(false);
        var opps = await CountOrZeroAsync(connection, "SELECT COUNT(*) FROM `epc_crm_opportunities` WHERE `active` = 1", cancellationToken).ConfigureAwait(false);
        var proposals = await CountOrZeroAsync(connection, "SELECT COUNT(*) FROM `epc_crm_opportunities` WHERE `active` = 1 AND `stage` IN ('proposal','negotiation','won')", cancellationToken).ConfigureAwait(false);
        var won = await CountOrZeroAsync(connection, "SELECT COUNT(*) FROM `epc_crm_opportunities` WHERE `active` = 1 AND `stage` = 'won'", cancellationToken).ConfigureAwait(false);

        var sources = new List<CpCrmLeadSource>();
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = "SELECT COALESCE(NULLIF(TRIM(`source`), ''), 'unknown') AS source, COUNT(*) AS c, IFNULL(SUM(`expected_value`),0), IFNULL(SUM(CASE WHEN `status` IN ('qualified','converted') THEN 1 ELSE 0 END),0) FROM `epc_crm_leads` WHERE `active` = 1 GROUP BY COALESCE(NULLIF(TRIM(`source`), ''), 'unknown') ORDER BY c DESC LIMIT 12";
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                sources.Add(new CpCrmLeadSource(S(r, 0), (int)L(r, 1), D(r, 2), (int)L(r, 3)));
            }
        }

        return new CpCrmIntelligence(
            new CpCrmLeadBands(hot, warm, cold, scored.Count),
            scored.Take(10).ToList(),
            Forecast(stageRows),
            next,
            Funnel(leads, qualified, opps, proposals, won),
            sources);
    }

    private static async Task<long> UserIdByEmailAsync(DbConnection connection, string email, CancellationToken cancellationToken)
    {
        try
        {
            return await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT IFNULL(`user_id`,0) FROM `users` WHERE `email` = ? LIMIT 1"), cancellationToken, email).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return 0;
        }
    }

    /// <summary>PHP <c>epc_crm_customer_recent_orders</c>: [] on ERP-only tenants (no <c>shop_orders</c>).</summary>
    private static async Task<(IReadOnlyList<CpCrmOrderRow> Rows, bool HasCommerce)> RecentOrdersCoreAsync(DbConnection connection, long userId, int limit, CancellationToken cancellationToken)
    {
        var list = new List<CpCrmOrderRow>();
        if (userId <= 0)
        {
            return (list, true);
        }

        try
        {
            await using var c = connection.CreateCommand();
            c.CommandText = ErpDb.Positional("SELECT `id`, IFNULL(`time`,0), IFNULL(`paid`,0), IFNULL(`price_total_wt_vat`,0), IFNULL((SELECT `name` FROM `shop_orders_statuses_ref` WHERE `id` = `shop_orders`.`status` LIMIT 1),'') FROM `shop_orders` WHERE `user_id` = ? ORDER BY `time` DESC LIMIT " + Math.Clamp(limit, 1, 50).ToString(CultureInfo.InvariantCulture));
            ErpDb.AddParameters(c, userId);
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                list.Add(new CpCrmOrderRow(L(r, 0), L(r, 1), L(r, 2) != 0, D(r, 3), S(r, 4)));
            }

            return (list, true);
        }
        catch (DbException)
        {
            return ([], false);
        }
    }

    private static async Task<int> CountOrZeroAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object[] args)
    {
        try
        {
            return (int)await ErpDb.LongAsync(connection, null, sql, cancellationToken, args).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return 0;
        }
    }

    private static long L(DbDataReader r, int i) => r.IsDBNull(i) ? 0 : Convert.ToInt64(r.GetValue(i), CultureInfo.InvariantCulture);

    private static decimal D(DbDataReader r, int i) => r.IsDBNull(i) ? 0m : Convert.ToDecimal(r.GetValue(i), CultureInfo.InvariantCulture);

    private static string S(DbDataReader r, int i) => r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i), CultureInfo.InvariantCulture) ?? "";
}
