<?php
/**
 * Apply one OMS · Orders sidebar entry + register OMS daily guide route.
 *
 * Targets: ecomae platform CP (DB ecomae) + all industry storefronts (docpart).
 * Roles: epartscart = spare parts; ecomae = overall control — see epc_cp_common_parity.php
 *
 * https://www.epartscart.com/epc-oms-menu-guide-setup.php?token=…&apply=1
 * Optional: &site_key=ecomae|epartscart|…
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(180);

$apply = !empty($_GET['apply']);
$onlySite = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? ''))));

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/general_pages/epc_cp_common_parity.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';
require_once __DIR__ . '/epc_oms_menu_guide_lib.php';

if (is_file(__DIR__ . '/content/general_pages/epc_perf_cache.php')) {
	require_once __DIR__ . '/content/general_pages/epc_perf_cache.php';
}

$tenants = epc_cp_common_parity_host_map();
$targetsMeta = epc_cp_common_parity_targets();
$report = array(
	'ok' => true,
	'apply' => $apply,
	'roles' => array(
		'epartscart' => 'spare_parts (auto_parts) — epartscart.com',
		'ecomae' => 'platform_control — ecomae.com/cp overall',
	),
	'tenants' => array(),
);

foreach ($tenants as $siteKey => $host) {
	if ($onlySite !== '' && $onlySite !== $siteKey) {
		continue;
	}
	$meta = $targetsMeta[$siteKey] ?? array();
	$row = array(
		'site_key' => $siteKey,
		'host' => $host,
		'role' => (string) ($meta['role'] ?? ''),
		'industry' => (string) ($meta['industry'] ?? ''),
		'ok' => false,
	);
	try {
		$_SERVER['HTTP_HOST'] = $host;
		$_SERVER['SERVER_NAME'] = $host;
		unset($GLOBALS['epc_portal_config_applied'], $GLOBALS['epc_db_link']);

		$cfg = new DP_Config();
		epc_portal_apply_config($cfg);
		$pdo = epc_oms_setup_pdo($cfg);
		$backend = trim((string) ($cfg->backend_dir ?? 'cp'), '/');
		if ($backend === '') {
			$backend = 'cp';
		}

		$menu = array('preview' => true);
		$guide = array('preview' => true);
		if ($apply) {
			$menu = epc_cp_shop_orders_menu_apply($pdo);
			$guide = epc_oms_register_guide_route($pdo, $backend);
			if (function_exists('epc_perf_cache_bust_prefix')) {
				epc_perf_cache_bust_prefix('epc_cp_menu_rows');
			}
		} else {
			$st = $pdo->query(
				"SELECT `id`, `caption`, `url` FROM `control_items`
				 WHERE `url` LIKE '%/shop/orders/%'
				 ORDER BY `items_group`, `order`, `id`"
			);
			$menu['current_items'] = $st->fetchAll(PDO::FETCH_ASSOC);
			$gst = $pdo->prepare('SELECT `id`, `url`, `content` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
			$gst->execute(array('shop/orders/oms-guide'));
			$guide['existing'] = $gst->fetch(PDO::FETCH_ASSOC) ?: null;
		}

		$row['ok'] = true;
		$row['db'] = $cfg->db;
		$row['menu'] = $menu;
		$row['guide'] = $guide;
	} catch (Throwable $e) {
		$row['error'] = $e->getMessage();
		$report['ok'] = false;
	}
	$report['tenants'][] = $row;
}

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
