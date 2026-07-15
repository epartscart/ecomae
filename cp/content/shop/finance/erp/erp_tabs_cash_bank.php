<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_phase8.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
$erpOnlyCash = epc_erp_is_erp_only_context();
$customersCash = $erpOnlyCash ? $db_link->query('SELECT `user_id`, `email` FROM `users` WHERE `user_id` > 0 ORDER BY `email` LIMIT 300')->fetchAll(PDO::FETCH_ASSOC) : array();
$suppliersCash = array();
if ($erpOnlyCash) {
	try {
		$suppliersCash = $db_link->query('SELECT `id`, `name` FROM `epc_erp_suppliers` WHERE `active` = 1 ORDER BY `name` LIMIT 300')->fetchAll(PDO::FETCH_ASSOC);
	} catch (Exception $e) {
		$suppliersCash = array();
	}
}

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
echo '<div class="epc-erp-related-links" style="margin-bottom:14px;padding:8px 12px;background:#f9f9f9;border:1px solid #e3e3e3;border-radius:4px;">'
	. '<strong style="font-size:12px;color:#555;">Related:</strong>'
	. ' <a href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'bank_recon', $date_from_str, $date_to_str, 'banking')) . '" style="margin-left:8px;font-size:12px;">Bank reconciliation</a>'
	. ' <a href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'gl', $date_from_str, $date_to_str, 'finance')) . '" style="margin-left:8px;font-size:12px;">General ledger / Journals</a>'
	. '</div>';
erp_stat_cards(array(
	array('label' => 'Accounts', 'value' => (string)count($accounts)),
	array('label' => 'Unmatched statement lines', 'value' => (string)count($stmtLines), 'class' => count($stmtLines) ? 'red' : 'green'),
));
// Resolve business-unit / legal-entity names for the account list.
$buNameMap = array();
$leNameMap = array();
try {
	foreach ($db_link->query("SELECT `id`, `name` FROM `epc_erp_pm_business_units`")->fetchAll(PDO::FETCH_ASSOC) as $bu) {
		$buNameMap[(int) $bu['id']] = (string) $bu['name'];
	}
} catch (Exception $e) {
}
try {
	foreach ($db_link->query("SELECT `id`, `name` FROM `epc_erp_pm_legal_entities`")->fetchAll(PDO::FETCH_ASSOC) as $le) {
		$leNameMap[(int) $le['id']] = (string) $le['name'];
	}
} catch (Exception $e) {
}
ob_start();
erp_table_open(array('Account', 'Business unit', 'Account no / IBAN', 'Currency', 'Opening', 'Balance', ''));
foreach ($accounts as $a) {
	echo '<tr><td>' . epc_erp_h($a['name']);
	if (!empty($a['bank_name'])) {
		echo ' <small class="text-muted">— ' . epc_erp_h($a['bank_name']) . '</small>';
	}
	$buId = (int) ($a['business_unit_id'] ?? 0);
	$leId = (int) ($a['legal_entity_id'] ?? 0);
	$buCell = $buId > 0 && isset($buNameMap[$buId]) ? $buNameMap[$buId] : ($leId > 0 && isset($leNameMap[$leId]) ? $leNameMap[$leId] : '—');
	$acctNo = (string) ($a['account_number'] ?? '');
	$iban = (string) ($a['iban'] ?? '');
	$acctCell = $acctNo !== '' ? $acctNo : '—';
	if ($iban !== '') {
		$acctCell .= ' <small class="text-muted">' . epc_erp_h($iban) . '</small>';
	}
	echo '</td><td>' . epc_erp_h($buCell) . '</td>';
	echo '<td>' . $acctCell . '</td>';
	echo '<td>' . epc_erp_h($a['currency_code']) . '</td>';
	echo '<td>' . epc_erp_money($a['opening_balance']) . '</td><td><strong>' . epc_erp_money($a['balance']) . '</strong></td>';
	echo '<td><a class="btn btn-xs btn-default" href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'cash_bank', $date_from_str, $date_to_str) . '&account_id=' . (int)$a['id']) . '">Reconcile</a></td></tr>';
}
erp_table_close();
erp_section_card('Accounts', ob_get_clean(), array('icon' => 'fa-list'));

