<?php
/** One-shot deploy verification — token gated. */
header('Content-Type: application/json; charset=utf-8');
if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}
define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/finance/epc_custom_shipping.php';
require_once __DIR__ . '/content/shop/finance/epc_custom_declaration_pdf_import.php';
$pdo = new PDO('mysql:host=' . (new DP_Config())->host . ';dbname=' . (new DP_Config())->db . ';charset=utf8', (new DP_Config())->user, (new DP_Config())->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
epc_cs_ensure_schema($pdo);
epc_cs_ensure_box_schema($pdo);
$tabPath = __DIR__ . '/cp/content/shop/finance/erp/erp_tabs_custom_shipping.php';
$tabSrc = is_file($tabPath) ? file_get_contents($tabPath) : '';
$idx = $pdo->query("SHOW INDEX FROM `epc_custom_shipping_declarations` WHERE Key_name = 'uq_cs_declaration_number'")->fetch(PDO::FETCH_ASSOC);
$cols = array();
$st = $pdo->query('SHOW COLUMNS FROM `epc_custom_shipping_declarations`');
while ($c = $st->fetch(PDO::FETCH_ASSOC)) {
	$cols[] = $c['Field'];
}
echo json_encode(array(
	'status' => true,
	'unique_declaration_number' => !empty($idx),
	'pdf_file_path_column' => in_array('pdf_file_path', $cols, true),
	'pdf_file_name_column' => in_array('pdf_file_name', $cols, true),
	'skip_box_keys_fn' => function_exists('epc_cs_form_skip_box_keys'),
	'attach_pdf_fn' => function_exists('epc_cs_attach_pdf_to_declaration'),
	'tab_manual_section' => strpos($tabSrc, 'Manual ERP fields') !== false,
	'tab_autofill_panel' => strpos($tabSrc, 'epc_cs_autofill_panel') !== false,
	'tab_pdf_viewer' => strpos($tabSrc, 'epc_cs_pdf_viewer_wrap') !== false,
	'tab_skip_box_keys' => strpos($tabSrc, 'skipBoxKeys') !== false,
	'ajax_pdf_token' => strpos((string) @file_get_contents(__DIR__ . '/cp/content/shop/finance/erp/ajax_erp.php'), 'pdf_token') !== false,
), JSON_PRETTY_PRINT);
