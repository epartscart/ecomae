<?php
/**
 * Accounting Automation — alias into the Automation Centre (accounting view).
 */
defined('_ASTEXE_') or die('No access');
if (!isset($_GET['auto_view']) || (string)$_GET['auto_view'] === '') {
	$_GET['auto_view'] = 'accounting';
}
$tab = 'accounting_automation';
require __DIR__ . '/erp_tabs_workflow_automation.php';
