using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

public sealed record ErpFxRevaluationLine(
    long DocId,
    string Ref,
    string Currency,
    decimal OutstandingFc,
    decimal BookedRate,
    decimal CurrentRate,
    decimal BookedBase,
    decimal CurrentBase,
    decimal Unrealised);

public sealed record ErpFxRevaluationCurrency(
    string Currency,
    decimal OutstandingFc,
    decimal BookedBase,
    decimal CurrentBase,
    decimal Unrealised,
    int Count);

public sealed record ErpFxRevaluationPreview(
    long AsOf,
    string Base,
    IReadOnlyList<ErpFxRevaluationLine> Lines,
    IReadOnlyList<ErpFxRevaluationCurrency> ByCurrency,
    decimal TotalUnrealised);

public sealed record ErpFxRevaluationPostResult(
    long JournalId,
    long ReverseJournalId,
    decimal TotalUnrealised,
    string Base,
    string Message);

/// <summary>
/// Live ASP.NET port of PHP <c>epc_erp_fx_revaluation_preview</c> / <c>epc_erp_fx_post_revaluation</c>
/// (<c>content/shop/finance/epc_erp_ccy_revaluation.php</c>): IAS-21 retranslation of open
/// foreign-currency receivables at the as-of rate, posted as a balanced 1100/6900 journal with an
/// optional next-day auto-reversal. Rates follow <c>epc_ccy_get_rate</c> (direct, inverse, via base).
/// </summary>
public interface IErpFxRevaluationWriteService
{
    Task<ErpFxRevaluationPreview> PreviewAsync(long asOf, CancellationToken cancellationToken = default);

    Task<ErpFxRevaluationPostResult> PostAsync(
        long asOf,
        bool autoReverse,
        int adminId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpFxRevaluationWriteService : IErpFxRevaluationWriteService
{
    private const string ArCode = "1100";
    private const string FxCode = "6900";

    public const string OpenForeignArSql =
        "SELECT `id`, `invoice_number`, `issue_date`, `currency_code`, ROUND(`total_incl_vat` - `paid_amount`, 2) AS outstanding_fc"
        + " FROM `epc_einvoice_documents`"
        + " WHERE `active` = 1 AND `status` <> 'cancelled'"
        + " AND `doc_category` IN ('tax_invoice','commercial_invoice')"
        + " AND UPPER(`currency_code`) <> ?"
        + " AND ROUND(`total_incl_vat` - `paid_amount`, 2) > 0.005"
        + " AND `issue_date` <= ?"
        + " ORDER BY `currency_code` ASC, `id` ASC";

    private static readonly IReadOnlyDictionary<string, int> MinorUnits = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["BHD"] = 3, ["IQD"] = 3, ["JOD"] = 3, ["KWD"] = 3, ["LYD"] = 3, ["OMR"] = 3, ["TND"] = 3,
        ["BIF"] = 0, ["CLP"] = 0, ["DJF"] = 0, ["GNF"] = 0, ["ISK"] = 0, ["JPY"] = 0, ["KMF"] = 0, ["KRW"] = 0,
        ["PYG"] = 0, ["RWF"] = 0, ["UGX"] = 0, ["VND"] = 0, ["VUV"] = 0, ["XAF"] = 0, ["XOF"] = 0, ["XPF"] = 0,
    };

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpGlPostingService _gl;
    private readonly IErpGlLedgerWriteService _ledger;
    private readonly IErpAuditLogWriter _audit;

    public ErpFxRevaluationWriteService(
        IErpWriteConnectionFactory connections,
        IErpGlPostingService gl,
        IErpGlLedgerWriteService ledger,
        IErpAuditLogWriter audit)
    {
        _connections = connections;
        _gl = gl;
        _ledger = ledger;
        _audit = audit;
    }

    /// <summary>PHP <c>epc_ccy_round</c>: round to the currency's minor units (default 2).</summary>
    public static decimal CcyRound(decimal amount, string code)
        => Math.Round(amount, MinorUnits.TryGetValue(code.Trim().ToUpperInvariant(), out var d) ? d : 2, MidpointRounding.AwayFromZero);

    /// <summary>PHP <c>strtotime($as_of . ' 23:59:59')</c>; zero when the date is empty or invalid.</summary>
    public static long AsOfFromDate(string? date)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            return 0;
        }

