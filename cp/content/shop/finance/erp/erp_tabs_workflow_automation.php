<?php
/**
 * ERP tab — Workflow Automation (diagnostic v2 — step-by-step error capture).
 */
defined('_ASTEXE_') or die('No access');

$_oldDisplayErrors = ini_get('display_errors');
$_oldErrorReporting = error_reporting();
ini_set('display_errors', '1');
error_reporting(E_ALL);

echo '<div style="padding:20px;background:#e3f2fd;border:2px solid #1565c0;border-radius:8px;margin:20px;">';
echo '<h4 style="color:#0d47a1;margin-top:0;">Workflow Automation — Diagnostic v2</h4>';
echo '<p>PHP ' . phpversion() . ' | db_link: ' . (isset($db_link) ? get_class($db_link) : 'N/A') . '</p>';

echo '<p>Step 1: Loading epc_erp_ui.php... ';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
echo '<span style="color:green">OK</span></p>';

echo '<p>Step 2: Loading epc_erp_helpers.php... ';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';
echo '<span style="color:green">OK</span></p>';

$wfBackendFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_workflow_builder.php';
echo '<p>Step 3: Backend file exists? ' . (is_file($wfBackendFile) ? 'YES' : 'NO') . '</p>';

if (is_file($wfBackendFile)) {
	echo '<p>Step 4: Loading backend... ';
	try {
		require_once $wfBackendFile;
		echo '<span style="color:green">OK</span></p>';
	} catch (\Throwable $e) {
		echo '<span style="color:red">FAILED: ' . htmlspecialchars($e->getMessage()) . ' in ' . htmlspecialchars($e->getFile()) . ':' . $e->getLine() . '</span></p>';
	}
}

echo '<p>Step 5: Schema init... ';
if (isset($db_link) && $db_link instanceof PDO && function_exists('epc_workflow_ensure_schema')) {
	try {
		epc_workflow_ensure_schema($db_link);
		echo '<span style="color:green">OK</span></p>';
	} catch (\Throwable $e) {
		echo '<span style="color:red">FAILED: ' . htmlspecialchars($e->getMessage()) . '</span></p>';
	}
} else {
	echo '<span style="color:orange">skipped</span></p>';
}

echo '<p>Step 6: Getting variables... ';
try {
	$wfAction = isset($_GET['wf_action']) ? (string)$_GET['wf_action'] : 'list';
	$wfId = isset($_GET['wf_id']) ? (int)$_GET['wf_id'] : 0;
	$csrfLocal = isset($csrf) ? $csrf : '';
	$wfBase = epc_erp_tab_url($erpUrl, 'workflow_automation', $date_from_str, $date_to_str, 'setup');
	$triggerTypes = function_exists('epc_workflow_trigger_types') ? epc_workflow_trigger_types() : array();
	$actionTypes = function_exists('epc_workflow_action_types') ? epc_workflow_action_types() : array();
	echo '<span style="color:green">OK</span></p>';
} catch (\Throwable $e) {
	echo '<span style="color:red">FAILED: ' . htmlspecialchars($e->getMessage()) . '</span></p>';
}

echo '<p>Step 7: erp_page_header... ';
try {
	erp_page_header(
		'<i class="fa fa-cogs"></i> Workflow automation',
		'Build no-code workflows: trigger &rarr; condition &rarr; action.',
		array(
			array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
			array('label' => 'System administration'),
			array('label' => 'Workflow automation'),
		),
		array(
			array('label' => 'New workflow', 'url' => $wfBase . '&wf_action=edit', 'class' => 'btn-primary', 'icon' => 'fa-plus'),
		)
	);
	echo '<span style="color:green">OK</span></p>';
} catch (\Throwable $e) {
	echo '<span style="color:red">FAILED: ' . htmlspecialchars($e->getMessage()) . '</span></p>';
}

echo '<p style="color:green;font-weight:bold;">ALL STEPS COMPLETED</p>';
echo '</div>';

ini_set('display_errors', $_oldDisplayErrors);
error_reporting($_oldErrorReporting);
