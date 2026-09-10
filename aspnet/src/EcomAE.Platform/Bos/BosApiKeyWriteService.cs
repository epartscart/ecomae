using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>rest_api</c> <c>key_revoke</c> / <c>epc_api_key_revoke</c>.
/// Generate stays Classic (random key + schema-ensure). This service does not invent a send.
/// It does not emit CREATE/ALTER.
/// </summary>
public interface IBosApiKeyWriteService
{
    Task<ErpSimpleWriteResult> RevokeAsync(
        long keyId,
        CancellationToken cancellationToken = default);
}

public sealed class BosApiKeyWriteService : IBosApiKeyWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public BosApiKeyWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> RevokeAsync(
        long keyId,
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
                    UPDATE `epc_api_keys` SET `active` = 0 WHERE `id` = ?
                    """),
                cancellationToken, keyId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("API key revoked", keyId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "API keys table is missing — schema-ensure stays Classic.");
        }
    }
}
