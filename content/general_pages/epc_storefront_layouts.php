<?php
/**
 * Multi-template layout system — each industry has 3-4 layout variants.
 *
 * Layouts are independent of colour themes. A tenant picks:
 *   1. An industry (electronics, fashion, jewellery, consulting, etc.)
 *   2. A LAYOUT (hero-first, grid-first, minimal, brand-focus, etc.)
 *   3. A COLOUR THEME (classic, modern, midnight, signature)
 *
 * The layout determines the homepage structure (hero banner vs category grid vs
 * product showcase vs editorial). The colour theme sets the palette.
 *
 * Tenants choose their layout from CP > Industry Settings > Storefront Layout.
 * Stored in site_settings as `storefront_layout` (separate from `storefront_package`).
 */
defined('_ASTEXE_') or die('No access');

function epc_storefront_layout_registry(): array
{
	return array(
		'electronics' => array(
			array(
				'id' => 'hero_carousel',
				'label' => 'Hero Carousel',
				'desc' => 'Full-width sliding hero banner with deal sections below — Virgin Megastore style',
				'preview' => 'hero-carousel-preview',
				'default' => true,
				'sections' => array('hero_carousel', 'promo_strip', 'featured_categories', 'deal_sections', 'brand_strip'),
			),
			array(
				'id' => 'category_grid',
				'label' => 'Category Grid First',
				'desc' => 'Category tiles on top, trending products below — Amazon style navigation',
				'preview' => 'category-grid-preview',
				'sections' => array('category_grid', 'trending_products', 'deal_sections', 'brand_strip'),
			),
			array(
				'id' => 'product_showcase',
				'label' => 'Product Showcase',
				'desc' => 'Clean product grid with filters — minimal, product-focused storefront',
				'preview' => 'product-showcase-preview',
				'sections' => array('search_bar_hero', 'product_grid', 'category_sidebar', 'brand_strip'),
			),
			array(
				'id' => 'brand_focused',
				'label' => 'Brand Focused',
				'desc' => 'Brand logos first, shop by brand — Best Buy style brand-centric',
				'preview' => 'brand-focused-preview',
				'sections' => array('brand_hero', 'brand_grid', 'featured_products', 'deal_sections'),
			),
		),
		'fashion' => array(
			array(
				'id' => 'editorial',
				'label' => 'Editorial Lookbook',
				'desc' => 'Full-screen lifestyle imagery with collection links — Namshi/ASOS style',
				'preview' => 'editorial-preview',
				'default' => true,
				'sections' => array('hero_lifestyle', 'collection_chips', 'for_you_grid', 'brand_filters', 'trend_sections'),
			),
			array(
				'id' => 'collection_grid',
				'label' => 'Collection Grid',
				'desc' => 'Visual collection tiles with hover effects — Pinterest style discovery',
				'preview' => 'collection-grid-preview',
				'sections' => array('seasonal_banner', 'collection_tiles', 'new_arrivals', 'shop_by_category'),
			),
			array(
				'id' => 'minimal_boutique',
				'label' => 'Minimal Boutique',
				'desc' => 'Clean white space, large product photography — luxury boutique feel',
				'preview' => 'minimal-boutique-preview',
				'sections' => array('featured_product', 'curated_edit', 'new_in', 'bestsellers'),
			),
			array(
				'id' => 'trend_feed',
				'label' => 'Trend Feed',
				'desc' => 'Social-media style scrollable feed with shoppable posts — Gen Z appeal',
				'preview' => 'trend-feed-preview',
				'sections' => array('stories_strip', 'trend_feed', 'flash_deals', 'style_guides'),
			),
		),
		'jewellery' => array(
			array(
				'id' => 'luxury_showcase',
				'label' => 'Luxury Showcase',
				'desc' => 'Full-width hero with gold accents, collection highlights — Kiyasha style',
				'preview' => 'luxury-showcase-preview',
				'default' => true,
				'sections' => array('hero_luxury', 'collection_highlights', 'featured_pieces', 'gold_price_ticker'),
			),
			array(
				'id' => 'collection_gallery',
				'label' => 'Collection Gallery',
				'desc' => 'Visual collection cards (Bridal, Gold, Diamond) as main navigation',
				'preview' => 'collection-gallery-preview',
				'sections' => array('collection_cards', 'bestsellers', 'new_arrivals', 'gold_rate'),
			),
			array(
				'id' => 'catalog_filter',
				'label' => 'Catalog with Filters',
				'desc' => 'Product grid with karat/weight/price filters — search-first approach',
				'preview' => 'catalog-filter-preview',
				'sections' => array('search_hero', 'filter_sidebar', 'product_grid', 'collection_links'),
			),
			array(
				'id' => 'editorial_luxury',
				'label' => 'Editorial Luxury',
				'desc' => 'Magazine-style with lifestyle imagery and storytelling — Tiffany style',
				'preview' => 'editorial-luxury-preview',
				'sections' => array('editorial_hero', 'story_sections', 'featured_collection', 'artisan_spotlight'),
			),
		),
		'tax_advisory' => array(
			array(
				'id' => 'professional_services',
				'label' => 'Professional Services',
				'desc' => 'Service cards with pricing tiers, trust signals — PrimeInvest style',
				'preview' => 'professional-services-preview',
				'default' => true,
				'sections' => array('hero_services', 'service_cards', 'pricing_tiers', 'testimonials', 'cta_consultation'),
			),
			array(
				'id' => 'calculator_led',
				'label' => 'Calculator Led',
				'desc' => 'Interactive calculators (VAT, CT) as hero — conversion-focused',
				'preview' => 'calculator-led-preview',
				'sections' => array('calculator_hero', 'quick_services', 'case_studies', 'faq_accordion'),
			),
			array(
				'id' => 'corporate_clean',
				'label' => 'Corporate Clean',
				'desc' => 'Minimal corporate with team photos, credentials — Big 4 inspired',
				'preview' => 'corporate-clean-preview',
				'sections' => array('hero_minimal', 'expertise_areas', 'team_grid', 'credentials', 'contact_form'),
			),
		),
		'consultancy' => array(
			array(
				'id' => 'professional_services',
				'label' => 'Professional Services',
				'desc' => 'Service cards with pricing, trust signals',
				'preview' => 'professional-services-preview',
				'default' => true,
				'sections' => array('hero_services', 'service_cards', 'pricing_tiers', 'testimonials', 'cta_consultation'),
			),
			array(
				'id' => 'calculator_led',
				'label' => 'Calculator Led',
				'desc' => 'Interactive calculators as hero — conversion-focused',
				'preview' => 'calculator-led-preview',
				'sections' => array('calculator_hero', 'quick_services', 'case_studies', 'faq_accordion'),
			),
			array(
				'id' => 'corporate_clean',
				'label' => 'Corporate Clean',
				'desc' => 'Minimal corporate with team photos',
				'preview' => 'corporate-clean-preview',
				'sections' => array('hero_minimal', 'expertise_areas', 'team_grid', 'credentials', 'contact_form'),
			),
		),
	);
}

