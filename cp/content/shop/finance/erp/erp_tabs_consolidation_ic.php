<?php
defined('_ASTEXE_') or die('No access');
/**
 * Intercompany sub-view (rendered inside erp_tabs_consolidation_bu.php).
 * Scope vars: $db_link, $erpUrl, $csrfLocal.
 */

$entities = epc_cons_entities_list($db_link);
$ics      = epc_cons_ic_list($db_link);

$icTotal = 0.0;
foreach ($ics as $ic) { $icTotal += (float) $ic['amount']; }
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-exchange"></i> Intercompany transactions</h4>
	<p class="text-muted">
		Record trade and funding flows between group members. Trading flows (sale / purchase / service) eliminate matched
		revenue &amp; expense in the group P&amp;L; funding flows (loan) eliminate the matched intercompany receivable &amp; payable
		in the group balance sheet. These feed the consolidation worksheet automatically.
	</p>
	<div class="epc-erp-kpi" style="margin-bottom:14px;">
		<div class="kpi"><div class="lbl">IC transactions</div><div class="val"><?php echo count($ics); ?></div></div>
		<div class="kpi"><div class="lbl">Total IC value</div><div class="val"><?php echo epc_erp_money($icTotal); ?></div></div>
		<div class="kpi"><div class="lbl">Group entities</div><div class="val"><?php echo count($entities); ?></div></div>
	</div>
</div>

<?php if (count($entities) < 2): ?>
	<div class="alert alert-info">Add at least two group entities (under <strong>Group consolidation</strong>) before recording intercompany transactions.</div>
<?php else: ?>
<div class="well well-sm">
	<form id="epc_cons_ic" class="form-inline">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<select name="from_entity" class="form-control input-sm" required>
			<option value="">From…</option>
			<?php foreach ($entities as $e): ?><option value="<?php echo epc_erp_h($e['code']); ?>"><?php echo epc_erp_h($e['code']); ?></option><?php endforeach; ?>
		</select>
		<select name="to_entity" class="form-control input-sm" required>
			<option value="">To…</option>
			<?php foreach ($entities as $e): ?><option value="<?php echo epc_erp_h($e['code']); ?>"><?php echo epc_erp_h($e['code']); ?></option><?php endforeach; ?>
		</select>
		<select name="txn_type" class="form-control input-sm">
			<option value="sale">Sale</option>
			<option value="purchase">Purchase</option>
			<option value="service">Service</option>
			<option value="loan">Loan / funding</option>
		</select>
		<input type="number" step="0.01" name="amount" class="form-control input-sm" placeholder="Amount" style="width:120px;" required>
		<input type="date" name="txn_date" class="form-control input-sm" value="<?php echo date('Y-m-d'); ?>">
		<input type="text" name="memo" class="form-control input-sm" placeholder="Memo" style="width:160px;">
		<button type="submit" class="btn btn-primary btn-sm">Record</button>
	</form>
</div>

<table class="table table-bordered table-condensed">
	<thead><tr><th>Date</th><th>Ref</th><th>From</th><th>To</th><th>Type</th><th class="text-right">Amount</th><th>Memo</th><th></th></tr></thead>
	<tbody>
	<?php if (empty($ics)): ?>
		<tr><td colspan="8" class="text-muted">No intercompany transactions yet.</td></tr>
	<?php else: foreach ($ics as $ic):
		$elimType = in_array($ic['txn_type'], array('sale','purchase','service'), true) ? 'P&L' : 'Balance sheet'; ?>
		<tr>
			<td><?php echo epc_erp_h($ic['txn_date']); ?></td>
			<td><small><?php echo epc_erp_h($ic['ref']); ?></small></td>
			<td><strong><?php echo epc_erp_h($ic['from_entity']); ?></strong></td>
			<td><strong><?php echo epc_erp_h($ic['to_entity']); ?></strong></td>
			<td><span class="label label-default"><?php echo epc_erp_h($ic['txn_type']); ?></span> <small class="text-muted">elim: <?php echo $elimType; ?></small></td>
			<td class="text-right"><?php echo epc_erp_money($ic['amount']); ?></td>
			<td><small><?php echo epc_erp_h($ic['memo']); ?></small></td>
			<td><button class="btn btn-link btn-xs epc-ic-del" data-id="<?php echo (int)$ic['id']; ?>" style="color:#c00;">Delete</button></td>
		</tr>
	<?php endforeach; endif; ?>
	</tbody>
</table>
<?php endif; ?>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function flash(j){ alert(j.message || (j.status?'Saved':'Error')); if(j.status) location.reload(); }
	var f = document.getElementById('epc_cons_ic');
	if (f) f.addEventListener('submit', function(e){ e.preventDefault(); post('cons_ic_save', new FormData(f)).then(flash); });
	document.querySelectorAll('.epc-ic-del').forEach(function(b){
		b.addEventListener('click', function(){
			if(!confirm('Delete this intercompany transaction?')) return;
			var fd = new FormData(); fd.append('csrf_guard_key', <?php echo json_encode($csrfLocal); ?>); fd.append('id', b.getAttribute('data-id'));
			post('cons_ic_delete', fd).then(flash);
		});
	});
})();
</script>
