using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>epc_po_approve</c> / <c>epc_po_reject</c>.</summary>
public interface ICpPoApprovalWriteService
{
    Task<ErpSimpleWriteResult> ApproveAsync(long poId, int tier, int approverId, string comment, CancellationToken cancellationToken = default);
    Task<ErpSimpleWriteResult> RejectAsync(long poId, int tier, int approverId, string reason, CancellationToken cancellationToken = default);
}

public sealed class CpPoApprovalWriteService : ICpPoApprovalWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpPoApprovalWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> ApproveAsync(
        long poId,
        int tier,
        int approverId,
        string comment,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        if (poId <= 0 || tier <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "PO and tier are required.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var select = connection.CreateCommand();
        select.CommandText = "SELECT IFNULL(`status`,''), IFNULL(`current_tier`,0) FROM `epc_po_requests` WHERE `id` = @id LIMIT 1";
        Add(select, "@id", poId);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("not_found", "PO not found.");
        }

        var status = Convert.ToString(reader[0] ?? string.Empty, CultureInfo.InvariantCulture) ?? string.Empty;
        var currentTier = Convert.ToInt32(reader[1] is DBNull ? 0 : reader[1], CultureInfo.InvariantCulture);
        await reader.DisposeAsync();

        if (!string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("not_pending", "PO is not pending approval.");
        }

        if (currentTier != tier)
        {
            return ErpSimpleWriteResult.Fail("wrong_tier", "Not the current approval tier.");
        }

        await using var step = connection.CreateCommand();
        step.CommandText = """
            UPDATE `epc_po_approval_steps`
            SET `decision` = 'approved', `approver_id` = @approver, `comment` = @comment, `decided_at` = NOW()
            WHERE `po_id` = @id AND `tier` = @tier AND `decision` = 'pending'
            """;
        Add(step, "@approver", approverId);
        Add(step, "@comment", comment ?? string.Empty);
        Add(step, "@id", poId);
        Add(step, "@tier", tier);
        await step.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using var next = connection.CreateCommand();
        next.CommandText = "SELECT COUNT(*) FROM `epc_po_approval_steps` WHERE `po_id` = @id AND `tier` = @nextTier";
        Add(next, "@id", poId);
        Add(next, "@nextTier", tier + 1);
        var hasNext = Convert.ToInt32(await next.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0, CultureInfo.InvariantCulture) > 0;

        await using var header = connection.CreateCommand();
        if (hasNext)
        {
            header.CommandText = "UPDATE `epc_po_requests` SET `current_tier` = @tier WHERE `id` = @id";
            Add(header, "@tier", tier + 1);
            Add(header, "@id", poId);
            await header.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Moved to next approval tier.", poId);
        }

        header.CommandText = "UPDATE `epc_po_requests` SET `status` = 'approved', `approved_at` = NOW() WHERE `id` = @id";
        Add(header, "@id", poId);
        await header.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("PO fully approved.", poId);
    }

    public async Task<ErpSimpleWriteResult> RejectAsync(
        long poId,
        int tier,
        int approverId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        if (poId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "PO is required.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT `id` FROM `epc_po_requests` WHERE `id` = @id LIMIT 1";
        Add(exists, "@id", poId);
        if (await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null or DBNull)
        {
            return ErpSimpleWriteResult.Fail("not_found", "PO not found.");
        }

        await using var step = connection.CreateCommand();
        step.CommandText = """
            UPDATE `epc_po_approval_steps`
            SET `decision` = 'rejected', `approver_id` = @approver, `comment` = @reason, `decided_at` = NOW()
            WHERE `po_id` = @id AND `tier` = @tier
            """;
        Add(step, "@approver", approverId);
        Add(step, "@reason", reason ?? string.Empty);
        Add(step, "@id", poId);
        Add(step, "@tier", tier <= 0 ? 1 : tier);
        await step.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using var header = connection.CreateCommand();
        header.CommandText = "UPDATE `epc_po_requests` SET `status` = 'rejected', `rejected_at` = NOW(), `rejection_reason` = @reason WHERE `id` = @id";
        Add(header, "@reason", reason ?? string.Empty);
        Add(header, "@id", poId);
        await header.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("PO rejected.", poId);
    }

    private static void Add(DbCommand command, string name, object? value)
    {
        var p = command.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        command.Parameters.Add(p);
    }
}
