<?php
/**
 * CLI integration tests for the SCM (supply chain) layer.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_scm_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

$db = new PDO("mysql:host=$host;dbname=$name;charset=utf8", $user, $pass, array(
    PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
));

$fin = dirname(__DIR__, 2) . '/content/shop/finance';
require_once $fin . '/epc_erp_helpers.php';
require_once $fin . '/epc_erp_schema.php';
require_once $fin . '/epc_erp_extended.php';
require_once $fin . '/epc_erp_inventory.php';
require_once $fin . '/epc_erp_scm.php';

$pass_count = 0;
$fail_count = 0;
function check(string $label, bool $cond): void
{
    global $pass_count, $fail_count;
    if ($cond) {
        $pass_count++;
        echo "  PASS  $label\n";
    } else {
        $fail_count++;
        echo "  FAIL  $label\n";
    }
}
function section(string $t): void
{
    echo "\n== $t ==\n";
}

epc_erp_ensure_schema($db);
epc_erp_extended_ensure_schema($db);
epc_erp_inventory_ensure_schema($db);
epc_scm_ensure_schema($db);

/* ---------------------------------------------------------------- schema */
section('Schema');
$tables = array(
    'epc_scm_rfq', 'epc_scm_rfq_lines', 'epc_scm_rfq_responses',
    'epc_scm_item_planning', 'epc_scm_landed_cost', 'epc_scm_landed_cost_lines',
    'epc_scm_carriers', 'epc_scm_shipments', 'epc_scm_shipment_lines',
);
foreach ($tables as $t) {
    $exists = $db->query("SHOW TABLES LIKE " . $db->quote($t))->fetchColumn();
    check("table $t exists", (bool) $exists);
}

/* ----------------------------------------------------- seed base inventory */
$wh = epc_erp_inventory_create_warehouse($db, array('code' => 'SCM1', 'name' => 'SCM Test WH'));
$wh = is_array($wh) ? (int) ($wh['id'] ?? 0) : (int) $wh;
if ($wh <= 0) {
    $wh = (int) $db->query("SELECT id FROM epc_erp_inv_warehouses ORDER BY id LIMIT 1")->fetchColumn();
}
$itemA = (int) epc_erp_inventory_create_item($db, array('sku' => 'SCM-A', 'name' => 'Fast Mover', 'reorder_level' => 5));
$itemB = (int) epc_erp_inventory_create_item($db, array('sku' => 'SCM-B', 'name' => 'Slow Mover', 'reorder_level' => 0));

// Opening stock + sales history for forecasting.
epc_erp_inventory_record_movement($db, array('movement_type' => 'opening', 'warehouse_id' => $wh, 'item_id' => $itemA, 'qty' => 100, 'unit_cost' => 10, 'movement_date' => date('Y-m-d', time() - 100 * 86400)));
// 60 units sold across the last 90 days => ~0.667/day
for ($d = 80; $d >= 5; $d -= 5) {
    epc_erp_inventory_record_movement($db, array('movement_type' => 'sale_out', 'warehouse_id' => $wh, 'item_id' => $itemA, 'qty' => 4, 'movement_date' => date('Y-m-d', time() - $d * 86400)));
}

/* --------------------------------------------------- demand & forecasting */
section('Demand forecasting & planning');
$stats = epc_scm_demand_stats($db, $itemA, $wh, 90);
check('demand stats returns positive avg daily', $stats['avg_daily_demand'] > 0);
check('total demand counted (64 units)', abs($stats['total_demand'] - 64) < 0.01);

$fc = epc_scm_forecast($db, $itemA, $wh, 30, 90);
check('forecast qty > 0 for 30-day horizon', $fc['forecast_qty'] > 0);

$rop = epc_scm_reorder_point(2.0, 7, 5.0);
check('reorder point = avg*lead + safety (2*7+5=19)', abs($rop - 19.0) < 0.001);

epc_scm_planning_set($db, $itemA, $wh, array('lead_time_days' => 14, 'safety_stock' => 10, 'min_order_qty' => 20, 'review_horizon_days' => 30));
$planRow = $db->query("SELECT lead_time_days FROM epc_scm_item_planning WHERE item_id=$itemA")->fetchColumn();
check('planning params persisted (lead_time=14)', (int) $planRow === 14);

