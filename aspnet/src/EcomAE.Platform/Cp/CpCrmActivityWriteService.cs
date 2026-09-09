using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_crm.php</c> <c>toggle_activity</c> twin of <c>epc_crm_toggle_activity_done</c>.
/// Create activity, quote email, and send stay Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpCrmActivityWriteService
{
    Task<ErpSimpleWriteResult> ToggleDoneAsync(
        long id,
        bool done,
        CancellationToken cancellationToken = default);
}

public sealed class CpCrmActivityWriteService : ICpCrmActivityWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpCrmActivityWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> ToggleDoneAsync(
        long id,
        bool done,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Activity id is invalid.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_crm_activities`
                    SET `done`=?, `time_updated`=?
                    WHERE `id`=?
                    """),
                cancellationToken,
                done ? 1 : 0, now, id);
            return ErpSimpleWriteResult.Ok(done ? "Activity updated" : "Activity updated", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "CRM activity table is missing — schema-ensure stays Classic.");
        }
    }
}
