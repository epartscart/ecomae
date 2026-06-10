<?php
/**
 * CP — Marketplace channels hub (Amazon, eBay).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/channels/epc_channel_helpers.php';

if ($_SERVER['REQUEST_METHOD'] === 'POST' && !empty($_POST['action'])) {
	require __DIR__ . '/ajax_channels.php';
	exit;
}

epc_channel_ensure_schema($db_link);
$dash = epc_channel_dashboard($db_link);
$channels = epc_channel_list_marketplaces($db_link);

$orders = $db_link->query(
	'SELECT mo.*, c.`code` AS channel_code, c.`name` AS channel_name
	FROM `epc_marketplace_orders` mo
	INNER JOIN `epc_marketplace_channels` c ON c.`id` = mo.`channel_id`
	ORDER BY mo.`id` DESC LIMIT 30'
)->fetchAll(PDO::FETCH_ASSOC);

$skus = $db_link->query(
	'SELECT m.*, c.`code` AS channel_code FROM `epc_marketplace_sku_map` m
	INNER JOIN `epc_marketplace_channels` c ON c.`id` = m.`channel_id`
	ORDER BY m.`id` DESC LIMIT 30'
)->fetchAll(PDO::FETCH_ASSOC);

$logs = $db_link->query("SELECT * FROM `epc_channel_sync_log` WHERE `kind` IN ('inventory_sync','order_import','seed') OR `channel_code` IN ('amazon','ebay','system') ORDER BY `id` DESC LIMIT 12")->fetchAll(PDO::FETCH_ASSOC);

$channelsUrl = '/' . $DP_Config->backend_dir . '/shop/channels/channels';
$guideUrl = '/' . $DP_Config->backend_dir . '/shop/channels/guide';
$logisticsGuideUrl = '/' . $DP_Config->backend_dir . '/shop/logistics/guide';
$csrf = isset($user_session['csrf_guard_key']) ? (string)$user_session['csrf_guard_key'] : '';
?>
<link rel="stylesheet" href="/content/shop/finance/epc_erp_ui.css?v=20260525">
<style>
.epc-ch-kpi { display:flex; flex-wrap:wrap; gap:12px; margin:0 0 18px; }
.epc-ch-kpi .kpi { flex:1 1 140px; min-width:120px; background:#f8fafc; border:1px solid #e2e8f0; border-radius:8px; padding:12px; }
.epc-ch-kpi .lbl { font-size:11px; color:#64748b; text-transform:uppercase; }
.epc-ch-kpi .val { font-size:22px; font-weight:700; color:#1e40af; }
</style>

<div class="col-lg-12 epc-erp-shell">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<i class="fa fa-plug"></i> Channels — Amazon &amp; eBay
			<span class="pull-right">
				<a class="btn btn-primary btn-xs" href="<?php echo epc_channel_h($guideUrl); ?>"><i class="fa fa-book"></i> Channels guide</a>
				<a class="btn btn-default btn-xs" href="<?php echo epc_channel_h($logisticsGuideUrl); ?>"><i class="fa fa-truck"></i> Logistics guide</a>
			</span>
		</div>
		<div class="panel-body">

			<div class="alert alert-info">
				<strong>Marketplace only.</strong> Listing sync, SKU map, and order import for Amazon/eBay.
				Delivery, carriers, and normal customer orders are under
				<a href="<?php echo epc_channel_h($logisticsGuideUrl); ?>">Logistics</a>.
			</div>

			<div class="epc-ch-kpi">
				<div class="kpi"><div class="lbl">Marketplace orders</div><div class="val"><?php echo (int)$dash['marketplace_orders']; ?></div></div>
				<div class="kpi"><div class="lbl">Awaiting ship</div><div class="val"><?php echo (int)$dash['marketplace_pending']; ?></div></div>
				<div class="kpi"><div class="lbl">SKU mappings</div><div class="val"><?php echo (int)$dash['sku_mapped']; ?></div></div>
				<div class="kpi"><div class="lbl">Active channels</div><div class="val"><?php echo count($channels); ?></div></div>
			</div>

			<p>
				<button type="button" class="btn btn-success btn-sm" id="epc_btn_seed"><i class="fa fa-database"></i> Load sample data</button>
				<button type="button" class="btn btn-primary btn-sm" id="epc_btn_sync_amz"><i class="fa fa-refresh"></i> Demo sync Amazon stock</button>
				<button type="button" class="btn btn-primary btn-sm" id="epc_btn_sync_ebay"><i class="fa fa-refresh"></i> Demo sync eBay stock</button>
				<a class="btn btn-default btn-sm" href="/epc-channels-demo.php?token=epartscart-deploy-2026" target="_blank"><i class="fa fa-external-link"></i> JSON report</a>
			</p>
			<div id="epc_ch_msg" class="alert" style="display:none;"></div>

			<h4><i class="fa fa-amazon"></i> Marketplace channels</h4>
			<table class="table table-bordered table-condensed">
				<thead><tr><th>Channel</th><th>Marketplace</th><th>Mode</th><th>Last sync</th></tr></thead>
				<tbody>
				<?php foreach ($channels as $ch): ?>
					<tr>
						<td><strong><?php echo epc_channel_h($ch['name']); ?></strong> (<?php echo epc_channel_h($ch['code']); ?>)</td>
						<td><?php echo epc_channel_h($ch['marketplace_id']); ?></td>
						<td><?php echo (int)$ch['demo_mode'] ? 'Demo' : 'Live'; ?></td>
						<td><?php echo (int)$ch['last_sync_at'] ? epc_channel_h(date('Y-m-d H:i', (int)$ch['last_sync_at'])) : '—'; ?></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>

			<h4>SKU map (sample)</h4>
			<table class="table table-striped table-condensed">
				<thead><tr><th>Channel</th><th>Brand</th><th>Article</th><th>External SKU</th><th>ASIN</th><th>Price</th><th>Stock</th></tr></thead>
				<tbody>
				<?php foreach ($skus as $s): ?>
					<tr>
						<td><?php echo epc_channel_h($s['channel_code']); ?></td>
						<td><?php echo epc_channel_h($s['manufacturer']); ?></td>
						<td><?php echo epc_channel_h($s['article']); ?></td>
						<td><?php echo epc_channel_h($s['external_sku']); ?></td>
						<td><?php echo epc_channel_h($s['external_asin'] ?: '—'); ?></td>
						<td><?php echo epc_channel_money($s['price']); ?></td>
						<td><?php echo (int)$s['stock_qty']; ?></td>
					</tr>
				<?php endforeach; ?>
				<?php if (empty($skus)): ?><tr><td colspan="7" class="text-muted">No SKUs — click Load sample data</td></tr><?php endif; ?>
				</tbody>
			</table>

			<h4>Marketplace orders (sample)</h4>
			<table class="table table-striped table-condensed">
				<thead><tr><th>Channel</th><th>External ID</th><th>Customer</th><th>Ship to</th><th>Total</th><th>Status</th><th></th></tr></thead>
				<tbody>
				<?php foreach ($orders as $o): ?>
					<tr>
						<td><?php echo epc_channel_h($o['channel_code']); ?></td>
						<td><?php echo epc_channel_h($o['external_order_id']); ?></td>
						<td><?php echo epc_channel_h($o['customer_name']); ?></td>
						<td><?php echo epc_channel_h($o['ship_city'] . ', ' . $o['ship_country']); ?></td>
						<td><?php echo epc_channel_money($o['total_amount']); ?> <?php echo epc_channel_h($o['currency']); ?></td>
						<td><?php echo epc_channel_h($o['status']); ?></td>
						<td><button type="button" class="btn btn-xs btn-default epc-import-order" data-id="<?php echo (int)$o['id']; ?>">Demo import</button></td>
					</tr>
				<?php endforeach; ?>
				<?php if (empty($orders)): ?><tr><td colspan="7" class="text-muted">No orders — click Load sample data</td></tr><?php endif; ?>
				</tbody>
			</table>

			<h4>Sync log</h4>
			<ul class="list-unstyled">
				<?php foreach ($logs as $lg): ?>
					<li><small class="text-muted"><?php echo epc_channel_h(date('Y-m-d H:i', (int)$lg['time_created'])); ?></small> — <?php echo epc_channel_h($lg['message']); ?></li>
				<?php endforeach; ?>
				<?php if (empty($logs)): ?><li class="text-muted">No log entries yet</li><?php endif; ?>
			</ul>
		</div>
	</div>
</div>

<script>
(function(){
	var url = <?php echo json_encode($channelsUrl); ?>;
	var csrf = <?php echo json_encode($csrf); ?>;
	function msg(ok, text) {
		var el = document.getElementById('epc_ch_msg');
		el.className = 'alert alert-' + (ok ? 'success' : 'danger');
		el.textContent = text;
		el.style.display = 'block';
	}
	function post(action, extra) {
		var fd = new FormData();
		fd.append('action', action);
		fd.append('csrf_guard_key', csrf);
		if (extra) { for (var k in extra) fd.append(k, extra[k]); }
		return fetch(url, { method: 'POST', body: fd, credentials: 'same-origin' }).then(function(r){ return r.json(); });
	}
	document.getElementById('epc_btn_seed').addEventListener('click', function(){
		post('seed_sample').then(function(j){ msg(!!j.status, j.message || ''); if (j.status) setTimeout(function(){ location.reload(); }, 800); });
	});
	document.getElementById('epc_btn_sync_amz').addEventListener('click', function(){
		post('sync_inventory', { channel: 'amazon' }).then(function(j){ msg(!!j.status, j.message || ''); if (j.status) setTimeout(function(){ location.reload(); }, 800); });
	});
	document.getElementById('epc_btn_sync_ebay').addEventListener('click', function(){
		post('sync_inventory', { channel: 'ebay' }).then(function(j){ msg(!!j.status, j.message || ''); if (j.status) setTimeout(function(){ location.reload(); }, 800); });
	});
	document.querySelectorAll('.epc-import-order').forEach(function(btn){
		btn.addEventListener('click', function(){
			post('import_order', { marketplace_order_id: btn.getAttribute('data-id') }).then(function(j){ msg(!!j.status, j.message || ''); if (j.status) setTimeout(function(){ location.reload(); }, 800); });
		});
	});
})();
</script>
