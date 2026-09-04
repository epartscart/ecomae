namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_ctr_set_status</c> twin. Schema ensure, save, sign, and OCR stay PHP.
/// </summary>
public interface IErpContractStatusWriteService
{
    Task<ErpSimpleWriteResult> SetStatusAsync(long id, string? status, CancellationToken cancellationToken = default);
}

public sealed class ErpContractStatusWriteService : IErpContractStatusWriteService
{
    internal static readonly string[] Allowed = ["draft", "sent", "signed", "active", "expired", "terminated"];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpContractStatusWriteService(IErpWriteConnectionFactory connections)
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
            return ErpSimpleWriteResult.Fail("invalid", "A contract id is required.");
        }

        var next = (status ?? string.Empty).Trim().ToLowerInvariant();
        if (!Allowed.Contains(next, StringComparer.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid contract status");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_contracts` SET `status` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            next, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), id);
        return ErpSimpleWriteResult.Ok("Contract " + next, id);
    }
}
