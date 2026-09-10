using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>wps_payroll</c> <c>approve_run</c> / <c>epc_payroll_approve_run</c>.
/// Create-run, employee-add, generate-sif, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER.
/// </summary>
public interface IBosPayrollWriteService
{
    Task<ErpSimpleWriteResult> ApproveRunAsync(
        long runId,
        long approverId,
        CancellationToken cancellationToken = default);
}

public sealed class BosPayrollWriteService : IBosPayrollWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public BosPayrollWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> ApproveRunAsync(
        long runId,
        long approverId,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var written = await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_payroll_runs` SET `status` = 'approved', `approved_by` = ?, `approved_at` = NOW() WHERE `id` = ? AND `status` = 'draft'
                    """),
                cancellationToken, approverId, runId).ConfigureAwait(false);
            if (written <= 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Payroll run was not approved");
            }

            return ErpSimpleWriteResult.Ok("Payroll run approved", runId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Payroll runs table is missing — schema-ensure stays Classic.");
        }
    }
}
