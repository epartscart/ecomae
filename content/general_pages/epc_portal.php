<?php
/**
 * Multi-industry portal registry — portable CP for many verticals.
 * Site hostname selects default industry + branding; CP can filter modules by industry.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

/**
 * Build a portal industry row (theme + packs + consolidation group).
 *
 * @param array $theme [primary, primary_dark, accent, sidebar_from, sidebar_to, hero_from, hero_to]
 */
function epc_portal_industry_row(
	string $code,
	string $name,
	string $ecosystem,
	string $icon,
	array $theme,
	array $cpPacks,
	string $groupKey,
	array $extra = array()
): array {
	$row = array(
		'code' => $code,
		'name' => $name,
		'ecosystem' => $ecosystem,
		'icon' => $icon,
		'group_key' => $groupKey,
		'theme' => array(
			'primary' => $theme[0],
			'primary_dark' => $theme[1],
			'accent' => $theme[2],
			'sidebar_from' => $theme[3],
			'sidebar_to' => $theme[4],
			'hero_from' => $theme[5],
			'hero_to' => $theme[6],
		),
		'cp_packs' => $cpPacks,
		'region' => 'uae_gcc',
	);
	return array_merge($row, $extra);
}

/**
 * Tenant-onboardable industries aligned to Dubai DET/DED + GCC trade activity clusters.
 * Maps into the 28 consolidation groups / subdomain templates (not 3,000 activity codes).
 */
function epc_portal_industries()
{
	$commerce = array('core', 'commerce', 'catalogue');
	$commerceErp = array('core', 'commerce', 'catalogue', 'erp', 'professional');
	$services = array('core', 'professional', 'erp', 'commerce');
	$autoPacks = array('core', 'commerce', 'auto_parts', 'logistics', 'erp', 'professional', 'marketing');
	$taxPacks = array('core', 'commerce', 'professional', 'tax_advisory', 'erp', 'logistics', 'marketing');

	return array(
		// —— Commerce (UAE/GCC retail & trading) ——
		'auto_parts' => epc_portal_industry_row('auto_parts', 'Auto spare parts', 'commerce', 'fa-car',
			array('#dc2626', '#991b1b', '#f97316', '#0f172a', '#1e293b', '#0b1220', '#1e3a5f'), $autoPacks, 'automotive'),
		'automotive_full_vehicles' => epc_portal_industry_row('automotive_full_vehicles', 'Vehicle sales & showrooms', 'commerce', 'fa-car',
			array('#b91c1c', '#7f1d1d', '#f59e0b', '#111827', '#1f2937', '#0f172a', '#7f1d1d'), $commerceErp, 'automotive'),
		'electronics' => epc_portal_industry_row('electronics', 'Electronics & gadgets', 'commerce', 'fa-microchip',
			array('#e10a0a', '#b00808', '#000000', '#000000', '#1a1a1a', '#000000', '#2d2d2d'), $commerce, 'electronics_technology', array(
				'storefront_package' => 'electronics_retail_virgin',
				'theme_template_default' => 'midnight',
			)),
		'it_hardware_accessories' => epc_portal_industry_row('it_hardware_accessories', 'IT hardware & accessories', 'commerce', 'fa-laptop',
			array('#2563eb', '#1d4ed8', '#38bdf8', '#0f172a', '#1e3a8a', '#0c4a6e', '#1e40af'), $commerce, 'electronics_technology'),
		'fashion' => epc_portal_industry_row('fashion', 'Fashion & apparel', 'commerce', 'fa-shopping-bag',
			array('#be185d', '#9d174d', '#ec4899', '#1f1020', '#4a1942', '#1f1020', '#701a75'), $commerce, 'fashion_apparel'),
		'jewellery' => epc_portal_industry_row('jewellery', 'Jewellery & luxury goods', 'commerce', 'fa-diamond',
			array('#b45309', '#92400e', '#fbbf24', '#1c1917', '#44403c', '#1c1917', '#78350f'), $commerce, 'jewellery_luxury'),
		'medical' => epc_portal_industry_row('medical', 'Medical supplies', 'commerce', 'fa-medkit',
			array('#0284c7', '#0369a1', '#22d3ee', '#0c4a6e', '#164e63', '#0c4a6e', '#155e75'), $commerceErp, 'healthcare_medical'),
		'pharmacy_retail' => epc_portal_industry_row('pharmacy_retail', 'Pharmacy & drugstore', 'commerce', 'fa-plus-square',
			array('#0d9488', '#0f766e', '#5eead4', '#134e4a', '#115e59', '#042f2e', '#0f766e'), $commerceErp, 'healthcare_medical'),
		'nutrition_supplements' => epc_portal_industry_row('nutrition_supplements', 'Food supplements & nutrition', 'commerce', 'fa-leaf',
			array('#15803d', '#166534', '#86efac', '#14532d', '#166534', '#052e16', '#15803d'), $commerce, 'healthcare_medical', array(
				'sub_industry_label' => 'Food supplements & nutrition',
			)),
		'food_beverage' => epc_portal_industry_row('food_beverage', 'Food & beverage / restaurants', 'commerce', 'fa-cutlery',
			array('#ea580c', '#c2410c', '#fb923c', '#7c2d12', '#9a3412', '#431407', '#c2410c'), $commerceErp, 'food_beverage'),
		'grocery_retail' => epc_portal_industry_row('grocery_retail', 'Grocery & supermarket', 'commerce', 'fa-shopping-cart',
			array('#16a34a', '#15803d', '#facc15', '#14532d', '#166534', '#052e16', '#15803d'), $commerce, 'retail_ecommerce'),
		'furniture_interiors' => epc_portal_industry_row('furniture_interiors', 'Furniture & interiors shop', 'commerce', 'fa-home',
			array('#92400e', '#78350f', '#d6d3d1', '#1c1917', '#44403c', '#292524', '#78716c'), $commerce, 'home_living', array(
				'sub_industry_label' => 'Furniture retail',
			)),
		'building_materials' => epc_portal_industry_row('building_materials', 'Building materials trading', 'commerce', 'fa-cubes',
			array('#78716c', '#57534e', '#f59e0b', '#1c1917', '#292524', '#0c0a09', '#44403c'), $commerceErp, 'construction_realestate'),
		'fmcg_wholesale' => epc_portal_industry_row('fmcg_wholesale', 'FMCG & general trading', 'commerce', 'fa-truck',
			array('#0369a1', '#075985', '#38bdf8', '#0c4a6e', '#075985', '#082f49', '#0c4a6e'), array('core', 'commerce', 'catalogue', 'logistics', 'erp'), 'wholesale_trading'),
		'industrial_equipment' => epc_portal_industry_row('industrial_equipment', 'Industrial equipment trading', 'commerce', 'fa-cogs',
			array('#475569', '#334155', '#94a3b8', '#0f172a', '#1e293b', '#020617', '#334155'), $commerceErp, 'manufacturing_industrial'),
		'agricultural_products' => epc_portal_industry_row('agricultural_products', 'Agriculture & farm products', 'commerce', 'fa-pagelines',
			array('#65a30d', '#4d7c0f', '#a3e635', '#365314', '#3f6212', '#1a2e05', '#4d7c0f'), $commerce, 'agriculture_farming'),
		'perfume_cosmetics' => epc_portal_industry_row('perfume_cosmetics', 'Perfume & cosmetics trading', 'commerce', 'fa-magic',
			array('#db2777', '#be185d', '#f9a8d4', '#500724', '#831843', '#500724', '#9d174d'), $commerce, 'beauty_wellness'),
		'pet_services' => epc_portal_industry_row('pet_services', 'Pet shop & animal services', 'commerce', 'fa-paw',
			array('#d97706', '#b45309', '#fcd34d', '#78350f', '#92400e', '#451a03', '#b45309'), $commerce, 'pet_animal'),

		// —— Lifestyle & consumer ——
		'health' => epc_portal_industry_row('health', 'Health & wellness', 'lifestyle_consumer', 'fa-heartbeat',
			array('#16a34a', '#15803d', '#4ade80', '#14532d', '#166534', '#14532d', '#15803d'), $commerce, 'healthcare_medical'),
		'beauty_skincare' => epc_portal_industry_row('beauty_skincare', 'Beauty salon & skincare', 'lifestyle_consumer', 'fa-female',
			array('#e11d48', '#be123c', '#fb7185', '#4c0519', '#881337', '#4c0519', '#9f1239'), $commerce, 'beauty_wellness'),
		'fitness_training' => epc_portal_industry_row('fitness_training', 'Gym & fitness centres', 'lifestyle_consumer', 'fa-heartbeat',
			array('#dc2626', '#b91c1c', '#fbbf24', '#450a0a', '#7f1d1d', '#450a0a', '#991b1b'), $commerce, 'sports_fitness'),
		'hospitality_travel' => epc_portal_industry_row('hospitality_travel', 'Hospitality, hotels & travel', 'lifestyle_consumer', 'fa-building',
			array('#0ea5e9', '#0284c7', '#fbbf24', '#0c4a6e', '#075985', '#082f49', '#0369a1'), $commerceErp, 'hospitality_travel'),
		'clinics_telemedicine' => epc_portal_industry_row('clinics_telemedicine', 'Clinics & telemedicine', 'lifestyle_consumer', 'fa-stethoscope',
			array('#0891b2', '#0e7490', '#67e8f9', '#164e63', '#155e75', '#083344', '#0e7490'), $services, 'healthcare_medical'),

		// —— Business services ——
		'tax_advisory' => epc_portal_industry_row('tax_advisory', 'Tax & advisory', 'business_services', 'fa-balance-scale',
			array('#0d9488', '#0f766e', '#14b8a6', '#042f2e', '#134e4a', '#042f2e', '#115e59'), $taxPacks, 'professional_services', array(
				'storefront_package' => 'consulting_primeinvest',
				'theme_template_default' => 'modern',
			)),
		'consultancy' => epc_portal_industry_row('consultancy', 'Consultancy', 'business_services', 'fa-briefcase',
			array('#7c3aed', '#6d28d9', '#a78bfa', '#2e1065', '#4c1d95', '#2e1065', '#5b21b6'), $services, 'professional_services'),
		'legal_services' => epc_portal_industry_row('legal_services', 'Legal services', 'business_services', 'fa-gavel',
			array('#1e3a8a', '#1e40af', '#93c5fd', '#172554', '#1e3a8a', '#0f172a', '#1e3a8a'), $services, 'professional_services'),
		'accounting_auditing' => epc_portal_industry_row('accounting_auditing', 'Accounting & auditing', 'business_services', 'fa-calculator',
			array('#0f766e', '#115e59', '#5eead4', '#042f2e', '#134e4a', '#022c22', '#0f766e'), $services, 'professional_services'),
		'hr_recruitment' => epc_portal_industry_row('hr_recruitment', 'HR & recruitment', 'business_services', 'fa-users',
			array('#7c3aed', '#6d28d9', '#c4b5fd', '#2e1065', '#4c1d95', '#1e1b4b', '#5b21b6'), $services, 'professional_services'),
		'marketing_digital' => epc_portal_industry_row('marketing_digital', 'Digital marketing & media agency', 'business_services', 'fa-bullhorn',
			array('#db2777', '#be185d', '#f472b6', '#500724', '#9d174d', '#500724', '#be185d'), array('core', 'professional', 'marketing', 'commerce'), 'media_entertainment'),
		'education_training' => epc_portal_industry_row('education_training', 'Education & training', 'business_services', 'fa-graduation-cap',
			array('#4f46e5', '#4338ca', '#a5b4fc', '#1e1b4b', '#312e81', '#1e1b4b', '#3730a3'), $services, 'education_training'),
		'cleaning_facilities' => epc_portal_industry_row('cleaning_facilities', 'Cleaning & facilities management', 'business_services', 'fa-paint-brush',
			array('#059669', '#047857', '#6ee7b7', '#064e3b', '#065f46', '#022c22', '#047857'), $services, 'cleaning_maintenance'),
		'security_services' => epc_portal_industry_row('security_services', 'Security & safety services', 'business_services', 'fa-shield',
			array('#1e293b', '#0f172a', '#38bdf8', '#020617', '#1e293b', '#020617', '#334155'), $services, 'security_safety'),
		'printing_signage' => epc_portal_industry_row('printing_signage', 'Printing & signage', 'business_services', 'fa-print',
			array('#ea580c', '#c2410c', '#fdba74', '#7c2d12', '#9a3412', '#431407', '#c2410c'), $commerce, 'printing_signage'),
		'construction_contracting' => epc_portal_industry_row('construction_contracting', 'Construction & contracting', 'business_services', 'fa-building',
			array('#b45309', '#92400e', '#fbbf24', '#1c1917', '#44403c', '#0c0a09', '#78350f'), $commerceErp, 'construction_realestate'),
		'logistics_freight' => epc_portal_industry_row('logistics_freight', 'Logistics & freight', 'business_services', 'fa-ship',
			array('#0369a1', '#075985', '#38bdf8', '#0c4a6e', '#075985', '#082f49', '#0284c7'), array('core', 'commerce', 'logistics', 'erp', 'professional'), 'logistics_transport'),
		'financial_services' => epc_portal_industry_row('financial_services', 'Financial services', 'business_services', 'fa-line-chart',
			array('#047857', '#065f46', '#34d399', '#022c22', '#064e3b', '#022c22', '#047857'), $services, 'financial_services'),
		'erp_standalone' => epc_portal_industry_row('erp_standalone', 'ERP standalone (no storefront)', 'business_services', 'fa-university',
			array('#0369a1', '#075985', '#0ea5e9', '#0c4a6e', '#075985', '#082f49', '#0c4a6e'), array('core', 'erp', 'professional', 'logistics'), 'professional_services'),
		'nonprofit_government' => epc_portal_industry_row('nonprofit_government', 'Non-profit & government', 'business_services', 'fa-university',
			array('#334155', '#1e293b', '#94a3b8', '#020617', '#0f172a', '#020617', '#1e293b'), array('core', 'professional', 'erp'), 'nonprofit_government'),

		// —— Asset sharing ——
		'rental' => epc_portal_industry_row('rental', 'Rental & leasing', 'asset_sharing', 'fa-key',
			array('#ca8a04', '#a16207', '#facc15', '#422006', '#713f12', '#422006', '#854d0e'), $commerce, 'rental_leasing'),
		'vehicle_leasing' => epc_portal_industry_row('vehicle_leasing', 'Vehicle leasing & rental', 'asset_sharing', 'fa-car',
			array('#ca8a04', '#a16207', '#fde047', '#422006', '#713f12', '#1c1917', '#a16207'), $commerceErp, 'rental_leasing'),
		'machinery_rental' => epc_portal_industry_row('machinery_rental', 'Machinery & equipment rental', 'asset_sharing', 'fa-wrench',
			array('#78716c', '#57534e', '#fbbf24', '#1c1917', '#292524', '#0c0a09', '#44403c'), $commerceErp, 'rental_leasing'),

		// —— Digital & technology ——
		'it_services_saas_support' => epc_portal_industry_row('it_services_saas_support', 'IT services & SaaS', 'digital_technology', 'fa-cloud',
			array('#2563eb', '#1d4ed8', '#60a5fa', '#0f172a', '#1e3a8a', '#020617', '#1d4ed8'), $services, 'it_software'),
		'media_entertainment' => epc_portal_industry_row('media_entertainment', 'Media & entertainment', 'digital_technology', 'fa-film',
			array('#c026d3', '#a21caf', '#e879f9', '#4a044e', '#701a75', '#3b0764', '#a21caf'), array('core', 'commerce', 'marketing', 'professional'), 'media_entertainment'),
		'energy_utilities' => epc_portal_industry_row('energy_utilities', 'Energy & utilities', 'digital_technology', 'fa-bolt',
			array('#eab308', '#ca8a04', '#fef08a', '#422006', '#713f12', '#1c1917', '#a16207'), $commerceErp, 'energy_utilities'),
		'manufacturing_industrial' => epc_portal_industry_row('manufacturing_industrial', 'Manufacturing & industrial', 'digital_technology', 'fa-industry',
			array('#64748b', '#475569', '#cbd5e1', '#0f172a', '#1e293b', '#020617', '#334155'), $commerceErp, 'manufacturing_industrial'),

		// —— Platform ——
		'platform_host' => epc_portal_industry_row('platform_host', 'Platform host (ecomae)', 'platform', 'fa-cloud',
			array('#0ea5e9', '#0284c7', '#38bdf8', '#0c4a6e', '#075985', '#082f49', '#0c4a6e'),
			array('core', 'professional', 'erp', 'marketing', 'super_platform'), 'retail_ecommerce'),
	);
}

