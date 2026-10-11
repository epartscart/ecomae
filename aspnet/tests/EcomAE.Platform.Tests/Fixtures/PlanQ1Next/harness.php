<?php
// PHP 8.3 goldens for plan Q1-next named helpers.
// Usage: php harness.php /workspace-wt/small-done > golden.json
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1n_' . substr(md5(uniqid('', true)), 0, 12);
	foreach (array('/content/general_pages') as $dir) {
		@mkdir($doc . $dir, 0777, true);
	}

	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));

	file_put_contents($doc . '/content/general_pages/epc_portal.php', '<?php
if (!function_exists("epc_portal_site_profile")) { function epc_portal_site_profile() { return $GLOBALS["__site"] ?? array(); } }
if (!function_exists("epc_portal_is_platform_operator_host")) { function epc_portal_is_platform_operator_host() { return !empty($GLOBALS["__operator"]); } }
if (!function_exists("epc_portal_commerce_storefront_enabled")) { function epc_portal_commerce_storefront_enabled() { return !empty($GLOBALS["__commerce"]); } }
if (!function_exists("epc_portal_active_storefront_package")) { function epc_portal_active_storefront_package() { return $GLOBALS["__pkg"] ?? ""; } }
if (!function_exists("epc_portal_load_site_settings")) { function epc_portal_load_site_settings() { return $GLOBALS["__settings"] ?? array(); } }
');
	file_put_contents($doc . '/content/general_pages/epc_portal_db.php', '<?php
if (!function_exists("epc_portal_load_site_settings")) { function epc_portal_load_site_settings() { return $GLOBALS["__settings"] ?? array(); } }
');
	file_put_contents($doc . '/content/general_pages/epc_branding.php', '<?php
if (!function_exists("epc_brand_trade_name")) { function epc_brand_trade_name() { return $GLOBALS["__trade"] ?? ""; } }
if (!function_exists("epc_brand_mandatory_line_applies")) { function epc_brand_mandatory_line_applies() { return !empty($GLOBALS["__mandatory"]); } }
');
	file_put_contents($doc . '/content/general_pages/epc_portal_tenant_brand.php', '<?php
if (!function_exists("epc_portal_tenant_brand_enabled")) { function epc_portal_tenant_brand_enabled() { return !empty($GLOBALS["__tenant_brand"]); } }
if (!function_exists("epc_portal_tenant_brand_enqueue")) { function epc_portal_tenant_brand_enqueue() { echo $GLOBALS["__tenant_enq"] ?? "TENANT_ENQ"; } }
if (!function_exists("epc_portal_tenant_brand_markup")) { function epc_portal_tenant_brand_markup($variant = "header") { return $GLOBALS["__tenant_html"] ?? "TENANT"; } }
');
	file_put_contents($doc . '/content/general_pages/epc_ecomae_hub_logo.php', '<?php
if (!function_exists("epc_ecomae_hub_logo_enqueue")) { function epc_ecomae_hub_logo_enqueue() { echo $GLOBALS["__hub_enq"] ?? "HUB_ENQ"; } }
if (!function_exists("epc_ecomae_hub_logo")) { function epc_ecomae_hub_logo($variant = "header", array $opts = array()) { return $GLOBALS["__hub_html"] ?? "HUB"; } }
');
	file_put_contents($doc . '/content/general_pages/epc_storefront_animated_logos.php', '<?php
if (!function_exists("epc_storefront_animated_logo_markup")) { function epc_storefront_animated_logo_markup(string $packageId = "") { return $GLOBALS["__anim"] ?? ""; } }
');

	$cleanup = function () use ($doc, $admin, $dbName) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
		$it = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($doc, FilesystemIterator::SKIP_DOTS), RecursiveIteratorIterator::CHILD_FIRST);
		foreach ($it as $file) {
			$file->isDir() ? @rmdir($file->getPathname()) : @unlink($file->getPathname());
		}
		@rmdir($doc);
	};

	$GLOBALS['__root'] = $root;
	$GLOBALS['__doc'] = $doc;
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	$_SERVER['REQUEST_METHOD'] = 'GET';
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$GLOBALS['db_link'] = $pdo;

	$name = $case['name'];
	if (strpos($name, 'logo_') === 0) {
		require $root . '/content/general_pages/epc_portal_storefront_logo.php';
	} elseif (strpos($name, 'pack_') === 0) {
		require $root . '/content/general_pages/epc_industry_packs.php';
	} elseif (strpos($name, 'promo_') === 0) {
		require $root . '/content/general_pages/epc_promotions_engine.php';
	}

	register_shutdown_function(function () use ($case, $cleanup) {
		$html = ob_get_clean();
		$cleanup();
		echo json_encode(
			array(
				'name' => $case['name'],
				'output' => $html,
				'result' => $GLOBALS['__result'] ?? null,
			),
			JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR
		);
	});
	ob_start();
	if (isset($case['eval'])) {
		$GLOBALS['__result'] = eval($case['eval']);
	}
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN')) . ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1n_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1n_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
