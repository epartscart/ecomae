<?php
/**
 * Orders items — runtime config JS (footer load, outside .row pane).
 */
define('_ASTEXE_', 1);
header('Content-Type: application/javascript; charset=utf-8');
header('Cache-Control: no-store, no-cache, must-revalidate');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
$user_session = DP_User::getAdminSession();
if (empty($user_session) || !is_array($user_session)) {
	echo 'window.EPC_OI={};';
	exit;
}

$backend = trim((string) $DP_Config->backend_dir, '/');
if ($backend === '') {
	$backend = 'cp';
}

$lang = 'en';
if (!empty($GLOBALS['multilang_params']['lang'])) {
	$lang = (string) $GLOBALS['multilang_params']['lang'];
}

$manager_id = (int) DP_User::getAdminId();
$inProcess = array();
try {
	$pdo = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$st = $pdo->query(
		'SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `count_flag` != 0 AND `for_finish` != 1 AND `for_created` != 1'
	);
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$inProcess[] = (string) $row['id'];
	}
} catch (Throwable $e) {
}

$sort_field = 'id';
$sort_asc_desc = 'desc';
if (!empty($_COOKIE['orders_items_sort'])) {
	$decoded = json_decode((string) $_COOKIE['orders_items_sort'], true);
	if (is_array($decoded)) {
		if (!empty($decoded['field'])) {
			$sort_field = (string) $decoded['field'];
		}
		if (!empty($decoded['asc_desc'])) {
			$sort_asc_desc = strtolower((string) $decoded['asc_desc']) === 'asc' ? 'asc' : 'desc';
		}
	}
}

$time_from = '';
$time_to = '';
if (!empty($_COOKIE['orders_items_filter'])) {
	$filter = json_decode((string) $_COOKIE['orders_items_filter'], true);
	if (is_array($filter)) {
		$time_from = (string) ($filter['time_from'] ?? '');
		$time_to = (string) ($filter['time_to'] ?? '');
	}
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_translate.php';

$config = array(
	'backend' => $backend,
	'lang' => $lang,
	'csrf' => (string) ($user_session['csrf_guard_key'] ?? ''),
	'managerId' => $manager_id,
	'sortField' => $sort_field,
	'sortDir' => $sort_asc_desc,
	'timeFrom' => $time_from,
	'timeTo' => $time_to,
	'inProcessStatuses' => $inProcess,
	'urls' => array(
		'items' => '/' . $backend . '/shop/orders/items',
		'setViewed' => '/' . $backend . '/content/shop/order_process/ajax_set_orders_viewed.php',
		'userModal' => '/' . $backend . '/content/users/statistics/frontAjax/ajax_loadUserModal.php',
		'setItemStatus' => '/content/shop/protocol/set_order_item_status.php',
	),
	'msg' => array(
		'selectItemsViewed' => translate_str_by_id(3619),
		'setViewedFail' => translate_str_by_id(3599),
		'selectItemsStatus' => translate_str_by_id(3559),
		'setStatusFail' => translate_str_by_id(3560),
		'statusOk' => translate_str_by_id(3755),
		'userModalFail' => translate_str_by_id(3541),
		'selectPlaceholder' => translate_str_by_id(2094),
	),
);

echo 'window.EPC_OI=' . json_encode($config, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . ';';
