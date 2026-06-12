<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

$floats = epc_erp_petty_cash_list($db_link);

erp_page_header(
	'<i class="fa fa-money"></i> Petty cash',
	'Small cash floats with custodian and linked cash account.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Petty cash'),
	)
);
erp_stat_cards(array(
	array('label' => 'Active floats', 'value' => (string) count($floats)),
));
ob_start();
if (empty($floats)) {
	erp_empty_state('No petty cash floats configured.', 'fa-money');
} else {
	erp_table_open(array('Name', 'Float AED', 'Account balance', 'Custodian ID'));
	foreach ($floats as $f) {
		echo '<tr><td>' . epc_erp_h($f['name']) . '</td><td>' . epc_erp_money($f['float_amount']) . '</td>';
		echo '<td>' . epc_erp_money($f['account_balance'] ?? 0) . '</td>';
		echo '<td>' . ((int) $f['custodian_user_id'] > 0 ? (int) $f['custodian_user_id'] : '—') . '</td></tr>';
	}
	erp_table_close();
}
erp_section_card('Petty cash floats', ob_get_clean(), array('icon' => 'fa-list'));
ob_start();
?>
<form id="epc_erp_form_petty_cash" class="form-horizontal" style="max-width:640px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<div class="form-group"><label class="col-sm-3">Name</label><div class="col-sm-9"><input name="name" class="form-control input-sm" required placeholder="Office petty cash"></div></div>
	<div class="form-group"><label class="col-sm-3">Float amount</label><div class="col-sm-9"><input name="float_amount" type="number" step="0.01" class="form-control input-sm" value="500"></div></div>
	<?php echo epc_erp_dim_render_fields($db_link); ?>
	<div class="form-group"><div class="col-sm-offset-3 col-sm-9"><button type="submit" class="btn btn-primary btn-sm">Create float</button></div></div>
</form>
<?php
erp_section_card('New petty cash', ob_get_clean(), array('icon' => 'fa-plus'));
