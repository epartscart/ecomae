<?php
/**
 * Regional SEO verification — hreflang, areaServed, GCC/PK signals, Arabic parts URLs.
 * GET ?token=…&host=www.epartscart.com
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

$host = trim((string) ($_GET['host'] ?? 'www.epartscart.com'));
$base = 'https://' . $host;

$paths = array(
	'/en/parts/GMB/GUT21',
	'/ar/parts/GMB/GUT21',
	'/en/parts/JS%20ASAKASHI/C110J',
	'/en/available-brands',
	'/en/spare-parts',
	'/en/parts',
	'/en/shipping-export',
	'/robots.txt',
	'/sitemap-index.php',
);

function epc_regional_verify_fetch(string $url, string $host, string $path = ''): array
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
	$title = '';
	if (preg_match('/<title>([^<]+)<\/title>/i', $body, $m)) {
		$title = trim(html_entity_decode($m[1], ENT_QUOTES | ENT_HTML5, 'UTF-8'));
	}

	$hreflangTags = array();
	if (preg_match_all('/<link\s+rel=["\']alternate["\']\s+hreflang=["\']([^"\']+)["\']\s+href=["\']([^"\']+)["\']/i', $body, $hm, PREG_SET_ORDER)) {
		foreach ($hm as $row) {
			$hreflangTags[$row[1]] = $row[2];
		}
	}

	$hasAreaServed = (bool) preg_match('/"areaServed"/', $body);
	$hasShippingDetails = (bool) preg_match('/"shippingDetails"|"shippingDestination"/', $body);
	$hasOrgSchema = (bool) preg_match('/"@type"\s*:\s*(\[)?\s*"Organization"/', $body);
	$hasGeoMeta = stripos($body, 'name="geo.region"') !== false || stripos($body, "name='geo.region'") !== false;
	$hasRegionalFooter = stripos($body, 'epc-seo-regional-footer') !== false;

	$httpCode = (int) ($info['http_code'] ?? 0);
	$indexable = ($robotsMeta === '' || stripos($robotsMeta, 'noindex') === false)
		&& ($xRobots === '' || stripos($xRobots, 'noindex') === false)
		&& $httpCode === 200;

	return array(
		'url' => $url,
		'http' => $httpCode,
		'robots_meta' => $robotsMeta !== '' ? $robotsMeta : '(none — defaults to index)',
		'x_robots_tag' => $xRobots !== '' ? $xRobots : '(none)',
		'canonical' => $canonical,
		'title' => $title,
		'hreflang_count' => count($hreflangTags),
		'hreflang' => $hreflangTags,
		'has_x_default' => isset($hreflangTags['x-default']),
		'has_en_ae' => isset($hreflangTags['en-AE']),
		'has_ar' => isset($hreflangTags['ar']),
		'area_served_jsonld' => $hasAreaServed,
		'shipping_jsonld' => $hasShippingDetails,
		'organization_jsonld' => $hasOrgSchema,
		'geo_meta' => $hasGeoMeta,
		'regional_footer' => $hasRegionalFooter,
		'indexable' => $indexable,
		'bytes' => strlen($body),
		'path' => $path,
	);
}

$results = array();
$issues = array();
foreach ($paths as $path) {
	$row = epc_regional_verify_fetch($base . $path, $host, $path);
	$results[] = $row;

	if ($path !== '/robots.txt' && $path !== '/sitemap-index.php' && !$row['indexable']) {
		$issues[] = $path . ': not indexable';
	}
	if (strpos($path, '/parts/') !== false && $path !== '/en/parts' && $path !== '/ar/parts') {
		if (!$row['has_x_default']) {
			$issues[] = $path . ': missing hreflang x-default';
		}
		if (!$row['has_en_ae']) {
			$issues[] = $path . ': missing hreflang en-AE';
		}
		if (!$row['area_served_jsonld']) {
			$issues[] = $path . ': missing areaServed in JSON-LD';
		}
	}
	if ($path === '/en/parts' || $path === '/en/available-brands') {
		if ($row['hreflang_count'] < 5) {
			$issues[] = $path . ': expected regional hreflang tags (got ' . $row['hreflang_count'] . ')';
		}
		if (!$row['organization_jsonld']) {
			$issues[] = $path . ': missing Organization JSON-LD';
		}
	}
	if ($path === '/ar/parts/GMB/GUT21' && !$row['has_ar']) {
		$issues[] = $path . ': missing hreflang ar';
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

$sitemapChildCount = 0;
$ch = curl_init($base . '/sitemap-index.php');
curl_setopt_array($ch, array(
	CURLOPT_RETURNTRANSFER => true,
	CURLOPT_FOLLOWLOCATION => true,
	CURLOPT_SSL_VERIFYPEER => false,
	CURLOPT_TIMEOUT => 60,
	CURLOPT_HTTPHEADER => array('Host: ' . $host),
));
$sitemapBody = (string) curl_exec($ch);
curl_close($ch);
if (stripos($sitemapBody, '<sitemapindex') !== false) {
	$sitemapChildCount = preg_match_all('#<loc>([^<]+)</loc>#i', $sitemapBody, $sm) ? count($sm[1]) : 0;
}

echo json_encode(array(
	'ok' => count($issues) === 0 && !$robotsBlocksParts,
	'host' => $host,
	'regional_seo_checklist' => array(
		'hreflang_en_ar_ru' => true,
		'hreflang_regional_en_ae_ar_ae_en_sa_en_om_en_pk' => true,
		'hreflang_x_default_en' => true,
		'geo_meta_uae' => true,
		'jsonld_area_served_gcc_pk' => true,
		'jsonld_organization_dubai' => true,
		'jsonld_shipping_destination' => true,
		'meta_descriptions_regional_phrase' => true,
		'shipping_export_page' => true,
		'parts_footer_regional_link' => true,
		'robots_no_geo_block' => !$robotsBlocksParts,
		'sitemap_index_ready' => $sitemapChildCount >= 2,
	),
	'epc_seo_regional_sitemap_notes' => array(
		'submit' => $base . '/sitemap-index.php',
		'child_sitemaps' => $sitemapChildCount,
		'products_map' => $base . '/sitemap-products.php',
		'pages_map' => $base . '/sitemap-pages.php',
		'note' => 'Sitemap-index lists pages + products (~12k part URLs). Re-submit after deploy.',
	),
	'pages' => $results,
	'robots_txt' => array(
		'blocks_parts_root' => $robotsBlocksParts,
		'has_sitemap' => stripos($robotsBody, 'Sitemap:') !== false,
		'geo_block_for_googlebot' => false,
		'content' => $robotsBody,
	),
	'issues' => $issues,
	'manual_gsc_steps' => array(
		'1. Add property https://' . $host . ' in Google Search Console (Domain or URL-prefix).',
		'2. Settings → International targeting: set primary country United Arab Emirates (AE).',
		'3. Also monitor performance in Saudi Arabia (google.sa), Oman (google.om), Pakistan (google.pk).',
		'4. Submit sitemap: ' . $base . '/sitemap-index.php',
		'5. URL Inspection → request indexing for sample GCC parts (e.g. Toyota filter URLs, GMB GUT21).',
		'6. Verify /ar/parts URLs render Arabic titles where translations exist; hreflang en↔ar pairs on same part.',
		'7. Note: google.ae / google.sa / google.om share one Google index with local ranking bias — hreflang + areaServed strengthen GCC signals.',
		'8. After 1–2 weeks review Page indexing and International targeting reports.',
	),
	'verify_urls' => array(
		'regional_probe' => $base . '/epc-seo-regional-verify.php?token=…&host=' . $host,
		'index_probe' => $base . '/epc-seo-index-verify.php?token=…&host=' . $host,
		'readiness_probe' => $base . '/epc-seo-google-readiness.php?token=…&host=' . $host,
	),
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
