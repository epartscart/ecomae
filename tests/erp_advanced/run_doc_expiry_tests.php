<?php
/**
 * CLI integration tests for the Document Expiry Tracker engine
 * (register CRUD, derived status, pure reminder selection, idempotent
 * dispatch log, insurance source feed, country-driven compliance profile).
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_doc_expiry_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);
define('EPC_DOCX_SUPPRESS_MAIL', true); // don't hit the MTA during tests

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_doc_expiry.php';

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

foreach (array('epc_erp_doc_expiry_reminders', 'epc_erp_doc_expiry') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}
epc_docx_ensure_schema($db);

$DAY = 86400;
$NOW = 1_700_000_000; // fixed clock for deterministic day math

section('Reminder-day parsing (pure)');
check('parses + sorts descending', epc_docx_parse_reminder_days('30,90,7,60') === array(90, 60, 30, 7));
check('de-duplicates + drops non-positive', epc_docx_parse_reminder_days('30, 30, 0, -5, 7') === array(30, 7));
check('empty string yields empty list', epc_docx_parse_reminder_days('') === array());

section('Days-left + status (pure)');
check('days left positive', epc_docx_days_left($NOW + 10 * $DAY, $NOW) === 10);
check('days left negative when past', epc_docx_days_left($NOW - 3 * $DAY, $NOW) === -3);
check('status none when no expiry', epc_docx_status(0, $NOW) === 'none');
check('status valid when far out', epc_docx_status($NOW + 200 * $DAY, $NOW) === 'valid');
check('status expiring within 30d window', epc_docx_status($NOW + 20 * $DAY, $NOW) === 'expiring');
check('status expired when past', epc_docx_status($NOW - 1 * $DAY, $NOW) === 'expired');
check('status expiring honours custom window', epc_docx_status($NOW + 50 * $DAY, $NOW, 60) === 'expiring');

section('Due-threshold selection (pure, idempotent)');
$rd = array(90, 60, 30, 7);
check('no thresholds due far from expiry', epc_docx_due_thresholds($NOW + 120 * $DAY, $rd, $NOW, array()) === array());
check('90 fires at 80 days left', epc_docx_due_thresholds($NOW + 80 * $DAY, $rd, $NOW, array()) === array(90));
check('already-sent threshold excluded', epc_docx_due_thresholds($NOW + 80 * $DAY, $rd, $NOW, array(90)) === array());
check('late add: 90+60 both due, ascending', epc_docx_due_thresholds($NOW + 45 * $DAY, $rd, $NOW, array()) === array(60, 90));
check('expired: all thresholds due', epc_docx_due_thresholds($NOW - 2 * $DAY, $rd, $NOW, array()) === array(7, 30, 60, 90));
check('no expiry yields nothing', epc_docx_due_thresholds(0, $rd, $NOW, array()) === array());

section('Country-driven compliance profile');
$ae = epc_docx_country_profile('AE');
check('AE profile has default reminder days', $ae['default_reminder_days'] === '90,60,30,7');
check('AE profile lists Trade Licence', in_array('Trade Licence', array_column($ae['documents'], 'type'), true));
check('AE profile lists VAT (TRN)', in_array('VAT Registration (TRN)', array_column($ae['documents'], 'type'), true));
$gen = epc_docx_country_profile('ZZ');
check('unknown country falls back generically', $gen['country'] === 'ZZ' && count($gen['documents']) > 0);

section('Register CRUD + derived list');
$id1 = epc_docx_save($db, array('company_id' => 1, 'category' => 'legal', 'doc_type' => 'Trade Licence', 'title' => 'DED Trade Licence', 'ref_no' => 'TL-001', 'owner' => 'HO', 'owner_email' => 'ops@example.com', 'expiry_date' => $NOW + 20 * $DAY, 'reminder_days' => '90,60,30,7'));
$id2 = epc_docx_save($db, array('company_id' => 1, 'category' => 'banking', 'doc_type' => 'Bank Guarantee', 'title' => 'BG ENBD', 'expiry_date' => $NOW - 5 * $DAY, 'owner_email' => 'fin@example.com'));
$id3 = epc_docx_save($db, array('company_id' => 2, 'category' => 'legal', 'doc_type' => 'CR', 'title' => 'Other company', 'expiry_date' => $NOW + 300 * $DAY));
check('three rows saved with ids', $id1 > 0 && $id2 > 0 && $id3 > 0);
$listAll = epc_docx_list($db, 0, '', $NOW);
check('list all returns 3', count($listAll) === 3);
$listCo1 = epc_docx_list($db, 1, '', $NOW);
check('company scope filters to 2', count($listCo1) === 2);
check('invalid category coerced to other', epc_docx_get($db, epc_docx_save($db, array('category' => 'bogus', 'title' => 'x', 'expiry_date' => $NOW)))['category'] === 'other');
$sum = epc_docx_summary($db, 1, $NOW);
check('summary counts expiring+expired for company 1', $sum['total'] === 2 && $sum['expiring'] === 1 && $sum['expired'] === 1);
// list ordered by soonest expiry first
check('list ordered expiry ascending (expired first)', (int) $listCo1[0]['id'] === $id2);
// update
epc_docx_save($db, array('category' => 'legal', 'doc_type' => 'Trade Licence', 'title' => 'DED Trade Licence (renewed)', 'expiry_date' => $NOW + 400 * $DAY, 'owner_email' => 'ops@example.com'), $id1);
check('update changes status to valid', epc_docx_list($db, 0, '', $NOW)[0]['status'] !== 'expired' || epc_docx_get($db, $id1)['title'] === 'DED Trade Licence (renewed)');

section('Reminder dispatch (idempotent)');
// id2 is expired → all thresholds due → one email, all thresholds logged
$run1 = epc_docx_run_reminders($db, 1, $NOW);
check('first run sends for expired + expiring docs', $run1['sent'] >= 1);
$run2 = epc_docx_run_reminders($db, 1, $NOW);
check('second run on same day sends nothing new (idempotent)', $run2['sent'] === 0);
$sent2 = epc_docx_reminders_sent($db, $id2);
check('expired doc logged all 4 thresholds', count($sent2) === 4);

section('Insurance source feed (upsert, no duplicates)');
$fed = epc_docx_upsert_source($db, 'insurance', 555, array('company_id' => 1, 'title' => 'Marine policy', 'ref_no' => 'POL-1', 'expiry_date' => $NOW + 40 * $DAY, 'owner_email' => 'risk@example.com', 'reminder_days' => '60,30'));
check('source feed inserts a row', $fed > 0);
$fed2 = epc_docx_upsert_source($db, 'insurance', 555, array('company_id' => 1, 'title' => 'Marine policy (renewed)', 'ref_no' => 'POL-1', 'expiry_date' => $NOW + 400 * $DAY, 'owner_email' => 'risk@example.com', 'reminder_days' => '60,30'));
check('re-feed updates same row (no duplicate)', $fed2 === $fed);
$row = epc_docx_get($db, $fed);
check('fed row carries source linkage', $row['source_module'] === 'insurance' && (int) $row['source_ref_id'] === 555);
check('fed row updated title', $row['title'] === 'Marine policy (renewed)');
epc_docx_delete_source($db, 'insurance', 555);
check('delete-source removes the fed row', epc_docx_get($db, $fed) === null);

echo "\n========================================\n";
echo 'DOC EXPIRY TESTS: ' . $pass_count . ' passed, ' . $fail_count . " failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
