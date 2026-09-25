using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_crm.php</c> twin of <c>epc_crm_save_expense</c> and <c>epc_crm_approve_expense</c>.
/// Approve posts a payment through the authoritative cash-bank ledger (<see cref="IErpCashWriteService"/>,
/// first active <c>epc_erp_cash_bank_accounts</c> row) and stores <c>cash_entry_id</c>. Schema-ensure stays Classic;
/// approval does not invent a send (no email/SMTP), matching PHP.
/// </summary>
public interface ICpCrmExpenseWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        CpCrmExpenseSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<CpCrmExpenseApproveResult> ApproveAsync(
        long expenseId,
        bool postToCash,
        int adminId,
        CancellationToken cancellationToken = default);
}

public sealed record CpCrmExpenseApproveResult(bool Succeeded, string Message, long ExpenseId, long CashEntryId);

public sealed record CpCrmExpenseSaveRequest(
    long Id,
    long EmployeeUserId,
    decimal Amount,
    string? Category,
    string? Status,
    string? ReceiptNote);

public sealed class CpCrmExpenseWriteService : ICpCrmExpenseWriteService
{
    public static readonly HashSet<string> Statuses = new(StringComparer.Ordinal)
    {
        "draft", "submitted", "approved", "rejected", "paid",
    };

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpCashWriteService? _cash;

    public CpCrmExpenseWriteService(IErpWriteConnectionFactory connections, IErpCashWriteService? cash = null)
    {
        _connections = connections;
        _cash = cash;
    }

    public static string NormalizeStatus(string? status)
    {
        var raw = (status ?? string.Empty).Trim();
        return Statuses.Contains(raw) ? raw : "draft";
    }

    public static string NormalizeCategory(string? category)
    {
        var raw = (category ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            raw = "travel";
        }

        return raw.Length > 64 ? raw[..64] : raw;
    }

    public static string NormalizeReceiptNote(string? note)
    {
        var raw = (note ?? string.Empty).Trim();
        return raw.Length > 512 ? raw[..512] : raw;
    }

    public static decimal NormalizeAmount(decimal amount)
        => amount < 0 ? 0 : amount;

    public async Task<ErpSimpleWriteResult> SaveAsync(
        CpCrmExpenseSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Id < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Expense id is invalid.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var employeeId = request.EmployeeUserId < 0 ? 0 : request.EmployeeUserId;
        var amount = NormalizeAmount(request.Amount);
        var category = NormalizeCategory(request.Category);
        var status = NormalizeStatus(request.Status);
        var receipt = NormalizeReceiptNote(request.ReceiptNote);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (request.Id > 0)
            {
                var exists = await ErpDb.LongAsync(
                    connection, null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `epc_crm_expenses` WHERE `id`=?"),
                    cancellationToken, request.Id).ConfigureAwait(false);
                if (exists <= 0)
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Expense was not found.");
                }

                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional(
                        """
                        UPDATE `epc_crm_expenses`
                        SET `employee_user_id`=?, `amount`=?, `category`=?, `status`=?, `receipt_note`=?, `time_updated`=?
                        WHERE `id`=?
                        """),
                    cancellationToken,
                    employeeId, amount, category, status, receipt, now, request.Id).ConfigureAwait(false);
                return ErpSimpleWriteResult.Ok("Expense saved", request.Id);
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_crm_expenses`
                    (`employee_user_id`, `amount`, `category`, `status`, `receipt_note`, `time_created`, `time_updated`)
                    VALUES (?, ?, ?, ?, ?, ?, ?)
                    """),
                cancellationToken,
                employeeId, amount, category, status, receipt, now, now).ConfigureAwait(false);
            var created = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Expense saved", created);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "CRM expense table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<CpCrmExpenseApproveResult> ApproveAsync(
        long expenseId,
        bool postToCash,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        if (expenseId <= 0)
        {
            return new CpCrmExpenseApproveResult(false, "Expense not found", expenseId, 0);
        }

        if (!_connections.IsConfigured)
        {
            return new CpCrmExpenseApproveResult(false, "TenantRegistry DB is not configured.", expenseId, 0);
        }

        try
        {
            decimal amount;
            string category;
            string receipt;
            long existingCash;
            await using (var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false))
            {
                await using var select = connection.CreateCommand();
                select.CommandText = ErpDb.Positional(
                    "SELECT IFNULL(`amount`,0), IFNULL(`category`,''), IFNULL(`receipt_note`,''), IFNULL(`cash_entry_id`,0) FROM `epc_crm_expenses` WHERE `id`=? AND `active`=1 LIMIT 1");
                ErpDb.AddParameters(select, expenseId);
                await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return new CpCrmExpenseApproveResult(false, "Expense not found", expenseId, 0);
                }

                amount = Convert.ToDecimal(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture);
                category = Convert.ToString(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture) ?? "";
                receipt = Convert.ToString(reader.GetValue(2), System.Globalization.CultureInfo.InvariantCulture) ?? "";
                existingCash = Convert.ToInt64(reader.GetValue(3), System.Globalization.CultureInfo.InvariantCulture);
            }

            var cashId = existingCash;
            if (postToCash && cashId <= 0 && amount > 0 && _cash is not null)
            {
                long accountId;
                await using (var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false))
                {
                    accountId = await ErpDb.LongAsync(
                        connection, null,
                        "SELECT IFNULL(`id`,0) FROM `epc_erp_cash_bank_accounts` WHERE `active` = 1 ORDER BY `id` ASC LIMIT 1",
                        cancellationToken).ConfigureAwait(false);
                }

                if (accountId > 0)
                {
                    try
                    {
                        var entry = await _cash.CashEntryAsync(
                            new ErpCashEntryInput
                            {
                                AccountId = (int)accountId,
                                Amount = amount,
                                Direction = false,
                                EntryType = "payment",
                                CounterpartyType = "internal",
                                Reference = "CRM-EXP-" + expenseId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                Note = "Expense: " + category + " — " + receipt,
                            },
                            adminId,
                            cancellationToken).ConfigureAwait(false);
                        cashId = entry.CashEntryId;
                    }
                    catch (ErpWriteException ex)
                    {
                        return new CpCrmExpenseApproveResult(false, "Cash posting failed: " + ex.Message, expenseId, 0);
                    }
                }
            }

            await using (var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false))
            {
                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("UPDATE `epc_crm_expenses` SET `status`='approved', `cash_entry_id`=?, `time_updated`=? WHERE `id`=?"),
                    cancellationToken,
                    cashId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), expenseId).ConfigureAwait(false);
            }

            return new CpCrmExpenseApproveResult(true, cashId > 0 ? "Expense approved and posted to cash" : "Expense approved", expenseId, cashId);
        }
        catch (DbException)
        {
            return new CpCrmExpenseApproveResult(false, "CRM expense table is missing — schema-ensure stays Classic.", expenseId, 0);
        }
    }
}
