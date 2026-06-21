<?php
/**
 * CLI tests for Organization administration: pure calendar arithmetic
 * (working set, is-working-day, count between, add working days over holidays),
 * global address book (parties, addresses with primary handling, contacts),
 * calendars + holidays, summary, multi-company scope.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_orgadmin_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_orgadmin.php';

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

foreach (array('epc_oa_holiday', 'epc_oa_calendar', 'epc_oa_contact', 'epc_oa_address', 'epc_oa_party') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}
epc_oa_ensure_schema($db);

$CO = 1;
$WD = '1,2,3,4,5'; // Mon-Fri

section('Pure: working set');
check('parses Mon-Fri', count(epc_oa_working_set($WD)) === 5);
check('ignores invalid days', count(epc_oa_working_set('1,2,9,0,5')) === 3);

section('Pure: is working day');
// 2026-06-08 is a Monday, 2026-06-13 Saturday, 2026-06-14 Sunday
$mon = strtotime('2026-06-08');
$sat = strtotime('2026-06-13');
check('Monday is working', epc_oa_is_working_day($WD, array(), $mon) === true);
check('Saturday not working', epc_oa_is_working_day($WD, array(), $sat) === false);
check('holiday on Monday not working', epc_oa_is_working_day($WD, array('2026-06-08'), $mon) === false);

section('Pure: working days between');
// Mon 2026-06-08 .. Sun 2026-06-14 => 5 working days (Mon-Fri)
check('full week = 5 working days', epc_oa_working_days_between($WD, array(), strtotime('2026-06-08'), strtotime('2026-06-14')) === 5);
check('with one holiday = 4', epc_oa_working_days_between($WD, array('2026-06-10'), strtotime('2026-06-08'), strtotime('2026-06-14')) === 4);
check('reversed range = 0', epc_oa_working_days_between($WD, array(), strtotime('2026-06-14'), strtotime('2026-06-08')) === 0);

section('Pure: add working days');
// Start Fri 2026-06-12 + 1 working day -> Mon 2026-06-15 (skips weekend)
check('Fri +1 = next Monday', epc_oa_add_working_days($WD, array(), strtotime('2026-06-12'), 1) === '2026-06-15');
// Start Mon 2026-06-08 + 0 -> same Monday (already working)
check('Mon +0 = same Monday', epc_oa_add_working_days($WD, array(), strtotime('2026-06-08'), 0) === '2026-06-08');
// Start Sat +0 -> next Monday
check('Sat +0 = next Monday', epc_oa_add_working_days($WD, array(), strtotime('2026-06-13'), 0) === '2026-06-15');
// Start Mon 06-08 + 3 working days with holiday Wed 06-10 -> Thu, Fri count, so +3 = Fri 06-12? Mon->Tue(1) Wed=holiday Thu(2) Fri(3) = 2026-06-12
check('add over holiday shifts out', epc_oa_add_working_days($WD, array('2026-06-10'), strtotime('2026-06-08'), 3) === '2026-06-12');

section('Address book: parties');
$p1 = epc_oa_party_save($db, $CO, array('party_type' => 'organization', 'name' => 'Acme Trading LLC'));
$p2 = epc_oa_party_save($db, $CO, array('party_type' => 'person', 'name' => 'John Smith'));
check('party saved with id', $p1 > 0);
check('invalid party type rejected', (function () use ($db, $CO) { try { epc_oa_party_save($db, $CO, array('party_type' => 'alien', 'name' => 'x')); return false; } catch (Throwable $e) { return true; } })());
check('name required', (function () use ($db, $CO) { try { epc_oa_party_save($db, $CO, array('name' => '')); return false; } catch (Throwable $e) { return true; } })());
check('parties list = 2', count(epc_oa_parties($db, $CO)) === 2);

section('Addresses (primary handling)');
epc_oa_address_save($db, $p1, array('purpose' => 'business', 'line1' => 'Old St', 'city' => 'Dubai', 'country' => 'AE', 'is_primary' => 1));
epc_oa_address_save($db, $p1, array('purpose' => 'business', 'line1' => 'New St', 'city' => 'Dubai', 'country' => 'AE', 'is_primary' => 1));
$addrs = epc_oa_addresses($db, $p1);
check('2 business addresses', count($addrs) === 2);
$primaries = array_filter($addrs, function ($a) { return (int) $a['is_primary'] === 1; });
check('exactly one primary', count($primaries) === 1);
check('latest is primary', $addrs[0]['line1'] === 'New St');
check('invalid purpose rejected', (function () use ($db, $p1) { try { epc_oa_address_save($db, $p1, array('purpose' => 'spaceship')); return false; } catch (Throwable $e) { return true; } })());

section('Contacts');
epc_oa_contact_save($db, $p1, array('contact_type' => 'email', 'value' => 'a@acme.ae', 'is_primary' => 1));
epc_oa_contact_save($db, $p1, array('contact_type' => 'phone', 'value' => '+97140000000'));
check('2 contacts', count(epc_oa_contacts($db, $p1)) === 2);
check('invalid contact type rejected', (function () use ($db, $p1) { try { epc_oa_contact_save($db, $p1, array('contact_type' => 'telepathy', 'value' => 'x')); return false; } catch (Throwable $e) { return true; } })());

section('Calendars + holidays');
$c1 = epc_oa_calendar_save($db, $CO, array('code' => 'UAE_STD', 'name' => 'UAE standard', 'working_days' => '1,2,3,4,5'));
check('calendar saved', $c1 > 0);
check('empty working days rejected', (function () use ($db, $CO) { try { epc_oa_calendar_save($db, $CO, array('code' => 'X', 'working_days' => '9,0')); return false; } catch (Throwable $e) { return true; } })());
epc_oa_holiday_add($db, $c1, '2026-12-02', 'National Day');
epc_oa_holiday_add($db, $c1, '2026-12-03', 'National Day 2');
check('2 holidays', count(epc_oa_holidays($db, $c1)) === 2);
check('bad holiday date rejected', (function () use ($db, $c1) { try { epc_oa_holiday_add($db, $c1, '02-12-2026', 'x'); return false; } catch (Throwable $e) { return true; } })());
check('calendars list shows holiday_count', epc_oa_calendars($db, $CO)[0]['holiday_count'] === 2);
check('holiday dates helper', in_array('2026-12-02', epc_oa_holiday_dates($db, $c1), true));

section('Summary + multi-company');
epc_oa_party_save($db, 2, array('party_type' => 'organization', 'name' => 'Other Co'));
check('company 2 isolated (1 party)', epc_oa_summary($db, 2)['parties'] === 1);
$sum = epc_oa_summary($db, $CO);
check('summary parties = 2', $sum['parties'] === 2);
check('summary addresses = 2', $sum['addresses'] === 2);
check('summary calendars = 1', $sum['calendars'] === 1);
check('summary holidays = 2', $sum['holidays'] === 2);

echo "\n========================================\n";
echo 'ORGADMIN TESTS: ' . $pass_count . ' passed, ' . $fail_count . " failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
