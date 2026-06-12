<?php
defined('_ASTEXE_') or die('No access');
/**
 * Group consolidation sub-view (rendered inside erp_tabs_consolidation_bu.php).
 * Scope vars: $db_link, $erpUrl, $date_from, $date_to, $date_from_str, $date_to_str, $csrfLocal.
 */

$dFrom = isset($date_from) ? (int) $date_from : strtotime(date('Y-m-01'));
$dTo   = isset($date_to) ? (int) $date_to : time();

$entities = epc_cons_entities_list($db_link);
$figures  = epc_cons_figures_map($db_link);
$cons     = epc_cons_consolidate($db_link, $dFrom, $dTo);
$C        = $cons['consolidated'];
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-building"></i> Group consolidation</h4>
	<p class="text-muted">
		Combine every group member into one set of statements. The <strong>home</strong> entity is pulled live from this tenant's GL;
		subsidiaries use the figures you capture below. Intercompany revenue/expense and balances are eliminated automatically,
		and minority interest is split out for partly-owned subsidiaries.
	</p>

	<div class="epc-erp-kpi" style="margin-bottom:14px;">
		<div class="kpi"><div class="lbl">Group revenue</div><div class="val"><?php echo epc_erp_money($C['revenue']); ?></div></div>
		<div class="kpi"><div class="lbl">Group net profit</div><div class="val"><?php echo epc_erp_money($C['net_profit']); ?></div></div>
		<div class="kpi"><div class="lbl">Group assets</div><div class="val"><?php echo epc_erp_money($C['assets']); ?></div></div>
		<div class="kpi"><div class="lbl">Eliminations</div><div class="val"><?php echo epc_erp_money($cons['elimination_pl'] + $cons['elimination_bs']); ?></div></div>
	</div>
</div>

<div class="row">
	<div class="col-md-4">
		<div class="well well-sm">
			<h5><i class="fa fa-plus-circle"></i> Add / update a group entity</h5>
			<form id="epc_cons_entity" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<div class="row">
					<div class="col-xs-5 form-group"><label>Code</label><input type="text" name="code" class="form-control input-sm" placeholder="HOLD / SUB1" required></div>
					<div class="col-xs-7 form-group"><label>Name</label><input type="text" name="name" class="form-control input-sm" required></div>
				</div>
				<div class="row">
					<div class="col-xs-5 form-group"><label>Currency</label><input type="text" name="currency_code" class="form-control input-sm" value="AED"></div>
					<div class="col-xs-7 form-group"><label>Ownership %</label><input type="number" step="0.001" name="ownership_pct" class="form-control input-sm" value="100"></div>
				</div>
				<div class="form-group">
					<label><input type="checkbox" name="is_home" value="1"> This is the home entity (live from GL)</label>
				</div>
				<button type="submit" class="btn btn-primary btn-sm">Save entity</button>
			</form>
		</div>
	</div>
	<div class="col-md-8">
		<h5>Group members</h5>
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Code</th><th>Name</th><th>Own %</th><th>Source</th><th></th></tr></thead>
			<tbody>
			<?php if (empty($entities)): ?>
				<tr><td colspan="5" class="text-muted">No entities yet. Add the holding company and its subsidiaries.</td></tr>
			<?php else: foreach ($entities as $e): ?>
				<tr>
					<td><strong><?php echo epc_erp_h($e['code']); ?></strong></td>
					<td><?php echo epc_erp_h($e['name']); ?> <small class="text-muted"><?php echo epc_erp_h($e['currency_code']); ?></small></td>
					<td><?php echo epc_erp_h(rtrim(rtrim(number_format((float)$e['ownership_pct'], 3), '0'), '.')); ?>%</td>
					<td><?php echo !empty($e['is_home']) ? '<span class="label label-info">Live GL</span>' : '<span class="label label-default">Manual</span>'; ?></td>
					<td><button class="btn btn-link btn-xs epc-cons-del" data-id="<?php echo (int)$e['id']; ?>" style="color:#c00;">Remove</button></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>

		<?php
		$subEntities = array_filter($entities, function ($e) { return empty($e['is_home']); });
		if (!empty($subEntities)): ?>
		<h5 style="margin-top:16px;">Subsidiary financials (this period)</h5>
		<form id="epc_cons_figures" class="form-inline" style="margin-bottom:10px;">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<select name="entity_code" class="form-control input-sm" required>
				<?php foreach ($subEntities as $e): $fg = $figures[$e['code']] ?? array(); ?>
					<option value="<?php echo epc_erp_h($e['code']); ?>"
						data-revenue="<?php echo (float)($fg['revenue'] ?? 0); ?>"
						data-expenses="<?php echo (float)($fg['expenses'] ?? 0); ?>"
						data-assets="<?php echo (float)($fg['assets'] ?? 0); ?>"
						data-liabilities="<?php echo (float)($fg['liabilities'] ?? 0); ?>"
						data-equity="<?php echo (float)($fg['equity'] ?? 0); ?>"><?php echo epc_erp_h($e['code'] . ' · ' . $e['name']); ?></option>
				<?php endforeach; ?>
			</select>
			<input type="number" step="0.01" name="revenue" class="form-control input-sm" placeholder="Revenue" style="width:110px;">
			<input type="number" step="0.01" name="expenses" class="form-control input-sm" placeholder="Expenses" style="width:110px;">
			<input type="number" step="0.01" name="assets" class="form-control input-sm" placeholder="Assets" style="width:100px;">
			<input type="number" step="0.01" name="liabilities" class="form-control input-sm" placeholder="Liabilities" style="width:100px;">
			<input type="number" step="0.01" name="equity" class="form-control input-sm" placeholder="Equity" style="width:90px;">
			<button type="submit" class="btn btn-default btn-sm">Save figures</button>
		</form>
		<?php endif; ?>
	</div>
