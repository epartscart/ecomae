using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public interface IErpThreeWayMatchWriteService
{
    Task<ErpSimpleWriteResult> DecideAsync(
        ErpThreeWayMatchDecisionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpThreeWayMatchDecisionRequest(
    long PurchaseOrderId = 0,
    string Decision = "match",
    string? ExceptionReason = null,
    long AdminId = 0);

public sealed class ErpThreeWayMatchWriteService : IErpThreeWayMatchWriteService
{
    private const decimal PhpMatchToleranceAmount = 0.02m;
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpAuditLogWriter _audit;

    public ErpThreeWayMatchWriteService(
        IErpWriteConnectionFactory connections,
        IErpAuditLogWriter audit)
    {
        _connections = connections;
        _audit = audit;
    }

    public async Task<ErpSimpleWriteResult> DecideAsync(
        ErpThreeWayMatchDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PurchaseOrderId <= 0
            || !string.Equals(request.Decision, "match", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(request.Decision, "exception", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("invalid", "A purchase order and match decision are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var read = connection.CreateCommand();
        read.CommandText = ErpDb.Positional("""
            SELECT
                IFNULL(po.`total_amount`,0) AS po_total,
                (SELECT COUNT(*) FROM `epc_erp_po_receipts` r WHERE r.`po_id`=po.`id`) AS receipt_count,
                IFNULL(p.`id`,0) AS purchase_id,
                IFNULL(p.`total_amount`,0) AS invoice_total
            FROM `epc_erp_purchase_orders` po
            LEFT JOIN `epc_erp_purchases` p
                ON p.`id`=po.`purchase_id` OR (po.`order_id`>0 AND p.`order_id`=po.`order_id`)
            WHERE po.`id`=? AND po.`status` IN ('approved','partial','received')
            LIMIT 1
            """);
        ErpDb.AddParameters(read, request.PurchaseOrderId);
        await using var reader = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("not_found", "Approved purchase order was not found.");
        }

        var poTotal = Convert.ToDecimal(reader["po_total"], CultureInfo.InvariantCulture);
        var receiptCount = Convert.ToInt32(reader["receipt_count"], CultureInfo.InvariantCulture);
        var purchaseId = Convert.ToInt64(reader["purchase_id"], CultureInfo.InvariantCulture);
        var invoiceTotal = Convert.ToDecimal(reader["invoice_total"], CultureInfo.InvariantCulture);
        var variance = invoiceTotal - poTotal;
        if (receiptCount <= 0 || purchaseId <= 0)
        {
            return ErpSimpleWriteResult.Fail(
                "incomplete",
                receiptCount <= 0 ? "Goods receipt is required before matching." : "Supplier invoice is required before matching.");
        }

        var withinTolerance = Math.Abs(variance) < PhpMatchToleranceAmount;
        var decision = request.Decision.Trim().ToLowerInvariant();
        if (decision == "match" && !withinTolerance)
        {
            return ErpSimpleWriteResult.Fail(
                "variance",
                $"Invoice variance {variance.ToString("N2", CultureInfo.InvariantCulture)} exceeds tolerance.");
        }

        var status = decision == "match" ? "matched" : "exception";
        if (status == "exception" && string.IsNullOrWhiteSpace(request.ExceptionReason))
        {
            return ErpSimpleWriteResult.Fail("reason_required", "An exception reason is required.");
        }

        await reader.DisposeAsync().ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `ecomae_erp_three_way_matches` (
                `id` bigint NOT NULL AUTO_INCREMENT,
                `purchase_order_id` bigint NOT NULL,
                `purchase_id` bigint NOT NULL,
                `receipt_count` int NOT NULL DEFAULT 0,
                `po_total` decimal(14,2) NOT NULL DEFAULT 0.00,
                `invoice_total` decimal(14,2) NOT NULL DEFAULT 0.00,
                `variance_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
                `tolerance_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
                `match_status` varchar(16) NOT NULL,
                `exception_reason` varchar(500) NOT NULL DEFAULT '',
                `admin_id` bigint NOT NULL DEFAULT 0,
                `created_at_utc` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at_utc` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (`id`),
                UNIQUE KEY `ux_ecomae_erp_3wm_po` (`purchase_order_id`)
            ) ENGINE=InnoDB
            """, cancellationToken).ConfigureAwait(false);

        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional("""
                INSERT INTO `ecomae_erp_three_way_matches`
                    (`purchase_order_id`,`purchase_id`,`receipt_count`,`po_total`,`invoice_total`,
                     `variance_amount`,`tolerance_amount`,`match_status`,`exception_reason`,`admin_id`)
                VALUES (?,?,?,?,?,?,?,?,?,?)
                ON DUPLICATE KEY UPDATE
                    `purchase_id`=VALUES(`purchase_id`),`receipt_count`=VALUES(`receipt_count`),
                    `po_total`=VALUES(`po_total`),`invoice_total`=VALUES(`invoice_total`),
                    `variance_amount`=VALUES(`variance_amount`),`tolerance_amount`=VALUES(`tolerance_amount`),
                    `match_status`=VALUES(`match_status`),`exception_reason`=VALUES(`exception_reason`),
                    `admin_id`=VALUES(`admin_id`),`updated_at_utc`=CURRENT_TIMESTAMP
                """),
            cancellationToken,
            request.PurchaseOrderId,
            purchaseId,
            receiptCount,
            poTotal,
            invoiceTotal,
            variance,
            PhpMatchToleranceAmount,
            status,
            Clip(request.ExceptionReason, 500),
            request.AdminId).ConfigureAwait(false);
        await _audit.LogAsync(
            connection,
            tx,
            (int)Math.Clamp(request.AdminId, 0, int.MaxValue),
            status == "matched" ? "three_way_match" : "three_way_match_exception",
            "purchase_order",
            request.PurchaseOrderId,
            status == "matched" ? "Purchase order matched" : "Purchase order routed to match exception",
            new Dictionary<string, string?>
            {
                ["po_total"] = poTotal.ToString("N2", CultureInfo.InvariantCulture),
                ["invoice_total"] = invoiceTotal.ToString("N2", CultureInfo.InvariantCulture),
                ["variance_amount"] = variance.ToString("N2", CultureInfo.InvariantCulture),
                ["tolerance_amount"] = PhpMatchToleranceAmount.ToString("N2", CultureInfo.InvariantCulture),
                ["exception_reason"] = Clip(request.ExceptionReason, 500)
            },
            cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(
            status == "matched" ? "Three-way match accepted." : "Three-way match exception recorded.",
            request.PurchaseOrderId);
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
