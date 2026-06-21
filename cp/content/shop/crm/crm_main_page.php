<?php
/**
 * Legacy route shop/crm/crm — redirect into ERP Finance CRM tab.
 */
defined('_ASTEXE_') or die('No access');

$backend = isset($GLOBALS['DP_Config']->backend_dir) ? (string) $GLOBALS['DP_Config']->backend_dir : 'cp';
$crmTab = isset($_GET['tab']) ? preg_replace('/[^a-z_]/', '', (string) $_GET['tab']) : '';
$qs = 'tab=crm';
if ($crmTab !== '') {
	$qs .= '&crm_tab=' . rawurlencode($crmTab);
}
if (!empty($_GET['from'])) {
	$qs .= '&from=' . rawurlencode((string) $_GET['from']);
}
if (!empty($_GET['to'])) {
	$qs .= '&to=' . rawurlencode((string) $_GET['to']);
}
header('Location: /' . $backend . '/shop/finance/erp?' . $qs, true, 302);
exit;

?>
