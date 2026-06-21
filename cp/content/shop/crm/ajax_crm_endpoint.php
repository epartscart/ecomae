<?php
/**
 * Standalone CRM AJAX endpoint — JSON only.
 */
header('Content-Type: application/json; charset=utf-8');

if (ob_get_level()) {
	ob_end_clean();
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;

try {
	$db_link = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	echo json_encode(array('status' => false, 'message' => 'Database connection failed'));
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_crm_access.php';

define('_ASTEXE_', 1);

if (!epc_crm_pack_enabled()) {
	echo json_encode(array('status' => false, 'message' => 'CRM pack not enabled for this site'));
	exit;
}

if (!epc_crm_user_can_access($db_link)) {
	echo json_encode(array('status' => false, 'message' => 'Access denied'));
	exit;
}

if ($_SERVER['REQUEST_METHOD'] !== 'POST' || empty($_POST['action'])) {
	echo json_encode(array('status' => false, 'message' => 'No action'));
	exit;
}

require __DIR__ . '/ajax_crm.php';
