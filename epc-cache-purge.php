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

// Auto-warm critical tenant pages so visitors don't hit cold cache.
// Retries failed pages because PHP-FPM workers may be busy right after purge.
if (!isset($_GET['no_warmup'])) {
	echo "\n--- AUTO-WARMING CACHE ---\n";
	$warmUrls = [
		['host' => 'www.ecomae.com', 'url' => 'https://www.ecomae.com/en/'],
		['host' => 'www.epartscart.com', 'url' => 'https://www.epartscart.com/en/'],
		['host' => 'www.taxofinca.com', 'url' => 'https://www.taxofinca.com/en/'],
		['host' => 'www.electronicae.com', 'url' => 'https://www.electronicae.com/en/'],
		['host' => 'www.stylenlook.com', 'url' => 'https://www.stylenlook.com/en/'],
		['host' => 'www.thejewellerytrend.com', 'url' => 'https://www.thejewellerytrend.com/en/'],
	];
	$warmOk = 0;
	for ($try = 0; $try < 3; $try++) {
		if ($try > 0) {
			$pending = array_filter($warmUrls, function($u) { return empty($u['ok']); });
			if (empty($pending)) break;
			$wait = $try * 10;
			echo "  Retry $try: " . count($pending) . " pending, waiting {$wait}s...\n";
			sleep($wait);
		}
		foreach ($warmUrls as &$wu) {
			if (!empty($wu['ok'])) continue;
			echo "  {$wu['host']}... ";
			$ch = curl_init($wu['url']);
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
			if ($code === 200 && $size > 5000) {
				echo "OK (" . number_format($size) . "B)\n";
				$wu['ok'] = true;
				$warmOk++;
			} else {
				echo "WAIT (HTTP $code, " . number_format($size) . "B)\n";
			}
			usleep(1000000);
		}
		unset($wu);
	}
	echo "--- WARMUP: $warmOk/" . count($warmUrls) . " ---\n";
}

echo "DONE\n";
