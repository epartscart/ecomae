<?php
/**
 * Module: Account Payable setup.
 * Sub-modules: Method of payment, Terms of payment, Vendor group,
 * + links to vendor bill / vendor payment journals.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

$view = isset($_GET['pm_view']) ? (string) $_GET['pm_view'] : 'methods';
$subs = array(
	'methods' => 'Method of payment',
	'terms' => 'Terms of payment',
	'groups' => 'Vendor group',
	'journals' => 'Journals',
);

echo '<div class="epc-erp-section"><h3 style="margin-top:0;"><i class="fa fa-credit-card"></i> Account Payable — setup</h3>';
echo '<p class="text-muted">Configure payment methods, payment terms and vendor groups, then post vendor bill &amp; payment journals. Per-tenant and fully configurable.</p></div>';

epc_erp_pm_module_tabs($erpUrl, 'ap_setup', 'purchasing', $date_from_str, $date_to_str, $subs, $view);

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
				array('name' => 'name', 'label' => 'Name', 'required' => true, 'placeholder' => 'Net 30 days'),
				array('name' => 'net_days', 'label' => 'Net days', 'type' => 'number', 'placeholder' => '30'),
				array('name' => 'note', 'label' => 'Note'),
			),
			array(array('key' => 'code', 'label' => 'Code'), array('key' => 'name', 'label' => 'Name'), array('key' => 'net_days', 'label' => 'Net days')),
			'fa-calendar');
		break;
	case 'groups':
		epc_erp_pm_section($db_link, $csrf, 'epc_erp_pm_vendor_groups', 'Vendor groups',
			array(
				array('name' => 'code', 'label' => 'Code', 'required' => true, 'placeholder' => 'LOCAL'),
				array('name' => 'name', 'label' => 'Name', 'required' => true),
				array('name' => 'terms_id', 'label' => 'Default terms', 'type' => 'select', 'options' => $termOpts),
				array('name' => 'note', 'label' => 'Note'),
			),
			array(array('key' => 'code', 'label' => 'Code'), array('key' => 'name', 'label' => 'Name')),
			'fa-truck');
		break;
	case 'journals':
		echo '<div class="epc-erp-section"><h4><i class="fa fa-book"></i> Vendor journals</h4>';
		echo '<p class="text-muted">Post and review vendor bills and payments:</p><div class="btn-group" style="flex-wrap:wrap;">';
		echo '<a class="btn btn-default btn-sm" href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'purchase_orders', $date_from_str, $date_to_str, 'purchasing')) . '"><i class="fa fa-file-text-o"></i> Vendor bill journal (purchase invoices)</a> ';
		echo '<a class="btn btn-default btn-sm" href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'payables', $date_from_str, $date_to_str, 'purchasing')) . '"><i class="fa fa-money"></i> Vendor payment journal</a> ';
		echo '<a class="btn btn-default btn-sm" href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'aging', $date_from_str, $date_to_str, 'finance')) . '&amp;aging_view=ap"><i class="fa fa-hourglass-half"></i> Payables aging</a>';
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