        return DateTime.TryParseExact(
            date.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? new DateTimeOffset(parsed.Date, TimeSpan.Zero).AddDays(1).AddSeconds(-1).ToUnixTimeSeconds()
            : 0;
    }

    public static string PostedMessage(decimal total, string baseCcy, long journalId, long reverseJournalId)
        => "FX revaluation posted: unrealised " + (total > 0m ? "gain" : "loss") + " "
            + Math.Abs(total).ToString("#,##0.00", CultureInfo.InvariantCulture) + " " + baseCcy
            + " (journal #" + journalId.ToString(CultureInfo.InvariantCulture)
            + (reverseJournalId > 0 ? ", auto-reversal #" + reverseJournalId.ToString(CultureInfo.InvariantCulture) : string.Empty)
            + ")";

    public async Task<ErpFxRevaluationPreview> PreviewAsync(long asOf, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await PreviewAsync(connection, asOf, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ErpFxRevaluationPostResult> PostAsync(
        long asOf,
        bool autoReverse,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var preview = await PreviewAsync(connection, asOf, cancellationToken).ConfigureAwait(false);
        var total = preview.TotalUnrealised;
        if (Math.Abs(total) < 0.005m)
        {
            throw new ErpWriteException("No revaluation needed — net unrealised FX is zero.");
        }

        var arId = await CoaIdByCodeAsync(connection, ArCode, cancellationToken).ConfigureAwait(false);
        if (arId <= 0)
        {
            throw new ErpWriteException("AR control account (1100) not found.");
        }

        var fxId = await CoaIdByCodeAsync(connection, FxCode, cancellationToken).ConfigureAwait(false);
        if (fxId <= 0)
        {
            fxId = await _ledger.CreateCoaAccountAsync(
                new ErpCoaAccountInput
                {
                    Code = FxCode,
                    Name = "Foreign exchange gain/loss (unrealised)",
                    AccountType = "expense",
                    NormalSide = "debit",
                    Description = "IAS-21 period-end retranslation of monetary items",
                },
                adminId,
                cancellationToken).ConfigureAwait(false);
        }

        var amount = Math.Abs(total);
        var lines = total > 0m
            ? new[]
            {
                new ErpGlLine(arId, amount, 0m, "FX revaluation (gain) of open AR"),
                new ErpGlLine(fxId, 0m, amount, "Unrealised FX gain"),
            }
            : new[]
            {
                new ErpGlLine(fxId, amount, 0m, "Unrealised FX loss"),
                new ErpGlLine(arId, 0m, amount, "FX revaluation (loss) of open AR"),
            };

        var asOfDate = DateTimeOffset.FromUnixTimeSeconds(preview.AsOf).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var journalId = await _gl.PostJournalAsync(
            connection,
            new ErpGlJournalHeader
            {
                JournalDate = preview.AsOf,
                Reference = "FX-REVAL " + asOfDate,
                Description = "Period-end FX revaluation of open foreign-currency receivables (IAS-21)",
                SourceType = "adjustment",
            },
            lines,
            adminId,
            cancellationToken).ConfigureAwait(false);

        long reverseId = 0;
        if (autoReverse)
        {
            var reversed = await _ledger.ReverseJournalAsync(
                connection,
                journalId,
                preview.AsOf + 86400,
                "Auto-reversal of FX revaluation " + asOfDate,
                adminId,
                cancellationToken).ConfigureAwait(false);
            reverseId = reversed.JournalId;
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_fx_revaluations` (`as_of`,`base_ccy`,`total_unrealised`,`gl_journal_id`,`reverse_journal_id`,`detail_json`,`admin_id`,`time_created`)"
                + " VALUES (?,?,?,?,?,?,?,?)"),
            cancellationToken,
            preview.AsOf,
            preview.Base,
            total,
            journalId,
            reverseId,
            JsonSerializer.Serialize(preview.ByCurrency.Select(c => new
            {
                currency = c.Currency,
                outstanding_fc = c.OutstandingFc,
                booked_base = c.BookedBase,
                current_base = c.CurrentBase,
                unrealised = c.Unrealised,
                count = c.Count,
            })),
            adminId,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);

        await _audit.LogAsync(
            connection,
            null,
            adminId,
            "fx_revaluation",
            "gl_journal",
            journalId,
            "FX revaluation " + asOfDate + " net " + total.ToString("#,##0.00", CultureInfo.InvariantCulture) + " " + preview.Base,
            new Dictionary<string, string?>
            {
                ["as_of"] = asOfDate,
                ["total_unrealised"] = total.ToString("0.00", CultureInfo.InvariantCulture),
                ["reverse_journal_id"] = reverseId.ToString(CultureInfo.InvariantCulture),
            },
            cancellationToken).ConfigureAwait(false);

        return new ErpFxRevaluationPostResult(
            journalId,
            reverseId,
            total,
            preview.Base,
            PostedMessage(total, preview.Base, journalId, reverseId));
    }

    private static async Task<ErpFxRevaluationPreview> PreviewAsync(DbConnection connection, long asOf, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        asOf = asOf > 0 ? asOf : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var baseCcy = await BaseCurrencyAsync(connection, cancellationToken).ConfigureAwait(false);
        var lines = await OpenForeignArAsync(connection, baseCcy, asOf, cancellationToken).ConfigureAwait(false);

        var byCcy = new Dictionary<string, (decimal Fc, decimal Booked, decimal Current, decimal Unrealised, int Count)>(StringComparer.Ordinal);
        var order = new List<string>();
        var total = 0m;
        foreach (var line in lines)
        {
            if (!byCcy.TryGetValue(line.Currency, out var acc))
            {
                order.Add(line.Currency);
            }

            byCcy[line.Currency] = (
                acc.Fc + line.OutstandingFc,
                acc.Booked + line.BookedBase,
                acc.Current + line.CurrentBase,
                acc.Unrealised + line.Unrealised,
                acc.Count + 1);
            total += line.Unrealised;
        }

        return new ErpFxRevaluationPreview(
            asOf,
            baseCcy,
            lines,
            order.Select(c => new ErpFxRevaluationCurrency(c, byCcy[c].Fc, byCcy[c].Booked, byCcy[c].Current, byCcy[c].Unrealised, byCcy[c].Count)).ToList(),
            CcyRound(total, baseCcy));
    }

    private static async Task<IReadOnlyList<ErpFxRevaluationLine>> OpenForeignArAsync(
        DbConnection connection,
        string baseCcy,
        long asOf,
        CancellationToken cancellationToken)
    {
        var result = new List<ErpFxRevaluationLine>();
        var hasTable = await ErpDb.LongAsync(
            connection,
            null,
            "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'epc_einvoice_documents'",
            cancellationToken).ConfigureAwait(false);
        if (hasTable <= 0)
        {
            return result;
        }

        var rows = new List<(long Id, string Ref, long IssueDate, string Ccy, decimal OutstandingFc)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(OpenForeignArSql);
            AddParameter(command, "@p0", baseCcy.ToUpperInvariant());
            AddParameter(command, "@p1", asOf);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add((
                    reader.GetInt64(0),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    reader.IsDBNull(2) ? 0L : Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture),
                    reader.IsDBNull(3) ? string.Empty : reader.GetString(3).Trim().ToUpperInvariant(),
                    reader.IsDBNull(4) ? 0m : Convert.ToDecimal(reader.GetValue(4), CultureInfo.InvariantCulture)));
            }
        }

        foreach (var row in rows)
        {
            var bookedRate = await RateAsync(connection, row.Ccy, baseCcy, row.IssueDate, baseCcy, 0, cancellationToken).ConfigureAwait(false);
            var currentRate = await RateAsync(connection, row.Ccy, baseCcy, asOf, baseCcy, 0, cancellationToken).ConfigureAwait(false);
            if (bookedRate is null || currentRate is null)
            {
                continue;
            }

            var bookedBase = CcyRound(row.OutstandingFc * bookedRate.Value, baseCcy);
            var currentBase = CcyRound(row.OutstandingFc * currentRate.Value, baseCcy);
            result.Add(new ErpFxRevaluationLine(
                row.Id,
                row.Ref,
                row.Ccy,
                row.OutstandingFc,
                bookedRate.Value,
                currentRate.Value,
                bookedBase,
                currentBase,
                CcyRound(currentBase - bookedBase, baseCcy)));
        }

        return result;
    }

    /// <summary>PHP <c>epc_ccy_get_rate</c>: same-currency 1, direct, inverse, then triangulated via base.</summary>
    private static async Task<decimal?> RateAsync(
        DbConnection connection,
        string from,
        string to,
        long asOf,
        string baseCcy,
        int depth,
        CancellationToken cancellationToken)
    {
        if (from == to)
        {
            return 1m;
        }

        var direct = await RateRawAsync(connection, from, to, asOf, cancellationToken).ConfigureAwait(false);
        if (direct is not null)
        {
            return direct;
        }

        var inverse = await RateRawAsync(connection, to, from, asOf, cancellationToken).ConfigureAwait(false);
        if (inverse is not null && inverse.Value != 0m)
        {
            return Math.Round(1m / inverse.Value, 8, MidpointRounding.AwayFromZero);
        }

        if (depth == 0 && baseCcy != from && baseCcy != to)
        {
            var fb = await RateAsync(connection, from, baseCcy, asOf, baseCcy, 1, cancellationToken).ConfigureAwait(false);
            var bt = await RateAsync(connection, baseCcy, to, asOf, baseCcy, 1, cancellationToken).ConfigureAwait(false);
            if (fb is not null && bt is not null)
            {
                return Math.Round(fb.Value * bt.Value, 8, MidpointRounding.AwayFromZero);
            }
        }

        return null;
    }

    private static async Task<decimal?> RateRawAsync(DbConnection connection, string from, string to, long asOf, CancellationToken cancellationToken)
    {
        var value = await ErpDb.ScalarAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `rate` FROM `epc_ccy_rates` WHERE `from_ccy` = ? AND `to_ccy` = ? AND `as_of` <= ? ORDER BY `as_of` DESC LIMIT 1"),
            cancellationToken,
            from,
            to,
            asOf).ConfigureAwait(false);
        return value is null or DBNull ? null : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    private static async Task<string> BaseCurrencyAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var value = await ErpDb.StringAsync(
            connection,
            null,
            "SELECT `base_currency` FROM `epc_ccy_config` ORDER BY `id` LIMIT 1",
            cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(value) ? "AED" : value.Trim().ToUpperInvariant();
    }

    private static Task<long> CoaIdByCodeAsync(DbConnection connection, string code, CancellationToken cancellationToken)
        => ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = ? LIMIT 1"),
            cancellationToken,
            code);

    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            null,
            "CREATE TABLE IF NOT EXISTS `epc_ccy_config` (`id` int(11) NOT NULL AUTO_INCREMENT, `base_currency` varchar(3) NOT NULL DEFAULT 'AED',"
            + " `enabled_csv` text, `symbol_before` tinyint(1) NOT NULL DEFAULT 1, `time_updated` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`))"
            + " ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Tenant currency config (single row)'",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            "CREATE TABLE IF NOT EXISTS `epc_ccy_rates` (`id` int(11) NOT NULL AUTO_INCREMENT, `from_ccy` varchar(3) NOT NULL, `to_ccy` varchar(3) NOT NULL,"
            + " `rate` decimal(18,8) NOT NULL DEFAULT 0, `as_of` int(11) NOT NULL DEFAULT 0, `source` varchar(32) NOT NULL DEFAULT 'manual',"
            + " `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), UNIQUE KEY `x_pair_date` (`from_ccy`,`to_ccy`,`as_of`), KEY `x_pair` (`from_ccy`,`to_ccy`))"
            + " ENGINE=InnoDB DEFAULT CHARSET=utf8",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            "CREATE TABLE IF NOT EXISTS `epc_erp_fx_revaluations` (`id` int(11) NOT NULL AUTO_INCREMENT, `as_of` int(11) NOT NULL DEFAULT 0,"
            + " `base_ccy` varchar(8) NOT NULL DEFAULT 'AED', `total_unrealised` decimal(16,2) NOT NULL DEFAULT 0.00, `gl_journal_id` int(11) NOT NULL DEFAULT 0,"
            + " `reverse_journal_id` int(11) NOT NULL DEFAULT 0, `detail_json` mediumtext, `admin_id` int(11) NOT NULL DEFAULT 0, `time_created` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`), KEY `x_asof` (`as_of`)) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='BOS FX revaluation runs'",
            cancellationToken).ConfigureAwait(false);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private void EnsureConfigured()
    {
        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("TenantRegistry DB is not configured.");
        }
    }
}
