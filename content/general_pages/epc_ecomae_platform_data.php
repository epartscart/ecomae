<?php
/**
 * ecomae.com platform marketing — copy, industry cards, rental tiers, demo package.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';

function epc_ecomae_h($v)
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

function epc_ecomae_platform_base_url()
{
	// Public marketing links must always stay on ecomae.com.
	return 'https://www.ecomae.com/';
}

function epc_ecomae_platform_super_cp_url()
{
	$dir = isset($GLOBALS['DP_Config']->backend_dir) ? $GLOBALS['DP_Config']->backend_dir : 'cp';
	return 'https://www.ecomae.com/' . $dir;
}

/** Platform company ERP (ecomae DB) — canonical entry is /erp/. */
function epc_ecomae_platform_company_erp_url()
{
	return 'https://www.ecomae.com/erp/';
}

/** Demo client ERP portal (ASAP tenant DB). */
function epc_ecomae_platform_erp_demo_url()
{
	if (function_exists('epc_client_erp_login_url')) {
		return epc_client_erp_login_url('asap');
	}
	return epc_ecomae_platform_super_cp_url() . '/client-erp/asap/';
}

function epc_ecomae_platform_super_erp_url()
{
	if (function_exists('epc_client_erp_shell_url')) {
		return epc_client_erp_shell_url('asap');
	}
	return epc_ecomae_platform_super_cp_url() . '/client-erp/asap/shop/finance/erp?epc_erp_shell=1';
}

function epc_ecomae_platform_onboard_url($industryCode = '')
{
	$url = epc_ecomae_platform_super_cp_url() . '/shop/tenant_hub/tenant_hub?tab=onboard';
	if ($industryCode !== '') {
		$url .= '&industry=' . rawurlencode($industryCode);
	}
	return $url;
}

function epc_ecomae_platform_industry_marketing()
{
	$portal = epc_portal_industries();
	$extra = array(
		'auto_parts' => array(
			'photo' => 'https://images.unsplash.com/photo-1486262715619-67b85e0b08d3?auto=format&fit=crop&w=640&q=75',
			'tagline' => 'Catalog, VIN search, supplier prices, logistics — the full autoparts stack your clients expect.',
			'highlights' => array('Epart catalog & vehicle fitment', 'Multi-supplier price search', 'Procurement & crosses', 'B2B balance & orders'),
			'demo_note' => 'Demo includes sample brands, catalog widgets, and a sandbox CP with prices module.',
		),
		'tax_advisory' => array(
			'photo' => 'https://images.unsplash.com/photo-1450101499163-c8848c66ca85?auto=format&fit=crop&w=640&q=75',
			'tagline' => 'Client portal, VAT workflows, documents and advisory CRM on one hosted platform.',
			'highlights' => array('UAE VAT & e-invoicing ready', 'Client approval workflows', 'Document control', 'ERP & finance packs'),
			'demo_note' => 'Demo shows advisory homepage, client hub, and finance modules with sample data.',
		),
		'fashion' => array(
			'photo' => 'https://images.unsplash.com/photo-1441984904996-e0b6df687efe?auto=format&fit=crop&w=640&q=75',
			'tagline' => 'Lookbook-style storefront, collections, and omnichannel orders without separate hosting.',
			'highlights' => array('Catalogue & variants', 'Marketing campaigns', 'Order fulfilment', 'Multi-currency ready'),
			'demo_note' => 'Demo storefront with collection grid and sample SKUs.',
		),
		'electronics' => array(
			'photo' => 'https://images.unsplash.com/photo-1498049794561-7780e7231661?auto=format&fit=crop&w=640&q=75',
			'tagline' => 'SKU-heavy catalogue, specs, and B2C/B2B checkout on a managed stack.',
			'highlights' => array('Bulk catalogue tools', 'Spec filters & search', 'Warranty & RMA flows', 'Payment integrations'),
			'demo_note' => 'Demo includes electronics-themed theme and sample product family.',
		),
		'medical' => array(
			'photo' => 'https://images.unsplash.com/photo-1576091160399-112ba8d25d1d?auto=format&fit=crop&w=640&q=75',
			'tagline' => 'Medical supplies ordering with compliance-friendly document trails.',
			'highlights' => array('Regulated catalogue', 'Customer accounts', 'Invoice & delivery docs', 'Professional services block'),
			'demo_note' => 'Demo uses medical industry CP pack and sample catalogue.',
		),
		'health' => array(
			'photo' => 'https://images.unsplash.com/photo-1571019614242-c5c5dee9f50b?auto=format&fit=crop&w=640&q=75',
			'tagline' => 'Wellness products, subscriptions, and client engagement in one portal.',
			'highlights' => array('Subscription-friendly catalogue', 'Client login area', 'Marketing automation hooks', 'Mobile-ready storefront'),
			'demo_note' => 'Demo highlights wellness branding and simple checkout.',
		),
		'consultancy' => array(
			'photo' => 'https://images.unsplash.com/photo-1552664730-d307ca884978?auto=format&fit=crop&w=640&q=75',
			'tagline' => 'Proposal-to-delivery: CRM, projects, billing, and client CP access.',
			'highlights' => array('CRM & customer hub', 'ERP & invoicing', 'Document library', 'Team CP roles'),
			'demo_note' => 'Demo shows consultancy homepage and client-facing CP slice.',
		),
		'rental' => array(
			'photo' => 'https://images.unsplash.com/photo-1449965408869-eaa3f722e40d?auto=format&fit=crop&w=640&q=75',
			'tagline' => 'Monthly rental businesses — fleet, equipment, or property — with booking-ready catalogue.',
			'highlights' => array('Rental SKU & periods', 'Deposits & contracts', 'Customer self-service', 'Monthly billing hooks'),
			'demo_note' => 'Ideal for monthly rental operators; demo includes rental-themed catalogue.',
		),
		'jewellery' => array(
			'photo' => 'https://images.unsplash.com/photo-1515562141207-7a88fb7ce338?auto=format&fit=crop&w=640&q=75',
			'tagline' => 'High-trust product presentation, certificates, and premium checkout experience.',
			'highlights' => array('Premium visual theme', 'Certificate & spec fields', 'Appointment / enquiry flows', 'Secure payments setup'),
			'demo_note' => 'Demo uses jewellery theme with gallery-style product cards.',
		),
	);

	$out = array();
	foreach ($portal as $code => $row) {
		if ($code === 'platform_host') {
			continue;
		}
		$meta = isset($extra[$code]) ? $extra[$code] : array(
			'photo' => 'https://images.unsplash.com/photo-1460925895917-afdab827c52f?auto=format&fit=crop&w=640&q=75',
			'tagline' => 'Hosted storefront and control panel for ' . strtolower($row['name']) . '.',
			'highlights' => array('Storefront + CP', 'Isolated database', 'SSL & DNS assist', 'Industry CP modules'),
			'demo_note' => '3-day demo with industry template pre-loaded.',
		);
		$out[$code] = array_merge($row, $meta);
	}
	return $out;
}

function epc_ecomae_platform_industry_marketing_grouped()
{
	$marketing = epc_ecomae_platform_industry_marketing();
	if (!function_exists('epc_portal_industries_grouped') || !function_exists('epc_portal_settings_industries')) {
		return array(
			array(
				'code' => 'all',
				'name' => 'Industry solutions',
				'industries' => $marketing,
				'placeholders' => array(),
			),
		);
	}
	$groups = epc_portal_industries_grouped(epc_portal_settings_industries());
	foreach ($groups as $ecoCode => &$group) {
		if (!is_array($group)) {
			continue;
		}
		if (!isset($group['name']) || $group['name'] === '') {
			$group['name'] = 'Industry solutions';
		}
		$rows = array();
		$inds = isset($group['industries']) && is_array($group['industries']) ? $group['industries'] : array();
		foreach ($inds as $indCode => $ind) {
			if (isset($marketing[$indCode])) {
				$rows[$indCode] = $marketing[$indCode];
			}
		}
		$group['industries'] = $rows;
	}
	unset($group);
	return $groups;
}

/**
 * UI-safe industry groups for /platform/industries and /platform/demo (never fatal).
 */
