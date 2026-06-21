<?php
/**
 * CLI tests for Company profile, legal document branding & Statements of
 * Account.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_plat_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_company_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_plat_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_company.php';

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

foreach (array('epc_co_profile', 'epc_co_branch_profile') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}

section('Company profile (logo, TRN, letterhead)');
$saved = epc_co_profile_save($db, array(
    'legal_name' => 'EPARTSCART TRADING LLC',
    'trade_name' => 'EPartsCart',
    'logo_url' => '/uploads/logo.png',
    'address' => 'Dubai, UAE',
    'country' => 'AE',
    'trn' => '100123456700003',
    'tax_label' => 'TRN',
    'trade_license' => 'CN-1234567',
    'bank_details' => 'Emirates NBD, IBAN AE000...',
    'base_currency' => 'AED',
    'invoice_terms' => 'Payment due in 30 days',
));
check('profile saved with TRN', $saved['trn'] === '100123456700003');
$got = epc_co_profile_get($db);
check('profile read back: legal name', $got['legal_name'] === 'EPARTSCART TRADING LLC');
check('logo + trade license persisted', $got['logo_url'] === '/uploads/logo.png' && $got['trade_license'] === 'CN-1234567');
check('base currency AED', $got['base_currency'] === 'AED');
$merged = epc_co_profile_save($db, array('phone' => '+9714000000')); // partial
check('partial save keeps TRN, adds phone', $merged['trn'] === '100123456700003' && $merged['phone'] === '+9714000000');
$empty = epc_co_profile_get(new PDO("mysql:host=$host;dbname=$name;charset=utf8", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)));
check('tax_label defaults to TRN', $got['tax_label'] === 'TRN');

section('Branch overrides on documents');
epc_co_branch_save($db, 5, array('name' => 'Sharjah Branch', 'address' => 'Sharjah, UAE', 'phone' => '+9716111111', 'trn' => '100999888700003'));
$hdrHo = epc_co_document_header($db, 0);
check('HO header uses company address', $hdrHo['address'] === 'Dubai, UAE');
$hdrBr = epc_co_document_header($db, 5);
check('branch header overrides address', $hdrBr['address'] === 'Sharjah, UAE');
check('branch header overrides TRN', $hdrBr['trn'] === '100999888700003');
check('branch name attached', ($hdrBr['branch_name'] ?? '') === 'Sharjah Branch');
$hdrBrNone = epc_co_document_header($db, 77); // no override row
check('unknown branch falls back to HO', $hdrBrNone['address'] === 'Dubai, UAE');

section('Amount in words (tax-invoice legal requirement)');
check('1234.50 AED', epc_co_amount_in_words(1234.50, 'AED') === 'AED One Thousand Two Hundred Thirty Four and Fifty Fils Only');
check('1000000 USD', epc_co_amount_in_words(1000000, 'USD') === 'USD One Million Only');
check('0.75 USD -> cents', epc_co_amount_in_words(0.75, 'USD') === 'USD Zero and Seventy Five Cents Only');
check('5.00 SAR -> Halala minor name not shown when 0', epc_co_amount_in_words(5.00, 'SAR') === 'SAR Five Only');
check('19 -> Nineteen', epc_co_int_to_words(19) === 'Nineteen');
check('rounding 0.999 -> 1', epc_co_amount_in_words(0.999, '') === 'One Only');

section('Customer Statement of Account (forward balance + ageing)');
$now = time();
$day = 86400;
$rows = array(
    array('date' => $now - 100 * $day, 'doc_no' => 'INV-001', 'type' => 'Invoice', 'debit' => 1000, 'credit' => 0),
    array('date' => $now - 50 * $day, 'doc_no' => 'RV-001', 'type' => 'Receipt', 'debit' => 0, 'credit' => 400),
    array('date' => $now - 20 * $day, 'doc_no' => 'INV-002', 'type' => 'Invoice', 'debit' => 700, 'credit' => 0),
    array('date' => $now - 5 * $day, 'doc_no' => 'INV-003', 'type' => 'Invoice', 'debit' => 300, 'credit' => 0),
);
$soa = epc_co_statement($rows, array('opening' => 200.0, 'to' => $now));
check('opening 200', abs($soa['opening'] - 200.0) < 0.01);
check('debit total = 2000', abs($soa['debit_total'] - 2000.0) < 0.01);
check('credit total = 400', abs($soa['credit_total'] - 400.0) < 0.01);
check('closing = 200+2000-400 = 1800', abs($soa['closing'] - 1800.0) < 0.01);
check('running balance on last line = 1800', abs($soa['lines'][count($soa['lines']) - 1]['balance'] - 1800.0) < 0.01);
// ageing: INV-001 net 600 (after RV nets against doc? no - ageing uses raw debit-credit per row): INV-001 1000 @100d ->90plus, RV -400 ignored(<=0), INV-002 700@20d ->d30, INV-003 300@5d ->d30
check('ageing 90+ has 1000 (INV-001)', abs($soa['ageing']['d90plus'] - 1000.0) < 0.01);
check('ageing 30 has 1000 (INV-002+003)', abs($soa['ageing']['d30'] - 1000.0) < 0.01);

section('Date-range statement (pre-range rolls into opening)');
$ranged = epc_co_statement($rows, array('opening' => 0.0, 'from' => $now - 30 * $day, 'to' => $now));
check('pre-range INV-001/RV-001 fold into opening (600)', abs($ranged['opening'] - 600.0) < 0.01);
check('only 2 in-range lines (INV-002, INV-003)', count($ranged['lines']) === 2);
check('closing = 600+1000 = 1600', abs($ranged['closing'] - 1600.0) < 0.01);

section('Vendor Statement (AP) reuses same builder');
$apRows = array(
    array('date' => $now - 40 * $day, 'doc_no' => 'BILL-1', 'type' => 'Bill', 'debit' => 0, 'credit' => 5000),
    array('date' => $now - 10 * $day, 'doc_no' => 'PV-1', 'type' => 'Payment', 'debit' => 3000, 'credit' => 0),
);
$ap = epc_co_statement($apRows, array('opening' => 0.0, 'to' => $now));
check('vendor closing = -2000 (we owe 2000)', abs($ap['closing'] - (-2000.0)) < 0.01);

section('Open-item statement mode (only unsettled docs)');
$openRows = array(
    array('date' => $now - 60 * $day, 'doc_no' => 'INV-A', 'type' => 'Invoice', 'debit' => 500, 'credit' => 0),
    array('date' => $now - 30 * $day, 'doc_no' => 'INV-A', 'type' => 'Receipt', 'debit' => 0, 'credit' => 500), // fully settled
    array('date' => $now - 20 * $day, 'doc_no' => 'INV-B', 'type' => 'Invoice', 'debit' => 800, 'credit' => 0), // open
);
$open = epc_co_statement($openRows, array('mode' => 'open', 'to' => $now));
$openDocs = array_map(function ($l) {
    return $l['doc_no'];
}, $open['lines']);
check('settled INV-A excluded from open items', !in_array('INV-A', $openDocs, true));
check('open INV-B included', in_array('INV-B', $openDocs, true));

echo "\n========================================\n";
echo "COMPANY PROFILE + SOA TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
