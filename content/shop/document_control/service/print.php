<?php
/**
 * Document Control — print / preview renderer.
 * /content/shop/document_control/service/print.php?doc=fta_tax_invoice&order_id=123
 */
header('Content-Type: text/html; charset=utf-8');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config;

try {
	$db_link = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	http_response_code(500);
	echo 'Database error';
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin() && !DP_User::isBackendGroup()) {
	http_response_code(403);
	echo 'Access denied — log in to the control panel.';
	exit;
}

define('_ASTEXE_', 1);
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/document_control/epc_document_control_helpers.php';

$doc = trim((string)($_GET['doc'] ?? 'fta_tax_invoice'));
$order_id = (int)($_GET['order_id'] ?? 0);
$preview = !empty($_GET['preview']);

try {
	echo epc_dc_render_template($db_link, $doc, $preview ? 0 : $order_id);
} catch (Throwable $e) {
	http_response_code(400);
	echo '<p>' . htmlspecialchars($e->getMessage(), ENT_QUOTES, 'UTF-8') . '</p>';
}
