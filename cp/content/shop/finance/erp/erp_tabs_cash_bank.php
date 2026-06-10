<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_phase8.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
$erpOnlyCash = epc_erp_is_erp_only_context();
$customersCash = $erpOnlyCash ? $db_link->query('SELECT `user_id`, `email` FROM `users` WHERE `user_id` > 0 ORDER BY `email` LIMIT 300')->fetchAll(PDO::FETCH_ASSOC) : array();

$viewAccount = isset($_GET['account_id']) ? (int)$_GET['account_id'] : 0;
$entries = epc_erp_list_cash_entries($db_link, $viewAccount);
$stmtLines = epc_erp_bank_unmatched_lines($db_link, $viewAccount);
$unmatchedEntries = $viewAccount > 0 ? epc_erp_bank_unmatched_entries($db_link, $viewAccount) : array();

erp_page_header(
	'<i class="fa fa-university"></i> Cash &amp; bank',
	'Accounts, journal entries, and bank statement reconciliation.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Cash &amp; bank'),
	)
);
erp_stat_cards(array(
	array('label' => 'Accounts', 'value' => (string)count($accounts)),
	array('label' => 'Unmatched statement lines', 'value' => (string)count($stmtLines), 'class' => count($stmtLines) ? 'red' : 'green'),
));
ob_start();
erp_table_open(array('Account', 'Type', 'Currency', 'Opening', 'Balance', ''));
foreach ($accounts as $a) {
	echo '<tr><td>' . epc_erp_h($a['name']);
	if ($a['bank_name']) {
		echo ' — ' . epc_erp_h($a['bank_name']);
	}
	echo '</td><td>' . epc_erp_h($a['account_type']) . '</td><td>' . epc_erp_h($a['currency_code']) . '</td>';
	echo '<td>' . epc_erp_money($a['opening_balance']) . '</td><td><strong>' . epc_erp_money($a['balance']) . '</strong></td>';
	echo '<td><a class="btn btn-xs btn-default" href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'cash_bank', $date_from_str, $date_to_str) . '&account_id=' . (int)$a['id']) . '">Reconcile</a></td></tr>';
}
erp_table_close();
erp_section_card('Accounts', ob_get_clean(), array('icon' => 'fa-list'));

ob_start();
?>
<form id="epc_erp_form_account" class="form-inline epc-erp-form-inline">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<input type="text" name="name" class="form-control input-sm" placeholder="Account name" required>
	<select name="account_type" class="form-control input-sm"><option value="cash">Cash</option><option value="bank">Bank</option></select>
	<input type="text" name="bank_name" class="form-control input-sm" placeholder="Bank name">
	<input type="number" step="0.01" name="opening_balance" class="form-control input-sm" placeholder="Opening" value="0">
	<button type="submit" class="btn btn-sm btn-primary">Create account</button>
</form>
<form id="epc_erp_form_entry" class="form-inline epc-erp-form-inline" style="margin-top:10px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<select name="account_id" class="form-control input-sm" required><option value="">Account</option>
	<?php foreach ($accounts as $a): ?><option value="<?php echo (int)$a['id']; ?>"<?php echo $viewAccount === (int)$a['id'] ? ' selected' : ''; ?>><?php echo epc_erp_h($a['name']); ?></option><?php endforeach; ?>
	</select>
	<select name="direction" class="form-control input-sm"><option value="1">Receipt (+)</option><option value="0">Payment (−)</option></select>
	<input type="number" step="0.01" name="amount" class="form-control input-sm" placeholder="Amount" required>
	<input type="number" name="order_id" class="form-control input-sm" placeholder="Order ID">
	<input type="text" name="reference" class="form-control input-sm" placeholder="Reference">
	<button type="submit" class="btn btn-sm btn-success">Post entry</button>
</form>
<?php if ($erpOnlyCash): ?>
<form id="epc_erp_form_receipt_voucher" class="form-horizontal" style="margin-top:14px;max-width:760px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<h5>Receipt voucher (RV-)</h5>
	<div class="form-group"><label class="col-sm-3">Customer</label><div class="col-sm-9"><select name="user_id" class="form-control input-sm" required><option value="">—</option>
	<?php foreach ($customersCash as $c): ?><option value="<?php echo (int)$c['user_id']; ?>"><?php echo epc_erp_h($c['email']); ?></option><?php endforeach; ?>
	</select></div></div>
	<div class="form-group"><label class="col-sm-3">Bank account</label><div class="col-sm-9"><select name="account_id" class="form-control input-sm" required><option value="">—</option>
	<?php foreach ($accounts as $a): ?><option value="<?php echo (int)$a['id']; ?>"><?php echo epc_erp_h($a['name']); ?></option><?php endforeach; ?>
	</select></div></div>
	<div class="form-group"><label class="col-sm-3">Amount AED</label><div class="col-sm-9"><input type="number" step="0.01" name="amount" class="form-control input-sm" required></div></div>
	<div class="form-group"><label class="col-sm-3">Sales order (opt.)</label><div class="col-sm-9"><input type="number" name="sales_order_id" class="form-control input-sm" placeholder="ERP SO id for VAT link"></div></div>
	<div class="form-group"><div class="col-sm-offset-3 col-sm-9">
	<label class="checkbox-inline"><input type="checkbox" name="is_advance" value="1" checked> UAE advance receipt (VAT + liability GL 2050)</label>
	<label class="checkbox-inline"><input type="checkbox" name="post_gl" value="1" checked> Post to GL</label>
	<button type="submit" class="btn btn-primary btn-sm">Post RV voucher</button></div></div>
