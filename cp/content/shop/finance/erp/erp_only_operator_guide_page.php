<?php
/**
 * CP route — ERP-only operator guide page wrapper.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cp_shell.php';
if (!epc_erp_is_shell_request()) {
	header('Location: ' . epc_erp_cp_shell_launcher_url() . '&area=guide&tab=erp_only_guide', true, 302);
	exit;
}

$include = $_SERVER['DOCUMENT_ROOT'] . '/' . $GLOBALS['DP_Config']->backend_dir
	. '/content/shop/finance/erp/erp_only_operator_guide.php';
if (is_file($include)) {
	include $include;
} else {
	echo '<div class="alert alert-danger">Guide file not found.</div>';
}
