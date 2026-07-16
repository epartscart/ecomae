<?php
/**
 * Portal industry → live industry-hub URL bridge (UAE/GCC onboard codes).
 *
 * Every onboardable portal industry must resolve to either:
 *   - hub_root: https://{hub}.ecomae.com/
 *   - existing sub-industry label on that hub template
 *   - inject: new sub-industry + product pack merged at render time
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

/**
 * @return array<string, array{template_key:string,mode:string,sub_label?:string,photo?:string,desc?:string,categories?:array,products?:array}>
 */
function epc_portal_industry_live_defs(): array
{
	$suppPhoto = 'https://images.unsplash.com/photo-1550572017-edd951aa8f72?w=1600&q=80';
	return array(
		// Hub roots — portal code represents the whole industry site
		'auto_parts' => array('template_key' => 'automotive', 'mode' => 'hub_root'),
		'electronics' => array('template_key' => 'electronics', 'mode' => 'hub_root'),
		'fashion' => array('template_key' => 'fashion', 'mode' => 'hub_root'),
		'jewellery' => array('template_key' => 'jewellery', 'mode' => 'hub_root'),
		'food_beverage' => array('template_key' => 'food_beverage', 'mode' => 'hub_root'),
		'hospitality_travel' => array('template_key' => 'hospitality', 'mode' => 'hub_root'),
		'education_training' => array('template_key' => 'education', 'mode' => 'hub_root'),
		'logistics_freight' => array('template_key' => 'logistics', 'mode' => 'hub_root'),
		'financial_services' => array('template_key' => 'finance', 'mode' => 'hub_root'),
		'energy_utilities' => array('template_key' => 'energy', 'mode' => 'hub_root'),
		'manufacturing_industrial' => array('template_key' => 'manufacturing', 'mode' => 'hub_root'),
		'media_entertainment' => array('template_key' => 'media', 'mode' => 'hub_root'),
		'printing_signage' => array('template_key' => 'printing', 'mode' => 'hub_root'),
		'nonprofit_government' => array('template_key' => 'nonprofit', 'mode' => 'hub_root'),
		'rental' => array('template_key' => 'rental', 'mode' => 'hub_root'),
		'construction_contracting' => array('template_key' => 'construction', 'mode' => 'hub_root'),
		'agricultural_products' => array('template_key' => 'agriculture', 'mode' => 'hub_root'),

		// Map onto existing dedicated sub-industry pages
		'automotive_full_vehicles' => array('template_key' => 'automotive', 'mode' => 'sub', 'sub_label' => 'Vehicle dealership & sales'),
		'it_hardware_accessories' => array('template_key' => 'it_software', 'mode' => 'sub', 'sub_label' => 'IT hardware distribution'),
		'medical' => array('template_key' => 'healthcare', 'mode' => 'sub', 'sub_label' => 'Medical equipment supply'),
		'pharmacy_retail' => array('template_key' => 'healthcare', 'mode' => 'sub', 'sub_label' => 'Pharmacy & drug dispensing'),
		'grocery_retail' => array('template_key' => 'retail', 'mode' => 'sub', 'sub_label' => 'Supermarket / grocery'),
		'furniture_interiors' => array('template_key' => 'home_living', 'mode' => 'sub', 'sub_label' => 'Furniture retail'),
		'building_materials' => array('template_key' => 'construction', 'mode' => 'sub', 'sub_label' => 'Building materials supply'),
		'fmcg_wholesale' => array('template_key' => 'wholesale', 'mode' => 'sub', 'sub_label' => 'FMCG distribution'),
		'industrial_equipment' => array('template_key' => 'manufacturing', 'mode' => 'sub', 'sub_label' => 'Machinery & equipment'),
		'perfume_cosmetics' => array('template_key' => 'beauty', 'mode' => 'sub', 'sub_label' => 'Perfume & fragrances'),
		'pet_services' => array('template_key' => 'pet', 'mode' => 'sub', 'sub_label' => 'Pet supply store'),
		'health' => array('template_key' => 'healthcare', 'mode' => 'sub', 'sub_label' => 'Wellness & holistic health'),
		'beauty_skincare' => array('template_key' => 'beauty', 'mode' => 'sub', 'sub_label' => 'Skincare & facials'),
		'fitness_training' => array('template_key' => 'sports', 'mode' => 'sub', 'sub_label' => 'Gym & fitness center'),
		'clinics_telemedicine' => array('template_key' => 'healthcare', 'mode' => 'sub', 'sub_label' => 'Telemedicine & telehealth'),
		'tax_advisory' => array('template_key' => 'professional', 'mode' => 'sub', 'sub_label' => 'Tax advisory & compliance'),
		'consultancy' => array('template_key' => 'professional', 'mode' => 'sub', 'sub_label' => 'Management consulting'),
		'legal_services' => array('template_key' => 'professional', 'mode' => 'sub', 'sub_label' => 'Law firm & legal services'),
		'accounting_auditing' => array('template_key' => 'professional', 'mode' => 'sub', 'sub_label' => 'Accounting & bookkeeping'),
		'hr_recruitment' => array('template_key' => 'professional', 'mode' => 'sub', 'sub_label' => 'HR & recruitment'),
		'marketing_digital' => array('template_key' => 'it_software', 'mode' => 'sub', 'sub_label' => 'Digital marketing agency'),
		'cleaning_facilities' => array('template_key' => 'cleaning', 'mode' => 'sub', 'sub_label' => 'Commercial cleaning'),
		'security_services' => array('template_key' => 'security', 'mode' => 'sub', 'sub_label' => 'Security guarding'),
		'vehicle_leasing' => array('template_key' => 'rental', 'mode' => 'sub', 'sub_label' => 'Car & vehicle rental'),
		'machinery_rental' => array('template_key' => 'rental', 'mode' => 'sub', 'sub_label' => 'Equipment & tool rental'),
		'it_services_saas_support' => array('template_key' => 'it_software', 'mode' => 'sub', 'sub_label' => 'SaaS / cloud platform'),

		// Injected verticals (not previously on the hub as this label)
		'nutrition_supplements' => array(
			'template_key' => 'healthcare',
			'mode' => 'inject',
			'sub_label' => 'Food supplements & nutrition',
			'photo' => $suppPhoto,
			'desc' => 'UAE/GCC food supplement and nutrition shops: vitamins, protein, nutraceuticals, herbal products, batch/expiry tracking, retail POS and e-commerce — DET/DED health-trading aligned.',
			'categories' => array('Vitamins', 'Protein & sports nutrition', 'Herbal & Ayurveda', 'Weight management', 'Immunity & wellness', 'Kids nutrition'),
			'products' => array(
				array('name' => 'Vitamin D3 Softgels 5000 IU', 'price' => 'AED 85', 'category' => 'Vitamins', 'image' => 'https://images.unsplash.com/photo-1550572017-edd951aa8f72?w=400&q=75'),
				array('name' => 'Whey Protein Isolate 2kg', 'price' => 'AED 220', 'category' => 'Protein & sports nutrition', 'image' => 'https://images.unsplash.com/photo-1593095948071-474c5cc2989d?w=400&q=75'),
				array('name' => 'Omega-3 Fish Oil Complex', 'price' => 'AED 95', 'category' => 'Vitamins', 'image' => 'https://images.unsplash.com/photo-1584308666744-24d5c474f2ae?w=400&q=75'),
				array('name' => 'Immunity Multivitamin Pack', 'price' => 'AED 120', 'category' => 'Immunity & wellness', 'image' => 'https://images.unsplash.com/photo-1471867481825-e8f285119f6d?w=400&q=75'),
				array('name' => 'Ashwagandha Herbal Capsules', 'price' => 'AED 75', 'category' => 'Herbal & Ayurveda', 'image' => 'https://images.unsplash.com/photo-1505751172876-fa1923c5c528?w=400&q=75'),
				array('name' => 'Kids Gummy Multivitamins', 'price' => 'AED 65', 'category' => 'Kids nutrition', 'image' => 'https://images.unsplash.com/photo-1587854692152-cbe660dbde88?w=400&q=75'),
			),
		),
	);
}

