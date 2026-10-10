<?php
// PHP 8.3 goldens for plan Q1-reef (CP prices manager performance helpers).
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function reef_freeze($value)
{
	if (is_string($value)) {
		$value = preg_replace('/ecomae_cpw_[a-f0-9]+/', 'TENANT_DB', $value);
		if (preg_match('/Failed to connect|Connection refused|Could not connect|Empty response|curl_init failed/i', $value)) {
			return 'NET';
		}
		return $value;
	}
	if (is_array($value)) {
		$out = array();
		foreach ($value as $k => $v) {
			$out[$k] = reef_freeze($v);
		}
		return $out;
	}
	return $value;
}

function reef_schema(PDO $pdo, bool $withRecordsCount, bool $withCron)
{
	$extra = $withRecordsCount ? ', `records_count` int NOT NULL DEFAULT 0' : '';
	$pdo->exec('CREATE TABLE `shop_docpart_prices` (
		`id` int NOT NULL,
		`name` varchar(64) NOT NULL DEFAULT \'\'' . $extra . ',
		PRIMARY KEY (`id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('CREATE TABLE `shop_docpart_prices_data` (
		`id` int NOT NULL AUTO_INCREMENT,
		`price_id` int NOT NULL,
		PRIMARY KEY (`id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	if ($withCron) {
		$pdo->exec('CREATE TABLE `shop_docpart_pyprices_crontab_prices` (
			`id` int NOT NULL AUTO_INCREMENT,
			`price_id` int NOT NULL,
			PRIMARY KEY (`id`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	}
}

function reef_norm_rows(array $rows): array
{
	$out = array();
	foreach ($rows as $row) {
		$out[] = array(
			'id' => (int) ($row['id'] ?? 0),
			'records_count' => (int) ($row['records_count'] ?? 0),
			'cron_tasks_count' => (int) ($row['cron_tasks_count'] ?? 0),
		);
	}
	return $out;
}

function reef_run_pure()
{
	$_GET = array();
	$_SERVER['HTTP_HOST'] = 'shop.example:8443';
	$plain = epc_prices_is_platform_operator_request();
	$largePlain = epc_prices_is_large_tenant_host();
	$pollPlain = epc_prices_external_poll_interval_ms();
	$_SERVER['HTTP_HOST'] = 'www.ecomae.com';
	$plat = epc_prices_is_platform_operator_request();
	$largePlat = epc_prices_is_large_tenant_host();
	$pollPlat = epc_prices_external_poll_interval_ms();
	$_SERVER['HTTP_HOST'] = 'cp.ecomae.com:443';
	$cp = epc_prices_is_platform_operator_request();
	$_SERVER['HTTP_HOST'] = 'demo.epartscart.com';
	$eparts = epc_prices_is_large_tenant_host();
	$pollEparts = epc_prices_external_poll_interval_ms();
	unset($_SERVER['HTTP_HOST']);
	$emptyHost = epc_prices_is_large_tenant_host();
	$cleanMiss = epc_prices_should_run_tables_cleaner();
	$_GET['epc_clean_pyprices'] = '';
	$cleanEmpty = epc_prices_should_run_tables_cleaner();
	$_GET['epc_clean_pyprices'] = '0';
	$cleanZero = epc_prices_should_run_tables_cleaner();
	$defer = epc_prices_defer_inline_update_history();
	if (!function_exists('epc_portal_is_platform_hostname')) {
		function epc_portal_is_platform_hostname(): bool
		{
			return true;
		}
	}
	$_SERVER['HTTP_HOST'] = 'other.test';
	$viaFn = epc_prices_is_platform_operator_request();
	$viaLarge = epc_prices_is_large_tenant_host();
	return reef_freeze(array(
		$plain, $largePlain, $pollPlain,
		$plat, $largePlat, $pollPlat,
		$cp, $eparts, $pollEparts, $emptyHost,
		$cleanMiss, $cleanEmpty, $cleanZero, $defer,
		$viaFn, $viaLarge,
	));
}

function reef_run_lists(PDO $pdo)
{
	reef_schema($pdo, true, true);
	$pdo->exec("INSERT INTO `shop_docpart_prices` (`id`,`name`,`records_count`) VALUES (1,'a',12),(2,'b',0)");
	$pdo->exec('INSERT INTO `shop_docpart_prices_data` (`price_id`) VALUES (1),(1),(2)');
	$pdo->exec('INSERT INTO `shop_docpart_pyprices_crontab_prices` (`price_id`) VALUES (1),(1),(1)');
	$filled = reef_norm_rows(epc_prices_fetch_lists_rows($pdo));
	$pdo->exec('UPDATE `shop_docpart_prices` SET `records_count` = 0');
	$live = reef_norm_rows(epc_prices_fetch_lists_rows($pdo));
	$persisted = $pdo->query('SELECT `id`, `records_count` FROM `shop_docpart_prices` ORDER BY `id`')->fetchAll(PDO::FETCH_ASSOC);
	foreach ($persisted as &$r) {
		$r['id'] = (int) $r['id'];
		$r['records_count'] = (int) $r['records_count'];
	}
	unset($r);
	$pdo->exec('DELETE FROM `shop_docpart_prices`');
	$none = reef_norm_rows(epc_prices_fetch_lists_rows($pdo));
	$badCol = epc_prices_table_has_column($pdo, 'shop-doc', 'records_count');
	$okCol = epc_prices_table_has_column($pdo, 'shop_docpart_prices', 'records_count');
	$missCol = epc_prices_table_has_column($pdo, 'shop_docpart_prices', 'nope');
	$map = epc_prices_live_counts_map($pdo);
	$normMap = array();
	if (is_array($map)) {
		foreach ($map as $k => $v) {
			$normMap[(string) (int) $k] = (int) $v;
		}
		ksort($normMap);
		$map = $normMap;
	}
	return reef_freeze(array($filled, $live, $persisted, $none, $badCol, $okCol, $missCol, $map));
}

function reef_run_indexes(PDO $pdo)
{
	reef_schema($pdo, true, true);
	epc_prices_ensure_listing_indexes($pdo, false);
	$idxBefore = $pdo->query("SHOW INDEX FROM `shop_docpart_prices_data` WHERE `Key_name` = 'x_price_id'")->fetchAll();
	// static $done already true — this second call must no-op even with allowAlter.
	epc_prices_ensure_listing_indexes($pdo, true);
	$idxStill = $pdo->query("SHOW INDEX FROM `shop_docpart_prices_data` WHERE `Key_name` = 'x_price_id'")->fetchAll();
	epc_prices_add_index_if_missing($pdo, 'shop-doc', 'x_price_id', '(`price_id`)');
	epc_prices_add_index_if_missing($pdo, 'shop_docpart_prices_data', 'x price', '(`price_id`)');
	epc_prices_add_index_if_missing($pdo, 'shop_docpart_prices_data', 'x_name', '(`price_id`)');
	$idxName = $pdo->query("SHOW INDEX FROM `shop_docpart_prices_data` WHERE `Key_name` = 'x_name'")->fetchAll();
	$idxPrice = $pdo->query("SHOW INDEX FROM `shop_docpart_prices_data` WHERE `Key_name` = 'x_price_id'")->fetchAll();
	return reef_freeze(array(
		count($idxBefore),
		count($idxStill),
		count($idxName) > 0,
		count($idxPrice) > 0,
	));
}

function reef_run_health()
{
	$cfgOk = (object) array('domain_path' => 'https://shop.example/', 'tech_key' => 'ok-key');
	$cfgZero = (object) array('domain_path' => 'https://shop.example/', 'tech_key' => 'zero');
	$cfgPlain = (object) array('domain_path' => 'https://shop.example/', 'tech_key' => 'plain');
	$cfgDead = (object) array('domain_path' => 'https://shop.example/', 'tech_key' => 'dead');
	$ok = epc_pyprices_health_check($cfgOk, 2);
	$zero = epc_pyprices_health_check($cfgZero, 2);
	$plain = epc_pyprices_health_check($cfgPlain, 2);
	$dead = epc_pyprices_health_check($cfgDead, 1);
	return reef_freeze(array($ok, $zero, $plain, $dead));
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$cleanup = function () use ($admin, $dbName) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
	};
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	if (!defined('CURLOPT_RETURNTRANSFER')) { define('CURLOPT_RETURNTRANSFER', 19913); }
	if (!defined('CURLOPT_POST')) { define('CURLOPT_POST', 47); }
	if (!defined('CURLOPT_POSTFIELDS')) { define('CURLOPT_POSTFIELDS', 10015); }
	if (!defined('CURLOPT_SSL_VERIFYHOST')) { define('CURLOPT_SSL_VERIFYHOST', 81); }
	if (!defined('CURLOPT_SSL_VERIFYPEER')) { define('CURLOPT_SSL_VERIFYPEER', 64); }
	if (!defined('CURLOPT_FOLLOWLOCATION')) { define('CURLOPT_FOLLOWLOCATION', 52); }
	if (!defined('CURLOPT_CONNECTTIMEOUT')) { define('CURLOPT_CONNECTTIMEOUT', 78); }
	if (!defined('CURLOPT_TIMEOUT')) { define('CURLOPT_TIMEOUT', 13); }
	if (!function_exists('curl_init')) {
		function curl_init($url = null)
		{
			$GLOBALS['REEF_CURL'] = array('url' => $url, 'opts' => array(), 'err' => '');
			return 'reef-curl';
		}
		function curl_setopt_array($ch, $opts)
		{
			$GLOBALS['REEF_CURL']['opts'] = $opts;
			return true;
		}
		function curl_exec($ch)
		{
			$post = (string) ($GLOBALS['REEF_CURL']['opts'][10015] ?? $GLOBALS['REEF_CURL']['opts']['CURLOPT_POSTFIELDS'] ?? '');
			if ($post === '') {
				foreach ($GLOBALS['REEF_CURL']['opts'] as $v) {
					if (is_string($v) && strpos($v, 'just_test_db=') !== false) {
						$post = $v;
					}
				}
			}
			if (strpos($post, 'key=dead') !== false) {
				$GLOBALS['REEF_CURL']['err'] = 'Failed to connect';
				return false;
			}
			if (strpos($post, 'key=ok-key') !== false) {
				return json_encode(array('status' => true, 'message' => 'OK'));
			}
			if (strpos($post, 'key=zero') !== false) {
				return json_encode(array('status' => 0, 'message' => 'no'));
			}
			return 'plain-fail-body';
		}
		function curl_error($ch)
		{
			return (string) ($GLOBALS['REEF_CURL']['err'] ?? '');
		}
		function curl_close($ch)
		{
		}
	}
	require $root . '/cp/content/shop/prices_upload/epc_prices_manager_perf.php';
	$_SERVER = array();
	$_GET = array();
	$GLOBALS['db_link'] = $pdo;
	register_shutdown_function($cleanup);
	if (isset($case['eval'])) {
		$result = eval($case['eval']);
		echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	}
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN')) . ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1r_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1r_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
