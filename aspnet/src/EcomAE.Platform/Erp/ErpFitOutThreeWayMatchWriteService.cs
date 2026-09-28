namespace EcomAE.Platform.Erp;

public interface IErpFitOutThreeWayMatchWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpFitOutThreeWayMatchSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutThreeWayMatchSaveRequest(
    long Id = 0,
    long ProcurementLinkId = 0,
    long ProjectId = 0,
    long PurchaseOrderId = 0,
    long GoodsReceiptId = 0,
    string? InvoiceReference = null,
    decimal OrderedAmount = 0,
    decimal ReceivedAmount = 0,
    decimal InvoicedAmount = 0,
    decimal TolerancePercent = 0);

public sealed class ErpFitOutThreeWayMatchWriteService : IErpFitOutThreeWayMatchWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutThreeWayMatchWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpFitOutThreeWayMatchSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var invoiceReference = Clip(request.InvoiceReference, 120);
        if (request.ProjectId <= 0 || invoiceReference.Length == 0)
        {
            return ErpSimpleWriteResult.Fail(
                "invalid",
                "Project and invoice reference are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail(
                "db",
                "TenantRegistry DB is not configured.");
        }

        var ordered = Math.Max(0m, request.OrderedAmount);
        var received = Math.Max(0m, request.ReceivedAmount);
        var invoiced = Math.Max(0m, request.InvoicedAmount);
        var tolerance = Math.Clamp(request.TolerancePercent, 0m, 100m);
        var toleranceAmount = Math.Max(ordered, received) * tolerance / 100m;
        var orderedVariance = Math.Abs(invoiced - ordered);
        var receivedVariance = Math.Abs(invoiced - received);
        var variance = Math.Max(orderedVariance, receivedVariance);
        var status = ordered <= 0m || received <= 0m || invoiced <= 0m
            ? "pending"
            : orderedVariance <= toleranceAmount && receivedVariance <= toleranceAmount
                ? "matched"
                : "exception";

        await using var connection = await _connections
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `ecomae_fitout_three_way_matches` (
                `id` bigint NOT NULL AUTO_INCREMENT,
                `procurement_link_id` bigint NOT NULL DEFAULT 0,
                `project_id` bigint NOT NULL,
                `purchase_order_id` bigint NOT NULL DEFAULT 0,
                `goods_receipt_id` bigint NOT NULL DEFAULT 0,
                `invoice_reference` varchar(120) NOT NULL,
                `ordered_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
                `received_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
                `invoiced_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
                `tolerance_percent` decimal(7,3) NOT NULL DEFAULT 0.000,
                `variance_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
                `match_status` varchar(16) NOT NULL DEFAULT 'pending',
                `created_at_utc` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at_utc` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (`id`),
                KEY `ix_ecomae_fitout_3wm_project` (`project_id`),
                KEY `ix_ecomae_fitout_3wm_po` (`purchase_order_id`),
                KEY `ix_ecomae_fitout_3wm_grn` (`goods_receipt_id`)
            ) ENGINE=InnoDB
            """, cancellationToken).ConfigureAwait(false);

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("""
                    UPDATE `ecomae_fitout_three_way_matches`
                    SET `procurement_link_id`=?,`project_id`=?,`purchase_order_id`=?,
                        `goods_receipt_id`=?,`invoice_reference`=?,`ordered_amount`=?,
                        `received_amount`=?,`invoiced_amount`=?,`tolerance_percent`=?,
                        `variance_amount`=?,`match_status`=?,
                        `updated_at_utc`=CURRENT_TIMESTAMP
                    WHERE `id`=?
                    """),
                cancellationToken,
                Math.Max(0, request.ProcurementLinkId),
                request.ProjectId,
                Math.Max(0, request.PurchaseOrderId),
                Math.Max(0, request.GoodsReceiptId),
                invoiceReference,
                ordered,
                received,
                invoiced,
                tolerance,
                variance,
                status,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Three-way match saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `ecomae_fitout_three_way_matches`
                    (`procurement_link_id`,`project_id`,`purchase_order_id`,`goods_receipt_id`,
                     `invoice_reference`,`ordered_amount`,`received_amount`,`invoiced_amount`,
                     `tolerance_percent`,`variance_amount`,`match_status`)
                VALUES (?,?,?,?,?,?,?,?,?,?,?)
                """),
            cancellationToken,
            Math.Max(0, request.ProcurementLinkId),
            request.ProjectId,
            Math.Max(0, request.PurchaseOrderId),
            Math.Max(0, request.GoodsReceiptId),
            invoiceReference,
            ordered,
            received,
            invoiced,
            tolerance,
            variance,
            status).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(
            connection,
            null,
            cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Three-way match saved", id);
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
