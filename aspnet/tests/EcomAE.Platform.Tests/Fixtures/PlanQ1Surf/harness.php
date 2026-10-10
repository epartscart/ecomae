<?php
// PHP 8.3 goldens for plan Q1-surf (BOC advanced control).
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function surf_freeze($value)
{
	if (is_string($value)) {
		$value = preg_replace('/ecomae_cpw_[a-f0-9]+/', 'TENANT_DB', $value);
		return $value;
	}
	if (is_array($value)) {
		$out = array();
		foreach ($value as $k => $v) {
			$out[$k] = surf_freeze($v);
		}
		return $out;
	}
	return $value;
}

function surf_run_pure()
{
	$money = array(
		epc_boc_adv_money(0),
		epc_boc_adv_money(62.5),
		epc_boc_adv_money(999),
		epc_boc_adv_money(1000),
		epc_boc_adv_money(1234.5),
		epc_boc_adv_money(1000000),
		epc_boc_adv_money(1250000),
		epc_boc_adv_money(1200000),
		epc_boc_adv_money(-2500),
		epc_boc_adv_money(38.4, 'USD'),
	);
	$vendors = epc_boc_vendor_rollup(array(
		array('site_key' => 'a', 'label' => 'Alpha & Co', 'type' => 'commerce', 'ok' => 1, 'vendors' => 9, 'active_vendors' => 7, 'rfq_open' => 2, 'spend' => 1250000, 'currency' => 'AED', 'note' => ''),
		array('site_key' => 'b', 'label' => "Beta's", 'ok' => '0', 'vendors' => 3, 'active_vendors' => 1, 'rfq_open' => 0, 'spend' => 62.5, 'note' => 'down'),
		array('site_key' => 'c', 'label' => 'Gamma', 'type' => 'demo', 'ok' => true, 'vendors' => 1, 'spend' => 0),
	));
	$warehouses = epc_boc_warehouse_rollup(array(
		array('site_key' => 'a', 'label' => 'Alpha', 'ok' => 1, 'warehouses' => 2, 'skus' => 40, 'stock_value' => 8800, 'low_stock' => 1, 'out_of_stock' => 0),
		array('site_key' => 'b', 'label' => 'Beta', 'ok' => 0, 'warehouses' => 1, 'skus' => 4, 'stock_value' => 10, 'low_stock' => 0, 'out_of_stock' => 0, 'note' => 'down'),
		array('site_key' => 'c', 'label' => 'Gamma', 'ok' => 1, 'warehouses' => 1, 'skus' => 8, 'stock_value' => 500, 'low_stock' => 0, 'out_of_stock' => 2),
	));
	$channels = epc_boc_channel_rollup(array(
		array('site_key' => 'a', 'label' => 'Alpha', 'ok' => 1, 'web' => 1, 'pos' => 1, 'api' => '0', 'marketplaces' => 2, 'arbitrage' => 1),
		array('site_key' => 'b', 'label' => 'Beta', 'ok' => 0, 'web' => 0, 'pos' => 0, 'api' => 0, 'marketplaces' => 0),
		array('site_key' => 'c', 'label' => 'Gamma', 'type' => 'demo', 'ok' => 1, 'web' => true, 'pos' => false, 'api' => 1, 'marketplaces' => 0, 'arbitrage' => '0'),
	));
	$tiles = array(
		epc_boc_adv_tile('Vendors', '1,234'),
		epc_boc_adv_tile('Active', '7', 'green', "Beta's hint"),
		epc_boc_adv_tile('Open', '2', 'amber'),
	);
	$chips = array(
		epc_boc_adv_rag_chip('green'),
		epc_boc_adv_rag_chip('amber'),
		epc_boc_adv_rag_chip('red'),
		epc_boc_adv_rag_chip('other'),
		epc_boc_adv_yn(true),
		epc_boc_adv_yn(false),
	);
	$classify = array(
		epc_boc_classify_tenant(array('is_demo' => 1, 'industry_code' => 'erp_only')),
		epc_boc_classify_tenant(array('is_demo' => '0', 'industry_code' => 'ERP_STANDALONE')),
		epc_boc_classify_tenant(array('industry' => 'erp_shop')),
		epc_boc_classify_tenant(array('industry_code' => 'retail')),
		epc_boc_classify_tenant(array()),
	);
	$labels = array(
		epc_boc_type_label('demo'),
		epc_boc_type_label('erp_only'),
		epc_boc_type_label('commerce'),
		epc_boc_type_label('other'),
		epc_boc_h("Alpha & Co's <x>"),
	);
	ob_start();
	epc_boc_adv_hero('MULTI-VENDOR', 'fa-truck', 'Vendor & Sourcing Control', "Every supplier <x>");
	$hero = ob_get_clean();
	return surf_freeze(array($money, $vendors, $warehouses, $channels, $tiles, $chips, $classify, $labels, $hero));
}

