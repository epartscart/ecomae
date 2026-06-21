<?php
/**
 * CRM module — AJAX / POST actions.
 */
defined('_ASTEXE_') or die('No access');

if (!isset($GLOBALS['DP_Config'])) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$GLOBALS['DP_Config'] = new DP_Config();
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_crm_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_crm_access.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';

header('Content-Type: application/json; charset=utf-8');

function epc_crm_json($ok, $message, $extra = array())
{
	echo json_encode(array_merge(array('status' => (bool)$ok, 'message' => (string)$message), $extra));
	exit;
}

if (!isset($db_link) || !($db_link instanceof PDO)) {
	epc_crm_json(false, 'No database');
}

if (!epc_crm_pack_enabled()) {
	epc_crm_json(false, 'CRM pack not enabled');
}

if (!epc_crm_user_can_access($db_link)) {
	epc_crm_json(false, 'Access denied');
}

if ($_SERVER['REQUEST_METHOD'] !== 'POST' || empty($_POST['action'])) {
	epc_crm_json(false, 'No action');
}

$action = (string)$_POST['action'];

try {
	$r = epc_crm_handle_ajax_action($db_link, $action, $_POST);
	$msg = $r['message'] ?? 'OK';
	unset($r['message']);
	epc_crm_json(true, $msg, $r);
} catch (Exception $e) {
	epc_crm_json(false, $e->getMessage());
}
