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
check('UAE CT report builds (live)', !empty($ctBuild['live']) && isset($ctBuild['summary']['CT payable']), 'live=' . (int) ($ctBuild['live'] ?? 0));
check('UAE CT schedule shows statutory adjustments', strpos($ctBuild['body'], 'Entertainment') !== false && strpos($ctBuild['body'], 'Interest limitation') !== false && strpos($ctBuild['body'], 'Tax bands') !== false, 'adjustments present');
check('UAE CT compliance panel present', strpos($ctBuild['body'], 'compliance checks') !== false && isset($ctBuild['summary']['Compliance']), 'compliance present');

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

echo "\n========================================\n";
echo "RESULT: $pass_n passed, $fail_n failed\n";
echo "========================================\n";
exit($fail_n === 0 ? 0 : 1);
