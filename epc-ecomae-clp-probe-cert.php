<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$pass = trim((string) ($_GET['clp_pass'] ?? ''));
$dom = trim((string) ($_GET['domain'] ?? 'www.epartscart.com'));
$uid = trim((string) ($_GET['uid'] ?? ''));
$cookie = '';
epc_clp_web_login('admin', $pass, $cookie);
$panel = epc_clp_panel_url();
$sitePath = '/site/' . rawurlencode($dom);
if ($uid === '') {
	$html = epc_clp_web_request($panel . $sitePath . '/certificates', array(), $cookie);
	preg_match('#/certificate/install\?uid=([a-f0-9]+)#', $html, $m);
	$uid = $m[1] ?? '';
	echo "first uid={$uid}\n";
}
if ($uid === '') {
	exit("no uid\n");
}
$url = $panel . $sitePath . '/certificate/install?uid=' . $uid;
$html = epc_clp_web_request($url, array(), $cookie);
echo "install page len=" . strlen($html) . "\n";
echo "body: " . substr($html, 0, 500) . "\n";
$headers = isset($GLOBALS['epc_clp_last_http_headers']) ? $GLOBALS['epc_clp_last_http_headers'] : array();
foreach ($headers as $h) {
	echo "H: $h\n";
}
