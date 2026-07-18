<?php
/**
 * Legacy print entry used by CP order card and customer my_order pages.
 * /content/shop/print_docs/service/print.php?doc_name=sales_receipt&order_id=20&csrf_guard_key=...
 */
declare(strict_types=1);

header('Content-Type: text/html; charset=utf-8');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();

try {
	$db_link = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$db_link->query('SET NAMES utf8;');
} catch (Throwable $e) {
	http_response_code(500);
	exit('Database connection error');
}

if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
	if (function_exists('epc_portal_apply_config')) {
		epc_portal_apply_config($DP_Config);
	}
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';

define('_ASTEXE_', 1);
define('_INTASK_', 1);

$user_id = (int) DP_User::getUserId();
$doc_name = preg_replace('/[^a-zA-Z0-9_\\-]/', '', (string) ($_GET['doc_name'] ?? ''));
$order_id = (int) ($_GET['order_id'] ?? 0);

if ($doc_name === '' || $order_id <= 0) {
	http_response_code(400);
	exit('doc_name and order_id are required');
}

$handlers = array(
	'sales_receipt' => __DIR__ . '/get_html_sales_receipt.php',
	'invoice_for_payment' => __DIR__ . '/get_html_invoice_for_payment.php',
);

// Russian legacy accounting docs are not shipped — fall back to sales receipt.
if (!isset($handlers[$doc_name])) {
	$doc_name = 'sales_receipt';
}

$handler = $handlers[$doc_name];
if (!is_file($handler)) {
	http_response_code(404);
	exit('Print template not found: ' . htmlspecialchars($doc_name, ENT_QUOTES, 'UTF-8'));
}

$HTML = '';
require $handler;

if ($HTML === '' || $HTML === null) {
	http_response_code(500);
	exit('Print document could not be generated');
}

echo $HTML;