/**
 * Explicit portal industry code → consolidation group (DED / subdomain template).
 *
 * @return array<string, string>
 */
function epc_portal_industry_group_map(): array
{
	$map = array();
	foreach (epc_portal_industries() as $code => $row) {
		if (!empty($row['group_key'])) {
			$map[$code] = (string) $row['group_key'];
		}
	}
	return $map;
}

/**
 * ECOM AE strategic ecosystem taxonomy (UAE/GCC onboard-ready).
 */
function epc_portal_ecosystems(): array
{
	$all = epc_portal_industries();
	$byEco = array();
	foreach ($all as $code => $ind) {
		if ($code === 'platform_host') {
			continue;
		}
		$eco = (string) ($ind['ecosystem'] ?? '');
		if ($eco === '') {
			continue;
		}
		if (!isset($byEco[$eco])) {
			$byEco[$eco] = array();
		}
		$byEco[$eco][] = $code;
	}
	return array(
		'commerce' => array(
			'code' => 'commerce',
			'name' => 'Commerce ecosystem',
			'existing' => $byEco['commerce'] ?? array(),
			'placeholders' => array(),
		),
		'business_services' => array(
			'code' => 'business_services',
			'name' => 'Business Services ecosystem',
			'existing' => $byEco['business_services'] ?? array(),
			'placeholders' => array('business_licensing_services'),
		),
		'lifestyle_consumer' => array(
			'code' => 'lifestyle_consumer',
			'name' => 'Lifestyle & Consumer ecosystem',
			'existing' => $byEco['lifestyle_consumer'] ?? array(),
			'placeholders' => array('wellness_subscriptions', 'travel_experiences'),
		),
		'asset_sharing' => array(
			'code' => 'asset_sharing',
			'name' => 'Asset & Sharing ecosystem',
			'existing' => $byEco['asset_sharing'] ?? array(),
			'placeholders' => array('real_estate_leasing', 'equipment_sharing', 'storage_warehousing_rental', 'fleet_management'),
		),
		'digital_technology' => array(
			'code' => 'digital_technology',
			'name' => 'Digital & Technology ecosystem',
			'existing' => $byEco['digital_technology'] ?? array(),
			'placeholders' => array('saas_tools', 'iot', 'ai_automation', 'cybersecurity', 'ecommerce_infrastructure_tools'),
		),
	);
}

