<?php
/**
 * Upload demand-countries CSV into CP tmp.
 */
header('Content-Type: application/json; charset=utf-8');
ini_set('display_errors', '0');
if (ob_get_level()) {
	@ob_end_clean();
}

try {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$DP_Config = new DP_Config();
	$GLOBALS['DP_Config'] = $DP_Config;
	$dbHost = trim((string) $DP_Config->host);
	if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
		$dbHost = '127.0.0.1';
	}
	$db_link = new PDO(
		'mysql:host=' . $dbHost . ';dbname=' . $DP_Config->db . ';charset=utf8mb4',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$GLOBALS['db_link'] = $db_link;
	$db_link->query('SET NAMES utf8mb4');
} catch (Throwable $e) {
	http_response_code(503);
	exit(json_encode(array('status' => false, 'message' => 'No DB connect')));
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'forbidden')));
}

$csrf_check_admin = true;
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';

if (empty($_FILES['csv_file']['name'])) {
	exit(json_encode(array('status' => false, 'message' => 'No file')));
}
$name = (string) $_FILES['csv_file']['name'];
if (strtolower(substr($name, -4)) !== '.csv') {
	exit(json_encode(array('status' => false, 'message' => 'Use .csv files only')));
}
if (!empty($_FILES['csv_file']['error']) && (int) $_FILES['csv_file']['error'] !== UPLOAD_ERR_OK) {
	exit(json_encode(array('status' => false, 'message' => 'Upload error')));
}

$uploaddir = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/tmp/';
if (!is_dir($uploaddir)) {
	@mkdir($uploaddir, 0755, true);
}
$safe = preg_replace('/[^a-zA-Z0-9._-]/', '_', basename($name));
$uploadfile = $uploaddir . 'epc_demand_' . time() . '_' . $safe;
if (!move_uploaded_file($_FILES['csv_file']['tmp_name'], $uploadfile)) {
	exit(json_encode(array('status' => false, 'message' => 'Could not upload file')));
}
exit(json_encode(array('status' => true, 'file_full_path' => $uploadfile)));
