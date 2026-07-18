<?php
/**
 * EParts CATA — unified catalog configuration, provider registry, shared schema.
 *
 * Providers: umapi | local_tecdoc | eparts_api | carcat | laximo | levam
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

define('EPC_CATA_VERSION', '0.5.26-home-critical-bundle');
define('EPC_CATA_CP_CACHE_TTL', 300);

/**
 * @return array<string, array<string, mixed>>
 */
function epc_cata_providers(): array
{
	return array(
		'umapi' => array(
			'label' => 'UMAPI',
			'proxy' => '/api/umapi_proxy.php',
			'legacy_tables' => array('prefix' => 'epc_umapi', 'mod_table' => 'epc_umapi_modifications'),
			'enabled' => true,
			'sync_mode' => 'merge_legacy',
		),
		'local_tecdoc' => array(
			'label' => 'TecDoc 2017 (local)',
			'proxy' => '',
			'legacy_tables' => array('prefix' => 'td_', 'mod_table' => ''),
			'enabled' => true,
			'sync_mode' => 'tecdoc_import',
		),
		'eparts_api' => array(
			'label' => 'EParts API',
			'proxy' => '/api/partsapi_proxy.php',
			'legacy_tables' => array('prefix' => 'epc_partsapi', 'mod_table' => 'epc_partsapi_cars'),
			'enabled' => true,
			'sync_mode' => 'merge_legacy',
		),
		'carcat' => array(
			'label' => 'CarCat',
			'proxy' => '/api/carcat_proxy.php',
			'legacy_tables' => array(),
			'enabled' => true,
			'sync_mode' => 'on_demand',
		),
		'laximo' => array(
			'label' => 'Laximo',
			'proxy' => '',
			'legacy_tables' => array(),
			'enabled' => true,
			'sync_mode' => 'on_demand',
		),
		'levam' => array(
			'label' => 'Levam',
			'proxy' => '',
			'legacy_tables' => array(),
			'enabled' => true,
			'sync_mode' => 'on_demand',
		),
	);
}

function epc_cata_provider_ids(): array
{
	return array_keys(epc_cata_providers());
}

function epc_cata_normalize_provider(string $provider): string
{
	$provider = strtolower(preg_replace('/[^a-z0-9_]/', '', $provider));
	$aliases = array(
		'local' => 'local_tecdoc',
		'tecdoc' => 'local_tecdoc',
		'eparts' => 'eparts_api',
		'partsapi' => 'eparts_api',
	);
	if (isset($aliases[$provider])) {
		$provider = $aliases[$provider];
	}
	return in_array($provider, epc_cata_provider_ids(), true) ? $provider : '';
}

function epc_cata_file_config(): array
{
	static $cached = null;
	if ($cached !== null) {
		return $cached;
	}
	$cached = array();
	$file = $_SERVER['DOCUMENT_ROOT'] . '/config.epc-cata.php';
	if (is_file($file)) {
		$cfg = require $file;
		if (is_array($cfg)) {
			$cached = $cfg;
		}
	}
	return $cached;
}

function epc_cata_enabled_for_request(): bool
{
	if (!function_exists('epc_portal_is_auto_parts_site')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
	}
	if (function_exists('epc_portal_is_epartscart_hostname') && epc_portal_is_epartscart_hostname()) {
		return true;
	}
	if (function_exists('epc_portal_is_auto_parts_site') && epc_portal_is_auto_parts_site()) {
		$file = epc_cata_file_config();
		return !isset($file['enabled']) || !empty($file['enabled']);
	}
	return false;
}

function epc_cata_db(): ?PDO
{
	static $db = false;
	if ($db !== false) {
		return $db;
	}
	$configPath = $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	if (!is_file($configPath)) {
		$db = null;
		return null;
	}
	require_once $configPath;
	if (!class_exists('DP_Config')) {
		$db = null;
		return null;
	}
	$cfg = new DP_Config();
	if (empty($cfg->host) || empty($cfg->db)) {
		$db = null;
		return null;
	}
	try {
		$db = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			(string) $cfg->user,
			(string) $cfg->password,
			array(
				PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
				PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
				PDO::ATTR_TIMEOUT => 5,
			)
		);
		$db->exec('SET SESSION max_execution_time=8000');
	} catch (Exception $e) {
		$db = null;
	}
	return $db;
}

/**
 * @return array<string, string>
 */
function epc_cata_tables(): array
{
	return array(
		'manufacturers' => 'epc_cata_manufacturers',
		'models' => 'epc_cata_models',
		'modifications' => 'epc_cata_modifications',
		'categories' => 'epc_cata_categories',
		'articles' => 'epc_cata_articles',
		'crosses' => 'epc_cata_crosses',
		'vin_cache' => 'epc_cata_vin_cache',
		'fitment' => 'epc_cata_fitment',
		'sync_status' => 'epc_cata_sync_status',
		'sync_log' => 'epc_cata_sync_log',
	);
}

