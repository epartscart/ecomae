<?php
/**
 * ERP UI helpers — page chrome, stat cards, grouped navigation.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_helpers.php';
require_once __DIR__ . '/epc_erp_dimensions.php';

function epc_erp_all_tabs_config()
{
	return array(
		'dashboard' => array('label' => 'Dashboard', 'icon' => 'fa-dashboard', 'group' => 'finance'),
		'sales_orders' => array('label' => 'Sales orders', 'icon' => 'fa-shopping-cart', 'group' => 'finance'),
		'invoices' => array('label' => 'Invoices (e-invoice)', 'icon' => 'fa-file-text-o', 'group' => 'finance'),
		'revenue' => array('label' => 'Revenue', 'icon' => 'fa-line-chart', 'group' => 'finance'),
		'receivables' => array('label' => 'Receivables', 'icon' => 'fa-users', 'group' => 'finance'),
		'payables' => array('label' => 'Payables', 'icon' => 'fa-truck', 'group' => 'finance'),
		'purchases' => array('label' => 'Purchases', 'icon' => 'fa-file-text-o', 'group' => 'finance'),
		'rfq' => array('label' => 'RFQ / proposals', 'icon' => 'fa-envelope-o', 'group' => 'finance'),
		'cash_bank' => array('label' => 'Cash &amp; bank', 'icon' => 'fa-university', 'group' => 'finance'),
		'coa' => array('label' => 'COA', 'icon' => 'fa-list', 'group' => 'finance'),
		'gl' => array('label' => 'General ledger', 'icon' => 'fa-book', 'group' => 'finance'),
		'pl' => array('label' => 'P&amp;L', 'icon' => 'fa-bar-chart', 'group' => 'finance'),
		'balance_sheet' => array('label' => 'Balance sheet', 'icon' => 'fa-balance-scale', 'group' => 'finance'),
		'vat_return' => array('label' => 'UAE VAT', 'icon' => 'fa-percent', 'group' => 'finance'),
		'expense_reports' => array('label' => 'Expenses', 'icon' => 'fa-credit-card', 'group' => 'finance'),
		'fulfilment' => array('label' => 'Fulfilment', 'icon' => 'fa-random', 'group' => 'operations'),
		'inventory' => array('label' => 'Inventory', 'icon' => 'fa-cubes', 'group' => 'operations'),
		'fixed_assets' => array('label' => 'Fixed assets', 'icon' => 'fa-building', 'group' => 'operations'),
		'opening_balances' => array('label' => 'Opening balances', 'icon' => 'fa-flag-o', 'group' => 'operations'),
		'manufacturing' => array('label' => 'Manufacturing', 'icon' => 'fa-cogs', 'group' => 'operations'),
		'crm' => array('label' => '<i class="fa fa-handshake-o"></i> CRM', 'icon' => 'fa-handshake-o', 'group' => 'crm', 'raw_label' => true),
		'contacts' => array('label' => 'Contacts', 'icon' => 'fa-address-book-o', 'group' => 'crm'),
		'staff' => array('label' => 'Staff', 'icon' => 'fa-id-badge', 'group' => 'people'),
		'workflow' => array('label' => 'Workflow', 'icon' => 'fa-tasks', 'group' => 'people'),
		'hr' => array('label' => 'HR', 'icon' => 'fa-user-circle', 'group' => 'people'),
		'payroll' => array('label' => 'Payroll', 'icon' => 'fa-money', 'group' => 'people'),
		'einvoice' => array('label' => 'E-Invoicing', 'icon' => 'fa-file-code-o', 'group' => 'tools'),
		'marketing' => array('label' => 'Marketing', 'icon' => 'fa-bullhorn', 'group' => 'tools'),
		'reports' => array('label' => 'Reports', 'icon' => 'fa-table', 'group' => 'tools'),
		'documents' => array('label' => 'Documents', 'icon' => 'fa-folder-open-o', 'group' => 'tools'),
		'audit' => array('label' => 'Audit trail', 'icon' => 'fa-history', 'group' => 'tools'),
	);
}

function epc_erp_tab_groups_config()
{
	return array(
		'finance' => array('label' => 'Finance', 'icon' => 'fa-money'),
		'operations' => array('label' => 'Operations', 'icon' => 'fa-truck'),
		'crm' => array('label' => 'CRM', 'icon' => 'fa-handshake-o'),
		'people' => array('label' => 'People', 'icon' => 'fa-users'),
		'tools' => array('label' => 'Tools', 'icon' => 'fa-wrench'),
	);
}

function epc_erp_tab_label($tabKey)
{
	$cfg = epc_erp_all_tabs_config();
	if (!isset($cfg[$tabKey])) {
		return ucfirst(str_replace('_', ' ', $tabKey));
	}
	$row = $cfg[$tabKey];
	if (!empty($row['raw_label'])) {
		return $row['label'];
	}
	return $row['label'];
}

function epc_erp_render_tab_nav($erpUrl, $activeTab, $from, $to, array $allowedTabs)
{
	$tabsCfg = epc_erp_all_tabs_config();
	$groupsCfg = epc_erp_tab_groups_config();
	$byGroup = array();
	foreach ($groupsCfg as $gk => $_g) {
		$byGroup[$gk] = array();
	}
	foreach ($tabsCfg as $key => $row) {
		if (!in_array($key, $allowedTabs, true)) {
			continue;
		}
		$gk = isset($row['group']) ? $row['group'] : 'finance';
		if (!isset($byGroup[$gk])) {
			$byGroup[$gk] = array();
		}
		$byGroup[$gk][$key] = $row;
	}
	$activeGroup = 'finance';
	if (isset($tabsCfg[$activeTab]['group'])) {
		$activeGroup = $tabsCfg[$activeTab]['group'];
	}
	echo '<div class="epc-erp-nav-groups">';
	foreach ($groupsCfg as $gk => $gmeta) {
		if (empty($byGroup[$gk])) {
			continue;
		}
		$open = ($gk === $activeGroup);
		echo '<div class="epc-erp-nav-group' . ($open ? ' is-open' : '') . '" data-group="' . epc_erp_h($gk) . '">';
		echo '<button type="button" class="epc-erp-nav-group-hd" aria-expanded="' . ($open ? 'true' : 'false') . '">';
		echo '<i class="fa ' . epc_erp_h($gmeta['icon']) . '"></i> ' . epc_erp_h($gmeta['label']);
		echo ' <span class="epc-erp-nav-count">' . count($byGroup[$gk]) . '</span>';
		echo '</button>';
		echo '<div class="epc-erp-nav-group-body">';
		foreach ($byGroup[$gk] as $key => $row) {
			$cls = ($activeTab === $key) ? 'btn-primary' : 'btn-default';
			$lbl = !empty($row['raw_label']) ? $row['label'] : epc_erp_h($row['label']);
			echo '<a class="btn btn-sm ' . $cls . '" href="' . epc_erp_h(epc_erp_tab_url($erpUrl, $key, $from, $to)) . '">' . $lbl . '</a>';
		}
		echo '</div></div>';
	}
	echo '</div>';
}

/**
 * @param string $title
 * @param string $subtitle
 * @param array $breadcrumbs [ ['label'=>'', 'url'=>''] ]
 * @param array $actions [ ['label'=>'', 'url'=>'', 'class'=>'btn-primary', 'icon'=>'fa-plus'] ]
 */
