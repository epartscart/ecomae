<?php
/**
 * CLI integration tests for the fixed-asset master (field depth + depreciation).
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_fixed_assets_tests.php
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
require_once $fin . '/epc_erp_fixed_assets.php';

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
epc_erp_fa_ensure_schema($db);

/* ---------------------------------------------------------------- schema */
section('Schema');
foreach (array('epc_erp_fa_assets', 'epc_erp_fa_categories', 'epc_erp_fa_depreciation_runs') as $t) {
    $exists = $db->query("SHOW TABLES LIKE " . $db->quote($t))->fetchColumn();
    check("table $t exists", (bool) $exists);
}
foreach (array(
    'asset_group', 'legal_entity_id', 'business_unit_id', 'asset_type', 'major_type',
    'property_type', 'quantity', 'placed_in_service_date', 'disposal_date',
    'depreciation_convention', 'posting_profile', 'barcode', 'make', 'model',
    'manufacturer', 'supplier_vendor_id', 'purchase_invoice_ref', 'insurance_policy_no',
    'insured_value', 'warranty_expiry', 'custodian', 'gl_asset_account',
    'gl_depreciation_account', 'gl_accum_depr_account',
) as $col) {
    check("column $col present", epc_erp_fa_has_column($db, $col));
}

/* ------------------------------------------------- create with full depth */
section('Asset master — extended field depth');
$aid = (int) epc_erp_fa_create_asset($db, array(
    'asset_code' => 'FA-D365-1', 'name' => 'Toyota Hilux', 'category_id' => 0,
    'acquisition_date' => '2025-01-15', 'cost' => 120000, 'salvage_value' => 20000,
    'useful_life_months' => 60, 'depreciation_method' => 'straight_line',
    'asset_group' => 'VEH', 'asset_type' => 'tangible', 'major_type' => 'Vehicle',
    'property_type' => 'Owned', 'legal_entity_id' => 3, 'business_unit_id' => 2,
    'quantity' => 1, 'placed_in_service_date' => '2025-01-20', 'disposal_date' => '',
    'depreciation_convention' => 'full_month', 'posting_profile' => 'CURRENT',
    'location' => 'Dubai HQ', 'tracking_id' => 'TRK-1', 'barcode' => 'BC-99',
    'serial_no' => 'VIN-12345', 'make' => 'Toyota', 'model' => 'Hilux 2025',
    'manufacturer' => 'Toyota Motor', 'supplier_vendor_id' => 77,
    'purchase_invoice_ref' => 'PINV-555', 'insurance_policy_no' => 'POL-888',
    'insured_value' => 130000, 'warranty_expiry' => '2028-01-15', 'custodian' => 'Ahmed K',
    'gl_asset_account' => '1500', 'gl_depreciation_account' => '6500', 'gl_accum_depr_account' => '1510',
));
check('asset created', $aid > 0);
$a = $db->query("SELECT * FROM epc_erp_fa_assets WHERE id=$aid")->fetch(PDO::FETCH_ASSOC);
check('asset_group persisted', $a['asset_group'] === 'VEH');
check('asset_type persisted', $a['asset_type'] === 'tangible');
check('major_type persisted', $a['major_type'] === 'Vehicle');
check('property_type persisted', $a['property_type'] === 'Owned');
check('legal_entity_id persisted', (int) $a['legal_entity_id'] === 3);
check('business_unit_id persisted', (int) $a['business_unit_id'] === 2);
check('quantity persisted', abs((float) $a['quantity'] - 1.0) < 0.001);
check('placed_in_service_date persisted', $a['placed_in_service_date'] === '2025-01-20');
check('depreciation_convention persisted', $a['depreciation_convention'] === 'full_month');
check('posting_profile persisted', $a['posting_profile'] === 'CURRENT');
check('barcode persisted', $a['barcode'] === 'BC-99');
check('serial_no persisted', $a['serial_no'] === 'VIN-12345');
check('make persisted', $a['make'] === 'Toyota');
check('model persisted', $a['model'] === 'Hilux 2025');
check('manufacturer persisted', $a['manufacturer'] === 'Toyota Motor');
check('supplier_vendor_id persisted', (int) $a['supplier_vendor_id'] === 77);
check('purchase_invoice_ref persisted', $a['purchase_invoice_ref'] === 'PINV-555');
check('insurance_policy_no persisted', $a['insurance_policy_no'] === 'POL-888');
check('insured_value persisted', abs((float) $a['insured_value'] - 130000) < 0.01);
check('warranty_expiry persisted', $a['warranty_expiry'] === '2028-01-15');
check('custodian persisted', $a['custodian'] === 'Ahmed K');
check('gl_asset_account persisted', $a['gl_asset_account'] === '1500');
check('gl_depreciation_account persisted', $a['gl_depreciation_account'] === '6500');
check('gl_accum_depr_account persisted', $a['gl_accum_depr_account'] === '1510');
check('book value = cost - opening accum (120000)', abs((float) $a['book_value'] - 120000) < 0.01);

/* ------------------------------------------------- minimal create still ok */
section('Backward compatibility');
$aid2 = (int) epc_erp_fa_create_asset($db, array(
    'asset_code' => 'FA-MIN-1', 'name' => 'Office Desk', 'cost' => 1500,
));
$a2 = $db->query("SELECT * FROM epc_erp_fa_assets WHERE id=$aid2")->fetch(PDO::FETCH_ASSOC);
check('minimal asset create still works', $aid2 > 0 && $a2['asset_type'] === 'tangible');
check('minimal asset defaults quantity to 1', abs((float) $a2['quantity'] - 1.0) < 0.001);

/* ---------------------------------------------------------------- summary */
echo "\n========================================\n";
echo "FIXED ASSETS TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
