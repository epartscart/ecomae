<?php
/**
 * Tenant-safe proxy for epartscross fitment widget JS (storefront HTML must not expose vendor hostnames).
 */
declare(strict_types=1);

header('Content-Type: application/javascript; charset=utf-8');
header('Cache-Control: public, max-age=300');

$article = trim((string) ($_GET['n'] ?? $_GET['article'] ?? ''));
$lang = strtolower(trim((string) ($_GET['lang'] ?? 'en')));
if ($lang !== 'ru') {
	$lang = 'en';
}
if ($article === '') {
	echo '/* epartscross: missing article */';
	exit;
}

$upstream = 'https://crossbase.ru/prim/getjs/index.php?n=' . rawurlencode($article)
	. '&lang=' . rawurlencode($lang) . '&cartype=UNI';

$body = '';
if (function_exists('curl_init')) {
	$ch = curl_init($upstream);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_CONNECTTIMEOUT => 8,
		CURLOPT_TIMEOUT => 18,
		CURLOPT_USERAGENT => 'ECOM-AE-epartscross-fitment-proxy',
	));
	$body = (string) curl_exec($ch);
	curl_close($ch);
}
if ($body === '') {
	$ctx = stream_context_create(array(
		'http' => array(
			'timeout' => 18,
			'header' => "User-Agent: ECOM-AE-epartscross-fitment-proxy\r\n",
		),
	));
	$body = (string) @file_get_contents($upstream, false, $ctx);
}
if ($body === '') {
	echo '/* epartscross fitment temporarily unavailable */';
	exit;
}
echo $body;
