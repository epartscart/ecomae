<?php
/**
 * Verify Google indexing signals on warehouse storefront + ecomae marketing URLs.
 * GET ?token=…&host=www.epartscart.com  (or host=www.ecomae.com)
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

$paths = $isEcomae
	? array(
		'/',
		'/platform',
		'/platform/capabilities',
		'/platform/auto-price-ai',
		'/platform/faq',
		'/platform/demo',
		'/robots.txt',
		'/sitemap-index.php',
	)
	: array(
		'/en/parts/GMB/GUT21',
		'/en/parts/JS%20ASAKASHI/C110J',
		'/en/available-brands',
		'/en/spare-parts',
		'/en/parts',
		'/en/parts/brands/GUT21',
		'/robots.txt',
		'/sitemap-index.php',
	);

function epc_seo_verify_fetch(string $url, string $host, string $path = '', bool $followRedirects = true): array
{
	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => $followRedirects,
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
	$hasJsonLd = stripos($body, 'application/ld+json') !== false;
	$hasOrgSchema = (bool) preg_match('/"@type"\s*:\s*"Organization"/', $body);
	$hasWebsiteSchema = (bool) preg_match('/"@type"\s*:\s*"WebSite"/', $body);
	$hasProductSchema = (bool) preg_match('/"@type"\s*:\s*"Product"/', $body)
		|| (bool) preg_match('/"@type"\s*:\s*\[[^\]]*"Product"/', $body);
	$hasFaqSchema = (bool) preg_match('/"@type"\s*:\s*"FAQPage"/', $body);
	$hasHreflang = stripos($body, 'hreflang=') !== false;
	$title = '';
	if (preg_match('/<title>([^<]+)<\/title>/i', $body, $m)) {
		$title = trim(html_entity_decode($m[1], ENT_QUOTES | ENT_HTML5, 'UTF-8'));
	}

	$is404 = stripos($body, '404 Page not found') !== false
		|| stripos($body, 'error_page') !== false && stripos($robotsMeta, 'noindex') !== false;
	$isPickerHub = strpos($path, '/parts/brands/') !== false;
	$httpCode = (int) ($info['http_code'] ?? 0);

	$indexable = ($robotsMeta === '' || stripos($robotsMeta, 'noindex') === false)
		&& ($xRobots === '' || stripos($xRobots, 'noindex') === false)
		&& $httpCode === 200;
	if ($isPickerHub) {
		$indexable = false;
		if ($httpCode >= 300 && $httpCode < 400) {
			$indexable = false;
		}
	}

	return array(
		'url' => $url,
		'http' => (int) ($info['http_code'] ?? 0),
		'robots_meta' => $robotsMeta !== '' ? $robotsMeta : '(none — defaults to index)',
		'x_robots_tag' => $xRobots !== '' ? $xRobots : '(none)',
		'canonical' => $canonical,
		'title' => $title,
		'json_ld' => $hasJsonLd,
		'organization_schema' => $hasOrgSchema,
		'website_schema' => $hasWebsiteSchema,
		'product_schema' => $hasProductSchema,
		'faq_schema' => $hasFaqSchema,
		'hreflang' => $hasHreflang,
		'indexable' => $indexable,
		'is_404' => $is404,
		'bytes' => strlen($body),
	);
}

$results = array();
$issues = array();
foreach ($paths as $path) {
	$row = epc_seo_verify_fetch($base . $path, $host, $path, strpos($path, '/parts/brands/') === false);
	$row['path'] = $path;
	$results[] = $row;
	if (!$row['indexable'] && $path !== '/robots.txt' && $path !== '/sitemap-index.php' && strpos($path, '/parts/brands/') === false) {
		$issues[] = $path . ': not indexable (http=' . $row['http'] . ', robots=' . $row['robots_meta'] . ')';
	}
	if ($path === '/en/parts/brands/GUT21') {
		$pickerOk = ($row['http'] >= 300 && $row['http'] < 400)
			|| stripos($row['robots_meta'], 'noindex') !== false;
		if (!$pickerOk) {
			$issues[] = $path . ': picker hub should redirect or be noindex';
		}
	}
	if ($path === '/' && $isEcomae) {
		if (!$row['organization_schema'] || !$row['website_schema']) {
			$issues[] = '/: missing Organization or WebSite JSON-LD';
		}
		if ($row['title'] === '') {
			$issues[] = '/: missing title';
		}
	}
	if ($path === '/platform/faq' && $isEcomae) {
		if (!$row['faq_schema']) {
			$issues[] = '/platform/faq: missing FAQPage JSON-LD';
		}
		if (!$row['indexable']) {
			$issues[] = '/platform/faq: should be index,follow';
		}
	}
	if ($path === '/en/parts/GMB/GUT21' && !$isEcomae) {
		if (!$row['product_schema']) {
			$issues[] = $path . ': missing Product JSON-LD';
		}
		if (!$row['indexable']) {
			$issues[] = $path . ': part page should be index,follow';
		}
	}
	if ($row['is_404'] && $path !== '/robots.txt') {
		$issues[] = $path . ': appears to be 404';
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
$robotsAllowsCatalog = stripos($robotsBody, 'Disallow: /*/parts/brands/') !== false
	&& stripos($robotsBody, 'Sitemap:') !== false;

echo json_encode(array(
	'ok' => count($issues) === 0 && (!$isEcomae || !$robotsBlocksParts),
	'host' => $host,
	'site_role' => $isEcomae ? 'ecomae_marketing' : 'epartscart_warehouse',
	'warehouse_indexing' => 'parts and catalog pages must be indexable; guest price hide must not add noindex',
	'pages' => $results,
	'robots_txt' => array(
		'http' => 200,
		'blocks_parts_root' => $robotsBlocksParts,
		'has_sitemap' => stripos($robotsBody, 'Sitemap:') !== false,
		'disallows_picker_hubs' => stripos($robotsBody, 'parts/brands') !== false,
		'content' => $robotsBody,
	),
	'issues' => $issues,
	'gsc_manual_steps' => array(
		'Add property https://' . $host . ' in Google Search Console.',
		'Submit sitemap: ' . $base . '/sitemap-index.php',
		'Inspect sample URL and request indexing for homepage + key product/platform pages.',
		'See epc-seo-sitemap-ping.php for full checklist.',
	),
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