// Legal entity / business unit options for the bank account form.
$leOptsCB = array();
$buOptsCB = array();
$glOptsCB = array();
try {
	foreach ($db_link->query("SELECT `id`, `code`, `name` FROM `epc_erp_pm_legal_entities` WHERE `active` = 1 ORDER BY `name`")->fetchAll(PDO::FETCH_ASSOC) as $le) {
		$leOptsCB[(int) $le['id']] = $le['code'] . ' · ' . $le['name'];
	}
} catch (Exception $e) {
}
try {
	foreach ($db_link->query("SELECT `id`, `code`, `name` FROM `epc_erp_pm_business_units` WHERE `active` = 1 ORDER BY `name`")->fetchAll(PDO::FETCH_ASSOC) as $bu) {
		$buOptsCB[(int) $bu['id']] = $bu['code'] . ' · ' . $bu['name'];
	}
} catch (Exception $e) {
}
try {
	foreach ($db_link->query("SELECT `id`, `code`, `name` FROM `epc_erp_coa_accounts` WHERE `active` = 1 AND `account_type` IN ('asset','bank') ORDER BY `code`")->fetchAll(PDO::FETCH_ASSOC) as $coa) {
		$glOptsCB[(int) $coa['id']] = $coa['code'] . ' · ' . $coa['name'];
	}
} catch (Exception $e) {
}
ob_start();
?>
<form id="epc_erp_form_account" class="form-horizontal epc-erp-form-inline" style="max-width:920px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<div class="row">
		<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Account name *</label><div class="col-sm-8"><input type="text" name="name" class="form-control input-sm" placeholder="e.g. Operating account" required></div></div>
		<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Account type</label><div class="col-sm-8"><select name="account_type" class="form-control input-sm"><option value="cash">Cash</option><option value="bank">Bank</option></select></div></div>
		<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Legal entity</label><div class="col-sm-8"><select name="legal_entity_id" class="form-control input-sm"><option value="0">— none —</option>
			<?php foreach ($leOptsCB as $v => $t): ?><option value="<?php echo (int) $v; ?>"><?php echo epc_erp_h($t); ?></option><?php endforeach; ?>
		</select></div></div>
		<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Business unit</label><div class="col-sm-8"><select name="business_unit_id" class="form-control input-sm"><option value="0">— none —</option>
			<?php foreach ($buOptsCB as $v => $t): ?><option value="<?php echo (int) $v; ?>"><?php echo epc_erp_h($t); ?></option><?php endforeach; ?>
		</select>
		<?php if (empty($buOptsCB)): ?><span class="help-block" style="margin:2px 0 0;font-size:11px;">No business units yet — add them under <a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'business_units', $date_from_str, $date_to_str, 'enterprise')); ?>">Business Unit</a>.</span><?php endif; ?>
		</div></div>
		<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Bank name</label><div class="col-sm-8"><input type="text" name="bank_name" class="form-control input-sm" placeholder="e.g. Emirates NBD"></div></div>
		<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Branch</label><div class="col-sm-8"><input type="text" name="bank_branch" class="form-control input-sm" placeholder="Branch name / number"></div></div>
		<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Account number</label><div class="col-sm-8"><input type="text" name="account_number" class="form-control input-sm"></div></div>
		<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">IBAN</label><div class="col-sm-8"><input type="text" name="iban" class="form-control input-sm"></div></div>
		<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">SWIFT / BIC</label><div class="col-sm-8"><input type="text" name="swift_bic" class="form-control input-sm"></div></div>
		<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Routing / sort code</label><div class="col-sm-8"><input type="text" name="routing_code" class="form-control input-sm"></div></div>
		<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Currency</label><div class="col-sm-8"><input type="text" name="currency_code" class="form-control input-sm" value="AED" maxlength="8"></div></div>
		<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Opening balance</label><div class="col-sm-8"><input type="number" step="0.01" name="opening_balance" class="form-control input-sm" value="0"></div></div>
		<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">GL main account</label><div class="col-sm-8"><select name="gl_account_id" class="form-control input-sm"><option value="0">— auto —</option>
			<?php foreach ($glOptsCB as $v => $t): ?><option value="<?php echo (int) $v; ?>"><?php echo epc_erp_h($t); ?></option><?php endforeach; ?>
		</select></div></div>
		<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Status</label><div class="col-sm-8"><select name="status" class="form-control input-sm"><option value="active">Active</option><option value="inactive">Inactive</option><option value="closed">Closed</option></select></div></div>
		<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Contact name</label><div class="col-sm-8"><input type="text" name="contact_name" class="form-control input-sm"></div></div>
		<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Contact phone</label><div class="col-sm-8"><input type="text" name="contact_phone" class="form-control input-sm"></div></div>
		<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Contact email</label><div class="col-sm-8"><input type="email" name="contact_email" class="form-control input-sm"></div></div>
		<div class="col-sm-12 form-group"><label class="col-sm-2 control-label">Bank address</label><div class="col-sm-10"><input type="text" name="address" class="form-control input-sm" placeholder="Street, city, country"></div></div>
		<div class="col-sm-12 form-group"><label class="col-sm-2 control-label">Notes</label><div class="col-sm-10"><input type="text" name="notes" class="form-control input-sm"></div></div>
	</div>
	<div class="form-group"><div class="col-sm-12"><?php echo epc_erp_dim_render_fields($db_link, array(), array('layout' => 'inline')); ?></div></div>
	<div class="form-group"><div class="col-sm-12"><button type="submit" class="btn btn-sm btn-primary"><i class="fa fa-plus"></i> Create account</button></div></div>
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
	<?php echo epc_erp_dim_render_fields($db_link, array(), array('layout' => 'inline')); ?>
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
	<div class="form-group"><label class="col-sm-3">Settle invoices</label><div class="col-sm-9">
		<button type="button" class="btn btn-default btn-xs epc-settle-load" data-doc="ar" data-party="user_id" data-grid="epc_rv_alloc">Load open invoices</button>
		<label class="checkbox-inline" style="margin-left:8px;"><input type="checkbox" name="auto_allocate" value="1"> Auto-allocate (oldest first)</label>
		<div id="epc_rv_alloc" class="epc-settle-grid" data-doc="ar" style="margin-top:8px;"></div>
		<p class="help-block" style="margin:4px 0 0;">Leave empty for an advance/on-account receipt; allocate to knock off specific invoices.</p>
	</div></div>
	<div class="form-group"><label class="col-sm-3">Sales order (opt.)</label><div class="col-sm-9"><input type="number" name="sales_order_id" class="form-control input-sm" placeholder="ERP SO id for VAT link"></div></div>
	<?php echo epc_erp_dim_render_fields($db_link); ?>
	<div class="form-group"><div class="col-sm-offset-3 col-sm-9">
	<label class="checkbox-inline"><input type="checkbox" name="is_advance" value="1" checked> UAE advance receipt (VAT + liability GL 2050) — ignored when invoices are allocated</label>
	<label class="checkbox-inline"><input type="checkbox" name="post_gl" value="1" checked> Post to GL</label>
	<button type="submit" class="btn btn-primary btn-sm">Post RV voucher</button></div></div>
