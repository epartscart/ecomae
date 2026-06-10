<?php
/**
 * CLI tests for Customs (Dubai/UAE) + Integration/API layer.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_plat_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_customs_integration_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_plat_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

$fin = dirname(__DIR__, 2) . '/content/shop/finance';
require_once $fin . '/epc_erp_customs.php';
require_once $fin . '/epc_erp_integration.php';

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

foreach (array('epc_cust_deposits', 'epc_cust_lines', 'epc_cust_declarations', 'epc_int_api_log', 'epc_int_api_keys', 'epc_int_deliveries', 'epc_int_connectors') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}

section('Dubai Customs — duty rate resolution');
check('default UAE duty 5%', abs(epc_cust_duty_rate('8708', 'AE') - 5.0) < 0.001);
check('cigarettes HS 2402 -> 100%', abs(epc_cust_duty_rate('2402200000', 'AE') - 100.0) < 0.001);
check('medicament HS 3004 -> 0%', abs(epc_cust_duty_rate('30049099', 'AE') - 0.0) < 0.001);

section('Dubai Customs — duty + VAT computation');
// 1 line: 100 units @ 100 = 10,000 goods; freight 500, insurance 200 -> CIF 10,700
// duty 5% of 10,700 = 535 ; VAT 5% of (10,700+535)=11,235 -> 561.75
$calc = epc_cust_compute(
    array('country' => 'AE', 'regime' => 'import_for_home', 'freight' => 500, 'insurance' => 200, 'fx_rate' => 1.0),
    array(array('hs_code' => '8708', 'qty' => 100, 'unit_value' => 100))
);
check('goods value = 10,000', abs($calc['goods_value'] - 10000.0) < 0.01);
check('CIF = 10,700', abs($calc['cif_value'] - 10700.0) < 0.01);
check('duty 5% = 535.00', abs($calc['duty_total'] - 535.0) < 0.01);
check('import VAT 5% on CIF+duty = 561.75', abs($calc['vat_total'] - 561.75) < 0.01);
check('total payable = 1,096.75', abs($calc['total_payable'] - 1096.75) < 0.01);

// Multi-line apportionment reconciles to CIF exactly.
$calc2 = epc_cust_compute(
    array('country' => 'AE', 'freight' => 300, 'insurance' => 0, 'fx_rate' => 1.0),
    array(
        array('hs_code' => '8708', 'qty' => 1, 'unit_value' => 700),
        array('hs_code' => '3004', 'qty' => 1, 'unit_value' => 300), // 0% duty
    )
);
$lineCifSum = array_sum(array_column($calc2['lines'], 'line_cif'));
check('multi-line CIF apportionment reconciles', abs($lineCifSum - $calc2['cif_value']) < 0.01);
check('0% duty line has no duty', abs($calc2['lines'][1]['duty_amount']) < 0.01);

section('Dubai Customs — persist declaration + deposit + payload');
$decl = epc_cust_declaration_save(
    $db,
    array('decl_no' => 'DXB-IMP-1001', 'type' => 'import', 'regime' => 'import_for_home', 'country' => 'AE', 'supplier_id' => 7, 'currency' => 'AED', 'fx_rate' => 1.0, 'freight' => 500, 'insurance' => 200),
    array(array('item_id' => 55, 'hs_code' => '8708', 'description' => 'Brake pads', 'qty' => 100, 'unit_value' => 100))
);
check('declaration persisted with id', $decl['id'] > 0);
check('persisted lines count = 1', (int) $db->query("SELECT COUNT(*) FROM epc_cust_lines WHERE declaration_id=" . $decl['id'])->fetchColumn() === 1);
$depId = epc_cust_deposit_add($db, $decl['id'], array('type' => 'guarantee', 'amount' => 1000, 'reference' => 'BG-1'));
check('deposit recorded as held', (string) $db->query("SELECT status FROM epc_cust_deposits WHERE id=$depId")->fetchColumn() === 'held');
epc_cust_deposit_refund($db, $depId);
check('deposit refunded', (string) $db->query("SELECT status FROM epc_cust_deposits WHERE id=$depId")->fetchColumn() === 'refunded');
$payload = epc_cust_export_payload($db, $decl['id']);
check('payload has declaration number + lines', $payload['declarationNumber'] === 'DXB-IMP-1001' && count($payload['lines']) === 1);
check('payload duty total carried', abs($payload['dutyTotal'] - 535.0) < 0.01);

section('Integration — outbound connector signing + retry');
$connId = epc_int_connector_save($db, array('code' => 'customs_gw', 'name' => 'Dubai Customs GW', 'kind' => 'rest', 'endpoint' => 'https://example.test/decl', 'auth_type' => 'hmac', 'secret' => 's3cr3t'));
check('connector created', $connId > 0);
$enq = epc_int_enqueue($db, 'customs_gw', 'declaration.submit', $payload);
check('delivery enqueued (pending)', $enq['id'] > 0);
$expectSig = hash_hmac('sha256', json_encode($payload), 's3cr3t');
check('payload HMAC signature correct', $enq['signature'] === $expectSig);
// Fail twice -> still pending with backoff; many times -> dead.
$f1 = epc_int_mark_failed($db, $enq['id'], 'timeout', 5, 1000);
check('first failure stays pending with backoff', $f1['status'] === 'pending' && $f1['next_attempt'] > 1000);
for ($i = 0; $i < 4; $i++) {
    $fx = epc_int_mark_failed($db, $enq['id'], 'timeout', 5, 1000);
}
check('exceeding max attempts -> dead', (string) $db->query("SELECT status FROM epc_int_deliveries WHERE id=" . $enq['id'])->fetchColumn() === 'dead');
// New delivery -> deliver OK.
$enq2 = epc_int_enqueue($db, 'customs_gw', 'declaration.submit', array('x' => 1));
epc_int_mark_delivered($db, $enq2['id']);
check('delivery marked delivered', (string) $db->query("SELECT status FROM epc_int_deliveries WHERE id=" . $enq2['id'])->fetchColumn() === 'delivered');
$due = epc_int_due_deliveries($db, 999999999);
check('due-deliveries query excludes delivered/dead', count($due) === 0);

section('Integration — inbound API keys + scopes');
$key = epc_int_api_key_create($db, 'Partner app', array('inventory:read', 'sales:write'));
check('api key issued with secret', strpos($key['api_key'], 'ak_') === 0 && strlen($key['secret']) > 0);
$vOk = epc_int_api_verify($db, $key['api_key'], $key['secret'], 'inventory:read');
check('valid key+secret+scope verifies', $vOk['ok'] === true);
$vScope = epc_int_api_verify($db, $key['api_key'], $key['secret'], 'payroll:read');
check('missing scope rejected', $vScope['ok'] === false && $vScope['reason'] === 'missing_scope');
$vBad = epc_int_api_verify($db, $key['api_key'], 'wrong-secret', 'inventory:read');
check('bad secret rejected', $vBad['ok'] === false && $vBad['reason'] === 'bad_secret');
$vExp = epc_int_api_verify($db, $key['api_key'], $key['secret'], 'inventory:read', 0);
epc_int_api_revoke($db, $key['api_key']);
$vRevoked = epc_int_api_verify($db, $key['api_key'], $key['secret'], 'inventory:read');
check('revoked key rejected', $vRevoked['ok'] === false && $vRevoked['reason'] === 'inactive');
epc_int_api_log($db, $key['api_key'], 'GET', '/api/items', 200);
check('api request logged', (int) $db->query("SELECT COUNT(*) FROM epc_int_api_log")->fetchColumn() === 1);

section('Integration — internal event bus');
$GLOBALS['_evt_hits'] = array();
epc_int_on('invoice.posted', function ($p) {
    $GLOBALS['_evt_hits'][] = 'einvoice:' . $p['id'];
});
epc_int_on('invoice.posted', function ($p) {
    $GLOBALS['_evt_hits'][] = 'treasury:' . $p['id'];
});
epc_int_on('invoice.posted', function ($p) {
    throw new Exception('subscriber boom');
});
$res = epc_int_emit('invoice.posted', array('id' => 42));
check('two subscribers handled + one error isolated', $res['handled'] === 2 && $res['errors'] === 1);
check('subscribers actually ran', in_array('einvoice:42', $GLOBALS['_evt_hits'], true) && in_array('treasury:42', $GLOBALS['_evt_hits'], true));

echo "\n========================================\n";
echo "CUSTOMS + INTEGRATION TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
