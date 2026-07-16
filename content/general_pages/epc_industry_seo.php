<?php
/**
 * Industry subdomain SEO helpers — public hosts, sub-industry slugs, sitemap URLs.
 */
defined('_ASTEXE_') or die('No access');

/**
 * Slugify a sub-industry label for crawlable paths (e.g. "Biomass & bioenergy" → biomass-bioenergy).
 */
function epc_industry_seo_sub_slug(string $label): string
{
	$s = strtolower(trim($label));
	$s = str_replace(array('&', '+'), ' ', $s);
	$s = preg_replace('/[^a-z0-9]+/', '-', $s) ?: $s;
	return trim($s, '-');
}

/**
 * Presentation variant for a sub-industry page (distinct from the industry hub 3D hero).
 * Deterministic from slug so each vertical keeps a stable look.
 *
 * @return array{key:string,label:string,tone:string}
 */
function epc_industry_seo_sub_presentation(string $slug): array
{
	$variants = array(
		array('key' => 'atelier', 'label' => 'Atelier', 'tone' => 'warm'),
		array('key' => 'ledger', 'label' => 'Ledger', 'tone' => 'ink'),
		array('key' => 'mosaic', 'label' => 'Mosaic', 'tone' => 'bright'),
		array('key' => 'dock', 'label' => 'Dock', 'tone' => 'cool'),
	);
	$idx = (int) (crc32(strtolower($slug)) % count($variants));
	if ($idx < 0) {
		$idx = -$idx;
	}
	return $variants[$idx];
}

/**
 * Category labels for one sub-industry from its template pack (parse-only).
 *
 * @return list<string>
 */
function epc_industry_seo_template_sub_categories(string $templateKey, string $subLabel): array
{
	$templateKey = preg_replace('/[^a-z0-9_]/', '', $templateKey) ?: '';
	$subLabel = trim($subLabel);
	if ($templateKey === '' || $subLabel === '') {
		return array();
	}
	$root = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/');
	$file = $root . '/content/general_pages/industry_templates/' . $templateKey . '.php';
	if (!is_file($file)) {
		return array();
	}
	$src = (string) file_get_contents($file);
	$escaped = preg_quote($subLabel, '/');
	if (!preg_match(
		"/'" . $escaped . "'\\s*=>\\s*array\\s*\\([\\s\\S]{0,2500}?'categories'\\s*=>\\s*array\\s*\\((.*?)\\)/",
		$src,
		$m
	)) {
		return array();
	}
	$out = array();
	if (preg_match_all("/'((?:\\\\'|[^'])*)'/", $m[1], $labels)) {
		foreach ($labels[1] as $label) {
			$label = trim(stripcslashes((string) $label));
			if ($label !== '') {
				$out[] = $label;
			}
		}
	}
	return $out;
}

/**
 * Map template_key / group → DNS-safe public host slug (no underscores).
 * These hosts must resolve via epc_industry_subdomain_resolve_group().
 *
 * @return array<string,string> template_key => host slug
 */
function epc_industry_seo_host_map(): array
{
	return array(
		'automotive' => 'automotive',
		'healthcare' => 'healthcare',
		'food_beverage' => 'food',
		'fashion' => 'fashion',
		'jewellery' => 'jewellery',
		'electronics' => 'electronics',
		'construction' => 'construction',
		'manufacturing' => 'manufacturing',
		'professional' => 'professional',
		'education' => 'education',
		'hospitality' => 'hospitality',
		'beauty' => 'beauty',
		'retail' => 'retail',
		'agriculture' => 'agriculture',
		'logistics' => 'logistics',
		'energy' => 'energy',
		'finance' => 'finance',
		'it_software' => 'technology',
		'media' => 'media',
		'sports' => 'sports',
		'home_living' => 'homeliving',
		'wholesale' => 'wholesale',
		'rental' => 'rental',
		'nonprofit' => 'nonprofit',
		'cleaning' => 'cleaning',
		'pet' => 'pet',
		'printing' => 'printing',
		'security' => 'security',
	);
}

/**
 * Primary public host for an industry group key (e.g. energy → energy.ecomae.com).
 */
function epc_industry_seo_primary_host(string $group): string
{
	$aliasToPrimary = array(
		'realestate' => 'construction',
		'consulting' => 'professional',
		'legal' => 'professional',
		'environmental' => 'energy',
		'telecom' => 'electronics',
		'government' => 'nonprofit',
		'aerospace' => 'manufacturing',
		'mining' => 'manufacturing',
		'food' => 'food',
		'technology' => 'technology',
		'homeliving' => 'homeliving',
	);
	$slug = $aliasToPrimary[$group] ?? $group;
	$map = epc_industry_seo_host_map();
	// If group is a template_key, translate
	if (isset($map[$slug])) {
		$slug = $map[$slug];
	}
	$slug = strtolower(preg_replace('/[^a-z0-9-]/', '', $slug) ?: 'retail');
	return $slug . '.ecomae.com';
}