</form>
<form id="epc_erp_form_payment_voucher" class="form-horizontal" style="margin-top:14px;max-width:760px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<h5>Payment voucher (PV-)</h5>
	<div class="form-group"><label class="col-sm-3">Supplier</label><div class="col-sm-9"><select name="supplier_id" class="form-control input-sm" required><option value="">—</option>
	<?php foreach ($suppliersCash as $s): ?><option value="<?php echo (int)$s['id']; ?>"><?php echo epc_erp_h($s['name']); ?></option><?php endforeach; ?>
	</select></div></div>
	<div class="form-group"><label class="col-sm-3">Bank account</label><div class="col-sm-9"><select name="account_id" class="form-control input-sm" required><option value="">—</option>
	<?php foreach ($accounts as $a): ?><option value="<?php echo (int)$a['id']; ?>"><?php echo epc_erp_h($a['name']); ?></option><?php endforeach; ?>
	</select></div></div>
	<div class="form-group"><label class="col-sm-3">Amount AED</label><div class="col-sm-9"><input type="number" step="0.01" name="amount" class="form-control input-sm" required></div></div>
	<div class="form-group"><label class="col-sm-3">Settle bills</label><div class="col-sm-9">
		<button type="button" class="btn btn-default btn-xs epc-settle-load" data-doc="ap" data-party="supplier_id" data-grid="epc_pv_alloc">Load open bills</button>
		<label class="checkbox-inline" style="margin-left:8px;"><input type="checkbox" name="auto_allocate" value="1"> Auto-allocate (oldest first)</label>
		<div id="epc_pv_alloc" class="epc-settle-grid" data-doc="ap" style="margin-top:8px;"></div>
		<p class="help-block" style="margin:4px 0 0;">Leave empty for an advance/on-account payment; allocate to knock off specific bills.</p>
	</div></div>
	<?php echo epc_erp_dim_render_fields($db_link); ?>
	<div class="form-group"><div class="col-sm-offset-3 col-sm-9">
	<button type="submit" class="btn btn-primary btn-sm">Post PV voucher</button></div></div>
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
	</div></div>
	<?php echo epc_erp_dim_render_fields($db_link); ?>
	<div class="form-group"><div class="col-sm-offset-3 col-sm-9"><button type="submit" class="btn btn-default btn-sm">Post TV transfer</button></div></div>
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

