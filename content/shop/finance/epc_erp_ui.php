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
 * ──────────────────────────────────────────────────────────────────────────
 * D365 Finance & Operations entry-module chrome
 *
 * Reusable building blocks that give our ERP entry modules (Sales order,
 * Purchase order, Inventory, Receivables, Payables, General journal) the
 * Dynamics 365 F&O look & feel: a command Action Pane, collapsible FastTabs,
 * status pills, dense data grids and a FactBox rail. These are pure
 * presentation helpers — they emit markup only and never touch business logic.
 * All D365 styling is scoped under the `.epc-erp-d365` wrapper that the ERP
 * workspace shell carries, so other CP modules are unaffected.
 * ──────────────────────────────────────────────────────────────────────────
 */

/**
 * Render a D365-style Action Pane (command ribbon).
 *
 * @param array $groups [ ['label'=>'Maintain', 'buttons'=>[
 *     ['label'=>'New','icon'=>'fa-plus','url'|'id'=>...,'class'=>'is-primary','target'=>'#anchor'],
 * ]] ]
 */
function erp_action_pane(array $groups)
{
	if (empty($groups)) {
		return;
	}
	echo '<div class="epc-d365-actionpane" role="toolbar">';
	foreach ($groups as $g) {
		$btns = isset($g['buttons']) && is_array($g['buttons']) ? $g['buttons'] : array();
		if (empty($btns)) {
			continue;
		}
		echo '<div class="epc-d365-ap-group">';
		echo '<div class="epc-d365-ap-buttons">';
		foreach ($btns as $b) {
			$lbl = epc_erp_h($b['label'] ?? '');
			$icon = !empty($b['icon']) ? '<i class="fa ' . epc_erp_h($b['icon']) . '"></i>' : '';
			$cls = 'epc-d365-ap-btn';
			if (!empty($b['class'])) {
				$cls .= ' ' . epc_erp_h($b['class']);
			}
			if (!empty($b['disabled'])) {
				echo '<span class="' . $cls . ' is-disabled" aria-disabled="true">' . $icon . '<span>' . $lbl . '</span></span>';
			} elseif (!empty($b['url'])) {
				echo '<a class="' . $cls . '" href="' . epc_erp_h($b['url']) . '">' . $icon . '<span>' . $lbl . '</span></a>';
			} else {
				$attrs = '';
				if (!empty($b['id'])) {
					$attrs .= ' id="' . epc_erp_h($b['id']) . '"';
				}
				if (!empty($b['target'])) {
					$attrs .= ' data-d365-target="' . epc_erp_h($b['target']) . '"';
				}
				echo '<button type="button" class="' . $cls . '"' . $attrs . '>' . $icon . '<span>' . $lbl . '</span></button>';
			}
		}
		echo '</div>';
		echo '<div class="epc-d365-ap-label">' . epc_erp_h($g['label'] ?? '') . '</div>';
		echo '</div>';
	}
	echo '</div>';
}

/**
 * Render the full D365 F&O two-row Action Pane ribbon: Row 1 = menu tabs that
 * switch Row 2 = the active tab's command groups (exactly like F&O).
 *
 * @param array $tabs [ ['label'=>'Sales order','key'=>'so','active'=>true,
 *     'groups'=>[ ['label'=>'New','buttons'=>[ ... ]] ]] ]
 */
