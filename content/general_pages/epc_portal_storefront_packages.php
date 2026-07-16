<?php
/**
 * Reusable storefront theme packages (layout chrome + homepage), separate from colour slots.
 *
 * Registry ids: automotive_spareparts_pro, electronics_retail_virgin, consulting_primeinvest,
 * fashion_retail_namshi, jewellery_retail_kiyasha (Shop Kiyasha parity).
 */
defined('_ASTEXE_') or die('No access');

/** @return array<string, array<string, mixed>> */
function epc_portal_storefront_package_registry()
{
	return array(
		'automotive_spareparts_pro' => array(
			'id' => 'automotive_spareparts_pro',
			'label' => 'Automotive spare parts pro',
			'desc' => 'Legacy eParts Cart — animated SVG logo, Nero automotive header, piston hero + quick-link banners',
			'industry_codes' => array('auto_parts'),
			'theme_template' => 'classic',
			'access_mode' => 'full',
			'enabled_packs' => array('core', 'commerce', 'auto_parts', 'logistics', 'erp', 'professional', 'marketing'),
			'tagline' => 'Auto parts & commerce',
			'implemented' => true,
			'css' => '/content/general_pages/epc_automotive_spareparts.css',
			'home' => '/content/general_pages/epc_portal_automotive_spareparts_home.php',
			'piston' => '/content/general_pages/epc_automotive_spareparts_piston_banner.php',
			'preset_file' => '/content/general_pages/epc_theme_presets/automotive_spareparts_pro.json',
		),
		'electronics_retail_virgin' => array(
			'id' => 'electronics_retail_virgin',
			'label' => 'Virgin Megastore retail',
			'desc' => 'Black utility bar, mega nav, retail footer, demo catalogue homepage — Virgin Megastore AE parity',
			'industry_codes' => array('electronics'),
			'theme_template' => 'midnight',
			'access_mode' => 'full',
			'enabled_packs' => array('core', 'commerce', 'catalogue'),
			'tagline' => 'Shop Online for Tech, Gaming, Toys & More',
			'css' => '/content/general_pages/epc_electronics_retail.css',
			'home' => '/content/general_pages/epc_portal_electronics_retail_home.php',
			'hero' => '/content/general_pages/epc_electronics_retail_virgin_hero_banner.php',
			'hero_css' => '/content/general_pages/epc_electronics_retail_virgin_hero.css',
			'header' => '/content/general_pages/epc_portal_electronics_retail_header.php',
			'footer' => '/content/general_pages/epc_portal_electronics_retail_footer.php',
			'preset_file' => '/content/general_pages/epc_theme_presets/electronics_retail_virgin.json',
		),
		'consulting_primeinvest' => array(
			'id' => 'consulting_primeinvest',
			'label' => 'Prime Invest consulting',
			'desc' => 'Finance advisory layout — green Rubik chrome, services hero, ERP CTAs (PrimeInvest WP theme patterns)',
			'industry_codes' => array('tax_advisory', 'consultancy'),
			'theme_template' => 'modern',
			'access_mode' => 'consultancy',
			'enabled_packs' => array('core', 'erp', 'professional', 'tax_advisory'),
			'tagline' => 'Tax & advisory services — UAE corporate tax, VAT and business compliance',
			'css' => '/content/general_pages/epc_consulting_primeinvest.css',
			'home' => '/content/general_pages/epc_portal_consulting_primeinvest_home.php',
			'hero' => '/content/general_pages/epc_consulting_primeinvest_hero_banner.php',
			'hero_css' => '/content/general_pages/epc_consulting_primeinvest_hero.css',
			'header' => '/content/general_pages/epc_portal_consulting_primeinvest_header.php',
			'footer' => '/content/general_pages/epc_portal_consulting_primeinvest_footer.php',
			'preset_file' => '/content/general_pages/epc_theme_presets/consulting_primeinvest.json',
		),
		'fashion_retail_namshi' => array(
			'id' => 'fashion_retail_namshi',
			'label' => 'Namshi fashion & beauty retail',
			'desc' => 'Clean white layout, category chips, For You product grids, brand filters — Namshi UAE beauty parity',
			'industry_codes' => array('fashion'),
			'theme_template' => 'signature',
			'access_mode' => 'full',
			'enabled_packs' => array('core', 'commerce', 'catalogue'),
			'tagline' => 'Fashion, beauty & lifestyle — UAE delivery, prices in AED',
			'implemented' => true,
			'css' => '/content/general_pages/epc_fashion_retail_namshi.css',
			'home' => '/content/general_pages/epc_portal_fashion_retail_namshi_home.php',
			'hero' => '/content/general_pages/epc_fashion_retail_namshi_hero_banner.php',
			'hero_css' => '/content/general_pages/epc_fashion_retail_namshi_hero.css',
			'header' => '/content/general_pages/epc_portal_fashion_retail_namshi_header.php',
			'footer' => '/content/general_pages/epc_portal_fashion_retail_namshi_footer.php',
			'preset_file' => '/content/general_pages/epc_theme_presets/fashion_retail_namshi.json',
		),
		'jewellery_retail_kiyasha' => array(
			'id' => 'jewellery_retail_kiyasha',
			'label' => 'Kiyasha jewellery luxury retail',
			'desc' => 'Gold & diamond aesthetic, hero banners, collection grids, product cards — Shop Kiyasha / fine jewellery parity',
			'industry_codes' => array('jewellery'),
			'theme_template' => 'classic',
			'access_mode' => 'full',
			'enabled_packs' => array('core', 'commerce', 'catalogue'),
			'tagline' => 'Fine gold, diamonds & bridal jewellery — UAE delivery, prices in AED',
			'implemented' => true,
			'css' => '/content/general_pages/epc_jewellery_retail_kiyasha.css',
			'home' => '/content/general_pages/epc_portal_jewellery_retail_kiyasha_home.php',
			'hero' => '/content/general_pages/epc_jewellery_retail_kiyasha_hero_banner.php',
			'hero_css' => '/content/general_pages/epc_jewellery_retail_kiyasha_hero.css',
			'header' => '/content/general_pages/epc_portal_jewellery_retail_kiyasha_header.php',
			'footer' => '/content/general_pages/epc_portal_jewellery_retail_kiyasha_footer.php',
			'preset_file' => '/content/general_pages/epc_theme_presets/jewellery_retail_kiyasha.json',
		),
	);
}

