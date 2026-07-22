<?php
/**
 * EPC Auto Price & Product Engine — schema, profiles, compare matrix, cross-list.
 */
defined('_ASTEXE_') or die('No access');

function epc_ape_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

/**
 * CP-routed AJAX endpoint (via cp/index.php — not direct /content/…/*.php).
 */
function epc_apai_ajax_url(?string $backend = null): string
{
	global $DP_Config;
	if ($backend === null || $backend === '') {
		$backend = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
	}
	if ($backend === '') {
		$backend = 'cp';
	}
	return '/' . $backend . '/control/portal/ajax_auto_price';
}

function epc_ape_profiles(): array
{
	return array(
		'warehouse_supplier' => array(
			'label' => 'Warehouse / supplier matrix',
			'hint' => 'B2B parts — N suppliers × M warehouses, CSV + manual import.',
		),
		'marketplace_arbitrage' => array(
			'label' => 'Marketplace arbitrage',
			'hint' => 'Monitor Amazon.ae, Noon, eBay — lowest price vs warehouse cost.',
		),
		'professional_services' => array(
			'label' => 'Professional services',
			'hint' => 'Consultancy, accounting, tax advisory — service packages with benchmark pricing.',
		),
	);
}

function epc_ape_source_types(): array
{
	return array(
		'warehouse' => 'Warehouse / storage',
		'supplier' => 'Supplier price list',
		'warehouse_supplier' => 'Warehouse supplier (epartscart)',
		'amazon_ae' => 'Amazon.ae',
		'noon' => 'Noon',
		'ebay' => 'eBay',
		'search_engine' => 'Search engine (.ae query)',
		'custom_website' => 'Custom website domain',
		'manual' => 'Manual / competitor URL',
	);
}

function epc_ape_channel_types(): array
{
	return array(
		'storefront' => 'Storefront catalogue',
		'ebay' => 'eBay listing',
		'amazon' => 'Amazon listing',
		'price_list' => 'Price list export',
	);
}

function epc_ape_ensure_schema(PDO $pdo): void
{
	static $done = array();
	$key = spl_object_hash($pdo);
	if (isset($done[$key])) {
		return;
	}
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_auto_price_tenant_config` (
			`site_key` VARCHAR(64) NOT NULL PRIMARY KEY,
			`profile` VARCHAR(32) NOT NULL DEFAULT \'warehouse_supplier\',
			`currency` VARCHAR(8) NOT NULL DEFAULT \'AED\',
			`active` TINYINT(1) NOT NULL DEFAULT 1,
			`config_json` TEXT NULL,
			`last_crawl_at` INT NOT NULL DEFAULT 0,
			`updated_at` INT NOT NULL DEFAULT 0
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);

	try {
		$col = $pdo->query("SHOW COLUMNS FROM `epc_auto_price_tenant_config` LIKE 'last_crawl_at'")->fetch(PDO::FETCH_ASSOC);
		if (!$col) {
			$pdo->exec('ALTER TABLE `epc_auto_price_tenant_config` ADD COLUMN `last_crawl_at` INT NOT NULL DEFAULT 0 AFTER `config_json`');
		}
		$colSched = $pdo->query("SHOW COLUMNS FROM `epc_auto_price_tenant_config` LIKE 'last_scheduled_crawl_at'")->fetch(PDO::FETCH_ASSOC);
		if (!$colSched) {
			$pdo->exec('ALTER TABLE `epc_auto_price_tenant_config` ADD COLUMN `last_scheduled_crawl_at` INT NOT NULL DEFAULT 0 AFTER `last_crawl_at`');
		}
	} catch (Throwable $e) {
	}

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_price_sources` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`site_key` VARCHAR(64) NOT NULL DEFAULT \'\',
			`source_type` VARCHAR(32) NOT NULL DEFAULT \'manual\',
			`name` VARCHAR(120) NOT NULL,
			`external_ref` VARCHAR(255) NOT NULL DEFAULT \'\',
			`warehouse_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`price_list_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`config_json` TEXT NULL,
			`active` TINYINT(1) NOT NULL DEFAULT 1,
			`sort_order` INT NOT NULL DEFAULT 100,
			`last_checked_at` INT NOT NULL DEFAULT 0,
			`created_at` INT NOT NULL DEFAULT 0,
			`updated_at` INT NOT NULL DEFAULT 0,
			KEY `site_type` (`site_key`, `source_type`),
			KEY `active_sort` (`active`, `sort_order`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_price_source_products` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`source_id` INT UNSIGNED NOT NULL,
			`product_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`external_sku` VARCHAR(128) NOT NULL DEFAULT \'\',
			`external_url` VARCHAR(512) NOT NULL DEFAULT \'\',
			`title` VARCHAR(255) NOT NULL DEFAULT \'\',
			`last_price` DECIMAL(12,4) NOT NULL DEFAULT 0,
			`last_currency` VARCHAR(8) NOT NULL DEFAULT \'AED\',
			`warehouse_cost` DECIMAL(12,4) NOT NULL DEFAULT 0,
			`last_stock_hint` INT NOT NULL DEFAULT 0,
			`last_checked_at` INT NOT NULL DEFAULT 0,
			`fetch_status` VARCHAR(32) NOT NULL DEFAULT \'pending\',
			`fetch_message` VARCHAR(255) NOT NULL DEFAULT \'\',
			`meta_json` TEXT NULL,
			`created_at` INT NOT NULL DEFAULT 0,
			`updated_at` INT NOT NULL DEFAULT 0,
			UNIQUE KEY `source_ext` (`source_id`, `external_sku`),
			KEY `product_id` (`product_id`),
			KEY `last_checked` (`last_checked_at`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_price_compare_runs` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`site_key` VARCHAR(64) NOT NULL DEFAULT \'\',
			`trigger_type` VARCHAR(24) NOT NULL DEFAULT \'manual\',
			`sources_checked` INT NOT NULL DEFAULT 0,
			`products_checked` INT NOT NULL DEFAULT 0,
			`prices_updated` INT NOT NULL DEFAULT 0,
			`errors` INT NOT NULL DEFAULT 0,
			`status` VARCHAR(24) NOT NULL DEFAULT \'done\',
			`summary` TEXT NULL,
			`started_at` INT NOT NULL DEFAULT 0,
			`finished_at` INT NOT NULL DEFAULT 0
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_channel_listings` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`site_key` VARCHAR(64) NOT NULL DEFAULT \'\',
			`product_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`source_product_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`channel_type` VARCHAR(32) NOT NULL DEFAULT \'storefront\',
			`external_listing_id` VARCHAR(128) NOT NULL DEFAULT \'\',
			`list_price` DECIMAL(12,4) NOT NULL DEFAULT 0,
			`currency` VARCHAR(8) NOT NULL DEFAULT \'AED\',
			`status` VARCHAR(24) NOT NULL DEFAULT \'draft\',
			`listing_json` TEXT NULL,
			`created_at` INT NOT NULL DEFAULT 0,
			`updated_at` INT NOT NULL DEFAULT 0,
			KEY `site_channel` (`site_key`, `channel_type`),
			KEY `product_id` (`product_id`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_auto_price_rules` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`site_key` VARCHAR(64) NOT NULL DEFAULT \'\',
			`rule_key` VARCHAR(64) NOT NULL DEFAULT \'default\',
			`min_margin_percent` DECIMAL(8,2) NOT NULL DEFAULT 15,
			`auto_update_prices` TINYINT(1) NOT NULL DEFAULT 0,
			`auto_cross_list` TINYINT(1) NOT NULL DEFAULT 0,
			`cross_list_channels` VARCHAR(255) NOT NULL DEFAULT \'storefront,ebay\',
			`schedule_hours` INT NOT NULL DEFAULT 24,
			`notes` TEXT NULL,
			`active` TINYINT(1) NOT NULL DEFAULT 1,
			`updated_at` INT NOT NULL DEFAULT 0,
			UNIQUE KEY `site_rule` (`site_key`, `rule_key`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);

	if (is_file(__DIR__ . '/epc_auto_price_categories.php')) {
		require_once __DIR__ . '/epc_auto_price_categories.php';
		epc_apai_category_map_schema($pdo);
	}
	if (is_file(__DIR__ . '/epc_auto_price_storefront.php')) {
		require_once __DIR__ . '/epc_auto_price_storefront.php';
		epc_apai_source_prices_schema($pdo);
	}

	epc_disc_ensure_schema($pdo);
	$done[$key] = true;
}

function epc_disc_ensure_schema(PDO $pdo): void
{
	static $done = array();
	$key = spl_object_hash($pdo);
	if (isset($done[$key])) {
		return;
	}
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_discovery_sources` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`site_key` VARCHAR(64) NOT NULL DEFAULT \'\',
			`source_type` VARCHAR(32) NOT NULL DEFAULT \'custom_website\',
			`domain` VARCHAR(255) NOT NULL DEFAULT \'\',
			`label` VARCHAR(120) NOT NULL DEFAULT \'\',
			`config_json` TEXT NULL,
			`enabled` TINYINT(1) NOT NULL DEFAULT 1,
			`priority` INT NOT NULL DEFAULT 100,
			`last_crawl` INT NOT NULL DEFAULT 0,
			`created_at` INT NOT NULL DEFAULT 0,
			`updated_at` INT NOT NULL DEFAULT 0,
			KEY `site_enabled` (`site_key`, `enabled`, `priority`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);

	epc_disc_source_schema_migrate($pdo);

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_product_discovery_queue` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`site_key` VARCHAR(64) NOT NULL DEFAULT \'\',
			`status` VARCHAR(24) NOT NULL DEFAULT \'suggested\',
			`title` VARCHAR(255) NOT NULL DEFAULT \'\',
			`description` TEXT NULL,
			`image_urls` TEXT NULL,
			`source_url` VARCHAR(512) NOT NULL DEFAULT \'\',
			`source_domain` VARCHAR(120) NOT NULL DEFAULT \'\',
			`suggested_price` DECIMAL(12,4) NOT NULL DEFAULT 0,
			`cost_estimate` DECIMAL(12,4) NOT NULL DEFAULT 0,
			`margin_pct` DECIMAL(8,2) NOT NULL DEFAULT 0,
			`sell_price` DECIMAL(12,4) NOT NULL DEFAULT 0,
			`currency` VARCHAR(8) NOT NULL DEFAULT \'AED\',
			`taxonomy_node_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`product_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`meta_json` TEXT NULL,
			`created_at` INT NOT NULL DEFAULT 0,
			`updated_at` INT NOT NULL DEFAULT 0,
			KEY `site_status` (`site_key`, `status`),
			KEY `taxonomy` (`taxonomy_node_id`),
			KEY `source_domain` (`source_domain`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);

	try {
		$col = $pdo->query("SHOW COLUMNS FROM `epc_product_discovery_queue` LIKE 'local_image_paths'")->fetch(PDO::FETCH_ASSOC);
		if (!$col) {
			$pdo->exec('ALTER TABLE `epc_product_discovery_queue` ADD COLUMN `local_image_paths` TEXT NULL AFTER `image_urls`');
		}
	} catch (Throwable $e) {
	}
	try {
		$col = $pdo->query("SHOW COLUMNS FROM `epc_product_discovery_queue` LIKE 'specs_json'")->fetch(PDO::FETCH_ASSOC);
		if (!$col) {
			$pdo->exec('ALTER TABLE `epc_product_discovery_queue` ADD COLUMN `specs_json` TEXT NULL AFTER `meta_json`');
		}
	} catch (Throwable $e) {
	}
	try {
		$col = $pdo->query("SHOW COLUMNS FROM `epc_product_discovery_queue` LIKE 'brand_article_key'")->fetch(PDO::FETCH_ASSOC);
		if (!$col) {
			$pdo->exec('ALTER TABLE `epc_product_discovery_queue` ADD COLUMN `brand_article_key` VARCHAR(128) NOT NULL DEFAULT \'\' AFTER `source_domain`');
			$pdo->exec('ALTER TABLE `epc_product_discovery_queue` ADD KEY `site_status_ba` (`site_key`, `status`, `brand_article_key`)');
		}
	} catch (Throwable $e) {
	}

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_apai_crawl_jobs` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`site_key` VARCHAR(64) NOT NULL DEFAULT \'\',
			`status` VARCHAR(16) NOT NULL DEFAULT \'pending\',
			`mode` VARCHAR(16) NOT NULL DEFAULT \'full\',
			`options_json` TEXT NULL,
			`result_json` TEXT NULL,
			`created_at` INT NOT NULL DEFAULT 0,
			`started_at` INT NOT NULL DEFAULT 0,
			`finished_at` INT NOT NULL DEFAULT 0,
			KEY `site_status` (`site_key`, `status`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);

	if (is_file(__DIR__ . '/epc_auto_price_images.php')) {
		require_once __DIR__ . '/epc_auto_price_images.php';
	}
	if (is_file(__DIR__ . '/epc_electronics_taxonomy.php')) {
		require_once __DIR__ . '/epc_electronics_taxonomy.php';
		epc_tax_ensure_schema($pdo);
	}
	if (is_file(__DIR__ . '/epc_industry_taxonomy.php')) {
		require_once __DIR__ . '/epc_industry_taxonomy.php';
		epc_apai_taxonomy_migrate_schema($pdo);
	}

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_warehouse_market_match` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`site_key` VARCHAR(64) NOT NULL DEFAULT \'\',
			`brand_article_key` VARCHAR(128) NOT NULL DEFAULT \'\',
			`brand` VARCHAR(64) NOT NULL DEFAULT \'\',
			`article` VARCHAR(64) NOT NULL DEFAULT \'\',
			`article_show` VARCHAR(64) NOT NULL DEFAULT \'\',
			`title` VARCHAR(255) NOT NULL DEFAULT \'\',
			`warehouse_cost` DECIMAL(12,4) NOT NULL DEFAULT 0,
			`market_min` DECIMAL(12,4) NOT NULL DEFAULT 0,
			`market_max` DECIMAL(12,4) NOT NULL DEFAULT 0,
			`margin_pct` DECIMAL(8,2) NOT NULL DEFAULT 0,
			`margin_abs` DECIMAL(12,4) NOT NULL DEFAULT 0,
			`source_match_count` INT NOT NULL DEFAULT 0,
			`matched_sources_json` TEXT NULL,
			`badge` VARCHAR(32) NOT NULL DEFAULT \'\',
			`product_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`price_list_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`meta_json` TEXT NULL,
			`updated_at` INT NOT NULL DEFAULT 0,
			UNIQUE KEY `site_ba` (`site_key`, `brand_article_key`),
			KEY `site_badge` (`site_key`, `badge`),
			KEY `site_match` (`site_key`, `source_match_count`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
	$done[$key] = true;
}

function epc_ape_tenant_config_get(PDO $pdo, string $siteKey): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	static $cache = array();
	if (isset($cache[$siteKey])) {
		return $cache[$siteKey];
	}
	$stmt = $pdo->prepare('SELECT * FROM `epc_auto_price_tenant_config` WHERE `site_key` = ? LIMIT 1');
	$stmt->execute(array($siteKey));
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	if ($row) {
		$row['config'] = json_decode((string) ($row['config_json'] ?? ''), true);
		if (!is_array($row['config'])) {
			$row['config'] = array();
		}
		$cache[$siteKey] = $row;
		return $row;
	}
	$cache[$siteKey] = array(
		'site_key' => $siteKey,
		'profile' => 'warehouse_supplier',
		'currency' => 'AED',
		'active' => 1,
		'config' => array(),
	);
	return $cache[$siteKey];
}

function epc_ape_tenant_config_save(PDO $pdo, string $siteKey, string $profile, string $currency = 'AED'): void
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$profiles = epc_ape_profiles();
	if (!isset($profiles[$profile])) {
		$profile = 'warehouse_supplier';
	}
	$now = time();
	$pdo->prepare(
		'INSERT INTO `epc_auto_price_tenant_config` (`site_key`, `profile`, `currency`, `active`, `updated_at`)
		 VALUES (?, ?, ?, 1, ?)
		 ON DUPLICATE KEY UPDATE `profile` = VALUES(`profile`), `currency` = VALUES(`currency`), `updated_at` = VALUES(`updated_at`)'
	)->execute(array($siteKey, $profile, strtoupper(substr($currency, 0, 8)), $now));
}

function epc_ape_rules_get(PDO $pdo, string $siteKey): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$stmt = $pdo->prepare('SELECT * FROM `epc_auto_price_rules` WHERE `site_key` = ? AND `rule_key` = ? LIMIT 1');
	$stmt->execute(array($siteKey, 'default'));
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	if ($row) {
		return $row;
	}
	return array(
		'site_key' => $siteKey,
		'rule_key' => 'default',
		'min_margin_percent' => 15,
		'auto_update_prices' => 0,
		'auto_cross_list' => 0,
		'cross_list_channels' => 'storefront,ebay',
		'schedule_hours' => 24,
		'active' => 1,
	);
}

function epc_ape_rules_save(PDO $pdo, string $siteKey, array $data): void
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$now = time();
	$pdo->prepare(
		'INSERT INTO `epc_auto_price_rules` (`site_key`, `rule_key`, `min_margin_percent`, `auto_update_prices`, `auto_cross_list`, `cross_list_channels`, `schedule_hours`, `notes`, `active`, `updated_at`)
		 VALUES (?, \'default\', ?, ?, ?, ?, ?, ?, 1, ?)
		 ON DUPLICATE KEY UPDATE
		   `min_margin_percent` = VALUES(`min_margin_percent`),
		   `auto_update_prices` = VALUES(`auto_update_prices`),
		   `auto_cross_list` = VALUES(`auto_cross_list`),
		   `cross_list_channels` = VALUES(`cross_list_channels`),
		   `schedule_hours` = VALUES(`schedule_hours`),
		   `notes` = VALUES(`notes`),
		   `updated_at` = VALUES(`updated_at`)'
	)->execute(array(
		$siteKey,
		(float) ($data['min_margin_percent'] ?? 15),
		!empty($data['auto_update_prices']) ? 1 : 0,
		!empty($data['auto_cross_list']) ? 1 : 0,
		trim((string) ($data['cross_list_channels'] ?? 'storefront,ebay')),
		max(1, (int) ($data['schedule_hours'] ?? 24)),
		trim((string) ($data['notes'] ?? '')),
		$now,
	));
	if (isset($data['pricing_strategy'])) {
		epc_apai_save_pricing_strategy(
			$pdo,
			$siteKey,
			(string) $data['pricing_strategy'],
			(float) ($data['pricing_markup_pct'] ?? 0)
		);
	}
}

function epc_ape_sources_list(PDO $pdo, string $siteKey): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$stmt = $pdo->prepare('SELECT * FROM `epc_price_sources` WHERE `site_key` = ? ORDER BY `sort_order`, `name`');
	$stmt->execute(array($siteKey));
	return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_ape_source_save(PDO $pdo, string $siteKey, array $data, int $id = 0): int
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$types = epc_ape_source_types();
	$type = (string) ($data['source_type'] ?? 'manual');
	if (!isset($types[$type])) {
		$type = 'manual';
	}
	$now = time();
	$name = trim((string) ($data['name'] ?? 'Source'));
	if ($name === '') {
		$name = ucfirst(str_replace('_', ' ', $type));
	}
	$params = array(
		$siteKey, $type, $name,
		trim((string) ($data['external_ref'] ?? '')),
		max(0, (int) ($data['warehouse_id'] ?? 0)),
		max(0, (int) ($data['price_list_id'] ?? 0)),
		!empty($data['active']) ? 1 : 0,
		(int) ($data['sort_order'] ?? 100),
		$now,
	);
	if ($id > 0) {
		$params[] = $id;
		$pdo->prepare(
			'UPDATE `epc_price_sources` SET `site_key`=?, `source_type`=?, `name`=?, `external_ref`=?, `warehouse_id`=?, `price_list_id`=?, `active`=?, `sort_order`=?, `updated_at`=? WHERE `id`=?'
		)->execute($params);
		return $id;
	}
	$params[] = $now;
	$pdo->prepare(
		'INSERT INTO `epc_price_sources` (`site_key`, `source_type`, `name`, `external_ref`, `warehouse_id`, `price_list_id`, `active`, `sort_order`, `updated_at`, `created_at`)
		 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
	)->execute($params);
	return (int) $pdo->lastInsertId();
}

function epc_ape_source_delete(PDO $pdo, int $id): void
{
	$pdo->prepare('DELETE FROM `epc_price_source_products` WHERE `source_id` = ?')->execute(array($id));
	$pdo->prepare('DELETE FROM `epc_price_sources` WHERE `id` = ?')->execute(array($id));
}

function epc_ape_source_products_list(PDO $pdo, string $siteKey, int $limit = 200): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$stmt = $pdo->prepare(
		'SELECT sp.*, ps.`name` AS `source_name`, ps.`source_type`, ps.`warehouse_id`
		 FROM `epc_price_source_products` sp
		 INNER JOIN `epc_price_sources` ps ON ps.`id` = sp.`source_id`
		 WHERE ps.`site_key` = ?
		 ORDER BY sp.`updated_at` DESC
		 LIMIT ' . max(1, min(500, $limit))
	);
	$stmt->execute(array($siteKey));
	return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_ape_compare_matrix(PDO $pdo, string $siteKey): array
{
	$rows = epc_ape_source_products_list($pdo, $siteKey, 500);
	$rules = epc_ape_rules_get($pdo, $siteKey);
	$minMargin = (float) ($rules['min_margin_percent'] ?? 15);
	$byProduct = array();

	foreach ($rows as $row) {
		$key = (int) ($row['product_id'] ?? 0);
		if ($key <= 0) {
			$key = 'url:' . md5((string) $row['external_url']);
		}
		if (!isset($byProduct[$key])) {
			$byProduct[$key] = array(
				'product_id' => (int) ($row['product_id'] ?? 0),
				'title' => (string) ($row['title'] ?? ''),
				'warehouse_cost' => 0.0,
				'sources' => array(),
				'lowest_price' => null,
				'lowest_source' => '',
				'margin_percent' => null,
			);
		}
		$price = (float) ($row['last_price'] ?? 0);
		$cost = (float) ($row['warehouse_cost'] ?? 0);
		if ($cost > 0 && $byProduct[$key]['warehouse_cost'] <= 0) {
			$byProduct[$key]['warehouse_cost'] = $cost;
		}
		if ($byProduct[$key]['title'] === '' && !empty($row['title'])) {
			$byProduct[$key]['title'] = (string) $row['title'];
		}
		$byProduct[$key]['sources'][] = array(
			'source_id' => (int) $row['source_id'],
			'source_name' => (string) ($row['source_name'] ?? ''),
			'source_type' => (string) ($row['source_type'] ?? ''),
			'price' => $price,
			'currency' => (string) ($row['last_currency'] ?? 'AED'),
			'url' => (string) ($row['external_url'] ?? ''),
			'last_checked_at' => (int) ($row['last_checked_at'] ?? 0),
			'fetch_status' => (string) ($row['fetch_status'] ?? ''),
			'specs' => epc_ape_source_product_specs($row),
		);
		if ($price > 0 && ($byProduct[$key]['lowest_price'] === null || $price < $byProduct[$key]['lowest_price'])) {
			$byProduct[$key]['lowest_price'] = $price;
			$byProduct[$key]['lowest_source'] = (string) ($row['source_name'] ?? '');
		}
	}

	foreach ($byProduct as &$p) {
		$cost = (float) ($p['warehouse_cost'] ?? 0);
		$low = $p['lowest_price'];
		if ($cost > 0 && $low !== null && $low > 0) {
			$p['margin_percent'] = round((($low - $cost) / $cost) * 100, 2);
		}
		$p['meets_margin'] = ($p['margin_percent'] !== null && $p['margin_percent'] >= $minMargin);
	}
	unset($p);

	return array_values($byProduct);
}

function epc_ape_source_product_specs(array $row): array
{
	$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
	if (is_array($meta) && !empty($meta['specs']) && is_array($meta['specs'])) {
		return $meta['specs'];
	}
	return array();
}

function epc_ape_kpi(PDO $pdo, string $siteKey): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$kpi = array('products' => 0, 'sources' => 0, 'channels' => 0, 'listings' => 0, 'last_run' => null);

	$stmt = $pdo->prepare('SELECT COUNT(*) FROM `epc_price_sources` WHERE `site_key` = ? AND `active` = 1');
	$stmt->execute(array($siteKey));
	$kpi['sources'] = (int) $stmt->fetchColumn();

	$stmt = $pdo->prepare(
		'SELECT COUNT(DISTINCT COALESCE(NULLIF(sp.`product_id`, 0), sp.`id`))
		 FROM `epc_price_source_products` sp
		 INNER JOIN `epc_price_sources` ps ON ps.`id` = sp.`source_id`
		 WHERE ps.`site_key` = ?'
	);
	$stmt->execute(array($siteKey));
	$kpi['products'] = (int) $stmt->fetchColumn();

	$stmt = $pdo->prepare('SELECT COUNT(DISTINCT `channel_type`) FROM `epc_channel_listings` WHERE `site_key` = ?');
	$stmt->execute(array($siteKey));
	$kpi['channels'] = (int) $stmt->fetchColumn();

	$stmt = $pdo->prepare('SELECT COUNT(*) FROM `epc_channel_listings` WHERE `site_key` = ?');
	$stmt->execute(array($siteKey));
	$kpi['listings'] = (int) $stmt->fetchColumn();

	$stmt = $pdo->prepare('SELECT * FROM `epc_price_compare_runs` WHERE `site_key` = ? ORDER BY `id` DESC LIMIT 1');
	$stmt->execute(array($siteKey));
	$kpi['last_run'] = $stmt->fetch(PDO::FETCH_ASSOC) ?: null;

	return $kpi;
}

function epc_ape_warehouse_matrix(PDO $pdo): array
{
	$matrix = array();
	try {
		$storages = $pdo->query(
			"SELECT s.`id`, s.`name`, s.`connection_options`
			 FROM `shop_storages` s
			 WHERE s.`hidden` = 0
			 ORDER BY s.`name`"
		)->fetchAll(PDO::FETCH_ASSOC);
	} catch (Exception $e) {
		return $matrix;
	}

	$priceMap = array();
	foreach ($storages as $row) {
		$co = json_decode((string) ($row['connection_options'] ?? ''), true);
		$pid = isset($co['price_id']) ? (int) $co['price_id'] : 0;
		if ($pid > 0) {
			$priceMap[$pid] = array('storage_id' => (int) $row['id'], 'name' => (string) $row['name']);
		}
	}
	if (!$priceMap) {
		return $matrix;
	}

	$ids = implode(',', array_map('intval', array_keys($priceMap)));
	try {
		$counts = $pdo->query(
			"SELECT `price_id`, COUNT(*) AS `cnt`, MIN(`price`) AS `min_price`, MAX(`price`) AS `max_price`
			 FROM `shop_docpart_prices_data`
			 WHERE `price_id` IN ({$ids}) AND IFNULL(`price`, 0) > 0
			 GROUP BY `price_id`"
		)->fetchAll(PDO::FETCH_ASSOC);
	} catch (Exception $e) {
		return $matrix;
	}

	foreach ($counts as $c) {
		$pid = (int) ($c['price_id'] ?? 0);
		if (!isset($priceMap[$pid])) {
			continue;
		}
		$matrix[] = array(
			'storage_id' => $priceMap[$pid]['storage_id'],
			'storage_name' => $priceMap[$pid]['name'],
			'price_list_id' => $pid,
			'product_count' => (int) ($c['cnt'] ?? 0),
			'min_price' => (float) ($c['min_price'] ?? 0),
			'max_price' => (float) ($c['max_price'] ?? 0),
		);
	}
	return $matrix;
}

function epc_ape_source_product_save(PDO $pdo, int $sourceId, array $data, int $id = 0): int
{
	$now = time();
	$params = array(
		$sourceId,
		max(0, (int) ($data['product_id'] ?? 0)),
		trim((string) ($data['external_sku'] ?? '')),
		trim((string) ($data['external_url'] ?? '')),
		trim((string) ($data['title'] ?? '')),
		(float) ($data['last_price'] ?? 0),
		strtoupper(substr((string) ($data['last_currency'] ?? 'AED'), 0, 8)),
		(float) ($data['warehouse_cost'] ?? 0),
		(int) ($data['last_stock_hint'] ?? 0),
		$now,
	);
	if ($id > 0) {
		$params[] = $id;
		$pdo->prepare(
			'UPDATE `epc_price_source_products` SET `source_id`=?, `product_id`=?, `external_sku`=?, `external_url`=?, `title`=?, `last_price`=?, `last_currency`=?, `warehouse_cost`=?, `last_stock_hint`=?, `updated_at`=? WHERE `id`=?'
		)->execute($params);
		return $id;
	}
	$params[] = $now;
	$pdo->prepare(
		'INSERT INTO `epc_price_source_products` (`source_id`, `product_id`, `external_sku`, `external_url`, `title`, `last_price`, `last_currency`, `warehouse_cost`, `last_stock_hint`, `updated_at`, `created_at`)
		 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
		 ON DUPLICATE KEY UPDATE `product_id`=VALUES(`product_id`), `external_url`=VALUES(`external_url`), `title`=VALUES(`title`), `last_price`=VALUES(`last_price`), `last_currency`=VALUES(`last_currency`), `warehouse_cost`=VALUES(`warehouse_cost`), `last_stock_hint`=VALUES(`last_stock_hint`), `updated_at`=VALUES(`updated_at`)'
	)->execute($params);
	$newId = (int) $pdo->lastInsertId();
	if ($newId > 0) {
		return $newId;
	}
	$lookup = $pdo->prepare('SELECT `id` FROM `epc_price_source_products` WHERE `source_id` = ? AND `external_sku` = ? LIMIT 1');
	$lookup->execute(array($sourceId, trim((string) ($data['external_sku'] ?? ''))));
	return (int) $lookup->fetchColumn();
}

function epc_ape_update_source_product_price(PDO $pdo, int $id, float $price, string $currency, string $status, string $message = ''): void
{
	$pdo->prepare(
		'UPDATE `epc_price_source_products` SET `last_price`=?, `last_currency`=?, `fetch_status`=?, `fetch_message`=?, `last_checked_at`=?, `updated_at`=? WHERE `id`=?'
	)->execute(array($price, $currency, $status, substr($message, 0, 255), time(), time(), $id));
}

function epc_ape_run_compare(PDO $pdo, string $siteKey, string $trigger = 'manual'): array
{
	require_once __DIR__ . '/epc_auto_price_adapters.php';

	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$started = time();
	$sourcesChecked = 0;
	$productsChecked = 0;
	$pricesUpdated = 0;
	$errors = 0;
	$messages = array();

	$sources = epc_ape_sources_list($pdo, $siteKey);
	foreach ($sources as $src) {
		if (empty($src['active'])) {
			continue;
		}
		$sourcesChecked++;
		$srcId = (int) $src['id'];
		$type = (string) ($src['source_type'] ?? 'manual');

		$stmt = $pdo->prepare('SELECT * FROM `epc_price_source_products` WHERE `source_id` = ?');
		$stmt->execute(array($srcId));
		$products = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();

		foreach ($products as $prod) {
			$productsChecked++;
			$url = trim((string) ($prod['external_url'] ?? ''));
			$manualPrice = (float) ($prod['last_price'] ?? 0);

			if ($type === 'warehouse' || $type === 'supplier') {
				continue;
			}

			$result = epc_ape_adapter_fetch($type, $url, $manualPrice);
			if (!empty($result['ok'])) {
				epc_ape_update_source_product_price(
					$pdo,
					(int) $prod['id'],
					(float) ($result['price'] ?? 0),
					(string) ($result['currency'] ?? 'AED'),
					'ok',
					(string) ($result['message'] ?? '')
				);
				if ((float) ($result['price'] ?? 0) > 0) {
					$pricesUpdated++;
				}
			} else {
				$errors++;
				epc_ape_update_source_product_price(
					$pdo,
					(int) $prod['id'],
					$manualPrice,
					(string) ($prod['last_currency'] ?? 'AED'),
					'error',
					(string) ($result['message'] ?? 'fetch failed')
				);
			}
		}

		$pdo->prepare('UPDATE `epc_price_sources` SET `last_checked_at` = ? WHERE `id` = ?')->execute(array(time(), $srcId));
	}

	$summary = "sources={$sourcesChecked} products={$productsChecked} updated={$pricesUpdated} errors={$errors}";
	$pdo->prepare(
		'INSERT INTO `epc_price_compare_runs` (`site_key`, `trigger_type`, `sources_checked`, `products_checked`, `prices_updated`, `errors`, `status`, `summary`, `started_at`, `finished_at`)
		 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
	)->execute(array($siteKey, $trigger, $sourcesChecked, $productsChecked, $pricesUpdated, $errors, $errors ? 'partial' : 'done', $summary, $started, time()));

	return array(
		'ok' => true,
		'sources_checked' => $sourcesChecked,
		'products_checked' => $productsChecked,
		'prices_updated' => $pricesUpdated,
		'errors' => $errors,
		'summary' => $summary,
	);
}

function epc_ape_extract_url_meta(string $url, array $sourceConfig = array(), string $prefetchedHtml = ''): array
{
	$url = trim($url);
	if ($url === '' || !preg_match('#^https?://#i', $url)) {
		return array('ok' => false, 'message' => 'Invalid URL');
	}

	$html = $prefetchedHtml;
	if ($html === '' && function_exists('epc_disc_http_fetch')) {
		$html = epc_disc_http_fetch($url, $sourceConfig);
	} elseif (function_exists('curl_init')) {
		$ch = curl_init($url);
		curl_setopt_array($ch, array(
			CURLOPT_RETURNTRANSFER => true,
			CURLOPT_FOLLOWLOCATION => true,
			CURLOPT_MAXREDIRS => 5,
			CURLOPT_TIMEOUT => 20,
			CURLOPT_USERAGENT => 'EPC-AutoPrice/1.0 (+https://www.ecomae.com)',
			CURLOPT_SSL_VERIFYPEER => true,
		));
		$html = (string) curl_exec($ch);
		curl_close($ch);
	}
	if ($html === '') {
		$ctx = stream_context_create(array('http' => array('timeout' => 15, 'user_agent' => 'EPC-AutoPrice/1.0')));
		$html = @file_get_contents($url, false, $ctx) ?: '';
	}
	if ($html === '' || strpos($html, 'epc_disc_use_cached_prices') !== false) {
		return array('ok' => false, 'message' => 'Could not fetch page (blocked or timeout)', 'use_cached' => true);
	}

	$meta = array('title' => '', 'description' => '', 'price' => 0.0, 'currency' => 'AED', 'images' => array());
	if (preg_match('/<meta[^>]+property=["\']og:title["\'][^>]+content=["\']([^"\']+)["\']/i', $html, $m)) {
		$meta['title'] = html_entity_decode($m[1], ENT_QUOTES, 'UTF-8');
	} elseif (preg_match('/<title[^>]*>([^<]+)<\/title>/i', $html, $m)) {
		$meta['title'] = html_entity_decode(trim($m[1]), ENT_QUOTES, 'UTF-8');
	}
	if (preg_match('/<meta[^>]+property=["\']og:description["\'][^>]+content=["\']([^"\']+)["\']/i', $html, $m)) {
		$meta['description'] = html_entity_decode($m[1], ENT_QUOTES, 'UTF-8');
	}
	foreach (array('og:image', 'og:image:url') as $prop) {
		if (preg_match('/<meta[^>]+property=["\']' . preg_quote($prop, '/') . '["\'][^>]+content=["\']([^"\']+)["\']/i', $html, $m)) {
			$meta['images'][] = $m[1];
		}
	}
	if (preg_match('/<meta[^>]+property=["\']og:price:amount["\'][^>]+content=["\']([^"\']+)["\']/i', $html, $m)) {
		$meta['price'] = (float) str_replace(',', '', $m[1]);
	}
	if (preg_match('/<meta[^>]+property=["\']og:price:currency["\'][^>]+content=["\']([^"\']+)["\']/i', $html, $m)) {
		$meta['currency'] = strtoupper($m[1]);
	}
	if ($meta['price'] <= 0 && preg_match('/"price"\s*:\s*([0-9.]+)/', $html, $m)) {
		$meta['price'] = (float) $m[1];
	}
	if (function_exists('epc_disc_extract_specs_from_html')) {
		$meta['specs'] = epc_disc_extract_specs_from_html($html, (string) ($meta['description'] ?? ''));
	} else {
		$meta['specs'] = array();
	}

	return array('ok' => true, 'meta' => $meta, 'message' => 'Extracted from page meta');
}

/**
 * Catalogue storage visible on storefront (interface_type=1, office-linked when possible).
 */
function epc_ape_ensure_catalogue_storage(PDO $pdo): int
{
	try {
		$existing = (int) $pdo->query(
			'SELECT `id` FROM `shop_storages` WHERE `interface_type` = 1 ORDER BY `id` ASC LIMIT 1'
		)->fetchColumn();
		if ($existing > 0) {
			epc_ape_ensure_storefront_storage_mapped($pdo, $existing);
			return $existing;
		}
		$currency = 1;
		try {
			$cur = $pdo->query('SELECT `id` FROM `shop_currencies` ORDER BY `id` ASC LIMIT 1')->fetchColumn();
			if ((int) $cur > 0) {
				$currency = (int) $cur;
			}
		} catch (Throwable $e) {
		}
		try {
			$fromStorage = $pdo->query('SELECT `currency` FROM `shop_storages` ORDER BY `id` ASC LIMIT 1')->fetchColumn();
			if ((int) $fromStorage > 0) {
				$currency = (int) $fromStorage;
			}
		} catch (Throwable $e) {
		}
		$users = '[]';
		try {
			$admin = $pdo->query(
				'SELECT `id` FROM `users` WHERE `user_type` = 2 ORDER BY `id` ASC LIMIT 1'
			)->fetchColumn();
			if ((int) $admin > 0) {
				$users = json_encode(array((string) (int) $admin));
			}
		} catch (Throwable $e) {
		}
		$pdo->prepare(
			'INSERT INTO `shop_storages`
			 (`name`, `interface_type`, `users`, `connection_options`, `currency`, `short_name`, `hidden`, `bg_line_color`)
			 VALUES (?, 1, ?, ?, ?, ?, 0, 0)'
		)->execute(array(
			'APAI Catalogue',
			$users,
			'[]',
			$currency,
			'APAI',
		));
		$newId = (int) $pdo->lastInsertId();
		if ($newId > 0) {
			epc_ape_ensure_storefront_storage_mapped($pdo, $newId);
		}
		return $newId;
	} catch (Throwable $e) {
		if (function_exists('epc_ape_set_last_storage_error')) {
			epc_ape_set_last_storage_error($e->getMessage());
		}
		return 0;
	}
}

function epc_ape_set_last_storage_error(string $message): void
{
	$ref = &epc_ape_storage_error_ref();
	$ref['msg'] = $message;
}

function epc_ape_get_last_storage_error(): string
{
	$ref = &epc_ape_storage_error_ref();
	return (string) ($ref['msg'] ?? '');
}

function &epc_ape_storage_error_ref(): array
{
	static $state = array('msg' => '');
	return $state;
}

function epc_ape_resolve_storefront_storage_id(PDO $pdo): int
{
	static $cache = null;
	if ($cache !== null && $cache > 0) {
		return (int) $cache;
	}
	$cache = 0;
	try {
		$cache = (int) $pdo->query(
			'SELECT s.`id`
			 FROM `shop_storages` s
			 INNER JOIN `shop_offices_storages_map` m ON m.`storage_id` = s.`id`
			 WHERE s.`interface_type` = 1
			 ORDER BY s.`id` ASC
			 LIMIT 1'
		)->fetchColumn();
		if ($cache <= 0) {
			$cache = epc_ape_ensure_catalogue_storage($pdo);
		}
		if ($cache <= 0) {
			$cache = (int) $pdo->query(
				'SELECT `id` FROM `shop_storages` WHERE `interface_type` = 1 ORDER BY `id` ASC LIMIT 1'
			)->fetchColumn();
		}
		if ($cache > 0) {
			epc_ape_ensure_storefront_storage_mapped($pdo, (int) $cache);
		}
	} catch (Throwable $e) {
		$cache = 0;
	}
	return (int) $cache;
}

/**
 * Ensure catalogue storage is mapped to all offices with guest-group markup row.
 */
function epc_ape_ensure_storefront_storage_mapped(PDO $pdo, int $storageId): void
{
	if ($storageId <= 0) {
		return;
	}
	try {
		$officeIds = array();
		$offStmt = $pdo->query('SELECT `id` FROM `shop_offices` ORDER BY `id` ASC');
		while ($row = $offStmt->fetch(PDO::FETCH_ASSOC)) {
			$oid = (int) ($row['id'] ?? 0);
			if ($oid > 0) {
				$officeIds[] = $oid;
			}
		}
		if (!$officeIds) {
			return;
		}
		$groupId = (int) $pdo->query('SELECT `id` FROM `groups` WHERE `for_guests` = 1 ORDER BY `id` ASC LIMIT 1')->fetchColumn();
		if ($groupId <= 0) {
			$groupId = (int) $pdo->query('SELECT `id` FROM `groups` ORDER BY `id` ASC LIMIT 1')->fetchColumn();
		}
		if ($groupId <= 0) {
			return;
		}
		$ins = $pdo->prepare(
			'INSERT INTO `shop_offices_storages_map`
			 (`office_id`, `storage_id`, `group_id`, `min_point`, `max_point`, `markup`, `additional_time`)
			 SELECT ?, ?, ?, 0, 999999999, 0, 0
			 FROM DUAL
			 WHERE NOT EXISTS (
			 	SELECT 1 FROM `shop_offices_storages_map`
			 	WHERE `office_id` = ? AND `storage_id` = ? AND `group_id` = ?
			 )'
		);
		foreach ($officeIds as $officeId) {
			$ins->execute(array($officeId, $storageId, $groupId, $officeId, $storageId, $groupId));
		}
	} catch (Throwable $e) {
	}
}

function epc_ape_set_catalogue_storage_price(PDO $pdo, int $productId, float $price, float $cost = 0): void
{
	if ($productId <= 0 || $price <= 0) {
		return;
	}
	try {
		$storageId = epc_ape_resolve_storefront_storage_id($pdo);
		if ($storageId <= 0) {
			return;
		}
		epc_ape_ensure_storefront_storage_mapped($pdo, $storageId);
		$chk = $pdo->prepare('SELECT `id` FROM `shop_storages_data` WHERE `product_id` = ? AND `storage_id` = ? LIMIT 1');
		$chk->execute(array($productId, $storageId));
		$rowId = (int) $chk->fetchColumn();
		if ($rowId > 0) {
			if ($cost > 0) {
				$pdo->prepare('UPDATE `shop_storages_data` SET `price` = ?, `price_purchase` = ?, `exist` = GREATEST(`exist`, 1) WHERE `id` = ?')
					->execute(array($price, $cost, $rowId));
			} else {
				$pdo->prepare('UPDATE `shop_storages_data` SET `price` = ?, `exist` = GREATEST(`exist`, 1) WHERE `id` = ?')->execute(array($price, $rowId));
			}
			return;
		}
		if ($cost > 0) {
			$pdo->prepare(
				'INSERT INTO `shop_storages_data` (`product_id`, `storage_id`, `price`, `price_purchase`, `exist`, `reserved`, `issued`, `time_to_exe`, `arrival_time`)
				 VALUES (?, ?, ?, ?, 1, 0, 0, 0, 0)'
			)->execute(array($productId, $storageId, $price, $cost));
			return;
		}
		$pdo->prepare(
			'INSERT INTO `shop_storages_data` (`product_id`, `storage_id`, `price`, `exist`, `reserved`, `issued`, `time_to_exe`, `arrival_time`)
			 VALUES (?, ?, ?, 1, 0, 0, 0, 0)'
		)->execute(array($productId, $storageId, $price));
	} catch (Throwable $e) {
	}
}

function epc_ape_create_catalogue_product(PDO $pdo, string $title, float $price, string $description = '', int $categoryId = 0, array $options = array()): int
{
	$title = trim($title);
	if ($title === '') {
		return 0;
	}
	$alias = trim((string) ($options['alias'] ?? ''));
	if ($alias === '') {
		$alias = preg_replace('/[^a-z0-9\-]+/', '-', strtolower($title));
		$alias = trim($alias, '-');
	}
	if ($alias === '') {
		$alias = 'product-' . time();
	}
	$now = time();

	$catId = max(0, $categoryId);
	if ($catId <= 0) {
		try {
			$catId = (int) $pdo->query(
				'SELECT `id` FROM `shop_catalogue_categories` WHERE `published_flag` = 1 AND `count` = 0 AND `alias` LIKE \'apai-%\' ORDER BY `level` DESC, `order` LIMIT 1'
			)->fetchColumn();
		} catch (Exception $e) {
		}
		if ($catId <= 0) {
			try {
				$catId = (int) $pdo->query(
					'SELECT `id` FROM `shop_catalogue_categories` WHERE `published_flag` = 1 AND `count` = 0 ORDER BY `level` DESC, `order` LIMIT 1'
				)->fetchColumn();
			} catch (Exception $e) {
			}
		}
	}

	$exists = $pdo->prepare('SELECT `id` FROM `shop_catalogue_products` WHERE `alias` = ? LIMIT 1');
	$exists->execute(array($alias));
	$existingId = (int) $exists->fetchColumn();
	if ($existingId > 0) {
		$alias .= '-' . substr(md5((string) $now), 0, 6);
	}

	try {
		$pdo->prepare(
			'INSERT INTO `shop_catalogue_products` (`caption`, `alias`, `published_flag`, `category_id`, `time_created`, `time_edited`)
			 VALUES (?, ?, 1, ?, ?, ?)'
		)->execute(array($title, $alias, $catId, $now, $now));
	} catch (Throwable $e) {
		try {
			$pdo->prepare(
				'INSERT INTO `shop_catalogue_products` (`category_id`, `caption`, `alias`, `title_tag`, `description_tag`, `keywords_tag`, `published_flag`)
				 VALUES (?, ?, ?, ?, ?, ?, 1)'
			)->execute(array($catId, $title, $alias, $title, $description !== '' ? $description : $title, $title));
		} catch (Throwable $e2) {
			try {
				$pdo->prepare(
					'INSERT INTO `shop_catalogue_products` (`caption`, `alias`, `published_flag`, `price`, `category_id`, `time_created`, `time_edited`)
					 VALUES (?, ?, 1, ?, ?, ?, ?)'
				)->execute(array($title, $alias, $price, $catId, $now, $now));
			} catch (Throwable $e3) {
				return 0;
			}
		}
	}
	$productId = (int) $pdo->lastInsertId();
	if ($productId > 0 && $price > 0) {
		epc_ape_set_catalogue_storage_price($pdo, $productId, $price);
	}
	return $productId;
}

function epc_ape_create_listing(PDO $pdo, string $siteKey, int $productId, string $channelType, float $price, array $extra = array()): int
{
	$channels = epc_ape_channel_types();
	if (!isset($channels[$channelType])) {
		$channelType = 'storefront';
	}
	$now = time();
	$status = ($channelType === 'ebay') ? 'draft' : 'active';
	$pdo->prepare(
		'INSERT INTO `epc_channel_listings` (`site_key`, `product_id`, `source_product_id`, `channel_type`, `list_price`, `currency`, `status`, `listing_json`, `created_at`, `updated_at`)
		 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
	)->execute(array(
		$siteKey,
		$productId,
		(int) ($extra['source_product_id'] ?? 0),
		$channelType,
		$price,
		(string) ($extra['currency'] ?? 'AED'),
		$status,
		json_encode($extra, JSON_UNESCAPED_UNICODE),
		$now,
		$now,
	));
	return (int) $pdo->lastInsertId();
}

function epc_ape_export_ebay_csv(PDO $pdo, string $siteKey): string
{
	$stmt = $pdo->prepare(
		'SELECT cl.*, scp.`caption` AS `product_title`
		 FROM `epc_channel_listings` cl
		 LEFT JOIN `shop_catalogue_products` scp ON scp.`id` = cl.`product_id`
		 WHERE cl.`site_key` = ? AND cl.`channel_type` = \'ebay\'
		 ORDER BY cl.`id` DESC'
	);
	$stmt->execute(array($siteKey));
	$rows = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();

	$lines = array('Action,Title,StartPrice,Currency,Category,ConditionID,Description');
	foreach ($rows as $r) {
		$title = str_replace('"', '""', (string) ($r['product_title'] ?? 'Item'));
		$lines[] = sprintf(
			'Add,"%s",%.2f,%s,11700,1000,"EPC auto listing draft #%d"',
			$title,
			(float) ($r['list_price'] ?? 0),
			(string) ($r['currency'] ?? 'AED'),
			(int) $r['id']
		);
	}
	return implode("\n", $lines) . "\n";
}

function epc_ape_tenant_pdo(PDO $platformPdo, string $siteKey): ?PDO
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if ($siteKey === '' || $siteKey === 'platform') {
		return null;
	}
	if (!function_exists('epc_portal_list_tenants')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
	}
	global $DP_Config;
	$cfg = $DP_Config instanceof DP_Config ? $DP_Config : new DP_Config();
	foreach (epc_portal_list_tenants($platformPdo) as $row) {
		if ((string) ($row['site_key'] ?? '') !== $siteKey) {
			continue;
		}
		if (!function_exists('epc_portal_tenant_setup_credentials')) {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
		}
		$cred = epc_portal_tenant_setup_credentials($row);
		$db = trim((string) ($cred['db'] ?? ''));
		if ($db === '') {
			return null;
		}
		$user = trim((string) ($cred['user'] ?? ''));
		if ($user === '') {
			$user = (string) $cfg->user;
		}
		$pass = (string) ($cred['pass'] ?? '');
		if ($pass === '') {
			$pass = (string) $cfg->password;
		}
		try {
			return new PDO(
				'mysql:host=' . $cfg->host . ';dbname=' . $db . ';charset=utf8',
				$user,
				$pass,
				array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
			);
		} catch (Throwable $e) {
			return null;
		}
	}
	return null;
}

function epc_ape_catalogue_product_url(PDO $pdo, int $productId): string
{
	if ($productId <= 0) {
		return '';
	}
	global $DP_Config;
	$domain = rtrim((string) ($DP_Config->domain_path ?? '/'), '/');
	$stmt = $pdo->prepare(
		'SELECT scp.`id`, scp.`alias`, scp.`category_id`, scc.`url` AS `category_url`
		 FROM `shop_catalogue_products` scp
		 LEFT JOIN `shop_catalogue_categories` scc ON scc.`id` = scp.`category_id`
		 WHERE scp.`id` = ? LIMIT 1'
	);
	$stmt->execute(array($productId));
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return '';
	}
	$catUrl = trim((string) ($row['category_url'] ?? ''), '/');
	$productUrlMode = (string) ($DP_Config->product_url ?? 'alias');
	if (is_file(__DIR__ . '/epc_auto_price_categories.php')) {
		require_once __DIR__ . '/epc_auto_price_categories.php';
		$path = epc_apai_catalogue_product_path($row, $productUrlMode);
		if ($path !== '') {
			$langPrefix = epc_apai_storefront_lang_prefix();
			return $domain . $langPrefix . $path;
		}
	}
	$slug = ($productUrlMode === 'id')
		? (string) (int) $row['id']
		: trim((string) ($row['alias'] ?? ''));
	if ($catUrl !== '' && $slug !== '') {
		$langPrefix = '';
		if (function_exists('multilang_init') && !empty($DP_Config->multilang)) {
			$isFrontMode = 1;
			$m = multilang_init();
			$langPrefix = (string) ($m['lang_href'] ?? '');
		}
		return $domain . $langPrefix . '/' . $catUrl . '/' . $slug;
	}
	$catId = (int) ($row['category_id'] ?? 0);
	if ($catId > 0) {
		return $domain . '/shop/catalogue/products?category_id=' . $catId . '&product_id=' . (int) $row['id'];
	}
	return $domain . '/shop/catalogue/products?product_id=' . (int) $row['id'];
}

function epc_ape_demo_product_ensure(PDO $pdo, string $siteKey): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$demo = array(
		'product_id' => 0,
		'title' => 'Samsung Galaxy Tab A9 — demo compare row',
		'storefront_url' => '',
		'sku' => 'DEMO-SAMSUNG-TAB',
		'in_matrix' => false,
	);
	if ($siteKey !== 'electronicae') {
		return $demo;
	}

	$stmt = $pdo->prepare(
		'SELECT sp.`product_id`, sp.`title`
		 FROM `epc_price_source_products` sp
		 INNER JOIN `epc_price_sources` ps ON ps.`id` = sp.`source_id`
		 WHERE ps.`site_key` = ? AND sp.`external_sku` = ?
		 ORDER BY sp.`product_id` DESC LIMIT 1'
	);
	$stmt->execute(array($siteKey, $demo['sku']));
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	$productId = (int) ($row['product_id'] ?? 0);
	if (!empty($row['title'])) {
		$demo['title'] = (string) $row['title'];
	}

	if ($productId <= 0) {
		$productId = epc_ape_create_catalogue_product($pdo, $demo['title'], 449.00, 'Auto Price Engine demo — marketplace arbitrage sample.');
		if ($productId > 0) {
			$upd = $pdo->prepare(
				'UPDATE `epc_price_source_products` sp
				 INNER JOIN `epc_price_sources` ps ON ps.`id` = sp.`source_id`
				 SET sp.`product_id` = ?
				 WHERE ps.`site_key` = ? AND sp.`external_sku` = ?'
			);
			$upd->execute(array($productId, $siteKey, $demo['sku']));
			$chk = $pdo->prepare('SELECT `id` FROM `epc_channel_listings` WHERE `site_key` = ? AND `product_id` = ? AND `channel_type` = ? LIMIT 1');
			$chk->execute(array($siteKey, $productId, 'storefront'));
			if ((int) $chk->fetchColumn() === 0) {
				epc_ape_create_listing($pdo, $siteKey, $productId, 'storefront', 449.00, array('source' => 'demo_seed'));
			}
		}
	}

	$demo['product_id'] = $productId;
	if ($productId > 0) {
		$demo['storefront_url'] = epc_ape_catalogue_product_url($pdo, $productId);
	}
	$matrix = epc_ape_compare_matrix($pdo, $siteKey);
	foreach ($matrix as $m) {
		if ((int) ($m['product_id'] ?? 0) === $productId || stripos((string) ($m['title'] ?? ''), 'Samsung Galaxy Tab') !== false) {
			$demo['in_matrix'] = true;
			break;
		}
	}
	if (!$demo['in_matrix'] && !empty($matrix)) {
		foreach ($matrix as $m) {
			if (stripos((string) ($m['title'] ?? ''), 'Samsung') !== false || stripos((string) ($m['title'] ?? ''), 'demo') !== false) {
				$demo['in_matrix'] = true;
				if ($demo['title'] === 'Samsung Galaxy Tab A9 — demo compare row') {
					$demo['title'] = (string) ($m['title'] ?? $demo['title']);
				}
				break;
			}
		}
	}

	return $demo;
}

/**
 * Apply tenant margin rules (epartscart pattern) — cost → sell price breakdown.
 *
 * @return array{cost:float,margin_pct:float,sell_price:float,margin_amount:float,currency:string}
 */
function epc_auto_price_apply_margin(PDO $pdo, float $cost, string $siteKey, string $currency = 'AED'): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$rules = epc_ape_rules_get($pdo, $siteKey);
	$marginPct = (float) ($rules['min_margin_percent'] ?? 15);
	$cfg = epc_ape_tenant_config_get($pdo, $siteKey);
	$config = (array) ($cfg['config'] ?? array());
	if (!empty($config['default_margin_pct'])) {
		$marginPct = (float) $config['default_margin_pct'];
	}
	if ($cost <= 0 && !empty($config['default_cost_ratio'])) {
		$cost = 0;
	}
	$cost = max(0, round($cost, 4));
	$sellPrice = $cost > 0 ? round($cost * (1 + $marginPct / 100), 2) : 0;
	return array(
		'cost' => $cost,
		'margin_pct' => $marginPct,
		'sell_price' => $sellPrice,
		'margin_amount' => $cost > 0 ? round($sellPrice - $cost, 2) : 0,
		'currency' => strtoupper(substr($currency, 0, 8)),
	);
}

function epc_disc_source_schema_migrate(PDO $pdo): void
{
	$migrations = array(
		'created_by_tenant' => 'TINYINT(1) NOT NULL DEFAULT 0 AFTER `priority`',
		'taxonomy_node_id' => 'INT UNSIGNED NOT NULL DEFAULT 0 AFTER `created_by_tenant`',
		'product_line_slug' => "VARCHAR(64) NOT NULL DEFAULT '' AFTER `taxonomy_node_id`",
		'auth_type' => "VARCHAR(16) NOT NULL DEFAULT 'none' AFTER `product_line_slug`",
		'auth_username' => "VARCHAR(255) NOT NULL DEFAULT '' AFTER `auth_type`",
		'auth_password' => "VARCHAR(512) NOT NULL DEFAULT '' AFTER `auth_username`",
	);
	foreach ($migrations as $col => $def) {
		try {
			$chk = $pdo->query("SHOW COLUMNS FROM `epc_discovery_sources` LIKE " . $pdo->quote($col))->fetch(PDO::FETCH_ASSOC);
			if (!$chk) {
				$pdo->exec('ALTER TABLE `epc_discovery_sources` ADD COLUMN `' . str_replace('`', '``', $col) . '` ' . $def);
			}
		} catch (Throwable $e) {
		}
	}
}

