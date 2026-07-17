<?php
/**
 * Orders list — runtime config JS (footer load, outside eval'd CP content pane).
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
	echo 'window.EPC_ORDERS={};';
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
$selected_order_id = isset($_GET['order_id']) ? (int) $_GET['order_id'] : 0;
$filter_status_id = isset($_GET['status_id']) ? (int) $_GET['status_id'] : 0;

$sort_field = 'id';
$sort_asc_desc = 'desc';
if (!empty($_COOKIE['orders_sort'])) {
	$decoded = json_decode((string) $_COOKIE['orders_sort'], true);
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
if (!empty($_COOKIE['orders_filter'])) {
	$filter = json_decode((string) $_COOKIE['orders_filter'], true);
	if (is_array($filter)) {
		$time_from = (string) ($filter['time_from'] ?? '');
		$time_to = (string) ($filter['time_to'] ?? '');
	}
}

$in_process = array();
$statuses_for_finish = array();
$statuses_for_inverse = array();
try {
	$pdo = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$st = $pdo->query(
		'SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_inverse` != 1 AND `for_finish` != 1 AND `for_created` != 1'
	);
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$in_process[] = (string) $row['id'];
	}
	$st = $pdo->query('SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_finish` = 1');
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$statuses_for_finish[] = (string) $row['id'];
	}
	$st = $pdo->query('SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_inverse` = 1');
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$statuses_for_inverse[] = (string) $row['id'];
	}
} catch (Throwable $e) {
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_translate.php';

$in_process_filter = $filter_status_id > 0
	? array((string) $filter_status_id)
	: $in_process;

$config = array(
	'backend' => $backend,
	'lang' => $lang,
	'csrf' => (string) ($user_session['csrf_guard_key'] ?? ''),
	'managerId' => $manager_id,
	'selectedOrderId' => $selected_order_id,
	'sortField' => $sort_field,
	'sortDir' => $sort_asc_desc,
	'timeFrom' => $time_from,
	'timeTo' => $time_to,
	'inProcessStatuses' => $in_process_filter,
	'autoRunInProcess' => $filter_status_id > 0,
	'statusesForFinish' => $statuses_for_finish,
	'statusesForInverse' => $statuses_for_inverse,
	'urls' => array(
		'orders' => '/' . $backend . '/shop/orders/orders',
		'orderFullBase' => '/' . $backend . '/shop/orders/order?order_id=',
		'ajaxDetail' => '/' . $backend . '/content/shop/order_process/ajax_epc_orders_detail_pane.php',
		'setViewed' => '/' . $backend . '/content/shop/order_process/ajax_set_orders_viewed.php',
		'deleteOrders' => '/' . $backend . '/content/shop/order_process/ajax_delete_orders.php',
		'userModal' => '/' . $backend . '/content/users/statistics/frontAjax/ajax_loadUserModal.php',
		'setOrderStatus' => '/content/shop/protocol/set_order_status.php',
		'addComment' => '/' . $backend . '/content/shop/order_process/ajax_add_comment_to_log.php',
		'erpAjax' => '/' . $backend . '/content/shop/finance/erp/ajax_erp_endpoint.php',
	),
	'msg' => array(
		'selectOrders' => translate_str_by_id(3597),
		'selectOrdersViewed' => translate_str_by_id(3598),
		'setViewedFail' => translate_str_by_id(3599),
		'deleteConfirm' => translate_str_by_id(3600),
		'setStatusFail' => translate_str_by_id(3508),
		'setStatusOk' => 'Status updated',
		'commentEmpty' => 'Enter a note first',
		'commentOk' => 'Note saved',
		'commentFail' => 'Could not save note',
		'finishConfirm' => translate_str_by_id(5297),
		'inverseConfirm' => translate_str_by_id(5299),
		'userModalFail' => translate_str_by_id(3541),
		'selectPlaceholder' => translate_str_by_id(2094),
		'selectAllText' => translate_str_by_id(2355),
		'allSelected' => translate_str_by_id(5660),
		'countSelected' => translate_str_by_id(5661),
	),
);

echo 'window.EPC_ORDERS=' . json_encode($config, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . ';';
