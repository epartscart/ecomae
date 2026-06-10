<?php
/**
 * Smoke test for ERP module files and schema helpers.
 * GET: token=epartscart-deploy-2026
 */
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
$root = __DIR__;
$files = array(
	'content/shop/finance/epc_erp_schema.php',
	'content/shop/finance/epc_erp_gl.php',
	'content/shop/finance/epc_erp_access.php',
	'content/shop/finance/erp_portal.php',
	'content/shop/finance/erp_guide_portal.php',
	'cp/content/shop/finance/erp/erp_tabs_accounting.php',
	'cp/content/shop/finance/erp/erp_main_page.php',
	'cp/content/shop/finance/erp/erp_guide.php',
	'cp/content/shop/finance/erp/erp_guide_page.php',
	'cp/content/shop/finance/erp/ajax_erp.php',
	'epc-erp-cp-setup.php',
);

$missing = array();
foreach ($files as $f) {
	if (!is_file($root . '/' . $f)) {
		$missing[] = $f;
	}
}

$syntax = array();
foreach ($files as $f) {
	$path = $root . '/' . $f;
	if (!is_file($path)) {
		continue;
	}
	$out = array();
	$code = 0;
	exec('php -l ' . escapeshellarg($path) . ' 2>&1', $out, $code);
	$syntax[$f] = ($code === 0) ? 'ok' : implode(' ', $out);
}

echo json_encode(array(
	'status' => empty($missing),
	'missing' => $missing,
	'syntax' => $syntax,
));
