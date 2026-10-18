using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public interface IErpFiscalYearLifecycleService
{
    Task<IReadOnlyList<ErpFiscalYearRow>> ListAsync(CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> ApplyAsync(
        ErpFiscalYearActionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFiscalYearActionRequest(string? Action, long YearId, long CompanyHint = 0, int AdminId = 0);

public sealed record ErpFiscalYearRow(
    long Id,
    string Label,
    long StartDate,
    long EndDate,
    string Status,
    decimal RetainedPl,
    int PeriodCount,
    decimal CurrencyRevaluation = 0m,
    decimal Fraction = 0m,
    decimal Depreciation = 0m);

/// <summary>
/// Hold, open, and close a fiscal year. Close posts the P&amp;L entry and opens
/// the next year with the balance sheet carried forward.
/// </summary>
public sealed class ErpFiscalYearLifecycleService : IErpFiscalYearLifecycleService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpGlPostingService _gl;
    private readonly IErpFyCreateWriteService _years;

    public ErpFiscalYearLifecycleService(
        IErpWriteConnectionFactory connections,
        IErpGlPostingService gl,
        IErpFyCreateWriteService years)
    {
        _connections = connections;
        _gl = gl;
        _years = years;
    }

    public async Task<IReadOnlyList<ErpFiscalYearRow>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpLazySchema.EnsurePeriodCloseAsync(connection, cancellationToken).ConfigureAwait(false);
            var rows = new List<ErpFiscalYearRow>();
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT y.`id`, y.`label`, y.`start_date`, y.`end_date`, y.`status`, y.`retained_pl`,"
                + " (SELECT COUNT(*) FROM `epc_fy_periods` p WHERE p.`year_id` = y.`id`)"
                + " FROM `epc_fy_years` y ORDER BY y.`start_date` DESC, y.`id` DESC";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpFiscalYearRow(
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture),
                    reader.IsDBNull(4) ? "open" : reader.GetString(4),
                    reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetValue(5), CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture)));
            }

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!row.Status.Equals("open", StringComparison.OrdinalIgnoreCase)
                    && !row.Status.Equals("hold", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var snap = await ErpFinancialCloseData.LoadAsync(connection, null, row.StartDate, row.EndDate, cancellationToken).ConfigureAwait(false);
                    var fxLines = ErpFinancialCloseBooks.Revaluation(snap.Monetary, snap.RevaluationAlready);
                    var fractionLines = ErpFinancialCloseBooks.Fraction(snap.Monetary, snap.FractionAlready);
                    var depreciation = ErpFinancialCloseBooks.Depreciate(snap.Assets, snap.OpenMonths);
                    rows[i] = row with
                    {
                        CurrencyRevaluation = ErpFinancialCloseBooks.SignedGain(fxLines, ErpFinancialCloseBooks.FxAccount),
                        Fraction = ErpFinancialCloseBooks.SignedGain(fractionLines, ErpFinancialCloseBooks.FractionAccount),
                        Depreciation = depreciation.Total,
                    };
                }
                catch (DbException)
                {
                    // The year list still opens when currency or asset tables are absent.
                }
            }

            return rows;
        }
        catch (DbException)
        {
            return [];
        }
    }

    public async Task<ErpSimpleWriteResult> ApplyAsync(
        ErpFiscalYearActionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var action = ErpFiscalYearBooks.Normalize(request.Action);
        if (!ErpFiscalYearBooks.IsAction(action))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose hold, open, or close.");
        }

        if (request.YearId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A fiscal year id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpLazySchema.EnsurePeriodCloseAsync(connection, cancellationToken).ConfigureAwait(false);
            var year = await LoadAsync(connection, request.YearId, cancellationToken).ConfigureAwait(false);
            if (year is null)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Fiscal year not found");
            }

            return action switch
            {
                "hold" => await HoldAsync(connection, year, cancellationToken).ConfigureAwait(false),
                "open" => await OpenAsync(connection, year, cancellationToken).ConfigureAwait(false),
                _ => await CloseAsync(connection, year, request, cancellationToken).ConfigureAwait(false),
            };
        }
        catch (ErpWriteException ex)
        {
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Fiscal year could not be updated.");
        }
    }

    private async Task<ErpSimpleWriteResult> HoldAsync(
        DbConnection connection,
        YearRow year,
        CancellationToken cancellationToken)
    {
        if (year.Status.Equals("hold", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Ok(year.Label + " is already on hold.", year.Id);
        }

        if (!year.Status.Equals("open", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Only an open year can be placed on hold.");
        }

        var updated = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_fy_years` SET `status`='hold' WHERE `id`=? AND `status`='open'"),
            cancellationToken,
            year.Id).ConfigureAwait(false);
        return updated == 1
            ? ErpSimpleWriteResult.Ok(year.Label + " is on hold. Journals in this year are refused until it is opened.", year.Id)
            : ErpSimpleWriteResult.Fail("invalid", "Only an open year can be placed on hold.");
    }

    private async Task<ErpSimpleWriteResult> OpenAsync(
        DbConnection connection,
        YearRow year,
        CancellationToken cancellationToken)
    {
        if (year.Status.Equals("open", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Ok(year.Label + " is already open.", year.Id);
        }

        if (year.Status.Equals("closed", StringComparison.OrdinalIgnoreCase))
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_fy_years` SET `status` = 'open', `closed_at` = 0 WHERE `id` = ? AND `status` = 'closed'"),
                cancellationToken,
                year.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(year.Label + " is open. The closing journal stays on the books until it is reversed.", year.Id);
        }

        var updated = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_fy_years` SET `status`='open' WHERE `id`=? AND `status`='hold'"),
            cancellationToken,
            year.Id).ConfigureAwait(false);
        return updated == 1
            ? ErpSimpleWriteResult.Ok(year.Label + " is open. Journals in this year can post.", year.Id)
            : ErpSimpleWriteResult.Fail("invalid", "This year cannot be opened.");
    }

    private async Task<ErpSimpleWriteResult> CloseAsync(
        DbConnection connection,
        YearRow year,
        ErpFiscalYearActionRequest request,
        CancellationToken cancellationToken)
    {
        if (!year.Status.Equals("open", StringComparison.OrdinalIgnoreCase)
            && !year.Status.Equals("hold", StringComparison.OrdinalIgnoreCase)
            && !year.Status.Equals("closed", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Year is not open");
        }

        await _gl.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await EnsureClearingAccountsAsync(connection, cancellationToken).ConfigureAwait(false);
        await EnsureAdjustmentAccountsAsync(connection, cancellationToken).ConfigureAwait(false);
        var companies = await CompanyIdsAsync(connection, cancellationToken).ConfigureAwait(false);
        var exclude = "FY-CLOSE-" + year.Id.ToString(CultureInfo.InvariantCulture) + "%";
        decimal net = 0m;
        var adjustments = "";

        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!year.Status.Equals("open", StringComparison.OrdinalIgnoreCase))
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional("UPDATE `epc_fy_years` SET `status`='open' WHERE `id`=? AND `status` IN ('hold','closed')"),
                    cancellationToken,
                    year.Id).ConfigureAwait(false);
            }

            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional(
                    "UPDATE `epc_fy_periods` SET `status`='open' WHERE `year_id`=? AND `start_date`<=? AND `end_date`>=? AND `status`='closed'"),
                cancellationToken,
                year.Id,
                year.EndDate,
                year.EndDate).ConfigureAwait(false);

            adjustments = await PostAdjustmentsAsync(connection, tx, year, companies[0], request, cancellationToken).ConfigureAwait(false);

            foreach (var companyId in companies)
            {
                var reference = JournalReference("FY-CLOSE", year.Id, companyId);
                var existing = await JournalIdAsync(connection, tx, "adjustment", year.Id, reference, cancellationToken).ConfigureAwait(false);
                var movements = await MovementsAsync(
                    connection,
                    tx,
                    companyId,
                    year.StartDate,
                    year.EndDate,
                    "('revenue','expense')",
                    exclude,
                    cancellationToken).ConfigureAwait(false);
                var entry = ErpFiscalYearBooks.CloseProfitAndLoss(movements);
                net += entry.NetProfit;
                if (existing != 0 || entry.Lines.Count < 2)
                {
                    continue;
                }

                var lines = await ToGlLinesAsync(connection, tx, entry.Lines, cancellationToken).ConfigureAwait(false);
                await _gl.PostJournalAsync(
                    connection,
                    tx,
                    new ErpGlJournalHeader
                    {
                        JournalDate = year.EndDate,
                        Reference = reference,
                        Description = ErpFyCloseWriteService.Memo(year.Label),
                        SourceType = "adjustment",
                        SourceId = year.Id,
                        CompanyId = companyId,
                    },
                    lines,
                    request.AdminId,
                    cancellationToken).ConfigureAwait(false);
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional(
                    "UPDATE `epc_fy_years` SET `status`='closed', `closed_at`=IF(`closed_at`>0, `closed_at`, ?), `retained_pl`=? WHERE `id`=?"),
                cancellationToken,
                now,
                net,
                year.Id).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional("UPDATE `epc_fy_periods` SET `status`='closed' WHERE `year_id`=? AND `status`='open'"),
                cancellationToken,
                year.Id).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            try
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbException)
            {
            }

            throw;
        }

        var opened = await FinishNextYearAsync(connection, year, companies, net, request, cancellationToken).ConfigureAwait(false);
        var outcome = ErpFyCloseWriteService.Outcome(net);
        var amount = net.ToString("#,##0.00", CultureInfo.InvariantCulture);
        var books = companies.Count > 1
            ? " across " + companies.Count.ToString(CultureInfo.InvariantCulture) + " legal entities"
            : string.Empty;
        var summary = year.Label + " is closed · " + outcome + " net P&L " + amount + books + ". " + adjustments + " " + opened.Message;
        return opened.Succeeded
            ? ErpSimpleWriteResult.Ok(summary, year.Id)
            : ErpSimpleWriteResult.Fail(opened.Code, summary);
    }

    /// <summary>
    /// Posts currency revaluation, the currency fraction, and depreciation on the
    /// first legal entity before profit and loss is cleared. A second close finds
    /// the same references and does not post them again.
    /// </summary>
    private async Task<string> PostAdjustmentsAsync(
        DbConnection connection,
        DbTransaction transaction,
        YearRow year,
        long companyId,
        ErpFiscalYearActionRequest request,
        CancellationToken cancellationToken)
    {
        ErpFinancialCloseData.Snapshot snapshot;
        try
        {
            snapshot = await ErpFinancialCloseData.LoadAsync(connection, transaction, year.StartDate, year.EndDate, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return "Currency revaluation, fraction, and depreciation were skipped.";
        }

        var fxReference = JournalReference("FY-FX", year.Id, companyId);
        var fractionReference = JournalReference("FY-FRAC", year.Id, companyId);
        var depreciationReference = JournalReference("FY-DEPR", year.Id, companyId);
        var fxLines = ErpFinancialCloseBooks.Revaluation(snapshot.Monetary, snapshot.RevaluationAlready);
        var fractionLines = ErpFinancialCloseBooks.Fraction(snapshot.Monetary, snapshot.FractionAlready);
        var depreciation = ErpFinancialCloseBooks.Depreciate(snapshot.Assets, snapshot.OpenMonths);

        var fxText = await PostOneAsync(
            connection,
            transaction,
            year,
            companyId,
            request,
            fxReference,
            "Year-end currency revaluation",
            fxLines,
            "Currency revaluation " + Money(ErpFinancialCloseBooks.SignedGain(fxLines, ErpFinancialCloseBooks.FxAccount)),
            cancellationToken).ConfigureAwait(false);
        var fractionText = await PostOneAsync(
            connection,
            transaction,
            year,
            companyId,
            request,
            fractionReference,
            "Year-end currency fraction",
            fractionLines,
            "Fraction " + Money(ErpFinancialCloseBooks.SignedGain(fractionLines, ErpFinancialCloseBooks.FractionAccount)),
            cancellationToken).ConfigureAwait(false);

        string depreciationText;
        if (await JournalIdAsync(connection, transaction, "adjustment", year.Id, depreciationReference, cancellationToken).ConfigureAwait(false) != 0)
        {
            depreciationText = "Depreciation is already posted.";
        }
        else if (depreciation.Lines.Count < 2)
        {
            depreciationText = "Depreciation 0.00.";
        }
        else
        {
            await PostJournalAsync(connection, transaction, year, companyId, request, depreciationReference, "Year-end depreciation", depreciation.Lines, cancellationToken).ConfigureAwait(false);
            await ErpFinancialCloseData.SaveDepreciationAsync(connection, transaction, depreciation, year.EndDate, request.AdminId, cancellationToken).ConfigureAwait(false);
            depreciationText = "Depreciation " + Money(depreciation.Total) + ".";
        }

        return fxText + " " + fractionText + " " + depreciationText;
    }

    private async Task<string> PostOneAsync(
        DbConnection connection,
        DbTransaction transaction,
        YearRow year,
        long companyId,
        ErpFiscalYearActionRequest request,
        string reference,
        string description,
        IReadOnlyList<ErpFyCloseEntryLine> lines,
        string postedText,
        CancellationToken cancellationToken)
    {
        if (await JournalIdAsync(connection, transaction, "adjustment", year.Id, reference, cancellationToken).ConfigureAwait(false) != 0)
        {
            return description + " is already posted.";
        }

        if (lines.Count < 2)
        {
            return postedText + ".";
        }

        await PostJournalAsync(connection, transaction, year, companyId, request, reference, description, lines, cancellationToken).ConfigureAwait(false);
        return postedText + ".";
    }

    private Task PostJournalAsync(
        DbConnection connection,
        DbTransaction transaction,
        YearRow year,
        long companyId,
        ErpFiscalYearActionRequest request,
        string reference,
        string description,
        IReadOnlyList<ErpFyCloseEntryLine> lines,
        CancellationToken cancellationToken)
        => PostReadyAsync(connection, transaction, year, companyId, request, reference, description, lines, cancellationToken);

    private async Task PostReadyAsync(
        DbConnection connection,
        DbTransaction transaction,
        YearRow year,
        long companyId,
        ErpFiscalYearActionRequest request,
        string reference,
        string description,
        IReadOnlyList<ErpFyCloseEntryLine> lines,
        CancellationToken cancellationToken)
    {
        var ready = await ToGlLinesAsync(connection, transaction, lines, cancellationToken).ConfigureAwait(false);
        await _gl.PostJournalAsync(
            connection,
            transaction,
            new ErpGlJournalHeader
            {
                JournalDate = year.EndDate,
                Reference = reference,
                Description = description,
                SourceType = "adjustment",
                SourceId = year.Id,
                CompanyId = companyId,
            },
            ready,
            request.AdminId,
            cancellationToken).ConfigureAwait(false);
    }

    private static string Money(decimal amount)
        => amount.ToString("#,##0.00", CultureInfo.InvariantCulture);

    private async Task<ErpSimpleWriteResult> FinishNextYearAsync(
        DbConnection connection,
        YearRow year,
        IReadOnlyList<long> companies,
        decimal netProfit,
        ErpFiscalYearActionRequest request,
        CancellationToken cancellationToken)
    {
        var nextLabel = ErpFiscalYearBooks.NextLabel(year.Label);
        var window = ErpFiscalYearBooks.NextWindow(year.StartDate, year.EndDate);
        var nextId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_fy_years` WHERE `label`=? LIMIT 1"),
            cancellationToken,
            nextLabel).ConfigureAwait(false);
        if (nextId <= 0)
        {
            var created = await _years.CreateAsync(
                new ErpFyCreateWriteRequest(nextLabel, window.Start, window.End, Monthly: true),
                cancellationToken).ConfigureAwait(false);
            if (!created.Succeeded)
            {
                return created;
            }

            nextId = created.Id;
        }

        await _gl.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        foreach (var companyId in companies)
        {
            var reference = JournalReference("FY-OPEN", nextId, companyId);
            var existing = await JournalIdAsync(connection, null, "opening", nextId, reference, cancellationToken).ConfigureAwait(false);
            if (existing != 0)
            {
                continue;
            }

            var balances = await MovementsAsync(
                connection,
                null,
                companyId,
                0,
                year.EndDate,
                "('asset','liability','equity')",
                null,
                cancellationToken).ConfigureAwait(false);
            var opening = ErpFiscalYearBooks.OpeningBalances(balances);
            if (opening.Count < 2)
            {
                continue;
            }

            try
            {
                var lines = await ToGlLinesAsync(connection, null, opening, cancellationToken).ConfigureAwait(false);
                await _gl.PostJournalAsync(
                    connection,
                    new ErpGlJournalHeader
                    {
                        JournalDate = window.Start,
                        Reference = reference,
                        Description = "Opening balances " + nextLabel,
                        SourceType = "opening",
                        SourceId = nextId,
                        CompanyId = companyId,
                    },
                    lines,
                    request.AdminId,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ErpWriteException ex)
            {
                return ErpSimpleWriteResult.Fail(
                    "invalid",
                    nextLabel + " is created, but the opening entry was refused: " + ex.Message);
            }
        }

        var profit = netProfit.ToString("#,##0.00", CultureInfo.InvariantCulture);
        return ErpSimpleWriteResult.Ok(
            nextLabel + " is open from the next day. Balance-sheet balances are the opening entry. Income and expense start at zero. Retained earnings moved by " + profit + ".",
            nextId);
    }

    private static string JournalReference(string prefix, long id, long companyId)
    {
        var text = prefix + "-" + id.ToString(CultureInfo.InvariantCulture);
        return companyId > 0
            ? text + "-" + companyId.ToString(CultureInfo.InvariantCulture)
            : text;
    }

    /// <summary>
    /// The fiscal year is shared by the tenant, so close writes one journal for every active legal entity.
    /// With no legal entity the books are one company.
    /// </summary>
    private static async Task<IReadOnlyList<long>> CompanyIdsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            if (!await ErpFinAdvancedCompany.ColumnExistsAsync(connection, "epc_erp_pm_legal_entities", "id", cancellationToken).ConfigureAwait(false))
            {
                return [0L];
            }

            var ids = new List<long>();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `active`=1 ORDER BY `id`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ids.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
            }

            return ids.Count == 0 ? [0L] : ids;
        }
        catch (DbException)
        {
            return [0L];
        }
    }

    private static async Task EnsureClearingAccountsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await InsertAccountAsync(connection, ErpFiscalYearBooks.RetainedAccount, "Retained earnings", "equity", "credit", "Year-end profit and loss", now, cancellationToken).ConfigureAwait(false);
        await InsertAccountAsync(connection, ErpFiscalYearBooks.ClearingAccount, "Profit and loss clearing", "equity", "credit", "Clears income and expense at year end", now, cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureAdjustmentAccountsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await InsertAccountAsync(connection, ErpFinancialCloseBooks.FxAccount, "Foreign exchange gain or loss", "expense", "debit", "Closing-rate difference on monetary items", now, cancellationToken).ConfigureAwait(false);
        await InsertAccountAsync(connection, ErpFinancialCloseBooks.FractionAccount, "Currency fraction", "expense", "debit", "Fil left when a foreign amount is rounded at the closing rate", now, cancellationToken).ConfigureAwait(false);
        await InsertAccountAsync(connection, ErpFinancialCloseBooks.DepreciationExpense, "Depreciation expense", "expense", "debit", "Period depreciation of fixed assets", now, cancellationToken).ConfigureAwait(false);
        await InsertAccountAsync(connection, ErpFinancialCloseBooks.AccumulatedDepreciation, "Accumulated depreciation", "asset", "credit", "Contra-asset: cumulative depreciation on fixed assets", now, cancellationToken).ConfigureAwait(false);
    }

    private static Task<int> InsertAccountAsync(
        DbConnection connection,
        string code,
        string name,
        string type,
        string side,
        string description,
        long now,
        CancellationToken cancellationToken)
        => ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_coa_accounts` (`code`,`name`,`account_type`,`normal_side`,`description`,`system_flag`,`time_created`)"
                + " SELECT ?,?,?,?,?,1,? FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM `epc_erp_coa_accounts` WHERE `code`=?)"),
            cancellationToken,
            code, name, type, side, description, now, code);

    private static async Task<IReadOnlyList<ErpFiscalYearBooks.Movement>> MovementsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        long companyId,
        long from,
        long to,
        string types,
        string? excludeReferencePrefix,
        CancellationToken cancellationToken)
    {
        var sql = "SELECT a.`code`, a.`account_type`, COALESCE(SUM(l.`debit`),0), COALESCE(SUM(l.`credit`),0)"
            + " FROM `epc_erp_gl_lines` l"
            + " INNER JOIN `epc_erp_gl_journals` j ON j.`id` = l.`journal_id` AND j.`active` = 1"
            + " INNER JOIN `epc_erp_coa_accounts` a ON a.`id` = l.`coa_id`"
            + " WHERE j.`journal_date` >= ? AND j.`journal_date` <= ? AND a.`account_type` IN " + types;
        if (companyId > 0)
        {
            sql += " AND j.`company_id` = ?";
        }

        if (!string.IsNullOrEmpty(excludeReferencePrefix))
        {
            sql += " AND j.`reference` NOT LIKE ?";
        }

        sql += " GROUP BY a.`code`, a.`account_type`";
        var rows = new List<ErpFiscalYearBooks.Movement>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional(sql);
        if (companyId > 0 && !string.IsNullOrEmpty(excludeReferencePrefix))
        {
            ErpDb.AddParameters(command, from, to, companyId, excludeReferencePrefix);
        }
        else if (companyId > 0)
        {
            ErpDb.AddParameters(command, from, to, companyId);
        }
        else if (!string.IsNullOrEmpty(excludeReferencePrefix))
        {
            ErpDb.AddParameters(command, from, to, excludeReferencePrefix);
        }
        else
        {
            ErpDb.AddParameters(command, from, to);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ErpFiscalYearBooks.Movement(
                reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture),
                Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ErpGlLine>> ToGlLinesAsync(
        DbConnection connection,
        DbTransaction? transaction,
        IReadOnlyList<ErpFyCloseEntryLine> lines,
        CancellationToken cancellationToken)
    {
        var result = new List<ErpGlLine>(lines.Count);
        foreach (var line in lines)
        {
            var id = await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code`=? AND `active`=1 LIMIT 1"),
                cancellationToken,
                line.Account).ConfigureAwait(false);
            if (id <= 0)
            {
                throw new ErpWriteException("Account " + line.Account + " is not on the chart of accounts.");
            }

            result.Add(new ErpGlLine(id, line.Debit, line.Credit, line.Account));
        }

        return result;
    }

    private static Task<long> JournalIdAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sourceType,
        long sourceId,
        string reference,
        CancellationToken cancellationToken)
        => ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_gl_journals` WHERE `source_type`=? AND `source_id`=? AND `reference`=? AND `active`=1 LIMIT 1"),
            cancellationToken,
            sourceType,
            sourceId,
            reference);

    private static async Task<YearRow?> LoadAsync(DbConnection connection, long yearId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `id`,`label`,`start_date`,`end_date`,`status`,`retained_pl` FROM `epc_fy_years` WHERE `id`=?");
        ErpDb.AddParameters(command, yearId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new YearRow(
            Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
            reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture),
            Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture),
            reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetValue(5), CultureInfo.InvariantCulture));
    }

    private sealed record YearRow(long Id, string Label, long StartDate, long EndDate, string Status, decimal RetainedPl);
}