function epc_ecomae_platform_get_industry_groups()
{
	try {
		$groups = epc_ecomae_platform_industry_marketing_grouped();
	} catch (Throwable $e) {
		$groups = array();
	}
	if (!is_array($groups) || $groups === array()) {
		return array(
			array(
				'code' => 'all',
				'name' => 'Industry solutions',
				'industries' => epc_ecomae_platform_industry_marketing(),
				'placeholders' => array(),
			),
		);
	}
	$hasAny = false;
	foreach ($groups as $grp) {
		if (!empty($grp['industries']) && is_array($grp['industries'])) {
			$hasAny = true;
			break;
		}
	}
	if (!$hasAny) {
		return array(
			array(
				'code' => 'all',
				'name' => 'Industry solutions',
				'industries' => epc_ecomae_platform_industry_marketing(),
				'placeholders' => array(),
			),
		);
	}
	return $groups;
}

function epc_ecomae_platform_image($key)
{
	return epc_ecomae_platform_screenshot($key);
}

/**
 * Resolve a marketing screenshot by slug (e.g. commerce-storefront, guide-pricing).
 * Prefers PNG/WebP/JPG on disk; guide-* slugs also resolve SVG module previews.
 *
 * @param bool $allowLegacyFallback When false (capability modals), never return generic login mock-cp.
 */
function epc_ecomae_platform_screenshot($slug, $allowLegacyFallback = true)
{
	$slug = preg_replace('/[^a-z0-9_-]/', '', strtolower((string) $slug));
	if ($slug === '') {
		return '';
	}
	$base = '/content/files/images/ecomae-platform/';
	$root = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
	$dir = $root . str_replace('/', DIRECTORY_SEPARATOR, $base);
	$exts = array('png', 'webp', 'jpg', 'jpeg');
	if (strpos($slug, 'guide-') === 0) {
		$exts[] = 'svg';
	}
	// Tenant storefront captures: prefer flat /content/files/images/ (web-served) over ecomae-platform/.
	if (strpos($slug, 'tenant-') === 0) {
		$flatBase = '/content/files/images/';
		$flatDir = $root . str_replace('/', DIRECTORY_SEPARATOR, $flatBase);
		foreach ($exts as $ext) {
			$file = $flatDir . $slug . '.' . $ext;
			if ($file !== '' && is_file($file)) {
				return $flatBase . $slug . '.' . $ext;
			}
		}
	}
	foreach ($exts as $ext) {
		$file = $dir . $slug . '.' . $ext;
		if ($file !== '' && is_file($file)) {
			return $base . $slug . '.' . $ext;
		}
	}
	if (!$allowLegacyFallback) {
		return '';
	}
	$legacy = array(
		'storefront' => 'mock-storefront.svg',
		'cp' => 'mock-cp.svg',
		'super_cp' => 'mock-super-cp.svg',
		'super-cp' => 'mock-super-cp.svg',
	);
	if (isset($legacy[$slug])) {
		return $base . $legacy[$slug];
	}
	if (preg_match('/-cp$/', $slug)) {
		return $base . 'mock-cp.svg';
	}
	if (strpos($slug, 'super') !== false) {
		return $base . 'mock-super-cp.svg';
	}
	return $base . 'mock-storefront.svg';
}

/** Screenshot for capability modals — guide-* SVG only, no login PNG fallback. */
function epc_ecomae_platform_capability_screenshot($slug)
{
	return epc_ecomae_platform_screenshot($slug, false);
}

/** Storefront or CP screenshot for a platform product area id. */
function epc_ecomae_platform_area_shot($areaId, $side)
{
	$areaId = preg_replace('/[^a-z0-9_-]/', '', strtolower((string) $areaId));
	$side = ($side === 'cp') ? 'cp' : 'storefront';
	return epc_ecomae_platform_screenshot($areaId . '-' . $side);
}

/** Industry vertical previews on /platform/industry/{code}. */
function epc_ecomae_platform_industry_shot($industryCode, $side)
{
	$code = preg_replace('/[^a-z0-9_]/', '', strtolower((string) $industryCode));
	$side = ($side === 'cp') ? 'cp' : 'storefront';
	$slug = $code . '-' . $side;
	$resolved = epc_ecomae_platform_screenshot($slug);
	if (strpos($resolved, 'mock-') === false) {
		return $resolved;
	}
	$map = array(
		'tax_advisory' => 'tax-advisory',
		'auto_parts' => 'auto-parts',
	);
	if (isset($map[$code])) {
		$resolved = epc_ecomae_platform_screenshot($map[$code] . '-' . $side);
		if (strpos($resolved, 'mock-') === false) {
			return $resolved;
		}
	}
	return epc_ecomae_platform_screenshot($side === 'cp' ? 'industry-cp' : 'industry-storefront');
}

/**
 * Full platform capability areas — each with storefront + CP context for marketing.
 */
