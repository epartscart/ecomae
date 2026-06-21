<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

$batches = epc_erp_payment_batches_list($db_link);
if (!isset($accounts)) {
	$accounts = epc_erp_list_cash_accounts($db_link);
}

erp_page_header(
	'<i class="fa fa-send"></i> Payment batches',
	'SEPA / local payment batch stub — group supplier payments for bank export.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Payment batches'),
	)
);
erp_stat_cards(array(
	array('label' => 'Draft batches', 'value' => (string) count(array_filter($batches, function ($b) {
		return ($b['status'] ?? '') === 'draft';
	}))),
));
ob_start();
if (empty($batches)) {
	erp_empty_state('No payment batches. Create a draft SEPA batch below.', 'fa-send');
} else {
	erp_table_open(array('Batch #', 'Type', 'Account', 'Total AED', 'Lines', 'Status', 'Execution'));
	foreach ($batches as $b) {
		echo '<tr><td>' . epc_erp_h($b['batch_no']) . '</td><td>' . epc_erp_h(strtoupper($b['batch_type'])) . '</td>';
		echo '<td>' . epc_erp_h($b['account_name'] ?: '—') . '</td><td>' . epc_erp_money($b['total_amount']) . '</td>';
		echo '<td>' . (int) $b['line_count'] . '</td><td>' . epc_erp_h($b['status']) . '</td>';
		echo '<td>' . ((int) $b['execution_date'] > 0 ? epc_erp_h(date('Y-m-d', (int) $b['execution_date'])) : '—') . '</td></tr>';
	}
	erp_table_close();
}
erp_section_card('Batch list', ob_get_clean(), array('icon' => 'fa-list'));
ob_start();
?>
<form id="epc_erp_form_payment_batch" class="form-horizontal" style="max-width:720px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<div class="form-group"><label class="col-sm-3">Pay from</label><div class="col-sm-9"><select name="account_id" class="form-control input-sm"><option value="0">—</option>
	<?php foreach ($accounts as $a): ?><option value="<?php echo (int) $a['id']; ?>"><?php echo epc_erp_h($a['name']); ?></option><?php endforeach; ?>
	</select></div></div>
	<div class="form-group"><label class="col-sm-3">Type / total / lines</label><div class="col-sm-9 form-inline">
		<select name="batch_type" class="form-control input-sm"><option value="sepa">SEPA</option><option value="local">Local</option></select>
		<input name="total_amount" type="number" step="0.01" class="form-control input-sm" placeholder="Total AED">
		<input name="line_count" type="number" class="form-control input-sm" placeholder="Lines" value="1"></div></div>
	<div class="form-group"><label class="col-sm-3">Execution date</label><div class="col-sm-9"><input name="execution_date" type="date" class="form-control input-sm"></div></div>
	<?php echo epc_erp_dim_render_fields($db_link); ?>
	<div class="form-group"><div class="col-sm-offset-3 col-sm-9"><button type="submit" class="btn btn-primary btn-sm">Create draft batch</button></div></div>
</form>
<p class="text-muted"><small>Bank file export is a stub — batches are stored for workflow tracking only.</small></p>
<?php
erp_section_card('New batch', ob_get_clean(), array('icon' => 'fa-plus'));