function surf_seed_full(PDO $pdo)
{
	$pdo->exec('CREATE TABLE `epc_erp_suppliers` (`id` int NOT NULL, `active` tinyint NOT NULL, PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('INSERT INTO `epc_erp_suppliers` (`id`,`active`) VALUES (1,1),(2,1),(3,0)');
	$pdo->exec('CREATE TABLE `epc_scm_rfq` (`id` int NOT NULL, `status` varchar(32) NOT NULL, PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec("INSERT INTO `epc_scm_rfq` (`id`,`status`) VALUES (1,'draft'),(2,'sent'),(3,'closed'),(4,'open')");
	$pdo->exec('CREATE TABLE `epc_erp_purchases` (`id` int NOT NULL, `total_amount` decimal(12,2) NOT NULL, PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('INSERT INTO `epc_erp_purchases` (`id`,`total_amount`) VALUES (1,100.50),(2,50.00)');
	$pdo->exec('CREATE TABLE `epc_erp_inv_warehouses` (`id` int NOT NULL, `active` tinyint NOT NULL, PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('INSERT INTO `epc_erp_inv_warehouses` (`id`,`active`) VALUES (1,1),(2,0)');
	$pdo->exec('CREATE TABLE `epc_erp_inv_items` (`id` int NOT NULL, `active` tinyint NOT NULL, PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('INSERT INTO `epc_erp_inv_items` (`id`,`active`) VALUES (1,1),(2,1),(3,0)');
	$pdo->exec('CREATE TABLE `epc_erp_inv_stock` (`item_id` int NOT NULL, `qty_on_hand` decimal(12,3) NOT NULL, `avg_unit_cost` decimal(12,4) NOT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('INSERT INTO `epc_erp_inv_stock` (`item_id`,`qty_on_hand`,`avg_unit_cost`) VALUES (1,10,2),(2,0,5),(4,-1,3)');
	$pdo->exec('CREATE TABLE `epc_scm_item_planning` (`item_id` int NOT NULL, `reorder_point` decimal(12,3) NOT NULL, PRIMARY KEY (`item_id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('INSERT INTO `epc_scm_item_planning` (`item_id`,`reorder_point`) VALUES (1,15),(2,5)');
	$pdo->exec('CREATE TABLE `epc_pos_registers` (`id` int NOT NULL, PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('INSERT INTO `epc_pos_registers` (`id`) VALUES (1)');
	$pdo->exec('CREATE TABLE `epc_pos_sales` (`id` int NOT NULL, PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('CREATE TABLE `epc_api_clients` (`site_key` varchar(64) NOT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec("INSERT INTO `epc_api_clients` (`site_key`) VALUES ('alpha')");
}

function surf_run_collect(PDO $pdo)
{
	surf_seed_full($pdo);
	if (!function_exists('epc_apai_marketplace_channels_for_tenant')) {
		function epc_apai_marketplace_channels_for_tenant($db, $siteKey)
		{
			if ($siteKey === 'throw') {
				throw new RuntimeException('mkt');
			}
			if ($siteKey === 'alpha') {
				return array('sell' => array('noon', 'amazon'));
			}
			if ($siteKey === 'empty-sell') {
				return array('sell' => 'nope');
			}
			return array();
		}
	}
	if (!function_exists('epc_apai_marketplace_arbitrage_enabled')) {
		function epc_apai_marketplace_arbitrage_enabled($db, $siteKey)
		{
			if ($siteKey === 'throw-arb') {
				throw new RuntimeException('arb');
			}
			return $siteKey === 'alpha';
		}
	}
	$vendor = epc_boc_collect_vendor($pdo);
	$wh = epc_boc_collect_warehouse($pdo);
	$chAlpha = epc_boc_collect_channel($pdo, $pdo, 'alpha', 'commerce');
	$chDemo = epc_boc_collect_channel($pdo, $pdo, 'beta', 'demo');
	$chErp = epc_boc_collect_channel($pdo, $pdo, 'gamma', 'erp_only');
	$chThrow = epc_boc_collect_channel($pdo, $pdo, 'throw', 'commerce');
	$chEmptySell = epc_boc_collect_channel($pdo, $pdo, 'empty-sell', 'commerce');
	$exists = epc_boc_adv_table_exists($pdo, 'epc_erp_suppliers');
	$missing = epc_boc_adv_table_exists($pdo, 'no_such_table');
	$scalarVendors = epc_boc_adv_scalar($pdo, 'SELECT COUNT(*) FROM `epc_erp_suppliers`');
	$emptyName = 'ecomae_cpw_' . substr(md5(uniqid('empty', true)), 0, 12);
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN'), array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$admin->exec('CREATE DATABASE `' . $emptyName . '`');
	$bare = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $emptyName . ';charset=utf8', 'ecomae', getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN'), array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$bare->exec('CREATE TABLE `epc_pos_sales` (`id` int NOT NULL, PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$vendorBare = epc_boc_collect_vendor($bare);
	$whBare = epc_boc_collect_warehouse($bare);
	$chSalesOnly = epc_boc_collect_channel(null, $bare, 'z', 'commerce');
	$admin->exec('DROP DATABASE IF EXISTS `' . $emptyName . '`');
	return surf_freeze(array(
		$vendor, $wh, $chAlpha, $chDemo, $chErp, $chThrow, $chEmptySell,
		$exists, $missing, $scalarVendors,
		$vendorBare, $whBare, $chSalesOnly,
	));
}

function surf_run_fleet()
{
	if (!function_exists('epc_portal_tenant_control_list_all')) {
		function epc_portal_tenant_control_list_all($db)
		{
			$mode = $GLOBALS['SURF_LIST_MODE'] ?? 'ok';
			if ($mode === 'throw') {
				throw new RuntimeException('list');
			}
			if ($mode === 'empty') {
				return array();
			}
			return array(
				array('site_key' => 'alpha', 'trade_name' => 'Alpha Live', 'industry_code' => 'retail'),
				array('site_key' => 'beta', 'system_name' => 'Beta Sys', 'is_demo' => 1),
				array('site_key' => 'gone', 'trade_name' => 'Gone'),
				array('site_key' => 'boom', 'trade_name' => 'Boom', 'industry' => 'erp_only'),
			);
		}
	}
	if (!function_exists('epc_portal_tenant_control_tenant_pdo_connect')) {
		function epc_portal_tenant_control_tenant_pdo_connect($t)
		{
			$key = (string) ($t['site_key'] ?? '');
			if ($key === 'gone') {
				return array('pdo' => null);
			}
			if ($key === 'boom') {
				throw new RuntimeException('connect');
			}
			return array('pdo' => $GLOBALS['db_link']);
		}
	}
	$GLOBALS['SURF_LIST_MODE'] = 'ok';
	$per = epc_boc_adv_fleet_metrics($GLOBALS['db_link'], static function (PDO $pdo) {
		return epc_boc_collect_vendor($pdo);
	});
	$GLOBALS['SURF_LIST_MODE'] = 'empty';
	$empty = epc_boc_adv_fleet_metrics($GLOBALS['db_link'], static function (PDO $pdo) {
		return epc_boc_collect_vendor($pdo);
	});
	$GLOBALS['SURF_LIST_MODE'] = 'throw';
	$threw = epc_boc_adv_fleet_metrics($GLOBALS['db_link'], static function (PDO $pdo) {
		return epc_boc_collect_vendor($pdo);
	});
	return surf_freeze(array($per, $empty, $threw));
}

function surf_run_render()
{
	$vendor = epc_boc_vendor_rollup(array(
		array('site_key' => 'a', 'label' => 'Alpha & Co', 'type' => 'commerce', 'ok' => 1, 'vendors' => 12, 'active_vendors' => 9, 'rfq_open' => 3, 'spend' => 1250000, 'currency' => 'AED'),
		array('site_key' => 'b', 'label' => "Beta's", 'type' => 'demo', 'ok' => 0, 'vendors' => 2, 'active_vendors' => 1, 'rfq_open' => 0, 'spend' => 400, 'currency' => 'USD', 'note' => 'DB unreachable'),
	));
	$warehouse = epc_boc_warehouse_rollup(array(
		array('site_key' => 'a', 'label' => 'Alpha', 'ok' => 1, 'warehouses' => 2, 'skus' => 40, 'stock_value' => 8800, 'low_stock' => 1, 'out_of_stock' => 0, 'currency' => 'AED'),
		array('site_key' => 'c', 'label' => 'Gamma', 'ok' => 1, 'warehouses' => 1, 'skus' => 8, 'stock_value' => 500, 'low_stock' => 0, 'out_of_stock' => 2, 'currency' => 'AED', 'note' => ''),
	));
	$channel = epc_boc_channel_rollup(array(
		array('site_key' => 'a', 'label' => 'Alpha', 'ok' => 1, 'web' => 1, 'pos' => 1, 'api' => 1, 'marketplaces' => 2, 'arbitrage' => 1),
		array('site_key' => 'b', 'label' => 'Beta', 'type' => 'erp_only', 'ok' => 1, 'web' => 0, 'pos' => 0, 'api' => 0, 'marketplaces' => 0, 'arbitrage' => 0),
	));
	$emptyVendor = epc_boc_vendor_rollup(array());
	ob_start();
	epc_boc_render_vendor_control(null, '/boc', $vendor);
	$vHtml = ob_get_clean();
	ob_start();
	epc_boc_render_warehouse_control(null, '/boc', $warehouse);
	$wHtml = ob_get_clean();
	ob_start();
	epc_boc_render_channel_control(null, '/boc', $channel);
	$cHtml = ob_get_clean();
	ob_start();
	epc_boc_render_vendor_control(null, '/boc', $emptyVendor);
	$emptyHtml = ob_get_clean();
	return surf_freeze(array($vHtml, $wHtml, $cHtml, $emptyHtml));
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
	require $root . '/content/general_pages/epc_boc_advanced.php';
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1s_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1s_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