/**
 * Live public storefront URL for a portal industry (hub + dedicated sub-industry path).
 * Example: nutrition_supplements → https://healthcare.ecomae.com/food-supplements-nutrition
 */
function epc_portal_industry_live_storefront_url(string $industryCode): string
{
	$code = preg_replace('/[^a-z0-9_]/', '', strtolower($industryCode));
	$all = epc_portal_industries();
	if ($code === '' || !isset($all[$code])) {
		return '';
	}
	$row = $all[$code];
	$groupKey = (string) ($row['group_key'] ?? '');
	$name = (string) ($row['name'] ?? '');
	if ($groupKey === '' || $name === '') {
		return '';
	}
	$seoFile = __DIR__ . '/epc_industry_seo.php';
	$consFile = __DIR__ . '/epc_industry_consolidation.php';
	if (!is_file($seoFile) || !is_file($consFile)) {
		return '';
	}
	require_once $consFile;
	require_once $seoFile;
	$groups = epc_industry_groups();
	$templateKey = (string) ($groups[$groupKey]['template_key'] ?? $groupKey);
	$host = function_exists('epc_industry_seo_primary_host')
		? epc_industry_seo_primary_host($templateKey)
		: '';
	if ($host === '') {
		return '';
	}
	// Prefer an explicit sub-industry label override when portal name differs from hub pack key.
	$subLabel = (string) ($row['sub_industry_label'] ?? $name);
	$slug = function_exists('epc_industry_seo_sub_slug') ? epc_industry_seo_sub_slug($subLabel) : '';
	if ($slug === '') {
		return 'https://' . $host . '/';
	}
	return 'https://' . $host . '/' . $slug;
}

/**
 * Audit: consolidation groups that still lack a portal onboard industry (UAE/GCC gap list).
 *
 * @return array<int, string>
 */
function epc_portal_ded_group_coverage_gaps(): array
{
	$consFile = __DIR__ . '/epc_industry_consolidation.php';
	if (!is_file($consFile)) {
		return array();
	}
	require_once $consFile;
	if (!function_exists('epc_industry_groups')) {
		return array();
	}
	$covered = array_flip(array_values(epc_portal_industry_group_map()));
	$gaps = array();
	foreach (array_keys(epc_industry_groups()) as $gk) {
		if (!isset($covered[$gk])) {
			$gaps[] = $gk;
		}
	}
	return $gaps;
}

function epc_portal_industries_grouped(array $industries = null): array
{
	$industries = is_array($industries) ? $industries : epc_portal_industries();
	$ecosystems = epc_portal_ecosystems();
	$groups = array();
	foreach ($ecosystems as $ecoCode => $eco) {
		$groups[$ecoCode] = array(
			'code' => $ecoCode,
			'name' => $eco['name'],
			'industries' => array(),
			'placeholders' => $eco['placeholders'],
		);
	}
	foreach ($industries as $code => $ind) {
		$ecoCode = isset($ind['ecosystem']) ? (string) $ind['ecosystem'] : '';
		if ($ecoCode === '' || !isset($groups[$ecoCode])) {
			continue;
		}
		$groups[$ecoCode]['industries'][$code] = $ind;
	}
	return $groups;
}

function epc_portal_sites()
{
	return array(
		'www.ecomae.com' => array(
			'industry' => 'platform_host',
			'system_name' => 'ECOM AE portal',
			'hub_name' => 'ecomae',
			'tagline' => 'E-Commerce Arab Emirates — hosted commerce platform',
			'domain_path' => 'https://www.ecomae.com/',
			'db' => 'ecomae',
			'user' => 'ecomae',
			'trade_name' => 'ecomae',
			'from_email' => 'hello@ecomae.com',
			'contact_phone' => '+971-567607011',
			'head_office_address' => 'Dubai, United Arab Emirates',
			'city' => 'Dubai',
			'country' => 'United Arab Emirates',
		),
		'ecomae.com' => array(
			'industry' => 'platform_host',
			'system_name' => 'ECOM AE portal',
			'hub_name' => 'ecomae',
			'tagline' => 'E-Commerce Arab Emirates — hosted commerce platform',
			'domain_path' => 'https://www.ecomae.com/',
			'db' => 'ecomae',
			'user' => 'ecomae',
			'trade_name' => 'ecomae',
			'from_email' => 'hello@ecomae.com',
			'contact_phone' => '+971-567607011',
			'head_office_address' => 'Dubai, United Arab Emirates',
			'city' => 'Dubai',
		),
		'cp.ecomae.com' => array(
			'industry' => 'platform_host',
			'super_cp' => true,
			'system_name' => 'ECOM AE portal',
			'hub_name' => 'ecomae Platform',
			'tagline' => 'Super control panel — all tenant sites',
			'domain_path' => 'https://cp.ecomae.com/',
			'db' => 'ecomae',
			'user' => 'ecomae',
			'trade_name' => 'ecomae',
			'from_email' => 'hello@ecomae.com',
		),
		'www.electronicae.com' => array(
			'industry' => 'electronics',
			'system_name' => 'Electronicae',
			'hub_name' => 'Electronicae',
			'tagline' => 'Shop Online for Tech, Gaming, Toys & More',
			'domain_path' => 'https://www.electronicae.com/',
			'trade_name' => 'Electronicae',
			'from_email' => 'hello@electronicae.com',
		),
		'electronicae.com' => array(
			'industry' => 'electronics',
			'system_name' => 'Electronicae',
			'hub_name' => 'Electronicae',
			'tagline' => 'Shop Online for Tech, Gaming, Toys & More',
			'domain_path' => 'https://www.electronicae.com/',
			'trade_name' => 'Electronicae',
			'from_email' => 'hello@electronicae.com',
		),
		'www.stylenlook.com' => array(
			'industry' => 'fashion',
			'system_name' => 'Style N Look',
			'hub_name' => 'Style N Look',
			'tagline' => 'Fashion, Beauty & Lifestyle — Curated for You',
			'domain_path' => 'https://www.stylenlook.com/',
			'trade_name' => 'Style N Look',
			'from_email' => 'hello@stylenlook.com',
		),
		'stylenlook.com' => array(
			'industry' => 'fashion',
			'system_name' => 'Style N Look',
			'hub_name' => 'Style N Look',
			'tagline' => 'Fashion, Beauty & Lifestyle — Curated for You',
			'domain_path' => 'https://www.stylenlook.com/',
			'trade_name' => 'Style N Look',
			'from_email' => 'hello@stylenlook.com',
		),
		'www.thejewellerytrend.com' => array(
			'industry' => 'jewellery',
			'system_name' => 'The Jewellery Trend',
			'hub_name' => 'The Jewellery Trend',
			'tagline' => 'Fine Jewellery, Diamonds & Timeless Pieces',
			'domain_path' => 'https://www.thejewellerytrend.com/',
			'trade_name' => 'The Jewellery Trend',
			'from_email' => 'hello@thejewellerytrend.com',
		),
		'thejewellerytrend.com' => array(
			'industry' => 'jewellery',
			'system_name' => 'The Jewellery Trend',
			'hub_name' => 'The Jewellery Trend',
			'tagline' => 'Fine Jewellery, Diamonds & Timeless Pieces',
			'domain_path' => 'https://www.thejewellerytrend.com/',
			'trade_name' => 'The Jewellery Trend',
			'from_email' => 'hello@thejewellerytrend.com',
		),
		'www.taxofinca.com' => array(
			'industry' => 'tax_advisory',
			'system_name' => 'Taxofin CA',
			'hub_name' => 'Taxofin CA',
			'tagline' => 'Tax, Audit & Advisory — Trusted Chartered Accountants',
			'domain_path' => 'https://www.taxofinca.com/',
			'trade_name' => 'Taxofin CA',
			'from_email' => 'hello@taxofinca.com',
		),
		'taxofinca.com' => array(
			'industry' => 'tax_advisory',
			'system_name' => 'Taxofin CA',
			'hub_name' => 'Taxofin CA',
			'tagline' => 'Tax, Audit & Advisory — Trusted Chartered Accountants',
			'domain_path' => 'https://www.taxofinca.com/',
			'trade_name' => 'Taxofin CA',
			'from_email' => 'hello@taxofinca.com',
		),
	);
}

function epc_portal_cp_pack_routes()
{
	return array(
		'core' => array(
			'/content/', '/users/', '/control/', '/templates', '/modules', '/plugins',
			'/shop/modul-pechati', '/shop/print', '/cp-guideline', '/guideline',
			'/control/portal',
		),
		'commerce' => array(
			'/shop/orders', '/shop/cart', '/shop/catalogue', '/shop/payments', '/shop/channels',
			'/shop/marketing', '/shop/customer', '/customers', '/shop/bulk', '/shop/pos',
		),
		'auto_parts' => array(
			'/shop/prices', '/shop/crosses', '/shop/procurement', '/shop/demand',
			'/shop/parts', '/shop/docpart', '/shop/price-management', '/shop/logistics',
			'/shop/vehicle', '/shop/product-family', '/shop/umapi', '/shop/agent',
		),
		'logistics' => array(
			'/shop/logistics', '/shop/storages', '/shop/offices', '/shop/warehouse',
		),
		'catalogue' => array(
			'/shop/catalogue', '/shop/bulk-upload', '/shop/product-family',
		),
		'professional' => array(
			'/shop/finance', '/shop/erp', '/shop/customer_mgmt', '/shop/einvoice',
			'/shop/customer-approval', '/shop/demand_countries', '/shop/document_control',
		),
		'crm' => array(
		),
		'erp' => array(
			'/shop/finance', '/shop/erp', '/shop/einvoice', '/shop/modul-pechati', '/shop/document_control',
		),
		'marketing' => array(
			'/shop/marketing',
		),
		'tax_advisory' => array(
			'/shop/finance', '/shop/erp', '/shop/customer_mgmt', '/shop/einvoice',
			'/shop/marketing', '/shop/payments', '/shop/print',
		),
		'super_platform' => array(
			'/shop/tenant_hub', '/control/portal',
		),
	);
}

