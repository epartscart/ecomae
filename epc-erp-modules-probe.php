<?php
/**
 * Probe ERP module packs per tenant — operator verification.
 * https://www.ecomae.com/epc-erp-modules-probe.php?token=...&site_key=asapcustom
 * https://www.ecomae.com/epc-erp-modules-probe.php?token=...  (all shared ERP tenants)
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(120);

require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant_control.php';
require_once __DIR__ . '/content/general_pages/epc_portal_erp_modules.php';
require_once __DIR__ . '/content/general_pages/epc_portal_shared_erp.php';

$siteKeyFilter = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? $_POST['site_key'] ?? ''))));

function epc_erp_mod_probe_platform_pdo(): PDO
{
	$cfgFile = '/home/ecomae/htdocs/www.ecomae.com/config.local.php';
	if (!is_file($cfgFile)) {
		throw new RuntimeException('Missing platform config.local.php');
	}
	$epc_config_local = null;
	include $cfgFile;
	$pdo = new PDO(
		'mysql:host=127.0.0.1;dbname=' . ($epc_config_local['db'] ?? 'ecomae') . ';charset=utf8',
		(string) ($epc_config_local['user'] ?? 'ecomae'),
		(string) ($epc_config_local['password'] ?? ''),
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	epc_portal_db_ensure($pdo);
	return $pdo;
}

function epc_erp_mod_probe_tenant_settings(?array $row): array
{
	if ($row === null) {
		return array();
	}
	$pdo = epc_portal_shared_erp_tenant_pdo($row);
	if (!($pdo instanceof PDO)) {
		return array();
	}
	return epc_portal_load_site_settings($pdo);
}

function epc_erp_mod_probe_row(array $row): array
{
	$settings = epc_erp_mod_probe_tenant_settings($row);
	$mods = epc_portal_erp_modules_enabled($settings);
	$areas = epc_portal_erp_modules_enabled_areas($settings);
	$preset = epc_portal_erp_modules_detect_preset($mods);
	$presets = epc_portal_erp_modules_presets();
	return array(
		'site_key' => (string) ($row['site_key'] ?? ''),
		'trade_name' => (string) ($row['trade_name'] ?? ''),
		'industry_code' => (string) ($row['industry_code'] ?? ''),
		'erp_only_shared' => !empty($row['erp_only_shared']) ? 1 : 0,
		'access_mode' => (string) ($settings['access_mode'] ?? ''),
		'erp_modules' => $mods,
		'erp_areas' => $areas,
		'erp_modules_count' => count($mods),
		'erp_areas_count' => count($areas),
		'detected_preset' => $preset,
		'preset_label' => ($preset !== '' && isset($presets[$preset]['label'])) ? (string) $presets[$preset]['label'] : '',
		'expected_preset' => epc_portal_industry_erp_modules_preset((string) ($row['industry_code'] ?? '')),
		'client_erp_shell' => function_exists('epc_client_erp_shell_url')
			? 'https://www.ecomae.com' . epc_client_erp_shell_url((string) ($row['site_key'] ?? ''))
			: '',
	);
}

try {
	$platformPdo = epc_erp_mod_probe_platform_pdo();
} catch (Throwable $e) {
	echo json_encode(array('ok' => false, 'error' => $e->getMessage()), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
	exit;
}

require_once __DIR__ . '/content/general_pages/epc_client_erp_router.php';

$out = array(
	'ok' => true,
	'probe' => 'erp_modules',
	'tenants' => array(),
	'presets' => array_map(function ($p) {
		return array('label' => $p['label'] ?? '', 'modules' => $p['modules'] ?? array());
	}, epc_portal_erp_modules_presets()),
);

if ($siteKeyFilter !== '') {
	$row = epc_portal_tenant_control_get_row($platformPdo, $siteKeyFilter);
	if ($row === null) {
		$out['ok'] = false;
		$out['error'] = 'tenant_not_found';
	} else {
		$out['tenants'][] = epc_erp_mod_probe_row($row);
	}
} else {
	$st = $platformPdo->query(
		'SELECT * FROM `epc_portal_tenants` WHERE `erp_only_shared` = 1 ORDER BY `site_key` ASC'
	);
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$out['tenants'][] = epc_erp_mod_probe_row($row);
	}
}

echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