function erp_page_header($title, $subtitle = '', $breadcrumbs = array(), $actions = array())
{
	echo '<div class="epc-erp-page-hd">';
	echo '<div class="epc-erp-page-hd-main">';
	if (!empty($breadcrumbs)) {
		echo '<nav class="epc-erp-breadcrumb" aria-label="Breadcrumb">';
		$parts = array();
		foreach ($breadcrumbs as $i => $bc) {
			$lbl = epc_erp_h($bc['label'] ?? '');
			if ($i < count($breadcrumbs) - 1 && !empty($bc['url'])) {
				$parts[] = '<a href="' . epc_erp_h($bc['url']) . '">' . $lbl . '</a>';
			} else {
				$parts[] = '<span>' . $lbl . '</span>';
			}
		}
		echo implode(' <span class="sep">/</span> ', $parts);
		echo '</nav>';
	}
	echo '<h3 class="epc-erp-page-title">' . $title . '</h3>';
	if ($subtitle !== '') {
		echo '<p class="epc-erp-page-sub">' . $subtitle . '</p>';
	}
	echo '</div>';
	if (!empty($actions)) {
		echo '<div class="epc-erp-page-actions">';
		foreach ($actions as $act) {
			$cls = epc_erp_h($act['class'] ?? 'btn-default');
			$icon = !empty($act['icon']) ? '<i class="fa ' . epc_erp_h($act['icon']) . '"></i> ' : '';
			if (!empty($act['url'])) {
				echo '<a class="btn btn-sm ' . $cls . '" href="' . epc_erp_h($act['url']) . '">' . $icon . epc_erp_h($act['label'] ?? '') . '</a>';
			} elseif (!empty($act['id'])) {
				echo '<button type="button" class="btn btn-sm ' . $cls . '" id="' . epc_erp_h($act['id']) . '">' . $icon . epc_erp_h($act['label'] ?? '') . '</button>';
			}
		}
		echo '</div>';
	}
	echo '</div>';
}

