<?php
/**
 * Benchmark fulfilment tab SQL for a date range.
 * GET: token=epartscart-deploy-2026&from=2026-05-01&to=2026-05-25
 */
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_fulfilment.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

$from_str = isset($_GET['from']) ? (string)$_GET['from'] : date('Y-m-01');
$to_str = isset($_GET['to']) ? (string)$_GET['to'] : date('Y-m-d');
$date_from = strtotime($from_str . ' 00:00:00') ?: strtotime(date('Y-m-01'));
$date_to = strtotime($to_str . ' 23:59:59') ?: time();

$steps = array();
$run = function ($name, callable $fn) use (&$steps) {
	$t0 = microtime(true);
	try {
		$result = $fn();
		$steps[$name] = array(
			'ok' => true,
			'ms' => round((microtime(true) - $t0) * 1000, 1),
			'result' => $result,
		);
	} catch (Throwable $e) {
		$steps[$name] = array(
			'ok' => false,
			'ms' => round((microtime(true) - $t0) * 1000, 1),
			'error' => $e->getMessage(),
		);
	}
};

$run('dashboard', function () use ($pdo, $date_from, $date_to) {
	$d = epc_erp_fulfilment_dashboard($pdo, $date_from, $date_to, 40);
	return array(
		'total_orders' => (int)$d['total_orders'],
		'sample' => count($d['orders']),
		'pipeline' => $d['pipeline'],
	);
});
$run('stock_movements', function () use ($pdo, $date_from, $date_to) {
	return count(epc_erp_fulfilment_stock_movements($pdo, $date_from, $date_to, 60));
});
$run('returns', function () use ($pdo) {
	return count(epc_erp_fulfilment_returns($pdo, 30));
});

echo json_encode(array(
	'status' => true,
	'from' => $from_str,
	'to' => $to_str,
	'steps' => $steps,
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
