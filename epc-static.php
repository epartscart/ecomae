<?php
/**
 * Platform static asset gateway — nginx on www.ecomae.com may 404 /templates|/content before PHP.
 * GET: f=content/general_pages/epc_automotive_spareparts.css
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_static_serve.php';

$rel = str_replace('\\', '/', (string) ($_GET['f'] ?? ''));
$rel = ltrim($rel, '/');
if ($rel === '' || strpos($rel, '..') !== false) {
	http_response_code(400);
	header('Content-Type: text/plain; charset=utf-8');
	exit('Bad path');
}
if (!preg_match('#^(templates|content|modules|lib|bos|cp)/#', $rel) && !preg_match('#^favicon\.(svg|ico)$#', $rel)) {
	http_response_code(400);
	header('Content-Type: text/plain; charset=utf-8');
	exit('Bad path');
}
$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
$file = $docRoot . '/' . $rel;
if (!is_file($file) || !is_readable($file)) {
	http_response_code(404);
	header('Content-Type: text/plain; charset=utf-8');
	exit('Not found');
}
$method = (string) ($_SERVER['REQUEST_METHOD'] ?? 'GET');
header('Content-Type: ' . epc_static_serve_mime($file));
header('Content-Length: ' . (string) filesize($file));
// Callers that pass a version query string (?v=...) get a fresh URL whenever the
// underlying file changes, so those responses are safe to cache aggressively for
// a full year and mark immutable. Anything requested without a version tag keeps
// the previous, shorter cache window so an in-place file change is still picked
// up reasonably quickly.
if (isset($_GET['v']) && $_GET['v'] !== '') {
	header('Cache-Control: public, max-age=31536000, immutable');
} else {
	header('Cache-Control: public, max-age=604800');
}
header('X-EPC-Static-Serve: gateway');
if ($method === 'HEAD') {
	exit;
}
readfile($file);
exit;
