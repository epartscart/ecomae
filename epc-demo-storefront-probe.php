<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
header('Content-Type: application/json; charset=utf-8');
$token = (string) ($_GET['token'] ?? '');
if ($token === '' || !hash_equals(epc_deploy_token(), $token)) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'message' => 'Forbidden')));
}
define('_ASTEXE_', 1);
$siteKey = preg_replace('/[^a-z0-9_]/', '', (string) ($_GET['site_key'] ?? 'demo_260601_ap_1'));
$report = array('ok' => true, 'site_key' => $siteKey, 'steps' => array());
try {
	require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';
	$pdo = epc_portal_demo_platform_pdo();
	$report['steps']['platform_pdo'] = $pdo instanceof PDO;
	$st = $pdo->prepare('SELECT * FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1');
	$st->execute(array($siteKey));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	$report['steps']['tenant_row'] = $row ? array(
		'is_demo' => $row['is_demo'] ?? null,
		'status' => $row['status'] ?? null,
		'industry_code' => $row['industry_code'] ?? null,
		'db_name' => $row['db_name'] ?? null,
		'demo_expires_at' => $row['demo_expires_at'] ?? null,
	) : null;
	if ($row) {
		$tenantPdo = epc_portal_demo_tenant_pdo($row);
		$report['steps']['tenant_pdo'] = $tenantPdo instanceof PDO;
		if ($tenantPdo instanceof PDO) {
			require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
			$settings = epc_portal_load_site_settings_for_host($tenantPdo, 'www.ecomae.com');
			$report['steps']['site_settings'] = array(
				'industry_code' => $settings['industry_code'] ?? null,
				'storefront_package' => $settings['contact']['storefront_package'] ?? null,
				'domain_path' => $settings['domain_path'] ?? null,
			);
		}
	}
	require_once __DIR__ . '/config.php';
	$DP_Config = new DP_Config();
	$_SERVER['REQUEST_URI'] = '/demo/' . $siteKey . '/';
	$boot = epc_portal_demo_try_bootstrap_storefront($DP_Config);
	$report['steps']['bootstrap'] = $boot;
	$report['steps']['demo_context'] = !empty($GLOBALS['epc_demo_storefront_context']);
	$report['steps']['domain_path'] = (string) ($DP_Config->domain_path ?? '');
	require_once __DIR__ . '/content/general_pages/epc_portal.php';
	epc_portal_apply_config($DP_Config);
	$profile = epc_portal_site_profile();
	$report['steps']['site_profile'] = array(
		'industry' => $profile['industry'] ?? null,
		'domain_path' => $profile['domain_path'] ?? null,
	);
	$loaded = epc_portal_load_site_settings();
	$report['steps']['load_site_settings'] = array(
		'industry_code' => $loaded['industry_code'] ?? null,
		'storefront_package' => $loaded['contact']['storefront_package'] ?? null,
	);
	$report['steps']['home_mode'] = epc_portal_home_mode();
	$report['steps']['storefront_package_fn'] = epc_portal_active_storefront_package();
	if (!empty($_GET['test_index'])) {
		ob_start();
		try {
			$_SERVER['REQUEST_METHOD'] = 'GET';
			$_SERVER['REQUEST_URI'] = '/demo/' . $siteKey . '/';
			unset($GLOBALS['epc_demo_storefront_context'], $GLOBALS['epc_demo_storefront_site_key'], $GLOBALS['epc_demo_storefront_industry'], $GLOBALS['epc_demo_storefront_tenant_row']);
			include __DIR__ . '/index.php';
			$out = ob_get_clean();
			$report['steps']['index'] = array('ok' => true, 'bytes' => strlen($out), 'head' => substr($out, 0, 200));
		} catch (Throwable $e) {
			if (ob_get_level()) {
				ob_end_clean();
			}
			$report['ok'] = false;
			$report['index_error'] = $e->getMessage();
			$report['index_file'] = $e->getFile();
			$report['index_line'] = $e->getLine();
		}
	}
	if (!empty($_GET['test_clone'])) {
		$report['steps']['epartscart_clone'] = epc_portal_demo_epartscart_theme_source();
	}
	if (!empty($_GET['test_push'])) {
		$presets = epc_portal_demo_industry_presets();
		$preset = $presets['auto_parts'];
		$report['steps']['push'] = epc_portal_demo_push_tenant_settings(
			$pdo,
			$siteKey,
			$preset,
			(string) ($row['trade_name'] ?? 'Demo'),
			(string) ($row['demo_contact_email'] ?? 'demo@ecomae.com')
		);
	}
} catch (Throwable $e) {
	$report['ok'] = false;
	$report['error'] = $e->getMessage();
	$report['file'] = $e->getFile();
	$report['line'] = $e->getLine();
}
echo json_encode($report, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
