<?php
/**
 * ERP area groups — left sidebar navigation (Dolibarr-inspired layout).
 */
defined('_ASTEXE_') or die('No access');

function epc_erp_nav_areas_config()
{
	return array(
		'overview' => array(
			'label' => 'Home',
			'icon' => 'fa-th-large',
			'desc' => 'Dashboards, workflow and approvals',
			'tabs' => array(
				'dashboard' => array('label' => 'Dashboard', 'icon' => 'fa-dashboard'),
				'workflow' => array('label' => 'Workflow', 'icon' => 'fa-tasks'),
				'processflow' => array('label' => 'Process flow', 'icon' => 'fa-sitemap'),
				'approvals' => array('label' => 'Approvals', 'icon' => 'fa-check-square-o'),
			),
			'groups' => array(
				'Workspaces' => array('dashboard', 'workflow', 'processflow'),
				'Governance' => array('approvals'),
			),
		),
		'sales' => array(
			'label' => 'Sales and marketing',
			'icon' => 'fa-line-chart',
			'desc' => 'CRM, prospects, quotations, sales orders, delivery and invoicing',
			'tabs' => array(
				'crm' => array('label' => 'CRM', 'icon' => 'fa-handshake-o', 'raw' => true),
				'marketing' => array('label' => 'Marketing', 'icon' => 'fa-bullhorn'),
				'proposals' => array('label' => 'Sales quotations', 'icon' => 'fa-file-text'),
				'sales_orders' => array('label' => 'Sales orders', 'icon' => 'fa-shopping-cart'),
				'subscriptions' => array('label' => 'Subscriptions', 'icon' => 'fa-refresh'),
				'delivery_notes' => array('label' => 'Delivery notes', 'icon' => 'fa-truck'),
				'invoices' => array('label' => 'Invoices (e-invoice)', 'icon' => 'fa-file-text-o'),
				'revenue' => array('label' => 'Revenue', 'icon' => 'fa-money'),
				'fulfilment' => array('label' => 'Fulfilment', 'icon' => 'fa-random'),
			),
			'groups' => array(
				'Common' => array('crm', 'marketing'),
				'Orders' => array('proposals', 'sales_orders', 'subscriptions', 'delivery_notes', 'invoices'),
				'Inquiries and reports' => array('revenue', 'fulfilment'),
			),
		),
		'ar' => array(
			'label' => 'Accounts receivable',
			'icon' => 'fa-users',
			'desc' => 'Customer receivables, collections and AR setup',
			'tabs' => array(
				'receivables' => array('label' => 'Receivables', 'icon' => 'fa-users'),
				'collections' => array('label' => 'Collections', 'icon' => 'fa-gavel'),
				'ar_setup' => array('label' => 'Customer setup', 'icon' => 'fa-handshake-o'),
			),
			'groups' => array(
				'Common' => array('receivables', 'collections'),
				'Setup' => array('ar_setup'),
			),
		),
		'purchasing' => array(
			'label' => 'Procurement and sourcing',
			'icon' => 'fa-shopping-basket',
			'desc' => 'Suppliers, RFQ, purchase orders, landed cost, customs and logistics',
			'tabs' => array(
				'supplier_portal' => array('label' => 'Supplier portal', 'icon' => 'fa-handshake-o'),
				'rfq' => array('label' => 'RFQ', 'icon' => 'fa-envelope-o'),
				'purchase_orders' => array('label' => 'Purchase orders', 'icon' => 'fa-clipboard'),
				'purchases' => array('label' => 'Purchases', 'icon' => 'fa-file-text-o'),
				'three_way_match' => array('label' => '3-way match', 'icon' => 'fa-check-square-o'),
				'landed_cost' => array('label' => 'Landed cost', 'icon' => 'fa-ship'),
				'custom_shipping' => array('label' => 'Customs & shipping', 'icon' => 'fa-ship'),
				'procurement_link' => array('label' => 'Procurement CP', 'icon' => 'fa-external-link', 'external' => true),
			),
			'groups' => array(
				'Common' => array('supplier_portal'),
				'Orders' => array('rfq', 'purchase_orders', 'purchases', 'three_way_match'),
				'Logistics' => array('landed_cost', 'custom_shipping', 'procurement_link'),
			),
		),
		'ap' => array(
			'label' => 'Accounts payable',
			'icon' => 'fa-credit-card',
			'desc' => 'Supplier payables and AP setup',
			'tabs' => array(
				'payables' => array('label' => 'Payables', 'icon' => 'fa-truck'),
				'ap_setup' => array('label' => 'Vendor setup', 'icon' => 'fa-credit-card'),
			),
			'groups' => array(
				'Common' => array('payables'),
				'Setup' => array('ap_setup'),
			),
		),
		'banking' => array(
			'label' => 'Cash and bank management',
			'icon' => 'fa-money',
			'desc' => 'Bank accounts, cash, payments and reconciliation',
			'tabs' => array(
				'cash_bank' => array('label' => 'Cash & bank', 'icon' => 'fa-university'),
				'petty_cash' => array('label' => 'Petty cash', 'icon' => 'fa-money'),
				'payment_batches' => array('label' => 'Payment batches', 'icon' => 'fa-send'),
				'bank_recon' => array('label' => 'Bank recon', 'icon' => 'fa-check-square'),
				'bank_setup' => array('label' => 'Bank accounts', 'icon' => 'fa-university'),
			),
			'groups' => array(
				'Common' => array('cash_bank', 'petty_cash'),
				'Journals' => array('payment_batches'),
				'Inquiries and reports' => array('bank_recon'),
				'Setup' => array('bank_setup'),
			),
		),
		'finance' => array(
			'label' => 'General ledger',
			'icon' => 'fa-university',
			'desc' => 'Chart of accounts, journals, period close and financial statements',
			'tabs' => array(
				'gl' => array('label' => 'General ledger', 'icon' => 'fa-book'),
				'opening_balances' => array('label' => 'Opening balances', 'icon' => 'fa-flag-o'),
				'aging' => array('label' => 'Aging (AR/AP/Inv)', 'icon' => 'fa-hourglass-half'),
				'pl' => array('label' => 'Profit & loss', 'icon' => 'fa-bar-chart'),
				'balance_sheet' => array('label' => 'Balance sheet', 'icon' => 'fa-balance-scale'),
				'reports' => array('label' => 'Reports', 'icon' => 'fa-table'),
				'enterprise_reports' => array('label' => 'Trial balance / reports', 'icon' => 'fa-table'),
				'fin_advanced' => array('label' => 'Financial depth', 'icon' => 'fa-sliders'),
				'year_end' => array('label' => 'Year-end closing', 'icon' => 'fa-calendar-check-o'),
				'coa' => array('label' => 'Chart of accounts', 'icon' => 'fa-list'),
			),
			'groups' => array(
				'Common' => array('gl'),
				'Journals' => array('opening_balances'),
				'Inquiries and reports' => array('aging', 'pl', 'balance_sheet', 'reports', 'enterprise_reports'),
				'Periodic' => array('fin_advanced', 'year_end'),
				'Setup' => array('coa'),
			),
		),
		'tax' => array(
			'label' => 'Tax',
			'icon' => 'fa-percent',
			'desc' => 'VAT/tax returns, e-invoicing, compliance and statutory reporting',
			'tabs' => array(
				'vat_return' => array('label' => 'VAT return', 'icon' => 'fa-percent'),
				'tax_compliance' => array('label' => 'Tax compliance', 'icon' => 'fa-gavel'),
				'vat_refund' => array('label' => 'Tourist VAT refunds', 'icon' => 'fa-plane'),
				'einvoice' => array('label' => 'E-invoicing', 'icon' => 'fa-file-code-o'),
				'compliance' => array('label' => 'Compliance center', 'icon' => 'fa-shield'),
				'ext_reports' => array('label' => 'External reporting', 'icon' => 'fa-file-text-o'),
				'document_control' => array('label' => 'Document control', 'icon' => 'fa-print'),
			),
			'groups' => array(
				'Declarations' => array('vat_return', 'tax_compliance', 'vat_refund'),
				'Common' => array('einvoice', 'compliance'),
				'Reports' => array('ext_reports'),
				'Setup' => array('document_control'),
			),
		),
		'fixed_assets' => array(
			'label' => 'Fixed assets',
			'icon' => 'fa-building',
			'desc' => 'Asset register, depreciation and disposals',
			'tabs' => array(
				'fixed_assets' => array('label' => 'Fixed assets', 'icon' => 'fa-building'),
			),
			'groups' => array(
				'Common' => array('fixed_assets'),
			),
		),
		'budgeting' => array(
			'label' => 'Budgeting',
			'icon' => 'fa-pie-chart',
			'desc' => 'Budget registers, control and budget vs actual',
			'tabs' => array(
				'budgeting' => array('label' => 'Budgeting', 'icon' => 'fa-pie-chart'),
			),
			'groups' => array(
				'Common' => array('budgeting'),
			),
		),
		'inventory_mgmt' => array(
			'label' => 'Inventory management',
			'icon' => 'fa-cubes',
			'desc' => 'Stock, item groups, order planning and barcodes',
			'tabs' => array(
				'inventory' => array('label' => 'Inventory', 'icon' => 'fa-cubes'),
				'inv_groups' => array('label' => 'Inventory (stock/groups)', 'icon' => 'fa-object-group'),
				'order_planning' => array('label' => 'Order planning', 'icon' => 'fa-cubes'),
				'retail_barcode' => array('label' => 'Retail barcode', 'icon' => 'fa-barcode'),
			),
			'groups' => array(
				'Common' => array('inventory', 'inv_groups'),
				'Periodic' => array('order_planning'),
				'Setup' => array('retail_barcode'),
			),
		),
		'pim' => array(
			'label' => 'Product information management',
			'icon' => 'fa-cube',
			'desc' => 'Products, dimensions and variant structures',
			'tabs' => array(
				'product_info' => array('label' => 'Product information', 'icon' => 'fa-cube'),
			),
			'groups' => array(
				'Common' => array('product_info'),
			),
		),
		'warehouse' => array(
			'label' => 'Warehouse management',
			'icon' => 'fa-cubes',
			'desc' => 'Locations, license plates, waves, work and mobile RF',
			'tabs' => array(
				'wms' => array('label' => 'Warehouse management', 'icon' => 'fa-cubes'),
			),
			'groups' => array(
				'Common' => array('wms'),
			),
		),
		'production' => array(
			'label' => 'Production control',
			'icon' => 'fa-cogs',
			'desc' => 'Production orders, routes, operations and quality',
			'tabs' => array(
				'manufacturing' => array('label' => 'Production', 'icon' => 'fa-cogs'),
				'mfg_planning' => array('label' => 'Production planning', 'icon' => 'fa-cogs'),
				'quality' => array('label' => 'Quality management', 'icon' => 'fa-check-circle'),
			),
			'groups' => array(
				'Common' => array('manufacturing'),
				'Periodic' => array('mfg_planning'),
				'Quality' => array('quality'),
			),
		),
		'master_planning_area' => array(
			'label' => 'Master planning',
			'icon' => 'fa-random',
			'desc' => 'Master scheduling and material requirements planning',
			'tabs' => array(
				'master_planning' => array('label' => 'Master planning', 'icon' => 'fa-random'),
			),
			'groups' => array(
				'Common' => array('master_planning'),
			),
		),
		'cost_mgmt' => array(
			'label' => 'Cost management',
			'icon' => 'fa-balance-scale',
			'desc' => 'Inventory costing value models and analysis',
			'tabs' => array(
				'cost_models' => array('label' => 'Costing value-models', 'icon' => 'fa-balance-scale'),
			),
			'groups' => array(
				'Common' => array('cost_models'),
			),
		),
		'retail' => array(
			'label' => 'Retail and commerce',
			'icon' => 'fa-shopping-cart',
			'desc' => 'Channels, assortments, retail pricing, POS and statements',
			'tabs' => array(
				'retail_commerce' => array('label' => 'Retail & commerce', 'icon' => 'fa-shopping-cart', 'raw' => true),
			),
			'groups' => array(
				'Common' => array('retail_commerce'),
			),
		),
		'people' => array(
			'label' => 'Human resources',
			'icon' => 'fa-users',
			'desc' => 'Employees, payroll and labour-law compliance',
			'tabs' => array(
				'staff' => array('label' => 'Workers', 'icon' => 'fa-id-badge'),
				'hr' => array('label' => 'HR', 'icon' => 'fa-user-circle'),
				'hr_ops' => array('label' => 'HR operations', 'icon' => 'fa-users'),
				'hr_law' => array('label' => 'Labour law & compliance', 'icon' => 'fa-gavel', 'raw' => true),
				'payroll' => array('label' => 'Payroll', 'icon' => 'fa-money'),
			),
			'groups' => array(
				'Common' => array('staff', 'hr', 'hr_ops'),
				'Compliance' => array('hr_law'),
				'Payroll' => array('payroll'),
			),
		),
		'expense' => array(
			'label' => 'Expense management',
			'icon' => 'fa-credit-card',
			'desc' => 'Employee expense reports and reimbursements',
			'tabs' => array(
				'expense_reports' => array('label' => 'Expense reports', 'icon' => 'fa-credit-card'),
			),
			'groups' => array(
				'Common' => array('expense_reports'),
			),
		),
		'projects' => array(
			'label' => 'Project management and accounting',
			'icon' => 'fa-tasks',
			'desc' => 'Projects, budgets, WIP and revenue recognition',
			'tabs' => array(
				'projects' => array('label' => 'Projects', 'icon' => 'fa-tasks'),
				'project_accounting' => array('label' => 'Project accounting', 'icon' => 'fa-pie-chart'),
			),
			'groups' => array(
				'Common' => array('projects'),
				'Periodic' => array('project_accounting'),
			),
		),
		'risk' => array(
			'label' => 'Compliance',
			'icon' => 'fa-shield',
			'desc' => 'Insurance policies, claims and document expiry tracking',
			'tabs' => array(
				'insurance' => array('label' => 'Insurance', 'icon' => 'fa-shield'),
				'doc_expiry' => array('label' => 'Document expiry', 'icon' => 'fa-calendar-times-o'),
			),
			'groups' => array(
				'Common' => array('insurance', 'doc_expiry'),
			),
		),
		'enterprise' => array(
			'label' => 'Organization administration',
			'icon' => 'fa-building-o',
			'desc' => 'Legal entities, business units, dimensions, address book and documents',
			'tabs' => array(
				'business_units' => array('label' => 'Business unit', 'icon' => 'fa-sitemap'),
				'org_admin' => array('label' => 'Organization administration', 'icon' => 'fa-sitemap'),
				'multi_entity' => array('label' => 'Multi-entity', 'icon' => 'fa-sitemap'),
				'consolidation_bu' => array('label' => 'Consolidation', 'icon' => 'fa-sitemap'),
				'agenda' => array('label' => 'Agenda', 'icon' => 'fa-calendar'),
				'contacts' => array('label' => 'Contacts', 'icon' => 'fa-address-book-o'),
				'documents' => array('label' => 'Documents', 'icon' => 'fa-folder-open-o'),
				'contracts' => array('label' => 'Contracts & e-sign', 'icon' => 'fa-file-text-o'),
				'doc_formats' => array('label' => 'Document formats', 'icon' => 'fa-files-o'),
				'knowledge_base' => array('label' => 'Knowledge base', 'icon' => 'fa-book'),
				'ai_advisor' => array('label' => 'AI advisor', 'icon' => 'fa-magic'),
				'listing' => array('label' => 'Listing', 'icon' => 'fa-list-alt'),
			),
			'groups' => array(
				'Organization' => array('business_units', 'org_admin', 'multi_entity', 'consolidation_bu'),
				'Documents' => array('agenda', 'contacts', 'documents', 'contracts', 'doc_formats'),
				'Knowledge' => array('knowledge_base', 'ai_advisor'),
				'Setup' => array('listing'),
			),
		),
		'setup' => array(
			'label' => 'System administration',
			'icon' => 'fa-sliders',
			'desc' => 'Company setup, security roles, batch/platform services, data and integration',
			'tabs' => array(
				'erp_setup' => array('label' => 'Accounting setup', 'icon' => 'fa-cogs'),
				'security_roles' => array('label' => 'Security roles', 'icon' => 'fa-shield'),
				'platform' => array('label' => 'Platform services', 'icon' => 'fa-cogs'),
				'data_import' => array('label' => 'Data import', 'icon' => 'fa-upload'),
				'integration' => array('label' => 'Data & integration', 'icon' => 'fa-plug'),
				'audit' => array('label' => 'Audit trail', 'icon' => 'fa-history'),
			),
			'groups' => array(
				'Setup' => array('erp_setup'),
				'Security' => array('security_roles'),
				'Platform' => array('platform'),
				'Data management' => array('data_import', 'integration'),
				'Inquiries' => array('audit'),
			),
		),
	);
}

