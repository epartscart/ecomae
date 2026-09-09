using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_crm.php</c> twin of <c>epc_crm_save_expense</c>.
/// Approve-to-cash and send stay Classic. Schema-ensure stays Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpCrmExpenseWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        CpCrmExpenseSaveRequest request,
        CancellationToken cancellationToken = default);
}

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

    public CpCrmExpenseWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
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
}
