namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_coll_case_set_status</c> twin. Schema ensure, promise,
/// activity, dunning run, and hold stay PHP. Case save is <c>IErpCollectionsCaseSaveWriteService</c>.
/// </summary>
public interface IErpCollectionsCaseStatusWriteService
{
    Task<ErpSimpleWriteResult> SetStatusAsync(long id, string? status, CancellationToken cancellationToken = default);
}

public sealed class ErpCollectionsCaseStatusWriteService : IErpCollectionsCaseStatusWriteService
{
    internal static readonly string[] Allowed = ["new", "in_progress", "promise_to_pay", "escalated", "resolved"];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpCollectionsCaseStatusWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetStatusAsync(
        long id,
        string? status,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A collections case id is required.");
        }

        var next = (status ?? string.Empty).Trim().ToLowerInvariant();
        if (!Allowed.Contains(next, StringComparer.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid case status");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_coll_cases` SET `status` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            next, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), id);
        return ErpSimpleWriteResult.Ok("Case status updated", id);
    }
}
