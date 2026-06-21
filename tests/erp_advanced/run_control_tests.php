<?php
/**
 * CLI tests for the three-tier control layer (user / admin / provider).
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_plat_test DB_NAME2=erp_t2_test \
 *     DB_USER=erp DB_PASS=erp php tests/erp_advanced/run_control_tests.php
 *
 * Uses two SEPARATE tenant databases to prove the provider fleet view never
 * co-mingles tenant data.
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_plat_test';
$name2 = getenv('DB_NAME2') ?: 'erp_t2_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_modules.php';
require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_governance.php';
require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_control.php';

$db = new PDO("mysql:host=$host;dbname=$name;charset=utf8", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
$db2 = new PDO("mysql:host=$host;dbname=$name2;charset=utf8", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));

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

foreach (array($db, $db2) as $conn) {
    foreach (array('epc_ctl_user_prefs', 'epc_ctl_tenant', 'epc_gov_roles', 'epc_gov_user_roles', 'epc_ntf_notifications', 'epc_mod_entitlements') as $t) {
        try {
            $conn->exec("DROP TABLE IF EXISTS `$t`");
        } catch (Throwable $e) {
        }
    }
    epc_mod_ensure_schema($conn);
    epc_gov_ensure_schema($conn);
    epc_ctl_ensure_schema($conn);
}

section('USER controls: preferences');
$saved = epc_ctl_user_prefs_save($db, 7, array('lang' => 'ar', 'branch_id' => 3, 'warehouse_id' => 11, 'landing' => 'sales_dashboard', 'page_size' => 50, 'extra' => array('theme' => 'dark')));
check('prefs saved with lang ar', $saved['lang'] === 'ar');
$got = epc_ctl_user_prefs_get($db, 7);
check('prefs read back: branch 3, wh 11', $got['branch_id'] === 3 && $got['warehouse_id'] === 11);
check('landing + page_size persisted', $got['landing'] === 'sales_dashboard' && $got['page_size'] === 50);
check('extra json round-trips (theme=dark)', ($got['extra']['theme'] ?? '') === 'dark');
$merged = epc_ctl_user_prefs_save($db, 7, array('branch_id' => 9)); // partial update
check('partial save merges (lang kept ar, branch->9)', $merged['lang'] === 'ar' && $merged['branch_id'] === 9);
$def = epc_ctl_user_prefs_get($db, 999);
check('unknown user gets defaults (page_size 25)', $def['page_size'] === 25 && $def['branch_id'] === 0);
$badSize = epc_ctl_user_prefs_save($db, 8, array('page_size' => 0));
check('invalid page_size falls back to 25', $badSize['page_size'] === 25);

section('USER controls: console (perms + unread + prefs)');
$adminRole = epc_gov_role_save($db, 'ADMIN', 'Administrator', array('*'), true);
$salesRole = epc_gov_role_save($db, 'SALES', 'Sales', array('crm.view', 'sales.create'), false);
epc_gov_assign_role($db, 7, $salesRole);
epc_ntf_push($db, 7, array('title' => 'Welcome', 'body' => 'Hi'));
$console = epc_ctl_user_console($db, 7);
check('console returns sales permissions', in_array('crm.view', $console['permissions'], true));
check('console unread = 1', $console['unread'] === 1);
check('console includes prefs (branch 9)', $console['prefs']['branch_id'] === 9);

section('ADMIN controls: tenant console (this tenant only)');
epc_mod_apply_bundle($db, 'payroll_only', true);
$admin = epc_ctl_admin_console($db);
check('admin sees only enabled modules (payroll_only -> few)', $admin['modules_enabled'] > 0 && $admin['modules_enabled'] < $admin['modules_total']);
check('admin sees role count (>=2)', $admin['roles'] >= 2);
check('admin module list contains hr', in_array('hr', $admin['modules_list'], true));
check('admin module list excludes procurement', !in_array('procurement', $admin['modules_list'], true));

section('PROVIDER controls: provision / suspend / expiry');
$prov = epc_ctl_provision_tenant($db, array('tenant_code' => 'acme', 'display_name' => 'ACME Co', 'plan' => 'finance_suite', 'expires_at' => time() + 86400, 'note' => 'annual'));
check('provision applies plan finance_suite', $prov['plan'] === 'finance_suite' && count($prov['modules']) > 0);
$t = epc_ctl_tenant_get($db);
check('tenant record stored (code acme, active)', $t['tenant_code'] === 'acme' && $t['status'] === 'active');
check('provisioning is exclusive: gl enabled, hr now off', epc_mod_enabled($db, 'gl') && !epc_mod_enabled($db, 'hr'));
check('tenant active gate true (not expired)', epc_ctl_tenant_active($db) === true);
epc_ctl_set_tenant_status($db, 'suspended');
check('suspended -> tenant_active false', epc_ctl_tenant_active($db) === false);
check('suspend keeps data (modules still enabled)', epc_mod_enabled($db, 'gl') === true);
epc_ctl_set_tenant_status($db, 'active');
check('reactivate -> tenant_active true again', epc_ctl_tenant_active($db) === true);
// expiry gate
epc_ctl_provision_tenant($db, array('tenant_code' => 'acme', 'plan' => 'finance_suite', 'expires_at' => time() - 10));
check('expired subscription -> tenant_active false', epc_ctl_tenant_active($db) === false);

section('PROVIDER controls: fleet overview across SEPARATE tenant DBs');
epc_ctl_provision_tenant($db, array('tenant_code' => 'acme', 'plan' => 'finance_suite', 'expires_at' => time() + 86400));
epc_ctl_provision_tenant($db2, array('tenant_code' => 'globex', 'display_name' => 'Globex', 'plan' => 'payroll_only', 'expires_at' => time() + 86400));
epc_ctl_set_tenant_status($db2, 'suspended');
$fleet = epc_ctl_fleet_overview(array('acme' => $db, 'globex' => $db2));
check('fleet lists 2 tenants', $fleet['count'] === 2);
check('fleet active=1, suspended=1', $fleet['active'] === 1 && $fleet['suspended'] === 1);
$codes = array_map(function ($x) {
    return $x['tenant_code'] ?? '';
}, $fleet['tenants']);
check('fleet shows both distinct tenant codes', in_array('acme', $codes, true) && in_array('globex', $codes, true));
// Isolation proof: globex is in its own DB; acme's DB cannot see globex's record.
$acmeOnly = epc_ctl_tenant_get($db);
check('acme DB only knows acme (isolation)', $acmeOnly['tenant_code'] === 'acme');
$globexOnly = epc_ctl_tenant_get($db2);
check('globex DB only knows globex (isolation)', $globexOnly['tenant_code'] === 'globex');

echo "\n========================================\n";
echo "CONTROL-TIER TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
