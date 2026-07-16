<?php
/**
 * ecomae — Industry subdomain router.
 *
 * Detects [industry].ecomae.com wildcard subdomains and sets platform context
 * so the storefront renders the marketing home page themed for that industry.
 *
 * Must be loaded early in index.php BEFORE epc_portal_apply_config().
 */
defined('_ASTEXE_') or die('No access');

/**
 * Check if the current request host is an industry wildcard subdomain.
 * Pattern: [slug].ecomae.com where [slug] is NOT www, cp, etc.
 */
function epc_industry_subdomain_detect(): ?string
{
	static $result = false;
	if ($result !== false) {
		return $result;
	}
	$host = '';
	if (!empty($_SERVER['HTTP_HOST'])) {
		$host = strtolower(trim((string) $_SERVER['HTTP_HOST']));
	}
	if ($host !== '' && strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	if ($host === '' && !empty($_SERVER['SERVER_NAME'])) {
		$host = strtolower(trim((string) $_SERVER['SERVER_NAME']));
	}
	// Must end with .ecomae.com
	if (!preg_match('/^([a-z0-9][a-z0-9_-]*)\.ecomae\.com$/', $host, $m)) {
		$result = null;
		return null;
	}
	$slug = $m[1];
	// Exclude known platform/system subdomains
	$reserved = array('www', 'cp', 'api', 'mail', 'smtp', 'ftp', 'ns1', 'ns2', 'cdn', 'admin', 'www1', 'asap');
	if (in_array($slug, $reserved, true)) {
		$result = null;
		return null;
	}
	$result = $slug;
	return $slug;
}

/**
 * Check if the current host is an industry wildcard subdomain.
 */
function epc_is_industry_subdomain(): bool
{
	return epc_industry_subdomain_detect() !== null;
}

/**
 * Resolve the industry subdomain slug to a consolidated industry group key.
 * E.g., "construction" → "construction", "medicalequipment" → "healthcare", "automotive" → "automotive"
 */
function epc_industry_subdomain_resolve_group(string $slug): string
{
	// Direct match to group keys
	$directGroups = array(
		'construction', 'healthcare', 'automotive', 'food', 'hospitality',
		'beauty', 'education', 'energy', 'manufacturing', 'agriculture',
		'technology', 'finance', 'logistics', 'realestate', 'media',
		'legal', 'sports', 'environmental', 'aerospace', 'mining',
		'telecom', 'retail', 'fashion', 'jewellery', 'electronics',
		'consulting', 'government', 'nonprofit',
		'wholesale', 'rental', 'cleaning', 'pet', 'printing', 'security',
		'homeliving', 'professional',
	);
	if (in_array($slug, $directGroups, true)) {
		return $slug;
	}
	// Alias mapping (subdomain slug → group key)
	$aliases = array(
		'medical' => 'healthcare',
		'medicalequipment' => 'healthcare',
		'health' => 'healthcare',
		'pharma' => 'healthcare',
		'pharmacy' => 'healthcare',
		'dental' => 'healthcare',
		'hospital' => 'healthcare',
		'auto' => 'automotive',
		'autoparts' => 'automotive',
		'cars' => 'automotive',
		'vehicles' => 'automotive',
		'spare' => 'automotive',
		'spareparts' => 'automotive',
		'foodbeverage' => 'food',
		'restaurant' => 'food',
		'catering' => 'food',
		'bakery' => 'food',
		'hotel' => 'hospitality',
		'hotels' => 'hospitality',
		'tourism' => 'hospitality',
		'travel' => 'hospitality',
		'salon' => 'beauty',
		'cosmetics' => 'beauty',
		'skincare' => 'beauty',
		'school' => 'education',
		'university' => 'education',
		'training' => 'education',
		'solar' => 'energy',
		'oil' => 'energy',
		'gas' => 'energy',
		'petroleum' => 'energy',
		'power' => 'energy',
		'utility' => 'energy',
		'utilities' => 'energy',
		'renewable' => 'energy',
		'electric' => 'energy',
		'biomass' => 'energy',
		'bioenergy' => 'energy',
		'biogas' => 'energy',
		'hydrogen' => 'energy',
		'geothermal' => 'energy',
		'nuclear' => 'energy',
		'wind' => 'energy',
		'ev' => 'energy',
		'evcharging' => 'energy',
		'battery' => 'energy',
		'grid' => 'energy',
		'energy_utilities' => 'energy',
		'factory' => 'manufacturing',
		'industrial' => 'manufacturing',
		'production' => 'manufacturing',
		'farming' => 'agriculture',
		'tech' => 'technology',
		'software' => 'technology',
		'it' => 'technology',
		'fintech' => 'finance',
		'banking' => 'finance',
		'insurance' => 'finance',
		'accounting' => 'finance',
		'tax' => 'consulting',
		'shipping' => 'logistics',
		'transport' => 'logistics',
		'freight' => 'logistics',
		'warehouse' => 'logistics',
		'property' => 'realestate',
		'properties' => 'realestate',
		'clothing' => 'fashion',
		'apparel' => 'fashion',
		'gold' => 'jewellery',
		'diamonds' => 'jewellery',
		'gadgets' => 'electronics',
		'computers' => 'electronics',
		'phones' => 'electronics',
		'advisory' => 'consulting',
		'consultancy' => 'consulting',
		'law' => 'legal',
		'fitness' => 'sports',
		'gym' => 'sports',
		'green' => 'environmental',
		'recycling' => 'environmental',
		'aviation' => 'aerospace',
		'defense' => 'aerospace',
		'defence' => 'aerospace',
		'telco' => 'telecom',
		'mobile' => 'telecom',
		'shopping' => 'retail',
		'ecommerce' => 'retail',
		'supermarket' => 'retail',
		'ngo' => 'nonprofit',
		'charity' => 'nonprofit',
		'furniture' => 'homeliving',
		'interiors' => 'homeliving',
		'home' => 'homeliving',
		'distribution' => 'wholesale',
		'trading' => 'wholesale',
		'leasing' => 'rental',
		'hire' => 'rental',
		'facilities' => 'cleaning',
		'maintenance' => 'cleaning',
		'veterinary' => 'pet',
		'petcare' => 'pet',
		'signage' => 'printing',
		'print' => 'printing',
		'surveillance' => 'security',
		'guard' => 'security',
		'cctv' => 'security',
	);
	if (isset($aliases[$slug])) {
		return $aliases[$slug];
	}
	// Fuzzy match: check if slug starts with or contains a known group
	foreach ($directGroups as $group) {
		if (strpos($slug, $group) !== false) {
			return $group;
		}
	}
	// Default to technology (general purpose)
	return 'technology';
}

/**
 * Bootstrap the industry subdomain context.
 * Sets globals and DP_Config so the page renders as platform marketing with industry theming.
 */
function epc_industry_subdomain_bootstrap($DP_Config): bool
{
	$slug = epc_industry_subdomain_detect();
	if ($slug === null) {
		return false;
	}
	$group = epc_industry_subdomain_resolve_group($slug);
	// Set globals for template theming
	$GLOBALS['epc_industry_subdomain_active'] = true;
	$GLOBALS['epc_industry_subdomain_slug'] = $slug;
	$GLOBALS['epc_industry_subdomain_group'] = $group;
	// Set domain_path to match the incoming host (passes dp_core license check)
	$host = strtolower(trim((string) ($_SERVER['HTTP_HOST'] ?? '')));
	if ($host !== '' && strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	if (is_object($DP_Config)) {
		$DP_Config->domain_path = 'https://' . $host . '/';
		$DP_Config->epc_portal_industry = $group;
	}
	return true;
}
