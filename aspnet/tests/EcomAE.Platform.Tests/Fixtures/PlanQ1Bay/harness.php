<?php
// PHP 8.3 goldens for plan Q1-bay (Power BI helpers).
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function bay_strip_row($row)
{
	if (!is_array($row)) {
		return $row;
	}
	unset($row['created_at'], $row['updated_at']);
	return $row;
}

function bay_freeze($value)
{
	if (is_string($value)) {
		$value = preg_replace('/ecomae_cpw_[a-f0-9]+/', 'TENANT_DB', $value);
		return $value;
	}
	if (is_array($value)) {
		$out = array();
		foreach ($value as $k => $v) {
			if ($k === 'created_at' || $k === 'updated_at') {
				continue;
			}
			$out[$k] = bay_freeze($v);
		}
		return $out;
	}
	return $value;
}

function bay_run_pure(PDO $pdo)
{
	$catalogDefault = epc_power_bi_dataset_catalog();
	$catalogBase = epc_power_bi_dataset_catalog('https://shop.example/');
	$guide = epc_power_bi_guide_steps();
	$caps = epc_power_bi_capabilities();
	$allow = array(
		epc_power_bi_embed_url_allowed('https://app.powerbi.com/reportEmbed?id=1'),
		epc_power_bi_embed_url_allowed('https://xxx.powerbi.us/'),
		epc_power_bi_embed_url_allowed('https://powerbi.com'),
		epc_power_bi_embed_url_allowed('http://app.powerbi.com/x'),
		epc_power_bi_embed_url_allowed('https://evil.com/?powerbi.com'),
		epc_power_bi_embed_url_allowed(''),
		epc_power_bi_embed_url_allowed('https://APP.POWERBI.COM/x'),
		epc_power_bi_embed_url_allowed('https://a.b.powerbi.com/report'),
		epc_power_bi_embed_url_allowed('  https://app.powerbi.com/x  '),
	);
	$_GET = array();
	$_SERVER = array();
	$wantNone = epc_power_bi_wants_csv();
	$_GET['format'] = 'CSV';
	$wantFmt = epc_power_bi_wants_csv();
	$_GET['format'] = ' json ';
	$_SERVER['HTTP_ACCEPT'] = 'text/html, text/csv';
	$wantAccept = epc_power_bi_wants_csv();
	$_GET['format'] = '0';
	unset($_SERVER['HTTP_ACCEPT']);
	$wantZero = epc_power_bi_wants_csv();

	$_GET = array();
	$parseEmpty = epc_power_bi_parse_date_param('from', 100);
	$_GET['from'] = '2026-01-15';
	$parseOk = epc_power_bi_parse_date_param('from', 100);
	$_GET['from'] = '2026-13-40';
	$parseBad = epc_power_bi_parse_date_param('from', 100);
	$_GET['from'] = ' 2026-01-15 ';
	$parseTrim = epc_power_bi_parse_date_param('from', 100);
	$_GET['from'] = '2026-01-15';
	$parseNoFb = epc_power_bi_parse_date_param('from');

	if (function_exists('header_remove')) {
		header_remove();
	}
	ob_start();
	epc_power_bi_emit_csv(
		array('a', 'b'),
		array(
			array('1', 'two'),
			array('x,y', 'say "hi"'),
			array(true, false),
			array(null, 0),
		),
		'kpi report.csv'
	);
	$csvBody = ob_get_clean();
	$csv = array(
		'headers' => headers_list(),
		'body_b64' => base64_encode($csvBody),
	);

	$kpisMiss = epc_power_bi_dataset_kpis($pdo, 'alpha');
	$ordersMiss = epc_power_bi_dataset_orders($pdo, 'alpha', 10);
	$reportMiss = epc_power_bi_dataset_report($pdo, 'sales', 0, 1768435200);
	$metricsMiss = epc_power_bi_dataset_metrics($pdo, 'alpha');
	return bay_freeze(array(
		$catalogDefault,
		$catalogBase,
		$guide,
		$caps,
		$allow,
		array($wantNone, $wantFmt, $wantAccept, $wantZero),
		array($parseEmpty, $parseOk, $parseBad, $parseTrim, $parseNoFb),
		$csv,
		$kpisMiss,
		$ordersMiss,
		$reportMiss,
		$metricsMiss,
	));
}