function epc_ecomae_platform_product_areas()
{
	$base = epc_ecomae_platform_base_url();
	$superUrl = epc_ecomae_platform_super_cp_url();
	$areaShot = function ($areaId, $side) {
		return epc_ecomae_platform_area_shot($areaId, $side);
	};

	return array(
		array(
			'id' => 'super-cp',
			'icon' => 'fa-cloud',
			'title' => 'Super CP (your operator console)',
			'summary' => 'Run the whole platform from www.ecomae.com/cp: onboard tenants, push industry templates, DNS checklists, deploy packages, and health probes — never exposed on client domains.',
			'storefront' => array(
				'label' => 'Not on client sites',
				'caption' => 'Super CP is operator-only at www.ecomae.com/cp. Clients never see tenant hub or deploy tools.',
				'image' => $areaShot('super-cp', 'storefront'),
				'features' => array('Tenant hub & intro forms', 'Register hostname + MySQL', 'Industry & style templates', 'Push settings to client DB', 'Deploy targets & one-click zip'),
			),
			'cp' => array(
				'label' => 'Super CP screens',
				'caption' => 'Same ECOM AE control panel shell with platform packs: tenant hub, industry settings, module toggles, sidebar blocks.',
				'image' => $areaShot('super-cp', 'cp'),
				'features' => array('Tenant list & status (draft / DNS / live)', 'Per-tenant industry + visual style', 'Module pack enable/disable', 'Hide CP sidebar groups per client', 'Operator admin & audit'),
			),
			'packs' => array('super_platform', 'core', 'professional', 'marketing', 'erp'),
			'live' => array('label' => 'Open Super CP', 'url' => $superUrl . '/shop/tenant_hub/tenant_hub?tab=onboard'),
		),
		array(
			'id' => 'client-cp-storefront',
			'icon' => 'fa-globe',
			'title' => 'Client domain — storefront + Client CP',
			'summary' => 'Each tenant gets www.client.com for shoppers and www.client.com/cp for staff. One codebase, isolated database, industry theme and module packs — DNS points to ecomae, no separate hosting.',
			'storefront' => array(
				'label' => 'Public storefront',
				'caption' => 'Branded shop or service site: catalogue, cart, enquiries, client login, multi-language.',
				'image' => $areaShot('client-cp-storefront', 'storefront'),
				'features' => array('Industry homepage & hero', 'Product / service catalogue', 'Cart & checkout', 'Customer login area', 'Contact, WhatsApp, SEO pages'),
			),
			'cp' => array(
				'label' => 'Client control panel',
				'caption' => 'Day-to-day operations: orders, stock, prices, finance, documents — only modules you enable for that industry.',
				'image' => $areaShot('client-cp-storefront', 'cp'),
				'features' => array('Role-based staff access', 'Industry-themed sidebar', 'Site & module settings', 'Orders & customers', 'Print documents & emails'),
			),
			'packs' => array('core', 'commerce', 'catalogue'),
			'live' => array('label' => 'Example tenant', 'url' => 'https://www.taxofinca.com/'),
			'live_cp' => array('label' => 'Example CP', 'url' => 'https://www.taxofinca.com/cp/'),
		),
		array(
			'id' => 'commerce',
			'icon' => 'fa-shopping-cart',
			'title' => 'Commerce & orders',
			'summary' => 'Omnichannel selling: web orders, payment gateways, cart rules, customer accounts, and fulfilment hooks for UAE & GCC retailers.',
			'storefront' => array(
				'label' => 'Storefront',
				'caption' => 'Product grids, filters, promotions, guest and logged-in checkout.',
				'image' => $areaShot('commerce', 'storefront'),
				'features' => array('Category navigation', 'Promo codes', 'Payment at checkout', 'Order history for clients', 'Multi-currency display'),
			),
			'cp' => array(
				'label' => 'Control panel',
				'caption' => 'Order desk, payments reconciliation, channel settings.',
				'image' => $areaShot('commerce', 'cp'),
				'features' => array('Order list & statuses', 'Payment gateway setup', 'Cart & delivery rules', 'Customer groups', 'Sales reports'),
			),
			'packs' => array('commerce'),
			'live' => array('label' => 'Storefront demo', 'url' => 'https://www.ecomae.com/shop'),
		),
		array(
			'id' => 'catalogue',
			'icon' => 'fa-th-large',
			'title' => 'Catalogue & products',
			'summary' => 'SKU management, variants, bulk CSV/XML import, families, and rich product pages — for fashion, electronics, medical, and general retail.',
			'storefront' => array(
				'label' => 'Storefront',
				'caption' => 'Fast catalogue browse, search, and product detail with specs and media.',
				'image' => $areaShot('catalogue', 'storefront'),
				'features' => array('Search & filters', 'Variant selectors (size, colour)', 'Related products', 'Bulk category trees', 'Mobile catalogue'),
			),
			'cp' => array(
				'label' => 'Control panel',
				'caption' => 'Catalogue editors, bulk upload, and merchandising.',
				'image' => $areaShot('catalogue', 'cp'),
				'features' => array('Product editor', 'Bulk CSV import/export', 'Categories & attributes', 'Image manager', 'Price lists'),
			),
			'packs' => array('catalogue', 'commerce'),
		),
		array(
			'id' => 'auto-parts',
			'icon' => 'fa-car',
			'title' => 'Auto spare parts',
			'summary' => 'Purpose-built for parts distributors: VIN/OEM search, supplier price files, crosses, procurement, and B2B trade balances (eParts-style stack).',
			'storefront' => array(
				'label' => 'Storefront',
				'caption' => 'Vehicle search widgets, brand trees, live price display, B2B login.',
				'image' => $areaShot('auto-parts', 'storefront'),
				'features' => array('VIN / OEM lookup', 'Brand & model catalogue', 'Live supplier prices', 'B2B fixed currency', 'Re-order from history'),
			),
			'cp' => array(
				'label' => 'Control panel',
				'caption' => 'Prices upload, crosses, procurement, logistics tied to parts.',
				'image' => $areaShot('auto-parts', 'cp'),
				'features' => array('Prices & crosses manager', 'Supplier procurement', 'Warehouse / logistics', 'Customer balance', 'Parts agent / AI chats'),
			),
			'packs' => array('auto_parts', 'commerce', 'logistics'),
			'live' => array('label' => 'Auto parts live site', 'url' => 'https://www.ecomae.com/'),
			'industry' => 'auto_parts',
		),
		array(
			'id' => 'logistics',
			'icon' => 'fa-truck',
			'title' => 'Logistics & warehouses',
			'summary' => 'Warehouses, storages, offices, pick/pack, and delivery documentation shared across industries.',
			'storefront' => array(
				'label' => 'Storefront',
				'caption' => 'Delivery options, branch pickup, and stock availability messaging.',
				'image' => $areaShot('logistics', 'storefront'),
				'features' => array('Delivery method choice', 'Branch / pickup points', 'Order tracking page', 'Lead times on SKU', 'Service area info'),
			),
			'cp' => array(
				'label' => 'Control panel',
				'caption' => 'Warehouse master data and fulfilment documents.',
				'image' => $areaShot('logistics', 'cp'),
				'features' => array('Warehouses & storages', 'Stock movements', 'Delivery notes', 'Office / branch list', 'Logistics reports'),
			),
			'packs' => array('logistics'),
		),
		array(
			'id' => 'worldwide-tax-toolkit',
			'icon' => 'fa-globe',
			'title' => 'Worldwide Tax Toolkit',
			'summary' => 'Complete business tax for 195+ countries — VAT/GST, corporate tax, import/export, double taxation, FTC, and ERP purchase/sales/profit hooks. One-click Update in Super CP.',
			'storefront' => array(
				'label' => 'Storefront',
				'caption' => 'Checkout and POS apply tenant indirect tax; corporate tax surfaces in ERP reports.',
				'image' => $areaShot('erp-finance', 'storefront'),
				'features' => array('Tenant jurisdiction tax', 'VAT/GST at checkout', 'Export zero-rating', 'POS indirect tax only', 'UAE e-invoice compatible'),
			),
			'cp' => array(
				'label' => 'Control panel',
				'caption' => 'Super CP Tax Toolkit: multi-section kit view, Update tax data, migrate tenants.',
				'image' => $areaShot('erp-finance', 'cp'),
				'features' => array('Indirect + direct + trade tabs', 'Update tax data (FTA for UAE)', '243 jurisdiction kits', 'ERP purchase/sales hooks', 'CIT estimate helper'),
			),
			'packs' => array('erp', 'professional', 'tax_advisory'),
			'live' => array('label' => 'Tax Toolkit (Super CP)', 'url' => epc_ecomae_platform_super_cp_url() . '/control/portal/epc_tax_toolkit_manage'),
		),
		array(
			'id' => 'erp-finance',
			'icon' => 'fa-university',
			'title' => 'ERP, VAT & UAE e-invoice',
			'summary' => 'General ledger, VAT returns, Peppol-ready e-invoice (PINT-AE), tax invoices from orders, payroll hooks, and audit-friendly documents — built for UAE Federal Tax Authority requirements. Powered by the Worldwide Tax Toolkit for multi-country customers.',
			'storefront' => array(
				'label' => 'Storefront',
				'caption' => 'Tax-inclusive pricing, TRN on invoices visible to clients where configured.',
				'image' => $areaShot('erp-finance', 'storefront'),
				'features' => array('VAT-inclusive prices', 'Download invoices (logged-in)', 'Quote / proforma request', 'Professional trust pages', 'Payment receipts'),
			),
			'cp' => array(
				'label' => 'Control panel',
				'caption' => 'Finance modules: GL, VAT, e-invoice, document control.',
				'image' => $areaShot('erp-finance', 'cp'),
				'features' => array('Chart of accounts', 'UAE e-invoice (Peppol / PINT-AE)', 'Invoices tab + XML/JSON export', 'Tax invoices & credit notes', 'Payroll integration hooks'),
			),
			'packs' => array('erp', 'professional'),
			'industry' => 'tax_advisory',
			'live' => array('label' => 'Standalone ERP portal', 'url' => epc_ecomae_platform_erp_demo_url()),
		),
		array(
			'id' => 'erp-inventory',
			'icon' => 'fa-cubes',
			'title' => 'ERP inventory (multi-warehouse)',
			'summary' => 'Client-owned stock across warehouses: CSV-style uploads via movements, purchase receipts, sales issues, weighted average costing, perishable expiry/batch, custom variant fields, and month-end closing snapshots.',
			'storefront' => array(
				'label' => 'Storefront',
				'caption' => 'Stock availability per warehouse feeds catalogue and checkout when linked to shop products.',
				'image' => $areaShot('erp-inventory', 'storefront'),
				'features' => array('Sell only what is in stock', 'Multi-location delivery', 'Expiry-aware picking (perishables)', 'B2B re-order from history', 'Linked SKU to catalogue'),
			),
			'cp' => array(
				'label' => 'Control panel',
				'caption' => 'ERP → Inventory: warehouses, SKUs, movements, valuation, closing.',
				'image' => $areaShot('erp-inventory', 'cp'),
				'features' => array('Sync from shop storages', 'Weighted average cost', 'Purchase in / sale out', '5 custom fields per SKU', 'Period closing & valuation KPI'),
			),
			'packs' => array('erp', 'logistics', 'commerce'),
			'live' => array('label' => 'Inventory tab', 'url' => epc_ecomae_platform_super_cp_url() . '/shop/finance/erp?tab=inventory'),
		),
		array(
			'id' => 'erp-fixed-assets',
			'icon' => 'fa-building',
			'title' => 'ERP fixed assets & depreciation',
			'summary' => 'Asset register with cost, accumulated depreciation, net book value, multiple depreciation methods, monthly runs, and location/tracking IDs for audits.',
			'storefront' => array(
				'label' => 'Storefront',
				'caption' => 'Generally internal; clients may see asset-backed service pages only.',
				'image' => $areaShot('erp-fixed-assets', 'storefront'),
				'features' => array('Not customer-facing', 'Indirect: fleet / equipment services', 'Trust & compliance pages', '—', '—'),
			),
			'cp' => array(
				'label' => 'Control panel',
				'caption' => 'ERP → Fixed assets: register, depreciate, track.',
				'image' => $areaShot('erp-fixed-assets', 'cp'),
				'features' => array('Straight line & declining methods', 'Opening accumulated dep. on migration', 'Monthly depreciation run', 'Book value report', 'Tracking log by location'),
			),
			'packs' => array('erp', 'professional'),
			'live' => array('label' => 'Fixed assets tab', 'url' => epc_ecomae_platform_super_cp_url() . '/shop/finance/erp?tab=fixed_assets'),
		),
		array(
			'id' => 'tax-advisory',
			'icon' => 'fa-balance-scale',
			'title' => 'Tax & advisory vertical',
			'summary' => 'Client portals for tax firms: approvals, document library, advisory CRM, and VAT workflows on the client domain.',
			'storefront' => array(
				'label' => 'Client portal (storefront)',
				'caption' => 'Professional site + secure client hub — not a product catalogue.',
				'image' => $areaShot('tax-advisory', 'storefront'),
				'features' => array('Advisory homepage', 'Secure client login', 'Document downloads', 'Enquiry & appointment forms', 'Teal / trust branding'),
			),
			'cp' => array(
				'label' => 'Advisory CP',
				'caption' => 'Workflows, VAT, client assignments, document templates.',
				'image' => $areaShot('tax-advisory', 'cp'),
				'features' => array('Client approval flows', 'Document control', 'CRM & assignments', 'VAT return data', 'Role-based advisors'),
			),
			'packs' => array('tax_advisory', 'erp', 'professional'),
			'live' => array('label' => 'Taxofinca example', 'url' => 'https://www.taxofinca.com/'),
			'industry' => 'tax_advisory',
		),
		array(
			'id' => 'professional-crm',
			'icon' => 'fa-briefcase',
			'title' => 'Professional services & CRM',
			'summary' => 'Consultancies and B2B services: native CRM pipeline (leads, opportunities, activities), approvals, demand intelligence, and client-facing hubs.',
			'storefront' => array(
				'label' => 'Storefront',
				'caption' => 'Services marketing site with case studies and client login.',
				'image' => $areaShot('professional-crm', 'storefront'),
				'features' => array('Service pages', 'Case studies', 'Proposal requests', 'Client portal login', 'Team credentials'),
			),
			'cp' => array(
				'label' => 'Control panel',
				'caption' => 'Native epc_crm_* tables: kanban pipeline, lead conversion, activities — plus ERP billing.',
				'image' => $areaShot('professional-crm', 'cp'),
				'features' => array('Leads & kanban pipeline', 'Opportunities & activities', 'Won → order hint', 'Customer hub', 'ERP finance integration'),
			),
			'packs' => array('professional', 'crm', 'commerce'),
			'live' => array('label' => 'CRM in Super CP', 'url' => epc_ecomae_platform_super_cp_url() . '/shop/crm/crm'),
			'live_cp' => array('label' => 'Example client CP', 'url' => 'https://www.taxofinca.com/cp/'),
		),
		array(
			'id' => 'marketing',
			'icon' => 'fa-bullhorn',
			'title' => 'Marketing & campaigns',
			'summary' => 'Campaigns, analytics hooks, and merchandising tools to drive traffic to the hosted storefront.',
			'storefront' => array(
				'label' => 'Storefront',
				'caption' => 'Landing pages, banners, and campaign-driven catalogue highlights.',
				'image' => $areaShot('marketing', 'storefront'),
				'features' => array('Campaign landing URLs', 'Hero banners', 'Featured collections', 'Newsletter capture', 'UTM-ready structure'),
			),
			'cp' => array(
				'label' => 'Control panel',
				'caption' => 'Campaign setup and performance views.',
				'image' => $areaShot('marketing', 'cp'),
				'features' => array('Campaign manager', 'Promo linkage', 'Basic analytics', 'Email template hooks', 'Channel links'),
			),
			'packs' => array('marketing'),
		),
		array(
			'id' => 'onboarding',
			'icon' => 'fa-plug',
			'title' => 'DNS onboarding & multi-tenant',
			'summary' => 'Model C hosting: client keeps GoDaddy domain, adds A record to platform IP, we attach nginx alias and isolated MySQL — go live in hours, not weeks.',
			'storefront' => array(
				'label' => 'After DNS is live',
				'caption' => 'www.client.com serves the industry storefront immediately after propagation.',
				'image' => $areaShot('onboarding', 'storefront'),
				'features' => array('SSL via Let\'s Encrypt', 'Same codebase as platform', 'Per-host branding', 'No separate cPanel for client', 'Cloudflare-friendly'),
			),
			'cp' => array(
				'label' => 'Operator workflow',
				'caption' => 'Super CP tracks draft → DNS pending → live.',
				'image' => $areaShot('onboarding', 'cp'),
				'features' => array('GoDaddy DNS checklist', 'Tenant registry in ecomae DB', 'docpart DB per client', 'Industry template on save', '3-day demo then rental'),
			),
			'packs' => array('super_platform'),
			'live' => array('label' => 'Onboard a client', 'url' => epc_ecomae_platform_onboard_url()),
		),
	);
}

