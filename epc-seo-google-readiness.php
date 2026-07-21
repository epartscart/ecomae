<?php
/**
 * Google Search Console readiness probe — sitemaps, robots, index signals for ecomae + epartscart.
 * GET ?token=…&host=www.epartscart.com  (repeat with host=www.ecomae.com)
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

$host = trim((string) ($_GET['host'] ?? 'www.epartscart.com'));
$base = 'https://' . $host;
$isEcomae = in_array(strtolower($host), array('www.ecomae.com', 'ecomae.com'), true);

function epc_gsc_probe_fetch(string $url, string $host): array
{
	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_TIMEOUT => 60,
		CURLOPT_HEADER => true,
		CURLOPT_HTTPHEADER => array('Host: ' . $host),
	));
	$raw = (string) curl_exec($ch);
	$info = curl_getinfo($ch);
	curl_close($ch);
	$headerSize = (int) ($info['header_size'] ?? 0);
	$headers = substr($raw, 0, $headerSize);
	$body = substr($raw, $headerSize);
	$robotsMeta = '';
	if (preg_match('/<meta\s+name=["\']robots["\']\s+content=["\']([^"\']+)["\']/i', $body, $m)) {
		$robotsMeta = trim($m[1]);
	}
	$xRobots = '';
	if (preg_match('/^x-robots-tag:\s*([^\r\n]+)/im', $headers, $m)) {
		$xRobots = trim($m[1]);
	}
	$canonical = '';
	if (preg_match('/<link\s+rel=["\']canonical["\']\s+href=["\']([^"\']+)["\']/i', $body, $m)) {
		$canonical = trim($m[1]);
	}
	$hasOg = stripos($body, 'property="og:title"') !== false || stripos($body, "property='og:title'") !== false;
	$hasJsonLd = stripos($body, 'application/ld+json') !== false;
	$hasHreflang = stripos($body, 'hreflang=') !== false;
	$indexable = ($robotsMeta === '' || stripos($robotsMeta, 'noindex') === false)
		&& ($xRobots === '' || stripos($xRobots, 'noindex') === false)
		&& (int) ($info['http_code'] ?? 0) === 200;
	return array(
		'url' => $url,
		'http' => (int) ($info['http_code'] ?? 0),
		'robots_meta' => $robotsMeta !== '' ? $robotsMeta : '(default index)',
		'x_robots_tag' => $xRobots !== '' ? $xRobots : '(none)',
		'canonical' => $canonical,
		'og' => $hasOg,
		'json_ld' => $hasJsonLd,
		'hreflang' => $hasHreflang,
		'indexable' => $indexable,
		'bytes' => strlen($body),
	);
}

function epc_gsc_probe_sitemap(string $url, string $host): array
{
	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_TIMEOUT => 60,
		CURLOPT_HTTPHEADER => array('Host: ' . $host),
	));
	$body = (string) curl_exec($ch);
	$http = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
	curl_close($ch);
	$urlCount = 0;
	$childMaps = 0;
	if (stripos($body, '<sitemapindex') !== false) {
		$childMaps = preg_match_all('#<loc>([^<]+)</loc>#i', $body, $m) ? count($m[1]) : 0;
	} else {
		$urlCount = preg_match_all('#<loc>([^<]+)</loc>#i', $body, $m) ? count($m[1]) : 0;
	}
	return array(
		'url' => $url,
		'http' => $http,
		'ok' => $http === 200 && (stripos($body, '<urlset') !== false || stripos($body, '<sitemapindex') !== false),
		'child_sitemaps' => $childMaps,
		'url_count' => $urlCount,
		'sample_locs' => array_slice($m[1] ?? array(), 0, 5),
	);
}

$pagePaths = $isEcomae
	? array('/', '/platform', '/platform/capabilities', '/platform/auto-price-ai', '/platform/demo', '/robots.txt')
	: array(
		'/en/parts/GMB/GUT21',
		'/en/parts/JS%20ASAKASHI/C110J',
		'/en/available-brands',
		'/en/spare-parts',
		'/en/accessories-spare-parts',
		'/en/accessories-spare-parts?id=648',
		'/en/parts',
		'/robots.txt',
	);

$pages = array();
$issues = array();
foreach ($pagePaths as $path) {
	$row = epc_gsc_probe_fetch($base . $path, $host);
	$row['path'] = $path;
	$pages[] = $row;
	if ($path !== '/robots.txt' && !$row['indexable']) {
		$issues[] = $path . ': not indexable';
	}
	if ($path !== '/robots.txt' && !$row['canonical']) {
		$issues[] = $path . ': missing canonical';
	}
}

$sitemapUrls = array($base . '/sitemap-index.php');
if ($isEcomae) {
	$sitemapUrls[] = $base . '/sitemap-marketing.php';
} else {
	$sitemapUrls[] = $base . '/sitemap-products.php';
	$sitemapUrls[] = $base . '/sitemap-pages.php';
}

$sitemaps = array();
foreach ($sitemapUrls as $smapUrl) {
	$row = epc_gsc_probe_sitemap($smapUrl, $host);
	$sitemaps[] = $row;
	if (!$row['ok']) {
		$issues[] = 'sitemap failed: ' . $smapUrl;
	}
}

$robotsBody = '';
$ch = curl_init($base . '/robots.txt');
curl_setopt_array($ch, array(
	CURLOPT_RETURNTRANSFER => true,
	CURLOPT_FOLLOWLOCATION => true,
	CURLOPT_SSL_VERIFYPEER => false,
	CURLOPT_TIMEOUT => 30,
	CURLOPT_HTTPHEADER => array('Host: ' . $host),
));
$robotsBody = (string) curl_exec($ch);
curl_close($ch);

$robotsBlocksParts = (bool) preg_match('#Disallow:\s*/(?:en|ru|ar)/parts/?\s*$#im', $robotsBody)
	|| (bool) preg_match('#Disallow:\s*/parts/?\s*$#im', $robotsBody);

