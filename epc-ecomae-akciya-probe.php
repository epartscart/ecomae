<?php
/**
 * Probe legacy /en/akciya redirect wiring.
 * https://www.ecomae.com/epc-ecomae-akciya-probe.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
echo "probe_version=v3\n";

$index = file_get_contents(__DIR__ . '/index.php');
$router = file_get_contents(__DIR__ . '/content/general_pages/epc_ecomae_platform_router.php');

echo "index_has_redirect_call=" . (strpos($index, 'epc_ecomae_marketing_redirect_legacy_cms_path') !== false ? 'yes' : 'no') . "\n";
echo "router_has_function=" . (strpos($router, 'function epc_ecomae_marketing_redirect_legacy_cms_path') !== false ? 'yes' : 'no') . "\n";
echo "index_bytes=" . strlen($index) . "\n";
echo "router_bytes=" . strlen($router) . "\n";

define('_ASTEXE_', 1);
$_SERVER['REQUEST_METHOD'] = 'GET';
$_SERVER['HTTP_HOST'] = 'www.ecomae.com';
$_SERVER['REQUEST_URI'] = '/en/akciya';

require_once __DIR__ . '/content/general_pages/epc_ecomae_platform_router.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
echo "is_marketing_host=" . (epc_ecomae_is_marketing_platform_host() ? 'yes' : 'no') . "\n";
echo "is_super_cp_host=" . (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host() ? 'yes' : 'no') . "\n";
echo "home_mode=" . (function_exists('epc_portal_home_mode') ? epc_portal_home_mode() : 'n/a') . "\n";
$path = epc_ecomae_platform_normalize_path('/en/akciya');
echo "normalized_path=" . $path . "\n";
echo "lang_subpath_match=" . (preg_match('#^/(en|ru|ar)/.+#i', $path) ? 'yes' : 'no') . "\n";
$would = !epc_portal_is_super_cp_host() && preg_match('#^/(en|ru|ar)/.+#i', $path);
echo "redirect_simulated=" . ($would ? 'yes' : 'no') . "\n";