function erp_action_pane_ribbon(array $tabs)
{
	$tabs = array_values(array_filter($tabs, function ($t) {
		return !empty($t['groups']);
	}));
	if (empty($tabs)) {
		return;
	}
	$hasActive = false;
	foreach ($tabs as $t) {
		if (!empty($t['active'])) {
			$hasActive = true;
			break;
		}
	}
	echo '<div class="epc-d365-ribbon">';
	// Row 1 — menu tabs
	echo '<div class="epc-d365-aptabs" role="tablist">';
	$i = 0;
	foreach ($tabs as $t) {
		$key = $t['key'] ?? ('apt' . $i);
		$active = !empty($t['active']) || (!$hasActive && $i === 0);
		echo '<button type="button" class="epc-d365-aptab' . ($active ? ' is-active' : '') . '"'
			. ' data-aptab="' . epc_erp_h($key) . '" role="tab" aria-selected="' . ($active ? 'true' : 'false') . '">'
			. epc_erp_h($t['label'] ?? '') . '</button>';
		$i++;
	}
	echo '</div>';
	// Row 2 — per-tab command groups (only active visible)
	$i = 0;
	foreach ($tabs as $t) {
		$key = $t['key'] ?? ('apt' . $i);
		$active = !empty($t['active']) || (!$hasActive && $i === 0);
		echo '<div class="epc-d365-ap-row' . ($active ? ' is-active' : '') . '" data-aptab="' . epc_erp_h($key) . '">';
		erp_action_pane($t['groups']);
		echo '</div>';
		$i++;
	}
	echo '</div>';
}

/**
 * Horizontal tab strip — used for the Lines|Header view toggle and the
 * line-detail secondary tab strip (General|Setup|Address|...). Tabs switch
 * sibling panels opened with erp_tabpanel_open() sharing the same $group key.
 *
 * @param array  $tabs [ ['label'=>'Lines','target'=>'#id','active'=>true,'icon'=>'fa-..'] ]
 * @param string $group unique group key
 * @param array  $opts ['variant'=>'view'|'sub']
 */
function erp_tabstrip(array $tabs, $group, array $opts = array())
{
	if (empty($tabs)) {
		return;
	}
	$variant = $opts['variant'] ?? 'view';
	$hasActive = false;
	foreach ($tabs as $t) {
		if (!empty($t['active'])) {
			$hasActive = true;
			break;
		}
	}
	echo '<div class="epc-d365-tabstrip epc-d365-tabstrip--' . epc_erp_h($variant) . '" role="tablist">';
	$i = 0;
	foreach ($tabs as $t) {
		$active = !empty($t['active']) || (!$hasActive && $i === 0);
		$tgt = $t['target'] ?? '';
		$icon = !empty($t['icon']) ? '<i class="fa ' . epc_erp_h($t['icon']) . '"></i> ' : '';
		echo '<button type="button" class="epc-d365-tab' . ($active ? ' is-active' : '') . '" role="tab"'
			. ' data-tabgroup="' . epc_erp_h($group) . '"'
			. ($tgt !== '' ? ' data-target="' . epc_erp_h($tgt) . '"' : '')
			. ' aria-selected="' . ($active ? 'true' : 'false') . '">'
			. $icon . epc_erp_h($t['label'] ?? '') . '</button>';
		$i++;
	}
	echo '</div>';
}

/**
 * Open a tab panel toggled by erp_tabstrip(). Must share the $group key with
 * the strip; $id matches the strip tab's target (without the leading '#').
 */
function erp_tabpanel_open($id, $group, $active = false)
{
	echo '<div class="epc-d365-tabpanel' . ($active ? ' is-active' : '') . '"'
		. ' id="' . epc_erp_h($id) . '" data-tabpanel="' . epc_erp_h($group) . '" role="tabpanel">';
}

function erp_tabpanel_close()
{
	echo '</div>';
}

/**
 * D365 list filter strip: saved-view selector, "Show" dropdown and a live
 * client-side quick filter. The selectors are presentational look-ups; the
 * quick filter hides non-matching rows of the table named in search.target.
 *
 * @param array $opts [
 *     'views'  => ['My view','All orders'],
 *     'show'   => ['label'=>'Show','options'=>['Open','Posted','All']],
 *     'search' => ['placeholder'=>'Filter','target'=>'#tableId'],
 *     'right'  => 'raw html',
 * ]
 */
