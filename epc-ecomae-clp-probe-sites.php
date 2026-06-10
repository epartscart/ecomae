<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$pass = trim((string) ($_GET['clp_pass'] ?? ''));
$cookie = '';
$r = epc_clp_web_login('admin', $pass, $cookie);
echo 'login: ' . (!empty($r['ok']) ? 'OK' : 'FAIL') . "\n\n";

$panel = epc_clp_panel_url();
foreach (array('www.ecomae.com', 'cp.ecomae.com') as $dom) {
	echo "=== {$dom} ===\n";
	foreach (array('', '/settings', '/vhost', '/certificates', '/ssl', '/file-manager') as $suffix) {
		$html = epc_clp_web_request($panel . '/site/' . $dom . $suffix, array(), $cookie);
		echo "{$suffix} len=" . strlen($html);
		if (preg_match('/<title>([^<]+)</', $html, $t)) {
			echo ' title=' . trim($t[1]);
		}
		echo "\n";
	}
	$cert = epc_clp_web_request($panel . '/site/' . $dom . '/certificates', array(), $cookie);
	if (preg_match_all('/<form[^>]*action="([^"]*)"[^>]*>/', $cert, $forms)) {
		echo "forms: " . implode(', ', $forms[1]) . "\n";
	}
	if (preg_match_all('/name="([^"]*\[_token\])" value="([^"]+)"/', $cert, $tok, PREG_SET_ORDER)) {
		foreach ($tok as $t) {
			echo "token: {$t[1]}\n";
		}
	}
	if (preg_match('/lets-encrypt[^"\']*/', $cert, $le)) {
		echo "le ref: {$le[0]}\n";
	}
	echo "\n";
}

echo "paths:\n";
foreach (array(
	'/home/ecomae',
	'/home/ecomae/htdocs',
	'/home/ecomae/htdocs/www.ecomae.com',
	'/home/ecomae/htdocs/cp.ecomae.com',
) as $p) {
	echo $p . '=' . (file_exists($p) ? (is_dir($p) ? 'dir' : 'file') : 'NO') . "\n";
}

$clp = epc_clp_run('site:list');
echo "\nclpctl site:list:\n" . $clp['output'] . "\n";
