<?php
/**
 * Frontend multi-vendor accounts — self-serve sellers (no CP login required).
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
				`contact_name` VARCHAR(190) NOT NULL DEFAULT '',
				`phone` VARCHAR(64) NOT NULL DEFAULT '',
				`city` VARCHAR(120) NOT NULL DEFAULT '',
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
				KEY `idx_storage` (`storage_id`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"
		);
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
