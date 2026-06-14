<?php
/**
 * CLI tests for the Free Tools tier compute engine (pure, no DB):
 * country-driven VAT/GST, corporate tax, payroll & gratuity, IFRS, e-invoice,
 * workflow, plus catalog/country integrity.
 *
 *   php tests/erp_advanced/run_free_tools_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);
$_SERVER['DOCUMENT_ROOT'] = dirname(__DIR__, 2);

require_once dirname(__DIR__, 2) . '/content/general_pages/epc_ecomae_free_tools.php';

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
function approx(float $a, float $b): bool
{
	return abs($a - $b) < 0.01;
}
function section(string $t): void
{
	echo "\n== $t ==\n";
}

section('Catalog & countries');
$cat = epc_free_tools_catalog();
check('catalog has 6 tools', count($cat) === 6);
check('catalog includes einvoice', isset($cat['einvoice']));
check('catalog includes workflow', isset($cat['workflow']));
$countries = epc_free_tools_countries();
check('countries include AE', isset($countries['AE']));
check('countries include generic XX', isset($countries['XX']));

section('VAT / GST — country-driven rate');
$ae = epc_free_tools_compute('vat', 'AE', array('standard_sales' => 100000, 'standard_purchases' => 40000));
check('AE VAT ok', $ae['ok'] === true);
check('AE rate is 5%', approx((float) $ae['rate'], 5.0));
check('AE currency AED', $ae['currency'] === 'AED');
check('AE net = 5% of (100000-40000) = 3000 payable', approx((float) $ae['net'], 3000.0));
check('AE net label payable', strpos($ae['net_label'], 'payable') !== false);
$sa = epc_free_tools_compute('vat', 'SA', array('standard_sales' => 100000, 'standard_purchases' => 0));
check('SA rate is 15%', approx((float) $sa['rate'], 15.0));
check('SA output = 15000', approx((float) $sa['net'], 15000.0));
$refund = epc_free_tools_compute('vat', 'AE', array('standard_sales' => 0, 'standard_purchases' => 20000));
check('refund label when input>output', strpos($refund['net_label'], 'refundable') !== false);

section('Corporate tax — relief band');
$ct = epc_free_tools_compute('ct', 'AE', array('revenue' => 1000000, 'expenses' => 400000));
check('AE CT ok', $ct['ok'] === true);
check('AE CT rate 9%', approx((float) $ct['rate'], 9.0));
// profit 600k; 0% first 375k; 9% on 225k = 20250
check('AE CT = 20250 (relief applied)', approx((float) $ct['net'], 20250.0));
$ctSmall = epc_free_tools_compute('ct', 'AE', array('revenue' => 300000, 'expenses' => 0));
check('AE CT zero under relief threshold', approx((float) $ctSmall['net'], 0.0));
$ctLoss = epc_free_tools_compute('ct', 'AE', array('revenue' => 100, 'expenses' => 5000));
check('AE CT zero on loss', approx((float) $ctLoss['net'], 0.0));
$bh = epc_free_tools_compute('ct', 'BH', array('revenue' => 1000000, 'expenses' => 0));
check('Bahrain 0% CT', approx((float) $bh['net'], 0.0));

section('Payroll & gratuity');
$pay = epc_free_tools_compute('payroll', 'AE', array('basic' => 9000, 'allowances' => 3000, 'deductions' => 500, 'years' => 3));
check('payroll ok', $pay['ok'] === true);
check('net pay = 12000-500 = 11500', approx((float) $pay['net'], 11500.0));
check('payroll currency AED', $pay['currency'] === 'AED');
// gratuity should be > 0 for 3 years AE
$gratRow = 0.0;
foreach ($pay['rows'] as $r) { if ($r[0] === 'End-of-service gratuity') { $gratRow = (float) $r[1]; } }
check('gratuity positive for 3yr AE', $gratRow > 0);

section('IFRS financials');
$ifrs = epc_free_tools_compute('ifrs', 'GB', array(
	'revenue' => 500000, 'cogs' => 300000, 'opex' => 80000, 'other_income' => 10000,
	'non_current_assets' => 200000, 'current_assets' => 150000,
	'equity' => 250000, 'non_current_liabilities' => 50000, 'current_liabilities' => 50000,
));
check('ifrs ok', $ifrs['ok'] === true);
check('operating profit = 500-300-80+10 = 130000', approx((float) $ifrs['net'], 130000.0));
check('balance sheet balances (350=350)', $ifrs['balanced'] === true);
$ifrsBad = epc_free_tools_compute('ifrs', 'GB', array('non_current_assets' => 100, 'equity' => 1));
check('unbalanced flagged', $ifrsBad['balanced'] === false);

section('E-invoice generator');
$inv = epc_free_tools_compute('einvoice', 'AE', array(
	'seller' => 'My Co', 'buyer' => 'Client', 'number' => 'INV-001',
	'lines' => array(
		array('desc' => 'Service A', 'qty' => 2, 'price' => 1000),
		array('desc' => 'Service B', 'qty' => 1, 'price' => 500),
	),
));
check('einvoice ok', $inv['ok'] === true);
check('subtotal = 2500', approx((float) $inv['subtotal'], 2500.0));
check('tax = 5% of 2500 = 125', approx((float) $inv['tax'], 125.0));
check('total = 2625', approx((float) $inv['net'], 2625.0));
check('keeps invoice number', $inv['number'] === 'INV-001');
check('AE scheme present', strpos((string) $inv['scheme'], 'FTA') !== false);
check('uuid generated', strlen((string) $inv['uuid']) >= 32);

section('Approval workflow');
$wf = epc_free_tools_compute('workflow', 'AE', array('tier1' => 5000, 'tier2' => 50000));
check('workflow ok', $wf['ok'] === true);
check('three tiers', count($wf['steps']) === 3);
check('tier uses AED currency', strpos($wf['steps'][0]['range'], 'AED') !== false);

section('Generic fallback');
$xx = epc_free_tools_compute('vat', 'XX', array('standard_sales' => 1000));
check('generic VAT 0% rate', approx((float) $xx['rate'], 0.0));
check('generic currency USD', $xx['currency'] === 'USD');
$bad = epc_free_tools_compute('nope', 'AE', array());
check('unknown tool returns not ok', empty($bad['ok']));

echo "\n========================================\n";
echo "FREE TOOLS TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
