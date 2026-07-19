<?php
/**
 * Frontend multi-vendor accounts — self-serve sellers (no CP login required).
 * Includes UAE e-invoice / FTA seller identity fields (TRN, legal reg, address).
 */
defined('_ASTEXE_') or define('_ASTEXE_', true);

if (!function_exists('epc_vendor_ensure_schema')) {
	function epc_vendor_ensure_schema(PDO $db): void
	{
		$db->exec(
			"CREATE TABLE IF NOT EXISTS `epc_vendor_accounts` (
				`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
				`user_id` INT UNSIGNED NOT NULL DEFAULT 0,
				`storage_id` INT UNSIGNED NOT NULL DEFAULT 0,
				`vendor_full` VARCHAR(255) NOT NULL DEFAULT '',
				`vendor_short` VARCHAR(64) NOT NULL DEFAULT '',
				`legal_name` VARCHAR(255) NOT NULL DEFAULT '',
				`trn` VARCHAR(32) NOT NULL DEFAULT '',
				`vat_registered` TINYINT(1) NOT NULL DEFAULT 1,
				`tin` VARCHAR(32) NOT NULL DEFAULT '',
				`peppol_endpoint` VARCHAR(64) NOT NULL DEFAULT '',
				`legal_reg_no` VARCHAR(64) NOT NULL DEFAULT '',
				`legal_reg_type` VARCHAR(8) NOT NULL DEFAULT 'TL',
				`authority_name` VARCHAR(255) NOT NULL DEFAULT '',
				`address_line1` VARCHAR(255) NOT NULL DEFAULT '',
				`address_line2` VARCHAR(255) NOT NULL DEFAULT '',
				`city` VARCHAR(120) NOT NULL DEFAULT '',
				`emirate` VARCHAR(64) NOT NULL DEFAULT '',
				`postal_code` VARCHAR(32) NOT NULL DEFAULT '',
				`country_code` VARCHAR(8) NOT NULL DEFAULT 'AE',
				`contact_name` VARCHAR(190) NOT NULL DEFAULT '',
				`contact_job_title` VARCHAR(120) NOT NULL DEFAULT '',
				`phone` VARCHAR(64) NOT NULL DEFAULT '',
				`billing_email` VARCHAR(190) NOT NULL DEFAULT '',
				`status` VARCHAR(16) NOT NULL DEFAULT 'pending',
				`notes` TEXT NULL,
				`approved_by` INT UNSIGNED NOT NULL DEFAULT 0,
				`approved_at` INT UNSIGNED NOT NULL DEFAULT 0,
				`created_at` INT UNSIGNED NOT NULL DEFAULT 0,
				`updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
				PRIMARY KEY (`id`),
				UNIQUE KEY `uq_user` (`user_id`),
				UNIQUE KEY `uq_short` (`vendor_short`),
				KEY `idx_status` (`status`),
				KEY `idx_storage` (`storage_id`),
				KEY `idx_trn` (`trn`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"
		);

		$cols = array(
			'legal_name' => "VARCHAR(255) NOT NULL DEFAULT ''",
			'trn' => "VARCHAR(32) NOT NULL DEFAULT ''",
			'vat_registered' => "TINYINT(1) NOT NULL DEFAULT 1",
			'tin' => "VARCHAR(32) NOT NULL DEFAULT ''",
			'peppol_endpoint' => "VARCHAR(64) NOT NULL DEFAULT ''",
			'legal_reg_no' => "VARCHAR(64) NOT NULL DEFAULT ''",
			'legal_reg_type' => "VARCHAR(8) NOT NULL DEFAULT 'TL'",
			'authority_name' => "VARCHAR(255) NOT NULL DEFAULT ''",
			'address_line1' => "VARCHAR(255) NOT NULL DEFAULT ''",
			'address_line2' => "VARCHAR(255) NOT NULL DEFAULT ''",
			'emirate' => "VARCHAR(64) NOT NULL DEFAULT ''",
			'postal_code' => "VARCHAR(32) NOT NULL DEFAULT ''",
			'country_code' => "VARCHAR(8) NOT NULL DEFAULT 'AE'",
			'contact_job_title' => "VARCHAR(120) NOT NULL DEFAULT ''",
			'billing_email' => "VARCHAR(190) NOT NULL DEFAULT ''",
		);
		foreach ($cols as $name => $def) {
			try {
				$st = $db->prepare('SHOW COLUMNS FROM `epc_vendor_accounts` LIKE ?');
				$st->execute(array($name));
				if (!$st->fetchColumn()) {
					$db->exec('ALTER TABLE `epc_vendor_accounts` ADD COLUMN `' . $name . '` ' . $def);
				}
			} catch (Exception $e) {
			}
		}
		try {
			$idx = $db->query("SHOW INDEX FROM `epc_vendor_accounts` WHERE Key_name = 'idx_trn'")->fetch();
			if (!$idx) {
				$db->exec('ALTER TABLE `epc_vendor_accounts` ADD KEY `idx_trn` (`trn`)');
			}
		} catch (Exception $e) {
		}
	}
}

if (!function_exists('epc_vendor_uae_emirates')) {
	function epc_vendor_uae_emirates(): array
	{
		return array(
			'Dubai',
			'Abu Dhabi',
			'Sharjah',
			'Ajman',
			'Umm Al Quwain',
			'Ras Al Khaimah',
			'Fujairah',
		);
	}
}

if (!function_exists('epc_vendor_legal_reg_types')) {
	/** @return array<string,string> */
	function epc_vendor_legal_reg_types(): array
	{
		return array(
			'TL' => 'Trade licence (TL)',
			'EID' => 'Emirates ID (EID)',
			'PAS' => 'Passport (PAS)',
			'CD' => 'Company document (CD)',
		);
	}
}

if (!function_exists('epc_vendor_authority_for_emirate')) {
	function epc_vendor_authority_for_emirate(string $emirate): string
	{
		$map = array(
			'Dubai' => 'Dubai Economy and Tourism',
			'Abu Dhabi' => 'Abu Dhabi Department of Economic Development',
			'Sharjah' => 'Sharjah Economic Development Department',
			'Ajman' => 'Ajman Department of Economic Development',
			'Umm Al Quwain' => 'Umm Al Quwain Department of Economic Development',
			'Ras Al Khaimah' => 'Ras Al Khaimah Department of Economic Development',
			'Fujairah' => 'Fujairah Municipality / Economic Development',
		);
		return $map[$emirate] ?? '';
	}
}

if (!function_exists('epc_vendor_group_key')) {
	function epc_vendor_group_key(): string
	{
		return 'EPC_VENDOR';
	}
}

if (!function_exists('epc_vendor_ensure_group')) {
	function epc_vendor_ensure_group(PDO $db): int
	{
		$key = epc_vendor_group_key();
		$st = $db->prepare('SELECT `id` FROM `groups` WHERE `value` = ? LIMIT 1');
		$st->execute(array($key));
		$id = (int) $st->fetchColumn();
		if ($id > 0) {
			return $id;
		}
		$maxId = (int) $db->query('SELECT IFNULL(MAX(`id`), 0) FROM `groups`')->fetchColumn();
		$id = $maxId + 1;
		$db->prepare(
			'INSERT INTO `groups` (`id`, `value`, `count`, `level`, `parent`, `unblocked`, `for_guests`, `for_registrated`, `for_backend`, `for_percentage`, `description`, `order`)
			 VALUES (?, ?, 0, 2, 1, 1, 0, 0, 0, 0, ?, 96)'
		)->execute(array($id, $key, $key));
		try {
			$db->exec('UPDATE `groups` SET `count` = (SELECT COUNT(*) FROM (SELECT `id` FROM `groups` WHERE `parent` = 1) x) WHERE `id` = 1');
		} catch (Exception $e) {
		}
		return $id;
	}
}
