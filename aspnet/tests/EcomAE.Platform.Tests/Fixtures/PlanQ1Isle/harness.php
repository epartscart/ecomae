<?php
// PHP 8.3 goldens for plan Q1-isle (public REST API). Leftover portal / shared-ERP / platform-data stay injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function isle_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = preg_replace('/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/general_pages\/epc_portal\.php[\'"]\s*;/', '// leftover portal injected', $code);
	$code = preg_replace('/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/general_pages\/epc_portal_db\.php[\'"]\s*;/', '// leftover portal db injected', $code);
	$code = preg_replace('/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/general_pages\/epc_portal_tenant_intro\.php[\'"]\s*;/', '// leftover tenant intro injected', $code);
	$code = preg_replace('/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/general_pages\/epc_portal_shared_erp\.php[\'"]\s*;/', '// leftover shared erp injected', $code);
	$code = preg_replace('/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/shop\/finance\/epc_erp_helpers\.php[\'"]\s*;/', '// leftover finance helpers injected', $code);
	$code = preg_replace('/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/general_pages\/epc_ecomae_platform_data\.php[\'"]\s*;/', '// leftover platform data injected', $code);
	$code = preg_replace('/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/general_pages\/epc_power_bi\.php[\'"]\s*;/', '// leftover power bi injected', $code);
	$code = preg_replace('/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/config\.php[\'"]\s*;/', '// leftover config injected', $code);
	file_put_contents($dest, $code);
}

function isle_stubs(): void
{
	if (!function_exists('epc_portal_db_ensure')) {
		function epc_portal_db_ensure($pdo): void
		{
		}
	}
	if (!function_exists('epc_portal_platform_pdo')) {
		function epc_portal_platform_pdo()
		{
			return $GLOBALS['ISLE_PLATFORM_PDO'] ?? null;
		}
	}
	if (!function_exists('epc_portal_tenant_get')) {
		function epc_portal_tenant_get($pdo, $key)
		{
			return $GLOBALS['ISLE_TENANTS'][$key] ?? null;
		}
	}
	if (!function_exists('epc_portal_shared_erp_tenant_pdo')) {
		function epc_portal_shared_erp_tenant_pdo($row)
		{
			$key = (string) ($row['site_key'] ?? '');
			return $GLOBALS['ISLE_TENANT_PDO'][$key] ?? null;
		}
	}
	if (!function_exists('epc_erp_order_status_name_sql')) {
		function epc_erp_order_status_name_sql($pdo): string
		{
			return '\'open\'';
		}
	}
	if (!function_exists('epc_erp_dashboard')) {
		function epc_erp_dashboard($pdo): array
		{
			return (array) ($GLOBALS['ISLE_DASH'] ?? array('date_from' => 1700000000, 'date_to' => 1700086400, 'order_count' => 2, 'revenue_ex_vat' => 10.126, 'profit_ex_vat' => 1, 'receivable_due_orders' => 0, 'customer_ledger_balance' => 3.1, 'payable_balance' => 0, 'cash_bank_total' => 8, 'vat_net_payable' => 0.5, 'vat_net_status' => 'payable'));
		}
	}
	if (!function_exists('epc_ecomae_platform_super_cp_capability_categories')) {
		function epc_ecomae_platform_super_cp_capability_categories(): array
		{
			return array('cp' => 3, 'bos' => 2);
		}
	}
	if (!function_exists('epc_ecomae_platform_super_cp_capability_count')) {
		function epc_ecomae_platform_super_cp_capability_count(): int
		{
			return 5;
		}
	}
	if (!function_exists('epc_power_bi_wants_csv')) {
		function epc_power_bi_wants_csv(): bool
		{
			return !empty($GLOBALS['ISLE_CSV']);
		}
	}
	if (!function_exists('epc_power_bi_emit_csv')) {
		function epc_power_bi_emit_csv($headers, $rows, $name): void
		{
			echo 'CSV:' . $name . ':' . implode(',', $headers);
		}
	}
	if (!function_exists('epc_power_bi_capabilities')) {
		function epc_power_bi_capabilities(): array
		{
			return array('kpis', 'orders');
		}
	}
	if (!function_exists('epc_power_bi_dataset_catalog')) {
		function epc_power_bi_dataset_catalog($base): array
		{
			return array(array('id' => 'kpis', 'url' => rtrim((string) $base, '/') . '/epc-api/v1/powerbi/kpis'));
		}
	}
	if (!function_exists('epc_power_bi_dataset_kpis')) {
		function epc_power_bi_dataset_kpis($pdo, $siteKey): array
		{
			return array('headers' => array('site', 'orders'), 'rows' => array(array($siteKey, 2)), 'meta' => array('kind' => 'kpis'));
		}
	}
	if (!function_exists('epc_power_bi_dataset_orders')) {
		function epc_power_bi_dataset_orders($pdo, $siteKey, $limit): array
		{
			return array('headers' => array('id'), 'rows' => array(array(9)), 'meta' => array('limit' => $limit));
		}
	}
	if (!function_exists('epc_power_bi_parse_date_param')) {
		function epc_power_bi_parse_date_param($name, $fallback)
		{
			return (int) $fallback;
		}
	}
	if (!function_exists('epc_power_bi_dataset_report')) {
		function epc_power_bi_dataset_report($pdo, $type, $from, $to): array
		{
			return array('headers' => array('type'), 'rows' => array(array($type)), 'meta' => array('from' => $from, 'to' => $to));
		}
	}
	if (!function_exists('epc_power_bi_dataset_metrics')) {
		function epc_power_bi_dataset_metrics($pdo, $siteKey): array
		{
			return array('headers' => array('m'), 'rows' => array(array('ok')), 'meta' => array('site' => $siteKey));
		}
	}
}

