<?php
defined('_ASTEXE_') or die('No access');
/**
 * Manufacturing depth — work centers, routes/operations with a
 * finite-capacity schedule preview, and a regenerative multi-level MRP run.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_mfg_routing.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_mfgr_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$view = isset($_GET['mv']) ? (string) $_GET['mv'] : 'mrp';

$summary = epc_mfgr_summary($db_link, $companyId);
$wcs = epc_mfgr_wc_list($db_link, $companyId);
$wcOpts = '';
foreach ($wcs as $w) {
	$wcOpts .= '<option value="' . (int) $w['id'] . '">' . epc_erp_h($w['code'] . ' — ' . $w['name']) . '</option>';
}

erp_page_header(
	'<i class="fa fa-cogs"></i> Manufacturing planning',
	'Enterprise routes &amp; operations, work-center capacity scheduling, and a regenerative multi-level MRP run with level-by-level netting.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Manufacturing planning'),
	)
);

erp_stat_cards(array(
	array('label' => 'Work centers', 'value' => (string) $summary['work_centers']),
	array('label' => 'Routes', 'value' => (string) $summary['routes']),
	array('label' => 'Planned orders', 'value' => (string) $summary['planned']),
	array('label' => 'Planned production', 'value' => (string) $summary['planned_production']),
	array('label' => 'Planned purchase', 'value' => (string) $summary['planned_purchase']),
	array('label' => 'Daily capacity (min)', 'value' => (string) $summary['capacity_min']),
));

$tabBase = epc_erp_tab_url($erpUrl, 'mfg_planning', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
$views = array('mrp' => 'MRP', 'routes' => 'Routes & ops', 'workcenters' => 'Work centers', 'schedule' => 'Capacity schedule');
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<div class="btn-group btn-group-sm" style="margin-bottom:10px;">
	<?php foreach ($views as $k => $lbl): ?>
		<a class="btn btn-<?php echo $view === $k ? 'primary' : 'default'; ?>" href="<?php echo epc_erp_h($tabBase . $sep . 'mv=' . $k); ?>"><?php echo epc_erp_h($lbl); ?></a>
	<?php endforeach; ?>
</div>

<?php if ($view === 'workcenters'): ?>
	<div class="row"><div class="col-md-4">
		<div class="well well-sm">
			<h5><i class="fa fa-plus-circle"></i> New work center</h5>
			<form id="epc_mfgr_wc" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<div class="form-group"><label>Code</label><input type="text" name="code" class="form-control input-sm" placeholder="CUT" required></div>
				<div class="form-group"><label>Name</label><input type="text" name="name" class="form-control input-sm"></div>
				<div class="form-group"><label>Capacity (min/day)</label><input type="number" name="capacity_min_per_day" class="form-control input-sm" value="480"></div>
				<div class="form-group"><label>Cost / hour</label><input type="number" step="0.01" name="cost_per_hour" class="form-control input-sm" value="0"></div>
				<button type="submit" class="btn btn-primary btn-sm">Add work center</button>
			</form>
		</div>
	</div><div class="col-md-8">
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Code</th><th>Name</th><th class="text-right">Cap (min/day)</th><th class="text-right">Cost/hr</th><th>Active</th></tr></thead>
			<tbody>
			<?php if (empty($wcs)): ?>
				<tr><td colspan="5" class="text-muted">No work centers yet.</td></tr>
			<?php else: foreach ($wcs as $w): ?>
				<tr><td><strong><?php echo epc_erp_h($w['code']); ?></strong></td><td><?php echo epc_erp_h($w['name']); ?></td>
				<td class="text-right"><?php echo (int) $w['capacity_min_per_day']; ?></td><td class="text-right"><?php echo epc_erp_money($w['cost_per_hour'], 2); ?></td>
				<td><span class="label label-<?php echo (int) $w['active'] ? 'success' : 'default'; ?>"><?php echo (int) $w['active'] ? 'yes' : 'no'; ?></span></td></tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div></div>

<?php elseif ($view === 'routes'):
	$routes = epc_mfgr_routes($db_link, $companyId); ?>
	<div class="row"><div class="col-md-5">
		<div class="well well-sm">
			<h5><i class="fa fa-plus-circle"></i> New route (up to 4 ops)</h5>
			<form id="epc_mfgr_route" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<div class="row">
					<div class="col-xs-6 form-group"><label>Product item ID</label><input type="number" name="product_item_id" class="form-control input-sm" required></div>
					<div class="col-xs-6 form-group"><label>Route name</label><input type="text" name="name" class="form-control input-sm"></div>
				</div>
				<table class="table table-condensed" style="margin-bottom:6px;">
					<thead><tr><th>Op</th><th>Work center</th><th>Setup</th><th>Run/unit</th></tr></thead>
					<tbody>
					<?php for ($i = 0; $i < 4; $i++): ?>
						<tr>
							<td><input type="number" name="op_no[]" class="form-control input-sm" value="<?php echo ($i + 1) * 10; ?>" style="width:60px;"></td>
							<td><select name="workcenter_id[]" class="form-control input-sm"><option value="0">—</option><?php echo $wcOpts; ?></select></td>
							<td><input type="number" step="0.01" name="setup_min[]" class="form-control input-sm" value="0" style="width:70px;"></td>
							<td><input type="number" step="0.0001" name="run_min_per_unit[]" class="form-control input-sm" value="0" style="width:80px;"></td>
						</tr>
					<?php endfor; ?>
					</tbody>
				</table>
				<button type="submit" class="btn btn-primary btn-sm">Save route</button>
			</form>
		</div>
	</div><div class="col-md-7">
		<table class="table table-bordered table-condensed">
			<thead><tr><th>#</th><th>Product item</th><th>Name</th><th class="text-right">Ops</th></tr></thead>
			<tbody>
			<?php if (empty($routes)): ?>
				<tr><td colspan="4" class="text-muted">No routes yet.</td></tr>
			<?php else: foreach ($routes as $r): ?>
				<tr><td><?php echo (int) $r['id']; ?></td><td><?php echo (int) $r['product_item_id']; ?></td><td><?php echo epc_erp_h($r['name']); ?></td><td class="text-right"><?php echo (int) $r['op_count']; ?></td></tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div></div>

<?php elseif ($view === 'schedule'):
	$routes = epc_mfgr_routes($db_link, $companyId);
	$routeOpts = '';
	foreach ($routes as $r) {
		$routeOpts .= '<option value="' . (int) $r['id'] . '">#' . (int) $r['id'] . ' · item ' . (int) $r['product_item_id'] . ' · ' . epc_erp_h($r['name']) . '</option>';
	}
	$schedRoute = (int) ($_GET['route_id'] ?? 0);
	$schedQty = (float) ($_GET['qty'] ?? 100);
	$finite = !isset($_GET['finite']) || $_GET['finite'] !== '0'; ?>
	<form method="get" class="form-inline" style="margin-bottom:10px;">
		<?php foreach ($_GET as $k => $v) { if (in_array($k, array('route_id', 'qty', 'finite'), true)) continue; echo '<input type="hidden" name="' . epc_erp_h($k) . '" value="' . epc_erp_h((string) $v) . '">'; } ?>
		<input type="hidden" name="mv" value="schedule">
		<div class="form-group"><label>Route&nbsp;</label><select name="route_id" class="form-control input-sm"><option value="0">—</option><?php echo $routeOpts; ?></select></div>
		<div class="form-group">&nbsp;<label>Qty&nbsp;</label><input type="number" step="0.01" name="qty" class="form-control input-sm" value="<?php echo epc_erp_h((string) $schedQty); ?>"></div>
		<div class="checkbox">&nbsp;<label><input type="checkbox" name="finite" value="1" <?php echo $finite ? 'checked' : ''; ?>> Finite capacity</label></div>
		&nbsp;<button class="btn btn-primary btn-sm">Schedule</button>
	</form>
	<?php if ($schedRoute > 0):
		$sch = epc_mfgr_schedule_route($db_link, $schedRoute, $schedQty, time(), $finite); ?>
		<p>Total load: <strong><?php echo epc_erp_money($sch['total_min'], 0); ?> min</strong> · spans <strong><?php echo (int) $sch['total_days']; ?></strong> day(s) · <?php echo $sch['finite'] ? 'finite' : 'infinite'; ?> capacity</p>
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Op</th><th>Work center</th><th>Description</th><th class="text-right">Load (min)</th><th class="text-right">Days</th><th>Start</th><th>End</th><th>Capacity</th></tr></thead>
			<tbody>
			<?php foreach ($sch['operations'] as $o): ?>
				<tr>
					<td><?php echo (int) $o['op_no']; ?></td>
					<td><?php echo epc_erp_h($o['wc_code']); ?></td>
					<td><?php echo epc_erp_h($o['description']); ?></td>
					<td class="text-right"><?php echo epc_erp_money($o['load_min'], 0); ?></td>
					<td class="text-right"><?php echo (int) $o['days']; ?></td>
					<td><small><?php echo date('d M H:i', (int) $o['start_ts']); ?></small></td>
					<td><small><?php echo date('d M H:i', (int) $o['end_ts']); ?></small></td>
					<td><span class="label label-<?php echo $o['capacity_ok'] ? 'success' : 'danger'; ?>"><?php echo $o['capacity_ok'] ? 'ok' : 'over'; ?></span></td>
				</tr>
			<?php endforeach; ?>
			</tbody>
		</table>
	<?php else: ?>
		<p class="text-muted">Pick a route and quantity to preview the forward, sequential capacity schedule.</p>
	<?php endif; ?>

<?php else:
	// MRP
	$planned = epc_mfgr_planned_list($db_link, $companyId); ?>
	<div class="row"><div class="col-md-4">
		<div class="well well-sm">
			<h5><i class="fa fa-refresh"></i> Run MRP (regenerative)</h5>
			<form id="epc_mfgr_mrp" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<p class="text-muted" style="font-size:11px;">One demand line per row as <code>itemId=qty</code> and optional on-hand as <code>itemId:qty</code>. Multi-level BOMs explode automatically.</p>
				<div class="form-group"><label>Demand (itemId=qty, one per line)</label><textarea name="demand" class="form-control input-sm" rows="4" placeholder="100=10&#10;105=25"></textarea></div>
				<div class="form-group"><label>On hand (itemId:qty, optional)</label><textarea name="onhand" class="form-control input-sm" rows="3" placeholder="300:50"></textarea></div>
				<button type="submit" class="btn btn-primary btn-sm">Regenerate plan</button>
			</form>
		</div>
	</div><div class="col-md-8">
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Item</th><th>Type</th><th class="text-right">Qty</th><th class="text-right">Level</th><th>Status</th><th></th></tr></thead>
			<tbody>
			<?php if (empty($planned)): ?>
				<tr><td colspan="6" class="text-muted">No plan. Enter demand and regenerate — lowest-level items are sequenced first (low-level coding).</td></tr>
			<?php else: foreach ($planned as $p): ?>
				<tr>
					<td><strong><?php echo (int) $p['item_id']; ?></strong></td>
					<td><span class="label label-<?php echo $p['order_type'] === 'production' ? 'info' : 'warning'; ?>"><?php echo epc_erp_h($p['order_type']); ?></span></td>
					<td class="text-right"><?php echo epc_erp_money($p['qty'], 2); ?></td>
					<td class="text-right"><?php echo (int) $p['level']; ?></td>
					<td><span class="label label-<?php echo $p['status'] === 'firmed' ? 'success' : 'default'; ?>"><?php echo epc_erp_h($p['status']); ?></span></td>
					<td><?php if ($p['status'] !== 'firmed'): ?><button class="btn btn-success btn-xs epc-mfgr-firm" data-id="<?php echo (int) $p['id']; ?>">Firm</button><?php endif; ?></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div></div>
<?php endif; ?>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	var csrf = <?php echo json_encode($csrfLocal); ?>;
	function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 800); }
	function bind(id, action){ var f=document.getElementById(id); if(f) f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }
	bind('epc_mfgr_wc', 'mfgr_wc_save');
	bind('epc_mfgr_route', 'mfgr_route_save');
	bind('epc_mfgr_mrp', 'mfgr_mrp_run');
	document.querySelectorAll('.epc-mfgr-firm').forEach(function(b){ b.addEventListener('click', function(){ var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('id',b.getAttribute('data-id')); post('mfgr_planned_firm', fd).then(msg); }); });
})();
</script>
