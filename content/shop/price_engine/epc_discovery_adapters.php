<?php
/**
 * EPC Product Discovery — fetch adapters (custom website, search engine stub, .ae priority).
 * MVP: og:meta parse + curated seed + optional SerpAPI/Google CSE when API key configured.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_auto_price_engine.php';
require_once __DIR__ . '/epc_electronics_taxonomy.php';
require_once __DIR__ . '/epc_industry_taxonomy.php';

define('EPC_DISC_HTTP_CONNECT_TIMEOUT', 8);
define('EPC_DISC_HTTP_TIMEOUT', 15);
define('EPC_DISC_HTTP_CACHE_TTL', 1200);
define('EPC_DISC_CRAWL_QUICK_SOURCES', 5);
define('EPC_DISC_CRAWL_QUICK_MAX_SEC', 30);
define('EPC_DISC_PARALLEL_BATCH', 6);

/**
 * @return array<string,array{body:string,expires_at:int}>
 */
function &epc_disc_http_html_cache(): array
{
	static $cache = array();
	return $cache;
}

function epc_disc_http_cache_key(string $url, array $sourceConfig = array()): string
{
	$sourceId = (int) ($sourceConfig['source_id'] ?? 0);
	return sha1($url . '|' . $sourceId);
}

function epc_disc_http_cache_get(string $url, array $sourceConfig = array()): string
{
	$key = epc_disc_http_cache_key($url, $sourceConfig);
	$cache = &epc_disc_http_html_cache();
	$now = time();
	if (!empty($cache[$key]['body']) && (int) ($cache[$key]['expires_at'] ?? 0) > $now) {
		return (string) $cache[$key]['body'];
	}
	$dir = sys_get_temp_dir() . DIRECTORY_SEPARATOR . 'epc_disc_html';
	if (is_dir($dir)) {
		$file = $dir . DIRECTORY_SEPARATOR . $key . '.html';
		if (is_file($file) && ($now - (int) filemtime($file)) < EPC_DISC_HTTP_CACHE_TTL) {
			$body = (string) @file_get_contents($file);
			if ($body !== '') {
				$cache[$key] = array('body' => $body, 'expires_at' => $now + EPC_DISC_HTTP_CACHE_TTL);
				return $body;
			}
		}
	}
	return '';
}

function epc_disc_http_cache_set(string $url, array $sourceConfig, string $body): void
{
	if ($body === '') {
		return;
	}
	$key = epc_disc_http_cache_key($url, $sourceConfig);
	$cache = &epc_disc_http_html_cache();
	$cache[$key] = array('body' => $body, 'expires_at' => time() + EPC_DISC_HTTP_CACHE_TTL);
	$dir = sys_get_temp_dir() . DIRECTORY_SEPARATOR . 'epc_disc_html';
	if (!is_dir($dir)) {
		@mkdir($dir, 0755, true);
	}
	if (is_dir($dir)) {
		@file_put_contents($dir . DIRECTORY_SEPARATOR . $key . '.html', $body);
	}
}

/**
 * @param array<int,array{url:string,config:array<string,mixed>}> $jobs
 * @return array<string,string> url => body
 */
function epc_disc_http_fetch_parallel(array $jobs, int $batchSize = EPC_DISC_PARALLEL_BATCH): array
{
	$results = array();
	if (!$jobs || !function_exists('curl_multi_init')) {
		foreach ($jobs as $job) {
			$url = (string) ($job['url'] ?? '');
			if ($url !== '') {
				$results[$url] = epc_disc_http_fetch($url, (array) ($job['config'] ?? array()));
			}
		}
		return $results;
	}
	$batches = array_chunk($jobs, max(1, $batchSize));
	foreach ($batches as $batch) {
		$mh = curl_multi_init();
		$handles = array();
		foreach ($batch as $idx => $job) {
			$url = (string) ($job['url'] ?? '');
			if ($url === '') {
				continue;
			}
			$config = (array) ($job['config'] ?? array());
			if (function_exists('epc_disc_source_is_skipped') && epc_disc_source_is_skipped($config)) {
				continue;
			}
			$cached = epc_disc_http_cache_get($url, $config);
			if ($cached !== '') {
				$results[$url] = $cached;
				continue;
			}
			$authType = strtolower(trim((string) ($config['auth_type'] ?? 'none')));
			if ($authType === 'form_login' && !empty($config['auth_username']) && !empty($config['auth_password'])) {
				epc_disc_http_ensure_form_login($config);
			}
			$ch = curl_init($url);
			$opts = array(
				CURLOPT_RETURNTRANSFER => true,
				CURLOPT_FOLLOWLOCATION => true,
				CURLOPT_MAXREDIRS => 5,
				CURLOPT_CONNECTTIMEOUT => EPC_DISC_HTTP_CONNECT_TIMEOUT,
				CURLOPT_TIMEOUT => EPC_DISC_HTTP_TIMEOUT,
				CURLOPT_USERAGENT => 'EPC-Discovery/1.0 (+https://www.ecomae.com)',
				CURLOPT_SSL_VERIFYPEER => true,
			);
			if ($authType === 'basic' && !empty($config['auth_username'])) {
				$opts[CURLOPT_USERPWD] = (string) $config['auth_username'] . ':' . (string) ($config['auth_password'] ?? '');
			}
			$cookieHeader = epc_disc_http_cookie_header($config);
			if ($cookieHeader !== '') {
				$opts[CURLOPT_HTTPHEADER] = array('Cookie: ' . $cookieHeader);
			}
			curl_setopt_array($ch, $opts);
			curl_multi_add_handle($mh, $ch);
			$handles[$idx] = array('ch' => $ch, 'url' => $url, 'config' => $config);
		}
		if ($handles) {
			$running = null;
			do {
				curl_multi_exec($mh, $running);
				curl_multi_select($mh, 0.5);
			} while ($running > 0);
			foreach ($handles as $h) {
				$body = (string) curl_exec($h['ch']);
				$errno = (int) curl_errno($h['ch']);
				curl_multi_remove_handle($mh, $h['ch']);
				curl_close($h['ch']);
				if ($body !== '' && $errno === 0) {
					epc_disc_http_cache_set($h['url'], $h['config'], $body);
					$results[$h['url']] = $body;
				} elseif (function_exists('epc_disc_source_mark_crawl_failure')) {
					epc_disc_source_mark_crawl_failure($h['config'], $errno > 0 ? 'timeout' : 'empty');
				}
			}
		}
		curl_multi_close($mh);
	}
	return $results;
}

/**
 * Limit sources for crawl/search mode.
 *
 * @param array<int,array<string,mixed>> $sources
 * @return array<int,array<string,mixed>>
 */
function epc_disc_sources_for_crawl_mode(array $sources, string $mode = 'quick'): array
{
	if ($mode !== 'quick' || !$sources) {
		return $sources;
	}
	return array_slice($sources, 0, EPC_DISC_CRAWL_QUICK_SOURCES);
}

function epc_disc_ae_domain_priority(string $industryKey = 'electronics'): array
{
	if (function_exists('epc_apai_ae_sources_for_industry')) {
		return epc_apai_ae_sources_for_industry($industryKey);
	}
	return array(
		array('domain' => 'sharafdg.com', 'label' => 'Sharaf DG UAE', 'priority' => 10),
		array('domain' => 'noon.com', 'label' => 'Noon UAE', 'priority' => 25),
		array('domain' => 'amazon.ae', 'label' => 'Amazon.ae', 'priority' => 30),
	);
}

/**
 * Country-aware domain list for tenant (falls back to AE industry pack).
 *
 * @return array<int,array{domain:string,label:string,priority:int}>
 */
function epc_disc_country_domain_priority(PDO $pdo, string $siteKey, string $industryKey = 'electronics', int $taxonomyNodeId = 0, string $taxonomySlug = ''): array
{
	$sources = epc_disc_sources_for_search($pdo, $siteKey, $taxonomyNodeId, $taxonomySlug, true);
	$domains = epc_disc_sources_to_domain_list($sources);
	if ($domains) {
		return $domains;
	}
	if (is_file(__DIR__ . '/epc_apai_country_sources.php')) {
		require_once __DIR__ . '/epc_apai_country_sources.php';
		if (function_exists('epc_apai_country_sources_for_tenant')) {
			return epc_apai_country_sources_for_tenant($pdo, $siteKey, $industryKey);
		}
	}
	return epc_disc_ae_domain_priority($industryKey);
}

function epc_disc_adapter_registry(): array
{
	return array(
		'custom_website' => 'epc_disc_adapter_custom_website',
		'search_engine' => 'epc_disc_adapter_search_engine',
		'warehouse_supplier' => 'epc_disc_adapter_warehouse_supplier',
		'amazon_ae' => 'epc_disc_adapter_amazon_ae',
		'noon' => 'epc_disc_adapter_noon',
		'ebay' => 'epc_disc_adapter_ebay',
	);
}

/**
 * Sort discovery sources — spare247 first when tenant has valid credentials (auto_parts).
 *
 * @param array<int,array<string,mixed>> $sources
 * @return array<int,array<string,mixed>>
 */
function epc_disc_sort_sources_autoparts_primary(array $sources, string $industryKey = 'auto_parts'): array
{
	if ($industryKey !== 'auto_parts' || !$sources) {
		return $sources;
	}
	usort($sources, function ($a, $b) {
		$aDom = strtolower((string) ($a['domain'] ?? ''));
		$bDom = strtolower((string) ($b['domain'] ?? ''));
		$aSpare = strpos($aDom, 'spare247') !== false && function_exists('epc_disc_source_has_login') && epc_disc_source_has_login($a) ? 0 : 1;
		$bSpare = strpos($bDom, 'spare247') !== false && function_exists('epc_disc_source_has_login') && epc_disc_source_has_login($b) ? 0 : 1;
		if ($aSpare !== $bSpare) {
			return $aSpare <=> $bSpare;
		}
		return ((int) ($a['priority'] ?? 100)) <=> ((int) ($b['priority'] ?? 100));
	});
	return $sources;
}

