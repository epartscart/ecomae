<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$_SERVER['HTTP_HOST'] = 'www.epartscart.com';
$_SERVER['SERVER_NAME'] = 'www.ecomae.com';
$_SERVER['HTTPS'] = 'on';
$_SERVER['REQUEST_URI'] = '/en/';
$_SERVER['REQUEST_METHOD'] = 'GET';
$_SERVER['DOCUMENT_ROOT'] = '/home/ecomae/htdocs/www.ecomae.com';

define('_ASTEXE_', 1);
$isFrontMode = 1;

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
$epcTenantDbFile = $_SERVER['DOCUMENT_ROOT'] . '/config.tenant-db.php';
if (is_file($epcTenantDbFile)) {
	$epc_tenant_db = null;
	require $epcTenantDbFile;
	if (isset($epc_tenant_db) && is_array($epc_tenant_db)) {
		foreach (array('db', 'user', 'password') as $epcTk) {
			if (!empty($epc_tenant_db[$epcTk]) && property_exists($DP_Config, $epcTk)) {
				$DP_Config->$epcTk = $epc_tenant_db[$epcTk];
			}
		}
	}
}
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);

echo 'pre-dpcore db=' . $DP_Config->db . ' domain_path=' . $DP_Config->domain_path . "\n";

ob_start();
try {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/core/dp_core.php';
	$out = ob_get_clean();
	echo "dp_core=loaded output_len=" . strlen($out) . "\n";
	if ($out !== '') {
		echo substr($out, 0, 200) . "\n";
	}
} catch (Throwable $e) {
	ob_end_clean();
	echo 'dp_core=exception ' . $e->getMessage() . "\n";
}