function epc_erp_nav_label_plain($label)
{
	return html_entity_decode(strip_tags((string) $label), ENT_QUOTES | ENT_HTML5, 'UTF-8');
}

function epc_erp_tab_to_area($tab)
{
	$tab = (string) $tab;
	if ($tab === 'bank_recon') {
		return 'banking';
	}
	foreach (epc_erp_nav_areas_config() as $areaKey => $area) {
		if (isset($area['tabs'][$tab])) {
			return $areaKey;
		}
	}
	return 'overview';
}

function epc_erp_area_default_tab($area)
{
	$cfg = epc_erp_nav_areas_config();
	if (!isset($cfg[$area]['tabs'])) {
		return 'dashboard';
	}
	$keys = array_keys($cfg[$area]['tabs']);
	return $keys[0] ?? 'dashboard';
}

function epc_erp_tab_url($base, $tab, $from, $to, $area = '')
{
	if ($area === '') {
		$area = epc_erp_tab_to_area($tab);
	}
	$q = 'area=' . urlencode($area) . '&tab=' . urlencode($tab)
		. '&from=' . urlencode($from) . '&to=' . urlencode($to);
	$url = $base . '?' . $q;
	if (!function_exists('epc_erp_shell_url_query')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cp_shell.php';
	}
	$shellQ = epc_erp_shell_url_query();
	if ($shellQ !== '') {
		$url .= '&' . $shellQ;
	}
	return $url;
}