// Drive item A on-hand low to trigger a suggestion.
$onHand = (float) $db->query("SELECT qty_on_hand FROM epc_erp_inv_stock WHERE item_id=$itemA AND warehouse_id=$wh")->fetchColumn();
epc_erp_inventory_record_movement($db, array('movement_type' => 'sale_out', 'warehouse_id' => $wh, 'item_id' => $itemA, 'qty' => max(1, $onHand - 3), 'movement_date' => date('Y-m-d')));
$sugg = epc_scm_planning_suggestions($db, $wh);
$found = false;
foreach ($sugg as $s) {
    if ($s['item_id'] === $itemA) {
        $found = true;
        check('suggestion respects min_order_qty (>=20)', $s['suggested_order_qty'] >= 20);
    }
}
check('low item A produced a reorder suggestion', $found);

/* ----------------------------------------------------------------- RFQ */
section('Procurement / RFQ');
$db->exec("INSERT INTO epc_erp_suppliers (`name`,`active`,`time_created`) VALUES ('Supplier One',1," . time() . ")");
$sup1 = (int) $db->lastInsertId();
$db->exec("INSERT INTO epc_erp_suppliers (`name`,`active`,`time_created`) VALUES ('Supplier Two',1," . time() . ")");
$sup2 = (int) $db->lastInsertId();

$rfqId = epc_scm_rfq_save($db, array(
    'title' => 'Brake pads + filters',
    'status' => 'sent',
    'lines' => array(
        array('item_id' => $itemA, 'description' => 'Brake pads', 'qty' => 100, 'target_price' => 9),
        array('item_id' => $itemB, 'description' => 'Oil filters', 'qty' => 50, 'target_price' => 4),
    ),
));
check('RFQ created with id', $rfqId > 0);
$lineIds = $db->query("SELECT id FROM epc_scm_rfq_lines WHERE rfq_id=$rfqId ORDER BY sort_order")->fetchAll(PDO::FETCH_COLUMN);
check('RFQ has 2 lines', count($lineIds) === 2);

// Supplier One cheaper on line 1, dearer on line 2; Supplier Two opposite.
epc_scm_rfq_add_response($db, $rfqId, array('rfq_line_id' => $lineIds[0], 'supplier_id' => $sup1, 'unit_price' => 8.0, 'lead_time_days' => 7));
epc_scm_rfq_add_response($db, $rfqId, array('rfq_line_id' => $lineIds[1], 'supplier_id' => $sup1, 'unit_price' => 4.5, 'lead_time_days' => 7));
epc_scm_rfq_add_response($db, $rfqId, array('rfq_line_id' => $lineIds[0], 'supplier_id' => $sup2, 'unit_price' => 8.5, 'lead_time_days' => 5));
epc_scm_rfq_add_response($db, $rfqId, array('rfq_line_id' => $lineIds[1], 'supplier_id' => $sup2, 'unit_price' => 3.5, 'lead_time_days' => 5));

$cmp = epc_scm_rfq_compare($db, $rfqId);
check('compare returns best supplier per line', $cmp['lines'][0]['best_supplier_id'] === $sup1 && $cmp['lines'][1]['best_supplier_id'] === $sup2);
// Sup1 total = 100*8 + 50*4.5 = 1025 ; Sup2 = 100*8.5 + 50*3.5 = 1025 -> tie; recommended is the lower/first.
check('compare produces a supplier ranking', count($cmp['supplier_ranking']) === 2);
check('recommended supplier resolved', $cmp['recommended_supplier_id'] > 0);

$award = epc_scm_rfq_award_to_po($db, $rfqId, $sup1, 0);
check('award created a PO', $award['status'] === true && $award['po_id'] > 0);
$rfqStatus = $db->query("SELECT status FROM epc_scm_rfq WHERE id=$rfqId")->fetchColumn();
check('RFQ marked awarded', $rfqStatus === 'awarded');
$poExists = $db->query("SELECT COUNT(*) FROM epc_erp_purchase_orders WHERE id=" . (int) $award['po_id'])->fetchColumn();
check('awarded PO exists in epc_erp_purchase_orders', (int) $poExists === 1);