/**
 * @return array{ok:bool,title:string,description:string,price:float,currency:string,images:array,source_url:string,source_domain:string,specs:array,message:string}
 */
function epc_disc_fetch_url(string $url, array $sourceConfig = array(), string $prefetchedHtml = ''): array
{
	$url = trim($url);
	if ($url === '' || !preg_match('#^https?://#i', $url)) {
		return array('ok' => false, 'title' => '', 'description' => '', 'price' => 0, 'currency' => 'AED', 'images' => array(), 'source_url' => $url, 'source_domain' => '', 'specs' => array(), 'message' => 'Invalid URL');
	}
	$extract = epc_ape_extract_url_meta($url, $sourceConfig, $prefetchedHtml);
	if (empty($extract['ok'])) {
		return array('ok' => false, 'title' => '', 'description' => '', 'price' => 0, 'currency' => 'AED', 'images' => array(), 'source_url' => $url, 'source_domain' => epc_disc_domain_from_url($url), 'specs' => array(), 'message' => (string) ($extract['message'] ?? 'Fetch failed'));
	}
	$meta = $extract['meta'] ?? array();
	$host = epc_disc_domain_from_url($url);
	return array(
		'ok' => true,
		'title' => (string) ($meta['title'] ?? ''),
		'description' => (string) ($meta['description'] ?? ''),
		'price' => (float) ($meta['price'] ?? 0),
		'currency' => (string) ($meta['currency'] ?? 'AED'),
		'images' => (array) ($meta['images'] ?? array()),
		'source_url' => $url,
		'source_domain' => $host,
		'specs' => (array) ($meta['specs'] ?? array()),
		'message' => 'Parsed og:meta from page',
	);
}

/**
 * Extract product specs from HTML (og:description, JSON-LD, tables) — MVP.
 *
 * @return array<string,string>
 */
/**
 * Parse "Toyota 1310154101" style query into brand + article.
 *
 * @return array{brand:string,article_number:string}
 */
function epc_disc_parse_brand_article_query(string $query): array
{
	$query = trim(preg_replace('/\s+/', ' ', $query));
	$out = array('brand' => '', 'article_number' => '');
	if ($query === '') {
		return $out;
	}
	if (preg_match('/^([A-Za-z][A-Za-z\-\.&]{1,24})\s+([A-Z0-9][A-Z0-9\-\/\.]{3,24})$/i', $query, $m)) {
		$out['brand'] = trim($m[1]);
		$out['article_number'] = trim($m[2]);
		return $out;
	}
	if (preg_match('/\b([A-Z0-9][A-Z0-9\-\/\.]{4,24})\b/', strtoupper($query), $am)) {
		$out['article_number'] = trim($am[1]);
		$brandPart = trim(str_ireplace($am[1], '', $query));
		if ($brandPart !== '') {
			$out['brand'] = $brandPart;
		}
	}
	return $out;
}

/**
 * Extract brand + article from URL path/query (e.g. /toyota/1310154101, ?brand=toyota&pn=1310154101).
 *
 * @return array{brand:string,article_number:string}
 */
function epc_disc_extract_brand_article_from_url(string $url): array
{
	$out = array('brand' => '', 'article_number' => '');
	$url = trim($url);
	if ($url === '') {
		return $out;
	}
	$path = (string) parse_url($url, PHP_URL_PATH);
	if (preg_match('#/([a-z][a-z0-9\-]{1,20})/([A-Z0-9][A-Z0-9\-\/\.]{3,24})#i', $path, $m)) {
		$out['brand'] = trim($m[1]);
		$out['article_number'] = trim($m[2]);
	}
	$query = (string) parse_url($url, PHP_URL_QUERY);
	if ($query !== '') {
		parse_str($query, $qs);
		foreach (array('brand', 'manufacturer', 'make', 'oem') as $bk) {
			if (!empty($qs[$bk]) && $out['brand'] === '') {
				$out['brand'] = trim((string) $qs[$bk]);
			}
		}
		foreach (array('pn', 'part', 'partno', 'part_number', 'article', 'sku', 'mpn', 'oem') as $ak) {
			if (!empty($qs[$ak]) && $out['article_number'] === '') {
				$out['article_number'] = trim((string) $qs[$ak]);
			}
		}
	}
	return $out;
}

/**
 * Build discovery search query for auto_parts — always brand + article, never title alone.
 */
function epc_disc_build_auto_parts_search_query(string $keyword, string $taxonomySlug = '', string $nodeName = ''): string
{
	if (is_file(__DIR__ . '/epc_apai_product_line_rankings.php')) {
		require_once __DIR__ . '/epc_apai_product_line_rankings.php';
	}
	$parsed = epc_disc_parse_brand_article_query($keyword);
	if (!empty($parsed['brand']) && !empty($parsed['article_number'])) {
		return trim($parsed['brand'] . ' ' . $parsed['article_number']);
	}
	if (!empty($parsed['article_number']) && $keyword !== '') {
		return trim($keyword);
	}
	$templates = function_exists('epc_apai_auto_parts_search_templates')
		? epc_apai_auto_parts_search_templates()
		: array();
	if ($taxonomySlug !== '' && !empty($templates[$taxonomySlug])) {
		return (string) $templates[$taxonomySlug];
	}
	foreach ($templates as $slug => $tpl) {
		if ($taxonomySlug !== '' && strpos($taxonomySlug, $slug) === 0) {
			return (string) $tpl;
		}
	}
	if ($keyword !== '') {
		return $keyword;
	}
	return $nodeName !== '' ? $nodeName : 'Toyota 1310154101';
}

function epc_disc_extract_specs_from_html(string $html, string $description = ''): array
{
	$specs = array();
	$text = $description !== '' ? $description : '';

	if (preg_match('/<meta[^>]+property=["\']og:description["\'][^>]+content=["\']([^"\']+)["\']/i', $html, $m)) {
		$text .= ' ' . html_entity_decode($m[1], ENT_QUOTES, 'UTF-8');
	}
	if (preg_match('/<script[^>]+type=["\']application\/ld\+json["\'][^>]*>(.*?)<\/script>/is', $html, $m)) {
		$ld = json_decode(trim($m[1]), true);
		if (is_array($ld)) {
			$nodes = isset($ld['@graph']) ? (array) $ld['@graph'] : array($ld);
			foreach ($nodes as $node) {
				if (!is_array($node)) {
					continue;
				}
				$type = (string) ($node['@type'] ?? '');
				if (stripos($type, 'Product') === false && empty($node['name'])) {
					continue;
				}
				if (!empty($node['brand']) && empty($specs['Brand'])) {
					$specs['Brand'] = trim((string) $node['brand']);
				}
				if (!empty($node['manufacturer']) && empty($specs['Manufacturer'])) {
					$specs['Manufacturer'] = trim((string) $node['manufacturer']);
				}
				if (!empty($node['mpn']) && empty($specs['MPN'])) {
					$specs['MPN'] = trim((string) $node['mpn']);
				}
				if (!empty($node['sku']) && empty($specs['SKU'])) {
					$specs['SKU'] = trim((string) $node['sku']);
				}
				if (!empty($node['name']) && empty($specs['Model'])) {
					$specs['Model'] = trim((string) $node['name']);
				}
				if (!empty($node['color'])) {
					$specs['Color'] = trim((string) $node['color']);
				}
				if (!empty($node['weight'])) {
					$specs['Weight'] = trim((string) $node['weight']);
				}
				foreach ((array) ($node['additionalProperty'] ?? array()) as $prop) {
					if (!is_array($prop)) {
						continue;
					}
					$k = trim((string) ($prop['name'] ?? ''));
					$v = trim((string) ($prop['value'] ?? ''));
					if ($k !== '' && $v !== '') {
						$specs[$k] = $v;
					}
				}
			}
		}
	}

	if (preg_match_all('/<tr[^>]*>\s*<t[hd][^>]*>([^<]+)<\/t[hd]>\s*<t[hd][^>]*>([^<]+)<\/t[hd]/is', $html, $rows, PREG_SET_ORDER)) {
		foreach ($rows as $row) {
			$k = trim(strip_tags($row[1]));
			$v = trim(strip_tags($row[2]));
			if ($k !== '' && $v !== '' && strlen($k) < 40 && strlen($v) < 120) {
				$specs[$k] = $v;
			}
		}
	}

	$autoPatterns = array(
		'Brand' => '/\b(?:brand|manufacturer|make|oem)\s*[:\-]\s*([A-Za-z][A-Za-z\-\.& ]{1,24})/i',
		'Article Number' => '/\b(?:article|part\s*(?:no|number|#)?|oem\s*(?:no|number)?|mpn|sku)\s*[:\-#]?\s*([A-Z0-9][A-Z0-9\-\/\.]{3,24})/i',
	);
	foreach ($autoPatterns as $key => $pat) {
		if (!empty($specs[$key])) {
			continue;
		}
		if (preg_match($pat, $text, $m)) {
			$specs[$key] = trim($m[1]);
		}
	}
	if (preg_match_all('/<meta[^>]+(?:name|property)=["\'](?:product:brand|og:brand|brand)["\'][^>]+content=["\']([^"\']+)["\']/i', $html, $bm)) {
		if (empty($specs['Brand'])) {
			$specs['Brand'] = trim(html_entity_decode($bm[1][0], ENT_QUOTES, 'UTF-8'));
		}
	}
	if (preg_match_all('/<meta[^>]+(?:name|property)=["\'](?:product:retailer_item_id|product:sku)["\'][^>]+content=["\']([^"\']+)["\']/i', $html, $sm)) {
		if (empty($specs['Article Number'])) {
			$specs['Article Number'] = trim(html_entity_decode($sm[1][0], ENT_QUOTES, 'UTF-8'));
		}
	}

	$patterns = array(
		'RAM' => '/\b(\d+\s*GB)\s*RAM\b/i',
		'Storage' => '/\b(\d+\s*GB|\d+\s*TB)\s*(?:storage|ROM|SSD|internal)?\b/i',
		'Color' => '/\b(Black|White|Silver|Gold|Graphite|Titanium|Natural Titanium|Space Gray|Navy|Emerald|Charcoal|Obsidian)\b/i',
		'Model' => '/\b(Galaxy S\d+|iPhone \d+|Pixel \d+|Tab [A-Z]\d+|WH-\d+|PlayStation \d+)\b/i',
		'Screen' => '/\b(\d+(?:\.\d+)?["\'])\s*(?:display|screen|UHD|OLED)?\b/i',
	);
	foreach ($patterns as $key => $pat) {
		if (!empty($specs[$key])) {
			continue;
		}
		if (preg_match($pat, $text, $m)) {
			$specs[$key] = trim($m[1]);
		}
	}

	return epc_apai_specs_enrich_brand_article(epc_disc_normalize_specs($specs));
}

