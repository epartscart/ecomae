<?php
defined('_ASTEXE_') or die('No access');
/**
 * Order Planning — replenishment recommendations grid + item inventory
 * analytics worksheet (inspired by Syncron Parts Planning).
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_order_planning.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_opl_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$oplWh     = (int) ($_GET['opl_wh'] ?? 0);
$oplItem   = (int) ($_GET['opl_item'] ?? 0);
$oplStatus = (string) ($_GET['opl_status'] ?? '');
$oplDue    = isset($_GET['opl_due']) ? (int) $_GET['opl_due'] : 1;
$oplSearch = trim((string) ($_GET['opl_search'] ?? ''));
$warehouses = epc_erp_inventory_list_warehouses($db_link);

erp_page_header(
	'<i class="fa fa-cubes"></i> Order planning',
	'Demand-driven replenishment recommendations and item inventory analytics — forecast, safety stock, reorder point, recommended order qty and days of cover.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Order planning'),
	)
);

$tabBase = epc_erp_tab_url($erpUrl, 'order_planning', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';

if ($oplItem > 0) {
	/* ---------- Item worksheet / inventory analytics ---------- */
	$rows = epc_erp_inventory_stock_report($db_link, $oplWh > 0 ? $oplWh : 0);
	$stockRow = null;
	foreach ($rows as $r) {
		if ((int) $r['item_id'] === $oplItem && ($oplWh === 0 || (int) $r['warehouse_id'] === $oplWh)) {
			if ($stockRow === null) {
				$stockRow = $r;
				$stockRow['qty_on_hand'] = 0.0;
			}
			$stockRow['qty_on_hand'] += (float) $r['qty_on_hand'];
		}
	}
	if ($stockRow === null) {
		erp_empty_state('Item not found in stock for this warehouse.');
		return;
	}
	$wid = (int) $stockRow['warehouse_id'];
	$m = epc_opl_compute($db_link, $stockRow);
	$params = epc_opl_params_get($db_link, $oplItem, $wid);
	$max = max(1, max($m['series']));
	?>
	<p><a href="<?php echo epc_erp_h($tabBase); ?>">&laquo; Back to recommendations</a></p>
	<div id="epc_erp_msg" class="alert" style="display:none;"></div>
	<h4><i class="fa fa-cube"></i> <?php echo epc_erp_h($m['sku'] . ' · ' . $m['name']); ?>
		<small class="text-muted"><?php echo epc_erp_h($m['warehouse_name']); ?> · demand: <span class="label label-default"><?php echo epc_erp_h($m['demand_class']); ?></span></small>
	</h4>

	<div class="epc-erp-kpi" style="margin:10px 0 16px;">
		<div class="kpi"><div class="lbl">On hand</div><div class="val"><?php echo epc_erp_money($m['on_hand']); ?></div></div>
		<div class="kpi"><div class="lbl">Avg daily demand</div><div class="val"><?php echo epc_erp_money($m['avg_daily_demand']); ?></div></div>
		<div class="kpi"><div class="lbl">Safety stock</div><div class="val"><?php echo epc_erp_money($m['safety_stock']); ?></div></div>
		<div class="kpi"><div class="lbl">Order level (ROP)</div><div class="val"><?php echo epc_erp_money($m['order_level']); ?></div></div>
		<div class="kpi"><div class="lbl">Target stock</div><div class="val"><?php echo epc_erp_money($m['target_stock']); ?></div></div>
		<div class="kpi"><div class="lbl">Recommended OQ</div><div class="val" style="color:<?php echo $m['roq'] > 0 ? '#0a7d33' : 'inherit'; ?>;"><?php echo epc_erp_money($m['roq']); ?></div></div>
		<div class="kpi"><div class="lbl">Days of cover</div><div class="val"><?php echo $m['coverage_days'] === null ? '∞' : epc_erp_money($m['coverage_days']); ?></div></div>
	</div>

	<div class="row">
		<div class="col-md-7">
			<div class="well well-sm">
				<h5><i class="fa fa-bar-chart"></i> 12-month demand (sale-out)</h5>
				<div style="display:flex;align-items:flex-end;gap:4px;height:120px;border-bottom:1px solid #ccc;padding-bottom:2px;">
					<?php foreach ($m['series'] as $i => $v): $h = (int) round(($v / $max) * 110); ?>
						<div style="flex:1;text-align:center;" title="<?php echo epc_erp_h(date('M Y', strtotime('-' . (count($m['series']) - 1 - $i) . ' months'))) . ': ' . epc_erp_money($v); ?>">
							<div style="background:#2bb3c0;height:<?php echo max(1, $h); ?>px;border-radius:2px 2px 0 0;"></div>
						</div>
					<?php endforeach; ?>
				</div>
				<small class="text-muted">Monthly demand. Mean <?php echo epc_erp_money($m['monthly_demand']); ?>/mo · σ <?php echo epc_erp_money($m['sigma_monthly']); ?> · ADI <?php echo epc_erp_money($m['adi']); ?> · CV² <?php echo epc_erp_money($m['cv2']); ?></small>
			</div>

			<table class="table table-condensed table-bordered">
				<tbody>
					<tr><th style="width:50%;">Stock balance / on hand</th><td class="text-right"><?php echo epc_erp_money($m['on_hand']); ?></td></tr>
					<tr><th>Effective stock</th><td class="text-right"><?php echo epc_erp_money($m['effective_stock']); ?></td></tr>
					<tr><th>Shortfall</th><td class="text-right"><?php echo epc_erp_money($m['shortfall']); ?></td></tr>
					<tr><th>Excess stock</th><td class="text-right"><?php echo epc_erp_money($m['excess']); ?></td></tr>
					<tr><th>Lead-time demand (<?php echo (int) $m['lead_time_days']; ?>d)</th><td class="text-right"><?php echo epc_erp_money($m['lead_time_demand']); ?></td></tr>
					<tr><th>Forecast (next month)</th><td class="text-right"><?php echo epc_erp_money($m['forecast_next']); ?></td></tr>
					<tr><th>Annual demand value</th><td class="text-right"><?php echo epc_erp_money($m['annual_value']); ?> AED</td></tr>
					<tr><th>Unit cost</th><td class="text-right"><?php echo epc_erp_money($m['unit_cost']); ?> AED</td></tr>
				</tbody>
			</table>
		</div>

		<div class="col-md-5">
			<div class="well well-sm">
				<h5><i class="fa fa-sliders"></i> Planning parameters</h5>
				<form id="epc_opl_params" class="form">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
					<input type="hidden" name="item_id" value="<?php echo (int) $oplItem; ?>">
					<input type="hidden" name="warehouse_id" value="<?php echo (int) $wid; ?>">
					<div class="row">
						<div class="col-xs-6 form-group"><label>Lead time (days)</label><input type="number" name="lead_time_days" class="form-control input-sm" value="<?php echo (int) $params['lead_time_days']; ?>"></div>
						<div class="col-xs-6 form-group"><label>Target service level %</label><input type="number" step="0.1" name="target_service_level" class="form-control input-sm" value="<?php echo epc_erp_h($params['target_service_level']); ?>"></div>
					</div>
					<div class="row">
						<div class="col-xs-6 form-group"><label>Review period (days)</label><input type="number" name="review_period_days" class="form-control input-sm" value="<?php echo (int) $params['review_period_days']; ?>"></div>
						<div class="col-xs-6 form-group"><label>Manual buffer</label><input type="number" step="0.001" name="manual_buffer" class="form-control input-sm" value="<?php echo epc_erp_h($params['manual_buffer']); ?>"></div>
					</div>
					<div class="row">
						<div class="col-xs-6 form-group"><label>Min order qty</label><input type="number" step="0.001" name="min_order_qty" class="form-control input-sm" value="<?php echo epc_erp_h($params['min_order_qty']); ?>"></div>
						<div class="col-xs-6 form-group"><label>Order multiple</label><input type="number" step="0.001" name="order_multiple" class="form-control input-sm" value="<?php echo epc_erp_h($params['order_multiple']); ?>"></div>
					</div>
					<div class="form-group"><label>Default supplier</label><input type="text" name="supplier" class="form-control input-sm" value="<?php echo epc_erp_h($params['supplier']); ?>"></div>
					<button type="submit" class="btn btn-primary btn-sm">Save &amp; recalculate</button>
				</form>
			</div>
			<div class="well well-sm">
				<h5><i class="fa fa-magic"></i> Recommendation</h5>
				<p>Service-level Z-factor <strong><?php echo epc_erp_money($m['z']); ?></strong> @ <?php echo epc_erp_money($m['service_level']); ?>% target.</p>
				<?php if ($m['roq'] > 0): ?>
					<p class="text-success"><strong>Order <?php echo epc_erp_money($m['roq']); ?> <?php echo epc_erp_h($m['unit']); ?></strong> (<?php echo epc_erp_money($m['value']); ?> AED) — stock at/below reorder point.</p>
					<button class="btn btn-success btn-sm epc-opl-act" data-item="<?php echo (int) $oplItem; ?>" data-wh="<?php echo (int) $wid; ?>" data-roq="<?php echo epc_erp_h($m['roq']); ?>" data-value="<?php echo epc_erp_h($m['value']); ?>" data-supplier="<?php echo epc_erp_h($m['supplier']); ?>" data-status="confirmed">Confirm order</button>
				<?php else: ?>
					<p class="text-muted">No order needed — effective stock is above the reorder point.</p>
				<?php endif; ?>
			</div>
		</div>
	</div>
	<?php
	echo epc_opl_worksheet_script($csrfLocal);
	return;
}

