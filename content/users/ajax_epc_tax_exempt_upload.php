<?php
/**
 * B2B tax-exempt certificate upload stub (stores file + profile flag for manager review).
 */
header('Content-Type: application/json;charset=utf-8;');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_customer_trade.php';

$DP_Config = new DP_Config;
$user_id = (int)DP_User::getUserId();
$result = array('status' => false, 'message' => 'Unauthorized');

if ($user_id <= 0) {
	exit(json_encode($result));
}

try {
	$db_link = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
	$db_link->query('SET NAMES utf8;');
} catch (PDOException $e) {
	$result['message'] = 'Database unavailable';
	exit(json_encode($result));
}

$customer_type = epc_trade_profile_get($db_link, $user_id, 'epc_customer_type', '');
if ($customer_type !== 'wholesale') {
	$result['message'] = 'Tax-exempt certificate upload is available for approved wholesale accounts.';
	exit(json_encode($result));
}

if (empty($_FILES['tax_exempt_cert']) || !is_uploaded_file($_FILES['tax_exempt_cert']['tmp_name'])) {
	$result['message'] = 'Please choose a PDF or image file.';
	exit(json_encode($result));
}

$file = $_FILES['tax_exempt_cert'];
$max = 8 * 1024 * 1024;
if ((int)$file['size'] > $max) {
	$result['message'] = 'File too large (max 8 MB).';
	exit(json_encode($result));
}

$ext = strtolower(pathinfo((string)$file['name'], PATHINFO_EXTENSION));
$allowed = array('pdf', 'jpg', 'jpeg', 'png', 'webp');
if (!in_array($ext, $allowed, true)) {
	$result['message'] = 'Allowed formats: PDF, JPG, PNG, WEBP.';
	exit(json_encode($result));
}

$dir = $_SERVER['DOCUMENT_ROOT'] . '/content/files/tax_exempt/' . $user_id;
if (!is_dir($dir) && !mkdir($dir, 0755, true) && !is_dir($dir)) {
	$result['message'] = 'Could not create upload folder.';
	exit(json_encode($result));
}

$safe = 'tax_exempt_' . date('Ymd_His') . '.' . $ext;
$dest = $dir . '/' . $safe;
if (!move_uploaded_file($file['tmp_name'], $dest)) {
	$result['message'] = 'Upload failed.';
	exit(json_encode($result));
}

$rel = '/content/files/tax_exempt/' . $user_id . '/' . $safe;
epc_trade_profile_set($db_link, $user_id, 'epc_tax_exempt_cert_path', $rel);
epc_trade_profile_set($db_link, $user_id, 'epc_tax_exempt_cert_status', 'pending_review');
epc_trade_profile_set($db_link, $user_id, 'epc_tax_exempt_cert_uploaded_at', (string)time());

$result['status'] = true;
$result['message'] = 'Certificate uploaded. Our team will review it for tax-exempt checkout eligibility.';
$result['path'] = $rel;
exit(json_encode($result));
