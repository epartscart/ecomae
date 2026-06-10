<?php
/**
 * Smoke-test UAE declaration PDF parser (token-gated).
 * Run: /epc-custom-shipping-pdf-test.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/content/shop/finance/epc_custom_declaration_pdf_import.php';

$sample = __DIR__ . '/content/shop/finance/dev_samples/custom/sample_declaration_text.pdf';
if (!is_file($sample)) {
	$sample = __DIR__ . '/content/shop/finance/dev_samples/custom/upload pdf.pdf';
}
if (!is_file($sample)) {
	exit(json_encode(array('status' => false, 'message' => 'Sample PDF not on server — upload dev sample first')));
}

$binary = (string) file_get_contents($sample);
if (!function_exists('epc_uae_fta_extract_text_from_pdf_binary')) {
	$taxPath = __DIR__ . '/content/shop/finance/epc_uae_tax_compliance.php';
	if (is_file($taxPath)) {
		require_once $taxPath;
	}
}
$diag = epc_cs_pdf_pdftotext_diagnostics();
$text = epc_cs_pdf_extract_text($binary);
$textValid = epc_cs_pdf_text_looks_valid($text);
try {
	$parsed = epc_cs_pdf_parse_declaration_text($text, 'Import for Re Export to Local from FZ', array(
		'allow_partial' => true,
		'pdftotext_available' => !empty($diag['available']),
	));
	$result = array(
		'status' => true,
		'message' => 'PDF parser OK',
		'boxes_mapped' => (int) ($parsed['boxes_mapped'] ?? 0),
		'declaration_type' => (string) ($parsed['declaration_type'] ?? ''),
		'line_items' => count($parsed['line_items'] ?? array()),
		'box_45_lines' => count($parsed['box_45_lines'] ?? array()),
		'box_45' => $parsed['box_45'] ?? array(),
		'box_45_fields' => $parsed['box_45'] ?? array(),
		'box_54_lines' => count($parsed['box_54_lines'] ?? array()),
		'box_45' => $parsed['box_45'] ?? array(),
		'invoice_term' => (string) ($parsed['core']['invoice_term'] ?? ''),
		'invoice_value' => (string) ($parsed['core']['invoice_value'] ?? ''),
		'customs_inspection_required' => (string) ($parsed['core']['customs_inspection_required'] ?? ''),
		'line_item_hs_codes' => array_values(array_map(function ($li) {
			return (string) ($li['hs_code'] ?? '');
		}, $parsed['line_items'] ?? array())),
		'line_items_detail' => array_values(array_map(function ($li) {
			return array(
				'hs_code' => (string) ($li['hs_code'] ?? ''),
				'description' => (string) ($li['description'] ?? ''),
				'country_of_origin' => (string) ($li['country_of_origin'] ?? ''),
				'quantity' => (string) ($li['quantity'] ?? ''),
				'weight_net' => (string) ($li['weight_net'] ?? ''),
				'weight_gross' => (string) ($li['weight_gross'] ?? ''),
			);
		}, $parsed['line_items'] ?? array())),
		'autofill_keys' => $parsed['autofill_keys'] ?? array(),
		'sample_dec_no' => $parsed['boxes']['box_01'] ?? '',
		'text_len' => strlen($text),
		'text_valid' => $textValid,
		'parse_warning' => (string) ($parsed['parse_warning'] ?? ''),
		'partial' => !empty($parsed['partial']),
		'text_preview' => mb_substr($text, 0, 400),
		'pdftotext' => $diag,
		'sample_file' => basename($sample),
	);
	echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
} catch (Throwable $e) {
	echo json_encode(array(
		'status' => false,
		'message' => $e->getMessage(),
		'text_len' => strlen($text),
		'text_valid' => $textValid,
		'text_preview' => mb_substr($text, 0, 200),
		'pdftotext' => $diag,
		'sample_file' => basename($sample),
	), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
}
