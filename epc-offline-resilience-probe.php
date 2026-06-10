<?php
/**
 * Offline resilience probe — UMAPI + Crossbase saved data readiness.
 * GET: token=epartscart-deploy-2026&key=TECH_KEY
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
require_once __DIR__ . '/content/shop/docpart/epc_crossbase_cache.php';

$report = array('ok' => true, 'umapi' => null, 'crossbase' => null, 'action_required' => array());

$host = isset($_SERVER['HTTP_HOST']) ? $_SERVER['HTTP_HOST'] : 'www.epartscart.com';
$base = 'https://' . $host;

if (function_exists('curl_init')) {
	$ch = curl_init($base . '/api/umapi_proxy.php?action=status');
	curl_setopt_array($ch, array(CURLOPT_RETURNTRANSFER => true, CURLOPT_TIMEOUT => 20, CURLOPT_SSL_VERIFYPEER => false));
	$umapiBody = curl_exec($ch);
	curl_close($ch);
	$report['umapi'] = json_decode((string)$umapiBody, true);

	$ch = curl_init($base . '/api/crossbase_status.php?sample=C110J');
	curl_setopt_array($ch, array(CURLOPT_RETURNTRANSFER => true, CURLOPT_TIMEOUT => 25, CURLOPT_SSL_VERIFYPEER => false));
	$cbBody = curl_exec($ch);
	curl_close($ch);
	$report['crossbase'] = json_decode((string)$cbBody, true);
}

$report['crossbase_cache'] = epc_crossbase_cache_stats();

try {
	$db = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8', $DP_Config->user, $DP_Config->password);
	$report['cp_cross_rows'] = (int)$db->query('SELECT COUNT(*) FROM `shop_docpart_articles_analogs_list`;')->fetchColumn();
	$report['local_crosses_on'] = !empty($DP_Config->local_crosses);
	try {
		$report['vin_cache_rows'] = (int)$db->query('SELECT COUNT(*) FROM `epc_umapi_vin_cache` WHERE `vehicle_count` > 0')->fetchColumn();
	} catch (Throwable $e2) {
		$report['vin_cache_rows'] = 0;
	}
} catch (Throwable $e) {
	$report['db_error'] = $e->getMessage();
}

if (!empty($report['umapi']['action_required'])) {
	$report['action_required'] = array_merge($report['action_required'], $report['umapi']['action_required']);
}
if (!empty($report['crossbase']['action_required'])) {
	$report['action_required'] = array_merge($report['action_required'], $report['crossbase']['action_required']);
}
$report['action_required'] = array_values(array_unique($report['action_required']));
$report['offline_ready'] = (!empty($report['umapi']['offline_ready']) || !empty($report['crossbase']['offline_ready']));

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