/**
 * Public Site URL for a consolidation template_key (used on /platform/industries cards).
 */
function epc_industry_seo_site_url_for_template(string $templateKey): string
{
	$map = epc_industry_seo_host_map();
	$slug = $map[$templateKey] ?? preg_replace('/_/', '', $templateKey);
	$slug = strtolower(preg_replace('/[^a-z0-9-]/', '', (string) $slug) ?: 'retail');
	return 'https://' . $slug . '.ecomae.com';
}

/**
 * Load sub-industry labels from an industry template file (parse only — do not execute).
 *
 * @return list<string>
 */
function epc_industry_seo_template_sub_industries(string $templateKey): array
{
	static $cache = array();
	$templateKey = preg_replace('/[^a-z0-9_]/', '', $templateKey) ?: '';
	if ($templateKey === '') {
		return array();
	}
	if (isset($cache[$templateKey])) {
		return $cache[$templateKey];
	}
	$root = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/');
	$file = $root . '/content/general_pages/industry_templates/' . $templateKey . '.php';
	if (!is_file($file)) {
		$cache[$templateKey] = array();
		return array();
	}
	$src = (string) file_get_contents($file);
	$subs = array();
	if (preg_match("/'sub_industries'\\s*=>\\s*array\\s*\\((.*?)\\)\\s*,/s", $src, $m)) {
		if (preg_match_all("/'((?:\\\\'|[^'])*)'/", $m[1], $labels)) {
			foreach ($labels[1] as $label) {
				$label = stripcslashes((string) $label);
				$label = trim($label);
				if ($label !== '') {
					$subs[] = $label;
				}
			}
		}
	}
	$cache[$templateKey] = $subs;
	return $subs;
}

/**
 * Absolute industry + sub-industry URLs for sitemaps.
 *
 * @return list<array{0:string,1:string,2:string}> [absoluteUrl, priority, changefreq]
 */
function epc_industry_seo_sitemap_entries(): array
{
	$entries = array();
	$seen = array();
	if (!function_exists('epc_industry_groups')) {
		$c = ($_SERVER['DOCUMENT_ROOT'] ?? '') . '/content/general_pages/epc_industry_consolidation.php';
		if (is_file($c)) {
			require_once $c;
		}
	}
	if (!function_exists('epc_industry_groups')) {
		return $entries;
	}
	foreach (epc_industry_groups() as $ginfo) {
		$tk = (string) ($ginfo['template_key'] ?? '');
		if ($tk === '') {
			continue;
		}
		$base = rtrim(epc_industry_seo_site_url_for_template($tk), '/');
		if ($base === '' || isset($seen[$base])) {
			continue;
		}
		$seen[$base] = true;
		$entries[] = array($base . '/', '0.85', 'weekly');

		$subs = epc_industry_seo_template_sub_industries($tk);
		if ($subs === array() && !empty($ginfo['available_sub_areas']) && is_array($ginfo['available_sub_areas'])) {
			$subs = array_values($ginfo['available_sub_areas']);
		}
		foreach ($subs as $label) {
			$slug = epc_industry_seo_sub_slug((string) $label);
			if ($slug === '') {
				continue;
			}
			$url = $base . '/' . $slug;
			if (isset($seen[$url])) {
				continue;
			}
			$seen[$url] = true;
			$entries[] = array($url, '0.7', 'monthly');
		}
	}
	return $entries;
}

/**
 * Resolve active sub-industry from the request path against a label list.
 *
 * @param list<string> $subIndustries
 * @return array{label:string,slug:string}|null
 */
function epc_industry_seo_match_request_sub(array $subIndustries): ?array
{
	$path = (string) (parse_url($_SERVER['REQUEST_URI'] ?? '/', PHP_URL_PATH) ?: '/');
	$path = strtolower(trim($path, '/'));
	if ($path === '' || $path === 'index.php') {
		return null;
	}
	// Ignore reserved / system paths
	if (preg_match('#^(cp|erp|api|platform|documentation|sitemap\.xml|robots\.txt|epc-)#', $path)) {
		return null;
	}
	// Only first path segment
	$seg = explode('/', $path, 2)[0];
	$seg = preg_replace('/[^a-z0-9-]/', '', $seg) ?: '';
	if ($seg === '') {
		return null;
	}
	foreach ($subIndustries as $label) {
		$canon = epc_industry_seo_sub_slug((string) $label);
		if ($canon === $seg) {
			return array('label' => (string) $label, 'slug' => $canon);
		}
	}
	// Short aliases: /biomass or /bioenergy → "Biomass & bioenergy"
	foreach ($subIndustries as $label) {
		$canon = epc_industry_seo_sub_slug((string) $label);
		$parts = array_filter(explode('-', $canon));
		if (in_array($seg, $parts, true) && strlen($seg) >= 4) {
			return array('label' => (string) $label, 'slug' => $canon);
		}
	}
	return null;
}
