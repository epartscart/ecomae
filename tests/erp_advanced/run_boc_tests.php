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
require_once dirname(__DIR__, 2) . '/content/general_pages/epc_boc_advanced.php';

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

foreach (array(
    'epc_boc_audit', 'epc_gov_user_roles', 'epc_gov_roles',
    'epc_erp_suppliers', 'epc_scm_rfq', 'epc_erp_purchases',
    'epc_erp_inv_warehouses', 'epc_erp_inv_items', 'epc_erp_inv_stock', 'epc_scm_item_planning',
) as $t) {
    try { $db->exec("DROP TABLE IF EXISTS `$t`"); } catch (Throwable $e) {}
}

section('Branding');
$brand = epc_boc_brand();
check('brand name is Business Operation System', $brand['name'] === 'Business Operation System');
check('brand short is BOS', $brand['short'] === 'BOS');
check('brand control scope label present', ($brand['control'] ?? '') === 'Control');
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

section('Role-scoped nav (one BOS console, many scopes)');
$navOp = epc_boc_nav_for_user($db, 10);   // full operator
$navOpCount = 0; foreach ($navOp as $g) { $navOpCount += count($g['areas']); }
check('wildcard operator sees every area', $navOpCount === count($areas), "op nav=$navOpCount areas=" . count($areas));
$navView = epc_boc_nav_for_user($db, 11); // ops viewer only
$navViewIds = array();
foreach ($navView as $g) { foreach ($g['areas'] as $id => $a) { $navViewIds[] = $id; } }
check('scoped user sees only permitted areas', in_array('audit_log', $navViewIds, true) && !in_array('auto_price', $navViewIds, true));
check('scoped nav drops empty groups', !isset($navView['commerce']));
check('legacy (no-role) user still gets full nav', (function () use ($db, $areas) {
    $n = epc_boc_nav_for_user($db, 991001);
    $c = 0; foreach ($n as $g) { $c += count($g['areas']); }
    return $c === count($areas);
})());

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

section('Advanced — money formatting (pure)');
check('millions compacted', epc_boc_adv_money(117617958.0) === 'AED 117.6M');
check('thousands compacted', epc_boc_adv_money(38395.0) === 'AED 38.4K');
check('small numbers verbatim', epc_boc_adv_money(742.0) === 'AED 742');
check('trailing zero trimmed', epc_boc_adv_money(2000000.0) === 'AED 2M');
check('currency overrideable', epc_boc_adv_money(5000.0, 'USD') === 'USD 5K');

section('Advanced — vendor rollup (pure)');
$vr = epc_boc_vendor_rollup(array(
    array('site_key' => 'a', 'label' => 'A', 'type' => 'commerce', 'ok' => true, 'vendors' => 12, 'active_vendors' => 10, 'rfq_open' => 3, 'spend' => 1000000.0),
    array('site_key' => 'b', 'label' => 'B', 'type' => 'erp_only', 'ok' => true, 'vendors' => 40, 'active_vendors' => 38, 'rfq_open' => 1, 'spend' => 500000.0),
    array('site_key' => 'c', 'label' => 'C', 'type' => 'commerce', 'ok' => false),
));
check('vendor totals summed', $vr['totals']['vendors'] === 52 && $vr['totals']['active_vendors'] === 48 && $vr['totals']['rfq_open'] === 4);
check('vendor spend summed', abs($vr['totals']['spend'] - 1500000.0) < 0.01);
check('vendor reachable counted', $vr['totals']['reachable'] === 2 && $vr['totals']['tenants'] === 3);
check('vendor rows sorted by vendor count desc', $vr['rows'][0]['site_key'] === 'b' && $vr['rows'][1]['site_key'] === 'a');
check('unreachable vendor row carries ok=false', $vr['rows'][2]['ok'] === false);

