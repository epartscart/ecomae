using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Reads the open foreign receivables, the rates, and the asset register used
/// by the year-end currency, fraction, and depreciation entries. Missing tables
/// produce an empty snapshot so a tenant without those books can still close.
/// </summary>
public static class ErpFinancialCloseData
{
    public sealed record Snapshot(
        IReadOnlyList<ErpFinancialCloseBooks.MonetaryItem> Monetary,
        IReadOnlyDictionary<string, decimal> RevaluationAlready,
        decimal FractionAlready,
        IReadOnlyList<ErpFinancialCloseBooks.AssetRow> Assets,
        IReadOnlyList<string> OpenMonths);

    public static async Task<Snapshot> LoadAsync(
        DbConnection connection,
        DbTransaction? transaction,
        long yearStart,
        long yearEnd,
        CancellationToken cancellationToken)
    {
        var monetary = await ReceivablesAsync(connection, transaction, yearEnd, cancellationToken).ConfigureAwait(false);
        var revaluationAlready = await AlreadyIncreaseAsync(connection, transaction, "FY-FX-%", cancellationToken).ConfigureAwait(false);
        var fractionAlready = await FractionAlreadyAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var assets = await AssetsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var posted = await PostedMonthsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var open = ErpFinancialCloseBooks.UnpostedMonths(ErpFinancialCloseBooks.Months(yearStart, yearEnd), posted);
        return new Snapshot(monetary, revaluationAlready, fractionAlready, assets, open);
    }