/**
 * Live public URL for a portal industry code.
 */
function epc_portal_industry_live_storefront_url(string $industryCode): string
{
	$code = preg_replace('/[^a-z0-9_]/', '', strtolower($industryCode));
	$defs = epc_portal_industry_live_defs();
	if ($code === '' || !isset($defs[$code])) {
		return '';
	}
	$def = $defs[$code];
	require_once __DIR__ . '/epc_industry_seo.php';
	$host = epc_industry_seo_primary_host((string) $def['template_key']);
	if ($host === '') {
		return '';
	}
	if (($def['mode'] ?? '') === 'hub_root') {
		return 'https://' . $host . '/';
	}
	$label = (string) ($def['sub_label'] ?? '');
	$slug = $label !== '' ? epc_industry_seo_sub_slug($label) : '';
	if ($slug === '') {
		return 'https://' . $host . '/';
	}
	return 'https://' . $host . '/' . $slug;
}

/**
 * Merge inject-mode portal verticals into an industry template payload.
 *
 * @param array $industryData
 * @param string $templateKey e.g. healthcare, automotive
 * @return array
 */
function epc_portal_merge_live_subs_into_industry(array $industryData, string $templateKey): array
{
	$tk = preg_replace('/[^a-z0-9_]/', '', strtolower($templateKey));
	if ($tk === '') {
		return $industryData;
	}
	$subs = isset($industryData['sub_industries']) && is_array($industryData['sub_industries'])
		? $industryData['sub_industries'] : array();
	$packs = isset($industryData['sub_industry_products']) && is_array($industryData['sub_industry_products'])
		? $industryData['sub_industry_products'] : array();

	foreach (epc_portal_industry_live_defs() as $def) {
		if (($def['template_key'] ?? '') !== $tk || ($def['mode'] ?? '') !== 'inject') {
			continue;
		}
		$label = (string) ($def['sub_label'] ?? '');
		if ($label === '') {
			continue;
		}
		if (!in_array($label, $subs, true)) {
			array_unshift($subs, $label);
		}
		if (!isset($packs[$label])) {
			$packs[$label] = array(
				'photo' => (string) ($def['photo'] ?? ($industryData['hero_photo'] ?? '')),
				'desc' => (string) ($def['desc'] ?? ($label . ' operations on ecomae.')),
				'categories' => isset($def['categories']) && is_array($def['categories']) ? $def['categories'] : array('Products', 'Services', 'Packages', 'Accessories', 'Support', 'Premium'),
				'products' => isset($def['products']) && is_array($def['products']) ? $def['products'] : array(),
			);
		}
	}

	$industryData['sub_industries'] = array_values(array_unique($subs));
	$industryData['sub_industry_products'] = $packs;
	return $industryData;
}