/**
 * @param array $cards [ ['label'=>'', 'value'=>'', 'hint'=>'', 'class'=>'green'] ]
 */
function erp_stat_cards(array $cards)
{
	if (empty($cards)) {
		return;
	}
	echo '<div class="epc-erp-kpi epc-erp-stat-row">';
	foreach ($cards as $c) {
		$valCls = !empty($c['class']) ? ' ' . epc_erp_h($c['class']) : '';
		echo '<div class="kpi">';
		echo '<div class="lbl">' . epc_erp_h($c['label'] ?? '') . '</div>';
		echo '<div class="val' . $valCls . '">' . ($c['value_html'] ?? epc_erp_h($c['value'] ?? '')) . '</div>';
		if (!empty($c['hint'])) {
			echo '<div class="hint">' . epc_erp_h($c['hint']) . '</div>';
		}
		echo '</div>';
	}
	echo '</div>';
}

function erp_section_card($title, $bodyHtml, $options = array())
{
	$icon = !empty($options['icon']) ? '<i class="fa ' . epc_erp_h($options['icon']) . '"></i> ' : '';
	$extra = !empty($options['header_html']) ? $options['header_html'] : '';
	echo '<div class="epc-erp-section-card">';
	echo '<div class="epc-erp-section-card-hd"><h4>' . $icon . epc_erp_h($title) . '</h4>' . $extra . '</div>';
	echo '<div class="epc-erp-section-card-bd">' . $bodyHtml . '</div>';
	echo '</div>';
}

function erp_filter_bar($erpUrl, $tab, $from, $to, $extraFieldsHtml = '')
{
	echo '<form method="get" class="epc-erp-filter-bar form-inline">';
	echo '<input type="hidden" name="tab" value="' . epc_erp_h($tab) . '">';
	echo '<label>From</label> <input type="date" name="from" class="form-control input-sm" value="' . epc_erp_h($from) . '">';
	echo '<label>To</label> <input type="date" name="to" class="form-control input-sm" value="' . epc_erp_h($to) . '">';
	echo $extraFieldsHtml;
	echo '<button type="submit" class="btn btn-default btn-sm"><i class="fa fa-filter"></i> Apply</button>';
	echo '</form>';
}

function erp_empty_state($message, $icon = 'fa-inbox')
{
	echo '<div class="epc-erp-empty"><i class="fa ' . epc_erp_h($icon) . '"></i><p>' . epc_erp_h($message) . '</p></div>';
}

function erp_table_open($headers, $tableClass = 'table table-striped table-bordered table-condensed table-epc epc-erp-table')
{
	echo '<div class="table-responsive epc-erp-table-wrap"><table class="' . epc_erp_h($tableClass) . '"><thead><tr>';
	foreach ($headers as $h) {
		echo '<th>' . $h . '</th>';
	}
	echo '</tr></thead><tbody>';
}

function erp_table_close()
{
	echo '</tbody></table></div>';
}

/**
 * ERP dashboard quick-action tiles — same pattern as CP tenant/Super CP dashboards.
 *
 * @return array<int, array{label:string,icon:string,url:string,tone:string,hint:string}>
 */
