namespace EcomAE.Platform.Erp;

/// <summary>Live PHP <c>epc_wms_work_assign</c> twin. WMS schema ownership remains Classic-owned.</summary>
public interface IErpWmsWorkAssignWriteService
{
    Task<ErpSimpleWriteResult> AssignAsync(long workId, string assignee, CancellationToken cancellationToken = default);
}

public sealed class ErpWmsWorkAssignWriteService : IErpWmsWorkAssignWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpWmsWorkAssignWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AssignAsync(long workId, string assignee, CancellationToken cancellationToken = default)
    {
        if (workId <= 0)
            return ErpSimpleWriteResult.Fail("invalid", "A WMS work id is required.");
        if (string.IsNullOrWhiteSpace(assignee))
            return ErpSimpleWriteResult.Fail("invalid", "An assignee is required.");
        if (!_connections.IsConfigured)
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var writes = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_wms_work` SET `assigned_to`=?, `status`=IF(`status`='open','in_progress',`status`), `time_updated`=? WHERE `id`=? AND `status`<>'closed'"),
            cancellationToken,
            assignee.Trim(),
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            workId).ConfigureAwait(false);
        return writes == 0
            ? ErpSimpleWriteResult.Fail("invalid", "WMS work was not found or is already closed.")
            : ErpSimpleWriteResult.Ok("WMS work assigned", workId);
    }
}
