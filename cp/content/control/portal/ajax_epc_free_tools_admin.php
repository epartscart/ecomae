<?php
/**
 * Free Tools admin AJAX (Super CP) — activate/deactivate a public free tool
 * and return usage stats. Bootstraps config + DB ($db_link) so the admin
 * session check works, then mirrors the operator auth used by other BOC ajax.
 */
define('_ASTEXE_', 1);
header('Content-Type: application/json; charset=utf-8');
if (ob_get_level()) {
    ob_end_clean();
}

$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
require_once $docRoot . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;
require_once $docRoot . '/content/general_pages/epc_portal.php';
require_once $docRoot . '/content/general_pages/epc_portal_db.php';
if (function_exists('epc_portal_apply_config')) {
    epc_portal_apply_config($DP_Config);
}

$dbHost = trim((string) $DP_Config->host);
if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
    $dbHost = '127.0.0.1';
}
global $db_link;
try {
    $db_link = new PDO(
        'mysql:host=' . $dbHost . ';dbname=' . $DP_Config->db . ';charset=utf8',
        $DP_Config->user,
        $DP_Config->password,
        array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
    );
} catch (Throwable $e) {
    exit(json_encode(array('ok' => false, 'message' => 'Database connection failed')));
}

require_once $docRoot . '/content/users/dp_user.php';
require_once $docRoot . '/content/general_pages/epc_ecomae_free_tools.php';

if ((int) DP_User::getAdminId() <= 0) {
    http_response_code(403);
    exit(json_encode(array('ok' => false, 'message' => 'Admin login required')));
}
if (!function_exists('epc_portal_is_super_cp_host') || !epc_portal_is_super_cp_host()) {
    http_response_code(403);
    exit(json_encode(array('ok' => false, 'message' => 'Super CP only')));
}

$action = (string) ($_POST['action'] ?? $_GET['action'] ?? 'toggle');

if ($action === 'toggle') {
    $tool = preg_replace('/[^a-z]/', '', (string) ($_POST['tool'] ?? ''));
    $catalog = epc_free_tools_catalog();
    if ($tool === '' || !isset($catalog[$tool])) {
        exit(json_encode(array('ok' => false, 'message' => 'Unknown tool')));
    }
    $active = (string) ($_POST['active'] ?? '') === '1';
    epc_free_tools_set_active($tool, $active);
    @include_once $docRoot . '/content/general_pages/epc_boc_kernel.php';
    if (function_exists('epc_boc_audit_log') && isset($db_link) && $db_link instanceof PDO) {
        epc_boc_audit_log($db_link, (int) DP_User::getAdminId(), 'free_tools', $active ? 'tool_activate' : 'tool_deactivate', $tool);
    }
    exit(json_encode(array(
        'ok' => true,
        'active' => $active,
        'message' => $catalog[$tool]['name'] . ($active ? ' is now active.' : ' is now deactivated (shown as unavailable to visitors).'),
    )));
}

if ($action === 'stats') {
    exit(json_encode(array('ok' => true, 'stats' => epc_free_tools_usage_stats())));
}

exit(json_encode(array('ok' => false, 'message' => 'Unknown action')));
