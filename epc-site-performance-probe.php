<?php
/**
 * Site performance benchmark — storefront pages, APIs, static assets.
 * GET: token=epartscart-deploy-2026&key=<tech_key>&save=1
 */
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
$cfg = new DP_Config();
if ((string)($_GET['key'] ?? '') !== $cfg->tech_key) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Invalid key')));
}

function epc_perf_grade($ms, $bytes)
{
	if ($ms <= 1500 && $bytes <= 400000) {
		return 'A';
	}
	if ($ms <= 3000 && $bytes <= 600000) {
		return 'B';
	}
	if ($ms <= 6000 && $bytes <= 900000) {
		return 'C';
	}
	return 'D';
}

function epc_perf_fetch($url, $host = '')
{
	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_TIMEOUT => 60,
		CURLOPT_CONNECTTIMEOUT => 15,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_HEADER => true,
		CURLOPT_ENCODING => '',
	));
	if ($host !== '') {
		curl_setopt($ch, CURLOPT_HTTPHEADER, array('Accept-Encoding: gzip, deflate, br'));
	}
	$started = microtime(true);
	$raw = curl_exec($ch);
	$ms = (int)round((microtime(true) - $started) * 1000);
	$info = curl_getinfo($ch);
	curl_close($ch);
	$headerSize = (int)($info['header_size'] ?? 0);
	$headers = substr((string)$raw, 0, $headerSize);
	$body = substr((string)$raw, $headerSize);
	$encoding = '';
	if (preg_match('/content-encoding:\s*([^\r\n]+)/i', $headers, $m)) {
		$encoding = trim($m[1]);
	}
	$cache = '';
	if (preg_match('/cache-control:\s*([^\r\n]+)/i', $headers, $m)) {
		$cache = trim($m[1]);
	}
	return array(
		'url' => $url,
		'http' => (int)($info['http_code'] ?? 0),
		'time_ms' => $ms,
		'bytes' => strlen($body),
		'content_encoding' => $encoding,
		'cache_control' => $cache,
		'grade' => epc_perf_grade($ms, strlen($body)),
	);
}

$base = rtrim($cfg->domain_path, '/');
$targets = array(
	array('group' => 'storefront', 'url' => $base . '/en/'),
	array('group' => 'storefront', 'url' => $base . '/en/umapi_catalog'),
	array('group' => 'storefront', 'url' => $base . '/en/vehicle-catalog'),
	array('group' => 'storefront', 'url' => $base . '/en/parts/555/SB2162'),
	array('group' => 'storefront', 'url' => $base . '/en/shop/cart'),
	array('group' => 'storefront', 'url' => $base . '/en/available-brands'),
	array('group' => 'api', 'url' => $base . '/api/umapi_proxy.php?action=manufacturers&section=passenger&language=en&region=WWW'),
	array('group' => 'api', 'url' => $base . '/api/umapi_proxy.php?action=status'),
	array('group' => 'api', 'url' => $base . '/api/crossbase_status.php'),
	array('group' => 'static', 'url' => $base . '/templates/nero/assets/css/style_all.css'),
	array('group' => 'static', 'url' => $base . '/templates/nero/assets/js/vendors_main.js'),
);

$results = array();
$grades = array('A' => 0, 'B' => 0, 'C' => 0, 'D' => 0);
foreach ($targets as $t) {
	$row = epc_perf_fetch($t['url']);
	$row['group'] = $t['group'];
	$row['label'] = preg_replace('#^' . preg_quote($base, '#') . '#', '', $row['url']);
	if ($row['label'] === '') {
		$row['label'] = '/';
	}
	$results[] = $row;
	if (isset($grades[$row['grade']])) {
		$grades[$row['grade']]++;
	}
}

$storefront = array_values(array_filter($results, function ($r) { return $r['group'] === 'storefront'; }));
$apis = array_values(array_filter($results, function ($r) { return $r['group'] === 'api'; }));
$avgStoreMs = count($storefront) ? (int)round(array_sum(array_column($storefront, 'time_ms')) / count($storefront)) : 0;
$avgApiMs = count($apis) ? (int)round(array_sum(array_column($apis, 'time_ms')) / count($apis)) : 0;

$overall = 'A';
if ($grades['D'] > 0 || $avgStoreMs > 6000) {
	$overall = 'C';
} elseif ($grades['C'] > 2 || $avgStoreMs > 3500) {
	$overall = 'B';
} elseif ($avgStoreMs > 2500) {
	$overall = 'B';
}

$report = array(
	'ok' => true,
	'tested_at' => date('c'),
	'overall_grade' => $overall,
	'summary' => array(
		'storefront_pages' => count($storefront),
		'avg_storefront_ms' => $avgStoreMs,
		'avg_api_ms' => $avgApiMs,
		'grade_counts' => $grades,
	),
	'results' => $results,
	'notes' => array(
		'Grades: A ≤1.5s & ≤400KB HTML, B ≤3s & ≤600KB, C ≤6s, D slower/larger.',
		'Storefront HTML is PHP-dynamic (Cloudflare cf-cache-status: DYNAMIC) — focus on payload size + deferred JS.',
		'Epart catalog APIs should use browser cache (Cache-Control) when served from DB/cache.',
		'Static CSS/JS should have long cache (already max-age on template assets).',
	),
	'cp_links' => array(
		'orders_guide' => '/' . $cfg->backend_dir . '/shop/orders/guide',
		'config' => '/' . $cfg->backend_dir . '/control/config',
	),
);

if (!empty($_GET['save'])) {
	$dir = $_SERVER['DOCUMENT_ROOT'] . '/content/files';
	if (!is_dir($dir)) {
		mkdir($dir, 0755, true);
	}
	file_put_contents($dir . '/epc_performance_last.json', json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
	$report['saved'] = '/content/files/epc_performance_last.json';
}

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
