<?php
/**
 * CLI tests for the E-commerce <-> ERP bridge.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_plat_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_ecommerce_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_plat_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_pricing.php';
require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_ecommerce.php';

$db = new PDO("mysql:host=$host;dbname=$name;charset=utf8", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));

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

foreach (array('epc_ec_order_lines', 'epc_ec_orders', 'epc_loy_ledger', 'epc_loy_accounts', 'epc_pl_prices', 'epc_pl_lists') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}

section('Storefront availability (ATP across warehouses)');
$atp = epc_ec_availability(array(
    array('warehouse_id' => 1, 'on_hand' => 10, 'reserved' => 3),
    array('warehouse_id' => 2, 'on_hand' => 5, 'reserved' => 8), // oversold -> 0, not negative
));
check('available = 7 (10-3) + 0 (clamped) = 7', abs($atp['available'] - 7.0) < 0.01);
check('in_stock true', $atp['in_stock'] === true);
check('per-warehouse breakdown present', count($atp['by_warehouse']) === 2);
$oos = epc_ec_availability(array(array('warehouse_id' => 1, 'on_hand' => 2, 'reserved' => 2)));
check('out of stock -> in_stock false', $oos['in_stock'] === false);

section('Storefront price via ERP price list');
epc_price_ensure_schema($db);
$baseList = epc_pl_list_save($db, array('code' => 'BASE', 'name' => 'Base', 'customer_id' => 0, 'priority' => 1));
$vipList = epc_pl_list_save($db, array('code' => 'VIP', 'name' => 'VIP', 'customer_id' => 50, 'priority' => 10));
epc_pl_price_set($db, array('list_id' => $baseList, 'item_id' => 100, 'price' => 20.00, 'min_qty' => 1));
epc_pl_price_set($db, array('list_id' => $vipList, 'item_id' => 100, 'price' => 15.00, 'min_qty' => 1));
$pBase = epc_ec_storefront_price($db, 100, 1, 0, 99.0);
check('anonymous shopper gets base list price 20', abs($pBase['price'] - 20.0) < 0.01);
$pVip = epc_ec_storefront_price($db, 100, 1, 50, 99.0);
check('VIP customer gets customer-list price 15', abs($pVip['price'] - 15.0) < 0.01);
$pFallback = epc_ec_storefront_price($db, 999, 1, 0, 42.5);
check('unknown item falls back to base price 42.5', abs($pFallback['price'] - 42.5) < 0.01 && $pFallback['source'] === 'base');

section('B2B credit-limit checkout gate');
$ok = epc_ec_credit_check(10000, 6000, 3000);
check('within limit allowed (avail 4000 >= 3000)', $ok['allowed'] === true && abs($ok['available_credit'] - 4000.0) < 0.01);
$blocked = epc_ec_credit_check(10000, 9000, 3000);
check('over limit blocked (avail 1000 < 3000)', $blocked['allowed'] === false && $blocked['reason'] === 'over_limit');
$noLimit = epc_ec_credit_check(0, 50000, 9999);
check('no credit limit set -> always allowed', $noLimit['allowed'] === true);

section('Order money computation (discount + tax)');
$money = epc_ec_compute_order(array(
    array('item_id' => 100, 'sku' => 'A', 'qty' => 2, 'unit_price' => 15.00),
    array('item_id' => 101, 'sku' => 'B', 'qty' => 1, 'unit_price' => 20.00),
), 5.00, 0.05);
check('subtotal = 50', abs($money['subtotal'] - 50.0) < 0.01);
check('discount = 5', abs($money['discount'] - 5.0) < 0.01);
check('taxable = 45', abs($money['taxable'] - 45.0) < 0.01);
check('tax @5% = 2.25', abs($money['tax'] - 2.25) < 0.01);
check('total = 47.25', abs($money['total'] - 47.25) < 0.01);
check('discount cannot exceed subtotal', abs(epc_ec_compute_order(array(array('qty' => 1, 'unit_price' => 10)), 999, 0)['discount'] - 10.0) < 0.01);

section('Web order intake (idempotent)');
$intake = epc_ec_intake_order($db, array('web_order_ref' => 'WEB-1001', 'customer_id' => 50, 'currency' => 'AED'), $money['lines'], $money);
check('order persisted with id', $intake['order_id'] > 0);
check('2 order lines stored', (int) $db->query("SELECT COUNT(*) FROM epc_ec_order_lines WHERE order_id=" . (int) $intake['order_id'])->fetchColumn() === 2);
$intake2 = epc_ec_intake_order($db, array('web_order_ref' => 'WEB-1001', 'customer_id' => 50, 'currency' => 'AED'), $money['lines'], $money);
check('re-intake same ref is idempotent (same id)', $intake2['order_id'] === $intake['order_id']);
check('still only 1 order row', (int) $db->query("SELECT COUNT(*) FROM epc_ec_orders")->fetchColumn() === 1);

section('Document chain advance (SO -> DO -> Invoice)');
$soCalls = 0;
$advanced = epc_ec_advance_documents($db, $intake['order_id'], array(
    'so' => function ($o) use (&$soCalls) {
        $soCalls++;
        return 5001;
    },
    'do' => function ($o) {
        return 6001;
    },
    'invoice' => function ($o) {
        return 7001;
    },
));
check('SO/DO/Invoice ids recorded', (int) $advanced['so_id'] === 5001 && (int) $advanced['do_id'] === 6001 && (int) $advanced['invoice_id'] === 7001);
check('status becomes invoiced', $advanced['status'] === 'invoiced');
$advancedAgain = epc_ec_advance_documents($db, $intake['order_id'], array(
    'so' => function ($o) use (&$soCalls) {
        $soCalls++;
        return 9999;
    },
));
check('idempotent: SO creator not called again once set', $soCalls === 1 && (int) $advancedAgain['so_id'] === 5001);

section('Payment capture + loyalty accrual');
$paid = epc_ec_mark_paid($db, $intake['order_id'], 1.0); // 1 point per currency unit
check('order marked paid', $paid['payment_status'] === 'paid');
check('loyalty earned = total 47.25', abs($paid['loyalty_earned'] - 47.25) < 0.01);
check('loyalty balance reflects accrual', abs(epc_loy_earn($db, 50, 0, 1.0, 'noop') - 47.25) < 0.01);

section('Customer My-Account portal');
epc_ec_intake_order($db, array('web_order_ref' => 'WEB-1002', 'customer_id' => 50, 'currency' => 'AED', 'payment_status' => 'unpaid'), $money['lines'], $money);
$portal = epc_ec_customer_portal($db, 50);
check('portal lists 2 orders for customer', $portal['count'] === 2);
check('total_spent counts only paid order (47.25)', abs($portal['total_spent'] - 47.25) < 0.01);
check('portal isolates other customer (none)', epc_ec_customer_portal($db, 999)['count'] === 0);

echo "\n========================================\n";
echo "E-COMMERCE BRIDGE TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
