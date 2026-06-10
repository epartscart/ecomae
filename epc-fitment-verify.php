<?php
/**
 * Fitment window verification probe — UMAPI proxy + offline brand sources.
 * GET: token=epartscart-deploy-2026&article=GUT21&brand=GMB
 */
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Forbidden')));
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;

define('_ASTEXE_', 1);

$article = trim((string)($_GET['article'] ?? 'GUT21'));
$brand = trim((string)($_GET['brand'] ?? 'GMB'));
$host = isset($_SERVER['HTTP_HOST']) ? $_SERVER['HTTP_HOST'] : 'www.epartscart.com';
$base = 'https://' . $host;

$report = array(
	'ok' => true,
	'host' => $host,
	'article' => $article,
	'brand' => $brand,
	'umapi_status' => null,
	'brands' => null,
	'analogs' => null,
	'article_links' => null,
	'fitment_panel_ready' => false,
	'issues' => array(),
	'test_urls' => array(
		'part_page' => $base . '/en/parts/' . rawurlencode($brand) . '/' . rawurlencode($article),
		'brands_api' => $base . '/api/umapi_proxy.php?action=brands&article=' . rawurlencode($article) . '&source=fitment&language=en&vehicle_type=PC',
		'status_api' => $base . '/api/umapi_proxy.php?action=status',
	),
);

function epc_fitment_probe_fetch($url, $timeout = 25)
{
	if (!function_exists('curl_init')) {
		return array('status' => 0, 'body' => '', 'error' => 'curl unavailable');
	}
	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_TIMEOUT => $timeout,
		CURLOPT_CONNECTTIMEOUT => 10,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_HTTPHEADER => array('Accept: application/json', 'User-Agent: epc-fitment-verify'),
	));
	$body = curl_exec($ch);
	$status = (int)curl_getinfo($ch, CURLINFO_HTTP_CODE);
	$error = curl_error($ch);
	curl_close($ch);
	return array('status' => $status, 'body' => is_string($body) ? $body : '', 'error' => $error);
}

$statusProbe = epc_fitment_probe_fetch($base . '/api/umapi_proxy.php?action=status');
$report['umapi_status'] = json_decode((string)$statusProbe['body'], true);
if (!is_array($report['umapi_status'])) {
	$report['issues'][] = 'UMAPI status endpoint did not return JSON (HTTP ' . $statusProbe['status'] . ')';
}

$brandsProbe = epc_fitment_probe_fetch($base . '/api/umapi_proxy.php?action=brands&article=' . rawurlencode($article) . '&source=fitment&language=en&vehicle_type=PC');
$brandsPayload = json_decode((string)$brandsProbe['body'], true);
$report['brands'] = array(
	'http_status' => $brandsProbe['status'],
	'rows' => 0,
	'source' => null,
	'offline_message' => null,
);
if (is_array($brandsPayload)) {
	$rows = isset($brandsPayload['data']) && is_array($brandsPayload['data']) ? $brandsPayload['data'] : (is_array($brandsPayload) ? $brandsPayload : array());
	$report['brands']['rows'] = is_array($rows) ? count($rows) : 0;
	$report['brands']['source'] = $brandsPayload['source'] ?? null;
	$report['brands']['offline_message'] = $brandsPayload['offline_message'] ?? null;
	$report['brands']['message'] = $brandsPayload['message'] ?? null;
}
if ($brandsProbe['status'] !== 200 || empty($report['brands']['rows'])) {
	$report['issues'][] = 'Brand refinement returned no rows for ' . $article;
} else {
	$report['fitment_panel_ready'] = true;
}

if ($brand !== '') {
	$analogsProbe = epc_fitment_probe_fetch($base . '/api/umapi_proxy.php?action=analogs&article=' . rawurlencode($article) . '&brand=' . rawurlencode($brand) . '&limit=5&source=fitment&language=en&vehicle_type=PC');
	$analogsPayload = json_decode((string)$analogsProbe['body'], true);
	$report['analogs'] = array('http_status' => $analogsProbe['status'], 'art_id' => 0, 'source' => null);
	if (is_array($analogsPayload)) {
		$rows = isset($analogsPayload['data']) && is_array($analogsPayload['data']) ? $analogsPayload['data'] : array();
		$target = isset($rows[0]) ? $rows[0] : null;
		if (is_array($target) && !empty($target['ART_ID'])) {
			$report['analogs']['art_id'] = (int)$target['ART_ID'];
		}
		$report['analogs']['source'] = $analogsPayload['source'] ?? null;
		$report['analogs']['message'] = $analogsPayload['message'] ?? null;
	}
	if ($report['analogs']['art_id'] > 0) {
		$linksProbe = epc_fitment_probe_fetch($base . '/api/umapi_proxy.php?action=article_links&id=' . (int)$report['analogs']['art_id'] . '&source=fitment&language=en&vehicle_type=PC');
		$linksPayload = json_decode((string)$linksProbe['body'], true);
		$pc = is_array($linksPayload) ? count((array)($linksPayload['PC'] ?? array())) : 0;
		$cv = is_array($linksPayload) ? count((array)($linksPayload['CV'] ?? array())) : 0;
		$mtb = is_array($linksPayload) ? count((array)($linksPayload['Motorcycle'] ?? array())) : 0;
		$report['article_links'] = array(
			'http_status' => $linksProbe['status'],
			'pc' => $pc,
			'cv' => $cv,
			'motorcycle' => $mtb,
			'source' => is_array($linksPayload) ? ($linksPayload['source'] ?? null) : null,
			'message' => is_array($linksPayload) ? ($linksPayload['message'] ?? null) : null,
		);
		if (($pc + $cv + $mtb) === 0) {
			$report['issues'][] = 'Article links returned no vehicle rows for ART_ID ' . $report['analogs']['art_id'];
		}
	} else {
		$report['issues'][] = 'Analog lookup did not return ART_ID for ' . $brand . ' ' . $article;
	}
}

$pageProbe = epc_fitment_probe_fetch($base . '/en/parts/' . rawurlencode($brand) . '/' . rawurlencode($article), 35);
$pageHtml = (string)$pageProbe['body'];
$report['storefront'] = array(
	'http_status' => $pageProbe['status'],
	'has_fitment_button' => (strpos($pageHtml, 'id="epc-fitment-check-btn"') !== false),
	'has_fitment_script' => (strpos($pageHtml, 'epcOpenFitmentCheck') !== false),
	'has_fitment_panel' => (strpos($pageHtml, 'id="epc-fitment-panel"') !== false),
);
if (!$report['storefront']['has_fitment_button'] || !$report['storefront']['has_fitment_script']) {
	$report['issues'][] = 'Storefront part page missing fitment UI assets';
}

if (!empty($report['umapi_status']) && empty($report['umapi_status']['connected'])) {
	$report['issues'][] = 'UMAPI live connection down: ' . (string)($report['umapi_status']['message'] ?? 'unknown');
}

$report['ok'] = empty($report['issues']) || $report['fitment_panel_ready'];
$report['summary'] = $report['fitment_panel_ready']
	? 'Fitment panel can open and load brand boxes.'
	: 'Fitment panel cannot load brand data yet.';

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
