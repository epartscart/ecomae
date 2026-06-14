<?php
defined('_ASTEXE_') or die('No access');
/**
 * Advanced WMS — D365 F&O-style warehousing: locations/bins, license plates,
 * work pool (put-away / pick / move / count) with a mobile "RF" complete
 * action, inbound receive, and outbound waves.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_wms.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_wms_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$view = isset($_GET['wv']) ? (string) $_GET['wv'] : 'work';
$workTypes = epc_wms_work_types();
$locTypes = epc_wms_location_types();

$summary = epc_wms_summary($db_link, $companyId);
$locations = epc_wms_locations($db_link, $companyId);
$locOpts = '';
foreach ($locations as $l) {
	$locOpts .= '<option value="' . (int) $l['id'] . '">' . epc_erp_h($l['warehouse'] . ' / ' . $l['code']) . '</option>';
}

erp_page_header(
	'<i class="fa fa-cubes"></i> Advanced WMS',
	'D365 F&amp;O-style warehouse management — locations &amp; bins, license plates, wave processing and a mobile RF work pool (put-away, pick, move, cycle count).',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Advanced WMS'),
	)
);

erp_stat_cards(array(
	array('label' => 'Locations', 'value' => (string) $summary['locations']),
	array('label' => 'License plates', 'value' => (string) $summary['license_plates']),
	array('label' => 'Open work', 'value' => (string) $summary['open_work']),
	array('label' => 'Open waves', 'value' => (string) $summary['open_waves']),
	array('label' => 'On hand', 'value' => epc_erp_money($summary['on_hand'], 0)),
));

$tabBase = epc_erp_tab_url($erpUrl, 'wms', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
$views = array('work' => 'Work pool', 'inbound' => 'Inbound', 'waves' => 'Waves', 'lp' => 'License plates', 'locations' => 'Locations');
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<div class="btn-group btn-group-sm" style="margin-bottom:10px;">
	<?php foreach ($views as $k => $lbl): ?>
		<a class="btn btn-<?php echo $view === $k ? 'primary' : 'default'; ?>" href="<?php echo epc_erp_h($tabBase . $sep . 'wv=' . $k); ?>"><?php echo epc_erp_h($lbl); ?></a>
	<?php endforeach; ?>
</div>

<?php if ($view === 'work'):
	$work = epc_wms_work_list($db_link, $companyId);
	$stLabel = array('open' => 'default', 'in_progress' => 'warning', 'closed' => 'success'); ?>
	<div class="table-responsive">
	<table class="table table-bordered table-condensed">
		<thead><tr><th>#</th><th>Type</th><th>Item</th><th class="text-right">Qty</th><th>From</th><th>To</th><th>Ref</th><th>Assigned</th><th>Status</th><th>RF action</th></tr></thead>
		<tbody>
		<?php if (empty($work)): ?>
			<tr><td colspan="10" class="text-muted">No work. Receive stock (Inbound) or release a wave to generate pick work.</td></tr>
		<?php else: foreach ($work as $w): ?>
			<tr>
				<td><?php echo (int) $w['id']; ?></td>
				<td><span class="label label-info"><?php echo epc_erp_h($workTypes[$w['work_type']] ?? $w['work_type']); ?></span></td>
				<td><?php echo epc_erp_h($w['item']); ?></td>
				<td class="text-right"><?php echo epc_erp_money($w['qty'], 0); ?></td>
				<td><small><?php echo epc_erp_h($w['from_code'] ?? '—'); ?></small></td>
				<td><small><?php echo epc_erp_h($w['to_code'] ?? '—'); ?></small></td>
				<td><small><?php echo epc_erp_h($w['reference']); ?></small></td>
				<td><small><?php echo epc_erp_h($w['assigned_to']); ?></small></td>
				<td><span class="label label-<?php echo $stLabel[$w['status']] ?? 'default'; ?>"><?php echo epc_erp_h(str_replace('_', ' ', (string) $w['status'])); ?></span></td>
				<td>
					<?php if ((string) $w['status'] !== 'closed'): ?>
						<button class="btn btn-success btn-xs epc-wms-complete" data-id="<?php echo (int) $w['id']; ?>"><i class="fa fa-check"></i> Confirm</button>
					<?php else: ?><small class="text-muted">done</small><?php endif; ?>
				</td>
			</tr>
		<?php endforeach; endif; ?>
		</tbody>
	</table>
	</div>
	<p class="text-muted" style="font-size:11px;"><i class="fa fa-mobile"></i> "Confirm" is the mobile RF step — scanning the LP/location then confirming applies the stock move (put-away relocates the LP, pick deducts on-hand, move relocates, count adjusts).</p>

<?php elseif ($view === 'inbound'): ?>
	<div class="row"><div class="col-md-5">
		<div class="well well-sm">
			<h5><i class="fa fa-download"></i> Receive stock</h5>
			<form id="epc_wms_receive" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<div class="form-group"><label>Item / SKU</label><input type="text" name="item" class="form-control input-sm" required></div>
				<div class="form-group"><label>Quantity</label><input type="number" step="0.001" name="qty" class="form-control input-sm" required></div>
				<div class="form-group"><label>Receiving location</label><select name="receive_location_id" class="form-control input-sm" required><?php echo $locOpts; ?></select></div>
				<div class="form-group"><label>Put-away destination</label><select name="putaway_location_id" class="form-control input-sm" required><?php echo $locOpts; ?></select></div>
				<div class="form-group"><label>Reference (PO / ASN)</label><input type="text" name="reference" class="form-control input-sm"></div>
				<div class="form-group"><label>License plate <small class="text-muted">(blank = auto)</small></label><input type="text" name="lp_code" class="form-control input-sm"></div>
				<button type="submit" class="btn btn-primary btn-sm">Receive &amp; raise put-away</button>
			</form>
		</div>
	</div><div class="col-md-7">
		<p class="text-muted">Receiving creates a license plate at the dock and raises a <strong>put-away</strong> work line. Complete it in the Work pool to move stock into its bin.</p>
	</div></div>

<?php elseif ($view === 'waves'):
	$waves = epc_wms_waves($db_link, $companyId);
	$stLabel = array('open' => 'default', 'released' => 'warning', 'closed' => 'success'); ?>
	<div class="row"><div class="col-md-5">
		<div class="well well-sm">
			<h5><i class="fa fa-plus-circle"></i> New wave + pick line</h5>
			<form id="epc_wms_wave" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<div class="form-group"><label>Order reference</label><input type="text" name="reference" class="form-control input-sm" placeholder="SO-1234" required></div>
				<div class="form-group"><label>Item / SKU</label><input type="text" name="item" class="form-control input-sm" required></div>
				<div class="form-group"><label>Quantity</label><input type="number" step="0.001" name="qty" class="form-control input-sm" required></div>
				<div class="form-group"><label>Pick from</label><select name="from_location_id" class="form-control input-sm"><option value="0">— any —</option><?php echo $locOpts; ?></select></div>
				<div class="form-group"><label>Stage to (ship)</label><select name="to_location_id" class="form-control input-sm"><option value="0">—</option><?php echo $locOpts; ?></select></div>
				<button type="submit" class="btn btn-primary btn-sm">Create wave + pick</button>
			</form>
		</div>
	</div><div class="col-md-7">
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Wave</th><th>Ref</th><th>Lines</th><th>Status</th><th></th></tr></thead>
			<tbody>
			<?php if (empty($waves)): ?>
				<tr><td colspan="5" class="text-muted">No waves yet.</td></tr>
			<?php else: foreach ($waves as $wv): ?>
				<tr>
					<td><strong><?php echo epc_erp_h($wv['wave_no']); ?></strong></td>
					<td><small><?php echo epc_erp_h($wv['reference']); ?></small></td>
					<td><?php echo (int) $wv['work_done']; ?>/<?php echo (int) $wv['work_lines']; ?></td>
					<td><span class="label label-<?php echo $stLabel[$wv['status']] ?? 'default'; ?>"><?php echo epc_erp_h($wv['status']); ?></span></td>
					<td><?php if ((string) $wv['status'] === 'open'): ?><button class="btn btn-warning btn-xs epc-wms-release" data-id="<?php echo (int) $wv['id']; ?>">Release</button><?php endif; ?></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
		<p class="text-muted" style="font-size:11px;">Release a wave to make its pick work actionable on RF. The wave auto-closes when all pick lines are confirmed.</p>
	</div></div>

<?php elseif ($view === 'lp'):
	$lps = epc_wms_lps($db_link, $companyId, '', false); ?>
	<div class="table-responsive">
	<table class="table table-bordered table-condensed">
		<thead><tr><th>LP</th><th>Item</th><th class="text-right">Qty</th><th>Location</th><th>Status</th></tr></thead>
		<tbody>
		<?php if (empty($lps)): ?>
			<tr><td colspan="5" class="text-muted">No license plates yet.</td></tr>
		<?php else: foreach ($lps as $p): ?>
			<tr<?php echo (string) $p['status'] !== 'active' ? ' class="text-muted"' : ''; ?>>
				<td><strong><?php echo epc_erp_h($p['lp_code']); ?></strong></td>
				<td><?php echo epc_erp_h($p['item']); ?></td>
				<td class="text-right"><?php echo epc_erp_money($p['qty'], 0); ?></td>
				<td><small><?php echo epc_erp_h(($p['warehouse'] ?? '') . ' / ' . ($p['location_code'] ?? '—')); ?></small></td>
				<td><span class="label label-<?php echo (string) $p['status'] === 'active' ? 'success' : 'default'; ?>"><?php echo epc_erp_h($p['status']); ?></span></td>
			</tr>
		<?php endforeach; endif; ?>
		</tbody>
	</table>
	</div>

<?php else:
	// locations
	?>
	<div class="row"><div class="col-md-4">
		<div class="well well-sm">
			<h5><i class="fa fa-plus-circle"></i> New location / bin</h5>
			<form id="epc_wms_location" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<div class="row">
					<div class="col-xs-6 form-group"><label>Warehouse</label><input type="text" name="warehouse" class="form-control input-sm" value="MAIN"></div>
					<div class="col-xs-6 form-group"><label>Zone</label><input type="text" name="zone" class="form-control input-sm"></div>
				</div>
				<div class="form-group"><label>Bin code</label><input type="text" name="code" class="form-control input-sm" placeholder="A-01-01" required></div>
				<div class="row">
					<div class="col-xs-6 form-group"><label>Type</label><select name="type" class="form-control input-sm"><?php foreach ($locTypes as $k => $lbl): ?><option value="<?php echo $k; ?>"><?php echo epc_erp_h($lbl); ?></option><?php endforeach; ?></select></div>
					<div class="col-xs-6 form-group"><label>Capacity</label><input type="number" name="capacity" class="form-control input-sm" value="0"></div>
				</div>
				<button type="submit" class="btn btn-primary btn-sm">Add location</button>
			</form>
		</div>
	</div><div class="col-md-8">
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Warehouse</th><th>Zone</th><th>Bin</th><th>Type</th><th class="text-right">On hand</th><th></th></tr></thead>
			<tbody>
			<?php if (empty($locations)): ?>
				<tr><td colspan="6" class="text-muted">No locations. Add a receiving dock, pick faces and a shipping dock to start.</td></tr>
			<?php else: foreach ($locations as $l): ?>
				<tr>
					<td><?php echo epc_erp_h($l['warehouse']); ?></td>
					<td><small><?php echo epc_erp_h($l['zone']); ?></small></td>
					<td><strong><?php echo epc_erp_h($l['code']); ?></strong></td>
					<td><small><?php echo epc_erp_h($locTypes[$l['type']] ?? $l['type']); ?></small></td>
					<td class="text-right"><?php echo epc_erp_money($l['on_hand'], 0); ?></td>
					<td><button class="btn btn-link btn-xs text-danger epc-wms-loc-del" data-id="<?php echo (int) $l['id']; ?>">Delete</button></td>
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
	bind('epc_wms_receive', 'wms_receive');
	bind('epc_wms_wave', 'wms_wave_create');
	bind('epc_wms_location', 'wms_location_save');
	function clickAll(sel, action, confirmMsg){ document.querySelectorAll(sel).forEach(function(b){ b.addEventListener('click', function(){ if(confirmMsg && !confirm(confirmMsg)) return; var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('id',b.getAttribute('data-id')); post(action, fd).then(msg); }); }); }
	clickAll('.epc-wms-complete', 'wms_work_complete', null);
	clickAll('.epc-wms-release', 'wms_wave_release', null);
	clickAll('.epc-wms-loc-del', 'wms_location_delete', 'Delete this location?');
})();
</script>
