<?php
/**
 * Warehouse sitemap helpers — cached shards of /en/parts/{BRAND}/{ARTICLE}.
 *
 * Public locs (Google-friendly, no brand query strings):
 *   /sitemap-warehouse-0.xml … /sitemap-warehouse-N.xml
 */
declare(strict_types=1);

function epc_sitemap_warehouse_cache_dir(): string
{
	$dir = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? dirname(__DIR__, 2)), '/') . '/sitemap_cache';
	if (!is_dir($dir)) {
		@mkdir($dir, 0755, true);
	}
	return $dir;
}

function epc_sitemap_warehouse_shard_size(): int
{
	return 5000;
}

function epc_sitemap_warehouse_max_shards(): int
{
	return 40;
}

function epc_sitemap_warehouse_cache_path(int $shard): string
{
	return epc_sitemap_warehouse_cache_dir() . '/warehouse-' . $shard . '.xml';
}

/** Public static file in web root (works on nginx without rewrite). */
function epc_sitemap_warehouse_public_path(int $shard): string
{
	$root = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? dirname(__DIR__, 2)), '/');
	return $root . '/sitemap-warehouse-' . $shard . '.xml';
}

function epc_sitemap_warehouse_meta_path(): string
{
	return epc_sitemap_warehouse_cache_dir() . '/warehouse-meta.json';
}

/**
 * @return array{shards:int,urls:int,generated_at:string,shard_size:int}
 */
function epc_sitemap_warehouse_meta_read(): array
{
	$path = epc_sitemap_warehouse_meta_path();
	$defaults = array(
		'shards' => 0,
		'urls' => 0,
		'generated_at' => '',
		'shard_size' => epc_sitemap_warehouse_shard_size(),
	);
	if (!is_file($path)) {
		return $defaults;
	}
	$raw = json_decode((string) file_get_contents($path), true);
	if (!is_array($raw)) {
		return $defaults;
	}
	return array(
		'shards' => (int) ($raw['shards'] ?? 0),
		'urls' => (int) ($raw['urls'] ?? 0),
		'generated_at' => (string) ($raw['generated_at'] ?? ''),
		'shard_size' => (int) ($raw['shard_size'] ?? epc_sitemap_warehouse_shard_size()),
	);
}

/**
 * @param array{shards:int,urls:int,generated_at?:string,shard_size?:int} $meta
 */
function epc_sitemap_warehouse_meta_write(array $meta): void
{
	$meta['generated_at'] = $meta['generated_at'] ?? gmdate('c');
	$meta['shard_size'] = $meta['shard_size'] ?? epc_sitemap_warehouse_shard_size();
	@file_put_contents(
		epc_sitemap_warehouse_meta_path(),
		json_encode($meta, JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT)
	);
}

function epc_sitemap_warehouse_price_filters(PDO $pdo): array
{
	require_once dirname(__DIR__, 2) . '/content/general_pages/epc_seo_indexing.php';
	$priceClause = epc_seo_sitemap_price_clause($pdo);
	if ($priceClause !== '' && strpos($priceClause, 'd.`price`') === false) {
		$priceClause = str_replace('`price`', 'd.`price`', $priceClause);
	}
	$storefrontPriceFilter = '';
	$ssf = dirname(__DIR__, 2) . '/content/shop/docpart/epc_storefront_storage_flags.php';
	if (is_file($ssf)) {
		require_once $ssf;
		if (function_exists('epc_ssf_price_data_active_sql')) {
			epc_ssf_ensure_schema($pdo);
			$storefrontPriceFilter = ' AND ' . epc_ssf_price_data_active_sql('d');
		}
	}
	return array($priceClause, $storefrontPriceFilter);
}

/**
 * Build one <url> block for a warehouse part page.
 */
function epc_sitemap_warehouse_url_xml(DP_Config $cfg, string $lang, string $brand, string $article, string $lastmod): string
{
	require_once dirname(__DIR__, 2) . '/epc_sitemap_lib.php';
	$loc = epc_sitemap_part_loc($cfg, $lang, $brand, $article);
	if ($loc === '' || strpos($loc, '/parts/brands/') !== false) {
		return '';
	}
	return "\t<url>\n"
		. "\t\t<loc>" . $loc . "</loc>\n"
		. "\t\t<lastmod>" . htmlspecialchars($lastmod, ENT_XML1, 'UTF-8') . "</lastmod>\n"
		. "\t\t<changefreq>weekly</changefreq>\n"
		. "\t\t<priority>0.6</priority>\n"
		. "\t</url>\n";
}

/**
 * Regenerate all warehouse shard cache files in one forward scan (no OFFSET).
 *
 * @return array{shards:int,urls:int,error:string}
 */
