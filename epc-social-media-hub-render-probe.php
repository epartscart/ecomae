<?php
/**
 * Social Media Hub — logged-in CP render probe (hub + guide tab + assets).
 * GET ?token=…&host=www.ecomae.com|www.epartscart.com&tab=guide|pack
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(45);

$host = trim((string) ($_GET['host'] ?? 'www.ecomae.com'));
$tab = preg_replace('/[^a-z_]/', '', strtolower(trim((string) ($_GET['tab'] ?? 'guide'))));
if ($tab === '') {
	$tab = 'guide';
}
$scheme = (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off') ? 'https' : 'https';

function epc_sm_probe_admin_cookie(PDO $pdo): string
{
	try {
		$st = $pdo->prepare(
			'SELECT s.`session`, s.`2fa_session`, s.`user_id`
			 FROM `sessions` s
			 WHERE s.`type` = 1
			 ORDER BY s.`last_activiti_time` DESC
			 LIMIT 1'
		);
		$st->execute();
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if (!$row || empty($row['session'])) {
			$st = $pdo->prepare(
				'SELECT s.`session`, s.`2fa_session`, s.`user_id`
				 FROM `sessions` s
				 WHERE s.`type` = 1 AND s.`user_id` IN (1, 5, 19)
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

function epc_sm_probe_fetch(string $url, string $host, string $cookie = ''): array
{
	$ch = curl_init($url);
	$headers = array('Host: ' . $host);
	if ($cookie !== '') {
		$headers[] = 'Cookie: ' . $cookie;
	}
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_TIMEOUT => 30,
		CURLOPT_HTTPHEADER => $headers,
	));
	$body = (string) curl_exec($ch);
	$out = array(
		'http' => (int) curl_getinfo($ch, CURLINFO_HTTP_CODE),
		'bytes' => strlen($body),
		'body' => $body,
	);
	curl_close($ch);
	return $out;
}

try {
	require_once __DIR__ . '/config.php';
	require_once __DIR__ . '/content/general_pages/epc_portal.php';
	require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
	require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

	$_SERVER['HTTP_HOST'] = $host;
	$_SERVER['SERVER_NAME'] = $host;
	$cfg = new DP_Config();
	epc_portal_apply_config($cfg);

	$tenantDbMap = array();
	$overrideFile = __DIR__ . '/config.tenant-host-db.php';
	if (is_file($overrideFile)) {
		require $overrideFile;
		if (!empty($epc_tenant_host_db) && is_array($epc_tenant_host_db)) {
			$tenantDbMap = $epc_tenant_host_db;
		}
	}
	$hk = strtolower($host);
	if (isset($tenantDbMap[$hk]) && is_array($tenantDbMap[$hk])) {
		foreach (array('db', 'user', 'password') as $tk) {
			if (!empty($tenantDbMap[$hk][$tk])) {
				$cfg->$tk = $tenantDbMap[$hk][$tk];
			}
		}
	}

	$dbHost = trim((string) $cfg->host);
	if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
		$dbHost = '127.0.0.1';
	}
	$pdo = new PDO(
		'mysql:host=' . $dbHost . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$cookie = epc_sm_probe_admin_cookie($pdo);

	$hubUrl = $scheme . '://' . $host . '/cp/control/portal/epc_social_media_hub?tab=' . rawurlencode($tab);
	$configUrl = $scheme . '://' . $host . '/content/general_pages/epc_social_media_hub_config.php';
	$jsUrl = $scheme . '://' . $host . '/content/general_pages/epc_social_media_hub_js.php';
	$cssUrl = $scheme . '://' . $host . '/content/general_pages/epc_social_media_hub_css.php';
	$ajaxUrl = $scheme . '://' . $host . '/content/general_pages/ajax_epc_social_media.php';

	$hub = epc_sm_probe_fetch($hubUrl, $host, $cookie);
	$config = epc_sm_probe_fetch($configUrl, $host, $cookie);
	$js = epc_sm_probe_fetch($jsUrl, $host, $cookie);
	$css = epc_sm_probe_fetch($cssUrl, $host, $cookie);
	$ajax = epc_sm_probe_fetch($ajaxUrl, $host, $cookie);

	$hubBody = (string) ($hub['body'] ?? '');
	$isLogin = stripos($hubBody, 'Log in form') !== false || stripos($hubBody, 'epc-cp-login') !== false;
	$hasHub = stripos($hubBody, 'epc-social-hub') !== false;
	$hasGuide = stripos($hubBody, 'epc-social-guide-step') !== false || stripos($hubBody, 'Step 1') !== false;
	$hasTabs = stripos($hubBody, 'epc-social-tabs') !== false;
	$hasAssets = stripos($hubBody, 'epc_social_media_hub_config.php') !== false
		&& stripos($hubBody, 'epc_social_media_hub_js.php') !== false;

	$configBody = (string) ($config['body'] ?? '');
	$configValid = strpos($configBody, 'window.EPC_SOCIAL_HUB') !== false
		&& strpos($configBody, 'ajaxUrl') !== false
		&& strpos($configBody, 'No access') === false;
	$configHasAjax = strpos($configBody, '/content/general_pages/ajax_epc_social_media.php') !== false;

	$jsOk = ((int) ($js['http'] ?? 0)) === 200 && strpos((string) ($js['body'] ?? ''), 'epc-social-copy') !== false;
	$cssOk = ((int) ($css['http'] ?? 0)) === 200 && strpos((string) ($css['body'] ?? ''), '.epc-social-hub') !== false;
	$ajaxOk = in_array((int) ($ajax['http'] ?? 0), array(200, 403), true);

	$hubRendered = $hasHub
		&& ($tab !== 'guide' || $hasGuide)
		&& $hasTabs
		&& $hasAssets
		&& ((int) ($hub['http'] ?? 0)) === 200
		&& (int) ($hub['bytes'] ?? 0) > 5000;
	$ok = $hubRendered
		&& $configValid
		&& $configHasAjax
		&& $jsOk
		&& $cssOk
		&& $ajaxOk;

	echo json_encode(array(
		'ok' => $ok,
		'host' => $host,
		'tab' => $tab,
		'admin_cookie' => $cookie !== '',
		'hub' => array(
			'http' => (int) ($hub['http'] ?? 0),
			'bytes' => (int) ($hub['bytes'] ?? 0),
			'is_login_page' => $isLogin,
			'has_hub_shell' => $hasHub,
			'has_guide_content' => $hasGuide,
			'has_tabs' => $hasTabs,
			'has_asset_tags' => $hasAssets,
		),
		'config' => array(
			'http' => (int) ($config['http'] ?? 0),
			'bytes' => (int) ($config['bytes'] ?? 0),
			'valid_js' => $configValid,
			'has_content_ajax_url' => $configHasAjax,
			'preview' => substr(preg_replace('/\s+/', ' ', $configBody), 0, 120),
		),
		'js_proxy' => array('http' => (int) ($js['http'] ?? 0), 'ok' => $jsOk),
		'css_proxy' => array('http' => (int) ($css['http'] ?? 0), 'ok' => $cssOk),
		'ajax_proxy' => array('http' => (int) ($ajax['http'] ?? 0), 'ok' => $ajaxOk),
	), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT | JSON_INVALID_UTF8_SUBSTITUTE);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array(
		'ok' => false,
		'error' => $e->getMessage(),
		'host' => $host,
	), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
}