/* ---------- Recommendations grid ---------- */
$summary = epc_opl_summary($db_link, $oplWh);
$recs = epc_opl_recommendations($db_link, array(
	'warehouse_id' => $oplWh,
	'only_due' => $oplDue === 1,
	'status' => $oplStatus,
	'search' => $oplSearch,
));

erp_stat_cards(array(
	array('label' => 'Planning lines', 'value' => (string) $summary['lines']),
	array('label' => 'Due to order', 'value' => (string) $summary['due']),
	array('label' => 'Suggested order value', 'value' => epc_erp_money($summary['order_value']) . ' AED'),
	array('label' => 'Confirmed value', 'value' => epc_erp_money($summary['confirmed_value']) . ' AED'),
	array('label' => 'Stock-out risk', 'value' => (string) $summary['stockout_risk']),
));
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<form method="get" class="form-inline" style="margin-bottom:12px;">
	<?php foreach ($_GET as $k => $vv) { if (in_array($k, array('opl_wh','opl_status','opl_due','opl_search'), true)) { continue; } echo '<input type="hidden" name="' . epc_erp_h($k) . '" value="' . epc_erp_h(is_array($vv) ? '' : (string) $vv) . '">'; } ?>
	<label>Warehouse</label>
	<select name="opl_wh" class="form-control input-sm">
		<option value="0">All</option>
		<?php foreach ($warehouses as $w): ?>
			<option value="<?php echo (int) $w['id']; ?>"<?php echo $oplWh === (int) $w['id'] ? ' selected' : ''; ?>><?php echo epc_erp_h($w['name']); ?></option>
		<?php endforeach; ?>
	</select>
	<label>Status</label>
	<select name="opl_status" class="form-control input-sm">
		<option value="">Any</option>
		<?php foreach (array('pending','confirmed','rejected') as $s): ?>
			<option value="<?php echo $s; ?>"<?php echo $oplStatus === $s ? ' selected' : ''; ?>><?php echo ucfirst($s); ?></option>
		<?php endforeach; ?>
	</select>
	<label><input type="checkbox" name="opl_due" value="1"<?php echo $oplDue === 1 ? ' checked' : ''; ?>> Due only</label>
	<input type="text" name="opl_search" class="form-control input-sm" placeholder="SKU / name" value="<?php echo epc_erp_h($oplSearch); ?>">
	<button type="submit" class="btn btn-default btn-sm">Filter</button>
	<button type="button" class="btn btn-success btn-sm" id="epc_opl_confirm_all"><i class="fa fa-check"></i> Confirm all due</button>
	<span class="pull-right">
		<button type="button" class="btn btn-info btn-sm" id="epc_opl_seed"><i class="fa fa-flask"></i> Generate sample demand</button>
		<button type="button" class="btn btn-link btn-sm text-muted" id="epc_opl_clear">Clear sample</button>
	</span>