/* --------------------------------------------------------- landed cost */
section('Landed cost');
$alloc = epc_scm_landed_cost_allocate(
    array(
        array('item_id' => $itemA, 'qty' => 100, 'base_value' => 800, 'weight' => 50),
        array('item_id' => $itemB, 'qty' => 50, 'base_value' => 200, 'weight' => 50),
    ),
    300, 0, 0, 0, 'value'
);
// By value: A gets 300*800/1000=240, B gets 60. Totals exact.
check('value-basis allocation A=240', abs($alloc[0]['allocated_cost'] - 240.0) < 0.01);
check('value-basis allocation B=60', abs($alloc[1]['allocated_cost'] - 60.0) < 0.01);
check('allocation sums to total (300)', abs(($alloc[0]['allocated_cost'] + $alloc[1]['allocated_cost']) - 300.0) < 0.01);
check('unit add-on A = 2.40 (240/100)', abs($alloc[0]['unit_landed_addon'] - 2.40) < 0.001);

$allocQty = epc_scm_landed_cost_allocate(
    array(
        array('item_id' => $itemA, 'qty' => 100, 'base_value' => 800, 'weight' => 50),
        array('item_id' => $itemB, 'qty' => 50, 'base_value' => 200, 'weight' => 50),
    ),
    150, 0, 0, 0, 'qty'
);
// By qty: A=150*100/150=100, B=50.
check('qty-basis allocation A=100', abs($allocQty[0]['allocated_cost'] - 100.0) < 0.01);

$costBefore = (float) $db->query("SELECT avg_unit_cost FROM epc_erp_inv_stock WHERE item_id=$itemA AND warehouse_id=$wh")->fetchColumn();
$lc = epc_scm_landed_cost_save($db, array(
    'reference' => 'Import shipment 1',
    'allocation_basis' => 'value',
    'freight' => 300,
    'lines' => array(
        array('item_id' => $itemA, 'qty' => 100, 'base_value' => 800),
        array('item_id' => $itemB, 'qty' => 50, 'base_value' => 200),
    ),
));
check('landed cost voucher saved', $lc['status'] === true && $lc['lc_id'] > 0);
$applied = epc_scm_landed_cost_apply($db, (int) $lc['lc_id'], $wh, 0);
check('landed cost applied to stock', $applied['status'] === true && $applied['items_updated'] >= 1);
$costAfter = (float) $db->query("SELECT avg_unit_cost FROM epc_erp_inv_stock WHERE item_id=$itemA AND warehouse_id=$wh")->fetchColumn();
check('item A avg cost rose by ~2.40', abs(($costAfter - $costBefore) - 2.40) < 0.01);
$reApply = epc_scm_landed_cost_apply($db, (int) $lc['lc_id'], $wh, 0);
check('landed cost apply is idempotent (no double-apply)', $reApply['status'] === false);

/* ----------------------------------------------------- shipping & logistics */
section('Shipping & logistics');
$carrierId = epc_scm_carrier_save($db, array('name' => 'DHL', 'code' => 'dhl', 'tracking_url' => 'https://track.dhl.com/{tracking}'));
check('carrier created', $carrierId > 0);
$carrier = $db->query("SELECT * FROM epc_scm_carriers WHERE id=$carrierId")->fetch(PDO::FETCH_ASSOC);
$url = epc_scm_carrier_tracking_url($carrier, 'ABC 123');
check('tracking url built with template + urlencode', $url === 'https://track.dhl.com/ABC%20123');

$shipId = epc_scm_shipment_save($db, array(
    'direction' => 'inbound',
    'carrier_id' => $carrierId,
    'warehouse_id' => $wh,
    'supplier_id' => $sup1,
    'tracking_no' => 'TRK999',
    'eta' => time() - 86400, // already overdue
    'status' => 'in_transit',
    'lines' => array(
        array('item_id' => $itemB, 'description' => 'Oil filters', 'qty' => 30, 'unit_cost' => 4),
    ),
));
check('shipment created', $shipId > 0);

$dash = epc_scm_logistics_dashboard($db);
check('dashboard counts in_transit shipment', ($dash['in_transit'] ?? 0) >= 1);
check('dashboard flags overdue (eta passed)', ($dash['overdue'] ?? 0) >= 1);

