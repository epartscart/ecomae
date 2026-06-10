<?php
/**
 * Performance probe — TTFB-style timing breakdown for storefront + CP URLs.
 * GET ?token=epartscart-deploy-2026&host=www.epartscart.com
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

$t0 = microtime(true);
$marks = array(array('label' => 'bootstrap', 'ms' => 0));

function epc_perf_probe_mark(array &$marks, float $t0, string $label): void
{
	$marks[] = array('label' => $label, 'ms' => round((microtime(true) - $t0) * 1000, 2));
}

$host = trim((string) ($_GET['host'] ?? 'www.epartscart.com'));
$host = preg_replace('/[^a-z0-9.\-]/', '', strtolower($host));
if ($host === '') {
	$host = 'www.epartscart.com';
}

$phase = trim((string) ($_GET['phase'] ?? ''));
if ($phase === 'login') {
	$loginPaths = array(
		'cp_root' => '/cp/',
		'cp_control' => '/cp/control',
	);
	$loginCurl = array();
	foreach ($loginPaths as $key => $path) {
		$url = 'https://' . $host . $path;
		$ch = curl_init($url);
		curl_setopt_array($ch, array(
			CURLOPT_RETURNTRANSFER => true,
			CURLOPT_FOLLOWLOCATION => false,
			CURLOPT_CONNECTTIMEOUT => 10,
			CURLOPT_TIMEOUT => 120,
			CURLOPT_SSL_VERIFYPEER => false,
			CURLOPT_HEADER => true,
		));
		$raw = curl_exec($ch);
		$info = curl_getinfo($ch);
		curl_close($ch);
		$loginCurl[$key] = array(
			'url' => $url,
			'http' => (int) ($info['http_code'] ?? 0),
			'redirect' => (int) ($info['redirect_url'] ?? 0) !== 0 ? (string) $info['redirect_url'] : null,
			'ttfb_ms' => round(((float) ($info['starttransfer_time'] ?? 0)) * 1000, 1),
			'total_ms' => round(((float) ($info['total_time'] ?? 0)) * 1000, 1),
			'bytes' => is_string($raw) ? strlen($raw) : 0,
		);
	}
	epc_perf_probe_mark($marks, $t0, 'login_curl');
	echo json_encode(array(
		'ok' => true,
		'phase' => 'login',
		'host' => $host,
		'probe_ms' => round((microtime(true) - $t0) * 1000, 2),
		'timings' => $marks,
		'curl' => $loginCurl,
		'targets_ms' => array('cp_root' => 2000, 'cp_control' => 2000),
	), JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
	exit;
}

if (!empty($_GET['warm_widgets'])) {
	define('_ASTEXE_', 1);
	require_once __DIR__ . '/config.php';
	require_once __DIR__ . '/content/general_pages/epc_perf_cache.php';
	$lang = '/en';
	$paths = array($lang . '/umapi_catalog', $lang . '/available-brands');
	$warmed = array();
	foreach ($paths as $path) {
		$url = 'https://' . $host . $path;
		$ch = curl_init($url);
		curl_setopt_array($ch, array(
			CURLOPT_RETURNTRANSFER => true,
			CURLOPT_FOLLOWLOCATION => true,
			CURLOPT_CONNECTTIMEOUT => 5,
			CURLOPT_TIMEOUT => 30,
			CURLOPT_SSL_VERIFYPEER => false,
		));
		$html = curl_exec($ch);
		$code = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
		curl_close($ch);
		if (is_string($html) && $html !== '' && $code === 200) {
			$key = 'epart_front_widget:v1:' . $host . ':' . md5($path);
			epc_perf_cache_set($key, $html, 900);
			$warmed[] = array('path' => $path, 'bytes' => strlen($html));
		}
	}
	echo json_encode(array('ok' => true, 'host' => $host, 'warmed' => $warmed), JSON_PRETTY_PRINT);
	exit;
}

$paths = array(
	'storefront_home' => '/en/',
	'cp_login' => '/cp/',
	'cp_control' => '/cp/control',
	'cp_apai_discover' => '/cp/control/portal/epc_auto_price_engine?tab=discover',
	'spare_parts' => '/en/spare-parts/',
);

$curlTimings = array();
foreach ($paths as $key => $path) {
	$url = 'https://' . $host . $path;
	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_CONNECTTIMEOUT => 10,
		CURLOPT_TIMEOUT => 120,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_HEADER => false,
		CURLOPT_NOBODY => false,
	));
	$body = curl_exec($ch);
	$info = curl_getinfo($ch);
	curl_close($ch);
	$curlTimings[$key] = array(
		'url' => $url,
		'http' => (int) ($info['http_code'] ?? 0),
		'dns_ms' => round(((float) ($info['namelookup_time'] ?? 0)) * 1000, 1),
		'connect_ms' => round(((float) ($info['connect_time'] ?? 0)) * 1000, 1),
		'ttfb_ms' => round(((float) ($info['starttransfer_time'] ?? 0)) * 1000, 1),
		'total_ms' => round(((float) ($info['total_time'] ?? 0)) * 1000, 1),
		'bytes' => is_string($body) ? strlen($body) : 0,
	);
}

epc_perf_probe_mark($marks, $t0, 'curl_probes');

define('_ASTEXE_', 1);
@ini_set('display_errors', '0');

$local = array();
try {
	require_once __DIR__ . '/config.php';
	epc_perf_probe_mark($marks, $t0, 'config');

	require_once __DIR__ . '/content/general_pages/epc_perf_cache.php';
	epc_perf_probe_mark($marks, $t0, 'perf_cache');

	$cfg = new DP_Config();
	require_once __DIR__ . '/content/general_pages/epc_portal.php';
	epc_portal_apply_config($cfg);
	epc_perf_probe_mark($marks, $t0, 'portal_config');

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
	epc_perf_probe_mark($marks, $t0, 'pdo_connect');

	$menuRows = epc_cp_menu_cache($pdo);
	$local['cp_menu_groups'] = count($menuRows['groups'] ?? array());
	$local['cp_menu_items'] = count($menuRows['items'] ?? array());
	epc_perf_probe_mark($marks, $t0, 'cp_menu_cache');

	if (is_file(__DIR__ . '/content/general_pages/epc_electronicae_storefront.php')) {
		require_once __DIR__ . '/content/general_pages/epc_electronicae_storefront.php';
		$tiles = epc_electronicae_product_line_tiles($pdo, 'epartscart', 8);
		$local['product_line_tiles'] = count($tiles);
		epc_perf_probe_mark($marks, $t0, 'product_line_tiles');
	}
} catch (Throwable $e) {
	$local['error'] = $e->getMessage();
}

$totalMs = round((microtime(true) - $t0) * 1000, 2);
$targets = array(
	'storefront_home' => 2000,
	'cp_control' => 3000,
	'cp_apai_discover' => 3000,
);

$pass = array();
foreach ($targets as $k => $ms) {
	if (!isset($curlTimings[$k])) {
		continue;
	}
	$pass[$k] = ($curlTimings[$k]['ttfb_ms'] ?? 99999) < $ms;
}

echo json_encode(array(
	'ok' => true,
	'host' => $host,
	'probe_ms' => $totalMs,
	'timings' => $marks,
	'curl' => $curlTimings,
	'local' => $local,
	'targets_ms' => $targets,
	'pass' => $pass,
	'hints' => array(
		'opcache' => 'Enable opcache.enable=1, opcache.validate_timestamps=0 in production',
		'gzip' => 'nginx gzip on; gzip_types text/css application/javascript application/json;',
		'static_cache' => 'Cache-Control max-age=604800 on CP CSS/JS proxy PHP files',
	),
), JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