function isle_include(): void
{
	isle_stubs();
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	include_once $GLOBALS['ISLE_PAGE'];
}

function isle_admin(): PDO
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	return new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
}

function isle_schema(PDO $admin, string $suffix): array
{
	$name = 'ecomae_cpw_isle_' . $suffix . '_' . substr(md5(uniqid('', true)), 0, 8);
	$admin->exec('CREATE DATABASE `' . $name . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	return array($name, $pdo);
}

function isle_capture(callable $fn)
{
	ob_start();
	$ret = $fn();
	$out = ob_get_clean();
	$j = json_decode($out, true);
	if (is_array($j) && isset($j['time'])) {
		$j['time'] = 'ISO';
	}
	return array($ret, $j, $out);
}

function isle_run_names()
{
	isle_include();
	$_SERVER['REQUEST_URI'] = '/epc-api/v1';
	$r1 = epc_api_v1_route_path();
	$_SERVER['REQUEST_URI'] = '/epc-api/v1/';
	$r2 = epc_api_v1_route_path();
	$_SERVER['REQUEST_URI'] = '/epc-api/v1/health';
	$r3 = epc_api_v1_route_path();
	$_SERVER['REQUEST_URI'] = '/epc-api/v1/tenant/info?x=1';
	$r4 = epc_api_v1_route_path();
	$_SERVER['REQUEST_URI'] = '/other';
	$r5 = epc_api_v1_route_path();
	unset($_SERVER['REQUEST_URI']);
	$r6 = epc_api_v1_route_path();
	unset($_SERVER['HTTP_X_API_KEY'], $_SERVER['HTTP_AUTHORIZATION']);
	$k0 = epc_api_v1_extract_key();
	$_SERVER['HTTP_X_API_KEY'] = '  key-one  ';
	$k1 = epc_api_v1_extract_key();
	unset($_SERVER['HTTP_X_API_KEY']);
	$_SERVER['HTTP_AUTHORIZATION'] = 'Bearer tok-99';
	$k2 = epc_api_v1_extract_key();
	$_SERVER['HTTP_X_API_KEY'] = '0';
	$_SERVER['HTTP_AUTHORIZATION'] = 'Bearer fallback';
	$k3 = epc_api_v1_extract_key();
	list(, $err) = isle_capture(static function () {
		epc_api_v1_error(401, 'missing_api_key', "O'Reilly & Co");
	});
	list(, $ok) = isle_capture(static function () {
		epc_api_v1_ok(array('n' => 1, 'href' => 'https://www.ecomae.com/x'));
	});
	return array(
		$r1, $r2, $r3, $r4, $r5, $r6,
		$k0, $k1, $k2, $k3,
		epc_api_v1_parse_scopes('nope'),
		epc_api_v1_parse_scopes('["read:orders","",0,"read:products"]'),
		epc_api_v1_scope_allowed(array('*'), 'read:x') ? 1 : 0,
		epc_api_v1_scope_allowed(array('read:*'), 'read:x') ? 1 : 0,
		epc_api_v1_scope_allowed(array('read:orders'), 'read:orders') ? 1 : 0,
		epc_api_v1_scope_allowed(array('read:orders'), 'read:erp') ? 1 : 0,
		$err, $ok,
	);
}

function isle_seed_platform(PDO $pdo): void
{
	$pdo->exec('CREATE TABLE `epc_portal_site_settings` (
		`host` VARCHAR(120) NOT NULL PRIMARY KEY,
		`access_mode` VARCHAR(24) NOT NULL DEFAULT \'\'
	)');
	$pdo->exec("INSERT INTO `epc_portal_site_settings` (`host`,`access_mode`) VALUES ('www.acme.test','catalog'), ('beta.test','')");
}

function isle_insert_key(PDO $pdo, string $site, string $raw, string $scopes, int $active = 1, string $label = 'Ops'): int
{
	$hash = hash('sha256', $raw);
	$st = $pdo->prepare('INSERT INTO `epc_api_keys` (`tenant_site_key`,`key_hash`,`key_prefix`,`label`,`scopes_json`,`active`,`created_at`,`last_used_at`) VALUES (?,?,?,?,?,?,?,?)');
	$st->execute(array($site, $hash, substr($raw, 0, 8), $label, $scopes, $active, 10, 0));
	return (int) $pdo->lastInsertId();
}

function isle_tenants(): array
{
	return array(
		'acme_parts' => array(
			'site_key' => 'acme_parts',
			'trade_name' => 'Acme Parts',
			'hostname' => 'www.acme.test',
			'industry_code' => 'auto_parts',
			'status' => 'live',
			'erp_only_shared' => 0,
			'db_name' => '',
		),
		'beta' => array(
			'site_key' => 'beta',
			'trade_name' => '',
			'hostname' => 'www.beta.test',
			'industry_code' => 'auto_parts',
			'status' => 'demo',
			'erp_only_shared' => 1,
		),
	);
}

function isle_run_auth()
{
	isle_include();
	$admin = isle_admin();
	list($schema, $pdo) = isle_schema($admin, 'a');
	try {
		epc_api_v1_ensure_keys_table($pdo);
		isle_seed_platform($pdo);
		isle_insert_key($pdo, 'acme_parts', 'isle-acme', '["read:tenant","read:orders"]', 1, "O'Reilly");
		isle_insert_key($pdo, 'ghost', 'isle-ghost', '["*"]', 1, 'Ghost');
		isle_insert_key($pdo, 'acme_parts', 'isle-dead', '["read:tenant"]', 0, 'Dead');
		$GLOBALS['ISLE_TENANTS'] = isle_tenants();
		unset($_SERVER['HTTP_X_API_KEY'], $_SERVER['HTTP_AUTHORIZATION']);
		list($missRet, $miss) = isle_capture(static function () use ($pdo) {
			return epc_api_v1_auth($pdo, 'read:tenant');
		});
		$_SERVER['HTTP_X_API_KEY'] = 'nope';
		list($badRet, $bad) = isle_capture(static function () use ($pdo) {
			return epc_api_v1_auth($pdo, 'read:tenant');
		});
		$_SERVER['HTTP_X_API_KEY'] = 'isle-dead';
		list($deadRet, $dead) = isle_capture(static function () use ($pdo) {
			return epc_api_v1_auth($pdo, 'read:tenant');
		});
		$_SERVER['HTTP_X_API_KEY'] = 'isle-acme';
		list($scopeRet, $scope) = isle_capture(static function () use ($pdo) {
			return epc_api_v1_auth($pdo, 'read:erp');
		});
		list($okRet, $okBody) = isle_capture(static function () use ($pdo) {
			return epc_api_v1_auth($pdo, 'read:tenant');
		});
		$used = (int) $pdo->query('SELECT `last_used_at` FROM `epc_api_keys` WHERE `label` = "O\'Reilly"')->fetchColumn();
		$_SERVER['HTTP_X_API_KEY'] = 'isle-ghost';
		list($ghostRet, $ghost) = isle_capture(static function () use ($pdo) {
			return epc_api_v1_auth($pdo, null);
		});
		$modeAcme = epc_api_v1_tenant_access_mode($pdo, $GLOBALS['ISLE_TENANTS']['acme_parts']);
		$modeBeta = epc_api_v1_tenant_access_mode($pdo, array('hostname' => 'www.beta.test'));
		$modeEmpty = epc_api_v1_tenant_access_mode($pdo, array('hostname' => ''));
		$modeMiss = epc_api_v1_tenant_access_mode($pdo, array('hostname' => 'none.test'));
		return array(
			$missRet === null ? 1 : 0, $miss,
			$badRet === null ? 1 : 0, $bad,
			$deadRet === null ? 1 : 0, $dead,
			$scopeRet === null ? 1 : 0, $scope,
			is_array($okRet) ? 1 : 0,
			is_array($okRet) ? (string) $okRet['tenant']['site_key'] : '',
			is_array($okRet) ? (string) $okRet['key']['label'] : '',
			$okRet['scopes'] ?? array(),
			$used > 0 ? 1 : 0,
			$ghostRet === null ? 1 : 0, $ghost,
			$modeAcme, $modeBeta, $modeEmpty, $modeMiss,
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

function isle_run_shop()
{
	isle_include();
	$admin = isle_admin();
	list($platName, $plat) = isle_schema($admin, 'p');
	list($acmeName, $acme) = isle_schema($admin, 't');
	list($betaName, $beta) = isle_schema($admin, 'b');
	try {
		epc_api_v1_ensure_keys_table($plat);
		isle_insert_key($plat, 'acme_parts', 'isle-acme', '["read:orders","read:products","read:erp","read:bi"]');
		isle_insert_key($plat, 'beta', 'isle-beta', '["read:products"]');
		foreach (array($acme, $beta) as $db) {
			$db->exec('CREATE TABLE `shop_catalogue_products` (
				`id` INT NOT NULL PRIMARY KEY,
				`caption` VARCHAR(200) NOT NULL,
				`alias` VARCHAR(120) NOT NULL,
				`category_id` INT NOT NULL DEFAULT 0,
				`published_flag` TINYINT NOT NULL DEFAULT 1
			)');
			$db->exec('CREATE TABLE `shop_orders` (
				`id` INT NOT NULL PRIMARY KEY,
				`time` INT NOT NULL,
				`user_id` INT NOT NULL,
				`paid` TINYINT NOT NULL DEFAULT 0,
				`paid_type` INT NOT NULL DEFAULT 0,
				`successfully_created` TINYINT NOT NULL DEFAULT 1
			)');
		}
		$acme->exec("INSERT INTO `shop_catalogue_products` VALUES (21,'Pixel 8','pixel-8',11,1),(22,'Hidden','hid',11,0)");
		$beta->exec("INSERT INTO `shop_catalogue_products` VALUES (31,'Beta Cable','beta-cable',4,1)");
		$acme->exec("INSERT INTO `shop_orders` VALUES (9,1700000000,21,1,2,1),(8,10,1,0,0,0)");
		$GLOBALS['ISLE_TENANTS'] = isle_tenants();
		$GLOBALS['ISLE_TENANT_PDO'] = array('acme_parts' => $acme, 'beta' => $beta);
		$_GET = array();
		$_SERVER['HTTP_X_API_KEY'] = 'isle-acme';
		list(, $missQ) = isle_capture(static function () use ($plat) {
			epc_api_v1_handle_products_search($plat);
		});
		$_GET['q'] = 'Pixel';
		list(, $acmeHit) = isle_capture(static function () use ($plat) {
			epc_api_v1_handle_products_search($plat);
		});
		$_SERVER['HTTP_X_API_KEY'] = 'isle-beta';
		$_GET['q'] = 'Pixel';
		list(, $betaMiss) = isle_capture(static function () use ($plat) {
			epc_api_v1_handle_products_search($plat);
		});
		$_GET['q'] = 'Beta';
		list(, $betaHit) = isle_capture(static function () use ($plat) {
			epc_api_v1_handle_products_search($plat);
		});
		$_SERVER['HTTP_X_API_KEY'] = 'isle-acme';
		$_GET['limit'] = '50';
		list(, $orders) = isle_capture(static function () use ($plat) {
			epc_api_v1_handle_orders($plat);
		});
		list(, $dash) = isle_capture(static function () use ($plat) {
			epc_api_v1_handle_erp_dashboard($plat);
		});
		$_SERVER['HTTP_X_API_KEY'] = 'isle-beta';
		list(, $dashDeny) = isle_capture(static function () use ($plat) {
			epc_api_v1_handle_erp_dashboard($plat);
		});
		$nodb = epc_api_v1_tenant_pdo(array('site_key' => 'none', 'db_name' => ''));
		return array($missQ, $acmeHit, $betaMiss, $betaHit, $orders, $dash, $dashDeny, $nodb === null ? 1 : 0);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $platName . '`');
		$admin->exec('DROP DATABASE IF EXISTS `' . $acmeName . '`');
		$admin->exec('DROP DATABASE IF EXISTS `' . $betaName . '`');
	}
}

function isle_run_dispatch()
{
	isle_include();
	$admin = isle_admin();
	list($schema, $pdo) = isle_schema($admin, 'd');
	try {
		epc_api_v1_ensure_keys_table($pdo);
		isle_seed_platform($pdo);
		isle_insert_key($pdo, 'acme_parts', 'isle-acme', '["read:tenant","read:bi","read:erp"]');
		$GLOBALS['ISLE_PLATFORM_PDO'] = $pdo;
		$GLOBALS['ISLE_TENANTS'] = isle_tenants();
		$GLOBALS['ISLE_TENANT_PDO'] = array('acme_parts' => $pdo);
		$_SERVER['REQUEST_URI'] = '/epc-api/v1/health';
		list(, $health) = isle_capture(static function () {
			epc_api_v1_dispatch();
		});
		$_SERVER['REQUEST_URI'] = '/epc-api/v1/capabilities';
		list(, $caps) = isle_capture(static function () {
			epc_api_v1_dispatch();
		});
		$_SERVER['REQUEST_URI'] = '/epc-api/v1/nope';
		list(, $unknown) = isle_capture(static function () {
			epc_api_v1_dispatch();
		});
		$_SERVER['REQUEST_URI'] = '/epc-api/v1/openapi.json';
		list(, $spec) = isle_capture(static function () {
			epc_api_v1_dispatch();
		});
		$_SERVER['REQUEST_URI'] = '/epc-api/v1/tenant/info';
		unset($_SERVER['HTTP_X_API_KEY']);
		list(, $noKey) = isle_capture(static function () {
			epc_api_v1_dispatch();
		});
		$_SERVER['HTTP_X_API_KEY'] = 'isle-acme';
		list(, $info) = isle_capture(static function () {
			epc_api_v1_dispatch();
		});
		$_SERVER['REQUEST_URI'] = '/epc-api/v1/powerbi/catalog';
		list(, $bi) = isle_capture(static function () {
			epc_api_v1_dispatch();
		});
		$dataset = array('headers' => array('a', 'b'), 'rows' => array(array('x', 1)), 'meta' => array('n' => 1));
		list(, $rows) = isle_capture(static function () use ($dataset) {
			epc_api_v1_powerbi_respond($dataset, 'kpis', 'acme_parts');
		});
		$GLOBALS['ISLE_CSV'] = 1;
		ob_start();
		epc_api_v1_powerbi_respond($dataset, 'kpis', 'acme_parts');
		$csv = ob_get_clean();
		$plat = epc_api_v1_platform_pdo() instanceof PDO ? 1 : 0;
		return array($health, $caps, $unknown, $spec, $noKey, $info, $bi, $rows, $csv, $plat);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_isle_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	isle_patch($root . '/content/general_pages/epc_api_v1.php', $tmp . '/page.php');
	$GLOBALS['ISLE_PAGE'] = $tmp . '/page.php';
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	$fn = 'isle_run_' . $case['name'];
	$result = $fn();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1isle_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1isle_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
