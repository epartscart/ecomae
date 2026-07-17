<?php
/**
 * Pre-generate warehouse sitemap shard caches (Cloudflare-safe).
 *
 * One shard per request — avoids 524 timeouts:
 *   /epc-seo-sitemap-warm.php?token=…&n=0
 *   /epc-seo-sitemap-warm.php?token=…&n=1
 *   …
 *
 * Auto-continue in browser (opens next shard until done):
 *   /epc-seo-sitemap-warm.php?token=…&auto=1
 *
 * Status only:
 *   /epc-seo-sitemap-warm.php?token=…&status=1
 */
declare(strict_types=1);

@set_time_limit(120);
@ini_set('memory_limit', '512M');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

require_once __DIR__ . '/epc_sitemap_lib.php';
require_once __DIR__ . '/content/general_pages/epc_sitemap_warehouse.php';

// Send headers early so Cloudflare sees a response start.
header('Content-Type: application/json; charset=utf-8');
header('X-Accel-Buffering: no');
if (function_exists('apache_setenv')) {
	@apache_setenv('no-gzip', '1');
}
@ini_set('zlib.output_compression', '0');
@ini_set('output_buffering', 'off');
while (ob_get_level() > 0) {
	ob_end_flush();
}

$cfg = new DP_Config();
$base = rtrim(epc_sitemap_base_url($cfg), '/');
$token = (string) ($_GET['token'] ?? '');
$auto = isset($_GET['auto']) && (string) $_GET['auto'] !== '0' && (string) $_GET['auto'] !== '';
$statusOnly = isset($_GET['status']) && (string) $_GET['status'] !== '0' && (string) $_GET['status'] !== '';

$pdo = epc_sitemap_pdo($cfg);
if (!($pdo instanceof PDO)) {
	http_response_code(500);
	echo json_encode(array('ok' => false, 'error' => 'DB unavailable'));
	exit;
}

$meta = epc_sitemap_warehouse_meta_read();
$existing = epc_sitemap_warehouse_existing_shard_count();
$stale = epc_sitemap_warehouse_is_stale();
$force = isset($_GET['force']) && (string) $_GET['force'] !== '0' && (string) $_GET['force'] !== '';

if ($statusOnly) {
	echo json_encode(array(
		'ok' => true,
		'stale' => $stale,
		'existing_shards' => $existing,
		'meta' => $meta,
		'next' => $base . '/epc-seo-sitemap-warm.php?token=' . rawurlencode($token) . '&n=0&auto=1&force=1',
		'gsc_submit' => $base . '/sitemap-index.php',
		'note' => 'After uploading new warehouse/supplier prices, run &auto=1&force=1 so new stock enters Google SEO sitemaps.',
	), JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
	exit;
}

// After price uploads (stale) or force=1: rebuild from shard 0 so new suppliers replace old XML.
if (($stale || $force) && !isset($_GET['n'])) {
	$n = 0;
} else {
	$n = isset($_GET['n']) ? (int) $_GET['n'] : $existing;
}
if ($n < 0) {
	$n = 0;
}
// Do NOT delete existing shards at start of force refresh — that made Google
// "Couldn't fetch" mid-warm. Overwrite shard-by-shard; trim extras when done.

$started = microtime(true);
$result = epc_sitemap_warehouse_regenerate_shard($cfg, $pdo, $n);
$elapsed = round(microtime(true) - $started, 2);
$meta = epc_sitemap_warehouse_meta_read();
$existing = epc_sitemap_warehouse_existing_shard_count();

$done = !empty($result['done']) || ($result['urls'] === 0 && $result['error'] === '');
if ($done) {
	// Remove only shards beyond the new end (after a shorter catalog).
	$publicRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? __DIR__), '/');
	foreach (glob($publicRoot . '/sitemap-warehouse-*.xml') ?: array() as $old) {
		if (preg_match('/sitemap-warehouse-(\d+)\.xml$/', $old, $m) && (int) $m[1] > $n) {
			@unlink($old);
		}
	}
	foreach (glob(epc_sitemap_warehouse_cache_dir() . '/warehouse-*.xml') ?: array() as $old) {
		if (preg_match('/warehouse-(\d+)\.xml$/', $old, $m) && (int) $m[1] > $n) {
			@unlink($old);
		}
	}
	epc_sitemap_warehouse_meta_refresh_from_files();
	$meta = epc_sitemap_warehouse_meta_read();
	$existing = epc_sitemap_warehouse_existing_shard_count();
}
$nextN = $done ? null : ($n + 1);
$forceQ = ($force || $stale) ? '&force=1' : '';
$nextUrl = $nextN === null
	? null
	: ($base . '/epc-seo-sitemap-warm.php?token=' . rawurlencode($token) . '&n=' . $nextN . ($auto ? '&auto=1' : '') . $forceQ);

$payload = array(
	'ok' => $result['error'] === '',
	'error' => $result['error'],
	'shard' => $n,
	'urls_in_shard' => $result['urls'],
	'bytes' => $result['bytes'],
	'elapsed_sec' => $elapsed,
	'done' => $done,
	'stale' => epc_sitemap_warehouse_is_stale(),
	'existing_shards' => $existing,
	'total_urls_cached' => (int) ($meta['urls'] ?? 0),
	'next_n' => $nextN,
	'next_url' => $nextUrl,
	'public_url' => $base . '/sitemap-warehouse-' . $n . '.xml',
	'gsc_submit' => $base . '/sitemap-index.php',
	'note' => $done
		? 'Warm complete — all in-stock warehouse/supplier brand+article URLs are in SEO sitemaps. Resubmit sitemap-index.php in GSC (optional if already submitted).'
		: 'Shard OK. Keep auto=1 until done. New supplier uploads mark sitemaps stale — re-run this warm after each big price import.',
);

// Browser auto-continue: HTML redirect chain (each hop < CF timeout).
if ($auto) {
	header('Content-Type: text/html; charset=utf-8');
	$json = htmlspecialchars(json_encode($payload, JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT), ENT_QUOTES, 'UTF-8');
	if ($done || $nextUrl === null) {
		echo '<!doctype html><meta charset="utf-8"><title>Sitemap warm complete</title>';
		echo '<h1>Warm complete</h1>';
		echo '<p>Shards: ' . (int) $existing . ' · URLs cached: ' . (int) ($meta['urls'] ?? 0) . '</p>';
		echo '<p>Resubmit in GSC: <a href="' . htmlspecialchars($base . '/sitemap-index.php', ENT_QUOTES, 'UTF-8') . '">sitemap-index.php</a></p>';
		echo '<pre>' . $json . '</pre>';
		exit;
	}
	echo '<!doctype html><meta charset="utf-8">';
	echo '<meta http-equiv="refresh" content="1;url=' . htmlspecialchars($nextUrl, ENT_QUOTES, 'UTF-8') . '">';
	echo '<title>Warming shard ' . (int) $n . '</title>';
	echo '<h1>Warmed shard ' . (int) $n . ' (' . (int) $result['urls'] . ' URLs, ' . $elapsed . 's)</h1>';
	echo '<p>Continuing to shard ' . (int) $nextN . '…</p>';
	echo '<p><a href="' . htmlspecialchars($nextUrl, ENT_QUOTES, 'UTF-8') . '">Continue now</a></p>';
	echo '<pre>' . $json . '</pre>';
	exit;
}

echo json_encode($payload, JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
