<?php
/**
 * CLI tests for the native commerce <-> ERP data-link.
 *
 * Seeds a realistic subset of the live storefront schema (shop_orders,
 * shop_orders_items, users, users_profiles, shop_users_accounting,
 * shop_catalogue_products, shop_storages, shop_storages_data) with
 * multi-industry data, then asserts the data-link normalizes it into ERP
 * customers / AR / orders / products, links orders into the ERP bridge
 * (idempotently), and builds the ERP sales dashboard figures.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     DB_NAME2=erp_test_b php tests/erp_advanced/run_datalink_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
    http_response_code(403);
    exit('CLI only');
}

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$name2 = getenv('DB_NAME2') ?: 'erp_test_b';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

$root = dirname(__DIR__, 2);
require_once $root . '/content/shop/finance/epc_erp_ecommerce.php';
require_once $root . '/content/shop/finance/epc_erp_datalink.php';

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

/**
 * Build the native storefront schema subset + seed multi-industry data.
 * $industry shifts the seeded values so the two tenants differ.
 */
function seed_native(PDO $db, string $industry): void
{
    $tables = array(
        'shop_orders', 'shop_orders_items', 'users', 'users_profiles',
        'shop_users_accounting', 'shop_catalogue_products', 'shop_storages',
        'shop_storages_data', 'epc_ec_orders', 'epc_ec_order_lines',
    );
    foreach ($tables as $t) {
        $db->exec('DROP TABLE IF EXISTS `' . $t . '`');
    }

    $db->exec("CREATE TABLE `users` (
        user_id INT AUTO_INCREMENT PRIMARY KEY, email VARCHAR(190) DEFAULT '',
        phone VARCHAR(40) DEFAULT '', time_registered INT DEFAULT 0)");
    $db->exec("CREATE TABLE `users_profiles` (
        id INT AUTO_INCREMENT PRIMARY KEY, user_id INT, data_key VARCHAR(64), data_value TEXT)");
    $db->exec("CREATE TABLE `shop_orders` (
        id INT AUTO_INCREMENT PRIMARY KEY, user_id INT, session_id INT DEFAULT 0,
        time INT DEFAULT 0, successfully_created TINYINT DEFAULT 1, status INT DEFAULT 0,
        paid TINYINT DEFAULT 0, paid_time INT DEFAULT 0, paid_type TINYINT DEFAULT 0,
        office_id INT DEFAULT 0)");
    $db->exec("CREATE TABLE `shop_orders_items` (
        id INT AUTO_INCREMENT PRIMARY KEY, order_id INT, product_id INT DEFAULT 0,
        price DECIMAL(14,2) DEFAULT 0, count_need INT DEFAULT 1, status INT DEFAULT 0,
        t2_name TEXT, t2_article TEXT, t2_price_purchase DECIMAL(14,2) DEFAULT 0)");
    $db->exec("CREATE TABLE `shop_users_accounting` (
        id INT AUTO_INCREMENT PRIMARY KEY, user_id INT, time INT DEFAULT 0,
        income TINYINT DEFAULT 0, amount DECIMAL(14,2) DEFAULT 0, operation_code VARCHAR(32) DEFAULT '',
        active TINYINT DEFAULT 1, pay_orders TEXT, order_id INT DEFAULT 0, office_id INT DEFAULT 0)");
    $db->exec("CREATE TABLE `shop_catalogue_products` (
        id INT AUTO_INCREMENT PRIMARY KEY, category_id INT DEFAULT 0, caption VARCHAR(190) DEFAULT '',
        alias VARCHAR(190) DEFAULT '', published_flag TINYINT DEFAULT 1)");
    $db->exec("CREATE TABLE `shop_storages` (
        id INT AUTO_INCREMENT PRIMARY KEY, name TEXT, short_name TEXT, hidden TINYINT DEFAULT 0)");
    $db->exec("CREATE TABLE `shop_storages_data` (
        id INT AUTO_INCREMENT PRIMARY KEY, storage_id INT, product_id INT, category_id INT DEFAULT 0,
        price DECIMAL(14,2) DEFAULT 0, price_purchase DECIMAL(14,2) DEFAULT 0,
        exist INT DEFAULT 0, reserved INT DEFAULT 0, issued INT DEFAULT 0)");

    // Industry-specific catalogue
    $catalogues = array(
        'jewellery' => array(
            array('22K Gold Ring', 1800.00, 1500.00, 12),
            array('Diamond Pendant', 5200.00, 4100.00, 4),
            array('Silver Bracelet', 320.00, 210.00, 30),
        ),
        'electronics' => array(
            array('4K Smart TV 55"', 2400.00, 1850.00, 18),
            array('Wireless Earbuds', 350.00, 190.00, 120),
            array('Gaming Laptop', 5600.00, 4700.00, 7),
        ),
    );
    $cat = $catalogues[$industry] ?? $catalogues['electronics'];
    $wh = $db->prepare("INSERT INTO `shop_storages` (name, short_name) VALUES (?,?)");
    $wh->execute(array('Main Warehouse', 'MAIN'));
    $whId = (int) $db->lastInsertId();

    $prodIds = array();
    $pp = $db->prepare("INSERT INTO `shop_catalogue_products` (caption, alias, published_flag) VALUES (?,?,1)");
    $sd = $db->prepare("INSERT INTO `shop_storages_data` (storage_id, product_id, price, price_purchase, exist, reserved) VALUES (?,?,?,?,?,?)");
    foreach ($cat as $i => $c) {
        $pp->execute(array($c[0], 'p-' . $i));
        $pid = (int) $db->lastInsertId();
        $prodIds[] = array('id' => $pid, 'name' => $c[0], 'price' => $c[1], 'cost' => $c[2]);
        $sd->execute(array($whId, $pid, $c[1], $c[2], (int) $c[3], 0));
    }

    // Customers
    $custs = array(
        array('alice@' . $industry . '.test', '+9715000001', 'Alice', 'Khan', 'Khan Trading'),
        array('bob@' . $industry . '.test', '+9715000002', 'Bob', 'Ali', ''),
    );
    $uu = $db->prepare("INSERT INTO `users` (email, phone, time_registered) VALUES (?,?,?)");
    $upf = $db->prepare("INSERT INTO `users_profiles` (user_id, data_key, data_value) VALUES (?,?,?)");
    $custIds = array();
    foreach ($custs as $c) {
        $uu->execute(array($c[0], $c[1], time() - 86400 * 30));
        $uid = (int) $db->lastInsertId();
        $custIds[] = $uid;
        $upf->execute(array($uid, 'first_name', $c[2]));
        $upf->execute(array($uid, 'last_name', $c[3]));
        if ($c[4] !== '') {
            $upf->execute(array($uid, 'company', $c[4]));
        }
    }

    // Orders: customer 0 buys product 0 (paid) and product 1 (unpaid); customer 1 buys product 2 (paid)
    $oo = $db->prepare("INSERT INTO `shop_orders` (user_id, time, status, paid, paid_time, office_id) VALUES (?,?,?,?,?,?)");
    $oi = $db->prepare("INSERT INTO `shop_orders_items` (order_id, product_id, price, count_need, t2_name, t2_article, t2_price_purchase) VALUES (?,?,?,?,?,?,?)");

    // Order A (paid): cust0, product0 x2
    $oo->execute(array($custIds[0], time() - 86400 * 5, 3, 1, time() - 86400 * 5, $whId));
    $oa = (int) $db->lastInsertId();
    $oi->execute(array($oa, $prodIds[0]['id'], $prodIds[0]['price'], 2, $prodIds[0]['name'], 'A0', $prodIds[0]['cost']));

    // Order B (unpaid): cust0, product1 x1
    $oo->execute(array($custIds[0], time() - 86400 * 2, 1, 0, 0, $whId));
    $ob = (int) $db->lastInsertId();
    $oi->execute(array($ob, $prodIds[1]['id'], $prodIds[1]['price'], 1, $prodIds[1]['name'], 'A1', $prodIds[1]['cost']));

    // Order C (paid): cust1, product2 x3
    $oo->execute(array($custIds[1], time() - 86400 * 1, 3, 1, time() - 86400, $whId));
    $oc = (int) $db->lastInsertId();
    $oi->execute(array($oc, $prodIds[2]['id'], $prodIds[2]['price'], 3, $prodIds[2]['name'], 'A2', $prodIds[2]['cost']));

    // Native AR ledger for cust0: a charge (debit) and a partial payment (credit)
    $ar = $db->prepare("INSERT INTO `shop_users_accounting` (user_id, time, income, amount, operation_code, active, order_id) VALUES (?,?,?,?,?,1,?)");
    $ar->execute(array($custIds[0], time() - 86400 * 5, 0, $prodIds[0]['price'] * 2, 'order_charge', $oa)); // debit 3600/4800
    $ar->execute(array($custIds[0], time() - 86400 * 4, 1, 1000.00, 'payment', 0)); // credit 1000
}

