<?php
/**
 * Procurement — supplier master extensions (separate from warehouse/stock).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/../finance/epc_erp_schema.php';

function epc_procurement_ensure_schema(PDO $db)
{
	epc_erp_ensure_schema($db);
	$cols = array(
		'legal_reg_no' => "varchar(64) DEFAULT NULL",
		'legal_reg_type' => "varchar(8) NOT NULL DEFAULT 'TL'",
		'authority_name' => "varchar(255) DEFAULT NULL",
		'address_line1' => "varchar(255) DEFAULT NULL",
		'city' => "varchar(128) DEFAULT NULL",
		'emirate' => "varchar(64) DEFAULT NULL",
		'payment_terms' => "varchar(255) DEFAULT NULL",
		'notes' => "text",
		'is_procurement_master' => "tinyint(1) NOT NULL DEFAULT 1",
		// Storefront / price-list vendor code (short). Full legal name stays in `name`.
		'vendor_code' => "varchar(64) DEFAULT NULL",
	);
	foreach ($cols as $col => $def) {
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_suppliers', $col, $def);
	}

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_procurement_advances` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`supplier_id` int(11) NOT NULL,
		`time` int(11) NOT NULL,
		`amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`reference` varchar(128) DEFAULT NULL,
		`note` text,
		`cash_entry_id` int(11) NOT NULL DEFAULT 0,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		PRIMARY KEY (`id`),
		KEY `x_supplier` (`supplier_id`,`active`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Supplier advance payments';");
}
