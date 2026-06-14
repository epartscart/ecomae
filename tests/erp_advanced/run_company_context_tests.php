<?php
/**
 * CLI integration tests for the D365-style company-context layer
 * (multi-company / legal-entity picker + per-company settings).
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_company_context_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_company_context.php';

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

// Clean slate for deterministic assertions (ensure schema exists first).
epc_erp_company_context_ensure($db);
$db->exec('DELETE FROM `epc_org_company_settings`');
$db->exec('DELETE FROM `epc_erp_pm_legal_entities`');

section('Auto-seed + company list');
epc_erp_company_context_ensure($db);
$companies = epc_erp_companies_list($db);
check('auto-seeds a default company when none exist', count($companies) === 1);
check('default company code is MAIN', isset($companies[0]['code']) && $companies[0]['code'] === 'MAIN');

// Add a second legal entity (a different industry / company).
$id2 = epc_erp_pm_save($db, 'epc_erp_pm_legal_entities', array('code' => 'JEWEL', 'name' => 'Jewellery Co', 'currency_code' => 'AED', 'country_code' => 'AE'));
$mainId = (int) $companies[0]['id'];
$companies = epc_erp_companies_list($db);
check('second legal entity registers', count($companies) === 2);
$firstId = (int) $companies[0]['id']; // list is ordered by code, so this is the default

section('Active company resolution');
unset($_GET['company'], $_SESSION);
check('defaults to first company when nothing selected', epc_erp_active_company_id($db) === $firstId);
$otherId = $firstId === $mainId ? $id2 : $mainId;
$_GET['company'] = (string) $otherId;
check('?company= switches active company', epc_erp_active_company_id($db) === $otherId);
$_GET['company'] = '999999';
check('invalid ?company= falls back to default', epc_erp_active_company_id($db) === $firstId);
unset($_GET['company']);
check('active_company() returns the active row', (int) epc_erp_active_company($db)['id'] === $firstId);

section('Per-company settings');
epc_erp_company_setting_set($db, $mainId, 'demo_key', 'alpha');
epc_erp_company_setting_set($db, $id2, 'demo_key', 'beta');
check('settings are isolated per company', epc_erp_company_setting_get($db, $mainId, 'demo_key') === 'alpha' && epc_erp_company_setting_get($db, $id2, 'demo_key') === 'beta');
check('missing setting returns default', epc_erp_company_setting_get($db, $mainId, 'no_such', 'fallback') === 'fallback');
epc_erp_company_setting_set($db, $mainId, 'demo_key', 'alpha2');
check('setting upserts in place', epc_erp_company_setting_get($db, $mainId, 'demo_key') === 'alpha2');

section('Per-company industry pack');
epc_erp_company_industry_pack_set($db, $mainId, 'auto_parts');
epc_erp_company_industry_pack_set($db, $id2, 'jewellery_retail');
check('each company keeps its own industry pack', epc_erp_company_industry_pack($db, $mainId) === 'auto_parts' && epc_erp_company_industry_pack($db, $id2) === 'jewellery_retail');

section('Company switch URL');
$_SERVER['REQUEST_URI'] = '/cp/shop/finance/erp/erp?tab=product_info&pm_view=dimensions';
$u = epc_erp_company_switch_url($id2);
check('switch url preserves path + existing params', strpos($u, 'tab=product_info') !== false && strpos($u, 'pm_view=dimensions') !== false);
check('switch url sets company param', strpos($u, 'company=' . $id2) !== false);
$_SERVER['REQUEST_URI'] = '/cp/shop/finance/erp/erp?tab=dashboard&company=' . $mainId;
$u2 = epc_erp_company_switch_url($id2);
check('switch url replaces existing company param', substr_count($u2, 'company=') === 1 && strpos($u2, 'company=' . $id2) !== false);

echo "\n";
echo 'COMPANY CONTEXT TESTS: ' . $pass_count . ' passed, ' . $fail_count . " failed\n";
exit($fail_count > 0 ? 1 : 0);
