<?php
/**
 * ERP area groups — left sidebar navigation (Dolibarr-inspired layout).
 */
defined('_ASTEXE_') or die('No access');

function epc_erp_nav_areas_config()
{
	return array(
		'overview' => array(
			'label' => 'Overview',
			'icon' => 'fa-th-large',
			'desc' => 'Dashboard and cross-department workflow',
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
			'label' => 'Sales',
			'icon' => 'fa-line-chart',
			'desc' => 'CRM, orders, revenue and fulfilment',
			'tabs' => array(
				'crm' => array('label' => 'CRM', 'icon' => 'fa-handshake-o', 'raw' => true),
				'proposals' => array('label' => 'Proposals', 'icon' => 'fa-file-text'),
				'sales_orders' => array('label' => 'Sales orders', 'icon' => 'fa-shopping-cart'),
				'ar_setup' => array('label' => 'A/R setup', 'icon' => 'fa-handshake-o'),
				'revenue' => array('label' => 'Revenue', 'icon' => 'fa-money'),
				'subscriptions' => array('label' => 'Subscriptions', 'icon' => 'fa-refresh'),
				'receivables' => array('label' => 'Receivables', 'icon' => 'fa-users'),
				'collections' => array('label' => 'Collections', 'icon' => 'fa-gavel'),
				'fulfilment' => array('label' => 'Fulfilment', 'icon' => 'fa-random'),
				'delivery_notes' => array('label' => 'Delivery notes', 'icon' => 'fa-truck'),
				'invoices' => array('label' => 'Invoices (e-invoice)', 'icon' => 'fa-file-text-o'),
			),
			'groups' => array(
				'Common' => array('crm', 'receivables', 'collections'),
				'Journals' => array('proposals', 'sales_orders', 'delivery_notes', 'invoices', 'subscriptions'),
				'Inquiries and reports' => array('revenue', 'fulfilment'),
				'Setup' => array('ar_setup'),
			),
		),
		'purchasing' => array(
			'label' => 'Purchasing',
			'icon' => 'fa-shopping-basket',
			'desc' => 'Suppliers, RFQ, POs and payables',
			'tabs' => array(
				'purchases' => array('label' => 'Purchases', 'icon' => 'fa-file-text-o'),
				'payables' => array('label' => 'Payables', 'icon' => 'fa-truck'),
				'rfq' => array('label' => 'RFQ', 'icon' => 'fa-envelope-o'),
				'purchase_orders' => array('label' => 'Purchase orders', 'icon' => 'fa-clipboard'),
				'three_way_match' => array('label' => '3-way match', 'icon' => 'fa-check-square-o'),
				'supplier_portal' => array('label' => 'Supplier portal', 'icon' => 'fa-handshake-o'),
				'ap_setup' => array('label' => 'A/P setup', 'icon' => 'fa-credit-card'),
				'landed_cost' => array('label' => 'Landed cost', 'icon' => 'fa-ship'),
				'procurement_link' => array('label' => 'Procurement CP', 'icon' => 'fa-external-link', 'external' => true),
			),
			'groups' => array(
				'Common' => array('payables', 'supplier_portal'),
				'Journals' => array('rfq', 'purchase_orders', 'purchases', 'three_way_match'),
				'Periodic' => array('landed_cost'),
				'Setup' => array('ap_setup'),
				'Links' => array('procurement_link'),
			),
		),
		'retail' => array(
			'label' => 'Retail &amp; Commerce',
			'icon' => 'fa-shopping-cart',
			'desc' => 'Channels, assortments, retail pricing, POS and statements',
			'tabs' => array(
				'retail_commerce' => array('label' => 'Retail &amp; Commerce', 'icon' => 'fa-shopping-cart'),
			),
			'groups' => array(
				'Common' => array('retail_commerce'),
			),
		),
		'finance' => array(
			'label' => 'Finance',
			'icon' => 'fa-university',
			'desc' => 'General ledger, tax, compliance and period close',
			'tabs' => array(
				'gl' => array('label' => 'General ledger', 'icon' => 'fa-book'),
				'coa' => array('label' => 'COA', 'icon' => 'fa-list'),
				'opening_balances' => array('label' => 'Opening balances', 'icon' => 'fa-flag-o'),
				'aging' => array('label' => 'Aging (AR/AP/Inv)', 'icon' => 'fa-hourglass-half'),
				'vat_return' => array('label' => 'UAE VAT', 'icon' => 'fa-percent'),
				'tax_compliance' => array('label' => 'Tax compliance', 'icon' => 'fa-gavel'),
				'vat_refund' => array('label' => 'Tourist VAT refunds', 'icon' => 'fa-plane'),
				'compliance' => array('label' => 'Compliance center', 'icon' => 'fa-shield'),
				'einvoice' => array('label' => 'E-Invoicing', 'icon' => 'fa-file-code-o'),
				'document_control' => array('label' => 'Document Control', 'icon' => 'fa-print'),
				'fin_advanced' => array('label' => 'Financial depth', 'icon' => 'fa-sliders'),
				'year_end' => array('label' => 'Year-end closing', 'icon' => 'fa-calendar-check-o'),
			),
			'groups' => array(
				'Common' => array('gl'),
				'Journals' => array('opening_balances'),
				'Inquiries and reports' => array('aging'),
				'Periodic' => array('fin_advanced', 'year_end'),
				'Tax and compliance' => array('vat_return', 'tax_compliance', 'vat_refund', 'compliance', 'einvoice'),
				'Setup' => array('coa', 'document_control'),
			),
		),
		'banking' => array(
			'label' => 'Cash &amp; Bank Management',
			'icon' => 'fa-money',
			'desc' => 'Bank accounts, cash, payments and reconciliation',
			'tabs' => array(
				'cash_bank' => array('label' => 'Cash & bank', 'icon' => 'fa-university'),
				'petty_cash' => array('label' => 'Petty cash', 'icon' => 'fa-money'),
				'payment_batches' => array('label' => 'Payment batches', 'icon' => 'fa-send'),
				'bank_recon' => array('label' => 'Bank recon', 'icon' => 'fa-check-square'),
				'bank_setup' => array('label' => 'Bank account', 'icon' => 'fa-university'),
			),
			'groups' => array(
				'Common' => array('cash_bank', 'petty_cash'),
				'Journals' => array('payment_batches'),
				'Inquiries and reports' => array('bank_recon'),
				'Setup' => array('bank_setup'),
			),
		),
		'operations' => array(
			'label' => 'Operations',
			'icon' => 'fa-cubes',
			'desc' => 'Inventory, assets and manufacturing',
			'tabs' => array(
				'inventory' => array('label' => 'Inventory', 'icon' => 'fa-cubes'),
				'wms' => array('label' => 'Advanced WMS', 'icon' => 'fa-cubes'),
				'product_info' => array('label' => 'Product information', 'icon' => 'fa-cube'),
				'inv_groups' => array('label' => 'Inventory (stock/groups)', 'icon' => 'fa-object-group'),
				'master_planning' => array('label' => 'Master planning', 'icon' => 'fa-random'),
				'order_planning' => array('label' => 'Order planning', 'icon' => 'fa-cubes'),
				'retail_barcode' => array('label' => 'Retail barcode', 'icon' => 'fa-barcode'),
				'fixed_assets' => array('label' => 'Fixed assets', 'icon' => 'fa-building'),
				'manufacturing' => array('label' => 'Manufacturing', 'icon' => 'fa-cogs'),
				'mfg_planning' => array('label' => 'Manufacturing planning', 'icon' => 'fa-cogs'),
				'cost_models' => array('label' => 'Costing value-models', 'icon' => 'fa-balance-scale'),
				'quality' => array('label' => 'Quality management', 'icon' => 'fa-check-circle'),
			),
			'groups' => array(
				'Common' => array('inventory', 'wms', 'product_info', 'inv_groups', 'order_planning'),
				'Journals' => array('manufacturing', 'quality'),
				'Periodic' => array('master_planning', 'mfg_planning', 'cost_models'),
				'Setup' => array('retail_barcode', 'fixed_assets'),
			),
		),
		'custom_shipping' => array(
			'label' => 'Custom & Shipping',
			'icon' => 'fa-ship',
			'desc' => 'UAE customs declarations, transit, and logistics documentation',
			'tabs' => array(
				'custom_shipping' => array('label' => 'Dashboard', 'icon' => 'fa-dashboard'),
			),
		),
		'people' => array(
			'label' => 'People',
			'icon' => 'fa-users',
			'desc' => 'HR, payroll and staff',
			'tabs' => array(
				'hr' => array('label' => 'HR', 'icon' => 'fa-user-circle'),
				'hr_ops' => array('label' => 'HR operations', 'icon' => 'fa-users'),
				'hr_law' => array('label' => 'Labour law &amp; compliance', 'icon' => 'fa-gavel'),
				'payroll' => array('label' => 'Payroll', 'icon' => 'fa-money'),
				'staff' => array('label' => 'Staff', 'icon' => 'fa-id-badge'),
				'expense_reports' => array('label' => 'Expenses', 'icon' => 'fa-credit-card'),
			),
			'groups' => array(
				'Common' => array('staff', 'hr', 'hr_ops'),
				'Compliance' => array('hr_law'),
				'Journals' => array('payroll', 'expense_reports'),
			),
		),
		'insights' => array(
			'label' => 'Insights',
			'icon' => 'fa-bar-chart',
			'desc' => 'Reports, marketing and knowledge',
			'tabs' => array(
				'pl' => array('label' => 'P&amp;L', 'icon' => 'fa-bar-chart'),
				'balance_sheet' => array('label' => 'Balance sheet', 'icon' => 'fa-balance-scale'),
				'reports' => array('label' => 'Reports', 'icon' => 'fa-table'),
				'marketing' => array('label' => 'Marketing', 'icon' => 'fa-bullhorn'),
				'knowledge_base' => array('label' => 'Knowledge base', 'icon' => 'fa-book'),
				'multi_entity' => array('label' => 'Multi-entity', 'icon' => 'fa-sitemap'),
				'ai_advisor' => array('label' => 'AI advisor', 'icon' => 'fa-magic'),
				'enterprise_reports' => array('label' => 'Trial balance / reports', 'icon' => 'fa-table'),
				'consolidation_bu' => array('label' => 'Consolidation', 'icon' => 'fa-sitemap'),
				'audit' => array('label' => 'Audit trail', 'icon' => 'fa-history'),
			),
			'groups' => array(
				'Reports' => array('pl', 'balance_sheet', 'reports', 'enterprise_reports'),
				'Intelligence' => array('ai_advisor'),
				'Inquiries' => array('consolidation_bu', 'multi_entity', 'audit'),
				'Common' => array('marketing', 'knowledge_base'),
			),
		),
		'collaboration' => array(
			'label' => 'Collaboration',
			'icon' => 'fa-calendar',
			'desc' => 'Agenda, contacts and documents',
			'tabs' => array(
				'agenda' => array('label' => 'Agenda', 'icon' => 'fa-calendar'),
				'projects' => array('label' => 'Projects', 'icon' => 'fa-tasks'),
				'project_accounting' => array('label' => 'Project accounting', 'icon' => 'fa-pie-chart'),
				'contacts' => array('label' => 'Contacts', 'icon' => 'fa-address-book-o'),
				'documents' => array('label' => 'Documents', 'icon' => 'fa-folder-open-o'),
				'contracts' => array('label' => 'Contracts &amp; e-sign', 'icon' => 'fa-file-text-o'),
				'doc_formats' => array('label' => 'Document formats', 'icon' => 'fa-files-o'),
			),
			'groups' => array(
				'Common' => array('agenda', 'projects', 'contacts', 'documents', 'contracts'),
				'Periodic' => array('project_accounting'),
				'Setup' => array('doc_formats'),
			),
		),
		'risk' => array(
			'label' => 'Risk &amp; Insurance',
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
			'label' => 'Enterprise',
			'icon' => 'fa-building-o',
			'desc' => 'Business units, financial dimensions, budgeting and listings',
			'tabs' => array(
				'business_units' => array('label' => 'Business unit', 'icon' => 'fa-sitemap'),
				'listing' => array('label' => 'Listing', 'icon' => 'fa-list-alt'),
				'budgeting' => array('label' => 'Budgeting', 'icon' => 'fa-pie-chart'),
			),
			'groups' => array(
				'Setup' => array('business_units', 'listing'),
				'Periodic' => array('budgeting'),
			),
		),
		'regrep' => array(
			'label' => 'External Reporting',
			'icon' => 'fa-file-text-o',
			'desc' => 'Statutory &amp; regulatory reports — country-driven, auto-formatted',
			'tabs' => array(
				'ext_reports' => array('label' => 'Report centre', 'icon' => 'fa-file-text-o'),
			),
			'groups' => array(
				'Reports' => array('ext_reports'),
			),
		),
		'administration' => array(
			'label' => 'Administration',
			'icon' => 'fa-shield',
			'desc' => 'Organization administration, security roles and platform / cross-cutting services',
			'tabs' => array(
				'org_admin' => array('label' => 'Organization administration', 'icon' => 'fa-sitemap'),
				'security_roles' => array('label' => 'Security roles', 'icon' => 'fa-shield'),
				'platform' => array('label' => 'Platform services', 'icon' => 'fa-cogs'),
			),
			'groups' => array(
				'Organization' => array('org_admin'),
				'Security' => array('security_roles'),
				'Platform' => array('platform'),
			),
		),
		'setup' => array(
			'label' => 'Setup &amp; Data',
			'icon' => 'fa-sliders',
			'desc' => 'Company, number sequences, valuation method and data import',
			'tabs' => array(
				'erp_setup' => array('label' => 'Accounting setup', 'icon' => 'fa-cogs'),
				'data_import' => array('label' => 'Data import', 'icon' => 'fa-upload'),
				'integration' => array('label' => 'Data &amp; integration', 'icon' => 'fa-plug'),
			),
			'groups' => array(
				'Setup' => array('erp_setup', 'data_import'),
				'Integration' => array('integration'),
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
