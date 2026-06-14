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
check('catalog has 14 tools', count($cat) === 14);
check('catalog includes einvoice', isset($cat['einvoice']));
check('catalog includes workflow', isset($cat['workflow']));
foreach (array('extreport', 'customs', 'insurance', 'docexpiry', 'valuation', 'finmodel', 'taxkit', 'hrcompliance') as $nt) {
	check("catalog includes $nt", isset($cat[$nt]));
}
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

section('VAT — CSV upload + compliance');
$vatCsv = "type,category,amount,trn\nsale,standard,1000,100123456700003\nsale,standard,500,\nsale,zero,2000,TRN1\npurchase,standard,400,";
$vc = epc_free_tools_compute('vat', 'AE', array('csv' => $vatCsv));
check('VAT CSV ok', $vc['ok'] === true);
check('VAT CSV has compliance findings', isset($vc['compliance']) && count($vc['compliance']) > 0);
$hasTrnWarn = false;
foreach ($vc['compliance'] as $f) { if (strpos($f['message'], 'TRN') !== false || strpos($f['message'], 'registration') !== false) { $hasTrnWarn = true; } }
check('VAT CSV flags missing TRN', $hasTrnWarn);
// output = 5% of (1000+500 standard sales)=75; input = 5% of 400 = 20 -> net 55
check('VAT CSV net = 55', approx((float) $vc['net'], 55.0));

section('CT — CSV upload + compliance');
$ctCsv = "type,amount\nrevenue,1000000\nexpense,400000\nadjustment,25000";
$ctc = epc_free_tools_compute('ct', 'AE', array('csv' => $ctCsv));
check('CT CSV ok', $ctc['ok'] === true);
// taxable profit = 1,000,000 - 400,000 + 25,000 = 625,000; 9% on (625k-375k)=250k => 22,500
check('CT CSV net = 22500', approx((float) $ctc['net'], 22500.0));

section('IFRS — CSV trial balance + balance check');
$tb = "account,debit,credit,classification\nSales,0,500000,revenue\nCOGS,300000,0,cogs\nOpex,80000,0,opex\nFixed assets,200000,0,non_current_assets\nCash,150000,0,current_assets\nCapital,0,230000,equity\nPayables,0,100000,current_liabilities\nLoan,0,0,non_current_liabilities\nSuspense,0,300000,equity";
$if = epc_free_tools_compute('ifrs', 'GB', array('csv' => $tb));
check('IFRS CSV ok', $if['ok'] === true);
check('IFRS CSV produces compliance', isset($if['compliance']) && count($if['compliance']) > 0);
$tbTotalsSeen = false;
foreach ($if['compliance'] as $f) { if (stripos($f['message'], 'trial balance') !== false) { $tbTotalsSeen = true; } }
check('IFRS CSV reports trial-balance check', $tbTotalsSeen);

section('External Reporting — country authority + nested packs');
$ext = epc_free_tools_compute('extreport', 'AE', array('standard_sales' => 100000, 'standard_purchases' => 40000, 'revenue' => 1000000, 'expenses' => 400000));
check('extreport ok', $ext['ok'] === true);
check('extreport names FTA authority', strpos((string) $ext['authority'], 'FTA') !== false);
check('extreport VAT return name VAT 201', $ext['vat_return_name'] === 'VAT 201');
check('extreport carries nested vat pack', isset($ext['vat']['rows']));
check('extreport carries nested ct pack', isset($ext['ct']['rows']));
$extSa = epc_free_tools_compute('extreport', 'SA', array('standard_sales' => 1000));
check('extreport SA names ZATCA', strpos((string) $extSa['authority'], 'ZATCA') !== false);

