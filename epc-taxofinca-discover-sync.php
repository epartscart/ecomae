<?php
/**
 * Discover taxofinca docroot and sync license fixes from this platform tree.
 * https://www.epartscart.com/epc-taxofinca-discover-sync.php?token=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(180);

$apply = !empty($_GET['apply']);
$hostname = 'www.taxofinca.com';
$bare = 'taxofinca.com';
$platformRoot = rtrim(__DIR__, '/\\');

$files = array(
	'core/dp_core.php',
	'content/general_pages/epc_portal.php',
	'content/general_pages/epc_portal_db.php',
	'content/general_pages/epc_portal_industry_home.php',
	'content/general_pages/animated_epartscart_logo.php',
	'content/general_pages/epc_branding.css',
	'content/shop/finance/epc_erp_portal_router.php',
	'templates/nero/desktop.php',
	'index.php',
	'epc-taxofinca-consultancy-theme.php',
	'cp/index.php',
);

function epc_tds_discover(string $hostname, string $bare): array
{
	$candidates = array();
	foreach (array(
		"/home/taxofinca/htdocs/{$hostname}",
		"/home/taxofinca/htdocs/{$hostname}/public",
		"/home/taxofinca/htdocs/{$bare}",
		"/home/taxofinca/htdocs/www.{$bare}",
		"/home/epartscart/htdocs/{$hostname}",
		"/home/epartscart/htdocs/www.{$bare}",
	) as $path) {
		$candidates[] = $path;
	}
	foreach (glob('/home/*/htdocs/*taxofinca*') ?: array() as $path) {
		$candidates[] = $path;
		$candidates[] = rtrim($path, '/') . '/public';
	}
	exec('find /home -maxdepth 6 -type f -name index.php 2>/dev/null | head -80', $findIdx);
	foreach ($findIdx as $idx) {
		$idx = trim($idx);
		if ($idx !== '' && stripos($idx, 'taxofinca') !== false) {
			$candidates[] = dirname($idx);
		}
	}
	exec('grep -R "server_name.*taxofinca" /etc/nginx/ 2>/dev/null | head -30', $ng);
	foreach ($ng as $line) {
		if (preg_match('/root\s+([^;]+);/', $line, $m)) {
			$candidates[] = trim($m[1]);
		}
	}
	$roots = array();
	$seen = array();
	foreach ($candidates as $path) {
		$path = rtrim($path, '/');
		if ($path === '' || isset($seen[$path]) || !is_dir($path)) {
			continue;
		}
		$seen[$path] = true;
		if (!is_file($path . '/index.php')) {
			continue;
		}
		$roots[] = $path;
	}
	return $roots;
}

function epc_tds_probe(string $url, string $hostHeader = ''): string
{
	$headers = $hostHeader !== '' ? ("Host: {$hostHeader}\r\n") : '';
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 20, 'ignore_errors' => true, 'header' => $headers),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	if (isset($http_response_header) && is_array($http_response_header)) {
		foreach ($http_response_header as $h) {
			if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
				$code = (int) $m[1];
			}
		}
	}
	$hint = '';
	if (is_string($body)) {
		if (stripos($body, 'License error') !== false) {
			$hint = ' [license]';
		} elseif (stripos($body, '<html') !== false) {
			$hint = ' [html len=' . strlen($body) . ']';
		} else {
			$hint = ' [bytes=' . strlen($body) . ']';
		}
	}
	return "HTTP {$code}{$hint}";
}

echo "=== Taxofinca discover + sync ===\n";
echo "platform_root={$platformRoot}\n";
echo "apply=" . ($apply ? 'yes' : 'no') . "\n\n";

$roots = epc_tds_discover($hostname, $bare);
echo 'discovered_roots=' . (count($roots) ? implode(', ', $roots) : '(none)') . "\n\n";

echo "Probes before:\n";
echo '  origin: ' . epc_tds_probe('http://127.0.0.1/', $hostname) . "\n";
echo '  public: ' . epc_tds_probe('https://' . $hostname . '/') . "\n\n";

if ($apply && count($roots) > 0) {
	foreach ($roots as $destRoot) {
		if (realpath($destRoot) === realpath($platformRoot)) {
			echo "skip same: {$destRoot}\n";
			continue;
		}
		echo "Sync -> {$destRoot}\n";
		foreach ($files as $rel) {
			$src = $platformRoot . '/' . $rel;
			$dest = $destRoot . '/' . $rel;
			if (!is_file($src)) {
				echo "  missing {$rel}\n";
				continue;
			}
			$dir = dirname($dest);
			if (!is_dir($dir)) {
				mkdir($dir, 0755, true);
			}
			$ok = @copy($src, $dest);
			echo '  ' . $rel . ': ' . ($ok ? 'ok' : 'FAIL') . "\n";
		}
		$probeDest = $destRoot . '/epc-taxofinca-live-probe.php';
		if (is_file($platformRoot . '/epc-taxofinca-live-probe.php')) {
			copy($platformRoot . '/epc-taxofinca-live-probe.php', $probeDest);
		}
	}
}

echo "\nProbes after:\n";
echo '  origin: ' . epc_tds_probe('http://127.0.0.1/', $hostname) . "\n";
echo '  public: ' . epc_tds_probe('https://' . $hostname . '/') . "\n";
echo "\nDone.\n";