$readiness = array(
	'site' => $host,
	'role' => $isEcomae ? 'marketing_platform' : 'warehouse_storefront',
	'indexable_pages' => count(array_filter($pages, static function ($p) {
		return ($p['path'] ?? '') !== '/robots.txt' && !empty($p['indexable']);
	})),
	'total_probe_pages' => count($pagePaths) - 1,
	'sitemaps_ok' => count(array_filter($sitemaps, static function ($s) {
		return !empty($s['ok']);
	})),
	'robots_allows_parts' => !$robotsBlocksParts,
	'robots_has_sitemap' => stripos($robotsBody, 'Sitemap:') !== false,
	'disallows_picker_hubs' => stripos($robotsBody, 'parts/brands') !== false,
);

echo json_encode(array(
	'ok' => count($issues) === 0 && $readiness['robots_allows_parts'],
	'host' => $host,
	'gsc_submit' => array(
		'primary' => $base . '/sitemap-index.php',
		'note' => 'Submit sitemap-index.php in Google Search Console for this property.',
	),
	'readiness' => $readiness,
	'sitemaps' => $sitemaps,
	'pages' => $pages,
	'robots_txt' => array(
		'blocks_parts_root' => $robotsBlocksParts,
		'has_sitemap_directive' => stripos($robotsBody, 'Sitemap:') !== false,
		'content' => $robotsBody,
	),
	'issues' => $issues,
	'manual_gsc_steps' => array(
		'Add property https://' . $host . ' in Google Search Console (URL-prefix or domain).',
		'Submit sitemap: ' . $base . '/sitemap-index.php',
		'Use URL Inspection on a sample part/product page to confirm index, follow.',
		'After 1–2 weeks, review Page indexing report and validate fixes if needed.',
	),
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
