<?php
defined('_ASTEXE_') or die('No access');
/**
 * Subscription billing & revenue recognition — recurring plans, cycle invoice
 * generation, MRR/ARR, and IFRS 15 / ASC 606 straight-line revenue recognition
 * with deferred-revenue reporting.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_subscriptions.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_sub_ensure_schema($db_link);
$csrfLocal  = isset($csrf) ? $csrf : '';
$subId      = isset($_GET['sub']) ? (int) $_GET['sub'] : 0;
$subSummary = epc_sub_summary($db_link);
$subs       = epc_sub_list($db_link, 200);

erp_page_header(
	'<i class="fa fa-refresh"></i> Subscription billing &amp; revenue recognition',
	'Recurring plans, cycle invoicing, MRR / ARR and IFRS 15 / ASC 606 straight-line revenue recognition with deferred revenue.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Subscriptions'),
	)
);

erp_stat_cards(array(
	array('label' => 'Active subscriptions', 'value' => (string) $subSummary['active']),
	array('label' => 'MRR', 'value' => epc_erp_money($subSummary['mrr']) . ' AED'),
	array('label' => 'ARR', 'value' => epc_erp_money($subSummary['arr']) . ' AED'),
	array('label' => 'Recognised revenue', 'value' => epc_erp_money($subSummary['recognized']) . ' AED'),
	array('label' => 'Deferred revenue', 'value' => epc_erp_money($subSummary['deferred']) . ' AED'),
));

$tabBase = epc_erp_tab_url($erpUrl, 'subscriptions', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<div class="row">
	<div class="col-md-4">
		<div class="well well-sm">
			<h5><i class="fa fa-plus-circle"></i> New subscription</h5>
			<form id="epc_sub_new" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<div class="row">
					<div class="col-xs-5 form-group"><label>Code</label><input type="text" name="code" class="form-control input-sm" placeholder="SUB-001" required></div>
					<div class="col-xs-7 form-group"><label>Customer</label><input type="text" name="customer" class="form-control input-sm" required></div>
				</div>
				<div class="form-group"><label>Plan</label><input type="text" name="plan_name" class="form-control input-sm" placeholder="e.g. Enterprise"></div>
				<div class="row">
					<div class="col-xs-6 form-group"><label>Amount / cycle</label><input type="number" step="0.01" name="amount" class="form-control input-sm" value="0" required></div>
					<div class="col-xs-6 form-group">
						<label>Cycle</label>
						<select name="cycle" class="form-control input-sm">
							<option value="monthly">Monthly</option>
							<option value="quarterly">Quarterly</option>
							<option value="annual">Annual</option>
						</select>
					</div>
				</div>
				<div class="row">
					<div class="col-xs-6 form-group"><label>Term (months)</label><input type="number" step="1" name="term_months" class="form-control input-sm" value="12"></div>
					<div class="col-xs-6 form-group"><label>Start</label><input type="date" name="start_date_str" class="form-control input-sm" value="<?php echo date('Y-m-d'); ?>"></div>
				</div>
				<button type="submit" class="btn btn-primary btn-sm">Create subscription</button>
			</form>
		</div>
	</div>

	<div class="col-md-8">
		<h5>Subscriptions</h5>
		<div class="table-responsive">
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Code</th><th>Customer</th><th>Plan</th><th>Cycle</th><th class="text-right">MRR</th><th class="text-right">Recognised</th><th class="text-right">Deferred</th><th>Status</th><th></th></tr></thead>
			<tbody>
			<?php if (empty($subs)): ?>
				<tr><td colspan="9" class="text-muted">No subscriptions yet. Create one on the left.</td></tr>
			<?php else: foreach ($subs as $s):
				$lbl = $s['status'] === 'active' ? 'success' : ($s['status'] === 'paused' ? 'warning' : 'default'); ?>
				<tr<?php echo $subId === (int) $s['id'] ? ' class="info"' : ''; ?>>
					<td><strong><?php echo epc_erp_h($s['code']); ?></strong></td>
					<td><?php echo epc_erp_h($s['customer']); ?></td>
					<td><small><?php echo epc_erp_h($s['plan_name']); ?></small></td>
					<td><small><?php echo epc_erp_h($s['cycle']); ?></small></td>
					<td class="text-right"><?php echo epc_erp_money($s['mrr']); ?></td>
					<td class="text-right"><?php echo epc_erp_money($s['rev']['recognized']); ?></td>
					<td class="text-right"><?php echo epc_erp_money($s['rev']['deferred']); ?></td>
					<td><span class="label label-<?php echo $lbl; ?>"><?php echo epc_erp_h($s['status']); ?></span></td>
					<td><a class="btn btn-link btn-xs" href="<?php echo epc_erp_h($tabBase . $sep . 'sub=' . (int) $s['id']); ?>">Open</a></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
		</div>
	</div>
</div>

<?php if ($subId > 0):
	$sub = epc_sub_get($db_link, $subId);
	if ($sub):
		$rev = epc_sub_revenue_recognized($db_link, $sub);
		$invs = epc_sub_invoices_list($db_link, $subId);
?>
<hr>
<div class="epc-erp-section">
	<h4><i class="fa fa-refresh"></i> <?php echo epc_erp_h($sub['code'] . ' · ' . $sub['customer']); ?>
		<small class="text-muted"><?php echo epc_erp_h($sub['plan_name']); ?> · <?php echo epc_erp_h($sub['cycle']); ?> · next bill <?php echo epc_erp_h(date('Y-m-d', (int) $sub['next_bill_date'])); ?></small>
	</h4>
	<div class="epc-erp-kpi" style="margin-bottom:12px;">
		<div class="kpi"><div class="lbl">MRR</div><div class="val"><?php echo epc_erp_money($rev['mrr']); ?></div></div>
		<div class="kpi"><div class="lbl">Contract value</div><div class="val"><?php echo epc_erp_money($rev['contract_value']); ?></div></div>
		<div class="kpi"><div class="lbl">Months elapsed</div><div class="val"><?php echo (int) $rev['elapsed']; ?> / <?php echo (int) $rev['term_months']; ?></div></div>
		<div class="kpi"><div class="lbl">Recognised</div><div class="val"><?php echo epc_erp_money($rev['recognized']); ?></div></div>
		<div class="kpi"><div class="lbl">Deferred</div><div class="val"><?php echo epc_erp_money($rev['deferred']); ?></div></div>
	</div>
	<div style="margin-bottom:12px;">
		<button class="btn btn-primary btn-sm" id="epc_sub_gen" data-id="<?php echo (int) $subId; ?>"><i class="fa fa-file-text"></i> Generate cycle invoice</button>
		<?php if ($sub['status'] === 'active'): ?><button class="btn btn-default btn-sm epc-sub-status" data-id="<?php echo (int) $subId; ?>" data-status="paused">Pause</button><?php endif; ?>
		<?php if ($sub['status'] === 'paused'): ?><button class="btn btn-success btn-sm epc-sub-status" data-id="<?php echo (int) $subId; ?>" data-status="active">Resume</button><?php endif; ?>
		<?php if ($sub['status'] !== 'cancelled'): ?><button class="btn btn-default btn-sm epc-sub-status" data-id="<?php echo (int) $subId; ?>" data-status="cancelled">Cancel</button><?php endif; ?>
	</div>
	<table class="table table-condensed table-bordered">
		<thead><tr><th>Invoice #</th><th>Period</th><th class="text-right">Amount</th><th>Status</th><th></th></tr></thead>
		<tbody>
		<?php if (empty($invs)): ?>
			<tr><td colspan="5" class="text-muted">No invoices generated yet.</td></tr>
		<?php else: foreach ($invs as $iv): ?>
			<tr>
				<td>#<?php echo (int) $iv['id']; ?></td>
				<td><small><?php echo epc_erp_h(date('Y-m-d', (int) $iv['period_start']) . ' → ' . date('Y-m-d', (int) $iv['period_end'])); ?></small></td>
				<td class="text-right"><?php echo epc_erp_money($iv['amount']); ?></td>
				<td><span class="label label-<?php echo $iv['status'] === 'paid' ? 'success' : 'warning'; ?>"><?php echo epc_erp_h($iv['status']); ?></span></td>
				<td><?php if ($iv['status'] !== 'paid'): ?><button class="btn btn-xs btn-success epc-sub-paid" data-id="<?php echo (int) $iv['id']; ?>">Mark paid</button><?php endif; ?></td>
			</tr>
		<?php endforeach; endif; ?>
		</tbody>
	</table>
</div>
<?php endif; endif; ?>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	var csrf = <?php echo json_encode($csrfLocal); ?>;
	function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 800); }
	function bind(id, action){ var f=document.getElementById(id); if(f) f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }
	bind('epc_sub_new', 'sub_save');
	function btn(sel, action, key){ document.querySelectorAll(sel).forEach(function(b){ b.addEventListener('click', function(){ var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append(key, b.getAttribute('data-id')); if(b.getAttribute('data-status')) fd.append('status', b.getAttribute('data-status')); post(action, fd).then(msg); }); }); }
	var gen=document.getElementById('epc_sub_gen'); if(gen){ gen.addEventListener('click', function(){ var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('id', gen.getAttribute('data-id')); post('sub_generate', fd).then(msg); }); }
	btn('.epc-sub-status', 'sub_status', 'id');
	btn('.epc-sub-paid', 'sub_invoice_paid', 'id');
})();
</script>
