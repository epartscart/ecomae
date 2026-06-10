<?php
/**
 * Diagnostic: www.ecomae.com homepage should render platform marketing, not ERP redirect.
 * https://www.ecomae.com/epc-ecomae-home-verify.php?token=...
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_ecomae_platform_router.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_portal_router.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$host = epc_portal_host();
$homeMode = function_exists('epc_portal_home_mode') ? epc_portal_home_mode() : 'unknown';
$accessMode = function_exists('epc_portal_access_mode') ? epc_portal_access_mode() : 'unknown';
$isPlatform = function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname();
$storefront = function_exists('epc_portal_storefront_enabled') && epc_portal_storefront_enabled();

echo "host={$host}\n";
echo "home_mode={$homeMode}\n";
echo "access_mode={$accessMode}\n";
echo "is_platform_hostname=" . ($isPlatform ? 'yes' : 'no') . "\n";
echo "storefront_enabled=" . ($storefront ? 'yes' : 'no') . "\n";

$idx = (string) @file_get_contents(__DIR__ . '/index.php');
echo 'index_marketing_guard=' . (strpos($idx, 'epc_is_ecomae_marketing_root_request') !== false ? 'yes' : 'no') . "\n";

$router = (string) @file_get_contents(__DIR__ . '/content/shop/finance/epc_erp_portal_router.php');
echo 'erp_router_platform_root_redirect=' . (strpos($router, "header('Location: ' . epc_erp_portal_canonical_base(''), true, 301)") !== false ? 'yes' : 'no') . "\n";

$matchHome = epc_ecomae_platform_match_path('/');
echo 'platform_match_root=' . ($matchHome !== null ? json_encode($matchHome) : 'null') . "\n";

$_SERVER['REQUEST_URI'] = '/';
$_SERVER['REQUEST_METHOD'] = 'GET';
epc_ecomae_platform_init_route();
$canRender = function_exists('epc_ecomae_platform_render_standalone');
echo 'render_standalone_fn=' . ($canRender ? 'yes' : 'no') . "\n";

$ctx = stream_context_create(array(
	'http' => array('timeout' => 20, 'ignore_errors' => true, 'follow_location' => 0),
	'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
));
$homeUrl = 'https://www.ecomae.com/';
$body = @file_get_contents($homeUrl, false, $ctx);
$code = 0;
$location = '';
if (isset($http_response_header) && is_array($http_response_header)) {
	foreach ($http_response_header as $h) {
		if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
			$code = (int) $m[1];
		}
		if (stripos($h, 'Location:') === 0) {
			$location = trim(substr($h, 9));
		}
	}
}
echo "http_root_code={$code}\n";
echo "http_root_location={$location}\n";

$markers = array('epm-body', 'ECOMAE-MARKETING-HOME-v3', 'ECOM AE', 'Unified ERP', 'epc-erp-standalone', 'Platform — E-commerce');
foreach ($markers as $m) {
	echo 'body_has_' . preg_replace('/[^a-z0-9]+/i', '_', $m) . '=' . (is_string($body) && stripos($body, $m) !== false ? 'yes' : 'no') . "\n";
}

$ok = ($code === 200 && is_string($body) && stripos($body, 'epm-body') !== false
	&& stripos($body, 'ECOMAE-MARKETING-HOME-v3') !== false
	&& stripos($body, 'epc-erp-standalone') === false);
echo 'PASS=' . ($ok ? 'yes' : 'no') . "\n";
