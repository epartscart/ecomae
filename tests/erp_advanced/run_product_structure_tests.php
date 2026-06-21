<?php
/**
 * CLI integration tests for the D365-style Product structure layer
 * (product dimensions, variant matrix, dimension groups + variant master).
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_product_structure_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_product_structure.php';

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

foreach (array('epc_erp_prod_variants', 'epc_erp_prod_dim_values', 'epc_erp_prod_dim_groups') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}

section('Dimension catalog');
$cat = epc_erp_prod_dimension_catalog();
check('catalog has product/storage/tracking categories', isset($cat['product'], $cat['storage'], $cat['tracking']));
check('product dims include size, color, style, configuration', isset($cat['product']['dims']['size'], $cat['product']['dims']['color'], $cat['product']['dims']['style'], $cat['product']['dims']['configuration']));
check('product dim keys ordered configuration-first', epc_erp_prod_product_dim_keys()[0] === 'configuration');
check('dimension label resolves', epc_erp_prod_dimension_label('product', 'color') === 'Colour');

section('Variant code token (pure)');
check('token uppercases + strips', epc_erp_variant_code_token('Light Blue') === 'LIGHTB');
check('token caps length at 6', strlen(epc_erp_variant_code_token('Extraordinary')) === 6);
check('empty token falls back to X', epc_erp_variant_code_token('!!!') === 'X');

section('Variant matrix (pure cartesian product)');
$m = epc_erp_variant_matrix(array('size' => array('S', 'M'), 'color' => array('Red', 'Blue')));
check('2x2 matrix has 4 combos', count($m) === 4);
check('combos carry both dims', isset($m[0]['size'], $m[0]['color']));
$m3 = epc_erp_variant_matrix(array('size' => array('S', 'M', 'L'), 'color' => array('Red', 'Blue'), 'style' => array('Slim')));
check('3x2x1 matrix has 6 combos', count($m3) === 6);
check('empty values yield empty matrix', epc_erp_variant_matrix(array('size' => array())) === array());
check('no product dims yields empty matrix', epc_erp_variant_matrix(array()) === array());
// canonical ordering: size before color regardless of input order
$mo = epc_erp_variant_matrix(array('color' => array('Red'), 'size' => array('S')));
$keys = array_keys($mo[0]);
check('matrix preserves canonical dim order (size before color)', $keys === array('size', 'color'));
// duplicate values de-duplicated
$md = epc_erp_variant_matrix(array('size' => array('S', 'S', 'M')));
check('duplicate values de-duplicated', count($md) === 2);

section('Variant SKU + label (pure)');
$combo = array('size' => 'M', 'color' => 'Red');
check('variant sku composes tokens in order', epc_erp_variant_sku('SHIRT', $combo) === 'SHIRT-M-RED');
check('variant label uses slash separator', epc_erp_variant_label($combo) === 'M / Red');
check('empty combo returns base sku', epc_erp_variant_sku('SHIRT', array()) === 'SHIRT');
check('blank base sku falls back to ITEM', epc_erp_variant_sku('', $combo) === 'ITEM-M-RED');

section('Dimension group persistence');
epc_erp_prod_structure_ensure_schema($db);
$def = epc_erp_prod_dim_group_get($db);
check('default group returned when none saved', $def['code'] === 'STD' && $def['product'] === array());
epc_erp_prod_dim_group_save($db, array('code' => 'APP', 'name' => 'Apparel', 'product' => array('size', 'color', 'bogus'), 'storage' => array('warehouse', 'location'), 'tracking' => array('batch')));
$g = epc_erp_prod_dim_group_get($db);
check('saved group code/name persisted', $g['code'] === 'APP' && $g['name'] === 'Apparel');
check('invalid product dim filtered out', $g['product'] === array('size', 'color'));
check('storage dims persisted', $g['storage'] === array('warehouse', 'location'));
check('tracking dims persisted', $g['tracking'] === array('batch'));
// re-save updates the single active group (no duplicate row)
epc_erp_prod_dim_group_save($db, array('code' => 'APP2', 'name' => 'Apparel v2', 'product' => array('size'), 'storage' => array(), 'tracking' => array()));
$cnt = (int) $db->query('SELECT COUNT(*) FROM `epc_erp_prod_dim_groups`')->fetchColumn();
check('re-save updates in place (single group row)', $cnt === 1);

section('Dimension values registry');
check('add size values', epc_erp_prod_dim_value_add($db, 'size', 'S') && epc_erp_prod_dim_value_add($db, 'size', 'M') && epc_erp_prod_dim_value_add($db, 'size', 'L'));
check('add color values', epc_erp_prod_dim_value_add($db, 'color', 'Red') && epc_erp_prod_dim_value_add($db, 'color', 'Blue'));
check('reject non-product dim type', epc_erp_prod_dim_value_add($db, 'warehouse', 'WH1') === false);
check('reject blank value', epc_erp_prod_dim_value_add($db, 'size', '   ') === false);
check('size list has 3 values', count(epc_erp_prod_dim_values_list($db, 'size')) === 3);
// idempotent add
epc_erp_prod_dim_value_add($db, 'size', 'S');
check('duplicate add stays at 3 (idempotent)', count(epc_erp_prod_dim_values_list($db, 'size')) === 3);

section('Variant generation from active group + values');
// Active group currently APP2 (size only). Reset to size+color for a richer matrix.
epc_erp_prod_dim_group_save($db, array('code' => 'APP', 'name' => 'Apparel', 'product' => array('size', 'color'), 'storage' => array('warehouse'), 'tracking' => array()));
$res = epc_erp_prod_variants_generate($db, 101, 'SHIRT');
check('generated 3 sizes x 2 colors = 6 variants', $res['generated'] === 6);
check('variants persisted for item', count($res['variants']) === 6);
$skus = array_map(static function ($v) { return $v['variant_sku']; }, $res['variants']);
check('expected SKU present (SHIRT-M-RED)', in_array('SHIRT-M-RED', $skus, true));
// re-generate is idempotent (no new rows, all existing)
$res2 = epc_erp_prod_variants_generate($db, 101, 'SHIRT');
check('re-generate adds 0 new, 6 existing', $res2['generated'] === 0 && $res2['existing'] === 6);
// adding a new value expands the matrix on next generate
epc_erp_prod_dim_value_add($db, 'color', 'Green');
$res3 = epc_erp_prod_variants_generate($db, 101, 'SHIRT');
check('new color adds 3 variants (one per size)', $res3['generated'] === 3 && count($res3['variants']) === 9);

echo "\n========================================\n";
echo 'PRODUCT STRUCTURE TESTS: ' . $pass_count . ' passed, ' . $fail_count . " failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