function epc_portal_host()
{
	$host = '';
	if (!empty($_SERVER['HTTP_HOST'])) {
		$host = strtolower((string) $_SERVER['HTTP_HOST']);
	}
	if ($host !== '' && strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	if ($host === '' && !empty($_SERVER['SERVER_NAME'])) {
		$host = strtolower((string) $_SERVER['SERVER_NAME']);
	}
	return $host;
}

function epc_portal_platform_hostnames_static(): array
{
	return array(
		'www.ecomae.com',
		'ecomae.com',
		'cp.ecomae.com',
	);
}

function epc_portal_is_client_hostname(string $host = null): bool
{
	if ($host === null) {
		$host = epc_portal_host();
	}
	$host = strtolower(trim($host));
	if ($host === '') {
		return false;
	}
	if (in_array($host, epc_portal_platform_hostnames_static(), true)) {
		return false;
	}
	// Industry wildcard subdomains (*.ecomae.com) are NOT client tenants
	if (preg_match('/^[a-z0-9][a-z0-9_-]*\.ecomae\.com$/', $host)) {
		return false;
	}
	return true;
}

function epc_portal_is_platform_operator_host(string $host = null): bool
{
	if ($host === null) {
		$host = epc_portal_host();
	}
	$host = strtolower(trim($host));
	return $host !== '' && in_array($host, epc_portal_platform_hostnames_static(), true);
}

/** Industries shown on CP → Industry settings (client sites exclude platform operator). */
function epc_portal_settings_industries(): array
{
	$all = epc_portal_industries();
	if (epc_portal_is_client_hostname()) {
		unset($all['platform_host']);
	}
	return $all;
}

/** CP module packs for Industry settings (Super CP pack is platform-only). */
function epc_portal_settings_packs(): array
{
	$all = epc_portal_pack_definitions();
	if (epc_portal_is_client_hostname()) {
		unset($all['super_platform']);
	}
	return $all;
}

function epc_portal_can_deploy_portal_package(): bool
{
	return epc_portal_is_platform_operator_host();
}

function epc_portal_docpart_config()
{
	static $cfg = null;
	if ($cfg !== null) {
		return $cfg;
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$cfg = new DP_Config();
	return $cfg;
}

function epc_portal_guess_domain_path($host = null)
{
	if ($host === null) {
		$host = epc_portal_host();
	}
	if ($host === '' || $host === 'localhost' || $host === '127.0.0.1') {
		return '';
	}
	$https = false;
	if (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off') {
		$https = true;
	}
	if (!empty($_SERVER['HTTP_X_FORWARDED_PROTO']) && strtolower((string) $_SERVER['HTTP_X_FORWARDED_PROTO']) === 'https') {
		$https = true;
	}
	return ($https ? 'https' : 'http') . '://' . $host . '/';
}

function epc_portal_default_contact($profile = array())
{
	$host = epc_portal_host();
	$trade = isset($profile['trade_name']) ? (string) $profile['trade_name'] : '';
	if ($trade === '' && isset($profile['hub_name'])) {
		$trade = (string) $profile['hub_name'];
	}
	if ($trade === '' && $host !== '') {
		$trade = preg_replace('/^www\./', '', $host);
		$trade = ucfirst(str_replace(array('.com', '.'), array('', ' '), $trade));
	}
	return array(
		'trade_name' => $trade,
		'from_name' => $trade,
		'from_email' => isset($profile['from_email']) ? (string) $profile['from_email'] : '',
		'admin_email' => isset($profile['admin_email']) ? (string) $profile['admin_email'] : '',
		'contact_phone' => isset($profile['contact_phone']) ? (string) $profile['contact_phone'] : '',
		'whatsapp_number' => isset($profile['whatsapp_number']) ? (string) $profile['whatsapp_number'] : '',
		'head_office_title' => 'Head Office',
		'head_office_address' => isset($profile['head_office_address']) ? (string) $profile['head_office_address'] : '',
		'head_office_email' => isset($profile['head_office_email']) ? (string) $profile['head_office_email'] : '',
		'city' => isset($profile['city']) ? (string) $profile['city'] : '',
		'country' => isset($profile['country']) ? (string) $profile['country'] : 'United Arab Emirates',
	);
}

function epc_portal_site_profile()
{
	static $profiles = array();
	$host = epc_portal_host();
	if (!empty($GLOBALS['epc_demo_storefront_context']) && !empty($GLOBALS['epc_demo_storefront_tenant_row'])) {
		$demoCacheKey = 'demo:' . (string) ($GLOBALS['epc_demo_storefront_site_key'] ?? '');
		if ($demoCacheKey !== 'demo:' && isset($profiles[$demoCacheKey])) {
			return $profiles[$demoCacheKey];
		}
		$tenantRow = (array) $GLOBALS['epc_demo_storefront_tenant_row'];
		$siteKey = preg_replace('/[^a-z0-9_]/', '', (string) ($tenantRow['site_key'] ?? ''));
		$industry = (string) ($tenantRow['industry_code'] ?? ($GLOBALS['epc_demo_storefront_industry'] ?? 'auto_parts'));
		$profile = array(
			'site_key' => $siteKey,
			'industry' => $industry,
			'trade_name' => (string) ($tenantRow['trade_name'] ?? 'Demo'),
			'hub_name' => (string) ($tenantRow['hub_name'] ?? $tenantRow['trade_name'] ?? 'Demo'),
			'from_email' => (string) ($tenantRow['from_email'] ?? $tenantRow['demo_contact_email'] ?? ''),
			'domain_path' => 'https://www.ecomae.com/demo/' . $siteKey . '/',
			'db' => (string) ($tenantRow['db_name'] ?? ''),
			'user' => (string) ($tenantRow['db_user'] ?? ''),
			'password' => (string) ($tenantRow['db_password'] ?? ''),
		);
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
		$dbSettings = array();
		if ($profile['db'] !== '' && $profile['user'] !== '' && $profile['password'] !== '') {
			try {
				require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
				$cfg = new DP_Config();
				$tenantPdo = new PDO(
					'mysql:host=' . $cfg->host . ';dbname=' . $profile['db'] . ';charset=utf8;connect_timeout=3',
					$profile['user'],
					$profile['password'],
					array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 3)
				);
				$dbSettings = epc_portal_load_site_settings_for_host($tenantPdo, 'www.ecomae.com');
			} catch (Exception $e) {
				$dbSettings = array();
			}
		}
		if (!empty($dbSettings['industry_code']) && $dbSettings['industry_code'] !== 'platform_host') {
			$profile['industry'] = $dbSettings['industry_code'];
		}
		if (!empty($dbSettings['system_name'])) {
			$profile['system_name'] = $dbSettings['system_name'];
		}
		if (!empty($dbSettings['hub_name'])) {
			$profile['hub_name'] = $dbSettings['hub_name'];
		}
		if (!empty($dbSettings['tagline'])) {
			$profile['tagline'] = $dbSettings['tagline'];
		}
		if (!empty($dbSettings['theme'])) {
			$profile['theme'] = $dbSettings['theme'];
		}
		if (!empty($dbSettings['domain_path']) && strpos((string) $dbSettings['domain_path'], '/demo/' . $siteKey) !== false) {
			$profile['domain_path'] = $dbSettings['domain_path'];
		}
		if (!empty($dbSettings['contact']) && is_array($dbSettings['contact'])) {
			$profile['contact'] = $dbSettings['contact'];
		}
		if ($demoCacheKey !== 'demo:') {
			$profiles[$demoCacheKey] = $profile;
		}
		return $profile;
	}
	if (!empty($GLOBALS['epc_demo_cp_context']) && !empty($GLOBALS['epc_demo_cp_tenant_row'])) {
		$demoCacheKey = 'demo-cp:' . (string) ($GLOBALS['epc_demo_cp_site_key'] ?? '');
		if ($demoCacheKey !== 'demo-cp:' && isset($profiles[$demoCacheKey])) {
			return $profiles[$demoCacheKey];
		}
		$tenantRow = (array) $GLOBALS['epc_demo_cp_tenant_row'];
		$siteKey = preg_replace('/[^a-z0-9_]/', '', (string) ($tenantRow['site_key'] ?? ($GLOBALS['epc_demo_cp_site_key'] ?? '')));
		$industry = (string) ($tenantRow['industry_code'] ?? 'auto_parts');
		$profile = array(
			'site_key' => $siteKey,
			'industry' => $industry,
			'trade_name' => (string) ($tenantRow['trade_name'] ?? 'Demo'),
			'hub_name' => (string) ($tenantRow['hub_name'] ?? $tenantRow['trade_name'] ?? 'Demo'),
			'from_email' => (string) ($tenantRow['from_email'] ?? $tenantRow['demo_contact_email'] ?? ''),
			'domain_path' => 'https://www.ecomae.com/demo/' . $siteKey . '/',
			'db' => (string) ($tenantRow['db_name'] ?? ''),
			'user' => (string) ($tenantRow['db_user'] ?? ''),
			'password' => (string) ($tenantRow['db_password'] ?? ''),
		);
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
		$dbSettings = array();
		if ($profile['db'] !== '' && $profile['user'] !== '' && $profile['password'] !== '') {
			try {
				require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
				$cfg = new DP_Config();
				$tenantPdo = new PDO(
					'mysql:host=' . $cfg->host . ';dbname=' . $profile['db'] . ';charset=utf8;connect_timeout=3',
					$profile['user'],
					$profile['password'],
					array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 3)
				);
				$dbSettings = epc_portal_load_site_settings_for_host($tenantPdo, 'www.ecomae.com');
			} catch (Exception $e) {
				$dbSettings = array();
			}
		}
		if (!empty($dbSettings['industry_code']) && $dbSettings['industry_code'] !== 'platform_host') {
			$profile['industry'] = $dbSettings['industry_code'];
		}
		if (!empty($dbSettings['system_name'])) {
			$profile['system_name'] = $dbSettings['system_name'];
		}
		if (!empty($dbSettings['hub_name'])) {
			$profile['hub_name'] = $dbSettings['hub_name'];
		}
		if (!empty($dbSettings['tagline'])) {
			$profile['tagline'] = $dbSettings['tagline'];
		}
		if (!empty($dbSettings['theme'])) {
			$profile['theme'] = $dbSettings['theme'];
		}
		if (!empty($dbSettings['contact']) && is_array($dbSettings['contact'])) {
			$profile['contact'] = $dbSettings['contact'];
		}
		if ($demoCacheKey !== 'demo-cp:') {
			$profiles[$demoCacheKey] = $profile;
		}
		return $profile;
	}
	if ($host !== '' && isset($profiles[$host])) {
		return $profiles[$host];
	}
	$sites = epc_portal_sites();
	$profile = null;
	if ($host !== '' && isset($sites[$host])) {
		$profile = $sites[$host];
	} else {
		foreach ($sites as $pattern => $site) {
			if ($host !== '' && substr($host, -strlen($pattern)) === $pattern) {
				$profile = $site;
				break;
			}
		}
	}
	if ($profile === null) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
		$tenant = epc_portal_load_tenant_by_host($host);
		if ($tenant !== null) {
			$profile = $tenant;
		}
	}
	if (epc_portal_is_client_hostname($host)) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
		$tenant = epc_portal_load_tenant_by_host($host);
		if ($tenant !== null) {
			$profile = $tenant;
		} else {
			$profile = is_array($profile) ? $profile : array();
			if (function_exists('epc_portal_is_epartscart_hostname') && epc_portal_is_epartscart_hostname($host)) {
				$profile['site_key'] = 'epartscart';
				$profile['industry'] = 'auto_parts';
				$profile['trade_name'] = 'eParts Cart';
				$profile['hub_name'] = 'Electronic World Group';
				$profile['from_email'] = 'partsdoc2025@gmail.com';
			}
		}
		$profile['domain_path'] = 'https://' . $host . '/';
		// Model C client CP/storefront: registry db_name may be ecomae — runtime always uses shared docpart.
		$runtimeOverride = epc_portal_runtime_host_db($host);
		if (is_array($runtimeOverride)) {
			$profile['db'] = (string) $runtimeOverride['db'];
			$profile['user'] = (string) $runtimeOverride['user'];
			$profile['password'] = (string) $runtimeOverride['password'];
		} else {
			$resolved = epc_portal_resolve_tenant_db_credentials();
			$profile['db'] = $resolved['db'];
			$profile['user'] = $resolved['user'];
			$profile['password'] = $resolved['password'];
		}
	}
	// Industry wildcard subdomains — use resolved industry from bootstrap
	if ($profile === null && !empty($GLOBALS['epc_industry_subdomain_active'])) {
		$industryGroup = (string) ($GLOBALS['epc_industry_subdomain_group'] ?? 'technology');
		$industrySlug = (string) ($GLOBALS['epc_industry_subdomain_slug'] ?? '');
		$profile = array(
			'industry' => $industryGroup,
			'system_name' => ucwords(str_replace(array('_', '-'), ' ', $industrySlug)) . ' — ECOM AE',
			'hub_name' => 'ecomae',
			'tagline' => ucwords(str_replace(array('_', '-'), ' ', $industryGroup)) . ' industry solutions',
			'domain_path' => 'https://' . $host . '/',
		);
	}
	if ($profile === null) {
		$profile = array(
			'industry' => 'auto_parts',
			'system_name' => 'ECOM AE portal',
			'hub_name' => 'ecomae',
			'tagline' => 'Designed by ecomae',
			'domain_path' => epc_portal_guess_domain_path($host),
		);
	}
	if (empty($profile['domain_path'])) {
		$profile['domain_path'] = epc_portal_guess_domain_path($host);
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
	$dbSettings = epc_portal_load_site_settings();
	$platformProfile = null;
	if (function_exists('epc_portal_sites')) {
		$sitesMap = epc_portal_sites();
		if (isset($sitesMap[$host]) && ($sitesMap[$host]['industry'] ?? '') === 'platform_host') {
			$platformProfile = $sitesMap[$host];
		}
	}
	if ($platformProfile !== null && empty($GLOBALS['epc_demo_storefront_context']) && empty($GLOBALS['epc_industry_subdomain_active'])) {
		$profile['industry'] = 'platform_host';
		if (!empty($platformProfile['db'])) {
			$profile['db'] = $platformProfile['db'];
		}
		if (!empty($platformProfile['user'])) {
			$profile['user'] = $platformProfile['user'];
		}
		if (!empty($platformProfile['domain_path'])) {
			$profile['domain_path'] = $platformProfile['domain_path'];
		}
	} elseif (!empty($dbSettings['industry_code']) && empty($GLOBALS['epc_industry_subdomain_active'])) {
		$profile['industry'] = $dbSettings['industry_code'];
	}
	if (!empty($dbSettings['system_name'])) {
		$profile['system_name'] = $dbSettings['system_name'];
	}
	if (!empty($dbSettings['hub_name'])) {
		$profile['hub_name'] = $dbSettings['hub_name'];
	}
	if (!empty($dbSettings['tagline'])) {
		$profile['tagline'] = $dbSettings['tagline'];
	}
	if (!empty($dbSettings['theme'])) {
		$profile['theme'] = $dbSettings['theme'];
	}
	if (!empty($dbSettings['domain_path']) && !epc_portal_is_client_hostname($host) && empty($GLOBALS['epc_industry_subdomain_active'])) {
		$profile['domain_path'] = $dbSettings['domain_path'];
	}
	if (epc_portal_is_client_hostname($host)) {
		$profile['domain_path'] = 'https://' . $host . '/';
	}
	if (!empty($dbSettings['contact']) && is_array($dbSettings['contact'])) {
		$profile['contact'] = $dbSettings['contact'];
	}
	if (function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname()
		&& function_exists('epc_portal_is_cp_request') && epc_portal_is_cp_request()) {
		$sharedErpFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
		if (is_file($sharedErpFile)) {
			require_once $sharedErpFile;
			if (function_exists('epc_portal_shared_erp_active_tenant')) {
				$sharedRow = epc_portal_shared_erp_active_tenant();
				if ($sharedRow !== null) {
					$trade = trim((string) ($sharedRow['trade_name'] ?? ''));
					if ($trade !== '') {
						$profile['trade_name'] = $trade;
						$profile['system_name'] = $trade;
						$profile['hub_name'] = $trade;
					}
					$profile['site_key'] = (string) ($sharedRow['site_key'] ?? '');
					if (!empty($sharedRow['industry_code'])) {
						$profile['industry'] = (string) $sharedRow['industry_code'];
					}
					if (!empty($sharedRow['db_name'])) {
						$profile['db'] = (string) $sharedRow['db_name'];
						$profile['user'] = (string) ($sharedRow['db_user'] ?? $sharedRow['db_name']);
						$profile['password'] = (string) ($sharedRow['db_password'] ?? '');
					}
					$profile['access_mode'] = 'erp_only';
					$profile['erp_only_shared'] = 1;
				}
			}
		}
	}
	if ($host !== '') {
		$profiles[$host] = $profile;
	}
	return $profile;
}

