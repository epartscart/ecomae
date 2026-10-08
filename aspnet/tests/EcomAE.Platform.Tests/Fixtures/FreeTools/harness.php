<?php
// Captures PHP epc_free_tools_compute() output for a broad vector set (plus FILTER_VALIDATE_EMAIL verdicts)
// as golden.json, encoded exactly as ajax_epc_free_tools.php answers (JSON_UNESCAPED_SLASHES).
// Usage: php harness.php <repo_root> > golden.json
declare(strict_types=1);

date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '', '/');
$_SERVER['DOCUMENT_ROOT'] = $root;
define('_ASTEXE_', 1);
require $root . '/content/general_pages/epc_ecomae_free_tools.php';

$now = time();
$day = static fn (int $offset): string => date('Y-m-d', $now + $offset * 86400);

$codes = array();
foreach (array('/content/shop/finance/epc_erp_localization.php', '/content/shop/finance/epc_erp_hr_law.php') as $f) {
	preg_match_all("/'([A-Z]{2})'\\s*=>\\s*array\\(/", (string) file_get_contents($root . $f), $m);
	$codes = array_merge($codes, $m[1]);
}
$codes = array_values(array_unique(array_merge($codes, array('XX', 'ZZ', '', 'ae', ' gb ', 'generic'))));

$cases = array();
$add = static function (string $tool, string $country, array $in) use (&$cases): void {
	$cases[] = array('tool' => $tool, 'country' => $country, 'inputs' => $in);
};

foreach ($codes as $c) {
	$add('vat', $c, array('standard_sales' => '1,234.50', 'zero_sales' => 100, 'exempt_sales' => '(50)', 'standard_purchases' => 400.5, 'import_vat' => 'AED 10'));
	$add('ct', $c, array('revenue' => 500000, 'expenses' => 80000, 'adjustments' => 1000));
	$add('ct', $c, array('revenue' => 30000, 'expenses' => 45000));
	$add('payroll', $c, array('basic' => 10000, 'allowances' => 2500, 'deductions' => 300, 'years' => 7.5));
	$add('payroll', $c, array('basic' => '4200.75', 'years' => '0.5'));
	$add('taxkit', $c, array());
	$add('hrcompliance', $c, array());
	$add('hrcompliance', $c, array('basic_salary' => 8000, 'hire_date' => '2019-03-15', 'leave_balance' => 60));
	$add('hrcompliance', $c, array('basic_salary' => 5000, 'hire_date' => $day(-40), 'leave_balance' => 2));
	$add('insurance', $c, array('sum_insured' => 250000, 'rate' => 0.35, 'expiry' => $day(20)));
	$add('einvoice', $c, array('number' => 'inv-001', 'seller' => 'Acme LLC', 'seller_trn' => '100200300400500', 'buyer' => 'Buyer Co', 'lines' => array(array('desc' => 'Widget', 'qty' => 3, 'price' => 19.99), array('desc' => 'Service', 'qty' => '1', 'price' => '250'))));
	$add('customs', $c, array('hs_code' => '8471.30', 'qty' => 10, 'unit_value' => 120, 'freight' => 300, 'insurance' => 25, 'fx_rate' => 3.6725, 'other' => 80));
	$add('extreport', $c, array('standard_sales' => 200000, 'standard_purchases' => 50000, 'revenue' => 900000, 'expenses' => 300000));
	$add('workflow', $c, array());
	$add('valuation', $c, array('revenue' => 2000000, 'ebitda' => 400000, 'net_debt' => 150000, 'tax_rate' => 9));
	$add('finmodel', $c, array('revenue' => 1000000));
	$add('docexpiry', $c, array('title' => 'Trade licence', 'expiry' => $day(45)));
}

$vatCsv = "Type,Category,Amount,TRN\n"
	. "sale,standard,\"1,000.00\",100123\n"
	. "Sales,zero,500,\n"
	. "sale,exempt,200,1\n"
	. "sale,weird,300,\n"
	. "output,,(120),9\n"
	. ",,,\n"
	. "purchase,standard,800,\n"
	. "Input,import,45.5,\n"
	. "expense,std,-20,\n"
	. "\"sale\",\"standard\",\"2,5\"\"x\",\"\"\n";
foreach (array('AE', 'GB', 'XX', 'BH') as $c) {
	$add('vat', $c, array('csv' => $vatCsv));
	$add('ct', $c, array('csv' => "type,amount\nrevenue,1000000\nCOGS,400000\nopex,150000\nAdd-back,12000\nmystery,5\nTurnover,\"50,000\"\ndisallowed,3000\n"));
	$add('extreport', $c, array('csv' => $vatCsv));
}
$add('vat', 'AE', array('csv' => "type,category,amount\n", 'standard_sales' => 1000));
$add('vat', 'AE', array('csv' => "   \n  "));
$add('ct', 'AE', array('csv' => "type,amount\n,\n", 'revenue' => 999));
$add('ct', 'GB', array('revenue' => 50000));
$add('ct', 'GB', array('revenue' => 50000.01));
$add('ct', 'NG', array('revenue' => '30,000,000', 'expenses' => '1,000,000'));