function bay_run_config(PDO $pdo)
{
	$first = epc_power_bi_configure($pdo, 'Acme Site!', array(
		'workspace_id' => str_repeat('W', 80),
		'azure_tenant_id' => 'tid',
		'default_report_id' => 'rid',
		'default_dataset_id' => 'did',
		'embed_url' => 'https://app.powerbi.com/x',
		'embed_mode' => 'URL',
		'notes' => str_repeat('N', 600),
	));
	$second = epc_power_bi_configure($pdo, 'Tenant_01-X', array(
		'workspace_id' => 'ws-2',
		'embed_mode' => 'url',
		'embed_url' => 'https://app.powerbi.com/t2',
		'notes' => "O'Reilly & <x>",
	));
	$upsert = epc_power_bi_configure($pdo, 'Tenant_01-X', array(
		'workspace_id' => 'ws-2b',
		'embed_mode' => 'azure',
		'notes' => 'updated',
	));
	$emptyKey = epc_power_bi_configure($pdo, '!!!', array('embed_mode' => 'none'));
	$gotSanitized = bay_strip_row(epc_power_bi_config_get($pdo, 'acmesite'));
	$gotRawMiss = epc_power_bi_config_get($pdo, 'Acme Site!');
	$gotTenant = bay_strip_row(epc_power_bi_config_get($pdo, 'tenant_01-x'));
	$gotEmpty = bay_strip_row(epc_power_bi_config_get($pdo, ''));
	$gotMiss = epc_power_bi_config_get($pdo, 'nope');
	epc_power_bi_register_report($pdo, 'acmesite', array(
		'report_id' => 'r1',
		'report_name' => 'Zed',
		'dataset_id' => 'd1',
		'category' => 'ops',
		'embed_url' => 'https://app.powerbi.com/r1',
	));
	epc_power_bi_register_report($pdo, 'acmesite', array(
		'report_id' => 'r0',
		'dataset_id' => 'd0',
	));
	epc_power_bi_register_report($pdo, 'other', array(
		'report_id' => 'hidden',
		'report_name' => 'Hidden',
		'category' => 'ops',
	));
	$pdo->prepare(
		'INSERT INTO `epc_power_bi_reports` (`site_key`,`report_id`,`report_name`,`category`,`active`) VALUES (?,?,?,?,0)'
	)->execute(array('acmesite', 'dead', 'Dead', 'ops'));
	$list = array_map('bay_strip_row', epc_power_bi_reports_list($pdo, 'acmesite'));
	$listOther = array_map('bay_strip_row', epc_power_bi_reports_list($pdo, 'other'));
	$fleet = bay_freeze(epc_power_bi_fleet_stats($pdo));
	return bay_freeze(array(
		$first,
		$second,
		$upsert,
		$emptyKey,
		$gotSanitized,
		$gotRawMiss,
		$gotTenant,
		$gotEmpty,
		$gotMiss,
		$list,
		$listOther,
		$fleet,
	));
}

function bay_run_embed(PDO $pdo)
{
	$missing = epc_power_bi_embed_resolve($pdo, 'alpha');
	epc_power_bi_configure($pdo, '__platform__', array(
		'workspace_id' => 'plat-ws',
		'default_report_id' => 'plat-r',
		'azure_tenant_id' => 'plat-t',
		'embed_url' => 'https://app.powerbi.com/plat',
		'embed_mode' => 'url',
	));
	$viaPlat = epc_power_bi_embed_resolve($pdo, 'alpha');
	epc_power_bi_configure($pdo, 'alpha', array(
		'workspace_id' => 'a-ws',
		'default_report_id' => 'a-r',
		'azure_tenant_id' => 'a-t',
		'embed_mode' => 'none',
		'embed_url' => 'https://app.powerbi.com/ignored',
	));
	$pdo->exec("UPDATE `epc_power_bi_config` SET `active`=0 WHERE `site_key`='alpha'");
	$inactiveFallsBack = epc_power_bi_embed_resolve($pdo, 'alpha');
	$pdo->exec("UPDATE `epc_power_bi_config` SET `active`=1 WHERE `site_key`='alpha'");
	$modeNone = epc_power_bi_embed_resolve($pdo, 'alpha');
	epc_power_bi_configure($pdo, 'alpha', array(
		'workspace_id' => 'a-ws',
		'default_report_id' => 'a-r',
		'azure_tenant_id' => 'a-t',
		'embed_mode' => 'azure',
		'embed_url' => 'https://app.powerbi.com/ignored',
	));
	$azure = epc_power_bi_embed_resolve($pdo, 'alpha');
	epc_power_bi_configure($pdo, 'alpha', array(
		'workspace_id' => 'a-ws',
		'default_report_id' => 'a-r',
		'embed_mode' => 'url',
		'embed_url' => 'https://evil.example/x',
	));
	$badUrl = epc_power_bi_embed_resolve($pdo, 'alpha');
	epc_power_bi_configure($pdo, 'alpha', array(
		'workspace_id' => 'a-ws',
		'default_report_id' => 'a-r',
		'embed_mode' => 'url',
		'embed_url' => 'https://app.powerbi.com/good',
	));
	$ok = epc_power_bi_embed_resolve($pdo, 'alpha');
	epc_power_bi_register_report($pdo, 'alpha', array(
		'report_id' => 'rep-1',
		'report_name' => 'R1',
		'embed_url' => 'https://app.powerbi.com/from-report',
	));
	$repId = (int) $pdo->query("SELECT `id` FROM `epc_power_bi_reports` WHERE `report_id`='rep-1'")->fetchColumn();
	epc_power_bi_configure($pdo, 'alpha', array(
		'workspace_id' => 'a-ws',
		'default_report_id' => 'a-r',
		'azure_tenant_id' => 'a-t',
		'embed_mode' => 'azure',
		'embed_url' => 'https://app.powerbi.com/config',
	));
	$reportOverridesAzure = epc_power_bi_embed_resolve($pdo, 'alpha', $repId);
	epc_power_bi_register_report($pdo, 'alpha', array(
		'report_id' => 'rep-empty',
		'report_name' => 'Empty',
		'embed_url' => '0',
	));
	$emptyRepId = (int) $pdo->query("SELECT `id` FROM `epc_power_bi_reports` WHERE `report_id`='rep-empty'")->fetchColumn();
	$emptyRepFallsThrough = epc_power_bi_embed_resolve($pdo, 'alpha', $emptyRepId);
	return bay_freeze(array(
		$missing,
		$viaPlat,
		$inactiveFallsBack,
		$modeNone,
		$azure,
		$badUrl,
		$ok,
		$reportOverridesAzure,
		$emptyRepFallsThrough,
		$repId,
		$emptyRepId,
	));
}

