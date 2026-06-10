<?php
/**
 * Probe tenant country across registry, site settings, tax, APAI.
 * ?token=...&site_key=epartscart
 * ?token=...&all=1
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/shop/price_engine/epc_apai_country_sources.php';
require_once __DIR__ . '/content/shop/tenant_hub/epc_tenant_country_profile.php';

$platformPdo = epc_portal_platform_pdo();
$all = !empty($_GET['all']) && (string) $_GET['all'] === '1';
$onlyKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['site_key'] ?? 'epartscart')));

function epc_tenant_country_probe_row(PDO $platformPdo, string $siteKey): array
{
	$row = epc_portal_tenant_registry_row($platformPdo, $siteKey);
	$host = is_array($row) ? (string) ($row['hostname'] ?? '') : '';
	$registryCc = is_array($row) ? strtoupper((string) ($row['country_code'] ?? '')) : '';
	$settingsCc = '';
	if ($host !== '') {
		$settings = epc_portal_load_site_settings_for_host($platformPdo, $host);
		$settingsCc = strtoupper((string) ($settings['country_code'] ?? ($settings['contact']['country_code'] ?? '')));
	}
	$tenantPdo = null;
	$taxCc = '';
	$apaiCc = '';
	$apaiPack = 0;
	if (is_array($row)) {
		$tenantPdo = epc_tenant_country_tenant_pdo($row);
		if ($tenantPdo instanceof PDO) {
			$apaiCc = epc_apai_tenant_country($siteKey, $tenantPdo);
			$apaiPack = count(epc_apai_country_sources_for_tenant($tenantPdo, $siteKey));
			if (is_file(__DIR__ . '/content/shop/finance/epc_tax_toolkit.php')) {
				require_once __DIR__ . '/content/shop/finance/epc_tax_toolkit.php';
				$taxCc = epc_tax_toolkit_detect_tenant_country($tenantPdo, $siteKey);
			}
		}
	}
	return array(
		'site_key' => $siteKey,
		'hostname' => $host,
		'registry_country_code' => $registryCc,
		'settings_country_code' => $settingsCc,
		'tax_country_code' => $taxCc,
		'apai_country_code' => $apaiCc,
		'apai_pack_count' => $apaiPack,
		'market_label' => epc_tenant_country_market_label($tenantPdo, $siteKey),
	);
}

$out = array('ok' => true, 'tenants' => array());
if ($all && $platformPdo instanceof PDO) {
	epc_portal_db_ensure($platformPdo);
	foreach (epc_portal_list_tenants($platformPdo) as $t) {
		$key = (string) ($t['site_key'] ?? '');
		if ($key === '' || $key === 'ecomae') {
			continue;
		}
		$out['tenants'][] = epc_tenant_country_probe_row($platformPdo, $key);
	}
} elseif ($platformPdo instanceof PDO) {
	$out['tenants'][] = epc_tenant_country_probe_row($platformPdo, $onlyKey);
}

echo json_encode($out, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