function epc_storefront_layouts_for_industry(string $industry): array
{
	$all = epc_storefront_layout_registry();
	return isset($all[$industry]) ? $all[$industry] : array();
}

function epc_storefront_layout_default(string $industry): string
{
	$layouts = epc_storefront_layouts_for_industry($industry);
	foreach ($layouts as $layout) {
		if (!empty($layout['default'])) {
			return $layout['id'];
		}
	}
	return !empty($layouts[0]['id']) ? $layouts[0]['id'] : 'hero_carousel';
}

function epc_storefront_layout_meta(string $industry, string $layoutId): ?array
{
	$layouts = epc_storefront_layouts_for_industry($industry);
	foreach ($layouts as $layout) {
		if ($layout['id'] === $layoutId) {
			return $layout;
		}
	}
	return null;
}

function epc_storefront_layout_sections(string $industry, string $layoutId): array
{
	$meta = epc_storefront_layout_meta($industry, $layoutId);
	return ($meta !== null && isset($meta['sections'])) ? $meta['sections'] : array();
}

function epc_storefront_active_layout(array $settings = null): string
{
	if ($settings === null) {
		require_once __DIR__ . '/epc_portal.php';
		$settings = epc_portal_load_site_settings();
	}
	$layout = isset($settings['storefront_layout']) ? trim((string) $settings['storefront_layout']) : '';
	if ($layout !== '') {
		return $layout;
	}
	$industry = isset($settings['industry_code']) ? (string) $settings['industry_code'] : '';
	return epc_storefront_layout_default($industry);
}

function epc_storefront_layouts_for_js(): array
{
	$all = epc_storefront_layout_registry();
	$out = array();
	foreach ($all as $industry => $layouts) {
		$out[$industry] = array();
		foreach ($layouts as $layout) {
			$out[$industry][] = array(
				'id' => $layout['id'],
				'label' => $layout['label'],
				'desc' => $layout['desc'],
				'default' => !empty($layout['default']),
				'sections' => $layout['sections'],
			);
		}
	}
	return $out;
}