function epc_portal_storefront_package_meta($packageId)
{
	$id = preg_replace('/[^a-z0-9_]/', '', (string) $packageId);
	$all = epc_portal_storefront_package_registry();
	return isset($all[$id]) ? $all[$id] : null;
}

function epc_portal_storefront_package_for_industry($industryCode)
{
	$code = preg_replace('/[^a-z0-9_]/', '', (string) $industryCode);
	foreach (epc_portal_storefront_package_registry() as $pkg) {
		if (!empty($pkg['industry_codes']) && in_array($code, $pkg['industry_codes'], true)) {
			return $pkg['id'];
		}
	}
	return '';
}

function epc_portal_resolve_storefront_package(array $settings = null)
{
	if ($settings === null) {
		require_once __DIR__ . '/epc_portal.php';
		$settings = epc_portal_load_site_settings();
	}
	$contact = isset($settings['contact']) && is_array($settings['contact']) ? $settings['contact'] : array();
	if (!empty($contact['storefront_package'])) {
		$explicit = preg_replace('/[^a-z0-9_]/', '', (string) $contact['storefront_package']);
		if ($explicit !== '' && epc_portal_storefront_package_meta($explicit)) {
			$meta = epc_portal_storefront_package_meta($explicit);
			if (isset($meta['implemented']) && $meta['implemented'] === false) {
				return '';
			}
			return $explicit;
		}
	}
	$industry = isset($settings['industry_code']) ? (string) $settings['industry_code'] : '';
	if ($industry === 'auto_parts') {
		return 'automotive_spareparts_pro';
	}
	if ($industry === 'electronics') {
		return 'electronics_retail_virgin';
	}
	if (in_array($industry, array('tax_advisory', 'consultancy'), true)) {
		return 'consulting_primeinvest';
	}
	if ($industry === 'fashion') {
		return 'fashion_retail_namshi';
	}
	if ($industry === 'jewellery') {
		return 'jewellery_retail_kiyasha';
	}
	return '';
}

/** Packages with full layout implementation (excludes registry placeholders). */
function epc_portal_storefront_package_implemented_ids()
{
	$out = array();
	foreach (epc_portal_storefront_package_registry() as $id => $row) {
		if (!isset($row['implemented']) || $row['implemented'] !== false) {
			$out[] = $id;
		}
	}
	return $out;
}

function epc_portal_storefront_package_preset($packageId)
{
	$meta = epc_portal_storefront_package_meta($packageId);
	if ($meta === null) {
		return array();
	}
	require_once __DIR__ . '/epc_portal_theme_templates.php';
	$industry = !empty($meta['industry_codes'][0]) ? $meta['industry_codes'][0] : 'electronics';
	$template = isset($meta['theme_template']) ? $meta['theme_template'] : 'midnight';
	return array(
		'storefront_package' => $meta['id'],
		'industry_code' => $industry,
		'theme_template' => $template,
		'access_mode' => isset($meta['access_mode']) ? $meta['access_mode'] : 'full',
		'enabled_packs' => isset($meta['enabled_packs']) ? $meta['enabled_packs'] : array('core', 'commerce', 'catalogue'),
		'tagline' => isset($meta['tagline']) ? $meta['tagline'] : '',
		'theme' => epc_portal_style_template_theme($industry, $template),
		'contact' => array('storefront_package' => $meta['id']),
	);
}

