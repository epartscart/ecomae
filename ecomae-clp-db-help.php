<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

foreach (array(
	'/usr/bin/clpctl db --help',
	'/usr/bin/clpctl db:export --help',
	'/usr/bin/clpctl db:import --help',
) as $cmd) {
	$r = epc_clp_run_cmd($cmd);
	echo $cmd . "\n" . substr($r['output'], 0, 800) . "\n---\n";
}

$cookie = '';
$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
if ($clpPass !== '' && epc_clp_web_login('admin', $clpPass, $cookie)['ok']) {
	$html = epc_clp_web_request(epc_clp_panel_url() . '/site/www.ecomae.com/databases', array(), $cookie);
	if (preg_match_all('/href="([^"]*database[^"]*)"/', $html, $m)) {
		echo "db links:\n" . implode("\n", array_unique(array_slice($m[1], 0, 15))) . "\n";
	}
	if (preg_match('/data-database[^>]+>/', $html, $dm)) {
		echo "data: " . $dm[0] . "\n";
	}
	// follow first database detail link
	if (preg_match('#/site/www\.ecomae\.com/database/[0-9]+#', $html, $idm)) {
		$detail = epc_clp_web_request(epc_clp_panel_url() . $idm[0], array(), $cookie);
		echo "detail len=" . strlen($detail) . "\n";
		if (preg_match('/name="[^"]*password[^"]*"[^>]*value="([^"]*)"/i', $detail, $pm)) {
			echo "found pass value len=" . strlen($pm[1]) . "\n";
		}
	}
}

$paths = glob('/home/ecomae/**/.env', GLOB_BRACE) ?: array();
foreach (array(
	'/home/ecomae/.my.cnf',
	'/home/ecomae/htdocs/www.ecomae.com/config.local.php',
) as $path) {
	if (is_file($path)) {
		echo "\n{$path}:\n" . substr((string) file_get_contents($path), 0, 400) . "\n";
	}
}
