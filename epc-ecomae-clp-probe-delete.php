<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$pass = trim((string) ($_GET['clp_pass'] ?? ''));
$dom = trim((string) ($_GET['domain'] ?? 'cp.ecomae.com'));
$cookie = '';
epc_clp_web_login('admin', $pass, $cookie);
$panel = epc_clp_panel_url();
foreach (array('/delete', '/settings/delete', '/danger-zone', '/settings') as $suffix) {
	$html = epc_clp_web_request($panel . '/site/' . $dom . $suffix, array(), $cookie);
	echo $suffix . ' len=' . strlen($html);
	if (preg_match('/name="([^"]*\[_token\])" value="([^"]+)"/', $html, $m)) {
		echo ' token=' . $m[1];
	}
	if (preg_match('/action="([^"]+)"/', $html, $a)) {
		echo ' action=' . $a[1];
	}
	echo "\n";
}

echo "\nclpctl commands:\n";
foreach (array('app:list', 'site:list', 'vhost:list', '--help') as $cmd) {
	$r = epc_clp_run_cmd('/usr/bin/clpctl ' . $cmd);
	echo $cmd . ' code=' . $r['code'] . ' ' . substr($r['output'], 0, 200) . "\n";
}
