<?php
/**
 * Seed realistic ERP operational data on the live ecomae tenant so module
 * screens show real records (not empty "dust"):
 *   - Inventory items + stock (opening / purchase movements across warehouses)
 *   - Sales orders (draft / confirmed / one converted to a tax invoice)
 *
 * Token-gated, ecomae-only (host/portal-resolved DB). Idempotent: keyed on the
 * SEED-prefixed SKUs / SO titles, so re-running won't duplicate.
 *
 * GET: token=epartscart-deploy-2026   [optional &reset=1 to wipe seeded rows]
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
@require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
@require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_helpers.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_inventory.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_vouchers.php';

header('Content-Type: text/plain; charset=utf-8');

$cfg = new DP_Config();
if (function_exists('epc_portal_apply_config')) {
    epc_portal_apply_config($cfg);
}
$db = new PDO(
    'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
    $cfg->user,
    $cfg->password,
    array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);

echo "ecomae ERP seed — DB: {$cfg->db} @ {$cfg->host}\n";
echo str_repeat('=', 56) . "\n";

// Warm schemas OUTSIDE any transaction (DDL implicitly commits in MySQL).
epc_erp_full_ensure_schema($db);
epc_erp_inventory_ensure_schema($db);
epc_erp_vouchers_ensure_schema($db);
epc_erp_inventory_sync_warehouses($db);

$reset = !empty($_GET['reset']);
$SEED = 'SEED-AE';

if ($reset) {
    $likeItem = $SEED . '-%';
    $ids = $db->prepare('SELECT `id` FROM `epc_erp_inv_items` WHERE `sku` LIKE ?');
    $ids->execute(array($likeItem));
    $itemIds = $ids->fetchAll(PDO::FETCH_COLUMN);
    if ($itemIds) {
        $in = implode(',', array_map('intval', $itemIds));
        $db->exec('DELETE FROM `epc_erp_inv_movements` WHERE `item_id` IN (' . $in . ')');
        $db->exec('DELETE FROM `epc_erp_inv_stock` WHERE `item_id` IN (' . $in . ')');
        $db->exec('DELETE FROM `epc_erp_inv_item_fields` WHERE `item_id` IN (' . $in . ')');
        $db->exec('DELETE FROM `epc_erp_inv_items` WHERE `id` IN (' . $in . ')');
    }
    $so = $db->prepare('SELECT `id` FROM `epc_erp_sales_orders` WHERE `title` LIKE ?');
    $so->execute(array('%[' . $SEED . ']%'));
    $soIds = $so->fetchAll(PDO::FETCH_COLUMN);
    if ($soIds) {
        $in = implode(',', array_map('intval', $soIds));
        $db->exec('DELETE FROM `epc_erp_sales_order_lines` WHERE `sales_order_id` IN (' . $in . ')');
        $db->exec('DELETE FROM `epc_erp_sales_orders` WHERE `id` IN (' . $in . ')');
    }
    echo "RESET: removed seeded items (" . count($itemIds) . ") and sales orders (" . count($soIds) . ")\n";
}

// ---- Warehouses ------------------------------------------------------------
$warehouses = epc_erp_inventory_list_warehouses($db);
if (empty($warehouses)) {
    epc_erp_inventory_create_warehouse($db, array('code' => 'WH-DXB', 'name' => 'Dubai main store'));
    $warehouses = epc_erp_inventory_list_warehouses($db);
}
$whId = (int) $warehouses[0]['id'];
$whId2 = (int) (isset($warehouses[1]) ? $warehouses[1]['id'] : $warehouses[0]['id']);
echo "Warehouses: " . count($warehouses) . " (primary #$whId)\n";

// ---- Inventory items + stock ----------------------------------------------
$catalogue = array(
    array('sku' => 'BRK-PAD-FRT', 'name' => 'Brake pad set — front (ceramic)', 'cost' => 85.00, 'qty' => 120),
    array('sku' => 'BRK-DSC-FRT', 'name' => 'Brake disc — front vented',       'cost' => 140.00, 'qty' => 60),
    array('sku' => 'OIL-FLT-STD', 'name' => 'Oil filter — standard',           'cost' => 18.50, 'qty' => 400),
    array('sku' => 'AIR-FLT-STD', 'name' => 'Air filter — standard',           'cost' => 32.00, 'qty' => 260),
    array('sku' => 'SPK-PLG-IRD', 'name' => 'Spark plug — iridium (4-pack)',   'cost' => 96.00, 'qty' => 150),
    array('sku' => 'BAT-70AH',    'name' => 'Battery 70Ah maintenance-free',   'cost' => 245.00, 'qty' => 45),
    array('sku' => 'WPR-BLD-24',  'name' => 'Wiper blade 24"',                 'cost' => 22.00, 'qty' => 300),
    array('sku' => 'CLT-KIT-STD', 'name' => 'Clutch kit — standard',           'cost' => 520.00, 'qty' => 20),
);

$createdItems = 0;
$stockPosted = 0;
foreach ($catalogue as $c) {
    $sku = $SEED . '-' . $c['sku'];
    $existing = epc_erp_inventory_get_item_by_sku($db, $sku);
    if ($existing) {
        $itemId = (int) $existing['id'];
    } else {
        $itemId = epc_erp_inventory_create_item($db, array(
            'sku' => $sku,
            'name' => $c['name'],
            'item_type' => 'standard',
            'unit' => 'pcs',
        ));
        $createdItems++;
    }
    // Post opening stock once (skip if movements already exist for this item).
    $cnt = $db->prepare('SELECT COUNT(*) FROM `epc_erp_inv_movements` WHERE `item_id` = ?');
    $cnt->execute(array($itemId));
    if ((int) $cnt->fetchColumn() === 0) {
        epc_erp_inventory_record_movement($db, array(
            'movement_type' => 'opening',
            'warehouse_id' => $whId,
            'item_id' => $itemId,
            'qty' => $c['qty'],
            'unit_cost' => $c['cost'],
            'reference' => $SEED . '-OPEN',
            'note' => 'Seed opening stock',
        ));
        // A smaller second-warehouse purchase to show multi-warehouse stock.
        epc_erp_inventory_record_movement($db, array(
            'movement_type' => 'purchase_in',
            'warehouse_id' => $whId2,
            'item_id' => $itemId,
            'qty' => max(5, (int) round($c['qty'] * 0.2)),
            'unit_cost' => round($c['cost'] * 1.05, 2),
            'reference' => $SEED . '-PO',
            'note' => 'Seed replenishment',
        ));
        $stockPosted++;
    }
}
echo "Inventory: created $createdItems new items; posted stock for $stockPosted items\n";
$val = epc_erp_inventory_valuation_total($db);
echo "Inventory valuation total: " . number_format((float) $val, 2) . " AED\n";

// ---- Sales orders ----------------------------------------------------------
// Pick existing customer user ids.
$custRows = $db->query('SELECT `user_id`, `email` FROM `users` WHERE `user_id` > 0 ORDER BY `user_id` LIMIT 6')->fetchAll(PDO::FETCH_ASSOC);
$custIds = array();
foreach ($custRows as $r) {
    $custIds[] = (int) $r['user_id'];
}
if (empty($custIds)) {
    echo "No users available for sales orders — skipping SO seed.\n";
} else {
    $soSpecs = array(
        array('title' => 'Brake service parts — fleet [' . $SEED . ']', 'status' => 'confirmed', 'lines' => array(
            array('Brake pad set — front (ceramic)', 10, 130.00),
            array('Brake disc — front vented', 6, 210.00),
        )),
        array('title' => 'Workshop consumables order [' . $SEED . ']', 'status' => 'draft', 'lines' => array(
            array('Oil filter — standard', 50, 28.00),
            array('Air filter — standard', 30, 48.00),
            array('Spark plug — iridium (4-pack)', 12, 145.00),
        )),
        array('title' => 'Battery bulk supply [' . $SEED . ']', 'status' => 'invoiced', 'lines' => array(
            array('Battery 70Ah maintenance-free', 8, 365.00),
        )),
        array('title' => 'Wiper + clutch counter sale [' . $SEED . ']', 'status' => 'draft', 'lines' => array(
            array('Wiper blade 24"', 20, 35.00),
            array('Clutch kit — standard', 2, 780.00),
        )),
    );

    $created = 0;
    foreach ($soSpecs as $i => $spec) {
        // Skip if a seeded SO with this title already exists.
        $chk = $db->prepare('SELECT `id`, `status` FROM `epc_erp_sales_orders` WHERE `title` = ? LIMIT 1');
        $chk->execute(array($spec['title']));
        $row = $chk->fetch(PDO::FETCH_ASSOC);
        if ($row) {
            echo "SO exists: {$spec['title']} (#{$row['id']}, {$row['status']})\n";
            continue;
        }
        $desc = array();
        $qty = array();
        $unit = array();
        foreach ($spec['lines'] as $ln) {
            $desc[] = $ln[0];
            $qty[] = $ln[1];
            $unit[] = $ln[2];
        }
        $soId = epc_erp_sales_order_save($db, array(
            'customer_user_id' => $custIds[$i % count($custIds)],
            'title' => $spec['title'],
            'line_desc' => $desc,
            'line_qty' => $qty,
            'line_unit' => $unit,
        ));
        $created++;
        if ($spec['status'] === 'confirmed') {
            epc_erp_sales_order_set_status($db, $soId, 'confirmed');
        } elseif ($spec['status'] === 'invoiced') {
            try {
                $inv = epc_erp_so_convert_to_invoice($db, $soId);
                echo "  SO #$soId -> invoice " . ($inv['invoice_no'] ?? '(ok)') . "\n";
            } catch (Throwable $e) {
                echo "  SO #$soId invoice convert failed: " . $e->getMessage() . "\n";
                epc_erp_sales_order_set_status($db, $soId, 'confirmed');
            }
        }
        echo "SO created: {$spec['title']} (#$soId, target {$spec['status']})\n";
    }
    echo "Sales orders: created $created\n";
}

// ---- Purchase orders -------------------------------------------------------
require_once __DIR__ . '/content/shop/finance/epc_erp_extended.php';
epc_erp_extended_ensure_schema($db);

// Ensure at least two suppliers exist.
$suppliers = epc_erp_list_suppliers($db);
if (count($suppliers) < 2) {
    epc_erp_create_supplier($db, array('name' => 'AL ARQAN Parts [' . $SEED . ']', 'contact_email' => 'seed.sup1@epartscart.local', 'trn' => '100000000000013'));
    epc_erp_create_supplier($db, array('name' => 'Gulf Auto Supply [' . $SEED . ']', 'contact_email' => 'seed.sup2@epartscart.local', 'trn' => '100000000000014'));
    $suppliers = epc_erp_list_suppliers($db);
}
$supA = (int) $suppliers[0]['id'];
$supB = (int) (isset($suppliers[1]) ? $suppliers[1]['id'] : $suppliers[0]['id']);

$poSpecs = array(
    array('supplier' => $supA, 'title' => 'Brake parts replenishment [' . $SEED . ']', 'amount' => 12000.00, 'status' => 'approved'),
    array('supplier' => $supB, 'title' => 'Filters & plugs stock-up [' . $SEED . ']', 'amount' => 7400.00, 'status' => 'received'),
    array('supplier' => $supA, 'title' => 'Battery quarterly order [' . $SEED . ']', 'amount' => 9800.00, 'status' => 'draft'),
);
$poCreated = 0;
foreach ($poSpecs as $spec) {
    $chk = $db->prepare('SELECT `id` FROM `epc_erp_purchase_orders` WHERE `title` = ? LIMIT 1');
    $chk->execute(array($spec['title']));
    if ($chk->fetchColumn()) {
        echo "PO exists: {$spec['title']}\n";
        continue;
    }
    $poId = epc_erp_po_save($db, array(
        'supplier_id' => $spec['supplier'],
        'title' => $spec['title'],
        'amount_ex_vat' => $spec['amount'],
    ));
    if ($spec['status'] !== 'draft') {
        epc_erp_po_set_status($db, $poId, $spec['status']);
    }
    $poCreated++;
    echo "PO created: {$spec['title']} (#$poId, {$spec['status']})\n";
}
echo "Purchase orders: created $poCreated\n";

echo str_repeat('=', 56) . "\n";
echo "DONE.\n";
