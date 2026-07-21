<?php
/**
 * CP return actions: decide line, set status, finalize against linked order.
 */
header('Content-Type: application/json; charset=utf-8');
require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config;
try {
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
	$db_link->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
} catch (PDOException $e) {
	exit(json_encode(array('status' => false, 'message' => 'DB')));
}
$db_link->query('SET NAMES utf8');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once dirname(__DIR__) . '/epc_returns_process.php';

if (!DP_User::isAdmin()) {
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

$user_session = DP_User::getAdminSession();
$csrf = isset($_POST['csrf_guard_key']) ? (string) $_POST['csrf_guard_key'] : '';
if (empty($user_session['csrf_guard_key']) || !hash_equals((string) $user_session['csrf_guard_key'], $csrf)) {
	exit(json_encode(array('status' => false, 'message' => 'CSRF')));
}

$action = isset($_POST['action']) ? (string) $_POST['action'] : '';
$return_id = isset($_POST['return_id']) ? (int) $_POST['return_id'] : 0;
if ($return_id < 1) {
	exit(json_encode(array('status' => false, 'message' => 'Invalid return')));
}

$auto = epc_returns_ensure_automation($db_link);
$adminId = (int) DP_User::getAdminId();
$now = time();

try {
	if ($action === 'set_return_status') {
		$status_id = (int) ($_POST['status_id'] ?? 0);
		if ($status_id < 1) {
			exit(json_encode(array('status' => false, 'message' => 'Invalid status')));
		}
		$closedId = epc_returns_closed_status_id($db_link);
		$complete = ($status_id === $closedId) ? 1 : 0;
		$db_link->prepare('UPDATE `shop_orders_returns` SET `status_id` = ?, `return_complete` = ? WHERE `id` = ?')
			->execute(array($status_id, $complete, $return_id));
		exit(json_encode(array('status' => true)));
	}

	if ($action === 'decide_line') {
		$line_id = (int) ($_POST['line_id'] ?? 0);
		$decide = (string) ($_POST['decide'] ?? '');
		if ($line_id < 1 || ($decide !== '0' && $decide !== '1')) {
			exit(json_encode(array('status' => false, 'message' => 'Invalid line decision')));
		}
		$lq = $db_link->prepare('SELECT * FROM `shop_orders_returns_items` WHERE `id` = ? AND `return_id` = ? LIMIT 1');
		$lq->execute(array($line_id, $return_id));
		$line = $lq->fetch(PDO::FETCH_ASSOC);
		if (!$line) {
			exit(json_encode(array('status' => false, 'message' => 'Line not found')));
		}

		$db_link->prepare('UPDATE `shop_orders_returns_items` SET `return_success` = ? WHERE `id` = ?')
			->execute(array((int) $decide, $line_id));

		$itemId = (int) $line['item_id'];
		$newStatus = ((int) $decide === 1) ? (int) $auto['complete_status_id'] : (int) $auto['reject_status_id'];
		if ($itemId > 0 && $newStatus > 0) {
			$db_link->prepare('UPDATE `shop_orders_items` SET `status` = ? WHERE `id` = ?')->execute(array($newStatus, $itemId));
			$oq = $db_link->prepare('SELECT `order_id` FROM `shop_orders_items` WHERE `id` = ? LIMIT 1');
			$oq->execute(array($itemId));
			$orderId = (int) $oq->fetchColumn();
			if ($orderId > 0) {
				$text = ((int) $decide === 1)
					? ('Return line approved for item ['.$itemId.'] on return #'.$return_id)
					: ('Return line denied for item ['.$itemId.'] on return #'.$return_id);
				$db_link->prepare('INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,?,?,0)')
					->execute(array($orderId, $now, $adminId, 1, $text));
			}
		}

		// Move return to Under consideration while deciding.
		$openId = epc_returns_open_status_id($db_link);
		if ($openId > 0) {
			$db_link->prepare('UPDATE `shop_orders_returns` SET `status_id` = ?, `return_complete` = 0 WHERE `id` = ? AND (`return_complete` IS NULL OR `return_complete` = 0)')
				->execute(array($openId, $return_id));
		}

		exit(json_encode(array('status' => true)));
	}

	if ($action === 'finalize_return') {
		$pending = $db_link->prepare('SELECT COUNT(*) FROM `shop_orders_returns_items` WHERE `return_id` = ? AND (`return_success` IS NULL OR `return_success` = \'\')');
		// return_success may be NULL or empty until decided; also treat non 0/1 as pending
		$pending = $db_link->prepare("SELECT COUNT(*) FROM `shop_orders_returns_items` WHERE `return_id` = ? AND (`return_success` IS NULL OR (`return_success` NOT IN (0,1) AND `return_success` NOT IN ('0','1')))");
		$pending->execute(array($return_id));
		if ((int) $pending->fetchColumn() > 0) {
			exit(json_encode(array('status' => false, 'message' => 'Decide every line (Approve or Deny) before closing.')));
		}

		// Apply item statuses for any lines not yet moved.
		$lines = $db_link->prepare('SELECT * FROM `shop_orders_returns_items` WHERE `return_id` = ?');
		$lines->execute(array($return_id));
		while ($line = $lines->fetch(PDO::FETCH_ASSOC)) {
			$itemId = (int) $line['item_id'];
			$newStatus = ((int) $line['return_success'] === 1) ? (int) $auto['complete_status_id'] : (int) $auto['reject_status_id'];
			if ($itemId > 0 && $newStatus > 0) {
				$db_link->prepare('UPDATE `shop_orders_items` SET `status` = ? WHERE `id` = ?')->execute(array($newStatus, $itemId));
			}
		}

		$approvedSum = 0.0;
		$sumQ = $db_link->prepare(
			'SELECT SUM(oi.`price` * oi.`count_need`) FROM `shop_orders_returns_items` ri
			 INNER JOIN `shop_orders_items` oi ON oi.`id` = ri.`item_id`
			 WHERE ri.`return_id` = ? AND ri.`return_success` IN (1,\'1\')'
		);
		$sumQ->execute(array($return_id));
		$approvedSum = (float) $sumQ->fetchColumn();

		$closedId = epc_returns_closed_status_id($db_link);
		$db_link->prepare('UPDATE `shop_orders_returns` SET `status_id` = ?, `return_complete` = 1, `sum` = ? WHERE `id` = ?')
			->execute(array($closedId, $approvedSum, $return_id));

		$orderIds = epc_returns_order_ids($db_link, $return_id);
		foreach ($orderIds as $orderId) {
			$db_link->prepare('INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,?,?,0)')
				->execute(array($orderId, $now, $adminId, 1, 'Return #'.$return_id.' closed. Approved sum: '.number_format($approvedSum, 2, '.', '')));
		}

		exit(json_encode(array('status' => true, 'approved_sum' => $approvedSum)));
	}

	exit(json_encode(array('status' => false, 'message' => 'Unknown action')));
} catch (Throwable $e) {
	exit(json_encode(array('status' => false, 'message' => $e->getMessage())));
}
