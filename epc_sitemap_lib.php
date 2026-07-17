<?php
declare(strict_types=1);

require_once __DIR__ . '/config.php';

function epc_sitemap_pdo(DP_Config $cfg): ?PDO
{
	try {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db,
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$pdo->query('SET NAMES utf8');
		return $pdo;
	} catch (Exception $e) {
		return null;
	}
}

function epc_sitemap_base_url(DP_Config $cfg): string
{
	require_once __DIR__ . '/content/general_pages/epc_portal.php';
	epc_portal_apply_config($cfg);
	$base = rtrim((string) $cfg->domain_path, '/');
	if ($base === '' || strpos($base, 'localhost') !== false) {
		$guessed = epc_portal_guess_domain_path();
		if ($guessed !== '') {
			$base = rtrim($guessed, '/');
		}
	}
	return $base;
}

/** True when the request host is an industry marketing subdomain (energy.ecomae.com, …). */
function epc_sitemap_is_industry_host(): bool
{
	$seo = __DIR__ . '/content/general_pages/epc_industry_seo.php';
	if (is_file($seo)) {
		if (!defined('_ASTEXE_')) {
			define('_ASTEXE_', 1);
		}
		require_once $seo;
		if (function_exists('epc_industry_seo_is_industry_host')) {
			return epc_industry_seo_is_industry_host();
		}
	}
	$host = strtolower(trim((string) ($_SERVER['HTTP_HOST'] ?? '')));
	if ($host !== '' && strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	return (bool) preg_match('/^[a-z0-9][a-z0-9_-]*\.ecomae\.com$/', $host)
		&& $host !== 'www.ecomae.com';
}

/**
 * Prefer the request host for industry subdomains so sitemap-index.php does not
 * rewrite locs to www.ecomae.com (which drops sub-industry URLs from discovery).
 */
function epc_sitemap_request_host_base(DP_Config $cfg): string
{
	$seo = __DIR__ . '/content/general_pages/epc_industry_seo.php';
	if (is_file($seo)) {
		if (!defined('_ASTEXE_')) {
			define('_ASTEXE_', 1);
		}
		require_once $seo;
		if (function_exists('epc_industry_seo_request_host_base')) {
			$base = epc_industry_seo_request_host_base();
			if ($base !== '') {
				return $base;
			}
		}
	}
	return epc_sitemap_base_url($cfg);
}

function epc_sitemap_lang(): string
{
	return 'en';
}

function epc_sitemap_parts_root(DP_Config $cfg): string
{
	return isset($cfg->chpu_search_config['level_1']['url']) ? (string) $cfg->chpu_search_config['level_1']['url'] : 'parts';
}

function epc_sitemap_slash_code(DP_Config $cfg): string
{
	return isset($cfg->chpu_search_config['slash_code']) ? (string) $cfg->chpu_search_config['slash_code'] : '&ms;';
}

function epc_sitemap_segment($value, $slash_code): string
{
	$value = trim((string) $value);
	$value = str_replace('/', $slash_code, $value);
	return rawurlencode($value);
}

function epc_sitemap_parts_path(DP_Config $cfg, $lang, $parts_root, $slash_code, $path): string
{
	$base = epc_sitemap_base_url($cfg);
	$path = trim((string) $path, '/');
	$suffix = $path !== '' ? '/' . $path : '';
	return htmlspecialchars($base . '/' . $lang . '/' . $parts_root . $suffix, ENT_XML1, 'UTF-8');
}

/**
 * Absolute warehouse part loc matching live CHPU:
 *   https://www.epartscart.com/en/parts/{BRAND}/{ARTICLE}
 * Never /parts/brands/{article} (that path 302s and is robots-disallowed).
 */
function epc_sitemap_part_loc(DP_Config $cfg, string $lang, string $brand, string $article): string
{
	$match = __DIR__ . '/content/shop/docpart/docpart_article_match.php';
	if (is_file($match)) {
		require_once $match;
	}
	if (!function_exists('epc_chpu_build_part_url')) {
		$slash = epc_sitemap_slash_code($cfg);
		$parts = epc_sitemap_parts_root($cfg);
		$path = epc_sitemap_segment(mb_strtoupper(trim($brand), 'UTF-8'), $slash)
			. '/' . epc_sitemap_segment(docpart_normalize_article_for_price($article), $slash);
		return epc_sitemap_parts_path($cfg, $lang, $parts, $slash, $path);
	}
	$rel = epc_chpu_build_part_url($cfg, '/' . trim($lang, '/'), $brand, $article);
	if ($rel === '') {
		return '';
	}
	$base = rtrim(epc_sitemap_base_url($cfg), '/');
	// epc_chpu_build_part_url returns /en/parts/BRAND/ARTICLE
	if (isset($rel[0]) && $rel[0] === '/') {
		return htmlspecialchars($base . $rel, ENT_XML1, 'UTF-8');
	}
	return htmlspecialchars($base . '/' . ltrim($rel, '/'), ENT_XML1, 'UTF-8');
}

function epc_sitemap_page_url(DP_Config $cfg, $lang, $path): string
{
	$base = epc_sitemap_base_url($cfg);
	$path = ltrim((string) $path, '/');
	return htmlspecialchars($base . '/' . $lang . '/' . $path, ENT_XML1, 'UTF-8');
}

function epc_sitemap_emit_urlset(array $entries): void
{
	echo "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";
	echo "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n";
	foreach ($entries as $entry) {
		echo "\t<url>\n";
		echo "\t\t<loc>" . $entry['loc'] . "</loc>\n";
		echo "\t\t<lastmod>" . $entry['lastmod'] . "</lastmod>\n";
		echo "\t\t<changefreq>" . $entry['changefreq'] . "</changefreq>\n";
		echo "\t\t<priority>" . $entry['priority'] . "</priority>\n";
		echo "\t</url>\n";
	}
	echo "</urlset>\n";
}

function epc_sitemap_add_entry(array &$entries, array &$seen, $loc, $lastmod, $changefreq, $priority): void
{
	if (isset($seen[$loc])) {
		return;
	}
	$seen[$loc] = true;
	$entries[] = array(
		'loc' => $loc,
		'lastmod' => $lastmod,
		'changefreq' => $changefreq,
		'priority' => $priority,
	);
}
