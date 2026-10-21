using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public interface IErpTrialBalanceReadService
{
    Task<ErpTrialBalanceResult> ReadAsync(
        ErpTrialBalanceParameters parameters,
        CancellationToken cancellationToken = default);
}

public sealed class ErpTrialBalanceReadService : IErpTrialBalanceReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpTrialBalanceReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpTrialBalanceResult> ReadAsync(
        ErpTrialBalanceParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (!_connections.IsConfigured)
        {
            return Empty(parameters, "migration", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpGlPostingService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureTrialBalanceSnapshotsAsync(connection, cancellationToken).ConfigureAwait(false);

        var loaded = await LoadLinesAsync(connection, parameters, cancellationToken).ConfigureAwait(false);
        var lines = loaded.Lines;
        var profit = ErpIfrs18Face.PeriodProfit(loaded.Profit);
        var vouchers = string.IsNullOrWhiteSpace(parameters.AccountCode)
            ? Array.Empty<ErpTrialBalanceVoucher>()
            : await LoadVouchersAsync(connection, parameters, cancellationToken).ConfigureAwait(false);
        var snapshots = await LoadSnapshotsAsync(connection, cancellationToken).ConfigureAwait(false);
        var opening = lines.Sum(l => l.Opening);
        var debit = lines.Sum(l => l.Debit);
        var credit = lines.Sum(l => l.Credit);
        var closing = lines.Sum(l => l.Closing);
        var message = parameters.Consolidated
            ? "Consolidated trial balance — only 'full' consolidation group members contribute. Unscoped journals count as group-level. "
            : ErpTrialBalanceParameters.UsesUnitLedger(parameters.DimensionSet, parameters.BusinessUnitId)
            ? "Business unit ledger uses posted journals only. Company opening balances stay on the company trial balance. A journal across units keeps each unit in balance with the intercompany receivable and payable."
            : string.Empty;
        const string profitNote = "Income, expenses, and gross profit use this date range. The year-end close stays on retained earnings.";
        message = message.Length == 0 ? profitNote : message + " " + profitNote;

        return new(
            parameters,
            lines,
            vouchers,
            snapshots,
            opening,
            debit,
            credit,
            closing,
            "live",
            message,
            profit.Income,
            profit.Expense,
            profit.GrossProfit,
            profit.NetProfit);
    }

    private static async Task<LoadedLines> LoadLinesAsync(
        DbConnection connection,
        ErpTrialBalanceParameters parameters,
        CancellationToken cancellationToken)
    {
        var fullMembers = parameters.Consolidated
            ? await ErpFinancialStatementReadService.FullMemberEntityIdsAsync(connection, cancellationToken).ConfigureAwait(false)
            : null;
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT IFNULL(a.`code`, '') AS code,
                   IFNULL(a.`name`, '') AS name,
                   IFNULL(a.`account_type`, '') AS account_type,
                   IFNULL(a.`normal_side`, '') AS normal_side,
                   IFNULL(a.`opening_balance`, 0) AS opening_balance,
                   IFNULL(j.`company_id`, 0) AS journal_company,
                   IFNULL(l.`legal_entity_id`, 0) AS line_entity,
                   IFNULL(NULLIF(l.`business_unit_id`, 0), IFNULL(j.`business_unit_id`, 0)) AS business_unit_id,
                   IFNULL(j.`journal_no`, '') AS journal_no,
                   IFNULL(j.`source_type`, '') AS source_type,
                   IFNULL(j.`reference`, '') AS reference,
                   IFNULL(j.`description`, '') AS description,
                   IFNULL(j.`uae_tax_legislation_ref`, '') AS legislation_ref,
                   CASE WHEN j.`journal_date` IS NULL THEN 0
                        WHEN j.`journal_date` < 100000000 THEN UNIX_TIMESTAMP(STR_TO_DATE(CAST(j.`journal_date` AS CHAR), '%Y%m%d'))
                        ELSE j.`journal_date` END AS journal_unix,
                   IFNULL(l.`debit`, 0) AS debit,
                   IFNULL(l.`credit`, 0) AS credit
            FROM `epc_erp_coa_accounts` a
            LEFT JOIN `epc_erp_gl_lines` l ON l.`coa_id` = a.`id`
            LEFT JOIN `epc_erp_gl_journals` j ON j.`id` = l.`journal_id` AND j.`active` = 1
            WHERE a.`active` = 1
            ORDER BY a.`code` ASC, j.`company_id` ASC
            """;
        var buckets = new Dictionary<string, LineAcc>(StringComparer.OrdinalIgnoreCase);
        var profit = new List<(string Name, string Type, decimal SignedAmount)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var code = ReadString(reader, "code");
            var name = ReadString(reader, "name");
            var type = ReadString(reader, "account_type");
            var side = ReadString(reader, "normal_side");
            var openingBalance = ReadDec(reader, "opening_balance");
            var journalCompany = ReadLong(reader, "journal_company");
            var lineEntity = ReadLong(reader, "line_entity");
            var companyId = lineEntity > 0 ? lineEntity : journalCompany;
            if (parameters.CompanyId > 0 && journalCompany == parameters.CompanyId)
            {
                companyId = parameters.CompanyId;
            }

            var businessUnitId = ReadLong(reader, "business_unit_id");
            var journalUnix = ReadLong(reader, "journal_unix");
            var unitLedger = ErpTrialBalanceParameters.UsesUnitLedger(parameters.DimensionSet, parameters.BusinessUnitId);
            if (journalUnix <= 0)
            {
                if (unitLedger)
                {
                    continue;
                }
            }
            else if (parameters.Consolidated && fullMembers is not null
                     && !InConsolidatedPerimeter(journalCompany, lineEntity, fullMembers))
            {
                continue;
            }
            else if (!ErpAccountClass.InCompany(journalCompany, lineEntity, parameters.CompanyId))
            {
                continue;
            }
            else if (parameters.BusinessUnitId > 0 && businessUnitId != parameters.BusinessUnitId)
            {
                continue;
            }

            var dimension = parameters.DimensionSet switch
            {
                ErpTrialBalanceDimensionSet.MainAccountCompany when companyId > 0 => companyId.ToString(CultureInfo.InvariantCulture),
                ErpTrialBalanceDimensionSet.MainAccountBusinessUnit => businessUnitId.ToString(CultureInfo.InvariantCulture),
                _ => "",
            };
            var key = parameters.DimensionSet == ErpTrialBalanceDimensionSet.MainAccount
                ? code
                : code + "|" + dimension;
            if (!buckets.TryGetValue(key, out var acc))
            {
                acc = new LineAcc(code, name, type, side, unitLedger ? 0m : openingBalance, dimension);
                buckets[key] = acc;
            }

            if (journalUnix <= 0)
            {
                continue;
            }

            var source = ReadString(reader, "source_type");
            var journalNo = ReadString(reader, "journal_no");
            var reference = ReadString(reader, "reference");
            var description = ReadString(reader, "description");
            var legislation = ReadString(reader, "legislation_ref");
            if (!parameters.AcceptsJournal(source, journalNo, reference, description, legislation))
            {
                continue;
            }

            var debit = ReadDec(reader, "debit");
            var credit = ReadDec(reader, "credit");
            if (journalUnix < parameters.FromUnix)
            {
                acc.OpeningDebits += debit;
                acc.OpeningCredits += credit;
            }
            else if (journalUnix <= parameters.ToUnix)
            {
                acc.PeriodDebits += debit;
                acc.PeriodCredits += credit;
                if (!ErpAccountClass.IsProfitAndLossClose(source, journalNo, reference, description))
                {
                    var signed = string.Equals(side, "credit", StringComparison.OrdinalIgnoreCase)
                        ? credit - debit
                        : debit - credit;
                    if (signed != 0m && parameters.MatchesFilter(code, name))
                    {
                        profit.Add((name, ErpAccountClass.Normalize(type, code), signed));
                    }
                }
            }
        }

        var lines = buckets.Values
            .Where(a => parameters.MatchesFilter(a.Code, a.Name))
            .Select(a => ErpTrialBalanceParameters.BuildLine(
                a.Code, a.Name, a.AccountType, a.NormalSide,
                a.OpeningBalance, a.OpeningDebits, a.OpeningCredits,
                a.PeriodDebits, a.PeriodCredits, a.Company, parameters.OpeningDisplay))
            .OrderBy(l => l.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(l => l.Company, StringComparer.OrdinalIgnoreCase)
            .Take(parameters.Limit)
            .ToList();
        return new LoadedLines(lines, profit);
    }

    private static async Task<IReadOnlyList<ErpTrialBalanceVoucher>> LoadVouchersAsync(
        DbConnection connection,
        ErpTrialBalanceParameters parameters,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT j.`id`,
                   IFNULL(j.`journal_no`, '') AS journal_no,
                   CASE WHEN j.`journal_date` < 100000000 THEN UNIX_TIMESTAMP(STR_TO_DATE(CAST(j.`journal_date` AS CHAR), '%Y%m%d'))
                        ELSE j.`journal_date` END AS journal_unix,
                   IFNULL(j.`source_type`, '') AS source_type,
                   IFNULL(j.`reference`, '') AS reference,
                   IFNULL(j.`description`, '') AS description,
                   IFNULL(j.`uae_tax_legislation_ref`, '') AS legislation_ref,
                   IFNULL(l.`debit`, 0) AS debit,
                   IFNULL(l.`credit`, 0) AS credit
            FROM `epc_erp_gl_lines` l
            INNER JOIN `epc_erp_gl_journals` j ON j.`id` = l.`journal_id` AND j.`active` = 1
            INNER JOIN `epc_erp_coa_accounts` a ON a.`id` = l.`coa_id`
            WHERE a.`code` = @code
              AND (@bu = 0 OR IFNULL(NULLIF(l.`business_unit_id`, 0), IFNULL(j.`business_unit_id`, 0)) = @bu)
              AND (@company = 0 OR IFNULL(j.`company_id`, 0) = @company OR IFNULL(l.`legal_entity_id`, 0) = @company)
            ORDER BY journal_unix DESC, j.`id` DESC
            LIMIT 200
            """;
        var p = command.CreateParameter();
        p.ParameterName = "@code";
        p.Value = parameters.AccountCode ?? "";
        command.Parameters.Add(p);
        var bu = command.CreateParameter();
        bu.ParameterName = "@bu";
        bu.Value = parameters.BusinessUnitId;
        command.Parameters.Add(bu);
        var company = command.CreateParameter();
        company.ParameterName = "@company";
        company.Value = parameters.CompanyId;
        command.Parameters.Add(company);

        var rows = new List<ErpTrialBalanceVoucher>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var source = ReadString(reader, "source_type");
            var journalNo = ReadString(reader, "journal_no");
            var reference = ReadString(reader, "reference");
            var description = ReadString(reader, "description");
            var legislation = ReadString(reader, "legislation_ref");
            if (!parameters.AcceptsJournal(source, journalNo, reference, description, legislation))
            {
                continue;
            }

            var unix = ReadLong(reader, "journal_unix");
            if (unix < parameters.FromUnix || unix > parameters.ToUnix)
            {
                continue;
            }

            rows.Add(new ErpTrialBalanceVoucher(
                ReadLong(reader, "id"),
                journalNo,
                unix,
                source,
                reference,
                description,
                legislation,
                ReadDec(reader, "debit"),
                ReadDec(reader, "credit")));
        }

        return rows;
    }

    internal static async Task<IReadOnlyList<ErpTrialBalanceSnapshotRow>> LoadSnapshotsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT `id`,
                   IFNULL(`tracking_id`, '') AS tracking_id,
                   IFNULL(`date_from`, '') AS date_from,
                   IFNULL(`date_to`, '') AS date_to,
                   IFNULL(`posting_layer`, '') AS posting_layer,
                   IFNULL(`dimension_set`, '') AS dimension_set,
                   IFNULL(`row_count`, 0) AS row_count,
                   IFNULL(`opening_total`, 0) AS opening_total,
                   IFNULL(`debit_total`, 0) AS debit_total,
                   IFNULL(`credit_total`, 0) AS credit_total,
                   IFNULL(`closing_total`, 0) AS closing_total,
                   IFNULL(`time_created`, 0) AS time_created
            FROM `epc_erp_tb_snapshots`
            ORDER BY `id` DESC
            LIMIT 40
            """;
        var rows = new List<ErpTrialBalanceSnapshotRow>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpTrialBalanceSnapshotRow(
                    ReadLong(reader, "id"),
                    ReadString(reader, "tracking_id"),
                    ReadString(reader, "date_from"),
                    ReadString(reader, "date_to"),
                    ReadString(reader, "posting_layer"),
                    ReadString(reader, "dimension_set"),
                    (int)ReadLong(reader, "row_count"),
                    ReadDec(reader, "opening_total"),
                    ReadDec(reader, "debit_total"),
                    ReadDec(reader, "credit_total"),
                    ReadDec(reader, "closing_total"),
                    ReadLong(reader, "time_created")));
            }
        }
        catch (DbException)
        {
            return [];
        }

        return rows;
    }

    private static ErpTrialBalanceResult Empty(ErpTrialBalanceParameters parameters, string source, string message)
        => new(parameters, [], [], [], 0, 0, 0, 0, source, message);

    private static string ReadString(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? "" : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? "";
    }

    /// <summary>Same perimeter the consolidated statement uses: unscoped rows count,
    /// entity-stamped rows only when the entity is a 'full' group member.</summary>
    private static bool InConsolidatedPerimeter(long journalCompany, long lineEntity, IReadOnlyCollection<long> fullMembers)
        => fullMembers.Count == 0
            ? journalCompany == 0 && lineEntity == 0
            : (journalCompany == 0 && lineEntity == 0) || fullMembers.Contains(journalCompany) || fullMembers.Contains(lineEntity);

    private static long ReadLong(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static decimal ReadDec(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private sealed record LoadedLines(
        IReadOnlyList<ErpTrialBalanceLine> Lines,
        IReadOnlyList<(string Name, string Type, decimal SignedAmount)> Profit);

    private sealed class LineAcc
    {
        public LineAcc(string code, string name, string accountType, string normalSide, decimal openingBalance, string company)
        {
            Code = code;
            Name = name;
            AccountType = accountType;
            NormalSide = normalSide;
            OpeningBalance = openingBalance;
            Company = company;
        }

        public string Code { get; }
        public string Name { get; }
        public string AccountType { get; }
        public string NormalSide { get; }
        public decimal OpeningBalance { get; }
        public string Company { get; }
        public decimal OpeningDebits { get; set; }
        public decimal OpeningCredits { get; set; }
        public decimal PeriodDebits { get; set; }
        public decimal PeriodCredits { get; set; }
    }
}
