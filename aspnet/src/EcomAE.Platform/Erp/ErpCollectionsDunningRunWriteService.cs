using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_coll_dunning_run</c> twin. Plans notices from ageing
/// buckets (PHP <c>epc_credit_dunning_level</c>) and inserts one
/// <c>epc_coll_dunning</c> row per level &gt;= 1. Schema ensure stays PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpCollectionsDunningRunWriteService
{
    Task<ErpSimpleWriteResult> RunAsync(
        ErpCollectionsDunningRunWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpCollectionsDunningRunWriteRequest(
    string? Customers = null,
    long CompanyId = 0);

public sealed record ErpCollectionsDunningPlanEntry(long CustomerId, int Level, decimal Amount, string Message);

public sealed class ErpCollectionsDunningRunWriteService : IErpCollectionsDunningRunWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpCollectionsDunningRunWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> RunAsync(
        ErpCollectionsDunningRunWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var customers = ParseCustomers(request.Customers);
        if (customers.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Enter at least one customer line (customerId|d1_30|d31_60|d61_90|d90_plus)");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var plan = Plan(customers);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_coll_dunning", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_coll_dunning", "run_id", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_coll_dunning", "message", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Collections dunning table is not provisioned");
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var maxSql = companyId > 0
                ? "SELECT COALESCE(MAX(`run_id`),0)+1 FROM `epc_coll_dunning` WHERE `company_id`=?"
                : "SELECT COALESCE(MAX(`run_id`),0)+1 FROM `epc_coll_dunning`";
            var runId = companyId > 0
                ? await ErpDb.LongAsync(connection, transaction, ErpDb.Positional(maxSql), cancellationToken, companyId)
                    .ConfigureAwait(false)
                : await ErpDb.LongAsync(connection, transaction, ErpDb.Positional(maxSql), cancellationToken)
                    .ConfigureAwait(false);

            foreach (var entry in plan)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_coll_dunning` (`company_id`,`run_id`,`customer_id`,`level`,`amount`,`message`,`time_created`) VALUES (?,?,?,?,?,?,?)"),
                    cancellationToken,
                    companyId,
                    runId,
                    entry.CustomerId,
                    entry.Level,
                    entry.Amount,
                    entry.Message,
                    now).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(
                "Dunning run #" + runId.ToString(CultureInfo.InvariantCulture)
                + " — " + plan.Count.ToString(CultureInfo.InvariantCulture) + " notice(s)",
                runId);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public static IReadOnlyList<(long CustomerId, decimal D1, decimal D31, decimal D61, decimal D90)> ParseCustomers(string? raw)
    {
        var text = raw ?? string.Empty;
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var list = new List<(long, decimal, decimal, decimal, decimal)>();
        foreach (var line in lines)
        {
            if (!line.Contains('|', StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length < 5)
            {
                continue;
            }

            if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var customerId))
            {
                continue;
            }

            list.Add((
                customerId,
                ParseDec(parts[1]),
                ParseDec(parts[2]),
                ParseDec(parts[3]),
                ParseDec(parts[4])));
        }

        return list;
    }

    public static IReadOnlyList<ErpCollectionsDunningPlanEntry> Plan(
        IReadOnlyList<(long CustomerId, decimal D1, decimal D31, decimal D61, decimal D90)> customers)
    {
        var outList = new List<ErpCollectionsDunningPlanEntry>();
        foreach (var customer in customers)
        {
            var level = Level(customer.D1, customer.D31, customer.D61, customer.D90);
            if (level.Level < 1)
            {
                continue;
            }

            outList.Add(new ErpCollectionsDunningPlanEntry(customer.CustomerId, level.Level, level.Overdue, level.Message));
        }

        return outList;
    }

    public static (int Level, decimal Overdue, string Message) Level(decimal d1, decimal d31, decimal d61, decimal d90)
    {
        int level;
        string tone;
        if (d90 > 0)
        {
            level = 3;
            tone = "Final notice — account may be referred to collections.";
        }
        else if (d61 > 0)
        {
            level = 2;
            tone = "Second reminder — please settle the overdue balance now.";
        }
        else if (d1 > 0 || d31 > 0)
        {
            level = 1;
            tone = "Friendly reminder — your invoice is past due.";
        }
        else
        {
            level = 0;
            tone = "Account current — no reminder needed.";
        }

        var overdue = decimal.Round(d1 + d31 + d61 + d90, 2, MidpointRounding.AwayFromZero);
        return (level, overdue, tone);
    }

    private static decimal ParseDec(string raw)
        => decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0m;

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }

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
