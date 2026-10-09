<?php
// PHP 8.3 goldens for plan Q1-keep (industry SEO + BOC tenant scope).
// Consolidation / live-bridge / unified / portal parents are stubbed, not copied.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1k_' . substr(md5(uniqid('', true)), 0, 12);
	@mkdir($doc . '/content/general_pages/industry_templates', 0777, true);
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	file_put_contents($doc . '/content/general_pages/industry_templates/keeptest.php', <<<'PHP'
<?php
$industryData = array(
	'sub_industries' => array('Keep Alpha', 'Keep Beta & bio', 'Zero'),
	'Keep Alpha' => array(
		'note' => 'x',
		'categories' => array('Cat A', 'Cat B'),
	),
	'Keep Beta & bio' => array(
		'categories' => array('X', 'Y\'s'),
	),
);
PHP
	);
	file_put_contents($doc . '/content/general_pages/' . 'epc_industry_consolidation.php', <<<'PHP'
<?php
function epc_industry_groups() {
	return isset($GLOBALS['__groups']) ? $GLOBALS['__groups'] : array();
}
PHP
	);
	file_put_contents($doc . '/content/general_pages/' . 'epc_portal_industry_live_bridge.php', <<<'PHP'
<?php
function epc_portal_industry_live_defs() {
	return isset($GLOBALS['__livedefs']) ? $GLOBALS['__livedefs'] : array();
}
PHP
	);
	file_put_contents($doc . '/content/general_pages/' . 'epc_bos_unified.php', <<<'PHP'
<?php
function epc_bos_tenant_list($pdo) {
	return isset($GLOBALS['__tenants']) ? $GLOBALS['__tenants'] : array();
}
PHP
	);
	copy($root . '/content/general_pages/epc_industry_seo.php', $doc . '/content/general_pages/epc_industry_seo.php');
	copy($root . '/content/general_pages/epc_boc_tenant_scope.php', $doc . '/content/general_pages/epc_boc_tenant_scope.php');
	$cleanup = function () use ($doc, $admin, $dbName) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
		$it = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($doc, FilesystemIterator::SKIP_DOTS), RecursiveIteratorIterator::CHILD_FIRST);
		foreach ($it as $file) {
			$file->isDir() ? @rmdir($file->getPathname()) : @unlink($file->getPathname());
		}
		@rmdir($doc);
	};
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$GLOBALS['db_link'] = $pdo;
	$GLOBALS['__super'] = false;
	$GLOBALS['__groups'] = array(
		array('template_key' => 'keeptest', 'available_sub_areas' => array('Fallback One')),
		array('template_key' => 'emptytpl', 'available_sub_areas' => array('Only Fallback')),
		array('template_key' => '', 'available_sub_areas' => array('Nope')),
		array('template_key' => 'keeptest', 'available_sub_areas' => array('Dup')),
	);
	$GLOBALS['__livedefs'] = array(
		array('template_key' => 'keeptest', 'mode' => 'inject', 'sub_label' => 'Injected Sub', 'categories' => array('Inj A', 'Inj B')),
		array('template_key' => 'keeptest', 'mode' => 'other', 'sub_label' => 'Skip Mode', 'categories' => array('No')),
		array('template_key' => 'other', 'mode' => 'inject', 'sub_label' => 'Wrong Tpl', 'categories' => array('No')),
	);
	$GLOBALS['__tenants'] = array(
		array('site_key' => 'ecomae', 'hostname' => 'ecomae.com', 'trade_name' => 'Plat', 'type' => 'commerce', 'status' => 'on'),
		array('site_key' => 'skiphost', 'hostname' => 'www.ecomae.com', 'trade_name' => 'X', 'type' => 'commerce', 'status' => 'on'),
		array('site_key' => 'plat', 'hostname' => 'p.example', 'trade_name' => 'P', 'type' => 'platform', 'status' => 'on'),
		array('site_key' => 'acme', 'hostname' => 'www.acme.test', 'trade_name' => "Acme's Co", 'type' => 'commerce', 'status' => 'live'),
		array('site_key' => 'demo_x', 'hostname' => 'demo.test', 'hub_name' => 'Demo X', 'type' => 'demo', 'status' => 'on'),
		array('site_key' => 'erp1', 'hostname' => 'erp.test', 'trade_name' => '', 'type' => 'erp_only', 'status' => 'on'),
		array('site_key' => '', 'hostname' => 'x.test', 'trade_name' => 'Blank', 'type' => 'commerce', 'status' => 'on'),
	);
	if (!function_exists('epc_portal_is_super_cp_host')) {
		function epc_portal_is_super_cp_host() {
			return !empty($GLOBALS['__super']);
		}
	}
	$name = (string) $case['name'];
	if (str_starts_with($name, 'seo_')) {
		require $doc . '/content/general_pages/epc_industry_seo.php';
	} else {
		if (session_status() === PHP_SESSION_NONE) {
			@session_start();
		}
		require $doc . '/content/general_pages/epc_boc_tenant_scope.php';
	}
	register_shutdown_function(function () use ($case, $cleanup) {
		$html = ob_get_clean();
		$cleanup();
		echo json_encode(array('name' => $case['name'], 'output' => $html, 'result' => $GLOBALS['__result'] ?? null), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1k_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1k_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
