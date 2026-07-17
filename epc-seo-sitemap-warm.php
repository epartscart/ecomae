<?php
/**
 * Pre-generate warehouse sitemap shard caches for Google (~133k brand/article URLs).
 * GET ?token=...
 */
declare(strict_types=1);

@set_time_limit(0);
@ini_set('memory_limit', '512M');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

require_once __DIR__ . '/epc_sitemap_lib.php';
require_once __DIR__ . '/content/general_pages/epc_sitemap_warehouse.php';

header('Content-Type: application/json; charset=utf-8');

$cfg = new DP_Config();
$pdo = epc_sitemap_pdo($cfg);
if (!($pdo instanceof PDO)) {
	http_response_code(500);
	echo json_encode(array('ok' => false, 'error' => 'DB unavailable'));
	exit;
}

$started = microtime(true);
$result = epc_sitemap_warehouse_regenerate_all($cfg, $pdo);
$elapsed = round(microtime(true) - $started, 2);

$meta = epc_sitemap_warehouse_meta_read();
$sample = array();
for ($i = 0; $i < min(3, (int) $result['shards']); $i++) {
	$path = epc_sitemap_warehouse_cache_path($i);
	$sample[] = array(
		'shard' => $i,
		'url' => rtrim(epc_sitemap_base_url($cfg), '/') . '/sitemap-warehouse-' . $i . '.xml',
		'bytes' => is_file($path) ? filesize($path) : 0,
	);
}

echo json_encode(array(
	'ok' => $result['error'] === '',
	'error' => $result['error'],
	'shards' => $result['shards'],
	'urls' => $result['urls'],
	'elapsed_sec' => $elapsed,
	'meta' => $meta,
	'sample' => $sample,
	'gsc_submit' => rtrim(epc_sitemap_base_url($cfg), '/') . '/sitemap-index.php',
	'note' => 'After warm, resubmit sitemap-index.php in GSC. Child maps are sitemap-warehouse-0.xml … with /en/parts/{BRAND}/{ARTICLE} locs.',
), JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