function epc_portal_industry($code = null)
{
	$all = epc_portal_industries();
	$site = null;
	if ($code === null) {
		$site = epc_portal_site_profile();
		$code = isset($site['industry']) ? $site['industry'] : 'auto_parts';
	}
	$row = isset($all[$code]) ? $all[$code] : $all['auto_parts'];
	if ($site !== null && !empty($site['theme']) && is_array($site['theme'])) {
		$row['theme'] = array_merge(isset($row['theme']) ? $row['theme'] : array(), $site['theme']);
	}
	return $row;
}

function epc_portal_apply_config($DP_Config)
{
	if (!is_object($DP_Config)) {
		return;
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
	if (function_exists('epc_portal_resolve_tenant_db')) {
		epc_portal_resolve_tenant_db($DP_Config);
	}
	$isDemoCp = !empty($GLOBALS['epc_demo_cp_context']);
	$isDemoStorefront = !empty($GLOBALS['epc_demo_storefront_context']);
	$isDemoIsolated = $isDemoCp || $isDemoStorefront;
	$site = epc_portal_site_profile();
	$isClientHost = function_exists('epc_portal_is_client_hostname') && epc_portal_is_client_hostname();
	if (!$isDemoIsolated && !empty($site['domain_path'])) {
		$DP_Config->domain_path = $site['domain_path'];
	}
	if (!$isDemoIsolated && !$isClientHost && !empty($site['db'])) {
		$DP_Config->db = $site['db'];
	}
	if (!$isDemoIsolated && !$isClientHost && !empty($site['user'])) {
		$DP_Config->user = $site['user'];
	}
	if (!$isDemoIsolated && !$isClientHost && !empty($site['password'])) {
		$DP_Config->password = $site['password'];
	}
	if (!$isDemoIsolated && !empty($site['industry'])) {
		$DP_Config->epc_portal_industry = $site['industry'];
	}
	// Platform config.local.php may set ecomae operator DB — never override tenant registry credentials.
	if (file_exists($_SERVER['DOCUMENT_ROOT'] . '/config.local.php')) {
		require $_SERVER['DOCUMENT_ROOT'] . '/config.local.php';
		if (isset($epc_config_local) && is_array($epc_config_local)) {
			$skipLocalKeys = !epc_portal_is_platform_hostname()
				? array('db', 'user', 'password', 'domain_path', 'host', 'host_external')
				: array();
			if (!empty($GLOBALS['epc_demo_storefront_context']) || !empty($GLOBALS['epc_demo_cp_context'])) {
				$skipLocalKeys = array_values(array_unique(array_merge(
					$skipLocalKeys,
					array('db', 'user', 'password', 'domain_path', 'epc_portal_industry')
				)));
			}
			foreach ($epc_config_local as $key => $value) {
				if (in_array($key, $skipLocalKeys, true)) {
					continue;
				}
				if (property_exists($DP_Config, $key)) {
					$DP_Config->$key = $value;
				}
			}
		}
	}
	if (!$isClientHost && !empty($site['db']) && !epc_portal_is_platform_hostname()) {
		$DP_Config->db = $site['db'];
	}
	if (!$isClientHost && !empty($site['user']) && !epc_portal_is_platform_hostname()) {
		$DP_Config->user = $site['user'];
	}
	if (!$isClientHost && !empty($site['password']) && !epc_portal_is_platform_hostname()) {
		$DP_Config->password = $site['password'];
	}
	if (!empty($site['domain_path']) && !$isDemoIsolated) {
		$DP_Config->domain_path = $site['domain_path'];
	}
	$siteCtxFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_site_context.php';
	if (is_file($siteCtxFile)) {
		require_once $siteCtxFile;
		epc_site_apply_contact_overrides($DP_Config);
	}
	if (epc_portal_is_platform_hostname()) {
		if ($isDemoIsolated) {
			if (!empty($DP_Config->epc_portal_industry)) {
				$DP_Config->epc_portal_industry = (string) $DP_Config->epc_portal_industry;
			}
			if ($isDemoCp && function_exists('epc_portal_demo_reapply_cp_config')) {
				epc_portal_demo_reapply_cp_config($DP_Config);
			}
		} else {
		$host = epc_portal_host();
		$clientErpBound = false;
		$sharedErpFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
		if (is_file($sharedErpFile)) {
			require_once $sharedErpFile;
		}
		// /cp/client-erp/{site_key}/ — bind ONLY that tenant registry DB (never ecomae/docpart).
		if (function_exists('epc_client_erp_is_active') && epc_client_erp_is_active()) {
			$tenantRow = $GLOBALS['epc_client_erp_tenant_row'] ?? null;
			if (!is_array($tenantRow) && function_exists('epc_client_erp_tenant_row')) {
				$tenantRow = epc_client_erp_tenant_row();
			}
			if (is_array($tenantRow) && function_exists('epc_portal_shared_erp_apply_row_config')) {
				$clientErpBound = epc_portal_shared_erp_apply_row_config($DP_Config, $tenantRow);
			}
		}
		if (!$clientErpBound) {
			$sites = epc_portal_sites();
			if (isset($sites[$host])) {
				foreach (array('db', 'user', 'password', 'domain_path') as $key) {
					if (!empty($sites[$host][$key])) {
						$DP_Config->$key = $sites[$host][$key];
					}
				}
			}
			$DP_Config->epc_portal_industry = 'platform_host';
		}
		}
	} elseif (function_exists('epc_portal_is_client_hostname') && epc_portal_is_client_hostname()) {
		$host = epc_portal_host();
		if ($host !== '') {
			$DP_Config->domain_path = 'https://' . $host . '/';
		}
		$site = epc_portal_site_profile();
		if (!empty($site['shop_currency'])) {
			$DP_Config->shop_currency = (string) $site['shop_currency'];
		} elseif ((string) ($DP_Config->shop_currency ?? '') === '643') {
			$DP_Config->shop_currency = '784';
		}
	}
	if (function_exists('epc_portal_demo_lock_domain_path')) {
		epc_portal_demo_lock_domain_path($DP_Config);
	}
	// Re-apply after site profile / config.local — registry must not override docpart on client CP.
	if ($isClientHost && function_exists('epc_portal_resolve_tenant_db')) {
		epc_portal_resolve_tenant_db($DP_Config);
	}
}

/** Per-tenant GA4 measurement ID (PDF: separate properties per tenant). */
function epc_portal_ga_measurement_id(string $host = null): string
{
	if ($host === null) {
		$host = epc_portal_host();
	}
	$host = strtolower(trim($host));
	$profile = epc_portal_site_profile();
	if (!empty($profile['contact']['ga_measurement_id'])) {
		return (string) $profile['contact']['ga_measurement_id'];
	}
	$map = array(
		'www.epartscart.com' => 'G-EPARTSCART1',
		'www.taxofinca.com' => 'G-TAXOFINCA01',
		'www.electronicae.com' => 'G-ELECTRONIC01',
		'www.stylenlook.com' => 'G-STYLENLOOK1',
		'www.thejewellerytrend.com' => 'G-JEWELLERY01',
	);
	return $map[$host] ?? 'G-J19D1KHXCG';
}

/** Floating seller button label — retail vs B2B (PDF §9.5). */
function epc_portal_seller_request_label(): string
{
	$industry = epc_portal_industry();
	$code = isset($industry['code']) ? (string) $industry['code'] : '';
	if (in_array($code, array('jewellery', 'fashion', 'electronics'), true)) {
		return 'Ask a question';
	}
	if ($code === 'tax_advisory') {
		return 'Contact us';
	}
	return 'Request to seller';
}

function epc_portal_cp_industry_session_key()
{
	return 'epc_cp_industry_filter';
}

function epc_portal_cp_active_industry()
{
	if (function_exists('epc_portal_demo_is_cp_context') && epc_portal_demo_is_cp_context()) {
		$row = $GLOBALS['epc_demo_cp_tenant_row'] ?? null;
		if (is_array($row)) {
			$registryIndustry = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($row['industry_code'] ?? '')));
			if ($registryIndustry !== '' && $registryIndustry !== 'platform_host') {
				return $registryIndustry;
			}
		}
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
	$settings = epc_portal_load_site_settings();
	if (!empty($GLOBALS['epc_demo_cp_context']) && !empty($settings['industry_code'])) {
		$row = $GLOBALS['epc_demo_cp_tenant_row'] ?? null;
		$registryIndustry = is_array($row)
			? preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($row['industry_code'] ?? '')))
			: '';
		if ($registryIndustry !== '' && $registryIndustry !== 'platform_host'
			&& (string) $settings['industry_code'] === 'platform_host') {
			return $registryIndustry;
		}
	}
	if (!empty($settings['industry_code'])) {
		return $settings['industry_code'];
	}
	$site = epc_portal_site_profile();
	return isset($site['industry']) ? $site['industry'] : 'auto_parts';
}

