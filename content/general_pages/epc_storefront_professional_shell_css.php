<?php
/**
 * Serve site_professional_shell.php style blocks as text/css for ASP.NET storefront chrome.
 * PHP nero desktop.php includes the shell inline; ASP.NET loads this stylesheet instead.
 */
declare(strict_types=1);

header('Content-Type: text/css; charset=utf-8');
header('Cache-Control: public, max-age=3600');

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

$ver = '20260805hdr1';
$etag = '"' . md5($css . '|' . $ver) . '"';
header('ETag: ' . $etag);
if (isset($_SERVER['HTTP_IF_NONE_MATCH']) && trim((string) $_SERVER['HTTP_IF_NONE_MATCH']) === $etag) {
	http_response_code(304);
	exit;
}

echo $css;