function epc_erp_nav_shell_link_attrs(): string
{
	if (!function_exists('epc_erp_shell_link_attrs')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cp_shell.php';
	}
	return epc_erp_shell_link_attrs();
}

function epc_erp_procurement_url()
{
	global $DP_Config;
	if (function_exists('epc_portal_demo_cp_is_erp_only') && epc_portal_demo_cp_is_erp_only()
		&& function_exists('epc_portal_demo_cp_tenant_base')) {
		return epc_portal_demo_cp_tenant_base() . 'shop/procurement/procurement';
	}
	$backend = isset($DP_Config->backend_dir) ? (string) $DP_Config->backend_dir : 'cp';
	return '/' . $backend . '/shop/procurement/procurement';
}

function epc_erp_nav_apply_commerce_filter(array $areas): array
{
	if (!function_exists('epc_erp_has_commerce_integration')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
	}
	if (epc_erp_has_commerce_integration()) {
		return $areas;
	}
	foreach ($areas as $areaKey => &$area) {
		if (!isset($area['tabs']) || !is_array($area['tabs'])) {
			continue;
		}
		foreach (epc_erp_commerce_tab_keys() as $tabKey) {
			unset($area['tabs'][$tabKey]);
		}
		if ($areaKey === 'sales') {
			$area['desc'] = 'CRM, sales orders, invoices and receivables';
		}
		if ($areaKey === 'purchasing') {
			$area['desc'] = 'Suppliers, RFQ, POs and payables';
		}
	}
	unset($area);
	return $areas;
}

