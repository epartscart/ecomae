<?php
/**
 * Auto Price AI — single tab partial render timing (server-side).
 * GET ?token=…&site_key=epartscart&tab=discover
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(45);

define('_ASTEXE_', 1);

function epc_apai_timing_admin_cookie(PDO $sessionPdo): string
{
	try {
		$st = $sessionPdo->prepare(
			'SELECT s.`session`, s.`2fa_session`, s.`user_id`
			 FROM `sessions` s
			 WHERE s.`type` = 1
			 ORDER BY s.`last_activiti_time` DESC
			 LIMIT 1'
		);
		$st->execute();
		$row = $st->fetch(PDO::FETCH_ASSOC);
	} catch (Throwable $e) {
		return '';
	}
	if (!$row || empty($row['session'])) {
		return '';
	}
	$cookie = 'admin_session=' . rawurlencode((string) $row['session']) . '; admin_u_id=' . (int) ($row['user_id'] ?? 0);
	if (!empty($row['2fa_session'])) {
		$cookie .= '; 2fa=' . rawurlencode((string) $row['2fa_session']);
	}
	return $cookie;
}

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'epartscart'))));
$tab = preg_replace('/[^a-z_]/', '', strtolower(trim((string) ($_GET['tab'] ?? 'discover'))));
if ($tab === '') {
	$tab = 'discover';
}
$t0 = microtime(true);

try {
	require_once __DIR__ . '/config.php';
	require_once __DIR__ . '/content/general_pages/epc_portal.php';
	require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
	require_once __DIR__ . '/content/users/dp_user.php';

	$cfg = new DP_Config();
	epc_portal_apply_config($cfg);
	$backend = trim((string) ($cfg->backend_dir ?? 'cp'), '/');
	$platformPdo = epc_portal_platform_pdo();
	$pdo = $platformPdo;
	foreach (epc_portal_list_tenants($platformPdo) as $t) {
		if ((string) ($t['site_key'] ?? '') === $siteKey) {
			require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_cp_install.php';
			$tenantPdo = epc_auto_price_setup_connect(array(
				'db' => (string) ($t['db_name'] ?? ''),
				'user' => (string) ($t['db_user'] ?? ''),
				'pass' => (string) ($t['db_password'] ?? ''),
			), $cfg);
			if ($tenantPdo instanceof PDO) {
				$pdo = $tenantPdo;
			}
			break;
		}
	}

	$stubPath = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/control/portal/epc_auto_price_engine.php';
	if (!is_file($stubPath)) {
		throw new RuntimeException('Engine stub missing');
	}

	$GLOBALS['db_link'] = $pdo;
	$GLOBALS['DP_Config'] = $cfg;

	$hostMap = array('epartscart' => 'www.epartscart.com', 'electronicae' => 'www.electronicae.com');
	$host = $hostMap[$siteKey] ?? ('www.' . $siteKey . '.com');
	$sessionPdo = ($siteKey === 'ecomae' || $host === 'www.ecomae.com') ? $platformPdo : $pdo;
	$adminCookie = epc_apai_timing_admin_cookie($sessionPdo);
	if ($adminCookie !== '') {
		foreach (explode(';', $adminCookie) as $part) {
			$part = trim($part);
			if ($part === '' || strpos($part, '=') === false) {
				continue;
			}
			list($ck, $cv) = explode('=', $part, 2);
			$_COOKIE[trim($ck)] = rawurldecode(trim($cv));
		}
	}

	$_GET = array('tab' => $tab, 'site_key' => $siteKey, 'apai_partial' => '1');
	$_POST = array();
	$_SERVER['REQUEST_METHOD'] = 'GET';

	ob_start();
	include $stubPath;
	$html = (string) ob_get_clean();

	echo json_encode(array(
		'ok' => strlen($html) > 200 && stripos($html, 'Fatal error') === false,
		'site_key' => $siteKey,
		'tab' => $tab,
		'bytes' => strlen($html),
		'ms' => (int) round((microtime(true) - $t0) * 1000),
	), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array(
		'ok' => false,
		'error' => $e->getMessage(),
		'site_key' => $siteKey,
		'tab' => $tab,
		'ms' => (int) round((microtime(true) - $t0) * 1000),
	), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
}
