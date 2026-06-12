<?php
/**
 * Granular ERP module registry — Super CP toggles per tenant; shell + staff respect flags.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

/** @return array<string, array{id:string,label:string,desc:string,area:string,icon:string,default_erp_only:bool,default_full:bool}> */
function epc_portal_erp_modules_registry(): array
{
	return array(
		'erp_overview' => array(
			'id' => 'erp_overview',
			'label' => 'Overview',
			'desc' => 'Dashboard and cross-department workflow',
			'area' => 'overview',
			'icon' => 'fa-th-large',
			'default_erp_only' => true,
			'default_full' => true,
		),
		'erp_sales' => array(
			'id' => 'erp_sales',
			'label' => 'Sales',
			'desc' => 'CRM, proposals, orders, revenue, receivables, fulfilment, delivery, invoices',
			'area' => 'sales',
			'icon' => 'fa-line-chart',
			'default_erp_only' => true,
			'default_full' => true,
		),
		'erp_purchasing' => array(
			'id' => 'erp_purchasing',
			'label' => 'Purchasing',
			'desc' => 'Suppliers, RFQ, POs, payables, procurement link',
			'area' => 'purchasing',
			'icon' => 'fa-shopping-basket',
			'default_erp_only' => true,
			'default_full' => true,
		),
		'erp_finance' => array(
			'id' => 'erp_finance',
			'label' => 'Finance',
			'desc' => 'Treasury, GL, COA, VAT, e-invoicing, opening balances',
			'area' => 'finance',
			'icon' => 'fa-university',
			'default_erp_only' => true,
			'default_full' => true,
		),
		'erp_operations' => array(
			'id' => 'erp_operations',
			'label' => 'Operations',
			'desc' => 'Inventory, fixed assets, manufacturing',
			'area' => 'operations',
			'icon' => 'fa-cubes',
			'default_erp_only' => true,
			'default_full' => true,
		),
		'erp_custom_shipping' => array(
			'id' => 'erp_custom_shipping',
			'label' => 'Custom & Shipping',
			'desc' => 'UAE customs declarations and logistics documentation',
			'area' => 'custom_shipping',
			'icon' => 'fa-ship',
			'default_erp_only' => true,
			'default_full' => true,
		),
		'erp_people' => array(
			'id' => 'erp_people',
			'label' => 'People',
			'desc' => 'HR, payroll, staff profiles, expense reports',
			'area' => 'people',
			'icon' => 'fa-users',
			'default_erp_only' => true,
			'default_full' => true,
		),
		'erp_insights' => array(
			'id' => 'erp_insights',
			'label' => 'Insights',
			'desc' => 'Reports, marketing campaigns, knowledge base, multi-entity, audit',
			'area' => 'insights',
			'icon' => 'fa-bar-chart',
			'default_erp_only' => true,
			'default_full' => true,
		),
		'erp_collaboration' => array(
			'id' => 'erp_collaboration',
			'label' => 'Collaboration',
			'desc' => 'Agenda, contacts, documents',
			'area' => 'collaboration',
			'icon' => 'fa-calendar',
			'default_erp_only' => true,
			'default_full' => true,
		),
		'erp_enterprise' => array(
			'id' => 'erp_enterprise',
			'label' => 'Enterprise',
			'desc' => 'Business units, financial dimensions, budgeting and listings',
			'area' => 'enterprise',
			'icon' => 'fa-building-o',
			'default_erp_only' => true,
			'default_full' => true,
		),
	);
}

/** Preset id => list of enabled module ids. */
function epc_portal_erp_modules_presets(): array
{
	$full = array_keys(epc_portal_erp_modules_registry());
	$customsLogistics = array('erp_overview', 'erp_custom_shipping', 'erp_collaboration');
	$hrOnly = array('erp_overview', 'erp_people', 'erp_collaboration');
	return array(
		'full_erp' => array(
			'label' => 'Full ERP',
			'desc' => 'All ERP areas enabled',
			'modules' => $full,
		),
		'customs_logistics' => array(
			'label' => 'Customs + Logistics',
			'desc' => 'Overview + customs declarations & shipping docs',
			'modules' => $customsLogistics,
		),
		'custom_shipping_only' => array(
			'label' => 'Custom & Shipping only',
			'desc' => 'Alias for customs_logistics',
			'modules' => $customsLogistics,
		),
		'hr_only' => array(
			'label' => 'HR only',
			'desc' => 'Overview + HR / payroll / staff',
			'modules' => $hrOnly,
		),
		'people_only' => array(
			'label' => 'People only',
			'desc' => 'Alias for hr_only',
			'modules' => $hrOnly,
		),
		'finance_einvoice' => array(
			'label' => 'Finance + e-invoice',
			'desc' => 'Overview + finance + sales invoices',
			'modules' => array('erp_overview', 'erp_finance', 'erp_sales', 'erp_collaboration'),
		),
	);
}