function epc_erp_nav_tab_allowed($tabKey, array $allowedTabs)
{
	if (!function_exists('epc_erp_has_commerce_integration')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
	}
	if (in_array($tabKey, epc_erp_commerce_tab_keys(), true) && !epc_erp_has_commerce_integration()) {
		return false;
	}
	if ($tabKey === 'procurement_link') {
		return !empty($GLOBALS['epc_erp_cp_links']);
	}
	if ($tabKey === 'bank_recon') {
		return in_array('bank_recon', $allowedTabs, true) || in_array('cash_bank', $allowedTabs, true);
	}
	return in_array($tabKey, $allowedTabs, true);
}

function epc_erp_nav_area_visible_tabs($areaKey, array $allowedTabs)
{
	$areas = epc_erp_nav_areas_for_tenant();
	if (!isset($areas[$areaKey]['tabs'])) {
		return array();
	}
	$out = array();
	foreach ($areas[$areaKey]['tabs'] as $tabKey => $meta) {
		if (epc_erp_nav_tab_allowed($tabKey, $allowedTabs)) {
			$out[$tabKey] = $meta;
		}
	}
	return $out;
}

function epc_erp_nav_tab_label($areaKey, $tabKey)
{
	$areas = epc_erp_nav_areas_config();
	if (!isset($areas[$areaKey]['tabs'][$tabKey])) {
		return ucfirst(str_replace('_', ' ', $tabKey));
	}
	$meta = $areas[$areaKey]['tabs'][$tabKey];
	if (!empty($meta['raw'])) {
		return strip_tags($meta['label']);
	}
	return html_entity_decode(strip_tags($meta['label']), ENT_QUOTES, 'UTF-8');
}

