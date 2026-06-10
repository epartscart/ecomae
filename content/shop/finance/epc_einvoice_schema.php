<?php
/**
 * UAE Electronic Invoicing — database schema (PINT-AE / MoF Feb 2026).
 */
defined('_ASTEXE_') or die('No access');

function epc_einvoice_ensure_schema(PDO $db)
{
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_einvoice_settings` (
		`setting_key` varchar(64) NOT NULL,
		`setting_value` text,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`setting_key`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='UAE e-invoice seller & ASP settings';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_einvoice_buyer_profiles` (
		`user_id` int(11) NOT NULL,
		`buyer_name` varchar(255) DEFAULT NULL,
		`trn` varchar(20) DEFAULT NULL,
		`tin` varchar(10) DEFAULT NULL,
		`legal_reg_no` varchar(64) DEFAULT NULL,
		`legal_reg_type` enum('TL','EID','PAS','CD') NOT NULL DEFAULT 'TL',
		`authority_name` varchar(255) DEFAULT NULL,
		`address_line1` varchar(255) DEFAULT NULL,
		`city` varchar(128) DEFAULT NULL,
		`emirate` varchar(64) DEFAULT NULL,
		`country_code` varchar(8) NOT NULL DEFAULT 'AE',
		`phone` varchar(32) DEFAULT NULL,
		`email` varchar(255) DEFAULT NULL,
		`electronic_id` varchar(8) NOT NULL DEFAULT '0235',
		`peppol_endpoint` varchar(32) DEFAULT NULL,
		`buyer_onboarded` tinyint(1) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`user_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Buyer Peppol / TRN profile for e-invoicing';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_einvoice_documents` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`uuid` char(36) NOT NULL,
		`invoice_number` varchar(64) NOT NULL,
		`order_id` int(11) NOT NULL DEFAULT 0,
		`user_id` int(11) NOT NULL DEFAULT 0,
		`doc_category` enum('tax_invoice','tax_credit_note','commercial_invoice','credit_note') NOT NULL DEFAULT 'tax_invoice',
		`invoice_type_code` varchar(8) NOT NULL DEFAULT '380',
		`issue_date` int(11) NOT NULL DEFAULT 0,
		`payment_due_date` int(11) NOT NULL DEFAULT 0,
		`vat_point_date` int(11) NOT NULL DEFAULT 0,
		`currency_code` varchar(8) NOT NULL DEFAULT 'AED',
		`vat_currency_code` varchar(8) NOT NULL DEFAULT 'AED',
		`transaction_type_code` char(8) NOT NULL DEFAULT '00000000',
		`payment_means_code` varchar(8) NOT NULL DEFAULT '30',
		`payment_terms` varchar(255) DEFAULT NULL,
		`bank_account` varchar(64) DEFAULT NULL,
		`billing_period_start` int(11) NOT NULL DEFAULT 0,
		`billing_period_end` int(11) NOT NULL DEFAULT 0,
		`seller_json` mediumtext,
		`buyer_json` mediumtext,
		`subtotal_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
		`total_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
		`total_incl_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
		`paid_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`rounding_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`amount_due` decimal(14,2) NOT NULL DEFAULT 0.00,
		`tax_breakdown_json` text,
		`status` enum('draft','validated','queued','submitted','accepted','rejected','cancelled') NOT NULL DEFAULT 'draft',
		`validation_ok` tinyint(1) NOT NULL DEFAULT 0,
		`validation_errors_json` text,
		`xml_content` mediumtext,
		`asp_name` varchar(128) DEFAULT NULL,
		`asp_reference` varchar(128) DEFAULT NULL,
		`fta_report_status` varchar(32) DEFAULT NULL,
		`time_created` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		`time_submitted` int(11) NOT NULL DEFAULT 0,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_uuid` (`uuid`),
		UNIQUE KEY `x_invoice_no` (`invoice_number`),
		KEY `x_order` (`order_id`),
		KEY `x_status` (`status`,`issue_date`),
		KEY `x_user` (`user_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='UAE electronic invoice documents';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_einvoice_lines` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`document_id` int(11) NOT NULL,
		`line_no` int(11) NOT NULL DEFAULT 1,
		`item_name` varchar(255) NOT NULL,
		`item_description` varchar(512) DEFAULT NULL,
		`item_type` enum('G','S','B') NOT NULL DEFAULT 'G',
		`quantity` decimal(14,4) NOT NULL DEFAULT 0.0000,
		`uom_code` varchar(16) NOT NULL DEFAULT 'C62',
		`unit_price` decimal(14,4) NOT NULL DEFAULT 0.0000,
		`line_net` decimal(14,2) NOT NULL DEFAULT 0.00,
		`tax_category` varchar(8) NOT NULL DEFAULT 'S',
		`tax_rate` decimal(5,2) NOT NULL DEFAULT 5.00,
		`tax_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`gross_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`vat_line_aed` decimal(14,2) NOT NULL DEFAULT 0.00,
		`line_amount_aed` decimal(14,2) NOT NULL DEFAULT 0.00,
		PRIMARY KEY (`id`),
		KEY `x_doc` (`document_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='UAE e-invoice line items';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_einvoice_events` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`document_id` int(11) NOT NULL,
		`event_type` varchar(32) NOT NULL,
		`status` varchar(32) NOT NULL DEFAULT 'info',
		`message` text,
		`payload_json` mediumtext,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_doc` (`document_id`,`time_created`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='E-invoice transmission & FTA event log';");

	epc_einvoice_seed_defaults($db);
}

function epc_einvoice_seed_defaults(PDO $db)
{
	$defaults = array(
		'seller_name' => 'ePartsCart LLC',
		'seller_trn' => '',
		'seller_tin' => '',
		'seller_legal_reg_no' => '',
		'seller_legal_reg_type' => 'TL',
		'seller_authority_name' => 'Dubai Economy and Tourism',
		'seller_address_line1' => '',
		'seller_city' => 'Dubai',
		'seller_emirate' => 'Dubai',
		'seller_country_code' => 'AE',
		'seller_phone' => '',
		'seller_email' => '',
		'seller_bank_account' => '',
		'payment_means_code' => '30',
		'payment_terms' => 'Within 7 days',
		'asp_name' => '',
		'asp_api_mode' => 'manual',
		'asp_api_url' => '',
		'asp_api_key' => '',
		'einvoice_enabled' => '1',
		'default_doc_category' => 'tax_invoice',
		'default_payment_due_days' => '7',
		'auto_validate' => '1',
	);
	$ins = $db->prepare(
		'INSERT INTO `epc_einvoice_settings` (`setting_key`, `setting_value`, `time_updated`)
		VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `setting_key` = `setting_key`'
	);
	$now = time();
	foreach ($defaults as $k => $v) {
		$ins->execute(array($k, (string)$v, $now));
	}
}
