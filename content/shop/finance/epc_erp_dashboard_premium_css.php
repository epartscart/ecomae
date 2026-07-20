<?php
/**
 * Serve Command Centre premium CSS when nginx 404s static theme CSS on ecomae.
 */
declare(strict_types=1);

$root = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
$candidates = array(
	$root . '/content/shop/finance/epc_erp_dashboard_premium.css',
	$root . '/content/shop/finance/erp/theme/erp_dashboard_premium.css',
	$root . '/cp/content/shop/finance/erp/theme/erp_dashboard_premium.css',
);
$path = '';
foreach ($candidates as $c) {
	if (is_file($c)) {
		$path = $c;
		break;
	}
}
if ($path === '') {
	http_response_code(404);
	header('Content-Type: text/plain; charset=utf-8');
	echo 'erp_dashboard_premium.css missing';
	exit;
}

$ver = '20260720colors2';
$mtime = (int) filemtime($path);
$etag = '"' . md5($mtime . '|' . filesize($path) . '|' . $ver) . '"';

header('Content-Type: text/css; charset=utf-8');
header('Cache-Control: public, max-age=604800, immutable');
header('ETag: ' . $etag);

if (isset($_SERVER['HTTP_IF_NONE_MATCH']) && trim((string) $_SERVER['HTTP_IF_NONE_MATCH']) === $etag) {
	http_response_code(304);
	exit;
}

readfile($path);
