<?php
/**
 * ERP module — database schema (cash/bank, suppliers, purchases, payables).
 */
defined('_ASTEXE_') or die('No access');

function epc_erp_ensure_schema(PDO $db)
{
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_cash_bank_accounts` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`name` varchar(255) NOT NULL,
		`account_type` enum('cash','bank') NOT NULL DEFAULT 'cash',
		`bank_name` varchar(255) DEFAULT NULL,
		`account_number` varchar(64) DEFAULT NULL,
		`currency_code` varchar(8) NOT NULL DEFAULT 'AED',
		`opening_balance` decimal(14,2) NOT NULL DEFAULT 0.00,
		`office_id` int(11) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_active` (`active`),
		KEY `x_office` (`office_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP cash and bank accounts';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_cash_bank_entries` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`account_id` int(11) NOT NULL,
		`time` int(11) NOT NULL,
		`entry_type` enum('receipt','payment','transfer_in','transfer_out','adjustment') NOT NULL DEFAULT 'receipt',
		`direction` tinyint(1) NOT NULL COMMENT '1=increase balance, 0=decrease',
		`amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`counterparty_type` enum('none','customer','supplier','internal') NOT NULL DEFAULT 'none',
		`counterparty_id` int(11) NOT NULL DEFAULT 0,
		`order_id` int(11) NOT NULL DEFAULT 0,
		`purchase_id` int(11) NOT NULL DEFAULT 0,
		`transfer_pair_id` int(11) NOT NULL DEFAULT 0,
		`reference` varchar(128) DEFAULT NULL,
		`note` text,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		PRIMARY KEY (`id`),
		KEY `x_account` (`account_id`,`active`),
		KEY `x_time` (`time`),
		KEY `x_order` (`order_id`),
		KEY `x_purchase` (`purchase_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP cash/bank journal entries';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_suppliers` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`storage_id` int(11) DEFAULT NULL,
		`name` varchar(255) NOT NULL,
		`contact_email` varchar(255) DEFAULT NULL,
		`contact_phone` varchar(64) DEFAULT NULL,
		`trn` varchar(64) DEFAULT NULL,
		`currency_code` varchar(8) NOT NULL DEFAULT 'AED',
		`active` tinyint(1) NOT NULL DEFAULT 1,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_storage` (`storage_id`),
		KEY `x_active` (`active`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP supplier master';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_purchases` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`supplier_id` int(11) NOT NULL,
		`order_id` int(11) NOT NULL DEFAULT 0,
		`storage_id` int(11) NOT NULL DEFAULT 0,
		`invoice_number` varchar(64) DEFAULT NULL,
		`purchase_date` int(11) NOT NULL DEFAULT 0,
		`amount_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
		`vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`total_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`status` enum('draft','confirmed','paid','partial') NOT NULL DEFAULT 'confirmed',
		`note` text,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		PRIMARY KEY (`id`),
		KEY `x_supplier` (`supplier_id`),
		KEY `x_order` (`order_id`),
		KEY `x_date` (`purchase_date`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP supplier purchase invoices';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_supplier_accounting` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`supplier_id` int(11) NOT NULL,
		`time` int(11) NOT NULL,
		`is_credit` tinyint(1) NOT NULL COMMENT '1=invoice/payable up, 0=payment down',
		`amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`purchase_id` int(11) NOT NULL DEFAULT 0,
		`cash_entry_id` int(11) NOT NULL DEFAULT 0,
		`order_id` int(11) NOT NULL DEFAULT 0,
		`reference` varchar(128) DEFAULT NULL,
		`note` text,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		PRIMARY KEY (`id`),
		KEY `x_supplier` (`supplier_id`,`active`,`is_credit`),
		KEY `x_purchase` (`purchase_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP supplier payable ledger';");

	epc_erp_schema_add_column_if_missing($db, 'epc_erp_supplier_accounting', 'entry_kind', "varchar(32) NOT NULL DEFAULT 'invoice'");
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_supplier_accounting', 'gl_journal_id', 'int(11) NOT NULL DEFAULT 0');

	epc_erp_schema_add_column_if_missing($db, 'epc_erp_suppliers', 'country_code', "varchar(8) NOT NULL DEFAULT 'AE'");
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_suppliers', 'vat_registered', 'tinyint(1) NOT NULL DEFAULT 1');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_purchases', 'vat_applicable', 'tinyint(1) NOT NULL DEFAULT 1');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_purchases', 'vat_rate', 'decimal(5,2) NOT NULL DEFAULT 5.00');

	// Bank account master — extended fields (legal entity / business unit + full bank details).
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_accounts', 'legal_entity_id', 'int(11) NOT NULL DEFAULT 0');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_accounts', 'business_unit_id', 'int(11) NOT NULL DEFAULT 0');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_accounts', 'gl_account_id', 'int(11) NOT NULL DEFAULT 0');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_accounts', 'iban', 'varchar(64) DEFAULT NULL');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_accounts', 'swift_bic', 'varchar(32) DEFAULT NULL');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_accounts', 'bank_branch', 'varchar(255) DEFAULT NULL');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_accounts', 'routing_code', 'varchar(64) DEFAULT NULL');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_accounts', 'address', 'varchar(512) DEFAULT NULL');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_accounts', 'contact_name', 'varchar(255) DEFAULT NULL');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_accounts', 'contact_phone', 'varchar(64) DEFAULT NULL');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_accounts', 'contact_email', 'varchar(128) DEFAULT NULL');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_accounts', 'status', "varchar(16) NOT NULL DEFAULT 'active'");
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_accounts', 'notes', 'varchar(1000) DEFAULT NULL');

	epc_erp_seed_defaults($db);
}

function epc_erp_schema_add_column_if_missing(PDO $db, $table, $column, $definition)
{
	try {
		$st = $db->prepare('SHOW COLUMNS FROM `' . str_replace('`', '', $table) . '` LIKE ?');
		$st->execute(array($column));
		if (!$st->fetch()) {
			$db->exec('ALTER TABLE `' . str_replace('`', '', $table) . '` ADD `' . str_replace('`', '', $column) . '` ' . $definition);
		}
	} catch (Exception $e) {
	}
}

function epc_erp_seed_defaults(PDO $db)
{
	$cnt = (int)$db->query('SELECT COUNT(*) FROM `epc_erp_cash_bank_accounts`')->fetchColumn();
	if ($cnt > 0) {
		return;
	}
	$now = time();
	$ins = $db->prepare(
		'INSERT INTO `epc_erp_cash_bank_accounts`
		(`name`, `account_type`, `bank_name`, `currency_code`, `opening_balance`, `time_created`)
		VALUES (?, ?, ?, ?, ?, ?)'
	);
	$ins->execute(array('Main cash — AED', 'cash', null, 'AED', 0, $now));
	$ins->execute(array('Main bank — AED', 'bank', 'Default bank', 'AED', 0, $now));
}
