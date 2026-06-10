<?php
/**
 * Seed pre-provisioned demo DB pool (CloudPanel / clpctl). Operator only.
 * GET/POST: token=epartscart-deploy-2026&count=3
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
header('Content-Type: application/json; charset=utf-8');
set_time_limit(600);

$token = (string) ($_GET['token'] ?? $_POST['token'] ?? '');
if ($token === '' || !hash_equals(epc_deploy_token(), $token)) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';

$count = (int) ($_GET['count'] ?? $_POST['count'] ?? 3);
$clpPass = trim((string) ($_GET['clp_pass'] ?? $_POST['clp_pass'] ?? ''));
if ($clpPass !== '') {
	$GLOBALS['epc_demo_clp_pass'] = $clpPass;
}

$pdo = epc_portal_demo_platform_pdo();
if (!$pdo instanceof PDO) {
	epc_portal_demo_json_out(array('ok' => false, 'message' => 'Platform DB unavailable'), 500);
	exit;
}

$target = max(1, min(10, $count > 0 ? $count : 3));
$result = epc_portal_demo_pool_replenish($pdo, $target);
if (empty($result['ok']) && $count > 0) {
	$result = epc_portal_demo_pool_seed($pdo, $count);
}
epc_portal_demo_json_out($result, !empty($result['ok']) ? 200 : 500);
