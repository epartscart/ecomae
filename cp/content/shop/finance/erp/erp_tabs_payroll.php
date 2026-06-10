<?php
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_payroll.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_staff.php';

$runs = epc_erp_payroll_list_runs($db_link);
$report = epc_erp_payroll_report($db_link, 2026);
$accounts = epc_erp_list_cash_accounts($db_link);
$viewRunId = isset($_GET['run_id']) ? (int)$_GET['run_id'] : 0;
$viewRun = null;
if ($viewRunId > 0) {
	$st = $db_link->prepare('SELECT * FROM `epc_erp_payroll_runs` WHERE `id` = ? LIMIT 1');
	$st->execute(array($viewRunId));
	$viewRun = $st->fetch(PDO::FETCH_ASSOC);
}
$viewLines = $viewRunId > 0 ? epc_erp_payroll_run_lines($db_link, $viewRunId) : array();
$viewEditable = $viewRun && $viewRun['status'] !== 'paid';
$csrfLocal = isset($csrf) ? $csrf : '';
$periodDefault = date('Y-m');
$stdDays = epc_erp_payroll_standard_days();
?>

<div class="epc-erp-section">
	<h4><i class="fa fa-money"></i> Payroll — salary recording &amp; payment</h4>
	<p class="text-muted">
		Monthly salary is quoted for a <strong><?php echo (int)$stdDays; ?>-day</strong> month.
		Actual pay = <code>(basic + allowances) ÷ <?php echo (int)$stdDays; ?> × days worked</code>.
		Days above <?php echo (int)$stdDays; ?> are paid at the same daily rate.
		<strong>HR</strong> sets days worked → generates run → <strong>Finance</strong> approves &amp; pays →
		<strong>Accounts</strong> sees GL (Dr 6100 / Cr 1010).
	</p>

	<div class="epc-erp-kpi" style="margin-bottom:16px;">
		<div class="kpi"><div class="lbl">YTD paid (2026)</div><div class="val"><?php echo epc_erp_money($report['ytd_paid']); ?> AED</div></div>
		<div class="kpi"><div class="lbl">Payroll runs</div><div class="val"><?php echo count($runs); ?></div></div>
		<div class="kpi"><div class="lbl">Staff on payroll</div><div class="val"><?php echo count(epc_erp_hr_list($db_link)); ?></div></div>
	</div>

	<div class="row">
		<div class="col-md-4">
			<div class="well well-sm">
				<h5>1. Generate run (HR)</h5>
				<form id="epc_payroll_generate" class="form">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
					<div class="form-group">
						<label>Period (YYYY-MM)</label>
						<input type="month" name="period_label" class="form-control input-sm" value="<?php echo epc_erp_h($periodDefault); ?>" required>
					</div>
					<button type="submit" class="btn btn-primary btn-sm">Generate payroll</button>
				</form>
			</div>
		</div>
		<div class="col-md-4">
			<div class="well well-sm">
				<h5>2. Approve (Finance)</h5>
				<form id="epc_payroll_approve" class="form-inline">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
					<select name="run_id" class="form-control input-sm">
						<?php foreach ($runs as $r): ?>
							<?php if ($r['status'] === 'draft'): ?>
								<option value="<?php echo (int)$r['id']; ?>"><?php echo epc_erp_h($r['period_label']); ?> — <?php echo epc_erp_money($r['total_net']); ?> AED</option>
							<?php endif; ?>
						<?php endforeach; ?>
					</select>
					<button type="submit" class="btn btn-default btn-sm">Approve</button>
				</form>
			</div>
		</div>
		<div class="col-md-4">
			<div class="well well-sm">
				<h5>3. Pay salaries (Finance)</h5>
				<form id="epc_payroll_pay" class="form">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
					<div class="form-group">
						<select name="run_id" class="form-control input-sm">
							<?php foreach ($runs as $r): ?>
								<?php if ($r['status'] === 'approved' || $r['status'] === 'draft'): ?>
									<option value="<?php echo (int)$r['id']; ?>"><?php echo epc_erp_h($r['period_label']); ?> — <?php echo epc_erp_money($r['total_net']); ?> AED (<?php echo epc_erp_h($r['status']); ?>)</option>
								<?php endif; ?>
							<?php endforeach; ?>
						</select>
					</div>
					<div class="form-group">
						<select name="cash_account_id" class="form-control input-sm">
							<?php foreach ($accounts as $a): ?>
								<option value="<?php echo (int)$a['id']; ?>"><?php echo epc_erp_h($a['name']); ?></option>
							<?php endforeach; ?>
						</select>
					</div>
					<button type="submit" class="btn btn-success btn-sm">Pay &amp; post to bank</button>
				</form>
			</div>
		</div>
	</div>

	<h5>Payroll runs</h5>
	<table class="table table-bordered table-condensed">
		<thead><tr><th>Period</th><th>Status</th><th>Std days</th><th>Gross earned</th><th>Deductions</th><th>Net pay</th><th>Paid</th><th></th></tr></thead>
		<tbody>
		<?php foreach ($runs as $r): ?>
			<tr>
				<td><strong><?php echo epc_erp_h($r['period_label']); ?></strong></td>
				<td><span class="label label-<?php echo $r['status'] === 'paid' ? 'success' : ($r['status'] === 'approved' ? 'info' : 'default'); ?>"><?php echo epc_erp_h($r['status']); ?></span></td>
				<td><?php echo (int)($r['standard_days'] ?? $stdDays); ?></td>
				<td><?php echo epc_erp_money($r['total_gross']); ?></td>
				<td><?php echo epc_erp_money($r['total_deductions']); ?></td>
				<td><strong><?php echo epc_erp_money($r['total_net']); ?> AED</strong></td>
				<td><?php echo (int)$r['paid_at'] ? epc_erp_h(date('Y-m-d', (int)$r['paid_at'])) : '—'; ?></td>
				<td><a class="btn btn-xs btn-default" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'payroll', $date_from_str, $date_to_str) . '&run_id=' . (int)$r['id']); ?>">Lines</a></td>
			</tr>
		<?php endforeach; ?>
		<?php if (empty($runs)): ?>
			<tr><td colspan="8" class="text-muted">No payroll runs — generate one above or run staff setup with sample=1</td></tr>
		<?php endif; ?>
		</tbody>
	</table>

	<?php if ($viewRunId > 0 && !empty($viewLines)): ?>
	<h5>Salary lines — <?php echo epc_erp_h($viewRun['period_label']); ?> (<?php echo (int)($viewRun['standard_days'] ?? $stdDays); ?>-day month)</h5>
	<?php if ($viewEditable): ?>
		<p class="text-muted small">Adjust <em>days worked</em> before approve/pay — totals recalculate automatically.</p>
	<?php endif; ?>
	<table class="table table-striped table-condensed">
		<thead>
			<tr>
				<th>Employee</th>
				<th>Fixed monthly<br><small>(30-day contract)</small></th>
				<th>Daily rate</th>
				<th>Days worked</th>
				<th>Extra days</th>
				<th>Earned gross</th>
				<th>Deductions</th>
				<th>Net</th>
				<th>Bank</th>
				<th>Status</th>
			</tr>
		</thead>
		<tbody>
		<?php foreach ($viewLines as $l): ?>
			<?php
			$monthlyBasic = (float)($l['monthly_basic'] ?? 0) > 0 ? (float)$l['monthly_basic'] : (float)$l['basic_salary'];
			$monthlyAllow = (float)($l['monthly_allowances'] ?? 0) > 0 ? (float)$l['monthly_allowances'] : (float)$l['allowances'];
			$monthlyTotal = $monthlyBasic + $monthlyAllow;
			$daysWorked = (float)($l['days_worked'] ?? 30);
			$extraDays = (float)($l['extra_days'] ?? max(0, $daysWorked - $stdDays));
			$dailyRate = (float)($l['daily_rate'] ?? 0);
			if ($dailyRate <= 0 && $monthlyTotal > 0) {
				$dailyRate = $monthlyTotal / $stdDays;
			}
			$grossPay = (float)($l['gross_pay'] ?? 0) > 0 ? (float)$l['gross_pay'] : (float)$l['basic_salary'] + (float)$l['allowances'];
			?>
			<tr data-line-id="<?php echo (int)$l['id']; ?>">
				<td><?php echo epc_erp_h($l['display_name']); ?><br><small><?php echo epc_erp_h($l['job_title']); ?> · <?php echo epc_erp_h(epc_erp_staff_department_name($l['department_code'])); ?></small></td>
				<td><?php echo epc_erp_money($monthlyTotal); ?><br><small><?php echo epc_erp_money($monthlyBasic); ?> + <?php echo epc_erp_money($monthlyAllow); ?></small></td>
				<td><?php echo epc_erp_money($dailyRate); ?></td>
				<td>
					<?php if ($viewEditable): ?>
						<input type="number" class="form-control input-sm epc-payroll-days" step="0.5" min="0" max="62" value="<?php echo epc_erp_h($daysWorked); ?>" style="width:70px;">
					<?php else: ?>
						<?php echo epc_erp_h(number_format($daysWorked, 1)); ?>
					<?php endif; ?>
				</td>
				<td><?php echo $extraDays > 0 ? '<span class="text-success">+' . epc_erp_h(number_format($extraDays, 1)) . '</span>' : '—'; ?></td>
				<td><?php echo epc_erp_money($grossPay); ?></td>
				<td><?php echo epc_erp_money($l['deductions']); ?></td>
				<td><strong class="epc-payroll-net"><?php echo epc_erp_money($l['net_pay']); ?></strong></td>
				<td><small><?php echo epc_erp_h($l['bank_name']); ?><br><?php echo epc_erp_h($l['bank_account']); ?></small></td>
				<td><?php echo epc_erp_h($l['status']); ?></td>
			</tr>
		<?php endforeach; ?>
		</tbody>
	</table>
	<?php endif; ?>

	<h5>YTD by department (paid runs)</h5>
	<table class="table table-condensed table-bordered">
		<thead><tr><th>Department</th><th>Headcount (lines)</th><th>Net paid</th></tr></thead>
		<tbody>
		<?php foreach ($report['by_department'] as $d): ?>
			<tr>
				<td><?php echo epc_erp_h(epc_erp_staff_department_name($d['department_code'])); ?></td>
				<td><?php echo (int)$d['headcount']; ?></td>
				<td><?php echo epc_erp_money($d['total_net']); ?> AED</td>
			</tr>
		<?php endforeach; ?>
		<?php if (empty($report['by_department'])): ?>
			<tr><td colspan="3" class="text-muted">No paid payroll yet</td></tr>
		<?php endif; ?>
		</tbody>
	</table>