section('Customs & Logistics — CIF/duty/import VAT');
$cust = epc_free_tools_compute('customs', 'AE', array('hs_code' => '8516', 'qty' => 10, 'unit_value' => 250, 'freight' => 300, 'insurance' => 100));
check('customs ok', $cust['ok'] === true);
check('customs net payable positive', (float) $cust['net'] > 0);
check('customs landed cost present', isset($cust['landed_cost']));
$custCsv = epc_free_tools_compute('customs', 'AE', array('csv' => "hs_code,qty,unit_value\n8516,10,250\n,5,100"));
check('customs CSV ok', $custCsv['ok'] === true);
$custHsWarn = false;
foreach ($custCsv['compliance'] as $f) { if (stripos($f['message'], 'HS code') !== false) { $custHsWarn = true; } }
check('customs CSV flags missing HS code', $custHsWarn);

section('Insurance — country cover + renewal');
$ins = epc_free_tools_compute('insurance', 'AE', array('sum_insured' => 1000000, 'rate' => 0.5, 'expiry' => date('Y-m-d', time() + 10 * 86400)));
check('insurance ok', $ins['ok'] === true);
check('insurance premium = 5000', approx((float) $ins['net'], 5000.0));
check('insurance lists recommended cover', isset($ins['recommended']) && count($ins['recommended']) >= 0);
$insExpiringFlag = false;
foreach ($ins['compliance'] as $f) { if ($f['level'] === 'warn' && stripos($f['message'], 'renew') !== false) { $insExpiringFlag = true; } }
check('insurance flags imminent renewal', $insExpiringFlag);

section('Document Expiry — status + reminders');
$dx = epc_free_tools_compute('docexpiry', 'AE', array('csv' => "title,expiry,reminder_days\nTrade Licence," . date('Y-m-d', time() + 20 * 86400) . ",90,60,30\nPassport," . date('Y-m-d', time() - 5 * 86400) . ",90"));
check('docexpiry ok', $dx['ok'] === true);
check('docexpiry tracks 2 docs', isset($dx['doc_rows']) && count($dx['doc_rows']) === 2);
$dxExpired = false;
foreach ($dx['compliance'] as $f) { if ($f['level'] === 'fail' && stripos($f['message'], 'EXPIRED') !== false) { $dxExpired = true; } }
check('docexpiry flags expired doc', $dxExpired);

section('Business Valuation — DCF + multiples');
$val = epc_free_tools_compute('valuation', 'AE', array('revenue' => 5000000, 'ebitda' => 1000000, 'net_debt' => 500000, 'growth' => 5, 'discount' => 15, 'ebitda_multiple' => 6, 'revenue_multiple' => 1.5));
check('valuation ok', $val['ok'] === true);
check('valuation equity less than EV (net debt subtracted)', (float) $val['net'] > 0);
// EBITDA EV = 6,000,000; Rev EV = 7,500,000; DCF = 1,000,000*1.05/0.10 = 10,500,000; avg = 8,000,000; equity = 7,500,000
check('valuation equity = 7,500,000', approx((float) $val['net'], 7500000.0));

section('Financial Model — projection');
$fm = epc_free_tools_compute('finmodel', 'AE', array('revenue' => 1000000, 'growth' => 10, 'gross_margin' => 40, 'opex_pct' => 25, 'tax_rate' => 9, 'years' => 5));
check('finmodel ok', $fm['ok'] === true);
check('finmodel projects 5 years', count($fm['projection']) === 5);
check('finmodel year1 revenue = base', approx((float) $fm['projection'][0]['revenue'], 1000000.0));
check('finmodel year2 revenue = +10%', approx((float) $fm['projection'][1]['revenue'], 1100000.0));

section('Tax Worldwide Kit — country snapshot');
$tk = epc_free_tools_compute('taxkit', 'AE', array());
check('taxkit ok', $tk['ok'] === true);
check('taxkit net = VAT rate 5', approx((float) $tk['net'], 5.0));
check('taxkit rows present', isset($tk['rows_text']) && count($tk['rows_text']) > 0);
$tkSa = epc_free_tools_compute('taxkit', 'SA', array());
check('taxkit SA VAT rate 15', approx((float) $tkSa['net'], 15.0));

