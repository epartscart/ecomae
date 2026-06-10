<?php
/**
 * Set Supplier order email (LPO) on warehouses.
 * GET: token, key, [email=...], [names=R-UAE,RK-UAE,L-UAE], [dry_run=1]
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

$email = strtolower(trim((string)($_GET['email'] ?? '786yawer@gmail.com')));
if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
	http_response_code(400);
	exit(json_encode(array('ok' => false, 'error' => 'Invalid email')));
}

$namesRaw = trim((string)($_GET['names'] ?? 'R-UAE,RK-UAE,L-UAE'));
$names = array_values(array_filter(array_map('trim', explode(',', $namesRaw))));
if ($names === array()) {
	http_response_code(400);
	exit(json_encode(array('ok' => false, 'error' => 'No warehouse names')));
}

$dryRun = !empty($_GET['dry_run']);

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

$report = array(
	'ok' => true,
	'dry_run' => $dryRun,
	'email' => $email,
	'updated' => array(),
	'not_found' => array(),
);

foreach ($names as $name) {
	$stmt = $db->prepare('SELECT `id`, `name`, `connection_options` FROM `shop_storages` WHERE UPPER(`name`) = UPPER(?) LIMIT 1');
	$stmt->execute(array($name));
	$row = $stmt->fetch();
	if (!$row) {
		$report['not_found'][] = $name;
		continue;
	}

	$opts = json_decode((string)($row['connection_options'] ?? ''), true);
	if (!is_array($opts)) {
		$opts = array();
	}
	$before = (string)($opts['order_email'] ?? '');

	if (!$dryRun) {
		$opts['order_email'] = $email;
		$db->prepare('UPDATE `shop_storages` SET `connection_options` = ? WHERE `id` = ?')
			->execute(array(json_encode($opts, JSON_UNESCAPED_UNICODE), (int)$row['id']));
	}

	$report['updated'][] = array(
		'id' => (int)$row['id'],
		'name' => $row['name'],
		'order_email_before' => $before,
		'order_email_after' => $email,
		'resolved_order_email' => $dryRun
			? ($before !== '' ? $before : $email)
			: epc_storage_supplier_order_email($db, (int)$row['id']),
	);
}

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
