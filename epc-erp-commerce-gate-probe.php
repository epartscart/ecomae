<?php
/**
 * Verify ERP commerce gating — ERP-only vs storefront tenants.
 * https://www.ecomae.com/epc-erp-commerce-gate-probe.php?token=...
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(60);

require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_erp_modules.php';
require_once __DIR__ . '/content/general_pages/epc_portal_shared_erp.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_vouchers.php';

function epc_commerce_gate_platform_pdo(): PDO
{
	$cfgFile = '/home/ecomae/htdocs/www.ecomae.com/config.local.php';
	if (!is_file($cfgFile)) {
		throw new RuntimeException('Missing platform config.local.php');
	}
	$epc_config_local = null;
	include $cfgFile;
	return new PDO(
		'mysql:host=127.0.0.1;dbname=' . ($epc_config_local['db'] ?? 'ecomae') . ';charset=utf8',
		(string) ($epc_config_local['user'] ?? 'ecomae'),
		(string) ($epc_config_local['password'] ?? ''),
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
}

function epc_commerce_gate_probe_tenant(PDO $platformPdo, string $siteKey): array
{
	$row = epc_portal_shared_erp_load_by_site_key($siteKey, $platformPdo);
	if ($row === null) {
		$st = $platformPdo->prepare('SELECT * FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1');
		$st->execute(array($siteKey));
		$row = $st->fetch(PDO::FETCH_ASSOC) ?: null;
	}
	if ($row === null) {
		return array('site_key' => $siteKey, 'error' => 'tenant_not_found');
	}
	$tenantPdo = null;
	if (!empty($row['erp_only_shared'])) {
		$tenantPdo = epc_portal_shared_erp_tenant_pdo($row);
	} else {
		require_once __DIR__ . '/config.php';
		$cfg = new DP_Config();
		try {
			$tenantPdo = new PDO(
				'mysql:host=' . $cfg->host . ';dbname=' . ($row['db_name'] ?? 'docpart') . ';charset=utf8',
				(string) ($row['db_user'] ?? $cfg->db_user),
				(string) ($row['db_password'] ?? $cfg->password),
				array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
			);
		} catch (Throwable $e) {
			$tenantPdo = null;
		}
	}
	$settings = ($tenantPdo instanceof PDO) ? epc_portal_load_site_settings($tenantPdo) : array();
	$mode = epc_portal_resolve_access_mode($settings);
	$tabs = epc_portal_erp_modules_allowed_tabs($settings);
	return array(
		'site_key' => (string) ($row['site_key'] ?? $siteKey),
		'erp_only_shared' => !empty($row['erp_only_shared']) ? 1 : 0,
		'access_mode' => $mode,
		'allowed_tabs' => $tabs,
		'has_fulfilment' => in_array('fulfilment', $tabs, true),
		'has_revenue' => in_array('revenue', $tabs, true),
		'has_sales_orders' => in_array('sales_orders', $tabs, true),
		'has_purchase_orders' => in_array('purchase_orders', $tabs, true),
		'has_procurement_link' => in_array('procurement_link', $tabs, true),
	);
}

try {
	$platformPdo = epc_commerce_gate_platform_pdo();
	epc_portal_db_ensure($platformPdo);
} catch (Throwable $e) {
	echo json_encode(array('ok' => false, 'error' => $e->getMessage()), JSON_PRETTY_PRINT);
	exit;
}

$asap = epc_commerce_gate_probe_tenant($platformPdo, 'asapcustom');
$eparts = epc_commerce_gate_probe_tenant($platformPdo, 'epartscart');

echo json_encode(array(
	'ok' => true,
	'probe' => 'erp_commerce_gate',
	'asapcustom' => $asap,
	'epartscart' => $eparts,
	'checks' => array(
		'asap_erp_only_mode' => ($asap['access_mode'] ?? '') === 'erp_only',
		'asap_no_fulfilment_tab' => empty($asap['has_fulfilment']),
		'asap_no_revenue_tab' => empty($asap['has_revenue']),
		'asap_has_direct_so_po' => !empty($asap['has_sales_orders']) && !empty($asap['has_purchase_orders']),
		'epartscart_has_commerce_tabs' => !empty($eparts['has_fulfilment']) && !empty($eparts['has_revenue']),
	),
), JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
