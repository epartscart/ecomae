namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_fy_reopen_year</c> / <c>epc_fy_set_period_status</c> twins.
/// Schema ensure, year create, and year-end close stay PHP.
/// </summary>
public interface IErpFyWriteService
{
    Task<ErpSimpleWriteResult> ReopenYearAsync(long yearId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetPeriodStatusAsync(
        long yearId,
        int periodNo,
        string? status,
        CancellationToken cancellationToken = default);
}

public sealed class ErpFyWriteService : IErpFyWriteService
{
    internal static readonly string[] AllowedPeriod = ["open", "closed", "locked"];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpFyWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> ReopenYearAsync(
        long yearId,
        CancellationToken cancellationToken = default)
    {
        if (yearId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A fiscal year id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_fy_years` SET `status` = 'open', `closed_at` = 0 WHERE `id` = ? AND `status` = 'closed'"),
            cancellationToken,
            yearId);
        return ErpSimpleWriteResult.Ok("Year reopened", yearId);
    }

    public async Task<ErpSimpleWriteResult> SetPeriodStatusAsync(
        long yearId,
        int periodNo,
        string? status,
        CancellationToken cancellationToken = default)
    {
        if (yearId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A fiscal year id is required.");
        }

        if (periodNo <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A period number is required.");
        }

        var next = (status ?? string.Empty).Trim().ToLowerInvariant();
        if (!AllowedPeriod.Contains(next, StringComparer.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid period status");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_fy_periods` SET `status` = ? WHERE `year_id` = ? AND `period_no` = ?"),
            cancellationToken,
            next,
            yearId,
            periodNo);
        return ErpSimpleWriteResult.Ok("Period status updated", yearId);
    }
}
