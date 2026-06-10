<?php
/**
 * Remove duplicate "ERP Login" from CMS top menu (header icon link remains in template).
 * Run: https://www.epartscart.com/epc-erp-menu-link-setup.php?token=epartscart-deploy-2026
 * Optional: action=add to restore menu item (not recommended — duplicates header link).
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_menu_link.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

$action = isset($_GET['action']) ? (string)$_GET['action'] : 'remove';
if ($action === 'add') {
	$result = epc_erp_menu_link_ensure($pdo);
	$message = 'ERP Login link added to top menu';
} else {
	$result = epc_erp_menu_link_remove($pdo);
	$message = $result['removed'] ? 'Duplicate ERP Login removed from CMS menu' : 'No ERP Login menu item found (already clean)';
}

$base = rtrim($cfg->domain_path, '/');

echo json_encode(array(
	'status' => true,
	'message' => $message,
	'action' => $action,
	'erp_content_id' => $result['erp_content_id'],
	'menu_ids' => $result['menu_ids'],
	'urls' => array(
		'portal_en' => $base . '/en/shop/erp',
		'portal' => $base . '/shop/erp',
	),
	'hint' => 'ERP Login with icon stays in header (next to Log in). Mobile bar link unchanged.',
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
