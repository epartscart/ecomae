<?php
/**
 * Orders list — runtime config JS (footer load, outside eval'd CP content pane).
 * Must open $db_link before DP_User::getAdminSession() (same pattern as multivendor config).
 */
declare(strict_types=1);

// Must emit pure JS — never HTML warnings (breaks OMS boot in the browser).
@ini_set('display_errors', '0');
@ini_set('html_errors', '0');
error_reporting(E_ALL & ~E_DEPRECATED & ~E_USER_DEPRECATED & ~E_NOTICE & ~E_STRICT);

header('Content-Type: application/javascript; charset=utf-8');
header('Cache-Control: no-store, no-cache, must-revalidate');

try {
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}

	$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
	require_once $docRoot . '/config.php';
	require_once $docRoot . '/content/users/dp_user.php';

	$epcOrdersConnect = static function ($cfg) {
		$dbHost = trim((string) ($cfg->host ?? ''));
		if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
			$dbHost = '127.0.0.1';
		}
		return new PDO(
			'mysql:host=' . $dbHost . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	};

	// Prefer the same DB bootstrap as OMS AJAX (docroot config). Portal apply can
	// point at a different schema where admin_session is missing.
	$DP_Config = new DP_Config();
	$GLOBALS['DP_Config'] = $DP_Config;
	$db_link = $epcOrdersConnect($DP_Config);
	$GLOBALS['db_link'] = $db_link;

	$user_session = DP_User::getAdminSession();
	if ((empty($user_session) || !is_array($user_session)) && !DP_User::isAdmin()) {
		if (is_file($docRoot . '/content/general_pages/epc_portal.php')) {
			require_once $docRoot . '/content/general_pages/epc_portal.php';
			if (function_exists('epc_portal_apply_config')) {
				$portalCfg = new DP_Config();
				ob_start();
				try {
					epc_portal_apply_config($portalCfg);
				} catch (Throwable $e) {
				}
				ob_end_clean();
				$DP_Config = $portalCfg;
				$GLOBALS['DP_Config'] = $DP_Config;
				$db_link = $epcOrdersConnect($DP_Config);
				$GLOBALS['db_link'] = $db_link;
				$user_session = DP_User::getAdminSession();
			}
		}
	}

	if ((empty($user_session) || !is_array($user_session)) && DP_User::isAdmin()) {
		$user_session = array(
			'csrf_guard_key' => (string) ($_COOKIE['csrf_guard_key'] ?? ''),
			'user_id' => (int) DP_User::getAdminId(),
		);
	}

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
		$st = $db_link->query(
			'SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_inverse` != 1 AND `for_finish` != 1 AND `for_created` != 1'
		);
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$in_process[] = (string) $row['id'];
		}
		$st = $db_link->query('SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_finish` = 1');
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$statuses_for_finish[] = (string) $row['id'];
		}
		$st = $db_link->query('SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_inverse` = 1');
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$statuses_for_inverse[] = (string) $row['id'];
		}
	} catch (Throwable $e) {
	}

	require_once $docRoot . '/lang/dp_lang.php';
	if (is_file($docRoot . '/content/general_pages/epc_cp_translate.php')) {
		require_once $docRoot . '/content/general_pages/epc_cp_translate.php';
	}

	$epcOrdersMsg = static function ($id, $fallback = '') {
		if (function_exists('translate_str_by_id')) {
			try {
				$v = translate_str_by_id($id);
				if ($v !== '' && $v !== null) {
					return (string) $v;
				}
			} catch (Throwable $e) {
			}
		}
		return (string) $fallback;
	};

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
			'omsAjax' => '/' . $backend . '/content/shop/order_process/ajax_epc_orders_oms.php',
			'setViewed' => '/' . $backend . '/content/shop/order_process/ajax_set_orders_viewed.php',
			'deleteOrders' => '/' . $backend . '/content/shop/order_process/ajax_delete_orders.php',
			'userModal' => '/' . $backend . '/content/users/statistics/frontAjax/ajax_loadUserModal.php',
			'setOrderStatus' => '/content/shop/protocol/set_order_status.php',
			'addComment' => '/' . $backend . '/content/shop/order_process/ajax_add_comment_to_log.php',
			'erpAjax' => '/' . $backend . '/content/shop/finance/erp/ajax_erp_endpoint.php',
			'payForOrder' => '/content/shop/protocol/pay_for_order.php',
			'payRefund' => '/' . $backend . '/content/shop/order_process/ajax_order_pay_refund.php',
			'dcPrintBase' => '/content/shop/document_control/service/print.php',
			'legacyPrintBase' => '/content/shop/print_docs/service/print.php',
		),
		'msg' => array(
			'selectOrders' => $epcOrdersMsg(3597, 'Select orders'),
			'selectOrdersViewed' => $epcOrdersMsg(3598, 'Select orders to mark viewed'),
			'setViewedFail' => $epcOrdersMsg(3599, 'Could not mark viewed'),
			'deleteConfirm' => $epcOrdersMsg(3600, 'Delete selected orders?'),
			'setStatusFail' => $epcOrdersMsg(3508, 'Could not update status'),
			'setStatusOk' => 'Status updated',
			'commentEmpty' => 'Enter a note first',
			'commentOk' => 'Note saved',
			'commentFail' => 'Could not save note',
			'finishConfirm' => $epcOrdersMsg(5297, 'Finish selected orders?'),
			'inverseConfirm' => $epcOrdersMsg(5299, 'Cancel selected orders?'),
			'userModalFail' => $epcOrdersMsg(3541, 'Could not open customer'),
			'selectPlaceholder' => $epcOrdersMsg(2094, 'Select'),
			'selectAllText' => $epcOrdersMsg(2355, 'Select all'),
			'allSelected' => $epcOrdersMsg(5660, 'All selected'),
			'countSelected' => $epcOrdersMsg(5661, '# of % selected'),
			'itemSaved' => 'Item updated',
			'itemFail' => 'Could not update item',
			'msgSent' => 'Message sent to customer',
			'msgFail' => 'Could not send message',
			'msgEmpty' => 'Enter a message first',
			'payEmpty' => 'Enter a payment amount',
			'payTooMuch' => 'Amount exceeds balance due',
			'payBalanceWarn' => 'Customer balance is lower than this amount. Continue?',
			'payOk' => 'Payment recorded',
			'payFail' => 'Payment failed',
			'refundConfirm' => 'Refund this order payment?',
			'refundOk' => 'Refund completed',
			'refundFail' => 'Refund failed',
		),
	);

	echo 'window.EPC_ORDERS=' . json_encode($config, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . ';';
} catch (Throwable $e) {
	echo 'window.EPC_ORDERS={};/* ' . str_replace(array('*/', "\n", "\r"), '', $e->getMessage()) . ' */';
}
