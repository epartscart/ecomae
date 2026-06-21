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

$pages_to_check = array();
$pages_to_check[] = array('url' => 'shop/demand_countries');
require_once $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/control/check_admin_access/check_admin_access.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
	exit(json_encode(array('status' => false, 'message' => 'Access denied')));
}

if (empty($_FILES['csv_file']['name'])) {
	exit(json_encode(array('status' => false, 'message' => 'No file')));
}
$name = (string)$_FILES['csv_file']['name'];
if (strtolower(substr($name, -4)) !== '.csv') {
	exit(json_encode(array('status' => false, 'message' => 'Use .csv files only')));
}

$uploaddir = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/tmp/';
if (!is_dir($uploaddir)) {
	@mkdir($uploaddir, 0755, true);
}
$uploadfile = $uploaddir . 'epc_demand_' . time() . '_' . preg_replace('/[^a-zA-Z0-9._-]/', '_', basename($name));
if (!move_uploaded_file($_FILES['csv_file']['tmp_name'], $uploadfile)) {
	exit(json_encode(array('status' => false, 'message' => 'Could not upload file')));
}
exit(json_encode(array('status' => true, 'file_full_path' => $uploadfile)));
