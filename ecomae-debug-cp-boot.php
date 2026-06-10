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
$isFrontMode = 0;

try {
	require __DIR__ . '/config.php';
	$DP_Config = new DP_Config();
	require __DIR__ . '/content/general_pages/epc_portal.php';
	epc_portal_apply_config($DP_Config);
	echo "step config ok super=" . (epc_portal_is_super_cp_host() ? 'yes' : 'no') . "\n";

	require __DIR__ . '/core/dp_helper.php';
	echo "step helper ok\n";
	require __DIR__ . '/core/dp_content.php';
	echo "step content ok\n";
	require __DIR__ . '/core/dp_module.php';
	echo "step module ok\n";
	require __DIR__ . '/core/dp_template.php';
	echo "step template ok\n";

	ob_start();
	require __DIR__ . '/core/dp_core.php';
	$out = ob_get_clean();
	echo "step core ok out_len=" . strlen($out) . "\n";
	if (stripos($out, 'Fatal') !== false || stripos($out, 'Tenant hub') !== false) {
		echo substr($out, 0, 800) . "\n";
	}
} catch (Throwable $e) {
	echo 'FAIL ' . $e->getMessage() . ' @ ' . $e->getFile() . ':' . $e->getLine() . "\n";
	echo $e->getTraceAsString() . "\n";
}
