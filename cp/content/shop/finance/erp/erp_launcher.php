<?php
/**
 * ERP suite entry — opens professional shell (not shop CP chrome).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cp_shell.php';

$target = epc_erp_cp_shell_launcher_url();
$qs = isset($_SERVER['QUERY_STRING']) && $_SERVER['QUERY_STRING'] !== '' ? ('&' . $_SERVER['QUERY_STRING']) : '';
if (strpos($qs, 'epc_erp_shell=') === false) {
	$qs = ($qs !== '' ? $qs . '&' : '?') . 'epc_erp_shell=1';
}
header('Location: ' . $target . $qs, true, 302);
exit;