function epc_disc_source_normalize_domain(string $input): string
{
	$domain = trim($input);
	if ($domain === '') {
		return '';
	}
	if (preg_match('#^https?://#i', $domain)) {
		$host = strtolower((string) parse_url($domain, PHP_URL_HOST));
		return preg_replace('/^www\./', '', $host);
	}
	$domain = preg_replace('/^www\./i', '', $domain);
	$domain = preg_replace('#/.*$#', '', $domain);
	return strtolower(rtrim($domain, '/'));
}

function epc_disc_source_origin(array $row): string
{
	return !empty($row['created_by_tenant']) ? 'custom' : 'country_pack';
}

function epc_disc_auth_types(): array
{
	return array('none', 'basic', 'form_login');
}

function epc_disc_auth_password_encode(string $plain): string
{
	if ($plain === '') {
		return '';
	}
	return 'b64:' . base64_encode($plain);
}

function epc_disc_auth_password_decode(string $stored): string
{
	if ($stored === '') {
		return '';
	}
	if (strpos($stored, 'b64:') === 0) {
		$decoded = base64_decode(substr($stored, 4), true);
		return is_string($decoded) ? $decoded : '';
	}
	return $stored;
}

function epc_disc_source_config_decode(array $row): array
{
	$cfg = json_decode((string) ($row['config_json'] ?? ''), true);
	return is_array($cfg) ? $cfg : array();
}

/**
 * @return array<int,int>
 */
function &epc_disc_source_failure_queue(): array
{
	static $queue = array();
	return $queue;
}

function epc_disc_source_is_skipped(array $sourceConfig): bool
{
	$skipUntil = (int) ($sourceConfig['crawl_skip_until'] ?? 0);
	return $skipUntil > time();
}

function epc_disc_source_mark_crawl_failure(array $sourceConfig, string $reason = 'timeout'): void
{
	$sourceId = (int) ($sourceConfig['source_id'] ?? 0);
	if ($sourceId <= 0) {
		return;
	}
	$queue = &epc_disc_source_failure_queue();
	$queue[$sourceId] = (int) ($queue[$sourceId] ?? 0) + 1;
	$queue['reason_' . $sourceId] = $reason;
}

/**
 * Persist session failures — skip source 1h after first failure; 24h when fail_count >= 3.
 *
 * @return array{skipped:int,fallbacks:array<int,string>}
 */
function epc_disc_source_flush_failures(PDO $pdo): array
{
	$queue = epc_disc_source_failure_queue();
	$skipped = 0;
	$fallbacks = array();
	$now = time();
	foreach ($queue as $key => $count) {
		if (!is_int($key) && !ctype_digit((string) $key)) {
			continue;
		}
		$sourceId = (int) $key;
		if ($sourceId <= 0 || $count < 1) {
			continue;
		}
		$stmt = $pdo->prepare('SELECT * FROM `epc_discovery_sources` WHERE `id` = ? LIMIT 1');
		$stmt->execute(array($sourceId));
		$row = $stmt->fetch(PDO::FETCH_ASSOC);
		if (!$row) {
			continue;
		}
		$cfg = epc_disc_source_config_decode($row);
		$cfg['crawl_fail_count'] = (int) ($cfg['crawl_fail_count'] ?? 0) + (int) $count;
		$cfg['last_crawl_error'] = (string) ($queue['reason_' . $sourceId] ?? 'timeout');
		$cfg['last_crawl_error_at'] = $now;
		$skipHours = ((int) $cfg['crawl_fail_count'] >= 3) ? 24 : 1;
		$cfg['crawl_skip_until'] = $now + ($skipHours * 3600);
		$pdo->prepare('UPDATE `epc_discovery_sources` SET `config_json` = ?, `updated_at` = ? WHERE `id` = ?')
			->execute(array(json_encode($cfg, JSON_UNESCAPED_UNICODE), $now, $sourceId));
		$skipped++;
		$domain = strtolower((string) ($row['domain'] ?? ''));
		if (strpos($domain, 'spare247') !== false) {
			$fallbacks[$sourceId] = 'spare247 slow — using cached prices, retry login in Market sources';
		}
	}
	$queue = &epc_disc_source_failure_queue();
	$queue = array();
	return array('skipped' => $skipped, 'fallbacks' => $fallbacks);
}

function epc_disc_source_set_skip(PDO $pdo, int $sourceId, string $siteKey, int $hours = 24): bool
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$stmt = $pdo->prepare('SELECT * FROM `epc_discovery_sources` WHERE `id` = ? AND `site_key` = ? LIMIT 1');
	$stmt->execute(array($sourceId, $siteKey));
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return false;
	}
	$cfg = epc_disc_source_config_decode($row);
	$cfg['crawl_skip_until'] = time() + max(1, $hours) * 3600;
	$cfg['crawl_skip_manual'] = 1;
	$pdo->prepare('UPDATE `epc_discovery_sources` SET `config_json` = ?, `updated_at` = ? WHERE `id` = ?')
		->execute(array(json_encode($cfg, JSON_UNESCAPED_UNICODE), time(), $sourceId));
	return true;
}

function epc_disc_source_clear_skip(PDO $pdo, int $sourceId, string $siteKey): bool
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$stmt = $pdo->prepare('SELECT * FROM `epc_discovery_sources` WHERE `id` = ? AND `site_key` = ? LIMIT 1');
	$stmt->execute(array($sourceId, $siteKey));
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return false;
	}
	$cfg = epc_disc_source_config_decode($row);
	unset($cfg['crawl_skip_until'], $cfg['crawl_skip_manual'], $cfg['crawl_fail_count'], $cfg['last_crawl_error']);
	$pdo->prepare('UPDATE `epc_discovery_sources` SET `config_json` = ?, `updated_at` = ? WHERE `id` = ?')
		->execute(array(json_encode($cfg, JSON_UNESCAPED_UNICODE), time(), $sourceId));
	return true;
}

function epc_disc_source_has_login(array $row): bool
{
	$type = strtolower(trim((string) ($row['auth_type'] ?? 'none')));
	if (!in_array($type, array('basic', 'form_login'), true)) {
		return false;
	}
	if ($type === 'basic') {
		return trim((string) ($row['auth_username'] ?? '')) !== '' && trim((string) ($row['auth_password'] ?? '')) !== '';
	}
	return trim((string) ($row['auth_username'] ?? '')) !== '' && epc_disc_auth_password_decode((string) ($row['auth_password'] ?? '')) !== '';
}

/**
 * Known restricted B2B domains — form-login hints when login_url not set.
 *
 * @return array<string,mixed>
 */
function epc_disc_auth_defaults_for_domain(string $domain): array
{
	$domain = strtolower(preg_replace('/^www\./', '', trim($domain)));
	if ($domain === '' || strpos($domain, 'spare247') !== false) {
		return array(
			'auth_type' => 'form_login',
			'login_url' => 'https://www.spare247.com/login',
			'login_username_field' => 'email',
			'login_password_field' => 'password',
			'login_form_selector' => 'form[action*="login"], form#login-form, form.login-form',
		);
	}
	return array();
}

/**
 * Build fetch/crawl config for a discovery source row (includes decoded password for internal use only).
 *
 * @return array<string,mixed>
 */
function epc_disc_source_auth_config(array $row): array
{
	$type = strtolower(trim((string) ($row['auth_type'] ?? 'none')));
	if (!in_array($type, epc_disc_auth_types(), true)) {
		$type = 'none';
	}
	$cfg = epc_disc_source_config_decode($row);
	$domain = (string) ($row['domain'] ?? '');
	$defaults = epc_disc_auth_defaults_for_domain($domain);
	$out = array(
		'source_id' => (int) ($row['id'] ?? 0),
		'domain' => $domain,
		'auth_type' => $type,
		'auth_username' => trim((string) ($row['auth_username'] ?? '')),
		'auth_password' => epc_disc_auth_password_decode((string) ($row['auth_password'] ?? '')),
		'login_url' => trim((string) ($cfg['login_url'] ?? $defaults['login_url'] ?? '')),
		'login_form_selector' => trim((string) ($cfg['login_form_selector'] ?? $defaults['login_form_selector'] ?? '')),
		'login_username_field' => trim((string) ($cfg['login_username_field'] ?? $defaults['login_username_field'] ?? 'email')),
		'login_password_field' => trim((string) ($cfg['login_password_field'] ?? $defaults['login_password_field'] ?? 'password')),
		'session_cookies' => (array) ($cfg['session_cookies'] ?? array()),
		'session_expires_at' => (int) ($cfg['session_expires_at'] ?? 0),
		'crawl_skip_until' => (int) ($cfg['crawl_skip_until'] ?? 0),
		'crawl_fail_count' => (int) ($cfg['crawl_fail_count'] ?? 0),
		'last_crawl_error' => (string) ($cfg['last_crawl_error'] ?? ''),
	);
	if ($type === 'form_login' && $out['login_url'] === '' && $domain !== '') {
		$out['login_url'] = 'https://www.' . preg_replace('/^www\./', '', $domain) . '/login';
	}
	return $out;
}

/**
 * Merge country-pack rows with tenant custom rows on the same domain (custom auth overrides pack).
 *
 * @param array<int,array<string,mixed>> $rows
 * @return array<int,array<string,mixed>>
 */
function epc_disc_sources_merge_by_domain(array $rows): array
{
	$byDomain = array();
	foreach ($rows as $row) {
		$d = strtolower(trim((string) ($row['domain'] ?? '')));
		if ($d === '') {
			continue;
		}
		if (!isset($byDomain[$d])) {
			$byDomain[$d] = $row;
			continue;
		}
		$existing = $byDomain[$d];
		$custom = !empty($row['created_by_tenant']) ? $row : $existing;
		$pack = !empty($row['created_by_tenant']) ? $existing : $row;
		$merged = !empty($custom['created_by_tenant']) ? $custom : $pack;
		if (empty($merged['label']) && !empty($pack['label'])) {
			$merged['label'] = $pack['label'];
		}
		if ((int) ($merged['priority'] ?? 100) > (int) ($pack['priority'] ?? 100)) {
			$merged['priority'] = (int) $pack['priority'];
		}
		foreach (array('auth_type', 'auth_username', 'auth_password') as $authKey) {
			$authRow = epc_disc_source_has_login($custom) ? $custom : (epc_disc_source_has_login($pack) ? $pack : $custom);
			if (!empty($authRow[$authKey]) && (string) $authRow[$authKey] !== 'none') {
				$merged[$authKey] = $authRow[$authKey];
			}
		}
		$cfgCustom = epc_disc_source_config_decode($custom);
		$cfgPack = epc_disc_source_config_decode($pack);
		$mergedCfg = array_merge($cfgPack, $cfgCustom);
		if ($mergedCfg) {
			$merged['config_json'] = json_encode($mergedCfg, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
		}
		$byDomain[$d] = $merged;
	}
	$out = array_values($byDomain);
	usort($out, function ($a, $b) {
		$aCustom = !empty($a['created_by_tenant']) ? 0 : 1;
		$bCustom = !empty($b['created_by_tenant']) ? 0 : 1;
		if ($aCustom !== $bCustom) {
			return $aCustom <=> $bCustom;
		}
		return ((int) ($a['priority'] ?? 100)) <=> ((int) ($b['priority'] ?? 100));
	});
	return $out;
}

/**
 * Resolve discovery source auth config for a URL from enabled sources list.
 *
 * @param array<int,array<string,mixed>> $sources
 * @return array<string,mixed>
 */
function epc_disc_source_config_for_url(string $url, array $sources): array
{
	$host = strtolower((string) parse_url($url, PHP_URL_HOST));
	$host = preg_replace('/^www\./', '', $host);
	if ($host === '') {
		return array();
	}
	foreach ($sources as $src) {
		$d = strtolower(preg_replace('/^www\./', '', trim((string) ($src['domain'] ?? ''))));
		if ($d === '' || ($d !== $host && strpos($host, $d) === false && strpos($d, $host) === false)) {
			continue;
		}
		if (!epc_disc_source_has_login($src)) {
			return array('source_id' => (int) ($src['id'] ?? 0), 'domain' => $d, 'auth_type' => 'none');
		}
		return epc_disc_source_auth_config($src);
	}
	return array();
}

function epc_disc_source_get(PDO $pdo, int $id, string $siteKey = ''): ?array
{
	if ($id <= 0) {
		return null;
	}
	$stmt = $pdo->prepare('SELECT * FROM `epc_discovery_sources` WHERE `id` = ? LIMIT 1');
	$stmt->execute(array($id));
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return null;
	}
	if ($siteKey !== '') {
		$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
		if ((string) ($row['site_key'] ?? '') !== $siteKey) {
			return null;
		}
	}
	return $row;
}

function epc_disc_source_format_row(array $row): array
{
	$taxId = (int) ($row['taxonomy_node_id'] ?? 0);
	$slug = (string) ($row['product_line_slug'] ?? '');
	$authType = strtolower(trim((string) ($row['auth_type'] ?? 'none')));
	if (!in_array($authType, epc_disc_auth_types(), true)) {
		$authType = 'none';
	}
	$cfg = epc_disc_source_config_decode($row);
	return array(
		'id' => (int) ($row['id'] ?? 0),
		'site_key' => (string) ($row['site_key'] ?? ''),
		'source_type' => (string) ($row['source_type'] ?? 'custom_website'),
		'domain' => (string) ($row['domain'] ?? ''),
		'label' => (string) ($row['label'] ?? ''),
		'enabled' => !empty($row['enabled']),
		'priority' => (int) ($row['priority'] ?? 100),
		'created_by_tenant' => !empty($row['created_by_tenant']),
		'taxonomy_node_id' => $taxId,
		'product_line_slug' => $slug,
		'origin' => epc_disc_source_origin($row),
		'editable' => !empty($row['created_by_tenant']),
		'scoped' => ($taxId > 0 || $slug !== ''),
		'last_crawl' => (int) ($row['last_crawl'] ?? 0),
		'auth_type' => $authType,
		'auth_username' => trim((string) ($row['auth_username'] ?? '')),
		'login_configured' => epc_disc_source_has_login($row),
		'login_url' => trim((string) ($cfg['login_url'] ?? '')),
		'login_form_selector' => trim((string) ($cfg['login_form_selector'] ?? '')),
		'last_test_at' => (int) ($cfg['last_test_at'] ?? 0),
		'last_test_ok' => !empty($cfg['last_test_ok']),
		'last_test_message' => trim((string) ($cfg['last_test_message'] ?? '')),
		'crawl_skip_until' => (int) ($cfg['crawl_skip_until'] ?? 0),
		'crawl_skipped' => (int) ($cfg['crawl_skip_until'] ?? 0) > time(),
		'last_crawl_error' => trim((string) ($cfg['last_crawl_error'] ?? '')),
	);
}

function epc_disc_sources_list(PDO $pdo, string $siteKey, bool $enabledOnly = false): array
{
	return epc_disc_sources_for_tenant($pdo, $siteKey, $enabledOnly);
}

/**
 * Tenant-visible discovery sources — excludes own storefront domain(s).
 *
 * @return array<int,array<string,mixed>>
 */
function epc_disc_sources_for_tenant(PDO $pdo, string $siteKey, bool $enabledOnly = false): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$sql = 'SELECT * FROM `epc_discovery_sources` WHERE `site_key` = ?';
	if ($enabledOnly) {
		$sql .= ' AND `enabled` = 1';
	}
	$sql .= ' ORDER BY `created_by_tenant` DESC, `priority`, `label`';
	$stmt = $pdo->prepare($sql);
	$stmt->execute(array($siteKey));
	$rows = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
	$rows = epc_disc_sources_merge_by_domain($rows);
	if (is_file(__DIR__ . '/epc_apai_country_sources.php')) {
		require_once __DIR__ . '/epc_apai_country_sources.php';
		if (function_exists('epc_apai_tenant_own_domains')) {
			$ownSet = array();
			foreach (epc_apai_tenant_own_domains($siteKey, $pdo) as $own) {
				$bare = function_exists('epc_apai_normalize_domain')
					? epc_apai_normalize_domain($own)
					: epc_disc_source_normalize_domain($own);
				if ($bare !== '') {
					$ownSet[$bare] = true;
				}
			}
			if ($ownSet) {
				$filtered = array();
				foreach ($rows as $row) {
					$d = function_exists('epc_apai_normalize_domain')
						? epc_apai_normalize_domain((string) ($row['domain'] ?? ''))
						: epc_disc_source_normalize_domain((string) ($row['domain'] ?? ''));
					if ($d !== '' && isset($ownSet[$d])) {
						continue;
					}
					$filtered[] = $row;
				}
				$rows = $filtered;
			}
		}
	}
	return $rows;
}

/**
 * Enabled sources for a discovery run — global + product-line scoped when taxonomy given.
 *
 * @return array<int,array<string,mixed>>
 */
