<?php
/**
 * CLI tests for ERP Accounting + BPA Automation Centre.
 *
 *   php tests/erp_advanced/run_automation_centre_tests.php
 */
declare(strict_types=1);

define('_ASTEXE_', 1);

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_automation_catalogue.php';
require_once dirname(__DIR__, 2) . '/content/general_pages/epc_workflow_builder.php';
require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_guide_content.php';

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

section('Catalogue completeness');
$cat = epc_erp_automation_catalogue();
check('catalogue has 20+ automations', count($cat) >= 20);
$acct = epc_erp_automation_by_category('accounting');
$proc = epc_erp_automation_by_category('process');
check('accounting automations >= 8', count($acct) >= 8);
check('process automations >= 10', count($proc) >= 10);
foreach (array('order_to_erp', 'period_close', 'collections_dunning', 'po_approval', 'process_flow_routing') as $need) {
	check("has automation '$need'", isset($cat[$need]));
}
foreach ($cat as $id => $row) {
	$ok = isset($row['name'], $row['category'], $row['pipeline'], $row['desc']) && is_array($row['pipeline']) && count($row['pipeline']) >= 2;
	if (!$ok) {
		echo "    incomplete: $id\n";
	}
	check("automation '$id' well-formed", $ok);
}

section('Workflow templates');
$tpl = epc_erp_automation_workflow_templates();
check('templates >= 10', count($tpl) >= 10);
foreach (array('po_approval_chain', 'invoice_auto_send', 'vat_filing_reminder', 'overdue_escalation') as $t) {
	check("template '$t' exists", isset($tpl[$t]));
	check("template '$t' has steps", isset($tpl[$t]['steps']) && count($tpl[$t]['steps']) >= 1);
}

section('Workflow builder actions');
$actions = epc_workflow_action_types();
$types = array_column($actions, 'type');
foreach (array('send_email', 'send_notification', 'gl_journal', 'credit_check', 'create_task') as $a) {
	check("action type '$a'", in_array($a, $types, true));
}
check('epc_workflow_run_action exists', function_exists('epc_workflow_run_action'));
check('epc_workflow_replace_steps exists', function_exists('epc_workflow_replace_steps'));

section('Schedule due helper');
check('never-run is due after hour', epc_erp_automation_schedule_due(array('cron_expression' => '0 0 * * *'), null) === true);
check('already ran today is not due', epc_erp_automation_schedule_due(array('cron_expression' => '0 0 * * *'), date('Y-m-d H:i:s')) === false);

section('Guide entries');
$g = epc_guide_modules();
check('guide has accounting_automation', isset($g['accounting_automation']));
check('guide has bpa_automation', isset($g['bpa_automation']));
check('accounting guide has setup+daily', !empty($g['accounting_automation']['setup']) && !empty($g['accounting_automation']['daily']));
check('bpa guide has setup+daily', !empty($g['bpa_automation']['setup']) && !empty($g['bpa_automation']['daily']));

section('UI files present');
$root = dirname(__DIR__, 2);
check('workflow_automation tab exists', is_file($root . '/cp/content/shop/finance/erp/erp_tabs_workflow_automation.php'));
check('accounting_automation tab exists', is_file($root . '/cp/content/shop/finance/erp/erp_tabs_accounting_automation.php'));
$nav = file_get_contents($root . '/cp/content/shop/finance/erp/erp_nav_areas.php');
check('nav has accounting_automation', strpos($nav, "'accounting_automation'") !== false);
check('nav has Automation Centre label', strpos($nav, 'Automation Centre') !== false);
$main = file_get_contents($root . '/cp/content/shop/finance/erp/erp_main.php');
check('main maps accounting_automation', strpos($main, "'accounting_automation'") !== false);
$ajax = file_get_contents($root . '/cp/content/shop/finance/erp/ajax_erp.php');
check('ajax has automation_activate', strpos($ajax, "case 'automation_activate'") !== false);
check('ajax has automation_tick', strpos($ajax, "case 'automation_tick'") !== false);
$jobs = file_get_contents($root . '/content/general_pages/epc_platform_jobs.php');
check('platform job erp_automation_tick', strpos($jobs, 'erp_automation_tick') !== false);

echo "\n========================================\n";
echo "AUTOMATION CENTRE TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
