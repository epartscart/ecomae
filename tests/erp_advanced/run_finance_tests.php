<?php
/**
 * CLI integration tests for the Finance suite + e-invoicing.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_fin_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_finance_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_fin_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

$db = new PDO("mysql:host=$host;dbname=$name;charset=utf8", $user, $pass, array(
    PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
));

$fin = dirname(__DIR__, 2) . '/content/shop/finance';
require_once $fin . '/epc_erp_helpers.php';
require_once $fin . '/epc_erp_schema.php';
require_once $fin . '/epc_erp_finance_suite.php';
require_once $fin . '/epc_erp_einvoice.php';

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

epc_erp_ensure_schema($db);

/* --------- minimal bank tables (subset of the live schema, for the test) -- */
$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_cash_bank_entries` (
    `id` int(11) NOT NULL AUTO_INCREMENT,
    `account_id` int(11) NOT NULL,
    `time` int(11) NOT NULL,
    `entry_type` varchar(20) NOT NULL DEFAULT 'receipt',
    `direction` tinyint(1) NOT NULL,
    `amount` decimal(14,2) NOT NULL DEFAULT 0.00,
    `reference` varchar(128) DEFAULT NULL,
    `note` text,
    `active` tinyint(1) NOT NULL DEFAULT 1,
    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8");
$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_bank_statement_lines` (
    `id` int(11) NOT NULL AUTO_INCREMENT,
    `account_id` int(11) NOT NULL,
    `line_date` int(11) NOT NULL,
    `description` varchar(255) DEFAULT NULL,
    `reference` varchar(128) DEFAULT NULL,
    `amount` decimal(14,2) NOT NULL DEFAULT 0.00,
    `direction` tinyint(1) NOT NULL,
    `matched_entry_id` int(11) NOT NULL DEFAULT 0,
    `time_created` int(11) NOT NULL DEFAULT 0,
    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8");

$acct = 1;
$now = time();
// Cash/bank entries.
$db->exec("INSERT INTO `epc_erp_cash_bank_entries` (`account_id`,`time`,`direction`,`amount`,`reference`,`note`,`active`) VALUES
    ($acct, " . ($now - 2 * 86400) . ", 1, 1500.00, 'INV-1001', 'Payment ACME', 1),
    ($acct, " . ($now - 1 * 86400) . ", 0, 320.50, 'BILL-77', 'Supplier XYZ', 1),
    ($acct, " . ($now - 10 * 86400) . ", 1, 999.99, 'INV-1002', 'Globex', 1)");
// Statement lines: line1 matches entry1 exactly (amount+dir+ref+date),
// line2 matches entry2, line3 has no counterpart.
$db->exec("INSERT INTO `epc_erp_bank_statement_lines` (`account_id`,`line_date`,`description`,`reference`,`amount`,`direction`,`matched_entry_id`) VALUES
    ($acct, " . ($now - 2 * 86400) . ", 'ACME deposit', 'INV-1001', 1500.00, 1, 0),
    ($acct, " . ($now - 1 * 86400) . ", 'XYZ payment', 'BILL-77', 320.50, 0, 0),
    ($acct, " . $now . ", 'Unknown credit', 'MISC', 4242.00, 1, 0)");

section('AI bank reconciliation');
$res = epc_fin_bank_auto_reconcile($db, $acct, 0.8, 5, true);
check('scanned 3 statement lines', $res['lines_scanned'] === 3);
check('auto-matched 2 lines', $res['matched_count'] === 2);
check('1 line left unmatched', $res['unmatched_count'] === 1);
$matchedAmounts = array_map(static function ($m) {
    return $m['amount'];
}, $res['matched']);
check('matched the 1500.00 credit', in_array(1500.00, $matchedAmounts, true));
check('matched the 320.50 debit', in_array(320.50, $matchedAmounts, true));
$summary = epc_fin_bank_recon_summary($db, $acct);
check('summary match rate ~66.7%', abs($summary['match_rate'] - 66.7) < 0.2);
check('summary unmatched net = 4242.00', abs($summary['unmatched_net'] - 4242.00) < 0.01);
// Idempotency: re-running matches nothing new.
$res2 = epc_fin_bank_auto_reconcile($db, $acct, 0.8, 5, true);
check('re-run matches nothing new (idempotent)', $res2['matched_count'] === 0);
// Wrong direction must never match.
check('match score 0 when direction differs', epc_fin_bank_match_score(
    array('direction' => 1, 'amount' => 100, 'line_date' => $now, 'reference' => 'X', 'description' => ''),
    array('direction' => 0, 'amount' => 100, 'time' => $now, 'reference' => 'X', 'note' => ''),
    5
) === 0.0);

section('VAT / GST return');
$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_sales_orders` (
    `id` int(11) NOT NULL AUTO_INCREMENT,
    `customer_user_id` int(11) NOT NULL DEFAULT 0,
    `amount_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
    `vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
    `total_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
    `status` enum('draft','confirmed','invoiced','cancelled') NOT NULL DEFAULT 'draft',
    `time_created` int(11) NOT NULL DEFAULT 0,
    `time_updated` int(11) NOT NULL DEFAULT 0,
    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8");
// Seed sales orders + purchases.
$db->exec("INSERT INTO `epc_erp_sales_orders` (`customer_user_id`,`amount_ex_vat`,`vat_amount`,`total_amount`,`status`,`time_created`,`time_updated`) VALUES
    (1, 10000.00, 500.00, 10500.00, 'confirmed', " . ($now - 5 * 86400) . ", $now),
    (2, 4000.00, 200.00, 4200.00, 'invoiced', " . ($now - 3 * 86400) . ", $now),
    (3, 999.00, 49.95, 1048.95, 'draft', " . ($now - 2 * 86400) . ", $now)");
$db->exec("INSERT INTO `epc_erp_purchases` (`supplier_id`,`purchase_date`,`amount_ex_vat`,`vat_amount`,`total_amount`,`status`) VALUES
    (1, " . ($now - 4 * 86400) . ", 6000.00, 300.00, 6300.00, 'confirmed'),
    (2, " . ($now - 1 * 86400) . ", 2000.00, 100.00, 2100.00, 'paid')");
$vat = epc_fin_vat_return($db, $now - 30 * 86400, $now + 86400);
check('output tax = 700 (confirmed+invoiced only, draft excluded)', abs($vat['output']['tax'] - 700.00) < 0.01);
check('input tax = 400', abs($vat['input']['tax'] - 400.00) < 0.01);
check('net VAT payable = 300', abs($vat['net_tax_payable'] - 300.00) < 0.01);
check('position = payable', $vat['position'] === 'payable');
check('box mapping present', isset($vat['boxes']['net_tax_due']));

section('Corporate tax');
$ct = epc_fin_corporate_tax($db, $now - 30 * 86400, $now + 86400, 9.0, 0.0);
check('CIT computed at 9% over 0 threshold', $ct['rate_percent'] === 9.0);
check('taxable profit >= 0', $ct['taxable_profit'] >= 0.0);
$ctHi = epc_fin_corporate_tax($db, $now - 30 * 86400, $now + 86400, 9.0, 1000000.0);
check('no CIT below high threshold', $ctHi['corporate_tax'] === 0.0);

section('Reporting center + narrative');
$rep = epc_fin_report_center($db, $now - 30 * 86400, $now + 86400);
check('report has vat_return section', isset($rep['sections']['vat_return']));
check('report has corporate_tax section', isset($rep['sections']['corporate_tax']));
check('narrative is non-empty', is_array($rep['narrative']) && count($rep['narrative']) > 0);
$joined = implode(' ', $rep['narrative']);
check('narrative mentions VAT', stripos($joined, 'VAT') !== false || stripos($joined, 'tax') !== false);

section('E-invoicing');
$inv = epc_einv_build(
    array(
        'invoice_no' => 'INV-2026-001',
        'issue_date' => $now,
        'currency' => 'AED',
        'seller' => array('name' => 'Spare247', 'tax_id' => '100123456700003', 'country' => 'AE', 'address' => 'Dubai'),
        'buyer' => array('name' => 'ACME', 'tax_id' => '100999888700003', 'country' => 'AE'),
    ),
    array(
        array('description' => 'Brake pad', 'qty' => 10, 'unit_price' => 50, 'tax_percent' => 5),
        array('description' => 'Oil filter', 'qty' => 4, 'unit_price' => 25, 'tax_percent' => 5),
    )
);
// net = 500 + 100 = 600 ; tax = 25 + 5 = 30 ; grand = 630
check('einvoice net = 600', abs($inv['totals']['net'] - 600.00) < 0.01);
check('einvoice tax = 30', abs($inv['totals']['tax'] - 30.00) < 0.01);
check('einvoice grand = 630', abs($inv['totals']['grand_total'] - 630.00) < 0.01);
check('UAE profile resolved (UAE-FTA)', $inv['profile'] === 'UAE-FTA');
check('valid when TRNs present', $inv['validation']['valid'] === true);
check('deterministic hash present', strlen((string) $inv['hash']) === 64);

// India GST requires HSN on lines -> should be invalid without it.
$invIn = epc_einv_build(
    array('invoice_no' => 'GST-1', 'issue_date' => $now, 'currency' => 'INR',
        'seller' => array('name' => 'S', 'tax_id' => '29ABCDE1234F1Z5', 'country' => 'IN'),
        'buyer' => array('name' => 'B', 'tax_id' => '29ZZZZZ1234F1Z5', 'country' => 'IN')),
    array(array('description' => 'Widget', 'qty' => 1, 'unit_price' => 100, 'tax_percent' => 18))
);
check('India GST invalid without HSN', $invIn['validation']['valid'] === false);
check('India profile = GST-IRN', $invIn['profile'] === 'GST-IRN');
$json = epc_einv_to_json($inv);
check('serialises to JSON', strlen($json) > 50 && strpos($json, 'INV-2026-001') !== false);

section('Bank account — extended fields (legal entity / business unit + bank details)');
$baId = epc_erp_create_cash_account($db, array(
    'name' => 'Operating account ' . $now,
    'account_type' => 'bank',
    'bank_name' => 'Emirates NBD',
    'bank_branch' => 'Deira Main',
    'account_number' => '0123456789',
    'iban' => 'AE070331234567890123456',
    'swift_bic' => 'EBILAEAD',
    'routing_code' => '302620122',
    'currency_code' => 'AED',
    'opening_balance' => 12500.50,
    'legal_entity_id' => 7,
    'business_unit_id' => 3,
    'gl_account_id' => 11,
    'address' => 'Baniyas Road, Dubai, UAE',
    'contact_name' => 'Relationship Mgr',
    'contact_phone' => '+9714000000',
    'contact_email' => 'rm@bank.example',
    'status' => 'active',
    'notes' => 'Primary AED current account',
));
check('bank account created', $baId > 0);
$row = $db->query("SELECT * FROM `epc_erp_cash_bank_accounts` WHERE `id` = " . (int) $baId)->fetch(PDO::FETCH_ASSOC);
check('legal_entity_id persisted', (int) $row['legal_entity_id'] === 7);
check('business_unit_id persisted', (int) $row['business_unit_id'] === 3);
check('gl_account_id persisted', (int) $row['gl_account_id'] === 11);
check('iban persisted', $row['iban'] === 'AE070331234567890123456');
check('swift_bic persisted', $row['swift_bic'] === 'EBILAEAD');
check('bank_branch persisted', $row['bank_branch'] === 'Deira Main');
check('routing_code persisted', $row['routing_code'] === '302620122');
check('contact_email persisted', $row['contact_email'] === 'rm@bank.example');
check('status persisted', $row['status'] === 'active');
check('opening balance persisted', abs((float) $row['opening_balance'] - 12500.50) < 0.01);

$baId2 = epc_erp_create_cash_account($db, array('name' => 'Petty cash ' . $now, 'account_type' => 'cash', 'status' => 'bogus'));
$row2 = $db->query("SELECT * FROM `epc_erp_cash_bank_accounts` WHERE `id` = " . (int) $baId2)->fetch(PDO::FETCH_ASSOC);
check('invalid status falls back to active', $row2['status'] === 'active');
check('cash account defaults legal/business unit to 0', (int) $row2['legal_entity_id'] === 0 && (int) $row2['business_unit_id'] === 0);

echo "\n========================================\n";
echo "FINANCE TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
