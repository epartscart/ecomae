<?php
/**
 * Demo CP session + URL scope probe — GET ?token=epartscart-deploy-2026&key=demo_260607_ap
 */
header('Content-Type: application/json; charset=utf-8');
if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	echo json_encode(array('ok' => false, 'message' => 'forbidden'));
	exit;
}

try {
	define('_ASTEXE_', 1);
	require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';

	$key = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['key'] ?? 'demo_260607_ap')));
	$sampleHtml = '<a href="/cp/shop/orders">Orders</a><a href="/cp/templates/bootstrap_admin/css/foo.css">CSS</a>';

	$GLOBALS['epc_demo_cp_context'] = true;
	$GLOBALS['epc_demo_cp_site_key'] = $key;

	$scopedPath = function_exists('epc_portal_demo_cp_scope_cp_path')
		? epc_portal_demo_cp_scope_cp_path('/cp/shop/orders', $key)
		: '';
	$scopedRewrite = function_exists('epc_portal_demo_cp_rewrite_nav_urls')
		? epc_portal_demo_cp_rewrite_nav_urls($sampleHtml)
		: $sampleHtml;
	$expected = '/cp/demo/' . $key . '/shop/orders';

	$out = array(
		'ok' => $scopedPath === $expected
			&& strpos($scopedRewrite, $expected) !== false
			&& strpos($scopedRewrite, '/cp/templates/') !== false,
		'site_key' => $key,
		'scoped_orders_path' => $scopedPath,
		'expected_orders_path' => $expected,
		'html_rewrite_with_ctx' => $scopedRewrite,
		'helpers' => array(
			'scope_cp_path' => function_exists('epc_portal_demo_cp_scope_cp_path'),
			'rewrite_nav_urls' => function_exists('epc_portal_demo_cp_rewrite_nav_urls'),
			'maybe_redirect_bare' => function_exists('epc_portal_demo_cp_maybe_redirect_bare_path'),
			'scope_cookie' => function_exists('epc_portal_demo_cp_scope_cookie_name'),
		),
	);
	echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array('ok' => false, 'error' => $e->getMessage(), 'file' => $e->getFile(), 'line' => $e->getLine()));
} catch (Exception $e) {
	http_response_code(500);
	echo json_encode(array('ok' => false, 'error' => $e->getMessage(), 'file' => $e->getFile(), 'line' => $e->getLine()));
}
