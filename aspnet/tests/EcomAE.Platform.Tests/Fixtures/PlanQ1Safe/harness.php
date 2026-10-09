<?php
// PHP 8.3 goldens for plan Q1-safe (warehouse sitemap, tenant data protection, customer mgmt).
// SEO / storage-flag / article-match / portal-tenant / finance parents are stubbed, not copied.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1s_' . substr(md5(uniqid('', true)), 0, 12);
	@mkdir($doc . '/content/general_pages', 0777, true);
	@mkdir($doc . '/content/shop/customer_mgmt', 0777, true);
	@mkdir($doc . '/content/shop/finance', 0777, true);
	@mkdir($doc . '/content/shop/pricing', 0777, true);
	@mkdir($doc . '/content/shop/docpart', 0777, true);
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	file_put_contents($doc . '/content/general_pages/safe_stub.php', <<<'PHP'
<?php
class DP_Config {}
$GLOBALS['__need_price'] = true;
PHP
	);
	file_put_contents($doc . '/content/shop/finance/epc_einvoice.php', <<<'PHP'
<?php
function epc_einvoice_save_buyer_profile($db, $data) {
	$GLOBALS['__buyer'] = $data;
}
PHP
	);
	file_put_contents($doc . '/content/shop/pricing/epc_customer_trade.php', <<<'PHP'
<?php
function epc_trade_profile_set($db, $user_id, $key, $val) {
	$GLOBALS['__trade'][] = array($user_id, $key, $val);
}
PHP
	);
	file_put_contents($doc . '/content/shop/finance/epc_uae_customer_vat.php', <<<'PHP'
<?php
function epc_uae_customer_vat_sync($db, $user_id) {
	$GLOBALS['__vat'][] = $user_id;
}
PHP
	);
	file_put_contents($doc . '/epc_sitemap_lib.php', <<<'PHP'
<?php
function epc_sitemap_lang() { return 'en'; }
function epc_sitemap_part_loc($cfg, $lang, $brand, $article) {
	if (strcasecmp(trim((string) $brand), 'brands') === 0) {
		$loc = 'https://www.epartscart.com/' . $lang . '/parts/brands/' . rawurlencode((string) $article);
		return htmlspecialchars($loc, ENT_XML1, 'UTF-8');
	}
	$b = rawurlencode(mb_strtoupper(trim((string) $brand), 'UTF-8'));
	$a = rawurlencode(strtoupper(preg_replace('/[^A-Za-z0-9]+/', '', (string) $article)));
	return htmlspecialchars('https://www.epartscart.com/' . $lang . '/parts/' . $b . '/' . $a, ENT_XML1, 'UTF-8');
}
PHP
	);
	file_put_contents($doc . '/content/general_pages/epc_seo_indexing.php', <<<'PHP'
<?php
function epc_seo_sitemap_price_clause($pdo) {
	return !empty($GLOBALS['__need_price']) ? ' AND IFNULL(`price`, 0) > 0' : '';
}
PHP
	);
	file_put_contents($doc . '/content/shop/docpart/docpart_article_match.php', "<?php\n");
	file_put_contents($doc . '/content/general_pages/epc_portal_tenant_control.php', <<<'PHP'
<?php
function epc_portal_tenant_control_get_row($pdo, $siteKey) {
	$map = array(
		'liveok' => array('db_name' => 'tenant_live', 'db_password' => 'pw', 'hostname' => 'shop.example.com', 'status' => 'live', 'erp_only_shared' => 0),
		'shared' => array('db_name' => 'docpart', 'db_password' => '', 'hostname' => 'www.ecomae.com', 'status' => 'live', 'erp_only_shared' => 1),
		'ecomaedb' => array('db_name' => 'ecomae', 'db_password' => 'pw', 'hostname' => 'x.example.com', 'status' => 'draft', 'erp_only_shared' => 0),
		'nopass' => array('db_name' => 'tenant_x', 'db_password' => '', 'hostname' => 'x.example.com', 'status' => 'weird', 'erp_only_shared' => 0),
	);
	return $map[$siteKey] ?? null;
}
function epc_portal_tenant_control_tenant_pdo_connect($row) {
	$db = (string) ($row['db_name'] ?? '');
	if ($db === 'tenant_live' || $db === 'docpart') {
		return array('pdo' => $GLOBALS['db_link'], 'error' => '');
	}
	return array('pdo' => null, 'error' => 'denied');
}
PHP
	);
	file_put_contents($doc . '/content/general_pages/epc_portal_tenant.php', <<<'PHP'
<?php
function epc_portal_list_tenants($pdo) {
	return array(
		array('site_key' => 'liveok'),
		array('site_key' => ''),
		array('site_key' => 'missing'),
	);
}
PHP
	);
	copy($root . '/content/general_pages/epc_sitemap_warehouse.php', $doc . '/content/general_pages/epc_sitemap_warehouse.php');
	copy($root . '/content/general_pages/epc_tenant_data_protection.php', $doc . '/content/general_pages/epc_tenant_data_protection.php');
	copy($root . '/content/shop/customer_mgmt/epc_customer_mgmt_helpers.php', $doc . '/content/shop/customer_mgmt/epc_customer_mgmt_helpers.php');
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
	$GLOBALS['__buyer'] = array();
	$GLOBALS['__trade'] = array();
	$GLOBALS['__vat'] = array();
	require $doc . '/content/general_pages/safe_stub.php';
	$name = (string) $case['name'];
	if (str_starts_with($name, 'tdp_')) {
		if ($name === 'tdp_db') {
			eval('function epc_portal_platform_pdo() { return $GLOBALS[\'db_link\']; }');
		}
		require $doc . '/content/general_pages/epc_tenant_data_protection.php';
	} elseif (str_starts_with($name, 'sm_')) {
		require $doc . '/content/general_pages/epc_sitemap_warehouse.php';
	} else {
		require $doc . '/content/shop/customer_mgmt/epc_customer_mgmt_helpers.php';
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1s_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1s_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
