using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>PHP-compatible quality-order creation over the PHP-owned table.</summary>
public interface IErpQmOrderCreateWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        ErpQmOrderCreateWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpQmOrderCreateWriteRequest(
    long CompanyId = 0,
    long PlanId = 0,
    string? RefType = null,
    string? RefId = null,
    long ItemId = 0,
    decimal Qty = 0,
    int ActorUserId = 0);

public sealed class ErpQmOrderCreateWriteService : IErpQmOrderCreateWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpAuditLogWriter _audit;

    public ErpQmOrderCreateWriteService(
        IErpWriteConnectionFactory connections,
        IErpAuditLogWriter audit)
    {
        _connections = connections;
        _audit = audit;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        ErpQmOrderCreateWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CompanyId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Company is required.");
        }

        if (request.PlanId < 0 || request.ItemId < 0 || request.Qty < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Quality order numeric values must be non-negative.");
        }

        var refType = string.IsNullOrWhiteSpace(request.RefType) ? "item" : request.RefType.Trim();
        var refId = request.RefId?.Trim() ?? string.Empty;
        if (refType.Length > 16 || refId.Length > 60)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Quality order reference is too long.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_qm_order", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Quality order table is not provisioned.");
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional(
                "INSERT INTO `epc_qm_order`"
                + " (`company_id`,`plan_id`,`ref_type`,`ref_id`,`item_id`,`qty`,`status`,`verdict`,`time_created`)"
                + " VALUES (?,?,?,?,?,?, 'open', '', ?)"),
            cancellationToken,
            request.CompanyId,
            request.PlanId,
            refType,
            refId,
            request.ItemId,
            request.Qty,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);

        var id = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT LAST_INSERT_ID()"),
            cancellationToken).ConfigureAwait(false);
        await _audit.LogAsync(
            connection,
            transaction,
            request.ActorUserId,
            "qm_order_create",
            "qm_order",
            id,
            "Quality order created",
            new Dictionary<string, string?>
            {
                ["company_id"] = request.CompanyId.ToString(),
                ["plan_id"] = request.PlanId.ToString(),
                ["ref_type"] = refType,
                ["ref_id"] = refId,
                ["item_id"] = request.ItemId.ToString(),
                ["qty"] = request.Qty.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["status"] = "open",
                ["verdict"] = string.Empty,
            },
            cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Quality order created", id);
    }

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return count > 0;
    }
}
