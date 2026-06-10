<?php
/**
 * One-shot repair: ensure www.ecomae.com / serves platform marketing, not ERP redirect.
 * https://www.ecomae.com/epc-ecomae-force-marketing-home.php?token=epartscart-deploy-2026&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(120);

$apply = !empty($_GET['apply']);
$marker = 'ECOMAE-MARKETING-HOME-v3';
$canonicalRoot = '/home/ecomae/htdocs/www.ecomae.com';
$docroots = array_unique(array_filter(array(
	__DIR__,
	$canonicalRoot,
	'/home/ecomae/htdocs/ecomae.com',
)));

echo "=== ecomae force marketing home (v3) ===\n";
echo 'script_dir=' . __DIR__ . "\n";
echo 'apply=' . ($apply ? '1' : '0') . "\n";

$filesToSync = array(
	'index.php',
	'content/general_pages/epc_ecomae_platform_router.php',
	'content/general_pages/epc_ecomae_platform_layout.php',
	'content/shop/finance/epc_erp_portal_router.php',
);

foreach ($docroots as $root) {
	if (!is_dir($root)) {
		echo "docroot_skip={$root} (missing)\n";
		continue;
	}
	echo "docroot={$root}\n";
	$idx = $root . '/index.php';
	if (!is_file($idx)) {
		echo "  index.php=MISSING\n";
		continue;
	}
	$hash = md5_file($idx);
	echo "  index_md5={$hash}\n";
	$src = (string) file_get_contents($idx);
	echo '  index_marketing_guard=' . (strpos($src, 'epc_is_ecomae_marketing_root_request') !== false ? 'yes' : 'no') . "\n";
	echo '  index_try_exit=' . (strpos($src, 'epc_ecomae_platform_try_exit_standalone') !== false ? 'yes' : 'no') . "\n";
}

$scriptRoot = __DIR__;
$canonicalIdx = $canonicalRoot . '/index.php';
if ($apply && is_dir($canonicalRoot) && is_file($scriptRoot . '/index.php')) {
	$scriptHash = md5_file($scriptRoot . '/index.php');
	$canonHash = is_file($canonicalIdx) ? md5_file($canonicalIdx) : '';
	if ($scriptHash !== $canonHash && realpath($scriptRoot) !== realpath($canonicalRoot)) {
		foreach ($filesToSync as $rel) {
			$src = $scriptRoot . '/' . $rel;
			$dest = $canonicalRoot . '/' . $rel;
			if (!is_file($src)) {
				continue;
			}
			$dir = dirname($dest);
			if (!is_dir($dir)) {
				mkdir($dir, 0755, true);
			}
			copy($src, $dest);
			echo "copied {$rel} -> {$canonicalRoot}\n";
		}
	} else {
		echo "canonical_index_sync=skip (same docroot or hash match)\n";
	}
}

$indexPath = $scriptRoot . '/index.php';
$routerPath = $scriptRoot . '/content/general_pages/epc_ecomae_platform_router.php';
$erpRouterPath = $scriptRoot . '/content/shop/finance/epc_erp_portal_router.php';
$layoutPath = $scriptRoot . '/content/general_pages/epc_ecomae_platform_layout.php';

$checks = array(
	'index_marketing_guard' => array($indexPath, 'epc_is_ecomae_marketing_root_request'),
	'index_render_exit' => array($indexPath, 'epc_render_ecomae_marketing_home_and_exit'),
	'router_root_fn' => array($routerPath, 'function epc_is_ecomae_marketing_root_request'),
	'router_cache_headers' => array($routerPath, 'epc_ecomae_platform_send_marketing_headers'),
	'layout_marker' => array($layoutPath, $marker),
	'erp_skip_platform_root' => array($erpRouterPath, 'epc_erp_portal_is_platform_marketing_root'),
);

$missing = array();
foreach ($checks as $key => $pair) {
	list($file, $needle) = $pair;
	$body = is_file($file) ? (string) file_get_contents($file) : '';
	$ok = $body !== '' && strpos($body, $needle) !== false;
	echo "{$key}=" . ($ok ? 'ok' : 'MISSING') . "\n";
	if (!$ok) {
		$missing[] = $key;
	}
}

foreach (array($indexPath, $routerPath, $erpRouterPath, $layoutPath) as $path) {
	if (is_file($path)) {
		@touch($path);
		echo 'touched ' . basename($path) . "\n";
	}
}

if (function_exists('opcache_reset')) {
	echo 'opcache_reset=' . (opcache_reset() ? 'ok' : 'skip') . "\n";
}

define('_ASTEXE_', 1);
require_once $scriptRoot . '/config.php';
require_once $scriptRoot . '/content/general_pages/epc_portal.php';
require_once $routerPath;
require_once $erpRouterPath;

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

echo 'host=' . epc_portal_host() . "\n";
echo 'home_mode=' . (function_exists('epc_portal_home_mode') ? epc_portal_home_mode() : '?') . "\n";
echo 'access_mode=' . (function_exists('epc_portal_access_mode') ? epc_portal_access_mode() : '?') . "\n";
echo 'is_marketing_root=' . (function_exists('epc_is_ecomae_marketing_root_request') && epc_is_ecomae_marketing_root_request() ? 'yes' : 'no') . "\n";
$_SERVER['REQUEST_URI'] = '/';
$_SERVER['REQUEST_METHOD'] = 'GET';
echo 'erp_redirect_would_fire=' . (epc_erp_portal_maybe_redirect_home() ? 'yes' : 'no') . "\n";

echo "\n--- localhost self-test (127.0.0.1 + Host: www.ecomae.com) ---\n";
$localBody = '';
$localCode = 0;
$localHdr = '';
if (function_exists('curl_init')) {
	$ch = curl_init('http://127.0.0.1/');
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_HTTPHEADER => array('Host: www.ecomae.com'),
		CURLOPT_FOLLOWLOCATION => false,
		CURLOPT_TIMEOUT => 15,
		CURLOPT_HEADER => true,
	));
	$raw = curl_exec($ch);
	$localCode = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
	curl_close($ch);
	if (is_string($raw)) {
		$parts = preg_split("/\r\n\r\n|\n\n/", $raw, 2);
		$hdrBlock = $parts[0] ?? '';
		$localBody = $parts[1] ?? '';
		if (preg_match('/X-ECOMAE-Marketing-Home:\s*(\S+)/i', $hdrBlock, $m)) {
			$localHdr = $m[1];
		}
	}
} else {
	$ctx = stream_context_create(array(
		'http' => array(
			'method' => 'GET',
			'header' => "Host: www.ecomae.com\r\n",
			'timeout' => 15,
			'ignore_errors' => true,
			'follow_location' => 0,
		),
	));
	$localBody = @file_get_contents('http://127.0.0.1/', false, $ctx);
	if (isset($http_response_header) && is_array($http_response_header)) {
		foreach ($http_response_header as $h) {
			if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
				$localCode = (int) $m[1];
			}
			if (stripos($h, 'X-ECOMAE-Marketing-Home:') === 0) {
				$localHdr = trim(substr($h, 24));
			}
		}
	}
}
echo "local_http_code={$localCode}\n";
echo "local_x_marketing={$localHdr}\n";
echo 'local_epm_body=' . (is_string($localBody) && stripos($localBody, 'epm-body') !== false ? 'yes' : 'no') . "\n";
echo 'local_marker=' . (is_string($localBody) && stripos($localBody, $marker) !== false ? 'yes' : 'no') . "\n";
echo 'local_erp_standalone=' . (is_string($localBody) && stripos($localBody, 'epc-erp-standalone') !== false ? 'yes' : 'no') . "\n";

echo "\n--- public HTTPS self-test ---\n";
$ctx = stream_context_create(array(
	'http' => array('timeout' => 20, 'ignore_errors' => true, 'follow_location' => 0),
	'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
));
$homeUrl = 'https://www.ecomae.com/?_=' . time();
$body = @file_get_contents($homeUrl, false, $ctx);
$code = 0;
$cacheControl = '';
$marketingHdr = '';
if (isset($http_response_header) && is_array($http_response_header)) {
	foreach ($http_response_header as $h) {
		if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
			$code = (int) $m[1];
		}
		if (stripos($h, 'Cache-Control:') === 0) {
			$cacheControl = trim(substr($h, 14));
		}
		if (stripos($h, 'X-ECOMAE-Marketing-Home:') === 0) {
			$marketingHdr = trim(substr($h, 24));
		}
	}
}

echo "http_code={$code}\n";
echo "cache_control={$cacheControl}\n";
echo "x_marketing={$marketingHdr}\n";
echo 'body_epm_body=' . (is_string($body) && stripos($body, 'epm-body') !== false ? 'yes' : 'no') . "\n";
echo 'body_marker=' . (is_string($body) && stripos($body, $marker) !== false ? 'yes' : 'no') . "\n";
echo 'body_erp_standalone=' . (is_string($body) && stripos($body, 'epc-erp-standalone') !== false ? 'yes' : 'no') . "\n";

$pass = ($code === 200 && is_string($body) && stripos($body, 'epm-body') !== false
	&& stripos($body, $marker) !== false
	&& stripos($body, 'epc-erp-standalone') === false);
echo 'PASS=' . ($pass ? 'yes' : 'no') . "\n";

if ($missing !== array()) {
	echo "\nACTION REQUIRED: push updated files via push_one.py:\n";
	foreach (array_merge($filesToSync, array('epc-ecomae-force-marketing-home.php')) as $rel) {
		echo "  {$rel}\n";
	}
}

echo "\n--- User cache purge (if browser still shows ERP) ---\n";
echo "1. Open https://www.ecomae.com/ in incognito OR hard-refresh (Ctrl+Shift+R).\n";
echo "2. View Source — search for: {$marker} and class=\"epm-body\".\n";
echo "3. Cloudflare dashboard → ecomae.com → Caching → Purge Everything.\n";
echo "   Or purge URL only: https://www.ecomae.com/ and https://ecomae.com/\n";
echo "4. Apex ecomae.com 301s to www — bookmark https://www.ecomae.com/ not /erp.\n";
echo "5. DevTools → Network → disable cache → reload; document must be 200 to / not 301/302 to /erp.\n";
