using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_fin_fx_revalue</c> / ajax <c>fin_fx_revalue</c> twin.
/// Parses <c>account|currency|fc_amount|book_lc|rate</c> balance lines, computes
/// <c>revalued_lc = round(fc*rate,2)</c>, <c>delta = round(revalued_lc - book_lc,2)</c>,
/// and INSERTs one <c>epc_fin_fx_run</c> row. No GL posting (PHP parity).
/// Company comes from <c>?company=</c> / first active legal entity. Does not CREATE tables.
/// </summary>
public interface IErpFinFxRevalueWriteService
{
    Task<ErpSimpleWriteResult> RevalueAsync(
        ErpFinFxRevalueWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFinFxRevalueWriteRequest(string? Balances, long CompanyHint = 0, long AsOf = 0);

public sealed record ErpFinFxRevalueLine(
    string Account,
    string Currency,
    decimal FcAmount,
    decimal BookLc,
    decimal Rate,
    decimal RevaluedLc,
    decimal Delta,
    string Effect);

public sealed class ErpFinFxRevalueWriteService : IErpFinFxRevalueWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFinFxRevalueWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> RevalueAsync(
        ErpFinFxRevalueWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var lines = ParseBalances(request.Balances);
        if (lines.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Enter at least one balance line (account|currency|fc_amount|book_lc|rate)");
        }

        var totalDelta = TotalDelta(lines);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var asOf = request.AsOf > 0 ? request.AsOf : now;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_fin_fx_run", "lines_json", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "FX revaluation run table is not provisioned");
        }

        var companyId = await ErpFinAdvancedCompany.ResolveAsync(connection, request.CompanyHint, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_fin_fx_run` (`company_id`,`as_of`,`total_delta`,`lines_json`,`time_created`) VALUES (?,?,?,?,?)"),
            cancellationToken,
            companyId,
            asOf,
            totalDelta,
            EncodeLines(lines),
            now).ConfigureAwait(false);
        var runId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(Message(runId, totalDelta), runId);
    }

    public static string Message(long runId, decimal totalDelta)
        => "Revaluation run #" + runId.ToString(CultureInfo.InvariantCulture)
           + " — net delta " + totalDelta.ToString("#,##0.00", CultureInfo.InvariantCulture);

    /// <summary>PHP <c>epc_fin_fx_reval_delta</c>.</summary>
    public static (decimal RevaluedLc, decimal Delta) Delta(decimal fcAmount, decimal bookLc, decimal rate)
    {
        var revalued = Math.Round(fcAmount * rate, 2, MidpointRounding.AwayFromZero);
        return (revalued, Math.Round(revalued - bookLc, 2, MidpointRounding.AwayFromZero));
    }

    public static decimal TotalDelta(IReadOnlyList<ErpFinFxRevalueLine> lines)
    {
        var total = 0m;
        foreach (var line in lines)
        {
            total = Math.Round(total + line.Delta, 2, MidpointRounding.AwayFromZero);
        }

        return total;
    }

    /// <summary>PHP ajax <c>fin_fx_revalue</c> line parser: skips blank/no-pipe/&lt;5-part lines.</summary>
    public static IReadOnlyList<ErpFinFxRevalueLine> ParseBalances(string? text)
    {
        var lines = new List<ErpFinFxRevalueLine>();
        if (string.IsNullOrEmpty(text))
        {
            return lines;
        }

        foreach (var raw in text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
        {
            var line = raw.Trim();
            if (line.Length == 0 || !line.Contains('|', StringComparison.Ordinal))
            {
                continue;
            }

            var p = line.Split('|');
            if (p.Length < 5)
            {
                continue;
            }

            var fc = Num(p[2]);
            var book = Num(p[3]);
            var rate = Num(p[4]);
            var (revalued, delta) = Delta(fc, book, rate);
            lines.Add(new ErpFinFxRevalueLine(
                p[0].Trim(),
                p[1].Trim(),
                fc,
                book,
                rate,
                revalued,
                delta,
                delta >= 0 ? "gain" : "loss"));
        }

        return lines;
    }

    public static string EncodeLines(IReadOnlyList<ErpFinFxRevalueLine> lines)
        => JsonSerializer.Serialize(lines.Select(l => new
        {
            account = l.Account,
            currency = l.Currency,
            fc_amount = l.FcAmount,
            book_lc = l.BookLc,
            rate = l.Rate,
            revalued_lc = l.RevaluedLc,
            delta = l.Delta,
            effect = l.Effect,
        }));

    private static decimal Num(string s)
        => decimal.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
