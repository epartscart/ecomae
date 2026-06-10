<?php
/**
 * CP orders visibility probe — tenant PDO, shop_orders count, office filter simulation.
 * GET ?token=…&site_key=epartscart  (omit site_key for all Model C tenants)
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(120);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

$onlySite = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? ''))));
if ($onlySite === 'all') {
	$onlySite = '';
}
$doRender = !empty($_GET['render']);

$tenants = array(
	'epartscart' => 'www.epartscart.com',
	'electronicae' => 'www.electronicae.com',
	'taxofinca' => 'www.taxofinca.com',
	'stylenlook' => 'www.stylenlook.com',
	'thejewellerytrend' => 'www.thejewellerytrend.com',
);

function epc_cp_orders_probe_tenant(string $siteKey, string $host): array
{
	$_SERVER['HTTP_HOST'] = $host;
	$_SERVER['SERVER_NAME'] = $host;
	unset($GLOBALS['epc_portal_config_applied'], $GLOBALS['epc_db_link']);

	$cfg = new DP_Config();
	epc_portal_apply_config($cfg);

	$site = epc_portal_site_profile();
	$out = array(
		'site_key' => $siteKey,
		'host' => $host,
		'docroot' => $_SERVER['DOCUMENT_ROOT'] ?? '',
		'cp_db' => (string) ($cfg->db ?? ''),
		'cp_user' => (string) ($cfg->user ?? ''),
		'profile_db' => (string) ($site['db'] ?? ''),
		'db_ok' => false,
		'shop_orders_total' => 0,
		'shop_offices_total' => 0,
		'orders_visible_admin' => 0,
		'orders_visible_no_offices' => false,
		'registry_db' => null,
		'error' => null,
	);

	try {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$out['db_ok'] = true;
		$out['shop_orders_total'] = (int) $pdo->query('SELECT COUNT(*) FROM `shop_orders`')->fetchColumn();
		$out['shop_offices_total'] = (int) $pdo->query('SELECT COUNT(*) FROM `shop_offices`')->fetchColumn();

		$officeIds = array();
		$oq = $pdo->query('SELECT `id` FROM `shop_offices` ORDER BY `id` ASC');
		while ($row = $oq->fetch(PDO::FETCH_ASSOC)) {
			$officeIds[] = (int) $row['id'];
		}
		if (count($officeIds) > 0) {
			$ph = implode(',', array_fill(0, count($officeIds), '?'));
			$st = $pdo->prepare("SELECT COUNT(*) FROM `shop_orders` WHERE `office_id` IN ($ph)");
			$st->execute($officeIds);
			$out['orders_visible_admin'] = (int) $st->fetchColumn();
		}
		$out['orders_visible_no_offices'] = count($officeIds) === 0;

		$backendIds = array();
		$bq = $pdo->query(
			'SELECT DISTINCT u.`user_id`, u.`email`
			 FROM `users` u
			 INNER JOIN `users_groups_bind` b ON b.`user_id` = u.`user_id`
			 INNER JOIN `groups` g ON g.`id` = b.`group_id` AND g.`for_backend` = 1
			 ORDER BY u.`user_id` ASC'
		);
		while ($bu = $bq->fetch(PDO::FETCH_ASSOC)) {
			$backendIds[] = (int) $bu['user_id'];
		}
		$out['backend_users'] = count($backendIds);

		$officesDetail = array();
		$oq2 = $pdo->query('SELECT `id`, `caption`, `users` FROM `shop_offices` ORDER BY `id` ASC');
		while ($or = $oq2->fetch(PDO::FETCH_ASSOC)) {
			$assigned = json_decode((string) ($or['users'] ?? ''), true);
			if (!is_array($assigned)) {
				$assigned = array();
			}
			$officesDetail[] = array(
				'id' => (int) $or['id'],
				'caption' => (string) ($or['caption'] ?? ''),
				'manager_count' => count($assigned),
			);
		}
		$out['offices'] = $officesDetail;

		$menuSt = $pdo->prepare(
			'SELECT COUNT(*) FROM `control_items` WHERE `url` LIKE ? OR `url` = ?'
		);
		$menuSt->execute(array('%/shop/orders/orders%', '/<backend>/shop/orders/orders'));
		$out['menu_orders_items'] = (int) $menuSt->fetchColumn();

		$shopGroupId = 0;
		$shopGrpSt = $pdo->query("SELECT `id` FROM `control_groups` WHERE `caption` = '744' LIMIT 1");
		if ($shopGrpSt) {
			$shopGroupId = (int) $shopGrpSt->fetchColumn();
		}
		if ($shopGroupId <= 0) {
			$shopGrpSt = $pdo->query(
				'SELECT g.`id` FROM `control_groups` g
				 INNER JOIN `control_items` i ON i.`items_group` = g.`id`
				 WHERE i.`url` LIKE \'%<backend>/shop/%\'
				 GROUP BY g.`id` ORDER BY COUNT(i.`id`) DESC LIMIT 1'
			);
			$shopGroupId = (int) $shopGrpSt->fetchColumn();
		}
		$out['shop_group_id'] = $shopGroupId;
		$out['menu_shop_orders'] = false;
		if ($shopGroupId > 0) {
			$shopMenuSt = $pdo->prepare(
				'SELECT `id`, `caption`, `order` FROM `control_items` WHERE `items_group` = ? AND `url` = ? LIMIT 1'
			);
			$shopMenuSt->execute(array($shopGroupId, '/<backend>/shop/orders/orders'));
			$shopMenuRow = $shopMenuSt->fetch(PDO::FETCH_ASSOC);
			$out['menu_shop_orders'] = is_array($shopMenuRow);
			$out['shop_orders_menu_item'] = $shopMenuRow ?: null;
		}

		$contentSt = $pdo->prepare(
			'SELECT `id`, `published_flag` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1'
		);
		$contentSt->execute(array('shop/orders/orders'));
		$contentRow = $contentSt->fetch(PDO::FETCH_ASSOC);
		$out['content_orders'] = $contentRow ? array(
			'id' => (int) $contentRow['id'],
			'published' => (int) $contentRow['published_flag'],
		) : null;
	} catch (Throwable $e) {
		$out['error'] = $e->getMessage();
	}

	$platformPdo = epc_portal_platform_pdo();
	if ($platformPdo instanceof PDO) {
		require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
		epc_portal_db_ensure($platformPdo);
		$st = $platformPdo->prepare('SELECT `db_name` FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1');
		$st->execute(array($siteKey));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if ($row) {
			$out['registry_db'] = (string) ($row['db_name'] ?? '');
		}
	}

	$out['cp_orders_url'] = 'https://' . $host . '/cp/shop/orders/orders';
	$out['cp_menu_paths'] = array(
		'shop' => 'Shop → Orders',
		'logistics' => 'Logistics → Customer orders',
	);
	$out['status'] = (
		$out['db_ok']
		&& $out['cp_db'] === 'docpart'
		&& !$out['orders_visible_no_offices']
		&& $out['shop_orders_total'] > 0
		&& !empty($out['content_orders'])
		&& (int) ($out['content_orders']['published'] ?? 0) === 1
		&& (int) ($out['menu_orders_items'] ?? 0) > 0
		&& !empty($out['menu_shop_orders'])
	) ? 'ok' : 'check';

	return $out;
}

function epc_cp_orders_render_smoke(string $host): array
{
	$_SERVER['HTTP_HOST'] = $host;
	$_SERVER['SERVER_NAME'] = $host;
	unset($GLOBALS['epc_portal_config_applied'], $GLOBALS['epc_db_link']);

	$cfg = new DP_Config();
	epc_portal_apply_config($cfg);
	$backend = (string) $cfg->backend_dir;
	$out = array(
		'host' => $host,
		'files' => array(),
		'render' => array('skipped' => false),
	);

	$required = array(
		'orders' => $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/shop/order_process/orders.php',
		'order_card' => $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/shop/order_process/order_card.php',
		'helpers' => $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/shop/order_process/epc_orders_workspace_helpers.php',
		'detail_pane' => $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/shop/order_process/epc_orders_detail_pane.php',
	);
	foreach ($required as $key => $path) {
		$out['files'][$key] = array(
			'exists' => is_file($path),
			'bytes' => is_file($path) ? (int) filesize($path) : 0,
		);
	}

	try {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Throwable $e) {
		$out['render'] = array('include_ok' => false, 'error' => 'DB: ' . $e->getMessage());
		return $out;
	}

	if (!function_exists('translate_str_by_id')) {
		function translate_str_by_id($id) { return (string) $id; }
	}
	if (!function_exists('translate_str_by_key')) {
		function translate_str_by_key($key) { return (string) $key; }
	}

	$GLOBALS['DP_Config'] = $cfg;
	$GLOBALS['DP_Template'] = (object) array('name' => 'bootstrap_admin');
	$GLOBALS['db_link'] = $pdo;
	$db_link = $pdo;
	$DP_Config = $cfg;

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = array('csrf_guard_key' => 'verify');
	$multilang_params = array('lang' => 'en');

	@ini_set('memory_limit', '512M');
	@set_time_limit(120);

	$cardSrc = is_file($required['order_card']) ? (string) file_get_contents($required['order_card']) : '';
	$out['order_detail_static'] = array(
		'actions_alert_eval_safe' => strpos($cardSrc, "require_once(\$_SERVER['DOCUMENT_ROOT'] . '/' . \$DP_Config->backend_dir . '/content/control/actions_alert.php')") !== false,
		'no_relative_actions_alert' => strpos($cardSrc, 'require_once("content/control/actions_alert.php")') === false,
	);

	$sampleOrderId = 0;
	try {
		$st16 = $pdo->prepare('SELECT `id` FROM `shop_orders` WHERE `id` = ? LIMIT 1');
		$st16->execute(array(16));
		$sampleOrderId = (int) $st16->fetchColumn();
		if ($sampleOrderId <= 0) {
			$sampleOrderId = (int) $pdo->query('SELECT `id` FROM `shop_orders` ORDER BY `id` DESC LIMIT 1')->fetchColumn();
		}
	} catch (Throwable $e) {
		$sampleOrderId = 0;
	}
	$out['order_detail'] = array(
		'sample_order_id' => $sampleOrderId,
		'skipped' => $sampleOrderId <= 0,
	);

	if ($sampleOrderId > 0 && is_file($required['order_card'])) {
		$GLOBALS['epc_orders_smoke_all_offices'] = true;
		$_GET['order_id'] = $sampleOrderId;
		$ordersBackground = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/shop/order_process/orders_background.php';
		if (is_file($ordersBackground)) {
			include $ordersBackground;
		}
		ob_start();
		try {
			include $required['order_card'];
			$orderHtml = (string) ob_get_clean();
			$out['order_detail'] = array_merge($out['order_detail'], array(
				'include_ok' => true,
				'html_length' => strlen($orderHtml),
				'has_order_items_table' => stripos($orderHtml, 'id="order_items_table"') !== false,
				'has_oc_boot' => stripos($orderHtml, 'id="epc-oc-boot"') !== false,
				'has_customer_panel' => stripos($orderHtml, 'panel-heading') !== false,
				'has_payment_panel' => stripos($orderHtml, 'contact-footer') !== false,
				'has_apai_badge_fn' => is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_fulfillment.php'),
				'has_inline_for_js' => stripos($orderHtml, 'orders_items_ids_to_orders_items_objects[') !== false,
				'eval_path_bug' => stripos($orderHtml, 'failed to open stream') !== false
					&& stripos($orderHtml, 'actions_alert.php') !== false,
				'order_not_found_msg' => stripos($orderHtml, 'Order not found or access denied') !== false,
				'order_not_specified_msg' => stripos($orderHtml, 'Order not specified') !== false,
				'php_fatal_in_html' => (bool) preg_match('/\b(Fatal error|Parse error|Uncaught)\b/i', $orderHtml),
			));
			$out['order_detail']['ok'] = $out['order_detail']['include_ok']
				&& $out['order_detail']['has_order_items_table']
				&& $out['order_detail']['has_oc_boot']
				&& !$out['order_detail']['order_not_found_msg']
				&& !$out['order_detail']['order_not_specified_msg']
				&& !$out['order_detail']['php_fatal_in_html']
				&& !$out['order_detail']['has_inline_for_js'];
			$out['order_detail']['cp_order_url'] = 'https://' . $host . '/' . $backend
				. '/shop/orders/order?order_id=' . $sampleOrderId;
		} catch (Throwable $e) {
			if (ob_get_level() > 0) {
				ob_end_clean();
			}
			$out['order_detail'] = array_merge($out['order_detail'], array(
				'include_ok' => false,
				'error' => $e->getMessage(),
				'error_line' => (int) $e->getLine(),
				'ok' => false,
			));
		}
		unset($_GET['order_id']);
		unset($GLOBALS['epc_orders_smoke_all_offices']);
	}

	ob_start();
	try {
		include $required['orders'];
		$html = (string) ob_get_clean();
		$out['render'] = array(
			'include_ok' => true,
			'html_length' => strlen($html),
			'has_orders_table' => stripos($html, 'id="orders_table"') !== false,
			'has_kpi' => stripos($html, 'epc-scp-orders-kpi') !== false,
			'has_workspace' => stripos($html, 'epc-scp-orders-workspace') !== false,
			'row_count_in_html' => substr_count(strtolower($html), 'epc-scp-orders-row'),
			'eval_dir_bug' => stripos($html, 'Helper file not found') !== false && stripos($html, '/core/') !== false,
		);
		$out['render']['ok'] = $out['render']['has_orders_table']
			&& $out['render']['has_workspace'];
	} catch (Throwable $e) {
		if (ob_get_level() > 0) {
			ob_end_clean();
		}
		$out['render'] = array(
			'include_ok' => false,
			'error' => $e->getMessage(),
			'ok' => false,
		);
	}

	return $out;
}

$results = array();
foreach ($tenants as $siteKey => $host) {
	if ($onlySite !== '' && $onlySite !== $siteKey) {
		continue;
	}
	$results[$siteKey] = epc_cp_orders_probe_tenant($siteKey, $host);
}

$renderSmoke = null;
if ($doRender) {
	$renderHost = $tenants['epartscart'] ?? 'www.epartscart.com';
	if ($onlySite !== '' && isset($tenants[$onlySite])) {
		$renderHost = $tenants[$onlySite];
	}
	$renderSmoke = epc_cp_orders_render_smoke($renderHost);
}

echo json_encode(
	array(
		'probe' => 'epc-cp-orders-verify',
		'ts' => gmdate('c'),
		'tenants' => $results,
		'render_smoke' => $renderSmoke,
	),
	JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES
);
