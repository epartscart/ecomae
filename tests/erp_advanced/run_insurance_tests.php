<?php
/**
 * CLI integration tests for the Insurance Management engine
 * (policy CRUD across all classes, derived status, document store, claims
 * lifecycle, country-driven recommended cover, and the renewal feed into the
 * central Document Expiry Tracker).
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_insurance_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);
define('EPC_DOCX_SUPPRESS_MAIL', true);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_insurance.php';

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

foreach (array('epc_erp_ins_claims', 'epc_erp_ins_documents', 'epc_erp_ins_policies', 'epc_erp_doc_expiry_reminders', 'epc_erp_doc_expiry') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}
epc_ins_ensure_schema($db);

$DAY = 86400;
$NOW = 1_700_000_000;

section('Class + claim-status registries');
$classes = epc_ins_classes();
foreach (array('marine', 'property_air', 'business_interruption', 'public_liability', 'medical', 'fidelity', 'electronic_equipment', 'warehouse', 'assets', 'gpa', 'workmen') as $k) {
    check("class registry includes $k", isset($classes[$k]));
}
check('class label resolves', epc_ins_class_label('gpa') === 'Group Personal Accident (GPA)');
check('claim statuses cover full lifecycle', isset(epc_ins_claim_statuses()['notified'], epc_ins_claim_statuses()['settled'], epc_ins_claim_statuses()['rejected']));

section('Country-driven recommended cover');
$aeRec = epc_ins_country_recommended('AE');
check('AE recommends compulsory medical', in_array('medical', array_column($aeRec, 'class'), true));
check('AE recommends workmen comp', in_array('workmen', array_column($aeRec, 'class'), true));
check('unknown country gives generic recommendation', count(epc_ins_country_recommended('ZZ')) > 0);

section('Policy CRUD + derived status');
$p1 = epc_ins_save($db, array('company_id' => 1, 'policy_no' => 'MAR-001', 'class' => 'marine', 'title' => 'Marine Cargo 2026', 'insurer' => 'Oman Insurance', 'sum_insured' => 1000000, 'premium' => 12000, 'currency' => 'AED', 'start_date' => $NOW - 300 * $DAY, 'expiry_date' => $NOW + 20 * $DAY, 'reminder_days' => '90,60,30,7', 'contact_email' => 'risk@example.com', 'status' => 'active'));
$p2 = epc_ins_save($db, array('company_id' => 1, 'policy_no' => 'MED-001', 'class' => 'medical', 'title' => 'Group Medical', 'insurer' => 'Daman', 'sum_insured' => 500000, 'premium' => 80000, 'expiry_date' => $NOW + 300 * $DAY, 'status' => 'active'));
$p3 = epc_ins_save($db, array('company_id' => 2, 'policy_no' => 'PROP-001', 'class' => 'property_air', 'title' => 'Property AR', 'expiry_date' => $NOW - 10 * $DAY, 'status' => 'active'));
check('three policies saved', $p1 > 0 && $p2 > 0 && $p3 > 0);
check('invalid class coerced to other', epc_ins_get($db, epc_ins_save($db, array('class' => 'bogus', 'policy_no' => 'X')))['class'] === 'other');
$list1 = epc_ins_list($db, 1, '', $NOW);
check('company scope filters policies', count($list1) === 2);
check('derived status marine = expiring (20d)', epc_ins_policy_status(epc_ins_get($db, $p1), $NOW) === 'expiring');
check('derived status medical = valid (300d)', epc_ins_policy_status(epc_ins_get($db, $p2), $NOW) === 'valid');
check('derived status property = expired', epc_ins_policy_status(epc_ins_get($db, $p3), $NOW) === 'expired');
$summary = epc_ins_summary($db, 1, $NOW);
check('summary sums sum-insured for active', abs($summary['sum_insured'] - 1500000.0) < 0.01);
check('summary sums annual premium', abs($summary['annual_premium'] - 92000.0) < 0.01);
check('summary counts expiring', $summary['expiring'] === 1);

section('Renewal feed into Document Expiry Tracker');
$fedRows = epc_docx_list($db, 0, '', $NOW);
$insFed = array_filter($fedRows, static function ($r) { return $r['source_module'] === 'insurance'; });
check('active policies fed into tracker', count($insFed) === 3);
$marineDoc = null;
foreach ($fedRows as $r) {
    if ($r['source_module'] === 'insurance' && (int) $r['source_ref_id'] === $p1) {
        $marineDoc = $r;
    }
}
check('marine policy present in tracker with ref no', $marineDoc !== null && $marineDoc['ref_no'] === 'MAR-001');
check('fed doc carries insurance category', $marineDoc !== null && $marineDoc['category'] === 'insurance');
// cancelling a policy removes it from the tracker
epc_ins_save($db, array('company_id' => 1, 'policy_no' => 'MAR-001', 'class' => 'marine', 'expiry_date' => $NOW + 20 * $DAY, 'status' => 'cancelled'), $p1);
$afterCancel = array_filter(epc_docx_list($db, 0, '', $NOW), static function ($r) use ($p1) { return $r['source_module'] === 'insurance' && (int) $r['source_ref_id'] === $p1; });
check('cancelled policy pulled from tracker', count($afterCancel) === 0);
// reactivate restores feed
epc_ins_save($db, array('company_id' => 1, 'policy_no' => 'MAR-001', 'class' => 'marine', 'expiry_date' => $NOW + 20 * $DAY, 'contact_email' => 'risk@example.com', 'status' => 'active'), $p1);
$afterReactivate = array_filter(epc_docx_list($db, 0, '', $NOW), static function ($r) use ($p1) { return $r['source_module'] === 'insurance' && (int) $r['source_ref_id'] === $p1; });
check('reactivated policy restored to tracker', count($afterReactivate) === 1);

section('Document store');
$d1 = epc_ins_doc_add($db, $p1, array('doc_type' => 'policy', 'title' => 'Policy schedule PDF', 'file_path' => '/up/mar001.pdf'));
$d2 = epc_ins_doc_add($db, $p1, array('doc_type' => 'endorsement', 'title' => 'Endorsement 1'));
check('two documents stored', $d1 > 0 && $d2 > 0);
check('docs listed for policy', count(epc_ins_docs($db, $p1)) === 2);
epc_ins_doc_delete($db, $d2);
check('doc delete works', count(epc_ins_docs($db, $p1)) === 1);

section('Claims lifecycle + tracking');
$c1 = epc_ins_claim_save($db, array('policy_id' => $p1, 'claim_no' => 'CLM-001', 'loss_date' => $NOW - 5 * $DAY, 'notified_date' => $NOW - 4 * $DAY, 'description' => 'Cargo damage', 'claim_amount' => 25000, 'deadline_date' => $NOW + 10 * $DAY, 'status' => 'notified'));
check('claim created', $c1 > 0);
check('claim listed under policy', count(epc_ins_claims($db, $p1)) === 1);
epc_ins_claim_set_status($db, $c1, 'survey');
check('claim advanced to survey', epc_ins_claims($db, $p1)[0]['status'] === 'survey');
$threw = false;
try {
    epc_ins_claim_set_status($db, $c1, 'bogus');
} catch (Throwable $e) {
    $threw = true;
}
check('invalid claim status rejected', $threw);
epc_ins_claim_save($db, array('policy_id' => $p1, 'claim_no' => 'CLM-001', 'claim_amount' => 25000, 'settled_amount' => 22000, 'status' => 'settled'), $c1);
check('claim settled with settled amount', (float) epc_ins_claims($db, $p1)[0]['settled_amount'] === 22000.0);
$openClaims = epc_ins_list($db, 1, '', $NOW);
$marineRow = null;
foreach ($openClaims as $r) {
    if ((int) $r['id'] === $p1) {
        $marineRow = $r;
    }
}
check('settled claim no longer counts as open', $marineRow !== null && (int) $marineRow['open_claims'] === 0);

section('Reminder feed dispatch (idempotent via tracker)');
$run = epc_docx_run_reminders($db, 1, $NOW);
check('reminder run dispatches for expiring fed policies', $run['sent'] >= 1);
$run2 = epc_docx_run_reminders($db, 1, $NOW);
check('reminder run idempotent', $run2['sent'] === 0);

section('Delete cascade');
epc_ins_delete($db, $p1);
check('policy delete removes claims', count(epc_ins_claims($db, $p1)) === 0);
check('policy delete removes docs', count(epc_ins_docs($db, $p1)) === 0);
check('policy delete removes tracker feed', count(array_filter(epc_docx_list($db, 0, '', $NOW), static function ($r) use ($p1) { return (int) $r['source_ref_id'] === $p1 && $r['source_module'] === 'insurance'; })) === 0);

echo "\n========================================\n";
echo 'INSURANCE TESTS: ' . $pass_count . ' passed, ' . $fail_count . " failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