function erp_list_toolbar(array $opts = array())
{
	echo '<div class="epc-d365-listbar">';
	echo '<div class="epc-d365-listbar-left">';
	if (!empty($opts['views']) && is_array($opts['views'])) {
		echo '<span class="epc-d365-view-sel"><i class="fa fa-th-list"></i> <select>';
		foreach ($opts['views'] as $v) {
			echo '<option>' . epc_erp_h($v) . '</option>';
		}
		echo '</select></span>';
	}
	if (!empty($opts['show']) && is_array($opts['show'])) {
		$s = $opts['show'];
		echo '<span class="epc-d365-show"><span class="lbl">' . epc_erp_h($s['label'] ?? 'Show') . ':</span> <select>';
		foreach (($s['options'] ?? array()) as $o) {
			echo '<option>' . epc_erp_h($o) . '</option>';
		}
		echo '</select></span>';
	}
	echo '</div>';
	echo '<div class="epc-d365-listbar-right">';
	if (!empty($opts['search']) && is_array($opts['search'])) {
		$se = $opts['search'];
		$ph = epc_erp_h($se['placeholder'] ?? 'Filter');
		$tgt = !empty($se['target']) ? ' data-quickfilter="' . epc_erp_h($se['target']) . '"' : '';
		echo '<span class="epc-d365-quickfilter"><i class="fa fa-search"></i>'
			. '<input type="text" placeholder="' . $ph . '"' . $tgt . ' autocomplete="off"></span>';
	}
	if (!empty($opts['right'])) {
		echo $opts['right'];
	}
	echo '</div>';
	echo '</div>';
}

/**
 * D365 status dot for the grid status column.
 */
function erp_status_dot($tone = 'muted')
{
	return '<span class="epc-d365-dot epc-d365-dot--' . epc_erp_h($tone) . '" aria-hidden="true"></span>';
}

/**
 * Open a D365 FastTab (collapsible section).
 *
 * @param string $title
 * @param array  $opts ['open'=>bool, 'summary'=>string, 'icon'=>'fa-...', 'id'=>string]
 */
function erp_fasttab_open($title, array $opts = array())
{
	$open = !array_key_exists('open', $opts) || !empty($opts['open']);
	$summary = trim((string) ($opts['summary'] ?? ''));
	$icon = !empty($opts['icon']) ? '<i class="fa ' . epc_erp_h($opts['icon']) . '"></i> ' : '';
	$idAttr = !empty($opts['id']) ? ' id="' . epc_erp_h($opts['id']) . '"' : '';
	echo '<section class="epc-d365-fasttab' . ($open ? ' is-open' : '') . '"' . $idAttr . '>';
	echo '<button type="button" class="epc-d365-ft-hd" aria-expanded="' . ($open ? 'true' : 'false') . '">';
	echo '<i class="fa fa-chevron-right epc-d365-ft-caret"></i>';
	echo '<span class="epc-d365-ft-title">' . $icon . epc_erp_h($title) . '</span>';
	if ($summary !== '') {
		echo '<span class="epc-d365-ft-summary">' . epc_erp_h($summary) . '</span>';
	}
	echo '</button>';
	echo '<div class="epc-d365-ft-bd">';
}

function erp_fasttab_close()
{
	echo '</div></section>';
}

/**
 * Derive a D365 tone (ok/info/warn/bad/muted) from a status word.
 */
function erp_status_tone($label)
{
	$k = strtolower(trim((string) $label));
	$map = array(
		'posted' => 'ok', 'paid' => 'ok', 'invoiced' => 'ok', 'confirmed' => 'info',
		'approved' => 'ok', 'completed' => 'ok', 'complete' => 'ok', 'active' => 'ok', 'received' => 'ok',
		'open' => 'info', 'draft' => 'muted', 'pending' => 'warn', 'on hold' => 'warn',
		'overdue' => 'bad', 'cancelled' => 'bad', 'canceled' => 'bad', 'rejected' => 'bad',
		'failed' => 'bad', 'due' => 'warn', 'closed' => 'muted', 'partial' => 'warn',
	);
	return $map[$k] ?? 'muted';
}

/**
 * D365 status pill. Tone auto-derived from common status words when omitted.
 */
function erp_status_pill($label, $tone = '')
{
	$label = (string) $label;
	if ($tone === '') {
		$tone = erp_status_tone($label);
	}
	return '<span class="epc-d365-pill epc-d365-pill--' . epc_erp_h($tone) . '">' . epc_erp_h($label) . '</span>';
}

/**
 * Open a FactBox rail. Wrap module content + the FactBox in erp_d365_layout_*.
 */