/**
 * @param array<string,string> $specs
 * @return array<string,string>
 */
function epc_disc_normalize_specs(array $specs): array
{
	$out = array();
	$map = array(
		'ram' => 'RAM', 'memory' => 'RAM', 'internal memory' => 'Storage', 'storage' => 'Storage',
		'capacity' => 'Storage', 'colour' => 'Color', 'color' => 'Color', 'model' => 'Model',
		'weight' => 'Weight', 'screen size' => 'Screen', 'display' => 'Screen',
		'manufacturer' => 'Brand', 'make' => 'Brand', 'oem brand' => 'Brand', 'oem' => 'Brand',
		'part no' => 'Article Number', 'part no.' => 'Article Number', 'part number' => 'Article Number',
		'part #' => 'Article Number', 'article no' => 'Article Number', 'article no.' => 'Article Number',
		'article number' => 'Article Number', 'article' => 'Article Number', 'oe number' => 'Article Number',
		'oem number' => 'Article Number', 'mpn' => 'Article Number', 'sku' => 'Article Number',
	);
	foreach ($specs as $k => $v) {
		$k = trim((string) $k);
		$v = trim((string) $v);
		if ($k === '' || $v === '') {
			continue;
		}
		$lk = strtolower($k);
		if (isset($map[$lk])) {
			$k = $map[$lk];
		}
		$out[$k] = $v;
	}
	return epc_apai_specs_enrich_brand_article($out);
}

/**
 * Build demo specs + alternate UAE source prices for discovery seed.
 *
 * @return array{specs:array,alternate_sources:array}
 */
function epc_disc_domain_from_list_item($item): string
{
	if (is_array($item)) {
		return preg_replace('/^www\./', '', trim((string) ($item['domain'] ?? '')));
	}
	return preg_replace('/^www\./', '', trim((string) $item));
}

function epc_disc_demo_specs_and_sources(string $title, float $price, string $industryKey, array $domains, string $primaryDomain = ''): array
{
	$specs = epc_disc_extract_specs_from_html('', $title);
	if (!$specs && preg_match('/(\d+)\s*GB/i', $title, $m)) {
		$specs['Storage'] = $m[1] . 'GB';
	}
	$model = epc_disc_extract_model_number(array('title' => $title, 'specs' => $specs));
	if ($model !== '') {
		$specs['Model'] = $model;
	}
	$domainList = array();
	foreach ($domains as $d) {
		$ds = epc_disc_domain_from_list_item($d);
		if ($ds !== '') {
			$domainList[] = $ds;
		}
	}
	$domainList = array_values(array_unique($domainList));
	$primaryDomain = preg_replace('/^www\./', '', trim($primaryDomain));
	$alts = array();
	$offsets = array(0.04, -0.03, 0.06, -0.02, 0.05);
	$idx = 0;
	foreach ($domainList as $d) {
		if ($primaryDomain !== '' && $d === $primaryDomain) {
			continue;
		}
		if ($idx >= 4) {
			break;
		}
		$altPrice = round($price * (1 + ($offsets[$idx] ?? 0)), 2);
		$alts[] = array(
			'source_domain' => $d,
			'source' => $d,
			'source_url' => 'https://www.' . $d . '/p/demo',
			'price' => $altPrice,
			'currency' => 'AED',
			'specs' => $specs,
			'fetched_at' => time(),
		);
		$idx++;
	}
	return array('specs' => $specs, 'alternate_sources' => $alts, 'source_prices' => $alts);
}

function epc_disc_domain_from_url(string $url): string
{
	$host = strtolower((string) parse_url($url, PHP_URL_HOST));
	return preg_replace('/^www\./', '', $host);
}

function epc_disc_adapter_custom_website(string $url, array $config = array()): array
{
	return epc_disc_fetch_url($url);
}

function epc_disc_adapter_search_engine(string $query, array $config = array()): array
{
	$query = trim($query);
	if ($query === '') {
		return array('ok' => false, 'results' => array(), 'message' => 'Empty search query');
	}

	$apiKey = trim((string) ($config['serpapi_key'] ?? $config['search_api_key'] ?? ''));
	$cseKey = trim((string) ($config['google_cse_key'] ?? ''));
	$cseCx = trim((string) ($config['google_cse_cx'] ?? ''));

	if ($apiKey !== '') {
		$hint = 'UAE';
		if (function_exists('epc_apai_industry_profiles')) {
			$profiles = epc_apai_industry_profiles();
			$ik = (string) ($config['industry_key'] ?? 'electronics');
			if (isset($profiles[$ik]['search_hint'])) {
				$hint = (string) $profiles[$ik]['search_hint'];
			}
		}
	$countryCode = strtoupper((string) ($config['country_code'] ?? 'AE'));
	$siteFilter = 'site:.ae';
	$customDomains = (array) ($config['discovery_domains'] ?? array());
	if ($customDomains) {
		$parts = array();
		$primary = array();
		$rest = array();
		foreach ($customDomains as $d) {
			$d = strtolower(trim((string) $d));
			if ($d === '') {
				continue;
			}
			if (strpos($d, 'spare247') !== false) {
				$primary[] = 'site:' . $d;
			} else {
				$rest[] = 'site:' . $d;
			}
		}
		$ordered = array_merge($primary, $rest);
		if ($ordered) {
			$siteFilter = implode(' OR ', $ordered);
		}
	} elseif (is_file(__DIR__ . '/epc_apai_country_sources.php')) {
			require_once __DIR__ . '/epc_apai_country_sources.php';
			if (function_exists('epc_apai_country_search_site_filter')) {
				$siteFilter = epc_apai_country_search_site_filter($countryCode);
			}
			$meta = epc_apai_country_meta($countryCode);
			if (!empty($meta['label']) && $countryCode !== 'AE') {
				$hint = (string) $meta['label'];
			}
		}
		$gl = $countryCode !== '' ? strtolower($countryCode) : 'ae';
		if (function_exists('epc_apai_country_meta')) {
			$gl = (string) (epc_apai_country_meta($countryCode)['search_gl'] ?? $gl);
		}
		$q = urlencode(trim($query . ' ' . $siteFilter . ' ' . $hint));
		$apiUrl = 'https://serpapi.com/search.json?engine=google&q=' . $q . '&gl=' . urlencode($gl) . '&api_key=' . urlencode($apiKey) . '&num=10';
		$json = epc_disc_http_get($apiUrl);
		if ($json !== '') {
			$data = json_decode($json, true);
			$results = array();
			foreach ((array) ($data['organic_results'] ?? array()) as $r) {
				$link = (string) ($r['link'] ?? '');
				if ($link === '') {
					continue;
				}
				$results[] = array(
					'title' => (string) ($r['title'] ?? ''),
					'url' => $link,
					'domain' => epc_disc_domain_from_url($link),
					'snippet' => (string) ($r['snippet'] ?? ''),
				);
			}
			if ($results) {
				return array('ok' => true, 'results' => $results, 'message' => 'SerpAPI: ' . count($results) . ' results');
			}
		}
	}

	if ($cseKey !== '' && $cseCx !== '') {
		$countryCode = strtoupper((string) ($config['country_code'] ?? 'AE'));
		$marketLabel = 'UAE';
		if (is_file(__DIR__ . '/epc_apai_country_sources.php')) {
			require_once __DIR__ . '/epc_apai_country_sources.php';
			$marketLabel = (string) (epc_apai_country_meta($countryCode)['label'] ?? $marketLabel);
		}
		$q = urlencode(trim($query . ' ' . ($config['industry_key'] ?? 'electronics') . ' ' . $marketLabel));
		$apiUrl = 'https://www.googleapis.com/customsearch/v1?key=' . urlencode($cseKey) . '&cx=' . urlencode($cseCx) . '&q=' . $q . '&num=10';
		$json = epc_disc_http_get($apiUrl);
		if ($json !== '') {
			$data = json_decode($json, true);
			$results = array();
			foreach ((array) ($data['items'] ?? array()) as $r) {
				$link = (string) ($r['link'] ?? '');
				if ($link === '') {
					continue;
				}
				$results[] = array(
					'title' => (string) ($r['title'] ?? ''),
					'url' => $link,
					'domain' => epc_disc_domain_from_url($link),
					'snippet' => (string) ($r['snippet'] ?? ''),
				);
			}
			if ($results) {
				return array('ok' => true, 'results' => $results, 'message' => 'Google CSE: ' . count($results) . ' results');
			}
		}
	}

	return array(
		'ok' => false,
		'results' => array(),
		'message' => 'Search API not configured — add SerpAPI or Google CSE key in Super CP tenant config, or paste product URLs manually',
	);
}

