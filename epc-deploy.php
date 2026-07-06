<?php
/**
 * Post-deploy script — warm all tenant page caches sequentially.
 *
 * Run IMMEDIATELY after git pull to prevent visitors hitting cold cache:
 *   php epc-deploy.php
 *   OR: curl "https://www.ecomae.com/epc-deploy.php?token=epartscart-deploy-2026"
 *
 * This renders each tenant's homepage once (populating page cache and brand cache)
 * so subsequent visitors get instant cached responses.
 */
if (PHP_SAPI !== 'cli') {
	header('Content-Type: text/plain; charset=utf-8');
	if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
		http_response_code(403);
		exit("Forbidden\n");
	}
}

@set_time_limit(600);
@ini_set('memory_limit', '256M');

$tenants = [
	'www.ecomae.com'            => ['/en/', '/platform/industries'],
	'www.epartscart.com'        => ['/en/'],
	'www.taxofinca.com'         => ['/en/'],
	'www.electronicae.com'      => ['/en/'],
	'www.stylenlook.com'        => ['/en/'],
	'www.thejewellerytrend.com' => ['/en/'],
];

echo "=== POST-DEPLOY CACHE WARMUP ===\n";
echo "Started: " . date('Y-m-d H:i:s') . "\n\n";

$total = 0;
$success = 0;

foreach ($tenants as $host => $paths) {
	foreach ($paths as $path) {
		$total++;
		$url = 'https://' . $host . $path;
		echo "Warming: $url ... ";
		$start = microtime(true);

		$ch = curl_init($url);
		curl_setopt_array($ch, [
			CURLOPT_RETURNTRANSFER => true,
			CURLOPT_FOLLOWLOCATION => true,
			CURLOPT_TIMEOUT        => 180,
			CURLOPT_CONNECTTIMEOUT => 10,
			CURLOPT_SSL_VERIFYPEER => false,
			CURLOPT_USERAGENT      => 'EPC-Deploy-Warmup/1.0',
			CURLOPT_HTTPHEADER     => ['Host: ' . $host],
		]);
		$body = curl_exec($ch);
		$code = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
		$size = (int) curl_getinfo($ch, CURLINFO_SIZE_DOWNLOAD);
		curl_close($ch);

		$elapsed = round(microtime(true) - $start, 1);

		if ($code === 200 && $size > 5000) {
			echo "OK (HTTP $code, " . number_format($size) . "B, {$elapsed}s)\n";
			$success++;
		} else {
			echo "WARN (HTTP $code, " . number_format($size) . "B, {$elapsed}s)\n";
		}

		// Small gap between requests to avoid overloading a single PHP-FPM pool
		usleep(500000);
	}
}

echo "\n=== DONE: $success/$total pages warmed ===\n";
echo "Finished: " . date('Y-m-d H:i:s') . "\n";
