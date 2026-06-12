<?php
/**
 * Module: Inventory (structural).
 * Sub-modules: Stock in hand, Inventory group, Inventory status.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';
epc_erp_pm_inline_assets();

$view = isset($_GET['pm_view']) ? (string) $_GET['pm_view'] : 'stock';
$subs = array(
	'stock' => 'Stock in hand',
	'groups' => 'Inventory groups',
	'status' => 'Inventory status',
);

echo '<div class="epc-erp-section"><h3 style="margin-top:0;"><i class="fa fa-cubes"></i> Inventory</h3>';
echo '<p class="text-muted">Stock-in-hand valuation, configurable inventory groups (valuation model per group) and live inventory status. Per-tenant.</p></div>';

epc_erp_pm_module_tabs($erpUrl, 'inv_groups', 'operations', $date_from_str, $date_to_str, $subs, $view);

switch ($view) {
	case 'groups':
		epc_erp_pm_section($db_link, $csrf, 'epc_erp_pm_inv_groups', 'Inventory groups',
			array(
				array('name' => 'code', 'label' => 'Code', 'required' => true, 'placeholder' => 'FG'),
				array('name' => 'name', 'label' => 'Name', 'required' => true),
				array('name' => 'valuation', 'label' => 'Valuation', 'type' => 'select', 'options' => array('weighted_avg' => 'Weighted average', 'fifo' => 'FIFO', 'standard' => 'Standard cost')),
				array('name' => 'note', 'label' => 'Note'),
			),
			array(array('key' => 'code', 'label' => 'Code'), array('key' => 'name', 'label' => 'Name'), array('key' => 'valuation', 'label' => 'Valuation')),
			'fa-object-group');
		break;
	case 'status':
		$rows = array();
		try {
			epc_erp_inventory_ensure_schema($db_link);
			$rows = $db_link->query("SELECT i.`sku`, i.`name`, i.`reorder_level`,
					(SELECT COALESCE(SUM(s.`qty_on_hand`),0) FROM `epc_erp_inv_stock` s WHERE s.`item_id` = i.`id`) AS qty
				FROM `epc_erp_inv_items` i WHERE i.`active` = 1 ORDER BY i.`sku` LIMIT 300")->fetchAll(PDO::FETCH_ASSOC) ?: array();
		} catch (Exception $e) {
		}
		echo '<div class="epc-erp-section"><h4><i class="fa fa-signal"></i> Inventory status</h4>';
		if (empty($rows)) {
			echo '<p class="text-muted">No items found.</p>';
		} else {
			echo '<div class="table-responsive"><table class="table table-striped table-bordered table-condensed"><thead><tr><th>SKU</th><th>Item</th><th>Qty on hand</th><th>Reorder level</th><th>Status</th></tr></thead><tbody>';
			foreach ($rows as $r) {
				$qty = (float) $r['qty'];
				$ro = (float) $r['reorder_level'];
				if ($qty <= 0) {
					$st = '<span class="label label-danger">Out of stock</span>';
				} elseif ($ro > 0 && $qty <= $ro) {
					$st = '<span class="label label-warning">Reorder</span>';
				} else {
					$st = '<span class="label label-success">In stock</span>';
				}
				echo '<tr><td>' . epc_erp_h((string) $r['sku']) . '</td><td>' . epc_erp_h((string) $r['name']) . '</td><td>' . epc_erp_h(number_format($qty, 2)) . '</td><td>' . epc_erp_h($ro > 0 ? number_format($ro, 2) : '—') . '</td><td>' . $st . '</td></tr>';
			}
			echo '</tbody></table></div>';
		}
		echo '</div>';
		break;
	case 'stock':
	default:
		$rows = array();
		$totVal = 0.0;
		try {
			epc_erp_inventory_ensure_schema($db_link);
			$rows = $db_link->query("SELECT i.`sku`, i.`name`, i.`unit`,
					(SELECT COALESCE(SUM(s.`qty_on_hand`),0) FROM `epc_erp_inv_stock` s WHERE s.`item_id` = i.`id`) AS qty,
					(SELECT COALESCE(AVG(s.`avg_unit_cost`),0) FROM `epc_erp_inv_stock` s WHERE s.`item_id` = i.`id`) AS cost
				FROM `epc_erp_inv_items` i WHERE i.`active` = 1 ORDER BY i.`sku` LIMIT 300")->fetchAll(PDO::FETCH_ASSOC) ?: array();
		} catch (Exception $e) {
		}
		echo '<div class="epc-erp-section"><h4><i class="fa fa-cubes"></i> Stock in hand</h4>';
		if (empty($rows)) {
			echo '<p class="text-muted">No items found.</p>';
		} else {
			echo '<div class="table-responsive"><table class="table table-striped table-bordered table-condensed"><thead><tr><th>SKU</th><th>Item</th><th>Unit</th><th>Qty on hand</th><th>Avg cost</th><th>Stock value</th></tr></thead><tbody>';
			foreach ($rows as $r) {
				$val = (float) $r['qty'] * (float) $r['cost'];
				$totVal += $val;
				echo '<tr><td>' . epc_erp_h((string) $r['sku']) . '</td><td>' . epc_erp_h((string) $r['name']) . '</td><td>' . epc_erp_h((string) $r['unit']) . '</td><td>' . epc_erp_h(number_format((float) $r['qty'], 2)) . '</td><td>' . epc_erp_money((float) $r['cost']) . '</td><td>' . epc_erp_money($val) . '</td></tr>';
			}
			echo '<tr><th colspan="5" style="text-align:right;">Total stock value</th><th>' . epc_erp_money($totVal) . '</th></tr>';
			echo '</tbody></table></div>';
		}
		echo '</div>';
		break;
}
