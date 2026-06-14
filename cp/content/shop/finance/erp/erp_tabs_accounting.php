<?php
/**
 * ERP tabs: COA, General Ledger, P&L, Balance Sheet.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

$coa_list = epc_erp_gl_list_coa($db_link);
$gl_journals = epc_erp_gl_list_journals($db_link, $date_from, $date_to);
$pl = epc_erp_gl_pl_report($db_link, $date_from, $date_to);
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_tax_compliance.php';
$uae_ct = epc_uae_corporate_tax_report($db_link, $date_from, $date_to);
$bs = epc_erp_gl_balance_sheet($db_link, $date_to);
$trial = epc_erp_gl_trial_balance($db_link, $date_to);
$view_journal = isset($_GET['journal_id']) ? (int)$_GET['journal_id'] : 0;
$journal_lines = $view_journal > 0 ? epc_erp_gl_journal_lines($db_link, $view_journal) : array();
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_fiscal_periods.php';
$fiscal_lock = epc_erp_fiscal_lock_date($db_link);
$fiscal_lock_str = $fiscal_lock > 0 ? date('Y-m-d', $fiscal_lock) : '';
?>

<?php if ($tab === 'coa'): ?>
	<div class="epc-erp-section">
		<h4><i class="fa fa-list"></i> Chart of accounts (COA)</h4>
		<p class="text-muted">Standard UAE parts-trader accounts. Assets 1xxx, Liabilities 2xxx, Equity 3xxx, Revenue 4xxx, Expenses 5xxx/6xxx.</p>
		<table class="table table-striped table-bordered table-condensed">
			<thead><tr><th>Code</th><th>Name</th><th>Type</th><th>Normal</th><th>Opening</th><th>Balance</th></tr></thead>
			<tbody>
			<?php foreach ($coa_list as $a): ?>
				<tr>
					<td><strong><?php echo epc_erp_h($a['code']); ?></strong></td>
					<td><?php echo epc_erp_h($a['name']); ?></td>
					<td><?php echo epc_erp_h($a['account_type']); ?></td>
					<td><?php echo epc_erp_h($a['normal_side']); ?></td>
					<td><?php echo epc_erp_money($a['opening_balance']); ?></td>
					<td><strong><?php echo epc_erp_money($a['balance']); ?></strong></td>
				</tr>
			<?php endforeach; ?>
			</tbody>
		</table>

		<h4>Add COA account</h4>
		<form id="epc_erp_form_coa" class="form-inline">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
			<input type="text" name="code" class="form-control input-sm" placeholder="Code e.g. 6200" required>
			<input type="text" name="name" class="form-control input-sm" placeholder="Account name" required>
			<select name="account_type" class="form-control input-sm">
				<?php foreach (epc_erp_gl_account_types() as $k => $lbl): ?>
					<option value="<?php echo epc_erp_h($k); ?>"><?php echo epc_erp_h($lbl); ?></option>
				<?php endforeach; ?>
			</select>
			<input type="number" step="0.01" name="opening_balance" class="form-control input-sm" placeholder="Opening balance" value="0">
			<button type="submit" class="btn btn-sm btn-primary">Add account</button>
		</form>
	</div>

<?php elseif ($tab === 'gl'): ?>
	<div class="epc-erp-section">
		<h4><i class="fa fa-book"></i> General journal &amp; ledger</h4>
		<?php
		erp_d365_assets();
		erp_action_pane(array(
			array('label' => 'New', 'buttons' => array(
				array('label' => 'Journal entry', 'icon' => 'fa-plus', 'class' => 'is-primary', 'target' => '#epc_erp_form_gl_manual'),
			)),
			array('label' => 'Post', 'buttons' => array(
				array('label' => 'Post journal', 'icon' => 'fa-check', 'target' => '#epc_erp_form_gl_manual'),
				array('label' => 'Sync to GL', 'icon' => 'fa-refresh', 'target' => '#epc_erp_gl_sync'),
				array('label' => 'Post sales to GL', 'icon' => 'fa-shopping-cart', 'target' => '#epc_erp_gl_post_sales'),
			)),
			array('label' => 'Period', 'buttons' => array(
				array('label' => 'Period close', 'icon' => 'fa-lock', 'target' => '#epc_erp_form_fiscal_lock'),
				array('label' => 'FX revaluation', 'icon' => 'fa-exchange', 'target' => '#epc_erp_form_fx_reval'),
			)),
			array('label' => 'View', 'buttons' => array(
				array('label' => 'Refresh', 'icon' => 'fa-refresh', 'url' => epc_erp_tab_url($erpUrl, 'gl', $date_from_str, $date_to_str)),
			)),
		));
		erp_fasttab_open('Period controls — close & FX revaluation', array('open' => false, 'icon' => 'fa-sliders'));
		?>
		<div class="well well-sm" style="margin-bottom:12px;">
			<form id="epc_erp_form_fiscal_lock" class="form-inline">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
				<strong><i class="fa fa-lock"></i> Period close</strong>
				<?php if ($fiscal_lock > 0): ?>
					<span class="label label-warning" style="margin:0 6px;">Locked up to <?php echo epc_erp_h($fiscal_lock_str); ?></span>
				<?php else: ?>
					<span class="label label-default" style="margin:0 6px;">No lock — all periods open</span>
				<?php endif; ?>
				<label style="margin-left:6px;">Lock on/before
					<input type="date" name="lock_date" class="form-control input-sm" value="<?php echo epc_erp_h($fiscal_lock_str); ?>">
				</label>
				<button type="submit" class="btn btn-sm btn-warning">Set lock</button>
				<button type="button" class="btn btn-sm btn-default" id="epc_erp_fiscal_clear">Clear lock</button>
				<span class="text-muted" style="display:block;margin-top:4px;">Journals dated on or before the lock date are rejected at posting. Corrections use reversals, not edits.</span>
			</form>
		</div>
		<div class="well well-sm" style="margin-bottom:12px;">
			<form id="epc_erp_form_fx_reval" class="form-inline">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
				<strong><i class="fa fa-exchange"></i> FX revaluation</strong>
				<label style="margin-left:6px;">As of
					<input type="date" name="as_of" class="form-control input-sm" value="<?php echo epc_erp_h(date('Y-m-d')); ?>">
				</label>
				<label class="checkbox-inline" style="margin-left:6px;"><input type="checkbox" name="auto_reverse" checked> Auto-reverse next period</label>
				<button type="button" class="btn btn-sm btn-default" id="epc_erp_fx_preview">Preview</button>
				<button type="submit" class="btn btn-sm btn-info">Post revaluation</button>
				<span class="text-muted" style="display:block;margin-top:4px;">Retranslates open foreign-currency receivables at the closing rate (IAS-21); posts the unrealised FX gain/loss and an optional auto-reversal.</span>
				<div id="epc_erp_fx_preview_out" style="margin-top:6px;"></div>
			</form>
		</div>
		<?php erp_fasttab_close(); ?>
		<p>
			<button type="button" class="btn btn-sm btn-default" id="epc_erp_gl_sync"><i class="fa fa-refresh"></i> Sync unposted purchases &amp; cash to GL</button>
			<button type="button" class="btn btn-sm btn-primary" id="epc_erp_gl_post_sales"><i class="fa fa-shopping-cart"></i> Post sales orders to GL (date range)</button>
		</p>
		<table class="table table-striped table-bordered table-condensed epc-erp-table">
			<thead><tr><th>Journal no.</th><th>Date</th><th>Source</th><th>Reference</th><th>Description</th><th class="num">Amount</th><th></th></tr></thead>
			<tbody>
			<?php foreach ($gl_journals as $j): ?>
				<tr>
					<td><?php echo epc_erp_h($j['journal_no']); ?></td>
					<td><?php echo epc_erp_h(date('Y-m-d', (int)$j['journal_date'])); ?></td>
					<td><?php echo epc_erp_h($j['source_type']); ?></td>
					<td><?php echo epc_erp_h($j['reference']); ?></td>
					<td><?php echo epc_erp_h($j['description']); ?></td>
					<td class="num"><?php echo epc_erp_money($j['total_debit']); ?></td>
					<td>
						<a class="btn btn-xs btn-default" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'gl', $date_from_str, $date_to_str) . '&journal_id=' . (int)$j['id']); ?>">Lines</a>
						<button type="button" class="btn btn-xs btn-warning epc-erp-gl-reverse" data-journal-id="<?php echo (int)$j['id']; ?>" data-journal-no="<?php echo epc_erp_h($j['journal_no']); ?>" title="Post a reversing journal">Reverse</button>
					</td>
				</tr>
			<?php endforeach; ?>
			</tbody>
		</table>

		<?php if ($view_journal > 0): ?>
			<h4>Journal lines #<?php echo (int)$view_journal; ?></h4>
			<table class="table table-bordered table-condensed epc-erp-table">
				<thead><tr><th>Code</th><th>Account</th><th>Type</th><th class="num">Debit</th><th class="num">Credit</th><th>Note</th></tr></thead>
				<tbody>
				<?php foreach ($journal_lines as $ln): ?>
					<tr>
						<td><?php echo epc_erp_h($ln['coa_code']); ?></td>
						<td><?php echo epc_erp_h($ln['coa_name']); ?></td>
						<td><?php echo epc_erp_h($ln['account_type']); ?></td>
						<td class="num"><?php echo epc_erp_money($ln['debit']); ?></td>
						<td class="num"><?php echo epc_erp_money($ln['credit']); ?></td>
						<td><?php echo epc_erp_h($ln['line_note']); ?></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
		<?php endif; ?>

		<?php erp_fasttab_open('Manual journal entry (double-entry)', array('open' => true, 'icon' => 'fa-plus')); ?>
		<form id="epc_erp_form_gl_manual">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
			<div class="form-inline" style="margin-bottom:8px;">
				<input type="date" name="journal_date" class="form-control input-sm" value="<?php echo epc_erp_h($date_to_str); ?>">
				<input type="text" name="reference" class="form-control input-sm" placeholder="Reference">
				<input type="text" name="description" class="form-control input-sm" placeholder="Description" style="min-width:220px;">
			</div>
			<table class="table table-condensed" id="epc_gl_lines_table">
				<thead><tr><th>Account</th><th>Debit</th><th>Credit</th><th>Note</th></tr></thead>
				<tbody>
					<tr class="epc-gl-line">
						<td><select name="coa_id[]" class="form-control input-sm"><?php foreach ($coa_list as $a): ?><option value="<?php echo (int)$a['id']; ?>"><?php echo epc_erp_h($a['code'] . ' — ' . $a['name']); ?></option><?php endforeach; ?></select></td>
						<td><input type="number" step="0.01" name="debit[]" class="form-control input-sm" value="0"></td>
						<td><input type="number" step="0.01" name="credit[]" class="form-control input-sm" value="0"></td>
						<td><input type="text" name="line_note[]" class="form-control input-sm"></td>
					</tr>
					<tr class="epc-gl-line">
						<td><select name="coa_id[]" class="form-control input-sm"><?php foreach ($coa_list as $a): ?><option value="<?php echo (int)$a['id']; ?>"><?php echo epc_erp_h($a['code'] . ' — ' . $a['name']); ?></option><?php endforeach; ?></select></td>
						<td><input type="number" step="0.01" name="debit[]" class="form-control input-sm" value="0"></td>
						<td><input type="number" step="0.01" name="credit[]" class="form-control input-sm" value="0"></td>
						<td><input type="text" name="line_note[]" class="form-control input-sm"></td>
					</tr>
				</tbody>
			</table>
			<button type="button" class="btn btn-xs btn-default" id="epc_gl_add_line">+ Add line</button>
			<button type="submit" class="btn btn-sm btn-success">Post journal</button>
		</form>
		<?php erp_fasttab_close(); ?>
	</div>

<?php elseif ($tab === 'pl'): ?>
	<div class="epc-erp-section">
		<h4><i class="fa fa-line-chart"></i> Profit &amp; loss statement</h4>
		<p class="text-muted">Period: <?php echo epc_erp_h(date('d M Y', $date_from)); ?> — <?php echo epc_erp_h(date('d M Y', $date_to)); ?></p>
		<h5>Revenue</h5>
		<table class="table table-bordered table-condensed">
			<tbody>
			<?php foreach ($pl['revenue'] as $r): ?>
				<tr><td><?php echo epc_erp_h($r['code'] . ' ' . $r['name']); ?></td><td class="text-right"><?php echo epc_erp_money($r['amount']); ?></td></tr>
			<?php endforeach; ?>
				<tr class="active"><td><strong>Total revenue</strong></td><td class="text-right"><strong><?php echo epc_erp_money($pl['total_revenue']); ?> AED</strong></td></tr>
			</tbody>
		</table>
		<h5>Expenses</h5>
		<table class="table table-bordered table-condensed">
			<tbody>
			<?php foreach ($pl['expenses'] as $r): ?>
				<tr><td><?php echo epc_erp_h($r['code'] . ' ' . $r['name']); ?></td><td class="text-right"><?php echo epc_erp_money($r['amount']); ?></td></tr>
			<?php endforeach; ?>
				<tr class="active"><td><strong>Total expenses</strong></td><td class="text-right"><strong><?php echo epc_erp_money($pl['total_expenses']); ?> AED</strong></td></tr>
			</tbody>
		</table>
		<div class="well well-sm" style="text-align:right;font-size:16px;">
			<strong>Net profit / (loss) before CT:</strong>
			<span style="color:<?php echo $pl['net_profit'] >= 0 ? '#166534' : '#b91c1c'; ?>;">
				<?php echo epc_erp_money($pl['net_profit']); ?> AED
			</span>
		</div>
		<?php if (!empty($uae_ct['enabled'])): ?>
		<table class="table table-bordered table-condensed">
			<tbody>
				<tr>
					<td>Accounting profit (GL net profit)</td>
					<td class="text-right"><?php echo epc_erp_money($uae_ct['accounting_profit'] ?? $uae_ct['taxable_profit']); ?></td>
				</tr>
				<?php if (!empty($uae_ct['adjustment_lines'])): ?>
				<?php foreach ($uae_ct['adjustment_lines'] as $adj): ?>
				<tr>
					<td><?php echo epc_erp_h($adj['label']); ?> (<?php echo $adj['direction'] === 'add' ? 'add-back' : 'deduct'; ?>)</td>
					<td class="text-right"><?php echo $adj['direction'] === 'add' ? '+' : '−'; ?> <?php echo epc_erp_money($adj['amount']); ?></td>
				</tr>
				<?php endforeach; ?>
				<tr>
					<td><strong>Adjusted taxable profit</strong></td>
					<td class="text-right"><strong><?php echo epc_erp_money($uae_ct['adjusted_taxable_profit'] ?? $uae_ct['taxable_profit']); ?></strong></td>
				</tr>
				<?php endif; ?>
				<tr>
					<td>Less: small business threshold (not taxed)</td>
					<td class="text-right"><?php echo epc_erp_money($uae_ct['small_business_threshold_aed']); ?></td>
				</tr>
				<tr>
					<td>Profit above threshold</td>
					<td class="text-right"><?php echo epc_erp_money($uae_ct['profit_above_threshold']); ?></td>
				</tr>
				<tr class="active">
					<td><strong>UAE Corporate Tax provision (<?php echo epc_erp_h(number_format($uae_ct['rate_percent'], 2)); ?>%)</strong></td>
					<td class="text-right"><strong style="color:#b91c1c;"><?php echo epc_erp_money($uae_ct['corporate_tax_provision']); ?> AED</strong></td>
				</tr>
				<tr class="success">
					<td><strong>Net profit after Corporate Tax</strong></td>
					<td class="text-right"><strong><?php echo epc_erp_money($uae_ct['profit_after_corporate_tax']); ?> AED</strong></td>
				</tr>
			</tbody>
		</table>
		<p class="text-muted"><?php echo epc_erp_h($uae_ct['note']); ?>
			<a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'tax_compliance', $date_from_str, $date_to_str, 'finance')); ?>">Tax compliance guide</a></p>
		<?php endif; ?>
		<p class="text-muted">Post sales and purchases to GL first (GL tab) for complete P&amp;L. COGS comes from purchase invoices; revenue from sales order posting.</p>
	</div>

<?php elseif ($tab === 'balance_sheet'): ?>
	<div class="epc-erp-section">
		<h4><i class="fa fa-balance-scale"></i> Balance sheet</h4>
		<p class="text-muted">As of <?php echo epc_erp_h(date('d M Y', $date_to)); ?></p>
		<div class="row">
			<div class="col-md-4">
				<h5>Assets</h5>
				<table class="table table-bordered table-condensed">
					<tbody>
					<?php foreach ($bs['assets'] as $r): ?>
						<tr><td><?php echo epc_erp_h($r['code'] . ' ' . $r['name']); ?></td><td class="text-right"><?php echo epc_erp_money($r['balance']); ?></td></tr>
					<?php endforeach; ?>
						<tr class="active"><td><strong>Total assets</strong></td><td class="text-right"><strong><?php echo epc_erp_money($bs['total_assets']); ?></strong></td></tr>
					</tbody>
				</table>
			</div>
			<div class="col-md-4">
				<h5>Liabilities</h5>
				<table class="table table-bordered table-condensed">
					<tbody>
					<?php foreach ($bs['liabilities'] as $r): ?>
						<tr><td><?php echo epc_erp_h($r['code'] . ' ' . $r['name']); ?></td><td class="text-right"><?php echo epc_erp_money($r['balance']); ?></td></tr>
					<?php endforeach; ?>
						<tr class="active"><td><strong>Total liabilities</strong></td><td class="text-right"><strong><?php echo epc_erp_money($bs['total_liabilities']); ?></strong></td></tr>
					</tbody>
				</table>
			</div>
			<div class="col-md-4">
				<h5>Equity</h5>
				<table class="table table-bordered table-condensed">
					<tbody>
					<?php foreach ($bs['equity'] as $r): ?>
						<tr><td><?php echo epc_erp_h($r['code'] . ' ' . $r['name']); ?></td><td class="text-right"><?php echo epc_erp_money($r['balance']); ?></td></tr>
					<?php endforeach; ?>
						<tr><td>Current period earnings (P&amp;L)</td><td class="text-right"><?php echo epc_erp_money($bs['current_earnings']); ?></td></tr>
						<tr class="active"><td><strong>Total equity</strong></td><td class="text-right"><strong><?php echo epc_erp_money($bs['total_equity']); ?></strong></td></tr>
					</tbody>
				</table>
			</div>
		</div>
		<div class="well well-sm">
			<strong>Assets</strong> <?php echo epc_erp_money($bs['total_assets']); ?> AED =
			<strong>Liabilities + Equity</strong> <?php echo epc_erp_money($bs['total_liabilities_equity']); ?> AED
			<?php if (abs($bs['total_assets'] - $bs['total_liabilities_equity']) > 0.05): ?>
				<span class="text-danger"> (difference <?php echo epc_erp_money($bs['total_assets'] - $bs['total_liabilities_equity']); ?> — check GL postings)</span>
			<?php else: ?>
				<span class="text-success"> ✓ Balanced</span>
			<?php endif; ?>
		</div>
		<h5>Trial balance (summary)</h5>
		<table class="table table-striped table-condensed table-bordered">
			<thead><tr><th>Code</th><th>Account</th><th>Debit</th><th>Credit</th></tr></thead>
			<tbody>
			<?php foreach ($trial['rows'] as $tr): ?>
				<tr>
					<td><?php echo epc_erp_h($tr['code']); ?></td>
					<td><?php echo epc_erp_h($tr['name']); ?></td>
					<td class="text-right"><?php echo $tr['debit'] > 0 ? epc_erp_money($tr['debit']) : '—'; ?></td>
					<td class="text-right"><?php echo $tr['credit'] > 0 ? epc_erp_money($tr['credit']) : '—'; ?></td>
				</tr>
			<?php endforeach; ?>
				<tr class="active"><td colspan="2"><strong>Totals</strong></td><td class="text-right"><strong><?php echo epc_erp_money($trial['total_debit']); ?></strong></td><td class="text-right"><strong><?php echo epc_erp_money($trial['total_credit']); ?></strong></td></tr>
			</tbody>
		</table>
	</div>
<?php endif; ?>

<script>
(function(){
	var erpPostUrl = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	var msgEl = document.getElementById('epc_erp_msg');
	function showMsg(ok, text) {
		if (!msgEl) return;
		msgEl.className = 'alert epc-erp-msg ' + (ok ? 'alert-success' : 'alert-danger');
		msgEl.textContent = text;
		msgEl.style.display = 'block';
	}
	function postAction(action, form) {
		var fd = new FormData(form);
		fd.append('action', action);
		return fetch(erpPostUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(function(r){ return r.json(); })
			.then(function(j){
				showMsg(!!j.status, j.message || (j.status ? 'OK' : 'Error'));
				if (j.status) setTimeout(function(){ location.reload(); }, 800);
			});
	}
	function bindCoaForm() {
		var f = document.getElementById('epc_erp_form_coa');
		if (f) f.addEventListener('submit', function(ev){ ev.preventDefault(); postAction('create_coa', f); });
	}
	function bindGlManual() {
		var f = document.getElementById('epc_erp_form_gl_manual');
		if (!f) return;
		f.addEventListener('submit', function(ev){
			ev.preventDefault();
			var lines = [];
			var coa = f.querySelectorAll('select[name="coa_id[]"]');
			var dr = f.querySelectorAll('input[name="debit[]"]');
			var cr = f.querySelectorAll('input[name="credit[]"]');
			var nt = f.querySelectorAll('input[name="line_note[]"]');
			for (var i = 0; i < coa.length; i++) {
				lines.push({ coa_id: coa[i].value, debit: dr[i].value, credit: cr[i].value, line_note: nt[i].value });
			}
			var fd = new FormData(f);
			fd.append('action', 'gl_manual_entry');
			fd.append('lines_json', JSON.stringify(lines));
			fetch(erpPostUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function(r){ return r.json(); })
				.then(function(j){ showMsg(!!j.status, j.message || 'Done'); if (j.status) setTimeout(function(){ location.reload(); }, 800); });
		});
		var addBtn = document.getElementById('epc_gl_add_line');
		if (addBtn) {
			addBtn.addEventListener('click', function(){
				var tbody = document.querySelector('#epc_gl_lines_table tbody');
				var first = tbody.querySelector('.epc-gl-line');
				if (first) tbody.appendChild(first.cloneNode(true));
			});
		}
	}
	function bindGlButtons() {
		var sync = document.getElementById('epc_erp_gl_sync');
		if (sync) sync.addEventListener('click', function(){
			var fd = new FormData();
			fd.append('action', 'gl_sync_unposted');
			var csrf = document.querySelector('input[name="csrf_guard_key"]');
			if (csrf) fd.append('csrf_guard_key', csrf.value);
			fetch(erpPostUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function(r){ return r.json(); })
				.then(function(j){ showMsg(!!j.status, j.message); if (j.status) setTimeout(function(){ location.reload(); }, 800); });
		});
		var ps = document.getElementById('epc_erp_gl_post_sales');
		if (ps) ps.addEventListener('click', function(){
			var fd = new FormData();
			fd.append('action', 'gl_post_sales');
			fd.append('date_from', '<?php echo epc_erp_h($date_from_str); ?>');
			fd.append('date_to', '<?php echo epc_erp_h($date_to_str); ?>');
			var csrf = document.querySelector('input[name="csrf_guard_key"]');
			if (csrf) fd.append('csrf_guard_key', csrf.value);
			fetch(erpPostUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function(r){ return r.json(); })
				.then(function(j){ showMsg(!!j.status, j.message); if (j.status) setTimeout(function(){ location.reload(); }, 800); });
		});
	}
	function bindFiscalLock() {
		var f = document.getElementById('epc_erp_form_fiscal_lock');
		if (f) f.addEventListener('submit', function(ev){ ev.preventDefault(); postAction('fiscal_set_lock', f); });
		var clr = document.getElementById('epc_erp_fiscal_clear');
		if (clr) clr.addEventListener('click', function(){
			if (!confirm('Clear the fiscal lock and re-open all periods?')) return;
			var fd = new FormData();
			fd.append('action', 'fiscal_set_lock');
			fd.append('lock_date', '');
			var csrf = document.querySelector('input[name="csrf_guard_key"]');
			if (csrf) fd.append('csrf_guard_key', csrf.value);
			fetch(erpPostUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function(r){ return r.json(); })
				.then(function(j){ showMsg(!!j.status, j.message); if (j.status) setTimeout(function(){ location.reload(); }, 800); });
		});
	}
	function bindGlReverse() {
		var btns = document.querySelectorAll('.epc-erp-gl-reverse');
		for (var i = 0; i < btns.length; i++) {
			btns[i].addEventListener('click', function(){
				var id = this.getAttribute('data-journal-id');
				var no = this.getAttribute('data-journal-no') || ('#' + id);
				if (!confirm('Post a reversing journal for ' + no + '? The original stays on record.')) return;
				var fd = new FormData();
				fd.append('action', 'gl_reverse_journal');
				fd.append('journal_id', id);
				var csrf = document.querySelector('input[name="csrf_guard_key"]');
				if (csrf) fd.append('csrf_guard_key', csrf.value);
				fetch(erpPostUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
					.then(function(r){ return r.json(); })
					.then(function(j){ showMsg(!!j.status, j.message); if (j.status) setTimeout(function(){ location.reload(); }, 1000); });
			});
		}
	}
	function bindFxReval() {
		var form = document.getElementById('epc_erp_form_fx_reval');
		if (!form) return;
		var out = document.getElementById('epc_erp_fx_preview_out');
		function run(action) {
			var fd = new FormData();
			fd.append('action', action);
			fd.append('as_of', (form.querySelector('input[name="as_of"]') || {}).value || '');
			fd.append('auto_reverse', form.querySelector('input[name="auto_reverse"]').checked ? '1' : '0');
			var csrf = document.querySelector('input[name="csrf_guard_key"]');
			if (csrf) fd.append('csrf_guard_key', csrf.value);
			fetch(erpPostUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function(r){ return r.json(); })
				.then(function(j){
					if (action === 'fx_revaluation_preview') {
						if (j.status && j.by_currency) {
							var h = '<table class="table table-condensed table-bordered" style="margin:6px 0;font-size:12px;"><thead><tr><th>Currency</th><th>Open docs</th><th>Outstanding (FC)</th><th>Booked (base)</th><th>Closing (base)</th><th>Unrealised</th></tr></thead><tbody>';
							if (!j.by_currency.length) { h += '<tr><td colspan="6" class="text-muted">No open foreign-currency receivables (or no rates set).</td></tr>'; }
							j.by_currency.forEach(function(c){
								h += '<tr><td>'+c.currency+'</td><td>'+c.count+'</td><td style="text-align:right;">'+Number(c.outstanding_fc).toFixed(2)+'</td><td style="text-align:right;">'+Number(c.booked_base).toFixed(2)+'</td><td style="text-align:right;">'+Number(c.current_base).toFixed(2)+'</td><td style="text-align:right;'+(c.unrealised>=0?'color:green;':'color:#c00;')+'">'+Number(c.unrealised).toFixed(2)+'</td></tr>';
							});
							h += '</tbody><tfoot><tr><th colspan="5" style="text-align:right;">Net unrealised ('+j.base+')</th><th style="text-align:right;'+(j.total_unrealised>=0?'color:green;':'color:#c00;')+'">'+Number(j.total_unrealised).toFixed(2)+'</th></tr></tfoot></table>';
							out.innerHTML = h;
						} else { out.innerHTML = '<span class="text-muted">'+(j.message||'No data')+'</span>'; }
					} else {
						showMsg(!!j.status, j.message);
						if (j.status) setTimeout(function(){ location.reload(); }, 1200);
					}
				});
		}
		var pv = document.getElementById('epc_erp_fx_preview');
		if (pv) pv.addEventListener('click', function(){ run('fx_revaluation_preview'); });
		form.addEventListener('submit', function(ev){
			ev.preventDefault();
			if (!confirm('Post the FX revaluation to the GL?')) return;
			run('fx_post_revaluation');
		});
	}
	bindCoaForm();
	bindGlManual();
	bindGlButtons();
	bindFiscalLock();
	bindGlReverse();
	bindFxReval();
})();
</script>