function epc_erp_nav_areas_for_tenant()
{
	if (function_exists('epc_portal_erp_modules_full_access_context')
		&& epc_portal_erp_modules_full_access_context()) {
		return epc_erp_nav_apply_commerce_filter(epc_erp_nav_areas_config());
	}
	$areas = epc_erp_nav_areas_config();
	$modFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_erp_modules.php';
	if (!is_file($modFile)) {
		return epc_erp_nav_apply_commerce_filter($areas);
	}
	require_once $modFile;
	if (!function_exists('epc_portal_erp_modules_enabled_areas')) {
		return epc_erp_nav_apply_commerce_filter($areas);
	}
	$enabledAreas = epc_portal_erp_modules_enabled_areas();
	if (empty($enabledAreas)) {
		return epc_erp_nav_apply_commerce_filter($areas);
	}
	$filtered = array();
	foreach ($areas as $key => $area) {
		if (in_array($key, $enabledAreas, true)) {
			$filtered[$key] = $area;
		}
	}
	return epc_erp_nav_apply_commerce_filter(!empty($filtered) ? $filtered : $areas);
}

function epc_erp_render_sidebar_nav($erpUrl, $activeArea, $activeTab, $from, $to, array $allowedTabs)
{
	$areas = epc_erp_nav_areas_for_tenant();
	echo '<nav class="epc-erp-sidebar-nav" aria-label="ERP modules">';
	echo '<ul class="epc-erp-sidebar-list">';
	foreach ($areas as $areaKey => $area) {
		$visibleTabs = epc_erp_nav_area_visible_tabs($areaKey, $allowedTabs);
		if (empty($visibleTabs)) {
			continue;
		}
		$isActiveArea = ($areaKey === $activeArea);
		$isOpen = $isActiveArea;
		echo '<li class="epc-erp-sidebar-group' . ($isActiveArea ? ' is-active-area' : '')
			. ($isOpen ? ' is-open' : '') . '" data-area="' . epc_erp_h($areaKey) . '">';
		echo '<button type="button" class="epc-erp-sidebar-group-hd" aria-expanded="' . ($isOpen ? 'true' : 'false') . '">';
		echo '<i class="fa ' . epc_erp_h($area['icon']) . ' epc-erp-sidebar-icon"></i>';
		echo '<span class="epc-erp-sidebar-label">' . epc_erp_h(epc_erp_nav_label_plain($area['label'])) . '</span>';
		echo '<i class="fa fa-chevron-right epc-erp-sidebar-chevron" aria-hidden="true"></i>';
		echo '</button>';
		echo '<ul class="epc-erp-sidebar-sublist">';
		// Enterprise style: group sub-modules (Common / Journals / Inquiries and
		// reports / Setup / Periodic …) when the area defines a 'groups' map.
		if (!empty($area['groups']) && is_array($area['groups'])) {
			$rendered = array();
			foreach ($area['groups'] as $groupLabel => $groupTabKeys) {
				$groupItems = array();
				foreach ((array) $groupTabKeys as $tabKey) {
					if (isset($visibleTabs[$tabKey])) {
						$groupItems[$tabKey] = $visibleTabs[$tabKey];
					}
				}
				if (empty($groupItems)) {
					continue;
				}
				echo '<li class="epc-erp-sidebar-subhead" aria-hidden="true">' . epc_erp_h((string) $groupLabel) . '</li>';
				foreach ($groupItems as $tabKey => $meta) {
					echo epc_erp_render_sidebar_item($erpUrl, $areaKey, $tabKey, $meta, $activeTab, $from, $to);
					$rendered[$tabKey] = true;
				}
			}
			// Any visible tab not covered by a group falls under "More".
			$leftover = array_diff_key($visibleTabs, $rendered);
			if (!empty($leftover)) {
				echo '<li class="epc-erp-sidebar-subhead" aria-hidden="true">More</li>';
				foreach ($leftover as $tabKey => $meta) {
					echo epc_erp_render_sidebar_item($erpUrl, $areaKey, $tabKey, $meta, $activeTab, $from, $to);
				}
			}
		} else {
			foreach ($visibleTabs as $tabKey => $meta) {
				echo epc_erp_render_sidebar_item($erpUrl, $areaKey, $tabKey, $meta, $activeTab, $from, $to);
			}
		}
		echo '</ul></li>';
	}
	echo '</ul></nav>';
}

