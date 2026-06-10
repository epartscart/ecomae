<?php
/**
 * Scheduled / manual price compare job runner.
 * GET /epc-auto-price-run.php?token=epartscart-deploy-2026&site_key=electronicae
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$host = function_exists('epc_portal_host') ? strtolower(epc_portal_host()) : '';
$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? ''))));
if ($siteKey === '') {
	if (strpos($host, 'electronicae') !== false) {
		$siteKey = 'electronicae';
	} elseif (strpos($host, 'epartscart') !== false) {
		$siteKey = 'epartscart';
	} else {
		$siteKey = 'platform';
	}
}

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		(string) $cfg->user,
		(string) $cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	epc_ape_ensure_schema($pdo);
	$result = epc_ape_run_compare($pdo, $siteKey, 'cron');
	echo json_encode(array(
		'ok' => true,
		'site_key' => $siteKey,
		'host' => $host,
		'result' => $result,
	), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array('ok' => false, 'error' => $e->getMessage()));
}