function epc_ecomae_platform_client_deliverables()
{
	return array(
		array(
			'icon' => 'fa-globe',
			'title' => 'Their domain only',
			'text' => 'Client keeps GoDaddy (or any registrar) domain. One A record to ecomae — no separate hosting bill.',
			'photo' => 'https://images.unsplash.com/photo-1558494949-ef010cbdcc31?auto=format&fit=crop&w=640&q=75',
		),
		array(
			'icon' => 'fa-shopping-cart',
			'title' => 'Storefront + CP',
			'text' => 'Public website and private control panel at www.client.com and www.client.com/cp on the same codebase.',
			'photo' => 'https://images.unsplash.com/photo-1551288049-bebda4e38f71?auto=format&fit=crop&w=640&q=75',
		),
		array(
			'icon' => 'fa-database',
			'title' => 'Dedicated MySQL',
			'text' => 'Each tenant gets an isolated database — exportable, backup-friendly, under your custody.',
			'photo' => 'https://images.unsplash.com/photo-1544197150-b99a580bb7a8?auto=format&fit=crop&w=640&q=75',
		),
		array(
			'icon' => 'fa-lock',
			'title' => 'SSL & updates',
			'text' => 'Let\'s Encrypt, security patches, and platform upgrades handled on ecomae infrastructure.',
			'photo' => 'https://images.unsplash.com/photo-1563986768609-322da13575f3?auto=format&fit=crop&w=640&q=75',
		),
		array(
			'icon' => 'fa-globe',
			'title' => 'Worldwide Tax Toolkit',
			'text' => '195+ country jurisdiction kits — VAT, GST, sales tax, import/export rules, and per-customer profiles wired into ERP.',
			'photo' => 'https://images.unsplash.com/photo-1450101499163-c8848c66ca85?auto=format&fit=crop&w=640&q=75',
		),
		array(
			'icon' => 'fa-university',
			'title' => 'ERP · VAT · FTA',
			'text' => 'UAE-ready finance, e-invoicing, document control — optional packs per industry.',
			'photo' => 'https://images.unsplash.com/photo-1554224155-6726b3ff858f?auto=format&fit=crop&w=640&q=75',
		),
		array(
			'icon' => 'fa-th-large',
			'title' => 'Super CP for you',
			'text' => 'Onboard tenants, DNS checklist, and health probes from www.ecomae.com/cp — not on the client site.',
			'photo' => 'https://images.unsplash.com/photo-1551434678-e076c223a692?auto=format&fit=crop&w=640&q=75',
		),
	);
}

