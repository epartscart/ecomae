<?php
// PHP 8.3 goldens for plan Q1-sail (tenant template catalogue). Leftover portal/theme/finance-pack parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function sail_dsn(): array
{
	$pass = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: '';
	if ($pass === '') {
		throw new RuntimeException('missing ECOMAE_LOCAL_MARIADB_E2E_DSN');
	}
	return array('ecomae', $pass, '127.0.0.1', '3306');
}

function sail_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("require_once __DIR__ . '/../../general_pages/epc_industry_consolidation.php';", '// leftover consolidation stubbed', $code);
	$code = str_replace("require_once __DIR__ . '/../../general_pages/epc_portal_storefront_packages.php';", '// leftover packages stubbed', $code);
	$code = str_replace("require_once __DIR__ . '/../../general_pages/epc_portal_industry_live_bridge.php';", '// leftover live-bridge stubbed', $code);
	$code = str_replace("require_once __DIR__ . '/../../general_pages/epc_portal.php';", '// leftover portal stubbed', $code);
	$code = str_replace("require_once __DIR__ . '/../../general_pages/epc_industry_seo.php';", '// leftover seo stubbed', $code);
	$code = str_replace("require_once __DIR__ . '/epc_tenant_hub_helpers.php';", '// leftover hub stubbed', $code);
	$code = str_replace("require_once __DIR__ . '/../../general_pages/epc_portal_db.php';", '// leftover portal-db stubbed', $code);
	$code = str_replace(
		"\$erpFile = __DIR__ . '/../finance/epc_erp_industry_packs.php';\n\tif (is_file(\$erpFile)) {\n\t\trequire_once \$erpFile;\n\t\tif (function_exists('epc_erp_industry_packs')) {\n\t\t\t\$erpPacks = epc_erp_industry_packs();\n\t\t}\n\t}",
		'$erpPacks = function_exists("epc_erp_industry_packs") ? epc_erp_industry_packs() : array();',
		$code
	);
	file_put_contents($dest, $code);
}

function sail_boot(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', true);
	}
	if (!function_exists('epc_portal_industry_live_defs')) {
		eval('function epc_portal_industry_live_defs() { return (array) ($GLOBALS["SAIL_DEFS"] ?? array()); }');
		eval('function epc_industry_groups() { return (array) ($GLOBALS["SAIL_GROUPS"] ?? array()); }');
		eval('function epc_portal_storefront_package_registry() { return (array) ($GLOBALS["SAIL_PKGS"] ?? array()); }');
		eval('function epc_portal_storefront_package_for_industry($code) { return (string) ($GLOBALS["SAIL_PKG_FOR"][$code] ?? ""); }');
		eval('function epc_portal_industries() { return (array) ($GLOBALS["SAIL_INDUSTRIES"] ?? array()); }');
		eval('function epc_erp_industry_packs() { return (array) ($GLOBALS["SAIL_ERP"] ?? array()); }');
		eval('function epc_industry_seo_primary_host($tk) { return $tk.".ecomae.com"; }');
		eval('function epc_th_apply_industry_theme($db, $site, $opts) { $GLOBALS["SAIL_APPLY"] = array($site, $opts); return (array) ($GLOBALS["SAIL_THEME"] ?? array("ok"=>true,"message"=>"Applied")); }');
		eval('function epc_portal_tenant_get($db, $key) { return null; }');
		eval('function epc_portal_load_site_settings_for_host($db, $host) { return array(); }');
		eval('function epc_portal_save_site_settings($db, $settings) { $GLOBALS["SAIL_SAVED"] = $settings; }');
	}
}

