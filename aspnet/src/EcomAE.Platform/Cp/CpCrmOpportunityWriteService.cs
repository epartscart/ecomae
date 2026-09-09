using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_crm.php</c> <c>update_stage</c> twin of <c>epc_crm_update_opportunity_stage</c>.
/// Convert, save opportunity, quote email, and send stay Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpCrmOpportunityWriteService
{
    Task<ErpSimpleWriteResult> UpdateStageAsync(
        long id,
        string? stage,
        CancellationToken cancellationToken = default);
}

public sealed class CpCrmOpportunityWriteService : ICpCrmOpportunityWriteService
{
    public static readonly HashSet<string> Stages = new(StringComparer.Ordinal)
    {
        "prospect", "qualified", "proposal", "negotiation", "won", "lost",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public CpCrmOpportunityWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> UpdateStageAsync(
        long id,
        string? stage,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Opportunity id is invalid.");
        }

        var rowStage = (stage ?? string.Empty).Trim();
        if (!Stages.Contains(rowStage))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid stage");
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
                    UPDATE `epc_crm_opportunities`
                    SET `stage`=?, `time_updated`=?
                    WHERE `id`=?
                    """),
                cancellationToken,
                rowStage, now, id);
            return ErpSimpleWriteResult.Ok("Stage updated", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "CRM opportunity table is missing — schema-ensure stays Classic.");
        }
    }
}