function epc_ecomae_platform_rental_plans()
{
	return array(
		array(
			'code' => 'launch',
			'name' => 'Launch',
			'price_aed' => 399,
			'period' => 'month',
			'featured' => false,
			'tagline' => 'Go live on your own domain',
			'items' => array('Hosted storefront + client CP', 'ERP-lite (orders, catalogue, invoicing)', 'Dedicated MySQL database', 'SSL & DNS onboarding', 'Unlimited products & users', 'Email support'),
		),
		array(
			'code' => 'growth',
			'name' => 'Growth',
			'price_aed' => 999,
			'period' => 'month',
			'featured' => true,
			'tagline' => 'Full ERP + commerce, country-driven',
			'items' => array('Everything in Launch', 'Full ERP & finance suite', 'Country-driven VAT / e-invoicing', 'Multi-warehouse + multi-vendor', 'POS + CRM', 'Priority onboarding', 'Unlimited users — no per-seat fees'),
		),
		array(
			'code' => 'scale',
			'name' => 'Scale',
			'price_aed' => 2499,
			'period' => 'month',
			'featured' => false,
			'tagline' => 'Multichannel, automation & API',
			'items' => array('Everything in Growth', 'Multichannel OMS (web/POS/API/marketplace)', 'Marketing automation + AI tools', 'Open API & integrations hub', 'Advanced analytics & multi-branch', 'Dedicated success manager'),
		),
		array(
			'code' => 'enterprise',
			'name' => 'Enterprise / Blockchain BOS Operator',
			'price_aed' => null,
			'period' => 'month',
			'featured' => false,
			'tagline' => 'Run your own tenant fleet',
			'items' => array('Blockchain BOS operator console — control a fleet of tenants', 'White-label & custom industry templates', 'Multi-tenant SLA + governance + audit', 'SSO / operator MFA', 'Migration assistance', 'Dedicated operator support'),
		),
	);
}

/**
 * Indicative market benchmark vs major ERP/commerce platforms. Figures are
 * public-list estimates (converted to AED, approx) and vary by region, edition,
 * user count and contract; shown to position ECOM AE's all-inclusive value.
 *
 * @return array{note:string,rows:array<int,array{name:string,price:string,model:string,setup:string,highlight:bool}>}
 */
function epc_ecomae_platform_price_benchmark()
{
	return array(
		'note' => 'Indicative public-list estimates in AED, normalised to a small/mid team. Actual ERP pricing is per-user and varies widely by region, edition and contract. ECOM AE is all-inclusive (hosting + storefront + CP + ERP + database) with unlimited users and no implementation fee.',
		'rows' => array(
			array('name' => 'Oracle NetSuite', 'price' => 'from ~AED 3,500 / user / mo', 'model' => 'Per user + base license', 'setup' => '~AED 75,000–180,000 implementation', 'highlight' => false),
			array('name' => 'Oracle Fusion Cloud ERP', 'price' => 'from ~AED 640 / user / mo', 'model' => 'Per user, enterprise minimums', 'setup' => 'Large SI implementation', 'highlight' => false),
			array('name' => 'SAP S/4HANA Cloud', 'price' => 'from ~AED 550 / user / mo', 'model' => 'Per user, enterprise minimums', 'setup' => 'Large SI implementation', 'highlight' => false),
			array('name' => 'SAP Business One', 'price' => 'from ~AED 340 / user / mo', 'model' => 'Per user + maintenance', 'setup' => '~AED 30,000–90,000 + license', 'highlight' => false),
			array('name' => 'Microsoft Dynamics 365 BC', 'price' => 'from ~AED 260 / user / mo', 'model' => 'Per user (Essentials/Premium)', 'setup' => 'Partner implementation', 'highlight' => false),
			array('name' => 'ECOM AE — all-inclusive', 'price' => 'from AED 399 / mo (flat)', 'model' => 'Per tenant — unlimited users', 'setup' => 'No implementation fee · 3-day free demo', 'highlight' => true),
		),
	);
}

function epc_ecomae_platform_demo_package()
{
	return array(
		'days' => 3,
		'title' => '3-day industry demo',
		'steps' => array(
			'Pick an industry template (auto parts, tax, fashion, rental, etc.).',
			'We register a demo tenant in Super CP — draft status until DNS is ready.',
			'Optional: point a subdomain or staging domain for the demo window.',
			'Full storefront + CP access for 3 days with sample catalogue and modules.',
			'After the demo: convert to monthly rental or we remove the demo tenant — no leftover clutter.',
		),
		'includes' => array(
			'Industry-themed homepage & CP sidebar',
			'Sandbox database (not production data)',
			'Operator walkthrough via Super CP',
			'DNS / GoDaddy checklist included',
		),
	);
}

function epc_ecomae_platform_ecosystem_model()
{
	return array(
		'governance' => 'Industries are added only when they increase transaction volume, platform dependency, or repeat usage.',
		'positioning' => 'ECOM AE is a structured multi-ecosystem platform, not a random listing site.',
		'scale' => array(
			'now' => 'Now: active operations running across commerce, advisory, and tenantized CP/ERP delivery.',
			'six_months' => 'In 6 months: expand depth in each ecosystem with adjacent high-frequency workflows.',
			'one_to_two_years' => 'In 1-2 years: a connected ecosystem portfolio with shared data rails and repeat B2B/B2C cycles.',
		),
		'ecosystems' => array(
			array(
				'name' => 'Commerce Ecosystem',
				'active' => array('Auto parts commerce', 'Fashion e-commerce', 'Electronics retail', 'Medical supplies ordering'),
				'roadmap' => array('Hyperlocal grocery & FMCG', 'B2B wholesale marketplaces', 'Cross-border niche commerce'),
			),
			array(
				'name' => 'Business Services Ecosystem',
				'active' => array('Tax advisory portals', 'Consultancy CRM + billing', 'ERP finance workflows'),
				'roadmap' => array('Legal/pro services operations', 'Corporate services onboarding', 'Managed back-office services'),
			),
			array(
				'name' => 'Lifestyle & Consumer Ecosystem',
				'active' => array('Health & wellness commerce', 'Jewellery premium storefronts', 'Consumer engagement campaigns'),
				'roadmap' => array('Beauty & personal care subscriptions', 'Home & living curation', 'Experience-led consumer services'),
			),
			array(
				'name' => 'Asset & Sharing Economy',
				'active' => array('Rental operations workflows', 'Multi-asset inventory controls', 'Deposit and contract tracking'),
				'roadmap' => array('Fleet sharing platforms', 'Equipment sharing networks', 'Property/service asset scheduling'),
			),
			array(
				'name' => 'Digital & Technology Ecosystem',
				'active' => array('Super CP tenant orchestration', 'Client CP operations layer', 'Integrated ERP + CRM stack'),
				'roadmap' => array('API-led partner integrations', 'Intelligence automation services', 'Data products for operator decisions'),
			),
		),
	);
}

/**
 * Customer-facing copy for cloud primary + backup continuity (operators use Super CP guide).
 */
function epc_ecomae_platform_business_continuity()
{
	return array(
		'headline' => 'Cloud + backup continuity',
		'subhead' => 'Primary cloud always on — with a professional backup path when the cloud hiccups.',
		'lead' => 'Tenants stay on ECOM AE in the cloud day to day. If the primary cloud is unreachable, traffic can move to your backup origin with a guided splash — then sync back when the cloud is healthy. Peace of mind without abandoning cloud-only operations.',
		'pillars' => array(
			array('icon' => 'fa-cloud', 'title' => 'Primary cloud always on', 'text' => 'Storefront, /cp/, and /erp run on the hosted ECOM AE cloud under normal operations.'),
			array('icon' => 'fa-heartbeat', 'title' => 'Automatic issue detection', 'text' => 'Health checks detect when the primary cloud is slow or unreachable — operators can switch modes without rebuilding the tenant.'),
			array('icon' => 'fa-shield', 'title' => 'Backup site with guided splash', 'text' => 'Visitors see a professional step-by-step splash while sessions route to your backup origin (on-prem or secondary host).'),
			array('icon' => 'fa-refresh', 'title' => 'Sync when cloud returns', 'text' => 'After recovery, sessions and cache hand back to the primary cloud with a clear failback story for staff.'),
			array('icon' => 'fa-handshake-o', 'title' => 'Peace of mind for tenants', 'text' => 'Ideal for operators who want cloud scale but fear “cloud-only” risk — continuity is part of the platform story.'),
		),
		'flow_steps' => array(
			array('key' => 'shop', 'label' => 'Customer', 'detail' => 'Browses your branded storefront'),
			array('key' => 'cloud', 'label' => 'Primary cloud', 'detail' => 'ECOM AE hosted stack (default)'),
			array('key' => 'detect', 'label' => 'Detect', 'detail' => 'Health probe flags cloud issue'),
			array('key' => 'backup', 'label' => 'Backup origin', 'detail' => 'On-prem / secondary site serves traffic'),
			array('key' => 'sync', 'label' => 'Sync back', 'detail' => 'Failback when cloud is healthy'),
		),
		'splash_modes' => array(
			array('mode' => 'primary_ok', 'title' => 'All systems normal', 'detail' => 'Shoppers use the primary cloud — no splash.'),
			array('mode' => 'primary_down', 'title' => 'Connecting to backup', 'detail' => 'Professional steps while routing to backup.'),
			array('mode' => 'backup_active', 'title' => 'Backup online', 'detail' => 'Commerce continues on backup origin.'),
			array('mode' => 'failback_sync', 'title' => 'Restoring cloud', 'detail' => 'Syncing sessions back to primary.'),
		),
	);
}

