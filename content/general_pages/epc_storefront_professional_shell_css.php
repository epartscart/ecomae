<?php
/**
 * Serve storefront professional-shell CSS (text/css).
 * Prefer the static extract (epc_storefront_professional_shell.css) so nginx/static
 * and PHP-FPM both succeed; fall back to extracting site_professional_shell.php.
 */
declare(strict_types=1);

header('Content-Type: text/css; charset=utf-8');
header('Cache-Control: public, max-age=3600');

$static = __DIR__ . '/epc_storefront_professional_shell.css';
if (is_file($static)) {
	$css = (string) file_get_contents($static);
	$ver = '20260805hdr2';
	$etag = '"' . md5($css . '|' . $ver) . '"';
	header('ETag: ' . $etag);
	if (isset($_SERVER['HTTP_IF_NONE_MATCH']) && trim((string) $_SERVER['HTTP_IF_NONE_MATCH']) === $etag) {
		http_response_code(304);
		exit;
	}
	echo $css;
	exit;
}

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

ob_start();
require __DIR__ . '/site_professional_shell.php';
$html = (string) ob_get_clean();

$css = '';
if (preg_match_all('/<style[^>]*>(.*?)<\/style>/is', $html, $matches)) {
	$css = implode("\n", $matches[1]);
}

if ($css === '') {
	http_response_code(500);
	echo '/* site_professional_shell styles missing */';
	exit;
}

echo $css;
