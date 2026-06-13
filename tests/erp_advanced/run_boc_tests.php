<?php
/**
 * CLI tests for the BOC kernel (Business Operation Control): branding, the
 * declarative area registry + nav, RBAC, audit log, and cross-tenant fleet
 * aggregation.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_boc_tests.php
 */
declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_governance.php';
require_once dirname(__DIR__, 2) . '/content/general_pages/epc_boc_kernel.php';

$db = new PDO("mysql:host=$host;dbname=$name;charset=utf8", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));

$pass_count = 0;
$fail_count = 0;
function check(string $label, bool $cond, string $extra = ''): void
{
    global $pass_count, $fail_count;
    if ($cond) {
        $pass_count++;
        echo "  PASS  $label\n";
    } else {
        $fail_count++;
        echo "  FAIL  $label" . ($extra !== '' ? "  ($extra)" : '') . "\n";
    }
}
function section(string $t): void { echo "\n== $t ==\n"; }

foreach (array('epc_boc_audit', 'epc_gov_user_roles', 'epc_gov_roles') as $t) {
    try { $db->exec("DROP TABLE IF EXISTS `$t`"); } catch (Throwable $e) {}
}

section('Branding');
$brand = epc_boc_brand();
check('brand name is Business Operation Control', $brand['name'] === 'Business Operation Control');
check('brand short is BOC', $brand['short'] === 'BOC');
check('legacy name retained for continuity', $brand['legacy'] === 'Super CP');

section('Area registry + nav (single source of truth)');
$areas = epc_boc_areas();
check('registry has many areas', count($areas) >= 20, count($areas) . ' areas');
check('command_center area exists & points at its route', isset($areas['command_center']) && strpos($areas['command_center']['path'], 'epc_boc_command_center') !== false);
check('every area has label, group, perm, path, scope', (function () use ($areas) {
    foreach ($areas as $id => $a) {
        if (($a['label'] ?? '') === '' || ($a['group'] ?? '') === '' || ($a['perm'] ?? '') === '' || ($a['path'] ?? '') === '' || empty($a['scope'])) {
            return false;
        }
    }
    return true;
})());
$groups = epc_boc_groups();
check('every area belongs to a known group', (function () use ($areas, $groups) {
    foreach ($areas as $a) { if (!isset($groups[$a['group']])) { return false; } }
    return true;
})());
$nav = epc_boc_nav();
check('nav groups areas in group order', array_keys($nav) === array_keys($groups) || count(array_diff(array_keys($groups), array_keys($nav))) === 0);
$navCount = 0;
foreach ($nav as $g) { $navCount += count($g['areas']); }
check('nav contains every registered area exactly once', $navCount === count($areas), "nav=$navCount areas=" . count($areas));
check('area perm lookup resolves', epc_boc_area_perm('command_center') === $areas['command_center']['perm']);
check('unknown area perm is empty', epc_boc_area_perm('does_not_exist') === '');

section('RBAC over governance');
epc_gov_ensure_schema($db);
$super = epc_gov_role_save($db, 'platform_super', 'Platform Super', array('*'), true);
$opsViewer = epc_gov_role_save($db, 'ops_viewer', 'Ops Viewer', array('boc.ops.view'));
epc_gov_assign_role($db, 10, $super);   // user 10 = full operator
epc_gov_assign_role($db, 11, $opsViewer); // user 11 = ops viewer only
check('user with no roles defaults to allow (legacy operator)', epc_boc_can($db, 999, 'command_center') === true);
check('wildcard operator can access any area', epc_boc_can($db, 10, 'auto_price') === true && epc_boc_can($db, 10, 'governance') === true);
check('ops viewer can see audit log (boc.ops.view)', epc_boc_can($db, 11, 'audit_log') === true);
check('ops viewer CANNOT manage commerce', epc_boc_can($db, 11, 'auto_price') === false);
check('unknown area is denied for a scoped user', epc_boc_can($db, 11, 'does_not_exist') === false);

section('Audit log');
epc_boc_audit_ensure_schema($db);
$id1 = epc_boc_audit_log($db, 10, 'tenant_control', 'reveal_credential', 'epartscart', array('via' => 'test'), 'admin@ecomae.com', '203.0.113.5');
$id2 = epc_boc_audit_log($db, 11, 'governance', 'rule_update', 'password_policy', array(), 'ops@ecomae.com');
check('audit entries get ids', $id1 > 0 && $id2 > 0);
$recent = epc_boc_audit_recent($db, 10);
check('recent returns newest first', count($recent) === 2 && (int) $recent[0]['id'] === $id2);
check('audit captures actor + action + target + ip', $recent[1]['actor'] === 'admin@ecomae.com' && $recent[1]['action'] === 'reveal_credential' && $recent[1]['target'] === 'epartscart' && $recent[1]['ip'] === '203.0.113.5');
$filtered = epc_boc_audit_recent($db, 10, 'governance');
check('audit can filter by area', count($filtered) === 1 && $filtered[0]['area'] === 'governance');

section('Tenant classification + health (pure)');
check('demo flag wins classification', epc_boc_classify_tenant(array('is_demo' => 1, 'industry_code' => 'auto_parts')) === 'demo');
check('erp_standalone -> erp_only', epc_boc_classify_tenant(array('industry_code' => 'erp_standalone')) === 'erp_only');
check('erp_only -> erp_only', epc_boc_classify_tenant(array('industry_code' => 'erp_only')) === 'erp_only');
check('default -> commerce', epc_boc_classify_tenant(array('industry_code' => 'fashion')) === 'commerce');
$hOk = epc_boc_tenant_health(array('status' => 'live', 'db_connect_ok' => true));
check('healthy tenant is green', $hOk['rag'] === 'green');
$hDb = epc_boc_tenant_health(array('status' => 'live', 'db_connect_ok' => false));
check('DB-unreachable tenant is red with reason', $hDb['rag'] === 'red' && in_array('Tenant DB unreachable', $hDb['reasons'], true));
$hDns = epc_boc_tenant_health(array('status' => 'dns_pending', 'db_connect_ok' => true));
check('dns_pending tenant is amber', $hDns['rag'] === 'amber');
$hBlk = epc_boc_tenant_health(array('status' => 'live', 'db_connect_ok' => true, 'access_blocked' => true));
check('access-blocked tenant is red', $hBlk['rag'] === 'red');

section('Fleet summary (pure)');
$fleet = array(
    array('type' => 'commerce', 'health' => 'green'),
    array('type' => 'commerce', 'health' => 'amber'),
    array('type' => 'erp_only', 'health' => 'green'),
    array('type' => 'demo', 'health' => 'red'),
    array('type' => 'demo', 'health' => 'green'),
);
$sum = epc_boc_fleet_summary($fleet);
check('summary totals correct', $sum['total'] === 5);
check('summary by type correct', $sum['by_type']['commerce'] === 2 && $sum['by_type']['erp_only'] === 1 && $sum['by_type']['demo'] === 2);
check('summary by health correct', $sum['by_health']['green'] === 3 && $sum['by_health']['amber'] === 1 && $sum['by_health']['red'] === 1);
check('type labels', epc_boc_type_label('erp_only') === 'ERP-only' && epc_boc_type_label('demo') === 'Demo' && epc_boc_type_label('commerce') === 'Commerce');

echo "\n========================================\n";
echo "BOC TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