if ($erpOnlyCash):
?>
<script>
(function(){
	function fmt(n){ return (Math.round((parseFloat(n)||0)*100)/100).toFixed(2); }
	function post(action, params){
		var url = window.epcErpPostUrl || '';
		var fd = new FormData();
		fd.append('action', action);
		var csrf = document.querySelector('input[name="csrf_guard_key"]');
		if (csrf) fd.append('csrf_guard_key', csrf.value);
		Object.keys(params).forEach(function(k){ fd.append(k, params[k]); });
		return fetch(url, { method: 'POST', body: fd, credentials: 'same-origin' }).then(function(r){ return r.json(); });
	}
	function render(grid, docs){
		var isAr = grid.getAttribute('data-doc') === 'ar';
		if (!docs || !docs.length){
			grid.innerHTML = '<p class="text-muted" style="margin:4px 0;">No open ' + (isAr ? 'invoices' : 'bills') + ' for this party.</p>';
			return;
		}
		var h = '<table class="table table-condensed" style="margin-bottom:4px;"><thead><tr>'
			+ '<th></th><th>' + (isAr ? 'Invoice' : 'Bill') + '</th><th>Date</th><th class="text-right">Outstanding</th><th class="text-right">Allocate</th></tr></thead><tbody>';
		docs.forEach(function(d){
			var dt = d.payment_due_date && parseInt(d.payment_due_date,10) > 0 ? d.payment_due_date : (d.issue_date || d.purchase_date || 0);
			var dstr = parseInt(dt,10) > 0 ? new Date(parseInt(dt,10)*1000).toISOString().slice(0,10) : '—';
			var num = d.invoice_number || ('#' + d.id);
			h += '<tr>'
				+ '<td><input type="checkbox" class="epc-alloc-chk" data-id="' + d.id + '"></td>'
				+ '<td>' + num + '</td><td>' + dstr + '</td>'
				+ '<td class="text-right">' + fmt(d.outstanding) + '</td>'
				+ '<td class="text-right"><input type="hidden" name="alloc_invoice_id[]" value="' + d.id + '" disabled>'
				+ '<input type="number" step="0.01" name="alloc_amount[]" class="form-control input-sm epc-alloc-amt" style="width:120px;display:inline-block;" data-out="' + fmt(d.outstanding) + '" value="' + fmt(d.outstanding) + '" disabled></td>'
				+ '</tr>';
		});
		h += '</tbody></table>';
		grid.innerHTML = h;
		grid.querySelectorAll('.epc-alloc-chk').forEach(function(chk){
			chk.addEventListener('change', function(){
				var row = chk.closest('tr');
				var idIn = row.querySelector('input[name="alloc_invoice_id[]"]');
				var amtIn = row.querySelector('input[name="alloc_amount[]"]');
				idIn.disabled = !chk.checked;
				amtIn.disabled = !chk.checked;
			});
		});
	}
	document.querySelectorAll('.epc-settle-load').forEach(function(btn){
		btn.addEventListener('click', function(){
			var form = btn.closest('form');
			var partyField = btn.getAttribute('data-party');
			var docType = btn.getAttribute('data-doc');
			var grid = document.getElementById(btn.getAttribute('data-grid'));
			var sel = form.querySelector('[name="' + partyField + '"]');
			var cp = sel ? sel.value : '';
			if (!cp){ grid.innerHTML = '<p class="text-danger" style="margin:4px 0;">Pick a ' + (docType === 'ar' ? 'customer' : 'supplier') + ' first.</p>'; return; }
			grid.innerHTML = '<p class="text-muted" style="margin:4px 0;">Loading…</p>';
			post('settlement_open_docs', { doc_type: docType, counterparty_id: cp }).then(function(j){
				render(grid, (j && j.docs) ? j.docs : []);
			}).catch(function(){ grid.innerHTML = '<p class="text-danger">Failed to load.</p>'; });
		});
	});
})();
</script>
<?php
endif;
