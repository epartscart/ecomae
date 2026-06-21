<?php
/**
 * CLI tests for Retail / Commerce: pure pricing (best discount, line price,
 * statement totals), channels, assortments, periodic discounts (date-effective
 * + per/all channel), POS sales, statement/Z-report, summary, multi-company.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_retail_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_retail.php';

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
function approx(float $a, float $b): bool
{
    return abs($a - $b) < 0.01;
}

foreach (array('epc_rtl_txn_line', 'epc_rtl_txn', 'epc_rtl_discount', 'epc_rtl_assortment', 'epc_rtl_channel') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}
epc_rtl_ensure_schema($db);

$CO = 1;

section('Pure: best discount');
$d1 = epc_rtl_best_discount(100.0, array(array('disc_type' => 'percent', 'value' => 10), array('disc_type' => 'amount', 'value' => 15)));
check('best of 10% vs -15 = 85', approx($d1['net_unit'], 85.0));
check('discount unit = 15', approx($d1['discount_unit'], 15.0));
$d2 = epc_rtl_best_discount(100.0, array());
check('no discount -> unit unchanged', approx($d2['net_unit'], 100.0) && $d2['applied'] === null);
$d3 = epc_rtl_best_discount(10.0, array(array('disc_type' => 'amount', 'value' => 25)));
check('over-discount floored at 0', approx($d3['net_unit'], 0.0));

section('Pure: line price + tax');
$lp = epc_rtl_price_line(100.0, 2, array(array('disc_type' => 'percent', 'value' => 10)), 5.0);
check('gross = 200', approx($lp['gross'], 200.0));
check('discount = 20', approx($lp['discount'], 20.0));
check('net = 180', approx($lp['net'], 180.0));
check('tax 5% on net = 9', approx($lp['tax'], 9.0));
check('total = 189', approx($lp['total'], 189.0));
$lp0 = epc_rtl_price_line(50.0, 3, array(), 0.0);
check('no discount no tax: total 150', approx($lp0['total'], 150.0));

section('Pure: statement totals');
$stt = epc_rtl_statement_totals(array(
    array('gross' => 200, 'discount' => 20, 'net' => 180, 'tax' => 9, 'total' => 189, 'tender_type' => 'cash'),
    array('gross' => 100, 'discount' => 0, 'net' => 100, 'tax' => 5, 'total' => 105, 'tender_type' => 'card'),
    array('gross' => 50, 'discount' => 0, 'net' => 50, 'tax' => 2.5, 'total' => 52.5, 'tender_type' => 'cash'),
));
check('count = 3', $stt['count'] === 3);
check('total = 346.5', approx($stt['total'], 346.5));
check('by tender cash = 241.5', approx($stt['by_tender']['cash'], 241.5));
check('by tender card = 105', approx($stt['by_tender']['card'], 105.0));

section('Channels');
$ch = epc_rtl_channel_save($db, $CO, array('code' => 'DXB-01', 'name' => 'Dubai Mall store', 'channel_type' => 'store', 'currency' => 'AED', 'active' => 1));
check('channel saved', $ch > 0);
$online = epc_rtl_channel_save($db, $CO, array('code' => 'WEB', 'name' => 'Online', 'channel_type' => 'online', 'currency' => 'AED', 'active' => 1));
check('invalid channel type rejected', (function () use ($db, $CO) { try { epc_rtl_channel_save($db, $CO, array('code' => 'X', 'channel_type' => 'spaceship')); return false; } catch (Throwable $e) { return true; } })());
check('code required', (function () use ($db, $CO) { try { epc_rtl_channel_save($db, $CO, array('code' => '')); return false; } catch (Throwable $e) { return true; } })());
check('channels list = 2', count(epc_rtl_channels($db, $CO)) === 2);

section('Assortments');
epc_rtl_assortment_set($db, $CO, $ch, 101, true);
epc_rtl_assortment_set($db, $CO, $ch, 102, true);
epc_rtl_assortment_set($db, $CO, $ch, 102, false);
check('item 101 in assortment', epc_rtl_in_assortment($db, $ch, 101) === true);
check('item 102 deactivated', epc_rtl_in_assortment($db, $ch, 102) === false);
check('unlisted item not in assortment', epc_rtl_in_assortment($db, $ch, 999) === false);
check('channel assort_count = 1', epc_rtl_channels($db, $CO)[0]['assort_count'] === 1);

section('Discounts (date-effective + scope)');
$now = 1_700_000_000;
epc_rtl_discount_save($db, $CO, array('channel_id' => $ch, 'code' => 'CH10', 'name' => 'Store 10%', 'disc_type' => 'percent', 'value' => 10, 'starts' => 0, 'ends' => 0, 'active' => 1));
epc_rtl_discount_save($db, $CO, array('channel_id' => 0, 'code' => 'ALL5', 'name' => 'All -5', 'disc_type' => 'amount', 'value' => 5, 'starts' => 0, 'ends' => 0, 'active' => 1));
epc_rtl_discount_save($db, $CO, array('channel_id' => $ch, 'code' => 'EXPIRED', 'name' => 'Old', 'disc_type' => 'percent', 'value' => 90, 'starts' => 1, 'ends' => 1000, 'active' => 1));
$act = epc_rtl_active_discounts($db, $CO, $ch, $now);
check('active discounts for channel = 2 (CH10 + ALL5, not expired)', count($act) === 2);
check('invalid discount type rejected', (function () use ($db, $CO) { try { epc_rtl_discount_save($db, $CO, array('code' => 'X', 'disc_type' => 'magic', 'value' => 1)); return false; } catch (Throwable $e) { return true; } })());

section('POS sale (applies best active discount + tax)');
// unit 100 qty 1: CH10 -> 90; ALL5 -> 95; best = 90. tax 5% = 4.5 -> total 94.5
$sale = epc_rtl_pos_sale($db, $CO, $ch, array(array('item_id' => 101, 'qty' => 1, 'unit_price' => 100)), 'card', 5.0, 'R-1', $now);
check('sale net = 90 (best 10%)', approx((float) $sale['net'], 90.0));
check('sale tax = 4.5', approx((float) $sale['tax'], 4.5));
check('sale total = 94.5', approx((float) $sale['total'], 94.5));
check('invalid tender rejected', (function () use ($db, $CO, $ch) { try { epc_rtl_pos_sale($db, $CO, $ch, array(), 'crypto', 0); return false; } catch (Throwable $e) { return true; } })());
epc_rtl_pos_sale($db, $CO, $ch, array(array('item_id' => 101, 'qty' => 2, 'unit_price' => 50)), 'cash', 5.0, 'R-2', $now + 10);

section('Statement / Z-report');
$z = epc_rtl_statement($db, $CO, $ch, $now - 100, $now + 100);
check('statement count = 2', $z['count'] === 2);
check('statement has cash + card tenders', isset($z['by_tender']['cash']) && isset($z['by_tender']['card']));

section('Summary + multi-company');
epc_rtl_channel_save($db, 2, array('code' => 'OTHER', 'name' => 'Other co', 'channel_type' => 'store', 'active' => 1));
check('company 2 isolated (1 channel, 0 txns)', epc_rtl_summary($db, 2)['channels'] === 1 && epc_rtl_summary($db, 2)['transactions'] === 0);
$sum = epc_rtl_summary($db, $CO);
check('summary channels = 2', $sum['channels'] === 2);
check('summary transactions = 2', $sum['transactions'] === 2);
check('summary sales_total > 0', $sum['sales_total'] > 0);

echo "\n========================================\n";
echo 'RETAIL TESTS: ' . $pass_count . ' passed, ' . $fail_count . " failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
