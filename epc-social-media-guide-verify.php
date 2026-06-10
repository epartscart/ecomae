<?php
/**
 * Social Media Hub — guide tab render probe (no CP login).
 * GET ?token=…&site_key=epartscart|platform|electronicae
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
	require_once __DIR__ . '/content/social_media/epc_social_media_helpers.php';
	require_once __DIR__ . '/content/social_media/epc_social_media_pack_data.php';

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
	if ($siteKey !== 'platform') {
		foreach (epc_portal_list_tenants($platformPdo) as $t) {
			if ((string) ($t['site_key'] ?? '') !== $siteKey) {
				continue;
			}
			$cred = function_exists('epc_portal_tenant_setup_credentials')
				? epc_portal_tenant_setup_credentials($t)
				: array(
					'db' => (string) ($t['db_name'] ?? ''),
					'user' => (string) ($t['db_user'] ?? ''),
					'pass' => (string) ($t['db_password'] ?? ''),
				);
			if ($cred['db'] !== '') {
				$host = trim((string) $cfg->host);
				if ($host === '' || strtolower($host) === 'localhost') {
					$host = '127.0.0.1';
				}
				try {
					$tenantPdo = new PDO(
						'mysql:host=' . $host . ';dbname=' . $cred['db'] . ';charset=utf8',
						$cred['user'] !== '' ? $cred['user'] : $cfg->user,
						$cred['pass'] !== '' ? $cred['pass'] : $cfg->password,
						array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
					);
					$pdo = $tenantPdo;
				} catch (Throwable $connectErr) {
					// platform PDO fallback
				}
			}
			break;
		}
	}

	epc_social_ensure_schema($pdo);
	$resolvedKey = epc_social_resolve_site_key($pdo);
	if ($siteKey !== 'platform') {
		$resolvedKey = $siteKey;
	}
	$brand = epc_social_brand_context($resolvedKey, $pdo);
	$integrationsUrl = '/' . $backend . '/control/portal/epc_integrations_hub';
	$guideUrl = epc_social_hub_url('guide', $siteKey === 'platform' ? 'platform' : null);

	$panelPath = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/control/portal/epc_social_media_hub_panel.php';
	$panelExists = is_file($panelPath);
	$panelBytes = $panelExists ? (int) filesize($panelPath) : 0;

	ob_start();
	$error = null;
	try {
		if (!$panelExists) {
			throw new RuntimeException('Panel missing: ' . $panelPath);
		}
		if (!function_exists('epc_social_render_guide_tab')) {
			require_once $panelPath;
		}
		epc_social_render_guide_tab($brand, $integrationsUrl, $guideUrl);
	} catch (Throwable $e) {
		$error = $e->getMessage();
	}
	$html = (string) ob_get_clean();

	$guideStart = stripos($html, 'epc-social-guide-step');
	if ($guideStart === false) {
		$guideStart = stripos($html, 'Guide URL');
	}
	$guideLen = $guideStart !== false ? (strlen($html) - $guideStart) : strlen($html);
	$hasSteps = stripos($html, 'Step 1') !== false;
	$hasPractices = stripos($html, 'GCC') !== false;

	echo json_encode(array(
		'ok' => $error === null && $panelExists && $guideLen > 200 && $hasSteps,
		'site_key' => $siteKey,
		'resolved_site_key' => $resolvedKey,
		'brand' => (string) ($brand['brand_name'] ?? ''),
		'panel_exists' => $panelExists,
		'panel_bytes' => $panelBytes,
		'guide_tab_html_length' => $guideLen,
		'has_steps' => $hasSteps,
		'has_gcc_practices' => $hasPractices,
		'error' => $error,
		'preview' => substr(preg_replace('/[\x00-\x08\x0B\x0C\x0E-\x1F]/', '', strip_tags($html)), 0, 160),
	), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT | JSON_INVALID_UTF8_SUBSTITUTE);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array(
		'ok' => false,
		'error' => $e->getMessage(),
		'site_key' => $siteKey,
	), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
}