function epc_erp_dashboard_quick_links($erpUrl, $from, $to, $guideUrl, array $allowedTabs)
{
	if (!function_exists('epc_erp_tab_url')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/' . ($GLOBALS['DP_Config']->backend_dir ?? 'cp')
			. '/content/shop/finance/erp/erp_nav_areas.php';
	}
	if (!function_exists('epc_erp_shell_append_query')) {
		require_once __DIR__ . '/epc_erp_cp_shell.php';
	}
	if (!function_exists('epc_erp_has_commerce_integration')) {
		require_once __DIR__ . '/epc_erp_vouchers.php';
	}

	$candidates = array(
		array('label' => 'Sales & CRM', 'icon' => 'fa-handshake-o', 'tab' => 'crm', 'area' => 'sales', 'tone' => 'clients', 'hint' => 'Leads, pipeline & opportunities'),
		array(
			'label' => 'Sales orders',
			'icon' => 'fa-shopping-cart',
			'tab' => 'sales_orders',
			'area' => 'sales',
			'tone' => 'orders',
			'hint' => function_exists('epc_erp_has_commerce_integration') && !epc_erp_has_commerce_integration()
				? 'Direct SO → SI workflow'
				: 'Quotations & fulfilment',
		),
		array('label' => 'Revenue', 'icon' => 'fa-line-chart', 'tab' => 'revenue', 'area' => 'sales', 'tone' => 'prices', 'hint' => 'Completed-order sales'),
		array('label' => 'Finance & treasury', 'icon' => 'fa-university', 'tab' => 'cash_bank', 'area' => 'finance', 'tone' => 'finance', 'hint' => 'Cash, bank & payments'),
		array('label' => 'General ledger', 'icon' => 'fa-book', 'tab' => 'gl', 'area' => 'finance', 'tone' => 'finance', 'hint' => 'Journals, COA & postings'),
		array('label' => 'Purchases & AP', 'icon' => 'fa-truck', 'tab' => 'payables', 'area' => 'purchasing', 'tone' => 'warehouse', 'hint' => 'Suppliers & payables'),
		array('label' => 'Inventory', 'icon' => 'fa-cubes', 'tab' => 'inventory', 'area' => 'operations', 'tone' => 'warehouse', 'hint' => 'Stock, warehouses & moves'),
		array('label' => 'HR & payroll', 'icon' => 'fa-users', 'tab' => 'hr', 'area' => 'people', 'tone' => 'clients', 'hint' => 'Employees, leave & payroll'),
		array('label' => 'UAE VAT', 'icon' => 'fa-percent', 'tab' => 'vat_return', 'area' => 'finance', 'tone' => 'finance', 'hint' => 'FTA return & compliance'),
		array('label' => 'Reports', 'icon' => 'fa-table', 'tab' => 'reports', 'area' => 'insights', 'tone' => 'docs', 'hint' => 'P&L, balance sheet & exports'),
		array('label' => 'Workflow', 'icon' => 'fa-tasks', 'tab' => 'workflow', 'area' => 'overview', 'tone' => 'platform', 'hint' => 'Cross-department board'),
		array('label' => 'E-Invoicing', 'icon' => 'fa-file-code-o', 'tab' => 'einvoice', 'area' => 'finance', 'tone' => 'platform', 'hint' => 'FTA Peppol & XML'),
		array('label' => 'Customs & shipping', 'icon' => 'fa-ship', 'tab' => 'custom_shipping', 'area' => 'custom_shipping', 'tone' => 'platform', 'hint' => 'UAE declarations & transit'),
		array('label' => 'ERP guide', 'icon' => 'fa-book', 'tab' => '', 'tone' => 'docs', 'hint' => 'Capability guides & help', 'guide' => true),
	);

	$links = array();
	foreach ($candidates as $row) {
		if (!empty($row['guide'])) {
			$url = epc_erp_shell_append_query((string) $guideUrl);
			$links[] = array(
				'label' => $row['label'],
				'icon' => $row['icon'],
				'url' => $url,
				'tone' => $row['tone'],
				'hint' => $row['hint'],
			);
			continue;
		}
		$tab = (string) ($row['tab'] ?? '');
		if ($tab === '') {
			continue;
		}
		if (function_exists('epc_erp_nav_tab_allowed')) {
			if (!epc_erp_nav_tab_allowed($tab, $allowedTabs)) {
				continue;
			}
		} elseif (!in_array($tab, $allowedTabs, true)) {
			continue;
		}
		$area = (string) ($row['area'] ?? '');
		$url = epc_erp_tab_url($erpUrl, $tab, $from, $to, $area);
		$links[] = array(
			'label' => $row['label'],
			'icon' => $row['icon'],
			'url' => $url,
			'tone' => $row['tone'],
			'hint' => $row['hint'],
		);
	}
	return $links;
}

/**
 * Render CP-style quick actions grid for ERP dashboard.
 */
function epc_erp_render_dashboard_quick_actions(array $quickLinks)
{
	if (empty($quickLinks)) {
		return;
	}
	echo '<div class="epc-erp-dashboard-quick epc-scp-dashboard-quick">';
	echo '<h3 class="epc-scp-section-title"><i class="fa fa-bolt"></i> Quick actions</h3>';
	echo '<div class="epc-scp-quick-grid">';
	foreach ($quickLinks as $link) {
		$tone = epc_erp_h($link['tone'] ?? 'platform');
		$hint = trim((string) ($link['hint'] ?? ''));
		echo '<a class="epc-scp-quick-card epc-cp-card epc-scp-quick-card--' . $tone . '" href="'
			. epc_erp_h($link['url'] ?? '#') . '"';
		if ($hint !== '') {
			echo ' title="' . epc_erp_h($hint) . '"';
		}
		echo '>';
		echo '<span class="epc-scp-quick-card__icon"><i class="fa ' . epc_erp_h($link['icon'] ?? 'fa-link') . '"></i></span>';
		echo '<span class="epc-scp-quick-card__label">' . epc_erp_h($link['label'] ?? '') . '</span>';
		if ($hint !== '') {
			echo '<span class="epc-scp-quick-card__hint">' . epc_erp_h($hint) . '</span>';
		}
		echo '</a>';
	}
	echo '</div></div>';
}
