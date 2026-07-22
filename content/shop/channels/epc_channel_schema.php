<?php
/**
 * Marketplace (Amazon/eBay) + carrier (DHL/FedEx/Aramex) integration schema.
 */
defined('_ASTEXE_') or die('No access');

function epc_channel_ensure_schema(PDO $db)
{
	$db->exec(
		'CREATE TABLE IF NOT EXISTS `epc_marketplace_channels` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
			`code` VARCHAR(64) NOT NULL,
			`name` VARCHAR(128) NOT NULL,
			`marketplace_id` VARCHAR(64) DEFAULT NULL,
			`active` TINYINT(1) NOT NULL DEFAULT 1,
			`demo_mode` TINYINT(1) NOT NULL DEFAULT 1,
			`config_json` TEXT,
			`last_sync_at` INT UNSIGNED NOT NULL DEFAULT 0,
			`time_created` INT UNSIGNED NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			UNIQUE KEY `code` (`code`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
	// Widen code for worldwide marketplace keys (amazon_co_uk, mercadolibre, …).
	try {
		$db->exec('ALTER TABLE `epc_marketplace_channels` MODIFY `code` VARCHAR(64) NOT NULL');
	} catch (Throwable $e) {
		// ignore if already applied / unsupported
	}

	$db->exec(
		'CREATE TABLE IF NOT EXISTS `epc_marketplace_sku_map` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
			`channel_id` INT UNSIGNED NOT NULL,
			`manufacturer` VARCHAR(128) NOT NULL DEFAULT \'\',
			`article` VARCHAR(128) NOT NULL DEFAULT \'\',
			`external_sku` VARCHAR(128) NOT NULL,
			`external_asin` VARCHAR(32) DEFAULT NULL,
			`title` VARCHAR(255) DEFAULT NULL,
			`price` DECIMAL(12,2) NOT NULL DEFAULT 0,
			`stock_qty` INT NOT NULL DEFAULT 0,
			`active` TINYINT(1) NOT NULL DEFAULT 1,
			`time_updated` INT UNSIGNED NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			UNIQUE KEY `channel_ext_sku` (`channel_id`, `external_sku`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);

	$db->exec(
		'CREATE TABLE IF NOT EXISTS `epc_marketplace_orders` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
			`channel_id` INT UNSIGNED NOT NULL,
			`external_order_id` VARCHAR(64) NOT NULL,
			`status` VARCHAR(32) NOT NULL DEFAULT \'pending\',
			`customer_name` VARCHAR(128) DEFAULT NULL,
			`customer_email` VARCHAR(128) DEFAULT NULL,
			`ship_city` VARCHAR(128) DEFAULT NULL,
			`ship_country` VARCHAR(8) DEFAULT NULL,
			`currency` VARCHAR(8) NOT NULL DEFAULT \'AED\',
			`total_amount` DECIMAL(12,2) NOT NULL DEFAULT 0,
			`items_json` TEXT,
			`shop_order_id` INT UNSIGNED DEFAULT NULL,
			`imported_at` INT UNSIGNED NOT NULL DEFAULT 0,
			`time_created` INT UNSIGNED NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			UNIQUE KEY `channel_ext` (`channel_id`, `external_order_id`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);

	$db->exec(
		'CREATE TABLE IF NOT EXISTS `epc_carrier_accounts` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
			`code` VARCHAR(32) NOT NULL,
			`name` VARCHAR(128) NOT NULL,
			`active` TINYINT(1) NOT NULL DEFAULT 1,
			`demo_mode` TINYINT(1) NOT NULL DEFAULT 1,
			`config_json` TEXT,
			`time_created` INT UNSIGNED NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			UNIQUE KEY `code` (`code`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);

	$db->exec(
		'CREATE TABLE IF NOT EXISTS `epc_carrier_shipments` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
			`order_id` INT UNSIGNED NOT NULL,
			`carrier_code` VARCHAR(32) NOT NULL,
			`service_code` VARCHAR(64) DEFAULT NULL,
			`tracking_number` VARCHAR(64) DEFAULT NULL,
			`label_url` VARCHAR(512) DEFAULT NULL,
			`status` VARCHAR(32) NOT NULL DEFAULT \'draft\',
			`weight_kg` DECIMAL(8,3) NOT NULL DEFAULT 0,
			`cost` DECIMAL(12,2) NOT NULL DEFAULT 0,
			`currency` VARCHAR(8) NOT NULL DEFAULT \'AED\',
			`recipient_json` TEXT,
			`raw_json` TEXT,
			`shipped_at` INT UNSIGNED NOT NULL DEFAULT 0,
			`time_created` INT UNSIGNED NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			KEY `order_id` (`order_id`),
			KEY `tracking` (`tracking_number`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);

	$db->exec(
		'CREATE TABLE IF NOT EXISTS `epc_channel_sync_log` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
			`kind` VARCHAR(32) NOT NULL,
			`channel_code` VARCHAR(32) DEFAULT NULL,
			`message` VARCHAR(512) NOT NULL,
			`payload_json` TEXT,
			`time_created` INT UNSIGNED NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			KEY `kind_time` (`kind`, `time_created`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
}