</div>

<div class="epc-erp-section" style="margin-top:8px;">
	<h5>Consolidation worksheet</h5>
	<div class="table-responsive">
	<table class="table table-bordered table-condensed">
		<thead><tr><th>Entity</th><th class="text-right">Revenue</th><th class="text-right">Expenses</th><th class="text-right">Profit</th><th class="text-right">Assets</th><th class="text-right">Liabilities</th><th class="text-right">Equity</th></tr></thead>
		<tbody>
		<?php foreach ($cons['entities'] as $r): ?>
			<tr>
				<td><?php echo epc_erp_h($r['code']); ?> <small class="text-muted"><?php echo $r['is_home'] ? '(home)' : ((rtrim(rtrim(number_format($r['ownership_pct'],3),'0'),'.')) . '%'); ?></small></td>
				<td class="text-right"><?php echo epc_erp_money($r['revenue']); ?></td>
				<td class="text-right"><?php echo epc_erp_money($r['expenses']); ?></td>
				<td class="text-right"><?php echo epc_erp_money($r['profit']); ?></td>
				<td class="text-right"><?php echo epc_erp_money($r['assets']); ?></td>
				<td class="text-right"><?php echo epc_erp_money($r['liabilities']); ?></td>
				<td class="text-right"><?php echo epc_erp_money($r['equity']); ?></td>
			</tr>
		<?php endforeach; ?>
		<tr style="background:#f7fafc;">
			<th>Combined</th>
			<th class="text-right"><?php echo epc_erp_money($cons['combined']['revenue']); ?></th>
			<th class="text-right"><?php echo epc_erp_money($cons['combined']['expenses']); ?></th>
			<th class="text-right"><?php echo epc_erp_money($cons['combined']['revenue'] - $cons['combined']['expenses']); ?></th>
			<th class="text-right"><?php echo epc_erp_money($cons['combined']['assets']); ?></th>
			<th class="text-right"><?php echo epc_erp_money($cons['combined']['liabilities']); ?></th>
			<th class="text-right"><?php echo epc_erp_money($cons['combined']['equity']); ?></th>
		</tr>
		<tr style="color:#c0392b;">
			<td>Intercompany eliminations</td>
			<td class="text-right">(<?php echo epc_erp_money($cons['elimination_pl']); ?>)</td>
			<td class="text-right">(<?php echo epc_erp_money($cons['elimination_pl']); ?>)</td>
			<td class="text-right">—</td>
			<td class="text-right">(<?php echo epc_erp_money($cons['elimination_bs']); ?>)</td>
			<td class="text-right">(<?php echo epc_erp_money($cons['elimination_bs']); ?>)</td>
			<td class="text-right">—</td>
		</tr>
		<tr style="background:#eef9f2;font-weight:bold;">
			<th>Consolidated group</th>
			<th class="text-right"><?php echo epc_erp_money($C['revenue']); ?></th>
			<th class="text-right"><?php echo epc_erp_money($C['expenses']); ?></th>
			<th class="text-right"><?php echo epc_erp_money($C['net_profit']); ?></th>
			<th class="text-right"><?php echo epc_erp_money($C['assets']); ?></th>
			<th class="text-right"><?php echo epc_erp_money($C['liabilities']); ?></th>
			<th class="text-right"><?php echo epc_erp_money($C['equity']); ?></th>
		</tr>
		</tbody>
	</table>
	</div>
	<p class="text-muted" style="font-size:12px;">
		Minority (non-controlling) interest: <strong><?php echo epc_erp_money($cons['minority_interest']); ?></strong> ·
		Profit attributable to the group: <strong><?php echo epc_erp_money($C['group_profit']); ?></strong> ·
		Eliminations sourced from <?php echo (int)$cons['ic_count']; ?> intercompany transaction(s).
	</p>
</div>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function flash(j){ alert(j.message || (j.status?'Saved':'Error')); if(j.status) location.reload(); }
	var ef = document.getElementById('epc_cons_entity');
	if (ef) ef.addEventListener('submit', function(e){ e.preventDefault(); post('cons_entity_save', new FormData(ef)).then(flash); });
	var ff = document.getElementById('epc_cons_figures');
	if (ff) {
		var sel = ff.querySelector('select[name=entity_code]');
		function fillFromOpt(){
			var o = sel.options[sel.selectedIndex]; if(!o) return;
			['revenue','expenses','assets','liabilities','equity'].forEach(function(k){
				var inp = ff.querySelector('[name='+k+']'); if(inp) inp.value = o.getAttribute('data-'+k) || '';
			});
		}
		if (sel){ sel.addEventListener('change', fillFromOpt); fillFromOpt(); }
		ff.addEventListener('submit', function(e){ e.preventDefault(); post('cons_figures_save', new FormData(ff)).then(flash); });
	}
	document.querySelectorAll('.epc-cons-del').forEach(function(b){
		b.addEventListener('click', function(){
			if(!confirm('Remove this entity from the group?')) return;
			var fd = new FormData(); fd.append('csrf_guard_key', <?php echo json_encode($csrfLocal); ?>); fd.append('id', b.getAttribute('data-id'));
			post('cons_entity_delete', fd).then(flash);
		});
	});
})();
</script>
