using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// PHP <c>epc_uae_tax_compliance_ensure_schema</c> twin for ERP-only tenants that
/// never ran the PHP installer. Called only on confirmed writes (and ERP-only onboard).
/// </summary>
public static class ErpUaeTaxComplianceSchema
{
    public static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_uae_vat_advance` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `order_id` int(11) NOT NULL DEFAULT 0,
                `user_id` int(11) NOT NULL DEFAULT 0,
                `ledger_id` int(11) NOT NULL DEFAULT 0,
                `payment_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
                `amount_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
                `vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
                `vat_rate` decimal(5,2) NOT NULL DEFAULT 5.00,
                `payment_time` int(11) NOT NULL DEFAULT 0,
                `einvoice_document_id` int(11) NOT NULL DEFAULT 0,
                `adjusted` tinyint(1) NOT NULL DEFAULT 0,
                `time_created` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                UNIQUE KEY `x_ledger` (`ledger_id`),
                KEY `x_order` (`order_id`,`adjusted`),
                KEY `x_time` (`payment_time`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='UAE VAT on customer advance payments'
            """,
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_uae_tax_compliance_cache` (
                `cache_key` varchar(64) NOT NULL,
                `payload_json` mediumtext,
                `source_url` varchar(512) DEFAULT NULL,
                `time_fetched` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`cache_key`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='FTA legislation fetch cache'
            """,
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_uae_tax_legislation_items` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `item_key` varchar(512) NOT NULL,
                `slug` varchar(128) NOT NULL DEFAULT '',
                `title` varchar(512) NOT NULL DEFAULT '',
                `issue_date` varchar(32) NOT NULL DEFAULT '',
                `publish_date` varchar(32) NOT NULL DEFAULT '',
                `category` varchar(128) NOT NULL DEFAULT '',
                `tax_category` varchar(32) NOT NULL DEFAULT 'general',
                `pdf_url` varchar(512) NOT NULL DEFAULT '',
                `erp_summary` mediumtext,
                `compliance_actions_json` mediumtext,
                `pattern_key` varchar(64) NOT NULL DEFAULT '',
                `is_new` tinyint(1) NOT NULL DEFAULT 0,
                `is_updated` tinyint(1) NOT NULL DEFAULT 0,
                `time_synced` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                UNIQUE KEY `x_item_key` (`item_key`(255)),
                KEY `x_tax_cat` (`tax_category`),
                KEY `x_issue` (`issue_date`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='UAE FTA legislation with ERP summaries'
            """,
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_uae_ct_adjustments` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `period_key` varchar(32) NOT NULL,
                `adjustment_key` varchar(64) NOT NULL,
                `direction` enum('add','deduct') NOT NULL DEFAULT 'add',
                `amount` decimal(14,2) NOT NULL DEFAULT 0.00,
                `notes` varchar(512) NOT NULL DEFAULT '',
                `time_updated` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                UNIQUE KEY `x_period_adj` (`period_key`,`adjustment_key`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='UAE CT P&L adjustments by period'
            """,
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_uae_tax_legislation_checklist` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `item_key` varchar(512) NOT NULL,
                `action_key` varchar(64) NOT NULL,
                `action_text` varchar(700) NOT NULL DEFAULT '',
                `status` enum('pending','done') NOT NULL DEFAULT 'pending',
                `time_updated` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                UNIQUE KEY `x_item_action` (`item_key`(191),`action_key`),
                KEY `x_status` (`status`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Per-instrument ERP compliance checklist implementation status'
            """,
            cancellationToken).ConfigureAwait(false);

        await ErpLazySchema.EnsureVatRefundsAsync(connection, cancellationToken).ConfigureAwait(false);
    }
}
