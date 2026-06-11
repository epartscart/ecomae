<?php
/**
 * Legacy route users/customer_mgmt — redirect to standalone Customers panel.
 */
defined('_ASTEXE_') or die('No access');

$backend = isset($GLOBALS['DP_Config']->backend_dir) ? (string)$GLOBALS['DP_Config']->backend_dir : 'cp';
$target = '/' . $backend . '/shop/customer_mgmt/customer_mgmt';
if (!empty($_SERVER['QUERY_STRING'])) {
	$target .= '?' . $_SERVER['QUERY_STRING'];
}
header('Location: ' . $target, true, 301);
exit;

?>
