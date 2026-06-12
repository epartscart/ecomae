<?php
/**
 * Module: Master Planning.
 * Sub-modules: Master planning setup, Master planning report.
 * Backed by the tested epc_scm_* planning / forecasting engine.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_scm.php';
epc_erp_pm_inline_assets();

$view = isset($_GET['pm_view']) ? (string) $_GET['pm_view'] : 'report';
$subs = array(
	'report' => 'Master planning report',
	'setup' => 'Master planning setup',
);

echo '<div class="epc-erp-section"><h3 style="margin-top:0;"><i class="fa fa-random"></i> Master Planning</h3>';
echo '<p class="text-muted">Requirements planning from live demand, lead time and safety stock — generates suggested replenishment so nothing runs out. Per-tenant.</p></div>';

epc_erp_pm_module_tabs($erpUrl, 'master_planning', 'operations', $date_from_str, $date_to_str, $subs, $view);

if ($view === 'setup') {
	echo '<div class="epc-erp-section"><h4><i class="fa fa-sliders"></i> Master planning setup</h4>';
	echo '<p class="text-muted">Planning parameters per item / warehouse (lead time, safety stock, review horizon, minimum order quantity) drive the reorder point and suggested order quantity. Defaults: lead time 7 days, review horizon 30 days.</p>';
	echo '<table class="table table-bordered table-condensed" style="max-width:620px;"><thead><tr><th>Parameter</th><th>Meaning</th><th>Default</th></tr></thead><tbody>';
	echo '<tr><td>Lead time (days)</td><td>Supplier delivery time</td><td>7</td></tr>';
	echo '<tr><td>Safety stock</td><td>Buffer below which to reorder</td><td>0</td></tr>';
	echo '<tr><td>Review horizon (days)</td><td>Coverage period planned</td><td>30</td></tr>';
	echo '<tr><td>Minimum order qty</td><td>Supplier MOQ rounding</td><td>0</td></tr>';
	echo '</tbody></table>';
	echo '<p class="text-muted">Reorder point = avg daily demand × lead time + safety stock. Suggested qty = avg daily demand × (lead time + horizon) + safety − on hand.</p>';
	echo '<a class="btn btn-default btn-sm" href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'inventory', $date_from_str, $date_to_str, 'operations')) . '"><i class="fa fa-cubes"></i> Manage items &amp; reorder levels</a>';
	echo '</div>';
} else {
	$sugg = array();
	try {
		$sugg = epc_scm_planning_suggestions($db_link, 0);
	} catch (Exception $e) {
	}
	echo '<div class="epc-erp-section"><h4><i class="fa fa-list-ol"></i> Master planning report — replenishment suggestions <span class="badge">' . count($sugg) . '</span></h4>';
	if (empty($sugg)) {
		echo '<p class="text-muted">No replenishment needed — all stocked items are above their reorder point. Add reorder levels / demand to see suggestions.</p>';
	} else {
		echo '<div class="table-responsive"><table class="table table-striped table-bordered table-condensed"><thead><tr><th>SKU</th><th>Item</th><th>On hand</th><th>Avg daily demand</th><th>Trend</th><th>Lead time</th><th>Reorder point</th><th>Suggested order</th><th></th></tr></thead><tbody>';
		foreach ($sugg as $s) {
			$trend = (string) ($s['trend'] ?? '');
			$tlbl = $trend === 'up' ? '<span class="text-danger">▲ rising</span>' : ($trend === 'down' ? '<span class="text-success">▼ falling</span>' : '<span class="text-muted">— flat</span>');
			$poUrl = epc_erp_tab_url($erpUrl, 'purchase_orders', $date_from_str, $date_to_str, 'purchasing');
			echo '<tr><td>' . epc_erp_h((string) $s['sku']) . '</td><td>' . epc_erp_h((string) $s['name']) . '</td><td>' . epc_erp_h(number_format((float) $s['qty_on_hand'], 2)) . '</td><td>' . epc_erp_h(number_format((float) $s['avg_daily_demand'], 2)) . '</td><td>' . $tlbl . '</td><td>' . (int) $s['lead_time_days'] . 'd</td><td>' . epc_erp_h(number_format((float) $s['reorder_point'], 2)) . '</td><td><strong>' . epc_erp_h(number_format((float) $s['suggested_order_qty'], 2)) . '</strong></td><td><a class="btn btn-xs btn-primary" href="' . epc_erp_h($poUrl) . '">Create PO</a></td></tr>';
		}
		echo '</tbody></table></div>';
	}
	echo '</div>';
}
