<?php
/**
 * Multi-industry portal registry — portable CP for many verticals.
 * Site hostname selects default industry + branding; CP can filter modules by industry.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

function epc_portal_industries()
{
	return array(
		'auto_parts' => array(
			'code' => 'auto_parts',
			'name' => 'Auto spare parts',
			'ecosystem' => 'commerce',
			'icon' => 'fa-car',
			'theme' => array(
				'primary' => '#dc2626',
				'primary_dark' => '#991b1b',
				'accent' => '#f97316',
				'sidebar_from' => '#0f172a',
				'sidebar_to' => '#1e293b',
				'hero_from' => '#0b1220',
				'hero_to' => '#1e3a5f',
			),
			'cp_packs' => array('core', 'commerce', 'auto_parts', 'logistics', 'erp', 'professional', 'marketing'),
		),
		'tax_advisory' => array(
			'code' => 'tax_advisory',
			'name' => 'Tax & advisory',
			'ecosystem' => 'business_services',
			'icon' => 'fa-balance-scale',
			'theme' => array(
				'primary' => '#0d9488',
				'primary_dark' => '#0f766e',
				'accent' => '#14b8a6',
				'sidebar_from' => '#042f2e',
				'sidebar_to' => '#134e4a',
				'hero_from' => '#042f2e',
				'hero_to' => '#115e59',
			),
			'cp_packs' => array('core', 'commerce', 'professional', 'tax_advisory', 'erp', 'logistics', 'marketing'),
		),
		'fashion' => array(
			'code' => 'fashion',
			'name' => 'Fashion & apparel',
			'ecosystem' => 'commerce',
			'icon' => 'fa-shopping-bag',
			'theme' => array(
				'primary' => '#be185d',
				'primary_dark' => '#9d174d',
				'accent' => '#ec4899',
				'sidebar_from' => '#1f1020',
				'sidebar_to' => '#4a1942',
				'hero_from' => '#1f1020',
				'hero_to' => '#701a75',
			),
			'cp_packs' => array('core', 'commerce', 'catalogue'),
		),
		'electronics' => array(
			'code' => 'electronics',
			'name' => 'Electronics',
			'ecosystem' => 'commerce',
			'icon' => 'fa-microchip',
			'storefront_package' => 'electronics_retail_virgin',
			'theme_template_default' => 'midnight',
			'theme' => array(
				'primary' => '#e10a0a',
				'primary_dark' => '#b00808',
				'accent' => '#000000',
				'sidebar_from' => '#000000',
				'sidebar_to' => '#1a1a1a',
				'hero_from' => '#000000',
				'hero_to' => '#2d2d2d',
			),
			'cp_packs' => array('core', 'commerce', 'catalogue'),
		),
		'medical' => array(
			'code' => 'medical',
			'name' => 'Medical supplies',
			'ecosystem' => 'commerce',
			'icon' => 'fa-medkit',
			'theme' => array(
				'primary' => '#0284c7',
				'primary_dark' => '#0369a1',
				'accent' => '#22d3ee',
				'sidebar_from' => '#0c4a6e',
				'sidebar_to' => '#164e63',
				'hero_from' => '#0c4a6e',
				'hero_to' => '#155e75',
			),
			'cp_packs' => array('core', 'commerce', 'catalogue', 'professional', 'erp'),
		),
		'health' => array(
			'code' => 'health',
			'name' => 'Health & wellness',
			'ecosystem' => 'lifestyle_consumer',
			'icon' => 'fa-heartbeat',
			'theme' => array(
				'primary' => '#16a34a',
				'primary_dark' => '#15803d',
				'accent' => '#4ade80',
				'sidebar_from' => '#14532d',
				'sidebar_to' => '#166534',
				'hero_from' => '#14532d',
				'hero_to' => '#15803d',
			),
			'cp_packs' => array('core', 'commerce', 'catalogue'),
		),
		'consultancy' => array(
			'code' => 'consultancy',
			'name' => 'Consultancy',
			'ecosystem' => 'business_services',
			'icon' => 'fa-briefcase',
			'theme' => array(
				'primary' => '#7c3aed',
				'primary_dark' => '#6d28d9',
				'accent' => '#a78bfa',
				'sidebar_from' => '#2e1065',
				'sidebar_to' => '#4c1d95',
				'hero_from' => '#2e1065',
				'hero_to' => '#5b21b6',
			),
			'cp_packs' => array('core', 'professional', 'erp', 'commerce'),
		),
		'erp_standalone' => array(
			'code' => 'erp_standalone',
			'name' => 'ERP standalone (no storefront)',
			'ecosystem' => 'business_services',
			'icon' => 'fa-university',
			'theme' => array(
				'primary' => '#0369a1',
				'primary_dark' => '#075985',
				'accent' => '#0ea5e9',
				'sidebar_from' => '#0c4a6e',
				'sidebar_to' => '#075985',
				'hero_from' => '#082f49',
				'hero_to' => '#0c4a6e',
			),
			'cp_packs' => array('core', 'erp', 'professional', 'logistics'),
		),
		'platform_host' => array(
			'code' => 'platform_host',
			'name' => 'Platform host (ecomae)',
			'ecosystem' => 'platform',
			'icon' => 'fa-cloud',
			'theme' => array(
				'primary' => '#0ea5e9',
				'primary_dark' => '#0284c7',
				'accent' => '#38bdf8',
				'sidebar_from' => '#0c4a6e',
				'sidebar_to' => '#075985',
				'hero_from' => '#082f49',
				'hero_to' => '#0c4a6e',
			),
			'cp_packs' => array('core', 'professional', 'erp', 'marketing', 'super_platform'),
		),
		'rental' => array(
			'code' => 'rental',
			'name' => 'Rental & leasing',
			'ecosystem' => 'asset_sharing',
			'icon' => 'fa-key',
			'theme' => array(
				'primary' => '#ca8a04',
				'primary_dark' => '#a16207',
				'accent' => '#facc15',
				'sidebar_from' => '#422006',
				'sidebar_to' => '#713f12',
				'hero_from' => '#422006',
				'hero_to' => '#854d0e',
			),
			'cp_packs' => array('core', 'commerce', 'catalogue'),
		),
		'jewellery' => array(
			'code' => 'jewellery',
			'name' => 'Jewellery',
			'ecosystem' => 'commerce',
			'icon' => 'fa-diamond',
			'theme' => array(
				'primary' => '#b45309',
				'primary_dark' => '#92400e',
				'accent' => '#fbbf24',
				'sidebar_from' => '#1c1917',
				'sidebar_to' => '#44403c',
				'hero_from' => '#1c1917',
				'hero_to' => '#78350f',
			),
			'cp_packs' => array('core', 'commerce', 'catalogue'),
		),
	);
}

/**
 * ECOM AE strategic ecosystem taxonomy.
 * Existing industry codes remain unchanged; expansion codes are placeholders for future onboarding.
 */
