<?php
defined('_ASTEXE_') or die('No access');
/**
 * Data & integration framework — D365 F&O-style data entities, OData-style query
 * explorer, and business events (subscriptions + dispatch log).
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_integration.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_intg_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$view = isset($_GET['iv']) ? (string) $_GET['iv'] : 'entities';
$summary = epc_intg_summary($db_link, $companyId);

erp_page_header(
	'<i class="fa fa-plug"></i> Data &amp; integration',
	'D365 F&amp;O-style data entities, an OData-style query layer ($select/$filter/$orderby/$top), and business events (subscriptions + dispatch log).',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Data &amp; integration'),
	)
);

erp_stat_cards(array(
	array('label' => 'Data entities', 'value' => (string) $summary['entities']),
	array('label' => 'Active subscriptions', 'value' => (string) $summary['subscriptions']),
	array('label' => 'Events logged', 'value' => (string) $summary['events_logged']),
	array('label' => 'Queued deliveries', 'value' => (string) $summary['queued']),
));

$tabBase = epc_erp_tab_url($erpUrl, 'integration', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
$views = array('entities' => 'Data entities', 'query' => 'OData explorer', 'events' => 'Business events');
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<div class="btn-group btn-group-sm" style="margin-bottom:10px;">
	<?php foreach ($views as $k => $lbl): ?>
		<a class="btn btn-<?php echo $view === $k ? 'primary' : 'default'; ?>" href="<?php echo epc_erp_h($tabBase . $sep . 'iv=' . $k); ?>"><?php echo epc_erp_h($lbl); ?></a>
	<?php endforeach; ?>
</div>

<?php if ($view === 'entities'):
	$entities = epc_intg_entities($db_link, $companyId); ?>
	<div class="well well-sm">
		<h5><i class="fa fa-plus-circle"></i> Register data entity</h5>
		<form id="epc_intg_entity" class="form-inline">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="text" name="name" class="form-control input-sm" placeholder="Entity name (e.g. Customers)" required>
			<input type="text" name="source_table" class="form-control input-sm" placeholder="source_table" style="width:150px;" required>
			<input type="text" name="key_field" class="form-control input-sm" placeholder="key (id)" style="width:90px;" value="id">
			<input type="text" name="fields" class="form-control input-sm" placeholder="fields: id,name,country" style="width:220px;">
			<label><input type="checkbox" name="enabled" value="1" checked> enabled</label>
			<button class="btn btn-primary btn-sm">Save</button>
		</form>
	</div>
	<table class="table table-bordered table-condensed">
		<thead><tr><th>Entity</th><th>Source table</th><th>Key</th><th>Fields</th><th>Status</th></tr></thead>
		<tbody>
		<?php if (empty($entities)): ?><tr><td colspan="5" class="text-muted">No data entities registered.</td></tr>
		<?php else: foreach ($entities as $e): ?>
			<tr><td><strong><?php echo epc_erp_h($e['name']); ?></strong></td><td><code><?php echo epc_erp_h($e['source_table']); ?></code></td>
			<td><?php echo epc_erp_h($e['key_field']); ?></td><td><small><?php echo epc_erp_h(implode(', ', $e['fields'])); ?></small></td>
			<td><span class="label label-<?php echo $e['enabled'] ? 'success' : 'default'; ?>"><?php echo $e['enabled'] ? 'enabled' : 'disabled'; ?></span></td></tr>
		<?php endforeach; endif; ?>
		</tbody>
	</table>

<?php elseif ($view === 'query'):
	$entities = epc_intg_entities($db_link, $companyId);
	$qEntity = (string) ($_GET['entity'] ?? (isset($entities[0]) ? $entities[0]['name'] : ''));
	$qSelect = (string) ($_GET['sel'] ?? '');
	$qFilter = (string) ($_GET['flt'] ?? '');
	$qOrder = (string) ($_GET['ord'] ?? '');
	$qTop = (string) ($_GET['top'] ?? '20');
	$rows = array();
	$err = '';
	if ($qEntity !== '' && isset($_GET['run'])) {
		try {
			$rows = epc_intg_entity_query($db_link, $companyId, $qEntity, array('$select' => $qSelect, '$filter' => $qFilter, '$orderby' => $qOrder, '$top' => $qTop));
		} catch (Throwable $ex) {
			$err = $ex->getMessage();
		}
	} ?>
	<div class="well well-sm">
		<form method="get" class="form">
			<?php foreach ($_GET as $k => $v) { if (in_array($k, array('entity', 'sel', 'flt', 'ord', 'top', 'run'), true)) { continue; } echo '<input type="hidden" name="' . epc_erp_h($k) . '" value="' . epc_erp_h((string) $v) . '">'; } ?>
			<input type="hidden" name="iv" value="query">
			<div class="row">
				<div class="col-md-3 form-group"><label>Entity</label>
					<select name="entity" class="form-control input-sm">
						<?php foreach ($entities as $e): ?><option value="<?php echo epc_erp_h($e['name']); ?>" <?php echo $e['name'] === $qEntity ? 'selected' : ''; ?>><?php echo epc_erp_h($e['name']); ?></option><?php endforeach; ?>
					</select></div>
				<div class="col-md-3 form-group"><label>$select</label><input type="text" name="sel" class="form-control input-sm" value="<?php echo epc_erp_h($qSelect); ?>" placeholder="name,amount"></div>
				<div class="col-md-3 form-group"><label>$filter</label><input type="text" name="flt" class="form-control input-sm" value="<?php echo epc_erp_h($qFilter); ?>" placeholder="country eq 'AE' and amount gt 100"></div>
				<div class="col-md-2 form-group"><label>$orderby</label><input type="text" name="ord" class="form-control input-sm" value="<?php echo epc_erp_h($qOrder); ?>" placeholder="amount desc"></div>
				<div class="col-md-1 form-group"><label>$top</label><input type="number" name="top" class="form-control input-sm" value="<?php echo epc_erp_h($qTop); ?>"></div>
			</div>
			<button class="btn btn-primary btn-sm" name="run" value="1">Run query</button>
		</form>
	</div>
	<?php if ($err !== ''): ?><div class="alert alert-danger"><?php echo epc_erp_h($err); ?></div><?php endif; ?>
	<?php if (!empty($rows)): ?>
		<table class="table table-bordered table-condensed">
			<thead><tr><?php foreach (array_keys($rows[0]) as $col): ?><th><?php echo epc_erp_h($col); ?></th><?php endforeach; ?></tr></thead>
			<tbody>
			<?php foreach ($rows as $r): ?><tr><?php foreach ($r as $v): ?><td><?php echo epc_erp_h((string) $v); ?></td><?php endforeach; ?></tr><?php endforeach; ?>
			</tbody>
		</table>
		<p class="text-muted"><?php echo count($rows); ?> row(s). Only whitelisted entity fields are queryable.</p>
	<?php elseif (isset($_GET['run']) && $err === ''): ?><p class="text-muted">No rows.</p><?php endif; ?>

<?php else:
	$subs = epc_intg_subs($db_link, $companyId);
	$log = epc_intg_event_log($db_link, $companyId, 50); ?>
	<div class="row"><div class="col-md-5">
		<div class="well well-sm">
			<h5><i class="fa fa-rss"></i> Subscribe to event</h5>
			<form id="epc_intg_sub" class="form-inline">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<select name="event" class="form-control input-sm">
					<?php foreach (epc_intg_event_catalog() as $ev): ?><option value="<?php echo epc_erp_h($ev); ?>"><?php echo epc_erp_h($ev); ?></option><?php endforeach; ?>
				</select>
				<select name="target_type" class="form-control input-sm"><option value="webhook">webhook</option><option value="internal">internal</option><option value="email">email</option></select>
				<input type="text" name="target" class="form-control input-sm" placeholder="URL / email" style="width:180px;">
				<button class="btn btn-primary btn-sm">Subscribe</button>
			</form>
		</div>
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Event</th><th>Target</th><th>Status</th></tr></thead>
			<tbody>
			<?php if (empty($subs)): ?><tr><td colspan="3" class="text-muted">No subscriptions.</td></tr>
			<?php else: foreach ($subs as $s): ?>
				<tr><td><strong><?php echo epc_erp_h($s['event']); ?></strong></td>
				<td><span class="label label-default"><?php echo epc_erp_h($s['target_type']); ?></span> <small><?php echo epc_erp_h($s['target']); ?></small></td>
				<td><span class="label label-<?php echo $s['active'] ? 'success' : 'default'; ?>"><?php echo $s['active'] ? 'active' : 'off'; ?></span></td></tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
		<div class="well well-sm">
			<h5><i class="fa fa-bolt"></i> Raise test event</h5>
			<form id="epc_intg_raise" class="form-inline">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<select name="event" class="form-control input-sm">
					<?php foreach (epc_intg_event_catalog() as $ev): ?><option value="<?php echo epc_erp_h($ev); ?>"><?php echo epc_erp_h($ev); ?></option><?php endforeach; ?>
				</select>
				<input type="text" name="payload" class="form-control input-sm" placeholder='{"order_id":1}' style="width:160px;">
				<button class="btn btn-warning btn-sm">Raise</button>
			</form>
		</div>
	</div><div class="col-md-7">
		<div class="panel panel-default">
			<div class="panel-heading"><strong>Dispatch log</strong></div>
			<table class="table table-condensed" style="margin-bottom:0;">
				<thead><tr><th>Event</th><th>Target</th><th>Status</th><th>When</th></tr></thead>
				<tbody>
				<?php if (empty($log)): ?><tr><td colspan="4" class="text-muted">No events dispatched.</td></tr>
				<?php else: foreach ($log as $l):
					$sc = $l['status'] === 'queued' ? 'info' : ($l['status'] === 'no_subscriber' ? 'default' : 'success'); ?>
					<tr><td><?php echo epc_erp_h($l['event']); ?></td>
					<td><small><?php echo epc_erp_h($l['target_type'] !== '' ? $l['target_type'] . ' · ' . $l['target'] : '—'); ?></small></td>
					<td><span class="label label-<?php echo $sc; ?>"><?php echo epc_erp_h($l['status']); ?></span></td>
					<td><small><?php echo date('d M H:i', (int) $l['time_created']); ?></small></td></tr>
				<?php endforeach; endif; ?>
				</tbody>
			</table>
		</div>
	</div></div>
<?php endif; ?>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 900); }
	function bind(id, action){ var f=document.getElementById(id); if(f) f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }
	bind('epc_intg_entity', 'intg_entity_save');
	bind('epc_intg_sub', 'intg_sub_save');
	bind('epc_intg_raise', 'intg_event_raise');
})();
</script>
