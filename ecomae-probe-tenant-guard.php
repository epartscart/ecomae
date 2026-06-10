<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
error_reporting(E_ALL);
ini_set('display_errors', '1');
header('Content-Type: text/plain; charset=utf-8');
define('_ASTEXE_', 1);
$_SERVER['HTTP_HOST'] = 'www.ecomae.com';
$_SERVER['DOCUMENT_ROOT'] = __DIR__;
require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config();
require_once __DIR__ . '/content/users/dp_user.php';
require_once __DIR__ . '/' . $DP_Config->backend_dir . '/content/control/epc_cp_page_guard.php';
echo "admin=" . (DP_User::isAdmin() ? 'yes' : 'no') . "\n";
ob_start();
if (epc_cp_page_require_admin('the Tenant hub')) {
	epc_cp_page_include('content/shop/tenant_hub/tenant_hub_main.php', 'missing');
}
echo "out_len=" . strlen(ob_get_clean()) . "\n";
