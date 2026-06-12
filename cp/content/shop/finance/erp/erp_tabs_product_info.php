<?php
/**
 * Module: Product Information System.
 * Sub-modules: Product dev kit (drafts), All products, Release product (active).
 * Backed by the per-tenant inventory item master.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';
epc_erp_pm_inline_assets();

$view = isset($_GET['pm_view']) ? (string) $_GET['pm_view'] : 'all';
$subs = array(
	'devkit' => 'Product dev kit',
	'all' => 'All products',
	'released' => 'Release product',
);

echo '<div class="epc-erp-section"><h3 style="margin-top:0;"><i class="fa fa-cube"></i> Product Information System</h3>';
echo '<p class="text-muted">Develop products, browse the full catalogue and release products as active. Per-tenant; specialized fields (gram/carat for jewellery, barrel/MT for oil &amp; gas) come from the industry pack.</p></div>';

epc_erp_pm_module_tabs($erpUrl, 'product_info', 'operations', $date_from_str, $date_to_str, $subs, $view);

$items = array();
try {
	epc_erp_inventory_ensure_schema($db_link);
	$items = $db_link->query("SELECT i.`id`, i.`sku`, i.`name`, i.`item_type`, i.`unit`, i.`active`,
			(SELECT COALESCE(SUM(s.`qty_on_hand`),0) FROM `epc_erp_inv_stock` s WHERE s.`item_id` = i.`id`) AS qty
		FROM `epc_erp_inv_items` i ORDER BY i.`sku` LIMIT 400")->fetchAll(PDO::FETCH_ASSOC) ?: array();
} catch (Exception $e) {
}

$newItemUrl = epc_erp_tab_url($erpUrl, 'inventory', $date_from_str, $date_to_str, 'operations');

if ($view === 'devkit') {
	echo '<div class="epc-erp-section"><h4><i class="fa fa-flask"></i> Product dev kit</h4>';
	echo '<p class="text-muted">Design new products: define SKU, type, unit and specialized attributes before releasing. Items with no stock movement yet are shown as in-development.</p>';
	$draft = array_filter($items, function ($r) {
		return (float) $r['qty'] == 0.0;
	});
	echo '<a class="btn btn-primary btn-sm" href="' . epc_erp_h($newItemUrl) . '"><i class="fa fa-plus"></i> New product (inventory item)</a>';
	echo '<div class="table-responsive" style="margin-top:10px;"><table class="table table-striped table-bordered table-condensed"><thead><tr><th>SKU</th><th>Name</th><th>Type</th><th>Unit</th><th>Stage</th></tr></thead><tbody>';
	if (empty($draft)) {
		echo '<tr><td colspan="5" class="text-muted">No in-development products.</td></tr>';
	}
	foreach ($draft as $r) {
		echo '<tr><td>' . epc_erp_h((string) $r['sku']) . '</td><td>' . epc_erp_h((string) $r['name']) . '</td><td>' . epc_erp_h((string) $r['item_type']) . '</td><td>' . epc_erp_h((string) $r['unit']) . '</td><td><span class="label label-warning">In development</span></td></tr>';
	}
	echo '</tbody></table></div></div>';
} elseif ($view === 'released') {
	echo '<div class="epc-erp-section"><h4><i class="fa fa-check-circle"></i> Released products (active)</h4>';
	$rel = array_filter($items, function ($r) {
		return (int) $r['active'] === 1;
	});
	echo '<div class="table-responsive"><table class="table table-striped table-bordered table-condensed"><thead><tr><th>SKU</th><th>Name</th><th>Type</th><th>Unit</th><th>Qty on hand</th><th>Status</th></tr></thead><tbody>';
	if (empty($rel)) {
		echo '<tr><td colspan="6" class="text-muted">No released products.</td></tr>';
	}
	foreach ($rel as $r) {
		echo '<tr><td>' . epc_erp_h((string) $r['sku']) . '</td><td>' . epc_erp_h((string) $r['name']) . '</td><td>' . epc_erp_h((string) $r['item_type']) . '</td><td>' . epc_erp_h((string) $r['unit']) . '</td><td>' . epc_erp_h(number_format((float) $r['qty'], 2)) . '</td><td><span class="label label-success">Released</span></td></tr>';
	}
	echo '</tbody></table></div></div>';
} else {
	echo '<div class="epc-erp-section"><h4><i class="fa fa-cubes"></i> All products <span class="badge">' . count($items) . '</span></h4>';
	echo '<a class="btn btn-primary btn-sm" href="' . epc_erp_h($newItemUrl) . '"><i class="fa fa-plus"></i> New product</a>';
	echo '<div class="table-responsive" style="margin-top:10px;"><table class="table table-striped table-bordered table-condensed"><thead><tr><th>SKU</th><th>Name</th><th>Type</th><th>Unit</th><th>Qty on hand</th><th>Status</th></tr></thead><tbody>';
	if (empty($items)) {
		echo '<tr><td colspan="6" class="text-muted">No products yet.</td></tr>';
	}
	foreach ($items as $r) {
		$active = (int) $r['active'] === 1;
		$badge = $active ? '<span class="label label-success">Released</span>' : '<span class="label label-default">Inactive</span>';
		echo '<tr><td>' . epc_erp_h((string) $r['sku']) . '</td><td>' . epc_erp_h((string) $r['name']) . '</td><td>' . epc_erp_h((string) $r['item_type']) . '</td><td>' . epc_erp_h((string) $r['unit']) . '</td><td>' . epc_erp_h(number_format((float) $r['qty'], 2)) . '</td><td>' . $badge . '</td></tr>';
	}
	echo '</tbody></table></div></div>';
}
