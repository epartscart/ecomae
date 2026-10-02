using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpPaymentBatchStatusWriteService
{
    Task<ErpSimpleWriteResult> SetStatusAsync(
        long batchId,
        string targetStatus,
        int adminId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpPaymentBatchStatusWriteService : IErpPaymentBatchStatusWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpPaymentBatchStatusWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetStatusAsync(
        long batchId,
        string targetStatus,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var target = targetStatus.Trim().ToLowerInvariant();
        if (batchId <= 0 || target is not ("submitted" or "processed" or "cancelled"))
        {
            return ErpSimpleWriteResult.Fail("validation", "Payment batch status transition is invalid.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var current = await ReadStatusAsync(connection, batchId, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Payment batch not found.");
        }

        if (!IsAllowed(current, target))
        {
            return ErpSimpleWriteResult.Fail("transition", $"Payment batch cannot move from {current} to {target}.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                UPDATE `epc_erp_payment_batches`
                SET `status`=?, `time_updated`=?
                WHERE `id`=? AND `status`=?
                """),
            cancellationToken,
            target,
            now,
            batchId,
            current).ConfigureAwait(false);

        await TrySyncProcessCaseAsync(connection, batchId, target, now, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok($"Payment batch {target}.", batchId);
    }

    private static bool IsAllowed(string current, string target)
        => (current, target) switch
        {
            ("draft", "submitted") => true,
            ("submitted", "processed") => true,
            ("draft", "cancelled") => true,
            ("submitted", "cancelled") => true,
            _ => false
        };

    private static async Task<string?> ReadStatusAsync(
        DbConnection connection,
        long batchId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `status` FROM `epc_erp_payment_batches` WHERE `id`=? LIMIT 1");
        Add(command, batchId);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    private static async Task TrySyncProcessCaseAsync(
        DbConnection connection,
        long batchId,
        string status,
        long now,
        CancellationToken cancellationToken)
    {
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("""
                    UPDATE `epc_pf_cases`
                    SET `current_step_no`=CASE WHEN ?='cancelled' THEN `current_step_no` ELSE ? END,
                        `status`=?,
                        `completed_at`=?,
                        `time_updated`=?
                    WHERE `subject_type`='erp_payment'
                      AND `subject_id`=?
                      AND `status`='open'
                    """),
                cancellationToken,
                status,
                status == "submitted" ? 2 : 3,
                status == "cancelled" ? "cancelled" : status == "processed" ? "done" : "open",
                status is "processed" or "cancelled" ? now : 0,
                now,
                batchId).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    private static void Add(DbCommand command, object value)
    {
        var parameter = command.CreateParameter();
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