function epc_portal_ecosystems(): array
{
	return array(
		'commerce' => array(
			'code' => 'commerce',
			'name' => 'Commerce ecosystem',
			'existing' => array('auto_parts', 'electronics', 'fashion', 'jewellery', 'medical'),
			'placeholders' => array(
				'industrial_equipment',
				'building_materials',
				'furniture_interiors',
				'fmcg_wholesale',
				'agricultural_products',
				'automotive_full_vehicles',
				'it_hardware_accessories',
			),
		),
		'business_services' => array(
			'code' => 'business_services',
			'name' => 'Business Services ecosystem',
			'existing' => array('tax_advisory', 'consultancy', 'erp_standalone'),
			'placeholders' => array(
				'legal_services',
				'accounting_auditing',
				'hr_recruitment',
				'marketing_digital',
				'it_services_saas_support',
				'business_licensing_services',
			),
		),
		'lifestyle_consumer' => array(
			'code' => 'lifestyle_consumer',
			'name' => 'Lifestyle & Consumer ecosystem',
			'existing' => array('health'),
			'placeholders' => array(
				'fitness_training',
				'beauty_skincare',
				'nutrition_supplements',
				'clinics_telemedicine',
				'wellness_subscriptions',
				'travel_experiences',
			),
		),
		'asset_sharing' => array(
			'code' => 'asset_sharing',
			'name' => 'Asset & Sharing ecosystem',
			'existing' => array('rental'),
			'placeholders' => array(
				'vehicle_leasing',
				'machinery_rental',
				'real_estate_leasing',
				'equipment_sharing',
				'storage_warehousing_rental',
				'fleet_management',
			),
		),
		'digital_technology' => array(
			'code' => 'digital_technology',
			'name' => 'Digital & Technology ecosystem',
			'existing' => array(),
			'placeholders' => array(
				'saas_tools',
				'erp_systems',
				'iot',
				'ai_automation',
				'cybersecurity',
				'ecommerce_infrastructure_tools',
			),
		),
	);
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
	return !in_array($host, epc_portal_platform_hostnames_static(), true);
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
	if ($platformProfile !== null && empty($GLOBALS['epc_demo_storefront_context'])) {
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
	} elseif (!empty($dbSettings['industry_code'])) {
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
	if (!empty($dbSettings['domain_path']) && !epc_portal_is_client_hostname($host)) {
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
	$template = isset($settings['theme_template']) ? (string) $settings['theme_template'] : '';
	if ($industry === 'auto_parts' && $template === 'classic') {
		return 'automotive_spareparts_pro';
	}
	if ($industry === 'electronics' && $template === 'midnight') {
		return 'electronics_retail_virgin';
	}
	if (in_array($industry, array('tax_advisory', 'consultancy'), true) && $template === 'modern') {
		return 'consulting_primeinvest';
	}
	if ($industry === 'fashion' && $template === 'signature') {
		return 'fashion_retail_namshi';
	}
	if ($industry === 'jewellery' && $template === 'classic') {
		return 'jewellery_retail_kiyasha';
	}
	return '';
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
