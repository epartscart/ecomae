<?php
/**
 * Cached warehouse product shards for Google.
 *
 * Preferred public URLs (rewrite):
 *   /sitemap-warehouse-0.xml
 *   /sitemap-warehouse-1.xml
 * Query fallback:
 *   /sitemap-warehouse.php?n=0
 *
 * Each loc is /en/parts/{BRAND}/{ARTICLE}.
 */
declare(strict_types=1);

@set_time_limit(180);
@ini_set('memory_limit', '512M');

require_once __DIR__ . '/epc_sitemap_lib.php';
require_once __DIR__ . '/content/general_pages/epc_sitemap_warehouse.php';
require_once __DIR__ . '/content/shop/docpart/docpart_article_match.php';

$n = isset($_GET['n']) ? (int) $_GET['n'] : -1;
if ($n < 0 && isset($_SERVER['REQUEST_URI'])) {
	if (preg_match('#/sitemap-warehouse-(\d+)\.xml#', (string) $_SERVER['REQUEST_URI'], $m)) {
		$n = (int) $m[1];
	}
}
if ($n < 0) {
	$n = 0;
}

// Fast path: serve pre-warmed cache (what Google needs for 133k discovery).
if (epc_sitemap_warehouse_serve_cached($n)) {
	exit;
}

// Cold cache: generate ALL shards once, then serve the requested shard.
$cfg = new DP_Config();
$pdo = epc_sitemap_pdo($cfg);
header('Content-Type: application/xml; charset=utf-8');
header('X-Sitemap-Cache: miss');

if (!($pdo instanceof PDO)) {
	echo "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";
	echo "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n</urlset>\n";
	exit;
}

$result = epc_sitemap_warehouse_regenerate_all($cfg, $pdo);
if ($result['error'] !== '') {
	echo "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";
	echo "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n</urlset>\n";
	exit;
}

if (epc_sitemap_warehouse_serve_cached($n)) {
	exit;
}

echo "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";
echo "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n</urlset>\n";
