<?php
/**
 * Early redirect for orphaned docpart CMS paths on www.ecomae.com marketing host.
 * Separate file so opcode cache cannot serve a stale index.php without this guard.
 */
declare(strict_types=1);

if (PHP_SAPI === 'cli') {
	return;
}
if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'GET' && ($_SERVER['REQUEST_METHOD'] ?? '') !== 'HEAD') {
	return;
}

$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
if (strpos($host, ':') !== false) {
	$host = explode(':', $host, 2)[0];
}
if (!in_array($host, array('www.ecomae.com', 'ecomae.com'), true)) {
	return;
}

$path = parse_url($_SERVER['REQUEST_URI'] ?? '/', PHP_URL_PATH);
if (!is_string($path) || $path === '') {
	return;
}
$path = '/' . trim(str_replace('\\', '/', $path), '/');
if ($path === '//') {
	$path = '/';
}

if (preg_match('#^/platform(?:/|$)#', $path) || preg_match('#^/cp(?:/|$)#', $path) || preg_match('#^/demo(?:/|$)#', $path)) {
	return;
}

$legacy = preg_match('#^/(en|ru|ar)/.+#i', $path)
	|| preg_match('#^/(akciya|aktsiya|akcia|promotion|promotions)(?:/|$)#i', $path);
if (!$legacy || headers_sent()) {
	return;
}

header('Location: /', true, 301);
header('X-Robots-Tag: noindex');
exit;