</form>
<form id="epc_erp_form_transfer_voucher" class="form-horizontal" style="margin-top:14px;max-width:760px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<h5>Transfer voucher (TV-)</h5>
	<div class="form-group"><label class="col-sm-3">From / To</label><div class="col-sm-9 form-inline">
	<select name="from_account_id" class="form-control input-sm" required><option value="">From</option>
	<?php foreach ($accounts as $a): ?><option value="<?php echo (int)$a['id']; ?>"><?php echo epc_erp_h($a['name']); ?></option><?php endforeach; ?>
	</select>
	<select name="to_account_id" class="form-control input-sm" required><option value="">To</option>
	<?php foreach ($accounts as $a): ?><option value="<?php echo (int)$a['id']; ?>"><?php echo epc_erp_h($a['name']); ?></option><?php endforeach; ?>
	</select></div></div>
	<div class="form-group"><label class="col-sm-3">Amount</label><div class="col-sm-9 form-inline">
	<input type="number" step="0.01" name="amount" class="form-control input-sm" required>
	<button type="submit" class="btn btn-default btn-sm">Post TV transfer</button></div></div>
</form>
<?php endif; ?>
<?php
erp_section_card('New account / entry', ob_get_clean(), array('icon' => 'fa-plus'));

if ($viewAccount > 0) {
	ob_start();
	?>
	<p class="text-muted">Import CSV: <code>date,description,reference,amount</code> (positive = credit in).</p>
	<form id="epc_erp_form_bank_import" class="form-inline">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
		<input type="hidden" name="account_id" value="<?php echo (int)$viewAccount; ?>">
		<textarea name="csv_text" class="form-control input-sm" rows="3" style="min-width:320px;" placeholder="2026-05-01,Transfer in,REF001,1500.00"></textarea>
		<button type="submit" class="btn btn-sm btn-info">Import statement</button>
	</form>
	<?php
	$reconTop = ob_get_clean();
	erp_section_card('Bank reconciliation — account #' . (int)$viewAccount, $reconTop, array('icon' => 'fa-check-square-o'));

	ob_start();
	if (empty($stmtLines) && empty($unmatchedEntries)) {
		erp_empty_state('No unmatched items. Import a bank statement or post cash entries.');
	} else {
		echo '<div class="row"><div class="col-md-6"><h5>Statement lines (unmatched)</h5>';
		erp_table_open(array('Date', 'Description', 'Amount', 'Match to entry'));
		foreach ($stmtLines as $ln) {
			echo '<tr><td>' . epc_erp_h(date('Y-m-d', (int)$ln['line_date'])) . '</td>';
			echo '<td>' . epc_erp_h($ln['description']) . '</td>';
			echo '<td>' . (((int)$ln['direction'] === 1) ? '+' : '−') . epc_erp_money($ln['amount']) . '</td><td>';
			echo '<form class="epc-erp-recon-match form-inline"><input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
			echo '<input type="hidden" name="line_id" value="' . (int)$ln['id'] . '">';
			echo '<select name="entry_id" class="form-control input-sm"><option value="">Entry</option>';
			foreach ($unmatchedEntries as $e) {
				echo '<option value="' . (int)$e['id'] . '">#' . (int)$e['id'] . ' ' . epc_erp_h(date('m-d', (int)$e['time'])) . ' ' . epc_erp_money($e['amount']) . '</option>';
			}
			echo '</select><button type="submit" class="btn btn-xs btn-primary">Match</button></form></td></tr>';
		}
		erp_table_close();
		echo '</div><div class="col-md-6"><h5>Unmatched cash entries</h5>';
		erp_table_open(array('Date', 'Type', 'Amount', 'Ref'));
		foreach ($unmatchedEntries as $e) {
			echo '<tr><td>' . epc_erp_h(date('Y-m-d', (int)$e['time'])) . '</td><td>' . epc_erp_h($e['entry_type']) . '</td>';
			echo '<td>' . epc_erp_money($e['amount']) . '</td><td>' . epc_erp_h($e['reference']) . '</td></tr>';
		}
		erp_table_close();
		echo '</div></div>';
	}
	erp_section_card('Match statement to ledger', ob_get_clean());
}

ob_start();
if (empty($entries)) {
	erp_empty_state('No entries for this filter.');
} else {
	erp_table_open(array('Date', 'Account', 'Type', 'In/Out', 'Amount', 'Order', 'Reference'));
	foreach ($entries as $e) {
		echo '<tr><td>' . epc_erp_h(date('Y-m-d H:i', (int)$e['time'])) . '</td><td>' . epc_erp_h($e['account_name']) . '</td>';
		echo '<td>' . epc_erp_h($e['entry_type']) . '</td><td>' . (((int)$e['direction'] === 1) ? '+' : '−') . '</td>';
		echo '<td>' . epc_erp_money($e['amount']) . '</td><td>' . ((int)$e['order_id'] ? '#' . (int)$e['order_id'] : '—') . '</td>';
		echo '<td>' . epc_erp_h($e['reference']) . '</td></tr>';
	}
	erp_table_close();
}
erp_section_card('Recent entries', ob_get_clean(), array('icon' => 'fa-list-alt'));
