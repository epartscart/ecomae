<?php
/**
 * Debug POS CP 500 — GET ?token=epartscart-deploy-2026
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
error_reporting(E_ALL);
ini_set('display_errors', '1');
header('Content-Type: text/plain; charset=utf-8');

$_SERVER['HTTP_HOST'] = 'www.ecomae.com';
$_SERVER['SERVER_NAME'] = 'www.ecomae.com';
$_SERVER['HTTPS'] = 'on';
$_SERVER['REQUEST_URI'] = '/cp/shop/pos/terminal';
$_SERVER['REQUEST_METHOD'] = 'GET';
$_SERVER['DOCUMENT_ROOT'] = __DIR__;

echo "=== POS CP debug ===\n";

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;
require_once __DIR__ . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);

require_once __DIR__ . '/content/users/dp_user.php';
$user_session = DP_User::getAdminSession();
echo 'admin_session=' . (is_array($user_session) ? 'yes' : 'no') . "\n";

$wrapper = __DIR__ . '/cp/content/shop/pos/epc_pos_terminal_page.php';
echo "include wrapper...\n";
ob_start();
try {
	include $wrapper;
	$out = ob_get_clean();
	echo 'wrapper_bytes=' . strlen($out) . "\n";
	echo substr(strip_tags($out), 0, 800) . "\n";
} catch (Throwable $e) {
	ob_end_clean();
	echo 'WRAPPER FAIL: ' . $e->getMessage() . "\n" . $e->getTraceAsString() . "\n";
}

echo "\n--- full CP boot ---\n";
$_SERVER['REQUEST_URI'] = '/cp/shop/pos/terminal';
try {
	require __DIR__ . '/cp/index.php';
} catch (Throwable $e) {
	echo 'CP FAIL: ' . $e->getMessage() . "\n" . $e->getTraceAsString() . "\n";
}