</form>

<div class="table-responsive">
<table class="table table-bordered table-condensed table-hover">
	<thead><tr>
		<th>Item</th><th>Warehouse</th><th>Demand</th><th class="text-right">On hand</th><th class="text-right">Fcst/mo</th>
		<th class="text-right">Lead dmd</th><th class="text-right">Safety</th><th class="text-right">Order lvl</th>
		<th class="text-right">ROQ</th><th class="text-right">Cover (d)</th><th class="text-right">Value</th><th>Supplier</th><th>Status</th><th></th>
	</tr></thead>
	<tbody>
	<?php if (empty($recs)): ?>
		<tr><td colspan="14" class="text-muted">No recommendations match your filters. (Demand is derived from sale-out movements — sell some stock on the demo to generate suggestions.)</td></tr>
	<?php else: foreach ($recs as $r):
		$lbl = $r['status'] === 'confirmed' ? 'success' : ($r['status'] === 'rejected' ? 'default' : 'warning');
		$wsUrl = $tabBase . $sep . 'opl_item=' . (int) $r['item_id'] . '&opl_wh=' . (int) $r['warehouse_id'];
		$lowCover = ($r['coverage_days'] !== null && $r['coverage_days'] < $r['lead_time_days']); ?>
		<tr<?php echo $r['roq'] > 0 ? ' class="warning"' : ''; ?>>
			<td><a href="<?php echo epc_erp_h($wsUrl); ?>"><strong><?php echo epc_erp_h($r['sku']); ?></strong></a><br><small class="text-muted"><?php echo epc_erp_h($r['name']); ?></small></td>
			<td><small><?php echo epc_erp_h($r['warehouse_name']); ?></small></td>
			<td><span class="label label-default"><?php echo epc_erp_h($r['demand_class']); ?></span></td>
			<td class="text-right"><?php echo epc_erp_money($r['on_hand']); ?></td>
			<td class="text-right"><?php echo epc_erp_money($r['forecast_next']); ?></td>
			<td class="text-right"><?php echo epc_erp_money($r['lead_time_demand']); ?></td>
			<td class="text-right"><?php echo epc_erp_money($r['safety_stock']); ?></td>
			<td class="text-right"><?php echo epc_erp_money($r['order_level']); ?></td>
			<td class="text-right"><strong><?php echo epc_erp_money($r['roq']); ?></strong></td>
			<td class="text-right"<?php echo $lowCover ? ' style="color:#c0392b;font-weight:bold;"' : ''; ?>><?php echo $r['coverage_days'] === null ? '∞' : epc_erp_money($r['coverage_days']); ?></td>
			<td class="text-right"><?php echo epc_erp_money($r['value']); ?></td>
			<td><small><?php echo epc_erp_h($r['supplier'] ?: '—'); ?></small></td>
			<td><span class="label label-<?php echo $lbl; ?>"><?php echo epc_erp_h($r['status']); ?></span></td>
			<td style="white-space:nowrap;">
				<?php if ($r['roq'] > 0 && $r['status'] !== 'confirmed'): ?><button class="btn btn-xs btn-success epc-opl-act" data-item="<?php echo (int) $r['item_id']; ?>" data-wh="<?php echo (int) $r['warehouse_id']; ?>" data-roq="<?php echo epc_erp_h($r['roq']); ?>" data-value="<?php echo epc_erp_h($r['value']); ?>" data-supplier="<?php echo epc_erp_h($r['supplier']); ?>" data-status="confirmed">Confirm</button><?php endif; ?>
				<?php if ($r['status'] !== 'rejected'): ?><button class="btn btn-xs btn-default epc-opl-act" data-item="<?php echo (int) $r['item_id']; ?>" data-wh="<?php echo (int) $r['warehouse_id']; ?>" data-roq="<?php echo epc_erp_h($r['roq']); ?>" data-value="<?php echo epc_erp_h($r['value']); ?>" data-supplier="<?php echo epc_erp_h($r['supplier']); ?>" data-status="rejected">Reject</button><?php endif; ?>
			</td>
		</tr>
	<?php endforeach; endif; ?>
	</tbody>
