using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>webhooks</c> <c>delete</c> / <c>epc_webhooks_delete</c>.
/// Register, update, deliver, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER.
/// </summary>
public interface IBosWebhookWriteService
{
    Task<ErpSimpleWriteResult> DeleteAsync(
        long webhookId,
        CancellationToken cancellationToken = default);
}

public sealed class BosWebhookWriteService : IBosWebhookWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public BosWebhookWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(
        long webhookId,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_webhooks` SET `active` = 0 WHERE `id` = ?
                    """),
                cancellationToken, webhookId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Webhook deleted", webhookId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Webhooks table is missing — schema-ensure stays Classic.");
        }
    }
}
