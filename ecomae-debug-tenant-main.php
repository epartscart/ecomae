<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
error_reporting(E_ALL);
ini_set('display_errors', '1');
header('Content-Type: text/plain; charset=utf-8');
define('_ASTEXE_', 1);
$_SERVER['DOCUMENT_ROOT'] = __DIR__;
require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config();
require_once __DIR__ . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);
$db_link = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8', $DP_Config->user, $DP_Config->password);
$user_session = array('user_id' => 1);
$path = __DIR__ . '/cp/content/shop/tenant_hub/tenant_hub_main.php';
echo "parse check\n";
try {
	include $path;
	echo "include ok len=" . strlen(ob_get_contents() ?: '') . "\n";
} catch (Throwable $e) {
	echo 'include fail: ' . $e->getMessage() . "\n";
}
