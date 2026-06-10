<?php
/**
 * Guide panel render probe — guide_tab_html_length only.
 * GET ?token=…&site_key=epartscart|electronicae
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'epartscart'))));
if ($siteKey === '') {
	$siteKey = 'epartscart';
}

try {
	require_once __DIR__ . '/config.php';
	require_once __DIR__ . '/content/general_pages/epc_portal.php';
	require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
	require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
	require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_cp_install.php';
	require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
	require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_adapters.php';
	require_once __DIR__ . '/content/shop/price_engine/epc_industry_taxonomy.php';
	require_once __DIR__ . '/content/shop/price_engine/epc_discovery_adapters.php';
	require_once __DIR__ . '/content/shop/price_engine/epc_apai_country_sources.php';

	$cfg = new DP_Config();
	epc_portal_apply_config($cfg);
	$backend = trim((string) ($cfg->backend_dir ?? 'cp'), '/');
	if ($backend === '') {
		$backend = 'cp';
	}

	$platformPdo = epc_portal_platform_pdo();
	if (!$platformPdo instanceof PDO) {
		throw new RuntimeException('Platform registry unavailable');
	}

	$pdo = $platformPdo;
	foreach (epc_portal_list_tenants($platformPdo) as $t) {
		if ((string) ($t['site_key'] ?? '') === $siteKey) {
			try {
				$tenantPdo = epc_auto_price_setup_connect(array(
					'db' => (string) ($t['db_name'] ?? ''),
					'user' => (string) ($t['db_user'] ?? ''),
					'pass' => (string) ($t['db_password'] ?? ''),
				), $cfg);
				if ($tenantPdo instanceof PDO) {
					$pdo = $tenantPdo;
				}
			} catch (Throwable $connectErr) {
				// fall back to platform PDO
			}
			break;
		}
	}

	epc_ape_ensure_schema($pdo);

	$guidePanelPath = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/control/portal/epc_auto_price_guide_panel.php';
	$panelExists = is_file($guidePanelPath);
	$panelBytes = $panelExists ? (int) filesize($guidePanelPath) : 0;

	$siteKeyVar = $siteKey;
	$backendVar = $backend;
	$isSuperCp = false;
	$pageBase = '/' . $backend . '/control/portal/epc_auto_price_engine';
	$rules = epc_ape_rules_get($pdo, $siteKey);

	ob_start();
	$error = null;
	try {
		if (!$panelExists) {
			throw new RuntimeException('Guide panel missing: ' . $guidePanelPath);
		}
		include $guidePanelPath;
	} catch (Throwable $e) {
		$error = $e->getMessage();
	}
	$html = (string) ob_get_clean();
	$guideStart = stripos($html, '<div class="epc-ape-guide"');
	$guideLen = $guideStart !== false ? (strlen($html) - $guideStart) : 0;
	$profile = '';
	$industry = '';
	if (function_exists('epc_ape_guide_context')) {
		$ctx = epc_ape_guide_context($pdo, $siteKey, $backend, false);
		$profile = (string) ($ctx['profile'] ?? '');
		$industry = (string) ($ctx['industry_key'] ?? '');
	}

	echo json_encode(array(
		'ok' => $error === null && $panelExists && $guideLen > 15360,
		'site_key' => $siteKey,
		'profile' => $profile,
		'industry' => $industry,
		'panel_exists' => $panelExists,
		'panel_bytes' => $panelBytes,
		'guide_tab_html_length' => $guideLen,
		'has_faq' => stripos($html, 'FAQ') !== false,
		'has_buy_sell_rule' => stripos($html, 'buy sources only') !== false || stripos($html, 'Buy sources only') !== false,
		'error' => $error,
		'preview' => substr(preg_replace('/[\x00-\x08\x0B\x0C\x0E-\x1F]/', '', strip_tags($html)), 0, 120),
	), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT | JSON_INVALID_UTF8_SUBSTITUTE);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array(
		'ok' => false,
		'error' => $e->getMessage(),
		'site_key' => $siteKey,
	), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
}
