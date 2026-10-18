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

        if (query.Level == ErpFinancialStatementLevel.Consolidation && headOffice is not { Applied: true } && !rateMissing)
        {
            var ledger = await LoadIcLedgerEliminationsAsync(connection, cancellationToken).ConfigureAwait(false);
            var useLedger = Math.Abs(ledger.Income) + Math.Abs(ledger.Expense) + Math.Abs(ledger.Asset) + Math.Abs(ledger.Liability) >= 0.005m;
            var (elimIncome, elimExpense, elimAsset, elimLiability) = useLedger
                ? ledger
                : await LoadEliminationsAsync(connection, cancellationToken).ConfigureAwait(false);
            AddElim(built, "ELIM-REV", "Intercompany revenue eliminated", "revenue", -elimIncome, ref income);
            AddElim(built, "ELIM-EXP", "Intercompany expense eliminated", "expense", -elimExpense, ref expense);
            AddElim(built, "ELIM-ASSET", "Intercompany asset eliminated", "asset", -elimAsset, ref assets);
            AddElim(built, "ELIM-LIAB", "Intercompany liability eliminated", "liability", -elimLiability, ref liabilities);
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

    private async Task<List<RawLine>> LoadAccountsAsync(
        DbConnection connection,
        ErpFinancialStatementQuery query,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        var filter = query.Level switch
        {
            ErpFinancialStatementLevel.BusinessUnit => " AND IFNULL(NULLIF(l.`business_unit_id`, 0), IFNULL(j.`business_unit_id`, 0)) = @unitId",
            ErpFinancialStatementLevel.Company when query.CompanyId > 0 => " AND (IFNULL(j.`company_id`, 0) IN (0, @companyId) OR IFNULL(l.`legal_entity_id`, 0) = @companyId)",
            _ => string.Empty,
        };
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

    private static async Task<(decimal Income, decimal Expense, decimal Asset, decimal Liability)> LoadIcLedgerEliminationsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            var purposes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var rules = connection.CreateCommand())
            {
                rules.CommandText = """
                    SELECT IFNULL(`debit_purpose`, '') AS debit_purpose,
                           IFNULL(`credit_purpose`, '') AS credit_purpose
                    FROM `epc_erp_ic_elim_rules`
                    WHERE `active` = 1 AND `method` = 'full'
                    """;
                await using var reader = await rules.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    AddPurpose(purposes, reader.GetString(0));
                    AddPurpose(purposes, reader.GetString(1));
                }
            }

            if (purposes.Count == 0)
            {
                return (0, 0, 0, 0);
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
                return (0, 0, 0, 0);
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

                balancesCommand.CommandText =
                    "SELECT IFNULL(a.`code`, '') AS code, IFNULL(a.`normal_side`, 'debit') AS normal_side,"
                    + " IFNULL(a.`opening_balance`, 0) AS opening_balance,"
                    + " IFNULL(SUM(CASE WHEN j.`id` IS NOT NULL THEN l.`debit` ELSE 0 END), 0) AS debits,"
                    + " IFNULL(SUM(CASE WHEN j.`id` IS NOT NULL THEN l.`credit` ELSE 0 END), 0) AS credits"
                    + " FROM `epc_erp_coa_accounts` a"
                    + " LEFT JOIN `epc_erp_gl_lines` l ON l.`coa_id` = a.`id`"
                    + " LEFT JOIN `epc_erp_gl_journals` j ON j.`id` = l.`journal_id` AND j.`active` = 1"
                    + " WHERE a.`code` IN (" + string.Join(",", names) + ")"
                    + " GROUP BY a.`id`, a.`code`, a.`normal_side`, a.`opening_balance`";
                await using var reader = await balancesCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    balances[reader.GetString(0)] = (reader.GetString(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4));
                }
            }

            decimal income = 0, expense = 0, asset = 0, liability = 0;
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

            return (income, expense, asset, liability);
        }
        catch (DbException)
        {
            return (0, 0, 0, 0);
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

    private static async Task<(decimal Income, decimal Expense, decimal Asset, decimal Liability)> LoadEliminationsAsync(
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

            return (income, expense, asset, liability);
        }
        catch (DbException)
        {
            return (0, 0, 0, 0);
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
            var currency = OneCurrency(slices);
            AddSlice(slices, "ELIM-REV", "Intercompany revenue eliminated", "revenue", currency, -elim.Income);
            AddSlice(slices, "ELIM-EXP", "Intercompany expense eliminated", "expense", currency, -elim.Expense);
            AddSlice(slices, "ELIM-ASSET", "Intercompany asset eliminated", "asset", currency, -elim.Asset);
            AddSlice(slices, "ELIM-LIAB", "Intercompany liability eliminated", "liability", currency, -elim.Liability);
            var rates = await LoadRatesAsync(connection, cancellationToken).ConfigureAwait(false);
            var result = ErpHeadOfficeMoney.Consolidate(slices, reporting, rates, profitAndLoss);
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

    private static string OneCurrency(IReadOnlyList<ErpHeadOfficeMoney.Slice> slices)
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
                return "AED";
            }
        }

        return currency.Length == 3 ? currency : "AED";
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

    private static async Task<List<ErpHeadOfficeMoney.Slice>> LoadCurrencySlicesAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ReadCurrencySlicesAsync(connection, true, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return await ReadCurrencySlicesAsync(connection, false, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<List<ErpHeadOfficeMoney.Slice>> ReadCurrencySlicesAsync(
        DbConnection connection,
        bool withEntity,
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
            + " WHERE a.`active` = 1"
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