function sail_seed(): void
{
	$GLOBALS['SAIL_DEFS'] = array(
		'auto_parts' => array('template_key' => 'automotive', 'mode' => 'hub_root'),
		'tyres' => array('template_key' => 'automotive', 'mode' => 'alias'),
		'jewellery' => array('template_key' => 'jewellery', 'mode' => 'hub_root'),
	);
	$GLOBALS['SAIL_GROUPS'] = array(
		'z_jew' => array('template_key' => 'jewellery', 'label' => 'Jewellery', 'description' => 'Gold', 'icon' => 'fa-gem', 'erp_base' => 'jew', 'color_scheme' => array('primary' => '#111', 'accent' => '#222'), 'available_sub_areas' => array('rings')),
		'a_auto' => array('template_key' => 'automotive', 'label' => 'Auto', 'description' => 'Parts', 'icon' => 'fa-car', 'erp_base' => 'auto', 'color_scheme' => array(), 'available_sub_areas' => array()),
	);
	$GLOBALS['SAIL_PKGS'] = array(
		'auto_pkg' => array('label' => 'Auto pack', 'desc' => 'Chrome', 'theme_template' => 'nero', 'industry_codes' => array('auto_parts'), 'implemented' => true),
	);
	$GLOBALS['SAIL_PKG_FOR'] = array('auto_parts' => 'auto_pkg', 'jewellery' => '');
	$GLOBALS['SAIL_INDUSTRIES'] = array('auto_parts' => array('name' => 'Auto parts'), 'tyres' => array('label' => 'Tyres'), 'jewellery' => array('name' => 'Jewellery'));
	$GLOBALS['SAIL_ERP'] = array('auto' => array('label' => 'Auto ERP'), 'jew' => array('label' => 'Jew ERP'));
	$GLOBALS['SAIL_THEME'] = array('ok' => true, 'message' => 'Applied');
}

function sail_include(): void
{
	include_once $GLOBALS['SAIL_PAGE'];
}

function sail_run_guide()
{
	sail_include();
	$steps = epc_th_templates_guide_steps();
	$titles = array();
	foreach ($steps as $s) {
		$titles[] = $s['title'];
	}
	return array(count($steps), $titles);
}

function sail_run_codes()
{
	sail_seed();
	sail_include();
	return array(
		epc_th_portal_codes_for_template('Automotive!'),
		epc_th_default_industry_for_template('automotive'),
		epc_th_default_industry_for_template('unknown_key'),
		epc_th_default_industry_for_template('healthcare'),
	);
}

function sail_run_catalog()
{
	sail_seed();
	sail_include();
	$cat = epc_th_industry_templates_catalog();
	$pkg = epc_th_storefront_packages_catalog();
	return array(
		count($cat),
		$cat[0]['label'],
		$cat[0]['industry_code'],
		$cat[0]['has_storefront_package'] ? 1 : 0,
		$cat[0]['live_url'],
		$cat[1]['label'],
		$cat[1]['erp_pack_label'],
		count($pkg),
		$pkg[0]['id'],
		$pkg[0]['implemented'] ? 1 : 0,
	);
}

function sail_run_apply()
{
	list($user, $pass, $host, $port) = sail_dsn();
	$schema = 'ecomae_cpw_sail_' . substr(md5(uniqid('', true)), 0, 8);
	$admin = new PDO("mysql:host={$host};port={$port};charset=utf8mb4", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$admin->exec("CREATE DATABASE `{$schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
	try {
		$db = new PDO("mysql:host={$host};port={$port};dbname={$schema};charset=utf8mb4", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
		sail_seed();
		sail_include();
		$bad = epc_th_apply_industry_template($db, 'acme', '!!!');
		$miss = epc_th_apply_industry_template($db, 'acme', 'nope');
		$ok = epc_th_apply_industry_template($db, 'acme', 'automotive');
		return array(
			$bad['ok'] ? 1 : 0,
			$bad['message'],
			$miss['ok'] ? 1 : 0,
			$miss['message'],
			$ok['ok'] ? 1 : 0,
			$ok['message'],
			$ok['template_key'],
			$GLOBALS['SAIL_APPLY'][0],
		);
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$schema}`");
	}
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_sail_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	sail_patch($root . '/content/shop/tenant_hub/epc_tenant_templates_catalog.php', $tmp . '/cat.php');
	$GLOBALS['SAIL_PAGE'] = $tmp . '/cat.php';
	sail_boot();
	$fn = 'sail_run_' . $case['name'];
	$result = $fn();
	@unlink($tmp . '/cat.php');
	@rmdir($tmp);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN'))
		. ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1sail_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1sail_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