function epc_portal_cp_item_visible($item_url)
{
	if (function_exists('epc_portal_demo_is_cp_context') && epc_portal_demo_is_cp_context()) {
		// Demo CP uses tenant shop menu only — no Super CP operator bypass.
	} elseif (function_exists('epc_portal_is_platform_operator') && epc_portal_is_platform_operator()) {
		return true;
	}
	$url = strtolower((string) $item_url);
	if (strpos($url, 'control/portal') !== false) {
		return true;
	}
	if (strpos($url, '/shop/pos') !== false) {
		return true;
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
	$packs = epc_portal_enabled_packs();
	$routes = epc_portal_cp_pack_routes();
	foreach ($packs as $pack) {
		if (!isset($routes[$pack])) {
			continue;
		}
		foreach ($routes[$pack] as $prefix) {
			if (strpos($url, strtolower($prefix)) !== false) {
				return true;
			}
		}
	}
	if (strpos($url, '/cp') !== false && substr_count($url, '/') <= 2) {
		return true;
	}
	return false;
}

function epc_portal_theme_css($for_cp = false)
{
	$industry = epc_portal_industry();
	$t = isset($industry['theme']) ? $industry['theme'] : array();
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
	$siteSettings = epc_portal_load_site_settings();
	if (!empty($siteSettings['theme']) && is_array($siteSettings['theme'])) {
		$t = array_merge($t, $siteSettings['theme']);
	}
	$primary = isset($t['primary']) ? $t['primary'] : '#2563eb';
	$primary_dark = isset($t['primary_dark']) ? $t['primary_dark'] : '#1d4ed8';
	$accent = isset($t['accent']) ? $t['accent'] : '#38bdf8';
	$sidebar_from = isset($t['sidebar_from']) ? $t['sidebar_from'] : '#0f172a';
	$sidebar_to = isset($t['sidebar_to']) ? $t['sidebar_to'] : '#1e293b';
	$hero_from = isset($t['hero_from']) ? $t['hero_from'] : '#0b1220';
	$hero_to = isset($t['hero_to']) ? $t['hero_to'] : '#1e3a5f';
	$code = isset($industry['code']) ? $industry['code'] : 'default';
	$styleTpl = 'classic';
	if (!empty($siteSettings['theme_template'])) {
		$styleTpl = preg_replace('/[^a-z0-9_]/', '', (string) $siteSettings['theme_template']);
	}

	$css = ':root{';
	$css .= '--epc-portal-primary:' . $primary . ';';
	$css .= '--epc-portal-primary-dark:' . $primary_dark . ';';
	$css .= '--epc-portal-accent:' . $accent . ';';
	$css .= '--epc-portal-hero-from:' . $hero_from . ';';
	$css .= '--epc-portal-hero-to:' . $hero_to . ';';
	if ($for_cp) {
		$css .= '--epc-cp-primary:' . $primary . ';';
		$css .= '--epc-cp-primary-dark:' . $primary_dark . ';';
		$css .= '--epc-cp-sidebar-from:' . $sidebar_from . ';';
		$css .= '--epc-cp-sidebar-to:' . $sidebar_to . ';';
	}
	$css .= '}';
	$css .= 'html[data-epc-industry="' . $code . '"][data-epc-style="' . $styleTpl . '"] .epc-home-pro .container:before,';
	$css .= 'html[data-epc-industry="' . $code . '"] .epc-home-pro .container:before{';
	$css .= 'background:radial-gradient(circle at 20% 25%,' . $accent . '44,transparent 28%),';
	$css .= 'radial-gradient(circle at 80% 15%,' . $primary . '33,transparent 32%),';
	$css .= 'linear-gradient(135deg,' . $hero_from . ',' . $hero_to . ');}';
	$css .= '.epc-portal-hero .btn-primary,.epc-home-pro .btn-primary{background:' . $primary . ';border-color:' . $primary . ';}';
	$css .= '.epc-portal-hero .btn-primary:hover,.epc-home-pro .btn-primary:hover{background:' . $primary_dark . ';border-color:' . $primary_dark . ';}';
	if ($code === 'electronics') {
		$css .= 'html[data-epc-industry="electronics"] .top-menu-line{background:#000!important;border-bottom:3px solid ' . $primary . ';}';
		$css .= 'html[data-epc-industry="electronics"] .schearch-line{border-bottom:2px solid #000;}';
		$css .= 'html[data-epc-industry="electronics"] .product_div_tile .product_price{color:' . $primary . ';font-weight:800;}';
	}
	return $css;
}

function epc_portal_is_cp_request()
{
	if (!isset($_SERVER['REQUEST_URI'])) {
		return false;
	}
	$path = parse_url((string) $_SERVER['REQUEST_URI'], PHP_URL_PATH);
	if (!is_string($path) || $path === '') {
		return false;
	}
	$backend = 'cp';
	if (isset($GLOBALS['DP_Config']) && is_object($GLOBALS['DP_Config']) && !empty($GLOBALS['DP_Config']->backend_dir)) {
		$backend = trim((string) $GLOBALS['DP_Config']->backend_dir, '/');
	}
	if ($backend === '') {
		$backend = 'cp';
	}
	$base = '/' . $backend;
	return $path === $base || $path === $base . '/'
		|| (strlen($path) > strlen($base) && strpos($path, $base . '/') === 0);
}

function epc_portal_platform_operator_pdo(): ?PDO
{
	static $pdo = null;
	static $tried = false;
	if ($tried) {
		return $pdo;
	}
	$tried = true;
	$cfgFile = $_SERVER['DOCUMENT_ROOT'] . '/config.local.php';
	if (!is_file($cfgFile)) {
		return null;
	}
	$epc_config_local = null;
	require $cfgFile;
	if (!isset($epc_config_local) || !is_array($epc_config_local)) {
		return null;
	}
	$db = trim((string) ($epc_config_local['db'] ?? ''));
	$user = trim((string) ($epc_config_local['user'] ?? ''));
	$pass = (string) ($epc_config_local['password'] ?? '');
	if ($db === '' || $user === '') {
		return null;
	}
	try {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
		$cfg = new DP_Config();
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $db . ';charset=utf8',
			$user,
			$pass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Exception $e) {
		$pdo = null;
	}
	return $pdo;
}

/** Shared ERP / tenant-session guards for platform operator cookie checks. */
function epc_portal_platform_operator_session_blocked(): bool
{
	if (!empty($GLOBALS['epc_demo_cp_context'])) {
		return true;
	}
	$sharedErpFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
	if (!is_file($sharedErpFile)) {
		return false;
	}
	require_once $sharedErpFile;
	if (function_exists('epc_portal_is_shared_erp_cp_session') && epc_portal_is_shared_erp_cp_session()) {
		return true;
	}
	if (function_exists('epc_portal_shared_erp_infer_tenant_from_session')) {
		$inferred = epc_portal_shared_erp_infer_tenant_from_session();
		if ($inferred !== null) {
			return true;
		}
	}
	return false;
}

/**
 * Validate Super CP operator cookies against platform registry DB (ecomae).
 * Works outside /cp/ URI — used by demo CP autologin handoff on www.ecomae.com.
 */
function epc_portal_platform_operator_session_valid(?int $expectedUserId = null): bool
{
	if (epc_portal_platform_operator_session_blocked()) {
		return false;
	}
	if (!function_exists('epc_portal_is_platform_operator_host') || !epc_portal_is_platform_operator_host()) {
		return false;
	}
	$session = isset($_COOKIE['admin_session']) ? (string) $_COOKIE['admin_session'] : '';
	$userId = isset($_COOKIE['admin_u_id']) ? (int) $_COOKIE['admin_u_id'] : 0;
	if ($session === '' || $userId <= 0) {
		return false;
	}
	if ($expectedUserId !== null && $expectedUserId > 0 && $userId !== $expectedUserId) {
		return false;
	}
	$pdo = epc_portal_platform_operator_pdo();
	if (!$pdo instanceof PDO) {
		return false;
	}
	try {
		$st = $pdo->prepare('SELECT COUNT(*) FROM `sessions` WHERE `session` = ? AND `type` = 1 AND `user_id` = ?');
		$st->execute(array($session, $userId));
		return ((int) $st->fetchColumn()) === 1;
	} catch (Exception $e) {
		return false;
	}
}

/** Super CP operator session must live in platform registry DB (ecomae), not tenant/docpart. */
function epc_portal_is_platform_operator(): bool
{
	if (!function_exists('epc_portal_is_super_cp_host') || !epc_portal_is_super_cp_host()) {
		return false;
	}
	return epc_portal_platform_operator_session_valid();
}

function epc_portal_is_super_cp_host()
{
	if (function_exists('epc_portal_demo_is_cp_context') && epc_portal_demo_is_cp_context()) {
		return false;
	}
	if (function_exists('epc_portal_demo_is_cp_request') && epc_portal_demo_is_cp_request()) {
		return false;
	}
	$host = epc_portal_host();
	if ($host === 'cp.ecomae.com') {
		return true;
	}
	if (function_exists('epc_portal_sites')) {
		$sites = epc_portal_sites();
		if ($host !== '' && isset($sites[$host]['super_cp']) && !empty($sites[$host]['super_cp'])) {
			return true;
		}
	}
	// Super CP also runs under /cp/ on the platform marketing host (www.ecomae.com).
	if (function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname()
		&& function_exists('epc_portal_is_cp_request') && epc_portal_is_cp_request()) {
		return true;
	}
	return false;
}

function epc_portal_is_auto_parts_site()
{
	$site = epc_portal_site_profile();
	return isset($site['industry']) && $site['industry'] === 'auto_parts';
}

function epc_portal_electronics_retail_enabled()
{
	$site = epc_portal_site_profile();
	if (!isset($site['industry']) || $site['industry'] !== 'electronics') {
		return false;
	}
	if (!function_exists('epc_portal_commerce_storefront_enabled') || !epc_portal_commerce_storefront_enabled()) {
		return false;
	}
	return epc_portal_active_storefront_package() === 'electronics_retail_virgin';
}

function epc_portal_consulting_primeinvest_enabled()
{
	$site = epc_portal_site_profile();
	if (!isset($site['industry']) || !in_array($site['industry'], array('tax_advisory', 'consultancy'), true)) {
		return false;
	}
	return epc_portal_active_storefront_package() === 'consulting_primeinvest';
}

function epc_portal_fashion_retail_namshi_enabled()
{
	$site = epc_portal_site_profile();
	if (!isset($site['industry']) || $site['industry'] !== 'fashion') {
		return false;
	}
	if (!function_exists('epc_portal_commerce_storefront_enabled') || !epc_portal_commerce_storefront_enabled()) {
		return false;
	}
	return epc_portal_active_storefront_package() === 'fashion_retail_namshi';
}

function epc_portal_jewellery_retail_kiyasha_enabled()
{
	$site = epc_portal_site_profile();
	if (!isset($site['industry']) || $site['industry'] !== 'jewellery') {
		return false;
	}
	if (!function_exists('epc_portal_commerce_storefront_enabled') || !epc_portal_commerce_storefront_enabled()) {
		return false;
	}
	return epc_portal_active_storefront_package() === 'jewellery_retail_kiyasha';
}

function epc_portal_automotive_spareparts_pro_enabled()
{
	$site = epc_portal_site_profile();
	if (!isset($site['industry']) || $site['industry'] !== 'auto_parts') {
		return false;
	}
	if (!function_exists('epc_portal_commerce_storefront_enabled') || !epc_portal_commerce_storefront_enabled()) {
		return false;
	}
	return epc_portal_active_storefront_package() === 'automotive_spareparts_pro';
}

function epc_portal_active_storefront_package()
{
	$settings = epc_portal_load_site_settings();
	$contact = isset($settings['contact']) && is_array($settings['contact']) ? $settings['contact'] : array();
	if (!empty($contact['storefront_package'])) {
		$explicit = preg_replace('/[^a-z0-9_]/', '', (string) $contact['storefront_package']);
		if ($explicit !== '') {
			require_once __DIR__ . '/epc_portal_storefront_packages.php';
			if (epc_portal_storefront_package_meta($explicit)) {
				return $explicit;
			}
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

/**
 * Get the consolidated industry group for the current tenant.
 * Used by storefront, CP, and ERP to determine which template group applies.
 *
 * @return array{group_key: string, group: array, sub_areas: array}
 */
function epc_portal_consolidated_industry_info(): array
{
	static $cached = null;
	if ($cached !== null) {
		return $cached;
	}

	require_once __DIR__ . '/epc_industry_consolidation.php';

	$settings = epc_portal_load_site_settings();
	$industryCode = isset($settings['industry_code']) ? (string) $settings['industry_code'] : 'general';
	$groupKey = epc_industry_resolve_group($industryCode);
	$group = epc_industry_get_group($industryCode);

	$cached = array(
		'group_key' => $groupKey,
		'group' => $group,
		'industry_code' => $industryCode,
		'template_key' => $group['template_key'] ?? 'retail',
		'erp_base' => $group['erp_base'] ?? 'general',
		'costing_default' => $group['costing_default'] ?? 'weighted_avg',
	);
	return $cached;
}

function epc_portal_home_mode()
{
	if (!empty($GLOBALS['epc_demo_storefront_context']) || !empty($GLOBALS['epc_demo_cp_context'])) {
		global $DP_Config;
		$industry = !empty($GLOBALS['epc_demo_cp_context'])
			? (string) (($GLOBALS['epc_demo_cp_tenant_row']['industry_code'] ?? '')
				?: (is_object($DP_Config) ? (string) ($DP_Config->epc_portal_industry ?? 'auto_parts') : 'auto_parts'))
			: (string) ($GLOBALS['epc_demo_storefront_industry'] ?? 'auto_parts');
		if ($industry === 'auto_parts') {
			return 'automotive_spareparts_pro';
		}
		if ($industry === 'fashion') {
			return 'fashion_retail';
		}
		return $industry !== '' ? $industry : 'auto_parts';
	}
	$site = epc_portal_site_profile();
	$industry = isset($site['industry']) ? $site['industry'] : 'auto_parts';
	if ($industry === 'platform_host' && !epc_portal_is_super_cp_host()) {
		return 'platform';
	}
	if (epc_portal_automotive_spareparts_pro_enabled()) {
		return 'automotive_spareparts_pro';
	}
	if ($industry === 'auto_parts') {
		return 'auto_parts';
	}
	if (epc_portal_electronics_retail_enabled()) {
		return 'electronics_retail';
	}
	if (epc_portal_consulting_primeinvest_enabled()) {
		return 'consulting_primeinvest';
	}
	if (epc_portal_fashion_retail_namshi_enabled()) {
		return 'fashion_retail';
	}
	if (epc_portal_jewellery_retail_kiyasha_enabled()) {
		return 'jewellery_retail';
	}
	return 'professional';
}

/** full / full_commerce = storefront + commerce; mixed = partial ERP + optional commerce; erp_only = ERP home */
function epc_portal_resolve_access_mode(array $settings)
{
	$mode = isset($settings['access_mode']) ? (string) $settings['access_mode'] : 'full';
	if ($mode === 'full_commerce') {
		$mode = 'full';
	}
	if (!in_array($mode, array('full', 'erp_only', 'consultancy', 'mixed'), true)) {
		$mode = 'full';
	}
	if ($mode === 'consultancy') {
		return 'consultancy';
	}
	if ($mode === 'mixed') {
		return 'mixed';
	}
	if ($mode === 'full') {
		$packs = isset($settings['enabled_packs']) && is_array($settings['enabled_packs']) ? $settings['enabled_packs'] : array();
		$hasErp = in_array('erp', $packs, true);
		$hasStore = in_array('commerce', $packs, true) || in_array('catalogue', $packs, true) || in_array('auto_parts', $packs, true);
		if ($hasErp && !$hasStore) {
			$mode = 'erp_only';
		}
	}
	return $mode;
}

function epc_portal_access_mode()
{
	$settings = epc_portal_load_site_settings();
	return epc_portal_resolve_access_mode($settings);
}

function epc_portal_storefront_enabled()
{
	return epc_portal_access_mode() !== 'erp_only';
}

/** Parts catalog, cart, and auto-parts header chrome (false for consultancy / ERP-only). */
function epc_portal_commerce_storefront_enabled()
{
	if (!epc_portal_storefront_enabled()) {
		return false;
	}
	if (epc_portal_access_mode() === 'consultancy') {
		return false;
	}
	$packs = epc_portal_enabled_packs();
	return in_array('commerce', $packs, true)
		|| in_array('catalogue', $packs, true)
		|| in_array('auto_parts', $packs, true);
}

function epc_portal_erp_url($langPrefix = '')
{
	global $DP_Config;
	if ($langPrefix === '' && function_exists('epc_erp_lang_href')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_access.php';
		$langPrefix = epc_erp_lang_href();
	}
	if ($langPrefix !== '' && isset($langPrefix[0]) && $langPrefix[0] === '/') {
		$scheme = parse_url((string) ($DP_Config->domain_path ?? ''), PHP_URL_SCHEME);
		$host = parse_url((string) ($DP_Config->domain_path ?? ''), PHP_URL_HOST);
		if ($scheme !== null && $scheme !== false && $host !== null && $host !== false && $host !== '') {
			return $scheme . '://' . $host . rtrim($langPrefix, '/') . '/erp';
		}
	}
	$base = rtrim((string) ($DP_Config->domain_path ?? ''), '/');
	return $base . ($langPrefix !== '' ? $langPrefix : '') . '/erp';
}

/** Module packs for ERP-only tenants (no commerce / catalogue / marketing). */
function epc_portal_erp_only_packs(): array
{
	return array('core', 'erp', 'professional', 'logistics');
}

function epc_portal_is_erp_only_tenant(): bool
{
	return epc_portal_access_mode() === 'erp_only';
}

/** True when tenant has a customer-facing storefront / commerce CP (false for erp_only_shared). */
function epc_portal_tenant_has_storefront(): bool
{
	if (!epc_portal_storefront_enabled()) {
		return false;
	}
	return epc_portal_commerce_storefront_enabled();
}

/** URL prefix for CP navigation links (demo CP uses /cp/demo/{site_key}). */
function epc_cp_nav_url_prefix(): string
{
	if (function_exists('epc_portal_demo_is_cp_context') && epc_portal_demo_is_cp_context()) {
		$key = function_exists('epc_portal_demo_cp_site_key') ? epc_portal_demo_cp_site_key() : '';
		if ($key !== '' && function_exists('epc_portal_demo_cp_tenant_base')) {
			return rtrim(epc_portal_demo_cp_tenant_base($key), '/');
		}
	}
	global $DP_Config;
	$backend = isset($DP_Config->backend_dir) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
	if ($backend === '') {
		$backend = 'cp';
	}
	return '/' . $backend;
}

/** Canonical CP dashboard URL (/cp/control on all hosts). */
function epc_cp_control_url(?string $backend = null): string
{
	if ($backend === null || $backend === '') {
		return epc_cp_nav_url_prefix() . '/control';
	}
	$backend = trim($backend, '/');
	if ($backend === '') {
		$backend = 'cp';
	}
	return '/' . $backend . '/control';
}

/** Default post-login / sidebar home for tenant CP sessions. */
function epc_cp_tenant_landing_url(?string $backend = null): string
{
	return epc_cp_control_url($backend);
}

/** CP login landing for ERP-only tenants — professional shell, no shop sidebar. */
function epc_portal_erp_cp_shell_url(): string
{
	if (function_exists('epc_portal_demo_cp_is_erp_only') && epc_portal_demo_cp_is_erp_only()
		&& function_exists('epc_portal_demo_erp_shell_url') && function_exists('epc_portal_demo_cp_site_key')) {
		$key = epc_portal_demo_cp_site_key();
		if ($key !== '') {
			return epc_portal_demo_erp_shell_url($key);
		}
	}
	if (function_exists('epc_client_erp_shell_url') && function_exists('epc_client_erp_site_key')) {
		$key = epc_client_erp_site_key();
		if ($key !== '') {
			return epc_client_erp_shell_url($key);
		}
	}
	global $DP_Config;
	$backend = isset($DP_Config->backend_dir) ? (string) $DP_Config->backend_dir : 'cp';
	return '/' . $backend . '/shop/finance/erp?epc_erp_shell=1';
}
