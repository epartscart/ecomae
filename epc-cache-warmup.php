<?php
/**
 * Cache warmup — render the homepage once so the page cache is warm.
 *
 * Call after deploy + cache purge to prevent the first visitor from hitting
 * a cold-cache render that may take 30-60s on heavy tenants (epartscart 133K+ parts).
 *
 * Usage:
 *   php epc-cache-warmup.php                         (CLI, local)
 *   curl -sk "https://www.epartscart.com/epc-cache-warmup.php?token=epartscart-deploy-2026"
 */
header('Content-Type: text/plain; charset=utf-8');

if (PHP_SAPI !== 'cli') {
	if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
		http_response_code(403);
		exit("Forbidden\n");
	}
}

define('_ASTEXE_', 1);
@set_time_limit(180);

$host = $_SERVER['HTTP_HOST'] ?? 'www.epartscart.com';
$scheme = (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off') ? 'https' : 'http';
$base = $scheme . '://' . $host;

$paths = array(
	'/en/',
);

echo "Cache warmup for $host\n";
echo "----\n";

foreach ($paths as $path) {
	$url = $base . $path;
	echo "Warming: $url ... ";
	$start = microtime(true);

	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_TIMEOUT => 120,
		CURLOPT_CONNECTTIMEOUT => 10,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_USERAGENT => 'EPC-CacheWarmup/1.0',
		CURLOPT_HTTPHEADER => array('Host: ' . $host),
	));
	$body = curl_exec($ch);
	$code = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
	$size = curl_getinfo($ch, CURLINFO_SIZE_DOWNLOAD);
	curl_close($ch);

	$elapsed = round(microtime(true) - $start, 2);
	echo "HTTP $code, " . number_format($size) . " bytes, {$elapsed}s\n";
}

echo "----\nDONE\n";
