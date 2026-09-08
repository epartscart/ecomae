using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_sub_generate_invoice</c> twin. INSERT cycle invoice and
/// advance <c>next_bill_date</c>. Schema ensure stays PHP. Does not CREATE tables.
/// </summary>
public interface IErpSubGenerateWriteService
{
    Task<ErpSimpleWriteResult> GenerateAsync(long subscriptionId, CancellationToken cancellationToken = default);
}

public sealed class ErpSubGenerateWriteService : IErpSubGenerateWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpSubGenerateWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> GenerateAsync(
        long subscriptionId,
        CancellationToken cancellationToken = default)
    {
        if (subscriptionId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Subscription not found");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_subscriptions", "next_bill_date", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_sub_invoices", "subscription_id", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Subscription invoice table is not provisioned");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `status`,`cycle`,`amount`,`next_bill_date`,`start_date` FROM `epc_erp_subscriptions` WHERE `id`=?");
        ErpDb.AddParameters(command, subscriptionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Subscription not found");
        }

        var status = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
        var cycle = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        var amount = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture);
        var nextBill = reader.IsDBNull(3) ? 0L : Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture);
        var startDate = reader.IsDBNull(4) ? 0L : Convert.ToInt64(reader.GetValue(4), CultureInfo.InvariantCulture);
        await reader.DisposeAsync().ConfigureAwait(false);

        if (!string.Equals(status, "active", StringComparison.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Subscription is not active");
        }

        amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        var months = CycleMonths(cycle);
        var periodStart = nextBill > 0 ? nextBill : startDate;
        var periodEnd = AddMonthsUnix(periodStart, months);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_sub_invoices` (`subscription_id`,`period_start`,`period_end`,`amount`,`status`,`time_created`) VALUES (?,?,?,?,'issued',?)"),
            cancellationToken,
            subscriptionId,
            periodStart,
            periodEnd,
            amount,
            now).ConfigureAwait(false);
        var invoiceId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_subscriptions` SET `next_bill_date`=? WHERE `id`=?"),
            cancellationToken,
            periodEnd,
            subscriptionId).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(FormatGeneratedMessage(invoiceId, amount), invoiceId);
    }

    public static int CycleMonths(string? cycle) => (cycle ?? string.Empty) switch
    {
        "annual" => 12,
        "quarterly" => 3,
        _ => 1,
    };

    public static long AddMonthsUnix(long startUnix, int months)
    {
        var start = DateTimeOffset.FromUnixTimeSeconds(startUnix < 0 ? 0 : startUnix);
        return start.AddMonths(months).ToUnixTimeSeconds();
    }

    public static string FormatGeneratedMessage(long invoiceId, decimal amount)
        => "Cycle invoice #" + invoiceId.ToString(CultureInfo.InvariantCulture)
           + " generated — " + amount.ToString("#,0.00", CultureInfo.InvariantCulture) + " AED";

    private static async Task<bool> ColumnExistsAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
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
