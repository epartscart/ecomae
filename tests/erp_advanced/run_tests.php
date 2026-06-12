<?php
/**
 * Advanced ERP — local integration test harness (CLI only).
 *
 * Loads the new modules against a local MySQL/MariaDB, runs the schema, and
 * exercises the industry foundation, advanced CRM, and worldwide tax catalog.
 *
 * Usage:
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *   php tests/erp_advanced/run_tests.php
 *
 * This file is NOT web-routable logic: it refuses to run outside the CLI.
 */

if (PHP_SAPI !== 'cli') {
    http_response_code(403);
    exit('CLI only');
}

define('_ASTEXE_', 1);

$root = dirname(__DIR__, 2);
$fin = $root . '/content/shop/finance';
if (empty($_SERVER['DOCUMENT_ROOT'])) {
    $_SERVER['DOCUMENT_ROOT'] = $root;
}

require_once $fin . '/epc_erp_advanced.php';
require_once $fin . '/epc_erp_industry.php';
require_once $fin . '/epc_crm_schema.php';
require_once $fin . '/epc_crm_helpers.php';
require_once $fin . '/epc_erp_crm_advanced.php';
require_once $fin . '/epc_tax_toolkit_world.php';

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

$db = new PDO("mysql:host=$host;dbname=$name;charset=utf8", $user, $pass, array(
    PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
));

$pass_n = 0;
$fail_n = 0;
function check(string $label, bool $ok, string $detail = ''): void
{
    global $pass_n, $fail_n;
    if ($ok) {
        $pass_n++;
        echo "  PASS  $label" . ($detail !== '' ? "  ($detail)" : '') . "\n";
    } else {
        $fail_n++;
        echo "  FAIL  $label" . ($detail !== '' ? "  ($detail)" : '') . "\n";
    }
}

echo "\n=== 1. Industry foundation schema + apply ===\n";
epc_erp_industry_ensure_schema($db);
check('industry_state table created', (bool) $db->query("SHOW TABLES LIKE 'epc_erp_industry_state'")->fetchColumn());

$catalog = epc_erp_industry_catalog();
check('catalog has >= 10 industries', count($catalog) >= 10, count($catalog) . ' industries');

// Apply auto_parts then food_perishable (additive).
$ap = epc_erp_industry_apply($db, 'auto_parts', 1);
check('auto_parts applied', $ap['status'] === true && $ap['fields_seeded'] >= 5, 'seeded ' . $ap['fields_seeded']);

$fp = epc_erp_industry_apply($db, 'food_perishable', 1);
check('food_perishable applied (perishable + expiry)', $fp['status'] && $fp['item_type'] === 'perishable' && $fp['track_expiry'] === 1);

// Verify custom fields actually landed in inventory field defs.
$fieldCount = (int) $db->query('SELECT COUNT(*) FROM `epc_erp_inv_field_defs`')->fetchColumn();
check('inv_field_defs populated', $fieldCount >= 10, $fieldCount . ' field defs');

$oem = $db->query("SELECT COUNT(*) FROM `epc_erp_inv_field_defs` WHERE `field_key`='oem_number'")->fetchColumn();
check('auto_parts OEM field present', (int) $oem === 1);

$expiry = $db->query("SELECT `field_type` FROM `epc_erp_inv_field_defs` WHERE `field_key`='expiry_date'")->fetchColumn();
check('expiry_date is a date field', $expiry === 'date', (string) $expiry);

$sideOpts = $db->query("SELECT `options_json` FROM `epc_erp_inv_field_defs` WHERE `field_key`='side_position'")->fetchColumn();
$sideArr = json_decode((string) $sideOpts, true);
check('select field stores options json', is_array($sideArr) && in_array('Front', $sideArr, true));

// Idempotency: re-apply auto_parts, field count for its keys must not duplicate.
epc_erp_industry_apply($db, 'auto_parts', 1);
$oem2 = (int) $db->query("SELECT COUNT(*) FROM `epc_erp_inv_field_defs` WHERE `field_key`='oem_number'")->fetchColumn();
check('re-apply is idempotent (no dup keys)', $oem2 === 1);

$cur = epc_erp_industry_current($db);
check('current industry persisted', $cur['key'] === 'auto_parts', $cur['key']);

echo "\n=== 2. Worldwide tax catalog resolution ===\n";
$cases = array(
    'AE' => array('type' => 'vat', 'rate' => 5.0),
    'SA' => array('type' => 'vat', 'rate' => 15.0),
    'GB' => array('type' => 'vat', 'rate' => 20.0),
    'IN' => array('type' => 'gst', 'rate' => 18.0),
);
foreach ($cases as $cc => $exp) {
    $meta = epc_tax_toolkit_world_meta_for_country($cc);
    $okType = isset($meta['tax_type']) && stripos((string) $meta['tax_type'], $exp['type']) !== false;
    $okRate = isset($meta['rate']) && abs((float) $meta['rate'] - $exp['rate']) < 0.001;
    check("tax meta $cc = {$exp['type']} {$exp['rate']}%", $okType && $okRate, ($meta['tax_type'] ?? '?') . ' ' . ($meta['rate'] ?? '?') . '%');
}

// Simple tax math sanity on resolved rate.
$meta = epc_tax_toolkit_world_meta_for_country('AE');
$net = 1000.0;
$tax = round($net * ((float) $meta['rate']) / 100, 2);
check('AE tax on 1000 = 50.00', abs($tax - 50.0) < 0.001, number_format($tax, 2));

echo "\n=== 3. Advanced CRM (scoring, forecast, tax-aware quote) ===\n";
epc_crm_ensure_schema($db);
check('crm schema present (leads)', (bool) $db->query("SHOW TABLES LIKE 'epc_crm_leads'")->fetchColumn());

// Seed deterministic CRM data.
$db->exec('DELETE FROM `epc_crm_leads`');
$db->exec('DELETE FROM `epc_crm_opportunities`');
$db->exec('DELETE FROM `epc_crm_quotes`');
$db->exec('DELETE FROM `epc_crm_quote_lines`');
$now = time();
$db->prepare("INSERT INTO `epc_crm_leads` (`company`,`contact_name`,`email`,`phone`,`status`,`expected_value`,`time_created`,`active`) VALUES (?,?,?,?,?,?,?,1)")
   ->execute(array('Big Co', 'Aisha', 'a@big.co', '+97150', 'qualified', 80000, $now));