/** Render a single sidebar sub-module <li>. */
function epc_erp_render_sidebar_item($erpUrl, $areaKey, $tabKey, array $meta, $activeTab, $from, $to)
{
	if (!empty($meta['external']) || $tabKey === 'procurement_link') {
		return '<li class="epc-erp-sidebar-item epc-erp-sidebar-item--external">'
			. '<a href="' . epc_erp_h(epc_erp_procurement_url()) . '" target="_blank" rel="noopener">'
			. '<i class="fa fa-external-link"></i> ' . epc_erp_h(epc_erp_nav_label_plain($meta['label'] ?? 'Procurement')) . '</a></li>';
	}
	$hrefTab = ($tabKey === 'bank_recon') ? 'cash_bank' : $tabKey;
	$isActive = ($activeTab === $tabKey)
		|| ($tabKey === 'bank_recon' && $activeTab === 'cash_bank' && !empty($_GET['account_id']));
	$lbl = !empty($meta['raw']) ? $meta['label'] : epc_erp_h(epc_erp_nav_label_plain($meta['label']));
	$out = '<li class="epc-erp-sidebar-item' . ($isActive ? ' is-active' : '') . '">';
	$out .= '<a href="' . epc_erp_h(epc_erp_tab_url($erpUrl, $hrefTab, $from, $to, $areaKey)) . '"' . epc_erp_nav_shell_link_attrs() . '>';
	if (empty($meta['raw'])) {
		$out .= '<i class="fa ' . epc_erp_h($meta['icon']) . '"></i> ';
	}
	$out .= $lbl . '</a></li>';
	return $out;
}