function epc_disc_adapter_warehouse_supplier(string $url, array $config = array()): array
{
	$parsed = epc_disc_parse_brand_article_query((string) ($config['query'] ?? $url));
	if ($parsed['brand'] === '' && $parsed['article_number'] === '') {
		$parsed = epc_disc_extract_brand_article_from_url($url);
	}
	$specs = epc_apai_specs_enrich_brand_article(array(
		'brand' => (string) ($config['brand'] ?? $parsed['brand'] ?? ''),
		'article_number' => (string) ($config['article_number'] ?? $parsed['article_number'] ?? ''),
	), $url);
	$title = '';
	$price = (float) ($config['cost'] ?? 0);
	global $db_link;
	if ($db_link instanceof PDO && !empty($specs['brand_article_key'])) {
		$wh = epc_disc_find_warehouse_row_by_brand_article(
			$db_link,
			(string) ($specs['brand'] ?? ''),
			(string) ($specs['article_number'] ?? '')
		);
		if ((float) ($wh['price'] ?? 0) > 0) {
			$price = (float) $wh['price'];
		}
		if (trim((string) ($wh['title'] ?? '')) !== '') {
			$title = trim((string) ($wh['manufacturer'] ?? $specs['brand'])) . ' ' . trim((string) ($wh['article'] ?? $specs['article_number'])) . ' — ' . trim((string) $wh['title']);
		}
	}
	if ($title === '' && !empty($specs['brand']) && !empty($specs['article_number'])) {
		$title = trim((string) $specs['brand']) . ' ' . trim((string) $specs['article_number']);
	}
	return array(
		'ok' => true,
		'title' => $title,
		'description' => 'Warehouse supplier — matched by brand + article',
		'price' => $price,
		'currency' => 'AED',
		'images' => array(),
		'source_url' => $url,
		'source_domain' => 'warehouse',
		'specs' => $specs,
		'message' => 'Warehouse supplier brand+article lookup',
	);
}

function epc_disc_adapter_amazon_ae(string $url, array $config = array()): array
{
	return epc_disc_fetch_url($url);
}

function epc_disc_adapter_noon(string $url, array $config = array()): array
{
	return epc_disc_fetch_url($url);
}

function epc_disc_adapter_ebay(string $url, array $config = array()): array
{
	return epc_disc_fetch_url($url);
}

function epc_disc_http_get(string $url, array $sourceConfig = array()): string
{
	return epc_disc_http_fetch($url, $sourceConfig);
}

/**
 * In-memory session cookie cache keyed by source_id (request lifetime).
 *
 * @return array<int,array{cookies:array<string,string>,expires_at:int}>
 */
function &epc_disc_http_session_cache(): array
{
	static $cache = array();
	return $cache;
}

/**
 * Authenticated HTTP fetch for discovery crawls.
 *
 * @param array<string,mixed> $sourceConfig From epc_disc_source_auth_config()
 */
function epc_disc_http_fetch(string $url, array $sourceConfig = array(), string $prefetchedHtml = ''): string
{
	if ($url === '') {
		return '';
	}
	if ($prefetchedHtml !== '') {
		return $prefetchedHtml;
	}
	if (function_exists('epc_disc_source_is_skipped') && epc_disc_source_is_skipped($sourceConfig)) {
		return '';
	}
	$cached = epc_disc_http_cache_get($url, $sourceConfig);
	if ($cached !== '') {
		return $cached;
	}
	$authType = strtolower(trim((string) ($sourceConfig['auth_type'] ?? 'none')));
	if ($authType === 'form_login' && !empty($sourceConfig['auth_username']) && !empty($sourceConfig['auth_password'])) {
		if (!epc_disc_http_ensure_form_login($sourceConfig)) {
			if (function_exists('epc_disc_source_mark_crawl_failure')) {
				epc_disc_source_mark_crawl_failure($sourceConfig, 'login_failed');
			}
			$cachedPrice = epc_disc_http_cached_price_fallback($sourceConfig);
			if ($cachedPrice !== '') {
				return $cachedPrice;
			}
			return '';
		}
	}
	if (!function_exists('curl_init')) {
		$ctx = stream_context_create(array('http' => array('timeout' => EPC_DISC_HTTP_TIMEOUT, 'user_agent' => 'EPC-Discovery/1.0')));
		$body = @file_get_contents($url, false, $ctx) ?: '';
		if ($body !== '') {
			epc_disc_http_cache_set($url, $sourceConfig, $body);
		}
		return $body;
	}
	$ch = curl_init($url);
	$opts = array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_MAXREDIRS => 5,
		CURLOPT_CONNECTTIMEOUT => EPC_DISC_HTTP_CONNECT_TIMEOUT,
		CURLOPT_TIMEOUT => EPC_DISC_HTTP_TIMEOUT,
		CURLOPT_USERAGENT => 'EPC-Discovery/1.0 (+https://www.ecomae.com)',
		CURLOPT_SSL_VERIFYPEER => true,
	);
	if ($authType === 'basic' && !empty($sourceConfig['auth_username'])) {
		$opts[CURLOPT_USERPWD] = (string) $sourceConfig['auth_username'] . ':' . (string) ($sourceConfig['auth_password'] ?? '');
	}
	$cookieHeader = epc_disc_http_cookie_header($sourceConfig);
	if ($cookieHeader !== '') {
		$opts[CURLOPT_HTTPHEADER] = array('Cookie: ' . $cookieHeader);
	}
	curl_setopt_array($ch, $opts);
	$body = (string) curl_exec($ch);
	$errno = (int) curl_errno($ch);
	curl_close($ch);
	if ($body !== '' && $errno === 0) {
		epc_disc_http_cache_set($url, $sourceConfig, $body);
		return $body;
	}
	if (function_exists('epc_disc_source_mark_crawl_failure')) {
		epc_disc_source_mark_crawl_failure($sourceConfig, $errno > 0 ? 'timeout' : 'empty');
	}
	return epc_disc_http_cached_price_fallback($sourceConfig);
}

/**
 * Placeholder HTML when source is slow/skipped — signals caller to use cached prices.
 */
function epc_disc_http_cached_price_fallback(array $sourceConfig): string
{
	$domain = strtolower((string) ($sourceConfig['domain'] ?? ''));
	if ($domain === '' || strpos($domain, 'spare247') === false) {
		return '';
	}
	return '<!-- epc_disc_use_cached_prices -->';
}

function epc_disc_http_cookie_header(array $sourceConfig): string
{
	$sourceId = (int) ($sourceConfig['source_id'] ?? 0);
	$cache = &epc_disc_http_session_cache();
	$cookies = array();
	if ($sourceId > 0 && !empty($cache[$sourceId]['cookies']) && is_array($cache[$sourceId]['cookies'])) {
		$cookies = $cache[$sourceId]['cookies'];
	} elseif (!empty($sourceConfig['session_cookies']) && is_array($sourceConfig['session_cookies'])) {
		$cookies = $sourceConfig['session_cookies'];
	}
	$parts = array();
	foreach ($cookies as $name => $value) {
		$name = trim((string) $name);
		if ($name === '') {
			continue;
		}
		$parts[] = rawurlencode($name) . '=' . rawurlencode((string) $value);
	}
	return implode('; ', $parts);
}

/**
 * @param array<string,mixed> $sourceConfig
 */
function epc_disc_http_ensure_form_login(array $sourceConfig): bool
{
	$sourceId = (int) ($sourceConfig['source_id'] ?? 0);
	$cache = &epc_disc_http_session_cache();
	$now = time();
	if ($sourceId > 0 && !empty($cache[$sourceId]['cookies']) && (int) ($cache[$sourceId]['expires_at'] ?? 0) > $now) {
		return true;
	}
	if (!empty($sourceConfig['session_cookies']) && (int) ($sourceConfig['session_expires_at'] ?? 0) > $now) {
		if ($sourceId > 0) {
			$cache[$sourceId] = array(
				'cookies' => (array) $sourceConfig['session_cookies'],
				'expires_at' => (int) $sourceConfig['session_expires_at'],
			);
		}
		return true;
	}
	if (!function_exists('curl_init')) {
		return false;
	}
	$loginUrl = trim((string) ($sourceConfig['login_url'] ?? ''));
	if ($loginUrl === '') {
		return false;
	}
	$userField = trim((string) ($sourceConfig['login_username_field'] ?? 'email'));
	$passField = trim((string) ($sourceConfig['login_password_field'] ?? 'password'));
	$postFields = array(
		$userField => (string) ($sourceConfig['auth_username'] ?? ''),
		$passField => (string) ($sourceConfig['auth_password'] ?? ''),
	);
	$cookieJar = tempnam(sys_get_temp_dir(), 'epc_disc_');
	if ($cookieJar === false) {
		return false;
	}
	$ch = curl_init($loginUrl);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_MAXREDIRS => 5,
		CURLOPT_CONNECTTIMEOUT => EPC_DISC_HTTP_CONNECT_TIMEOUT,
		CURLOPT_TIMEOUT => EPC_DISC_HTTP_TIMEOUT,
		CURLOPT_POST => true,
		CURLOPT_POSTFIELDS => http_build_query($postFields),
		CURLOPT_USERAGENT => 'EPC-Discovery/1.0 (+https://www.ecomae.com)',
		CURLOPT_SSL_VERIFYPEER => true,
		CURLOPT_COOKIEJAR => $cookieJar,
		CURLOPT_COOKIEFILE => $cookieJar,
		CURLOPT_HEADER => true,
	));
	$response = (string) curl_exec($ch);
	$httpCode = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
	curl_close($ch);
	$cookies = epc_disc_http_parse_cookie_jar($cookieJar);
	@unlink($cookieJar);
	if (!$cookies && $response !== '') {
		$cookies = epc_disc_http_parse_set_cookie_headers($response);
	}
	if (!$cookies) {
		return false;
	}
	$expiresAt = $now + 3600;
	if ($sourceId > 0) {
		$cache[$sourceId] = array('cookies' => $cookies, 'expires_at' => $expiresAt);
	}
	return $httpCode >= 200 && $httpCode < 400;
}

