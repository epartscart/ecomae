<?php
// PHP 8.3 goldens for plan Q1-shoal (integrations helpers). Leftover portal.php stays injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function shoal_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = preg_replace('/require_once\s+__DIR__\s*\.\s*[\'"]\/epc_portal\.php[\'"]\s*;/', '// leftover portal injected', $code);
	$code = preg_replace('/require_once\s+__DIR__\s*\.\s*[\'"]\/epc_portal_db\.php[\'"]\s*;/', '// leftover portal db injected', $code);
	$code = preg_replace('/require_once\s+__DIR__\s*\.\s*[\'"]\/epc_perf_cache\.php[\'"]\s*;/', '// leftover perf cache injected', $code);
	$code = preg_replace('/require_once\s+dirname\(__DIR__,\s*2\)\s*\.\s*[\'"]\/epc_cp_mainstream_menu\.php[\'"]\s*;/', '// leftover menu injected', $code);
	file_put_contents($dest, $code);
}

function shoal_stubs(): void
{
	if (!function_exists('epc_portal_is_super_cp_host')) {
		function epc_portal_is_super_cp_host(): bool
		{
			return !empty($GLOBALS['SHOAL_SUPER']);
		}
	}
	if (!function_exists('epc_portal_host')) {
		function epc_portal_host(): string
		{
			return (string) ($GLOBALS['SHOAL_HOST'] ?? ($_SERVER['HTTP_HOST'] ?? ''));
		}
	}
	if (!function_exists('epc_portal_list_tenants')) {
		function epc_portal_list_tenants($pdo): array
		{
			return (array) ($GLOBALS['SHOAL_TENANTS'] ?? array());
		}
	}
	if (!function_exists('epc_portal_platform_pdo')) {
		function epc_portal_platform_pdo()
		{
			return $GLOBALS['SHOAL_PDO'] ?? null;
		}
	}
	if (!function_exists('epc_portal_db_ensure')) {
		function epc_portal_db_ensure($pdo): void
		{
		}
	}
	if (!function_exists('epc_portal_load_site_settings')) {
		function epc_portal_load_site_settings($pdo): array
		{
			return (array) ($GLOBALS['SHOAL_SETTINGS'] ?? array());
		}
	}
	if (!function_exists('epc_portal_save_site_settings')) {
		function epc_portal_save_site_settings($pdo, array $settings): void
		{
			$GLOBALS['SHOAL_SETTINGS'] = $settings;
			$GLOBALS['SHOAL_SAVED'][] = $settings;
		}
	}
	if (!function_exists('epc_perf_cache_remember')) {
		function epc_perf_cache_remember($key, $ttl, $fn)
		{
			return $fn();
		}
	}
}

function shoal_include(): void
{
	shoal_stubs();
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$cfg = new stdClass();
	$cfg->backend_dir = (string) ($GLOBALS['SHOAL_BACKEND'] ?? 'cp');
	$GLOBALS['DP_Config'] = $cfg;
	include_once $GLOBALS['SHOAL_PAGE'];
}

function shoal_admin(): PDO
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	return new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
}