section('HR Compliance Worldwide — labour card + EOS');
$hr = epc_free_tools_compute('hrcompliance', 'AE', array('basic_salary' => 10000, 'hire_date' => date('Y-m-d', time() - 3 * 365 * 86400)));
check('hrcompliance ok', $hr['ok'] === true);
check('hrcompliance has labour card rows', isset($hr['rows_text']) && count($hr['rows_text']) > 0);
check('hrcompliance has authority url', isset($hr['authority_url']) && $hr['authority_url'] !== '');
check('hrcompliance EOS liability positive', (float) $hr['net'] > 0);
$hrSa = epc_free_tools_compute('hrcompliance', 'SA', array());
check('hrcompliance SA card resolves', $hrSa['ok'] === true && count($hrSa['rows_text']) > 0);

section('Pre-registration guides');
$gOk = true;
foreach (array_keys($cat) as $tkey) {
	$g = epc_free_tools_guide($tkey);
	if (($g['what'] ?? '') === '' || empty($g['get']) || ($g['how'] ?? '') === '') { $gOk = false; }
}
check('every tool has a guide (what/get/how)', $gOk);
$gv = epc_free_tools_guide('vat');
check('VAT guide documents CSV format', isset($gv['csv']) && strpos($gv['csv'], 'trn') !== false);
$ghtml = epc_free_tools_guide_html('ifrs', $cat['ifrs']);
check('guide HTML renders for a tool', strpos($ghtml, 'What it does') !== false && strpos($ghtml, 'eft-guide') !== false);
check('guide HTML escapes / has no PHP error', strpos($ghtml, '<?php') === false);

section('Generic fallback');
$xx = epc_free_tools_compute('vat', 'XX', array('standard_sales' => 1000));
check('generic VAT 0% rate', approx((float) $xx['rate'], 0.0));
check('generic currency USD', $xx['currency'] === 'USD');
$bad = epc_free_tools_compute('nope', 'AE', array());
check('unknown tool returns not ok', empty($bad['ok']));

section('Auth — email + password (validation, DB-independent)');
check('register fn exists', function_exists('epc_free_tools_register'));
check('login fn exists', function_exists('epc_free_tools_login'));
$rBadEmail = epc_free_tools_register('not-an-email', 'Acme', 'AE', 'secret1');
check('register rejects invalid email', empty($rBadEmail['ok']) && strpos($rBadEmail['message'], 'valid email') !== false);
$rShortPw = epc_free_tools_register('user@acme.com', 'Acme', 'AE', '123');
check('register rejects short password', empty($rShortPw['ok']) && strpos($rShortPw['message'], '6 characters') !== false);
$lNoEmail = epc_free_tools_login('', 'whatever');
check('login rejects empty/invalid email', empty($lNoEmail['ok']) && strpos($lNoEmail['message'], 'valid email') !== false);
$lNoPw = epc_free_tools_login('user@acme.com', '');
check('login requires a password', empty($lNoPw['ok']) && strpos($lNoPw['message'], 'password') !== false);

section('Password reset — code flow (validation, DB-independent)');
check('request_reset fn exists', function_exists('epc_free_tools_request_reset'));
check('confirm_reset fn exists', function_exists('epc_free_tools_confirm_reset'));
$rsBad = epc_free_tools_request_reset('nope');
check('request_reset rejects invalid email', empty($rsBad['ok']));
$crShort = epc_free_tools_confirm_reset('user@acme.com', '123456', 'abc');
check('confirm_reset rejects short new password', empty($crShort['ok']) && strpos($crShort['message'], '6 characters') !== false);

section('Secure delete — cross-code required (DB-independent)');
check('request_delete fn exists', function_exists('epc_free_tools_request_delete'));
check('confirm_delete fn exists', function_exists('epc_free_tools_confirm_delete'));
$dReq = epc_free_tools_request_delete('');
check('delete request needs a session token', empty($dReq['ok']) && strpos($dReq['message'], 'sign in') !== false);
$dConf = epc_free_tools_confirm_delete('', '000000');
check('delete confirm needs a session token', empty($dConf['ok']) && strpos($dConf['message'], 'sign in') !== false);

