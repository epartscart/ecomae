<?php
/**
 * Automotive spare parts storefront defaults (nav hints, hero, CTAs, quick links).
 * Tenants override colours via site_settings.theme / theme_template; copy via tagline or future JSON.
 */
defined('_ASTEXE_') or die('No access');

function epc_asp_home_lang(array $multilang_params = null): string
{
	if ($multilang_params === null) {
		global $multilang_params;
	}
	return (isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '')
		? (string) $multilang_params['lang_href']
		: '/en';
}

function epc_asp_hero_eyebrow(): string
{
	return 'Professional auto spare parts store';
}

function epc_asp_hero_title(): string
{
	return 'Find genuine and aftermarket spare parts faster, cleaner, and with confidence.';
}

function epc_asp_hero_copy(): string
{
	return 'Search by article number, browse brands, open the electronic catalog, or send a VIN request. Everything is arranged for workshops, garages, fleet buyers, and retail customers.';
}

/** @return array<int, array{label: string, href: string, icon: string, primary?: bool}> */
function epc_asp_hero_actions(string $lang): array
{
	$l = rtrim($lang, '/');
	return array(
		array('label' => 'Search parts', 'href' => $l . '/parts', 'icon' => 'fa-search', 'primary' => true),
		array('label' => 'Open catalog', 'href' => $l . '/umapi_catalog', 'icon' => 'fa-cubes'),
		array('label' => 'View brands', 'href' => $l . '/available-brands', 'icon' => 'fa-tags'),
	);
}

/** @return array<int, array{value: string, label: string}> */
function epc_asp_hero_stats(): array
{
	return array(
		array('value' => 'Fast', 'label' => 'article search'),
		array('value' => 'VIN', 'label' => 'request support'),
		array('value' => 'AED', 'label' => 'clear pricing'),
	);
}

/** @return array<int, array{title: string, text: string, href: string, icon: string, tone: string}> */
function epc_asp_home_banners(string $lang): array
{
	$l = rtrim($lang, '/');
	return array(
		array(
			'title' => 'Search by part number',
			'text' => 'Quickly check price, availability and delivery time.',
			'href' => $l . '/parts',
			'icon' => 'fa-barcode',
			'tone' => 'red',
		),
		array(
			'title' => 'Trusted brands',
			'text' => 'Browse available manufacturers and product lines.',
			'href' => $l . '/available-brands',
			'icon' => 'fa-shield',
			'tone' => 'blue',
		),
		array(
			'title' => 'Electronic catalog',
			'text' => 'Find parts through vehicle catalog navigation.',
			'href' => $l . '/umapi_catalog',
			'icon' => 'fa-sitemap',
			'tone' => 'green',
		),
		array(
			'title' => 'VIN request',
			'text' => 'Ask sellers to identify the exact spare part.',
			'href' => $l . '/zapros-prodavczu',
			'icon' => 'fa-car',
			'tone' => 'dark',
		),
	);
}

function epc_asp_store_hours_line(): string
{
	return 'Mon–Sat 9:00–18:00 · UAE';
}
