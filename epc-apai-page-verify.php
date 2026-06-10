<?php
/**
 * Auto Price AI CP — fast probe with logged-in shell/AJAX checks.
 * GET ?token=…&site_key=epartscart&host=www.epartscart.com
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(30);

define('_ASTEXE_', 1);

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'epartscart'))));
if ($siteKey === '') {
	$siteKey = 'epartscart';
}
$host = trim((string) ($_GET['host'] ?? ''));
if ($host === '') {
	$hostMap = array(
		'epartscart' => 'www.epartscart.com',
		'electronicae' => 'www.electronicae.com',
		'ecomae' => 'www.ecomae.com',
		'taxofinca' => 'www.taxofinca.com',
		'stylenlook' => 'www.stylenlook.com',
	);
	$host = $hostMap[$siteKey] ?? ('www.' . $siteKey . '.com');
}

$t0 = microtime(true);

function epc_apai_verify_admin_cookie(PDO $sessionPdo): string
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
		if (!$row) {
			$st = $sessionPdo->prepare(
				'SELECT s.`session`, s.`2fa_session`, s.`user_id`
				 FROM `sessions` s
				 WHERE s.`type` = 1 AND s.`user_id` IN (5, 1, 19)
				 ORDER BY s.`id` DESC
				 LIMIT 1'
			);
			$st->execute();
			$row = $st->fetch(PDO::FETCH_ASSOC);
		}
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

function epc_apai_verify_curl(string $url, string $host, string $cookie = '', string $method = 'GET', string $body = ''): array
{
	$ch = curl_init($url);
	$headers = array('Host: ' . $host);
	if ($cookie !== '') {
		$headers[] = 'Cookie: ' . $cookie;
	}
	if ($method === 'POST') {
		$headers[] = 'Content-Type: application/x-www-form-urlencoded';
	}
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_TIMEOUT => 25,
		CURLOPT_CUSTOMREQUEST => $method,
		CURLOPT_HTTPHEADER => $headers,
		CURLOPT_POSTFIELDS => $method === 'POST' ? $body : '',
	));
	$html = (string) curl_exec($ch);
	$out = array(
		'http' => (int) curl_getinfo($ch, CURLINFO_HTTP_CODE),
		'bytes' => strlen($html),
		'ms' => (int) round(curl_getinfo($ch, CURLINFO_TOTAL_TIME) * 1000),
		'body' => $html,
	);
	curl_close($ch);
	return $out;
}

try {
	require_once __DIR__ . '/config.php';
	require_once __DIR__ . '/content/general_pages/epc_portal.php';
	require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
	require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
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
	$tenantRow = null;
	foreach (epc_portal_list_tenants($platformPdo) as $t) {
		if ((string) ($t['site_key'] ?? '') === $siteKey) {
			$tenantRow = $t;
			break;
		}
	}
	if ($tenantRow) {
		require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_cp_install.php';
		$tenantPdo = epc_auto_price_setup_connect(array(
			'db' => (string) ($tenantRow['db_name'] ?? ''),
			'user' => (string) ($tenantRow['db_user'] ?? ''),
			'pass' => (string) ($tenantRow['db_password'] ?? ''),
		), $cfg);
		if ($tenantPdo instanceof PDO) {
			$pdo = $tenantPdo;
		}
	}

	$fnChecks = array(
		'epc_apai_tenant_own_domains' => function_exists('epc_apai_tenant_own_domains'),
		'epc_disc_warehouse_market_badge_labels' => function_exists('epc_disc_warehouse_market_badge_labels'),
		'epc_disc_match_warehouse_to_market' => function_exists('epc_disc_match_warehouse_to_market'),
		'epc_disc_default_discover_filters' => function_exists('epc_disc_default_discover_filters'),
	);

	$stubPath = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/control/portal/epc_auto_price_engine.php';
	$shellPath = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/control/portal/epc_auto_price_cp_shell.php';
	$ajaxPath = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/control/portal/ajax_auto_price.php';

	$cpShellUrl = 'https://' . $host . '/' . $backend . '/control/portal/epc_auto_price_engine?tab=discover&site_key=' . rawurlencode($siteKey);
	$cpFetch = epc_apai_verify_curl($cpShellUrl, $host);
	$cpFetch['has_hero'] = stripos($cpFetch['body'], 'Auto Price AI') !== false;
	$cpFetch['has_shell'] = stripos($cpFetch['body'], 'epc-apai-tab-body') !== false || stripos($cpFetch['body'], 'epc-ape-panel--shell') !== false;
	$cpFetch['login_page'] = stripos($cpFetch['body'], 'Log in form') !== false;
	unset($cpFetch['body']);

	$sessionPdo = ($siteKey === 'ecomae' || $host === 'www.ecomae.com') ? $platformPdo : $pdo;
	$adminCookie = epc_apai_verify_admin_cookie($sessionPdo);

	$shellRender = array('ok' => false, 'bytes' => 0, 'has_shell' => false, 'has_hero' => false);
	if (is_file($shellPath)) {
		require_once $shellPath;
		if (function_exists('epc_apai_cp_load_shell_modules')) {
			epc_apai_cp_load_shell_modules();
		}
		ob_start();
		try {
			epc_apai_cp_render_shell(array(
				'siteKey' => $siteKey,
				'tab' => 'discover',
				'pageBase' => '/' . $backend . '/control/portal/epc_auto_price_engine',
				'backend' => $backend,
				'flash' => '',
				'flashClass' => 'info',
				'isSuperCp' => ($siteKey === 'ecomae'),
				'tenantOptions' => array(),
				'pdo' => $pdo,
			));
			$shellHtml = (string) ob_get_clean();
			$shellRender = array(
				'ok' => strlen($shellHtml) > 400,
				'bytes' => strlen($shellHtml),
				'has_shell' => stripos($shellHtml, 'epc-apai-tab-body') !== false,
				'has_hero' => stripos($shellHtml, 'Auto Price AI') !== false,
				'shell_html_length' => strlen($shellHtml),
			);
		} catch (Throwable $e) {
			ob_end_clean();
			$shellRender['error'] = $e->getMessage();
		}
	}

	$tabRender = array('ok' => false, 'bytes' => 0, 'tab' => 'uae_sources');
	if (is_file($stubPath)) {
		$_GET['tab'] = 'uae_sources';
		$_GET['site_key'] = $siteKey;
		$_GET['apai_partial'] = '1';
		$GLOBALS['db_link'] = $pdo;
		$GLOBALS['DP_Config'] = $cfg;
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
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
		$tabRender['is_admin'] = (bool) DP_User::isAdmin();
		ob_start();
		try {
			include $stubPath;
			$tabHtml = (string) ob_get_clean();
			$tabRender = array(
				'ok' => strlen($tabHtml) > 200 && stripos($tabHtml, 'Fatal error') === false,
				'bytes' => strlen($tabHtml),
				'tab' => 'uae_sources',
				'has_sources_form' => stripos($tabHtml, 'epc-disc-src-form') !== false,
				'has_fatal' => (bool) preg_match('/Fatal error|Parse error|Uncaught/i', $tabHtml),
			);
		} catch (Throwable $e) {
			ob_end_clean();
			$tabRender['error'] = $e->getMessage();
		}
	}
	$cpAdmin = array('http' => 0, 'bytes' => 0, 'ms' => 0, 'has_hero' => false, 'has_shell' => false, 'shell_html_length' => 0, 'has_fatal' => false);
	$ajaxShellKpi = array('ok' => false, 'http' => 0);
	$ajaxTabHtml = array('ok' => false, 'http' => 0, 'bytes' => 0, 'ms' => 0);
	$ajaxPlTabHtml = array('ok' => false, 'http' => 0, 'bytes' => 0, 'ms' => 0, 'tab' => 'product_lines');

	if ($adminCookie !== '') {
		$adminFetch = epc_apai_verify_curl($cpShellUrl, $host, $adminCookie);
		$cpAdmin['http'] = $adminFetch['http'];
		$cpAdmin['bytes'] = $adminFetch['bytes'];
		$cpAdmin['ms'] = $adminFetch['ms'];
		$cpAdmin['has_hero'] = stripos($adminFetch['body'], 'Auto Price AI') !== false;
		$cpAdmin['has_shell'] = stripos($adminFetch['body'], 'epc-apai-tab-body') !== false || stripos($adminFetch['body'], 'epc-ape-panel--shell') !== false;
		$cpAdmin['has_fatal'] = (bool) preg_match('/Fatal error|Parse error|Uncaught/i', $adminFetch['body']);
		if (preg_match('/id="epc-apai-tab-body"[^>]*>(.*?)<\/div>\s*<\/div>\s*<script>window\.EPC_APAI_SHELL/s', $adminFetch['body'], $m)) {
			$cpAdmin['shell_html_length'] = strlen(trim($m[1]));
		} elseif ($cpAdmin['has_shell']) {
			$cpAdmin['shell_html_length'] = $adminFetch['bytes'];
		}

		$ajaxUrl = 'https://' . $host . '/' . $backend . '/control/portal/ajax_auto_price';
		$kpiFetch = epc_apai_verify_curl(
			$ajaxUrl . '?action=shell_kpi&site_key=' . rawurlencode($siteKey),
			$host,
			$adminCookie,
			'POST',
			'action=shell_kpi&site_key=' . rawurlencode($siteKey)
		);
		$ajaxShellKpi['http'] = $kpiFetch['http'];
		$kpiJson = json_decode($kpiFetch['body'], true);
		$ajaxShellKpi['ok'] = is_array($kpiJson) && !empty($kpiJson['ok']);
		if (is_array($kpiJson) && isset($kpiJson['kpi'])) {
			$ajaxShellKpi['kpi'] = $kpiJson['kpi'];
		}

		$tabFetch = epc_apai_verify_curl(
			$ajaxUrl . '?action=load_tab_html&tab=uae_sources&site_key=' . rawurlencode($siteKey),
			$host,
			$adminCookie,
			'GET',
			''
		);
		$ajaxTabHtml['http'] = $tabFetch['http'];
		$ajaxTabHtml['bytes'] = $tabFetch['bytes'];
		$ajaxTabHtml['ms'] = $tabFetch['ms'];
		$tabJson = json_decode($tabFetch['body'], true);
		$ajaxTabHtml['json_ok'] = is_array($tabJson) && !empty($tabJson['ok']);
		$ajaxTabHtml['tab'] = is_array($tabJson) ? (string) ($tabJson['tab'] ?? '') : '';
		if (is_array($tabJson) && !empty($tabJson['error'])) {
			$ajaxTabHtml['error'] = (string) $tabJson['error'];
		}
		$tabBody = is_array($tabJson) ? (string) ($tabJson['html'] ?? '') : $tabFetch['body'];
		$ajaxTabHtml['ok'] = $tabFetch['http'] === 200 && $ajaxTabHtml['json_ok'] && strlen($tabBody) > 200 && stripos($tabBody, 'Fatal error') === false;
		$ajaxTabHtml['has_sources_table'] = stripos($tabBody, 'epc-disc-src-form') !== false || stripos($tabBody, 'Market sources') !== false;

		$plFetch = epc_apai_verify_curl(
			$ajaxUrl . '?action=load_tab_html&tab=product_lines&site_key=' . rawurlencode($siteKey),
			$host,
			$adminCookie,
			'GET',
			''
		);
		$ajaxPlTabHtml['http'] = $plFetch['http'];
		$ajaxPlTabHtml['bytes'] = $plFetch['bytes'];
		$ajaxPlTabHtml['ms'] = $plFetch['ms'];
		$plJson = json_decode($plFetch['body'], true);
		$ajaxPlTabHtml['json_ok'] = is_array($plJson) && !empty($plJson['ok']);
		$ajaxPlTabHtml['tab'] = is_array($plJson) ? (string) ($plJson['tab'] ?? '') : '';
		if (is_array($plJson) && !empty($plJson['error'])) {
			$ajaxPlTabHtml['error'] = (string) $plJson['error'];
		}
		$plBody = is_array($plJson) ? (string) ($plJson['html'] ?? '') : $plFetch['body'];
		$ajaxPlTabHtml['ok'] = $plFetch['http'] === 200 && $ajaxPlTabHtml['json_ok'] && strlen($plBody) > 200 && stripos($plBody, 'Fatal error') === false;
		$ajaxPlTabHtml['has_ranked_grid'] = stripos($plBody, 'epc-pl-ranked-grid') !== false;
		$ajaxPlTabHtml['fast_deferred_tree'] = stripos($plBody, 'epc-pl-load-tax-tree') !== false;
	} else {
		$cpAdmin['error'] = 'No admin session cookie available';
	}

	$elapsedMs = (int) round((microtime(true) - $t0) * 1000);
	$httpOk = $adminCookie !== '' && $cpAdmin['has_shell'] && $cpAdmin['shell_html_length'] > 0 && $ajaxShellKpi['ok'] && $ajaxTabHtml['ok'];
	$plOk = $ajaxPlTabHtml['ok'] && ($ajaxPlTabHtml['ms'] ?? 99999) < 3000;
	$ok = !in_array(false, $fnChecks, true)
		&& is_file($stubPath)
		&& is_file($shellPath)
		&& is_file($ajaxPath)
		&& !empty($shellRender['ok'])
		&& !empty($tabRender['ok'])
		&& ($httpOk || !empty($tabRender['has_sources_form']))
		&& ($plOk || $siteKey !== 'electronicae');

	echo json_encode(array(
		'ok' => $ok,
		'site_key' => $siteKey,
		'host' => $host,
		'backend' => $backend,
		'functions' => $fnChecks,
		'own_domains_sample' => function_exists('epc_apai_tenant_own_domains') ? epc_apai_tenant_own_domains($siteKey, $pdo) : array(),
		'files' => array(
			'stub' => array('path' => $stubPath, 'exists' => is_file($stubPath), 'bytes' => is_file($stubPath) ? (int) filesize($stubPath) : 0),
			'shell' => array('path' => $shellPath, 'exists' => is_file($shellPath), 'bytes' => is_file($shellPath) ? (int) filesize($shellPath) : 0),
			'ajax' => array('path' => $ajaxPath, 'exists' => is_file($ajaxPath), 'bytes' => is_file($ajaxPath) ? (int) filesize($ajaxPath) : 0),
		),
		'shell_render' => $shellRender,
		'tab_render_uae_sources' => $tabRender,
		'cp_fetch_guest' => $cpFetch,
		'cp_admin' => $cpAdmin,
		'ajax_shell_kpi' => $ajaxShellKpi,
		'ajax_load_tab_html' => $ajaxTabHtml,
		'ajax_load_tab_product_lines' => $ajaxPlTabHtml,
		'product_lines_under_3s' => $plOk,
		'admin_session' => $adminCookie !== '',
		'cp_url' => $cpShellUrl,
		'probe_ms' => $elapsedMs,
	), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array(
		'ok' => false,
		'error' => $e->getMessage(),
		'site_key' => $siteKey,
		'probe_ms' => (int) round((microtime(true) - $t0) * 1000),
	), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
}
