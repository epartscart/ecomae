namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_sub_invoice_set_status(..., 'paid')</c> twin for <c>sub_invoice_paid</c>.
/// Schema ensure, generate, and subscription save stay PHP.
/// </summary>
public interface IErpSubInvoicePaidWriteService
{
    Task<ErpSimpleWriteResult> MarkPaidAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class ErpSubInvoicePaidWriteService : IErpSubInvoicePaidWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpSubInvoicePaidWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> MarkPaidAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A subscription invoice id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_sub_invoices` SET `status` = ? WHERE `id` = ?"),
            cancellationToken,
            "paid", id);
        return ErpSimpleWriteResult.Ok("Invoice marked paid", id);
    }
}
