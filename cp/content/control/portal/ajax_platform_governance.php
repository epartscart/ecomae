<?php
/**
 * Platform governance AJAX (Super CP).
 */
define('_ASTEXE_', 1);
header('Content-Type: application/json; charset=utf-8');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_platform_governance.php';

if ((int) DP_User::getAdminId() <= 0) {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Admin login required')));
}

if (!function_exists('epc_portal_is_super_cp_host') || !epc_portal_is_super_cp_host()) {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Super CP only')));
}

$pdo = epc_portal_platform_pdo();
if (!$pdo instanceof PDO) {
	$cfg = new DP_Config();
	try {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Exception $e) {
		exit(json_encode(array('status' => false, 'message' => 'DB error')));
	}
}

epc_platform_governance_db_ensure($pdo);
$action = (string) ($_POST['action'] ?? $_GET['action'] ?? '');

if ($action === 'save_rule') {
	$key = preg_replace('/[^a-z0-9_]/', '', (string) ($_POST['rule_key'] ?? ''));
	if ($key === '') {
		exit(json_encode(array('status' => false, 'message' => 'Invalid rule_key')));
	}
	$ok = epc_platform_governance_update_rule($pdo, $key, array(
		'active' => !empty($_POST['active']),
		'enforcement' => (string) ($_POST['enforcement'] ?? 'required'),
	));
	exit(json_encode(array('status' => $ok, 'rule_key' => $key)));
}

if ($action === 'list_rules') {
	exit(json_encode(array(
		'status' => true,
		'rules' => epc_platform_governance_list_rules($pdo),
	)));
}

http_response_code(400);
echo json_encode(array('status' => false, 'message' => 'Unknown action'));
