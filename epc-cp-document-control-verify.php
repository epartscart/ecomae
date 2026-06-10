<?php
/**
 * Document Control — route, files, tenant DB, and smoke render probe.
 * GET https://www.epartscart.com/epc-cp-document-control-verify.php?token=epartscart-deploy-2026
 * Optional: &site_key=epartscart&apply=1 (re-run installer on that tenant DB)
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(120);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/shop/document_control/epc_document_control_cp_install.php';
require_once __DIR__ . '/content/shop/document_control/epc_document_control_helpers.php';

$onlySite = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? ''))));
$apply = !empty($_GET['apply']) && (string) $_GET['apply'] === '1';

$tenants = array(
	'epartscart' => 'www.epartscart.com',
	'electronicae' => 'www.electronicae.com',
	'taxofinca' => 'www.taxofinca.com',
	'stylenlook' => 'www.stylenlook.com',
	'thejewellerytrend' => 'www.thejewellerytrend.com',
	'ecomae' => 'www.ecomae.com',
);

function epc_dc_verify_probe_host(string $siteKey, string $host, bool $apply): array
{
	$_SERVER['HTTP_HOST'] = $host;
	$_SERVER['SERVER_NAME'] = $host;
	unset($GLOBALS['epc_portal_config_applied'], $GLOBALS['epc_db_link'], $GLOBALS['db_link']);

	$cfg = new DP_Config();
	epc_portal_apply_config($cfg);
	$backend = trim((string) ($cfg->backend_dir ?? 'cp'), '/');
	if ($backend === '') {
		$backend = 'cp';
	}
	$docRoot = $_SERVER['DOCUMENT_ROOT'] ?? '';

	$out = array(
		'site_key' => $siteKey,
		'host' => $host,
		'cp_db' => (string) ($cfg->db ?? ''),
		'db_ok' => false,
		'route' => null,
		'probe' => null,
		'menu_visible' => null,
		'dashboard' => null,
		'php_main_exists' => false,
		'php_path_resolved' => null,
		'apply' => null,
		'public_url' => epc_document_control_cp_public_url($host, $backend),
		'error' => null,
	);

	try {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$out['db_ok'] = true;
		$out['probe'] = epc_document_control_cp_probe($pdo, $docRoot, $backend);
		$row = $out['probe']['content']['shop/document_control/document_control'] ?? null;
		$out['route'] = $row ? array(
			'id' => (int) ($row['id'] ?? 0),
			'published' => (int) ($row['published_flag'] ?? 0),
			'content' => (string) ($row['content'] ?? ''),
		) : null;

		if ($row && !empty($row['content'])) {
			$phpRel = str_replace('<backend_dir>', $backend, (string) $row['content']);
			$phpPath = rtrim($docRoot, '/\\') . $phpRel;
			$out['php_path_resolved'] = $phpPath;
			$out['php_main_exists'] = is_file($phpPath);
		}

		if ($apply && function_exists('epc_document_control_cp_install')) {
			$out['apply'] = epc_document_control_cp_install($pdo, $backend);
		}

		if (function_exists('epc_portal_cp_item_visible')) {
			$out['menu_visible'] = epc_portal_cp_item_visible('/' . $backend . '/shop/document_control/document_control');
		}

		if (function_exists('epc_dc_dashboard')) {
			$out['dashboard'] = epc_dc_dashboard($pdo);
		}
	} catch (Throwable $e) {
		$out['error'] = $e->getMessage();
	}

	$platformPdo = function_exists('epc_portal_platform_pdo') ? epc_portal_platform_pdo() : null;
	if ($platformPdo instanceof PDO && $siteKey !== 'ecomae') {
		epc_portal_db_ensure($platformPdo);
		$st = $platformPdo->prepare('SELECT `db_name` FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1');
		$st->execute(array($siteKey));
		$registryDb = (string) ($st->fetchColumn() ?: '');
		$out['registry_db'] = $registryDb;
		$out['registry_db_match'] = ($registryDb === '' || $registryDb === $out['cp_db']);
	}

	$routeOk = is_array($out['route']) && (int) ($out['route']['published'] ?? 0) === 1;
	$filesOk = !empty($out['probe']['files']['main']) && !empty($out['probe']['files']['main_page']);
	$out['status'] = ($out['db_ok'] && $routeOk && $filesOk && $out['error'] === null) ? 'ok' : 'check';

	if ($out['db_ok'] && $out['php_main_exists'] && is_file($out['php_path_resolved'] ?? '')) {
		$mainPath = dirname((string) $out['php_path_resolved']) . '/document_control_main.php';
		if (!function_exists('translate_str_by_id')) {
			function translate_str_by_id($id) { return (string) $id; }
		}
		$GLOBALS['DP_Config'] = $cfg;
		$GLOBALS['db_link'] = $pdo;
		$db_link = $pdo;
		$DP_Config = $cfg;
		$user_session = array('csrf_guard_key' => 'verify');
		ob_start();
		try {
			include $mainPath;
			$html = (string) ob_get_clean();
			$out['render_smoke'] = array(
				'include_ok' => true,
				'html_length' => strlen($html),
				'has_hero' => stripos($html, 'epc-dc-hero') !== false,
				'has_tabs' => stripos($html, 'epc-dc-nav') !== false,
				'inline_style_leak' => stripos($html, '<style>') !== false || stripos($html, '.epc-dc-hero {') !== false,
				'inline_script_leak' => stripos($html, '<script') !== false,
				'php_fatal_in_html' => (bool) preg_match('/\b(Fatal error|Parse error|Uncaught)\b/i', $html),
				'ok' => stripos($html, 'epc-dc-hero') !== false
					&& stripos($html, '<style>') === false
					&& stripos($html, '<script') === false
					&& !preg_match('/\b(Fatal error|Parse error|Uncaught)\b/i', $html),
			);
			if (!$out['render_smoke']['ok']) {
				$out['status'] = 'check';
			}
		} catch (Throwable $e) {
			if (ob_get_level() > 0) {
				ob_end_clean();
			}
			$out['render_smoke'] = array(
				'include_ok' => false,
				'error' => $e->getMessage(),
				'ok' => false,
			);
			$out['status'] = 'check';
		}
	}

	return $out;
}

$results = array();
foreach ($tenants as $siteKey => $host) {
	if ($onlySite !== '' && $onlySite !== $siteKey) {
		continue;
	}
	$results[$siteKey] = epc_dc_verify_probe_host($siteKey, $host, $apply && ($onlySite === '' || $onlySite === $siteKey));
}

echo json_encode(
	array(
		'probe' => 'epc-cp-document-control-verify',
		'ts' => gmdate('c'),
		'apply' => $apply,
		'tenants' => $results,
	),
	JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES
);