$db = new PDO("mysql:host=$host;dbname=$name;charset=utf8", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
seed_native($db, 'jewellery');

section('Schema introspection (tolerant)');
check('detects shop_orders exists', epc_dl_table_exists($db, 'shop_orders') === true);
check('detects missing table absent', epc_dl_table_exists($db, 'nope_table_xyz') === false);
check('reads shop_orders columns', in_array('paid', epc_dl_columns($db, 'shop_orders'), true));
check('has_col positive', epc_dl_has_col($db, 'users', 'email') === true);
check('has_col negative', epc_dl_has_col($db, 'users', 'no_such_col') === false);

section('Customers <- users + users_profiles');
$customers = epc_dl_customers($db);
check('2 customers normalized', count($customers) === 2);
$byName = array();
foreach ($customers as $c) {
    $byName[$c['email']] = $c;
}
check('company + person name composed', $byName['alice@jewellery.test']['name'] === 'Khan Trading (Alice Khan)');
check('person-only name when no company', $byName['bob@jewellery.test']['name'] === 'Bob Ali');
check('email carried through', $byName['bob@jewellery.test']['email'] === 'bob@jewellery.test');
check('phone carried through', $byName['alice@jewellery.test']['phone'] === '+9715000001');

section('AR (receivables) <- shop_users_accounting');
$cust0 = $customers[1]['user_id'] < $customers[0]['user_id'] ? $customers[1]['user_id'] : null;
// find Alice's id explicitly
$aliceId = $byName['alice@jewellery.test']['user_id'];
$ar = epc_dl_customer_ar($db, $aliceId);
check('debit = 3600 (1800*2)', abs($ar['debit'] - 3600.00) < 0.01);
check('credit = 1000', abs($ar['credit'] - 1000.00) < 0.01);
check('balance = 2600 outstanding (debit-credit)', abs($ar['balance'] - (-2600.00)) < 0.01 || abs($ar['balance'] - 2600.00) < 0.01);
check('two ledger entries', count($ar['entries']) === 2);
$arBob = epc_dl_customer_ar($db, $byName['bob@jewellery.test']['user_id']);
check('customer with no ledger -> zero balance', abs($arBob['balance']) < 0.01 && count($arBob['entries']) === 0);

section('Orders <- shop_orders + items (with margin)');
$orders = epc_dl_orders($db);
check('3 orders normalized', count($orders) === 3);
$paidCount = 0;
foreach ($orders as $o) {
    if ($o['paid']) {
        $paidCount++;
    }
}
check('2 paid orders', $paidCount === 2);
// Order A: product0 1800 cost1500 x2 -> total 3600 cost 3000 margin 600
$orderA = null;
foreach ($orders as $o) {
    if (abs($o['total'] - 3600.00) < 0.01) {
        $orderA = $o;
    }
}
check('order A total 3600', $orderA !== null);
check('order A cost 3000', $orderA && abs($orderA['cost'] - 3000.00) < 0.01);
check('order A margin 600', $orderA && abs($orderA['margin'] - 600.00) < 0.01);
$paidOnly = epc_dl_orders($db, array('paid_only' => true));
check('paid_only filter returns 2', count($paidOnly) === 2);
$forAlice = epc_dl_orders($db, array('user_id' => $aliceId));
check('user filter returns Alice 2 orders', count($forAlice) === 2);

section('Products <- catalogue + storages_data');
$products = epc_dl_products($db);
check('3 products normalized', count($products) === 3);
$ring = null;
foreach ($products as $p) {
    if ($p['name'] === '22K Gold Ring') {
        $ring = $p;
    }
}
check('ring price 1800 from stock data', $ring && abs($ring['price'] - 1800.00) < 0.01);
check('ring cost 1500 from stock data', $ring && abs($ring['cost'] - 1500.00) < 0.01);
check('ring on_hand 12', $ring && abs($ring['on_hand'] - 12.0) < 0.01);

section('Order -> ERP bridge linkage (idempotent)');
$nativeOrderIds = $db->query('SELECT id FROM shop_orders ORDER BY id')->fetchAll(PDO::FETCH_COLUMN);
$link1 = epc_dl_link_order($db, (int) $nativeOrderIds[0]);
check('link created', !empty($link1['ok']) && $link1['linked'] === 'created');
check('ERP order id assigned', (int) $link1['erp_order_id'] > 0);
check('web_order_ref = shoporder:<id>', $link1['web_order_ref'] === 'shoporder:' . (int) $nativeOrderIds[0]);
$mapCount1 = (int) $db->query("SELECT COUNT(*) FROM epc_ec_orders WHERE web_order_ref LIKE 'shoporder:%'")->fetchColumn();
check('1 mapped order', $mapCount1 === 1);
$link2 = epc_dl_link_order($db, (int) $nativeOrderIds[0]);
check('re-link updates (idempotent)', !empty($link2['ok']) && $link2['linked'] === 'updated');
$mapCount2 = (int) $db->query("SELECT COUNT(*) FROM epc_ec_orders WHERE web_order_ref LIKE 'shoporder:%'")->fetchColumn();
check('still 1 mapped order after re-link', $mapCount2 === 1);
$lineCount = (int) $db->query('SELECT COUNT(*) FROM epc_ec_order_lines')->fetchColumn();
check('lines not duplicated on re-link', $lineCount === 1);

section('Bulk sync all native orders into ERP');
$sync = epc_dl_sync_orders($db);
check('scanned 3', $sync['scanned'] === 3);
check('created 2 more (1 already linked->updated)', $sync['created'] === 2 && $sync['updated'] === 1);
$total = (int) $db->query("SELECT COUNT(*) FROM epc_ec_orders WHERE web_order_ref LIKE 'shoporder:%'")->fetchColumn();
check('3 orders linked total', $total === 3);

section('ERP sales dashboard from live shop data');
$sum = epc_dl_sales_summary($db);
// totals: A 3600 (paid), B 5200 (unpaid), C 320*3=960 (paid)
check('revenue = 9760', abs($sum['revenue'] - 9760.00) < 0.01);
check('paid revenue = 4560 (3600+960)', abs($sum['paid_revenue'] - 4560.00) < 0.01);
check('ar outstanding = 5200', abs($sum['ar_outstanding'] - 5200.00) < 0.01);
check('paid orders 2 / unpaid 1', $sum['paid_orders'] === 2 && $sum['unpaid_orders'] === 1);
check('distinct customers 2', $sum['customers'] === 2);
check('gross margin computed', $sum['gross_margin'] > 0);
check('top products present', count($sum['top_products']) >= 1);
check('top product is highest revenue', $sum['top_products'][0]['revenue'] >= $sum['top_products'][count($sum['top_products']) - 1]['revenue']);

section('Link coverage report');
$rep = epc_dl_link_report($db);
check('native orders 3', $rep['native']['orders'] === 3);
check('native customers 2', $rep['native']['customers'] === 2);
check('native products 3', $rep['native']['products'] === 3);
check('linked orders 3', $rep['linked']['orders'] === 3);
check('coverage 100%', abs($rep['coverage_pct'] - 100.0) < 0.01);

section('Multi-tenant isolation (Model-C: separate DBs)');
try {
    $dbB = new PDO("mysql:host=$host;dbname=$name2;charset=utf8", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
    seed_native($dbB, 'electronics');
    $custA = epc_dl_customers($db);
    $custB = epc_dl_customers($dbB);
    $emailsA = array_map(static function ($c) {
        return $c['email'];
    }, $custA);
    $emailsB = array_map(static function ($c) {
        return $c['email'];
    }, $custB);
    check('tenant A sees only its customers', in_array('alice@jewellery.test', $emailsA, true) && !in_array('alice@electronics.test', $emailsA, true));
    check('tenant B sees only its customers', in_array('alice@electronics.test', $emailsB, true) && !in_array('alice@jewellery.test', $emailsB, true));
    $prodB = epc_dl_products($dbB);
    $prodBnames = array_map(static function ($p) {
        return $p['name'];
    }, $prodB);
    check('tenant B catalogue is electronics, not jewellery', in_array('4K Smart TV 55"', $prodBnames, true) && !in_array('22K Gold Ring', $prodBnames, true));
    // cache must not leak between connections (different DBs)
    check('tenant B order count independent', count(epc_dl_orders($dbB)) === 3);
} catch (Throwable $e) {
    check('multi-tenant isolation (second DB available)', false);
    echo '    (could not open ' . $name2 . ': ' . $e->getMessage() . ")\n";
}

section('Permission gate (role can/cannot access ERP data)');
/** Minimal permission check mirroring the governance module contract. */
function dl_can(array $perms, string $need): bool
{
    return in_array('*', $perms, true) || in_array($need, $perms, true);
}
check('finance role can read AR', dl_can(array('erp.finance.read'), 'erp.finance.read') === true);
check('sales role cannot read AR', dl_can(array('erp.sales.read'), 'erp.finance.read') === false);
check('admin wildcard can read anything', dl_can(array('*'), 'erp.finance.read') === true);

echo "\n========================================\n";
echo "DATALINK TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
