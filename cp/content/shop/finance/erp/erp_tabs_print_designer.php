<?php
/**
 * ERP tab — Print Designer (diagnostic v2 — add backend require with error capture).
 */
defined('_ASTEXE_') or die('No access');

// Force error display so we can see what crashes
$_oldDisplayErrors = ini_get('display_errors');
$_oldErrorReporting = error_reporting();
ini_set('display_errors', '1');
error_reporting(E_ALL);

echo '<div style="padding:20px;background:#fff3e0;border:2px solid #ff9800;border-radius:8px;margin:20px;">';
echo '<h4 style="color:#e65100;margin-top:0;">Print Designer — Diagnostic v2</h4>';
echo '<p>PHP ' . phpversion() . ' | db_link: ' . (isset($db_link) ? get_class($db_link) : 'N/A') . '</p>';

// Step 1: Load UI helpers (same as tenant_config — should be safe)
echo '<p>Step 1: Loading epc_erp_ui.php... ';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
echo '<span style="color:green">OK</span></p>';

echo '<p>Step 2: Loading epc_erp_helpers.php... ';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';
echo '<span style="color:green">OK</span></p>';

// Step 3: Load the print designer backend
$pdBackendFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_print_designer.php';
echo '<p>Step 3: Backend file exists? ' . (is_file($pdBackendFile) ? 'YES' : 'NO') . '</p>';

if (is_file($pdBackendFile)) {
	echo '<p>Step 4: Loading backend... ';
	try {
		require_once $pdBackendFile;
		echo '<span style="color:green">OK</span></p>';
	} catch (\Throwable $e) {
		echo '<span style="color:red">FAILED: ' . htmlspecialchars($e->getMessage()) . ' in ' . htmlspecialchars($e->getFile()) . ':' . $e->getLine() . '</span></p>';
	}
}

// Step 5: Schema init
echo '<p>Step 5: Schema init... ';
if (isset($db_link) && $db_link instanceof PDO) {
	if (function_exists('epc_erp_print_designer_ensure_schema')) {
		try {
			epc_erp_print_designer_ensure_schema($db_link);
			echo '<span style="color:green">OK</span></p>';
		} catch (\Throwable $e) {
			echo '<span style="color:red">FAILED: ' . htmlspecialchars($e->getMessage()) . '</span></p>';
		}
	} else {
		echo '<span style="color:orange">function not found</span></p>';
	}
} else {
	echo '<span style="color:orange">no db_link</span></p>';
}

// Step 6: Seed defaults
echo '<p>Step 6: Seed defaults... ';
if (isset($db_link) && $db_link instanceof PDO && function_exists('epc_erp_print_designer_seed_defaults')) {
	try {
		epc_erp_print_designer_seed_defaults($db_link);
		echo '<span style="color:green">OK</span></p>';
	} catch (\Throwable $e) {
		echo '<span style="color:red">FAILED: ' . htmlspecialchars($e->getMessage()) . '</span></p>';
	}
} else {
	echo '<span style="color:orange">skipped</span></p>';
}

// Step 7: Get variables
echo '<p>Step 7: Getting template variables... ';
try {
	$pdAction = isset($_GET['pd_action']) ? (string)$_GET['pd_action'] : 'list';
	$pdId = isset($_GET['pd_id']) ? (int)$_GET['pd_id'] : 0;
	$pdDocType = isset($_GET['pd_doctype']) ? (string)$_GET['pd_doctype'] : '';
	$csrfLocal = isset($csrf) ? $csrf : '';
	$pdBase = epc_erp_tab_url($erpUrl, 'print_designer', $date_from_str, $date_to_str, 'setup');
	$docTypes = function_exists('epc_erp_print_doc_types') ? epc_erp_print_doc_types() : array();
	$mergeFields = function_exists('epc_erp_print_merge_fields') ? epc_erp_print_merge_fields() : array();
	echo '<span style="color:green">OK</span></p>';
} catch (\Throwable $e) {
	echo '<span style="color:red">FAILED: ' . htmlspecialchars($e->getMessage()) . '</span></p>';
}

// Step 8: erp_page_header
echo '<p>Step 8: Calling erp_page_header... ';
try {
	erp_page_header(
		'<i class="fa fa-paint-brush"></i> Print designer',
		'Customise voucher, invoice, PO, and report print layouts.',
		array(
			array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
			array('label' => 'System administration'),
			array('label' => 'Print designer'),
		),
		array(
			array('label' => 'New template', 'url' => $pdBase . '&pd_action=edit', 'class' => 'btn-primary', 'icon' => 'fa-plus'),
		)
	);
	echo '<span style="color:green">OK</span></p>';
} catch (\Throwable $e) {
	echo '<span style="color:red">FAILED: ' . htmlspecialchars($e->getMessage()) . '</span></p>';
}

// Step 9: List templates
echo '<p>Step 9: Listing templates... ';
if (function_exists('epc_erp_print_templates_list') && isset($db_link) && $db_link instanceof PDO) {
	try {
		$templates = epc_erp_print_templates_list($db_link, $pdDocType);
		echo '<span style="color:green">OK (' . count($templates) . ' templates)</span></p>';
	} catch (\Throwable $e) {
		echo '<span style="color:red">FAILED: ' . htmlspecialchars($e->getMessage()) . '</span></p>';
	}
} else {
	echo '<span style="color:orange">skipped</span></p>';
}

echo '<p style="color:green;font-weight:bold;">ALL STEPS COMPLETED</p>';
echo '</div>';

// Restore error settings
ini_set('display_errors', $_oldDisplayErrors);
error_reporting($_oldErrorReporting);
