<?php
/**
 * Document Control System — schema (templates, company profile, attachments).
 */
defined('_ASTEXE_') or die('No access');

function epc_doc_control_ensure_schema(PDO $pdo): void
{
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_document_company` (
			`id` TINYINT UNSIGNED NOT NULL DEFAULT 1 PRIMARY KEY,
			`legal_name` VARCHAR(255) NOT NULL DEFAULT \'\',
			`trade_name` VARCHAR(255) NOT NULL DEFAULT \'\',
			`address_line1` VARCHAR(255) NOT NULL DEFAULT \'\',
			`address_line2` VARCHAR(255) NOT NULL DEFAULT \'\',
			`city` VARCHAR(120) NOT NULL DEFAULT \'\',
			`country` VARCHAR(80) NOT NULL DEFAULT \'United Arab Emirates\',
			`trn` VARCHAR(32) NOT NULL DEFAULT \'\',
			`phone` VARCHAR(64) NOT NULL DEFAULT \'\',
			`email` VARCHAR(120) NOT NULL DEFAULT \'\',
			`website` VARCHAR(120) NOT NULL DEFAULT \'\',
			`logo_path` VARCHAR(255) NOT NULL DEFAULT \'\',
			`bank_name` VARCHAR(120) NOT NULL DEFAULT \'\',
			`bank_iban` VARCHAR(64) NOT NULL DEFAULT \'\',
			`legal_footer` TEXT NULL,
			`updated_at` INT NOT NULL DEFAULT 0
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_document_templates` (
			`code` VARCHAR(32) NOT NULL PRIMARY KEY,
			`title` VARCHAR(120) NOT NULL,
			`description` VARCHAR(255) NOT NULL DEFAULT \'\',
			`category` VARCHAR(32) NOT NULL DEFAULT \'sales\',
			`header_html` MEDIUMTEXT NULL,
			`body_html` MEDIUMTEXT NULL,
			`footer_html` MEDIUMTEXT NULL,
			`css_extra` TEXT NULL,
			`active` TINYINT(1) NOT NULL DEFAULT 1,
			`sort_order` INT NOT NULL DEFAULT 0,
			`updated_at` INT NOT NULL DEFAULT 0
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_document_attachments` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`entity_type` VARCHAR(32) NOT NULL DEFAULT \'order\',
			`entity_id` INT NOT NULL DEFAULT 0,
			`doc_category` VARCHAR(32) NOT NULL DEFAULT \'supplier_invoice\',
			`supplier_name` VARCHAR(255) NOT NULL DEFAULT \'\',
			`reference_no` VARCHAR(64) NOT NULL DEFAULT \'\',
			`file_name` VARCHAR(255) NOT NULL,
			`file_path` VARCHAR(512) NOT NULL,
			`mime_type` VARCHAR(120) NOT NULL DEFAULT \'\',
			`file_size` INT NOT NULL DEFAULT 0,
			`notes` TEXT NULL,
			`uploaded_by` INT NOT NULL DEFAULT 0,
			`uploaded_at` INT NOT NULL DEFAULT 0,
			KEY `idx_entity` (`entity_type`, `entity_id`),
			KEY `idx_category` (`doc_category`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);

	$cnt = (int)$pdo->query('SELECT COUNT(*) FROM `epc_document_company`')->fetchColumn();
	if ($cnt === 0) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_site_context.php';
		$co = epc_site_document_company_defaults();
		$ins = $pdo->prepare(
			'INSERT INTO `epc_document_company`
			(`id`, `legal_name`, `trade_name`, `address_line1`, `city`, `country`, `trn`, `phone`, `email`, `website`, `legal_footer`, `updated_at`)
			VALUES (1, ?, ?, ?, ?, ?, \'\', ?, ?, ?, ?, ?)'
		);
		$ins->execute(array(
			$co['legal_name'],
			$co['trade_name'],
			$co['address_line1'],
			$co['city'],
			$co['country'],
			$co['phone'],
			$co['email'],
			$co['website'],
			'This document is issued in accordance with UAE Federal Tax Authority (FTA) requirements. VAT Registration Number (TRN) must appear on all tax invoices. Retain records for minimum 5 years.',
			time(),
		));
	}

	epc_doc_control_seed_templates($pdo);
}

function epc_doc_control_seed_templates(PDO $pdo): void
{
	require_once __DIR__ . '/epc_document_control_templates_default.php';
	$now = time();
	$ins = $pdo->prepare(
		'INSERT INTO `epc_document_templates`
		(`code`, `title`, `description`, `category`, `header_html`, `body_html`, `footer_html`, `css_extra`, `active`, `sort_order`, `updated_at`)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, 1, ?, ?)
		ON DUPLICATE KEY UPDATE
		`title` = VALUES(`title`), `description` = VALUES(`description`), `category` = VALUES(`category`),
		`updated_at` = VALUES(`updated_at`)'
	);
	foreach (epc_doc_control_default_templates() as $row) {
		$ins->execute(array(
			$row['code'],
			$row['title'],
			$row['description'],
			$row['category'],
			$row['header_html'],
			$row['body_html'],
			$row['footer_html'],
			$row['css_extra'],
			(int)$row['sort_order'],
			$now,
		));
	}
}