/**
 * Customer-facing Super CP capability areas — maps operator guides to public marketing copy.
 * Internal runbooks (tokens, CLP passwords, curl examples) stay in Super CP only.
 */
function epc_ecomae_platform_super_cp_guide_areas()
{
	$base = epc_ecomae_platform_base_url();
	$themes = epc_ecomae_platform_storefront_theme_showcase();

	return array(
		array(
			'id' => 'overview',
			'icon' => 'fa-cloud',
			'title' => 'Platform & Super CP overview',
			'tagline' => 'One operator console for every tenant — never exposed on client domains.',
			'summary' => 'Super CP at www.ecomae.com/cp is your private platform layer: same ECOM AE control panel shell as client sites, plus tenant orchestration packs. Clients only see their branded storefront and /cp/ on their own domain.',
			'benefits' => array(
				'Onboard unlimited tenants from one hosted stack',
				'Push industry templates and module packs per client',
				'Isolated MySQL per tenant — data stays separate',
				'Platform operators never share Super CP access with clients',
			),
			'capabilities' => array(
				array('label' => 'Super CP vs Client CP', 'text' => 'Super CP runs on ecomae.com with the platform database. Client CP runs on www.client.com/cp with that tenant\'s database — same UI, different hostname routing.'),
				array('label' => 'Tenant hub', 'text' => 'Register clients, track draft → DNS pending → live status, launch checklists, and health probes from one screen.'),
				array('label' => 'Industry settings', 'text' => 'Pick vertical, visual style, enabled module packs, and sidebar visibility — then push to a client site when ready.'),
				array('label' => 'ERP & CRM on platform', 'text' => 'Demo finance, inventory, CRM pipeline, and UAE e-invoice modules on the platform host before enabling them for tenants.'),
			),
			'cta' => array('label' => 'Full platform tour', 'href' => $base . 'platform'),
			'super_cp' => 'Tenant hub · Industry settings · super_platform pack · ERP/CRM demos',
		),
		array(
			'id' => 'onboarding',
			'icon' => 'fa-rocket',
			'title' => 'Tenant onboarding & industries',
			'tagline' => 'Intro form → DNS → live storefront + CP in as little as 24 hours.',
			'summary' => 'The onboard workflow captures brand, domain, industry, contacts, and tenant database details in one step. Industry templates pre-load CP module packs, storefront package, and visual style so the client site is ready when DNS propagates.',
			'benefits' => array(
				'Single intro form seeds portal settings immediately',
				'Nine industry verticals grouped in five ecosystems',
				'GoDaddy-friendly DNS checklist — client keeps their registrar',
				'3-day demo sandbox before monthly rental',
			),
			'capabilities' => array(
				array('label' => 'Onboard client form', 'text' => 'Trade name, hostname, industry, admin email, and tenant DB — registers the client and applies the industry template on save.'),
				array('label' => 'ERP-only deployment', 'text' => 'Shared ERP on www.ecomae.com/cp/ — no client domain or DNS. access_mode=erp_only, one MySQL DB per company, login email routes to the correct tenant ERP shell.'),
				array('label' => 'Industry verticals', 'text' => 'Auto parts, tax advisory, fashion, electronics, medical, health, consultancy, rental, and jewellery — each with dedicated marketing page and CP packs.'),
				array('label' => 'DNS & launch checklist', 'text' => 'Step-by-step A-record guide, domain alias on the platform stack, SSL issuance, and status flip to Live when probes pass.'),
				array('label' => 'Health probes', 'text' => 'HTTP checks on live tenants so you know storefront and CP endpoints respond before handoff.'),
			),
			'cta' => array('label' => 'Browse industries', 'href' => $base . 'platform/industries'),
			'super_cp' => 'Tenant hub → Onboard · DNS tab · Guide · Tenants list · Launch checklist',
		),
		array(
			'id' => 'storefront-themes',
			'icon' => 'fa-paint-brush',
			'title' => 'Storefront theme packages',
			'tagline' => 'Five production-ready retail and services layouts — not generic templates.',
			'summary' => 'Each package ships full homepage, header, footer, and industry-tuned CP sidebar. Pick the layout that matches your client\'s sector; colour styles can be switched without losing catalogue or order data.',
			'benefits' => array(
				'Named retail parity themes (automotive, Virgin-style electronics, Namshi fashion, Kiyasha jewellery, Prime Invest advisory)',
				'Same backend — different chrome and homepage storytelling',
				'Live on production tenants today',
				'Module packs aligned to each vertical',
			),
			'capabilities' => $themes,
			'cta' => array('label' => 'See live customer sites', 'href' => $base . 'platform/customer-results'),
			'super_cp' => 'Industry settings → storefront_package · visual style templates · contact JSON presets',
		),
		array(
			'id' => 'continuity',
			'icon' => 'fa-shield',
			'title' => 'Business continuity & failover',
			'tagline' => 'Cloud-first operations with a professional backup path when the primary stack hiccups.',
			'summary' => 'Tenants run on the ECOM AE cloud day to day. If the primary cloud is slow or unreachable, shoppers see a guided splash while traffic can route to your backup origin — then sync back when healthy. Peace of mind without abandoning cloud scale.',
			'benefits' => array(
				'Automatic health detection — no manual rebuild per tenant',
				'Professional splash screens instead of generic error pages',
				'Sticky backup banner while failover is active',
				'Clear failback story when the cloud returns',
			),
			'capabilities' => array(
				array('label' => 'Primary cloud always on', 'text' => 'Storefront, /cp/, and /erp on hosted ECOM AE infrastructure under normal operations.'),
				array('label' => 'Splash modes', 'text' => 'Connecting to backup, backup online, restoring cloud — each with shopper-friendly copy and step indicators.'),
				array('label' => 'Operator control', 'text' => 'Platform operators manage failover modes from Super CP — detailed runbooks are not published on this marketing site.'),
				array('label' => 'Tenant peace of mind', 'text' => 'Ideal for agencies selling cloud scale to clients who want a credible continuity story.'),
			),
			'cta' => array('label' => 'Continuity deep dive', 'href' => $base . 'platform/business-continuity'),
			'super_cp' => 'Tenant hub → Failover guide · platform status · splash preview',
		),
		array(
			'id' => 'operations',
			'icon' => 'fa-cogs',
			'title' => 'Operations, DNS & SSL',
			'tagline' => 'Model C hosting — one nginx stack, many client domains, managed certificates.',
			'summary' => 'Clients keep their registrar domain. You add an A record to the platform IP and attach the hostname as an alias on the live ecomae stack — no separate hosting package per client. SSL is issued automatically; platform upgrades apply to all tenants.',
			'benefits' => array(
				'No per-client cPanel or CloudPanel site — saves disk and ops time',
				'Let\'s Encrypt SSL on every tenant hostname',
				'Hostname-based routing to isolated tenant databases',
				'Platform patches and security updates handled centrally',
			),
			'capabilities' => array(
				array('label' => 'DNS-only onboarding', 'text' => 'Client adds @ and www A records at GoDaddy (or any registrar). Propagation typically 5–60 minutes.'),
				array('label' => 'Domain alias model', 'text' => 'New client domains attach to the existing platform vhost — same codebase, per-host branding and DB routing.'),
				array('label' => 'SSL & trust', 'text' => 'Certificates renew on the platform stack; shoppers always see HTTPS on www.client.com.'),
				array('label' => 'Go live in 24 hours', 'text' => 'Most operators move from intro form to working storefront + CP + ERP within one business day once DNS is pointed.'),
			),
			'cta' => array('label' => 'Request onboarding call', 'href' => $base . 'platform/contact'),
			'super_cp' => 'Tenant hub → DNS tab · Industry settings deploy · Health probes',
		),
		array(
			'id' => 'results',
			'icon' => 'fa-trophy',
			'title' => 'Customer results & ecosystems',
			'tagline' => 'Live proof across commerce, advisory, fashion, electronics, and jewellery tenants.',
			'summary' => 'Production tenants demonstrate implementation quality: each has a public storefront and operational /cp/ on the same domain. ECOM AE groups industries into five connected ecosystems with active verticals today and expansion-ready roadmap slots.',
			'benefits' => array(
				'Transparent links to live sites and client portals',
				'Five-ecosystem governance model for structured expansion',
				'Repeatable B2B and B2C workflows per vertical',
				'Your next tenant can follow the same playbook',
			),
			'capabilities' => array(
				array('label' => 'Live tenant gallery', 'text' => 'eParts Cart, taxofinca, electronicae, stylenlook, thejewellerytrend — each with outcome summary and direct site links.'),
				array('label' => 'Commerce ecosystem', 'text' => 'Auto parts, fashion e-commerce, electronics retail, medical supplies.'),
				array('label' => 'Business services ecosystem', 'text' => 'Tax advisory portals, consultancy CRM + billing, ERP finance workflows.'),
				array('label' => 'Digital & technology ecosystem', 'text' => 'Super CP tenant orchestration, client CP operations, integrated ERP + CRM stack.'),
			),
			'cta' => array('label' => 'View customer results', 'href' => $base . 'platform/customer-results'),
			'super_cp' => 'Tenant hub → Tenants · Health · Customer-facing proof links',
		),
	);
}