/**
 * Test discovery source credentials (basic or form login).
 *
 * @param array<string,mixed> $sourceConfig
 * @return array{ok:bool,message:string}
 */
function epc_disc_test_source_auth(array $sourceConfig): array
{
	$authType = strtolower(trim((string) ($sourceConfig['auth_type'] ?? 'none')));
	$username = trim((string) ($sourceConfig['auth_username'] ?? ''));
	$password = (string) ($sourceConfig['auth_password'] ?? '');
	$domain = trim((string) ($sourceConfig['domain'] ?? ''));
	if (!in_array($authType, array('basic', 'form_login'), true)) {
		return array('ok' => false, 'message' => 'Select HTTP Basic or Form login');
	}
	if ($username === '' || $password === '') {
		return array('ok' => false, 'message' => 'Username and password required');
	}
	$loginUrl = trim((string) ($sourceConfig['login_url'] ?? ''));
	if ($loginUrl === '' && $domain !== '') {
		$loginUrl = 'https://www.' . preg_replace('/^www\./', '', preg_replace('/^https?:\/\//', '', $domain)) . '/login';
	}
	$testUrl = $loginUrl !== '' ? $loginUrl : $domain;
	if ($testUrl !== '' && !preg_match('/^https?:\/\//', $testUrl)) {
		$testUrl = 'https://' . ltrim($testUrl, '/');
	}
	if ($testUrl === '') {
		return array('ok' => false, 'message' => 'Domain or login URL required');
	}
	if ($authType === 'basic') {
		if (!function_exists('curl_init')) {
			return array('ok' => false, 'message' => 'cURL required for login test');
		}
		$ch = curl_init($testUrl);
		curl_setopt_array($ch, array(
			CURLOPT_RETURNTRANSFER => true,
			CURLOPT_FOLLOWLOCATION => true,
			CURLOPT_MAXREDIRS => 5,
			CURLOPT_TIMEOUT => 25,
			CURLOPT_NOBODY => true,
			CURLOPT_USERPWD => $username . ':' . $password,
			CURLOPT_USERAGENT => 'EPC-Discovery/1.0 (+https://www.ecomae.com)',
			CURLOPT_SSL_VERIFYPEER => true,
		));
		curl_exec($ch);
		$httpCode = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
		curl_close($ch);
		if ($httpCode === 401 || $httpCode === 403) {
			return array('ok' => false, 'message' => 'Login failed: HTTP ' . $httpCode);
		}
		if ($httpCode >= 200 && $httpCode < 400) {
			return array('ok' => true, 'message' => 'Login successful ✓');
		}
		return array('ok' => false, 'message' => 'Login failed: HTTP ' . $httpCode);
	}
	$sourceId = (int) ($sourceConfig['source_id'] ?? 0);
	if ($sourceId > 0) {
		$cache = &epc_disc_http_session_cache();
		unset($cache[$sourceId]);
	}
	if ($loginUrl === '') {
		return array('ok' => false, 'message' => 'Login URL required for form login');
	}
	$loggedIn = epc_disc_http_ensure_form_login($sourceConfig);
	if (!$loggedIn) {
		return array('ok' => false, 'message' => 'Login failed: invalid credentials or no session cookie');
	}
	$probeUrl = preg_replace('#/login(?:/.*)?$#i', '/', $loginUrl);
	if ($probeUrl === $loginUrl) {
		$probeUrl = 'https://www.' . preg_replace('/^www\./', '', preg_replace('/^https?:\/\//', '', $domain));
	}
	$html = epc_disc_http_fetch($probeUrl, $sourceConfig);
	if ($html !== '' && preg_match('/type\s*=\s*["\']password["\']/i', $html) && preg_match('/login|sign[\s-]?in/i', $html)) {
		return array('ok' => false, 'message' => 'Login failed: still on login page');
	}
	return array('ok' => true, 'message' => 'Login successful ✓');
}

function epc_disc_http_parse_cookie_jar(string $path): array
{
	$cookies = array();
	if (!is_readable($path)) {
		return $cookies;
	}
	$lines = file($path, FILE_IGNORE_NEW_LINES | FILE_SKIP_EMPTY_LINES) ?: array();
	foreach ($lines as $line) {
		if ($line === '' || $line[0] === '#') {
			continue;
		}
		$parts = explode("\t", $line);
		if (count($parts) >= 7) {
			$cookies[(string) $parts[5]] = (string) $parts[6];
		}
	}
	return $cookies;
}

function epc_disc_http_parse_set_cookie_headers(string $response): array
{
	$cookies = array();
	if (!preg_match_all('/^Set-Cookie:\s*([^=]+)=([^;\r\n]+)/mi', $response, $matches, PREG_SET_ORDER)) {
		return $cookies;
	}
	foreach ($matches as $m) {
		$cookies[trim($m[1])] = trim($m[2]);
	}
	return $cookies;
}

/**
 * Run discovery for a taxonomy node — MVP uses curated demo products when live fetch unavailable.
 *
 * @return array{ok:bool,added:int,message:string}
 */
function epc_disc_run_for_taxonomy(PDO $pdo, string $siteKey, string $taxonomySlug = '', string $keyword = '', array $options = array()): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	epc_disc_ensure_schema($pdo);
	$searchMode = in_array((string) ($options['search_mode'] ?? 'full'), array('fast', 'full'), true)
		? (string) ($options['search_mode'] ?? 'full')
		: 'full';

	$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	$node = null;
	if ($taxonomySlug !== '') {
		$node = epc_apai_tax_by_slug($pdo, $industryKey, $taxonomySlug);
		if (!$node) {
			$node = epc_tax_by_slug($pdo, $taxonomySlug, $industryKey);
		}
	}
	$nodeId = $node ? (int) $node['id'] : 0;
	$nodeName = $node ? (string) ($node['name_en'] ?? '') : ($keyword !== '' ? $keyword : ucfirst(str_replace('_', ' ', $industryKey)));
	$taxSlug = $taxonomySlug !== '' ? $taxonomySlug : ($node ? (string) ($node['slug'] ?? '') : '');

	$tenantCfg = epc_ape_tenant_config_get($pdo, $siteKey);
	$config = (array) ($tenantCfg['config'] ?? array());
	$config['industry_key'] = $industryKey;
	if (is_file(__DIR__ . '/epc_apai_country_sources.php')) {
		require_once __DIR__ . '/epc_apai_country_sources.php';
		epc_apai_install_country_sources($pdo, $siteKey);
		$config['country_code'] = epc_apai_tenant_country($siteKey, $pdo);
	}
	if ($industryKey === 'auto_parts') {
		$searchQuery = epc_disc_build_auto_parts_search_query($keyword, $taxSlug, $nodeName);
	} else {
		$searchQuery = $keyword !== '' ? $keyword : $nodeName;
	}

	$added = 0;
	$sources = epc_disc_sources_for_search($pdo, $siteKey, $nodeId, $taxSlug, true);
	if ($industryKey === 'auto_parts') {
		$sources = epc_disc_sort_sources_autoparts_primary($sources, $industryKey);
	}
	if ($searchMode === 'fast') {
		$sources = array_slice($sources, 0, 3);
		$config['search_mode'] = 'fast';
	}
	$domainList = epc_disc_sources_to_domain_list($sources);
	$config['discovery_domains'] = array_column($domainList, 'domain');
	$config['discovery_sources'] = $sources;
	$searchResult = epc_disc_adapter_search_engine($searchQuery, $config);
	$sourceMsg = epc_disc_sources_search_message($sources, 'Using');
	if ($searchMode === 'fast') {
		$sourceMsg = 'Fast search (3 sources) — ' . $sourceMsg;
	}

	if ($industryKey === 'auto_parts' && $searchQuery !== '') {
		foreach ($sources as $src) {
			$srcDomain = strtolower((string) ($src['domain'] ?? ''));
			if (strpos($srcDomain, 'spare247') === false || !epc_disc_source_has_login($src)) {
				continue;
			}
			$brandArticle = epc_disc_parse_brand_article_query($searchQuery);
			if ($brandArticle['brand'] === '' || $brandArticle['article_number'] === '') {
				break;
			}
			$spareUrl = 'https://www.spare247.com/search?q=' . rawurlencode($brandArticle['brand'] . ' ' . $brandArticle['article_number']);
			$fetched = epc_disc_deep_fetch_url($spareUrl, epc_disc_source_config_for_url($spareUrl, $sources));
			if (!empty($fetched['specs']) && is_array($fetched['specs'])) {
				$fetched['specs'] = epc_apai_specs_enrich_brand_article($fetched['specs'], $spareUrl, (string) ($fetched['title'] ?? ''));
			}
			if (!empty($fetched['ok']) && epc_disc_queue_add_from_fetch($pdo, $siteKey, $fetched, $nodeId)) {
				$added++;
			}
			break;
		}
	}

	if (!empty($searchResult['ok']) && !empty($searchResult['results'])) {
		foreach ($searchResult['results'] as $r) {
			$url = (string) ($r['url'] ?? '');
			if ($url === '') {
				continue;
			}
			$fetched = $industryKey === 'auto_parts' && function_exists('epc_disc_deep_fetch_url')
				? epc_disc_deep_fetch_url($url, epc_disc_source_config_for_url($url, $sources))
				: epc_disc_fetch_url($url, epc_disc_source_config_for_url($url, $sources));
			if (!empty($fetched['specs']) && is_array($fetched['specs'])) {
				$fetched['specs'] = epc_apai_specs_enrich_brand_article($fetched['specs'], $url, (string) ($fetched['title'] ?? ''));
			}
			if (empty($fetched['ok']) || trim((string) ($fetched['title'] ?? '')) === '') {
				$fetched = array(
					'ok' => true,
					'title' => (string) ($r['title'] ?? 'Product'),
					'description' => (string) ($r['snippet'] ?? ''),
					'price' => 0,
					'currency' => 'AED',
					'images' => array(),
					'source_url' => $url,
					'source_domain' => (string) ($r['domain'] ?? epc_disc_domain_from_url($url)),
				);
			}
			if (epc_disc_queue_add_from_fetch($pdo, $siteKey, $fetched, $nodeId)) {
				$added++;
			}
			if ($added >= 10) {
				break;
			}
		}
	}

	if ($added === 0) {
		$demoProducts = epc_disc_demo_products_for_node($nodeName, $taxSlug, $sources, $industryKey, $pdo, $siteKey, $nodeId);
		foreach ($demoProducts as $prod) {
			if (epc_disc_queue_add_from_fetch($pdo, $siteKey, $prod, $nodeId ?: (int) ($prod['taxonomy_node_id'] ?? 0))) {
				$added++;
			}
		}
	}

	epc_disc_cross_source_match($pdo, $siteKey);
	if (function_exists('epc_disc_merge_cross_source_prices')) {
		epc_disc_merge_cross_source_prices($pdo, $siteKey);
	}

	foreach ($sources as $src) {
		$pdo->prepare('UPDATE `epc_discovery_sources` SET `last_crawl` = ? WHERE `id` = ?')->execute(array(time(), (int) $src['id']));
	}

	$searchMessage = (string) ($searchResult['message'] ?? '');
	if ($searchMessage === '' || strpos($searchMessage, 'not configured') !== false) {
		$searchMessage = epc_disc_sources_search_message($sources, 'Demo/stub search across');
	} else {
		$searchMessage .= ' · ' . $sourceMsg;
	}

	$msg = $added > 0
		? "Added {$added} suggested products for {$nodeName}"
		: 'No new products discovered — configure search API or paste URLs';

	return array(
		'ok' => $added > 0,
		'added' => $added,
		'message' => $msg,
		'search_message' => $searchMessage,
		'sources_used' => count($domainList),
		'source_domains' => array_column($domainList, 'domain'),
		'search_mode' => $searchMode,
	);
}

