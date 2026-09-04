namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_bos_wf_disable_rule</c> twin. Rule save, decide, and raise stay PHP.
/// </summary>
public interface IErpBosWfDisableRuleWriteService
{
    Task<ErpSimpleWriteResult> DisableAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class ErpBosWfDisableRuleWriteService : IErpBosWfDisableRuleWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpBosWfDisableRuleWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> DisableAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "An approval rule id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_bos_approval_rules` SET `active` = 0 WHERE `id` = ?"),
            cancellationToken,
            id);
        return ErpSimpleWriteResult.Ok("Rule disabled", id);
    }
}