function epc_sitemap_warehouse_regenerate_all(DP_Config $cfg, PDO $pdo): array
{
	require_once dirname(__DIR__, 2) . '/epc_sitemap_lib.php';
	require_once dirname(__DIR__, 2) . '/content/shop/docpart/docpart_article_match.php';

	@set_time_limit(0);
	@ini_set('memory_limit', '512M');

	$lang = epc_sitemap_lang();
	$lastmod = date('Y-m-d');
	$shardSize = epc_sitemap_warehouse_shard_size();
	$maxShards = epc_sitemap_warehouse_max_shards();
	list($priceClause, $storefrontPriceFilter) = epc_sitemap_warehouse_price_filters($pdo);

	// Clear old shard files (cache + public static copies for nginx)
	foreach (glob(epc_sitemap_warehouse_cache_dir() . '/warehouse-*.xml') ?: array() as $old) {
		@unlink($old);
	}
	$publicRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? dirname(__DIR__, 2)), '/');
	foreach (glob($publicRoot . '/sitemap-warehouse-*.xml') ?: array() as $old) {
		@unlink($old);
	}

	$sql = 'SELECT TRIM(d.`manufacturer`) AS manufacturer,
			TRIM(d.`article`) AS article
		FROM `shop_docpart_prices_data` d
		WHERE TRIM(IFNULL(d.`manufacturer`, \'\')) != \'\'
		AND TRIM(IFNULL(d.`article`, \'\')) != \'\'
		AND IFNULL(d.`exist`, 0) > 0' . $priceClause . $storefrontPriceFilter . '
		GROUP BY TRIM(d.`manufacturer`), TRIM(d.`article`)
		ORDER BY TRIM(d.`manufacturer`) ASC, TRIM(d.`article`) ASC';

	try {
		// Unbuffered cursor — avoid loading ~133k rows into PHP memory at once.
		$pdo->setAttribute(PDO::MYSQL_ATTR_USE_BUFFERED_QUERY, false);
		$stmt = $pdo->query($sql);
	} catch (Throwable $e) {
		try {
			$pdo->setAttribute(PDO::MYSQL_ATTR_USE_BUFFERED_QUERY, true);
		} catch (Throwable $ignored) {
		}
		return array('shards' => 0, 'urls' => 0, 'error' => $e->getMessage());
	}

	$shard = 0;
	$inShard = 0;
	$totalUrls = 0;
	$fh = null;
	$buffers = array();
	$openShard = function (int $n) use (&$buffers): void {
		$buffers[$n] = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
			. "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n";
	};
	$flushShard = function (int $n) use (&$buffers): void {
		if (!isset($buffers[$n])) {
			return;
		}
		$body = $buffers[$n] . "</urlset>\n";
		@file_put_contents(epc_sitemap_warehouse_cache_path($n), $body);
		@file_put_contents(epc_sitemap_warehouse_public_path($n), $body);
		unset($buffers[$n]);
	};

	try {
		$openShard(0);
		while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
			$mfr = trim((string) ($row['manufacturer'] ?? ''));
			$art = trim((string) ($row['article'] ?? ''));
			if ($mfr === '' || $art === '') {
				continue;
			}
			$xml = epc_sitemap_warehouse_url_xml($cfg, $lang, $mfr, $art, $lastmod);
			if ($xml === '') {
				continue;
			}
			if ($inShard >= $shardSize) {
				$flushShard($shard);
				if ($shard + 1 >= $maxShards) {
					break;
				}
				$shard++;
				$inShard = 0;
				$openShard($shard);
			}
			$buffers[$shard] .= $xml;
			$inShard++;
			$totalUrls++;
		}
		$flushShard($shard);
	} catch (Throwable $e) {
		return array('shards' => 0, 'urls' => 0, 'error' => $e->getMessage());
	}

	try {
		$pdo->setAttribute(PDO::MYSQL_ATTR_USE_BUFFERED_QUERY, true);
	} catch (Throwable $ignored) {
	}

	$shardCount = $totalUrls > 0 ? ($shard + 1) : 0;
	epc_sitemap_warehouse_meta_write(array(
		'shards' => $shardCount,
		'urls' => $totalUrls,
		'generated_at' => gmdate('c'),
		'shard_size' => $shardSize,
	));

	return array('shards' => $shardCount, 'urls' => $totalUrls, 'error' => '');
}

/**
 * Serve a cached warehouse shard. Returns true if response was emitted.
 */
function epc_sitemap_warehouse_serve_cached(int $shard): bool
{
	if ($shard < 0) {
		return false;
	}
	// Prefer public static file (nginx serves it without PHP when present).
	$path = epc_sitemap_warehouse_public_path($shard);
	if (!is_file($path) || filesize($path) < 40) {
		$path = epc_sitemap_warehouse_cache_path($shard);
	}
	if (!is_file($path) || filesize($path) < 40) {
		return false;
	}
	header('Content-Type: application/xml; charset=utf-8');
	header('X-Sitemap-Cache: hit');
	header('Cache-Control: public, max-age=3600');
	readfile($path);
	return true;
}

/**
 * Estimate shard count when cache is cold (for sitemap-index listing).
 */
function epc_sitemap_warehouse_estimate_shards(PDO $pdo): int
{
	$meta = epc_sitemap_warehouse_meta_read();
	if ($meta['shards'] > 0) {
		return $meta['shards'];
	}
	list($priceClause, $storefrontPriceFilter) = epc_sitemap_warehouse_price_filters($pdo);
	try {
		// Faster approximate: row count with stock (upper bound), not distinct pairs.
		$approx = (int) $pdo->query(
			'SELECT COUNT(*) FROM `shop_docpart_prices_data` d
			WHERE TRIM(IFNULL(d.`manufacturer`, \'\')) != \'\'
			AND TRIM(IFNULL(d.`article`, \'\')) != \'\'
			AND IFNULL(d.`exist`, 0) > 0' . $priceClause . $storefrontPriceFilter
		)->fetchColumn();
	} catch (Throwable $e) {
		$approx = 0;
	}
	if ($approx <= 0) {
		return 0;
	}
	// Distinct pairs ≤ rows; cap shards so Google always has children to fetch.
	$shards = (int) ceil(min($approx, 200000) / epc_sitemap_warehouse_shard_size());
	if ($shards > epc_sitemap_warehouse_max_shards()) {
		$shards = epc_sitemap_warehouse_max_shards();
	}
	return max(1, $shards);
}