/**
 * @return array<int,array<string,mixed>>
 */
function epc_disc_demo_products_for_node(string $nodeName, string $slug, array $sources, string $industryKey = 'electronics', ?PDO $pdo = null, string $siteKey = '', int $taxonomyNodeId = 0): array
{
	$domains = array();
	if ($sources) {
		foreach (epc_disc_sources_to_domain_list($sources) as $src) {
			$domains[] = (string) ($src['domain'] ?? '');
		}
	}
	if (!$domains && $pdo instanceof PDO && $siteKey !== '') {
		foreach (epc_disc_country_domain_priority($pdo, $siteKey, $industryKey, $taxonomyNodeId, $slug) as $src) {
			$domains[] = (string) ($src['domain'] ?? '');
		}
	} elseif (!$domains) {
		foreach (epc_disc_ae_domain_priority($industryKey) as $src) {
			$domains[] = (string) ($src['domain'] ?? '');
		}
	}
	if (!$domains) {
		$domains = array('noon.com', 'amazon.ae');
	}
	$currency = 'AED';
	if ($pdo instanceof PDO && $siteKey !== '' && is_file(__DIR__ . '/epc_apai_country_sources.php')) {
		require_once __DIR__ . '/epc_apai_country_sources.php';
		$currency = (string) (epc_apai_country_meta(epc_apai_tenant_country($siteKey, $pdo))['currency'] ?? 'AED');
	}

	$catalog = array(
		'auto_parts' => array(
			'auto-engine-filters-oil' => array(
				array('title' => 'Toyota Oil Filter 04152-YZZA6', 'brand' => 'Toyota', 'article_number' => '04152YZZA6', 'price' => 45, 'cost' => 32, 'img' => 'https://m.media-amazon.com/images/I/31lmcNasPol._AC_SL1000_.jpg'),
				array('title' => 'Nissan Oil Filter 15208-65F0E', 'brand' => 'Nissan', 'article_number' => '1520865F0E', 'price' => 38, 'cost' => 26, 'img' => 'https://m.media-amazon.com/images/I/714Rq4k05UL._AC_SL1000_.jpg'),
				array('title' => 'Toyota Air Filter 17801-31090', 'brand' => 'Toyota', 'article_number' => '1780131090', 'price' => 89, 'cost' => 62, 'img' => 'https://m.media-amazon.com/images/I/61S+J4NfYLL._AC_SL1500_.jpg'),
			),
			'auto-brakes-pads' => array(
				array('title' => 'Toyota Brake Pad 04465-33471', 'brand' => 'Toyota', 'article_number' => '0446533471', 'price' => 320, 'cost' => 245),
				array('title' => 'Nissan Brake Pad D1060-1MA0A', 'brand' => 'Nissan', 'article_number' => 'D10601MA0A', 'price' => 185, 'cost' => 140),
			),
			'auto-oem-toyota' => array(
				array('title' => 'Toyota Spark Plug 1310154101', 'brand' => 'Toyota', 'article_number' => '1310154101', 'price' => 75, 'cost' => 52),
				array('title' => 'Toyota Water Pump 16100-39466', 'brand' => 'Toyota', 'article_number' => '1610039466', 'price' => 890, 'cost' => 720),
			),
			'auto-body-lights' => array(
				array('title' => 'Toyota Headlight 81110-33C80', 'brand' => 'Toyota', 'article_number' => '8111033C80', 'price' => 890, 'cost' => 720),
				array('title' => 'Osram Bulb H7 64210', 'brand' => 'Osram', 'article_number' => '64210', 'price' => 75, 'cost' => 52),
			),
		),
		'electronics' => array(
			'cell-phones' => array(
				array('title' => 'Samsung Galaxy S24 Ultra 256GB — Titanium Gray', 'price' => 4299, 'cost' => 3650, 'img' => 'https://images.samsung.com/is/image/samsung/p6pim/ae/2401/gallery/ae-galaxy-s24-ultra-sm-s928bztheua-thumb-539864570'),
				array('title' => 'Apple iPhone 15 Pro 128GB — Natural Titanium', 'price' => 3999, 'cost' => 3400, 'img' => 'https://store.storeimages.cdn-apple.com/4982/as-images.apple.com/is/iphone-15-pro-finish-select-202309-6-1inch-naturaltitanium'),
				array('title' => 'Google Pixel 8 Pro 128GB — Obsidian', 'price' => 3499, 'cost' => 2950, 'img' => 'https://lh3.googleusercontent.com/pixel8pro'),
			),
			'computers-tablets' => array(
				array('title' => 'Samsung Galaxy Tab S9 128GB WiFi — Graphite', 'price' => 2499, 'cost' => 2100),
				array('title' => 'Apple iPad Air 11" M2 128GB — Space Gray', 'price' => 2799, 'cost' => 2380),
			),
			'headphones' => array(
				array('title' => 'Sony WH-1000XM5 Wireless Noise Cancelling', 'price' => 1299, 'cost' => 1050),
				array('title' => 'Apple AirPods Pro (2nd generation) USB-C', 'price' => 899, 'cost' => 750),
			),
			'tv-video-televisions' => array(
				array('title' => 'Samsung 55" Crystal UHD 4K Smart TV CU8000', 'price' => 1899, 'cost' => 1550),
				array('title' => 'LG 65" OLED C3 4K Smart TV', 'price' => 5499, 'cost' => 4700),
			),
			'gaming-consoles' => array(
				array('title' => 'Sony PlayStation 5 Slim Console — UAE Version', 'price' => 1899, 'cost' => 1650),
				array('title' => 'Xbox Series X 1TB Console', 'price' => 1799, 'cost' => 1550),
			),
			'smart-home-assistants' => array(
				array('title' => 'Amazon Echo Dot (5th Gen) — Charcoal', 'price' => 199, 'cost' => 145),
				array('title' => 'Google Nest Hub 2nd Gen — Chalk', 'price' => 349, 'cost' => 280),
			),
		),
		'fashion' => array(
			'fashion-men-shirts' => array(
				array('title' => 'Calvin Klein Slim Fit Cotton Shirt — White', 'price' => 249, 'cost' => 165),
				array('title' => 'Tommy Hilfiger Regular Fit Polo — Navy', 'price' => 199, 'cost' => 130),
			),
			'fashion-women-dresses' => array(
				array('title' => 'Mango Satin Midi Dress — Emerald', 'price' => 299, 'cost' => 195),
				array('title' => 'Zara Linen Blend Abaya — Sand', 'price' => 349, 'cost' => 220),
			),
		),
		'jewellery' => array(
			'jewellery-rings-gold-22k' => array(
				array('title' => '22K Gold Ring — Classic Band 4g', 'price' => 1450, 'cost' => 1180),
				array('title' => 'Malabar 22K Gold Ring — Floral design', 'price' => 1890, 'cost' => 1540),
			),
			'jewellery-watches-luxury' => array(
				array('title' => 'Citizen Eco-Drive Classic — Silver dial', 'price' => 899, 'cost' => 620),
				array('title' => 'Fossil Gen 6 Hybrid Smartwatch — Rose gold', 'price' => 749, 'cost' => 520),
			),
		),
		'general_retail' => array(
			'retail-home-appliances-coffee' => array(
				array('title' => 'De\'Longhi Magnifica S ECAM22.110 — Espresso machine', 'price' => 1299, 'cost' => 980),
				array('title' => 'Nespresso Vertuo Next — Matte black', 'price' => 599, 'cost' => 420),
			),
			'retail-health-vitamins' => array(
				array('title' => 'Centrum Adults Multivitamin — 100 tablets', 'price' => 89, 'cost' => 58),
				array('title' => 'Nature Made Vitamin D3 5000 IU — 180 softgels', 'price' => 75, 'cost' => 48),
			),
		),
	);

	$indCatalog = $catalog[$industryKey] ?? $catalog['general_retail'];
	$key = $slug;
	if (!isset($indCatalog[$key])) {
		foreach (array_keys($indCatalog) as $k) {
			if ($slug !== '' && (strpos($slug, $k) === 0 || strpos($k, explode('-', $slug)[0]) === 0)) {
				$key = $k;
				break;
			}
		}
	}
	if (!isset($indCatalog[$key])) {
		$key = array_key_first($indCatalog);
	}
	$items = $indCatalog[$key] ?? array();
	$maxItems = ($industryKey === 'auto_parts' || $industryKey === 'electronics') ? 3 : 2;

	$out = array();
	$i = 0;
	foreach ($items as $item) {
		$d = $domains[$i % count($domains)];
		$slugPart = preg_replace('/[^a-z0-9]+/', '-', strtolower(substr($item['title'], 0, 40)));
		$img = (string) ($item['img'] ?? '');
		$images = $img !== '' ? array($img) : array();
		$itemSpecs = array();
		if ($industryKey === 'auto_parts' && !empty($item['brand']) && !empty($item['article_number'])) {
			$itemSpecs = epc_apai_specs_enrich_brand_article(array(
				'brand' => (string) $item['brand'],
				'article_number' => (string) $item['article_number'],
			));
		}
		$extra = epc_disc_demo_specs_and_sources($item['title'], (float) $item['price'], $industryKey, $domains, $d);
		if ($itemSpecs) {
			$extra['specs'] = array_merge((array) ($extra['specs'] ?? array()), $itemSpecs);
		}
		$baSlug = !empty($itemSpecs['brand_article_key'])
			? preg_replace('/[^a-z0-9]+/', '-', strtolower((string) $itemSpecs['brand_article_key']))
			: preg_replace('/[^a-z0-9]+/', '-', strtolower(substr($item['title'], 0, 40)));
		$out[] = array(
			'ok' => true,
			'title' => $item['title'],
			'description' => 'Suggested from ' . $d . ' — ' . $nodeName . ' (' . str_replace('_', ' ', $industryKey) . '). Review margin before approve.',
			'price' => (float) $item['price'],
			'currency' => $currency,
			'images' => $images,
			'source_url' => 'https://www.' . $d . '/' . ($industryKey === 'auto_parts' && !empty($itemSpecs['brand']) ? epc_apai_normalize_brand((string) $itemSpecs['brand']) . '/' . epc_apai_normalize_article((string) ($itemSpecs['article_number'] ?? '')) : 'product/' . trim($baSlug, '-')) . '-demo',
			'source_domain' => $d,
			'cost_estimate' => (float) $item['cost'],
			'specs' => (array) ($item['specs'] ?? $extra['specs']),
			'alternate_sources' => (array) ($extra['alternate_sources'] ?? array()),
			'source_prices' => (array) ($extra['source_prices'] ?? $extra['alternate_sources'] ?? array()),
			'message' => 'Curated demo discovery item',
		);
		$i++;
		if ($i >= $maxItems) {
			break;
		}
	}
	return $out;
}

