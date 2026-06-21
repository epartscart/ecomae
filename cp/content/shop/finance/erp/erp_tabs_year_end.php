<?php
defined('_ASTEXE_') or die('No access');
/**
 * Year-end closing — fiscal years & periods, close P&L to retained earnings,
 * carry balance-sheet balances forward, reopen. Surfaces the existing
 * epc_erp_closing engine.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_closing.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_fy_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';

$years = $db_link->query("SELECT * FROM `epc_fy_years` ORDER BY start_date DESC")->fetchAll(PDO::FETCH_ASSOC);
$openCount = 0;
$closedCount = 0;
foreach ($years as $y) {
	if ($y['status'] === 'open') { $openCount++; } else { $closedCount++; }
}

erp_page_header(
	'<i class="fa fa-calendar-check-o"></i> Year-end closing',
	'Fiscal years &amp; periods, close P&amp;L to retained earnings, carry opening balances forward (enterprise year-end period close).',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Year-end closing'),
	)
);

erp_stat_cards(array(
	array('label' => 'Fiscal years', 'value' => (string) count($years)),
	array('label' => 'Open years', 'value' => (string) $openCount),
	array('label' => 'Closed years', 'value' => (string) $closedCount),
));

$tabBase = epc_erp_tab_url($erpUrl, 'year_end', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
$selYear = (int) ($_GET['year_id'] ?? 0);
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<div class="row"><div class="col-md-7">
	<div class="well well-sm">
		<h5><i class="fa fa-plus-circle"></i> New fiscal year</h5>
		<form id="epc_fy_create" class="form-inline">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="text" name="label" class="form-control input-sm" placeholder="Label (FY2026)" style="width:120px;" required>
			<input type="date" name="start_date" class="form-control input-sm" required title="Start date">
			<input type="date" name="end_date" class="form-control input-sm" required title="End date">
			<label><input type="checkbox" name="monthly" value="1" checked> monthly periods</label>
			<button class="btn btn-primary btn-sm">Create</button>
		</form>
	</div>
	<table class="table table-bordered table-condensed">
		<thead><tr><th>Year</th><th>Period</th><th>Status</th><th class="text-right">Retained P&amp;L</th><th></th></tr></thead>
		<tbody>
		<?php if (empty($years)): ?><tr><td colspan="5" class="text-muted">No fiscal years defined.</td></tr>
		<?php else: foreach ($years as $y): ?>
			<tr<?php echo (int) $y['id'] === $selYear ? ' class="info"' : ''; ?>>
				<td><strong><?php echo epc_erp_h($y['label']); ?></strong></td>
				<td><small><?php echo epc_erp_h(date('Y-m-d', (int) $y['start_date']) . ' → ' . date('Y-m-d', (int) $y['end_date'])); ?></small></td>
				<td><span class="label label-<?php echo $y['status'] === 'open' ? 'success' : ($y['status'] === 'locked' ? 'default' : 'warning'); ?>"><?php echo epc_erp_h($y['status']); ?></span></td>
				<td class="text-right"><?php echo number_format((float) $y['retained_pl'], 2); ?></td>
				<td>
					<a class="btn btn-default btn-xs" href="<?php echo epc_erp_h($tabBase . $sep . 'year_id=' . (int) $y['id']); ?>">Periods</a>
					<?php if ($y['status'] === 'open'): ?>
						<form class="epc_fy_close form-inline" style="display:inline;" onsubmit="return confirm('Close fiscal year <?php echo epc_erp_h($y['label']); ?>? This posts the P&amp;L closing entry to retained earnings.');">
							<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
							<input type="hidden" name="year_id" value="<?php echo (int) $y['id']; ?>">
							<button class="btn btn-warning btn-xs">Close year</button>
						</form>
					<?php else: ?>
						<form class="epc_fy_reopen form-inline" style="display:inline;">
							<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
							<input type="hidden" name="year_id" value="<?php echo (int) $y['id']; ?>">
							<button class="btn btn-default btn-xs">Reopen</button>
						</form>
					<?php endif; ?>
				</td>
			</tr>
		<?php endforeach; endif; ?>
		</tbody>
	</table>
	<p class="text-muted">Closing computes net P&amp;L for the year (from the GL P&amp;L report when available), posts the closing entry to retained earnings (acct 3200) via P&amp;L clearing (3900), and marks all periods closed. Balance-sheet balances carry forward as the next year's opening; P&amp;L accounts reset to zero.</p>
</div><div class="col-md-5">
	<?php if ($selYear > 0):
		$periods = $db_link->prepare("SELECT * FROM `epc_fy_periods` WHERE year_id=? ORDER BY period_no");
		$periods->execute(array($selYear));
		$periods = $periods->fetchAll(PDO::FETCH_ASSOC); ?>
		<div class="panel panel-default">
			<div class="panel-heading"><strong>Fiscal periods</strong></div>
			<table class="table table-condensed" style="margin-bottom:0;">
				<thead><tr><th>#</th><th>Range</th><th>Status</th><th></th></tr></thead>
				<tbody>
				<?php if (empty($periods)): ?><tr><td colspan="4" class="text-muted">No periods.</td></tr>
				<?php else: foreach ($periods as $p): ?>
					<tr><td><?php echo (int) $p['period_no']; ?></td>
					<td><small><?php echo epc_erp_h(date('M Y', (int) $p['start_date'])); ?></small></td>
					<td><span class="label label-<?php echo $p['status'] === 'open' ? 'success' : ($p['status'] === 'locked' ? 'default' : 'warning'); ?>"><?php echo epc_erp_h($p['status']); ?></span></td>
					<td>
						<form class="epc_fy_period form-inline" style="display:inline;">
							<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
							<input type="hidden" name="year_id" value="<?php echo (int) $selYear; ?>">
							<input type="hidden" name="period_no" value="<?php echo (int) $p['period_no']; ?>">
							<select name="status" class="form-control input-sm" style="width:auto;display:inline-block;" onchange="this.form.requestSubmit ? this.form.requestSubmit() : this.form.submit();">
								<?php foreach (array('open', 'closed', 'locked') as $s): ?>
									<option value="<?php echo $s; ?>" <?php echo $p['status'] === $s ? 'selected' : ''; ?>><?php echo $s; ?></option>
								<?php endforeach; ?>
							</select>
						</form>
					</td></tr>
				<?php endforeach; endif; ?>
				</tbody>
			</table>
		</div>
	<?php else: ?><p class="text-muted">Pick a fiscal year to manage its periods (open / closed / locked).</p><?php endif; ?>
</div></div>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 1000); }
	function bind(id, action){ var f=document.getElementById(id); if(f) f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }
	function bindAll(cls, action){ Array.prototype.forEach.call(document.querySelectorAll('.'+cls), function(f){ f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }); }
	bind('epc_fy_create', 'fy_create');
	bindAll('epc_fy_close', 'fy_close');
	bindAll('epc_fy_reopen', 'fy_reopen');
	bindAll('epc_fy_period', 'fy_period_status');
})();
</script>
