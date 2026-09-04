namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_hr_leave_set_status</c> / <c>epc_hr_expense_set_status</c> twins.
/// Schema ensure, leave request, and expense save stay PHP.
/// </summary>
public interface IErpHrStatusWriteService
{
    Task<ErpSimpleWriteResult> SetLeaveStatusAsync(long id, string? status, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetExpenseStatusAsync(long id, string? status, CancellationToken cancellationToken = default);
}

public sealed class ErpHrStatusWriteService : IErpHrStatusWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpHrStatusWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public Task<ErpSimpleWriteResult> SetLeaveStatusAsync(
        long id,
        string? status,
        CancellationToken cancellationToken = default)
        => SetAsync(
            id,
            status,
            "A leave id is required.",
            "A leave status is required.",
            "UPDATE `epc_hr_leave` SET `status` = ? WHERE `id` = ?",
            "Leave ",
            cancellationToken);

    public Task<ErpSimpleWriteResult> SetExpenseStatusAsync(
        long id,
        string? status,
        CancellationToken cancellationToken = default)
        => SetAsync(
            id,
            status,
            "An expense id is required.",
            "An expense status is required.",
            "UPDATE `epc_hr_expenses` SET `status` = ? WHERE `id` = ?",
            "Expense ",
            cancellationToken);

    private async Task<ErpSimpleWriteResult> SetAsync(
        long id,
        string? status,
        string missingId,
        string missingStatus,
        string sql,
        string messagePrefix,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", missingId);
        }

        var next = (status ?? string.Empty).Trim();
        if (next.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", missingStatus);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional(sql), cancellationToken, next, id);
        return ErpSimpleWriteResult.Ok(messagePrefix + next, id);
    }
}