function bay_run_datasets(PDO $pdo)
{
	function epc_erp_dashboard($tenantPdo)
	{
		return array(
			'date_from' => 1768435200,
			'date_to' => 0,
			'order_count' => 3.9,
			'revenue_ex_vat' => 62.555,
			'profit_ex_vat' => 1.225,
			'receivable_due_orders' => '10.1',
			'cash_bank_total' => null,
		);
	}
	function epc_erp_reports_export($tenantPdo, $type, $dateFrom, $dateTo)
	{
		if ($type === 'boom') {
			throw new RuntimeException('export boom');
		}
		return array(
			'headers' => array('sku', 'qty'),
			'rows' => array(
				array('A1', 2),
				array('B2', 0),
			),
		);
	}
	function epc_bi_latest_all($db, $siteKey)
	{
		if ($siteKey === 'boom') {
			throw new RuntimeException('bi boom');
		}
		return array(
			'revenue' => array('value' => '12.5', 'previous_value' => 10, 'change_pct' => 25, 'period_start' => '2026-01-01', 'computed_at' => '2026-01-15 00:00:00'),
			'orders' => array('value' => 3),
		);
	}

	$kpis = epc_power_bi_dataset_kpis($pdo, 'alpha');
	$pdo->exec('CREATE TABLE `shop_orders` (
		`id` int NOT NULL,
		`time` int NOT NULL,
		`user_id` int NOT NULL,
		`paid` varchar(8) DEFAULT NULL,
		`paid_type` int NOT NULL DEFAULT 0,
		`successfully_created` int NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec("INSERT INTO `shop_orders` (`id`,`time`,`user_id`,`paid`,`paid_type`,`successfully_created`) VALUES
		(10,1768435200,7,'1',2,1),
		(9,0,3,'0',0,1),
		(8,1768435300,1,'',1,1),
		(7,1768435400,2,NULL,0,0),
		(6,1768435500,4,'1',1,1)");
	$ordersDefault = epc_power_bi_dataset_orders($pdo, 'alpha');
	$ordersTwo = epc_power_bi_dataset_orders($pdo, 'alpha', 2);
	$ordersZero = epc_power_bi_dataset_orders($pdo, 'alpha', 0);
	$ordersWide = epc_power_bi_dataset_orders($pdo, 'alpha', 500);
	$sales = epc_power_bi_dataset_report($pdo, 'sales', 1768435200, 1768521600);
	$salesZeroFrom = epc_power_bi_dataset_report($pdo, 'sales', 0, 0);
	$salesBoom = epc_power_bi_dataset_report($pdo, 'boom', 1, 2);
	$metrics = epc_power_bi_dataset_metrics($pdo, 'alpha');
	$metricsBoom = epc_power_bi_dataset_metrics($pdo, 'boom');
	return bay_freeze(array(
		$kpis,
		$ordersDefault,
		$ordersTwo,
		$ordersZero,
		$ordersWide,
		$sales,
		$salesZeroFrom,
		$salesBoom,
		$metrics,
		$metricsBoom,
	));
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
	require $root . '/content/general_pages/epc_power_bi.php';
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1bay_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1bay_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
