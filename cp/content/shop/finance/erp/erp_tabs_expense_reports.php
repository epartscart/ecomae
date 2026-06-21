<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_phase8.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

$expStatus = isset($_GET['exp_status']) ? (string)$_GET['exp_status'] : '';
$expenses = epc_erp_expense_reports_list($db_link, $expStatus);

erp_page_header(
	'<i class="fa fa-credit-card"></i> Expense reports',
	'Staff expense claims — submit, approve, and pay via cash &amp; bank.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Expenses'),
	)
);
erp_filter_bar($erpUrl, 'expense_reports', $date_from_str, $date_to_str,
	'<label>Status</label> <select name="exp_status" class="form-control input-sm"><option value="">All</option>'
	. '<option value="submitted">Submitted</option><option value="approved">Approved</option><option value="paid">Paid</option></select>'
);
ob_start();
if (empty($expenses)) {
	erp_empty_state('No expense reports yet.');
} else {
	erp_table_open(array('Report #', 'Staff', 'Title', 'Amount AED', 'Status', 'Period'));
	foreach ($expenses as $e) {
		echo '<tr><td>' . epc_erp_h($e['report_no']) . '</td><td>' . epc_erp_h($e['display_name'] ?: ('User #' . (int)$e['staff_user_id'])) . '</td>';
		echo '<td>' . epc_erp_h($e['title']) . '</td><td>' . epc_erp_money($e['total_amount']) . '</td>';
		echo '<td><span class="label label-default">' . epc_erp_h($e['status']) . '</span></td>';
		echo '<td>' . epc_erp_h(date('Y-m-d', (int)$e['period_from']) . ' — ' . date('Y-m-d', (int)$e['period_to'])) . '</td></tr>';
	}
	erp_table_close();
}
erp_section_card('Expense reports', ob_get_clean(), array('icon' => 'fa-list'));
ob_start();
?>
<form id="epc_erp_form_expense" class="form-horizontal" style="max-width:640px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<div class="form-group"><label class="col-sm-3">Title</label><div class="col-sm-9"><input name="title" class="form-control input-sm" required></div></div>
	<div class="form-group"><label class="col-sm-3">Amount AED</label><div class="col-sm-9"><input name="total_amount" type="number" step="0.01" class="form-control input-sm" required></div></div>
	<div class="form-group"><label class="col-sm-3">Period</label><div class="col-sm-9 form-inline">
		<input name="period_from" type="date" class="form-control input-sm" value="<?php echo epc_erp_h($date_from_str); ?>">
		<input name="period_to" type="date" class="form-control input-sm" value="<?php echo epc_erp_h($date_to_str); ?>"></div></div>
	<div class="form-group"><label class="col-sm-3">Notes</label><div class="col-sm-9"><textarea name="notes" class="form-control input-sm" rows="2"></textarea></div></div>
	<div class="form-group"><div class="col-sm-offset-3 col-sm-9"><button type="submit" class="btn btn-primary btn-sm">Submit expense report</button></div></div>
</form>
<?php
erp_section_card('New expense report', ob_get_clean(), array('icon' => 'fa-plus'));
