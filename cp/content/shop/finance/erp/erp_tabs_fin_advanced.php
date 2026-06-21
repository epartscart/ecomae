<?php
defined('_ASTEXE_') or die('No access');
/**
 * Financial depth — period management, FX revaluation, ledger
 * allocation rules, and accrual schemes.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_fin_advanced.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_fin_adv_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$view = isset($_GET['fv']) ? (string) $_GET['fv'] : 'periods';
$summary = epc_fin_adv_summary($db_link, $companyId);

erp_page_header(
	'<i class="fa fa-sliders"></i> Financial depth',
	'Enterprise fiscal period management, foreign-currency revaluation, ledger allocation rules and accrual schemes.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Financial depth'),
	)
);

erp_stat_cards(array(
	array('label' => 'Open periods', 'value' => (string) $summary['open_periods']),
	array('label' => 'Closed periods', 'value' => (string) $summary['closed_periods']),
	array('label' => 'Allocation rules', 'value' => (string) $summary['alloc_rules']),
	array('label' => 'Accrual schemes', 'value' => (string) $summary['accruals']),
	array('label' => 'FX revaluations', 'value' => (string) $summary['fx_runs']),
));

$tabBase = epc_erp_tab_url($erpUrl, 'fin_advanced', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
$views = array('periods' => 'Period management', 'fx' => 'FX revaluation', 'alloc' => 'Allocations', 'accruals' => 'Accruals');
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<div class="btn-group btn-group-sm" style="margin-bottom:10px;">
	<?php foreach ($views as $k => $lbl): ?>
		<a class="btn btn-<?php echo $view === $k ? 'primary' : 'default'; ?>" href="<?php echo epc_erp_h($tabBase . $sep . 'fv=' . $k); ?>"><?php echo epc_erp_h($lbl); ?></a>
	<?php endforeach; ?>
</div>

<?php if ($view === 'periods'):
	$fy = (int) ($_GET['fy'] ?? (int) date('Y'));
	$periods = epc_fin_periods_list($db_link, $companyId, $fy); ?>
	<div class="row"><div class="col-md-4">
		<div class="well well-sm">
			<h5><i class="fa fa-calendar"></i> Generate fiscal year</h5>
			<form id="epc_fin_periods" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<div class="form-group"><label>Fiscal year</label><input type="number" name="fy" class="form-control input-sm" value="<?php echo (int) $fy; ?>" required></div>
				<div class="form-group"><label>Fiscal start month</label>
					<select name="start_month" class="form-control input-sm">
						<?php for ($m = 1; $m <= 12; $m++): ?><option value="<?php echo $m; ?>" <?php echo $m === 1 ? 'selected' : ''; ?>><?php echo date('F', mktime(0, 0, 0, $m, 1)); ?></option><?php endfor; ?>
					</select>
				</div>
				<button type="submit" class="btn btn-primary btn-sm">Generate 12 periods</button>
			</form>
		</div>
	</div><div class="col-md-8">
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Period</th><th>Start</th><th>End</th><th>Status</th><th>Set</th></tr></thead>
			<tbody>
			<?php if (empty($periods)): ?>
				<tr><td colspan="5" class="text-muted">No periods for FY <?php echo (int) $fy; ?>. Generate the calendar first.</td></tr>
			<?php else: foreach ($periods as $p):
				$st = (string) $p['status'];
				$cls = $st === 'open' ? 'success' : ($st === 'closed' ? 'default' : 'warning'); ?>
				<tr data-fy="<?php echo (int) $p['fy']; ?>" data-pno="<?php echo (int) $p['period_no']; ?>">
					<td><strong><?php echo (int) $p['fy']; ?> / P<?php echo (int) $p['period_no']; ?></strong></td>
					<td><?php echo date('d M Y', (int) $p['start_date']); ?></td>
					<td><?php echo date('d M Y', (int) $p['end_date']); ?></td>
					<td><span class="label label-<?php echo $cls; ?>"><?php echo epc_erp_h($st); ?></span></td>
					<td class="btn-group btn-group-xs">
						<button class="btn btn-success btn-xs epc-fin-pstat" data-status="open">Open</button>
						<button class="btn btn-warning btn-xs epc-fin-pstat" data-status="on_hold">Hold</button>
						<button class="btn btn-default btn-xs epc-fin-pstat" data-status="closed">Close</button>
					</td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div></div>

<?php elseif ($view === 'fx'):
	$runs = epc_fin_fx_runs($db_link, $companyId); ?>
	<div class="row"><div class="col-md-5">
		<div class="well well-sm">
			<h5><i class="fa fa-exchange"></i> Run FX revaluation</h5>
			<form id="epc_fin_fx" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<p class="text-muted" style="font-size:11px;">One open FC balance per line: <code>account|currency|fc_amount|book_lc|rate</code>. Delta = fc×rate − book (gain/loss).</p>
				<div class="form-group"><textarea name="balances" class="form-control input-sm" rows="5" placeholder="1200|USD|1000|3600|3.75&#10;2100|EUR|500|2050|4.00"></textarea></div>
				<button type="submit" class="btn btn-primary btn-sm">Revalue</button>
			</form>
		</div>
	</div><div class="col-md-7">
		<?php foreach ($runs as $run): ?>
			<div class="panel panel-default">
				<div class="panel-heading"><strong>Run #<?php echo (int) $run['id']; ?></strong> · as of <?php echo date('d M Y', (int) $run['as_of']); ?> · net delta <strong><?php echo epc_erp_money($run['total_delta'], 2); ?></strong></div>
				<table class="table table-condensed" style="margin-bottom:0;">
					<thead><tr><th>Acct</th><th>Ccy</th><th class="text-right">FC</th><th class="text-right">Book</th><th class="text-right">Rate</th><th class="text-right">Revalued</th><th class="text-right">Delta</th><th>Effect</th></tr></thead>
					<tbody>
					<?php foreach ($run['lines'] as $ln): ?>
						<tr><td><?php echo epc_erp_h($ln['account']); ?></td><td><?php echo epc_erp_h($ln['currency']); ?></td>
						<td class="text-right"><?php echo epc_erp_money($ln['fc_amount'], 2); ?></td><td class="text-right"><?php echo epc_erp_money($ln['book_lc'], 2); ?></td>
						<td class="text-right"><?php echo epc_erp_h(number_format((float) $ln['rate'], 4)); ?></td><td class="text-right"><?php echo epc_erp_money($ln['revalued_lc'], 2); ?></td>
						<td class="text-right"><?php echo epc_erp_money($ln['delta'], 2); ?></td>
						<td><span class="label label-<?php echo $ln['effect'] === 'gain' ? 'success' : 'danger'; ?>"><?php echo epc_erp_h($ln['effect']); ?></span></td></tr>
					<?php endforeach; ?>
					</tbody>
				</table>
			</div>
		<?php endforeach; ?>
		<?php if (empty($runs)): ?><p class="text-muted">No revaluation runs yet.</p><?php endif; ?>
	</div></div>

<?php elseif ($view === 'alloc'):
	$rules = epc_fin_alloc_rules($db_link, $companyId); ?>
	<div class="row"><div class="col-md-5">
		<div class="well well-sm">
			<h5><i class="fa fa-sitemap"></i> New allocation rule</h5>
			<form id="epc_fin_alloc" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<div class="row"><div class="col-xs-6 form-group"><label>Code</label><input type="text" name="code" class="form-control input-sm" required></div>
				<div class="col-xs-6 form-group"><label>Source account</label><input type="text" name="source_account" class="form-control input-sm"></div></div>
				<div class="form-group"><label>Name</label><input type="text" name="name" class="form-control input-sm"></div>
				<div class="form-group"><label>Basis (dest|weight per line)</label><textarea name="basis" class="form-control input-sm" rows="4" placeholder="DEPT-A|2&#10;DEPT-B|1"></textarea></div>
				<button type="submit" class="btn btn-primary btn-sm">Save rule</button>
			</form>
		</div>
	</div><div class="col-md-7">
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Code</th><th>Source</th><th>Destinations</th><th>Run</th></tr></thead>
			<tbody>
			<?php if (empty($rules)): ?>
				<tr><td colspan="4" class="text-muted">No allocation rules yet.</td></tr>
			<?php else: foreach ($rules as $r):
				$dests = array();
				foreach ((array) $r['basis_arr'] as $d => $w) { $dests[] = $d . ':' . $w; } ?>
				<tr><td><strong><?php echo epc_erp_h($r['code']); ?></strong><br><small class="text-muted"><?php echo epc_erp_h($r['name']); ?></small></td>
				<td><?php echo epc_erp_h($r['source_account']); ?></td>
				<td><small><?php echo epc_erp_h(implode(', ', $dests)); ?></small></td>
				<td><div class="input-group input-group-sm" style="max-width:180px;"><input type="number" step="0.01" class="form-control epc-alloc-amt" placeholder="amount"><span class="input-group-btn"><button class="btn btn-default epc-alloc-run" data-id="<?php echo (int) $r['id']; ?>">Allocate</button></span></div></td></tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div></div>

<?php else:
	$accruals = epc_fin_accruals($db_link, $companyId); ?>
	<div class="row"><div class="col-md-4">
		<div class="well well-sm">
			<h5><i class="fa fa-calendar-check-o"></i> New accrual scheme</h5>
			<form id="epc_fin_accrual" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<div class="form-group"><label>Code</label><input type="text" name="code" class="form-control input-sm" required></div>
				<div class="form-group"><label>Description</label><input type="text" name="description" class="form-control input-sm"></div>
				<div class="row"><div class="col-xs-6 form-group"><label>Total</label><input type="number" step="0.01" name="total_amount" class="form-control input-sm" required></div>
				<div class="col-xs-6 form-group"><label>Periods</label><input type="number" name="periods" class="form-control input-sm" value="12"></div></div>
				<div class="row"><div class="col-xs-6 form-group"><label>Start FY</label><input type="number" name="start_fy" class="form-control input-sm" value="<?php echo (int) date('Y'); ?>"></div>
				<div class="col-xs-6 form-group"><label>Start period</label><input type="number" name="start_period" class="form-control input-sm" value="1"></div></div>
				<button type="submit" class="btn btn-primary btn-sm">Create schedule</button>
			</form>
		</div>
	</div><div class="col-md-8">
		<?php foreach ($accruals as $a): ?>
			<div class="panel panel-default">
				<div class="panel-heading"><strong><?php echo epc_erp_h($a['code']); ?></strong> — <?php echo epc_erp_h($a['description']); ?> · total <?php echo epc_erp_money($a['total_amount'], 2); ?> over <?php echo (int) $a['periods']; ?></div>
				<table class="table table-condensed" style="margin-bottom:0;">
					<thead><tr><th>Seq</th><th>FY</th><th>Period</th><th class="text-right">Amount</th></tr></thead>
					<tbody>
					<?php foreach ((array) $a['schedule'] as $s): ?>
						<tr><td><?php echo (int) $s['seq']; ?></td><td><?php echo (int) $s['fy']; ?></td><td>P<?php echo (int) $s['period_no']; ?></td><td class="text-right"><?php echo epc_erp_money($s['amount'], 2); ?></td></tr>
					<?php endforeach; ?>
					</tbody>
				</table>
			</div>
		<?php endforeach; ?>
		<?php if (empty($accruals)): ?><p class="text-muted">No accrual schemes yet.</p><?php endif; ?>
	</div></div>
<?php endif; ?>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	var csrf = <?php echo json_encode($csrfLocal); ?>;
	function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 900); }
	function bind(id, action){ var f=document.getElementById(id); if(f) f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }
	bind('epc_fin_periods', 'fin_periods_generate');
	bind('epc_fin_fx', 'fin_fx_revalue');
	bind('epc_fin_alloc', 'fin_alloc_save');
	bind('epc_fin_accrual', 'fin_accrual_save');
	document.querySelectorAll('.epc-fin-pstat').forEach(function(b){ b.addEventListener('click', function(){ var tr=b.closest('tr'); var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('fy',tr.getAttribute('data-fy')); fd.append('period_no',tr.getAttribute('data-pno')); fd.append('status',b.getAttribute('data-status')); post('fin_period_status', fd).then(msg); }); });
	document.querySelectorAll('.epc-alloc-run').forEach(function(b){ b.addEventListener('click', function(){ var amt=b.closest('td').querySelector('.epc-alloc-amt').value; var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('rule_id',b.getAttribute('data-id')); fd.append('amount',amt); post('fin_alloc_run', fd).then(msg); }); });
})();
</script>
