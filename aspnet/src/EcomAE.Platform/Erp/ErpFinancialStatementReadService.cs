using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public enum ErpFinancialStatementKind
{
    ProfitAndLoss,
    BalanceSheet,
}

public enum ErpFinancialStatementLevel
{
    Company,
    BusinessUnit,
    Consolidation,
}

public sealed record ErpFinancialStatementQuery(
    ErpFinancialStatementKind Kind,
    ErpFinancialStatementLevel Level,
    int CompanyId,
    long BusinessUnitId,
    string ReportingCurrency,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null)
{
    public long FromUnix => FromDate is null
        ? 0
        : new DateTimeOffset(FromDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();

    public long ToUnix => ToDate is null
        ? 0
        : new DateTimeOffset(ToDate.Value.ToDateTime(new TimeOnly(23, 59, 59)), TimeSpan.Zero).ToUnixTimeSeconds();

    public static ErpFinancialStatementQuery FromQuery(
        string? tab,
        string? level,
        string? company,
        string? businessUnit,
        string? currency,
        string? from = null,
        string? to = null)
    {
        var kind = string.Equals(tab, "balance_sheet", StringComparison.OrdinalIgnoreCase)
            ? ErpFinancialStatementKind.BalanceSheet
            : ErpFinancialStatementKind.ProfitAndLoss;
        var scope = (level ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "business_unit" or "bu" or "branch" => ErpFinancialStatementLevel.BusinessUnit,
            "consolidation" or "consolidated" or "group" => ErpFinancialStatementLevel.Consolidation,
            _ => ErpFinancialStatementLevel.Company,
        };
        _ = int.TryParse(company, NumberStyles.Integer, CultureInfo.InvariantCulture, out var companyId);
        _ = long.TryParse(businessUnit, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unitId);
        var code = (currency ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length > 8)
        {
            code = code[..8];
        }

        var fromDate = ErpTrialBalanceParameters.ParseDate(from);
        var toDate = ErpTrialBalanceParameters.ParseDate(to);
        if (fromDate is not null && toDate is not null && toDate < fromDate)
        {
            (fromDate, toDate) = (toDate, fromDate);
        }

        return new(kind, scope, companyId < 0 ? 0 : companyId, unitId < 0 ? 0 : unitId, code, fromDate, toDate);
    }
}

public sealed record ErpFinancialStatementLine(
    string Code,
    string Name,
    string AccountType,
    decimal Accounting,
    decimal Reporting);

public sealed record ErpFinancialStatementUnit(long Id, string Code, string Name);

public sealed record ErpFinancialStatement(
    ErpFinancialStatementQuery Query,
    string AccountingCurrency,
    string ReportingCurrency,
    decimal Rate,
    string RateNote,
    IReadOnlyList<ErpFinancialStatementLine> Lines,
    decimal IncomeAccounting,
    decimal ExpenseAccounting,
    decimal NetProfitAccounting,
    decimal AssetAccounting,
    decimal LiabilityAccounting,
    decimal EquityAccounting,
    decimal BalanceCheckAccounting,
    string LevelLabel,
    string Source,
    string Message,
    IReadOnlyList<ErpFinancialStatementUnit> BusinessUnits);

public interface IErpFinancialStatementReadService
{
    Task<ErpFinancialStatement> ReadAsync(
        ErpFinancialStatementQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Profit and loss and balance sheet from posted journals.
/// Company uses one legal entity. Business unit uses the document header.
/// Consolidation sums every company and removes intercompany amounts.
/// A reporting currency translates the accounting-currency totals with the latest exchange rate.
/// </summary>
public sealed class ErpFinancialStatementReadService : IErpFinancialStatementReadService
{
    private const string JournalUnix =
        "(CASE WHEN j.`journal_date` IS NULL THEN 0 WHEN j.`journal_date` < 100000000 THEN IFNULL(UNIX_TIMESTAMP(STR_TO_DATE(CAST(j.`journal_date` AS CHAR), '%Y%m%d')), 0) ELSE j.`journal_date` END)";

    private const string ProfitCloseSql =
        "j.`source_type` = 'adjustment' AND ("
        + "LOWER(CONCAT(IFNULL(j.`journal_no`,''),' ',IFNULL(j.`reference`,''),' ',IFNULL(j.`description`,''))) LIKE '%year-end close%'"
        + " OR LOWER(CONCAT(IFNULL(j.`journal_no`,''),' ',IFNULL(j.`reference`,''),' ',IFNULL(j.`description`,''))) LIKE '%year end close%'"
        + " OR LOWER(CONCAT(IFNULL(j.`journal_no`,''),' ',IFNULL(j.`reference`,''),' ',IFNULL(j.`description`,''))) LIKE '%closing transaction%'"
        + " OR LOWER(CONCAT(IFNULL(j.`journal_no`,''),' ',IFNULL(j.`reference`,''),' ',IFNULL(j.`description`,''))) LIKE '%profit and loss close%'"
        + " OR LOWER(CONCAT(IFNULL(j.`journal_no`,''),' ',IFNULL(j.`reference`,''),' ',IFNULL(j.`description`,''))) LIKE '%p&l close%'"
        + " OR LOWER(CONCAT(IFNULL(j.`journal_no`,''),' ',IFNULL(j.`reference`,''),' ',IFNULL(j.`description`,''))) LIKE '%fy-close%'"
        + ")";
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFinancialStatementReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpFinancialStatement> ReadAsync(
        ErpFinancialStatementQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!_connections.IsConfigured)
        {
            return Empty(query, "migration", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpGlPostingService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            await ErpLazySchema.EnsureFxRatesAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex)
        {
            return Empty(query, "database-error", ex.Message);
        }

        var units = await LoadUnitsAsync(connection, cancellationToken).ConfigureAwait(false);
        var accounting = await AccountingCurrencyAsync(connection, query.CompanyId, cancellationToken).ConfigureAwait(false);
        var requested = query.ReportingCurrency;
        if (requested.Length == 0)
        {
            var policyCurrency = await PolicyReportingCurrencyAsync(connection, cancellationToken).ConfigureAwait(false);
            if (policyCurrency.Length == 3 && !policyCurrency.Equals(accounting, StringComparison.OrdinalIgnoreCase))
            {
                requested = policyCurrency;
            }
        }

        var (rate, reporting, rateNote) = await ResolveRateAsync(
            connection, accounting, requested, cancellationToken).ConfigureAwait(false);
        ErpHeadOfficeMoney.Result? headOffice = null;
        if (query.Level == ErpFinancialStatementLevel.Consolidation)
        {
            var office = requested.Length == 3 ? requested : accounting;
            headOffice = await TryHeadOfficeAsync(
                connection,
                office,
                query.Kind == ErpFinancialStatementKind.ProfitAndLoss,
                cancellationToken).ConfigureAwait(false);
            if (headOffice is { Applied: true })
            {
                rate = 1m;
                accounting = headOffice.Currency;
                reporting = headOffice.Currency;
                rateNote = headOffice.Note;
            }
            else if (headOffice is { Applied: false }
                && headOffice.Note.Contains("No exchange rate", StringComparison.Ordinal))
            {
                rateNote = headOffice.Note;
            }
        }

        var rateMissing = headOffice is { Applied: false }
            && headOffice.Note.Contains("No exchange rate", StringComparison.Ordinal);
        List<RawLine> raw;
        try
        {
            raw = headOffice is { Applied: true }
                ? HeadOfficeRows(headOffice)
                : rateMissing
                    ? []
                    : await LoadAccountsAsync(connection, query, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex)
        {
            return Empty(query, "database-error", ex.Message, units);
        }

        var income = 0m;
        var expense = 0m;
        var assets = 0m;
        var liabilities = 0m;
        var equity = 0m;
        var built = new List<(string Code, string Name, string Type, decimal Amount)>();
        foreach (var row in raw)
        {
            var amount = Signed(row, query);
            var type = ErpAccountClass.Normalize(row.Type, row.Code);
            if (type is "revenue" or "income") income += amount;
            else if (type == "expense") expense += amount;
            else if (type == "asset") assets += amount;
            else if (type == "liability") liabilities += amount;
            else if (type == "equity") equity += amount;
            else continue;

            var onThisStatement = query.Kind == ErpFinancialStatementKind.BalanceSheet
                ? type is "asset" or "liability" or "equity"
                : type is "revenue" or "income" or "expense";
            if (onThisStatement)
            {
                built.Add((row.Code, row.Name, type, amount));
            }
        }

        var nciMemos = new List<ErpFinancialStatementLine>();
        if (query.Level == ErpFinancialStatementLevel.Consolidation && headOffice is not { Applied: true } && !rateMissing)
        {
            var ledger = await LoadIcLedgerEliminationsAsync(connection, cancellationToken).ConfigureAwait(false);
            var useLedger = Math.Abs(ledger.Income) + Math.Abs(ledger.Expense) + Math.Abs(ledger.Asset) + Math.Abs(ledger.Liability) >= 0.005m;
            var (elimIncome, elimExpense, elimAsset, elimLiability, _) = useLedger
                ? ledger
                : await LoadEliminationsAsync(connection, cancellationToken).ConfigureAwait(false);
            AddElim(built, "ELIM-REV", "Intercompany revenue eliminated", "revenue", -elimIncome, ref income);
            AddElim(built, "ELIM-EXP", "Intercompany expense eliminated", "expense", -elimExpense, ref expense);
            AddElim(built, "ELIM-ASSET", "Intercompany asset eliminated", "asset", -elimAsset, ref assets);
            AddElim(built, "ELIM-LIAB", "Intercompany liability eliminated", "liability", -elimLiability, ref liabilities);
            if (Math.Abs(ledger.UnrealizedProfit) >= 0.005m)
            {
                AddElim(built, "ELIM-UP-EXP", "Unrealized intercompany profit (cost of sales)", "expense", ledger.UnrealizedProfit, ref expense);
                AddElim(built, "ELIM-UP-INV", "Unrealized intercompany profit (inventory)", "asset", -ledger.UnrealizedProfit, ref assets);
            }

            foreach (var nci in await LoadNciAsync(connection, query, cancellationToken).ConfigureAwait(false))
            {
                if (query.Kind == ErpFinancialStatementKind.BalanceSheet)
                {
                    // Memo within equity — the consolidated total already carries the member's full equity.
                    built.Add(("NCI-" + nci.Code, "of which non-controlling interest — " + nci.Name, "equity", nci.EquityShare));
                }
                else
                {
                    nciMemos.Add(new("NCI-" + nci.Code, "of which attributable to non-controlling interest — " + nci.Name, "total", nci.ProfitShare, Round2(nci.ProfitShare * rate)));
                }
            }
        }

        if (built.Count == 0 && query.Level == ErpFinancialStatementLevel.Consolidation && headOffice is not { Applied: true } && !rateMissing)
        {
            var figures = await LoadFiguresAsync(connection, cancellationToken).ConfigureAwait(false);
            if (figures is not null)
            {
                AddFigure(built, "FIG-REV", "Subsidiary revenue", "revenue", figures.Value.Revenue, ref income);
                AddFigure(built, "FIG-EXP", "Subsidiary expenses", "expense", figures.Value.Expenses, ref expense);
                AddFigure(built, "FIG-ASSET", "Subsidiary assets", "asset", figures.Value.Assets, ref assets);
                AddFigure(built, "FIG-LIAB", "Subsidiary liabilities", "liability", figures.Value.Liabilities, ref liabilities);
                AddFigure(built, "FIG-EQ", "Subsidiary equity", "equity", figures.Value.Equity, ref equity);
            }
        }

        var profit = Round2(income - expense);
        income = Round2(income);
        expense = Round2(expense);
        assets = Round2(assets);
        liabilities = Round2(liabilities);
        equity = Round2(equity);
        var check = Round2(assets - liabilities - equity - profit);

        var wanted = query.Kind == ErpFinancialStatementKind.ProfitAndLoss
            ? new[] { "revenue", "income", "expense" }
            : new[] { "asset", "liability", "equity" };
        var lines = built
            .Where(row => wanted.Contains(row.Type, StringComparer.Ordinal))
            .OrderBy(row => Order(row.Type))
            .ThenBy(row => row.Code, StringComparer.OrdinalIgnoreCase)
            .Select(row => new ErpFinancialStatementLine(
                row.Code,
                row.Name,
                row.Type,
                Round2(row.Amount),
                Round2(row.Amount * rate)))
            .ToList();

        if (query.Kind == ErpFinancialStatementKind.ProfitAndLoss)
        {
            lines.Add(new("", "Net profit / (loss)", "total", profit, Round2(profit * rate)));
            lines.AddRange(nciMemos);
        }
        else
        {
            lines.Add(new("", "Current year earnings", "equity", profit, Round2(profit * rate)));
            lines.Add(new(
                "",
                Math.Abs(check) < 0.005m ? "In balance" : "Out of balance",
                "total",
                check,
                Round2(check * rate)));
        }

        var levelLabel = query.Level switch
        {
            ErpFinancialStatementLevel.BusinessUnit => UnitLabel(units, query.BusinessUnitId),
            ErpFinancialStatementLevel.Consolidation => "Consolidation",
            _ => query.CompanyId > 0 ? "Company " + query.CompanyId.ToString(CultureInfo.InvariantCulture) : "This company",
        };
        var message = query.Level switch
        {
            ErpFinancialStatementLevel.BusinessUnit => "Posted ledger for this business unit. Opening balances stay on the company statement.",
            ErpFinancialStatementLevel.Consolidation => headOffice is { Applied: true }
                ? headOffice.Note
                : "Consolidated posted ledger. Active elimination rules clear intercompany sales, purchases, receivables, and payables.",
            _ => query.Kind == ErpFinancialStatementKind.ProfitAndLoss && query.FromDate is not null
                ? "Profit and loss for the selected dates. Income, expenses, and gross profit stay on this statement. The year-end close remains on retained earnings."
                : query.Kind == ErpFinancialStatementKind.BalanceSheet && query.ToDate is not null
                    ? "Balance sheet through the selected end date. Income, expenses, and gross profit for these dates stay visible. The year-end close is in retained earnings and is not added again."
                    : "Posted ledger for this company. Accounts with no movement stay on the statement at zero.",
        };
        if (rateNote.Length > 0 && !message.Contains(rateNote, StringComparison.Ordinal))
        {
            message = message + " " + rateNote;
        }

        return new(
            query,
            accounting,
            reporting,
            rate,
            rateNote,
            lines,
            income,
            expense,
            profit,
            assets,
            liabilities,
            equity,
            check,
            levelLabel,
            "live",
            message,
            units);
    }

    private static void AddElim(
        List<(string Code, string Name, string Type, decimal Amount)> built,
        string code,
        string name,
        string type,
        decimal amount,
        ref decimal total)
    {
        if (Math.Abs(amount) < 0.005m)
        {
            return;
        }

        built.Add((code, name, type, amount));
        total += amount;
    }

    private static void AddFigure(
        List<(string Code, string Name, string Type, decimal Amount)> built,
        string code,
        string name,
        string type,
        decimal amount,
        ref decimal total)
    {
        if (Math.Abs(amount) < 0.005m)
        {
            return;
        }

        built.Add((code, name, type, amount));
        total += amount;
    }

    private static decimal Signed(RawLine row, ErpFinancialStatementQuery query)
    {
        var opening = query.Level == ErpFinancialStatementLevel.BusinessUnit
            || (query.Kind == ErpFinancialStatementKind.ProfitAndLoss && query.FromDate is not null)
            ? 0m
            : row.Opening;
        return string.Equals(row.Side, "credit", StringComparison.OrdinalIgnoreCase)
            ? opening + row.Credits - row.Debits
            : opening + row.Debits - row.Credits;
    }

    private static int Order(string type) => type switch
    {
        "revenue" or "income" => 0,
        "expense" => 1,
        "asset" => 0,
        "liability" => 1,
        "equity" => 2,
        _ => 9,
    };

    private static decimal Round2(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Entity ids of 'full' consolidation members (member site_key = pm legal-entity
    /// code). Returns <see langword="null"/> when the member register is absent or
    /// has no member rows at all — the all-journals fallback; an empty list when
    /// members exist but none is 'full' — consolidated views then exclude every
    /// entity-stamped journal rather than silently re-including 'excluded' members.
    /// </summary>
    internal static async Task<List<long>?> FullMemberEntityIdsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var ids = new List<long>();
        var anyMembers = 0L;
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ErpDb.Positional(
                "SELECT COUNT(DISTINCT e.`id`),"
                + " COUNT(DISTINCT CASE WHEN m.`consolidation`='full' THEN e.`id` END)"
                + " FROM `epc_entity_members` m"
                + " JOIN `epc_erp_pm_legal_entities` e ON e.`code` = m.`site_key` AND e.`active` = 1");
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            anyMembers = reader.GetInt64(0);
            if (anyMembers == 0)
            {
                return null;
            }

            var fullCount = reader.GetInt64(1);
            if (fullCount == 0)
            {
                return ids;
            }
        }
        catch (DbException)
        {
            // Table absent on single-entity tenants — consolidation falls back to all journals.
            return null;
        }

        await using (var full = connection.CreateCommand())
        {
            full.CommandText = ErpDb.Positional(
                "SELECT DISTINCT e.`id` FROM `epc_entity_members` m"
                + " JOIN `epc_erp_pm_legal_entities` e ON e.`code` = m.`site_key` AND e.`active` = 1"
                + " WHERE m.`consolidation` = 'full'");
            await using var fullReader = await full.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await fullReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ids.Add(fullReader.GetInt64(0));
            }
        }

        return ids;
    }

    private async Task<List<RawLine>> LoadAccountsAsync(
        DbConnection connection,
        ErpFinancialStatementQuery query,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        var filter = query.Level switch
        {
            ErpFinancialStatementLevel.BusinessUnit => " AND IFNULL(NULLIF(l.`business_unit_id`, 0), IFNULL(j.`business_unit_id`, 0)) = @unitId",
            ErpFinancialStatementLevel.Company when query.CompanyId > 0 => " AND (IFNULL(j.`company_id`, 0) = @companyId OR IFNULL(l.`legal_entity_id`, 0) = @companyId)",
            _ => string.Empty,
        };
        if (query.Level == ErpFinancialStatementLevel.Consolidation)
        {
            // Sum only 'full' group members — 'excluded'/'proportional'/'equity'
            // entities (managed-but-not-consolidated) stay off consolidated
            // statements. When the tenant has no member register at all the
            // prior all-journals behaviour is preserved.
            var fullMembers = await FullMemberEntityIdsAsync(connection, cancellationToken).ConfigureAwait(false);
            if (fullMembers is { Count: > 0 })
            {
                var ids = string.Join(',', fullMembers);
                // Unscoped headers (company_id=0) count only when the LINE also
                // carries no entity — an excluded entity stamped on the line would
                // otherwise leak through the header's missing scope.
                filter = " AND ((IFNULL(j.`company_id`, 0) = 0 AND IFNULL(l.`legal_entity_id`, 0) = 0)"
                    + " OR IFNULL(j.`company_id`, 0) IN (" + ids + ")"
                    + " OR IFNULL(l.`legal_entity_id`, 0) IN (" + ids + "))";
            }
            else if (fullMembers is not null)
            {
                // Members exist but none consolidates — only entity-less journals count.
                filter = " AND IFNULL(j.`company_id`, 0) = 0 AND IFNULL(l.`legal_entity_id`, 0) = 0";
            }
        }
        var fromUnix = query.Kind == ErpFinancialStatementKind.ProfitAndLoss ? query.FromUnix : 0L;
        var toUnix = query.ToUnix;
        var excludeClose = query.Kind == ErpFinancialStatementKind.ProfitAndLoss ? 1 : 0;
        var posted = "j.`id` IS NOT NULL"
            + " AND (@fromUnix = 0 OR " + JournalUnix + " >= @fromUnix)"
            + " AND (@toUnix = 0 OR " + JournalUnix + " <= @toUnix)"
            + " AND (@excludeClose = 0 OR NOT (" + ProfitCloseSql + "))";
        command.CommandText =
            "SELECT IFNULL(a.`code`, '') AS code,"
            + " IFNULL(a.`name`, '') AS name,"
            + " IFNULL(a.`account_type`, '') AS account_type,"
            + " IFNULL(a.`normal_side`, 'debit') AS normal_side,"
            + " IFNULL(a.`opening_balance`, 0) AS opening_balance,"
            + " IFNULL(SUM(CASE WHEN " + posted + " THEN l.`debit` ELSE 0 END), 0) AS debits,"
            + " IFNULL(SUM(CASE WHEN " + posted + " THEN l.`credit` ELSE 0 END), 0) AS credits"
            + " FROM `epc_erp_coa_accounts` a"
            + " LEFT JOIN `epc_erp_gl_lines` l ON l.`coa_id` = a.`id`"
            + " LEFT JOIN `epc_erp_gl_journals` j ON j.`id` = l.`journal_id` AND j.`active` = 1"
            + filter
            + " WHERE a.`active` = 1"
            + " GROUP BY a.`id`, a.`code`, a.`name`, a.`account_type`, a.`normal_side`, a.`opening_balance`"
            + " ORDER BY a.`code`";
        Add(command, "@fromUnix", fromUnix);
        Add(command, "@toUnix", toUnix);
        Add(command, "@excludeClose", excludeClose);
        if (query.Level == ErpFinancialStatementLevel.BusinessUnit)
        {
            Add(command, "@unitId", query.BusinessUnitId);
        }
        else if (query.Level == ErpFinancialStatementLevel.Company && query.CompanyId > 0)
        {
            Add(command, "@companyId", query.CompanyId);
        }

        var rows = new List<RawLine>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new RawLine(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2).Trim().ToLowerInvariant(),
                reader.GetString(3),
                reader.GetDecimal(4),
                reader.GetDecimal(5),
                reader.GetDecimal(6)));
        }

        return rows;
    }

    private static async Task<(decimal Income, decimal Expense, decimal Asset, decimal Liability, decimal UnrealizedProfit)> LoadIcLedgerEliminationsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            var purposes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pctRules = new List<(string DebitPurpose, decimal Rate)>();
            await using (var rules = connection.CreateCommand())
            {
                rules.CommandText = """
                    SELECT IFNULL(`debit_purpose`, '') AS debit_purpose,
                           IFNULL(`credit_purpose`, '') AS credit_purpose,
                           IFNULL(`method`, 'full') AS method,
                           IFNULL(`rate_pct`, 0) AS rate_pct
                    FROM `epc_erp_ic_elim_rules`
                    WHERE `active` = 1
                    """;
                await using var reader = await rules.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var method = reader.GetString(2).Trim().ToLowerInvariant();
                    var rate = reader.GetDecimal(3);
                    if (method == "pct" && rate > 0)
                    {
                        var basePurpose = reader.GetString(0).Trim().ToLowerInvariant();
                        pctRules.Add((basePurpose, rate));
                        AddPurpose(purposes, basePurpose);
                        continue;
                    }

                    if (method != "full")
                    {
                        continue;
                    }

                    AddPurpose(purposes, reader.GetString(0));
                    AddPurpose(purposes, reader.GetString(1));
                }
            }

            if (purposes.Count == 0)
            {
                return (0, 0, 0, 0, 0);
            }

            var codesByPurpose = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            await using (var map = connection.CreateCommand())
            {
                map.CommandText = """
                    SELECT IFNULL(`purpose`, '') AS purpose, IFNULL(`coa_code`, '') AS coa_code
                    FROM `epc_erp_ic_accounts`
                    WHERE `active` = 1
                    """;
                await using var reader = await map.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var purpose = reader.GetString(0).Trim().ToLowerInvariant();
                    var code = reader.GetString(1).Trim();
                    if (!purposes.Contains(purpose) || code.Length == 0)
                    {
                        continue;
                    }

                    if (!codesByPurpose.TryGetValue(purpose, out var set))
                    {
                        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        codesByPurpose[purpose] = set;
                    }

                    set.Add(code);
                }
            }

            var codes = codesByPurpose.SelectMany(pair => pair.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (codes.Count == 0)
            {
                return (0, 0, 0, 0, 0);
            }

            var balances = new Dictionary<string, (string Side, decimal Opening, decimal Debit, decimal Credit)>(StringComparer.OrdinalIgnoreCase);
            await using (var balancesCommand = connection.CreateCommand())
            {
                var names = new string[codes.Count];
                for (var i = 0; i < codes.Count; i++)
                {
                    names[i] = "@c" + i.ToString(CultureInfo.InvariantCulture);
                    var parameter = balancesCommand.CreateParameter();
                    parameter.ParameterName = names[i];
                    parameter.Value = codes[i];
                    balancesCommand.Parameters.Add(parameter);
                }

                // Eliminations follow the same member perimeter as the accounts
                // themselves — a non-full member's intercompany balance must not
                // be subtracted from the consolidated group.
                var fullMembers = await FullMemberEntityIdsAsync(connection, cancellationToken).ConfigureAwait(false);
                var memberFilter = fullMembers is null
                    ? string.Empty
                    : fullMembers.Count == 0
                        ? " AND IFNULL(j.`company_id`, 0) = 0 AND IFNULL(l.`legal_entity_id`, 0) = 0"
                        : " AND ((IFNULL(j.`company_id`, 0) = 0 AND IFNULL(l.`legal_entity_id`, 0) = 0)"
                            + " OR IFNULL(j.`company_id`, 0) IN (" + string.Join(',', fullMembers) + ")"
                            + " OR IFNULL(l.`legal_entity_id`, 0) IN (" + string.Join(',', fullMembers) + "))";

                balancesCommand.CommandText =
                    "SELECT IFNULL(a.`code`, '') AS code, IFNULL(a.`normal_side`, 'debit') AS normal_side,"
                    + " IFNULL(a.`opening_balance`, 0) AS opening_balance,"
                    + " IFNULL(SUM(CASE WHEN j.`id` IS NOT NULL THEN l.`debit` ELSE 0 END), 0) AS debits,"
                    + " IFNULL(SUM(CASE WHEN j.`id` IS NOT NULL THEN l.`credit` ELSE 0 END), 0) AS credits"
                    + " FROM `epc_erp_coa_accounts` a"
                    + " LEFT JOIN `epc_erp_gl_lines` l ON l.`coa_id` = a.`id`"
                    + " LEFT JOIN `epc_erp_gl_journals` j ON j.`id` = l.`journal_id` AND j.`active` = 1"
                    + memberFilter
                    + " WHERE a.`code` IN (" + string.Join(",", names) + ")"
                    + " GROUP BY a.`id`, a.`code`, a.`normal_side`, a.`opening_balance`";
                await using var reader = await balancesCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    balances[reader.GetString(0)] = (reader.GetString(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4));
                }
            }

            decimal income = 0, expense = 0, asset = 0, liability = 0, unrealized = 0;
            foreach (var purpose in purposes)
            {
                if (!codesByPurpose.TryGetValue(purpose, out var set))
                {
                    continue;
                }

                var signed = 0m;
                foreach (var code in set)
                {
                    if (!balances.TryGetValue(code, out var row))
                    {
                        continue;
                    }

                    signed += string.Equals(row.Side, "credit", StringComparison.OrdinalIgnoreCase)
                        ? row.Opening + row.Credit - row.Debit
                        : row.Opening + row.Debit - row.Credit;
                }

                foreach (var (basePurpose, rate) in pctRules)
                {
                    if (basePurpose.Equals(purpose, StringComparison.OrdinalIgnoreCase))
                    {
                        // method='pct' rules eliminate rate_pct % of the mapped
                        // intercompany balance — unrealized profit in inventory.
                        unrealized += signed * rate / 100m;
                    }
                }

                switch (purpose)
                {
                    case "sales":
                        income += signed;
                        break;
                    case "purchase":
                        expense += signed;
                        break;
                    case "receivable":
                        asset += signed;
                        break;
                    case "payable":
                        liability += signed;
                        break;
                }
            }

            return (income, expense, asset, liability, unrealized);
        }
        catch (DbException)
        {
            return (0, 0, 0, 0, 0);
        }
    }

    private static void AddPurpose(HashSet<string> purposes, string raw)
    {
        var purpose = raw.Trim().ToLowerInvariant();
        if (purpose is "sales" or "purchase" or "receivable" or "payable")
        {
            purposes.Add(purpose);
        }
    }

    private static async Task<string> PolicyReportingCurrencyAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT IFNULL(`reporting_currency`, '') FROM `epc_erp_ic_policy` WHERE `id` = 1";
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var code = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim().ToUpperInvariant() ?? "";
            return code.Length == 3 ? code : "";
        }
        catch (DbException)
        {
            return "";
        }
    }

    private static async Task<(decimal Income, decimal Expense, decimal Asset, decimal Liability, decimal UnrealizedProfit)> LoadEliminationsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT IFNULL(`txn_type`, '') AS txn_type, IFNULL(`amount`, 0) AS amount
                FROM `epc_cons_ic`
                """;
            decimal income = 0, expense = 0, asset = 0, liability = 0;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var amount = reader.GetDecimal(1);
                var type = reader.GetString(0).Trim().ToLowerInvariant();
                if (type is "loan" or "balance")
                {
                    asset += amount;
                    liability += amount;
                }
                else
                {
                    income += amount;
                    expense += amount;
                }
            }

            return (income, expense, asset, liability, 0m);
        }
        catch (DbException)
        {
            return (0, 0, 0, 0, 0);
        }
    }

    private sealed record NciSlice(string Code, string Name, decimal EquityShare, decimal ProfitShare);

    /// <summary>
    /// Non-controlling interest for 'full' members held at &lt; 100%: the outside
    /// share of each member's net assets (equity + period result) and profit.
    /// Per-entity books come from posted movements — company-level opening
    /// balances are not entity-stamped.
    /// </summary>
    private static async Task<IReadOnlyList<NciSlice>> LoadNciAsync(
        DbConnection connection,
        ErpFinancialStatementQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var members = new List<(long EntityId, string Code, string Name, decimal Pct)>();
            await using (var memberCommand = connection.CreateCommand())
            {
                memberCommand.CommandText = """
                    SELECT e.`id`, m.`site_key`, IFNULL(m.`entity_name`, ''), m.`ownership_pct`
                    FROM `epc_entity_members` m
                    JOIN `epc_erp_pm_legal_entities` e ON e.`code` = m.`site_key` AND e.`active` = 1
                    WHERE m.`consolidation` = 'full' AND m.`ownership_pct` < 100
                    """;
                await using var memberReader = await memberCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await memberReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    members.Add((memberReader.GetInt64(0), memberReader.GetString(1), memberReader.GetString(2), memberReader.GetDecimal(3)));
                }
            }

            if (members.Count == 0)
            {
                return [];
            }

            var ids = string.Join(',', members.Select(m => m.EntityId.ToString(CultureInfo.InvariantCulture)));
            var posted = "(@fromUnix = 0 OR " + JournalUnix + " >= @fromUnix)"
                + " AND (@toUnix = 0 OR " + JournalUnix + " <= @toUnix)";
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COALESCE(NULLIF(l.`legal_entity_id`, 0), NULLIF(j.`company_id`, 0), 0) AS ent,"
                + " a.`account_type`, a.`normal_side`,"
                + " IFNULL(SUM(CASE WHEN " + posted + " THEN l.`debit` ELSE 0 END), 0) AS debits,"
                + " IFNULL(SUM(CASE WHEN " + posted + " THEN l.`credit` ELSE 0 END), 0) AS credits"
                + " FROM `epc_erp_coa_accounts` a"
                + " JOIN `epc_erp_gl_lines` l ON l.`coa_id` = a.`id`"
                + " JOIN `epc_erp_gl_journals` j ON j.`id` = l.`journal_id` AND j.`active` = 1"
                + " WHERE a.`active` = 1"
                + " AND COALESCE(NULLIF(l.`legal_entity_id`, 0), NULLIF(j.`company_id`, 0), 0) IN (" + ids + ")"
                + " GROUP BY ent, a.`account_type`, a.`normal_side`";
            Add(command, "@fromUnix", query.Kind == ErpFinancialStatementKind.ProfitAndLoss ? query.FromUnix : 0L);
            Add(command, "@toUnix", query.ToUnix);

            var totals = new Dictionary<long, (decimal Equity, decimal Profit)>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var ent = reader.GetInt64(0);
                var type = ErpAccountClass.Normalize(reader.GetString(1), "");
                var side = reader.GetString(2);
                var signed = string.Equals(side, "credit", StringComparison.OrdinalIgnoreCase)
                    ? reader.GetDecimal(4) - reader.GetDecimal(3)
                    : reader.GetDecimal(3) - reader.GetDecimal(4);
                totals.TryGetValue(ent, out var acc);
                var profitDelta = type is "revenue" or "income" ? signed : type == "expense" ? -signed : 0m;
                var equityDelta = type is "liability" or "equity" ? signed : 0m;
                totals[ent] = (acc.Equity + equityDelta + profitDelta, acc.Profit + profitDelta);
            }

            var slices = new List<NciSlice>(members.Count);
            foreach (var (entityId, code, name, pct) in members)
            {
                totals.TryGetValue(entityId, out var acc);
                // Equity base = equity + result (net assets under balanced books);
                // expense rows are debit-normal so signed equity already nets them.
                var outside = (100m - pct) / 100m;
                var equity = Round2(acc.Equity * outside);
                var profitShare = Round2(acc.Profit * outside);
                if (Math.Abs(equity) < 0.005m && Math.Abs(profitShare) < 0.005m)
                {
                    continue;
                }

                slices.Add(new NciSlice(code, name, equity, profitShare));
            }

            return slices;
        }
        catch (DbException)
        {
            return [];
        }
    }

    private static async Task<(decimal Revenue, decimal Expenses, decimal Assets, decimal Liabilities, decimal Equity)?> LoadFiguresAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT IFNULL(SUM(`revenue`), 0), IFNULL(SUM(`expenses`), 0),
                       IFNULL(SUM(`assets`), 0), IFNULL(SUM(`liabilities`), 0), IFNULL(SUM(`equity`), 0)
                FROM `epc_cons_figures`
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            return (reader.GetDecimal(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4));
        }
        catch (DbException)
        {
            return null;
        }
    }

    private static async Task<ErpHeadOfficeMoney.Result?> TryHeadOfficeAsync(
        DbConnection connection,
        string reporting,
        bool profitAndLoss,
        CancellationToken cancellationToken)
    {
        try
        {
            var slices = await LoadCurrencySlicesAsync(connection, cancellationToken).ConfigureAwait(false);
            var ledger = await LoadIcLedgerEliminationsAsync(connection, cancellationToken).ConfigureAwait(false);
            var useLedger = Math.Abs(ledger.Income) + Math.Abs(ledger.Expense) + Math.Abs(ledger.Asset) + Math.Abs(ledger.Liability) >= 0.005m;
            var elim = useLedger
                ? ledger
                : await LoadEliminationsAsync(connection, cancellationToken).ConfigureAwait(false);
            var currency = SharedCurrency(slices);
            var elimAmount = Math.Abs(elim.Income) + Math.Abs(elim.Expense) + Math.Abs(elim.Asset) + Math.Abs(elim.Liability);
            var elimHeld = currency.Length != 3 && elimAmount >= 0.005m;
            if (!elimHeld)
            {
                AddSlice(slices, "ELIM-REV", "Intercompany revenue eliminated", "revenue", currency, -elim.Income);
                AddSlice(slices, "ELIM-EXP", "Intercompany expense eliminated", "expense", currency, -elim.Expense);
                AddSlice(slices, "ELIM-ASSET", "Intercompany asset eliminated", "asset", currency, -elim.Asset);
                AddSlice(slices, "ELIM-LIAB", "Intercompany liability eliminated", "liability", currency, -elim.Liability);
                AddSlice(slices, "ELIM-UP-EXP", "Unrealized intercompany profit (cost of sales)", "expense", currency, ledger.UnrealizedProfit);
                AddSlice(slices, "ELIM-UP-INV", "Unrealized intercompany profit (inventory)", "asset", currency, -ledger.UnrealizedProfit);
            }

            var rates = await LoadRatesAsync(connection, cancellationToken).ConfigureAwait(false);
            var result = ErpHeadOfficeMoney.Consolidate(slices, reporting, rates, profitAndLoss);
            if (elimHeld && result.Applied)
            {
                var note = result.Note.Length == 0
                    ? MixedEliminationNote
                    : result.Note + " " + MixedEliminationNote;
                result = result with { Note = note };
            }
            if (result.Applied)
            {
                return result;
            }

            // A missing rate must not fall through to a raw sum of mixed currencies.
            return result.Note.Contains("No exchange rate", StringComparison.Ordinal) ? result : null;
        }
        catch (DbException)
        {
            return null;
        }
    }

    private static void AddSlice(
        List<ErpHeadOfficeMoney.Slice> slices,
        string code,
        string name,
        string type,
        string currency,
        decimal amount)
    {
        if (Math.Abs(amount) < 0.005m)
        {
            return;
        }

        slices.Add(new ErpHeadOfficeMoney.Slice(code, name, type, currency, amount));
    }

    private const string MixedEliminationNote =
        "Intercompany eliminations stay off this statement because the companies do not share one currency.";

    /// <summary>
    /// Currency of every operating slice when they agree. A mixed book returns
    /// an empty code so an untagged elimination is not converted as dirhams.
    /// </summary>
    private static string SharedCurrency(IReadOnlyList<ErpHeadOfficeMoney.Slice> slices)
    {
        string currency = "";
        foreach (var slice in slices)
        {
            var code = ErpHeadOfficeMoney.Code(slice.Currency);
            if (code.Length != 3)
            {
                continue;
            }

            if (currency.Length == 0)
            {
                currency = code;
            }
            else if (!currency.Equals(code, StringComparison.Ordinal))
            {
                return "";
            }
        }

        return currency.Length == 3 ? currency : "";
    }

    private static List<RawLine> HeadOfficeRows(ErpHeadOfficeMoney.Result result)
    {
        var rows = new List<RawLine>();
        foreach (var line in result.Lines)
        {
            var debit = line.Amount > 0m ? line.Amount : 0m;
            var credit = line.Amount < 0m ? -line.Amount : 0m;
            rows.Add(new RawLine(line.Code, line.Name, line.Type, "debit", 0m, debit, credit));
        }

        return rows;
    }

    /// <summary>
    /// Per-currency operating slices for consolidation. When the tenant has a
    /// member register, only 'full' members (plus unscoped journals) are summed —
    /// 'excluded' entities stay off the consolidated statements.
    /// </summary>
    private static async Task<List<ErpHeadOfficeMoney.Slice>> LoadCurrencySlicesAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var fullMembers = await FullMemberEntityIdsAsync(connection, cancellationToken).ConfigureAwait(false);
        var memberClause = fullMembers is null
            ? string.Empty
            : fullMembers.Count == 0
                ? " AND IFNULL(j.`company_id`, 0) = 0 AND IFNULL(l.`legal_entity_id`, 0) = 0"
                : " AND ((IFNULL(j.`company_id`, 0) = 0 AND IFNULL(l.`legal_entity_id`, 0) = 0)"
                    + " OR IFNULL(j.`company_id`, 0) IN (" + string.Join(',', fullMembers) + ")"
                    + " OR IFNULL(l.`legal_entity_id`, 0) IN (" + string.Join(',', fullMembers) + "))";
        try
        {
            return await ReadCurrencySlicesAsync(connection, true, memberClause, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return await ReadCurrencySlicesAsync(connection, false, memberClause, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<List<ErpHeadOfficeMoney.Slice>> ReadCurrencySlicesAsync(
        DbConnection connection,
        bool withEntity,
        string memberClause,
        CancellationToken cancellationToken)
    {
        var currency = withEntity
            ? """
              UPPER(SUBSTRING(TRIM(CASE
                  WHEN CHAR_LENGTH(TRIM(IFNULL(j.`currency_code`, ''))) >= 3 THEN j.`currency_code`
                  WHEN CHAR_LENGTH(TRIM(IFNULL(e.`currency_code`, ''))) >= 3 THEN e.`currency_code`
                  ELSE 'AED' END), 1, 3))
              """
            : """
              UPPER(SUBSTRING(TRIM(CASE
                  WHEN CHAR_LENGTH(TRIM(IFNULL(j.`currency_code`, ''))) >= 3 THEN j.`currency_code`
                  ELSE 'AED' END), 1, 3))
              """;
        var entity = withEntity
            ? " LEFT JOIN `epc_erp_pm_legal_entities` e ON e.`id` = IFNULL(NULLIF(l.`legal_entity_id`, 0), j.`company_id`) AND e.`active` = 1"
            : "";
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT IFNULL(a.`code`, ''), IFNULL(a.`name`, ''), IFNULL(a.`account_type`, ''), IFNULL(a.`normal_side`, 'debit'),"
            + " IFNULL(SUM(l.`debit`), 0), IFNULL(SUM(l.`credit`), 0), " + currency
            + " FROM `epc_erp_coa_accounts` a"
            + " INNER JOIN `epc_erp_gl_lines` l ON l.`coa_id` = a.`id`"
            + " INNER JOIN `epc_erp_gl_journals` j ON j.`id` = l.`journal_id` AND j.`active` = 1"
            + entity
            + " WHERE a.`active` = 1" + memberClause
            + " GROUP BY a.`id`, a.`code`, a.`name`, a.`account_type`, a.`normal_side`, " + currency;
        var rows = new List<ErpHeadOfficeMoney.Slice>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var side = reader.GetString(3);
            var debits = reader.GetDecimal(4);
            var credits = reader.GetDecimal(5);
            var amount = string.Equals(side, "credit", StringComparison.OrdinalIgnoreCase)
                ? credits - debits
                : debits - credits;
            if (Math.Abs(amount) < 0.005m)
            {
                continue;
            }

            rows.Add(new ErpHeadOfficeMoney.Slice(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2).Trim().ToLowerInvariant(),
                reader.GetString(6),
                amount));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ErpHeadOfficeMoney.Rate>> LoadRatesAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var rates = new List<ErpHeadOfficeMoney.Rate>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT IFNULL(`base_currency`, ''), IFNULL(`target_currency`, ''), `rate`
            FROM `epc_fx_rates`
            WHERE `rate` > 0
            ORDER BY `effective_date`, `id`
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rates.Add(new ErpHeadOfficeMoney.Rate(reader.GetString(0), reader.GetString(1), reader.GetDecimal(2)));
        }

        return rates;
    }

    private static async Task<string> AccountingCurrencyAsync(
        DbConnection connection,
        int companyId,
        CancellationToken cancellationToken)
    {
        if (companyId <= 0)
        {
            return "AED";
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT IFNULL(`currency_code`, '') FROM `epc_erp_pm_legal_entities`
                WHERE `id` = @id AND `active` = 1 LIMIT 1
                """;
            Add(command, "@id", companyId);
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var code = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim().ToUpperInvariant() ?? "";
            return code.Length == 3 ? code : "AED";
        }
        catch (DbException)
        {
            return "AED";
        }
    }

    private static async Task<(decimal Rate, string Reporting, string Note)> ResolveRateAsync(
        DbConnection connection,
        string accounting,
        string requested,
        CancellationToken cancellationToken)
    {
        if (requested.Length == 0 || requested.Equals(accounting, StringComparison.OrdinalIgnoreCase))
        {
            return (1m, accounting, string.Empty);
        }

        try
        {
            var direct = await RateAsync(connection, accounting, requested, cancellationToken).ConfigureAwait(false);
            if (direct is > 0)
            {
                return (direct.Value, requested, string.Empty);
            }

            var back = await RateAsync(connection, requested, accounting, cancellationToken).ConfigureAwait(false);
            if (back is > 0)
            {
                return (Round8(1m / back.Value), requested, string.Empty);
            }
        }
        catch (DbException)
        {
            // Rates stay in the accounting currency.
        }

        return (1m, accounting, "No exchange rate from " + accounting + " to " + requested + ". Amounts stay in " + accounting + ".");
    }

    private static async Task<decimal?> RateAsync(
        DbConnection connection,
        string from,
        string to,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT `rate` FROM `epc_fx_rates`
            WHERE `base_currency` = @from AND `target_currency` = @to AND `rate` > 0
            ORDER BY `effective_date` DESC, `id` DESC
            LIMIT 1
            """;
        Add(command, "@from", from);
        Add(command, "@to", to);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (value is null or DBNull)
        {
            return null;
        }

        return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyList<ErpFinancialStatementUnit>> LoadUnitsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT `id`, IFNULL(`code`, ''), IFNULL(`name`, '')
                FROM `epc_erp_pm_business_units`
                WHERE `active` = 1
                ORDER BY `code`
                LIMIT 80
                """;
            var rows = new List<ErpFinancialStatementUnit>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
            }

            return rows;
        }
        catch (DbException)
        {
            return [];
        }
    }

    private static string UnitLabel(IReadOnlyList<ErpFinancialStatementUnit> units, long id)
    {
        var unit = units.FirstOrDefault(row => row.Id == id);
        if (unit is null)
        {
            return id > 0
                ? "Business unit " + id.ToString(CultureInfo.InvariantCulture)
                : "Business unit";
        }

        return string.IsNullOrWhiteSpace(unit.Name) ? unit.Code : unit.Code + " · " + unit.Name;
    }

    private static decimal Round8(decimal value)
        => Math.Round(value, 8, MidpointRounding.AwayFromZero);

    private static void Add(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static ErpFinancialStatement Empty(
        ErpFinancialStatementQuery query,
        string source,
        string message,
        IReadOnlyList<ErpFinancialStatementUnit>? units = null)
        => new(
            query,
            "AED",
            string.IsNullOrWhiteSpace(query.ReportingCurrency) ? "AED" : query.ReportingCurrency,
            1m,
            string.Empty,
            [],
            0, 0, 0, 0, 0, 0, 0,
            query.Level.ToString(),
            source,
            message,
            units ?? []);

    private sealed record RawLine(
        string Code,
        string Name,
        string Type,
        string Side,
        decimal Opening,
        decimal Debits,
        decimal Credits);
}
