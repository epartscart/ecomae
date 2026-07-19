<?php
/**
 * Serve epc_cp_tenant_polish.css when nginx 404s /cp/templates/*.css.
 */
declare(strict_types=1);

$root = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
$path = $root . '/cp/templates/bootstrap_admin/css/epc_cp_tenant_polish.css';
if (!is_file($path)) {
	http_response_code(404);
	header('Content-Type: text/plain; charset=utf-8');
	echo 'epc_cp_tenant_polish.css missing';
	exit;
}

$ver = function_exists('epc_cp_shell_css_version') ? epc_cp_shell_css_version() : '20260719cpTenantPolish1';
$mtime = (int) filemtime($path);
$etag = '"' . md5($mtime . '|' . filesize($path) . '|' . $ver) . '"';

header('Content-Type: text/css; charset=utf-8');
header('Cache-Control: public, max-age=86400');
header('ETag: ' . $etag);

if (isset($_SERVER['HTTP_IF_NONE_MATCH']) && trim((string) $_SERVER['HTTP_IF_NONE_MATCH']) === $etag) {
	http_response_code(304);
	exit;
}

readfile($path);
