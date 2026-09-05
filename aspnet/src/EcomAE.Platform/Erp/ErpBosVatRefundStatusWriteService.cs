namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_bos_vat_refund_set_status</c> twin. Schema ensure stays PHP. Save is <see cref="IErpBosVatRefundSaveWriteService"/>.
/// </summary>
public interface IErpBosVatRefundStatusWriteService
{
    Task<ErpSimpleWriteResult> SetStatusAsync(long id, string? status, CancellationToken cancellationToken = default);
}

public sealed class ErpBosVatRefundStatusWriteService : IErpBosVatRefundStatusWriteService
{
    internal static readonly string[] Allowed = ["recorded", "validated", "exported", "refunded", "void"];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpBosVatRefundStatusWriteService(IErpWriteConnectionFactory connections)
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
            return ErpSimpleWriteResult.Fail("invalid", "A VAT refund id is required.");
        }

        var next = (status ?? string.Empty).Trim().ToLowerInvariant();
        if (!Allowed.Contains(next, StringComparer.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid status");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_bos_vat_refunds` SET `status` = ? WHERE `id` = ?"),
            cancellationToken,
            next, id);
        return ErpSimpleWriteResult.Ok("Status updated", id);
    }
}
