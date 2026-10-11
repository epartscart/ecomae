<?php
// PHP 8.3 goldens for plan Q1-wind (price-upload diagnostics). Leftover history parent stays stubbed. HTTP injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function wind_dsn(): array
{
	$pass = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: '';
	if ($pass === '') {
		throw new RuntimeException('missing ECOMAE_LOCAL_MARIADB_E2E_DSN');
	}
	return array('ecomae', $pass, '127.0.0.1', '3306');
}

function wind_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("require_once __DIR__ . '/docpart_price_upload_history.php';", '// leftover history stubbed', $code);
	file_put_contents($dest, $code);
}

function wind_boot(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', true);
	}
	if (!function_exists('epc_price_history_ensure_schema')) {
		eval('function epc_price_history_ensure_schema($db) { $GLOBALS["WIND_SCHEMA"] = 1; }');
	}
	if (!function_exists('curl_init')) {
		eval('function curl_init($url = null) { $GLOBALS["WIND_CURL_URL"] = (string) $url; return 1; }');
		eval('function curl_setopt($ch, $opt, $val) { $GLOBALS["WIND_CURL"][(string) $opt] = $val; }');
		eval('function curl_exec($ch) { return (string) ($GLOBALS["WIND_BODY"] ?? ""); }');
		eval('function curl_getinfo($ch, $opt = null) { return (int) ($GLOBALS["WIND_CODE"] ?? 0); }');
		eval('function curl_close($ch) {}');
	}
	if (!defined('CURLOPT_RETURNTRANSFER')) {
		define('CURLOPT_RETURNTRANSFER', 19913);
		define('CURLOPT_TIMEOUT', 13);
		define('CURLOPT_SSL_VERIFYHOST', 81);
		define('CURLOPT_SSL_VERIFYPEER', 64);
		define('CURLOPT_FOLLOWLOCATION', 52);
		define('CURLOPT_POST', 47);
		define('CURLOPT_POSTFIELDS', 10015);
		define('CURLINFO_HTTP_CODE', 2097154);
	}
}

function wind_include(): void
{
	include_once $GLOBALS['WIND_PAGE'];
}

function wind_run_url()
{
	wind_include();
	return array(
		epc_pyprices_api_url(''),
		epc_pyprices_api_url('https://shop.example/'),
		epc_pyprices_api_url('https://shop.example'),
	);
}

function wind_run_channels()
{
	wind_include();
	$rows = epc_price_upload_channel_definitions(array(
		'backend_dir' => 'cp',
		'domain_path' => 'https://shop.example',
		'tech_key' => 'k1',
	));
	$slash = epc_price_upload_channel_definitions(array(
		'backend_dir' => 'cp',
		'domain_path' => 'https://shop.example/',
		'tech_key' => 'k1',
	));
	$cron = null;
	foreach ($rows as $r) {
		if ($r['id'] === 'cron_scheduled') {
			$cron = $r['cron_wget'];
		}
	}
	$cronSlash = null;
	foreach ($slash as $r) {
		if ($r['id'] === 'cron_scheduled') {
			$cronSlash = $r['cron_wget'];
		}
	}
	return array(count($rows), $rows[0]['cp_url'], $cron, $cronSlash, $rows[6]['id']);
}