/** Industry code => default ERP module preset id for ERP-only onboarding. */
function epc_portal_industry_erp_modules_preset_map(): array
{
	return array(
		'erp_standalone' => 'full_erp',
		'hr_recruitment' => 'hr_only',
		'logistics' => 'customs_logistics',
		'tax_advisory' => 'finance_einvoice',
		'consultancy' => 'full_erp',
	);
}

function epc_portal_industry_erp_modules_preset(string $industryCode): string
{
	$code = preg_replace('/[^a-z0-9_]/', '', strtolower($industryCode));
	$map = epc_portal_industry_erp_modules_preset_map();
	return isset($map[$code]) ? (string) $map[$code] : 'full_erp';
}

/** Resolve module ids for onboarding / live sync when intro omits explicit checkboxes. */
function epc_portal_erp_modules_resolve_for_onboard(array $intro, string $industryCode = '', string $accessMode = 'full'): array
{
	$mods = array();
	if (!empty($intro['erp_modules']) && is_array($intro['erp_modules'])) {
		$mods = epc_portal_erp_modules_normalize_list($intro['erp_modules']);
	}
	if (count($mods) === 0) {
		$mods = epc_portal_erp_modules_from_post($intro);
	}
	if (count($mods) > 0) {
		return $mods;
	}
	$presetId = '';
	if (!empty($intro['erp_modules_preset'])) {
		$presetId = preg_replace('/[^a-z0-9_]/', '', strtolower((string) $intro['erp_modules_preset']));
	}
	if ($presetId === '' && $industryCode !== '') {
		$presetId = epc_portal_industry_erp_modules_preset($industryCode);
	}
	if ($presetId !== '') {
		$presets = epc_portal_erp_modules_presets();
		if (isset($presets[$presetId]['modules'])) {
			return epc_portal_erp_modules_normalize_list($presets[$presetId]['modules']);
		}
	}
	return epc_portal_erp_modules_default_ids($accessMode);
}

/** Presets shown in operator UI (aliases hidden). */
function epc_portal_erp_modules_presets_ui(): array
{
	$all = epc_portal_erp_modules_presets();
	$ids = array('full_erp', 'hr_only', 'customs_logistics', 'finance_einvoice');
	$out = array();
	foreach ($ids as $id) {
		if (isset($all[$id])) {
			$out[$id] = $all[$id];
		}
	}
	return $out;
}

function epc_portal_erp_modules_detect_preset(array $enabledIds): string
{
	$enabled = epc_portal_erp_modules_normalize_list($enabledIds);
	sort($enabled);
	foreach (epc_portal_erp_modules_presets() as $pid => $preset) {
		$mods = epc_portal_erp_modules_normalize_list($preset['modules'] ?? array());
		sort($mods);
		if ($mods === $enabled) {
			return (string) $pid;
		}
	}
	return '';
}

function epc_portal_erp_modules_default_ids(string $accessMode = 'full'): array
{
	$registry = epc_portal_erp_modules_registry();
	$erpOnly = ($accessMode === 'erp_only');
	$out = array();
	foreach ($registry as $id => $meta) {
		$on = $erpOnly ? !empty($meta['default_erp_only']) : !empty($meta['default_full']);
		if ($on) {
			$out[] = $id;
		}
	}
	return $out;
}

function epc_portal_erp_modules_normalize_list($raw): array
{
	$registry = epc_portal_erp_modules_registry();
	$ids = array();
	if (is_string($raw) && $raw !== '') {
		$decoded = json_decode($raw, true);
		if (is_array($decoded)) {
			$raw = $decoded;
		} else {
			$raw = preg_split('/[\s,]+/', $raw, -1, PREG_SPLIT_NO_EMPTY);
		}
	}
	if (!is_array($raw)) {
		return array();
	}
	foreach ($raw as $item) {
		if (is_array($item) && isset($item['id'])) {
			$item = $item['id'];
		}
		$id = preg_replace('/[^a-z0-9_]/', '', strtolower((string) $item));
		if ($id !== '' && isset($registry[$id])) {
			$ids[$id] = true;
		}
	}
	return array_keys($ids);
}

/** Platform ERP operators see every module area regardless of ecomae registry flags. */
function epc_portal_erp_modules_full_access_context(): bool
{
	if (function_exists('epc_platform_erp_is_request') && epc_platform_erp_is_request()) {
		return true;
	}
	if (function_exists('epc_platform_erp_is_active') && epc_platform_erp_is_active()) {
		return true;
	}
	return false;
}

