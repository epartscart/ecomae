<?php
/**
 * Register CP URL shop/finance/erp/advanced-guide (repair helper).
 * GET/POST: token=epartscart-deploy-2026, key=<tech_key>
 *
 * Standalone counterpart to epc-register-erp-guide-content.php — registers the
 * Advanced ERP guide page using the shared registrar. Additive and idempotent.
 */
declare(strict_types=1);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

$DP_Config = isset($GLOBALS['DP_Config']) ? $GLOBALS['DP_Config'] : new DP_Config();
if ((string) ($_POST['key'] ?? $_GET['key'] ?? '') !== (string) $DP_Config->tech_key) {
    http_response_code(403);
    exit(json_encode(array('status' => false, 'message' => 'Invalid tech_key')));
}

require_once __DIR__ . '/content/shop/finance/epc_erp_advanced.php';

try {
    $db = new PDO(
        'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
        $DP_Config->user,
        $DP_Config->password,
        array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
    );
} catch (PDOException $e) {
    exit(json_encode(array('status' => false, 'message' => 'DB connect failed')));
}
$db->query('SET NAMES utf8');

$result = epc_erp_adv_register_guides($db, (string) $DP_Config->backend_dir);

echo json_encode(array(
    'status' => true,
    'message' => 'Advanced ERP guide registration processed',
    'result' => $result,
    'cp_url' => '/' . $DP_Config->backend_dir . '/shop/finance/erp/advanced-guide',
), JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