/** Named storefront packages with live tenant examples (customer-facing). */
function epc_ecomae_platform_storefront_theme_showcase()
{
	return array(
		array(
			'label' => 'Automotive spare parts pro',
			'text' => 'Piston hero, VIN-aware catalogue, animated parts branding — built for eParts-style distributors.',
			'example' => 'epartscart.com',
			'example_url' => 'https://www.epartscart.com/',
			'industry' => 'auto_parts',
		),
		array(
			'label' => 'Virgin Megastore retail',
			'text' => 'Black utility bar, mega navigation, spec-heavy electronics homepage — B2C/B2B SKU retail.',
			'example' => 'electronicae.com',
			'example_url' => 'https://www.electronicae.com/',
			'industry' => 'electronics',
		),
		array(
			'label' => 'Prime Invest consulting',
			'text' => 'Finance advisory layout, services hero, client hub — tax and corporate advisory firms.',
			'example' => 'taxofinca.com',
			'example_url' => 'https://www.taxofinca.com/',
			'industry' => 'tax_advisory',
		),
		array(
			'label' => 'Namshi fashion & beauty',
			'text' => 'Clean white layout, category chips, collection grids — fashion and beauty retail.',
			'example' => 'stylenlook.com',
			'example_url' => 'https://www.stylenlook.com/',
			'industry' => 'fashion',
		),
		array(
			'label' => 'Kiyasha jewellery luxury',
			'text' => 'Gold and diamond aesthetic, gallery product cards, premium enquiry flows — fine jewellery.',
			'example' => 'thejewellerytrend.com',
			'example_url' => 'https://www.thejewellerytrend.com/',
			'industry' => 'jewellery',
		),
	);
}

function epc_ecomae_platform_nav()
{
	$base = epc_ecomae_platform_base_url();
	return array(
		array('key' => 'home', 'label' => 'Home', 'href' => $base),
		array('key' => 'platform', 'label' => 'Platform', 'href' => $base . 'platform'),
		array('key' => 'industries', 'label' => 'Industries', 'href' => $base . 'platform/industries'),
		array('key' => 'capabilities', 'label' => 'Capabilities', 'href' => $base . 'platform/capabilities'),
		array('key' => 'free_tools', 'label' => 'Free Tools', 'href' => $base . 'platform/free-tools'),
		array('key' => 'pricing', 'label' => 'Pricing', 'href' => $base . 'platform/pricing'),
		array('key' => 'about', 'label' => 'About', 'href' => $base . 'platform/about'),
		array('key' => 'contact', 'label' => 'Contact', 'href' => $base . 'platform/contact'),
	);
}

function epc_ecomae_platform_nav_dropdowns()
{
	$base = epc_ecomae_platform_base_url();
	return array(
		array(
			'key' => 'solutions',
			'label' => 'Solutions',
			'items' => array(
				array('key' => 'platform_guides', 'label' => 'Super CP guides', 'href' => $base . 'platform/platform-guides'),
				array('key' => 'api_services', 'label' => 'Catalog & Price API', 'href' => $base . 'platform/api-services'),
				array('key' => 'api_documentation', 'label' => 'Tenant ERP API', 'href' => $base . 'platform/api-documentation'),
				array('key' => 'auto_price_ai', 'label' => 'Auto Price AI', 'href' => $base . 'platform/auto-price-ai'),
				array('key' => 'faq', 'label' => 'FAQ', 'href' => $base . 'platform/faq'),
				array('key' => 'customer_results', 'label' => 'Customer results', 'href' => $base . 'platform/customer-results'),
				array('key' => 'business_continuity', 'label' => 'Continuity', 'href' => $base . 'platform/business-continuity'),
				array('key' => 'demo', 'label' => '3-day demo', 'href' => $base . 'platform/demo'),
			),
		),
		array(
			'key' => 'resources',
			'label' => 'Resources',
			'items' => array(
				array('key' => 'docs', 'label' => 'Documentation', 'href' => $base . 'documentation'),
				array('key' => 'compare', 'label' => 'Compare', 'href' => $base . 'compare'),
				array('key' => 'blockchain', 'label' => 'Blockchain BOS', 'href' => $base . 'blockchain'),
				array('key' => 'bos', 'label' => 'What is Blockchain BOS', 'href' => $base . 'bos'),
				array('key' => 'solution', 'label' => 'Solutions', 'href' => $base . 'solutions'),
			),
		),
	);
}

function epc_ecomae_platform_customer_results()
{
	$base = '/content/files/images/ecomae-platform/assets/';
	return array(
		array(
			'key' => 'epartscart',
			'name' => 'eParts Cart',
			'industry' => 'auto_parts',
			'theme' => 'automotive_spareparts_pro',
			'outcome' => 'Flagship auto-parts tenant on the platform: VIN-aware catalogue, supplier price files, procurement, logistics, and B2B trade accounts with managed CP.',
			'site_url' => 'https://www.epartscart.com/',
			'portal_url' => 'https://www.epartscart.com/cp/',
			'logo_url' => $base . 'epartscart.png',
		),
		array(
			'key' => 'taxofinca',
			'name' => 'taxofinca',
			'industry' => 'tax_advisory',
			'theme' => 'consulting_primeinvest',
			'outcome' => 'Tax advisory tenant with professional presentation, client journeys, and CP workflow support.',
			'site_url' => 'https://www.taxofinca.com/',
			'portal_url' => 'https://www.taxofinca.com/cp/',
			'logo_url' => $base . 'taxofinca.png',
		),
		array(
			'key' => 'electronicae',
			'name' => 'electronicae',
			'industry' => 'electronics',
			'theme' => 'electronics_retail_virgin',
			'outcome' => 'Electronics-focused tenant demonstrating SKU-heavy storefront structure with managed CP backend.',
			'site_url' => 'https://www.electronicae.com/',
			'portal_url' => 'https://www.electronicae.com/cp/',
			'logo_url' => $base . 'electronicae.png',
		),
		array(
			'key' => 'stylenlook',
			'name' => 'stylenlook',
			'industry' => 'fashion',
			'theme' => 'fashion_retail_namshi',
			'outcome' => 'Fashion/retail style tenant proving themed commerce delivery with tenant-isolated operations.',
			'site_url' => 'https://www.stylenlook.com/',
			'portal_url' => 'https://www.stylenlook.com/cp/',
			'logo_url' => $base . 'stylenlook.png',
		),
		array(
			'key' => 'thejewellerytrend',
			'name' => 'thejewellerytrend',
			'industry' => 'jewellery',
			'theme' => 'jewellery_retail_kiyasha',
			'outcome' => 'Jewellery-oriented tenant showing premium catalogue presentation and portal access under one domain.',
			'site_url' => 'https://www.thejewellerytrend.com/',
			'portal_url' => 'https://www.thejewellerytrend.com/cp/',
			'logo_url' => $base . 'thejewellerytrend.png',
		),
	);
}

/**
 * Rich marketing content per industry — separate landing page for each vertical.
 */
