<?php
/**
 * Marketing & growth module — database schema.
 */
defined('_ASTEXE_') or die('No access');

function epc_marketing_ensure_schema(PDO $db): void
{
	$db->exec(
		'CREATE TABLE IF NOT EXISTS `epc_marketing_task_progress` (
			`strategy_key` VARCHAR(64) NOT NULL,
			`task_key` VARCHAR(128) NOT NULL,
			`is_done` TINYINT(1) NOT NULL DEFAULT 0,
			`done_at` INT UNSIGNED NULL DEFAULT NULL,
			`note` TEXT NULL,
			`updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
			PRIMARY KEY (`strategy_key`, `task_key`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
	);

	$db->exec(
		'CREATE TABLE IF NOT EXISTS `epc_marketing_kpi_log` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
			`strategy_key` VARCHAR(64) NOT NULL DEFAULT \'\',
			`kpi_key` VARCHAR(128) NOT NULL,
			`value_decimal` DECIMAL(20,4) NULL DEFAULT NULL,
			`value_text` VARCHAR(512) NOT NULL DEFAULT \'\',
			`note` TEXT NULL,
			`recorded_at` INT UNSIGNED NOT NULL,
			`recorded_by` INT UNSIGNED NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			KEY `kpi_key` (`kpi_key`),
			KEY `recorded_at` (`recorded_at`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
	);

	$db->exec(
		'CREATE TABLE IF NOT EXISTS `epc_marketing_reviews` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
			`strategy_key` VARCHAR(64) NOT NULL,
			`review_type` VARCHAR(32) NOT NULL DEFAULT \'weekly\',
			`score` TINYINT UNSIGNED NOT NULL DEFAULT 0,
			`notes` TEXT NULL,
			`created_at` INT UNSIGNED NOT NULL,
			`created_by` INT UNSIGNED NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			KEY `strategy_key` (`strategy_key`),
			KEY `created_at` (`created_at`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
	);
}