/**
 * Audit: portal codes with no live URL or broken sub reference.
 *
 * @return array{ok:array<int,string>,broken:array<string,string>}
 */
function epc_portal_industry_live_audit(): array
{
	require_once __DIR__ . '/epc_portal.php';
	require_once __DIR__ . '/epc_industry_seo.php';
	$ok = array();
	$broken = array();
	$defs = epc_portal_industry_live_defs();
	foreach (epc_portal_industries() as $code => $row) {
		if (in_array($code, array('platform_host', 'erp_standalone'), true)) {
			continue;
		}
		if (!isset($defs[$code])) {
			$broken[$code] = 'no live def';
			continue;
		}
		$url = epc_portal_industry_live_storefront_url($code);
		if ($url === '') {
			$broken[$code] = 'empty url';
			continue;
		}
		$def = $defs[$code];
		if (($def['mode'] ?? '') === 'sub') {
			$tk = (string) $def['template_key'];
			$file = __DIR__ . '/industry_templates/' . $tk . '.php';
			$src = is_file($file) ? (string) file_get_contents($file) : '';
			$label = (string) ($def['sub_label'] ?? '');
			if ($label === '' || (strpos($src, "'" . $label . "'") === false && strpos($src, '"' . $label . '"') === false)) {
				$broken[$code] = 'sub label missing in template: ' . $label;
				continue;
			}
		}
		$ok[] = $code;
	}
	return array('ok' => $ok, 'broken' => $broken);
}
