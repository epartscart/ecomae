<?php
/**
 * AJAX: load order detail pane HTML for dual-pane orders workspace.
 */
header('Content-Type: text/html; charset=utf-8');

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;

$dbHost = trim((string) ($DP_Config->host ?? ''));
if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
	$dbHost = '127.0.0.1';
}

try {
	$db_link = new PDO(
		'mysql:host=' . $dbHost . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (PDOException $e) {
	http_response_code(502);
	echo '<div class="epc-scp-orders-detail__empty"><p>Database unavailable</p></div>';
	exit;
}
$GLOBALS['db_link'] = $db_link;
$db_link->query('SET NAMES utf8;');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/lang/dp_lang.php';

if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
	if (function_exists('epc_portal_apply_config')) {
		epc_portal_apply_config($DP_Config);
		try {
			$db_link = new PDO(
				'mysql:host=' . (strtolower((string) $DP_Config->host) === 'localhost' ? '127.0.0.1' : $DP_Config->host)
					. ';dbname=' . $DP_Config->db . ';charset=utf8',
				$DP_Config->user,
				$DP_Config->password,
				array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
			);
			$db_link->query('SET NAMES utf8;');
			$GLOBALS['db_link'] = $db_link;
		} catch (Throwable $e) {
			// Keep first connection.
		}
	}
}

if (!DP_User::isAdmin()) {
	http_response_code(403);
	echo '<div class="epc-scp-orders-detail__empty"><p>Access denied</p></div>';
	exit;
}

// Print-doc links in the OMS pane need the admin CSRF key (stop_csrf CSRF 3 if empty).
$user_session = DP_User::getAdminSession();
if (!is_array($user_session)) {
	$user_session = array();
}
$GLOBALS['user_session'] = $user_session;

$order_id = isset($_GET['order_id']) ? (int) $_GET['order_id'] : 0;

try {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/shop/order_process/orders_background.php';

	// Paid-type captions used by the OMS console (optional on list page).
	if (!isset($shop_orders_paid_type) || !is_array($shop_orders_paid_type)) {
		$shop_orders_paid_type = array();
		try {
			$pt = $db_link->query('SELECT `id`, `name` FROM `shop_orders_paid_type` WHERE `active` = 1 ORDER BY `order`');
			while ($row = $pt->fetch(PDO::FETCH_ASSOC)) {
				$shop_orders_paid_type[(int) $row['id']] = $row['name'];
			}
		} catch (Throwable $e) {
			$shop_orders_paid_type = array();
		}
	}

	$epc_orders_detail_pane = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir
		. '/content/shop/order_process/epc_orders_detail_pane.php';
	ob_start();
	if (is_file($epc_orders_detail_pane)) {
		include $epc_orders_detail_pane;
	} else {
		echo '<div class="epc-scp-orders-detail__empty"><p>Detail pane file not found</p></div>';
	}
	$html = ob_get_clean();
	http_response_code(200);
	echo $html;
} catch (Throwable $e) {
	http_response_code(500);
	echo '<div class="epc-scp-orders-detail__empty"><p>Could not load OMS console</p><span class="text-muted small">'
		. htmlspecialchars($e->getMessage(), ENT_QUOTES, 'UTF-8')
		. '</span></div>';
}