function epc_disc_batch_urls(PDO $pdo, string $siteKey, string $urlsRaw, int $taxonomyNodeId = 0): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$added = 0;
	$lines = preg_split('/[\r\n]+/', $urlsRaw);
	foreach ($lines as $line) {
		$url = trim($line);
		if ($url === '' || !preg_match('#^https?://#i', $url)) {
			continue;
		}
		$fetched = epc_disc_fetch_url($url, epc_disc_source_config_for_url($url, epc_disc_sources_list($pdo, $siteKey, false)));
		if (epc_disc_queue_add_from_fetch($pdo, $siteKey, $fetched, $taxonomyNodeId)) {
			$added++;
		}
	}
	return array('ok' => $added > 0, 'added' => $added, 'message' => "Imported {$added} URLs to discovery queue");
}

/**
 * Deep crawl — og:title, og:image, og:description, JSON-LD specs, gallery images, price.
 *
 * @return array{ok:bool,title:string,description:string,price:float,currency:string,images:array,source_url:string,source_domain:string,specs:array,message:string,crawl_depth:int}
 */
function epc_disc_deep_fetch_url(string $url, array $sourceConfig = array()): array
{
	$base = epc_disc_fetch_url($url, $sourceConfig);
	if (empty($base['ok'])) {
		return array_merge($base, array('crawl_depth' => 0));
	}
	$html = epc_disc_http_get($url, $sourceConfig);
	if ($html === '') {
		return array_merge($base, array('crawl_depth' => 1));
	}

	$images = (array) ($base['images'] ?? array());
	if (preg_match_all('/<meta[^>]+property=["\']og:image(?::secure_url)?["\'][^>]+content=["\']([^"\']+)["\']/i', $html, $ogImgs)) {
		foreach ($ogImgs[1] as $imgUrl) {
			$imgUrl = trim(html_entity_decode($imgUrl, ENT_QUOTES, 'UTF-8'));
			if ($imgUrl !== '' && !in_array($imgUrl, $images, true)) {
				$images[] = $imgUrl;
			}
		}
	}
	if (preg_match_all('/<img[^>]+src=["\']([^"\']+)["\']/i', $html, $imgTags)) {
		foreach ($imgTags[1] as $imgUrl) {
			$imgUrl = trim(html_entity_decode($imgUrl, ENT_QUOTES, 'UTF-8'));
			if ($imgUrl === '' || stripos($imgUrl, 'data:') === 0) {
				continue;
			}
			if (strpos($imgUrl, '//') === 0) {
				$imgUrl = 'https:' . $imgUrl;
			} elseif (strpos($imgUrl, '/') === 0) {
				$parts = parse_url($url);
				$imgUrl = ($parts['scheme'] ?? 'https') . '://' . ($parts['host'] ?? '') . $imgUrl;
			}
			if (preg_match('/(product|gallery|zoom|large|800|1000)/i', $imgUrl) && !in_array($imgUrl, $images, true)) {
				$images[] = $imgUrl;
			}
			if (count($images) >= 8) {
				break;
			}
		}
	}

	$specs = epc_disc_extract_specs_from_html($html, (string) ($base['description'] ?? ''));
	if (!$specs && !empty($base['specs']) && is_array($base['specs'])) {
		$specs = $base['specs'];
	}
	$specs = epc_apai_specs_enrich_brand_article($specs, $url, (string) ($base['title'] ?? ''));

	$price = (float) ($base['price'] ?? 0);
	if ($price <= 0 && preg_match('/"price"\s*:\s*"?([0-9.]+)"?/i', $html, $pm)) {
		$price = (float) $pm[1];
	}

	return array(
		'ok' => true,
		'title' => (string) ($base['title'] ?? ''),
		'description' => (string) ($base['description'] ?? ''),
		'price' => $price,
		'currency' => (string) ($base['currency'] ?? 'AED'),
		'images' => array_slice(array_values(array_unique($images)), 0, 8),
		'source_url' => $url,
		'source_domain' => (string) ($base['source_domain'] ?? epc_disc_domain_from_url($url)),
		'specs' => $specs,
		'message' => 'Deep crawl: og/meta + JSON-LD + gallery',
		'crawl_depth' => 2,
	);
}

/**
 * Crawl all active discovery sources — re-fetch queue URLs + top product lines.
 *
 * @param array<string,mixed> $options taxonomy_id, limit
 * @return array{ok:bool,updated:int,added:int,sources_crawled:int,message:string,last_crawl_at:int}
 */