function epc_disc_sources_for_search(PDO $pdo, string $siteKey, int $taxonomyNodeId = 0, string $taxonomySlug = '', bool $enabledOnly = true): array
{
	if (is_file(__DIR__ . '/epc_apai_country_sources.php')) {
		require_once __DIR__ . '/epc_apai_country_sources.php';
		epc_apai_install_country_sources($pdo, $siteKey);
	}
	$rows = epc_disc_sources_list($pdo, $siteKey, $enabledOnly);
	$active = array();
	foreach ($rows as $row) {
		$cfg = epc_disc_source_auth_config($row);
		if (!epc_disc_source_is_skipped($cfg)) {
			$active[] = $row;
		}
	}
	$rows = $active ?: $rows;
	if ($taxonomyNodeId <= 0 && $taxonomySlug === '') {
		return $rows;
	}
	$slug = preg_replace('/[^a-z0-9\-]/', '', strtolower(trim($taxonomySlug)));
	$filtered = array();
	foreach ($rows as $row) {
		$rowTaxId = (int) ($row['taxonomy_node_id'] ?? 0);
		$rowSlug = preg_replace('/[^a-z0-9\-]/', '', strtolower((string) ($row['product_line_slug'] ?? '')));
		$isGlobal = ($rowTaxId <= 0 && $rowSlug === '');
		$matchesTax = ($taxonomyNodeId > 0 && $rowTaxId === $taxonomyNodeId);
		$matchesSlug = ($slug !== '' && $rowSlug !== '' && ($rowSlug === $slug || strpos($slug, $rowSlug) === 0 || strpos($rowSlug, $slug) === 0));
		if ($isGlobal || $matchesTax || $matchesSlug) {
			$filtered[] = $row;
		}
	}
	if (!$filtered) {
		return $rows;
	}
	usort($filtered, function ($a, $b) {
		$aScoped = ((int) ($a['taxonomy_node_id'] ?? 0) > 0 || (string) ($a['product_line_slug'] ?? '') !== '') ? 0 : 1;
		$bScoped = ((int) ($b['taxonomy_node_id'] ?? 0) > 0 || (string) ($b['product_line_slug'] ?? '') !== '') ? 0 : 1;
		if ($aScoped !== $bScoped) {
			return $aScoped <=> $bScoped;
		}
		return ((int) ($a['priority'] ?? 100)) <=> ((int) ($b['priority'] ?? 100));
	});
	return $filtered;
}

/**
 * @return array<int,array{domain:string,label:string,priority:int}>
 */
function epc_disc_sources_to_domain_list(array $sources): array
{
	$out = array();
	$seen = array();
	foreach ($sources as $src) {
		if ((string) ($src['source_type'] ?? '') === 'search_engine') {
			continue;
		}
		$d = strtolower(trim((string) ($src['domain'] ?? '')));
		if ($d === '' || isset($seen[$d])) {
			continue;
		}
		$seen[$d] = true;
		$out[] = array(
			'domain' => $d,
			'label' => (string) ($src['label'] ?? $d),
			'priority' => (int) ($src['priority'] ?? 100),
		);
	}
	return $out;
}

function epc_disc_sources_search_message(array $sources, string $prefix = 'Searching'): string
{
	$domains = epc_disc_sources_to_domain_list($sources);
	if (!$domains) {
		return $prefix . ' configured market sources';
	}
	$labels = array();
	foreach ($domains as $d) {
		$labels[] = (string) $d['domain'];
	}
	$msg = $prefix . ' ' . count($labels) . ' source' . (count($labels) === 1 ? '' : 's') . ': ' . implode(', ', array_slice($labels, 0, 10));
	if (count($labels) > 10) {
		$msg .= '…';
	}
	return $msg;
}

function epc_disc_source_save(PDO $pdo, string $siteKey, array $data, int $id = 0): int
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$domain = epc_disc_source_normalize_domain((string) ($data['domain'] ?? ''));
	if ($domain === '') {
		throw new InvalidArgumentException('Domain or URL is required');
	}
	if (is_file(__DIR__ . '/epc_apai_country_sources.php')) {
		require_once __DIR__ . '/epc_apai_country_sources.php';
		if (function_exists('epc_apai_is_tenant_own_domain') && epc_apai_is_tenant_own_domain($siteKey, $domain, $pdo)) {
			throw new InvalidArgumentException('Your own storefront domain cannot be used as an external discovery source');
		}
	}
	$label = trim((string) ($data['label'] ?? $domain));
	if ($label === '') {
		$label = $domain;
	}
	$type = (string) ($data['source_type'] ?? 'custom_website');
	$taxonomyNodeId = max(0, (int) ($data['taxonomy_node_id'] ?? 0));
	$productLineSlug = preg_replace('/[^a-z0-9\-]/', '', strtolower(trim((string) ($data['product_line_slug'] ?? ''))));
	if ($taxonomyNodeId > 0 && $productLineSlug === '' && is_file(__DIR__ . '/epc_industry_taxonomy.php')) {
		require_once __DIR__ . '/epc_industry_taxonomy.php';
		$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
		$st = $pdo->prepare('SELECT `slug` FROM `epc_product_taxonomy_nodes` WHERE `id` = ? AND `industry_key` = ? LIMIT 1');
		$st->execute(array($taxonomyNodeId, $industryKey));
		$productLineSlug = preg_replace('/[^a-z0-9\-]/', '', strtolower((string) ($st->fetchColumn() ?: '')));
	}
	$createdByTenant = !empty($data['created_by_tenant']) ? 1 : 0;
	if ($id > 0) {
		$existing = epc_disc_source_get($pdo, $id, $siteKey);
		if (!$existing) {
			throw new RuntimeException('Discovery source not found');
		}
		if (empty($existing['created_by_tenant'])) {
			throw new RuntimeException('Country pack sources cannot be edited');
		}
		$createdByTenant = 1;
	}
	$authType = strtolower(trim((string) ($data['auth_type'] ?? 'none')));
	if (empty($data['requires_login']) && $authType === 'none') {
		$authType = 'none';
	} elseif ($authType === 'none' && !empty($data['requires_login'])) {
		$authType = 'form_login';
	}
	if (!in_array($authType, epc_disc_auth_types(), true)) {
		$authType = 'none';
	}
	$authUsername = trim((string) ($data['auth_username'] ?? ''));
	$newPassword = (string) ($data['auth_password'] ?? '');
	$authPassword = '';
	if ($id > 0 && isset($existing)) {
		$authPassword = (string) ($existing['auth_password'] ?? '');
	}
	if ($newPassword !== '') {
		$authPassword = epc_disc_auth_password_encode($newPassword);
	} elseif ($authType === 'none') {
		$authPassword = '';
		$authUsername = '';
	}
	$configJson = array();
	if ($id > 0 && isset($existing)) {
		$configJson = epc_disc_source_config_decode($existing);
	}
	if (!empty($data['config_json']) && is_array($data['config_json'])) {
		$configJson = array_merge($configJson, $data['config_json']);
	}
	$loginUrl = trim((string) ($data['login_url'] ?? ''));
	$loginFormSelector = trim((string) ($data['login_form_selector'] ?? ''));
	if ($loginUrl !== '') {
		$configJson['login_url'] = $loginUrl;
	} elseif ($authType === 'none') {
		unset($configJson['login_url'], $configJson['login_form_selector']);
	}
	if ($loginFormSelector !== '') {
		$configJson['login_form_selector'] = $loginFormSelector;
	}
	if ($authPassword !== '') {
		$configJson['auth_storage'] = 'b64_pending_encryption';
	} else {
		unset($configJson['auth_storage']);
	}
	$configJsonStr = $configJson ? json_encode($configJson, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) : null;
	$now = time();
	$params = array(
		$siteKey, $type, $domain, $label,
		!empty($data['enabled']) ? 1 : 0,
		(int) ($data['priority'] ?? 100),
		$createdByTenant,
		$taxonomyNodeId,
		$productLineSlug,
		$authType,
		$authUsername,
		$authPassword,
		$configJsonStr,
		$now,
	);
	if ($id > 0) {
		$params[] = $id;
		$pdo->prepare(
			'UPDATE `epc_discovery_sources` SET `site_key`=?, `source_type`=?, `domain`=?, `label`=?, `enabled`=?, `priority`=?, `created_by_tenant`=?, `taxonomy_node_id`=?, `product_line_slug`=?, `auth_type`=?, `auth_username`=?, `auth_password`=?, `config_json`=?, `updated_at`=? WHERE `id`=?'
		)->execute($params);
		return $id;
	}
	$params[] = $now;
	$pdo->prepare(
		'INSERT INTO `epc_discovery_sources` (`site_key`, `source_type`, `domain`, `label`, `enabled`, `priority`, `created_by_tenant`, `taxonomy_node_id`, `product_line_slug`, `auth_type`, `auth_username`, `auth_password`, `config_json`, `updated_at`, `created_at`)
		 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
	)->execute($params);
	return (int) $pdo->lastInsertId();
}

/**
 * Test login for a discovery source; persists last_test_* in config_json when id given.
 *
 * @param array<string,mixed> $data
 * @return array{ok:bool,message:string,last_test_at?:int,last_test_ok?:bool}
 */
function epc_disc_source_test_login(PDO $pdo, string $siteKey, array $data): array
{
	require_once __DIR__ . '/epc_discovery_adapters.php';
	$id = max(0, (int) ($data['id'] ?? 0));
	$row = $id > 0 ? epc_disc_source_get($pdo, $id, $siteKey) : null;
	$domain = trim((string) ($data['domain'] ?? ''));
	if ($domain === '' && $row) {
		$domain = (string) ($row['domain'] ?? '');
	}
	$authType = strtolower(trim((string) ($data['auth_type'] ?? '')));
	if ($authType === '' && $row) {
		$authType = strtolower(trim((string) ($row['auth_type'] ?? 'none')));
	}
	if (!empty($data['requires_login']) && $authType === 'none') {
		$authType = 'form_login';
	}
	$authUsername = trim((string) ($data['auth_username'] ?? ''));
	if ($authUsername === '' && $row) {
		$authUsername = trim((string) ($row['auth_username'] ?? ''));
	}
	$authPassword = (string) ($data['auth_password'] ?? '');
	if ($authPassword === '' && $row) {
		$authPassword = epc_disc_auth_password_decode((string) ($row['auth_password'] ?? ''));
	}
	$config = array(
		'source_id' => $id > 0 ? $id : 900000 + (int) (time() % 100000),
		'domain' => $domain,
		'auth_type' => $authType,
		'auth_username' => $authUsername,
		'auth_password' => $authPassword,
		'login_url' => trim((string) ($data['login_url'] ?? '')),
		'login_form_selector' => trim((string) ($data['login_form_selector'] ?? '')),
	);
	if ($row) {
		$existing = epc_disc_source_auth_config($row);
		if ($config['login_url'] === '') {
			$config['login_url'] = (string) ($existing['login_url'] ?? '');
		}
		if ($config['login_form_selector'] === '') {
			$config['login_form_selector'] = (string) ($existing['login_form_selector'] ?? '');
		}
		$config['login_username_field'] = (string) ($existing['login_username_field'] ?? 'email');
		$config['login_password_field'] = (string) ($existing['login_password_field'] ?? 'password');
	} else {
		$defaults = epc_disc_auth_defaults_for_domain($domain);
		if ($config['login_url'] === '' && !empty($defaults['login_url'])) {
			$config['login_url'] = (string) $defaults['login_url'];
		}
		$config['login_username_field'] = (string) ($defaults['login_username_field'] ?? 'email');
		$config['login_password_field'] = (string) ($defaults['login_password_field'] ?? 'password');
	}
	$result = epc_disc_test_source_auth($config);
	$now = time();
	if ($id > 0 && $row) {
		$cfg = epc_disc_source_config_decode($row);
		$cfg['last_test_at'] = $now;
		$cfg['last_test_ok'] = !empty($result['ok']) ? 1 : 0;
		$cfg['last_test_message'] = (string) ($result['message'] ?? '');
		$pdo->prepare('UPDATE `epc_discovery_sources` SET `config_json` = ?, `updated_at` = ? WHERE `id` = ?')
			->execute(array(json_encode($cfg, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), $now, $id));
	}
	return array_merge($result, array(
		'last_test_at' => $now,
		'last_test_ok' => !empty($result['ok']),
	));
}

function epc_disc_source_delete(PDO $pdo, int $id, string $siteKey = ''): bool
{
	$row = epc_disc_source_get($pdo, $id, $siteKey);
	if (!$row) {
		return false;
	}
	if (empty($row['created_by_tenant'])) {
		return false;
	}
	$pdo->prepare('DELETE FROM `epc_discovery_sources` WHERE `id` = ?')->execute(array($id));
	return true;
}

function epc_disc_source_toggle(PDO $pdo, int $id, string $siteKey, ?bool $enabled = null): bool
{
	$row = epc_disc_source_get($pdo, $id, $siteKey);
	if (!$row) {
		return false;
	}
	$newState = ($enabled === null) ? (empty($row['enabled']) ? 1 : 0) : ($enabled ? 1 : 0);
	$pdo->prepare('UPDATE `epc_discovery_sources` SET `enabled` = ?, `updated_at` = ? WHERE `id` = ?')
		->execute(array($newState, time(), $id));
	return true;
}

function epc_disc_price_threshold_pct(): float
{
	return 1.0;
}

function epc_disc_price_threshold_abs(): float
{
	return 0.01;
}

function epc_disc_prices_differ(float $a, float $b): bool
{
	if ($a <= 0 || $b <= 0) {
		return false;
	}
	if (abs($a - $b) >= epc_disc_price_threshold_abs()) {
		return true;
	}
	$base = max($a, $b);
	return $base > 0 && (abs($a - $b) / $base) * 100 >= epc_disc_price_threshold_pct();
}

function epc_disc_get_last_crawl_at(PDO $pdo, string $siteKey): int
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	try {
		$stmt = $pdo->prepare('SELECT `last_crawl_at` FROM `epc_auto_price_tenant_config` WHERE `site_key` = ? LIMIT 1');
		$stmt->execute(array($siteKey));
		return (int) $stmt->fetchColumn();
	} catch (Throwable $e) {
		return 0;
	}
}

function epc_disc_set_last_crawl_at(PDO $pdo, string $siteKey, int $ts = 0): void
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$ts = $ts > 0 ? $ts : time();
	try {
		$pdo->prepare(
			'INSERT INTO `epc_auto_price_tenant_config` (`site_key`, `profile`, `currency`, `active`, `last_crawl_at`, `updated_at`)
			 VALUES (?, \'warehouse_supplier\', \'AED\', 1, ?, ?)
			 ON DUPLICATE KEY UPDATE `last_crawl_at` = VALUES(`last_crawl_at`), `updated_at` = VALUES(`updated_at`)'
		)->execute(array($siteKey, $ts, $ts));
	} catch (Throwable $e) {
	}
}

function epc_disc_get_last_scheduled_crawl_at(PDO $pdo, string $siteKey): int
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	try {
		$stmt = $pdo->prepare('SELECT `last_scheduled_crawl_at` FROM `epc_auto_price_tenant_config` WHERE `site_key` = ? LIMIT 1');
		$stmt->execute(array($siteKey));
		return (int) $stmt->fetchColumn();
	} catch (Throwable $e) {
		return 0;
	}
}

function epc_disc_set_last_scheduled_crawl_at(PDO $pdo, string $siteKey, int $ts = 0): void
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$ts = $ts > 0 ? $ts : time();
	try {
		$pdo->prepare(
			'INSERT INTO `epc_auto_price_tenant_config` (`site_key`, `profile`, `currency`, `active`, `last_crawl_at`, `last_scheduled_crawl_at`, `updated_at`)
			 VALUES (?, \'warehouse_supplier\', \'AED\', 1, ?, ?, ?)
			 ON DUPLICATE KEY UPDATE `last_crawl_at` = VALUES(`last_crawl_at`), `last_scheduled_crawl_at` = VALUES(`last_scheduled_crawl_at`), `updated_at` = VALUES(`updated_at`)'
		)->execute(array($siteKey, $ts, $ts, $ts));
	} catch (Throwable $e) {
	}
}

function epc_disc_crawl_job_enqueue(PDO $pdo, string $siteKey, string $mode = 'full', array $options = array()): int
{
	epc_disc_ensure_schema($pdo);
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$mode = in_array($mode, array('quick', 'full'), true) ? $mode : 'full';
	$now = time();
	$pdo->prepare(
		'INSERT INTO `epc_apai_crawl_jobs` (`site_key`, `status`, `mode`, `options_json`, `created_at`) VALUES (?, \'pending\', ?, ?, ?)'
	)->execute(array($siteKey, $mode, json_encode($options, JSON_UNESCAPED_UNICODE), $now));
	return (int) $pdo->lastInsertId();
}

/**
 * @return array<string,mixed>|null
 */
function epc_disc_crawl_job_active(PDO $pdo, string $siteKey): ?array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$stmt = $pdo->prepare(
		'SELECT * FROM `epc_apai_crawl_jobs` WHERE `site_key` = ? AND `status` IN (\'pending\', \'running\') ORDER BY `id` DESC LIMIT 1'
	);
	$stmt->execute(array($siteKey));
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

/**
 * @return array<string,mixed>|null
 */
function epc_disc_crawl_job_get(PDO $pdo, int $jobId, string $siteKey = ''): ?array
{
	if ($jobId <= 0) {
		return null;
	}
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if ($siteKey !== '') {
		$stmt = $pdo->prepare('SELECT * FROM `epc_apai_crawl_jobs` WHERE `id` = ? AND `site_key` = ? LIMIT 1');
		$stmt->execute(array($jobId, $siteKey));
	} else {
		$stmt = $pdo->prepare('SELECT * FROM `epc_apai_crawl_jobs` WHERE `id` = ? LIMIT 1');
		$stmt->execute(array($jobId));
	}
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_disc_crawl_job_process_one(PDO $pdo, string $siteKey = ''): ?array
{
	epc_disc_ensure_schema($pdo);
	require_once __DIR__ . '/epc_discovery_adapters.php';
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$sql = 'SELECT * FROM `epc_apai_crawl_jobs` WHERE `status` = \'pending\'';
	$params = array();
	if ($siteKey !== '') {
		$sql .= ' AND `site_key` = ?';
		$params[] = $siteKey;
	}
	$sql .= ' ORDER BY `id` ASC LIMIT 1';
	$stmt = $pdo->prepare($sql);
	$stmt->execute($params);
	$job = $stmt->fetch(PDO::FETCH_ASSOC);
	if (!$job) {
		return null;
	}
	$jobId = (int) ($job['id'] ?? 0);
	$jobSite = (string) ($job['site_key'] ?? '');
	$mode = (string) ($job['mode'] ?? 'full');
	$opts = json_decode((string) ($job['options_json'] ?? ''), true);
	if (!is_array($opts)) {
		$opts = array();
	}
	$now = time();
	$pdo->prepare('UPDATE `epc_apai_crawl_jobs` SET `status` = \'running\', `started_at` = ? WHERE `id` = ?')
		->execute(array($now, $jobId));
	$res = epc_disc_crawl_sources($pdo, $jobSite, array_merge($opts, array('mode' => $mode, 'scheduled' => !empty($opts['scheduled']))));
	$pdo->prepare('UPDATE `epc_apai_crawl_jobs` SET `status` = \'done\', `finished_at` = ?, `result_json` = ? WHERE `id` = ?')
		->execute(array(time(), json_encode($res, JSON_UNESCAPED_UNICODE), $jobId));
	return array_merge($res, array('job_id' => $jobId, 'job_status' => 'done'));
}

/**
 * Stagger hourly cron — tenant index slots every 12 minutes within the hour.
 */
function epc_apai_hourly_crawl_should_run_tenant(string $siteKey, int $tenantIndex, int $tenantCount): bool
{
	if ($tenantCount <= 1) {
		return true;
	}
	$minute = (int) date('i');
	$slot = (int) floor($minute / 12);
	$tenantSlot = $tenantIndex % max(1, min(5, $tenantCount));
	return $slot === $tenantSlot;
}

/**
 * @return array{enabled:bool,interval_hours:int}
 */
function epc_apai_auto_crawl_config(PDO $pdo, string $siteKey): array
{
	$cfg = epc_ape_tenant_config_get($pdo, $siteKey);
	$config = (array) ($cfg['config'] ?? array());
	return array(
		'enabled' => !array_key_exists('auto_crawl_enabled', $config) || !empty($config['auto_crawl_enabled']),
		'interval_hours' => max(1, (int) ($config['auto_crawl_interval_hours'] ?? 1)),
	);
}

function epc_apai_next_scheduled_crawl_at(PDO $pdo, string $siteKey): int
{
	$autoCfg = epc_apai_auto_crawl_config($pdo, $siteKey);
	if (!$autoCfg['enabled']) {
		return 0;
	}
	$last = epc_disc_get_last_scheduled_crawl_at($pdo, $siteKey);
	if ($last <= 0) {
		$last = epc_disc_get_last_crawl_at($pdo, $siteKey);
	}
	$interval = $autoCfg['interval_hours'] * 3600;
	if ($last <= 0) {
		return time();
	}
	return $last + $interval;
}

/** Normalize OEM brand for brand_article_key (lowercase slug). */
function epc_apai_normalize_brand(string $s): string
{
	$s = strtolower(trim(preg_replace('/\s+/', ' ', $s)));
	$s = preg_replace('/[^a-z0-9\-]/', '', str_replace(array(' ', '_'), '-', $s));
	$aliases = array(
		'mercedes-benz' => 'mercedes',
		'mercedesbenz' => 'mercedes',
		'vw' => 'volkswagen',
		'lexus-toyota' => 'toyota',
		'gm' => 'chevrolet',
	);
	return $aliases[$s] ?? $s;
}

/** Normalize part/article number — strip spaces, uppercase. */
function epc_apai_normalize_article(string $s): string
{
	$s = strtoupper(trim($s));
	return preg_replace('/[\s\-\.]/', '', $s);
}

/** Primary spare-parts key: `{brand}:{article}` e.g. `toyota:1310154101`. */
function epc_apai_brand_article_key(string $brand, string $article): string
{
	$brand = epc_apai_normalize_brand($brand);
	$article = epc_apai_normalize_article($article);
	if ($brand === '' || $article === '') {
		return '';
	}
	return $brand . ':' . $article;
}

/**
 * @param array<string,string> $specs
 */
function epc_apai_specs_extract_brand(array $specs): string
{
	foreach (array('brand', 'Brand', 'OEM Brand', 'Manufacturer', 'Make', 'OEM') as $k) {
		if (!empty($specs[$k])) {
			return trim((string) $specs[$k]);
		}
	}
	return '';
}

/**
 * @param array<string,string> $specs
 */
function epc_apai_specs_extract_article(array $specs): string
{
	foreach (array(
		'article_number', 'Article Number', 'Article', 'article', 'Part Number', 'Part number',
		'part_number', 'OEM Number', 'OE Number', 'oem_number', 'MPN', 'mpn', 'SKU', 'sku',
	) as $k) {
		if (!empty($specs[$k])) {
			return trim((string) $specs[$k]);
		}
	}
	return '';
}

/**
 * Enrich specs with brand, article_number, brand_article_key (and aliases).
 *
 * @param array<string,string> $specs
 * @return array<string,string>
 */
function epc_apai_specs_enrich_brand_article(array $specs, string $url = '', string $title = ''): array
{
	$brand = epc_apai_specs_extract_brand($specs);
	$article = epc_apai_specs_extract_article($specs);
	if (($brand === '' || $article === '') && $url !== '' && function_exists('epc_disc_extract_brand_article_from_url')) {
		$urlParts = epc_disc_extract_brand_article_from_url($url);
		if ($brand === '' && !empty($urlParts['brand'])) {
			$brand = (string) $urlParts['brand'];
		}
		if ($article === '' && !empty($urlParts['article_number'])) {
			$article = (string) $urlParts['article_number'];
		}
	}
	if (($brand === '' || $article === '') && $title !== '' && function_exists('epc_disc_parse_brand_article_query')) {
		$parsed = epc_disc_parse_brand_article_query($title);
		if ($brand === '' && !empty($parsed['brand'])) {
			$brand = (string) $parsed['brand'];
		}
		if ($article === '' && !empty($parsed['article_number'])) {
			$article = (string) $parsed['article_number'];
		}
	}
	if ($brand !== '') {
		$specs['brand'] = $brand;
	}
	if ($article !== '') {
		$specs['article_number'] = $article;
		$specs['oem_number'] = $article;
		$specs['part_number'] = $article;
	}
	$key = epc_apai_brand_article_key($brand, $article);
	if ($key !== '') {
		$specs['brand_article_key'] = $key;
	}
	return $specs;
}

/**
 * @param array<string,mixed> $row
 */
function epc_apai_queue_brand_article_key(array $row): string
{
	$specs = is_array($row['specs'] ?? null) ? $row['specs'] : array();
	if (!$specs) {
		$specs = json_decode((string) ($row['specs_json'] ?? ''), true);
		if (!is_array($specs)) {
			$specs = array();
		}
	}
	if (!empty($specs['brand_article_key'])) {
		return (string) $specs['brand_article_key'];
	}
	return epc_apai_brand_article_key(
		epc_apai_specs_extract_brand($specs),
		epc_apai_specs_extract_article($specs)
	);
}

/**
 * Tenant pricing strategy options (Rules tab).
 *
 * @return array<string,string>
 */
function epc_apai_pricing_strategy_options(): array
{
	return array(
		'lowest_cost_highest_target' => 'Lowest buy cost → Marketplace sell target',
		'lowest_cost_plus_markup_pct' => 'Lowest cost + fixed markup %',
		'match_lowest_competitor' => 'Match lowest competitor',
	);
}

function epc_apai_pricing_strategy(PDO $pdo, string $siteKey): string
{
	$cfg = epc_ape_tenant_config_get($pdo, $siteKey);
	$config = (array) ($cfg['config'] ?? array());
	$strategy = (string) ($config['pricing_strategy'] ?? '');
	$options = epc_apai_pricing_strategy_options();
	if (!isset($options[$strategy])) {
		$profile = (string) ($cfg['profile'] ?? 'warehouse_supplier');
		return in_array($profile, array('warehouse_supplier', 'marketplace_arbitrage', 'professional_services'), true)
			? 'lowest_cost_highest_target'
			: 'lowest_cost_plus_markup_pct';
	}
	return $strategy;
}

function epc_apai_save_pricing_strategy(PDO $pdo, string $siteKey, string $strategy, float $markupPct = 0): void
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$options = epc_apai_pricing_strategy_options();
	if (!isset($options[$strategy])) {
		$strategy = 'lowest_cost_highest_target';
	}
	$cfg = epc_ape_tenant_config_get($pdo, $siteKey);
	$config = (array) ($cfg['config'] ?? array());
	$config['pricing_strategy'] = $strategy;
	if ($markupPct > 0) {
		$config['pricing_markup_pct'] = $markupPct;
	}
	$now = time();
	$pdo->prepare(
		'INSERT INTO `epc_auto_price_tenant_config` (`site_key`, `profile`, `currency`, `active`, `config_json`, `updated_at`)
		 VALUES (?, ?, ?, 1, ?, ?)
		 ON DUPLICATE KEY UPDATE `config_json` = VALUES(`config_json`), `updated_at` = VALUES(`updated_at`)'
	)->execute(array(
		$siteKey,
		(string) ($cfg['profile'] ?? 'warehouse_supplier'),
		(string) ($cfg['currency'] ?? 'AED'),
		json_encode($config, JSON_UNESCAPED_UNICODE),
		$now,
	));
}

/**
 * Normalize product title for cross-source identity (strip colors, storage, region).
 */
function epc_disc_normalize_title_identity(string $title): string
{
	$t = strtolower(trim(preg_replace('/\s+/', ' ', $title)));
	if ($t === '') {
		return '';
	}
	$strip = array(
		'/[\—\-–]\s*(titanium|natural|black|white|silver|gold|rose gold|graphite|obsidian|charcoal|chalk|sand|emerald|navy|gray|grey|space gray|midnight|starlight|blue|red|green|purple|pink|beige|uae version|global version|international version)[\s\w]*/iu',
		'/\b\d+\s*(gb|tb|mb)\b/iu',
		'/\(\s*[^)]*\s*\)/u',
		'/\b(wifi|wi-fi|lte|5g|4g|unlocked|renewed|refurbished)\b/iu',
	);
	foreach ($strip as $pat) {
		$t = trim(preg_replace($pat, ' ', $t));
	}
	$t = preg_replace('/\s+/', ' ', $t);
	return trim($t);
}

/**
 * Extract brand token from product title.
 */
function epc_disc_extract_brand_from_title(string $title): string
{
	$known = array('samsung', 'apple', 'sony', 'lg', 'google', 'microsoft', 'xbox', 'nintendo', 'dell', 'hp', 'lenovo', 'asus', 'huawei', 'xiaomi', 'oneplus', 'amazon', 'bose', 'jbl', 'philips', 'toyota', 'nissan', 'osram');
	$t = strtolower(trim($title));
	foreach ($known as $brand) {
		if (strpos($t, $brand) === 0 || preg_match('/\b' . preg_quote($brand, '/') . '\b/i', $t)) {
			return $brand;
		}
	}
	if (preg_match('/^([a-z][a-z0-9+\-]{1,20})\b/i', $title, $m)) {
		return strtolower($m[1]);
	}
	return '';
}

/**
 * Extract model / MPN token from queue row specs or title.
 *
 * @param array<string,mixed> $row
 */
function epc_disc_extract_model_number(array $row): string
{
	$specs = is_array($row['specs'] ?? null) ? $row['specs'] : array();
	if (!$specs) {
		$specs = json_decode((string) ($row['specs_json'] ?? ''), true);
		if (!is_array($specs)) {
			$specs = array();
		}
	}
	foreach (array('Model', 'model', 'MPN', 'mpn', 'Model Number', 'Part Number', 'SKU', 'Sku') as $k) {
		if (!empty($specs[$k])) {
			$v = strtoupper(preg_replace('/[^A-Z0-9\-]/', '', (string) $specs[$k]));
			if (strlen($v) >= 3) {
				return $v;
			}
		}
	}
	$title = (string) ($row['title'] ?? '');
	if (preg_match('/\b([A-Z]{1,3}[-]?[0-9]{3,}[A-Z0-9\-]*)\b/u', $title, $m)) {
		return strtoupper($m[1]);
	}
	if (preg_match('/\b(CU\d{4,}|WH-\d{4}[A-Z0-9\-]*|SM-[A-Z0-9]+|PS5|PS4|XBOX\s*SERIES\s*[XS]|RTX\s*\d{4})\b/iu', $title, $m)) {
		return strtoupper(preg_replace('/\s+/', '', $m[1]));
	}
	return '';
}

/**
 * Product identity key per industry — stable across market sources.
 *
 * @param array<string,mixed> $row
 */
function epc_disc_product_identity_key(array $row, string $industry = 'general_retail'): string
{
	if ($industry === 'auto_parts') {
		$baKey = epc_apai_queue_brand_article_key($row);
		if ($baKey !== '') {
			return $baKey;
		}
	}
	$specs = is_array($row['specs'] ?? null) ? $row['specs'] : array();
	if (!$specs) {
		$specs = json_decode((string) ($row['specs_json'] ?? ''), true);
		if (!is_array($specs)) {
			$specs = array();
		}
	}
	foreach (array('EAN', 'GTIN', 'ean', 'UPC', 'upc') as $k) {
		if (!empty($specs[$k])) {
			$ean = preg_replace('/\D/', '', (string) $specs[$k]);
			if (strlen($ean) >= 8) {
				return 'ean:' . $ean;
			}
		}
	}
	$model = epc_disc_extract_model_number($row);
	if ($model !== '') {
		$brand = epc_disc_extract_brand_from_title((string) ($row['title'] ?? ''));
		if ($brand === '' && !empty($specs['brand'])) {
			$brand = strtolower(trim((string) $specs['brand']));
		}
		return 'model:' . ($brand !== '' ? $brand : 'unknown') . ':' . strtolower($model);
	}
	$sku = epc_disc_queue_extract_sku($row);
	if ($sku !== '') {
		return 'sku:' . $sku;
	}
	$extSku = trim((string) ($row['external_sku'] ?? ''));
	if ($extSku !== '') {
		return 'sku:' . strtolower($extSku);
	}
	$core = epc_disc_normalize_title_identity((string) ($row['title'] ?? ''));
	if ($core !== '') {
		return 'title:' . substr(md5($core), 0, 16);
	}
	return '';
}

/**
 * Primary industry source domain for alternate-channel badges.
 */
function epc_disc_industry_main_source(string $industry): string
{
	if ($industry === 'auto_parts') {
		return 'spare247.com';
	}
	if ($industry === 'electronics') {
		return 'noon.com';
	}
	return '';
}

/**
 * Resolve tenant warehouse / catalogue prices for a product identity.
 *
 * @param array<string,mixed> $row
 * @return array{warehouse:float,catalogue:float,your_price:float,compare_type:string,in_warehouse:bool,product_id:int}
 */
function epc_disc_resolve_your_prices(PDO $pdo, string $siteKey, string $identityKey, array $row = array()): array
{
	$out = array(
		'warehouse' => 0.0,
		'catalogue' => 0.0,
		'your_price' => 0.0,
		'compare_type' => '',
		'in_warehouse' => false,
		'product_id' => 0,
	);
	$tenantCfg = epc_ape_tenant_config_get($pdo, $siteKey);
	$profile = (string) ($tenantCfg['profile'] ?? 'warehouse_supplier');
	$productId = (int) ($row['product_id'] ?? 0);
	$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
	if (!is_array($meta)) {
		$meta = array();
	}
	if ($productId <= 0) {
		$productId = (int) ($meta['catalogue_product_id'] ?? 0);
	}
	if ($productId <= 0 && strpos($identityKey, ':') !== false && strpos($identityKey, 'title:') !== 0 && strpos($identityKey, 'sku:') !== 0 && strpos($identityKey, 'model:') !== 0 && strpos($identityKey, 'ean:') !== 0) {
		$productId = epc_disc_find_catalogue_by_brand_article($pdo, $identityKey);
	}
	if ($productId <= 0 && !empty($row['title'])) {
		$productId = epc_disc_find_catalogue_by_title($pdo, (string) $row['title']);
	}
	$out['product_id'] = $productId;
	if ($productId > 0) {
		$cp = epc_disc_catalogue_prices($pdo, $siteKey, $productId);
		$out['catalogue'] = (float) ($cp['sell_price'] ?? 0);
		$out['warehouse'] = (float) ($cp['warehouse_cost'] ?? 0);
	}
	if ($out['warehouse'] <= 0 && strpos($identityKey, ':') !== false && strpos($identityKey, 'title:') !== 0) {
		$parts = explode(':', $identityKey, 2);
		if (count($parts) === 2 && strpos($identityKey, 'model:') !== 0 && strpos($identityKey, 'ean:') !== 0) {
			$whRow = epc_disc_find_warehouse_row_by_brand_article($pdo, epc_apai_normalize_brand($parts[0]), epc_apai_normalize_article($parts[1]));
			if ($whRow && (float) ($whRow['price'] ?? 0) > 0) {
				$out['warehouse'] = (float) $whRow['price'];
				$out['in_warehouse'] = true;
			}
		}
	}
	if ($out['warehouse'] <= 0 && $identityKey !== '') {
		try {
			$whStmt = $pdo->prepare(
				'SELECT MAX(`warehouse_cost`) FROM `epc_price_source_products` WHERE `external_sku` = ? AND `warehouse_cost` > 0'
			);
			$whStmt->execute(array($identityKey));
			$out['warehouse'] = (float) $whStmt->fetchColumn();
		} catch (Throwable $e) {
		}
	}
	if ($profile === 'warehouse_supplier' && $out['warehouse'] > 0) {
		$out['your_price'] = $out['warehouse'];
		$out['compare_type'] = 'warehouse';
		$out['in_warehouse'] = true;
	} elseif ($out['catalogue'] > 0) {
		$out['your_price'] = $out['catalogue'];
		$out['compare_type'] = 'catalogue';
	} elseif ($out['warehouse'] > 0) {
		$out['your_price'] = $out['warehouse'];
		$out['compare_type'] = 'warehouse';
		$out['in_warehouse'] = true;
	}
	return $out;
}

/**
 * Product identity for cross-source price aggregation.
 */
function epc_disc_queue_identity_key(PDO $pdo, string $siteKey, array $row): string
{
	$industry = function_exists('epc_apai_resolve_industry') ? epc_apai_resolve_industry($pdo, $siteKey) : 'general_retail';
	$key = epc_disc_product_identity_key($row, $industry);
	if ($key !== '') {
		return $key;
	}
	$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
	if (is_array($meta) && !empty($meta['identity_key'])) {
		return (string) $meta['identity_key'];
	}
	return '';
}

/**
 * Aggregate min/max source prices for a product identity across discovery queue + source tables.
 *
 * @param array<string,mixed> $rowContext Optional current queue row for primary + alternate_sources
 * @return array{min_price:float,max_price:float,min_source:string,max_source:string,margin_abs:float,margin_pct:float,source_count:int,prices:array,currency:string}
 */
