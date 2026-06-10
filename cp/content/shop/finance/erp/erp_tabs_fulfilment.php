<?php
/**
 * ERP — Fulfilment tab (pipeline, stock movement, returns).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

if (!epc_erp_has_commerce_integration()) {
	erp_page_header(
		'<i class="fa fa-random"></i> Fulfilment',
		'Storefront order fulfilment is not available for ERP-only tenants. Create sales orders and purchase orders directly in ERP.',
		array(
			array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
			array('label' => 'Fulfilment'),
		),
		array(
			array('label' => 'Sales orders', 'url' => epc_erp_tab_url($erpUrl, 'sales_orders', $date_from_str, $date_to_str, 'sales'), 'class' => 'btn-primary', 'icon' => 'fa-shopping-cart'),
			array('label' => 'Purchase orders', 'url' => epc_erp_tab_url($erpUrl, 'purchase_orders', $date_from_str, $date_to_str, 'purchasing'), 'class' => 'btn-default', 'icon' => 'fa-clipboard'),
		)
	);
	return;
}

@set_time_limit(90);

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_fulfilment.php';

$ff = epc_erp_fulfilment_dashboard($db_link, $date_from, $date_to, 40);
$movements = epc_erp_fulfilment_stock_movements($db_link, $date_from, $date_to, 60);
$returns = epc_erp_fulfilment_returns($db_link, 30);
$pipe_labels = epc_erp_fulfilment_pipeline_labels();
$view_order = isset($_GET['order_id']) ? (int)$_GET['order_id'] : 0;
$order_detail = ($view_order > 0) ? epc_erp_fulfilment_order_metrics($db_link, $view_order) : null;
$funnel_json = json_encode($ff['funnel']);
$pay_mix_json = json_encode(array(
	'labels' => array('Advance', 'Credit terms', 'Fully paid', 'Awaiting'),
	'values' => array(
		(int)$ff['pipeline']['customer_advance'],
		(int)$ff['pipeline']['customer_credit'],
		(int)$ff['pipeline']['customer_paid'],
		(int)$ff['pipeline']['customer_pending'],
	),
));
?>

<div class="epc-erp-shell">
	<div class="epc-erp-hero">
		<h3><i class="fa fa-random"></i> Order fulfilment pipeline</h3>
		<p>Track the full cycle: customer payment (advance or credit) → supplier payment → goods into stock → delivery to customer → returns back to supplier. Financial recognition stays on the <strong>Completed</strong> tab once the order is finished in CP.</p>
		<?php if ((int)$ff['total_orders'] > count($ff['orders'])): ?>
			<p class="text-muted"><small>Showing latest <?php echo count($ff['orders']); ?> of <?php echo (int)$ff['total_orders']; ?> orders in this date range.</small></p>
		<?php endif; ?>
	</div>

	<div class="epc-erp-pipeline">
		<?php foreach ($pipe_labels as $step => $pl): ?>
			<?php $cnt = isset($ff['funnel']['values'][$step - 1]) ? (int)$ff['funnel']['values'][$step - 1] : 0; ?>
			<div class="step <?php echo $cnt > 0 ? 'done' : ''; ?>">
				<span class="ico"><i class="fa <?php echo epc_erp_h($pl['icon']); ?>"></i></span>
				<span class="ttl"><?php echo epc_erp_h($pl['label']); ?></span>
				<span class="cnt"><?php echo $cnt; ?></span>
			</div>
		<?php endforeach; ?>
	</div>

	<div class="epc-erp-flow-diagram">
		<svg viewBox="0 0 880 120" xmlns="http://www.w3.org/2000/svg" aria-label="Fulfilment flow">
			<defs>
				<marker id="epc-arr" markerWidth="8" markerHeight="8" refX="6" refY="3" orient="auto"><path d="M0,0 L6,3 L0,6 Z" fill="#64748b"/></marker>
			</defs>
			<rect x="10" y="40" width="100" height="44" rx="8" fill="#eff6ff" stroke="#2563eb"/>
			<text x="60" y="67" text-anchor="middle" font-size="11" fill="#1e40af">Customer pay</text>
			<line x1="110" y1="62" x2="140" y2="62" stroke="#64748b" marker-end="url(#epc-arr)"/>
			<rect x="140" y="40" width="100" height="44" rx="8" fill="#f5f3ff" stroke="#7c3aed"/>
			<text x="190" y="67" text-anchor="middle" font-size="11" fill="#5b21b6">Supplier pay</text>
			<line x1="240" y1="62" x2="270" y2="62" stroke="#64748b" marker-end="url(#epc-arr)"/>
			<rect x="270" y="40" width="110" height="44" rx="8" fill="#ecfeff" stroke="#0891b2"/>
			<text x="325" y="67" text-anchor="middle" font-size="11" fill="#0e7490">Goods in stock</text>
			<line x1="380" y1="62" x2="410" y2="62" stroke="#64748b" marker-end="url(#epc-arr)"/>
			<rect x="410" y="40" width="110" height="44" rx="8" fill="#f0fdf4" stroke="#16a34a"/>
			<text x="465" y="67" text-anchor="middle" font-size="11" fill="#166534">Deliver customer</text>
			<line x1="520" y1="62" x2="550" y2="62" stroke="#64748b" marker-end="url(#epc-arr)"/>
			<rect x="550" y="40" width="90" height="44" rx="8" fill="#fef2f2" stroke="#dc2626"/>
			<text x="595" y="67" text-anchor="middle" font-size="11" fill="#991b1b">Return</text>
			<line x1="640" y1="62" x2="670" y2="62" stroke="#64748b" marker-end="url(#epc-arr)"/>
			<rect x="670" y="40" width="100" height="44" rx="8" fill="#fff7ed" stroke="#d97706"/>
			<text x="720" y="67" text-anchor="middle" font-size="11" fill="#92400e">Supplier return</text>
			<text x="440" y="18" text-anchor="middle" font-size="12" fill="#475569" font-weight="bold">Advance / credit at each stage — revenue &amp; AP when order Complete</text>
		</svg>
	</div>

	<div class="epc-erp-chart-row">
		<div class="epc-erp-chart-card" style="flex:2 1 360px;">
			<h5><i class="fa fa-filter"></i> Orders reaching each stage (period)</h5>
			<canvas id="epc_erp_chart_funnel" height="200"></canvas>
		</div>
		<div class="epc-erp-chart-card">
			<h5><i class="fa fa-pie-chart"></i> Customer payment mix</h5>
			<canvas id="epc_erp_chart_pay" height="200"></canvas>
		</div>
		<div class="epc-erp-chart-card">
			<h5><i class="fa fa-bar-chart"></i> Stock &amp; delivery</h5>
			<canvas id="epc_erp_chart_stock" height="200"></canvas>
		</div>
	</div>

	<div class="epc-erp-kpi">
		<div class="kpi"><div class="lbl">Orders in period</div><div class="val blue"><?php echo (int)$ff['total_orders']; ?></div></div>
		<div class="kpi"><div class="lbl">Customer advance</div><div class="val"><?php echo (int)$ff['pipeline']['customer_advance']; ?></div></div>
		<div class="kpi"><div class="lbl">Customer on credit</div><div class="val"><?php echo (int)$ff['pipeline']['customer_credit']; ?></div></div>
		<div class="kpi"><div class="lbl">Supplier on credit</div><div class="val"><?php echo (int)$ff['pipeline']['supplier_credit']; ?></div></div>
		<div class="kpi"><div class="lbl">In stock / ready</div><div class="val green"><?php echo (int)$ff['pipeline']['stock_ready']; ?></div></div>
		<div class="kpi"><div class="lbl">Delivered</div><div class="val green"><?php echo (int)$ff['pipeline']['delivery_done']; ?></div></div>
		<div class="kpi"><div class="lbl">Returns open</div><div class="val red"><?php echo (int)$ff['pipeline']['returns_open']; ?></div></div>
	</div>

	<div class="epc-erp-section">
		<h4><i class="fa fa-list"></i> Orders — fulfilment status</h4>
		<table class="table table-striped table-bordered table-condensed epc-erp-table-compact">
			<thead><tr>
				<th>Order</th><th>Date</th><th>Customer</th><th>Customer $</th><th>Supplier $</th><th>Stock</th><th>Delivery</th><th>Return</th><th></th>
			</tr></thead>
			<tbody>
			<?php foreach ($ff['orders'] as $o): ?>
				<?php
				list($c_lbl, $c_cls) = epc_erp_fulfilment_pay_badge('customer', $o['customer_pay']);
				list($s_lbl, $s_cls) = epc_erp_fulfilment_pay_badge('supplier', $o['supplier_pay']);
				list($st_lbl, $st_cls) = epc_erp_fulfilment_pay_badge('stock', $o['stock_state']);
				list($d_lbl, $d_cls) = epc_erp_fulfilment_pay_badge('delivery', $o['delivery']);
				?>
				<tr>
					<td><?php if (!empty($ordersUrl)): ?><a href="<?php echo epc_erp_h($ordersUrl . '?order_id=' . (int)$o['id']); ?>">#<?php echo (int)$o['id']; ?></a><?php else: ?>#<?php echo (int)$o['id']; ?><?php endif; ?></td>
					<td><?php echo epc_erp_h(date('Y-m-d', (int)$o['time'])); ?></td>
					<td><?php echo epc_erp_h($o['customer_email'] ?: ('User ' . (int)$o['user_id'])); ?></td>
					<td class="epc-erp-badge-stack"><span class="label label-<?php echo epc_erp_h($c_cls); ?>"><?php echo epc_erp_h($c_lbl); ?></span></td>
					<td class="epc-erp-badge-stack"><span class="label label-<?php echo epc_erp_h($s_cls); ?>"><?php echo epc_erp_h($s_lbl); ?></span></td>
					<td class="epc-erp-badge-stack"><span class="label label-<?php echo epc_erp_h($st_cls); ?>"><?php echo epc_erp_h($st_lbl); ?></span></td>
					<td class="epc-erp-badge-stack"><span class="label label-<?php echo epc_erp_h($d_cls); ?>"><?php echo epc_erp_h($d_lbl); ?></span></td>
					<td><?php echo ($o['return_state'] === 'open') ? '<span class="label label-danger">Return</span>' : '—'; ?></td>
					<td><a class="btn btn-xs btn-default" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'fulfilment', $date_from_str, $date_to_str) . '&order_id=' . (int)$o['id']); ?>">Detail</a></td>
				</tr>
			<?php endforeach; ?>
			</tbody>
		</table>
	</div>

	<?php if ($order_detail): ?>
		<div class="epc-erp-section">
			<h4>Order #<?php echo (int)$view_order; ?> — pipeline detail</h4>
			<div class="epc-erp-kpi">
				<div class="kpi"><div class="lbl">Customer paid</div><div class="val"><?php echo epc_erp_money($order_detail['paid_amount']); ?></div></div>
				<div class="kpi"><div class="lbl">Purchase recorded</div><div class="val"><?php echo epc_erp_money($order_detail['purchase_total']); ?></div></div>
				<div class="kpi"><div class="lbl">Supplier paid</div><div class="val"><?php echo epc_erp_money($order_detail['supplier_paid']); ?></div></div>
				<div class="kpi"><div class="lbl">Qty issued</div><div class="val"><?php echo epc_erp_h($order_detail['qty_issued'] . ' / ' . $order_detail['qty_total']); ?></div></div>
				<div class="kpi"><div class="lbl">Lines delivered</div><div class="val"><?php echo (int)$order_detail['lines_delivered']; ?> / <?php echo (int)$order_detail['lines_total']; ?></div></div>
			</div>
			<p class="text-muted">Status: <?php echo epc_erp_h($order_detail['order_status_name']); ?>. Revenue/AP in finance tabs when order is <strong>Completed</strong>.</p>
		</div>
	<?php endif; ?>

	<div class="epc-erp-section">
		<h4><i class="fa fa-exchange"></i> Stock &amp; goods movement</h4>
		<p class="text-muted">Reserved → issued from warehouse when goods arrive; delivered when line status is finished; returns restore stock.</p>
		<table class="table table-striped table-bordered table-condensed epc-erp-table-compact">
			<thead><tr><th>Order</th><th>Part</th><th>Warehouse</th><th>Reserved</th><th>Issued</th><th>Movement</th><th>Line status</th></tr></thead>
			<tbody>
			<?php foreach ($movements as $m): ?>
				<tr>
					<td>#<?php echo (int)$m['order_id']; ?></td>
					<td><?php echo epc_erp_h($m['manufacturer'] . ' ' . $m['article']); ?></td>
					<td><?php echo epc_erp_h($m['storage_name'] ?: ('#' . (int)$m['storage_id'])); ?></td>
					<td><?php echo (int)$m['count_reserved']; ?></td>
					<td><?php echo (int)$m['count_issued']; ?></td>
					<td class="movement-<?php echo epc_erp_h($m['movement_kind']); ?>"><?php echo epc_erp_h($m['movement']); ?></td>
					<td><?php echo epc_erp_h($m['status_name']); ?></td>
				</tr>
			<?php endforeach; ?>
			</tbody>
		</table>
	</div>

	<div class="epc-erp-section">
		<h4><i class="fa fa-reply"></i> Customer returns → supplier return</h4>
		<table class="table table-striped table-bordered table-condensed epc-erp-table-compact">
			<thead><tr><th>Return</th><th>Date</th><th>Customer</th><th>Orders</th><th>Items</th><th>Amount</th><th>Status</th></tr></thead>
			<tbody>
			<?php if (empty($returns)): ?>
				<tr><td colspan="7" class="text-muted">No returns in system.</td></tr>
			<?php else: ?>
				<?php foreach ($returns as $r): ?>
					<tr>
						<td>#<?php echo (int)$r['return_id']; ?></td>
						<td><?php echo epc_erp_h(date('Y-m-d', (int)$r['time'])); ?></td>
						<td><?php echo epc_erp_h($r['customer_email']); ?></td>
						<td><?php echo epc_erp_h($r['order_ids']); ?></td>
						<td><?php echo (int)$r['items_count']; ?></td>
						<td><?php echo epc_erp_money($r['sum']); ?></td>
						<td><?php echo epc_erp_h($r['status_name']); ?></td>
					</tr>
				<?php endforeach; ?>
			<?php endif; ?>
			</tbody>
		</table>
	</div>
</div>

<script defer src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js" crossorigin="anonymous"></script>
<script>
(function(){
	var funnel = <?php echo $funnel_json; ?>;
	var payMix = <?php echo $pay_mix_json; ?>;
	var stockData = {
		labels: ['Awaiting', 'Partial', 'In stock', 'Delivered', 'Returns'],
		values: [
			<?php echo (int)$ff['pipeline']['stock_awaiting']; ?>,
			<?php echo (int)$ff['pipeline']['stock_partial']; ?>,
			<?php echo (int)$ff['pipeline']['stock_ready']; ?>,
			<?php echo (int)$ff['pipeline']['delivery_done']; ?>,
			<?php echo (int)$ff['pipeline']['returns_open']; ?>
		]
	};
	function initEpcErpCharts() {
		if (typeof Chart === 'undefined') {
			return;
		}
		var funnelEl = document.getElementById('epc_erp_chart_funnel');
		if (funnelEl) {
			new Chart(funnelEl, {
				type: 'bar',
				data: {
					labels: funnel.labels,
					datasets: [{ label: 'Orders', data: funnel.values, backgroundColor: funnel.colors || '#2563eb', borderRadius: 6 }]
				},
				options: { responsive: true, plugins: { legend: { display: false } }, scales: { y: { beginAtZero: true, ticks: { precision: 0 } } } }
			});
		}
		var payEl = document.getElementById('epc_erp_chart_pay');
		if (payEl) {
			new Chart(payEl, {
				type: 'doughnut',
				data: {
					labels: payMix.labels,
					datasets: [{ data: payMix.values, backgroundColor: ['#2563eb','#d97706','#16a34a','#94a3b8'] }]
				},
				options: { responsive: true, plugins: { legend: { position: 'bottom' } } }
			});
		}
		var stockEl = document.getElementById('epc_erp_chart_stock');
		if (stockEl) {
			new Chart(stockEl, {
				type: 'polarArea',
				data: {
					labels: stockData.labels,
					datasets: [{ data: stockData.values, backgroundColor: ['#94a3b8','#0891b2','#16a34a','#1e40af','#dc2626'] }]
				},
				options: { responsive: true, plugins: { legend: { position: 'bottom' } } }
			});
		}
	}
	function bootCharts() {
		if (typeof Chart !== 'undefined') {
			initEpcErpCharts();
			return;
		}
		var tries = 0;
		var timer = setInterval(function(){
			tries++;
			if (typeof Chart !== 'undefined') {
				clearInterval(timer);
				initEpcErpCharts();
			} else if (tries > 40) {
				clearInterval(timer);
			}
		}, 100);
	}
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', bootCharts);
	} else {
		bootCharts();
	}
})();
</script>
