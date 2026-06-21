<?php
/**
 * CLI integration tests for the Advanced WMS engine (locations, license
 * plates, inbound receive + put-away work, outbound waves + pick work, moves,
 * cycle count, mobile RF complete, on-hand by location, multi-company scope).
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_wms_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_wms.php';

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

foreach (array('epc_erp_wms_work', 'epc_erp_wms_waves', 'epc_erp_wms_lp', 'epc_erp_wms_locations') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}
epc_wms_ensure_schema($db);

$CO = 1;

section('Registries');
check('work types include pick + putaway', isset(epc_wms_work_types()['pick'], epc_wms_work_types()['putaway']));
check('location types include receive + ship', isset(epc_wms_location_types()['receive'], epc_wms_location_types()['ship']));

section('Locations');
$recv = epc_wms_location_save($db, array('company_id' => $CO, 'warehouse' => 'MAIN', 'zone' => 'IN', 'code' => 'recv-01', 'type' => 'receive'));
$bin1 = epc_wms_location_save($db, array('company_id' => $CO, 'warehouse' => 'MAIN', 'zone' => 'A', 'code' => 'a-01-01', 'type' => 'pick'));
$bin2 = epc_wms_location_save($db, array('company_id' => $CO, 'warehouse' => 'MAIN', 'zone' => 'A', 'code' => 'a-01-02', 'type' => 'bulk'));
$ship = epc_wms_location_save($db, array('company_id' => $CO, 'warehouse' => 'MAIN', 'zone' => 'OUT', 'code' => 'ship-01', 'type' => 'ship'));
check('four locations created', $recv > 0 && $bin1 > 0 && $bin2 > 0 && $ship > 0);
check('code upper-cased on save', epc_wms_location_get($db, $bin1)['code'] === 'A-01-01');
check('invalid type coerced to pick', epc_wms_location_get($db, epc_wms_location_save($db, array('company_id' => $CO, 'code' => 'x-1', 'type' => 'bogus')))['type'] === 'pick');
check('locations listed scoped to company', count(epc_wms_locations($db, $CO)) === 5);

section('Inbound receive + put-away (RF)');
$rcv = epc_wms_receive($db, $CO, 'WIDGET', 100, $recv, $bin1, 'PO-1001');
check('receive creates LP + putaway work', $rcv['lp_id'] > 0 && $rcv['work_id'] > 0);
check('LP initially at receiving dock', (int) epc_wms_lp_get($db, $rcv['lp_id'])['location_id'] === $recv);
check('on-hand at receiving = 100', abs(epc_wms_on_hand($db, $CO, 'WIDGET', $recv) - 100.0) < 0.001);
epc_wms_work_complete($db, $rcv['work_id']);
check('after putaway LP at pick bin', (int) epc_wms_lp_get($db, $rcv['lp_id'])['location_id'] === $bin1);
check('on-hand now at pick bin', abs(epc_wms_on_hand($db, $CO, 'WIDGET', $bin1) - 100.0) < 0.001);
check('putaway work closed', epc_wms_work_get($db, $rcv['work_id'])['status'] === 'closed');
check('cannot complete closed work twice', (function () use ($db, $rcv) { try { epc_wms_work_complete($db, $rcv['work_id']); return false; } catch (Throwable $e) { return true; } })());

section('Outbound wave + pick (RF)');
$wave = epc_wms_wave_create($db, $CO, 'SO-2001');
check('wave created with number', strpos((string) epc_wms_waves($db, $CO)[0]['wave_no'], 'WAVE') === 0);
$pick1 = epc_wms_wave_add_pick($db, $wave, 'WIDGET', 30, $bin1, $ship);
check('pick work added to wave', $pick1 > 0 && (int) epc_wms_work_get($db, $pick1)['wave_id'] === $wave);
epc_wms_wave_release($db, $wave);
check('wave released', epc_wms_waves($db, $CO)[0]['status'] === 'released');
epc_wms_work_complete($db, $pick1);
check('pick deducts on-hand to 70', abs(epc_wms_on_hand($db, $CO, 'WIDGET', $bin1) - 70.0) < 0.001);
check('wave auto-closes when all work done', epc_wms_waves($db, $CO)[0]['status'] === 'closed');

section('Pick shortage guard');
$wave2 = epc_wms_wave_create($db, $CO, 'SO-2002');
$pickBig = epc_wms_wave_add_pick($db, $wave2, 'WIDGET', 9999, $bin1, $ship);
check('over-pick rejected (insufficient stock)', (function () use ($db, $pickBig) { try { epc_wms_work_complete($db, $pickBig); return false; } catch (Throwable $e) { return true; } })());

section('Move + cycle count');
$mv = epc_wms_work_create($db, array('company_id' => $CO, 'work_type' => 'move', 'item' => 'WIDGET', 'qty' => 70, 'lp_id' => $rcv['lp_id'], 'to_location_id' => $bin2, 'status' => 'open'));
epc_wms_work_complete($db, $mv);
check('move relocates LP to bulk', (int) epc_wms_lp_get($db, $rcv['lp_id'])['location_id'] === $bin2);
$cnt = epc_wms_work_create($db, array('company_id' => $CO, 'work_type' => 'count', 'item' => 'WIDGET', 'qty' => 65, 'lp_id' => $rcv['lp_id'], 'status' => 'open'));
epc_wms_work_complete($db, $cnt);
check('cycle count sets LP qty to 65', abs((float) epc_wms_lp_get($db, $rcv['lp_id'])['qty'] - 65.0) < 0.001);

section('Assignment + work pool');
$openWork = epc_wms_work_list($db, $CO, 'open');
check('open work pool excludes closed', count(array_filter($openWork, static function ($w) { return $w['status'] === 'closed'; })) === 0);
$anyOpen = epc_wms_wave_add_pick($db, epc_wms_wave_create($db, $CO, 'SO-3001'), 'WIDGET', 5, $bin2, $ship);
epc_wms_work_assign($db, $anyOpen, 'picker01');
check('assign sets user + in_progress', epc_wms_work_get($db, $anyOpen)['assigned_to'] === 'picker01' && epc_wms_work_get($db, $anyOpen)['status'] === 'in_progress');

section('Location delete guard + multi-company');
check('cannot delete location holding stock', (function () use ($db, $bin2) { try { epc_wms_location_delete($db, $bin2); return false; } catch (Throwable $e) { return true; } })());
epc_wms_location_save($db, array('company_id' => 2, 'warehouse' => 'WH2', 'code' => 'b-01', 'type' => 'pick'));
check('company 2 sees only its own location', count(epc_wms_locations($db, 2)) === 1);

section('Summary');
$sum = epc_wms_summary($db, $CO);
check('summary reports locations', $sum['locations'] >= 5);
check('summary reports active LPs', $sum['license_plates'] >= 1);
check('summary reports open work', $sum['open_work'] >= 1);

echo "\n========================================\n";
echo 'WMS TESTS: ' . $pass_count . ' passed, ' . $fail_count . " failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
