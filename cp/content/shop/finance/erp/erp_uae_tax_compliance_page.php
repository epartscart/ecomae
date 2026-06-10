<?php
/**
 * UAE tax compliance guide — CP page wrapper.
 * URL: /cp/shop/finance/erp/uae-tax-compliance
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cp_shell.php';
if (!epc_erp_is_shell_request()) {
	epc_erp_cp_shell_page_redirect();
	return;
}
$GLOBALS['epc_erp_shell_mode'] = true;

if (!isset($user_session) || !is_array($user_session)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = DP_User::getAdminSession();
}

if (empty($user_session) || !is_array($user_session)) {
	$login = '/' . htmlspecialchars($GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8') . '/';
	echo '<div class="alert alert-warning">Please <a href="' . $login
		. '">log in to the control panel</a> to view this guide.</div>';
	return;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cp_shell.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_access.php';

if (!isset($db_link) || !($db_link instanceof PDO)) {
	try {
		$cfg = $GLOBALS['DP_Config'] ?? new DP_Config();
		$db_link = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
		$db_link->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
	} catch (Exception $e) {
		echo '<div class="alert alert-danger">Database unavailable.</div>';
		return;
	}
}

if (!epc_erp_user_can_access($db_link)) {
	echo '<div class="alert alert-danger">Access denied.</div>';
	return;
}

$date_from_str = isset($_GET['from']) ? (string)$_GET['from'] : date('Y-m-01');
$date_to_str = isset($_GET['to']) ? (string)$_GET['to'] : date('Y-m-d');
$date_from = strtotime($date_from_str . ' 00:00:00') ?: strtotime(date('Y-m-01'));
$date_to = strtotime($date_to_str . ' 23:59:59') ?: time();

if (!isset($epc_erp_portal)) {
	extract(epc_erp_configure_portal_urls('cp'));
} else {
	extract(epc_erp_configure_portal_urls($epc_erp_portal));
}
$erpUrl = isset($erpUrl) ? $erpUrl : ('/' . $GLOBALS['DP_Config']->backend_dir . '/shop/finance/erp');
$erpAjaxEndpoint = '/' . $GLOBALS['DP_Config']->backend_dir . '/content/shop/finance/erp/ajax_erp_endpoint.php';
$user_session = $user_session ?? array();
$csrf = isset($user_session['csrf_guard_key']) ? (string)$user_session['csrf_guard_key'] : '';

echo '<div class="col-lg-12"><h3><i class="fa fa-gavel"></i> UAE Tax Compliance</h3>';
$openInErp = epc_erp_shell_append_query(epc_erp_tab_url($erpUrl, 'tax_compliance', $date_from_str, $date_to_str, 'finance'));
echo '<p><a class="btn btn-default btn-sm" href="' . htmlspecialchars($openInErp, ENT_QUOTES, 'UTF-8') . '"><i class="fa fa-briefcase"></i> Open in ERP</a></p>';
require $_SERVER['DOCUMENT_ROOT'] . '/' . trim((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp'), '/') . '/content/shop/finance/erp/erp_tabs_tax_compliance.php';
echo '</div>';
