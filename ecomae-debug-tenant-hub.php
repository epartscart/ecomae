<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
error_reporting(E_ALL);
ini_set('display_errors', '1');

define('_ASTEXE_', 1);
$_SERVER['HTTP_HOST'] = 'www.ecomae.com';
$_SERVER['REQUEST_URI'] = '/cp/shop/tenant_hub/tenant_hub?tab=onboard';
$_SERVER['DOCUMENT_ROOT'] = __DIR__;
$_SERVER['REQUEST_METHOD'] = 'GET';

require __DIR__ . '/config.php';
$DP_Config = new DP_Config();
require __DIR__ . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);

echo 'super_cp=' . (epc_portal_is_super_cp_host() ? 'yes' : 'no') . "\n";
echo 'platform_host=' . (epc_portal_is_platform_hostname() ? 'yes' : 'no') . "\n";

try {
	$pdo = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
	$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
	echo "db ok\n";
} catch (Exception $e) {
	exit('db fail ' . $e->getMessage());
}

require __DIR__ . '/content/shop/tenant_hub/epc_tenant_hub_helpers.php';
echo "helpers ok\n";
try {
	$stats = epc_th_platform_stats($pdo);
	echo 'stats=' . json_encode($stats) . "\n";
	$tenants = epc_th_list_tenants($pdo);
	echo 'tenants=' . count($tenants) . "\n";
} catch (Throwable $e) {
	echo 'tenant hub fail ' . $e->getMessage() . ' @ ' . $e->getFile() . ':' . $e->getLine() . "\n";
}

$packs = epc_portal_enabled_packs();
echo 'packs=' . implode(',', $packs) . "\n";
