<?php
/**
 * Multi-tenant cache warmup script.
 *
 * Hits each tenant homepage internally (via localhost curl) to populate the
 * page cache.  Run after deploy to ensure all tenant storefronts respond
 * instantly from cache rather than triggering a cold render under load.
 *
 * Usage:
 *   curl -sk "https://www.ecomae.com/epc-cache-warm-tenants.php?token=epartscart-deploy-2026"
 *   php epc-cache-warm-tenants.php   (CLI)
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

$token = 'epartscart-deploy-2026';
if (PHP_SAPI !== 'cli') {
	if (empty($_GET['token']) || $_GET['token'] !== $token) {
		http_response_code(403);
		die('Forbidden');
	}
	header('Content-Type: text/plain; charset=UTF-8');
}

$tenants = array(
	array('host' => 'www.epartscart.com', 'path' => '/en/'),
	array('host' => 'www.taxofinca.com', 'path' => '/en/'),
	array('host' => 'www.stylenlook.com', 'path' => '/en/'),
	array('host' => 'www.thejewellerytrend.com', 'path' => '/en/'),
	array('host' => 'www.electronicae.com', 'path' => '/en/'),
);

$results = array();
foreach ($tenants as $tenant) {
	$url = 'https://' . $tenant['host'] . $tenant['path'];
	$start = microtime(true);

	$ch = curl_init();
	curl_setopt_array($ch, array(
		CURLOPT_URL => $url,
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_TIMEOUT => 180,
		CURLOPT_CONNECTTIMEOUT => 10,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_SSL_VERIFYHOST => 0,
		CURLOPT_USERAGENT => 'ecomae-cache-warmer/1.0',
		CURLOPT_HTTPHEADER => array('X-EPC-Cache-Warm: 1'),
	));
	$body = curl_exec($ch);
	$httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
	$err = curl_error($ch);
	curl_close($ch);

	$elapsed = round(microtime(true) - $start, 2);
	$size = $body !== false ? strlen($body) : 0;
	$cached = (is_string($body) && strpos($body, 'X-EPC-Cache: HIT') !== false) ? 'HIT' : 'MISS';

	$status = $httpCode === 200 ? 'OK' : ($httpCode === 503 ? 'BUSY' : 'ERR');
	$line = sprintf('[%s] %s%s — HTTP %d, %s bytes, %.2fs, cache: %s',
		$status, $tenant['host'], $tenant['path'],
		$httpCode, number_format($size), $elapsed, $cached
	);
	if ($err !== '') {
		$line .= ' (curl: ' . $err . ')';
	}
	$results[] = $line;
	echo $line . "\n";
	if (PHP_SAPI !== 'cli') {
		ob_flush();
		flush();
	}
}

echo "\n--- Cache warmup complete. " . count($results) . " tenants processed. ---\n";
