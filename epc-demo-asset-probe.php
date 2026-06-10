<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
header('Content-Type: application/json; charset=utf-8');
$token = (string) ($_GET['token'] ?? '');
if ($token === '' || !hash_equals(epc_deploy_token(), $token)) {
	http_response_code(403);
	exit(json_encode(array('ok' => false)));
}
$doc = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
$paths = array(
	'/content/general_pages/epc_automotive_spareparts.css',
	'/templates/nero/assets/css/style_all.css',
	'/templates/nero/css/astself.css',
	'/modules/slider/css/style.css',
	'/content/files/images/catalogue_images/62.png',
);
$out = array('docroot' => $doc, 'files' => array());
foreach ($paths as $rel) {
	$full = $doc . $rel;
	$out['files'][$rel] = array(
		'exists' => is_file($full),
		'size' => is_file($full) ? filesize($full) : 0,
		'readable' => is_readable($full),
	);
}
echo json_encode($out, JSON_PRETTY_PRINT);
