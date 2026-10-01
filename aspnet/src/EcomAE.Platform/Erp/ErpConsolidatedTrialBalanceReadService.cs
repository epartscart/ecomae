using System.Data.Common;

namespace EcomAE.Platform.Erp;

public sealed record ErpConsolidatedTrialBalanceSnapshot(
    long GroupId,
    int Entities,
    IReadOnlyList<object> Accounts,
    string Source,
    string Message);

public interface IErpConsolidatedTrialBalanceReadService
{
    Task<ErpConsolidatedTrialBalanceSnapshot> ReadAsync(
        long groupId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpConsolidatedTrialBalanceReadService : IErpConsolidatedTrialBalanceReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpConsolidatedTrialBalanceReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpConsolidatedTrialBalanceSnapshot> ReadAsync(
        long groupId,
        CancellationToken cancellationToken = default)
    {
        if (groupId <= 0)
        {
            return Empty(groupId, "A group id is required.", "invalid");
        }

        if (!_connections.IsConfigured)
        {
            return Empty(groupId, "TenantRegistry DB is not configured.", "migration");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entities = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_entity_members` WHERE `group_id` = ?"),
                cancellationToken,
                groupId).ConfigureAwait(false);

            return new(groupId, checked((int)entities), [], "database", string.Empty);
        }
        catch (DbException exception)
        {
            return Empty(groupId, exception.Message, "database-error");
        }
    }

    private static ErpConsolidatedTrialBalanceSnapshot Empty(
        long groupId,
        string message,
        string source)
        => new(groupId, 0, [], source, message);
}