$bBefore = (float) ($db->query("SELECT qty_on_hand FROM epc_erp_inv_stock WHERE item_id=$itemB AND warehouse_id=$wh")->fetchColumn() ?: 0);
$shLineId = (int) $db->query("SELECT id FROM epc_scm_shipment_lines WHERE shipment_id=$shipId LIMIT 1")->fetchColumn();
$recv = epc_scm_shipment_receive($db, $shipId, array(array('line_id' => $shLineId, 'qty_received' => 30)), $wh, 0);
check('shipment received into inventory', $recv['status'] === true && $recv['lines_received'] === 1);
$bAfter = (float) $db->query("SELECT qty_on_hand FROM epc_erp_inv_stock WHERE item_id=$itemB AND warehouse_id=$wh")->fetchColumn();
check('item B stock increased by 30 after receive', abs(($bAfter - $bBefore) - 30) < 0.01);
$shipStatus = $db->query("SELECT status FROM epc_scm_shipments WHERE id=$shipId")->fetchColumn();
check('shipment marked delivered after receive', $shipStatus === 'delivered');

/* ----------------------------------------------- item master (field depth) */
section('Item master — extended field depth');
$mItem = (int) epc_erp_inventory_create_item($db, array(
    'sku' => 'MASTER-1', 'name' => 'Steel Rod 12mm', 'unit' => 'pcs',
    'search_name' => 'Rod12', 'product_type' => 'item', 'item_group' => 'Raw material',
    'item_model_group' => 'FIFO', 'costing_method' => 'fifo',
    'storage_dim_group' => 'Site-WH', 'tracking_dim_group' => 'Batch',
    'purchase_unit' => 'box', 'sales_unit' => 'pcs',
    'default_warehouse_id' => $wh, 'default_vendor_id' => 77,
    'sales_tax_group' => 'STD', 'purchase_tax_group' => 'STD',
    'buyer_group' => 'METAL', 'coverage_group' => 'MinMax', 'abc_code' => 'A',
    'net_weight' => 1.2, 'gross_weight' => 1.5, 'tare_weight' => 0.3, 'volume' => 0.02,
    'gross_depth' => 12, 'gross_width' => 2, 'gross_height' => 2,
    'standard_cost' => 4.5, 'sales_price' => 7.25, 'purchase_price' => 4.0,
    'notes' => 'Construction grade',
));
check('item master created', $mItem > 0);
$mr = $db->query("SELECT * FROM epc_erp_inv_items WHERE id=$mItem")->fetch(PDO::FETCH_ASSOC);
check('item master search_name persisted', $mr['search_name'] === 'Rod12');
check('item master product_type persisted', $mr['product_type'] === 'item');
check('item master item_group persisted', $mr['item_group'] === 'Raw material');
check('item master item_model_group persisted', $mr['item_model_group'] === 'FIFO');
check('item master costing_method persisted', $mr['costing_method'] === 'fifo');
check('item master storage_dim_group persisted', $mr['storage_dim_group'] === 'Site-WH');
check('item master tracking_dim_group persisted', $mr['tracking_dim_group'] === 'Batch');
check('item master purchase_unit persisted', $mr['purchase_unit'] === 'box');
check('item master sales_unit persisted', $mr['sales_unit'] === 'pcs');
check('item master default_warehouse_id persisted', (int) $mr['default_warehouse_id'] === $wh);
check('item master default_vendor_id persisted', (int) $mr['default_vendor_id'] === 77);
check('item master sales_tax_group persisted', $mr['sales_tax_group'] === 'STD');
check('item master abc_code persisted', $mr['abc_code'] === 'A');
check('item master net_weight persisted', abs((float) $mr['net_weight'] - 1.2) < 0.001);
check('item master gross_weight persisted', abs((float) $mr['gross_weight'] - 1.5) < 0.001);
check('item master volume persisted', abs((float) $mr['volume'] - 0.02) < 0.001);
check('item master standard_cost persisted', abs((float) $mr['standard_cost'] - 4.5) < 0.0001);
check('item master sales_price persisted', abs((float) $mr['sales_price'] - 7.25) < 0.0001);
check('item master purchase_price persisted', abs((float) $mr['purchase_price'] - 4.0) < 0.0001);
check('item master notes persisted', $mr['notes'] === 'Construction grade');
// Minimal create still works (extended fields default, not required).
$mItem2 = (int) epc_erp_inventory_create_item($db, array('sku' => 'MASTER-2', 'name' => 'Plain Item'));
$mr2 = $db->query("SELECT * FROM epc_erp_inv_items WHERE id=$mItem2")->fetch(PDO::FETCH_ASSOC);
check('minimal item create still works', $mItem2 > 0 && $mr2['product_type'] === 'item');

/* ---------------------------------------------------------------- summary */
echo "\n========================================\n";
echo "SCM TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
