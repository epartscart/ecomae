using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// PHP <c>epc_erp_phase8_ensure_schema</c> RFQ twin plus SCM RFQ tables for
/// ERP-only tenants that never ran the installer. Called only on confirmed
/// writes (and ERP-only onboard).
/// </summary>
public static class ErpRfqSchema
{
    public static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_erp_rfq` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `rfq_no` varchar(32) NOT NULL,
                `supplier_id` int(11) NOT NULL DEFAULT 0,
                `title` varchar(255) NOT NULL,
                `description` text,
                `amount_est` decimal(14,2) NOT NULL DEFAULT 0.00,
                `currency_code` varchar(8) NOT NULL DEFAULT 'AED',
                `status` enum('draft','sent','quoted','accepted','rejected','cancelled') NOT NULL DEFAULT 'draft',
                `due_date` int(11) NOT NULL DEFAULT 0,
                `order_id` int(11) NOT NULL DEFAULT 0,
                `admin_id` int(11) NOT NULL DEFAULT 0,
                `time_created` int(11) NOT NULL DEFAULT 0,
                `time_updated` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                UNIQUE KEY `x_rfq_no` (`rfq_no`),
                KEY `x_supplier` (`supplier_id`),
                KEY `x_status` (`status`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP supplier RFQ / proposals'
            """,
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_scm_rfq` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `rfq_no` varchar(32) NOT NULL,
                `title` varchar(255) NOT NULL DEFAULT '',
                `status` enum('draft','sent','closed','awarded','cancelled') NOT NULL DEFAULT 'draft',
                `due_date` int(11) NOT NULL DEFAULT 0,
                `notes` text,
                `awarded_supplier_id` int(11) NOT NULL DEFAULT 0,
                `awarded_po_id` int(11) NOT NULL DEFAULT 0,
                `admin_id` int(11) NOT NULL DEFAULT 0,
                `time_created` int(11) NOT NULL DEFAULT 0,
                `time_updated` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                UNIQUE KEY `x_rfq_no` (`rfq_no`),
                KEY `x_status` (`status`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='RFQ headers'
            """,
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_scm_rfq_lines` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `rfq_id` int(11) NOT NULL,
                `item_id` int(11) NOT NULL DEFAULT 0,
                `description` varchar(255) NOT NULL DEFAULT '',
                `qty` decimal(14,3) NOT NULL DEFAULT 0.000,
                `unit` varchar(16) NOT NULL DEFAULT 'pcs',
                `target_price` decimal(14,4) NOT NULL DEFAULT 0.0000,
                `sort_order` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                KEY `x_rfq` (`rfq_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='RFQ request lines'
            """,
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_scm_rfq_responses` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `rfq_id` int(11) NOT NULL,
                `rfq_line_id` int(11) NOT NULL DEFAULT 0,
                `supplier_id` int(11) NOT NULL DEFAULT 0,
                `unit_price` decimal(14,4) NOT NULL DEFAULT 0.0000,
                `lead_time_days` int(11) NOT NULL DEFAULT 0,
                `notes` varchar(255) NOT NULL DEFAULT '',
                `time_created` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                KEY `x_rfq` (`rfq_id`),
                KEY `x_supplier` (`supplier_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Supplier quotes against RFQ lines'
            """,
            cancellationToken).ConfigureAwait(false);
    }
}
