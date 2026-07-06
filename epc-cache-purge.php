<?php
/**
 * Purge all page/widget caches after deploy, then auto-warm critical pages.
 *
 * Usage: curl -sk "https://www.epartscart.com/epc-cache-purge.php?token=epartscart-deploy-2026"
 *
 * IMPORTANT: This now automatically warms the cache after purging.
 * Do NOT call this unless you also want to wait for warmup (takes 10-30s per tenant).
 * For code-only deploys (no template changes), skip purge entirely — the old cache
 * still serves valid HTML and expires naturally in 5 min.
 */
header('Content-Type: text/plain; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
@set_time_limit(600);

$purged = 0;

// Purge full-page cache
require_once __DIR__ . '/content/general_pages/epc_page_cache.php';
$purged += epc_page_cache_purge_all();

// Purge widget/perf cache
require_once __DIR__ . '/content/general_pages/epc_perf_cache.php';
$purged += epc_perf_cache_bust_prefix('');

// Purge brands query cache
$brandsDir = __DIR__ . '/content/files/epc_brands_cache';
if (is_dir($brandsDir)) {
	foreach (glob($brandsDir . '/*') ?: array() as $f) {
		if (@unlink($f)) { $purged++; }
	}
}

echo "Purged " . $purged . " cache files.\n";

// Auto-warm critical tenant pages so visitors don't hit cold cache
if (!isset($_GET['no_warmup'])) {
	echo "\n--- AUTO-WARMING CACHE ---\n";
	$tenants = [
		'www.epartscart.com',
		'www.taxofinca.com',
		'www.electronicae.com',
		'www.stylenlook.com',
		'www.thejewellerytrend.com',
		'www.ecomae.com',
	];
	foreach ($tenants as $host) {
		$url = 'https://' . $host . '/en/';
		echo "Warming $host... ";
		$start = microtime(true);
		$ch = curl_init($url);
		curl_setopt_array($ch, [
			CURLOPT_RETURNTRANSFER => true,
			CURLOPT_FOLLOWLOCATION => true,
			CURLOPT_TIMEOUT        => 180,
			CURLOPT_CONNECTTIMEOUT => 10,
			CURLOPT_SSL_VERIFYPEER => false,
			CURLOPT_USERAGENT      => 'EPC-CacheWarmup/2.0',
		]);
		$body = curl_exec($ch);
		$code = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
		$size = (int) curl_getinfo($ch, CURLINFO_SIZE_DOWNLOAD);
		curl_close($ch);
		$elapsed = round(microtime(true) - $start, 1);
		echo "HTTP $code, " . number_format($size) . "B, {$elapsed}s\n";
		usleep(500000); // small gap between tenants
	}
	echo "--- WARMUP DONE ---\n";
}

echo "DONE\n";