function epc_ecomae_platform_industry_details()
{
	return array(
		'auto_parts' => array(
			'summary' => 'Built for spare parts distributors and workshops: VIN-aware catalogue, multi-supplier price search, procurement, and B2B trade accounts on one hosted stack.',
			'storefront' => array('Vehicle & VIN search widgets', 'Brand / model catalogue tree', 'Live supplier price display', 'B2B login with fixed currency', 'WhatsApp order share', 'Multi-language storefront'),
			'cp_special' => array('Epart prices upload & crosses', 'Procurement & supplier POs', 'Warehouse & logistics module', 'Customer balance & credit limits', 'Document control & tax invoices', 'Industry-themed CP sidebar'),
			'workflows' => array('Customer searches part by VIN or OEM number', 'System pulls prices from connected suppliers', 'Order placed → ERP invoice → delivery note', 'B2B client manages balance in client CP'),
			'ideal_for' => 'Auto spare parts traders, workshops, and regional distributors in UAE & GCC.',
		),
		'tax_advisory' => array(
			'summary' => 'Tax firms and advisory practices get a client portal, VAT-ready finance, document control, and approval workflows — without building custom software.',
			'storefront' => array('Professional services homepage', 'Client login & secure hub', 'Service enquiry forms', 'Document download area', 'Branded advisory theme', 'Mobile-friendly client area'),
			'cp_special' => array('Customer approval workflows', 'UAE VAT & e-invoicing (FTA)', 'Document control templates', 'ERP finance & GL', 'Client CRM & assignments', 'Role-based CP access'),
			'workflows' => array('Prospect submits enquiry on storefront', 'Advisor onboard client in CP', 'Documents generated with TRN & legal footer', 'VAT return data exported from ERP module'),
			'ideal_for' => 'Tax consultancies, accounting firms, and corporate advisory boutiques.',
		),
		'fashion' => array(
			'summary' => 'Fashion and apparel brands sell collections online with variant catalogue, campaigns, and fulfilment — hosted on the client domain.',
			'storefront' => array('Collection & lookbook layouts', 'Size / colour variants', 'Campaign landing pages', 'Wishlist & cart', 'Multi-currency checkout', 'Instagram-ready product grids'),
			'cp_special' => array('Catalogue bulk upload', 'Marketing campaigns module', 'Order fulfilment & returns', 'Channel integrations pack', 'Stock by size/colour', 'Promo & discount rules'),
			'workflows' => array('Upload seasonal collection via CP', 'Launch campaign on storefront', 'Orders flow to warehouse pick list', 'Returns managed in CP'),
			'ideal_for' => 'Fashion boutiques, uniform suppliers, and D2C apparel brands.',
		),
		'electronics' => array(
			'summary' => 'SKU-heavy electronics retail with spec filters, warranty tracking, and B2C/B2B checkout on managed infrastructure.',
			'storefront' => array('Spec filter search', 'Compare products', 'Warranty registration hooks', 'B2B & retail price tiers', 'Rich product datasheets', 'Fast catalogue pagination'),
			'cp_special' => array('Bulk SKU import', 'Category & attribute manager', 'RMA / returns handling', 'Payment gateway setup', 'ERP stock valuation', 'Serial / batch fields'),
			'workflows' => array('Import SKU sheet with specs', 'Customer filters by brand & spec', 'Order → invoice with warranty terms', 'RMA processed in CP'),
			'ideal_for' => 'Electronics retailers, IT hardware resellers, and component distributors.',
		),
		'medical' => array(
			'summary' => 'Medical supplies ordering with regulated catalogue, customer accounts, and audit-friendly document trails.',
			'storefront' => array('Regulated product catalogue', 'Institutional customer login', 'Re-order from history', 'Certificate & spec display', 'Delivery scheduling', 'Clean clinical theme'),
			'cp_special' => array('Customer account approvals', 'Document control & delivery notes', 'Batch / expiry tracking fields', 'Professional services CRM', 'ERP billing', 'Access-controlled CP roles'),
			'workflows' => array('Hospital account approved in CP', 'Catalogue ordered against contract', 'Delivery note + invoice archived', 'Compliance documents stored per order'),
			'ideal_for' => 'Medical supply distributors and clinic procurement teams.',
		),
		'health' => array(
			'summary' => 'Wellness and health product brands with subscriptions, client engagement, and a modern storefront.',
			'storefront' => array('Subscription-friendly catalogue', 'Client wellness hub', 'Blog / content blocks', 'Mobile-first checkout', 'Promo bundles', 'Email capture & campaigns'),
			'cp_special' => array('Marketing automation hooks', 'Customer profiles & notes', 'Recurring order support', 'Catalogue themes', 'Analytics-ready structure', 'Social channel links'),
			'workflows' => array('Launch wellness product line', 'Client subscribes via storefront', 'CP tracks repeat purchases', 'Campaign pushed to customer segment'),
			'ideal_for' => 'Supplement brands, wellness centres, and health lifestyle retailers.',
		),
		'consultancy' => array(
			'summary' => 'From proposal to delivery: CRM, projects, billing, and a client-facing portal for consultancies.',
			'storefront' => array('Service portfolio pages', 'Case study showcase', 'Contact & proposal requests', 'Client login area', 'Team & credentials section', 'Professional brand theme'),
			'cp_special' => array('CRM & customer hub', 'ERP invoicing & receipts', 'Document library', 'Team CP roles', 'Project billing hooks', 'Client approval flows'),
			'workflows' => array('Lead captured on website', 'Consultant creates project in CP', 'Time & materials invoiced', 'Client downloads deliverables from hub'),
			'ideal_for' => 'Management consultancies, agencies, and professional service firms.',
		),
		'rental' => array(
			'summary' => 'Monthly rental operators — fleet, equipment, or property — with rental periods, deposits, and self-service catalogue.',
			'storefront' => array('Rental SKU with periods', 'Availability calendar hooks', 'Deposit & contract summary', 'Customer self-service', 'Fleet / asset gallery', 'Enquiry-to-book flow'),
			'cp_special' => array('Rental period pricing', 'Contract & deposit tracking', 'Monthly billing hooks', 'Asset catalogue manager', 'Customer accounts', 'Logistics handover docs'),
			'workflows' => array('Asset listed with daily/monthly rate', 'Customer books via storefront', 'Contract & deposit recorded in CP', 'Monthly invoice generated from ERP'),
			'ideal_for' => 'Equipment rental, car rental, and commercial leasing businesses.',
		),
		'jewellery' => array(
			'summary' => 'High-trust jewellery presentation with certificates, premium visuals, and secure enquiry or checkout flows.',
			'storefront' => array('Gallery-style product cards', 'Certificate & spec fields', 'Appointment / enquiry CTA', 'Premium dark/gold themes', 'Zoom-ready imagery', 'Secure checkout setup'),
			'cp_special' => array('Certificate metadata on SKU', 'High-value order flags', 'Custom document templates', 'Customer VIP accounts', 'Inventory by weight / purity', 'Marketing for collections'),
			'workflows' => array('Collection photographed & uploaded', 'Customer enquires or buys online', 'Certificate attached to invoice', 'VIP client managed in CRM'),
			'ideal_for' => 'Jewellers, gold traders, and luxury accessory brands.',
		),
	);
}

function epc_ecomae_platform_industry_full($code)
{
	$all = epc_ecomae_platform_industry_marketing();
	if (!isset($all[$code])) {
		return null;
	}
	$ind = $all[$code];
	$details = epc_ecomae_platform_industry_details();
	if (isset($details[$code])) {
		$ind = array_merge($ind, $details[$code]);
	}
	return $ind;
}

/**
 * Full Super CP / client CP capability catalog for marketing (105 items).
 */
function epc_ecomae_platform_super_cp_capabilities_catalog()
{
	static $catalog = null;
	if ($catalog === null) {
		$catalog = require __DIR__ . '/epc_ecomae_platform_capabilities_catalog.php';
		if (!is_array($catalog)) {
			$catalog = array();
		}
	}
	return $catalog;
}

/** Category slug => count for chips and filters. */
function epc_ecomae_platform_super_cp_capability_categories()
{
	$counts = array();
	foreach (epc_ecomae_platform_super_cp_capabilities_catalog() as $cap) {
		$cat = isset($cap['category']) ? (string) $cap['category'] : 'Other';
		if (!isset($counts[$cat])) {
			$counts[$cat] = 0;
		}
		$counts[$cat]++;
	}
	return $counts;
}

function epc_ecomae_platform_super_cp_capability_count()
{
	return count(epc_ecomae_platform_super_cp_capabilities_catalog());
}
