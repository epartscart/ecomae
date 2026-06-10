<?php
/**
 * Enable standalone ERP portal (schema + probe).
 * https://www.epartscart.com/epc-erp-portal-setup.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/content/general_pages/epc_portal_db.php';

$cfg = new DP_Config();
$pdo = new PDO(
	'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
	$cfg->user,
	$cfg->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);

echo "=== ERP portal setup ===\n\n";

epc_portal_db_ensure($pdo);
echo "Portal DB schema OK (access_mode column)\n";

require_once __DIR__ . '/content/shop/finance/epc_erp_portal_router.php';
echo "Router loaded: " . (function_exists('epc_erp_portal_try_route') ? 'yes' : 'no') . "\n";

$host = function_exists('epc_portal_host') ? epc_portal_host() : '';
if ($host !== '') {
	require_once __DIR__ . '/content/general_pages/epc_portal.php';
	$settings = epc_portal_load_site_settings($pdo);
	echo "Host {$host} access_mode=" . epc_portal_resolve_access_mode($settings) . "\n";
	echo "Storefront enabled: " . (epc_portal_storefront_enabled() ? 'yes' : 'no') . "\n";
}

$base = rtrim($cfg->domain_path, '/');
echo "\nPublic URLs:\n";
echo "  {$base}/erp\n";
echo "  {$base}/erp/guide\n";
echo "  {$base}/cp/shop/finance/erp (control panel)\n";
echo "\nERP-only tenants: set access_mode=erp_only in epc_portal_site_settings (home redirects to /erp).\n";
echo "Also run epc-erp-frontend-setup.php once per site if /shop/erp content is missing.\n";
echo "Done.\n";
