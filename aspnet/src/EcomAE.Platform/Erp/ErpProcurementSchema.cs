using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// PHP <c>epc_proc_ensure_schema</c> twin for ERP-only tenants that never ran
/// the installer. Called only on confirmed writes (and ERP-only onboard).
/// </summary>
public static class ErpProcurementSchema
{
    public static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_proc_category` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `company_id` int(11) NOT NULL DEFAULT 0,
                `code` varchar(40) NOT NULL DEFAULT '',
                `name` varchar(160) NOT NULL DEFAULT '',
                `parent_id` int(11) NOT NULL DEFAULT 0,
                `default_account` varchar(40) NOT NULL DEFAULT '',
                `active` tinyint(1) NOT NULL DEFAULT 1,
                `time_created` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                KEY `x_company` (`company_id`),
                KEY `x_parent` (`parent_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Procurement categories'
            """,
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_proc_policy` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `company_id` int(11) NOT NULL DEFAULT 0,
                `name` varchar(160) NOT NULL DEFAULT '',
                `category_id` int(11) NOT NULL DEFAULT 0,
                `approval_threshold` decimal(18,2) NOT NULL DEFAULT 0.00,
                `preferred_vendor` varchar(160) NOT NULL DEFAULT '',
                `active` tinyint(1) NOT NULL DEFAULT 1,
                `time_created` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                KEY `x_company` (`company_id`),
                KEY `x_category` (`category_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Procurement policies'
            """,
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_proc_req` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `company_id` int(11) NOT NULL DEFAULT 0,
                `req_number` varchar(40) NOT NULL DEFAULT '',
                `requester` varchar(160) NOT NULL DEFAULT '',
                `business_unit_id` int(11) NOT NULL DEFAULT 0,
                `status` varchar(20) NOT NULL DEFAULT 'draft',
                `justification` text,
                `total` decimal(18,2) NOT NULL DEFAULT 0.00,
                `requires_approval` tinyint(1) NOT NULL DEFAULT 0,
                `decided_by` varchar(160) NOT NULL DEFAULT '',
                `decision_note` varchar(255) NOT NULL DEFAULT '',
                `po_ref` varchar(40) NOT NULL DEFAULT '',
                `time_created` int(11) NOT NULL DEFAULT 0,
                `time_updated` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                KEY `x_company` (`company_id`),
                KEY `x_status` (`status`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Purchase requisitions'
            """,
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_proc_req_line` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `req_id` int(11) NOT NULL DEFAULT 0,
                `category_id` int(11) NOT NULL DEFAULT 0,
                `item_code` varchar(80) NOT NULL DEFAULT '',
                `description` varchar(255) NOT NULL DEFAULT '',
                `qty` decimal(18,4) NOT NULL DEFAULT 0.0000,
                `unit_price` decimal(18,2) NOT NULL DEFAULT 0.00,
                `line_total` decimal(18,2) NOT NULL DEFAULT 0.00,
                `preferred_vendor` varchar(160) NOT NULL DEFAULT '',
                PRIMARY KEY (`id`),
                KEY `x_req` (`req_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Purchase requisition lines'
            """,
            cancellationToken).ConfigureAwait(false);
    }
}
