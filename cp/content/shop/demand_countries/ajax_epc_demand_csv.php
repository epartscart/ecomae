<?php
header('Content-Type: application/json;charset=utf-8;');
require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config;

try {
	$db_link = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
} catch (PDOException $e) {
	exit(json_encode(array('status' => false, 'message' => 'No DB connect')));
}
$db_link->query('SET NAMES utf8;');
$db_link->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

$pages_to_check = array();
$pages_to_check[] = array('url' => 'shop/demand_countries');
require_once $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/control/check_admin_access/check_admin_access.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
	exit(json_encode(array('status' => false, 'message' => 'Access denied')));
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_demand_intelligence.php';

$action = isset($_POST['action']) ? (string)$_POST['action'] : '';
$mode = isset($_POST['mode']) ? (string)$_POST['mode'] : 'merge';
$file_path = isset($_POST['file_full_path']) ? (string)$_POST['file_full_path'] : '';

if ($file_path === '') {
	exit(json_encode(array('status' => false, 'message' => 'Missing file path')));
}

$file_name = basename($file_path);
$expected = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/tmp/' . $file_name;
if ($expected !== $file_path || strpos($file_path, '..') !== false) {
	exit(json_encode(array('status' => false, 'message' => 'Invalid file path')));
}
if (!is_readable($file_path)) {
	exit(json_encode(array('status' => false, 'message' => 'File not found')));
}

if ($action === 'preview') {
	$result = epc_demand_csv_preview_file($file_path, 30);
	exit(json_encode($result, JSON_UNESCAPED_UNICODE));
}

if ($action === 'import') {
	$result = epc_demand_csv_import_file($db_link, $file_path, $mode === 'replace' ? 'replace' : 'merge');
	@unlink($file_path);
	exit(json_encode($result, JSON_UNESCAPED_UNICODE));
}

exit(json_encode(array('status' => false, 'message' => 'Unknown action')));
