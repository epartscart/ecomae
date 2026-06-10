<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
if ($clpPass === '') {
	exit("clp_pass required\n");
}
$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
$panel = epc_clp_panel_url();
$html = epc_clp_web_request($panel . '/site/www.ecomae.com/database/user/new', array(), $cookie);
echo "len=" . strlen($html) . "\n";
if (preg_match_all('/<input[^>]+>/i', $html, $inputs)) {
	foreach ($inputs[0] as $inp) {
		echo $inp . "\n";
	}
}
if (preg_match_all('/<select[^>]*>.*?<\/select>/is', $html, $sel)) {
	foreach ($sel[0] as $s) {
		echo substr($s, 0, 400) . "\n---\n";
	}
}
