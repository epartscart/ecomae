<?php
/**
 * Platform health + multi-tenant timing probes (Super CP / deploy token).
 * GET https://www.ecomae.com/epc-platform-health.php?token=epartscart-deploy-2026
 * GET ...&timing=1 — include origin + public curl-style timings per audit URL
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');
set_time_limit(120);

$withTiming = !empty($_GET['timing']);

function epc_ph_probe(string $url, string $hostHeader = '', int $timeout = 20): array
{
	$headers = $hostHeader !== '' ? ("Host: {$hostHeader}\r\nAccept-Encoding: gzip\r\n") : "Accept-Encoding: gzip\r\n";
	$ctx = stream_context_create(array(
		'http' => array('timeout' => $timeout, 'ignore_errors' => true, 'header' => $headers),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$t0 = microtime(true);
	$body = @file_get_contents($url, false, $ctx);
	$ms = (int) round((microtime(true) - $t0) * 1000);
	$code = 0;
	$encoding = '';
	$cache = '';
	if (isset($http_response_header) && is_array($http_response_header)) {
		foreach ($http_response_header as $h) {
			if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
				$code = (int) $m[1];
			}
			if (stripos($h, 'content-encoding:') === 0) {
				$encoding = trim(substr($h, 17));
			}
			if (stripos($h, 'cache-control:') === 0) {
				$cache = trim(substr($h, 14));
			}
		}
	}
	return array(
		'http' => $code ?: null,
		'ms' => $ms,
		'bytes' => is_string($body) ? strlen($body) : 0,
		'ok' => $code >= 200 && $code < 400,
		'encoding' => $encoding,
		'cache_control' => $cache,
	);
}

$auditUrls = array(
	array('site' => 'ecomae', 'label' => 'Owner /', 'host' => 'www.ecomae.com', 'path' => '/'),
	array('site' => 'ecomae', 'label' => 'Owner /platform/capabilities', 'host' => 'www.ecomae.com', 'path' => '/platform/capabilities'),
	array('site' => 'ecomae', 'label' => 'Owner /cp/', 'host' => 'www.ecomae.com', 'path' => '/cp/'),
	array('site' => 'epartscart', 'label' => 'Storefront /en/', 'host' => 'www.epartscart.com', 'path' => '/en/'),
	array('site' => 'epartscart', 'label' => 'CP /cp/', 'host' => 'www.epartscart.com', 'path' => '/cp/'),
	array('site' => 'taxofinca', 'label' => 'Storefront /en/', 'host' => 'www.taxofinca.com', 'path' => '/en/'),
	array('site' => 'taxofinca', 'label' => 'CP /cp/', 'host' => 'www.taxofinca.com', 'path' => '/cp/'),
	array('site' => 'electronicae', 'label' => 'Storefront /en/', 'host' => 'www.electronicae.com', 'path' => '/en/'),
	array('site' => 'electronicae', 'label' => 'CP /cp/', 'host' => 'www.electronicae.com', 'path' => '/cp/'),
	array('site' => 'stylenlook', 'label' => 'Storefront /en/', 'host' => 'www.stylenlook.com', 'path' => '/en/'),
	array('site' => 'stylenlook', 'label' => 'CP /cp/', 'host' => 'www.stylenlook.com', 'path' => '/cp/'),
	array('site' => 'thejewellerytrend', 'label' => 'Storefront /en/', 'host' => 'www.thejewellerytrend.com', 'path' => '/en/'),
	array('site' => 'thejewellerytrend', 'label' => 'CP /cp/', 'host' => 'www.thejewellerytrend.com', 'path' => '/cp/'),
	array('site' => 'cp.ecomae.com', 'label' => 'Super CP /cp/', 'host' => 'cp.ecomae.com', 'path' => '/cp/'),
);

$payload = array(
	'time' => date('c'),
	'host' => $_SERVER['HTTP_HOST'] ?? '',
	'php_version' => PHP_VERSION,
	'load' => null,
	'opcache' => array('enabled' => function_exists('opcache_get_status'), 'status' => null),
);

if (function_exists('sys_getloadavg')) {
	$load = sys_getloadavg();
	if (is_array($load)) {
		$payload['load'] = array('1m' => $load[0], '5m' => $load[1], '15m' => $load[2]);
	}
}

if (function_exists('opcache_get_status')) {
	$st = @opcache_get_status(false);
	if (is_array($st)) {
		$payload['opcache']['status'] = array(
			'cached_scripts' => $st['opcache_statistics']['num_cached_scripts'] ?? null,
			'hit_rate' => isset($st['opcache_statistics']['opcache_hit_rate'])
				? round((float) $st['opcache_statistics']['opcache_hit_rate'], 2)
				: null,
			'memory_used_mb' => isset($st['memory_usage']['used_memory'])
				? round($st['memory_usage']['used_memory'] / 1048576, 1)
				: null,
		);
	}
}

if ($withTiming) {
	$timings = array();
	foreach ($auditUrls as $row) {
		$host = $row['host'];
		$path = $row['path'];
		$queryPos = strpos($path, '?');
		$originPath = $queryPos !== false ? strstr($path, '?', true) : $path;
		$pubUrl = 'https://' . $host . $path;
		$origin = epc_ph_probe('http://127.0.0.1' . $originPath, $host);
		$public = epc_ph_probe($pubUrl);
		$timings[] = array(
			'site' => $row['site'],
			'label' => $row['label'],
			'url' => $pubUrl,
			'origin_ms' => $origin['ms'],
			'origin_http' => $origin['http'],
			'public_ms' => $public['ms'],
			'public_http' => $public['http'],
			'bytes' => $public['bytes'],
			'encoding' => $public['encoding'],
			'cache_control' => $public['cache_control'],
			'ok' => $public['ok'],
		);
	}
	$payload['timings'] = $timings;
	$payload['static_sample'] = epc_ph_probe(
		'https://www.epartscart.com/templates/nero/assets/css/style_all.css'
	);
}

echo json_encode($payload, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
