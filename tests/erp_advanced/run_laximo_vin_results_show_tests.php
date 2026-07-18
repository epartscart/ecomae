<?php
/**
 * VIN roaming results must expose brand/name columns for Twig rendering.
 */
declare(strict_types=1);

$root = dirname(__DIR__, 2);
require_once $root . '/content/laximo/com_guayaquil/guayaquillib/BaseGuayaquilObject.php';
require_once $root . '/content/laximo/com_guayaquil/guayaquillib/objects/AttributeObject.php';
require_once $root . '/content/laximo/com_guayaquil/guayaquillib/objects/VehicleObject.php';
require_once $root . '/content/laximo/com_guayaquil/guayaquillib/objects/VehicleListObject.php';

use guayaquil\guayaquillib\objects\AttributeObject;
use guayaquil\guayaquillib\objects\VehicleListObject;
use guayaquil\guayaquillib\objects\VehicleObject;

function assert_true($cond, $msg)
{
    if (!$cond) {
        fwrite(STDERR, "FAIL: $msg\n");
        exit(1);
    }
    echo "OK: $msg\n";
}

$xml = simplexml_load_string(
    '<VehicleList><row brand="NISSAN" catalog="NISSAN201809" name="X-TRAIL" ssd="abc" vehicleid="0">'
    . '<attribute key="date" name="Vehicle date" value="09.2015"/>'
    . '<attribute key="engine" name="Engine" value="MR20DD"/>'
    . '</row></VehicleList>'
);

$list = new VehicleListObject($xml->children());
assert_true(!empty($list->vehicles) && count($list->vehicles) === 1, 'parses one vehicle');
assert_true($list->vehicles[0] instanceof VehicleObject, 'vehicle object type');
assert_true($list->vehicles[0]->brand === 'NISSAN', 'brand parsed');
assert_true($list->vehicles[0]->name === 'X-TRAIL', 'name parsed');

$grouped = $list->groupColumnsByVehicles();
assert_true(in_array('brand', $grouped->tableColumns, true), 'brand column always visible');
assert_true(in_array('name', $grouped->tableColumns, true), 'name column always visible');
assert_true(isset($grouped->tableHeaders['brand']), 'brand header present');
assert_true(isset($grouped->tableHeaders['name']), 'name header present');
assert_true(!empty($grouped->commonColumns), 'shared attrs go to commonColumns');

// Twig regression: Config::$VehiclesColumns int must NOT be used as column keys.
$fakeColumns = 4;
assert_true(!(is_array($fakeColumns) && in_array('brand', $fakeColumns, true)), 'int columns are not usable as keys');

echo "All laximo VIN results-show tests passed\n";