function epc_disc_crawl_sources(PDO $pdo, string $siteKey, array $options = array()): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	epc_disc_ensure_schema($pdo);
	if (is_file(__DIR__ . '/epc_apai_country_sources.php')) {
		require_once __DIR__ . '/epc_apai_country_sources.php';
		if (function_exists('epc_apai_purge_own_domain_sources')) {
			epc_apai_purge_own_domain_sources($pdo, $siteKey);
		}
	}
	$startedAt = microtime(true);
	$now = time();
	$scheduled = !empty($options['scheduled']);
	$mode = in_array((string) ($options['mode'] ?? 'quick'), array('quick', 'full'), true)
		? (string) ($options['mode'] ?? 'quick')
		: 'quick';
	if ($scheduled && empty($options['mode'])) {
		$mode = 'quick';
	}
	$sourcesAll = epc_disc_sources_for_search($pdo, $siteKey, max(0, (int) ($options['taxonomy_id'] ?? 0)), '', true);
	$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	if ($industryKey === 'auto_parts') {
		$sourcesAll = epc_disc_sort_sources_autoparts_primary($sourcesAll, $industryKey);
	}
	$sources = ($mode === 'quick')
		? epc_disc_sources_for_crawl_mode($sourcesAll, 'quick')
		: $sourcesAll;
	$sourceCount = count($sources);
	$sourceTotal = count($sourcesAll);
	$queueLimit = ($mode === 'quick') ? 40 : 120;

	$stmt = $pdo->prepare(
		'SELECT * FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` IN (\'suggested\', \'imported\')
		 ORDER BY `updated_at` DESC LIMIT ' . (int) $queueLimit
	);
	$stmt->execute(array($siteKey));
	$rows = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();

	$queueIds = array();
	foreach ($rows as $row) {
		$queueIds[] = (int) ($row['id'] ?? 0);
	}
	$fetchRes = epc_disc_fetch_queue_prices($pdo, $siteKey, $queueIds, max(0, (int) ($options['taxonomy_id'] ?? 0)), array('deep' => true, 'mode' => $mode));
	$updated = (int) ($fetchRes['updated'] ?? 0);
	$failureFlush = epc_disc_source_flush_failures($pdo);

	$added = 0;
	$rawSuggested = function_exists('epc_disc_queue_suggested_count')
		? epc_disc_queue_suggested_count($pdo, $siteKey)
		: count($rows);
	$lineSearchLimit = 0;
	if ($mode === 'full') {
		$lineSearchLimit = $industryKey === 'electronics' ? 5 : 3;
	} elseif ($mode === 'quick' && $rawSuggested < 5) {
		$lineSearchLimit = 5;
	}
	if ($lineSearchLimit > 0 && is_file(__DIR__ . '/epc_apai_product_line_rankings.php')) {
		require_once __DIR__ . '/epc_apai_product_line_rankings.php';
		if (function_exists('epc_apai_product_line_rankings')) {
			$rankings = epc_apai_product_line_rankings($pdo, $siteKey, $industryKey);
			$topLines = array_slice((array) ($rankings['top'] ?? array()), 0, $lineSearchLimit);
			$templates = ($industryKey === 'auto_parts' && function_exists('epc_apai_auto_parts_search_templates'))
				? epc_apai_auto_parts_search_templates()
				: array();
			foreach ($topLines as $line) {
				if ((microtime(true) - $startedAt) > 120) {
					break;
				}
				$slug = (string) ($line['slug'] ?? '');
				if ($slug === '') {
					continue;
				}
				$keyword = (string) ($templates[$slug] ?? '');
				$res = epc_disc_run_for_taxonomy($pdo, $siteKey, $slug, $keyword, array('search_mode' => 'fast'));
				$added += (int) ($res['added'] ?? 0);
			}
			if ($mode === 'full' && $industryKey === 'electronics' && function_exists('epc_disc_crawl_electronics_cross_source')) {
				$crossRes = epc_disc_crawl_electronics_cross_source($pdo, $siteKey, $options);
				$added += (int) ($crossRes['added'] ?? 0);
			}
		}
	}

	foreach ($sources as $src) {
		$pdo->prepare('UPDATE `epc_discovery_sources` SET `last_crawl` = ? WHERE `id` = ?')->execute(array($now, (int) $src['id']));
	}

	$crossMatch = epc_disc_cross_source_match($pdo, $siteKey);
	$mergeRes = function_exists('epc_disc_merge_cross_source_prices')
		? epc_disc_merge_cross_source_prices($pdo, $siteKey)
		: array('merged' => 0, 'canonical' => 0);
	$arbitrageScan = array('scanned' => 0, 'opportunities' => 0, 'updated' => 0, 'message' => '');
	if (is_file(__DIR__ . '/epc_apai_marketplace_channels.php')) {
		require_once __DIR__ . '/epc_apai_marketplace_channels.php';
		if (function_exists('epc_disc_marketplace_arbitrage_scan')) {
			$arbitrageScan = epc_disc_marketplace_arbitrage_scan($pdo, $siteKey, array(
				'check_presence' => true,
				'limit' => ($mode === 'full') ? 80 : 40,
			));
		}
	}
	$catMatch = array('count' => 0, 'items' => array());
	if ($mode === 'full' && function_exists('epc_disc_match_catalogue_to_market') && $industryKey === 'auto_parts') {
		$catMatch = epc_disc_match_catalogue_to_market($pdo, $siteKey);
	}

	if ($scheduled) {
		epc_disc_set_last_scheduled_crawl_at($pdo, $siteKey, $now);
	} else {
		epc_disc_set_last_crawl_at($pdo, $siteKey, $now);
	}

	$elapsed = round(microtime(true) - $startedAt, 1);
	$sourcesFetched = (int) ($fetchRes['sources_fetched'] ?? $sourceCount);
	$bgTick = !empty($options['bg_tick']);
	$maxSec = (float) ($options['max_seconds'] ?? 0);
	$crawlComplete = !$bgTick || ($maxSec <= 0) || ($elapsed < $maxSec);
	$msg = "Updated {$updated} products";
	if ($added > 0) {
		$msg .= ", {$added} new suggestions";
	}
	$msg .= " · Crawled {$sourcesFetched}/{$sourceTotal} sources in {$elapsed}s";
	if ($mode === 'quick') {
		$msg .= ' (quick)';
	}
	if (!empty($failureFlush['fallbacks'])) {
		$msg .= ' · ' . implode('; ', array_values($failureFlush['fallbacks']));
	}
	if ((int) ($arbitrageScan['opportunities'] ?? 0) > 0) {
		$msg .= ' · ' . (int) $arbitrageScan['opportunities'] . ' arbitrage gap(s)';
	}

	$marketConfirmed = (int) ($crossMatch['market_confirmed'] ?? 0);
	$fallbackAction = '';
	if ($marketConfirmed === 0 && $updated === 0 && $added === 0) {
		$fallbackAction = 'quick_compare_demo';
	}

	return array(
		'ok' => ($updated + $added) > 0 || $sourceCount > 0,
		'updated' => $updated,
		'added' => $added,
		'sources_crawled' => $sourceCount,
		'sources_total' => $sourceTotal,
		'sources_fetched' => $sourcesFetched,
		'elapsed_sec' => $elapsed,
		'mode' => $mode,
		'message' => $msg,
		'last_crawl_at' => $now,
		'last_scheduled_crawl_at' => $scheduled ? $now : epc_disc_get_last_scheduled_crawl_at($pdo, $siteKey),
		'scheduled' => $scheduled,
		'complete' => $crawlComplete,
		'source_domains' => array_column(epc_disc_sources_to_domain_list($sources), 'domain'),
		'cross_source_match' => $crossMatch,
		'merge_cross_source' => $mergeRes,
		'warehouse_market_match' => array(
			'count' => (int) ($catMatch['count'] ?? 0),
			'message' => (string) ($catMatch['message'] ?? ''),
		),
		'fallbacks' => (array) ($failureFlush['fallbacks'] ?? array()),
		'fallback_action' => $fallbackAction,
		'marketplace_arbitrage_scan' => $arbitrageScan,
	);
}

/**
 * Electronics crawl — seed cross-source prices for top product lines across all enabled retailers.
 *
 * @param array<string,mixed> $options
 * @return array{ok:bool,added:int,updated:int,message:string}
 */
function epc_disc_crawl_electronics_cross_source(PDO $pdo, string $siteKey, array $options = array()): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if (epc_apai_resolve_industry($pdo, $siteKey) !== 'electronics') {
		return array('ok' => true, 'added' => 0, 'updated' => 0, 'message' => 'Skipped — not electronics tenant');
	}
	if (!is_file(__DIR__ . '/epc_apai_product_line_rankings.php')) {
		return array('ok' => true, 'added' => 0, 'updated' => 0, 'message' => 'Product line rankings unavailable');
	}
	require_once __DIR__ . '/epc_apai_product_line_rankings.php';
	$rankings = epc_apai_product_line_rankings($pdo, $siteKey, 'electronics');
	$sources = epc_disc_sources_for_search($pdo, $siteKey, max(0, (int) ($options['taxonomy_id'] ?? 0)), '', true);
	$added = 0;
	$updated = 0;
	$now = time();
	$updStmt = $pdo->prepare(
		'UPDATE `epc_product_discovery_queue` SET `meta_json` = ?, `updated_at` = ? WHERE `id` = ? AND `site_key` = ?'
	);
	foreach (array_slice((array) ($rankings['top'] ?? array()), 0, 5) as $line) {
		$slug = (string) ($line['slug'] ?? '');
		if ($slug === '') {
			continue;
		}
		$node = function_exists('epc_apai_tax_by_slug') ? epc_apai_tax_by_slug($pdo, 'electronics', $slug) : null;
		if (!$node) {
			$node = epc_tax_by_slug($pdo, $slug, 'electronics');
		}
		$nodeId = $node ? (int) ($node['id'] ?? 0) : 0;
		$nodeName = (string) ($line['name'] ?? $line['name_en'] ?? $slug);
		$demos = epc_disc_demo_products_for_node($nodeName, $slug, $sources, 'electronics', $pdo, $siteKey, $nodeId);
		foreach ($demos as $prod) {
			if (epc_disc_queue_add_from_fetch($pdo, $siteKey, $prod, $nodeId)) {
				$added++;
				continue;
			}
			$draftRow = array(
				'title' => (string) ($prod['title'] ?? ''),
				'source_domain' => (string) ($prod['source_domain'] ?? ''),
				'suggested_price' => (float) ($prod['price'] ?? 0),
				'specs_json' => json_encode((array) ($prod['specs'] ?? array()), JSON_UNESCAPED_UNICODE),
				'meta_json' => '{}',
			);
			$identityKey = epc_disc_queue_identity_key($pdo, $siteKey, $draftRow);
			if ($identityKey === '') {
				continue;
			}
			$findStmt = $pdo->prepare(
				'SELECT * FROM `epc_product_discovery_queue`
				 WHERE `site_key` = ? AND `status` = \'suggested\' AND `meta_json` LIKE ?
				 ORDER BY `updated_at` DESC LIMIT 5'
			);
			$findStmt->execute(array($siteKey, '%"identity_key":"' . $identityKey . '"%'));
			foreach ($findStmt->fetchAll(PDO::FETCH_ASSOC) ?: array() as $existing) {
				$meta = json_decode((string) ($existing['meta_json'] ?? ''), true);
				if (!is_array($meta)) {
					$meta = array();
				}
				if (!empty($prod['alternate_sources'])) {
					$meta['alternate_sources'] = (array) $prod['alternate_sources'];
				}
				if (!empty($prod['source_prices'])) {
					$meta['source_prices'] = (array) $prod['source_prices'];
				}
				$meta['identity_key'] = $identityKey;
				$meta['cross_source_crawl_at'] = $now;
				$rowCtx = array_merge($existing, array('meta_json' => json_encode($meta, JSON_UNESCAPED_UNICODE)));
				$rangeCompact = epc_disc_build_source_price_range($pdo, $siteKey, $rowCtx);
				epc_disc_persist_source_price_range_meta($meta, $rangeCompact);
				$updStmt->execute(array(json_encode($meta, JSON_UNESCAPED_UNICODE), $now, (int) ($existing['id'] ?? 0), $siteKey));
				$updated++;
			}
		}
	}
	return array(
		'ok' => true,
		'added' => $added,
		'updated' => $updated,
		'message' => "Electronics cross-source: {$added} added, {$updated} updated",
	);
}
