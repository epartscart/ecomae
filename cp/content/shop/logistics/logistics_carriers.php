<?php
/**
 * CP — International carriers & shipments (logistics).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/logistics/epc_logistics_helpers.php';

if (!isset($db_link) || !($db_link instanceof PDO)) {
	try {
		$db_link = new PDO(
			'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
			$DP_Config->user,
			$DP_Config->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$db_link->query('SET NAMES utf8;');
	} catch (Exception $e) {
		echo '<div class="alert alert-danger">Database connection failed.</div>';
		return;
	}
}

if ($_SERVER['REQUEST_METHOD'] === 'POST' && !empty($_POST['action'])) {
	require __DIR__ . '/ajax_logistics.php';
	exit;
}

$dash = epc_logistics_dashboard($db_link);
$carriers = epc_channel_list_carriers($db_link);
$catalog = epc_channel_carriers_catalog();
$shipments = $db_link->query(
	'SELECT s.* FROM `epc_carrier_shipments` s ORDER BY s.`id` DESC LIMIT 30'
)->fetchAll(PDO::FETCH_ASSOC);
$logs = $db_link->query("SELECT * FROM `epc_channel_sync_log` WHERE `kind` IN ('shipment','seed') OR `channel_code` IN ('dhl','fedex','aramex','ups','logistics') ORDER BY `id` DESC LIMIT 12")->fetchAll(PDO::FETCH_ASSOC);

extract(epc_logistics_configure_urls());
$csrf = isset($user_session['csrf_guard_key']) ? (string)$user_session['csrf_guard_key'] : '';
?>
<link rel="stylesheet" href="/content/shop/finance/epc_erp_ui.css?v=20260525">
<style>
.epc-log-kpi { display:flex; flex-wrap:wrap; gap:12px; margin:0 0 18px; }
.epc-log-kpi .kpi { flex:1 1 140px; min-width:120px; background:#f8fafc; border:1px solid #e2e8f0; border-radius:8px; padding:12px; }
.epc-log-kpi .lbl { font-size:11px; color:#64748b; text-transform:uppercase; }
.epc-log-kpi .val { font-size:22px; font-weight:700; color:#0f766e; }
</style>

<div class="col-lg-12 epc-erp-shell">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<i class="fa fa-truck"></i> Logistics — carriers &amp; shipments
			<span class="pull-right">
				<a class="btn btn-primary btn-xs" href="<?php echo epc_logistics_h($guideUrl); ?>"><i class="fa fa-book"></i> Logistics guide</a>
				<a class="btn btn-default btn-xs" href="<?php echo epc_logistics_h($logisticsUrl); ?>"><i class="fa fa-th-large"></i> Logistics hub</a>
				<a class="btn btn-default btn-xs" href="<?php echo epc_logistics_h($obtainModesUrl); ?>"><i class="fa fa-list"></i> Delivery methods</a>
				<a class="btn btn-default btn-xs" href="<?php echo epc_logistics_h($ordersUrl); ?>"><i class="fa fa-shopping-cart"></i> Orders</a>
			</span>
		</div>
		<div class="panel-body">

			<div class="alert alert-info">
				<strong>For all customer orders</strong> — storefront checkout and CP order fulfilment.
				Use DHL, FedEx, Aramex, UPS at checkout; create labels from any paid order with carrier delivery.
				<a href="<?php echo epc_logistics_h($guideUrl); ?>">Open logistics guide</a>.
			</div>

			<div class="epc-log-kpi">
				<div class="kpi"><div class="lbl">Carrier accounts</div><div class="val"><?php echo (int)$dash['carriers']; ?></div></div>
				<div class="kpi"><div class="lbl">Shipments</div><div class="val"><?php echo (int)$dash['shipments_shipped']; ?>/<?php echo (int)$dash['shipments']; ?></div></div>
				<div class="kpi"><div class="lbl">Pending labels</div><div class="val"><?php echo (int)$dash['shipments_pending']; ?></div></div>
				<div class="kpi"><div class="lbl">Shop orders</div><div class="val"><?php echo (int)$dash['shop_orders']; ?></div></div>
			</div>

			<p>
				<button type="button" class="btn btn-success btn-sm" id="epc_btn_log_seed"><i class="fa fa-database"></i> Load sample shipment</button>
				<a class="btn btn-default btn-sm" href="<?php echo epc_logistics_h($demoJsonUrl); ?>" target="_blank"><i class="fa fa-external-link"></i> JSON report</a>
			</p>
			<div id="epc_log_msg" class="alert" style="display:none;"></div>

			<h4><i class="fa fa-globe"></i> Carrier accounts</h4>
			<table class="table table-bordered table-condensed">
				<thead><tr><th>Carrier</th><th>Code</th><th>Mode</th></tr></thead>
				<tbody>
				<?php foreach ($carriers as $ca): ?>
					<tr>
						<td><strong><?php echo epc_logistics_h($ca['name']); ?></strong></td>
						<td><code><?php echo epc_logistics_h($ca['code']); ?></code></td>
						<td><?php echo (int)$ca['demo_mode'] ? 'Demo rates &amp; labels' : 'Live API'; ?></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>

			<h4>Recent shipments</h4>
			<p class="text-muted">From storefront orders and CP order card — marketplace channel orders use the same workflow after import.</p>
			<table class="table table-striped table-condensed">
				<thead><tr><th>Order</th><th>Carrier</th><th>Tracking</th><th>Cost</th><th>Status</th></tr></thead>
				<tbody>
				<?php foreach ($shipments as $sh): ?>
					<tr>
						<td><a href="<?php echo epc_logistics_h($ordersUrl); ?>?order_id=<?php echo (int)$sh['order_id']; ?>">#<?php echo (int)$sh['order_id']; ?></a></td>
						<td><?php echo epc_logistics_h(strtoupper($sh['carrier_code'])); ?></td>
						<td><?php if ($sh['label_url']): ?><a href="<?php echo epc_logistics_h($sh['label_url']); ?>" target="_blank"><?php echo epc_logistics_h($sh['tracking_number']); ?></a><?php else: ?><?php echo epc_logistics_h($sh['tracking_number']); ?><?php endif; ?></td>
						<td><?php echo epc_logistics_money($sh['cost']); ?> <?php echo epc_logistics_h($sh['currency']); ?></td>
						<td><?php echo epc_logistics_h($sh['status']); ?></td>
					</tr>
				<?php endforeach; ?>
				<?php if (empty($shipments)): ?><tr><td colspan="5" class="text-muted">No shipments — create from an order card or load sample data</td></tr><?php endif; ?>
				</tbody>
			</table>

			<h4>Carrier services (reference)</h4>
			<table class="table table-condensed table-bordered">
				<thead><tr><th>Code</th><th>Name</th><th>Services</th></tr></thead>
				<tbody>
				<?php foreach ($catalog as $code => $c): ?>
					<tr>
						<td><code><?php echo epc_logistics_h($code); ?></code></td>
						<td><?php echo epc_logistics_h($c['name']); ?></td>
						<td><?php echo epc_logistics_h(implode(', ', array_values($c['services']))); ?></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>

			<h4>Activity log</h4>
			<ul class="list-unstyled">
				<?php foreach ($logs as $lg): ?>
					<li><small class="text-muted"><?php echo epc_logistics_h(date('Y-m-d H:i', (int)$lg['time_created'])); ?></small> — <?php echo epc_logistics_h($lg['message']); ?></li>
				<?php endforeach; ?>
				<?php if (empty($logs)): ?><li class="text-muted">No log entries yet</li><?php endif; ?>
			</ul>
		</div>
	</div>
</div>

<script>
(function(){
	var url = <?php echo json_encode($carriersUrl); ?>;
	var csrf = <?php echo json_encode($csrf); ?>;
	function msg(ok, text) {
		var el = document.getElementById('epc_log_msg');
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
	document.getElementById('epc_btn_log_seed').addEventListener('click', function(){
		post('seed_sample').then(function(j){ msg(!!j.status, j.message || ''); if (j.status) setTimeout(function(){ location.reload(); }, 800); });
	});
})();
</script>