function erp_factbox_open($title, $icon = 'fa-info-circle')
{
	echo '<aside class="epc-d365-factbox">';
	echo '<div class="epc-d365-fb-hd"><i class="fa ' . epc_erp_h($icon) . '"></i> ' . epc_erp_h($title) . '</div>';
	echo '<div class="epc-d365-fb-bd">';
}

function erp_factbox_close()
{
	echo '</div></aside>';
}

/**
 * Render a list of label/value rows inside a FactBox body.
 *
 * @param array $rows [ ['label'=>'', 'value'=>'' , 'value_html'=>''] ]
 */
function erp_factbox_rows(array $rows)
{
	echo '<dl class="epc-d365-fb-list">';
	foreach ($rows as $r) {
		echo '<dt>' . epc_erp_h($r['label'] ?? '') . '</dt>';
		echo '<dd>' . ($r['value_html'] ?? epc_erp_h($r['value'] ?? '')) . '</dd>';
	}
	echo '</dl>';
}

/**
 * Print the FastTab / Action-pane interaction JS + a marker once per request.
 * Safe to call from every module; it self-guards against duplication.
 */
function erp_d365_assets()
{
	if (!empty($GLOBALS['__epc_d365_assets_done'])) {
		return;
	}
	$GLOBALS['__epc_d365_assets_done'] = true;
	echo "\n<script id=\"epc-d365-js\">(function(){\n";
	echo "function ready(fn){if(document.readyState!='loading'){fn();}else{document.addEventListener('DOMContentLoaded',fn);}}\n";
	echo "ready(function(){\n";
	echo "  document.addEventListener('click',function(e){\n";
	// FastTab header collapse/expand
	echo "    var hd=e.target.closest&&e.target.closest('.epc-d365-ft-hd');\n";
	echo "    if(hd){var ft=hd.parentNode;var open=ft.classList.toggle('is-open');hd.setAttribute('aria-expanded',open?'true':'false');return;}\n";
	// Action Pane Row-1 menu tab → swap Row-2 command groups
	echo "    var apt=e.target.closest&&e.target.closest('.epc-d365-aptab');\n";
	echo "    if(apt){var rb=apt.closest('.epc-d365-ribbon');if(rb){var k=apt.getAttribute('data-aptab');rb.querySelectorAll('.epc-d365-aptab').forEach(function(x){var on=x===apt;x.classList.toggle('is-active',on);x.setAttribute('aria-selected',on?'true':'false');});rb.querySelectorAll('.epc-d365-ap-row').forEach(function(r){r.classList.toggle('is-active',r.getAttribute('data-aptab')===k);});}return;}\n";
	// Generic tab strip (Lines|Header view toggle, line-detail subtabs) → swap panels in the group
	echo "    var tb=e.target.closest&&e.target.closest('.epc-d365-tab[data-target]');\n";
	echo "    if(tb){var grp=tb.getAttribute('data-tabgroup');var strip=tb.closest('.epc-d365-tabstrip');if(strip){strip.querySelectorAll('.epc-d365-tab').forEach(function(x){var on=x===tb;x.classList.toggle('is-active',on);x.setAttribute('aria-selected',on?'true':'false');});}if(grp){document.querySelectorAll('.epc-d365-tabpanel[data-tabpanel=\"'+grp+'\"]').forEach(function(p){p.classList.remove('is-active');});}var sel=tb.getAttribute('data-target');var t=sel&&document.querySelector(sel);if(t){t.classList.add('is-active');}return;}\n";
	// Sortable column header (client-side, presentational)
	echo "    var th=e.target.closest&&e.target.closest('.epc-erp-table th[data-sort]');\n";
	echo "    if(th){var table=th.closest('table');var row=th.parentNode;var idx=Array.prototype.indexOf.call(row.children,th);var dir=th.getAttribute('data-sortdir')==='asc'?'desc':'asc';row.querySelectorAll('th').forEach(function(h){h.removeAttribute('data-sortdir');h.classList.remove('is-sorted-asc','is-sorted-desc');});th.setAttribute('data-sortdir',dir);th.classList.add(dir==='asc'?'is-sorted-asc':'is-sorted-desc');var tbody=table.tBodies[0];if(!tbody)return;var rows=Array.prototype.slice.call(tbody.rows).filter(function(r){return !r.classList.contains('epc-d365-sumrow');});var num=th.getAttribute('data-sort')==='num';rows.sort(function(a,b){var x=(a.cells[idx]?a.cells[idx].textContent:'').trim();var y=(b.cells[idx]?b.cells[idx].textContent:'').trim();if(num){x=parseFloat(x.replace(/[^0-9.\\-]/g,''))||0;y=parseFloat(y.replace(/[^0-9.\\-]/g,''))||0;return dir==='asc'?x-y:y-x;}return dir==='asc'?x.localeCompare(y):y.localeCompare(x);});rows.forEach(function(r){tbody.appendChild(r);});return;}\n";
	// Action Pane button with a scroll target (opens its FastTab, scrolls + focuses)
	echo "    var ap=e.target.closest&&e.target.closest('.epc-d365-ap-btn[data-d365-target]');\n";
	echo "    if(ap){var sel=ap.getAttribute('data-d365-target');var t=sel&&document.querySelector(sel);if(t){var s=t.closest('.epc-d365-fasttab');if(s&&!s.classList.contains('is-open')){s.classList.add('is-open');var b=s.querySelector('.epc-d365-ft-hd');if(b){b.setAttribute('aria-expanded','true');}}t.scrollIntoView({behavior:'smooth',block:'center'});if(t.focus){try{t.focus({preventScroll:true});}catch(_e){t.focus();}}}}\n";
	echo "  });\n";
	// Live quick filter (hides non-matching rows of the targeted table)
	echo "  document.addEventListener('input',function(e){\n";
	echo "    var qf=e.target.closest&&e.target.closest('input[data-quickfilter]');if(!qf)return;var sel=qf.getAttribute('data-quickfilter');var tbl=sel&&document.querySelector(sel);if(!tbl)return;var q=qf.value.toLowerCase();var b=tbl.tBodies[0];if(!b)return;Array.prototype.forEach.call(b.rows,function(r){if(r.classList.contains('epc-d365-sumrow'))return;r.style.display=(!q||r.textContent.toLowerCase().indexOf(q)>-1)?'':'none';});\n";
	echo "  });\n";
	echo "});\n";
	echo "})();</script>\n";
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

/**
 * Open a data grid. Each header may be a plain string (rendered as-is) or an
 * array for D365 grids: ['label'=>'Total','sort'=>'num'|'text','class'=>'num'].
 * Pass $tableId to enable the live quick filter / column sort targeting.
 */
function erp_table_open($headers, $tableClass = 'table table-striped table-bordered table-condensed table-epc epc-erp-table', $tableId = '')
{
	$idAttr = $tableId !== '' ? ' id="' . epc_erp_h($tableId) . '"' : '';
	echo '<div class="table-responsive epc-erp-table-wrap"><table class="' . epc_erp_h($tableClass) . '"' . $idAttr . '><thead><tr>';
	foreach ($headers as $h) {
		if (is_array($h)) {
			$attrs = '';
			if (!empty($h['sort'])) {
				$attrs .= ' data-sort="' . epc_erp_h($h['sort']) . '"';
			}
			if (!empty($h['class'])) {
				$attrs .= ' class="' . epc_erp_h($h['class']) . '"';
			}
			echo '<th' . $attrs . '>' . ($h['label'] ?? '') . '</th>';
		} else {
			echo '<th>' . $h . '</th>';
		}
	}
	echo '</tr></thead><tbody>';
}

/**
 * Close a data grid. Pass $footerHtml (e.g. a Sum row) to emit a <tfoot>.
 * The footer row should carry class="epc-d365-sumrow" so sort/filter skip it.
 */
function erp_table_close($footerHtml = '')
{
	echo '</tbody>';
	if ($footerHtml !== '') {
		echo '<tfoot>' . $footerHtml . '</tfoot>';
	}
	echo '</table></div>';
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
