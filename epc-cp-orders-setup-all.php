<?php
/**
 * Ensure CP orders list is reachable on all Model C tenants:
 * - content routes (shop/orders/*)
 * - Logistics + Shop menu links to /cp/shop/orders/orders
 * - backend CP users assigned to shop_offices (office filter)
 *
 * GET ?token=… [&apply=1] [&site_key=epartscart]
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
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$tenants = array(
	'epartscart' => 'www.epartscart.com',
	'electronicae' => 'www.electronicae.com',
	'taxofinca' => 'www.taxofinca.com',
	'stylenlook' => 'www.stylenlook.com',
	'thejewellerytrend' => 'www.thejewellerytrend.com',
);

function epc_cpo_setup_pdo($cfg): PDO
{
	$host = trim((string) ($cfg->host ?? '127.0.0.1'));
	if ($host === 'localhost') {
		$host = '127.0.0.1';
	}
	$db = (string) ($cfg->db ?? '');
	$user = (string) ($cfg->user ?? '');
	$pass = (string) ($cfg->password ?? '');
	return new PDO(
		'mysql:host=' . $host . ';dbname=' . $db . ';charset=utf8',
		$user,
		$pass,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
}

function epc_cpo_backend_user_ids(PDO $pdo): array
{
	$ids = array();
	$st = $pdo->query(
		'SELECT DISTINCT u.`user_id`
		 FROM `users` u
		 INNER JOIN `users_groups_bind` b ON b.`user_id` = u.`user_id`
		 INNER JOIN `groups` g ON g.`id` = b.`group_id` AND g.`for_backend` = 1
		 ORDER BY u.`user_id` ASC'
	);
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$ids[] = (int) $row['user_id'];
	}
	return $ids;
}

function epc_cpo_office_users_merge(string $usersJson, array $backendIds): array
{
	$current = json_decode($usersJson, true);
	if (!is_array($current)) {
		$current = array();
	}
	$merged = array();
	foreach ($current as $id) {
		$id = (int) $id;
		if ($id > 0) {
			$merged[$id] = true;
		}
	}
	foreach ($backendIds as $id) {
		if ($id > 0) {
			$merged[$id] = true;
		}
	}
	$out = array_keys($merged);
	sort($out, SORT_NUMERIC);
	return $out;
}

function epc_cpo_sync_office_users(PDO $pdo, array $backendIds, bool $apply): array
{
	$report = array('offices' => array(), 'changed' => 0);
	$st = $pdo->query('SELECT `id`, `caption`, `users` FROM `shop_offices` ORDER BY `id` ASC');
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$officeId = (int) $row['id'];
		$before = (string) ($row['users'] ?? '');
		$afterIds = epc_cpo_office_users_merge($before, $backendIds);
		$after = json_encode($afterIds);
		$changed = $after !== $before;
		if ($apply && $changed) {
			$pdo->prepare('UPDATE `shop_offices` SET `users` = ? WHERE `id` = ?')->execute(array($after, $officeId));
			$report['changed']++;
		}
		$report['offices'][] = array(
			'id' => $officeId,
			'caption' => (string) ($row['caption'] ?? ''),
			'users_before' => $before,
			'users_after' => $after,
			'changed' => $changed,
		);
	}
	return $report;
}

function epc_cpo_menu_orders(PDO $pdo, string $backend): array
{
	$backendToken = '<backend>';
	$ordersUrl = '/' . $backendToken . '/shop/orders/orders';
	$st = $pdo->prepare(
		'SELECT `id`, `caption`, `url`, `items_group`, `show_anyway`
		 FROM `control_items`
		 WHERE `url` = ? OR `url` LIKE ?'
	);
	$st->execute(array($ordersUrl, '%/shop/orders/orders%'));
	$items = $st->fetchAll(PDO::FETCH_ASSOC);
	$logisticsGroup = epc_cp_mm_group_id($pdo, 'epc_cp_group_logistics');
	$shopGroup = epc_cp_mm_find_shop_group($pdo);
	$content = $pdo->prepare('SELECT `id`, `published_flag`, `content` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$content->execute(array('shop/orders/orders'));
	$contentRow = $content->fetch(PDO::FETCH_ASSOC) ?: null;
	return array(
		'logistics_group_id' => $logisticsGroup,
		'shop_group_id' => (int) ($shopGroup['id'] ?? 0),
		'menu_items' => $items,
		'content_orders' => $contentRow,
		'orders_url' => '/' . $backend . '/shop/orders/orders',
	);
}

function epc_cpo_setup_tenant(string $siteKey, string $host, bool $apply): array
{
	$_SERVER['HTTP_HOST'] = $host;
	$_SERVER['SERVER_NAME'] = $host;
	unset($GLOBALS['epc_portal_config_applied'], $GLOBALS['epc_db_link']);

	$cfg = new DP_Config();
	epc_portal_apply_config($cfg);
	$backend = trim((string) ($cfg->backend_dir ?? 'cp'), '/');
	if ($backend === '') {
		$backend = 'cp';
	}

	$out = array(
		'site_key' => $siteKey,
		'host' => $host,
		'cp_db' => (string) ($cfg->db ?? ''),
		'apply' => $apply,
		'ok' => false,
		'error' => null,
	);

	try {
		$pdo = epc_cpo_setup_pdo($cfg);
		$backendIds = epc_cpo_backend_user_ids($pdo);
		$offices = epc_cpo_sync_office_users($pdo, $backendIds, $apply);
		$menuBefore = epc_cpo_menu_orders($pdo, $backend);
		$menuApply = null;
		if ($apply) {
			$menuApply = epc_cp_mainstream_menu_apply($pdo);
			$menuApply['shop_orders'] = epc_cp_shop_orders_menu_apply($pdo);
		}
		$menuAfter = epc_cpo_menu_orders($pdo, $backend);
		$ordersTotal = (int) $pdo->query('SELECT COUNT(*) FROM `shop_orders`')->fetchColumn();

		$out['backend_users'] = count($backendIds);
		$out['shop_orders_total'] = $ordersTotal;
		$out['offices'] = $offices;
		$out['menu_before'] = $menuBefore;
		$out['menu_after'] = $menuAfter;
		$out['menu_apply'] = $menuApply;
		$out['cp_orders_url'] = 'https://' . $host . '/' . $backend . '/shop/orders/orders';
		$shopMenuItem = null;
		if (!empty($menuAfter['shop_group_id'])) {
			$shopSt = $pdo->prepare(
				'SELECT `id`, `caption`, `order` FROM `control_items` WHERE `items_group` = ? AND `url` = ? LIMIT 1'
			);
			$shopSt->execute(array((int) $menuAfter['shop_group_id'], '/<backend>/shop/orders/orders'));
			$shopMenuItem = $shopSt->fetch(PDO::FETCH_ASSOC) ?: null;
		}
		$out['shop_orders_menu_item'] = $shopMenuItem;
		$out['menu_shop_orders'] = $shopMenuItem !== null;
		$out['cp_menu_paths'] = array(
			'shop' => 'Shop → Orders',
			'logistics' => 'Logistics → Customer orders',
		);
		$out['ok'] = $ordersTotal > 0
			&& (string) $cfg->db === 'docpart'
			&& !empty($menuAfter['content_orders'])
			&& (int) ($menuAfter['content_orders']['published_flag'] ?? 0) === 1
			&& count($menuAfter['menu_items']) > 0
			&& !empty($out['menu_shop_orders']);
	} catch (Throwable $e) {
		$out['error'] = $e->getMessage();
	}

	return $out;
}

$results = array();
foreach ($tenants as $siteKey => $host) {
	if ($onlySite !== '' && $onlySite !== $siteKey) {
		continue;
	}
	$results[$siteKey] = epc_cpo_setup_tenant($siteKey, $host, $apply);
}

echo json_encode(
	array(
		'script' => 'epc-cp-orders-setup-all',
		'apply' => $apply,
		'ts' => gmdate('c'),
		'hint' => $apply
			? 'Applied. Hard-refresh CP, sign in, open /cp/shop/orders/orders (or Logistics → Customer orders).'
			: 'Dry run — add apply=1 to sync office managers and menu.',
		'tenants' => $results,
	),
	JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE
);
