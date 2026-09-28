using System.Globalization;

namespace EcomAE.Platform.Erp;

public interface IErpFitOutProcurementLinkWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpFitOutProcurementLinkSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutProcurementLinkSaveRequest(
    long Id = 0,
    long ProjectId = 0,
    long CostCodeId = 0,
    long RequisitionId = 0,
    long PurchaseOrderId = 0,
    long GoodsReceiptId = 0,
    string? Description = null,
    decimal Quantity = 0,
    decimal CommittedAmount = 0,
    string? Status = null);

public sealed class ErpFitOutProcurementLinkWriteService : IErpFitOutProcurementLinkWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutProcurementLinkWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpFitOutProcurementLinkSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var description = Clip(request.Description, 240);
        if (request.ProjectId <= 0 || description.Length == 0)
        {
            return ErpSimpleWriteResult.Fail(
                "invalid",
                "Project and procurement description are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail(
                "db",
                "TenantRegistry DB is not configured.");
        }

        var status = request.Status?.Trim().ToLowerInvariant() switch
        {
            "requested" => "requested",
            "ordered" => "ordered",
            "partially_received" => "partially_received",
            "received" => "received",
            "cancelled" => "cancelled",
            _ => "requested"
        };
        var quantity = Math.Max(0m, request.Quantity);
        var committedAmount = Math.Max(0m, request.CommittedAmount);

        await using var connection = await _connections
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `ecomae_fitout_procurement_links` (
                `id` bigint NOT NULL AUTO_INCREMENT,
                `project_id` bigint NOT NULL,
                `cost_code_id` bigint NOT NULL DEFAULT 0,
                `requisition_id` bigint NOT NULL DEFAULT 0,
                `purchase_order_id` bigint NOT NULL DEFAULT 0,
                `goods_receipt_id` bigint NOT NULL DEFAULT 0,
                `description` varchar(240) NOT NULL,
                `quantity` decimal(14,3) NOT NULL DEFAULT 0.000,
                `committed_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
                `status` varchar(24) NOT NULL DEFAULT 'requested',
                `created_at_utc` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at_utc` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (`id`),
                KEY `ix_ecomae_fitout_proc_project` (`project_id`),
                KEY `ix_ecomae_fitout_proc_req` (`requisition_id`),
                KEY `ix_ecomae_fitout_proc_po` (`purchase_order_id`),
                KEY `ix_ecomae_fitout_proc_grn` (`goods_receipt_id`)
            ) ENGINE=InnoDB
            """, cancellationToken).ConfigureAwait(false);

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("""
                    UPDATE `ecomae_fitout_procurement_links`
                    SET `project_id`=?,`cost_code_id`=?,`requisition_id`=?,
                        `purchase_order_id`=?,`goods_receipt_id`=?,`description`=?,
                        `quantity`=?,`committed_amount`=?,`status`=?,
                        `updated_at_utc`=CURRENT_TIMESTAMP
                    WHERE `id`=?
                    """),
                cancellationToken,
                request.ProjectId,
                Math.Max(0, request.CostCodeId),
                Math.Max(0, request.RequisitionId),
                Math.Max(0, request.PurchaseOrderId),
                Math.Max(0, request.GoodsReceiptId),
                description,
                quantity,
                committedAmount,
                status,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Fit-out procurement link saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `ecomae_fitout_procurement_links`
                    (`project_id`,`cost_code_id`,`requisition_id`,`purchase_order_id`,
                     `goods_receipt_id`,`description`,`quantity`,`committed_amount`,`status`)
                VALUES (?,?,?,?,?,?,?,?,?)
                """),
            cancellationToken,
            request.ProjectId,
            Math.Max(0, request.CostCodeId),
            Math.Max(0, request.RequisitionId),
            Math.Max(0, request.PurchaseOrderId),
            Math.Max(0, request.GoodsReceiptId),
            description,
            quantity,
            committedAmount,
            status).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(
            connection,
            null,
            cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Fit-out procurement link saved", id);
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