</div>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	var csrf = <?php echo json_encode($csrfLocal); ?>;
	function post(action, fd) {
		fd.append('action', action);
		return fetch(url, { method: 'POST', body: fd, credentials: 'same-origin' }).then(function(r){ return r.json(); });
	}
	function msg(j) {
		var el = document.getElementById('epc_erp_msg');
		if (!el) return;
		el.className = 'alert alert-' + (j.status ? 'success' : 'danger');
		el.textContent = j.message || '';
		el.style.display = 'block';
	}
	['epc_payroll_generate','epc_payroll_approve','epc_payroll_pay'].forEach(function(id){
		var f = document.getElementById(id);
		if (!f) return;
		f.addEventListener('submit', function(ev){
			ev.preventDefault();
			post(id.replace('epc_payroll_','payroll_'), new FormData(f)).then(function(j){ msg(j); if (j.status) setTimeout(function(){ location.reload(); }, 800); });
		});
	});
	document.querySelectorAll('.epc-payroll-days').forEach(function(inp){
		var timer;
		inp.addEventListener('change', function(){
			clearTimeout(timer);
			var row = inp.closest('tr');
			var lineId = row ? row.getAttribute('data-line-id') : 0;
			timer = setTimeout(function(){
				var fd = new FormData();
				fd.append('csrf_guard_key', csrf);
				fd.append('line_id', lineId);
				fd.append('days_worked', inp.value);
				post('payroll_update_days', fd).then(function(j){
					msg(j);
					if (j.status && j.calc) {
						var netEl = row.querySelector('.epc-payroll-net');
						if (netEl) netEl.textContent = Number(j.calc.net_pay).toFixed(2);
					}
				});
			}, 400);
		});
	});
})();
</script>
