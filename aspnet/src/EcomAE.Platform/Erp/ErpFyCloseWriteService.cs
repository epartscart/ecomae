using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_fy_close_year</c> / ajax <c>fy_close</c> twin.
/// Loads the fiscal year by id, computes net P&amp;L through the PHP
/// <c>epc_erp_gl_pl_report</c> contract (0.0 when unavailable), rounds to 2dp,
/// builds the closing-entry payload (profit: Dr 3900 / Cr 3200; loss reversed),
/// marks the year closed and closes its open <c>epc_fy_periods</c>.
/// No GL journal is posted (PHP parity). Schema stays PHP-owned.
/// </summary>
public interface IErpFyCloseWriteService
{
    Task<ErpFyCloseWriteResult> CloseYearAsync(
        ErpFyCloseWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFyCloseWriteRequest(long YearId, long CompanyHint = 0);

public sealed record ErpFyCloseEntryLine(string Account, decimal Debit, decimal Credit);

public sealed record ErpFyCloseWriteResult(
    ErpSimpleWriteResult Result,
    long YearId,
    string Label,
    decimal NetPl,
    string Outcome,
    long EntryDate,
    string Memo,
    IReadOnlyList<ErpFyCloseEntryLine> ClosingEntry,
    int PeriodsClosed);

public sealed class ErpFyCloseWriteService : IErpFyCloseWriteService
{
    public const string RetainedAccount = "3200";
    public const string PlClearingAccount = "3900";

    private readonly IErpWriteConnectionFactory _connections;

    public ErpFyCloseWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpFyCloseWriteResult> CloseYearAsync(
        ErpFyCloseWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return Fail(request.YearId, "db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ErpFinAdvancedCompany.ColumnExistsAsync(connection, "epc_fy_years", "retained_pl", cancellationToken).ConfigureAwait(false)
            || !await ErpFinAdvancedCompany.ColumnExistsAsync(connection, "epc_fy_periods", "year_id", cancellationToken).ConfigureAwait(false))
        {
            return Fail(request.YearId, "db", "Fiscal year schema is not provisioned");
        }

        string? label = null;
        long startDate = 0;
        long endDate = 0;
        string? status = null;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(
                "SELECT `label`,`start_date`,`end_date`,`status` FROM `epc_fy_years` WHERE `id`=?");
            ErpDb.AddParameters(command, request.YearId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                label = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                startDate = Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
                endDate = Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture);
                status = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            }
        }

        if (label is null)
        {
            return Fail(request.YearId, "not_found", "Fiscal year not found");
        }

        if (!string.Equals(status, "open", StringComparison.Ordinal))
        {
            return Fail(request.YearId, "invalid", "Year is not open");
        }

        var netPl = await NetProfitAsync(connection, request.CompanyHint, startDate, endDate, cancellationToken).ConfigureAwait(false);
        var entry = ClosingEntry(netPl);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        int periodsClosed;
        await using (var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
        {
            var updated = await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional("UPDATE `epc_fy_years` SET `status`='closed', `closed_at`=?, `retained_pl`=? WHERE `id`=? AND `status`='open'"),
                cancellationToken,
                now,
                netPl,
                request.YearId).ConfigureAwait(false);
            if (updated != 1)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail(request.YearId, "invalid", "Year is not open");
            }

            periodsClosed = await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional("UPDATE `epc_fy_periods` SET `status`='closed' WHERE `year_id`=? AND `status`='open'"),
                cancellationToken,
                request.YearId).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return new ErpFyCloseWriteResult(
            new ErpSimpleWriteResult(true, "ok", Message(netPl), request.YearId, 1 + periodsClosed),
            request.YearId,
            label,
            netPl,
            Outcome(netPl),
            endDate,
            Memo(label),
            entry,
            periodsClosed);
    }

    /// <summary>PHP: <c>'Year closed · ' . result . ' net P&amp;L ' . number_format(net_pl, 2)</c>.</summary>
    public static string Message(decimal netPl)
        => "Year closed · " + Outcome(netPl) + " net P&L " + netPl.ToString("#,##0.00", CultureInfo.InvariantCulture);

    public static string Outcome(decimal netPl) => netPl >= 0 ? "profit" : "loss";

    public static string Memo(string label) => "Year-end close " + label;

    /// <summary>Profit (net ≥ 0): Dr P&amp;L clearing / Cr retained earnings; loss reversed.</summary>
    public static IReadOnlyList<ErpFyCloseEntryLine> ClosingEntry(decimal netPl)
    {
        var amount = Math.Abs(netPl);
        return netPl >= 0
            ?
            [
                new ErpFyCloseEntryLine(PlClearingAccount, amount, 0m),
                new ErpFyCloseEntryLine(RetainedAccount, 0m, amount),
            ]
            :
            [
                new ErpFyCloseEntryLine(RetainedAccount, amount, 0m),
                new ErpFyCloseEntryLine(PlClearingAccount, 0m, amount),
            ];
    }

    /// <summary>
    /// PHP <c>epc_erp_gl_pl_report(start,end)['net_profit']</c> scoped like
    /// <c>epc_erp_gl_resolve_company_id</c>; any failure yields 0.0 (PHP catch).
    /// </summary>
    private static async Task<decimal> NetProfitAsync(
        DbConnection connection,
        long companyHint,
        long startDate,
        long endDate,
        CancellationToken cancellationToken)
    {
        try
        {
            var companyId = await ErpFinAdvancedCompany.ResolveAsync(connection, companyHint, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.BuildSumErpWorkspaceGlNetProfit((int)companyId);
            AddNamed(command, "@dateFrom", startDate);
            AddNamed(command, "@dateTo", endDate);
            if (companyId > 0)
            {
                AddNamed(command, "@companyId", companyId);
            }

            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var net = value is null or DBNull ? 0m : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            return Math.Round(net, 2, MidpointRounding.AwayFromZero);
        }
        catch (DbException)
        {
            return 0m;
        }
    }

    private static void AddNamed(DbCommand command, string name, object value)
    {
        var p = command.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        command.Parameters.Add(p);
    }

    private static ErpFyCloseWriteResult Fail(long yearId, string code, string message)
        => new(ErpSimpleWriteResult.Fail(code, message), yearId, string.Empty, 0m, "profit", 0, string.Empty, [], 0);
}
