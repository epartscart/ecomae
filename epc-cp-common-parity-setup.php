<?php
/**
 * Map + apply common CP packs across ecomae platform and industry tenants.
 *
 * Dry-run:  …/epc-cp-common-parity-setup.php?token=…
 * Apply:    …/epc-cp-common-parity-setup.php?token=…&apply=1
 * Filter:   &site_key=ecomae|&pack=oms_orders_menu
 *
 * Bidirectional: missing on platform → build/apply there; missing on tenant → apply there.
 * epartscart stays spare-parts; ecomae stays overall control.
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(240);

$apply = !empty($_GET['apply']);
$onlySite = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? ''))));
$onlyPack = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['pack'] ?? ''))));

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

function epc_parity_probe_oms(PDO $pdo): array
{
	$st = $pdo->query(
		"SELECT `id`, `caption`, `url` FROM `control_items`
		 WHERE `url` LIKE '%/shop/orders/%'
		 ORDER BY `items_group`, `order`, `id`"
	);
	$items = $st->fetchAll(PDO::FETCH_ASSOC);
	$hasStatuses = false;
	$hasItems = false;
	$hasOmsCaption = false;
	foreach ($items as $it) {
		$url = (string) ($it['url'] ?? '');
		$cap = (string) ($it['caption'] ?? '');
		if (preg_match('#/shop/orders/statuses/?$#', $url)) {
			$hasStatuses = true;
		}
		if (preg_match('#/shop/orders/items/?$#', $url)) {
			$hasItems = true;
		}
		if (in_array($cap, array('epc_oms_orders_cp', 'epc_shop_orders_cp'), true)) {
			$hasOmsCaption = true;
		}
	}
	$gst = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$gst->execute(array('shop/orders/oms-guide'));
	$guideId = (int) $gst->fetchColumn();

	return array(
		'order_menu_items' => $items,
		'has_statuses_menu' => $hasStatuses,
		'has_items_menu' => $hasItems,
		'has_oms_caption' => $hasOmsCaption,
		'guide_content_id' => $guideId,
		'oms_menu_ok' => !$hasStatuses && !$hasItems && $guideId > 0,
	);
}

$docroot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? __DIR__), '/');
$fileChecks = array(
	'oms_guide' => $docroot . '/cp/content/shop/order_process/oms_daily_guide.php',
	'oms_guide_page' => $docroot . '/cp/content/shop/order_process/oms_daily_guide_page.php',
	'menu_lib' => $docroot . '/epc_cp_mainstream_menu.php',
	'parity_lib' => $docroot . '/content/general_pages/epc_cp_common_parity.php',
	'mv_config' => $docroot . '/cp/content/shop/prices_upload/epc_multivendor_cp_config.php',
	'mv_js' => $docroot . '/cp/content/shop/prices_upload/epc_multivendor_cp.js',
);

$files = array();
foreach ($fileChecks as $k => $path) {
	$files[$k] = array(
		'exists' => is_file($path),
		'bytes' => is_file($path) ? (int) filesize($path) : 0,
	);
}

$report = array(
	'ok' => true,
	'apply' => $apply,
	'roles' => array(
		'epartscart.com' => 'spare_parts / auto_parts only',
		'ecomae.com/cp' => 'platform_control — overall everything',
	),
	'packs' => epc_cp_common_parity_packs(),
	'files' => $files,
	'targets' => array(),
);

foreach (epc_cp_common_parity_targets() as $siteKey => $meta) {
	if ($onlySite !== '' && $onlySite !== $siteKey) {
		continue;
	}
	$host = (string) $meta['host'];
	$row = array(
		'site_key' => $siteKey,
		'host' => $host,
		'role' => $meta['role'],
		'industry' => $meta['industry'],
		'label' => $meta['label'],
		'ok' => false,
		'packs' => array(),
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
		$row['db'] = $cfg->db;

		$probe = epc_parity_probe_oms($pdo);
		$row['before'] = $probe;

		$packKeys = array('oms_orders_menu', 'oms_daily_guide', 'multivendor_upload', 'vehicle_catalog', 'platform_governance');
		foreach ($packKeys as $packKey) {
			if ($onlyPack !== '' && $onlyPack !== $packKey) {
				continue;
			}
			$applies = epc_cp_common_parity_pack_applies($packKey, $meta);
			$packRow = array('pack' => $packKey, 'applies' => $applies, 'action' => 'skip');
			if (!$applies) {
				$packRow['action'] = 'out_of_scope';
				$row['packs'][] = $packRow;
				continue;
			}

			if ($packKey === 'oms_orders_menu') {
				$needsMenu = !empty($probe['has_statuses_menu']) || !empty($probe['has_items_menu']) || empty($probe['has_oms_caption']);
				$packRow['needed'] = $needsMenu;
				$packRow['action'] = $needsMenu ? ($apply ? 'apply' : 'would_apply') : 'ok';
				if ($apply) {
					$packRow['result'] = epc_cp_shop_orders_menu_apply($pdo);
					if (function_exists('epc_perf_cache_bust_prefix')) {
						epc_perf_cache_bust_prefix('epc_cp_menu_rows');
					}
					if (!$needsMenu) {
						$packRow['action'] = 'refresh';
					}
				}
			} elseif ($packKey === 'oms_daily_guide') {
				$needsGuide = (int) ($probe['guide_content_id'] ?? 0) <= 0;
				$packRow['needed'] = $needsGuide;
				$packRow['action'] = $needsGuide ? ($apply ? 'apply' : 'would_apply') : 'ok';
				if ($apply) {
					$packRow['result'] = epc_oms_register_guide_route($pdo, $backend);
					if (!$needsGuide) {
						$packRow['action'] = 'refresh';
					}
				}
			} elseif ($packKey === 'multivendor_upload') {
				$ok = !empty($files['mv_config']['exists']) && !empty($files['mv_js']['exists']);
				$packRow['needed'] = !$ok;
				$packRow['action'] = $ok ? 'ok' : 'missing_files';
			} elseif ($packKey === 'vehicle_catalog') {
				$packRow['action'] = 'industry_scoped_ok';
				$packRow['note'] = 'Spare-parts catalog stays on epartscart / auto_parts only';
			} elseif ($packKey === 'platform_governance') {
				$packRow['action'] = 'platform_scoped_ok';
				$packRow['note'] = 'Tenant control / governance remains on ecomae platform CP';
			}

			$row['packs'][] = $packRow;
		}

		$row['after'] = $apply ? epc_parity_probe_oms($pdo) : $probe;
		$row['ok'] = true;
	} catch (Throwable $e) {
		$row['error'] = $e->getMessage();
		$report['ok'] = false;
	}
	$report['targets'][] = $row;
}

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
