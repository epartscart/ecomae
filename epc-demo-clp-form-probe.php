<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$cookie = '';
if ($clpPass === '' || empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("login fail\n");
}
$panel = epc_clp_panel_url();
$html = epc_clp_web_request($panel . '/site/www.ecomae.com/database/new', array(), $cookie);
echo "form len=" . strlen($html) . "\n";
if (preg_match_all('/name="([^"]+)"/', $html, $m)) {
	echo "fields:\n" . implode("\n", array_unique($m[1])) . "\n";
}
if (preg_match('/<form[^>]*action="([^"]*)"/', $html, $fm)) {
	echo "action=" . $fm[1] . "\n";
}
echo substr($html, 0, 2000) . "\n";