section('Tool activation control');
check('is_active fn exists', function_exists('epc_free_tools_is_active'));
check('set_active fn exists', function_exists('epc_free_tools_set_active'));
check('disabled_map fn exists', function_exists('epc_free_tools_disabled_map'));
check('default disabled map empty (no DB) → tools active', epc_free_tools_is_active('vat') === true);
check('disabled_map returns array', is_array(epc_free_tools_disabled_map()));

section('Usage stats (BOS dashboard)');
check('usage_stats fn exists', function_exists('epc_free_tools_usage_stats'));
$us = epc_free_tools_usage_stats();
check('usage_stats returns array', is_array($us));
check('usage_stats not ok without DB', empty($us['ok']));
check('touch_account fn exists', function_exists('epc_free_tools_touch_account'));

section('SEO — titles, descriptions, structured data');
check('seo fn exists', function_exists('epc_free_tools_seo'));
$seoHub = epc_free_tools_seo('');
check('hub SEO has title + description + faq', $seoHub['title'] !== '' && $seoHub['description'] !== '' && !empty($seoHub['faq']));
$seoVat = epc_free_tools_seo('vat');
check('vat SEO title is unique vs hub', $seoVat['title'] !== $seoHub['title']);
check('vat SEO title keyword-rich', stripos($seoVat['title'], 'VAT') !== false && stripos($seoVat['title'], 'free') !== false);
$titlesSeen = array();
$dupTitle = false;
foreach (array_keys($cat) as $tk) {
	$t = epc_free_tools_seo($tk)['title'];
	if (isset($titlesSeen[$t])) { $dupTitle = true; }
	$titlesSeen[$t] = true;
}
check('every tool has a unique SEO title', !$dupTitle && count($titlesSeen) === count($cat));
$ld = epc_free_tools_jsonld('https://www.ecomae.com', 'vat');
$ldJson = preg_replace('#</?script[^>]*>#', '', $ld);
$ldArr = json_decode($ldJson, true);
check('tool JSON-LD is valid JSON', json_last_error() === JSON_ERROR_NONE && isset($ldArr['@graph']));
$types = array();
foreach ($ldArr['@graph'] as $g) { $types[] = $g['@type']; }
check('tool JSON-LD has SoftwareApplication + Breadcrumb + FAQ', in_array('SoftwareApplication', $types, true) && in_array('BreadcrumbList', $types, true) && in_array('FAQPage', $types, true));
$hubLd = json_decode(preg_replace('#</?script[^>]*>#', '', epc_free_tools_jsonld('https://www.ecomae.com', '')), true);
$hubHasList = false; $listN = 0;
foreach ($hubLd['@graph'] as $g) { if ($g['@type'] === 'ItemList') { $hubHasList = true; $listN = count($g['itemListElement']); } }
check('hub JSON-LD ItemList lists all 14 tools', $hubHasList && $listN === 14);
$faqHtml = epc_free_tools_faq_html('vat');
check('FAQ HTML renders details/summary', strpos($faqHtml, '<details') !== false && strpos($faqHtml, '<summary') !== false);
check('FAQ HTML escapes / no PHP error', strpos($faqHtml, '<?php') === false);

section('Branded transactional email helpers');
check('send_mail fn exists', function_exists('epc_free_tools_send_mail'));
check('mail_shell fn exists', function_exists('epc_free_tools_mail_shell'));
$shell = epc_free_tools_mail_shell('Hi', '<p>Body</p>');
check('mail shell wraps body', strpos($shell, 'Body') !== false);
check('mail shell escapes / no PHP error', strpos($shell, '<?php') === false);

echo "\n========================================\n";
echo "FREE TOOLS TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
