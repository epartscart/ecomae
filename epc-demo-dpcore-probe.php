<?php
declare(strict_types=1);
ini_set('display_errors', '1');
error_reporting(E_ALL);
require_once __DIR__ . '/epc_deploy_auth.php';
header('Content-Type: text/plain; charset=utf-8');
$token = (string) ($_GET['token'] ?? '');
if ($token === '' || !hash_equals(epc_deploy_token(), $token)) {
	http_response_code(403);
	exit('Forbidden');
}
define('_ASTEXE_', 1);
$siteKey = preg_replace('/[^a-z0-9_]/', '', (string) ($_GET['site_key'] ?? 'demo_260601_ap_1'));
$_SERVER['REQUEST_METHOD'] = 'GET';
$_SERVER['REQUEST_URI'] = '/demo/' . $siteKey . '/';
$_SERVER['HTTP_HOST'] = 'www.ecomae.com';
echo "step 1 config\n";
require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config();
echo "step 2 portal\n";
require_once __DIR__ . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);
echo "step 3 demo bootstrap\n";
require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';
epc_portal_demo_try_bootstrap_storefront($DP_Config);
$GLOBALS['DP_Config'] = $DP_Config;
epc_portal_apply_config($DP_Config);
if (!empty($GLOBALS['epc_demo_storefront_site_key'])) {
	$DP_Config->domain_path = 'https://www.ecomae.com/demo/' . $GLOBALS['epc_demo_storefront_site_key'] . '/';
}
echo "db={$DP_Config->db} domain_path={$DP_Config->domain_path}\n";
echo "step 4 dp_core (no pre-db_link)\n";
$isFrontMode = 1;
require_once __DIR__ . '/core/dp_helper.php';
require_once __DIR__ . '/core/dp_content.php';
require_once __DIR__ . '/core/dp_module.php';
require_once __DIR__ . '/core/dp_template.php';
require_once __DIR__ . '/core/dp_core.php';
echo "OK dp_core loaded\n";