function epc_erp_render_content_header($erpUrl, $activeArea, $activeTab, $from, $to)
{
	$areas = epc_erp_nav_areas_for_tenant();
	$areaLabel = isset($areas[$activeArea]['label'])
		? epc_erp_nav_label_plain($areas[$activeArea]['label'])
		: ucfirst($activeArea);
	$areaDesc = isset($areas[$activeArea]['desc']) ? $areas[$activeArea]['desc'] : '';
	$tabLabel = epc_erp_nav_tab_label($activeArea, $activeTab);
	if ($activeTab === 'bank_recon' || ($activeTab === 'cash_bank' && !empty($_GET['account_id']))) {
		$tabLabel = 'Bank recon';
	}
	$tabIcon = 'fa-folder-open-o';
	if (isset($areas[$activeArea]['tabs'][$activeTab]['icon'])) {
		$tabIcon = $areas[$activeArea]['tabs'][$activeTab]['icon'];
	} elseif ($activeTab === 'bank_recon' || ($activeTab === 'cash_bank' && !empty($_GET['account_id']))) {
		$tabIcon = 'fa-check-square';
	}
	$dashUrl = epc_erp_h(epc_erp_tab_url($erpUrl, 'dashboard', $from, $to, 'overview'));
	echo '<div class="epc-erp-content-header">';
	// Company (legal-entity) picker, top-right of the header.
	$companyPicker = '';
	if (isset($GLOBALS['db_link']) && $GLOBALS['db_link'] instanceof PDO) {
		if (!function_exists('epc_erp_company_picker_html')) {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
		}
		try {
			$companyPicker = epc_erp_company_picker_html($GLOBALS['db_link']);
		} catch (Throwable $e) {
			$companyPicker = '';
		}
	}
	if ($companyPicker !== '') {
		echo '<div class="epc-erp-company-scope" style="float:right;margin-top:2px;">' . $companyPicker . '</div>';
	}
	echo '<nav class="epc-erp-breadcrumb" aria-label="Breadcrumb">';
	echo '<a href="' . $dashUrl . '"' . epc_erp_nav_shell_link_attrs() . '>ERP</a>';
	echo ' <span class="sep">/</span> <span>' . epc_erp_h($areaLabel) . '</span>';
	echo ' <span class="sep">/</span> <span class="epc-erp-breadcrumb-current">' . epc_erp_h($tabLabel) . '</span>';
	echo '</nav>';
	echo '<h1 class="epc-erp-content-title"><i class="fa ' . epc_erp_h($tabIcon) . '"></i> ';
	echo epc_erp_h($tabLabel) . '</h1>';
	if ($areaDesc !== '') {
		echo '<p class="epc-erp-content-subtitle">' . epc_erp_h($areaDesc) . '</p>';
	}
	echo '</div>';
}