function epc_disc_source_price_range(PDO $pdo, string $siteKey, string $identityKey, array $rowContext = array()): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$identityKey = trim($identityKey);
	$currency = strtoupper(substr((string) ($rowContext['currency'] ?? 'AED'), 0, 8));
	$entries = array();

	$addPrice = function (float $price, string $source, string $cur = 'AED') use (&$entries): void {
		if ($price <= 0 || $source === '') {
			return;
		}
		$entries[] = array('price' => $price, 'source' => $source, 'currency' => strtoupper(substr($cur, 0, 8)));
	};

	if ($rowContext) {
		$primaryDomain = (string) ($rowContext['source_domain'] ?? '');
		$primaryPrice = (float) ($rowContext['suggested_price'] ?? 0);
		if ($primaryPrice > 0 && $primaryDomain !== '') {
			$addPrice($primaryPrice, $primaryDomain, (string) ($rowContext['currency'] ?? $currency));
		}
		$meta = json_decode((string) ($rowContext['meta_json'] ?? ''), true);
		if (!is_array($meta)) {
			$meta = array();
		}
		if (!empty($meta['alternate_sources']) && is_array($meta['alternate_sources'])) {
			foreach ($meta['alternate_sources'] as $alt) {
				if (!is_array($alt)) {
					continue;
				}
				$addPrice((float) ($alt['price'] ?? 0), (string) ($alt['source_domain'] ?? ''), (string) ($alt['currency'] ?? $currency));
			}
		}
		if (!empty($meta['source_prices']) && is_array($meta['source_prices'])) {
			foreach ($meta['source_prices'] as $sp) {
				if (!is_array($sp)) {
					continue;
				}
				$addPrice((float) ($sp['price'] ?? 0), (string) ($sp['source'] ?? $sp['source_domain'] ?? ''), (string) ($sp['currency'] ?? $currency));
			}
		}
	}

	if ($identityKey !== '') {
		$isBrandArticle = strpos($identityKey, ':') !== false
			&& strpos($identityKey, 'title:') !== 0
			&& strpos($identityKey, 'sku:') !== 0
			&& strpos($identityKey, 'model:') !== 0
			&& strpos($identityKey, 'ean:') !== 0;
		$identityLike = '%"identity_key":"' . $identityKey . '"%';
		try {
			if ($isBrandArticle) {
				$qStmt = $pdo->prepare(
					'SELECT `suggested_price`, `source_domain`, `currency`, `meta_json`
					 FROM `epc_product_discovery_queue`
					 WHERE `site_key` = ? AND `status` IN (\'suggested\', \'imported\')
					   AND (`specs_json` LIKE ? OR `meta_json` LIKE ? OR `meta_json` LIKE ?)'
				);
				$likeKey = '%"brand_article_key":"' . $identityKey . '"%';
				$qStmt->execute(array($siteKey, $likeKey, $likeKey, $identityLike));
			} else {
				$qStmt = $pdo->prepare(
					'SELECT `suggested_price`, `source_domain`, `currency`, `meta_json`
					 FROM `epc_product_discovery_queue`
					 WHERE `site_key` = ? AND `status` IN (\'suggested\', \'imported\')
					   AND (`meta_json` LIKE ? OR `specs_json` LIKE ?)'
				);
				$qStmt->execute(array($siteKey, $identityLike, $identityLike));
			}
			foreach ($qStmt->fetchAll(PDO::FETCH_ASSOC) ?: array() as $qRow) {
				$addPrice((float) ($qRow['suggested_price'] ?? 0), (string) ($qRow['source_domain'] ?? ''), (string) ($qRow['currency'] ?? $currency));
				$qMeta = json_decode((string) ($qRow['meta_json'] ?? ''), true);
				if (!is_array($qMeta)) {
					continue;
				}
				if (!empty($qMeta['alternate_sources'])) {
					foreach ((array) $qMeta['alternate_sources'] as $alt) {
						if (!is_array($alt)) {
							continue;
						}
						$addPrice((float) ($alt['price'] ?? 0), (string) ($alt['source_domain'] ?? ''), (string) ($alt['currency'] ?? $currency));
					}
				}
				if (!empty($qMeta['source_prices']) && is_array($qMeta['source_prices'])) {
					foreach ($qMeta['source_prices'] as $sp) {
						if (!is_array($sp)) {
							continue;
						}
						$addPrice((float) ($sp['price'] ?? 0), (string) ($sp['source'] ?? $sp['source_domain'] ?? ''), (string) ($sp['currency'] ?? $currency));
					}
				}
			}
		} catch (Throwable $e) {
		}

		try {
			$srcStmt = $pdo->prepare(
				'SELECT sp.`last_price`, sp.`meta_json`, sp.`warehouse_cost`, ps.`name` AS `source_name`
				 FROM `epc_price_source_products` sp
				 INNER JOIN `epc_price_sources` ps ON ps.`id` = sp.`source_id`
				 WHERE ps.`site_key` = ? AND (sp.`external_sku` = ? OR sp.`meta_json` LIKE ?) AND sp.`last_price` > 0'
			);
			$srcStmt->execute(array($siteKey, $identityKey, $identityLike));
			foreach ($srcStmt->fetchAll(PDO::FETCH_ASSOC) ?: array() as $srcRow) {
				$srcName = (string) ($srcRow['source_name'] ?? 'source');
				$addPrice((float) ($srcRow['last_price'] ?? 0), $srcName, $currency);
				$srcMeta = json_decode((string) ($srcRow['meta_json'] ?? ''), true);
				if (is_array($srcMeta) && !empty($srcMeta['source_prices']) && is_array($srcMeta['source_prices'])) {
					foreach ($srcMeta['source_prices'] as $sp) {
						if (!is_array($sp)) {
							continue;
						}
						$addPrice((float) ($sp['price'] ?? 0), (string) ($sp['source'] ?? $sp['source_domain'] ?? $srcName), (string) ($sp['currency'] ?? $currency));
					}
				}
			}
		} catch (Throwable $e) {
		}

		try {
			$pspStmt = $pdo->prepare(
				'SELECT `price`, `source_domain`, `currency`, `specs_json`
				 FROM `epc_product_source_prices`
				 WHERE `site_key` = ? AND `price` > 0 AND (`specs_json` LIKE ? OR `specs_json` LIKE ?)'
			);
			$pspStmt->execute(array(
				$siteKey,
				'%"brand_article_key":"' . $identityKey . '"%',
				$identityLike,
			));
			foreach ($pspStmt->fetchAll(PDO::FETCH_ASSOC) ?: array() as $pspRow) {
				$addPrice((float) ($pspRow['price'] ?? 0), (string) ($pspRow['source_domain'] ?? ''), (string) ($pspRow['currency'] ?? $currency));
			}
		} catch (Throwable $e) {
		}
	}

	return epc_disc_compute_split_price_ranges($pdo, $siteKey, $entries, $currency, $rowContext);
}

/**
 * Extract marketplace listing prices from row meta (presence checks / estimates).
 *
 * @param array<string,mixed> $rowContext
 * @return array<int,array{price:float,source:string,currency:string}>
 */
function epc_disc_marketplace_prices_from_row_meta(array $rowContext, string $currency = 'AED'): array
{
	$meta = json_decode((string) ($rowContext['meta_json'] ?? ''), true);
	if (!is_array($meta)) {
		$meta = array();
	}
	$out = array();
	foreach ((array) ($meta['marketplace_presence'] ?? array()) as $key => $p) {
		if (!is_array($p) || empty($p['found'])) {
			continue;
		}
		$price = (float) ($p['price'] ?? 0);
		if ($price <= 0) {
			continue;
		}
		$dom = (string) ($p['domain'] ?? $key);
		$out[] = array('price' => $price, 'source' => $dom, 'currency' => $currency);
	}
	$est = (float) ($meta['estimated_marketplace_price'] ?? 0);
	if ($est > 0 && empty($out)) {
		$out[] = array('price' => $est, 'source' => 'estimate', 'currency' => $currency);
	}
	return $out;
}

/**
 * Split price entries into buy-source vs sell-marketplace ranges (never mix for sell target).
 *
 * @param array<int,array{price:float,source:string,currency:string}> $entries
 * @param array<string,mixed> $rowContext
 * @return array<string,mixed>
 */
function epc_disc_compute_split_price_ranges(PDO $pdo, string $siteKey, array $entries, string $currency = 'AED', array $rowContext = array()): array
{
	if (is_file(__DIR__ . '/epc_apai_marketplace_channels.php')) {
		require_once __DIR__ . '/epc_apai_marketplace_channels.php';
	}

	$buyEntries = array();
	$marketEntries = array();
	foreach ($entries as $e) {
		$source = (string) ($e['source'] ?? '');
		$domain = strtolower(preg_replace('/^www\./', '', $source));
		if ($domain === '' || (float) ($e['price'] ?? 0) <= 0) {
			continue;
		}
		$role = function_exists('epc_apai_source_role') ? epc_apai_source_role($domain, $siteKey, $pdo) : 'buy_source';
		if ($role === 'own_tenant') {
			continue;
		}
		if ($role === 'sell_marketplace') {
			$marketEntries[] = $e;
		} else {
			$buyEntries[] = $e;
		}
	}
	foreach (epc_disc_marketplace_prices_from_row_meta($rowContext, $currency) as $mp) {
		$marketEntries[] = $mp;
	}

	$buyRange = epc_disc_compute_range_from_prices($buyEntries, $currency);
	$marketRange = epc_disc_compute_range_from_prices($marketEntries, $currency);

	$buyMin = (float) ($buyRange['min_price'] ?? 0);
	$buyMax = (float) ($buyRange['max_price'] ?? 0);
	$buyCount = (int) ($buyRange['source_count'] ?? 0);
	$mpMin = (float) ($marketRange['min_price'] ?? 0);
	$mpMax = (float) ($marketRange['max_price'] ?? 0);
	$mpCount = (int) ($marketRange['source_count'] ?? 0);
	$marketListed = $mpCount > 0 && $mpMin > 0;

	$targetSell = $marketListed ? $mpMin : 0.0;
	$marketKnown = $marketListed;
	if (!$marketListed && is_file(__DIR__ . '/epc_apai_marketplace_channels.php')) {
		$meta = json_decode((string) ($rowContext['meta_json'] ?? ''), true);
		if (is_array($meta)) {
			$est = (float) ($meta['estimated_marketplace_price'] ?? 0);
			if ($est > 0) {
				$targetSell = $est;
				$marketKnown = !empty($meta['estimated_marketplace_known']);
			}
		}
	}

	$marginAbs = ($buyMin > 0 && $targetSell > 0) ? round($targetSell - $buyMin, 2) : 0.0;
	$marginPct = ($buyMin > 0 && $marginAbs > 0) ? round(($marginAbs / $buyMin) * 100, 1) : 0.0;

	return array(
		'min_price' => $buyMin,
		'max_price' => $buyMax,
		'min_source' => (string) ($buyRange['min_source'] ?? ''),
		'max_source' => (string) ($buyRange['max_source'] ?? ''),
		'margin_abs' => $marginAbs,
		'margin_pct' => $marginPct,
		'source_count' => $buyCount,
		'market_confirmed' => $buyCount >= 2,
		'single_source' => $buyCount === 1,
		'source_labels' => (array) ($buyRange['source_labels'] ?? array()),
		'prices' => (array) ($buyRange['prices'] ?? array()),
		'currency' => strtoupper(substr($currency, 0, 8)),
		'buy_source_range' => array(
			'min' => $buyMin,
			'max' => $buyMax,
			'min_source' => (string) ($buyRange['min_source'] ?? ''),
			'max_source' => (string) ($buyRange['max_source'] ?? ''),
			'source_count' => $buyCount,
			'source_labels' => (array) ($buyRange['source_labels'] ?? array()),
			'prices' => (array) ($buyRange['prices'] ?? array()),
		),
		'marketplace_range' => array(
			'min' => $mpMin,
			'max' => $mpMax,
			'min_source' => (string) ($marketRange['min_source'] ?? ''),
			'max_source' => (string) ($marketRange['max_source'] ?? ''),
			'source_count' => $mpCount,
			'listed' => $marketListed,
			'source_labels' => (array) ($marketRange['source_labels'] ?? array()),
			'prices' => (array) ($marketRange['prices'] ?? array()),
		),
		'target_sell_price' => $targetSell,
		'marketplace_price' => $targetSell,
		'marketplace_known' => $marketKnown,
		'marketplace_listed' => $marketListed,
	);
}

/**
 * Business advice for Discover pricing cards.
 *
 * @param array<string,mixed> $row
 * @return array{badge:string,message:string,level:string,save_abs:float}
 */
function epc_disc_pricing_advice(array $row, float $yourPrice = 0.0, float $buyMin = 0.0, float $marketplacePrice = 0.0): array
{
	$currency = (string) ($row['currency'] ?? $row['source_price_range']['currency'] ?? 'AED');
	$range = (array) ($row['source_price_range'] ?? array());
	if ($buyMin <= 0) {
		$buyMin = (float) ($range['buy_min'] ?? $range['min'] ?? $range['min_price'] ?? 0);
	}
	if ($marketplacePrice <= 0) {
		$marketplacePrice = (float) ($range['target_sell_price'] ?? $range['marketplace_price'] ?? 0);
	}
	if ($yourPrice <= 0) {
		$yourPrice = (float) ($row['your_price'] ?? $row['catalogue_price'] ?? 0);
	}
	$buyMax = (float) ($range['buy_max'] ?? $range['max'] ?? 0);
	$buyLabels = (array) ($range['buy_source_labels'] ?? $range['source_labels'] ?? array());
	$buyMinSource = (string) ($range['min_source'] ?? '');
	$marketListed = !empty($range['marketplace_listed']);
	$channels = array();
	if (!empty($row['list_on_marketplaces'])) {
		$channels = (array) $row['list_on_marketplaces'];
	}
	$primaryMp = (string) ($row['primary_marketplace'] ?? 'Noon');

	if ($yourPrice > 0 && $buyMin > 0 && $yourPrice > $buyMin * 1.01) {
		$save = round($yourPrice - $buyMin, 2);
		return array(
			'badge' => 'Buy cheaper',
			'message' => 'Save ' . number_format($save, 0) . ' ' . $currency . ' from ' . ($buyMinSource !== '' ? $buyMinSource : 'lowest buy source'),
			'level' => 'warning',
			'save_abs' => $save,
		);
	}
	if ($yourPrice > 0 && $marketplacePrice > 0 && $yourPrice > $marketplacePrice * 1.02) {
		return array(
			'badge' => 'Overpriced',
			'message' => 'Lower to match Noon/Amazon (~' . number_format($marketplacePrice, 0) . ' ' . $currency . ')',
			'level' => 'danger',
			'save_abs' => round($yourPrice - $marketplacePrice, 2),
		);
	}
	if ($yourPrice > 0 && $buyMin > 0 && $yourPrice <= $buyMin) {
		return array(
			'badge' => 'Good cost',
			'message' => 'Your cost is at or below market buy price — margin opportunity',
			'level' => 'success',
			'save_abs' => 0.0,
		);
	}
	if ($buyMin > 0 && !$marketListed && empty($row['arbitrage_opportunity'])) {
		$miss = (array) ($row['missing_marketplaces'] ?? array());
		$mpLabel = $miss ? implode('/', array_slice(array_map(function ($d) {
			return ucfirst(preg_replace('/\.(com|ae|sa|co\.uk)$/', '', $d));
		}, $miss), 0, 2)) : $primaryMp;
		return array(
			'badge' => 'List on ' . ($mpLabel !== '' ? $mpLabel : 'marketplace'),
			'message' => 'Not listed on sell marketplaces — arbitrage gap',
			'level' => 'warning',
			'save_abs' => 0.0,
		);
	}
	if (!empty($row['arbitrage_opportunity'])) {
		return array(
			'badge' => 'List on ' . $primaryMp,
			'message' => 'Buy low · sell on marketplace — import & list',
			'level' => 'success',
			'save_abs' => 0.0,
		);
	}
	if ($buyMin > 0 && $buyMax > $buyMin) {
		$labelStr = $buyLabels ? implode(' · ', array_slice($buyLabels, 0, 4)) : 'buy sources';
		return array(
			'badge' => 'Price range only',
			'message' => 'Buy ' . number_format($buyMin, 0) . '–' . number_format($buyMax, 0) . ' ' . $currency . ' (' . $labelStr . ')',
			'level' => 'info',
			'save_abs' => 0.0,
		);
	}
	if ($buyMin > 0 && !$marketListed) {
		return array(
			'badge' => 'Research needed',
			'message' => 'Not listed on Noon/Amazon — est. marketplace price unknown',
			'level' => 'info',
			'save_abs' => 0.0,
		);
	}
	return array(
		'badge' => '',
		'message' => '',
		'level' => 'info',
		'save_abs' => 0.0,
	);
}

/**
 * @param array<int,array{price:float,source:string,currency:string}> $entries
 * @return array{min_price:float,max_price:float,min_source:string,max_source:string,margin_abs:float,margin_pct:float,source_count:int,prices:array,currency:string}
 */
function epc_disc_compute_range_from_prices(array $entries, string $currency = 'AED'): array
{
	$seen = array();
	$unique = array();
	$byDomain = array();
	foreach ($entries as $e) {
		$source = (string) ($e['source'] ?? '');
		$domain = strtolower(preg_replace('/^www\./', '', $source));
		$price = round((float) ($e['price'] ?? 0), 2);
		if ($price <= 0 || $domain === '') {
			continue;
		}
		if (!isset($byDomain[$domain]) || $price < (float) $byDomain[$domain]['price']) {
			$byDomain[$domain] = array('price' => $price, 'source' => $source, 'currency' => (string) ($e['currency'] ?? $currency));
		}
	}
	foreach ($byDomain as $domain => $e) {
		$key = $domain . ':' . round((float) ($e['price'] ?? 0), 2);
		if (isset($seen[$key])) {
			continue;
		}
		$seen[$key] = true;
		$unique[] = $e;
	}
	$sourceCount = count($byDomain);
	$minPrice = 0.0;
	$maxPrice = 0.0;
	$minSource = '';
	$maxSource = '';
	foreach ($unique as $e) {
		$p = (float) ($e['price'] ?? 0);
		$s = (string) ($e['source'] ?? '');
		if ($p <= 0) {
			continue;
		}
		if ($minPrice <= 0 || $p < $minPrice) {
			$minPrice = $p;
			$minSource = $s;
		}
		if ($p > $maxPrice) {
			$maxPrice = $p;
			$maxSource = $s;
		}
	}
	$marketConfirmed = $sourceCount >= 2;
	if (!$marketConfirmed) {
		$maxPrice = 0.0;
		$maxSource = '';
	}
	$marginAbs = ($marketConfirmed && $minPrice > 0 && $maxPrice > 0) ? round($maxPrice - $minPrice, 2) : 0.0;
	$marginPct = ($marketConfirmed && $minPrice > 0 && $marginAbs > 0) ? round(($marginAbs / $minPrice) * 100, 1) : 0.0;
	$sourceLabels = array_keys($byDomain);
	sort($sourceLabels);
	return array(
		'min_price' => $minPrice,
		'max_price' => $maxPrice,
		'min_source' => $minSource,
		'max_source' => $maxSource,
		'margin_abs' => $marginAbs,
		'margin_pct' => $marginPct,
		'source_count' => $sourceCount,
		'market_confirmed' => $marketConfirmed,
		'single_source' => $sourceCount === 1,
		'source_labels' => $sourceLabels,
		'prices' => $unique,
		'currency' => strtoupper(substr($currency, 0, 8)),
	);
}

/**
 * Build source_price_range for a discovery queue row and return compact storage format.
 *
 * @param array<string,mixed> $row
 * @return array<string,mixed>
 */
function epc_disc_build_source_price_range(PDO $pdo, string $siteKey, array $row): array
{
	$identityKey = epc_disc_queue_identity_key($pdo, $siteKey, $row);
	$range = epc_disc_source_price_range($pdo, $siteKey, $identityKey, $row);
	$buyRange = (array) ($range['buy_source_range'] ?? array());
	$mpRange = (array) ($range['marketplace_range'] ?? array());
	$compact = array(
		'min' => (float) ($buyRange['min'] ?? $range['min_price'] ?? 0),
		'max' => (float) ($buyRange['max'] ?? $range['max_price'] ?? 0),
		'min_source' => (string) ($buyRange['min_source'] ?? $range['min_source'] ?? ''),
		'max_source' => (string) ($buyRange['max_source'] ?? $range['max_source'] ?? ''),
		'buy_min' => (float) ($buyRange['min'] ?? $range['min_price'] ?? 0),
		'buy_max' => (float) ($buyRange['max'] ?? $range['max_price'] ?? 0),
		'buy_source_count' => (int) ($buyRange['source_count'] ?? $range['source_count'] ?? 0),
		'buy_source_labels' => (array) ($buyRange['source_labels'] ?? $range['source_labels'] ?? array()),
		'buy_source_range' => $buyRange,
		'marketplace_min' => (float) ($mpRange['min'] ?? 0),
		'marketplace_max' => (float) ($mpRange['max'] ?? 0),
		'marketplace_range' => $mpRange,
		'target_sell_price' => (float) ($range['target_sell_price'] ?? 0),
		'marketplace_price' => (float) ($range['marketplace_price'] ?? 0),
		'marketplace_known' => !empty($range['marketplace_known']),
		'marketplace_listed' => !empty($range['marketplace_listed']),
		'margin_abs' => (float) ($range['margin_abs'] ?? 0),
		'margin_pct' => (float) ($range['margin_pct'] ?? 0),
		'source_count' => (int) ($buyRange['source_count'] ?? $range['source_count'] ?? 0),
		'market_confirmed' => !empty($range['market_confirmed']),
		'single_source' => !empty($range['single_source']),
		'source_labels' => (array) ($buyRange['source_labels'] ?? $range['source_labels'] ?? array()),
		'currency' => (string) ($range['currency'] ?? 'AED'),
		'updated_at' => date('c'),
		'identity_key' => $identityKey,
	);
	$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
	$adviceRow = array_merge($row, array('source_price_range' => $compact));
	if (is_array($meta)) {
		$adviceRow = array_merge($adviceRow, array(
			'arbitrage_opportunity' => !empty($meta['arbitrage_opportunity']),
			'missing_marketplaces' => (array) ($meta['missing_marketplaces'] ?? array()),
			'primary_marketplace' => (string) ($meta['primary_marketplace'] ?? ''),
		));
	}
	$compact['pricing_advice'] = epc_disc_pricing_advice(
		$adviceRow,
		(float) ($row['your_price'] ?? 0),
		(float) $compact['buy_min'],
		(float) $compact['target_sell_price']
	);
	return $compact;
}

/**
 * @param array<string,mixed> $meta
 * @param array<string,mixed> $rangeCompact
 */
function epc_disc_persist_source_price_range_meta(array &$meta, array $rangeCompact): void
{
	if ((float) ($rangeCompact['min'] ?? 0) <= 0) {
		return;
	}
	$meta['source_price_range'] = $rangeCompact;
}

/**
 * Default Discover sub-tab for tenant industry / profile.
 */
function epc_disc_default_discover_view(PDO $pdo, string $siteKey): string
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$industry = function_exists('epc_apai_resolve_industry') ? epc_apai_resolve_industry($pdo, $siteKey) : 'general_retail';
	// Auto parts operators care first about catalogue vs live market — not empty arbitrage gaps.
	if ($industry === 'auto_parts') {
		return 'catalogue_match';
	}
	if (is_file(__DIR__ . '/epc_apai_marketplace_channels.php')) {
		require_once __DIR__ . '/epc_apai_marketplace_channels.php';
		if (function_exists('epc_apai_marketplace_arbitrage_enabled') && epc_apai_marketplace_arbitrage_enabled($pdo, $siteKey)) {
			return 'marketplace_opportunities';
		}
	}
	if ($industry === 'tax_advisory') {
		return 'all_suggestions';
	}
	return 'all_suggestions';
}

/**
 * Normalize Discover tab filter params with smart defaults and empty-view fallback.
 *
 * @param array<string,mixed> $raw taxonomy_id, view, disc_sort|sort, limit, warehouse_only, show_all
 * @return array<string,mixed>
 */
function epc_disc_default_discover_filters(PDO $pdo, string $siteKey, array $raw = array()): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$taxonomyId = max(0, (int) ($raw['taxonomy_id'] ?? 0));
	$limit = max(1, min(200, (int) ($raw['limit'] ?? 20)));
	$sort = (string) ($raw['sort'] ?? $raw['disc_sort'] ?? 'newest');
	if (!in_array($sort, array('newest', 'price_change', 'last_updated'), true)) {
		$sort = 'newest';
	}

	$viewExplicit = isset($raw['view']) && trim((string) $raw['view']) !== '';
	$view = trim((string) ($raw['view'] ?? $raw['visibility'] ?? $raw['disc_visibility'] ?? ''));
	if (isset($raw['show_all']) && ($raw['show_all'] === '1' || $raw['show_all'] === 1 || $raw['show_all'] === true)) {
		$view = 'all_suggestions';
		$viewExplicit = true;
	}
	if ($view === '' || $view === 'show_all' || $view === 'new') {
		$view = epc_disc_default_discover_view($pdo, $siteKey);
		$viewExplicit = false;
	}
	$validViews = array('market_confirmed', 'all_suggestions', 'price_changes', 'catalogue_match', 'marketplace_opportunities');
	if (!in_array($view, $validViews, true)) {
		$view = epc_disc_default_discover_view($pdo, $siteKey);
	}

	$warehouseOnly = false;
	if (isset($raw['warehouse_only'])) {
		$wo = $raw['warehouse_only'];
		$warehouseOnly = ($wo === '1' || $wo === 1 || $wo === true);
	}

	$filters = array(
		'taxonomy_id' => $taxonomyId,
		'view' => $view,
		'sort' => $sort,
		'limit' => $limit,
		'warehouse_only' => $warehouseOnly,
		'view_explicit' => $viewExplicit,
	);

	// Auto-fallback: default view with 0 results but alternatives exist.
	if (!$viewExplicit) {
		$probe = array_merge($filters, array('limit' => 1));
		$curCount = count(epc_disc_queue_list_for_discover($pdo, $siteKey, $probe));
		if ($curCount === 0) {
			$fallbackOrder = array();
			if ($view === 'marketplace_opportunities') {
				$fallbackOrder = array('catalogue_match', 'all_suggestions', 'market_confirmed');
			} elseif ($view === 'market_confirmed') {
				$fallbackOrder = array('catalogue_match', 'all_suggestions');
			} elseif ($view === 'catalogue_match') {
				$fallbackOrder = array('all_suggestions', 'market_confirmed', 'marketplace_opportunities');
			}
			foreach ($fallbackOrder as $fbView) {
				$fbProbe = array_merge($filters, array('view' => $fbView, 'limit' => 1));
				if (count(epc_disc_queue_list_for_discover($pdo, $siteKey, $fbProbe)) > 0) {
					$filters['view'] = $fbView;
					$filters['fallback_from'] = $view;
					$filters['auto_fallback'] = true;
					break;
				}
			}
		}
	}

	return $filters;
}

/**
 * Extract spare247 reference price from matched_sources meta.
 *
 * @param array<int,array<string,mixed>> $matchedSources
 */
function epc_disc_spare247_price_from_sources(array $matchedSources): float
{
	foreach ($matchedSources as $src) {
		if (!is_array($src)) {
			continue;
		}
		$domain = strtolower((string) ($src['source_domain'] ?? ''));
		if ($domain !== '' && strpos($domain, 'spare247') !== false) {
			return (float) ($src['price'] ?? 0);
		}
	}
	return 0.0;
}

/**
 * Cross-source match pass — group queue rows by product identity, count distinct sources,
 * set market_confirmed when source_match_count >= 2.
 *
 * @return array{ok:bool,updated:int,groups:int,market_confirmed:int,message:string}
 */
function epc_disc_cross_source_match(PDO $pdo, string $siteKey): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$industry = function_exists('epc_apai_resolve_industry') ? epc_apai_resolve_industry($pdo, $siteKey) : 'general_retail';
	$now = time();
	$stmt = $pdo->prepare(
		'SELECT * FROM `epc_product_discovery_queue`
		 WHERE `site_key` = ? AND `status` IN (\'suggested\', \'imported\')
		 ORDER BY `updated_at` DESC LIMIT 500'
	);
	$stmt->execute(array($siteKey));
	$rows = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();

	$groups = array();
	foreach ($rows as $row) {
		$identityKey = epc_disc_queue_identity_key($pdo, $siteKey, $row);
		if ($identityKey === '') {
			continue;
		}
		if (!isset($groups[$identityKey])) {
			$groups[$identityKey] = array();
		}
		$groups[$identityKey][] = $row;
	}

	$updated = 0;
	$confirmedGroups = 0;
	$updStmt = $pdo->prepare(
		'UPDATE `epc_product_discovery_queue` SET `meta_json` = ?, `updated_at` = ? WHERE `id` = ? AND `site_key` = ?'
	);

	foreach ($groups as $identityKey => $groupRows) {
		$sourceMap = array();
		$collectSources = function (array $row) use (&$sourceMap): void {
			$domain = strtolower(preg_replace('/^www\./', '', trim((string) ($row['source_domain'] ?? ''))));
			$price = (float) ($row['suggested_price'] ?? 0);
			if ($domain !== '' && $price > 0) {
				if (!isset($sourceMap[$domain]) || $price < (float) $sourceMap[$domain]['price']) {
					$sourceMap[$domain] = array(
						'price' => $price,
						'currency' => (string) ($row['currency'] ?? 'AED'),
						'source_url' => (string) ($row['source_url'] ?? ''),
						'row_id' => (int) ($row['id'] ?? 0),
					);
				}
			}
			$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
			if (!is_array($meta)) {
				return;
			}
			foreach (array('alternate_sources', 'source_prices') as $field) {
				if (empty($meta[$field]) || !is_array($meta[$field])) {
					continue;
				}
				foreach ($meta[$field] as $alt) {
					if (!is_array($alt)) {
						continue;
					}
					$altDomain = strtolower(preg_replace('/^www\./', '', trim((string) ($alt['source_domain'] ?? $alt['source'] ?? ''))));
					$altPrice = (float) ($alt['price'] ?? 0);
					if ($altDomain === '' || $altPrice <= 0) {
						continue;
					}
					if (!isset($sourceMap[$altDomain]) || $altPrice < (float) $sourceMap[$altDomain]['price']) {
						$sourceMap[$altDomain] = array(
							'price' => $altPrice,
							'currency' => (string) ($alt['currency'] ?? 'AED'),
							'source_url' => (string) ($alt['source_url'] ?? ''),
							'row_id' => (int) ($row['id'] ?? 0),
						);
					}
				}
			}
		};
		foreach ($groupRows as $row) {
			$collectSources($row);
		}
		$sourceCount = count($sourceMap);
		$matchedSources = array();
		foreach ($sourceMap as $domain => $info) {
			$matchedSources[] = array(
				'source_domain' => $domain,
				'price' => (float) ($info['price'] ?? 0),
				'currency' => (string) ($info['currency'] ?? 'AED'),
				'source_url' => (string) ($info['source_url'] ?? ''),
			);
		}
		usort($matchedSources, function ($a, $b) {
			return ((float) ($a['price'] ?? 0)) <=> ((float) ($b['price'] ?? 0));
		});
		$marketConfirmed = $sourceCount >= 2;
		if ($marketConfirmed) {
			$confirmedGroups++;
		}
		$spare247Price = epc_disc_spare247_price_from_sources($matchedSources);
		$baKey = epc_apai_queue_brand_article_key($groupRows[0]);
		$sourcePrices = array();
		foreach ($matchedSources as $ms) {
			$sourcePrices[] = array(
				'source' => (string) ($ms['source_domain'] ?? ''),
				'source_domain' => (string) ($ms['source_domain'] ?? ''),
				'price' => (float) ($ms['price'] ?? 0),
				'currency' => (string) ($ms['currency'] ?? 'AED'),
				'source_url' => (string) ($ms['source_url'] ?? ''),
			);
		}

		foreach ($groupRows as $row) {
			$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
			if (!is_array($meta)) {
				$meta = array();
			}
			$meta['identity_key'] = $identityKey;
			$meta['market_confirmed'] = $marketConfirmed;
			$meta['source_match_count'] = $sourceCount;
			$meta['matched_sources'] = $matchedSources;
			$meta['source_prices'] = $sourcePrices;
			if ($baKey !== '') {
				$meta['brand_article_key'] = $baKey;
			}
			if ($spare247Price > 0) {
				$meta['spare247_price'] = $spare247Price;
				$meta['spare247_reference'] = true;
			} else {
				unset($meta['spare247_price'], $meta['spare247_reference']);
			}
			$rowDomain = strtolower(preg_replace('/^www\./', '', trim((string) ($row['source_domain'] ?? ''))));
			$alts = array();
			foreach ($matchedSources as $ms) {
				$msDomain = strtolower((string) ($ms['source_domain'] ?? ''));
				if ($msDomain !== '' && $msDomain !== $rowDomain) {
					$alts[] = array(
						'source_domain' => $msDomain,
						'price' => (float) ($ms['price'] ?? 0),
						'currency' => (string) ($ms['currency'] ?? 'AED'),
						'source_url' => (string) ($ms['source_url'] ?? ''),
					);
				}
			}
			if ($alts) {
				$meta['alternate_sources'] = $alts;
			}
			$mainSource = epc_disc_industry_main_source($industry);
			if ($mainSource !== '' && $marketConfirmed) {
				$onMain = isset($sourceMap[$mainSource]);
				$meta['on_main_source'] = $onMain;
				if (!$onMain && $sourceCount >= 1) {
					$cheapest = $matchedSources[0] ?? null;
					$meta['alternate_channel'] = array(
						'main_source' => $mainSource,
						'buy_source' => (string) ($cheapest['source_domain'] ?? ''),
						'buy_price' => (float) ($cheapest['price'] ?? 0),
					);
				}
			}
			$rowCtx = array_merge($row, array('meta_json' => json_encode($meta, JSON_UNESCAPED_UNICODE)));
			$rangeCompact = epc_disc_build_source_price_range($pdo, $siteKey, $rowCtx);
			epc_disc_persist_source_price_range_meta($meta, $rangeCompact);
			$meta['cross_match_at'] = $now;
			$metaJson = json_encode($meta, JSON_UNESCAPED_UNICODE);
			$updStmt->execute(array($metaJson, $now, (int) ($row['id'] ?? 0), $siteKey));
			$updated++;
		}
	}

	return array(
		'ok' => true,
		'updated' => $updated,
		'groups' => count($groups),
		'market_confirmed' => $confirmedGroups,
		'message' => "Cross-source match: {$updated} row(s), {$confirmedGroups} market-confirmed group(s)",
	);
}

/**
 * Merge duplicate identity rows into one canonical card; hide duplicates from Discover.
 *
 * @return array{ok:bool,merged:int,canonical:int,message:string}
 */
function epc_disc_merge_cross_source_prices(PDO $pdo, string $siteKey): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	epc_disc_cross_source_match($pdo, $siteKey);
	$now = time();
	$stmt = $pdo->prepare(
		'SELECT * FROM `epc_product_discovery_queue`
		 WHERE `site_key` = ? AND `status` = \'suggested\'
		 ORDER BY `updated_at` DESC LIMIT 500'
	);
	$stmt->execute(array($siteKey));
	$rows = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
	$groups = array();
	foreach ($rows as $row) {
		$identityKey = epc_disc_queue_identity_key($pdo, $siteKey, $row);
		if ($identityKey === '') {
			continue;
		}
		if (!isset($groups[$identityKey])) {
			$groups[$identityKey] = array();
		}
		$groups[$identityKey][] = $row;
	}
	$updStmt = $pdo->prepare(
		'UPDATE `epc_product_discovery_queue` SET `meta_json` = ?, `updated_at` = ? WHERE `id` = ? AND `site_key` = ?'
	);
	$merged = 0;
	$canonicalCount = 0;
	foreach ($groups as $identityKey => $groupRows) {
		if (count($groupRows) < 2) {
			continue;
		}
		usort($groupRows, function ($a, $b) {
			$ma = json_decode((string) ($a['meta_json'] ?? ''), true);
			$mb = json_decode((string) ($b['meta_json'] ?? ''), true);
			$ca = is_array($ma) ? (int) ($ma['source_match_count'] ?? 0) : 0;
			$cb = is_array($mb) ? (int) ($mb['source_match_count'] ?? 0) : 0;
			if ($ca !== $cb) {
				return $cb <=> $ca;
			}
			return (int) ($b['updated_at'] ?? 0) <=> (int) ($a['updated_at'] ?? 0);
		});
		$canonical = $groupRows[0];
		$canonicalId = (int) ($canonical['id'] ?? 0);
		if ($canonicalId <= 0) {
			continue;
		}
		$canonicalCount++;
		$range = epc_disc_source_price_range($pdo, $siteKey, $identityKey, $canonical);
		$meta = json_decode((string) ($canonical['meta_json'] ?? ''), true);
		if (!is_array($meta)) {
			$meta = array();
		}
		$meta['identity_key'] = $identityKey;
		$meta['canonical_queue_id'] = $canonicalId;
		$meta['is_canonical'] = true;
		$sourcePrices = array();
		foreach ((array) ($range['prices'] ?? array()) as $pe) {
			$sourcePrices[] = array(
				'source' => (string) ($pe['source'] ?? ''),
				'source_domain' => (string) ($pe['source'] ?? ''),
				'price' => (float) ($pe['price'] ?? 0),
				'currency' => (string) ($pe['currency'] ?? $range['currency'] ?? 'AED'),
			);
		}
		$meta['source_prices'] = $sourcePrices;
		$meta['matched_sources'] = $sourcePrices;
		$meta['source_match_count'] = (int) ($range['source_count'] ?? 0);
		$meta['market_confirmed'] = !empty($range['market_confirmed']);
		$rangeCompact = epc_disc_build_source_price_range($pdo, $siteKey, array_merge($canonical, array('meta_json' => json_encode($meta, JSON_UNESCAPED_UNICODE))));
		epc_disc_persist_source_price_range_meta($meta, $rangeCompact);
		$updStmt->execute(array(json_encode($meta, JSON_UNESCAPED_UNICODE), $now, $canonicalId, $siteKey));
		for ($i = 1; $i < count($groupRows); $i++) {
			$dupRow = $groupRows[$i];
			$dupId = (int) ($dupRow['id'] ?? 0);
			if ($dupId <= 0 || $dupId === $canonicalId) {
				continue;
			}
			$dupMeta = json_decode((string) ($dupRow['meta_json'] ?? ''), true);
			if (!is_array($dupMeta)) {
				$dupMeta = array();
			}
			$dupMeta['identity_key'] = $identityKey;
			$dupMeta['duplicate_of'] = $canonicalId;
			$dupMeta['merged_at'] = $now;
			unset($dupMeta['is_canonical']);
			$updStmt->execute(array(json_encode($dupMeta, JSON_UNESCAPED_UNICODE), $now, $dupId, $siteKey));
			$merged++;
		}
	}
	return array(
		'ok' => true,
		'merged' => $merged,
		'canonical' => $canonicalCount,
		'message' => "Merged {$merged} duplicate row(s) into {$canonicalCount} canonical card(s)",
	);
}

