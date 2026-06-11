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
    // Jewellery & bullion samples — demonstrate gram / carat / tola units
    array('sku' => 'GOLD-22K-BAR', 'name' => 'Gold 22K — by weight',           'cost' => 232.00, 'qty' => 1500, 'unit' => 'gram'),
    array('sku' => 'GOLD-18K-BAR', 'name' => 'Gold 18K — by weight',           'cost' => 190.00, 'qty' => 900,  'unit' => 'gram'),
    array('sku' => 'SILVER-925',   'name' => 'Silver 925 — by weight',         'cost' => 3.10,   'qty' => 8000, 'unit' => 'gram'),
    array('sku' => 'DIAMOND-VS1',  'name' => 'Diamond VS1 G — loose stones',   'cost' => 1400.00,'qty' => 35,   'unit' => 'carat'),
    array('sku' => 'GOLD-BISCUIT', 'name' => 'Gold biscuit (bullion)',         'cost' => 2700.00,'qty' => 60,   'unit' => 'tola'),
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
            'unit' => $c['unit'] ?? 'pcs',
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

// ---- CRM pipeline (leads / opportunities / quotes / tickets / projects) -----
// Fills Sales → CRM and Sales → Proposals (quotes) in one shot.
try {
    require_once __DIR__ . '/content/shop/finance/epc_crm_schema.php';
    epc_crm_ensure_schema($db);
    $crmBefore = (int) $db->query('SELECT COUNT(*) FROM `epc_crm_leads`')->fetchColumn();
    epc_crm_seed_sample_if_empty($db);
    $crmAfter = (int) $db->query('SELECT COUNT(*) FROM `epc_crm_leads`')->fetchColumn();
    $oppN = (int) $db->query('SELECT COUNT(*) FROM `epc_crm_opportunities`')->fetchColumn();
    $qN = (int) $db->query('SELECT COUNT(*) FROM `epc_crm_quotes`')->fetchColumn();
    // Quotes/proposals are only created by the bundled seeder when leads were
    // empty. If leads pre-existed (so it skipped) but there are no quotes yet,
    // seed proposals here so Sales -> Proposals shows records.
    if ($qN === 0) {
        // The live quotes table may predate newer columns (schema drift), so
        // introspect the actual columns and insert only what exists.
        $qcols = array();
        foreach ($db->query('SHOW COLUMNS FROM `epc_crm_quotes`')->fetchAll(PDO::FETCH_ASSOC) as $cr) {
            $qcols[strtolower($cr['Field'])] = true;
        }
        $amountCol = isset($qcols['subtotal']) ? 'subtotal' : (isset($qcols['total']) ? 'total' : (isset($qcols['amount']) ? 'amount' : ''));
        $opps = $db->query('SELECT `id`, `lead_id`, `title`, `amount` FROM `epc_crm_opportunities` ORDER BY `id` LIMIT 3')->fetchAll(PDO::FETCH_ASSOC);
        $now = time();
        $statuses = array('sent', 'draft', 'accepted');
        $seq = 1;
        foreach ($opps as $i => $op) {
            $qno = 'Q-' . date('Ym') . '-' . str_pad((string) ($seq++), 3, '0', STR_PAD_LEFT);
            $sub = round((float) ($op['amount'] ?? 0) * 0.95, 2);
            if ($sub <= 0) { $sub = 5000.00; }
            $row = array();
            if (isset($qcols['opportunity_id'])) { $row['opportunity_id'] = (int) $op['id']; }
            if (isset($qcols['lead_id'])) { $row['lead_id'] = (int) $op['lead_id']; }
            if (isset($qcols['quote_number'])) { $row['quote_number'] = $qno; }
            if (isset($qcols['status'])) { $row['status'] = $statuses[$i % 3]; }
            if ($amountCol !== '') { $row[$amountCol] = $sub; }
            if (isset($qcols['notes'])) { $row['notes'] = 'Proposal for ' . ($op['title'] ?? 'opportunity') . ' [' . $SEED . ']'; }
            if (isset($qcols['time_created'])) { $row['time_created'] = $now - $i * 3600; }
            if (isset($qcols['time_updated'])) { $row['time_updated'] = $now - $i * 3600; }
            if (isset($qcols['active'])) { $row['active'] = 1; }
            $cols = array_keys($row);
            $ph = implode(',', array_fill(0, count($cols), '?'));
            $sql = 'INSERT INTO `epc_crm_quotes` (`' . implode('`,`', $cols) . '`) VALUES (' . $ph . ')';
            $db->prepare($sql)->execute(array_values($row));
            $qid = (int) $db->lastInsertId();
            // Quote line (best-effort; ignore if table/columns differ).
            try {
                $db->prepare('INSERT INTO `epc_crm_quote_lines` (`quote_id`, `description`, `qty`, `unit_price`, `sort_order`) VALUES (?,?,?,?,0)')
                   ->execute(array($qid, ($op['title'] ?? 'Supply package'), 1, $sub));
            } catch (Throwable $e) { /* line table optional */ }
        }
        $qN = (int) $db->query('SELECT COUNT(*) FROM `epc_crm_quotes`')->fetchColumn();
    }
    // Older live quotes tables predate the `subtotal` column the Proposals
    // renderer reads, so amounts showed 0.00. Ensure the column exists (done in
    // extended_ensure_schema) and backfill any zero subtotals from the linked
    // opportunity amount so Sales -> Proposals shows real figures.
    require_once __DIR__ . '/content/shop/finance/epc_erp_extended.php';
    epc_erp_extended_ensure_schema($db);
    try {
        $db->exec('UPDATE `epc_crm_quotes` q
            JOIN `epc_crm_opportunities` o ON o.`id` = q.`opportunity_id`
            SET q.`subtotal` = ROUND(o.`amount` * 0.95, 2)
            WHERE (q.`subtotal` IS NULL OR q.`subtotal` = 0) AND o.`amount` > 0');
        echo "CRM: quote subtotals backfilled\n";
    } catch (Throwable $e) {
        echo "CRM quote subtotal backfill skipped: " . $e->getMessage() . "\n";
    }
    echo "CRM: leads " . $crmBefore . " -> " . $crmAfter . "; opportunities $oppN; quotes/proposals $qN\n";
} catch (Throwable $e) {
    echo "CRM seed skipped: " . $e->getMessage() . "\n";
}

// ---- Cash account (needed by payment batches / petty cash) ------------------
$cashAccounts = epc_erp_list_cash_accounts($db);
if (empty($cashAccounts)) {
    epc_erp_create_cash_account($db, array('name' => 'Main bank — AED [' . $SEED . ']', 'account_type' => 'bank', 'opening_balance' => 250000));
    epc_erp_create_cash_account($db, array('name' => 'Cash on hand [' . $SEED . ']', 'account_type' => 'cash', 'opening_balance' => 15000));
    $cashAccounts = epc_erp_list_cash_accounts($db);
}
$cashAcctId = !empty($cashAccounts) ? (int) $cashAccounts[0]['id'] : 0;

// ---- Payment batches -------------------------------------------------------
try {
    $existingBatches = epc_erp_payment_batches_list($db);
    if (count($existingBatches) === 0) {
        epc_erp_payment_batch_save($db, array('batch_type' => 'local', 'account_id' => $cashAcctId, 'total_amount' => 28400.00, 'line_count' => 6, 'execution_date' => date('Y-m-d'), 'notes' => 'Supplier run — week ' . date('W') . ' [' . $SEED . ']'));
        epc_erp_payment_batch_save($db, array('batch_type' => 'cheque', 'account_id' => $cashAcctId, 'total_amount' => 11750.00, 'line_count' => 3, 'execution_date' => date('Y-m-d', time() + 7 * 86400), 'notes' => 'Cheque batch — utilities & rent [' . $SEED . ']'));
        echo "Payment batches: created 2\n";
    } else {
        echo "Payment batches: exist (" . count($existingBatches) . ")\n";
    }
} catch (Throwable $e) {
    echo "Payment batch seed skipped: " . $e->getMessage() . "\n";
}

// ---- Petty cash floats -----------------------------------------------------
try {
    $existingFloats = epc_erp_petty_cash_list($db);
    if (count($existingFloats) === 0) {
        epc_erp_petty_cash_save($db, array('name' => 'Front desk float [' . $SEED . ']', 'float_amount' => 2000.00, 'custodian_user_id' => 0));
        epc_erp_petty_cash_save($db, array('name' => 'Workshop float [' . $SEED . ']', 'float_amount' => 1500.00, 'custodian_user_id' => 0));
        echo "Petty cash: created 2 floats\n";
    } else {
        echo "Petty cash: exist (" . count($existingFloats) . ")\n";
    }
} catch (Throwable $e) {
    echo "Petty cash seed skipped: " . $e->getMessage() . "\n";
}

// ---- Expense reports -------------------------------------------------------
try {
    require_once __DIR__ . '/content/shop/finance/epc_erp_phase8.php';
    epc_erp_phase8_ensure_schema($db);
    $existingExp = epc_erp_expense_reports_list($db);
    if (count($existingExp) === 0) {
        $staffUid = !empty($custIds) ? (int) $custIds[0] : 0;
        epc_erp_expense_report_save($db, array('staff_user_id' => $staffUid, 'title' => 'Client visit — Abu Dhabi [' . $SEED . ']', 'total_amount' => 845.50, 'period_from' => date('Y-m-01'), 'period_to' => date('Y-m-d'), 'notes' => 'Fuel, tolls, meals'));
        epc_erp_expense_report_save($db, array('staff_user_id' => $staffUid, 'title' => 'Trade show booth — DWTC [' . $SEED . ']', 'total_amount' => 3200.00, 'period_from' => date('Y-m-01'), 'period_to' => date('Y-m-d'), 'notes' => 'Stand + materials'));
        echo "Expense reports: created 2\n";
    } else {
        echo "Expense reports: exist (" . count($existingExp) . ")\n";
    }
} catch (Throwable $e) {
    echo "Expense report seed skipped: " . $e->getMessage() . "\n";
}

// ---- Agenda events ---------------------------------------------------------
try {
    $existingEvents = epc_erp_agenda_list($db, date('Y-m'));
    if (count($existingEvents) === 0) {
        epc_erp_agenda_save($db, array('title' => 'Month-end close review [' . $SEED . ']', 'event_type' => 'finance', 'start_at' => date('Y-m-25 10:00:00'), 'end_at' => date('Y-m-25 11:30:00'), 'location' => 'Finance room'));
        epc_erp_agenda_save($db, array('title' => 'Supplier negotiation — Gulf Auto [' . $SEED . ']', 'event_type' => 'meeting', 'start_at' => date('Y-m-d 14:00:00', time() + 2 * 86400), 'end_at' => date('Y-m-d 15:00:00', time() + 2 * 86400), 'location' => 'Meeting room A'));
        epc_erp_agenda_save($db, array('title' => 'Stock count — main warehouse [' . $SEED . ']', 'event_type' => 'operations', 'start_at' => date('Y-m-d 09:00:00', time() + 5 * 86400), 'end_at' => date('Y-m-d 12:00:00', time() + 5 * 86400), 'location' => 'Warehouse'));
        echo "Agenda: created 3 events\n";
    } else {
        echo "Agenda: exist (" . count($existingEvents) . ")\n";
    }
} catch (Throwable $e) {
    echo "Agenda seed skipped: " . $e->getMessage() . "\n";
}

// ---- Knowledge base --------------------------------------------------------
try {
    epc_erp_kb_seed_defaults($db);
    $kbN = (int) $db->query('SELECT COUNT(*) FROM `epc_erp_kb_articles`')->fetchColumn();
    echo "Knowledge base: $kbN articles\n";
} catch (Throwable $e) {
    echo "KB seed skipped: " . $e->getMessage() . "\n";
}

echo str_repeat('=', 56) . "\n";
echo "DONE.\n";