</table>
</div>
<?php echo epc_opl_worksheet_script($csrfLocal);

function epc_opl_worksheet_script(string $csrfLocal): string
{
	$url = isset($GLOBALS['erpAjaxEndpoint']) ? $GLOBALS['erpAjaxEndpoint'] : ('/' . (isset($GLOBALS['DP_Config']->backend_dir) ? $GLOBALS['DP_Config']->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php');
	$urlJson = json_encode($url);
	$csrfJson = json_encode($csrfLocal);
	return <<<HTML
<script>
(function(){
	var url = {$urlJson};
	var csrf = {$csrfJson};
	function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 700); }
	var pf=document.getElementById('epc_opl_params'); if(pf) pf.addEventListener('submit', function(e){ e.preventDefault(); post('opl_params_save', new FormData(pf)).then(msg); });
	document.querySelectorAll('.epc-opl-act').forEach(function(b){ b.addEventListener('click', function(){ var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('item_id',b.getAttribute('data-item')); fd.append('warehouse_id',b.getAttribute('data-wh')); fd.append('roq',b.getAttribute('data-roq')); fd.append('value',b.getAttribute('data-value')); fd.append('supplier',b.getAttribute('data-supplier')||''); fd.append('status',b.getAttribute('data-status')); post('opl_set_status', fd).then(msg); }); });
	function whVal(){ var w=document.querySelector('[name=opl_wh]'); return w?w.value:'0'; }
	var ca=document.getElementById('epc_opl_confirm_all'); if(ca) ca.addEventListener('click', function(){ var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('warehouse_id', whVal()); post('opl_confirm_all', fd).then(msg); });
	var sd=document.getElementById('epc_opl_seed'); if(sd) sd.addEventListener('click', function(){ if(!confirm('Generate 12 months of sample sale-out demand across stocked items? (re-runnable; tagged DEMO-DEMAND)')) return; sd.disabled=true; sd.textContent='Generating…'; var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('warehouse_id', whVal()); post('opl_seed_demo', fd).then(msg); });
	var cl=document.getElementById('epc_opl_clear'); if(cl) cl.addEventListener('click', function(){ if(!confirm('Clear seeded sample demand and recommendation statuses?')) return; var fd=new FormData(); fd.append('csrf_guard_key',csrf); post('opl_clear_demo', fd).then(msg); });
})();
</script>
HTML;
}
