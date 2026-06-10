<?php
/**
 * Serve tenant marketing storefront captures when nginx does not expose new files under /content/files/images/.
 * GET ?f=tenant-{key}-storefront.png
 */
declare(strict_types=1);

$file = preg_replace('/[^a-z0-9._-]/', '', strtolower((string) ($_GET['f'] ?? '')));
if ($file === '' || strpos($file, 'tenant-') !== 0 || strpos($file, '-storefront.') === false) {
	http_response_code(404);
	exit('Not found');
}

$root = __DIR__;
$candidates = array(
	$root . '/content/files/images/' . $file,
	$root . '/content/files/images/ecomae-platform/' . $file,
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
	exit('Not found');
}

$ext = strtolower(pathinfo($path, PATHINFO_EXTENSION));
$types = array(
	'png' => 'image/png',
	'webp' => 'image/webp',
	'jpg' => 'image/jpeg',
	'jpeg' => 'image/jpeg',
);
$mime = isset($types[$ext]) ? $types[$ext] : 'application/octet-stream';
header('Content-Type: ' . $mime);
header('Cache-Control: public, max-age=86400');
header('Content-Length: ' . (string) filesize($path));
readfile($path);
exit;