function wind_run_snap()
{
	list($user, $pass, $host, $port) = wind_dsn();
	$schema = 'ecomae_cpw_wind_' . substr(md5(uniqid('', true)), 0, 8);
	$admin = new PDO("mysql:host={$host};port={$port};charset=utf8mb4", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$admin->exec("CREATE DATABASE `{$schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
	try {
		$db = new PDO("mysql:host={$host};port={$port};dbname={$schema};charset=utf8mb4", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
		$db->exec('CREATE TABLE shop_docpart_prices_load_modes (id INT, name VARCHAR(32))');
		$db->exec("INSERT INTO shop_docpart_prices_load_modes VALUES (1,'Manual'),(2,'FTP')");
		$db->exec('CREATE TABLE shop_docpart_prices (id INT, name VARCHAR(64), load_mode INT, last_updated VARCHAR(32))');
		$db->exec("INSERT INTO shop_docpart_prices VALUES (3,'Beta',1,'2026-01-02'),(2,'Acme',1,'2026-01-01')");
		$db->exec('CREATE TABLE shop_docpart_prices_data (price_id INT)');
		$db->exec('INSERT INTO shop_docpart_prices_data VALUES (2),(2),(3)');
		$db->exec('CREATE TABLE epc_price_upload_history (upload_source VARCHAR(32), created_at VARCHAR(32))');
		$db->exec("INSERT INTO epc_price_upload_history VALUES ('cp_wizard','2026-02-01'),('cp_wizard','2026-02-02')");
		wind_include();
		$cfg = array('backend_dir' => 'cp', 'domain_path' => '', 'tech_key' => '');
		$ok = epc_price_upload_diagnostics_snapshot($db, $cfg);
		$missing = epc_price_upload_diagnostics_snapshot($db, $cfg);
		// drop cron tables so second snapshot hits catch
		// first snapshot already ran with missing cron → -1
		return array(
			(int) $GLOBALS['WIND_SCHEMA'],
			$ok['price_lists_total'],
			$ok['by_load_mode'][1]['count'],
			$ok['by_load_mode'][1]['records'],
			$ok['by_load_mode'][1]['lists'][0]['name'],
			$ok['history_by_source']['cp_wizard']['uploads'],
			$ok['cron_tasks'],
			$ok['pyprices_pending_tasks'],
			count($ok['channels']),
		);
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$schema}`");
	}
}

function wind_run_health()
{
	wind_include();
	$GLOBALS['WIND_BODY'] = json_encode(array('status' => true, 'list_to_handle' => array()));
	$GLOBALS['WIND_CODE'] = 200;
	$tmp = sys_get_temp_dir() . '/ecomae_wind_h_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp . '/cp/tmp/prices_upload_files', 0777, true);
	@mkdir($tmp . '/content/files/price_upload_history', 0777, true);
	file_put_contents($tmp . '/epc-upload-uae-prices.php', '1');
	@mkdir($tmp . '/cp/content/shop/prices_upload', 0777, true);
	file_put_contents($tmp . '/cp/content/shop/prices_upload/ajax_1_prepare_tmp_dir.php', '1');
	file_put_contents($tmp . '/cp/content/shop/prices_upload/ajax_5_import_csv_to_db.php', '1');
	file_put_contents($tmp . '/cp/content/shop/prices_upload/ajax_6_complete_session.php', '1');
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	$ok = epc_price_upload_run_health_checks(array(
		'domain_path' => 'https://shop.example',
		'backend_dir' => 'cp',
		'tech_key' => 'k1',
		'tmp_dir_prices_upload' => '/tmp/prices_upload_files',
	));
	$GLOBALS['WIND_BODY'] = 'nope';
	$GLOBALS['WIND_CODE'] = 503;
	$fail = epc_price_upload_run_health_checks(array(
		'domain_path' => 'https://shop.example',
		'backend_dir' => 'cp',
		'tech_key' => 'k1',
		'tmp_dir_prices_upload' => '/tmp/prices_upload_files',
	));
	// cleanup
	@unlink($tmp . '/epc-upload-uae-prices.php');
	@unlink($tmp . '/cp/content/shop/prices_upload/ajax_1_prepare_tmp_dir.php');
	@unlink($tmp . '/cp/content/shop/prices_upload/ajax_5_import_csv_to_db.php');
	@unlink($tmp . '/cp/content/shop/prices_upload/ajax_6_complete_session.php');
	@rmdir($tmp . '/cp/content/shop/prices_upload');
	@rmdir($tmp . '/cp/content/shop');
	@rmdir($tmp . '/cp/content');
	@rmdir($tmp . '/cp/tmp/prices_upload_files');
	@rmdir($tmp . '/cp/tmp');
	@rmdir($tmp . '/cp');
	@rmdir($tmp . '/content/files/price_upload_history');
	@rmdir($tmp . '/content/files');
	@rmdir($tmp . '/content');
	@rmdir($tmp);
	return array(
		!empty($ok['all_ok']) ? 1 : 0,
		!empty($ok['checks']['pyprices_api_reachable']['ok']) ? 1 : 0,
		$ok['pyprices_url'],
		!empty($fail['all_ok']) ? 1 : 0,
		!empty($fail['checks']['cron_crutch']['ok']) ? 1 : 0,
		substr((string) $fail['checks']['cron_crutch']['detail'], 0, 8),
	);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_wind_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	wind_patch($root . '/content/shop/docpart/epc_price_upload_diagnostics.php', $tmp . '/diag.php');
	$GLOBALS['WIND_PAGE'] = $tmp . '/diag.php';
	wind_boot();
	$fn = 'wind_run_' . $case['name'];
	$result = $fn();
	@unlink($tmp . '/diag.php');
	@rmdir($tmp);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN'))
		. ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1wind_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1wind_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