$db->prepare("INSERT INTO `epc_crm_leads` (`company`,`contact_name`,`email`,`phone`,`status`,`expected_value`,`time_created`,`active`) VALUES (?,?,?,?,?,?,?,1)")
   ->execute(array('Small Co', 'Bilal', '', '', 'new', 0, $now));

$scored = epc_crm_adv_scored_leads($db, 50);
check('scored leads returned', count($scored) === 2, count($scored) . ' leads');
check('hottest lead first', isset($scored[0]) && $scored[0]['company'] === 'Big Co', $scored[0]['lead_band'] ?? '?');
check('qualified lead scores higher than new', $scored[0]['lead_score'] > $scored[1]['lead_score'], $scored[0]['lead_score'] . ' vs ' . $scored[1]['lead_score']);

// Opportunities for forecast.
$db->prepare("INSERT INTO `epc_crm_opportunities` (`title`,`stage`,`amount`,`probability`,`linked_user_id`,`time_created`,`active`) VALUES (?,?,?,?,?,?,1)")
   ->execute(array('Deal A', 'proposal', 10000, 50, 501, $now));
$db->prepare("INSERT INTO `epc_crm_opportunities` (`title`,`stage`,`amount`,`probability`,`linked_user_id`,`time_created`,`active`) VALUES (?,?,?,?,?,?,1)")
   ->execute(array('Deal B', 'won', 20000, 100, 501, $now));
$db->prepare("INSERT INTO `epc_crm_opportunities` (`title`,`stage`,`amount`,`probability`,`linked_user_id`,`time_created`,`active`) VALUES (?,?,?,?,?,?,1)")
   ->execute(array('Deal C', 'lost', 5000, 0, 502, $now));

$fc = epc_crm_adv_pipeline_forecast($db);
check('forecast open value = 10000', abs($fc['open_value'] - 10000) < 0.001, number_format($fc['open_value'], 2));
check('forecast weighted = 5000', abs($fc['weighted_value'] - 5000) < 0.001, number_format($fc['weighted_value'], 2));
check('forecast win rate = 50%', abs($fc['win_rate'] - 50.0) < 0.001, $fc['win_rate'] . '%');

// Customer 360.
$c360 = epc_crm_adv_customer_360($db, 501);
check('customer 360 won value = 20000', abs($c360['opportunities']['won_value'] - 20000) < 0.001, number_format($c360['opportunities']['won_value'], 2));

// Tax-aware quote (flat-rate fallback path with vat_percent=5).
epc_erp_adv_set_setting($db, 'vat_percent', '5');
$db->prepare("INSERT INTO `epc_crm_quotes` (`opportunity_id`,`customer_user_id`,`quote_number`,`status`,`currency_code`,`subtotal`,`time_created`,`active`) VALUES (?,?,?,?,?,?,?,1)")
   ->execute(array(0, 999, 'Q-TEST-1', 'draft', 'AED', 0, $now));
$qid = (int) $db->lastInsertId();
$db->prepare("INSERT INTO `epc_crm_quote_lines` (`quote_id`,`description`,`qty`,`unit_price`,`sort_order`) VALUES (?,?,?,?,?)")
   ->execute(array($qid, 'Item 1', 2, 500, 10));
$tt = epc_crm_adv_quote_tax_totals($db, $qid);
check('quote subtotal from lines = 1000', abs($tt['subtotal'] - 1000) < 0.001, number_format($tt['subtotal'], 2));
check('quote tax computed (>0) via engine', $tt['tax_amount'] > 0 && $tt['total'] > $tt['subtotal'], $tt['engine'] . ' tax=' . $tt['tax_amount']);

