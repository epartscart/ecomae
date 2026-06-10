<?php
/**
 * Probe supplier LPO e-mail configuration and optional test send.
 * GET: token, key, [order_id], [send=1]
 */
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Forbidden')));
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
if ((string)($_GET['key'] ?? '') !== $DP_Config->tech_key) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Invalid key')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/content/shop/usefull/epc_supplier_notifications.php';

try {
	$db = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC)
	);
} catch (Throwable $e) {
	exit(json_encode(array('ok' => false, 'error' => $e->getMessage())));
}

$report = array('ok' => true, 'storages' => array(), 'notification' => null);

$n = $db->prepare('SELECT `id`, `name`, `email_on`, `email_subject` FROM `notifications_settings` WHERE `name` = ? LIMIT 1');
$n->execute(array('lpo_to_supplier'));
$report['notification'] = $n->fetch() ?: array('missing' => true);

$sq = $db->query('SELECT s.`id`, s.`name`, s.`interface_type`, s.`connection_options`, p.`name` AS `price_list`, p.`sender_email`
	FROM `shop_storages` s
	LEFT JOIN `shop_docpart_prices` p ON p.`id` = CAST(JSON_UNQUOTE(JSON_EXTRACT(s.`connection_options`, "$.price_id")) AS UNSIGNED)
	ORDER BY s.`id`');
while ($row = $sq->fetch()) {
	$sid = (int)$row['id'];
	$resolved = epc_storage_supplier_order_email($db, $sid);
	$report['storages'][] = array(
		'id' => $sid,
		'name' => $row['name'],
		'interface_type' => (int)$row['interface_type'],
		'price_list' => $row['price_list'],
		'price_sender_email' => $row['sender_email'],
		'resolved_order_email' => $resolved,
		'lpo_ready' => $resolved !== '',
	);
}

$order_id = (int)($_GET['order_id'] ?? 0);
if ($order_id > 0) {
	$report['order_id'] = $order_id;
	$groups = array();
	$iq = $db->prepare('SELECT * FROM `shop_orders_items` WHERE `order_id` = ?');
	$iq->execute(array($order_id));
	while ($item = $iq->fetch()) {
		$sid = epc_order_item_storage_id($db, $item);
		if ($sid <= 0) {
			continue;
		}
		if (!isset($groups[$sid])) {
			$groups[$sid] = 0;
		}
		$groups[$sid]++;
	}
	$report['order_lines_by_storage'] = $groups;

	if (!empty($_GET['send'])) {
		epc_send_supplier_lpo_notifications($db, $order_id);
		$report['send_triggered'] = true;
		$logs = $db->prepare('SELECT `text` FROM `shop_orders_logs` WHERE `order_id` = ? AND `text` LIKE ? ORDER BY `id` DESC LIMIT 10');
		$logs->execute(array($order_id, '%Supplier LPO%'));
		$report['recent_logs'] = $logs->fetchAll(PDO::FETCH_COLUMN);
	}
}

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