/**
 * Match tenant catalogue / warehouse products to live market prices across sources.
 *
 * @param array<string,mixed> $opts Optional: limit (int) caps matched rows before final sort
 * @return array{ok:bool,items:array,count:int,message:string}
 */
function epc_disc_match_catalogue_to_market(PDO $pdo, string $siteKey, array $opts = array()): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$seen = array();
	$items = array();
	$hardLimit = max(0, (int) ($opts['limit'] ?? 0));

	$addMatch = function (int $productId, string $baKey, string $brand, string $article, string $title) use ($pdo, $siteKey, &$seen, &$items, $hardLimit): void {
		if ($hardLimit > 0 && count($items) >= $hardLimit) {
			return;
		}
		if ($productId <= 0 || $baKey === '' || isset($seen[$baKey])) {
			return;
		}
		$seen[$baKey] = true;
		$range = epc_disc_source_price_range($pdo, $siteKey, $baKey);
		$catPrices = epc_disc_catalogue_prices($pdo, $siteKey, $productId);
		$yourPrice = (float) ($catPrices['sell_price'] ?? 0);
		if ($yourPrice <= 0) {
			$yourPrice = (float) ($catPrices['warehouse_cost'] ?? 0);
		}
		$marketMin = (float) ($range['min_price'] ?? 0);
		$marketMax = (float) ($range['max_price'] ?? 0);
		$sourceCount = (int) ($range['source_count'] ?? 0);
		$spare247Price = 0.0;
		foreach ((array) ($range['prices'] ?? array()) as $pe) {
			$dom = strtolower((string) ($pe['source'] ?? ''));
			if ($dom !== '' && strpos($dom, 'spare247') !== false) {
				$spare247Price = (float) ($pe['price'] ?? 0);
				break;
			}
		}
		$marginFlag = 'no_market';
		if ($marketMin > 0 && $yourPrice > 0) {
			if ($yourPrice > $marketMax * 1.05 && $marketMax > 0) {
				$marginFlag = 'overpriced';
			} elseif ($yourPrice < $marketMin * 0.95) {
				$marginFlag = 'underpriced';
			} else {
				$marginFlag = 'good_margin';
			}
		} elseif ($sourceCount >= 2) {
			$marginFlag = 'market_only';
		}
		$diffPct = 0.0;
		if ($yourPrice > 0 && $marketMin > 0) {
			$diffPct = round((($yourPrice - $marketMin) / $marketMin) * 100, 1);
		}
		$items[] = array(
			'product_id' => $productId,
			'brand_article_key' => $baKey,
			'brand' => $brand,
			'article_number' => $article,
			'title' => $title,
			'your_price' => $yourPrice,
			'market_min' => $marketMin,
			'market_max' => $marketMax,
			'source_match_count' => $sourceCount,
			'market_confirmed' => $sourceCount >= 2,
			'spare247_price' => $spare247Price,
			'margin_flag' => $marginFlag,
			'price_diff_pct' => $diffPct,
			'currency' => (string) ($range['currency'] ?? 'AED'),
			'source_price_range' => array(
				'min' => $marketMin,
				'max' => $marketMax,
				'source_count' => $sourceCount,
				'currency' => (string) ($range['currency'] ?? 'AED'),
			),
		);
	};

	try {
		$stmt = $pdo->prepare(
			'SELECT sp.`product_id`, sp.`external_sku`, scp.`caption`
			 FROM `epc_price_source_products` sp
			 INNER JOIN `shop_catalogue_products` scp ON scp.`id` = sp.`product_id` AND scp.`published_flag` = 1
			 WHERE sp.`product_id` > 0 AND sp.`external_sku` LIKE \'%:%\''
		);
		$stmt->execute();
		foreach ($stmt->fetchAll(PDO::FETCH_ASSOC) ?: array() as $row) {
			if ($hardLimit > 0 && count($items) >= $hardLimit) {
				break;
			}
			$baKey = strtolower(trim((string) ($row['external_sku'] ?? '')));
			$parts = explode(':', $baKey, 2);
			$addMatch(
				(int) ($row['product_id'] ?? 0),
				$baKey,
				epc_apai_normalize_brand($parts[0] ?? ''),
				epc_apai_normalize_article($parts[1] ?? ''),
				(string) ($row['caption'] ?? '')
			);
		}
	} catch (Throwable $e) {
	}

	try {
		if ($hardLimit <= 0 || count($items) < $hardLimit) {
			$stmt = $pdo->query(
				'SELECT `manufacturer`, `article`, `name`
				 FROM `shop_docpart_prices_data`
				 WHERE `article` IS NOT NULL AND `article` != \'\'
				 GROUP BY UPPER(REPLACE(REPLACE(`article`, \' \', \'\'), \'-\', \'\')), LOWER(`manufacturer`)
				 LIMIT 300'
			);
			foreach ($stmt->fetchAll(PDO::FETCH_ASSOC) ?: array() as $row) {
				if ($hardLimit > 0 && count($items) >= $hardLimit) {
					break;
				}
				$brand = epc_apai_normalize_brand((string) ($row['manufacturer'] ?? ''));
				$article = epc_apai_normalize_article((string) ($row['article'] ?? ''));
				$baKey = epc_apai_brand_article_key($brand, $article);
				if ($baKey === '') {
					continue;
				}
				$productId = epc_disc_find_catalogue_by_brand_article($pdo, $baKey);
				if ($productId <= 0) {
					continue;
				}
				$addMatch($productId, $baKey, $brand, $article, (string) ($row['name'] ?? ''));
			}
		}
	} catch (Throwable $e) {
	}

	usort($items, function ($a, $b) {
		$ca = !empty($a['market_confirmed']) ? 1 : 0;
		$cb = !empty($b['market_confirmed']) ? 1 : 0;
		if ($ca !== $cb) {
			return $cb <=> $ca;
		}
		return (int) ($b['source_match_count'] ?? 0) <=> (int) ($a['source_match_count'] ?? 0);
	});

	return array(
		'ok' => true,
		'items' => $items,
		'count' => count($items),
		'message' => count($items) . ' catalogue product(s) matched to market',
	);
}

/** @return array<string,string> */
function epc_disc_warehouse_market_badge_labels(): array
{
	return array(
		'good_margin' => 'Good margin',
		'below_market' => 'Below market',
		'over_market' => 'Over market',
		'no_market_data' => 'No market data',
	);
}

function epc_disc_warehouse_market_badge(float $warehouseCost, float $marketMin, float $marketMax, int $sourceCount): string
{
	if ($sourceCount <= 0 || ($marketMin <= 0 && $marketMax <= 0)) {
		return 'no_market_data';
	}
	if ($warehouseCost <= 0) {
		return 'no_market_data';
	}
	if ($marketMax > 0 && $warehouseCost > $marketMax * 1.02) {
		return 'over_market';
	}
	if ($marketMin > 0 && $warehouseCost < $marketMin * 0.98) {
		return 'below_market';
	}
	return 'good_margin';
}

/**
 * Match warehouse price list rows (shop_docpart_prices_data) to live market prices.
 *
 * @return array{ok:bool,count:int,message:string}
 */
function epc_disc_match_warehouse_to_market(PDO $pdo, string $siteKey): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	epc_disc_cross_source_match($pdo, $siteKey);
	$now = time();
	$upserted = 0;

	try {
		$stmt = $pdo->query(
			'SELECT `manufacturer`, `article`, `name`, MIN(`price`) AS `warehouse_cost`, MAX(`price_list_id`) AS `price_list_id`
			 FROM `shop_docpart_prices_data`
			 WHERE `article` IS NOT NULL AND `article` != \'\'
			 GROUP BY UPPER(REPLACE(REPLACE(`article`, \' \', \'\'), \'-\', \'\')), LOWER(`manufacturer`)
			 LIMIT 500'
		);
		$rows = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
	} catch (Throwable $e) {
		return array('ok' => false, 'count' => 0, 'message' => 'Warehouse price list unavailable');
	}

	$ins = $pdo->prepare(
		'INSERT INTO `epc_warehouse_market_match`
		 (`site_key`, `brand_article_key`, `brand`, `article`, `article_show`, `title`, `warehouse_cost`, `market_min`, `market_max`, `margin_pct`, `margin_abs`, `source_match_count`, `matched_sources_json`, `badge`, `product_id`, `price_list_id`, `updated_at`)
		 VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
		 ON DUPLICATE KEY UPDATE
		 `brand`=VALUES(`brand`), `article`=VALUES(`article`), `article_show`=VALUES(`article_show`), `title`=VALUES(`title`),
		 `warehouse_cost`=VALUES(`warehouse_cost`), `market_min`=VALUES(`market_min`), `market_max`=VALUES(`market_max`),
		 `margin_pct`=VALUES(`margin_pct`), `margin_abs`=VALUES(`margin_abs`), `source_match_count`=VALUES(`source_match_count`),
		 `matched_sources_json`=VALUES(`matched_sources_json`), `badge`=VALUES(`badge`), `product_id`=VALUES(`product_id`),
		 `price_list_id`=VALUES(`price_list_id`), `updated_at`=VALUES(`updated_at`)'
	);

	foreach ($rows as $row) {
		$brand = epc_apai_normalize_brand((string) ($row['manufacturer'] ?? ''));
		$article = epc_apai_normalize_article((string) ($row['article'] ?? ''));
		$baKey = epc_apai_brand_article_key($brand, $article);
		if ($baKey === '') {
			continue;
		}
		$warehouseCost = (float) ($row['warehouse_cost'] ?? 0);
		$range = epc_disc_source_price_range($pdo, $siteKey, $baKey);
		$marketMin = (float) ($range['min_price'] ?? 0);
		$marketMax = (float) ($range['max_price'] ?? 0);
		$sourceCount = (int) ($range['source_count'] ?? 0);
		$badge = epc_disc_warehouse_market_badge($warehouseCost, $marketMin, $marketMax, $sourceCount);
		$marginAbs = ($marketMin > 0 && $warehouseCost > 0) ? ($marketMin - $warehouseCost) : 0.0;
		$marginPct = ($warehouseCost > 0 && $marginAbs != 0.0) ? round(($marginAbs / $warehouseCost) * 100, 1) : 0.0;
		$productId = epc_disc_find_catalogue_by_brand_article($pdo, $baKey);
		$ins->execute(array(
			$siteKey,
			$baKey,
			$brand,
			$article,
			(string) ($row['article'] ?? ''),
			(string) ($row['name'] ?? ''),
			$warehouseCost,
			$marketMin,
			$marketMax,
			$marginPct,
			$marginAbs,
			$sourceCount,
			json_encode((array) ($range['prices'] ?? array()), JSON_UNESCAPED_UNICODE),
			$badge,
			$productId,
			(int) ($row['price_list_id'] ?? 0),
			$now,
		));
		$upserted++;
	}

	return array(
		'ok' => true,
		'count' => $upserted,
		'message' => $upserted . ' warehouse SKU(s) matched to market',
	);
}

/**
 * @param array<string,mixed> $filters
 * @return array<int,array<string,mixed>>
 */
function epc_disc_warehouse_market_list(PDO $pdo, string $siteKey, array $filters = array()): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$limit = max(1, min(500, (int) ($filters['limit'] ?? 250)));
	$badge = trim((string) ($filters['badge'] ?? ''));
	$sql = 'SELECT * FROM `epc_warehouse_market_match` WHERE `site_key` = ?';
	$params = array($siteKey);
	if ($badge !== '') {
		$sql .= ' AND `badge` = ?';
		$params[] = $badge;
	}
	$sql .= ' ORDER BY `source_match_count` DESC, `updated_at` DESC LIMIT ' . $limit;
	$stmt = $pdo->prepare($sql);
	$stmt->execute($params);
	$rows = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
	$out = array();
	foreach ($rows as $row) {
		$row['matched_sources'] = json_decode((string) ($row['matched_sources_json'] ?? ''), true);
		if (!is_array($row['matched_sources'])) {
			$row['matched_sources'] = array();
		}
		$out[] = $row;
	}
	return $out;
}

/** @return array<string,int> */
function epc_disc_warehouse_market_counts(PDO $pdo, string $siteKey): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$counts = array(
		'good_margin' => 0,
		'below_market' => 0,
		'over_market' => 0,
		'no_market_data' => 0,
		'total' => 0,
	);
	try {
		$stmt = $pdo->prepare('SELECT `badge`, COUNT(*) AS `cnt` FROM `epc_warehouse_market_match` WHERE `site_key` = ? GROUP BY `badge`');
		$stmt->execute(array($siteKey));
		while ($r = $stmt->fetch(PDO::FETCH_ASSOC)) {
			$b = (string) ($r['badge'] ?? '');
			$c = (int) ($r['cnt'] ?? 0);
			if (isset($counts[$b])) {
				$counts[$b] = $c;
			}
			$counts['total'] += $c;
		}
	} catch (Throwable $e) {
	}
	return $counts;
}

function epc_disc_warehouse_update_catalogue_price(PDO $pdo, string $siteKey, string $baKey, bool $useMarketTarget = true): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$baKey = trim($baKey);
	if ($baKey === '') {
		return array('ok' => false, 'message' => 'brand_article_key required');
	}
	$stmt = $pdo->prepare('SELECT * FROM `epc_warehouse_market_match` WHERE `site_key` = ? AND `brand_article_key` = ? LIMIT 1');
	$stmt->execute(array($siteKey, $baKey));
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return array('ok' => false, 'message' => 'No warehouse market row — run warehouse match first');
	}
	$productId = (int) ($row['product_id'] ?? 0);
	if ($productId <= 0) {
		$productId = epc_disc_find_catalogue_by_brand_article($pdo, $baKey);
	}
	if ($productId <= 0) {
		return array('ok' => false, 'message' => 'No catalogue product linked for ' . $baKey);
	}
	$target = $useMarketTarget ? (float) ($row['market_max'] ?? 0) : (float) ($row['market_min'] ?? 0);
	if ($target <= 0) {
		$target = (float) ($row['market_min'] ?? 0);
	}
	if ($target <= 0) {
		return array('ok' => false, 'message' => 'No market price to apply');
	}
	epc_ape_set_catalogue_storage_price($pdo, $productId, $target);
	return array(
		'ok' => true,
		'message' => 'Catalogue #' . $productId . ' price updated to ' . number_format($target, 2),
		'product_id' => $productId,
		'price' => $target,
	);
}

function epc_disc_warehouse_flag_repricing(PDO $pdo, string $siteKey, string $baKey): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$baKey = trim($baKey);
	if ($baKey === '') {
		return array('ok' => false, 'message' => 'brand_article_key required');
	}
	$now = time();
	$meta = json_encode(array('repricing_flagged' => true, 'flagged_at' => $now), JSON_UNESCAPED_UNICODE);
	$stmt = $pdo->prepare('UPDATE `epc_warehouse_market_match` SET `meta_json` = ?, `updated_at` = ? WHERE `site_key` = ? AND `brand_article_key` = ?');
	$stmt->execute(array($meta, $now, $siteKey, $baKey));
	if ($stmt->rowCount() <= 0) {
		return array('ok' => false, 'message' => 'Row not found — run warehouse match first');
	}
	return array('ok' => true, 'message' => 'Flagged for repricing: ' . $baKey);
}

/**
 * Market-confirmed compare matrix from discovery queue (2+ sources per brand_article_key).
 *
 * @return array<int,array<string,mixed>>
 */
