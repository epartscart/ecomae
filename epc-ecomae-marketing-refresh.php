<?php
/**
 * One-shot: confirm marketing PHP is loaded (opcode cache bust via touch).
 * https://www.ecomae.com/epc-ecomae-marketing-refresh.php?token=epartscart-deploy-2026
 */
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

$files = array(
	__DIR__ . '/index.php',
	__DIR__ . '/content/general_pages/epc_ecomae_platform_data.php',
	__DIR__ . '/content/general_pages/epc_ecomae_platform_pages.php',
	__DIR__ . '/content/general_pages/epc_ecomae_platform_layout.php',
	__DIR__ . '/content/general_pages/epc_ecomae_platform_tenant_showcase.php',
	__DIR__ . '/content/general_pages/epc_ecomae_platform_capabilities_catalog.php',
	__DIR__ . '/content/general_pages/epc_ecomae_platform_capability_guides.php',
	__DIR__ . '/content/general_pages/epc_ecomae_platform_router.php',
	__DIR__ . '/content/shop/finance/epc_erp_portal_router.php',
);

foreach ($files as $path) {
	if (is_file($path)) {
		@touch($path);
		echo "touched " . basename($path) . "\n";
	}
}

if (function_exists('opcache_reset')) {
	echo 'opcache_reset: ' . (opcache_reset() ? 'OK' : 'skip') . "\n";
}

$layout = file_get_contents(__DIR__ . '/content/general_pages/epc_ecomae_platform_layout.php');
echo "headline_check: " . (strpos($layout, 'One cloud: E-commerce + ERP + CRM') !== false ? 'OK' : 'MISSING') . "\n";
echo "einvoice_check: " . (strpos($layout, 'UAE e-invoice') !== false ? 'OK' : 'MISSING') . "\n";
echo "golive_check: " . (strpos($layout, 'ERP live within 24 hours') !== false ? 'OK' : 'MISSING') . "\n";
echo "continuity_check: " . (strpos($layout, 'epm-failover-flow') !== false ? 'OK' : 'MISSING') . "\n";
$data = file_get_contents(__DIR__ . '/content/general_pages/epc_ecomae_platform_data.php');
echo "continuity_nav: " . (strpos($data, 'business-continuity') !== false ? 'OK' : 'MISSING') . "\n";
$showcase = file_get_contents(__DIR__ . '/content/general_pages/epc_ecomae_platform_tenant_showcase.php');
echo "tenant_showcase: " . (strpos($showcase, 'epm-mini-hero--live') !== false ? 'OK' : 'MISSING') . "\n";
$shotFlat = __DIR__ . '/content/files/images/tenant-epartscart-storefront.png';
$shotSub = __DIR__ . '/content/files/images/ecomae-platform/tenant-epartscart-storefront.png';
echo "epartscart_storefront_flat: " . (is_file($shotFlat) ? 'OK' : 'MISSING') . "\n";
echo "epartscart_storefront_sub: " . (is_file($shotSub) ? 'OK' : 'MISSING') . "\n";
echo "cap_modal: " . (strpos($layout, 'epm-cap-modal') !== false ? 'OK' : 'MISSING') . "\n";
echo "done\n";
