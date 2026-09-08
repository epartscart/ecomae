using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_payroll_pay_run</c> twin. Posts an internal cash payment
/// and marks the run/lines paid. Schema ensure and COA GL stay PHP.
/// </summary>
public interface IErpPayrollPayWriteService
{
    Task<ErpSimpleWriteResult> PayRunAsync(
        long runId,
        long cashAccountId = 0,
        string? reference = null,
        long adminId = 0,
        CancellationToken cancellationToken = default);
}

public sealed class ErpPayrollPayWriteService : IErpPayrollPayWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpPayrollPayWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> PayRunAsync(
        long runId,
        long cashAccountId = 0,
        string? reference = null,
        long adminId = 0,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        if (runId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Payroll run not found");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_payroll_runs", "cash_entry_id", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_payroll_lines", "status", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_cash_bank_accounts", "active", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_cash_bank_entries", "counterparty_type", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_cash_bank_entries", "voucher_no", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Payroll table is not provisioned");
        }

        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var select = connection.CreateCommand();
        select.Transaction = tx;
        select.CommandText = ErpDb.Positional(
            "SELECT IFNULL(`status`,''), IFNULL(`total_net`,0), IFNULL(`period_label`,'')"
            + " FROM `epc_erp_payroll_runs` WHERE `id`=? LIMIT 1");
        ErpDb.AddParameters(select, runId);
        var found = false;
        var status = "";
        decimal totalNet = 0;
        var periodLabel = "";
        await using (var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                found = true;
                status = reader.IsDBNull(0) ? "" : reader.GetString(0);
                totalNet = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture);
                periodLabel = reader.IsDBNull(2) ? "" : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? "";
            }
        }

        if (!found)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Payroll run not found");
        }

        if (string.Equals(status, "paid", StringComparison.Ordinal))
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Already paid");
        }

        totalNet = decimal.Round(totalNet, 2, MidpointRounding.AwayFromZero);
        if (totalNet <= 0m)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Nothing to pay");
        }

        if (cashAccountId <= 0)
        {
            cashAccountId = await ErpDb.LongAsync(
                connection,
                tx,
                ErpDb.Positional(
                    "SELECT `id` FROM `epc_erp_cash_bank_accounts` WHERE `active` = 1 ORDER BY `account_type` DESC, `id` ASC LIMIT 1"),
                cancellationToken).ConfigureAwait(false);
        }

        if (cashAccountId <= 0)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "No cash/bank account");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var pref = string.IsNullOrWhiteSpace(reference) ? "PAYROLL-" + periodLabel : reference.Trim();
        var note = "Staff payroll " + periodLabel + " — net salaries";

        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_cash_bank_entries`"
                + " (`account_id`,`time`,`entry_type`,`direction`,`amount`,`counterparty_type`,"
                + " `counterparty_id`,`order_id`,`reference`,`note`,`voucher_no`,`admin_id`)"
                + " VALUES (?,?,?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            cashAccountId,
            now,
            "payment",
            0,
            totalNet,
            "internal",
            0,
            0,
            pref,
            note,
            null,
            adminId).ConfigureAwait(false);
        var entryId = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);

        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional(
                "UPDATE `epc_erp_payroll_runs` SET `status`='paid', `cash_account_id`=?, `cash_entry_id`=?, `paid_at`=? WHERE `id`=?"),
            cancellationToken,
            cashAccountId,
            entryId,
            now,
            runId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional("UPDATE `epc_erp_payroll_lines` SET `status`='paid', `paid_at`=? WHERE `run_id`=?"),
            cancellationToken,
            now,
            runId).ConfigureAwait(false);

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

        var net = totalNet.ToString("N2", CultureInfo.InvariantCulture);
        return ErpSimpleWriteResult.Ok("Salaries paid — " + net + " AED", runId);
    }

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
