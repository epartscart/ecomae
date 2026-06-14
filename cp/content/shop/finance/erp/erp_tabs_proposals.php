<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

$kind = isset($_GET['quote_kind']) ? (string) $_GET['quote_kind'] : '';
$quotes = epc_erp_proposals_list($db_link, $kind);
$crmUrl = epc_erp_tab_url($erpUrl, 'crm', $date_from_str, $date_to_str) . '&crm_tab=quotes';

erp_page_header(
	'<i class="fa fa-file-text"></i> Sales quotations',
	'Sales quotations, quotes and proforma invoices — create and edit in the CRM tab, then confirm a quotation into a sales order.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Sales and marketing'),
		array('label' => 'Sales quotations'),
	),
	array(array('label' => 'Open CRM quotes', 'url' => $crmUrl, 'class' => 'btn-primary', 'icon' => 'fa-handshake-o'))
);
erp_stat_cards(array(
	array('label' => 'Total quotations', 'value' => (string) count($quotes)),
	array('label' => 'Draft / sent', 'value' => (string) count(array_filter($quotes, function ($q) {
		return in_array($q['status'] ?? '', array('draft', 'sent'), true);
	}))),
));
erp_filter_bar($erpUrl, 'proposals', $date_from_str, $date_to_str,
	'<label>Type</label> <select name="quote_kind" class="form-control input-sm">'
	. '<option value="">All</option><option value="quote"' . ($kind === 'quote' ? ' selected' : '') . '>Quote</option>'
	. '<option value="proforma"' . ($kind === 'proforma' ? ' selected' : '') . '>Proforma</option>'
	. '<option value="commercial"' . ($kind === 'commercial' ? ' selected' : '') . '>Commercial</option></select>'
);
ob_start();
if (empty($quotes)) {
	erp_empty_state('No quotations yet. Use CRM → Quotes or create from an opportunity.', 'fa-file-text-o');
} else {
	erp_table_open(array('Number', 'Type', 'Opportunity', 'Status', 'Subtotal AED', 'Updated', ''));
	foreach ($quotes as $q) {
		echo '<tr><td>' . epc_erp_h($q['quote_number']) . '</td>';
		echo '<td><span class="label label-default">' . epc_erp_h($q['quote_kind'] ?? 'quote') . '</span></td>';
		echo '<td>' . epc_erp_h($q['opp_title'] ?: '—') . '</td>';
		echo '<td>' . epc_erp_h($q['status']) . '</td>';
		echo '<td>' . epc_erp_money($q['subtotal']) . '</td>';
		echo '<td>' . epc_erp_h(date('Y-m-d', (int) ($q['time_updated'] ?: $q['time_created']))) . '</td>';
		echo '<td><a class="btn btn-xs btn-default" href="' . epc_erp_h($crmUrl) . '">CRM</a></td></tr>';
	}
	erp_table_close();
}
erp_section_card('Sales quotation list', ob_get_clean(), array('icon' => 'fa-list'));