section('Advanced — warehouse rollup (pure + RAG)');
$wr = epc_boc_warehouse_rollup(array(
    array('site_key' => 'a', 'type' => 'commerce', 'ok' => true, 'warehouses' => 3, 'skus' => 1200, 'stock_value' => 5000000.0, 'low_stock' => 0, 'out_of_stock' => 0),
    array('site_key' => 'b', 'type' => 'commerce', 'ok' => true, 'warehouses' => 2, 'skus' => 800, 'stock_value' => 2000000.0, 'low_stock' => 5, 'out_of_stock' => 0),
    array('site_key' => 'c', 'type' => 'erp_only', 'ok' => true, 'warehouses' => 1, 'skus' => 50, 'stock_value' => 100000.0, 'low_stock' => 0, 'out_of_stock' => 4),
    array('site_key' => 'd', 'type' => 'commerce', 'ok' => false),
));
check('warehouse totals summed', $wr['totals']['warehouses'] === 6 && $wr['totals']['skus'] === 2050);
check('stock value summed', abs($wr['totals']['stock_value'] - 7100000.0) < 0.01);
check('healthy warehouse green', $wr['rows'][0]['rag'] === 'green');
$byKey = array();
foreach ($wr['rows'] as $row) { $byKey[$row['site_key']] = $row; }
check('low-stock tenant amber', $byKey['b']['rag'] === 'amber');
check('out-of-stock tenant red', $byKey['c']['rag'] === 'red');
check('unreachable tenant red', $byKey['d']['rag'] === 'red');
check('warehouse rows sorted by stock value desc', $wr['rows'][0]['site_key'] === 'a');

section('Advanced — channel rollup (pure)');
$cr = epc_boc_channel_rollup(array(
    array('site_key' => 'a', 'type' => 'commerce', 'ok' => true, 'web' => true, 'pos' => true, 'api' => true, 'marketplaces' => 3, 'arbitrage' => true),
    array('site_key' => 'b', 'type' => 'commerce', 'ok' => true, 'web' => true, 'pos' => false, 'api' => false, 'marketplaces' => 1, 'arbitrage' => false),
    array('site_key' => 'c', 'type' => 'erp_only', 'ok' => true, 'web' => false, 'pos' => false, 'api' => true, 'marketplaces' => 0, 'arbitrage' => false),
));
check('channel count per tenant (web+pos+api+mkt)', $cr['rows'][0]['channels'] === 6);
check('channel totals summed', $cr['totals']['channels'] === 9 && $cr['totals']['web'] === 2 && $cr['totals']['pos'] === 1 && $cr['totals']['api'] === 2 && $cr['totals']['marketplaces'] === 4);
check('arbitrage tenants counted', $cr['totals']['arbitrage'] === 1);
check('channel rows sorted by channel count desc', $cr['rows'][0]['site_key'] === 'a');

section('Advanced — collectors defensive on empty DB');
$vc = epc_boc_collect_vendor($db);
check('vendor collector returns zeros when no ERP tables', $vc['vendors'] === 0 && $vc['rfq_open'] === 0 && $vc['has_erp'] === false);
$wc = epc_boc_collect_warehouse($db);
check('warehouse collector returns zeros when no ERP tables', $wc['warehouses'] === 0 && $wc['stock_value'] === 0.0 && $wc['has_erp'] === false);
check('table-exists is false for missing table', epc_boc_adv_table_exists($db, 'epc_does_not_exist_xyz') === false);

section('Advanced — supply group + areas registered');
$areas2 = epc_boc_areas();
$groups2 = epc_boc_groups();
check('supply group exists', isset($groups2['supply']));
check('vendor/warehouse/channel areas registered in supply group', isset($areas2['vendor_control']) && $areas2['vendor_control']['group'] === 'supply' && $areas2['warehouse_control']['group'] === 'supply' && $areas2['channel_control']['group'] === 'supply');
check('supply areas carry boc.supply.view perm', $areas2['vendor_control']['perm'] === 'boc.supply.view');
check('channel area routes to its page', strpos($areas2['channel_control']['path'], 'epc_boc_channel_control') !== false);

echo "\n========================================\n";
echo "BOC TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
