<?php
// PHP 8.3 goldens for plan Q1-inlet (tenant hub helpers). Leftover portal / demo / client-ERP stay injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function inlet_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("defined('_ASTEXE_') or die('No access');", "// access gate stubbed", $code);
	$code = preg_replace('/require_once\s+(?:__DIR__\s*\.\s*|\\$_SERVER\[[\'"]DOCUMENT_ROOT[\'"]\]\s*\.\s*)[\'"][^\'"]+[\'"]\s*;/', '// leftover injected', $code);
	$code = str_replace('$body = @file_get_contents($url, false, $ctx);', '$body = $GLOBALS[\'INLET_PROBE_BODY\'] ?? false; if (isset($GLOBALS[\'INLET_PROBE_HEADERS\'])) { $http_response_header = $GLOBALS[\'INLET_PROBE_HEADERS\']; }', $code);
	file_put_contents($dest, $code);
}

function inlet_stubs(): void
{
	if (!function_exists('epc_portal_tenant_is_shared_erp_row')) {
		function epc_portal_tenant_is_shared_erp_row(array $row): bool
		{
			if (!empty($row['erp_only_shared'])) {
				return true;
			}
			return (string) ($row['hosted_on'] ?? '') === 'platform';
		}
	}
	if (!function_exists('epc_client_erp_login_url')) {
		function epc_client_erp_login_url(string $siteKey): string
		{
			$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
			return '/cp/client-erp/' . $key . '/';
		}
	}
	if (!function_exists('epc_client_erp_shell_url')) {
		function epc_client_erp_shell_url(string $siteKey): string
		{
			return epc_client_erp_login_url($siteKey) . 'shop/finance/erp?epc_erp_shell=1';
		}
	}
	if (!function_exists('epc_portal_tenant_control_commerce_host')) {
		function epc_portal_tenant_control_commerce_host(string $hostname): string
		{
			$host = strtolower(trim($hostname));
			if ($host === '') {
				return '';
			}
			$host = preg_replace('#^https?://#', '', $host);
			$host = preg_replace('#/.*$#', '', $host);
			$host = preg_replace('/^www\./', '', $host);
			if ($host === '' || strpos($host, '.') === false) {
				return '';
			}
			return 'www.' . $host;
		}
	}
	if (!function_exists('epc_portal_resolve_tenant_db_credentials')) {
		function epc_portal_resolve_tenant_db_credentials(): array
		{
			return (array) ($GLOBALS['INLET_CREDS'] ?? array());
		}
	}
	if (!function_exists('epc_portal_is_platform_operator')) {
		function epc_portal_is_platform_operator(): bool
		{
			return !empty($GLOBALS['INLET_SUPER']);
		}
	}
	if (!function_exists('epc_portal_list_tenants')) {
		function epc_portal_list_tenants($db): array
		{
			return (array) ($GLOBALS['INLET_TENANTS'] ?? array());
		}
	}
	if (!function_exists('epc_portal_industries')) {
		function epc_portal_industries(): array
		{
			return array(
				'auto_parts' => array('name' => 'Auto Parts', 'ecosystem' => 'commerce'),
				'erp_only' => array('name' => 'ERP only', 'ecosystem' => 'erp'),
				'erp_standalone' => array('name' => 'ERP standalone', 'ecosystem' => 'erp'),
			);
		}
	}
	if (!function_exists('epc_portal_ecosystems')) {
		function epc_portal_ecosystems(): array
		{
			return array(
				'commerce' => array('name' => 'Commerce'),
				'erp' => array('name' => 'ERP'),
			);
		}
	}
	if (!function_exists('epc_portal_tenant_statuses')) {
		function epc_portal_tenant_statuses(): array
		{
			return array(
				'live' => 'Live',
				'dns_pending' => 'DNS pending',
				'suspended' => 'Suspended',
			);
		}
	}
	if (!function_exists('epc_portal_demo_urls')) {
		function epc_portal_demo_urls(string $siteKey, $row = null): array
		{
			return array(
				'storefront' => 'https://demo.ecomae.com/' . $siteKey . '/',
				'cp' => 'https://demo.ecomae.com/' . $siteKey . '/cp/',
			);
		}
	}
	if (!function_exists('epc_portal_demo_cp_autologin_url')) {
		function epc_portal_demo_cp_autologin_url(string $siteKey): string
		{
			return 'https://www.ecomae.com/demo-cp/' . $siteKey;
		}
	}
	if (!function_exists('epc_portal_demo_cp_login_url')) {
		function epc_portal_demo_cp_login_url(string $siteKey): string
		{
			return '/demo/' . $siteKey . '/cp';
		}
	}
	if (!function_exists('epc_portal_demo_erp_shell_url')) {
		function epc_portal_demo_erp_shell_url(string $siteKey): string
		{
			return '/demo/' . $siteKey . '/erp';
		}
	}
	if (!function_exists('epc_portal_intro_decode')) {
		function epc_portal_intro_decode($json): array
		{
			$raw = json_decode((string) $json, true);
			return is_array($raw) ? $raw : array();
		}
	}
	if (!function_exists('epc_portal_platform_ip')) {
		function epc_portal_platform_ip(): string
		{
			return (string) ($GLOBALS['INLET_IP'] ?? '203.0.113.10');
		}
	}
	if (!function_exists('epc_portal_host')) {
		function epc_portal_host(): string
		{
			return (string) ($GLOBALS['INLET_HOST'] ?? 'www.ecomae.com');
		}
	}
	if (!function_exists('epc_portal_save_tenant')) {
		function epc_portal_save_tenant($db, array $data): array
		{
			$GLOBALS['INLET_SAVED'][] = $data;
			return array('ok' => true, 'site_key' => (string) ($data['site_key'] ?? 'saved'));
		}
	}
	if (!function_exists('epc_portal_sync_tenant_packs_to_client_db')) {
		function epc_portal_sync_tenant_packs_to_client_db($db, $key): array
		{
			return (array) ($GLOBALS['INLET_SYNC'] ?? array('ok' => true, 'message' => 'packs synced'));
		}
	}
	if (!function_exists('epc_bc_bos_normalize_mode')) {
		function epc_bc_bos_normalize_mode($mode): string
		{
			$mode = strtolower(trim((string) $mode));
			return in_array($mode, array('off', 'anchor', 'network'), true) ? $mode : 'off';
		}
	}
	if (!function_exists('epc_bc_bos_clear_tenant_mode_cache')) {
		function epc_bc_bos_clear_tenant_mode_cache($siteKey = null): void
		{
			$GLOBALS['INLET_CLEARED'][] = (string) $siteKey;
		}
	}
	if (!function_exists('epc_bc_bos_modes')) {
		function epc_bc_bos_modes(): array
		{
			return array(
				'off' => 'Off',
				'anchor' => 'Blockchain anchor (recommended)',
				'network' => 'Network participant (roadmap)',
			);
		}
	}
	if (!function_exists('epc_bc_bos_anchor_pending_batch')) {
		function epc_bc_bos_anchor_pending_batch($limit = 100): array
		{
			return (array) ($GLOBALS['INLET_ANCHOR'] ?? array('ok' => true, 'proof_count' => 0));
		}
	}
	if (!function_exists('epc_bc_bos_anchor_network')) {
		function epc_bc_bos_anchor_network(): string
		{
			return 'local_merkle';
		}
	}
	if (!function_exists('epc_portal_onboard_client')) {
		function epc_portal_onboard_client($db, array $post, $submittedBy = ''): array
		{
			return array('ok' => true, 'by' => (string) $submittedBy, 'trade' => (string) ($post['trade_name'] ?? ''));
		}
	}
	if (!function_exists('epc_portal_tenant_launch_checklist')) {
		function epc_portal_tenant_launch_checklist($db, $siteKey): array
		{
			return array('ok' => true, 'site_key' => (string) $siteKey, 'ready' => 1);
		}
	}
	if (!function_exists('epc_portal_apply_industry_theme_to_tenant')) {
		function epc_portal_apply_industry_theme_to_tenant($db, $siteKey, $opts = array()): array
		{
			return array('ok' => true, 'site_key' => (string) $siteKey, 'theme' => (string) ($opts['theme'] ?? 'auto'));
		}
	}
	if (!function_exists('epc_portal_db_ensure')) {
		function epc_portal_db_ensure($pdo): void
		{
		}
	}
}