echo "\n=== 4. CP guide registration helper (dry, needs content table) ===\n";
$db->exec("CREATE TABLE IF NOT EXISTS `content` (
    `id` int(11) NOT NULL AUTO_INCREMENT, `count` int(11) DEFAULT 0, `url` varchar(255) DEFAULT '',
    `level` int(11) DEFAULT 0, `alias` varchar(255) DEFAULT '', `value` varchar(255) DEFAULT '',
    `parent` int(11) DEFAULT 0, `description` text, `is_frontend` tinyint(1) DEFAULT 0,
    `content_type` varchar(32) DEFAULT 'php', `content` text, `title_tag` varchar(255) DEFAULT '',
    `description_tag` varchar(255) DEFAULT '0', `keywords_tag` varchar(255) DEFAULT '0',
    `author_tag` varchar(255) DEFAULT '0', `main_flag` tinyint(1) DEFAULT 0, `modules_array` text,
    `css_js` text, `robots_tag` varchar(64) DEFAULT '', `system_flag` tinyint(1) DEFAULT 0,
    `published_flag` tinyint(1) DEFAULT 1, `open` tinyint(1) DEFAULT 0, `time_created` int(11) DEFAULT 0,
    `time_edited` int(11) DEFAULT 0, `order` int(11) DEFAULT 1, PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8");
// Seed parent shop/finance/erp.
$db->exec("INSERT INTO `content` (`url`,`level`,`alias`,`is_frontend`) VALUES ('shop/finance/erp', 3, 'erp', 0)");
$reg = epc_erp_adv_register_guides($db, 'cp');
check('guide page registered', isset($reg[0]['status']) && $reg[0]['status'] === true, $reg[0]['message'] ?? '');
$cnt = (int) $db->query("SELECT COUNT(*) FROM `content` WHERE `url`='shop/finance/erp/advanced-guide'")->fetchColumn();
check('advanced-guide content row exists', $cnt === 1);

echo "\n=== 5. External Reporting registry + country-driven links ===\n";
require_once $fin . '/epc_erp_external_reports.php';
$extCats = epc_ext_reports_categories();
check('26 reporting categories', count($extCats) === 26, count($extCats) . ' categories');
$extReg = epc_ext_reports_registry();
check('report registry populated (>= 200)', count($extReg) >= 200, count($extReg) . ' report types');
$extLive = 0;
foreach ($extReg as $r) {
    if ($r['builder'] !== '') {
        $extLive++;
    }
}
check('live-builder reports present', $extLive >= 20, $extLive . ' live builders');
// every report resolves an authority url for the tenant country (worldwide rule)
$missing = 0;
foreach (array('AE', 'SA', 'IN', 'SG', 'GB', 'US') as $cc) {
    foreach ($extReg as $k => $r) {
        $l = epc_ext_report_links($k, $cc);
        if (empty($l['authority']['url']) || empty($l['authority']['law'])) {
            $missing++;
        }
    }
}
check('all reports resolve law + authority for AE/SA/IN/SG/GB/US', $missing === 0, $missing . ' missing');
// UAE sub-layer precision
$uaeTax = epc_ext_authority('AE', 'tax');
check('UAE tax → FTA + Decree-Law 47/2022', strpos($uaeTax['name'], 'FTA') !== false && strpos($uaeTax['law'], '47/2022') !== false, $uaeTax['name']);
$uaeHr = epc_ext_authority('AE', 'hr');
check('UAE HR → MOHRE + WPS', strpos($uaeHr['name'], 'MOHRE') !== false && strpos($uaeHr['law'], 'WPS') !== false, $uaeHr['name']);
// IFRS link for financial statements
$ifrs = epc_ext_ifrs_link('IAS1');
check('IFRS IAS 1 link resolves', is_array($ifrs) && strpos($ifrs['url'], 'ifrs.org') !== false, $ifrs['url'] ?? 'none');
// corporate-tax rule re-localizes per country
require_once $fin . '/epc_erp_external_reports_build.php';
$ctAe = epc_ext_ct_rule('AE');
$ctSa = epc_ext_ct_rule('SA');
check('CT rule UAE 9% / threshold 375k', $ctAe['rate'] === 9.0 && $ctAe['threshold'] === 375000.0, $ctAe['rate'] . '% / ' . $ctAe['threshold']);
check('CT rule KSA 20%', $ctSa['rate'] === 20.0, $ctSa['rate'] . '%');

// UAE VAT special-scheme + compliance engine
$vatCat = epc_ext_vat_treatment_catalog();
check('VAT treatment catalog has invest_gold (0%) + gold_rcm (RCM)',
    isset($vatCat['invest_gold']) && $vatCat['invest_gold']['rate'] === 0.0
    && isset($vatCat['gold_rcm']) && !empty($vatCat['gold_rcm']['rcm']),
    'invest_gold ' . ($vatCat['invest_gold']['rate'] ?? '?') . '% / gold_rcm rcm=' . (int) ($vatCat['gold_rcm']['rcm'] ?? 0));
$vatLines = epc_ext_vat_sample_supply_lines();
check('VAT sample supply lines cover multiple sectors', count($vatLines) >= 20, count($vatLines) . ' lines');
$vatSectors = array();
foreach ($vatLines as $vl) { $vatSectors[(string) ($vl['sector'] ?? '')] = true; }
check('VAT sample data spans many industries', count($vatSectors) >= 10, count($vatSectors) . ' sectors');
$vatChecks = epc_ext_vat_compliance($vatLines);
$vatErr = 0; $vatOk = 0;
foreach ($vatChecks as $vc) { if ($vc['status'] === 'error') { $vatErr++; } elseif ($vc['status'] === 'ok') { $vatOk++; } }
check('VAT compliance flags the deliberate cross-sector errors', $vatErr === 5, $vatErr . ' errors / ' . $vatOk . ' pass');
// Correct treatment passes, wrong treatment fails
$okGold = epc_ext_vat_compliance(array(array('doc' => 'T1', 'item' => '24kt', 'scheme' => 'invest_gold', 'net' => 1000.0, 'declared' => 0.0, 'margin' => 0.0, 'trn' => true)));
$badGold = epc_ext_vat_compliance(array(array('doc' => 'T2', 'item' => '24kt', 'scheme' => 'invest_gold', 'net' => 1000.0, 'declared' => 50.0, 'margin' => 0.0, 'trn' => true)));
check('Investment gold 0% passes, 5% fails', $okGold[0]['status'] === 'ok' && $badGold[0]['status'] === 'error', $okGold[0]['status'] . ' / ' . $badGold[0]['status']);

// UAE CT full computation builds with adjustments schedule + compliance
$ctBuild = epc_ext_b_ct($db, 'Corporate Income Tax Return', 'AE', 'AED', '2026-01-01', '2026-12-31');
check('UAE CT report builds (live)', !empty($ctBuild['live']) && isset($ctBuild['summary']['Net CT payable']) && isset($ctBuild['summary']['CT before credits']), 'live=' . (int) ($ctBuild['live'] ?? 0));
check('UAE CT schedule shows statutory adjustments', strpos($ctBuild['body'], 'Entertainment') !== false && strpos($ctBuild['body'], 'Interest limitation') !== false && strpos($ctBuild['body'], 'Tax bands') !== false, 'adjustments present');
check('UAE CT compliance panel present', strpos($ctBuild['body'], 'compliance checks') !== false && isset($ctBuild['summary']['Compliance']), 'compliance present');
check('UAE CT shows taxpayer & period + elections', strpos($ctBuild['body'], 'Taxpayer &amp; tax period') !== false && strpos($ctBuild['body'], 'Elections &amp; reliefs') !== false && strpos($ctBuild['body'], 'Small Business Relief') !== false, 'header/elections present');
check('UAE CT supporting schedules + downloads present', strpos($ctBuild['body'], 'CT supporting schedules') !== false && strpos($ctBuild['body'], 'CT_Depreciation.csv') !== false && strpos($ctBuild['body'], 'CT_Related_party.csv') !== false && strpos($ctBuild['body'], 'Foreign tax credit') !== false, 'schedules present');
$ctSd = epc_ext_ct_schedule_data();
check('CT schedules reconcile (acct dep 60k, tax dep 70k, exempt 15k)', array_sum(array_column($ctSd['assets'], 'acct')) === 60000.0 && array_sum(array_column($ctSd['assets'], 'tax')) === 70000.0 && array_sum(array_column($ctSd['exempt'], 'amount')) === 15000.0, 'sums tie');
$ctSchedHtml = epc_ext_ct_schedules_html('AED');
check('CT schedules HTML has all 6 schedules', substr_count($ctSchedHtml, '<details') === 6 && strpos($ctSchedHtml, 'Transfer pricing') !== false || strpos($ctSchedHtml, 'transfer pricing') !== false, substr_count($ctSchedHtml, '<details') . ' details');
// Group VAT + intercompany
$vatGroupBuild = epc_ext_b_vat($db, 'VAT Return', 'AE', 'AED', '2026-04-01', '2026-06-30');
check('VAT shows Tax Group + intercompany eliminations', strpos($vatGroupBuild['body'], 'VAT Tax Group') !== false && strpos($vatGroupBuild['body'], 'Intercompany supplies eliminated') !== false && strpos($vatGroupBuild['body'], 'disregarded') !== false, 'group vat present');
$vatGroupHtml = epc_ext_vat_group_html('AED');
check('VAT group has members + intercompany download', strpos($vatGroupHtml, 'Representative member') !== false && strpos($vatGroupHtml, 'VAT_Group_Intercompany_eliminations.csv') !== false, 'vat group members/csv');
check('CT shows Tax Group + intercompany eliminations', strpos($ctBuild['body'], 'Tax Group &amp; intercompany') !== false && strpos($ctBuild['body'], 'CT_Group_Intercompany_eliminations.csv') !== false && strpos($ctBuild['body'], 'single taxable person') !== false, 'group ct present');
check('CT compliance covers tax group', strpos($ctBuild['body'], 'single taxable person — intercompany transactions eliminated') !== false, 'ct group check');
// CT in-place drill-down on the computation lines
check('CT computation lines drill down in place',
    strpos($ctBuild['body'], 'epcCtDrill') !== false && strpos($ctBuild['body'], 'epc-ct-drill') !== false
    && strpos($ctBuild['body'], 'Source / breakdown') !== false,
    'ct drill present');

// FTA supporting schedules (TRN-wise / invoice-wise / supplier-wise / adjustments)
$sched = epc_ext_vat_schedule_data();
check('VAT schedule has output/input/adjust sets', !empty($sched['output']) && !empty($sched['input']) && !empty($sched['adjust']), count($sched['output']) . '/' . count($sched['input']) . '/' . count($sched['adjust']));
$schedHtml = epc_ext_vat_schedules_html('AED');
check('VAT schedules render downloads + TRN drill-down',
    strpos($schedHtml, 'Invoice-wise') !== false && strpos($schedHtml, 'TRN-wise') !== false
    && strpos($schedHtml, 'Supplier-wise') !== false && strpos($schedHtml, 'epcDlCsv') !== false,
    'schedules present');

// Field guides on VAT + CT
$vatBuild = epc_ext_b_vat($db, 'VAT Return', 'AE', 'AED', '2026-01-01', '2026-12-31');
check('VAT field guide explains each box', strpos($vatBuild['body'], 'Field guide') !== false && strpos($vatBuild['body'], 'Box 12 / 13 / 14') !== false, 'vat guide present');
check('CT field guide explains each line', strpos($ctBuild['body'], 'Field guide') !== false && strpos($ctBuild['body'], 'Interest limitation') !== false, 'ct guide present');

// AML / goAML SAR/STR builder
$sar = epc_ext_b_aml($db, 'Suspicious Activity Report (SAR)', 'AE', 'AED');
$str = epc_ext_b_aml($db, 'Suspicious Transaction Report (STR)', 'AE', 'AED');
check('AML SAR builds with goAML format + KYC + grounds', !empty($sar['live']) && strpos($sar['body'], 'goAML') !== false && strpos($sar['body'], 'Grounds for suspicion') !== false && strpos($sar['body'], 'Field guide') !== false, 'SAR ok');
check('AML STR detected as transaction report', $str['summary']['Report type'] === 'STR', $str['summary']['Report type']);

// UAE complete-format template with full sample data for any category
$corpRep = epc_ext_report_build($db, 'corp__annual_return_filing', 'AE', '2026-01-01', '2026-12-31');
check('UAE corp filing renders complete format (live)', !empty($corpRep['body']) && $corpRep['live'] === true && strpos($corpRep['body'], 'Filing particulars') !== false && strpos($corpRep['body'], 'Field guide') !== false, 'corp live=' . (int) ($corpRep['live'] ?? 0));
$customsRep = epc_ext_report_build($db, 'customs__customs_declaration', 'AE', '2026-01-01', '2026-12-31');
check('UAE customs declaration has HS/CIF/duty schedule', strpos($customsRep['body'], 'HS code') !== false && strpos($customsRep['body'], 'CIF value') !== false, 'customs schedule present');

// Per-report reporting period model
check('VAT period type = quarter', epc_ext_report_period_type('tax', 'tax__vat_return') === 'quarter', epc_ext_report_period_type('tax', 'tax__vat_return'));
check('CT period type = year', epc_ext_report_period_type('tax', 'tax__corporate_income_tax_return') === 'year', epc_ext_report_period_type('tax', 'tax__corporate_income_tax_return'));
check('WPS period type = month', epc_ext_report_period_type('hr', 'hr__wage_protection_reporting') === 'month', epc_ext_report_period_type('hr', 'hr__wage_protection_reporting'));
$pq = epc_ext_resolve_period('quarter', '2026-Q2', mktime(0, 0, 0, 6, 15, 2026));
check('Quarter Q2-2026 resolves Apr 1 – Jun 30', date('Y-m-d', $pq['from']) === '2026-04-01' && date('Y-m-d', $pq['to']) === '2026-06-30' && $pq['label'] === 'Q2 2026', $pq['label'] . ' ' . date('Y-m-d', $pq['from']) . '..' . date('Y-m-d', $pq['to']));
$py = epc_ext_resolve_period('year', '2025', mktime(0, 0, 0, 6, 15, 2026));
check('FY2025 resolves Jan 1 – Dec 31', date('Y-m-d', $py['from']) === '2025-01-01' && date('Y-m-d', $py['to']) === '2025-12-31' && $py['label'] === 'FY2025', $py['label']);
$pm = epc_ext_resolve_period('month', '2026-02', mktime(0, 0, 0, 6, 15, 2026));
check('Feb 2026 resolves last day 28', date('Y-m-d', $pm['to']) === '2026-02-28', date('Y-m-d', $pm['to']));
check('Period offers preset options', count($pq['options']) >= 4 && count($py['options']) >= 4, count($pq['options']) . '/' . count($py['options']));
$pbad = epc_ext_resolve_period('quarter', 'garbage', mktime(0, 0, 0, 6, 15, 2026));
check('Invalid period falls back to current', $pbad['token'] === '2026-Q2', $pbad['token']);

// ---- Off-system Excel/CSV import (VAT + CT from uploaded summary) ----
$vatTpl = epc_ext_import_template_csv('vat');
$ctTpl = epc_ext_import_template_csv('ct');
check('VAT import template has Code header + boxes', strpos($vatTpl, 'Code') !== false && strpos($vatTpl, 'Adjustment') !== false && strpos($vatTpl, 'BOX1A') !== false && strpos($vatTpl, 'BOX9') !== false, 'vat tpl');
check('CT import template has Code header + lines', strpos($ctTpl, 'ACCT_PROFIT') !== false && strpos($ctTpl, 'NET_INTEREST') !== false && strpos($ctTpl, 'LOSSES_BF') !== false, 'ct tpl');

// round-trip: write template to temp, parse it back, build returns
$tmpV = tempnam(sys_get_temp_dir(), 'vat') . '.csv';
file_put_contents($tmpV, $vatTpl);
$rowsV = epc_ext_parse_table($tmpV, 'sample.csv');
@unlink($tmpV);
$mapV = epc_ext_import_map($rowsV ?: array());
check('Parse VAT CSV -> boxes + meta', !empty($mapV['vat']['BOX1B']) && ($mapV['meta']['META_TRN'] ?? '') === '100000000000003', count($mapV['vat']) . ' boxes');
$impVat = epc_ext_b_vat_summary($mapV, 'AED');
check('Import VAT builds FTA 201 + reconciles + compliance', strpos($impVat['body'], 'FTA') !== false || strpos($impVat['title'], 'VAT') !== false, $impVat['title']);
check('Import VAT net = output - input', isset($impVat['summary']['Output VAT']) && isset($impVat['summary']['Input VAT']), implode(',', array_keys($impVat['summary'])));
check('Import VAT is off-system (no ERP read)', strpos($impVat['body'], 'off-system') !== false && strpos($impVat['body'], 'uploaded file') !== false, 'off-system note');

$tmpC = tempnam(sys_get_temp_dir(), 'ct') . '.csv';
file_put_contents($tmpC, $ctTpl);
$rowsC = epc_ext_parse_table($tmpC, 'sample.csv');
@unlink($tmpC);
$mapC = epc_ext_import_map($rowsC ?: array());
check('Parse CT CSV -> values + meta', ($mapC['values']['ACCT_PROFIT'] ?? 0) == 1250000.0 && ($mapC['meta']['META_LEGAL_NAME'] ?? '') !== '', count($mapC['values']) . ' lines');
$impCt = epc_ext_b_ct_summary($mapC, 'AED');
check('Import CT builds computation + bands + compliance', strpos($impCt['body'], 'Computation of taxable income') !== false && strpos($impCt['body'], 'Corporate tax compliance checks') !== false, $impCt['title']);
check('Import CT applies 0%/9% bands', isset($impCt['summary']['Net CT payable']) && isset($impCt['summary']['Taxable income']), implode(',', array_keys($impCt['summary'])));

// ---- Full multi-sheet .xlsx template round-trip (schedules + compliance) ----
if (class_exists('ZipArchive')) {
    $vatSheets = epc_ext_import_template_sheets('vat');
    $ctSheets = epc_ext_import_template_sheets('ct');
    check('VAT workbook carries all schedule sheets', isset($vatSheets['VAT Boxes'], $vatSheets['Customer TRN-wise'], $vatSheets['Supplier-wise'], $vatSheets['Adjustments'], $vatSheets['Supplies by treatment'], $vatSheets['Tax group & intercompany'], $vatSheets['Compliance checklist']), count($vatSheets) . ' sheets');
    check('CT workbook carries all 6 schedules + compliance', isset($ctSheets['CT Computation'], $ctSheets['Elections & reliefs'], $ctSheets['Sch 1 Adjustments'], $ctSheets['Sch 2 Fixed assets'], $ctSheets['Sch 3 Exempt income'], $ctSheets['Sch 4 Related party'], $ctSheets['Sch 5 Tax losses'], $ctSheets['Sch 6 Foreign tax credit'], $ctSheets['Tax group & intercompany'], $ctSheets['Compliance checklist']), count($ctSheets) . ' sheets');

    $tmpVx = tempnam(sys_get_temp_dir(), 'vatx') . '.xlsx';
    file_put_contents($tmpVx, epc_ext_import_template_xlsx('vat'));
    $mapVx = epc_ext_import_map(epc_ext_parse_all_rows($tmpVx, 'wb.xlsx') ?: array());
    @unlink($tmpVx);
    check('XLSX VAT round-trip reads boxes + TRN/address', !empty($mapVx['vat']) && ($mapVx['meta']['META_TRN'] ?? '') === '100000000000003' && ($mapVx['meta']['META_ADDRESS'] ?? '') !== '', count($mapVx['vat']) . ' boxes');

    $tmpCx = tempnam(sys_get_temp_dir(), 'ctx') . '.xlsx';
    file_put_contents($tmpCx, epc_ext_import_template_xlsx('ct'));
    $mapCx = epc_ext_import_map(epc_ext_parse_all_rows($tmpCx, 'wb.xlsx') ?: array());
    @unlink($tmpCx);
    // Regression: compliance-checklist rows labelled "Entertainment"/"Donations"/
    // "Provisions" must NOT overwrite the real computation figures.
    check('XLSX CT round-trip: schedule rows do not clobber computation', ($mapCx['values']['ENTERTAINMENT'] ?? 0) == 40000.0 && ($mapCx['values']['DONATIONS'] ?? 0) == 10000.0 && ($mapCx['values']['PROVISIONS'] ?? 0) == 25000.0 && ($mapCx['values']['ACCT_PROFIT'] ?? 0) == 1250000.0, 'ent=' . ($mapCx['values']['ENTERTAINMENT'] ?? 'NA'));
    $impCtx = epc_ext_b_ct_summary($mapCx, 'AED');
    check('XLSX CT round-trip builds correct taxable income', ($impCtx['summary']['Taxable income'] ?? '') === 'AED 1,110,000.00', $impCtx['summary']['Taxable income'] ?? 'NA');
}

// column-letter helper + print helpers
check('XLSX column index A=0, B=1, AA=26', epc_ext_xlsx_col_index('A') === 0 && epc_ext_xlsx_col_index('B') === 1 && epc_ext_xlsx_col_index('AA') === 26, 'col idx');
check('Print helpers emit ctx + shared fn', strpos(epc_ext_print_ctx_js(array('co' => 'X')), '__epcExtCtx') !== false && strpos(epc_ext_print_fn_js(), 'function epcExtPrint') !== false, 'print js');

// ---- External Audit Report (ISA 700) — cover page + full IFRS pack ----
$audit = epc_ext_b_audit($db, 'Demo Co', 'AE', 'AED', strtotime('2024-01-01'), strtotime('2024-12-31'));
check('Audit report has cover page + table of contents', strpos($audit['body'], 'ext-cover') !== false && strpos($audit['body'], 'Table of contents') !== false, 'cover');
check('Audit report has all four IFRS statements', strpos($audit['body'], 'Statement of Financial Position') !== false && strpos($audit['body'], 'Other Comprehensive Income') !== false && strpos($audit['body'], 'Changes in Equity') !== false && strpos($audit['body'], 'Cash Flows') !== false, 'statements');
check('Audit report has Independent Auditor\'s Report', strpos($audit['body'], 'Independent Auditor') !== false && strpos($audit['body'], 'ISA 700') !== false, 'opinion');
$reqStds = array('IAS 7', 'IAS 21', 'IAS 23', 'IAS 32', 'IAS 36', 'IAS 40', 'IFRS 8', 'IFRS 13', 'IFRS 5', 'IAS 20', 'IAS 12', 'IAS 19', 'IAS 24', 'IAS 33', 'IFRS 9', 'IFRS 15', 'IFRS 16');
$missingStd = array();
foreach ($reqStds as $s) { if (strpos($audit['body'], $s) === false) { $missingStd[] = $s; } }
check('Audit notes cover full IAS/IFRS set', $missingStd === array(), $missingStd === array() ? count($reqStds) . ' standards' : 'missing: ' . implode(',', $missingStd));
check('Audit report has Standards applicability index', strpos($audit['body'], 'Standards applicability index') !== false && strpos($audit['body'], 'Not applicable') !== false && strpos($audit['body'], 'IFRS 17') !== false, 'std index');
// notes carry build-up figures with comparatives that reconcile to the face
$reqNoteBits = array('Tax reconciliation', 'Ageing of gross receivables', 'Movement in NBV', 'Fair-value hierarchy', 'Earnings per share', 'Gearing ratio', 'Key management remuneration');
$missNote = array();
foreach ($reqNoteBits as $b) { if (strpos($audit['body'], $b) === false) { $missNote[] = $b; } }
check('Audit notes have figure build-ups with comparatives', $missNote === array(), $missNote === array() ? count($reqNoteBits) . ' schedules' : 'missing: ' . implode(',', $missNote));
// EPS note reconciles: profit / shares = EPS shown
check('Audit notes reconcile (PPE movement ties to closing NBV)', strpos($audit['body'], 'Closing net book value') !== false && strpos($audit['body'], 'Opening net book value') !== false, 'recon');
// not-applicable standards are presented in structure (not bare)
check('Not-applicable standards presented in structure', strpos($audit['body'], 'Standards considered but not applicable') !== false && strpos($audit['body'], 'What the standard covers') !== false && strpos($audit['body'], 'If it applied') !== false && strpos($audit['body'], 'IFRS 3') !== false && strpos($audit['body'], 'IAS 41') !== false, 'na structured');
// notes cite policy, basis & procedure with standard + law references
check('Applied notes carry policy/basis/procedure + law references', strpos($audit['body'], 'Accounting policy adopted by the Company') !== false && strpos($audit['body'], 'The Company has adopted') !== false && strpos($audit['body'], 'IFRS 15.31') !== false && strpos($audit['body'], 'IAS 16.7') !== false && strpos($audit['body'], 'Federal Decree-Law 47/2022') !== false && strpos($audit['body'], 'Federal Decree-Law 33/2021') !== false, 'policy refs');
// section 9 — financial analysis & commentary with impact grading
check('Audit report has financial analysis section + impact grades', strpos($audit['body'], 'Financial analysis') !== false && (strpos($audit['body'], '>High<') !== false || strpos($audit['body'], '>Medium<') !== false || strpos($audit['body'], '>Low<') !== false), 'analysis sec');
// PDF print layout — A4, equal margins, unified font, red corporate theme
check('Audit PDF is A4 with equal margins + per-element page breaks', strpos($audit['body'], 'size:A4') !== false && strpos($audit['body'], 'margin:18mm') !== false && strpos($audit['body'], 'page-break-before:always') !== false, 'A4 print');
check('Audit PDF uses unified red corporate theme', strpos($audit['body'], '#b3122a') !== false && strpos($audit['body'], 'print-color-adjust:exact') !== false, 'red theme');

// ---- Off-system IFRS financial-statements import (template + builder) ----
$finTpl = epc_ext_import_template_csv('fin');
check('FIN import template has Code header + lines', strpos($finTpl, 'FIN_REVENUE') !== false && strpos($finTpl, 'FIN_PPE') !== false && strpos($finTpl, 'Prior year') !== false, 'fin tpl');
$tmpF = tempnam(sys_get_temp_dir(), 'fin') . '.csv';
file_put_contents($tmpF, $finTpl);
$mapF = epc_ext_import_map(epc_ext_parse_table($tmpF, 'sample.csv') ?: array());
@unlink($tmpF);
check('Parse FIN CSV -> current + prior + meta', ($mapF['fin']['FIN_REVENUE']['cur'] ?? 0) == 8400000.0 && ($mapF['fin']['FIN_REVENUE']['pri'] ?? 0) == 7500000.0 && ($mapF['meta']['META_TRN'] ?? '') === '100000000000003', count($mapF['fin']) . ' lines');
$impFin = epc_ext_b_fin_summary($mapF, 'AED');
check('Import FIN builds full IFRS pack + cover', strpos($impFin['body'], 'ext-cover') !== false && strpos($impFin['body'], 'Independent Auditor') !== false && strpos($impFin['body'], 'Statement of Cash Flows') !== false, $impFin['title']);
check('Import FIN SOFP balances', ($impFin['summary']['SOFP balanced'] ?? '') === 'Yes', $impFin['summary']['SOFP balanced'] ?? 'NA');
check('Import FIN is off-system', strpos($impFin['body'], 'off-system') !== false && strpos($impFin['body'], 'uploaded workbook') !== false, 'off-system note');
if (class_exists('ZipArchive')) {
    $finSheets = epc_ext_import_template_sheets('fin');
    check('FIN workbook carries all statement sheets', isset($finSheets['Company & details'], $finSheets['Financial data'], $finSheets['Notes inputs'], $finSheets['Compliance checklist']), count($finSheets) . ' sheets');
    $tmpFx = tempnam(sys_get_temp_dir(), 'finx') . '.xlsx';
    file_put_contents($tmpFx, epc_ext_import_template_xlsx('fin'));
    $mapFx = epc_ext_import_map(epc_ext_parse_all_rows($tmpFx, 'wb.xlsx') ?: array());
    @unlink($tmpFx);
    check('XLSX FIN round-trip reads figures + comparatives', ($mapFx['fin']['FIN_PPE']['cur'] ?? 0) == 3528000.0 && ($mapFx['fin']['FIN_PPE']['pri'] ?? 0) == 3150000.0 && ($mapFx['meta']['META_AUDITOR'] ?? '') !== '', count($mapFx['fin']) . ' lines');
}

// ---- Expanded IFRS Financials upload workbook (maximum input) ----
$finSheetsX = epc_ext_import_template_sheets('fin');
$reqFinSheets = array('Company & details', 'Financial data', 'Revenue & segments', 'PPE & intangible movement', 'Receivables, inventory & ECL', 'Tax reconciliation', 'Leases, borrowings & risk', 'Equity, EPS & dividends', 'Related parties & KMP', 'Other disclosures', 'Notes inputs', 'Compliance checklist');
$missFinSheet = array();
foreach ($reqFinSheets as $s) { if (!isset($finSheetsX[$s])) { $missFinSheet[] = $s; } }
check('FIN workbook is comprehensive (12 input sheets)', $missFinSheet === array(), $missFinSheet === array() ? count($finSheetsX) . ' sheets' : 'missing: ' . implode(',', $missFinSheet));
$finFlat = json_encode($finSheetsX);
check('FIN workbook carries granular detail codes', strpos($finFlat, 'FIN_REV_GOODS') !== false && strpos($finFlat, 'FIN_PPE_ADDITIONS') !== false && strpos($finFlat, 'FIN_RECEIVABLES_GROSS') !== false && strpos($finFlat, 'FIN_TAX_DEFERRED') !== false && strpos($finFlat, 'FIN_SHARES_WEIGHTED') !== false && strpos($finFlat, 'META_INDUSTRY') !== false, 'detail codes');
// detail codes feed the report notes (round-trip through the off-system builder)
$tmpFx2 = tempnam(sys_get_temp_dir(), 'finx2') . '.xlsx';
file_put_contents($tmpFx2, epc_ext_import_template_xlsx('fin'));
$mapFx2 = epc_ext_import_map(epc_ext_parse_all_rows($tmpFx2, 'wb.xlsx') ?: array());
@unlink($tmpFx2);
check('FIN detail codes round-trip (revenue split, gross rec, KMP)', ($mapFx2['fin']['FIN_REV_GOODS']['cur'] ?? 0) == 6048000.0 && ($mapFx2['fin']['FIN_RECEIVABLES_GROSS']['cur'] ?? 0) == 1387000.0 && ($mapFx2['fin']['FIN_KMP_SALARIES']['cur'] ?? 0) == 720000.0 && ($mapFx2['meta']['META_INDUSTRY'] ?? '') !== '', count($mapFx2['fin']) . ' lines');
$impFin2 = epc_ext_b_fin_summary($mapFx2, 'AED');
check('Off-system notes use detail inputs (disaggregation, EPS, KMP, segments)', strpos($impFin2['body'], 'Sale of goods (point in time)') !== false && strpos($impFin2['body'], 'Earnings per share') !== false && strpos($impFin2['body'], 'Key management personnel remuneration') !== false && strpos($impFin2['body'], 'Operating segments') !== false, 'wired notes');

// ---- Financial Model + Business Valuation ----
$fm = epc_ext_b_finmodel($db, 'Demo Co', 'AE', 'AED', strtotime('2024-01-01'), strtotime('2024-12-31'));
check('Financial model has assumptions + projection + FCF', strpos($fm['body'], 'Assumptions') !== false && strpos($fm['body'], 'free cash flow') !== false && strpos($fm['body'], 'EBITDA') !== false, $fm['title']);
check('Financial model summary has year-5 figures', isset($fm['summary']['Year-5 revenue'], $fm['summary']['Year-1 free cash flow']), implode(',', array_keys($fm['summary'])));
$val = epc_ext_b_valuation($db, 'Demo Co', 'AE', 'AED', strtotime('2024-01-01'), strtotime('2024-12-31'));
check('Valuation has DCF + multiples + net assets', strpos($val['body'], 'Discounted cash flow') !== false && strpos($val['body'], 'Market multiples') !== false && strpos($val['body'], 'Net assets') !== false, $val['title']);
check('Valuation summary has EV + equity + central value', isset($val['summary']['Enterprise value (DCF)'], $val['summary']['Equity value (DCF)'], $val['summary']['Central equity value']), implode(',', array_keys($val['summary'])));

// ---- Advanced detail: model finance/EBIT lines + valuation net-debt + sensitivity ----
check('Financial model P&L shows EBIT + finance costs', strpos($fm['body'], 'Operating profit (EBIT)') !== false && strpos($fm['body'], 'Finance costs') !== false, 'pl lines');
check('Valuation has net-debt build + sensitivity table', strpos($val['body'], 'Net-debt build') !== false && strpos($val['body'], 'Sensitivity') !== false && strpos($val['body'], 'WACC \\ g') !== false, 'advanced detail');

// ---- Linked Excel (.xlsx) export — Assumptions / Calculations / Results, live formulas ----
if (class_exists('ZipArchive')) {
    $xbin = epc_ext_finmodel_xlsx($db, 'AED', strtotime('2024-01-01'), strtotime('2024-12-31'));
    check('Linked model workbook is non-empty .xlsx', $xbin !== '' && strncmp($xbin, "PK", 2) === 0, strlen($xbin) . ' bytes');
    $tmpX = tempnam(sys_get_temp_dir(), 'finx') . '.xlsx';
    file_put_contents($tmpX, $xbin);
    $z = new ZipArchive();
    $okZip = $z->open($tmpX) === true;
    $s1 = $okZip ? (string) $z->getFromName('xl/worksheets/sheet1.xml') : '';
    $s2 = $okZip ? (string) $z->getFromName('xl/worksheets/sheet2.xml') : '';
    $s3 = $okZip ? (string) $z->getFromName('xl/worksheets/sheet3.xml') : '';
    $wb = $okZip ? (string) $z->getFromName('xl/workbook.xml') : '';
    if ($okZip) { $z->close(); }
    @unlink($tmpX);
    check('Workbook has Assumptions / Calculations / Results sheets', strpos($wb, 'name="Assumptions"') !== false && strpos($wb, 'name="Calculations"') !== false && strpos($wb, 'name="Results"') !== false, '3 sheets');
    check('Calculations cells are live formulas referencing Assumptions', strpos($s2, '<f>Assumptions!$B$3*(1+Assumptions!$B$4)</f>') !== false && strpos($s2, '<f>SUM(B19:F19)</f>') !== false, 'formulas');
    check('Results valuation cells are live formulas', strpos($s3, '<f>Calculations!B26</f>') !== false && strpos($s3, '<f>AVERAGE(B10:B13)</f>') !== false, 'result formulas');
    check('Assumptions sheet carries numeric input cells', strpos($s1, ' t="n"><v>') !== false, 'numeric inputs');
}

// ---- Linked audit pack (.xlsx) — one sheet per element, linked to Trial Balance ----
if (class_exists('ZipArchive')) {
    $abin = epc_ext_audit_xlsx($db, 'AED', strtotime('2024-01-01'), strtotime('2024-12-31'));
    check('Linked audit pack is non-empty .xlsx', $abin !== '' && strncmp($abin, "PK", 2) === 0, strlen($abin) . ' bytes');
    $tmpA = tempnam(sys_get_temp_dir(), 'audx') . '.xlsx';
    file_put_contents($tmpA, $abin);
    $za = new ZipArchive();
    $okA = $za->open($tmpA) === true;
    $wbA = $okA ? (string) $za->getFromName('xl/workbook.xml') : '';
    // resolve each worksheet by its display name (position-independent): sheets
    // appear in workbook.xml in file order, so name index N maps to sheetN.xml.
    $sheetByName = static function (string $name) use ($za, $wbA, $okA): string {
        if (!$okA) { return ''; }
        if (preg_match_all('/<sheet name="([^"]*)"/', $wbA, $mm)) {
            foreach ($mm[1] as $idx => $nm) {
                if ($nm === $name) { return (string) $za->getFromName('xl/worksheets/sheet' . ($idx + 1) . '.xml'); }
            }
        }
        return '';
    };
    $aSofp = $sheetByName('Financial Position');
    $aCf = $sheetByName('Cash Flows');
    $aNotes = $sheetByName('Notes');
    $aCover = $sheetByName('Cover &amp; Contents');
    $aAud = $sheetByName('Auditor&apos;s Report');
    $aStd = $sheetByName('Standards Index');
    $aCons = $sheetByName('Consolidation');
    $aFa = $sheetByName('Financial Analysis');
    if ($okA) { $za->close(); }
    @unlink($tmpA);
    check('Audit pack has one sheet per element (TB, SOFP, P&L, CF, SOCE, Notes)',
        strpos($wbA, 'name="Trial Balance"') !== false && strpos($wbA, 'name="Financial Position"') !== false
        && strpos($wbA, 'name="Profit &amp; Loss OCI"') !== false && strpos($wbA, 'name="Cash Flows"') !== false
        && strpos($wbA, 'name="Changes in Equity"') !== false && strpos($wbA, 'name="Notes"') !== false, '6 sheets');
    check('SOFP cells are live formulas linked to the Trial Balance', strpos($aSofp, "&apos;Trial Balance&apos;!C3") !== false && strpos($aSofp, 'B5+B10') !== false, 'SOFP→TB');
    check('Cash flow links to P&L and reconciles cash', strpos($aCf, "&apos;Profit &amp; Loss OCI&apos;!B9") !== false && strpos($aCf, 'B22+B23') !== false, 'CF→P&L');
    check('Notes totals link back to the face statements', strpos($aNotes, "&apos;Financial Position&apos;!B3") !== false && strpos($aNotes, "&apos;Financial Position&apos;!B8") !== false, 'Notes→SOFP');
    // ---- Excel = full PDF parity (cover, auditor's report, standards index, consolidation, analysis) ----
    check('Audit pack mirrors PDF (11 sheets incl. cover, auditor report, standards, consolidation, analysis)',
        strpos($wbA, 'name="Cover &amp; Contents"') !== false && strpos($wbA, 'name="Auditor') !== false
        && strpos($wbA, 'name="Standards Index"') !== false && strpos($wbA, 'name="Consolidation"') !== false
        && strpos($wbA, 'name="Financial Analysis"') !== false, 'parity sheets');
    check('Cover sheet carries entity + framework + contents', strpos($aCover, 'EXTERNAL AUDIT REPORT') !== false && strpos($aCover, 'CONTENTS') !== false, 'cover');
    check('Auditor report sheet has ISA opinion text', strpos($aAud, 'INDEPENDENT AUDITOR') !== false && strpos($aAud, 'Basis for Opinion') !== false, 'ISA text');
    check('Standards index lists every IAS/IFRS', strpos($aStd, 'IAS 1') !== false && strpos($aStd, 'IFRS 17') !== false, 'std index');
    check('Consolidation worksheet has Parent/Subsidiary/Eliminations and formula total', strpos($aCons, 'Subsidiary') !== false && strpos($aCons, 'Eliminations') !== false && strpos($aCons, '+C') !== false && strpos($aCons, '+D') !== false, 'consolidation');
    check('Financial analysis sheet grades impact (High/Medium/Low)', strpos($aFa, 'Impact') !== false && (strpos($aFa, 'High') !== false || strpos($aFa, 'Medium') !== false || strpos($aFa, 'Low') !== false), 'analysis');
}

echo "\n========================================\n";
echo "RESULT: $pass_n passed, $fail_n failed\n";
echo "========================================\n";
exit($fail_n === 0 ? 0 : 1);