function shoal_db(PDO $admin): array
{
	$schema = 'ecomae_cpw_shoal_' . substr(md5(uniqid('', true)), 0, 8);
	$admin->exec('CREATE DATABASE `' . $schema . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$db = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $schema . ';charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$db->exec('CREATE TABLE `epc_portal_site_settings` (
		`id` INT NOT NULL PRIMARY KEY,
		`cp_menu_json` TEXT NULL,
		`integrations_json` TEXT NULL
	)');
	$db->exec('CREATE TABLE `lang_text_strings` (
		`str_key` VARCHAR(64) NOT NULL PRIMARY KEY,
		`description` VARCHAR(255) NULL,
		`same` VARCHAR(64) NULL,
		`is_error` TINYINT NOT NULL DEFAULT 0,
		`is_custom` TINYINT NOT NULL DEFAULT 0,
		`used_found` TINYINT NOT NULL DEFAULT 0
	)');
	$db->exec('CREATE TABLE `lang_text_strings_translation` (
		`str_key` VARCHAR(64) NOT NULL,
		`lang_code` VARCHAR(8) NOT NULL,
		`value` VARCHAR(255) NOT NULL,
		PRIMARY KEY (`str_key`, `lang_code`)
	)');
	$db->exec('CREATE TABLE `content` (
		`id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
		`count` INT NOT NULL DEFAULT 0,
		`url` VARCHAR(190) NOT NULL DEFAULT \'\',
		`level` INT NOT NULL DEFAULT 0,
		`alias` VARCHAR(64) NOT NULL DEFAULT \'\',
		`value` VARCHAR(64) NOT NULL DEFAULT \'\',
		`parent` INT NOT NULL DEFAULT 0,
		`description` VARCHAR(255) NOT NULL DEFAULT \'\',
		`is_frontend` TINYINT NOT NULL DEFAULT 0,
		`content_type` VARCHAR(16) NOT NULL DEFAULT \'\',
		`content` VARCHAR(255) NOT NULL DEFAULT \'\',
		`title_tag` VARCHAR(64) NOT NULL DEFAULT \'\',
		`description_tag` VARCHAR(16) NOT NULL DEFAULT \'\',
		`keywords_tag` VARCHAR(16) NOT NULL DEFAULT \'\',
		`author_tag` VARCHAR(16) NOT NULL DEFAULT \'\',
		`main_flag` TINYINT NOT NULL DEFAULT 0,
		`modules_array` VARCHAR(16) NOT NULL DEFAULT \'\',
		`css_js` VARCHAR(16) NOT NULL DEFAULT \'\',
		`robots_tag` VARCHAR(16) NOT NULL DEFAULT \'\',
		`system_flag` TINYINT NOT NULL DEFAULT 0,
		`published_flag` TINYINT NOT NULL DEFAULT 0,
		`open` TINYINT NOT NULL DEFAULT 0,
		`time_created` INT NOT NULL DEFAULT 0,
		`time_edited` INT NOT NULL DEFAULT 0,
		`order` INT NOT NULL DEFAULT 0
	)');
	$db->exec('CREATE TABLE `content_access` (
		`content_id` INT NOT NULL,
		`group_id` INT NOT NULL,
		PRIMARY KEY (`content_id`, `group_id`)
	)');
	$db->exec("INSERT INTO `content` (`url`,`level`,`is_frontend`,`alias`) VALUES ('control/config',1,0,'config'),('control/portal/industry_settings',2,0,'industry_settings')");
	$db->exec('INSERT INTO `content_access` (`content_id`,`group_id`) VALUES (2,7),(2,9)');
	return array($schema, $db);
}

function shoal_run_names()
{
	$GLOBALS['SHOAL_BACKEND'] = 'cp';
	shoal_include();
	$cat = epc_integrations_catalog();
	$keys = array_keys($cat);
	return array(
		epc_int_h("O'Reilly & Co"),
		epc_int_backend(),
		array_keys(epc_integrations_categories()),
		epc_integrations_resolve_guide('', ''),
		epc_integrations_resolve_guide('', 'pos'),
		epc_integrations_resolve_guide('/cp/x', 'pos'),
		epc_integrations_resolve_guide('https://e.com/g', 'pos'),
		epc_integrations_resolve_guide('docs/foo', 'pos'),
		epc_integrations_resolve_guide('#bar', ''),
		epc_integrations_resolve_guide('other', ''),
		count($keys),
		$keys,
		!empty($cat['oauth']['super_only_config']) ? 1 : 0,
		(string) ($cat['tenant_registry']['tenant_url'] ?? ''),
		$cat['whatsapp']['menu_patterns'],
		$cat['custom_shipping']['tenant_url'],
	);
}

function shoal_run_flags()
{
	$GLOBALS['SHOAL_BACKEND'] = 'CP/';
	shoal_include();
	$admin = shoal_admin();
	[$schema, $db] = shoal_db($admin);
	try {
		$GLOBALS['SHOAL_PDO'] = $db;
		$GLOBALS['SHOAL_SUPER'] = 1;
		$superKey = epc_integrations_site_key($db);
		$GLOBALS['SHOAL_SUPER'] = 0;
		$GLOBALS['SHOAL_HOST'] = 'www.acme.test';
		$GLOBALS['SHOAL_TENANTS'] = array(
			array('hostname' => 'www.acme.test', 'site_key' => 'acme_parts'),
			array('hostname' => 'beta.trading', 'site_key' => 'beta'),
		);
		$acme = epc_integrations_site_key($db);
		$GLOBALS['SHOAL_HOST'] = 'unknown.host';
		$slug = epc_integrations_site_key($db);
		$defaults = epc_integrations_features_for_site('platform', $db);
		$unknown = epc_integrations_feature_enabled('nope', 'acme_parts', $db) ? 1 : 0;
		$before = epc_integrations_feature_enabled('pos', 'acme_parts', $db) ? 1 : 0;
		$save = epc_integrations_save_feature_flags($db, 'acme_parts', array('pos' => 0, 'nope!' => 1, 'oauth' => 1));
		// request cache still has defaults until process restart — call features again after save
		// leftover caches per request; same process keeps old map. Reset by using a fresh include is
		// not possible. Clear by calling with a different key then the same after save already wrote.
		// PHP static $reqCache will still return cached defaults for acme_parts if it was loaded.
		// Load after save using a site that was not cached first: we already called feature_enabled
		// for acme_parts which populated cache BEFORE save. So after is stale unless we don't
		// pre-read. Re-read via SQL instead for the after flag.
		$st = $db->prepare('SELECT `feature_key`,`enabled` FROM `epc_tenant_feature_flags` WHERE `site_key`=? ORDER BY `feature_key`');
		$st->execute(array('acme_parts'));
		$rows = $st->fetchAll(PDO::FETCH_ASSOC);
		return array(
			epc_int_backend(),
			$superKey,
			$acme,
			$slug,
			count($defaults),
			!empty($defaults['pos']) ? 1 : 0,
			$unknown,
			$before,
			$save,
			$rows,
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

function shoal_run_config()
{
	shoal_include();
	$admin = shoal_admin();
	[$schema, $db] = shoal_db($admin);
	try {
		$GLOBALS['SHOAL_SETTINGS'] = array();
		$empty = epc_integrations_load_tenant_config($db);
		$save = epc_integrations_save_tenant_config($db, array('mobile' => array('app_name' => "O'Reilly", 'enabled' => true)));
		$mobile = epc_integrations_mobile_config($db);
		$plat = epc_integrations_platform_mobile_defaults();
		$def = epc_integrations_default_mobile_config();
		$GLOBALS['SHOAL_SUPER'] = 1;
		$superBlock = epc_integrations_menu_blocked_by_feature('/cp/shop/pos/terminal') ? 1 : 0;
		$GLOBALS['SHOAL_SUPER'] = 0;
		$GLOBALS['SHOAL_HOST'] = 'www.acme.test';
		$GLOBALS['SHOAL_TENANTS'] = array(array('hostname' => 'www.acme.test', 'site_key' => 'acme_parts'));
		$GLOBALS['SHOAL_PDO'] = $db;
		epc_integrations_save_feature_flags($db, 'acme_parts', array('pos' => 0, 'web_tracker' => 0));
		$pos = epc_integrations_menu_blocked_by_feature('/cp/shop/pos/terminal') ? 1 : 0;
		$pay = epc_integrations_menu_blocked_by_feature('/cp/shop/payments/payments') ? 1 : 0;
		$track = epc_integrations_menu_blocked_by_feature('/cp/control/portal/epc_web_tracker?x=1') ? 1 : 0;
		$home = epc_integrations_menu_blocked_by_feature('/cp/control/config') ? 1 : 0;
		return array($empty, $save, $mobile, $plat, $def['pwa_enabled'] ? 1 : 0, $superBlock, $pos, $pay, $track, $home);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

function shoal_run_hub()
{
	shoal_include();
	$admin = shoal_admin();
	[$schema, $db] = shoal_db($admin);
	try {
		$GLOBALS['SHOAL_SUPER'] = 0;
		$GLOBALS['SHOAL_HOST'] = 'www.acme.test';
		$GLOBALS['SHOAL_TENANTS'] = array(array('hostname' => 'www.acme.test', 'site_key' => 'acme_parts'));
		$GLOBALS['SHOAL_PDO'] = $db;
		epc_integrations_save_feature_flags($db, 'acme_parts', array('pos' => 0));
		$super = epc_integrations_hub_rows($db, true);
		$tenant = epc_integrations_hub_rows($db, false);
		$proj = static function (array $rows): array {
			$out = array();
			foreach ($rows as $r) {
				$out[] = array(
					'key' => $r['key'],
					'enabled' => !empty($r['enabled']) ? 1 : 0,
					'configure_url' => $r['configure_url'],
					'guide' => $r['guide'],
					'super_only' => !empty($r['super_only']) ? 1 : 0,
				);
			}
			return $out;
		};
		$id = epc_integrations_register_cp_content($db, 'epc_integrations_hub', 'str_int_hub', 'Integrations hub', 'Интеграции', 'epc_integrations_hub.php', 8);
		$again = epc_integrations_register_cp_content($db, 'epc_integrations_hub', 'str_int_hub', 'Integrations hub', 'Интеграции', 'epc_integrations_hub.php', 8);
		$cnt = (int) $db->query("SELECT COUNT(*) FROM `content` WHERE `url`='control/portal/epc_integrations_hub'")->fetchColumn();
		$groups = (int) $db->query('SELECT COUNT(*) FROM `content_access` WHERE `content_id`=' . (int) $id)->fetchColumn();
		return array(
			count($super),
			count($tenant),
			$proj($super)[1]['key'] ?? '',
			$proj($tenant),
			$id,
			$again,
			$cnt,
			$groups,
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_shoal_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	shoal_patch($root . '/content/general_pages/epc_integrations_helpers.php', $tmp . '/page.php');
	$GLOBALS['SHOAL_PAGE'] = $tmp . '/page.php';
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	$fn = 'shoal_run_' . $case['name'];
	$result = $fn();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1shoal_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1shoal_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