function epc_portal_storefront_packages_for_js()
{
	$out = array();
	foreach (epc_portal_storefront_package_registry() as $id => $row) {
		$out[$id] = array(
			'label' => $row['label'],
			'desc' => $row['desc'],
			'industry_codes' => $row['industry_codes'],
			'theme_template' => $row['theme_template'],
		);
	}
	return $out;
}

/**
 * Apply industry-matched visual style + storefront package onto settings/contact.
 * Used by client onboard and Super CP "Apply industry theme".
 *
 * @param array $settings mutable site settings
 * @param array $contact mutable contact JSON
 * @param string $industryCode tenant industry
 * @param array $opts optional: theme_template, storefront_package, erp_only (bool), skip_package (bool)
 * @return array{theme_template:string,storefront_package:string,message:string}
 */
function epc_portal_apply_industry_theme_profile(array &$settings, array &$contact, $industryCode, array $opts = array())
{
	require_once __DIR__ . '/epc_portal_theme_templates.php';
	$code = preg_replace('/[^a-z0-9_]/', '', (string) $industryCode);
	if ($code === '') {
		$code = 'auto_parts';
	}
	$erpOnly = !empty($opts['erp_only']) || $code === 'erp_standalone';

	$overrideTpl = isset($opts['theme_template']) ? preg_replace('/[^a-z0-9_]/', '', strtolower((string) $opts['theme_template'])) : '';
	$overridePkg = isset($opts['storefront_package']) ? preg_replace('/[^a-z0-9_]/', '', strtolower((string) $opts['storefront_package'])) : '';

	$packageId = '';
	if (!$erpOnly && empty($opts['skip_package'])) {
		if ($overridePkg === 'none' || $overridePkg === '0') {
			$packageId = '';
		} elseif ($overridePkg !== '' && epc_portal_storefront_package_meta($overridePkg)) {
			$packageId = $overridePkg;
		} else {
			$packageId = (string) epc_portal_storefront_package_for_industry($code);
		}
	}

	$themeTemplate = '';
	if ($overrideTpl !== '') {
		$themeTemplate = epc_portal_normalize_theme_template($code, $overrideTpl);
	} elseif ($packageId !== '') {
		$meta = epc_portal_storefront_package_meta($packageId);
		$themeTemplate = epc_portal_normalize_theme_template(
			$code,
			isset($meta['theme_template']) ? (string) $meta['theme_template'] : epc_portal_default_theme_template($code)
		);
	} else {
		$themeTemplate = epc_portal_default_theme_template($code);
	}

	$settings['industry_code'] = $code;
	$settings['theme_template'] = $themeTemplate;
	$settings['theme'] = epc_portal_style_template_theme($code, $themeTemplate);

	if ($packageId !== '') {
		$meta = epc_portal_storefront_package_meta($packageId);
		$contact['storefront_package'] = $packageId;
		if (!empty($meta['enabled_packs']) && is_array($meta['enabled_packs']) && empty($opts['keep_packs'])) {
			$settings['enabled_packs'] = $meta['enabled_packs'];
		}
		if (!empty($meta['access_mode']) && empty($opts['keep_access_mode']) && empty($settings['access_mode'])) {
			$settings['access_mode'] = (string) $meta['access_mode'];
		}
		if (!empty($meta['tagline']) && (empty($settings['tagline']) || !empty($opts['force_package_tagline']))) {
			$settings['tagline'] = (string) $meta['tagline'];
		}
		// Automotive package uses platform SVG logo; other packages use tenant brand.
		if ($packageId === 'automotive_spareparts_pro') {
			$contact['use_animated_hub_logo'] = false;
			$contact['use_tenant_brand'] = false;
		} else {
			$contact['use_animated_hub_logo'] = true;
			$contact['use_tenant_brand'] = true;
		}
	} else {
		unset($contact['storefront_package']);
		if ($erpOnly) {
			$contact['use_animated_hub_logo'] = true;
			$contact['use_tenant_brand'] = true;
		} else {
			$contact['use_animated_hub_logo'] = true;
		}
	}

	$pkgLabel = $packageId !== '' ? $packageId : 'colour-only (no chrome package)';
	return array(
		'theme_template' => $themeTemplate,
		'storefront_package' => $packageId,
		'message' => 'Applied ' . $code . ' → style ' . $themeTemplate . ' · package ' . $pkgLabel,
	);
}