    public static async Task SaveDepreciationAsync(
        DbConnection connection,
        DbTransaction transaction,
        ErpFinancialCloseBooks.DepreciationPlan plan,
        long runDate,
        int adminId,
        CancellationToken cancellationToken)
    {
        foreach (var update in plan.Updates)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "UPDATE `epc_erp_fa_assets` SET `accumulated_depreciation`=?, `book_value`=?, `status`=? WHERE `id`=? AND `status`='active'"),
                cancellationToken,
                update.Accumulated,
                update.BookValue,
                update.Status,
                update.Id).ConfigureAwait(false);
        }

        foreach (var month in plan.Months)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_fa_depreciation_runs` (`period_month`,`run_date`,`admin_id`,`note`,`total_amount`) VALUES (?,?,?,?,?)"),
                cancellationToken,
                month.Month,
                runDate,
                adminId,
                "Year-end depreciation",
                month.Amount).ConfigureAwait(false);
        }
    }

    private static async Task<IReadOnlyList<ErpFinancialCloseBooks.MonetaryItem>> ReceivablesAsync(
        DbConnection connection,
        DbTransaction? transaction,
        long asOf,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, transaction, "epc_einvoice_documents", cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        var basis = await BaseCurrencyAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var rows = new List<(string Currency, decimal Foreign, long Issued)>();
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = ErpDb.Positional(
                "SELECT `currency_code`, ROUND(`total_incl_vat` - `paid_amount`, 2), `issue_date`"
                + " FROM `epc_einvoice_documents`"
                + " WHERE `active`=1 AND `status`<>'cancelled'"
                + " AND `doc_category` IN ('tax_invoice','commercial_invoice')"
                + " AND UPPER(`currency_code`)<>?"
                + " AND ROUND(`total_incl_vat` - `paid_amount`, 2) > 0.005"
                + " AND `issue_date`<=?");
            ErpDb.AddParameters(command, basis, asOf);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add((
                    reader.IsDBNull(0) ? string.Empty : reader.GetString(0).Trim().ToUpperInvariant(),
                    Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture),
                    reader.IsDBNull(2) ? 0L : Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture)));
            }
        }
        catch (DbException)
        {
            return [];
        }

        var items = new List<ErpFinancialCloseBooks.MonetaryItem>();
        var cache = new Dictionary<string, decimal?>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row.Currency.Length == 0 || row.Foreign <= 0m)
            {
                continue;
            }

            var bookedRate = await CachedRateAsync(connection, transaction, cache, row.Currency, basis, row.Issued, cancellationToken).ConfigureAwait(false);
            var closingRate = await CachedRateAsync(connection, transaction, cache, row.Currency, basis, asOf, cancellationToken).ConfigureAwait(false);
            if (bookedRate is null or <= 0m || closingRate is null or <= 0m)
            {
                continue;
            }

            var exact = row.Foreign * closingRate.Value;
            items.Add(new ErpFinancialCloseBooks.MonetaryItem(
                ErpFinancialCloseBooks.ReceivableAccount,
                ErpFxRevaluationWriteService.CcyRound(row.Foreign * bookedRate.Value, basis),
                ErpFxRevaluationWriteService.CcyRound(exact, basis),
                exact,
                true));
        }

        return items;
    }

    private static async Task<decimal?> CachedRateAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Dictionary<string, decimal?> cache,
        string from,
        string to,
        long asOf,
        CancellationToken cancellationToken)
    {
        var key = from + ">" + to + "@" + asOf.ToString(CultureInfo.InvariantCulture);
        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var rate = await RateAsync(connection, transaction, from, to, asOf, cancellationToken).ConfigureAwait(false);
        cache[key] = rate;
        return rate;
    }

    private static async Task<decimal?> RateAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string from,
        string to,
        long asOf,
        CancellationToken cancellationToken)
    {
        if (from.Equals(to, StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        var direct = await RateRawAsync(connection, transaction, from, to, asOf, cancellationToken).ConfigureAwait(false);
        if (direct is > 0m)
        {
            return direct;
        }

        var inverse = await RateRawAsync(connection, transaction, to, from, asOf, cancellationToken).ConfigureAwait(false);
        return inverse is > 0m ? Math.Round(1m / inverse.Value, 8, MidpointRounding.AwayFromZero) : null;
    }

    private static async Task<decimal?> RateRawAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string from,
        string to,
        long asOf,
        CancellationToken cancellationToken)
    {
        var ccy = await ScalarDecimalAsync(
            connection,
            transaction,
            "SELECT `rate` FROM `epc_ccy_rates` WHERE `from_ccy`=? AND `to_ccy`=? AND `as_of`<=? AND `rate`>0 ORDER BY `as_of` DESC LIMIT 1",
            cancellationToken,
            from,
            to,
            asOf).ConfigureAwait(false);
        if (ccy is > 0m)
        {
            return ccy;
        }

        var day = DateTimeOffset.FromUnixTimeSeconds(asOf > 0 ? asOf : 0).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return await ScalarDecimalAsync(
            connection,
            transaction,
            "SELECT `rate` FROM `epc_fx_rates` WHERE `base_currency`=? AND `target_currency`=? AND `effective_date`<=? AND `rate`>0 ORDER BY `effective_date` DESC, `id` DESC LIMIT 1",
            cancellationToken,
            from,
            to,
            day).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyDictionary<string, decimal>> AlreadyIncreaseAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string referenceLike,
        CancellationToken cancellationToken)
    {
        var amounts = new Dictionary<string, decimal>(StringComparer.Ordinal);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = ErpDb.Positional(
                "SELECT a.`code`, COALESCE(SUM(l.`debit`),0), COALESCE(SUM(l.`credit`),0)"
                + " FROM `epc_erp_gl_lines` l"
                + " INNER JOIN `epc_erp_gl_journals` j ON j.`id`=l.`journal_id` AND j.`active`=1"
                + " INNER JOIN `epc_erp_coa_accounts` a ON a.`id`=l.`coa_id`"
                + " WHERE j.`reference` LIKE ?"
                + " GROUP BY a.`code`");
            ErpDb.AddParameters(command, referenceLike);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var code = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                if (code.Length == 0
                    || code == ErpFinancialCloseBooks.FxAccount
                    || code == ErpFinancialCloseBooks.FractionAccount)
                {
                    continue;
                }

                var debit = Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture);
                var credit = Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture);
                amounts[code] = ErpFiscalYearBooks.Round(debit - credit);
            }
        }
        catch (DbException)
        {
            return amounts;
        }

        return amounts;
    }

    private static async Task<decimal> FractionAlreadyAsync(
        DbConnection connection,
        DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var posted = await AlreadyIncreaseAsync(connection, transaction, "FY-FRAC-%", cancellationToken).ConfigureAwait(false);
        return posted.TryGetValue(ErpFinancialCloseBooks.ReceivableAccount, out var amount) ? amount : 0m;
    }

    private static async Task<IReadOnlyList<ErpFinancialCloseBooks.AssetRow>> AssetsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var assets = new List<ErpFinancialCloseBooks.AssetRow>();
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "SELECT `id`,`cost`,`salvage_value`,`accumulated_depreciation`,`useful_life_months`,`depreciation_method`"
                + " FROM `epc_erp_fa_assets` WHERE `status`='active'";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                assets.Add(new ErpFinancialCloseBooks.AssetRow(
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
                    reader.IsDBNull(5) ? "straight_line" : reader.GetString(5)));
            }
        }
        catch (DbException)
        {
            return [];
        }

        return assets;
    }

    private static async Task<IReadOnlyList<string>> PostedMonthsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var months = new List<string>();
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT `period_month` FROM `epc_erp_fa_depreciation_runs`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!reader.IsDBNull(0))
                {
                    months.Add(reader.GetString(0));
                }
            }
        }
        catch (DbException)
        {
            return [];
        }

        return months;
    }

    private static async Task<string> BaseCurrencyAsync(
        DbConnection connection,
        DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var value = await ScalarStringAsync(
            connection,
            transaction,
            "SELECT `base_currency` FROM `epc_ccy_config` ORDER BY `id` LIMIT 1",
            cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(value) ? "AED" : value.Trim().ToUpperInvariant();
    }

    private static async Task<decimal?> ScalarDecimalAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        CancellationToken cancellationToken,
        params object?[] parameters)
    {
        try
        {
            var value = await ErpDb.ScalarAsync(connection, transaction, ErpDb.Positional(sql), cancellationToken, parameters).ConfigureAwait(false);
            return value is null or DBNull ? null : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }
        catch (DbException)
        {
            return null;
        }
    }

    private static async Task<string?> ScalarStringAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ErpDb.StringAsync(connection, transaction, sql, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return null;
        }
    }

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string table,
        CancellationToken cancellationToken)
    {
        try
        {
            var count = await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=?"),
                cancellationToken,
                table).ConfigureAwait(false);
            return count > 0;
        }
        catch (DbException)
        {
            return false;
        }
    }
}