function epc_disc_market_confirmed_compare_matrix(PDO $pdo, string $siteKey): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	epc_disc_merge_cross_source_prices($pdo, $siteKey);
	$industry = function_exists('epc_apai_resolve_industry') ? epc_apai_resolve_industry($pdo, $siteKey) : 'general_retail';
	$stmt = $pdo->prepare(
		'SELECT q.*, scp.`caption` AS `catalogue_title`
		 FROM `epc_product_discovery_queue` q
		 LEFT JOIN `shop_catalogue_products` scp ON scp.`id` = q.`product_id`
		 WHERE q.`site_key` = ? AND q.`status` IN (\'suggested\', \'imported\')
		   AND q.`meta_json` LIKE \'%"market_confirmed":true%\'
		   AND (q.`meta_json` NOT LIKE \'%"duplicate_of":%\' OR q.`meta_json` LIKE \'%"duplicate_of":0%\')
		 ORDER BY q.`updated_at` DESC LIMIT 200'
	);
	$stmt->execute(array($siteKey));
	$rows = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();

	$groups = array();
	foreach ($rows as $row) {
		$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
		if (!is_array($meta) || empty($meta['market_confirmed'])) {
			continue;
		}
		if ((int) ($meta['duplicate_of'] ?? 0) > 0) {
			continue;
		}
		$groupKey = epc_disc_queue_identity_key($pdo, $siteKey, $row);
		if ($groupKey === '') {
			continue;
		}
		if (!isset($groups[$groupKey])) {
			$specs = json_decode((string) ($row['specs_json'] ?? ''), true);
			if (!is_array($specs)) {
				$specs = array();
			}
			$productId = (int) ($row['product_id'] ?? 0);
			if ($productId <= 0 && $industry === 'auto_parts') {
				$productId = epc_disc_find_catalogue_by_brand_article($pdo, $groupKey);
			}
			$yourInfo = epc_disc_resolve_your_prices($pdo, $siteKey, $groupKey, $row);
			$storedRange = (array) ($meta['source_price_range'] ?? array());
			if (empty($storedRange['buy_min']) && function_exists('epc_disc_build_source_price_range')) {
				$storedRange = epc_disc_build_source_price_range($pdo, $siteKey, $row);
			}
			$advice = (array) ($storedRange['pricing_advice'] ?? array());
			if (empty($advice['badge'])) {
				$advice = epc_disc_pricing_advice(
					array_merge($row, array('source_price_range' => $storedRange, 'your_price' => (float) ($yourInfo['your_price'] ?? 0))),
					(float) ($yourInfo['your_price'] ?? 0),
					(float) ($storedRange['buy_min'] ?? $storedRange['min'] ?? 0),
					(float) ($storedRange['target_sell_price'] ?? 0)
				);
			}
			$groups[$groupKey] = array(
				'brand_article_key' => strpos($groupKey, ':') !== false && strpos($groupKey, 'title:') !== 0 ? $groupKey : '',
				'identity_key' => $groupKey,
				'brand' => (string) ($specs['brand'] ?? epc_disc_extract_brand_from_title((string) ($row['title'] ?? ''))),
				'article_number' => (string) ($specs['article_number'] ?? epc_disc_extract_model_number($row)),
				'title' => (string) ($row['title'] ?? $row['catalogue_title'] ?? ''),
				'product_id' => max($productId, (int) ($yourInfo['product_id'] ?? 0)),
				'your_price' => (float) ($yourInfo['your_price'] ?? 0),
				'your_warehouse_price' => (float) ($yourInfo['warehouse'] ?? 0),
				'your_catalogue_price' => (float) ($yourInfo['catalogue'] ?? 0),
				'source_match_count' => (int) ($meta['source_match_count'] ?? 0),
				'spare247_price' => (float) ($meta['spare247_price'] ?? 0),
				'matched_sources' => (array) ($meta['matched_sources'] ?? array()),
				'source_price_range' => $storedRange,
				'pricing_advice' => $advice,
				'market_confirmed' => true,
			);
		}
	}
	$out = array_values($groups);
	usort($out, function ($a, $b) {
		return (int) ($b['source_match_count'] ?? 0) <=> (int) ($a['source_match_count'] ?? 0);
	});
	return $out;
}

/**
 * @param array<string,mixed> $match
 * @return array<string,mixed>
 */
function epc_disc_catalogue_match_to_discover_card(array $match): array
{
	$range = (array) ($match['source_price_range'] ?? array());
	return array(
		'id' => 0,
		'product_id' => (int) ($match['product_id'] ?? 0),
		'catalogue_product_id' => (int) ($match['product_id'] ?? 0),
		'title' => (string) ($match['title'] ?? ''),
		'brand' => (string) ($match['brand'] ?? ''),
		'article_number' => (string) ($match['article_number'] ?? ''),
		'brand_article_key' => (string) ($match['brand_article_key'] ?? ''),
		'suggested_price' => (float) ($match['market_min'] ?? 0),
		'sell_price' => (float) ($match['your_price'] ?? 0),
		'cost_estimate' => (float) ($match['market_min'] ?? 0),
		'currency' => (string) ($match['currency'] ?? 'AED'),
		'status' => 'catalogue_match',
		'market_confirmed' => !empty($match['market_confirmed']),
		'source_match_count' => (int) ($match['source_match_count'] ?? 0),
		'spare247_price' => (float) ($match['spare247_price'] ?? 0),
		'margin_flag' => (string) ($match['margin_flag'] ?? ''),
		'price_diff_pct' => (float) ($match['price_diff_pct'] ?? 0),
		'catalogue_price' => (float) ($match['your_price'] ?? 0),
		'source_price_range' => $range,
		'is_catalogue_match' => true,
	);
}

/**
 * Apply tenant pricing strategy to a source price range.
 *
 * @param array<string,mixed> $range Full or compact range
 * @return array{cost:float,sell_price:float,margin_pct:float,margin_abs:float,strategy:string}
 */
function epc_apai_apply_pricing_strategy(PDO $pdo, string $siteKey, array $range): array
{
	$min = (float) ($range['buy_min'] ?? $range['min'] ?? $range['min_price'] ?? 0);
	$targetSell = (float) ($range['target_sell_price'] ?? $range['marketplace_price'] ?? 0);
	$strategy = epc_apai_pricing_strategy($pdo, $siteKey);
	$cfg = epc_ape_tenant_config_get($pdo, $siteKey);
	$config = (array) ($cfg['config'] ?? array());
	$markupPct = (float) ($config['pricing_markup_pct'] ?? 0);
	if ($markupPct <= 0) {
		$rules = epc_ape_rules_get($pdo, $siteKey);
		$markupPct = (float) ($rules['min_margin_percent'] ?? 15);
	}
	$cost = $min;
	$sellPrice = $targetSell;
	if ($strategy === 'lowest_cost_plus_markup_pct') {
		$sellPrice = $cost > 0 ? round($cost * (1 + $markupPct / 100), 2) : 0;
	} elseif ($strategy === 'match_lowest_competitor') {
		$sellPrice = $targetSell > 0 ? $targetSell : ($min > 0 ? $min : 0);
	}
	if ($sellPrice <= 0 && $cost > 0) {
		$sellPrice = round($cost * (1 + $markupPct / 100), 2);
	}
	$marginAbs = ($cost > 0 && $sellPrice > 0) ? round($sellPrice - $cost, 2) : 0.0;
	$marginPct = ($cost > 0 && $marginAbs > 0) ? round(($marginAbs / $cost) * 100, 2) : 0.0;
	return array(
		'cost' => $cost,
		'sell_price' => $sellPrice,
		'margin_pct' => $marginPct,
		'margin_abs' => $marginAbs,
		'strategy' => $strategy,
	);
}

function epc_disc_margin_css_class(float $marginPct, float $minPrice, float $maxPrice): string
{
	if ($minPrice > 0 && abs($minPrice - $maxPrice) < 0.01) {
		return 'epc-disc-price-range--flat';
	}
	if ($marginPct < 10) {
		return 'epc-disc-price-range--thin';
	}
	return 'epc-disc-price-range--good';
}

/**
 * Render source price range block for Discover / Imports cards.
 *
 * @param array<string,mixed> $range
 */
function epc_disc_render_price_range_block(array $range, array $flags = array(), array $yourPrices = array()): string
{
	$buyMin = (float) ($range['buy_min'] ?? $range['min'] ?? $range['min_price'] ?? 0);
	$buyMax = (float) ($range['buy_max'] ?? $range['max'] ?? $range['max_price'] ?? 0);
	if ($buyMin <= 0) {
		return '';
	}
	$currency = (string) ($range['currency'] ?? 'AED');
	$buyCount = (int) ($range['buy_source_count'] ?? $range['source_count'] ?? 0);
	$buyLabels = (array) ($range['buy_source_labels'] ?? $range['source_labels'] ?? array());
	$marketplacePrice = (float) ($range['target_sell_price'] ?? $range['marketplace_price'] ?? 0);
	$marketplaceListed = !empty($range['marketplace_listed']);
	$marketplaceKnown = !empty($range['marketplace_known']);
	$mpMin = (float) ($range['marketplace_min'] ?? 0);
	$mpMax = (float) ($range['marketplace_max'] ?? 0);
	$yourPrice = (float) ($yourPrices['your_price'] ?? $range['your_price'] ?? 0);
	$compareType = (string) ($yourPrices['compare_type'] ?? $range['compare_type'] ?? '');
	$marginAbs = (float) ($range['margin_abs'] ?? 0);
	$marginPct = (float) ($range['margin_pct'] ?? 0);
	if ($marketplacePrice > 0 && $buyMin > 0 && $marginAbs <= 0) {
		$marginAbs = round($marketplacePrice - $buyMin, 2);
		$marginPct = round(($marginAbs / $buyMin) * 100, 1);
	}
	$cls = epc_disc_margin_css_class($marginPct, $buyMin, $marketplacePrice > 0 ? $marketplacePrice : $buyMax);
	$advice = (array) ($range['pricing_advice'] ?? array());
	if (empty($advice['badge'])) {
		$advice = epc_disc_pricing_advice(array('source_price_range' => $range), $yourPrice, $buyMin, $marketplacePrice);
	}
	$adviceLevel = (string) ($advice['level'] ?? 'info');
	$adviceCls = 'epc-disc-advice--' . ($adviceLevel === 'danger' ? 'danger' : ($adviceLevel === 'success' ? 'success' : ($adviceLevel === 'warning' ? 'warning' : 'info')));
	$flagHtml = '';
	foreach ((array) $flags as $flag) {
		if ($flag === 'cost_down') {
			$flagHtml .= '<span class="epc-disc-range-flag epc-disc-range-flag--down">Buy price down ↓</span> ';
		} elseif ($flag === 'target_up') {
			$flagHtml .= '<span class="epc-disc-range-flag epc-disc-range-flag--up">Marketplace up ↑</span> ';
		} elseif ($flag === 'margin_changed') {
			$flagHtml .= '<span class="epc-disc-range-flag epc-disc-range-flag--margin">Margin changed</span> ';
		}
	}
	ob_start();
	?>
	<div class="epc-disc-price-range <?php echo htmlspecialchars($cls, ENT_QUOTES, 'UTF-8'); ?><?php echo $buyCount === 1 ? ' epc-disc-price-range--single' : ''; ?>" data-field="source_price_range">
		<?php if ($flagHtml !== '') { ?><div class="epc-disc-price-range__flags"><?php echo $flagHtml; ?></div><?php } ?>
		<?php if (!empty($advice['badge'])) { ?>
		<div class="epc-disc-advice <?php echo htmlspecialchars($adviceCls, ENT_QUOTES, 'UTF-8'); ?>" data-field="pricing_advice">
			<strong><?php echo htmlspecialchars((string) $advice['badge'], ENT_QUOTES, 'UTF-8'); ?></strong>
			<?php if (!empty($advice['message'])) { ?> — <?php echo htmlspecialchars((string) $advice['message'], ENT_QUOTES, 'UTF-8'); ?><?php } ?>
		</div>
		<?php } ?>
		<div class="epc-disc-price-range__row epc-disc-price-range__row--cost">
			<small>Buy from (lowest)</small>
			<strong data-field="range_min"><?php echo number_format($buyMin, 2); ?></strong> <?php echo htmlspecialchars($currency, ENT_QUOTES, 'UTF-8'); ?>
			<span class="epc-disc-price-range__src">(<?php echo htmlspecialchars((string) ($range['min_source'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>)</span>
		</div>
		<?php if ($buyMax > $buyMin && $buyCount >= 2) { ?>
		<div class="epc-disc-price-range__row epc-disc-price-range__row--buy-band">
			<small>Buy source range</small>
			<strong data-field="range_buy_max"><?php echo number_format($buyMin, 2); ?>–<?php echo number_format($buyMax, 2); ?></strong> <?php echo htmlspecialchars($currency, ENT_QUOTES, 'UTF-8'); ?>
			<em class="epc-disc-price-range__hint">sourcing only</em>
		</div>
		<?php } ?>
		<div class="epc-disc-price-range__row epc-disc-price-range__row--target">
			<small>Sell on marketplace</small>
			<?php if ($marketplaceListed && $marketplacePrice > 0) { ?>
			<strong data-field="range_marketplace"><?php echo number_format($marketplacePrice, 2); ?></strong> <?php echo htmlspecialchars($currency, ENT_QUOTES, 'UTF-8'); ?>
			<span class="epc-disc-price-range__src">(<?php echo htmlspecialchars((string) (($range['marketplace_range']['min_source'] ?? '') ?: 'marketplace'), ENT_QUOTES, 'UTF-8'); ?>)</span>
			<?php } elseif ($marketplacePrice > 0) { ?>
			<strong data-field="range_marketplace">~<?php echo number_format($marketplacePrice, 2); ?></strong> <?php echo htmlspecialchars($currency, ENT_QUOTES, 'UTF-8'); ?>
			<em class="epc-disc-price-range__hint"><?php echo $marketplaceKnown ? 'category benchmark' : 'estimate'; ?></em>
			<?php } else { ?>
			<strong class="text-muted" data-field="range_marketplace">Not listed — opportunity</strong>
			<em class="epc-disc-price-range__hint">Research needed</em>
			<?php } ?>
		</div>
		<?php if ($marketplacePrice > 0) { ?>
		<div class="epc-disc-price-range__row epc-disc-price-range__row--sell-target">
			<small>Target sell price</small>
			<strong data-field="range_max"><?php echo number_format($marketplacePrice, 2); ?></strong> <?php echo htmlspecialchars($currency, ENT_QUOTES, 'UTF-8'); ?>
			<em class="epc-disc-price-range__hint">marketplace</em>
		</div>
		<?php } ?>
		<div class="epc-disc-price-range__row epc-disc-price-range__row--yours">
			<small>Your price</small>
			<?php if ($yourPrice > 0) { ?>
			<strong data-field="your_price"><?php echo number_format($yourPrice, 2); ?></strong> <?php echo htmlspecialchars($currency, ENT_QUOTES, 'UTF-8'); ?>
			<span class="epc-disc-price-range__src">(<?php echo htmlspecialchars($compareType !== '' ? $compareType : 'catalogue', ENT_QUOTES, 'UTF-8'); ?>)</span>
			<?php } else { ?>
			<strong class="text-muted" data-field="your_price">Not in warehouse</strong>
			<?php } ?>
		</div>
		<?php if ($marginAbs > 0 && $marketplacePrice > 0) { ?>
		<div class="epc-disc-price-range__row epc-disc-price-range__row--margin">
			<small>Margin</small>
			<strong data-field="range_margin"><?php echo number_format($marginAbs, 2); ?> <?php echo htmlspecialchars($currency, ENT_QUOTES, 'UTF-8'); ?> (<?php echo number_format($marginPct, 1); ?>%)</strong>
			<em class="epc-disc-price-range__hint">marketplace − buy lowest</em>
		</div>
		<?php } elseif ($buyCount === 1) { ?>
		<div class="epc-disc-price-range__row epc-disc-price-range__row--margin text-muted">
			<small>Margin</small>
			<strong>Need marketplace listing data</strong>
		</div>
		<?php } ?>
		<?php if ($buyCount > 0) { ?>
		<div class="epc-disc-price-range__sources" data-field="range_source_count">Buy sources: <?php echo htmlspecialchars(implode(' · ', $buyLabels), ENT_QUOTES, 'UTF-8'); ?></div>
		<?php } ?>
	</div>
	<?php
	return (string) ob_get_clean();
}

/**
 * Detect significant changes between previous and current source price ranges.
 *
 * @param array<string,mixed> $prev
 * @param array<string,mixed> $curr
 * @return array{cost_down:bool,target_up:bool,margin_changed:bool,flags:array}
 */
function epc_disc_detect_range_changes(array $prev, array $curr): array
{
	$prevMin = (float) ($prev['buy_min'] ?? $prev['min'] ?? $prev['min_price'] ?? 0);
	$prevTarget = (float) ($prev['target_sell_price'] ?? $prev['marketplace_price'] ?? $prev['max'] ?? $prev['max_price'] ?? 0);
	$prevMargin = (float) ($prev['margin_pct'] ?? 0);
	$currMin = (float) ($curr['buy_min'] ?? $curr['min'] ?? $curr['min_price'] ?? 0);
	$currTarget = (float) ($curr['target_sell_price'] ?? $curr['marketplace_price'] ?? $curr['max'] ?? $curr['max_price'] ?? 0);
	$currMargin = (float) ($curr['margin_pct'] ?? 0);
	$flags = array();
	$costDown = ($prevMin > 0 && $currMin > 0 && epc_disc_prices_differ($currMin, $prevMin) && $currMin < $prevMin);
	$targetUp = ($prevTarget > 0 && $currTarget > 0 && epc_disc_prices_differ($currTarget, $prevTarget) && $currTarget > $prevTarget);
	$marginChanged = ($prevMargin > 0 && abs($currMargin - $prevMargin) >= 1.0);
	if ($costDown) {
		$flags[] = 'cost_down';
	}
	if ($targetUp) {
		$flags[] = 'target_up';
	}
	if ($marginChanged) {
		$flags[] = 'margin_changed';
	}
	return array(
		'cost_down' => $costDown,
		'target_up' => $targetUp,
		'margin_changed' => $marginChanged,
		'flags' => $flags,
	);
}

function epc_disc_find_catalogue_by_brand_article(PDO $pdo, string $brandArticleKey): int
{
	$brandArticleKey = strtolower(trim($brandArticleKey));
	if ($brandArticleKey === '' || strpos($brandArticleKey, ':') === false) {
		return 0;
	}
	$parts = explode(':', $brandArticleKey, 2);
	$brand = epc_apai_normalize_brand($parts[0] ?? '');
	$article = epc_apai_normalize_article($parts[1] ?? '');
	if ($brand === '' || $article === '') {
		return 0;
	}
	try {
		$stmt = $pdo->prepare(
			'SELECT `product_id` FROM `epc_price_source_products`
			 WHERE `external_sku` = ? AND `product_id` > 0
			 ORDER BY `updated_at` DESC LIMIT 1'
		);
		$stmt->execute(array($brandArticleKey));
		$pid = (int) $stmt->fetchColumn();
		if ($pid > 0) {
			return $pid;
		}
		$stmt = $pdo->prepare(
			'SELECT `product_id` FROM `epc_price_source_products`
			 WHERE `external_sku` IN (?, ?) AND `product_id` > 0
			 ORDER BY `updated_at` DESC LIMIT 1'
		);
		$stmt->execute(array($article, $brand . '-' . $article));
		$pid = (int) $stmt->fetchColumn();
		if ($pid > 0) {
			return $pid;
		}
		$wh = epc_disc_find_warehouse_row_by_brand_article($pdo, $brand, $article);
		if (!empty($wh['product_id'])) {
			return (int) $wh['product_id'];
		}
	} catch (Throwable $e) {
	}
	return 0;
}

/**
 * @return array{product_id:int,price:float,cost:float,manufacturer:string,article:string,title:string}
 */
function epc_disc_find_warehouse_row_by_brand_article(PDO $pdo, string $brand, string $article): array
{
	$out = array('product_id' => 0, 'price' => 0.0, 'cost' => 0.0, 'manufacturer' => '', 'article' => '', 'title' => '');
	$brand = epc_apai_normalize_brand($brand);
	$article = epc_apai_normalize_article($article);
	if ($brand === '' || $article === '') {
		return $out;
	}
	try {
		$stmt = $pdo->prepare(
			'SELECT `manufacturer`, `article`, `name`, `price`
			 FROM `shop_docpart_prices_data`
			 WHERE UPPER(REPLACE(REPLACE(REPLACE(`article`, \' \', \'\'), \'-\', \'\'), \'.\', \'\')) = ?
			   AND LOWER(REPLACE(`manufacturer`, \' \', \'-\')) LIKE ?
			 ORDER BY IFNULL(`exist`, 0) DESC, `price` ASC
			 LIMIT 1'
		);
		$stmt->execute(array($article, $brand . '%'));
		$row = $stmt->fetch(PDO::FETCH_ASSOC);
		if (!$row) {
			$stmt = $pdo->prepare(
				'SELECT `manufacturer`, `article`, `name`, `price`
				 FROM `shop_docpart_prices_data`
				 WHERE UPPER(REPLACE(REPLACE(REPLACE(`article`, \' \', \'\'), \'-\', \'\'), \'.\', \'\')) = ?
				 ORDER BY IFNULL(`exist`, 0) DESC, `price` ASC
				 LIMIT 1'
			);
			$stmt->execute(array($article));
			$row = $stmt->fetch(PDO::FETCH_ASSOC);
		}
		if ($row) {
			$out['manufacturer'] = (string) ($row['manufacturer'] ?? '');
			$out['article'] = (string) ($row['article'] ?? '');
			$out['title'] = (string) ($row['name'] ?? '');
			$out['price'] = (float) ($row['price'] ?? 0);
			$out['cost'] = (float) ($row['price'] ?? 0);
			$key = epc_apai_brand_article_key($out['manufacturer'] ?: $brand, $out['article'] ?: $article);
			if ($key !== '') {
				$srcStmt = $pdo->prepare(
					'SELECT `product_id`, `warehouse_cost`, `last_price` FROM `epc_price_source_products`
					 WHERE `external_sku` = ? AND `product_id` > 0 ORDER BY `updated_at` DESC LIMIT 1'
				);
				$srcStmt->execute(array($key));
				$src = $srcStmt->fetch(PDO::FETCH_ASSOC);
				if ($src) {
					$out['product_id'] = (int) ($src['product_id'] ?? 0);
					$out['cost'] = (float) ($src['warehouse_cost'] ?? $src['last_price'] ?? $out['price']);
				}
			}
		}
	} catch (Throwable $e) {
	}
	return $out;
}

function epc_disc_find_catalogue_by_title(PDO $pdo, string $title): int
{
	$title = trim($title);
	if ($title === '') {
		return 0;
	}
	$norm = strtolower(preg_replace('/\s+/', ' ', $title));
	try {
		$stmt = $pdo->prepare('SELECT `id` FROM `shop_catalogue_products` WHERE `published_flag` = 1 AND LOWER(`caption`) = ? LIMIT 1');
		$stmt->execute(array($norm));
		$id = (int) $stmt->fetchColumn();
		if ($id > 0) {
			return $id;
		}
		$short = substr($norm, 0, 48);
		if ($short !== '') {
			$stmt = $pdo->prepare('SELECT `id` FROM `shop_catalogue_products` WHERE `published_flag` = 1 AND LOWER(`caption`) LIKE ? LIMIT 1');
			$stmt->execute(array($short . '%'));
			return (int) $stmt->fetchColumn();
		}
	} catch (Throwable $e) {
	}
	return 0;
}

/**
 * @return array{sell_price:float,warehouse_cost:float,product_id:int}
 */
function epc_disc_catalogue_prices(PDO $pdo, string $siteKey, int $productId): array
{
	$out = array('sell_price' => 0.0, 'warehouse_cost' => 0.0, 'product_id' => max(0, $productId));
	if ($productId <= 0) {
		return $out;
	}
	try {
		$pr = $pdo->prepare('SELECT MIN(`price`) FROM `shop_storages_data` WHERE `product_id` = ? AND `price` > 0');
		$pr->execute(array($productId));
		$out['sell_price'] = (float) $pr->fetchColumn();
	} catch (Throwable $e) {
	}
	try {
		$wh = $pdo->prepare('SELECT MAX(`warehouse_cost`) FROM `epc_price_source_products` WHERE `product_id` = ? AND `warehouse_cost` > 0');
		$wh->execute(array($productId));
		$out['warehouse_cost'] = (float) $wh->fetchColumn();
	} catch (Throwable $e) {
	}
	return $out;
}

/**
 * Compare source price to catalogue sell / warehouse cost for tenant profile.
 *
 * @return array<string,mixed>
 */
function epc_disc_compute_price_diff(PDO $pdo, string $siteKey, float $sourcePrice, array $row, bool $fastPartial = false): array
{
	$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
	if (!is_array($meta)) {
		$meta = array();
	}

	if ($fastPartial) {
		$productId = max(0, (int) ($row['product_id'] ?? 0));
		if ($productId <= 0) {
			$productId = (int) ($meta['catalogue_product_id'] ?? 0);
		}
		$rangeCompact = !empty($meta['source_price_range']) && is_array($meta['source_price_range'])
			? (array) $meta['source_price_range']
			: array();
		return array(
			'catalogue_product_id' => $productId,
			'catalogue_price' => 0.0,
			'compare_type' => 'catalogue',
			'price_diff_pct' => 0.0,
			'price_changed' => 0,
			'sell_price' => 0.0,
			'warehouse_cost' => 0.0,
			'your_price' => 0.0,
			'your_warehouse_price' => 0.0,
			'your_catalogue_price' => 0.0,
			'in_warehouse' => !empty($meta['in_warehouse']),
			'show_reason' => '',
			'identity_key' => (string) ($row['brand_article_key'] ?? ''),
			'source_price_range' => $rangeCompact,
			'range_flags' => (array) ($meta['range_change_flags'] ?? array()),
		);
	}

	$tenantCfg = epc_ape_tenant_config_get($pdo, $siteKey);
	$profile = (string) ($tenantCfg['profile'] ?? '');
	$status = (string) ($row['status'] ?? 'suggested');

	$productId = (int) ($row['product_id'] ?? 0);
	if ($productId <= 0) {
		$productId = (int) ($meta['catalogue_product_id'] ?? 0);
	}
	$industryKey = function_exists('epc_apai_resolve_industry') ? epc_apai_resolve_industry($pdo, $siteKey) : 'general_retail';
	if ($productId <= 0 && $industryKey === 'auto_parts') {
		$baKey = epc_apai_queue_brand_article_key($row);
		if ($baKey !== '') {
			$productId = epc_disc_find_catalogue_by_brand_article($pdo, $baKey);
		}
	}
	if ($productId <= 0) {
		$productId = epc_disc_find_catalogue_by_title($pdo, (string) ($row['title'] ?? ''));
	}
	$prices = epc_disc_catalogue_prices($pdo, $siteKey, $productId);
	$comparePrice = (float) $prices['sell_price'];
	$compareType = 'catalogue';
	if ($profile === 'warehouse_supplier' && (float) $prices['warehouse_cost'] > 0) {
		$comparePrice = (float) $prices['warehouse_cost'];
		$compareType = 'warehouse';
	} elseif ($comparePrice <= 0 && (float) $prices['warehouse_cost'] > 0) {
		$comparePrice = (float) $prices['warehouse_cost'];
		$compareType = 'warehouse';
	}

	$diffPct = 0.0;
	$hasDiff = false;
	$showReason = '';
	if ($sourcePrice > 0 && $comparePrice > 0) {
		$diffPct = round((($sourcePrice - $comparePrice) / $comparePrice) * 100, 1);
	}

	$baselineSource = (float) ($meta['baseline_source_price'] ?? $meta['import_source_price'] ?? 0);
	$isLinked = ($status === 'imported' || $productId > 0);

	$identityKey = epc_disc_queue_identity_key($pdo, $siteKey, $row);
	$yourInfo = epc_disc_resolve_your_prices($pdo, $siteKey, $identityKey, $row);
	if ((float) ($yourInfo['your_price'] ?? 0) > 0) {
		if ($profile === 'warehouse_supplier' && (float) ($yourInfo['warehouse'] ?? 0) > 0) {
			$comparePrice = (float) $yourInfo['warehouse'];
			$compareType = 'warehouse';
		} elseif ((float) ($yourInfo['catalogue'] ?? 0) > 0) {
			$comparePrice = (float) $yourInfo['catalogue'];
			$compareType = 'catalogue';
		} elseif ((float) ($yourInfo['your_price'] ?? 0) > 0) {
			$comparePrice = (float) $yourInfo['your_price'];
			$compareType = (string) ($yourInfo['compare_type'] ?? 'catalogue');
		}
		if ($productId <= 0 && (int) ($yourInfo['product_id'] ?? 0) > 0) {
			$productId = (int) $yourInfo['product_id'];
		}
	}

	$rangeCompact = epc_disc_build_source_price_range($pdo, $siteKey, $row);
	$prevRange = ($status === 'imported')
		? (array) ($meta['baseline_source_price_range'] ?? array())
		: (array) ($meta['source_price_range'] ?? array());
	$rangeChanges = ($prevRange && $rangeCompact) ? epc_disc_detect_range_changes($prevRange, $rangeCompact) : array('flags' => array());
	$rangeFlags = (array) ($rangeChanges['flags'] ?? array());

	if ($isLinked && $baselineSource > 0 && $sourcePrice > 0) {
		$hasDiff = epc_disc_prices_differ($sourcePrice, $baselineSource);
	} elseif ($isLinked) {
		$hasDiff = false;
	} elseif ($sourcePrice > 0 && $comparePrice > 0 && $productId > 0) {
		$hasDiff = epc_disc_prices_differ($sourcePrice, $comparePrice);
	}
	if (!$hasDiff && $rangeFlags) {
		$hasDiff = true;
	}
	if ($hasDiff) {
		$showReason = 'price_changed';
	}

	return array(
		'catalogue_product_id' => $productId,
		'catalogue_price' => $comparePrice,
		'compare_type' => $compareType,
		'price_diff_pct' => $diffPct,
		'price_changed' => $hasDiff ? 1 : 0,
		'sell_price' => (float) $prices['sell_price'],
		'warehouse_cost' => (float) $prices['warehouse_cost'],
		'your_price' => (float) ($yourInfo['your_price'] ?? $comparePrice),
		'your_warehouse_price' => (float) ($yourInfo['warehouse'] ?? 0),
		'your_catalogue_price' => (float) ($yourInfo['catalogue'] ?? 0),
		'in_warehouse' => !empty($yourInfo['in_warehouse']),
		'identity_key' => $identityKey,
		'show_reason' => $showReason,
		'source_price_range' => $rangeCompact,
		'range_flags' => $rangeFlags,
		'cost_down' => !empty($rangeChanges['cost_down']),
		'target_up' => !empty($rangeChanges['target_up']),
		'margin_changed' => !empty($rangeChanges['margin_changed']),
	);
}

/**
 * @param array<string,mixed> $row
 * @param array<string,mixed> $diffInfo
 */
function epc_disc_apply_price_meta_to_row(array &$row, array $diffInfo, int $now = 0): void
{
	$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
	if (!is_array($meta)) {
		$meta = array();
	}
	if (!empty($diffInfo['price_changed'])) {
		$meta['price_changed'] = 1;
		$meta['show_reason'] = 'price_changed';
	} else {
		unset($meta['price_changed']);
		if (($meta['show_reason'] ?? '') === 'price_changed') {
			unset($meta['show_reason']);
		}
	}
	if ((float) ($diffInfo['catalogue_price'] ?? 0) > 0) {
		$meta['catalogue_price'] = (float) $diffInfo['catalogue_price'];
		$meta['catalogue_product_id'] = (int) ($diffInfo['catalogue_product_id'] ?? 0);
		$meta['compare_type'] = (string) ($diffInfo['compare_type'] ?? 'catalogue');
	}
	$meta['price_diff_pct'] = (float) ($diffInfo['price_diff_pct'] ?? 0);
	if (!empty($diffInfo['source_price_range']) && is_array($diffInfo['source_price_range'])) {
		epc_disc_persist_source_price_range_meta($meta, $diffInfo['source_price_range']);
	}
	if (!empty($diffInfo['range_flags']) && is_array($diffInfo['range_flags'])) {
		$meta['range_change_flags'] = $diffInfo['range_flags'];
	}
	if ($now > 0) {
		$meta['last_fetched'] = $now;
	}
	$row['meta_json'] = json_encode($meta, JSON_UNESCAPED_UNICODE);
	$row['price_diff_pct'] = (float) ($diffInfo['price_diff_pct'] ?? 0);
	$row['price_changed'] = !empty($diffInfo['price_changed']);
	$row['catalogue_price'] = (float) ($diffInfo['catalogue_price'] ?? 0);
	$row['catalogue_product_id'] = (int) ($diffInfo['catalogue_product_id'] ?? 0);
	$row['show_reason'] = (string) ($diffInfo['show_reason'] ?? '');
}

/**
 * @param array<string,mixed> $row
 * @param array<string,mixed> $filters
 * @param array<string,mixed> $diffInfo
 */
function epc_disc_discover_resolve_view(array $filters): string
{
	$view = (string) ($filters['view'] ?? '');
	if ($view === '') {
		$view = (string) ($filters['visibility'] ?? '');
	}
	if ($view === 'show_all' || $view === 'new') {
		$view = 'all_suggestions';
	}
	$valid = array('market_confirmed', 'all_suggestions', 'price_changes', 'catalogue_match', 'marketplace_opportunities');
	if (!in_array($view, $valid, true)) {
		$view = 'all_suggestions';
	}
	return $view;
}

function epc_disc_should_show_in_discover(PDO $pdo, string $siteKey, array $row, array $filters, array $diffInfo): bool
{
	$view = epc_disc_discover_resolve_view($filters);
	$status = (string) ($row['status'] ?? 'suggested');
	$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
	if (!is_array($meta)) {
		$meta = array();
	}
	$catId = max(
		(int) ($diffInfo['catalogue_product_id'] ?? 0),
		(int) ($row['product_id'] ?? 0),
		(int) ($meta['catalogue_product_id'] ?? 0)
	);
	$priceChanged = !empty($diffInfo['price_changed']);
	$marketConfirmed = !empty($meta['market_confirmed'])
		|| (int) ($meta['source_match_count'] ?? 0) >= 2
		|| !empty($diffInfo['source_price_range']['market_confirmed']);

	if ($view === 'catalogue_match') {
		return false;
	}

	if ($view === 'price_changes') {
		if ($status !== 'imported' && $catId <= 0) {
			return false;
		}
		return $priceChanged;
	}

	if ($status === 'imported' || $status !== 'suggested') {
		return false;
	}
	if ($catId > 0) {
		return false;
	}
	if ((int) ($meta['duplicate_of'] ?? 0) > 0) {
		return false;
	}

	if ($view === 'market_confirmed' && !$marketConfirmed) {
		return false;
	}

	if ($view === 'marketplace_opportunities') {
		if (empty($meta['arbitrage_opportunity'])) {
			return false;
		}
		$channels = array();
		if (is_file(__DIR__ . '/epc_apai_marketplace_channels.php')) {
			require_once __DIR__ . '/epc_apai_marketplace_channels.php';
			if (function_exists('epc_apai_marketplace_channels_for_tenant')) {
				$channels = epc_apai_marketplace_channels_for_tenant($pdo, $siteKey);
			}
		}
		$minMargin = (float) ($channels['min_margin_pct'] ?? 15);
		$arbMargin = (float) ($meta['arbitrage_margin_pct'] ?? 0);
		return $arbMargin >= $minMargin;
	}

	$warehouseOnly = !empty($filters['warehouse_only']);
	if ($warehouseOnly) {
		$baKey = (string) ($meta['brand_article_key'] ?? '');
		if ($baKey === '') {
			$specs = json_decode((string) ($row['specs_json'] ?? ''), true);
			if (is_array($specs) && !empty($specs['brand_article_key'])) {
				$baKey = (string) $specs['brand_article_key'];
			}
		}
		if ($baKey === '' || empty($meta['in_warehouse'])) {
			return false;
		}
	}

	return true;
}

/**
 * @param array<string,mixed> $row
 */
function epc_disc_queue_backfill_import_baseline(PDO $pdo, string $siteKey, array &$row): void
{
	if ((string) ($row['status'] ?? '') !== 'imported') {
		return;
	}
	$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
	if (!is_array($meta)) {
		$meta = array();
	}
	$sourcePrice = (float) ($row['suggested_price'] ?? 0);
	$changed = false;
	if ((float) ($meta['baseline_source_price'] ?? 0) <= 0 && $sourcePrice > 0) {
		$meta['baseline_source_price'] = $sourcePrice;
		$meta['import_source_price'] = $sourcePrice;
		$changed = true;
	}
	if (empty($meta['imported_at'])) {
		$meta['imported_at'] = (int) ($row['updated_at'] ?? time());
		$changed = true;
	}
	if (empty($meta['catalogue_product_id']) && (int) ($row['product_id'] ?? 0) > 0) {
		$meta['catalogue_product_id'] = (int) $row['product_id'];
		$changed = true;
	}
	if (!$changed) {
		return;
	}
	$now = time();
	$row['meta_json'] = json_encode($meta, JSON_UNESCAPED_UNICODE);
	$pdo->prepare('UPDATE `epc_product_discovery_queue` SET `meta_json` = ?, `updated_at` = ? WHERE `id` = ? AND `site_key` = ?')
		->execute(array($row['meta_json'], $now, (int) ($row['id'] ?? 0), $siteKey));
}

/**
 * Discover tab queue with catalogue/price visibility rules.
 *
 * @param array<string,mixed> $filters taxonomy_id, view (new|price_changes), sort (newest|price_change|last_updated), limit
 * @return array<int,array<string,mixed>>
 */
function epc_disc_queue_list_for_discover(PDO $pdo, string $siteKey, array $filters = array()): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$taxonomyNodeId = max(0, (int) ($filters['taxonomy_id'] ?? 0));
	$limit = max(1, min(200, (int) ($filters['limit'] ?? 60)));
	$sort = (string) ($filters['sort'] ?? 'newest');
	$view = epc_disc_discover_resolve_view($filters);
	$fastPartial = !empty($filters['fast_partial']);

	if ($view === 'catalogue_match') {
		// fast_partial used to return [] here — badge counts stayed correct while the grid
		// showed an empty state. Always load matches; cap work under partial/shell loads.
		$matchOpts = array();
		if ($fastPartial) {
			$matchOpts['limit'] = max($limit, 40);
		}
		$matchData = epc_disc_match_catalogue_to_market($pdo, $siteKey, $matchOpts);
		$visible = array();
		foreach ((array) ($matchData['items'] ?? array()) as $match) {
			$visible[] = epc_disc_catalogue_match_to_discover_card($match);
		}
		if ($sort === 'price_change') {
			usort($visible, function ($a, $b) {
				return abs((float) ($b['price_diff_pct'] ?? 0)) <=> abs((float) ($a['price_diff_pct'] ?? 0));
			});
		}
		return array_slice($visible, 0, $limit);
	}

	$sql = 'SELECT q.*, t.`name_en` AS `taxonomy_name`, t.`slug` AS `taxonomy_slug`
		 FROM `epc_product_discovery_queue` q
		 LEFT JOIN `epc_product_taxonomy_nodes` t ON t.`id` = q.`taxonomy_node_id`
		 WHERE q.`site_key` = ? AND q.`status` IN (\'suggested\', \'imported\')';
	$params = array($siteKey);
	if ($taxonomyNodeId > 0) {
		$sql .= ' AND q.`taxonomy_node_id` = ?';
		$params[] = $taxonomyNodeId;
	}
	$sql .= ' ORDER BY q.`updated_at` DESC LIMIT ' . ($fastPartial ? $limit : max($limit, 60));
	$stmt = $pdo->prepare($sql);
	$stmt->execute($params);
	$raw = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();

	$visible = array();
	foreach ($raw as $row) {
		if (!$fastPartial) {
			epc_disc_queue_backfill_import_baseline($pdo, $siteKey, $row);
		}
		$sourcePrice = (float) ($row['suggested_price'] ?? 0);
		$diffInfo = epc_disc_compute_price_diff($pdo, $siteKey, $sourcePrice, $row, $fastPartial);
		if (!epc_disc_should_show_in_discover($pdo, $siteKey, $row, $filters, $diffInfo)) {
			continue;
		}
		epc_disc_apply_price_meta_to_row($row, $diffInfo);
		$parsed = epc_disc_queue_parse_row($row);
		$discIndustry = function_exists('epc_apai_resolve_industry') ? epc_apai_resolve_industry($pdo, $siteKey) : 'general_retail';
		$parsed['needs_part_number'] = ($discIndustry === 'auto_parts' && (string) ($parsed['brand_article_key'] ?? '') === '');
		$parsed['price_diff_pct'] = (float) ($diffInfo['price_diff_pct'] ?? 0);
		$parsed['price_changed'] = !empty($diffInfo['price_changed']);
		$parsed['catalogue_price'] = (float) ($diffInfo['catalogue_price'] ?? 0);
		$parsed['catalogue_product_id'] = (int) ($diffInfo['catalogue_product_id'] ?? 0);
		$parsed['show_reason'] = (string) ($diffInfo['show_reason'] ?? '');
		$parsed['source_price_range'] = (array) ($diffInfo['source_price_range'] ?? array());
		$parsed['range_flags'] = (array) ($diffInfo['range_flags'] ?? array());
		$parsed['your_price'] = (float) ($diffInfo['your_price'] ?? 0);
		$parsed['your_warehouse_price'] = (float) ($diffInfo['your_warehouse_price'] ?? 0);
		$parsed['your_catalogue_price'] = (float) ($diffInfo['your_catalogue_price'] ?? 0);
		$parsed['compare_type'] = (string) ($diffInfo['compare_type'] ?? '');
		$parsed['identity_key'] = (string) ($diffInfo['identity_key'] ?? '');
		$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
		if (empty($parsed['source_price_range']) && is_array($meta) && !empty($meta['source_price_range'])) {
			$parsed['source_price_range'] = (array) $meta['source_price_range'];
		}
		if (!empty($parsed['source_price_range'])) {
			$parsed['pricing_advice'] = (array) ($parsed['source_price_range']['pricing_advice'] ?? array());
			if (empty($parsed['pricing_advice']['badge']) && function_exists('epc_disc_pricing_advice')) {
				$parsed['pricing_advice'] = epc_disc_pricing_advice(
					array_merge($parsed, array('source_price_range' => $parsed['source_price_range'])),
					(float) ($parsed['your_price'] ?? 0),
					(float) ($parsed['source_price_range']['buy_min'] ?? $parsed['source_price_range']['min'] ?? 0),
					(float) ($parsed['source_price_range']['target_sell_price'] ?? 0)
				);
			}
		}
		if (empty($parsed['range_flags']) && is_array($meta) && !empty($meta['range_change_flags'])) {
			$parsed['range_flags'] = (array) $meta['range_change_flags'];
		}
		if (is_array($meta)) {
			$parsed['market_confirmed'] = !empty($meta['market_confirmed']);
			$parsed['source_match_count'] = (int) ($meta['source_match_count'] ?? 0);
			$parsed['matched_sources'] = (array) ($meta['matched_sources'] ?? array());
			$parsed['in_warehouse'] = !empty($meta['in_warehouse']);
			$parsed['no_market_data'] = !empty($meta['no_market_data']);
			$parsed['suggest_add_warehouse'] = !empty($meta['suggest_add_warehouse']);
			$parsed['spare247_price'] = (float) ($meta['spare247_price'] ?? 0);
			$parsed['alternate_channel'] = (array) ($meta['alternate_channel'] ?? array());
			$parsed['on_main_source'] = !isset($meta['on_main_source']) || !empty($meta['on_main_source']);
			$parsed['arbitrage_opportunity'] = !empty($meta['arbitrage_opportunity']);
			$parsed['buy_sources'] = (array) ($meta['buy_sources'] ?? array());
			$parsed['buy_price_min'] = (float) ($meta['buy_price_min'] ?? 0);
			$parsed['missing_marketplaces'] = (array) ($meta['missing_marketplaces'] ?? array());
			$parsed['estimated_marketplace_price'] = (float) ($meta['estimated_marketplace_price'] ?? 0);
			$parsed['estimated_marketplace_known'] = !empty($meta['estimated_marketplace_known']);
			$parsed['arbitrage_margin_abs'] = (float) ($meta['arbitrage_margin_abs'] ?? 0);
			$parsed['arbitrage_margin_pct'] = (float) ($meta['arbitrage_margin_pct'] ?? 0);
			$parsed['list_on_marketplaces'] = (array) ($meta['list_on_marketplaces'] ?? array());
			$parsed['primary_marketplace'] = (string) ($meta['primary_marketplace'] ?? '');
			$parsed['marketplace_presence'] = (array) ($meta['marketplace_presence'] ?? array());
		}
		$parsed['last_crawl_at'] = (int) (is_array($meta) ? ($meta['last_crawl_at'] ?? 0) : 0);
		$parsed['last_updated'] = max(
			(int) $parsed['last_fetched'],
			(int) $parsed['last_crawl_at'],
			(int) ($row['updated_at'] ?? 0)
		);
		$parsed['market_confirmed'] = is_array($meta) && !empty($meta['market_confirmed']);
		$parsed['source_match_count'] = is_array($meta) ? (int) ($meta['source_match_count'] ?? 0) : 0;
		$parsed['spare247_price'] = is_array($meta) ? (float) ($meta['spare247_price'] ?? 0) : 0.0;
		$parsed['matched_sources'] = is_array($meta) ? (array) ($meta['matched_sources'] ?? array()) : array();
		$visible[] = $parsed;
	}

	if ($view === 'market_confirmed') {
		usort($visible, function ($a, $b) {
			$ca = (int) ($a['source_match_count'] ?? 0);
			$cb = (int) ($b['source_match_count'] ?? 0);
			if ($ca !== $cb) {
				return $cb <=> $ca;
			}
			return (int) ($b['last_updated'] ?? 0) <=> (int) ($a['last_updated'] ?? 0);
		});
	} elseif ($view === 'marketplace_opportunities') {
		usort($visible, function ($a, $b) {
			$ma = (float) ($a['arbitrage_margin_pct'] ?? 0);
			$mb = (float) ($b['arbitrage_margin_pct'] ?? 0);
			if ($ma !== $mb) {
				return $mb <=> $ma;
			}
			return (int) ($b['last_updated'] ?? 0) <=> (int) ($a['last_updated'] ?? 0);
		});
	}

	if ($sort === 'price_change') {
		usort($visible, function ($a, $b) {
			$da = abs((float) ($a['price_diff_pct'] ?? 0));
			$db = abs((float) ($b['price_diff_pct'] ?? 0));
			if ($da !== $db) {
				return $db <=> $da;
			}
			return (int) ($b['last_updated'] ?? 0) <=> (int) ($a['last_updated'] ?? 0);
		});
	} elseif ($sort === 'last_updated') {
		usort($visible, function ($a, $b) {
			return (int) ($b['last_updated'] ?? 0) <=> (int) ($a['last_updated'] ?? 0);
		});
	} else {
		usort($visible, function ($a, $b) {
			$pa = !empty($a['market_confirmed']) ? 1 : 0;
			$pb = !empty($b['market_confirmed']) ? 1 : 0;
			if ($pa !== $pb) {
				return $pb <=> $pa;
			}
			$sa = (int) ($a['source_match_count'] ?? 0);
			$sb = (int) ($b['source_match_count'] ?? 0);
			if ($sa !== $sb) {
				return $sb <=> $sa;
			}
			return (int) ($b['last_updated'] ?? 0) <=> (int) ($a['last_updated'] ?? 0);
		});
	}

	return array_slice($visible, 0, $limit);
}

/**
 * Discover sub-tab counts for New / Price changes views.
 *
 * @param array<string,mixed> $filters taxonomy_id
 * @return array{new:int,price_changes:int}
 */
function epc_disc_discover_counts(PDO $pdo, string $siteKey, array $filters = array()): array
{
	$views = array('market_confirmed', 'all_suggestions', 'price_changes', 'catalogue_match', 'marketplace_opportunities');
	$counts = array('market_confirmed' => 0, 'all_suggestions' => 0, 'price_changes' => 0, 'catalogue_match' => 0, 'marketplace_opportunities' => 0, 'new' => 0);
	foreach ($views as $view) {
		$viewFilters = array_merge($filters, array('view' => $view, 'limit' => 200));
		$counts[$view] = count(epc_disc_queue_list_for_discover($pdo, $siteKey, $viewFilters));
	}
	$counts['new'] = $counts['all_suggestions'];
	return $counts;
}

function epc_disc_norm_match_text(string $s): string
{
	$s = strtolower(trim(preg_replace('/\s+/', ' ', $s)));
	return preg_replace('/[^a-z0-9\s\-]/', '', $s);
}

/**
 * @param array<string,mixed> $row
 */
function epc_disc_queue_extract_sku(array $row): string
{
	$specs = is_array($row['specs'] ?? null) ? $row['specs'] : array();
	if (!$specs) {
		$specs = json_decode((string) ($row['specs_json'] ?? ''), true);
		if (!is_array($specs)) {
			$specs = array();
		}
	}
	foreach (array('SKU', 'Sku', 'sku', 'Part Number', 'Part number', 'Article', 'OE Number', 'Model', 'MPN') as $k) {
		if (!empty($specs[$k])) {
			return epc_disc_norm_match_text((string) $specs[$k]);
		}
	}
	$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
	if (is_array($meta) && !empty($meta['sku'])) {
		return epc_disc_norm_match_text((string) $meta['sku']);
	}
	return '';
}

/**
 * @param array<string,mixed> $row
 */
function epc_disc_queue_dup_key(array $row, int $catalogueProductId = 0, string $industryKey = ''): string
{
	$domain = epc_disc_norm_match_text((string) ($row['source_domain'] ?? ''));
	if ($catalogueProductId <= 0) {
		$catalogueProductId = (int) ($row['product_id'] ?? 0);
	}
	if ($catalogueProductId <= 0) {
		$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
		if (is_array($meta)) {
			$catalogueProductId = max(
				(int) ($meta['catalogue_product_id'] ?? 0),
				(int) ($meta['duplicate_of'] ?? 0)
			);
		}
	}
	if ($industryKey === '' && !empty($row['site_key'])) {
		global $db_link;
		if ($db_link instanceof PDO && function_exists('epc_apai_resolve_industry')) {
			$industryKey = epc_apai_resolve_industry($db_link, (string) $row['site_key']);
		}
	}
	if ($industryKey === 'auto_parts') {
		$baKey = epc_apai_queue_brand_article_key($row);
		if ($baKey !== '') {
			return 'ba:' . $baKey . '|src:' . $domain;
		}
	}
	$sku = epc_disc_queue_extract_sku($row);
	if ($sku !== '') {
		return 'sku:' . $sku . '|src:' . $domain;
	}
	if ($catalogueProductId > 0) {
		return 'cat:' . $catalogueProductId;
	}
	$title = epc_disc_norm_match_text((string) ($row['title'] ?? ''));
	$fallback = 'title:' . substr($title, 0, 120) . '|src:' . $domain;
	if ($industryKey === 'auto_parts') {
		return $fallback . '|needs_part_number';
	}
	return $fallback;
}

/**
 * @param array<string,mixed> $row
 * @param array<string,mixed> $diffInfo
 */
function epc_disc_queue_persist_duplicate_meta(PDO $pdo, string $siteKey, array $row, array $diffInfo): void
{
	$id = (int) ($row['id'] ?? 0);
	if ($id <= 0) {
		return;
	}
	$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
	if (!is_array($meta)) {
		$meta = array();
	}
	$catId = (int) ($diffInfo['catalogue_product_id'] ?? 0);
	$priceChanged = !empty($diffInfo['price_changed']);
	$changed = false;
	if ($catId > 0) {
		$meta['catalogue_product_id'] = $catId;
		if (!$priceChanged) {
			if ((int) ($meta['duplicate_of'] ?? 0) !== $catId) {
				$meta['duplicate_of'] = $catId;
				$changed = true;
			}
		} elseif (!empty($meta['duplicate_of'])) {
			unset($meta['duplicate_of']);
			$changed = true;
		}
	} elseif (!empty($meta['duplicate_of'])) {
		unset($meta['duplicate_of']);
		$changed = true;
	}
	$dupKey = epc_disc_queue_dup_key($row, $catId);
	if (($meta['dup_key'] ?? '') !== $dupKey) {
		$meta['dup_key'] = $dupKey;
		$changed = true;
	}
	if (!$changed) {
		return;
	}
	$now = time();
	$pdo->prepare('UPDATE `epc_product_discovery_queue` SET `meta_json` = ?, `updated_at` = ? WHERE `id` = ? AND `site_key` = ?')
		->execute(array(json_encode($meta, JSON_UNESCAPED_UNICODE), $now, $id, $siteKey));
}

function epc_disc_queue_mark_catalogue_duplicates(PDO $pdo, string $siteKey, int $limit = 200): int
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$stmt = $pdo->prepare(
		'SELECT * FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` = \'suggested\' ORDER BY `updated_at` DESC LIMIT ' . max(1, min(500, $limit))
	);
	$stmt->execute(array($siteKey));
	$rows = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
	$marked = 0;
	foreach ($rows as $row) {
		$diffInfo = epc_disc_compute_price_diff($pdo, $siteKey, (float) ($row['suggested_price'] ?? 0), $row);
		$before = (string) ($row['meta_json'] ?? '');
		epc_disc_queue_persist_duplicate_meta($pdo, $siteKey, $row, $diffInfo);
		$afterStmt = $pdo->prepare('SELECT `meta_json` FROM `epc_product_discovery_queue` WHERE `id` = ? LIMIT 1');
		$afterStmt->execute(array((int) $row['id']));
		if ($before !== (string) $afterStmt->fetchColumn()) {
			$marked++;
		}
	}
	return $marked;
}

/**
 * @return array<int,array<string,mixed>>
 */
function epc_disc_imports_fetch_rows(PDO $pdo, string $siteKey, int $limit = 250): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$limit = max(1, min(500, $limit));
	$stmt = $pdo->prepare(
		'SELECT q.*, t.`name_en` AS `taxonomy_name`, t.`slug` AS `taxonomy_slug`
		 FROM `epc_product_discovery_queue` q
		 LEFT JOIN `epc_product_taxonomy_nodes` t ON t.`id` = q.`taxonomy_node_id`
		 WHERE q.`site_key` = ? AND q.`status` IN (\'suggested\', \'imported\')
		 ORDER BY q.`updated_at` DESC LIMIT ' . $limit
	);
	$stmt->execute(array($siteKey));
	return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/**
 * @param array<string,mixed> $row
 * @return array<string,mixed>
 */
function epc_disc_imports_enrich_row(PDO $pdo, string $siteKey, array $row, bool $fastPartial = false): array
{
	$sourcePrice = (float) ($row['suggested_price'] ?? 0);
	$diffInfo = epc_disc_compute_price_diff($pdo, $siteKey, $sourcePrice, $row, $fastPartial);
	epc_disc_apply_price_meta_to_row($row, $diffInfo);
	$parsed = epc_disc_queue_parse_row($row);
	$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
	if (!is_array($meta)) {
		$meta = array();
	}
	$catId = (int) ($diffInfo['catalogue_product_id'] ?? 0);
	$parsed['price_diff_pct'] = (float) ($diffInfo['price_diff_pct'] ?? 0);
	$parsed['price_changed'] = !empty($diffInfo['price_changed']) || !empty($meta['price_changed']);
	$parsed['catalogue_price'] = (float) ($diffInfo['catalogue_price'] ?? 0);
	$parsed['catalogue_product_id'] = $catId;
	$parsed['duplicate_of'] = (int) ($meta['duplicate_of'] ?? 0);
	if ($fastPartial) {
		$parsed['dup_key'] = '';
		$parsed['sku'] = epc_disc_queue_extract_sku($row);
		$parsed['brand_article_key'] = (string) ($row['brand_article_key'] ?? '');
		$specs = is_array($parsed['specs'] ?? null) ? $parsed['specs'] : array();
		$parsed['brand'] = (string) ($specs['brand'] ?? '');
		$parsed['article_number'] = (string) ($specs['article_number'] ?? '');
		$parsed['needs_part_number'] = false;
		$parsed['last_updated'] = max((int) ($parsed['last_fetched'] ?? 0), (int) ($row['updated_at'] ?? 0));
		return $parsed;
	}
	$industryKey = function_exists('epc_apai_resolve_industry') ? epc_apai_resolve_industry($pdo, $siteKey) : 'general_retail';
	$parsed['dup_key'] = epc_disc_queue_dup_key($row, $catId, $industryKey);
	$parsed['sku'] = epc_disc_queue_extract_sku($row);
	$baKey = epc_apai_queue_brand_article_key($row);
	$parsed['brand_article_key'] = $baKey;
	$specs = is_array($parsed['specs'] ?? null) ? $parsed['specs'] : array();
	$parsed['brand'] = (string) ($specs['brand'] ?? '');
	$parsed['article_number'] = (string) ($specs['article_number'] ?? epc_apai_specs_extract_article($specs));
	$parsed['needs_part_number'] = ($industryKey === 'auto_parts' && $baKey === '');
	$parsed['last_updated'] = max(
		(int) ($parsed['last_fetched'] ?? 0),
		(int) (is_array($meta) ? ($meta['last_crawl_at'] ?? 0) : 0),
		(int) ($row['updated_at'] ?? 0)
	);
	return $parsed;
}

/**
 * @param array<int,array<string,mixed>> $enriched
 * @return array<string,array<int,array<string,mixed>>>
 */
function epc_disc_imports_build_dup_groups(array $enriched): array
{
	$groups = array();
	foreach ($enriched as $item) {
		if ((string) ($item['status'] ?? '') !== 'suggested') {
			continue;
		}
		$key = (string) ($item['dup_key'] ?? '');
		if ($key === '') {
			continue;
		}
		if (!isset($groups[$key])) {
			$groups[$key] = array();
		}
		$groups[$key][] = $item;
	}
	$out = array();
	foreach ($groups as $key => $items) {
		if (count($items) >= 2) {
			$out[$key] = $items;
		}
	}
	return $out;
}

/**
 * @param array<string,mixed> $item
 * @param array<string,array<int,array<string,mixed>>> $dupGroups
 */
function epc_disc_imports_in_dup_group(array $item, array $dupGroups): bool
{
	$key = (string) ($item['dup_key'] ?? '');
	return $key !== '' && isset($dupGroups[$key]) && count($dupGroups[$key]) >= 2;
}

/**
 * @return array{new:int,price_changes:int,duplicates:int}
 */
function epc_disc_imports_counts(PDO $pdo, string $siteKey): array
{
	epc_disc_queue_mark_catalogue_duplicates($pdo, $siteKey);
	$enriched = array();
	foreach (epc_disc_imports_fetch_rows($pdo, $siteKey) as $row) {
		$enriched[] = epc_disc_imports_enrich_row($pdo, $siteKey, $row);
	}
	$dupGroups = epc_disc_imports_build_dup_groups($enriched);
	$new = 0;
	$priceChanges = 0;
	foreach ($enriched as $item) {
		$status = (string) ($item['status'] ?? '');
		$priceChanged = !empty($item['price_changed']);
		$dupOf = (int) ($item['duplicate_of'] ?? 0);
		if ($status === 'suggested' && !$priceChanged && $dupOf <= 0 && !epc_disc_imports_in_dup_group($item, $dupGroups)) {
			$new++;
		}
		if ($priceChanged && ($status === 'imported' || ($status === 'suggested' && (int) ($item['catalogue_product_id'] ?? 0) > 0))) {
			$priceChanges++;
		}
	}
	return array(
		'new' => $new,
		'price_changes' => $priceChanges,
		'duplicates' => count($dupGroups),
	);
}

/**
 * Imports tab queue: filter = new | price_changes | duplicates.
 *
 * @return array{filter:string,items:array,groups:array,count:int,counts:array}
 */
function epc_disc_queue_list_for_imports(PDO $pdo, string $siteKey, array $filters = array()): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$filter = (string) ($filters['filter'] ?? 'new');
	if (!in_array($filter, array('new', 'price_changes', 'duplicates'), true)) {
		$filter = 'new';
	}
	$limit = max(1, min(200, (int) ($filters['limit'] ?? 60)));
	$fastPartial = !empty($filters['fast_partial']);
	$fetchLimit = $fastPartial ? max($limit * 4, 60) : 250;

	if (!$fastPartial) {
		epc_disc_queue_mark_catalogue_duplicates($pdo, $siteKey);
	}
	$enriched = array();
	foreach (epc_disc_imports_fetch_rows($pdo, $siteKey, $fetchLimit) as $row) {
		$enriched[] = epc_disc_imports_enrich_row($pdo, $siteKey, $row, $fastPartial);
	}
	$dupGroups = $fastPartial ? array() : epc_disc_imports_build_dup_groups($enriched);
	$counts = array(
		'new' => 0,
		'price_changes' => 0,
		'duplicates' => count($dupGroups),
	);
	foreach ($enriched as $item) {
		$status = (string) ($item['status'] ?? '');
		$priceChanged = !empty($item['price_changed']);
		$dupOf = (int) ($item['duplicate_of'] ?? 0);
		if ($status === 'suggested' && !$priceChanged && $dupOf <= 0 && !epc_disc_imports_in_dup_group($item, $dupGroups)) {
			$counts['new']++;
		}
		if ($priceChanged && ($status === 'imported' || ($status === 'suggested' && (int) ($item['catalogue_product_id'] ?? 0) > 0))) {
			$counts['price_changes']++;
		}
	}

	$items = array();
	$groups = array();
	if ($filter === 'new') {
		foreach ($enriched as $item) {
			$status = (string) ($item['status'] ?? '');
			if ($status !== 'suggested') {
				continue;
			}
			if (!empty($item['price_changed'])) {
				continue;
			}
			if ((int) ($item['duplicate_of'] ?? 0) > 0) {
				continue;
			}
			if (epc_disc_imports_in_dup_group($item, $dupGroups)) {
				continue;
			}
			$items[] = $item;
		}
		usort($items, function ($a, $b) {
			return (int) ($b['last_updated'] ?? 0) <=> (int) ($a['last_updated'] ?? 0);
		});
		$items = array_slice($items, 0, $limit);
	} elseif ($filter === 'price_changes') {
		foreach ($enriched as $item) {
			$status = (string) ($item['status'] ?? '');
			if (empty($item['price_changed'])) {
				continue;
			}
			if ($status === 'imported' || ((int) ($item['catalogue_product_id'] ?? 0) > 0 && $status === 'suggested')) {
				$items[] = $item;
			}
		}
		usort($items, function ($a, $b) {
			$da = abs((float) ($a['price_diff_pct'] ?? 0));
			$db = abs((float) ($b['price_diff_pct'] ?? 0));
			if ($da !== $db) {
				return $db <=> $da;
			}
			return (int) ($b['last_updated'] ?? 0) <=> (int) ($a['last_updated'] ?? 0);
		});
		$items = array_slice($items, 0, $limit);
	} else {
		if ($fastPartial) {
			$groups = array();
		} else {
		foreach ($dupGroups as $key => $groupItems) {
			usort($groupItems, function ($a, $b) {
				return (int) ($b['last_updated'] ?? 0) <=> (int) ($a['last_updated'] ?? 0);
			});
			$groups[] = array(
				'dup_key' => $key,
				'count' => count($groupItems),
				'items' => array_slice($groupItems, 0, 20),
			);
		}
		usort($groups, function ($a, $b) {
			return (int) ($b['count'] ?? 0) <=> (int) ($a['count'] ?? 0);
		});
		$groups = array_slice($groups, 0, $limit);
		}
	}

	return array(
		'filter' => $filter,
		'items' => $items,
		'groups' => $groups,
		'count' => $filter === 'duplicates' ? count($groups) : count($items),
		'counts' => $counts,
	);
}

/**
 * Keep one duplicate candidate; reject the rest. Optionally import the kept row.
 *
 * @param array<int,int> $dismissIds
 * @return array{ok:bool,kept:int,rejected:int,message:string,import?:array}
 */
function epc_disc_queue_dismiss_duplicates(PDO $pdo, string $siteKey, int $keepId, array $dismissIds = array(), bool $approveKeep = true): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$keepId = max(0, $keepId);
	if ($keepId <= 0) {
		return array('ok' => false, 'message' => 'keep_id required', 'kept' => 0, 'rejected' => 0);
	}
	$stmt = $pdo->prepare('SELECT * FROM `epc_product_discovery_queue` WHERE `id` = ? AND `site_key` = ? LIMIT 1');
	$stmt->execute(array($keepId, $siteKey));
	$keepRow = $stmt->fetch(PDO::FETCH_ASSOC);
	if (!$keepRow || (string) ($keepRow['status'] ?? '') !== 'suggested') {
		return array('ok' => false, 'message' => 'Keep item not found or already processed', 'kept' => 0, 'rejected' => 0);
	}

	if (!$dismissIds) {
		$enriched = epc_disc_imports_enrich_row($pdo, $siteKey, $keepRow);
		$dupKey = (string) ($enriched['dup_key'] ?? '');
		if ($dupKey !== '') {
			$all = epc_disc_imports_fetch_rows($pdo, $siteKey);
			foreach ($all as $row) {
				$id = (int) ($row['id'] ?? 0);
				if ($id <= 0 || $id === $keepId || (string) ($row['status'] ?? '') !== 'suggested') {
					continue;
				}
				$other = epc_disc_imports_enrich_row($pdo, $siteKey, $row);
				if ((string) ($other['dup_key'] ?? '') === $dupKey) {
					$dismissIds[] = $id;
				}
			}
		}
	}

	$rejected = 0;
	foreach (array_unique(array_filter(array_map('intval', $dismissIds))) as $did) {
		if ($did === $keepId) {
			continue;
		}
		if (epc_disc_queue_reject($pdo, $did, $siteKey)) {
			$rejected++;
		}
	}

	$importRes = null;
	if ($approveKeep) {
		$importRes = epc_disc_queue_approve_import($pdo, $siteKey, $keepId);
	}

	$msg = $approveKeep && !empty($importRes['ok'])
		? 'Kept and imported queue #' . $keepId . ', dismissed ' . $rejected . ' duplicate(s)'
		: 'Kept queue #' . $keepId . ', dismissed ' . $rejected . ' duplicate(s)';

	return array(
		'ok' => true,
		'kept' => $keepId,
		'rejected' => $rejected,
		'message' => $msg,
		'import' => is_array($importRes) ? $importRes : null,
	);
}

/**
 * @param array<string,mixed> $row
 * @return array<string,mixed>
 */
function epc_disc_queue_parse_row(array $row): array
{
	$row['images'] = json_decode((string) ($row['image_urls'] ?? ''), true);
	if (!is_array($row['images'])) {
		$row['images'] = array();
	}
	$row['local_images'] = json_decode((string) ($row['local_image_paths'] ?? ''), true);
	if (!is_array($row['local_images'])) {
		$row['local_images'] = array();
	}
	$row['specs'] = json_decode((string) ($row['specs_json'] ?? ''), true);
	if (!is_array($row['specs'])) {
		$row['specs'] = array();
	}
	$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
	if (is_array($meta) && !empty($meta['alternate_sources']) && is_array($meta['alternate_sources'])) {
		$row['alternate_sources'] = $meta['alternate_sources'];
	} else {
		$row['alternate_sources'] = array();
	}
	$row['last_fetched'] = (int) (is_array($meta) ? ($meta['last_fetched'] ?? 0) : 0);
	$row['price_diff_pct'] = (float) (is_array($meta) ? ($meta['price_diff_pct'] ?? 0) : 0);
	$row['catalogue_price'] = (float) (is_array($meta) ? ($meta['catalogue_price'] ?? 0) : 0);
	$row['source_price_range'] = (is_array($meta) && !empty($meta['source_price_range'])) ? (array) $meta['source_price_range'] : array();
	$row['range_flags'] = (is_array($meta) && !empty($meta['range_change_flags'])) ? (array) $meta['range_change_flags'] : array();
	$row['brand'] = (string) ($row['specs']['brand'] ?? epc_apai_specs_extract_brand((array) $row['specs']));
	$row['article_number'] = (string) ($row['specs']['article_number'] ?? epc_apai_specs_extract_article((array) $row['specs']));
	$row['brand_article_key'] = epc_apai_queue_brand_article_key($row);
	$row['needs_part_number'] = false;
	if (!empty($row['site_key']) && function_exists('epc_apai_resolve_industry')) {
		global $db_link;
		$pdoRef = ($db_link instanceof PDO) ? $db_link : null;
		if ($pdoRef) {
			$row['needs_part_number'] = epc_apai_resolve_industry($pdoRef, (string) $row['site_key']) === 'auto_parts' && $row['brand_article_key'] === '';
		}
	}
	return $row;
}

function epc_disc_queue_list(PDO $pdo, string $siteKey, string $status = 'suggested', int $taxonomyNodeId = 0, int $limit = 50): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$sql = 'SELECT q.*, t.`name_en` AS `taxonomy_name`, t.`slug` AS `taxonomy_slug`
		 FROM `epc_product_discovery_queue` q
		 LEFT JOIN `epc_product_taxonomy_nodes` t ON t.`id` = q.`taxonomy_node_id`
		 WHERE q.`site_key` = ?';
	$params = array($siteKey);
	if ($status !== '' && $status !== 'all') {
		$sql .= ' AND q.`status` = ?';
		$params[] = $status;
	}
	if ($taxonomyNodeId > 0) {
		$sql .= ' AND q.`taxonomy_node_id` = ?';
		$params[] = $taxonomyNodeId;
	}
	$sql .= ' ORDER BY q.`updated_at` DESC LIMIT ' . max(1, min(200, $limit));
	$stmt = $pdo->prepare($sql);
	$stmt->execute($params);
	$rows = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
	$out = array();
	foreach ($rows as $row) {
		$out[] = epc_disc_queue_parse_row($row);
	}
	return $out;
}

function epc_disc_queue_count(PDO $pdo, string $siteKey, string $status = ''): int
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if ($status === '') {
		$stmt = $pdo->prepare('SELECT COUNT(*) FROM `epc_product_discovery_queue` WHERE `site_key` = ?');
		$stmt->execute(array($siteKey));
	} else {
		$stmt = $pdo->prepare('SELECT COUNT(*) FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` = ?');
		$stmt->execute(array($siteKey, $status));
	}
	return (int) $stmt->fetchColumn();
}

function epc_disc_queue_find_existing(PDO $pdo, string $siteKey, array $fetched): ?array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$url = trim((string) ($fetched['source_url'] ?? ''));
	$title = trim((string) ($fetched['title'] ?? ''));
	if ($url !== '') {
		$stmt = $pdo->prepare(
			'SELECT * FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `source_url` = ? AND `status` IN (\'suggested\', \'imported\') LIMIT 1'
		);
		$stmt->execute(array($siteKey, $url));
		$row = $stmt->fetch(PDO::FETCH_ASSOC);
		if ($row) {
			return $row;
		}
	}
	$catId = epc_disc_find_catalogue_by_title($pdo, $title);
	if ($catId > 0) {
		$stmt = $pdo->prepare(
			'SELECT * FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `product_id` = ? AND `status` = \'imported\' LIMIT 1'
		);
		$stmt->execute(array($siteKey, $catId));
		$row = $stmt->fetch(PDO::FETCH_ASSOC);
		if ($row) {
			return $row;
		}
	}
	$industryKey = function_exists('epc_apai_resolve_industry') ? epc_apai_resolve_industry($pdo, $siteKey) : 'general_retail';
	if ($industryKey === 'auto_parts') {
		$specs = (array) ($fetched['specs'] ?? array());
		$specs = epc_apai_specs_enrich_brand_article($specs, $url, $title);
		$baKey = (string) ($specs['brand_article_key'] ?? '');
		if ($baKey !== '') {
			$stmt = $pdo->prepare(
				'SELECT * FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` IN (\'suggested\', \'imported\')
				 AND `specs_json` LIKE ? LIMIT 1'
			);
			$stmt->execute(array($siteKey, '%"brand_article_key":"' . $baKey . '"%'));
			$row = $stmt->fetch(PDO::FETCH_ASSOC);
			if ($row) {
				return $row;
			}
			$domain = (string) ($fetched['source_domain'] ?? epc_disc_domain_from_url_safe($url));
			$allStmt = $pdo->prepare(
				'SELECT * FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` IN (\'suggested\', \'imported\')
				 AND `source_domain` = ? ORDER BY `updated_at` DESC LIMIT 80'
			);
			$allStmt->execute(array($siteKey, $domain));
			foreach ($allStmt->fetchAll(PDO::FETCH_ASSOC) ?: array() as $candidate) {
				if (epc_apai_queue_brand_article_key($candidate) === $baKey) {
					return $candidate;
				}
			}
		}
	}
	if ($title !== '') {
		$domain = (string) ($fetched['source_domain'] ?? epc_disc_domain_from_url_safe($url));
		$normTitle = strtolower(trim(preg_replace('/\s+/', ' ', $title)));
		$stmt = $pdo->prepare(
			'SELECT * FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` IN (\'suggested\', \'imported\')
			 AND `source_domain` = ? AND LOWER(TRIM(`title`)) = ? LIMIT 1'
		);
		$stmt->execute(array($siteKey, $domain, $normTitle));
		$row = $stmt->fetch(PDO::FETCH_ASSOC);
		if ($row) {
			return $row;
		}
	}
	return null;
}

function epc_disc_queue_update_from_fetch(PDO $pdo, string $siteKey, array $existing, array $fetched, int $taxonomyNodeId = 0): bool
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$id = (int) ($existing['id'] ?? 0);
	if ($id <= 0) {
		return false;
	}
	$price = (float) ($fetched['price'] ?? 0);
	if ($price <= 0) {
		$price = (float) ($existing['suggested_price'] ?? 0);
	}
	$images = (array) ($fetched['images'] ?? array());
	$specs = (array) ($fetched['specs'] ?? array());
	if (!$specs) {
		$specs = json_decode((string) ($existing['specs_json'] ?? ''), true);
		if (!is_array($specs)) {
			$specs = array();
		}
	}
	$specs = epc_apai_specs_enrich_brand_article(
		$specs,
		trim((string) ($fetched['source_url'] ?? $existing['source_url'] ?? '')),
		trim((string) ($fetched['title'] ?? $existing['title'] ?? ''))
	);
	$meta = json_decode((string) ($existing['meta_json'] ?? ''), true);
	if (!is_array($meta)) {
		$meta = array();
	}
	if (!empty($fetched['alternate_sources']) && is_array($fetched['alternate_sources'])) {
		$meta['alternate_sources'] = $fetched['alternate_sources'];
	}
	$now = time();
	$meta['last_fetched'] = $now;
	$row = $existing;
	$row['suggested_price'] = $price;
	$row['specs_json'] = json_encode($specs, JSON_UNESCAPED_UNICODE);
	$row['meta_json'] = json_encode($meta, JSON_UNESCAPED_UNICODE);
	$rangeCompact = epc_disc_build_source_price_range($pdo, $siteKey, $row);
	epc_disc_persist_source_price_range_meta($meta, $rangeCompact);
	$strategyPrices = epc_apai_apply_pricing_strategy($pdo, $siteKey, $rangeCompact);
	$cost = (float) ($strategyPrices['cost'] ?? 0);
	if ($cost <= 0 && $price > 0) {
		$cost = round($price * 0.82, 2);
	}
	$sellPrice = (float) ($strategyPrices['sell_price'] ?? $existing['sell_price'] ?? 0);
	if ($sellPrice <= 0 && $price > 0) {
		$marginRow = epc_auto_price_apply_margin($pdo, $cost > 0 ? $cost : round($price * 0.82, 2), $siteKey, (string) ($existing['currency'] ?? 'AED'));
		$sellPrice = max($price, (float) ($marginRow['sell_price'] ?? $price));
	}
	$marginPct = (float) ($strategyPrices['margin_pct'] ?? 0);
	if ($marginPct <= 0 && $cost > 0) {
		$marginPct = round((($sellPrice - $cost) / $cost) * 100, 2);
	}
	$diffInfo = epc_disc_compute_price_diff($pdo, $siteKey, $price, $row);
	epc_disc_apply_price_meta_to_row($row, $diffInfo, $now);
	$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
	if (!is_array($meta)) {
		$meta = array();
	}
	$taxId = (int) ($existing['taxonomy_node_id'] ?? 0);
	if ($taxonomyNodeId > 0) {
		$taxId = $taxonomyNodeId;
	}
	$imageJson = $images
		? json_encode($images, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES)
		: (string) ($existing['image_urls'] ?? '[]');
	$pdo->prepare(
		'UPDATE `epc_product_discovery_queue` SET `title` = ?, `description` = ?, `image_urls` = ?, `suggested_price` = ?, `cost_estimate` = ?, `margin_pct` = ?, `sell_price` = ?, `taxonomy_node_id` = ?, `specs_json` = ?, `meta_json` = ?, `updated_at` = ? WHERE `id` = ? AND `site_key` = ?'
	)->execute(array(
		trim((string) ($fetched['title'] ?? $existing['title'] ?? '')),
		trim((string) ($fetched['description'] ?? $existing['description'] ?? '')),
		$imageJson,
		$price,
		$cost,
		$marginPct,
		$sellPrice,
		$taxId,
		json_encode($specs, JSON_UNESCAPED_UNICODE),
		json_encode($meta, JSON_UNESCAPED_UNICODE),
		$now,
		$id,
		$siteKey,
	));
	return false;
}

function epc_disc_queue_add_from_fetch(PDO $pdo, string $siteKey, array $fetched, int $taxonomyNodeId = 0): bool
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$title = trim((string) ($fetched['title'] ?? ''));
	$url = trim((string) ($fetched['source_url'] ?? ''));
	if ($title === '' || $url === '') {
		return false;
	}
	$existing = epc_disc_queue_find_existing($pdo, $siteKey, $fetched);
	if ($existing) {
		epc_disc_queue_update_from_fetch($pdo, $siteKey, $existing, $fetched, $taxonomyNodeId);
		return false;
	}

	$price = (float) ($fetched['price'] ?? 0);
	$images = (array) ($fetched['images'] ?? array());
	$specs = epc_apai_specs_enrich_brand_article((array) ($fetched['specs'] ?? array()), $url, $title);
	$meta = array('message' => (string) ($fetched['message'] ?? ''));
	if (function_exists('epc_apai_resolve_industry') && epc_apai_resolve_industry($pdo, $siteKey) === 'auto_parts' && empty($specs['brand_article_key'])) {
		$meta['needs_part_number'] = 1;
	}
	if (!empty($fetched['alternate_sources']) && is_array($fetched['alternate_sources'])) {
		$meta['alternate_sources'] = $fetched['alternate_sources'];
	}
	if (!empty($fetched['source_prices']) && is_array($fetched['source_prices'])) {
		$meta['source_prices'] = $fetched['source_prices'];
	}
	$draftRow = array(
		'title' => $title,
		'source_url' => $url,
		'source_domain' => (string) ($fetched['source_domain'] ?? epc_disc_domain_from_url_safe($url)),
		'suggested_price' => $price,
		'currency' => (string) ($fetched['currency'] ?? 'AED'),
		'specs_json' => json_encode($specs, JSON_UNESCAPED_UNICODE),
		'meta_json' => json_encode($meta, JSON_UNESCAPED_UNICODE),
	);
	$identityKey = epc_disc_queue_identity_key($pdo, $siteKey, $draftRow);
	if ($identityKey !== '') {
		$meta['identity_key'] = $identityKey;
		$draftRow['meta_json'] = json_encode($meta, JSON_UNESCAPED_UNICODE);
	}
	$rangeCompact = epc_disc_build_source_price_range($pdo, $siteKey, $draftRow);
	epc_disc_persist_source_price_range_meta($meta, $rangeCompact);
	$strategyPrices = epc_apai_apply_pricing_strategy($pdo, $siteKey, $rangeCompact);
	$cost = (float) ($strategyPrices['cost'] ?? 0);
	if ($cost <= 0 && $price > 0) {
		$cost = round($price * 0.82, 2);
	}
	$sellPrice = (float) ($strategyPrices['sell_price'] ?? 0);
	if ($sellPrice <= 0 && $price > 0) {
		$margin = epc_auto_price_apply_margin($pdo, $cost, $siteKey, (string) ($fetched['currency'] ?? 'AED'));
		$sellPrice = max($price, (float) $margin['sell_price']);
	}
	if ($price <= 0) {
		$price = $sellPrice;
	}
	$marginPct = (float) ($strategyPrices['margin_pct'] ?? 0);
	if ($marginPct <= 0 && $cost > 0) {
		$marginPct = round((($sellPrice - $cost) / $cost) * 100, 2);
	}
	$now = time();

	$pdo->prepare(
		'INSERT INTO `epc_product_discovery_queue`
		 (`site_key`, `status`, `title`, `description`, `image_urls`, `source_url`, `source_domain`,
		  `suggested_price`, `cost_estimate`, `margin_pct`, `sell_price`, `currency`, `taxonomy_node_id`, `specs_json`, `meta_json`, `created_at`, `updated_at`)
		 VALUES (?, \'suggested\', ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
	)->execute(array(
		$siteKey, $title, trim((string) ($fetched['description'] ?? '')),
		json_encode($images, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES),
		$url, (string) ($fetched['source_domain'] ?? epc_disc_domain_from_url_safe($url)),
		$price, $cost, $marginPct, $sellPrice,
		strtoupper(substr((string) ($fetched['currency'] ?? 'AED'), 0, 8)),
		$taxonomyNodeId,
		json_encode($specs, JSON_UNESCAPED_UNICODE),
		json_encode($meta, JSON_UNESCAPED_UNICODE),
		$now, $now,
	));
	$newId = (int) $pdo->lastInsertId();
	if ($newId > 0) {
		$rowStmt = $pdo->prepare('SELECT * FROM `epc_product_discovery_queue` WHERE `id` = ? AND `site_key` = ? LIMIT 1');
		$rowStmt->execute(array($newId, $siteKey));
		$newRow = $rowStmt->fetch(PDO::FETCH_ASSOC);
		if ($newRow) {
			$diffInfo = epc_disc_compute_price_diff($pdo, $siteKey, $price, $newRow);
			epc_disc_queue_persist_duplicate_meta($pdo, $siteKey, $newRow, $diffInfo);
		}
	}
	return true;
}

function epc_disc_domain_from_url_safe(string $url): string
{
	$host = strtolower((string) parse_url($url, PHP_URL_HOST));
	return preg_replace('/^www\./', '', $host);
}

function epc_disc_queue_reject(PDO $pdo, int $id, string $siteKey): bool
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$stmt = $pdo->prepare('UPDATE `epc_product_discovery_queue` SET `status` = \'rejected\', `updated_at` = ? WHERE `id` = ? AND `site_key` = ? AND `status` = \'suggested\'');
	$stmt->execute(array(time(), $id, $siteKey));
	return $stmt->rowCount() > 0;
}

function epc_disc_queue_approve_import(PDO $pdo, string $siteKey, int $queueId, array $options = array()): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$stmt = $pdo->prepare('SELECT * FROM `epc_product_discovery_queue` WHERE `id` = ? AND `site_key` = ? LIMIT 1');
	$stmt->execute(array($queueId, $siteKey));
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	if (!$row || (string) ($row['status'] ?? '') !== 'suggested') {
		return array('ok' => false, 'message' => 'Item not found or already processed');
	}

	$title = (string) ($row['title'] ?? '');
	$metaPre = json_decode((string) ($row['meta_json'] ?? ''), true);
	if (!is_array($metaPre)) {
		$metaPre = array();
	}
	$rangeCompact = epc_disc_build_source_price_range($pdo, $siteKey, $row);
	$strategyPrices = epc_apai_apply_pricing_strategy($pdo, $siteKey, $rangeCompact);
	$sellPrice = (float) ($strategyPrices['sell_price'] ?? $row['sell_price'] ?? $row['suggested_price'] ?? 0);
	$cost = (float) ($strategyPrices['cost'] ?? $row['cost_estimate'] ?? 0);
	if (!empty($metaPre['arbitrage_opportunity'])) {
		$arbBuy = (float) ($metaPre['buy_price_min'] ?? 0);
		$arbSell = (float) ($metaPre['estimated_marketplace_price'] ?? 0);
		if ($arbBuy > 0) {
			$cost = $arbBuy;
		}
		if ($arbSell > 0) {
			$sellPrice = $arbSell;
		} elseif ($arbBuy > 0) {
			$rules = epc_ape_rules_get($pdo, $siteKey);
			$marginPct = (float) ($rules['min_margin_percent'] ?? 15);
			$sellPrice = round($arbBuy * (1 + $marginPct / 100), 2);
		}
	}
	$description = (string) ($row['description'] ?? '');

	if (is_file(__DIR__ . '/epc_auto_price_ai_enrich.php')) {
		require_once __DIR__ . '/epc_auto_price_ai_enrich.php';
		$enriched = epc_apai_enrich_product($pdo, $siteKey, array(
			'title' => $title,
			'description' => $description,
			'source_url' => (string) ($row['source_url'] ?? ''),
			'cost_estimate' => $cost,
			'suggested_price' => $sellPrice,
		));
		$title = (string) ($enriched['title'] ?? $title);
		$description = (string) ($enriched['description'] ?? $description);
		$sellPrice = (float) ($enriched['sell_price'] ?? $sellPrice);
		if (!empty($enriched['taxonomy_node_id']) && (int) ($row['taxonomy_node_id'] ?? 0) <= 0) {
			$row['taxonomy_node_id'] = (int) $enriched['taxonomy_node_id'];
		}
	}

	$taxonomyNodeId = (int) ($row['taxonomy_node_id'] ?? 0);
	$categoryId = 0;
	$categoryCreated = false;
	if (is_file(__DIR__ . '/epc_auto_price_categories.php')) {
		require_once __DIR__ . '/epc_auto_price_categories.php';
		epc_apai_sync_categories($pdo, $siteKey);

		$categoryMode = (string) ($options['category_mode'] ?? 'auto');
		$overrideCategoryId = max(0, (int) ($options['category_id'] ?? 0));
		if ($categoryMode === '' && $overrideCategoryId > 0) {
			$categoryMode = 'override';
		}

		if ($categoryMode === 'override' && $overrideCategoryId > 0) {
			$categoryId = $overrideCategoryId;
		} else {
			$advisory = epc_apai_advise_category($pdo, $siteKey, $row);
			if ($categoryMode === 'create_from_name') {
				$advisory['action'] = 'create_taxonomy_and_category';
				$proposed = epc_apai_propose_category_from_product($title, $description);
				$advisory['proposed_name'] = $proposed['name'];
				$advisory['proposed_slug'] = $proposed['slug'];
			}
			$resolved = epc_apai_resolve_import_category($pdo, $siteKey, $row, $advisory, array(
				'category_id' => $overrideCategoryId,
				'category_mode' => $categoryMode !== '' ? $categoryMode : 'auto',
			));
			$categoryId = (int) ($resolved['category_id'] ?? 0);
			$categoryCreated = !empty($resolved['created']);
			if ((int) ($resolved['taxonomy_node_id'] ?? 0) > 0) {
				$taxonomyNodeId = (int) $resolved['taxonomy_node_id'];
			}
		}
	}

	$specs = json_decode((string) ($row['specs_json'] ?? ''), true);
	if (!is_array($specs)) {
		$specs = array();
	}
	$specs = epc_apai_specs_enrich_brand_article($specs, (string) ($row['source_url'] ?? ''), $title);
	$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	$brand = (string) ($specs['brand'] ?? '');
	$article = (string) ($specs['article_number'] ?? '');
	if (($brand === '' || $article === '') && $industryKey === 'auto_parts' && is_file(__DIR__ . '/epc_auto_price_categories.php')) {
		require_once __DIR__ . '/epc_auto_price_categories.php';
		$fromTitle = epc_apai_extract_brand_article_from_title($title);
		if ($brand === '' && $fromTitle['brand'] !== '') {
			$brand = (string) $fromTitle['brand'];
			$specs['brand'] = $brand;
		}
		if ($article === '' && $fromTitle['article'] !== '') {
			$article = (string) $fromTitle['article'];
			$specs['article_number'] = $article;
		}
		if ($brand !== '' && $article !== '') {
			$specs = epc_apai_specs_enrich_brand_article($specs);
		}
	}
	$productAlias = '';
	if ($industryKey === 'auto_parts' && $brand !== '' && $article !== '') {
		if (is_file(__DIR__ . '/epc_auto_price_categories.php')) {
			require_once __DIR__ . '/epc_auto_price_categories.php';
			$productAlias = epc_apai_product_chpu($brand, $article);
		}
	}

	$productId = epc_ape_create_catalogue_product($pdo, $title, $sellPrice, $description, $categoryId, array(
		'alias' => $productAlias,
	));
	if ($productId <= 0) {
		return array('ok' => false, 'message' => 'Failed to create catalogue product');
	}
	if ($categoryId > 0 && is_file(__DIR__ . '/epc_auto_price_categories.php')) {
		epc_apai_assign_product_category($pdo, $productId, $categoryId);
		if ($industryKey === 'auto_parts') {
			epc_apai_ensure_category_base_properties($pdo, $categoryId, $industryKey);
			if ($brand !== '' && $article !== '') {
				epc_apai_assign_product_part_properties($pdo, $productId, $categoryId, $brand, $article);
			}
		}
	}

	$imageUrls = json_decode((string) ($row['image_urls'] ?? ''), true);
	if (!is_array($imageUrls)) {
		$imageUrls = array();
	}
	$imageImport = array('imported' => 0, 'warnings' => array(), 'fallback_urls' => array(), 'local_paths' => array());
	if (!empty($options['skip_images'])) {
		// Bulk seeding: keep external URL as fallback instead of downloading (fast path).
		$imageImport['fallback_urls'] = $imageUrls;
	} elseif (is_file(__DIR__ . '/epc_auto_price_images.php')) {
		require_once __DIR__ . '/epc_auto_price_images.php';
		$imageImport = epc_auto_price_import_images($pdo, $siteKey, $productId, $imageUrls);
	}
	$localPathsJson = json_encode((array) ($imageImport['local_paths'] ?? array()), JSON_UNESCAPED_UNICODE);
	$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
	if (!is_array($meta)) {
		$meta = array();
	}
	if (!empty($imageImport['fallback_urls'])) {
		$meta['image_fallback_urls'] = $imageImport['fallback_urls'];
	}
	if (!empty($imageImport['warnings'])) {
		$meta['image_import_warnings'] = $imageImport['warnings'];
	}

	$whSrc = null;
	foreach (epc_ape_sources_list($pdo, $siteKey) as $src) {
		if (in_array((string) ($src['source_type'] ?? ''), array('warehouse', 'warehouse_supplier'), true)) {
			$whSrc = $src;
			break;
		}
	}
	if ($whSrc) {
		$baKey = (string) ($specs['brand_article_key'] ?? '');
		$externalSku = $baKey !== '' ? $baKey : ('DISC-' . $queueId);
		$whCost = $cost;
		if ($baKey !== '') {
			$whRow = epc_disc_find_warehouse_row_by_brand_article(
				$pdo,
				(string) ($specs['brand'] ?? ''),
				(string) ($specs['article_number'] ?? '')
			);
			if ((float) ($whRow['cost'] ?? 0) > 0) {
				$whCost = (float) $whRow['cost'];
			}
			if (trim((string) ($whRow['title'] ?? '')) !== '' && stripos($title, (string) $whRow['article']) === false) {
				$title = trim((string) $whRow['manufacturer']) !== ''
					? trim((string) $whRow['manufacturer']) . ' ' . trim((string) $whRow['article']) . ' — ' . trim((string) $whRow['title'])
					: $title;
			}
		}
		$whCost = $cost > 0 ? $cost : $whCost;
		$srcProductId = epc_ape_source_product_save($pdo, (int) $whSrc['id'], array(
			'product_id' => $productId,
			'external_sku' => $externalSku,
			'external_url' => (string) ($row['source_url'] ?? ''),
			'title' => $title,
			'last_price' => $sellPrice,
			'warehouse_cost' => $whCost,
		));
		$meta['brand_article_key'] = $baKey;
		$meta['external_sku'] = $externalSku;
		if ($srcProductId > 0 && $rangeCompact) {
			try {
				$fullRange = epc_disc_source_price_range($pdo, $siteKey, epc_disc_queue_identity_key($pdo, $siteKey, $row), $row);
				$srcMeta = array('source_price_range' => $rangeCompact, 'source_prices' => array());
				foreach ((array) ($fullRange['prices'] ?? array()) as $pe) {
					if (!is_array($pe)) {
						continue;
					}
					$srcMeta['source_prices'][] = array(
						'source' => (string) ($pe['source'] ?? ''),
						'price' => (float) ($pe['price'] ?? 0),
						'currency' => (string) ($pe['currency'] ?? 'AED'),
					);
				}
				$pdo->prepare('UPDATE `epc_price_source_products` SET `meta_json` = ? WHERE `id` = ?')
					->execute(array(json_encode($srcMeta, JSON_UNESCAPED_UNICODE), $srcProductId));
			} catch (Throwable $e) {
			}
		}
	}

	epc_ape_set_catalogue_storage_price($pdo, $productId, $sellPrice);
	epc_ape_create_listing($pdo, $siteKey, $productId, 'storefront', $sellPrice, array(
		'source' => 'discovery_approve',
		'discovery_queue_id' => $queueId,
		'source_url' => (string) ($row['source_url'] ?? ''),
		'source_domain' => (string) ($row['source_domain'] ?? ''),
		'specs' => $specs,
	));

	if (is_file(__DIR__ . '/epc_auto_price_storefront.php')) {
		require_once __DIR__ . '/epc_auto_price_storefront.php';
		$primaryDomain = (string) ($row['source_domain'] ?? '');
		epc_apai_source_price_save($pdo, $siteKey, array(
			'product_id' => $productId,
			'discovery_queue_id' => $queueId,
			'source_domain' => $primaryDomain,
			'source_url' => (string) ($row['source_url'] ?? ''),
			'price' => (float) ($row['suggested_price'] ?? 0),
			'currency' => (string) ($row['currency'] ?? 'AED'),
			'specs' => $specs,
			'fetched_at' => time(),
			'is_primary' => 1,
		));
		$altSources = array();
		if (!empty($meta['alternate_sources']) && is_array($meta['alternate_sources'])) {
			$altSources = $meta['alternate_sources'];
			foreach ($altSources as $alt) {
				if (!is_array($alt)) {
					continue;
				}
				epc_apai_source_price_save($pdo, $siteKey, array(
					'product_id' => $productId,
					'discovery_queue_id' => $queueId,
					'source_domain' => (string) ($alt['source_domain'] ?? ''),
					'source_url' => (string) ($alt['source_url'] ?? ''),
					'price' => (float) ($alt['price'] ?? 0),
					'currency' => (string) ($alt['currency'] ?? 'AED'),
					'specs' => (array) ($alt['specs'] ?? $specs),
					'fetched_at' => (int) ($alt['fetched_at'] ?? time()),
					'is_primary' => 0,
				));
			}
		}
		epc_apai_seed_alternate_source_prices(
			$pdo,
			$siteKey,
			$productId,
			$queueId,
			(float) ($row['suggested_price'] ?? $sellPrice),
			$specs,
			$altSources,
			$primaryDomain
		);
	}

	$meta['specs'] = $specs;
	$meta['category_id'] = $categoryId;
	$meta['taxonomy_node_id'] = $taxonomyNodeId;
	$meta['catalogue_product_id'] = $productId;
	$meta['imported_at'] = time();
	$importSourcePrice = (float) ($row['suggested_price'] ?? 0);
	$meta['import_source_price'] = $importSourcePrice;
	$meta['baseline_source_price'] = $importSourcePrice;
	$meta['import_warehouse_cost'] = $cost;
	epc_disc_persist_source_price_range_meta($meta, $rangeCompact);
	$meta['baseline_source_price_range'] = $rangeCompact;
	$meta['import_pricing_strategy'] = (string) ($strategyPrices['strategy'] ?? '');
	if (!empty($metaPre['arbitrage_opportunity'])) {
		$meta['arbitrage_import'] = 1;
		$meta['list_on_marketplaces'] = (array) ($metaPre['list_on_marketplaces'] ?? $meta['list_on_marketplaces'] ?? array());
		$meta['buy_price_min'] = (float) ($metaPre['buy_price_min'] ?? $cost);
		$meta['estimated_marketplace_price'] = (float) ($metaPre['estimated_marketplace_price'] ?? $sellPrice);
		$meta['missing_marketplaces'] = (array) ($metaPre['missing_marketplaces'] ?? array());
	}
	unset($meta['price_changed'], $meta['show_reason'], $meta['range_change_flags']);
	if ($categoryCreated) {
		$meta['category_auto_created'] = 1;
	}

	$pdo->prepare(
		'UPDATE `epc_product_discovery_queue` SET `status` = \'imported\', `product_id` = ?, `taxonomy_node_id` = ?, `local_image_paths` = ?, `meta_json` = ?, `updated_at` = ? WHERE `id` = ?'
	)->execute(array($productId, $taxonomyNodeId, $localPathsJson, json_encode($meta, JSON_UNESCAPED_UNICODE), time(), $queueId));

	$photoMsg = '';
	$importedPhotos = (int) ($imageImport['imported'] ?? 0);
	if ($importedPhotos > 0) {
		$photoMsg = ' — ' . $importedPhotos . ' photo' . ($importedPhotos === 1 ? '' : 's') . ' copied to catalogue';
	} elseif (!empty($imageImport['warnings'])) {
		$photoMsg = ' — photos not copied (using external URLs; see warnings)';
	}

	return array(
		'ok' => true,
		'product_id' => $productId,
		'category_id' => $categoryId,
		'storefront_url' => epc_ape_catalogue_product_url($pdo, $productId),
		'images_imported' => $importedPhotos,
		'image_warnings' => (array) ($imageImport['warnings'] ?? array()),
		'local_image_paths' => (array) ($imageImport['local_paths'] ?? array()),
		'specs' => $specs,
		'message' => 'Product #' . $productId . ' imported to catalogue' . ($categoryId > 0 ? ' (category #' . $categoryId . ')' : '') . ($categoryCreated ? ' — new category created' : '') . $photoMsg,
	);
}

function epc_disc_queue_refresh_images(PDO $pdo, string $siteKey, int $queueId): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$stmt = $pdo->prepare('SELECT * FROM `epc_product_discovery_queue` WHERE `id` = ? AND `site_key` = ? LIMIT 1');
	$stmt->execute(array($queueId, $siteKey));
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	if (!$row || (string) ($row['status'] ?? '') !== 'imported') {
		return array('ok' => false, 'message' => 'Imported item not found');
	}
	$productId = (int) ($row['product_id'] ?? 0);
	if ($productId <= 0) {
		return array('ok' => false, 'message' => 'No linked catalogue product');
	}
	if (!is_file(__DIR__ . '/epc_auto_price_images.php')) {
		return array('ok' => false, 'message' => 'Image module unavailable');
	}
	require_once __DIR__ . '/epc_auto_price_images.php';

	$imageUrls = json_decode((string) ($row['image_urls'] ?? ''), true);
	if (!is_array($imageUrls)) {
		$imageUrls = array();
	}
	$imageImport = epc_auto_price_import_images($pdo, $siteKey, $productId, $imageUrls, array('replace_existing' => true));
	$localPathsJson = json_encode((array) ($imageImport['local_paths'] ?? array()), JSON_UNESCAPED_UNICODE);
	$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
	if (!is_array($meta)) {
		$meta = array();
	}
	$meta['images_refreshed_at'] = time();
	if (!empty($imageImport['fallback_urls'])) {
		$meta['image_fallback_urls'] = $imageImport['fallback_urls'];
	}
	if (!empty($imageImport['warnings'])) {
		$meta['image_import_warnings'] = $imageImport['warnings'];
	}
	$pdo->prepare('UPDATE `epc_product_discovery_queue` SET `local_image_paths` = ?, `meta_json` = ?, `updated_at` = ? WHERE `id` = ?')
		->execute(array($localPathsJson, json_encode($meta, JSON_UNESCAPED_UNICODE), time(), $queueId));

	$importedPhotos = (int) ($imageImport['imported'] ?? 0);
	return array(
		'ok' => $importedPhotos > 0 || empty($imageUrls),
		'images_imported' => $importedPhotos,
		'message' => $importedPhotos > 0
			? $importedPhotos . ' photo' . ($importedPhotos === 1 ? '' : 's') . ' refreshed from source'
			: 'No photos could be downloaded from source',
		'image_warnings' => (array) ($imageImport['warnings'] ?? array()),
	);
}

function epc_disc_seed_sources(PDO $pdo, string $siteKey): int
{
	require_once __DIR__ . '/epc_discovery_adapters.php';
	require_once __DIR__ . '/epc_industry_taxonomy.php';
	if (is_file(__DIR__ . '/epc_apai_country_sources.php')) {
		require_once __DIR__ . '/epc_apai_country_sources.php';
		return epc_apai_install_country_sources($pdo, $siteKey);
	}
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	$added = 0;
	foreach (epc_disc_ae_domain_priority($industryKey) as $src) {
		$chk = $pdo->prepare('SELECT `id` FROM `epc_discovery_sources` WHERE `site_key` = ? AND `domain` = ? LIMIT 1');
		$chk->execute(array($siteKey, $src['domain']));
		if ((int) $chk->fetchColumn() > 0) {
			continue;
		}
		epc_disc_source_save($pdo, $siteKey, array(
			'source_type' => 'custom_website',
			'domain' => $src['domain'],
			'label' => $src['label'],
			'priority' => (int) $src['priority'],
			'enabled' => 1,
		));
		$added++;
	}
	$profiles = epc_apai_industry_profiles();
	$searchLabel = isset($profiles[$industryKey]['label']) ? $profiles[$industryKey]['label'] : 'UAE retail';
	$chk = $pdo->prepare('SELECT `id` FROM `epc_discovery_sources` WHERE `site_key` = ? AND `source_type` = ? LIMIT 1');
	$chk->execute(array($siteKey, 'search_engine'));
	if ((int) $chk->fetchColumn() === 0) {
		epc_disc_source_save($pdo, $siteKey, array(
			'source_type' => 'search_engine',
			'domain' => 'google.com',
			'label' => 'Google Search (.ae ' . $searchLabel . ')',
			'priority' => 5,
			'enabled' => 1,
		));
		$added++;
	}
	return $added;
}

function epc_disc_seed_queue(PDO $pdo, string $siteKey): array
{
	require_once __DIR__ . '/epc_discovery_adapters.php';
	require_once __DIR__ . '/epc_industry_taxonomy.php';

	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	$added = 0;
	foreach (epc_apai_demo_slugs_for_industry($industryKey) as $slug) {
		$node = epc_apai_tax_by_slug($pdo, $industryKey, $slug);
		if (!$node) {
			$node = epc_tax_by_slug($pdo, $slug, $industryKey);
		}
		$nodeId = $node ? (int) $node['id'] : 0;
		$nodeName = $node ? (string) $node['name_en'] : $slug;
		$products = epc_disc_demo_products_for_node($nodeName, $slug, array(), $industryKey, $pdo, $siteKey);
		foreach ($products as $prod) {
			if (epc_disc_queue_add_from_fetch($pdo, $siteKey, $prod, $nodeId)) {
				$added++;
			}
		}
	}

	$importedId = 0;
	$importStmt = $pdo->prepare(
		'SELECT `id` FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` = \'imported\' LIMIT 1'
	);
	$importStmt->execute(array($siteKey));
	if ((int) $importStmt->fetchColumn() === 0) {
		$first = $pdo->prepare(
			'SELECT `id` FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` = \'suggested\' ORDER BY `id` ASC LIMIT 1'
		);
		$first->execute(array($siteKey));
		$firstId = (int) $first->fetchColumn();
		if ($firstId > 0) {
			$res = epc_disc_queue_approve_import($pdo, $siteKey, $firstId);
			if (!empty($res['ok'])) {
				$importedId = (int) ($res['product_id'] ?? 0);
			}
		}
	}

	return array('added' => $added, 'imported_product_id' => $importedId, 'industry_key' => $industryKey);
}

function epc_disc_kpi(PDO $pdo, string $siteKey): array
{
	$industryKey = function_exists('epc_apai_resolve_industry') ? epc_apai_resolve_industry($pdo, $siteKey) : 'electronics';
	$taxCount = function_exists('epc_apai_tax_count') ? epc_apai_tax_count($pdo, $industryKey) : (function_exists('epc_tax_count') ? epc_tax_count($pdo) : 0);
	$counts = array('suggested' => 0, 'imported' => 0, 'rejected' => 0);
	try {
		$st = $pdo->prepare('SELECT `status`, COUNT(*) AS `c` FROM `epc_product_discovery_queue` WHERE `site_key` = ? GROUP BY `status`');
		$st->execute(array($siteKey));
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$status = (string) ($row['status'] ?? '');
			if (array_key_exists($status, $counts)) {
				$counts[$status] = (int) ($row['c'] ?? 0);
			}
		}
	} catch (Throwable $e) {
		$counts['suggested'] = epc_disc_queue_count($pdo, $siteKey, 'suggested');
		$counts['imported'] = epc_disc_queue_count($pdo, $siteKey, 'imported');
		$counts['rejected'] = epc_disc_queue_count($pdo, $siteKey, 'rejected');
	}
	$sources = 0;
	try {
		$srcSt = $pdo->prepare('SELECT COUNT(*) FROM `epc_discovery_sources` WHERE `site_key` = ? AND `enabled` = 1');
		$srcSt->execute(array($siteKey));
		$sources = (int) $srcSt->fetchColumn();
	} catch (Throwable $e) {
		$sources = count(epc_disc_sources_list($pdo, $siteKey));
	}
	return array(
		'suggested' => $counts['suggested'],
		'imported' => $counts['imported'],
		'rejected' => $counts['rejected'],
		'sources' => $sources,
		'taxonomy_nodes' => $taxCount,
		'industry_key' => $industryKey,
	);
}

/**
 * @return array<string,mixed>
 */
function epc_ape_guide_context(PDO $pdo, string $siteKey, string $backend, bool $isSuperCp = false): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$backend = trim($backend, '/');
	global $DP_Config;
	$host = function_exists('epc_portal_host') ? strtolower(epc_portal_host()) : strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	$isElectronicaeHost = (strpos($host, 'electronicae') !== false);
	$tenantHost = $isElectronicaeHost ? 'www.electronicae.com' : ($siteKey === 'electronicae' ? 'www.electronicae.com' : $host);
	$tenantBase = 'https://' . $tenantHost . '/' . $backend;
	$superBase = 'https://www.ecomae.com/' . $backend;
	$pageEngine = '/control/portal/epc_auto_price_engine';
	$pageGuide = '/control/portal/epc_auto_price_guide';
	$skQs = 'site_key=' . urlencode($siteKey);

	$tenantCfg = epc_ape_tenant_config_get($pdo, $siteKey);
	$demo = epc_ape_demo_product_ensure($pdo, $siteKey === 'platform' && $isSuperCp ? 'electronicae' : $siteKey);

	$industryKey = function_exists('epc_apai_resolve_industry') ? epc_apai_resolve_industry($pdo, $siteKey === 'platform' && $isSuperCp ? 'electronicae' : $siteKey) : 'electronics';
	$indProfiles = function_exists('epc_apai_industry_profiles') ? epc_apai_industry_profiles() : array();
	$indLabel = (string) (($indProfiles[$industryKey]['label'] ?? ucfirst(str_replace('_', ' ', $industryKey))));

	return array(
		'site_key' => $siteKey,
		'industry_key' => $industryKey,
		'industry_label' => $indLabel,
		'profile' => (string) ($tenantCfg['profile'] ?? 'warehouse_supplier'),
		'profiles' => epc_ape_profiles(),
		'demo' => $demo,
		'urls' => array(
			'engine_tenant' => $tenantBase . $pageEngine . '?' . $skQs,
			'engine_super' => $superBase . $pageEngine . '?' . $skQs,
			'compare_tenant' => $tenantBase . $pageEngine . '?' . $skQs . '&tab=compare',
			'compare_super' => $superBase . $pageEngine . '?' . $skQs . '&tab=compare',
			'discovery_tenant' => $tenantBase . $pageEngine . '?' . $skQs . '&tab=discovery',
			'discovery_super' => $superBase . $pageEngine . '?' . $skQs . '&tab=discovery',
			'taxonomy_tenant' => $tenantBase . $pageEngine . '?' . $skQs . '&tab=taxonomy',
			'disc_sources_tenant' => $tenantBase . $pageEngine . '?' . $skQs . '&tab=disc_sources',
			'wizard_tenant' => $tenantBase . $pageEngine . '?' . $skQs . '&tab=wizard',
			'guide_tab' => ($isSuperCp ? $superBase : $tenantBase) . $pageEngine . '?' . $skQs . '&tab=guide',
			'guide_route' => ($isSuperCp ? $superBase : $tenantBase) . $pageGuide . '?' . $skQs,
			'catalogue_cp' => $tenantBase . '/shop/catalogue/products',
			'storefront' => (string) ($demo['storefront_url'] ?? ''),
			'run_job' => 'https://' . $tenantHost . '/epc-auto-price-run.php?token=epartscart-deploy-2026&site_key=electronicae',
			'run_discovery' => 'https://' . $tenantHost . '/epc-auto-discovery-run.php?token=epartscart-deploy-2026&site_key=electronicae&taxonomy=cell-phones',
			'setup_seed' => 'https://www.ecomae.com/epc-auto-price-setup-all.php?token=epartscart-deploy-2026&apply=1&seed=1&db=docpart',
		),
		'is_super_cp' => $isSuperCp,
		'tenant_label' => $siteKey === 'electronicae' ? 'Electronicae' : ($siteKey === 'epartscart' ? 'eParts Cart' : ($siteKey === 'taxofinca' ? 'Taxofinca' : ucfirst($siteKey))),
	);
}

function epc_ape_seed_tenant_preset(PDO $pdo, string $siteKey, string $hostname = ''): array
{
	require_once __DIR__ . '/epc_industry_taxonomy.php';
	require_once __DIR__ . '/epc_discovery_adapters.php';

	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$host = strtolower($hostname);
	$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	$profiles = epc_apai_industry_profiles();
	$indProfile = $profiles[$industryKey] ?? $profiles['general_retail'];
	$apeProfile = (string) ($indProfile['profile'] ?? 'marketplace_arbitrage');

	$seeded = array('config' => false, 'sources' => 0, 'demo_product' => false, 'industry_key' => $industryKey);

	$marginPct = ($industryKey === 'auto_parts') ? 8 : (($industryKey === 'electronics') ? 12 : (($industryKey === 'tax_advisory') ? 20 : 15));
	epc_ape_tenant_config_save($pdo, $siteKey, $apeProfile, 'AED');
	$cfgRow = epc_ape_tenant_config_get($pdo, $siteKey);
	$config = (array) ($cfgRow['config'] ?? array());
	$config['industry_key'] = $industryKey;
	if ($industryKey === 'tax_advisory') {
		$config['auto_suggest_enabled'] = 0;
		$config['show_market_prices_on_frontend'] = 0;
		$config['service_benchmark_mode'] = 1;
	}
	$pdo->prepare(
		'UPDATE `epc_auto_price_tenant_config` SET `config_json` = ?, `updated_at` = ? WHERE `site_key` = ?'
	)->execute(array(json_encode($config, JSON_UNESCAPED_UNICODE), time(), $siteKey));
	epc_ape_rules_save($pdo, $siteKey, array(
		'min_margin_percent' => $marginPct,
		'auto_update_prices' => 0,
		'auto_cross_list' => 0,
		'cross_list_channels' => 'storefront',
		'schedule_hours' => 24,
		'notes' => 'Auto Price AI preset — ' . (string) ($indProfile['label'] ?? $industryKey),
	));
	$seeded['config'] = true;

	$taxSeed = epc_apai_seed_all_taxonomies($pdo);
	$seeded['taxonomy_nodes'] = (int) ($taxSeed['nodes'] ?? 0);
	$seeded['taxonomy_by_industry'] = (array) ($taxSeed['by_industry'] ?? array());

	if (is_file(__DIR__ . '/epc_auto_price_categories.php')) {
		require_once __DIR__ . '/epc_auto_price_categories.php';
		$catSync = epc_apai_sync_categories($pdo, $siteKey, $industryKey);
		$seeded['categories_synced'] = (int) ($catSync['synced'] ?? 0);
		$seeded['category_count'] = epc_apai_category_count($pdo, $siteKey, $industryKey);
	}

	$seeded['discovery_sources'] = epc_disc_seed_sources($pdo, $siteKey);
	$discSeed = epc_disc_seed_queue($pdo, $siteKey);
	$seeded['discovery_queue'] = (int) ($discSeed['added'] ?? 0);
	$seeded['discovery_imported_id'] = (int) ($discSeed['imported_product_id'] ?? 0);

	if ($industryKey === 'auto_parts' || strpos($host, 'epartscart') !== false || $siteKey === 'epartscart') {
		$matrix = epc_ape_warehouse_matrix($pdo);
		foreach ($matrix as $i => $wh) {
			$chk = $pdo->prepare('SELECT `id` FROM `epc_price_sources` WHERE `site_key` = ? AND `warehouse_id` = ? LIMIT 1');
			$chk->execute(array($siteKey, (int) $wh['storage_id']));
			if ((int) $chk->fetchColumn() > 0) {
				continue;
			}
			epc_ape_source_save($pdo, $siteKey, array(
				'source_type' => 'warehouse',
				'name' => (string) $wh['storage_name'],
				'warehouse_id' => (int) $wh['storage_id'],
				'price_list_id' => (int) $wh['price_list_id'],
				'sort_order' => 10 + $i,
				'active' => 1,
			));
			$seeded['sources']++;
		}
		if ($seeded['sources'] === 0) {
			epc_ape_source_save($pdo, $siteKey, array('source_type' => 'warehouse_supplier', 'name' => 'Default warehouse supplier', 'sort_order' => 5, 'active' => 1));
			$seeded['sources']++;
		}
	} elseif ($industryKey === 'tax_advisory') {
		$presets = array(
			array('manual', 'Competitor benchmark (manual)', 10),
			array('search_engine', 'FTA / Big4 reference search', 20),
		);
		foreach ($presets as $p) {
			$chk = $pdo->prepare('SELECT `id` FROM `epc_price_sources` WHERE `site_key` = ? AND `source_type` = ? LIMIT 1');
			$chk->execute(array($siteKey, $p[0]));
			if ((int) $chk->fetchColumn() > 0) {
				continue;
			}
			epc_ape_source_save($pdo, $siteKey, array('source_type' => $p[0], 'name' => $p[1], 'sort_order' => $p[2], 'active' => 1));
			$seeded['sources']++;
		}
	} elseif ($industryKey === 'electronics') {
		$presets = array(
			array('amazon_ae', 'Amazon.ae UAE', 10),
			array('noon', 'Noon UAE', 20),
			array('warehouse', 'Warehouse cost baseline', 5),
		);
		foreach ($presets as $p) {
			$chk = $pdo->prepare('SELECT `id` FROM `epc_price_sources` WHERE `site_key` = ? AND `source_type` = ? LIMIT 1');
			$chk->execute(array($siteKey, $p[0]));
			if ((int) $chk->fetchColumn() > 0) {
				continue;
			}
			epc_ape_source_save($pdo, $siteKey, array('source_type' => $p[0], 'name' => $p[1], 'sort_order' => $p[2], 'active' => 1));
			$seeded['sources']++;
		}
		$demoProduct = epc_ape_demo_product_ensure($pdo, $siteKey);
		$seeded['demo_product'] = (int) ($demoProduct['product_id'] ?? 0) > 0 || !empty($demoProduct['in_matrix']);
		$seeded['demo_product_id'] = (int) ($demoProduct['product_id'] ?? 0);
	}

	if ($seeded['discovery_imported_id'] > 0) {
		$seeded['demo_product'] = true;
		$seeded['demo_product_id'] = (int) $seeded['discovery_imported_id'];
	}

	if (is_file(__DIR__ . '/epc_auto_price_storefront.php')) {
		require_once __DIR__ . '/epc_auto_price_storefront.php';
		$seeded['imported_refreshed'] = epc_apai_refresh_imported_source_prices($pdo, $siteKey);
	}

	return $seeded;
}

function epc_apai_refresh_imported_source_prices(PDO $pdo, string $siteKey): int
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$stmt = $pdo->prepare(
		'SELECT * FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` = \'imported\' AND `product_id` > 0'
	);
	$stmt->execute(array($siteKey));
	$rows = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
	$updated = 0;
	foreach ($rows as $row) {
		$productId = (int) ($row['product_id'] ?? 0);
		if ($productId <= 0) {
			continue;
		}
		$specs = json_decode((string) ($row['specs_json'] ?? ''), true);
		if (!is_array($specs)) {
			$specs = array();
		}
		if (!$specs && !empty($row['title'])) {
			require_once __DIR__ . '/epc_discovery_adapters.php';
			$specs = epc_disc_extract_specs_from_html('', (string) $row['title']);
			$pdo->prepare('UPDATE `epc_product_discovery_queue` SET `specs_json` = ? WHERE `id` = ?')
				->execute(array(json_encode($specs, JSON_UNESCAPED_UNICODE), (int) $row['id']));
		}
		$chk = $pdo->prepare('SELECT COUNT(*) FROM `epc_product_source_prices` WHERE `site_key` = ? AND `product_id` = ?');
		$chk->execute(array($siteKey, $productId));
		if ((int) $chk->fetchColumn() > 0) {
			continue;
		}
		if (!is_file(__DIR__ . '/epc_auto_price_storefront.php')) {
			require_once __DIR__ . '/epc_auto_price_storefront.php';
		}
		epc_apai_source_price_save($pdo, $siteKey, array(
			'product_id' => $productId,
			'discovery_queue_id' => (int) ($row['id'] ?? 0),
			'source_domain' => (string) ($row['source_domain'] ?? ''),
			'source_url' => (string) ($row['source_url'] ?? ''),
			'price' => (float) ($row['suggested_price'] ?? 0),
			'currency' => (string) ($row['currency'] ?? 'AED'),
			'specs' => $specs,
			'fetched_at' => time(),
			'is_primary' => 1,
		));
		$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
		$alts = (is_array($meta) && !empty($meta['alternate_sources'])) ? (array) $meta['alternate_sources'] : array();
		epc_apai_seed_alternate_source_prices(
			$pdo,
			$siteKey,
			$productId,
			(int) ($row['id'] ?? 0),
			(float) ($row['suggested_price'] ?? 0),
			$specs,
			$alts,
			(string) ($row['source_domain'] ?? '')
		);
		$updated++;
	}
	return $updated;
}

/**
 * Re-fetch source URLs for suggested (or selected) queue items — updates prices + specs_json.
 *
 * @param array<int,int> $queueIds Empty = all suggested for tenant
 * @return array{ok:bool,updated:int,message:string,items:array}
 */
function epc_disc_fetch_queue_prices(PDO $pdo, string $siteKey, array $queueIds = array(), int $taxonomyNodeId = 0, array $options = array()): array
{
	require_once __DIR__ . '/epc_discovery_adapters.php';
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$queueIds = array_values(array_filter(array_map('intval', $queueIds)));
	$deep = !empty($options['deep']);
	$crawlMode = (string) ($options['mode'] ?? 'full');
	$rowLimit = ($crawlMode === 'quick') ? 40 : (count($queueIds) ? count($queueIds) : 60);
	$startedAt = microtime(true);
	$mergedSources = epc_disc_sources_for_search($pdo, $siteKey, $taxonomyNodeId, '', true);
	if ($crawlMode === 'quick') {
		$mergedSources = epc_disc_sources_for_crawl_mode($mergedSources, 'quick');
	}
	$mergedDomains = epc_disc_sources_to_domain_list($mergedSources);

	if ($queueIds) {
		$placeholders = implode(',', array_fill(0, count($queueIds), '?'));
		$stmt = $pdo->prepare(
			'SELECT * FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` IN (\'suggested\', \'imported\') AND `id` IN (' . $placeholders . ')'
		);
		$stmt->execute(array_merge(array($siteKey), $queueIds));
	} else {
		$stmt = $pdo->prepare(
			'SELECT * FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` IN (\'suggested\', \'imported\') ORDER BY `updated_at` DESC LIMIT ' . (int) $rowLimit
		);
		$stmt->execute(array($siteKey));
	}
	$rows = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
	$updated = 0;
	$items = array();
	$now = time();
	$prefetchJobs = array();
	$urlConfigs = array();
	foreach ($rows as $row) {
		$url = trim((string) ($row['source_url'] ?? ''));
		if ($url === '' || !preg_match('#^https?://#i', $url) || strpos($url, '-demo') !== false) {
			continue;
		}
		$sourceConfig = epc_disc_source_config_for_url($url, $mergedSources);
		$prefetchJobs[] = array('url' => $url, 'config' => $sourceConfig);
		$urlConfigs[$url] = $sourceConfig;
	}
	$prefetched = function_exists('epc_disc_http_fetch_parallel')
		? epc_disc_http_fetch_parallel($prefetchJobs)
		: array();
	$sourcesFetched = count($prefetched);
	$failureFlush = epc_disc_source_flush_failures($pdo);

	foreach ($rows as $row) {
		if ($crawlMode === 'quick' && (microtime(true) - $startedAt) > EPC_DISC_CRAWL_QUICK_MAX_SEC) {
			break;
		}
		$id = (int) ($row['id'] ?? 0);
		$url = trim((string) ($row['source_url'] ?? ''));
		$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
		if (!is_array($meta)) {
			$meta = array();
		}
		$price = (float) ($row['suggested_price'] ?? 0);
		$specs = json_decode((string) ($row['specs_json'] ?? ''), true);
		if (!is_array($specs)) {
			$specs = array();
		}
		$fetchedOk = false;

		if ($url !== '' && preg_match('#^https?://#i', $url) && strpos($url, '-demo') === false) {
			$sourceConfig = $urlConfigs[$url] ?? epc_disc_source_config_for_url($url, $mergedSources);
			$html = (string) ($prefetched[$url] ?? '');
			if ($deep && function_exists('epc_disc_deep_fetch_url')) {
				$base = epc_disc_fetch_url($url, $sourceConfig, $html);
				if (!empty($base['ok']) && $html !== '') {
					$fetched = array_merge($base, array('crawl_depth' => 2));
				} else {
					$fetched = epc_disc_deep_fetch_url($url, $sourceConfig);
				}
			} else {
				$fetched = epc_disc_fetch_url($url, $sourceConfig, $html);
			}
			if (!empty($fetched['ok'])) {
				$fetchedOk = true;
				if (trim((string) ($fetched['title'] ?? '')) !== '') {
					$row['title'] = (string) $fetched['title'];
				}
				if (trim((string) ($fetched['description'] ?? '')) !== '') {
					$row['description'] = (string) $fetched['description'];
				}
				if ((float) ($fetched['price'] ?? 0) > 0) {
					$price = (float) $fetched['price'];
				}
				if (!empty($fetched['specs']) && is_array($fetched['specs'])) {
					$specs = $fetched['specs'];
				}
				$specs = epc_apai_specs_enrich_brand_article($specs, $url, (string) ($row['title'] ?? ''));
				if (!empty($fetched['images']) && is_array($fetched['images'])) {
					$row['image_urls'] = json_encode($fetched['images'], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
				}
				if ($deep) {
					$meta['crawl_depth'] = max(1, (int) ($fetched['crawl_depth'] ?? 1));
					$meta['last_crawl_at'] = $now;
				}
			}
		}

		if (!$fetchedOk) {
			$jitter = (mt_rand(-3, 3) / 100);
			if ($price > 0) {
				$price = round($price * (1 + $jitter), 2);
			}
			if (!$specs && !empty($row['title'])) {
				$specs = epc_disc_extract_specs_from_html('', (string) $row['title']);
			}
			if ($mergedDomains && (strpos($url, '-demo') !== false || $url === '')) {
				$extra = epc_disc_demo_specs_and_sources((string) ($row['title'] ?? 'Product'), $price, epc_apai_resolve_industry($pdo, $siteKey), $mergedDomains, (string) ($row['source_domain'] ?? ''));
				if (!empty($extra['alternate_sources'])) {
					$meta['alternate_sources'] = (array) $extra['alternate_sources'];
				}
				if (!empty($extra['source_prices'])) {
					$meta['source_prices'] = (array) $extra['source_prices'];
				}
			}
		}

		$row['specs_json'] = json_encode($specs, JSON_UNESCAPED_UNICODE);
		$row['meta_json'] = json_encode($meta, JSON_UNESCAPED_UNICODE);
		$rangeCompact = epc_disc_build_source_price_range($pdo, $siteKey, $row);
		epc_disc_persist_source_price_range_meta($meta, $rangeCompact);
		$strategyPrices = epc_apai_apply_pricing_strategy($pdo, $siteKey, $rangeCompact);
		$cost = (float) ($strategyPrices['cost'] ?? 0);
		if ($cost <= 0 && $price > 0) {
			$cost = round($price * 0.82, 2);
		}
		$sellPrice = (float) ($strategyPrices['sell_price'] ?? 0);
		if ($sellPrice <= 0 && $price > 0) {
			$marginRow = epc_auto_price_apply_margin($pdo, $cost > 0 ? $cost : round($price * 0.82, 2), $siteKey, (string) ($row['currency'] ?? 'AED'));
			$sellPrice = max($price, (float) ($marginRow['sell_price'] ?? $price));
		}
		$marginPct = (float) ($strategyPrices['margin_pct'] ?? 0);
		if ($marginPct <= 0 && $cost > 0) {
			$marginPct = round((($sellPrice - $cost) / $cost) * 100, 2);
		}
		$meta['last_fetched'] = $now;
		$diffInfo = epc_disc_compute_price_diff($pdo, $siteKey, $price, $row);
		epc_disc_apply_price_meta_to_row($row, $diffInfo, $now);
		$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
		if (!is_array($meta)) {
			$meta = array();
		}

		$pdo->prepare(
			'UPDATE `epc_product_discovery_queue` SET `title` = ?, `suggested_price` = ?, `cost_estimate` = ?, `margin_pct` = ?, `sell_price` = ?, `specs_json` = ?, `meta_json` = ?, `updated_at` = ? WHERE `id` = ? AND `site_key` = ?'
		)->execute(array(
			(string) ($row['title'] ?? ''),
			$price,
			$cost,
			$marginPct,
			$sellPrice,
			json_encode($specs, JSON_UNESCAPED_UNICODE),
			json_encode($meta, JSON_UNESCAPED_UNICODE),
			$now,
			$id,
			$siteKey,
		));
		$updated++;
		$items[] = array(
			'id' => $id,
			'price' => $price,
			'cost' => $cost,
			'sell_price' => $sellPrice,
			'last_fetched' => $now,
			'last_crawl_at' => (int) ($meta['last_crawl_at'] ?? 0),
			'price_diff_pct' => (float) ($diffInfo['price_diff_pct'] ?? 0),
			'price_changed' => !empty($diffInfo['price_changed']),
			'catalogue_price' => (float) ($diffInfo['catalogue_price'] ?? 0),
			'source_price_range' => (array) ($diffInfo['source_price_range'] ?? $rangeCompact),
			'range_flags' => (array) ($diffInfo['range_flags'] ?? array()),
		);
	}

	epc_disc_cross_source_match($pdo, $siteKey);
	if (function_exists('epc_disc_merge_cross_source_prices')) {
		epc_disc_merge_cross_source_prices($pdo, $siteKey);
	}

	$elapsed = round(microtime(true) - $startedAt, 1);
	$srcTotal = count($mergedDomains);
	$msg = $updated > 0
		? "Updated prices for {$updated} item(s) · Crawled {$sourcesFetched}/{$srcTotal} sources in {$elapsed}s"
		: 'No queue items to refresh';
	if (!empty($failureFlush['fallbacks'])) {
		$msg .= ' · ' . implode('; ', array_values($failureFlush['fallbacks']));
	}

	return array(
		'ok' => $updated > 0,
		'updated' => $updated,
		'message' => $msg,
		'items' => $items,
		'sources_used' => count($mergedDomains),
		'source_domains' => array_column($mergedDomains, 'domain'),
		'elapsed_sec' => $elapsed,
		'sources_fetched' => $sourcesFetched,
		'sources_total' => $srcTotal,
		'fallbacks' => (array) ($failureFlush['fallbacks'] ?? array()),
		'mode' => $crawlMode,
	);
}

/**
 * Bulk approve discovery queue items into catalogue.
 *
 * @param array<int,int> $queueIds
 * @return array{ok:bool,imported:int,failed:int,results:array,message:string}
 */
function epc_disc_bulk_approve(PDO $pdo, string $siteKey, array $queueIds, array $options = array()): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$queueIds = array_values(array_unique(array_filter(array_map('intval', $queueIds))));
	epc_disc_cross_source_match($pdo, $siteKey);
	$imported = 0;
	$failed = 0;
	$results = array();
	$overrides = (array) ($options['category_overrides'] ?? array());

	usort($queueIds, function ($a, $b) use ($pdo, $siteKey) {
		$score = function ($qid) use ($pdo, $siteKey) {
			$st = $pdo->prepare('SELECT `meta_json` FROM `epc_product_discovery_queue` WHERE `id` = ? AND `site_key` = ? LIMIT 1');
			$st->execute(array((int) $qid, $siteKey));
			$meta = json_decode((string) ($st->fetchColumn() ?: ''), true);
			if (!is_array($meta)) {
				return 0;
			}
			return !empty($meta['market_confirmed']) ? 1000 + (int) ($meta['source_match_count'] ?? 0) : (int) ($meta['source_match_count'] ?? 0);
		};
		return $score($b) <=> $score($a);
	});

	foreach ($queueIds as $qid) {
		$itemOpts = array();
		if (isset($overrides[$qid]) && is_array($overrides[$qid])) {
			$itemOpts = $overrides[$qid];
		} elseif (isset($overrides[(string) $qid]) && is_array($overrides[(string) $qid])) {
			$itemOpts = $overrides[(string) $qid];
		}
		$res = epc_disc_queue_approve_import($pdo, $siteKey, $qid, $itemOpts);
		$results[] = array(
			'queue_id' => $qid,
			'ok' => !empty($res['ok']),
			'product_id' => (int) ($res['product_id'] ?? 0),
			'message' => (string) ($res['message'] ?? ''),
		);
		if (!empty($res['ok'])) {
			$imported++;
		} else {
			$failed++;
		}
	}

	return array(
		'ok' => $imported > 0,
		'imported' => $imported,
		'failed' => $failed,
		'results' => $results,
		'message' => $imported > 0
			? "Imported {$imported} of " . count($queueIds) . ' selected product(s)'
			: 'No products could be imported',
	);
}