/** @deprecated Horizontal pills removed — use epc_erp_render_sidebar_nav */
function epc_erp_render_area_nav($erpUrl, $activeArea, $activeTab, $from, $to, array $allowedTabs)
{
}

/** @deprecated Sub-nav merged into left sidebar */
function epc_erp_render_area_subnav($erpUrl, $activeArea, $activeTab, $from, $to, array $allowedTabs)
{
}

function epc_erp_render_notifications_stub(PDO $db)
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
	$n = epc_erp_notifications_unread_count($db);
	echo '<div class="epc-erp-notify-stub dropdown">';
	echo '<button type="button" class="btn btn-default btn-xs dropdown-toggle" data-toggle="dropdown" title="Notifications">';
	echo '<i class="fa fa-bell"></i>';
	if ($n > 0) {
		echo ' <span class="badge">' . (int) $n . '</span>';
	}
	echo '</button>';
	echo '<ul class="dropdown-menu dropdown-menu-right epc-erp-notify-menu">';
	$items = epc_erp_notifications_list($db, 8);
	if (empty($items)) {
		echo '<li class="text-muted" style="padding:10px 14px;">No notifications yet.</li>';
	} else {
		foreach ($items as $it) {
			echo '<li><a href="#"><strong>' . epc_erp_h($it['title']) . '</strong><br><small>' . epc_erp_h($it['body']) . '</small></a></li>';
		}
	}
	echo '<li class="divider"></li><li class="text-center"><small class="text-muted">Notification centre (stub)</small></li>';
	echo '</ul></div>';
}
