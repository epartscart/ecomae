<?php
/**
 * CLI tests for Platform RBAC: pure access-rank/flatten/can, privileges, duties
 * (+ privilege attach), roles (+ duty attach), user assignment, DB-driven
 * effective access resolution, summary and multi-company scope.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_rbac_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_rbac.php';

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

foreach (array('epc_rbac_user_role', 'epc_rbac_role_duty', 'epc_rbac_role', 'epc_rbac_duty_priv', 'epc_rbac_duty', 'epc_rbac_privilege') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}
epc_rbac_ensure_schema($db);

$CO = 1;

section('Pure: access rank');
check('read < update < full', epc_rbac_access_rank('read') < epc_rbac_access_rank('update') && epc_rbac_access_rank('update') < epc_rbac_access_rank('full'));
check('unknown level rank 0', epc_rbac_access_rank('bogus') === 0);

section('Pure: effective flatten + can');
// role 10 -> duties [1,2]; duty1 -> priv [100(read)], duty2 -> priv [101(full),100(update)]
$privMeta = array(
    100 => array('code' => 'cust.view', 'access_level' => 'read'),
    101 => array('code' => 'cust.post', 'access_level' => 'full'),
);
$eff = epc_rbac_effective(array(10), array(10 => array(1, 2)), array(1 => array(100), 2 => array(101, 100)), $privMeta);
check('two privileges resolved', count($eff) === 2);
check('cust.view present', isset($eff['cust.view']));
check('cust.post is full', ($eff['cust.post'] ?? '') === 'full');
check('can read cust.view', epc_rbac_can($eff, 'cust.view', 'read') === true);
check('cannot full cust.view (only read)', epc_rbac_can($eff, 'cust.view', 'full') === false);
check('can full cust.post', epc_rbac_can($eff, 'cust.post', 'full') === true);
check('unknown privilege denied', epc_rbac_can($eff, 'nope', 'read') === false);
$effNone = epc_rbac_effective(array(), array(), array(), $privMeta);
check('no roles -> empty access', count($effNone) === 0);

section('Privileges');
$p1 = epc_rbac_privilege_save($db, $CO, array('code' => 'cust.view', 'name' => 'View customers', 'access_level' => 'read'));
$p2 = epc_rbac_privilege_save($db, $CO, array('code' => 'cust.post', 'name' => 'Post customer', 'access_level' => 'full'));
$p3 = epc_rbac_privilege_save($db, $CO, array('code' => 'inv.view', 'name' => 'View inventory', 'access_level' => 'read'));
check('privilege saved with id', $p1 > 0);
check('invalid access level rejected', (function () use ($db, $CO) { try { epc_rbac_privilege_save($db, $CO, array('code' => 'x', 'access_level' => 'godmode')); return false; } catch (Throwable $e) { return true; } })());
check('code required', (function () use ($db, $CO) { try { epc_rbac_privilege_save($db, $CO, array('code' => '')); return false; } catch (Throwable $e) { return true; } })());
check('privileges list = 3', count(epc_rbac_privileges($db, $CO)) === 3);

section('Duties');
$d1 = epc_rbac_duty_save($db, $CO, array('code' => 'maintain_cust', 'name' => 'Maintain customers'));
epc_rbac_duty_attach_priv($db, $d1, $p1, true);
epc_rbac_duty_attach_priv($db, $d1, $p2, true);
check('duty has 2 privileges', count(epc_rbac_duty_privileges($db, $d1)) === 2);
epc_rbac_duty_attach_priv($db, $d1, $p2, false);
check('detach -> 1 privilege', count(epc_rbac_duty_privileges($db, $d1)) === 1);
epc_rbac_duty_attach_priv($db, $d1, $p2, true);
$d2 = epc_rbac_duty_save($db, $CO, array('code' => 'view_inv', 'name' => 'View inventory'));
epc_rbac_duty_attach_priv($db, $d2, $p3, true);
check('duties list shows priv_count', epc_rbac_duties($db, $CO)[0]['priv_count'] >= 1);

section('Roles + assignment');
$r1 = epc_rbac_role_save($db, $CO, array('code' => 'AR_CLERK', 'name' => 'Accounts receivable clerk'));
epc_rbac_role_attach_duty($db, $r1, $d1, true);
epc_rbac_role_attach_duty($db, $r1, $d2, true);
check('role has 2 duties', count(epc_rbac_role_duties($db, $r1)) === 2);
check('roles list shows duty_count=2', epc_rbac_roles($db, $CO)[0]['duty_count'] === 2);
$USER = 555;
epc_rbac_user_assign_role($db, $CO, $USER, $r1, true);
check('user has role', epc_rbac_user_roles($db, $CO, $USER) === array($r1));
check('idempotent assign', (function () use ($db, $CO, $USER, $r1) { epc_rbac_user_assign_role($db, $CO, $USER, $r1, true); return count(epc_rbac_user_roles($db, $CO, $USER)) === 1; })());

section('DB-driven effective access');
$acc = epc_rbac_user_privileges($db, $CO, $USER);
check('user has cust.view', epc_rbac_can($acc, 'cust.view', 'read'));
check('user has cust.post full', epc_rbac_can($acc, 'cust.post', 'full'));
check('user has inv.view', epc_rbac_can($acc, 'inv.view', 'read'));
check('user cannot post inventory (no priv)', epc_rbac_can($acc, 'inv.post', 'read') === false);
epc_rbac_user_assign_role($db, $CO, $USER, $r1, false);
check('unassign -> no access', count(epc_rbac_user_privileges($db, $CO, $USER)) === 0);

section('Summary + multi-company');
epc_rbac_privilege_save($db, 2, array('code' => 'other.view', 'access_level' => 'read'));
check('company 2 isolated (1 privilege)', epc_rbac_summary($db, 2)['privileges'] === 1);
$sum = epc_rbac_summary($db, $CO);
check('summary privileges = 3', $sum['privileges'] === 3);
check('summary duties = 2', $sum['duties'] === 2);
check('summary roles = 1', $sum['roles'] === 1);

echo "\n========================================\n";
echo 'RBAC TESTS: ' . $pass_count . ' passed, ' . $fail_count . " failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
