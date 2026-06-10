<?php
/**
 * Auto Price Engine — demo status probe (deploy token).
 * GET /epc-auto-price-demo-probe.php?token=…&site_key=electronicae
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
	$siteKey = strpos($host, 'electronicae') !== false ? 'electronicae' : 'platform';
}

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		(string) $cfg->user,
		(string) $cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	epc_ape_ensure_schema($pdo);
	$ctx = epc_ape_guide_context($pdo, $siteKey, (string) ($cfg->backend_dir ?? 'cp'), false);
	$matrix = epc_ape_compare_matrix($pdo, $siteKey);
	echo json_encode(array(
		'ok' => true,
		'site_key' => $siteKey,
		'host' => $host,
		'profile' => $ctx['profile'] ?? '',
		'demo' => $ctx['demo'] ?? array(),
		'urls' => $ctx['urls'] ?? array(),
		'matrix_rows' => count($matrix),
		'matrix_sample' => array_slice($matrix, 0, 3),
	), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array('ok' => false, 'error' => $e->getMessage()));
}