$tbBalanced = "Account,Debit,Credit,Classification\n"
	. "Sales,,500000,revenue\nCOGS,200000,,cost of sales\nRent,60000,,overheads\nInterest income,,5000,other income\n"
	. "PPE,300000,,ppe\nCash,95000,,cash\nShare capital,,100000,capital\nBank loan,,40000,loans\nCreditors,,10000,payables\n";
$add('ifrs', 'AE', array('csv' => $tbBalanced));
$add('ifrs', 'GB', array('csv' => "account,amount,class\nSales,-1000,turnover\nStock,700,inventory\nMisc,50,unknown-class\nEquity,-300,equity\n"));
$add('ifrs', 'XX', array('csv' => "account,debit,credit,type\nA,100,,ca\nB,,90,cl\n"));
$add('ifrs', 'AE', array('revenue' => 1000, 'cogs' => 400, 'opex' => 100, 'other_income' => 20, 'non_current_assets' => 900, 'current_assets' => 100, 'equity' => 600, 'non_current_liabilities' => 300, 'current_liabilities' => 100));
$add('ifrs', 'AE', array('revenue' => 10, 'current_assets' => 5));
$add('ifrs', 'AE', array('csv' => "only,header\n"));

$add('customs', 'AE', array('csv' => "HS_Code,Qty,Unit_Value\n2402.20,100,3.5\n,5,10\n8703.23,1,0\n3004.90,,12.25\n2204,2,\"1,050\"\n", 'freight' => '1,200', 'insurance' => 60, 'fx_rate' => 0, 'regime' => 'import_for_home', 'other' => 150));
$add('customs', 'SA', array('csv' => "hs,quantity,value\n1006.30,1000,0.75\n", 'regime' => 'transit', 'fx_rate' => '3.75'));
$add('customs', 'GB', array('csv' => "hs_code,qty,unit_value\n"));
$add('customs', 'XX', array());

$add('insurance', 'AE', array('premium' => 1200, 'expiry' => $day(-3)));
$add('insurance', 'SA', array('sum_insured' => 100000, 'rate' => '0.12345', 'expiry' => 'not a date'));
$add('insurance', 'IN', array('sum_insured' => 50000, 'expiry' => $day(400)));
$add('insurance', 'XX', array());
$add('insurance', 'GB', array('expiry' => '1970-01-01'));

$docCsv = "Title,Expiry,Reminder_Days\n"
	. "Passport," . $day(-10) . ",\n"
	. "Visa," . $day(5) . ",\"30,7\"\n"
	. "Lease," . $day(75) . ",90\n"
	. "Licence," . $day(200) . ",\"90, 60 ,30,x,-5,60\"\n"
	. "Permit,not a date,\n"
	. "Blank,,\n"
	. "Euro," . date('d.m.Y', $now + 61 * 86400) . ",\n"
	. "US," . date('m/d/Y', $now + 8 * 86400) . ",\n"
	. "Words," . date('M j, Y', $now + 100 * 86400) . ",\n"
	. "Overflow,2027-02-31,\n"
	. "Dashed," . date('d-m-Y', $now + 31 * 86400) . ",\n";
$add('docexpiry', 'AE', array('csv' => $docCsv));
$add('docexpiry', 'AE', array('csv' => $docCsv, 'reminder_days' => '120, 14'));
$add('docexpiry', 'GB', array('csv' => "document,expires\nInsurance," . $day(29) . "\n"));
$add('docexpiry', 'XX', array('title' => '', 'expiry' => '', 'reminder_days' => ''));
$add('docexpiry', 'XX', array('expiry' => $day(0)));

