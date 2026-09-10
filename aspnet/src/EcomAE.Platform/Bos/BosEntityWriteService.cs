using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>multi_entity</c> <c>eliminate</c> / <c>epc_entity_eliminate</c>.
/// Create, add-member, intercompany, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER. PHP always returns ok — this write does not invent id/not-found checks.
/// </summary>
public interface IBosEntityWriteService
{
    Task<ErpSimpleWriteResult> EliminateAsync(
        long groupId,
        CancellationToken cancellationToken = default);
}

public sealed class BosEntityWriteService : IBosEntityWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public BosEntityWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> EliminateAsync(
        long groupId,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var eliminated = await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_intercompany_txns` SET `status`='eliminated' WHERE `group_id`=? AND `status`='matched'
                    """),
                cancellationToken, groupId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(
                "Eliminated " + eliminated.ToString(CultureInfo.InvariantCulture) + " matched inter-company row(s).",
                groupId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Intercompany table is missing — schema-ensure stays Classic.");
        }
    }
}
