<?php
/**
 * Module: Account Receivable setup.
 * Sub-modules: Method of payment, Terms of payment, Customer group,
 * + links to customer invoice / customer payment journals.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

$view = isset($_GET['pm_view']) ? (string) $_GET['pm_view'] : 'methods';
$subs = array(
	'methods' => 'Method of payment',
	'terms' => 'Terms of payment',
	'groups' => 'Customer group',
	'journals' => 'Journals',
);

echo '<div class="epc-erp-section"><h3 style="margin-top:0;"><i class="fa fa-handshake-o"></i> Account Receivable — setup</h3>';
echo '<p class="text-muted">Configure payment methods, payment terms and customer groups, then post customer invoice &amp; payment journals. Per-tenant and fully configurable.</p></div>';

epc_erp_pm_module_tabs($erpUrl, 'ar_setup', 'sales', $date_from_str, $date_to_str, $subs, $view);

$termOpts = array('0' => '— none —');
try {
	foreach (epc_erp_pm_list($db_link, 'epc_erp_pm_pay_terms', true) as $t) {
		$termOpts[(string) $t['id']] = $t['code'] . ' · ' . $t['name'];
	}
} catch (Exception $e) {
}

switch ($view) {
	case 'terms':
		epc_erp_pm_section($db_link, $csrf, 'epc_erp_pm_pay_terms', 'Terms of payment',
			array(
				array('name' => 'code', 'label' => 'Code', 'required' => true, 'placeholder' => 'NET30'),
				array('name' => 'name', 'label' => 'Name', 'required' => true),
				array('name' => 'net_days', 'label' => 'Net days', 'type' => 'number'),
				array('name' => 'note', 'label' => 'Note'),
			),
			array(array('key' => 'code', 'label' => 'Code'), array('key' => 'name', 'label' => 'Name'), array('key' => 'net_days', 'label' => 'Net days')),
			'fa-calendar');
		break;
	case 'groups':
		epc_erp_pm_section($db_link, $csrf, 'epc_erp_pm_customer_groups', 'Customer groups',
			array(
				array('name' => 'code', 'label' => 'Code', 'required' => true, 'placeholder' => 'WHOLESALE'),
				array('name' => 'name', 'label' => 'Name', 'required' => true),
				array('name' => 'terms_id', 'label' => 'Default terms', 'type' => 'select', 'options' => $termOpts),
				array('name' => 'note', 'label' => 'Note'),
			),
			array(array('key' => 'code', 'label' => 'Code'), array('key' => 'name', 'label' => 'Name')),
			'fa-users');
		break;
	case 'journals':
		echo '<div class="epc-erp-section"><h4><i class="fa fa-book"></i> Customer journals</h4>';
		echo '<p class="text-muted">Post and review customer invoices and receipts:</p><div class="btn-group" style="flex-wrap:wrap;">';
		echo '<a class="btn btn-default btn-sm" href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'sales_orders', $date_from_str, $date_to_str, 'sales')) . '"><i class="fa fa-file-text-o"></i> Customer invoice journal (sales invoices)</a> ';
		echo '<a class="btn btn-default btn-sm" href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'receivables', $date_from_str, $date_to_str, 'sales')) . '"><i class="fa fa-money"></i> Customer payment journal</a> ';
		echo '<a class="btn btn-default btn-sm" href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'aging', $date_from_str, $date_to_str, 'finance')) . '&amp;aging_view=ar"><i class="fa fa-hourglass-half"></i> Receivables aging</a>';
		echo '</div></div>';
		break;
	case 'methods':
	default:
		epc_erp_pm_section($db_link, $csrf, 'epc_erp_pm_pay_methods', 'Methods of payment',
			array(
				array('name' => 'code', 'label' => 'Code', 'required' => true, 'placeholder' => 'BANK'),
				array('name' => 'name', 'label' => 'Name', 'required' => true),
				array('name' => 'method_type', 'label' => 'Type', 'type' => 'select', 'options' => array('cash' => 'Cash', 'bank' => 'Bank transfer', 'cheque' => 'Cheque', 'card' => 'Card', 'online' => 'Online')),
				array('name' => 'account_code', 'label' => 'GL account'),
				array('name' => 'note', 'label' => 'Note'),
			),
			array(array('key' => 'code', 'label' => 'Code'), array('key' => 'name', 'label' => 'Name'), array('key' => 'method_type', 'label' => 'Type'), array('key' => 'account_code', 'label' => 'GL acct')),
			'fa-credit-card');
		break;
}
