<?php
defined('_ASTEXE_') or die('No access');
/**
 * Costing value-models — D365 F&O-style Standard / FIFO / LIFO / Moving-average
 * inventory valuation with recalculation / closing.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cost_models.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_costm_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$view = isset($_GET['mv']) ? (string) $_GET['mv'] : 'items';
$summary = epc_costm_summary($db_link, $companyId);

erp_page_header(
	'<i class="fa fa-balance-scale"></i> Costing value-models',
	'D365 F&amp;O-style inventory valuation — Standard / FIFO / LIFO / Moving-average with recalculation &amp; closing. Analysis layer; live valuation is unchanged.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Costing value-models'),
	)
);

erp_stat_cards(array(
	array('label' => 'Items with model', 'value' => (string) $summary['items']),
	array('label' => 'Closing runs', 'value' => (string) $summary['closings']),
	array('label' => 'Closing value (latest)', 'value' => epc_erp_money($summary['closing_value'], 0)),
));

$tabBase = epc_erp_tab_url($erpUrl, 'cost_models', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
$views = array('items' => 'Items & closing', 'compare' => 'Model comparison');
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<div class="btn-group btn-group-sm" style="margin-bottom:10px;">
	<?php foreach ($views as $k => $lbl): ?>
		<a class="btn btn-<?php echo $view === $k ? 'primary' : 'default'; ?>" href="<?php echo epc_erp_h($tabBase . $sep . 'mv=' . $k); ?>"><?php echo epc_erp_h($lbl); ?></a>
	<?php endforeach; ?>
</div>

<?php if ($view === 'items'):
	$items = epc_costm_items($db_link, $companyId);
	$selItem = (int) ($_GET['item_id'] ?? 0); ?>
	<div class="row"><div class="col-md-5">
		<div class="well well-sm">
			<h5><i class="fa fa-tag"></i> Assign costing model</h5>
			<form id="epc_costm_item" class="form-inline">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="number" name="item_id" class="form-control input-sm" placeholder="Item ID" style="width:110px;" required>
				<select name="model" class="form-control input-sm">
					<?php foreach (epc_costm_models() as $m): ?><option value="<?php echo $m; ?>"><?php echo $m; ?></option><?php endforeach; ?>
				</select>
				<input type="number" step="0.0001" name="std_cost" class="form-control input-sm" placeholder="Std cost" style="width:110px;">
				<button class="btn btn-primary btn-sm">Save</button>
			</form>
		</div>
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Item</th><th>Model</th><th class="text-right">Std cost</th><th></th></tr></thead>
			<tbody>
			<?php if (empty($items)): ?><tr><td colspan="4" class="text-muted">No items assigned a model yet.</td></tr>
			<?php else: foreach ($items as $it): ?>
				<tr><td><strong><?php echo (int) $it['item_id']; ?></strong></td>
				<td><span class="label label-info"><?php echo epc_erp_h($it['model']); ?></span></td>
				<td class="text-right"><?php echo number_format((float) $it['std_cost'], 4); ?></td>
				<td><a class="btn btn-default btn-xs" href="<?php echo epc_erp_h($tabBase . $sep . 'mv=items&item_id=' . (int) $it['item_id']); ?>">Open</a></td></tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div><div class="col-md-7">
		<?php if ($selItem > 0):
			$txns = epc_costm_txns($db_link, $companyId, $selItem);
			$closes = epc_costm_closes($db_link, $companyId, $selItem);
			$itm = epc_costm_item_get($db_link, $companyId, $selItem); ?>
			<div class="panel panel-default">
				<div class="panel-heading"><strong>Item <?php echo (int) $selItem; ?></strong> · model <span class="label label-info"><?php echo epc_erp_h($itm['model']); ?></span>
					<form id="epc_costm_close" style="display:inline; margin-left:10px;">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
						<input type="hidden" name="item_id" value="<?php echo (int) $selItem; ?>">
						<input type="text" name="label" class="input-sm" placeholder="Period label" style="width:110px;">
						<button class="btn btn-success btn-xs">Run closing</button>
					</form>
				</div>
				<div class="panel-body">
					<form id="epc_costm_txn" class="form-inline" style="margin-bottom:8px;">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
						<input type="hidden" name="item_id" value="<?php echo (int) $selItem; ?>">
						<select name="txn_type" class="form-control input-sm"><option value="receipt">Receipt</option><option value="issue">Issue</option></select>
						<input type="number" step="0.0001" name="qty" class="form-control input-sm" placeholder="Qty" style="width:90px;" required>
						<input type="number" step="0.0001" name="unit_cost" class="form-control input-sm" placeholder="Unit cost" style="width:100px;">
						<button class="btn btn-primary btn-sm">Add</button>
					</form>
					<table class="table table-condensed">
						<thead><tr><th>Date</th><th>Type</th><th class="text-right">Qty</th><th class="text-right">Unit cost</th></tr></thead>
						<tbody>
						<?php if (empty($txns)): ?><tr><td colspan="4" class="text-muted">No transactions.</td></tr>
						<?php else: foreach ($txns as $t): ?>
							<tr><td><small><?php echo date('d M Y', (int) $t['txn_date']); ?></small></td>
							<td><span class="label label-<?php echo $t['txn_type'] === 'receipt' ? 'success' : 'warning'; ?>"><?php echo epc_erp_h($t['txn_type']); ?></span></td>
							<td class="text-right"><?php echo number_format((float) $t['qty'], 4); ?></td><td class="text-right"><?php echo number_format((float) $t['unit_cost'], 4); ?></td></tr>
						<?php endforeach; endif; ?>
						</tbody>
					</table>
				</div>
			</div>
			<div class="panel panel-default">
				<div class="panel-heading"><strong>Closing history</strong></div>
				<table class="table table-condensed" style="margin-bottom:0;">
					<thead><tr><th>Label</th><th>Model</th><th class="text-right">COGS</th><th class="text-right">Closing qty</th><th class="text-right">Closing value</th><th class="text-right">Variance</th></tr></thead>
					<tbody>
					<?php if (empty($closes)): ?><tr><td colspan="6" class="text-muted">No closing runs.</td></tr>
					<?php else: foreach ($closes as $c): ?>
						<tr><td><?php echo epc_erp_h($c['label']); ?></td><td><?php echo epc_erp_h($c['model']); ?></td>
						<td class="text-right"><?php echo epc_erp_money($c['cogs'], 2); ?></td><td class="text-right"><?php echo number_format((float) $c['closing_qty'], 4); ?></td>
						<td class="text-right"><strong><?php echo epc_erp_money($c['closing_value'], 2); ?></strong></td>
						<td class="text-right"><?php echo epc_erp_money($c['variance'], 2); ?></td></tr>
					<?php endforeach; endif; ?>
					</tbody>
				</table>
			</div>
		<?php else: ?>
			<p class="text-muted">Pick an item to add receipt/issue transactions and run a closing.</p>
		<?php endif; ?>
	</div></div>

<?php else:
	$rawT = (string) ($_GET['t'] ?? "receipt|10|10\nreceipt|10|12\nissue|15|0");
	$std = (float) ($_GET['std'] ?? 11);
	$txns = array();
	foreach (preg_split('/[\r\n]+/', $rawT) as $line) {
		$line = trim($line);
		if ($line === '' || strpos($line, '|') === false) { continue; }
		$p = array_map('trim', explode('|', $line));
		if (count($p) < 3) { continue; }
		$txns[] = array('txn_type' => $p[0] === 'issue' ? 'issue' : 'receipt', 'qty' => (float) $p[1], 'unit_cost' => (float) $p[2]);
	}
	$cmp = epc_costm_compare($txns, $std); ?>
	<div class="well well-sm">
		<form method="get" class="form">
			<?php foreach ($_GET as $k => $v) { if (in_array($k, array('t', 'std'), true)) { continue; } echo '<input type="hidden" name="' . epc_erp_h($k) . '" value="' . epc_erp_h((string) $v) . '">'; } ?>
			<input type="hidden" name="mv" value="compare">
			<div class="row"><div class="col-md-8 form-group"><label>Transactions (<code>type|qty|unit_cost</code> per line; type = receipt|issue)</label><textarea name="t" class="form-control input-sm" rows="4"><?php echo epc_erp_h($rawT); ?></textarea></div>
			<div class="col-md-2 form-group"><label>Standard cost</label><input type="number" step="0.0001" name="std" class="form-control input-sm" value="<?php echo epc_erp_h((string) $std); ?>"></div>
			<div class="col-md-2 form-group"><label>&nbsp;</label><br><button class="btn btn-primary btn-sm">Compare</button></div></div>
		</form>
	</div>
	<table class="table table-bordered table-condensed">
		<thead><tr><th>Model</th><th class="text-right">COGS</th><th class="text-right">Closing qty</th><th class="text-right">Closing value</th><th class="text-right">Variance (PPV)</th></tr></thead>
		<tbody>
		<?php foreach ($cmp as $m => $r): ?>
			<tr><td><strong><?php echo epc_erp_h($m); ?></strong></td>
			<td class="text-right"><?php echo epc_erp_money($r['cogs'], 2); ?></td>
			<td class="text-right"><?php echo number_format((float) $r['closing_qty'], 4); ?></td>
			<td class="text-right"><strong><?php echo epc_erp_money($r['closing_value'], 2); ?></strong></td>
			<td class="text-right"><?php echo $m === 'standard' ? epc_erp_money($r['variance'], 2) : '—'; ?></td></tr>
		<?php endforeach; ?>
		</tbody>
	</table>
	<p class="text-muted" style="font-size:11px;">Same transactions, four value-models side by side. FIFO vs LIFO COGS divergence reflects cost-flow assumption; Standard shows purchase-price variance vs the standard cost.</p>
<?php endif; ?>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 800); }
	function bind(id, action){ var f=document.getElementById(id); if(f) f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }
	bind('epc_costm_item', 'costm_item_set');
	bind('epc_costm_txn', 'costm_txn_add');
	bind('epc_costm_close', 'costm_close_run');
})();
</script>