/** Resolve enabled ERP module ids for tenant (empty stored = all defaults for access mode). */
function epc_portal_erp_modules_enabled(array $settings = null): array
{
	if (epc_portal_erp_modules_full_access_context()) {
		return array_keys(epc_portal_erp_modules_registry());
	}
	if ($settings === null && function_exists('epc_portal_load_site_settings')) {
		$settings = epc_portal_load_site_settings();
	}
	if (!is_array($settings)) {
		$settings = array();
	}
	$stored = array();
	if (isset($settings['erp_modules']) && is_array($settings['erp_modules'])) {
		$stored = epc_portal_erp_modules_normalize_list($settings['erp_modules']);
	} elseif (!empty($settings['erp_modules_json'])) {
		$stored = epc_portal_erp_modules_normalize_list($settings['erp_modules_json']);
	}
	if (count($stored) > 0) {
		return $stored;
	}
	$mode = function_exists('epc_portal_resolve_access_mode')
		? epc_portal_resolve_access_mode($settings)
		: (string) ($settings['access_mode'] ?? 'full');
	return epc_portal_erp_modules_default_ids($mode);
}

function epc_portal_erp_modules_enabled_areas(array $settings = null): array
{
	$registry = epc_portal_erp_modules_registry();
	$enabled = epc_portal_erp_modules_enabled($settings);
	$areas = array();
	foreach ($enabled as $modId) {
		if (isset($registry[$modId]['area'])) {
			$areas[$registry[$modId]['area']] = true;
		}
	}
	// 'setup' (Accounting setup + Data import) is a core admin/config area every
	// tenant must be able to reach, so it is always enabled regardless of the
	// tenant's purchased module list. 'enterprise' (business units, financial
	// dimensions, budgeting, listings) is likewise foundational master data.
	$areas['setup'] = true;
	$areas['enterprise'] = true;
	// 'regrep' (External Reporting) is a foundational, country-driven statutory
	// reporting centre that every tenant must be able to reach for compliance.
	$areas['regrep'] = true;
	return array_keys($areas);
}

/** All ERP tab keys allowed by tenant module flags (before staff department filter). */
function epc_portal_erp_modules_allowed_tabs(array $settings = null): array
{
	$navFile = $_SERVER['DOCUMENT_ROOT'] . '/cp/content/shop/finance/erp/erp_nav_areas.php';
	if (!is_file($navFile)) {
		$navFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/erp_nav_areas.php';
	}
	if (!function_exists('epc_erp_nav_areas_config')) {
		if (is_file($navFile)) {
			require_once $navFile;
		}
	}
	if (!function_exists('epc_erp_nav_areas_config')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_staff.php';
		return epc_erp_staff_all_tabs();
	}
	$areasCfg = epc_erp_nav_areas_config();
	$enabledAreas = epc_portal_erp_modules_enabled_areas($settings);
	if (empty($enabledAreas)) {
		return epc_erp_staff_all_tabs();
	}
	$tabs = array();
	foreach ($enabledAreas as $areaKey) {
		if (!isset($areasCfg[$areaKey]['tabs'])) {
			continue;
		}
		foreach (array_keys($areasCfg[$areaKey]['tabs']) as $tabKey) {
			if ($tabKey === 'procurement_link') {
				continue;
			}
			$tabs[$tabKey] = true;
			if ($tabKey === 'cash_bank') {
				$tabs['bank_recon'] = true;
			}
		}
	}
	if (empty($tabs)) {
		return array('dashboard');
	}
	$tabList = array_values(array_keys($tabs));
	if (!function_exists('epc_erp_filter_commerce_tabs')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
	}
	return epc_erp_filter_commerce_tabs($tabList, $settings);
}

/** Intersect staff/user tab list with tenant-enabled ERP modules. */
function epc_erp_filter_tabs_by_tenant_modules(array $userTabs, array $settings = null): array
{
	$tenantTabs = epc_portal_erp_modules_allowed_tabs($settings);
	if (empty($tenantTabs)) {
		return $userTabs;
	}
	$filtered = array_values(array_intersect($userTabs, $tenantTabs));
	if (empty($filtered)) {
		return in_array('dashboard', $tenantTabs, true) ? array('dashboard') : array($tenantTabs[0]);
	}
	return $filtered;
}

function epc_portal_erp_modules_from_post(array $post): array
{
	if (!empty($post['erp_modules_preset'])) {
		$presets = epc_portal_erp_modules_presets();
		$presetId = preg_replace('/[^a-z0-9_]/', '', strtolower((string) $post['erp_modules_preset']));
		if ($presetId !== '' && isset($presets[$presetId]['modules'])) {
			return epc_portal_erp_modules_normalize_list($presets[$presetId]['modules']);
		}
	}
	if (isset($post['erp_modules']) && is_array($post['erp_modules'])) {
		$list = epc_portal_erp_modules_normalize_list($post['erp_modules']);
		if (count($list) > 0) {
			return $list;
		}
	}
	return array();
}

function epc_portal_erp_modules_area_enabled(string $areaKey, array $settings = null): bool
{
	return in_array($areaKey, epc_portal_erp_modules_enabled_areas($settings), true);
}
