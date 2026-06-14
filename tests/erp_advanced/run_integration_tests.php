<?php
/**
 * CLI tests for Data & integration framework: data entity registry, pure
 * OData-style parser, live entity query, business events (subscriptions,
 * pure matching, raise + dispatch log), summary and multi-company scope.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_integration_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_integration.php';

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

foreach (array('epc_intg_event_log', 'epc_intg_event_sub', 'epc_intg_entity', 'epc_intg_demo') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}
epc_intg_ensure_schema($db);

$CO = 1;

section('OData parser (pure)');
$allowed = array('id', 'name', 'country', 'amount');
$p = epc_intg_odata_parse(array('$select' => 'name,amount,secret', '$filter' => "country eq 'AE' and amount gt 100", '$orderby' => 'amount desc', '$top' => '5', '$skip' => '2'), $allowed);
check('select drops non-whitelisted (secret)', $p['select'] === array('name', 'amount'));
check('where built with 2 clauses', substr_count($p['where'], '?') === 2 && strpos($p['where'], '`country` =') !== false && strpos($p['where'], '`amount` >') !== false);
check('params parsed (string + numeric)', $p['params'] === array('AE', 100));
check('order by whitelisted desc', $p['order'] === '`amount` DESC');
check('limit/offset parsed', $p['limit'] === 5 && $p['offset'] === 2);
$pc = epc_intg_odata_parse(array('$filter' => "contains(name,'brake')"), $allowed);
check('contains -> LIKE %..%', $pc['where'] === '`name` LIKE ?' && $pc['params'] === array('%brake%'));
$pbad = epc_intg_odata_parse(array('$filter' => "evil eq 'x'", '$orderby' => 'evil'), $allowed);
check('non-whitelisted filter field ignored', $pbad['where'] === '');
check('non-whitelisted orderby field dropped', $pbad['order'] === '');

section('Data entity registry');
$eid = epc_intg_entity_save($db, $CO, array('name' => 'DemoEntity', 'source_table' => 'epc_intg_demo', 'key_field' => 'id', 'fields' => array('id', 'name', 'country', 'amount'), 'enabled' => 1));
check('entity saved with id', $eid > 0);
check('entity name required', (function () use ($db, $CO) { try { epc_intg_entity_save($db, $CO, array('name' => '')); return false; } catch (Throwable $e) { return true; } })());
$ent = epc_intg_entity_get($db, $CO, 'DemoEntity');
check('entity fields decoded', is_array($ent['fields']) && in_array('country', $ent['fields'], true));
check('entities list has 1', count(epc_intg_entities($db, $CO)) === 1);
// re-save updates (idempotent on name)
epc_intg_entity_save($db, $CO, array('name' => 'DemoEntity', 'source_table' => 'epc_intg_demo', 'key_field' => 'id', 'fields' => array('id', 'name', 'country', 'amount'), 'enabled' => 1));
check('entity save idempotent on name', count(epc_intg_entities($db, $CO)) === 1);

section('Live entity query (OData over source table)');
$db->exec("CREATE TABLE IF NOT EXISTS `epc_intg_demo` (`id` int PRIMARY KEY AUTO_INCREMENT, `name` varchar(40), `country` varchar(4), `amount` decimal(10,2))");
$db->exec("DELETE FROM `epc_intg_demo`");
$db->exec("INSERT INTO `epc_intg_demo` (name,country,amount) VALUES ('Alpha','AE',150),('Beta','AE',90),('Gamma','SA',300),('Delta','AE',250)");
$rowsAE = epc_intg_entity_query($db, $CO, 'DemoEntity', array('$filter' => "country eq 'AE' and amount gt 100", '$orderby' => 'amount desc'));
check('query returns AE>100 (2 rows)', count($rowsAE) === 2);
check('query ordered desc (Delta first)', $rowsAE[0]['name'] === 'Delta');
$rowsTop = epc_intg_entity_query($db, $CO, 'DemoEntity', array('$orderby' => 'amount asc', '$top' => '1'));
check('top 1 asc returns Beta (90)', count($rowsTop) === 1 && $rowsTop[0]['name'] === 'Beta');
$rowsSel = epc_intg_entity_query($db, $CO, 'DemoEntity', array('$select' => 'name', '$filter' => "name eq 'Gamma'"));
check('select returns only requested column', $rowsSel === array(array('name' => 'Gamma')));
check('unknown entity throws', (function () use ($db, $CO) { try { epc_intg_entity_query($db, $CO, 'Nope', array()); return false; } catch (Throwable $e) { return true; } })());
// disabled entity
epc_intg_entity_save($db, $CO, array('name' => 'DemoEntity', 'source_table' => 'epc_intg_demo', 'key_field' => 'id', 'fields' => array('id', 'name', 'country', 'amount'), 'enabled' => 0));
check('disabled entity rejected', (function () use ($db, $CO) { try { epc_intg_entity_query($db, $CO, 'DemoEntity', array()); return false; } catch (Throwable $e) { return true; } })());

section('Business events');
check('catalog has known events', in_array('SalesOrderConfirmed', epc_intg_event_catalog(), true));
$s1 = epc_intg_sub_save($db, $CO, 'SalesOrderConfirmed', 'webhook', 'https://hook.example/so', true);
epc_intg_sub_save($db, $CO, 'SalesOrderConfirmed', 'email', 'ops@co.com', true);
epc_intg_sub_save($db, $CO, 'SalesOrderConfirmed', 'webhook', 'https://off.example', false);
check('sub saved with id', $s1 > 0);
check('invalid target type rejected', (function () use ($db, $CO) { try { epc_intg_sub_save($db, $CO, 'X', 'carrier-pigeon', 't'); return false; } catch (Throwable $e) { return true; } })());

section('Event matching (pure)');
$subs = epc_intg_subs($db, $CO);
$matched = epc_intg_event_match($subs, 'SalesOrderConfirmed');
check('matches only active for event (2 of 3)', count($matched) === 2);
check('inactive sub excluded', !in_array('https://off.example', array_column($matched, 'target'), true));
check('non-matching event -> 0', count(epc_intg_event_match($subs, 'PaymentPosted')) === 0);

section('Raise event + dispatch log');
$r = epc_intg_event_raise($db, $CO, 'SalesOrderConfirmed', array('order_id' => 42, 'total' => 999));
check('raise deliveries = 2', $r['deliveries'] === 2);
check('two queued log rows for event', count(array_filter(epc_intg_event_log($db, $CO), function ($l) { return $l['event'] === 'SalesOrderConfirmed' && $l['status'] === 'queued'; })) === 2);
$r2 = epc_intg_event_raise($db, $CO, 'PaymentPosted', array('amt' => 1));
check('no subscriber -> 0 deliveries', $r2['deliveries'] === 0);
check('no_subscriber row logged', count(array_filter(epc_intg_event_log($db, $CO), function ($l) { return $l['status'] === 'no_subscriber'; })) === 1);

section('Summary + multi-company');
epc_intg_entity_save($db, 2, array('name' => 'OtherCoEntity', 'source_table' => 'epc_intg_demo', 'fields' => array('id'), 'enabled' => 1));
check('company 2 isolated (1 entity)', epc_intg_summary($db, 2)['entities'] === 1);
$sum = epc_intg_summary($db, $CO);
check('summary entities = 1', $sum['entities'] === 1);
check('summary active subscriptions = 2', $sum['subscriptions'] === 2);
check('summary queued = 2', $sum['queued'] === 2);

echo "\n========================================\n";
echo 'INTEGRATION TESTS: ' . $pass_count . ' passed, ' . $fail_count . " failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