function epc_cata_ensure_tables(): bool
{
	$db = epc_cata_db();
	if (!$db) {
		return false;
	}
	$t = epc_cata_tables();
	try {
		$db->exec("CREATE TABLE IF NOT EXISTS `{$t['manufacturers']}` (
			`id` bigint unsigned NOT NULL AUTO_INCREMENT,
			`source` varchar(32) NOT NULL,
			`section` varchar(20) NOT NULL DEFAULT 'passenger',
			`ext_id` int NOT NULL DEFAULT 0,
			`name` varchar(255) NOT NULL,
			`raw_json` mediumtext NULL,
			`updated_at` int NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			UNIQUE KEY `source_section_ext` (`source`, `section`, `ext_id`),
			KEY `name` (`name`),
			KEY `source_section` (`source`, `section`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

		$db->exec("CREATE TABLE IF NOT EXISTS `{$t['models']}` (
			`id` bigint unsigned NOT NULL AUTO_INCREMENT,
			`source` varchar(32) NOT NULL,
			`section` varchar(20) NOT NULL DEFAULT 'passenger',
			`mfa_ext_id` int NOT NULL DEFAULT 0,
			`ext_id` int NOT NULL DEFAULT 0,
			`name` varchar(255) NOT NULL,
			`year_from` varchar(20) NULL,
			`year_to` varchar(20) NULL,
			`raw_json` mediumtext NULL,
			`updated_at` int NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			UNIQUE KEY `source_section_ext` (`source`, `section`, `ext_id`),
			KEY `mfa_ext` (`source`, `section`, `mfa_ext_id`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

		$db->exec("CREATE TABLE IF NOT EXISTS `{$t['modifications']}` (
			`id` bigint unsigned NOT NULL AUTO_INCREMENT,
			`source` varchar(32) NOT NULL,
			`section` varchar(20) NOT NULL DEFAULT 'passenger',
			`ms_ext_id` int NOT NULL DEFAULT 0,
			`ext_id` int NOT NULL DEFAULT 0,
			`title` varchar(255) NOT NULL,
			`raw_json` mediumtext NULL,
			`updated_at` int NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			UNIQUE KEY `source_section_ext` (`source`, `section`, `ext_id`),
			KEY `ms_ext` (`source`, `section`, `ms_ext_id`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

		$db->exec("CREATE TABLE IF NOT EXISTS `{$t['categories']}` (
			`id` bigint unsigned NOT NULL AUTO_INCREMENT,
			`source` varchar(32) NOT NULL,
			`section` varchar(20) NOT NULL DEFAULT 'passenger',
			`mod_ext_id` int NOT NULL DEFAULT 0,
			`ext_id` int NOT NULL DEFAULT 0,
			`parent_ext_id` int NOT NULL DEFAULT 0,
			`name` varchar(255) NOT NULL,
			`raw_json` mediumtext NULL,
			`updated_at` int NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			UNIQUE KEY `source_mod_ext` (`source`, `section`, `mod_ext_id`, `ext_id`),
			KEY `parent` (`source`, `section`, `mod_ext_id`, `parent_ext_id`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

		$db->exec("CREATE TABLE IF NOT EXISTS `{$t['articles']}` (
			`id` bigint unsigned NOT NULL AUTO_INCREMENT,
			`source` varchar(32) NOT NULL,
			`section` varchar(20) NOT NULL DEFAULT 'passenger',
			`mod_ext_id` int NOT NULL DEFAULT 0,
			`cat_ext_id` int NOT NULL DEFAULT 0,
			`brand` varchar(128) NOT NULL DEFAULT '',
			`article` varchar(64) NOT NULL,
			`article_norm` varchar(64) NOT NULL,
			`name` varchar(512) NOT NULL DEFAULT '',
			`raw_json` mediumtext NULL,
			`updated_at` int NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			UNIQUE KEY `source_article` (`source`, `brand`, `article_norm`, `mod_ext_id`, `cat_ext_id`),
			KEY `article_norm` (`article_norm`),
			KEY `mod_cat` (`source`, `section`, `mod_ext_id`, `cat_ext_id`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

		$db->exec("CREATE TABLE IF NOT EXISTS `{$t['crosses']}` (
			`id` bigint unsigned NOT NULL AUTO_INCREMENT,
			`source` varchar(32) NOT NULL,
			`brand` varchar(128) NOT NULL DEFAULT '',
			`article` varchar(64) NOT NULL,
			`article_norm` varchar(64) NOT NULL,
			`cross_brand` varchar(128) NOT NULL DEFAULT '',
			`cross_article` varchar(64) NOT NULL,
			`cross_article_norm` varchar(64) NOT NULL,
			`name` varchar(512) NOT NULL DEFAULT '',
			`raw_json` mediumtext NULL,
			`updated_at` int NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			UNIQUE KEY `cross_pair` (`source`, `brand`, `article_norm`, `cross_brand`, `cross_article_norm`),
			KEY `article_norm` (`article_norm`),
			KEY `cross_article_norm` (`cross_article_norm`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

		$db->exec("CREATE TABLE IF NOT EXISTS `{$t['sync_status']}` (
			`source` varchar(32) NOT NULL,
			`connected` tinyint NOT NULL DEFAULT 0,
			`status_code` int NOT NULL DEFAULT 0,
			`message` varchar(255) NULL,
			`last_checked` int NOT NULL DEFAULT 0,
			`last_success` int NOT NULL DEFAULT 0,
			`last_error` int NOT NULL DEFAULT 0,
			`manufacturers` int NOT NULL DEFAULT 0,
			`models` int NOT NULL DEFAULT 0,
			`modifications` int NOT NULL DEFAULT 0,
			`categories` int NOT NULL DEFAULT 0,
			`articles` int NOT NULL DEFAULT 0,
			`crosses` int NOT NULL DEFAULT 0,
			PRIMARY KEY (`source`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

		$db->exec("CREATE TABLE IF NOT EXISTS `{$t['sync_log']}` (
			`id` bigint unsigned NOT NULL AUTO_INCREMENT,
			`source` varchar(32) NOT NULL,
			`action` varchar(64) NOT NULL,
			`rows` int NOT NULL DEFAULT 0,
			`message` varchar(255) NULL,
			`created_at` int NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			KEY `source_created` (`source`, `created_at`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

		$db->exec("CREATE TABLE IF NOT EXISTS `{$t['vin_cache']}` (
			`id` bigint unsigned NOT NULL AUTO_INCREMENT,
			`source` varchar(32) NOT NULL,
			`vin` varchar(17) NOT NULL,
			`language` varchar(10) NOT NULL DEFAULT 'en',
			`region` varchar(10) NOT NULL DEFAULT 'WWW',
			`vehicle_count` int NOT NULL DEFAULT 0,
			`manufacturer` varchar(255) NOT NULL DEFAULT '',
			`model_label` varchar(255) NOT NULL DEFAULT '',
			`vehicles_json` mediumtext NULL,
			`raw_json` mediumtext NULL,
			`http_status` int NOT NULL DEFAULT 200,
			`updated_at` int NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			UNIQUE KEY `source_vin_lang_region` (`source`, `vin`, `language`, `region`),
			KEY `vin` (`vin`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

		$db->exec("CREATE TABLE IF NOT EXISTS `{$t['fitment']}` (
			`id` bigint unsigned NOT NULL AUTO_INCREMENT,
			`source` varchar(32) NOT NULL,
			`section` varchar(20) NOT NULL DEFAULT 'passenger',
			`art_id` int NOT NULL DEFAULT 0,
			`brand` varchar(128) NOT NULL DEFAULT '',
			`article_nr` varchar(64) NOT NULL DEFAULT '',
			`article_norm` varchar(64) NOT NULL DEFAULT '',
			`mfa_id` int NOT NULL DEFAULT 0,
			`ms_id` int NOT NULL DEFAULT 0,
			`pc_id` int NOT NULL DEFAULT 0,
			`modification_label` varchar(255) NOT NULL DEFAULT '',
			`raw_json` mediumtext NULL,
			`updated_at` int NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			UNIQUE KEY `source_article_vehicle` (`source`, `article_norm`, `brand`, `pc_id`, `mfa_id`, `ms_id`),
			KEY `article_norm` (`article_norm`),
			KEY `pc_id` (`pc_id`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

		return true;
	} catch (Exception $e) {
		return false;
	}
}

/**
 * Car-mod style default category tree when TecDoc categories are not imported.
 *
 * @return array<int, array<string, mixed>>
 */
function epc_cata_default_categories(): array
{
	$rows = array(
		array('ORDER' => 1, 'STR_ID' => 2, 'ICON_ID' => 2, 'CATEGORY_NAME' => 'Filters'),
		array('ORDER' => 2, 'STR_ID' => 1, 'ICON_ID' => 1, 'CATEGORY_NAME' => 'Service parts'),
		array('ORDER' => 3, 'STR_ID' => 4, 'ICON_ID' => 4, 'CATEGORY_NAME' => 'Suspension'),
		array('ORDER' => 4, 'STR_ID' => 5, 'ICON_ID' => 5, 'CATEGORY_NAME' => 'Brake System'),
		array('ORDER' => 5, 'STR_ID' => 7, 'ICON_ID' => 7, 'CATEGORY_NAME' => 'Damping'),
		array('ORDER' => 6, 'STR_ID' => 8, 'ICON_ID' => 8, 'CATEGORY_NAME' => 'Belt Drive'),
		array('ORDER' => 7, 'STR_ID' => 15, 'ICON_ID' => 15, 'CATEGORY_NAME' => 'Windscreen Cleaning'),
		array('ORDER' => 8, 'STR_ID' => 9, 'ICON_ID' => 9, 'CATEGORY_NAME' => 'Clutch'),
		array('ORDER' => 9, 'STR_ID' => 10, 'ICON_ID' => 10, 'CATEGORY_NAME' => 'Ignition'),
		array('ORDER' => 10, 'STR_ID' => 3, 'ICON_ID' => 3, 'CATEGORY_NAME' => 'Engine'),
		array('ORDER' => 11, 'STR_ID' => 16, 'ICON_ID' => 16, 'CATEGORY_NAME' => 'Wheel Drive'),
		array('ORDER' => 12, 'STR_ID' => 11, 'ICON_ID' => 11, 'CATEGORY_NAME' => 'Bodywork'),
		array('ORDER' => 13, 'STR_ID' => 12, 'ICON_ID' => 12, 'CATEGORY_NAME' => 'Electrics'),
		array('ORDER' => 14, 'STR_ID' => 6, 'ICON_ID' => 6, 'CATEGORY_NAME' => 'Wheels'),
		array('ORDER' => 15, 'STR_ID' => 18, 'ICON_ID' => 18, 'CATEGORY_NAME' => 'Steering'),
		array('ORDER' => 16, 'STR_ID' => 17, 'ICON_ID' => 17, 'CATEGORY_NAME' => 'Fuel Supply'),
		array('ORDER' => 17, 'STR_ID' => 26, 'ICON_ID' => 26, 'CATEGORY_NAME' => 'Fuel Mixture Formation'),
		array('ORDER' => 18, 'STR_ID' => 19, 'ICON_ID' => 19, 'CATEGORY_NAME' => 'Cooling'),
		array('ORDER' => 19, 'STR_ID' => 20, 'ICON_ID' => 20, 'CATEGORY_NAME' => 'Exhaust'),
		array('ORDER' => 20, 'STR_ID' => 24, 'ICON_ID' => 24, 'CATEGORY_NAME' => 'Axle Drive'),
		array('ORDER' => 21, 'STR_ID' => 23, 'ICON_ID' => 23, 'CATEGORY_NAME' => 'Heating, Ventilation'),
		array('ORDER' => 22, 'STR_ID' => 22, 'ICON_ID' => 22, 'CATEGORY_NAME' => 'Air Conditioning'),
		array('ORDER' => 23, 'STR_ID' => 13, 'ICON_ID' => 13, 'CATEGORY_NAME' => 'Manual Transmission'),
		array('ORDER' => 24, 'STR_ID' => 14, 'ICON_ID' => 14, 'CATEGORY_NAME' => 'Automatic Transmission'),
		array('ORDER' => 25, 'STR_ID' => 872, 'ICON_ID' => 872, 'CATEGORY_NAME' => 'Auto Chemicals'),
		array('ORDER' => 26, 'STR_ID' => 34, 'ICON_ID' => 34, 'CATEGORY_NAME' => 'Electric Drive'),
	);
	$out = array();
	foreach ($rows as $row) {
		$strId = (int) $row['STR_ID'];
		$out[] = array(
			'ORDER' => (int) $row['ORDER'],
			'STR_ID' => $strId,
			'CATEGORY_ID' => $strId,
			'ICON_ID' => (int) $row['ICON_ID'],
			'CATEGORY_NAME' => (string) $row['CATEGORY_NAME'],
			'source' => 'default_tree',
		);
	}
	return $out;
}

function epc_cata_default_categories_json_path(): string
{
	return $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cata_default_categories.json';
}

/**
 * Static JSON cache of the 26-group car-mod default tree (TecDoc CSV has no category tree).
 *
 * @return array<int, array<string, mixed>>
 */
function epc_cata_default_categories_cached(): array
{
	static $cached = null;
	if ($cached !== null) {
		return $cached;
	}
	$path = epc_cata_default_categories_json_path();
	if (is_file($path)) {
		$json = json_decode((string) file_get_contents($path), true);
		if (is_array($json) && $json) {
			$rows = epc_cata_normalize_category_rows($json);
			if ($rows) {
				$cached = $rows;
				return $cached;
			}
		}
	}
	$cached = epc_cata_default_categories();
	return $cached;
}

function epc_cata_sanitize_category_name(string $name, int $strId = 0): string
{
	$name = trim($name);
	if ($name === '' || preg_match('/^[\?\.\s\x{FFFD}]+$/u', $name)) {
		return $strId > 0 ? ('Category ' . $strId) : '';
	}
	if (preg_match('/^\?{2,}$/u', $name)) {
		return $strId > 0 ? ('Category ' . $strId) : '';
	}
	return $name;
}

/**
 * @param array<int, array<string, mixed>> $rows
 * @return array<int, array<string, mixed>>
 */
function epc_cata_normalize_category_rows(array $rows): array
{
	$out = array();
	foreach ($rows as $row) {
		if (!is_array($row)) {
			continue;
		}
		$strId = (int) ($row['STR_ID'] ?? ($row['CATEGORY_ID'] ?? ($row['ext_id'] ?? ($row['id'] ?? 0))));
		$name = epc_cata_sanitize_category_name(
			(string) ($row['CATEGORY_NAME'] ?? ($row['name'] ?? '')),
			$strId
		);
		if ($name === '') {
			continue;
		}
		$iconId = (int) ($row['ICON_ID'] ?? ($row['icon_id'] ?? $strId));
		$order = (int) ($row['ORDER'] ?? ($row['order'] ?? 0));
		$entry = array(
			'STR_ID' => $strId > 0 ? $strId : count($out) + 1,
			'CATEGORY_ID' => $strId > 0 ? $strId : count($out) + 1,
			'ICON_ID' => $iconId > 0 ? $iconId : ($strId > 0 ? $strId : count($out) + 1),
			'CATEGORY_NAME' => $name,
			'source' => (string) ($row['source'] ?? 'default_tree'),
		);
		if ($order > 0) {
			$entry['ORDER'] = $order;
		}
		$out[] = $entry;
	}
	usort($out, static function (array $a, array $b): int {
		$ao = (int) ($a['ORDER'] ?? 0);
		$bo = (int) ($b['ORDER'] ?? 0);
		if ($ao !== $bo) {
			return $ao <=> $bo;
		}
		return ((int) $a['STR_ID']) <=> ((int) $b['STR_ID']);
	});
	return $out;
}

/**
 * @return array<int, array<string, mixed>>
 */
function epc_cata_payload_data(array $payload): array
{
	if (!empty($payload['data']) && is_array($payload['data'])) {
		return $payload['data'];
	}
	if (isset($payload[0]) && is_array($payload[0])) {
		return array_values($payload);
	}
	return array();
}

function epc_cata_tecdoc_config(): array
{
	$file = epc_cata_file_config();
	$td = $file['tecdoc'] ?? null;
	if (is_array($td) && $td !== array()) {
		return $td;
	}
	$root = trim((string) ($file['tecdoc_data_root'] ?? ''));
	if ($root === '') {
		$root = '/home/ecomae/epc-tecdoc-data';
	}
	$archives = is_array($file['tecdoc_archive_paths'] ?? null) ? $file['tecdoc_archive_paths'] : array();
	$outerReady = false;
	if (!empty($file['tecdoc_archive_path']) && is_file((string) $file['tecdoc_archive_path'])) {
		$outerReady = true;
	} elseif ($root !== '') {
		foreach ($archives as $rel) {
			$candidate = rtrim($root, '/\\') . '/' . ltrim(str_replace('\\', '/', (string) $rel), '/');
			if (is_file($candidate)) {
				$outerReady = true;
				break;
			}
		}
	}
	return array(
		'data_root' => $root,
		'work_path' => (string) ($file['tecdoc_work_path'] ?? $root . '/work'),
		'dump_path' => (string) ($file['tecdoc_dump_path'] ?? $root . '/work'),
		'archive_path' => (string) ($file['tecdoc_archive_path'] ?? ''),
		'archive_paths' => $archives,
		'archives_configured' => $outerReady,
		'export_inner_name' => (string) ($file['tecdoc_export_inner_name'] ?? 'EN-EXPORT_UTF8.zip'),
		'stream_mode' => !empty($file['tecdoc_stream_mode']),
		'no_full_extract' => !empty($file['tecdoc_no_full_extract']),
		'min_year' => (int) ($file['tecdoc_min_year'] ?? 2017),
		'batch_limit' => (int) ($file['tecdoc_batch_limit'] ?? 5000),
		'table_prefix' => (string) ($file['tecdoc_table_prefix'] ?? 'td_'),
		'sql_files' => is_array($file['tecdoc_sql_files'] ?? null) ? $file['tecdoc_sql_files'] : array(),
	);
}

function epc_cata_provider_enabled(string $provider): bool
{
	$provider = epc_cata_normalize_provider($provider);
	if ($provider === '') {
		return false;
	}
	$providers = epc_cata_providers();
	if (!isset($providers[$provider]) || empty($providers[$provider]['enabled'])) {
		return false;
	}
	$file = epc_cata_file_config();
	if (isset($file['providers'][$provider]['enabled']) && empty($file['providers'][$provider]['enabled'])) {
		return false;
	}
	$saved = epc_cata_setting_get('providers_enabled', array());
	if (is_array($saved) && array_key_exists($provider, $saved)) {
		return !empty($saved[$provider]);
	}
	return true;
}

/**
 * CP webservices tab — per-provider ON/OFF map (settings + file defaults).
 *
 * @return array<string, bool>
 */
function epc_cata_providers_enabled_map(): array
{
	$saved = epc_cata_setting_get('providers_enabled', array());
	if (!is_array($saved)) {
		$saved = array();
	}
	$map = array();
	foreach (epc_cata_providers() as $id => $meta) {
		if (array_key_exists($id, $saved)) {
			$map[$id] = !empty($saved[$id]);
			continue;
		}
		$file = epc_cata_file_config();
		if (isset($file['providers'][$id]['enabled'])) {
			$map[$id] = !empty($file['providers'][$id]['enabled']);
			continue;
		}
		$map[$id] = !empty($meta['enabled']);
	}
	return $map;
}

/**
 * Local TecDoc MySQL connection settings (config.epc-local-catalog.php).
 *
 * @return array<string, mixed>
 */
function epc_cata_local_catalog_config(): array
{
	static $cfg = null;
	if ($cfg !== null) {
		return $cfg;
	}
	$cfg = array(
		'host' => '',
		'db' => '',
		'user' => '',
		'password' => '',
		'table_prefix' => 'td_',
	);
	$file = $_SERVER['DOCUMENT_ROOT'] . '/config.epc-local-catalog.php';
	if (is_file($file)) {
		$loaded = require $file;
		if (is_array($loaded)) {
			$cfg = array_merge($cfg, $loaded);
		}
	}
	return $cfg;
}

function epc_cata_local_catalog_pdo(): ?PDO
{
	static $pdo = false;
	if ($pdo !== false) {
		return $pdo ?: null;
	}
	$cfg = epc_cata_local_catalog_config();
	if (trim((string) ($cfg['host'] ?? '')) === '' || trim((string) ($cfg['db'] ?? '')) === '') {
		$pdo = null;
		return null;
	}
	try {
		$pdo = new PDO(
			'mysql:host=' . $cfg['host'] . ';dbname=' . $cfg['db'] . ';charset=utf8',
			(string) ($cfg['user'] ?? ''),
			(string) ($cfg['password'] ?? ''),
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Throwable $e) {
		$pdo = null;
	}
	return $pdo ?: null;
}

/**
 * Fast CP shell — stale cache or sync_status table only (no live COUNT / zip scans).
 *
 * @return array<string, mixed>
 */
function epc_cata_cp_status_shell(): array
{
	static $mem = null;
	if (is_array($mem)) {
		return $mem;
	}
	$cached = epc_cata_setting_get('epc_cata_cp_status_v1', null);
	if (is_array($cached) && !empty($cached['payload']) && is_array($cached['payload'])) {
		$mem = $cached['payload'];
		return $mem;
	}
	if (function_exists('epc_cata_sync_status_from_table')) {
		$mem = epc_cata_sync_status_from_table();
		return $mem;
	}
	$mem = array(
		'ok' => true,
		'version' => EPC_CATA_VERSION,
		'providers' => array(),
		'totals' => array(
			'manufacturers' => 0, 'models' => 0, 'modifications' => 0, 'categories' => 0,
			'articles' => 0, 'crosses' => 0, 'vins' => 0, 'fitment' => 0,
		),
		'origins' => array(),
		'legacy_cache' => array(),
		'legacy_vins' => array(),
		'search_log' => array('total' => 0, 'by_provider' => array(), 'by_type' => array()),
		'save_coverage' => array(),
		'protocol' => defined('EPC_CATALOG_SAVE_PROTOCOL') ? EPC_CATALOG_SAVE_PROTOCOL : '',
	);
	return $mem;
}

/**
 * Cached sync status for CP dashboard (avoid hammering DB on every page load).
 *
 * @return array<string, mixed>
 */
function epc_cata_cp_status_cached(int $ttlSeconds = 0, bool $forceRebuild = false): array
{
	if ($ttlSeconds <= 0) {
		$ttlSeconds = defined('EPC_CATA_CP_CACHE_TTL') ? (int) EPC_CATA_CP_CACHE_TTL : 300;
	}
	static $mem = null;
	static $memAt = 0;
	static $memTtl = 0;
	$now = time();
	if (!$forceRebuild && is_array($mem) && $memTtl === $ttlSeconds && ($now - $memAt) < $ttlSeconds) {
		return $mem;
	}
	$cacheKey = 'epc_cata_cp_status_v1';
	if (!$forceRebuild) {
		$cached = epc_cata_setting_get($cacheKey, null);
		if (is_array($cached) && !empty($cached['payload']) && ($now - (int) ($cached['cached_at'] ?? 0)) < $ttlSeconds) {
			$mem = $cached['payload'];
			$memAt = $now;
			$memTtl = $ttlSeconds;
			return $mem;
		}
	}
	$payload = function_exists('epc_cata_sync_status_from_table')
		? epc_cata_sync_status_from_table()
		: array('ok' => false, 'error' => 'sync status unavailable', 'version' => EPC_CATA_VERSION);
	if ($forceRebuild && function_exists('epc_cata_sync_status_payload')) {
		$payload = epc_cata_sync_status_payload();
	}
	epc_cata_setting_set($cacheKey, array('cached_at' => $now, 'payload' => $payload));
	$mem = $payload;
	$memAt = $now;
	$memTtl = $ttlSeconds;
	return $payload;
}

/**
 * Import/upload progress bundle for CP monitoring panel.
 *
 * @return array<string, mixed>
 */
function epc_cata_cp_import_monitor(bool $forceRebuild = false): array
{
	static $mem = null;
	static $memAt = 0;
	$now = time();
	$ttl = defined('EPC_CATA_CP_CACHE_TTL') ? (int) EPC_CATA_CP_CACHE_TTL : 300;
	if (!$forceRebuild && is_array($mem) && ($now - $memAt) < $ttl) {
		return $mem;
	}
	if (!$forceRebuild) {
		$cached = epc_cata_setting_get('epc_cata_cp_import_monitor_v1', null);
		if (is_array($cached) && !empty($cached['payload']) && ($now - (int) ($cached['cached_at'] ?? 0)) < $ttl) {
			$mem = $cached['payload'];
			$memAt = $now;
			return $mem;
		}
	}

	$wt = function_exists('epc_wt_import_summary') ? epc_wt_import_summary(!$forceRebuild) : array();
	$tdInv = function_exists('epc_cata_tecdoc_archive_inventory_cached')
		? epc_cata_tecdoc_archive_inventory_cached($ttl, $forceRebuild)
		: (function_exists('epc_cata_tecdoc_archive_inventory') ? epc_cata_tecdoc_archive_inventory() : array());
	$tdArchives = $tdInv['archives'] ?? array();
	$transferBytes = 0;
	$wetransferBytes = 0;
	foreach ($tdArchives as $arch) {
		$key = (string) ($arch['key'] ?? '');
		$bytes = (int) ($arch['size_bytes'] ?? 0);
		if ($key === 'transfernow') {
			$transferBytes = $bytes;
		} elseif ($key === 'wetransfer_others') {
			$wetransferBytes = $bytes;
		}
	}

	$targets = array(
		'tecdoc_transfernow_gb' => 4.7,
		'wetransfer_outer_gb' => 1.08,
		'emex_articles' => 1690000,
		'emex_prices' => 812000,
		'crossbase' => 63000000,
	);
	$emexArticles = (int) ($wt['emex_articles'] ?? 0);
	$emexPrices = (int) ($wt['emex_prices'] ?? 0);
	$crosses = (int) ($wt['crosses'] ?? 0);
	$priceStock = 0;
	$db = epc_cata_db();
	if ($db) {
		try {
			$priceStock = (int) $db->query(
				'SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE IFNULL(`exist`, 0) > 0'
			)->fetchColumn();
		} catch (Throwable $e) {
			$priceStock = 0;
		}
	}

	$mem = array(
		'ok' => true,
		'generated_at' => $now,
		'generated_label' => gmdate('Y-m-d H:i:s') . ' UTC',
		'import_phase' => (string) ($wt['import_phase'] ?? 'unknown'),
		'last_sync_label' => (string) ($wt['last_sync_label'] ?? ''),
		'rows' => array(
			'emex_articles' => $emexArticles,
			'emex_prices' => $emexPrices,
			'crosses' => $crosses,
			'price_stock_rows' => $priceStock,
		),
		'datasets' => array(
			array(
				'key' => 'tecdoc_transfernow',
				'label' => 'TecDoc TransferNow',
				'target_gb' => $targets['tecdoc_transfernow_gb'],
				'current_bytes' => $transferBytes,
				'current_gb' => round($transferBytes / 1073741824, 2),
				'pct' => $targets['tecdoc_transfernow_gb'] > 0
					? round(min(100, ($transferBytes / ($targets['tecdoc_transfernow_gb'] * 1073741824)) * 100), 1)
					: 0,
				'phase' => !empty($tdInv['export_zip_ready']) ? 'stream ready' : 'uploading',
			),
			array(
				'key' => 'wetransfer_outer',
				'label' => 'WeTransfer outer zip',
				'target_gb' => $targets['wetransfer_outer_gb'],
				'current_bytes' => $wetransferBytes,
				'current_gb' => round($wetransferBytes / 1073741824, 2),
				'pct' => $targets['wetransfer_outer_gb'] > 0
					? round(min(100, ($wetransferBytes / ($targets['wetransfer_outer_gb'] * 1073741824)) * 100), 1)
					: 0,
				'phase' => $wetransferBytes > 0 ? 'on disk' : 'missing',
			),
			array(
				'key' => 'emex_articles',
				'label' => 'Emex articles',
				'target' => $targets['emex_articles'],
				'current' => $emexArticles,
				'pct' => round(min(100, ($emexArticles / max(1, $targets['emex_articles'])) * 100), 1),
				'phase' => (string) ($wt['import_phase'] ?? 'unknown'),
			),
			array(
				'key' => 'emex_prices',
				'label' => 'Emex prices',
				'target' => $targets['emex_prices'],
				'current' => $emexPrices,
				'pct' => round(min(100, ($emexPrices / max(1, $targets['emex_prices'])) * 100), 1),
				'phase' => !empty($wt['price_source_ready']) ? 'importing' : 'waiting for xlsx',
			),
			array(
				'key' => 'crossbase',
				'label' => 'Crossbase refs',
				'target' => $targets['crossbase'],
				'current' => $crosses,
				'pct' => round(min(100, ($crosses / max(1, $targets['crossbase'])) * 100), 2),
				'phase' => $crosses > 0 ? 'batch import' : 'pending',
			),
		),
		'stock_note' => $priceStock > 0
			? ($priceStock . ' price rows with stock qty > 0')
			: 'No shop_docpart_prices_data rows with exist>0 yet — Emex import still partial or qty column empty',
	);
	$memAt = $now;
	epc_cata_setting_set('epc_cata_cp_import_monitor_v1', array('cached_at' => $now, 'payload' => $mem));
	return $mem;
}

function epc_cata_should_use_default_category_tree(string $source): bool
{
	$source = epc_cata_normalize_provider($source);
	if ($source === '' || $source === 'local_tecdoc') {
		return true;
	}
	if (function_exists('epc_tecdoc_stream_enabled') && epc_tecdoc_stream_enabled()) {
		return true;
	}
	return false;
}

function epc_cata_route_url(): string
{
	return '/eparts-cata';
}

function epc_cata_normalize_vin(string $vin): string
{
	$vin = strtoupper(preg_replace('/[^A-Z0-9]/', '', $vin));
	$len = strlen($vin);
	return ($len >= 11 && $len <= 17) ? $vin : '';
}

/**
 * Normalize unified CATA / UMAPI modification row for storefront table UI.
 *
 * @param array<string, mixed> $row
 * @return array<string, mixed>
 */
function epc_cata_modification_from_row(array $row): array
{
	$raw = array();
	if (!empty($row['raw_json'])) {
		$decoded = json_decode((string) $row['raw_json'], true);
		if (is_array($decoded)) {
			$raw = $decoded;
		}
	}
	$base = $raw ?: $row;
	$id = (int) ($base['PC_ID'] ?? ($base['ID'] ?? ($base['carId'] ?? ($row['ext_id'] ?? 0))));
	if ($id <= 0) {
		$id = (int) ($row['ext_id'] ?? 0);
	}
	$title = (string) ($base['MODIFICATION'] ?? ($base['PASSENGER_CAR'] ?? ($base['COMMERCIAL_VEHICLE'] ?? ($base['MOTORBIKE'] ?? ($row['title'] ?? ($base['title'] ?? ($base['name'] ?? '')))))));
	return array(
		'source' => (string) ($row['source'] ?? ($base['source'] ?? '')),
		'section' => (string) ($row['section'] ?? ($base['section'] ?? 'passenger')),
		'ms_ext_id' => (int) ($row['ms_ext_id'] ?? ($base['MS_ID'] ?? 0)),
		'ext_id' => $id,
		'ID' => $id,
		'carId' => $id,
		'PC_ID' => (int) ($base['PC_ID'] ?? $id),
		'title' => $title,
		'MODIFICATION' => $title,
		'PASSENGER_CAR' => (string) ($base['PASSENGER_CAR'] ?? $title),
		'power_kw' => (string) ($base['POWER_KW'] ?? ($row['power_kw'] ?? '')),
		'power_ps' => (string) ($base['POWER_PS'] ?? ($row['power_ps'] ?? '')),
		'POWER_KW' => (string) ($base['POWER_KW'] ?? ($row['power_kw'] ?? '')),
		'POWER_PS' => (string) ($base['POWER_PS'] ?? ($row['power_ps'] ?? '')),
		'fuel_type' => (string) ($base['FUEL_TYPE'] ?? ($row['fuel_type'] ?? '')),
		'FUEL_TYPE' => (string) ($base['FUEL_TYPE'] ?? ($row['fuel_type'] ?? '')),
		'engine_type' => (string) ($base['ENGINE_TYPE'] ?? ($row['engine_type'] ?? '')),
		'ENGINE_TYPE' => (string) ($base['ENGINE_TYPE'] ?? ($row['engine_type'] ?? '')),
		'engine_code' => (string) ($base['ENGINE_CODE'] ?? ($base['ENG_CODE'] ?? ($row['engine_code'] ?? ''))),
		'ENGINE_CODE' => (string) ($base['ENGINE_CODE'] ?? ($base['ENG_CODE'] ?? ($row['engine_code'] ?? ''))),
		'drive_type' => (string) ($base['DRIVE_TYPE'] ?? ($row['drive_type'] ?? '')),
		'DRIVE_TYPE' => (string) ($base['DRIVE_TYPE'] ?? ($row['drive_type'] ?? '')),
		'capacity_lt' => (string) ($base['CAPACITY_LT'] ?? ($row['capacity_lt'] ?? '')),
		'CAPACITY_LT' => (string) ($base['CAPACITY_LT'] ?? ($row['capacity_lt'] ?? '')),
		'year_from' => (string) ($base['CI_FROM'] ?? ($row['year_from'] ?? '')),
		'year_to' => (string) ($base['CI_TO'] ?? ($row['year_to'] ?? '')),
		'CI_FROM' => (string) ($base['CI_FROM'] ?? ($row['year_from'] ?? '')),
		'CI_TO' => (string) ($base['CI_TO'] ?? ($row['year_to'] ?? '')),
	);
}

function epc_cata_provider_origin(string $provider): string
{
	$provider = epc_cata_normalize_provider($provider);
	if (in_array($provider, array('local_tecdoc', 'eparts_api', 'umapi'), true)) {
		return 'inside';
	}
	return 'outside';
}

/**
 * @return array<string, array<string, int>>
 */
function epc_cata_legacy_browse_counts(): array
{
	static $cached = null;
	if ($cached !== null) {
		return $cached;
	}
	$cached = array();
	$db = epc_cata_db();
	if (!$db) {
		return $cached;
	}
	$legacyMap = array(
		'umapi' => array('epc_umapi_manufacturers', 'epc_umapi_models', 'epc_umapi_modifications'),
		'eparts_api' => array('epc_partsapi_manufacturers', 'epc_partsapi_models', 'epc_partsapi_cars'),
	);
	foreach ($legacyMap as $source => $tables) {
		$counts = array('manufacturers' => 0, 'models' => 0, 'modifications' => 0);
		try {
			foreach (array('manufacturers' => $tables[0], 'models' => $tables[1], 'modifications' => $tables[2]) as $key => $table) {
				$chk = $db->prepare('SHOW TABLES LIKE ?');
				$chk->execute(array($table));
				if ($chk->fetchColumn()) {
					$counts[$key] = (int) $db->query("SELECT COUNT(*) FROM `{$table}`")->fetchColumn();
				}
			}
		} catch (Exception $e) {
		}
		if (array_sum($counts) > 0) {
			$cached[$source] = $counts;
		}
	}
	return $cached;
}

/**
 * @return array<string, int>
 */
function epc_cata_legacy_vin_counts(): array
{
	$db = epc_cata_db();
	if (!$db) {
		return array();
	}
	$out = array();
	foreach (array('umapi' => 'epc_umapi_vin_cache', 'eparts_api' => 'epc_partsapi_vin_cache') as $source => $table) {
		try {
			$chk = $db->prepare('SHOW TABLES LIKE ?');
			$chk->execute(array($table));
			if ($chk->fetchColumn()) {
				$cnt = (int) $db->query("SELECT COUNT(*) FROM `{$table}` WHERE `vehicle_count` > 0")->fetchColumn();
				if ($cnt > 0) {
					$out[$source] = $cnt;
				}
			}
		} catch (Exception $e) {
		}
	}
	return $out;
}

function epc_cata_settings_table(): string
{
	return 'epc_cata_settings';
}

function epc_cata_settings_ensure(): bool
{
	$db = epc_cata_db();
	if (!$db) {
		return false;
	}
	$table = epc_cata_settings_table();
	try {
		$db->exec("CREATE TABLE IF NOT EXISTS `{$table}` (
			`setting_key` varchar(64) NOT NULL,
			`setting_value` mediumtext NULL,
			`updated_at` int NOT NULL DEFAULT 0,
			PRIMARY KEY (`setting_key`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8;");
		return true;
	} catch (Exception $e) {
		return false;
	}
}

function &epc_cata_settings_cache_ref(): array
{
	static $cache = array();
	return $cache;
}

function epc_cata_setting_forget(string $key): void
{
	$cache = &epc_cata_settings_cache_ref();
	unset($cache[$key]);
}

function epc_cata_setting_get(string $key, $default = null)
{
	$cache = &epc_cata_settings_cache_ref();
	if (array_key_exists($key, $cache)) {
		return $cache[$key];
	}
	$db = epc_cata_db();
	if (!$db || !epc_cata_settings_ensure()) {
		return $default;
	}
	$table = epc_cata_settings_table();
	try {
		$stmt = $db->prepare("SELECT `setting_value` FROM `{$table}` WHERE `setting_key` = ? LIMIT 1");
		$stmt->execute(array($key));
		$raw = $stmt->fetchColumn();
		if ($raw === false) {
			$cache[$key] = $default;
			return $default;
		}
		$decoded = json_decode((string) $raw, true);
		$cache[$key] = (json_last_error() === JSON_ERROR_NONE) ? $decoded : $raw;
		return $cache[$key];
	} catch (Exception $e) {
		return $default;
	}
}

function epc_cata_setting_set(string $key, $value): bool
{
	$db = epc_cata_db();
	if (!$db || !epc_cata_settings_ensure()) {
		return false;
	}
	$table = epc_cata_settings_table();
	$encoded = is_string($value) ? $value : json_encode($value, JSON_UNESCAPED_UNICODE);
	try {
		$stmt = $db->prepare(
			"INSERT INTO `{$table}` (`setting_key`, `setting_value`, `updated_at`)
			VALUES (?, ?, ?)
			ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`), `updated_at` = VALUES(`updated_at`)"
		);
		$ok = $stmt->execute(array($key, $encoded, time()));
		if ($ok) {
			epc_cata_setting_forget($key);
		}
		return $ok;
	} catch (Exception $e) {
		return false;
	}
}

function epc_cata_storefront_url(): string
{
	$lang = '';
	if (!empty($GLOBALS['multilang_params']['lang_href'])) {
		$lang = rtrim((string) $GLOBALS['multilang_params']['lang_href'], '/');
	}
	return ($lang !== '' ? $lang : '/en') . '/eparts-cata';
}

/**
 * Car-mod apanel/settings defaults mapped for EParts CATA CP → storefront.
 *
 * @return array<string, mixed>
 */
function epc_cata_presentation_defaults(): array
{
	return array(
		'default_article_view' => 'list',
		'show_category_icons' => 1,
		'warehouse_only_prices' => 1,
		'default_section' => 'passenger',
		'storefront_primary' => 'eparts-mod',
		'enable_vin_search' => 1,
		'enable_plate_search' => 1,
		'sections_enabled' => array(
			'passenger' => 1,
			'commercial' => 1,
			'motorbike' => 1,
		),
		'disabled_category_ids' => array(),
	);
}

/**
 * @return array<string, mixed>
 */
function epc_cata_presentation_config(): array
{
	$raw = epc_cata_setting_get('presentation', array());
	if (!is_array($raw)) {
		$raw = array();
	}
	$cfg = array_merge(epc_cata_presentation_defaults(), $raw);
	$sections = is_array($cfg['sections_enabled'] ?? null) ? $cfg['sections_enabled'] : array();
	$defaults = epc_cata_presentation_defaults()['sections_enabled'];
	foreach ($defaults as $key => $on) {
		if (!array_key_exists($key, $sections)) {
			$sections[$key] = $on;
		} else {
			$sections[$key] = !empty($sections[$key]);
		}
	}
	$cfg['sections_enabled'] = $sections;
	$disabled = $cfg['disabled_category_ids'] ?? array();
	if (!is_array($disabled)) {
		$disabled = array();
	}
	$cfg['disabled_category_ids'] = array_values(array_unique(array_filter(array_map('intval', $disabled))));
	foreach (array('show_category_icons', 'warehouse_only_prices', 'enable_vin_search', 'enable_plate_search') as $boolKey) {
		$cfg[$boolKey] = !empty($cfg[$boolKey]);
	}
	$view = strtolower((string) ($cfg['default_article_view'] ?? 'list'));
	if (!in_array($view, array('list', 'card', 'compact'), true)) {
		$view = 'list';
	}
	$cfg['default_article_view'] = $view;
	$section = strtolower((string) ($cfg['default_section'] ?? 'passenger'));
	if (!in_array($section, array('passenger', 'commercial', 'motorbike'), true)) {
		$section = 'passenger';
	}
	if (empty($sections[$section])) {
		foreach ($sections as $secKey => $secOn) {
			if ($secOn) {
				$section = $secKey;
				break;
			}
		}
	}
	$cfg['default_section'] = $section;
	return $cfg;
}

/**
 * Category tree for storefront (CP can disable groups from the 26-group default tree).
 *
 * @return array<int, array<string, mixed>>
 */
function epc_cata_default_subcategories_json_path(): string
{
	return $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cata_default_subcategories.json';
}

/**
 * Car-mod style subcategories keyed by parent STR_ID (hover flyout + sidebar tree).
 *
 * @return array<string, array<int, array<string, mixed>>>
 */
function epc_cata_default_subcategories_cached(): array
{
	static $cached = null;
	if ($cached !== null) {
		return $cached;
	}
	$cached = array();
	$path = epc_cata_default_subcategories_json_path();
	if (is_file($path)) {
		$json = json_decode((string) file_get_contents($path), true);
		if (is_array($json)) {
			foreach ($json as $parentId => $rows) {
				if (!is_array($rows)) {
					continue;
				}
				$parentKey = (string) (int) $parentId;
				$children = array();
				foreach ($rows as $row) {
					if (!is_array($row)) {
						continue;
					}
					$name = trim((string) ($row['name'] ?? ($row['CATEGORY_NAME'] ?? '')));
					if ($name === '') {
						continue;
					}
					$subId = (string) ($row['id'] ?? ($row['STR_ID'] ?? ''));
					$children[] = array(
						'STR_ID' => $subId,
						'CATEGORY_ID' => $subId,
						'CATEGORY_NAME' => $name,
						'name' => $name,
						'PARENT_STR_ID' => (int) $parentKey,
					);
				}
				if ($children) {
					$cached[$parentKey] = $children;
				}
			}
		}
	}
	return $cached;
}

/**
 * @param array<int, array<string, mixed>> $categories
 * @return array<int, array<string, mixed>>
 */
function epc_cata_attach_subcategories(array $categories): array
{
	$subMap = epc_cata_default_subcategories_cached();
	if (!$subMap) {
		return $categories;
	}
	foreach ($categories as $idx => $row) {
		if (!is_array($row)) {
			continue;
		}
		$id = (string) (int) ($row['STR_ID'] ?? ($row['CATEGORY_ID'] ?? 0));
		if ($id !== '0' && !empty($subMap[$id])) {
			$categories[$idx]['children'] = $subMap[$id];
		}
	}
	return $categories;
}

/**
 * Storefront category overrides (CP Categories tab) — JSON in epc_cata_settings.
 */
function epc_cata_category_config_setting_key(): string
{
	return 'storefront_categories';
}

function epc_cata_category_icon_dir(): string
{
	return $_SERVER['DOCUMENT_ROOT'] . '/content/files/epc-cata/category-icons/';
}

/**
 * @return array<int, int>
 */
function epc_cata_category_icons_available(): array
{
	static $cached = null;
	if ($cached !== null) {
		return $cached;
	}
	$cached = array();
	$dir = epc_cata_category_icon_dir();
	if (is_dir($dir)) {
		foreach (glob($dir . '*.png') ?: array() as $file) {
			$id = (int) preg_replace('/\.png$/i', '', basename($file));
			if ($id > 0) {
				$cached[] = $id;
			}
		}
		sort($cached, SORT_NUMERIC);
	}
	return $cached;
}

/**
 * @return array<string, mixed>|null
 */
function epc_cata_category_config_raw(): ?array
{
	$raw = epc_cata_setting_get(epc_cata_category_config_setting_key(), null);
	if (!is_array($raw) || empty($raw['categories']) || !is_array($raw['categories'])) {
		return null;
	}
	return $raw;
}

/**
 * @param mixed $subs
 * @return array<int, array<string, string>>
 */
function epc_cata_category_config_normalize_subcategories($subs): array
{
	if (!is_array($subs)) {
		return array();
	}
	$out = array();
	foreach ($subs as $sub) {
		if (!is_array($sub)) {
			continue;
		}
		$name = trim((string) ($sub['name'] ?? ($sub['CATEGORY_NAME'] ?? '')));
		if ($name === '') {
			continue;
		}
		$subId = trim((string) ($sub['STR_ID'] ?? ($sub['id'] ?? ($sub['CATEGORY_ID'] ?? ''))));
		if ($subId === '') {
			$subId = (string) (count($out) + 1);
		}
		$entry = array('STR_ID' => $subId, 'name' => $name);
		$filter = trim((string) ($sub['product_filter'] ?? ''));
		if ($filter !== '') {
			$entry['product_filter'] = $filter;
		}
		$out[] = $entry;
	}
	return $out;
}

/**
 * @return array<string, mixed>|null
 */
function epc_cata_category_config_normalize_entry(array $row, int $fallbackOrder = 0): ?array
{
	$strId = (int) ($row['STR_ID'] ?? ($row['str_id'] ?? ($row['CATEGORY_ID'] ?? ($row['id'] ?? 0))));
	$defaultName = epc_cata_sanitize_category_name(
		(string) ($row['default_name'] ?? ($row['CATEGORY_NAME'] ?? ($row['name'] ?? ''))),
		$strId
	);
	if ($defaultName === '' && $strId > 0) {
		foreach (epc_cata_default_categories_cached() as $def) {
			if ((int) ($def['STR_ID'] ?? 0) === $strId) {
				$defaultName = epc_cata_sanitize_category_name((string) ($def['CATEGORY_NAME'] ?? ''), $strId);
				break;
			}
		}
	}
	$displayName = trim((string) ($row['display_name'] ?? ''));
	$name = $displayName !== '' ? epc_cata_sanitize_category_name($displayName, $strId) : $defaultName;
	if ($strId <= 0 || $name === '') {
		return null;
	}
	$iconId = (int) ($row['ICON_ID'] ?? ($row['icon_id'] ?? $strId));
	$order = (int) ($row['ORDER'] ?? ($row['order'] ?? $fallbackOrder));
	$enabled = !array_key_exists('enabled', $row) || !empty($row['enabled']);
	$entry = array(
		'STR_ID' => $strId,
		'CATEGORY_ID' => $strId,
		'ICON_ID' => $iconId > 0 ? $iconId : $strId,
		'CATEGORY_NAME' => $name,
		'default_name' => $defaultName !== '' ? $defaultName : $name,
		'ORDER' => $order > 0 ? $order : ($fallbackOrder > 0 ? $fallbackOrder : 1),
		'enabled' => $enabled,
		'catalog_target' => epc_cata_normalize_catalog_target((string) ($row['catalog_target'] ?? 'eparts-mod')),
		'subcategories' => epc_cata_category_config_normalize_subcategories($row['subcategories'] ?? null),
		'source' => (string) ($row['source'] ?? 'cp'),
	);
	if ($displayName !== '') {
		$entry['display_name'] = $displayName;
	}
	$ptId = (int) ($row['tecdoc_pt_id'] ?? ($row['pt_id'] ?? 0));
	if ($ptId > 0) {
		$entry['tecdoc_pt_id'] = $ptId;
	}
	$umapiId = trim((string) ($row['umapi_category_id'] ?? ($row['umapi_id'] ?? '')));
	if ($umapiId !== '') {
		$entry['umapi_category_id'] = $umapiId;
	}
	return $entry;
}

/**
 * Seed rows from 26-tree defaults + subcategories + legacy category_mappings.
 *
 * @return array<int, array<string, mixed>>
 */
function epc_cata_category_config_seed(): array
{
	$defaults = epc_cata_default_categories_cached();
	$subMap = epc_cata_default_subcategories_cached();
	$disabled = epc_cata_presentation_config()['disabled_category_ids'] ?? array();
	$legacyMap = array();
	$legacyRaw = epc_cata_setting_get('category_mappings', array());
	if (is_array($legacyRaw)) {
		foreach ($legacyRaw as $key => $row) {
			if (!is_array($row)) {
				continue;
			}
			$strId = (int) ($row['str_id'] ?? ($row['STR_ID'] ?? $key));
			if ($strId > 0) {
				$legacyMap[$strId] = $row;
			}
		}
	}
	$rows = array();
	$order = 1;
	foreach ($defaults as $row) {
		$strId = (int) ($row['STR_ID'] ?? 0);
		if ($strId <= 0) {
			continue;
		}
		$key = (string) $strId;
		$subs = array();
		if (!empty($subMap[$key])) {
			foreach ($subMap[$key] as $sub) {
				if (!is_array($sub)) {
					continue;
				}
				$subs[] = array(
					'STR_ID' => (string) ($sub['STR_ID'] ?? ($sub['id'] ?? '')),
					'name' => (string) ($sub['CATEGORY_NAME'] ?? ($sub['name'] ?? '')),
				);
			}
		}
		$legacy = $legacyMap[$strId] ?? array();
		$display = trim((string) ($legacy['display_name'] ?? ''));
		$catName = (string) ($row['CATEGORY_NAME'] ?? '');
		$rows[] = array(
			'STR_ID' => $strId,
			'CATEGORY_ID' => $strId,
			'ICON_ID' => (int) ($legacy['icon_id'] ?? ($row['ICON_ID'] ?? $strId)),
			'CATEGORY_NAME' => $display !== '' ? $display : $catName,
			'default_name' => $catName,
			'display_name' => $display,
			'ORDER' => (int) ($row['ORDER'] ?? $order),
			'enabled' => !in_array($strId, $disabled, true),
			'catalog_target' => epc_cata_normalize_catalog_target((string) ($legacy['catalog_target'] ?? 'eparts-mod')),
			'tecdoc_pt_id' => (int) ($legacy['tecdoc_pt_id'] ?? 0),
			'umapi_category_id' => trim((string) ($legacy['umapi_category_id'] ?? '')),
			'subcategories' => !empty($legacy['subcategories']) && is_array($legacy['subcategories'])
				? epc_cata_category_config_normalize_subcategories($legacy['subcategories'])
				: $subs,
			'source' => 'default_tree',
		);
		$order++;
	}
	return $rows;
}

/**
 * All categories for CP (includes disabled).
 *
 * @return array<int, array<string, mixed>>
 */
function epc_cata_category_config_rows_for_cp(): array
{
	$seed = array();
	foreach (epc_cata_category_config_seed() as $row) {
		$seed[(int) $row['STR_ID']] = $row;
	}
	$saved = epc_cata_category_config_raw();
	if ($saved === null) {
		return array_values($seed);
	}
	$merged = array();
	$seen = array();
	foreach ($saved['categories'] as $idx => $row) {
		if (!is_array($row)) {
			continue;
		}
		if (empty($row['default_name']) && !empty($seed[(int) ($row['STR_ID'] ?? 0)]['default_name'])) {
			$row['default_name'] = $seed[(int) $row['STR_ID']]['default_name'];
		}
		$norm = epc_cata_category_config_normalize_entry($row, $idx + 1);
		if ($norm === null) {
			continue;
		}
		$strId = (int) $norm['STR_ID'];
		if (!$norm['subcategories'] && !empty($seed[$strId]['subcategories'])) {
			$norm['subcategories'] = $seed[$strId]['subcategories'];
		}
		if (empty($norm['default_name']) && !empty($seed[$strId]['default_name'])) {
			$norm['default_name'] = $seed[$strId]['default_name'];
		}
		$merged[$strId] = $norm;
		$seen[$strId] = true;
	}
	foreach ($seed as $strId => $row) {
		if (empty($seen[$strId])) {
			$merged[$strId] = $row;
		}
	}
	$rows = array_values($merged);
	usort($rows, static function (array $a, array $b): int {
		$ao = (int) ($a['ORDER'] ?? 0);
		$bo = (int) ($b['ORDER'] ?? 0);
		if ($ao !== $bo) {
			return $ao <=> $bo;
		}
		return ((int) $a['STR_ID']) <=> ((int) $b['STR_ID']);
	});
	return $rows;
}

/**
 * @return array<string, array<int, array<string, mixed>>>
 */
function epc_cata_storefront_subcategories_map(): array
{
	$map = array();
	foreach (epc_cata_category_config_rows_for_cp() as $row) {
		if (empty($row['enabled'])) {
			continue;
		}
		$strId = (string) (int) $row['STR_ID'];
		$subs = $row['subcategories'] ?? array();
		if (!$subs) {
			continue;
		}
		$children = array();
		foreach ($subs as $sub) {
			if (!is_array($sub)) {
				continue;
			}
			$name = trim((string) ($sub['name'] ?? ''));
			if ($name === '') {
				continue;
			}
			$subId = (string) ($sub['STR_ID'] ?? '');
			$child = array(
				'STR_ID' => $subId,
				'CATEGORY_ID' => $subId,
				'CATEGORY_NAME' => $name,
				'name' => $name,
				'PARENT_STR_ID' => (int) $strId,
			);
			$filter = trim((string) ($sub['product_filter'] ?? ''));
			if ($filter !== '') {
				$child['product_filter'] = $filter;
			}
			$children[] = $child;
		}
		if ($children) {
			$map[$strId] = $children;
		}
	}
	if (!$map) {
		return epc_cata_default_subcategories_cached();
	}
	return $map;
}

/**
 * @param array<int, array<string, mixed>> $categories
 */
function epc_cata_category_config_save_bulk(array $categories): bool
{
	$rows = array();
	foreach ($categories as $idx => $row) {
		if (!is_array($row)) {
			continue;
		}
		$norm = epc_cata_category_config_normalize_entry($row, $idx + 1);
		if ($norm !== null) {
			$rows[] = $norm;
		}
	}
	usort($rows, static function (array $a, array $b): int {
		$ao = (int) ($a['ORDER'] ?? 0);
		$bo = (int) ($b['ORDER'] ?? 0);
		if ($ao !== $bo) {
			return $ao <=> $bo;
		}
		return ((int) $a['STR_ID']) <=> ((int) $b['STR_ID']);
	});
	$payload = array(
		'categories' => $rows,
		'updated_at' => time(),
	);
	$ok = epc_cata_setting_set(epc_cata_category_config_setting_key(), $payload);
	if ($ok) {
		epc_cata_category_config_sync_presentation_disabled($rows);
		epc_cata_category_config_sync_legacy_mappings($rows);
	}
	return $ok;
}

/**
 * @param array<string, mixed> $row
 */
function epc_cata_category_config_save_one(array $row): bool
{
	$norm = epc_cata_category_config_normalize_entry($row, 0);
	if ($norm === null) {
		return false;
	}
	$all = epc_cata_category_config_rows_for_cp();
	$found = false;
	foreach ($all as $idx => $existing) {
		if ((int) $existing['STR_ID'] === (int) $norm['STR_ID']) {
			$norm['ORDER'] = (int) ($norm['ORDER'] ?? $existing['ORDER'] ?? ($idx + 1));
			$norm['default_name'] = (string) ($existing['default_name'] ?? $norm['default_name'] ?? '');
			$all[$idx] = array_merge($existing, $norm);
			$found = true;
			break;
		}
	}
	if (!$found) {
		$norm['ORDER'] = (int) ($norm['ORDER'] ?? (count($all) + 1));
		$all[] = $norm;
	}
	return epc_cata_category_config_save_bulk($all);
}

function epc_cata_category_config_reset(): bool
{
	$db = epc_cata_db();
	if ($db && epc_cata_settings_ensure()) {
		$table = epc_cata_settings_table();
		try {
			$stmt = $db->prepare("DELETE FROM `{$table}` WHERE `setting_key` = ?");
			$stmt->execute(array(epc_cata_category_config_setting_key()));
		} catch (Exception $e) {
			return false;
		}
	}
	epc_cata_setting_forget(epc_cata_category_config_setting_key());
	return true;
}

/**
 * @param array<int, array<string, mixed>> $rows
 */
function epc_cata_category_config_sync_presentation_disabled(array $rows): void
{
	$disabled = array();
	foreach ($rows as $row) {
		if (empty($row['enabled'])) {
			$disabled[] = (int) ($row['STR_ID'] ?? 0);
		}
	}
	$presentation = epc_cata_presentation_config();
	$presentation['disabled_category_ids'] = array_values(array_unique(array_filter($disabled)));
	epc_cata_setting_set('presentation', $presentation);
}

/**
 * Keep legacy category_mappings in sync for Presentation tab mapping table.
 *
 * @param array<int, array<string, mixed>> $rows
 */
function epc_cata_category_config_sync_legacy_mappings(array $rows): void
{
	$clean = array();
	foreach ($rows as $row) {
		$strId = (int) ($row['STR_ID'] ?? 0);
		if ($strId <= 0) {
			continue;
		}
		$entry = array(
			'str_id' => $strId,
			'catalog_target' => epc_cata_normalize_catalog_target((string) ($row['catalog_target'] ?? 'eparts-mod')),
		);
		$display = trim((string) ($row['display_name'] ?? ''));
		if ($display === '' && !empty($row['default_name']) && (string) $row['CATEGORY_NAME'] !== (string) $row['default_name']) {
			$display = (string) $row['CATEGORY_NAME'];
		}
		if ($display !== '') {
			$entry['display_name'] = $display;
		}
		$iconId = (int) ($row['ICON_ID'] ?? 0);
		if ($iconId > 0) {
			$entry['icon_id'] = $iconId;
		}
		$ptId = (int) ($row['tecdoc_pt_id'] ?? 0);
		if ($ptId > 0) {
			$entry['tecdoc_pt_id'] = $ptId;
		}
		$umapiId = trim((string) ($row['umapi_category_id'] ?? ''));
		if ($umapiId !== '') {
			$entry['umapi_category_id'] = $umapiId;
		}
		if (!empty($row['subcategories'])) {
			$subs = array();
			foreach ($row['subcategories'] as $sub) {
				if (!is_array($sub)) {
					continue;
				}
				$name = trim((string) ($sub['name'] ?? ''));
				if ($name === '') {
					continue;
				}
				$subEntry = array('name' => $name);
				$subId = trim((string) ($sub['STR_ID'] ?? ''));
				if ($subId !== '') {
					$subEntry['id'] = $subId;
				}
				$filter = trim((string) ($sub['product_filter'] ?? ''));
				if ($filter !== '') {
					$subEntry['product_filter'] = $filter;
				}
				$subs[] = $subEntry;
			}
			if ($subs) {
				$entry['subcategories'] = $subs;
			}
		}
		$clean[(string) $strId] = $entry;
	}
	epc_cata_setting_set('category_mappings', $clean);
}

/**
 * @return array<string, mixed>
 */
function epc_cata_category_config_for_ajax(): array
{
	return array(
		'categories' => epc_cata_category_config_rows_for_cp(),
		'icons' => epc_cata_category_icons_available(),
		'icon_base' => '/content/files/epc-cata/category-icons/',
		'catalog_targets' => epc_cata_catalog_targets(),
		'customized' => epc_cata_category_config_raw() !== null,
		'storefront_count' => count(epc_cata_storefront_categories()),
	);
}

/**
 * Storefront catalog routes for product-family category cards.
 *
 * @return array<string, string>
 */
function epc_cata_catalog_targets(): array
{
	return array(
		'eparts-mod' => 'EParts Mod (vehicle picker → category parts)',
		'eparts-cata' => 'EParts CATA (unified catalog)',
		'parts-search' => 'Parts search (keyword, no vehicle)',
	);
}

function epc_cata_normalize_catalog_target(string $target): string
{
	$target = strtolower(trim(preg_replace('/[^a-z0-9\-]/', '', $target)));
	return array_key_exists($target, epc_cata_catalog_targets()) ? $target : 'eparts-mod';
}

/**
 * CP overrides keyed by STR_ID (string).
 *
 * @return array<string, array<string, mixed>>
 */
function epc_cata_category_mappings_raw(): array
{
	if (epc_cata_category_config_raw() !== null) {
		$out = array();
		foreach (epc_cata_category_config_rows_for_cp() as $row) {
			$strId = (int) ($row['STR_ID'] ?? 0);
			if ($strId <= 0) {
				continue;
			}
			$entry = array(
				'str_id' => $strId,
				'catalog_target' => epc_cata_normalize_catalog_target((string) ($row['catalog_target'] ?? 'eparts-mod')),
			);
			$display = trim((string) ($row['display_name'] ?? ''));
			if ($display !== '') {
				$entry['display_name'] = $display;
			}
			$iconId = (int) ($row['ICON_ID'] ?? 0);
			if ($iconId > 0) {
				$entry['icon_id'] = $iconId;
			}
			$ptId = (int) ($row['tecdoc_pt_id'] ?? 0);
			if ($ptId > 0) {
				$entry['tecdoc_pt_id'] = $ptId;
			}
			$umapiId = trim((string) ($row['umapi_category_id'] ?? ''));
			if ($umapiId !== '') {
				$entry['umapi_category_id'] = $umapiId;
			}
			if (!empty($row['subcategories'])) {
				$subs = array();
				foreach ($row['subcategories'] as $sub) {
					if (!is_array($sub)) {
						continue;
					}
					$name = trim((string) ($sub['name'] ?? ''));
					if ($name === '') {
						continue;
					}
					$subEntry = array('name' => $name);
					$subId = trim((string) ($sub['STR_ID'] ?? ''));
					if ($subId !== '') {
						$subEntry['id'] = $subId;
					}
					$subs[] = $subEntry;
				}
				if ($subs) {
					$entry['subcategories'] = $subs;
				}
			}
			$out[(string) $strId] = $entry;
		}
		return $out;
	}
	$raw = epc_cata_setting_get('category_mappings', array());
	if (!is_array($raw)) {
		return array();
	}
	$out = array();
	foreach ($raw as $key => $row) {
		if (!is_array($row)) {
			continue;
		}
		$strId = (int) ($row['str_id'] ?? ($row['STR_ID'] ?? $key));
		if ($strId <= 0) {
			continue;
		}
		$out[(string) $strId] = $row;
	}
	return $out;
}

/**
 * @return array<string, mixed>
 */
function epc_cata_category_mapping_for(int $strId): array
{
	$map = epc_cata_category_mappings_raw();
	return isset($map[(string) $strId]) && is_array($map[(string) $strId]) ? $map[(string) $strId] : array();
}

/**
 * Merge CP mapping onto default 26-tree rows for storefront + product-family.
 *
 * @param array<int, array<string, mixed>> $categories
 * @return array<int, array<string, mixed>>
 */
function epc_cata_apply_category_mappings(array $categories): array
{
	$mappings = epc_cata_category_mappings_raw();
	if (!$mappings) {
		foreach ($categories as $idx => $row) {
			if (!is_array($row)) {
				continue;
			}
			if (empty($row['catalog_target'])) {
				$categories[$idx]['catalog_target'] = 'eparts-mod';
			}
		}
		return $categories;
	}
	foreach ($categories as $idx => $row) {
		if (!is_array($row)) {
			continue;
		}
		$strId = (int) ($row['STR_ID'] ?? ($row['CATEGORY_ID'] ?? 0));
		if ($strId <= 0) {
			continue;
		}
		$map = $mappings[(string) $strId] ?? array();
		$display = trim((string) ($map['display_name'] ?? ($map['CATEGORY_NAME'] ?? '')));
		if ($display !== '') {
			$categories[$idx]['CATEGORY_NAME'] = $display;
			$categories[$idx]['name'] = $display;
		}
		$iconId = (int) ($map['icon_id'] ?? ($map['ICON_ID'] ?? 0));
		if ($iconId > 0) {
			$categories[$idx]['ICON_ID'] = $iconId;
		}
		$categories[$idx]['catalog_target'] = epc_cata_normalize_catalog_target(
			(string) ($map['catalog_target'] ?? ($row['catalog_target'] ?? 'eparts-mod'))
		);
		$ptId = (int) ($map['tecdoc_pt_id'] ?? ($map['pt_id'] ?? 0));
		if ($ptId > 0) {
			$categories[$idx]['tecdoc_pt_id'] = $ptId;
		}
		$umapiId = trim((string) ($map['umapi_category_id'] ?? ($map['umapi_id'] ?? '')));
		if ($umapiId !== '') {
			$categories[$idx]['umapi_category_id'] = $umapiId;
		}
		if (!empty($map['subcategories']) && is_array($map['subcategories'])) {
			$children = array();
			foreach ($map['subcategories'] as $subRow) {
				if (!is_array($subRow)) {
					continue;
				}
				$name = trim((string) ($subRow['name'] ?? ($subRow['CATEGORY_NAME'] ?? '')));
				if ($name === '') {
					continue;
				}
				$subId = (string) ($subRow['id'] ?? ($subRow['STR_ID'] ?? ''));
				$children[] = array(
					'STR_ID' => $subId,
					'CATEGORY_ID' => $subId,
					'CATEGORY_NAME' => $name,
					'name' => $name,
					'PARENT_STR_ID' => $strId,
					'product_filter' => trim((string) ($subRow['product_filter'] ?? '')),
				);
			}
			if ($children) {
				$categories[$idx]['children'] = $children;
			}
		}
		if (empty($categories[$idx]['catalog_target'])) {
			$categories[$idx]['catalog_target'] = 'eparts-mod';
		}
	}
	return $categories;
}

/**
 * URL slug for a storefront category name (matches product-family JS slugify).
 */
function epc_cata_storefront_slug(string $name): string
{
	$slug = strtolower(trim($name));
	$slug = preg_replace('/[^a-z0-9]+/', '-', $slug);
	return trim($slug, '-');
}

/**
 * Resolve category (+ optional subcategory) from STR_ID, slug, or display name.
 *
 * @return array{category: array<string, mixed>, subcategory: array<string, mixed>|null}|null
 */
function epc_cata_storefront_resolve_category(string $key, string $subKey = ''): ?array
{
	$key = trim($key);
	if ($key === '') {
		return null;
	}
	$categories = epc_cata_storefront_categories();
	$subMap = epc_cata_storefront_subcategories_map();
	$keyLower = strtolower($key);
	$keySlug = epc_cata_storefront_slug($key);
	$cat = null;
	foreach ($categories as $row) {
		$strId = (string) (int) ($row['STR_ID'] ?? ($row['CATEGORY_ID'] ?? 0));
		$name = trim((string) ($row['CATEGORY_NAME'] ?? ($row['name'] ?? '')));
		$slug = $name !== '' ? epc_cata_storefront_slug($name) : '';
		if ($strId !== '0' && ($strId === $key || $strId === (string) (int) $key)) {
			$cat = $row;
			break;
		}
		if ($slug !== '' && ($slug === $keySlug || $slug === $keyLower)) {
			$cat = $row;
			break;
		}
		if ($name !== '' && strtolower($name) === $keyLower) {
			$cat = $row;
			break;
		}
	}
	if ($cat === null) {
		return null;
	}
	$sub = null;
	$subKey = trim($subKey);
	if ($subKey !== '') {
		$parentId = (string) (int) ($cat['STR_ID'] ?? ($cat['CATEGORY_ID'] ?? 0));
		$children = !empty($cat['children']) && is_array($cat['children'])
			? $cat['children']
			: ($subMap[$parentId] ?? array());
		$subSlug = epc_cata_storefront_slug($subKey);
		$subLower = strtolower($subKey);
		foreach ($children as $child) {
			if (!is_array($child)) {
				continue;
			}
			$subId = trim((string) ($child['STR_ID'] ?? ($child['CATEGORY_ID'] ?? ($child['id'] ?? ''))));
			$subName = trim((string) ($child['CATEGORY_NAME'] ?? ($child['name'] ?? '')));
			if ($subId !== '' && $subId === $subKey) {
				$sub = $child;
				break;
			}
			if ($subName !== '' && (epc_cata_storefront_slug($subName) === $subSlug || strtolower($subName) === $subLower)) {
				$sub = $child;
				break;
			}
		}
	}
	return array(
		'category' => $cat,
		'subcategory' => $sub,
	);
}

/**
 * Build product-family / storefront deep link for a mapped category row.
 */
function epc_cata_storefront_category_href(string $langHref, array $catRow, string $subName = ''): string
{
	$langHref = rtrim($langHref !== '' ? $langHref : '/en', '/');
	$strId = (int) ($catRow['STR_ID'] ?? ($catRow['CATEGORY_ID'] ?? 0));
	$name = (string) ($catRow['CATEGORY_NAME'] ?? ($catRow['name'] ?? ''));
	$target = epc_cata_normalize_catalog_target((string) ($catRow['catalog_target'] ?? 'eparts-mod'));
	if ($target === 'parts-search') {
		$q = array('group' => $name);
		if ($subName !== '') {
			$q['term'] = $subName;
		}
		$ptId = (int) ($catRow['tecdoc_pt_id'] ?? 0);
		if ($ptId > 0) {
			$q['pt_id'] = (string) $ptId;
		}
		return $langHref . '/shop/part_search?' . http_build_query($q, '', '&', PHP_QUERY_RFC3986);
	}
	$base = $target === 'eparts-cata' ? ($langHref . '/eparts-cata') : ($langHref . '/eparts-mod');
	$href = $base . '?category=' . rawurlencode((string) $strId) . '&category_name=' . rawurlencode($name);
	if ($subName !== '') {
		$href .= '&subcategory_name=' . rawurlencode($subName);
	}
	return $href;
}

function epc_cata_storefront_categories(): array
{
	if (epc_cata_category_config_raw() !== null) {
		$subMap = epc_cata_storefront_subcategories_map();
		$out = array();
		foreach (epc_cata_category_config_rows_for_cp() as $row) {
			if (empty($row['enabled'])) {
				continue;
			}
			$entry = array(
				'STR_ID' => (int) $row['STR_ID'],
				'CATEGORY_ID' => (int) $row['STR_ID'],
				'ICON_ID' => (int) ($row['ICON_ID'] ?? $row['STR_ID']),
				'CATEGORY_NAME' => (string) $row['CATEGORY_NAME'],
				'catalog_target' => epc_cata_normalize_catalog_target((string) ($row['catalog_target'] ?? 'eparts-mod')),
				'source' => (string) ($row['source'] ?? 'cp'),
			);
			if (!empty($row['ORDER'])) {
				$entry['ORDER'] = (int) $row['ORDER'];
			}
			$ptId = (int) ($row['tecdoc_pt_id'] ?? 0);
			if ($ptId > 0) {
				$entry['tecdoc_pt_id'] = $ptId;
			}
			$umapiId = trim((string) ($row['umapi_category_id'] ?? ''));
			if ($umapiId !== '') {
				$entry['umapi_category_id'] = $umapiId;
			}
			$key = (string) (int) $row['STR_ID'];
			if (!empty($subMap[$key])) {
				$entry['children'] = $subMap[$key];
			}
			$out[] = $entry;
		}
		return $out;
	}
	$rows = epc_cata_apply_category_mappings(epc_cata_attach_subcategories(epc_cata_default_categories_cached()));
	usort($rows, static function (array $a, array $b): int {
		$ao = (int) ($a['ORDER'] ?? 0);
		$bo = (int) ($b['ORDER'] ?? 0);
		if ($ao !== $bo) {
			return $ao <=> $bo;
		}
		return ((int) ($a['STR_ID'] ?? ($a['CATEGORY_ID'] ?? 0))) <=> ((int) ($b['STR_ID'] ?? ($b['CATEGORY_ID'] ?? 0)));
	});
	$disabled = epc_cata_presentation_config()['disabled_category_ids'] ?? array();
	if (!$disabled) {
		return $rows;
	}
	return array_values(array_filter($rows, static function (array $row) use ($disabled): bool {
		$id = (int) ($row['STR_ID'] ?? ($row['CATEGORY_ID'] ?? 0));
		return $id > 0 && !in_array($id, $disabled, true);
	}));
}

/**
 * Lightweight inline config — no DB category tree (prevents homepage/CP FPM exhaustion).
 *
 * @return array<string, mixed>
 */
function epc_cata_presentation_js_config_light(): array
{
	static $mem = null;
	if (is_array($mem)) {
		return $mem;
	}
	$p = epc_cata_presentation_defaults();
	$mem = array(
		'defaultSection' => (string) $p['default_section'],
		'defaultArticleView' => (string) $p['default_article_view'],
		'showCategoryIcons' => !empty($p['show_category_icons']),
		'warehouseOnlyPrices' => !empty($p['warehouse_only_prices']),
		'enableVinSearch' => !empty($p['enable_vin_search']),
		'enablePlateSearch' => !empty($p['enable_plate_search']),
		'sectionsEnabled' => $p['sections_enabled'],
		'categories' => array(),
		'subcategoriesMap' => array(),
		'catalogTargets' => epc_cata_catalog_targets(),
		'categoryIconBase' => '/content/files/epc-cata/category-icons/',
		'version' => EPC_CATA_VERSION,
		'categoriesDeferred' => true,
	);
	return $mem;
}

/**
 * JSON-safe bundle for eparts-mod / eparts-cata inline scripts.
 *
 * @return array<string, mixed>
 */
function epc_cata_presentation_js_config(): array
{
	static $mem = null;
	static $memAt = 0;
	$now = time();
	if (is_array($mem) && ($now - $memAt) < 120) {
		return $mem;
	}
	if (function_exists('epc_perf_cache_remember')) {
		$cached = epc_perf_cache_remember('epc_cata_presentation_js_v1', 300, static function (): array {
			$p = epc_cata_presentation_config();
			return array(
				'defaultSection' => (string) $p['default_section'],
				'defaultArticleView' => (string) $p['default_article_view'],
				'showCategoryIcons' => !empty($p['show_category_icons']),
				'warehouseOnlyPrices' => !empty($p['warehouse_only_prices']),
				'enableVinSearch' => !empty($p['enable_vin_search']),
				'enablePlateSearch' => !empty($p['enable_plate_search']),
				'sectionsEnabled' => $p['sections_enabled'],
				'categories' => epc_cata_storefront_categories(),
				'subcategoriesMap' => epc_cata_storefront_subcategories_map(),
				'catalogTargets' => epc_cata_catalog_targets(),
				'categoryIconBase' => '/content/files/epc-cata/category-icons/',
				'version' => EPC_CATA_VERSION,
				'categoriesDeferred' => false,
			);
		});
		$mem = is_array($cached) ? $cached : epc_cata_presentation_js_config_light();
		$memAt = $now;
		return $mem;
	}
	$mem = epc_cata_presentation_js_config_light();
	$memAt = $now;
	return $mem;
}
