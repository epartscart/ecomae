<?php
defined('_ASTEXE_') or die('No access');
/**
 * Retail / Commerce — Commerce channels, assortments, periodic
 * discounts, POS sales and end-of-day statements.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_retail.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_rtl_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$view = isset($_GET['rv']) ? (string) $_GET['rv'] : 'channels';
$summary = epc_rtl_summary($db_link, $companyId);

erp_page_header(
	'<i class="fa fa-shopping-cart"></i> Retail &amp; Commerce',
	'Commerce channels, assortments, periodic discounts, POS sales and end-of-day statements.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Retail &amp; Commerce'),
	)
);

erp_stat_cards(array(
	array('label' => 'Channels', 'value' => (string) $summary['channels']),
	array('label' => 'Active discounts', 'value' => (string) $summary['discounts']),
	array('label' => 'POS transactions', 'value' => (string) $summary['transactions']),
	array('label' => 'Sales total', 'value' => number_format((float) $summary['sales_total'], 2)),
));

$tabBase = epc_erp_tab_url($erpUrl, 'retail_commerce', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
$views = array('channels' => 'Channels & assortments', 'discounts' => 'Periodic discounts', 'pos' => 'POS & statement');
$channels = epc_rtl_channels($db_link, $companyId);
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<div class="btn-group btn-group-sm" style="margin-bottom:10px;">
	<?php foreach ($views as $k => $lbl): ?>
		<a class="btn btn-<?php echo $view === $k ? 'primary' : 'default'; ?>" href="<?php echo epc_erp_h($tabBase . $sep . 'rv=' . $k); ?>"><?php echo epc_erp_h($lbl); ?></a>
	<?php endforeach; ?>
</div>

<?php if ($view === 'channels'):
	$selCh = (int) ($_GET['channel_id'] ?? 0); ?>
	<div class="row"><div class="col-md-6">
		<div class="well well-sm">
			<h5><i class="fa fa-plus-circle"></i> New channel</h5>
			<form id="epc_rtl_channel" class="form-inline">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="text" name="code" class="form-control input-sm" placeholder="Code" style="width:90px;" required>
				<input type="text" name="name" class="form-control input-sm" placeholder="Name" style="width:170px;">
				<select name="channel_type" class="form-control input-sm"><option value="store">store</option><option value="online">online</option><option value="callcenter">call center</option></select>
				<input type="text" name="currency" class="form-control input-sm" placeholder="CUR" style="width:60px;">
				<label><input type="checkbox" name="active" value="1" checked> active</label>
				<button class="btn btn-primary btn-sm">Save</button>
			</form>
		</div>
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Code</th><th>Name</th><th>Type</th><th>Cur</th><th class="text-right">Assort.</th><th></th></tr></thead>
			<tbody>
			<?php if (empty($channels)): ?><tr><td colspan="6" class="text-muted">No channels.</td></tr>
			<?php else: foreach ($channels as $c): ?>
				<tr><td><strong><?php echo epc_erp_h($c['code']); ?></strong></td><td><?php echo epc_erp_h($c['name']); ?></td>
				<td><span class="label label-default"><?php echo epc_erp_h($c['channel_type']); ?></span></td><td><?php echo epc_erp_h($c['currency']); ?></td>
				<td class="text-right"><?php echo (int) $c['assort_count']; ?></td>
				<td><a class="btn btn-default btn-xs" href="<?php echo epc_erp_h($tabBase . $sep . 'rv=channels&channel_id=' . (int) $c['id']); ?>">Assortment</a></td></tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div><div class="col-md-6">
		<?php if ($selCh > 0):
			$assort = epc_rtl_assortments($db_link, $selCh); ?>
			<div class="panel panel-default">
				<div class="panel-heading"><strong>Channel assortment</strong></div>
				<div class="panel-body">
					<form id="epc_rtl_assort" class="form-inline" style="margin-bottom:8px;">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
						<input type="hidden" name="channel_id" value="<?php echo (int) $selCh; ?>">
						<input type="number" name="item_id" class="form-control input-sm" placeholder="Item ID" required>
						<select name="active" class="form-control input-sm"><option value="1">include</option><option value="0">exclude</option></select>
						<button class="btn btn-primary btn-sm">Set</button>
					</form>
					<table class="table table-condensed">
						<thead><tr><th>Item ID</th><th>Status</th></tr></thead>
						<tbody>
						<?php if (empty($assort)): ?><tr><td colspan="2" class="text-muted">No items.</td></tr>
						<?php else: foreach ($assort as $a): ?>
							<tr><td><?php echo (int) $a['item_id']; ?></td><td><span class="label label-<?php echo $a['active'] ? 'success' : 'default'; ?>"><?php echo $a['active'] ? 'included' : 'excluded'; ?></span></td></tr>
						<?php endforeach; endif; ?>
						</tbody>
					</table>
				</div>
			</div>
		<?php else: ?><p class="text-muted">Pick a channel to manage its assortment.</p><?php endif; ?>
	</div></div>

<?php elseif ($view === 'discounts'):
	$discs = epc_rtl_discounts($db_link, $companyId); ?>
	<div class="well well-sm">
		<h5><i class="fa fa-tag"></i> New periodic discount</h5>
		<form id="epc_rtl_discount" class="form-inline">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<select name="channel_id" class="form-control input-sm"><option value="0">All channels</option>
				<?php foreach ($channels as $c): ?><option value="<?php echo (int) $c['id']; ?>"><?php echo epc_erp_h($c['code']); ?></option><?php endforeach; ?>
			</select>
			<input type="text" name="code" class="form-control input-sm" placeholder="Code" style="width:90px;" required>
			<input type="text" name="name" class="form-control input-sm" placeholder="Name" style="width:150px;">
			<select name="disc_type" class="form-control input-sm"><option value="percent">percent</option><option value="amount">amount</option></select>
			<input type="number" step="0.01" name="value" class="form-control input-sm" placeholder="value" style="width:80px;">
			<label><input type="checkbox" name="active" value="1" checked> active</label>
			<button class="btn btn-primary btn-sm">Save</button>
		</form>
	</div>
	<table class="table table-bordered table-condensed">
		<thead><tr><th>Code</th><th>Name</th><th>Channel</th><th>Type</th><th class="text-right">Value</th><th>Status</th></tr></thead>
		<tbody>
		<?php if (empty($discs)): ?><tr><td colspan="6" class="text-muted">No discounts.</td></tr>
		<?php else: foreach ($discs as $d):
			$chName = 'All';
			foreach ($channels as $c) { if ((int) $c['id'] === (int) $d['channel_id']) { $chName = $c['code']; break; } } ?>
			<tr><td><strong><?php echo epc_erp_h($d['code']); ?></strong></td><td><?php echo epc_erp_h($d['name']); ?></td>
			<td><?php echo epc_erp_h($chName); ?></td><td><?php echo epc_erp_h($d['disc_type']); ?></td>
			<td class="text-right"><?php echo epc_erp_h(rtrim(rtrim((string) $d['value'], '0'), '.')); ?><?php echo $d['disc_type'] === 'percent' ? '%' : ''; ?></td>
			<td><span class="label label-<?php echo $d['active'] ? 'success' : 'default'; ?>"><?php echo $d['active'] ? 'active' : 'off'; ?></span></td></tr>
		<?php endforeach; endif; ?>
		</tbody>
	</table>

<?php else:
	$selCh = (int) ($_GET['channel_id'] ?? (isset($channels[0]) ? $channels[0]['id'] : 0));
	$txns = $selCh > 0 ? epc_rtl_transactions($db_link, $companyId, $selCh, 0, 0, 50) : array();
	$stmt = $selCh > 0 ? epc_rtl_statement($db_link, $companyId, $selCh, 0, 0) : null; ?>
	<form method="get" class="form-inline" style="margin-bottom:10px;">
		<?php foreach ($_GET as $k => $v) { if (in_array($k, array('channel_id'), true)) { continue; } echo '<input type="hidden" name="' . epc_erp_h($k) . '" value="' . epc_erp_h((string) $v) . '">'; } ?>
		<input type="hidden" name="rv" value="pos">
		<label>Channel</label>
		<select name="channel_id" class="form-control input-sm" onchange="this.form.submit()">
			<?php foreach ($channels as $c): ?><option value="<?php echo (int) $c['id']; ?>" <?php echo (int) $c['id'] === $selCh ? 'selected' : ''; ?>><?php echo epc_erp_h($c['code'] . ' — ' . $c['name']); ?></option><?php endforeach; ?>
		</select>
	</form>
	<?php if ($selCh > 0): ?>
	<div class="row"><div class="col-md-6">
		<div class="well well-sm">
			<h5><i class="fa fa-cash-register"></i> POS sale (single line)</h5>
			<form id="epc_rtl_sale" class="form-inline">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="hidden" name="channel_id" value="<?php echo (int) $selCh; ?>">
				<input type="number" name="item_id" class="form-control input-sm" placeholder="Item ID" style="width:90px;" required>
				<input type="number" step="0.0001" name="qty" class="form-control input-sm" placeholder="Qty" style="width:80px;" required>
				<input type="number" step="0.0001" name="unit_price" class="form-control input-sm" placeholder="Unit price" style="width:100px;" required>
				<select name="tender" class="form-control input-sm"><option value="cash">cash</option><option value="card">card</option><option value="online">online</option><option value="voucher">voucher</option></select>
				<input type="number" step="0.01" name="tax_rate" class="form-control input-sm" placeholder="Tax %" style="width:70px;" value="5">
				<button class="btn btn-success btn-sm">Sell</button>
			</form>
			<p class="text-muted" style="margin-top:6px;">Active channel discounts are applied automatically (best discount wins). Tax % is country-driven — set from the tenant's registration country.</p>
		</div>
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Receipt</th><th class="text-right">Net</th><th class="text-right">Tax</th><th class="text-right">Total</th><th>Tender</th></tr></thead>
			<tbody>
			<?php if (empty($txns)): ?><tr><td colspan="5" class="text-muted">No transactions.</td></tr>
			<?php else: foreach ($txns as $t): ?>
				<tr><td>#<?php echo (int) $t['id']; ?> <small><?php echo epc_erp_h($t['receipt_no']); ?></small></td>
				<td class="text-right"><?php echo number_format((float) $t['net'], 2); ?></td>
				<td class="text-right"><?php echo number_format((float) $t['tax'], 2); ?></td>
				<td class="text-right"><strong><?php echo number_format((float) $t['total'], 2); ?></strong></td>
				<td><span class="label label-default"><?php echo epc_erp_h($t['tender_type']); ?></span></td></tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div><div class="col-md-6">
		<div class="panel panel-info">
			<div class="panel-heading"><strong>End-of-day statement (Z-report)</strong></div>
			<table class="table table-condensed" style="margin-bottom:0;">
				<tbody>
					<tr><td>Transactions</td><td class="text-right"><?php echo (int) $stmt['count']; ?></td></tr>
					<tr><td>Gross</td><td class="text-right"><?php echo number_format((float) $stmt['gross'], 2); ?></td></tr>
					<tr><td>Discount</td><td class="text-right">−<?php echo number_format((float) $stmt['discount'], 2); ?></td></tr>
					<tr><td>Net</td><td class="text-right"><?php echo number_format((float) $stmt['net'], 2); ?></td></tr>
					<tr><td>Tax</td><td class="text-right"><?php echo number_format((float) $stmt['tax'], 2); ?></td></tr>
					<tr class="active"><td><strong>Total</strong></td><td class="text-right"><strong><?php echo number_format((float) $stmt['total'], 2); ?></strong></td></tr>
				</tbody>
			</table>
			<div class="panel-footer">
				<strong>By tender:</strong>
				<?php if (empty($stmt['by_tender'])): ?> <span class="text-muted">—</span>
				<?php else: foreach ($stmt['by_tender'] as $tt => $amt): ?>
					<span class="label label-default"><?php echo epc_erp_h($tt); ?>: <?php echo number_format((float) $amt, 2); ?></span>
				<?php endforeach; endif; ?>
			</div>
		</div>
	</div></div>
	<?php else: ?><p class="text-muted">Create a channel first to record POS sales.</p><?php endif; ?>
<?php endif; ?>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 900); }
	function bind(id, action){ var f=document.getElementById(id); if(f) f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }
	bind('epc_rtl_channel', 'rtl_channel_save');
	bind('epc_rtl_assort', 'rtl_assortment_set');
	bind('epc_rtl_discount', 'rtl_discount_save');
	bind('epc_rtl_sale', 'rtl_pos_sale');
})();
</script>