$add('valuation', 'AE', array('revenue' => 1000, 'ebitda' => 200, 'growth' => 20, 'discount' => 10));
$add('valuation', 'AE', array('csv' => "type,amount\nrevenue,800000\nexpense,500000\nnoise,1\n", 'ebitda_multiple' => 0, 'revenue_multiple' => '2.345'));
$add('valuation', 'GB', array('revenue' => 1000, 'ebitda' => -50));
$add('finmodel', 'AE', array('revenue' => 500000, 'growth' => 12.5, 'gross_margin' => 35, 'opex_pct' => 20, 'tax_rate' => 9, 'years' => 12));
$add('finmodel', 'AE', array('revenue' => 0, 'years' => 0));
$add('finmodel', 'GB', array('csv' => "type,amount\nsales,250000\n", 'years' => '3.7'));
$add('workflow', 'AE', array('tier1' => '12500.5', 'tier2' => 1e6, 'approver0' => '  Team lead ', 'approver1' => 'CFO', 'approver2' => 'Board'));
$add('einvoice', 'AE', array('number' => 'A-1', 'lines' => array()));
$add('einvoice', 'XX', array('number' => '', 'lines' => array('x', 5, array('desc' => '', 'qty' => 0, 'price' => 0), array('desc' => 'Only desc'))));
$add('einvoice', 'DE', array('number' => 'de-9', 'seller' => ' ', 'seller_trn' => 'DE123', 'buyer_trn' => ' B1 ', 'lines' => array(array('desc' => 'Ünïcode / slash', 'qty' => 1.5, 'price' => 0.1))));
$add('einvoice', 'AE', array('seller' => 'Random number', 'lines' => array(array('desc' => 'x', 'qty' => 1, 'price' => 1))));
$add('payroll', 'AE', array('basic' => array(1), 'allowances' => true, 'years' => '30'));
$add('payroll', 'IN', array('basic' => 26000, 'years' => 6.4));
$add('payroll', 'IN', array('basic' => 26000, 'years' => 4.99));
$add('payroll', 'IN', array('basic' => 9000000, 'years' => 20));
$add('payroll', 'AE', array('basic' => 30000, 'years' => 40));
$add('hrcompliance', 'AE', array('basic_salary' => 4000, 'hire_date' => $day(-170), 'leave_balance' => 100));
$add('hrcompliance', 'SA', array('basic_salary' => '7,000', 'hire_date' => 'not a date'));
$add('hrcompliance', 'IN', array('basic_salary' => 20000, 'hire_date' => '15 March 2015'));
$add('hrcompliance', 'XX', array('basic_salary' => 3000, 'hire_date' => '1965-01-01'));
$add('vat', 'AE', array('standard_sales' => '-', 'zero_sales' => '1.2.3', 'exempt_sales' => '--5', 'standard_purchases' => ' 1e3 ', 'import_vat' => true));
$add('vat', 'AE', array('standard_sales' => 0.1, 'standard_purchases' => 0.2));
$add('vat', 'AE', array('standard_sales' => 123456789012345678, 'standard_purchases' => 1e-7));
$add('bogus', 'AE', array());
$add('', 'AE', array());

$out = array();
foreach ($cases as $case) {
	$res = epc_free_tools_compute($case['tool'], $case['country'], $case['inputs']);
	$out[] = array(
		'tool' => $case['tool'],
		'country' => $case['country'],
		'inputs' => json_encode($case['inputs'], JSON_UNESCAPED_SLASHES),
		'out' => json_encode($res, JSON_UNESCAPED_SLASHES),
	);
}

$emails = array(
	'a@b.co', 'user.name+tag@example.com', 'x@localhost', 'a@b', 'a..b@example.com', '.a@example.com', 'a.@example.com',
	'"quoted"@example.com', 'a@[127.0.0.1]', 'a@[IPv6:::1]', 'a@-example.com', 'a@example-.com', 'a@ex_ample.com',
	'test@xn--bcher-kva.example', 'ünï@example.com', 'a b@example.com', str_repeat('a', 64) . '@example.com',
	str_repeat('a', 65) . '@example.com', 'a@' . str_repeat('b', 63) . '.com', 'a@' . str_repeat('b', 64) . '.com',
	'first.last@sub.domain.example.org', 'a@1.2.3.4', 'a@b.c1', 'a@b.1c', "a'b@example.com", 'a/b@example.com',
);
$emailOut = array();
foreach ($emails as $e) {
	$emailOut[] = array('email' => $e, 'valid' => filter_var($e, FILTER_VALIDATE_EMAIL) !== false);
}

$dates = array(
	'2026-10-08', '2026-1-5', '2026-02-31', '2026/03/04', '2026/3/4 13:45', '2026-03-04 07:08:09', '03/04/2027', '3/4/2027',
	'04-03-2027', '4-3-2027', '04.03.2027', '4.3.2027 10:30', '5 March 2027', '5-mar-2027', 'Mar 5, 2027', 'March 5 2027',
	'mar 5th, 2027', 'sept 3 2027', '16 jan', 'jan 16', 'January 16', 'june 31', 'feb 30', ' 2027-01-01 ', 'today', 'TODAY',
	'tomorrow', 'yesterday', 'midnight', '0000-00-00', '0001-01-01', '1969-12-31', '2027-13-01', '2027-00-10', 'not a date', '',
	'2027-02-29', '2028-02-29', '31.12.1999', '12/31/1999 23:59:59', '2027-01-01 24:00',
);
$dateOut = array();
foreach ($dates as $d) {
	$t = strtotime($d);
	$dateOut[] = array('input' => $d, 'unix' => $t === false ? null : $t);
}

echo json_encode(array('now' => $now, 'cases' => $out, 'emails' => $emailOut, 'dates' => $dateOut), JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE), "\n";
