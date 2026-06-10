<?php
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
error_reporting(E_ALL);
ini_set('display_errors', '1');
header('Content-Type: text/plain; charset=utf-8');

$_SERVER['HTTP_HOST'] = 'www.ecomae.com';
$_SERVER['SERVER_NAME'] = 'www.ecomae.com';
$_SERVER['HTTPS'] = 'on';
$_SERVER['REQUEST_URI'] = '/cp/';
$_SERVER['REQUEST_METHOD'] = 'GET';
$_SERVER['DOCUMENT_ROOT'] = __DIR__;

echo "step 1 config\n";
require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config();
require_once __DIR__ . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);

echo "step 2 guard file exists=" . (is_file(__DIR__ . '/cp/content/control/epc_cp_page_guard.php') ? 'yes' : 'no') . "\n";
echo "step 3 tenant page exists=" . (is_file(__DIR__ . '/cp/content/shop/tenant_hub/tenant_hub_main_page.php') ? 'yes' : 'no') . "\n";

echo "step 4 load cp/index\n";
chdir(__DIR__);
require __DIR__ . '/cp/index.php';