function inlet_include(): void
{
	inlet_stubs();
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	include_once $GLOBALS['INLET_PAGE'];
}

function inlet_admin(): PDO
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	return new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
}

function inlet_db(PDO $admin): array
{
	$schema = 'ecomae_cpw_inlet_' . substr(md5(uniqid('', true)), 0, 8);
	$admin->exec('CREATE DATABASE `' . $schema . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$db = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $schema . ';charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$db->exec('CREATE TABLE `epc_portal_tenants` (
		`site_key` VARCHAR(64) NOT NULL PRIMARY KEY,
		`hostname` VARCHAR(120) NOT NULL DEFAULT \'\',
		`trade_name` VARCHAR(120) NOT NULL DEFAULT \'\',
		`db_name` VARCHAR(64) NOT NULL DEFAULT \'\',
		`status` VARCHAR(32) NOT NULL DEFAULT \'\',
		`blockchain_mode` VARCHAR(32) NOT NULL DEFAULT \'\',
		`updated_at` INT NOT NULL DEFAULT 0
	)');
	return array($schema, $db, $pw);
}

function inlet_tenants(): array
{
	return array(
		array(
			'site_key' => 'acme_parts',
			'hostname' => 'www.acme.test',
			'industry_code' => 'auto_parts',
			'status' => 'live',
			'is_demo' => 0,
			'intro_json' => '{"submitted_at":1700000000}',
			'db_name' => '',
		),
		array(
			'site_key' => 'Beta Demo!',
			'hostname' => 'demo.beta.test',
			'industry_code' => 'auto_parts',
			'status' => 'dns_pending',
			'is_demo' => 1,
			'intro_json' => '{}',
			'db_name' => '',
		),
		array(
			'site_key' => 'shared_erp',
			'hostname' => 'www.ecomae.com',
			'industry_code' => 'erp_only',
			'status' => 'live',
			'erp_only_shared' => 1,
			'is_demo' => 0,
			'intro_json' => '',
			'db_name' => '',
		),
	);
}

function inlet_proj(array $row): array
{
	$out = array(
		'site_key' => $row['site_key'],
		'industry_name' => $row['industry_name'],
		'ecosystem_code' => $row['ecosystem_code'],
		'ecosystem_name' => $row['ecosystem_name'],
		'status_label' => $row['status_label'],
		'storefront_url' => $row['storefront_url'],
		'cp_url' => $row['cp_url'],
		'erp_url' => $row['erp_url'],
		'intro_done' => !empty($row['intro_done']) ? 1 : 0,
		'db_connect_ok' => !empty($row['db_connect_ok']) ? 1 : 0,
	);
	if (!empty($row['is_demo_tenant'])) {
		$out['is_demo_tenant'] = 1;
	}
	if (isset($row['erp_login'])) {
		$out['erp_login'] = $row['erp_login'];
	}
	return $out;
}

function inlet_run_names()
{
	inlet_include();
	$mixed = epc_th_tenant_action_urls(array('site_key' => 'Acme-Parts!', 'hostname' => 'https://ACME.test/shop', 'industry_code' => 'auto_parts'));
	$erp = epc_th_tenant_action_urls(array('site_key' => 'beta', 'hostname' => 'beta.trading', 'industry_code' => 'erp_only'));
	$shared = epc_th_tenant_action_urls(array('site_key' => 'shared_erp', 'hostname' => 'www.ecomae.com', 'industry_code' => 'erp_only', 'erp_only_shared' => 1));
	$blank = epc_th_tenant_action_urls(array('site_key' => 'x', 'hostname' => 'localhost', 'industry_code' => ''));
	$empty = epc_th_tenant_db_connect_ok(array('db_name' => '', 'db_user' => '', 'db_password' => ''));
	$GLOBALS['INLET_SUPER'] = 1;
	$ok = 'ok';
	try {
		epc_th_require_super_cp();
	} catch (Throwable $e) {
		$ok = $e->getMessage();
	}
	$GLOBALS['INLET_SUPER'] = 0;
	$denied = 'ok';
	try {
		epc_th_require_super_cp();
	} catch (Throwable $e) {
		$denied = $e->getMessage();
	}
	return array(
		epc_th_h("O'Reilly & Co"),
		$mixed,
		$erp,
		$shared,
		$blank,
		$empty ? 1 : 0,
		$ok,
		$denied,
	);
}

function inlet_run_list()
{
	inlet_include();
	$GLOBALS['INLET_TENANTS'] = inlet_tenants();
	$GLOBALS['INLET_IP'] = '203.0.113.10';
	$GLOBALS['INLET_HOST'] = 'www.ecomae.com';
	$rows = array_map('inlet_proj', epc_th_list_tenants(inlet_admin()));
	$stats = epc_th_platform_stats(inlet_admin());
	$add = epc_th_add_tenant(inlet_admin(), array('site_key' => 'newco', 'trade_name' => "O'Reilly"));
	return array($rows, $stats, $add, count($GLOBALS['INLET_SAVED'] ?? array()));
}

function inlet_run_status()
{
	inlet_include();
	$admin = inlet_admin();
	[$schema, $db] = inlet_db($admin);
	try {
		$db->exec("INSERT INTO `epc_portal_tenants` (`site_key`,`status`,`blockchain_mode`,`updated_at`) VALUES ('acme','dns_pending','off',1)");
		$bad = epc_th_update_tenant_status($db, 'acme', 'nope');
		$missing = epc_th_update_tenant_status($db, 'missing', 'live');
		$GLOBALS['INLET_SYNC'] = array('ok' => true, 'message' => 'packs synced');
		$live = epc_th_update_tenant_status($db, 'acme', 'live');
		$st = $db->query("SELECT `status`,`updated_at` FROM `epc_portal_tenants` WHERE `site_key`='acme'");
		$row = $st->fetch(PDO::FETCH_ASSOC);
		$GLOBALS['INLET_SYNC'] = array('ok' => false, 'message' => 'denied');
		$db->exec("UPDATE `epc_portal_tenants` SET `status`='dns_pending'");
		$fail = epc_th_update_tenant_status($db, 'acme', 'live');
		return array($bad, $missing, $live, $row['status'], (int) $row['updated_at'] > 1 ? 1 : 0, $fail);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

function inlet_run_chain()
{
	inlet_include();
	$admin = inlet_admin();
	[$schema, $db] = inlet_db($admin);
	try {
		$db->exec("INSERT INTO `epc_portal_tenants` (`site_key`,`blockchain_mode`,`updated_at`) VALUES ('acme','off',1),('same','anchor',1)");
		$badKey = epc_th_update_tenant_blockchain_mode($db, '!!!', 'anchor');
		$missing = epc_th_update_tenant_blockchain_mode($db, 'nope', 'anchor');
		$ok = epc_th_update_tenant_blockchain_mode($db, 'acme', 'NETWORK');
		$same = epc_th_update_tenant_blockchain_mode($db, 'same', 'anchor');
		$GLOBALS['INLET_ANCHOR'] = array('ok' => false, 'error' => 'down');
		$fail = epc_th_anchor_blockchain_pending_now(5);
		$GLOBALS['INLET_ANCHOR'] = array('ok' => true, 'proof_count' => 0);
		$none = epc_th_anchor_blockchain_pending_now(5);
		$GLOBALS['INLET_ANCHOR'] = array('ok' => true, 'proof_count' => 3, 'anchor_network' => 'local_merkle', 'merkle_root' => 'abcdef0123456789ffff');
		$some = epc_th_anchor_blockchain_pending_now(5);
		$GLOBALS['INLET_PROBE_BODY'] = '<b>Hello</b> world';
		$GLOBALS['INLET_PROBE_HEADERS'] = array('HTTP/1.1 200 OK');
		$probe = epc_th_probe_url('https://ok.test/');
		$onboard = epc_th_onboard_client($db, array('trade_name' => 'Acme'), 'ops');
		$check = epc_th_launch_checklist($db, 'acme');
		$theme = epc_th_apply_industry_theme($db, 'acme', array('theme' => 'auto'));
		return array($badKey, $missing, $ok, $same, $fail, $none, $some, $probe['ok'] ? 1 : 0, $probe['http_code'], $probe['snippet'], $onboard, $check, $theme, $GLOBALS['INLET_CLEARED'] ?? array());
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_inlet_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	inlet_patch($root . '/content/shop/tenant_hub/epc_tenant_hub_helpers.php', $tmp . '/page.php');
	$GLOBALS['INLET_PAGE'] = $tmp . '/page.php';
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	$fn = 'inlet_run_' . $case['name'];
	$result = $fn();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1inlet_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1inlet_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
